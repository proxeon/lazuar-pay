//! Port of C# `Hosting/HealthTests.cs`.

mod support;

use support::{call, TestApp};

#[test]
fn health_returns_ok() {
    let app = TestApp::spawn();
    let resp = call(ureq::get(&format!("{}/health", app.base_url)));
    assert!(resp.status() < 300);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("ok"), "{body}");
}

#[test]
fn v1_health_returns_ok() {
    let app = TestApp::spawn();
    let resp = call(ureq::get(&format!("{}/v1/health", app.base_url)));
    assert!(resp.status() < 300);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("ok"), "{body}");
}

#[test]
fn health_does_not_call_one() {
    let app = TestApp::spawn();
    let h = call(ureq::get(&format!("{}/health", app.base_url)));
    let v1 = call(ureq::get(&format!("{}/v1/health", app.base_url)));
    assert!(h.status() < 300);
    assert!(v1.status() < 300);
    assert_eq!(app.one.send_count(), 0);
}

#[test]
fn unversioned_ready_returns_200() {
    let app = TestApp::spawn();
    let resp = call(ureq::get(&format!("{}/ready", app.base_url)));
    assert!(resp.status() < 300);
    let body = resp.into_string().unwrap_or_default();
    assert!(body.contains("ready"), "{body}");
    assert_eq!(app.one.send_count(), 0);
}
