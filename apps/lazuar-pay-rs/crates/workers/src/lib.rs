//! PG-lease loops. CHIP refunds are never auto-claimed (`refund_idempotent = false`).

#![forbid(unsafe_code)]

pub mod expire;
pub mod hmac;
pub mod outbound;
pub mod outbound_url;
pub mod psync;
pub mod retention;
pub mod secret_box;
pub mod settler;

use std::time::Duration as StdDuration;

use sqlx::PgPool;
use storage::RetentionCfg;

use crate::outbound::OutboundCfg;
use crate::psync::NoopSync;
use crate::secret_box::SecretBox;
use crate::settler::NoopRefund;

#[derive(Clone)]
pub struct Config {
    pub pool: PgPool,
    pub wrap_key: [u8; 32],
    pub allow_loopback: bool,
    pub retention: RetentionCfg,
}

pub async fn run(cfg: Config) {
    let ret_pool = cfg.pool.clone();
    let ret_cfg = cfg.retention;
    tokio::spawn(async move {
        tokio::time::sleep(StdDuration::from_secs(120)).await;
        loop {
            let _ = retention::sweep(&ret_pool, ret_cfg).await;
            tokio::time::sleep(StdDuration::from_secs(86_400)).await;
        }
    });

    let mut expire_tick = tokio::time::interval(StdDuration::from_secs(5));
    let mut outbound_tick = tokio::time::interval(StdDuration::from_secs(5));
    let mut psync_tick = tokio::time::interval(StdDuration::from_secs(5));
    let mut settler_tick = tokio::time::interval(StdDuration::from_secs(15));
    loop {
        tokio::select! {
            _ = expire_tick.tick() => {
                let _ = expire::once(&cfg.pool).await;
            }
            _ = outbound_tick.tick() => {
                let box_ = SecretBox::new(cfg.wrap_key);
                let _ = outbound::process_batch(OutboundCfg {
                    pool: &cfg.pool,
                    box_,
                    allow_loopback: cfg.allow_loopback,
                }).await;
            }
            _ = psync_tick.tick() => {
                let _ = psync::process_batch(&cfg.pool, &NoopSync).await;
            }
            _ = settler_tick.tick() => {
                let _ = settler::process_batch(&cfg.pool, &NoopRefund).await;
            }
        }
    }
}
