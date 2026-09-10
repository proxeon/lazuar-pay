//! Live `/v1` through pay-client against Fake One (035/02). Docker Postgres 16.

mod support;

use pay_client::{Client, Config, Error};
use rust_decimal::Decimal;
use support::serve;

fn machine(base: &str) -> Client {
    Client::new(Config::new(base, "lzr_sk_test", Some("t1".into())).unwrap()).unwrap()
}

#[tokio::test]
async fn whoami_and_ready() {
    let (base, _h) = serve().await;
    let c = machine(&base);
    let me = c.whoami().await.unwrap();
    assert_eq!(me["user_id"], "k1");
    assert_eq!(me["active_org_id"], "t1");
    let ready = c.ready().await.unwrap();
    assert_eq!(ready["org_id"], "t1");
    assert_eq!(ready["ready"], true);
}

#[tokio::test]
async fn checkout_create_get_wire_status_open() {
    let (base, _h) = serve().await;
    let c = machine(&base);
    let amount = Decimal::from_str_exact("10.00").unwrap();
    let created = c
        .checkout_create("test", amount, "MYR", Some("cli-idem-1"))
        .await
        .unwrap();
    assert_eq!(created["org_id"], "t1");
    assert_eq!(created["provider"], "test");
    assert_eq!(created["currency"], "MYR");
    assert_eq!(created["status"], "open");
    assert_ne!(created["status"], "settled");
    assert!(created["amount"].is_number(), "{}", created["amount"]);
    assert_eq!(created["amount"], 10);
    let pay_url = created["pay_url"].as_str().unwrap();
    assert!(pay_url.contains("/c/"), "{pay_url}");
    let id = created["id"].as_str().unwrap();
    assert_eq!(id.len(), 32);

    let got = c.checkout_get(id).await.unwrap();
    assert_eq!(got["id"], id);
    assert_eq!(got["status"], "open");
    assert_eq!(got["pay_url"], created["pay_url"]);
}

#[tokio::test]
async fn checkout_idempotent_replay() {
    let (base, _h) = serve().await;
    let c = machine(&base);
    let amount = Decimal::from_str_exact("10.00").unwrap();
    let a = c
        .checkout_create("test", amount, "MYR", Some("same-key"))
        .await
        .unwrap();
    let b = c
        .checkout_create("test", amount, "MYR", Some("same-key"))
        .await
        .unwrap();
    assert_eq!(a["id"], b["id"]);
}

#[tokio::test]
async fn missing_org_is_config_error() {
    let (base, _h) = serve().await;
    let c = Client::new(Config::new(&base, "lzr_sk_test", None).unwrap()).unwrap();
    let err = c.ready().await.unwrap_err();
    assert!(matches!(err, Error::Config(_)), "{err}");
}

#[tokio::test]
async fn payments_list_empty_page() {
    let (base, _h) = serve().await;
    let c = machine(&base);
    let page = c.payments_list(Some(10), None).await.unwrap();
    assert!(page["items"].is_array());
}

#[tokio::test]
async fn unknown_bearer_is_api_401() {
    let (base, _h) = serve().await;
    let c = Client::new(Config::new(&base, "lzr_sk_nope", Some("t1".into())).unwrap()).unwrap();
    let err = c.whoami().await.unwrap_err();
    match err {
        Error::Api { status, .. } => assert_eq!(status, 401),
        other => panic!("{other}"),
    }
}
