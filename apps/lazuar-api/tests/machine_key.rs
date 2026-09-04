//! Port of C# `Identity/MachineKeyTests.cs` + `WhoamiCacheTests`.

mod support;

use lazuar_api::identity::one_webhook_signature;
use support::{call, machine_one, member_one, MACHINE_KEY, TestApp};

fn bearer_get(app: &TestApp, path: &str, token: &str) -> ureq::Response {
    call(ureq::get(&format!("{}{path}", app.base_url)).set("Authorization", &format!("Bearer {token}")))
}

fn bearer_post(app: &TestApp, path: &str, token: &str, body: &str) -> ureq::Response {
    support::send(
        ureq::post(&format!("{}{path}", app.base_url)).set("Authorization", &format!("Bearer {token}")),
        body,
    )
}

#[test]
fn whoami_forwards_machine_key_shape() {
    let app = TestApp::spawn();
    machine_one(&app);
    let resp = bearer_get(&app, "/v1/whoami", MACHINE_KEY);
    let status = resp.status();
    let raw = resp.into_string().unwrap_or_default();
    assert_eq!(status, 200, "{raw}");
    let doc: serde_json::Value = serde_json::from_str(&raw).unwrap();
    assert_eq!(doc["user_id"], "key-1");
    assert_eq!(doc["tenants"][0]["id"], "t1");
    assert_eq!(doc["is_platform_admin"], false);
    let last = app.one.last().expect("one /me");
    assert!(last.url.ends_with("/me"), "{}", last.url);
    assert!(
        last.headers.iter().any(|(k, v)| {
            k.eq_ignore_ascii_case("authorization") && v == &format!("Bearer {MACHINE_KEY}")
        })
    );
}

#[test]
fn key_ready_does_not_call_authz_check() {
    let app = TestApp::spawn();
    machine_one(&app);
    let resp = bearer_get(&app, "/v1/orgs/t1/ready", MACHINE_KEY);
    assert_eq!(resp.status(), 200, "{}", resp.into_string().unwrap_or_default());
    let last = app.one.last().expect("one /me");
    assert!(last.url.ends_with("/me"), "{}", last.url);
    assert!(!last.url.contains("authz"));
}

#[test]
fn key_member_role_can_create_checkout() {
    let app = TestApp::spawn();
    machine_one(&app);
    let resp = bearer_post(
        &app,
        "/v1/checkouts",
        MACHINE_KEY,
        r#"{"org_id":"t1","amount":10,"provider":"test"}"#,
    );
    assert_eq!(resp.status(), 201, "{}", resp.into_string().unwrap_or_default());
}

#[test]
fn jwt_member_still_cannot_create_checkout() {
    let app = TestApp::spawn();
    member_one(&app);
    let resp = bearer_post(
        &app,
        "/v1/checkouts",
        "tok",
        r#"{"org_id":"t1","amount":10,"provider":"test"}"#,
    );
    assert_eq!(resp.status(), 403);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("Writer role required"), "{body}");
}

#[test]
fn key_bound_to_other_tenant_is_403() {
    let app = TestApp::spawn();
    machine_one(&app);
    let minted = bearer_post(
        &app,
        "/v1/checkouts",
        MACHINE_KEY,
        r#"{"org_id":"t2","amount":10,"provider":"test"}"#,
    );
    assert_eq!(minted.status(), 403);
    let ready = bearer_get(&app, "/v1/orgs/t2/ready", MACHINE_KEY);
    assert_eq!(ready.status(), 403);
    let body = ready.into_string().unwrap_or_default();
    assert!(!body.contains("user_id is required"), "{body}");
}

#[test]
fn key_suspended_tenant_is_403() {
    let app = TestApp::spawn();
    app.one.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 200,
        body: r#"{"user_id":"key-1","is_platform_admin":false,"tenants":[{"id":"t1","role":"member","status":"suspended"}]}"#.into(),
    });
    let resp = bearer_get(&app, "/v1/orgs/t1/ready", MACHINE_KEY);
    assert_eq!(resp.status(), 403);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.to_lowercase().contains("suspend"), "{body}");
}

