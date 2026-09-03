//! Ingest end-to-end — the test rail drives the full pipeline against real
//! Postgres: verification → dedupe → CAS → fulfillment → receipt → journal.

mod support;

use hmac::{Hmac, Mac};
use lazuar_api::rails::remote::Refunder;
use lazuar_api::secrets::SecretBox;
use lazuar_api::webhooks::ingest::{handle, IngestInput, IngestOutcome};
use lazuar_api::webhooks::psp_parse::Headers;
use rust_decimal::Decimal;
use sha2::Sha256;
use std::str::FromStr;
use support::TestApp;

struct OkRemote;
impl Refunder for OkRemote {
    fn refund_charge(&self, _: &lazuar_api::rails::remote::ChargeRef, _: Decimal, _: &str) -> Result<(), lazuar_api::rails::remote::RefundRemoteError> {
        Ok(())
    }
}

fn hmac_hex(secret: &str, body: &str) -> String {
    let mut mac = <Hmac<Sha256> as Mac>::new_from_slice(secret.as_bytes()).unwrap();
    mac.update(body.as_bytes());
    hex::encode(mac.finalize().into_bytes())
}

fn ingest(app: &TestApp, db: &mut postgres::Client, body: &str, sig: &str) -> IngestOutcome {
    let box_one = SecretBox::from_env_testing(None).unwrap();
    let gates = lazuar_api::money::fulfillment::CheckoutGates::default();
    let input = IngestInput {
        provider_raw: "test",
        org_id: "org_1",
        raw_body: body,
        headers: &headers_vec(sig),
        environment: "Testing",
        test_webhook_secret: &app.config.test_webhook_secret,
        stripe_webhook_secret: &app.config.stripe_webhook_secret,
    };
    handle(db, &box_one, &gates, &OkRemote, &input).unwrap()
}

fn headers_vec(sig: &str) -> Vec<(String, String)> {
    vec![("X-Pay-Test-Signature".to_string(), sig.to_string())]
}

