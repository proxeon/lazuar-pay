//! Port of `Rails/ProcessorRemote.cs` — the processor seam for refunds.
//!
//! The C# taxonomy is three-way and the distinction is load-bearing:
//! - unsupported/unconfigured rail (`InvalidOperationException`): nothing could
//!   have moved — reservation releases as `failed`.
//! - definitive no (`ProcessorRejectedException`, <500 answer): no money moved —
//!   releases as `failed`.
//! - outcome unknown (`ProcessorOutcomeUnknownException`, 5xx-after-send, timeout,
//!   connection reset): the refund MAY have executed — reservation stays `pending`
//!   for reconciliation (issue 001). Releasing it is how a retry double-refunded.

use rust_decimal::Decimal;

/// The charge snapshot a rail needs to execute a refund. Rails needing checkout
/// fields (CHIP's provider session id) receive them in Phase 4 via a checkout ref.
#[derive(Debug, Clone)]
pub struct ChargeRef {
    pub id: String,
    pub org_id: String,
    pub checkout_id: String,
    pub provider: Option<String>,
    pub provider_ref: Option<String>,
    pub amount: Decimal,
    pub currency: String,
    pub status: String,
}

#[derive(Debug, Clone, thiserror::Error)]
pub enum RefundRemoteError {
    /// Nothing could have moved at the processor.
    #[error("{0}")]
    UnsupportedRail(String),
    /// Definitive (<500) processor answer: the refund was NOT created.
    #[error("processor rejected the refund: {0}")]
    ProcessorRejected(String),
    /// 5xx-after-send / transport loss: the refund MAY have been created.
    #[error("refund outcome unknown — {0}")]
    OutcomeUnknown(String),
}

/// The seam rails implement and tests fake. `refund_id` is the deterministic
/// processor idempotency key (issue 001) — implementations MUST pass it through.
pub trait Refunder: Send + Sync {
    fn refund_charge(
        &self,
        charge: &ChargeRef,
        amount: Decimal,
        refund_id: &str,
    ) -> Result<(), RefundRemoteError>;
}
