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

fn write_json(name: &str, body: &str) -> std::path::PathBuf {
    let path = std::env::temp_dir().join(format!("lazuar-pay-{name}-{}.json", std::process::id()));
    std::fs::write(&path, body).unwrap();
    path
}

#[tokio::test]
async fn gateway_put_file_stripe_and_solana() {
    let (base, _h) = serve().await;
    let stripe = write_json(
        "stripe",
        r#"{"provider":"stripe","secret":"sk_test_cli_dummy","webhook_secret":"whsec_cli_test","environment":"test"}"#,
    );
    let put = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "gateway",
        "put",
        "--file",
        stripe.to_str().unwrap(),
    ]))
    .await
    .unwrap();
    assert_eq!(put["provider"], "stripe");
    assert_eq!(put["configured"], true);
    assert_eq!(put["last4"], "ummy");
    let dumped = put.to_string();
    assert!(!dumped.contains("sk_test_cli_dummy"), "{dumped}");
    assert!(!dumped.contains("whsec_cli_test"), "{dumped}");
    let _ = std::fs::remove_file(&stripe);

    let got = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "gateway",
        "get",
        "--provider",
        "stripe",
    ]))
    .await
    .unwrap();
    assert_eq!(got["configured"], true);

    let sol = write_json(
        "solana",
        r#"{"provider":"solana","public_merchant_id":"4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU","environment":"devnet"}"#,
    );
    let put = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "gateway",
        "put",
        "--file",
        sol.to_str().unwrap(),
    ]))
    .await
    .unwrap();
    assert_eq!(put["provider"], "solana");
    assert_eq!(put["currency"], "USDC");
    assert_eq!(put["configured"], true);
    let _ = std::fs::remove_file(&sol);

    let list = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "gateway",
        "list",
    ]))
    .await
    .unwrap();
    assert!(list["processors"]
        .as_array()
        .unwrap()
        .iter()
        .any(|p| { p["provider"] == "stripe" && p["configured"] == true }));
    assert!(list["processors"]
        .as_array()
        .unwrap()
        .iter()
        .any(|p| { p["provider"] == "solana" && p["configured"] == true }));
}

#[tokio::test]
async fn gateway_put_file_solana_secret_never_sent() {
    let (base, _h) = serve().await;
    let path = write_json(
        "sol-bad",
        r#"{"provider":"solana","secret":"sk_test_should_not_leave_disk","public_merchant_id":"4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU","environment":"devnet"}"#,
    );
    let err = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "gateway",
        "put",
        "--file",
        path.to_str().unwrap(),
    ]))
    .await
    .unwrap_err();
    let msg = err.to_string();
    assert!(msg.contains("does not take an API secret"), "{msg}");
    assert!(!msg.contains("sk_test_should_not_leave_disk"), "{msg}");
    let _ = std::fs::remove_file(&path);
}
