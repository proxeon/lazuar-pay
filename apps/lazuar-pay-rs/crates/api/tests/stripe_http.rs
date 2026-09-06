mod support;

use api::{testing_state, AppState};
use axum::body::Body;
use axum::http::{Request, StatusCode};
use http_body_util::BodyExt;
use serde_json::{json, Value};
use support::pool;
use tokio::sync::Mutex;
use tower::ServiceExt;

const SECRET: &str = "test-secret";
static STRIPE_HTTP: Mutex<()> = Mutex::const_new(());
const WHSEC: &str = "whsec_test";
const SK: &str = "sk_test_dummy";

async fn call(app: axum::Router, req: Request<Body>) -> (StatusCode, Value) {
    let res = app.oneshot(req).await.unwrap();
    let status = res.status();
    let bytes = res.into_body().collect().await.unwrap().to_bytes();
    let json: Value = if bytes.is_empty() {
        json!(null)
    } else {
        serde_json::from_slice(&bytes).unwrap_or(json!(null))
    };
    (status, json)
}

fn authed(method: &str, uri: &str, bearer: &str, body: Option<Value>) -> Request<Body> {
    let mut b = Request::builder()
        .method(method)
        .uri(uri)
        .header("Authorization", format!("Bearer {bearer}"));
    if body.is_some() {
        b = b.header("Content-Type", "application/json");
    }
    b.body(match body {
        Some(v) => Body::from(v.to_string()),
        None => Body::empty(),
    })
    .unwrap()
}

fn fixture(name: &str) -> String {
    std::fs::read_to_string(format!(
        "{}/../rails/tests/fixtures/stripe/{name}",
        env!("CARGO_MANIFEST_DIR")
    ))
    .unwrap()
}

fn put_stripe() -> Value {
    json!({
        "provider": "stripe",
        "secret": SK,
        "webhook_secret": WHSEC,
    })
}

#[tokio::test]
async fn put_test_processor_is_400() {
    let _g = STRIPE_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app,
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(json!({
                "provider": "test",
                "secret": "x",
                "webhook_secret": "y",
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "test processor does not take secrets");
}

#[tokio::test]
async fn put_billplz_is_rail_not_configured() {
    let _g = STRIPE_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app,
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(json!({
                "provider": "billplz",
                "secret": "x",
                "webhook_secret": "y",
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "rail not configured");
}

#[tokio::test]
async fn put_get_never_echoes_secrets_and_omit_env_keeps_live() {
    let _g = STRIPE_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(json!({
                "provider": "stripe",
                "secret": SK,
                "webhook_secret": WHSEC,
                "environment": "live",
            })),
        ),
    )
    .await;
    assert!(st.is_success());
    assert_eq!(body["last4"], "ummy");
    assert_eq!(body["configured"], true);
    assert_eq!(body["environment"], "live");
    let dumped = body.to_string();
    assert!(!dumped.contains(SK));
    assert!(!dumped.contains(WHSEC));

    let (st, body) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_stripe()),
        ),
    )
    .await;
    assert!(st.is_success());
    assert_eq!(body["environment"], "live");

    let (st, got) = call(
        app,
        authed(
            "GET",
            "/v1/orgs/t1/gateway?provider=stripe",
            "test-writer",
            None,
        ),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(got["last4"], "ummy");
    assert_eq!(got["environment"], "live");
    assert_eq!(got["webhook_configured"], true);
    assert_eq!(got["capability"], "hosted_link");
    let dumped = got.to_string();
    assert!(!dumped.contains(SK));
    assert!(!dumped.contains(WHSEC));
    assert!(!dumped.contains("sk_test"));
}

