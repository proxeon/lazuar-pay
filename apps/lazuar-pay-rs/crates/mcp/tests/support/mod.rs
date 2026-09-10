//! Shared Postgres 16 + axum bind for MCP HTTP tests.

#![allow(dead_code)]

use sqlx::postgres::PgPoolOptions;
use sqlx::PgPool;
use std::sync::OnceLock;
use testcontainers::runners::SyncRunner;
use testcontainers::{Container, ImageExt};
use testcontainers_modules::postgres::Postgres as PgImage;

struct Pg {
    _container: Container<PgImage>,
    url: String,
}

static PG: OnceLock<Pg> = OnceLock::new();

fn db_url() -> &'static str {
    &PG.get_or_init(|| {
        std::thread::spawn(|| {
            let container = PgImage::default()
                .with_tag("16-alpine")
                .start()
                .expect("Pay MCP tests require Docker/Testcontainers Postgres 16");
            let port = container.get_host_port_ipv4(5432).expect("postgres port");
            Pg {
                _container: container,
                url: format!(
                    "postgres://postgres:postgres@127.0.0.1:{port}/postgres?sslmode=disable"
                ),
            }
        })
        .join()
        .expect("postgres container thread")
    })
    .url
}

pub async fn pool() -> PgPool {
    let pool = PgPoolOptions::new()
        .max_connections(8)
        .connect(db_url())
        .await
        .expect("connect postgres");
    storage::migrate(&pool).await.expect("migrate pay_rs");
    pool
}

pub async fn serve() -> (String, tokio::task::JoinHandle<()>) {
    let pool = pool().await;
    let state = api::testing_state(pool, "test-secret");
    let listener = tokio::net::TcpListener::bind("127.0.0.1:0")
        .await
        .expect("bind");
    let addr = listener.local_addr().expect("addr");
    let handle = tokio::spawn(async move {
        axum::serve(listener, api::router(state))
            .await
            .expect("serve");
    });
    (format!("http://{addr}"), handle)
}
