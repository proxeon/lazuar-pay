use axum::extract::{Path, Query, State};
use axum::http::{HeaderMap, StatusCode};
use axum::response::{IntoResponse, Response};
use axum::Json;
use domain::money::{Currency, Money};
use domain::rail::RailId;
use domain::{PaymentId, RefundId};
use rust_decimal::Decimal;
use serde::Deserialize;
use serde_json::json;
use sha2::{Digest, Sha256};
use time::OffsetDateTime;
use uuid::Uuid;
use workers::settler::RefundRemote;
use workers::stripe_remote::StripeRemote;

use crate::boot::Env;
use crate::errors::{from_apply, problem};
use crate::identity::{require_member, require_writer, Bearer};
use crate::json::{de_opt_decimal, money_number};
use crate::payment_links::ListQuery;
use crate::AppState;

#[derive(Deserialize)]
pub struct CreateRefundRequest {
    pub checkout_id: Option<String>,
    #[serde(default, deserialize_with = "de_opt_decimal")]
    pub amount: Option<Decimal>,
    pub idempotency_key: Option<String>,
}

#[derive(Deserialize)]
pub struct ResolveRefundRequest {
    pub status: Option<String>,
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
    let after = q.after.as_deref().and_then(parse_uuid);
    match storage::money_query::list_refunds(&st.pool, &org_id, limit, after).await {
        Ok((rows, next)) => {
            let items: Vec<_> = rows
                .iter()
                .map(|r| refund_json(r, r.number.clone()))
                .collect();
            Json(json!({"items": items, "next_cursor": next})).into_response()
        }
        Err(e) => from_apply(e, false),
    }
}

