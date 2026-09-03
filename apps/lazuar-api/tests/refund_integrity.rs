//! Refund money-path integrity — the issues/001 core, on real Postgres.
//! Test names mirror the spec's invariant inventory (plans/023-evals/04).

mod support;

use lazuar_api::domain::transitions::try_leave_open;
use lazuar_api::money::refunds::{
    create_refund, stable_refund_id, CreateRefund, CreateRefundOutcome,
};
use lazuar_api::rails::remote::{ChargeRef, RefundRemoteError, Refunder};
use rust_decimal::Decimal;
use std::str::FromStr;
use std::sync::{mpsc, Arc, Mutex};
use support::TestApp;

fn amount(v: &str) -> Decimal {
    Decimal::from_str(v).unwrap()
}

fn input(checkout_id: &str, amount: Option<Decimal>, key: Option<&str>) -> CreateRefund {
    CreateRefund {
        checkout_id: checkout_id.to_string(),
        amount,
        idempotency_key: key.map(str::to_string),
    }
}

struct OkRemote;
impl Refunder for OkRemote {
    fn refund_charge(&self, _: &ChargeRef, _: Decimal, _: &str) -> Result<(), RefundRemoteError> {
        Ok(())
    }
}

struct FixedRemote(RefundRemoteError);
impl Refunder for FixedRemote {
    fn refund_charge(&self, _: &ChargeRef, _: Decimal, _: &str) -> Result<(), RefundRemoteError> {
        Err(self.0.clone())
    }
}

/// Holds the FIRST processor call until released — deterministically parks one
/// caller inside the processor window so a second reservation can commit, which
/// is exactly the interleaving issue 010 was about.
struct GatedRemote {
    state: Arc<Mutex<GateState>>,
}

struct GateState {
    entered_tx: Option<mpsc::Sender<()>>,
    release_rx: Option<mpsc::Receiver<()>>,
    entered: bool,
}

impl GatedRemote {
    fn gated() -> (Self, mpsc::Receiver<()>, mpsc::Sender<()>) {
        let (entered_tx, entered_rx) = mpsc::channel();
        let (release_tx, release_rx) = mpsc::channel();
        (
            Self {
                state: Arc::new(Mutex::new(GateState {
                    entered_tx: Some(entered_tx),
                    release_rx: Some(release_rx),
                    entered: false,
                })),
            },
            entered_rx,
            release_tx,
        )
    }
}

impl Refunder for GatedRemote {
    fn refund_charge(&self, _: &ChargeRef, _: Decimal, _: &str) -> Result<(), RefundRemoteError> {
        let mut st = self.state.lock().unwrap();
        if !st.entered {
            st.entered = true;
            let rx = st.release_rx.take();
            let entered_tx = st.entered_tx.take();
            drop(st);
            if let Some(tx) = entered_tx {
                let _ = tx.send(());
            }
            if let Some(rx) = rx {
                let _ = rx.recv(); // parked at the processor until released
            }
        }
        Ok(())
    }
}

// ---------------------------------------------------------------------------
// Issue 001 — ambiguous outcomes hold capacity, retries replay
// ---------------------------------------------------------------------------

#[test]
fn ambiguous_refund_keeps_reservation_pending_and_retry_replays() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let checkout = support::insert_charged_checkout(&mut db, "org_1", amount("10.00"));

    // A transport loss after the processor may have executed: held pending.
    let outcome = create_refund(
        &mut db,
        "org_1",
        &input(&checkout, None, Some("r1")),
        &FixedRemote(RefundRemoteError::OutcomeUnknown("timeout after send".into())),
    )
    .unwrap();
    assert!(matches!(outcome, CreateRefundOutcome::AmbiguousOutcome), "got {outcome:?}");

    let row = db
        .query_one(
            "SELECT \"Status\" FROM public.refunds WHERE \"IdempotencyKey\" = 'r1'",
            &[],
        )
        .unwrap();
    assert_eq!(row.get::<_, String>(0), "pending");

    // Retry with the same key: replay the held row — never a second insert.
    let retry = create_refund(
        &mut db,
        "org_1",
        &input(&checkout, None, Some("r1")),
        &OkRemote,
    )
    .unwrap();
    match retry {
        CreateRefundOutcome::Replayed(view) => assert_eq!(view.status, "pending"),
        other => panic!("expected replay, got {other:?}"),
    }

    // The pending reservation still counts against the remainder: nothing else
    // is refundable while the outcome is unknown.
    let outcome = create_refund(
        &mut db,
        "org_1",
        &input(&checkout, Some(amount("1.00")), Some("r2")),
        &OkRemote,
    )
    .unwrap();
    assert!(matches!(outcome, CreateRefundOutcome::AlreadyRefunded), "got {outcome:?}");

    let count: i64 = db
        .query_one("SELECT count(*) FROM public.refunds", &[])
        .unwrap()
        .get(0);
    assert_eq!(count, 1);
}

