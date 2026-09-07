//! Charge / document / refund list reads. GET does not re-fold (033/12).

use domain::money::Money;
use domain::{ChargeId, PaymentId, RefundId};
use sqlx::{PgPool, Row};
use time::OffsetDateTime;
use uuid::Uuid;

use crate::error::ApplyError;
use crate::rows;

pub fn uuid_wire(id: Uuid) -> String {
    id.as_simple().to_string()
}

pub fn wire_reason(reason: &str) -> &str {
    match reason {
        "over_capacity" => "late_pay",
        other => other,
    }
}

#[derive(Clone, Debug)]
pub struct ChargeListItem {
    pub id: Uuid,
    pub tenant_id: String,
    pub payment_id: PaymentId,
    pub quoted: Money,
    pub status: String,
    pub provider: String,
    pub payer_name: Option<String>,
    pub created_at: OffsetDateTime,
    pub label: Option<String>,
}

#[derive(Clone, Debug)]
pub struct DocumentListItem {
    pub id: Uuid,
    pub tenant_id: String,
    pub number: String,
    pub title: String,
    pub payment_id: PaymentId,
    pub quoted: Money,
    pub payer_name: Option<String>,
    pub created_at: OffsetDateTime,
    pub label: Option<String>,
}

#[derive(Clone, Debug)]
pub struct RefundListItem {
    pub id: Uuid,
    pub tenant_id: String,
    pub payment_id: PaymentId,
    pub charge_id: Option<Uuid>,
    pub quoted: Money,
    pub status: String,
    pub rail: String,
    pub reason: String,
    pub created_at: OffsetDateTime,
    pub number: Option<String>,
    pub next_attempt_at: Option<OffsetDateTime>,
    pub idempotency_key: Option<String>,
}

#[derive(Clone, Debug)]
pub struct ChargeRef {
    pub id: Uuid,
    pub payment_id: PaymentId,
    pub quoted: Money,
    pub status: String,
    pub provider: String,
}

pub async fn payment_in_org(
    pool: &PgPool,
    tenant_id: &str,
    payment_id: PaymentId,
) -> Result<bool, ApplyError> {
    let n: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.payments WHERE id = $1 AND tenant_id = $2",
    )
    .bind(payment_id.as_uuid())
    .bind(tenant_id)
    .fetch_one(pool)
    .await?;
    Ok(n > 0)
}

pub async fn charge_for_payment(
    pool: &PgPool,
    tenant_id: &str,
    payment_id: PaymentId,
) -> Result<Option<ChargeRef>, ApplyError> {
    let row = sqlx::query(
        r#"
        SELECT c.id, c.payment_id, c.amount_minor, c.currency, c.exponent, c.status,
               COALESCE(c.rail, a.rail, 'test') AS provider
          FROM pay_rs.charges c
          LEFT JOIN LATERAL (
            SELECT rail FROM pay_rs.attempts WHERE payment_id = c.payment_id
             ORDER BY created_at DESC LIMIT 1
          ) a ON true
         WHERE c.payment_id = $1 AND c.tenant_id = $2
        "#,
    )
    .bind(payment_id.as_uuid())
    .bind(tenant_id)
    .fetch_optional(pool)
    .await?;
    let Some(row) = row else {
        return Ok(None);
    };
    Ok(Some(map_charge_ref(row)?))
}

fn map_charge_ref(row: sqlx::postgres::PgRow) -> Result<ChargeRef, ApplyError> {
    Ok(ChargeRef {
        id: row.try_get("id")?,
        payment_id: PaymentId::from_uuid(row.try_get("payment_id")?),
        quoted: rows::money_from_parts(
            row.try_get("amount_minor")?,
            row.try_get("currency")?,
            row.try_get("exponent")?,
        )?,
        status: row.try_get("status")?,
        provider: row.try_get("provider")?,
    })
}

pub async fn reserved_minor(pool: &PgPool, payment_id: PaymentId) -> Result<i64, ApplyError> {
    let used: i64 = sqlx::query_scalar(
        r#"
        SELECT COALESCE(SUM(amount_minor), 0)::bigint
          FROM pay_rs.refunds
         WHERE payment_id = $1 AND status IN ('pending','succeeded')
        "#,
    )
    .bind(payment_id.as_uuid())
    .fetch_one(pool)
    .await?;
    Ok(used)
}

