use axum::extract::{Path, State};
use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use axum::Json;
use serde::Deserialize;
use serde_json::json;
use uuid::Uuid;
use workers::outbound_url::validate_outbound_url;
use workers::secret_box::SecretBox;

use crate::errors::{from_apply, problem};
use crate::identity::{require_member, require_writer, Bearer};
use crate::AppState;

#[derive(Deserialize)]
pub struct PutOrgWebhook {
    pub url: Option<String>,
}

pub async fn put(
    State(st): State<AppState>,
    Bearer(who): Bearer,
    Path(org_id): Path<String>,
    Json(body): Json<PutOrgWebhook>,
) -> Response {
    if let Err(r) = require_writer(&who, &org_id) {
        return r;
    }
    let raw = body.url.as_deref().unwrap_or("");
    let url = match validate_outbound_url(raw, st.env.allows_test()) {
        Ok(u) => u,
        Err(d) => return problem(StatusCode::BAD_REQUEST, "Bad Request", d),
    };
    let secret = mint_secret();
    let prefix = prefix_of(&secret);
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
    match storage::org_settings::upsert_org_webhook(&st.pool, &org_id, &url, &ct, prefix).await {
        Ok(row) => {
            let _ = storage::org_settings::audit(
                &st.pool,
                &org_id,
                "org.webhook.upsert",
                &who.user_id,
                json!({"url": url, "secret_prefix": prefix}),
            )
            .await;
            Json(created_view(&row, Some(&secret))).into_response()
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
    match storage::org_settings::get_org_webhook(&st.pool, &org_id).await {
        Ok(Some(row)) => Json(get_view(&row)).into_response(),
        Ok(None) => Json(json!({
            "org_id": org_id,
            "url": null,
            "webhook_configured": false,
            "secret_prefix": null,
        }))
        .into_response(),
        Err(e) => from_apply(e, false),
    }
}

pub async fn rotate(
    State(st): State<AppState>,
    Bearer(who): Bearer,
    Path(org_id): Path<String>,
) -> Response {
    if let Err(r) = require_writer(&who, &org_id) {
        return r;
    }
    let secret = mint_secret();
    let prefix = prefix_of(&secret);
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
    match storage::org_settings::rotate_org_webhook(&st.pool, &org_id, &ct, prefix).await {
        Ok(Some(row)) => {
            let _ = storage::org_settings::audit(
                &st.pool,
                &org_id,
                "org.webhook.rotated",
                &who.user_id,
                json!({"url": row.url, "secret_prefix": prefix}),
            )
            .await;
            Json(created_view(&row, Some(&secret))).into_response()
        }
        Ok(None) => problem(
            StatusCode::NOT_FOUND,
            "Not Found",
            "webhook endpoint not found",
        ),
        Err(e) => from_apply(e, false),
    }
}

pub async fn test_ping(
    State(st): State<AppState>,
    Bearer(who): Bearer,
    Path(org_id): Path<String>,
) -> Response {
    if let Err(r) = require_writer(&who, &org_id) {
        return r;
    }
    match storage::org_settings::get_org_webhook(&st.pool, &org_id).await {
        Ok(None) => {
            return problem(
                StatusCode::NOT_FOUND,
                "Not Found",
                "webhook endpoint not found",
            );
        }
        Ok(Some(_)) => {}
        Err(e) => return from_apply(e, false),
    }
    let event_id = format!("test-{}", Uuid::new_v4().simple());
    let created = time::OffsetDateTime::now_utc()
        .format(&time::format_description::well_known::Rfc3339)
        .unwrap_or_default();
    let payload = json!({
        "id": event_id,
        "type": "webhook.test",
        "created_at": created,
        "org_id": org_id,
        "api_version": "0.1.0",
        "data": { "ok": true },
    });
    match storage::enqueue_outbound_pool(&st.pool, &org_id, &event_id, "webhook.test", &payload)
        .await
    {
        Ok(()) => Json(json!({"ok": true, "event_id": event_id})).into_response(),
        Err(e) => from_apply(e, false),
    }
}

fn created_view(
    row: &storage::org_settings::OrgWebhookRow,
    secret: Option<&str>,
) -> serde_json::Value {
    json!({
        "org_id": row.tenant_id,
        "url": row.url,
        "webhook_configured": true,
        "secret_prefix": row.secret_prefix,
        "webhook_secret": secret,
    })
}

fn get_view(row: &storage::org_settings::OrgWebhookRow) -> serde_json::Value {
    json!({
        "org_id": row.tenant_id,
        "url": row.url,
        "webhook_configured": true,
        "secret_prefix": row.secret_prefix,
    })
}

fn mint_secret() -> String {
    format!("whsec_{}", hex::encode(Uuid::new_v4().as_bytes()))
}

fn prefix_of(secret: &str) -> &str {
    let n = secret.len();
    if n >= 4 {
        &secret[n - 4..]
    } else {
        secret
    }
}
