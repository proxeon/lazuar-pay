//! Vault PUT through pay-client. Docker Postgres 16. Secrets must not appear in JSON views.

mod support;

use pay_client::{validate_gateway_put, Client, Config, Error};
use serde_json::{json, Value};
use support::serve;
use tokio::sync::Mutex;

static GATE: Mutex<()> = Mutex::const_new(());

/// On-curve pubkey (USDC devnet mint). Valid receive address for vault tests.
const SOLANA_ADDR: &str = "4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU";
const SK: &str = "sk_test_cli_dummy";
const WHSEC: &str = "whsec_cli_test";

fn machine(base: &str) -> Client {
    Client::new(Config::new(base, "lzr_sk_test", Some("t1".into())).unwrap()).unwrap()
}

fn stripe_body() -> Value {
    json!({
        "provider": "stripe",
        "secret": SK,
        "webhook_secret": WHSEC,
        "environment": "test"
    })
}

fn solana_body() -> Value {
    json!({
        "provider": "solana",
        "public_merchant_id": SOLANA_ADDR,
        "environment": "devnet"
    })
}

fn assert_no_secret(v: &Value) {
    let s = v.to_string();
    assert!(!s.contains(SK), "{s}");
    assert!(!s.contains(WHSEC), "{s}");
    assert!(v.get("secret").is_none(), "{s}");
    assert!(v.get("webhook_secret").is_none(), "{s}");
}

#[tokio::test]
async fn put_stripe_get_list_never_echo_secret() {
    let _g = GATE.lock().await;
    let (base, _h) = serve().await;
    let c = machine(&base);
    let put = c.gateway_put(stripe_body()).await.unwrap();
    assert_eq!(put["provider"], "stripe");
    assert_eq!(put["configured"], true);
    assert_eq!(put["last4"], "ummy");
    assert_eq!(put["currency"], "MYR");
    assert_no_secret(&put);

    let got = c.gateway_get("stripe").await.unwrap();
    assert_eq!(got["configured"], true);
    assert_eq!(got["last4"], "ummy");
    assert_no_secret(&got);

    let list = c.gateway_list().await.unwrap();
    let stripe = list["processors"]
        .as_array()
        .unwrap()
        .iter()
        .find(|p| p["provider"] == "stripe")
        .unwrap();
    assert_eq!(stripe["configured"], true);
    assert_no_secret(stripe);
}

#[tokio::test]
async fn put_solana_receive_address() {
    let _g = GATE.lock().await;
    let (base, _h) = serve().await;
    let c = machine(&base);
    let put = c.gateway_put(solana_body()).await.unwrap();
    assert_eq!(put["provider"], "solana");
    assert_eq!(put["configured"], true);
    assert_eq!(put["currency"], "USDC");
    assert_eq!(put["environment"], "devnet");
    assert_eq!(
        put["last4"],
        SOLANA_ADDR[SOLANA_ADDR.len() - 4..].to_string()
    );
    assert_no_secret(&put);
}

#[tokio::test]
async fn put_solana_secret_fails_before_http() {
    let err = validate_gateway_put(&json!({
        "provider": "solana",
        "secret": SK,
        "public_merchant_id": SOLANA_ADDR,
        "environment": "devnet"
    }))
    .unwrap_err();
    assert!(matches!(err, Error::Config(_)));
}

#[tokio::test]
async fn put_test_fails_before_http() {
    let err =
        Client::new(Config::new("http://127.0.0.1:9", "lzr_sk_test", Some("t1".into())).unwrap())
            .unwrap()
            .gateway_put(json!({"provider":"test","secret":"x","webhook_secret":"y"}))
            .await
            .unwrap_err();
    assert!(err.to_string().contains("test processor"), "{err}");
}
