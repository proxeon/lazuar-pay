use axum::extract::{Path, State};
use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use axum::Json;
use domain::rail::{HostedSession, RailId};
use domain::wire::buyer_status;
use serde::Deserialize;
use serde_json::{json, Value};
use storage::{apply, ApplyCmd, ApplyOutcome};

use crate::errors::{from_apply, problem};
use crate::json::money_number;
use crate::AppState;

#[derive(Default, Deserialize)]
pub struct StartPayRequest {
    pub name: Option<String>,
    pub email: Option<String>,
    pub slot_key: Option<String>,
}

pub async fn get(State(st): State<AppState>, Path(token): Path<String>) -> Response {
    if !st.limiter.try_acquire(&token) {
        return problem(
            StatusCode::TOO_MANY_REQUESTS,
            "Too Many Requests",
            "Too many start attempts",
        );
    }
    match storage::read::payment_by_public_token(&st.pool, &token).await {
        Ok(Some(view)) => Json(public_json(&st, &view)).into_response(),
        Ok(None) => problem(StatusCode::NOT_FOUND, "Not Found", "checkout not found"),
        Err(e) => from_apply(e, false),
    }
}

pub async fn start(
    State(st): State<AppState>,
    Path(token): Path<String>,
    body: Option<Json<StartPayRequest>>,
) -> Response {
    if !st.limiter.try_acquire(&token) {
        return problem(
            StatusCode::TOO_MANY_REQUESTS,
            "Too Many Requests",
            "Too many start attempts",
        );
    }
    let req = body.map(|j| j.0).unwrap_or_default();
    let view = match storage::read::payment_by_public_token(&st.pool, &token).await {
        Ok(Some(v)) => v,
        Ok(None) => return problem(StatusCode::NOT_FOUND, "Not Found", "checkout not found"),
        Err(e) => return from_apply(e, false),
    };
    if let Some(url) = view.session_url.clone() {
        return start_json(&url);
    }
    let rail = view
        .provider
        .as_deref()
        .and_then(|s| RailId::parse(s).ok())
        .unwrap_or(RailId::TEST);
    let email = req
        .email
        .as_deref()
        .map(str::trim)
        .filter(|s| !s.is_empty());
    if rail.caps().requires_email && !email_usable(email) {
        return problem(StatusCode::BAD_REQUEST, "Bad Request", "email is required");
    }
    if rail == RailId::BILLPLZ && !rails::billplz::public_base_ok(&st.public_base_url) {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "callback base not public",
        );
    }
    if email_usable(email) || req.name.as_deref().is_some_and(|s| !s.trim().is_empty()) {
        let _ = storage::update_payer(
            &st.pool,
            view.id.as_uuid(),
            req.name.as_deref().map(str::trim).filter(|s| !s.is_empty()),
            email,
        )
        .await;
    }
    let started = match apply(
        &st.pool,
        ApplyCmd::StartAttempt {
            payment_id: view.id,
            rail,
        },
    )
    .await
    {
        Ok(ApplyOutcome::Started { attempt_id, .. }) => attempt_id,
        Ok(other) => {
            let _ = other;
            return problem(StatusCode::CONFLICT, "Conflict", "not startable");
        }
        Err(storage::ApplyError::LiveAttemptExists) => {
            if let Some(url) = view.session_url {
                return start_json(&url);
            }
            return problem(StatusCode::CONFLICT, "Conflict", "not startable");
        }
        Err(e) => return from_apply(e, false),
    };
    let success = view.success_url.clone().unwrap_or_else(|| {
        format!(
            "{}/c/{}?status=verifying",
            st.checkout_base_url.trim_end_matches('/'),
            view.public_token.as_str()
        )
    });
    let cancel = view.cancel_url.clone().unwrap_or_else(|| {
        format!(
            "{}/c/{}",
            st.checkout_base_url.trim_end_matches('/'),
            view.public_token.as_str()
        )
    });
    let session = if rail == RailId::STRIPE {
        let form = rails::stripe::checkout_form(
            &view.id.to_wire(),
            view.tenant_id.as_str(),
            i64::try_from(view.quoted.minor()).unwrap_or(i64::MAX),
            view.quoted.currency().code.as_str(),
            &success,
            &cancel,
        );
        match st.stripe.create_session(&form) {
            Ok(s) => s,
            Err(_) => {
                return problem(
                    StatusCode::SERVICE_UNAVAILABLE,
                    "Service Unavailable",
                    "Stripe rejected the org key",
                );
            }
        }
    } else if rail == RailId::CHIP {
        let cred = match storage::get_credential(&st.pool, view.tenant_id.as_str(), "chip").await {
            Ok(Some(c)) => c,
            _ => {
                return problem(
                    StatusCode::BAD_REQUEST,
                    "Bad Request",
                    "rail not configured",
                );
            }
        };
        let brand = cred.public_merchant_id.as_deref().unwrap_or("");
        if brand.is_empty() {
            return problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                "rail not configured",
            );
        }
        let mail = email.unwrap_or("");
        let name = name_from(mail, req.name.as_deref());
        let payload = rails::chip::purchase_body(
            &view.id.to_wire(),
            view.tenant_id.as_str(),
            brand,
            mail,
            &name,
            i64::try_from(view.quoted.minor()).unwrap_or(i64::MAX),
            view.quoted.currency().code.as_str(),
            &success,
            &cancel,
        );
        match st.chip.create_purchase(&payload) {
            Ok(s) => s,
            Err(_) => {
                return problem(
                    StatusCode::SERVICE_UNAVAILABLE,
                    "Service Unavailable",
                    "CHIP rejected the org key",
                );
            }
        }
    } else if rail == RailId::BILLPLZ {
        let cred = match storage::get_credential(&st.pool, view.tenant_id.as_str(), "billplz").await
        {
            Ok(Some(c)) => c,
            _ => {
                return problem(
                    StatusCode::BAD_REQUEST,
                    "Bad Request",
                    "rail not configured",
                );
            }
        };
        let collection = cred.public_merchant_id.as_deref().unwrap_or("");
        if collection.is_empty() {
            return problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                "rail not configured",
            );
        }
        let mail = email.unwrap_or("");
        let name = name_from(mail, req.name.as_deref());
        let base = st.public_base_url.trim_end_matches('/');
        let callback = format!(
            "{base}/v1/webhooks/billplz/{}?checkout_id={}",
            view.tenant_id.as_str(),
            view.id.to_wire()
        );
        let host = rails::billplz::api_host(&cred.environment);
        let payload = rails::billplz::bill_body(
            &view.id.to_wire(),
            collection,
            mail,
            &name,
            i64::try_from(view.quoted.minor()).unwrap_or(i64::MAX),
            &callback,
            &success,
        );
        match st.billplz.create_bill(host, &payload) {
            Ok(s) => s,
            Err(_) => {
                return problem(
                    StatusCode::SERVICE_UNAVAILABLE,
                    "Service Unavailable",
                    "Billplz rejected the org key",
                );
            }
        }
    } else if rail == RailId::XENDIT {
        match storage::get_credential(&st.pool, view.tenant_id.as_str(), "xendit").await {
            Ok(Some(_)) => {}
            _ => {
                return problem(
                    StatusCode::BAD_REQUEST,
                    "Bad Request",
                    "rail not configured",
                );
            }
        }
        let mail = email.unwrap_or("");
        let payload = rails::xendit::invoice_body(
            &view.id.to_wire(),
            view.tenant_id.as_str(),
            mail,
            crate::json::money_number(view.quoted),
            view.quoted.currency().code.as_str(),
            &success,
            &cancel,
        );
        match st.xendit.create_invoice(rails::xendit::API_BASE, &payload) {
            Ok(s) => s,
            Err(_) => {
                return problem(
                    StatusCode::SERVICE_UNAVAILABLE,
                    "Service Unavailable",
                    "Xendit rejected the org key",
                );
            }
        }
    } else if rail == RailId::RAZORPAY {
        match storage::get_credential(&st.pool, view.tenant_id.as_str(), "razorpay").await {
            Ok(Some(_)) => {}
            _ => {
                return problem(
                    StatusCode::BAD_REQUEST,
                    "Bad Request",
                    "rail not configured",
                );
            }
        }
        let mail = email.unwrap_or("");
        let name = name_from(mail, req.name.as_deref());
        let payload = rails::razorpay::link_body(
            &view.id.to_wire(),
            view.tenant_id.as_str(),
            mail,
            &name,
            i64::try_from(view.quoted.minor()).unwrap_or(i64::MAX),
            view.quoted.currency().code.as_str(),
            &success,
        );
        let idem = rails::razorpay::mint_idempotency_key(&view.id.to_wire());
        match st
            .razorpay
            .create_link(rails::razorpay::API_BASE, &payload, &idem)
        {
            Ok(s) => s,
            Err(_) => {
                return problem(
                    StatusCode::SERVICE_UNAVAILABLE,
                    "Service Unavailable",
                    "Razorpay rejected the org key",
                );
            }
        }
    } else if rail == RailId::SOLANA {
        let cred = match storage::get_credential(&st.pool, view.tenant_id.as_str(), "solana").await
        {
            Ok(Some(c)) => c,
            _ => {
                return problem(
                    StatusCode::BAD_REQUEST,
                    "Bad Request",
                    "rail not configured",
                );
            }
        };
        let Some(vault) = cred
            .public_merchant_id
            .as_deref()
            .and_then(rails::solana::try_normalize)
        else {
            return problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                "rail not configured",
            );
        };
        if !rails::solana::matches_vault(&st.solana_cluster, &cred.environment) {
            return problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                "solana cluster mismatch",
            );
        }
        match rails::solana::pay_uri(&vault, view.quoted, &view.id.to_wire(), &st.solana_cluster) {
            Ok(s) => s,
            Err(_) => {
                return problem(
                    StatusCode::BAD_REQUEST,
                    "Bad Request",
                    "rail not configured",
                );
            }
        }
    } else {
        HostedSession {
            url: success,
            session_id: format!("test:{}", view.id.to_wire()),
        }
    };
    let url = session.url.clone();
    match apply(
        &st.pool,
        ApplyCmd::RecordSession {
            attempt_id: started,
            session,
        },
    )
    .await
    {
        Ok(ApplyOutcome::SessionResume { url, .. }) => start_json(&url),
        Ok(_) => start_json(&url),
        Err(e) => from_apply(e, false),
    }
}

