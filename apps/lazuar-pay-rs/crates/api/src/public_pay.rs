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
        Ok(Some(view)) => Json(public_json(&view)).into_response(),
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
    let _body = body.map(|j| j.0).unwrap_or_default();
    let view = match storage::read::payment_by_public_token(&st.pool, &token).await {
        Ok(Some(v)) => v,
        Ok(None) => return problem(StatusCode::NOT_FOUND, "Not Found", "checkout not found"),
        Err(e) => return from_apply(e, false),
    };
    if let Some(url) = view.session_url.clone() {
        return Json(json!({"redirect_url": url})).into_response();
    }
    let started = match apply(
        &st.pool,
        ApplyCmd::StartAttempt {
            payment_id: view.id,
            rail: RailId::TEST,
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
                return Json(json!({"redirect_url": url})).into_response();
            }
            return problem(StatusCode::CONFLICT, "Conflict", "not startable");
        }
        Err(e) => return from_apply(e, false),
    };
    let session_id = format!("test:{}", view.id.to_wire());
    // C# TestHosted → CheckoutUrls.Success.
    let url = view.success_url.clone().unwrap_or_else(|| {
        format!(
            "{}/c/{}?status=verifying",
            st.checkout_base_url.trim_end_matches('/'),
            view.public_token.as_str()
        )
    });
    match apply(
        &st.pool,
        ApplyCmd::RecordSession {
            attempt_id: started,
            session: HostedSession {
                url: url.clone(),
                session_id,
            },
        },
    )
    .await
    {
        Ok(ApplyOutcome::SessionResume { url, .. }) => {
            Json(json!({"redirect_url": url})).into_response()
        }
        Ok(_) => Json(json!({"redirect_url": url})).into_response(),
        Err(e) => from_apply(e, false),
    }
}

pub async fn confirm(State(st): State<AppState>, Path(token): Path<String>) -> Response {
    let confirm_key = format!("confirm:{token}");
    if !st.limiter.try_acquire(&confirm_key) {
        return problem(
            StatusCode::TOO_MANY_REQUESTS,
            "Too Many Requests",
            "Too many confirm attempts",
        );
    }
    match storage::read::payment_by_public_token(&st.pool, &token).await {
        Ok(None) => problem(StatusCode::NOT_FOUND, "Not Found", "Checkout not found"),
        Ok(Some(_)) => problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "not a solana checkout",
        ),
        Err(e) => from_apply(e, false),
    }
}

fn public_json(view: &storage::PaymentView) -> Value {
    json!({
        "token": view.public_token.as_str(),
        "amount": money_number(view.quoted),
        "currency": view.quoted.currency().code.as_str(),
        "status": buyer_status(view.status),
        "provider": view.provider.as_deref().unwrap_or("test"),
        "payer_name": view.payer_name,
        "payer_email": view.payer_email,
        "started": view.session_url.is_some(),
        "redirect_url": view.session_url,
        "email_required": false,
    })
}