#[test]
fn stable_refund_id_is_deterministic_per_org_and_key() {
    let a = stable_refund_id("org_1", "r1");
    let b = stable_refund_id("org_1", "r1");
    let c = stable_refund_id("org_2", "r1");
    assert_eq!(a, b, "same logical refund must reuse the processor key");
    assert_ne!(a, c, "different orgs must never share a processor key");
}

// ---------------------------------------------------------------------------
// Definitive outcomes release capacity
// ---------------------------------------------------------------------------

#[test]
fn processor_reject_releases_capacity() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let checkout = support::insert_charged_checkout(&mut db, "org_1", amount("10.00"));

    let outcome = create_refund(
        &mut db,
        "org_1",
        &input(&checkout, None, Some("r1")),
        &FixedRemote(RefundRemoteError::ProcessorRejected("card declined refund".into())),
    )
    .unwrap();
    assert!(matches!(outcome, CreateRefundOutcome::ProcessorRejected), "got {outcome:?}");
    assert_eq!(
        db.query_one("SELECT \"Status\" FROM public.refunds WHERE \"IdempotencyKey\"='r1'", &[])
            .unwrap()
            .get::<_, String>(0),
        "failed"
    );

    // Capacity released: a fresh refund of the full amount now succeeds.
    let outcome = create_refund(
        &mut db,
        "org_1",
        &input(&checkout, None, Some("r2")),
        &OkRemote,
    )
    .unwrap();
    assert!(matches!(outcome, CreateRefundOutcome::Created { .. }), "got {outcome:?}");
}

#[test]
fn unsupported_rail_releases_capacity() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let checkout = support::insert_charged_checkout(&mut db, "org_1", amount("10.00"));

    let outcome = create_refund(
        &mut db,
        "org_1",
        &input(&checkout, None, Some("r1")),
        &FixedRemote(RefundRemoteError::UnsupportedRail("unknown provider".into())),
    )
    .unwrap();
    assert!(matches!(outcome, CreateRefundOutcome::UnsupportedRail(_)), "got {outcome:?}");

    let outcome = create_refund(
        &mut db,
        "org_1",
        &input(&checkout, None, Some("r2")),
        &OkRemote,
    )
    .unwrap();
    assert!(matches!(outcome, CreateRefundOutcome::Created { .. }), "got {outcome:?}");
}

// ---------------------------------------------------------------------------
// Issue 010 — settle-time recompute under a real processor-window race
// ---------------------------------------------------------------------------

#[test]
fn concurrent_partials_converge_on_fully_refunded() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let checkout = support::insert_charged_checkout(&mut db, "org_1", amount("10.00"));

    let (gated, entered_rx, release_tx) = GatedRemote::gated();
    let gated = Arc::new(gated);

    // A reserves 5.00 and parks inside the processor.
    let app_a = app.pool.clone();
    let gated_a = gated.clone();
    let key_a = "a";
    let checkout_a = checkout.clone();
    let handle_a = std::thread::spawn(move || {
        let mut conn = app_a.get().unwrap();
        create_refund(
            &mut conn,
            "org_1",
            &input(&checkout_a, Some(amount("5.00")), Some(key_a)),
            gated_a.as_ref(),
        )
        .unwrap()
    });

    entered_rx
        .recv_timeout(std::time::Duration::from_secs(5))
        .expect("A entered the processor window");

    // B's full flow commits while A is parked: reserve 5.00, settle, recompute.
    let mut db2 = app.db();
    let outcome_b = create_refund(&mut db2, "org_1", &input(&checkout, Some(amount("5.00")), Some("b")), &OkRemote)
        .unwrap();
    assert!(matches!(outcome_b, CreateRefundOutcome::Created { .. }), "got {outcome_b:?}");

    // Release A: its settle must recompute from persisted rows — total 10 ≥ 10 —
    // and NOT stamp partially_refunded over B's committed refunded.
    drop(release_tx);
    let outcome_a = handle_a.join().unwrap();
    assert!(matches!(outcome_a, CreateRefundOutcome::Created { .. }));

    let status: String = db
        .query_one("SELECT \"Status\" FROM public.charges", &[])
        .unwrap()
        .get(0);
    assert_eq!(status, "refunded", "fully refunded charge must be labeled refunded");

    let lines: i64 = db.query_one("SELECT count(*) FROM public.journal_lines", &[]).unwrap().get(0);
    assert_eq!(lines, 4, "two refunds, two journal lines each");
}

