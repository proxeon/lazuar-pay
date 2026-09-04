//! Phase 0.1 — C# buyer paths are the only public pay URLs.

mod support;

use support::TestApp;

#[test]
fn csharp_pay_paths_are_routed() {
    let app = TestApp::spawn();
    let missing = "tok_does_not_exist";
    let get = ureq::get(&format!("{}/v1/pay/{missing}", app.base_url)).call();
    let status = match get {
        Ok(r) => r.status(),
        Err(ureq::Error::Status(code, _)) => code,
        Err(e) => panic!("{e}"),
    };
    assert_eq!(status, 404, "GET /v1/pay/{{token}} must be routed (not 404-unmatched 404 from missing router arm)");

    let start = ureq::post(&format!("{}/v1/pay/{missing}/start", app.base_url))
        .send_string("{}");
    let (status, body) = match start {
        Ok(r) => (r.status(), r.into_string().unwrap_or_default()),
        Err(ureq::Error::Status(code, r)) => (code, r.into_string().unwrap_or_default()),
        Err(e) => panic!("{e}"),
    };
    // Missing token is a routed 404 (problem JSON), not an unmatched router arm.
    assert_eq!(status, 404);
    assert!(
        body.contains("Checkout not found") || body.contains("Not Found"),
        "start 404 body: {body}"
    );

    let confirm = ureq::post(&format!("{}/v1/pay/{missing}/confirm", app.base_url))
        .send_string("{}");
    let status = match confirm {
        Ok(r) => r.status(),
        Err(ureq::Error::Status(code, _)) => code,
        Err(e) => panic!("{e}"),
    };
    assert_eq!(status, 404);
}

#[test]
fn public_prefix_is_gone() {
    let app = TestApp::spawn();
    let resp = ureq::get(&format!("{}/v1/public/pay/x", app.base_url)).call();
    let status = match resp {
        Ok(r) => r.status(),
        Err(ureq::Error::Status(code, _)) => code,
        Err(e) => panic!("{e}"),
    };
    assert_eq!(status, 404);
}

#[test]
fn ready_body_is_csharp_shape() {
    let app = TestApp::spawn();
    let resp = ureq::get(&format!("{}/ready", app.base_url)).call().unwrap();
    assert_eq!(resp.status(), 200);
    assert_eq!(resp.into_string().unwrap(), r#"{"status":"ready"}"#);
}

#[test]
fn cutover_gates_hold_in_source() {
    let app = include_str!("../src/app.rs");
    assert!(
        !app.contains(r#"["v1", "public", "pay""#),
        "buyer surface must stay /v1/pay"
    );
    assert!(
        app.contains("LiveRefunder::load"),
        "refunds/webhooks must load LiveRefunder"
    );
    assert!(
        !app.contains("NoopRefunder"),
        "NoopRefunder must not ship on HTTP routes"
    );
    assert!(
        app.contains("VaultedRail"),
        "start must mint from the org vault when the pool exists"
    );
    let razorpay = include_str!("../src/rails/razorpay_webhook.rs");
    assert!(
        razorpay.contains("captured:{payment_id}"),
        "issue 018: body-derived captured: id"
    );
}
