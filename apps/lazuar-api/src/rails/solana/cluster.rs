//! Ports of `SolanaCluster.cs` + `SolanaUsdc.cs` constants.

use crate::rails::providers;

pub const MAINNET: &str = "mainnet-beta";
pub const DEVNET: &str = "devnet";
pub const MAINNET_GENESIS: &str = "5eykt4UsFv8P8NJdTREpY1vzqKqZKvdpKuc147dw2N9d";
pub const DEVNET_GENESIS: &str = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";

pub const USDC_CURRENCY: &str = "USDC";
pub const USDC_DECIMALS: i32 = 6;
pub const MAINNET_MINT: &str = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";
pub const DEVNET_MINT: &str = "4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU";
pub const TOKEN_PROGRAM: &str = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";
pub const TOKEN2022_PROGRAM: &str = "TokenzQdBNbLqP5VEhdkAS6EPFLC1PHnBqCXEpPxuEb";
pub const MEMO_PROGRAM: &str = "MemoSq4gqABAXKb96qnH8TysNcWxMyWCqXgDLGmfcHr";

pub fn try_normalize(raw: Option<&str>) -> Option<&'static str> {
    let normalized = raw.unwrap_or("").trim().to_lowercase();
    match normalized.as_str() {
        "mainnet" | "mainnet-beta" => Some(MAINNET),
        "devnet" => Some(DEVNET),
        _ => None,
    }
}

pub fn from_config(config_cluster: Option<&str>) -> &'static str {
    try_normalize(config_cluster).unwrap_or(DEVNET)
}

pub fn vault_environment(cluster: &str) -> &'static str {
    if cluster == MAINNET {
        "mainnet"
    } else {
        "devnet"
    }
}

pub fn matches_vault(cluster: &str, vault_env: Option<&str>) -> bool {
    match providers::try_normalize_solana_environment(vault_env) {
        Some(env) => env == vault_environment(cluster),
        None => false,
    }
}

pub fn mint(cluster: &str) -> &'static str {
    if cluster == MAINNET {
        MAINNET_MINT
    } else {
        DEVNET_MINT
    }
}

pub fn genesis_hash(cluster: &str) -> &'static str {
    if cluster == MAINNET {
        MAINNET_GENESIS
    } else {
        DEVNET_GENESIS
    }
}

pub fn mint_for(environment: &str) -> &'static str {
    match providers::try_normalize_solana_environment(Some(environment)) {
        Some("mainnet") => MAINNET_MINT,
        _ => DEVNET_MINT,
    }
}

pub fn is_pinned_mint(mint: &str) -> bool {
    mint == MAINNET_MINT || mint == DEVNET_MINT
}
