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
const RZP: &str = "rzp_test:secret";
const WH: &str = "wh_rzp";
static RAZORPAY_HTTP: Mutex<()> = Mutex::const_new(());

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

fn put_rzp() -> Value {
    json!({
        "provider": "razorpay",
        "secret": RZP,
        "webhook_secret": WH,
    })
}

fn signed_hook(body: &str) -> Request<Body> {
    let sig = rails::razorpay::sign(WH, body.as_bytes());
    Request::builder()
        .method("POST")
        .uri("/v1/webhooks/razorpay/t1")
        .header("Content-Type", "application/json")
        .header("X-Razorpay-Signature", sig)
        .body(Body::from(body.to_string()))
        .unwrap()
}

#[tokio::test]
async fn put_nocolon_is_400() {
    let _g = RAZORPAY_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app,
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(json!({
                "provider": "razorpay",
                "secret": "nocolon",
                "webhook_secret": WH,
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "secret must be key_id:key_secret");
}

#[tokio::test]
async fn put_get_last4_is_key_id_suffix() {
    let _g = RAZORPAY_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app.clone(),
        authed("PUT", "/v1/orgs/t1/gateway", "test-writer", Some(put_rzp())),
    )
    .await;
    assert!(st.is_success());
    assert_eq!(body["last4"], "test");
    assert_ne!(body["last4"], "cret");
    assert_eq!(body["currency"], "INR");
    let dumped = body.to_string();
    assert!(!dumped.contains("rzp_test:secret"));
    assert!(!dumped.contains(WH));

    let (st, got) = call(
        app.clone(),
        authed(
            "GET",
            "/v1/orgs/t1/gateway?provider=razorpay",
            "test-writer",
            None,
        ),
    )
    .await;
    assert!(st.is_success());
    assert_eq!(got["configured"], true);
    assert_eq!(got["last4"], "test");

    let (st, list) = call(
        app,
        authed("GET", "/v1/orgs/t1/gateways", "test-writer", None),
    )
    .await;
    assert!(st.is_success());
    assert!(list["processors"]
        .as_array()
        .unwrap()
        .iter()
        .any(|p| p["provider"] == "razorpay" && p["configured"] == true));
}

#[tokio::test]
async fn put_key_fields_concat() {
    let _g = RAZORPAY_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app,
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(json!({
                "provider": "razorpay",
                "key_id": "rzp_test",
                "key_secret": "secret",
                "webhook_secret": WH,
            })),
        ),
    )
    .await;
    assert!(st.is_success(), "{body}");
    assert_eq!(body["last4"], "test");
}