pub async fn refund_by_id(
    pool: &PgPool,
    tenant_id: &str,
    id: Uuid,
) -> Result<Option<RefundListItem>, ApplyError> {
    let row = sqlx::query(
        r#"
        SELECT r.id, r.tenant_id, r.payment_id, r.charge_id, r.amount_minor, r.currency,
               r.exponent, r.status, r.rail, r.reason, r.created_at, r.next_attempt_at,
               r.idempotency_key, d.number
          FROM pay_rs.refunds r
          LEFT JOIN pay_rs.documents d
            ON d.tenant_id = r.tenant_id AND d.series = 'REF'
           AND d.number = ('REF-TEST-' || replace(r.id::text, '-', ''))
         WHERE r.id = $1 AND r.tenant_id = $2
        "#,
    )
    .bind(id)
    .bind(tenant_id)
    .fetch_optional(pool)
    .await?;
    match row {
        Some(r) => Ok(Some(map_refund(r)?)),
        None => Ok(None),
    }
}

pub async fn refund_by_idempotency(
    pool: &PgPool,
    tenant_id: &str,
    key: &str,
) -> Result<Option<RefundListItem>, ApplyError> {
    let row = sqlx::query(
        r#"
        SELECT r.id, r.tenant_id, r.payment_id, r.charge_id, r.amount_minor, r.currency,
               r.exponent, r.status, r.rail, r.reason, r.created_at, r.next_attempt_at,
               r.idempotency_key, d.number
          FROM pay_rs.refunds r
          LEFT JOIN pay_rs.documents d
            ON d.tenant_id = r.tenant_id AND d.series = 'REF'
           AND d.number = ('REF-TEST-' || replace(r.id::text, '-', ''))
         WHERE r.tenant_id = $1 AND r.idempotency_key = $2
        "#,
    )
    .bind(tenant_id)
    .bind(key)
    .fetch_optional(pool)
    .await?;
    match row {
        Some(r) => Ok(Some(map_refund(r)?)),
        None => Ok(None),
    }
}

fn map_refund(row: sqlx::postgres::PgRow) -> Result<RefundListItem, ApplyError> {
    Ok(RefundListItem {
        id: row.try_get("id")?,
        tenant_id: row.try_get("tenant_id")?,
        payment_id: PaymentId::from_uuid(row.try_get("payment_id")?),
        charge_id: row.try_get("charge_id")?,
        quoted: rows::money_from_parts(
            row.try_get("amount_minor")?,
            row.try_get("currency")?,
            row.try_get("exponent")?,
        )?,
        status: row.try_get("status")?,
        rail: row.try_get("rail")?,
        reason: row.try_get("reason")?,
        created_at: row.try_get("created_at")?,
        number: row.try_get("number")?,
        next_attempt_at: row.try_get("next_attempt_at")?,
        idempotency_key: row.try_get("idempotency_key")?,
    })
}

fn map_charge(row: sqlx::postgres::PgRow) -> Result<ChargeListItem, ApplyError> {
    Ok(ChargeListItem {
        id: row.try_get("id")?,
        tenant_id: row.try_get("tenant_id")?,
        payment_id: PaymentId::from_uuid(row.try_get("payment_id")?),
        quoted: rows::money_from_parts(
            row.try_get("amount_minor")?,
            row.try_get("currency")?,
            row.try_get("exponent")?,
        )?,
        status: row.try_get("status")?,
        provider: row.try_get("provider")?,
        payer_name: row.try_get("payer_name")?,
        created_at: row.try_get("created_at")?,
        label: row.try_get("label")?,
    })
}

