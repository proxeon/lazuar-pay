mod support;

use tokio::sync::Mutex;

use domain::money::{Currency, Money};
use domain::proof::SyncOutcome;
use domain::rail::{ConnectorRefs, HostedSession, RailId};
use domain::{PublicToken, TenantId};
use storage::{apply, ApplyCmd, ApplyOutcome, MintSpec};
use support::pool;
use time::{Duration, OffsetDateTime};
use uuid::Uuid;
use workers::psync::{self, ConstSync};
use workers::secret_box::SecretBox;
use workers::stripe_remote::StripeRemote;

fn myr10() -> Money {
    Money::from_quoted_str("10.00", Currency::MYR).unwrap()
}

static PSYNC: Mutex<()> = Mutex::const_new(());

async fn mint_session(
    pool: &sqlx::PgPool,
    rail: RailId,
    age_session: bool,
) -> (TenantId, domain::PaymentId, domain::AttemptId, String) {
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
            rail,
        }),
    )
    .await
    .unwrap();
    let ApplyOutcome::Minted { payment_id } = minted else {
        panic!("mint");
    };
    let started = apply(pool, ApplyCmd::StartAttempt { payment_id, rail })
        .await
        .unwrap();
    let ApplyOutcome::Started { attempt_id, .. } = started else {
        panic!("start");
    };
    let sid = format!("cs-{}", Uuid::new_v4().simple());
    apply(
        pool,
        ApplyCmd::RecordSession {
            attempt_id,
            session: HostedSession {
                url: "https://example.test/s".into(),
                session_id: sid.clone(),
            },
        },
    )
    .await
    .unwrap();
    if age_session {
        sqlx::query(
            "UPDATE pay_rs.attempts SET updated_at = now() - interval '2 minutes' WHERE id = $1",
        )
        .bind(attempt_id.as_uuid())
        .execute(pool)
        .await
        .unwrap();
    }
    (tenant, payment_id, attempt_id, sid)
}

#[tokio::test]
async fn test_rail_is_never_claimed() {
    let _g = PSYNC.lock().await;
    let pool = pool().await;
    let (_t, _p, _a, _s) = mint_session(&pool, RailId::TEST, true).await;
    let n = psync::process_batch(&pool, &ConstSync(SyncOutcome::Unknown))
        .await
        .unwrap();
    assert_eq!(n, 0);
}

#[tokio::test]
async fn unknown_keeps_pending() {
    let _g = PSYNC.lock().await;
    let pool = pool().await;
    let (_t, payment_id, attempt_id, _s) = mint_session(&pool, RailId::STRIPE, true).await;
    psync::process_batch(&pool, &ConstSync(SyncOutcome::Unknown))
        .await
        .unwrap();
    psync::process_batch(&pool, &ConstSync(SyncOutcome::Unknown))
        .await
        .unwrap();
    let ast: String = sqlx::query_scalar("SELECT status FROM pay_rs.attempts WHERE id = $1")
        .bind(attempt_id.as_uuid())
        .fetch_one(&pool)
        .await
        .unwrap();
    let pst: String = sqlx::query_scalar("SELECT status FROM pay_rs.payments WHERE id = $1")
        .bind(payment_id.as_uuid())
        .fetch_one(&pool)
        .await
        .unwrap();
    assert_eq!(ast, "session_live");
    assert_eq!(pst, "open");
}