#[tokio::test]
async fn put_rejects_brand() {
    let _g = RAZORPAY_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app,
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(json!({
                "provider": "razorpay",
                "secret": RZP,
                "webhook_secret": WH,
                "public_merchant_id": "brand",
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(
        body["detail"],
        "public_merchant_id is not used for this provider"
    );
}

#[tokio::test]
async fn mint_without_vault_is_400() {
    let _g = RAZORPAY_HTTP.lock().await;
    let pool = pool().await;
    sqlx::query(
        "DELETE FROM pay_rs.gateway_credentials WHERE tenant_id = 't1' AND rail = 'razorpay'",
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
                "provider": "razorpay",
                "amount": 10.00,
                "currency": "INR"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "rail not configured");
}

#[tokio::test]
async fn mint_myr_is_400() {
    let _g = RAZORPAY_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, _) = call(
        app.clone(),
        authed("PUT", "/v1/orgs/t1/gateway", "test-writer", Some(put_rzp())),
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
                "provider": "razorpay",
                "amount": 10.00,
                "currency": "MYR"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert!(body["detail"].as_str().unwrap().contains("currency"));
}

#[tokio::test]
async fn put_mint_start_captured_then_link_paid_one_charge() {
    let _g = RAZORPAY_HTTP.lock().await;
    let pool = pool().await;
    let state: AppState = testing_state(pool.clone(), SECRET);
    let rzp = state.razorpay.clone();
    let app = api::router(state);

    let (st, _) = call(
        app.clone(),
        authed("PUT", "/v1/orgs/t1/gateway", "test-writer", Some(put_rzp())),
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
                "provider": "razorpay",
                "amount": 10.00,
                "currency": "INR"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED);
    assert_eq!(minted["provider"], "razorpay");
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
    assert!(rzp.last_body.lock().expect("lock").is_none());

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
    assert!(started["redirect_url"].as_str().unwrap().contains("rzp.io"));
    assert_eq!(*rzp.last_amount.lock().expect("lock"), Some(1000));
    assert_ne!(*rzp.last_amount.lock().expect("lock"), Some(10));
    assert_eq!(
        rzp.last_email.lock().expect("lock").clone().unwrap(),
        "ada@acme.test"
    );
    let idem = rzp.last_idempotency.lock().expect("lock").clone().unwrap();
    assert!(idem.starts_with("lazuar-checkout:"));

    let pid = domain::PaymentId::from_wire(&id).unwrap();
    let sid: String =
        sqlx::query_scalar("SELECT session_id FROM pay_rs.attempts WHERE payment_id = $1")
            .bind(pid.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();

    let captured = format!(
        r#"{{"event":"payment.captured","payload":{{"payment":{{"entity":{{"id":"pay_both","amount":1000,"currency":"INR","notes":{{"checkout_id":"{id}"}}}}}},"payment_link":{{"entity":{{"id":"{sid}"}}}}}}}}"#
    );
    let (st, hook) = call(app.clone(), signed_hook(&captured)).await;
    assert_eq!(st, StatusCode::OK, "{hook}");
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

    let link_paid = format!(
        r#"{{"event":"payment_link.paid","payload":{{"payment":{{"entity":{{"id":"pay_both","amount":1000,"currency":"INR","notes":{{"checkout_id":"{id}"}}}}}},"payment_link":{{"entity":{{"id":"{sid}"}}}}}}}}"#
    );
    let (st, second) = call(app.clone(), signed_hook(&link_paid)).await;
    assert_eq!(st, StatusCode::OK, "{second}");
    let charges: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(pid.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(charges, 1);

    let (st, dup) = call(app, signed_hook(&captured)).await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(dup["duplicate"], true);
}

#[tokio::test]
async fn link_paid_without_notes_joins_plink() {
    let _g = RAZORPAY_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool.clone(), SECRET));
    let (st, _) = call(
        app.clone(),
        authed("PUT", "/v1/orgs/t1/gateway", "test-writer", Some(put_rzp())),
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
                "provider": "razorpay",
                "amount": 10.00,
                "currency": "INR"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED);
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
    let pid = domain::PaymentId::from_wire(minted["id"].as_str().unwrap()).unwrap();
    let sid: String =
        sqlx::query_scalar("SELECT session_id FROM pay_rs.attempts WHERE payment_id = $1")
            .bind(pid.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    let body = format!(
        r#"{{"event":"payment_link.paid","payload":{{"payment":{{"entity":{{"id":"pay_ln","amount":1000,"currency":"INR"}}}},"payment_link":{{"entity":{{"id":"{sid}"}}}}}}}}"#
    );
    let (st, hook) = call(app.clone(), signed_hook(&body)).await;
    assert_eq!(st, StatusCode::OK, "{hook}");
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
}

#[tokio::test]
async fn expired_is_ignored() {
    let _g = RAZORPAY_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, _) = call(
        app.clone(),
        authed("PUT", "/v1/orgs/t1/gateway", "test-writer", Some(put_rzp())),
    )
    .await;
    assert!(st.is_success());
    let body = r#"{"event":"payment_link.expired","payload":{"payment_link":{"entity":{"id":"plink_x","status":"expired"}}}}"#;
    let (st, ign) = call(app, signed_hook(body)).await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(ign["ignored"], "payment_link.expired");
}

#[tokio::test]
async fn failed_fails_checkout() {
    let _g = RAZORPAY_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool.clone(), SECRET));
    let (st, _) = call(
        app.clone(),
        authed("PUT", "/v1/orgs/t1/gateway", "test-writer", Some(put_rzp())),
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
                "provider": "razorpay",
                "amount": 10.00,
                "currency": "INR"
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
    let body = format!(
        r#"{{"event":"payment.failed","payload":{{"payment":{{"entity":{{"id":"pay_fail","amount":1000,"currency":"INR","notes":{{"checkout_id":"{id}"}}}}}}}}}}"#
    );
    let (st, _) = call(app.clone(), signed_hook(&body)).await;
    assert_eq!(st, StatusCode::OK);
    let (st, pay) = call(
        app,
        Request::builder()
            .uri(format!("/v1/pay/{token}"))
            .body(Body::empty())
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(pay["status"], "failed");
}

#[tokio::test]
async fn amount_10_does_not_consume_inbound() {
    let _g = RAZORPAY_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool.clone(), SECRET));
    let (st, _) = call(
        app.clone(),
        authed("PUT", "/v1/orgs/t1/gateway", "test-writer", Some(put_rzp())),
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
                "provider": "razorpay",
                "amount": 10.00,
                "currency": "INR"
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
    let body = format!(
        r#"{{"event":"payment.captured","payload":{{"payment":{{"entity":{{"id":"pay_mis","amount":10,"currency":"INR","notes":{{"checkout_id":"{id}"}}}}}}}}}}"#
    );
    let (st, _) = call(app, signed_hook(&body)).await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    let inbound: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.inbound_events WHERE proof_id = $1",
    )
    .bind("captured:pay_mis")
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(inbound, 0);
}

#[tokio::test]
async fn empty_body_is_400() {
    let _g = RAZORPAY_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, _) = call(
        app.clone(),
        authed("PUT", "/v1/orgs/t1/gateway", "test-writer", Some(put_rzp())),
    )
    .await;
    assert!(st.is_success());
    let (st, body) = call(
        app,
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/razorpay/t1")
            .header("Content-Type", "application/json")
            .body(Body::from("  "))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "invalid signature");
}
