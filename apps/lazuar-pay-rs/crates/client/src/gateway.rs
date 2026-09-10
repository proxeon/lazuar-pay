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
        if rails::solana::try_normalize(brand).is_none() {
            return Err(Error::Config(
                "public_merchant_id must be a Solana wallet address".into(),
            ));
        }
        // Host requires devnet|mainnet (mainnet-beta → mainnet).
        if rails::solana::normalize_vault_env(field(body, "environment")).is_none() {
            return Err(Error::Config(
                "environment must be devnet or mainnet".into(),
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

    let mut assembled = secret.to_string();
    let has_split = !kid.is_empty() && !ksec.is_empty();
    if assembled.is_empty() && has_split {
        assembled = format!("{kid}:{ksec}");
    }
    if assembled.is_empty() {
        return Err(Error::Config("secret is required".into()));
    }
    if provider == "razorpay" && rails::razorpay::try_split(&assembled).is_none() {
        return Err(Error::Config("secret must be key_id:key_secret".into()));
    }
    if webhook.is_empty() {
        return Err(Error::Config("webhook_secret is required".into()));
    }
    if provider == "chip" && !rails::chip::pem_ok(webhook) {
        return Err(Error::Config("webhook_secret must be a CHIP PEM".into()));
    }
    let env_in = field(body, "environment").to_ascii_lowercase();
    if !env_in.is_empty() && env_in != "test" && env_in != "live" {
        return Err(Error::Config("environment must be test or live".into()));
    }
    if provider == "billplz" && env_in.is_empty() {
        return Err(Error::Config("environment is required".into()));
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

    #[test]
    fn chip_rejects_non_pem() {
        let err = validate_gateway_put(&json!({
            "provider": "chip",
            "secret": "chipkey",
            "webhook_secret": "not-a-pem",
            "public_merchant_id": "brand_1",
            "environment": "test"
        }))
        .unwrap_err();
        assert!(err.to_string().contains("CHIP PEM"), "{err}");
    }

    #[test]
    fn billplz_requires_environment() {
        let err = validate_gateway_put(&json!({
            "provider": "billplz",
            "secret": "bp_sk",
            "webhook_secret": "xsig",
            "public_merchant_id": "col_1"
        }))
        .unwrap_err();
        assert!(err.to_string().contains("environment"), "{err}");
    }

    #[test]
    fn razorpay_requires_colon_secret() {
        let err = validate_gateway_put(&json!({
            "provider": "razorpay",
            "secret": "rzp_only",
            "webhook_secret": "hook",
            "environment": "test"
        }))
        .unwrap_err();
        assert!(err.to_string().contains("key_id:key_secret"), "{err}");
    }

    #[test]
    fn solana_requires_environment() {
        let err = validate_gateway_put(&json!({
            "provider": "solana",
            "public_merchant_id": "4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU"
        }))
        .unwrap_err();
        assert!(err.to_string().contains("devnet or mainnet"), "{err}");
    }
}
