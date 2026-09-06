//! Gateway credentials. Decrypt happens at the call boundary (032/15).

use sqlx::{PgPool, Row};
use uuid::Uuid;

use crate::error::ApplyError;

#[derive(Clone, Debug)]
pub struct CredentialRow {
    pub tenant_id: String,
    pub rail: String,
    pub ciphertext: Vec<u8>,
    pub last4: Option<String>,
    pub webhook_ciphertext: Option<Vec<u8>>,
    pub public_merchant_id: Option<String>,
    pub environment: String,
}

pub async fn get_credential(
    pool: &PgPool,
    tenant_id: &str,
    rail: &str,
) -> Result<Option<CredentialRow>, ApplyError> {
    let row = sqlx::query(
        r#"
        SELECT tenant_id, rail, ciphertext, last4, webhook_ciphertext,
               public_merchant_id, environment
          FROM pay_rs.gateway_credentials
         WHERE tenant_id = $1 AND rail = $2
        "#,
    )
    .bind(tenant_id)
    .bind(rail)
    .fetch_optional(pool)
    .await?;
    Ok(row.map(|r| CredentialRow {
        tenant_id: r.get("tenant_id"),
        rail: r.get("rail"),
        ciphertext: r.get("ciphertext"),
        last4: r.get("last4"),
        webhook_ciphertext: r.get("webhook_ciphertext"),
        public_merchant_id: r.get("public_merchant_id"),
        environment: r.get("environment"),
    }))
}

pub async fn ensure_org_settings(pool: &PgPool, tenant_id: &str) -> Result<(), ApplyError> {
    sqlx::query("INSERT INTO pay_rs.org_settings (tenant_id) VALUES ($1) ON CONFLICT DO NOTHING")
        .bind(tenant_id)
        .execute(pool)
        .await?;
    Ok(())
}

#[allow(clippy::too_many_arguments)]
pub async fn upsert_stripe(
    pool: &PgPool,
    tenant_id: &str,
    ciphertext: &[u8],
    webhook_ciphertext: &[u8],
    last4: &str,
    environment: Option<&str>,
) -> Result<CredentialRow, ApplyError> {
    ensure_org_settings(pool, tenant_id).await?;
    let existing = get_credential(pool, tenant_id, "stripe").await?;
    if let Some(row) = existing {
        let env = environment.unwrap_or(&row.environment);
        sqlx::query(
            r#"
            UPDATE pay_rs.gateway_credentials
               SET ciphertext = $3,
                   webhook_ciphertext = $4,
                   last4 = $5,
                   environment = $6,
                   updated_at = now()
             WHERE tenant_id = $1 AND rail = $2
            "#,
        )
        .bind(tenant_id)
        .bind("stripe")
        .bind(ciphertext)
        .bind(webhook_ciphertext)
        .bind(last4)
        .bind(env)
        .execute(pool)
        .await?;
        return get_credential(pool, tenant_id, "stripe")
            .await?
            .ok_or(ApplyError::NotFound);
    }
    let env = environment.unwrap_or("test");
    let res = sqlx::query(
        r#"
        INSERT INTO pay_rs.gateway_credentials (
            tenant_id, rail, ciphertext, last4, webhook_ciphertext, environment
        ) VALUES ($1, 'stripe', $2, $3, $4, $5)
        "#,
    )
    .bind(tenant_id)
    .bind(ciphertext)
    .bind(last4)
    .bind(webhook_ciphertext)
    .bind(env)
    .execute(pool)
    .await;
    match res {
        Ok(_) => {}
        Err(e) if crate::error::is_unique_violation(&e) => {
            return get_credential(pool, tenant_id, "stripe")
                .await?
                .ok_or(ApplyError::NotFound);
        }
        Err(e) => return Err(e.into()),
    }
    get_credential(pool, tenant_id, "stripe")
        .await?
        .ok_or(ApplyError::NotFound)
}

pub async fn audit_gateway(
    pool: &PgPool,
    tenant_id: &str,
    actor: &str,
    provider: &str,
    last4: &str,
    environment: &str,
    webhook_configured: bool,
) -> Result<(), ApplyError> {
    sqlx::query(
        r#"
        INSERT INTO pay_rs.audit_events (id, tenant_id, action, actor, detail)
        VALUES ($1, $2, 'gateway.credentials.upsert', $3, $4::jsonb)
        "#,
    )
    .bind(Uuid::new_v4())
    .bind(tenant_id)
    .bind(actor)
    .bind(
        serde_json::json!({
            "provider": provider,
            "last4": last4,
            "environment": environment,
            "webhook_configured": webhook_configured,
        })
        .to_string(),
    )
    .execute(pool)
    .await?;
    Ok(())
}

pub async fn record_ignored_inbound(
    pool: &PgPool,
    tenant_id: &str,
    rail: &str,
    proof_id: &str,
    reason: &str,
) -> Result<bool, ApplyError> {
    let row = sqlx::query(
        r#"
        INSERT INTO pay_rs.inbound_events (tenant_id, rail, proof_id, ignore_reason)
        VALUES ($1, $2, $3, $4)
        ON CONFLICT ON CONSTRAINT inbound_events_pk DO NOTHING
        RETURNING proof_id
        "#,
    )
    .bind(tenant_id)
    .bind(rail)
    .bind(proof_id)
    .bind(reason)
    .fetch_optional(pool)
    .await?;
    Ok(row.is_some())
}

pub async fn payment_by_session(
    pool: &PgPool,
    tenant_id: &str,
    rail: &str,
    session_id: &str,
) -> Result<Option<(uuid::Uuid, uuid::Uuid)>, ApplyError> {
    let row = sqlx::query(
        r#"
        SELECT payment_id, id FROM pay_rs.attempts
         WHERE tenant_id = $1 AND rail = $2 AND session_id = $3
         ORDER BY created_at DESC
         LIMIT 1
        "#,
    )
    .bind(tenant_id)
    .bind(rail)
    .bind(session_id)
    .fetch_optional(pool)
    .await?;
    Ok(row.map(|r| (r.get("payment_id"), r.get("id"))))
}

pub async fn latest_attempt_refs(
    pool: &PgPool,
    payment_id: uuid::Uuid,
) -> Result<Option<(Option<String>, Option<String>)>, ApplyError> {
    let row = sqlx::query(
        r#"
        SELECT capture_id, session_id FROM pay_rs.attempts
         WHERE payment_id = $1
         ORDER BY created_at DESC
         LIMIT 1
        "#,
    )
    .bind(payment_id)
    .fetch_optional(pool)
    .await?;
    Ok(row.map(|r| (r.get("capture_id"), r.get("session_id"))))
}

pub async fn refund_amount(
    pool: &PgPool,
    id: Uuid,
) -> Result<Option<(String, i64, uuid::Uuid)>, ApplyError> {
    let row =
        sqlx::query("SELECT tenant_id, amount_minor, payment_id FROM pay_rs.refunds WHERE id = $1")
            .bind(id)
            .fetch_optional(pool)
            .await?;
    Ok(row.map(|r| {
        (
            r.get("tenant_id"),
            r.get("amount_minor"),
            r.get("payment_id"),
        )
    }))
}
