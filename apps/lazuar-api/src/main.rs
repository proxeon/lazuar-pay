//! lazuar-api — sync Rust port of `apps/lazuar-pay` (branch `rust-port`).
//!
//! Reference implementation: the C# service in `apps/lazuar-pay`, which stays
//! frozen and runnable during the entire port. This crate must reach fixture
//! parity with it before any cutover decision — see
//! `plans/023-evals/04-rust-port-spec.md` for the gates and
//! `PORT_DECISIONS.md` for the running decision log.

use std::sync::Arc;

type PgPool = r2d2::Pool<r2d2_postgres::PostgresConnectionManager<postgres::NoTls>>;

fn main() {
    let addr = std::env::var("LISTEN_ADDR").unwrap_or_else(|_| "127.0.0.1:8095".to_string());
    let state = Arc::new(State::from_env());

    println!("lazuar-api (sync rust) listening on http://{addr}");

    // rouille: thread-per-request. Blocking inside a handler is safe by design —
    // there is no executor to stall (PORT_DECISIONS D001).
    rouille::start_server(addr, move |request| router(request, &state));
}

/// Anything a handler might reach for. Grows as phases land; stays cheap to clone
/// into each rouille worker thread.
struct State {
    #[allow(dead_code)] // consumed by the request-log phase (port order step 9)
    started_at: chrono::DateTime<chrono::Utc>,
    pool: Option<PgPool>,
}

impl State {
    fn from_env() -> Self {
        let pool = match std::env::var("Pay__ConnectionString") {
            Ok(cs) => {
                let manager = r2d2_postgres::PostgresConnectionManager::new(
                    cs.parse().expect("invalid Pay__ConnectionString"),
                    postgres::NoTls,
                );
                match r2d2::Pool::builder().build(manager) {
                    Ok(pool) => Some(pool),
                    // D005: the .NET service stays authoritative; a missing DB must not
                    // stop this process from booting for local fixture work.
                    Err(err) => {
                        eprintln!("db pool unavailable, running degraded: {err}");
                        None
                    }
                }
            }
            Err(_) => None,
        };
        Self { started_at: chrono::Utc::now(), pool }
    }
}

fn router(request: &rouille::Request, state: &State) -> rouille::Response {
    let route = format!("{} {}", request.method(), request.url());
    match route.as_str() {
        "GET /health" => health(state),
        "GET /ready" => ready(state),
        _ => not_found(&route),
    }
}

fn health(_state: &State) -> rouille::Response {
    rouille::Response::json(&serde_json::json!({ "status": "ok" }))
}

/// DB-ping readiness, mirroring `Hosting/HealthEndpoints.cs` + `Hosting/PayReady.cs`:
/// `/ready` degrades when the database is unreachable; `/health` never does.
fn ready(state: &State) -> rouille::Response {
    let db_ok = match &state.pool {
        Some(pool) => pool
            .get()
            .ok()
            .map(|mut conn| conn.query_one("SELECT 1", &[]).is_ok())
            .unwrap_or(false),
        None => false,
    };

    if db_ok {
        rouille::Response::json(&serde_json::json!({ "status": "ready" }))
    } else {
        rouille::Response::json(&serde_json::json!({
            "status": "not_ready",
            "checks": { "database": { "ok": false } }
        }))
        .with_status_code(503)
    }
}

fn not_found(route: &str) -> rouille::Response {
    let _ = route;
    rouille::Response::json(&serde_json::json!({
        "status": 404,
        "title": "Not Found"
    }))
    .with_status_code(404)
}
