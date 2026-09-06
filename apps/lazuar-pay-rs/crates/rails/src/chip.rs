//! CHIP hosted purchase. Parse has no sqlx. purchase.total is already sen.
//! Refund is not auto-settled (`refund_idempotent = false`).

use domain::money::{Currency, Money};
use domain::proof::{Binding, IgnoreReason, SyncOutcome, WebhookOutcome};
use domain::rail::{ConnectorRefs, HostedSession};
use domain::PaymentId;
use rsa::pkcs1::{DecodeRsaPrivateKey, DecodeRsaPublicKey};
use rsa::pkcs1v15::{Signature, SigningKey, VerifyingKey};
use rsa::pkcs8::{DecodePrivateKey, DecodePublicKey};
use rsa::sha2::Sha256;
use rsa::signature::{SignatureEncoding, Signer, Verifier};
use rsa::traits::PublicKeyParts;
use rsa::RsaPublicKey;
use serde_json::{json, Value};

pub const SIGNATURE_HEADER: &str = "X-Signature";
pub const API_BASE: &str = "https://gate.chip-in.asia/api/v1";

#[derive(Debug, thiserror::Error, PartialEq, Eq)]
pub enum ChipParseError {
    #[error("invalid signature")]
    InvalidSignature,
    #[error("invalid event")]
    InvalidEvent,
    #[error("missing purchase id")]
    MissingPurchaseId,
    #[error("missing currency")]
    MissingCurrency,
}

/// PUT gate: ImportFromPem + RSA ≥ 2048 (032/15 lock 6).
pub fn pem_ok(pem: &str) -> bool {
    parse_public_pem(pem).is_ok_and(|k| k.size() >= 256)
}

fn parse_public_pem(pem: &str) -> Result<RsaPublicKey, ChipParseError> {
    RsaPublicKey::from_public_key_pem(pem)
        .or_else(|_| RsaPublicKey::from_pkcs1_pem(pem))
        .map_err(|_| ChipParseError::InvalidSignature)
}

pub fn sign(private_pem: &str, body: &[u8]) -> Result<String, ChipParseError> {
    let key = rsa::RsaPrivateKey::from_pkcs1_pem(private_pem)
        .or_else(|_| rsa::RsaPrivateKey::from_pkcs8_pem(private_pem))
        .map_err(|_| ChipParseError::InvalidSignature)?;
    let signing = SigningKey::<Sha256>::new(key);
    let sig = signing.sign(body);
    Ok(base64::Engine::encode(
        &base64::engine::general_purpose::STANDARD,
        sig.to_bytes(),
    ))
}

fn verify(pem: &str, body: &[u8], header: &str) -> Result<(), ChipParseError> {
    let pub_key = parse_public_pem(pem)?;
    let verifying = VerifyingKey::<Sha256>::new(pub_key);
    let raw = base64::Engine::decode(&base64::engine::general_purpose::STANDARD, header.trim())
        .map_err(|_| ChipParseError::InvalidSignature)?;
    let sig = Signature::try_from(raw.as_slice()).map_err(|_| ChipParseError::InvalidSignature)?;
    verifying
        .verify(body, &sig)
        .map_err(|_| ChipParseError::InvalidSignature)
}

fn json_i64(v: &Value) -> Option<i64> {
    v.as_i64()
        .or_else(|| v.as_u64().and_then(|u| i64::try_from(u).ok()))
}

fn purchase_id(root: &Value) -> Option<String> {
    root.get("purchase")
        .and_then(|p| p.get("id"))
        .and_then(|v| v.as_str())
        .filter(|s| !s.is_empty())
        .map(str::to_string)
        .or_else(|| {
            root.get("id")
                .and_then(|v| v.as_str())
                .filter(|s| !s.is_empty())
                .map(str::to_string)
        })
}

fn binding(purchase: Option<&Value>, purchase_id: &str) -> Binding {
    let checkout = purchase
        .and_then(|p| p.get("metadata"))
        .and_then(|m| m.get("checkout_id"))
        .and_then(|v| v.as_str())
        .filter(|s| !s.is_empty());
    if let Some(id) = checkout.and_then(|s| PaymentId::from_wire(s).ok()) {
        Binding::Payment { id }
    } else {
        Binding::Session {
            session_id: purchase_id.to_string(),
        }
    }
}

