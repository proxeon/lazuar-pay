//! Port of C# `Money/RefundTests.cs` through `POST /v1/orgs/{orgId}/refunds`
//! and inbound webhook HTTP. Library integrity is not this slice.
//!
//! `Billplz_refund_fails_and_releases_the_reservation` already lives in
//! `paid_webhooks.rs`.

mod support;

use lazuar_api::rails::stripe_webhook;
use rust_decimal::Decimal;
use support::{
    auth_put, checkout_status_of, docs_count, owner_one, put_gateway, seed_checkout, start_pay,
    TestApp,
};

fn refund(app: &TestApp, body: &str, idempotency: Option<&str>) -> ureq::Response {
    let mut req = ureq::post(&format!("{}/v1/orgs/t1/refunds", app.base_url))
        .set("Authorization", "Bearer jwt");
    if let Some(key) = idempotency {
        req = req.set("Idempotency-Key", key);
    }
    support::send(req, body)
}

fn charge_status(app: &TestApp) -> String {
    app.pool
        .get()
        .expect("pool")
        .query_one("SELECT \"Status\" FROM public.charges", &[])
        .unwrap()
        .get(0)
}

#[test]
fn full_refund_reverses_journal_and_uses_ref_number() {
    let app = TestApp::spawn();
    owner_one(&app);
    let hook = auth_put(
        &app,
        "/v1/orgs/t1/webhooks",
        r#"{"url":"http://127.0.0.1:9/hook"}"#,
    );
    assert!(hook.status() < 300, "{}", hook.into_string().unwrap_or_default());
    let (token, checkout_id) = seed_checkout(&app, "test", None);
    let started = start_pay(&app, &token, r#"{"name":"Ada"}"#);
    assert_eq!(started.status(), 200, "{}", started.into_string().unwrap_or_default());

    let body = format!(r#"{{"checkout_id":"{checkout_id}"}}"#);
    let response = refund(&app, &body, Some("ref-1"));
    let status = response.status();
    let raw = response.into_string().unwrap_or_default();
    assert_eq!(status, 201, "{raw}");
    let doc: serde_json::Value = serde_json::from_str(&raw).unwrap();
    assert_eq!(doc["status"], "succeeded");
    let number = doc["number"].as_str().unwrap_or("");
    assert!(number.starts_with("REF-"), "{number}");
    assert!(!number.starts_with("RCPT-"), "{number}");
    assert!(doc["amount"].is_number(), "refund amount must be a JSON number: {doc}");

    let replay = refund(&app, &body, Some("ref-1"));
    assert_eq!(replay.status(), 200, "{}", replay.into_string().unwrap_or_default());

    assert_eq!(charge_status(&app), "refunded");
    let mut db = app.pool.get().expect("pool");
    let cash_c: i64 = db
        .query_one(
            "SELECT count(*) FROM public.journal_lines WHERE \"Account\" = 'cash' AND \"Dc\" = 'C'",
            &[],
        )
        .unwrap()
        .get(0);
    let refund_docs: i64 = db
        .query_one(
            "SELECT count(*) FROM public.documents WHERE \"Title\" = 'Refund'",
            &[],
        )
        .unwrap()
        .get(0);
    let deliveries: i64 = db
        .query_one(
            "SELECT count(*) FROM public.org_webhook_deliveries WHERE \"EventType\" = 'refund.created'",
            &[],
        )
        .unwrap()
        .get(0);
    assert_eq!(cash_c, 1);
    assert_eq!(refund_docs, 1);
    assert_eq!(deliveries, 1);
}

#[test]
fn partial_then_remainder() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, checkout_id) = seed_checkout(&app, "test", None);
    let started = start_pay(&app, &token, r#"{"name":"Ada"}"#);
    assert_eq!(started.status(), 200, "{}", started.into_string().unwrap_or_default());

    let partial = refund(
        &app,
        &format!(r#"{{"checkout_id":"{checkout_id}","amount":4}}"#),
        None,
    );
    let partial_status = partial.status();
    let partial_raw = partial.into_string().unwrap_or_default();
    assert_eq!(partial_status, 201, "{partial_raw}");
    let partial_doc: serde_json::Value = serde_json::from_str(&partial_raw).unwrap();
    assert!(
        partial_doc["amount"].is_number(),
        "partial amount must be a JSON number: {partial_doc}"
    );
    assert_eq!(partial_doc["amount"].as_f64(), Some(4.0));
    assert_eq!(charge_status(&app), "partially_refunded");

    let rest = refund(&app, &format!(r#"{{"checkout_id":"{checkout_id}"}}"#), None);
    assert_eq!(rest.status(), 201, "{}", rest.into_string().unwrap_or_default());
    assert_eq!(charge_status(&app), "refunded");
}

#[test]
fn solana_refund_is_refused_and_releases_the_reservation() {
    let app = TestApp::spawn();
    owner_one(&app);
    let mut db = app.pool.get().expect("pool");
    db.execute(
        "INSERT INTO public.checkouts \
         (\"Id\",\"OrgId\",\"PublicToken\",\"Amount\",\"Currency\",\"Status\",\"Provider\",\"Interval\",\"CreatedAt\") \
         VALUES ('c_sol','t1','tok_sol',$1,'USDC','paid','solana','one_off',$2)",
        &[&Decimal::from(10), &chrono::Utc::now()],
    )
    .unwrap();
    db.execute(
        "INSERT INTO public.charges \
         (\"Id\",\"OrgId\",\"CheckoutId\",\"Provider\",\"Amount\",\"Currency\",\"Status\") \
         VALUES ('ch_sol','t1','c_sol','solana',$1,'USDC','paid')",
        &[&Decimal::from(10)],
    )
    .unwrap();
    drop(db);

    let response = refund(&app, r#"{"checkout_id":"c_sol"}"#, None);
    let status = response.status();
    let body = response.into_string().unwrap_or_default();
    assert_eq!(status, 400);
    assert!(body.contains("refund not supported"), "{body}");

    let mut db = app.pool.get().expect("pool");
    let refund_status: String = db
        .query_one("SELECT \"Status\" FROM public.refunds", &[])
        .unwrap()
        .get(0);
    let charge: String = db
        .query_one("SELECT \"Status\" FROM public.charges", &[])
        .unwrap()
        .get(0);
    assert_eq!(refund_status, "failed");
    assert_eq!(charge, "paid");
}

#[test]
fn stripe_refund_without_session_or_intent_fails_closed() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (_, checkout_id) = seed_checkout(&app, "stripe", None);
    let payload = format!(
        r#"{{"id":"evt_fs","object":"event","api_version":"2024-06-20","created":1700000000,"livemode":false,"pending_webhooks":1,"request":{{"id":null}},"type":"checkout.session.completed","data":{{"object":{{"id":"cs_fs_1","object":"checkout.session","mode":"payment","amount_total":1000,"currency":"myr","client_reference_id":"{checkout_id}","payment_status":"paid","status":"complete","metadata":{{"checkout_id":"{checkout_id}"}}}}}}}}"#
    );
    let sig = stripe_webhook::sign_fixture(
        &app.config.stripe_webhook_secret,
        &payload,
        chrono::Utc::now().timestamp(),
    );
    let paid = support::send(
        ureq::post(&format!("{}/v1/webhooks/stripe/t1", app.base_url)).set("Stripe-Signature", &sig),
        &payload,
    );
    assert!(paid.status() < 300, "{}", paid.into_string().unwrap_or_default());

    let mut db = app.pool.get().expect("pool");
    db.execute(
        "UPDATE public.checkouts SET \"ProviderSessionId\" = NULL WHERE \"Id\" = $1",
        &[&checkout_id],
    )
    .unwrap();
    db.execute(
        "UPDATE public.charges SET \"ProviderRef\" = NULL WHERE \"CheckoutId\" = $1",
        &[&checkout_id],
    )
    .unwrap();
    drop(db);

    let response = refund(&app, &format!(r#"{{"checkout_id":"{checkout_id}"}}"#), None);
    let status = response.status();
    let body = response.into_string().unwrap_or_default();
    assert_eq!(status, 400, "{body}");

    let mut db = app.pool.get().expect("pool");
    let refund_status: String = db
        .query_one("SELECT \"Status\" FROM public.refunds", &[])
        .unwrap()
        .get(0);
    let charge: String = db
        .query_one("SELECT \"Status\" FROM public.charges", &[])
        .unwrap()
        .get(0);
    assert_eq!(refund_status, "failed");
    assert_eq!(charge, "paid");
}

#[test]
fn paid_webhook_on_expired_does_not_fulfill() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (_, checkout_id) = seed_checkout(&app, "test", None);
    app.pool
        .get()
        .expect("pool")
        .execute(
            "UPDATE public.checkouts SET \"Status\" = 'expired' WHERE \"Id\" = $1",
            &[&checkout_id],
        )
        .unwrap();
    let resp = support::test_webhook_paid(&app, "evt_late", &checkout_id);
    let status = resp.status();
    let body = resp.into_string().unwrap_or_default();
    assert_eq!(status, 200, "{body}");
    assert!(body.contains("refunded"), "{body}");
    assert_eq!(docs_count(&app), 0);
    let mut db = app.pool.get().expect("pool");
    let charges: i64 = db.query_one("SELECT count(*) FROM public.charges", &[]).unwrap().get(0);
    let reason: String = db
        .query_one("SELECT \"Reason\" FROM public.refunds", &[])
        .unwrap()
        .get(0);
    let refund_status: String = db
        .query_one("SELECT \"Status\" FROM public.refunds", &[])
        .unwrap()
        .get(0);
    drop(db);
    assert_eq!(charges, 0);
    assert_eq!(checkout_status_of(&app, &checkout_id), "expired");
    assert_eq!(reason, "late_pay");
    assert_eq!(refund_status, "succeeded");
}
