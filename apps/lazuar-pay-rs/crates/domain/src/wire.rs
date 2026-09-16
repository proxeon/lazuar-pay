//! TypeSpec `/v1` adapter names. Domain does not serialize.

use crate::fold::IntakeKind;
use crate::types::{PaymentStatus, TerminalReason};

/// Buyer `GET /v1/pay/{token}` and merchant checkout list.
pub fn buyer_status(status: PaymentStatus) -> &'static str {
    match status {
        PaymentStatus::Open | PaymentStatus::Processing => "open",
        PaymentStatus::Settled => "paid",
        PaymentStatus::Failed => "failed",
        PaymentStatus::Expired => "expired",
    }
}

/// Plane C envelope `type` when apply commits the projection.
pub fn outbound_event(
    kind: IntakeKind,
    status: PaymentStatus,
    reason: TerminalReason,
) -> Option<&'static str> {
    match kind {
        IntakeKind::Take => Some("payment.completed"),
        IntakeKind::ReturnLate | IntakeKind::ReturnOverCapacity => {
            // v1: `refund.created` when the refund *settles*, not when pending late_pay is inserted.
            let _ = reason;
            None
        }
        IntakeKind::Keep => match status {
            PaymentStatus::Failed => Some("payment.failed"),
            PaymentStatus::Expired => Some("checkout.expired"),
            _ => None,
        },
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn processing_is_open_on_the_wire() {
        assert_eq!(buyer_status(PaymentStatus::Processing), "open");
        assert_eq!(buyer_status(PaymentStatus::Settled), "paid");
    }

    #[test]
    fn processing_is_never_the_json_word() {
        for s in [
            PaymentStatus::Open,
            PaymentStatus::Processing,
            PaymentStatus::Settled,
            PaymentStatus::Failed,
            PaymentStatus::Expired,
        ] {
            assert_ne!(buyer_status(s), "processing");
        }
    }
}
