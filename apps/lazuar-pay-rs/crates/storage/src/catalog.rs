//! Payment links, products, occupancy counts. Not the money fold.

use domain::money::Money;
use domain::{PaymentId, PaymentLinkId, TenantId};
use sqlx::{PgPool, Row};
use time::OffsetDateTime;
use uuid::Uuid;

use crate::error::ApplyError;
use crate::rows;

pub fn clamp_limit(limit: Option<i64>) -> i64 {
    match limit {
        Some(n) if n >= 1 => n.min(100),
        _ => 50,
    }
}

#[derive(Clone, Debug)]
pub struct PaymentLinkRow {
    pub id: PaymentLinkId,
    pub tenant_id: TenantId,
    pub public_token: String,
    pub rail: String,
    pub product_id: Option<Uuid>,
    pub quoted: Money,
    pub max_payers: Option<i32>,
    pub label: Option<String>,
    pub created_at: OffsetDateTime,
}

#[derive(Clone, Debug)]
pub struct Occupancy {
    pub taken: i64,
    pub paid: i64,
}

impl Occupancy {
    pub fn merchant_status(&self, max_payers: Option<i32>) -> &'static str {
        match max_payers {
            Some(max) if self.taken > i64::from(max) => "over_capacity",
            Some(max) if self.taken >= i64::from(max) => "full",
            _ => "open",
        }
    }

    pub fn remaining_unclamped(&self, max_payers: Option<i32>) -> Option<i64> {
        max_payers.map(|m| i64::from(m) - self.taken)
    }

    pub fn remaining_clamped(&self, max_payers: Option<i32>) -> Option<i64> {
        self.remaining_unclamped(max_payers).map(|r| r.max(0))
    }

    pub fn is_full(&self, max_payers: Option<i32>) -> bool {
        max_payers.is_some_and(|m| self.taken >= i64::from(m))
    }
}

