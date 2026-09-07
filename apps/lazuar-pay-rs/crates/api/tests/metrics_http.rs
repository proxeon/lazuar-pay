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
static METRICS: Mutex<()> = Mutex::const_new(());

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

async fn scrape(app: axum::Router, req: Request<Body>) -> (StatusCode, String) {
    let res = app.oneshot(req).await.unwrap();
    let status = res.status();
    let bytes = res.into_body().collect().await.unwrap().to_bytes();
    (status, String::from_utf8_lossy(&bytes).into_owned())
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
async fn scrape_contains_type_and_token_gate() {
    let _g = METRICS.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool.clone(), SECRET));
    let (st, body) = scrape(
        app,
        Request::builder()
            .uri("/metrics")
            .body(Body::empty())
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{body}");
    assert!(body.contains("psp_parse_outcome"), "{body}");
    assert!(body.contains("refunds_pending"), "{body}");

    let mut gated = testing_state(pool, SECRET);
    gated.metrics_token = "scrape-secret".into();
    let app = api::router(gated);
    let (st, _) = scrape(
        app.clone(),
        Request::builder()
            .uri("/metrics")
            .body(Body::empty())
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::UNAUTHORIZED);
    let (st, body) = scrape(
        app,
        Request::builder()
            .uri("/metrics")
            .header("X-Metrics-Token", "scrape-secret")
            .body(Body::empty())
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{body}");
    assert!(body.contains("psp_parse_outcome"), "{body}");
}

#[tokio::test]
async fn stripe_verify_failed_and_solana_throw() {
    let _g = METRICS.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(json!({
                "provider": "stripe",
                "secret": "sk_test_dummy",
                "webhook_secret": "whsec_test",
            })),
        ),
    )
    .await;
    assert!(st.is_success());
    let (st, _) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/stripe/t1")
            .header("Content-Type", "application/json")
            .body(Body::from("{}"))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    let (st, _) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/solana/t1")
            .header("Content-Type", "application/json")
            .body(Body::from("{}"))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    let (st, body) = scrape(
        app,
        Request::builder()
            .uri("/metrics")
            .body(Body::empty())
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert!(
        body.contains("outcome=\"verify_failed\"") && body.contains("rail=\"stripe\""),
        "{body}"
    );
    assert!(body.contains("rail=\"solana\""), "{body}");
    assert!(
        body.contains("psp_parse_outcome{outcome=\"verify_failed\",rail=\"stripe\"}")
            || body.contains("psp_parse_outcome{rail=\"stripe\",outcome=\"verify_failed\"}"),
        "{body}"
    );
    assert!(
        body.contains("psp_parse_outcome{outcome=\"verify_failed\",rail=\"solana\"}")
            || body.contains("psp_parse_outcome{rail=\"solana\",outcome=\"verify_failed\"}"),
        "{body}"
    );
}

#[tokio::test]
async fn test_rail_dedupe() {
    let _g = METRICS.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let id = mint_paid(app.clone()).await;
    let hook = json!({
        "id": "evt_metrics_dup",
        "checkout_id": id,
        "amount_total": 1000,
        "currency": "MYR"
    });
    let raw = hook.to_string();
    let sig = sign(SECRET, &raw);
    let (st, first) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/test/t1")
            .header("X-Pay-Test-Signature", &sig)
            .header("Content-Type", "application/json")
            .body(Body::from(raw.clone()))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{first}");
    let (st, second) = call(
        app.clone(),
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/test/t1")
            .header("X-Pay-Test-Signature", sig)
            .header("Content-Type", "application/json")
            .body(Body::from(raw))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{second}");
    assert_eq!(second["duplicate"], true);
    let (st, body) = scrape(
        app,
        Request::builder()
            .uri("/metrics")
            .body(Body::empty())
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert!(
        body.contains("psp_parse_outcome{outcome=\"dedupe\",rail=\"test\"}")
            || body.contains("psp_parse_outcome{rail=\"test\",outcome=\"dedupe\"}"),
        "{body}"
    );
}

#[tokio::test]
async fn chip_pending_refund_gauge() {
    let _g = METRICS.lock().await;
    let pool = pool().await;
    sqlx::query("UPDATE pay_rs.org_settings SET charges_paused = false WHERE tenant_id = 't1'")
        .execute(&pool)
        .await
        .ok();
    let app = api::router(testing_state(pool.clone(), SECRET));
    let id = mint_paid(app.clone()).await;
    let pid = domain::PaymentId::from_wire(&id).unwrap();
    sqlx::query("UPDATE pay_rs.attempts SET rail = 'chip' WHERE payment_id = $1")
        .bind(pid.as_uuid())
        .execute(&pool)
        .await
        .unwrap();
    let (st, created) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/orgs/t1/refunds",
            "test-writer",
            Some(json!({"checkout_id": id})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED, "{created}");
    assert_eq!(created["status"], "pending");
    let (st, body) = scrape(
        app,
        Request::builder()
            .uri("/metrics")
            .body(Body::empty())
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{body}");
    assert!(body.contains("refunds_pending{rail=\"chip\"}"), "{body}");
    let claimable: i64 = sqlx::query_scalar(
        r#"
        SELECT count(*)::bigint FROM pay_rs.refunds
         WHERE payment_id = $1 AND status = 'pending' AND rail = 'chip'
           AND reason = 'late_pay'
        "#,
    )
    .bind(pid.as_uuid())
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(claimable, 0);
}
