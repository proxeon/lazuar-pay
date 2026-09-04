//! Background worker loops — the port of `OutboundWebhookWorker` and the
//! Solana confirm worker hosting. Every failure is logged (the C# webhook
//! worker had no logger at all — plans/023-evals/02 B2).

use std::time::Duration;

use chrono::Duration as ChronoDuration;

use crate::money::fulfillment::CheckoutGates;
use crate::rails::solana::confirm::{ConfirmDeps, Watcher};
use crate::rails::solana::rpc::SolanaRpc;
use crate::secrets::SecretBox;
use crate::transport::UreqTransport;

fn connect(conn_string: &str) -> postgres::Client {
    postgres::Client::connect(conn_string, postgres::NoTls)
        .expect("worker cannot open a database connection")
}

/// Outbound webhook dispatcher: claim → deliver → per-row persistence, every 5s.
pub fn webhook_worker(conn_string: String, box_one: SecretBox, environment: String) -> ! {
    loop {
        let result = (|| -> Result<usize, String> {
            let mut conn = connect(&conn_string);
            let transport = crate::webhooks::dispatch::webhook_transport(&environment);
            crate::webhooks::dispatch::process_batch(&mut conn, &box_one, transport.as_ref(), &environment)
                .map_err(|e| format!("batch: {e}"))
        })();
        match result {
            Ok(0) => {}
            Ok(n) => log::info!("webhook dispatch processed {n} deliveries"),
            Err(message) => log::error!("webhook dispatch pass failed: {message}"),
        }
        std::thread::sleep(Duration::from_secs(5));
    }
}

/// Solana reference watcher: claim open solana checkouts (2s lease), confirm
/// by reference signatures, expire watch-timeout — every 2s, draining claims.
pub fn solana_watcher(
    conn_string: String,
    box_one: SecretBox,
    environment: String,
    cluster: String,
    rpc_url: String,
    ttl_minutes: i64,
) -> ! {
    loop {
        let result = (|| -> Result<usize, String> {
            let mut conn = connect(&conn_string);
            let transport = Box::new(UreqTransport::new(10));
            let rpc = SolanaRpc { rpc_url: Some(rpc_url.clone()), transport };
            let gates = CheckoutGates::default();
            let deps = ConfirmDeps {
                box_one: &box_one,
                gates: &gates,
                rpc: &rpc,
                environment: &environment,
                config_cluster: &cluster,
            };
            let mut watcher = Watcher {
                conn: &mut conn,
                deps: &deps,
                ttl: ChronoDuration::minutes(ttl_minutes.max(1)),
            };
            // Drain claims: run_once re-claims until the batch is empty.
            let mut processed = watcher.run_once().map_err(|e| e.to_string())?;
            while processed > 0 {
                processed = watcher.run_once().map_err(|e| e.to_string())?;
            }
            Ok(0)
        })();
        match result {
            Ok(_) => {}
            Err(message) => log::error!("solana watcher pass failed: {message}"),
        }
        std::thread::sleep(Duration::from_secs(2));
    }
}
