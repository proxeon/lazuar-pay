//! Port of C# `PublicPay/PublicPayTests.cs`.

mod support;

use rust_decimal::Decimal;
use support::{
    auth_post, call, owner_one, put_chip, put_gateway, seed_checkout, start_pay, TestApp,
};

#[test]
fn public_get_does_not_need_bearer() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, _) = seed_checkout(&app, "stripe", None);
    let get = call(ureq::get(&format!("{}/v1/pay/{token}", app.base_url)));
    assert_eq!(get.status(), 200);
    let after = app.one.send_count();
    assert!(after > 0);
    let again = call(ureq::get(&format!("{}/v1/pay/{token}", app.base_url)));
    assert_eq!(again.status(), 200);
    assert_eq!(app.one.send_count(), after);
}

#[test]
fn public_missing_is_404() {
    let app = TestApp::spawn();
    let resp = call(ureq::get(&format!("{}/v1/pay/missing", app.base_url)));
    assert_eq!(resp.status(), 404);
    assert_eq!(app.one.send_count(), 0);
}

#[test]
fn start_twice_returns_same_url_without_second_psp_http() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 200,
        body: r#"{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}"#.into(),
    });
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, checkout_id) = seed_checkout(&app, "chip", None);
    let first = start_pay(&app, &token, r#"{"name":"Ada","email":"ada@acme.test"}"#);
    let status = first.status();
    let raw = first.into_string().unwrap_or_default();
    assert_eq!(status, 200, "{raw}");
    let url = serde_json::from_str::<serde_json::Value>(&raw).unwrap()["redirect_url"]
        .as_str()
        .unwrap()
        .to_string();
    assert_eq!(url, "https://gate.chip-in.asia/p/x");
    assert_eq!(app.psp.send_count(), 1);
    let second = start_pay(&app, &token, r#"{"name":"Ada","email":"ada@acme.test"}"#);
    let second_status = second.status();
    let second_raw = second.into_string().unwrap_or_default();
    assert_eq!(second_status, 200, "{second_raw}");
    let second_doc: serde_json::Value = serde_json::from_str(&second_raw).unwrap();
    assert_eq!(second_doc["redirect_url"], url);
    assert_eq!(app.psp.send_count(), 1);
    let mut db = app.db();
    let row = db
        .query_one(
            "SELECT \"ProviderSessionId\",\"Provider\" FROM public.checkouts WHERE \"Id\" = $1",
            &[&checkout_id],
        )
        .unwrap();
    let session: Option<String> = row.get(0);
    let provider: Option<String> = row.get(1);
    assert_eq!(session.as_deref(), Some("purch_1"));
    assert_eq!(provider.as_deref(), Some("chip"));
}

#[test]
fn public_get_exposes_started_and_redirect_after_start() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 200,
        body: r#"{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}"#.into(),
    });
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, _) = seed_checkout(&app, "chip", None);
    let before = call(ureq::get(&format!("{}/v1/pay/{token}", app.base_url)));
    let before_doc: serde_json::Value = before.into_json().unwrap();
    assert_eq!(before_doc["started"], false);
    assert!(before_doc["redirect_url"].is_null() || before_doc.get("redirect_url").is_none());
    let started = start_pay(&app, &token, r#"{"name":"Ada","email":"ada@acme.test"}"#);
    assert_eq!(started.status(), 200, "{}", started.into_string().unwrap_or_default());
    let after = call(ureq::get(&format!("{}/v1/pay/{token}", app.base_url)));
    let after_doc: serde_json::Value = after.into_json().unwrap();
    assert_eq!(after_doc["started"], true);
    assert_eq!(after_doc["redirect_url"], "https://gate.chip-in.asia/p/x");
}

#[test]
fn start_paid_is_409() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, checkout_id) = seed_checkout(&app, "stripe", None);
    let mut db = app.db();
    db.execute(
        "UPDATE public.checkouts SET \"Status\" = 'paid', \"PspRedirectUrl\" = 'https://already.example/x' \
         WHERE \"Id\" = $1",
        &[&checkout_id],
    )
    .unwrap();
    drop(db);
    let resp = start_pay(&app, &token, r#"{"email":"ada@acme.test"}"#);
    assert_eq!(resp.status(), 409, "{}", resp.into_string().unwrap_or_default());
}

#[test]
fn start_paused_is_403_even_with_stored_url() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, checkout_id) = seed_checkout(&app, "stripe", None);
    let mut db = app.db();
    db.execute(
        "UPDATE public.checkouts SET \"PspRedirectUrl\" = 'https://gate.chip-in.asia/p/x' WHERE \"Id\" = $1",
        &[&checkout_id],
    )
    .unwrap();
    db.execute(
        "UPDATE public.org_settings SET \"ChargesPaused\" = TRUE WHERE \"OrgId\" = 't1'",
        &[],
    )
    .unwrap();
    drop(db);
    let resp = start_pay(&app, &token, r#"{"email":"ada@acme.test"}"#);
    assert_eq!(resp.status(), 403, "{}", resp.into_string().unwrap_or_default());
    assert_eq!(app.psp.send_count(), 0);
}

#[test]
fn email_required_true_when_active_chip() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, _) = seed_checkout(&app, "chip", None);
    let get = call(ureq::get(&format!("{}/v1/pay/{token}", app.base_url)));
    let doc: serde_json::Value = get.into_json().unwrap();
    assert_eq!(doc["email_required"], true);
}

#[test]
fn email_required_false_when_active_stripe() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, _) = seed_checkout(&app, "stripe", None);
    let get = call(ureq::get(&format!("{}/v1/pay/{token}", app.base_url)));
    let doc: serde_json::Value = get.into_json().unwrap();
    assert_eq!(doc["email_required"], false);
}

fn insert_open_checkout(app: &TestApp, token: &str, provider: Option<&str>) {
    let mut db = app.db();
    db.execute(
        "INSERT INTO public.checkouts \
         (\"Id\",\"OrgId\",\"PublicToken\",\"Amount\",\"Currency\",\"Status\",\"Interval\",\"CreatedAt\",\"Provider\") \
         VALUES ($1,'t1',$2,$3,'MYR','open','one_off',$4,$5)",
        &[
            &uuid::Uuid::new_v4().simple().to_string(),
            &token,
            &Decimal::from(10),
            &chrono::Utc::now(),
            &provider,
        ],
    )
    .unwrap();
}

#[test]
fn start_without_rail_is_503() {
    let app = TestApp::spawn();
    insert_open_checkout(&app, "legacyopen", None);
    let resp = auth_post(&app, "/v1/pay/legacyopen/start", r#"{"email":"ada@acme.test"}"#);
    assert_eq!(resp.status(), 503);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("rail not configured"), "{body}");
}

#[test]
fn start_does_not_read_leftover_active_provider() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let mut db = app.db();
    db.execute(
        "UPDATE public.org_settings SET \"ActiveProvider\" = 'stripe' WHERE \"OrgId\" = 't1'",
        &[],
    )
    .unwrap();
    drop(db);
    insert_open_checkout(&app, "legacyactive", None);
    let resp = start_pay(&app, "legacyactive", r#"{"email":"ada@acme.test"}"#);
    assert_eq!(resp.status(), 503);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("rail not configured"), "{body}");
}
