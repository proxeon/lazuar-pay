//! Hosted-mint outbound bodies — C# `*RailTests` LastBody / LastUri plus
//! Stripe session shape. Fixtures only (D004).

mod support;

use support::{auth_post, owner_one, put_chip, put_gateway, seed_checkout, TestApp};

fn psp_ok(body: &'static str) -> impl Fn(&support::RecordedRequest) -> lazuar_api::transport::OutResponse {
    move |_| lazuar_api::transport::OutResponse {
        status: 200,
        body: body.into(),
    }
}

fn start(app: &TestApp, token: &str, body: &str) -> ureq::Response {
    auth_post(app, &format!("/v1/pay/{token}/start"), body)
}

fn last_psp(app: &TestApp) -> support::RecordedRequest {
    app.psp.last().expect("hosted mint must POST the PSP")
}

#[test]
fn stripe_start_sends_checkout_session_payment_mode() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(psp_ok(r#"{"id":"cs_1","url":"https://checkout.stripe.com/c"}"#));
    let put = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, checkout_id) = seed_checkout(&app, "stripe", None);
    let started = start(&app, &token, r#"{"name":"Ada"}"#);
    assert_eq!(started.status(), 200, "{}", started.into_string().unwrap_or_default());
    let req = last_psp(&app);
    assert!(req.url.contains("/v1/checkout/sessions"), "{}", req.url);
    let body = req.body.as_deref().unwrap_or("");
    assert!(body.contains("mode=payment"), "{body}");
    assert!(body.contains(&format!("metadata[checkout_id]={checkout_id}")), "{body}");
    assert!(body.contains("metadata[org_id]=t1"), "{body}");
    assert!(body.contains("unit_amount]=1000"), "{body}");
    assert!(
        req.headers.iter().any(|(k, v)| {
            k.eq_ignore_ascii_case("idempotency-key") && v == &format!("lazuar-checkout:{checkout_id}")
        }),
        "{:?}",
        req.headers
    );
}

#[test]
fn chip_start_sends_currency_metadata_and_not_force_recurring() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(psp_ok(
        r#"{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}"#,
    ));
    let put = put_chip(&app);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, checkout_id) = seed_checkout(&app, "chip", None);
    let started = start(&app, &token, r#"{"name":"Ada","email":"ada@acme.test"}"#);
    let status = started.status();
    let started_body = started.into_string().unwrap_or_default();
    assert_eq!(status, 200, "{started_body}");
    let view: serde_json::Value = serde_json::from_str(&started_body).unwrap();
    assert_eq!(view["redirect_url"], "https://gate.chip-in.asia/p/x");
    let req = last_psp(&app);
    let body = req.body.as_deref().unwrap_or("");
    assert!(!body.contains("force_recurring"), "{body}");
    assert!(body.contains("\"currency\""), "{body}");
    assert!(body.contains("MYR"), "{body}");
    assert!(body.contains("1000"), "{body}");
    assert!(body.contains("checkout_id"), "{body}");
    assert!(body.contains("org_id"), "{body}");
    assert!(body.contains(&checkout_id), "{body}");
    assert!(
        req.headers.iter().any(|(k, v)| {
            k.eq_ignore_ascii_case("idempotency-key") && v == &format!("lazuar-checkout:{checkout_id}")
        }),
        "{:?}",
        req.headers
    );
}

#[test]
fn xendit_start_sends_major_units_not_sen() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(psp_ok(
        r#"{"id":"inv_1","invoice_url":"https://checkout.xendit.co/inv_1"}"#,
    ));
    let put = put_gateway(
        &app,
        r#"{"provider":"xendit","secret":"xnd_sk","webhook_secret":"tok_1"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, checkout_id) = seed_checkout(&app, "xendit", None);
    let started = start(&app, &token, r#"{"email":"ada@acme.test"}"#);
    assert_eq!(started.status(), 200, "{}", started.into_string().unwrap_or_default());
    let req = last_psp(&app);
    let v: serde_json::Value = serde_json::from_str(req.body.as_deref().unwrap_or("{}")).unwrap();
    assert!(v["amount"].is_number(), "{v}");
    assert_eq!(v["amount"].to_string(), "10");
    assert_ne!(v["amount"], 1000);
    assert_eq!(v["external_id"], checkout_id);
    assert!(
        req.headers.iter().any(|(k, v)| {
            k.eq_ignore_ascii_case("idempotency-key") && v == &format!("lazuar-checkout:{checkout_id}")
        }),
        "{:?}",
        req.headers
    );
}

#[test]
fn billplz_start_uses_sandbox_host_and_https_callback() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(psp_ok(
        r#"{"id":"bill_1","url":"https://www.billplz-sandbox.com/bills/bill_1"}"#,
    ));
    let put = put_gateway(
        &app,
        r#"{"provider":"billplz","secret":"bp_sk","webhook_secret":"xsig","public_merchant_id":"col_1","environment":"test"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, checkout_id) = seed_checkout(&app, "billplz", None);
    let started = start(&app, &token, r#"{"email":"ada@acme.test"}"#);
    assert_eq!(started.status(), 200, "{}", started.into_string().unwrap_or_default());
    let req = last_psp(&app);
    assert!(req.url.contains("billplz-sandbox"), "{}", req.url);
    let body = req.body.as_deref().unwrap_or("");
    assert!(body.contains("collection_id=col_1"), "{body}");
    assert!(body.contains("callback_url="), "{body}");
    assert!(body.contains("webhooks%2Fbillplz") || body.contains("/v1/webhooks/billplz"), "{body}");
    assert!(body.contains(&urlencoding::encode(&checkout_id).into_owned()) || body.contains(&checkout_id), "{body}");
    assert!(
        req.headers.iter().any(|(k, v)| {
            k.eq_ignore_ascii_case("idempotency-key") && v == &format!("lazuar-checkout:{checkout_id}")
        }),
        "{:?}",
        req.headers
    );
}

#[test]
fn razorpay_start_sends_payment_link_notes() {
    let app = TestApp::spawn();
    owner_one(&app);
    app.psp.respond_with(psp_ok(r#"{"id":"plink_1","short_url":"https://rzp.io/i/x"}"#));
    let put = put_gateway(
        &app,
        r#"{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh_rzp"}"#,
    );
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, checkout_id) = seed_checkout(&app, "razorpay", Some("INR"));
    let started = start(&app, &token, r#"{"email":"ada@acme.test"}"#);
    assert_eq!(started.status(), 200, "{}", started.into_string().unwrap_or_default());
    let req = last_psp(&app);
    assert!(req.url.contains("/v1/payment_links"), "{}", req.url);
    let v: serde_json::Value = serde_json::from_str(req.body.as_deref().unwrap_or("{}")).unwrap();
    assert_eq!(v["amount"], 1000);
    assert_eq!(v["currency"], "INR");
    assert_eq!(v["notes"]["checkout_id"], checkout_id);
    assert!(
        req.headers.iter().any(|(k, v)| {
            k.eq_ignore_ascii_case("x-razorpay-idempotency") && v == &format!("lazuar-checkout:{checkout_id}")
        }),
        "{:?}",
        req.headers
    );
}
