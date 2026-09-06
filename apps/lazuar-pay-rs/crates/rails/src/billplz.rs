//! Billplz hosted bill. MYR only. No refund API in v1. Amount is already sen.

use std::collections::HashMap;

use domain::money::{Currency, Money};
use domain::proof::{Binding, IgnoreReason, SyncOutcome, WebhookOutcome};
use domain::rail::{ConnectorRefs, HostedSession};
use domain::PaymentId;
use hmac::{Hmac, Mac};
use sha2::Sha256;
use subtle::ConstantTimeEq;

pub const SANDBOX_HOST: &str = "https://www.billplz-sandbox.com/api/v3";
pub const LIVE_HOST: &str = "https://www.billplz.com/api/v3";

const EXTRA: &[&str] = &["paid_at", "transaction_id", "transaction_status"];

#[derive(Debug, thiserror::Error, PartialEq, Eq)]
pub enum BillplzParseError {
    #[error("invalid signature")]
    InvalidSignature,
    #[error("invalid event")]
    InvalidEvent,
    #[error("missing bill id")]
    MissingBillId,
}

type HmacSha256 = Hmac<Sha256>;

pub fn api_host(environment: &str) -> &'static str {
    if environment.eq_ignore_ascii_case("live") {
        LIVE_HOST
    } else {
        SANDBOX_HOST
    }
}

/// HTTPS, not loopback, not lazuar-local-dev.com (`BillplzHosted.TryPublicBase`).
pub fn public_base_ok(raw: &str) -> bool {
    let value = raw.trim().trim_end_matches('/');
    let rest = match value.split_once("://") {
        Some((scheme, rest)) if scheme.eq_ignore_ascii_case("https") => rest,
        _ => return false,
    };
    let hostport = rest
        .rsplit_once('@')
        .map(|(_, h)| h)
        .unwrap_or(rest)
        .split(['/', '?', '#'])
        .next()
        .unwrap_or("");
    let host = if let Some(inside) = hostport.strip_prefix('[') {
        inside.split(']').next().unwrap_or("")
    } else {
        hostport.split(':').next().unwrap_or("")
    };
    if host.is_empty() || host_is_loopback(host) || host_is_local_dev(host) {
        return false;
    }
    true
}

fn host_is_local_dev(host: &str) -> bool {
    host.to_ascii_lowercase().contains("lazuar-local-dev.com")
}

fn host_is_loopback(host: &str) -> bool {
    if host.eq_ignore_ascii_case("localhost") || host.eq_ignore_ascii_case("::1") {
        return true;
    }
    let mut parts = host.split('.');
    match (
        parts.next(),
        parts.next(),
        parts.next(),
        parts.next(),
        parts.next(),
    ) {
        (Some("127"), Some(b), Some(c), Some(d), None) => {
            b.parse::<u8>().is_ok() && c.parse::<u8>().is_ok() && d.parse::<u8>().is_ok()
        }
        _ => false,
    }
}

pub fn parse_form(body: &str) -> HashMap<String, String> {
    let mut out = HashMap::new();
    for pair in body.split('&') {
        if pair.is_empty() {
            continue;
        }
        let (k, v) = pair.split_once('=').unwrap_or((pair, ""));
        out.insert(percent_decode(k), percent_decode(v));
    }
    out
}

fn percent_decode(s: &str) -> String {
    let bytes = s.as_bytes();
    let mut out = Vec::with_capacity(bytes.len());
    let mut i = 0;
    while i < bytes.len() {
        if bytes[i] == b'%' && i + 2 < bytes.len() {
            if let (Some(a), Some(b)) = (from_hex(bytes[i + 1]), from_hex(bytes[i + 2])) {
                out.push((a << 4) | b);
                i += 3;
                continue;
            }
        }
        if bytes[i] == b'+' {
            out.push(b' ');
        } else {
            out.push(bytes[i]);
        }
        i += 1;
    }
    String::from_utf8_lossy(&out).into_owned()
}

fn from_hex(b: u8) -> Option<u8> {
    match b {
        b'0'..=b'9' => Some(b - b'0'),
        b'a'..=b'f' => Some(b - b'a' + 10),
        b'A'..=b'F' => Some(b - b'A' + 10),
        _ => None,
    }
}

fn form_get<'a>(form: &'a HashMap<String, String>, key: &str) -> &'a str {
    form.iter()
        .find(|(k, _)| k.eq_ignore_ascii_case(key))
        .map(|(_, v)| v.as_str())
        .unwrap_or("")
}

fn compute_hmac(form: &HashMap<String, String>, secret: &str, exclude_extra: bool) -> String {
    let mut elements: Vec<String> = form
        .iter()
        .filter(|(k, _)| !k.eq_ignore_ascii_case("x_signature"))
        .filter(|(k, _)| !(exclude_extra && EXTRA.iter().any(|e| k.eq_ignore_ascii_case(e))))
        .map(|(k, v)| format!("{k}{v}"))
        .collect();
    // C# OrderBy(e => e, StringComparer.Ordinal) on key+value concatenations.
    elements.sort_unstable();
    let source = elements.join("|");
    let mut mac = HmacSha256::new_from_slice(secret.as_bytes()).expect("hmac");
    mac.update(source.as_bytes());
    hex::encode(mac.finalize().into_bytes())
}

