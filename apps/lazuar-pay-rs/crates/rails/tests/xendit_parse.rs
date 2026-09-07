use domain::money::{Currency, Money};
use domain::proof::{Binding, SyncOutcome, WebhookOutcome};
use rails::xendit::{
    ignore_detail, invoice_body, map_sync, parse_hosted, parse_webhook, token_ok, XenditParseError,
    API_BASE,
};
use serde_json::json;

const SECRET: &str = "tok_1";

fn parse_file(name: &str) -> (String, WebhookOutcome) {
    let body = std::fs::read_to_string(format!("tests/fixtures/xendit/{name}")).unwrap();
    parse_webhook(body.trim().as_bytes(), Some(SECRET), SECRET).unwrap()
}

#[test]
fn paid_major_10_is_minor_1000() {
    let (id, out) = parse_file("webhook_paid.json");
    assert_eq!(id, "paid:inv_1");
    let WebhookOutcome::Paid { received, refs, .. } = out else {
        panic!("{out:?}");
    };
    assert_eq!(received.minor(), 1000);
    assert_eq!(received.currency().code.as_str(), "MYR");
    assert_eq!(refs.session_id.as_deref(), Some("inv_1"));
    assert_eq!(refs.capture_id.as_deref(), Some("inv_1"));
}

#[test]
fn paid_binds_metadata_checkout_id() {
    let (_, out) = parse_file("webhook_paid.json");
    let WebhookOutcome::Paid { binding, .. } = out else {
        panic!();
    };
    assert!(matches!(binding, Binding::Payment { .. }));
}

#[test]
fn nested_data_is_paid() {
    let (id, out) = parse_file("webhook_paid_nested.json");
    assert_eq!(id, "paid:inv_1");
    let WebhookOutcome::Paid { received, .. } = out else {
        panic!("{out:?}");
    };
    assert_eq!(received.minor(), 1000);
}

#[test]
fn amount_field_without_paid_amount() {
    let (_, out) = parse_file("webhook_paid_amount_field.json");
    let WebhookOutcome::Paid { received, .. } = out else {
        panic!("{out:?}");
    };
    assert_eq!(received.minor(), 1000);
}

#[test]
fn settled_is_ignored() {
    let (id, out) = parse_file("webhook_settled.json");
    assert_eq!(id, "settled:inv_1");
    assert!(matches!(out, WebhookOutcome::Ignored { .. }));
    assert_eq!(ignore_detail(&id), "settled");
}

#[test]
fn mint_amount_is_major_10_not_1000() {
    let quoted = Money::from_quoted_str("10.00", Currency::MYR).unwrap();
    assert_eq!(quoted.minor(), 1000);
    let body = invoice_body(
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "t1",
        "ada@acme.test",
        json!(10),
        "MYR",
        "https://ok.test",
        "https://no.test",
    );
    assert_eq!(body["amount"], 10);
    assert_ne!(body["amount"], 1000);
    let dumped = body.to_string();
    assert!(!dumped.contains("\"amount\":1000"));
}

#[test]
fn idr_10_is_still_wire_10() {
    let quoted = Money::from_quoted_str("10.00", Currency::IDR).unwrap();
    assert_eq!(quoted.minor(), 1000);
    let body = invoice_body(
        "id",
        "t1",
        "a@b.c",
        json!(10),
        "IDR",
        "https://ok",
        "https://no",
    );
    assert_eq!(body["amount"], 10);
}

#[test]
fn bad_token_is_error() {
    let body = std::fs::read_to_string("tests/fixtures/xendit/webhook_paid.json").unwrap();
    assert_eq!(
        parse_webhook(body.as_bytes(), Some("nope"), SECRET).unwrap_err(),
        XenditParseError::InvalidSignature
    );
}

#[test]
fn missing_token_is_error() {
    let body = std::fs::read_to_string("tests/fixtures/xendit/webhook_paid.json").unwrap();
    assert_eq!(
        parse_webhook(body.as_bytes(), None, SECRET).unwrap_err(),
        XenditParseError::InvalidSignature
    );
}

#[test]
fn empty_body_is_invalid_event() {
    assert_eq!(
        parse_webhook(b"  ", Some(SECRET), SECRET).unwrap_err(),
        XenditParseError::InvalidEvent
    );
}

#[test]
fn token_ok_hashes_first() {
    assert!(token_ok(SECRET, SECRET));
    assert!(!token_ok("tok", SECRET));
    assert!(!token_ok("", SECRET));
}

#[test]
fn psync_paid_has_invoice() {
    let body = std::fs::read_to_string("tests/fixtures/xendit/psync_paid.json").unwrap();
    let SyncOutcome::Paid { received, refs } = map_sync(200, &body) else {
        panic!("expected paid");
    };
    assert_eq!(received.minor(), 1000);
    assert_eq!(refs.session_id.as_deref(), Some("inv_1"));
    assert_eq!(refs.capture_id.as_deref(), refs.session_id.as_deref());
}

#[test]
fn psync_pending_and_expired_are_unknown() {
    let pending = std::fs::read_to_string("tests/fixtures/xendit/psync_pending.json").unwrap();
    let expired = std::fs::read_to_string("tests/fixtures/xendit/psync_expired.json").unwrap();
    assert!(matches!(map_sync(200, &pending), SyncOutcome::Unknown));
    assert!(matches!(map_sync(200, &expired), SyncOutcome::Unknown));
    assert!(matches!(
        map_sync(200, r#"{"status":"SETTLED"}"#),
        SyncOutcome::Unknown
    ));
}

#[test]
fn psync_404_is_failed() {
    assert!(matches!(map_sync(404, "{}"), SyncOutcome::Failed { .. }));
}

#[test]
fn mint_response_invoice_url() {
    let body = std::fs::read_to_string("tests/fixtures/xendit/mint_response.json").unwrap();
    let s = parse_hosted(&body).unwrap();
    assert!(s.session_id.starts_with("inv_"));
    assert!(s.url.starts_with("https://"));
}

#[test]
fn host_is_always_api_xendit() {
    assert_eq!(API_BASE, "https://api.xendit.co");
}

#[test]
fn invoice_body_shape() {
    let body = invoice_body(
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "t1",
        "ada@acme.test",
        json!(10),
        "MYR",
        "https://ok.test",
        "https://no.test",
    );
    assert_eq!(body["external_id"], "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
    assert_eq!(body["metadata"]["checkout_id"], body["external_id"]);
    assert_eq!(body["payer_email"], "ada@acme.test");
}
