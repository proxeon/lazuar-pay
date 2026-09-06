//! `serve` / `--api-only` / `--worker-only`: TypeSpec `/v1` + SKIP LOCKED loops.

use std::net::SocketAddr;
use std::sync::Arc;

use api::boot::{throw_if_misconfigured, wrap_key_testing_fallback, Env};
use api::identity::{FakeOne, HttpOne, OneClient, WhoamiCache};
use api::limiter::Limiter;
use api::AppState;
use base64::Engine;
use workers::secret_box::SecretBox;

#[tokio::main]
async fn main() {
    let arg = std::env::args().nth(1).unwrap_or_else(|| "serve".into());
    match arg.as_str() {
        "--help" | "-h" | "help" => {
            println!(
                "lazuar-pay-rs — new payment host (032)\n\
                 \n\
                 Commands:\n\
                   serve              api + workers (test rail)\n\
                   --api-only         api, no loops\n\
                   --worker-only      loops, no :8081\n\
                   --watcher-only     not implemented\n"
            );
        }
        "serve" => {
            if let Err(e) = serve(true).await {
                eprintln!("{e}");
                std::process::exit(1);
            }
        }
        "--api-only" => {
            if let Err(e) = serve(false).await {
                eprintln!("{e}");
                std::process::exit(1);
            }
        }
        "--worker-only" => {
            if let Err(e) = worker_only().await {
                eprintln!("{e}");
                std::process::exit(1);
            }
        }
        "--watcher-only" => {
            eprintln!("lazuar-pay-rs: {arg} is not implemented.");
            std::process::exit(2);
        }
        other => {
            eprintln!("unknown argument: {other}");
            std::process::exit(2);
        }
    }
}

fn wrap_key_bytes(env: Env, configured: &str) -> [u8; 32] {
    if let Some(k) = wrap_key_testing_fallback(env, configured) {
        return k;
    }
    if configured.is_empty() {
        return [0u8; 32];
    }
    if let Ok(bytes) = base64::engine::general_purpose::STANDARD.decode(configured) {
        if let Ok(arr) = <[u8; 32]>::try_from(bytes.as_slice()) {
            return arr;
        }
    }
    [0u8; 32]
}

fn workers_cfg(pool: sqlx::PgPool, env: Env, wrap_key: [u8; 32]) -> workers::Config {
    workers::Config {
        pool,
        wrap_key,
        allow_loopback: env.allows_test(),
        retention: storage::RetentionCfg::default(),
    }
}

async fn connect_pool() -> Result<(Env, sqlx::PgPool, [u8; 32]), Box<dyn std::error::Error>> {
    let env =
        Env::parse(&std::env::var("ASPNETCORE_ENVIRONMENT").unwrap_or_else(|_| "Testing".into()));
    throw_if_misconfigured(env)?;
    let wrap_configured = std::env::var("Pay__WrapKey").unwrap_or_default();
    let wrap_key = wrap_key_bytes(env, &wrap_configured);
    let _ = SecretBox::new(wrap_key);
    let url = std::env::var("ConnectionStrings__Pay")
        .or_else(|_| std::env::var("DATABASE_URL"))
        .map_err(|_| "ConnectionStrings__Pay is required")?;
    let pool = sqlx::postgres::PgPoolOptions::new()
        .max_connections(8)
        .connect(&url)
        .await?;
    storage::migrate(&pool).await?;
    Ok((env, pool, wrap_key))
}

async fn worker_only() -> Result<(), Box<dyn std::error::Error>> {
    let (env, pool, wrap_key) = connect_pool().await?;
    eprintln!("lazuar-pay-rs workers only");
    workers::run(workers_cfg(pool, env, wrap_key)).await;
    Ok(())
}

async fn serve(with_workers: bool) -> Result<(), Box<dyn std::error::Error>> {
    let (env, pool, wrap_key) = connect_pool().await?;
    let secret = std::env::var("Pay__TestWebhookSecret").unwrap_or_else(|_| "test-secret".into());
    let max: u32 = std::env::var("Pay__StartMaxPerMinute")
        .ok()
        .and_then(|s| s.parse().ok())
        .unwrap_or(20);
    let one = match std::env::var("One__BaseUrl") {
        Ok(base) => OneClient::Http(HttpOne::new(base)),
        Err(_) if env == Env::Testing => OneClient::Fake(FakeOne::writer("t1")),
        Err(_) => OneClient::Http(HttpOne::new("http://localhost:8080/api/v1")),
    };
    let wcfg = workers_cfg(pool.clone(), env, wrap_key);
    let state = AppState {
        pool,
        env,
        checkout_base_url: std::env::var("Pay__CheckoutBaseUrl")
            .unwrap_or_else(|_| "http://localhost:5179".into()),
        test_webhook_secret: secret,
        one,
        whoami_cache: Arc::new(WhoamiCache::new()),
        limiter: Arc::new(Limiter::new(max)),
    };
    let app = api::router(state);
    let port: u16 = std::env::var("PORT")
        .ok()
        .and_then(|s| s.parse().ok())
        .unwrap_or(8081);
    let addr = SocketAddr::from(([0, 0, 0, 0], port));
    let listener = tokio::net::TcpListener::bind(addr).await?;
    eprintln!("lazuar-pay-rs listening on {addr}");
    if with_workers {
        tokio::select! {
            r = axum::serve(listener, app) => r?,
            _ = workers::run(wcfg) => {}
        }
    } else {
        axum::serve(listener, app).await?;
    }
    Ok(())
}
