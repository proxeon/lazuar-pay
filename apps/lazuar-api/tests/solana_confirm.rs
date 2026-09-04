//! Solana rail — tx validation and confirm flow against fixture RPC responses.

mod support;

use lazuar_api::money::fulfillment::CheckoutGates;
use lazuar_api::secrets::SecretBox;
use support::TestApp;
use lazuar_api::rails::solana::cluster;
use lazuar_api::rails::solana::confirm::{confirm, ConfirmDeps, ConfirmOutcome, Watcher};
use lazuar_api::rails::solana::rpc::SolanaRpc;
use lazuar_api::rails::solana::tx::{validate, ValidateInput};
use rust_decimal::Decimal;
use std::str::FromStr;
use support::FakeTransport;

fn real_signature() -> String {
    lazuar_api::rails::solana::base58::encode(b"solana-fixture-signature-001")
}

fn reference_pubkey() -> String {
    lazuar_api::rails::solana::base58::encode(b"reference-key-pubkey-fixture")
}

fn merchant_address() -> String {
    lazuar_api::rails::solana::base58::encode(b"merchant-receive-address-fixture")
}

/// A valid transferChecked getTransaction result for a 10 USDC payment to the
/// merchant, referencing the checkout reference, memo = checkout id.
fn rpc_fixture(
    signature: &str,
    keys: &[String],
    destination: &str,
    merchant: &str,
    atomic_amount: i64,
    mint: &str,
    memo: &str,
) -> String {
    let token_program = lazuar_api::rails::solana::cluster::TOKEN_PROGRAM;
    let memo_program = lazuar_api::rails::solana::cluster::MEMO_PROGRAM;
    let dest_index = keys
        .iter()
        .position(|k| k == destination)
        .map(|p| p as i64)
        .unwrap_or(i64::MAX);
    serde_json::json!({
        "result": {
            "meta": {
                "err": null,
                "preTokenBalances": [],
                "postTokenBalances": [
                    { "accountIndex": dest_index, "owner": merchant, "mint": mint,
                      "uiTokenAmount": { "amount": atomic_amount.to_string() } }
                ]
            },
            "transaction": {
                "signatures": [signature],
                "message": {
                    "accountKeys": keys,
                    "instructions": [
                        { "programId": token_program,
                          "parsed": { "type": "transferChecked",
                                      "info": { "destination": destination, "mint": mint,
                                                "tokenAmount": { "amount": atomic_amount.to_string() } } } },
                        { "programId": memo_program,
                          "parsed": { "info": { "memo": memo } } }
                    ]
                }
            }
        }
    })
    .to_string()
}

#[test]
fn validate_accepts_the_reference_transfer_to_the_merchant() {
    let signature = real_signature();
    let reference = reference_pubkey();
    let merchant = merchant_address();
    let fixture = rpc_fixture(&signature, &["payer".to_string(), reference.clone(), merchant.clone(), "token_acct".to_string()],&"token_acct", &merchant, 10_000_000, cluster::DEVNET_MINT, "co_solana_1");
    let keys = vec!["payer".to_string(), reference.clone(), merchant.clone()];
    let doc: serde_json::Value = serde_json::from_str(&fixture).unwrap();

    validate(
        &doc,
        &ValidateInput {
            checkout_id: "co_solana_1",
            checkout_amount: Decimal::from_str("10").unwrap(),
            provider_session_id: &reference,
            public_merchant_id: &merchant,
            signature: &signature,
            cluster: "devnet",
        },
    )
    .expect("valid transfer must pass");
}

