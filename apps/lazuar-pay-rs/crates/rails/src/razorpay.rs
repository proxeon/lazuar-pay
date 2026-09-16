//! Razorpay payment link. Amount is already paise. Proof id is body-derived,
//! not `X-Razorpay-Event-Id` (issue 018). No refund API in v1.

use domain::money::{Currency, Money};
use domain::proof::{Binding, IgnoreReason, SyncOutcome, WebhookOutcome};
use domain::rail::{ConnectorRefs, HostedSession};
use domain::PaymentId;
use hmac::{Hmac, Mac};
use serde_json::{json, Value};
use sha2::Sha256;
use subtle::ConstantTimeEq;

pub const API_BASE: &str = "https://api.razorpay.com/v1";
pub const SIGNATURE_HEADER: &str = "X-Razorpay-Signature";

#[derive(Debug, thiserror::Error, PartialEq, Eq)]
pub enum RazorpayParseError {
    #[error("invalid signature")]
    InvalidSignature,
    #[error("invalid event")]
    InvalidEvent,
    #[error("missing payment id")]
    MissingPaymentId,
    #[error("missing event id")]
    MissingEventId,
    #[error("missing currency")]
    MissingCurrency,
}

type HmacSha256 = Hmac<Sha256>;

/// First `:`; both sides non-empty (`RazorpayHosted.TrySplit`).
pub fn try_split(secret: &str) -> Option<(&str, &str)> {
    let i = secret.find(':')?;
    if i == 0 || i + 1 == secret.len() {
        return None;
    }
    Some((&secret[..i], &secret[i + 1..]))
}

pub fn sign(secret: &str, body: &[u8]) -> String {
    let mut mac = HmacSha256::new_from_slice(secret.as_bytes()).expect("hmac");
    mac.update(body);
    hex::encode(mac.finalize().into_bytes())
}

fn verify(secret: &str, body: &[u8], header: &str) -> bool {
    let expected = sign(secret, body);
    let provided = header.trim().to_ascii_lowercase();
    let a = expected.as_bytes();
    let b = provided.as_bytes();
    a.len() == b.len() && bool::from(a.ct_eq(b))
}

fn json_str<'a>(v: &'a Value, key: &str) -> Option<&'a str> {
    v.get(key)
        .and_then(|x| x.as_str())
        .filter(|s| !s.is_empty())
}

fn json_i64(v: &Value, key: &str) -> Option<i64> {
    v.get(key).and_then(|x| {
        x.as_i64()
            .or_else(|| x.as_u64().and_then(|u| i64::try_from(u).ok()))
            .or_else(|| x.as_str().and_then(|s| s.parse().ok()))
    })
}

fn payment_entity(root: &Value) -> Option<&Value> {
    root.get("payload")?
        .get("payment")?
        .get("entity")
        .filter(|e| e.is_object())
}

fn link_id(root: &Value) -> Option<&str> {
    json_str(
        root.get("payload")?.get("payment_link")?.get("entity")?,
        "id",
    )
}

fn notes_checkout(entity: &Value) -> &str {
    entity
        .get("notes")
        .and_then(|n| json_str(n, "checkout_id"))
        .unwrap_or("")
}

fn binding(checkout_id: &str, session_id: &str) -> Binding {
    if let Ok(id) = PaymentId::from_wire(checkout_id) {
        Binding::Payment { id }
    } else {
        Binding::Session {
            session_id: session_id.to_string(),
        }
    }
}

fn refs(pay_id: &str, plink: Option<&str>) -> ConnectorRefs {
    ConnectorRefs {
        session_id: Some(plink.unwrap_or(pay_id).to_string()),
        capture_id: Some(pay_id.to_string()),
        network_id: None,
    }
}

