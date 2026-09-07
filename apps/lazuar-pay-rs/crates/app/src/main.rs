//! `serve` / `--api-only` / `--worker-only` / `--watcher-only`: TypeSpec `/v1` + SKIP LOCKED loops.

use std::net::SocketAddr;
use std::sync::Arc;

use api::boot::{throw_if_misconfigured, wrap_key_bytes, BootCfg, Env};
use api::cors;
use api::identity::{FakeOne, HttpOne, OneClient, WhoamiCache};
use api::limiter::Limiter;
use api::mint_http;
use api::stripe_http::FakeStripe;
use api::AppState;
use rails::billplz::FakeBillplz;
use rails::chip::FakeChip;
use rails::razorpay::FakeRazorpay;
use rails::solana::FakeSolanaRpc;
use rails::xendit::FakeXendit;
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
                   serve              api + workers\n\
                   --api-only         api, no loops\n\
                   --worker-only      loops, no :8081\n\
                   --watcher-only     Solana watch + bind, no :8081\n"
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
            if let Err(e) = watcher_only().await {
                eprintln!("{e}");
                std::process::exit(1);
            }
        }
        other => {
            eprintln!("unknown argument: {other}");
            std::process::exit(2);
        }
    }
}

#[derive(Clone, Default)]
struct Fakes {
    stripe: FakeStripe,
    chip: FakeChip,
    billplz: FakeBillplz,
    xendit: FakeXendit,
    razorpay: FakeRazorpay,
    solana: FakeSolanaRpc,
}

fn workers_cfg(
    pool: sqlx::PgPool,
    env: Env,
    wrap_key: [u8; 32],
    cfg: &BootCfg,
    fakes: &Fakes,
) -> workers::Config {
    let cluster =
        rails::solana::normalize_cluster(&cfg.solana_cluster).unwrap_or_else(|| "devnet".into());
    let rpc = {
        let s = cfg.solana_rpc_url.trim();
        if s.is_empty() {
            None
        } else {
            Some(s.to_string())
        }
    };
    let testing = env == Env::Testing;
    workers::Config {
        pool,
        wrap_key,
        allow_loopback: env.allows_test(),
        retention: storage::RetentionCfg::default(),
        solana_cluster: cluster,
        solana_rpc_url: rpc,
        solana_fake: testing.then_some(fakes.solana.clone()),
        use_live_psp: !testing,
        stripe_fake: fakes.stripe.clone(),
        chip_fake: fakes.chip.clone(),
        billplz_fake: fakes.billplz.clone(),
        xendit_fake: fakes.xendit.clone(),
        razorpay_fake: fakes.razorpay.clone(),
    }
}

async fn probe_solana(cfg: &BootCfg) -> Result<(), Box<dyn std::error::Error>> {
    let rpc = cfg.solana_rpc_url.trim();
    if rpc.is_empty() {
        return Ok(());
    }
    let cluster = rails::solana::normalize_cluster(&cfg.solana_cluster)
        .unwrap_or_else(|| cfg.solana_cluster.trim().to_string());
    let expected = rails::solana::genesis_hash(&cluster);
    let live = chain::solana::LiveRpc::new(rpc.to_string());
    let hash = match live.get_genesis_hash().await {
        Ok(h) => h,
        Err(_) => return Err("Pay:Solana:RpcUrl is unreachable".into()),
    };
    if hash != expected {
        return Err("Pay:Solana:RpcUrl genesis hash mismatch".into());
    }
    Ok(())
}

async fn connect_pool() -> Result<(Env, BootCfg, sqlx::PgPool, [u8; 32]), Box<dyn std::error::Error>>
{
    let env =
        Env::parse(&std::env::var("ASPNETCORE_ENVIRONMENT").unwrap_or_else(|_| "Testing".into()));
    let cfg = BootCfg::from_env();
    throw_if_misconfigured(env, &cfg)?;
    let wrap_key = wrap_key_bytes(env, &cfg.wrap_key)?;
    let _ = SecretBox::new(wrap_key);
    if cfg.connection_string.trim().is_empty() {
        return Err("ConnectionStrings:Pay is required".into());
    }
    let pool = sqlx::postgres::PgPoolOptions::new()
        .max_connections(8)
        .connect(cfg.connection_string.trim())
        .await?;
    storage::migrate(&pool).await?;
    probe_solana(&cfg).await?;
    Ok((env, cfg, pool, wrap_key))
}

async fn watcher_only() -> Result<(), Box<dyn std::error::Error>> {
    let (env, cfg, pool, wrap_key) = connect_pool().await?;
    eprintln!("lazuar-pay-rs watcher only");
    workers::watcher_only(workers_cfg(pool, env, wrap_key, &cfg, &Fakes::default())).await;
    Ok(())
}

async fn worker_only() -> Result<(), Box<dyn std::error::Error>> {
    let (env, cfg, pool, wrap_key) = connect_pool().await?;
    eprintln!("lazuar-pay-rs workers only");
    workers::run(workers_cfg(pool, env, wrap_key, &cfg, &Fakes::default())).await;
    Ok(())
}

async fn serve(with_workers: bool) -> Result<(), Box<dyn std::error::Error>> {
    let (env, cfg, pool, wrap_key) = connect_pool().await?;
    let secret = std::env::var("Pay__TestWebhookSecret").unwrap_or_else(|_| "test-secret".into());
    let max: u32 = if cfg.start_max_per_minute > 0 {
        cfg.start_max_per_minute as u32
    } else {
        20
    };
    let one = match cfg.one_base_url.trim() {
        "" if env == Env::Testing => OneClient::Fake(FakeOne::writer("t1")),
        "" => return Err("One:BaseUrl must be a public URL in Production and Staging".into()),
        base => OneClient::Http(HttpOne::new(base)),
    };
    let cors_origins = cors::resolve(&cfg.cors_origins_raw, env)?;
    let fakes = Fakes::default();
    let testing = env == Env::Testing;
    let wcfg = workers_cfg(pool.clone(), env, wrap_key, &cfg, &fakes);
    let state = AppState {
        pool,
        env,
        checkout_base_url: if cfg.checkout_base_url.trim().is_empty() {
            "http://localhost:5179".into()
        } else {
            cfg.checkout_base_url.clone()
        },
        test_webhook_secret: secret,
        one,
        whoami_cache: Arc::new(WhoamiCache::new()),
        limiter: Arc::new(Limiter::new(max)),
        wrap_key,
        stripe: fakes.stripe,
        chip: fakes.chip,
        billplz: fakes.billplz,
        xendit: fakes.xendit,
        razorpay: fakes.razorpay,
        solana: fakes.solana,
        solana_cluster: rails::solana::normalize_cluster(&cfg.solana_cluster)
            .unwrap_or_else(|| "devnet".into()),
        public_base_url: std::env::var("Pay__PublicBaseUrl")
            .unwrap_or_else(|_| "https://pay.example.test".into()),
        one_webhook_secret: std::env::var("Pay__OneWebhookSecret").unwrap_or_default(),
        metrics_token: std::env::var("Pay__MetricsToken").unwrap_or_default(),
        cors_origins,
        live_http: if testing {
            None
        } else {
            Some(mint_http::live_client())
        },
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
