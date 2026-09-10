//! SKIP LOCKED claims and non-fold SQL (033/04). Money status still goes through `apply`.

use domain::money::Money;
use domain::{AttemptId, PaymentId, TenantId};
use rust_decimal::Decimal;
use serde_json::{json, Number, Value};
use sqlx::{PgPool, Postgres, Row, Transaction};
use time::{Duration, OffsetDateTime};
use uuid::Uuid;

use crate::error::ApplyError;
use crate::rows;

#[derive(Clone, Debug)]
pub struct ExpireCandidate {
    pub payment_id: PaymentId,
    pub watch_timeout: bool,
}

#[derive(Clone, Debug)]
pub struct DeliveryRow {
    pub id: Uuid,
    pub tenant_id: String,
    pub event_id: String,
    pub event_type: String,
    pub payload_json: String,
    pub attempt_count: i32,
}

#[derive(Clone, Debug)]
pub struct EndpointRow {
    pub url: String,
    pub secret_ciphertext: Vec<u8>,
}

#[derive(Clone, Debug)]
pub struct PsyncCandidate {
    pub attempt_id: AttemptId,
    pub payment_id: PaymentId,
    pub tenant_id: TenantId,
    pub rail: String,
    pub session_id: String,
    pub quoted: Money,
}

#[derive(Clone, Debug)]
pub struct RefundClaim {
    pub id: Uuid,
    pub tenant_id: String,
    pub payment_id: PaymentId,
    pub rail: String,
    pub amount_minor: i64,
    pub currency: String,
    pub exponent: i16,
    pub attempt_count: i32,
}

pub async fn claim_expired(
    pool: &PgPool,
    now: OffsetDateTime,
    limit: i64,
) -> Result<Vec<ExpireCandidate>, ApplyError> {
    let mut tx = pool.begin().await?;
    let rows = sqlx::query(
        r#"
        SELECT id, payment_link_id, monitoring_until, expires_at
          FROM pay_rs.payments
         WHERE status IN ('open', 'processing')
           AND (
                 expires_at <= $1
              OR (payment_link_id IS NOT NULL AND monitoring_until <= $1)
           )
         ORDER BY expires_at
         LIMIT $2
           FOR UPDATE SKIP LOCKED
        "#,
    )
    .bind(now)
    .bind(limit)
    .fetch_all(&mut *tx)
    .await?;
    let mut out = Vec::with_capacity(rows.len());
    for row in rows {
        let link: Option<Uuid> = row.try_get("payment_link_id")?;
        let monitoring: OffsetDateTime = row.try_get("monitoring_until")?;
        out.push(ExpireCandidate {
            payment_id: PaymentId::from_uuid(row.try_get("id")?),
            watch_timeout: link.is_some() && monitoring <= now,
        });
    }
    tx.commit().await?;
    Ok(out)
}

pub async fn claim_deliveries(
    pool: &PgPool,
    _now: OffsetDateTime,
    lease: Duration,
    limit: i64,
) -> Result<Vec<DeliveryRow>, ApplyError> {
    let lease_secs = lease.whole_seconds();
    let mut tx = pool.begin().await?;
    let rows = sqlx::query(
        r#"
        UPDATE pay_rs.org_webhook_deliveries AS d
           SET leased_until = now() + ($1::bigint * interval '1 second')
          FROM (
                SELECT id FROM pay_rs.org_webhook_deliveries
                 WHERE status = 'pending'
                   AND next_attempt_at <= now()
                   AND (leased_until IS NULL OR leased_until < now())
                 ORDER BY created_at
                 LIMIT $2
                   FOR UPDATE SKIP LOCKED
               ) pick
         WHERE d.id = pick.id
     RETURNING d.id, d.tenant_id, d.event_id, d.event_type, d.payload_json::text, d.attempt_count
        "#,
    )
    .bind(lease_secs)
    .bind(limit)
    .fetch_all(&mut *tx)
    .await?;
    tx.commit().await?;
    let mut out = Vec::with_capacity(rows.len());
    for row in rows {
        out.push(DeliveryRow {
            id: row.try_get("id")?,
            tenant_id: row.try_get("tenant_id")?,
            event_id: row.try_get("event_id")?,
            event_type: row.try_get("event_type")?,
            payload_json: row.try_get("payload_json")?,
            attempt_count: row.try_get("attempt_count")?,
        });
    }
    Ok(out)
}

