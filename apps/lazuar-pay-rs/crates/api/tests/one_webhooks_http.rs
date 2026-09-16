mod support;

use api::{testing_state, AppState};
use axum::body::Body;
use axum::http::{Request, StatusCode};
use http_body_util::BodyExt;
use serde_json::{json, Value};
use support::pool;
use tokio::sync::Mutex;
use tower::ServiceExt;
use uuid::Uuid;
use workers::hmac::sign_v1;

const SECRET: &str = "test-secret";
const ONE: &str = "one_whsec_test";
static ONE_HTTP: Mutex<()> = Mutex::const_new(());

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

fn post_one(body: &str, secret: &str, unix: i64, event_id: Option<&str>) -> Request<Body> {
    let hex = sign_v1(secret, body.as_bytes(), unix);
    let mut b = Request::builder()
        .method("POST")
        .uri("/v1/one/webhooks")
        .header("Content-Type", "application/json")
        .header("X-Lazuar-Signature", format!("t={unix},v1={hex}"));
    if let Some(id) = event_id {
        b = b.header("X-Lazuar-Event-Id", id);
    }
    b.body(Body::from(body.to_string())).unwrap()
}

fn post_one_split(body: &str, secret: &str, unix: i64) -> Request<Body> {
    let hex = sign_v1(secret, body.as_bytes(), unix);
    Request::builder()
        .method("POST")
        .uri("/v1/one/webhooks")
        .header("Content-Type", "application/json")
        .header("X-Lazuar-Signature", format!("v1={hex}"))
        .header("X-Lazuar-Timestamp", unix.to_string())
        .body(Body::from(body.to_string()))
        .unwrap()
}

fn with_process_secret(pool: sqlx::PgPool, secret: &str) -> AppState {
    let mut st = testing_state(pool, SECRET);
    st.one_webhook_secret = secret.into();
    st
}

fn now() -> i64 {
    time::OffsetDateTime::now_utc().unix_timestamp()
}

fn del(tag: &str) -> String {
    format!("del_{tag}_{}", Uuid::new_v4().simple())
}

async fn unpause(pool: &sqlx::PgPool) {
    let _ = sqlx::query(
        "INSERT INTO pay_rs.org_settings (tenant_id, charges_paused) VALUES ('t1', false)
         ON CONFLICT (tenant_id) DO UPDATE SET charges_paused = false",
    )
    .execute(pool)
    .await;
}

