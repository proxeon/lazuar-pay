mod support;

use domain::money::{Currency, Money};
use domain::{PublicToken, TenantId};
use storage::{apply, ApplyCmd, ApplyOutcome, MintSpec};
use support::pool;
use time::{Duration, OffsetDateTime};
use uuid::Uuid;
use workers::settler::{self, FakeSettled};

fn myr10() -> Money {
    Money::from_quoted_str("10.00", Currency::MYR).unwrap()
}

async fn pending_refund(pool: &sqlx::PgPool, rail: &str, age: Duration) -> (String, Uuid) {
    let now = OffsetDateTime::now_utc();
    let tenant = TenantId::new(format!("t-{}", Uuid::new_v4().simple()));
    let minted = apply(
        pool,
        ApplyCmd::Mint(MintSpec {
            tenant_id: tenant.clone(),
            public_token: PublicToken::new(format!("p-{}", Uuid::new_v4().simple())),
            quoted: myr10(),
            expires_at: now + Duration::minutes(30),
            monitoring_until: now + Duration::minutes(30),
            payment_link_id: None,
            slot_key: None,
            success_url: None,
            cancel_url: None,
        }),
    )
    .await
    .unwrap();
    let ApplyOutcome::Minted { payment_id } = minted else {
        panic!("mint");
    };
    sqlx::query(
        "INSERT INTO pay_rs.org_webhook_endpoints (tenant_id, url, secret_ciphertext)
         VALUES ($1, 'http://127.0.0.1:9/h', $2)",
    )
    .bind(tenant.as_str())
    .bind(&[1u8][..])
    .execute(pool)
    .await
    .unwrap();
    let id: Uuid = sqlx::query_scalar(
        r#"
        INSERT INTO pay_rs.refunds (
            tenant_id, payment_id, amount_minor, currency, exponent,
            status, rail, reason, created_at, next_attempt_at
        ) VALUES (
            $1, $2, 1000, 'MYR', 2, 'pending', $3, 'late_pay',
            now() - ($4::bigint * interval '1 second'), now()
        )
        RETURNING id
        "#,
    )
    .bind(tenant.as_str())
    .bind(payment_id.as_uuid())
    .bind(rail)
    .bind(age.whole_seconds())
    .fetch_one(pool)
    .await
    .unwrap();
    (tenant.as_str().to_string(), id)
}

#[tokio::test]
async fn chip_pending_is_never_claimed() {
    let pool = pool().await;
    let (_t, id) = pending_refund(&pool, "chip", Duration::minutes(1)).await;
    let n = settler::process_batch(&pool, &FakeSettled).await.unwrap();
    assert_eq!(n, 0);
    let st: String = sqlx::query_scalar("SELECT status FROM pay_rs.refunds WHERE id = $1")
        .bind(id)
        .fetch_one(&pool)
        .await
        .unwrap();
    assert_eq!(st, "pending");
}

#[tokio::test]
async fn stripe_late_pay_fake_settles_and_enqueues() {
    let pool = pool().await;
    let (tenant, id) = pending_refund(&pool, "stripe", Duration::minutes(1)).await;
    let n = settler::process_batch(&pool, &FakeSettled).await.unwrap();
    assert_eq!(n, 1);
    let st: String = sqlx::query_scalar("SELECT status FROM pay_rs.refunds WHERE id = $1")
        .bind(id)
        .fetch_one(&pool)
        .await
        .unwrap();
    assert_eq!(st, "succeeded");
    let deliveries: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.org_webhook_deliveries
          WHERE tenant_id = $1 AND event_type = 'refund.created'",
    )
    .bind(&tenant)
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(deliveries, 1);
}

#[tokio::test]
async fn older_than_24h_never_claimed() {
    let pool = pool().await;
    let (_t, id) = pending_refund(&pool, "stripe", Duration::hours(25)).await;
    let n = settler::process_batch(&pool, &FakeSettled).await.unwrap();
    assert_eq!(n, 0);
    let st: String = sqlx::query_scalar("SELECT status FROM pay_rs.refunds WHERE id = $1")
        .bind(id)
        .fetch_one(&pool)
        .await
        .unwrap();
    assert_eq!(st, "pending");
}
