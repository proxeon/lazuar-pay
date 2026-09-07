//! Xendit hosted invoice. Amounts are major units on the wire (028 P0-3).
//! No refund API in v1. Host is always api.xendit.co.

use domain::money::{Currency, Money};
use domain::proof::{Binding, IgnoreReason, SyncOutcome, WebhookOutcome};
use domain::rail::{ConnectorRefs, HostedSession};
use domain::PaymentId;
use serde_json::{json, Value};
use sha2::{Digest, Sha256};
use subtle::ConstantTimeEq;

pub const API_BASE: &str = "https://api.xendit.co";
pub const SIGNATURE_HEADER: &str = "x-callback-token";

#[derive(Debug, thiserror::Error, PartialEq, Eq)]
pub enum XenditParseError {
    #[error("invalid signature")]
    InvalidSignature,
    #[error("invalid event")]
    InvalidEvent,
    #[error("missing invoice id")]
    MissingInvoiceId,
    #[error("missing currency")]
    MissingCurrency,
}

/// SHA-256 both sides then constant-time on the digests (length is not an oracle).
pub fn token_ok(provided: &str, secret: &str) -> bool {
    let left = Sha256::digest(provided.trim().as_bytes());
    let right = Sha256::digest(secret.as_bytes());
    bool::from(left.ct_eq(&right))
}

fn refs(invoice_id: &str) -> ConnectorRefs {
    ConnectorRefs {
        session_id: Some(invoice_id.to_string()),
        capture_id: Some(invoice_id.to_string()),
        network_id: None,
    }
}

fn binding(checkout_id: &str, invoice_id: &str) -> Binding {
    if let Ok(id) = PaymentId::from_wire(checkout_id) {
        Binding::Payment { id }
    } else {
        Binding::Session {
            session_id: invoice_id.to_string(),
        }
    }
}

fn json_str<'a>(v: &'a Value, key: &str) -> Option<&'a str> {
    v.get(key)
        .and_then(|x| x.as_str())
        .filter(|s| !s.is_empty())
}

fn major_money(v: &Value, key: &str, currency: Currency) -> Option<Money> {
    let n = v.get(key)?;
    let s = match n {
        Value::Number(num) => num.to_string(),
        Value::String(s) if !s.is_empty() => s.clone(),
        _ => return None,
    };
    Money::from_quoted_str(&s, currency).ok()
}

fn invoice_obj(root: &Value) -> &Value {
    root.get("data").filter(|d| d.is_object()).unwrap_or(root)
}

/// Verify `x-callback-token` and map to [`WebhookOutcome`].
pub fn parse_webhook(
    body: &[u8],
    provided_token: Option<&str>,
    secret: &str,
) -> Result<(String, WebhookOutcome), XenditParseError> {
    if body.iter().all(|b| b.is_ascii_whitespace()) {
        return Err(XenditParseError::InvalidEvent);
    }
    let provided = provided_token.unwrap_or("").trim();
    if provided.is_empty() || !token_ok(provided, secret) {
        return Err(XenditParseError::InvalidSignature);
    }
    let root: Value = serde_json::from_slice(body).map_err(|_| XenditParseError::InvalidEvent)?;
    let invoice = invoice_obj(&root);
    let invoice_id = json_str(invoice, "id").ok_or(XenditParseError::MissingInvoiceId)?;
    let status = json_str(invoice, "status")
        .or_else(|| json_str(&root, "event"))
        .unwrap_or("");
    if status.eq_ignore_ascii_case("SETTLED") || status.eq_ignore_ascii_case("invoice.settled") {
        return Ok((
            format!("settled:{invoice_id}"),
            WebhookOutcome::Ignored {
                reason: IgnoreReason::UnknownEvent,
            },
        ));
    }
    let is_paid =
        status.eq_ignore_ascii_case("PAID") || status.eq_ignore_ascii_case("invoice.paid");
    if !is_paid {
        return Ok((
            format!("{status}:{invoice_id}"),
            WebhookOutcome::Ignored {
                reason: IgnoreReason::UnrecognizedShape,
            },
        ));
    }
    let raw_ccy = json_str(invoice, "currency").unwrap_or("").trim();
    if raw_ccy.is_empty() {
        return Err(XenditParseError::MissingCurrency);
    }
    let currency = Currency::by_code(&raw_ccy.to_ascii_uppercase())
        .ok_or(XenditParseError::MissingCurrency)?;
    let received = major_money(invoice, "paid_amount", currency)
        .or_else(|| major_money(invoice, "amount", currency))
        .ok_or(XenditParseError::InvalidEvent)?;
    let checkout = invoice
        .get("metadata")
        .and_then(|m| json_str(m, "checkout_id"))
        .or_else(|| json_str(invoice, "external_id"))
        .unwrap_or("");
    Ok((
        format!("paid:{invoice_id}"),
        WebhookOutcome::Paid {
            binding: binding(checkout, invoice_id),
            received,
            refs: refs(invoice_id),
        },
    ))
}

pub fn ignore_detail(proof_id: &str) -> String {
    if proof_id.starts_with("settled:") {
        "settled".into()
    } else {
        proof_id.split(':').next().unwrap_or("").into()
    }
}

