mod support;

use domain::money::{Currency, Money};
use domain::proof::Proof;
use domain::rail::{HostedSession, RailId};
use domain::ProofId;
use domain::{PublicToken, TenantId};
use storage::{apply, ApplyCmd, ApplyOutcome, MintSpec};
use support::pool;
use time::{Duration, OffsetDateTime};
use uuid::Uuid;
use workers::expire;

fn myr10() -> Money {
    Money::from_quoted_str("10.00", Currency::MYR).unwrap()
}

#[tokio::test]
async fn expire_open_becomes_expired_without_charge() {
    let pool = pool().await;
    let now = OffsetDateTime::now_utc();
    let tenant = TenantId::new(format!("t-{}", Uuid::new_v4().simple()));
    let minted = apply(
        &pool,
        ApplyCmd::Mint(MintSpec {
            tenant_id: tenant,
            public_token: PublicToken::new(format!("p-{}", Uuid::new_v4().simple())),
            quoted: myr10(),
            expires_at: now - Duration::seconds(1),
            monitoring_until: now - Duration::seconds(1),
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
    expire::once(&pool).await.unwrap();
    let status: String = sqlx::query_scalar("SELECT status FROM pay_rs.payments WHERE id = $1")
        .bind(payment_id.as_uuid())
        .fetch_one(&pool)
        .await
        .unwrap();
    assert_eq!(status, "expired");
    let charges: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(payment_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(charges, 0);
}

#[tokio::test]
async fn expire_vs_paid_one_charge_xor_expired() {
    let pool = pool().await;
    let now = OffsetDateTime::now_utc();
    let tenant = TenantId::new(format!("t-{}", Uuid::new_v4().simple()));
    let minted = apply(
        &pool,
        ApplyCmd::Mint(MintSpec {
            tenant_id: tenant.clone(),
            public_token: PublicToken::new(format!("p-{}", Uuid::new_v4().simple())),
            quoted: myr10(),
            expires_at: now - Duration::seconds(1),
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
    let started = apply(
        &pool,
        ApplyCmd::StartAttempt {
            payment_id,
            rail: RailId::TEST,
        },
    )
    .await
    .unwrap();
    let ApplyOutcome::Started { attempt_id, .. } = started else {
        panic!("start");
    };
    apply(
        &pool,
        ApplyCmd::RecordSession {
            attempt_id,
            session: HostedSession {
                url: "https://example.test/s".into(),
                session_id: format!("cs-{}", Uuid::new_v4().simple()),
            },
        },
    )
    .await
    .unwrap();

    let a = pool.clone();
    let b = pool.clone();
    let t = tenant.clone();
    let paid = tokio::spawn(async move {
        apply(
            &a,
            ApplyCmd::InjectPaid {
                tenant_id: t,
                rail: RailId::TEST,
                proof_id: format!("evt-{}", Uuid::new_v4().simple()),
                payment_id,
                attempt_id,
                received: myr10(),
                proof: Proof::PspWebhook {
                    rail: RailId::TEST,
                    event_id: ProofId::new(format!("evt-{}", Uuid::new_v4().simple())),
                },
                now,
            },
        )
        .await
    });
    let exp = tokio::spawn(async move { expire::once(&b).await });
    let _ = paid.await.unwrap();
    let _ = exp.await.unwrap();
    let charges: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(payment_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    let status: String = sqlx::query_scalar("SELECT status FROM pay_rs.payments WHERE id = $1")
        .bind(payment_id.as_uuid())
        .fetch_one(&pool)
        .await
        .unwrap();
    assert!(charges <= 1);
    if charges == 1 {
        assert_eq!(status, "settled");
    } else {
        assert_eq!(status, "expired");
    }
}
