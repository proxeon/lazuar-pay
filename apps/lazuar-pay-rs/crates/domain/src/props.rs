//! G1 property tests (032/07 §13).

use proptest::prelude::*;
use time::OffsetDateTime;

use crate::fold::{propose, FoldInput, IntakeKind, OccupancySnap};
use crate::ids::{AttemptId, ChargeId, PaymentId, PublicToken, RefundId, SettlementId, TenantId};
use crate::journal::JournalEntry;
use crate::money::{Currency, Money, RateLock};
use crate::proof::Proof;
use crate::rail::{ConnectorRefs, MethodId, RailId};
use crate::types::{
    Attempt, AttemptStatus, ExceptionStatus, Intake, Payment, PaymentStatus, ReturnKind,
    Settlement, SettlementState, TerminalReason,
};

fn ts(secs: i64) -> OffsetDateTime {
    OffsetDateTime::from_unix_timestamp(1_700_000_000 + secs).unwrap()
}

fn myr(cents: i64) -> Money {
    Money::from_minor(cents as i128, Currency::MYR).unwrap()
}

fn payment(status: PaymentStatus, intake: Intake, expires_at: OffsetDateTime) -> Payment {
    let quoted = myr(1000);
    Payment {
        id: PaymentId::from_u128(1),
        tenant_id: TenantId::new("t1"),
        public_token: PublicToken::new("tok"),
        quoted,
        rate: RateLock::identity(quoted, ts(0), expires_at),
        status,
        exception: ExceptionStatus::None,
        intake,
        terminal_reason: TerminalReason::None,
        expires_at,
        monitoring_until: expires_at,
        occupancy: None,
        version: 0,
    }
}

fn attempt(status: AttemptStatus) -> Attempt {
    Attempt {
        id: AttemptId::from_u128(2),
        payment_id: PaymentId::from_u128(1),
        rail: RailId::TEST,
        method: MethodId::new(Currency::MYR.code, RailId::TEST),
        quoted_rail: myr(1000),
        status,
        session: None,
        refs: ConnectorRefs::default(),
        fail_reason: None,
        version: 0,
    }
}

fn settlement(state: SettlementState, minor: i64) -> Settlement {
    Settlement {
        id: SettlementId::from_u128(3),
        attempt_id: AttemptId::from_u128(2),
        received: myr(minor),
        proof: Proof::PspWebhook {
            rail: RailId::TEST,
            event_id: crate::ids::ProofId::new("e"),
        },
        state,
    }
}

fn arb_status() -> impl Strategy<Value = PaymentStatus> {
    prop_oneof![
        Just(PaymentStatus::Open),
        Just(PaymentStatus::Processing),
        Just(PaymentStatus::Settled),
        Just(PaymentStatus::Failed),
        Just(PaymentStatus::Expired),
    ]
}

fn arb_intake() -> impl Strategy<Value = Intake> {
    prop_oneof![
        Just(Intake::Open),
        Just(Intake::Taken {
            charge_id: ChargeId::from_u128(4),
        }),
        Just(Intake::Returned {
            kind: ReturnKind::Late,
            refund_id: RefundId::from_u128(5),
        }),
        Just(Intake::Returned {
            kind: ReturnKind::OverCapacity,
            refund_id: RefundId::from_u128(6),
        }),
    ]
}

fn arb_attempt_status() -> impl Strategy<Value = AttemptStatus> {
    prop_oneof![
        Just(AttemptStatus::Created),
        Just(AttemptStatus::Pending),
        Just(AttemptStatus::SessionLive),
        Just(AttemptStatus::Failed),
        Just(AttemptStatus::Expired),
        Just(AttemptStatus::Settled),
    ]
}

fn arb_settlement_state() -> impl Strategy<Value = SettlementState> {
    prop_oneof![
        Just(SettlementState::Seen),
        Just(SettlementState::Confirmed),
        Just(SettlementState::Reorged),
        Just(SettlementState::Ignored),
    ]
}

