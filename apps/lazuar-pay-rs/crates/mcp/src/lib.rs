//! stdio MCP adapter over `pay-client` (035/01, 036/006 #24). HTTP only.
//! Eight tools. No `pay_put_gateway`. Idempotency required on mint and refund.

#![forbid(unsafe_code)]

use pay_client::{CheckoutExtras, Client, Error};
use rust_decimal::Decimal;
use serde_json::{json, Value};
use std::str::FromStr;

const PROTOCOL: &str = "2024-11-05";

/// MCP tool catalog. Keep this list short; secrets stay in env / `--file`.
pub fn tools() -> Value {
    json!([
        tool("pay_whoami", "GET /v1/whoami — prove the One key.", true, false, json!({})),
        tool("pay_ready", "GET /v1/orgs/{org}/ready — can this shop take money?", true, false, json!({})),
        tool(
            "pay_create_checkout",
            "POST /v1/checkouts. Returns id, pay_url, status (open/paid/failed/expired). Exact amount, ≤2 decimals. solana requires currency USDC. interval mo/yr is refused. Idempotency-Key required. Buyer still pays on the hosted page — do not POST /v1/pay/{token}/start.",
            false,
            false,
            json!({
                "provider": {"type": "string"},
                "amount": {"type": ["number", "string"]},
                "currency": {"type": "string", "description": "Fiat default MYR. solana = USDC."},
                "idempotency_key": {"type": "string"},
                "success_url": {"type": "string"},
                "cancel_url": {"type": "string"},
                "product_id": {"type": "string"}
            }),
        ),
        tool(
            "pay_get_checkout",
            "GET /v1/checkouts/{id}. Poll until status is paid. paid means a Proof took; no admin override.",
            true,
            false,
            json!({ "id": {"type": "string"} }),
        ),
        tool("pay_list_payments", "GET /v1/orgs/{org}/payments", true, false, json!({
            "limit": {"type": "integer"},
            "after": {"type": "string"}
        })),
        tool("pay_list_receipts", "GET /v1/orgs/{org}/receipts", true, false, json!({
            "limit": {"type": "integer"},
            "after": {"type": "string"}
        })),
        tool(
            "pay_create_refund",
            "POST /v1/orgs/{org}/refunds. Destructive. Idempotency-Key required. Solana cannot chain-refund.",
            false,
            true,
            json!({
                "checkout": {"type": "string"},
                "amount": {"type": ["number", "string"]},
                "idempotency_key": {"type": "string"}
            }),
        ),
        tool(
            "pay_create_payment_link",
            "POST /v1/payment-links. Occupancy / cap mint (SPA Pay links).",
            false,
            false,
            json!({
                "provider": {"type": "string"},
                "amount": {"type": ["number", "string"]},
                "currency": {"type": "string"},
                "max_payers": {"type": "integer"},
                "unlimited": {"type": "boolean"},
                "label": {"type": "string"},
                "product_id": {"type": "string"}
            }),
        )
    ])
}

fn tool(
    name: &str,
    description: &str,
    read_only: bool,
    destructive: bool,
    properties: Value,
) -> Value {
    let required: Vec<&str> = match name {
        "pay_create_checkout" => vec!["provider", "amount", "idempotency_key"],
        "pay_get_checkout" => vec!["id"],
        "pay_create_refund" => vec!["checkout", "idempotency_key"],
        "pay_create_payment_link" => vec!["provider", "amount"],
        _ => vec![],
    };
    json!({
        "name": name,
        "description": description,
        "inputSchema": {
            "type": "object",
            "properties": properties,
            "required": required
        },
        "annotations": {
            "readOnlyHint": read_only,
            "destructiveHint": destructive
        }
    })
}

/// JSON-RPC 2.0. Notifications (`id` missing) return `None`.
pub async fn handle_rpc(req: &Value, client: Option<&Client>) -> Option<Value> {
    let method = req.get("method").and_then(Value::as_str).unwrap_or("");
    let id = req.get("id").cloned();
    if id.is_none() {
        return None;
    }
    let result = match method {
        "initialize" => Ok(json!({
            "protocolVersion": PROTOCOL,
            "capabilities": { "tools": { "listChanged": false } },
            "serverInfo": { "name": "lazuar-pay", "version": env!("CARGO_PKG_VERSION") }
        })),
        "ping" => Ok(json!({})),
        "tools/list" => Ok(json!({ "tools": tools() })),
        "tools/call" => {
            let params = req.get("params").cloned().unwrap_or(Value::Null);
            call_tool(&params, client).await
        }
        other => Err(rpc_err(-32601, format!("unknown method: {other}"))),
    };
    Some(match result {
        Ok(r) => json!({"jsonrpc": "2.0", "id": id, "result": r}),
        Err(e) => json!({"jsonrpc": "2.0", "id": id, "error": e}),
    })
}

