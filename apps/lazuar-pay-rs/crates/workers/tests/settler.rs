mod support;

use domain::money::{Currency, Money};
use domain::proof::Proof;
use domain::rail::{ConnectorRefs, HostedSession, RailId};
use domain::{ProofId, PublicToken, TenantId};
use storage::{apply, ApplyCmd, ApplyOutcome, MintSpec};
use support::pool;
use time::{Duration, OffsetDateTime};
use tokio::sync::Mutex;
use uuid::Uuid;
use workers::secret_box::SecretBox;
use workers::settler::{self, FakeSettled};
use workers::stripe_remote::StripeRemote;

fn myr10() -> Money {
    Money::from_quoted_str("10.00", Currency::MYR).unwrap()
}

static SETTLER: Mutex<()> = Mutex::const_new(());

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
            rail: RailId::TEST,
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
    let _g = SETTLER.lock().await;
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
async fn billplz_pending_is_never_claimed() {
    let _g = SETTLER.lock().await;
    let pool = pool().await;
    let (_t, id) = pending_refund(&pool, "billplz", Duration::minutes(1)).await;
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
async fn xendit_pending_is_never_claimed() {
    let _g = SETTLER.lock().await;
    let pool = pool().await;
    let (_t, id) = pending_refund(&pool, "xendit", Duration::minutes(1)).await;
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
async fn razorpay_pending_is_never_claimed() {
    let _g = SETTLER.lock().await;
    let pool = pool().await;
    let (_t, id) = pending_refund(&pool, "razorpay", Duration::minutes(1)).await;
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
async fn solana_pending_is_never_claimed() {
    let _g = SETTLER.lock().await;
    let pool = pool().await;
    let (_t, id) = pending_refund(&pool, "solana", Duration::minutes(1)).await;
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
    let _g = SETTLER.lock().await;
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
    let _g = SETTLER.lock().await;
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

async fn paid_stripe_refund(pool: &sqlx::PgPool) -> (String, Uuid, rails::stripe::FakeStripe) {
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
            rail: RailId::STRIPE,
        }),
    )
    .await
    .unwrap();
    let ApplyOutcome::Minted { payment_id } = minted else {
        panic!("mint");
    };
    let started = apply(
        pool,
        ApplyCmd::StartAttempt {
            payment_id,
            rail: RailId::STRIPE,
        },
    )
    .await
    .unwrap();
    let ApplyOutcome::Started { attempt_id, .. } = started else {
        panic!("start");
    };
    apply(
        pool,
        ApplyCmd::RecordSession {
            attempt_id,
            session: HostedSession {
                url: "https://example.test/s".into(),
                session_id: "cs_test_1".into(),
            },
        },
    )
    .await
    .unwrap();
    apply(
        pool,
        ApplyCmd::InjectPaid {
            tenant_id: tenant.clone(),
            rail: RailId::STRIPE,
            proof_id: format!("evt-{}", Uuid::new_v4().simple()),
            payment_id,
            attempt_id,
            received: myr10(),
            proof: Proof::PspWebhook {
                rail: RailId::STRIPE,
                event_id: ProofId::new(format!("evt-{}", Uuid::new_v4().simple())),
            },
            now,
            refs: ConnectorRefs {
                session_id: Some("cs_test_1".into()),
                capture_id: Some("pi_abc".into()),
                network_id: None,
            },
        },
    )
    .await
    .unwrap();
    sqlx::query(
        "INSERT INTO pay_rs.org_webhook_endpoints (tenant_id, url, secret_ciphertext)
         VALUES ($1, 'http://127.0.0.1:9/h', $2)",
    )
    .bind(tenant.as_str())
    .bind(&[1u8][..])
    .execute(pool)
    .await
    .unwrap();
    let box_ = SecretBox::new(SecretBox::testing_fallback_key());
    let ct = box_.protect_str("sk_test_dummy").unwrap();
    let wh = box_.protect_str("whsec_test").unwrap();
    storage::upsert_stripe(pool, tenant.as_str(), &ct, &wh, "ummy", Some("test"))
        .await
        .unwrap();
    let id: Uuid = sqlx::query_scalar(
        r#"
        INSERT INTO pay_rs.refunds (
            tenant_id, payment_id, amount_minor, currency, exponent,
            status, rail, reason, created_at, next_attempt_at
        ) VALUES (
            $1, $2, 1000, 'MYR', 2, 'pending', 'stripe', 'late_pay',
            now() - interval '1 minute', now()
        )
        RETURNING id
        "#,
    )
    .bind(tenant.as_str())
    .bind(payment_id.as_uuid())
    .fetch_one(pool)
    .await
    .unwrap();
    (
        tenant.as_str().to_string(),
        id,
        rails::stripe::FakeStripe::default(),
    )
}

#[tokio::test]
async fn stripe_refund_uses_pi_not_cs() {
    let _g = SETTLER.lock().await;
    let pool = pool().await;
    let (_tenant, id, fake) = paid_stripe_refund(&pool).await;
    let remote = StripeRemote::fake(
        pool.clone(),
        SecretBox::testing_fallback_key(),
        fake.clone(),
    );
    let n = settler::process_batch(&pool, &remote).await.unwrap();
    assert_eq!(n, 1);
    assert_eq!(
        fake.last_refund_pi.lock().expect("lock").clone().unwrap(),
        "pi_abc"
    );
    let st: String = sqlx::query_scalar("SELECT status FROM pay_rs.refunds WHERE id = $1")
        .bind(id)
        .fetch_one(&pool)
        .await
        .unwrap();
    assert_eq!(st, "succeeded");
}

#[tokio::test]
async fn stripe_refund_5xx_is_unknown() {
    let _g = SETTLER.lock().await;
    let pool = pool().await;
    let (_tenant, id, fake) = paid_stripe_refund(&pool).await;
    fake.set_refund(500, r#"{"error":{"type":"api_error"}}"#);
    let remote = StripeRemote::fake(pool.clone(), SecretBox::testing_fallback_key(), fake);
    let n = settler::process_batch(&pool, &remote).await.unwrap();
    assert_eq!(n, 1);
    let st: String = sqlx::query_scalar("SELECT status FROM pay_rs.refunds WHERE id = $1")
        .bind(id)
        .fetch_one(&pool)
        .await
        .unwrap();
    assert_eq!(st, "pending");
}
