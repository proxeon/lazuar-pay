//! Chain watcher. Inserts proofs / reservations. Does not write `attempt_id` (032/05).
//! Future `--watcher-only` binary. v1: Solana USDC only. Bitcoin is 032/19, not v1.

#![forbid(unsafe_code)]

pub mod solana;
