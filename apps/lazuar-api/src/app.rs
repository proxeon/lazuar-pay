//! Application assembly + HTTP router. The C# equivalent is `Program.cs`.
//! Handlers are thin: parse → typed module call → status mapping.

use std::sync::Arc;

use serde_json::{json, Value};

use crate::config::Config;
use crate::hosting::PayError;
use crate::identity::one_client::OneClient;
use crate::money::refunds;
use crate::transport::Transport;

pub type PgPool = crate::db::PgPool;

pub struct State {
    pub config: Config,
    pub started_at: chrono::DateTime<chrono::Utc>,
    pub pool: Option<PgPool>,
    pub psp: Arc<dyn Transport>,
    pub one: Arc<dyn Transport>,
    pub one_client: OneClient,
    pub secret_box: crate::secrets::SecretBox,
    pub fulfill_gates: crate::money::fulfillment::CheckoutGates,
    pub start_gates: crate::publicpay::gates::GateMap,
    pub link_gates: crate::publicpay::gates::GateMap,
    pub limiter: crate::publicpay::limiter::PublicPayLimiter,
    pub whoami_cache: Arc<crate::identity::whoami_cache::OneWhoamiCache>,
}

pub fn router(request: &rouille::Request, state: &State) -> rouille::Response {
    let url = request.url().to_string();
    let segments: Vec<&str> = url.split('/').filter(|s| !s.is_empty()).collect();
    let method = request.method();

    match (method, segments.as_slice()) {
        ("GET", ["health"]) => health(),
        ("GET", ["ready"]) => ready(state),
        ("GET", ["v1", "health"]) => health(),

        ("GET", ["v1", "whoami"]) => whoami_route(request, state),

        ("POST", ["v1", "webhooks", provider, org_id]) => {
            webhook_route(request, state, provider, org_id)
        }

        ("POST", ["v1", "orgs", org_id, "refunds"]) => refund_create_route(request, state, org_id),
        ("GET", ["v1", "orgs", org_id, "refunds"]) => refund_list_route(request, state, org_id),

        ("POST", ["v1", "payment-links"]) => link_create_route(request, state),
        ("GET", ["v1", "orgs", org_id, "payment-links"]) => {
            link_list_route(request, state, org_id)
        }

        ("GET", ["v1", "pay", token]) => public_view_route(request, state, token),
        ("POST", ["v1", "pay", token, "start"]) => public_start_route(request, state, token),
        ("POST", ["v1", "pay", token, "confirm"]) => public_confirm_route(request, state, token),

        ("GET", ["v1", "orgs", org_id, "ready"]) => org_ready_route(request, state, org_id),

        ("POST", ["v1", "checkouts"]) => checkout_create_route(request, state),
        ("GET", ["v1", "checkouts", id]) => checkout_get_route(request, state, id),
        ("GET", ["v1", "orgs", org_id, "checkouts"]) => checkout_list_route(request, state, org_id),

        ("POST", ["v1", "orgs", org_id, "products"]) => catalog_create_route(request, state, org_id),
        ("GET", ["v1", "orgs", org_id, "products"]) => catalog_list_route(request, state, org_id),

        ("PUT", ["v1", "orgs", org_id, "gateway"]) => gateway_put_route(request, state, org_id),
        ("GET", ["v1", "orgs", org_id, "gateway"]) => gateway_get_route(request, state, org_id),
        ("GET", ["v1", "orgs", org_id, "gateways"]) => gateway_list_route(request, state, org_id),

        ("GET", ["v1", "orgs", org_id, "payments"]) => payments_list_route(request, state, org_id),
        ("GET", ["v1", "orgs", org_id, "receipts", id]) => receipt_get_route(request, state, org_id, id),
        ("GET", ["v1", "orgs", org_id, "receipts"]) => receipts_list_route(request, state, org_id),
        ("GET", ["v1", "orgs", org_id, "subscriptions"]) => subscriptions_list_route(request, state, org_id),

        ("POST", ["v1", "one", "webhooks"]) => one_webhook_route(request, state),
        ("PUT", ["v1", "orgs", org_id, "one-webhook"]) => one_webhook_put_route(request, state, org_id),
        ("GET", ["v1", "orgs", org_id, "one-webhook"]) => one_webhook_get_route(request, state, org_id),

        ("PUT", ["v1", "orgs", org_id, "webhooks"]) => org_webhook_put_route(request, state, org_id),
        ("GET", ["v1", "orgs", org_id, "webhooks"]) => org_webhook_get_route(request, state, org_id),
        ("POST", ["v1", "orgs", org_id, "webhooks", "rotate"]) => {
            org_webhook_rotate_route(request, state, org_id)
        }
        ("POST", ["v1", "orgs", org_id, "webhooks", "test"]) => {
            org_webhook_test_route(request, state, org_id)
        }

        _ => not_found(),
    }
}

// ---------------------------------------------------------------------------
// Health
// ---------------------------------------------------------------------------

fn health() -> rouille::Response {
    rouille::Response::json(&serde_json::json!({ "status": "ok" }))
}

fn ready(state: &State) -> rouille::Response {
    let db_ok = match &state.pool {
        Some(pool) => pool.get().ok().map(|mut conn| {
            conn.query_one("SELECT 1", &[]).is_ok()
                && conn
                    .query_one(
                        "SELECT to_regclass('public.checkouts') IS NOT NULL \
                         AND to_regclass('public.org_settings') IS NOT NULL",
                        &[],
                    )
                    .map(|row| row.get::<_, bool>(0))
                    .unwrap_or(false)
        }).unwrap_or(false),
        None => false,
    };
    if db_ok {
        rouille::Response::json(&serde_json::json!({ "status": "ready" }))
    } else {
        rouille::Response::json(&serde_json::json!({ "status": "not_ready" })).with_status_code(503)
    }
}

fn not_found() -> rouille::Response {
    error_response(&PayError::new(404, "Not Found", "Not Found"))
}

// ---------------------------------------------------------------------------
// Error mapping
// ---------------------------------------------------------------------------

/// Parse an inbound JSON amount as an exact decimal: accepts JSON numbers
/// (via the raw arbitrary-precision token, so `12.50` keeps its scale) and
/// numeric strings. C# `decimal?` binds both shapes.
pub fn parse_decimal(value: Option<&serde_json::Value>) -> Option<rust_decimal::Decimal> {
    use rust_decimal::Decimal;
    use std::str::FromStr as _;
    match value? {
        serde_json::Value::Number(n) => Decimal::from_str(&n.to_string()).ok(),
        serde_json::Value::String(s) => Decimal::from_str(s).ok(),
        _ => None,
    }
}

