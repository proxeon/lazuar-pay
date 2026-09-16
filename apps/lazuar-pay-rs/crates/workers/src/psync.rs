//! PSync claim loop. Test/solana skipped (caps.sync = false). Fake retrieve in this slice.

use domain::proof::{Proof, SyncOutcome};
use domain::rail::RailId;
use domain::TerminalReason;
use sqlx::PgPool;
use storage::{apply, ApplyCmd, ApplyError};
use time::{Duration, OffsetDateTime};

pub trait SyncRail: Send + Sync {
    fn retrieve(
        &self,
        tenant_id: &str,
        rail: &str,
        session_id: &str,
    ) -> impl std::future::Future<Output = SyncOutcome> + Send;
}

#[derive(Clone, Copy, Default)]
pub struct NoopSync;

impl SyncRail for NoopSync {
    async fn retrieve(&self, _tenant_id: &str, _rail: &str, _session_id: &str) -> SyncOutcome {
        SyncOutcome::Unknown
    }
}

pub struct ConstSync(pub SyncOutcome);

impl SyncRail for ConstSync {
    async fn retrieve(&self, _tenant_id: &str, _rail: &str, _session_id: &str) -> SyncOutcome {
        self.0.clone()
    }
}

pub struct Dispatch<A, B, C, D, E> {
    pub stripe: A,
    pub chip: B,
    pub billplz: C,
    pub xendit: D,
    pub razorpay: E,
}

impl<A: SyncRail, B: SyncRail, C: SyncRail, D: SyncRail, E: SyncRail> SyncRail
    for Dispatch<A, B, C, D, E>
{
    async fn retrieve(&self, tenant_id: &str, rail: &str, session_id: &str) -> SyncOutcome {
        match rail {
            "stripe" => self.stripe.retrieve(tenant_id, rail, session_id).await,
            "chip" => self.chip.retrieve(tenant_id, rail, session_id).await,
            "billplz" => self.billplz.retrieve(tenant_id, rail, session_id).await,
            "xendit" => self.xendit.retrieve(tenant_id, rail, session_id).await,
            "razorpay" => self.razorpay.retrieve(tenant_id, rail, session_id).await,
            _ => SyncOutcome::Unknown,
        }
    }
}

pub async fn process_batch<S: SyncRail>(pool: &PgPool, sync: &S) -> Result<usize, ApplyError> {
    let now = OffsetDateTime::now_utc();
    let claimed = storage::claim_psync(pool, now, 20).await?;
    let n = claimed.len();
    for c in claimed {
        let Ok(rail) = RailId::parse(&c.rail) else {
            continue;
        };
        if !rail.caps().sync {
            continue;
        }
        match sync
            .retrieve(c.tenant_id.as_str(), &c.rail, &c.session_id)
            .await
        {
            SyncOutcome::Unknown => {
                let until = now + Duration::minutes(1) - Duration::seconds(30);
                let _ = storage::defer_psync(pool, c.attempt_id, until).await;
            }
            SyncOutcome::Failed { .. } => {
                let proof_id = format!("sync:{}:{}:fail", c.rail, c.session_id);
                let _ = apply(
                    pool,
                    ApplyCmd::InjectFailed {
                        tenant_id: c.tenant_id,
                        rail,
                        proof_id,
                        payment_id: c.payment_id,
                        attempt_id: c.attempt_id,
                        reason: TerminalReason::PspFailed,
                        now,
                    },
                )
                .await;
            }
            SyncOutcome::Paid { received, refs } => {
                let txn = refs
                    .capture_id
                    .clone()
                    .or(refs.network_id.clone())
                    .unwrap_or_else(|| c.session_id.clone());
                let proof_id = format!("sync:{}:{}:{txn}", c.rail, c.session_id);
                match apply(
                    pool,
                    ApplyCmd::InjectPaid {
                        tenant_id: c.tenant_id,
                        rail,
                        proof_id: proof_id.clone(),
                        payment_id: c.payment_id,
                        attempt_id: c.attempt_id,
                        received,
                        proof: Proof::PspSync {
                            rail,
                            connector_txn_id: txn,
                        },
                        now,
                        refs,
                    },
                )
                .await
                {
                    Ok(_) | Err(ApplyError::Conflict) | Err(ApplyError::NotFound) => {}
                    Err(e) => return Err(e),
                }
            }
        }
    }
    Ok(n)
}
