mod support;

use api::{testing_state, testing_state_with_limit};
use axum::body::Body;
use axum::http::{Request, StatusCode};
use http_body_util::BodyExt;
use serde_json::{json, Value};
use support::{pool, sign};
use tower::ServiceExt;

const SECRET: &str = "test-secret";

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

fn get(uri: &str) -> Request<Body> {
    Request::builder().uri(uri).body(Body::empty()).unwrap()
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

#[tokio::test]
async fn health_ok() {
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(app.clone(), get("/health")).await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(body["status"], "ok");
    let (st, body) = call(app, get("/v1/health")).await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(body["status"], "ok");
}

#[tokio::test]
async fn ready_ok() {
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(app, get("/ready")).await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(body["status"], "ok");
    assert!(body.get("checks").is_none());
}

#[tokio::test]
async fn whoami_requires_bearer() {
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(app, get("/v1/whoami")).await;
    assert_eq!(st, StatusCode::UNAUTHORIZED);
    assert_eq!(body["detail"], "Missing bearer token");
}

#[tokio::test]
async fn whoami_rejects_sk_family() {
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app,
        Request::builder()
            .uri("/v1/whoami")
            .header("Authorization", "Bearer sk_test_abc")
            .body(Body::empty())
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::UNAUTHORIZED);
    assert_eq!(body["detail"], "Invalid bearer");
}

#[tokio::test]
async fn whoami_ok() {
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(app, authed("GET", "/v1/whoami", "test-writer", None)).await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(body["user_id"], "u1");
    assert_eq!(body["active_org_id"], "t1");
}

