use domain::proof::{Binding, SyncOutcome, WebhookOutcome};
use rails::stripe::{
    checkout_form, map_refund, map_sync, parse_hosted, parse_webhook, sign, StripeParseError,
};

const WHSEC: &str = "whsec_test";
const NOW: i64 = 1_700_000_000;

fn parse_file(name: &str) -> (String, WebhookOutcome) {
    let body = std::fs::read_to_string(format!("tests/fixtures/stripe/{name}")).unwrap();
    let sig = sign(WHSEC, body.as_bytes(), NOW);
    parse_webhook(body.as_bytes(), Some(&sig), WHSEC, NOW).unwrap()
}

#[test]
fn paid_minor_is_1000_and_pi_not_cs() {
    let (id, out) = parse_file("webhook_paid.json");
    assert_eq!(id, "evt_paid_1");
    let WebhookOutcome::Paid { received, refs, .. } = out else {
        panic!("{out:?}");
    };
    assert_eq!(received.minor(), 1000);
    assert_eq!(received.currency().code.as_str(), "MYR");
    assert!(refs.session_id.as_deref().unwrap().starts_with("cs_"));
    assert!(refs.capture_id.as_deref().unwrap().starts_with("pi_"));
    assert!(!refs.capture_id.as_deref().unwrap().starts_with("cs_"));
}

#[test]
fn unpaid_completed_is_ignored() {
    let (_, out) = parse_file("webhook_completed_unpaid.json");
    assert!(matches!(out, WebhookOutcome::Ignored { .. }));
}

#[test]
fn async_failed_is_failed() {
    let (_, out) = parse_file("webhook_async_failed.json");
    assert!(matches!(out, WebhookOutcome::Failed { .. }));
}

#[test]
fn other_type_is_ignored() {
    let (_, out) = parse_file("webhook_other_type.json");
    assert!(matches!(out, WebhookOutcome::Ignored { .. }));
}

#[test]
fn bad_sig_is_error() {
    let body = std::fs::read_to_string("tests/fixtures/stripe/webhook_paid.json").unwrap();
    let err = parse_webhook(body.as_bytes(), Some("t=1,v1=deadbeef"), WHSEC, NOW).unwrap_err();
    assert_eq!(err, StripeParseError::InvalidSignature);
}

#[test]
fn missing_sig_is_error() {
    let body = b"{}";
    assert_eq!(
        parse_webhook(body, None, WHSEC, NOW).unwrap_err(),
        StripeParseError::InvalidSignature
    );
}

#[test]
fn psync_paid_has_pi() {
    let body = std::fs::read_to_string("tests/fixtures/stripe/psync_paid.json").unwrap();
    let SyncOutcome::Paid { received, refs } = map_sync(200, &body) else {
        panic!("expected paid");
    };
    assert_eq!(received.minor(), 1000);
    assert!(refs.capture_id.unwrap().starts_with("pi_"));
}

#[test]
fn psync_open_is_unknown() {
    let body = std::fs::read_to_string("tests/fixtures/stripe/psync_open.json").unwrap();
    assert!(matches!(map_sync(200, &body), SyncOutcome::Unknown));
}

#[test]
fn psync_expired_is_failed() {
    let body = std::fs::read_to_string("tests/fixtures/stripe/psync_expired.json").unwrap();
    assert!(matches!(map_sync(200, &body), SyncOutcome::Failed { .. }));
}

#[test]
fn mint_response_cs_and_url() {
    let body = std::fs::read_to_string("tests/fixtures/stripe/mint_response.json").unwrap();
    let s = parse_hosted(&body).unwrap();
    assert!(s.session_id.starts_with("cs_"));
    assert!(s.url.starts_with("https://"));
}

#[test]
fn checkout_form_unit_amount_is_minor() {
    let form = checkout_form("abc", "t1", 1000, "MYR", "https://s", "https://c");
    let ua = form
        .iter()
        .find(|(k, _)| k.contains("unit_amount"))
        .unwrap();
    assert_eq!(ua.1, "1000");
}

#[test]
fn refund_5xx_unknown_4xx_rejected() {
    let five = std::fs::read_to_string("tests/fixtures/stripe/refund_5xx.json").unwrap();
    let four = std::fs::read_to_string("tests/fixtures/stripe/refund_4xx.json").unwrap();
    assert_eq!(map_refund(500, &five), domain::RefundOutcome::Unknown);
    assert_eq!(map_refund(400, &four), domain::RefundOutcome::Rejected);
    assert_eq!(
        map_refund(400, r#"{"error":{"code":"charge_already_refunded"}}"#),
        domain::RefundOutcome::Settled
    );
}

#[test]
fn psync_404_is_failed() {
    assert!(matches!(
        map_sync(404, r#"{"error":{"type":"invalid_request_error"}}"#),
        SyncOutcome::Failed { .. }
    ));
}

#[test]
fn paid_binding_is_payment_id() {
    let (_, out) = parse_file("webhook_paid.json");
    let WebhookOutcome::Paid { binding, .. } = out else {
        panic!();
    };
    assert!(matches!(binding, Binding::Payment { .. }));
}
