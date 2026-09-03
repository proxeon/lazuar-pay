//! PublicPay start mechanics — limiter (016), Start-race conditional persist
//! (007), same-slot recovery (011), past-due retry (004), occupancy (008).

mod support;

use lazuar_api::money::fulfillment::CheckoutGates;
use lazuar_api::publicpay::gates::GateMap;
use lazuar_api::publicpay::limiter::PublicPayLimiter;
use lazuar_api::publicpay::start::{
    start, HostedRail, StartDeps, StartOutcome, StartRequest, StartRailError,
};
use rust_decimal::Decimal;
use std::str::FromStr;
use support::TestApp;

struct TestOnlyRail {
    checkout_base_url: String,
}

impl HostedRail for TestOnlyRail {
    fn create_hosted_url(
        &self,
        checkout_id: &str,
        public_token: &str,
        _org_id: &str,
    ) -> Result<lazuar_api::publicpay::start::HostedSession, StartRailError> {
        Ok(lazuar_api::publicpay::start::HostedSession {
            provider_session_id: format!("test:{checkout_id}"),
            url: format!("{}/c/{public_token}", self.checkout_base_url),
        })
    }
}

struct RejectingRail;
impl HostedRail for RejectingRail {
    fn create_hosted_url(
        &self,
        _: &str,
        _: &str,
        _: &str,
    ) -> Result<lazuar_api::publicpay::start::HostedSession, StartRailError> {
        Err(StartRailError::Rejected("psp down".into()))
    }
}

fn deps<'a>(
    app: &'a TestApp,
    limiter: &'a PublicPayLimiter,
    start_gates: &'a GateMap,
    link_gates: &'a GateMap,
    fulfill_gates: &'a CheckoutGates,
    rail: &'a dyn HostedRail,
) -> StartDeps<'a> {
    StartDeps {
        environment: "Testing",
        start_max_per_minute: 200,
        limiter,
        start_gates,
        link_gates,
        fulfill_gates,
        rail,
    }
}

fn req<'a>(email: &'a str, slot: &'a str) -> StartRequest<'a> {
    StartRequest { name: None, email: Some(email), slot_key: Some(slot) }
}

fn insert_link(db: &mut postgres::Client, org: &str, token: &str, max_payers: Option<i32>) -> String {
    let id = uuid::Uuid::new_v4().to_string();
    db.execute(
        "INSERT INTO public.payment_links \
         (\"Id\",\"OrgId\",\"PublicToken\",\"Provider\",\"Amount\",\"Currency\",\"MaxPayers\",\"CreatedAt\") \
         VALUES ($1,$2,$3,'test',$4,$5,$6,$7)",
        &[
            &id,
            &org,
            &token,
            &Decimal::from_str("9.90").unwrap(),
            &"MYR",
            &max_payers,
            &chrono::Utc::now(),
        ],
    )
    .unwrap();
    id
}

