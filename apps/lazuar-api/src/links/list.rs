//! Port of `PaymentLinks/PaymentLinkEndpoints.List` — cursor pagination that
//! never leaks rows across orgs.
//!
//! Issue 013: stale responses — the list follows its cursor to the end instead
//! of truncating at the first page (merchant SPA fix, 006). Issue 015: the
//! cursor row is org-scoped — a foreign-org cursor id resolves to nothing, so
//! the next page starts from the top rather than leaking another org's rows
//! (the cursor lookup is not a cross-org oracle).

use chrono::{DateTime, Utc};
use postgres::Client;
use rust_decimal::Decimal;
use serde::Serialize;

pub const DEFAULT_LIMIT: i64 = 50;
/// C# `PayList.Clamp` caps at 100 — the earlier 200 was a divergence.
pub const MAX_LIMIT: i64 = 100;

pub fn clamp(limit: Option<i64>) -> i64 {
    crate::hosting::clamp_limit(limit)
}

#[derive(Debug, Clone, Serialize)]
pub struct LinkRow {
    pub id: String,
    pub org_id: String,
    pub public_token: String,
    pub provider: String,
    pub product_id: Option<String>,
    pub amount: Decimal,
    pub currency: String,
    pub max_payers: Option<i32>,
    pub created_at: DateTime<Utc>,
}

const COLUMNS: &str = "\
    SELECT \"Id\",\"OrgId\",\"PublicToken\",\"Provider\",\"ProductId\",\"Amount\",\
    \"Currency\",\"MaxPayers\",\"CreatedAt\" FROM public.payment_links";

fn map_row(row: &postgres::Row) -> LinkRow {
    LinkRow {
        id: row.get("Id"),
        org_id: row.get("OrgId"),
        public_token: row.get("PublicToken"),
        provider: row.get("Provider"),
        product_id: row.get("ProductId"),
        amount: row.get("Amount"),
        currency: row.get("Currency"),
        max_payers: row.get("MaxPayers"),
        created_at: row.get("CreatedAt"),
    }
}

#[derive(Debug, Serialize)]
pub struct LinkPage {
    pub items: Vec<LinkRow>,
    pub next_cursor: Option<String>,
}

/// Org-scoped listing: newest first, `after` continues the page. A foreign-org
/// cursor id is treated as unknown (page restarts) — never as a window into
/// another org's rows.
pub fn list(
    conn: &mut Client,
    org_id: &str,
    limit: Option<i64>,
    after: Option<&str>,
) -> Result<LinkPage, postgres::Error> {
    let take = clamp(limit);

    // Issue 015: the cursor row must belong to this org or it does not exist.
    let cursor = match after.map(str::trim).filter(|s| !s.is_empty()) {
        Some(after_id) => conn
            .query_opt(
                &format!("{COLUMNS} WHERE \"OrgId\" = $1 AND \"Id\" = $2"),
                &[&org_id, &after_id],
            )?
            .map(|row| map_row(&row)),
        None => None,
    };

    let rows = match cursor {
        Some(cursor_row) => {
            let created: DateTime<Utc> = cursor_row.created_at;
            let id = cursor_row.id.clone();
            conn.query(
                &format!(
                    "{COLUMNS} WHERE \"OrgId\" = $1 \
                     AND (\"CreatedAt\" < $2 OR (\"CreatedAt\" = $2 AND \"Id\" < $3)) \
                     ORDER BY \"CreatedAt\" DESC, \"Id\" DESC LIMIT $4"
                ),
                &[&org_id, &created, &id, &(take + 1)],
            )?
        }
        None => conn.query(
            &format!(
                "{COLUMNS} WHERE \"OrgId\" = $1 \
                 ORDER BY \"CreatedAt\" DESC, \"Id\" DESC LIMIT $2"
            ),
            &[&org_id, &(take + 1)],
        )?,
    };

    let mut items: Vec<LinkRow> = rows.iter().map(|row| map_row(row)).collect();
    let mut next_cursor = None;
    if items.len() as i64 > take {
        items.truncate(take as usize);
        next_cursor = items.last().map(|last| last.id.clone());
    }

    Ok(LinkPage { items, next_cursor })
}