// ---------------------------------------------------------------------------
// Issue 012 — same-key concurrent refunds answer as replay, not 500
// ---------------------------------------------------------------------------

#[test]
fn same_key_concurrent_refunds_replay_not_500() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let checkout = support::insert_charged_checkout(&mut db, "org_1", amount("10.00"));
    let pool = app.pool.clone();

    let mut handles = Vec::new();
    for _ in 0..2 {
        let pool = pool.clone();
        let checkout = checkout.clone();
        handles.push(std::thread::spawn(move || {
            let mut conn = pool.get().unwrap();
            create_refund(&mut conn, "org_1", &input(&checkout, None, Some("same")), &OkRemote).unwrap()
        }));
    }
    let outcomes: Vec<CreateRefundOutcome> = handles.into_iter().map(|h| h.join().unwrap()).collect();

    let ids: Vec<String> = outcomes
        .iter()
        .map(|o| match o {
            CreateRefundOutcome::Created { refund, .. } | CreateRefundOutcome::Replayed(refund) => {
                refund.id.clone()
            }
            other => panic!("unexpected outcome {other:?}"),
        })
        .collect();
    assert_eq!(ids[0], ids[1], "same logical refund must answer one row");

    let count: i64 = db.query_one("SELECT count(*) FROM public.refunds", &[]).unwrap().get(0);
    assert_eq!(count, 1);
}

// ---------------------------------------------------------------------------
// Issue 009 — the late_pay unique index caps refunds per checkout
// ---------------------------------------------------------------------------

#[test]
fn second_late_pay_refund_is_blocked_by_unique_index() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let checkout = support::insert_charged_checkout(&mut db, "org_1", amount("10.00"));

    let insert_late_pay = |conn: &mut postgres::Client, id: &str| {
        conn.execute(
            "INSERT INTO public.refunds \
             (\"Id\",\"OrgId\",\"CheckoutId\",\"ChargeId\",\"Amount\",\"Currency\",\"Status\",\
             \"Provider\",\"ProviderRef\",\"Reason\",\"IdempotencyKey\",\"CreatedAt\") \
             VALUES ($1,$2,$3,NULL,$4,$5,'succeeded','test',NULL,'late_pay',NULL,$6)",
            &[
                &id.to_string(),
                &"org_1",
                &checkout,
                &amount("10.00"),
                &"MYR",
                &chrono::Utc::now(),
            ],
        )
    };

    insert_late_pay(&mut db, "lp_1").unwrap();
    let second = insert_late_pay(&mut db, "lp_2");
    let err = second.expect_err("filtered unique index must reject a second late_pay refund");
    assert_eq!(err.as_db_error().unwrap().code(), &postgres::error::SqlState::UNIQUE_VIOLATION);
}

// ---------------------------------------------------------------------------
// Fully refunded charge answers AlreadyRefunded to fresh refund attempts
// ---------------------------------------------------------------------------

#[test]
fn fresh_refund_after_full_refund_conflicts() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let checkout = support::insert_charged_checkout(&mut db, "org_1", amount("10.00"));

    let first = create_refund(&mut db, "org_1", &input(&checkout, None, Some("r1")), &OkRemote).unwrap();
    assert!(matches!(first, CreateRefundOutcome::Created { .. }));
    assert_eq!(try_leave_open(&mut db, uuid::Uuid::new_v4(), "paid").unwrap(), false);

    let outcome = create_refund(&mut db, "org_1", &input(&checkout, None, Some("r2")), &OkRemote).unwrap();
    assert!(matches!(outcome, CreateRefundOutcome::AlreadyRefunded), "got {outcome:?}");
}
