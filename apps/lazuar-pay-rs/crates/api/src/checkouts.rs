use axum::extract::{Path, State};
use axum::http::{HeaderMap, StatusCode};
use axum::response::{IntoResponse, Response};
use axum::Json;
use domain::money::{Currency, Money};
use domain::rail::{rail_supports_currency, RailId};
use domain::wire::buyer_status;
use domain::{PublicToken, TenantId};
use rust_decimal::Decimal;
use serde::Deserialize;
use serde_json::{json, Value};
use sha2::{Digest, Sha256};
use storage::{apply, ApplyCmd, MintSpec};
use time::{Duration, OffsetDateTime};
use uuid::Uuid;

use crate::errors::{from_apply, problem};
use crate::identity::{require_writer, Bearer};
use crate::json::{de_decimal, money_number};
use crate::AppState;

#[derive(Deserialize)]
pub struct CreateCheckoutRequest {
    #[serde(default)]
    pub org_id: String,
    pub provider: String,
    #[serde(deserialize_with = "de_decimal")]
    pub amount: Decimal,
    pub currency: Option<String>,
    pub success_url: Option<String>,
    pub cancel_url: Option<String>,
    pub idempotency_key: Option<String>,
    pub interval: Option<String>,
}

pub async fn create(
    State(st): State<AppState>,
    Bearer(who): Bearer,
    headers: HeaderMap,
    Json(body): Json<CreateCheckoutRequest>,
) -> Response {
    let org_id = body.org_id.trim().to_string();
    if let Err(r) = require_writer(&who, &org_id) {
        return r;
    }
    if matches!(body.interval.as_deref(), Some("mo") | Some("yr")) {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "recurring billing is not offered",
        );
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
    } else if provider == RailId::STRIPE.as_str()
        || provider == RailId::CHIP.as_str()
        || provider == RailId::BILLPLZ.as_str()
        || provider == RailId::XENDIT.as_str()
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
    } else {
        if RailId::parse(&provider).is_err() {
            return problem(StatusCode::BAD_REQUEST, "Bad Request", "unknown provider");
        }
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "rail not configured",
        );
    }
    match storage::read::charges_paused(&st.pool, &org_id).await {
        Ok(true) => {
            return problem(StatusCode::FORBIDDEN, "Forbidden", "Org charges are paused");
        }
        Ok(false) => {}
        Err(e) => return from_apply(e, true),
    }
    let currency = body
        .currency
        .as_deref()
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .unwrap_or("MYR");
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

    let header_key = headers
        .get("Idempotency-Key")
        .and_then(|v| v.to_str().ok())
        .map(str::trim)
        .filter(|s| !s.is_empty());
    let idem = header_key
        .map(str::to_string)
        .or_else(|| body.idempotency_key.clone());
    let hash = idem_hash(&org_id, &provider, &quoted, body.success_url.as_deref());
    if let Some(key) = idem.as_deref() {
        match storage::read::lookup_idempotency(&st.pool, &org_id, key).await {
            Ok(Some((id, stored))) if stored == hash => {
                if let Ok(Some(view)) =
                    storage::read::payment_by_id(&st.pool, domain::PaymentId::from_uuid(id)).await
                {
                    return (StatusCode::OK, Json(checkout_json(&st, &view))).into_response();
                }
            }
            Ok(Some(_)) => {
                return problem(
                    StatusCode::CONFLICT,
                    "Conflict",
                    "Idempotency-Key reused with a different body",
                );
            }
            Ok(None) => {}
            Err(e) => return from_apply(e, true),
        }
    }

    let now = OffsetDateTime::now_utc();
    let token = unguessable_token();
    let minted = apply(
        &st.pool,
        ApplyCmd::Mint(MintSpec {
            tenant_id: TenantId::new(org_id.clone()),
            public_token: PublicToken::new(token),
            quoted,
            expires_at: now + Duration::minutes(30),
            monitoring_until: now + Duration::minutes(30),
            payment_link_id: None,
            slot_key: None,
            success_url: body.success_url.clone(),
            cancel_url: body.cancel_url.clone(),
            rail,
        }),
    )
    .await;
    let payment_id = match minted {
        Ok(storage::ApplyOutcome::Minted { payment_id }) => payment_id,
        Ok(_) => {
            return problem(
                StatusCode::INTERNAL_SERVER_ERROR,
                "Internal Server Error",
                "mint",
            );
        }
        Err(e) => return from_apply(e, true),
    };
    if let Some(key) = idem.as_deref() {
        let _ =
            storage::read::insert_idempotency(&st.pool, &org_id, key, payment_id.as_uuid(), &hash)
                .await;
    }
    match storage::read::payment_by_id(&st.pool, payment_id).await {
        Ok(Some(view)) => (StatusCode::CREATED, Json(checkout_json(&st, &view))).into_response(),
        Ok(None) => problem(StatusCode::NOT_FOUND, "Not Found", "checkout not found"),
        Err(e) => from_apply(e, true),
    }
}

pub async fn get(
    State(st): State<AppState>,
    Bearer(who): Bearer,
    Path(id): Path<String>,
) -> Response {
    let Ok(pid) = domain::PaymentId::from_wire(&id) else {
        return problem(StatusCode::BAD_REQUEST, "Bad Request", "invalid id");
    };
    match storage::read::payment_by_id(&st.pool, pid).await {
        Ok(Some(view)) => {
            let org = view.tenant_id.as_str();
            match who.tenants.iter().find(|t| t.id == org) {
                None => problem(StatusCode::NOT_FOUND, "Not Found", "Checkout not found"),
                Some(t) if t.status.as_deref() != Some("active") => {
                    problem(StatusCode::FORBIDDEN, "Forbidden", "Tenant is suspended.")
                }
                Some(_) => Json(checkout_json(&st, &view)).into_response(),
            }
        }
        Ok(None) => problem(StatusCode::NOT_FOUND, "Not Found", "Checkout not found"),
        Err(e) => from_apply(e, true),
    }
}

fn checkout_json(st: &AppState, view: &storage::PaymentView) -> Value {
    let pay_url = format!(
        "{}/c/{}",
        st.checkout_base_url.trim_end_matches('/'),
        view.public_token.as_str()
    );
    json!({
        "id": view.id.to_wire(),
        "org_id": view.tenant_id.as_str(),
        "provider": view.provider.as_deref().unwrap_or("test"),
        "amount": money_number(view.quoted),
        "currency": view.quoted.currency().code.as_str(),
        "status": buyer_status(view.status),
        "public_token": view.public_token.as_str(),
        "pay_url": pay_url,
        "success_url": view.success_url,
        "cancel_url": view.cancel_url,
        "created_at": view.created_at.format(&time::format_description::well_known::Rfc3339).unwrap_or_default(),
    })
}

fn unguessable_token() -> String {
    let d = Sha256::digest(Uuid::new_v4().as_bytes());
    hex::encode(d)
}

fn idem_hash(org: &str, provider: &str, quoted: &Money, success: Option<&str>) -> String {
    let mut h = Sha256::new();
    h.update(org.as_bytes());
    h.update(provider.as_bytes());
    h.update(quoted.minor().to_string().as_bytes());
    h.update(quoted.currency().code.as_str().as_bytes());
    if let Some(s) = success {
        h.update(s.as_bytes());
    }
    hex::encode(h.finalize())
}
