//! `serve` / `--api-only`: TypeSpec `/v1` on :8081 (test rail).

use std::net::SocketAddr;
use std::sync::Arc;

use api::boot::{throw_if_misconfigured, wrap_key_testing_fallback, Env};
use api::identity::{FakeOne, HttpOne, OneClient, WhoamiCache};
use api::limiter::Limiter;
use api::AppState;

#[tokio::main]
async fn main() {
    let arg = std::env::args().nth(1).unwrap_or_else(|| "serve".into());
    match arg.as_str() {
        "--help" | "-h" | "help" => {
            println!(
                "lazuar-pay-rs — new payment host (032)\n\
                 \n\
                 Commands:\n\
                   serve              api (test rail)\n\
                   --api-only\n\
                   --worker-only      not implemented\n\
                   --watcher-only     not implemented\n"
            );
        }
        "serve" | "--api-only" => {
            if let Err(e) = serve().await {
                eprintln!("{e}");
                std::process::exit(1);
            }
        }
        "--worker-only" | "--watcher-only" => {
            eprintln!("lazuar-pay-rs: {arg} is not implemented.");
            std::process::exit(2);
        }
        other => {
            eprintln!("unknown argument: {other}");
            std::process::exit(2);
        }
    }
}

async fn serve() -> Result<(), Box<dyn std::error::Error>> {
    let env =
        Env::parse(&std::env::var("ASPNETCORE_ENVIRONMENT").unwrap_or_else(|_| "Testing".into()));
    throw_if_misconfigured(env)?;
    let wrap_configured = std::env::var("Pay__WrapKey").unwrap_or_default();
    let _wrap_key = wrap_key_testing_fallback(env, &wrap_configured);
    let url = std::env::var("ConnectionStrings__Pay")
        .or_else(|_| std::env::var("DATABASE_URL"))
        .map_err(|_| "ConnectionStrings__Pay is required")?;
    let pool = sqlx::postgres::PgPoolOptions::new()
        .max_connections(8)
        .connect(&url)
        .await?;
    storage::migrate(&pool).await?;
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
    axum::serve(listener, app).await?;
    Ok(())
}
