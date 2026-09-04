//! Remaining C# rail HTTP negatives (placeholder email, empty body, missing Stripe sig).

mod support;

use support::{owner_one, put_chip, put_gateway, seed_checkout, start_pay, TestApp};

#[test]
fn missing_stripe_signature_header_is_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let resp = support::send(
        ureq::post(&format!("{}/v1/webhooks/stripe/t1", app.base_url)),
        r#"{"id":"evt_x","type":"checkout.session.completed"}"#,
    );
    assert_eq!(resp.status(), 400);
}

#[test]
fn razorpay_placeholder_email_is_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_gateway(
        &app,
        r#"{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, _) = seed_checkout(&app, "razorpay", Some("INR"));
    let resp = start_pay(&app, &token, r#"{"email":"customer@example.com"}"#);
    assert_eq!(resp.status(), 400);
}

#[test]
fn xendit_placeholder_email_is_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_gateway(
        &app,
        r#"{"provider":"xendit","secret":"xnd","webhook_secret":"tok"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, _) = seed_checkout(&app, "xendit", None);
    let resp = start_pay(&app, &token, r#"{"email":"customer@example.com"}"#);
    assert_eq!(resp.status(), 400);
}

#[test]
fn billplz_placeholder_email_is_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_gateway(
        &app,
        r#"{"provider":"billplz","secret":"bp_sk","webhook_secret":"xsig","public_merchant_id":"col_1","environment":"test"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, _) = seed_checkout(&app, "billplz", None);
    let resp = start_pay(&app, &token, r#"{"email":"customer@example.com"}"#);
    assert_eq!(resp.status(), 400);
}

#[test]
fn razorpay_empty_body_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_gateway(
        &app,
        r#"{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let resp = support::send(
        ureq::post(&format!("{}/v1/webhooks/razorpay/t1", app.base_url)),
        "  ",
    );
    assert_eq!(resp.status(), 400);
}

#[test]
fn xendit_empty_body_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_gateway(
        &app,
        r#"{"provider":"xendit","secret":"xnd","webhook_secret":"tok"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let resp = support::send(
        ureq::post(&format!("{}/v1/webhooks/xendit/t1", app.base_url)),
        "  ",
    );
    assert_eq!(resp.status(), 400);
}

#[test]
fn chip_placeholder_email_is_400() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, _) = seed_checkout(&app, "chip", None);
    let resp = start_pay(&app, &token, r#"{"name":"Ada","email":"customer@example.com"}"#);
    assert_eq!(resp.status(), 400);
}
