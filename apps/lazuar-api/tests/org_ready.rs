//! Port of C# `Identity/OrgReadyTests.cs`.

mod support;

use lazuar_api::identity::org_ready::is_ready;
use support::{auth_get, call, owner_one, TestApp};

#[test]
fn ready_when_one_allows_member() {
    let app = TestApp::spawn();
    owner_one(&app);
    let resp = auth_get(&app, "/v1/orgs/t1/ready");
    let status = resp.status();
    let raw = resp.into_string().unwrap_or_default();
    assert_eq!(status, 200, "{raw}");
    let body: serde_json::Value = serde_json::from_str(&raw).unwrap();
    assert_eq!(body["org_id"], "t1");
    assert_eq!(body["ready"], true);
    let last = app.one.last().expect("authz check");
    assert!(last.url.contains("/tenants/t1/authz/check"), "{}", last.url);
    let posted = last.body.as_deref().unwrap_or("");
    assert!(posted.contains("\"relation\":\"member\""), "{posted}");
    assert!(posted.contains("\"type\":\"tenant\""), "{posted}");
    assert!(posted.contains("\"id\":\"t1\""), "{posted}");
    assert!(!posted.contains("user_id"), "{posted}");
}

#[test]
fn ready_forbidden_when_allowed_false() {
    let app = TestApp::spawn();
    app.one.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 200,
        body: r#"{"allowed":false}"#.into(),
    });
    let resp = auth_get(&app, "/v1/orgs/t1/ready");
    assert_eq!(resp.status(), 403, "{}", resp.into_string().unwrap_or_default());
}

#[test]
fn ready_forbidden_when_one_403() {
    let app = TestApp::spawn();
    app.one.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 403,
        body: String::new(),
    });
    let resp = auth_get(&app, "/v1/orgs/t1/ready");
    assert_eq!(resp.status(), 403);
}

#[test]
fn ready_503_when_one_500() {
    let app = TestApp::spawn();
    app.one.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 500,
        body: String::new(),
    });
    let resp = auth_get(&app, "/v1/orgs/t1/ready");
    assert_eq!(resp.status(), 503);
}

#[test]
fn ready_400_when_one_400() {
    let app = TestApp::spawn();
    app.one.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 400,
        body: r#"{"detail":"The value 't1' is not valid."}"#.into(),
    });
    let resp = auth_get(&app, "/v1/orgs/t1/ready");
    assert_eq!(resp.status(), 400);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("The value 't1' is not valid."), "{body}");
}

#[test]
fn ready_429_when_one_429() {
    let app = TestApp::spawn();
    app.one.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 429,
        body: String::new(),
    });
    let resp = auth_get(&app, "/v1/orgs/t1/ready");
    assert_eq!(resp.status(), 429);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("Identity provider rate limited"), "{body}");
}

#[test]
fn ready_403_passes_through_suspended_detail() {
    let app = TestApp::spawn();
    app.one.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 403,
        body: r#"{"detail":"Tenant is suspended."}"#.into(),
    });
    let resp = auth_get(&app, "/v1/orgs/t1/ready");
    assert_eq!(resp.status(), 403);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("Tenant is suspended."), "{body}");
    assert!(!body.contains("Not a member"), "{body}");
}

#[test]
fn ready_false_when_charges_paused() {
    let app = TestApp::spawn();
    owner_one(&app);
    let mut db = app.db();
    db.execute(
        "INSERT INTO public.org_settings (\"OrgId\",\"Currency\",\"ChargesPaused\") \
         VALUES ('t1','MYR', TRUE)",
        &[],
    )
    .unwrap();
    drop(db);
    let resp = auth_get(&app, "/v1/orgs/t1/ready");
    assert_eq!(resp.status(), 200);
    let body: serde_json::Value = resp.into_json().unwrap();
    assert_eq!(body["ready"], false);
}

#[test]
fn ready_is_false_without_vault_when_test_is_off() {
    assert!(!is_ready(false, false, false));
    assert!(is_ready(false, true, false));
    assert!(!is_ready(true, true, true));
}

#[test]
fn ready_401_without_bearer_skips_one() {
    let app = TestApp::spawn();
    let resp = call(ureq::get(&format!("{}/v1/orgs/t1/ready", app.base_url)));
    assert_eq!(resp.status(), 401);
    assert_eq!(app.one.send_count(), 0);
}

#[test]
fn ready_checks_path_org_not_header() {
    let app = TestApp::spawn();
    owner_one(&app);
    let resp = call(
        ureq::get(&format!("{}/v1/orgs/path-org/ready", app.base_url))
            .set("Authorization", "Bearer jwt")
            .set("X-Lazuar-Tenant-Id", "header-org"),
    );
    let status = resp.status();
    let raw = resp.into_string().unwrap_or_default();
    assert_eq!(status, 200, "{raw}");
    let body: serde_json::Value = serde_json::from_str(&raw).unwrap();
    assert_eq!(body["org_id"], "path-org");
    let last = app.one.last().expect("authz check");
    assert!(last.url.contains("/tenants/path-org/authz/check"), "{}", last.url);
    let posted = last.body.as_deref().unwrap_or("");
    assert!(posted.contains("\"id\":\"path-org\""), "{posted}");
}
