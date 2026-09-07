mod support;

use sqlx::Row;
use support::pool;
use tokio::sync::Mutex;
use uuid::Uuid;

use storage::backfill::{self, BackfillOpts};
use storage::BackfillError;

static BACKFILL: Mutex<()> = Mutex::const_new(());

fn n_id() -> String {
    Uuid::new_v4().simple().to_string()
}

async fn reset_public(pool: &sqlx::PgPool) {
    storage::backfill::install_public_ddl(pool)
        .await
        .expect("ddl");
    sqlx::query(
        r#"
        TRUNCATE public.checkouts, public.charges, public.refunds,
                 public.journal_entries, public.journal_lines, public.documents,
                 public.org_settings, public.gateway_credentials, public.products,
                 public.prices, public.payment_links, public.document_sequences,
                 public.org_webhook_endpoints
        "#,
    )
    .execute(pool)
    .await
    .expect("truncate public");
}

#[tokio::test]
async fn dry_run_does_not_write_and_abort_on_young_open() {
    let _g = BACKFILL.lock().await;
    let pool = pool().await;
    reset_public(&pool).await;
    let org = format!("bf-{}", n_id());
    let open_id = n_id();
    sqlx::query(
        r#"
        INSERT INTO public.checkouts (
            "Id", "OrgId", "PublicToken", "Amount", "Currency", "Status", "Provider", "CreatedAt"
        ) VALUES ($1, $2, $3, 10.00, 'MYR', 'open', 'stripe', now())
        "#,
    )
    .bind(&open_id)
    .bind(&org)
    .bind(format!("tok-{}", n_id()))
    .execute(&pool)
    .await
    .unwrap();

    let dry = storage::backfill::run(&pool, BackfillOpts::default())
        .await
        .unwrap();
    assert!(dry.open_young >= 1);
    assert!(!dry.aborted);
    let n: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.payments WHERE tenant_id = $1")
            .bind(&org)
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(n, 0);

    let err = storage::backfill::run(&pool, BackfillOpts::apply())
        .await
        .unwrap_err();
    match err {
        BackfillError::DrainNotDone { open_young } => assert!(open_young >= 1),
        other => panic!("{other}"),
    }
    let n: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.payments WHERE tenant_id = $1")
            .bind(&org)
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(n, 0);
}

