//! Port of `Money/Queries/PaymentQueryEndpoints.cs` + subscription list.

use postgres::Client;
use rust_decimal::Decimal;
use serde_json::{json, Value};

use crate::hosting::clamp_limit;

pub fn list_payments(
    conn: &mut Client,
    org_id: &str,
    limit: Option<i64>,
    after: Option<&str>,
) -> Result<Value, postgres::Error> {
    let take = clamp_limit(limit);
    let after_row: Option<(String, chrono::DateTime<chrono::Utc>)> = match after.map(str::trim).filter(|s| !s.is_empty()) {
        Some(after_id) => conn
            .query_opt(
                "SELECT c.\"Id\", ch.\"CreatedAt\" FROM public.charges c \
                 JOIN public.checkouts ch ON c.\"CheckoutId\" = ch.\"Id\" \
                 WHERE c.\"OrgId\" = $1 AND c.\"Id\" = $2",
                &[&org_id, &after_id],
            )?
            .map(|row| (row.get(0), row.get(1))),
        None => None,
    };
    let rows = match &after_row {
        Some((id, created)) => conn.query(
            "SELECT c.\"Id\", c.\"OrgId\", c.\"CheckoutId\", c.\"Amount\", c.\"Currency\", \
                    c.\"Status\", c.\"Provider\", ch.\"PayerName\", ch.\"CreatedAt\", ch.\"ProductId\" \
             FROM public.charges c JOIN public.checkouts ch ON c.\"CheckoutId\" = ch.\"Id\" \
             WHERE c.\"OrgId\" = $1 AND (ch.\"CreatedAt\" < $2 OR (ch.\"CreatedAt\" = $2 AND c.\"Id\" < $3)) \
             ORDER BY ch.\"CreatedAt\" DESC, c.\"Id\" DESC LIMIT $4",
            &[&org_id, created, id, &(take + 1)],
        )?,
        None => conn.query(
            "SELECT c.\"Id\", c.\"OrgId\", c.\"CheckoutId\", c.\"Amount\", c.\"Currency\", \
                    c.\"Status\", c.\"Provider\", ch.\"PayerName\", ch.\"CreatedAt\", ch.\"ProductId\" \
             FROM public.charges c JOIN public.checkouts ch ON c.\"CheckoutId\" = ch.\"Id\" \
             WHERE c.\"OrgId\" = $1 \
             ORDER BY ch.\"CreatedAt\" DESC, c.\"Id\" DESC LIMIT $2",
            &[&org_id, &(take + 1)],
        )?,
    };
    let has_more = rows.len() as i64 > take;
    let page: Vec<_> = rows.into_iter().take(take as usize).collect();
    let mut items = Vec::new();
    for row in &page {
        let product_id: Option<String> = row.get("ProductId");
        let label = match product_id.as_deref().filter(|s| !s.is_empty()) {
            Some(pid) => conn
                .query_opt("SELECT \"Name\" FROM public.products WHERE \"Id\" = $1", &[&pid])?
                .map(|r| r.get::<_, String>(0)),
            None => None,
        };
        let amount: Decimal = row.get("Amount");
        items.push(json!({
            "id": row.get::<_, String>("Id"),
            "org_id": row.get::<_, String>("OrgId"),
            "checkout_id": row.get::<_, String>("CheckoutId"),
            "amount": crate::hosting::decimal_json(amount),
            "currency": row.get::<_, String>("Currency"),
            "status": row.get::<_, String>("Status"),
            "provider": row.get::<_, Option<String>>("Provider"),
            "payer_name": row.get::<_, Option<String>>("PayerName"),
            "created_at": row.get::<_, chrono::DateTime<chrono::Utc>>("CreatedAt"),
            "label": label,
        }));
    }
    let next_cursor = if has_more {
        page.last().map(|r| r.get::<_, String>("Id"))
    } else {
        None
    };
    Ok(json!({ "items": items, "next_cursor": next_cursor }))
}

