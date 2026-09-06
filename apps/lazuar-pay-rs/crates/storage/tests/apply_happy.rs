//! Happy path and gates (033/02 §7.4). No axum.

mod support;

use domain::money::{Currency, Money};
use domain::proof::Proof;
use domain::rail::{HostedSession, RailId};
use domain::ProofId;
use domain::{PaymentId, PublicToken, TenantId, TerminalReason};
use sqlx::PgPool;
use storage::{apply, ApplyCmd, ApplyError, ApplyOutcome, MintSpec};
use support::{pool, token};
use time::{Duration, OffsetDateTime};

fn myr10() -> Money {
    Money::from_quoted_str("10.00", Currency::MYR).unwrap()
}

fn tenant() -> TenantId {
    TenantId::new(token("t"))
}

async fn mint_and_session(
    pool: &PgPool,
    tenant: &TenantId,
    expires: OffsetDateTime,
) -> (PaymentId, domain::AttemptId) {
    let now = OffsetDateTime::now_utc();
    let minted = apply(
        pool,
        ApplyCmd::Mint(MintSpec {
            tenant_id: tenant.clone(),
            public_token: PublicToken::new(token("p")),
            quoted: myr10(),
            expires_at: expires,
            monitoring_until: expires,
            payment_link_id: None,
            slot_key: None,
            success_url: None,
            cancel_url: None,
        }),
    )
    .await
    .unwrap();
    let ApplyOutcome::Minted { payment_id } = minted else {
        panic!("expected minted");
    };
    let started = apply(
        pool,
        ApplyCmd::StartAttempt {
            payment_id,
            rail: RailId::TEST,
        },
    )
    .await
    .unwrap();
    let ApplyOutcome::Started { attempt_id, .. } = started else {
        panic!("expected started");
    };
    apply(
        pool,
        ApplyCmd::RecordSession {
            attempt_id,
            session: HostedSession {
                url: "https://example.test/s".into(),
                session_id: token("cs"),
            },
        },
    )
    .await
    .unwrap();
    let _ = now;
    (payment_id, attempt_id)
}

fn paid_cmd(
    tenant: &TenantId,
    payment_id: PaymentId,
    attempt_id: domain::AttemptId,
    received: Money,
    proof_id: String,
    now: OffsetDateTime,
) -> ApplyCmd {
    ApplyCmd::InjectPaid {
        tenant_id: tenant.clone(),
        rail: RailId::TEST,
        proof_id: proof_id.clone(),
        payment_id,
        attempt_id,
        received,
        proof: Proof::PspWebhook {
            rail: RailId::TEST,
            event_id: ProofId::new(proof_id),
        },
        now,
    }
}

