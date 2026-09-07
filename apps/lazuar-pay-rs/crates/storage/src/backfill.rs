//! Offline terminal copy `public` (EF) → `pay_rs`. Never writes `public.*`. Never copies `open`.

use std::collections::HashSet;
use std::fmt;
use std::str::FromStr;

use base64::Engine;
use domain::money::{Currency, Money};
use domain::rail::{MethodId, RailId};
use rust_decimal::Decimal;
use sqlx::{PgPool, Postgres, Row, Transaction};
use time::{Duration, OffsetDateTime};
use uuid::Uuid;

pub const PUBLIC_DDL: &str = include_str!("../tests/public_pay_ddl.sql");

#[derive(Debug, thiserror::Error)]
pub enum BackfillError {
    #[error("drain not done: {open_young} open checkouts younger than TTL")]
    DrainNotDone { open_young: i64 },
    #[error("bad id {0}")]
    BadId(String),
    #[error("bad amount {0}")]
    BadMoney(String),
    #[error("bad secret box {0}")]
    BadSecret(String),
    #[error(transparent)]
    Sql(#[from] sqlx::Error),
}

#[derive(Clone, Debug, Default)]
pub struct BackfillOpts {
    pub apply: bool,
    pub abort_open_minutes: i64,
}

impl BackfillOpts {
    pub fn apply() -> Self {
        Self {
            apply: true,
            abort_open_minutes: 30,
        }
    }
}

#[derive(Clone, Debug, Default)]
pub struct BackfillReport {
    pub open_young: i64,
    pub open_skipped: i64,
    pub paid: i64,
    pub failed: i64,
    pub expired: i64,
    pub settings: u64,
    pub credentials: u64,
    pub products: u64,
    pub prices: u64,
    pub links: u64,
    pub sequences: u64,
    pub endpoints: u64,
    pub payments_inserted: u64,
    pub charges_inserted: u64,
    pub refunds_inserted: u64,
    pub aborted: bool,
}

impl fmt::Display for BackfillReport {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        writeln!(
            f,
            "open_young={} open_skipped={} paid={} failed={} expired={}",
            self.open_young, self.open_skipped, self.paid, self.failed, self.expired
        )?;
        writeln!(
            f,
            "settings={} credentials={} products={} links={}",
            self.settings, self.credentials, self.products, self.links
        )?;
        writeln!(
            f,
            "payments_inserted={} charges_inserted={} refunds_inserted={}",
            self.payments_inserted, self.charges_inserted, self.refunds_inserted
        )?;
        write!(f, "aborted={}", self.aborted)
    }
}

pub fn uuid_from_n(id: &str) -> Result<Uuid, BackfillError> {
    let s = id.trim();
    Uuid::try_parse(s).map_err(|_| BackfillError::BadId(s.to_string()))
}

pub fn minor_from_numeric(amount: Decimal, currency: &str) -> Result<(i64, i16), BackfillError> {
    let c = Currency::by_code(currency).ok_or_else(|| BackfillError::BadMoney(currency.into()))?;
    let m = Money::from_quoted_display(amount, c)
        .map_err(|e| BackfillError::BadMoney(e.to_string()))?;
    let minor = i64::try_from(m.minor()).map_err(|_| BackfillError::BadMoney("overflow".into()))?;
    Ok((minor, i16::from(c.exponent)))
}

pub fn decode_secret_box(b64: &str) -> Result<Vec<u8>, BackfillError> {
    let t = b64.trim();
    if t.is_empty() {
        return Ok(Vec::new());
    }
    base64::engine::general_purpose::STANDARD
        .decode(t)
        .map_err(|_| BackfillError::BadSecret(t.chars().take(12).collect()))
}

fn parse_rail(raw: &str) -> Option<RailId> {
    RailId::parse(&raw.trim().to_ascii_lowercase()).ok()
}

fn env_norm(raw: &str, rail: &str) -> String {
    let e = raw.trim().to_ascii_lowercase();
    if rail == "solana" {
        return if e == "mainnet-beta" || e == "mainnet" {
            "mainnet".into()
        } else {
            "devnet".into()
        };
    }
    if e == "live" {
        "live".into()
    } else {
        "test".into()
    }
}

