//! One module per rail. Parse has no persistence. Caps are data (032/10).
//! This crate must not import `sqlx`.

#![forbid(unsafe_code)]

pub mod billplz;
pub mod chip;
pub mod razorpay;
pub mod solana;
pub mod stripe;
pub mod test_rail;
pub mod xendit;

pub use test_rail as test;
