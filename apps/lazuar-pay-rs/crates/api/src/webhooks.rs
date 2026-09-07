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
            refs: Default::default(),
        }
    };
    match apply(&st.pool, cmd).await {
        Ok(ApplyOutcome::Duplicate) => Json(json!({"duplicate": true})).into_response(),
        Ok(_) => Json(json!({"ok": true})).into_response(),
        Err(e) => from_apply(e, false),
    }
}

pub async fn stripe_webhook(
    State(st): State<AppState>,
    Path(org_id): Path<String>,
    headers: HeaderMap,
    body: String,
) -> Response {
    let cred = match storage::get_credential(&st.pool, &org_id, "stripe").await {
        Ok(Some(c)) => c,
        Ok(None) => {
            return problem(StatusCode::BAD_REQUEST, "Bad Request", "invalid signature");
        }
        Err(e) => return from_apply(e, false),
    };
    let box_ = workers::secret_box::SecretBox::new(st.wrap_key);
    let secret = match cred.webhook_ciphertext.as_deref() {
        Some(ct) if !ct.is_empty() => match box_.unprotect_str(ct) {
            Ok(s) => s,
            Err(_) => {
                return problem(
                    StatusCode::SERVICE_UNAVAILABLE,
                    "Service Unavailable",
                    "webhook secret undecryptable",
                );
            }
        },
        _ if st.env == crate::boot::Env::Testing && !st.test_webhook_secret.is_empty() => {
            st.test_webhook_secret.clone()
        }
        _ => {
            return problem(
                StatusCode::INTERNAL_SERVER_ERROR,
                "Internal Server Error",
                "webhook secret missing",
            );
        }
    };
    let sig = headers
        .get(rails::stripe::SIGNATURE_HEADER)
        .and_then(|v| v.to_str().ok());
    let now_unix = OffsetDateTime::now_utc().unix_timestamp();
    let (event_id, outcome) =
        match rails::stripe::parse_webhook(body.as_bytes(), sig, &secret, now_unix) {
            Ok(v) => v,
            Err(e) => {
                return problem(StatusCode::BAD_REQUEST, "Bad Request", &e.to_string());
            }
        };
    use domain::proof::{Binding, WebhookOutcome};
    if let WebhookOutcome::Ignored { .. } = &outcome {
        let typ = serde_json::from_str::<serde_json::Value>(&body)
            .ok()
            .and_then(|v| v.get("type").and_then(|t| t.as_str()).map(str::to_string))
            .unwrap_or_default();
        let session = serde_json::from_str::<serde_json::Value>(&body)
            .ok()
            .and_then(|v| v.get("data").and_then(|d| d.get("object")).cloned());
        let reason = rails::stripe::ignore_detail(&outcome, &typ, session.as_ref());
        let _ =
            storage::record_ignored_inbound(&st.pool, &org_id, "stripe", &event_id, &reason).await;
        return Json(json!({"ignored": reason})).into_response();
    }
    let binding = match &outcome {
        WebhookOutcome::Paid { binding, .. } | WebhookOutcome::Failed { binding, .. } => {
            binding.clone()
        }
        WebhookOutcome::Ignored { .. } => unreachable!(),
    };
    let (payment_id, attempt_id) = match binding {
        Binding::Payment { id } => {
            let view = match storage::read::payment_by_id(&st.pool, id).await {
                Ok(Some(v)) => v,
                _ => {
                    return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
                }
            };
            if view.tenant_id.as_str() != org_id {
                return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
            }
            if view.provider.as_deref() != Some("stripe") {
                return problem(StatusCode::BAD_REQUEST, "Bad Request", "provider mismatch");
            }
            let Some(aid) = view.attempt_id else {
                return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
            };
            (id, aid)
        }
        Binding::Session { session_id } => {
            match storage::payment_by_session(&st.pool, &org_id, "stripe", &session_id).await {
                Ok(Some((pid, aid))) => (
                    domain::PaymentId::from_uuid(pid),
                    domain::AttemptId::from_uuid(aid),
                ),
                _ => {
                    return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
                }
            }
        }
    };
    let now = OffsetDateTime::now_utc();
    let cmd = match outcome {
        WebhookOutcome::Failed { .. } => ApplyCmd::InjectFailed {
            tenant_id: TenantId::new(org_id),
            rail: RailId::STRIPE,
            proof_id: event_id,
            payment_id,
            attempt_id,
            reason: TerminalReason::PspFailed,
            now,
        },
        WebhookOutcome::Paid { received, refs, .. } => ApplyCmd::InjectPaid {
            tenant_id: TenantId::new(org_id),
            rail: RailId::STRIPE,
            proof_id: event_id.clone(),
            payment_id,
            attempt_id,
            received,
            proof: Proof::PspWebhook {
                rail: RailId::STRIPE,
                event_id: ProofId::new(event_id),
            },
            now,
            refs,
        },
        WebhookOutcome::Ignored { .. } => unreachable!(),
    };
    match apply(&st.pool, cmd).await {
        Ok(ApplyOutcome::Duplicate) => Json(json!({"duplicate": true})).into_response(),
        Ok(_) => Json(json!({"ok": true})).into_response(),
        Err(e) => from_apply(e, false),
    }
}

