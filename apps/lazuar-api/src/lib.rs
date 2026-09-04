//! lazuar-api library crate — the sync Rust port of `apps/lazuar-pay`.
//!
//! The binary in `main.rs` is a thin shell; everything testable lives here so
//! integration tests can assemble the app with fake transports, mirroring the
//! C# `PayApiFactory` seam-for-seam.

pub mod app;
pub mod config;
pub mod boot;
pub mod db;
pub mod domain;
pub mod hosting;
pub mod identity;
pub mod links;
pub mod merchant;
pub mod money;
pub mod publicpay;
pub mod rails;
pub mod secrets;
pub mod transport;
pub mod webhooks;
pub mod workers;
