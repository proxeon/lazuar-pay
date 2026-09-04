//! Port of `Rails/Solana/*` — receive-only USDC rail: base58, cluster rules,
//! money conversion, JSON-RPC client, transaction validation, and the
//! confirm/watch machinery.

pub mod address;
pub mod base58;
pub mod cluster;
pub mod confirm;
pub mod money;
pub mod rpc;
pub mod tx;
pub mod usdc;
