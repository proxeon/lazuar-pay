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
const XND_SK: &str = "xnd_sk";
const TOK: &str = "tok_1";
static XENDIT_HTTP: Mutex<()> = Mutex::const_new(());

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

fn put_xendit() -> Value {
    json!({
        "provider": "xendit",
        "secret": XND_SK,
        "webhook_secret": TOK,
    })
}

#[tokio::test]
async fn put_rejects_brand() {
    let _g = XENDIT_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app,
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(json!({
                "provider": "xendit",
                "secret": XND_SK,
                "webhook_secret": TOK,
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
async fn put_get_never_echoes_secrets() {
    let _g = XENDIT_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_xendit()),
        ),
    )
    .await;
    assert!(st.is_success());
    assert_eq!(body["last4"], "d_sk");
    assert_eq!(body["environment"], "test");
    let dumped = body.to_string();
    assert!(!dumped.contains(XND_SK));
    assert!(!dumped.contains(TOK));

    let (st, got) = call(
        app.clone(),
        authed(
            "GET",
            "/v1/orgs/t1/gateway?provider=xendit",
            "test-writer",
            None,
        ),
    )
    .await;
    assert!(st.is_success());
    assert_eq!(got["configured"], true);
    let dumped = got.to_string();
    assert!(!dumped.contains(XND_SK));
    assert!(!dumped.contains(TOK));

    let (st, list) = call(
        app,
        authed("GET", "/v1/orgs/t1/gateways", "test-writer", None),
    )
    .await;
    assert!(st.is_success());
    let processors = list["processors"].as_array().unwrap();
    assert!(processors
        .iter()
        .any(|p| p["provider"] == "xendit" && p["configured"] == true));
}

#[tokio::test]
async fn mint_without_vault_is_400() {
    let _g = XENDIT_HTTP.lock().await;
    let pool = pool().await;
    sqlx::query(
        "DELETE FROM pay_rs.gateway_credentials WHERE tenant_id = 't1' AND rail = 'xendit'",
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
                "provider": "xendit",
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
    let _g = XENDIT_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_xendit()),
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
                "provider": "xendit",
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
async fn put_mint_start_paid_then_settled() {
    let _g = XENDIT_HTTP.lock().await;
    let pool = pool().await;
    let state: AppState = testing_state(pool.clone(), SECRET);
    let xendit = state.xendit.clone();
    let app = api::router(state);

    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_xendit()),
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
                "provider": "xendit",
                "amount": 10.00,
                "currency": "MYR"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED);
    assert_eq!(minted["provider"], "xendit");
    assert_eq!(minted["status"], "open");
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
    assert!(xendit.last_body.lock().expect("lock").is_none());

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
    assert!(xendit.last_body.lock().expect("lock").is_none());

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
    let url = started["redirect_url"].as_str().unwrap();
    assert!(url.contains("checkout.xendit.co"));
    let amt = xendit.last_amount.lock().expect("lock").clone().unwrap();
    assert_ne!(amt, json!(1000));
    let amt_s = amt.to_string();
    assert!(
        amt_s == "10" || amt_s == "10.0" || amt_s == "10.00",
        "major amount, got {amt_s}"
    );
    assert_eq!(
        xendit.last_email.lock().expect("lock").clone().unwrap(),
        "ada@acme.test"
    );
    assert!(xendit
        .last_host
        .lock()
        .expect("lock")
        .clone()
        .unwrap()
        .contains("api.xendit.co"));

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

    let payload = json!({
        "id": sid,
        "status": "PAID",
        "currency": "MYR",
        "paid_amount": 10,
        "metadata": { "checkout_id": id }
    });
    let (st, hook) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/xendit/t1")
            .header("Content-Type", "application/json")
            .header("x-callback-token", TOK)
            .body(Body::from(payload.to_string()))
            .unwrap(),
    )
    .await;
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

    let settled = json!({
        "id": sid,
        "status": "SETTLED",
        "currency": "MYR",
        "paid_amount": 10,
        "metadata": { "checkout_id": id }
    });
    let (st, ign) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/xendit/t1")
            .header("Content-Type", "application/json")
            .header("x-callback-token", TOK)
            .body(Body::from(settled.to_string()))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(ign["ignored"], "settled");
    let charges: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(pid.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(charges, 1);

    let (st, dup) = call(
        app,
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/xendit/t1")
            .header("Content-Type", "application/json")
            .header("x-callback-token", TOK)
            .body(Body::from(payload.to_string()))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(dup["duplicate"], true);
}

#[tokio::test]
async fn paid_amount_1_does_not_consume_inbound() {
    let _g = XENDIT_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool.clone(), SECRET));
    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_xendit()),
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
                "provider": "xendit",
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
    let payload = json!({
        "id": "inv_mis",
        "status": "PAID",
        "currency": "MYR",
        "paid_amount": 1,
        "metadata": { "checkout_id": id }
    });
    let (st, _) = call(
        app,
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/xendit/t1")
            .header("Content-Type", "application/json")
            .header("x-callback-token", TOK)
            .body(Body::from(payload.to_string()))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    let inbound: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.inbound_events WHERE proof_id = $1",
    )
    .bind("paid:inv_mis")
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(inbound, 0);
}

#[tokio::test]
async fn empty_body_is_400() {
    let _g = XENDIT_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_xendit()),
        ),
    )
    .await;
    assert!(st.is_success());
    let (st, body) = call(
        app,
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/xendit/t1")
            .header("Content-Type", "application/json")
            .header("x-callback-token", TOK)
            .body(Body::from("  "))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "invalid event");
}
