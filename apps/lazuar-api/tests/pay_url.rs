//! Port of C# `Checkouts/PayUrlTests.cs`.

mod support;

use support::{auth_get, auth_post, machine_one, owner_one, MACHINE_KEY, TestApp};

#[test]
fn checkout_create_and_get_include_pay_url() {
    let app = TestApp::spawn();
    owner_one(&app);
    let created = auth_post(&app, "/v1/checkouts", r#"{"org_id":"t1","amount":10,"provider":"test"}"#);
    let status = created.status();
    let raw = created.into_string().unwrap_or_default();
    assert_eq!(status, 201, "{raw}");
    let doc: serde_json::Value = serde_json::from_str(&raw).unwrap();
    let token = doc["public_token"].as_str().unwrap();
    let id = doc["id"].as_str().unwrap();
    let expected = format!("{}/c/{token}", app.config.checkout_base_url);
    assert_eq!(doc["pay_url"], expected);
    assert!(!expected.contains("localhost:5179"), "{expected}");
    let got = auth_get(&app, &format!("/v1/checkouts/{id}"));
    let got: serde_json::Value = got.into_json().unwrap();
    assert_eq!(got["pay_url"], expected);
}

#[test]
fn payment_link_create_includes_pay_url() {
    let app = TestApp::spawn();
    owner_one(&app);
    let created = auth_post(
        &app,
        "/v1/payment-links",
        r#"{"org_id":"t1","amount":10,"provider":"test","max_payers":1}"#,
    );
    let status = created.status();
    let raw = created.into_string().unwrap_or_default();
    assert_eq!(status, 201, "{raw}");
    let doc: serde_json::Value = serde_json::from_str(&raw).unwrap();
    let token = doc["public_token"].as_str().unwrap();
    assert_eq!(
        doc["pay_url"],
        format!("{}/c/{token}", app.config.checkout_base_url)
    );
}

#[test]
fn key_mint_includes_pay_url() {
    let app = TestApp::spawn();
    machine_one(&app);
    let created = support::send(
        ureq::post(&format!("{}/v1/checkouts", app.base_url))
            .set("Authorization", &format!("Bearer {MACHINE_KEY}")),
        r#"{"org_id":"t1","amount":10,"provider":"test"}"#,
    );
    let status = created.status();
    let raw = created.into_string().unwrap_or_default();
    assert_eq!(status, 201, "{raw}");
    let doc: serde_json::Value = serde_json::from_str(&raw).unwrap();
    let prefix = format!("{}/c/", app.config.checkout_base_url);
    assert!(
        doc["pay_url"].as_str().is_some_and(|u| u.starts_with(&prefix)),
        "{doc}"
    );
}
