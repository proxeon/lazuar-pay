//! Proofs and reservations. Watcher inserts proofs with no attempt_id.

use serde_json::Value;
use sqlx::{PgPool, Row};
use uuid::Uuid;

use crate::error::ApplyError;
use domain::{AttemptId, PaymentId, TenantId};

#[derive(Clone, Debug)]
pub struct ProofRow {
    pub id: Uuid,
    pub txid: String,
    pub raw: Value,
}

#[derive(Clone, Debug)]
pub struct WatchRow {
    pub reservation_id: Uuid,
    pub tenant_id: TenantId,
    pub attempt_id: AttemptId,
    pub payment_id: PaymentId,
    pub locator: String,
    pub amount_minor: i64,
    pub currency: String,
    pub exponent: i16,
    pub vault: Option<String>,
    pub environment: Option<String>,
}

pub async fn insert_proof(
    pool: &PgPool,
    chain: &str,
    txid: &str,
    raw: &str,
) -> Result<bool, ApplyError> {
    let raw_json: Value = serde_json::from_str(raw).unwrap_or(Value::Null);
    let row = sqlx::query(
        r#"
        INSERT INTO pay_rs.proofs (chain, txid, raw)
        VALUES ($1, $2, $3)
        ON CONFLICT (chain, txid) DO NOTHING
        RETURNING id
        "#,
    )
    .bind(chain)
    .bind(txid)
    .bind(raw_json)
    .fetch_optional(pool)
    .await?;
    Ok(row.is_some())
}

pub async fn claim_unbound_proofs(pool: &PgPool, limit: i64) -> Result<Vec<ProofRow>, ApplyError> {
    let mut tx = pool.begin().await?;
    let stamp = time::OffsetDateTime::now_utc();
    sqlx::query(
        r#"
        UPDATE pay_rs.proofs AS p
           SET claimed_at = $1
          FROM (
                SELECT id
                  FROM pay_rs.proofs
                 WHERE claimed_at IS NULL AND chain = 'solana'
                 ORDER BY seen_at
                 LIMIT $2
                   FOR UPDATE SKIP LOCKED
               ) pick
         WHERE p.id = pick.id
        "#,
    )
    .bind(stamp)
    .bind(limit)
    .execute(&mut *tx)
    .await?;
    let rows = sqlx::query(
        r#"
        SELECT id, txid, raw
          FROM pay_rs.proofs
         WHERE claimed_at = $1 AND chain = 'solana'
         ORDER BY seen_at
        "#,
    )
    .bind(stamp)
    .fetch_all(&mut *tx)
    .await?;
    tx.commit().await?;
    Ok(rows
        .into_iter()
        .map(|r| ProofRow {
            id: r.get("id"),
            txid: r.get("txid"),
            raw: r.try_get("raw").unwrap_or(Value::Null),
        })
        .collect())
}

pub async fn bind_proof(
    pool: &PgPool,
    proof_id: Uuid,
    attempt_id: AttemptId,
) -> Result<(), ApplyError> {
    sqlx::query("UPDATE pay_rs.proofs SET bound_attempt_id = $2 WHERE id = $1")
        .bind(proof_id)
        .bind(attempt_id.as_uuid())
        .execute(pool)
        .await?;
    Ok(())
}

pub async fn reservation_by_locator(
    pool: &PgPool,
    chain: &str,
    locator: &str,
) -> Result<Option<(TenantId, PaymentId, AttemptId)>, ApplyError> {
    let row = sqlx::query(
        r#"
        SELECT r.tenant_id, a.payment_id, r.attempt_id
          FROM pay_rs.reservations r
          JOIN pay_rs.attempts a ON a.id = r.attempt_id
         WHERE r.chain = $1 AND r.locator = $2
        "#,
    )
    .bind(chain)
    .bind(locator)
    .fetch_optional(pool)
    .await?;
    Ok(row.map(|r| {
        (
            TenantId::new(r.get::<String, _>("tenant_id")),
            PaymentId::from_uuid(r.get("payment_id")),
            AttemptId::from_uuid(r.get("attempt_id")),
        )
    }))
}

pub async fn claim_watch_reservations(
    pool: &PgPool,
    limit: i64,
) -> Result<Vec<WatchRow>, ApplyError> {
    let mut tx = pool.begin().await?;
    let stamp = time::OffsetDateTime::now_utc();
    let lease = stamp - time::Duration::seconds(2);
    sqlx::query(
        r#"
        UPDATE pay_rs.reservations AS r
           SET claimed_at = $1
          FROM (
                SELECT r.id
                  FROM pay_rs.reservations r
                  JOIN pay_rs.attempts a ON a.id = r.attempt_id
                 WHERE r.chain = 'solana'
                   AND a.status = 'session_live'
                   AND (r.claimed_at IS NULL OR r.claimed_at < $2)
                 ORDER BY r.created_at
                 LIMIT $3
                   FOR UPDATE SKIP LOCKED
               ) pick
         WHERE r.id = pick.id
        "#,
    )
    .bind(stamp)
    .bind(lease)
    .bind(limit)
    .execute(&mut *tx)
    .await?;
    let rows = sqlx::query(
        r#"
        SELECT r.id, r.tenant_id, r.attempt_id, r.locator,
               a.payment_id, p.amount_minor, p.currency, p.exponent,
               g.public_merchant_id, g.environment
          FROM pay_rs.reservations r
          JOIN pay_rs.attempts a ON a.id = r.attempt_id
          JOIN pay_rs.payments p ON p.id = a.payment_id
          LEFT JOIN pay_rs.gateway_credentials g
            ON g.tenant_id = r.tenant_id AND g.rail = 'solana'
         WHERE r.claimed_at = $1
        "#,
    )
    .bind(stamp)
    .fetch_all(&mut *tx)
    .await?;
    tx.commit().await?;
    Ok(rows
        .into_iter()
        .map(|r| WatchRow {
            reservation_id: r.get("id"),
            tenant_id: TenantId::new(r.get::<String, _>("tenant_id")),
            attempt_id: AttemptId::from_uuid(r.get("attempt_id")),
            payment_id: PaymentId::from_uuid(r.get("payment_id")),
            locator: r.get("locator"),
            amount_minor: r.get("amount_minor"),
            currency: r.get("currency"),
            exponent: r.get("exponent"),
            vault: r.try_get("public_merchant_id").ok(),
            environment: r.try_get("environment").ok(),
        })
        .collect())
}
