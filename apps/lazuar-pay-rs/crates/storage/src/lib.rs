//! Persistence. `apply` is the only composer of fold + journal + outbox (033/02).
//! This crate must not import rail HTTP clients.

#![forbid(unsafe_code)]

pub mod apply;
pub mod error;
pub mod read;
pub mod rows;

pub use apply::{apply, ApplyCmd, ApplyOutcome, MintSpec};
pub use error::ApplyError;
pub use read::{AttemptView, PaymentView};

use sqlx::postgres::PgPool;

/// Relative to `crates/storage` (`CARGO_MANIFEST_DIR`).
pub static MIGRATOR: sqlx::migrate::Migrator = sqlx::migrate!("../../migrations");

pub async fn migrate(pool: &PgPool) -> Result<(), sqlx::migrate::MigrateError> {
    MIGRATOR.run(pool).await
}
