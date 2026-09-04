//! Port of C# `Checkouts/CheckoutTests.cs`.

mod support;

use support::{
    allow_org, auth_get, auth_post, call, member_one, put_gateway, seed_checkout, seed_payment_link,
    start_pay, TestApp,
};

fn create(app: &TestApp, body: &str) -> ureq::Response {
    auth_post(app, "/v1/checkouts", body)
}

#[test]
fn create_without_bearer_is_401() {
    let app = TestApp::spawn();
    let resp = support::send(
        ureq::post(&format!("{}/v1/checkouts", app.base_url)),
        r#"{"org_id":"t1","amount":10}"#,
    );
    assert_eq!(resp.status(), 401);
    assert_eq!(app.one.send_count(), 0);
}

#[test]
fn create_and_get_open_session() {
    let app = TestApp::spawn();
    allow_org(&app, "t1");
    let put = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let created = create(
        &app,
        r#"{"org_id":"t1","amount":12.50,"currency":"myr","provider":"stripe","success_url":"https://ok.test","cancel_url":"https://no.test"}"#,
    );
    let status = created.status();
    let raw = created.into_string().unwrap_or_default();
    assert_eq!(status, 201, "{raw}");
    let doc: serde_json::Value = serde_json::from_str(&raw).unwrap();
    let id = doc["id"].as_str().unwrap();
    assert_eq!(doc["org_id"], "t1");
    assert!(doc["amount"].is_number(), "{doc}");
    assert_eq!(doc["amount"].to_string(), "12.50");
    assert_eq!(doc["currency"], "MYR");
    assert_eq!(doc["status"], "open");
    assert_eq!(doc["provider"], "stripe");
    assert_eq!(doc["success_url"], "https://ok.test");
    assert_eq!(doc["cancel_url"], "https://no.test");
    let fetched = auth_get(&app, &format!("/v1/checkouts/{id}"));
    assert_eq!(fetched.status(), 200);
    let got: serde_json::Value = fetched.into_json().unwrap();
    assert_eq!(got["id"], id);
}

#[test]
fn get_unknown_is_404() {
    let app = TestApp::spawn();
    let resp = auth_get(&app, "/v1/checkouts/missing");
    assert_eq!(resp.status(), 404);
    assert_eq!(app.one.send_count(), 0);
}

#[test]
fn get_without_bearer_is_401_for_unknown() {
    let app = TestApp::spawn();
    let resp = call(ureq::get(&format!("{}/v1/checkouts/missing", app.base_url)));
    assert_eq!(resp.status(), 401);
    assert_eq!(app.one.send_count(), 0);
}

#[test]
fn get_without_bearer_is_401_for_known() {
    let app = TestApp::spawn();
    allow_org(&app, "t1");
    let (_, id) = seed_checkout(&app, "test", None);
    let before = app.one.send_count();
    let resp = call(ureq::get(&format!("{}/v1/checkouts/{id}", app.base_url)));
    assert_eq!(resp.status(), 401);
    assert_eq!(app.one.send_count(), before);
}

