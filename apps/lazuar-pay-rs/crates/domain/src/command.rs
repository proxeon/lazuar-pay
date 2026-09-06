//! The only legal writers of new facts. Apply owns persistence.

use crate::error::Illegal;
use crate::ids::{AttemptId, PaymentId};
use crate::money::Money;
use crate::proof::Proof;
use crate::rail::{HostedSession, RailId};
use crate::types::{Attempt, AttemptStatus, Intake, Payment, PaymentStatus};

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum Command {
    MintPayment {
        payment_id: PaymentId,
    },
    StartAttempt {
        payment_id: PaymentId,
        rail: RailId,
    },
    SessionRecorded {
        attempt_id: AttemptId,
        session: HostedSession,
    },
    SessionFailed {
        attempt_id: AttemptId,
        reason: String,
    },
    FailAttempt {
        attempt_id: AttemptId,
        reason: String,
    },
    ApplyProof {
        proof: Proof,
        received: Money,
        attempt_id: AttemptId,
    },
    ExpireClock {
        payment_id: PaymentId,
    },
    WatchTimeout {
        payment_id: PaymentId,
    },
    AdminCloseStuck {
        attempt_id: AttemptId,
    },
}

/// v1: payment Open|Processing, intake Open, no live attempt.
pub fn check_start_attempt(payment: &Payment, attempts: &[Attempt]) -> Result<(), Illegal> {
    let startable = matches!(
        payment.status,
        PaymentStatus::Open | PaymentStatus::Processing
    ) && matches!(payment.intake, Intake::Open);
    if !startable {
        return Err(Illegal::NotStartable);
    }
    if attempts.iter().any(|a| a.status.is_live()) {
        return Err(Illegal::LiveAttemptExists);
    }
    Ok(())
}

/// Session already recorded: keep (issue 007 / 011 loser reloads the URL).
pub fn session_already_recorded(attempt: &Attempt) -> bool {
    attempt.session.is_some() || attempt.status == AttemptStatus::SessionLive
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ids::{PaymentId, PublicToken, TenantId};
    use crate::money::{Currency, Money, RateLock};
    use crate::rail::{ConnectorRefs, MethodId};
    use crate::types::{ExceptionStatus, TerminalReason};
    use time::{Duration, OffsetDateTime};

    fn open_payment() -> Payment {
        let quoted = Money::from_quoted_str("10.00", Currency::MYR).unwrap();
        let now = OffsetDateTime::from_unix_timestamp(1_700_000_000).unwrap();
        Payment {
            id: PaymentId::from_u128(1),
            tenant_id: TenantId::new("t1"),
            public_token: PublicToken::new("tok"),
            quoted,
            rate: RateLock::identity(quoted, now, now + Duration::minutes(30)),
            status: PaymentStatus::Open,
            exception: ExceptionStatus::None,
            intake: Intake::Open,
            terminal_reason: TerminalReason::None,
            expires_at: now + Duration::minutes(30),
            monitoring_until: now + Duration::minutes(30),
            occupancy: None,
            version: 0,
        }
    }

    fn live_attempt() -> Attempt {
        Attempt {
            id: crate::ids::AttemptId::from_u128(2),
            payment_id: PaymentId::from_u128(1),
            rail: RailId::CHIP,
            method: MethodId::new(Currency::MYR.code, RailId::CHIP),
            quoted_rail: Money::from_quoted_str("10.00", Currency::MYR).unwrap(),
            status: AttemptStatus::SessionLive,
            session: None,
            refs: ConnectorRefs::default(),
            fail_reason: None,
            version: 0,
        }
    }

    #[test]
    fn second_live_attempt_is_illegal() {
        let p = open_payment();
        let err = check_start_attempt(&p, &[live_attempt()]).unwrap_err();
        assert_eq!(err, Illegal::LiveAttemptExists);
    }

    #[test]
    fn failed_payment_is_not_startable() {
        let mut p = open_payment();
        p.status = PaymentStatus::Failed;
        assert_eq!(
            check_start_attempt(&p, &[]).unwrap_err(),
            Illegal::NotStartable
        );
    }
}
