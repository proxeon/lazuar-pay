//! CLI `run()` against Fake One. Docker Postgres 16.

mod support;

use pay_cli::{run, Cli, Parser};
use serde_json::json;
use support::serve;
use tokio::sync::Mutex;

static GATE: Mutex<()> = Mutex::const_new(());

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
    let subs = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "subscription",
        "list",
    ]))
    .await
    .unwrap();
    assert_eq!(subs["items"], json!([]));

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

    let listed = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "checkout",
        "list",
        "--limit",
        "10",
    ]))
    .await
    .unwrap();
    assert!(listed["items"]
        .as_array()
        .unwrap()
        .iter()
        .any(|i| i["id"] == id));

    let pays = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "payments",
        "list",
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
        "--limit",
        "1",
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
        "--idempotency-key",
        "paypal-1",
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
    #[cfg(unix)]
    {
        use std::os::unix::fs::PermissionsExt;
        let mut p = std::fs::metadata(&path).unwrap().permissions();
        p.set_mode(0o600);
        std::fs::set_permissions(&path, p).unwrap();
    }
    path
}

#[tokio::test]
async fn gateway_put_file_stripe_and_solana() {
    let _g = GATE.lock().await;
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
    let _g = GATE.lock().await;
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

#[tokio::test]
async fn checkout_wait_open_and_timeout() {
    let (base, _h) = serve().await;
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
        "--idempotency-key",
        "wait-cli-1",
    ]))
    .await
    .unwrap();
    let id = created["id"].as_str().unwrap();
    let open = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "checkout",
        "wait",
        id,
        "--until",
        "open",
        "--timeout-secs",
        "2",
        "--interval-ms",
        "50",
    ]))
    .await
    .unwrap();
    assert_eq!(open["status"], "open");

    let err = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "checkout",
        "wait",
        id,
        "--until",
        "paid",
        "--timeout-secs",
        "1",
        "--interval-ms",
        "50",
    ]))
    .await
    .unwrap_err();
    let j = err.to_json();
    assert_eq!(j["status"], 408);
    assert_eq!(j["last"]["status"], "open");
}

#[tokio::test]
async fn payment_link_and_refund_create() {
    let (base, _h) = serve().await;
    let link = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "payment-link",
        "create",
        "--provider",
        "test",
        "--amount",
        "10.00",
        "--max-payers",
        "2",
        "--label",
        "seat",
    ]))
    .await
    .unwrap();
    assert!(link["pay_url"].as_str().unwrap().contains("/c/"));

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
        "--idempotency-key",
        "refund-cli-1",
    ]))
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

    let paid = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "checkout",
        "wait",
        id,
        "--until",
        "paid",
        "--timeout-secs",
        "5",
        "--interval-ms",
        "50",
    ]))
    .await
    .unwrap();
    assert_eq!(paid["status"], "paid");

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
        "--limit",
        "1",
    ]))
    .await
    .unwrap();
    let rid = rcpts["items"][0]["id"].as_str().unwrap();
    let one = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "receipts",
        "get",
        rid,
    ]))
    .await
    .unwrap();
    assert_eq!(one["id"], rid);
    // Host only sets next_cursor when the page is full; still send --after (036/006 #19).
    let page2 = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "receipts",
        "list",
        "--after",
        rid,
    ]))
    .await
    .unwrap();
    assert!(page2["items"].is_array());
    let pays = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "payments",
        "list",
        "--limit",
        "1",
    ]))
    .await
    .unwrap();
    let pay_after = pays["items"][0]["id"]
        .as_str()
        .or_else(|| pays["next_cursor"].as_str())
        .expect("payments page");
    let page2 = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "payments",
        "list",
        "--after",
        pay_after,
    ]))
    .await
    .unwrap();
    assert!(page2["items"].is_array());

    let refund = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "refund",
        "create",
        "--checkout",
        id,
        "--idempotency-key",
        "refund-cli-idem",
    ]))
    .await
    .unwrap();
    assert_eq!(refund["status"], "succeeded");
    assert!(refund["number"].as_str().unwrap().starts_with("REF-"));

    let links = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "payment-link",
        "list",
        "--limit",
        "10",
    ]))
    .await
    .unwrap();
    assert!(links["items"]
        .as_array()
        .unwrap()
        .iter()
        .any(|i| i["pay_url"] == link["pay_url"]));

    let refunds = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "refund",
        "list",
    ]))
    .await
    .unwrap();
    assert!(refunds["items"]
        .as_array()
        .unwrap()
        .iter()
        .any(|i| i["id"] == refund["id"]));
}

#[tokio::test]
async fn product_create_and_list() {
    let (base, _h) = serve().await;
    let created = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "product",
        "create",
        "--name",
        "Seat",
        "--amount",
        "10.00",
    ]))
    .await
    .unwrap();
    assert_eq!(created["name"], "Seat");
    let id = created["id"].as_str().unwrap();
    let page = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "product",
        "list",
    ]))
    .await
    .unwrap();
    assert!(page["items"]
        .as_array()
        .unwrap()
        .iter()
        .any(|i| i["id"] == id));
}

