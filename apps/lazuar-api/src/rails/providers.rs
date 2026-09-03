//! Port of `Rails/PayProviders.cs` — canonical provider names and rules.
//!
//! C# returns `out string` + bool; Rust returns `Option<&'static str>`, which
//! carries the same "normalized or not" information in the type.

pub const STRIPE: &str = "stripe";
pub const CHIP: &str = "chip";
pub const BILLPLZ: &str = "billplz";
pub const XENDIT: &str = "xendit";
pub const RAZORPAY: &str = "razorpay";
pub const SOLANA: &str = "solana";
pub const TEST: &str = "test";

pub const CAPABILITY: &str = "hosted_link";

pub const ALL: [&str; 6] = [STRIPE, CHIP, BILLPLZ, XENDIT, RAZORPAY, SOLANA];

/// Rails listed for merchants. The fake `test` rail is fenced out of production
/// at five separate doors in C# (checkout create, link create, webhook ingest,
/// gateway vault, hosted start); `allows_test` here drives the same listing rule.
pub fn listed(environment: &str) -> Vec<&'static str> {
    let mut all = ALL.to_vec();
    if allows_test(environment) {
        all.push(TEST);
    }
    all
}

pub fn allows_test(environment: &str) -> bool {
    environment == "Development" || environment == "Testing"
}

pub fn is_test(provider: &str) -> bool {
    provider == TEST
}

/// Normalize a raw provider string to the canonical constant.
pub fn try_normalize(raw: Option<&str>) -> Option<&'static str> {
    let normalized = raw.unwrap_or("").trim().to_lowercase();
    // `const` values are unique, so matching on them is exact.
    match normalized.as_str() {
        STRIPE => Some(STRIPE),
        CHIP => Some(CHIP),
        BILLPLZ => Some(BILLPLZ),
        XENDIT => Some(XENDIT),
        RAZORPAY => Some(RAZORPAY),
        SOLANA => Some(SOLANA),
        TEST => Some(TEST),
        _ => None,
    }
}

pub fn is_solana(provider: &str) -> bool {
    provider == SOLANA
}

pub fn uses_receive_address(provider: &str) -> bool {
    provider == SOLANA
}

pub fn uses_catalog_product(provider: &str) -> bool {
    provider != SOLANA
}

/// Solana Pay transfer-request URLs are the "on-page" rail.
pub fn is_on_page_url(url: Option<&str>) -> bool {
    url.is_some_and(|u| u.starts_with("solana:"))
}

pub fn requires_public_merchant_id(provider: &str) -> bool {
    provider == CHIP || provider == BILLPLZ || provider == SOLANA
}

pub fn requires_email(provider: &str) -> bool {
    provider != STRIPE && provider != TEST && provider != SOLANA
}

pub fn allows_public_merchant_id(provider: &str) -> bool {
    requires_public_merchant_id(provider)
}

/// Normalize the Solana environment; "mainnet-beta" collapses to "mainnet".
pub fn try_normalize_solana_environment(raw: Option<&str>) -> Option<&'static str> {
    let normalized = raw.unwrap_or("").trim().to_lowercase();
    match normalized.as_str() {
        "mainnet-beta" => Some("mainnet"),
        "devnet" => Some("devnet"),
        "mainnet" => Some("mainnet"),
        _ => None,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn try_normalize_is_case_insensitive_and_canonical() {
        assert_eq!(try_normalize(Some(" Stripe ")), Some(STRIPE));
        assert_eq!(try_normalize(Some("SOLANA")), Some(SOLANA));
        assert_eq!(try_normalize(Some("nope")), None);
        assert_eq!(try_normalize(None), None);
    }

    #[test]
    fn test_rail_is_listed_only_in_dev_environments() {
        assert!(listed("Development").contains(&TEST));
        assert!(listed("Testing").contains(&TEST));
        assert!(!listed("Production").contains(&TEST));
        assert!(!listed("Production").is_empty());
    }

    #[test]
    fn solana_environment_aliases_mainnet_beta() {
        assert_eq!(try_normalize_solana_environment(Some("Mainnet-Beta")), Some("mainnet"));
        assert_eq!(try_normalize_solana_environment(Some("devnet")), Some("devnet"));
        assert_eq!(try_normalize_solana_environment(Some("testnet")), None);
    }
}
