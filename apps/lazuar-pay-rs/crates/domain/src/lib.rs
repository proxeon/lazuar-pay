//! Payment, Attempt, Settlement, and the total fold `propose`.
//!
//! This crate has **no I/O**. Forbidden: `tokio`, `sqlx`, `axum`, `f64` on the money path.
//! Enums are exhaustive (no `#[non_exhaustive]`). Adding a variant is a major of `domain`.
//!
//! Spec: 032/07.

#![forbid(unsafe_code)]

pub mod command;
pub mod error;
pub mod fold;
pub mod ids;
pub mod journal;
pub mod money;
pub mod proof;
pub mod rail;
pub mod types;
pub mod wire;

#[cfg(test)]
mod fold_tests;
#[cfg(test)]
mod props;

pub use command::{check_start_attempt, Command};
pub use error::{Illegal, JournalError, MoneyError, ParseIdError};
pub use fold::{propose, FoldInput, IntakeKind, OccupancySnap, Projection};
pub use ids::*;
pub use journal::{Account, Dc, JournalEntry, JournalLine};
pub use money::{
    AmountPolicy, AssetId, ChainId, Currency, CurrencyCode, CurrencyKind, Integrity, Money,
    RateLock, RateSource, DISPLAY_DECIMALS,
};
pub use proof::{
    sufficiency, Binding, ConfirmPolicy, IgnoreReason, Proof, ProofSufficiency, RefundOutcome,
    RefundReason, RefundStatus, SolanaConfirm, SyncOutcome, WebhookOutcome,
};
pub use rail::{
    rail_supports_currency, ConnectorRefs, HostedSession, MethodId, RailCaps, RailId, SessionCtx,
};
pub use types::{
    Attempt, AttemptStatus, Charge, ExceptionStatus, Intake, OccupancyRef, Payment, PaymentStatus,
    Refund, ReturnKind, Settlement, SettlementState, TerminalReason,
};
pub use wire::{buyer_status, outbound_event};

/// v1 quoted amounts accept at most two decimal places, including USDC.
pub const V1_DISPLAY_DECIMALS: u8 = DISPLAY_DECIMALS;