#[test]
fn validate_rejects_wrong_amount_mint_and_missing_reference() {
    let signature = real_signature();
    let reference = reference_pubkey();
    let merchant = merchant_address();
    let input_amount = Decimal::from_str("10").unwrap();

    // Wrong amount: 9 USDC against a 10 USDC checkout.
    let keys = vec![
        "payer".to_string(),
        reference.clone(),
        merchant.clone(),
        "token_acct".to_string(),
    ];
    let fixture = rpc_fixture(&signature, &keys, "token_acct", &merchant, 9_000_000, cluster::DEVNET_MINT, "co_solana_1");
    let doc: serde_json::Value = serde_json::from_str(&fixture).unwrap();
    println!("[dbg] fixture={fixture}");
    let err = validate(
        &doc,
        &ValidateInput {
            checkout_id: "co_solana_1",
            checkout_amount: input_amount,
            provider_session_id: &reference,
            public_merchant_id: &merchant,
            signature: &signature,
            cluster: "devnet",
        },
    )
    .unwrap_err();
    println!("[dbg] got err={err}");
    assert_eq!(err, "amount mismatch");

    // Missing reference key: keys carry no reference entry — the movement cannot
    // be attributed to this checkout (transfer itself is valid to the merchant).
    let keys = vec!["payer".to_string(), merchant.clone(), "token_acct".to_string()];
    let fixture = rpc_fixture(&signature, &keys, "token_acct", &merchant, 10_000_000, cluster::DEVNET_MINT, "co_solana_1");
    let doc: serde_json::Value = serde_json::from_str(&fixture).unwrap();
    let err = validate(
        &doc,
        &ValidateInput {
            checkout_id: "co_solana_1",
            checkout_amount: input_amount,
            provider_session_id: "reference-not-present",
            public_merchant_id: &merchant,
            signature: &signature,
            cluster: "devnet",
        },
    )
    .unwrap_err();
    assert_eq!(err, "reference missing");

    // Transaction failed on chain.
    let failed = r#"{"result":{"meta":{"err":{"InstructionError":[0,{"Custom":1}]}}}}"#;
    let doc: serde_json::Value = serde_json::from_str(failed).unwrap();
    let err = validate(
        &doc,
        &ValidateInput {
            checkout_id: "co_solana_1",
            checkout_amount: input_amount,
            provider_session_id: &reference,
            public_merchant_id: &merchant,
            signature: &signature,
            cluster: "devnet",
        },
    )
    .unwrap_err();
    assert_eq!(err, "transaction failed");

    // Not found (not yet finalized / wrong signature).
    let doc: serde_json::Value = serde_json::from_str(r#"{"result":null}"#).unwrap();
    let err = validate(
        &doc,
        &ValidateInput {
            checkout_id: "co_solana_1",
            checkout_amount: input_amount,
            provider_session_id: &reference,
            public_merchant_id: &merchant,
            signature: &signature,
            cluster: "devnet",
        },
    )
    .unwrap_err();
    assert_eq!(err, "transaction not found");
}

// ---------------------------------------------------------------------------
// Confirm flow on real Postgres
// ---------------------------------------------------------------------------

fn solana_checkout(app: &TestApp, status: &str) -> (String, String, String) {
    let mut db = app.db();
    let checkout_id = uuid::Uuid::new_v4().to_string();
    let public_token = uuid::Uuid::new_v4().simple().to_string();
    let reference = reference_pubkey();
    let merchant = merchant_address();
    db.execute(
        "INSERT INTO public.checkouts \
         (\"Id\",\"OrgId\",\"PublicToken\",\"Amount\",\"Currency\",\"Status\",\"Interval\",\
         \"Provider\",\"ProviderSessionId\",\"PspRedirectUrl\",\"CreatedAt\") \
         VALUES ($1,$2,$3,$4,'USDC',$5,'one_off','solana',$6,$7,$8)",
        &[
            &checkout_id,
            &"org_1",
            &public_token,
            &Decimal::from_str("10").unwrap(),
            &status,
            &reference,
            &format!("http://checkout.test/c/{public_token}"),
            &chrono::Utc::now(),
        ],
    )
    .unwrap();
    db.execute(
        "INSERT INTO public.gateway_credentials \
         (\"OrgId\",\"Provider\",\"Ciphertext\",\"Environment\",\"UpdatedAt\") \
         VALUES ($1,'solana','wrapped',$2,$3)",
        &[&"org_1", &"devnet", &chrono::Utc::now()],
    )
    .unwrap();
    (checkout_id, public_token, reference)
}

#[test]
fn confirm_open_checkout_with_valid_signature_fulfills() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let (checkout_id, _token, reference) = solana_checkout(&app, "open");

    let signature = real_signature();
    let merchant = merchant_address();
    let fixture = rpc_fixture(&signature, &["payer".to_string(), reference.clone(), merchant.clone(), "token_acct".to_string()],&"token_acct", &merchant, 10_000_000, cluster::DEVNET_MINT, &checkout_id);
    let fake = FakeTransport::new("solana-rpc");
    let payload = fixture.clone();
    fake.respond_with(move |_| lazuar_api::transport::OutResponse { status: 200, body: payload.clone() });
    let rpc = SolanaRpc { rpc_url: Some("http://solana.test/".into()), transport: Box::new(fake) };
    let box_one = SecretBox::from_env_testing(None).unwrap();
    let gates = CheckoutGates::default();
    let deps = ConfirmDeps { box_one: &box_one, gates: &gates, rpc: &rpc, environment: "Testing", config_cluster: "devnet" };

    let checkout = lazuar_api::rails::solana::confirm::CheckoutForSolana {
        id: checkout_id.clone(),
        org_id: "org_1".into(),
        amount: Decimal::from_str("10").unwrap(),
        currency: "USDC".into(),
        status: "open".into(),
        provider: Some("solana".into()),
        provider_session_id: reference.clone(),
        public_merchant_id: merchant.clone(),
    };
    let outcome = confirm(&mut db, &deps, &checkout, &signature).unwrap();
    assert!(matches!(outcome, ConfirmOutcome::Ok), "got {outcome:?}");

    assert_eq!(
        db.query_one("SELECT \"Status\" FROM public.checkouts", &[]).unwrap().get::<_, String>(0),
        "paid"
    );
    let receipts: i64 = db.query_one("SELECT count(*) FROM public.documents", &[]).unwrap().get(0);
    assert_eq!(receipts, 1);
    // Replay: duplicate.
    let outcome = confirm(&mut db, &deps, &checkout, &signature).unwrap();
    assert!(matches!(outcome, ConfirmOutcome::Duplicate));
}

