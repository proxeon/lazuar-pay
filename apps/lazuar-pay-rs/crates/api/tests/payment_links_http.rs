mod support;

use api::testing_state;
use axum::body::Body;
use axum::http::{Request, StatusCode};
use http_body_util::BodyExt;
use serde_json::{json, Value};
use support::{pool, sign};
use tokio::sync::Mutex;
use tower::ServiceExt;

const SECRET: &str = "test-secret";
static LINKS: Mutex<()> = Mutex::const_new(());

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

fn public(method: &str, uri: &str, body: Option<Value>) -> Request<Body> {
    let mut b = Request::builder().method(method).uri(uri);
    if body.is_some() {
        b = b.header("Content-Type", "application/json");
    }
    b.body(match body {
        Some(v) => Body::from(v.to_string()),
        None => Body::empty(),
    })
    .unwrap()
}

#[tokio::test]
async fn create_defaults_to_one_payer() {
    let _g = LINKS.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app,
        authed(
            "POST",
            "/v1/payment-links",
            "test-writer",
            Some(json!({
                "org_id": "t1",
                "provider": "test",
                "amount": 10
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED, "{body}");
    assert_eq!(body["max_payers"], 1);
    assert_eq!(body["unlimited"], false);
    assert_eq!(body["status"], "open");
    assert_eq!(body["taken_count"], 0);
    assert!(body["pay_url"].as_str().unwrap().contains("/c/"));
    assert_eq!(body["remaining"], 1);
}

#[tokio::test]
async fn create_unlimited_and_max_zero() {
    let _g = LINKS.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/payment-links",
            "test-writer",
            Some(json!({
                "org_id": "t1",
                "provider": "test",
                "amount": 10,
                "unlimited": true
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED, "{body}");
    assert!(body["max_payers"].is_null());
    assert_eq!(body["unlimited"], true);

    let (st, body) = call(
        app,
        authed(
            "POST",
            "/v1/payment-links",
            "test-writer",
            Some(json!({
                "org_id": "t1",
                "provider": "test",
                "amount": 10,
                "max_payers": 0
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "max_payers must be at least 1");
}

#[tokio::test]
async fn member_cannot_create_admin_can_list() {
    let _g = LINKS.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, _) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/payment-links",
            "test-member",
            Some(json!({
                "org_id": "t1",
                "provider": "test",
                "amount": 10
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::FORBIDDEN);

    let (st, created) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/payment-links",
            "test-writer",
            Some(json!({
                "org_id": "t1",
                "provider": "test",
                "amount": 10
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED, "{created}");
    let (st, list) = call(
        app,
        authed("GET", "/v1/orgs/t1/payment-links", "test-member", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert!(!list["items"].as_array().unwrap().is_empty());
}

#[tokio::test]
async fn foreign_cursor_equals_bogus() {
    let _g = LINKS.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let _ = call(
        app.clone(),
        authed(
            "POST",
            "/v1/payment-links",
            "test-writer",
            Some(json!({"org_id":"t1","provider":"test","amount":10})),
        ),
    )
    .await;
    let (st, a) = call(
        app.clone(),
        authed(
            "GET",
            "/v1/orgs/t1/payment-links?after=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "test-writer",
            None,
        ),
    )
    .await;
    let (st2, b) = call(
        app,
        authed(
            "GET",
            "/v1/orgs/t1/payment-links?after=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            "test-writer",
            None,
        ),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(st2, StatusCode::OK);
    assert_eq!(a["items"], b["items"]);
}

#[tokio::test]
async fn start_slot_resume_and_full() {
    let _g = LINKS.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool.clone(), SECRET));
    let (st, created) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/payment-links",
            "test-writer",
            Some(json!({"org_id":"t1","provider":"test","amount":10})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED, "{created}");
    let token = created["public_token"].as_str().unwrap().to_string();

    let (st, pay) = call(
        app.clone(),
        public("GET", &format!("/v1/pay/{token}"), None),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{pay}");
    assert_eq!(pay["status"], "open");
    assert_eq!(pay["mine"], false);
    assert_eq!(pay["taken_count"], 0);

    let (st, body) = call(
        app.clone(),
        public(
            "POST",
            &format!("/v1/pay/{token}/start"),
            Some(json!({"name":"Ada"})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "slot_key is required");

    let (st, started) = call(
        app.clone(),
        public(
            "POST",
            &format!("/v1/pay/{token}/start"),
            Some(json!({"name":"Ada","slot_key":"slot-key-1"})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{started}");
    assert!(started.get("redirect_url").is_some());

    let (st, again) = call(
        app.clone(),
        public(
            "POST",
            &format!("/v1/pay/{token}/start"),
            Some(json!({"name":"Ada","slot_key":"slot-key-1"})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(again["redirect_url"], started["redirect_url"]);

    let (st, mine) = call(
        app.clone(),
        public("GET", &format!("/v1/pay/{token}?slot_key=slot-key-1"), None),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(mine["mine"], true);
    assert_eq!(mine["started"], true);

    let (st, full) = call(
        app.clone(),
        public(
            "POST",
            &format!("/v1/pay/{token}/start"),
            Some(json!({"name":"Bob","slot_key":"slot-key-2"})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CONFLICT);
    assert_eq!(full["detail"], "This pay link is full");

    let (st, list) = call(
        app,
        authed("GET", "/v1/orgs/t1/checkouts", "test-writer", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    let items = list["items"].as_array().unwrap();
    assert!(items.iter().all(|i| i["public_token"] != token));
}

#[tokio::test]
async fn pay_child_already_paid_one_charge() {
    let _g = LINKS.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool.clone(), SECRET));
    let (st, created) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/payment-links",
            "test-writer",
            Some(json!({"org_id":"t1","provider":"test","amount":10})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED, "{created}");
    let token = created["public_token"].as_str().unwrap().to_string();
    let (st, started) = call(
        app.clone(),
        public(
            "POST",
            &format!("/v1/pay/{token}/start"),
            Some(json!({"slot_key":"slot-key-1"})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{started}");
    let child_id: String = sqlx::query_scalar(
        "SELECT id::text FROM pay_rs.payments WHERE payment_link_id IS NOT NULL ORDER BY created_at DESC LIMIT 1",
    )
    .fetch_one(&pool)
    .await
    .unwrap();
    let child_hex = child_id.replace('-', "");
    let hook = json!({
        "id": "evt_link_1",
        "checkout_id": child_hex,
        "amount_total": 1000,
        "currency": "MYR"
    })
    .to_string();
    let sig = sign(SECRET, &hook);
    let (st, _) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/test/t1")
            .header("Content-Type", "application/json")
            .header("X-Pay-Test-Signature", sig)
            .body(Body::from(hook))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    let (st, pay) = call(
        app.clone(),
        public("GET", &format!("/v1/pay/{token}"), None),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{pay}");
    assert_eq!(pay["status"], "already_paid");
    let charges: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.charges c JOIN pay_rs.payments p ON p.id = c.payment_id WHERE p.payment_link_id IS NOT NULL",
    )
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(charges, 1);

    let (st, sub) = call(
        app,
        authed("GET", "/v1/orgs/t1/subscriptions", "test-writer", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(sub["items"], json!([]));
}
