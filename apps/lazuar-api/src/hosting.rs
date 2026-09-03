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
        Self::new(429, "Too Many Requests", "Too many start attempts")
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