pub async fn load_endpoint(
    pool: &PgPool,
    tenant_id: &str,
) -> Result<Option<EndpointRow>, ApplyError> {
    let row = sqlx::query(
        "SELECT url, secret_ciphertext FROM pay_rs.org_webhook_endpoints WHERE tenant_id = $1",
    )
    .bind(tenant_id)
    .fetch_optional(pool)
    .await?;
    Ok(match row {
        Some(r) => Some(EndpointRow {
            url: r.try_get("url")?,
            secret_ciphertext: r.try_get("secret_ciphertext")?,
        }),
        None => None,
    })
}

pub async fn mark_delivery(
    pool: &PgPool,
    id: Uuid,
    status: &str,
    attempt_count: i32,
    next_attempt_at: OffsetDateTime,
    last_http_status: Option<i32>,
    last_error: Option<&str>,
) -> Result<(), ApplyError> {
    sqlx::query(
        r#"
        UPDATE pay_rs.org_webhook_deliveries
           SET status = $2,
               attempt_count = $3,
               next_attempt_at = $4,
               last_http_status = $5,
               last_error = $6,
               leased_until = NULL
         WHERE id = $1
        "#,
    )
    .bind(id)
    .bind(status)
    .bind(attempt_count)
    .bind(next_attempt_at)
    .bind(last_http_status)
    .bind(last_error)
    .execute(pool)
    .await?;
    Ok(())
}

pub async fn claim_psync(
    pool: &PgPool,
    now: OffsetDateTime,
    limit: i64,
) -> Result<Vec<PsyncCandidate>, ApplyError> {
    let mut tx = pool.begin().await?;
    let rows = sqlx::query(
        r#"
        UPDATE pay_rs.attempts AS a
           SET updated_at = $1, version = version + 1
          FROM (
                SELECT att.id
                  FROM pay_rs.attempts att
                  JOIN pay_rs.payments p ON p.id = att.payment_id
                 WHERE att.status IN ('pending', 'session_live')
                   AND att.updated_at < $1 - interval '30 seconds'
                   AND att.session_id IS NOT NULL
                   AND btrim(att.session_id) <> ''
                   AND p.status IN ('open', 'processing')
                   AND att.rail NOT IN ('test', 'solana')
                 ORDER BY att.updated_at
                 LIMIT $2
                   FOR UPDATE OF att SKIP LOCKED
               ) pick
         WHERE a.id = pick.id
     RETURNING a.id, a.payment_id, a.tenant_id, a.rail, a.session_id,
               a.amount_minor, a.currency, a.exponent
        "#,
    )
    .bind(now)
    .bind(limit)
    .fetch_all(&mut *tx)
    .await?;
    tx.commit().await?;
    let mut out = Vec::with_capacity(rows.len());
    for row in rows {
        let quoted = rows::money_from_parts(
            row.try_get("amount_minor")?,
            row.try_get("currency")?,
            row.try_get("exponent")?,
        )?;
        out.push(PsyncCandidate {
            attempt_id: AttemptId::from_uuid(row.try_get("id")?),
            payment_id: PaymentId::from_uuid(row.try_get("payment_id")?),
            tenant_id: TenantId::new(row.try_get::<String, _>("tenant_id")?),
            rail: row.try_get("rail")?,
            session_id: row.try_get("session_id")?,
            quoted,
        });
    }
    Ok(out)
}

pub async fn defer_psync(
    pool: &PgPool,
    attempt_id: AttemptId,
    until: OffsetDateTime,
) -> Result<(), ApplyError> {
    sqlx::query("UPDATE pay_rs.attempts SET updated_at = $2 WHERE id = $1")
        .bind(attempt_id.as_uuid())
        .bind(until)
        .execute(pool)
        .await?;
    Ok(())
}

pub async fn claim_refunds(
    pool: &PgPool,
    _now: OffsetDateTime,
    limit: i64,
) -> Result<Vec<RefundClaim>, ApplyError> {
    let mut tx = pool.begin().await?;
    let rows = sqlx::query(
        r#"
        UPDATE pay_rs.refunds AS r
           SET next_attempt_at = now() + interval '60 seconds'
          FROM (
                SELECT id FROM pay_rs.refunds
                 WHERE status = 'pending'
                   AND reason = 'late_pay'
                   AND rail = 'stripe'
                   AND attempt_count < 6
                   AND created_at > now() - interval '24 hours'
                   AND (next_attempt_at IS NULL OR next_attempt_at <= now())
                 ORDER BY created_at
                 LIMIT $1
                   FOR UPDATE SKIP LOCKED
               ) pick
         WHERE r.id = pick.id
     RETURNING r.id, r.tenant_id, r.payment_id, r.rail, r.amount_minor,
               r.currency, r.exponent, r.attempt_count
        "#,
    )
    .bind(limit)
    .fetch_all(&mut *tx)
    .await?;
    tx.commit().await?;
    let mut out = Vec::with_capacity(rows.len());
    for row in rows {
        out.push(RefundClaim {
            id: row.try_get("id")?,
            tenant_id: row.try_get("tenant_id")?,
            payment_id: PaymentId::from_uuid(row.try_get("payment_id")?),
            rail: row.try_get("rail")?,
            amount_minor: row.try_get("amount_minor")?,
            currency: row.try_get("currency")?,
            exponent: row.try_get("exponent")?,
            attempt_count: row.try_get("attempt_count")?,
        });
    }
    Ok(out)
}

