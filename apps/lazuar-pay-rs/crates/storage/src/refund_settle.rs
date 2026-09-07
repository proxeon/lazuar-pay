//! Merchant refund settle / fail / resolve. Charge status from succeeded rows only.

use domain::journal::JournalEntry;
use domain::money::Money;
use domain::{PaymentId, TenantId};
use serde_json::json;
use sqlx::{PgPool, Postgres, Row, Transaction};
use time::OffsetDateTime;
use uuid::Uuid;

use crate::error::ApplyError;
use crate::lease::{enqueue_outbound, money_number};
use crate::money_query::{uuid_wire, RefundListItem};
use crate::rows;

pub fn ref_number(refund_id: Uuid) -> String {
    format!("REF-TEST-{}", uuid_wire(refund_id))
}

pub async fn fail_refund(pool: &PgPool, id: Uuid) -> Result<(), ApplyError> {
    sqlx::query(
        r#"
        UPDATE pay_rs.refunds
           SET status = 'failed', next_attempt_at = NULL, last_error = 'unsupported_or_rejected'
         WHERE id = $1 AND status = 'pending'
        "#,
    )
    .bind(id)
    .execute(pool)
    .await?;
    Ok(())
}

pub async fn settle_refund(pool: &PgPool, id: Uuid) -> Result<RefundListItem, ApplyError> {
    let mut tx = pool.begin().await?;
    let item = settle_refund_tx(&mut tx, id).await?;
    tx.commit().await?;
    Ok(item)
}

pub async fn resolve_refund(
    pool: &PgPool,
    tenant_id: &str,
    id: Uuid,
    succeeded: bool,
) -> Result<RefundListItem, ApplyError> {
    let mut tx = pool.begin().await?;
    let row = sqlx::query(
        r#"
        SELECT id, status, next_attempt_at
          FROM pay_rs.refunds
         WHERE id = $1 AND tenant_id = $2
         FOR UPDATE
        "#,
    )
    .bind(id)
    .bind(tenant_id)
    .fetch_optional(&mut *tx)
    .await?
    .ok_or(ApplyError::NotFound)?;
    let status: String = row.try_get("status")?;
    if status != "pending" {
        return Err(ApplyError::RefundNotPending);
    }
    let lease: Option<OffsetDateTime> = row.try_get("next_attempt_at")?;
    if lease.is_some_and(|t| t > OffsetDateTime::now_utc()) {
        return Err(ApplyError::RefundInFlight);
    }
    let item = if succeeded {
        settle_refund_tx(&mut tx, id).await?
    } else {
        sqlx::query(
            r#"
            UPDATE pay_rs.refunds
               SET status = 'failed', next_attempt_at = NULL
             WHERE id = $1
            "#,
        )
        .bind(id)
        .execute(&mut *tx)
        .await?;
        load_item(&mut tx, id).await?
    };
    tx.commit().await?;
    Ok(item)
}

async fn settle_refund_tx(
    tx: &mut Transaction<'_, Postgres>,
    id: Uuid,
) -> Result<RefundListItem, ApplyError> {
    let row = sqlx::query(
        r#"
        SELECT id, tenant_id, payment_id, charge_id, amount_minor, currency, exponent,
               status, rail, reason, created_at, idempotency_key
          FROM pay_rs.refunds
         WHERE id = $1
         FOR UPDATE
        "#,
    )
    .bind(id)
    .fetch_optional(&mut **tx)
    .await?
    .ok_or(ApplyError::NotFound)?;
    let status: String = row.try_get("status")?;
    if status != "pending" {
        return Err(ApplyError::RefundNotPending);
    }
    let tenant: String = row.try_get("tenant_id")?;
    let payment_id = PaymentId::from_uuid(row.try_get("payment_id")?);
    let charge_id: Option<Uuid> = row.try_get("charge_id")?;
    let quoted = rows::money_from_parts(
        row.try_get("amount_minor")?,
        row.try_get("currency")?,
        row.try_get("exponent")?,
    )?;
    let rail: String = row.try_get("rail")?;
    let reason: String = row.try_get("reason")?;
    let created_at: OffsetDateTime = row.try_get("created_at")?;
    let idempotency_key: Option<String> = row.try_get("idempotency_key")?;

    sqlx::query(
        r#"
        UPDATE pay_rs.refunds
           SET status = 'succeeded', next_attempt_at = NULL, last_error = NULL
         WHERE id = $1
        "#,
    )
    .bind(id)
    .execute(&mut **tx)
    .await?;

    if let Some(cid) = charge_id {
        recompute_charge(tx, cid).await?;
    }

    let number = ref_number(id);
    sqlx::query(
        r#"
        INSERT INTO pay_rs.documents (tenant_id, payment_id, series, number, title)
        VALUES ($1, $2, 'REF', $3, 'Refund')
        ON CONFLICT (tenant_id, number) DO NOTHING
        "#,
    )
    .bind(&tenant)
    .bind(payment_id.as_uuid())
    .bind(&number)
    .execute(&mut **tx)
    .await?;

    if reason == "merchant" {
        insert_journal(
            tx,
            &tenant,
            payment_id,
            quoted,
            JournalEntry::merchant_refund_settled(
                TenantId::new(tenant.clone()),
                payment_id,
                quoted,
            )?,
        )
        .await?;
    } else {
        insert_journal(
            tx,
            &tenant,
            payment_id,
            quoted,
            JournalEntry::late_refund_settled(TenantId::new(tenant.clone()), payment_id, quoted)?,
        )
        .await?;
    }

    let event_id = uuid_wire(id);
    let payload = json!({
        "id": event_id,
        "type": "refund.created",
        "org_id": tenant,
        "api_version": "0.1.0",
        "data": {
            "refund_id": event_id,
            "checkout_id": payment_id.to_wire(),
            "charge_id": charge_id.map(uuid_wire),
            "amount": money_number(quoted),
            "currency": quoted.currency().code.as_str(),
            "number": number,
            "provider": rail,
        }
    });
    enqueue_outbound(tx, &tenant, &event_id, "refund.created", &payload).await?;

    Ok(RefundListItem {
        id,
        tenant_id: tenant,
        payment_id,
        charge_id,
        quoted,
        status: "succeeded".into(),
        rail,
        reason,
        created_at,
        number: Some(number),
        next_attempt_at: None,
        idempotency_key,
    })
}

