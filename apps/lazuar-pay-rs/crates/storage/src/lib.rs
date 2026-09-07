//! Persistence. `apply` is the only composer of fold + journal + outbox (033/02).
//! This crate must not import rail HTTP clients.

#![forbid(unsafe_code)]

pub mod apply;
pub mod error;
pub mod lease;
pub mod read;
pub mod rows;
pub mod vault;

pub use apply::{apply, ApplyCmd, ApplyOutcome, MintSpec};
pub use error::ApplyError;
pub use lease::{
    claim_deliveries, claim_expired, claim_psync, claim_refunds, defer_psync, enqueue_outbound,
    enqueue_outbound_pool, envelope, load_endpoint, mark_delivery, mark_refund, money_number,
    sweep_retention, DeliveryRow, EndpointRow, ExpireCandidate, PsyncCandidate, RefundClaim,
    RetentionCfg,
};
pub use read::{AttemptView, PaymentView};
pub use vault::{
    audit_gateway, ensure_org_settings, get_credential, latest_attempt_refs, payment_by_session,
    record_ignored_inbound, refund_amount, update_payer, upsert_billplz, upsert_chip,
    upsert_stripe, upsert_xendit, CredentialRow,
};

use sqlx::postgres::PgPool;

/// Relative to `crates/storage` (`CARGO_MANIFEST_DIR`).
pub static MIGRATOR: sqlx::migrate::Migrator = sqlx::migrate!("../../migrations");

pub async fn migrate(pool: &PgPool) -> Result<(), sqlx::migrate::MigrateError> {
    MIGRATOR.run(pool).await
}