fn affected(r: sqlx::postgres::PgQueryResult) -> u64 {
    r.rows_affected()
}

pub async fn install_public_ddl(pool: &PgPool) -> Result<(), BackfillError> {
    sqlx::raw_sql(PUBLIC_DDL).execute(pool).await?;
    Ok(())
}

pub async fn run(pool: &PgPool, opts: BackfillOpts) -> Result<BackfillReport, BackfillError> {
    let minutes = if opts.abort_open_minutes <= 0 {
        30
    } else {
        opts.abort_open_minutes
    };
    let mut report = counts(pool, minutes).await?;
    if !opts.apply {
        return Ok(report);
    }
    if report.open_young > 0 {
        report.aborted = true;
        return Err(BackfillError::DrainNotDone {
            open_young: report.open_young,
        });
    }
    let mut tx = pool.begin().await?;
    copy_catalog(&mut tx, &mut report).await?;
    copy_money(&mut tx, &mut report).await?;
    tx.commit().await?;
    Ok(report)
}

async fn counts(pool: &PgPool, minutes: i64) -> Result<BackfillReport, BackfillError> {
    let open_young: i64 = sqlx::query_scalar(
        r#"
        SELECT count(*)::bigint FROM public.checkouts
         WHERE "Status" = 'open'
           AND "CreatedAt" > now() - ($1::bigint * interval '1 minute')
        "#,
    )
    .bind(minutes)
    .fetch_one(pool)
    .await?;
    let open_skipped: i64 = sqlx::query_scalar(
        r#"
        SELECT count(*)::bigint FROM public.checkouts
         WHERE "Status" = 'open'
           AND "CreatedAt" <= now() - ($1::bigint * interval '1 minute')
        "#,
    )
    .bind(minutes)
    .fetch_one(pool)
    .await?;
    let paid: i64 = sqlx::query_scalar(
        r#"SELECT count(*)::bigint FROM public.checkouts WHERE "Status" = 'paid'"#,
    )
    .fetch_one(pool)
    .await?;
    let failed: i64 = sqlx::query_scalar(
        r#"SELECT count(*)::bigint FROM public.checkouts WHERE "Status" = 'failed'"#,
    )
    .fetch_one(pool)
    .await?;
    let expired: i64 = sqlx::query_scalar(
        r#"SELECT count(*)::bigint FROM public.checkouts WHERE "Status" = 'expired'"#,
    )
    .fetch_one(pool)
    .await?;
    Ok(BackfillReport {
        open_young,
        open_skipped,
        paid,
        failed,
        expired,
        ..BackfillReport::default()
    })
}