#[test]
fn confirm_rejects_wrong_amount_without_consuming_the_signature() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let (checkout_id, _token, reference) = solana_checkout(&app, "open");

    let signature = real_signature();
    let merchant = merchant_address();
    let fixture = rpc_fixture(&signature, &["payer".to_string(), reference.clone(), merchant.clone(), "token_acct".to_string()], "token_acct", &merchant, 9_000_000, cluster::DEVNET_MINT, &checkout_id);
    let fake = FakeTransport::new("solana-rpc");
    let payload = fixture.clone();
    fake.respond_with(move |_| lazuar_api::transport::OutResponse { status: 200, body: payload.clone() });
    let rpc = SolanaRpc { rpc_url: Some("http://solana.test/".into()), transport: Box::new(fake) };
    let box_one = SecretBox::from_env_testing(None).unwrap();
    let gates = CheckoutGates::default();
    let deps = ConfirmDeps { box_one: &box_one, gates: &gates, rpc: &rpc, environment: "Testing", config_cluster: "devnet" };

    let checkout = lazuar_api::rails::solana::confirm::CheckoutForSolana {
        id: checkout_id.clone(),
        org_id: "org_1".into(),
        amount: Decimal::from_str("10").unwrap(),
        currency: "USDC".into(),
        status: "open".into(),
        provider: Some("solana".into()),
        provider_session_id: reference.clone(),
        public_merchant_id: merchant.clone(),
    };
    let outcome = confirm(&mut db, &deps, &checkout, &signature).unwrap();
    assert!(
        matches!(&outcome, ConfirmOutcome::ValidationFailed(reason) if reason == "amount mismatch"),
        "got {outcome:?}"
    );
    // The signature was not consumed: a later valid confirmation still works.
    let events: i64 = db.query_one("SELECT count(*) FROM public.psp_webhook_events", &[]).unwrap().get(0);
    assert_eq!(events, 0);
}

