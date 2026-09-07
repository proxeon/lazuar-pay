mod support;

use api::testing_state;
use axum::body::Body;
use axum::http::{Request, StatusCode};
use http_body_util::BodyExt;
use serde_json::{json, Value};
use support::{pool, sign};
use tokio::sync::Mutex;
use tower::ServiceExt;
use uuid::Uuid;

const SECRET: &str = "test-secret";
static HOOKS: Mutex<()> = Mutex::const_new(());

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

async fn mint_paid(app: axum::Router) -> String {
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
    let (st, _) = call(
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
    let hook = json!({
        "id": format!("evt_{}", Uuid::new_v4().simple()),
        "checkout_id": id,
        "amount_total": 1000,
        "currency": "MYR"
    });
    let raw = hook.to_string();
    let sig = sign(SECRET, &raw);
    let (st, _) = call(
        app,
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/test/t1")
            .header("X-Pay-Test-Signature", sig)
            .header("Content-Type", "application/json")
            .body(Body::from(raw))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    id
}

#[tokio::test]
async fn put_get_rotate_test_no_echo() {
    let _g = HOOKS.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool.clone(), SECRET));

    let (st, put) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/webhooks",
            "test-writer",
            Some(json!({"url": "http://127.0.0.1:9/hook"})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{put}");
    let secret = put["webhook_secret"].as_str().unwrap().to_string();
    assert!(secret.starts_with("whsec_"));
    assert_eq!(put["secret_prefix"], &secret[secret.len() - 4..]);
    assert_eq!(put["webhook_configured"], true);

    let (st, got) = call(
        app.clone(),
        authed("GET", "/v1/orgs/t1/webhooks", "test-member", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{got}");
    assert_eq!(got["webhook_configured"], true);
    assert!(got.get("webhook_secret").is_none());
    assert!(!got.to_string().contains(&secret));

    let (st, denied) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/webhooks",
            "test-member",
            Some(json!({"url": "http://127.0.0.1:9/hook"})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::FORBIDDEN, "{denied}");

    let (st, rot) = call(
        app.clone(),
        authed("POST", "/v1/orgs/t1/webhooks/rotate", "test-writer", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{rot}");
    let secret2 = rot["webhook_secret"].as_str().unwrap().to_string();
    assert_ne!(secret2, secret);
    assert!(secret2.starts_with("whsec_"));

    let (st, ping) = call(
        app.clone(),
        authed("POST", "/v1/orgs/t1/webhooks/test", "test-writer", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{ping}");
    let eid = ping["event_id"].as_str().unwrap();
    assert!(eid.starts_with("test-"));
    let n: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.org_webhook_deliveries
          WHERE tenant_id = 't1' AND event_type = 'webhook.test' AND event_id = $1",
    )
    .bind(eid)
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(n, 1);
}

#[tokio::test]
async fn rotate_without_row_is_404() {
    let _g = HOOKS.lock().await;
    let pool = pool().await;
    sqlx::query("DELETE FROM pay_rs.org_webhook_endpoints WHERE tenant_id = 't1'")
        .execute(&pool)
        .await
        .unwrap();
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app.clone(),
        authed("POST", "/v1/orgs/t1/webhooks/rotate", "test-writer", None),
    )
    .await;
    assert_eq!(st, StatusCode::NOT_FOUND, "{body}");
    assert_eq!(body["detail"], "webhook endpoint not found");
    let (st, body) = call(
        app,
        authed("POST", "/v1/orgs/t1/webhooks/test", "test-writer", None),
    )
    .await;
    assert_eq!(st, StatusCode::NOT_FOUND, "{body}");
}

#[tokio::test]
async fn take_without_endpoint_has_no_delivery() {
    let _g = HOOKS.lock().await;
    let pool = pool().await;
    sqlx::query("DELETE FROM pay_rs.org_webhook_endpoints WHERE tenant_id = 't1'")
        .execute(&pool)
        .await
        .unwrap();
    let app = api::router(testing_state(pool.clone(), SECRET));
    let id = mint_paid(app).await;
    let n: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.org_webhook_deliveries
          WHERE tenant_id = 't1' AND event_id = $1",
    )
    .bind(format!("{id}:payment.completed"))
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(n, 0);
    let charges: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.charges c
          JOIN pay_rs.payments p ON p.id = c.payment_id WHERE p.id = $1",
    )
    .bind(domain::PaymentId::from_wire(&id).unwrap().as_uuid())
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(charges, 1);
}

#[tokio::test]
async fn take_with_endpoint_enqueues_completed() {
    let _g = HOOKS.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool.clone(), SECRET));
    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/webhooks",
            "test-writer",
            Some(json!({"url": "http://127.0.0.1:9/hook"})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    let id = mint_paid(app).await;
    let n: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.org_webhook_deliveries
          WHERE tenant_id = 't1' AND event_id = $1",
    )
    .bind(format!("{id}:payment.completed"))
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(n, 1);
}
