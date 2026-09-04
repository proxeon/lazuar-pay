mod support;

use lazuar_api::identity::one_webhook_signature;
use support::TestApp;

#[test]
fn one_webhook_pauses_charges() {
    let app = TestApp::spawn_with(|mut c| {
        c.one_webhook_secret = "whsec_process".into();
        c
    });
    let body = r#"{"id":"evt_1","type":"tenant.suspended","org_id":"t1"}"#;
    let ts = chrono::Utc::now().timestamp();
    let v1 = one_webhook_signature::compute("whsec_process", body, ts);
    let resp = ureq::post(&format!("{}/v1/one/webhooks", app.base_url))
        .set("X-Lazuar-Signature", &format!("v1={v1}"))
        .set("X-Lazuar-Timestamp", &ts.to_string())
        .send_string(body)
        .unwrap();
    assert_eq!(resp.status(), 200);
    let mut db = app.db();
    let paused: bool = db
        .query_one(
            "SELECT \"ChargesPaused\" FROM public.org_settings WHERE \"OrgId\" = $1",
            &[&"t1"],
        )
        .unwrap()
        .get(0);
    assert!(paused);
}

#[test]
fn one_webhook_duplicate_is_ok() {
    let app = TestApp::spawn_with(|mut c| {
        c.one_webhook_secret = "whsec_process".into();
        c
    });
    let body = r#"{"id":"evt_dup","type":"unknown"}"#;
    let ts = chrono::Utc::now().timestamp();
    let v1 = one_webhook_signature::compute("whsec_process", body, ts);
    let post = || {
        ureq::post(&format!("{}/v1/one/webhooks", app.base_url))
            .set("X-Lazuar-Signature", &format!("v1={v1}"))
            .set("X-Lazuar-Timestamp", &ts.to_string())
            .send_string(body)
    };
    assert_eq!(post().unwrap().status(), 200);
    let body: serde_json::Value = post().unwrap().into_json().unwrap();
    assert_eq!(body["duplicate"], true);
}
