//! Port of C# `Money/PaymentQueryTests.cs`.

mod support;

use std::str::FromStr;

use support::{auth_get, owner_one, seed_checkout, start_pay, TestApp};

fn pay_one(app: &TestApp) {
    owner_one(app);
    let (token, _) = seed_checkout(app, "test", None);
    let started = start_pay(app, &token, r#"{"name":"Ada"}"#);
    assert_eq!(started.status(), 200, "{}", started.into_string().unwrap_or_default());
}

#[test]
fn list_payments_includes_provider_and_label() {
    let app = TestApp::spawn();
    pay_one(&app);
    let resp = auth_get(&app, "/v1/orgs/t1/payments");
    let status = resp.status();
    let raw = resp.into_string().unwrap_or_default();
    assert_eq!(status, 200, "{raw}");
    let doc: serde_json::Value = serde_json::from_str(&raw).unwrap();
    let items = doc["items"].as_array().expect("items");
    assert_eq!(items.len(), 1);
    assert_eq!(items[0]["provider"], "test");
    assert_eq!(items[0]["status"], "paid");
    assert_eq!(items[0]["payer_name"], "Ada");
    assert_eq!(
        rust_decimal::Decimal::from_str(&items[0]["amount"].to_string()).unwrap(),
        rust_decimal::Decimal::from(10)
    );
}

#[test]
fn list_receipts_includes_number_amount_and_payer() {
    let app = TestApp::spawn();
    pay_one(&app);
    let resp = auth_get(&app, "/v1/orgs/t1/receipts");
    let status = resp.status();
    let raw = resp.into_string().unwrap_or_default();
    assert_eq!(status, 200, "{raw}");
    let doc: serde_json::Value = serde_json::from_str(&raw).unwrap();
    let items = doc["items"].as_array().expect("items");
    assert_eq!(items.len(), 1);
    assert!(items[0]["number"].as_str().is_some_and(|n| n.starts_with("RCPT-")), "{doc}");
    assert_eq!(items[0]["title"], "Official Receipt");
    assert_eq!(items[0]["status"], "issued");
    assert_eq!(items[0]["payer_name"], "Ada");
    assert_eq!(
        rust_decimal::Decimal::from_str(&items[0]["amount"].to_string()).unwrap(),
        rust_decimal::Decimal::from(10)
    );
}

#[test]
fn get_receipt_by_id_matches_list_fields() {
    let app = TestApp::spawn();
    pay_one(&app);
    let listed = auth_get(&app, "/v1/orgs/t1/receipts");
    let list_doc: serde_json::Value = listed.into_json().unwrap();
    let id = list_doc["items"][0]["id"].as_str().unwrap();
    let resp = auth_get(&app, &format!("/v1/orgs/t1/receipts/{id}"));
    let status = resp.status();
    let raw = resp.into_string().unwrap_or_default();
    assert_eq!(status, 200, "{raw}");
    let doc: serde_json::Value = serde_json::from_str(&raw).unwrap();
    assert_eq!(doc["id"], id);
    assert!(doc["number"].as_str().is_some_and(|n| n.starts_with("RCPT-")), "{doc}");
    assert_eq!(doc["title"], "Official Receipt");
    assert_eq!(doc["status"], "issued");
    assert_eq!(doc["payer_name"], "Ada");
    assert_eq!(
        rust_decimal::Decimal::from_str(&doc["amount"].to_string()).unwrap(),
        rust_decimal::Decimal::from(10)
    );
    assert_eq!(doc["currency"], "MYR");
}

#[test]
fn get_receipt_unknown_is_404() {
    let app = TestApp::spawn();
    owner_one(&app);
    let resp = auth_get(&app, "/v1/orgs/t1/receipts/missing");
    assert_eq!(resp.status(), 404);
}

#[test]
fn get_receipt_other_org_is_403() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.one.respond_with(|req| {
        if req.url.contains("/tenants/t2/authz/check") {
            lazuar_api::transport::OutResponse {
                status: 200,
                body: r#"{"allowed":false}"#.into(),
            }
        } else if req.url.contains("/me") {
            lazuar_api::transport::OutResponse {
                status: 200,
                body: r#"{"user_id":"u1","is_platform_admin":false,"tenants":[{"id":"t1","role":"owner","status":"active"}]}"#.into(),
            }
        } else {
            lazuar_api::transport::OutResponse {
                status: 200,
                body: r#"{"allowed":true}"#.into(),
            }
        }
    });
    let resp = auth_get(&app, "/v1/orgs/t2/receipts/anything");
    assert_eq!(resp.status(), 403);
}
