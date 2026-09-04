//! Port of C# `Subscriptions/SubscriptionTests.cs`.

mod support;

use support::{auth_get, auth_post, owner_one, start_pay, TestApp};

#[test]
fn mint_interval_lists_incomplete_then_active_on_pay() {
    let app = TestApp::spawn();
    owner_one(&app);
    let minted = auth_post(
        &app,
        "/v1/checkouts",
        r#"{"org_id":"t1","amount":10,"provider":"test","interval":"mo"}"#,
    );
    let status = minted.status();
    let raw = minted.into_string().unwrap_or_default();
    assert_eq!(status, 201, "{raw}");
    let doc: serde_json::Value = serde_json::from_str(&raw).unwrap();
    assert_eq!(doc["interval"], "mo");
    let token = doc["public_token"].as_str().unwrap();

    let listed = auth_get(&app, "/v1/orgs/t1/subscriptions");
    let open_raw = listed.into_string().unwrap_or_default();
    let open_doc: serde_json::Value = serde_json::from_str(&open_raw).unwrap();
    assert_eq!(open_doc["items"][0]["status"], "incomplete");
    assert!(!open_raw.contains("subscription.activated"), "{open_raw}");

    let started = start_pay(&app, token, r#"{"name":"Ada"}"#);
    assert_eq!(started.status(), 200, "{}", started.into_string().unwrap_or_default());

    let paid_list = auth_get(&app, "/v1/orgs/t1/subscriptions");
    let paid_doc: serde_json::Value = paid_list.into_json().unwrap();
    assert_eq!(paid_doc["items"][0]["status"], "active");
    let mut db = app.db();
    let n: i64 = db
        .query_one(
            "SELECT COUNT(*) FROM public.org_webhook_deliveries WHERE \"EventType\" LIKE 'subscription.%'",
            &[],
        )
        .unwrap()
        .get(0);
    assert_eq!(n, 0);
}

#[test]
fn failed_marks_past_due() {
    let app = TestApp::spawn();
    owner_one(&app);
    let minted = auth_post(
        &app,
        "/v1/checkouts",
        r#"{"org_id":"t1","amount":10,"provider":"test","interval":"yr"}"#,
    );
    let raw = minted.into_string().unwrap_or_default();
    let checkout_id = serde_json::from_str::<serde_json::Value>(&raw).unwrap()["id"]
        .as_str()
        .unwrap()
        .to_string();
    let body = format!(
        r#"{{"id":"evt_sub_fail","checkout_id":"{checkout_id}","status":"failed","currency":"myr"}}"#
    );
    let sig = lazuar_api::rails::test_webhook::test_hmac_hex("test_whsec_local", &body);
    let resp = support::send(
        ureq::post(&format!("{}/v1/webhooks/test/t1", app.base_url))
            .set("X-Pay-Test-Signature", &sig),
        &body,
    );
    assert_eq!(resp.status(), 200, "{}", resp.into_string().unwrap_or_default());
    let mut db = app.db();
    let row = db
        .query_one(
            "SELECT \"Status\",\"AttemptCount\",\"PastDueAt\" FROM public.subscriptions",
            &[],
        )
        .unwrap();
    let status: String = row.get(0);
    let attempts: i32 = row.get(1);
    let past_due: Option<chrono::DateTime<chrono::Utc>> = row.get(2);
    assert_eq!(status, "past_due");
    assert_eq!(attempts, 1);
    assert!(past_due.is_some());
}
