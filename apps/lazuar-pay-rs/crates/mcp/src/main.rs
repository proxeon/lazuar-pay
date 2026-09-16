//! `lazuar-pay-mcp` — MCP stdio client of TypeSpec `/v1`. Not inside `serve`.

use pay_client::{Client, Config};
use pay_mcp::handle_rpc;
use serde_json::Value;
use tokio::io::{AsyncBufReadExt, AsyncReadExt, AsyncWriteExt, BufReader};

#[tokio::main]
async fn main() {
    // tools/list works without a key; tools/call returns isError Config.
    let client = Config::from_env().ok().and_then(|c| Client::new(c).ok());

    let mut stdin = BufReader::new(tokio::io::stdin());
    let mut stdout = tokio::io::stdout();
    loop {
        let Some(msg) = read_message(&mut stdin).await else {
            break;
        };
        let Ok(req) = serde_json::from_slice::<Value>(&msg) else {
            continue;
        };
        if let Some(res) = handle_rpc(&req, client.as_ref()).await {
            if write_message(&mut stdout, &res).await.is_err() {
                break;
            }
        }
    }
}

/// MCP stdio is LSP-style `Content-Length` frames (035/01).
async fn read_message(stdin: &mut BufReader<tokio::io::Stdin>) -> Option<Vec<u8>> {
    let mut headers = String::new();
    loop {
        let mut line = String::new();
        let n = stdin.read_line(&mut line).await.ok()?;
        if n == 0 {
            return None;
        }
        if line == "\r\n" || line == "\n" {
            break;
        }
        headers.push_str(&line);
    }
    let mut len = 0usize;
    for h in headers.lines() {
        let h = h.trim();
        if let Some(rest) = h.strip_prefix("Content-Length:") {
            len = rest.trim().parse().ok()?;
        }
    }
    if len == 0 {
        return None;
    }
    let mut buf = vec![0u8; len];
    stdin.read_exact(&mut buf).await.ok()?;
    Some(buf)
}

async fn write_message(stdout: &mut tokio::io::Stdout, body: &Value) -> Result<(), std::io::Error> {
    let bytes = serde_json::to_vec(body).unwrap_or_else(|_| b"{}".to_vec());
    let header = format!("Content-Length: {}\r\n\r\n", bytes.len());
    stdout.write_all(header.as_bytes()).await?;
    stdout.write_all(&bytes).await?;
    stdout.flush().await
}
