//! Outbound HTTP for rails, PSPs, and the One identity API.
//!
//! The C# port replaces `IHttpClientFactory` with `StaticHttpFactory(FakePspHandler)`
//! in tests; here the seam is the `Transport` trait — `UreqTransport` in production,
//! `FakeTransport` (tests/support) in tests. One seam, same semantics: every outbound
//! call records-and-responds in tests, real HTTP in prod.

/// An outbound request. Bodies are strings — every rail in the reference
/// implementation sends and receives JSON/form text.
#[derive(Debug, Clone)]
pub struct OutRequest {
    pub method: String,
    pub url: String,
    pub headers: Vec<(String, String)>,
    pub body: Option<String>,
}

#[derive(Debug, Clone)]
pub struct OutResponse {
    pub status: u16,
    pub body: String,
}

#[derive(Debug, thiserror::Error)]
pub enum TransportError {
    #[error("transport failure: {0}")]
    Transport(String),
    #[error("timed out after {timeout_secs}s")]
    Timeout { timeout_secs: u64 },
}

/// The seam every outbound call goes through.
pub trait Transport: Send + Sync {
    fn send(&self, request: OutRequest) -> Result<OutResponse, TransportError>;
}

impl<T: Transport + ?Sized> Transport for std::sync::Arc<T> {
    fn send(&self, request: OutRequest) -> Result<OutResponse, TransportError> {
        (**self).send(request)
    }
}

/// Real transport backed by `ureq` (sync). Non-2xx statuses are *responses*,
/// not errors — rails and the webhook dispatcher branch on status codes, and
/// a 500-after-send is the ambiguous-outcome case (issues/001) that must reach them.
pub struct UreqTransport {
    pub timeout_secs: u64,
    /// ureq default is 5. C# solana + pay-webhooks set AllowAutoRedirect = false.
    redirects: u32,
}

impl UreqTransport {
    pub fn new(timeout_secs: u64) -> Self {
        Self { timeout_secs, redirects: 5 }
    }

    /// Sync transport that never follows redirects — the C# solana and
    /// pay-webhooks clients set `AllowAutoRedirect = false`.
    pub fn new_no_redirects(timeout_secs: u64) -> Self {
        Self { timeout_secs, redirects: 0 }
    }
}

impl Transport for UreqTransport {
    fn send(&self, request: OutRequest) -> Result<OutResponse, TransportError> {
        let agent = ureq::AgentBuilder::new()
            .timeout(std::time::Duration::from_secs(self.timeout_secs))
            .redirects(self.redirects)
            .build();

        let mut req = agent.request(&request.method, &request.url);
        for (name, value) in &request.headers {
            req = req.set(name, value);
        }

        let dispatched = match &request.body {
            Some(body) => req.send_string(body),
            None => req.call(),
        };

        match dispatched {
            Ok(resp) => Ok(OutResponse {
                status: resp.status(),
                body: resp.into_string().unwrap_or_default(),
            }),
            Err(ureq::Error::Status(code, resp)) => Ok(OutResponse {
                status: code,
                body: resp.into_string().unwrap_or_default(),
            }),
            Err(ureq::Error::Transport(t)) => match t.kind() {
                ureq::ErrorKind::Io if t.to_string().contains("timed out") => {
                    Err(TransportError::Timeout { timeout_secs: self.timeout_secs })
                }
                _ => Err(TransportError::Transport(t.to_string())),
            },
        }
    }
}

#[cfg(test)]
mod tests {
    #[test]
    fn new_no_redirects_disables_follow() {
        assert_eq!(super::UreqTransport::new_no_redirects(10).redirects, 0);
        assert_eq!(super::UreqTransport::new(10).redirects, 5);
    }
}
