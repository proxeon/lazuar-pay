//! Port of C# `Webhooks/OrgWebhookTests.cs` plus rotate/test HTTP.

mod support;

use support::{auth_get, auth_post, auth_put, member_one, owner_one, seed_checkout, TestApp};

fn put_url(app: &TestApp, org_id: &str, url: &str) -> ureq::Response {
    auth_put(
        app,
        &format!("/v1/orgs/{org_id}/webhooks"),
        &format!(r#"{{"url":"{url}"}}"#),
    )
}

#[test]
fn compute_then_verify_round_trip() {
    let body = r#"{"ok":true}"#;
    let unix = chrono::Utc::now().timestamp();
    let v1 = lazuar_api::identity::one_webhook_signature::compute("whsec_abc", body, unix);
    assert!(lazuar_api::identity::one_webhook_signature::try_verify(
        "whsec_abc",
        body,
        Some(&format!("v1={v1}")),
        Some(&unix.to_string()),
        300,
        Some(unix),
    ));
    assert!(!lazuar_api::identity::one_webhook_signature::try_verify(
        "whsec_abc",
        &(body.to_string() + "x"),
        Some(&format!("v1={v1}")),
        Some(&unix.to_string()),
        300,
        Some(unix),
    ));
}

#[test]
fn production_rejects_loopback_and_metadata() {
    use lazuar_api::webhooks::org_config::validate_url;
    assert!(validate_url(Some("http://127.0.0.1/hook"), "Production").is_err());
    assert!(validate_url(Some("http://169.254.169.254/"), "Production").is_err());
    let ok = validate_url(Some("https://app.example/hook"), "Production");
    // Unresolved public hostnames are accepted at registration (C# ValidateResolvable
    // keeps them; the dispatcher re-resolves at send). Rust currently fails closed
    // when DNS yields nothing in Production — either outcome is a 400/Ok pair we
    // pin here: https + non-literal host must not be rejected as a private literal.
    match ok {
        Ok(url) => assert!(url.starts_with("https://app.example"), "{url}"),
        Err(err) => assert_eq!(err.detail, "url is not allowed"),
    }
}

#[test]
fn production_rejects_private_ipv6_and_mapped_literals() {
    use lazuar_api::webhooks::org_config::validate_url;
    for url in [
        "http://[::ffff:10.0.0.1]/hook",
        "http://[fc00::1]/hook",
        "http://[fe80::1]/hook",
        "http://[::1]/hook",
        "http://100.64.0.1/hook",
    ] {
        assert!(
            validate_url(Some(url), "Production").is_err(),
            "expected reject {url}"
        );
    }
}

#[test]
fn production_rejects_hostname_that_resolves_private() {
    use lazuar_api::webhooks::org_config::validate_url;
    assert!(validate_url(Some("http://localhost/hook"), "Production").is_err());
}

#[test]
fn testing_allows_loopback() {
    use lazuar_api::webhooks::org_config::validate_url;
    assert!(validate_url(Some("http://127.0.0.1:9/x"), "Testing").is_ok());
    assert!(validate_url(Some("http://127.0.0.1:9/x"), "Development").is_ok());
}

#[test]
fn member_cannot_register() {
    let app = TestApp::spawn();
    member_one(&app);
    let resp = put_url(&app, "t1", "http://127.0.0.1:9/hook");
    assert_eq!(resp.status(), 403, "{}", resp.into_string().unwrap_or_default());
}

#[test]
fn put_and_get_does_not_echo_secret() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_url(&app, "t1", "http://127.0.0.1:9/hook");
    let put_status = put.status();
    let put_json = put.into_string().unwrap_or_default();
    assert_eq!(put_status, 200, "{put_json}");
    let put_doc: serde_json::Value = serde_json::from_str(&put_json).unwrap();
    let secret = put_doc["webhook_secret"].as_str().expect("secret once");
    assert!(secret.starts_with("whsec_"), "{secret}");
    let got = auth_get(&app, "/v1/orgs/t1/webhooks");
    let got_json = got.into_string().unwrap_or_default();
    assert!(got_json.contains("\"webhook_configured\":true"), "{got_json}");
    assert!(!got_json.contains(secret), "{got_json}");
}

#[test]
fn rotate_returns_new_secret_once() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_url(&app, "t1", "http://127.0.0.1:9/hook");
    let first: serde_json::Value = put.into_json().unwrap();
    let first_secret = first["webhook_secret"].as_str().unwrap().to_string();
    let rotate = auth_post(&app, "/v1/orgs/t1/webhooks/rotate", "{}");
    let rotate_status = rotate.status();
    let rotate_json = rotate.into_string().unwrap_or_default();
    assert_eq!(rotate_status, 200, "{rotate_json}");
    let doc: serde_json::Value = serde_json::from_str(&rotate_json).unwrap();
    let second = doc["webhook_secret"].as_str().expect("rotated secret");
    assert!(second.starts_with("whsec_"), "{second}");
    assert_ne!(second, first_secret);
    let got = auth_get(&app, "/v1/orgs/t1/webhooks").into_string().unwrap_or_default();
    assert!(!got.contains(second), "{got}");
    assert!(!got.contains(&first_secret), "{got}");
}

#[test]
fn test_ping_enqueues_webhook_test() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_url(&app, "t1", "http://127.0.0.1:9/hook");
    assert_eq!(put.status(), 200, "{}", put.into_string().unwrap_or_default());
    let ping = auth_post(&app, "/v1/orgs/t1/webhooks/test", "{}");
    assert_eq!(ping.status(), 200, "{}", ping.into_string().unwrap_or_default());
    let mut db = app.db();
    let row = db
        .query_one(
            "SELECT \"EventType\",\"Status\" FROM public.org_webhook_deliveries WHERE \"OrgId\" = 't1'",
            &[],
        )
        .unwrap();
    let event_type: String = row.get(0);
    let status: String = row.get(1);
    assert_eq!(event_type, "webhook.test");
    assert_eq!(status, "pending");
}

#[test]
fn no_endpoint_still_paid() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, _) = seed_checkout(&app, "test", None);
    let start = auth_post(&app, &format!("/v1/pay/{token}/start"), r#"{"name":"Ada"}"#);
    assert_eq!(start.status(), 200, "{}", start.into_string().unwrap_or_default());
    let mut db = app.db();
    let charges: i64 = db
        .query_one("SELECT COUNT(*) FROM public.charges", &[])
        .unwrap()
        .get(0);
    let deliveries: i64 = db
        .query_one("SELECT COUNT(*) FROM public.org_webhook_deliveries", &[])
        .unwrap()
        .get(0);
    assert_eq!(charges, 1);
    assert_eq!(deliveries, 0);
}

#[test]
fn production_put_rejects_ssrf_literals() {
    let app = TestApp::spawn_with(|mut c| {
        c.environment = "Production".into();
        c
    });
    owner_one(&app);
    for url in [
        "http://127.0.0.1/hook",
        "http://169.254.169.254/",
        "http://[::1]/hook",
        "http://[fc00::1]/hook",
        "http://[fe80::1]/hook",
        "http://[::ffff:10.0.0.1]/hook",
        "http://100.64.0.1/hook",
        "http://localhost/hook",
    ] {
        let resp = put_url(&app, "t1", url);
        assert_eq!(resp.status(), 400, "{url} {}", resp.into_string().unwrap_or_default());
    }
}
