use std::fmt;

use crate::types::PaymentStatus;

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum MoneyError {
    Inexact,
    CurrencyMismatch,
    Overflow,
    Negative,
    UnknownCurrency,
    ExponentTooSmall,
}

impl fmt::Display for MoneyError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(match self {
            Self::Inexact => "amount is not representable at 2 display decimals",
            Self::CurrencyMismatch => "currency mismatch",
            Self::Overflow => "amount overflow",
            Self::Negative => "amount must not be negative",
            Self::UnknownCurrency => "unknown currency",
            Self::ExponentTooSmall => "currency exponent is below display decimals",
        })
    }
}

impl std::error::Error for MoneyError {}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum Illegal {
    NotStartable,
    LiveAttemptExists,
    Terminal(PaymentStatus),
    UnknownRail,
}

impl fmt::Display for Illegal {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::NotStartable => f.write_str("payment is not startable"),
            Self::LiveAttemptExists => f.write_str("a live attempt already exists"),
            Self::Terminal(s) => write!(f, "payment is terminal ({s:?})"),
            Self::UnknownRail => f.write_str("unknown rail"),
        }
    }
}

impl std::error::Error for Illegal {}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum JournalError {
    Empty,
    Unbalanced,
    MixedCurrency,
    MixedTenant,
    MixedPayment,
}

impl fmt::Display for JournalError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(match self {
            Self::Empty => "journal has no lines",
            Self::Unbalanced => "journal is not balanced",
            Self::MixedCurrency => "journal mixes currencies",
            Self::MixedTenant => "journal mixes tenants",
            Self::MixedPayment => "journal mixes payments",
        })
    }
}

impl std::error::Error for JournalError {}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum ParseIdError {
    Uuid,
}

impl fmt::Display for ParseIdError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str("invalid id")
    }
}

impl std::error::Error for ParseIdError {}
