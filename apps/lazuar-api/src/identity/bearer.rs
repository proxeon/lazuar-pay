//! Port of `Identity/Client/Bearer.cs`.

const PREFIX: &str = "Bearer ";

/// Authorization header present, `Bearer `-prefixed, non-empty payload.
pub fn try_get(auth_header: Option<&str>) -> Option<String> {
    let authorization = auth_header?.trim();
    if authorization.is_empty() || !authorization.to_ascii_uppercase().starts_with(&PREFIX.to_uppercase()) {
        return None;
    }
    let token = authorization[PREFIX.len()..].trim();
    (!token.is_empty()).then(|| authorization.to_string())
}

pub fn token(authorization: &str) -> &str {
    if authorization.len() > PREFIX.len()
        && authorization[..PREFIX.len()].eq_ignore_ascii_case(PREFIX)
    {
        authorization[PREFIX.len()..].trim()
    } else {
        authorization.trim()
    }
}

/// One machine key. Not a JWT, not Stripe/Hub `sk_`.
pub fn is_machine_key(authorization: &str) -> bool {
    token(authorization).starts_with("lzr_sk_")
}

/// Stripe/Hub `sk_` as Pay Authorization is the wrong family.
pub fn reject_wrong_family(authorization: &str) -> Option<crate::hosting::PayError> {
    let t = token(authorization);
    if t.starts_with("sk_") {
        Some(crate::hosting::PayError::unauthorized("Invalid bearer"))
    } else {
        None
    }
}
