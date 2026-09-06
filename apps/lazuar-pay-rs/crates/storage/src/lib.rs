//! Persistence. sqlx migrate for `pay_rs` (033/01).
//!
//! `apply` is the only composer of fold + journal + outbox — **not in this crate
//! yet** (P2). This crate must not import rail HTTP clients.

#![forbid(unsafe_code)]

use sqlx::postgres::PgPool;

/// Relative to `crates/storage` (`CARGO_MANIFEST_DIR`).
pub static MIGRATOR: sqlx::migrate::Migrator = sqlx::migrate!("../../migrations");

pub async fn migrate(pool: &PgPool) -> Result<(), sqlx::migrate::MigrateError> {
    MIGRATOR.run(pool).await
}