#[tokio::test]
async fn webhook_put_get_rotate_test() {
    let (base, _h) = serve().await;
    let put = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "webhook",
        "put",
        "--url",
        "http://127.0.0.1:9/hook",
    ]))
    .await
    .unwrap();
    assert_eq!(put["webhook_configured"], true);
    let secret = put["webhook_secret"].as_str().unwrap().to_string();
    assert!(secret.starts_with("whsec_"));

    let got = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "webhook",
        "get",
    ]))
    .await
    .unwrap();
    assert!(got.get("webhook_secret").is_none());
    assert!(!got.to_string().contains(&secret), "{got}");

    let rot = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "webhook",
        "rotate",
    ]))
    .await
    .unwrap();
    assert_ne!(rot["webhook_secret"].as_str().unwrap(), secret.as_str());

    let ping = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "webhook",
        "test",
    ]))
    .await
    .unwrap();
    assert_eq!(ping["ok"], true);
    let events = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "events",
        "list",
        "--limit",
        "20",
    ]))
    .await
    .unwrap();
    assert!(
        events["items"]
            .as_array()
            .unwrap()
            .iter()
            .any(|e| e["event_id"] == ping["event_id"]),
        "{events}"
    );
}

#[tokio::test]
async fn listen_forwards_loopback() {
    let (base, _h) = serve().await;
    let hits: std::sync::Arc<tokio::sync::Mutex<Vec<String>>> =
        std::sync::Arc::new(tokio::sync::Mutex::new(Vec::new()));
    let hits2 = hits.clone();
    let listener = tokio::net::TcpListener::bind("127.0.0.1:0").await.unwrap();
    let addr = listener.local_addr().unwrap();
    let fwd = format!("http://{addr}/hook");
    tokio::spawn(async move {
        let app = axum::Router::new().route(
            "/hook",
            axum::routing::post({
                let hits = hits2;
                move |body: String| {
                    let hits = hits.clone();
                    async move {
                        hits.lock().await.push(body);
                        "ok"
                    }
                }
            }),
        );
        let _ = axum::serve(listener, app).await;
    });
    let _ = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "webhook",
        "put",
        "--url",
        &fwd,
    ]))
    .await
    .unwrap();
    let _ = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "webhook",
        "test",
    ]))
    .await
    .unwrap();
    let out = run(parse(&[
        "lazuar-pay",
        "--base-url",
        &base,
        "--api-key",
        "lzr_sk_test",
        "--org-id",
        "t1",
        "listen",
        "--forward-to",
        &fwd,
        "--timeout-secs",
        "2",
        "--interval-ms",
        "50",
    ]))
    .await
    .unwrap();
    assert!(
        out["forwarded"].as_u64().unwrap() >= 1,
        "{out} hits={:?}",
        hits.lock().await
    );
}

fn chip_pem() -> String {
    std::fs::read_to_string(format!(
        "{}/../rails/tests/fixtures/chip/test_public.pem",
        env!("CARGO_MANIFEST_DIR")
    ))
    .expect("chip test_public.pem")
}

fn write_value(name: &str, body: serde_json::Value) -> std::path::PathBuf {
    write_json(name, &body.to_string())
}

#[tokio::test]
async fn gateway_put_file_chip_billplz_xendit_razorpay() {
    let _g = GATE.lock().await;
    let (base, _h) = serve().await;
    let pem = chip_pem();

    async fn put_file(base: &str, path: &std::path::Path, secret: &str) -> serde_json::Value {
        let put = run(parse(&[
            "lazuar-pay",
            "--base-url",
            base,
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
        .unwrap();
        let dumped = put.to_string();
        assert!(!dumped.contains(secret), "{dumped}");
        assert_eq!(put["configured"], true);
        let _ = std::fs::remove_file(path);
        put
    }

    let chip = put_file(
        &base,
        &write_value(
            "chip",
            json!({
                "provider": "chip",
                "secret": "chip_sk",
                "webhook_secret": pem,
                "public_merchant_id": "brand_1",
                "environment": "test"
            }),
        ),
        "chip_sk",
    )
    .await;
    assert_eq!(chip["provider"], "chip");
    assert!(!chip.to_string().contains("BEGIN PUBLIC KEY"));

    let bp = put_file(
        &base,
        &write_value(
            "billplz",
            json!({
                "provider": "billplz",
                "secret": "bp_sk",
                "webhook_secret": "xsig",
                "public_merchant_id": "col_1",
                "environment": "test"
            }),
        ),
        "bp_sk",
    )
    .await;
    assert_eq!(bp["provider"], "billplz");
    assert!(!bp.to_string().contains("xsig"));

    let xn = put_file(
        &base,
        &write_value(
            "xendit",
            json!({
                "provider": "xendit",
                "secret": "xnd_sk",
                "webhook_secret": "tok_1",
                "environment": "test"
            }),
        ),
        "xnd_sk",
    )
    .await;
    assert_eq!(xn["provider"], "xendit");
    assert!(!xn.to_string().contains("tok_1"));

    let rz = put_file(
        &base,
        &write_value(
            "razorpay",
            json!({
                "provider": "razorpay",
                "secret": "rzp_test:secret",
                "webhook_secret": "wh_rzp",
                "environment": "test"
            }),
        ),
        "rzp_test:secret",
    )
    .await;
    assert_eq!(rz["provider"], "razorpay");
    assert!(!rz.to_string().contains("wh_rzp"));
}