fn error_response(err: &PayError) -> rouille::Response {
    rouille::Response::json(&serde_json::json!({
        "status": err.status,
        "title": err.title,
        "detail": err.detail,
    }))
    .with_status_code(err.status)
}

// ---------------------------------------------------------------------------
// Whoami
// ---------------------------------------------------------------------------

fn bearer(request: &rouille::Request) -> Option<String> {
    let value = request.header("Authorization")?;
    (!value.trim().is_empty()).then(|| value.to_string())
}

fn tenant_hint(request: &rouille::Request) -> Option<String> {
    request.header("X-Lazuar-Tenant-Id").map(str::to_string)
}

fn whoami_route(request: &rouille::Request, state: &State) -> rouille::Response {
    use crate::identity::whoami::WhoamiOutcome;
    match crate::identity::whoami::whoami(
        &state.one_client,
        state.one.as_ref(),
        Some(state.whoami_cache.as_ref()),
        bearer(request).as_deref(),
        tenant_hint(request).as_deref(),
    ) {
        WhoamiOutcome::Ok(resp) => rouille::Response::json(&resp),
        WhoamiOutcome::Error(err) => error_response(&err),
    }
}

// ---------------------------------------------------------------------------
// Webhook ingestion
// ---------------------------------------------------------------------------

fn webhook_route(
    request: &rouille::Request,
    state: &State,
    provider_raw: &str,
    org_id: &str,
) -> rouille::Response {
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    let raw_body = {
        let mut buf = String::new();
        use std::io::Read as _;
        let _ = request.data().map(|mut r| r.read_to_string(&mut buf));
        buf
    };
    let headers: Vec<(String, String)> = request
        .headers()
        .map(|(k, v)| (k.to_string(), v.to_string()))
        .collect();

    let input = crate::webhooks::ingest::IngestInput {
        provider_raw,
        org_id,
        raw_body: &raw_body,
        headers: &headers,
        environment: &state.config.environment,
        test_webhook_secret: &state.config.test_webhook_secret,
        stripe_webhook_secret: &state.config.stripe_webhook_secret,
    };
    let remote = crate::rails::remote::LiveRefunder::load(
        &mut conn,
        &state.secret_box,
        state.psp.clone(),
        org_id,
        None,
    )
    .unwrap_or_else(|_| crate::rails::remote::LiveRefunder {
        transport: state.psp.clone(),
        secrets: Default::default(),
        chip_session_id: None,
    });
    match crate::webhooks::ingest::handle(&mut conn, &state.secret_box, &state.fulfill_gates, &remote, &input)
    {
        Ok(outcome) => match outcome {
            crate::webhooks::ingest::IngestOutcome::Duplicate => {
                rouille::Response::json(&serde_json::json!({ "duplicate": true }))
            }
            crate::webhooks::ingest::IngestOutcome::Ignored { reason } => {
                rouille::Response::json(&serde_json::json!({ "ignored": reason }))
            }
            crate::webhooks::ingest::IngestOutcome::Failed => {
                rouille::Response::json(&serde_json::json!({ "failed": true }))
            }
            crate::webhooks::ingest::IngestOutcome::LateRefunded { refunded } => {
                rouille::Response::json(&serde_json::json!({ "refunded": refunded }))
            }
            crate::webhooks::ingest::IngestOutcome::PaidOk => {
                rouille::Response::json(&serde_json::json!({ "ok": true }))
            }
            crate::webhooks::ingest::IngestOutcome::PausedConflict => error_response(&PayError::conflict("Org charges are paused")),
            crate::webhooks::ingest::IngestOutcome::UnknownProvider
            | crate::webhooks::ingest::IngestOutcome::EmptyBody
            | crate::webhooks::ingest::IngestOutcome::RailNotConfigured
            | crate::webhooks::ingest::IngestOutcome::CheckoutNotFound
            | crate::webhooks::ingest::IngestOutcome::ProviderMismatch
            | crate::webhooks::ingest::IngestOutcome::CurrencyMismatch
            | crate::webhooks::ingest::IngestOutcome::AmountMismatch
            | crate::webhooks::ingest::IngestOutcome::VerifyError(_) => {
                error_response(&PayError::bad_request(ingest_detail(&outcome)))
            }
            crate::webhooks::ingest::IngestOutcome::MissingSecret(message) => {
                error_response(&PayError::unavailable(message))
            }
            crate::webhooks::ingest::IngestOutcome::FulfillConflict => {
                error_response(&PayError::internal("fulfill conflict"))
            }
            crate::webhooks::ingest::IngestOutcome::FulfillFailed => {
                error_response(&PayError::internal("fulfill failed"))
            }
        },
        Err(err) => {
            log::error!("webhook ingest error: {err}");
            error_response(&PayError::internal("webhook processing failed"))
        }
    }
}

fn ingest_detail(outcome: &crate::webhooks::ingest::IngestOutcome) -> String {
    use crate::webhooks::ingest::IngestOutcome as O;
    match outcome {
        O::UnknownProvider => "unknown provider".into(),
        O::EmptyBody => "empty body".into(),
        O::RailNotConfigured => "rail not configured".into(),
        O::CheckoutNotFound => "checkout not found".into(),
        O::ProviderMismatch => "provider mismatch".into(),
        O::CurrencyMismatch => "currency mismatch".into(),
        O::AmountMismatch => "amount mismatch".into(),
        O::VerifyError(message) => message.clone(),
        _ => "bad request".into(),
    }
}

// ---------------------------------------------------------------------------
// Refunds
// ---------------------------------------------------------------------------

