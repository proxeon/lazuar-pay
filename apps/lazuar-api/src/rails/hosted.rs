//! Hosted mint implementations for live rails. Fixtures-only (D004).

use postgres::Client;
use serde_json::json;

use crate::publicpay::start::{HostedRail, HostedSession, StartRailError};
use crate::rails::providers;
use crate::secrets::SecretBox;
use crate::transport::{OutRequest, Transport};

pub struct VaultedRail<'a> {
    pub pool: crate::app::PgPool,
    pub box_one: &'a SecretBox,
    pub transport: &'a dyn Transport,
    pub environment: &'a str,
    pub public_base_url: &'a str,
    pub checkout_base_url: &'a str,
    pub solana_cluster: &'a str,
}

impl HostedRail for VaultedRail<'_> {
    fn create_hosted_url(
        &self,
        checkout_id: &str,
        public_token: &str,
        org_id: &str,
    ) -> Result<HostedSession, StartRailError> {
        let mut conn = self.pool.get().map_err(|e| StartRailError::Rejected(e.to_string()))?;
        let row = conn
            .query_opt(
                "SELECT \"Provider\",\"Amount\",\"Currency\",\"SuccessUrl\",\"CancelUrl\" \
                 FROM public.checkouts WHERE \"Id\" = $1",
                &[&checkout_id],
            )
            .map_err(|e| StartRailError::Rejected(e.to_string()))?
            .ok_or_else(|| StartRailError::Rejected("checkout not found".into()))?;
        let provider: String = row.get("Provider");
        let amount: rust_decimal::Decimal = row.get("Amount");
        let currency: String = row.get("Currency");
        let Some(name) = providers::try_normalize(Some(&provider)) else {
            return Err(StartRailError::Rejected("unknown provider".into()));
        };
        if providers::is_test(name) {
            if !providers::allows_test(self.environment) {
                return Err(StartRailError::Rejected("test processor is not enabled".into()));
            }
            return Ok(HostedSession {
                provider_session_id: format!("test_{checkout_id}"),
                url: format!("{}/c/{public_token}", self.checkout_base_url),
            });
        }
        if providers::is_solana(name) {
            return mint_solana(&mut conn, org_id, checkout_id, amount, self.solana_cluster);
        }

        let cred = conn
            .query_opt(
                "SELECT \"Ciphertext\",\"PublicMerchantId\",\"Environment\" FROM public.gateway_credentials \
                 WHERE \"OrgId\" = $1 AND \"Provider\" = $2",
                &[&org_id, &name],
            )
            .map_err(|e| StartRailError::Rejected(e.to_string()))?
            .ok_or_else(|| StartRailError::Rejected("rail not configured".into()))?;
        let ciphertext: String = cred.get("Ciphertext");
        let secret = self
            .box_one
            .unprotect(&ciphertext)
            .map_err(|_| StartRailError::Rejected("rail not configured".into()))?;
        let success = format!("{}/c/{public_token}?status=verifying", self.checkout_base_url);
        let cancel = format!("{}/c/{public_token}", self.checkout_base_url);
        let minor = crate::domain::currency::to_minor(amount);

        match name {
            p if p == providers::STRIPE => mint_stripe(self.transport, &secret, checkout_id, org_id, &currency, minor, &success, &cancel),
            p if p == providers::BILLPLZ => {
                let collection: String = cred.get("PublicMerchantId");
                mint_billplz(
                    self.transport,
                    &secret,
                    &collection,
                    checkout_id,
                    org_id,
                    minor,
                    self.public_base_url,
                    &success,
                )
            }
            p if p == providers::CHIP => mint_chip(self.transport, &secret, checkout_id, &currency, minor, &success, &cancel),
            p if p == providers::XENDIT => mint_xendit(self.transport, &secret, checkout_id, &currency, minor, &success),
            p if p == providers::RAZORPAY => mint_razorpay(self.transport, &secret, checkout_id, &currency, minor, &success, &cancel),
            _ => Err(StartRailError::Rejected("rail not configured".into())),
        }
    }
}

