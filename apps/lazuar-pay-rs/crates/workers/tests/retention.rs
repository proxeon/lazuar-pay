mod support;

use domain::money::{Currency, Money};
use domain::{PublicToken, TenantId};
use storage::{apply, ApplyCmd, ApplyOutcome, MintSpec, RetentionCfg};
use support::pool;
use time::{Duration, OffsetDateTime};
use uuid::Uuid;
use workers::retention;

fn myr10() -> Money {
    Money::from_quoted_str("10.00", Currency::MYR).unwrap()
}

#[tokio::test]
async fn sweep_deletes_old_inbound_keeps_charges() {
    let pool = pool().await;
    let now = OffsetDateTime::now_utc();
    let tenant = TenantId::new(format!("t-{}", Uuid::new_v4().simple()));
    let minted = apply(
        &pool,
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
        "INSERT INTO pay_rs.charges (tenant_id, payment_id, amount_minor, currency, exponent, status)
         VALUES ($1, $2, 1000, 'MYR', 2, 'paid')",
    )
    .bind(tenant.as_str())
    .bind(payment_id.as_uuid())
    .execute(&pool)
    .await
    .unwrap();
    sqlx::query(
        "INSERT INTO pay_rs.inbound_events (tenant_id, rail, proof_id, received_at)
         VALUES ($1, 'test', $2, now() - interval '100 days')",
    )
    .bind(tenant.as_str())
    .bind(format!("old-{}", Uuid::new_v4().simple()))
    .execute(&pool)
    .await
    .unwrap();

    retention::sweep(&pool, RetentionCfg::default())
        .await
        .unwrap();

    let inbound: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.inbound_events WHERE tenant_id = $1",
    )
    .bind(tenant.as_str())
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(inbound, 0);
    let charges: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(payment_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(charges, 1);
}

#[tokio::test]
async fn zero_days_disables_table() {
    let pool = pool().await;
    let tenant = format!("t-{}", Uuid::new_v4().simple());
    sqlx::query(
        "INSERT INTO pay_rs.inbound_events (tenant_id, rail, proof_id, received_at)
         VALUES ($1, 'test', $2, now() - interval '100 days')",
    )
    .bind(&tenant)
    .bind(format!("old-{}", Uuid::new_v4().simple()))
    .execute(&pool)
    .await
    .unwrap();
    let cfg = RetentionCfg {
        inbound_days: 0,
        ..RetentionCfg::default()
    };
    retention::sweep(&pool, cfg).await.unwrap();
    let inbound: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.inbound_events WHERE tenant_id = $1",
    )
    .bind(&tenant)
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(inbound, 1);
}
