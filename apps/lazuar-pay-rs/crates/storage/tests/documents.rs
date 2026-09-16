//! Document sequence allocation (C# DocumentNumbers). Docker Postgres 16.

mod support;

use domain::money::{Currency, Money};
use domain::proof::Proof;
use domain::rail::{HostedSession, RailId};
use domain::{PaymentId, ProofId, PublicToken, TenantId};
use sqlx::PgPool;
use storage::{
    allocate_document, apply, malaysia_year, ApplyCmd, ApplyOutcome, DocSeries, MintSpec,
};
use support::{assert_issued_number, pool, token};
use time::format_description::well_known::Rfc3339;
use time::{Duration, OffsetDateTime};

fn myr10() -> Money {
    Money::from_quoted_str("10.00", Currency::MYR).unwrap()
}

fn parse(s: &str) -> OffsetDateTime {
    OffsetDateTime::parse(s, &Rfc3339).expect(s)
}

async fn mint_take(pool: &PgPool, tenant: &TenantId) -> PaymentId {
    let now = OffsetDateTime::now_utc();
    let minted = apply(
        pool,
        ApplyCmd::Mint(MintSpec {
            tenant_id: tenant.clone(),
            public_token: PublicToken::new(token("p")),
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
        panic!("minted");
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
        panic!("started");
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
    apply(
        pool,
        ApplyCmd::InjectPaid {
            tenant_id: tenant.clone(),
            rail: RailId::TEST,
            proof_id: token("evt"),
            payment_id,
            attempt_id,
            received: myr10(),
            proof: Proof::PspWebhook {
                rail: RailId::TEST,
                event_id: ProofId::new(token("evt")),
            },
            now,
            refs: Default::default(),
        },
    )
    .await
    .unwrap();
    payment_id
}

#[tokio::test]
async fn first_allocate_is_00001_for_myt_year() {
    let pool = pool().await;
    let tenant = token("t");
    let now = OffsetDateTime::now_utc();
    let mut tx = pool.begin().await.unwrap();
    let number = allocate_document(&mut tx, &tenant, DocSeries::Receipt, now)
        .await
        .unwrap();
    tx.commit().await.unwrap();
    let year = malaysia_year(now);
    assert_eq!(number, format!("RCPT-{year}-00001"));
}

#[tokio::test]
async fn allocate_increments_per_tenant_series_year() {
    let pool = pool().await;
    let tenant = token("t");
    let now = OffsetDateTime::now_utc();
    let mut tx = pool.begin().await.unwrap();
    let a = allocate_document(&mut tx, &tenant, DocSeries::Receipt, now)
        .await
        .unwrap();
    let b = allocate_document(&mut tx, &tenant, DocSeries::Receipt, now)
        .await
        .unwrap();
    let refund = allocate_document(&mut tx, &tenant, DocSeries::Refund, now)
        .await
        .unwrap();
    tx.commit().await.unwrap();
    let year = malaysia_year(now);
    assert_eq!(a, format!("RCPT-{year}-00001"));
    assert_eq!(b, format!("RCPT-{year}-00002"));
    // REF is a separate sequence, not the next RCPT n.
    assert_eq!(refund, format!("REF-{year}-00001"));
}

#[tokio::test]
async fn tenants_do_not_share_sequences() {
    let pool = pool().await;
    let now = OffsetDateTime::now_utc();
    let mut tx = pool.begin().await.unwrap();
    let a = allocate_document(&mut tx, &token("a"), DocSeries::Receipt, now)
        .await
        .unwrap();
    let b = allocate_document(&mut tx, &token("b"), DocSeries::Receipt, now)
        .await
        .unwrap();
    tx.commit().await.unwrap();
    let year = malaysia_year(now);
    assert_eq!(a, format!("RCPT-{year}-00001"));
    assert_eq!(b, format!("RCPT-{year}-00001"));
}

#[tokio::test]
async fn new_year_starts_at_00001() {
    let pool = pool().await;
    let tenant = token("t");
    let nye = parse("2025-12-31T15:59:59Z");
    let jan = parse("2025-12-31T16:00:00Z");
    let mut tx = pool.begin().await.unwrap();
    let a = allocate_document(&mut tx, &tenant, DocSeries::Receipt, nye)
        .await
        .unwrap();
    let b = allocate_document(&mut tx, &tenant, DocSeries::Receipt, jan)
        .await
        .unwrap();
    tx.commit().await.unwrap();
    assert_eq!(a, "RCPT-2025-00001");
    assert_eq!(b, "RCPT-2026-00001");
}

#[tokio::test]
async fn continues_after_backfilled_last_n() {
    let pool = pool().await;
    let tenant = token("t");
    let now = OffsetDateTime::now_utc();
    let year = malaysia_year(now);
    sqlx::query(
        r#"
        INSERT INTO pay_rs.document_sequences (tenant_id, series, year_myt, last_n)
        VALUES ($1, 'RCPT', $2, 7)
        "#,
    )
    .bind(&tenant)
    .bind(year)
    .execute(&pool)
    .await
    .unwrap();
    let mut tx = pool.begin().await.unwrap();
    let number = allocate_document(&mut tx, &tenant, DocSeries::Receipt, now)
        .await
        .unwrap();
    tx.commit().await.unwrap();
    assert_eq!(number, format!("RCPT-{year}-00008"));
}

#[tokio::test]
async fn concurrent_allocate_does_not_duplicate() {
    let pool = pool().await;
    let tenant = token("t");
    let now = OffsetDateTime::now_utc();
    let year = malaysia_year(now);
    let (a, b) = tokio::join!(
        async {
            let mut tx = pool.begin().await.unwrap();
            let n = allocate_document(&mut tx, &tenant, DocSeries::Receipt, now)
                .await
                .unwrap();
            tx.commit().await.unwrap();
            n
        },
        async {
            let mut tx = pool.begin().await.unwrap();
            let n = allocate_document(&mut tx, &tenant, DocSeries::Receipt, now)
                .await
                .unwrap();
            tx.commit().await.unwrap();
            n
        }
    );
    let mut got = [a, b];
    got.sort();
    assert_eq!(got[0], format!("RCPT-{year}-00001"));
    assert_eq!(got[1], format!("RCPT-{year}-00002"));
}

#[tokio::test]
async fn take_issues_rcpt_year_n_not_test_uuid() {
    let pool = pool().await;
    let tenant = TenantId::new(token("t"));
    let payment_id = mint_take(&pool, &tenant).await;
    let number: String = sqlx::query_scalar(
        r#"
        SELECT number FROM pay_rs.documents
         WHERE payment_id = $1 AND series = 'RCPT'
        "#,
    )
    .bind(payment_id.as_uuid())
    .fetch_one(&pool)
    .await
    .unwrap();
    let year = malaysia_year(OffsetDateTime::now_utc());
    assert_eq!(number, format!("RCPT-{year}-00001"));
    assert!(!number.contains("TEST"));
}

#[tokio::test]
async fn two_takes_same_tenant_are_consecutive() {
    let pool = pool().await;
    let tenant = TenantId::new(token("t"));
    let a = mint_take(&pool, &tenant).await;
    let b = mint_take(&pool, &tenant).await;
    let mut numbers: Vec<String> = sqlx::query_scalar(
        r#"
        SELECT number FROM pay_rs.documents
         WHERE tenant_id = $1 AND series = 'RCPT'
         ORDER BY number
        "#,
    )
    .bind(tenant.as_str())
    .fetch_all(&pool)
    .await
    .unwrap();
    numbers.sort();
    let year = malaysia_year(OffsetDateTime::now_utc());
    assert_eq!(
        numbers,
        vec![format!("RCPT-{year}-00001"), format!("RCPT-{year}-00002"),]
    );
    let _ = (a, b);
}

#[tokio::test]
async fn refund_settle_issues_ref_and_joins_by_refund_id() {
    let pool = pool().await;
    let tenant = TenantId::new(token("t"));
    let payment_id = mint_take(&pool, &tenant).await;
    let charge_id: uuid::Uuid =
        sqlx::query_scalar("SELECT id FROM pay_rs.charges WHERE payment_id = $1")
            .bind(payment_id.as_uuid())
            .fetch_one(&pool)
            .await
            .unwrap();
    let refund_id: uuid::Uuid = sqlx::query_scalar(
        r#"
        INSERT INTO pay_rs.refunds (
            tenant_id, payment_id, charge_id,
            amount_minor, currency, exponent, status, rail, reason
        ) VALUES ($1, $2, $3, 1000, 'MYR', 2, 'pending', 'test', 'merchant')
        RETURNING id
        "#,
    )
    .bind(tenant.as_str())
    .bind(payment_id.as_uuid())
    .bind(charge_id)
    .fetch_one(&pool)
    .await
    .unwrap();

    let item = storage::refund_settle::settle_refund(&pool, refund_id)
        .await
        .unwrap();
    let year = malaysia_year(OffsetDateTime::now_utc());
    let expected = format!("REF-{year}-00001");
    assert_eq!(item.number.as_deref(), Some(expected.as_str()));

    let (doc_refund, number): (uuid::Uuid, String) = sqlx::query_as(
        r#"
        SELECT refund_id, number FROM pay_rs.documents
         WHERE series = 'REF' AND payment_id = $1
        "#,
    )
    .bind(payment_id.as_uuid())
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(doc_refund, refund_id);
    assert_eq!(number, format!("REF-{year}-00001"));

    let listed = storage::money_query::refund_by_id(&pool, tenant.as_str(), refund_id)
        .await
        .unwrap()
        .unwrap();
    assert_eq!(listed.number.as_deref(), Some(number.as_str()));
    assert_issued_number("REF", listed.number.as_deref().unwrap());
}