#[derive(Default, Deserialize)]
pub struct ConfirmPayRequest {
    pub signature: Option<String>,
}

pub async fn confirm(
    State(st): State<AppState>,
    Path(token): Path<String>,
    body: Option<Json<ConfirmPayRequest>>,
) -> Response {
    let confirm_key = format!("confirm:{token}");
    if !st.limiter.try_acquire(&confirm_key) {
        return problem(
            StatusCode::TOO_MANY_REQUESTS,
            "Too Many Requests",
            "Too many confirm attempts",
        );
    }
    let view = match storage::read::payment_by_public_token(&st.pool, &token).await {
        Ok(Some(v)) => v,
        Ok(None) => {
            return problem(StatusCode::NOT_FOUND, "Not Found", "Checkout not found");
        }
        Err(e) => return from_apply(e, false),
    };
    if view.provider.as_deref() != Some(RailId::SOLANA.as_str()) {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "not a solana checkout",
        );
    }
    if view.session_url.is_none() || view.session_id.is_none() || view.attempt_id.is_none() {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "confirm a started checkout token",
        );
    }
    let signature = body
        .as_ref()
        .and_then(|b| b.signature.as_deref())
        .map(str::trim)
        .unwrap_or("");
    if signature.is_empty() || rails::solana::decode(signature).is_none() {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "signature is required",
        );
    }
    let cred = match storage::get_credential(&st.pool, view.tenant_id.as_str(), "solana").await {
        Ok(Some(c)) => c,
        _ => {
            return problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                "rail not configured",
            );
        }
    };
    let Some(vault) = cred
        .public_merchant_id
        .as_deref()
        .and_then(rails::solana::try_normalize)
    else {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "rail not configured",
        );
    };
    if !rails::solana::matches_vault(&st.solana_cluster, &cred.environment) {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "solana cluster mismatch",
        );
    }
    let paused = storage::read::charges_paused(&st.pool, view.tenant_id.as_str())
        .await
        .unwrap_or(false);
    if paused && matches!(view.status, domain::PaymentStatus::Open) {
        return problem(StatusCode::CONFLICT, "Conflict", "Org charges are paused");
    }
    let tx = match st.solana.get_transaction(signature) {
        Ok(t) => t,
        Err(rails::solana::SolanaError::Throttled) => {
            return problem(
                StatusCode::SERVICE_UNAVAILABLE,
                "Service Unavailable",
                "solana RPC throttled",
            );
        }
        Err(_) => {
            return problem(
                StatusCode::SERVICE_UNAVAILABLE,
                "Service Unavailable",
                "solana RPC rejected the method",
            );
        }
    };
    let expected = match rails::solana::try_to_atomic(view.quoted) {
        Ok(v) => v,
        Err(_) => {
            return problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                "amount is not a valid USDC amount",
            );
        }
    };
    let reference = view.session_id.clone().unwrap_or_default();
    if let Err(e) = rails::solana::validate(
        &tx,
        &vault,
        expected,
        rails::solana::mint(&st.solana_cluster),
        &reference,
        &view.id.to_wire(),
        signature,
    ) {
        return problem(StatusCode::BAD_REQUEST, "Bad Request", &e.to_string());
    }
    let inserted = match storage::insert_proof(&st.pool, "solana", signature, &tx).await {
        Ok(v) => v,
        Err(e) => return from_apply(e, false),
    };
    let _ = workers::solana_bind::once(&st.pool).await;
    if !inserted {
        return Json(json!({"duplicate": true})).into_response();
    }
    let fresh = storage::read::payment_by_id(&st.pool, view.id)
        .await
        .ok()
        .flatten();
    if let Some(v) = fresh {
        if matches!(
            v.status,
            domain::PaymentStatus::Failed | domain::PaymentStatus::Expired
        ) {
            return Json(json!({"refunded": false, "reason": "late_pay_manual"})).into_response();
        }
    }
    Json(json!({"ok": true})).into_response()
}