proptest! {
    #[test]
    fn propose_is_total(
        status in arb_status(),
        intake in arb_intake(),
        full: bool,
        force: bool,
        now_off in -100i64..4000,
        exp_off in 0i64..3600,
        astatus in arb_attempt_status(),
        sstate in arb_settlement_state(),
        sminor in 0i64..2000,
    ) {
        let p = payment(status, intake, ts(exp_off));
        let a = attempt(astatus);
        let s = settlement(sstate, sminor);
        let proj = propose(&FoldInput {
            now: ts(now_off),
            payment: &p,
            attempts: &[a],
            settlements: &[s],
            occupancy: OccupancySnap { full },
            force_clock_expired: force,
        });
        let _ = proj.status;
    }

    #[test]
    fn never_settled_if_clock_expired_and_intake_open(
        full: bool,
        now_off in 0i64..100,
        astatus in arb_attempt_status(),
        sstate in arb_settlement_state(),
        sminor in 0i64..2000,
    ) {
        let p = payment(PaymentStatus::Open, Intake::Open, ts(0));
        let a = attempt(astatus);
        let s = settlement(sstate, sminor);
        let proj = propose(&FoldInput {
            now: ts(now_off),
            payment: &p,
            attempts: &[a],
            settlements: &[s],
            occupancy: OccupancySnap { full },
            force_clock_expired: now_off == 0,
        });
        if now_off >= 0 {
            prop_assert_ne!(proj.status, PaymentStatus::Settled);
        }
    }

    #[test]
    fn never_settled_if_occupancy_full_and_intake_open(
        astatus in arb_attempt_status(),
        sminor in 0i64..2000,
        sstate in arb_settlement_state(),
    ) {
        let p = payment(PaymentStatus::Open, Intake::Open, ts(3600));
        let a = attempt(astatus);
        let s = settlement(sstate, sminor);
        let proj = propose(&FoldInput {
            now: ts(0),
            payment: &p,
            attempts: &[a],
            settlements: &[s],
            occupancy: OccupancySnap { full: true },
            force_clock_expired: false,
        });
        prop_assert_ne!(proj.status, PaymentStatus::Settled);
    }

    #[test]
    fn taken_stays_settled(full: bool, force: bool, now_off in -100i64..4000) {
        let p = payment(
            PaymentStatus::Settled,
            Intake::Taken { charge_id: ChargeId::from_u128(4) },
            ts(30),
        );
        let proj = propose(&FoldInput {
            now: ts(now_off),
            payment: &p,
            attempts: &[],
            settlements: &[],
            occupancy: OccupancySnap { full },
            force_clock_expired: force,
        });
        prop_assert_eq!(proj.status, PaymentStatus::Settled);
        prop_assert_eq!(proj.intake_kind, IntakeKind::Keep);
    }

    #[test]
    fn failed_never_becomes_expired_on_clock(
        now_off in 0i64..4000,
        astatus in arb_attempt_status(),
    ) {
        let p = payment(PaymentStatus::Failed, Intake::Open, ts(0));
        let a = attempt(astatus);
        let proj = propose(&FoldInput {
            now: ts(now_off),
            payment: &p,
            attempts: &[a],
            settlements: &[],
            occupancy: OccupancySnap { full: false },
            force_clock_expired: true,
        });
        prop_assert_ne!(proj.status, PaymentStatus::Expired);
    }
}

#[test]
fn at_most_one_take_in_sequence() {
    let mut p = payment(PaymentStatus::Open, Intake::Open, ts(3600));
    let a = attempt(AttemptStatus::SessionLive);
    let s = settlement(SettlementState::Confirmed, 1000);
    let mut takes = 0;
    for _ in 0..5 {
        let proj = propose(&FoldInput {
            now: ts(0),
            payment: &p,
            attempts: std::slice::from_ref(&a),
            settlements: std::slice::from_ref(&s),
            occupancy: OccupancySnap { full: false },
            force_clock_expired: false,
        });
        if proj.intake_kind == IntakeKind::Take {
            takes += 1;
            p.intake = Intake::Taken {
                charge_id: ChargeId::from_u128(4),
            };
            p.status = PaymentStatus::Settled;
        } else {
            p.status = proj.status;
            p.exception = proj.exception;
        }
    }
    assert_eq!(takes, 1);
}

#[test]
fn at_most_one_return_in_sequence() {
    let mut p = payment(PaymentStatus::Expired, Intake::Open, ts(0));
    let s = settlement(SettlementState::Confirmed, 1000);
    let mut returns = 0;
    for _ in 0..5 {
        let proj = propose(&FoldInput {
            now: ts(4000),
            payment: &p,
            attempts: &[],
            settlements: std::slice::from_ref(&s),
            occupancy: OccupancySnap { full: false },
            force_clock_expired: false,
        });
        match proj.intake_kind {
            IntakeKind::ReturnLate | IntakeKind::ReturnOverCapacity => {
                returns += 1;
                p.intake = Intake::Returned {
                    kind: ReturnKind::Late,
                    refund_id: RefundId::from_u128(5),
                };
                p.status = proj.status;
                p.exception = proj.exception;
            }
            _ => {
                p.status = proj.status;
            }
        }
    }
    assert_eq!(returns, 1);
}

#[test]
fn intake_kind_journals_balance() {
    let amount = myr(1000);
    for kind in [
        IntakeKind::Keep,
        IntakeKind::Take,
        IntakeKind::ReturnLate,
        IntakeKind::ReturnOverCapacity,
    ] {
        let e = JournalEntry::for_intake_kind(
            kind,
            TenantId::new("t1"),
            PaymentId::from_u128(1),
            amount,
        )
        .unwrap();
        if kind == IntakeKind::Keep {
            assert!(e.is_none());
        } else {
            assert!(e.is_some());
        }
    }
}

#[test]
fn usdc_minor_is_not_times_100() {
    let m = Money::from_quoted_str("10.00", Currency::USDC_SOLANA).unwrap();
    assert_eq!(m.minor(), 10_000_000);
}
