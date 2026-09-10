//! MCP tools/call against Fake One. Docker Postgres 16.

mod support;

use pay_client::{CheckoutExtras, Client, Config};
use pay_mcp::handle_rpc;
use rust_decimal::Decimal;
use serde_json::{json, Value};
use support::serve;

fn machine(base: &str) -> Client {
    Client::new(Config::new(base, "lzr_sk_test", Some("t1".into())).unwrap()).unwrap()
}

fn tool_json(res: &Value) -> Value {
    assert_eq!(res["result"]["isError"], false, "{res}");
    serde_json::from_str(res["result"]["content"][0]["text"].as_str().unwrap()).unwrap()
}

async fn call(client: &Client, name: &str, arguments: Value) -> Value {
    let req = json!({
        "jsonrpc": "2.0",
        "id": 1,
        "method": "tools/call",
        "params": { "name": name, "arguments": arguments }
    });
    handle_rpc(&req, Some(client)).await.unwrap()
}

#[tokio::test]
async fn wait_until_paid_then_list_events() {
    let (base, _h) = serve().await;
    let c = machine(&base);
    let amount = Decimal::from_str_exact("10.00").unwrap();
    let created = c
        .checkout_create(
            "test",
            amount,
            "MYR",
            "mcp-wait-1",
            CheckoutExtras::default(),
        )
        .await
        .unwrap();
    let id = created["id"].as_str().unwrap();
    let token = created["public_token"].as_str().unwrap();

    let open = call(
        &c,
        "pay_wait_checkout",
        json!({"id": id, "until": "open", "timeout_secs": 5, "interval_ms": 50}),
    )
    .await;
    let open = tool_json(&open);
    assert_eq!(open["status"], "open");

    let res = reqwest::Client::new()
        .post(format!("{base}/v1/pay/{token}/start"))
        .header("Content-Type", "application/json")
        .body("{}")
        .send()
        .await
        .unwrap();
    assert!(res.status().is_success(), "{}", res.status());

    let paid = call(
        &c,
        "pay_wait_checkout",
        json!({"id": id, "until": "paid", "timeout_secs": 5, "interval_ms": 50}),
    )
    .await;
    let paid = tool_json(&paid);
    assert_eq!(paid["status"], "paid");
    assert_ne!(paid["status"], "settled");

    let _ = c.webhook_put("http://127.0.0.1:9/hook").await.unwrap();
    let ping = c.webhook_test().await.unwrap();
    let eid = ping["event_id"].as_str().unwrap();
    let events = call(&c, "pay_list_events", json!({"limit": 20})).await;
    let events = tool_json(&events);
    assert!(
        events["items"]
            .as_array()
            .unwrap()
            .iter()
            .any(|e| e["event_id"] == eid),
        "{events}"
    );
    let page2 = call(&c, "pay_list_events", json!({"after": eid})).await;
    let page2 = tool_json(&page2);
    assert!(
        page2["items"]
            .as_array()
            .unwrap()
            .iter()
            .all(|e| e["event_id"] != eid),
        "{page2}"
    );
}
