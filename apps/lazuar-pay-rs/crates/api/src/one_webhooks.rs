use axum::body::Bytes;
use axum::extract::{Path, State};
use axum::http::{HeaderMap, StatusCode};
use axum::response::{IntoResponse, Response};
use axum::Json;
use serde::Deserialize;
use serde_json::{json, Value};
use time::OffsetDateTime;
use workers::hmac::verify_v1;
use workers::secret_box::SecretBox;

use crate::errors::{from_apply, problem};
use crate::identity::{require_member, require_writer, Bearer};
use crate::AppState;

#[derive(Deserialize)]
pub struct PutOneWebhook {
    pub webhook_secret: Option<String>,
}

pub async fn inbound(State(st): State<AppState>, headers: HeaderMap, body: Bytes) -> Response {
    let sig = headers
        .get("X-Lazuar-Signature")
        .and_then(|v| v.to_str().ok())
        .unwrap_or("");
    let ts = headers
        .get("X-Lazuar-Timestamp")
        .and_then(|v| v.to_str().ok());
    let now = OffsetDateTime::now_utc().unix_timestamp();
    let Some(secret) = resolve_secret(&st, &body).await else {
        return problem(StatusCode::UNAUTHORIZED, "Unauthorized", "Invalid HMAC");
    };
    if !verify_v1(&secret, &body, sig, ts, now) {
        return problem(StatusCode::UNAUTHORIZED, "Unauthorized", "Invalid HMAC");
    }
    if body.is_empty() {
        return problem(StatusCode::BAD_REQUEST, "Bad Request", "invalid event");
    }
    let Ok(doc) = serde_json::from_slice::<Value>(&body) else {
        return problem(StatusCode::BAD_REQUEST, "Bad Request", "invalid event");
    };
    let event_type = doc
        .get("type")
        .and_then(Value::as_str)
        .unwrap_or("unknown")
        .to_string();
    let body_id = doc.get("id").and_then(Value::as_str).map(str::trim);
    let header_id = headers
        .get("X-Lazuar-Event-Id")
        .and_then(|v| v.to_str().ok())
        .map(str::trim)
        .filter(|s| !s.is_empty());
    let delivery = match body_id.filter(|s| !s.is_empty()) {
        Some(id) => id.to_string(),
        None => match header_id {
            Some(id) => id.to_string(),
            None => {
                return problem(StatusCode::BAD_REQUEST, "Bad Request", "event id required");
            }
        },
    };
    let org_id = peek_org_id(&doc);
    match storage::org_settings::insert_one_event(&st.pool, &delivery, &event_type).await {
        Ok(false) => return Json(json!({"duplicate": true})).into_response(),
        Ok(true) => {}
        Err(e) => return from_apply(e, false),
    }
    if event_type == "tenant.suspended" {
        if let Some(org) = org_id.as_deref() {
            if let Err(e) = storage::org_settings::set_charges_paused(&st.pool, org, true).await {
                return from_apply(e, false);
            }
            st.whoami_cache.invalidate_org(org);
        }
    }
    if event_type == "tenant.reactivated" {
        if let Some(org) = org_id.as_deref() {
            if let Err(e) = storage::org_settings::set_charges_paused(&st.pool, org, false).await {
                return from_apply(e, false);
            }
        }
    }
    if event_type == "api_key.revoked" {
        if let Some(key_id) = peek_key_id(&doc) {
            st.whoami_cache.invalidate_key(&key_id);
        }
    }
    Json(json!({"ok": true})).into_response()
}

pub async fn put(
    State(st): State<AppState>,
    Bearer(who): Bearer,
    Path(org_id): Path<String>,
    Json(body): Json<PutOneWebhook>,
) -> Response {
    if let Err(r) = require_writer(&who, &org_id) {
        return r;
    }
    let secret = body
        .webhook_secret
        .as_deref()
        .map(str::trim)
        .filter(|s| !s.is_empty());
    let Some(secret) = secret else {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "webhook_secret is required",
        );
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
    let hex = hex::encode(&ct);
    match storage::org_settings::set_one_webhook_ciphertext(&st.pool, &org_id, &hex).await {
        Ok(()) => {
            let _ = storage::org_settings::audit(
                &st.pool,
                &org_id,
                "one.webhook_secret.upsert",
                &who.user_id,
                json!(null),
            )
            .await;
            let configured = storage::org_settings::get_one_webhook_ciphertext(&st.pool, &org_id)
                .await
                .ok()
                .flatten()
                .is_some();
            Json(json!({
                "org_id": org_id,
                "webhook_configured": configured,
            }))
            .into_response()
        }
        Err(e) => from_apply(e, false),
    }
}

pub async fn get(
    State(st): State<AppState>,
    Bearer(who): Bearer,
    Path(org_id): Path<String>,
) -> Response {
    if let Err(r) = require_member(&who, &org_id) {
        return r;
    }
    match storage::org_settings::get_one_webhook_ciphertext(&st.pool, &org_id).await {
        Ok(ct) => Json(json!({
            "org_id": org_id,
            "webhook_configured": ct.is_some(),
        }))
        .into_response(),
        Err(e) => from_apply(e, false),
    }
}

async fn resolve_secret(st: &AppState, body: &[u8]) -> Option<String> {
    if !st.one_webhook_secret.is_empty() {
        return Some(st.one_webhook_secret.clone());
    }
    let doc: Value = serde_json::from_slice(body).ok()?;
    let org = peek_org_id(&doc)?;
    let hex = storage::org_settings::get_one_webhook_ciphertext(&st.pool, &org)
        .await
        .ok()
        .flatten()?;
    let raw = hex::decode(&hex).ok()?;
    let box_ = SecretBox::new(st.wrap_key);
    box_.unprotect_str(&raw).ok()
}

fn peek_org_id(v: &Value) -> Option<String> {
    if let Some(s) = v.get("org_id").and_then(Value::as_str) {
        let t = s.trim();
        if !t.is_empty() {
            return Some(t.to_string());
        }
    }
    if let Some(s) = v.get("tenant_id").and_then(Value::as_str) {
        let t = s.trim();
        if !t.is_empty() {
            return Some(t.to_string());
        }
    }
    if let Some(data) = v.get("data") {
        return peek_org_id(data);
    }
    None
}

fn peek_key_id(v: &Value) -> Option<String> {
    if let Some(s) = v.get("key_id").and_then(Value::as_str) {
        let t = s.trim();
        if !t.is_empty() {
            return Some(t.to_string());
        }
    }
    if let Some(data) = v.get("data") {
        return peek_key_id(data);
    }
    None
}
