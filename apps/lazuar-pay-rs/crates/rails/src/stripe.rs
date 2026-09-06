//! Stripe hosted Checkout. Parse has no sqlx. AmountTotal is already minor.

use domain::money::{Currency, Money};
use domain::proof::{Binding, SyncOutcome, WebhookOutcome};
use domain::rail::{ConnectorRefs, HostedSession};
use domain::PaymentId;
use hmac::{Hmac, Mac};
use sha2::Sha256;
use subtle::ConstantTimeEq;

pub const SIGNATURE_HEADER: &str = "Stripe-Signature";
pub const API_BASE: &str = "https://api.stripe.com";

#[derive(Debug, thiserror::Error, PartialEq, Eq)]
pub enum StripeParseError {
    #[error("invalid signature")]
    InvalidSignature,
    #[error("invalid event")]
    InvalidEvent,
    #[error("missing currency")]
    MissingCurrency,
}

#[derive(Debug, Clone)]
pub struct StripePaid {
    pub event_id: String,
    pub outcome: WebhookOutcome,
}

type HmacSha256 = Hmac<Sha256>;

pub fn sign(secret: &str, body: &[u8], unix: i64) -> String {
    let mut mac = HmacSha256::new_from_slice(secret.as_bytes()).expect("hmac");
    mac.update(unix.to_string().as_bytes());
    mac.update(b".");
    mac.update(body);
    format!("t={unix},v1={}", hex::encode(mac.finalize().into_bytes()))
}

fn parse_sig_header(header: &str) -> Option<(i64, String)> {
    let mut t = None;
    let mut v1 = None;
    for part in header.split(',') {
        let part = part.trim();
        if let Some(rest) = part.strip_prefix("t=") {
            t = rest.parse().ok();
        } else if let Some(rest) = part.strip_prefix("v1=") {
            v1 = Some(rest.to_ascii_lowercase());
        }
    }
    Some((t?, v1?))
}

fn verify(secret: &str, body: &[u8], header: &str, now_unix: i64) -> Result<(), StripeParseError> {
    let (t, v1) = parse_sig_header(header).ok_or(StripeParseError::InvalidSignature)?;
    if (now_unix - t).abs() > 300 {
        return Err(StripeParseError::InvalidSignature);
    }
    let mut mac = HmacSha256::new_from_slice(secret.as_bytes())
        .map_err(|_| StripeParseError::InvalidSignature)?;
    mac.update(t.to_string().as_bytes());
    mac.update(b".");
    mac.update(body);
    let expected = hex::encode(mac.finalize().into_bytes());
    let left = expected.as_bytes();
    let right = v1.as_bytes();
    if left.len() != right.len() || !bool::from(left.ct_eq(right)) {
        return Err(StripeParseError::InvalidSignature);
    }
    Ok(())
}

fn payment_intent_id(session: &serde_json::Value) -> Option<String> {
    match session.get("payment_intent") {
        Some(serde_json::Value::String(s)) if s.starts_with("pi_") => Some(s.clone()),
        Some(serde_json::Value::Object(o)) => o
            .get("id")
            .and_then(|v| v.as_str())
            .filter(|s| s.starts_with("pi_"))
            .map(str::to_string),
        _ => None,
    }
}

fn binding(session: &serde_json::Value) -> Binding {
    let checkout = session
        .get("client_reference_id")
        .and_then(|v| v.as_str())
        .filter(|s| !s.is_empty())
        .or_else(|| {
            session
                .get("metadata")
                .and_then(|m| m.get("checkout_id"))
                .and_then(|v| v.as_str())
                .filter(|s| !s.is_empty())
        });
    if let Some(id) = checkout.and_then(|s| PaymentId::from_wire(s).ok()) {
        Binding::Payment { id }
    } else {
        Binding::Session {
            session_id: session
                .get("id")
                .and_then(|v| v.as_str())
                .unwrap_or("")
                .to_string(),
        }
    }
}

fn paid_from_session(session: &serde_json::Value) -> Result<WebhookOutcome, StripeParseError> {
    let amount = session
        .get("amount_total")
        .and_then(|v| v.as_i64())
        .ok_or(StripeParseError::InvalidEvent)?;
    let ccy = session
        .get("currency")
        .and_then(|v| v.as_str())
        .map(|s| s.trim().to_ascii_uppercase())
        .filter(|s| !s.is_empty())
        .ok_or(StripeParseError::MissingCurrency)?;
    let currency = Currency::by_code(&ccy).ok_or(StripeParseError::MissingCurrency)?;
    let received = Money::from_minor(i128::from(amount), currency)
        .map_err(|_| StripeParseError::InvalidEvent)?;
    let cs = session
        .get("id")
        .and_then(|v| v.as_str())
        .unwrap_or("")
        .to_string();
    Ok(WebhookOutcome::Paid {
        binding: binding(session),
        received,
        refs: ConnectorRefs {
            session_id: Some(cs),
            capture_id: payment_intent_id(session),
            network_id: None,
        },
    })
}

