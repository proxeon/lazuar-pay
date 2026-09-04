//! Port of C# `FillTests` / `FailedAndExpiredTests` / leftover `PostgresTxTests`
//! through `TestApp` HTTP (and one library concurrent-fulfill that C# also
//! calls on `IFulfillPaid` rather than the webhook route).

mod support;

use lazuar_api::money::fulfillment::fulfill_paid;
use lazuar_api::rails::billplz_webhook;
use lazuar_api::rails::stripe_webhook;
use lazuar_api::rails::test_webhook;
use rust_decimal::Decimal;
use support::{
    auth_get, auth_put, call, checkout_status_of, docs_count, events_count, owner_one, put_chip,
    put_gateway, seed_checkout, seed_payment_link, start_pay, TestApp,
};

fn put_stripe(app: &TestApp) {
    let put = put_gateway(
        app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
}

fn put_hook(app: &TestApp) {
    let put = auth_put(app, "/v1/orgs/t1/webhooks", r#"{"url":"http://127.0.0.1:9/hook"}"#);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
}

fn put_billplz(app: &TestApp) {
    let put = put_gateway(
        app,
        r#"{"provider":"billplz","secret":"bp_sk","webhook_secret":"xsig","public_merchant_id":"col_1","environment":"test"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
}

fn stripe_paid(event_id: &str, checkout_id: &str, extra: &str) -> String {
    format!(
        r#"{{"id":"{event_id}","object":"event","api_version":"2024-06-20","created":1700000000,"livemode":false,"pending_webhooks":1,"request":{{"id":null}},"type":"checkout.session.completed","data":{{"object":{{"id":"cs_x","object":"checkout.session","mode":"payment",{extra},"client_reference_id":"{checkout_id}","payment_status":"paid","status":"complete","metadata":{{"checkout_id":"{checkout_id}"}}}}}}}}"#
    )
}

fn stripe_sign(app: &TestApp, payload: &str) -> String {
    stripe_webhook::sign_fixture(
        &app.config.stripe_webhook_secret,
        payload,
        chrono::Utc::now().timestamp(),
    )
}

fn post_stripe(app: &TestApp, payload: &str, sig: &str) -> ureq::Response {
    support::send(
        ureq::post(&format!("{}/v1/webhooks/stripe/t1", app.base_url)).set("Stripe-Signature", sig),
        payload,
    )
}

fn post_test(app: &TestApp, body: &str) -> ureq::Response {
    let mac = test_webhook::test_hmac_hex(&app.config.test_webhook_secret, body);
    support::send(
        ureq::post(&format!("{}/v1/webhooks/test/t1", app.base_url)).set("X-Pay-Test-Signature", &mac),
        body,
    )
}

fn billplz_form(secret: &str, bill_id: &str, checkout_id: &str) -> String {
    let raw = format!(
        "id={bill_id}&paid=true&state=paid&paid_amount=1000&currency=MYR&x_signature=pending&reference_1={checkout_id}"
    );
    let fields = billplz_webhook::parse_form(&raw);
    let mac = billplz_webhook::compute_hmac(&fields, secret, false);
    format!(
        "id={bill_id}&paid=true&state=paid&paid_amount=1000&currency=MYR&x_signature={mac}&reference_1={checkout_id}"
    )
}

fn post_billplz(app: &TestApp, form: &str) -> ureq::Response {
    support::send(
        ureq::post(&format!("{}/v1/webhooks/billplz/t1", app.base_url))
            .set("Content-Type", "application/x-www-form-urlencoded"),
        form,
    )
}

fn refund(app: &TestApp, checkout_id: &str, idempotency: Option<&str>) -> ureq::Response {
    let mut req = ureq::post(&format!("{}/v1/orgs/t1/refunds", app.base_url))
        .set("Authorization", "Bearer jwt");
    if let Some(key) = idempotency {
        req = req.set("Idempotency-Key", key);
    }
    support::send(req, &format!(r#"{{"checkout_id":"{checkout_id}"}}"#))
}

fn event_count_of(app: &TestApp, event_id: &str) -> i64 {
    app.pool
        .get()
        .expect("pool")
        .query_one(
            "SELECT count(*) FROM public.psp_webhook_events WHERE \"EventId\" = $1",
            &[&event_id],
        )
        .unwrap()
        .get(0)
}

fn charges_count(app: &TestApp) -> i64 {
    app.pool
        .get()
        .expect("pool")
        .query_one("SELECT count(*) FROM public.charges", &[])
        .unwrap()
        .get(0)
}

fn deliveries_count(app: &TestApp) -> i64 {
    app.pool
        .get()
        .expect("pool")
        .query_one("SELECT count(*) FROM public.org_webhook_deliveries", &[])
        .unwrap()
        .get(0)
}

// ---------------------------------------------------------------------------
// FillTests
// ---------------------------------------------------------------------------

#[test]
fn fulfill_throw_returns_5xx_event_not_committed_retry_pays() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.fulfill_gates.arm_throw_next();
    put_stripe(&app);
    let (_, checkout_id) = seed_checkout(&app, "stripe", None);
    let event_id = format!("evt_throw_{}", uuid::Uuid::new_v4().simple());
    let payload = stripe_paid(&event_id, &checkout_id, r#""amount_total":1000,"currency":"myr""#);
    let sig = stripe_sign(&app, &payload);
    let first = post_stripe(&app, &payload, &sig);
    assert!(first.status() >= 500, "{}", first.into_string().unwrap_or_default());
    assert_eq!(docs_count(&app), 0);
    assert_eq!(event_count_of(&app, &event_id), 0);

    let second = post_stripe(&app, &payload, &sig);
    assert_eq!(second.status(), 200, "{}", second.into_string().unwrap_or_default());
    assert_eq!(docs_count(&app), 1);
}

#[test]
fn amount_mismatch_does_not_mint_receipt() {
    let app = TestApp::spawn();
    owner_one(&app);
    put_stripe(&app);
    let (_, checkout_id) = seed_checkout(&app, "stripe", None);
    let event_id = format!("evt_mm_{}", uuid::Uuid::new_v4().simple());
    let payload = stripe_paid(&event_id, &checkout_id, r#""amount_total":999,"currency":"myr""#);
    let sig = stripe_sign(&app, &payload);
    let response = post_stripe(&app, &payload, &sig);
    assert_eq!(response.status(), 400);
    assert_eq!(docs_count(&app), 0);
    assert_eq!(events_count(&app), 0);
    assert_eq!(checkout_status_of(&app, &checkout_id), "open");
}

#[test]
fn currency_mismatch_does_not_mint_receipt() {
    let app = TestApp::spawn();
    owner_one(&app);
    put_stripe(&app);
    let (_, checkout_id) = seed_checkout(&app, "stripe", None);
    let event_id = format!("evt_ccy_{}", uuid::Uuid::new_v4().simple());
    let payload = stripe_paid(&event_id, &checkout_id, r#""amount_total":1000,"currency":"usd""#);
    let sig = stripe_sign(&app, &payload);
    assert_eq!(post_stripe(&app, &payload, &sig).status(), 400);
    assert_eq!(docs_count(&app), 0);
}

#[test]
fn rail_not_configured_is_400_when_body_present() {
    let app = TestApp::spawn();
    let resp = support::send(
        ureq::post(&format!("{}/v1/webhooks/stripe/t1", app.base_url)),
        r#"{"id":"evt_x"}"#,
    );
    assert_eq!(resp.status(), 400);
    assert!(
        resp.into_string()
            .unwrap_or_default()
            .contains("rail not configured")
    );
}

#[test]
fn never_started_checkout_webhook_is_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    put_stripe(&app);
    let (_, checkout_id) = seed_checkout(&app, "stripe", None);
    app.pool
        .get()
        .expect("pool")
        .execute(
            "UPDATE public.checkouts SET \"Provider\" = NULL WHERE \"Id\" = $1",
            &[&checkout_id],
        )
        .unwrap();
    let event_id = format!("evt_nostart_{}", uuid::Uuid::new_v4().simple());
    let payload = stripe_paid(&event_id, &checkout_id, r#""amount_total":1000,"currency":"myr""#);
    let sig = stripe_sign(&app, &payload);
    let response = post_stripe(&app, &payload, &sig);
    assert_eq!(response.status(), 400);
    assert!(
        response
            .into_string()
            .unwrap_or_default()
            .contains("provider mismatch")
    );
}

#[test]
fn empty_webhook_is_400() {
    let app = TestApp::spawn();
    let resp = support::send(
        ureq::post(&format!("{}/v1/webhooks/stripe/t1", app.base_url)),
        "",
    );
    assert_eq!(resp.status(), 400);
}

#[test]
fn concurrent_fulfill_of_one_checkout_mints_one_receipt() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (_, checkout_id) = seed_checkout(&app, "test", None);
    let gates = app.fulfill_gates.clone();
    let pool = app.pool.clone();
    let id_a = checkout_id.clone();
    let id_b = checkout_id.clone();
    std::thread::scope(|scope| {
        let ga = gates.clone();
        let gb = gates.clone();
        let pa = pool.clone();
        let pb = pool.clone();
        let a = scope.spawn(move || {
            let mut conn = pa.get().unwrap();
            let mut tx = conn.transaction().unwrap();
            let result = fulfill_paid(&mut tx, &ga, &id_a, "test", Some("ref-a"));
            tx.commit().unwrap();
            result.unwrap()
        });
        let b = scope.spawn(move || {
            let mut conn = pb.get().unwrap();
            let mut tx = conn.transaction().unwrap();
            let result = fulfill_paid(&mut tx, &gb, &id_b, "test", Some("ref-b"));
            tx.commit().unwrap();
            result.unwrap()
        });
        a.join().unwrap();
        b.join().unwrap();
    });
    assert_eq!(docs_count(&app), 1);
    assert_eq!(charges_count(&app), 1);
    assert_eq!(checkout_status_of(&app, &checkout_id), "paid");
}

#[test]
fn over_capacity_paid_webhook_books_pending_late_refund() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 200,
        body: r#"{"id":"bill_1","url":"https://www.billplz-sandbox.com/bills/bill_1"}"#.into(),
    });
    put_billplz(&app);
    let (link_token, link_id) = seed_payment_link(&app, "billplz", Some(2));
    let a = start_pay(&app, &link_token, r#"{"email":"ada@acme.test","slot_key":"slot-oc-a"}"#);
    let b = start_pay(&app, &link_token, r#"{"email":"bob@acme.test","slot_key":"slot-oc-b"}"#);
    assert!(a.status() < 300 && b.status() < 300);

    let mut db = app.pool.get().expect("pool");
    let checkout_a: String = db
        .query_one(
            "SELECT \"Id\" FROM public.checkouts WHERE \"SlotKey\" = 'slot-oc-a'",
            &[],
        )
        .unwrap()
        .get(0);
    let checkout_b: String = db
        .query_one(
            "SELECT \"Id\" FROM public.checkouts WHERE \"SlotKey\" = 'slot-oc-b'",
            &[],
        )
        .unwrap()
        .get(0);
    drop(db);

    let paid_a = post_billplz(&app, &billplz_form("xsig", "bill_oc_a", &checkout_a));
    assert!(paid_a.status() < 300, "{}", paid_a.into_string().unwrap_or_default());
    app.pool
        .get()
        .expect("pool")
        .execute(
            "UPDATE public.payment_links SET \"MaxPayers\" = 1 WHERE \"Id\" = $1",
            &[&link_id],
        )
        .unwrap();

    let paid_b = post_billplz(&app, &billplz_form("xsig", "bill_oc_b", &checkout_b));
    assert!(paid_b.status() < 300, "{}", paid_b.into_string().unwrap_or_default());

    assert_eq!(checkout_status_of(&app, &checkout_b), "expired");
    let mut db = app.pool.get().expect("pool");
    let refund_status: String = db
        .query_one("SELECT \"Status\" FROM public.refunds", &[])
        .unwrap()
        .get(0);
    let reason: String = db
        .query_one("SELECT \"Reason\" FROM public.refunds", &[])
        .unwrap()
        .get(0);
    let refund_checkout: String = db
        .query_one("SELECT \"CheckoutId\" FROM public.refunds", &[])
        .unwrap()
        .get(0);
    drop(db);
    assert_eq!(refund_status, "pending");
    assert_eq!(reason, "late_pay");
    assert_eq!(refund_checkout, checkout_b);
    assert_eq!(charges_count(&app), 1);
    assert_eq!(docs_count(&app), 1);
}

// ---------------------------------------------------------------------------
// FailedAndExpiredTests
// ---------------------------------------------------------------------------

#[test]
fn test_failed_webhook_persists_and_enqueues() {
    let app = TestApp::spawn();
    owner_one(&app);
    put_hook(&app);
    let (_, checkout_id) = seed_checkout(&app, "test", None);
    let body = format!(
        r#"{{"id":"evt_fail","checkout_id":"{checkout_id}","status":"failed","currency":"myr"}}"#
    );
    let response = post_test(&app, &body);
    let status = response.status();
    let raw = response.into_string().unwrap_or_default();
    assert_eq!(status, 200, "{raw}");
    assert!(raw.contains("failed"), "{raw}");
    assert_eq!(checkout_status_of(&app, &checkout_id), "failed");
    assert_eq!(docs_count(&app), 0);
    let event_type: String = app
        .pool
        .get()
        .expect("pool")
        .query_one("SELECT \"EventType\" FROM public.org_webhook_deliveries", &[])
        .unwrap()
        .get(0);
    assert_eq!(event_type, "payment.failed");
}

#[test]
fn ignored_psp_does_not_emit_failed() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (_, checkout_id) = seed_checkout(&app, "chip", None);
    let payload = format!(
        r#"{{"event_type":"purchase.preauthorized","id":"purch_1","purchase":{{"id":"purch_1","total":0,"currency":"MYR","metadata":{{"checkout_id":"{checkout_id}"}}}}}}"#
    );
    let sig = support::chip_signer().sign(&payload);
    let response = support::send(
        ureq::post(&format!("{}/v1/webhooks/chip/t1", app.base_url)).set("X-Signature", &sig),
        &payload,
    );
    assert!(
        response.into_string().unwrap_or_default().contains("preauthorized")
    );
    assert_eq!(checkout_status_of(&app, &checkout_id), "open");
    assert_eq!(deliveries_count(&app), 0);
}

#[test]
fn stale_reservation_emits_checkout_expired() {
    let app = TestApp::spawn();
    owner_one(&app);
    put_hook(&app);
    let (token, link_id) = seed_payment_link(&app, "test", Some(1));
    let mut db = app.pool.get().expect("pool");
    let amount: Decimal = db
        .query_one(
            "SELECT \"Amount\" FROM public.payment_links WHERE \"Id\" = $1",
            &[&link_id],
        )
        .unwrap()
        .get(0);
    let child_id = uuid::Uuid::new_v4().to_string();
    db.execute(
        "INSERT INTO public.checkouts \
         (\"Id\",\"OrgId\",\"PublicToken\",\"Provider\",\"PaymentLinkId\",\"SlotKey\",\"Amount\",\
         \"Currency\",\"Status\",\"Interval\",\"CreatedAt\") \
         VALUES ($1,'t1',$2,'test',$3,'slot-exp-1',$4,'MYR','open','one_off',$5)",
        &[
            &child_id,
            &uuid::Uuid::new_v4().simple().to_string(),
            &link_id,
            &amount,
            &(chrono::Utc::now() - chrono::Duration::minutes(31)),
        ],
    )
    .unwrap();
    drop(db);

    let get = call(ureq::get(&format!("{}/v1/pay/{token}", app.base_url)));
    assert_eq!(get.status(), 200, "{}", get.into_string().unwrap_or_default());
    assert_eq!(checkout_status_of(&app, &child_id), "expired");
    let event_type: String = app
        .pool
        .get()
        .expect("pool")
        .query_one("SELECT \"EventType\" FROM public.org_webhook_deliveries", &[])
        .unwrap()
        .get(0);
    assert_eq!(event_type, "checkout.expired");
}

// ---------------------------------------------------------------------------
// PostgresTxTests
// ---------------------------------------------------------------------------

#[test]
fn fulfill_save_then_throw_rolls_back_event() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.fulfill_gates.arm_throw_after_save();
    put_stripe(&app);
    let (_, checkout_id) = seed_checkout(&app, "stripe", None);
    let event_id = format!("evt_tx_{}", uuid::Uuid::new_v4().simple());
    let payload = stripe_paid(&event_id, &checkout_id, r#""amount_total":1000,"currency":"myr""#);
    let sig = stripe_sign(&app, &payload);
    let first = post_stripe(&app, &payload, &sig);
    assert!(first.status() >= 500, "{}", first.into_string().unwrap_or_default());
    assert_eq!(docs_count(&app), 0);
    assert_eq!(event_count_of(&app, &event_id), 0);

    let second = post_stripe(&app, &payload, &sig);
    assert_eq!(second.status(), 200, "{}", second.into_string().unwrap_or_default());
    assert_eq!(docs_count(&app), 1);
    assert_eq!(event_count_of(&app, &event_id), 1);
}

#[test]
fn concurrent_starts_on_one_seat_leave_one_open() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, _) = seed_payment_link(&app, "test", Some(1));
    let base = app.base_url.clone();
    let (first, second) = std::thread::scope(|scope| {
        let a = scope.spawn(|| {
            support::start_pay_at(&base, &token, r#"{"name":"Ada","slot_key":"slot-pg-a"}"#).status()
        });
        let b = scope.spawn(|| {
            support::start_pay_at(&base, &token, r#"{"name":"Ada","slot_key":"slot-pg-b"}"#).status()
        });
        (a.join().unwrap(), b.join().unwrap())
    });
    let codes = [first, second];
    assert_eq!(codes.iter().filter(|c| **c == 200).count(), 1, "got {codes:?}");
    assert_eq!(
        codes.iter().filter(|c| **c >= 400 && **c < 500).count(),
        1,
        "got {codes:?}"
    );
    let listed = auth_get(&app, "/v1/orgs/t1/payment-links");
    let doc: serde_json::Value = listed.into_json().unwrap();
    let items = doc["items"].as_array().expect("items");
    assert_eq!(items[0]["taken_count"], 1);
    assert_eq!(items[0]["status"], "full");
}

#[test]
fn concurrent_fulfill_same_checkout_one_receipt() {
    let app = TestApp::spawn();
    owner_one(&app);
    put_stripe(&app);
    let (_, checkout_id) = seed_checkout(&app, "stripe", None);
    let t = chrono::Utc::now().timestamp();
    let base = app.base_url.clone();
    let secret = app.config.stripe_webhook_secret.clone();
    let checkout = checkout_id.clone();
    let (a, b) = std::thread::scope(|scope| {
        let event_a = format!("evt_rcpt_a_{}", uuid::Uuid::new_v4().simple());
        let event_b = format!("evt_rcpt_b_{}", uuid::Uuid::new_v4().simple());
        let ha = {
            let base = base.clone();
            let secret = secret.clone();
            let checkout = checkout.clone();
            scope.spawn(move || {
                let payload =
                    stripe_paid(&event_a, &checkout, r#""amount_total":1000,"currency":"myr""#);
                let sig = stripe_webhook::sign_fixture(&secret, &payload, t);
                support::send(
                    ureq::post(&format!("{base}/v1/webhooks/stripe/t1")).set("Stripe-Signature", &sig),
                    &payload,
                )
                .status()
            })
        };
        let hb = {
            let base = base.clone();
            let secret = secret.clone();
            let checkout = checkout.clone();
            scope.spawn(move || {
                let payload =
                    stripe_paid(&event_b, &checkout, r#""amount_total":1000,"currency":"myr""#);
                let sig = stripe_webhook::sign_fixture(&secret, &payload, t);
                support::send(
                    ureq::post(&format!("{base}/v1/webhooks/stripe/t1")).set("Stripe-Signature", &sig),
                    &payload,
                )
                .status()
            })
        };
        (ha.join().unwrap(), hb.join().unwrap())
    });
    assert!(a < 300, "a={a}");
    assert!(b < 300, "b={b}");
    assert_eq!(docs_count(&app), 1);
    assert_eq!(charges_count(&app), 1);
}

#[test]
fn refund_after_fulfill_books_refund_document_on_postgres() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (_, checkout_id) = seed_checkout(&app, "test", None);
    let body = format!(
        r#"{{"id":"evt_pg_pay","checkout_id":"{checkout_id}","amount_total":1000,"currency":"myr"}}"#
    );
    let pay = post_test(&app, &body);
    assert!(pay.status() < 300, "{}", pay.into_string().unwrap_or_default());

    let response = refund(&app, &checkout_id, None);
    assert_eq!(response.status(), 201, "{}", response.into_string().unwrap_or_default());

    let mut db = app.pool.get().expect("pool");
    let titles: Vec<String> = db
        .query("SELECT \"Title\" FROM public.documents", &[])
        .unwrap()
        .iter()
        .map(|row| row.get(0))
        .collect();
    assert!(titles.iter().any(|t| t == "Official Receipt"), "{titles:?}");
    assert!(titles.iter().any(|t| t == "Refund"), "{titles:?}");
    let cash_c: i64 = db
        .query_one(
            "SELECT count(*) FROM public.journal_lines WHERE \"Account\" = 'cash' AND \"Dc\" = 'C'",
            &[],
        )
        .unwrap()
        .get(0);
    let charge_status: String = db
        .query_one("SELECT \"Status\" FROM public.charges", &[])
        .unwrap()
        .get(0);
    assert_eq!(cash_c, 1);
    assert_eq!(charge_status, "refunded");
}

#[test]
fn concurrent_same_key_refunds_replay_not_conflict() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (_, checkout_id) = seed_checkout(&app, "test", None);
    let body = format!(
        r#"{{"id":"evt_pg_idem","checkout_id":"{checkout_id}","amount_total":1000,"currency":"myr"}}"#
    );
    let pay = post_test(&app, &body);
    assert!(pay.status() < 300, "{}", pay.into_string().unwrap_or_default());

    let base = app.base_url.clone();
    let id = checkout_id.clone();
    let (first, second) = std::thread::scope(|scope| {
        let a = scope.spawn(|| {
            support::send(
                ureq::post(&format!("{base}/v1/orgs/t1/refunds"))
                    .set("Authorization", "Bearer jwt")
                    .set("Idempotency-Key", "pg-ref-1"),
                &format!(r#"{{"checkout_id":"{id}"}}"#),
            )
            .status()
        });
        let b = scope.spawn(|| {
            support::send(
                ureq::post(&format!("{base}/v1/orgs/t1/refunds"))
                    .set("Authorization", "Bearer jwt")
                    .set("Idempotency-Key", "pg-ref-1"),
                &format!(r#"{{"checkout_id":"{id}"}}"#),
            )
            .status()
        });
        (a.join().unwrap(), b.join().unwrap())
    });
    let codes = [first, second];
    assert_eq!(codes.iter().filter(|c| **c == 201).count(), 1, "got {codes:?}");
    assert_eq!(codes.iter().filter(|c| **c == 200).count(), 1, "got {codes:?}");

    let refunds: i64 = app
        .pool
        .get()
        .expect("pool")
        .query_one("SELECT count(*) FROM public.refunds", &[])
        .unwrap()
        .get(0);
    assert_eq!(refunds, 1);
    let charge_status: String = app
        .pool
        .get()
        .expect("pool")
        .query_one("SELECT \"Status\" FROM public.charges", &[])
        .unwrap()
        .get(0);
    assert_eq!(charge_status, "refunded");
    let cash_c: i64 = app
        .pool
        .get()
        .expect("pool")
        .query_one(
            "SELECT count(*) FROM public.journal_lines WHERE \"Account\" = 'cash' AND \"Dc\" = 'C'",
            &[],
        )
        .unwrap()
        .get(0);
    assert_eq!(cash_c, 1);
}
