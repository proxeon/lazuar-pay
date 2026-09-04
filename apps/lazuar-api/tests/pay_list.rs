//! Port of C# `Hosting/PayListTests.cs`.

mod support;

use support::{auth_get, owner_one, seed_checkout, TestApp};

#[test]
fn checkout_list_pages_with_cursor_on_v1() {
    let app = TestApp::spawn();
    owner_one(&app);
    seed_checkout(&app, "test", None);
    seed_checkout(&app, "test", None);
    seed_checkout(&app, "test", None);

    let page1 = auth_get(&app, "/v1/orgs/t1/checkouts?limit=2");
    let status = page1.status();
    let raw = page1.into_string().unwrap_or_default();
    assert_eq!(status, 200, "{raw}");
    let doc1: serde_json::Value = serde_json::from_str(&raw).unwrap();
    assert_eq!(doc1["items"].as_array().unwrap().len(), 2);
    let cursor = doc1["next_cursor"].as_str().expect("next_cursor");
    assert!(!cursor.is_empty());

    let page2 = auth_get(&app, &format!("/v1/orgs/t1/checkouts?limit=2&after={cursor}"));
    let doc2: serde_json::Value = page2.into_json().unwrap();
    assert_eq!(doc2["items"].as_array().unwrap().len(), 1);
    assert!(doc2["next_cursor"].is_null(), "{doc2}");
}