#[tokio::test]
async fn skips_old_open_copies_paid_myr_and_usdc_rerun_noop() {
    let _g = BACKFILL.lock().await;
    let pool = pool().await;
    reset_public(&pool).await;
    let org = format!("bf-{}", n_id());
    sqlx::query(
        r#"
        INSERT INTO public.org_settings ("OrgId", "Currency", "ChargesPaused")
        VALUES ($1, 'MYR', false)
        "#,
    )
    .bind(&org)
    .execute(&pool)
    .await
    .unwrap();

    let wrap = vec![7u8; 32];
    let b64 = base64::Engine::encode(&base64::engine::general_purpose::STANDARD, &wrap);
    sqlx::query(
        r#"
        INSERT INTO public.gateway_credentials (
            "OrgId", "Provider", "Ciphertext", "WebhookCiphertext", "Last4", "Environment"
        ) VALUES ($1, 'stripe', $2, $2, 'ummy', 'test')
        "#,
    )
    .bind(&org)
    .bind(&b64)
    .execute(&pool)
    .await
    .unwrap();

    sqlx::query(
        r#"
        INSERT INTO public.document_sequences ("OrgId", "Series", "YearMyt", "LastN")
        VALUES ($1, 'RCPT', 2026, 42)
        "#,
    )
    .bind(&org)
    .execute(&pool)
    .await
    .unwrap();

    sqlx::query(
        r#"
        INSERT INTO public.org_webhook_endpoints ("OrgId", "Url", "SecretCiphertext", "SecretPrefix")
        VALUES ($1, 'https://hooks.example/pay', $2, 'whsec_ab')
        "#,
    )
    .bind(&org)
    .bind(&b64)
    .execute(&pool)
    .await
    .unwrap();

    let old_open = n_id();
    sqlx::query(
        r#"
        INSERT INTO public.checkouts (
            "Id", "OrgId", "PublicToken", "Amount", "Currency", "Status", "Provider", "CreatedAt"
        ) VALUES ($1, $2, $3, 10.00, 'MYR', 'open', 'stripe', now() - interval '2 hours')
        "#,
    )
    .bind(&old_open)
    .bind(&org)
    .bind(format!("tok-{}", n_id()))
    .execute(&pool)
    .await
    .unwrap();

    let paid_id = n_id();
    let charge_id = n_id();
    sqlx::query(
        r#"
        INSERT INTO public.checkouts (
            "Id", "OrgId", "PublicToken", "Amount", "Currency", "Status",
            "Provider", "ProviderSessionId", "CreatedAt"
        ) VALUES ($1, $2, $3, 10.00, 'MYR', 'paid', 'stripe', 'cs_live_1', now() - interval '1 day')
        "#,
    )
    .bind(&paid_id)
    .bind(&org)
    .bind(format!("tok-{}", n_id()))
    .execute(&pool)
    .await
    .unwrap();
    sqlx::query(
        r#"
        INSERT INTO public.charges (
            "Id", "OrgId", "CheckoutId", "Provider", "ProviderRef", "Amount", "Currency", "Status"
        ) VALUES ($1, $2, $3, 'stripe', 'cs_live_1', 10.00, 'MYR', 'paid')
        "#,
    )
    .bind(&charge_id)
    .bind(&org)
    .bind(&paid_id)
    .execute(&pool)
    .await
    .unwrap();

    let usdc_id = n_id();
    let usdc_charge = n_id();
    sqlx::query(
        r#"
        INSERT INTO public.checkouts (
            "Id", "OrgId", "PublicToken", "Amount", "Currency", "Status",
            "Provider", "CreatedAt"
        ) VALUES ($1, $2, $3, 10.00, 'USDC', 'paid', 'solana', now() - interval '1 day')
        "#,
    )
    .bind(&usdc_id)
    .bind(&org)
    .bind(format!("tok-{}", n_id()))
    .execute(&pool)
    .await
    .unwrap();
    sqlx::query(
        r#"
        INSERT INTO public.charges (
            "Id", "OrgId", "CheckoutId", "Provider", "ProviderRef", "Amount", "Currency", "Status"
        ) VALUES ($1, $2, $3, 'solana', 'sig_1', 10.00, 'USDC', 'paid')
        "#,
    )
    .bind(&usdc_charge)
    .bind(&org)
    .bind(&usdc_id)
    .execute(&pool)
    .await
    .unwrap();

    let r1 = storage::backfill::run(&pool, BackfillOpts::apply())
        .await
        .unwrap();
    assert!(!r1.aborted);
    assert_eq!(r1.payments_inserted, 2);
    assert_eq!(r1.charges_inserted, 2);

    let paid_uuid = backfill::uuid_from_n(&paid_id).unwrap();
    let row = sqlx::query(
        r#"
        SELECT status, intake, amount_minor, exponent, currency
          FROM pay_rs.payments WHERE id = $1
        "#,
    )
    .bind(paid_uuid)
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(row.get::<String, _>("status"), "settled");
    assert_eq!(row.get::<String, _>("intake"), "taken");
    assert_eq!(row.get::<i64, _>("amount_minor"), 1000);
    assert_eq!(row.get::<i16, _>("exponent"), 2);

    let cap: Option<String> =
        sqlx::query_scalar("SELECT capture_id FROM pay_rs.attempts WHERE payment_id = $1")
            .bind(paid_uuid)
            .fetch_one(&pool)
            .await
            .unwrap();
    assert!(cap.is_none(), "cs_ is not capture_id");

    let usdc_uuid = backfill::uuid_from_n(&usdc_id).unwrap();
    let uminor: i64 = sqlx::query_scalar("SELECT amount_minor FROM pay_rs.payments WHERE id = $1")
        .bind(usdc_uuid)
        .fetch_one(&pool)
        .await
        .unwrap();
    assert_eq!(uminor, 10_000_000);

    let skipped: i64 =
        sqlx::query_scalar("SELECT count(*)::bigint FROM pay_rs.payments WHERE id = $1")
            .bind(backfill::uuid_from_n(&old_open).unwrap())
            .fetch_one(&pool)
            .await
            .unwrap();
    assert_eq!(skipped, 0);

    let ct: Vec<u8> = sqlx::query_scalar(
        "SELECT ciphertext FROM pay_rs.gateway_credentials WHERE tenant_id = $1 AND rail = 'stripe'",
    )
    .bind(&org)
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(ct, wrap);

    let last_n: i32 = sqlx::query_scalar(
        "SELECT last_n FROM pay_rs.document_sequences WHERE tenant_id = $1 AND series = 'RCPT'",
    )
    .bind(&org)
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(last_n, 42);

    let r2 = storage::backfill::run(&pool, BackfillOpts::apply())
        .await
        .unwrap();
    assert_eq!(r2.payments_inserted, 0);
    assert_eq!(r2.charges_inserted, 0);
}