#[test]
fn cluster_mismatch_is_rejected() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let (checkout_id, _token, _reference) = solana_checkout(&app, "open");
    // Vault pins mainnet; config says devnet.
    db.execute(
        "UPDATE public.gateway_credentials SET \"Environment\"='mainnet'",
        &[],
    )
    .unwrap();

    let signature = real_signature();
    let merchant = merchant_address();
    let fixture = rpc_fixture(&signature, &["payer".to_string(), reference_pubkey(), merchant.clone()],&"token_acct", &merchant, 10_000_000, cluster::DEVNET_MINT, &checkout_id);
    let fake = FakeTransport::new("solana-rpc");
    let payload = fixture.clone();
    fake.respond_with(move |_| lazuar_api::transport::OutResponse { status: 200, body: payload.clone() });
    let rpc = SolanaRpc { rpc_url: Some("http://solana.test/".into()), transport: Box::new(fake) };
    let box_one = SecretBox::from_env_testing(None).unwrap();
    let gates = CheckoutGates::default();
    let deps = ConfirmDeps { box_one: &box_one, gates: &gates, rpc: &rpc, environment: "Testing", config_cluster: "devnet" };

    let checkout = lazuar_api::rails::solana::confirm::CheckoutForSolana {
        id: checkout_id,
        org_id: "org_1".into(),
        amount: Decimal::from_str("10").unwrap(),
        currency: "USDC".into(),
        status: "open".into(),
        provider: Some("solana".into()),
        provider_session_id: reference_pubkey(),
        public_merchant_id: merchant,
    };
    let outcome = confirm(&mut db, &deps, &checkout, &signature).unwrap();
    assert!(matches!(outcome, ConfirmOutcome::ClusterMismatch));
}

#[test]
fn watcher_loads_vault_receive_address_and_fulfills() {
    let app = TestApp::spawn();
    let mut db = app.db();
    let (checkout_id, _token, reference) = solana_checkout(&app, "open");
    let merchant = merchant_address();
    db.execute(
        "UPDATE public.gateway_credentials SET \"PublicMerchantId\" = $1 WHERE \"OrgId\" = $2",
        &[&merchant, &"org_1"],
    )
    .unwrap();

    let signature = real_signature();
    let fixture = rpc_fixture(
        &signature,
        &[
            "payer".to_string(),
            reference.clone(),
            merchant.clone(),
            "token_acct".to_string(),
        ],
        "token_acct",
        &merchant,
        10_000_000,
        cluster::DEVNET_MINT,
        &checkout_id,
    );
    let fake = FakeTransport::new("solana-rpc");
    let payload = fixture.clone();
    let sig = signature.clone();
    fake.respond_with(move |req| {
        let body = req.body.clone().unwrap_or_default();
        if body.contains("getSignaturesForAddress") {
            lazuar_api::transport::OutResponse {
                status: 200,
                body: serde_json::json!({ "result": [{ "signature": sig }] }).to_string(),
            }
        } else {
            lazuar_api::transport::OutResponse {
                status: 200,
                body: payload.clone(),
            }
        }
    });
    let rpc = SolanaRpc {
        rpc_url: Some("http://solana.test/".into()),
        transport: Box::new(fake),
    };
    let box_one = SecretBox::from_env_testing(None).unwrap();
    let gates = CheckoutGates::default();
    let deps = ConfirmDeps {
        box_one: &box_one,
        gates: &gates,
        rpc: &rpc,
        environment: "Testing",
        config_cluster: "devnet",
    };
    let mut watcher = Watcher {
        conn: &mut db,
        deps: &deps,
        ttl: chrono::Duration::minutes(30),
    };
    let n = watcher.run_once().unwrap();
    assert!(n >= 1, "claimed open solana checkout");
    assert_eq!(
        db.query_one("SELECT \"Status\" FROM public.checkouts WHERE \"Id\" = $1", &[&checkout_id])
            .unwrap()
            .get::<_, String>(0),
        "paid"
    );
}
