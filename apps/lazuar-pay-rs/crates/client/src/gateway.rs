//! BYOK vault body (TypeSpec `PutGateway`). Secrets live in a file the CLI reads;
//! this module never prints them. Host `/gateway` is still the oracle for PEM shape.

use serde_json::Value;

use crate::error::Error;

const RAILS: &[&str] = &["stripe", "chip", "billplz", "xendit", "razorpay", "solana"];

fn field<'a>(body: &'a Value, key: &str) -> &'a str {
    body.get(key).and_then(Value::as_str).unwrap_or("").trim()
}

/// Fail closed before HTTP so a Solana file with `secret` never leaves the laptop,
/// and `provider=test` is not "configured" by stuffing a dummy key.
pub fn validate_gateway_put(body: &Value) -> Result<(), Error> {
    let Some(obj) = body.as_object() else {
        return Err(Error::Config("gateway file must be a JSON object".into()));
    };
    let provider = field(body, "provider").to_ascii_lowercase();
    if provider.is_empty() {
        return Err(Error::Config("provider is required".into()));
    }
    if provider == "test" {
        return Err(Error::Config("test processor does not take secrets".into()));
    }
    if !RAILS.contains(&provider.as_str()) {
        return Err(Error::Config(format!("unknown provider: {provider}")));
    }

    let secret = field(body, "secret");
    let webhook = field(body, "webhook_secret");
    let kid = field(body, "key_id");
    let ksec = field(body, "key_secret");
    let brand = field(body, "public_merchant_id");

    if provider == "solana" {
        // Receive-only: a public address, never a signing key / PEM / sk_.
        if !secret.is_empty() || !kid.is_empty() || !ksec.is_empty() {
            return Err(Error::Config("solana does not take an API secret".into()));
        }
        if !webhook.is_empty() {
            return Err(Error::Config(
                "solana does not take a webhook secret".into(),
            ));
        }
        if brand.is_empty() {
            return Err(Error::Config(
                "public_merchant_id must be a Solana wallet address".into(),
            ));
        }
        return Ok(());
    }

    // Fiat rails: Stripe/Xendit/Razorpay do not use public_merchant_id (host 400).
    if matches!(provider.as_str(), "stripe" | "xendit" | "razorpay") && !brand.is_empty() {
        return Err(Error::Config(
            "public_merchant_id is not used for this provider".into(),
        ));
    }
    if matches!(provider.as_str(), "chip" | "billplz") && brand.is_empty() {
        return Err(Error::Config("public_merchant_id is required".into()));
    }

    let has_split = !kid.is_empty() && !ksec.is_empty();
    if secret.is_empty() && !has_split {
        return Err(Error::Config("secret is required".into()));
    }
    if webhook.is_empty() {
        return Err(Error::Config("webhook_secret is required".into()));
    }
    let _ = obj;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    #[test]
    fn test_rail_rejected() {
        let err = validate_gateway_put(&json!({"provider":"test","secret":"x"})).unwrap_err();
        assert!(err.to_string().contains("test processor"), "{err}");
    }

    #[test]
    fn solana_rejects_api_secret() {
        let err = validate_gateway_put(&json!({
            "provider": "solana",
            "secret": "sk_test_x",
            "public_merchant_id": "Addr1111111111111111111111111111111111111",
            "environment": "devnet"
        }))
        .unwrap_err();
        assert!(
            err.to_string().contains("does not take an API secret"),
            "{err}"
        );
    }

    #[test]
    fn solana_address_only_ok() {
        validate_gateway_put(&json!({
            "provider": "solana",
            "public_merchant_id": "4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU",
            "environment": "devnet"
        }))
        .unwrap();
    }

    #[test]
    fn stripe_ok() {
        validate_gateway_put(&json!({
            "provider": "stripe",
            "secret": "sk_test_dummy",
            "webhook_secret": "whsec_test",
            "environment": "test"
        }))
        .unwrap();
    }

    #[test]
    fn stripe_rejects_brand() {
        let err = validate_gateway_put(&json!({
            "provider": "stripe",
            "secret": "sk_test_dummy",
            "webhook_secret": "whsec_test",
            "public_merchant_id": "acct_1"
        }))
        .unwrap_err();
        assert!(err.to_string().contains("public_merchant_id"), "{err}");
    }

    #[test]
    fn razorpay_split_keys_count_as_secret() {
        validate_gateway_put(&json!({
            "provider": "razorpay",
            "key_id": "rzp_test_abc",
            "key_secret": "shh",
            "webhook_secret": "hook"
        }))
        .unwrap();
    }

    #[test]
    fn chip_needs_brand() {
        let err = validate_gateway_put(&json!({
            "provider": "chip",
            "secret": "chipkey",
            "webhook_secret": "-----BEGIN PUBLIC KEY-----\nM\n-----END PUBLIC KEY-----"
        }))
        .unwrap_err();
        assert!(err.to_string().contains("public_merchant_id"), "{err}");
    }
}
