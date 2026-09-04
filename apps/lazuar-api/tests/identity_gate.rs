//! Identity gate — whoami mapping and member/writer authorization, driven by
//! the fake One transport (FixtureOne semantics). `require_*` return
//! `Result<(), PayError>`: Ok = allowed, Err(PayError) = the denial.

mod support;

use lazuar_api::identity::member_gate::{require_member, require_writer};
use lazuar_api::identity::one_client::OneClient;
use lazuar_api::identity::whoami::whoami;
use lazuar_api::identity::whoami_cache::OneWhoamiCache;
use support::FakeTransport;
use std::sync::Arc;

fn one() -> (OneClient, Arc<FakeTransport>) {
    (OneClient { base_url: "http://one.test/api/v1".into(), timeout_secs: 2 }, Arc::new(FakeTransport::new("one")))
}

fn me_json(tenants: &str) -> String {
    format!(r#"{{"user_id":"user_1","email":"a@b.test","name":"A","is_platform_admin":false,"active_tenant_id":"org_1","tenants":[{tenants}]}}"#)
}

#[test]
fn whoami_maps_me_response() {
    let (client, one) = one();
    let me = me_json(r#"{"id":"org_1","slug":"acme","name":"Acme","role":"owner","status":"active"}"#);
    one.respond_with(move |_| lazuar_api::transport::OutResponse { status: 200, body: me.clone() });

    match whoami(&client, one.as_ref(), None, Some("Bearer jwt_1"), None) {
        lazuar_api::identity::whoami::WhoamiOutcome::Ok(resp) => {
            assert_eq!(resp.user_id, "user_1");
            assert_eq!(resp.tenants[0].id, "org_1");
            assert_eq!(resp.tenants[0].role.as_deref(), Some("owner"));
        }
        other => panic!("unexpected {other:?}"),
    }
}

#[test]
fn whoami_does_not_cache_human_jwts() {
    let (client, one) = one();
    let cache = OneWhoamiCache::new();
    let me = me_json(r#"{"id":"org_1","role":"owner","status":"active"}"#);
    one.respond_with(move |_| lazuar_api::transport::OutResponse { status: 200, body: me.clone() });
    whoami(&client, one.as_ref(), Some(&cache), Some("Bearer jwt_1"), None);
    whoami(&client, one.as_ref(), Some(&cache), Some("Bearer jwt_1"), None);
    assert_eq!(one.send_count(), 2, "human JWT must re-hit One /me");
}

#[test]
fn whoami_caches_machine_keys() {
    let (client, one) = one();
    let cache = OneWhoamiCache::new();
    let me = me_json(r#"{"id":"org_1","role":"admin","status":"active"}"#);
    one.respond_with(move |_| lazuar_api::transport::OutResponse { status: 200, body: me.clone() });
    whoami(&client, one.as_ref(), Some(&cache), Some("Bearer lzr_sk_machine"), None);
    whoami(&client, one.as_ref(), Some(&cache), Some("Bearer lzr_sk_machine"), None);
    assert_eq!(one.send_count(), 1, "machine keys are cached 60s");
}

#[test]
fn whoami_rejects_wrong_key_family_before_touching_one() {
    let (client, one) = one();
    let outcome = whoami(&client, one.as_ref(), None, Some("Bearer sk_live_realkey"), None);
    assert!(matches!(outcome, lazuar_api::identity::whoami::WhoamiOutcome::Error(ref e) if e.status == 401));
    // The request never left: no One calls recorded.
    assert_eq!(one.send_count(), 0);
}

#[test]
fn member_gate_human_member_allowed_via_authz_check() {
    let (client, one) = one();
    one.respond_with(|_| lazuar_api::transport::OutResponse { status: 200, body: r#"{"allowed":true}"#.into() });
    require_member(&client, one.as_ref(), Some("Bearer jwt_1"), None, "org_1")
        .expect("member allowed");
    assert_eq!(one.send_count(), 1, "authz check called");
    let last = one.last().expect("recorded authz check");
    assert!(
        last.headers.iter().any(|(k, v)| k.eq_ignore_ascii_case("content-type")
            && v.to_ascii_lowercase().starts_with("application/json")),
        "One JSON POST must send Content-Type application/json, got {:?}",
        last.headers
    );
}

#[test]
fn member_gate_non_member_forbidden() {
    let (client, one) = one();
    one.respond_with(|_| lazuar_api::transport::OutResponse { status: 200, body: r#"{"allowed":false}"#.into() });
    let err = require_member(&client, one.as_ref(), Some("Bearer jwt_1"), None, "org_1")
        .expect_err("non-member denied");
    assert_eq!(err.status, 403);
}

#[test]
fn writer_requires_owner_or_admin_role() {
    let (client, one) = one();
    // Member passes; whoami shows role=member → writer denied.
    one.respond_with(|req| {
        if req.url.ends_with("/authz/check") {
            lazuar_api::transport::OutResponse { status: 200, body: r#"{"allowed":true}"#.into() }
        } else {
            lazuar_api::transport::OutResponse { status: 200, body: me_json(r#"{"id":"org_1","role":"member","status":"active"}"#) }
        }
    });
    let err = require_writer(&client, one.as_ref(), Some("Bearer jwt_1"), None, "org_1")
        .expect_err("member role must not write")
        ;
    assert!(err.detail.contains("Writer role required"));
}

#[test]
fn machine_key_checks_tenant_binding_and_active_status() {
    let (client, one) = one();
    // Bound + active → allowed.
    one.respond_with(|_| {
        lazuar_api::transport::OutResponse { status: 200, body: me_json(r#"{"id":"org_1","role":"admin","status":"active"}"#) }
    });
    require_member(&client, one.as_ref(), Some("Bearer lzr_sk_machine"), None, "org_1")
        .expect("bound machine key allowed");

    // Bound but suspended → forbidden.
    one.respond_with(|_| {
        lazuar_api::transport::OutResponse { status: 200, body: me_json(r#"{"id":"org_1","role":"admin","status":"suspended"}"#) }
    });
    let err = require_member(&client, one.as_ref(), Some("Bearer lzr_sk_machine"), None, "org_1")
        .expect_err("suspended tenant denied");
    assert!(err.detail.contains("suspended"));
}

#[test]
fn one_unreachable_surfaces_503() {
    let (client, one) = one();
    one.respond_with(|_| lazuar_api::transport::OutResponse { status: 503, body: "down".into() });
    let err = require_member(&client, one.as_ref(), Some("Bearer jwt_1"), None, "org_1")
        .expect_err("upstream 503 must surface");
    assert_eq!(err.status, 503);
}

#[test]
fn missing_bearer_is_401() {
    let (client, one) = one();
    let err = require_member(&client, one.as_ref(), None, None, "org_1")
        .expect_err("missing bearer denied");
    assert_eq!(err.status, 401);
    assert_eq!(one.send_count(), 0);
}
