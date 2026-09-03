//! Application assembly + router. The C# equivalent is `Program.cs` +
//! `Hosting/*`; tests assemble this same `State` with fake transports
//! (see `tests/support`), which is the Rust analogue of `PayApiFactory`.

use std::sync::Arc;

use crate::config::Config;
use crate::transport::Transport;

pub type PgPool = r2d2::Pool<r2d2_postgres::PostgresConnectionManager<postgres::NoTls>>;

pub struct State {
    pub config: Config,
    pub started_at: chrono::DateTime<chrono::Utc>,
    pub pool: Option<PgPool>,
    pub psp: Arc<dyn Transport>,
    pub one: Arc<dyn Transport>,
}

pub fn router(request: &rouille::Request, state: &State) -> rouille::Response {
    let route = format!("{} {}", request.method(), request.url());
    match route.as_str() {
        "GET /health" => health(),
        "GET /ready" => ready(state),
        _ => not_found(),
    }
}

fn health() -> rouille::Response {
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

fn not_found() -> rouille::Response {
    rouille::Response::json(&serde_json::json!({
        "status": 404,
        "title": "Not Found"
    }))
    .with_status_code(404)
}

/// Block serving requests. rouille is thread-per-request: blocking inside a
/// handler is safe by design — there is no executor to stall (D001).
pub fn serve(addr: String, state: Arc<State>) -> ! {
    println!("lazuar-api (sync rust) listening on http://{addr}");
    rouille::start_server(addr, move |request| router(request, &state))
}