fn refund_create_route(
    request: &rouille::Request,
    state: &State,
    org_id: &str,
) -> rouille::Response {
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    let auth = bearer(request);
    let hint = tenant_hint(request);

    // Writer gate (machine key or owner/admin human).
    if let Err(err) = crate::identity::member_gate::require_writer(
        &state.one_client,
        state.one.as_ref(),
        Some(state.whoami_cache.as_ref()),
        auth.as_deref(),
        hint.as_deref(),
        org_id,
    ) {
        return error_response(&err);
    }

    let raw_body = {
        let mut buf = String::new();
        use std::io::Read as _;
        let _ = request.data().map(|mut r| r.read_to_string(&mut buf));
        buf
    };
    let parsed: serde_json::Value = serde_json::from_str(&raw_body).unwrap_or(serde_json::Value::Null);
    let checkout_id = parsed
        .get("checkout_id")
        .and_then(serde_json::Value::as_str)
        .unwrap_or("")
        .to_string();
    let amount = parse_decimal(parsed.get("amount"));
    let idempotency = request
        .header("Idempotency-Key")
        .map(str::to_string)
        .filter(|s| !s.trim().is_empty())
        .or_else(|| {
            parsed
                .get("idempotency_key")
                .and_then(serde_json::Value::as_str)
                .map(str::to_string)
        });

    let input = refunds::CreateRefund { checkout_id, amount, idempotency_key: idempotency };
    let remote = crate::rails::remote::LiveRefunder::load(
        &mut conn,
        &state.secret_box,
        state.psp.clone(),
        org_id,
        None,
    )
    .unwrap_or_else(|_| crate::rails::remote::LiveRefunder {
        transport: state.psp.clone(),
        secrets: Default::default(),
        chip_session_id: None,
    });
    match refunds::create_refund(&mut conn, org_id, &input, &remote) {
        Ok(refunds::CreateRefundOutcome::Created { refund, number }) => {
            rouille::Response::json(&serde_json::json!({
                "id": refund.id,
                "org_id": refund.org_id,
                "checkout_id": refund.checkout_id,
                "charge_id": refund.charge_id,
                "amount": crate::hosting::decimal_json(refund.amount),
                "currency": refund.currency,
                "status": refund.status,
                "provider": refund.provider,
                "reason": refund.reason,
                "number": number,
                "created_at": refund.created_at,
            }))
            .with_status_code(201)
        }
        Ok(refunds::CreateRefundOutcome::Replayed(refund)) => {
            rouille::Response::json(&serde_json::json!({
                "id": refund.id,
                "org_id": refund.org_id,
                "checkout_id": refund.checkout_id,
                "charge_id": refund.charge_id,
                "amount": crate::hosting::decimal_json(refund.amount),
                "currency": refund.currency,
                "status": refund.status,
                "provider": refund.provider,
                "reason": refund.reason,
                "number": serde_json::Value::Null,
                "created_at": refund.created_at,
            }))
        }
        Ok(refunds::CreateRefundOutcome::CheckoutIdRequired) => {
            error_response(&PayError::bad_request("checkout_id is required"))
        }
        Ok(refunds::CreateRefundOutcome::ChargeNotFound) => {
            error_response(&PayError::not_found("charge not found"))
        }
        Ok(refunds::CreateRefundOutcome::CheckoutNotFound) => {
            error_response(&PayError::not_found("checkout not found"))
        }
        Ok(refunds::CreateRefundOutcome::AlreadyRefunded) => {
            error_response(&PayError::conflict("already refunded"))
        }
        Ok(refunds::CreateRefundOutcome::AmountOutOfRange) => error_response(&PayError::bad_request(
            "amount must be within the refundable remainder",
        )),
        Ok(refunds::CreateRefundOutcome::Conflict) => error_response(&PayError::conflict(
            "idempotency key reused with a different body",
        )),
        Ok(refunds::CreateRefundOutcome::UnsupportedRail(message)) => {
            error_response(&PayError::bad_request(message))
        }
        Ok(refunds::CreateRefundOutcome::ProcessorRejected) => {
            error_response(&PayError::bad_gateway("processor rejected the refund"))
        }
        Ok(refunds::CreateRefundOutcome::ReservationConflict) => {
            error_response(&PayError::internal("refund reservation conflict"))
        }
        Ok(refunds::CreateRefundOutcome::AmbiguousOutcome) => error_response(&PayError::bad_gateway(
            "refund outcome unknown — held pending for reconciliation",
        )),
        Err(err) => {
            log::error!("refund create error: {err}");
            error_response(&PayError::internal("refund failed"))
        }
    }
}

fn refund_list_route(request: &rouille::Request, state: &State, org_id: &str) -> rouille::Response {
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    let auth = bearer(request);
    let hint = tenant_hint(request);
    if let Err(err) = crate::identity::member_gate::require_member(
        &state.one_client,
        state.one.as_ref(),
        Some(state.whoami_cache.as_ref()),
        auth.as_deref(),
        hint.as_deref(),
        org_id,
    ) {
        return error_response(&err);
    }
    let limit: Option<i64> = request.get_param("limit").and_then(|v| v.parse().ok());
    let after: Option<String> = request.get_param("after").map(|v| v.to_string());
    match refunds::list_refunds(&mut conn, org_id, limit, after.as_deref()) {
        Ok(page) => rouille::Response::json(&page),
        Err(err) => {
            log::error!("refund list error: {err}");
            error_response(&PayError::internal("refund list failed"))
        }
    }
}

// ---------------------------------------------------------------------------
// Payment links
// ---------------------------------------------------------------------------

fn link_create_route(request: &rouille::Request, state: &State) -> rouille::Response {
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    let raw_body = {
        let mut buf = String::new();
        use std::io::Read as _;
        let _ = request.data().map(|mut r| r.read_to_string(&mut buf));
        buf
    };
    let parsed: serde_json::Value = serde_json::from_str(&raw_body).unwrap_or(serde_json::Value::Null);
    let org_id = parsed.get("org_id").and_then(serde_json::Value::as_str).unwrap_or("");
    let auth = bearer(request);
    let hint = tenant_hint(request);
    if let Err(err) = crate::identity::member_gate::require_writer(
        &state.one_client,
        state.one.as_ref(),
        Some(state.whoami_cache.as_ref()),
        auth.as_deref(),
        hint.as_deref(),
        org_id,
    ) {
        return error_response(&err);
    }

    let input = crate::links::create::CreateLinkInput {
        org_id,
        provider: parsed.get("provider").and_then(serde_json::Value::as_str),
        amount: parse_decimal(parsed.get("amount")),
        currency: parsed.get("currency").and_then(serde_json::Value::as_str),
        product_id: parsed.get("product_id").and_then(serde_json::Value::as_str),
        max_payers: parsed.get("max_payers").and_then(serde_json::Value::as_i64).map(|v| v as i32),
        unlimited: parsed.get("unlimited").and_then(serde_json::Value::as_bool).unwrap_or(false),
    };
    match crate::links::create::create_link(&mut conn, &state.config.environment, &input) {
        Ok(crate::links::create::CreateLinkOutcome::Created {
            id,
            public_token,
            provider,
            amount,
            currency,
            max_payers,
        }) => {
            let checkout_base = &state.config.checkout_base_url;
            let remaining = crate::publicpay::occupancy::remaining_unclamped(max_payers, 0);
            rouille::Response::json(&serde_json::json!({
                "id": id,
                "org_id": org_id,
                "provider": provider,
                "amount": crate::hosting::decimal_json(amount),
                "currency": currency,
                "status": "open",
                "public_token": public_token,
                "pay_url": format!("{checkout_base}/c/{public_token}"),
                "max_payers": max_payers,
                "unlimited": max_payers.is_none(),
                "paid_count": 0,
                "taken_count": 0,
                "remaining": remaining,
            }))
            .with_status_code(201)
        }
        Ok(outcome) => {
            use crate::links::create::CreateLinkOutcome as O;
            let err = match outcome {
                O::Created { .. } => unreachable!(),
                O::Paused => PayError::forbidden("Org charges are paused"),
                O::AmountRequired => PayError::bad_request("amount must be greater than 0"),
                O::UnknownProvider => PayError::bad_request("unknown provider"),
                O::TestNotEnabled => PayError::bad_request("test processor is not enabled"),
                O::RailNotConfigured => PayError::bad_request("rail not configured"),
                O::MaxPayersTooLow => PayError::bad_request("max_payers must be at least 1"),
                O::SolanaMintError(message) => PayError::bad_request(message),
                O::CurrencyUnsupported { currency, provider, supported } => PayError::bad_request(format!(
                    "currency {currency} is not supported on {provider}; supported: {supported}"
                )),
                O::ProductNotFound => PayError::not_found("product not found"),
                O::CatalogPriceMismatch => PayError::bad_request("amount must match the catalog price"),
            };
            error_response(&err)
        }
        Err(err) => {
            log::error!("link create error: {err}");
            error_response(&PayError::internal("link create failed"))
        }
    }
}

