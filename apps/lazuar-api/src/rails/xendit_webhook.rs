//! Port of `Rails/Xendit/XenditWebhook.cs`.
//! Hash-first token compare so token length is not a timing oracle.

use sha2::{Digest, Sha256};
use serde_json::Value;
use subtle::ConstantTimeEq;

use crate::domain::currency;
use crate::secrets::SecretBox;
use crate::webhooks::psp_parse::{ParsedWebhook, WebhookParseError};

pub const CALLBACK_TOKEN_HEADER: &str = "x-callback-token";

pub fn parse(
    raw: &str,
    headers: &crate::webhooks::psp_parse::Headers,
    cred_webhook_ciphertext: Option<&str>,
    box_one: &SecretBox,
) -> Result<ParsedWebhook, WebhookParseError> {
    let Some(ct) = cred_webhook_ciphertext.map(str::trim).filter(|s| !s.is_empty()) else {
        return Err(WebhookParseError::MissingSecret("webhook secret missing".into()));
    };

    let expected = box_one.unprotect(ct).map_err(|_| WebhookParseError::verify_error("invalid signature"))?;
    let provided = headers.get(CALLBACK_TOKEN_HEADER).unwrap_or("");

    // Hash first so token length is not a timing oracle (Hub 073 judgment).
    let left = Sha256::digest(provided.as_bytes());
    let right = Sha256::digest(expected.as_bytes());
    if !bool::from(left.as_slice().ct_eq(right.as_slice())) {
        return Err(WebhookParseError::verify_error("invalid signature"));
    }

    let root: Value = serde_json::from_str(raw)
        .map_err(|_| WebhookParseError::verify_error("invalid event"))?;
    let invoice = root.get("data").filter(|d| d.is_object()).unwrap_or(&root);

    let status = read_string(invoice, "status")
        .or_else(|| read_string(&root, "event"))
        .unwrap_or_default();
    let invoice_id = read_string(invoice, "id").unwrap_or_default();
    if invoice_id.trim().is_empty() {
        return Err(WebhookParseError::verify_error("missing invoice id"));
    }

    if status.eq_ignore_ascii_case("SETTLED") || status.eq_ignore_ascii_case("invoice.settled") {
        return Ok(ParsedWebhook {
            event_id: format!("settled:{invoice_id}"),
            ignored: true,
            ignore_reason: Some("settled".into()),
            ..ParsedWebhook::default()
        });
    }

    let paid = status.eq_ignore_ascii_case("PAID") || status.eq_ignore_ascii_case("invoice.paid");
    if !paid {
        return Ok(ParsedWebhook {
            event_id: format!("{status}:{invoice_id}"),
            ignored: true,
            ignore_reason: Some(status.to_string()),
            ..ParsedWebhook::default()
        });
    }

    let currency = read_string(invoice, "currency")
        .as_deref()
        .and_then(|c| currency::try_normalize_currency(Some(c)))
        .ok_or_else(|| WebhookParseError::verify_error("missing currency"))?;

    // Invoice paid_amount is major units (10.00), then ToMinor for match.
    let amount = invoice
        .get("paid_amount")
        .and_then(Value::as_f64)
        .or_else(|| invoice.get("amount").and_then(Value::as_f64))
        .unwrap_or(0.0);

    let checkout_id = invoice
        .get("metadata")
        .and_then(|m| m.get("checkout_id"))
        .and_then(Value::as_str)
        .map(str::to_string)
        .or_else(|| read_string(invoice, "external_id"));

    Ok(ParsedWebhook {
        event_id: format!("paid:{invoice_id}"),
        checkout_id,
        hosted_session_id: Some(invoice_id.to_string()),
        provider_ref: Some(invoice_id.to_string()),
        amount_minor: Some(crate::domain::currency::to_minor(
            rust_decimal::Decimal::try_from(amount).unwrap_or_default(),
        )),
        currency: Some(currency),
        ..ParsedWebhook::default()
    })
}

fn read_string(el: &Value, name: &str) -> Option<String> {
    el.get(name).and_then(Value::as_str).map(str::to_string)
}