pub async fn chip_webhook(
    State(st): State<AppState>,
    Path(org_id): Path<String>,
    headers: HeaderMap,
    body: String,
) -> Response {
    let cred = match storage::get_credential(&st.pool, &org_id, "chip").await {
        Ok(Some(c)) => c,
        Ok(None) => {
            return problem(StatusCode::BAD_REQUEST, "Bad Request", "invalid signature");
        }
        Err(e) => return from_apply(e, false),
    };
    let box_ = workers::secret_box::SecretBox::new(st.wrap_key);
    let pem = match cred.webhook_ciphertext.as_deref() {
        Some(ct) if !ct.is_empty() => match box_.unprotect_str(ct) {
            Ok(s) => s,
            Err(_) => {
                return problem(
                    StatusCode::SERVICE_UNAVAILABLE,
                    "Service Unavailable",
                    "webhook secret undecryptable",
                );
            }
        },
        _ => {
            return problem(
                StatusCode::INTERNAL_SERVER_ERROR,
                "Internal Server Error",
                "webhook secret missing",
            );
        }
    };
    let sig = headers
        .iter()
        .find(|(k, _)| {
            k.as_str()
                .eq_ignore_ascii_case(rails::chip::SIGNATURE_HEADER)
        })
        .and_then(|(_, v)| v.to_str().ok());
    let (event_id, outcome) = match rails::chip::parse_webhook(body.as_bytes(), sig, &pem) {
        Ok(v) => v,
        Err(e) => {
            return problem(StatusCode::BAD_REQUEST, "Bad Request", &e.to_string());
        }
    };
    use domain::proof::{Binding, WebhookOutcome};
    if let WebhookOutcome::Ignored { .. } = &outcome {
        let typ = serde_json::from_str::<serde_json::Value>(&body)
            .ok()
            .and_then(|v| {
                v.get("event_type")
                    .and_then(|t| t.as_str())
                    .map(str::to_string)
            })
            .unwrap_or_default();
        let reason = rails::chip::ignore_detail(&outcome, &typ);
        let _ =
            storage::record_ignored_inbound(&st.pool, &org_id, "chip", &event_id, &reason).await;
        return Json(json!({"ignored": reason})).into_response();
    }
    let binding = match &outcome {
        WebhookOutcome::Paid { binding, .. } | WebhookOutcome::Failed { binding, .. } => {
            binding.clone()
        }
        WebhookOutcome::Ignored { .. } => unreachable!(),
    };
    let (payment_id, attempt_id) = match binding {
        Binding::Payment { id } => {
            let view = match storage::read::payment_by_id(&st.pool, id).await {
                Ok(Some(v)) => v,
                _ => {
                    return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
                }
            };
            if view.tenant_id.as_str() != org_id {
                return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
            }
            if view.provider.as_deref() != Some("chip") {
                return problem(StatusCode::BAD_REQUEST, "Bad Request", "provider mismatch");
            }
            let Some(aid) = view.attempt_id else {
                return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
            };
            (id, aid)
        }
        Binding::Session { session_id } => {
            match storage::payment_by_session(&st.pool, &org_id, "chip", &session_id).await {
                Ok(Some((pid, aid))) => (
                    domain::PaymentId::from_uuid(pid),
                    domain::AttemptId::from_uuid(aid),
                ),
                _ => {
                    return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
                }
            }
        }
    };
    let now = OffsetDateTime::now_utc();
    let cmd = match outcome {
        WebhookOutcome::Failed { .. } => ApplyCmd::InjectFailed {
            tenant_id: TenantId::new(org_id),
            rail: RailId::CHIP,
            proof_id: event_id,
            payment_id,
            attempt_id,
            reason: TerminalReason::PspFailed,
            now,
        },
        WebhookOutcome::Paid { received, refs, .. } => ApplyCmd::InjectPaid {
            tenant_id: TenantId::new(org_id),
            rail: RailId::CHIP,
            proof_id: event_id.clone(),
            payment_id,
            attempt_id,
            received,
            proof: Proof::PspWebhook {
                rail: RailId::CHIP,
                event_id: ProofId::new(event_id),
            },
            now,
            refs,
        },
        WebhookOutcome::Ignored { .. } => unreachable!(),
    };
    match apply(&st.pool, cmd).await {
        Ok(ApplyOutcome::Duplicate) => Json(json!({"duplicate": true})).into_response(),
        Ok(_) => Json(json!({"ok": true})).into_response(),
        Err(e) => from_apply(e, false),
    }
}

