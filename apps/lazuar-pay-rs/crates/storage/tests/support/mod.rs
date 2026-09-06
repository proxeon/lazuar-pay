//! Process-global Postgres 16 (sync testcontainers). Each tokio test owns its own pool
//! so we never reuse a PgPool after a test runtime shuts down.

use sqlx::postgres::PgPoolOptions;
use sqlx::{PgPool, Postgres, Transaction};
use std::sync::OnceLock;
use testcontainers::runners::SyncRunner;
use testcontainers::{Container, ImageExt};
use testcontainers_modules::postgres::Postgres as PgImage;
use uuid::Uuid;

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
                .expect("Pay storage tests require Docker/Testcontainers Postgres 16");
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
        .max_connections(4)
        .connect(db_url())
        .await
        .expect("connect postgres");
    // Idempotent (sqlx.schema_migrations). Cheap after the first test.
    storage::migrate(&pool).await.expect("migrate pay_rs");
    pool
}

pub fn is_unique_violation(err: &sqlx::Error) -> bool {
    matches!(err, sqlx::Error::Database(e) if e.code().as_deref() == Some("23505"))
}

pub fn is_check_violation(err: &sqlx::Error) -> bool {
    matches!(err, sqlx::Error::Database(e) if e.code().as_deref() == Some("23514"))
}

pub fn token(prefix: &str) -> String {
    format!("{prefix}-{}", Uuid::new_v4().simple())
}

pub async fn insert_payment(tx: &mut Transaction<'_, Postgres>, token: &str) -> sqlx::Result<Uuid> {
    let row: (Uuid,) = sqlx::query_as(
        r#"
        INSERT INTO pay_rs.payments (
            tenant_id, public_token, amount_minor, currency, exponent,
            status, expires_at, monitoring_until
        ) VALUES (
            't1', $1, 1000, 'MYR', 2,
            'open', now() + interval '30 minutes', now() + interval '30 minutes'
        )
        RETURNING id
        "#,
    )
    .bind(token)
    .fetch_one(&mut **tx)
    .await?;
    Ok(row.0)
}

pub async fn insert_link(tx: &mut Transaction<'_, Postgres>, token: &str) -> sqlx::Result<Uuid> {
    let row: (Uuid,) = sqlx::query_as(
        r#"
        INSERT INTO pay_rs.payment_links (
            tenant_id, public_token, rail, amount_minor, currency, exponent
        ) VALUES ('t1', $1, 'test', 1000, 'MYR', 2)
        RETURNING id
        "#,
    )
    .bind(token)
    .fetch_one(&mut **tx)
    .await?;
    Ok(row.0)
}

pub async fn insert_attempt(
    tx: &mut Transaction<'_, Postgres>,
    payment_id: Uuid,
    status: &str,
    session_id: Option<&str>,
) -> sqlx::Result<Uuid> {
    let row: (Uuid,) = sqlx::query_as(
        r#"
        INSERT INTO pay_rs.attempts (
            payment_id, tenant_id, rail, method,
            amount_minor, currency, exponent, status, session_id
        ) VALUES (
            $1, 't1', 'test', 'MYR-TEST',
            1000, 'MYR', 2, $2, $3
        )
        RETURNING id
        "#,
    )
    .bind(payment_id)
    .bind(status)
    .bind(session_id)
    .fetch_one(&mut **tx)
    .await?;
    Ok(row.0)
}

pub async fn insert_charge(
    tx: &mut Transaction<'_, Postgres>,
    payment_id: Uuid,
) -> sqlx::Result<Uuid> {
    let row: (Uuid,) = sqlx::query_as(
        r#"
        INSERT INTO pay_rs.charges (
            tenant_id, payment_id, amount_minor, currency, exponent, status
        ) VALUES ('t1', $1, 1000, 'MYR', 2, 'paid')
        RETURNING id
        "#,
    )
    .bind(payment_id)
    .fetch_one(&mut **tx)
    .await?;
    Ok(row.0)
}

pub async fn insert_refund(
    tx: &mut Transaction<'_, Postgres>,
    payment_id: Uuid,
    reason: &str,
    charge_id: Option<Uuid>,
    idem: Option<&str>,
) -> sqlx::Result<Uuid> {
    let row: (Uuid,) = sqlx::query_as(
        r#"
        INSERT INTO pay_rs.refunds (
            tenant_id, payment_id, charge_id,
            amount_minor, currency, exponent, status, rail, reason, idempotency_key
        ) VALUES (
            't1', $1, $2, 1000, 'MYR', 2, 'pending', 'test', $3, $4
        )
        RETURNING id
        "#,
    )
    .bind(payment_id)
    .bind(charge_id)
    .bind(reason)
    .bind(idem)
    .fetch_one(&mut **tx)
    .await?;
    Ok(row.0)
}
