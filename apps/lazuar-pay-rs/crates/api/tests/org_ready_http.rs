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
static READY: Mutex<()> = Mutex::const_new(());

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

fn authed(method: &str, uri: &str, bearer: &str) -> Request<Body> {
    Request::builder()
        .method(method)
        .uri(uri)
        .header("Authorization", format!("Bearer {bearer}"))
        .body(Body::empty())
        .unwrap()
}

#[tokio::test]
async fn testing_ready_without_vault_and_paused_false() {
    let _g = READY.lock().await;
    let pool = pool().await;
    sqlx::query("DELETE FROM pay_rs.org_settings WHERE tenant_id = 't1'")
        .execute(&pool)
        .await
        .ok();
    let app = api::router(testing_state(pool.clone(), SECRET));
    let (st, body) = call(
        app.clone(),
        authed("GET", "/v1/orgs/t1/ready", "test-member"),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{body}");
    assert_eq!(body["org_id"], "t1");
    assert_eq!(body["ready"], true);

    sqlx::query(
        "INSERT INTO pay_rs.org_settings (tenant_id, charges_paused) VALUES ('t1', true)
         ON CONFLICT (tenant_id) DO UPDATE SET charges_paused = true",
    )
    .execute(&pool)
    .await
    .unwrap();
    let (st, body) = call(
        app.clone(),
        authed("GET", "/v1/orgs/t1/ready", "test-member"),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{body}");
    assert_eq!(body["ready"], false);

    let (st, host) = call(
        app.clone(),
        Request::builder()
            .uri("/ready")
            .body(Body::empty())
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(host["status"], "ok");

    let (st, forbidden) = call(app, authed("GET", "/v1/orgs/t2/ready", "test-member")).await;
    assert_eq!(st, StatusCode::FORBIDDEN, "{forbidden}");
    sqlx::query("UPDATE pay_rs.org_settings SET charges_paused = false WHERE tenant_id = 't1'")
        .execute(&pool)
        .await
        .ok();
}
