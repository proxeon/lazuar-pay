//! Port of `Rails/Razorpay/RazorpayWebhook.cs`.
//! Issue 018: the rotating `X-Razorpay-Event-Id` header is the dedupe id when
//! present — replaying a captured event with a fresh header id must NOT book a
//! second fulfillment, and the header id (not the payment id) is what the
//! `(Org, Provider, EventId)` dedupe sees.

use hmac::{Hmac, Mac};
use serde_json::Value;
use sha2::Sha256;
use subtle::ConstantTimeEq;

use crate::domain::currency;
use crate::secrets::SecretBox;
use crate::webhooks::psp_parse::{Headers, ParsedWebhook, WebhookParseError};

pub const SIGNATURE_HEADER: &str = "X-Razorpay-Signature";
pub const EVENT_ID_HEADER: &str = "X-Razorpay-Event-Id";

fn header<'a>(headers: &'a Headers, name: &str) -> Option<&'a str> {
    headers.get(name).map(str::trim).filter(|v| !v.is_empty())
}

pub fn parse(
    raw: &str,
    headers: &Headers,
    cred_webhook_ciphertext: Option<&str>,
    box_one: &SecretBox,
) -> Result<ParsedWebhook, WebhookParseError> {
    let Some(ct) = cred_webhook_ciphertext.map(str::trim).filter(|s| !s.is_empty()) else {
        return Err(WebhookParseError::MissingSecret("webhook secret missing".into()));
    };

    let Some(signature) = header(headers, SIGNATURE_HEADER).filter(|s| !s.is_empty()) else {
        return Err(WebhookParseError::verify_error("invalid signature"));
    };

    let secret = box_one.unprotect(ct).map_err(|_| WebhookParseError::verify_error("invalid signature"))?;
    let mut mac = <Hmac<Sha256> as Mac>::new_from_slice(secret.as_bytes())
        .expect("hmac accepts any key length");
    mac.update(raw.as_bytes());
    let expected = hex::encode(mac.finalize().into_bytes());
    let provided = signature.trim().to_lowercase();
    if !bool::from(provided.as_bytes().ct_eq(expected.as_bytes())) {
        return Err(WebhookParseError::verify_error("invalid signature"));
    }

    let root: Value = serde_json::from_str(raw)
        .map_err(|_| WebhookParseError::verify_error("invalid event"))?;
    let event_type = root.get("event").and_then(Value::as_str);
    let entity = root.pointer("/payload/payment/entity");
    let payment_id = entity.and_then(|e| e.get("id")).and_then(Value::as_str).map(str::to_string);
    let header_event_id = header(headers, EVENT_ID_HEADER).map(str::to_string);

    if event_type == Some("payment.failed") {
        let failed_id = header_event_id.clone().or_else(|| {
            payment_id.as_deref().filter(|p| !p.trim().is_empty()).map(|p| format!("failed:{p}"))
        });
        let Some(failed_id) = failed_id else {
            return Err(WebhookParseError::verify_error("missing event id"));
        };
        let failed_checkout = entity
            .and_then(|e| e.get("notes"))
            .and_then(|n| n.get("checkout_id"))
            .and_then(Value::as_str)
            .map(str::to_string);
        return Ok(ParsedWebhook {
            event_id: failed_id,
            failed: true,
            ignore_reason: Some("payment_failed".into()),
            checkout_id: failed_checkout,
            provider_ref: payment_id,
            ..ParsedWebhook::default()
        });
    }

    let is_captured = event_type == Some("payment.captured");
    if matches!(
        event_type,
        Some("payment_link.paid") | Some("payment_link.expired") | Some("order.paid")
    ) || !is_captured
    {
        let other_id = header_event_id.clone().unwrap_or_else(|| {
            match payment_id.as_deref().filter(|p| !p.trim().is_empty()) {
                Some(p) => format!("{}:{p}", event_type.unwrap_or("razorpay")),
                None => format!("{}:none", event_type.unwrap_or("razorpay")),
            }
        });
        return Ok(ParsedWebhook {
            event_id: other_id,
            ignored: true,
            ignore_reason: event_type.map(str::to_string),
            ..ParsedWebhook::default()
        });
    }

    let Some(entity) = entity else {
        return Err(WebhookParseError::verify_error("missing payment id"));
    };
    let Some(payment_id) = payment_id else {
        return Err(WebhookParseError::verify_error("missing payment id"));
    };

    let currency = entity
        .get("currency")
        .and_then(Value::as_str)
        .and_then(|c| currency::try_normalize_currency(Some(c)))
        .ok_or_else(|| WebhookParseError::verify_error("missing currency"))?;

    // Payment entity amount is already minor (paise/sen). RM10.00 → 1000.
    let amount = entity.get("amount").and_then(Value::as_i64).unwrap_or(0);

    let checkout_id = entity
        .get("notes")
        .and_then(|n| n.get("checkout_id"))
        .and_then(Value::as_str)
        .map(str::to_string);

    let hosted_session_id = root
        .pointer("/payload/payment_link/entity/id")
        .and_then(Value::as_str)
        .map(str::to_string);

    let event_id = match header_event_id {
        Some(id) if !id.trim().is_empty() => id,
        _ => format!("captured:{payment_id}"),
    };

    Ok(ParsedWebhook {
        event_id,
        checkout_id,
        hosted_session_id,
        provider_ref: Some(payment_id),
        amount_minor: Some(amount),
        currency: Some(currency),
        ..ParsedWebhook::default()
    })
}
