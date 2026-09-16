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
    pub key_id: Option<String>,
    pub key_secret: Option<String>,
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
    if provider == "solana" {
        return put_solana(&st, &who.user_id, &org_id, &body).await;
    }
    if provider != "stripe"
        && provider != "chip"
        && provider != "billplz"
        && provider != "xendit"
        && provider != "razorpay"
    {
        if domain::rail::RailId::parse(&provider).is_err() {
            return problem(StatusCode::BAD_REQUEST, "Bad Request", "unknown provider");
        }
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "rail not configured",
        );
    }
    let brand = body
        .public_merchant_id
        .as_deref()
        .map(str::trim)
        .filter(|s| !s.is_empty());
    if (provider == "stripe" || provider == "xendit" || provider == "razorpay") && brand.is_some() {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "public_merchant_id is not used for this provider",
        );
    }
    if (provider == "chip" || provider == "billplz") && brand.is_none() {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "public_merchant_id is required",
        );
    }
    let mut secret = body
        .secret
        .as_deref()
        .map(str::trim)
        .unwrap_or("")
        .to_string();
    if secret.is_empty() {
        let kid = body.key_id.as_deref().map(str::trim).unwrap_or("");
        let ksec = body.key_secret.as_deref().map(str::trim).unwrap_or("");
        if !kid.is_empty() && !ksec.is_empty() {
            secret = format!("{kid}:{ksec}");
        }
    }
    let whsec = body.webhook_secret.as_deref().map(str::trim).unwrap_or("");
    if secret.is_empty() {
        return problem(StatusCode::BAD_REQUEST, "Bad Request", "secret is required");
    }
    if provider == "razorpay" && rails::razorpay::try_split(&secret).is_none() {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "secret must be key_id:key_secret",
        );
    }
    if whsec.is_empty() {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "webhook_secret is required",
        );
    }
    if provider == "chip" && !rails::chip::pem_ok(whsec) {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "webhook_secret must be a CHIP PEM",
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
    if provider == "billplz" && env_in.is_none() {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "environment is required",
        );
    }
    let last4_owned = if provider == "razorpay" {
        rails::razorpay::try_split(&secret)
            .map(|(id, _)| {
                if id.len() >= 4 {
                    id[id.len() - 4..].to_string()
                } else {
                    id.to_string()
                }
            })
            .unwrap_or_default()
    } else if secret.len() >= 4 {
        secret[secret.len() - 4..].to_string()
    } else {
        secret.clone()
    };
    let last4 = last4_owned.as_str();
    let box_ = SecretBox::new(st.wrap_key);
    let ct = match box_.protect_str(&secret) {
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
    let saved = if provider == "chip" {
        storage::upsert_chip(
            &st.pool,
            &org_id,
            &ct,
            &wh,
            last4,
            env_in.as_deref(),
            brand.unwrap_or(""),
        )
        .await
    } else if provider == "billplz" {
        storage::upsert_billplz(
            &st.pool,
            &org_id,
            &ct,
            &wh,
            last4,
            env_in.as_deref(),
            brand.unwrap_or(""),
        )
        .await
    } else if provider == "xendit" {
        storage::upsert_xendit(&st.pool, &org_id, &ct, &wh, last4, env_in.as_deref()).await
    } else if provider == "razorpay" {
        storage::upsert_razorpay(&st.pool, &org_id, &ct, &wh, last4, env_in.as_deref()).await
    } else {
        storage::upsert_stripe(&st.pool, &org_id, &ct, &wh, last4, env_in.as_deref()).await
    };
    match saved {
        Ok(row) => {
            let _ = storage::audit_gateway(
                &st.pool,
                &org_id,
                &who.user_id,
                &provider,
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
    if provider != "stripe"
        && provider != "chip"
        && provider != "billplz"
        && provider != "xendit"
        && provider != "razorpay"
        && provider != "solana"
    {
        return Json(json!({"org_id": org_id, "provider": provider, "configured": false}))
            .into_response();
    }
    match storage::get_credential(&st.pool, &org_id, &provider).await {
        Ok(None) => Json(json!({"org_id": org_id, "provider": provider, "configured": false}))
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
    for rail in ["stripe", "chip", "billplz", "xendit", "razorpay", "solana"] {
        match storage::get_credential(&st.pool, &org_id, rail).await {
            Ok(Some(row)) => processors.push(gateway_json(&org_id, &row)),
            _ => processors.push(json!({
                "org_id": org_id,
                "provider": rail,
                "configured": false,
                "currency": rail_currency(rail),
                "capability": "hosted_link",
            })),
        }
    }
    Json(json!({"org_id": org_id, "processors": processors})).into_response()
}

fn gateway_json(org_id: &str, row: &storage::CredentialRow) -> Value {
    let configured = if row.rail == "solana" {
        row.public_merchant_id
            .as_deref()
            .and_then(rails::solana::try_normalize)
            .is_some()
    } else {
        true
    };
    json!({
        "org_id": org_id,
        "provider": row.rail,
        "configured": configured,
        "last4": row.last4,
        "environment": row.environment,
        "webhook_configured": row.webhook_ciphertext.as_ref().is_some_and(|c| !c.is_empty()),
        "public_merchant_id": row.public_merchant_id,
        "currency": rail_currency(&row.rail),
        "capability": "hosted_link",
    })
}

fn rail_currency(rail: &str) -> &'static str {
    match rail {
        "razorpay" => "INR",
        "solana" => "USDC",
        _ => "MYR",
    }
}

async fn put_solana(st: &AppState, actor: &str, org_id: &str, body: &PutGateway) -> Response {
    let secret = body.secret.as_deref().map(str::trim).unwrap_or("");
    let kid = body.key_id.as_deref().map(str::trim).unwrap_or("");
    let ksec = body.key_secret.as_deref().map(str::trim).unwrap_or("");
    if !secret.is_empty() || !kid.is_empty() || !ksec.is_empty() {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "solana does not take an API secret",
        );
    }
    if body
        .webhook_secret
        .as_deref()
        .map(str::trim)
        .is_some_and(|s| !s.is_empty())
    {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "solana does not take a webhook secret",
        );
    }
    let Some(address) = body
        .public_merchant_id
        .as_deref()
        .and_then(rails::solana::try_normalize)
    else {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "public_merchant_id must be a Solana wallet address",
        );
    };
    let Some(env) = body
        .environment
        .as_deref()
        .and_then(rails::solana::normalize_vault_env)
    else {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "environment must be devnet or mainnet",
        );
    };
    if !rails::solana::matches_vault(&st.solana_cluster, &env) {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "solana cluster mismatch",
        );
    }
    let last4 = rails::solana::last4(&address).to_string();
    match storage::upsert_solana(&st.pool, org_id, &last4, &env, &address).await {
        Ok(row) => {
            let _ = storage::audit_gateway(
                &st.pool,
                org_id,
                actor,
                "solana",
                &last4,
                &row.environment,
                false,
            )
            .await;
            Json(gateway_json(org_id, &row)).into_response()
        }
        Err(_) => problem(
            StatusCode::INTERNAL_SERVER_ERROR,
            "Internal Server Error",
            "vault",
        ),
    }
}
