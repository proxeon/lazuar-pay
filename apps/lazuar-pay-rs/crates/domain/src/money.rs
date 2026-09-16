//! Exponent-aware money. Minor is `i128`. Never `f64`. Never ×100 helper.

use std::fmt;
use std::str::FromStr;

use rust_decimal::prelude::ToPrimitive;
use rust_decimal::Decimal;
use time::OffsetDateTime;

use crate::error::MoneyError;

/// v1 quoted amounts (including USDC) accept at most two decimal places.
pub const DISPLAY_DECIMALS: u8 = 2;

#[derive(Clone, Copy, PartialEq, Eq, Hash)]
pub struct CurrencyCode {
    buf: [u8; 8],
    len: u8,
}

impl CurrencyCode {
    pub const fn from_static(s: &'static str) -> Self {
        assert!(s.len() <= 8);
        let bytes = s.as_bytes();
        let mut buf = [0u8; 8];
        let mut i = 0;
        while i < bytes.len() {
            buf[i] = bytes[i];
            i += 1;
        }
        Self {
            buf,
            len: bytes.len() as u8,
        }
    }

    pub fn as_str(&self) -> &str {
        std::str::from_utf8(&self.buf[..self.len as usize]).unwrap_or("")
    }
}

impl fmt::Debug for CurrencyCode {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.debug_tuple("CurrencyCode").field(&self.as_str()).finish()
    }
}

impl fmt::Display for CurrencyCode {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(self.as_str())
    }
}

#[derive(Clone, Copy, PartialEq, Eq, Hash)]
pub struct ChainId {
    buf: [u8; 16],
    len: u8,
}

impl ChainId {
    pub const SOLANA: Self = Self::from_static("solana");

    pub const fn from_static(s: &'static str) -> Self {
        assert!(s.len() <= 16);
        let bytes = s.as_bytes();
        let mut buf = [0u8; 16];
        let mut i = 0;
        while i < bytes.len() {
            buf[i] = bytes[i];
            i += 1;
        }
        Self {
            buf,
            len: bytes.len() as u8,
        }
    }

    pub fn as_str(&self) -> &str {
        std::str::from_utf8(&self.buf[..self.len as usize]).unwrap_or("")
    }
}

impl fmt::Debug for ChainId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.debug_tuple("ChainId").field(&self.as_str()).finish()
    }
}

#[derive(Clone, Copy, PartialEq, Eq, Hash)]
pub struct AssetId {
    buf: [u8; 16],
    len: u8,
}

impl AssetId {
    pub const USDC: Self = Self::from_static("usdc");

    pub const fn from_static(s: &'static str) -> Self {
        assert!(s.len() <= 16);
        let bytes = s.as_bytes();
        let mut buf = [0u8; 16];
        let mut i = 0;
        while i < bytes.len() {
            buf[i] = bytes[i];
            i += 1;
        }
        Self {
            buf,
            len: bytes.len() as u8,
        }
    }

    pub fn as_str(&self) -> &str {
        std::str::from_utf8(&self.buf[..self.len as usize]).unwrap_or("")
    }
}