fn rpc_err(code: i32, message: String) -> Value {
    json!({ "code": code, "message": message })
}

async fn call_tool(params: &Value, client: Option<&Client>) -> Result<Value, Value> {
    let name = params
        .get("name")
        .and_then(Value::as_str)
        .unwrap_or("")
        .trim();
    if name == "pay_put_gateway" || name.contains("gateway") {
        return Ok(tool_err(
            "gateway PUT is not an MCP tool; use CLI gateway put --file",
        ));
    }
    let args = params.get("arguments").cloned().unwrap_or(json!({}));
    let Some(client) = client else {
        return Ok(tool_err("LAZUAR_PAY_API_KEY is required"));
    };
    let out = match name {
        "pay_whoami" => client.whoami().await,
        "pay_ready" => client.ready().await,
        "pay_create_checkout" => create_checkout(client, &args).await,
        "pay_get_checkout" => get_checkout(client, &args).await,
        "pay_list_payments" => {
            client
                .payments_list(arg_u32(&args, "limit"), arg_str(&args, "after").as_deref())
                .await
        }
        "pay_list_receipts" => {
            client
                .receipts_list(arg_u32(&args, "limit"), arg_str(&args, "after").as_deref())
                .await
        }
        "pay_create_refund" => create_refund(client, &args).await,
        "pay_create_payment_link" => create_link(client, &args).await,
        other => return Ok(tool_err(&format!("unknown tool: {other}"))),
    };
    Ok(match out {
        Ok(v) => tool_ok(&v),
        Err(e) => tool_err_json(&e),
    })
}

async fn get_checkout(client: &Client, args: &Value) -> Result<Value, Error> {
    let id = arg_str(args, "id").ok_or_else(|| Error::Config("id is required".into()))?;
    client.checkout_get(&id).await
}

async fn create_checkout(client: &Client, args: &Value) -> Result<Value, Error> {
    let provider =
        arg_str(args, "provider").ok_or_else(|| Error::Config("provider is required".into()))?;
    let amount = arg_amount(args)?;
    let currency = arg_str(args, "currency").unwrap_or_else(|| "MYR".into());
    let key = arg_str(args, "idempotency_key")
        .ok_or_else(|| Error::Config("idempotency_key is required".into()))?;
    let success_url = arg_str(args, "success_url");
    let cancel_url = arg_str(args, "cancel_url");
    let product_id = arg_str(args, "product_id");
    client
        .checkout_create(
            &provider,
            amount,
            &currency,
            &key,
            CheckoutExtras {
                success_url: success_url.as_deref(),
                cancel_url: cancel_url.as_deref(),
                product_id: product_id.as_deref(),
            },
        )
        .await
}

async fn create_refund(client: &Client, args: &Value) -> Result<Value, Error> {
    let checkout =
        arg_str(args, "checkout").ok_or_else(|| Error::Config("checkout is required".into()))?;
    let key = arg_str(args, "idempotency_key")
        .ok_or_else(|| Error::Config("idempotency_key is required".into()))?;
    let amount = match args.get("amount") {
        None | Some(Value::Null) => None,
        Some(_) => Some(arg_amount(args)?),
    };
    client.refund_create(&checkout, amount, &key).await
}

async fn create_link(client: &Client, args: &Value) -> Result<Value, Error> {
    let provider =
        arg_str(args, "provider").ok_or_else(|| Error::Config("provider is required".into()))?;
    let amount = arg_amount(args)?;
    let currency = arg_str(args, "currency").unwrap_or_else(|| "MYR".into());
    let unlimited = args
        .get("unlimited")
        .and_then(Value::as_bool)
        .unwrap_or(false);
    let label = arg_str(args, "label");
    let product_id = arg_str(args, "product_id");
    client
        .payment_link_create(
            &provider,
            amount,
            &currency,
            arg_i32(args, "max_payers"),
            unlimited,
            label.as_deref(),
            product_id.as_deref(),
        )
        .await
}

fn arg_str(args: &Value, key: &str) -> Option<String> {
    args.get(key)
        .and_then(|v| {
            v.as_str()
                .map(|s| s.to_string())
                .or_else(|| v.as_i64().map(|n| n.to_string()))
        })
        .map(|s| s.trim().to_string())
        .filter(|s| !s.is_empty())
}

