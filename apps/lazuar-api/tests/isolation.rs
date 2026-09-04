//! Port of the meaningful C# IsolationTests locks for the Rust crate.

#[test]
fn no_tokio_in_the_crate() {
    let src = include_str!("../src/lib.rs");
    assert!(!src.contains("tokio"), "D001: no Tokio in the crate root");
}

#[test]
fn money_is_decimal_not_f64() {
    let refunds = include_str!("../src/money/refunds.rs");
    assert!(refunds.contains("rust_decimal::Decimal"));
    assert!(!refunds.contains("f64"));
    for (name, src) in [
        ("chip_webhook", include_str!("../src/rails/chip_webhook.rs")),
        ("xendit_webhook", include_str!("../src/rails/xendit_webhook.rs")),
        ("hosted", include_str!("../src/rails/hosted.rs")),
        ("remote", include_str!("../src/rails/remote.rs")),
    ] {
        assert!(
            !src.contains("as_f64") && !src.contains("f64"),
            "{name} must not parse money as f64"
        );
    }
}

#[test]
fn checkout_status_only_via_transitions() {
    let trans = include_str!("../src/domain/transitions.rs");
    assert!(trans.contains("try_leave_open") || trans.contains("try_transition"));
}