#[tokio::test]
async fn e1_inject_paid_takes_charge_and_journal() {
    let pool = pool().await;
    let tenant = tenant();
    let now = OffsetDateTime::now_utc();
    let (payment_id, attempt_id) =
        mint_and_session(&pool, &tenant, now + Duration::minutes(30)).await;
    apply(
        &pool,
        paid_cmd(&tenant, payment_id, attempt_id, myr10(), token("evt"), now),
    )
    .await
    .unwrap();
    let status: String = sqlx::query_scalar("SELECT status FROM pay_rs.payments WHERE id = $1")
        .bind(payment_id.as_uuid())
        .fetch_one(&pool)
        .await
        .unwrap();
    assert_eq!(status, "settled");
    let charges: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(payment_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(charges, 1);
    let lines: i64 = sqlx::query_scalar(
        r#"
        SELECT count(*)::bigint FROM pay_rs.journal_lines l
          JOIN pay_rs.journal_entries e ON e.id = l.entry_id
         WHERE e.payment_id = $1
        "#,
    )
    .bind(payment_id.as_uuid())
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(lines, 2);
    let deliveries: i64 = sqlx::query_scalar(
        r#"
        SELECT count(*)::bigint FROM pay_rs.org_webhook_deliveries
         WHERE tenant_id = $1 AND event_type = 'payment.completed'
        "#,
    )
    .bind(tenant.as_str())
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(deliveries, 1);
}

#[tokio::test]
async fn e2_amount_mismatch_no_settlement() {
    let pool = pool().await;
    let tenant = tenant();
    let now = OffsetDateTime::now_utc();
    let (payment_id, attempt_id) =
        mint_and_session(&pool, &tenant, now + Duration::minutes(30)).await;
    let nine = Money::from_quoted_str("9.00", Currency::MYR).unwrap();
    let err = apply(
        &pool,
        paid_cmd(&tenant, payment_id, attempt_id, nine, token("evt"), now),
    )
    .await
    .unwrap_err();
    assert!(matches!(err, ApplyError::Integrity));
    let settlements: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.settlements WHERE payment_id = $1")
            .bind(payment_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(settlements, 0);
    let status: String = sqlx::query_scalar("SELECT status FROM pay_rs.payments WHERE id = $1")
        .bind(payment_id.as_uuid())
        .fetch_one(&pool)
        .await
        .unwrap();
    assert_eq!(status, "open");
    let inbound: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.inbound_events WHERE tenant_id = $1",
    )
    .bind(tenant.as_str())
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(inbound, 0);
}

#[tokio::test]
async fn pause_blocks_take_rolls_back_inbound() {
    let pool = pool().await;
    let tenant = tenant();
    sqlx::query("INSERT INTO pay_rs.org_settings (tenant_id, charges_paused) VALUES ($1, true)")
        .bind(tenant.as_str())
        .execute(&pool)
        .await
        .unwrap();
    let now = OffsetDateTime::now_utc();
    let (payment_id, attempt_id) =
        mint_and_session(&pool, &tenant, now + Duration::minutes(30)).await;
    let err = apply(
        &pool,
        paid_cmd(&tenant, payment_id, attempt_id, myr10(), token("evt"), now),
    )
    .await
    .unwrap_err();
    assert!(matches!(err, ApplyError::Paused));
    let charges: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(payment_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(charges, 0);
    let inbound: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.inbound_events WHERE tenant_id = $1",
    )
    .bind(tenant.as_str())
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(inbound, 0);
}

#[tokio::test]
async fn pause_does_not_block_late_return() {
    let pool = pool().await;
    let tenant = tenant();
    let now = OffsetDateTime::now_utc();
    let (payment_id, attempt_id) =
        mint_and_session(&pool, &tenant, now - Duration::minutes(1)).await;
    apply(&pool, ApplyCmd::ExpireClock { payment_id, now })
        .await
        .unwrap();
    sqlx::query("INSERT INTO pay_rs.org_settings (tenant_id, charges_paused) VALUES ($1, true)")
        .bind(tenant.as_str())
        .execute(&pool)
        .await
        .unwrap();
    apply(
        &pool,
        paid_cmd(&tenant, payment_id, attempt_id, myr10(), token("evt"), now),
    )
    .await
    .unwrap();
    let refunds: (i64, String) = sqlx::query_as(
        r#"
        SELECT count(*)::bigint, min(reason)
          FROM pay_rs.refunds WHERE payment_id = $1
        "#,
    )
    .bind(payment_id.as_uuid())
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(refunds.0, 1);
    assert_eq!(refunds.1, "late_pay");
}

#[tokio::test]
async fn inbound_duplicate_does_not_take_twice() {
    let pool = pool().await;
    let tenant = tenant();
    let now = OffsetDateTime::now_utc();
    let (payment_id, attempt_id) =
        mint_and_session(&pool, &tenant, now + Duration::minutes(30)).await;
    let proof = token("evt");
    apply(
        &pool,
        paid_cmd(&tenant, payment_id, attempt_id, myr10(), proof.clone(), now),
    )
    .await
    .unwrap();
    let again = apply(
        &pool,
        paid_cmd(&tenant, payment_id, attempt_id, myr10(), proof, now),
    )
    .await
    .unwrap();
    assert!(matches!(again, ApplyOutcome::Duplicate));
    let charges: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(payment_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(charges, 1);
}

#[tokio::test]
async fn clock_does_not_expire_failed() {
    let pool = pool().await;
    let tenant = tenant();
    let now = OffsetDateTime::now_utc();
    let (payment_id, attempt_id) =
        mint_and_session(&pool, &tenant, now + Duration::minutes(30)).await;
    apply(
        &pool,
        ApplyCmd::InjectFailed {
            tenant_id: tenant,
            rail: RailId::TEST,
            proof_id: token("fail"),
            payment_id,
            attempt_id,
            reason: TerminalReason::PspFailed,
            now,
        },
    )
    .await
    .unwrap();
    apply(
        &pool,
        ApplyCmd::ExpireClock {
            payment_id,
            now: now + Duration::hours(1),
        },
    )
    .await
    .unwrap();
    let status: String = sqlx::query_scalar("SELECT status FROM pay_rs.payments WHERE id = $1")
        .bind(payment_id.as_uuid())
        .fetch_one(&pool)
        .await
        .unwrap();
    assert_eq!(status, "failed");
}