#[test]
fn revoked_key_is_401() {
    let app = TestApp::spawn_with(|mut c| {
        c.one_webhook_secret = "one_whsec_revoked".into();
        c
    });
    machine_one(&app);
    let first = bearer_get(&app, "/v1/whoami", MACHINE_KEY);
    assert_eq!(first.status(), 200);
    let body = r#"{"id":"del_m19","type":"api_key.revoked","data":{"key_id":"key-1","tenant_id":"t1"}}"#;
    let unix = chrono::Utc::now().timestamp();
    let v1 = one_webhook_signature::compute("one_whsec_revoked", body, unix);
    let rev = support::send(
        ureq::post(&format!("{}/v1/one/webhooks", app.base_url))
            .set("X-Lazuar-Signature", &format!("t={unix},v1={v1}")),
        body,
    );
    assert_eq!(rev.status(), 200, "{}", rev.into_string().unwrap_or_default());
    app.one.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 401,
        body: String::new(),
    });
    let second = bearer_get(&app, "/v1/whoami", MACHINE_KEY);
    assert_eq!(second.status(), 401);
    let mint = bearer_post(
        &app,
        "/v1/checkouts",
        MACHINE_KEY,
        r#"{"org_id":"t1","amount":10,"provider":"test"}"#,
    );
    assert_eq!(mint.status(), 401);
}

#[test]
fn missing_bearer_does_not_use_env_key() {
    let app = TestApp::spawn();
    machine_one(&app);
    let resp = call(ureq::get(&format!("{}/v1/orgs/t1/ready", app.base_url)));
    assert_eq!(resp.status(), 401);
    assert_eq!(app.one.send_count(), 0);
}

#[test]
fn scope_403_is_not_not_a_member() {
    let app = TestApp::spawn();
    app.one.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 403,
        body: r#"{"detail":"API key lacks required scope authz:check."}"#.into(),
    });
    let resp = bearer_get(&app, "/v1/orgs/t1/ready", "tok");
    assert_eq!(resp.status(), 403);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("scope"), "{body}");
    assert!(!body.contains("Not a member"), "{body}");
}

#[test]
fn revoke_event_then_mint_is_401() {
    let app = TestApp::spawn_with(|mut c| {
        c.one_webhook_secret = "one_whsec_cache".into();
        c
    });
    machine_one(&app);
    let first = bearer_post(
        &app,
        "/v1/checkouts",
        MACHINE_KEY,
        r#"{"org_id":"t1","amount":10,"provider":"test"}"#,
    );
    assert_eq!(first.status(), 201, "{}", first.into_string().unwrap_or_default());
    let before = app.one.send_count();
    let cached = bearer_post(
        &app,
        "/v1/checkouts",
        MACHINE_KEY,
        r#"{"org_id":"t1","amount":11,"provider":"test"}"#,
    );
    assert_eq!(cached.status(), 201, "{}", cached.into_string().unwrap_or_default());
    assert_eq!(app.one.send_count(), before);
    let body = r#"{"id":"del_rev","type":"api_key.revoked","data":{"key_id":"key-1","tenant_id":"t1"}}"#;
    let unix = chrono::Utc::now().timestamp();
    let v1 = one_webhook_signature::compute("one_whsec_cache", body, unix);
    let rev = support::send(
        ureq::post(&format!("{}/v1/one/webhooks", app.base_url))
            .set("X-Lazuar-Signature", &format!("t={unix},v1={v1}")),
        body,
    );
    assert_eq!(rev.status(), 200);
    app.one.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 401,
        body: r#"{"detail":"revoked"}"#.into(),
    });
    let after = bearer_post(
        &app,
        "/v1/checkouts",
        MACHINE_KEY,
        r#"{"org_id":"t1","amount":12,"provider":"test"}"#,
    );
    assert_eq!(after.status(), 401);
}
