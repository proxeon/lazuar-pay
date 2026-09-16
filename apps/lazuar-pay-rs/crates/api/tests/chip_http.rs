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
const CHIP_SK: &str = "chip_sk";
static CHIP_HTTP: Mutex<()> = Mutex::const_new(());

fn fixture(name: &str) -> String {
    std::fs::read_to_string(format!(
        "{}/../rails/tests/fixtures/chip/{name}",
        env!("CARGO_MANIFEST_DIR")
    ))
    .unwrap()
}

fn public_pem() -> String {
    fixture("test_public.pem")
}

fn private_pem() -> String {
    fixture("test_private.pem")
}

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

fn put_chip() -> Value {
    json!({
        "provider": "chip",
        "secret": CHIP_SK,
        "webhook_secret": public_pem(),
        "public_merchant_id": "brand_1",
    })
}

#[tokio::test]
async fn put_chip_requires_brand() {
    let _g = CHIP_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app,
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(json!({
                "provider": "chip",
                "secret": CHIP_SK,
                "webhook_secret": public_pem(),
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "public_merchant_id is required");
}

#[tokio::test]
async fn put_chip_rejects_non_pem() {
    let _g = CHIP_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app,
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(json!({
                "provider": "chip",
                "secret": CHIP_SK,
                "webhook_secret": "nope",
                "public_merchant_id": "brand_1",
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert!(body["detail"].as_str().unwrap().contains("PEM"));
}

#[tokio::test]
async fn put_get_never_echoes_pem_and_omit_env_keeps_live() {
    let _g = CHIP_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(json!({
                "provider": "chip",
                "secret": CHIP_SK,
                "webhook_secret": public_pem(),
                "public_merchant_id": "brand_1",
                "environment": "live",
            })),
        ),
    )
    .await;
    assert!(st.is_success());
    assert_eq!(body["last4"], "p_sk");
    assert_eq!(body["public_merchant_id"], "brand_1");
    assert_eq!(body["environment"], "live");
    let dumped = body.to_string();
    assert!(!dumped.contains("BEGIN PUBLIC KEY"));
    assert!(!dumped.contains(CHIP_SK));

    let (st, body) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_chip()),
        ),
    )
    .await;
    assert!(st.is_success());
    assert_eq!(body["environment"], "live");

    let (st, got) = call(
        app,
        authed(
            "GET",
            "/v1/orgs/t1/gateway?provider=chip",
            "test-writer",
            None,
        ),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(got["configured"], true);
    assert_eq!(got["public_merchant_id"], "brand_1");
    assert!(!got.to_string().contains("BEGIN PUBLIC KEY"));
}

#[tokio::test]
async fn mint_chip_without_vault_is_400() {
    let _g = CHIP_HTTP.lock().await;
    let pool = pool().await;
    sqlx::query("DELETE FROM pay_rs.gateway_credentials WHERE tenant_id = 't1' AND rail = 'chip'")
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
                "provider": "chip",
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
async fn mint_chip_usd_is_400() {
    let _g = CHIP_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_chip()),
        ),
    )
    .await;
    assert!(st.is_success());
    let (st, body) = call(
        app,
        authed(
            "POST",
            "/v1/checkouts",
            "test-writer",
            Some(json!({
                "org_id": "t1",
                "provider": "chip",
                "amount": 10.00,
                "currency": "USD"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert!(body["detail"].as_str().unwrap().contains("currency"));
}

#[tokio::test]
async fn put_mint_start_webhook_paid() {
    let _g = CHIP_HTTP.lock().await;
    let pool = pool().await;
    let state: AppState = testing_state(pool.clone(), SECRET);
    let chip = state.chip.clone();
    let app = api::router(state);

    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_chip()),
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
                "provider": "chip",
                "amount": 10.00,
                "currency": "MYR"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED);
    assert_eq!(minted["provider"], "chip");
    assert_eq!(minted["status"], "open");
    assert_ne!(minted["status"], "settled");
    assert!(minted["amount"].is_number());
    let id = minted["id"].as_str().unwrap().to_string();
    let token = minted["public_token"].as_str().unwrap().to_string();

    let (st, miss) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token}/start"))
            .header("Content-Type", "application/json")
            .body(Body::from("{}"))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(miss["detail"], "email is required");
    assert!(chip.last_body.lock().expect("lock").is_none());

    let (st, ph) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token}/start"))
            .header("Content-Type", "application/json")
            .body(Body::from(r#"{"email":"customer@example.com"}"#))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(ph["detail"], "email is required");
    assert!(chip.last_body.lock().expect("lock").is_none());

    let (st, started) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token}/start"))
            .header("Content-Type", "application/json")
            .body(Body::from(r#"{"name":"Ada","email":"ada@acme.test"}"#))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{started}");
    assert_eq!(started["redirect_url"], "https://gate.chip-in.asia/p/x");
    assert_eq!(*chip.last_price.lock().expect("lock"), Some(1000));
    assert_eq!(
        chip.last_email.lock().expect("lock").clone().unwrap(),
        "ada@acme.test"
    );
    let dumped = chip.last_body.lock().expect("lock").clone().unwrap();
    assert!(!dumped.contains("force_recurring"));

    let (st, again) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token}/start"))
            .header("Content-Type", "application/json")
            .body(Body::from(r#"{"email":"ada@acme.test"}"#))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(again["redirect_url"], started["redirect_url"]);

    let pid = domain::PaymentId::from_wire(&id).unwrap();
    let sid: String =
        sqlx::query_scalar("SELECT session_id FROM pay_rs.attempts WHERE payment_id = $1")
            .bind(pid.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert!(sid.starts_with("purch_"));
    let body = fixture("webhook_paid.json")
        .replace("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", &id)
        .replace("purch_1", &sid);
    let sig = rails::chip::sign(&private_pem(), body.as_bytes()).unwrap();
    let (st, hook) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/chip/t1")
            .header("X-Signature", sig)
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
    assert_eq!(pay["email_required"], true);

    let cap: Option<String> =
        sqlx::query_scalar("SELECT capture_id FROM pay_rs.attempts WHERE payment_id = $1")
            .bind(pid.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(cap.as_deref(), Some(sid.as_str()));

    let sig = rails::chip::sign(&private_pem(), body.as_bytes()).unwrap();
    let (st, dup) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/chip/t1")
            .header("X-Signature", sig)
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

    let pre =
        fixture("webhook_preauthorized.json").replace("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", &id);
    let sig = rails::chip::sign(&private_pem(), pre.as_bytes()).unwrap();
    let (st, ign) = call(
        app,
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/chip/t1")
            .header("X-Signature", sig)
            .header("Content-Type", "application/json")
            .body(Body::from(pre))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(ign["ignored"], "preauthorized");
}

#[tokio::test]
async fn paid_total_10_does_not_consume_inbound() {
    let _g = CHIP_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool.clone(), SECRET));
    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_chip()),
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
                "provider": "chip",
                "amount": 10.00,
                "currency": "MYR"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED);
    let id = minted["id"].as_str().unwrap().to_string();
    let token = minted["public_token"].as_str().unwrap().to_string();
    let (st, started) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token}/start"))
            .header("Content-Type", "application/json")
            .body(Body::from(r#"{"email":"ada@acme.test"}"#))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{started}");
    let mismatch = fixture("webhook_paid.json")
        .replace("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", &id)
        .replace("purch_1", "purch_mis")
        .replace("\"total\":1000", "\"total\":10");
    let sig = rails::chip::sign(&private_pem(), mismatch.as_bytes()).unwrap();
    let (st, _) = call(
        app,
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/chip/t1")
            .header("X-Signature", sig)
            .header("Content-Type", "application/json")
            .body(Body::from(mismatch))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    let inbound: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.inbound_events WHERE proof_id = $1",
    )
    .bind("paid:purch_mis")
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(inbound, 0);
}

#[tokio::test]
async fn missing_chip_signature_is_400() {
    let _g = CHIP_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_chip()),
        ),
    )
    .await;
    assert!(st.is_success());
    let (st, body) = call(
        app,
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/chip/t1")
            .header("Content-Type", "application/json")
            .body(Body::from("{}"))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "invalid signature");
}
