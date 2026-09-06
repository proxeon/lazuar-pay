use domain::proof::{Binding, SyncOutcome, WebhookOutcome};
use rails::billplz::{
    api_host, ignore_detail, map_sync, parse_form, parse_hosted, parse_webhook, public_base_ok,
    sign_form, BillplzParseError, LIVE_HOST, SANDBOX_HOST,
};

const SECRET: &str = "xsig";

fn signed(unsigned: &str) -> String {
    let form = parse_form(unsigned);
    let mac = sign_form(&form, SECRET);
    format!("{unsigned}&x_signature={mac}")
}

fn parse_file(name: &str) -> (String, WebhookOutcome) {
    let unsigned = std::fs::read_to_string(format!("tests/fixtures/billplz/{name}")).unwrap();
    let body = signed(unsigned.trim());
    parse_webhook(body.as_bytes(), SECRET).unwrap()
}

#[test]
fn paid_minor_is_1000_and_session_eq_capture() {
    let (id, out) = parse_file("webhook_paid.form");
    assert_eq!(id, "paid:bill_1");
    let WebhookOutcome::Paid { received, refs, .. } = out else {
        panic!("{out:?}");
    };
    assert_eq!(received.minor(), 1000);
    assert_eq!(received.currency().code.as_str(), "MYR");
    assert_eq!(refs.session_id.as_deref(), Some("bill_1"));
    assert_eq!(refs.capture_id.as_deref(), Some("bill_1"));
}

#[test]
fn paid_binds_reference_1() {
    let (_, out) = parse_file("webhook_paid.form");
    let WebhookOutcome::Paid { binding, .. } = out else {
        panic!();
    };
    assert!(matches!(binding, Binding::Payment { .. }));
}

#[test]
fn paid_without_currency_defaults_myr() {
    let (_, out) = parse_file("webhook_paid_no_currency.form");
    let WebhookOutcome::Paid { received, .. } = out else {
        panic!("{out:?}");
    };
    assert_eq!(received.minor(), 1000);
    assert_eq!(received.currency().code.as_str(), "MYR");
}

#[test]
fn unpaid_is_ignored() {
    let (id, out) = parse_file("webhook_unpaid.form");
    assert_eq!(id, "unpaid:bill_u");
    assert!(matches!(out, WebhookOutcome::Ignored { .. }));
    assert_eq!(ignore_detail(&out), "unpaid");
}

#[test]
fn two_pass_hmac_accepts_paid_at() {
    let (id, out) = parse_file("webhook_paid_with_extra.form");
    assert_eq!(id, "paid:bill_1");
    assert!(matches!(out, WebhookOutcome::Paid { .. }));
    // Signature computed without extra fields, body includes paid_at.
    let base = parse_form("id=bill_1&paid=true&state=paid&paid_amount=1000&currency=MYR");
    let mac = sign_form(&base, SECRET);
    let with_extra = format!(
        "id=bill_1&paid=true&state=paid&paid_amount=1000&currency=MYR&paid_at=2026-01-01T00:00:00Z&x_signature={mac}"
    );
    let (id2, out2) = parse_webhook(with_extra.as_bytes(), SECRET).unwrap();
    assert_eq!(id2, "paid:bill_1");
    assert!(matches!(out2, WebhookOutcome::Paid { .. }));
}

#[test]
fn bad_sig_is_error() {
    let body = "id=bill_1&paid=true&paid_amount=1000&x_signature=deadbeef";
    assert_eq!(
        parse_webhook(body.as_bytes(), SECRET).unwrap_err(),
        BillplzParseError::InvalidSignature
    );
}

#[test]
fn missing_sig_is_error() {
    let body = "id=bill_1&paid=true&paid_amount=1000";
    assert_eq!(
        parse_webhook(body.as_bytes(), SECRET).unwrap_err(),
        BillplzParseError::InvalidSignature
    );
}

#[test]
fn empty_body_is_invalid_event() {
    assert_eq!(
        parse_webhook(b"  ", SECRET).unwrap_err(),
        BillplzParseError::InvalidEvent
    );
}

#[test]
fn psync_paid_has_bill() {
    let body = std::fs::read_to_string("tests/fixtures/billplz/psync_paid.json").unwrap();
    let SyncOutcome::Paid { received, refs } = map_sync(200, &body) else {
        panic!("expected paid");
    };
    assert_eq!(received.minor(), 1000);
    assert_eq!(refs.session_id.as_deref(), Some("bill_1"));
    assert_eq!(refs.capture_id.as_deref(), refs.session_id.as_deref());
}

#[test]
fn psync_due_is_unknown() {
    let body = std::fs::read_to_string("tests/fixtures/billplz/psync_due.json").unwrap();
    assert!(matches!(map_sync(200, &body), SyncOutcome::Unknown));
}

#[test]
fn mint_response_bill_and_url() {
    let body = std::fs::read_to_string("tests/fixtures/billplz/mint_response.json").unwrap();
    let s = parse_hosted(&body).unwrap();
    assert!(s.session_id.starts_with("bill_"));
    assert!(s.url.starts_with("https://"));
}

#[test]
fn host_follows_environment() {
    assert!(api_host("test").contains("sandbox"));
    assert_eq!(api_host("live"), LIVE_HOST);
    assert_eq!(api_host("TEST"), SANDBOX_HOST);
}

#[test]
fn public_base_rejects_localhost() {
    assert!(public_base_ok("https://pay.example.test"));
    assert!(public_base_ok("HTTPS://pay.example.test"));
    assert!(!public_base_ok("http://localhost:8081"));
    assert!(!public_base_ok("https://localhost"));
    assert!(!public_base_ok("https://127.0.0.1"));
    assert!(!public_base_ok("https://127.0.0.2"));
    assert!(!public_base_ok("https://[::1]"));
    assert!(!public_base_ok("https://[::1]:443"));
    assert!(!public_base_ok("https://foo.lazuar-local-dev.com"));
    assert!(!public_base_ok("https://FOO.LAZUAR-LOCAL-DEV.COM"));
}

#[test]
fn missing_bill_id_after_good_sig() {
    let body = signed("paid=true&paid_amount=1000");
    assert_eq!(
        parse_webhook(body.as_bytes(), SECRET).unwrap_err(),
        BillplzParseError::MissingBillId
    );
}

#[test]
fn unknown_currency_is_invalid_event() {
    let body = signed("id=bill_1&paid=true&paid_amount=1000&currency=ZZZ");
    assert_eq!(
        parse_webhook(body.as_bytes(), SECRET).unwrap_err(),
        BillplzParseError::InvalidEvent
    );
}

#[test]
fn psync_404_is_failed() {
    assert!(matches!(map_sync(404, "{}"), SyncOutcome::Failed { .. }));
    assert!(matches!(map_sync(500, "{}"), SyncOutcome::Unknown));
}