#[allow(clippy::too_many_arguments)]
fn mint_stripe(
    transport: &dyn Transport,
    secret: &str,
    checkout_id: &str,
    org_id: &str,
    currency: &str,
    minor: i64,
    success: &str,
    cancel: &str,
) -> Result<HostedSession, StartRailError> {
    let body = format!(
        "mode=payment&client_reference_id={}&success_url={}&cancel_url={}&metadata[checkout_id]={}&metadata[org_id]={}&line_items[0][quantity]=1&line_items[0][price_data][currency]={}&line_items[0][price_data][unit_amount]={}&line_items[0][price_data][product_data][name]=Pay",
        urlencoding::encode(checkout_id),
        urlencoding::encode(success),
        urlencoding::encode(cancel),
        urlencoding::encode(checkout_id),
        urlencoding::encode(org_id),
        urlencoding::encode(&currency.to_lowercase()),
        minor
    );
    let resp = transport
        .send(OutRequest {
            method: "POST".into(),
            url: "https://api.stripe.com/v1/checkout/sessions".into(),
            headers: vec![
                ("Authorization".into(), format!("Bearer {secret}")),
                ("Idempotency-Key".into(), format!("lazuar-checkout:{checkout_id}")),
                ("Content-Type".into(), "application/x-www-form-urlencoded".into()),
            ],
            body: Some(body),
        })
        .map_err(|e| StartRailError::Rejected(e.to_string()))?;
    if resp.status >= 400 {
        return Err(StartRailError::Rejected(format!("stripe status {}", resp.status)));
    }
    let v: serde_json::Value = serde_json::from_str(&resp.body).unwrap_or(json!({}));
    let url = v.get("url").and_then(|u| u.as_str()).ok_or_else(|| StartRailError::Rejected("Stripe returned no URL".into()))?;
    let id = v.get("id").and_then(|u| u.as_str()).unwrap_or(checkout_id);
    Ok(HostedSession { provider_session_id: id.to_string(), url: url.to_string() })
}

#[allow(clippy::too_many_arguments)]
fn mint_billplz(
    transport: &dyn Transport,
    secret: &str,
    collection: &str,
    checkout_id: &str,
    org_id: &str,
    minor: i64,
    public_base: &str,
    success: &str,
) -> Result<HostedSession, StartRailError> {
    if !public_base.starts_with("https://") {
        return Err(StartRailError::BadRequest("callback base must be public https".into()));
    }
    let callback = format!(
        "{}/v1/webhooks/billplz/{}?checkout_id={}",
        public_base.trim_end_matches('/'),
        urlencoding::encode(org_id),
        urlencoding::encode(checkout_id)
    );
    let auth = base64::Engine::encode(&base64::engine::general_purpose::STANDARD, format!("{secret}:"));
    let body = format!(
        "collection_id={}&description=Pay&amount={}&callback_url={}&redirect_url={}&reference_1={}",
        urlencoding::encode(collection),
        minor,
        urlencoding::encode(&callback),
        urlencoding::encode(success),
        urlencoding::encode(checkout_id)
    );
    let resp = transport
        .send(OutRequest {
            method: "POST".into(),
            url: "https://www.billplz.com/api/v3/bills".into(),
            headers: vec![
                ("Authorization".into(), format!("Basic {auth}")),
                ("Content-Type".into(), "application/x-www-form-urlencoded".into()),
            ],
            body: Some(body),
        })
        .map_err(|e| StartRailError::Rejected(e.to_string()))?;
    if resp.status >= 400 {
        return Err(StartRailError::Rejected(format!("billplz status {}", resp.status)));
    }
    let v: serde_json::Value = serde_json::from_str(&resp.body).unwrap_or(json!({}));
    let url = v.get("url").and_then(|u| u.as_str()).ok_or_else(|| StartRailError::Rejected("billplz returned no URL".into()))?;
    let id = v.get("id").and_then(|u| u.as_str()).unwrap_or(checkout_id);
    Ok(HostedSession { provider_session_id: id.to_string(), url: url.to_string() })
}

