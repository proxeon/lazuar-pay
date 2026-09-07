//! Echo `X-Request-Id`: printable ASCII, cap 64, fallback uuid (issues/003).

use axum::http::{HeaderValue, Request};
use axum::middleware::Next;
use axum::response::Response;
use uuid::Uuid;

const HEADER: &str = "X-Request-Id";
const MAX_LEN: usize = 64;

pub fn sanitize(value: &str) -> String {
    let capped: String = value.chars().take(MAX_LEN).collect();
    capped
        .chars()
        .filter(|c| (' '..='~').contains(c))
        .collect::<String>()
        .trim()
        .to_string()
}

pub async fn echo(req: Request<axum::body::Body>, next: Next) -> Response {
    let incoming = req
        .headers()
        .get(HEADER)
        .and_then(|v| v.to_str().ok())
        .unwrap_or("");
    let mut id = sanitize(incoming);
    if id.is_empty() {
        id = Uuid::new_v4().to_string();
    }
    let mut res = next.run(req).await;
    if let Ok(val) = HeaderValue::from_str(&id) {
        res.headers_mut().insert(HEADER, val);
    }
    res
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn non_ascii_stripped() {
        assert_eq!(sanitize("träck-123"), "trck-123");
    }

    #[test]
    fn control_bytes_stripped() {
        assert_eq!(sanitize("abc\tdef\r\ninj"), "abcdefinj");
    }

    #[test]
    fn over_64_capped() {
        assert_eq!(sanitize(&"a".repeat(200)).len(), 64);
    }

    #[test]
    fn only_invalid_empty() {
        assert!(sanitize("中文").is_empty());
    }

    #[test]
    fn clean_verbatim() {
        assert_eq!(sanitize("evt-abc_123.X"), "evt-abc_123.X");
    }
}
