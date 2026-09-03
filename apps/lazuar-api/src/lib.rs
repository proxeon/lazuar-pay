//! lazuar-api library crate — the sync Rust port of `apps/lazuar-pay`.
//!
//! The binary in `main.rs` is a thin shell; everything testable lives here so
//! integration tests can assemble the app with fake transports, mirroring the
//! C# `PayApiFactory` seam-for-seam.

pub mod app;
pub mod config;
pub mod transport;
