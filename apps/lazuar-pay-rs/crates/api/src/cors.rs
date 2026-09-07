//! CORS origin list. Laptop defaults in Testing/Development; configured list replaces them.

use axum::http::HeaderValue;
use tower_http::cors::{AllowHeaders, AllowMethods, AllowOrigin, CorsLayer};

use crate::boot::Env;

pub const DEVELOPMENT_ORIGINS: &[&str] = &[
    "http://localhost:5178",
    "http://127.0.0.1:5178",
    "http://localhost:5179",
    "http://127.0.0.1:5179",
    "http://localhost:4178",
    "http://127.0.0.1:4178",
    "http://localhost:4179",
    "http://127.0.0.1:4179",
];

pub fn try_parse(raw: &str) -> Option<Vec<String>> {
    if raw.trim().is_empty() {
        return None;
    }
    let origins: Vec<String> = raw
        .split(',')
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .map(str::to_string)
        .collect();
    if origins.is_empty() {
        None
    } else {
        Some(origins)
    }
}

pub fn resolve(raw: &str, env: Env) -> Result<Vec<String>, String> {
    if let Some(origins) = try_parse(raw) {
        return Ok(origins);
    }
    if env.allows_test() {
        return Ok(DEVELOPMENT_ORIGINS
            .iter()
            .map(|s| (*s).to_string())
            .collect());
    }
    Err("Pay:CorsOrigins must be configured in Production and Staging.".into())
}

pub fn layer(origins: &[String]) -> CorsLayer {
    let list: Vec<HeaderValue> = origins.iter().filter_map(|o| o.parse().ok()).collect();
    CorsLayer::new()
        .allow_origin(AllowOrigin::list(list))
        .allow_methods(AllowMethods::any())
        .allow_headers(AllowHeaders::any())
}

#[cfg(test)]
mod tests {
    use super::*;

    fn laptop() -> Vec<String> {
        DEVELOPMENT_ORIGINS
            .iter()
            .map(|s| (*s).to_string())
            .collect()
    }

    #[test]
    fn empty_in_development_uses_laptop_list() {
        assert_eq!(resolve("", Env::Development).expect("laptop"), laptop());
        assert_eq!(resolve("  ", Env::Testing).expect("laptop"), laptop());
    }

    #[test]
    fn empty_in_production_fails() {
        let err = resolve("", Env::Production).unwrap_err();
        assert!(err.contains("Pay:CorsOrigins"), "{err}");
        let err = resolve("  ", Env::Staging).unwrap_err();
        assert!(err.contains("Pay:CorsOrigins"), "{err}");
    }

    #[test]
    fn configured_replaces_laptop() {
        let got = resolve("https://checkout.example", Env::Testing).expect("parsed");
        assert_eq!(got, vec!["https://checkout.example".to_string()]);
    }
}
