//! Port of `SolanaMoney.cs` — USDC amount conversion to atomic units (6 dp).
//! Rejects subscriptions, catalog products, and non-USDC currencies.

use rust_decimal::Decimal;

use super::usdc::{USDC_CURRENCY, USDC_DECIMALS};
use crate::rails::providers;

/// Convert a USDC major-unit amount to atomic units (6 dp). `None` when the
/// value has more precision than USDC supports.
pub fn try_to_atomic(amount: Decimal) -> Option<i64> {
    if amount <= Decimal::ZERO {
        return None;
    }
    let scaled = amount * Decimal::from(10u32.pow(USDC_DECIMALS as u32));
    if !scaled.fract().is_zero() {
        return None;
    }
    let value = scaled.to_i64_checked()?;
    if value == i64::MAX {
        return None;
    }
    Some(value)
}

trait DecimalExt {
    fn to_i64_checked(&self) -> Option<i64>;
}

impl DecimalExt for Decimal {
    fn to_i64_checked(&self) -> Option<i64> {
        use rust_decimal::prelude::ToPrimitive;
        self.to_i64()
    }
}

/// The create-path currency/product rules for the Solana rail
/// (`SolanaMoney.MintError`): err message when the mint payload is invalid.
pub fn mint_error(
    provider: &str,
    currency: Option<&str>,
    interval: Option<&str>,
    product_id: Option<&str>,
    amount: Option<Decimal>,
) -> Option<String> {
    if !providers::is_solana(provider) {
        return None;
    }

    if interval == Some("mo") || interval == Some("yr") {
        return Some("solana does not support subscriptions".into());
    }

    if !providers::uses_catalog_product(provider) && product_id.map(str::trim).is_some_and(|p| !p.is_empty()) {
        return Some("solana does not use a MYR catalog product".into());
    }

    let Some(currency) = currency.map(str::trim).filter(|c| !c.is_empty()) else {
        return Some("solana currency must be USDC".into());
    };

    let normalized = currency.to_uppercase();
    if normalized == "MYR" {
        return Some("solana does not capture ringgit".into());
    }
    if normalized == "USD" {
        return Some("solana receives USDC, not USD".into());
    }
    if normalized != USDC_CURRENCY {
        return Some("solana currency must be USDC".into());
    }

    if let Some(value) = amount {
        if try_to_atomic(value).is_none() {
            return Some("amount is not a valid USDC amount".into());
        }
        let rounded = value.round_dp_with_strategy(2, rust_decimal::RoundingStrategy::AwayFromZero);
        if rounded != value {
            return Some("solana amounts support at most 2 decimal places".into());
        }
    }

    None
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::str::FromStr;

    #[test]
    fn to_atomic_scales_six_decimals_and_rejects_more() {
        assert_eq!(try_to_atomic(Decimal::from_str("1").unwrap()), Some(1_000_000));
        assert_eq!(try_to_atomic(Decimal::from_str("9.90").unwrap()), Some(9_900_000));
        assert_eq!(
            try_to_atomic(Decimal::from_str("0.000001").unwrap()),
            Some(1)
        );
        // More precision than USDC supports.
        assert_eq!(try_to_atomic(Decimal::from_str("0.0000001").unwrap()), None);
    }

    #[test]
    fn mint_error_rules() {
        assert_eq!(
            mint_error("solana", Some("MYR"), None, None, None).as_deref(),
            Some("solana does not capture ringgit")
        );
        assert_eq!(
            mint_error("solana", Some("USD"), None, None, None).as_deref(),
            Some("solana receives USDC, not USD")
        );
        assert_eq!(
            mint_error("solana", Some("USDC"), Some("mo"), None, None).as_deref(),
            Some("solana does not support subscriptions")
        );
        assert!(mint_error("solana", Some("usdc"), None, None, Some(Decimal::from_str("5").unwrap())).is_none());
        assert!(mint_error("stripe", Some("MYR"), None, None, None).is_none(), "non-solana rails unaffected");
    }
}
