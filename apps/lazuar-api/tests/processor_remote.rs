//! Live processor remote — Stripe PaymentIntent resolution and CHIP 5xx taxonomy.

mod support;

use lazuar_api::rails::remote::{ChargeRef, LiveRefunder, RefundRemoteError, Refunder};
use rust_decimal::Decimal;
use std::str::FromStr;
use std::sync::Arc;
use support::FakeTransport;

fn amount(v: &str) -> Decimal {
    Decimal::from_str(v).unwrap()
}

fn stripe_charge(session_or_pi: &str) -> ChargeRef {
    ChargeRef {
        id: "ch_1".into(),
        org_id: "org_1".into(),
        checkout_id: "co_1".into(),
        provider: Some("stripe".into()),
        provider_ref: Some(session_or_pi.into()),
        amount: amount("9.90"),
        currency: "MYR".into(),
        status: "succeeded".into(),
        provider_session_id: None,
    }
}

#[test]
fn stripe_refund_resolves_checkout_session_to_payment_intent() {
    let fake = Arc::new(FakeTransport::new("stripe"));
    fake.respond_with(|req| {
        if req.method == "GET" && req.url.contains("/v1/checkout/sessions/") {
            lazuar_api::transport::OutResponse {
                status: 200,
                body: r#"{"id":"cs_test_1","payment_intent":"pi_abc"}"#.into(),
            }
        } else {
            lazuar_api::transport::OutResponse {
                status: 200,
                body: r#"{"id":"re_1"}"#.into(),
            }
        }
    });
    let remote = LiveRefunder {
        transport: fake.clone() as Arc<dyn lazuar_api::transport::Transport>,
        secrets: [("stripe".into(), "sk_test".into())].into(),
        chip_session_id: None,
    };
    remote
        .refund_charge(&stripe_charge("cs_test_1"), amount("9.90"), "refund_1")
        .expect("stripe refund");

    let all = fake.all();
    assert!(
        all.iter().any(|r| r.method == "GET" && r.url.contains("/v1/checkout/sessions/cs_test_1")),
        "must GET the Checkout Session: {all:?}"
    );
    let refund = all
        .iter()
        .find(|r| r.url.contains("/v1/refunds"))
        .expect("POST /v1/refunds");
    let body = refund.body.as_deref().unwrap_or("");
    assert!(body.contains("payment_intent=pi_abc"), "body={body}");
    assert!(!body.contains("charge="), "must not refund the session id as a charge: {body}");
    assert!(
        refund
            .headers
            .iter()
            .any(|(k, v)| k.eq_ignore_ascii_case("idempotency-key") && v == "lazuar-refund:refund_1")
    );
}

#[test]
fn stripe_refund_skips_session_fetch_when_ref_is_already_a_payment_intent() {
    let fake = Arc::new(FakeTransport::new("stripe"));
    fake.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 200,
        body: r#"{"id":"re_1"}"#.into(),
    });
    let remote = LiveRefunder {
        transport: fake.clone() as Arc<dyn lazuar_api::transport::Transport>,
        secrets: [("stripe".into(), "sk_test".into())].into(),
        chip_session_id: None,
    };
    remote
        .refund_charge(&stripe_charge("pi_already"), amount("9.90"), "refund_2")
        .expect("pi_ refund");
    assert_eq!(fake.send_count(), 1, "no session GET when ref is pi_");
    let body = fake.last().unwrap().body.unwrap_or_default();
    assert!(body.contains("payment_intent=pi_already"), "body={body}");
}

#[test]
fn chip_5xx_is_outcome_unknown_not_a_definitive_reject() {
    let fake = Arc::new(FakeTransport::new("chip"));
    fake.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 503,
        body: "unavailable".into(),
    });
    let remote = LiveRefunder {
        transport: fake as Arc<dyn lazuar_api::transport::Transport>,
        secrets: [("chip".into(), "chip_sk".into())].into(),
        chip_session_id: Some("purchase_1".into()),
    };
    let charge = ChargeRef {
        id: "ch_1".into(),
        org_id: "org_1".into(),
        checkout_id: "co_1".into(),
        provider: Some("chip".into()),
        provider_ref: None,
        amount: amount("10.00"),
        currency: "MYR".into(),
        status: "succeeded".into(),
        provider_session_id: Some("purchase_1".into()),
    };
    let err = remote
        .refund_charge(&charge, amount("10.00"), "refund_chip")
        .expect_err("5xx must not look settled");
    assert!(
        matches!(err, RefundRemoteError::OutcomeUnknown(_)),
        "got {err:?}"
    );
}

#[test]
fn chip_4xx_is_processor_rejected() {
    let fake = Arc::new(FakeTransport::new("chip"));
    fake.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 400,
        body: "bad".into(),
    });
    let remote = LiveRefunder {
        transport: fake as Arc<dyn lazuar_api::transport::Transport>,
        secrets: [("chip".into(), "chip_sk".into())].into(),
        chip_session_id: Some("purchase_1".into()),
    };
    let charge = ChargeRef {
        id: "ch_1".into(),
        org_id: "org_1".into(),
        checkout_id: "co_1".into(),
        provider: Some("chip".into()),
        provider_ref: None,
        amount: amount("10.00"),
        currency: "MYR".into(),
        status: "succeeded".into(),
        provider_session_id: Some("purchase_1".into()),
    };
    let err = remote
        .refund_charge(&charge, amount("10.00"), "refund_chip")
        .expect_err("4xx is definitive");
    assert!(
        matches!(err, RefundRemoteError::ProcessorRejected(_)),
        "got {err:?}"
    );
}
