//! Worked examples from 032/07 §11. Amounts MYR 10.00 unless noted.

use time::{Duration, OffsetDateTime};

use crate::fold::{propose, FoldInput, IntakeKind, OccupancySnap};
use crate::ids::{
    AttemptId, ChargeId, PaymentId, PaymentLinkId, ProofId, PublicToken, RefundId, SettlementId,
    TenantId,
};
use crate::money::{integrity, AmountPolicy, Currency, Integrity, Money, RateLock};
use crate::proof::Proof;
use crate::rail::{ConnectorRefs, HostedSession, MethodId, RailId};
use crate::types::{
    Attempt, AttemptStatus, ExceptionStatus, Intake, OccupancyRef, Payment, PaymentStatus,
    ReturnKind, Settlement, SettlementState, TerminalReason,
};

const T0: i64 = 1_700_000_000;

fn now() -> OffsetDateTime {
    OffsetDateTime::from_unix_timestamp(T0).unwrap()
}

fn later() -> OffsetDateTime {
    now() + Duration::hours(1)
}

fn myr10() -> Money {
    Money::from_quoted_str("10.00", Currency::MYR).unwrap()
}

fn payment_open(occupancy: bool) -> Payment {
    let quoted = myr10();
    let n = now();
    Payment {
        id: PaymentId::from_u128(1),
        tenant_id: TenantId::new("t1"),
        public_token: PublicToken::new("tok"),
        quoted,
        rate: RateLock::identity(quoted, n, n + Duration::minutes(30)),
        status: PaymentStatus::Open,
        exception: ExceptionStatus::None,
        intake: Intake::Open,
        terminal_reason: TerminalReason::None,
        expires_at: n + Duration::minutes(30),
        monitoring_until: n + Duration::minutes(30),
        occupancy: occupancy.then_some(OccupancyRef {
            link_id: PaymentLinkId::from_u128(9),
            slot_key: None,
        }),
        version: 0,
    }
}

fn chip_live() -> Attempt {
    Attempt {
        id: AttemptId::from_u128(2),
        payment_id: PaymentId::from_u128(1),
        rail: RailId::CHIP,
        method: MethodId::new(Currency::MYR.code, RailId::CHIP),
        quoted_rail: myr10(),
        status: AttemptStatus::SessionLive,
        session: Some(HostedSession {
            url: "https://gate.chip-in.asia/p".into(),
            session_id: "pur_1".into(),
        }),
        refs: ConnectorRefs {
            session_id: Some("pur_1".into()),
            ..ConnectorRefs::default()
        },
        fail_reason: None,
        version: 1,
    }
}

fn confirmed(received: Money) -> Settlement {
    Settlement {
        id: SettlementId::from_u128(3),
        attempt_id: AttemptId::from_u128(2),
        received,
        proof: Proof::PspWebhook {
            rail: RailId::CHIP,
            event_id: ProofId::new("evt_1"),
        },
        state: SettlementState::Confirmed,
    }
}

fn run(
    payment: &Payment,
    attempts: &[Attempt],
    settlements: &[Settlement],
    full: bool,
    t: OffsetDateTime,
    force: bool,
) -> crate::fold::Projection {
    propose(&FoldInput {
        now: t,
        payment,
        attempts,
        settlements,
        occupancy: OccupancySnap { full },
        force_clock_expired: force,
    })
}

#[test]
fn e1_chip_happy_path() {
    let p = payment_open(false);
    let a = chip_live();
    let s = confirmed(myr10());
    let proj = run(&p, &[a], &[s], false, now(), false);
    assert_eq!(proj.status, PaymentStatus::Settled);
    assert_eq!(proj.intake_kind, IntakeKind::Take);
    assert_eq!(proj.exception, ExceptionStatus::None);
}

#[test]
fn e1_second_proof_after_taken_is_s1_keep() {
    let mut p = payment_open(false);
    p.status = PaymentStatus::Settled;
    p.intake = Intake::Taken {
        charge_id: ChargeId::from_u128(4),
    };
    let proj = run(&p, &[], &[confirmed(myr10())], false, now(), false);
    assert_eq!(proj.status, PaymentStatus::Settled);
    assert_eq!(proj.intake_kind, IntakeKind::Keep);
}

#[test]
fn e2_amount_mismatch_no_settlement() {
    let q = myr10();
    let r = Money::from_quoted_str("9.00", Currency::MYR).unwrap();
    assert_eq!(
        integrity(q, r, AmountPolicy::Exact),
        Integrity::AmountMismatch
    );
}

#[test]
fn e4_late_money_on_expired() {
    let mut p = payment_open(false);
    p.status = PaymentStatus::Expired;
    p.terminal_reason = TerminalReason::ReservationTtl;
    let proj = run(
        &p,
        &[chip_live()],
        &[confirmed(myr10())],
        false,
        later(),
        false,
    );
    assert_eq!(proj.status, PaymentStatus::Expired);
    assert_eq!(proj.exception, ExceptionStatus::Late);
    assert_eq!(proj.intake_kind, IntakeKind::ReturnLate);
}

