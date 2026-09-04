//! Port of C# `Identity/WorkerKeyTests.cs`.

mod support;

use lazuar_api::boot;
use lazuar_api::config::Config;
use support::{owner_one, TestApp};

#[test]
fn rejects_sk_live_in_worker_slot() {
    let mut c = Config::from_env();
    c.environment = "Testing".into();
    c.one_api_key = Some("sk_live_xxx".into());
    c.one_worker_org_id = Some("t1".into());
    assert!(boot::run(&c).unwrap_err().0.contains("lzr_sk_"));
    c.one_api_key = Some("sk_test_xxx".into());
    assert!(boot::run(&c).unwrap_err().0.contains("lzr_sk_"));
    c.one_api_key = Some("lzr_sk_tenantbound".into());
    boot::run(&c).expect("lzr_sk_ is valid");
}

#[test]
fn requires_worker_org_when_key_set() {
    let mut c = Config::from_env();
    c.environment = "Testing".into();
    c.one_api_key = Some("lzr_sk_job".into());
    c.one_worker_org_id = None;
    assert!(boot::run(&c).unwrap_err().0.contains("WorkerOrgId"));
    c.one_worker_org_id = Some("t1".into());
    boot::run(&c).expect("org + key ok");
}

#[test]
fn missing_request_bearer_still_401_when_env_key_set() {
    let app = TestApp::spawn_with(|mut c| {
        c.one_api_key = Some("lzr_sk_job".into());
        c.one_worker_org_id = Some("t1".into());
        c
    });
    owner_one(&app);
    let resp = support::send(
        ureq::post(&format!("{}/v1/checkouts", app.base_url)),
        r#"{"org_id":"t1","amount":10,"provider":"test"}"#,
    );
    assert_eq!(resp.status(), 401);
    assert_eq!(app.one.send_count(), 0);
}
