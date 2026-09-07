//! Live Solana JSON-RPC. Parse/validate stay in `rails::solana` (no sqlx here).

use rails::solana::SolanaError;

pub struct LiveRpc {
    client: reqwest::Client,
    url: String,
}

impl LiveRpc {
    pub fn new(url: String) -> Self {
        let client = reqwest::Client::builder()
            .timeout(std::time::Duration::from_secs(10))
            .redirect(reqwest::redirect::Policy::none())
            .build()
            .unwrap_or_else(|_| reqwest::Client::new());
        Self { client, url }
    }

    async fn post(&self, method: &str, params: serde_json::Value) -> Result<String, SolanaError> {
        let payload = serde_json::json!({
            "jsonrpc": "2.0",
            "id": 1,
            "method": method,
            "params": params,
        });
        let res = self
            .client
            .post(&self.url)
            .json(&payload)
            .send()
            .await
            .map_err(|_| SolanaError::Rejected)?;
        if res.status().as_u16() == 429 {
            return Err(SolanaError::Throttled);
        }
        if !res.status().is_success() {
            return Err(SolanaError::Rejected);
        }
        res.text().await.map_err(|_| SolanaError::Rejected)
    }

    pub async fn get_transaction(&self, signature: &str) -> Result<String, SolanaError> {
        self.post(
            "getTransaction",
            serde_json::json!([
                signature,
                {
                    "encoding": "jsonParsed",
                    "commitment": "finalized",
                    "maxSupportedTransactionVersion": 0
                }
            ]),
        )
        .await
    }

    pub async fn get_signatures(&self, reference: &str) -> Result<String, SolanaError> {
        self.post(
            "getSignaturesForAddress",
            serde_json::json!([
                reference,
                { "commitment": "finalized", "limit": 20 }
            ]),
        )
        .await
    }
}
