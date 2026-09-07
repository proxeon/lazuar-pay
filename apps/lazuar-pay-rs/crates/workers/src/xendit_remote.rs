//! Xendit retrieve. Decrypt at this boundary; rails has no sqlx. No refund API.

use std::time::Duration;

use domain::proof::SyncOutcome;
use rails::xendit::{map_sync, FakeXendit, API_BASE};
use sqlx::PgPool;

use crate::psync::SyncRail;
use crate::secret_box::SecretBox;

pub enum XenditHttp {
    Fake(FakeXendit),
    Live(reqwest::Client),
}

pub struct XenditRemote {
    pool: PgPool,
    wrap_key: [u8; 32],
    http: XenditHttp,
}

impl XenditRemote {
    pub fn live(pool: PgPool, wrap_key: [u8; 32]) -> Self {
        let client = reqwest::Client::builder()
            .timeout(Duration::from_secs(10))
            .redirect(reqwest::redirect::Policy::none())
            .build()
            .unwrap_or_else(|_| reqwest::Client::new());
        Self {
            pool,
            wrap_key,
            http: XenditHttp::Live(client),
        }
    }

    pub fn fake(pool: PgPool, wrap_key: [u8; 32], fake: FakeXendit) -> Self {
        Self {
            pool,
            wrap_key,
            http: XenditHttp::Fake(fake),
        }
    }

    async fn secret(&self, tenant_id: &str) -> Option<String> {
        let cred = storage::get_credential(&self.pool, tenant_id, "xendit")
            .await
            .ok()
            .flatten()?;
        SecretBox::new(self.wrap_key)
            .unprotect_str(&cred.ciphertext)
            .ok()
    }

    async fn retrieve_http(&self, secret: &str, invoice_id: &str) -> (u16, String) {
        match &self.http {
            XenditHttp::Fake(f) => f.retrieve(invoice_id),
            XenditHttp::Live(c) => {
                let url = format!("{API_BASE}/v2/invoices/{invoice_id}");
                match c.get(url).basic_auth(secret, None::<&str>).send().await {
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

impl SyncRail for XenditRemote {
    async fn retrieve(&self, tenant_id: &str, rail: &str, session_id: &str) -> SyncOutcome {
        if rail != "xendit" {
            return SyncOutcome::Unknown;
        }
        let Some(secret) = self.secret(tenant_id).await else {
            return SyncOutcome::Unknown;
        };
        let (status, body) = self.retrieve_http(&secret, session_id).await;
        map_sync(status, &body)
    }
}
