//! Razorpay retrieve. Decrypt at this boundary; rails has no sqlx. No refund API.

use std::time::Duration;

use domain::proof::SyncOutcome;
use rails::razorpay::{map_sync, try_split, FakeRazorpay, API_BASE};
use sqlx::PgPool;

use crate::psync::SyncRail;
use crate::secret_box::SecretBox;

pub enum RazorpayHttp {
    Fake(FakeRazorpay),
    Live(reqwest::Client),
}

pub struct RazorpayRemote {
    pool: PgPool,
    wrap_key: [u8; 32],
    http: RazorpayHttp,
}

impl RazorpayRemote {
    pub fn live(pool: PgPool, wrap_key: [u8; 32]) -> Self {
        let client = reqwest::Client::builder()
            .timeout(Duration::from_secs(10))
            .redirect(reqwest::redirect::Policy::none())
            .build()
            .unwrap_or_else(|_| reqwest::Client::new());
        Self {
            pool,
            wrap_key,
            http: RazorpayHttp::Live(client),
        }
    }

    pub fn fake(pool: PgPool, wrap_key: [u8; 32], fake: FakeRazorpay) -> Self {
        Self {
            pool,
            wrap_key,
            http: RazorpayHttp::Fake(fake),
        }
    }

    async fn keys(&self, tenant_id: &str) -> Option<(String, String)> {
        let cred = storage::get_credential(&self.pool, tenant_id, "razorpay")
            .await
            .ok()
            .flatten()?;
        let secret = SecretBox::new(self.wrap_key)
            .unprotect_str(&cred.ciphertext)
            .ok()?;
        let (id, sec) = try_split(&secret)?;
        Some((id.to_string(), sec.to_string()))
    }

    async fn retrieve_http(&self, key_id: &str, key_secret: &str, link_id: &str) -> (u16, String) {
        match &self.http {
            RazorpayHttp::Fake(f) => f.retrieve(link_id),
            RazorpayHttp::Live(c) => {
                let url = format!("{API_BASE}/payment_links/{link_id}");
                match c.get(url).basic_auth(key_id, Some(key_secret)).send().await {
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
}

impl SyncRail for RazorpayRemote {
    async fn retrieve(&self, tenant_id: &str, rail: &str, session_id: &str) -> SyncOutcome {
        if rail != "razorpay" {
            return SyncOutcome::Unknown;
        }
        let Some((key_id, key_secret)) = self.keys(tenant_id).await else {
            return SyncOutcome::Unknown;
        };
        let (status, body) = self.retrieve_http(&key_id, &key_secret, session_id).await;
        map_sync(status, &body)
    }
}
