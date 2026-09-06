//! TypeSpec `/v1` adapter over `storage::apply`. Test rail only (033/03).

#![forbid(unsafe_code)]

pub mod boot;
pub mod checkouts;
pub mod errors;
pub mod health;
pub mod identity;
pub mod json;
pub mod limiter;
pub mod public_pay;
pub mod webhooks;

use std::sync::Arc;

use axum::routing::{get, post};
use axum::Router;
use sqlx::PgPool;

use crate::boot::Env;
use crate::identity::{FakeOne, OneClient, WhoamiCache};
use crate::limiter::Limiter;

#[derive(Clone)]
pub struct AppState {
    pub pool: PgPool,
    pub env: Env,
    pub checkout_base_url: String,
    pub test_webhook_secret: String,
    pub one: OneClient,
    pub whoami_cache: Arc<WhoamiCache>,
    pub limiter: Arc<Limiter>,
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
    }
}