pub async fn billplz_webhook(
    State(st): State<AppState>,
    Path(org_id): Path<String>,
    body: String,
) -> Response {
    let cred = match storage::get_credential(&st.pool, &org_id, "billplz").await {
        Ok(Some(c)) => c,
        Ok(None) => {
            return problem(StatusCode::BAD_REQUEST, "Bad Request", "invalid signature");
        }
        Err(e) => return from_apply(e, false),
    };
    let box_ = workers::secret_box::SecretBox::new(st.wrap_key);
    let secret = match cred.webhook_ciphertext.as_deref() {
        Some(ct) if !ct.is_empty() => match box_.unprotect_str(ct) {
            Ok(s) => s,
            Err(_) => {
                return problem(
                    StatusCode::SERVICE_UNAVAILABLE,
                    "Service Unavailable",
                    "webhook secret undecryptable",
                );
            }
        },
        _ => {
            return problem(
                StatusCode::INTERNAL_SERVER_ERROR,
                "Internal Server Error",
                "webhook secret missing",
            );
        }
    };
    let (event_id, outcome) = match rails::billplz::parse_webhook(body.as_bytes(), &secret) {
        Ok(v) => v,
        Err(e) => {
            return problem(StatusCode::BAD_REQUEST, "Bad Request", &e.to_string());
        }
    };
    use domain::proof::{Binding, WebhookOutcome};
    if let WebhookOutcome::Ignored { .. } = &outcome {
        let reason = rails::billplz::ignore_detail(&outcome);
        let _ =
            storage::record_ignored_inbound(&st.pool, &org_id, "billplz", &event_id, &reason).await;
        return Json(json!({"ignored": reason})).into_response();
    }
    let binding = match &outcome {
        WebhookOutcome::Paid { binding, .. } | WebhookOutcome::Failed { binding, .. } => {
            binding.clone()
        }
        WebhookOutcome::Ignored { .. } => unreachable!(),
    };
    let (payment_id, attempt_id) = match binding {
        Binding::Payment { id } => {
            let view = match storage::read::payment_by_id(&st.pool, id).await {
                Ok(Some(v)) => v,
                _ => {
                    return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
                }
            };
            if view.tenant_id.as_str() != org_id {
                return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
            }
            if view.provider.as_deref() != Some("billplz") {
                return problem(StatusCode::BAD_REQUEST, "Bad Request", "provider mismatch");
            }
            let Some(aid) = view.attempt_id else {
                return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
            };
            (id, aid)
        }
        Binding::Session { session_id } => {
            match storage::payment_by_session(&st.pool, &org_id, "billplz", &session_id).await {
                Ok(Some((pid, aid))) => (
                    domain::PaymentId::from_uuid(pid),
                    domain::AttemptId::from_uuid(aid),
                ),
                _ => {
                    return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
                }
            }
        }
    };
    let now = OffsetDateTime::now_utc();
    let cmd = match outcome {
        WebhookOutcome::Failed { .. } => ApplyCmd::InjectFailed {
            tenant_id: TenantId::new(org_id),
            rail: RailId::BILLPLZ,
            proof_id: event_id,
            payment_id,
            attempt_id,
            reason: TerminalReason::PspFailed,
            now,
        },
        WebhookOutcome::Paid { received, refs, .. } => ApplyCmd::InjectPaid {
            tenant_id: TenantId::new(org_id),
            rail: RailId::BILLPLZ,
            proof_id: event_id.clone(),
            payment_id,
            attempt_id,
            received,
            proof: Proof::PspWebhook {
                rail: RailId::BILLPLZ,
                event_id: ProofId::new(event_id),
            },
            now,
            refs,
        },
        WebhookOutcome::Ignored { .. } => unreachable!(),
    };
    match apply(&st.pool, cmd).await {
        Ok(ApplyOutcome::Duplicate) => Json(json!({"duplicate": true})).into_response(),
        Ok(_) => Json(json!({"ok": true})).into_response(),
        Err(e) => from_apply(e, false),
    }
}

