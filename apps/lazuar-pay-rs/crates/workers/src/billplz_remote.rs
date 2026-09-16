//! Billplz retrieve. Decrypt at this boundary; rails has no sqlx. No refund API.

use std::time::Duration;

use domain::proof::SyncOutcome;
use rails::billplz::{api_host, map_sync, FakeBillplz};
use sqlx::PgPool;

use crate::psync::SyncRail;
use crate::secret_box::SecretBox;

pub enum BillplzHttp {
    Fake(FakeBillplz),
    Live(reqwest::Client),
}

pub struct BillplzRemote {
    pool: PgPool,
    wrap_key: [u8; 32],
    http: BillplzHttp,
}

impl BillplzRemote {
    pub fn live(pool: PgPool, wrap_key: [u8; 32]) -> Self {
        let client = reqwest::Client::builder()
            .timeout(Duration::from_secs(10))
            .redirect(reqwest::redirect::Policy::none())
            .build()
            .unwrap_or_else(|_| reqwest::Client::new());
        Self {
            pool,
            wrap_key,
            http: BillplzHttp::Live(client),
        }
    }

    pub fn fake(pool: PgPool, wrap_key: [u8; 32], fake: FakeBillplz) -> Self {
        Self {
            pool,
            wrap_key,
            http: BillplzHttp::Fake(fake),
        }
    }

    async fn account(&self, tenant_id: &str) -> Option<(String, String)> {
        let cred = storage::get_credential(&self.pool, tenant_id, "billplz")
            .await
            .ok()
            .flatten()?;
        let secret = SecretBox::new(self.wrap_key)
            .unprotect_str(&cred.ciphertext)
            .ok()?;
        Some((secret, cred.environment))
    }

    async fn retrieve_http(&self, secret: &str, env: &str, bill_id: &str) -> (u16, String) {
        match &self.http {
            BillplzHttp::Fake(f) => f.retrieve(bill_id),
            BillplzHttp::Live(c) => {
                let url = format!("{}/bills/{bill_id}", api_host(env));
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

impl SyncRail for BillplzRemote {
    async fn retrieve(&self, tenant_id: &str, rail: &str, session_id: &str) -> SyncOutcome {
        if rail != "billplz" {
            return SyncOutcome::Unknown;
        }
        let Some((secret, env)) = self.account(tenant_id).await else {
            return SyncOutcome::Unknown;
        };
        let (status, body) = self.retrieve_http(&secret, &env, session_id).await;
        map_sync(status, &body)
    }
}
