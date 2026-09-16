use domain::proof::{Binding, IgnoreReason, SyncOutcome, WebhookOutcome};
use rails::chip::{
    ignore_detail, map_refund, map_sync, parse_hosted, parse_webhook, pem_ok, purchase_body, sign,
    ChipParseError,
};

fn private_pem() -> String {
    std::fs::read_to_string("tests/fixtures/chip/test_private.pem").unwrap()
}

fn public_pem() -> String {
    std::fs::read_to_string("tests/fixtures/chip/test_public.pem").unwrap()
}

fn parse_file(name: &str) -> (String, WebhookOutcome) {
    let body = std::fs::read_to_string(format!("tests/fixtures/chip/{name}")).unwrap();
    let sig = sign(&private_pem(), body.as_bytes()).unwrap();
    parse_webhook(body.as_bytes(), Some(&sig), &public_pem()).unwrap()
}

#[test]
fn public_pem_is_2048() {
    assert!(pem_ok(&public_pem()));
    assert!(!pem_ok("nope"));
    assert!(!pem_ok(
        "-----BEGIN PUBLIC KEY-----\nM\n-----END PUBLIC KEY-----"
    ));
}

#[test]
fn paid_minor_is_1000_and_session_eq_capture() {
    let (id, out) = parse_file("webhook_paid.json");
    assert_eq!(id, "paid:purch_1");
    let WebhookOutcome::Paid { received, refs, .. } = out else {
        panic!("{out:?}");
    };
    assert_eq!(received.minor(), 1000);
    assert_eq!(received.currency().code.as_str(), "MYR");
    assert_eq!(refs.session_id.as_deref(), Some("purch_1"));
    assert_eq!(refs.capture_id.as_deref(), Some("purch_1"));
}

#[test]
fn paid_binding_is_payment_id() {
    let (_, out) = parse_file("webhook_paid.json");
    let WebhookOutcome::Paid { binding, .. } = out else {
        panic!();
    };
    assert!(matches!(binding, Binding::Payment { .. }));
}

#[test]
fn paid_without_metadata_binds_session() {
    let (_, out) = parse_file("webhook_paid_no_meta.json");
    let WebhookOutcome::Paid { binding, .. } = out else {
        panic!();
    };
    assert!(matches!(binding, Binding::Session { .. }));
}

#[test]
fn preauthorized_is_ignored_not_paid() {
    let (id, out) = parse_file("webhook_preauthorized.json");
    assert_eq!(id, "preauth:purch_1");
    assert!(matches!(
        out,
        WebhookOutcome::Ignored {
            reason: IgnoreReason::Preauthorized
        }
    ));
    assert_eq!(
        ignore_detail(&out, "purchase.preauthorized"),
        "preauthorized"
    );
}

#[test]
fn payment_failure_is_failed() {
    let (id, out) = parse_file("webhook_payment_failure.json");
    assert_eq!(id, "failed:purch_1");
    assert!(matches!(out, WebhookOutcome::Failed { .. }));
}

#[test]
fn other_type_is_ignored() {
    let (_, out) = parse_file("webhook_other_type.json");
    assert!(matches!(out, WebhookOutcome::Ignored { .. }));
}

#[test]
fn bad_sig_is_error() {
    let body = std::fs::read_to_string("tests/fixtures/chip/webhook_paid.json").unwrap();
    let err = parse_webhook(body.as_bytes(), Some("aaaa"), &public_pem()).unwrap_err();
    assert_eq!(err, ChipParseError::InvalidSignature);
}

#[test]
fn missing_sig_is_error() {
    let body = b"{}";
    assert_eq!(
        parse_webhook(body, None, &public_pem()).unwrap_err(),
        ChipParseError::InvalidSignature
    );
}

#[test]
fn empty_body_is_invalid_event() {
    assert_eq!(
        parse_webhook(b"  ", Some("x"), &public_pem()).unwrap_err(),
        ChipParseError::InvalidEvent
    );
}

#[test]
fn psync_paid_has_purch() {
    let body = std::fs::read_to_string("tests/fixtures/chip/psync_paid.json").unwrap();
    let SyncOutcome::Paid { received, refs } = map_sync(200, &body) else {
        panic!("expected paid");
    };
    assert_eq!(received.minor(), 1000);
    assert_eq!(refs.capture_id.as_deref(), refs.session_id.as_deref());
    assert_eq!(refs.session_id.as_deref(), Some("purch_1"));
}

#[test]
fn psync_pending_is_unknown() {
    let body = std::fs::read_to_string("tests/fixtures/chip/psync_pending.json").unwrap();
    assert!(matches!(map_sync(200, &body), SyncOutcome::Unknown));
}

#[test]
fn psync_expired_is_failed() {
    let body = std::fs::read_to_string("tests/fixtures/chip/psync_expired.json").unwrap();
    assert!(matches!(map_sync(200, &body), SyncOutcome::Failed { .. }));
}

#[test]
fn mint_response_purch_and_url() {
    let body = std::fs::read_to_string("tests/fixtures/chip/mint_response.json").unwrap();
    let s = parse_hosted(&body).unwrap();
    assert!(s.session_id.starts_with("purch_"));
    assert!(s.url.starts_with("https://"));
}

#[test]
fn purchase_body_price_is_sen_no_force_recurring() {
    let body = purchase_body(
        "abc",
        "t1",
        "brand_1",
        "ada@acme.test",
        "Ada",
        1000,
        "MYR",
        "https://s",
        "https://c",
    );
    assert_eq!(
        body.pointer("/purchase/products/0/price")
            .and_then(|v| v.as_i64()),
        Some(1000)
    );
    let dumped = body.to_string();
    assert!(!dumped.contains("force_recurring"));
    assert!(dumped.contains("ada@acme.test"));
}

#[test]
fn refund_5xx_unknown_4xx_rejected() {
    let five = std::fs::read_to_string("tests/fixtures/chip/refund_5xx.json").unwrap();
    let four = std::fs::read_to_string("tests/fixtures/chip/refund_4xx.json").unwrap();
    assert_eq!(map_refund(500, &five), domain::RefundOutcome::Unknown);
    assert_eq!(map_refund(400, &four), domain::RefundOutcome::Rejected);
    assert_eq!(map_refund(200, "{}"), domain::RefundOutcome::Settled);
}
