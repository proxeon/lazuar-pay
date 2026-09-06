use axum::extract::{Path, State};
use axum::http::{HeaderMap, StatusCode};
use axum::response::{IntoResponse, Response};
use axum::Json;
use domain::money::{Currency, Money};
use domain::rail::RailId;
use domain::{PaymentId, TenantId, TerminalReason};
use domain::{Proof, ProofId};
use rails::test_rail::{parse_webhook, TestParseError, SIGNATURE_HEADER};
use serde_json::json;
use storage::{apply, ApplyCmd, ApplyOutcome};
use time::OffsetDateTime;

use crate::errors::{from_apply, problem};
use crate::AppState;

pub async fn test_webhook(
    State(st): State<AppState>,
    Path(org_id): Path<String>,
    headers: HeaderMap,
    body: String,
) -> Response {
    if st.test_webhook_secret.is_empty() {
        return problem(
            StatusCode::INTERNAL_SERVER_ERROR,
            "Internal Server Error",
            "webhook secret missing",
        );
    }
    let sig = headers.get(SIGNATURE_HEADER).and_then(|v| v.to_str().ok());
    let event = match parse_webhook(body.as_bytes(), sig, &st.test_webhook_secret) {
        Ok(e) => e,
        Err(TestParseError::SecretMissing) => {
            return problem(
                StatusCode::INTERNAL_SERVER_ERROR,
                "Internal Server Error",
                "webhook secret missing",
            );
        }
        Err(e) => {
            return problem(StatusCode::BAD_REQUEST, "Bad Request", &e.to_string());
        }
    };
    let Ok(payment_id) = PaymentId::from_wire(&event.checkout_id) else {
        return problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "invalid checkout id",
        );
    };
    let view = match storage::read::payment_by_id(&st.pool, payment_id).await {
        Ok(Some(v)) => v,
        Ok(None) => return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found"),
        Err(e) => return from_apply(e, false),
    };
    if view.tenant_id.as_str() != org_id {
        return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
    }
    let Some(attempt_id) = view.attempt_id else {
        return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
    };
    let now = OffsetDateTime::now_utc();
    let cmd = if event.failed {
        ApplyCmd::InjectFailed {
            tenant_id: TenantId::new(org_id),
            rail: RailId::TEST,
            proof_id: event.event_id,
            payment_id,
            attempt_id,
            reason: TerminalReason::PspFailed,
            now,
        }
    } else {
        let ccy = event
            .currency
            .as_deref()
            .and_then(Currency::by_code)
            .unwrap_or(Currency::MYR);
        let received = match Money::from_minor(event.amount_minor.unwrap_or(0) as i128, ccy) {
            Ok(m) => m,
            Err(_) => {
                return problem(StatusCode::BAD_REQUEST, "Bad Request", "amount mismatch");
            }
        };
        ApplyCmd::InjectPaid {
            tenant_id: TenantId::new(org_id),
            rail: RailId::TEST,
            proof_id: event.event_id.clone(),
            payment_id,
            attempt_id,
            received,
            proof: Proof::PspWebhook {
                rail: RailId::TEST,
                event_id: ProofId::new(event.event_id),
            },
            now,
        }
    };
    match apply(&st.pool, cmd).await {
        Ok(ApplyOutcome::Duplicate) => Json(json!({"duplicate": true})).into_response(),
        Ok(_) => Json(json!({"ok": true})).into_response(),
        Err(e) => from_apply(e, false),
    }
}
