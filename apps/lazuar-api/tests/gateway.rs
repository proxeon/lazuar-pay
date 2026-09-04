//! Port of C# `Credentials/GatewayTests.cs`.

mod support;

use support::{auth_get, member_one, owner_one, put_chip, put_gateway, TestApp};

#[test]
fn member_cannot_put_gateway() {
    let app = TestApp::spawn();
    member_one(&app);
    let resp = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_x"}"#,
    );
    assert_eq!(resp.status(), 403, "{}", resp.into_string().unwrap_or_default());
}

#[test]
fn put_requires_webhook_secret() {
    let app = TestApp::spawn();
    owner_one(&app);
    let resp = put_gateway(&app, r#"{"provider":"stripe","secret":"sk_test_dummy"}"#);
    assert_eq!(resp.status(), 400, "{}", resp.into_string().unwrap_or_default());
}

#[test]
fn put_and_get_does_not_echo_secret() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}"#,
    );
    let put_status = put.status();
    let put_body = put.into_string().unwrap_or_default();
    assert!(put_status < 300, "{put_body}");
    assert!(!put_body.contains("sk_test_dummy"), "{put_body}");
    assert!(!put_body.contains("whsec_abc"), "{put_body}");

    let got = auth_get(&app, "/v1/orgs/t1/gateway?provider=stripe");
    let json = got.into_string().unwrap_or_default();
    let body: serde_json::Value = serde_json::from_str(&json).unwrap();
    assert_eq!(body["configured"], true, "{json}");
    assert_eq!(body["provider"], "stripe", "{json}");
    assert_eq!(body["capability"], "hosted_link", "{json}");
    assert_eq!(body["webhook_configured"], true, "{json}");
    assert!(!json.contains("sk_test"), "{json}");
    assert!(!json.contains("whsec_abc"), "{json}");

    let mut db = app.db();
    let audits: i64 = db
        .query_one(
            "SELECT COUNT(*) FROM public.audit_events \
             WHERE \"Action\" = 'gateway.credentials.upsert' AND \"OrgId\" = 't1'",
            &[],
        )
        .unwrap()
        .get(0);
    assert_eq!(audits, 1);
    let active: Option<String> = db
        .query_one(
            "SELECT \"ActiveProvider\" FROM public.org_settings WHERE \"OrgId\" = 't1'",
            &[],
        )
        .unwrap()
        .get(0);
    assert_eq!(active, None);
}

#[test]
fn chip_put_requires_brand_id() {
    let app = TestApp::spawn();
    owner_one(&app);
    let resp = put_gateway(
        &app,
        r#"{"provider":"chip","secret":"chip_sk","webhook_secret":"-----BEGIN PUBLIC KEY-----\nM\n-----END PUBLIC KEY-----"}"#,
    );
    assert_eq!(resp.status(), 400, "{}", resp.into_string().unwrap_or_default());
}

#[test]
fn put_unknown_provider_is_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    let resp = put_gateway(
        &app,
        r#"{"provider":"paypal","secret":"x","webhook_secret":"y"}"#,
    );
    assert_eq!(resp.status(), 400, "{}", resp.into_string().unwrap_or_default());
}

#[test]
fn member_can_get_gateway_metadata() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    member_one(&app);
    let got = auth_get(&app, "/v1/orgs/t1/gateway?provider=stripe");
    assert_eq!(got.status(), 200);
    let json = got.into_string().unwrap_or_default();
    assert!(!json.contains("sk_test"), "{json}");
    assert!(!json.contains("whsec"), "{json}");
    let body: serde_json::Value = serde_json::from_str(&json).unwrap();
    assert_eq!(body["provider"], "stripe");
    assert_eq!(body["capability"], "hosted_link");
}

#[test]
fn list_returns_all_vaulted_rails_and_put_does_not_default_pay_links() {
    let app = TestApp::spawn();
    owner_one(&app);
    let stripe = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}"#,
    );
    assert!(stripe.status() < 300, "{}", stripe.into_string().unwrap_or_default());
    let chip = put_chip(&app);
    assert!(chip.status() < 300, "{}", chip.into_string().unwrap_or_default());

    let got = auth_get(&app, "/v1/orgs/t1/gateways");
    assert_eq!(got.status(), 200);
    let body: serde_json::Value = got.into_json().unwrap();
    let processors = body["processors"].as_array().expect("processors");
    assert_eq!(processors.len(), 7, "{body}");
    let find = |name: &str| {
        processors
            .iter()
            .find(|p| p["provider"] == name)
            .unwrap_or_else(|| panic!("missing {name} in {body}"))
    };
    assert_eq!(find("solana")["configured"], false);
    assert_eq!(find("solana")["capability"], "hosted_link");
    assert_eq!(find("stripe")["configured"], true);
    assert_eq!(find("chip")["configured"], true);
    assert_eq!(find("xendit")["configured"], false);
    assert_eq!(find("test")["configured"], true);

    let mut db = app.db();
    let active: Option<String> = db
        .query_one(
            "SELECT \"ActiveProvider\" FROM public.org_settings WHERE \"OrgId\" = 't1'",
            &[],
        )
        .unwrap()
        .get(0);
    assert_eq!(active, None);
    let creds: i64 = db
        .query_one("SELECT COUNT(*) FROM public.gateway_credentials", &[])
        .unwrap()
        .get(0);
    assert_eq!(creds, 2);
}

#[test]
fn get_singular_without_provider_is_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    let got = auth_get(&app, "/v1/orgs/t1/gateway");
    assert_eq!(got.status(), 400);
    let body = got.into_string().unwrap_or_default();
    assert!(body.contains("provider is required"), "{body}");
}

#[test]
fn put_test_processor_is_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    let resp = put_gateway(
        &app,
        r#"{"provider":"test","secret":"x","webhook_secret":"y"}"#,
    );
    assert_eq!(resp.status(), 400);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("does not take secrets"), "{body}");
}

#[test]
fn get_unknown_provider_query_is_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    let got = auth_get(&app, "/v1/orgs/t1/gateway?provider=paypal");
    assert_eq!(got.status(), 400, "{}", got.into_string().unwrap_or_default());
}

#[test]
fn billplz_put_requires_collection_id() {
    let app = TestApp::spawn();
    owner_one(&app);
    let resp = put_gateway(
        &app,
        r#"{"provider":"billplz","secret":"bp","webhook_secret":"x","environment":"test"}"#,
    );
    assert_eq!(resp.status(), 400, "{}", resp.into_string().unwrap_or_default());
}

#[test]
fn razorpay_put_requires_key_id_colon_secret() {
    let app = TestApp::spawn();
    owner_one(&app);
    let resp = put_gateway(
        &app,
        r#"{"provider":"razorpay","secret":"nocolon","webhook_secret":"wh"}"#,
    );
    assert_eq!(resp.status(), 400, "{}", resp.into_string().unwrap_or_default());
}

#[test]
fn chip_put_rejects_non_pem_webhook_secret() {
    let app = TestApp::spawn();
    owner_one(&app);
    let resp = put_gateway(
        &app,
        r#"{"provider":"chip","secret":"chip_sk","webhook_secret":"nope","public_merchant_id":"brand_1"}"#,
    );
    assert_eq!(resp.status(), 400);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("PEM"), "{body}");
}
