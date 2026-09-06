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
const BP_SK: &str = "bp_sk";
const XSIG: &str = "xsig";
static BILLPLZ_HTTP: Mutex<()> = Mutex::const_new(());

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

fn put_billplz() -> Value {
    json!({
        "provider": "billplz",
        "secret": BP_SK,
        "webhook_secret": XSIG,
        "public_merchant_id": "col_1",
        "environment": "test",
    })
}

#[tokio::test]
async fn put_requires_collection() {
    let _g = BILLPLZ_HTTP.lock().await;
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
                "secret": BP_SK,
                "webhook_secret": XSIG,
                "environment": "test",
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "public_merchant_id is required");
}

#[tokio::test]
async fn put_requires_environment() {
    let _g = BILLPLZ_HTTP.lock().await;
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
                "secret": BP_SK,
                "webhook_secret": XSIG,
                "public_merchant_id": "col_1",
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "environment is required");
}

#[tokio::test]
async fn put_get_never_echoes_secrets() {
    let _g = BILLPLZ_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_billplz()),
        ),
    )
    .await;
    assert!(st.is_success());
    assert_eq!(body["last4"], "p_sk");
    assert_eq!(body["public_merchant_id"], "col_1");
    assert_eq!(body["environment"], "test");
    let dumped = body.to_string();
    assert!(!dumped.contains(BP_SK));
    assert!(!dumped.contains(XSIG));

    let (st, got) = call(
        app.clone(),
        authed(
            "GET",
            "/v1/orgs/t1/gateway?provider=billplz",
            "test-writer",
            None,
        ),
    )
    .await;
    assert!(st.is_success());
    assert_eq!(got["configured"], true);
    assert_eq!(got["public_merchant_id"], "col_1");
    let dumped = got.to_string();
    assert!(!dumped.contains(BP_SK));
    assert!(!dumped.contains(XSIG));

    let (st, list) = call(
        app,
        authed("GET", "/v1/orgs/t1/gateways", "test-writer", None),
    )
    .await;
    assert!(st.is_success());
    let processors = list["processors"].as_array().unwrap();
    assert!(processors.iter().any(|p| p["provider"] == "billplz"
        && p["configured"] == true
        && p["public_merchant_id"] == "col_1"));
}