fn map_document(row: sqlx::postgres::PgRow) -> Result<DocumentListItem, ApplyError> {
    let ch_minor: Option<i64> = row.try_get("ch_minor")?;
    let quoted = if let (Some(minor), Some(ccy), Some(exp)) = (
        ch_minor,
        row.try_get::<Option<String>, _>("ch_ccy")?,
        row.try_get::<Option<i16>, _>("ch_exp")?,
    ) {
        rows::money_from_parts(minor, &ccy, exp)?
    } else {
        rows::money_from_parts(
            row.try_get("pay_minor")?,
            row.try_get("pay_ccy")?,
            row.try_get("pay_exp")?,
        )?
    };
    Ok(DocumentListItem {
        id: row.try_get("id")?,
        tenant_id: row.try_get("tenant_id")?,
        number: row.try_get("number")?,
        title: row.try_get("title")?,
        payment_id: PaymentId::from_uuid(row.try_get("payment_id")?),
        quoted,
        payer_name: row.try_get("payer_name")?,
        created_at: row.try_get("created_at")?,
        label: row.try_get("label")?,
    })
}

const CHARGE_SELECT: &str = r#"
        SELECT c.id, c.tenant_id, c.payment_id, c.amount_minor, c.currency, c.exponent,
               c.status, COALESCE(c.rail, a.rail, 'test') AS provider,
               p.payer_name, p.created_at, pr.name AS label
          FROM pay_rs.charges c
          JOIN pay_rs.payments p ON p.id = c.payment_id
          LEFT JOIN LATERAL (
            SELECT rail FROM pay_rs.attempts WHERE payment_id = p.id
             ORDER BY created_at DESC LIMIT 1
          ) a ON true
          LEFT JOIN pay_rs.products pr ON pr.id = p.product_id AND pr.tenant_id = c.tenant_id
"#;

pub async fn list_charges(
    pool: &PgPool,
    tenant_id: &str,
    limit: i64,
    after: Option<Uuid>,
) -> Result<(Vec<ChargeListItem>, Option<String>), ApplyError> {
    let cursor = if let Some(id) = after {
        sqlx::query(
            r#"
            SELECT c.id, p.created_at
              FROM pay_rs.charges c
              JOIN pay_rs.payments p ON p.id = c.payment_id
             WHERE c.tenant_id = $1 AND c.id = $2
            "#,
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
        let cid: Uuid = c.try_get("id")?;
        sqlx::query(&format!(
            r#"
            {CHARGE_SELECT}
             WHERE c.tenant_id = $1
               AND (p.created_at < $2 OR (p.created_at = $2 AND c.id < $3))
             ORDER BY p.created_at DESC, c.id DESC
             LIMIT $4
            "#
        ))
        .bind(tenant_id)
        .bind(created)
        .bind(cid)
        .bind(limit + 1)
        .fetch_all(pool)
        .await?
    } else {
        sqlx::query(&format!(
            r#"
            {CHARGE_SELECT}
             WHERE c.tenant_id = $1
             ORDER BY p.created_at DESC, c.id DESC
             LIMIT $2
            "#
        ))
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
            let id: Uuid = last.try_get("id")?;
            next = Some(ChargeId::from_uuid(id).to_wire());
        }
    }
    let mut out = Vec::with_capacity(list.len());
    for row in list {
        out.push(map_charge(row)?);
    }
    Ok((out, next))
}

const DOC_SELECT: &str = r#"
        SELECT d.id, d.tenant_id, d.payment_id, d.number, d.title, d.created_at,
               p.payer_name, p.amount_minor AS pay_minor, p.currency AS pay_ccy,
               p.exponent AS pay_exp,
               c.amount_minor AS ch_minor, c.currency AS ch_ccy, c.exponent AS ch_exp,
               pr.name AS label
          FROM pay_rs.documents d
          JOIN pay_rs.payments p ON p.id = d.payment_id
          LEFT JOIN pay_rs.charges c ON c.payment_id = d.payment_id
          LEFT JOIN pay_rs.products pr ON pr.id = p.product_id AND pr.tenant_id = d.tenant_id
"#;

