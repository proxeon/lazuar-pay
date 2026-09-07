//! TypeSpec `/v1` adapter over `storage::apply`. Test + Stripe + CHIP + Billplz + Xendit + Razorpay + Solana (033/10).

#![forbid(unsafe_code)]

pub mod boot;
pub mod checkouts;
pub mod errors;
pub mod gateway;
pub mod health;
pub mod identity;
pub mod json;
pub mod limiter;
pub mod public_pay;
pub mod stripe_http;
pub mod webhooks;

use std::sync::Arc;

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
}

pub fn router(state: AppState) -> Router {
    Router::new()
        .route("/health", get(health::health))
        .route("/v1/health", get(health::health))
        .route("/ready", get(health::ready))
        .route("/v1/whoami", get(identity::whoami))
        .route("/v1/checkouts", post(checkouts::create))
        .route("/v1/checkouts/{id}", get(checkouts::get))
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
    }
}