#[test]
fn e4_second_late_is_s3_keep() {
    let mut p = payment_open(false);
    p.status = PaymentStatus::Expired;
    p.exception = ExceptionStatus::Late;
    p.intake = Intake::Returned {
        kind: ReturnKind::Late,
        refund_id: RefundId::from_u128(8),
    };
    let proj = run(&p, &[], &[confirmed(myr10())], false, later(), false);
    assert_eq!(proj.intake_kind, IntakeKind::Keep);
    assert_eq!(proj.exception, ExceptionStatus::Late);
}

#[test]
fn e5_over_capacity_while_open() {
    let p = payment_open(true);
    let proj = run(
        &p,
        &[chip_live()],
        &[confirmed(myr10())],
        true,
        now(),
        false,
    );
    assert_eq!(proj.status, PaymentStatus::Expired);
    assert_eq!(proj.exception, ExceptionStatus::OverCapacity);
    assert_eq!(proj.intake_kind, IntakeKind::ReturnOverCapacity);
}

#[test]
fn e6_free_slot_after_expiry_is_late_not_take() {
    let mut p = payment_open(true);
    p.status = PaymentStatus::Expired;
    p.terminal_reason = TerminalReason::ReservationTtl;
    let proj = run(
        &p,
        &[chip_live()],
        &[confirmed(myr10())],
        false,
        later(),
        false,
    );
    assert_eq!(proj.status, PaymentStatus::Expired);
    assert_eq!(proj.exception, ExceptionStatus::Late);
    assert_eq!(proj.intake_kind, IntakeKind::ReturnLate);
    assert_ne!(proj.intake_kind, IntakeKind::Take);
}

#[test]
fn e7_failed_then_late_capture() {
    let mut p = payment_open(false);
    p.status = PaymentStatus::Failed;
    p.terminal_reason = TerminalReason::PspFailed;
    let mut a = chip_live();
    a.status = AttemptStatus::Failed;
    a.fail_reason = Some(TerminalReason::PspFailed);
    let proj = run(&p, &[a], &[confirmed(myr10())], false, now(), false);
    assert_eq!(proj.status, PaymentStatus::Failed);
    assert_eq!(proj.exception, ExceptionStatus::Late);
    assert_eq!(proj.intake_kind, IntakeKind::ReturnLate);
}

#[test]
fn e8_standalone_watch_timeout_is_failed() {
    let p = payment_open(false);
    let mut a = chip_live();
    a.status = AttemptStatus::Failed;
    a.fail_reason = Some(TerminalReason::WatchTimeout);
    let proj = run(&p, &[a], &[], false, now(), false);
    assert_eq!(proj.status, PaymentStatus::Failed);
    assert_eq!(proj.terminal_reason, TerminalReason::WatchTimeout);
    assert_eq!(proj.intake_kind, IntakeKind::Keep);
}

#[test]
fn e8_link_child_watch_timeout_is_expired() {
    let p = payment_open(true);
    let proj = run(&p, &[chip_live()], &[], false, now(), true);
    assert_eq!(proj.status, PaymentStatus::Expired);
    assert_eq!(proj.terminal_reason, TerminalReason::ReservationTtl);
}

#[test]
fn e15_seen_is_processing_json_still_open() {
    let p = payment_open(false);
    let s = Settlement {
        id: SettlementId::from_u128(3),
        attempt_id: AttemptId::from_u128(2),
        received: myr10(),
        proof: Proof::ChainTx {
            chain: crate::money::ChainId::SOLANA,
            txid: ProofId::new("sig"),
            confirmations: 1,
            finalized: false,
        },
        state: SettlementState::Seen,
    };
    let proj = run(&p, &[chip_live()], &[s], false, now(), false);
    assert_eq!(proj.status, PaymentStatus::Processing);
    assert_eq!(crate::wire::buyer_status(proj.status), "open");
}

#[test]
fn f1_ttl_no_money() {
    let p = payment_open(false);
    let proj = run(&p, &[chip_live()], &[], false, later(), false);
    assert_eq!(proj.status, PaymentStatus::Expired);
    assert_eq!(proj.intake_kind, IntakeKind::Keep);
    assert_eq!(proj.attempt_updates[0].1, AttemptStatus::Expired);
}

#[test]
fn o1_before_o2_expired_never_fulfills() {
    let p = payment_open(true);
    let proj = run(
        &p,
        &[chip_live()],
        &[confirmed(myr10())],
        false,
        later(),
        false,
    );
    assert_eq!(proj.intake_kind, IntakeKind::ReturnLate);
    assert_eq!(proj.status, PaymentStatus::Expired);
}