/// Verify Stripe-Signature and map to [`WebhookOutcome`]. `now_unix` for 300s skew.
pub fn parse_webhook(
    body: &[u8],
    signature: Option<&str>,
    secret: &str,
    now_unix: i64,
) -> Result<(String, WebhookOutcome), StripeParseError> {
    let sig = signature
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .ok_or(StripeParseError::InvalidSignature)?;
    verify(secret, body, sig, now_unix)?;
    let root: serde_json::Value =
        serde_json::from_slice(body).map_err(|_| StripeParseError::InvalidEvent)?;
    let event_id = root
        .get("id")
        .and_then(|v| v.as_str())
        .filter(|s| !s.is_empty())
        .ok_or(StripeParseError::InvalidEvent)?
        .to_string();
    let typ = root
        .get("type")
        .and_then(|v| v.as_str())
        .unwrap_or("")
        .to_string();
    let session = root.get("data").and_then(|d| d.get("object"));

    if typ == "checkout.session.async_payment_failed" {
        let binding = session.map(binding).unwrap_or(Binding::Session {
            session_id: String::new(),
        });
        return Ok((
            event_id,
            WebhookOutcome::Failed {
                binding,
                reason: "async_payment_failed".into(),
            },
        ));
    }

    if typ != "checkout.session.completed" && typ != "checkout.session.async_payment_succeeded" {
        return Ok((
            event_id,
            WebhookOutcome::Ignored {
                reason: domain::IgnoreReason::UnknownEvent,
            },
        ));
    }

    let Some(session) = session else {
        return Ok((
            event_id,
            WebhookOutcome::Ignored {
                reason: domain::IgnoreReason::UnrecognizedShape,
            },
        ));
    };

    let mode = session.get("mode").and_then(|v| v.as_str()).unwrap_or("");
    let amount = session
        .get("amount_total")
        .and_then(|v| v.as_i64())
        .unwrap_or(0);
    if mode == "setup" || amount == 0 {
        return Ok((
            event_id,
            WebhookOutcome::Ignored {
                reason: domain::IgnoreReason::UnrecognizedShape,
            },
        ));
    }

    if typ == "checkout.session.completed" {
        let ps = session
            .get("payment_status")
            .and_then(|v| v.as_str())
            .unwrap_or("");
        if ps != "paid" && ps != "no_payment_required" {
            return Ok((
                event_id,
                WebhookOutcome::Ignored {
                    reason: domain::IgnoreReason::UnrecognizedShape,
                },
            ));
        }
    }

    Ok((event_id, paid_from_session(session)?))
}

/// HTTP ignore reason string for `{ignored: ...}` (C# uses the type / payment_status).
pub fn ignore_detail(
    outcome: &WebhookOutcome,
    event_type: &str,
    session: Option<&serde_json::Value>,
) -> String {
    match outcome {
        WebhookOutcome::Ignored { .. } => {
            if event_type == "checkout.session.completed" {
                if let Some(s) = session {
                    let mode = s.get("mode").and_then(|v| v.as_str()).unwrap_or("");
                    let amt = s.get("amount_total").and_then(|v| v.as_i64()).unwrap_or(0);
                    if mode == "setup" || amt == 0 {
                        return "setup_or_zero".into();
                    }
                    let ps = s
                        .get("payment_status")
                        .and_then(|v| v.as_str())
                        .unwrap_or("missing");
                    if ps != "paid" && ps != "no_payment_required" {
                        return format!("payment_status:{ps}");
                    }
                }
            }
            event_type.to_string()
        }
        _ => event_type.to_string(),
    }
}

pub fn checkout_form(
    payment_id: &str,
    org_id: &str,
    amount_minor: i64,
    currency: &str,
    success_url: &str,
    cancel_url: &str,
) -> Vec<(String, String)> {
    vec![
        ("mode".into(), "payment".into()),
        ("client_reference_id".into(), payment_id.into()),
        ("success_url".into(), success_url.into()),
        ("cancel_url".into(), cancel_url.into()),
        ("metadata[checkout_id]".into(), payment_id.into()),
        ("metadata[org_id]".into(), org_id.into()),
        ("line_items[0][quantity]".into(), "1".into()),
        (
            "line_items[0][price_data][currency]".into(),
            currency.to_ascii_lowercase(),
        ),
        (
            "line_items[0][price_data][unit_amount]".into(),
            amount_minor.to_string(),
        ),
        (
            "line_items[0][price_data][product_data][name]".into(),
            "Pay".into(),
        ),
    ]
}

