//! Port of `Webhooks/PspParseResult.cs` + the header helper every rail uses.

/// Case-insensitive header list (rouille headers arrive as name/value pairs).
pub struct Headers<'a>(pub &'a [(String, String)]);

impl<'a> Headers<'a> {
    pub fn get(&self, name: &str) -> Option<&'a str> {
        self.0
            .iter()
            .find(|(k, _)| k.eq_ignore_ascii_case(name))
            .map(|(_, v)| v.as_str())
    }
}

/// Port of `PspParseResult` — what a rail's parser extracted from a webhook.
#[derive(Debug, Clone, Default)]
pub struct ParsedWebhook {
    pub event_id: String,
    pub ignored: bool,
    pub failed: bool,
    pub ignore_reason: Option<String>,
    pub checkout_id: Option<String>,
    pub hosted_session_id: Option<String>,
    pub provider_ref: Option<String>,
    pub amount_minor: Option<i64>,
    pub currency: Option<String>,
}

/// Verification/parse failures. `Verify` maps to 400; `MissingSecret` maps to
/// 503 — a configured rail whose secret cannot be resolved is a server fault,
/// not a caller fault (C# `InvalidOperationException("webhook secret …")`).
#[derive(Debug, thiserror::Error)]
pub enum WebhookParseError {
    #[error("{0}")]
    Verify(String),
    #[error("{0}")]
    MissingSecret(String),
}

impl ParsedWebhook {
    pub fn verify_error(message: impl Into<String>) -> WebhookParseError {
        WebhookParseError::Verify(message.into())
    }
}

impl WebhookParseError {
    pub fn verify_error(message: impl Into<String>) -> Self {
        WebhookParseError::Verify(message.into())
    }
}
