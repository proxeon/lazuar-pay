//! Read models for the TypeSpec adapter. GET does not re-fold (07).

use domain::{AttemptId, Money, PaymentId, PaymentStatus, PublicToken, TenantId};
use sqlx::{PgPool, Row};
use time::OffsetDateTime;

use crate::error::ApplyError;
use crate::rows;

#[derive(Clone, Debug)]
pub struct PaymentView {
    pub id: PaymentId,
    pub tenant_id: TenantId,
    pub public_token: PublicToken,
    pub quoted: Money,
    pub status: PaymentStatus,
    pub expires_at: OffsetDateTime,
    pub created_at: OffsetDateTime,
    pub success_url: Option<String>,
    pub cancel_url: Option<String>,
    pub payer_name: Option<String>,
    pub payer_email: Option<String>,
    /// Live or last attempt rail, if any.
    pub provider: Option<String>,
    pub session_url: Option<String>,
    pub session_id: Option<String>,
    pub attempt_id: Option<AttemptId>,
    pub payment_link_id: Option<uuid::Uuid>,
    pub slot_key: Option<String>,
}

pub async fn payment_by_id(
    pool: &PgPool,
    id: PaymentId,
) -> Result<Option<PaymentView>, ApplyError> {
    load(
        pool,
        "SELECT p.*, a.rail AS attempt_rail, a.session_url, a.session_id, a.id AS attempt_id
           FROM pay_rs.payments p
           LEFT JOIN LATERAL (
             SELECT rail, session_url, session_id, id
               FROM pay_rs.attempts
              WHERE payment_id = p.id
              ORDER BY created_at DESC
              LIMIT 1
           ) a ON true
          WHERE p.id = $1",
        id.as_uuid(),
    )
    .await
}

pub async fn payment_by_public_token(
    pool: &PgPool,
    token: &str,
) -> Result<Option<PaymentView>, ApplyError> {
    let row = sqlx::query("SELECT p.id FROM pay_rs.payments p WHERE p.public_token = $1")
        .bind(token)
        .fetch_optional(pool)
        .await?;
    let Some(row) = row else {
        return Ok(None);
    };
    let id: uuid::Uuid = row.try_get("id")?;
    payment_by_id(pool, PaymentId::from_uuid(id)).await
}

#[derive(Clone, Debug)]
pub struct AttemptView {
    pub id: AttemptId,
    pub rail: String,
    pub session_url: Option<String>,
    pub session_id: Option<String>,
    pub status: String,
}

/// Live/last rail for GET. Does not re-fold.
pub async fn attempts_for(
    pool: &PgPool,
    payment_id: PaymentId,
) -> Result<Vec<AttemptView>, ApplyError> {
    let rows = sqlx::query(
        "SELECT id, rail, session_url, session_id, status
           FROM pay_rs.attempts
          WHERE payment_id = $1
          ORDER BY created_at DESC",
    )
    .bind(payment_id.as_uuid())
    .fetch_all(pool)
    .await?;
    let mut out = Vec::with_capacity(rows.len());
    for row in rows {
        out.push(AttemptView {
            id: AttemptId::from_uuid(row.try_get("id")?),
            rail: row.try_get("rail")?,
            session_url: row.try_get("session_url")?,
            session_id: row.try_get("session_id")?,
            status: row.try_get("status")?,
        });
    }
    Ok(out)
}

pub async fn charges_paused(pool: &PgPool, tenant: &str) -> Result<bool, ApplyError> {
    let v: Option<bool> =
        sqlx::query_scalar("SELECT charges_paused FROM pay_rs.org_settings WHERE tenant_id = $1")
            .bind(tenant)
            .fetch_optional(pool)
            .await?;
    Ok(v.unwrap_or(false))
}

pub async fn lookup_idempotency(
    pool: &PgPool,
    tenant: &str,
    key: &str,
) -> Result<Option<(uuid::Uuid, String)>, ApplyError> {
    let row = sqlx::query(
        "SELECT resource_id, request_hash FROM pay_rs.idempotency_keys WHERE tenant_id = $1 AND key = $2",
    )
    .bind(tenant)
    .bind(key)
    .fetch_optional(pool)
    .await?;
    Ok(match row {
        Some(r) => Some((r.try_get("resource_id")?, r.try_get("request_hash")?)),
        None => None,
    })
}

pub async fn insert_idempotency(
    pool: &PgPool,
    tenant: &str,
    key: &str,
    resource_id: uuid::Uuid,
    request_hash: &str,
) -> Result<(), ApplyError> {
    sqlx::query(
        r#"
        INSERT INTO pay_rs.idempotency_keys (tenant_id, key, resource_kind, resource_id, request_hash)
        VALUES ($1, $2, 'payment', $3, $4)
        "#,
    )
    .bind(tenant)
    .bind(key)
    .bind(resource_id)
    .bind(request_hash)
    .execute(pool)
    .await
    .map_err(ApplyError::from_sql)?;
    Ok(())
}

async fn load(pool: &PgPool, sql: &str, id: uuid::Uuid) -> Result<Option<PaymentView>, ApplyError> {
    let row = sqlx::query(sql).bind(id).fetch_optional(pool).await?;
    let Some(row) = row else {
        return Ok(None);
    };
    let quoted = rows::money_from_parts(
        row.try_get("amount_minor")?,
        row.try_get("currency")?,
        row.try_get("exponent")?,
    )?;
    let attempt_id: Option<uuid::Uuid> = row.try_get("attempt_id")?;
    Ok(Some(PaymentView {
        id: PaymentId::from_uuid(row.try_get("id")?),
        tenant_id: TenantId::new(row.try_get::<String, _>("tenant_id")?),
        public_token: PublicToken::new(row.try_get::<String, _>("public_token")?),
        quoted,
        status: rows::parse_payment_status(row.try_get("status")?)?,
        expires_at: row.try_get("expires_at")?,
        created_at: row.try_get("created_at")?,
        success_url: row.try_get("success_url")?,
        cancel_url: row.try_get("cancel_url")?,
        payer_name: row.try_get("payer_name")?,
        payer_email: row.try_get("payer_email")?,
        provider: row.try_get("attempt_rail")?,
        session_url: row.try_get("session_url")?,
        session_id: row.try_get("session_id")?,
        attempt_id: attempt_id.map(AttemptId::from_uuid),
        payment_link_id: row.try_get("payment_link_id")?,
        slot_key: row.try_get("slot_key")?,
    }))
}