pub async fn mark_refund(
    pool: &PgPool,
    id: Uuid,
    status: &str,
    attempt_count: i32,
    next_attempt_at: Option<OffsetDateTime>,
    last_error: Option<&str>,
) -> Result<(), ApplyError> {
    sqlx::query(
        r#"
        UPDATE pay_rs.refunds
           SET status = $2,
               attempt_count = $3,
               next_attempt_at = $4,
               last_error = $5
         WHERE id = $1
        "#,
    )
    .bind(id)
    .bind(status)
    .bind(attempt_count)
    .bind(next_attempt_at)
    .bind(last_error)
    .execute(pool)
    .await?;
    Ok(())
}

pub async fn enqueue_outbound(
    tx: &mut Transaction<'_, Postgres>,
    tenant_id: &str,
    event_id: &str,
    event_type: &str,
    payload: &Value,
) -> Result<(), ApplyError> {
    let exists: bool = sqlx::query_scalar(
        "SELECT EXISTS(SELECT 1 FROM pay_rs.org_webhook_endpoints WHERE tenant_id = $1)",
    )
    .bind(tenant_id)
    .fetch_one(&mut **tx)
    .await?;
    if !exists {
        return Ok(());
    }
    sqlx::query(
        r#"
        INSERT INTO pay_rs.org_webhook_deliveries (
            tenant_id, event_id, event_type, payload_json, status, next_attempt_at
        ) VALUES ($1, $2, $3, $4::jsonb, 'pending', now())
        ON CONFLICT (tenant_id, event_id) DO NOTHING
        "#,
    )
    .bind(tenant_id)
    .bind(event_id)
    .bind(event_type)
    .bind(payload.to_string())
    .execute(&mut **tx)
    .await?;
    Ok(())
}

/// Plane C cursor: events *newer* than `after` event_id, oldest first (036/006 #29).
#[derive(Clone, Debug)]
pub struct OrgEvent {
    pub event_id: String,
    pub event_type: String,
    pub payload: Value,
    pub status: String,
    pub created_at: OffsetDateTime,
}

pub async fn list_org_events(
    pool: &PgPool,
    tenant_id: &str,
    limit: i64,
    after: Option<&str>,
) -> Result<(Vec<OrgEvent>, Option<String>), ApplyError> {
    let cursor = if let Some(id) = after.map(str::trim).filter(|s| !s.is_empty()) {
        sqlx::query(
            "SELECT event_id, created_at FROM pay_rs.org_webhook_deliveries WHERE tenant_id = $1 AND event_id = $2",
        )
        .bind(tenant_id)
        .bind(id)
        .fetch_optional(pool)
        .await?
    } else {
        None
    };
    let rows = if let Some(c) = cursor {
        let created: OffsetDateTime = c.try_get("created_at")?;
        let eid: String = c.try_get("event_id")?;
        sqlx::query(
            r#"
            SELECT event_id, event_type, payload_json::text, status, created_at
              FROM pay_rs.org_webhook_deliveries
             WHERE tenant_id = $1
               AND (created_at > $2 OR (created_at = $2 AND event_id > $3))
             ORDER BY created_at ASC, event_id ASC
             LIMIT $4
            "#,
        )
        .bind(tenant_id)
        .bind(created)
        .bind(eid)
        .bind(limit + 1)
        .fetch_all(pool)
        .await?
    } else {
        sqlx::query(
            r#"
            SELECT event_id, event_type, payload_json::text, status, created_at
              FROM pay_rs.org_webhook_deliveries
             WHERE tenant_id = $1
             ORDER BY created_at ASC, event_id ASC
             LIMIT $2
            "#,
        )
        .bind(tenant_id)
        .bind(limit + 1)
        .fetch_all(pool)
        .await?
    };
    let mut list = rows;
    let mut next = None;
    if list.len() as i64 > limit {
        list.truncate(limit as usize);
        if let Some(last) = list.last() {
            next = Some(last.try_get::<String, _>("event_id")?);
        }
    }
    let mut out = Vec::with_capacity(list.len());
    for row in list {
        let raw: String = row.try_get("payload_json")?;
        let payload = serde_json::from_str(&raw).unwrap_or(Value::Object(Default::default()));
        out.push(OrgEvent {
            event_id: row.try_get("event_id")?,
            event_type: row.try_get("event_type")?,
            payload,
            status: row.try_get("status")?,
            created_at: row.try_get("created_at")?,
        });
    }
    Ok((out, next))
}