pub fn parse_hosted(body: &str) -> Result<HostedSession, StripeParseError> {
    let v: serde_json::Value =
        serde_json::from_str(body).map_err(|_| StripeParseError::InvalidEvent)?;
    let id = v
        .get("id")
        .and_then(|x| x.as_str())
        .filter(|s| s.starts_with("cs_"))
        .ok_or(StripeParseError::InvalidEvent)?;
    let url = v
        .get("url")
        .and_then(|x| x.as_str())
        .filter(|s| !s.is_empty())
        .ok_or(StripeParseError::InvalidEvent)?;
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
    let ps = v
        .get("payment_status")
        .and_then(|x| x.as_str())
        .unwrap_or("");
    let st = v.get("status").and_then(|x| x.as_str()).unwrap_or("");
    if ps == "paid" || ps == "no_payment_required" {
        let amount = v.get("amount_total").and_then(|x| x.as_i64()).unwrap_or(0);
        let ccy = v
            .get("currency")
            .and_then(|x| x.as_str())
            .unwrap_or("myr")
            .to_ascii_uppercase();
        let currency = Currency::by_code(&ccy).unwrap_or(Currency::MYR);
        let Ok(received) = Money::from_minor(i128::from(amount), currency) else {
            return SyncOutcome::Unknown;
        };
        let cs = v
            .get("id")
            .and_then(|x| x.as_str())
            .unwrap_or("")
            .to_string();
        return SyncOutcome::Paid {
            received,
            refs: ConnectorRefs {
                session_id: Some(cs),
                capture_id: payment_intent_id(&v),
                network_id: None,
            },
        };
    }
    if st == "expired" && ps != "paid" && ps != "no_payment_required" {
        return SyncOutcome::Failed {
            reason: "expired".into(),
        };
    }
    SyncOutcome::Unknown
}

pub fn map_refund(status: u16, body: &str) -> domain::RefundOutcome {
    if status == 0 || status >= 500 {
        return domain::RefundOutcome::Unknown;
    }
    if (200..300).contains(&status) {
        return domain::RefundOutcome::Settled;
    }
    if body.contains("charge_already_refunded") {
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

pub fn capture_id_from_body(body: &str) -> Option<String> {
    let v: serde_json::Value = serde_json::from_str(body).ok()?;
    payment_intent_id(&v)
}

/// In-memory Stripe. CI never calls api.stripe.com.
#[derive(Clone)]
pub struct FakeStripe {
    pub last_unit_amount: std::sync::Arc<std::sync::Mutex<Option<i64>>>,
    pub last_refund_pi: std::sync::Arc<std::sync::Mutex<Option<String>>>,
    retrieve_status: std::sync::Arc<std::sync::Mutex<u16>>,
    retrieve_body: std::sync::Arc<std::sync::Mutex<String>>,
    refund_status: std::sync::Arc<std::sync::Mutex<u16>>,
    refund_body: std::sync::Arc<std::sync::Mutex<String>>,
}

impl Default for FakeStripe {
    fn default() -> Self {
        Self {
            last_unit_amount: std::sync::Arc::new(std::sync::Mutex::new(None)),
            last_refund_pi: std::sync::Arc::new(std::sync::Mutex::new(None)),
            retrieve_status: std::sync::Arc::new(std::sync::Mutex::new(200)),
            retrieve_body: std::sync::Arc::new(std::sync::Mutex::new(
                r#"{"id":"cs_test_1","status":"open","payment_status":"unpaid","amount_total":1000,"currency":"myr"}"#
                    .into(),
            )),
            refund_status: std::sync::Arc::new(std::sync::Mutex::new(200)),
            refund_body: std::sync::Arc::new(std::sync::Mutex::new(
                r#"{"id":"re_1","status":"succeeded"}"#.into(),
            )),
        }
    }
}

impl FakeStripe {
    pub fn create_session(
        &self,
        form: &[(String, String)],
    ) -> Result<HostedSession, StripeParseError> {
        let ua = form
            .iter()
            .find(|(k, _)| k.contains("unit_amount"))
            .and_then(|(_, v)| v.parse().ok());
        *self.last_unit_amount.lock().expect("lock") = ua;
        parse_hosted(
            r#"{"id":"cs_test_1","object":"checkout.session","url":"https://checkout.stripe.com/c/cs_test_1","payment_status":"unpaid"}"#,
        )
    }

    pub fn set_retrieve(&self, status: u16, body: &str) {
        *self.retrieve_status.lock().expect("lock") = status;
        *self.retrieve_body.lock().expect("lock") = body.to_string();
    }

    pub fn set_refund(&self, status: u16, body: &str) {
        *self.refund_status.lock().expect("lock") = status;
        *self.refund_body.lock().expect("lock") = body.to_string();
    }

    pub fn retrieve(&self, _session_id: &str) -> (u16, String) {
        (
            *self.retrieve_status.lock().expect("lock"),
            self.retrieve_body.lock().expect("lock").clone(),
        )
    }

    pub fn refund(&self, payment_intent: &str, _amount_minor: i64, _idem: &str) -> (u16, String) {
        *self.last_refund_pi.lock().expect("lock") = Some(payment_intent.to_string());
        (
            *self.refund_status.lock().expect("lock"),
            self.refund_body.lock().expect("lock").clone(),
        )
    }
}
