use axum::extract::{Path, Query, State};
use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use axum::Json;
use domain::money::{Currency, Money};
use domain::rail::{rail_supports_currency, RailId};
use domain::PaymentLinkId;
use rust_decimal::Decimal;
use serde::Deserialize;
use serde_json::{json, Value};
use uuid::Uuid;

use crate::errors::{from_apply, problem};
use crate::identity::{require_member, require_writer, Bearer};
use crate::json::{de_decimal, money_number};
use crate::AppState;

#[derive(Deserialize)]
pub struct CreateLinkRequest {
    #[serde(default)]
    pub org_id: String,
    pub provider: String,
    #[serde(deserialize_with = "de_decimal")]
    pub amount: Decimal,
    pub currency: Option<String>,
    pub product_id: Option<String>,
    pub max_payers: Option<i32>,
    #[serde(default)]
    pub unlimited: bool,
    pub label: Option<String>,
}

#[derive(Deserialize)]
pub struct ListQuery {
    pub limit: Option<i64>,
    pub after: Option<String>,
}

pub async fn create(
    State(st): State<AppState>,
    Bearer(who): Bearer,
    Json(body): Json<CreateLinkRequest>,
) -> Response {
    let org_id = body.org_id.trim().to_string();
    if let Err(r) = require_writer(&who, &org_id) {
        return r;
    }
    match storage::read::charges_paused(&st.pool, &org_id).await {
        Ok(true) => {
            return problem(StatusCode::FORBIDDEN, "Forbidden", "Org charges are paused");
        }
        Ok(false) => {}
        Err(e) => return from_apply(e, true),
    }
    let provider = body.provider.trim().to_ascii_lowercase();
    if provider == RailId::TEST.as_str() {
        if !st.env.allows_test() {
            return problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                "test processor is not enabled",
            );
        }
    } else if RailId::parse(&provider).is_ok()
        && ["stripe", "chip", "billplz", "xendit", "razorpay", "solana"]
            .contains(&provider.as_str())
    {
        match storage::get_credential(&st.pool, &org_id, &provider).await {
            Ok(Some(_)) => {}
            Ok(None) => {
                return problem(
                    StatusCode::BAD_REQUEST,
                    "Bad Request",
                    "rail not configured",
                );
            }
            Err(e) => return from_apply(e, true),
        }
    } else if RailId::parse(&provider).is_err() {
        return problem(StatusCode::BAD_REQUEST, "Bad Request", "unknown provider");
    } else {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "rail not configured",
        );
    }
    let currency = body
        .currency
        .as_deref()
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .unwrap_or(if provider == "solana" { "" } else { "MYR" });
    if provider == "solana" {
        let n = currency.to_ascii_uppercase();
        if n.is_empty() || (n != "USDC" && n != "MYR" && n != "USD") {
            return problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                "solana currency must be USDC",
            );
        }
        if n == "MYR" {
            return problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                "solana does not capture ringgit",
            );
        }
        if n == "USD" {
            return problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                "solana receives USDC, not USD",
            );
        }
    }
    let Some(ccy) = Currency::by_code(currency) else {
        return problem(StatusCode::BAD_REQUEST, "Bad Request", "unknown currency");
    };
    let quoted = match Money::from_quoted_display(body.amount, ccy) {
        Ok(m) if m.minor() > 0 => m,
        _ => {
            return problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                "amount must be greater than 0",
            );
        }
    };
    let rail = RailId::parse(&provider).unwrap_or(RailId::TEST);
    if !rail_supports_currency(rail, ccy) {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "currency not supported on this rail",
        );
    }
    let max_payers = if body.unlimited {
        None
    } else {
        let n = body.max_payers.unwrap_or(1);
        if !(1..=1_000_000).contains(&n) {
            return problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                "max_payers must be at least 1",
            );
        }
        Some(n)
    };
    let product_id = body
        .product_id
        .as_deref()
        .map(str::trim)
        .filter(|s| !s.is_empty());
    let mut product_uuid = None;
    if let Some(pid) = product_id {
        let Some(id) = parse_id(pid) else {
            return problem(StatusCode::NOT_FOUND, "Not Found", "product not found");
        };
        match storage::catalog::product_price(&st.pool, &org_id, id).await {
            Ok(None) => {
                return problem(StatusCode::NOT_FOUND, "Not Found", "product not found");
            }
            Ok(Some(p)) if p.quoted.minor() > 0 && p.quoted != quoted => {
                return problem(
                    StatusCode::BAD_REQUEST,
                    "Bad Request",
                    "amount must match the catalog price",
                );
            }
            Ok(Some(_)) => product_uuid = Some(id),
            Err(e) => return from_apply(e, true),
        }
    }
    let label = trim_label(body.label.as_deref());
    let token = unguessable_token();
    match storage::catalog::insert_link(
        &st.pool,
        &org_id,
        &token,
        &provider,
        product_uuid,
        quoted,
        max_payers,
        label.as_deref(),
    )
    .await
    {
        Ok(row) => {
            let occ = storage::catalog::Occupancy { taken: 0, paid: 0 };
            (
                StatusCode::CREATED,
                Json(link_json(&st, &row, &occ, label.as_deref())),
            )
                .into_response()
        }
        Err(e) => from_apply(e, true),
    }
}

