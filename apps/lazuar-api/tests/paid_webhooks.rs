//! Port of C# paid-path rail HTTP: CHIP join, Billplz unsigned query, Razorpay
//! plink, Xendit paid/settled, Stripe ignore/replay, test-rail mint+webhook.

mod support;

use lazuar_api::rails::billplz_webhook;
use lazuar_api::rails::stripe_webhook;
use support::{
    allow_org, auth_post, auth_put, call, checkout_status_of, docs_count, events_count, owner_one,
    put_chip, put_gateway, seed_checkout, start_pay, TestApp,
};

fn psp_ok(body: &'static str) -> impl Fn(&support::RecordedRequest) -> lazuar_api::transport::OutResponse {
    move |_| lazuar_api::transport::OutResponse {
        status: 200,
        body: body.into(),
    }
}

fn post(app: &TestApp, path: &str, body: &str) -> ureq::Response {
    support::send(ureq::post(&format!("{}{path}", app.base_url)), body)
}

fn post_header(app: &TestApp, path: &str, header: (&str, &str), body: &str) -> ureq::Response {
    support::send(
        ureq::post(&format!("{}{path}", app.base_url)).set(header.0, header.1),
        body,
    )
}

fn stripe_completed(checkout_id: &str, event_id: &str, extras: &str) -> String {
    format!(
        r#"{{"id":"{event_id}","object":"event","api_version":"2024-06-20","created":1700000000,"livemode":false,"pending_webhooks":1,"request":{{"id":null}},"type":"checkout.session.completed","data":{{"object":{{"id":"cs_test_1","object":"checkout.session","mode":"payment","amount_total":1000,"currency":"myr","client_reference_id":"{checkout_id}","payment_status":"paid","status":"complete"{extras}}}}}}}"#
    )
}

fn stripe_sign(app: &TestApp, payload: &str) -> String {
    stripe_webhook::sign_fixture(
        &app.config.stripe_webhook_secret,
        payload,
        chrono::Utc::now().timestamp(),
    )
}

fn put_stripe(app: &TestApp) {
    let put = put_gateway(
        app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
}

fn billplz_form(secret: &str, bill_id: &str, checkout_id: &str, paid: bool, amount: &str) -> String {
    let (paid_flag, state) = if paid {
        ("true", "paid")
    } else {
        ("false", "due")
    };
    let raw = format!(
        "id={bill_id}&paid={paid_flag}&state={state}&paid_amount={amount}&currency=MYR&x_signature=pending&reference_1={checkout_id}"
    );
    let fields = billplz_webhook::parse_form(&raw);
    let mac = billplz_webhook::compute_hmac(&fields, secret, false);
    format!(
        "id={bill_id}&paid={paid_flag}&state={state}&paid_amount={amount}&currency=MYR&x_signature={mac}&reference_1={checkout_id}"
    )
}

fn put_billplz(app: &TestApp) {
    let put = put_gateway(
        app,
        r#"{"provider":"billplz","secret":"bp_sk","webhook_secret":"xsig","public_merchant_id":"col_1","environment":"test"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
}

fn journal_balanced(app: &TestApp) {
    let mut db = app.pool.get().expect("pool");
    let debit: rust_decimal::Decimal = db
        .query_one(
            "SELECT COALESCE(sum(\"Amount\"),0) FROM public.journal_lines WHERE \"Dc\" = 'D'",
            &[],
        )
        .unwrap()
        .get(0);
    let credit: rust_decimal::Decimal = db
        .query_one(
            "SELECT COALESCE(sum(\"Amount\"),0) FROM public.journal_lines WHERE \"Dc\" = 'C'",
            &[],
        )
        .unwrap()
        .get(0);
    assert_eq!(debit, credit);
}

// ---------------------------------------------------------------------------
// CHIP
// ---------------------------------------------------------------------------

#[test]
fn chip_start_and_paid_webhook() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(psp_ok(
        r#"{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}"#,
    ));
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, checkout_id) = seed_checkout(&app, "chip", None);
    let started = start_pay(&app, &token, r#"{"name":"Ada","email":"ada@acme.test"}"#);
    let started_status = started.status();
    let started_raw = started.into_string().unwrap_or_default();
    assert_eq!(started_status, 200, "{started_raw}");
    let start_doc: serde_json::Value = serde_json::from_str(&started_raw).unwrap();
    assert_eq!(start_doc["redirect_url"], "https://gate.chip-in.asia/p/x");
    let psp_body = app.psp.last().expect("psp").body.unwrap_or_default();
    assert!(!psp_body.contains("force_recurring"), "{psp_body}");
    assert!(psp_body.contains("currency"), "{psp_body}");
    assert!(psp_body.contains("MYR"), "{psp_body}");
    assert!(psp_body.contains("1000"), "{psp_body}");
    assert!(psp_body.contains("checkout_id"), "{psp_body}");
    assert!(psp_body.contains("org_id"), "{psp_body}");

    let payload = format!(
        r#"{{"event_type":"purchase.paid","id":"purch_1","purchase":{{"id":"purch_1","total":1000,"currency":"MYR","metadata":{{"checkout_id":"{checkout_id}","org_id":"t1"}}}}}}"#
    );
    let sig = support::chip_signer().sign(&payload);
    let paid = post_header(&app, "/v1/webhooks/chip/t1", ("X-Signature", &sig), &payload);
    assert_eq!(paid.status(), 200, "{}", paid.into_string().unwrap_or_default());
    let mut db = app.pool.get().expect("pool");
    let provider: String = db
        .query_one("SELECT \"Provider\" FROM public.checkouts", &[])
        .unwrap()
        .get(0);
    let session: String = db
        .query_one("SELECT \"ProviderSessionId\" FROM public.checkouts", &[])
        .unwrap()
        .get(0);
    let number: String = db
        .query_one("SELECT \"Number\" FROM public.documents", &[])
        .unwrap()
        .get(0);
    drop(db);
    assert_eq!(provider, "chip");
    assert_eq!(session, "purch_1");
    assert_eq!(docs_count(&app), 1);
    assert!(number.starts_with("RCPT-"), "{number}");
    journal_balanced(&app);

    let replay = post_header(&app, "/v1/webhooks/chip/t1", ("X-Signature", &sig), &payload);
    let replay_body = replay.into_string().unwrap_or_default();
    assert!(replay_body.contains("duplicate"), "{replay_body}");
    assert_eq!(docs_count(&app), 1);
}

#[test]
fn chip_paid_without_metadata_joins_on_purchase_id() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(psp_ok(
        r#"{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}"#,
    ));
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, _) = seed_checkout(&app, "chip", None);
    assert_eq!(
        start_pay(&app, &token, r#"{"name":"Ada","email":"ada@acme.test"}"#).status(),
        200
    );
    let payload = r#"{"event_type":"purchase.paid","id":"purch_1","purchase":{"id":"purch_1","total":1000,"currency":"MYR"}}"#;
    let sig = support::chip_signer().sign(payload);
    let paid = post_header(&app, "/v1/webhooks/chip/t1", ("X-Signature", &sig), payload);
    assert_eq!(paid.status(), 200, "{}", paid.into_string().unwrap_or_default());
    assert_eq!(docs_count(&app), 1);
    let mut db = app.pool.get().expect("pool");
    let status: String = db
        .query_one("SELECT \"Status\" FROM public.checkouts", &[])
        .unwrap()
        .get(0);
    assert_eq!(status, "paid");
}