pub async fn enqueue_outbound_pool(
    pool: &PgPool,
    tenant_id: &str,
    event_id: &str,
    event_type: &str,
    payload: &Value,
) -> Result<(), ApplyError> {
    let mut tx = pool.begin().await?;
    enqueue_outbound(&mut tx, tenant_id, event_id, event_type, payload).await?;
    tx.commit().await?;
    Ok(())
}

#[derive(Clone, Copy, Debug)]
pub struct RetentionCfg {
    pub inbound_days: i32,
    pub one_days: i32,
    pub deliveries_days: i32,
    pub audit_days: i32,
    pub batch: i32,
}

impl Default for RetentionCfg {
    fn default() -> Self {
        Self {
            inbound_days: 90,
            one_days: 90,
            deliveries_days: 180,
            audit_days: 730,
            batch: 10_000,
        }
    }
}

pub async fn sweep_retention(pool: &PgPool, cfg: RetentionCfg) -> Result<u64, ApplyError> {
    let mut total = 0u64;
    total += sweep_table(
        pool,
        cfg.inbound_days,
        cfg.batch,
        r#"
        DELETE FROM pay_rs.inbound_events AS t
         USING (
            SELECT tenant_id, rail, proof_id
              FROM pay_rs.inbound_events
             WHERE received_at < $1
             ORDER BY received_at
             LIMIT $2
         ) pick
         WHERE (t.tenant_id, t.rail, t.proof_id)
             = (pick.tenant_id, pick.rail, pick.proof_id)
        "#,
    )
    .await?;
    total += sweep_table(
        pool,
        cfg.one_days,
        cfg.batch,
        r#"
        DELETE FROM pay_rs.one_webhook_events AS t
         USING (
            SELECT id FROM pay_rs.one_webhook_events
             WHERE received_at < $1
             ORDER BY received_at
             LIMIT $2
         ) pick
         WHERE t.id = pick.id
        "#,
    )
    .await?;
    total += sweep_table(
        pool,
        cfg.deliveries_days,
        cfg.batch,
        r#"
        DELETE FROM pay_rs.org_webhook_deliveries AS t
         USING (
            SELECT id FROM pay_rs.org_webhook_deliveries
             WHERE created_at < $1
             ORDER BY created_at
             LIMIT $2
         ) pick
         WHERE t.id = pick.id
        "#,
    )
    .await?;
    total += sweep_table(
        pool,
        cfg.audit_days,
        cfg.batch,
        r#"
        DELETE FROM pay_rs.audit_events AS t
         USING (
            SELECT id FROM pay_rs.audit_events
             WHERE at < $1
             ORDER BY at
             LIMIT $2
         ) pick
         WHERE t.id = pick.id
        "#,
    )
    .await?;
    Ok(total)
}

async fn sweep_table(pool: &PgPool, days: i32, batch: i32, sql: &str) -> Result<u64, ApplyError> {
    if days <= 0 {
        return Ok(0);
    }
    let cutoff = OffsetDateTime::now_utc() - Duration::days(i64::from(days));
    let mut deleted = 0u64;
    let batch = i64::from(batch.max(1));
    loop {
        let n = sqlx::query(sql)
            .bind(cutoff)
            .bind(batch)
            .execute(pool)
            .await?
            .rows_affected();
        deleted += n;
        if n < batch as u64 {
            return Ok(deleted);
        }
    }
}

pub fn money_number(m: Money) -> Value {
    let d = m
        .to_quoted_display()
        .unwrap_or_else(|_| Decimal::from(m.minor() as i64));
    let s = d.normalize().to_string();
    Value::Number(s.parse::<Number>().unwrap_or_else(|_| Number::from(0)))
}

pub fn envelope(
    event_id: &str,
    event_type: &str,
    org_id: &str,
    checkout_id: &str,
    quoted: Money,
    provider: &str,
) -> Value {
    let created = OffsetDateTime::now_utc()
        .format(&time::format_description::well_known::Rfc3339)
        .unwrap_or_default();
    json!({
        "id": event_id,
        "type": event_type,
        "created_at": created,
        "org_id": org_id,
        "api_version": "0.1.0",
        "data": {
            "checkout_id": checkout_id,
            "amount": money_number(quoted),
            "currency": quoted.currency().code.as_str(),
            "provider": provider,
        }
    })
}
