//! Port of `PublicPay/BuyerEmail.cs`.

pub const PLACEHOLDER: &str = "customer@example.com";

pub fn is_usable(email: Option<&str>) -> bool {
    match email {
        Some(e) => {
            !e.trim().is_empty() && !e.trim().eq_ignore_ascii_case(PLACEHOLDER)
        }
        None => false,
    }
}

/// `BuyerEmail.NameFrom` — display name preference: payer name, else email local part.
pub fn name_from(email: Option<&str>, name: Option<&str>) -> String {
    if let Some(n) = name.map(str::trim).filter(|s| !s.is_empty()) {
        return n.to_string();
    }
    match email.map(str::trim).filter(|s| !s.is_empty()) {
        Some(e) => e.split('@').next().unwrap_or(e).to_string(),
        None => "Customer".to_string(),
    }
}

/// `NormalizeSlotKey` — trimmed, 8..=128 chars, else absent.
pub fn normalize_slot_key(raw: Option<&str>) -> Option<String> {
    let slot = raw?.trim();
    if slot.is_empty() || !(8..=128).contains(&slot.len()) {
        return None;
    }
    Some(slot.to_string())
}