#[test]
fn create_for_other_org_is_403() {
    let app = TestApp::spawn();
    allow_org(&app, "t1");
    let resp = create(&app, r#"{"org_id":"t2","amount":10}"#);
    assert_eq!(resp.status(), 403, "{}", resp.into_string().unwrap_or_default());
}

#[test]
fn get_other_org_session_is_404() {
    let app = TestApp::spawn();
    allow_org(&app, "t1");
    let put = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let created = create(&app, r#"{"org_id":"t1","amount":10,"provider":"stripe"}"#);
    let id = serde_json::from_str::<serde_json::Value>(&created.into_string().unwrap())
        .unwrap()["id"]
        .as_str()
        .unwrap()
        .to_string();
    allow_org(&app, "t2");
    let fetched = auth_get(&app, &format!("/v1/checkouts/{id}"));
    assert_eq!(fetched.status(), 404);
    let body = fetched.into_string().unwrap_or_default();
    assert!(body.contains("Checkout not found"), "{body}");
}

#[test]
fn create_idempotent_on_key() {
    let app = TestApp::spawn();
    allow_org(&app, "t1");
    let put = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let post = |json: &str| {
        let resp = support::send(
            ureq::post(&format!("{}/v1/checkouts", app.base_url))
                .set("Authorization", "Bearer jwt")
                .set("Idempotency-Key", "k1"),
            json,
        );
        let status = resp.status();
        let raw = resp.into_string().unwrap_or_default();
        let id = serde_json::from_str::<serde_json::Value>(&raw)
            .ok()
            .and_then(|v| v["id"].as_str().map(str::to_string))
            .unwrap_or_default();
        (status, id)
    };
    let a = post(r#"{"org_id":"t1","amount":10,"provider":"stripe"}"#);
    assert_eq!(a.0, 201, "{}", a.1);
    let b = post(r#"{"org_id":"t1","amount":10,"provider":"stripe"}"#);
    assert_eq!(b.0, 200);
    assert_eq!(b.1, a.1);
    let conflict = post(r#"{"org_id":"t1","amount":20,"provider":"stripe"}"#);
    assert_eq!(conflict.0, 409);
}

#[test]
fn create_defaults_currency_to_myr() {
    let app = TestApp::spawn();
    allow_org(&app, "t1");
    let put = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let resp = create(&app, r#"{"org_id":"t1","amount":10,"provider":"stripe"}"#);
    assert_eq!(resp.status(), 201);
    let doc: serde_json::Value = resp.into_json().unwrap();
    assert_eq!(doc["currency"], "MYR");
}

#[test]
fn create_without_provider_is_400() {
    let app = TestApp::spawn();
    allow_org(&app, "t1");
    let put = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let resp = create(&app, r#"{"org_id":"t1","amount":10}"#);
    assert_eq!(resp.status(), 400);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("unknown provider"), "{body}");
}

#[test]
fn create_unknown_provider_is_400() {
    let app = TestApp::spawn();
    allow_org(&app, "t1");
    let resp = create(&app, r#"{"org_id":"t1","amount":10,"provider":"paypal"}"#);
    assert_eq!(resp.status(), 400);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("unknown provider"), "{body}");
}

#[test]
fn create_unconfigured_rail_is_400() {
    let app = TestApp::spawn();
    allow_org(&app, "t1");
    let put = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let resp = create(&app, r#"{"org_id":"t1","amount":10,"provider":"chip"}"#);
    assert_eq!(resp.status(), 400);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("rail not configured"), "{body}");
}

#[test]
fn create_test_without_vault_is_201() {
    let app = TestApp::spawn();
    allow_org(&app, "t1");
    let resp = create(&app, r#"{"org_id":"t1","amount":10,"provider":"test"}"#);
    let status = resp.status();
    let raw = resp.into_string().unwrap_or_default();
    assert_eq!(status, 201, "{raw}");
    let doc: serde_json::Value = serde_json::from_str(&raw).unwrap();
    assert_eq!(doc["provider"], "test");
}

#[test]
fn create_rejects_non_positive_amount() {
    let app = TestApp::spawn();
    allow_org(&app, "t1");
    let resp = create(&app, r#"{"org_id":"t1","amount":0}"#);
    assert_eq!(resp.status(), 400);
}

#[test]
fn member_cannot_create_checkout() {
    let app = TestApp::spawn();
    member_one(&app);
    let resp = create(&app, r#"{"org_id":"t1","amount":10}"#);
    assert_eq!(resp.status(), 403);
}

#[test]
fn list_returns_org_checkouts_newest_first() {
    let app = TestApp::spawn();
    allow_org(&app, "t1");
    seed_checkout(&app, "test", None);
    seed_checkout(&app, "test", None);
    let resp = auth_get(&app, "/v1/orgs/t1/checkouts");
    let status = resp.status();
    let raw = resp.into_string().unwrap_or_default();
    assert_eq!(status, 200, "{raw}");
    let doc: serde_json::Value = serde_json::from_str(&raw).unwrap();
    let items = doc["items"].as_array().expect("items");
    assert_eq!(items.len(), 2);
    assert_eq!(items[0]["provider"], "test");
    assert_eq!(items[0]["status"], "open");
    assert!(items[0]["public_token"].as_str().is_some_and(|s| !s.is_empty()));
}

#[test]
fn list_omits_payment_link_children() {
    let app = TestApp::spawn();
    allow_org(&app, "t1");
    seed_checkout(&app, "test", None);
    let (token, _) = seed_payment_link(&app, "test", 1);
    let started = start_pay(&app, &token, r#"{"name":"Ada","slot_key":"slot-child-1"}"#);
    assert_eq!(started.status(), 200, "{}", started.into_string().unwrap_or_default());
    let resp = auth_get(&app, "/v1/orgs/t1/checkouts");
    let doc: serde_json::Value = resp.into_json().unwrap();
    let items = doc["items"].as_array().expect("items");
    assert_eq!(items.len(), 1);
    assert_eq!(items[0]["status"], "open");
}

#[test]
fn list_other_org_is_403() {
    let app = TestApp::spawn();
    allow_org(&app, "t1");
    let resp = auth_get(&app, "/v1/orgs/t2/checkouts");
    assert_eq!(resp.status(), 403);
}

#[test]
fn health_still_skips_one() {
    let app = TestApp::spawn();
    let resp = call(ureq::get(&format!("{}/health", app.base_url)));
    assert!(resp.status() < 300);
    assert_eq!(app.one.send_count(), 0);
}
