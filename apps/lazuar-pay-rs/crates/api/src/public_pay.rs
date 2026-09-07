use axum::extract::{Path, Query, State};
use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use axum::Json;
use domain::rail::{HostedSession, RailId};
use domain::wire::buyer_status;
use domain::{AttemptId, PaymentStatus, Proof, ProofId};
use serde::Deserialize;
use serde_json::{json, Value};
use storage::{apply, ApplyCmd, ApplyOutcome};
use time::{Duration, OffsetDateTime};

use crate::errors::{from_apply, problem};
use crate::json::money_number;
use crate::mint_http;
use crate::AppState;

fn not_configured() -> Response {
    problem(
        StatusCode::BAD_REQUEST,
        "Bad Request",
        "rail not configured",
    )
}

fn rejected(rail: &str) -> Response {
    problem(
        StatusCode::SERVICE_UNAVAILABLE,
        "Service Unavailable",
        &format!("{rail} rejected the org key"),
    )
}

#[derive(Default, Deserialize)]
pub struct StartPayRequest {
    pub name: Option<String>,
    pub email: Option<String>,
    pub slot_key: Option<String>,
}

#[derive(Default, Deserialize)]
pub struct PayQuery {
    pub slot_key: Option<String>,
}

pub async fn get(
    State(st): State<AppState>,
    Path(token): Path<String>,
    Query(q): Query<PayQuery>,
) -> Response {
    if !st.limiter.try_acquire(&token) {
        return problem(
            StatusCode::TOO_MANY_REQUESTS,
            "Too Many Requests",
            "Too many start attempts",
        );
    }
    if let Ok(Some(link)) = storage::catalog::get_link_by_token(&st.pool, &token).await {
        return get_link(&st, &link, q.slot_key.as_deref()).await;
    }
    match storage::read::payment_by_public_token(&st.pool, &token).await {
        Ok(Some(view)) => {
            if let Some(lid) = view.payment_link_id {
                if let Ok(Some(link)) = storage::catalog::get_link_by_id(&st.pool, lid).await {
                    return get_link(&st, &link, view.slot_key.as_deref()).await;
                }
            }
            Json(public_json(&st, &view)).into_response()
        }
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
    if let Ok(Some(link)) = storage::catalog::get_link_by_token(&st.pool, &token).await {
        match mint_or_resume(&st, &link, &req).await {
            Ok(view) => {
                return start_hosted(st, view, req).await;
            }
            Err(r) => return r,
        }
    }
    let view = match storage::read::payment_by_public_token(&st.pool, &token).await {
        Ok(Some(v)) => v,
        Ok(None) => return problem(StatusCode::NOT_FOUND, "Not Found", "checkout not found"),
        Err(e) => return from_apply(e, false),
    };
    start_hosted(st, view, req).await
}

async fn start_hosted(st: AppState, view: storage::PaymentView, req: StartPayRequest) -> Response {
    if let Some(url) = view.session_url.clone() {
        if let Some(attempt_id) = view.attempt_id {
            if let Err(r) = fulfill_test(&st, &view, attempt_id).await {
                return r;
            }
        }
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
    let payment_id = view.id.to_wire();
    let session = if rail == RailId::STRIPE {
        let cred = match storage::get_credential(&st.pool, view.tenant_id.as_str(), "stripe").await
        {
            Ok(Some(c)) => c,
            _ => return not_configured(),
        };
        let form = rails::stripe::checkout_form(
            &payment_id,
            view.tenant_id.as_str(),
            i64::try_from(view.quoted.minor()).unwrap_or(i64::MAX),
            view.quoted.currency().code.as_str(),
            &success,
            &cancel,
        );
        let idem = rails::stripe::mint_idempotency_key(&payment_id);
        if let Some(http) = st.live_http.as_ref() {
            let Some(secret) = mint_http::unprotect(st.wrap_key, &cred) else {
                return not_configured();
            };
            match mint_http::stripe_session(http, &secret, &form, &idem).await {
                Ok(s) => s,
                Err(()) => return rejected("Stripe"),
            }
        } else {
            match st.stripe.create_session(&form) {
                Ok(s) => s,
                Err(_) => return rejected("Stripe"),
            }
        }
    } else if rail == RailId::CHIP {
        let cred = match storage::get_credential(&st.pool, view.tenant_id.as_str(), "chip").await {
            Ok(Some(c)) => c,
            _ => return not_configured(),
        };
        let brand = cred.public_merchant_id.as_deref().unwrap_or("");
        if brand.is_empty() {
            return not_configured();
        }
        let mail = email.unwrap_or("");
        let name = name_from(mail, req.name.as_deref());
        let payload = rails::chip::purchase_body(
            &payment_id,
            view.tenant_id.as_str(),
            brand,
            mail,
            &name,
            i64::try_from(view.quoted.minor()).unwrap_or(i64::MAX),
            view.quoted.currency().code.as_str(),
            &success,
            &cancel,
        );
        let idem = rails::chip::mint_idempotency_key(&payment_id);
        if let Some(http) = st.live_http.as_ref() {
            let Some(secret) = mint_http::unprotect(st.wrap_key, &cred) else {
                return not_configured();
            };
            match mint_http::chip_purchase(http, &secret, &payload, &idem).await {
                Ok(s) => s,
                Err(()) => return rejected("CHIP"),
            }
        } else {
            match st.chip.create_purchase(&payload) {
                Ok(s) => s,
                Err(_) => return rejected("CHIP"),
            }
        }
    } else if rail == RailId::BILLPLZ {
        let cred = match storage::get_credential(&st.pool, view.tenant_id.as_str(), "billplz").await
        {
            Ok(Some(c)) => c,
            _ => return not_configured(),
        };
        let collection = cred.public_merchant_id.as_deref().unwrap_or("");
        if collection.is_empty() {
            return not_configured();
        }
        let mail = email.unwrap_or("");
        let name = name_from(mail, req.name.as_deref());
        let base = st.public_base_url.trim_end_matches('/');
        let callback = format!(
            "{base}/v1/webhooks/billplz/{}?checkout_id={}",
            view.tenant_id.as_str(),
            payment_id
        );
        let host = rails::billplz::api_host(&cred.environment);
        let payload = rails::billplz::bill_body(
            &payment_id,
            collection,
            mail,
            &name,
            i64::try_from(view.quoted.minor()).unwrap_or(i64::MAX),
            &callback,
            &success,
        );
        let idem = rails::billplz::mint_idempotency_key(&payment_id);
        if let Some(http) = st.live_http.as_ref() {
            let Some(secret) = mint_http::unprotect(st.wrap_key, &cred) else {
                return not_configured();
            };
            match mint_http::billplz_bill(http, &secret, host, &payload, &idem).await {
                Ok(s) => s,
                Err(()) => return rejected("Billplz"),
            }
        } else {
            match st.billplz.create_bill(host, &payload) {
                Ok(s) => s,
                Err(_) => return rejected("Billplz"),
            }
        }
    } else if rail == RailId::XENDIT {
        let cred = match storage::get_credential(&st.pool, view.tenant_id.as_str(), "xendit").await
        {
            Ok(Some(c)) => c,
            _ => return not_configured(),
        };
        let mail = email.unwrap_or("");
        let payload = rails::xendit::invoice_body(
            &payment_id,
            view.tenant_id.as_str(),
            mail,
            crate::json::money_number(view.quoted),
            view.quoted.currency().code.as_str(),
            &success,
            &cancel,
        );
        let idem = rails::xendit::mint_idempotency_key(&payment_id);
        if let Some(http) = st.live_http.as_ref() {
            let Some(secret) = mint_http::unprotect(st.wrap_key, &cred) else {
                return not_configured();
            };
            match mint_http::xendit_invoice(http, &secret, &payload, &idem).await {
                Ok(s) => s,
                Err(()) => return rejected("Xendit"),
            }
        } else {
            match st.xendit.create_invoice(rails::xendit::API_BASE, &payload) {
                Ok(s) => s,
                Err(_) => return rejected("Xendit"),
            }
        }
    } else if rail == RailId::RAZORPAY {
        let cred =
            match storage::get_credential(&st.pool, view.tenant_id.as_str(), "razorpay").await {
                Ok(Some(c)) => c,
                _ => return not_configured(),
            };
        let mail = email.unwrap_or("");
        let name = name_from(mail, req.name.as_deref());
        let payload = rails::razorpay::link_body(
            &payment_id,
            view.tenant_id.as_str(),
            mail,
            &name,
            i64::try_from(view.quoted.minor()).unwrap_or(i64::MAX),
            view.quoted.currency().code.as_str(),
            &success,
        );
        let idem = rails::razorpay::mint_idempotency_key(&payment_id);
        if let Some(http) = st.live_http.as_ref() {
            let Some(secret) = mint_http::unprotect(st.wrap_key, &cred) else {
                return not_configured();
            };
            let Some((key_id, key_secret)) = rails::razorpay::try_split(&secret) else {
                return not_configured();
            };
            match mint_http::razorpay_link(http, key_id, key_secret, &payload, &idem).await {
                Ok(s) => s,
                Err(()) => return rejected("Razorpay"),
            }
        } else {
            match st
                .razorpay
                .create_link(rails::razorpay::API_BASE, &payload, &idem)
            {
                Ok(s) => s,
                Err(_) => return rejected("Razorpay"),
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
        if !st.env.allows_test() {
            return problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                "test processor is not enabled",
            );
        }
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
        Ok(ApplyOutcome::SessionResume { url, .. }) => {
            if let Err(r) = fulfill_test(&st, &view, started).await {
                return r;
            }
            start_json(&url)
        }
        Ok(_) => {
            if let Err(r) = fulfill_test(&st, &view, started).await {
                return r;
            }
            start_json(&url)
        }
        Err(e) => from_apply(e, false),
    }
}

/// .NET `TestHosted` + `FulfillPaid` on start: no PSP, no webhook. SPA `?status=verifying`
/// polls GET until `paid`. Without this Take, the buyer tab waits forever.
async fn fulfill_test(
    st: &AppState,
    view: &storage::PaymentView,
    attempt_id: AttemptId,
) -> Result<(), Response> {
    let rail = view
        .provider
        .as_deref()
        .and_then(|s| RailId::parse(s).ok())
        .unwrap_or(RailId::TEST);
    if rail != RailId::TEST {
        return Ok(());
    }
    if !st.env.allows_test() {
        return Err(problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "test processor is not enabled",
        ));
    }
    if !matches!(view.status, PaymentStatus::Open | PaymentStatus::Processing) {
        return Ok(());
    }
    let proof_id = view
        .session_id
        .clone()
        .unwrap_or_else(|| format!("test:{}", view.id.to_wire()));
    match apply(
        &st.pool,
        ApplyCmd::InjectPaid {
            tenant_id: view.tenant_id.clone(),
            rail: RailId::TEST,
            proof_id: proof_id.clone(),
            payment_id: view.id,
            attempt_id,
            received: view.quoted,
            proof: Proof::PspWebhook {
                rail: RailId::TEST,
                event_id: ProofId::new(proof_id),
            },
            now: OffsetDateTime::now_utc(),
            refs: Default::default(),
        },
    )
    .await
    {
        Ok(ApplyOutcome::Duplicate | ApplyOutcome::Applied { .. }) => Ok(()),
        Ok(_) => Ok(()),
        Err(e) => Err(from_apply(e, false)),
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
    if let Ok(Some(_)) = storage::catalog::get_link_by_token(&st.pool, &token).await {
        if storage::read::payment_by_public_token(&st.pool, &token)
            .await
            .ok()
            .flatten()
            .is_none()
        {
            return problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                "confirm a started checkout token",
            );
        }
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

async fn get_link(
    st: &AppState,
    link: &storage::catalog::PaymentLinkRow,
    slot_key: Option<&str>,
) -> Response {
    expire_link_children(st, link).await;
    let occ = storage::catalog::occupancy(&st.pool, link.id)
        .await
        .unwrap_or(storage::catalog::Occupancy { taken: 0, paid: 0 });
    let slot = normalize_slot_key(slot_key);
    if let Some(slot) = slot {
        if let Ok(Some(pid)) = storage::catalog::child_by_slot(&st.pool, link.id, &slot).await {
            if let Ok(Some(view)) = storage::read::payment_by_id(&st.pool, pid).await {
                if !matches!(view.status, PaymentStatus::Expired | PaymentStatus::Failed) {
                    return Json(checkout_on_link(st, link, &view, &occ, true)).into_response();
                }
            }
        }
    }
    let status = if link.max_payers == Some(1) && occ.paid >= 1 {
        "already_paid"
    } else if occ.is_full(link.max_payers) {
        "full"
    } else {
        "open"
    };
    Json(link_pay_json(st, link, status, &occ)).into_response()
}

async fn expire_link_children(st: &AppState, link: &storage::catalog::PaymentLinkRow) {
    let paused = storage::read::charges_paused(&st.pool, link.tenant_id.as_str())
        .await
        .unwrap_or(false);
    let now = OffsetDateTime::now_utc();
    let ids = if paused {
        storage::catalog::open_child_ids(&st.pool, link.id, None)
            .await
            .unwrap_or_default()
    } else {
        storage::catalog::open_child_ids(&st.pool, link.id, Some(now))
            .await
            .unwrap_or_default()
    };
    for id in ids {
        let cmd = if paused {
            ApplyCmd::WatchTimeout {
                payment_id: id,
                now,
            }
        } else {
            ApplyCmd::ExpireClock {
                payment_id: id,
                now,
            }
        };
        let _ = apply(&st.pool, cmd).await;
    }
}

async fn mint_or_resume(
    st: &AppState,
    link: &storage::catalog::PaymentLinkRow,
    req: &StartPayRequest,
) -> Result<storage::PaymentView, Response> {
    if storage::read::charges_paused(&st.pool, link.tenant_id.as_str())
        .await
        .unwrap_or(false)
    {
        return Err(problem(
            StatusCode::FORBIDDEN,
            "Forbidden",
            "Org charges are paused",
        ));
    }
    let Some(slot) = normalize_slot_key(req.slot_key.as_deref()) else {
        return Err(problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "slot_key is required",
        ));
    };
    let rail = RailId::parse(&link.rail).unwrap_or(RailId::TEST);
    let email = req
        .email
        .as_deref()
        .map(str::trim)
        .filter(|s| !s.is_empty());
    if rail.caps().requires_email && !email_usable(email) {
        return Err(problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "email is required",
        ));
    }
    if rail == RailId::BILLPLZ && !rails::billplz::public_base_ok(&st.public_base_url) {
        return Err(problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "callback base not public",
        ));
    }
    expire_link_children(st, link).await;
    let occ = storage::catalog::occupancy(&st.pool, link.id)
        .await
        .unwrap_or(storage::catalog::Occupancy { taken: 0, paid: 0 });
    if let Ok(Some(pid)) = storage::catalog::child_by_slot(&st.pool, link.id, &slot).await {
        if let Ok(Some(view)) = storage::read::payment_by_id(&st.pool, pid).await {
            match view.status {
                PaymentStatus::Settled => {
                    if view.session_url.as_deref().is_some_and(|s| !s.is_empty()) {
                        return Ok(view);
                    }
                    return Err(problem(
                        StatusCode::CONFLICT,
                        "Conflict",
                        "Checkout is not open",
                    ));
                }
                PaymentStatus::Expired | PaymentStatus::Failed => {
                    let _ = storage::catalog::burn_slot(&st.pool, pid, &slot).await;
                }
                _ => return Ok(view),
            }
        }
    }
    if occ.is_full(link.max_payers) {
        return Err(from_apply(storage::ApplyError::LinkFull, false));
    }
    let now = OffsetDateTime::now_utc();
    let ttl = Duration::minutes(30);
    let base = st.checkout_base_url.trim_end_matches('/');
    let token = format!(
        "{}{}",
        uuid::Uuid::new_v4().simple(),
        uuid::Uuid::new_v4().simple()
    );
    let minted = apply(
        &st.pool,
        ApplyCmd::Mint(storage::MintSpec {
            tenant_id: link.tenant_id.clone(),
            public_token: domain::PublicToken::new(token),
            quoted: link.quoted,
            expires_at: now + ttl,
            monitoring_until: now + ttl,
            payment_link_id: Some(link.id),
            slot_key: Some(slot.clone()),
            success_url: Some(format!("{base}/c/{}?status=verifying", link.public_token)),
            cancel_url: Some(format!("{base}/c/{}", link.public_token)),
            rail,
        }),
    )
    .await;
    let payment_id = match minted {
        Ok(ApplyOutcome::Minted { payment_id }) => payment_id,
        Err(storage::ApplyError::LinkFull) => {
            return Err(from_apply(storage::ApplyError::LinkFull, false));
        }
        Err(storage::ApplyError::Conflict) => {
            if let Ok(Some(pid)) = storage::catalog::child_by_slot(&st.pool, link.id, &slot).await {
                if let Ok(Some(view)) = storage::read::payment_by_id(&st.pool, pid).await {
                    if !matches!(
                        view.status,
                        PaymentStatus::Settled | PaymentStatus::Expired | PaymentStatus::Failed
                    ) {
                        return Ok(view);
                    }
                }
            }
            return Err(from_apply(storage::ApplyError::LinkFull, false));
        }
        Err(e) => return Err(from_apply(e, false)),
        Ok(_) => {
            return Err(problem(
                StatusCode::INTERNAL_SERVER_ERROR,
                "Internal Server Error",
                "mint",
            ));
        }
    };
    let _ = storage::update_payer(
        &st.pool,
        payment_id.as_uuid(),
        req.name.as_deref().map(str::trim).filter(|s| !s.is_empty()),
        email,
    )
    .await;
    storage::read::payment_by_id(&st.pool, payment_id)
        .await
        .ok()
        .flatten()
        .ok_or_else(|| problem(StatusCode::NOT_FOUND, "Not Found", "checkout not found"))
}

fn normalize_slot_key(raw: Option<&str>) -> Option<String> {
    let slot = raw.map(str::trim).filter(|s| !s.is_empty())?;
    if slot.len() < 8 || slot.len() > 128 {
        return None;
    }
    Some(slot.to_string())
}

fn link_pay_json(
    st: &AppState,
    link: &storage::catalog::PaymentLinkRow,
    status: &str,
    occ: &storage::catalog::Occupancy,
) -> Value {
    let solana = link.rail == RailId::SOLANA.as_str();
    json!({
        "token": link.public_token,
        "amount": money_number(link.quoted),
        "currency": link.quoted.currency().code.as_str(),
        "status": status,
        "email_required": RailId::parse(&link.rail).map(|r| r.caps().requires_email).unwrap_or(false),
        "started": false,
        "mine": false,
        "provider": link.rail,
        "redirect_url": Value::Null,
        "solana_pay_url": Value::Null,
        "solana_cluster": if solana { json!(st.solana_cluster) } else { Value::Null },
        "remaining": occ.remaining_clamped(link.max_payers),
        "max_payers": link.max_payers,
        "paid_count": occ.paid,
        "taken_count": occ.taken,
    })
}

fn checkout_on_link(
    st: &AppState,
    link: &storage::catalog::PaymentLinkRow,
    view: &storage::PaymentView,
    occ: &storage::catalog::Occupancy,
    mine: bool,
) -> Value {
    let mut v = public_json(st, view);
    v["token"] = json!(link.public_token);
    v["mine"] = json!(mine);
    v["remaining"] = json!(occ.remaining_clamped(link.max_payers));
    v["max_payers"] = json!(link.max_payers);
    v["paid_count"] = json!(occ.paid);
    v["taken_count"] = json!(occ.taken);
    v
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
        "mine": true,
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