async fn copy_catalog(
    tx: &mut Transaction<'_, Postgres>,
    report: &mut BackfillReport,
) -> Result<(), BackfillError> {
    let settings = sqlx::query(
        r#"SELECT "OrgId", "Currency", "ChargesPaused", "OneWebhookCiphertext" FROM public.org_settings"#,
    )
    .fetch_all(&mut **tx)
    .await?;
    for row in settings {
        let org: String = row.get("OrgId");
        let currency: String = row.get("Currency");
        let paused: bool = row.get("ChargesPaused");
        let one_wh: Option<String> = row.get("OneWebhookCiphertext");
        let n = sqlx::query(
            r#"
            INSERT INTO pay_rs.org_settings (tenant_id, charges_paused, currency, one_webhook_ciphertext)
            VALUES ($1, $2, $3, $4)
            ON CONFLICT DO NOTHING
            "#,
        )
        .bind(&org)
        .bind(paused)
        .bind(&currency)
        .bind(one_wh)
        .execute(&mut **tx)
        .await?;
        report.settings += affected(n);
    }

    let creds = sqlx::query(
        r#"
        SELECT "OrgId", "Provider", "Ciphertext", "Last4", "WebhookCiphertext",
               "PublicMerchantId", "Environment", "UpdatedAt"
          FROM public.gateway_credentials
        "#,
    )
    .fetch_all(&mut **tx)
    .await?;
    for row in creds {
        let org: String = row.get("OrgId");
        let provider: String = row.get("Provider");
        let Some(rail) = parse_rail(&provider) else {
            continue;
        };
        if rail == RailId::TEST {
            continue;
        }
        let ct: String = row.get("Ciphertext");
        let last4: Option<String> = row.get("Last4");
        let wh: Option<String> = row.get("WebhookCiphertext");
        let pmid: Option<String> = row.get("PublicMerchantId");
        let env: String = row.get("Environment");
        let updated: OffsetDateTime = row.get("UpdatedAt");
        let cipher = decode_secret_box(&ct)?;
        let wh_bytes = match wh.as_deref() {
            Some(s) if !s.trim().is_empty() => Some(decode_secret_box(s)?),
            _ => None,
        };
        let n = sqlx::query(
            r#"
            INSERT INTO pay_rs.gateway_credentials (
                tenant_id, rail, ciphertext, last4, webhook_ciphertext,
                public_merchant_id, environment, updated_at
            ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
            ON CONFLICT DO NOTHING
            "#,
        )
        .bind(&org)
        .bind(rail.as_str())
        .bind(&cipher)
        .bind(last4)
        .bind(wh_bytes)
        .bind(pmid)
        .bind(env_norm(&env, rail.as_str()))
        .bind(updated)
        .execute(&mut **tx)
        .await?;
        report.credentials += affected(n);
    }

    let products = sqlx::query(
        r#"SELECT "Id", "OrgId", "Name", "Description", "CreatedAt" FROM public.products"#,
    )
    .fetch_all(&mut **tx)
    .await?;
    for row in products {
        let id = uuid_from_n(row.get::<String, _>("Id").as_str())?;
        let n = sqlx::query(
            r#"
            INSERT INTO pay_rs.products (id, tenant_id, name, description, created_at)
            VALUES ($1, $2, $3, $4, $5)
            ON CONFLICT DO NOTHING
            "#,
        )
        .bind(id)
        .bind(row.get::<String, _>("OrgId"))
        .bind(row.get::<String, _>("Name"))
        .bind(row.get::<Option<String>, _>("Description"))
        .bind(row.get::<OffsetDateTime, _>("CreatedAt"))
        .execute(&mut **tx)
        .await?;
        report.products += affected(n);
    }

    let prices = sqlx::query(
        r#"SELECT "Id", "ProductId", "Currency", "Amount"::text, "Interval" FROM public.prices"#,
    )
    .fetch_all(&mut **tx)
    .await?;
    for row in prices {
        let interval: String = row.get("Interval");
        if interval != "one_off" {
            continue;
        }
        let ccy: String = row.get("Currency");
        let amt = Decimal::from_str(row.get::<String, _>("Amount").as_str())
            .map_err(|_| BackfillError::BadMoney(ccy.clone()))?;
        let Ok((minor, exp)) = minor_from_numeric(amt, &ccy) else {
            continue;
        };
        let product_id = uuid_from_n(row.get::<String, _>("ProductId").as_str())?;
        let tenant: Option<String> =
            sqlx::query_scalar("SELECT tenant_id FROM pay_rs.products WHERE id = $1")
                .bind(product_id)
                .fetch_optional(&mut **tx)
                .await?;
        let Some(tenant) = tenant else {
            continue;
        };
        let n = sqlx::query(
            r#"
            INSERT INTO pay_rs.prices (
                id, product_id, tenant_id, amount_minor, currency, exponent, interval
            ) VALUES ($1, $2, $3, $4, $5, $6, 'one_off')
            ON CONFLICT DO NOTHING
            "#,
        )
        .bind(uuid_from_n(row.get::<String, _>("Id").as_str())?)
        .bind(product_id)
        .bind(tenant)
        .bind(minor)
        .bind(&ccy)
        .bind(exp)
        .execute(&mut **tx)
        .await?;
        report.prices += affected(n);
    }

    let links = sqlx::query(
        r#"
        SELECT "Id", "OrgId", "PublicToken", "Provider", "ProductId",
               "Amount"::text, "Currency", "MaxPayers", "Label", "CreatedAt"
          FROM public.payment_links
        "#,
    )
    .fetch_all(&mut **tx)
    .await?;
    for row in links {
        let Some(rail) = parse_rail(row.get::<String, _>("Provider").as_str()) else {
            continue;
        };
        let ccy: String = row.get("Currency");
        let amt = Decimal::from_str(row.get::<String, _>("Amount").as_str())
            .map_err(|_| BackfillError::BadMoney(ccy.clone()))?;
        let Ok((minor, exp)) = minor_from_numeric(amt, &ccy) else {
            continue;
        };
        let product_id = match row.get::<Option<String>, _>("ProductId") {
            Some(p) => uuid_from_n(&p).ok(),
            None => None,
        };
        let n = sqlx::query(
            r#"
            INSERT INTO pay_rs.payment_links (
                id, tenant_id, public_token, rail, product_id,
                amount_minor, currency, exponent, max_payers, label, created_at
            ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)
            ON CONFLICT DO NOTHING
            "#,
        )
        .bind(uuid_from_n(row.get::<String, _>("Id").as_str())?)
        .bind(row.get::<String, _>("OrgId"))
        .bind(row.get::<String, _>("PublicToken"))
        .bind(rail.as_str())
        .bind(product_id)
        .bind(minor)
        .bind(&ccy)
        .bind(exp)
        .bind(row.get::<Option<i32>, _>("MaxPayers"))
        .bind(row.get::<Option<String>, _>("Label"))
        .bind(row.get::<OffsetDateTime, _>("CreatedAt"))
        .execute(&mut **tx)
        .await?;
        report.links += affected(n);
    }

    let seqs = sqlx::query(
        r#"SELECT "OrgId", "Series", "YearMyt", "LastN" FROM public.document_sequences"#,
    )
    .fetch_all(&mut **tx)
    .await?;
    for row in seqs {
        let series: String = row.get("Series");
        if series != "RCPT" && series != "REF" {
            continue;
        }
        let n = sqlx::query(
            r#"
            INSERT INTO pay_rs.document_sequences (tenant_id, series, year_myt, last_n)
            VALUES ($1, $2, $3, $4)
            ON CONFLICT DO NOTHING
            "#,
        )
        .bind(row.get::<String, _>("OrgId"))
        .bind(&series)
        .bind(row.get::<i32, _>("YearMyt"))
        .bind(row.get::<i32, _>("LastN"))
        .execute(&mut **tx)
        .await?;
        report.sequences += affected(n);
    }

    let eps = sqlx::query(
        r#"
        SELECT "OrgId", "Url", "SecretCiphertext", "SecretPrefix", "UpdatedAt"
          FROM public.org_webhook_endpoints
        "#,
    )
    .fetch_all(&mut **tx)
    .await?;
    for row in eps {
        let secret = decode_secret_box(row.get::<String, _>("SecretCiphertext").as_str())?;
        let n = sqlx::query(
            r#"
            INSERT INTO pay_rs.org_webhook_endpoints (
                tenant_id, url, secret_ciphertext, secret_prefix, updated_at
            ) VALUES ($1, $2, $3, $4, $5)
            ON CONFLICT DO NOTHING
            "#,
        )
        .bind(row.get::<String, _>("OrgId"))
        .bind(row.get::<String, _>("Url"))
        .bind(&secret)
        .bind(row.get::<Option<String>, _>("SecretPrefix"))
        .bind(row.get::<OffsetDateTime, _>("UpdatedAt"))
        .execute(&mut **tx)
        .await?;
        report.endpoints += affected(n);
    }
    Ok(())
}

async fn copy_money(
    tx: &mut Transaction<'_, Postgres>,
    report: &mut BackfillReport,
) -> Result<(), BackfillError> {
    let rows = sqlx::query(
        r#"
        SELECT "Id", "OrgId", "PublicToken", "Amount"::text, "Currency", "Status",
               "SuccessUrl", "CancelUrl", "PspRedirectUrl", "PayerName", "PayerEmail",
               "ProductId", "Provider", "ProviderSessionId", "PaymentLinkId", "SlotKey",
               "CreatedAt"
          FROM public.checkouts
         WHERE "Status" IN ('paid', 'failed', 'expired')
        "#,
    )
    .fetch_all(&mut **tx)
    .await?;
    let mut proofs: HashSet<String> = HashSet::new();
    for row in rows {
        copy_checkout(tx, report, &mut proofs, &row).await?;
    }
    Ok(())
}

async fn copy_checkout(
    tx: &mut Transaction<'_, Postgres>,
    report: &mut BackfillReport,
    proofs: &mut HashSet<String>,
    row: &sqlx::postgres::PgRow,
) -> Result<(), BackfillError> {
    let id_raw: String = row.get("Id");
    let org: String = row.get("OrgId");
    let status_in: String = row.get("Status");
    let ccy: String = row.get("Currency");
    let provider: Option<String> = row.get("Provider");
    let Some(rail) = provider.as_deref().and_then(parse_rail) else {
        return Ok(());
    };
    let amt = Decimal::from_str(row.get::<String, _>("Amount").as_str())
        .map_err(|_| BackfillError::BadMoney(id_raw.clone()))?;
    let Ok((minor, exp)) = minor_from_numeric(amt, &ccy) else {
        return Ok(());
    };
    let payment_id = uuid_from_n(&id_raw)?;
    let created: OffsetDateTime = row.get("CreatedAt");
    let exp_at = created + Duration::minutes(30);

    let charge = sqlx::query(
        r#"
        SELECT "Id", "ProviderRef", "Amount"::text, "Currency", "Status", "Provider"
          FROM public.charges WHERE "CheckoutId" = $1
        "#,
    )
    .bind(&id_raw)
    .fetch_optional(&mut **tx)
    .await?;

    let late = sqlx::query(
        r#"
        SELECT "Id", "Amount"::text, "Currency", "Status", "Provider", "ProviderRef",
               "IdempotencyKey", "CreatedAt", "AttemptCount", "NextAttemptAt", "LastError"
          FROM public.refunds
         WHERE "CheckoutId" = $1 AND "Reason" = 'late_pay'
         ORDER BY "CreatedAt" ASC
         LIMIT 1
        "#,
    )
    .bind(&id_raw)
    .fetch_optional(&mut **tx)
    .await?;

    let pay_status = match status_in.as_str() {
        "paid" => "settled",
        "failed" => "failed",
        _ => "expired",
    };
    let attempt_status = pay_status;
    let terminal = match status_in.as_str() {
        "paid" => "none",
        "failed" => "psp_failed",
        _ => "watch_timeout",
    };
    let has_late = late.is_some();
    let exception = if has_late { "late" } else { "none" };
    let (intake, intake_charge, intake_refund, return_kind) = if has_late {
        let rid = uuid_from_n(late.as_ref().unwrap().get::<String, _>("Id").as_str())?;
        ("returned", None, Some(rid), Some("late"))
    } else if status_in == "paid" {
        if let Some(ch) = &charge {
            let cid = uuid_from_n(ch.get::<String, _>("Id").as_str())?;
            ("taken", Some(cid), None, None)
        } else {
            ("open", None, None, None)
        }
    } else {
        ("open", None, None, None)
    };

    let product_id = match row.get::<Option<String>, _>("ProductId") {
        Some(p) => uuid_from_n(&p).ok(),
        None => None,
    };
    let link_id = match row.get::<Option<String>, _>("PaymentLinkId") {
        Some(p) => uuid_from_n(&p).ok(),
        None => None,
    };
    let ccy_code = Currency::by_code(&ccy)
        .map(|c| c.code)
        .unwrap_or(Currency::MYR.code);
    let method = MethodId::new(ccy_code, rail).to_string();

    let n = sqlx::query(
        r#"
        INSERT INTO pay_rs.payments (
            id, tenant_id, public_token, amount_minor, currency, exponent,
            status, exception, intake, intake_charge_id, intake_refund_id, return_kind,
            terminal_reason, expires_at, monitoring_until, payment_link_id, slot_key,
            product_id, success_url, cancel_url, payer_name, payer_email, version, created_at
        ) VALUES (
            $1, $2, $3, $4, $5, $6,
            $7, $8, $9, $10, $11, $12,
            $13, $14, $15, $16, $17,
            $18, $19, $20, $21, $22, 1, $23
        )
        ON CONFLICT DO NOTHING
        "#,
    )
    .bind(payment_id)
    .bind(&org)
    .bind(row.get::<String, _>("PublicToken"))
    .bind(minor)
    .bind(&ccy)
    .bind(exp)
    .bind(pay_status)
    .bind(exception)
    .bind(intake)
    .bind(intake_charge)
    .bind(intake_refund)
    .bind(return_kind)
    .bind(terminal)
    .bind(exp_at)
    .bind(exp_at)
    .bind(link_id)
    .bind(row.get::<Option<String>, _>("SlotKey"))
    .bind(product_id)
    .bind(row.get::<Option<String>, _>("SuccessUrl"))
    .bind(row.get::<Option<String>, _>("CancelUrl"))
    .bind(row.get::<Option<String>, _>("PayerName"))
    .bind(row.get::<Option<String>, _>("PayerEmail"))
    .bind(created)
    .execute(&mut **tx)
    .await?;
    if affected(n) == 0 {
        return Ok(());
    }
    report.payments_inserted += 1;

    let session_id: Option<String> = row.get("ProviderSessionId");
    let capture_from_session = session_id
        .as_deref()
        .filter(|s| s.starts_with("pi_"))
        .map(str::to_string);
    let fail_reason = if pay_status == "settled" {
        None
    } else {
        Some(terminal)
    };
    let attempt_id = Uuid::new_v4();
    sqlx::query(
        r#"
        INSERT INTO pay_rs.attempts (
            id, payment_id, tenant_id, rail, method,
            amount_minor, currency, exponent, status,
            session_url, session_id, capture_id, fail_reason, version, created_at, updated_at
        ) VALUES (
            $1, $2, $3, $4, $5,
            $6, $7, $8, $9,
            $10, $11, $12, $13, 1, $14, $14
        )
        ON CONFLICT DO NOTHING
        "#,
    )
    .bind(attempt_id)
    .bind(payment_id)
    .bind(&org)
    .bind(rail.as_str())
    .bind(&method)
    .bind(minor)
    .bind(&ccy)
    .bind(exp)
    .bind(attempt_status)
    .bind(row.get::<Option<String>, _>("PspRedirectUrl"))
    .bind(&session_id)
    .bind(&capture_from_session)
    .bind(fail_reason)
    .bind(created)
    .execute(&mut **tx)
    .await?;

    if let Some(ch) = charge {
        let charge_id = uuid_from_n(ch.get::<String, _>("Id").as_str())?;
        let pref: Option<String> = ch.get("ProviderRef");
        let capture = pref
            .as_deref()
            .filter(|s| s.starts_with("pi_"))
            .map(str::to_string);
        let st: String = ch.get("Status");
        let ch_status = match st.as_str() {
            "partially_refunded" | "refunded" => st,
            _ => "paid".into(),
        };
        let ch_ccy: String = ch.get("Currency");
        let ch_amt = Decimal::from_str(ch.get::<String, _>("Amount").as_str()).unwrap_or(amt);
        let (ch_minor, ch_exp) = minor_from_numeric(ch_amt, &ch_ccy).unwrap_or((minor, exp));
        let n = sqlx::query(
            r#"
            INSERT INTO pay_rs.charges (
                id, tenant_id, payment_id, attempt_id, rail, capture_id,
                amount_minor, currency, exponent, status, created_at
            ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)
            ON CONFLICT DO NOTHING
            "#,
        )
        .bind(charge_id)
        .bind(&org)
        .bind(payment_id)
        .bind(attempt_id)
        .bind(rail.as_str())
        .bind(&capture)
        .bind(ch_minor)
        .bind(&ch_ccy)
        .bind(ch_exp)
        .bind(&ch_status)
        .bind(created)
        .execute(&mut **tx)
        .await?;
        report.charges_inserted += affected(n);

        if pay_status == "settled" {
            let mut proof = pref
                .as_deref()
                .filter(|s| !s.is_empty())
                .map(str::to_string)
                .unwrap_or_else(|| format!("backfill:{charge_id}"));
            let key = format!("{org}:{proof}");
            if !proofs.insert(key) {
                proof = format!("{proof}:{id_raw}");
                proofs.insert(format!("{org}:{proof}"));
            }
            sqlx::query(
                r#"
                INSERT INTO pay_rs.settlements (
                    id, attempt_id, payment_id, tenant_id,
                    amount_minor, currency, exponent, state, proof_kind, proof_id, finalized, created_at
                ) VALUES (
                    $1, $2, $3, $4, $5, $6, $7, 'confirmed', 'psp_webhook', $8, true, $9
                )
                ON CONFLICT DO NOTHING
                "#,
            )
            .bind(Uuid::new_v4())
            .bind(attempt_id)
            .bind(payment_id)
            .bind(&org)
            .bind(ch_minor)
            .bind(&ch_ccy)
            .bind(ch_exp)
            .bind(&proof)
            .bind(created)
            .execute(&mut **tx)
            .await?;
        }
    }

    copy_refunds(tx, report, &org, &id_raw, payment_id, rail.as_str()).await?;
    copy_journal(tx, &org, &id_raw, payment_id, &ccy).await?;
    copy_documents(tx, &org, &id_raw, payment_id).await?;
    Ok(())
}

async fn copy_refunds(
    tx: &mut Transaction<'_, Postgres>,
    report: &mut BackfillReport,
    org: &str,
    checkout_id: &str,
    payment_id: Uuid,
    rail: &str,
) -> Result<(), BackfillError> {
    let rows = sqlx::query(
        r#"
        SELECT "Id", "ChargeId", "Amount"::text, "Currency", "Status", "Provider",
               "ProviderRef", "Reason", "IdempotencyKey", "CreatedAt",
               "AttemptCount", "NextAttemptAt", "LastError"
          FROM public.refunds WHERE "CheckoutId" = $1
        "#,
    )
    .bind(checkout_id)
    .fetch_all(&mut **tx)
    .await?;
    for row in rows {
        let reason_in: String = row.get("Reason");
        let reason = match reason_in.as_str() {
            "over_capacity" => "over_capacity",
            "late_pay" => "late_pay",
            _ => "merchant",
        };
        let charge_id = if reason == "merchant" {
            match row.get::<Option<String>, _>("ChargeId") {
                Some(c) => Some(uuid_from_n(&c)?),
                None => continue,
            }
        } else {
            None
        };
        let ccy: String = row.get("Currency");
        let amt = match Decimal::from_str(row.get::<String, _>("Amount").as_str()) {
            Ok(d) => d,
            Err(_) => continue,
        };
        let Ok((minor, exp)) = minor_from_numeric(amt, &ccy) else {
            continue;
        };
        let st: String = row.get("Status");
        let status = match st.as_str() {
            "pending" | "succeeded" | "failed" | "manual" => st,
            _ => "succeeded".into(),
        };
        let n = sqlx::query(
            r#"
            INSERT INTO pay_rs.refunds (
                id, tenant_id, payment_id, charge_id,
                amount_minor, currency, exponent, status, rail, reason,
                provider_ref, idempotency_key, attempt_count, next_attempt_at, last_error, created_at
            ) VALUES (
                $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16
            )
            ON CONFLICT DO NOTHING
            "#,
        )
        .bind(uuid_from_n(row.get::<String, _>("Id").as_str())?)
        .bind(org)
        .bind(payment_id)
        .bind(charge_id)
        .bind(minor)
        .bind(&ccy)
        .bind(exp)
        .bind(&status)
        .bind(rail)
        .bind(reason)
        .bind(row.get::<Option<String>, _>("ProviderRef"))
        .bind(row.get::<Option<String>, _>("IdempotencyKey"))
        .bind(row.get::<i32, _>("AttemptCount"))
        .bind(row.get::<Option<OffsetDateTime>, _>("NextAttemptAt"))
        .bind(row.get::<Option<String>, _>("LastError"))
        .bind(row.get::<OffsetDateTime, _>("CreatedAt"))
        .execute(&mut **tx)
        .await?;
        report.refunds_inserted += affected(n);
    }
    Ok(())
}

async fn copy_journal(
    tx: &mut Transaction<'_, Postgres>,
    org: &str,
    checkout_id: &str,
    payment_id: Uuid,
    ccy: &str,
) -> Result<(), BackfillError> {
    let entries = sqlx::query(
        r#"
        SELECT "Id", "Currency", "CreatedAt" FROM public.journal_entries
         WHERE "CheckoutId" = $1
        "#,
    )
    .bind(checkout_id)
    .fetch_all(&mut **tx)
    .await?;
    for entry in entries {
        let eid = uuid_from_n(entry.get::<String, _>("Id").as_str())?;
        let ecy: String = entry.get("Currency");
        let exp = Currency::by_code(&ecy)
            .map(|c| i16::from(c.exponent))
            .unwrap_or(2);
        sqlx::query(
            r#"
            INSERT INTO pay_rs.journal_entries (id, tenant_id, payment_id, currency, exponent, created_at)
            VALUES ($1, $2, $3, $4, $5, $6)
            ON CONFLICT DO NOTHING
            "#,
        )
        .bind(eid)
        .bind(org)
        .bind(payment_id)
        .bind(&ecy)
        .bind(exp)
        .bind(entry.get::<OffsetDateTime, _>("CreatedAt"))
        .execute(&mut **tx)
        .await?;
        let lines = sqlx::query(
            r#"
            SELECT "Id", "Account", "Dc", "Amount"::text
              FROM public.journal_lines WHERE "EntryId" = $1
            "#,
        )
        .bind(entry.get::<String, _>("Id"))
        .fetch_all(&mut **tx)
        .await?;
        for line in lines {
            let amt = match Decimal::from_str(line.get::<String, _>("Amount").as_str()) {
                Ok(d) => d,
                Err(_) => continue,
            };
            let Ok((minor, _)) = minor_from_numeric(amt, ccy) else {
                continue;
            };
            sqlx::query(
                r#"
                INSERT INTO pay_rs.journal_lines (id, entry_id, account, dc, amount_minor)
                VALUES ($1, $2, $3, $4, $5)
                ON CONFLICT DO NOTHING
                "#,
            )
            .bind(uuid_from_n(line.get::<String, _>("Id").as_str())?)
            .bind(eid)
            .bind(line.get::<String, _>("Account"))
            .bind(line.get::<String, _>("Dc"))
            .bind(minor)
            .execute(&mut **tx)
            .await?;
        }
    }
    Ok(())
}

async fn copy_documents(
    tx: &mut Transaction<'_, Postgres>,
    org: &str,
    checkout_id: &str,
    payment_id: Uuid,
) -> Result<(), BackfillError> {
    let rows = sqlx::query(
        r#"
        SELECT "Id", "Number", "Title", "CreatedAt"
          FROM public.documents WHERE "CheckoutId" = $1
        "#,
    )
    .bind(checkout_id)
    .fetch_all(&mut **tx)
    .await?;
    for row in rows {
        let Some(number) = row.get::<Option<String>, _>("Number") else {
            continue;
        };
        if number.is_empty() || number == "PENDING" {
            continue;
        }
        let series = if number.starts_with("RCPT-") {
            "RCPT"
        } else if number.starts_with("REF-") {
            "REF"
        } else {
            continue;
        };
        sqlx::query(
            r#"
            INSERT INTO pay_rs.documents (
                id, tenant_id, payment_id, series, number, title, created_at
            ) VALUES ($1, $2, $3, $4, $5, $6, $7)
            ON CONFLICT DO NOTHING
            "#,
        )
        .bind(uuid_from_n(row.get::<String, _>("Id").as_str())?)
        .bind(org)
        .bind(payment_id)
        .bind(series)
        .bind(&number)
        .bind(row.get::<String, _>("Title"))
        .bind(row.get::<OffsetDateTime, _>("CreatedAt"))
        .execute(&mut **tx)
        .await?;
    }
    Ok(())
}

#[cfg(test)]
mod unit {
    use super::*;

    #[test]
    fn uuid_from_n_simple_and_hyphenated() {
        let a = uuid_from_n("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa").unwrap();
        let b = uuid_from_n("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa").unwrap();
        assert_eq!(a, b);
    }

    #[test]
    fn minor_myr_and_usdc() {
        let ten = Decimal::from_str("10.00").unwrap();
        assert_eq!(minor_from_numeric(ten, "MYR").unwrap(), (1000, 2));
        assert_eq!(minor_from_numeric(ten, "USDC").unwrap(), (10_000_000, 6));
        assert_eq!(minor_from_numeric(ten, "IDR").unwrap(), (1000, 2));
        assert!(minor_from_numeric(ten, "JPY").is_err());
    }

    #[test]
    fn decode_empty_and_base64() {
        assert!(decode_secret_box("").unwrap().is_empty());
        let raw = vec![1u8, 2, 3, 4];
        let b64 = base64::engine::general_purpose::STANDARD.encode(&raw);
        assert_eq!(decode_secret_box(&b64).unwrap(), raw);
    }
}