#[tokio::test]
async fn late_pay_marks_returned() {
    let _g = BACKFILL.lock().await;
    let pool = pool().await;
    reset_public(&pool).await;
    let org = format!("bf-{}", n_id());
    let paid_id = n_id();
    let charge_id = n_id();
    let refund_id = n_id();
    sqlx::query(
        r#"
        INSERT INTO public.checkouts (
            "Id", "OrgId", "PublicToken", "Amount", "Currency", "Status",
            "Provider", "CreatedAt"
        ) VALUES ($1, $2, $3, 10.00, 'MYR', 'paid', 'stripe', now() - interval '1 day')
        "#,
    )
    .bind(&paid_id)
    .bind(&org)
    .bind(format!("tok-{}", n_id()))
    .execute(&pool)
    .await
    .unwrap();
    sqlx::query(
        r#"
        INSERT INTO public.charges (
            "Id", "OrgId", "CheckoutId", "Provider", "ProviderRef", "Amount", "Currency", "Status"
        ) VALUES ($1, $2, $3, 'stripe', 'cs_x', 10.00, 'MYR', 'paid')
        "#,
    )
    .bind(&charge_id)
    .bind(&org)
    .bind(&paid_id)
    .execute(&pool)
    .await
    .unwrap();
    sqlx::query(
        r#"
        INSERT INTO public.refunds (
            "Id", "OrgId", "CheckoutId", "Amount", "Currency", "Status",
            "Provider", "Reason", "CreatedAt"
        ) VALUES ($1, $2, $3, 10.00, 'MYR', 'succeeded', 'stripe', 'late_pay', now())
        "#,
    )
    .bind(&refund_id)
    .bind(&org)
    .bind(&paid_id)
    .execute(&pool)
    .await
    .unwrap();

    storage::backfill::run(&pool, BackfillOpts::apply())
        .await
        .unwrap();
    let pid = backfill::uuid_from_n(&paid_id).unwrap();
    let row = sqlx::query(
        "SELECT status, exception, intake, return_kind FROM pay_rs.payments WHERE id = $1",
    )
    .bind(pid)
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(row.get::<String, _>("status"), "settled");
    assert_eq!(row.get::<String, _>("exception"), "late");
    assert_eq!(row.get::<String, _>("intake"), "returned");
    assert_eq!(
        row.get::<Option<String>, _>("return_kind").as_deref(),
        Some("late")
    );
    let n: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.refunds WHERE payment_id = $1 AND reason = 'late_pay'",
    )
    .bind(pid)
    .fetch_one(&pool)
    .await
    .unwrap();
    assert_eq!(n, 1);
}