#[test]
fn chip_preauthorized_is_ignored() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (_, checkout_id) = seed_checkout(&app, "chip", None);
    let payload = format!(
        r#"{{"event_type":"purchase.preauthorized","id":"purch_1","purchase":{{"id":"purch_1","total":0,"currency":"MYR","metadata":{{"checkout_id":"{checkout_id}"}}}},"recurring_token":"tok"}}"#
    );
    let sig = support::chip_signer().sign(&payload);
    let resp = post_header(&app, "/v1/webhooks/chip/t1", ("X-Signature", &sig), &payload);
    assert_eq!(resp.status(), 200);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("preauthorized"), "{body}");
    assert_eq!(docs_count(&app), 0);
}

#[test]
fn chip_start_without_email_is_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, _) = seed_checkout(&app, "chip", None);
    let resp = start_pay(&app, &token, r#"{"name":"Ada"}"#);
    assert_eq!(resp.status(), 400);
}

#[test]
fn chip_empty_body_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let resp = post(&app, "/v1/webhooks/chip/t1", "  ");
    assert_eq!(resp.status(), 400);
}

#[test]
fn chip_amount_mismatch_does_not_consume_event() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(psp_ok(
        r#"{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}"#,
    ));
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, checkout_id) = seed_checkout(&app, "chip", None);
    assert_eq!(
        start_pay(&app, &token, r#"{"name":"Ada","email":"ada@acme.test"}"#).status(),
        200
    );
    let payload = format!(
        r#"{{"event_type":"purchase.paid","id":"purch_1","purchase":{{"id":"purch_1","total":10,"currency":"MYR","metadata":{{"checkout_id":"{checkout_id}"}}}}}}"#
    );
    let sig = support::chip_signer().sign(&payload);
    let resp = post_header(&app, "/v1/webhooks/chip/t1", ("X-Signature", &sig), &payload);
    assert_eq!(resp.status(), 400);
    assert_eq!(docs_count(&app), 0);
    assert_eq!(events_count(&app), 0);
    assert_eq!(checkout_status_of(&app, &checkout_id), "open");
}

// ---------------------------------------------------------------------------
// Billplz
// ---------------------------------------------------------------------------

