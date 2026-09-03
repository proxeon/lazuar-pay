//! Port of `Rails/Stripe/StripeWebhook.cs` — native implementation of Stripe's
//! signature scheme (`t=ts,v1=hmac_sha256(whsec, "{ts}.{payload}")`, ±300s
//! tolerance) instead of the Stripe SDK, so the port carries no SDK dependency.
//! 5xx-after-send ambiguity is the caller's concern (issue 001); a definitive
//! <500 reject maps to `PspVerifyException` here.

use chrono::Utc;
use serde_json::Value;

use crate::domain::currency;
use crate::secrets::SecretBox;
use crate::webhooks::psp_parse::{Headers, ParsedWebhook, WebhookParseError};

pub const SIGNATURE_HEADER: &str = "Stripe-Signature";

struct StripeSig {
    timestamp: i64,
    v1: Option<String>,
}

fn parse_signature_header(header: &str) -> Option<StripeSig> {
    let mut timestamp = None;
    let mut v1 = None;
    for part in header.split(',') {
        let mut kv = part.trim().splitn(2, '=');
        let key = kv.next()?.trim();
        let value = kv.next()?.trim();
        match key {
            "t" => timestamp = value.parse().ok(),
            "v1" => v1 = Some(value.to_string()),
            _ => {}
        }
    }
    Some(StripeSig { timestamp: timestamp?, v1 })
}

fn expected_v1(whsec: &str, timestamp: i64, payload: &str) -> String {
    use hmac::{Hmac, Mac};
    use sha2::Sha256;
    let mut mac = <Hmac<Sha256> as Mac>::new_from_slice(whsec.as_bytes())
        .expect("hmac accepts any key length");
    mac.update(format!("{timestamp}.{payload}").as_bytes());
    hex::encode(mac.finalize().into_bytes())
}

/// Stripe's signature tolerance: replay protection without clock-fascism.
const TOLERANCE_SECONDS: i64 = 300;

/// Resolve the webhook secret: per-org vault ciphertext first; the process
/// secret only in Testing; else none — which 503s upstream.
pub fn resolve_secret(
    cred_webhook_ciphertext: Option<&str>,
    box_one: &SecretBox,
    config_secret: Option<&str>,
    environment: &str,
) -> Option<String> {
    if let Some(ct) = cred_webhook_ciphertext.map(str::trim).filter(|s| !s.is_empty()) {
        return box_one.unprotect(ct).ok();
    }
    if environment == "Testing" {
        return config_secret.map(str::to_string).filter(|s| !s.trim().is_empty());
    }
    None
}