pub async fn create(
    State(st): State<AppState>,
    Bearer(who): Bearer,
    Path(org_id): Path<String>,
    headers: HeaderMap,
    Json(body): Json<CreateRefundRequest>,
) -> Response {
    if let Err(r) = require_writer(&who, &org_id) {
        return r;
    }
    let checkout_id = body
        .checkout_id
        .as_deref()
        .map(str::trim)
        .filter(|s| !s.is_empty());
    let Some(checkout_id) = checkout_id else {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "checkout_id is required",
        );
    };
    if let Some(requested) = body.amount {
        if exceeds_two_decimals(requested) {
            return problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                "amount must have at most 2 decimal places",
            );
        }
    }
    let header_key = headers
        .get("Idempotency-Key")
        .and_then(|v| v.to_str().ok())
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .map(str::to_string);
    let idem = header_key.or_else(|| {
        body.idempotency_key
            .as_deref()
            .map(str::trim)
            .filter(|s| !s.is_empty())
            .map(str::to_string)
    });
    let key = idem.unwrap_or_else(|| Uuid::new_v4().as_simple().to_string());
    let Ok(payment_id) = PaymentId::from_wire(checkout_id) else {
        return problem(StatusCode::NOT_FOUND, "Not Found", "charge not found");
    };
    match storage::money_query::payment_in_org(&st.pool, &org_id, payment_id).await {
        Ok(true) => {}
        Ok(false) => {
            return problem(StatusCode::NOT_FOUND, "Not Found", "checkout not found");
        }
        Err(e) => return from_apply(e, false),
    }
    let charge = match storage::money_query::charge_for_payment(&st.pool, &org_id, payment_id).await
    {
        Ok(Some(c)) => c,
        Ok(None) => {
            return problem(StatusCode::NOT_FOUND, "Not Found", "charge not found");
        }
        Err(e) => return from_apply(e, false),
    };
    if let Some(existing) =
        match storage::money_query::refund_by_idempotency(&st.pool, &org_id, &key).await {
            Ok(v) => v,
            Err(e) => return from_apply(e, false),
        }
    {
        if existing.payment_id != payment_id
            || body.amount.is_some_and(|amt| {
                money_from_decimal(amt, charge.quoted.currency())
                    .is_ok_and(|m| m.minor() != existing.quoted.minor())
            })
        {
            return problem(
                StatusCode::CONFLICT,
                "Conflict",
                "Idempotency-Key reused with a different body",
            );
        }
        return (
            StatusCode::OK,
            Json(refund_json(&existing, existing.number.clone())),
        )
            .into_response();
    }
    let used = match storage::money_query::reserved_minor(&st.pool, payment_id).await {
        Ok(n) => n,
        Err(e) => return from_apply(e, false),
    };
    let remaining = charge.quoted.minor() - i128::from(used);
    if remaining <= 0 {
        return problem(StatusCode::CONFLICT, "Conflict", "already refunded");
    }
    let amount = match body.amount {
        Some(d) => match money_from_decimal(d, charge.quoted.currency()) {
            Ok(m) => m,
            Err(_) => {
                return problem(
                    StatusCode::BAD_REQUEST,
                    "Bad Request",
                    "amount must have at most 2 decimal places",
                );
            }
        },
        None => match Money::from_minor(remaining, charge.quoted.currency()) {
            Ok(m) => m,
            Err(_) => {
                return problem(
                    StatusCode::BAD_REQUEST,
                    "Bad Request",
                    "amount must be within the refundable remainder",
                );
            }
        },
    };
    if amount.minor() <= 0 || amount.minor() > remaining {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "amount must be within the refundable remainder",
        );
    }
    let hash = refund_hash(checkout_id, body.amount.map(|_| amount.minor()));
    let applied = storage::apply(
        &st.pool,
        storage::ApplyCmd::MerchantRefund {
            payment_id,
            amount,
            idempotency_key: key.clone(),
            request_hash: hash,
            now: OffsetDateTime::now_utc(),
        },
    )
    .await;
    let refund_id = match applied {
        Ok(storage::ApplyOutcome::MerchantRefunded { refund_id }) => refund_id,
        Ok(storage::ApplyOutcome::RefundReplay { refund_id }) => {
            return replay_ok(&st, &org_id, refund_id).await;
        }
        Ok(_) => {
            return problem(
                StatusCode::INTERNAL_SERVER_ERROR,
                "Internal Server Error",
                "refund",
            );
        }
        Err(storage::ApplyError::AlreadyRefunded) => {
            if let Ok(Some(existing)) =
                storage::money_query::refund_by_idempotency(&st.pool, &org_id, &key).await
            {
                return (
                    StatusCode::OK,
                    Json(refund_json(&existing, existing.number.clone())),
                )
                    .into_response();
            }
            return problem(StatusCode::CONFLICT, "Conflict", "already refunded");
        }
        Err(e) => return from_apply(e, false),
    };

    let rail = charge.provider.as_str();
    let caps_refund = RailId::parse(rail)
        .map(|r| r.caps().refund)
        .unwrap_or(false);
    if !caps_refund {
        let _ = storage::refund_settle::fail_refund(&st.pool, refund_id.as_uuid()).await;
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "refund not supported on this rail",
        );
    }
    if rail == RailId::TEST.as_str() {
        return match storage::refund_settle::settle_refund(&st.pool, refund_id.as_uuid()).await {
            Ok(row) => (
                StatusCode::CREATED,
                Json(refund_json(&row, row.number.clone())),
            )
                .into_response(),
            Err(e) => from_apply(e, false),
        };
    }
    if rail == RailId::CHIP.as_str() {
        return pending_created(&st, &org_id, refund_id).await;
    }
    if rail == RailId::STRIPE.as_str() {
        let remote = if st.env == Env::Testing {
            StripeRemote::fake(st.pool.clone(), st.wrap_key, st.stripe.clone())
        } else {
            StripeRemote::live(st.pool.clone(), st.wrap_key)
        };
        match remote.refund("stripe", refund_id.as_uuid()).await {
            domain::RefundOutcome::Settled => {
                match storage::refund_settle::settle_refund(&st.pool, refund_id.as_uuid()).await {
                    Ok(row) => (
                        StatusCode::CREATED,
                        Json(refund_json(&row, row.number.clone())),
                    )
                        .into_response(),
                    Err(e) => from_apply(e, false),
                }
            }
            domain::RefundOutcome::Rejected => {
                let _ = storage::refund_settle::fail_refund(&st.pool, refund_id.as_uuid()).await;
                problem(
                    StatusCode::BAD_GATEWAY,
                    "Bad Gateway",
                    "processor rejected the refund",
                )
            }
            domain::RefundOutcome::Unknown => problem(
                StatusCode::BAD_GATEWAY,
                "Bad Gateway",
                "refund outcome unknown — held pending for reconciliation",
            ),
        }
    } else {
        pending_created(&st, &org_id, refund_id).await
    }
}