fn refs(purchase_id: &str) -> ConnectorRefs {
    ConnectorRefs {
        session_id: Some(purchase_id.to_string()),
        capture_id: Some(purchase_id.to_string()),
        network_id: None,
    }
}

/// Verify X-Signature and map to [`WebhookOutcome`]. First return is proof_id.
pub fn parse_webhook(
    body: &[u8],
    signature: Option<&str>,
    pem: &str,
) -> Result<(String, WebhookOutcome), ChipParseError> {
    if body.iter().all(|b| b.is_ascii_whitespace()) {
        return Err(ChipParseError::InvalidEvent);
    }
    let sig = signature
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .ok_or(ChipParseError::InvalidSignature)?;
    verify(pem, body, sig)?;
    let root: Value = serde_json::from_slice(body).map_err(|_| ChipParseError::InvalidEvent)?;
    let pid = purchase_id(&root).ok_or(ChipParseError::MissingPurchaseId)?;
    let event_type = root
        .get("event_type")
        .and_then(|v| v.as_str())
        .unwrap_or("");
    let purchase = root.get("purchase").filter(|p| p.is_object());

    if event_type == "purchase.preauthorized" {
        return Ok((
            format!("preauth:{pid}"),
            WebhookOutcome::Ignored {
                reason: IgnoreReason::Preauthorized,
            },
        ));
    }
    if event_type == "purchase.payment_failure" {
        return Ok((
            format!("failed:{pid}"),
            WebhookOutcome::Failed {
                binding: binding(purchase, &pid),
                reason: "payment_failure".into(),
            },
        ));
    }
    if event_type != "purchase.paid" {
        return Ok((
            format!("{event_type}:{pid}"),
            WebhookOutcome::Ignored {
                reason: IgnoreReason::UnknownEvent,
            },
        ));
    }

    let total = purchase
        .and_then(|p| p.get("total"))
        .and_then(json_i64)
        .ok_or(ChipParseError::InvalidEvent)?;
    let ccy = purchase
        .and_then(|p| p.get("currency"))
        .and_then(|v| v.as_str())
        .map(|s| s.trim().to_ascii_uppercase())
        .filter(|s| !s.is_empty())
        .ok_or(ChipParseError::MissingCurrency)?;
    let currency = Currency::by_code(&ccy).ok_or(ChipParseError::MissingCurrency)?;
    let received =
        Money::from_minor(i128::from(total), currency).map_err(|_| ChipParseError::InvalidEvent)?;
    Ok((
        format!("paid:{pid}"),
        WebhookOutcome::Paid {
            binding: binding(purchase, &pid),
            received,
            refs: refs(&pid),
        },
    ))
}

pub fn ignore_detail(outcome: &WebhookOutcome, event_type: &str) -> String {
    match outcome {
        WebhookOutcome::Ignored {
            reason: IgnoreReason::Preauthorized,
        } => "preauthorized".into(),
        WebhookOutcome::Ignored { .. } => {
            if event_type.is_empty() {
                "chip".into()
            } else {
                event_type.to_string()
            }
        }
        _ => event_type.to_string(),
    }
}

#[allow(clippy::too_many_arguments)]
pub fn purchase_body(
    payment_id: &str,
    org_id: &str,
    brand_id: &str,
    email: &str,
    full_name: &str,
    amount_minor: i64,
    currency: &str,
    success_url: &str,
    cancel_url: &str,
) -> Value {
    json!({
        "brand_id": brand_id,
        "client": { "email": email, "full_name": full_name },
        "purchase": {
            "currency": currency,
            "products": [{ "name": "Pay", "price": amount_minor }],
            "metadata": { "checkout_id": payment_id, "org_id": org_id }
        },
        "success_redirect": success_url,
        "failure_redirect": cancel_url,
        "cancel_redirect": cancel_url
    })
}

pub fn parse_hosted(body: &str) -> Result<HostedSession, ChipParseError> {
    let v: Value = serde_json::from_str(body).map_err(|_| ChipParseError::InvalidEvent)?;
    let id = v
        .get("id")
        .and_then(|x| x.as_str())
        .filter(|s| !s.is_empty())
        .ok_or(ChipParseError::InvalidEvent)?;
    let url = v
        .get("checkout_url")
        .and_then(|x| x.as_str())
        .filter(|s| !s.is_empty())
        .ok_or(ChipParseError::InvalidEvent)?;
    Ok(HostedSession {
        url: url.into(),
        session_id: id.into(),
    })
}

