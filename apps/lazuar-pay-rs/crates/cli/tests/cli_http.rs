//! CLI `run()` against Fake One. Docker Postgres 16.

mod support;

use pay_cli::{run, Cli, Parser};
use support::serve;

fn parse(args: &[&str]) -> Cli {
    Cli::try_parse_from(args).unwrap()
}

#[tokio::test]
async fn whoami_ready_checkout() {
    let (base, _h) = serve().await;
    let me = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "whoami",
    ]))
    .await
    .unwrap();
    assert_eq!(me["user_id"], "k1");

    let ready = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "ready",
    ]))
    .await
    .unwrap();
    assert_eq!(ready["ready"], true);

    let created = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "checkout",
        "create",
        "--provider",
        "test",
        "--amount",
        "10.00",
        "--currency",
        "MYR",
        "--idempotency-key",
        "cli-http-1",
    ]))
    .await
    .unwrap();
    assert_eq!(created["status"], "open");
    assert_ne!(created["status"], "settled");
    assert!(created["amount"].is_number());
    assert!(created["pay_url"].as_str().unwrap().contains("/c/"));
    let id = created["id"].as_str().unwrap().to_string();

    let got = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "checkout",
        "get",
        &id,
    ]))
    .await
    .unwrap();
    assert_eq!(got["id"], id);
    assert_eq!(got["pay_url"], created["pay_url"]);

    let pays = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "payments",
        "--limit",
        "10",
    ]))
    .await
    .unwrap();
    assert!(pays["items"].is_array());

    let rcpts = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "receipts",
        "list",
    ]))
    .await
    .unwrap();
    assert!(rcpts["items"].is_array());
}

#[tokio::test]
async fn unknown_provider_is_api_error() {
    let (base, _h) = serve().await;
    let err = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "checkout",
        "create",
        "--provider",
        "paypal",
        "--amount",
        "10",
    ]))
    .await
    .unwrap_err();
    let msg = err.to_string();
    assert!(
        msg.contains("unknown provider") || msg.contains("400"),
        "{msg}"
    );
}
