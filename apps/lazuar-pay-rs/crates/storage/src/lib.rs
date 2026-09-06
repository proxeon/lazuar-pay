//! Persistence. `apply` is the only composer of fold + journal + outbox (032/08).
//! sqlx lands with the first migration. This crate must not import rail HTTP clients.

#![forbid(unsafe_code)]