#[tokio::test]
async fn mint_without_vault_is_400() {
    let _g = BILLPLZ_HTTP.lock().await;
    let pool = pool().await;
    sqlx::query(
        "DELETE FROM pay_rs.gateway_credentials WHERE tenant_id = 't1' AND rail = 'billplz'",
    )
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
                "provider": "billplz",
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
async fn mint_usd_is_400() {
    let _g = BILLPLZ_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_billplz()),
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
                "provider": "billplz",
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
async fn localhost_callback_is_400_without_http() {
    let _g = BILLPLZ_HTTP.lock().await;
    let pool = pool().await;
    let mut state = testing_state(pool, SECRET);
    state.public_base_url = "http://localhost:8081".into();
    let billplz = state.billplz.clone();
    let app = api::router(state);
    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_billplz()),
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
                "provider": "billplz",
                "amount": 10.00,
                "currency": "MYR"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED);
    let token = minted["public_token"].as_str().unwrap();
    let (st, body) = call(
        app,
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token}/start"))
            .header("Content-Type", "application/json")
            .body(Body::from(r#"{"email":"ada@acme.test"}"#))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "callback base not public");
    assert!(billplz.last_body.lock().expect("lock").is_none());
}

#[tokio::test]
async fn put_mint_start_form_paid_query_cannot_rebind() {
    let _g = BILLPLZ_HTTP.lock().await;
    let pool = pool().await;
    let state: AppState = testing_state(pool.clone(), SECRET);
    let billplz = state.billplz.clone();
    let app = api::router(state);

    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_billplz()),
        ),
    )
    .await;
    assert!(st.is_success());

    let mint = |app: axum::Router| async move {
        call(
            app,
            authed(
                "POST",
                "/v1/checkouts",
                "test-writer",
                Some(json!({
                    "org_id": "t1",
                    "provider": "billplz",
                    "amount": 10.00,
                    "currency": "MYR"
                })),
            ),
        )
        .await
    };
    let (st, a) = mint(app.clone()).await;
    assert_eq!(st, StatusCode::CREATED);
    assert_eq!(a["provider"], "billplz");
    assert_eq!(a["status"], "open");
    assert!(a["amount"].is_number());
    let (st, b) = mint(app.clone()).await;
    assert_eq!(st, StatusCode::CREATED);
    let id_a = a["id"].as_str().unwrap().to_string();
    let id_b = b["id"].as_str().unwrap().to_string();
    let token_a = a["public_token"].as_str().unwrap().to_string();

    let (st, miss) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token_a}/start"))
            .header("Content-Type", "application/json")
            .body(Body::from("{}"))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(miss["detail"], "email is required");
    assert!(billplz.last_body.lock().expect("lock").is_none());

    let (st, ph) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token_a}/start"))
            .header("Content-Type", "application/json")
            .body(Body::from(r#"{"email":"customer@example.com"}"#))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(ph["detail"], "email is required");
    assert!(billplz.last_body.lock().expect("lock").is_none());

    let (st, started) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token_a}/start"))
            .header("Content-Type", "application/json")
            .body(Body::from(r#"{"email":"ada@acme.test"}"#))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{started}");
    assert!(started["redirect_url"]
        .as_str()
        .unwrap()
        .contains("billplz-sandbox"));
    assert_eq!(*billplz.last_amount.lock().expect("lock"), Some(1000));
    assert_eq!(
        billplz.last_email.lock().expect("lock").clone().unwrap(),
        "ada@acme.test"
    );
    assert!(billplz
        .last_host
        .lock()
        .expect("lock")
        .clone()
        .unwrap()
        .contains("sandbox"));

    let (st, again) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token_a}/start"))
            .header("Content-Type", "application/json")
            .body(Body::from(r#"{"email":"ada@acme.test"}"#))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(again["redirect_url"], started["redirect_url"]);

    let pid = domain::PaymentId::from_wire(&id_a).unwrap();
    let sid: String =
        sqlx::query_scalar("SELECT session_id FROM pay_rs.attempts WHERE payment_id = $1")
            .bind(pid.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    let unsigned =
        format!("id={sid}&paid=true&state=paid&paid_amount=1000&currency=MYR&reference_1={id_a}");
    let form = rails::billplz::signed_body(&unsigned, XSIG);
    let (st, hook) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/webhooks/billplz/t1?checkout_id={id_b}"))
            .header("Content-Type", "application/x-www-form-urlencoded")
            .body(Body::from(form.clone()))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{hook}");
    assert_eq!(hook["ok"], true);

    let (_st, pay_a) = call(
        app.clone(),
        Request::builder()
            .uri(format!("/v1/pay/{token_a}"))
            .body(Body::empty())
            .unwrap(),
    )
    .await;
    assert_eq!(pay_a["status"], "paid");
    assert_ne!(pay_a["status"], "settled");
    let token_b = b["public_token"].as_str().unwrap();
    let (st, pay_b) = call(
        app.clone(),
        Request::builder()
            .uri(format!("/v1/pay/{token_b}"))
            .body(Body::empty())
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(pay_b["status"], "open");
    let charges: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(pid.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(charges, 1);

    let (st, dup) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/billplz/t1")
            .header("Content-Type", "application/x-www-form-urlencoded")
            .body(Body::from(form))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(dup["duplicate"], true);

    let unsigned_unpaid = format!("id={sid}&paid=false&state=due&paid_amount=0&reference_1={id_a}");
    let form = rails::billplz::signed_body(&unsigned_unpaid, XSIG);
    let (st, ign) = call(
        app,
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/billplz/t1")
            .header("Content-Type", "application/x-www-form-urlencoded")
            .body(Body::from(form))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(ign["ignored"], "unpaid");
}

#[tokio::test]
async fn paid_amount_10_does_not_consume_inbound() {
    let _g = BILLPLZ_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool.clone(), SECRET));
    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_billplz()),
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
                "provider": "billplz",
                "amount": 10.00,
                "currency": "MYR"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED);
    let id = minted["id"].as_str().unwrap().to_string();
    let token = minted["public_token"].as_str().unwrap().to_string();
    let (st, _) = call(
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
    let unsigned =
        format!("id=bill_mis&paid=true&state=paid&paid_amount=10&currency=MYR&reference_1={id}");
    let form = rails::billplz::signed_body(&unsigned, XSIG);
    let (st, _) = call(
        app,
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/billplz/t1")
            .header("Content-Type", "application/x-www-form-urlencoded")
            .body(Body::from(form))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    let inbound: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.inbound_events WHERE proof_id = $1",
    )
    .bind("paid:bill_mis")
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(inbound, 0);
}

#[tokio::test]
async fn empty_body_is_400() {
    let _g = BILLPLZ_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_billplz()),
        ),
    )
    .await;
    assert!(st.is_success());
    let (st, body) = call(
        app,
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/billplz/t1")
            .header("Content-Type", "application/x-www-form-urlencoded")
            .body(Body::from("  "))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "invalid event");
}