#[tokio::test]
async fn mint_stripe_without_vault_is_400() {
    let _g = STRIPE_HTTP.lock().await;
    let pool = pool().await;
    sqlx::query("DELETE FROM pay_rs.gateway_credentials WHERE tenant_id = 't1' AND rail = 'stripe'")
        .execute(&pool)
        .await
        .unwrap();
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app,
        authed(
            "POST",
            "/v1/checkouts",
            "test-writer",
            Some(json!({
                "org_id": "t1",
                "provider": "stripe",
                "amount": 10.00,
                "currency": "MYR"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "rail not configured");
}

#[tokio::test]
async fn put_mint_start_webhook_paid() {
    let _g = STRIPE_HTTP.lock().await;
    let pool = pool().await;
    let state: AppState = testing_state(pool.clone(), SECRET);
    let stripe = state.stripe.clone();
    let app = api::router(state);

    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_stripe()),
        ),
    )
    .await;
    assert!(st.is_success());

    let (st, minted) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/checkouts",
            "test-writer",
            Some(json!({
                "org_id": "t1",
                "provider": "stripe",
                "amount": 10.00,
                "currency": "MYR"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED);
    assert_eq!(minted["provider"], "stripe");
    assert_eq!(minted["status"], "open");
    assert_ne!(minted["status"], "settled");
    assert!(minted["amount"].is_number());
    let id = minted["id"].as_str().unwrap().to_string();
    let token = minted["public_token"].as_str().unwrap().to_string();

    let (st, pay) = call(
        app.clone(),
        Request::builder()
            .uri(format!("/v1/pay/{token}"))
            .body(Body::empty())
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(pay["provider"], "stripe");
    assert_eq!(pay["status"], "open");

    let (st, started) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token}/start"))
            .header("Content-Type", "application/json")
            .body(Body::from("{}"))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    let redirect = started["redirect_url"].as_str().unwrap().to_string();
    assert!(redirect.contains("cs_test_1"));
    assert_eq!(*stripe.last_unit_amount.lock().expect("lock"), Some(1000));

    let (st, again) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token}/start"))
            .header("Content-Type", "application/json")
            .body(Body::from("{}"))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(again["redirect_url"], redirect);

    let body = fixture("webhook_paid.json").replace("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", &id);
    let now = time::OffsetDateTime::now_utc().unix_timestamp();
    let sig = rails::stripe::sign(WHSEC, body.as_bytes(), now);
    let (st, hook) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/stripe/t1")
            .header("Stripe-Signature", sig)
            .header("Content-Type", "application/json")
            .body(Body::from(body.clone()))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert!(hook.get("ignored").is_none());
    assert_eq!(hook["ok"], true);

    let (st, pay) = call(
        app.clone(),
        Request::builder()
            .uri(format!("/v1/pay/{token}"))
            .body(Body::empty())
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(pay["status"], "paid");
    assert_ne!(pay["status"], "settled");

    let pid = domain::PaymentId::from_wire(&id).unwrap();
    let cap: Option<String> =
        sqlx::query_scalar("SELECT capture_id FROM pay_rs.attempts WHERE payment_id = $1")
            .bind(pid.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert!(cap.as_deref().unwrap().starts_with("pi_"));
    assert!(!cap.as_deref().unwrap().starts_with("cs_"));

    let now = time::OffsetDateTime::now_utc().unix_timestamp();
    let sig = rails::stripe::sign(WHSEC, body.as_bytes(), now);
    let (st, dup) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/stripe/t1")
            .header("Stripe-Signature", sig)
            .header("Content-Type", "application/json")
            .body(Body::from(body))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(dup["duplicate"], true);

    let charges: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(pid.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(charges, 1);
}

#[tokio::test]
async fn missing_stripe_signature_is_400() {
    let _g = STRIPE_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_stripe()),
        ),
    )
    .await;
    assert!(st.is_success());
    let (st, body) = call(
        app,
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/stripe/t1")
            .header("Content-Type", "application/json")
            .body(Body::from("{}"))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "invalid signature");
}

#[tokio::test]
async fn ignored_unpaid_completed_is_200() {
    let _g = STRIPE_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_stripe()),
        ),
    )
    .await;
    assert!(st.is_success());
    let body = fixture("webhook_completed_unpaid.json");
    let now = time::OffsetDateTime::now_utc().unix_timestamp();
    let sig = rails::stripe::sign(WHSEC, body.as_bytes(), now);
    let (st, json) = call(
        app,
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/stripe/t1")
            .header("Stripe-Signature", sig)
            .header("Content-Type", "application/json")
            .body(Body::from(body))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(json["ignored"], "payment_status:unpaid");
}
