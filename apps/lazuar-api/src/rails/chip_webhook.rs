//! Port of `Rails/Chip/ChipWebhook.cs` — RSA-2048 SHA256 PKCS1 verify over the
//! raw body with the merchant's PEM public key (vaulted via SecretBox).

use base64::engine::general_purpose::STANDARD as B64;
use base64::Engine;
use rsa::pkcs1v15::{Signature, VerifyingKey};
use rsa::signature::Verifier;
use rsa::RsaPublicKey;
use serde_json::Value;
use sha2::Sha256;

use crate::domain::currency;
use crate::secrets::SecretBox;
use crate::webhooks::psp_parse::{Headers, ParsedWebhook, WebhookParseError};

pub const SIGNATURE_HEADER: &str = "X-Signature";

pub fn parse(
    raw: &str,
    headers: &Headers,
    cred_webhook_ciphertext: Option<&str>,
    box_one: &SecretBox,
) -> Result<ParsedWebhook, WebhookParseError> {
    let Some(sig) = headers.get(SIGNATURE_HEADER).map(str::trim).filter(|s| !s.is_empty()) else {
        return Err(WebhookParseError::verify_error("invalid signature"));
    };
    let Some(ct) = cred_webhook_ciphertext.map(str::trim).filter(|s| !s.is_empty()) else {
        return Err(WebhookParseError::MissingSecret("webhook secret missing".into()));
    };

    let pem = box_one.unprotect(ct).map_err(|e| { eprintln!("chip step failed: {e}"); WebhookParseError::verify_error("invalid signature") })?;
    let signature_bytes = B64.decode(sig).map_err(|e| { eprintln!("chip step failed: {e}"); WebhookParseError::verify_error("invalid signature") })?;

    let Ok(pem_str) = std::str::from_utf8(pem.as_bytes()) else {
        eprintln!("chip step failed: pem not utf8");
        return Err(WebhookParseError::verify_error("invalid signature"));
    };
    // C# ImportFromPem accepts both SPKI ("PUBLIC KEY") and PKCS1 ("RSA PUBLIC KEY").
    let public_key = <RsaPublicKey as rsa::pkcs8::DecodePublicKey>::from_public_key_pem(pem_str)
        .or_else(|_| <RsaPublicKey as rsa::pkcs1::DecodeRsaPublicKey>::from_pkcs1_pem(pem_str))
        .map_err(|e| { eprintln!("chip step failed: {e}"); WebhookParseError::verify_error("invalid signature") })?;
    let verify_key = VerifyingKey::<Sha256>::new(public_key);
    let Ok(signature) = Signature::try_from(&signature_bytes[..]) else {
        eprintln!("chip step failed: signature slice");
        return Err(WebhookParseError::verify_error("invalid signature"));
    };
    verify_key
        .verify(raw.as_bytes(), &signature)
        .map_err(|e| { eprintln!("chip step failed: {e}"); WebhookParseError::verify_error("invalid signature") })?;

    let root: Value = serde_json::from_str(raw)
        .map_err(|_| WebhookParseError::verify_error("invalid event"))?;
    let event_type = root.get("event_type").and_then(Value::as_str);
    let purchase_id = read_stable_purchase_id(&root).unwrap_or_default();
    if purchase_id.trim().is_empty() {
        return Err(WebhookParseError::verify_error("missing purchase id"));
    }

    if event_type == Some("purchase.preauthorized") {
        return Ok(ParsedWebhook {
            event_id: format!("preauth:{purchase_id}"),
            ignored: true,
            ignore_reason: Some("preauthorized".into()),
            ..ParsedWebhook::default()
        });
    }

    if event_type == Some("purchase.payment_failure") {
        let failed_checkout = root
            .pointer("/purchase/metadata/checkout_id")
            .and_then(Value::as_str)
            .map(str::to_string);
        return Ok(ParsedWebhook {
            event_id: format!("failed:{purchase_id}"),
            failed: true,
            ignore_reason: Some("payment_failure".into()),
            checkout_id: failed_checkout,
            hosted_session_id: Some(purchase_id.to_string()),
            provider_ref: Some(purchase_id.to_string()),
            ..ParsedWebhook::default()
        });
    }

    if event_type != Some("purchase.paid") {
        return Ok(ParsedWebhook {
            event_id: format!("{}:{purchase_id}", event_type.unwrap_or("chip")),
            ignored: true,
            ignore_reason: event_type.map(str::to_string),
            ..ParsedWebhook::default()
        });
    }

    let purchase = root.get("purchase").filter(|p| p.is_object());
    // CHIP purchase.total is sen/cents. RM10.00 → 1000. Do not divide by 100.
    let total = purchase.and_then(|p| p.get("total")).and_then(Value::as_f64).unwrap_or(0.0);
    let currency = purchase
        .and_then(|p| p.get("currency"))
        .and_then(Value::as_str)
        .and_then(|c| currency::try_normalize_currency(Some(c)))
        .ok_or_else(|| WebhookParseError::verify_error("missing currency"))?;

    let checkout_id = purchase
        .and_then(|p| p.pointer("/metadata/checkout_id"))
        .and_then(Value::as_str)
        .map(str::to_string);

    Ok(ParsedWebhook {
        event_id: format!("paid:{purchase_id}"),
        checkout_id,
        hosted_session_id: Some(purchase_id.to_string()),
        provider_ref: Some(purchase_id.to_string()),
        amount_minor: Some(total as i64),
        currency: Some(currency),
        ..ParsedWebhook::default()
    })
}

fn read_stable_purchase_id(root: &Value) -> Option<String> {
    if let Some(id) = root.pointer("/purchase/id").and_then(Value::as_str) {
        if !id.trim().is_empty() {
            return Some(id.to_string());
        }
    }
    root.get("id")
        .and_then(Value::as_str)
        .map(str::to_string)
        .filter(|s| !s.trim().is_empty())
}
