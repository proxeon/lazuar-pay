//! Test rail webhook parse. No sqlx. Port of `TestWebhook.cs`.

use hmac::{Hmac, Mac};
use sha2::Sha256;
use subtle::ConstantTimeEq;

pub const SIGNATURE_HEADER: &str = "X-Pay-Test-Signature";

#[derive(Debug, thiserror::Error, PartialEq, Eq)]
pub enum TestParseError {
    #[error("webhook secret missing")]
    SecretMissing,
    #[error("invalid signature")]
    InvalidSignature,
    #[error("invalid event")]
    InvalidEvent,
    #[error("missing event id")]
    MissingEventId,
    #[error("missing checkout id")]
    MissingCheckoutId,
    #[error("missing amount")]
    MissingAmount,
    #[error("missing currency")]
    MissingCurrency,
}

#[derive(Debug, Clone)]
pub struct TestEvent {
    pub event_id: String,
    pub checkout_id: String,
    pub failed: bool,
    pub amount_minor: Option<i64>,
    pub currency: Option<String>,
}

type HmacSha256 = Hmac<Sha256>;

pub fn parse_webhook(
    body: &[u8],
    signature: Option<&str>,
    secret: &str,
) -> Result<TestEvent, TestParseError> {
    if secret.is_empty() {
        return Err(TestParseError::SecretMissing);
    }
    let provided = signature.map(str::trim).filter(|s| !s.is_empty());
    let Some(got) = provided else {
        return Err(TestParseError::InvalidSignature);
    };
    let mut mac = HmacSha256::new_from_slice(secret.as_bytes())
        .map_err(|_| TestParseError::InvalidSignature)?;
    mac.update(body);
    let expected = hex::encode(mac.finalize().into_bytes());
    let left = expected.as_bytes();
    let right = got.to_ascii_lowercase();
    let right = right.as_bytes();
    if left.len() != right.len() || !bool::from(left.ct_eq(right)) {
        return Err(TestParseError::InvalidSignature);
    }

    let root: serde_json::Value =
        serde_json::from_slice(body).map_err(|_| TestParseError::InvalidEvent)?;
    let event_id = root
        .get("id")
        .and_then(|v| v.as_str())
        .filter(|s| !s.is_empty())
        .ok_or(TestParseError::MissingEventId)?
        .to_string();
    let checkout_id = root
        .get("checkout_id")
        .and_then(|v| v.as_str())
        .filter(|s| !s.is_empty())
        .ok_or(TestParseError::MissingCheckoutId)?
        .to_string();
    let failed = root
        .get("status")
        .and_then(|v| v.as_str())
        .is_some_and(|s| s.eq_ignore_ascii_case("failed"));
    let amount_minor = root.get("amount_total").and_then(|v| v.as_i64());
    if !failed && amount_minor.is_none() {
        return Err(TestParseError::MissingAmount);
    }
    let currency = root
        .get("currency")
        .and_then(|v| v.as_str())
        .map(|s| s.trim().to_ascii_uppercase())
        .filter(|s| !s.is_empty());
    if !failed && currency.is_none() {
        return Err(TestParseError::MissingCurrency);
    }
    Ok(TestEvent {
        event_id,
        checkout_id,
        failed,
        amount_minor,
        currency,
    })
}