fn signed_body(event_id: &str, checkout_id: &str, amount_total: i64, currency: &str) -> String {
    format!(r#"{{"id":"{event_id}","checkout_id":"{checkout_id}","amount_total":{amount_total},"currency":"{currency}"}}"#)
}

#[test]
fn paid_event_fulfills_with_receipt_journal_and_charge() {
    let app = TestApp::spawn();
    let mut db = app.db();
    support::insert_checkout_org(&mut db, uuid::Uuid::new_v4(), "org_1", "open");
    let checkout_id: String = db
        .query_one("SELECT \"Id\" FROM public.checkouts LIMIT 1", &[])
        .unwrap()
        .get(0);

    let body = signed_body("evt_paid_1", &checkout_id, 990, "MYR");
    let outcome = ingest(&app, &mut db, &body, &hmac_hex(&app.config.test_webhook_secret, &body));
    assert!(matches!(outcome, IngestOutcome::PaidOk), "got {outcome:?}");

    assert_eq!(
        db.query_one("SELECT \"Status\" FROM public.checkouts", &[]).unwrap().get::<_, String>(0),
        "paid"
    );
    let charges: i64 = db.query_one("SELECT count(*) FROM public.charges", &[]).unwrap().get(0);
    assert_eq!(charges, 1);
    let lines: i64 = db.query_one("SELECT count(*) FROM public.journal_lines", &[]).unwrap().get(0);
    assert_eq!(lines, 2, "two-line journal: cash D / revenue C");
    let docs: i64 = db.query_one("SELECT count(*) FROM public.documents", &[]).unwrap().get(0);
    assert_eq!(docs, 1);
    let number: String = db.query_one("SELECT \"Number\" FROM public.documents", &[]).unwrap().get(0);
    assert!(number.starts_with("RCPT-"), "official receipt numbering: {number}");

    // Replay of the same event: duplicate, still one charge.
    let outcome = ingest(&app, &mut db, &body, &hmac_hex(&app.config.test_webhook_secret, &body));
    assert!(matches!(outcome, IngestOutcome::Duplicate));
    let charges: i64 = db.query_one("SELECT count(*) FROM public.charges", &[]).unwrap().get(0);
    assert_eq!(charges, 1);
}

#[test]
fn amount_mismatch_does_not_consume_the_event() {
    let app = TestApp::spawn();
    let mut db = app.db();
    support::insert_checkout_org(&mut db, uuid::Uuid::new_v4(), "org_1", "open");
    let checkout_id: String = db.query_one("SELECT \"Id\" FROM public.checkouts LIMIT 1", &[]).unwrap().get(0);

    // Wrong amount: rejected…
    let body = signed_body("evt_amt", &checkout_id, 500, "MYR");
    let outcome = ingest(&app, &mut db, &body, &hmac_hex(&app.config.test_webhook_secret, &body));
    assert!(matches!(outcome, IngestOutcome::AmountMismatch), "got {outcome:?}");

    // …and the event was NOT consumed — the corrected event still fulfills.
    let events: i64 = db.query_one("SELECT count(*) FROM public.psp_webhook_events", &[]).unwrap().get(0);
    assert_eq!(events, 0, "a rejected amount must not consume the event id");

    let body = signed_body("evt_amt", &checkout_id, 990, "MYR");
    let outcome = ingest(&app, &mut db, &body, &hmac_hex(&app.config.test_webhook_secret, &body));
    assert!(matches!(outcome, IngestOutcome::PaidOk), "got {outcome:?}");
}

#[test]
fn currency_mismatch_is_rejected() {
    let app = TestApp::spawn();
    let mut db = app.db();
    support::insert_checkout_org(&mut db, uuid::Uuid::new_v4(), "org_1", "open");
    let checkout_id: String = db.query_one("SELECT \"Id\" FROM public.checkouts LIMIT 1", &[]).unwrap().get(0);

    let body = signed_body("evt_ccy", &checkout_id, 990, "USD");
    let outcome = ingest(&app, &mut db, &body, &hmac_hex(&app.config.test_webhook_secret, &body));
    assert!(matches!(outcome, IngestOutcome::CurrencyMismatch), "got {outcome:?}");
}

#[test]
fn failed_event_flips_checkout_and_marks_subscription_past_due() {
    let app = TestApp::spawn();
    let mut db = app.db();
    support::insert_checkout_org(&mut db, uuid::Uuid::new_v4(), "org_1", "open");
    let checkout_id: String = db.query_one("SELECT \"Id\" FROM public.checkouts LIMIT 1", &[]).unwrap().get(0);
    // The org subscribes to outbound webhooks.
    db.execute(
        "INSERT INTO public.org_webhook_endpoints \
         (\"OrgId\",\"Url\",\"SecretCiphertext\",\"SecretPrefix\",\"UpdatedAt\") \
         VALUES ($1,$2,$3,$4,$5)",
        &[
            &"org_1",
            &"https://app.test/webhooks",
            &"wrapped",
            &"wr_",
            &chrono::Utc::now(),
        ],
    )
    .unwrap();
    db.execute(
        "INSERT INTO public.subscriptions \
         (\"Id\",\"OrgId\",\"CheckoutId\",\"PayerId\",\"Status\",\"Interval\",\"AttemptCount\",\"CreatedAt\") \
         VALUES ($1,$2,$3,NULL,'active','mo',0,$4)",
        &[
            &uuid::Uuid::new_v4().simple().to_string(),
            &"org_1",
            &checkout_id,
            &chrono::Utc::now(),
        ],
    )
    .unwrap();

    let body = format!(r#"{{"id":"evt_fail","checkout_id":"{checkout_id}","status":"failed"}}"#);
    let outcome = ingest(&app, &mut db, &body, &hmac_hex(&app.config.test_webhook_secret, &body));
    assert!(matches!(outcome, IngestOutcome::Failed), "got {outcome:?}");

    assert_eq!(
        db.query_one("SELECT \"Status\" FROM public.checkouts", &[]).unwrap().get::<_, String>(0),
        "failed"
    );
    let sub_row = db
        .query_one("SELECT \"Status\",\"AttemptCount\" FROM public.subscriptions", &[])
        .unwrap();
    let status: String = sub_row.get(0);
    let attempts: i32 = sub_row.get(1);
    assert_eq!(status, "past_due");
    assert_eq!(attempts, 1);
    // payment.failed was enqueued outbound (the org has an endpoint).
    let deliveries: i64 = db
        .query_one("SELECT count(*) FROM public.org_webhook_deliveries", &[])
        .unwrap()
        .get(0);
    assert_eq!(deliveries, 1);
}

#[test]
fn late_pay_books_exactly_one_pending_refund_across_events() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let checkout = support::insert_charged_checkout(&mut db, "org_1", Decimal::from_str("10.00").unwrap());
    // Simulate the expiry sweep having committed before the money arrived.
    db.execute("UPDATE public.checkouts SET \"Status\" = 'expired'", &[]).unwrap();

    for event_id in ["evt_late_1", "evt_late_2", "evt_late_3"] {
        let body = signed_body(event_id, &checkout, 1000, "MYR");
        let outcome = ingest(&app, &mut db, &body, &hmac_hex(&app.config.test_webhook_secret, &body));
        assert!(
            matches!(outcome, IngestOutcome::LateRefunded { .. }),
            "got {outcome:?}"
        );
    }

    // Issue 009: three success events with distinct ids — exactly ONE late_pay refund.
    let refund_row = db
        .query_one(
            "SELECT count(*), max(\"Status\") FROM public.refunds WHERE \"Reason\" = 'late_pay'",
            &[],
        )
        .unwrap();
    let count: i64 = refund_row.get(0);
    let status: String = refund_row.get(1);
    assert_eq!(count, 1);
    assert_eq!(status, "pending");
}