#[test]
fn billplz_paid_form_and_localhost_blocked() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(psp_ok(
        r#"{"id":"bill_1","url":"https://www.billplz-sandbox.com/bills/bill_1"}"#,
    ));
    put_billplz(&app);
    let (token, checkout_id) = seed_checkout(&app, "billplz", None);
    let started = start_pay(&app, &token, r#"{"email":"ada@acme.test"}"#);
    assert!(started.status() < 300, "{}", started.into_string().unwrap_or_default());
    let uri = app.psp.last().expect("psp").url;
    assert!(uri.contains("billplz-sandbox"), "{uri}");

    let form = billplz_form("xsig", "bill_1", &checkout_id, true, "1000");
    let paid = support::send(
        ureq::post(&format!(
            "{}/v1/webhooks/billplz/t1?checkout_id={checkout_id}",
            app.base_url
        ))
        .set("Content-Type", "application/x-www-form-urlencoded"),
        &form,
    );
    assert_eq!(paid.status(), 200, "{}", paid.into_string().unwrap_or_default());
    assert_eq!(docs_count(&app), 1);
    let mut db = app.pool.get().expect("pool");
    let number: String = db
        .query_one("SELECT \"Number\" FROM public.documents", &[])
        .unwrap()
        .get(0);
    assert!(number.starts_with("RCPT-"), "{number}");
    drop(db);

    let replay = support::send(
        ureq::post(&format!(
            "{}/v1/webhooks/billplz/t1?checkout_id={checkout_id}",
            app.base_url
        ))
        .set("Content-Type", "application/x-www-form-urlencoded"),
        &form,
    );
    let replay_body = replay.into_string().unwrap_or_default();
    assert!(replay_body.contains("duplicate"), "{replay_body}");
    assert_eq!(docs_count(&app), 1);
}

#[test]
fn billplz_unsigned_query_cannot_redirect_binding() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(psp_ok(
        r#"{"id":"bill_1","url":"https://www.billplz-sandbox.com/bills/bill_1"}"#,
    ));
    put_billplz(&app);
    let (token_a, checkout_a) = seed_checkout(&app, "billplz", None);
    let (_, checkout_b) = seed_checkout(&app, "billplz", None);
    assert!(
        start_pay(&app, &token_a, r#"{"email":"ada@acme.test"}"#).status() < 300
    );
    let form = billplz_form("xsig", "bill_1", &checkout_a, true, "1000");
    let paid = support::send(
        ureq::post(&format!(
            "{}/v1/webhooks/billplz/t1?checkout_id={checkout_b}",
            app.base_url
        ))
        .set("Content-Type", "application/x-www-form-urlencoded"),
        &form,
    );
    assert_eq!(paid.status(), 200, "{}", paid.into_string().unwrap_or_default());
    let mut db = app.pool.get().expect("pool");
    let doc_checkout: String = db
        .query_one("SELECT \"CheckoutId\" FROM public.documents", &[])
        .unwrap()
        .get(0);
    assert_eq!(doc_checkout, checkout_a);
    drop(db);
    assert_eq!(checkout_status_of(&app, &checkout_a), "paid");
    assert_eq!(checkout_status_of(&app, &checkout_b), "open");
    let charges: i64 = app
        .pool
        .get()
        .expect("pool")
        .query_one("SELECT count(*) FROM public.charges", &[])
        .unwrap()
        .get(0);
    assert_eq!(charges, 1);
}

#[test]
fn billplz_localhost_callback_start_is_400_without_psp_http() {
    let app = TestApp::spawn_with(|mut c| {
        c.public_base_url = "http://localhost:8081".into();
        c
    });
    owner_one(&app);
    put_billplz(&app);
    let (token, _) = seed_checkout(&app, "billplz", None);
    let resp = start_pay(&app, &token, r#"{"email":"ada@acme.test"}"#);
    assert_eq!(resp.status(), 400);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("callback base not public"), "{body}");
    assert_eq!(app.psp.send_count(), 0);
}

#[test]
fn billplz_unpaid_is_ignored() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(psp_ok(
        r#"{"id":"bill_1","url":"https://www.billplz-sandbox.com/bills/bill_1"}"#,
    ));
    put_billplz(&app);
    let (token, checkout_id) = seed_checkout(&app, "billplz", None);
    assert!(
        start_pay(&app, &token, r#"{"email":"ada@acme.test"}"#).status() < 300
    );
    let form = billplz_form("xsig", "bill_u", &checkout_id, false, "0");
    let resp = support::send(
        ureq::post(&format!(
            "{}/v1/webhooks/billplz/t1?checkout_id={checkout_id}",
            app.base_url
        ))
        .set("Content-Type", "application/x-www-form-urlencoded"),
        &form,
    );
    assert_eq!(resp.status(), 200);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("unpaid"), "{body}");
    assert_eq!(docs_count(&app), 0);
}

#[test]
fn billplz_late_pay_stays_pending_when_rail_cannot_refund() {
    let app = TestApp::spawn();
    owner_one(&app);
    put_billplz(&app);
    let (_, checkout_id) = seed_checkout(&app, "billplz", None);
    app.pool
        .get()
        .expect("pool")
        .execute(
            "UPDATE public.checkouts SET \"Status\" = 'expired' WHERE \"Id\" = $1",
            &[&checkout_id],
        )
        .unwrap();
    let form = billplz_form("xsig", "bill_late", &checkout_id, true, "1000");
    let resp = support::send(
        ureq::post(&format!("{}/v1/webhooks/billplz/t1", app.base_url))
            .set("Content-Type", "application/x-www-form-urlencoded"),
        &form,
    );
    let status = resp.status();
    let body = resp.into_string().unwrap_or_default();
    assert_eq!(status, 200, "{body}");
    assert!(body.contains("\"refunded\":false"), "{body}");
    let mut db = app.pool.get().expect("pool");
    let reason: String = db
        .query_one("SELECT \"Reason\" FROM public.refunds", &[])
        .unwrap()
        .get(0);
    let refund_status: String = db
        .query_one("SELECT \"Status\" FROM public.refunds", &[])
        .unwrap()
        .get(0);
    let charges: i64 = db.query_one("SELECT count(*) FROM public.charges", &[]).unwrap().get(0);
    drop(db);
    assert_eq!(reason, "late_pay");
    assert_eq!(refund_status, "pending");
    assert_eq!(docs_count(&app), 0);
    assert_eq!(charges, 0);
}

