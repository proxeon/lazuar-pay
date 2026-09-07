//! TypeSpec `/v1` adapter over `storage::apply`. Hosted rails + catalog (033/15).

#![forbid(unsafe_code)]

pub mod boot;
pub mod checkouts;
pub mod cors;
pub mod errors;
pub mod gateway;
pub mod health;
pub mod identity;
pub mod json;
pub mod limiter;
pub mod metrics;
pub mod mint_http;
pub mod one_webhooks;
pub mod org_ready;
pub mod org_webhooks;
pub mod payment_links;
pub mod payments;
pub mod products;
pub mod public_pay;
pub mod receipts;
pub mod refunds;
pub mod request_id;
pub mod stripe_http;
pub mod subscriptions;
pub mod webhooks;

use std::sync::Arc;

use axum::middleware;
use axum::routing::{get, post, put};
use axum::Router;
use sqlx::PgPool;

use crate::boot::Env;
use crate::identity::{FakeOne, OneClient, WhoamiCache};
use crate::limiter::Limiter;
use crate::stripe_http::FakeStripe;
use rails::billplz::FakeBillplz;
use rails::chip::FakeChip;
use rails::razorpay::FakeRazorpay;
use rails::solana::FakeSolanaRpc;
use rails::xendit::FakeXendit;

#[derive(Clone)]
pub struct AppState {
    pub pool: PgPool,
    pub env: Env,
    pub checkout_base_url: String,
    pub test_webhook_secret: String,
    pub one: OneClient,
    pub whoami_cache: Arc<WhoamiCache>,
    pub limiter: Arc<Limiter>,
    pub wrap_key: [u8; 32],
    pub stripe: FakeStripe,
    pub chip: FakeChip,
    pub billplz: FakeBillplz,
    pub xendit: FakeXendit,
    pub razorpay: FakeRazorpay,
    pub solana: FakeSolanaRpc,
    pub solana_cluster: String,
    pub public_base_url: String,
    /// Process One HMAC secret (`Pay__OneWebhookSecret`). Empty → org ciphertext.
    pub one_webhook_secret: String,
    /// Empty → `/metrics` is open. Set `Pay__MetricsToken` in serve.
    pub metrics_token: String,
    pub cors_origins: Vec<String>,
    /// `None` → Fake mint (Testing). `Some` → live PSP HTTP.
    pub live_http: Option<reqwest::Client>,
}

pub fn router(state: AppState) -> Router {
    Router::new()
        .route("/health", get(health::health))
        .route("/v1/health", get(health::health))
        .route("/ready", get(health::ready))
        .route("/metrics", get(metrics::scrape))
        .route("/v1/whoami", get(identity::whoami))
        .route("/v1/checkouts", post(checkouts::create))
        .route("/v1/checkouts/{id}", get(checkouts::get))
        .route("/v1/orgs/{org_id}/checkouts", get(checkouts::list))
        .route("/v1/payment-links", post(payment_links::create))
        .route("/v1/orgs/{org_id}/payment-links", get(payment_links::list))
        .route(
            "/v1/orgs/{org_id}/products",
            post(products::create).get(products::list),
        )
        .route("/v1/orgs/{org_id}/subscriptions", get(subscriptions::list))
        .route("/v1/orgs/{org_id}/payments", get(payments::list))
        .route("/v1/orgs/{org_id}/receipts", get(receipts::list))
        .route("/v1/orgs/{org_id}/receipts/{id}", get(receipts::get))
        .route(
            "/v1/orgs/{org_id}/refunds",
            post(refunds::create).get(refunds::list),
        )
        .route(
            "/v1/orgs/{org_id}/refunds/{id}/resolve",
            post(refunds::resolve),
        )
        .route("/v1/pay/{token}", get(public_pay::get))
        .route("/v1/pay/{token}/start", post(public_pay::start))
        .route("/v1/pay/{token}/confirm", post(public_pay::confirm))
        .route("/v1/webhooks/test/{org_id}", post(webhooks::test_webhook))
        .route(
            "/v1/webhooks/stripe/{org_id}",
            post(webhooks::stripe_webhook),
        )
        .route("/v1/webhooks/chip/{org_id}", post(webhooks::chip_webhook))
        .route(
            "/v1/webhooks/billplz/{org_id}",
            post(webhooks::billplz_webhook),
        )
        .route(
            "/v1/webhooks/xendit/{org_id}",
            post(webhooks::xendit_webhook),
        )
        .route(
            "/v1/webhooks/razorpay/{org_id}",
            post(webhooks::razorpay_webhook),
        )
        .route(
            "/v1/webhooks/solana/{org_id}",
            post(webhooks::solana_webhook),
        )
        .route(
            "/v1/orgs/{org_id}/gateway",
            put(gateway::put).get(gateway::get),
        )
        .route("/v1/orgs/{org_id}/gateways", get(gateway::list))
        .route(
            "/v1/orgs/{org_id}/webhooks",
            put(org_webhooks::put).get(org_webhooks::get),
        )
        .route(
            "/v1/orgs/{org_id}/webhooks/rotate",
            post(org_webhooks::rotate),
        )
        .route(
            "/v1/orgs/{org_id}/webhooks/test",
            post(org_webhooks::test_ping),
        )
        .route("/v1/one/webhooks", post(one_webhooks::inbound))
        .route(
            "/v1/orgs/{org_id}/one-webhook",
            put(one_webhooks::put).get(one_webhooks::get),
        )
        .route("/v1/orgs/{org_id}/ready", get(org_ready::get))
        .layer(cors::layer(&state.cors_origins))
        .layer(middleware::from_fn(request_id::echo))
        .with_state(state)
}

pub fn testing_state(pool: PgPool, secret: &str) -> AppState {
    testing_state_with_limit(pool, secret, 20)
}

pub fn testing_state_with_limit(pool: PgPool, secret: &str, start_max: u32) -> AppState {
    AppState {
        pool,
        env: Env::Testing,
        checkout_base_url: "http://localhost:5179".into(),
        test_webhook_secret: secret.into(),
        one: OneClient::Fake(FakeOne::writer("t1")),
        whoami_cache: Arc::new(WhoamiCache::new()),
        limiter: Arc::new(Limiter::new(start_max)),
        wrap_key: workers::secret_box::SecretBox::testing_fallback_key(),
        stripe: FakeStripe::default(),
        chip: FakeChip::default(),
        billplz: FakeBillplz::default(),
        xendit: FakeXendit::default(),
        razorpay: FakeRazorpay::default(),
        solana: FakeSolanaRpc::default(),
        solana_cluster: "devnet".into(),
        public_base_url: "https://pay.example.test".into(),
        one_webhook_secret: String::new(),
        metrics_token: String::new(),
        cors_origins: cors::DEVELOPMENT_ORIGINS
            .iter()
            .map(|s| (*s).to_string())
            .collect(),
        live_http: None,
    }
}
