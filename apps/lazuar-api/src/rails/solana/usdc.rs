//! Port of `SolanaUsdc.cs` — USDC constants. Values live in `cluster` to keep
//! the pinned mints and programs next to the cluster tables.

pub use super::cluster::{DEVNET_MINT, MAINNET_MINT, TOKEN2022_PROGRAM, TOKEN_PROGRAM, USDC_CURRENCY, USDC_DECIMALS};

pub fn mint_for(environment: &str) -> &'static str {
    super::cluster::mint_for(environment)
}

pub fn is_pinned_mint(mint: &str) -> bool {
    super::cluster::is_pinned_mint(mint)
}
