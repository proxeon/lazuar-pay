use domain::proof::{Binding, SyncOutcome, WebhookOutcome};
use rails::razorpay::{
    ignore_detail, link_body, map_sync, parse_hosted, parse_webhook, sign, try_split,
    RazorpayParseError, API_BASE,
};
const SECRET: &str = "wh_rzp";

fn parse_file(name: &str) -> (String, WebhookOutcome) {
    let body = std::fs::read_to_string(format!("tests/fixtures/razorpay/{name}")).unwrap();
    let sig = sign(SECRET, body.trim().as_bytes());
    parse_webhook(body.trim().as_bytes(), Some(&sig), SECRET).unwrap()
}

#[test]
fn captured_paise_1000_not_10() {
    let (id, out) = parse_file("webhook_captured.json");
    assert_eq!(id, "captured:pay_1");
    let WebhookOutcome::Paid { received, refs, .. } = out else {
        panic!("{out:?}");
    };
    assert_eq!(received.minor(), 1000);
    assert_eq!(received.currency().code.as_str(), "INR");
    assert_eq!(refs.capture_id.as_deref(), Some("pay_1"));
}

#[test]
fn captured_binds_notes_checkout() {
    let (_, out) = parse_file("webhook_captured.json");
    let WebhookOutcome::Paid { binding, .. } = out else {
        panic!();
    };
    assert!(matches!(binding, Binding::Payment { .. }));
}

#[test]
fn link_paid_distinct_proof_id() {
    let (id, out) = parse_file("webhook_link_paid.json");
    assert_eq!(id, "link_paid:pay_lp");
    assert_ne!(id, "captured:pay_lp");
    let WebhookOutcome::Paid { refs, .. } = out else {
        panic!("{out:?}");
    };
    assert_eq!(refs.session_id.as_deref(), Some("plink_1"));
    assert_eq!(refs.capture_id.as_deref(), Some("pay_lp"));
}

#[test]
fn link_paid_without_notes_binds_session() {
    let (_, out) = parse_file("webhook_link_paid_no_notes.json");
    let WebhookOutcome::Paid { binding, refs, .. } = out else {
        panic!("{out:?}");
    };
    assert!(matches!(binding, Binding::Session { .. }));
    assert_eq!(refs.session_id.as_deref(), Some("plink_1"));
}

#[test]
fn captured_without_notes_binds_plink() {
    let (_, out) = parse_file("webhook_captured_no_notes.json");
    let WebhookOutcome::Paid { binding, refs, .. } = out else {
        panic!();
    };
    assert!(matches!(binding, Binding::Session { .. }));
    assert_eq!(refs.session_id.as_deref(), Some("plink_1"));
    assert_eq!(refs.capture_id.as_deref(), Some("pay_1"));
}

#[test]
fn failed_is_failed_not_ignored() {
    let (id, out) = parse_file("webhook_failed.json");
    assert_eq!(id, "failed:pay_1");
    assert!(matches!(out, WebhookOutcome::Failed { .. }));
}

#[test]
fn expired_is_ignored() {
    let (id, out) = parse_file("webhook_expired.json");
    assert_eq!(id, "payment_link.expired:plink_1");
    assert!(matches!(out, WebhookOutcome::Ignored { .. }));
    assert_eq!(ignore_detail(&id), "payment_link.expired");
}

#[test]
fn event_id_header_is_not_the_proof_id() {
    let body = std::fs::read_to_string("tests/fixtures/razorpay/webhook_captured.json").unwrap();
    let sig = sign(SECRET, body.trim().as_bytes());
    let (id, _) = parse_webhook(body.trim().as_bytes(), Some(&sig), SECRET).unwrap();
    assert_eq!(id, "captured:pay_1");
    assert!(!id.contains("evt_"));
}

#[test]
fn mint_amount_is_paise_1000_not_10() {
    let body = link_body(
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "t1",
        "ada@acme.test",
        "Ada",
        1000,
        "INR",
        "https://ok.test",
    );
    assert_eq!(body["amount"], 1000);
    assert_ne!(body["amount"], 10);
    assert_eq!(body["reference_id"], "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
}

#[test]
fn try_split_requires_colon() {
    assert!(try_split("nocolon").is_none());
    assert!(try_split(":secret").is_none());
    assert!(try_split("rzp_test:").is_none());
    let (id, sec) = try_split("rzp_test:secret").unwrap();
    assert_eq!(id, "rzp_test");
    assert_eq!(sec, "secret");
}

#[test]
fn bad_sig_is_error() {
    let body = std::fs::read_to_string("tests/fixtures/razorpay/webhook_captured.json").unwrap();
    assert_eq!(
        parse_webhook(body.as_bytes(), Some("deadbeef"), SECRET).unwrap_err(),
        RazorpayParseError::InvalidSignature
    );
}

#[test]
fn missing_sig_is_error() {
    let body = std::fs::read_to_string("tests/fixtures/razorpay/webhook_captured.json").unwrap();
    assert_eq!(
        parse_webhook(body.as_bytes(), None, SECRET).unwrap_err(),
        RazorpayParseError::InvalidSignature
    );
}

#[test]
fn empty_body_is_invalid_event() {
    let sig = sign(SECRET, b"  ");
    assert_eq!(
        parse_webhook(b"  ", Some(&sig), SECRET).unwrap_err(),
        RazorpayParseError::InvalidEvent
    );
}

#[test]
fn psync_paid_is_paise() {
    let body = std::fs::read_to_string("tests/fixtures/razorpay/psync_paid.json").unwrap();
    let SyncOutcome::Paid { received, refs } = map_sync(200, &body) else {
        panic!("expected paid");
    };
    assert_eq!(received.minor(), 1000);
    assert_eq!(refs.session_id.as_deref(), Some("plink_1"));
}

#[test]
fn psync_created_and_expired_are_unknown() {
    let created = std::fs::read_to_string("tests/fixtures/razorpay/psync_created.json").unwrap();
    let expired = std::fs::read_to_string("tests/fixtures/razorpay/psync_expired.json").unwrap();
    assert!(matches!(map_sync(200, &created), SyncOutcome::Unknown));
    assert!(matches!(map_sync(200, &expired), SyncOutcome::Unknown));
    assert!(matches!(
        map_sync(200, r#"{"status":"cancelled"}"#),
        SyncOutcome::Unknown
    ));
}

#[test]
fn psync_404_is_failed() {
    assert!(matches!(map_sync(404, "{}"), SyncOutcome::Failed { .. }));
}

#[test]
fn mint_response_short_url() {
    let body = std::fs::read_to_string("tests/fixtures/razorpay/mint_response.json").unwrap();
    let s = parse_hosted(&body).unwrap();
    assert!(s.session_id.starts_with("plink_"));
    assert!(s.url.starts_with("https://"));
}

#[test]
fn host_is_always_api_razorpay() {
    assert_eq!(API_BASE, "https://api.razorpay.com/v1");
}

#[test]
fn link_body_shape() {
    let body = link_body(
        "id",
        "t1",
        "ada@acme.test",
        "Ada",
        1000,
        "INR",
        "https://ok",
    );
    assert_eq!(body["customer"]["email"], "ada@acme.test");
    assert_eq!(body["callback_method"], "get");
    assert_eq!(body["notes"]["org_id"], "t1");
}
