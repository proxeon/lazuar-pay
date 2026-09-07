mod support;

use api::testing_state;
use axum::body::Body;
use axum::http::{Request, StatusCode};
use http_body_util::BodyExt;
use rails::solana::{encode, sample_address, tx_json, DEVNET_MINT};
use serde_json::{json, Value};
use support::pool;
use tokio::sync::Mutex;
use tower::ServiceExt;

const SECRET: &str = "test-secret";
static SOLANA_HTTP: Mutex<()> = Mutex::const_new(());

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

fn put_ok(address: &str) -> Value {
    json!({
        "provider": "solana",
        "public_merchant_id": address,
        "environment": "devnet"
    })
}

fn reference_from(url: &str) -> String {
    for part in url.split('?').nth(1).unwrap_or("").split('&') {
        let mut kv = part.splitn(2, '=');
        if kv.next() == Some("reference") {
            return kv.next().unwrap_or("").to_string();
        }
    }
    panic!("reference missing");
}

#[tokio::test]
async fn put_rejects_secret() {
    let _g = SOLANA_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app,
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(json!({
                "provider": "solana",
                "secret": "x",
                "webhook_secret": "y",
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "solana does not take an API secret");
}

#[tokio::test]
async fn put_get_list_usdc() {
    let _g = SOLANA_HTTP.lock().await;
    let pool = pool().await;
    let address = sample_address();
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_ok(&address)),
        ),
    )
    .await;
    assert!(st.is_success(), "{body}");
    assert_eq!(body["last4"], address[address.len() - 4..].to_string());
    assert_eq!(body["currency"], "USDC");
    assert!(!body.to_string().contains("secret"));

    let (st, got) = call(
        app.clone(),
        authed(
            "GET",
            "/v1/orgs/t1/gateway?provider=solana",
            "test-writer",
            None,
        ),
    )
    .await;
    assert!(st.is_success());
    assert_eq!(got["configured"], true);
    assert_eq!(got["public_merchant_id"], address);

    let (st, list) = call(
        app,
        authed("GET", "/v1/orgs/t1/gateways", "test-writer", None),
    )
    .await;
    assert!(st.is_success());
    assert!(list["processors"]
        .as_array()
        .unwrap()
        .iter()
        .any(|p| p["provider"] == "solana" && p["configured"] == true && p["currency"] == "USDC"));
}