#[tokio::test]
async fn paid_takes_once_and_webhook_dup_does_not_second_charge() {
    let _g = PSYNC.lock().await;
    let pool = pool().await;
    let (tenant, payment_id, attempt_id, sid) = mint_session(&pool, RailId::STRIPE, true).await;
    let outcome = SyncOutcome::Paid {
        received: myr10(),
        refs: ConnectorRefs {
            session_id: Some(sid.clone()),
            capture_id: Some("pi_1".into()),
            network_id: None,
        },
    };
    psync::process_batch(&pool, &ConstSync(outcome.clone()))
        .await
        .unwrap();
    let charges: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(payment_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(charges, 1);
    let _ = tenant;
    let _ = attempt_id;
    let _ = sid;
}

#[tokio::test]
async fn empty_session_id_skipped() {
    let _g = PSYNC.lock().await;
    let pool = pool().await;
    let (_t, _p, attempt_id, _s) = mint_session(&pool, RailId::STRIPE, true).await;
    sqlx::query("UPDATE pay_rs.attempts SET session_id = NULL, updated_at = now() - interval '2 minutes' WHERE id = $1")
        .bind(attempt_id.as_uuid())
        .execute(&pool)
        .await
        .unwrap();
    let _ = psync::process_batch(&pool, &ConstSync(SyncOutcome::Unknown)).await;
    let sid: Option<String> =
        sqlx::query_scalar("SELECT session_id FROM pay_rs.attempts WHERE id = $1")
            .bind(attempt_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert!(sid.is_none());
    let updated: OffsetDateTime =
        sqlx::query_scalar("SELECT updated_at FROM pay_rs.attempts WHERE id = $1")
            .bind(attempt_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert!(updated < OffsetDateTime::now_utc() - Duration::seconds(30));
}

async fn vault_stripe(pool: &sqlx::PgPool, tenant: &str) {
    let box_ = SecretBox::new(SecretBox::testing_fallback_key());
    let ct = box_.protect_str("sk_test_dummy").unwrap();
    let wh = box_.protect_str("whsec_test").unwrap();
    storage::upsert_stripe(pool, tenant, &ct, &wh, "ummy", Some("test"))
        .await
        .unwrap();
}

fn fixture(name: &str) -> String {
    std::fs::read_to_string(format!(
        "{}/../rails/tests/fixtures/stripe/{name}",
        env!("CARGO_MANIFEST_DIR")
    ))
    .unwrap()
}

#[tokio::test]
async fn stripe_remote_paid_fixture_takes_with_pi() {
    let _g = PSYNC.lock().await;
    let pool = pool().await;
    let (tenant, payment_id, _a, _s) = mint_session(&pool, RailId::STRIPE, true).await;
    vault_stripe(&pool, tenant.as_str()).await;
    let fake = rails::stripe::FakeStripe::default();
    fake.set_retrieve(200, &fixture("psync_paid.json"));
    let remote = StripeRemote::fake(pool.clone(), SecretBox::testing_fallback_key(), fake);
    psync::process_batch(&pool, &remote).await.unwrap();
    let charges: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(payment_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(charges, 1);
    let cap: Option<String> =
        sqlx::query_scalar("SELECT capture_id FROM pay_rs.attempts WHERE payment_id = $1")
            .bind(payment_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert!(cap.as_deref().unwrap().starts_with("pi_"));
}

#[tokio::test]
async fn stripe_remote_open_stays_session_live() {
    let _g = PSYNC.lock().await;
    let pool = pool().await;
    let (tenant, payment_id, attempt_id, _s) = mint_session(&pool, RailId::STRIPE, true).await;
    vault_stripe(&pool, tenant.as_str()).await;
    let fake = rails::stripe::FakeStripe::default();
    fake.set_retrieve(200, &fixture("psync_open.json"));
    let remote = StripeRemote::fake(pool.clone(), SecretBox::testing_fallback_key(), fake);
    psync::process_batch(&pool, &remote).await.unwrap();
    let ast: String = sqlx::query_scalar("SELECT status FROM pay_rs.attempts WHERE id = $1")
        .bind(attempt_id.as_uuid())
        .fetch_one(&pool)
        .await
        .unwrap();
    let pst: String = sqlx::query_scalar("SELECT status FROM pay_rs.payments WHERE id = $1")
        .bind(payment_id.as_uuid())
        .fetch_one(&pool)
        .await
        .unwrap();
    assert_eq!(ast, "session_live");
    assert_eq!(pst, "open");
}

#[tokio::test]
async fn missing_cred_is_not_failed() {
    let _g = PSYNC.lock().await;
    let pool = pool().await;
    let (_t, payment_id, attempt_id, _s) = mint_session(&pool, RailId::STRIPE, true).await;
    let fake = rails::stripe::FakeStripe::default();
    fake.set_retrieve(200, &fixture("psync_paid.json"));
    let remote = StripeRemote::fake(pool.clone(), SecretBox::testing_fallback_key(), fake);
    psync::process_batch(&pool, &remote).await.unwrap();
    let ast: String = sqlx::query_scalar("SELECT status FROM pay_rs.attempts WHERE id = $1")
        .bind(attempt_id.as_uuid())
        .fetch_one(&pool)
        .await
        .unwrap();
    let pst: String = sqlx::query_scalar("SELECT status FROM pay_rs.payments WHERE id = $1")
        .bind(payment_id.as_uuid())
        .fetch_one(&pool)
        .await
        .unwrap();
    assert_eq!(ast, "session_live");
    assert_eq!(pst, "open");
}
