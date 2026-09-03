//! Rail webhook verification — signature schemes, negatives, and the issue-018
//! Razorpay replay id semantics, all fixture-driven (D004).

use base64::Engine as _;
use hmac::{Hmac, Mac};
use lazuar_api::rails::billplz_webhook;
use lazuar_api::rails::chip_webhook;
use lazuar_api::rails::razorpay_webhook;
use lazuar_api::rails::stripe_webhook;
use lazuar_api::rails::test_webhook;
use lazuar_api::rails::xendit_webhook;
use lazuar_api::secrets::SecretBox;
use lazuar_api::webhooks::psp_parse::{Headers, WebhookParseError};
use sha2::Sha256;

fn headers(list: &[(&str, &str)]) -> Vec<(String, String)> {
    list.iter().map(|(k, v)| (k.to_string(), v.to_string())).collect()
}

fn secret_box() -> SecretBox {
    SecretBox::from_env_testing(None).unwrap()
}

fn hmac_hex(secret: &str, body: &str) -> String {
    let mut mac = <Hmac<Sha256> as Mac>::new_from_slice(secret.as_bytes()).unwrap();
    mac.update(body.as_bytes());
    hex::encode(mac.finalize().into_bytes())
}

// ---------------------------------------------------------------------------
// Test rail
// ---------------------------------------------------------------------------

#[test]
fn test_rail_valid_signature_parses_paid_event() {
    let secret = "test_whsec_local";
    let body = r#"{"id":"evt_1","checkout_id":"co_1","amount_total":990,"currency":"MYR"}"#;
    let hs = headers(&[("X-Pay-Test-Signature", &hmac_hex(secret, body))]);
    let parsed = test_webhook::parse(body, &Headers(&hs), secret).unwrap();
    assert_eq!(parsed.event_id, "evt_1");
    assert_eq!(parsed.checkout_id.as_deref(), Some("co_1"));
    assert_eq!(parsed.amount_minor, Some(990));
    assert_eq!(parsed.currency.as_deref(), Some("MYR"));
    assert!(!parsed.failed);
}

