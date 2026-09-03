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
    let config = Config::from_env();

    let pool = config.connection_string.as_deref().map(|cs| {
        let manager = r2d2_postgres::PostgresConnectionManager::new(
            cs.parse().expect("invalid Pay__ConnectionString"),
            postgres::NoTls,
        );
        r2d2::Pool::builder()
            .build(manager)
            .expect("failed to build db pool")
    });

    // Real transports. Per-client timeouts mirror Program.cs: solana 10s,
    // webhooks 10s, rails default 10s (the 100s-default hazard is not ported).
    let psp: Arc<dyn Transport> = Arc::new(UreqTransport::new(10));
    let one: Arc<dyn Transport> = Arc::new(UreqTransport::new(2));

    let state = Arc::new(State {
        config: config.clone(),
        started_at: chrono::Utc::now(),
        pool,
        psp,
        one,
    });

    app::serve(config.listen_addr, state);
}