pub async fn xendit_webhook(
    State(st): State<AppState>,
    Path(org_id): Path<String>,
    headers: HeaderMap,
    body: String,
) -> Response {
    let cred = match storage::get_credential(&st.pool, &org_id, "xendit").await {
        Ok(Some(c)) => c,
        Ok(None) => {
            return problem(StatusCode::BAD_REQUEST, "Bad Request", "invalid signature");
        }
        Err(e) => return from_apply(e, false),
    };
    let box_ = workers::secret_box::SecretBox::new(st.wrap_key);
    let secret = match cred.webhook_ciphertext.as_deref() {
        Some(ct) if !ct.is_empty() => match box_.unprotect_str(ct) {
            Ok(s) => s,
            Err(_) => {
                return problem(
                    StatusCode::SERVICE_UNAVAILABLE,
                    "Service Unavailable",
                    "webhook secret undecryptable",
                );
            }
        },
        _ => {
            return problem(
                StatusCode::INTERNAL_SERVER_ERROR,
                "Internal Server Error",
                "webhook secret missing",
            );
        }
    };
    let token = headers.iter().find(|(k, _)| {
        k.as_str()
            .eq_ignore_ascii_case(rails::xendit::SIGNATURE_HEADER)
    });
    let token = token.and_then(|(_, v)| v.to_str().ok());
    let (event_id, outcome) = match rails::xendit::parse_webhook(body.as_bytes(), token, &secret) {
        Ok(v) => v,
        Err(e) => {
            return problem(StatusCode::BAD_REQUEST, "Bad Request", &e.to_string());
        }
    };
    use domain::proof::{Binding, WebhookOutcome};
    if let WebhookOutcome::Ignored { .. } = &outcome {
        let reason = rails::xendit::ignore_detail(&event_id);
        let _ =
            storage::record_ignored_inbound(&st.pool, &org_id, "xendit", &event_id, &reason).await;
        return Json(json!({"ignored": reason})).into_response();
    }
    let binding = match &outcome {
        WebhookOutcome::Paid { binding, .. } | WebhookOutcome::Failed { binding, .. } => {
            binding.clone()
        }
        WebhookOutcome::Ignored { .. } => unreachable!(),
    };
    let (payment_id, attempt_id) = match binding {
        Binding::Payment { id } => {
            let view = match storage::read::payment_by_id(&st.pool, id).await {
                Ok(Some(v)) => v,
                _ => {
                    return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
                }
            };
            if view.tenant_id.as_str() != org_id {
                return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
            }
            if view.provider.as_deref() != Some("xendit") {
                return problem(StatusCode::BAD_REQUEST, "Bad Request", "provider mismatch");
            }
            let Some(aid) = view.attempt_id else {
                return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
            };
            (id, aid)
        }
        Binding::Session { session_id } => {
            match storage::payment_by_session(&st.pool, &org_id, "xendit", &session_id).await {
                Ok(Some((pid, aid))) => (
                    domain::PaymentId::from_uuid(pid),
                    domain::AttemptId::from_uuid(aid),
                ),
                _ => {
                    return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
                }
            }
        }
    };
    let now = OffsetDateTime::now_utc();
    let cmd = match outcome {
        WebhookOutcome::Failed { .. } => ApplyCmd::InjectFailed {
            tenant_id: TenantId::new(org_id),
            rail: RailId::XENDIT,
            proof_id: event_id,
            payment_id,
            attempt_id,
            reason: TerminalReason::PspFailed,
            now,
        },
        WebhookOutcome::Paid { received, refs, .. } => ApplyCmd::InjectPaid {
            tenant_id: TenantId::new(org_id),
            rail: RailId::XENDIT,
            proof_id: event_id.clone(),
            payment_id,
            attempt_id,
            received,
            proof: Proof::PspWebhook {
                rail: RailId::XENDIT,
                event_id: ProofId::new(event_id),
            },
            now,
            refs,
        },
        WebhookOutcome::Ignored { .. } => unreachable!(),
    };
    match apply(&st.pool, cmd).await {
        Ok(ApplyOutcome::Duplicate) => Json(json!({"duplicate": true})).into_response(),
        Ok(_) => Json(json!({"ok": true})).into_response(),
        Err(e) => from_apply(e, false),
    }
}