#[test]
fn concurrent_starts_mint_exactly_one_session_per_slot() {
    let app = TestApp::spawn();
    let mut db = app.db();
    insert_link(&mut db, "org_1", "link_tok_1", None);
    let pool = app.pool.clone();

    let limiter = PublicPayLimiter::new();
    let start_gates = GateMap::new();
    let link_gates = GateMap::new();
    let fulfill_gates = CheckoutGates::default();
    let rail = TestOnlyRail { checkout_base_url: "http://checkout.test".into() };

    let limiter_ref = &limiter;
    let start_gates_ref = &start_gates;
    let link_gates_ref = &link_gates;
    let fulfill_gates_ref = &fulfill_gates;
    let rail_ref: &dyn HostedRail = &rail;
    let outcomes: Vec<StartOutcome> = std::thread::scope(|scope| {
        let mut handles = Vec::new();
        for _ in 0..4 {
            let pool = pool.clone();
            handles.push(scope.spawn(move || {
                let deps = StartDeps {
                    environment: "Testing",
                    start_max_per_minute: 200,
                    limiter: limiter_ref,
                    start_gates: start_gates_ref,
                    link_gates: link_gates_ref,
                    fulfill_gates: fulfill_gates_ref,
                    rail: rail_ref,
                };
                let mut conn = pool.get().unwrap();
                start(&mut conn, &deps, "link_tok_1", &req("buyer@test", "slot-12345678")).unwrap()
            }));
        }
        handles.into_iter().map(|h| h.join().unwrap()).collect()
    });

    // Every start answers Started-with-its-URL or NotOpen (a late arrival after
    // the test rail fulfilled) — never a 500, never a second checkout.
    let mut urls: Vec<String> = Vec::new();
    for outcome in &outcomes {
        match outcome {
            StartOutcome::Started { redirect_url } => urls.push(redirect_url.clone()),
            StartOutcome::NotOpen => {}
            other => panic!("unexpected outcome {other:?}"),
        }
    }
    assert!(!urls.is_empty(), "at least one start must win");
    urls.sort();
    urls.dedup();
    assert_eq!(urls.len(), 1, "one checkout, one URL: {urls:?}");

    let mut db2 = app.db();
    let (count, status): (i64, String) = {
        let row = db2
            .query_one(
                "SELECT count(*), min(\"Status\") FROM public.checkouts WHERE \"PaymentLinkId\" IS NOT NULL",
                &[],
            )
            .unwrap();
        (row.get(0), row.get(1))
    };
    assert_eq!(count, 1, "exactly one checkout per slot");
    assert_eq!(status, "paid", "test rail fulfills instantly");
}

#[test]
fn same_slot_start_recovers_the_loser_without_a_500() {
    let app = TestApp::spawn();
    let mut db = app.db();
    insert_link(&mut db, "org_1", "link_tok_2", None);

    let limiter = PublicPayLimiter::new();
    let start_gates = GateMap::new();
    let link_gates = GateMap::new();
    let fulfill_gates = CheckoutGates::default();
    let rail = TestOnlyRail { checkout_base_url: "http://checkout.test".into() };
    let deps = deps(&app, &limiter, &start_gates, &link_gates, &fulfill_gates, &rail);

    let first = start(&mut db, &deps, "link_tok_2", &req("buyer@test", "slot-87654321")).unwrap();
    assert!(matches!(first, StartOutcome::Started { .. }));

    // A second start for the same slot after the test rail fulfilled: the slot is
    // terminal — answered as NotOpen, never a 500 (issue 011's recovery path).
    let second = start(&mut db, &deps, "link_tok_2", &req("buyer@test", "slot-87654321")).unwrap();
    assert!(matches!(second, StartOutcome::NotOpen), "got {second:?}");

    let count: i64 = db
        .query_one(
            "SELECT count(*) FROM public.checkouts WHERE \"PaymentLinkId\" IS NOT NULL",
            &[],
        )
        .unwrap()
        .get(0);
    assert_eq!(count, 1);
}

#[test]
fn limiter_sweeps_idle_keys_and_caps_key_length() {
    // Issue 016: bounded keys, sweepable idle entries.
    let limiter = PublicPayLimiter::new();
    assert!(limiter.try_acquire("k", 2, 60));
    assert!(limiter.try_acquire("k", 2, 60));
    assert!(!limiter.try_acquire("k", 2, 60), "third hit inside the window is capped");
    assert_eq!(limiter.tracked_keys(), 1);

    // A 4KB junk key is truncated, not stored whole — it cannot buy memory.
    let junk = "x".repeat(4096);
    assert!(limiter.try_acquire(&junk, 100, 60));
    assert!(limiter.try_acquire(&"x".repeat(300), 100, 60), "truncated keys collide by cap");
    assert_eq!(limiter.tracked_keys(), 2);

    // Sweep drops idle keys.
    limiter.sweep(chrono::Utc::now().timestamp() + 10);
    assert_eq!(limiter.tracked_keys(), 0);
}