pub fn list_receipts(
    conn: &mut Client,
    org_id: &str,
    limit: Option<i64>,
    after: Option<&str>,
) -> Result<Value, postgres::Error> {
    let take = clamp_limit(limit);
    let after_row: Option<(String, chrono::DateTime<chrono::Utc>)> = match after.map(str::trim).filter(|s| !s.is_empty()) {
        Some(after_id) => conn
            .query_opt(
                "SELECT \"Id\",\"CreatedAt\" FROM public.documents \
                 WHERE \"OrgId\" = $1 AND \"Id\" = $2",
                &[&org_id, &after_id],
            )?
            .map(|row| (row.get(0), row.get(1))),
        None => None,
    };
    let rows = match &after_row {
        Some((id, created)) => conn.query(
            "SELECT \"Id\",\"OrgId\",\"CheckoutId\",\"Number\",\"Title\",\"CreatedAt\" FROM public.documents \
             WHERE \"OrgId\" = $1 AND (\"CreatedAt\" < $2 OR (\"CreatedAt\" = $2 AND \"Id\" < $3)) \
             ORDER BY \"CreatedAt\" DESC, \"Id\" DESC LIMIT $4",
            &[&org_id, created, id, &(take + 1)],
        )?,
        None => conn.query(
            "SELECT \"Id\",\"OrgId\",\"CheckoutId\",\"Number\",\"Title\",\"CreatedAt\" FROM public.documents \
             WHERE \"OrgId\" = $1 ORDER BY \"CreatedAt\" DESC, \"Id\" DESC LIMIT $2",
            &[&org_id, &(take + 1)],
        )?,
    };
    let has_more = rows.len() as i64 > take;
    let page: Vec<_> = rows.into_iter().take(take as usize).collect();
    let mut items = Vec::new();
    for row in &page {
        items.push(receipt_item(conn, row)?);
    }
    let next_cursor = if has_more {
        page.last().map(|r| r.get::<_, String>("Id"))
    } else {
        None
    };
    Ok(json!({ "items": items, "next_cursor": next_cursor }))
}

fn receipt_item(conn: &mut Client, row: &postgres::Row) -> Result<Value, postgres::Error> {
    let number: Option<String> = row.get("Number");
    let status = if number.as_deref().map(str::trim).filter(|s| !s.is_empty()).is_none() {
        "pending"
    } else {
        "issued"
    };
    let checkout_id: Option<String> = row.get("CheckoutId");
    let mut payer_name: Option<String> = None;
    let mut amount: Option<Decimal> = None;
    let mut currency: Option<String> = None;
    let mut product_id: Option<String> = None;
    if let Some(cid) = checkout_id.as_deref().filter(|s| !s.is_empty()) {
        if let Some(ch) = conn.query_opt(
            "SELECT \"PayerName\",\"Amount\",\"Currency\",\"ProductId\" FROM public.checkouts WHERE \"Id\" = $1",
            &[&cid],
        )? {
            payer_name = ch.get("PayerName");
            amount = Some(ch.get("Amount"));
            currency = Some(ch.get("Currency"));
            product_id = ch.get("ProductId");
        }
        if let Some(charge) = conn.query_opt(
            "SELECT \"Amount\",\"Currency\" FROM public.charges WHERE \"CheckoutId\" = $1 LIMIT 1",
            &[&cid],
        )? {
            amount = Some(charge.get("Amount"));
            currency = Some(charge.get("Currency"));
        }
    }
    let label = match product_id.as_deref().filter(|s| !s.is_empty()) {
        Some(pid) => conn
            .query_opt("SELECT \"Name\" FROM public.products WHERE \"Id\" = $1", &[&pid])?
            .map(|r| r.get::<_, String>(0)),
        None => None,
    };
    Ok(json!({
        "id": row.get::<_, String>("Id"),
        "org_id": row.get::<_, String>("OrgId"),
        "checkout_id": checkout_id,
        "title": row.get::<_, String>("Title"),
        "number": number.clone().unwrap_or_else(|| "PENDING".into()),
        "status": status,
        "created_at": row.get::<_, chrono::DateTime<chrono::Utc>>("CreatedAt"),
        "payer_name": payer_name,
        "amount": amount.map(crate::hosting::decimal_json),
        "currency": currency,
        "label": label,
    }))
}

