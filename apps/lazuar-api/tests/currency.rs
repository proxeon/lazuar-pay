//! Port of C# `Money/CurrencyValidationTests.cs` (issues 003 / 014).

mod support;

use support::{auth_post, owner_one, put_chip, put_gateway, TestApp};

fn create(app: &TestApp, path: &str, json: &str) -> ureq::Response {
    auth_post(app, path, json)
}

#[test]
fn zero_decimal_currency_is_rejected_on_checkout_and_link() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());

    let checkout = create(
        &app,
        "/v1/checkouts",
        r#"{"org_id":"t1","amount":1000,"currency":"JPY","provider":"stripe"}"#,
    );
    assert_eq!(checkout.status(), 400);
    let body = checkout.into_string().unwrap_or_default();
    assert!(body.contains("not supported"), "{body}");

    let link = create(
        &app,
        "/v1/payment-links",
        r#"{"org_id":"t1","amount":1000,"currency":"JPY","provider":"stripe","max_payers":1}"#,
    );
    assert_eq!(link.status(), 400, "{}", link.into_string().unwrap_or_default());

    let ok = create(
        &app,
        "/v1/checkouts",
        r#"{"org_id":"t1","amount":10,"currency":"USD","provider":"stripe"}"#,
    );
    assert_eq!(ok.status(), 201, "{}", ok.into_string().unwrap_or_default());
}

#[test]
fn billplz_and_chip_reject_non_myr_and_razorpay_rejects_non_inr() {
    let app = TestApp::spawn();
    owner_one(&app);
    let billplz = put_gateway(
        &app,
        r#"{"provider":"billplz","secret":"plz_key","webhook_secret":"wb_key","public_merchant_id":"bar_1","environment":"test"}"#,
    );
    assert!(billplz.status() < 300, "{}", billplz.into_string().unwrap_or_default());
    let chip = put_chip(&app);
    assert!(chip.status() < 300, "{}", chip.into_string().unwrap_or_default());
    let razor = put_gateway(
        &app,
        r#"{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"rzp_wh"}"#,
    );
    assert!(razor.status() < 300, "{}", razor.into_string().unwrap_or_default());

    let billplz_usd = create(
        &app,
        "/v1/payment-links",
        r#"{"org_id":"t1","amount":10,"currency":"USD","provider":"billplz","max_payers":1}"#,
    );
    assert_eq!(billplz_usd.status(), 400, "{}", billplz_usd.into_string().unwrap_or_default());

    let chip_usd = create(
        &app,
        "/v1/checkouts",
        r#"{"org_id":"t1","amount":10,"currency":"USD","provider":"chip"}"#,
    );
    assert_eq!(chip_usd.status(), 400, "{}", chip_usd.into_string().unwrap_or_default());

    let razor_myr = create(
        &app,
        "/v1/checkouts",
        r#"{"org_id":"t1","amount":10,"currency":"MYR","provider":"razorpay"}"#,
    );
    assert_eq!(razor_myr.status(), 400, "{}", razor_myr.into_string().unwrap_or_default());

    let billplz_myr = create(
        &app,
        "/v1/payment-links",
        r#"{"org_id":"t1","amount":10,"currency":"MYR","provider":"billplz","max_payers":1}"#,
    );
    assert_eq!(billplz_myr.status(), 201, "{}", billplz_myr.into_string().unwrap_or_default());

    let razor_inr = create(
        &app,
        "/v1/checkouts",
        r#"{"org_id":"t1","amount":10,"currency":"INR","provider":"razorpay"}"#,
    );
    assert_eq!(razor_inr.status(), 201, "{}", razor_inr.into_string().unwrap_or_default());
}
