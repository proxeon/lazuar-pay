//! G4 Postgres races (033/02 §7.3). Two connections; end state is the spec.

mod support;

use domain::money::{Currency, Money};
use domain::proof::Proof;
use domain::rail::{HostedSession, RailId};
use domain::ProofId;
use domain::{AttemptId, PaymentId, PaymentLinkId, PublicToken, TenantId};
use sqlx::PgPool;
use storage::{apply, ApplyCmd, ApplyError, ApplyOutcome, MintSpec};
use support::{insert_link_max, pool, token};
use time::{Duration, OffsetDateTime};

fn myr10() -> Money {
    Money::from_quoted_str("10.00", Currency::MYR).unwrap()
}

fn tenant() -> TenantId {
    TenantId::new(token("t"))
}

async fn mint(
    pool: &PgPool,
    tenant: &TenantId,
    expires: OffsetDateTime,
    link: Option<(PaymentLinkId, Option<String>)>,
) -> PaymentId {
    let (payment_link_id, slot_key) = match link {
        Some((id, slot)) => (Some(id), slot),
        None => (None, None),
    };
    let out = apply(
        pool,
        ApplyCmd::Mint(MintSpec {
            tenant_id: tenant.clone(),
            public_token: PublicToken::new(token("p")),
            quoted: myr10(),
            expires_at: expires,
            monitoring_until: expires,
            payment_link_id,
            slot_key,
            success_url: None,
            cancel_url: None,
            rail: RailId::TEST,
        }),
    )
    .await
    .unwrap();
    match out {
        ApplyOutcome::Minted { payment_id } => payment_id,
        _ => panic!("mint"),
    }
}

async fn start_session(pool: &PgPool, payment_id: PaymentId) -> AttemptId {
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
        panic!("start");
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
    attempt_id
}

fn paid(
    tenant: &TenantId,
    payment_id: PaymentId,
    attempt_id: AttemptId,
    proof_id: String,
    now: OffsetDateTime,
) -> ApplyCmd {
    ApplyCmd::InjectPaid {
        tenant_id: tenant.clone(),
        rail: RailId::TEST,
        proof_id: proof_id.clone(),
        payment_id,
        attempt_id,
        received: myr10(),
        proof: Proof::PspWebhook {
            rail: RailId::TEST,
            event_id: ProofId::new(proof_id),
        },
        now,
        refs: Default::default(),
    }
}

#[tokio::test]
async fn issue_002_paid_vs_expire_never_settled_then_expired() {
    let pool = pool().await;
    let tenant = tenant();
    let now = OffsetDateTime::now_utc();
    let payment_id = mint(&pool, &tenant, now - Duration::seconds(1), None).await;
    let attempt_id = start_session(&pool, payment_id).await;
    let a = pool.clone();
    let b = pool.clone();
    let t = tenant.clone();
    let (paid_res, exp_res) = tokio::join!(
        apply(&a, paid(&t, payment_id, attempt_id, token("evt"), now),),
        apply(&b, ApplyCmd::ExpireClock { payment_id, now },),
    );
    assert!(paid_res.is_ok() || matches!(paid_res, Err(ApplyError::Conflict)));
    assert!(exp_res.is_ok() || matches!(exp_res, Err(ApplyError::Conflict)));
    let row: (String, i64) = sqlx::query_as(
        r#"
        SELECT p.status, (SELECT count(*)::bigint FROM pay_rs.charges c WHERE c.payment_id = p.id)
          FROM pay_rs.payments p WHERE p.id = $1
        "#,
    )
    .bind(payment_id.as_uuid())
    .fetch_one(&pool)
    .await
    .unwrap();
    assert!(
        !(row.0 == "expired" && row.1 > 0),
        "settled then expired: {row:?}"
    );
}

#[tokio::test]
async fn issue_007_concurrent_record_session_one_session_id() {
    let pool = pool().await;
    let tenant = tenant();
    let now = OffsetDateTime::now_utc();
    let payment_id = mint(&pool, &tenant, now + Duration::minutes(30), None).await;
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
    let a = pool.clone();
    let b = pool.clone();
    let (r1, r2) = tokio::join!(
        apply(
            &a,
            ApplyCmd::RecordSession {
                attempt_id,
                session: HostedSession {
                    url: "https://example.test/a".into(),
                    session_id: "cs_a".into(),
                },
            },
        ),
        apply(
            &b,
            ApplyCmd::RecordSession {
                attempt_id,
                session: HostedSession {
                    url: "https://example.test/b".into(),
                    session_id: "cs_b".into(),
                },
            },
        ),
    );
    let mut urls = vec![];
    for r in [r1, r2] {
        match r.unwrap() {
            ApplyOutcome::Applied { .. } => {}
            ApplyOutcome::SessionResume { session_id, url } => {
                urls.push((session_id, url));
            }
            other => panic!("unexpected {other:?}"),
        }
    }
    let sid: String = sqlx::query_scalar("SELECT session_id FROM pay_rs.attempts WHERE id = $1")
        .bind(attempt_id.as_uuid())
        .fetch_one(&pool)
        .await
        .unwrap();
    assert!(sid == "cs_a" || sid == "cs_b");
    let n: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.attempts WHERE payment_id = $1 AND session_id IS NOT NULL",
    )
    .bind(payment_id.as_uuid())
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(n, 1);
    if let Some((resume_id, _)) = urls.first() {
        assert_eq!(resume_id, &sid);
    }
}