fn mint_chip(
    transport: &dyn Transport,
    secret: &str,
    checkout_id: &str,
    currency: &str,
    minor: i64,
    success: &str,
    cancel: &str,
) -> Result<HostedSession, StartRailError> {
    let body = json!({
        "success_redirect": success,
        "failure_redirect": cancel,
        "client": { "email": "payer@example.com" },
        "purchase": {
            "currency": currency,
            "products": [{ "name": "Pay", "price": minor }],
        },
        "reference": checkout_id,
    });
    let resp = transport
        .send(OutRequest {
            method: "POST".into(),
            url: "https://gate.chip-in.asia/api/v1/purchases/".into(),
            headers: vec![
                ("Authorization".into(), format!("Bearer {secret}")),
                ("Content-Type".into(), "application/json".into()),
            ],
            body: Some(body.to_string()),
        })
        .map_err(|e| StartRailError::Rejected(e.to_string()))?;
    if resp.status >= 400 {
        return Err(StartRailError::Rejected(format!("chip status {}", resp.status)));
    }
    let v: serde_json::Value = serde_json::from_str(&resp.body).unwrap_or(json!({}));
    let url = v.get("checkout_url").or_else(|| v.get("url")).and_then(|u| u.as_str())
        .ok_or_else(|| StartRailError::Rejected("chip returned no URL".into()))?;
    let id = v.get("id").and_then(|u| u.as_str()).unwrap_or(checkout_id);
    Ok(HostedSession { provider_session_id: id.to_string(), url: url.to_string() })
}

fn mint_xendit(
    transport: &dyn Transport,
    secret: &str,
    checkout_id: &str,
    currency: &str,
    minor: i64,
    success: &str,
) -> Result<HostedSession, StartRailError> {
    let auth = base64::Engine::encode(&base64::engine::general_purpose::STANDARD, format!("{secret}:"));
    // C# XenditHosted: amount is major units (FromMinor(ToMinor)), not sen.
    let major = crate::domain::currency::from_minor(rust_decimal::Decimal::from(minor));
    let body = json!({
        "external_id": checkout_id,
        "amount": crate::hosting::decimal_json(major),
        "currency": currency,
        "success_redirect_url": success,
    });
    let resp = transport
        .send(OutRequest {
            method: "POST".into(),
            url: "https://api.xendit.co/v2/invoices".into(),
            headers: vec![
                ("Authorization".into(), format!("Basic {auth}")),
                ("Content-Type".into(), "application/json".into()),
            ],
            body: Some(body.to_string()),
        })
        .map_err(|e| StartRailError::Rejected(e.to_string()))?;
    if resp.status >= 400 {
        return Err(StartRailError::Rejected(format!("xendit status {}", resp.status)));
    }
    let v: serde_json::Value = serde_json::from_str(&resp.body).unwrap_or(json!({}));
    let url = v.get("invoice_url").or_else(|| v.get("url")).and_then(|u| u.as_str())
        .ok_or_else(|| StartRailError::Rejected("xendit returned no URL".into()))?;
    let id = v.get("id").and_then(|u| u.as_str()).unwrap_or(checkout_id);
    Ok(HostedSession { provider_session_id: id.to_string(), url: url.to_string() })
}

