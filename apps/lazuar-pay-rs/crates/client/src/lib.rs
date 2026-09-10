//! TypeSpec `/v1` HTTP client. No I/O into `pay_rs` — the host is the money writer
//! (035/01 lock 2). Wire JSON is returned as `serde_json::Value` so CLI/MCP cannot
//! rename `paid` to `Settled`.

#![forbid(unsafe_code)]

mod config;
mod error;

pub use config::Config;
pub use error::Error;

use rust_decimal::Decimal;
use serde_json::{json, Map, Number, Value};

/// Merchant `/v1` caller. Holds one reqwest client (no redirects — same posture as
/// outbound webhooks; a 3xx must not silently POST a mint at another origin).
pub struct Client {
    http: reqwest::Client,
    cfg: Config,
}

impl Client {
    pub fn new(cfg: Config) -> Result<Self, Error> {
        let http = reqwest::Client::builder()
            .redirect(reqwest::redirect::Policy::none())
            .build()
            .map_err(Error::Transport)?;
        Ok(Self { http, cfg })
    }

    pub fn config(&self) -> &Config {
        &self.cfg
    }

    pub async fn whoami(&self) -> Result<Value, Error> {
        self.get("/v1/whoami").await
    }

    pub async fn ready(&self) -> Result<Value, Error> {
        let org = self.cfg.org_id()?;
        self.get(&format!("/v1/orgs/{org}/ready")).await
    }

    /// `POST /v1/checkouts`. Amount is a JSON number (09 lock 7), never a string, never f64.
    pub async fn checkout_create(
        &self,
        provider: &str,
        amount: Decimal,
        currency: &str,
        idempotency_key: Option<&str>,
    ) -> Result<Value, Error> {
        let org = self.cfg.org_id()?;
        let body = json!({
            "org_id": org,
            "provider": provider,
            "amount": decimal_number(amount)?,
            "currency": currency,
        });
        self.post("/v1/checkouts", body, idempotency_key).await
    }

    pub async fn checkout_get(&self, id: &str) -> Result<Value, Error> {
        self.get(&format!("/v1/checkouts/{id}")).await
    }

    pub async fn payments_list(
        &self,
        limit: Option<u32>,
        after: Option<&str>,
    ) -> Result<Value, Error> {
        let org = self.cfg.org_id()?;
        self.get_list(&format!("/v1/orgs/{org}/payments"), limit, after)
            .await
    }

    pub async fn receipts_list(
        &self,
        limit: Option<u32>,
        after: Option<&str>,
    ) -> Result<Value, Error> {
        let org = self.cfg.org_id()?;
        self.get_list(&format!("/v1/orgs/{org}/receipts"), limit, after)
            .await
    }

    pub async fn receipts_get(&self, id: &str) -> Result<Value, Error> {
        let org = self.cfg.org_id()?;
        self.get(&format!("/v1/orgs/{org}/receipts/{id}")).await
    }

    async fn get(&self, path: &str) -> Result<Value, Error> {
        let req = self
            .http
            .get(self.cfg.url(path))
            .header("Authorization", self.cfg.authorization());
        self.send(req).await
    }

    async fn get_list(
        &self,
        path: &str,
        limit: Option<u32>,
        after: Option<&str>,
    ) -> Result<Value, Error> {
        let mut q: Vec<(&str, String)> = Vec::new();
        if let Some(n) = limit {
            q.push(("limit", n.to_string()));
        }
        if let Some(a) = after {
            q.push(("after", a.to_string()));
        }
        let req = self
            .http
            .get(self.cfg.url(path))
            .query(&q)
            .header("Authorization", self.cfg.authorization());
        self.send(req).await
    }

    async fn post(
        &self,
        path: &str,
        body: Value,
        idempotency_key: Option<&str>,
    ) -> Result<Value, Error> {
        let mut req = self
            .http
            .post(self.cfg.url(path))
            .header("Authorization", self.cfg.authorization())
            .header("Content-Type", "application/json")
            .json(&body);
        if let Some(key) = idempotency_key.map(str::trim).filter(|s| !s.is_empty()) {
            req = req.header("Idempotency-Key", key);
        }
        self.send(req).await
    }

    async fn send(&self, req: reqwest::RequestBuilder) -> Result<Value, Error> {
        let res = req.send().await.map_err(Error::Transport)?;
        let status = res.status().as_u16();
        let bytes = res.bytes().await.map_err(Error::Transport)?;
        let body: Value = if bytes.is_empty() {
            Value::Null
        } else {
            serde_json::from_slice(&bytes)
                .unwrap_or_else(|_| Value::String(String::from_utf8_lossy(&bytes).into_owned()))
        };
        if (200..300).contains(&status) {
            return Ok(body);
        }
        Err(Error::from_problem(status, &body))
    }
}

/// Host amounts are JSON numbers. `rust_decimal` default serde is a string — do not use it
/// on the wire (028 P1-14).
pub fn decimal_number(d: Decimal) -> Result<Value, Error> {
    let s = d.normalize().to_string();
    let n: Number = s
        .parse()
        .map_err(|_| Error::Config(format!("amount is not a JSON number: {s}")))?;
    Ok(Value::Number(n))
}

pub fn object_field<'a>(v: &'a Value, key: &str) -> Option<&'a Value> {
    v.as_object().and_then(|m: &Map<String, Value>| m.get(key))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn decimal_number_is_json_number_not_string() {
        let d = Decimal::from_str_exact("10.00").unwrap();
        let v = decimal_number(d).unwrap();
        assert!(v.is_number(), "{v}");
        assert!(!v.is_string(), "{v}");
        // normalize() drops trailing zeros; the host still accepts 10 as MYR 10.00.
        assert_eq!(v.to_string(), "10");
    }

    #[test]
    fn decimal_number_strips_trailing_zeros_via_normalize() {
        let d = Decimal::from_str_exact("10.50").unwrap();
        let v = decimal_number(d).unwrap();
        assert_eq!(v.to_string(), "10.5");
    }
}
