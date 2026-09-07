use domain::money::{Currency, Money};
use rails::solana::{
    decode, decoy_json, encode, genesis_hash, is_on_ed25519, looks_like_secret, matches_vault,
    mint, parse_genesis_hash, parse_webhook, pay_uri, sample_address, try_normalize, try_to_atomic,
    tx_json, validate, DEVNET_GENESIS, DEVNET_MINT, MAINNET_GENESIS, MAINNET_MINT, MEMO_PROGRAM,
    TOKEN_2022_PROGRAM, TOKEN_PROGRAM, WEBHOOK_THROW,
};

fn usdc10() -> Money {
    Money::from_quoted_str("10.00", Currency::USDC_SOLANA).unwrap()
}

fn sig() -> String {
    encode(&[7u8; 64])
}

#[test]
fn uri_amount_is_display_10_not_atomic() {
    let vault = sample_address();
    let payment_id = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    let session = pay_uri(&vault, usdc10(), payment_id, "devnet").unwrap();
    assert!(session.url.starts_with(&format!("solana:{vault}")));
    assert!(session.url.contains("amount=10"));
    assert!(!session.url.contains("amount=10000000"));
    assert!(session.url.contains(&format!("spl-token={DEVNET_MINT}")));
    assert!(session.url.contains(&format!("memo={payment_id}")));
    assert!(session
        .url
        .contains(&format!("reference={}", session.session_id)));
    assert_eq!(try_to_atomic(usdc10()).unwrap(), 10_000_000);
}

#[test]
fn validate_paid_atomic_10000000() {
    let owner = sample_address();
    let reference = encode(&[3u8; 32]);
    let signature = sig();
    let memo = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    let json = tx_json(
        &signature,
        &owner,
        DEVNET_MINT,
        "10000000",
        &reference,
        memo,
    );
    validate(
        &json,
        &owner,
        10_000_000,
        DEVNET_MINT,
        &reference,
        memo,
        &signature,
    )
    .unwrap();
}

#[test]
fn validate_wrong_mint() {
    let owner = sample_address();
    let reference = encode(&[3u8; 32]);
    let signature = sig();
    let memo = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    let json = tx_json(
        &signature,
        &owner,
        MAINNET_MINT,
        "10000000",
        &reference,
        memo,
    );
    let err = validate(
        &json,
        &owner,
        10_000_000,
        DEVNET_MINT,
        &reference,
        memo,
        &signature,
    )
    .unwrap_err();
    assert_eq!(err.to_string(), "mint mismatch");
}

#[test]
fn validate_wrong_amount() {
    let owner = sample_address();
    let reference = encode(&[3u8; 32]);
    let signature = sig();
    let memo = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    let json = tx_json(&signature, &owner, DEVNET_MINT, "1000", &reference, memo);
    let err = validate(
        &json,
        &owner,
        10_000_000,
        DEVNET_MINT,
        &reference,
        memo,
        &signature,
    )
    .unwrap_err();
    assert_eq!(err.to_string(), "amount mismatch");
}

#[test]
fn validate_decoy_is_destination_mismatch() {
    let owner = sample_address();
    let reference = encode(&[3u8; 32]);
    let signature = sig();
    let memo = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    let json = decoy_json(
        &signature,
        &owner,
        DEVNET_MINT,
        "10000000",
        &reference,
        memo,
    );
    let err = validate(
        &json,
        &owner,
        10_000_000,
        DEVNET_MINT,
        &reference,
        memo,
        &signature,
    )
    .unwrap_err();
    assert_eq!(err.to_string(), "destination mismatch");
}

#[test]
fn validate_token2022_mismatch() {
    let owner = sample_address();
    let reference = encode(&[3u8; 32]);
    let signature = sig();
    let memo = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    let json = tx_json(
        &signature,
        &owner,
        DEVNET_MINT,
        "10000000",
        &reference,
        memo,
    )
    .replace(TOKEN_PROGRAM, TOKEN_2022_PROGRAM);
    let err = validate(
        &json,
        &owner,
        10_000_000,
        DEVNET_MINT,
        &reference,
        memo,
        &signature,
    )
    .unwrap_err();
    assert_eq!(err.to_string(), "token program mismatch");
}

#[test]
fn validate_failed_tx() {
    let json = r#"{"jsonrpc":"2.0","result":{"meta":{"err":{"InstructionError":[0,"Custom"]}},"transaction":{"message":{}}}}"#;
    let err = validate(json, "v", 1, "m", "r", "p", "s").unwrap_err();
    assert_eq!(err.to_string(), "transaction failed");
}

#[test]
fn webhook_always_throws() {
    let err = parse_webhook(b"{}", Some("sig"), "secret").unwrap_err();
    assert_eq!(err.to_string(), WEBHOOK_THROW);
}

#[test]
fn normalize_rejects_secrets_and_off_curve() {
    assert!(looks_like_secret("-----BEGIN PRIVATE KEY-----"));
    assert!(looks_like_secret("sk_test"));
    assert!(try_normalize("sk_test").is_none());
    let off = [2u8; 32];
    assert!(!is_on_ed25519(&off));
    assert!(try_normalize(&encode(&off)).is_none());
    let ok = sample_address();
    assert!(try_normalize(&ok).is_some());
    assert_eq!(decode(&encode(&[1, 2, 3])).as_deref(), Some(&[1, 2, 3][..]));
}

#[test]
fn mints_are_pinned() {
    assert_eq!(mint("devnet"), DEVNET_MINT);
    assert_eq!(mint("mainnet-beta"), MAINNET_MINT);
    assert!(MEMO_PROGRAM.starts_with("Memo"));
}

#[test]
fn genesis_hash_is_pinned_per_cluster() {
    assert_eq!(
        parse_genesis_hash(
            r#"{"jsonrpc":"2.0","result":"5eykt4UsFv8P8NJdTREpY1vzqKqZKvdpKuc147dw2N9d"}"#
        )
        .as_deref(),
        Some(MAINNET_GENESIS)
    );
    assert_eq!(genesis_hash("devnet"), DEVNET_GENESIS);
    assert_eq!(genesis_hash("mainnet-beta"), MAINNET_GENESIS);
    assert!(matches_vault("devnet", "devnet"));
    assert!(matches_vault("mainnet-beta", "mainnet"));
    assert!(!matches_vault("devnet", "mainnet"));
}