/// .NET `Mint_and_start_pays_without_keys`. SPA `?status=verifying` polls GET `/v1/pay/{token}`
/// until `paid`. Test rail has no webhook; start must Take or the buyer tab waits forever.
#[tokio::test]
async fn mint_and_start_pays_without_webhook() {
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));

    let (st, minted) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/checkouts",
            "test-writer",
            Some(json!({
                "org_id": "t1",
                "provider": "test",
                "amount": 10.00,
                "currency": "MYR"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED, "{minted}");
    let id = minted["id"].as_str().unwrap().to_string();
    let token = minted["public_token"].as_str().unwrap().to_string();

    let (st, started) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token}/start"))
            .header("Content-Type", "application/json")
            .body(Body::from(r#"{"name":"Ada"}"#))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{started}");
    let redirect = started["redirect_url"].as_str().unwrap();
    assert!(redirect.contains("status=verifying"), "{started}");
    assert!(started.get("solana_pay_url").is_none());

    let (st, pay) = call(app.clone(), get(&format!("/v1/pay/{token}"))).await;
    assert_eq!(st, StatusCode::OK, "{pay}");
    assert_eq!(pay["status"], "paid", "{pay}");
    assert_eq!(pay["provider"], "test");
    assert_eq!(pay["started"], true);

    let (st, charges) = call(
        app.clone(),
        authed("GET", "/v1/orgs/t1/payments?limit=50", "test-writer", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{charges}");
    let charge = charges["items"]
        .as_array()
        .unwrap()
        .iter()
        .find(|i| i["checkout_id"] == id)
        .expect("charge for started checkout");
    assert_eq!(charge["status"], "paid");
    assert_ne!(charge["status"], "settled");

    let (st, receipts) = call(
        app,
        authed("GET", "/v1/orgs/t1/receipts?limit=50", "test-writer", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{receipts}");
    assert!(
        receipts["items"]
            .as_array()
            .unwrap()
            .iter()
            .any(|i| i["checkout_id"] == id),
        "{receipts}"
    );
}

#[tokio::test]
async fn mint_start_webhook_paid() {
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));

    let mint_body = json!({
        "org_id": "t1",
        "provider": "test",
        "amount": 10.00,
        "currency": "MYR"
    });
    let (st, minted) = call(
        app.clone(),
        authed("POST", "/v1/checkouts", "test-writer", Some(mint_body)),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED);
    assert!(minted["amount"].is_number());
    assert_eq!(minted["status"], "open");
    assert_ne!(minted["status"], "settled");
    assert!(minted.get("attempt_id").is_none());
    assert!(minted.get("intake").is_none());
    let id = minted["id"].as_str().unwrap().to_string();
    assert_eq!(id.len(), 32);
    let token = minted["public_token"].as_str().unwrap().to_string();
    assert!(minted["pay_url"].as_str().unwrap().contains(&token));

    let (st, got) = call(
        app.clone(),
        authed("GET", &format!("/v1/checkouts/{id}"), "test-writer", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(got["status"], "open");
    assert!(got["amount"].is_number());

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
    assert!(redirect.contains("status=verifying"), "{started}");

    let (st, after_start) = call(app.clone(), get(&format!("/v1/pay/{token}"))).await;
    assert_eq!(st, StatusCode::OK, "{after_start}");
    assert_eq!(after_start["status"], "paid", "{after_start}");
    assert_eq!(after_start["provider"], "test");

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

    let (st, confirm) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token}/confirm"))
            .header("Content-Type", "application/json")
            .body(Body::from("{}"))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(confirm["detail"], "not a solana checkout");

    let hook = json!({
        "id": "evt_1",
        "checkout_id": id,
        "amount_total": 1000,
        "currency": "MYR"
    });
    let raw = hook.to_string();
    let sig = sign(SECRET, &raw);
    let (st, _) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/test/t1")
            .header("X-Pay-Test-Signature", sig)
            .header("Content-Type", "application/json")
            .body(Body::from(raw.clone()))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);

    let (st, pay) = call(app.clone(), get(&format!("/v1/pay/{token}"))).await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(pay["status"], "paid");
    assert!(pay["amount"].is_number());

    let sig2 = sign(SECRET, &raw);
    let (st, dup) = call(
        app,
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/test/t1")
            .header("X-Pay-Test-Signature", sig2)
            .header("Content-Type", "application/json")
            .body(Body::from(raw))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(dup["duplicate"], true);
}

#[tokio::test]
async fn mint_stripe_is_400() {
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app,
        authed(
            "POST",
            "/v1/checkouts",
            "test-writer",
            Some(json!({"org_id":"t1","provider":"stripe","amount":10.00})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "rail not configured");
}

#[tokio::test]
async fn mint_interval_mo_is_400() {
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, _) = call(
        app,
        authed(
            "POST",
            "/v1/checkouts",
            "test-writer",
            Some(json!({"org_id":"t1","provider":"test","amount":10.00,"interval":"mo"})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn mint_jpy_is_400() {
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, _) = call(
        app,
        authed(
            "POST",
            "/v1/checkouts",
            "test-writer",
            Some(json!({"org_id":"t1","provider":"test","amount":10.00,"currency":"JPY"})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn machine_key_is_writer() {
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, minted) = call(
        app,
        authed(
            "POST",
            "/v1/checkouts",
            "lzr_sk_test",
            Some(json!({"org_id":"t1","provider":"test","amount":10.00})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED);
    assert_eq!(minted["org_id"], "t1");
}

#[tokio::test]
async fn get_checkout_wrong_org_is_404() {
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, minted) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/checkouts",
            "test-writer",
            Some(json!({"org_id":"t1","provider":"test","amount":10.00})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED);
    let id = minted["id"].as_str().unwrap();
    let (st, body) = call(
        app,
        authed("GET", &format!("/v1/checkouts/{id}"), "test-other", None),
    )
    .await;
    assert_eq!(st, StatusCode::NOT_FOUND);
    assert_eq!(body["detail"], "Checkout not found");
}

#[tokio::test]
async fn limiter_returns_429() {
    let pool = pool().await;
    let app = api::router(testing_state_with_limit(pool, SECRET, 1));
    let uri = "/v1/pay/junk-token/start";
    let req = || {
        Request::builder()
            .method("POST")
            .uri(uri)
            .header("Content-Type", "application/json")
            .body(Body::from("{}"))
            .unwrap()
    };
    let (st, _) = call(app.clone(), req()).await;
    assert_eq!(st, StatusCode::NOT_FOUND);
    let (st, body) = call(app, req()).await;
    assert_eq!(st, StatusCode::TOO_MANY_REQUESTS);
    assert_eq!(body["detail"], "Too many start attempts");
}