#[test]
fn past_due_retry_reopens_failed_checkout() {
    let app = TestApp::spawn();
    let mut db = app.db();
    support::insert_checkout_org(&mut db, uuid::Uuid::new_v4(), "org_1", "failed");
    let checkout_id: String = db.query_one("SELECT \"Id\" FROM public.checkouts", &[]).unwrap().get(0);
    db.execute(
        "INSERT INTO public.subscriptions \
         (\"Id\",\"OrgId\",\"CheckoutId\",\"PayerId\",\"Status\",\"Interval\",\"AttemptCount\",\"PastDueAt\",\"CreatedAt\") \
         VALUES ($1,$2,$3,NULL,'past_due','mo',2,$4,$4)",
        &[
            &uuid::Uuid::new_v4().simple().to_string(),
            &"org_1",
            &checkout_id,
            &chrono::Utc::now(),
        ],
    )
    .unwrap();

    let limiter = PublicPayLimiter::new();
    let start_gates = GateMap::new();
    let link_gates = GateMap::new();
    let fulfill_gates = CheckoutGates::default();
    let rail = TestOnlyRail { checkout_base_url: "http://checkout.test".into() };
    let deps = deps(&app, &limiter, &start_gates, &link_gates, &fulfill_gates, &rail);

    // The token equals the checkout's public token — find it.
    let token: String = db.query_one("SELECT \"PublicToken\" FROM public.checkouts", &[]).unwrap().get(0);
    let outcome = start(&mut db, &deps, &token, &req("buyer@test", "slot-11111111")).unwrap();
    assert!(matches!(outcome, StartOutcome::Started { .. }), "past_due retry must reopen: {outcome:?}");
    assert_eq!(
        db.query_one("SELECT \"Status\" FROM public.checkouts", &[]).unwrap().get::<_, String>(0),
        "paid"
    );
}

#[test]
fn failed_one_off_without_subscription_stays_terminal() {
    let app = TestApp::spawn();
    let mut db = app.db();
    support::insert_checkout_org(&mut db, uuid::Uuid::new_v4(), "org_1", "failed");
    let token: String = db.query_one("SELECT \"PublicToken\" FROM public.checkouts", &[]).unwrap().get(0);

    let limiter = PublicPayLimiter::new();
    let start_gates = GateMap::new();
    let link_gates = GateMap::new();
    let fulfill_gates = CheckoutGates::default();
    let rail = TestOnlyRail { checkout_base_url: "http://checkout.test".into() };
    let deps = deps(&app, &limiter, &start_gates, &link_gates, &fulfill_gates, &rail);

    let outcome = start(&mut db, &deps, &token, &req("buyer@test", "slot-11111111")).unwrap();
    assert!(matches!(outcome, StartOutcome::NotOpen), "failed one-off stays terminal: {outcome:?}");
}

#[test]
fn start_rejection_expires_the_reservation() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let checkout_id = support::insert_charged_checkout(&mut db, "org_1", Decimal::from_str("10.00").unwrap());
    // Turn it back to open (as if the charge had not landed) with a nonexistent rail.
    db.execute(
        "UPDATE public.checkouts SET \"Status\"='open', \"Provider\"='stripe' WHERE \"Id\"=$1",
        &[&checkout_id],
    )
    .unwrap();

    let limiter = PublicPayLimiter::new();
    let start_gates = GateMap::new();
    let link_gates = GateMap::new();
    let fulfill_gates = CheckoutGates::default();
    let rail = RejectingRail;
    let deps = deps(&app, &limiter, &start_gates, &link_gates, &fulfill_gates, &rail);

    let token: String = db.query_one("SELECT \"PublicToken\" FROM public.checkouts", &[]).unwrap().get(0);
    let outcome = start(&mut db, &deps, &token, &req("buyer@test", "slot-22222222")).unwrap();
    assert!(matches!(outcome, StartOutcome::RailNotConfigured), "got {outcome:?}");

    // Issue 002: the expiry after a failed mint is a CAS — and it enqueues the
    // start_failed webhook.
    let status: String = db.query_one("SELECT \"Status\" FROM public.checkouts", &[]).unwrap().get(0);
    assert_eq!(status, "expired");
}