fn link_list_route(request: &rouille::Request, state: &State, org_id: &str) -> rouille::Response {
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    let auth = bearer(request);
    let hint = tenant_hint(request);
    if let Err(err) = crate::identity::member_gate::require_member(
        &state.one_client,
        state.one.as_ref(),
        Some(state.whoami_cache.as_ref()),
        auth.as_deref(),
        hint.as_deref(),
        org_id,
    ) {
        return error_response(&err);
    }
    let limit: Option<i64> = request.get_param("limit").and_then(|v| v.parse().ok());
    let after: Option<String> = request.get_param("after").map(|v| v.to_string());
    match crate::links::list::list_views(
        &mut conn,
        org_id,
        &state.config.checkout_base_url,
        limit,
        after.as_deref(),
    ) {
        Ok(page) => rouille::Response::json(&page),
        Err(err) => {
            log::error!("link list error: {err}");
            error_response(&PayError::internal("link list failed"))
        }
    }
}

// ---------------------------------------------------------------------------
// Public pay
// ---------------------------------------------------------------------------

fn public_view_route(request: &rouille::Request, state: &State, token: &str) -> rouille::Response {
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    let slot_key = request.get_param("slot_key");
    match crate::publicpay::view::get(
        &mut conn,
        &state.config.checkout_base_url,
        &state.config.solana_cluster,
        state.config.reservation_ttl_minutes,
        token,
        slot_key.as_deref(),
    ) {
        Ok(Ok(view)) => rouille::Response::json(&view),
        Ok(Err(err)) => error_response(&err),
        Err(err) => {
            log::error!("public view error: {err}");
            error_response(&PayError::internal("public view failed"))
        }
    }
}

fn public_start_route(
    request: &rouille::Request,
    state: &State,
    token: &str,
) -> rouille::Response {
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    let raw_body = {
        let mut buf = String::new();
        use std::io::Read as _;
        let _ = request.data().map(|mut r| r.read_to_string(&mut buf));
        buf
    };
    let parsed: serde_json::Value = serde_json::from_str(&raw_body).unwrap_or(serde_json::Value::Null);

    let test_rail = crate::publicpay::start::TestRail {
        checkout_base_url: state.config.checkout_base_url.clone(),
    };
    let vaulted = state.pool.clone().map(|pool| crate::rails::hosted::VaultedRail {
        pool,
        box_one: &state.secret_box,
        transport: state.psp.as_ref(),
        environment: &state.config.environment,
        public_base_url: &state.config.public_base_url,
        checkout_base_url: &state.config.checkout_base_url,
        solana_cluster: &state.config.solana_cluster,
    });
    let rail: &dyn crate::publicpay::start::HostedRail = match vaulted.as_ref() {
        Some(v) => v,
        None => &test_rail,
    };
    let deps = crate::publicpay::start::StartDeps {
        environment: &state.config.environment,
        start_max_per_minute: state.config.start_max_per_minute as i32,
        limiter: &state.limiter,
        start_gates: &state.start_gates,
        link_gates: &state.link_gates,
        fulfill_gates: &state.fulfill_gates,
        rail,
    };
    let req = crate::publicpay::start::StartRequest {
        name: parsed.get("name").and_then(serde_json::Value::as_str),
        email: parsed.get("email").and_then(serde_json::Value::as_str),
        slot_key: parsed.get("slot_key").and_then(serde_json::Value::as_str),
    };

    use crate::publicpay::start::StartOutcome;
    match crate::publicpay::start::start(&mut conn, &deps, token, &req) {
        Ok(StartOutcome::Started { redirect_url }) => {
            rouille::Response::json(&serde_json::json!({ "redirect_url": redirect_url }))
        }
        Ok(StartOutcome::TooManyRequests) => error_response(&PayError::too_many_requests()),
        Ok(StartOutcome::CheckoutNotFound) => error_response(&PayError::not_found("Checkout not found")),
        Ok(StartOutcome::NotOpen) => error_response(&PayError::conflict("Checkout is not open")),
        Ok(StartOutcome::Paused) => error_response(&PayError::forbidden("Org charges are paused")),
        Ok(StartOutcome::EmailRequired) => error_response(&PayError::bad_request("email is required")),
        Ok(StartOutcome::SlotKeyRequired) => error_response(&PayError::bad_request("slot_key is required")),
        Ok(StartOutcome::LinkFull) => error_response(&PayError::conflict("This pay link is full")),
        Ok(StartOutcome::RailNotConfigured) => {
            error_response(&PayError::unavailable("rail not configured"))
        }
        Ok(StartOutcome::Rejected(message)) => error_response(&PayError::unavailable(message)),
        Ok(StartOutcome::BadRequest(message)) => error_response(&PayError::bad_request(message)),
        Err(err) => {
            log::error!("public start error: {err}");
            error_response(&PayError::internal("start failed"))
        }
    }
}

