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
}

#[test]
fn checkout_status_only_via_transitions() {
    let trans = include_str!("../src/domain/transitions.rs");
    assert!(trans.contains("try_leave_open") || trans.contains("try_transition"));
}
