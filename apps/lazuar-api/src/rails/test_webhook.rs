//! Port of `Rails/Test/TestWebhook.cs` — the dev/test no-op rail.
//! Fenced out of production at five doors; this parser exists only so the
//! suite can drive the full webhook pipeline without PSP credentials.

use serde_json::Value;

use crate::domain::currency;
use crate::webhooks::psp_parse::{Headers, ParsedWebhook, WebhookParseError};

pub const SIGNATURE_HEADER: &str = "X-Pay-Test-Signature";

pub fn parse(
    raw: &str,
    headers: &Headers,
    test_webhook_secret: &str,
) -> Result<ParsedWebhook, WebhookParseError> {
    if test_webhook_secret.trim().is_empty() {
        // 503 path: a configured rail whose secret is missing is a server fault.
        return Err(WebhookParseError::MissingSecret("webhook secret missing".into()));
    }

    let Some(provided) = headers.get(SIGNATURE_HEADER).map(str::trim).filter(|s| !s.is_empty()) else {
        return Err(WebhookParseError::verify_error("invalid signature"));
    };

    let expected = test_hmac_hex(test_webhook_secret, raw);
    if !fixed_time_eq_hex(provided.trim().to_lowercase().as_bytes(), expected.as_bytes()) {
        return Err(WebhookParseError::verify_error("invalid signature"));
    }

    let root: Value = serde_json::from_str(raw)
        .map_err(|_| WebhookParseError::verify_error("invalid event"))?;

    let event_id = root.get("id").and_then(Value::as_str).unwrap_or("");
    if event_id.trim().is_empty() {
        return Err(WebhookParseError::verify_error("missing event id"));
    }

    let checkout_id = root
        .get("checkout_id")
        .and_then(Value::as_str)
        .map(str::to_string)
        .filter(|s| !s.trim().is_empty());
    let Some(checkout_id) = checkout_id else {
        return Err(WebhookParseError::verify_error("missing checkout id"));
    };

    let failed = root
        .get("status")
        .and_then(Value::as_str)
        .is_some_and(|s| s.eq_ignore_ascii_case("failed"));

    let mut amount_minor = None;
    if let Some(a) = root.get("amount_total").and_then(Value::as_i64) {
        amount_minor = Some(a);
    } else if !failed {
        return Err(WebhookParseError::verify_error("missing amount"));
    }

    let currency = root
        .get("currency")
        .and_then(Value::as_str)
        .and_then(|c| currency::try_normalize_currency(Some(c)));

    if !failed && currency.is_none() {
        return Err(WebhookParseError::verify_error("missing currency"));
    }

    Ok(ParsedWebhook {
        event_id: event_id.to_string(),
        checkout_id: Some(checkout_id),
        provider_ref: Some(event_id.to_string()),
        amount_minor,
        currency,
        failed,
        ignore_reason: failed.then(|| "payment_failed".to_string()),
        ..ParsedWebhook::default()
    })
}

/// HMAC-SHA256(secret, body), lowercase hex — computed with fixed-time compare.
pub fn test_hmac_hex(secret: &str, body: &str) -> String {
    use hmac::{Hmac, Mac};
    use sha2::Sha256;
    let mut mac = <Hmac<Sha256> as Mac>::new_from_slice(secret.as_bytes())
        .expect("hmac accepts any key length");
    mac.update(body.as_bytes());
    hex::encode(mac.finalize().into_bytes())
}

pub fn fixed_time_eq_hex(provided: &[u8], expected: &[u8]) -> bool {
    use subtle::ConstantTimeEq;
    provided.len() == expected.len() && bool::from(provided.ct_eq(expected))
}