pub fn sign_form(form: &HashMap<String, String>, secret: &str) -> String {
    compute_hmac(form, secret, false)
}

pub fn signed_body(unsigned: &str, secret: &str) -> String {
    let form = parse_form(unsigned);
    let mac = sign_form(&form, secret);
    if unsigned.is_empty() {
        format!("x_signature={mac}")
    } else {
        format!("{unsigned}&x_signature={mac}")
    }
}

fn verify(form: &HashMap<String, String>, secret: &str, provided: &str) -> bool {
    let provided = provided.trim().to_ascii_lowercase();
    let with_extra = compute_hmac(form, secret, false);
    if ct_eq_hex(&provided, &with_extra) {
        return true;
    }
    let without = compute_hmac(form, secret, true);
    ct_eq_hex(&provided, &without)
}

fn ct_eq_hex(left: &str, right: &str) -> bool {
    let a = left.as_bytes();
    let b = right.as_bytes();
    a.len() == b.len() && bool::from(a.ct_eq(b))
}

fn refs(bill_id: &str) -> ConnectorRefs {
    ConnectorRefs {
        session_id: Some(bill_id.to_string()),
        capture_id: Some(bill_id.to_string()),
        network_id: None,
    }
}

fn binding(reference_1: &str, bill_id: &str) -> Binding {
    if let Ok(id) = PaymentId::from_wire(reference_1) {
        Binding::Payment { id }
    } else {
        Binding::Session {
            session_id: bill_id.to_string(),
        }
    }
}

/// Verify form `x_signature` (two-pass) and map to [`WebhookOutcome`].
pub fn parse_webhook(
    body: &[u8],
    secret: &str,
) -> Result<(String, WebhookOutcome), BillplzParseError> {
    if body.iter().all(|b| b.is_ascii_whitespace()) {
        return Err(BillplzParseError::InvalidEvent);
    }
    let raw = std::str::from_utf8(body).map_err(|_| BillplzParseError::InvalidEvent)?;
    let form = parse_form(raw);
    let provided = form_get(&form, "x_signature");
    if provided.is_empty() {
        return Err(BillplzParseError::InvalidSignature);
    }
    if !verify(&form, secret, provided) {
        return Err(BillplzParseError::InvalidSignature);
    }
    let bill_id = form_get(&form, "id");
    if bill_id.is_empty() {
        return Err(BillplzParseError::MissingBillId);
    }
    let paid = form_get(&form, "paid");
    let state = form_get(&form, "state");
    let is_paid = paid.eq_ignore_ascii_case("true") || state.eq_ignore_ascii_case("paid");
    if !is_paid {
        return Ok((
            format!("unpaid:{bill_id}"),
            WebhookOutcome::Ignored {
                reason: IgnoreReason::UnrecognizedShape,
            },
        ));
    }
    let paid_amount = form_get(&form, "paid_amount").parse::<i64>().unwrap_or(0);
    let raw_ccy = form_get(&form, "currency").trim();
    let currency = if raw_ccy.is_empty() {
        Currency::MYR
    } else {
        Currency::by_code(&raw_ccy.to_ascii_uppercase()).ok_or(BillplzParseError::InvalidEvent)?
    };
    let received = Money::from_minor(i128::from(paid_amount), currency)
        .map_err(|_| BillplzParseError::InvalidEvent)?;
    let reference_1 = form_get(&form, "reference_1");
    Ok((
        format!("paid:{bill_id}"),
        WebhookOutcome::Paid {
            binding: binding(reference_1, bill_id),
            received,
            refs: refs(bill_id),
        },
    ))
}

pub fn ignore_detail(outcome: &WebhookOutcome) -> String {
    match outcome {
        WebhookOutcome::Ignored { .. } => "unpaid".into(),
        _ => String::new(),
    }
}

#[allow(clippy::too_many_arguments)]
pub fn bill_body(
    payment_id: &str,
    collection_id: &str,
    email: &str,
    name: &str,
    amount_minor: i64,
    callback_url: &str,
    redirect_url: &str,
) -> serde_json::Value {
    serde_json::json!({
        "collection_id": collection_id,
        "email": email,
        "name": name,
        "amount": amount_minor,
        "description": "Pay",
        "callback_url": callback_url,
        "redirect_url": redirect_url,
        "reference_1_label": "Checkout",
        "reference_1": payment_id,
    })
}