#[allow(clippy::too_many_arguments)]
pub async fn insert_link(
    pool: &PgPool,
    tenant_id: &str,
    public_token: &str,
    rail: &str,
    product_id: Option<Uuid>,
    quoted: Money,
    max_payers: Option<i32>,
    label: Option<&str>,
) -> Result<PaymentLinkRow, ApplyError> {
    let id = Uuid::new_v4();
    sqlx::query(
        r#"
        INSERT INTO pay_rs.payment_links (
            id, tenant_id, public_token, rail, product_id,
            amount_minor, currency, exponent, max_payers, label
        ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
        "#,
    )
    .bind(id)
    .bind(tenant_id)
    .bind(public_token)
    .bind(rail)
    .bind(product_id)
    .bind(quoted.minor() as i64)
    .bind(quoted.currency().code.as_str())
    .bind(i16::from(quoted.currency().exponent))
    .bind(max_payers)
    .bind(label)
    .execute(pool)
    .await?;
    get_link_by_id(pool, id).await?.ok_or(ApplyError::NotFound)
}

fn map_link(row: sqlx::postgres::PgRow) -> Result<PaymentLinkRow, ApplyError> {
    Ok(PaymentLinkRow {
        id: PaymentLinkId::from_uuid(row.try_get("id")?),
        tenant_id: TenantId::new(row.try_get::<String, _>("tenant_id")?),
        public_token: row.try_get("public_token")?,
        rail: row.try_get("rail")?,
        product_id: row.try_get("product_id")?,
        quoted: rows::money_from_parts(
            row.try_get("amount_minor")?,
            row.try_get("currency")?,
            row.try_get("exponent")?,
        )?,
        max_payers: row.try_get("max_payers")?,
        label: row.try_get("label")?,
        created_at: row.try_get("created_at")?,
    })
}

pub async fn get_link_by_id(pool: &PgPool, id: Uuid) -> Result<Option<PaymentLinkRow>, ApplyError> {
    let row = sqlx::query("SELECT * FROM pay_rs.payment_links WHERE id = $1")
        .bind(id)
        .fetch_optional(pool)
        .await?;
    match row {
        Some(r) => Ok(Some(map_link(r)?)),
        None => Ok(None),
    }
}

pub async fn get_link_by_token(
    pool: &PgPool,
    token: &str,
) -> Result<Option<PaymentLinkRow>, ApplyError> {
    let row = sqlx::query("SELECT * FROM pay_rs.payment_links WHERE public_token = $1")
        .bind(token)
        .fetch_optional(pool)
        .await?;
    match row {
        Some(r) => Ok(Some(map_link(r)?)),
        None => Ok(None),
    }
}

pub async fn occupancy(pool: &PgPool, link_id: PaymentLinkId) -> Result<Occupancy, ApplyError> {
    let taken: i64 = sqlx::query_scalar(
        r#"
        SELECT count(*)::bigint FROM pay_rs.payments
         WHERE payment_link_id = $1
           AND status IN ('open','processing','settled')
        "#,
    )
    .bind(link_id.as_uuid())
    .fetch_one(pool)
    .await?;
    let paid: i64 = sqlx::query_scalar(
        r#"
        SELECT count(*)::bigint FROM pay_rs.payments
         WHERE payment_link_id = $1 AND status = 'settled'
        "#,
    )
    .bind(link_id.as_uuid())
    .fetch_one(pool)
    .await?;
    Ok(Occupancy { taken, paid })
}

pub async fn child_by_slot(
    pool: &PgPool,
    link_id: PaymentLinkId,
    slot: &str,
) -> Result<Option<PaymentId>, ApplyError> {
    let id: Option<Uuid> = sqlx::query_scalar(
        r#"
        SELECT id FROM pay_rs.payments
         WHERE payment_link_id = $1 AND slot_key = $2
         ORDER BY created_at DESC
         LIMIT 1
        "#,
    )
    .bind(link_id.as_uuid())
    .bind(slot)
    .fetch_optional(pool)
    .await?;
    Ok(id.map(PaymentId::from_uuid))
}

pub async fn product_name(
    pool: &PgPool,
    tenant_id: &str,
    product_id: Uuid,
) -> Result<Option<String>, ApplyError> {
    let name: Option<String> =
        sqlx::query_scalar("SELECT name FROM pay_rs.products WHERE id = $1 AND tenant_id = $2")
            .bind(product_id)
            .bind(tenant_id)
            .fetch_optional(pool)
            .await?;
    Ok(name)
}

pub struct ProductPrice {
    pub id: Uuid,
    pub quoted: Money,
}

pub async fn product_price(
    pool: &PgPool,
    tenant_id: &str,
    product_id: Uuid,
) -> Result<Option<ProductPrice>, ApplyError> {
    let exists: Option<Uuid> =
        sqlx::query_scalar("SELECT id FROM pay_rs.products WHERE id = $1 AND tenant_id = $2")
            .bind(product_id)
            .bind(tenant_id)
            .fetch_optional(pool)
            .await?;
    if exists.is_none() {
        return Ok(None);
    }
    let row = sqlx::query(
        "SELECT id, amount_minor, currency, exponent FROM pay_rs.prices WHERE product_id = $1 LIMIT 1",
    )
    .bind(product_id)
    .fetch_optional(pool)
    .await?;
    let Some(row) = row else {
        return Ok(Some(ProductPrice {
            id: product_id,
            quoted: Money::from_minor(0, domain::money::Currency::MYR).unwrap(),
        }));
    };
    Ok(Some(ProductPrice {
        id: row.try_get("id")?,
        quoted: rows::money_from_parts(
            row.try_get("amount_minor")?,
            row.try_get("currency")?,
            row.try_get("exponent")?,
        )?,
    }))
}

pub async fn insert_product(
    pool: &PgPool,
    tenant_id: &str,
    name: &str,
    description: Option<&str>,
    quoted: Money,
) -> Result<(Uuid, Uuid), ApplyError> {
    let pid = Uuid::new_v4();
    let price_id = Uuid::new_v4();
    sqlx::query(
        "INSERT INTO pay_rs.products (id, tenant_id, name, description) VALUES ($1, $2, $3, $4)",
    )
    .bind(pid)
    .bind(tenant_id)
    .bind(name)
    .bind(description)
    .execute(pool)
    .await?;
    sqlx::query(
        r#"
        INSERT INTO pay_rs.prices (id, product_id, tenant_id, amount_minor, currency, exponent, interval)
        VALUES ($1, $2, $3, $4, $5, $6, 'one_off')
        "#,
    )
    .bind(price_id)
    .bind(pid)
    .bind(tenant_id)
    .bind(quoted.minor() as i64)
    .bind(quoted.currency().code.as_str())
    .bind(i16::from(quoted.currency().exponent))
    .execute(pool)
    .await?;
    Ok((pid, price_id))
}

#[derive(Clone, Debug)]
pub struct ProductListItem {
    pub id: Uuid,
    pub tenant_id: String,
    pub name: String,
    pub created_at: OffsetDateTime,
    pub price_id: Option<Uuid>,
    pub amount_minor: Option<i64>,
    pub currency: Option<String>,
    pub exponent: Option<i16>,
    pub interval: Option<String>,
}

pub async fn list_links(
    pool: &PgPool,
    tenant_id: &str,
    limit: i64,
    after: Option<Uuid>,
) -> Result<(Vec<PaymentLinkRow>, Option<String>), ApplyError> {
    let cursor = if let Some(id) = after {
        sqlx::query(
            "SELECT id, created_at FROM pay_rs.payment_links WHERE tenant_id = $1 AND id = $2",
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
        sqlx::query(
            r#"
            SELECT * FROM pay_rs.payment_links
             WHERE tenant_id = $1
               AND (created_at < $2 OR (created_at = $2 AND id < $3))
             ORDER BY created_at DESC, id DESC
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
            SELECT * FROM pay_rs.payment_links
             WHERE tenant_id = $1
             ORDER BY created_at DESC, id DESC
             LIMIT $2
            "#,
        )
        .bind(tenant_id)
        .bind(limit + 1)
        .fetch_all(pool)
        .await?
    };
    page_links(rows, limit).await
}

async fn page_links(
    mut rows: Vec<sqlx::postgres::PgRow>,
    limit: i64,
) -> Result<(Vec<PaymentLinkRow>, Option<String>), ApplyError> {
    let mut next = None;
    if rows.len() as i64 > limit {
        rows.truncate(limit as usize);
        if let Some(last) = rows.last() {
            let id: Uuid = last.try_get("id")?;
            next = Some(PaymentLinkId::from_uuid(id).to_wire());
        }
    }
    let mut out = Vec::with_capacity(rows.len());
    for r in rows {
        out.push(map_link(r)?);
    }
    Ok((out, next))
}

#[derive(Clone, Debug)]
pub struct CheckoutListItem {
    pub id: PaymentId,
    pub tenant_id: String,
    pub provider: Option<String>,
    pub quoted: Money,
    pub status: String,
    pub public_token: String,
    pub created_at: OffsetDateTime,
    pub product_id: Option<Uuid>,
}

pub async fn list_standalone_checkouts(
    pool: &PgPool,
    tenant_id: &str,
    limit: i64,
    after: Option<Uuid>,
) -> Result<(Vec<CheckoutListItem>, Option<String>), ApplyError> {
    let cursor = if let Some(id) = after {
        sqlx::query("SELECT id, created_at FROM pay_rs.payments WHERE tenant_id = $1 AND id = $2")
            .bind(tenant_id)
            .bind(id)
            .fetch_optional(pool)
            .await?
    } else {
        None
    };
    let sql_tail = r#"
         WHERE p.tenant_id = $1 AND p.payment_link_id IS NULL
    "#;
    let rows = if let Some(c) = cursor {
        let created: OffsetDateTime = c.try_get("created_at")?;
        let cid: Uuid = c.try_get("id")?;
        sqlx::query(&format!(
            r#"
            SELECT p.id, p.tenant_id, p.public_token, p.amount_minor, p.currency, p.exponent,
                   p.status, p.created_at, p.product_id, a.rail AS provider
              FROM pay_rs.payments p
              LEFT JOIN LATERAL (
                SELECT rail FROM pay_rs.attempts WHERE payment_id = p.id
                 ORDER BY created_at DESC LIMIT 1
              ) a ON true
            {sql_tail}
               AND (p.created_at < $2 OR (p.created_at = $2 AND p.id < $3))
             ORDER BY p.created_at DESC, p.id DESC
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
            SELECT p.id, p.tenant_id, p.public_token, p.amount_minor, p.currency, p.exponent,
                   p.status, p.created_at, p.product_id, a.rail AS provider
              FROM pay_rs.payments p
              LEFT JOIN LATERAL (
                SELECT rail FROM pay_rs.attempts WHERE payment_id = p.id
                 ORDER BY created_at DESC LIMIT 1
              ) a ON true
            {sql_tail}
             ORDER BY p.created_at DESC, p.id DESC
             LIMIT $2
            "#
        ))
        .bind(tenant_id)
        .bind(limit + 1)
        .fetch_all(pool)
        .await?
    };
    let mut next = None;
    let mut list = rows;
    if list.len() as i64 > limit {
        list.truncate(limit as usize);
        if let Some(last) = list.last() {
            let id: Uuid = last.try_get("id")?;
            next = Some(PaymentId::from_uuid(id).to_wire());
        }
    }
    let mut out = Vec::new();
    for row in list {
        out.push(CheckoutListItem {
            id: PaymentId::from_uuid(row.try_get("id")?),
            tenant_id: row.try_get("tenant_id")?,
            provider: row.try_get("provider")?,
            quoted: rows::money_from_parts(
                row.try_get("amount_minor")?,
                row.try_get("currency")?,
                row.try_get("exponent")?,
            )?,
            status: row.try_get("status")?,
            public_token: row.try_get("public_token")?,
            created_at: row.try_get("created_at")?,
            product_id: row.try_get("product_id")?,
        });
    }
    Ok((out, next))
}

pub async fn list_products(
    pool: &PgPool,
    tenant_id: &str,
    limit: i64,
    after: Option<Uuid>,
) -> Result<(Vec<ProductListItem>, Option<String>), ApplyError> {
    let cursor = if let Some(id) = after {
        sqlx::query("SELECT id, created_at FROM pay_rs.products WHERE tenant_id = $1 AND id = $2")
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
            SELECT p.id, p.tenant_id, p.name, p.created_at,
                   pr.id AS price_id, pr.amount_minor, pr.currency, pr.exponent, pr.interval
              FROM pay_rs.products p
              LEFT JOIN pay_rs.prices pr ON pr.product_id = p.id
             WHERE p.tenant_id = $1
               AND (p.created_at < $2 OR (p.created_at = $2 AND p.id < $3))
             ORDER BY p.created_at DESC, p.id DESC
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
            SELECT p.id, p.tenant_id, p.name, p.created_at,
                   pr.id AS price_id, pr.amount_minor, pr.currency, pr.exponent, pr.interval
              FROM pay_rs.products p
              LEFT JOIN pay_rs.prices pr ON pr.product_id = p.id
             WHERE p.tenant_id = $1
             ORDER BY p.created_at DESC, p.id DESC
             LIMIT $2
            "#,
        )
        .bind(tenant_id)
        .bind(limit + 1)
        .fetch_all(pool)
        .await?
    };
    let mut next = None;
    let mut list = rows;
    if list.len() as i64 > limit {
        list.truncate(limit as usize);
        if let Some(last) = list.last() {
            let id: Uuid = last.try_get("id")?;
            next = Some(id.as_simple().to_string());
        }
    }
    let mut out = Vec::new();
    for row in list {
        out.push(ProductListItem {
            id: row.try_get("id")?,
            tenant_id: row.try_get("tenant_id")?,
            name: row.try_get("name")?,
            created_at: row.try_get("created_at")?,
            price_id: row.try_get("price_id")?,
            amount_minor: row.try_get("amount_minor")?,
            currency: row.try_get("currency")?,
            exponent: row.try_get("exponent")?,
            interval: row.try_get("interval")?,
        });
    }
    Ok((out, next))
}

pub async fn open_child_ids(
    pool: &PgPool,
    link_id: PaymentLinkId,
    older_than: Option<OffsetDateTime>,
) -> Result<Vec<PaymentId>, ApplyError> {
    let rows = if let Some(cut) = older_than {
        sqlx::query_scalar(
            r#"
            SELECT id FROM pay_rs.payments
             WHERE payment_link_id = $1
               AND status IN ('open','processing')
               AND expires_at <= $2
            "#,
        )
        .bind(link_id.as_uuid())
        .bind(cut)
        .fetch_all(pool)
        .await?
    } else {
        sqlx::query_scalar(
            r#"
            SELECT id FROM pay_rs.payments
             WHERE payment_link_id = $1
               AND status IN ('open','processing')
            "#,
        )
        .bind(link_id.as_uuid())
        .fetch_all(pool)
        .await?
    };
    Ok(rows.into_iter().map(PaymentId::from_uuid).collect())
}

pub async fn burn_slot(pool: &PgPool, payment_id: PaymentId, slot: &str) -> Result<(), ApplyError> {
    if slot.contains(":burned:") {
        return Ok(());
    }
    let burned = format!("{slot}:burned:{}", payment_id.to_wire());
    sqlx::query("UPDATE pay_rs.payments SET slot_key = $2 WHERE id = $1")
        .bind(payment_id.as_uuid())
        .bind(burned)
        .execute(pool)
        .await?;
    Ok(())
}
