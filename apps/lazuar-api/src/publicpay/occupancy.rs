//! Port of `PaymentLinks/PaymentLinkOccupancy.cs` + the expiry sweep.
//!
//! A payer is an `open` reservation or a `paid` child. Unpaid `open` rows older
//! than the reservation TTL become `expired` and no longer occupy. Issue 002:
//! expiry is a compare-and-set off "open" — a sweep that read the row while open
//! must not overwrite a just-committed "paid".

use chrono::{DateTime, Utc};
use postgres::Transaction;
use uuid::Uuid;

use crate::domain::transitions;
use crate::webhooks::envelope;
use crate::webhooks::enqueue;

pub const DEFAULT_RESERVATION_TTL_MINUTES: i64 = 30;

pub fn counts_toward_capacity(status: &str) -> bool {
    status == "open" || status == "paid"
}

pub fn is_full(max_payers: Option<i32>, taken: i64) -> bool {
    max_payers.is_some_and(|max| taken >= i64::from(max))
}

pub fn is_over_capacity(max_payers: Option<i32>, taken: i64) -> bool {
    max_payers.is_some_and(|max| taken > i64::from(max))
}

pub fn merchant_status(max_payers: Option<i32>, taken: i64) -> &'static str {
    if is_over_capacity(max_payers, taken) {
        "over_capacity"
    } else if is_full(max_payers, taken) {
        "full"
    } else {
        "open"
    }
}

pub fn remaining(max_payers: Option<i32>, taken: i64) -> Option<i64> {
    max_payers.map(|max| std::cmp::max(0, i64::from(max) - taken))
}

/// C# remaining_unclamped — can be negative when over capacity.
pub fn remaining_unclamped(max_payers: Option<i32>, taken: i64) -> Option<i64> {
    max_payers.map(|max| i64::from(max) - taken)
}

pub fn reservation_ttl(config_minutes: Option<i64>) -> chrono::Duration {
    chrono::Duration::minutes(std::cmp::max(1, config_minutes.unwrap_or(DEFAULT_RESERVATION_TTL_MINUTES)))
}

/// `LockParentAsync` — serialize capacity checks against minting on the parent
/// link row (issue 008).
pub fn lock_parent(tx: &mut Transaction, link_id: &str) -> Result<(), postgres::Error> {
    tx.execute(
        "SELECT 1 FROM public.payment_links WHERE \"Id\" = $1 FOR UPDATE",
        &[&link_id],
    )?;
    Ok(())
}

/// Expire stale open reservations for a link — CAS transitions + expired webhook.
pub fn expire_stale(
    tx: &mut Transaction,
    link_id: &str,
    ttl: chrono::Duration,
) -> Result<Vec<String>, postgres::Error> {
    let cutoff = Utc::now() - ttl;
    let stale: Vec<String> = tx
        .query(
            "SELECT \"Id\" FROM public.checkouts \
             WHERE \"PaymentLinkId\" = $1 AND \"Status\" = 'open' AND \"CreatedAt\" < $2",
            &[&link_id, &cutoff],
        )?
        .iter()
        .filter_map(|row| row.try_get::<_, String>(0).ok())
        .collect();
    mark_expired(tx, stale, "ttl")
}

/// `MarkExpiredAsync` — CAS off "open" per row; the webhook only fires for rows
/// this writer actually expired.
pub fn mark_expired(
    tx: &mut Transaction,
    rows: Vec<String>,
    reason: &str,
) -> Result<Vec<String>, postgres::Error> {
    let mut expired = Vec::with_capacity(rows.len());
    for id in rows {
        let Ok(uuid) = Uuid::parse_str(&id) else { continue };
        if !transitions::try_leave_open(tx, uuid, "expired")? {
            continue;
        }
        let org_id = org_of(tx, &id)?;
        let payment_link_id: Option<String> = tx
            .query_opt(
                "SELECT \"PaymentLinkId\" FROM public.checkouts WHERE \"Id\" = $1",
                &[&id],
            )?
            .and_then(|row| row.get(0));
        enqueue::try_add(
            tx,
            &org_id,
            &format!("expired:{id}"),
            envelope::EXPIRED,
            serde_json::json!({ "checkout_id": id, "payment_link_id": payment_link_id, "reason": reason }),
        )?;
        expired.push(id);
    }
    Ok(expired)
}

fn org_of<C: postgres::GenericClient>(tx: &mut C, checkout_id: &str) -> Result<String, postgres::Error> {
    let row = tx.query_opt(
        "SELECT \"OrgId\" FROM public.checkouts WHERE \"Id\" = $1",
        &[&checkout_id],
    )?;
    Ok(row.map(|r| r.get(0)).unwrap_or_default())
}

/// Timestamped helper for callers assembling expiry evidence.
pub fn now() -> DateTime<Utc> {
    Utc::now()
}
