//! Port of C# `Identity/OneWebhookTests.cs`.

mod support;

use lazuar_api::identity::one_webhook_signature;
use support::{auth_get, auth_put, member_one, owner_one, TestApp};

const SECRET: &str = "one_whsec_test";

fn sign(secret: &str, body: &str, unix: i64) -> String {
    let v1 = one_webhook_signature::compute(secret, body, unix);
    format!("t={unix},v1={v1}")
}

fn spawn_secret() -> TestApp {
    TestApp::spawn_with(|mut c| {
        c.one_webhook_secret = SECRET.into();
        c
    })
}

fn post_one(app: &TestApp, body: &str, secret: &str) -> ureq::Response {
    let t = chrono::Utc::now().timestamp();
    support::send(
        ureq::post(&format!("{}/v1/one/webhooks", app.base_url)).set("X-Lazuar-Signature", &sign(secret, body, t)),
        body,
    )
}

fn put_secret(app: &TestApp, org: &str, secret: &str) {
    let resp = auth_put(
        app,
        &format!("/v1/orgs/{org}/one-webhook"),
        &format!(r#"{{"webhook_secret":"{secret}"}}"#),
    );
    let status = resp.status();
    let raw = resp.into_string().unwrap_or_default();
    assert_eq!(status, 200, "{raw}");
    assert!(!raw.contains(secret), "{raw}");
}

fn paused(app: &TestApp, org: &str) -> bool {
    app.db()
        .query_one(
            "SELECT \"ChargesPaused\" FROM public.org_settings WHERE \"OrgId\" = $1",
            &[&org],
        )
        .unwrap()
        .get(0)
}

#[test]
fn valid_tenant_suspended_sets_charges_paused() {
    let app = spawn_secret();
    let body = r#"{"id":"del_1","type":"tenant.suspended","org_id":"t1"}"#;
    let t = chrono::Utc::now().timestamp();
    let resp = support::send(
        ureq::post(&format!("{}/v1/one/webhooks", app.base_url)).set("X-Lazuar-Signature", &sign(SECRET, body, t)),
        body,
    );
    assert_eq!(resp.status(), 200, "{}", resp.into_string().unwrap_or_default());
    assert!(paused(&app, "t1"));
}

#[test]
fn valid_tenant_id_field_sets_charges_paused() {
    let app = spawn_secret();
    let body = r#"{"id":"del_tenant","type":"tenant.suspended","tenant_id":"t1"}"#;
    let resp = post_one(&app, body, SECRET);
    assert_eq!(resp.status(), 200, "{}", resp.into_string().unwrap_or_default());
    assert!(paused(&app, "t1"));
}

#[test]
fn body_only_uppercase_hex_is_401() {
    let app = spawn_secret();
    let body = r#"{"id":"del_old","type":"tenant.suspended","org_id":"t1"}"#;
    let hex = one_webhook_signature::compute(SECRET, body, chrono::Utc::now().timestamp()).to_uppercase();
    let resp = support::send(
        ureq::post(&format!("{}/v1/one/webhooks", app.base_url)).set("X-Lazuar-Signature", &hex),
        body,
    );
    assert_eq!(resp.status(), 401);
}

#[test]
fn missing_signature_is_401() {
    let app = spawn_secret();
    let resp = support::send(
        ureq::post(&format!("{}/v1/one/webhooks", app.base_url)),
        r#"{"id":"del_x","type":"tenant.suspended","org_id":"t1"}"#,
    );
    assert_eq!(resp.status(), 401);
}

#[test]
fn stale_timestamp_is_401() {
    let app = spawn_secret();
    let body = r#"{"id":"del_stale","type":"tenant.suspended","org_id":"t1"}"#;
    let t = chrono::Utc::now().timestamp() - 1000;
    let resp = support::send(
        ureq::post(&format!("{}/v1/one/webhooks", app.base_url)).set("X-Lazuar-Signature", &sign(SECRET, body, t)),
        body,
    );
    assert_eq!(resp.status(), 401);
}

#[test]
fn missing_secret_is_503() {
    let app = TestApp::spawn_with(|mut c| {
        c.one_webhook_secret = String::new();
        c
    });
    let resp = support::send(ureq::post(&format!("{}/v1/one/webhooks", app.base_url)), r#"{"id":"x"}"#);
    assert_eq!(resp.status(), 503);
}

#[test]
fn replay_delivery_is_duplicate() {
    let app = spawn_secret();
    let body = r#"{"id":"del_replay","type":"tenant.suspended","org_id":"t1"}"#;
    let t = chrono::Utc::now().timestamp();
    let sig = sign(SECRET, body, t);
    let first = support::send(
        ureq::post(&format!("{}/v1/one/webhooks", app.base_url)).set("X-Lazuar-Signature", &sig),
        body,
    );
    assert_eq!(first.status(), 200);
    let second = support::send(
        ureq::post(&format!("{}/v1/one/webhooks", app.base_url)).set("X-Lazuar-Signature", &sig),
        body,
    );
    assert_eq!(second.status(), 200);
    let json = second.into_string().unwrap_or_default();
    assert!(json.contains("duplicate"), "{json}");
}

#[test]
fn tenant_reactivated_clears_pause() {
    let app = spawn_secret();
    let t = chrono::Utc::now().timestamp();
    let suspend = r#"{"id":"del_s","type":"tenant.suspended","org_id":"t1"}"#;
    let s = support::send(
        ureq::post(&format!("{}/v1/one/webhooks", app.base_url)).set("X-Lazuar-Signature", &sign(SECRET, suspend, t)),
        suspend,
    );
    assert_eq!(s.status(), 200);
    let reactivate = r#"{"id":"del_r","type":"tenant.reactivated","org_id":"t1"}"#;
    let r = support::send(
        ureq::post(&format!("{}/v1/one/webhooks", app.base_url)).set("X-Lazuar-Signature", &sign(SECRET, reactivate, t)),
        reactivate,
    );
    assert_eq!(r.status(), 200);
    assert!(!paused(&app, "t1"));
}

#[test]
fn product_one_split_headers_suspend_charges() {
    let app = spawn_secret();
    let body = r#"{"id":"del_one","type":"tenant.suspended","tenant_id":"t1"}"#;
    let t = chrono::Utc::now().timestamp();
    let v1 = one_webhook_signature::compute(SECRET, body, t);
    let resp = support::send(
        ureq::post(&format!("{}/v1/one/webhooks", app.base_url))
            .set("X-Lazuar-Signature", &format!("v1={v1}"))
            .set("X-Lazuar-Timestamp", &t.to_string()),
        body,
    );
    assert_eq!(resp.status(), 200, "{}", resp.into_string().unwrap_or_default());
    assert!(paused(&app, "t1"));
}

#[test]
fn empty_signed_body_is_400() {
    let app = spawn_secret();
    let body = "";
    let t = chrono::Utc::now().timestamp();
    let resp = support::send(
        ureq::post(&format!("{}/v1/one/webhooks", app.base_url)).set("X-Lazuar-Signature", &sign(SECRET, body, t)),
        body,
    );
    assert_eq!(resp.status(), 400);
}

#[test]
fn garbage_signed_body_is_400() {
    let app = spawn_secret();
    let body = "not-json";
    let t = chrono::Utc::now().timestamp();
    let resp = support::send(
        ureq::post(&format!("{}/v1/one/webhooks", app.base_url)).set("X-Lazuar-Signature", &sign(SECRET, body, t)),
        body,
    );
    assert_eq!(resp.status(), 400);
    let json = resp.into_string().unwrap_or_default();
    assert!(json.contains("invalid event"), "{json}");
}

#[test]
fn missing_body_id_uses_event_id_header() {
    let app = spawn_secret();
    let body = r#"{"type":"tenant.suspended","org_id":"t1"}"#;
    let t = chrono::Utc::now().timestamp();
    let sig = sign(SECRET, body, t);
    let first = support::send(
        ureq::post(&format!("{}/v1/one/webhooks", app.base_url))
            .set("X-Lazuar-Signature", &sig)
            .set("X-Lazuar-Event-Id", "del_header"),
        body,
    );
    assert_eq!(first.status(), 200);
    let second = support::send(
        ureq::post(&format!("{}/v1/one/webhooks", app.base_url))
            .set("X-Lazuar-Signature", &sig)
            .set("X-Lazuar-Event-Id", "del_header"),
        body,
    );
    assert_eq!(second.status(), 200);
    let json = second.into_string().unwrap_or_default();
    assert!(json.contains("duplicate"), "{json}");
}

#[test]
fn signed_json_without_event_id_is_400() {
    let app = spawn_secret();
    let body = r#"{"type":"tenant.suspended","org_id":"t1"}"#;
    let resp = post_one(&app, body, SECRET);
    assert_eq!(resp.status(), 400);
    let json = resp.into_string().unwrap_or_default();
    assert!(json.contains("event id required"), "{json}");
}

#[test]
fn member_cannot_put_one_webhook_secret() {
    let app = TestApp::spawn_with(|mut c| {
        c.one_webhook_secret = String::new();
        c
    });
    member_one(&app);
    let resp = auth_put(&app, "/v1/orgs/t1/one-webhook", r#"{"webhook_secret":"whsec_a"}"#);
    assert_eq!(resp.status(), 403);
}

#[test]
fn put_requires_webhook_secret() {
    let app = TestApp::spawn_with(|mut c| {
        c.one_webhook_secret = String::new();
        c
    });
    owner_one(&app);
    let resp = auth_put(&app, "/v1/orgs/t1/one-webhook", "{}");
    assert_eq!(resp.status(), 400);
}

#[test]
fn put_and_get_does_not_echo_secret() {
    let app = TestApp::spawn_with(|mut c| {
        c.one_webhook_secret = String::new();
        c
    });
    owner_one(&app);
    put_secret(&app, "t1", "whsec_shop_a");
    let got = auth_get(&app, "/v1/orgs/t1/one-webhook");
    assert_eq!(got.status(), 200);
    let json = got.into_string().unwrap_or_default();
    assert!(json.contains("\"webhook_configured\":true"), "{json}");
    assert!(!json.contains("whsec_shop_a"), "{json}");
    let audits: i64 = app
        .db()
        .query_one(
            "SELECT COUNT(*) FROM public.audit_events \
             WHERE \"Action\" = 'one.webhook_secret.upsert' AND \"OrgId\" = 't1'",
            &[],
        )
        .unwrap()
        .get(0);
    assert_eq!(audits, 1);
}

#[test]
fn two_orgs_only_matching_secret_pauses() {
    let app = TestApp::spawn_with(|mut c| {
        c.one_webhook_secret = String::new();
        c
    });
    app.one.respond_with(|req| {
        if req.url.contains("/me") {
            lazuar_api::transport::OutResponse {
                status: 200,
                body: r#"{"user_id":"u1","is_platform_admin":false,"tenants":[{"id":"t1","role":"owner","status":"active"},{"id":"t2","role":"owner","status":"active"}]}"#.into(),
            }
        } else {
            lazuar_api::transport::OutResponse {
                status: 200,
                body: r#"{"allowed":true}"#.into(),
            }
        }
    });
    put_secret(&app, "t1", "whsec_a");
    put_secret(&app, "t2", "whsec_b");
    assert_eq!(
        post_one(&app, r#"{"id":"del_a","type":"tenant.suspended","org_id":"t1"}"#, "whsec_a").status(),
        200
    );
    assert_eq!(
        post_one(&app, r#"{"id":"del_steal_t1","type":"tenant.suspended","org_id":"t1"}"#, "whsec_b").status(),
        401
    );
    assert_eq!(
        post_one(&app, r#"{"id":"del_steal_t2","type":"tenant.suspended","org_id":"t2"}"#, "whsec_a").status(),
        401
    );
    assert!(paused(&app, "t1"));
    assert!(!paused(&app, "t2"));
    assert_eq!(
        post_one(&app, r#"{"id":"del_b","type":"tenant.suspended","org_id":"t2"}"#, "whsec_b").status(),
        200
    );
    assert!(paused(&app, "t2"));
    let steals: i64 = app
        .db()
        .query_one(
            "SELECT COUNT(*) FROM public.one_webhook_events WHERE \"DeliveryId\" LIKE 'del_steal_%'",
            &[],
        )
        .unwrap()
        .get(0);
    assert_eq!(steals, 0);
}

#[test]
fn stored_secret_wins_over_process_fallback() {
    let app = spawn_secret();
    owner_one(&app);
    put_secret(&app, "t1", "whsec_stored");
    let body = r#"{"id":"del_stored","type":"tenant.suspended","org_id":"t1"}"#;
    assert_eq!(post_one(&app, body, SECRET).status(), 401);
    assert_eq!(post_one(&app, body, "whsec_stored").status(), 200);
    assert!(paused(&app, "t1"));
}

#[test]
fn nested_data_tenant_id_suspends() {
    let app = spawn_secret();
    let body = r#"{"id":"del_nested","type":"tenant.suspended","data":{"tenant_id":"t1"}}"#;
    assert_eq!(post_one(&app, body, SECRET).status(), 200);
    assert!(paused(&app, "t1"));
}
