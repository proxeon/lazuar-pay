//! Hosting — error mapping shared by every endpoint (port of `Hosting/PayErrors.cs`).

use serde::Serialize;

#[derive(Debug, Clone, Serialize)]
pub struct PayError {
    pub status: u16,
    pub title: &'static str,
    pub detail: String,
}

impl PayError {
    pub fn new(status: u16, title: &'static str, detail: impl Into<String>) -> Self {
        Self { status, title, detail: detail.into() }
    }

    pub fn unauthorized(detail: impl Into<String>) -> Self {
        Self::new(401, "Unauthorized", detail)
    }
    pub fn forbidden(detail: impl Into<String>) -> Self {
        Self::new(403, "Forbidden", detail)
    }
    pub fn not_found(detail: impl Into<String>) -> Self {
        Self::new(404, "Not Found", detail)
    }
    pub fn bad_request(detail: impl Into<String>) -> Self {
        Self::new(400, "Bad Request", detail)
    }
    pub fn conflict(detail: impl Into<String>) -> Self {
        Self::new(409, "Conflict", detail)
    }
    pub fn too_many_requests() -> Self {
        Self::too_many_requests_detail("Too many start attempts")
    }
    pub fn too_many_requests_detail(detail: impl Into<String>) -> Self {
        Self::new(429, "Too Many Requests", detail)
    }
    pub fn unavailable(detail: impl Into<String>) -> Self {
        Self::new(503, "Service Unavailable", detail)
    }
    pub fn internal(detail: impl Into<String>) -> Self {
        Self::new(500, "Internal Server Error", detail)
    }
    pub fn bad_gateway(detail: impl Into<String>) -> Self {
        Self::new(502, "Bad Gateway", detail)
    }
}

/// JSON number for a Decimal (C# emits bare numbers, not strings).
pub fn decimal_json(amount: rust_decimal::Decimal) -> serde_json::Value {
    use std::str::FromStr as _;
    serde_json::Value::Number(
        serde_json::Number::from_str(&amount.to_string()).unwrap_or_else(|_| serde_json::Number::from(0)),
    )
}

/// Port of `Hosting/PayList.cs:5-16` — default 50, max 100, `limit < 1` → default.
pub fn clamp_limit(limit: Option<i64>) -> i64 {
    match limit {
        Some(n) if n >= 1 => n.min(100),
        _ => 50,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn clamp_matches_pay_list() {
        assert_eq!(clamp_limit(None), 50);
        assert_eq!(clamp_limit(Some(0)), 50);
        assert_eq!(clamp_limit(Some(-5)), 50);
        assert_eq!(clamp_limit(Some(1)), 1);
        assert_eq!(clamp_limit(Some(100)), 100);
        assert_eq!(clamp_limit(Some(101)), 100);
    }
}
