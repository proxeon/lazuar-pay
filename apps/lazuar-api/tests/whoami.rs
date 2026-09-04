//! Port of C# `Identity/WhoamiTests.cs`.

mod support;

use support::{call, TestApp};

const ME: &str = r#"{"user_id":"u1","email":"ada@acme.test","name":"Ada Lovelace","is_platform_admin":false,"active_tenant_id":"t1","active_role":"owner","tenants":[{"id":"t1","slug":"acme","name":"Acme","role":"owner","status":"active"}]}"#;

#[test]
fn whoami_maps_org_id_from_one_me() {
    let app = TestApp::spawn();
    app.one.respond_with(|req| {
        assert!(req.url.ends_with("/me"), "{}", req.url);
        assert!(
            req.headers
                .iter()
                .any(|(k, v)| k.eq_ignore_ascii_case("authorization") && v == "Bearer tok")
        );
        lazuar_api::transport::OutResponse {
            status: 200,
            body: ME.into(),
        }
    });
    let resp = call(
        ureq::get(&format!("{}/v1/whoami", app.base_url)).set("Authorization", "Bearer tok"),
    );
    assert_eq!(resp.status(), 200);
    let doc: serde_json::Value = resp.into_json().unwrap();
    assert_eq!(doc["user_id"], "u1");
    assert_eq!(doc["email"], "ada@acme.test");
    assert_eq!(doc["name"], "Ada Lovelace");
    assert_eq!(doc["active_org_id"], "t1");
    assert_eq!(doc["tenants"][0]["id"], "t1");
    assert_eq!(app.one.send_count(), 1);
}

#[test]
fn whoami_allows_empty_tenants() {
    let app = TestApp::spawn();
    app.one.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 200,
        body: r#"{"user_id":"u1","email":"ada@acme.test","is_platform_admin":false,"tenants":[]}"#.into(),
    });
    let resp = call(
        ureq::get(&format!("{}/v1/whoami", app.base_url)).set("Authorization", "Bearer tok"),
    );
    assert_eq!(resp.status(), 200);
    let doc: serde_json::Value = resp.into_json().unwrap();
    assert_eq!(doc["tenants"].as_array().unwrap().len(), 0);
}

#[test]
fn bearer_sk_live_is_401_skips_one() {
    let app = TestApp::spawn();
    let resp = call(
        ureq::get(&format!("{}/v1/whoami", app.base_url)).set("Authorization", "Bearer sk_live_dummy"),
    );
    assert_eq!(resp.status(), 401);
    assert_eq!(app.one.send_count(), 0);
}

#[test]
fn bearer_lzr_sk_is_not_rejected_at_parser() {
    let app = TestApp::spawn();
    app.one.respond_with(|req| {
        assert!(
            req.headers
                .iter()
                .any(|(k, v)| k.eq_ignore_ascii_case("authorization") && v == "Bearer lzr_sk_testfixture")
        );
        lazuar_api::transport::OutResponse {
            status: 401,
            body: r#"{"detail":"bad"}"#.into(),
        }
    });
    let resp = call(
        ureq::get(&format!("{}/v1/whoami", app.base_url)).set("Authorization", "Bearer lzr_sk_testfixture"),
    );
    assert_eq!(resp.status(), 401);
    assert_eq!(app.one.send_count(), 1);
}

#[test]
fn whoami_without_authorization_is_401_and_skips_one() {
    let app = TestApp::spawn();
    let resp = call(ureq::get(&format!("{}/v1/whoami", app.base_url)));
    assert_eq!(resp.status(), 401);
    assert_eq!(app.one.send_count(), 0);
}

#[test]
fn whoami_maps_one_401() {
    let app = TestApp::spawn();
    app.one.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 401,
        body: String::new(),
    });
    let resp = call(
        ureq::get(&format!("{}/v1/whoami", app.base_url)).set("Authorization", "Bearer tok"),
    );
    assert_eq!(resp.status(), 401);
}

#[test]
fn whoami_maps_one_500_to_503() {
    let app = TestApp::spawn();
    app.one.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 500,
        body: String::new(),
    });
    let resp = call(
        ureq::get(&format!("{}/v1/whoami", app.base_url)).set("Authorization", "Bearer tok"),
    );
    assert_eq!(resp.status(), 503);
}
