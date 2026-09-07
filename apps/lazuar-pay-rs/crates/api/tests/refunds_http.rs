mod support;

use api::testing_state;
use axum::body::Body;
use axum::http::{Request, StatusCode};
use http_body_util::BodyExt;
use serde_json::{json, Number, Value};
use std::str::FromStr;
use support::{pool, sign};
use tokio::sync::Mutex;
use tower::ServiceExt;
use uuid::Uuid;

const SECRET: &str = "test-secret";
static MONEY: Mutex<()> = Mutex::const_new(());

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
    authed_headers(method, uri, bearer, body, &[])
}

fn authed_headers(
    method: &str,
    uri: &str,
    bearer: &str,
    body: Option<Value>,
    extra: &[(&str, &str)],
) -> Request<Body> {
    let mut b = Request::builder()
        .method(method)
        .uri(uri)
        .header("Authorization", format!("Bearer {bearer}"));
    if body.is_some() {
        b = b.header("Content-Type", "application/json");
    }
    for (k, v) in extra {
        b = b.header(*k, *v);
    }
    b.body(match body {
        Some(v) => Body::from(v.to_string()),
        None => Body::empty(),
    })
    .unwrap()
}

async fn mint_paid(app: axum::Router) -> String {
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

fn find_charge<'a>(body: &'a Value, checkout_id: &str) -> &'a Value {
    body["items"]
        .as_array()
        .unwrap()
        .iter()
        .find(|x| x["checkout_id"] == checkout_id)
        .unwrap_or_else(|| panic!("missing {checkout_id} in {body}"))
}