/// Verify HMAC over the raw body. Proof ids are body-derived (not Event-Id).
pub fn parse_webhook(
    body: &[u8],
    signature: Option<&str>,
    secret: &str,
) -> Result<(String, WebhookOutcome), RazorpayParseError> {
    let sig = signature.unwrap_or("").trim();
    if sig.is_empty() || !verify(secret, body, sig) {
        return Err(RazorpayParseError::InvalidSignature);
    }
    if body.iter().all(|b| b.is_ascii_whitespace()) {
        return Err(RazorpayParseError::InvalidEvent);
    }
    let root: Value = serde_json::from_slice(body).map_err(|_| RazorpayParseError::InvalidEvent)?;
    let event = json_str(&root, "event").unwrap_or("");
    let entity = payment_entity(&root);
    let pay_id = entity.and_then(|e| json_str(e, "id"));
    let plink = link_id(&root);

    if event == "payment.failed" {
        let pay_id = pay_id.ok_or(RazorpayParseError::MissingEventId)?;
        let checkout = entity.map(notes_checkout).unwrap_or("");
        let session = plink.unwrap_or(pay_id);
        return Ok((
            format!("failed:{pay_id}"),
            WebhookOutcome::Failed {
                binding: binding(checkout, session),
                reason: "payment_failed".into(),
            },
        ));
    }

    if event != "payment.captured" && event != "payment_link.paid" {
        let suffix = pay_id.or(plink).unwrap_or("none");
        return Ok((
            format!("{event}:{suffix}"),
            WebhookOutcome::Ignored {
                reason: IgnoreReason::UnknownEvent,
            },
        ));
    }

    let entity = entity.ok_or(RazorpayParseError::MissingPaymentId)?;
    let pay_id = pay_id.ok_or(RazorpayParseError::MissingPaymentId)?;
    let raw_ccy = json_str(entity, "currency").unwrap_or("").trim();
    if raw_ccy.is_empty() {
        return Err(RazorpayParseError::MissingCurrency);
    }
    let currency = Currency::by_code(&raw_ccy.to_ascii_uppercase())
        .ok_or(RazorpayParseError::MissingCurrency)?;
    let amount = json_i64(entity, "amount").unwrap_or(0);
    let received = Money::from_minor(i128::from(amount), currency)
        .map_err(|_| RazorpayParseError::InvalidEvent)?;
    let checkout = notes_checkout(entity);
    let session = plink.unwrap_or(pay_id);
    let proof_id = if event == "payment_link.paid" {
        format!("link_paid:{pay_id}")
    } else {
        format!("captured:{pay_id}")
    };
    Ok((
        proof_id,
        WebhookOutcome::Paid {
            binding: binding(checkout, session),
            received,
            refs: refs(pay_id, plink),
        },
    ))
}

pub fn ignore_detail(proof_id: &str) -> String {
    proof_id
        .rsplit_once(':')
        .map(|(e, _)| e.to_string())
        .unwrap_or_else(|| proof_id.to_string())
}

pub fn link_body(
    payment_id: &str,
    org_id: &str,
    email: &str,
    name: &str,
    amount_minor: i64,
    currency: &str,
    success_url: &str,
) -> Value {
    json!({
        "amount": amount_minor,
        "currency": currency,
        "description": "Pay",
        "customer": { "email": email, "name": name },
        "notes": {
            "checkout_id": payment_id,
            "org_id": org_id,
        },
        "callback_url": success_url,
        "callback_method": "get",
        "reference_id": payment_id,
    })
}

