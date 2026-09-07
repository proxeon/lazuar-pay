//! Org webhook endpoint, One ciphertext, pause flag. Not the money fold.

use serde_json::Value;
use sqlx::{PgPool, Row};
use uuid::Uuid;

use crate::error::{is_unique_violation, ApplyError};

#[derive(Clone, Debug)]
pub struct OrgWebhookRow {
    pub tenant_id: String,
    pub url: String,
    pub secret_prefix: Option<String>,
}

pub async fn upsert_org_webhook(
    pool: &PgPool,
    tenant_id: &str,
    url: &str,
    secret_ciphertext: &[u8],
    secret_prefix: &str,
) -> Result<OrgWebhookRow, ApplyError> {
    sqlx::query(
        r#"
        INSERT INTO pay_rs.org_webhook_endpoints (
            tenant_id, url, secret_ciphertext, secret_prefix, updated_at
        ) VALUES ($1, $2, $3, $4, now())
        ON CONFLICT (tenant_id) DO UPDATE SET
            url = EXCLUDED.url,
            secret_ciphertext = EXCLUDED.secret_ciphertext,
            secret_prefix = EXCLUDED.secret_prefix,
            updated_at = now()
        "#,
    )
    .bind(tenant_id)
    .bind(url)
    .bind(secret_ciphertext)
    .bind(secret_prefix)
    .execute(pool)
    .await?;
    get_org_webhook(pool, tenant_id)
        .await?
        .ok_or(ApplyError::NotFound)
}

pub async fn get_org_webhook(
    pool: &PgPool,
    tenant_id: &str,
) -> Result<Option<OrgWebhookRow>, ApplyError> {
    let row = sqlx::query(
        "SELECT tenant_id, url, secret_prefix FROM pay_rs.org_webhook_endpoints WHERE tenant_id = $1",
    )
    .bind(tenant_id)
    .fetch_optional(pool)
    .await?;
    Ok(row.map(|r| OrgWebhookRow {
        tenant_id: r.get("tenant_id"),
        url: r.get("url"),
        secret_prefix: r.get("secret_prefix"),
    }))
}

pub async fn rotate_org_webhook(
    pool: &PgPool,
    tenant_id: &str,
    secret_ciphertext: &[u8],
    secret_prefix: &str,
) -> Result<Option<OrgWebhookRow>, ApplyError> {
    let n = sqlx::query(
        r#"
        UPDATE pay_rs.org_webhook_endpoints
           SET secret_ciphertext = $2, secret_prefix = $3, updated_at = now()
         WHERE tenant_id = $1
        "#,
    )
    .bind(tenant_id)
    .bind(secret_ciphertext)
    .bind(secret_prefix)
    .execute(pool)
    .await?
    .rows_affected();
    if n == 0 {
        return Ok(None);
    }
    get_org_webhook(pool, tenant_id).await
}

pub async fn set_charges_paused(
    pool: &PgPool,
    tenant_id: &str,
    paused: bool,
) -> Result<(), ApplyError> {
    if paused {
        sqlx::query(
            r#"
            INSERT INTO pay_rs.org_settings (tenant_id, charges_paused)
            VALUES ($1, true)
            ON CONFLICT (tenant_id) DO UPDATE
               SET charges_paused = true, updated_at = now()
            "#,
        )
        .bind(tenant_id)
        .execute(pool)
        .await?;
    } else {
        sqlx::query(
            r#"
            UPDATE pay_rs.org_settings
               SET charges_paused = false, updated_at = now()
             WHERE tenant_id = $1
            "#,
        )
        .bind(tenant_id)
        .execute(pool)
        .await?;
    }
    Ok(())
}

pub async fn set_one_webhook_ciphertext(
    pool: &PgPool,
    tenant_id: &str,
    ciphertext_hex: &str,
) -> Result<(), ApplyError> {
    crate::vault::ensure_org_settings(pool, tenant_id).await?;
    sqlx::query(
        r#"
        UPDATE pay_rs.org_settings
           SET one_webhook_ciphertext = $2, updated_at = now()
         WHERE tenant_id = $1
        "#,
    )
    .bind(tenant_id)
    .bind(ciphertext_hex)
    .execute(pool)
    .await?;
    Ok(())
}

pub async fn get_one_webhook_ciphertext(
    pool: &PgPool,
    tenant_id: &str,
) -> Result<Option<String>, ApplyError> {
    let v: Option<String> = sqlx::query_scalar(
        "SELECT one_webhook_ciphertext FROM pay_rs.org_settings WHERE tenant_id = $1",
    )
    .bind(tenant_id)
    .fetch_optional(pool)
    .await?;
    Ok(v.filter(|s| !s.is_empty()))
}

/// Returns `true` if this delivery was new.
pub async fn insert_one_event(
    pool: &PgPool,
    delivery_id: &str,
    event_type: &str,
) -> Result<bool, ApplyError> {
    let row = sqlx::query(
        r#"
        INSERT INTO pay_rs.one_webhook_events (delivery_id, event_type)
        VALUES ($1, $2)
        ON CONFLICT (delivery_id) DO NOTHING
        RETURNING id
        "#,
    )
    .bind(delivery_id)
    .bind(event_type)
    .fetch_optional(pool)
    .await;
    match row {
        Ok(Some(_)) => Ok(true),
        Ok(None) => Ok(false),
        Err(e) if is_unique_violation(&e) => Ok(false),
        Err(e) => Err(e.into()),
    }
}

pub async fn has_vault(pool: &PgPool, tenant_id: &str) -> Result<bool, ApplyError> {
    let v: bool = sqlx::query_scalar(
        "SELECT EXISTS(SELECT 1 FROM pay_rs.gateway_credentials WHERE tenant_id = $1)",
    )
    .bind(tenant_id)
    .fetch_one(pool)
    .await?;
    Ok(v)
}

pub async fn audit(
    pool: &PgPool,
    tenant_id: &str,
    action: &str,
    actor: &str,
    detail: Value,
) -> Result<(), ApplyError> {
    sqlx::query(
        r#"
        INSERT INTO pay_rs.audit_events (id, tenant_id, action, actor, detail)
        VALUES ($1, $2, $3, $4, $5::jsonb)
        "#,
    )
    .bind(Uuid::new_v4())
    .bind(tenant_id)
    .bind(action)
    .bind(actor)
    .bind(detail.to_string())
    .execute(pool)
    .await?;
    Ok(())
}