#[tokio::test]
async fn remainder_refund_replay_and_mismatch() {
    let _g = MONEY.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool.clone(), SECRET));
    let id = mint_paid(app.clone()).await;

    let (st, created) = call(
        app.clone(),
        authed_headers(
            "POST",
            "/v1/orgs/t1/refunds",
            "test-writer",
            Some(json!({"checkout_id": id})),
            &[("Idempotency-Key", "ref-1")],
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED, "{created}");
    assert_eq!(created["status"], "succeeded");
    assert!(created["number"].as_str().unwrap().starts_with("REF-"));
    assert!(created["amount"].is_number());
    assert_eq!(created["amount"], 10);
    assert_eq!(created["reason"], "merchant");
    let refund_id = created["id"].as_str().unwrap().to_string();

    let (st, pays) = call(
        app.clone(),
        authed("GET", "/v1/orgs/t1/payments", "test-writer", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(find_charge(&pays, &id)["status"], "refunded");

    let (st, replay) = call(
        app.clone(),
        authed_headers(
            "POST",
            "/v1/orgs/t1/refunds",
            "test-writer",
            Some(json!({"checkout_id": id})),
            &[("Idempotency-Key", "ref-1")],
        ),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{replay}");
    assert_eq!(replay["id"], refund_id);

    let (st, mismatch) = call(
        app.clone(),
        authed_headers(
            "POST",
            "/v1/orgs/t1/refunds",
            "test-writer",
            Some(json!({"checkout_id": id, "amount": 4})),
            &[("Idempotency-Key", "ref-1")],
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CONFLICT, "{mismatch}");
    assert!(mismatch["detail"]
        .as_str()
        .unwrap()
        .to_ascii_lowercase()
        .contains("idempotency"));

    let (st, listed) = call(
        app.clone(),
        authed("GET", "/v1/orgs/t1/refunds", "test-member", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{listed}");
    let row = listed["items"]
        .as_array()
        .unwrap()
        .iter()
        .find(|x| x["id"] == refund_id)
        .unwrap();
    assert_eq!(row["reason"], "merchant");
    assert_ne!(row["reason"], "over_capacity");

    let (st, _denied) = call(
        app,
        authed(
            "POST",
            "/v1/orgs/t1/refunds",
            "test-member",
            Some(json!({"checkout_id": id})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::FORBIDDEN);
    let _ = pool;
}

#[tokio::test]
async fn subcent_and_missing_charge() {
    let _g = MONEY.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool.clone(), SECRET));
    let id = mint_paid(app.clone()).await;

    for amount in ["0.001", "0.004", "4.001"] {
        let body = json!({
            "checkout_id": id,
            "amount": Number::from_str(amount).unwrap()
        });
        let (st, body) = call(
            app.clone(),
            authed("POST", "/v1/orgs/t1/refunds", "test-writer", Some(body)),
        )
        .await;
        assert_eq!(st, StatusCode::BAD_REQUEST, "{amount} {body}");
        assert!(body["detail"]
            .as_str()
            .unwrap()
            .contains("at most 2 decimal places"));
    }
    let n: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.refunds r JOIN pay_rs.payments p ON p.id = r.payment_id WHERE p.id = $1",
    )
    .bind(domain::PaymentId::from_wire(&id).unwrap().as_uuid())
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(n, 0);

    let (st, tiny) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/orgs/t1/refunds",
            "test-writer",
            Some(json!({"checkout_id": id, "amount": Number::from_str("0.01").unwrap()})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED, "{tiny}");
    assert_eq!(tiny["status"], "succeeded");
    let (st, pays) = call(
        app.clone(),
        authed("GET", "/v1/orgs/t1/payments", "test-writer", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(find_charge(&pays, &id)["status"], "partially_refunded");

    let mint_body = json!({
        "org_id": "t1",
        "provider": "test",
        "amount": 10.00,
        "currency": "MYR"
    });
    let (st, open) = call(
        app.clone(),
        authed("POST", "/v1/checkouts", "test-writer", Some(mint_body)),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED);
    let open_id = open["id"].as_str().unwrap();
    let (st, miss) = call(
        app,
        authed(
            "POST",
            "/v1/orgs/t1/refunds",
            "test-writer",
            Some(json!({"checkout_id": open_id})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::NOT_FOUND, "{miss}");
    assert_eq!(miss["detail"], "charge not found");
}

#[tokio::test]
async fn unsupported_rail_fails_closed() {
    let _g = MONEY.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool.clone(), SECRET));
    let id = mint_paid(app.clone()).await;
    let pid = domain::PaymentId::from_wire(&id).unwrap();
    sqlx::query("UPDATE pay_rs.attempts SET rail = 'solana' WHERE payment_id = $1")
        .bind(pid.as_uuid())
        .execute(&pool)
        .await
        .unwrap();

    let (st, body) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/orgs/t1/refunds",
            "test-writer",
            Some(json!({"checkout_id": id})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST, "{body}");
    assert!(body["detail"]
        .as_str()
        .unwrap()
        .contains("refund not supported"));

    let status: String = sqlx::query_scalar(
        "SELECT status FROM pay_rs.refunds WHERE payment_id = $1 ORDER BY created_at DESC LIMIT 1",
    )
    .bind(pid.as_uuid())
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(status, "failed");
    let (st, pays) = call(
        app,
        authed("GET", "/v1/orgs/t1/payments", "test-writer", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(find_charge(&pays, &id)["status"], "paid");
}

#[tokio::test]
async fn chip_pending_resolve_and_settler_skips() {
    let _g = MONEY.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool.clone(), SECRET));
    let id = mint_paid(app.clone()).await;
    let pid = domain::PaymentId::from_wire(&id).unwrap();
    sqlx::query("UPDATE pay_rs.attempts SET rail = 'chip' WHERE payment_id = $1")
        .bind(pid.as_uuid())
        .execute(&pool)
        .await
        .unwrap();
    sqlx::query(
        "INSERT INTO pay_rs.org_webhook_endpoints (tenant_id, url, secret_ciphertext)
         VALUES ('t1', 'http://127.0.0.1:9/h', $1)
         ON CONFLICT (tenant_id) DO NOTHING",
    )
    .bind(&[1u8][..])
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
    let rid = created["id"].as_str().unwrap().to_string();

    let (st, pays) = call(
        app.clone(),
        authed("GET", "/v1/orgs/t1/payments", "test-writer", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(find_charge(&pays, &id)["status"], "paid");

    let claimable: i64 = sqlx::query_scalar(
        r#"
        SELECT count(*)::bigint FROM pay_rs.refunds
         WHERE id = $1
           AND status = 'pending'
           AND reason = 'late_pay'
           AND rail = 'stripe'
        "#,
    )
    .bind(domain::RefundId::from_wire(&rid).unwrap().as_uuid())
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(claimable, 0, "settler never claims merchant CHIP");
    let still: String = sqlx::query_scalar("SELECT status FROM pay_rs.refunds WHERE id = $1")
        .bind(domain::RefundId::from_wire(&rid).unwrap().as_uuid())
        .fetch_one(&pool)
        .await
        .unwrap();
    assert_eq!(still, "pending");

    let (st, bad) = call(
        app.clone(),
        authed(
            "POST",
            &format!("/v1/orgs/t1/refunds/{rid}/resolve"),
            "test-writer",
            Some(json!({"status": "nope"})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST, "{bad}");

    let (st, resolved) = call(
        app.clone(),
        authed(
            "POST",
            &format!("/v1/orgs/t1/refunds/{rid}/resolve"),
            "test-writer",
            Some(json!({"status": "succeeded"})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{resolved}");
    assert_eq!(resolved["status"], "succeeded");

    let (st, pays) = call(
        app.clone(),
        authed("GET", "/v1/orgs/t1/payments", "test-writer", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(find_charge(&pays, &id)["status"], "refunded");

    let (st, again) = call(
        app.clone(),
        authed(
            "POST",
            &format!("/v1/orgs/t1/refunds/{rid}/resolve"),
            "test-writer",
            Some(json!({"status": "failed"})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CONFLICT, "{again}");
    assert_eq!(again["detail"], "refund is not pending");

    let n: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.org_webhook_deliveries
          WHERE tenant_id = 't1' AND event_type = 'refund.created' AND event_id = $1",
    )
    .bind(&rid)
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(n, 1);

    let (st, miss) = call(
        app,
        authed(
            "POST",
            "/v1/orgs/t1/refunds/cccccccccccccccccccccccccccccccc/resolve",
            "test-writer",
            Some(json!({"status": "succeeded"})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::NOT_FOUND);
    assert_eq!(miss["detail"], "Refund not found");
}

#[tokio::test]
async fn refunds_cursor_foreign_after_is_page_one() {
    let _g = MONEY.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let id = mint_paid(app.clone()).await;
    let (st, _) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/orgs/t1/refunds",
            "test-writer",
            Some(json!({"checkout_id": id, "amount": 1})),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED);

    let (st, page) = call(
        app.clone(),
        authed("GET", "/v1/orgs/t1/refunds?limit=1", "test-writer", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    let first = page["items"][0]["id"].clone();
    let (st, bogus) = call(
        app,
        authed(
            "GET",
            "/v1/orgs/t1/refunds?limit=1&after=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            "test-writer",
            None,
        ),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(bogus["items"][0]["id"], first);
}
