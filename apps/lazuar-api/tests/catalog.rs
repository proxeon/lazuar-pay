//! Port of C# `Catalog/CatalogTests.cs` (non-MYR already in merchant_http).

mod support;

use support::{allow_org, auth_post, member_one, put_gateway, TestApp};

#[test]
fn create_product_as_owner() {
    let app = TestApp::spawn();
    allow_org(&app, "t1");
    let resp = auth_post(&app, "/v1/orgs/t1/products", r#"{"name":"Seat","amount":10}"#);
    assert_eq!(resp.status(), 201, "{}", resp.into_string().unwrap_or_default());
}

#[test]
fn member_cannot_create_product() {
    let app = TestApp::spawn();
    member_one(&app);
    let resp = auth_post(&app, "/v1/orgs/t1/products", r#"{"name":"Seat","amount":10}"#);
    assert_eq!(resp.status(), 403);
}

#[test]
fn payment_link_amount_must_match_catalog_price() {
    let app = TestApp::spawn();
    allow_org(&app, "t1");
    let put = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let created = auth_post(&app, "/v1/orgs/t1/products", r#"{"name":"Seat","amount":99}"#);
    let status = created.status();
    let raw = created.into_string().unwrap_or_default();
    assert_eq!(status, 201, "{raw}");
    let product_id = serde_json::from_str::<serde_json::Value>(&raw).unwrap()["id"]
        .as_str()
        .unwrap()
        .to_string();
    let link = auth_post(
        &app,
        "/v1/payment-links",
        &format!(r#"{{"org_id":"t1","amount":10,"provider":"stripe","product_id":"{product_id}"}}"#),
    );
    assert_eq!(link.status(), 400);
    let body = link.into_string().unwrap_or_default();
    assert!(body.contains("catalog"), "{body}");
}