pub async fn list_documents(
    pool: &PgPool,
    tenant_id: &str,
    limit: i64,
    after: Option<Uuid>,
) -> Result<(Vec<DocumentListItem>, Option<String>), ApplyError> {
    let cursor = if let Some(id) = after {
        sqlx::query("SELECT id, created_at FROM pay_rs.documents WHERE tenant_id = $1 AND id = $2")
            .bind(tenant_id)
            .bind(id)
            .fetch_optional(pool)
            .await?
    } else {
        None
    };
    let rows = if let Some(c) = cursor {
        let created: OffsetDateTime = c.try_get("created_at")?;
        let cid: Uuid = c.try_get("id")?;
        sqlx::query(&format!(
            r#"
            {DOC_SELECT}
             WHERE d.tenant_id = $1
               AND (d.created_at < $2 OR (d.created_at = $2 AND d.id < $3))
             ORDER BY d.created_at DESC, d.id DESC
             LIMIT $4
            "#
        ))
        .bind(tenant_id)
        .bind(created)
        .bind(cid)
        .bind(limit + 1)
        .fetch_all(pool)
        .await?
    } else {
        sqlx::query(&format!(
            r#"
            {DOC_SELECT}
             WHERE d.tenant_id = $1
             ORDER BY d.created_at DESC, d.id DESC
             LIMIT $2
            "#
        ))
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
            let id: Uuid = last.try_get("id")?;
            next = Some(uuid_wire(id));
        }
    }
    let mut out = Vec::with_capacity(list.len());
    for row in list {
        out.push(map_document(row)?);
    }
    Ok((out, next))
}

pub async fn document_by_id(
    pool: &PgPool,
    tenant_id: &str,
    id: Uuid,
) -> Result<Option<DocumentListItem>, ApplyError> {
    let row = sqlx::query(&format!(
        r#"
        {DOC_SELECT}
         WHERE d.tenant_id = $1 AND d.id = $2
        "#
    ))
    .bind(tenant_id)
    .bind(id)
    .fetch_optional(pool)
    .await?;
    match row {
        Some(r) => Ok(Some(map_document(r)?)),
        None => Ok(None),
    }
}

pub async fn list_refunds(
    pool: &PgPool,
    tenant_id: &str,
    limit: i64,
    after: Option<Uuid>,
) -> Result<(Vec<RefundListItem>, Option<String>), ApplyError> {
    let cursor = if let Some(id) = after {
        sqlx::query("SELECT id, created_at FROM pay_rs.refunds WHERE tenant_id = $1 AND id = $2")
            .bind(tenant_id)
            .bind(id)
            .fetch_optional(pool)
            .await?
    } else {
        None
    };
    let rows = if let Some(c) = cursor {
        let created: OffsetDateTime = c.try_get("created_at")?;
        let cid: Uuid = c.try_get("id")?;
        sqlx::query(
            r#"
            SELECT r.id, r.tenant_id, r.payment_id, r.charge_id, r.amount_minor, r.currency,
                   r.exponent, r.status, r.rail, r.reason, r.created_at, r.next_attempt_at,
                   r.idempotency_key,
                   (SELECT d.number FROM pay_rs.documents d
                     WHERE d.tenant_id = r.tenant_id AND d.series = 'REF'
                       AND d.number = ('REF-TEST-' || replace(r.id::text, '-', ''))
                     LIMIT 1) AS number
              FROM pay_rs.refunds r
             WHERE r.tenant_id = $1
               AND (r.created_at < $2 OR (r.created_at = $2 AND r.id < $3))
             ORDER BY r.created_at DESC, r.id DESC
             LIMIT $4
            "#,
        )
        .bind(tenant_id)
        .bind(created)
        .bind(cid)
        .bind(limit + 1)
        .fetch_all(pool)
        .await?
    } else {
        sqlx::query(
            r#"
            SELECT r.id, r.tenant_id, r.payment_id, r.charge_id, r.amount_minor, r.currency,
                   r.exponent, r.status, r.rail, r.reason, r.created_at, r.next_attempt_at,
                   r.idempotency_key,
                   (SELECT d.number FROM pay_rs.documents d
                     WHERE d.tenant_id = r.tenant_id AND d.series = 'REF'
                       AND d.number = ('REF-TEST-' || replace(r.id::text, '-', ''))
                     LIMIT 1) AS number
              FROM pay_rs.refunds r
             WHERE r.tenant_id = $1
             ORDER BY r.created_at DESC, r.id DESC
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
            let id: Uuid = last.try_get("id")?;
            next = Some(RefundId::from_uuid(id).to_wire());
        }
    }
    let mut out = Vec::with_capacity(list.len());
    for row in list {
        out.push(map_refund(row)?);
    }
    Ok((out, next))
}
