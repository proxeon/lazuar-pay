//! Port of `Rails/ProcessorRemote.cs` — the processor seam for refunds.
//!
//! The C# taxonomy is three-way and the distinction is load-bearing:
//! - unsupported/unconfigured rail (`InvalidOperationException`): nothing could
//!   have moved — reservation releases as `failed`.
//! - definitive no (`ProcessorRejectedException`, <500 answer): no money moved —
//!   releases as `failed`.
//! - outcome unknown (`ProcessorOutcomeUnknownException`, 5xx-after-send, timeout,
//!   connection reset): the refund MAY have executed — reservation stays `pending`
//!   for reconciliation (issue 001). Releasing it is how a retry double-refunded.

use rust_decimal::Decimal;

/// The charge snapshot a rail needs to execute a refund. Rails needing checkout
/// fields (CHIP's provider session id) receive them in Phase 4 via a checkout ref.
#[derive(Debug, Clone)]
pub struct ChargeRef {
    pub id: String,
    pub org_id: String,
    pub checkout_id: String,
    pub provider: Option<String>,
    pub provider_ref: Option<String>,
    pub amount: Decimal,
    pub currency: String,
    pub status: String,
    pub provider_session_id: Option<String>,
}

#[derive(Debug, Clone, thiserror::Error)]
pub enum RefundRemoteError {
    /// Nothing could have moved at the processor.
    #[error("{0}")]
    UnsupportedRail(String),
    /// Definitive (<500) processor answer: the refund was NOT created.
    #[error("processor rejected the refund: {0}")]
    ProcessorRejected(String),
    /// 5xx-after-send / transport loss: the refund MAY have been created.
    #[error("refund outcome unknown — {0}")]
    OutcomeUnknown(String),
}

/// The seam rails implement and tests fake. `refund_id` is the deterministic
/// processor idempotency key (issue 001) — implementations MUST pass it through.
pub trait Refunder: Send + Sync {
    fn refund_charge(
        &self,
        charge: &ChargeRef,
        amount: Decimal,
        refund_id: &str,
    ) -> Result<(), RefundRemoteError>;
}

/// Live processor remote. Secrets are unwrapped org credentials keyed by provider.
pub struct LiveRefunder {
    pub transport: std::sync::Arc<dyn crate::transport::Transport>,
    pub secrets: std::collections::HashMap<String, String>,
    pub chip_session_id: Option<String>,
}

impl LiveRefunder {
    pub fn load(
        conn: &mut postgres::Client,
        box_one: &crate::secrets::SecretBox,
        transport: std::sync::Arc<dyn crate::transport::Transport>,
        org_id: &str,
        chip_session_id: Option<String>,
    ) -> Result<Self, postgres::Error> {
        let rows = conn.query(
            "SELECT \"Provider\",\"Ciphertext\" FROM public.gateway_credentials WHERE \"OrgId\" = $1",
            &[&org_id],
        )?;
        let mut secrets = std::collections::HashMap::new();
        for row in rows {
            let provider: String = row.get("Provider");
            let ct: String = row.get("Ciphertext");
            if let Ok(secret) = box_one.unprotect(&ct) {
                if !secret.trim().is_empty() {
                    secrets.insert(provider, secret);
                }
            }
        }
        Ok(Self { transport, secrets, chip_session_id })
    }
}