pub fn parse_hosted(body: &str) -> Result<HostedSession, BillplzParseError> {
    let v: serde_json::Value =
        serde_json::from_str(body).map_err(|_| BillplzParseError::InvalidEvent)?;
    let id = v
        .get("id")
        .and_then(|x| x.as_str())
        .filter(|s| !s.is_empty())
        .ok_or(BillplzParseError::InvalidEvent)?;
    let url = v
        .get("url")
        .and_then(|x| x.as_str())
        .filter(|s| !s.is_empty())
        .ok_or(BillplzParseError::InvalidEvent)?;
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
    let Ok(v) = serde_json::from_str::<serde_json::Value>(body) else {
        return SyncOutcome::Unknown;
    };
    let paid = v.get("paid").and_then(|x| x.as_bool()).unwrap_or(false);
    let state = v
        .get("state")
        .and_then(|x| x.as_str())
        .unwrap_or("")
        .to_ascii_lowercase();
    if paid || state == "paid" {
        let amount = v
            .get("paid_amount")
            .and_then(|x| x.as_i64())
            .or_else(|| {
                v.get("paid_amount")
                    .and_then(|x| x.as_str())
                    .and_then(|s| s.parse().ok())
            })
            .unwrap_or(0);
        let Ok(received) = Money::from_minor(i128::from(amount), Currency::MYR) else {
            return SyncOutcome::Unknown;
        };
        let id = v
            .get("id")
            .and_then(|x| x.as_str())
            .unwrap_or("")
            .to_string();
        return SyncOutcome::Paid {
            received,
            refs: refs(&id),
        };
    }
    SyncOutcome::Unknown
}

pub fn mint_idempotency_key(payment_id_hex: &str) -> String {
    format!("lazuar-checkout:{payment_id_hex}")
}

static FAKE_SEQ: std::sync::atomic::AtomicU64 = std::sync::atomic::AtomicU64::new(1);

/// In-memory Billplz. CI never calls billplz.com.
#[derive(Clone)]
pub struct FakeBillplz {
    pub last_amount: std::sync::Arc<std::sync::Mutex<Option<i64>>>,
    pub last_email: std::sync::Arc<std::sync::Mutex<Option<String>>>,
    pub last_host: std::sync::Arc<std::sync::Mutex<Option<String>>>,
    pub last_body: std::sync::Arc<std::sync::Mutex<Option<String>>>,
    retrieve_status: std::sync::Arc<std::sync::Mutex<u16>>,
    retrieve_body: std::sync::Arc<std::sync::Mutex<String>>,
}

impl Default for FakeBillplz {
    fn default() -> Self {
        Self {
            last_amount: std::sync::Arc::new(std::sync::Mutex::new(None)),
            last_email: std::sync::Arc::new(std::sync::Mutex::new(None)),
            last_host: std::sync::Arc::new(std::sync::Mutex::new(None)),
            last_body: std::sync::Arc::new(std::sync::Mutex::new(None)),
            retrieve_status: std::sync::Arc::new(std::sync::Mutex::new(200)),
            retrieve_body: std::sync::Arc::new(std::sync::Mutex::new(
                r#"{"id":"bill_1","paid":false,"state":"due","paid_amount":0}"#.into(),
            )),
        }
    }
}

impl FakeBillplz {
    pub fn create_bill(
        &self,
        host: &str,
        body: &serde_json::Value,
    ) -> Result<HostedSession, BillplzParseError> {
        *self.last_host.lock().expect("lock") = Some(host.to_string());
        *self.last_amount.lock().expect("lock") = body.get("amount").and_then(|v| v.as_i64());
        *self.last_email.lock().expect("lock") = body
            .get("email")
            .and_then(|v| v.as_str())
            .map(str::to_string);
        *self.last_body.lock().expect("lock") = Some(body.to_string());
        let n = FAKE_SEQ.fetch_add(1, std::sync::atomic::Ordering::Relaxed);
        parse_hosted(&format!(
            r#"{{"id":"bill_{n}","url":"https://www.billplz-sandbox.com/bills/bill_{n}"}}"#
        ))
    }

    pub fn set_retrieve(&self, status: u16, body: &str) {
        *self.retrieve_status.lock().expect("lock") = status;
        *self.retrieve_body.lock().expect("lock") = body.to_string();
    }

    pub fn retrieve(&self, _bill_id: &str) -> (u16, String) {
        (
            *self.retrieve_status.lock().expect("lock"),
            self.retrieve_body.lock().expect("lock").clone(),
        )
    }
}

#[cfg(test)]
mod hmac_order {
    use super::*;

    #[test]
    fn sorts_key_value_concatenations_not_keys() {
        let mut form = HashMap::new();
        form.insert("id".into(), "x".into());
        form.insert("i".into(), "dy".into());
        let mut mac = HmacSha256::new_from_slice(b"xsig").expect("hmac");
        mac.update(b"idx|idy");
        let concat_sorted = hex::encode(mac.finalize().into_bytes());
        assert_eq!(compute_hmac(&form, "xsig", false), concat_sorted);
        let mut mac = HmacSha256::new_from_slice(b"xsig").expect("hmac");
        mac.update(b"idy|idx");
        let key_sorted = hex::encode(mac.finalize().into_bytes());
        assert_ne!(compute_hmac(&form, "xsig", false), key_sorted);
    }
}