pub fn get_receipt(conn: &mut Client, org_id: &str, id: &str) -> Result<Option<Value>, postgres::Error> {
    let row = conn.query_opt(
        "SELECT \"Id\",\"OrgId\",\"CheckoutId\",\"Number\",\"Title\",\"CreatedAt\" FROM public.documents \
         WHERE \"OrgId\" = $1 AND \"Id\" = $2",
        &[&org_id, &id],
    )?;
    match row {
        Some(r) => Ok(Some(receipt_item(conn, &r)?)),
        None => Ok(None),
    }
}

/// Cursor lookup is **not** org-scoped — C# SubscriptionEndpoints.cs deliberate exception.
pub fn list_subscriptions(
    conn: &mut Client,
    org_id: &str,
    limit: Option<i64>,
    after: Option<&str>,
) -> Result<Value, postgres::Error> {
    let take = clamp_limit(limit);
    let after_row: Option<(String, chrono::DateTime<chrono::Utc>)> = match after.map(str::trim).filter(|s| !s.is_empty()) {
        Some(after_id) => conn
            .query_opt(
                "SELECT \"Id\",\"CreatedAt\" FROM public.subscriptions WHERE \"Id\" = $1",
                &[&after_id],
            )?
            .map(|row| (row.get(0), row.get(1))),
        None => None,
    };
    let rows = match &after_row {
        Some((id, created)) => conn.query(
            "SELECT \"Id\",\"OrgId\",\"CheckoutId\",\"Interval\",\"Status\",\"AttemptCount\",\"PastDueAt\",\"CreatedAt\" \
             FROM public.subscriptions \
             WHERE \"OrgId\" = $1 AND (\"CreatedAt\" < $2 OR (\"CreatedAt\" = $2 AND \"Id\" < $3)) \
             ORDER BY \"CreatedAt\" DESC, \"Id\" DESC LIMIT $4",
            &[&org_id, created, id, &(take + 1)],
        )?,
        None => conn.query(
            "SELECT \"Id\",\"OrgId\",\"CheckoutId\",\"Interval\",\"Status\",\"AttemptCount\",\"PastDueAt\",\"CreatedAt\" \
             FROM public.subscriptions WHERE \"OrgId\" = $1 \
             ORDER BY \"CreatedAt\" DESC, \"Id\" DESC LIMIT $2",
            &[&org_id, &(take + 1)],
        )?,
    };
    let has_more = rows.len() as i64 > take;
    let page: Vec<_> = rows.into_iter().take(take as usize).collect();
    let items: Vec<Value> = page
        .iter()
        .map(|row| {
            let status: String = row.get("Status");
            json!({
                "id": row.get::<_, String>("Id"),
                "org_id": row.get::<_, String>("OrgId"),
                "checkout_id": row.get::<_, Option<String>>("CheckoutId"),
                "interval": row.get::<_, Option<String>>("Interval"),
                "status": status,
                "dunning_status": status,
                "attempt_count": row.get::<_, i32>("AttemptCount"),
                "past_due_at": row.get::<_, Option<chrono::DateTime<chrono::Utc>>>("PastDueAt"),
                "created_at": row.get::<_, chrono::DateTime<chrono::Utc>>("CreatedAt"),
            })
        })
        .collect();
    let next_cursor = if has_more {
        page.last().map(|r| r.get::<_, String>("Id"))
    } else {
        None
    };
    Ok(json!({ "items": items, "next_cursor": next_cursor }))
}
