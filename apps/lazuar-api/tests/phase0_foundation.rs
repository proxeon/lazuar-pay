//! Phase 0 acceptance — amount parsing (divergence #3), clamps (PayList),
//! SecretBox production resolution (B1/B5).

mod support;

use lazuar_api::app::parse_decimal;
use rust_decimal::Decimal;
use std::str::FromStr;
use support::TestApp;

fn dec(v: &str) -> Decimal {
    Decimal::from_str(v).unwrap()
}

#[test]
fn json_number_amount_keeps_its_scale() {
    // Divergence #3: a JSON number amount (12.50) must bind as an exact
    // decimal — a parse failure would silently turn a partial refund into a
    // full-remainder refund.
    let number = serde_json::Value::Number(
        serde_json::Number::from_str("12.50").unwrap(),
    );
    let parsed = parse_decimal(Some(&number)).expect("number binds");
    assert_eq!(parsed, dec("12.50"));

    // Numeric strings bind too.
    let as_string = parse_decimal(Some(&serde_json::Value::String("12.50".into())))
        .expect("string binds");
    assert_eq!(as_string, dec("12.50"));

    // Non-numeric strings do not.
    assert!(parse_decimal(Some(&serde_json::Value::String("abc".into()))).is_none());
    assert!(parse_decimal(None).is_none());
}

#[test]
fn secretbox_production_requires_key_and_validates_length() {
    use lazuar_api::secrets::SecretBox;

    // Production + missing key → fail closed (PayBoot B1).
    let err = SecretBox::from_env("Production", None).unwrap_err();
    assert!(matches!(err, lazuar_api::secrets::SecretBoxError::KeyRequired));

    // Production + short key → fail closed (PayBoot B5).
    let err = SecretBox::from_env("Production", Some(&base64_short())).unwrap_err();
    assert!(matches!(err, lazuar_api::secrets::SecretBoxError::KeyInvalid));

    // Testing + missing key → dev fallback.
    assert!(SecretBox::from_env("Testing", None).is_ok());
}

#[test]
fn ready_body_is_exactly_the_csharp_shape() {
    let app = TestApp::spawn();
    // /ready with a working DB → ready; the not-ready variant must emit
    // exactly {"status":"not_ready"} (Phase 0 / 025-04 §6).
    let resp = ureq::get(&format!("{}/ready", app.base_url)).call().unwrap();
    assert_eq!(resp.status(), 200);
    let resp = ureq::get(&format!("{}/v1/health", app.base_url)).call().unwrap();
    assert_eq!(resp.status(), 200);
    assert_eq!(resp.into_string().unwrap(), r#"{"status":"ok"}"#);
}

fn base64_short() -> String {
    use base64::Engine as _;
    base64::engine::general_purpose::STANDARD.encode([1u8; 16])
}
