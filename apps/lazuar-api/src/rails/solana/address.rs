//! Port of `Rails/Solana/SolanaAddress.cs`.

use crate::rails::solana::base58;

pub fn try_normalize(raw: &str) -> Option<String> {
    let trimmed = raw.trim();
    if trimmed.is_empty() {
        return None;
    }
    let lower = trimmed.to_ascii_lowercase();
    if lower.contains("begin ") || lower.contains("private") || lower.starts_with("sk_") || lower.starts_with("lzr_sk_") {
        return None;
    }
    let decoded = base58::decode(trimmed)?;
    if decoded.len() != 32 {
        return None;
    }
    let encoded = base58::encode(&decoded);
    if encoded.len() < 32 || encoded.len() > 44 {
        return None;
    }
    Some(encoded)
}

pub fn last4(address: &str) -> String {
    if address.len() >= 4 {
        address[address.len() - 4..].to_string()
    } else {
        address.to_string()
    }
}