pub fn invoice_body(
    payment_id: &str,
    org_id: &str,
    email: &str,
    amount: Value,
    currency: &str,
    success_url: &str,
    cancel_url: &str,
) -> Value {
    json!({
        "external_id": payment_id,
        "amount": amount,
        "currency": currency,
        "description": "Pay",
        "payer_email": email,
        "success_redirect_url": success_url,
        "failure_redirect_url": cancel_url,
        "metadata": {
            "checkout_id": payment_id,
            "org_id": org_id,
        }
    })
}

pub fn parse_hosted(body: &str) -> Result<HostedSession, XenditParseError> {
    let v: Value = serde_json::from_str(body).map_err(|_| XenditParseError::InvalidEvent)?;
    let id = v
        .get("id")
        .and_then(|x| x.as_str())
        .filter(|s| !s.is_empty())
        .ok_or(XenditParseError::InvalidEvent)?;
    let url = v
        .get("invoice_url")
        .and_then(|x| x.as_str())
        .filter(|s| !s.is_empty())
        .ok_or(XenditParseError::InvalidEvent)?;
    Ok(HostedSession {
        url: url.into(),
        session_id: id.into(),
    })
}

pub fn map_sync(status: u16, body: &str) -> SyncOutcome {
    if status == 404 {
        return SyncOutcome::Failed {
            reason: "not_found".into(),
        };
    }
    if !(200..300).contains(&status) {
        return SyncOutcome::Unknown;
    }
    let Ok(v) = serde_json::from_str::<Value>(body) else {
        return SyncOutcome::Unknown;
    };
    let st = json_str(&v, "status").unwrap_or("").to_ascii_uppercase();
    if st == "PENDING" || st == "EXPIRED" || st == "SETTLED" {
        return SyncOutcome::Unknown;
    }
    if st != "PAID" {
        return SyncOutcome::Unknown;
    }
    let raw_ccy = json_str(&v, "currency").unwrap_or("MYR");
    let currency = Currency::by_code(&raw_ccy.to_ascii_uppercase()).unwrap_or(Currency::MYR);
    let Some(received) =
        major_money(&v, "paid_amount", currency).or_else(|| major_money(&v, "amount", currency))
    else {
        return SyncOutcome::Unknown;
    };
    let id = json_str(&v, "id").unwrap_or("").to_string();
    SyncOutcome::Paid {
        received,
        refs: refs(&id),
    }
}

pub fn mint_idempotency_key(payment_id_hex: &str) -> String {
    format!("lazuar-checkout:{payment_id_hex}")
}

static FAKE_SEQ: std::sync::atomic::AtomicU64 = std::sync::atomic::AtomicU64::new(1);

/// In-memory Xendit. CI never calls api.xendit.co.
#[derive(Clone)]
pub struct FakeXendit {
    pub last_amount: std::sync::Arc<std::sync::Mutex<Option<Value>>>,
    pub last_email: std::sync::Arc<std::sync::Mutex<Option<String>>>,
    pub last_host: std::sync::Arc<std::sync::Mutex<Option<String>>>,
    pub last_body: std::sync::Arc<std::sync::Mutex<Option<String>>>,
    retrieve_status: std::sync::Arc<std::sync::Mutex<u16>>,
    retrieve_body: std::sync::Arc<std::sync::Mutex<String>>,
}

impl Default for FakeXendit {
    fn default() -> Self {
        Self {
            last_amount: std::sync::Arc::new(std::sync::Mutex::new(None)),
            last_email: std::sync::Arc::new(std::sync::Mutex::new(None)),
            last_host: std::sync::Arc::new(std::sync::Mutex::new(None)),
            last_body: std::sync::Arc::new(std::sync::Mutex::new(None)),
            retrieve_status: std::sync::Arc::new(std::sync::Mutex::new(200)),
            retrieve_body: std::sync::Arc::new(std::sync::Mutex::new(
                r#"{"id":"inv_1","status":"PENDING","currency":"MYR","amount":10}"#.into(),
            )),
        }
    }
}

impl FakeXendit {
    pub fn create_invoice(
        &self,
        host: &str,
        body: &Value,
    ) -> Result<HostedSession, XenditParseError> {
        *self.last_host.lock().expect("lock") = Some(host.to_string());
        *self.last_amount.lock().expect("lock") = body.get("amount").cloned();
        *self.last_email.lock().expect("lock") = body
            .get("payer_email")
            .and_then(|v| v.as_str())
            .map(str::to_string);
        *self.last_body.lock().expect("lock") = Some(body.to_string());
        let n = FAKE_SEQ.fetch_add(1, std::sync::atomic::Ordering::Relaxed);
        parse_hosted(&format!(
            r#"{{"id":"inv_{n}","invoice_url":"https://checkout.xendit.co/web/inv_{n}"}}"#
        ))
    }

    pub fn set_retrieve(&self, status: u16, body: &str) {
        *self.retrieve_status.lock().expect("lock") = status;
        *self.retrieve_body.lock().expect("lock") = body.to_string();
    }

    pub fn retrieve(&self, _invoice_id: &str) -> (u16, String) {
        (
            *self.retrieve_status.lock().expect("lock"),
            self.retrieve_body.lock().expect("lock").clone(),
        )
    }
}