#[tokio::test]
async fn issue_011_same_slot_second_mint_conflicts() {
    let pool = pool().await;
    let tenant = tenant();
    let now = OffsetDateTime::now_utc();
    let link = insert_link_max(&pool, tenant.as_str(), 5).await.unwrap();
    let link_id = PaymentLinkId::from_uuid(link);
    let spec = || MintSpec {
        tenant_id: tenant.clone(),
        public_token: PublicToken::new(token("p")),
        quoted: myr10(),
        expires_at: now + Duration::minutes(30),
        monitoring_until: now + Duration::minutes(30),
        payment_link_id: Some(link_id),
        slot_key: Some("slot-1".into()),
        success_url: None,
        cancel_url: None,
        rail: RailId::TEST,
    };
    let a = pool.clone();
    let b = pool.clone();
    let (r1, r2) = tokio::join!(
        apply(&a, ApplyCmd::Mint(spec())),
        apply(&b, ApplyCmd::Mint(spec())),
    );
    let oks = [r1, r2].into_iter().filter(|r| r.is_ok()).count();
    let errs = 2 - oks;
    assert_eq!(oks, 1);
    assert_eq!(errs, 1);
}

#[tokio::test]
async fn issue_008_occupancy_full_returns_over_capacity() {
    let pool = pool().await;
    let tenant = tenant();
    let now = OffsetDateTime::now_utc();
    let link = insert_link_max(&pool, tenant.as_str(), 1).await.unwrap();
    let link_id = PaymentLinkId::from_uuid(link);
    let a_id = mint(
        &pool,
        &tenant,
        now + Duration::minutes(30),
        Some((link_id, None)),
    )
    .await;
    let a_att = start_session(&pool, a_id).await;
    apply(&pool, paid(&tenant, a_id, a_att, token("evt-a"), now))
        .await
        .unwrap();
    let b_id = mint(
        &pool,
        &tenant,
        now + Duration::minutes(30),
        Some((link_id, None)),
    )
    .await;
    let b_att = start_session(&pool, b_id).await;
    apply(&pool, paid(&tenant, b_id, b_att, token("evt-b"), now))
        .await
        .unwrap();
    let a_status: String = sqlx::query_scalar("SELECT status FROM pay_rs.payments WHERE id = $1")
        .bind(a_id.as_uuid())
        .fetch_one(&pool)
        .await
        .unwrap();
    let b: (String, String, i64) = sqlx::query_as(
        r#"
        SELECT p.status, p.exception,
               (SELECT count(*)::bigint FROM pay_rs.refunds r WHERE r.payment_id = p.id AND r.reason = 'over_capacity')
          FROM pay_rs.payments p WHERE p.id = $1
        "#,
    )
    .bind(b_id.as_uuid())
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(a_status, "settled");
    assert_eq!(b.0, "expired");
    assert_eq!(b.1, "over_capacity");
    assert_eq!(b.2, 1);
    let charges: i64 = sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges")
        .bind(a_id.as_uuid())
        .fetch_one(&pool)
        .await
        .unwrap();
    let _ = charges;
    let a_charges: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(a_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    let b_charges: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(b_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(a_charges, 1);
    assert_eq!(b_charges, 0);
}

#[tokio::test]
async fn issue_009_second_late_proof_no_second_refund() {
    let pool = pool().await;
    let tenant = tenant();
    let now = OffsetDateTime::now_utc();
    let payment_id = mint(&pool, &tenant, now - Duration::minutes(1), None).await;
    let attempt_id = start_session(&pool, payment_id).await;
    apply(&pool, ApplyCmd::ExpireClock { payment_id, now })
        .await
        .unwrap();
    apply(
        &pool,
        paid(&tenant, payment_id, attempt_id, token("evt-1"), now),
    )
    .await
    .unwrap();
    let second = apply(
        &pool,
        paid(&tenant, payment_id, attempt_id, token("evt-2"), now),
    )
    .await
    .unwrap();
    assert!(matches!(
        second,
        ApplyOutcome::Applied { .. } | ApplyOutcome::Duplicate
    ));
    let n: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.refunds WHERE payment_id = $1")
            .bind(payment_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(n, 1);
}

#[tokio::test]
async fn issue_010_concurrent_remainder_refunds_one_conflict() {
    let pool = pool().await;
    let tenant = tenant();
    let now = OffsetDateTime::now_utc();
    let payment_id = mint(&pool, &tenant, now + Duration::minutes(30), None).await;
    let attempt_id = start_session(&pool, payment_id).await;
    apply(
        &pool,
        paid(&tenant, payment_id, attempt_id, token("evt"), now),
    )
    .await
    .unwrap();
    let a = pool.clone();
    let b = pool.clone();
    let (r1, r2) = tokio::join!(
        apply(
            &a,
            ApplyCmd::MerchantRefund {
                payment_id,
                amount: myr10(),
                idempotency_key: token("idem-a"),
                request_hash: "h1".into(),
                now,
            },
        ),
        apply(
            &b,
            ApplyCmd::MerchantRefund {
                payment_id,
                amount: myr10(),
                idempotency_key: token("idem-b"),
                request_hash: "h2".into(),
                now,
            },
        ),
    );
    let ok = [&r1, &r2].iter().filter(|r| r.is_ok()).count();
    let already = [&r1, &r2]
        .iter()
        .filter(|r| matches!(r, Err(ApplyError::AlreadyRefunded)))
        .count();
    assert_eq!(ok, 1);
    assert_eq!(already, 1);
    let n: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.refunds WHERE payment_id = $1 AND status = 'pending'",
    )
    .bind(payment_id.as_uuid())
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(n, 1);
}

#[tokio::test]
async fn issue_012_idempotency_replay_and_mismatch() {
    let pool = pool().await;
    let tenant = tenant();
    let now = OffsetDateTime::now_utc();
    let payment_id = mint(&pool, &tenant, now + Duration::minutes(30), None).await;
    let attempt_id = start_session(&pool, payment_id).await;
    apply(
        &pool,
        paid(&tenant, payment_id, attempt_id, token("evt"), now),
    )
    .await
    .unwrap();
    let key = token("idem");
    let first = apply(
        &pool,
        ApplyCmd::MerchantRefund {
            payment_id,
            amount: myr10(),
            idempotency_key: key.clone(),
            request_hash: "body-a".into(),
            now,
        },
    )
    .await
    .unwrap();
    let replay = apply(
        &pool,
        ApplyCmd::MerchantRefund {
            payment_id,
            amount: myr10(),
            idempotency_key: key.clone(),
            request_hash: "body-a".into(),
            now,
        },
    )
    .await
    .unwrap();
    assert!(matches!(replay, ApplyOutcome::RefundReplay { .. }));
    let mismatch = apply(
        &pool,
        ApplyCmd::MerchantRefund {
            payment_id,
            amount: myr10(),
            idempotency_key: key,
            request_hash: "body-b".into(),
            now,
        },
    )
    .await
    .unwrap_err();
    assert!(matches!(mismatch, ApplyError::IdempotencyMismatch));
    let n: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.refunds WHERE payment_id = $1")
            .bind(payment_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(n, 1);
    let _ = first;
}

#[tokio::test]
async fn e6_late_after_ttl_does_not_fill_freed_slot() {
    let pool = pool().await;
    let tenant = tenant();
    let now = OffsetDateTime::now_utc();
    let link = insert_link_max(&pool, tenant.as_str(), 1).await.unwrap();
    let link_id = PaymentLinkId::from_uuid(link);
    let a_id = mint(
        &pool,
        &tenant,
        now + Duration::minutes(30),
        Some((link_id, None)),
    )
    .await;
    let a_att = start_session(&pool, a_id).await;
    apply(&pool, paid(&tenant, a_id, a_att, token("evt-a"), now))
        .await
        .unwrap();
    let b_id = mint(
        &pool,
        &tenant,
        now - Duration::minutes(1),
        Some((link_id, None)),
    )
    .await;
    let b_att = start_session(&pool, b_id).await;
    apply(
        &pool,
        ApplyCmd::ExpireClock {
            payment_id: b_id,
            now,
        },
    )
    .await
    .unwrap();
    apply(&pool, paid(&tenant, b_id, b_att, token("evt-b"), now))
        .await
        .unwrap();
    let b: (String, String, i64) = sqlx::query_as(
        r#"
        SELECT status, exception,
               (SELECT count(*)::bigint FROM pay_rs.refunds r
                 WHERE r.payment_id = p.id AND r.reason = 'late_pay')
          FROM pay_rs.payments p WHERE id = $1
        "#,
    )
    .bind(b_id.as_uuid())
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(b.0, "expired");
    assert_eq!(b.1, "late");
    assert_eq!(b.2, 1);
    let b_charges: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.charges WHERE payment_id = $1")
            .bind(b_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(b_charges, 0);
}
