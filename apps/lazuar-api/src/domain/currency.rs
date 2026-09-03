//! Port of `Money/RailCurrencies.cs` + `Money/MoneyMath.cs`.
//!
//! Issues 003 and 014 (issues/001): which currencies each rail may settle.
//! [`to_minor`] multiplies by 100 — it assumes two-decimal currencies — and every
//! rail conversion hardcodes that assumption. A zero-decimal currency (JPY, KRW,
//! VND, …) therefore used to produce a processor charge 100× the quoted amount
//! while the ledger booked the quote, invisible to the webhook amount check (both
//! sides ran through the same ×100). Those codes are rejected until exponent-aware
//! conversion exists. Rails also only settle what they actually bill: Billplz and
//! CHIP bill MYR only and Razorpay settles INR — a USD quote on those rails used
//! to collect MYR at the processor while charge, journal, and receipt booked USD.

use rust_decimal::prelude::*;
use rust_decimal::Decimal;
use rust_decimal::RoundingStrategy;

use crate::rails::providers;

/// Solana settles USDC only (see `Rails/Solana/SolanaUsdc.cs`).
pub const SOLANA_CURRENCY: &str = "USDC";

/// Every currency the system may quote. All are two-decimal ISO-4217 codes — the
/// set [`to_minor`] is correct for. Zero-decimal codes must NOT be added here
/// without making `to_minor`/`from_minor` exponent-aware first.
pub const TWO_DECIMAL: [&str; 16] = [
    "MYR", "USD", "SGD", "EUR", "GBP", "AUD", "NZD", "CHF", "CAD", "HKD", "THB", "PHP", "IDR",
    "INR", "CNY", "TWD",
];

fn by_provider(provider: &str) -> Option<&'static [&'static str]> {
    match provider {
        // Stripe settles two-decimal currencies; IDR is two-decimal on Stripe.
        providers::STRIPE => Some(&TWO_DECIMAL),
        // Xendit's regional coverage.
        providers::XENDIT => Some(&["IDR", "MYR", "PHP", "THB", "SGD"]),
        // Billplz bills are MYR-only — the payload never carries a currency.
        providers::BILLPLZ => Some(&["MYR"]),
        // CHIP purchases are MYR.
        providers::CHIP => Some(&["MYR"]),
        // Razorpay settles INR.
        providers::RAZORPAY => Some(&["INR"]),
        // The no-op test rail accepts what the suite exercises.
        providers::TEST => Some(&["MYR", "USD"]),
        _ => None,
    }
}

pub fn is_supported(provider: &str, currency: &str) -> bool {
    if providers::is_solana(provider) {
        // Solana's USDC rules (amounts, decimals) are validated by the solana money module.
        return currency.eq_ignore_ascii_case(SOLANA_CURRENCY);
    }

    by_provider(provider)
        .is_some_and(|list| list.iter().any(|c| c.eq_ignore_ascii_case(currency)))
}

/// Human-readable list for the 400 message.
pub fn describe(provider: &str) -> String {
    if providers::is_solana(provider) {
        return SOLANA_CURRENCY.to_string();
    }
    by_provider(provider).unwrap_or(&[]).join(", ")
}

/// `MoneyMath.ToMinor`: multiply by 100, rounding half away from zero.
pub fn to_minor(amount: Decimal) -> i64 {
    let rounded = amount * Decimal::from(100);
    rounded
        .round_dp_with_strategy(0, RoundingStrategy::MidpointAwayFromZero)
        .to_i64()
        .expect("minor units fit i64 at 18,2 scale")
}

/// `MoneyMath.FromMinor`: cents back to a two-decimal amount.
pub fn from_minor(cents: Decimal) -> Decimal {
    cents / Decimal::from(100)
}

/// `MoneyMath.TryNormalizeCurrency`: trim, uppercase, exactly 3 letters.
pub fn try_normalize_currency(raw: Option<&str>) -> Option<String> {
    let trimmed = raw?.trim();
    if trimmed.is_empty() {
        return None;
    }
    let upper = trimmed.to_uppercase();
    if upper.len() != 3 {
        return None;
    }
    Some(upper)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::str::FromStr;

    #[test]
    fn zero_decimal_currency_rejected() {
        // Issue 003: zero-decimal currencies (JPY etc.) are charged 100x by ToMinor.
        // They are not quotable anywhere.
        assert!(!is_supported(providers::STRIPE, "JPY"));
        assert!(!is_supported(providers::STRIPE, "KRW"));
        assert!(!is_supported(providers::STRIPE, "VND"));
    }

    #[test]
    fn rail_currency_mismatch_rejected() {
        // Issue 014: Billplz/CHIP bill MYR only; Razorpay settles INR only.
        assert!(!is_supported(providers::BILLPLZ, "USD"));
        assert!(!is_supported(providers::CHIP, "USD"));
        assert!(!is_supported(providers::RAZORPAY, "MYR"));
        assert!(is_supported(providers::RAZORPAY, "INR"));
        assert!(is_supported(providers::BILLPLZ, "myr")); // case-insensitive
    }

    #[test]
    fn solana_settles_usdc_only() {
        assert!(is_supported(providers::SOLANA, "USDC"));
        assert!(is_supported(providers::SOLANA, "usdc"));
        assert!(!is_supported(providers::SOLANA, "SOL"));
        assert_eq!(describe(providers::SOLANA), "USDC");
    }

    #[test]
    fn to_minor_rounds_half_away_from_zero_and_from_minor_inverts() {
        assert_eq!(to_minor(Decimal::from_str("9.90").unwrap()), 990);
        assert_eq!(to_minor(Decimal::from_str("9.985").unwrap()), 999); // 998.5 → 999 (away from zero)
        assert_eq!(to_minor(Decimal::from_str("9.9949").unwrap()), 999);
        assert_eq!(to_minor(Decimal::from_str("0.005").unwrap()), 1);
        assert_eq!(
            from_minor(Decimal::from(990)),
            Decimal::from_str("9.90").unwrap()
        );
    }

    #[test]
    fn normalize_currency_trims_uppercases_and_requires_three_letters() {
        assert_eq!(try_normalize_currency(Some(" myr ")).as_deref(), Some("MYR"));
        assert_eq!(try_normalize_currency(None), None);
        assert_eq!(try_normalize_currency(Some("  ")), None);
        assert_eq!(try_normalize_currency(Some("AB")), None);
        assert_eq!(try_normalize_currency(Some("ABCD")), None);
    }
}
