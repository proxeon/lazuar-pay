//! SQL text ↔ domain enums. No I/O rules here.

use domain::{
    Account, AttemptStatus, ChargeId, Currency, Dc, ExceptionStatus, Intake, Money, PaymentStatus,
    Proof, ProofId, RefundId, ReturnKind, SettlementState, TerminalReason,
};

use crate::error::ApplyError;

pub fn payment_status_sql(s: PaymentStatus) -> &'static str {
    match s {
        PaymentStatus::Open => "open",
        PaymentStatus::Processing => "processing",
        PaymentStatus::Settled => "settled",
        PaymentStatus::Failed => "failed",
        PaymentStatus::Expired => "expired",
    }
}

pub fn parse_payment_status(s: &str) -> Result<PaymentStatus, ApplyError> {
    Ok(match s {
        "open" => PaymentStatus::Open,
        "processing" => PaymentStatus::Processing,
        "settled" => PaymentStatus::Settled,
        "failed" => PaymentStatus::Failed,
        "expired" => PaymentStatus::Expired,
        _ => return Err(ApplyError::Conflict),
    })
}

pub fn exception_sql(s: ExceptionStatus) -> &'static str {
    match s {
        ExceptionStatus::None => "none",
        ExceptionStatus::Partial => "partial",
        ExceptionStatus::Over => "over",
        ExceptionStatus::Late => "late",
        ExceptionStatus::OverCapacity => "over_capacity",
    }
}

pub fn parse_exception(s: &str) -> Result<ExceptionStatus, ApplyError> {
    Ok(match s {
        "none" => ExceptionStatus::None,
        "partial" => ExceptionStatus::Partial,
        "over" => ExceptionStatus::Over,
        "late" => ExceptionStatus::Late,
        "over_capacity" => ExceptionStatus::OverCapacity,
        _ => return Err(ApplyError::Conflict),
    })
}

pub fn terminal_sql(s: TerminalReason) -> &'static str {
    match s {
        TerminalReason::None => "none",
        TerminalReason::PspFailed => "psp_failed",
        TerminalReason::WatchTimeout => "watch_timeout",
        TerminalReason::ReservationTtl => "reservation_ttl",
        TerminalReason::OverCapacity => "over_capacity",
    }
}

pub fn parse_terminal(s: &str) -> Result<TerminalReason, ApplyError> {
    Ok(match s {
        "none" => TerminalReason::None,
        "psp_failed" => TerminalReason::PspFailed,
        "watch_timeout" => TerminalReason::WatchTimeout,
        "reservation_ttl" => TerminalReason::ReservationTtl,
        "over_capacity" => TerminalReason::OverCapacity,
        _ => return Err(ApplyError::Conflict),
    })
}

pub fn attempt_status_sql(s: AttemptStatus) -> &'static str {
    match s {
        AttemptStatus::Created => "created",
        AttemptStatus::Pending => "pending",
        AttemptStatus::SessionLive => "session_live",
        AttemptStatus::Failed => "failed",
        AttemptStatus::Expired => "expired",
        AttemptStatus::Settled => "settled",
    }
}

pub fn parse_attempt_status(s: &str) -> Result<AttemptStatus, ApplyError> {
    Ok(match s {
        "created" => AttemptStatus::Created,
        "pending" => AttemptStatus::Pending,
        "session_live" => AttemptStatus::SessionLive,
        "failed" => AttemptStatus::Failed,
        "expired" => AttemptStatus::Expired,
        "settled" => AttemptStatus::Settled,
        _ => return Err(ApplyError::Conflict),
    })
}

pub fn intake_parts(
    intake: Intake,
) -> (
    &'static str,
    Option<uuid::Uuid>,
    Option<uuid::Uuid>,
    Option<&'static str>,
) {
    match intake {
        Intake::Open => ("open", None, None, None),
        Intake::Taken { charge_id } => ("taken", Some(charge_id.as_uuid()), None, None),
        Intake::Returned { kind, refund_id } => (
            "returned",
            None,
            Some(refund_id.as_uuid()),
            Some(match kind {
                ReturnKind::Late => "late",
                ReturnKind::OverCapacity => "over_capacity",
            }),
        ),
    }
}

pub fn parse_intake(
    intake: &str,
    charge_id: Option<uuid::Uuid>,
    refund_id: Option<uuid::Uuid>,
    return_kind: Option<&str>,
) -> Result<Intake, ApplyError> {
    Ok(match intake {
        "open" => Intake::Open,
        "taken" => Intake::Taken {
            charge_id: ChargeId::from_uuid(charge_id.ok_or(ApplyError::Conflict)?),
        },
        "returned" => Intake::Returned {
            kind: match return_kind {
                Some("late") => ReturnKind::Late,
                Some("over_capacity") => ReturnKind::OverCapacity,
                _ => return Err(ApplyError::Conflict),
            },
            refund_id: RefundId::from_uuid(refund_id.ok_or(ApplyError::Conflict)?),
        },
        _ => return Err(ApplyError::Conflict),
    })
}

pub fn money_from_parts(minor: i64, code: &str, exponent: i16) -> Result<Money, ApplyError> {
    let c =
        Currency::by_code(code).ok_or(ApplyError::Money(domain::MoneyError::UnknownCurrency))?;
    if i16::from(c.exponent) != exponent {
        return Err(ApplyError::Money(domain::MoneyError::UnknownCurrency));
    }
    Ok(Money::from_minor(i128::from(minor), c)?)
}

pub fn account_sql(a: Account) -> &'static str {
    match a {
        Account::Cash => "cash",
        Account::PendingSettlement => "pending_settlement",
        Account::Unearned => "unearned",
        Account::Revenue => "revenue",
        Account::RefundsPayable => "refunds_payable",
    }
}

pub fn dc_sql(d: Dc) -> &'static str {
    match d {
        Dc::Debit => "D",
        Dc::Credit => "C",
    }
}

pub fn parse_settlement_state(s: &str) -> Result<SettlementState, ApplyError> {
    Ok(match s {
        "seen" => SettlementState::Seen,
        "confirmed" => SettlementState::Confirmed,
        "reorged" => SettlementState::Reorged,
        "ignored" => SettlementState::Ignored,
        _ => return Err(ApplyError::Conflict),
    })
}

pub fn dummy_proof(kind: &str, proof_id: &str) -> Result<Proof, ApplyError> {
    let id = ProofId::new(proof_id);
    Ok(match kind {
        "psp_webhook" => Proof::PspWebhook {
            rail: domain::RailId::TEST,
            event_id: id,
        },
        "psp_sync" => Proof::PspSync {
            rail: domain::RailId::TEST,
            connector_txn_id: proof_id.to_string(),
        },
        "chain_tx" => Proof::ChainTx {
            chain: domain::ChainId::SOLANA,
            txid: id,
            confirmations: 0,
            finalized: true,
        },
        _ => return Err(ApplyError::Conflict),
    })
}

pub fn refund_reason_sql(kind: domain::fold::IntakeKind) -> Option<&'static str> {
    match kind {
        domain::fold::IntakeKind::ReturnLate => Some("late_pay"),
        domain::fold::IntakeKind::ReturnOverCapacity => Some("over_capacity"),
        _ => None,
    }
}

pub fn return_kind_from_intake(kind: domain::fold::IntakeKind) -> Option<ReturnKind> {
    match kind {
        domain::fold::IntakeKind::ReturnLate => Some(ReturnKind::Late),
        domain::fold::IntakeKind::ReturnOverCapacity => Some(ReturnKind::OverCapacity),
        _ => None,
    }
}
