//! Port of `SolanaRpc.cs` — JSON-RPC over the Transport seam
//! (`getTransaction`, `getSignaturesForAddress`, `getGenesisHash`), pinned to
//! `finalized` commitment everywhere.

use serde_json::{json, Value};

use crate::transport::{OutRequest, Transport};

#[derive(Debug, thiserror::Error)]
pub enum SolanaRpcError {
    #[error("solana RPC throttled")]
    Throttled,
    #[error("{0}")]
    InvalidOperation(String),
}

pub struct SolanaRpc {
    pub rpc_url: Option<String>,
    pub transport: Box<dyn Transport>,
}

impl SolanaRpc {
    fn post(&self, method: &str, params: Value) -> Result<Value, SolanaRpcError> {
        let Some(url) = self.rpc_url.as_deref().map(str::trim).filter(|u| !u.is_empty()) else {
            return Err(SolanaRpcError::InvalidOperation(
                "Pay:Solana:RpcUrl is not configured".into(),
            ));
        };

        let payload = json!({ "jsonrpc": "2.0", "id": 1, "method": method, "params": params });
        let request = OutRequest {
            method: "POST".into(),
            url: url.to_string(),
            headers: vec![("Content-Type".into(), "application/json".into())],
            body: Some(payload.to_string()),
        };
        let response = self
            .transport
            .send(request)
            .map_err(|e| SolanaRpcError::InvalidOperation(format!("solana RPC rejected {method}: {e}")))?;

        if response.status == 429 {
            return Err(SolanaRpcError::Throttled);
        }
        if !(200..300).contains(&response.status) {
            return Err(SolanaRpcError::InvalidOperation(format!(
                "solana RPC rejected {method}"
            )));
        }

        serde_json::from_str(&response.body)
            .map_err(|_| SolanaRpcError::InvalidOperation(format!("solana RPC returned invalid {method} body")))
    }

    pub fn get_transaction(&self, signature: &str) -> Result<Value, SolanaRpcError> {
        self.post(
            "getTransaction",
            json!([signature, {
                "encoding": "jsonParsed",
                "commitment": "finalized",
                "maxSupportedTransactionVersion": 0,
            }]),
        )
    }

    pub fn get_signatures_for_address(&self, reference: &str) -> Result<Value, SolanaRpcError> {
        self.post(
            "getSignaturesForAddress",
            json!([reference, { "commitment": "finalized", "limit": 20 }]),
        )
    }

    /// Boot-time genesis probe: validates the RPC actually serves the pinned cluster.
    pub fn get_genesis_hash(&self) -> Result<String, SolanaRpcError> {
        let doc = self.post("getGenesisHash", json!([]))?;
        match doc.get("result").and_then(Value::as_str).filter(|s| !s.is_empty()) {
            Some(hash) => Ok(hash.to_string()),
            None => Err(SolanaRpcError::InvalidOperation(
                "solana RPC returned no genesis hash".into(),
            )),
        }
    }
}