impl fmt::Debug for AssetId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.debug_tuple("AssetId").field(&self.as_str()).finish()
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum CurrencyKind {
    Fiat,
    Crypto { chain: ChainId, asset: AssetId },
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct Currency {
    pub code: CurrencyCode,
    pub exponent: u8,
    pub kind: CurrencyKind,
}

impl Currency {
    pub const MYR: Self = Self::fiat("MYR");
    pub const USD: Self = Self::fiat("USD");
    pub const SGD: Self = Self::fiat("SGD");
    pub const EUR: Self = Self::fiat("EUR");
    pub const GBP: Self = Self::fiat("GBP");
    pub const AUD: Self = Self::fiat("AUD");
    pub const NZD: Self = Self::fiat("NZD");
    pub const CHF: Self = Self::fiat("CHF");
    pub const CAD: Self = Self::fiat("CAD");
    pub const HKD: Self = Self::fiat("HKD");
    pub const THB: Self = Self::fiat("THB");
    pub const PHP: Self = Self::fiat("PHP");
    /// Stripe/Xendit two-decimal, not ISO 0. Do not “fix” this.
    pub const IDR: Self = Self::fiat("IDR");
    pub const INR: Self = Self::fiat("INR");
    pub const CNY: Self = Self::fiat("CNY");
    pub const TWD: Self = Self::fiat("TWD");
    /// USDC on Solana. Ledger exponent 6; quote display still 2 in v1.
    pub const USDC_SOLANA: Self = Self {
        code: CurrencyCode::from_static("USDC"),
        exponent: 6,
        kind: CurrencyKind::Crypto {
            chain: ChainId::SOLANA,
            asset: AssetId::USDC,
        },
    };

    const fn fiat(code: &'static str) -> Self {
        Self {
            code: CurrencyCode::from_static(code),
            exponent: 2,
            kind: CurrencyKind::Fiat,
        }
    }

    /// v1 allowlist. JPY/KRW/VND are absent (issue 003).
    pub const V1: &'static [Currency] = &[
        Self::MYR,
        Self::USD,
        Self::SGD,
        Self::EUR,
        Self::GBP,
        Self::AUD,
        Self::NZD,
        Self::CHF,
        Self::CAD,
        Self::HKD,
        Self::THB,
        Self::PHP,
        Self::IDR,
        Self::INR,
        Self::CNY,
        Self::TWD,
        Self::USDC_SOLANA,
    ];

    pub fn by_code(code: &str) -> Option<Self> {
        Self::V1
            .iter()
            .copied()
            .find(|c| c.code.as_str().eq_ignore_ascii_case(code))
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct Money {
    minor: i128,
    currency: Currency,
}

impl Money {
    pub fn from_minor(minor: i128, currency: Currency) -> Result<Self, MoneyError> {
        if minor < 0 {
            return Err(MoneyError::Negative);
        }
        Ok(Self { minor, currency })
    }

    pub fn zero(currency: Currency) -> Self {
        Self { minor: 0, currency }
    }

    pub fn minor(self) -> i128 {
        self.minor
    }

    pub fn currency(self) -> Currency {
        self.currency
    }

    /// Exact: representable at `DISPLAY_DECIMALS` (v1: 2 for all quoted amounts)
    /// and at `currency.exponent` internally.
    pub fn from_quoted_display(d: Decimal, c: Currency) -> Result<Self, MoneyError> {
        if d.is_sign_negative() {
            return Err(MoneyError::Negative);
        }
        if c.exponent < DISPLAY_DECIMALS {
            return Err(MoneyError::ExponentTooSmall);
        }
        let factor = Decimal::from(10u32.pow(u32::from(DISPLAY_DECIMALS)));
        let scaled = d * factor;
        if !scaled.is_integer() {
            return Err(MoneyError::Inexact);
        }
        let display_units = scaled.to_i128().ok_or(MoneyError::Overflow)?;
        let extra = u32::from(c.exponent - DISPLAY_DECIMALS);
        let minor = display_units
            .checked_mul(10i128.pow(extra))
            .ok_or(MoneyError::Overflow)?;
        Self::from_minor(minor, c)
    }

    pub fn from_quoted_str(s: &str, c: Currency) -> Result<Self, MoneyError> {
        let d = Decimal::from_str(s.trim()).map_err(|_| MoneyError::Inexact)?;
        Self::from_quoted_display(d, c)
    }

    pub fn to_quoted_display(self) -> Result<Decimal, MoneyError> {
        if self.currency.exponent < DISPLAY_DECIMALS {
            return Err(MoneyError::ExponentTooSmall);
        }
        let extra = u32::from(self.currency.exponent - DISPLAY_DECIMALS);
        let div = 10i128.pow(extra);
        if self.minor % div != 0 {
            return Err(MoneyError::Inexact);
        }
        let display_units = self.minor / div;
        Ok(Decimal::from(display_units) / Decimal::from(100))
    }

    pub fn checked_add(self, other: Self) -> Result<Self, MoneyError> {
        if self.currency != other.currency {
            return Err(MoneyError::CurrencyMismatch);
        }
        let minor = self
            .minor
            .checked_add(other.minor)
            .ok_or(MoneyError::Overflow)?;
        Self::from_minor(minor, self.currency)
    }

    pub fn saturating_one_more(self) -> Self {
        Self {
            minor: self.minor.saturating_add(1),
            currency: self.currency,
        }
    }
}

impl PartialOrd for Money {
    fn partial_cmp(&self, other: &Self) -> Option<std::cmp::Ordering> {
        (self.currency == other.currency).then(|| self.minor.cmp(&other.minor))
    }
}

impl fmt::Display for Money {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{} {}", self.minor, self.currency.code)
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum RateSource {
    Identity,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct RateLock {
    pub quoted: Money,
    pub rail: Money,
    pub source: RateSource,
    pub locked_at: OffsetDateTime,
    pub expires_at: OffsetDateTime,
}

impl RateLock {
    /// v1: rail currency must equal quoted. No FX book.
    pub fn identity(quoted: Money, locked_at: OffsetDateTime, expires_at: OffsetDateTime) -> Self {
        Self {
            quoted,
            rail: quoted,
            source: RateSource::Identity,
            locked_at,
            expires_at,
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum AmountPolicy {
    Exact,
    Tolerance { max_under: Money, max_over: Money },
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Integrity {
    Ok,
    CurrencyMismatch,
    AmountMismatch,
}

pub fn integrity(quoted_rail: Money, received: Money, policy: AmountPolicy) -> Integrity {
    if quoted_rail.currency() != received.currency() {
        return Integrity::CurrencyMismatch;
    }
    match policy {
        AmountPolicy::Exact if quoted_rail.minor() != received.minor() => Integrity::AmountMismatch,
        AmountPolicy::Exact | AmountPolicy::Tolerance { .. } => Integrity::Ok,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn myr_10_is_1000_minor() {
        let m = Money::from_quoted_str("10.00", Currency::MYR).unwrap();
        assert_eq!(m.minor(), 1000);
    }

    #[test]
    fn usdc_10_is_10_000_000_minor() {
        let m = Money::from_quoted_str("10.00", Currency::USDC_SOLANA).unwrap();
        assert_eq!(m.minor(), 10_000_000);
        assert_ne!(m.minor(), 1000);
    }

    #[test]
    fn usdc_10_001_is_inexact_at_display_2() {
        let err = Money::from_quoted_str("10.001", Currency::USDC_SOLANA).unwrap_err();
        assert_eq!(err, MoneyError::Inexact);
    }

    #[test]
    fn myr_10_001_is_inexact() {
        let err = Money::from_quoted_str("10.001", Currency::MYR).unwrap_err();
        assert_eq!(err, MoneyError::Inexact);
    }

    #[test]
    fn idr_stays_two_decimal() {
        assert_eq!(Currency::IDR.exponent, 2);
        let m = Money::from_quoted_str("10.00", Currency::IDR).unwrap();
        assert_eq!(m.minor(), 1000);
    }

    #[test]
    fn jpy_is_not_a_v1_constant() {
        assert!(Currency::by_code("JPY").is_none());
    }

    #[test]
    fn mixed_currency_add_is_err() {
        let a = Money::from_quoted_str("10.00", Currency::MYR).unwrap();
        let b = Money::from_quoted_str("10.00", Currency::USD).unwrap();
        assert_eq!(a.checked_add(b).unwrap_err(), MoneyError::CurrencyMismatch);
    }

    #[test]
    fn display_round_trip_every_v1_currency() {
        for &c in Currency::V1 {
            let m = Money::from_quoted_str("10.00", c).unwrap();
            let back = m.to_quoted_display().unwrap();
            assert_eq!(back, Decimal::from(10));
            let again = Money::from_quoted_display(back, c).unwrap();
            assert_eq!(again, m);
        }
    }

    #[test]
    fn integrity_exact_mismatch() {
        let q = Money::from_quoted_str("10.00", Currency::MYR).unwrap();
        let r = Money::from_quoted_str("9.00", Currency::MYR).unwrap();
        assert_eq!(
            integrity(q, r, AmountPolicy::Exact),
            Integrity::AmountMismatch
        );
    }
}
