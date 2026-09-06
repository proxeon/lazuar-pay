//! CHIP retrieve. Decrypt at this boundary; rails has no sqlx.
//! Settler never claims CHIP (`refund_idempotent = false`).

use std::time::Duration;

use domain::proof::SyncOutcome;
use rails::chip::{map_sync, FakeChip, API_BASE};
use sqlx::PgPool;

use crate::psync::SyncRail;
use crate::secret_box::SecretBox;

pub enum ChipHttp {
    Fake(FakeChip),
    Live(reqwest::Client),
}

pub struct ChipRemote {
    pool: PgPool,
    wrap_key: [u8; 32],
    http: ChipHttp,
}

impl ChipRemote {
    pub fn live(pool: PgPool, wrap_key: [u8; 32]) -> Self {
        let client = reqwest::Client::builder()
            .timeout(Duration::from_secs(10))
            .redirect(reqwest::redirect::Policy::none())
            .build()
            .unwrap_or_else(|_| reqwest::Client::new());
        Self {
            pool,
            wrap_key,
            http: ChipHttp::Live(client),
        }
    }

    pub fn fake(pool: PgPool, wrap_key: [u8; 32], fake: FakeChip) -> Self {
        Self {
            pool,
            wrap_key,
            http: ChipHttp::Fake(fake),
        }
    }

    async fn secret(&self, tenant_id: &str) -> Option<String> {
        let cred = storage::get_credential(&self.pool, tenant_id, "chip")
            .await
            .ok()
            .flatten()?;
        SecretBox::new(self.wrap_key)
            .unprotect_str(&cred.ciphertext)
            .ok()
    }

    async fn retrieve_http(&self, secret: &str, purchase_id: &str) -> (u16, String) {
        match &self.http {
            ChipHttp::Fake(f) => f.retrieve(purchase_id),
            ChipHttp::Live(c) => {
                let url = format!("{API_BASE}/purchases/{purchase_id}/");
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
}

impl SyncRail for ChipRemote {
    async fn retrieve(&self, tenant_id: &str, rail: &str, session_id: &str) -> SyncOutcome {
        if rail != "chip" {
            return SyncOutcome::Unknown;
        }
        let Some(secret) = self.secret(tenant_id).await else {
            return SyncOutcome::Unknown;
        };
        let (status, body) = self.retrieve_http(&secret, session_id).await;
        map_sync(status, &body)
    }
}
