//! Port of C# `Hosting/CorsTests.cs`.

mod support;

use lazuar_api::app::resolve_cors_origins;
use support::{call, TestApp};

fn with_origin(app: &TestApp, method: &str, path: &str, origin: &str) -> ureq::Response {
    let url = format!("{}{path}", app.base_url);
    call(ureq::request(method, &url).set("Origin", origin))
}

fn allow_origin(resp: &ureq::Response) -> Option<String> {
    resp.header("Access-Control-Allow-Origin").map(str::to_string)
}

#[test]
fn health_allows_merchant_origin() {
    let app = TestApp::spawn();
    let resp = with_origin(&app, "GET", "/health", "http://localhost:5178");
    assert_eq!(resp.status(), 200);
    assert_eq!(allow_origin(&resp).as_deref(), Some("http://localhost:5178"));
}

#[test]
fn health_allows_checkout_origin() {
    let app = TestApp::spawn();
    let resp = with_origin(&app, "GET", "/health", "http://localhost:5179");
    assert_eq!(resp.status(), 200);
    assert_eq!(allow_origin(&resp).as_deref(), Some("http://localhost:5179"));
}

#[test]
fn health_allows_preview_checkout_origin() {
    let app = TestApp::spawn();
    let resp = with_origin(&app, "GET", "/health", "http://localhost:4179");
    assert_eq!(resp.status(), 200);
    assert_eq!(allow_origin(&resp).as_deref(), Some("http://localhost:4179"));
}

#[test]
fn health_does_not_allow_ops_origin() {
    let app = TestApp::spawn();
    let resp = with_origin(&app, "GET", "/health", "http://localhost:3003");
    assert_eq!(resp.status(), 200);
    assert_eq!(allow_origin(&resp), None);
}

#[test]
fn health_does_not_allow_portal_origin() {
    let app = TestApp::spawn();
    let resp = with_origin(&app, "GET", "/health", "http://localhost:3004");
    assert_eq!(resp.status(), 200);
    assert_eq!(allow_origin(&resp), None);
}

#[test]
fn health_allows_configured_extra_origin() {
    let app = TestApp::spawn_with(|mut c| {
        c.cors_origins = vec!["https://checkout.example".into()];
        c
    });
    let resp = with_origin(&app, "GET", "/health", "https://checkout.example");
    assert_eq!(resp.status(), 200);
    assert_eq!(allow_origin(&resp).as_deref(), Some("https://checkout.example"));
}

#[test]
fn configured_origins_replace_laptop_list() {
    let app = TestApp::spawn_with(|mut c| {
        c.cors_origins = vec!["https://checkout.example".into()];
        c
    });
    let resp = with_origin(&app, "GET", "/health", "http://localhost:5179");
    assert_eq!(resp.status(), 200);
    assert_eq!(allow_origin(&resp), None);
}

#[test]
fn public_pay_get_allows_checkout_origin() {
    let app = TestApp::spawn();
    let resp = with_origin(&app, "GET", "/v1/pay/missing", "http://localhost:5179");
    assert_eq!(resp.status(), 404);
    assert_eq!(allow_origin(&resp).as_deref(), Some("http://localhost:5179"));
}

#[test]
fn public_pay_post_allows_checkout_origin() {
    let app = TestApp::spawn();
    let resp = support::send(
        ureq::post(&format!("{}/v1/pay/missing/start", app.base_url))
            .set("Origin", "http://localhost:5179")
            .set("Content-Type", "application/json"),
        r#"{"name":"Ada"}"#,
    );
    assert_eq!(resp.status(), 404);
    assert_eq!(allow_origin(&resp).as_deref(), Some("http://localhost:5179"));
}

#[test]
fn public_pay_options_allows_checkout_origin() {
    let app = TestApp::spawn();
    let resp = call(
        ureq::request("OPTIONS", &format!("{}/v1/pay/missing", app.base_url))
            .set("Origin", "http://localhost:5179")
            .set("Access-Control-Request-Method", "GET"),
    );
    assert!(resp.status() < 300, "status {}", resp.status());
    assert_eq!(allow_origin(&resp).as_deref(), Some("http://localhost:5179"));
}

#[test]
fn public_pay_options_denies_ops_origin() {
    let app = TestApp::spawn();
    let resp = call(
        ureq::request("OPTIONS", &format!("{}/v1/pay/missing", app.base_url))
            .set("Origin", "http://localhost:3003")
            .set("Access-Control-Request-Method", "POST"),
    );
    assert_eq!(allow_origin(&resp), None);
}

#[test]
fn empty_cors_in_development_uses_laptop_list() {
    let origins = resolve_cors_origins(&[], "Development");
    assert!(origins.iter().any(|o| o == "http://localhost:5178"));
    assert!(origins.iter().any(|o| o == "http://localhost:5179"));
    assert!(origins.iter().any(|o| o == "http://localhost:4179"));
    let testing = resolve_cors_origins(&[], "Testing");
    assert_eq!(testing, origins);
}

#[test]
fn empty_cors_in_production_is_empty_for_boot_to_reject() {
    assert!(resolve_cors_origins(&[], "Production").is_empty());
    assert!(resolve_cors_origins(&[], "Staging").is_empty());
}
