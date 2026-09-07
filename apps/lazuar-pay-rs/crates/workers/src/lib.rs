//! PG-lease loops. CHIP refunds are never auto-claimed (`refund_idempotent = false`).

#![forbid(unsafe_code)]

pub mod billplz_remote;
pub mod chip_remote;
pub mod expire;
pub mod hmac;
pub mod outbound;
pub mod outbound_url;
pub mod psync;
pub mod razorpay_remote;
pub mod retention;
pub mod secret_box;
pub mod settler;
pub mod solana_bind;
pub mod solana_watch;
pub mod stripe_remote;
pub mod xendit_remote;

use std::time::Duration as StdDuration;

use sqlx::PgPool;
use storage::RetentionCfg;

use crate::billplz_remote::BillplzRemote;
use crate::chip_remote::ChipRemote;
use crate::outbound::OutboundCfg;
use crate::psync::Dispatch;
use crate::razorpay_remote::RazorpayRemote;
use crate::secret_box::SecretBox;
use crate::solana_watch::Rpc;
use crate::stripe_remote::StripeRemote;
use crate::xendit_remote::XenditRemote;
use rails::solana::FakeSolanaRpc;

#[derive(Clone)]
pub struct Config {
    pub pool: PgPool,
    pub wrap_key: [u8; 32],
    pub allow_loopback: bool,
    pub retention: RetentionCfg,
    pub solana_cluster: String,
    pub solana_rpc_url: Option<String>,
    pub solana_fake: Option<FakeSolanaRpc>,
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

    let rpc = if let Some(fake) = cfg.solana_fake.clone() {
        Some(Rpc::Fake(fake))
    } else {
        cfg.solana_rpc_url
            .clone()
            .map(|u| Rpc::Live(chain::solana::LiveRpc::new(u)))
    };
    let mut expire_tick = tokio::time::interval(StdDuration::from_secs(5));
    let mut outbound_tick = tokio::time::interval(StdDuration::from_secs(5));
    let mut psync_tick = tokio::time::interval(StdDuration::from_secs(5));
    let mut settler_tick = tokio::time::interval(StdDuration::from_secs(15));
    let mut watch_tick = tokio::time::interval(StdDuration::from_secs(2));
    let mut bind_tick = tokio::time::interval(StdDuration::from_secs(2));
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
                let remote = Dispatch {
                    stripe: StripeRemote::live(cfg.pool.clone(), cfg.wrap_key),
                    chip: ChipRemote::live(cfg.pool.clone(), cfg.wrap_key),
                    billplz: BillplzRemote::live(cfg.pool.clone(), cfg.wrap_key),
                    xendit: XenditRemote::live(cfg.pool.clone(), cfg.wrap_key),
                    razorpay: RazorpayRemote::live(cfg.pool.clone(), cfg.wrap_key),
                };
                let _ = psync::process_batch(&cfg.pool, &remote).await;
            }
            _ = settler_tick.tick() => {
                let remote = StripeRemote::live(cfg.pool.clone(), cfg.wrap_key);
                let _ = settler::process_batch(&cfg.pool, &remote).await;
            }
            _ = watch_tick.tick() => {
                if let Some(rpc) = rpc.as_ref() {
                    if let Err(storage::ApplyError::Conflict) =
                        solana_watch::once(&cfg.pool, rpc, &cfg.solana_cluster).await
                    {
                        watch_tick = tokio::time::interval(StdDuration::from_secs(15));
                        watch_tick.tick().await;
                    }
                }
            }
            _ = bind_tick.tick() => {
                let _ = solana_bind::once(&cfg.pool).await;
            }
        }
    }
}

pub async fn watcher_only(cfg: Config) {
    let rpc = if let Some(fake) = cfg.solana_fake.clone() {
        Some(Rpc::Fake(fake))
    } else {
        cfg.solana_rpc_url
            .clone()
            .map(|u| Rpc::Live(chain::solana::LiveRpc::new(u)))
    };
    let mut watch_tick = tokio::time::interval(StdDuration::from_secs(2));
    let mut bind_tick = tokio::time::interval(StdDuration::from_secs(2));
    loop {
        tokio::select! {
            _ = watch_tick.tick() => {
                if let Some(rpc) = rpc.as_ref() {
                    let _ = solana_watch::once(&cfg.pool, rpc, &cfg.solana_cluster).await;
                }
            }
            _ = bind_tick.tick() => {
                let _ = solana_bind::once(&cfg.pool).await;
            }
        }
    }
}