fn public_confirm_route(request: &rouille::Request, state: &State, token: &str) -> rouille::Response {
    if state.config.start_max_per_minute > 0
        && !state.limiter.try_acquire(
            &format!("confirm:{token}"),
            state.config.start_max_per_minute as i32,
            60,
        )
    {
        return error_response(&PayError::too_many_requests_detail("Too many confirm attempts"));
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    let parsed = read_json(request);
    let signature = parsed
        .get("signature")
        .and_then(Value::as_str)
        .unwrap_or("")
        .trim()
        .to_string();

    if conn
        .query_opt(
            "SELECT 1 FROM public.payment_links WHERE \"PublicToken\" = $1",
            &[&token],
        )
        .ok()
        .flatten()
        .is_some()
    {
        return error_response(&PayError::bad_request("confirm a started checkout token"));
    }
    let Some(session) = crate::domain::checkout_store::get_by_public_token(&mut conn, token)
        .ok()
        .flatten()
    else {
        return error_response(&PayError::not_found("Checkout not found"));
    };
    let checkout = crate::rails::solana::confirm::CheckoutForSolana {
        id: session.id.clone(),
        org_id: session.org_id.clone(),
        amount: session.amount,
        currency: session.currency.clone(),
        status: session.status.clone(),
        provider: session.provider.clone(),
        provider_session_id: conn
            .query_opt(
                "SELECT COALESCE(\"ProviderSessionId\",'') FROM public.checkouts WHERE \"Id\" = $1",
                &[&session.id],
            )
            .ok()
            .flatten()
            .map(|r| r.get::<_, String>(0))
            .unwrap_or_default(),
        public_merchant_id: conn
            .query_opt(
                "SELECT COALESCE(\"PublicMerchantId\",'') FROM public.gateway_credentials \
                 WHERE \"OrgId\" = $1 AND \"Provider\" = $2",
                &[&session.org_id, &crate::rails::providers::SOLANA],
            )
            .ok()
            .flatten()
            .map(|r| r.get::<_, String>(0))
            .unwrap_or_default(),
    };
    let rpc = crate::rails::solana::rpc::SolanaRpc {
        rpc_url: Some(state.config.solana_rpc_url.clone()),
        transport: Box::new(crate::transport::UreqTransport::new_no_redirects(10)),
    };
    let deps = crate::rails::solana::confirm::ConfirmDeps {
        box_one: &state.secret_box,
        gates: &state.fulfill_gates,
        rpc: &rpc,
        environment: &state.config.environment,
        config_cluster: &state.config.solana_cluster,
    };
    match crate::rails::solana::confirm::confirm(&mut conn, &deps, &checkout, &signature) {
        Ok(crate::rails::solana::confirm::ConfirmOutcome::Ok)
        | Ok(crate::rails::solana::confirm::ConfirmOutcome::Duplicate) => {
            rouille::Response::json(&json!({ "ok": true }))
        }
        Ok(crate::rails::solana::confirm::ConfirmOutcome::LatePayManual { refunded }) => {
            rouille::Response::json(&json!({ "refunded": refunded }))
        }
        Ok(crate::rails::solana::confirm::ConfirmOutcome::ProviderMismatch) => {
            error_response(&PayError::bad_request("not a solana checkout"))
        }
        Ok(crate::rails::solana::confirm::ConfirmOutcome::SignatureRequired) => {
            error_response(&PayError::bad_request("signature is required"))
        }
        Ok(crate::rails::solana::confirm::ConfirmOutcome::RailNotConfigured) => {
            error_response(&PayError::unavailable("rail not configured"))
        }
        Ok(crate::rails::solana::confirm::ConfirmOutcome::ClusterMismatch) => {
            error_response(&PayError::bad_request("solana cluster mismatch"))
        }
        Ok(crate::rails::solana::confirm::ConfirmOutcome::Paused) => {
            error_response(&PayError::forbidden("Org charges are paused"))
        }
        Ok(crate::rails::solana::confirm::ConfirmOutcome::NotOpen) => {
            error_response(&PayError::conflict("Checkout is not open"))
        }
        Ok(crate::rails::solana::confirm::ConfirmOutcome::ValidationFailed(m)) => {
            error_response(&PayError::bad_request(m))
        }
        Ok(crate::rails::solana::confirm::ConfirmOutcome::FulfillConflict) => {
            error_response(&PayError::internal("fulfill conflict"))
        }
        Ok(crate::rails::solana::confirm::ConfirmOutcome::Unavailable(m)) => {
            error_response(&PayError::unavailable(m))
        }
        Ok(crate::rails::solana::confirm::ConfirmOutcome::Throttled) => {
            error_response(&PayError::internal("solana rpc throttled"))
        }
        Err(err) => {
            log::error!("confirm error: {err}");
            error_response(&PayError::internal("confirm failed"))
        }
    }
}

fn read_json(request: &rouille::Request) -> Value {
    let mut buf = String::new();
    use std::io::Read as _;
    let _ = request.data().map(|mut r| r.read_to_string(&mut buf));
    serde_json::from_str(&buf).unwrap_or(Value::Null)
}

fn gate_writer(request: &rouille::Request, state: &State, org_id: &str) -> Result<(), PayError> {
    crate::identity::member_gate::require_writer(
        &state.one_client,
        state.one.as_ref(),
        Some(state.whoami_cache.as_ref()),
        bearer(request).as_deref(),
        tenant_hint(request).as_deref(),
        org_id,
    )
}

fn gate_member(request: &rouille::Request, state: &State, org_id: &str) -> Result<(), PayError> {
    crate::identity::member_gate::require_member(
        &state.one_client,
        state.one.as_ref(),
        Some(state.whoami_cache.as_ref()),
        bearer(request).as_deref(),
        tenant_hint(request).as_deref(),
        org_id,
    )
}

fn org_ready_route(request: &rouille::Request, state: &State, org_id: &str) -> rouille::Response {
    if let Err(err) = gate_member(request, state, org_id) {
        return error_response(&err);
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    match crate::identity::org_ready::handle(&mut conn, org_id, &state.config.environment) {
        Ok(v) => rouille::Response::json(&v),
        Err(err) => {
            log::error!("org ready: {err}");
            error_response(&PayError::internal("ready failed"))
        }
    }
}

fn checkout_create_route(request: &rouille::Request, state: &State) -> rouille::Response {
    let parsed = read_json(request);
    let org_id = parsed.get("org_id").and_then(Value::as_str).unwrap_or("");
    if let Err(err) = gate_writer(request, state, org_id) {
        return error_response(&err);
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    let idem = request.header("Idempotency-Key").map(str::trim).filter(|s| !s.is_empty());
    let idem = idem.or_else(|| parsed.get("idempotency_key").and_then(Value::as_str));
    match crate::merchant::checkout_create(
        &mut conn,
        &state.config.environment,
        &state.config.checkout_base_url,
        org_id,
        &parsed,
        idem,
    ) {
        Ok(Ok((status, body))) => rouille::Response::json(&body).with_status_code(status),
        Ok(Err(err)) => error_response(&err),
        Err(err) => {
            log::error!("checkout create: {err}");
            error_response(&PayError::internal("checkout create failed"))
        }
    }
}

fn checkout_get_route(request: &rouille::Request, state: &State, id: &str) -> rouille::Response {
    if bearer(request).is_none() {
        return error_response(&PayError::unauthorized("Missing bearer token"));
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    match crate::merchant::checkout_get(&mut conn, &state.config.checkout_base_url, id) {
        Ok(Some(session)) => {
            if let Err(err) = gate_member(request, state, &session.org_id) {
                if err.status == 403 && !err.detail.to_lowercase().contains("suspend") {
                    return error_response(&PayError::not_found("Checkout not found"));
                }
                return error_response(&err);
            }
            rouille::Response::json(&crate::merchant::checkout_view(&session, &state.config.checkout_base_url))
        }
        Ok(None) => error_response(&PayError::not_found("Checkout not found")),
        Err(err) => {
            log::error!("checkout get: {err}");
            error_response(&PayError::internal("checkout get failed"))
        }
    }
}

fn checkout_list_route(request: &rouille::Request, state: &State, org_id: &str) -> rouille::Response {
    if let Err(err) = gate_member(request, state, org_id) {
        return error_response(&err);
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    let limit = request.get_param("limit").and_then(|v| v.parse().ok());
    let after = request.get_param("after");
    match crate::merchant::checkout_list(&mut conn, &state.config.checkout_base_url, org_id, limit, after.as_deref()) {
        Ok(v) => rouille::Response::json(&v),
        Err(err) => {
            log::error!("checkout list: {err}");
            error_response(&PayError::internal("checkout list failed"))
        }
    }
}

fn catalog_create_route(request: &rouille::Request, state: &State, org_id: &str) -> rouille::Response {
    if let Err(err) = gate_writer(request, state, org_id) {
        return error_response(&err);
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    match crate::merchant::catalog_create(&mut conn, org_id, &read_json(request)) {
        Ok(Ok((status, body))) => rouille::Response::json(&body).with_status_code(status),
        Ok(Err(err)) => error_response(&err),
        Err(err) => {
            log::error!("catalog create: {err}");
            error_response(&PayError::internal("catalog create failed"))
        }
    }
}

fn catalog_list_route(request: &rouille::Request, state: &State, org_id: &str) -> rouille::Response {
    if let Err(err) = gate_member(request, state, org_id) {
        return error_response(&err);
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    let limit = request.get_param("limit").and_then(|v| v.parse().ok());
    let after = request.get_param("after");
    match crate::merchant::catalog_list(&mut conn, org_id, limit, after.as_deref()) {
        Ok(v) => rouille::Response::json(&v),
        Err(err) => {
            log::error!("catalog list: {err}");
            error_response(&PayError::internal("catalog list failed"))
        }
    }
}

fn gateway_put_route(request: &rouille::Request, state: &State, org_id: &str) -> rouille::Response {
    if let Err(err) = gate_writer(request, state, org_id) {
        return error_response(&err);
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    match crate::merchant::gateway_put(
        &mut conn,
        &state.secret_box,
        &state.config.solana_cluster,
        org_id,
        &read_json(request),
    ) {
        Ok(Ok(v)) => rouille::Response::json(&v),
        Ok(Err(err)) => error_response(&err),
        Err(err) => {
            log::error!("gateway put: {err}");
            error_response(&PayError::internal("gateway put failed"))
        }
    }
}

fn gateway_get_route(request: &rouille::Request, state: &State, org_id: &str) -> rouille::Response {
    if let Err(err) = gate_member(request, state, org_id) {
        return error_response(&err);
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    let provider = request.get_param("provider").unwrap_or_default();
    match crate::merchant::gateway_get(&mut conn, &state.config.environment, org_id, &provider) {
        Ok(Ok(v)) => rouille::Response::json(&v),
        Ok(Err(err)) => error_response(&err),
        Err(err) => {
            log::error!("gateway get: {err}");
            error_response(&PayError::internal("gateway get failed"))
        }
    }
}

fn gateway_list_route(request: &rouille::Request, state: &State, org_id: &str) -> rouille::Response {
    if let Err(err) = gate_member(request, state, org_id) {
        return error_response(&err);
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    match crate::merchant::gateway_list(&mut conn, &state.config.environment, org_id) {
        Ok(v) => rouille::Response::json(&v),
        Err(err) => {
            log::error!("gateway list: {err}");
            error_response(&PayError::internal("gateway list failed"))
        }
    }
}

fn payments_list_route(request: &rouille::Request, state: &State, org_id: &str) -> rouille::Response {
    if let Err(err) = gate_member(request, state, org_id) {
        return error_response(&err);
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    let limit = request.get_param("limit").and_then(|v| v.parse().ok());
    let after = request.get_param("after");
    match crate::money::queries::list_payments(&mut conn, org_id, limit, after.as_deref()) {
        Ok(v) => rouille::Response::json(&v),
        Err(err) => {
            log::error!("payments: {err}");
            error_response(&PayError::internal("payments list failed"))
        }
    }
}

fn receipts_list_route(request: &rouille::Request, state: &State, org_id: &str) -> rouille::Response {
    if let Err(err) = gate_member(request, state, org_id) {
        return error_response(&err);
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    let limit = request.get_param("limit").and_then(|v| v.parse().ok());
    let after = request.get_param("after");
    match crate::money::queries::list_receipts(&mut conn, org_id, limit, after.as_deref()) {
        Ok(v) => rouille::Response::json(&v),
        Err(err) => {
            log::error!("receipts: {err}");
            error_response(&PayError::internal("receipts list failed"))
        }
    }
}

fn receipt_get_route(request: &rouille::Request, state: &State, org_id: &str, id: &str) -> rouille::Response {
    if let Err(err) = gate_member(request, state, org_id) {
        return error_response(&err);
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    match crate::money::queries::get_receipt(&mut conn, org_id, id) {
        Ok(Some(v)) => rouille::Response::json(&v),
        Ok(None) => error_response(&PayError::not_found("Receipt not found")),
        Err(err) => {
            log::error!("receipt: {err}");
            error_response(&PayError::internal("receipt get failed"))
        }
    }
}

fn subscriptions_list_route(request: &rouille::Request, state: &State, org_id: &str) -> rouille::Response {
    if let Err(err) = gate_member(request, state, org_id) {
        return error_response(&err);
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    let limit = request.get_param("limit").and_then(|v| v.parse().ok());
    let after = request.get_param("after");
    match crate::money::queries::list_subscriptions(&mut conn, org_id, limit, after.as_deref()) {
        Ok(v) => rouille::Response::json(&v),
        Err(err) => {
            log::error!("subscriptions: {err}");
            error_response(&PayError::internal("subscriptions list failed"))
        }
    }
}

fn one_webhook_route(request: &rouille::Request, state: &State) -> rouille::Response {
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    let mut buf = String::new();
    use std::io::Read as _;
    let _ = request.data().map(|mut r| r.read_to_string(&mut buf));
    let sig = request.header("X-Lazuar-Signature");
    let ts = request.header("X-Lazuar-Timestamp");
    let event_id = request.header("X-Lazuar-Event-Id");
    match crate::identity::one_webhooks::handle(
        &mut conn,
        &state.secret_box,
        state.whoami_cache.as_ref(),
        &state.config.one_webhook_secret,
        &buf,
        sig,
        ts,
        event_id,
    ) {
        Ok(Ok(v)) => rouille::Response::json(&v),
        Ok(Err(err)) => error_response(&err),
        Err(err) => {
            log::error!("one webhook: {err}");
            error_response(&PayError::internal("one webhook failed"))
        }
    }
}

fn one_webhook_put_route(request: &rouille::Request, state: &State, org_id: &str) -> rouille::Response {
    if let Err(err) = gate_writer(request, state, org_id) {
        return error_response(&err);
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    let parsed = read_json(request);
    let secret = parsed.get("webhook_secret").and_then(Value::as_str);
    match crate::identity::one_webhooks::put_secret(&mut conn, &state.secret_box, org_id, secret) {
        Ok(Ok(v)) => rouille::Response::json(&v),
        Ok(Err(err)) => error_response(&err),
        Err(err) => {
            log::error!("one webhook put: {err}");
            error_response(&PayError::internal("one webhook put failed"))
        }
    }
}

fn one_webhook_get_route(request: &rouille::Request, state: &State, org_id: &str) -> rouille::Response {
    if let Err(err) = gate_member(request, state, org_id) {
        return error_response(&err);
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    match crate::identity::one_webhooks::get_secret(&mut conn, org_id) {
        Ok(v) => rouille::Response::json(&v),
        Err(err) => {
            log::error!("one webhook get: {err}");
            error_response(&PayError::internal("one webhook get failed"))
        }
    }
}

fn org_webhook_put_route(request: &rouille::Request, state: &State, org_id: &str) -> rouille::Response {
    if let Err(err) = gate_writer(request, state, org_id) {
        return error_response(&err);
    }
    let parsed = read_json(request);
    let url = match crate::webhooks::org_config::validate_url(
        parsed.get("url").and_then(Value::as_str),
        &state.config.environment,
    ) {
        Ok(u) => u,
        Err(err) => return error_response(&err),
    };
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    match crate::webhooks::org_config::put(&mut conn, &state.secret_box, org_id, &url) {
        Ok(Ok(v)) => rouille::Response::json(&v),
        Ok(Err(err)) => error_response(&err),
        Err(err) => {
            log::error!("org webhook put: {err}");
            error_response(&PayError::internal("org webhook put failed"))
        }
    }
}

fn org_webhook_get_route(request: &rouille::Request, state: &State, org_id: &str) -> rouille::Response {
    if let Err(err) = gate_member(request, state, org_id) {
        return error_response(&err);
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    match crate::webhooks::org_config::get(&mut conn, org_id) {
        Ok(v) => rouille::Response::json(&v),
        Err(err) => {
            log::error!("org webhook get: {err}");
            error_response(&PayError::internal("org webhook get failed"))
        }
    }
}

fn org_webhook_rotate_route(request: &rouille::Request, state: &State, org_id: &str) -> rouille::Response {
    if let Err(err) = gate_writer(request, state, org_id) {
        return error_response(&err);
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    match crate::webhooks::org_config::rotate(&mut conn, &state.secret_box, org_id) {
        Ok(Ok(v)) => rouille::Response::json(&v),
        Ok(Err(err)) => error_response(&err),
        Err(err) => {
            log::error!("org webhook rotate: {err}");
            error_response(&PayError::internal("org webhook rotate failed"))
        }
    }
}

fn org_webhook_test_route(request: &rouille::Request, state: &State, org_id: &str) -> rouille::Response {
    if let Err(err) = gate_writer(request, state, org_id) {
        return error_response(&err);
    }
    let Some(mut conn) = state.pool.as_ref().and_then(|p| p.get().ok()) else {
        return error_response(&PayError::unavailable("database unavailable"));
    };
    match crate::webhooks::org_config::test_ping(&mut conn, org_id) {
        Ok(Ok(v)) => rouille::Response::json(&v),
        Ok(Err(err)) => error_response(&err),
        Err(err) => {
            log::error!("org webhook test: {err}");
            error_response(&PayError::internal("org webhook test failed"))
        }
    }
}

/// Route pattern for logging (C# `RouteEndpoint.RoutePattern.RawText` analogue).
fn route_pattern(request: &rouille::Request) -> &'static str {
    let url = request.url().to_string();
    let segments: Vec<&str> = url.split('/').filter(|s| !s.is_empty()).collect();
    match (request.method(), segments.as_slice()) {
        ("GET", ["health"]) | ("GET", ["v1", "health"]) => "/health",
        ("GET", ["ready"]) => "/ready",
        ("GET", ["v1", "whoami"]) => "/v1/whoami",
        ("POST", ["v1", "webhooks", _p, _o]) => "/v1/webhooks/{provider}/{orgId}",
        ("POST", ["v1", "orgs", _o, "refunds"]) => "/v1/orgs/{orgId}/refunds",
        ("GET", ["v1", "orgs", _o, "refunds"]) => "/v1/orgs/{orgId}/refunds",
        ("POST", ["v1", "payment-links"]) => "/v1/payment-links",
        ("GET", ["v1", "orgs", _o, "payment-links"]) => "/v1/orgs/{orgId}/payment-links",
        ("GET", ["v1", "pay", _t]) => "/v1/pay/{token}",
        ("POST", ["v1", "pay", _t, "start"]) => "/v1/pay/{token}/start",
        ("POST", ["v1", "pay", _t, "confirm"]) => "/v1/pay/{token}/confirm",
        ("GET", ["v1", "orgs", _o, "ready"]) => "/v1/orgs/{orgId}/ready",
        ("POST", ["v1", "checkouts"]) => "/v1/checkouts",
        ("GET", ["v1", "checkouts", _id]) => "/v1/checkouts/{id}",
        ("GET", ["v1", "orgs", _o, "checkouts"]) => "/v1/orgs/{orgId}/checkouts",
        ("POST", ["v1", "orgs", _o, "products"]) => "/v1/orgs/{orgId}/products",
        ("GET", ["v1", "orgs", _o, "products"]) => "/v1/orgs/{orgId}/products",
        ("PUT", ["v1", "orgs", _o, "gateway"]) => "/v1/orgs/{orgId}/gateway",
        ("GET", ["v1", "orgs", _o, "gateway"]) => "/v1/orgs/{orgId}/gateway",
        ("GET", ["v1", "orgs", _o, "gateways"]) => "/v1/orgs/{orgId}/gateways",
        ("GET", ["v1", "orgs", _o, "payments"]) => "/v1/orgs/{orgId}/payments",
        ("GET", ["v1", "orgs", _o, "receipts", _id]) => "/v1/orgs/{orgId}/receipts/{id}",
        ("GET", ["v1", "orgs", _o, "receipts"]) => "/v1/orgs/{orgId}/receipts",
        ("GET", ["v1", "orgs", _o, "subscriptions"]) => "/v1/orgs/{orgId}/subscriptions",
        ("POST", ["v1", "one", "webhooks"]) => "/v1/one/webhooks",
        ("PUT", ["v1", "orgs", _o, "one-webhook"]) => "/v1/orgs/{orgId}/one-webhook",
        ("GET", ["v1", "orgs", _o, "one-webhook"]) => "/v1/orgs/{orgId}/one-webhook",
        ("PUT", ["v1", "orgs", _o, "webhooks"]) => "/v1/orgs/{orgId}/webhooks",
        ("GET", ["v1", "orgs", _o, "webhooks"]) => "/v1/orgs/{orgId}/webhooks",
        ("POST", ["v1", "orgs", _o, "webhooks", "rotate"]) => "/v1/orgs/{orgId}/webhooks/rotate",
        ("POST", ["v1", "orgs", _o, "webhooks", "test"]) => "/v1/orgs/{orgId}/webhooks/test",
        _ => "(unmatched)",
    }
}

/// `PayCors.Resolve` (C# `Hosting/PayCors.cs:33-47`): configured origins, or
/// the laptop list in Development/Testing.
pub fn resolve_cors_origins(raw: &[String], environment: &str) -> Vec<String> {
    if !raw.is_empty() {
        return raw.to_vec();
    }
    if environment == "Development" || environment == "Testing" {
        return [
            "http://localhost:5178",
            "http://127.0.0.1:5178",
            "http://localhost:5179",
            "http://127.0.0.1:5179",
            "http://localhost:4178",
            "http://127.0.0.1:4178",
            "http://localhost:4179",
            "http://127.0.0.1:4179",
        ]
        .iter()
        .map(|s| s.to_string())
        .collect();
    }
    Vec::new()
}

fn origin_allowed(state: &State, request: &rouille::Request) -> Option<String> {
    let origin = request.header("Origin")?;
    let allowed = resolve_cors_origins(&state.config.cors_origins, &state.config.environment);
    allowed
        .into_iter()
        .find(|allowed| allowed.eq_ignore_ascii_case(origin))
}

/// CORS headers for allowed origins (C# `WithOrigins(origins)
/// .AllowAnyHeader().AllowAnyMethod()`).
fn cors_headers(response: &mut rouille::Response, state: &State, request: &rouille::Request) {
    if let Some(allowed) = origin_allowed(state, request) {
        response
            .headers
            .push(("Access-Control-Allow-Origin".into(), allowed.into()));
        response
            .headers
            .push(("Vary".into(), "Origin".into()));
    }
}

fn preflight(state: &State, request: &rouille::Request) -> rouille::Response {
    let Some(allowed) = origin_allowed(state, request) else {
        return rouille::Response::text("").with_status_code(403);
    };
    let mut response = rouille::Response::text("").with_status_code(204);
    response
        .headers
        .push(("Access-Control-Allow-Origin".into(), allowed.into()));
    response
        .headers
        .push(("Access-Control-Allow-Methods".into(), "GET, POST, PUT, DELETE, OPTIONS".into()));
    if let Some(requested) = request.header("Access-Control-Request-Headers") {
        response
            .headers
            .push(("Access-Control-Allow-Headers".into(), requested.to_string().into()));
    }
    response
        .headers
        .push(("Access-Control-Max-Age".into(), "600".into()));
    response
}

/// Block serving requests. rouille is thread-per-request: blocking inside a
/// handler is safe by design — there is no executor to stall (D001).
/// The wrapper adds the CORS layer and the request log (C# UseCors +
/// UsePayRequestLog, `Program.cs:129-130`).
pub fn serve(addr: String, state: Arc<State>) -> ! {
    println!("lazuar-api (sync rust) listening on http://{addr}");
    rouille::start_server(addr, move |request| {
        let start = std::time::Instant::now();
        // X-Request-Id echo with fallback (C# RequestLog.cs:15-17).
        let request_id = request
            .header("X-Request-Id")
            .map(str::trim)
            .filter(|s| !s.is_empty())
            .map(str::to_string)
            .unwrap_or_else(|| uuid::Uuid::new_v4().simple().to_string());

        // CORS preflight short-circuits before routing (C# UseCors handles it).
        let mut response = if request.method() == "OPTIONS" {
            preflight(&state, request)
        } else {
            let mut r = router(request, &state);
            cors_headers(&mut r, &state, request);
            r
        };

        response
            .headers
            .push(("X-Request-Id".into(), request_id.clone().into()));

        let pattern = route_pattern(request);
        let org = if pattern.starts_with("/v1/orgs/") || pattern.starts_with("/v1/webhooks/") {
            request.url().split('/').nth(4).unwrap_or("").to_string()
        } else {
            String::new()
        };
        let duration_ms = start.elapsed().as_millis();
        log::info!(
            "http {} {} {pattern} {status} {duration_ms} {org}",
            request_id,
            request.method(),
            status = response.status_code,
        );
        response
    })
}
