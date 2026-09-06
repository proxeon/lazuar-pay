//! Rails, methods, caps. Domain view — HTTP lives in `crates/rails`.

use std::fmt;

use crate::error::Illegal;
use crate::ids::PaymentId;
use crate::money::{Currency, CurrencyCode, Money};

#[derive(Clone, Copy, PartialEq, Eq, Hash)]
pub struct RailId {
    buf: [u8; 16],
    len: u8,
}

impl RailId {
    pub const STRIPE: Self = Self::from_static("stripe");
    pub const CHIP: Self = Self::from_static("chip");
    pub const BILLPLZ: Self = Self::from_static("billplz");
    pub const XENDIT: Self = Self::from_static("xendit");
    pub const RAZORPAY: Self = Self::from_static("razorpay");
    pub const SOLANA: Self = Self::from_static("solana");
    pub const TEST: Self = Self::from_static("test");

    pub const V1: &'static [RailId] = &[
        Self::STRIPE,
        Self::CHIP,
        Self::BILLPLZ,
        Self::XENDIT,
        Self::RAZORPAY,
        Self::SOLANA,
        Self::TEST,
    ];

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

    pub fn parse(s: &str) -> Result<Self, Illegal> {
        Self::V1
            .iter()
            .copied()
            .find(|r| r.as_str() == s)
            .ok_or(Illegal::UnknownRail)
    }

    pub fn as_str(&self) -> &str {
        std::str::from_utf8(&self.buf[..self.len as usize]).unwrap_or("")
    }

    pub fn caps(self) -> RailCaps {
        match self.as_str() {
            "stripe" => RailCaps {
                session: true,
                webhook: true,
                sync: true,
                refund: true,
                refund_idempotent: true,
                cancel_session: true,
                requires_email: false,
            },
            "chip" => RailCaps {
                session: true,
                webhook: true,
                sync: true,
                refund: true,
                refund_idempotent: false,
                cancel_session: true,
                requires_email: true,
            },
            "billplz" => RailCaps {
                session: true,
                webhook: true,
                sync: true,
                refund: false,
                refund_idempotent: false,
                cancel_session: false,
                requires_email: true,
            },
            "xendit" => RailCaps {
                session: true,
                webhook: true,
                sync: true,
                refund: false,
                refund_idempotent: false,
                cancel_session: false,
                requires_email: true,
            },
            "razorpay" => RailCaps {
                session: true,
                webhook: true,
                sync: true,
                refund: false,
                refund_idempotent: false,
                cancel_session: false,
                requires_email: true,
            },
            // Watcher is the chain crate, not HTTP PSync (`test` is the only missing retrieve).
            "solana" => RailCaps {
                session: true,
                webhook: false,
                sync: false,
                refund: false,
                refund_idempotent: false,
                cancel_session: false,
                requires_email: false,
            },
            "test" => RailCaps {
                session: true,
                webhook: true,
                sync: false,
                refund: true,
                refund_idempotent: true,
                cancel_session: false,
                requires_email: false,
            },
            _ => RailCaps::none(),
        }
    }
}

impl fmt::Debug for RailId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.debug_tuple("RailId").field(&self.as_str()).finish()
    }
}

impl fmt::Display for RailId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(self.as_str())
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct RailCaps {
    pub session: bool,
    pub webhook: bool,
    pub sync: bool,
    pub refund: bool,
    pub refund_idempotent: bool,
    pub cancel_session: bool,
    pub requires_email: bool,
}

impl RailCaps {
    pub const fn none() -> Self {
        Self {
            session: false,
            webhook: false,
            sync: false,
            refund: false,
            refund_idempotent: false,
            cancel_session: false,
            requires_email: false,
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct MethodId {
    pub asset: CurrencyCode,
    pub rail: RailId,
}

impl MethodId {
    pub fn new(asset: CurrencyCode, rail: RailId) -> Self {
        Self { asset, rail }
    }
}

impl fmt::Display for MethodId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            f,
            "{}-{}",
            self.asset,
            self.rail.as_str().to_ascii_uppercase()
        )
    }
}

#[derive(Clone, Debug, PartialEq, Eq, Default)]
pub struct ConnectorRefs {
    /// `cs_`, bill, CHIP purchase, solana reference pubkey.
    pub session_id: Option<String>,
    /// `pi_` / charge used to *refund*. NEVER assume == `session_id` (028 P0-1).
    pub capture_id: Option<String>,
    /// Stripe `ch_`, solana signature.
    pub network_id: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct HostedSession {
    /// https redirect or `solana:` URI.
    pub url: String,
    pub session_id: String,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct SessionCtx {
    pub payment_id: PaymentId,
    pub quoted: Money,
    pub payer_email: Option<String>,
    pub payer_name: Option<String>,
    pub success_url: Option<String>,
    pub cancel_url: Option<String>,
}

/// Port of `RailCurrencies.IsSupported`. Mint rejects a mismatch.
pub fn rail_supports_currency(rail: RailId, currency: Currency) -> bool {
    let code = currency.code.as_str();
    match rail.as_str() {
        "solana" => currency == Currency::USDC_SOLANA,
        "stripe" => {
            currency.kind == crate::money::CurrencyKind::Fiat
                && Currency::V1.iter().any(|c| c.code.as_str() == code)
        }
        "xendit" => matches!(code, "IDR" | "MYR" | "PHP" | "THB" | "SGD"),
        "billplz" | "chip" => code == "MYR",
        "razorpay" => code == "INR",
        "test" => matches!(code, "MYR" | "USD"),
        _ => false,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn stripe_caps_match_10() {
        let c = RailId::STRIPE.caps();
        assert!(
            c.session && c.webhook && c.sync && c.refund && c.refund_idempotent && c.cancel_session
        );
        assert!(!c.requires_email);
    }

    #[test]
    fn chip_refund_is_not_idempotent() {
        let c = RailId::CHIP.caps();
        assert!(c.refund && !c.refund_idempotent && c.requires_email && c.sync);
    }

    #[test]
    fn test_rail_has_no_psync() {
        assert!(!RailId::TEST.caps().sync);
    }

    #[test]
    fn method_id_display() {
        let m = MethodId::new(Currency::MYR.code, RailId::CHIP);
        assert_eq!(m.to_string(), "MYR-CHIP");
        let s = MethodId::new(Currency::USDC_SOLANA.code, RailId::SOLANA);
        assert_eq!(s.to_string(), "USDC-SOLANA");
    }

    #[test]
    fn razorpay_rejects_myr() {
        assert!(!rail_supports_currency(RailId::RAZORPAY, Currency::MYR));
        assert!(rail_supports_currency(RailId::RAZORPAY, Currency::INR));
    }

    #[test]
    fn solana_is_usdc_only() {
        assert!(rail_supports_currency(
            RailId::SOLANA,
            Currency::USDC_SOLANA
        ));
        assert!(!rail_supports_currency(RailId::SOLANA, Currency::MYR));
    }
}