pub async fn list(
    State(st): State<AppState>,
    Bearer(who): Bearer,
    Path(org_id): Path<String>,
    Query(q): Query<ListQuery>,
) -> Response {
    if let Err(r) = require_member(&who, &org_id) {
        return r;
    }
    let limit = storage::catalog::clamp_limit(q.limit);
    let after = q.after.as_deref().and_then(parse_id);
    match storage::catalog::list_links(&st.pool, &org_id, limit, after).await {
        Ok((rows, next)) => {
            let mut items = Vec::new();
            for row in rows {
                let occ = storage::catalog::occupancy(&st.pool, row.id)
                    .await
                    .unwrap_or(storage::catalog::Occupancy { taken: 0, paid: 0 });
                let pname = if let Some(pid) = row.product_id {
                    storage::catalog::product_name(&st.pool, &org_id, pid)
                        .await
                        .ok()
                        .flatten()
                } else {
                    None
                };
                let label = row.label.clone().or(pname);
                items.push(link_json(&st, &row, &occ, label.as_deref()));
            }
            Json(json!({"items": items, "next_cursor": next})).into_response()
        }
        Err(e) => from_apply(e, false),
    }
}

pub fn link_json(
    st: &AppState,
    row: &storage::catalog::PaymentLinkRow,
    occ: &storage::catalog::Occupancy,
    label: Option<&str>,
) -> Value {
    let pay_url = format!(
        "{}/c/{}",
        st.checkout_base_url.trim_end_matches('/'),
        row.public_token
    );
    json!({
        "id": row.id.to_wire(),
        "org_id": row.tenant_id.as_str(),
        "provider": row.rail,
        "amount": money_number(row.quoted),
        "currency": row.quoted.currency().code.as_str(),
        "status": occ.merchant_status(row.max_payers),
        "public_token": row.public_token,
        "pay_url": pay_url,
        "created_at": row.created_at.format(&time::format_description::well_known::Rfc3339).unwrap_or_default(),
        "max_payers": row.max_payers,
        "unlimited": row.max_payers.is_none(),
        "paid_count": occ.paid,
        "taken_count": occ.taken,
        "remaining": occ.remaining_unclamped(row.max_payers),
        "label": label.or(row.label.as_deref()),
    })
}

fn trim_label(raw: Option<&str>) -> Option<String> {
    let t = raw.map(str::trim).filter(|s| !s.is_empty())?;
    Some(if t.len() > 80 {
        t[..80].to_string()
    } else {
        t.to_string()
    })
}

fn unguessable_token() -> String {
    format!("{}{}", Uuid::new_v4().simple(), Uuid::new_v4().simple())
}

fn parse_id(s: &str) -> Option<Uuid> {
    PaymentLinkId::from_wire(s)
        .ok()
        .map(|id| id.as_uuid())
        .or_else(|| Uuid::parse_str(s).ok())
}
