//! Live `/v1` through pay-client against Fake One (035/02). Docker Postgres 16.

mod support;

use pay_client::{CheckoutExtras, Client, Config, Error};
use rust_decimal::Decimal;
use std::time::Duration;
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
        .checkout_create(
            "test",
            amount,
            "MYR",
            "cli-idem-1",
            CheckoutExtras::default(),
        )
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
    let page = c.checkout_list(Some(10), None).await.unwrap();
    assert!(page["items"]
        .as_array()
        .unwrap()
        .iter()
        .any(|i| i["id"] == id));
}

#[tokio::test]
async fn checkout_create_sends_success_and_cancel_urls() {
    let (base, _h) = serve().await;
    let c = machine(&base);
    let amount = Decimal::from_str_exact("10.00").unwrap();
    let created = c
        .checkout_create(
            "test",
            amount,
            "MYR",
            "urls-1",
            CheckoutExtras {
                success_url: Some("https://app.example/ok"),
                cancel_url: Some("https://app.example/no"),
                product_id: None,
            },
        )
        .await
        .unwrap();
    assert_eq!(created["success_url"], "https://app.example/ok");
    assert_eq!(created["cancel_url"], "https://app.example/no");
}

#[tokio::test]
async fn checkout_idempotent_replay() {
    let (base, _h) = serve().await;
    let c = machine(&base);
    let amount = Decimal::from_str_exact("10.00").unwrap();
    let a = c
        .checkout_create("test", amount, "MYR", "same-key", CheckoutExtras::default())
        .await
        .unwrap();
    let b = c
        .checkout_create("test", amount, "MYR", "same-key", CheckoutExtras::default())
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
async fn checkout_wait_until_open_is_immediate() {
    let (base, _h) = serve().await;
    let c = machine(&base);
    let amount = Decimal::from_str_exact("10.00").unwrap();
    let created = c
        .checkout_create(
            "test",
            amount,
            "MYR",
            "wait-open",
            CheckoutExtras::default(),
        )
        .await
        .unwrap();
    let id = created["id"].as_str().unwrap();
    let got = c
        .checkout_wait(
            id,
            "open",
            Duration::from_secs(2),
            Duration::from_millis(50),
        )
        .await
        .unwrap();
    assert_eq!(got["status"], "open");
}

#[tokio::test]
async fn checkout_wait_paid_times_out_while_open() {
    let (base, _h) = serve().await;
    let c = machine(&base);
    let amount = Decimal::from_str_exact("10.00").unwrap();
    let created = c
        .checkout_create(
            "test",
            amount,
            "MYR",
            "wait-paid",
            CheckoutExtras::default(),
        )
        .await
        .unwrap();
    let id = created["id"].as_str().unwrap();
    let err = c
        .checkout_wait(
            id,
            "paid",
            Duration::from_millis(200),
            Duration::from_millis(50),
        )
        .await
        .unwrap_err();
    match err {
        Error::WaitTimeout { until, last } => {
            assert_eq!(until, "paid");
            assert_eq!(last["status"], "open");
            let j = Error::WaitTimeout {
                until: until.clone(),
                last: last.clone(),
            }
            .to_json();
            assert_eq!(j["status"], 408);
        }
        other => panic!("{other}"),
    }
}

#[tokio::test]
async fn empty_idempotency_is_config() {
    let (base, _h) = serve().await;
    let c = machine(&base);
    let amount = Decimal::from_str_exact("10.00").unwrap();
    let err = c
        .checkout_create("test", amount, "MYR", "  ", CheckoutExtras::default())
        .await
        .unwrap_err();
    assert!(err.to_string().contains("idempotency-key"), "{err}");
}

#[tokio::test]
async fn refund_create_after_test_start() {
    let (base, _h) = serve().await;
    let c = machine(&base);
    let amount = Decimal::from_str_exact("10.00").unwrap();
    let created = c
        .checkout_create("test", amount, "MYR", "refund-1", CheckoutExtras::default())
        .await
        .unwrap();
    let token = created["public_token"].as_str().unwrap();
    let id = created["id"].as_str().unwrap();
    let res = reqwest::Client::new()
        .post(format!("{base}/v1/pay/{token}/start"))
        .header("Content-Type", "application/json")
        .body("{}")
        .send()
        .await
        .unwrap();
    assert!(res.status().is_success(), "{}", res.status());
    let paid = c
        .checkout_wait(
            id,
            "paid",
            Duration::from_secs(5),
            Duration::from_millis(50),
        )
        .await
        .unwrap();
    assert_eq!(paid["status"], "paid");
    let refund = c.refund_create(id, None, "refund-idem-1").await.unwrap();
    assert_eq!(refund["status"], "succeeded");
    assert_eq!(refund["reason"], "merchant");
    assert!(refund["number"].as_str().unwrap().starts_with("REF-"));

    let rcpts = c.receipts_list(Some(1), None).await.unwrap();
    let items = rcpts["items"].as_array().unwrap();
    assert!(!items.is_empty(), "{rcpts}");
    let rid = items[0]["id"].as_str().unwrap();
    let one = c.receipts_get(rid).await.unwrap();
    assert_eq!(one["id"], rid);
    if let Some(after) = rcpts["next_cursor"].as_str() {
        let page2 = c.receipts_list(Some(1), Some(after)).await.unwrap();
        assert!(page2["items"].is_array());
    }
    let pays = c.payments_list(Some(1), None).await.unwrap();
    if let Some(after) = pays["next_cursor"].as_str() {
        let page2 = c.payments_list(Some(1), Some(after)).await.unwrap();
        assert!(page2["items"].is_array());
    }
}

#[tokio::test]
async fn payment_link_create_test_rail() {
    let (base, _h) = serve().await;
    let c = machine(&base);
    let amount = Decimal::from_str_exact("10.00").unwrap();
    let link = c
        .payment_link_create("test", amount, "MYR", Some(3), false, Some("seat"), None)
        .await
        .unwrap();
    assert_eq!(link["provider"], "test");
    assert!(link["pay_url"].as_str().unwrap().contains("/c/"));
    assert_eq!(link["unlimited"], false);
    let page = c.payment_link_list(Some(10), None).await.unwrap();
    assert!(page["items"]
        .as_array()
        .unwrap()
        .iter()
        .any(|i| i["id"] == link["id"]));
}

#[tokio::test]
async fn webhook_put_get_rotate_test_never_echo_on_get() {
    let (base, _h) = serve().await;
    let c = machine(&base);
    let put = c.webhook_put("http://127.0.0.1:9/hook").await.unwrap();
    assert_eq!(put["webhook_configured"], true);
    let secret = put["webhook_secret"].as_str().unwrap().to_string();
    assert!(secret.starts_with("whsec_"), "{secret}");

    let got = c.webhook_get().await.unwrap();
    assert_eq!(got["webhook_configured"], true);
    assert!(got.get("webhook_secret").is_none(), "{got}");
    assert!(!got.to_string().contains(&secret), "{got}");

    let rot = c.webhook_rotate().await.unwrap();
    let secret2 = rot["webhook_secret"].as_str().unwrap().to_string();
    assert_ne!(secret2, secret);
    assert!(secret2.starts_with("whsec_"));

    let ping = c.webhook_test().await.unwrap();
    assert_eq!(ping["ok"], true);
    assert!(
        ping["event_id"].as_str().unwrap().starts_with("test-"),
        "{ping}"
    );
}

#[tokio::test]
async fn product_create_list_then_payment_link() {
    let (base, _h) = serve().await;
    let c = machine(&base);
    let amount = Decimal::from_str_exact("10.00").unwrap();
    let product = c
        .product_create("Seat", amount, "MYR", Some("row A"))
        .await
        .unwrap();
    assert_eq!(product["name"], "Seat");
    assert_eq!(product["currency"], "MYR");
    let pid = product["id"].as_str().unwrap();
    let page = c.product_list(Some(10), None).await.unwrap();
    assert!(page["items"]
        .as_array()
        .unwrap()
        .iter()
        .any(|i| i["id"] == pid));
    let link = c
        .payment_link_create("test", amount, "MYR", Some(1), false, None, Some(pid))
        .await
        .unwrap();
    assert!(link["pay_url"].as_str().unwrap().contains("/c/"));
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