fn arg_u32(args: &Value, key: &str) -> Option<u32> {
    args.get(key).and_then(Value::as_u64).map(|n| n as u32)
}

fn arg_i32(args: &Value, key: &str) -> Option<i32> {
    args.get(key).and_then(Value::as_i64).map(|n| n as i32)
}

fn arg_amount(args: &Value) -> Result<Decimal, Error> {
    let v = args
        .get("amount")
        .ok_or_else(|| Error::Config("amount is required".into()))?;
    let s = if let Some(n) = v.as_number() {
        n.to_string()
    } else if let Some(s) = v.as_str() {
        s.trim().to_string()
    } else {
        return Err(Error::Config("amount must be a decimal".into()));
    };
    Decimal::from_str(&s).map_err(|_| Error::Config("amount must be a decimal".into()))
}

fn tool_ok(v: &Value) -> Value {
    json!({
        "content": [{ "type": "text", "text": v.to_string() }],
        "isError": false
    })
}

fn tool_err(detail: &str) -> Value {
    tool_err_json(&Error::Config(detail.into()))
}

fn tool_err_json(err: &Error) -> Value {
    json!({
        "content": [{ "type": "text", "text": err.to_json().to_string() }],
        "isError": true
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn tools_list_is_eight_and_has_no_gateway() {
        let req = json!({"jsonrpc":"2.0","id":1,"method":"tools/list"});
        let res = handle_rpc(&req, None).await.unwrap();
        let tools = res["result"]["tools"].as_array().unwrap();
        assert_eq!(tools.len(), 8, "{tools:?}");
        let names: Vec<_> = tools.iter().map(|t| t["name"].as_str().unwrap()).collect();
        assert_eq!(
            names,
            [
                "pay_whoami",
                "pay_ready",
                "pay_create_checkout",
                "pay_get_checkout",
                "pay_list_payments",
                "pay_list_receipts",
                "pay_create_refund",
                "pay_create_payment_link",
            ]
        );
        assert!(!names.iter().any(|n| n.contains("gateway")));
        let create = tools
            .iter()
            .find(|t| t["name"] == "pay_create_checkout")
            .unwrap();
        let reqd = create["inputSchema"]["required"].as_array().unwrap();
        assert!(reqd.iter().any(|v| v == "idempotency_key"));
        let refund = tools
            .iter()
            .find(|t| t["name"] == "pay_create_refund")
            .unwrap();
        assert_eq!(refund["annotations"]["destructiveHint"], true);
    }

    #[tokio::test]
    async fn initialize_advertises_tools() {
        let req = json!({"jsonrpc":"2.0","id":"init","method":"initialize","params":{}});
        let res = handle_rpc(&req, None).await.unwrap();
        assert_eq!(res["result"]["protocolVersion"], PROTOCOL);
        assert_eq!(res["result"]["serverInfo"]["name"], "lazuar-pay");
    }

    #[tokio::test]
    async fn notification_has_no_response() {
        let req = json!({"jsonrpc":"2.0","method":"notifications/initialized"});
        assert!(handle_rpc(&req, None).await.is_none());
    }

    #[tokio::test]
    async fn gateway_tool_is_rejected() {
        let req = json!({
            "jsonrpc":"2.0",
            "id": 2,
            "method":"tools/call",
            "params":{"name":"pay_put_gateway","arguments":{}}
        });
        let res = handle_rpc(&req, None).await.unwrap();
        assert_eq!(res["result"]["isError"], true);
        let text = res["result"]["content"][0]["text"].as_str().unwrap();
        assert!(text.contains("not an MCP tool"), "{text}");
    }

    #[tokio::test]
    async fn create_checkout_requires_idempotency() {
        let client = Client::new(
            pay_client::Config::new("http://127.0.0.1:9", "lzr_sk_test", Some("t1".into()))
                .unwrap(),
        )
        .unwrap();
        let req = json!({
            "jsonrpc":"2.0",
            "id": 3,
            "method":"tools/call",
            "params":{
                "name":"pay_create_checkout",
                "arguments":{"provider":"test","amount":10}
            }
        });
        let res = handle_rpc(&req, Some(&client)).await.unwrap();
        assert_eq!(res["result"]["isError"], true);
        let text = res["result"]["content"][0]["text"].as_str().unwrap();
        assert!(text.contains("idempotency_key"), "{text}");
    }
}