#[test]
fn billplz_refund_fails_and_releases_the_reservation() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(psp_ok(
        r#"{"id":"bill_1","url":"https://www.billplz-sandbox.com/bills/bill_1"}"#,
    ));
    put_billplz(&app);
    let (token, checkout_id) = seed_checkout(&app, "billplz", None);
    assert!(
        start_pay(&app, &token, r#"{"email":"ada@acme.test"}"#).status() < 300
    );
    let form = billplz_form("xsig", "bill_1", &checkout_id, true, "1000");
    let paid = support::send(
        ureq::post(&format!("{}/v1/webhooks/billplz/t1", app.base_url))
            .set("Content-Type", "application/x-www-form-urlencoded"),
        &form,
    );
    assert!(paid.status() < 300, "{}", paid.into_string().unwrap_or_default());

    let refund_body = format!(r#"{{"checkout_id":"{checkout_id}"}}"#);
    let first = auth_post(&app, "/v1/orgs/t1/refunds", &refund_body);
    let first_status = first.status();
    let first_body = first.into_string().unwrap_or_default();
    assert_eq!(first_status, 400, "{first_body}");
    assert!(first_body.contains("refund not supported"), "{first_body}");
    let second = auth_post(&app, "/v1/orgs/t1/refunds", &refund_body);
    assert_eq!(second.status(), 400);
    let mut db = app.pool.get().expect("pool");
    let refunds: i64 = db.query_one("SELECT count(*) FROM public.refunds", &[]).unwrap().get(0);
    let failed: i64 = db
        .query_one("SELECT count(*) FROM public.refunds WHERE \"Status\" = 'failed'", &[])
        .unwrap()
        .get(0);
    let charge_status: String = db
        .query_one("SELECT \"Status\" FROM public.charges", &[])
        .unwrap()
        .get(0);
    assert_eq!(refunds, 2);
    assert_eq!(failed, 2);
    assert_eq!(charge_status, "paid");
}

#[test]
fn billplz_empty_body_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    put_billplz(&app);
    let resp = support::send(
        ureq::post(&format!("{}/v1/webhooks/billplz/t1", app.base_url))
            .set("Content-Type", "application/x-www-form-urlencoded"),
        "  ",
    );
    assert_eq!(resp.status(), 400);
}

#[test]
fn billplz_amount_mismatch_does_not_consume_event() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(psp_ok(
        r#"{"id":"bill_1","url":"https://www.billplz-sandbox.com/bills/bill_1"}"#,
    ));
    put_billplz(&app);
    let (token, checkout_id) = seed_checkout(&app, "billplz", None);
    assert!(
        start_pay(&app, &token, r#"{"email":"ada@acme.test"}"#).status() < 300
    );
    let form = billplz_form("xsig", "bill_1", &checkout_id, true, "10");
    let resp = support::send(
        ureq::post(&format!(
            "{}/v1/webhooks/billplz/t1?checkout_id={checkout_id}",
            app.base_url
        ))
        .set("Content-Type", "application/x-www-form-urlencoded"),
        &form,
    );
    assert_eq!(resp.status(), 400);
    assert_eq!(docs_count(&app), 0);
    assert_eq!(events_count(&app), 0);
    assert_eq!(checkout_status_of(&app, &checkout_id), "open");
}

// ---------------------------------------------------------------------------
// Razorpay
// ---------------------------------------------------------------------------

