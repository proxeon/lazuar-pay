//! G1 gate for the harness itself: the PayApiFactory analogue must boot a real
//! app, on a real per-test database with the reference schema, before any
//! domain code is ported onto it.

mod support;

use lazuar_api::transport::Transport;
use support::TestApp;

#[test]
fn harness_boots_real_server_with_unique_reference_database() {
    let app = TestApp::spawn();

    // /health — liveness never touches the DB.
    let resp = ureq::get(&format!("{}/health", app.base_url)).call().unwrap();
    assert_eq!(resp.status(), 200);
    assert_eq!(resp.into_string().unwrap(), r#"{"status":"ok"}"#);

    // /ready — the per-test database is reachable and the reference schema is in.
    let resp = ureq::get(&format!("{}/ready", app.base_url)).call().unwrap();
    assert_eq!(resp.status(), 200);

    let mut db = app.db();
    let row = db
        .query_one(
            "SELECT to_regclass('public.checkouts') IS NOT NULL AS ok",
            &[],
        )
        .unwrap();
    let checkouts_exist: bool = row.get(0);
    assert!(checkouts_exist, "reference schema must contain public.checkouts");

    // Isolation: a second app gets a different database.
    let app2 = TestApp::spawn();
    assert_ne!(app.db_name, app2.db_name);
}

#[test]
fn fake_transports_record_sends_and_default_to_404() {
    let app = TestApp::spawn();

    // Default: 404 like FakePspHandler with no responder.
    let out = app
        .psp
        .send(lazuar_api::transport::OutRequest {
            method: "POST".into(),
            url: "http://stripe.test/v1/charges".into(),
            headers: vec![],
            body: Some("{}".into()),
        })
        .unwrap();
    assert_eq!(out.status, 404);
    assert_eq!(app.psp.send_count(), 1);
    let last = app.psp.last().unwrap();
    assert_eq!(last.url, "http://stripe.test/v1/charges");
    assert_eq!(last.body.as_deref(), Some("{}"));

    // With a responder: canned behavior flows through.
    app.one.respond_with(|req| lazuar_api::transport::OutResponse {
        status: 200,
        body: format!("{{\"asked\":\"{}\"}}", req.url),
    });
    let out = app
        .one
        .send(lazuar_api::transport::OutRequest {
            method: "GET".into(),
            url: "http://one.test/api/v1/me".into(),
            headers: vec![],
            body: None,
        })
        .unwrap();
    assert_eq!(out.status, 200);
    assert!(out.body.contains("http://one.test/api/v1/me"));
}
