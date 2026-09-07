mod support;

use api::testing_state;
use axum::body::Body;
use axum::http::{Request, StatusCode};
use support::pool;
use tower::ServiceExt;

const SECRET: &str = "test-secret";

fn is_printable_ascii(value: &str) -> bool {
    value.chars().all(|c| (' '..='~').contains(&c))
}

async fn echo(header: Option<&str>) -> (StatusCode, Option<String>) {
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let mut b = Request::builder().uri("/health");
    if let Some(v) = header {
        b = b.header("X-Request-Id", v);
    }
    let res = app.oneshot(b.body(Body::empty()).unwrap()).await.unwrap();
    let status = res.status();
    let echoed = res
        .headers()
        .get("X-Request-Id")
        .and_then(|v| v.to_str().ok())
        .map(str::to_string);
    (status, echoed)
}

#[tokio::test]
async fn non_ascii_sanitize_is_unit_tested() {
    assert_eq!(api::request_id::sanitize("träck-123"), "trck-123");
    assert_eq!(api::request_id::sanitize("abc\tdef\r\ninj"), "abcdefinj");
}

#[tokio::test]
async fn request_id_over_64_chars_is_capped() {
    let (status, echoed) = echo(Some(&"a".repeat(200))).await;
    assert_eq!(status, StatusCode::OK);
    assert_eq!(echoed.as_deref().map(str::len), Some(64));
}

#[tokio::test]
async fn only_invalid_bytes_falls_back() {
    let (status, echoed) = echo(Some("中文")).await;
    assert_eq!(status, StatusCode::OK);
    let id = echoed.expect("fallback");
    assert!(!id.is_empty());
    assert!(is_printable_ascii(&id));
}

#[tokio::test]
async fn missing_header_falls_back() {
    let (status, echoed) = echo(None).await;
    assert_eq!(status, StatusCode::OK);
    let id = echoed.expect("fallback");
    assert!(!id.is_empty());
    assert!(is_printable_ascii(&id));
}

#[tokio::test]
async fn clean_request_id_is_echoed_verbatim() {
    let (status, echoed) = echo(Some("evt-abc_123.X")).await;
    assert_eq!(status, StatusCode::OK);
    assert_eq!(echoed.as_deref(), Some("evt-abc_123.X"));
}

#[tokio::test]
async fn health_survives_oversized_header() {
    let (status, echoed) = echo(Some(&"x".repeat(500))).await;
    assert_eq!(status, StatusCode::OK);
    let id = echoed.expect("echo");
    assert!(is_printable_ascii(&id));
    assert_eq!(id.len(), 64);
}