pub fn parse(
    raw: &str,
    headers: &Headers,
    whsec: &str,
) -> Result<ParsedWebhook, WebhookParseError> {
    if whsec.trim().is_empty() {
        return Err(WebhookParseError::MissingSecret("webhook secret missing".into()));
    }

    let Some(sig) = headers.get(SIGNATURE_HEADER) else {
        return Err(WebhookParseError::verify_error("invalid signature"));
    };
    let Some(parsed_sig) = parse_signature_header(sig) else {
        return Err(WebhookParseError::verify_error("invalid signature"));
    };

    let age = Utc::now().timestamp() - parsed_sig.timestamp;
    if age.abs() > TOLERANCE_SECONDS {
        return Err(WebhookParseError::verify_error("invalid signature"));
    }
    let Some(v1) = &parsed_sig.v1 else {
        return Err(WebhookParseError::verify_error("invalid signature"));
    };
    let expected = expected_v1(whsec, parsed_sig.timestamp, raw);
    if !fixed_time_eq(v1.trim().as_bytes(), expected.as_bytes()) {
        return Err(WebhookParseError::verify_error("invalid signature"));
    }

    let root: Value = serde_json::from_str(raw)
        .map_err(|_| WebhookParseError::verify_error("invalid event"))?;

    let event_id = root.get("id").and_then(Value::as_str).unwrap_or("");
    if event_id.is_empty() {
        return Err(WebhookParseError::verify_error("invalid event"));
    }
    let event_type = root.get("type").and_then(Value::as_str).unwrap_or("");
    let session = root.pointer("/data/object");

    if event_type == "checkout.session.async_payment_failed" {
        return Ok(ParsedWebhook {
            event_id: event_id.into(),
            failed: true,
            ignore_reason: Some("async_payment_failed".into()),
            checkout_id: session
                .and_then(|s| str_or_meta_checkout(s))
                .or_else(|| session.and_then(|s| s.get("client_reference_id")).and_then(Value::as_str).map(str::to_string)),
            hosted_session_id: session.and_then(|s| s.get("id")).and_then(Value::as_str).map(str::to_string),
            provider_ref: session.and_then(|s| s.get("id")).and_then(Value::as_str).map(str::to_string),
            ..ParsedWebhook::default()
        });
    }

    if event_type != "checkout.session.completed" && event_type != "checkout.session.async_payment_succeeded" {
        return Ok(ParsedWebhook {
            event_id: event_id.into(),
            ignored: true,
            ignore_reason: Some(event_type.to_string()),
            ..ParsedWebhook::default()
        });
    }

    let Some(session) = session else {
        return Ok(ParsedWebhook {
            event_id: event_id.into(),
            ignored: true,
            ignore_reason: Some("no_session".into()),
            ..ParsedWebhook::default()
        });
    };

    let mode = session.get("mode").and_then(Value::as_str);
    let amount_total = session.get("amount_total").and_then(Value::as_i64);
    if mode == Some("setup") || amount_total.is_none_or(|a| a == 0) {
        return Ok(ParsedWebhook {
            event_id: event_id.into(),
            ignored: true,
            ignore_reason: Some("setup_or_zero".into()),
            ..ParsedWebhook::default()
        });
    }

    let payment_status = session.get("payment_status").and_then(Value::as_str);
    if event_type == "checkout.session.completed"
        && !matches!(payment_status, Some("paid") | Some("no_payment_required"))
    {
        return Ok(ParsedWebhook {
            event_id: event_id.into(),
            ignored: true,
            ignore_reason: Some(format!("payment_status:{}", payment_status.unwrap_or("missing"))),
            ..ParsedWebhook::default()
        });
    }

    let checkout_id = session
        .get("client_reference_id")
        .and_then(Value::as_str)
        .map(str::to_string)
        .or_else(|| str_or_meta_checkout(session));
    let currency = session
        .get("currency")
        .and_then(Value::as_str)
        .and_then(|c| currency::try_normalize_currency(Some(c)))
        .ok_or_else(|| WebhookParseError::verify_error("missing currency"))?;
    // AmountTotal is Stripe cents (minor). Do not ToMinor again.

    Ok(ParsedWebhook {
        event_id: event_id.into(),
        checkout_id,
        hosted_session_id: session.get("id").and_then(Value::as_str).map(str::to_string),
        provider_ref: session.get("id").and_then(Value::as_str).map(str::to_string),
        amount_minor: amount_total,
        currency: Some(currency),
        ..ParsedWebhook::default()
    })
}

fn str_or_meta_checkout(session: &Value) -> Option<String> {
    session
        .get("metadata")
        .and_then(|m| m.get("checkout_id"))
        .and_then(Value::as_str)
        .map(str::to_string)
}

pub fn fixed_time_eq(provided: &[u8], expected: &[u8]) -> bool {
    use subtle::ConstantTimeEq;
    provided.len() == expected.len() && bool::from(provided.ct_eq(expected))
}

/// Build a signed Stripe-style payload for fixtures/tests (the C# suite used
/// the SDK to mint these; the port mints them natively).
pub fn sign_fixture(whsec: &str, payload: &str, timestamp: i64) -> String {
    format!("t={timestamp},v1={}", expected_v1(whsec, timestamp, payload))
}
