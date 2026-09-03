//! Port of `Rails/Billplz/BillplzWebhook.cs`.
//!
//! Bind only from the HMAC-signed form. The callback URL's `?checkout_id=`
//! query is NOT covered by `x_signature`, so trusting it would let a replayed
//! paid body be aimed at any same-org checkout. `reference_1` is stamped with
//! the checkout id at bill create; a mismatched bill id still resolves via
//! HostedSessionId in the ingest flow.

use std::collections::BTreeMap;

use hmac::{Hmac, Mac};
use sha2::Sha256;
use subtle::ConstantTimeEq;

use crate::domain::currency;
use crate::secrets::SecretBox;
use crate::webhooks::psp_parse::{ParsedWebhook, WebhookParseError};

const EXTRA_FIELDS: [&str; 3] = ["paid_at", "transaction_id", "transaction_status"];

/// Case-insensitive form map (BTreeMap gives the Ord sorting ComputeHmac needs).
pub type Form = BTreeMap<String, String>;

/// Port of `ParseForm` — urlencoded body, last value wins, keys upper-cased for
/// case-insensitive lookups (C# used OrdinalIgnoreCase dictionary keys).
pub fn parse_form(raw: &str) -> Form {
    let mut map = Form::new();
    for pair in raw.split('&') {
        if pair.is_empty() {
            continue;
        }
        let (key, value) = match pair.split_once('=') {
            Some((k, v)) => (k, v),
            None => (pair, ""),
        };
        let key = urlencoding::decode(key).unwrap_or_default();
        let value = urlencoding::decode(value).unwrap_or_default();
        // C# keys keep original case but compare ignoring case; here we store
        // the raw key and do case-insensitive lookups via `form_get`.
        map.insert(key.to_string(), value.to_string());
    }
    map
}

pub fn form_get<'a>(form: &'a Form, name: &str) -> Option<&'a str> {
    form.iter()
        .find(|(k, _)| k.eq_ignore_ascii_case(name))
        .map(|(_, v)| v.as_str())
}

pub fn compute_hmac(form: &Form, secret_key: &str, exclude_extra: bool) -> String {
    let mut elements: Vec<String> = form
        .iter()
        .filter(|(k, _)| {
            if k.eq_ignore_ascii_case("x_signature") {
                return false;
            }
            if exclude_extra && EXTRA_FIELDS.iter().any(|f| k.eq_ignore_ascii_case(f)) {
                return false;
            }
            true
        })
        .map(|(k, v)| format!("{k}{v}"))
        .collect();
    elements.sort(); // String Ord == Ordinal
    let source = elements.join("|");
    let mut mac = <Hmac<Sha256> as Mac>::new_from_slice(secret_key.as_bytes())
        .expect("hmac accepts any key length");
    mac.update(source.as_bytes());
    hex::encode(mac.finalize().into_bytes())
}

fn fixed_time_equals_hex(provided: &str, computed: &str) -> bool {
    let left = provided.trim().to_lowercase();
    let right = computed.trim().to_lowercase();
    bool::from(left.as_bytes().ct_eq(right.as_bytes()))
}

pub fn parse(
    raw: &str,
    cred_webhook_ciphertext: Option<&str>,
    box_one: &SecretBox,
) -> Result<ParsedWebhook, WebhookParseError> {
    let Some(ct) = cred_webhook_ciphertext.map(str::trim).filter(|s| !s.is_empty()) else {
        return Err(WebhookParseError::MissingSecret("webhook secret missing".into()));
    };

    let form = parse_form(raw);
    let Some(provided) = form_get(&form, "x_signature").filter(|s| !s.is_empty()) else {
        return Err(WebhookParseError::verify_error("invalid signature"));
    };

    let secret = box_one.unprotect(ct).map_err(|_| WebhookParseError::verify_error("invalid signature"))?;
    let with_extra = compute_hmac(&form, &secret, false);
    if !fixed_time_equals_hex(provided, &with_extra) {
        let without = compute_hmac(&form, &secret, true);
        if !fixed_time_equals_hex(provided, &without) {
            return Err(WebhookParseError::verify_error("invalid signature"));
        }
    }

    let bill_id = form_get(&form, "id").unwrap_or("");
    if bill_id.trim().is_empty() {
        return Err(WebhookParseError::verify_error("missing bill id"));
    }

    let paid = form_get(&form, "paid").unwrap_or("false");
    let state = form_get(&form, "state").unwrap_or("due");
    let is_paid = paid.eq_ignore_ascii_case("true") || state.eq_ignore_ascii_case("paid");
    if !is_paid {
        return Ok(ParsedWebhook {
            event_id: format!("unpaid:{bill_id}"),
            ignored: true,
            ignore_reason: Some("unpaid".into()),
            ..ParsedWebhook::default()
        });
    }

    let checkout_id = form_get(&form, "reference_1").unwrap_or("");
    // Form paid_amount is sen (minor). RM10.00 → 1000.
    let paid_cents = form_get(&form, "paid_amount").and_then(|p| p.parse::<i64>().ok()).unwrap_or(0);
    let currency = form_get(&form, "currency")
        .and_then(|c| currency::try_normalize_currency(Some(c)))
        .ok_or_else(|| WebhookParseError::verify_error("missing currency"))?;

    Ok(ParsedWebhook {
        event_id: format!("paid:{bill_id}"),
        checkout_id: (!checkout_id.trim().is_empty()).then(|| checkout_id.to_string()),
        hosted_session_id: Some(bill_id.to_string()),
        provider_ref: Some(bill_id.to_string()),
        amount_minor: Some(paid_cents),
        currency: Some(currency),
        ..ParsedWebhook::default()
    })
}