fn status_of(v: &Value) -> String {
    v.get("status")
        .and_then(|x| x.as_str())
        .or_else(|| {
            v.get("purchase")
                .and_then(|p| p.get("status"))
                .and_then(|x| x.as_str())
        })
        .unwrap_or("")
        .trim()
        .to_ascii_lowercase()
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
    let st = status_of(&v);
    if st == "paid" || st == "view" {
        let purchase = v.get("purchase").unwrap_or(&v);
        let amount = purchase.get("total").and_then(json_i64).unwrap_or(0);
        let ccy = purchase
            .get("currency")
            .and_then(|x| x.as_str())
            .unwrap_or("myr")
            .to_ascii_uppercase();
        let currency = Currency::by_code(&ccy).unwrap_or(Currency::MYR);
        let Ok(received) = Money::from_minor(i128::from(amount), currency) else {
            return SyncOutcome::Unknown;
        };
        let pid = purchase_id(&v).unwrap_or_default();
        return SyncOutcome::Paid {
            received,
            refs: refs(&pid),
        };
    }
    if matches!(
        st.as_str(),
        "error" | "cancelled" | "canceled" | "overdue" | "expired"
    ) {
        return SyncOutcome::Failed { reason: st };
    }
    SyncOutcome::Unknown
}

pub fn map_refund(status: u16, _body: &str) -> domain::RefundOutcome {
    if status == 0 || status >= 500 {
        return domain::RefundOutcome::Unknown;
    }
    if (200..300).contains(&status) {
        return domain::RefundOutcome::Settled;
    }
    domain::RefundOutcome::Rejected
}

pub fn mint_idempotency_key(payment_id_hex: &str) -> String {
    format!("lazuar-checkout:{payment_id_hex}")
}

pub fn refund_idempotency_key(refund_id: &str) -> String {
    format!("lazuar-refund:{refund_id}")
}

static FAKE_SEQ: std::sync::atomic::AtomicU64 = std::sync::atomic::AtomicU64::new(1);

/// In-memory CHIP. CI never calls gate.chip-in.asia.
#[derive(Clone)]
pub struct FakeChip {
    pub last_price: std::sync::Arc<std::sync::Mutex<Option<i64>>>,
    pub last_email: std::sync::Arc<std::sync::Mutex<Option<String>>>,
    pub last_body: std::sync::Arc<std::sync::Mutex<Option<String>>>,
    retrieve_status: std::sync::Arc<std::sync::Mutex<u16>>,
    retrieve_body: std::sync::Arc<std::sync::Mutex<String>>,
}

impl Default for FakeChip {
    fn default() -> Self {
        Self {
            last_price: std::sync::Arc::new(std::sync::Mutex::new(None)),
            last_email: std::sync::Arc::new(std::sync::Mutex::new(None)),
            last_body: std::sync::Arc::new(std::sync::Mutex::new(None)),
            retrieve_status: std::sync::Arc::new(std::sync::Mutex::new(200)),
            retrieve_body: std::sync::Arc::new(std::sync::Mutex::new(
                r#"{"id":"purch_1","status":"pending","purchase":{"total":1000,"currency":"MYR"}}"#
                    .into(),
            )),
        }
    }
}

impl FakeChip {
    pub fn create_purchase(&self, body: &Value) -> Result<HostedSession, ChipParseError> {
        let dumped = body.to_string();
        let price = body
            .pointer("/purchase/products/0/price")
            .and_then(json_i64);
        let email = body
            .pointer("/client/email")
            .and_then(|v| v.as_str())
            .map(str::to_string);
        *self.last_price.lock().expect("lock") = price;
        *self.last_email.lock().expect("lock") = email;
        *self.last_body.lock().expect("lock") = Some(dumped);
        let n = FAKE_SEQ.fetch_add(1, std::sync::atomic::Ordering::Relaxed);
        parse_hosted(&format!(
            r#"{{"id":"purch_{n}","checkout_url":"https://gate.chip-in.asia/p/x"}}"#
        ))
    }

    pub fn set_retrieve(&self, status: u16, body: &str) {
        *self.retrieve_status.lock().expect("lock") = status;
        *self.retrieve_body.lock().expect("lock") = body.to_string();
    }

    pub fn retrieve(&self, _purchase_id: &str) -> (u16, String) {
        (
            *self.retrieve_status.lock().expect("lock"),
            self.retrieve_body.lock().expect("lock").clone(),
        )
    }
}