fn mint_razorpay(
    transport: &dyn Transport,
    secret: &str,
    checkout_id: &str,
    currency: &str,
    minor: i64,
    success: &str,
    cancel: &str,
) -> Result<HostedSession, StartRailError> {
    let (key_id, key_secret) = secret.split_once(':').unwrap_or((secret, ""));
    let auth = base64::Engine::encode(&base64::engine::general_purpose::STANDARD, format!("{key_id}:{key_secret}"));
    let body = json!({
        "amount": minor,
        "currency": currency,
        "accept_partial": false,
        "callback_url": success,
        "cancel_url": cancel,
        "notes": { "checkout_id": checkout_id },
    });
    let resp = transport
        .send(OutRequest {
            method: "POST".into(),
            url: "https://api.razorpay.com/v1/payment_links".into(),
            headers: vec![
                ("Authorization".into(), format!("Basic {auth}")),
                ("Content-Type".into(), "application/json".into()),
            ],
            body: Some(body.to_string()),
        })
        .map_err(|e| StartRailError::Rejected(e.to_string()))?;
    if resp.status >= 400 {
        return Err(StartRailError::Rejected(format!("razorpay status {}", resp.status)));
    }
    let v: serde_json::Value = serde_json::from_str(&resp.body).unwrap_or(json!({}));
    let url = v.get("short_url").or_else(|| v.get("url")).and_then(|u| u.as_str())
        .ok_or_else(|| StartRailError::Rejected("razorpay returned no URL".into()))?;
    let id = v.get("id").and_then(|u| u.as_str()).unwrap_or(checkout_id);
    Ok(HostedSession { provider_session_id: id.to_string(), url: url.to_string() })
}

fn mint_solana(
    conn: &mut Client,
    org_id: &str,
    checkout_id: &str,
    amount: rust_decimal::Decimal,
    cluster: &str,
) -> Result<HostedSession, StartRailError> {
    use crate::rails::solana::base58;
    let address: String = conn
        .query_opt(
            "SELECT \"PublicMerchantId\" FROM public.gateway_credentials \
             WHERE \"OrgId\" = $1 AND \"Provider\" = $2",
            &[&org_id, &providers::SOLANA],
        )
        .map_err(|e| StartRailError::Rejected(e.to_string()))?
        .and_then(|row| row.get::<_, Option<String>>(0))
        .filter(|s| !s.is_empty())
        .ok_or_else(|| StartRailError::Rejected("rail not configured".into()))?;
    let atomic = crate::rails::solana::money::try_to_atomic(amount)
        .ok_or_else(|| StartRailError::BadRequest("amount is not a valid USDC amount".into()))?;
    let reference = base58::encode(&uuid::Uuid::new_v4().as_bytes()[..]);
    let mint = crate::rails::solana::cluster::mint(crate::rails::solana::cluster::from_config(Some(cluster)));
    let uri = format!(
        "solana:{address}?amount={}&spl-token={mint}&reference={reference}&memo={}",
        amount,
        urlencoding::encode(checkout_id)
    );
    Ok(HostedSession { provider_session_id: reference, url: uri })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::transport::{OutRequest, OutResponse, Transport, TransportError};
    use std::sync::Mutex;

    struct Capture(Mutex<Option<OutRequest>>);
    impl Transport for Capture {
        fn send(&self, request: OutRequest) -> Result<OutResponse, TransportError> {
            *self.0.lock().unwrap() = Some(request);
            Ok(OutResponse {
                status: 200,
                body: r#"{"id":"inv_1","invoice_url":"https://xendit.test/i"}"#.into(),
            })
        }
    }

    #[test]
    fn xendit_mint_sends_major_units_not_sen() {
        let cap = Capture(Mutex::new(None));
        mint_xendit(&cap, "xnd_key", "co_1", "MYR", 990, "https://ok.example/c/t").unwrap();
        let req = cap.0.lock().unwrap().clone().expect("xendit POST");
        let v: serde_json::Value = serde_json::from_str(req.body.as_deref().unwrap()).unwrap();
        assert!(v["amount"].is_number(), "amount must be a JSON number: {v}");
        assert_eq!(v["amount"].to_string(), "9.90");
        assert_ne!(v["amount"], 990);
    }
}
