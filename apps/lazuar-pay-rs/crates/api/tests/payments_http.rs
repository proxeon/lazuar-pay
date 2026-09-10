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

async fn mint_paid(app: axum::Router) -> (String, String) {
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
    (id, token)
}

fn find_item<'a>(body: &'a Value, checkout_id: &str) -> &'a Value {
    body["items"]
        .as_array()
        .unwrap()
        .iter()
        .find(|x| x["checkout_id"] == checkout_id)
        .unwrap_or_else(|| panic!("missing {checkout_id} in {}", body))
}

#[tokio::test]
async fn list_charges_paid_not_settled() {
    let _g = MONEY.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (id, _) = mint_paid(app.clone()).await;

    let (st, body) = call(
        app.clone(),
        authed("GET", "/v1/orgs/t1/payments", "test-member", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{body}");
    let row = find_item(&body, &id);
    assert_eq!(row["status"], "paid");
    assert_ne!(row["status"], "settled");
    assert_eq!(row["checkout_id"], id);
    assert_eq!(id.len(), 32);
    assert!(row["amount"].is_number());
    assert_eq!(row["amount"], 10);
    assert_eq!(row["currency"], "MYR");
    assert_eq!(row["org_id"], "t1");
    assert!(row["id"].as_str().unwrap().len() == 32);

    let (st, body) = call(
        app.clone(),
        authed("GET", "/v1/orgs/t1/receipts", "test-member", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{body}");
    let rcpt = find_item(&body, &id);
    support::assert_issued_number("RCPT", rcpt["number"].as_str().unwrap());
    assert_eq!(rcpt["status"], "issued");
    assert_eq!(rcpt["checkout_id"], id);
    assert!(rcpt["amount"].is_number());
    let rid = rcpt["id"].as_str().unwrap().to_string();

    let (st, one) = call(
        app.clone(),
        authed(
            "GET",
            &format!("/v1/orgs/t1/receipts/{rid}"),
            "test-member",
            None,
        ),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{one}");
    assert_eq!(one["number"], rcpt["number"]);
    assert_eq!(one["checkout_id"], id);

    let (st, miss) = call(
        app.clone(),
        authed(
            "GET",
            "/v1/orgs/t1/receipts/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "test-member",
            None,
        ),
    )
    .await;
    assert_eq!(st, StatusCode::NOT_FOUND);
    assert_eq!(miss["detail"], "Receipt not found");

    let (st, forbidden) = call(
        app,
        authed("GET", "/v1/orgs/t2/receipts", "test-member", None),
    )
    .await;
    assert_eq!(st, StatusCode::FORBIDDEN);
    assert_eq!(forbidden["detail"], "Not a member");
}

#[tokio::test]
async fn payments_cursor_foreign_after_is_page_one() {
    let _g = MONEY.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (a, _) = mint_paid(app.clone()).await;
    let (b, _) = mint_paid(app.clone()).await;

    let (st, page) = call(
        app.clone(),
        authed("GET", "/v1/orgs/t1/payments?limit=1", "test-writer", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    let first_id = page["items"][0]["id"].as_str().unwrap().to_string();
    assert!(page["items"][0]["checkout_id"] == a || page["items"][0]["checkout_id"] == b);

    let (st, bogus) = call(
        app.clone(),
        authed(
            "GET",
            "/v1/orgs/t1/payments?limit=1&after=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            "test-writer",
            None,
        ),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(bogus["items"][0]["id"], first_id);

    let (st, receipts) = call(
        app.clone(),
        authed(
            "GET",
            "/v1/orgs/t1/receipts?limit=1&after=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            "test-writer",
            None,
        ),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    let (st, r1) = call(
        app,
        authed("GET", "/v1/orgs/t1/receipts?limit=1", "test-writer", None),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(receipts["items"][0]["id"], r1["items"][0]["id"]);
}
