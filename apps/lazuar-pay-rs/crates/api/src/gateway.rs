use axum::extract::{Path, Query, State};
use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use axum::Json;
use serde::Deserialize;
use serde_json::{json, Value};

use crate::errors::problem;
use crate::identity::{require_member, require_writer, Bearer};
use crate::AppState;
use workers::secret_box::SecretBox;

#[derive(Deserialize)]
pub struct PutGateway {
    pub provider: String,
    pub secret: Option<String>,
    pub webhook_secret: Option<String>,
    pub public_merchant_id: Option<String>,
    pub environment: Option<String>,
}

#[derive(Deserialize)]
pub struct GatewayQuery {
    pub provider: Option<String>,
}

pub async fn put(
    State(st): State<AppState>,
    Bearer(who): Bearer,
    Path(org_id): Path<String>,
    Json(body): Json<PutGateway>,
) -> Response {
    if let Err(r) = require_writer(&who, &org_id) {
        return r;
    }
    let provider = body.provider.trim().to_ascii_lowercase();
    if provider == "test" {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "test processor does not take secrets",
        );
    }
    if provider != "stripe" {
        if domain::rail::RailId::parse(&provider).is_err() {
            return problem(StatusCode::BAD_REQUEST, "Bad Request", "unknown provider");
        }
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "rail not configured",
        );
    }
    if body
        .public_merchant_id
        .as_deref()
        .is_some_and(|s| !s.trim().is_empty())
    {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "public_merchant_id is not used for this provider",
        );
    }
    let secret = body.secret.as_deref().map(str::trim).unwrap_or("");
    let whsec = body.webhook_secret.as_deref().map(str::trim).unwrap_or("");
    if secret.is_empty() {
        return problem(StatusCode::BAD_REQUEST, "Bad Request", "secret is required");
    }
    if whsec.is_empty() {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "webhook_secret is required",
        );
    }
    let env_in = body
        .environment
        .as_deref()
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .map(|s| s.to_ascii_lowercase());
    if let Some(e) = env_in.as_deref() {
        if e != "test" && e != "live" {
            return problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                "environment must be test or live",
            );
        }
    }
    let last4 = if secret.len() >= 4 {
        &secret[secret.len() - 4..]
    } else {
        secret
    };
    let box_ = SecretBox::new(st.wrap_key);
    let ct = match box_.protect_str(secret) {
        Ok(v) => v,
        Err(_) => {
            return problem(
                StatusCode::SERVICE_UNAVAILABLE,
                "Service Unavailable",
                "WrapKey",
            );
        }
    };
    let wh = match box_.protect_str(whsec) {
        Ok(v) => v,
        Err(_) => {
            return problem(
                StatusCode::SERVICE_UNAVAILABLE,
                "Service Unavailable",
                "WrapKey",
            );
        }
    };
    match storage::upsert_stripe(&st.pool, &org_id, &ct, &wh, last4, env_in.as_deref()).await {
        Ok(row) => {
            let _ = storage::audit_gateway(
                &st.pool,
                &org_id,
                &who.user_id,
                "stripe",
                last4,
                &row.environment,
                true,
            )
            .await;
            Json(gateway_json(&org_id, &row)).into_response()
        }
        Err(_) => problem(
            StatusCode::INTERNAL_SERVER_ERROR,
            "Internal Server Error",
            "vault",
        ),
    }
}

pub async fn get(
    State(st): State<AppState>,
    Bearer(who): Bearer,
    Path(org_id): Path<String>,
    Query(q): Query<GatewayQuery>,
) -> Response {
    if let Err(r) = require_member(&who, &org_id) {
        return r;
    }
    let Some(provider) = q
        .provider
        .as_deref()
        .map(str::trim)
        .filter(|s| !s.is_empty())
    else {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "provider is required",
        );
    };
    let provider = provider.to_ascii_lowercase();
    if provider == "test" {
        if st.env.allows_test() {
            return Json(json!({
                "org_id": org_id,
                "provider": "test",
                "configured": true,
                "environment": "test",
                "webhook_configured": true,
                "currency": "MYR",
                "capability": "hosted_link",
            }))
            .into_response();
        }
        return Json(json!({"org_id": org_id, "provider": "test", "configured": false}))
            .into_response();
    }
    if provider != "stripe" {
        return Json(json!({"org_id": org_id, "provider": provider, "configured": false}))
            .into_response();
    }
    match storage::get_credential(&st.pool, &org_id, "stripe").await {
        Ok(None) => Json(json!({"org_id": org_id, "provider": "stripe", "configured": false}))
            .into_response(),
        Ok(Some(row)) => Json(gateway_json(&org_id, &row)).into_response(),
        Err(_) => problem(
            StatusCode::INTERNAL_SERVER_ERROR,
            "Internal Server Error",
            "vault",
        ),
    }
}

pub async fn list(
    State(st): State<AppState>,
    Bearer(who): Bearer,
    Path(org_id): Path<String>,
) -> Response {
    if let Err(r) = require_member(&who, &org_id) {
        return r;
    }
    let stripe = storage::get_credential(&st.pool, &org_id, "stripe")
        .await
        .ok()
        .flatten();
    let mut processors = vec![];
    if st.env.allows_test() {
        processors.push(json!({
            "org_id": org_id,
            "provider": "test",
            "configured": true,
            "environment": "test",
            "webhook_configured": true,
            "currency": "MYR",
            "capability": "hosted_link",
        }));
    }
    match stripe {
        Some(row) => processors.push(gateway_json(&org_id, &row)),
        None => processors.push(json!({
            "org_id": org_id,
            "provider": "stripe",
            "configured": false,
            "currency": "MYR",
            "capability": "hosted_link",
        })),
    }
    Json(json!({"org_id": org_id, "processors": processors})).into_response()
}

fn gateway_json(org_id: &str, row: &storage::CredentialRow) -> Value {
    json!({
        "org_id": org_id,
        "provider": row.rail,
        "configured": true,
        "last4": row.last4,
        "environment": row.environment,
        "webhook_configured": row.webhook_ciphertext.as_ref().is_some_and(|c| !c.is_empty()),
        "currency": "MYR",
        "capability": "hosted_link",
    })
}
