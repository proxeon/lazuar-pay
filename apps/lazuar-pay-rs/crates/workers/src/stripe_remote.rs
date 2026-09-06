//! Stripe retrieve + refund. Decrypt at this boundary; rails has no sqlx.

use std::time::Duration;

use domain::proof::{RefundOutcome, SyncOutcome};
use rails::stripe::{
    capture_id_from_body, map_refund, map_sync, refund_idempotency_key, FakeStripe, API_BASE,
};
use sqlx::PgPool;
use uuid::Uuid;

use crate::psync::SyncRail;
use crate::secret_box::SecretBox;
use crate::settler::RefundRemote;

pub enum StripeHttp {
    Fake(FakeStripe),
    Live(reqwest::Client),
}

pub struct StripeRemote {
    pool: PgPool,
    wrap_key: [u8; 32],
    http: StripeHttp,
}

impl StripeRemote {
    pub fn live(pool: PgPool, wrap_key: [u8; 32]) -> Self {
        let client = reqwest::Client::builder()
            .timeout(Duration::from_secs(10))
            .redirect(reqwest::redirect::Policy::none())
            .build()
            .unwrap_or_else(|_| reqwest::Client::new());
        Self {
            pool,
            wrap_key,
            http: StripeHttp::Live(client),
        }
    }

    pub fn fake(pool: PgPool, wrap_key: [u8; 32], fake: FakeStripe) -> Self {
        Self {
            pool,
            wrap_key,
            http: StripeHttp::Fake(fake),
        }
    }

    async fn secret(&self, tenant_id: &str) -> Option<String> {
        let cred = storage::get_credential(&self.pool, tenant_id, "stripe")
            .await
            .ok()
            .flatten()?;
        SecretBox::new(self.wrap_key)
            .unprotect_str(&cred.ciphertext)
            .ok()
    }

    async fn retrieve_http(&self, secret: &str, session_id: &str) -> (u16, String) {
        match &self.http {
            StripeHttp::Fake(f) => f.retrieve(session_id),
            StripeHttp::Live(c) => {
                let url = format!("{API_BASE}/v1/checkout/sessions/{session_id}");
                match c.get(url).bearer_auth(secret).send().await {
                    Ok(res) => {
                        let status = res.status().as_u16();
                        let body = res.text().await.unwrap_or_default();
                        (status, body)
                    }
                    Err(_) => (0, String::new()),
                }
            }
        }
    }

    async fn refund_http(
        &self,
        secret: &str,
        payment_intent: &str,
        amount_minor: i64,
        idem: &str,
    ) -> (u16, String) {
        match &self.http {
            StripeHttp::Fake(f) => f.refund(payment_intent, amount_minor, idem),
            StripeHttp::Live(c) => {
                let url = format!("{API_BASE}/v1/refunds");
                let body = format!("payment_intent={payment_intent}&amount={amount_minor}");
                match c
                    .post(url)
                    .bearer_auth(secret)
                    .header("Idempotency-Key", idem)
                    .header("Content-Type", "application/x-www-form-urlencoded")
                    .body(body)
                    .send()
                    .await
                {
                    Ok(res) => {
                        let status = res.status().as_u16();
                        let text = res.text().await.unwrap_or_default();
                        (status, text)
                    }
                    Err(_) => (0, String::new()),
                }
            }
        }
    }

    async fn resolve_pi(
        &self,
        secret: &str,
        capture_id: Option<&str>,
        session_id: Option<&str>,
    ) -> Option<String> {
        if let Some(id) = capture_id.filter(|s| s.starts_with("pi_")) {
            return Some(id.to_string());
        }
        let sid = session_id.filter(|s| !s.is_empty())?;
        let (status, body) = self.retrieve_http(secret, sid).await;
        if !(200..300).contains(&status) {
            return capture_id
                .filter(|s| s.starts_with("pi_"))
                .map(str::to_string);
        }
        capture_id_from_body(&body)
    }
}

impl SyncRail for StripeRemote {
    async fn retrieve(&self, tenant_id: &str, rail: &str, session_id: &str) -> SyncOutcome {
        if rail != "stripe" {
            return SyncOutcome::Unknown;
        }
        let Some(secret) = self.secret(tenant_id).await else {
            return SyncOutcome::Unknown;
        };
        let (status, body) = self.retrieve_http(&secret, session_id).await;
        map_sync(status, &body)
    }
}

impl RefundRemote for StripeRemote {
    async fn refund(&self, rail: &str, refund_id: Uuid) -> RefundOutcome {
        if rail != "stripe" {
            return RefundOutcome::Unknown;
        }
        let Some((tenant_id, amount_minor, payment_id)) =
            (match storage::refund_amount(&self.pool, refund_id).await {
                Ok(v) => v,
                Err(_) => return RefundOutcome::Unknown,
            })
        else {
            return RefundOutcome::Unknown;
        };
        if amount_minor <= 0 {
            return RefundOutcome::Rejected;
        }
        let Some(secret) = self.secret(&tenant_id).await else {
            return RefundOutcome::Unknown;
        };
        let refs = storage::latest_attempt_refs(&self.pool, payment_id)
            .await
            .ok()
            .flatten();
        let (capture_id, session_id) = refs.unwrap_or((None, None));
        let Some(pi) = self
            .resolve_pi(&secret, capture_id.as_deref(), session_id.as_deref())
            .await
        else {
            return RefundOutcome::Rejected;
        };
        let idem = refund_idempotency_key(&refund_id.to_string());
        let (status, body) = self.refund_http(&secret, &pi, amount_minor, &idem).await;
        map_refund(status, &body)
    }
}