async fn load_item(
    tx: &mut Transaction<'_, Postgres>,
    id: Uuid,
) -> Result<RefundListItem, ApplyError> {
    let row = sqlx::query(
        r#"
        SELECT r.id, r.tenant_id, r.payment_id, r.charge_id, r.amount_minor, r.currency,
               r.exponent, r.status, r.rail, r.reason, r.created_at, r.next_attempt_at,
               r.idempotency_key, d.number
          FROM pay_rs.refunds r
          LEFT JOIN pay_rs.documents d
            ON d.tenant_id = r.tenant_id AND d.series = 'REF'
           AND d.number = ('REF-TEST-' || replace(r.id::text, '-', ''))
         WHERE r.id = $1
        "#,
    )
    .bind(id)
    .fetch_one(&mut **tx)
    .await?;
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

async fn recompute_charge(
    tx: &mut Transaction<'_, Postgres>,
    charge_id: Uuid,
) -> Result<(), ApplyError> {
    let amount: i64 = sqlx::query_scalar(
        r#"
        SELECT amount_minor
          FROM pay_rs.charges
         WHERE id = $1
         FOR UPDATE
        "#,
    )
    .bind(charge_id)
    .fetch_optional(&mut **tx)
    .await?
    .ok_or(ApplyError::NotFound)?;
    let succeeded: i64 = sqlx::query_scalar(
        r#"
        SELECT COALESCE(SUM(amount_minor), 0)::bigint
          FROM pay_rs.refunds
         WHERE charge_id = $1 AND status = 'succeeded'
        "#,
    )
    .bind(charge_id)
    .fetch_one(&mut **tx)
    .await?;
    let status = if succeeded >= amount {
        "refunded"
    } else if succeeded > 0 {
        "partially_refunded"
    } else {
        "paid"
    };
    sqlx::query("UPDATE pay_rs.charges SET status = $2 WHERE id = $1")
        .bind(charge_id)
        .bind(status)
        .execute(&mut **tx)
        .await?;
    Ok(())
}

async fn insert_journal(
    tx: &mut Transaction<'_, Postgres>,
    tenant: &str,
    payment_id: PaymentId,
    amount: Money,
    entry: JournalEntry,
) -> Result<(), ApplyError> {
    let eid = Uuid::new_v4();
    sqlx::query(
        r#"
        INSERT INTO pay_rs.journal_entries (id, tenant_id, payment_id, currency, exponent)
        VALUES ($1, $2, $3, $4, $5)
        "#,
    )
    .bind(eid)
    .bind(tenant)
    .bind(payment_id.as_uuid())
    .bind(amount.currency().code.as_str())
    .bind(i16::from(amount.currency().exponent))
    .execute(&mut **tx)
    .await?;
    for line in entry.lines() {
        sqlx::query(
            r#"
            INSERT INTO pay_rs.journal_lines (entry_id, account, dc, amount_minor)
            VALUES ($1, $2, $3, $4)
            "#,
        )
        .bind(eid)
        .bind(rows::account_sql(line.account))
        .bind(rows::dc_sql(line.dc))
        .bind(line.amount.minor() as i64)
        .execute(&mut **tx)
        .await?;
    }
    Ok(())
}
