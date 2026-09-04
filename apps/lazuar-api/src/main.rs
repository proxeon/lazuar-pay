//! lazuar-api — sync Rust port of `apps/lazuar-pay` (branch `rust-port`).
//!
//! Reference implementation: the C# service in `apps/lazuar-pay`, which stays
//! frozen and runnable during the entire port. This crate must reach fixture
//! parity with it before any cutover decision — see
//! `plans/023-evals/04-rust-port-spec.md` for the gates and
//! `PORT_DECISIONS.md` for the running decision log.

use std::sync::Arc;

use lazuar_api::app::{self, State};
use lazuar_api::config::Config;
use lazuar_api::transport::{Transport, UreqTransport};

fn main() {
    env_logger::builder()
        .filter_level(log::LevelFilter::Info)
        .parse_default_env()
        .init();

    let config = Config::from_env();
    if let Err(err) = lazuar_api::boot::run(&config) {
        eprintln!("{err}");
        std::process::exit(1);
    }
    if let Err(err) = lazuar_api::boot::probe_solana_rpc(&config) {
        eprintln!("{err}");
        std::process::exit(1);
    }

    let pool = config.connection_string.as_deref().map(|cs| {
        let manager = r2d2_postgres::PostgresConnectionManager::new(
            cs.parse().expect("invalid ConnectionStrings__Pay"),
            postgres::NoTls,
        );
        r2d2::Pool::builder()
            .build(manager)
            .expect("failed to build db pool")
    });

    // Real transports. Timeouts mirror Program.cs: rails 10s, One 2s — the
    // 100s-default hazard is not ported.
    let psp: Arc<dyn Transport> = Arc::new(UreqTransport::new(10));
    let one: Arc<dyn Transport> = Arc::new(UreqTransport::new(2));
    let one_client = lazuar_api::identity::one_client::OneClient {
        base_url: config.one_base_url.clone(),
        timeout_secs: config.one_timeout_secs,
    };
    let secret_box = lazuar_api::secrets::SecretBox::from_env(
        &config.environment,
        config.wrap_key.as_deref(),
    )
    .expect("Pay__WrapKey missing or invalid (PayBoot B1/B5)");

    let state = Arc::new(State {
        config: config.clone(),
        started_at: chrono::Utc::now(),
        pool,
        psp,
        one,
        one_client,
        secret_box,
        fulfill_gates: Default::default(),
        start_gates: Default::default(),
        link_gates: Default::default(),
        limiter: Default::default(),
        whoami_cache: Arc::new(lazuar_api::identity::whoami_cache::OneWhoamiCache::new()),
    });

    // Background workers: outbound webhook dispatch + Solana reference watcher.
    // Skipped in Testing to mirror the C# hosted-service env gate.
    if config.environment != "Testing" {
        if let Some(cs) = config.connection_string.clone() {
            let box_one = state.secret_box.clone();
            let environment = config.environment.clone();
            std::thread::spawn(move || {
                lazuar_api::workers::webhook_worker(cs, box_one, environment);
            });
        }
        if let Some(cs) = config.connection_string.clone() {
            let box_one = state.secret_box.clone();
            let environment = config.environment.clone();
            let cluster = config.solana_cluster.clone();
            let rpc_url = config.solana_rpc_url.clone();
            let ttl_minutes = config.reservation_ttl_minutes;
            std::thread::spawn(move || {
                lazuar_api::workers::solana_watcher(cs, box_one, environment, cluster, rpc_url, ttl_minutes);
            });
        }
        if let Some(cs) = config.connection_string.clone() {
            std::thread::spawn(move || {
                lazuar_api::workers::refund_reconciliation_worker(cs);
            });
        }
    }

    app::serve(config.listen_addr, state);
}