pub fn parse_hosted(body: &str) -> Result<HostedSession, RazorpayParseError> {
    let v: Value = serde_json::from_str(body).map_err(|_| RazorpayParseError::InvalidEvent)?;
    let id = v
        .get("id")
        .and_then(|x| x.as_str())
        .filter(|s| !s.is_empty())
        .ok_or(RazorpayParseError::InvalidEvent)?;
    let url = v
        .get("short_url")
        .and_then(|x| x.as_str())
        .filter(|s| !s.is_empty())
        .ok_or(RazorpayParseError::InvalidEvent)?;
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
    let st = json_str(&v, "status").unwrap_or("").to_ascii_lowercase();
    if st == "created" || st == "issued" || st == "expired" || st == "cancelled" {
        return SyncOutcome::Unknown;
    }
    if st != "paid" {
        return SyncOutcome::Unknown;
    }
    let amount = json_i64(&v, "amount").unwrap_or(0);
    let Ok(received) = Money::from_minor(i128::from(amount), Currency::INR) else {
        return SyncOutcome::Unknown;
    };
    let id = json_str(&v, "id").unwrap_or("").to_string();
    let pay = v
        .get("payments")
        .and_then(|p| p.as_array())
        .and_then(|a| a.first())
        .and_then(|p| json_str(p, "id"))
        .unwrap_or(id.as_str());
    SyncOutcome::Paid {
        received,
        refs: refs(pay, Some(&id)),
    }
}

pub fn mint_idempotency_key(payment_id_hex: &str) -> String {
    format!("lazuar-checkout:{payment_id_hex}")
}

static FAKE_SEQ: std::sync::atomic::AtomicU64 = std::sync::atomic::AtomicU64::new(1);

/// In-memory Razorpay. CI never calls api.razorpay.com.
#[derive(Clone)]
pub struct FakeRazorpay {
    pub last_amount: std::sync::Arc<std::sync::Mutex<Option<i64>>>,
    pub last_email: std::sync::Arc<std::sync::Mutex<Option<String>>>,
    pub last_host: std::sync::Arc<std::sync::Mutex<Option<String>>>,
    pub last_body: std::sync::Arc<std::sync::Mutex<Option<String>>>,
    pub last_idempotency: std::sync::Arc<std::sync::Mutex<Option<String>>>,
    retrieve_status: std::sync::Arc<std::sync::Mutex<u16>>,
    retrieve_body: std::sync::Arc<std::sync::Mutex<String>>,
}

impl Default for FakeRazorpay {
    fn default() -> Self {
        Self {
            last_amount: std::sync::Arc::new(std::sync::Mutex::new(None)),
            last_email: std::sync::Arc::new(std::sync::Mutex::new(None)),
            last_host: std::sync::Arc::new(std::sync::Mutex::new(None)),
            last_body: std::sync::Arc::new(std::sync::Mutex::new(None)),
            last_idempotency: std::sync::Arc::new(std::sync::Mutex::new(None)),
            retrieve_status: std::sync::Arc::new(std::sync::Mutex::new(200)),
            retrieve_body: std::sync::Arc::new(std::sync::Mutex::new(
                r#"{"id":"plink_1","status":"created","amount":1000,"currency":"INR"}"#.into(),
            )),
        }
    }
}

impl FakeRazorpay {
    pub fn create_link(
        &self,
        host: &str,
        body: &Value,
        idempotency: &str,
    ) -> Result<HostedSession, RazorpayParseError> {
        *self.last_host.lock().expect("lock") = Some(host.to_string());
        *self.last_amount.lock().expect("lock") = body.get("amount").and_then(|v| v.as_i64());
        *self.last_email.lock().expect("lock") = body
            .get("customer")
            .and_then(|c| c.get("email"))
            .and_then(|v| v.as_str())
            .map(str::to_string);
        *self.last_body.lock().expect("lock") = Some(body.to_string());
        *self.last_idempotency.lock().expect("lock") = Some(idempotency.to_string());
        let n = FAKE_SEQ.fetch_add(1, std::sync::atomic::Ordering::Relaxed);
        parse_hosted(&format!(
            r#"{{"id":"plink_{n}","short_url":"https://rzp.io/i/{n}"}}"#
        ))
    }

    pub fn set_retrieve(&self, status: u16, body: &str) {
        *self.retrieve_status.lock().expect("lock") = status;
        *self.retrieve_body.lock().expect("lock") = body.to_string();
    }

    pub fn retrieve(&self, _link_id: &str) -> (u16, String) {
        (
            *self.retrieve_status.lock().expect("lock"),
            self.retrieve_body.lock().expect("lock").clone(),
        )
    }
}