fn put_razorpay(app: &TestApp) {
    let put = put_gateway(
        app,
        r#"{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh_rzp"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
}

fn razorpay_sig(body: &str) -> String {
    lazuar_api::rails::test_webhook::test_hmac_hex("wh_rzp", body)
}

#[test]
fn razorpay_captured() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(psp_ok(r#"{"id":"plink_1","short_url":"https://rzp.io/i/x"}"#));
    put_razorpay(&app);
    let (token, checkout_id) = seed_checkout(&app, "razorpay", Some("INR"));
    let started = start_pay(&app, &token, r#"{"email":"ada@acme.test"}"#);
    assert!(started.status() < 300, "{}", started.into_string().unwrap_or_default());
    let payload = format!(
        r#"{{"event":"payment.captured","payload":{{"payment":{{"entity":{{"id":"pay_1","amount":1000,"currency":"INR","tax":12,"fee":30,"notes":{{"checkout_id":"{checkout_id}"}}}}}}}}}}"#
    );
    let paid = post_header(
        &app,
        "/v1/webhooks/razorpay/t1",
        ("X-Razorpay-Signature", &razorpay_sig(&payload)),
        &payload,
    );
    assert_eq!(paid.status(), 200, "{}", paid.into_string().unwrap_or_default());
    assert_eq!(docs_count(&app), 1);
    let mut db = app.pool.get().expect("pool");
    let number: String = db
        .query_one("SELECT \"Number\" FROM public.documents", &[])
        .unwrap()
        .get(0);
    let lines: i64 = db
        .query_one("SELECT count(*) FROM public.journal_lines", &[])
        .unwrap()
        .get(0);
    assert!(number.starts_with("RCPT-"), "{number}");
    assert_eq!(lines, 2);
    drop(db);
    journal_balanced(&app);
}

#[test]
fn razorpay_payment_failed_is_ignored() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(psp_ok(r#"{"id":"plink_1","short_url":"https://rzp.io/i/x"}"#));
    put_razorpay(&app);
    let (token, checkout_id) = seed_checkout(&app, "razorpay", Some("INR"));
    let _ = start_pay(&app, &token, r#"{"email":"ada@acme.test"}"#);
    let payload = format!(
        r#"{{"event":"payment.failed","payload":{{"payment":{{"entity":{{"id":"pay_1","amount":1000,"currency":"INR","notes":{{"checkout_id":"{checkout_id}"}}}}}}}}}}"#
    );
    let resp = post_header(
        &app,
        "/v1/webhooks/razorpay/t1",
        ("X-Razorpay-Signature", &razorpay_sig(&payload)),
        &payload,
    );
    assert_eq!(resp.status(), 200);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("failed"), "{body}");
    assert_eq!(docs_count(&app), 0);
}

#[test]
fn razorpay_captured_without_notes_joins_plink() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(psp_ok(r#"{"id":"plink_1","short_url":"https://rzp.io/i/x"}"#));
    put_razorpay(&app);
    let (token, _) = seed_checkout(&app, "razorpay", Some("INR"));
    let _ = start_pay(&app, &token, r#"{"email":"ada@acme.test"}"#);
    let payload = r#"{"event":"payment.captured","payload":{"payment":{"entity":{"id":"pay_1","amount":1000,"currency":"INR"}},"payment_link":{"entity":{"id":"plink_1"}}}}"#;
    let paid = post_header(
        &app,
        "/v1/webhooks/razorpay/t1",
        ("X-Razorpay-Signature", &razorpay_sig(payload)),
        payload,
    );
    assert_eq!(paid.status(), 200, "{}", paid.into_string().unwrap_or_default());
    assert_eq!(docs_count(&app), 1);
}

// ---------------------------------------------------------------------------
// Xendit
// ---------------------------------------------------------------------------

#[test]
fn xendit_paid_and_settled() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(psp_ok(
        r#"{"id":"inv_1","invoice_url":"https://checkout.xendit.co/inv_1"}"#,
    ));
    let put = put_gateway(
        &app,
        r#"{"provider":"xendit","secret":"xnd_sk","webhook_secret":"tok_1"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, checkout_id) = seed_checkout(&app, "xendit", None);
    let started = start_pay(&app, &token, r#"{"email":"ada@acme.test"}"#);
    assert!(started.status() < 300, "{}", started.into_string().unwrap_or_default());
    let payload = format!(
        r#"{{"id":"inv_1","status":"PAID","currency":"MYR","paid_amount":10,"metadata":{{"checkout_id":"{checkout_id}"}}}}"#
    );
    let paid = post_header(
        &app,
        "/v1/webhooks/xendit/t1",
        ("x-callback-token", "tok_1"),
        &payload,
    );
    assert!(paid.status() < 300, "{}", paid.into_string().unwrap_or_default());
    let settled = format!(
        r#"{{"id":"inv_1","status":"SETTLED","currency":"MYR","paid_amount":10,"metadata":{{"checkout_id":"{checkout_id}"}}}}"#
    );
    let second = post_header(
        &app,
        "/v1/webhooks/xendit/t1",
        ("x-callback-token", "tok_1"),
        &settled,
    );
    assert_eq!(second.status(), 200);
    let second_body = second.into_string().unwrap_or_default();
    assert!(
        second_body.contains("settled") || second_body.contains("ignored"),
        "{second_body}"
    );
    assert_eq!(docs_count(&app), 1);
    let mut db = app.pool.get().expect("pool");
    let number: String = db
        .query_one("SELECT \"Number\" FROM public.documents", &[])
        .unwrap()
        .get(0);
    let status: String = db
        .query_one("SELECT \"Status\" FROM public.checkouts", &[])
        .unwrap()
        .get(0);
    assert!(number.starts_with("RCPT-"), "{number}");
    assert_eq!(status, "paid");
}

// ---------------------------------------------------------------------------
// Stripe HTTP
// ---------------------------------------------------------------------------

#[test]
fn missing_webhook_secret_is_503_when_rail_configured() {
    let app = TestApp::spawn_with(|mut c| {
        c.stripe_webhook_secret.clear();
        c
    });
    owner_one(&app);
    put_stripe(&app);
    let _ = seed_checkout(&app, "stripe", None);
    app.pool
        .get()
        .expect("pool")
        .execute(
            "UPDATE public.gateway_credentials SET \"WebhookCiphertext\" = NULL WHERE \"OrgId\" = 't1' AND \"Provider\" = 'stripe'",
            &[],
        )
        .unwrap();
    let resp = post(&app, "/v1/webhooks/stripe/t1", r#"{"id":"evt_x"}"#);
    assert_eq!(resp.status(), 503);
}

#[test]
fn invalid_signature_is_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    put_stripe(&app);
    let _ = seed_checkout(&app, "stripe", None);
    let resp = post_header(
        &app,
        "/v1/webhooks/stripe/t1",
        ("Stripe-Signature", "t=1,v1=deadbeef"),
        r#"{"id":"evt_x","type":"checkout.session.completed"}"#,
    );
    assert_eq!(resp.status(), 400);
}

#[test]
fn completed_session_writes_receipt_and_replay_is_noop() {
    let app = TestApp::spawn();
    owner_one(&app);
    put_stripe(&app);
    let (_, checkout_id) = seed_checkout(&app, "stripe", None);
    let event_id = format!("evt_test_{}", uuid::Uuid::new_v4().simple());
    let payload = stripe_completed(
        &checkout_id,
        &event_id,
        &format!(r#","metadata":{{"checkout_id":"{checkout_id}","org_id":"t1"}}"#),
    );
    let sig = stripe_sign(&app, &payload);
    let first = post_header(&app, "/v1/webhooks/stripe/t1", ("Stripe-Signature", &sig), &payload);
    let first_status = first.status();
    let first_body = first.into_string().unwrap_or_default();
    assert_eq!(first_status, 200, "{first_body}");
    assert!(!first_body.contains("SST registration unknown"), "{first_body}");
    assert_eq!(docs_count(&app), 1);
    let mut db = app.pool.get().expect("pool");
    let number: String = db
        .query_one("SELECT \"Number\" FROM public.documents", &[])
        .unwrap()
        .get(0);
    let title: String = db
        .query_one("SELECT \"Title\" FROM public.documents", &[])
        .unwrap()
        .get(0);
    let sst: Option<bool> = db
        .query_one("SELECT \"SstRegistered\" FROM public.org_settings WHERE \"OrgId\" = 't1'", &[])
        .unwrap()
        .get(0);
    drop(db);
    assert!(number.starts_with("RCPT-"), "{number}");
    assert_eq!(title, "Official Receipt");
    assert_eq!(checkout_status_of(&app, &checkout_id), "paid");
    assert!(sst.is_none());
    journal_balanced(&app);

    let replay = post_header(&app, "/v1/webhooks/stripe/t1", ("Stripe-Signature", &sig), &payload);
    assert_eq!(replay.status(), 200);
    let replay_body = replay.into_string().unwrap_or_default();
    assert!(replay_body.contains("duplicate"), "{replay_body}");
    assert_eq!(docs_count(&app), 1);
}

#[test]
fn setup_mode_is_ignored() {
    let app = TestApp::spawn();
    owner_one(&app);
    put_stripe(&app);
    let (_, checkout_id) = seed_checkout(&app, "stripe", None);
    let event_id = format!("evt_setup_{}", uuid::Uuid::new_v4().simple());
    let payload = format!(
        r#"{{"id":"{event_id}","object":"event","api_version":"2024-06-20","created":1700000000,"livemode":false,"pending_webhooks":1,"request":{{"id":null}},"type":"checkout.session.completed","data":{{"object":{{"id":"cs_setup","object":"checkout.session","mode":"setup","amount_total":0,"currency":"myr","client_reference_id":"{checkout_id}","payment_status":"unpaid","status":"complete"}}}}}}"#
    );
    let sig = stripe_sign(&app, &payload);
    let resp = post_header(&app, "/v1/webhooks/stripe/t1", ("Stripe-Signature", &sig), &payload);
    let status = resp.status();
    let body = resp.into_string().unwrap_or_default();
    assert_eq!(status, 200, "{body}");
    assert!(body.contains("ignored"), "{body}");
    assert!(body.contains("setup"), "{body}");
    assert_eq!(docs_count(&app), 0);
    assert_eq!(checkout_status_of(&app, &checkout_id), "open");
}

#[test]
fn zero_amount_session_is_ignored() {
    let app = TestApp::spawn();
    owner_one(&app);
    put_stripe(&app);
    let (_, checkout_id) = seed_checkout(&app, "stripe", None);
    let event_id = format!("evt_zero_{}", uuid::Uuid::new_v4().simple());
    let payload = format!(
        r#"{{"id":"{event_id}","object":"event","api_version":"2024-06-20","created":1700000000,"livemode":false,"pending_webhooks":1,"request":{{"id":null}},"type":"checkout.session.completed","data":{{"object":{{"id":"cs_zero","object":"checkout.session","mode":"payment","amount_total":0,"currency":"myr","client_reference_id":"{checkout_id}","payment_status":"paid","status":"complete"}}}}}}"#
    );
    let sig = stripe_sign(&app, &payload);
    let resp = post_header(&app, "/v1/webhooks/stripe/t1", ("Stripe-Signature", &sig), &payload);
    assert_eq!(resp.status(), 200);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("ignored"), "{body}");
    assert_eq!(docs_count(&app), 0);
    assert_eq!(checkout_status_of(&app, &checkout_id), "open");
}

#[test]
fn cross_org_checkout_is_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    put_stripe(&app);
    let (_, checkout_id) = seed_checkout(&app, "stripe", None);
    allow_org(&app, "t2");
    let keys2 = auth_put(
        &app,
        "/v1/orgs/t2/gateway",
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}"#,
    );
    assert!(keys2.status() < 300, "{}", keys2.into_string().unwrap_or_default());
    owner_one(&app);
    let event_id = format!("evt_xorg_{}", uuid::Uuid::new_v4().simple());
    let payload = stripe_completed(&checkout_id, &event_id, "");
    let sig = stripe_sign(&app, &payload);
    let resp = post_header(
        &app,
        "/v1/webhooks/stripe/t2",
        ("Stripe-Signature", &sig),
        &payload,
    );
    assert_eq!(resp.status(), 400);
    assert_eq!(docs_count(&app), 0);
}

#[test]
fn unknown_provider_is_400() {
    let app = TestApp::spawn();
    let resp = post(&app, "/v1/webhooks/paypal/t1", r#"{"id":"x"}"#);
    assert_eq!(resp.status(), 400);
}

#[test]
fn paused_org_does_not_mint_receipt() {
    let app = TestApp::spawn();
    owner_one(&app);
    put_stripe(&app);
    let (_, checkout_id) = seed_checkout(&app, "stripe", None);
    app.pool
        .get()
        .expect("pool")
        .execute(
            "UPDATE public.org_settings SET \"ChargesPaused\" = TRUE WHERE \"OrgId\" = 't1'",
            &[],
        )
        .unwrap();
    let event_id = format!("evt_paused_{}", uuid::Uuid::new_v4().simple());
    let payload = stripe_completed(
        &checkout_id,
        &event_id,
        &format!(r#","metadata":{{"checkout_id":"{checkout_id}","org_id":"t1"}}"#),
    );
    let sig = stripe_sign(&app, &payload);
    let resp = post_header(&app, "/v1/webhooks/stripe/t1", ("Stripe-Signature", &sig), &payload);
    assert_eq!(resp.status(), 409, "{}", resp.into_string().unwrap_or_default());
    assert_eq!(docs_count(&app), 0);
    assert_eq!(checkout_status_of(&app, &checkout_id), "open");
    let paused_events: i64 = app
        .pool
        .get()
        .expect("pool")
        .query_one(
            "SELECT count(*) FROM public.psp_webhook_events WHERE \"EventId\" = $1",
            &[&event_id],
        )
        .unwrap()
        .get(0);
    assert_eq!(paused_events, 0);
    app.pool
        .get()
        .expect("pool")
        .execute(
            "UPDATE public.org_settings SET \"ChargesPaused\" = FALSE WHERE \"OrgId\" = 't1'",
            &[],
        )
        .unwrap();
    let paid = post_header(&app, "/v1/webhooks/stripe/t1", ("Stripe-Signature", &sig), &payload);
    assert_eq!(paid.status(), 200, "{}", paid.into_string().unwrap_or_default());
    assert_eq!(docs_count(&app), 1);
}

#[test]
fn unpaid_completed_session_is_ignored() {
    let app = TestApp::spawn();
    owner_one(&app);
    put_stripe(&app);
    let (_, checkout_id) = seed_checkout(&app, "stripe", None);
    let event_id = format!("evt_unpaid_{}", uuid::Uuid::new_v4().simple());
    let payload = format!(
        r#"{{"id":"{event_id}","object":"event","api_version":"2024-06-20","created":1700000000,"livemode":false,"pending_webhooks":1,"request":{{"id":null}},"type":"checkout.session.completed","data":{{"object":{{"id":"cs_unpaid","object":"checkout.session","mode":"payment","amount_total":1000,"currency":"myr","client_reference_id":"{checkout_id}","payment_status":"unpaid","status":"complete"}}}}}}"#
    );
    let sig = stripe_sign(&app, &payload);
    let resp = post_header(&app, "/v1/webhooks/stripe/t1", ("Stripe-Signature", &sig), &payload);
    let status = resp.status();
    let body = resp.into_string().unwrap_or_default();
    assert_eq!(status, 200, "{body}");
    assert!(body.contains("ignored"), "{body}");
    assert_eq!(docs_count(&app), 0);
    assert_eq!(checkout_status_of(&app, &checkout_id), "open");
}

#[test]
fn async_payment_succeeded_pays_after_unpaid_completed() {
    let app = TestApp::spawn();
    owner_one(&app);
    put_stripe(&app);
    let (_, checkout_id) = seed_checkout(&app, "stripe", None);
    let unpaid_id = format!("evt_unpaid2_{}", uuid::Uuid::new_v4().simple());
    let unpaid = format!(
        r#"{{"id":"{unpaid_id}","object":"event","api_version":"2024-06-20","created":1700000000,"livemode":false,"pending_webhooks":1,"request":{{"id":null}},"type":"checkout.session.completed","data":{{"object":{{"id":"cs_async","object":"checkout.session","mode":"payment","amount_total":1000,"currency":"myr","client_reference_id":"{checkout_id}","payment_status":"unpaid","status":"complete"}}}}}}"#
    );
    let t = chrono::Utc::now().timestamp();
    let unpaid_sig = stripe_webhook::sign_fixture(&app.config.stripe_webhook_secret, &unpaid, t);
    assert_eq!(
        post_header(
            &app,
            "/v1/webhooks/stripe/t1",
            ("Stripe-Signature", &unpaid_sig),
            &unpaid
        )
        .status(),
        200
    );
    let paid_id = format!("evt_async_{}", uuid::Uuid::new_v4().simple());
    let paid = format!(
        r#"{{"id":"{paid_id}","object":"event","api_version":"2024-06-20","created":1700000000,"livemode":false,"pending_webhooks":1,"request":{{"id":null}},"type":"checkout.session.async_payment_succeeded","data":{{"object":{{"id":"cs_async","object":"checkout.session","mode":"payment","amount_total":1000,"currency":"myr","client_reference_id":"{checkout_id}","payment_status":"paid","status":"complete"}}}}}}"#
    );
    let paid_sig = stripe_webhook::sign_fixture(&app.config.stripe_webhook_secret, &paid, t);
    let resp = post_header(
        &app,
        "/v1/webhooks/stripe/t1",
        ("Stripe-Signature", &paid_sig),
        &paid,
    );
    assert_eq!(resp.status(), 200, "{}", resp.into_string().unwrap_or_default());
    assert_eq!(docs_count(&app), 1);
    assert_eq!(checkout_status_of(&app, &checkout_id), "paid");
}

// ---------------------------------------------------------------------------
// Test rail HTTP
// ---------------------------------------------------------------------------

fn signed_test(app: &TestApp, body: &str) -> ureq::Response {
    let mac = lazuar_api::rails::test_webhook::test_hmac_hex(&app.config.test_webhook_secret, body);
    post_header(&app, "/v1/webhooks/test/t1", ("X-Pay-Test-Signature", &mac), body)
}

#[test]
fn mint_and_start_pays_without_keys() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, checkout_id) = seed_checkout(&app, "test", None);
    let started = start_pay(&app, &token, r#"{"name":"Ada"}"#);
    let started_status = started.status();
    let started_raw = started.into_string().unwrap_or_default();
    assert_eq!(started_status, 200, "{started_raw}");
    let start_doc: serde_json::Value = serde_json::from_str(&started_raw).unwrap();
    let redirect = start_doc["redirect_url"].as_str().unwrap_or("");
    assert!(redirect.contains("status=verifying"), "{redirect}");
    let get = call(ureq::get(&format!("{}/v1/pay/{token}", app.base_url)));
    let pay: serde_json::Value = get.into_json().unwrap();
    assert_eq!(pay["status"], "paid");
    assert_eq!(pay["provider"], "test");
    assert_eq!(checkout_status_of(&app, &checkout_id), "paid");
    assert_eq!(docs_count(&app), 1);
    let title: String = app
        .pool
        .get()
        .expect("pool")
        .query_one("SELECT \"Title\" FROM public.documents", &[])
        .unwrap()
        .get(0);
    assert_eq!(title, "Official Receipt");
    assert_eq!(app.psp.send_count(), 0);
}

#[test]
fn webhook_pays_open_test_checkout() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (_, checkout_id) = seed_checkout(&app, "test", None);
    let body = format!(
        r#"{{"id":"evt_test_1","checkout_id":"{checkout_id}","amount_total":1000,"currency":"myr"}}"#
    );
    let resp = signed_test(&app, &body);
    assert_eq!(resp.status(), 200, "{}", resp.into_string().unwrap_or_default());
    assert_eq!(docs_count(&app), 1);
}

#[test]
fn unsigned_test_webhook_is_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (_, checkout_id) = seed_checkout(&app, "test", None);
    let resp = post(
        &app,
        "/v1/webhooks/test/t1",
        &format!(
            r#"{{"id":"evt_unsigned","checkout_id":"{checkout_id}","amount_total":1000,"currency":"myr"}}"#
        ),
    );
    assert_eq!(resp.status(), 400);
    assert_eq!(docs_count(&app), 0);
}

#[test]
fn test_webhook_without_amount_is_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (_, checkout_id) = seed_checkout(&app, "test", None);
    let resp = signed_test(
        &app,
        &format!(r#"{{"id":"evt_omit","checkout_id":"{checkout_id}","currency":"myr"}}"#),
    );
    assert_eq!(resp.status(), 400);
    assert_eq!(docs_count(&app), 0);
}

#[test]
fn test_webhook_wrong_amount_does_not_consume_event() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (_, checkout_id) = seed_checkout(&app, "test", None);
    let resp = signed_test(
        &app,
        &format!(
            r#"{{"id":"evt_mm","checkout_id":"{checkout_id}","amount_total":10,"currency":"myr"}}"#
        ),
    );
    assert_eq!(resp.status(), 400);
    assert_eq!(docs_count(&app), 0);
    assert_eq!(events_count(&app), 0);
}

#[test]
fn test_webhook_without_id_is_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (_, checkout_id) = seed_checkout(&app, "test", None);
    let resp = signed_test(
        &app,
        &format!(r#"{{"checkout_id":"{checkout_id}","amount_total":1000,"currency":"myr"}}"#),
    );
    assert_eq!(resp.status(), 400);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("event id"), "{body}");
}

#[test]
fn test_webhook_replay_same_id_is_duplicate() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (_, checkout_id) = seed_checkout(&app, "test", None);
    let body = format!(
        r#"{{"id":"evt_dup","checkout_id":"{checkout_id}","amount_total":1000,"currency":"myr"}}"#
    );
    assert_eq!(signed_test(&app, &body).status(), 200);
    let second = signed_test(&app, &body);
    let second_body = second.into_string().unwrap_or_default();
    assert!(second_body.contains("duplicate"), "{second_body}");
    assert_eq!(docs_count(&app), 1);
}