fn start_json(url: &str) -> Response {
    if url.starts_with("solana:") {
        Json(json!({"solana_pay_url": url})).into_response()
    } else {
        Json(json!({"redirect_url": url})).into_response()
    }
}

fn public_json(st: &AppState, view: &storage::PaymentView) -> Value {
    let solana = view.provider.as_deref() == Some(RailId::SOLANA.as_str());
    let (redirect, solana_pay) = match view.session_url.as_deref() {
        Some(u) if u.starts_with("solana:") => (Value::Null, json!(u)),
        Some(u) => (json!(u), Value::Null),
        None => (Value::Null, Value::Null),
    };
    json!({
        "token": view.public_token.as_str(),
        "amount": money_number(view.quoted),
        "currency": view.quoted.currency().code.as_str(),
        "status": buyer_status(view.status),
        "provider": view.provider.as_deref().unwrap_or("test"),
        "payer_name": view.payer_name,
        "payer_email": view.payer_email,
        "started": view.session_url.is_some(),
        "redirect_url": redirect,
        "solana_pay_url": solana_pay,
        "solana_cluster": if solana { json!(st.solana_cluster) } else { Value::Null },
        "email_required": view
            .provider
            .as_deref()
            .and_then(|s| RailId::parse(s).ok())
            .map(|r| r.caps().requires_email)
            .unwrap_or(false),
    })
}

const PLACEHOLDER_EMAIL: &str = "customer@example.com";

fn email_usable(email: Option<&str>) -> bool {
    match email {
        Some(s) if !s.is_empty() => !s.eq_ignore_ascii_case(PLACEHOLDER_EMAIL),
        _ => false,
    }
}

fn name_from(email: &str, name: Option<&str>) -> String {
    if let Some(n) = name.map(str::trim).filter(|s| !s.is_empty()) {
        return n.to_string();
    }
    email
        .split('@')
        .next()
        .filter(|s| !s.is_empty())
        .unwrap_or("Customer")
        .to_string()
}