#[tokio::test]
async fn put_cluster_mismatch_and_bad_address() {
    let _g = SOLANA_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(json!({
                "provider": "solana",
                "public_merchant_id": sample_address(),
                "environment": "mainnet"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "solana cluster mismatch");

    let (st, body) = call(
        app,
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(json!({
                "provider": "solana",
                "public_merchant_id": "not-an-address",
                "environment": "devnet"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(
        body["detail"],
        "public_merchant_id must be a Solana wallet address"
    );
}

#[tokio::test]
async fn mint_currency_and_vault_gates() {
    let _g = SOLANA_HTTP.lock().await;
    let pool = pool().await;
    sqlx::query(
        "DELETE FROM pay_rs.gateway_credentials WHERE tenant_id = 't1' AND rail = 'solana'",
    )
    .execute(&pool)
    .await
    .unwrap();
    let app = api::router(testing_state(pool.clone(), SECRET));
    let (st, body) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/checkouts",
            "test-writer",
            Some(json!({
                "org_id": "t1",
                "provider": "solana",
                "amount": 10,
                "currency": "USDC"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "rail not configured");

    let address = sample_address();
    let (st, _) = call(
        app.clone(),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_ok(&address)),
        ),
    )
    .await;
    assert!(st.is_success());

    let (st, body) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/checkouts",
            "test-writer",
            Some(json!({
                "org_id": "t1",
                "provider": "solana",
                "amount": 10,
                "currency": "MYR"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "solana does not capture ringgit");

    let (st, body) = call(
        app.clone(),
        authed(
            "POST",
            "/v1/checkouts",
            "test-writer",
            Some(json!({
                "org_id": "t1",
                "provider": "solana",
                "amount": 10,
                "currency": "USD"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "solana receives USDC, not USD");

    let (st, body) = call(
        app,
        authed(
            "POST",
            "/v1/checkouts",
            "test-writer",
            Some(json!({
                "org_id": "t1",
                "provider": "solana",
                "amount": 10,
                "currency": "USDC"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED, "{body}");
    assert_eq!(body["provider"], "solana");
    assert_eq!(body["currency"], "USDC");
    assert_eq!(body["status"], "open");
}

#[tokio::test]
async fn start_confirm_paid_replay_mismatch() {
    let _g = SOLANA_HTTP.lock().await;
    let pool = pool().await;
    let address = sample_address();
    let state = testing_state(pool.clone(), SECRET);
    let (st, _) = call(
        api::router(state.clone()),
        authed(
            "PUT",
            "/v1/orgs/t1/gateway",
            "test-writer",
            Some(put_ok(&address)),
        ),
    )
    .await;
    assert!(st.is_success());
    let (st, mint) = call(
        api::router(state.clone()),
        authed(
            "POST",
            "/v1/checkouts",
            "test-writer",
            Some(json!({
                "org_id": "t1",
                "provider": "solana",
                "amount": 10,
                "currency": "USDC"
            })),
        ),
    )
    .await;
    assert_eq!(st, StatusCode::CREATED, "{mint}");
    let token = mint["public_token"].as_str().unwrap().to_string();
    let checkout_id = mint["id"].as_str().unwrap().to_string();

    let (st, started) = call(
        api::router(state.clone()),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token}/start"))
            .header("Content-Type", "application/json")
            .body(Body::from(json!({"name":"Ada"}).to_string()))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{started}");
    assert!(started.get("redirect_url").is_none());
    let url = started["solana_pay_url"].as_str().unwrap();
    assert!(url.starts_with(&format!("solana:{address}")));
    assert!(url.contains("amount=10"));
    assert!(!url.contains("amount=10000000"));
    assert!(url.contains(&format!("spl-token={DEVNET_MINT}")));
    let reference = reference_from(url);

    let n: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.reservations WHERE locator = $1")
            .bind(&reference)
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(n, 1);

    let (st, again) = call(
        api::router(state.clone()),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token}/start"))
            .header("Content-Type", "application/json")
            .body(Body::from(json!({"name":"Ada"}).to_string()))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(again["solana_pay_url"], started["solana_pay_url"]);

    let (st, pay) = call(
        api::router(state.clone()),
        Request::builder()
            .method("GET")
            .uri(format!("/v1/pay/{token}"))
            .body(Body::empty())
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(pay["status"], "open");
    assert_eq!(pay["email_required"], false);
    assert!(pay["redirect_url"].is_null());
    assert_eq!(pay["solana_pay_url"], started["solana_pay_url"]);
    assert_eq!(pay["solana_cluster"], "devnet");

    let signature = encode(&[9u8; 64]);
    state.solana.set_tx(
        &signature,
        &tx_json(
            &signature,
            &address,
            DEVNET_MINT,
            "10000000",
            &reference,
            &checkout_id,
        ),
    );

    let (st, conf) = call(
        api::router(state.clone()),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token}/confirm"))
            .header("Content-Type", "application/json")
            .body(Body::from(json!({"signature": signature}).to_string()))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK, "{conf}");
    assert_eq!(conf["ok"], true);

    let (st, pay) = call(
        api::router(state.clone()),
        Request::builder()
            .method("GET")
            .uri(format!("/v1/pay/{token}"))
            .body(Body::empty())
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(pay["status"], "paid");
    assert_ne!(pay["status"], "settled");
    let pid: uuid::Uuid =
        sqlx::query_scalar("SELECT id FROM pay_rs.payments WHERE public_token = $1")
            .bind(&token)
            .fetch_one(&pool)
            .await
            .unwrap();
    let charges: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(pid)
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(charges, 1);

    let (st, dup) = call(
        api::router(state.clone()),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token}/confirm"))
            .header("Content-Type", "application/json")
            .body(Body::from(json!({"signature": signature}).to_string()))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::OK);
    assert_eq!(dup["duplicate"], true);

    let bad = encode(&[8u8; 64]);
    state.solana.set_tx(
        &bad,
        &tx_json(
            &bad,
            &address,
            rails::solana::MAINNET_MINT,
            "10000000",
            &reference,
            &checkout_id,
        ),
    );
    let (st, mm) = call(
        api::router(state),
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token}/confirm"))
            .header("Content-Type", "application/json")
            .body(Body::from(json!({"signature": bad}).to_string()))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(mm["detail"], "mint mismatch");
}

#[tokio::test]
async fn webhook_throws() {
    let _g = SOLANA_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
    let (st, body) = call(
        app,
        Request::builder()
            .method("POST")
            .uri("/v1/webhooks/solana/t1")
            .header("Content-Type", "application/json")
            .body(Body::from("{}"))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "solana does not use inbound PSP webhooks");
}

#[tokio::test]
async fn confirm_non_solana_is_400() {
    let _g = SOLANA_HTTP.lock().await;
    let pool = pool().await;
    let app = api::router(testing_state(pool, SECRET));
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
    assert_eq!(st, StatusCode::CREATED, "{mint}");
    let token = mint["public_token"].as_str().unwrap();
    let (st, body) = call(
        app,
        Request::builder()
            .method("POST")
            .uri(format!("/v1/pay/{token}/confirm"))
            .header("Content-Type", "application/json")
            .body(Body::from(json!({"signature": "x"}).to_string()))
            .unwrap(),
    )
    .await;
    assert_eq!(st, StatusCode::BAD_REQUEST);
    assert_eq!(body["detail"], "not a solana checkout");
}