impl Refunder for LiveRefunder {
    fn refund_charge(
        &self,
        charge: &ChargeRef,
        amount: Decimal,
        refund_id: &str,
    ) -> Result<(), RefundRemoteError> {
        use crate::domain::currency;
        use crate::rails::providers;
        use crate::transport::OutRequest;

        let provider = charge.provider.as_deref().unwrap_or("");
        let Some(name) = providers::try_normalize(Some(provider)) else {
            return Err(RefundRemoteError::UnsupportedRail("unknown provider".into()));
        };
        if providers::is_test(name) {
            return Ok(());
        }
        let minor = currency::to_minor(amount);
        if name == providers::STRIPE {
            let Some(secret) = self.secrets.get(providers::STRIPE) else {
                return Err(RefundRemoteError::UnsupportedRail(
                    "stripe is not configured; cannot refund".into(),
                ));
            };
            let body = format!(
                "charge={}&amount={}",
                urlencoding::encode(charge.provider_ref.as_deref().unwrap_or("")),
                minor
            );
            let req = OutRequest {
                method: "POST".into(),
                url: "https://api.stripe.com/v1/refunds".into(),
                headers: vec![
                    ("Authorization".into(), format!("Bearer {secret}")),
                    ("Idempotency-Key".into(), format!("lazuar-refund:{refund_id}")),
                    ("Content-Type".into(), "application/x-www-form-urlencoded".into()),
                ],
                body: Some(body),
            };
            return map_stripe(self.transport.send(req));
        }
        if name == providers::CHIP {
            let Some(secret) = self.secrets.get(providers::CHIP) else {
                return Err(RefundRemoteError::UnsupportedRail(
                    "chip is not configured; nothing to refund with".into(),
                ));
            };
            let session = charge
                .provider_session_id
                .as_deref()
                .or(self.chip_session_id.as_deref())
                .filter(|s| !s.is_empty())
                .ok_or_else(|| {
                    RefundRemoteError::UnsupportedRail("chip purchase is missing; nothing to refund".into())
                })?;
            let url = format!(
                "https://gate.chip-in.asia/api/v1/purchases/{}/refund/",
                urlencoding::encode(session)
            );
            let req = OutRequest {
                method: "POST".into(),
                url,
                headers: vec![
                    ("Authorization".into(), format!("Bearer {secret}")),
                    ("Content-Type".into(), "application/json".into()),
                ],
                body: Some(serde_json::json!({ "amount": minor }).to_string()),
            };
            return map_chip(self.transport.send(req));
        }
        Err(RefundRemoteError::UnsupportedRail("refund not supported on this rail".into()))
    }
}

fn map_stripe(
    result: Result<crate::transport::OutResponse, crate::transport::TransportError>,
) -> Result<(), RefundRemoteError> {
    match result {
        Err(crate::transport::TransportError::Timeout { .. })
        | Err(crate::transport::TransportError::Transport(_)) => {
            Err(RefundRemoteError::OutcomeUnknown("stripe transport loss".into()))
        }
        Ok(resp) if resp.status >= 500 => {
            Err(RefundRemoteError::OutcomeUnknown(format!("stripe refund status {}", resp.status)))
        }
        Ok(resp) if resp.status >= 400 => {
            Err(RefundRemoteError::ProcessorRejected(resp.body))
        }
        Ok(_) => Ok(()),
    }
}

fn map_chip(
    result: Result<crate::transport::OutResponse, crate::transport::TransportError>,
) -> Result<(), RefundRemoteError> {
    match result {
        Err(crate::transport::TransportError::Timeout { .. })
        | Err(crate::transport::TransportError::Transport(_)) => {
            Err(RefundRemoteError::OutcomeUnknown("chip transport loss".into()))
        }
        Ok(resp) if !(200..300).contains(&resp.status) => {
            Err(RefundRemoteError::ProcessorRejected(format!("chip status {}", resp.status)))
        }
        Ok(_) => Ok(()),
    }
}

/// Best-effort expire of an unpaid hosted session (Stripe expire / CHIP cancel).
pub fn expire_hosted(
    transport: &dyn crate::transport::Transport,
    provider: &str,
    secret: Option<&str>,
    provider_session_id: Option<&str>,
) {
    use crate::rails::providers;
    use crate::transport::OutRequest;
    let Some(session) = provider_session_id.filter(|s| !s.is_empty()) else { return };
    let Some(secret) = secret.filter(|s| !s.is_empty()) else { return };
    if provider == providers::STRIPE {
        let _ = transport.send(OutRequest {
            method: "POST".into(),
            url: format!("https://api.stripe.com/v1/checkout/sessions/{session}/expire"),
            headers: vec![("Authorization".into(), format!("Bearer {secret}"))],
            body: None,
        });
    } else if provider == providers::CHIP {
        let _ = transport.send(OutRequest {
            method: "POST".into(),
            url: format!("https://gate.chip-in.asia/api/v1/purchases/{session}/cancel/"),
            headers: vec![
                ("Authorization".into(), format!("Bearer {secret}")),
                ("Content-Type".into(), "application/json".into()),
            ],
            body: None,
        });
    }
}

/// Flip a pending late-pay refund to succeeded after a successful processor call.
pub fn settle_pending(
    conn: &mut postgres::Client,
    remote: &dyn Refunder,
    refund_id: &str,
    charge: &ChargeRef,
    amount: Decimal,
) -> bool {
    if remote.refund_charge(charge, amount, refund_id).is_err() {
        return false;
    }
    conn.execute(
        "UPDATE public.refunds SET \"Status\" = 'succeeded' WHERE \"Id\" = $1 AND \"Status\" = 'pending'",
        &[&refund_id],
    )
    .ok();
    true
}
