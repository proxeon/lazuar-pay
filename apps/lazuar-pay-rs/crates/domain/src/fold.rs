//! Total fold. No I/O. Occupancy is a snapshot the caller took under `FOR UPDATE`.

use time::OffsetDateTime;

use crate::ids::AttemptId;
use crate::money::Money;
use crate::types::{
    Attempt, AttemptStatus, ExceptionStatus, Intake, Payment, PaymentStatus, ReturnKind,
    Settlement, SettlementState, TerminalReason,
};

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct OccupancySnap {
    pub full: bool,
}

#[derive(Clone, Debug)]
pub struct FoldInput<'a> {
    pub now: OffsetDateTime,
    pub payment: &'a Payment,
    pub attempts: &'a [Attempt],
    pub settlements: &'a [Settlement],
    pub occupancy: OccupancySnap,
    /// Link-child watch-timeout: treat as clock expiry even if `now < expires_at`.
    pub force_clock_expired: bool,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum IntakeKind {
    Keep,
    Take,
    ReturnLate,
    ReturnOverCapacity,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Projection {
    pub status: PaymentStatus,
    pub exception: ExceptionStatus,
    pub intake: Intake,
    pub intake_kind: IntakeKind,
    pub terminal_reason: TerminalReason,
    pub attempt_updates: Vec<(AttemptId, AttemptStatus)>,
}

pub fn propose(input: &FoldInput<'_>) -> Projection {
    let clock_expired = input.force_clock_expired || input.now >= input.payment.expires_at;
    let confirmed = confirmed_sum(input.payment.quoted, input.settlements);
    let exact = confirmed == input.payment.quoted;
    let seen_unconfirmed = input
        .settlements
        .iter()
        .any(|s| s.state == SettlementState::Seen);
    let any_live = input.attempts.iter().any(|a| a.status.is_live());
    let any_failed = input
        .attempts
        .iter()
        .any(|a| a.status == AttemptStatus::Failed);
    let p = input.payment;

    // S. Sticky terminals
    match p.intake {
        Intake::Taken { .. } => {
            return sticky(
                PaymentStatus::Settled,
                ExceptionStatus::None,
                p.intake,
                TerminalReason::None,
            );
        }
        Intake::Returned {
            kind: ReturnKind::OverCapacity,
            ..
        } => {
            return sticky(
                PaymentStatus::Expired,
                ExceptionStatus::OverCapacity,
                p.intake,
                TerminalReason::OverCapacity,
            );
        }
        Intake::Returned {
            kind: ReturnKind::Late,
            ..
        } => {
            return sticky(p.status, ExceptionStatus::Late, p.intake, p.terminal_reason);
        }
        Intake::Open => {}
    }

    if p.status == PaymentStatus::Settled {
        return sticky(
            PaymentStatus::Settled,
            ExceptionStatus::None,
            p.intake,
            TerminalReason::None,
        );
    }

    if p.status == PaymentStatus::Failed && exact {
        return Projection {
            status: PaymentStatus::Failed,
            exception: ExceptionStatus::Late,
            intake: p.intake,
            intake_kind: IntakeKind::ReturnLate,
            terminal_reason: p.terminal_reason,
            attempt_updates: expire_live(input.attempts),
        };
    }

    if p.status == PaymentStatus::Expired && exact {
        return Projection {
            status: PaymentStatus::Expired,
            exception: ExceptionStatus::Late,
            intake: p.intake,
            intake_kind: IntakeKind::ReturnLate,
            terminal_reason: p.terminal_reason,
            attempt_updates: expire_live(input.attempts),
        };
    }

    let openish = matches!(p.status, PaymentStatus::Open | PaymentStatus::Processing);
    if !openish {
        return keep(p);
    }

    // O. Open / Processing, exact confirmed
    if exact {
        if clock_expired {
            return Projection {
                status: PaymentStatus::Expired,
                exception: ExceptionStatus::Late,
                intake: p.intake,
                intake_kind: IntakeKind::ReturnLate,
                terminal_reason: TerminalReason::ReservationTtl,
                attempt_updates: expire_live(input.attempts),
            };
        }
        if input.occupancy.full {
            return Projection {
                status: PaymentStatus::Expired,
                exception: ExceptionStatus::OverCapacity,
                intake: p.intake,
                intake_kind: IntakeKind::ReturnOverCapacity,
                terminal_reason: TerminalReason::OverCapacity,
                attempt_updates: expire_live(input.attempts),
            };
        }
        return Projection {
            status: PaymentStatus::Settled,
            exception: ExceptionStatus::None,
            intake: p.intake,
            intake_kind: IntakeKind::Take,
            terminal_reason: TerminalReason::None,
            attempt_updates: settle_confirmed(input.attempts, input.settlements),
        };
    }

    // F. no confirmed money
    if confirmed.minor() == 0 {
        if clock_expired {
            return Projection {
                status: PaymentStatus::Expired,
                exception: ExceptionStatus::None,
                intake: p.intake,
                intake_kind: IntakeKind::Keep,
                terminal_reason: TerminalReason::ReservationTtl,
                attempt_updates: expire_live(input.attempts),
            };
        }
        if seen_unconfirmed {
            return Projection {
                status: PaymentStatus::Processing,
                exception: ExceptionStatus::None,
                intake: p.intake,
                intake_kind: IntakeKind::Keep,
                terminal_reason: TerminalReason::None,
                attempt_updates: vec![],
            };
        }
        if !any_live && any_failed {
            let reason = input
                .attempts
                .iter()
                .find(|a| a.status == AttemptStatus::Failed)
                .and_then(|a| a.fail_reason)
                .unwrap_or(TerminalReason::PspFailed);
            return Projection {
                status: PaymentStatus::Failed,
                exception: ExceptionStatus::None,
                intake: p.intake,
                intake_kind: IntakeKind::Keep,
                terminal_reason: reason,
                attempt_updates: vec![],
            };
        }
        return Projection {
            status: PaymentStatus::Open,
            exception: ExceptionStatus::None,
            intake: p.intake,
            intake_kind: IntakeKind::Keep,
            terminal_reason: TerminalReason::None,
            attempt_updates: vec![],
        };
    }

    // Unreachable in v1 Exact (property tests still cover).
    if confirmed < p.quoted {
        if clock_expired {
            return Projection {
                status: PaymentStatus::Expired,
                exception: ExceptionStatus::Late,
                intake: p.intake,
                intake_kind: IntakeKind::ReturnLate,
                terminal_reason: TerminalReason::ReservationTtl,
                attempt_updates: expire_live(input.attempts),
            };
        }
        return Projection {
            status: PaymentStatus::Processing,
            exception: ExceptionStatus::Partial,
            intake: p.intake,
            intake_kind: IntakeKind::Keep,
            terminal_reason: TerminalReason::None,
            attempt_updates: vec![],
        };
    }

    // confirmed > quoted
    if clock_expired {
        return Projection {
            status: PaymentStatus::Expired,
            exception: ExceptionStatus::Late,
            intake: p.intake,
            intake_kind: IntakeKind::ReturnLate,
            terminal_reason: TerminalReason::ReservationTtl,
            attempt_updates: expire_live(input.attempts),
        };
    }
    if input.occupancy.full {
        return Projection {
            status: PaymentStatus::Expired,
            exception: ExceptionStatus::OverCapacity,
            intake: p.intake,
            intake_kind: IntakeKind::ReturnOverCapacity,
            terminal_reason: TerminalReason::OverCapacity,
            attempt_updates: expire_live(input.attempts),
        };
    }
    Projection {
        status: PaymentStatus::Settled,
        exception: ExceptionStatus::Over,
        intake: p.intake,
        intake_kind: IntakeKind::Take,
        terminal_reason: TerminalReason::None,
        attempt_updates: settle_confirmed(input.attempts, input.settlements),
    }
}

fn sticky(
    status: PaymentStatus,
    exception: ExceptionStatus,
    intake: Intake,
    terminal_reason: TerminalReason,
) -> Projection {
    Projection {
        status,
        exception,
        intake,
        intake_kind: IntakeKind::Keep,
        terminal_reason,
        attempt_updates: vec![],
    }
}

fn keep(p: &Payment) -> Projection {
    Projection {
        status: p.status,
        exception: p.exception,
        intake: p.intake,
        intake_kind: IntakeKind::Keep,
        terminal_reason: p.terminal_reason,
        attempt_updates: vec![],
    }
}

fn confirmed_sum(quoted: Money, settlements: &[Settlement]) -> Money {
    let mut sum = Money::zero(quoted.currency());
    for s in settlements {
        if s.state != SettlementState::Confirmed {
            continue;
        }
        if s.received.currency() != quoted.currency() {
            continue;
        }
        match sum.checked_add(s.received) {
            Ok(v) => sum = v,
            Err(_) => return quoted.saturating_one_more(),
        }
    }
    sum
}

fn expire_live(attempts: &[Attempt]) -> Vec<(AttemptId, AttemptStatus)> {
    attempts
        .iter()
        .filter(|a| a.status.is_live())
        .map(|a| (a.id, AttemptStatus::Expired))
        .collect()
}

fn settle_confirmed(
    attempts: &[Attempt],
    settlements: &[Settlement],
) -> Vec<(AttemptId, AttemptStatus)> {
    let confirmed: Vec<AttemptId> = settlements
        .iter()
        .filter(|s| s.state == SettlementState::Confirmed)
        .map(|s| s.attempt_id)
        .collect();
    attempts
        .iter()
        .filter_map(|a| {
            if confirmed.contains(&a.id) && a.status != AttemptStatus::Settled {
                Some((a.id, AttemptStatus::Settled))
            } else if a.status.is_live() && !confirmed.contains(&a.id) {
                Some((a.id, AttemptStatus::Expired))
            } else {
                None
            }
        })
        .collect()
}
