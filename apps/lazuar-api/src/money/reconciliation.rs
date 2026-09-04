//! Port of `Money/RefundReconciliationWorker.cs` — log stale pending refunds.
//! Does NOT auto-retry (issue 001: ambiguous outcomes need a human).

use postgres::Client;

pub struct StaleRefund {
    pub id: String,
    pub org_id: String,
    pub checkout_id: String,
    pub amount: rust_decimal::Decimal,
    pub currency: String,
    pub created_at: chrono::DateTime<chrono::Utc>,
}

/// Returns pending refunds older than `stale_minutes`, capped at `limit`.
pub fn list_stale(
    conn: &mut Client,
    stale_minutes: i64,
    limit: i64,
) -> Result<Vec<StaleRefund>, postgres::Error> {
    let cutoff = chrono::Utc::now() - chrono::Duration::minutes(stale_minutes.max(1));
    let rows = conn.query(
        "SELECT \"Id\",\"OrgId\",\"CheckoutId\",\"Amount\",\"Currency\",\"CreatedAt\" \
         FROM public.refunds \
         WHERE \"Status\" = 'pending' AND \"CreatedAt\" < $1 \
         ORDER BY \"CreatedAt\" ASC LIMIT $2",
        &[&cutoff, &limit],
    )?;
    Ok(rows
        .iter()
        .map(|row| StaleRefund {
            id: row.get("Id"),
            org_id: row.get("OrgId"),
            checkout_id: row.get("CheckoutId"),
            amount: row.get("Amount"),
            currency: row.get("Currency"),
            created_at: row.get("CreatedAt"),
        })
        .collect())
}

pub fn run_once(conn: &mut Client) -> Result<usize, postgres::Error> {
    let stale = list_stale(conn, 30, 100)?;
    for row in &stale {
        log::warn!(
            "stale pending refund: id={} org={} checkout={} amount={} {} created={}",
            row.id, row.org_id, row.checkout_id, row.amount, row.currency, row.created_at
        );
    }
    if !stale.is_empty() {
        log::warn!(
            "{} pending refund(s) older than 30 minutes — reconciliation required",
            stale.len()
        );
    }
    Ok(stale.len())
}