pub async fn razorpay_webhook(
    State(st): State<AppState>,
    Path(org_id): Path<String>,
    headers: HeaderMap,
    body: String,
) -> Response {
    let cred = match storage::get_credential(&st.pool, &org_id, "razorpay").await {
        Ok(Some(c)) => c,
        Ok(None) => {
            return problem(StatusCode::BAD_REQUEST, "Bad Request", "invalid signature");
        }
        Err(e) => return from_apply(e, false),
    };
    let box_ = workers::secret_box::SecretBox::new(st.wrap_key);
    let secret = match cred.webhook_ciphertext.as_deref() {
        Some(ct) if !ct.is_empty() => match box_.unprotect_str(ct) {
            Ok(s) => s,
            Err(_) => {
                return problem(
                    StatusCode::SERVICE_UNAVAILABLE,
                    "Service Unavailable",
                    "webhook secret undecryptable",
                );
            }
        },
        _ => {
            return problem(
                StatusCode::INTERNAL_SERVER_ERROR,
                "Internal Server Error",
                "webhook secret missing",
            );
        }
    };
    let sig = headers.iter().find(|(k, _)| {
        k.as_str()
            .eq_ignore_ascii_case(rails::razorpay::SIGNATURE_HEADER)
    });
    let sig = sig.and_then(|(_, v)| v.to_str().ok());
    let (event_id, outcome) = match rails::razorpay::parse_webhook(body.as_bytes(), sig, &secret) {
        Ok(v) => v,
        Err(e) => {
            return problem(StatusCode::BAD_REQUEST, "Bad Request", &e.to_string());
        }
    };
    use domain::proof::{Binding, WebhookOutcome};
    if let WebhookOutcome::Ignored { .. } = &outcome {
        let reason = rails::razorpay::ignore_detail(&event_id);
        let _ = storage::record_ignored_inbound(&st.pool, &org_id, "razorpay", &event_id, &reason)
            .await;
        return Json(json!({"ignored": reason})).into_response();
    }
    let binding = match &outcome {
        WebhookOutcome::Paid { binding, .. } | WebhookOutcome::Failed { binding, .. } => {
            binding.clone()
        }
        WebhookOutcome::Ignored { .. } => unreachable!(),
    };
    let (payment_id, attempt_id) = match binding {
        Binding::Payment { id } => {
            let view = match storage::read::payment_by_id(&st.pool, id).await {
                Ok(Some(v)) => v,
                _ => {
                    return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
                }
            };
            if view.tenant_id.as_str() != org_id {
                return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
            }
            if view.provider.as_deref() != Some("razorpay") {
                return problem(StatusCode::BAD_REQUEST, "Bad Request", "provider mismatch");
            }
            let Some(aid) = view.attempt_id else {
                return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
            };
            (id, aid)
        }
        Binding::Session { session_id } => {
            match storage::payment_by_session(&st.pool, &org_id, "razorpay", &session_id).await {
                Ok(Some((pid, aid))) => (
                    domain::PaymentId::from_uuid(pid),
                    domain::AttemptId::from_uuid(aid),
                ),
                _ => {
                    return problem(StatusCode::BAD_REQUEST, "Bad Request", "checkout not found");
                }
            }
        }
    };
    let now = OffsetDateTime::now_utc();
    let cmd = match outcome {
        WebhookOutcome::Failed { .. } => ApplyCmd::InjectFailed {
            tenant_id: TenantId::new(org_id),
            rail: RailId::RAZORPAY,
            proof_id: event_id,
            payment_id,
            attempt_id,
            reason: TerminalReason::PspFailed,
            now,
        },
        WebhookOutcome::Paid { received, refs, .. } => ApplyCmd::InjectPaid {
            tenant_id: TenantId::new(org_id),
            rail: RailId::RAZORPAY,
            proof_id: event_id.clone(),
            payment_id,
            attempt_id,
            received,
            proof: Proof::PspWebhook {
                rail: RailId::RAZORPAY,
                event_id: ProofId::new(event_id),
            },
            now,
            refs,
        },
        WebhookOutcome::Ignored { .. } => unreachable!(),
    };
    match apply(&st.pool, cmd).await {
        Ok(ApplyOutcome::Duplicate) => Json(json!({"duplicate": true})).into_response(),
        Ok(_) => Json(json!({"ok": true})).into_response(),
        Err(e) => from_apply(e, false),
    }
}
