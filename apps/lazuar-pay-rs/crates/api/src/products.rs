use axum::extract::{Path, Query, State};
use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use axum::Json;
use domain::money::{Currency, Money};
use rust_decimal::Decimal;
use serde::Deserialize;
use serde_json::json;
use uuid::Uuid;

use crate::errors::{from_apply, problem};
use crate::identity::{require_member, require_writer, Bearer};
use crate::json::money_number;
use crate::payment_links::ListQuery;
use crate::AppState;

#[derive(Deserialize)]
pub struct CreateProductRequest {
    pub name: Option<String>,
    pub description: Option<String>,
    #[serde(default, deserialize_with = "de_opt_decimal")]
    pub amount: Option<Decimal>,
    pub currency: Option<String>,
    pub interval: Option<String>,
}

fn de_opt_decimal<'de, D>(d: D) -> Result<Option<Decimal>, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let v = Option::<serde_json::Value>::deserialize(d)?;
    match v {
        None | Some(serde_json::Value::Null) => Ok(None),
        Some(serde_json::Value::Number(n)) => Decimal::from_str_exact(&n.to_string())
            .map(Some)
            .map_err(serde::de::Error::custom),
        Some(serde_json::Value::String(s)) => Decimal::from_str_exact(&s)
            .map(Some)
            .map_err(serde::de::Error::custom),
        Some(_) => Err(serde::de::Error::custom("amount must be a number")),
    }
}

pub async fn create(
    State(st): State<AppState>,
    Bearer(who): Bearer,
    Path(org_id): Path<String>,
    Json(body): Json<CreateProductRequest>,
) -> Response {
    if let Err(r) = require_writer(&who, &org_id) {
        return r;
    }
    let Some(name) = body
        .name
        .as_deref()
        .map(str::trim)
        .filter(|s| !s.is_empty())
    else {
        return problem(StatusCode::BAD_REQUEST, "Bad Request", "name is required");
    };
    let currency = body
        .currency
        .as_deref()
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .unwrap_or("MYR")
        .to_ascii_uppercase();
    if currency != "MYR" {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "Bar B currency is MYR",
        );
    }
    if matches!(body.interval.as_deref(), Some("mo") | Some("yr")) {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "recurring billing is not offered",
        );
    }
    let Some(amount) = body.amount else {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "amount must be greater than 0",
        );
    };
    let quoted = match Money::from_quoted_display(amount, Currency::MYR) {
        Ok(m) if m.minor() > 0 => m,
        _ => {
            return problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                "amount must be greater than 0",
            );
        }
    };
    match storage::catalog::insert_product(
        &st.pool,
        &org_id,
        name,
        body.description
            .as_deref()
            .map(str::trim)
            .filter(|s| !s.is_empty()),
        quoted,
    )
    .await
    {
        Ok((id, price_id)) => (
            StatusCode::CREATED,
            Json(json!({
                "id": id.as_simple().to_string(),
                "org_id": org_id,
                "name": name,
                "price_id": price_id.as_simple().to_string(),
                "amount": money_number(quoted),
                "currency": "MYR",
                "interval": "one_off",
            })),
        )
            .into_response(),
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
    let after = q.after.as_deref().and_then(|s| {
        Uuid::try_parse(s).ok().or_else(|| {
            let mut hex = s.to_string();
            if hex.len() == 32 {
                hex.insert(8, '-');
                hex.insert(13, '-');
                hex.insert(18, '-');
                hex.insert(23, '-');
                Uuid::parse_str(&hex).ok()
            } else {
                None
            }
        })
    });
    match storage::catalog::list_products(&st.pool, &org_id, limit, after).await {
        Ok((rows, next)) => {
            let items: Vec<_> = rows
                .iter()
                .map(|p| {
                    let amount = match (p.amount_minor, p.currency.as_deref(), p.exponent) {
                        (Some(m), Some(c), Some(e)) => Currency::by_code(c)
                            .and_then(|ccy| Money::from_minor(i128::from(m), ccy).ok())
                            .map(|q| {
                                let _ = e;
                                money_number(q)
                            }),
                        _ => None,
                    };
                    json!({
                        "id": p.id.as_simple().to_string(),
                        "org_id": p.tenant_id,
                        "name": p.name,
                        "prices": if let (Some(pid), Some(amt), Some(cur), Some(iv)) =
                            (p.price_id, amount, p.currency.as_deref(), p.interval.as_deref())
                        {
                            json!([{
                                "id": pid.as_simple().to_string(),
                                "amount": amt,
                                "currency": cur,
                                "interval": iv,
                            }])
                        } else {
                            json!([])
                        }
                    })
                })
                .collect();
            Json(json!({"items": items, "next_cursor": next})).into_response()
        }
        Err(e) => from_apply(e, false),
    }
}