#[test]
fn test_rail_rejects_bad_signature_and_missing_amount() {
    let secret = "test_whsec_local";
    let body = r#"{"id":"evt_1","checkout_id":"co_1","amount_total":990,"currency":"MYR"}"#;
    // Tampered body under a valid-for-other-body signature.
    let hs = headers(&[("X-Pay-Test-Signature", &hmac_hex(secret, r#"{"id":"evt_1","checkout_id":"co_2","amount_total":990,"currency":"MYR"}"#))]);
    assert!(matches!(
        test_webhook::parse(body, &Headers(&hs), secret),
        Err(WebhookParseError::Verify(_))
    ));
    // Missing signature header entirely.
    assert!(matches!(
        test_webhook::parse(body, &Headers(&[]), secret),
        Err(WebhookParseError::Verify(_))
    ));
    // Failed events may omit the amount.
    let failed_body = r#"{"id":"evt_2","checkout_id":"co_1","status":"failed"}"#;
    let hs = headers(&[("X-Pay-Test-Signature", &hmac_hex(secret, failed_body))]);
    let parsed = test_webhook::parse(failed_body, &Headers(&hs), secret).unwrap();
    assert!(parsed.failed);
    assert_eq!(parsed.ignore_reason.as_deref(), Some("payment_failed"));
}

// ---------------------------------------------------------------------------
// Stripe
// ---------------------------------------------------------------------------

#[test]
fn stripe_valid_signature_parses_completed_session() {
    let secret = "whsec_test_local";
    let payload = r#"{"id":"evt_stripe_1","type":"checkout.session.completed","data":{"object":{"id":"cs_1","mode":"payment","amount_total":990,"currency":"myr","payment_status":"paid","client_reference_id":"co_9","metadata":{"checkout_id":"co_9"}}}}"#;
    let sig = stripe_webhook::sign_fixture(secret, payload, chrono::Utc::now().timestamp());
    let hs = headers(&[("Stripe-Signature", &sig)]);
    let parsed = stripe_webhook::parse(payload, &Headers(&hs), secret).unwrap();
    assert_eq!(parsed.event_id, "evt_stripe_1");
    assert_eq!(parsed.checkout_id.as_deref(), Some("co_9"));
    assert_eq!(parsed.amount_minor, Some(990)); // already minor — never ToMinor again
    assert_eq!(parsed.currency.as_deref(), Some("MYR"));
}

#[test]
fn stripe_tampered_body_and_stale_timestamp_rejected() {
    let secret = "whsec_test_local";
    let payload = r#"{"id":"evt_s","type":"checkout.session.completed","data":{"object":{"id":"cs_1","amount_total":990,"currency":"MYR","payment_status":"paid"}}}"#;

    // Tampered body.
    let sig = stripe_webhook::sign_fixture(secret, r#"{"other":true}"#, chrono::Utc::now().timestamp());
    let hs = headers(&[("Stripe-Signature", &sig)]);
    assert!(matches!(
        stripe_webhook::parse(payload, &Headers(&hs), secret),
        Err(WebhookParseError::Verify(_))
    ));

    // Stale timestamp outside the 300s tolerance.
    let sig = stripe_webhook::sign_fixture(secret, payload, chrono::Utc::now().timestamp() - 3600);
    let hs = headers(&[("Stripe-Signature", &sig)]);
    assert!(matches!(
        stripe_webhook::parse(payload, &Headers(&hs), secret),
        Err(WebhookParseError::Verify(_))
    ));

    // Unpaid payment_status is ignored, not fulfilled.
    let unpaid = r#"{"id":"evt_u","type":"checkout.session.completed","data":{"object":{"id":"cs_2","amount_total":990,"currency":"MYR","payment_status":"unpaid"}}}"#;
    let sig = stripe_webhook::sign_fixture(secret, unpaid, chrono::Utc::now().timestamp());
    let hs = headers(&[("Stripe-Signature", &sig)]);
    let parsed = stripe_webhook::parse(unpaid, &Headers(&hs), secret).unwrap();
    assert!(parsed.ignored);
}

// ---------------------------------------------------------------------------
// Billplz
// ---------------------------------------------------------------------------

#[test]
fn billplz_valid_hmac_parses_paid_bill() {
    let box_one = secret_box();
    let wrapped = box_one.protect("billplz_secret");
    let mut form = billplz_webhook::Form::new();
    form.insert("id".into(), "bill_1".into());
    form.insert("paid".into(), "true".into());
    form.insert("paid_amount".into(), "1000".into());
    form.insert("currency".into(), "MYR".into());
    form.insert("reference_1".into(), "co_bp".into());
    let sig = billplz_webhook::compute_hmac(&form, "billplz_secret", false);
    let raw = "id=bill_1&paid=true&paid_amount=1000&currency=MYR&reference_1=co_bp&x_signature=".to_string() + &sig;

    let parsed = billplz_webhook::parse(&raw, Some(&wrapped), &box_one).unwrap();
    assert_eq!(parsed.event_id, "paid:bill_1");
    assert_eq!(parsed.checkout_id.as_deref(), Some("co_bp"));
    assert_eq!(parsed.amount_minor, Some(1000)); // sen — never ToMinor again
    assert_eq!(parsed.hosted_session_id.as_deref(), Some("bill_1"));
}

#[test]
fn billplz_wrong_signature_rejected() {
    let box_one = secret_box();
    let wrapped = box_one.protect("billplz_secret");
    let raw = "id=bill_1&paid=true&paid_amount=1000&currency=MYR&reference_1=co_bp&x_signature=deadbeef";
    assert!(matches!(
        billplz_webhook::parse(raw, Some(&wrapped), &box_one),
        Err(WebhookParseError::Verify(_))
    ));
    // Unsigned (no x_signature) is also rejected.
    let raw = "id=bill_1&paid=true&paid_amount=1000&currency=MYR";
    assert!(matches!(
        billplz_webhook::parse(raw, Some(&wrapped), &box_one),
        Err(WebhookParseError::Verify(_))
    ));
}

// ---------------------------------------------------------------------------
// Xendit
// ---------------------------------------------------------------------------

#[test]
fn xendit_valid_token_parses_paid_invoice() {
    let box_one = secret_box();
    let wrapped = box_one.protect("xendit_token");
    let body = r#"{"id":"inv_1","status":"PAID","currency":"IDR","paid_amount":15000,"external_id":"co_x","metadata":{"checkout_id":"co_x"}}"#;
    let hs = headers(&[("x-callback-token", "xendit_token")]);
    let parsed = xendit_webhook::parse(body, &Headers(&hs), Some(&wrapped), &box_one).unwrap();
    assert_eq!(parsed.event_id, "paid:inv_1");
    assert_eq!(parsed.checkout_id.as_deref(), Some("co_x"));
    assert_eq!(parsed.currency.as_deref(), Some("IDR"));
}

#[test]
fn xendit_wrong_token_rejected() {
    let box_one = secret_box();
    let wrapped = box_one.protect("xendit_token");
    let body = r#"{"id":"inv_1","status":"PAID","currency":"IDR","paid_amount":15000,"external_id":"co_x"}"#;
    let hs = headers(&[("x-callback-token", "attacker_token")]);
    assert!(matches!(
        xendit_webhook::parse(body, &Headers(&hs), Some(&wrapped), &box_one),
        Err(WebhookParseError::Verify(_))
    ));
}

// ---------------------------------------------------------------------------
// Razorpay — including issue 018's rotating event id
// ---------------------------------------------------------------------------

#[test]
fn razorpay_captured_with_header_event_id_parses() {
    let box_one = secret_box();
    let wrapped = box_one.protect("rzp_secret");
    let body = r#"{"event":"payment.captured","payload":{"payment":{"entity":{"id":"pay_1","amount":99000,"currency":"INR","notes":{"checkout_id":"co_r"}}}}}"#;
    let sig = hmac_hex("rzp_secret", body);
    let hs = headers(&[("X-Razorpay-Signature", &sig), ("X-Razorpay-Event-Id", "evt_hdr_1")]);
    let parsed = razorpay_webhook::parse(body, &Headers(&hs), Some(&wrapped), &box_one).unwrap();
    assert_eq!(parsed.event_id, "evt_hdr_1", "header id wins as the dedupe id (issue 018)");
    assert_eq!(parsed.checkout_id.as_deref(), Some("co_r"));
    assert_eq!(parsed.amount_minor, Some(99000));
}

#[test]
fn razorpay_replay_with_rotating_header_id_still_yields_dedupe_id() {
    let box_one = secret_box();
    let wrapped = box_one.protect("rzp_secret");
    // A replayed capture (same body) presented with a DIFFERENT header event id:
    // parse must surface that header id as event_id — the ingest dedupe then
    // treats it as a brand-new event (matching C# semantics), which is why the
    // issue-018 resolution routes through the same-key refund guards downstream.
    let body = r#"{"event":"payment.captured","payload":{"payment":{"entity":{"id":"pay_1","amount":99000,"currency":"INR","notes":{"checkout_id":"co_r"}}}}}"#;
    for header_id in ["evt_hdr_1", "evt_hdr_2_rotated"] {
        let sig = hmac_hex("rzp_secret", body);
        let hs = headers(&[("X-Razorpay-Signature", &sig), ("X-Razorpay-Event-Id", header_id)]);
        let parsed = razorpay_webhook::parse(body, &Headers(&hs), Some(&wrapped), &box_one).unwrap();
        assert_eq!(parsed.event_id, header_id);
    }
    // Without a header id, the derived id is stable (captured:<payment id>).
    let sig = hmac_hex("rzp_secret", body);
    let hs = headers(&[("X-Razorpay-Signature", &sig)]);
    let parsed = razorpay_webhook::parse(body, &Headers(&hs), Some(&wrapped), &box_one).unwrap();
    assert_eq!(parsed.event_id, "captured:pay_1");
}

#[test]
fn razorpay_missing_or_invalid_signature_rejected() {
    let box_one = secret_box();
    let wrapped = box_one.protect("rzp_secret");
    let body = r#"{"event":"payment.captured","payload":{"payment":{"entity":{"id":"pay_1","amount":99000,"currency":"INR"}}}}"#;
    // Missing signature header — the negative case the C# suite never had.
    assert!(matches!(
        razorpay_webhook::parse(body, &Headers(&[]), Some(&wrapped), &box_one),
        Err(WebhookParseError::Verify(_))
    ));
    // Wrong signature.
    let hs = headers(&[("X-Razorpay-Signature", "f00d")]);
    assert!(matches!(
        razorpay_webhook::parse(body, &Headers(&hs), Some(&wrapped), &box_one),
        Err(WebhookParseError::Verify(_))
    ));
}

// ---------------------------------------------------------------------------
// CHIP — RSA verify with a generated keypair (mechanics identical at any size)
// ---------------------------------------------------------------------------

#[test]
fn chip_valid_rsa_signature_parses_paid_purchase() {
    use rsa::pkcs1v15::{Signature, SigningKey};
    use rsa::pkcs8::EncodePublicKey;
    use rsa::signature::{RandomizedSigner, SignatureEncoding};
    #[allow(unused_imports)] use rsa::signature::SignatureEncoding as _;
    use rsa::RsaPrivateKey;
    use sha2::Sha256;

    let mut rng = rand::thread_rng();
    let private_key = RsaPrivateKey::new(&mut rng, 2048).expect("generate test keypair");
    let public_key = RsaPrivateKey::to_public_key(&private_key);
    let signing = SigningKey::<Sha256>::new(private_key);

    let box_one = secret_box();
    let pem = public_key
        .to_public_key_pem(rsa::pkcs8::LineEnding::LF)
        .expect("encode pem");
    let wrapped = box_one.protect(pem.as_str());

    let body = r#"{"event_type":"purchase.paid","purchase":{"id":"pu_1","total":1000,"currency":"MYR","metadata":{"checkout_id":"co_chip"}}}"#;
    let signature: Signature = signing.sign_with_rng(&mut rng, body.as_bytes());
    // CHIP wires base64 — encode the raw DER explicitly (to_string() is hex).
    let sig_b64 = base64::engine::general_purpose::STANDARD.encode(signature.to_vec());
    let hs = headers(&[("X-Signature", &sig_b64)]);

    let parsed = chip_webhook::parse(body, &Headers(&hs), Some(&wrapped), &box_one).unwrap();
    assert_eq!(parsed.event_id, "paid:pu_1");
    assert_eq!(parsed.checkout_id.as_deref(), Some("co_chip"));
    assert_eq!(parsed.amount_minor, Some(1000)); // sen — never ToMinor again
}

#[test]
fn chip_tampered_body_rejected() {
    use rsa::pkcs1v15::{Signature, SigningKey};
    use rsa::pkcs8::EncodePublicKey;
    use rsa::signature::{RandomizedSigner, SignatureEncoding};
    #[allow(unused_imports)] use rsa::signature::SignatureEncoding as _;
    use rsa::RsaPrivateKey;
    use sha2::Sha256;

    let mut rng = rand::thread_rng();
    let private_key = RsaPrivateKey::new(&mut rng, 2048).expect("generate test keypair");
    let public_key = RsaPrivateKey::to_public_key(&private_key);
    let signing = SigningKey::<Sha256>::new(private_key);

    let box_one = secret_box();
    let pem = public_key
        .to_public_key_pem(rsa::pkcs8::LineEnding::LF)
        .expect("encode pem");
    let wrapped = box_one.protect(pem.as_str());

    let signed_body = r#"{"event_type":"purchase.paid","purchase":{"id":"pu_1","total":1000,"currency":"MYR"}}"#;
    let signature = signing.sign_with_rng(&mut rng, signed_body.as_bytes());
    let sig_b64 = signature.to_string();

    // Different body under a valid signature → rejected.
    let tampered = r#"{"event_type":"purchase.paid","purchase":{"id":"pu_2","total":100000,"currency":"MYR"}}"#;
    let hs = headers(&[("X-Signature", &sig_b64)]);
    assert!(matches!(
        chip_webhook::parse(tampered, &Headers(&hs), Some(&wrapped), &box_one),
        Err(WebhookParseError::Verify(_))
    ));
}
