//! TypeSpec `/v1` HTTP client. No I/O into `pay_rs` — the host is the money writer
//! (035/01 lock 2). Wire JSON is returned as `serde_json::Value` so CLI/MCP cannot
//! rename `paid` to `Settled`.

#![forbid(unsafe_code)]

mod config;
mod error;
mod gateway;

pub use config::{env_first, Config};
pub use error::Error;
pub use gateway::validate_gateway_put;

use rust_decimal::Decimal;
use serde_json::{json, Map, Number, Value};
use std::time::{Duration, Instant};

/// Optional TypeSpec fields on `POST /v1/checkouts` (036/006 #13).
#[derive(Clone, Debug, Default)]
pub struct CheckoutExtras<'a> {
    pub success_url: Option<&'a str>,
    pub cancel_url: Option<&'a str>,
    pub product_id: Option<&'a str>,
}

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
    /// Idempotency-Key is required so agent retries do not mint a second charge (036/006 #7).
    pub async fn checkout_create(
        &self,
        provider: &str,
        amount: Decimal,
        currency: &str,
        idempotency_key: &str,
        extras: CheckoutExtras<'_>,
    ) -> Result<Value, Error> {
        validate_currency_for_provider(provider, currency)?;
        let org = self.cfg.org_id()?;
        let key = require_idempotency(idempotency_key)?;
        let mut body = json!({
            "org_id": org,
            "provider": provider,
            "amount": decimal_number(amount)?,
            "currency": currency,
        });
        if let Some(u) = extras.success_url.map(str::trim).filter(|s| !s.is_empty()) {
            body["success_url"] = json!(u);
        }
        if let Some(u) = extras.cancel_url.map(str::trim).filter(|s| !s.is_empty()) {
            body["cancel_url"] = json!(u);
        }
        if let Some(p) = extras.product_id.map(str::trim).filter(|s| !s.is_empty()) {
            // TypeSpec optional. Host standalone mint currently does not persist it.
            body["product_id"] = json!(p);
        }
        self.post("/v1/checkouts", body, Some(key)).await
    }

    pub async fn checkout_get(&self, id: &str) -> Result<Value, Error> {
        self.get(&format!("/v1/checkouts/{id}")).await
    }

    /// Poll `GET /v1/checkouts/{id}` until wire `status` matches `until` (`paid`/`failed`/`expired`/`open`).
    pub async fn checkout_wait(
        &self,
        id: &str,
        until: &str,
        timeout: Duration,
        interval: Duration,
    ) -> Result<Value, Error> {
        let until = until.trim().to_ascii_lowercase();
        if !matches!(until.as_str(), "paid" | "failed" | "expired" | "open") {
            return Err(Error::Config(
                "until must be paid, failed, expired, or open".into(),
            ));
        }
        let deadline = Instant::now() + timeout;
        let mut last = self.checkout_get(id).await?;
        loop {
            if last.get("status").and_then(Value::as_str) == Some(until.as_str()) {
                return Ok(last);
            }
            if Instant::now() >= deadline {
                return Err(Error::WaitTimeout { until, last });
            }
            tokio::time::sleep(interval).await;
            last = self.checkout_get(id).await?;
        }
    }

    /// `POST /v1/orgs/{org}/refunds`. Idempotency-Key is required (036/006 #7).
    pub async fn refund_create(
        &self,
        checkout_id: &str,
        amount: Option<Decimal>,
        idempotency_key: &str,
    ) -> Result<Value, Error> {
        let org = self.cfg.org_id()?;
        let key = require_idempotency(idempotency_key)?;
        let mut body = json!({ "checkout_id": checkout_id });
        if let Some(a) = amount {
            body["amount"] = decimal_number(a)?;
        }
        self.post(&format!("/v1/orgs/{org}/refunds"), body, Some(key))
            .await
    }

    /// `POST /v1/payment-links`. Occupancy mint (SPA "Pay links").
    pub async fn payment_link_create(
        &self,
        provider: &str,
        amount: Decimal,
        currency: &str,
        max_payers: Option<i32>,
        unlimited: bool,
        label: Option<&str>,
    ) -> Result<Value, Error> {
        validate_currency_for_provider(provider, currency)?;
        let org = self.cfg.org_id()?;
        let mut body = json!({
            "org_id": org,
            "provider": provider,
            "amount": decimal_number(amount)?,
            "currency": currency,
            "unlimited": unlimited,
        });
        if let Some(n) = max_payers {
            body["max_payers"] = json!(n);
        }
        if let Some(l) = label.map(str::trim).filter(|s| !s.is_empty()) {
            body["label"] = json!(l);
        }
        self.post("/v1/payment-links", body, None).await
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

    /// `PUT /v1/orgs/{org}/gateway`. Body is TypeSpec PutGateway (from `--file`).
    pub async fn gateway_put(&self, body: Value) -> Result<Value, Error> {
        validate_gateway_put(&body)?;
        let org = self.cfg.org_id()?;
        self.put(&format!("/v1/orgs/{org}/gateway"), body).await
    }

    pub async fn gateway_get(&self, provider: &str) -> Result<Value, Error> {
        let org = self.cfg.org_id()?;
        let p = provider.trim();
        if p.is_empty() {
            return Err(Error::Config("provider is required".into()));
        }
        let req = self
            .http
            .get(self.cfg.url(&format!("/v1/orgs/{org}/gateway")))
            .query(&[("provider", p)])
            .header("Authorization", self.cfg.authorization());
        self.send(req).await
    }

    pub async fn gateway_list(&self) -> Result<Value, Error> {
        let org = self.cfg.org_id()?;
        self.get(&format!("/v1/orgs/{org}/gateways")).await
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

    async fn put(&self, path: &str, body: Value) -> Result<Value, Error> {
        let req = self
            .http
            .put(self.cfg.url(path))
            .header("Authorization", self.cfg.authorization())
            .header("Content-Type", "application/json")
            .json(&body);
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

/// Host 400s `solana` + MYR (`solana does not capture ringgit`). Fail closed here (036/006 #14).
pub fn validate_currency_for_provider(provider: &str, currency: &str) -> Result<(), Error> {
    let p = provider.trim().to_ascii_lowercase();
    let c = currency.trim().to_ascii_uppercase();
    if p == "solana" && c != "USDC" {
        return Err(Error::Config(
            "solana requires currency USDC (receive-only; not MYR or USD)".into(),
        ));
    }
    Ok(())
}

fn require_idempotency(raw: &str) -> Result<&str, Error> {
    let key = raw.trim();
    if key.is_empty() {
        return Err(Error::Config("idempotency-key is required".into()));
    }
    Ok(key)
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

    #[test]
    fn solana_rejects_myr_before_http() {
        let err = validate_currency_for_provider("solana", "MYR").unwrap_err();
        assert!(err.to_string().contains("USDC"), "{err}");
        validate_currency_for_provider("solana", "USDC").unwrap();
        validate_currency_for_provider("test", "MYR").unwrap();
    }
}
