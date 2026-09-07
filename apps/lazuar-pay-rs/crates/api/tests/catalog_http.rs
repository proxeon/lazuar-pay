mod support;

use api::testing_state;
use axum::body::Body;
use axum::http::{Request, StatusCode};
use http_body_util::BodyExt;
use serde_json::{json, Value};
use support::pool;
use tokio::sync::Mutex;
use tower::ServiceExt;

const SECRET: &str = "test-secret";
static CAT: Mutex<()> = Mutex::const_new(());

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

#[tokio::test]
async fn create_product_myr_and_usd_rejected() {
    let _g = CAT.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/orgs/t1/products",
            "test-writer",
            Some(json!({"name":"Kopi","amount":10})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED, "{body}");
    assert_eq!(body["currency"], "MYR");
    let pid = body["id"].as_str().unwrap().to_string();

    let (st, body) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/orgs/t1/products",
            "test-writer",
            Some(json!({"name":"X","amount":10,"currency":"USD"})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "Bar B currency is MYR");

    let (st, _body) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/orgs/t1/products",
            "test-member",
            Some(json!({"name":"Nope","amount":10})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::FORBIDDEN);

    let (st, body) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/payment-links",
            "test-writer",
            Some(json!({
                "org_id": "t1",
                "provider": "test",
                "amount": 20,
                "product_id": pid
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST, "{body}");
    assert_eq!(body["detail"], "amount must match the catalog price");

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
                "product_id": pid
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED, "{body}");
}
