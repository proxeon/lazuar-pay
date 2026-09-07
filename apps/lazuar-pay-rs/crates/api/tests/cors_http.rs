mod support;

use api::testing_state;
use axum::body::Body;
use axum::http::header::{ACCESS_CONTROL_ALLOW_ORIGIN, ACCESS_CONTROL_REQUEST_METHOD, ORIGIN};
use axum::http::{HeaderMap, Request, StatusCode};
use support::pool;
use tower::ServiceExt;

const SECRET: &str = "test-secret";

async fn send(app: axum::Router, req: Request<Body>) -> (StatusCode, HeaderMap) {
    let res = app.oneshot(req).await.unwrap();
    (res.status(), res.headers().clone())
}

fn origin_get(uri: &str, origin: &str) -> Request<Body> {
    Request::builder()
        .uri(uri)
        .header(ORIGIN, origin)
        .body(Body::empty())
        .unwrap()
}

async fn assert_allows(app: axum::Router, origin: &str) {
    let (st, headers) = send(app, origin_get("/health", origin)).await;
    assert_eq!(st, StatusCode::OK);
    let aco = headers
        .get(ACCESS_CONTROL_ALLOW_ORIGIN)
        .and_then(|v| v.to_str().ok());
    assert_eq!(aco, Some(origin), "origin {origin}");
}

async fn assert_denies(app: axum::Router, origin: &str) {
    let (st, headers) = send(app, origin_get("/health", origin)).await;
    assert_eq!(st, StatusCode::OK);
    assert!(
        headers.get(ACCESS_CONTROL_ALLOW_ORIGIN).is_none(),
        "origin {origin} must not be allowed"
    );
}

#[tokio::test]
async fn health_allows_merchant_and_checkout_origins() {
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    assert_allows(app.clone(), "http://localhost:5178").await;
    assert_allows(app.clone(), "http://localhost:5179").await;
    assert_allows(app, "http://localhost:4179").await;
}

#[tokio::test]
async fn health_does_not_allow_ops_or_portal() {
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    assert_denies(app.clone(), "http://localhost:3003").await;
    assert_denies(app, "http://localhost:3004").await;
}

#[tokio::test]
async fn configured_origins_replace_laptop_list() {
    let pool = pool().await;
    let mut st = testing_state(pool, SECRET);
    st.cors_origins = vec!["https://checkout.example".into()];
    let app = api::router(st);
    assert_allows(app.clone(), "https://checkout.example").await;
    assert_denies(app, "http://localhost:5179").await;
}

#[tokio::test]
async fn public_pay_get_and_post_allow_checkout_origin() {
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let origin = "http://localhost:5179";
    let (st, headers) = send(app.clone(), origin_get("/v1/pay/missing", origin)).await;
    assert_eq!(st, StatusCode::NOT_FOUND);
    assert_eq!(
        headers
            .get(ACCESS_CONTROL_ALLOW_ORIGIN)
            .and_then(|v| v.to_str().ok()),
        Some(origin)
    );

    let req = Request::builder()
        .method("POST")
        .uri("/v1/pay/missing/start")
        .header(ORIGIN, origin)
        .header("Content-Type", "application/json")
        .body(Body::from(r#"{"name":"Ada"}"#))
        .unwrap();
    let (st, headers) = send(app, req).await;
    assert_eq!(st, StatusCode::NOT_FOUND);
    assert_eq!(
        headers
            .get(ACCESS_CONTROL_ALLOW_ORIGIN)
            .and_then(|v| v.to_str().ok()),
        Some(origin)
    );
}

#[tokio::test]
async fn public_pay_options_allows_checkout_denies_ops() {
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let req = Request::builder()
        .method("OPTIONS")
        .uri("/v1/pay/missing")
        .header(ORIGIN, "http://localhost:5179")
        .header(ACCESS_CONTROL_REQUEST_METHOD, "GET")
        .body(Body::empty())
        .unwrap();
    let (st, headers) = send(app.clone(), req).await;
    assert!(st.as_u16() < 300, "{st}");
    assert_eq!(
        headers
            .get(ACCESS_CONTROL_ALLOW_ORIGIN)
            .and_then(|v| v.to_str().ok()),
        Some("http://localhost:5179")
    );

    let req = Request::builder()
        .method("OPTIONS")
        .uri("/v1/pay/missing")
        .header(ORIGIN, "http://localhost:3003")
        .header(ACCESS_CONTROL_REQUEST_METHOD, "POST")
        .body(Body::empty())
        .unwrap();
    let (_st, headers) = send(app, req).await;
    assert!(headers.get(ACCESS_CONTROL_ALLOW_ORIGIN).is_none());
}