#[tokio::test]
async fn suspend_and_reactivate() {
    let _g = ONE_HTTP.lock().await;
    let pool = pool().await;
    unpause(&pool).await;
    let app = api::router(with_process_secret(pool.clone(), ONE));
    let t = now();
    let id = del("1");
    let body = format!(r#"{{"id":"{id}","type":"tenant.suspended","org_id":"t1"}}"#);
    let (st, res) = call(app.clone(), post_one(&body, ONE, t, None)).await;
    assert_eq!(st, StatusCode::OK, "{res}");
    let paused: bool =
        sqlx::query_scalar("SELECT charges_paused FROM pay_rs.org_settings WHERE tenant_id = 't1'")
            .fetch_one(&pool)
            .await
            .unwrap();
    assert!(paused);

    let (st, mint) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/checkouts",
            "test-writer",
            Some(json!({
                "org_id": "t1",
                "provider": "test",
                "amount": 10,
                "currency": "MYR"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::FORBIDDEN, "{mint}");
    assert_eq!(mint["detail"], "Org charges are paused");

    let rid = del("r");
    let react = format!(r#"{{"id":"{rid}","type":"tenant.reactivated","org_id":"t1"}}"#);
    let (st, _) = call(app.clone(), post_one(&react, ONE, t, None)).await;
    assert_eq!(st, StatusCode::OK);
    let paused: bool =
        sqlx::query_scalar("SELECT charges_paused FROM pay_rs.org_settings WHERE tenant_id = 't1'")
            .fetch_one(&pool)
            .await
            .unwrap();
    assert!(!paused);
}

#[tokio::test]
async fn tenant_id_and_nested_data() {
    let _g = ONE_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(with_process_secret(pool.clone(), ONE));
    let t = now();
    let id = del("tenant");
    let body = format!(r#"{{"id":"{id}","type":"tenant.suspended","tenant_id":"t1"}}"#);
    let (st, _) = call(app.clone(), post_one(&body, ONE, t, None)).await;
    assert_eq!(st, StatusCode::OK);
    sqlx::query("UPDATE pay_rs.org_settings SET charges_paused = false WHERE tenant_id = 't1'")
        .execute(&pool)
        .await
        .unwrap();
    let nid = del("nested");
    let nested =
        format!(r#"{{"id":"{nid}","type":"tenant.suspended","data":{{"tenant_id":"t1"}}}}"#);
    let (st, _) = call(app, post_one_split(&nested, ONE, t)).await;
    assert_eq!(st, StatusCode::OK);
    unpause(&pool).await;
}

#[tokio::test]
async fn hmac_failures_and_replay() {
    let _g = ONE_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(with_process_secret(pool.clone(), ONE));
    let t = now();
    let rid = del("replay");
    let body = format!(r#"{{"id":"{rid}","type":"tenant.suspended","org_id":"t1"}}"#);
    let (st, _) = call(app.clone(), post_one(&body, ONE, t, None)).await;
    assert_eq!(st, StatusCode::OK);
    let (st, dup) = call(app.clone(), post_one(&body, ONE, t, None)).await;
    assert_eq!(st, StatusCode::OK, "{dup}");
    assert_eq!(dup["duplicate"], true);

    let hex = sign_v1(ONE, body.as_bytes(), t);
    let (st, _) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri("/v1/one/webhooks")
            .header("Content-Type", "application/json")
            .header("X-Lazuar-Signature", hex)
            .body(Body::from(body.to_string()))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::UNAUTHORIZED);

    let (st, _) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri("/v1/one/webhooks")
            .header("Content-Type", "application/json")
            .body(Body::from(body.to_string()))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::UNAUTHORIZED);

    let (st, _) = call(app.clone(), post_one(&body, ONE, t - 1000, None)).await;
    assert_eq!(st, StatusCode::UNAUTHORIZED);

    let no_id = r#"{"type":"tenant.suspended","org_id":"t1"}"#;
    let (st, miss) = call(app.clone(), post_one(no_id, ONE, t, None)).await;
    assert_eq!(st, StatusCode::BAD_REQUEST, "{miss}");
    assert_eq!(miss["detail"], "event id required");

    let hid = del("header");
    let (st, _) = call(app, post_one(no_id, ONE, t, Some(&hid))).await;
    assert_eq!(st, StatusCode::OK);
    unpause(&pool).await;
}

#[tokio::test]
async fn steal_and_process_secret() {
    let _g = ONE_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool.clone(), SECRET));
    let (st, put) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/one-webhook",
            "test-writer",
            Some(json!({"webhook_secret": "whsec_a"})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{put}");
    assert_eq!(put["webhook_configured"], true);
    assert!(!put.to_string().contains("whsec_a"));

    let (st, got) = call(
        app.clone(),
        authed("GET", "/v1/orgs/t1/one-webhook", "test-member", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(got["webhook_configured"], true);
    assert!(!got.to_string().contains("whsec_a"));

    let (st, denied) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/one-webhook",
            "test-member",
            Some(json!({"webhook_secret": "whsec_x"})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::FORBIDDEN, "{denied}");

    let (st, bad) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/one-webhook",
            "test-writer",
            Some(json!({})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST, "{bad}");

    let t = now();
    let steal_id = del("steal");
    let steal = format!(r#"{{"id":"{steal_id}","type":"tenant.suspended","org_id":"t1"}}"#);
    let (st, _) = call(app.clone(), post_one(&steal, "whsec_b", t, None)).await;
    assert_eq!(st, StatusCode::UNAUTHORIZED);
    let n: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.one_webhook_events WHERE delivery_id = $1",
    )
    .bind(&steal_id)
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(n, 0);

    let ok_id = del("a");
    let ok = format!(r#"{{"id":"{ok_id}","type":"tenant.suspended","org_id":"t1"}}"#);
    let (st, _) = call(app.clone(), post_one(&ok, "whsec_a", t, None)).await;
    assert_eq!(st, StatusCode::OK);

    let mut proc = testing_state(pool.clone(), SECRET);
    proc.one_webhook_secret = ONE.into();
    let app2 = api::router(proc);
    let stored_id = del("stored");
    let stored = format!(r#"{{"id":"{stored_id}","type":"tenant.suspended","org_id":"t1"}}"#);
    let (st, _) = call(app2.clone(), post_one(&stored, "whsec_a", t, None)).await;
    assert_eq!(st, StatusCode::UNAUTHORIZED);
    let (st, _) = call(app2, post_one(&stored, ONE, t, None)).await;
    assert_eq!(st, StatusCode::OK);
    unpause(&pool).await;
}