pub async fn resolve(
    State(st): State<AppState>,
    Bearer(who): Bearer,
    Path((org_id, id)): Path<(String, String)>,
    Json(body): Json<ResolveRefundRequest>,
) -> Response {
    if let Err(r) = require_writer(&who, &org_id) {
        return r;
    }
    let status = body
        .status
        .as_deref()
        .map(str::trim)
        .map(str::to_ascii_lowercase);
    let succeeded = match status.as_deref() {
        Some("succeeded") => true,
        Some("failed") => false,
        _ => {
            return problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                "status must be succeeded or failed",
            );
        }
    };
    let Some(uid) = parse_uuid(&id) else {
        return problem(StatusCode::NOT_FOUND, "Not Found", "Refund not found");
    };
    match storage::refund_settle::resolve_refund(&st.pool, &org_id, uid, succeeded).await {
        Ok(row) => Json(refund_json(&row, row.number.clone())).into_response(),
        Err(storage::ApplyError::NotFound) => {
            problem(StatusCode::NOT_FOUND, "Not Found", "Refund not found")
        }
        Err(e) => from_apply(e, false),
    }
}

async fn replay_ok(st: &AppState, org_id: &str, refund_id: RefundId) -> Response {
    match storage::money_query::refund_by_id(&st.pool, org_id, refund_id.as_uuid()).await {
        Ok(Some(row)) => {
            (StatusCode::OK, Json(refund_json(&row, row.number.clone()))).into_response()
        }
        Ok(None) => problem(StatusCode::NOT_FOUND, "Not Found", "Refund not found"),
        Err(e) => from_apply(e, false),
    }
}

async fn pending_created(st: &AppState, org_id: &str, refund_id: RefundId) -> Response {
    match storage::money_query::refund_by_id(&st.pool, org_id, refund_id.as_uuid()).await {
        Ok(Some(row)) => (
            StatusCode::CREATED,
            Json(refund_json(&row, row.number.clone())),
        )
            .into_response(),
        Ok(None) => problem(
            StatusCode::INTERNAL_SERVER_ERROR,
            "Internal Server Error",
            "refund",
        ),
        Err(e) => from_apply(e, false),
    }
}

fn refund_json(
    row: &storage::money_query::RefundListItem,
    number: Option<String>,
) -> serde_json::Value {
    json!({
        "id": storage::money_query::uuid_wire(row.id),
        "org_id": row.tenant_id,
        "checkout_id": row.payment_id.to_wire(),
        "charge_id": row.charge_id.map(storage::money_query::uuid_wire),
        "amount": money_number(row.quoted),
        "currency": row.quoted.currency().code.as_str(),
        "status": row.status,
        "provider": row.rail,
        "reason": storage::money_query::wire_reason(&row.reason),
        "number": number,
        "created_at": row.created_at.format(&time::format_description::well_known::Rfc3339).unwrap_or_default(),
    })
}

fn exceeds_two_decimals(d: Decimal) -> bool {
    d.round_dp(2) != d
}

fn money_from_decimal(d: Decimal, ccy: Currency) -> Result<Money, domain::MoneyError> {
    Money::from_quoted_display(d, ccy)
}

fn refund_hash(checkout_id: &str, amount_minor: Option<i128>) -> String {
    let mut h = Sha256::new();
    h.update(checkout_id.as_bytes());
    match amount_minor {
        Some(m) => h.update(m.to_string().as_bytes()),
        None => h.update(b"remainder"),
    }
    hex::encode(h.finalize())
}

fn parse_uuid(s: &str) -> Option<Uuid> {
    RefundId::from_wire(s)
        .ok()
        .map(|id| id.as_uuid())
        .or_else(|| Uuid::parse_str(s).ok())
}
