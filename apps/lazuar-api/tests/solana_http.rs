//! Port of C# `SolanaVaultTests` / `SolanaCatalogTests` / `SolanaHostedTests` /
//! leftover `SolanaConfirmTests` through `TestApp`.

mod support;

use chrono::Duration;
use lazuar_api::money::fulfillment::CheckoutGates;
use lazuar_api::rails::providers;
use lazuar_api::rails::solana::base58;
use lazuar_api::rails::solana::cluster;
use lazuar_api::rails::solana::confirm::{ConfirmDeps, Watcher};
use lazuar_api::rails::solana::rpc::SolanaRpc;
use lazuar_api::secrets::SecretBox;
use rand::Rng;
use support::{
    auth_get, auth_post, call, checkout_status_of, docs_count, events_count, member_one, owner_one,
    put_gateway, seed_checkout, start_pay, TestApp,
};

const MERCHANT_ATA: &str = "Dest11111111111111111111111111111111111112";
const BUYER_ATA: &str = "Buyr11111111111111111111111111111111111112";

fn sample_address() -> String {
    let mut bytes = [0u8; 32];
    rand::thread_rng().fill(&mut bytes);
    base58::encode(&bytes)
}

fn sample_sig() -> String {
    let mut bytes = [0u8; 64];
    rand::thread_rng().fill(&mut bytes);
    base58::encode(&bytes)
}

fn put_solana(app: &TestApp, address: &str) -> ureq::Response {
    put_gateway(
        app,
        &format!(
            r#"{{"provider":"solana","public_merchant_id":"{address}","environment":"devnet"}}"#
        ),
    )
}

fn reference_from(url: &str) -> String {
    let q = url.split_once('?').map(|(_, q)| q).unwrap_or("");
    for part in q.split('&') {
        if let Some(v) = part.strip_prefix("reference=") {
            return v.to_string();
        }
    }
    panic!("reference missing from {url}");
}

fn rpc_fixture(
    signature: &str,
    _owner: &str,
    mint: &str,
    atomic: &str,
    reference: &str,
    memo: &str,
    dest: &str,
    dest_owner: &str,
) -> String {
    format!(
        r#"{{
          "jsonrpc": "2.0",
          "result": {{
            "slot": 1,
            "meta": {{
              "err": null,
              "preTokenBalances": [
                {{ "accountIndex": 1, "mint": "{mint}", "owner": "{dest_owner}", "uiTokenAmount": {{ "amount": "0", "decimals": 6 }} }}
              ],
              "postTokenBalances": [
                {{ "accountIndex": 1, "mint": "{mint}", "owner": "{dest_owner}", "uiTokenAmount": {{ "amount": "{atomic}", "decimals": 6 }} }}
              ]
            }},
            "transaction": {{
              "signatures": ["{signature}"],
              "message": {{
                "accountKeys": [
                  {{ "pubkey": "11111111111111111111111111111111", "signer": true, "writable": true }},
                  {{ "pubkey": "{dest}", "signer": false, "writable": true }},
                  {{ "pubkey": "{token}", "signer": false, "writable": false }},
                  {{ "pubkey": "{reference}", "signer": false, "writable": false }}
                ],
                "instructions": [
                  {{
                    "programId": "{token}",
                    "parsed": {{
                      "type": "transferChecked",
                      "info": {{
                        "mint": "{mint}",
                        "destination": "{dest}",
                        "tokenAmount": {{ "amount": "{atomic}", "decimals": 6 }}
                      }}
                    }}
                  }},
                  {{
                    "programId": "{memo_program}",
                    "parsed": "{memo}"
                  }}
                ]
              }}
            }}
          }}
        }}"#,
        token = cluster::TOKEN_PROGRAM,
        memo_program = cluster::MEMO_PROGRAM,
        dest_owner = dest_owner,
    )
}

fn good_tx(signature: &str, owner: &str, mint: &str, atomic: &str, reference: &str, memo: &str) -> String {
    rpc_fixture(signature, owner, mint, atomic, reference, memo, MERCHANT_ATA, owner)
}

fn decoy_tx(signature: &str, owner: &str, mint: &str, atomic: &str, reference: &str, memo: &str) -> String {
    format!(
        r#"{{
          "jsonrpc": "2.0",
          "result": {{
            "slot": 1,
            "meta": {{
              "err": null,
              "preTokenBalances": [
                {{ "accountIndex": 1, "mint": "{mint}", "owner": "buyer", "uiTokenAmount": {{ "amount": "0", "decimals": 6 }} }},
                {{ "accountIndex": 2, "mint": "{mint}", "owner": "{owner}", "uiTokenAmount": {{ "amount": "0", "decimals": 6 }} }}
              ],
              "postTokenBalances": [
                {{ "accountIndex": 1, "mint": "{mint}", "owner": "buyer", "uiTokenAmount": {{ "amount": "{atomic}", "decimals": 6 }} }},
                {{ "accountIndex": 2, "mint": "{mint}", "owner": "{owner}", "uiTokenAmount": {{ "amount": "0", "decimals": 6 }} }}
              ]
            }},
            "transaction": {{
              "signatures": ["{signature}"],
              "message": {{
                "accountKeys": [
                  {{ "pubkey": "11111111111111111111111111111111", "signer": true, "writable": true }},
                  {{ "pubkey": "{buyer}", "signer": false, "writable": true }},
                  {{ "pubkey": "{merchant}", "signer": false, "writable": true }},
                  {{ "pubkey": "{token}", "signer": false, "writable": false }},
                  {{ "pubkey": "{reference}", "signer": false, "writable": false }}
                ],
                "instructions": [
                  {{
                    "programId": "{token}",
                    "parsed": {{
                      "type": "transferChecked",
                      "info": {{
                        "mint": "{mint}",
                        "destination": "{buyer}",
                        "tokenAmount": {{ "amount": "{atomic}", "decimals": 6 }}
                      }}
                    }}
                  }},
                  {{
                    "programId": "{memo_program}",
                    "parsed": "{memo}"
                  }}
                ]
              }}
            }}
          }}
        }}"#,
        buyer = BUYER_ATA,
        merchant = MERCHANT_ATA,
        token = cluster::TOKEN_PROGRAM,
        memo_program = cluster::MEMO_PROGRAM,
    )
}

fn confirm(app: &TestApp, token: &str, signature: &str) -> ureq::Response {
    support::send(
        ureq::post(&format!("{}/v1/pay/{token}/confirm", app.base_url)),
        &format!(r#"{{"signature":"{signature}"}}"#),
    )
}

fn start_solana(app: &TestApp) -> (String, String, String, String) {
    let address = sample_address();
    let put = put_solana(app, &address);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let (token, checkout_id) = seed_checkout(app, "solana", Some("USDC"));
    let started = start_pay(app, &token, r#"{"name":"Ada"}"#);
    let status = started.status();
    let raw = started.into_string().unwrap_or_default();
    assert_eq!(status, 200, "{raw}");
    let doc: serde_json::Value = serde_json::from_str(&raw).unwrap();
    let url = doc["solana_pay_url"].as_str().unwrap_or("").to_string();
    (token, checkout_id, address, url)
}

fn run_watcher(app: &TestApp) {
    let mut db = app.pool.get().expect("pool");
    let rpc = SolanaRpc {
        rpc_url: Some(app.config.solana_rpc_url.clone()),
        transport: Box::new(app.psp.clone()),
    };
    let box_one = SecretBox::from_env_testing(None).unwrap();
    let gates = CheckoutGates::default();
    let deps = ConfirmDeps {
        box_one: &box_one,
        gates: &gates,
        rpc: &rpc,
        environment: &app.config.environment,
        config_cluster: &app.config.solana_cluster,
    };
    let mut watcher = Watcher {
        conn: &mut db,
        deps: &deps,
        ttl: Duration::minutes(app.config.reservation_ttl_minutes.max(1)),
    };
    watcher.run_once().expect("watcher");
}

// ---------------------------------------------------------------------------
// Vault
// ---------------------------------------------------------------------------

#[test]
fn put_solana_address_without_secret() {
    let app = TestApp::spawn();
    owner_one(&app);
    let address = sample_address();
    let put = put_solana(&app, &address);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let got = auth_get(&app, "/v1/orgs/t1/gateway?provider=solana");
    let json = got.into_string().unwrap_or_default();
    let doc: serde_json::Value = serde_json::from_str(&json).unwrap();
    assert_eq!(doc["configured"], true);
    assert_eq!(doc["public_merchant_id"], address);
    assert_eq!(doc["environment"], "devnet");
    assert_eq!(doc["last4"], &address[address.len() - 4..]);
    assert_eq!(doc["webhook_configured"], false);
    assert_eq!(doc["capability"], "hosted_link");
    assert!(!json.contains("sk_"), "{json}");
    assert!(!json.contains("whsec_"), "{json}");
    let mut db = app.pool.get().expect("pool");
    let row = db
        .query_one(
            "SELECT \"Ciphertext\",\"WebhookCiphertext\" FROM public.gateway_credentials WHERE \"Provider\" = 'solana'",
            &[],
        )
        .unwrap();
    let ct: String = row.get(0);
    let wh: Option<String> = row.get(1);
    assert_eq!(ct, "");
    assert!(wh.is_none());
}

#[test]
fn put_solana_rejects_secret_and_webhook_secret() {
    let app = TestApp::spawn();
    owner_one(&app);
    let address = sample_address();
    let secret = put_gateway(
        &app,
        &format!(
            r#"{{"provider":"solana","secret":"sk_test_x","public_merchant_id":"{address}","environment":"devnet"}}"#
        ),
    );
    assert_eq!(secret.status(), 400);
    assert!(
        secret.into_string().unwrap_or_default().contains("API secret")
    );
    let wh = put_gateway(
        &app,
        &format!(
            r#"{{"provider":"solana","webhook_secret":"whsec_x","public_merchant_id":"{address}","environment":"devnet"}}"#
        ),
    );
    assert_eq!(wh.status(), 400);
    assert!(
        wh.into_string().unwrap_or_default().contains("webhook secret")
    );
}

#[test]
fn put_solana_rejects_invalid_address_and_rpc() {
    let app = TestApp::spawn();
    owner_one(&app);
    for bad in [
        r#"{"provider":"solana","public_merchant_id":"not-an-address","environment":"devnet"}"#,
        r#"{"provider":"solana","public_merchant_id":"0xabc","environment":"devnet"}"#,
        r#"{"provider":"solana","public_merchant_id":"https://api.devnet.solana.com","environment":"devnet"}"#,
        r#"{"provider":"solana","public_merchant_id":"-----BEGIN","environment":"devnet"}"#,
        r#"{"provider":"solana","environment":"devnet"}"#,
    ] {
        let res = put_gateway(&app, bad);
        assert_eq!(res.status(), 400, "{bad}");
    }
}

#[test]
fn put_solana_requires_devnet_or_mainnet() {
    let app = TestApp::spawn();
    owner_one(&app);
    let address = sample_address();
    let live = put_gateway(
        &app,
        &format!(
            r#"{{"provider":"solana","public_merchant_id":"{address}","environment":"live"}}"#
        ),
    );
    assert_eq!(live.status(), 400);
    assert!(
        live.into_string()
            .unwrap_or_default()
            .contains("devnet or mainnet")
    );
    let mainnet = put_gateway(
        &app,
        &format!(
            r#"{{"provider":"solana","public_merchant_id":"{address}","environment":"mainnet-beta"}}"#
        ),
    );
    assert_eq!(mainnet.status(), 400);
    assert!(
        mainnet
            .into_string()
            .unwrap_or_default()
            .contains("cluster mismatch")
    );
    let put = put_solana(&app, &address);
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let got = auth_get(&app, "/v1/orgs/t1/gateway?provider=solana");
    let doc: serde_json::Value = got.into_json().unwrap();
    assert_eq!(doc["environment"], "devnet");
}

#[test]
fn member_cannot_put_solana() {
    let app = TestApp::spawn();
    member_one(&app);
    let resp = put_solana(&app, &sample_address());
    assert_eq!(resp.status(), 403);
}

#[test]
fn stripe_still_rejects_public_merchant_id() {
    let app = TestApp::spawn();
    owner_one(&app);
    let res = put_gateway(
        &app,
        r#"{"provider":"stripe","secret":"sk_test_x","webhook_secret":"whsec_x","public_merchant_id":"brand"}"#,
    );
    assert_eq!(res.status(), 400);
    assert!(
        res.into_string()
            .unwrap_or_default()
            .contains("not used for this provider")
    );
}

// ---------------------------------------------------------------------------
// Catalog
// ---------------------------------------------------------------------------

#[test]
fn pay_providers_knows_solana() {
    assert_eq!(providers::SOLANA, "solana");
    assert!(providers::ALL.contains(&providers::SOLANA));
    assert!(!providers::ALL.contains(&providers::TEST));
    assert_eq!(providers::CAPABILITY, "hosted_link");
    assert_eq!(providers::try_normalize(Some("solana")), Some(providers::SOLANA));
    assert_eq!(providers::try_normalize(Some(" Solana ")), Some(providers::SOLANA));
    assert!(providers::try_normalize(Some("paypal")).is_none());
    assert!(!providers::requires_email(providers::SOLANA));
    assert!(providers::requires_public_merchant_id(providers::SOLANA));
    assert!(providers::allows_public_merchant_id(providers::SOLANA));
    assert!(!providers::allows_public_merchant_id(providers::STRIPE));
    let testing = providers::listed("Testing");
    assert!(testing.contains(&providers::SOLANA));
    assert!(testing.contains(&providers::TEST));
    assert_eq!(testing.len(), 7);
    let production = providers::listed("Production");
    assert!(production.contains(&providers::SOLANA));
    assert!(!production.contains(&providers::TEST));
    assert_eq!(production.len(), 6);
}

#[test]
fn paypal_is_still_unknown_provider() {
    let app = TestApp::spawn();
    owner_one(&app);
    let resp = auth_post(
        &app,
        "/v1/checkouts",
        r#"{"org_id":"t1","amount":10,"provider":"paypal"}"#,
    );
    assert_eq!(resp.status(), 400);
    assert!(
        resp.into_string()
            .unwrap_or_default()
            .contains("unknown provider")
    );
}

#[test]
fn solana_mint_without_vault_is_rail_not_configured() {
    let app = TestApp::spawn();
    owner_one(&app);
    let resp = auth_post(
        &app,
        "/v1/checkouts",
        r#"{"org_id":"t1","amount":10,"provider":"solana","currency":"USDC"}"#,
    );
    assert_eq!(resp.status(), 400);
    assert!(
        resp.into_string()
            .unwrap_or_default()
            .contains("rail not configured")
    );
}

#[test]
fn solana_plane_b_webhook_is_not_stripe_parse() {
    let app = TestApp::spawn();
    let resp = support::send(
        ureq::post(&format!("{}/v1/webhooks/solana/t1", app.base_url)),
        r#"{"type":"checkout.session.completed"}"#,
    );
    assert_eq!(resp.status(), 400);
    let body = resp.into_string().unwrap_or_default();
    assert!(
        body.contains("solana does not use inbound PSP webhooks") || body.contains("rail not configured"),
        "{body}"
    );
}

#[test]
fn solana_mint_requires_usdc() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_solana(&app, &sample_address());
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let ok = auth_post(
        &app,
        "/v1/checkouts",
        r#"{"org_id":"t1","amount":10,"provider":"solana","currency":"USDC"}"#,
    );
    let ok_status = ok.status();
    let ok_raw = ok.into_string().unwrap_or_default();
    assert_eq!(ok_status, 201, "{ok_raw}");
    let doc: serde_json::Value = serde_json::from_str(&ok_raw).unwrap();
    assert_eq!(doc["currency"], "USDC");
    assert_eq!(doc["amount"].as_f64(), Some(10.0));

    for body in [
        r#"{"org_id":"t1","amount":10,"provider":"solana"}"#,
        r#"{"org_id":"t1","amount":10,"provider":"solana","currency":"MYR"}"#,
        r#"{"org_id":"t1","amount":10,"provider":"solana","currency":"USD"}"#,
        r#"{"org_id":"t1","amount":10,"provider":"solana","currency":"USDC","interval":"mo"}"#,
    ] {
        let res = auth_post(&app, "/v1/checkouts", body);
        assert_eq!(res.status(), 400, "{body}");
    }

    let link_myr = auth_post(
        &app,
        "/v1/payment-links",
        r#"{"org_id":"t1","amount":10,"provider":"solana","currency":"MYR"}"#,
    );
    assert_eq!(link_myr.status(), 400);
    assert!(
        link_myr.into_string().unwrap_or_default().contains("ringgit")
    );

    let link_ok = auth_post(
        &app,
        "/v1/payment-links",
        r#"{"org_id":"t1","amount":10,"provider":"solana","currency":"USDC"}"#,
    );
    assert_eq!(link_ok.status(), 201, "{}", link_ok.into_string().unwrap_or_default());

    let too_many = auth_post(
        &app,
        "/v1/checkouts",
        r#"{"org_id":"t1","amount":10.1234567,"provider":"solana","currency":"USDC"}"#,
    );
    assert_eq!(too_many.status(), 400);
    assert!(
        too_many.into_string().unwrap_or_default().contains("USDC amount")
    );

    let sub_cent = auth_post(
        &app,
        "/v1/checkouts",
        r#"{"org_id":"t1","amount":10.000001,"provider":"solana","currency":"USDC"}"#,
    );
    assert_eq!(sub_cent.status(), 400);
    assert!(
        sub_cent
            .into_string()
            .unwrap_or_default()
            .contains("2 decimal places")
    );

    let link_sub = auth_post(
        &app,
        "/v1/payment-links",
        r#"{"org_id":"t1","amount":0.005,"provider":"solana","currency":"USDC"}"#,
    );
    assert_eq!(link_sub.status(), 400);
}

#[test]
fn solana_rejects_myr_catalog_product() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_solana(&app, &sample_address());
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let created = auth_post(
        &app,
        "/v1/orgs/t1/products",
        r#"{"name":"Bar","amount":10,"currency":"MYR"}"#,
    );
    let created_status = created.status();
    let created_raw = created.into_string().unwrap_or_default();
    assert_eq!(created_status, 201, "{created_raw}");
    let product_id = serde_json::from_str::<serde_json::Value>(&created_raw).unwrap()["id"]
        .as_str()
        .unwrap()
        .to_string();
    let link = auth_post(
        &app,
        "/v1/payment-links",
        &format!(
            r#"{{"org_id":"t1","amount":10,"provider":"solana","currency":"USDC","product_id":"{product_id}"}}"#
        ),
    );
    assert_eq!(link.status(), 400);
    assert!(link.into_string().unwrap_or_default().contains("catalog"));
}

// ---------------------------------------------------------------------------
// Hosted
// ---------------------------------------------------------------------------

#[test]
fn start_returns_solana_pay_url_and_stays_open() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, checkout_id, address, url) = start_solana(&app);
    assert!(!url.is_empty());
    assert!(url.starts_with(&format!("solana:{address}")), "{url}");
    assert!(url.contains(&format!("spl-token={}", cluster::DEVNET_MINT)), "{url}");
    assert!(url.contains("amount=10"), "{url}");
    assert!(url.contains("reference="), "{url}");
    assert!(url.contains(&format!("memo={checkout_id}")), "{url}");

    let get = call(ureq::get(&format!("{}/v1/pay/{token}", app.base_url)));
    let get_doc: serde_json::Value = get.into_json().unwrap();
    assert_eq!(get_doc["status"], "open");
    assert_eq!(get_doc["started"], true);
    assert_eq!(get_doc["email_required"], false);
    assert!(get_doc["redirect_url"].is_null());
    assert_eq!(get_doc["solana_pay_url"], url);
    assert_eq!(get_doc["solana_cluster"], "devnet");
    assert!(get_doc.get("pay_url").is_none(), "{get_doc}");

    let mut db = app.pool.get().expect("pool");
    let row = db
        .query_one(
            "SELECT \"Status\",\"PspRedirectUrl\",\"ProviderSessionId\" FROM public.checkouts",
            &[],
        )
        .unwrap();
    let status: String = row.get(0);
    let psp: Option<String> = row.get(1);
    let session: Option<String> = row.get(2);
    drop(db);
    assert_eq!(status, "open");
    assert_eq!(psp.as_deref(), Some(url.as_str()));
    assert!(session.as_deref().is_some_and(|s| !s.is_empty()));
    assert_eq!(docs_count(&app), 0);

    let again = start_pay(&app, &token, r#"{"name":"Ada"}"#);
    let again_doc: serde_json::Value = again.into_json().unwrap();
    assert_eq!(again_doc["solana_pay_url"], url);
}

#[test]
fn slot_key_resumes_the_same_qr_without_a_second_seat() {
    let app = TestApp::spawn();
    owner_one(&app);
    let put = put_solana(&app, &sample_address());
    assert!(put.status() < 300, "{}", put.into_string().unwrap_or_default());
    let created = auth_post(
        &app,
        "/v1/payment-links",
        r#"{"org_id":"t1","amount":10,"provider":"solana","currency":"USDC","max_payers":1}"#,
    );
    let created_status = created.status();
    let created_raw = created.into_string().unwrap_or_default();
    assert_eq!(created_status, 201, "{created_raw}");
    let token = serde_json::from_str::<serde_json::Value>(&created_raw).unwrap()["public_token"]
        .as_str()
        .unwrap()
        .to_string();
    let first = start_pay(
        &app,
        &token,
        r#"{"name":"Ada","slot_key":"slot-key-1"}"#,
    );
    let first_status = first.status();
    let first_raw = first.into_string().unwrap_or_default();
    assert_eq!(first_status, 200, "{first_raw}");
    let url = serde_json::from_str::<serde_json::Value>(&first_raw).unwrap()["solana_pay_url"]
        .as_str()
        .unwrap()
        .to_string();
    let second = start_pay(
        &app,
        &token,
        r#"{"name":"Ada","slot_key":"slot-key-1"}"#,
    );
    let second_doc: serde_json::Value = second.into_json().unwrap();
    assert_eq!(second_doc["solana_pay_url"], url);
    let get = call(ureq::get(&format!(
        "{}/v1/pay/{token}?slot_key=slot-key-1",
        app.base_url
    )));
    let get_doc: serde_json::Value = get.into_json().unwrap();
    assert_eq!(get_doc["solana_pay_url"], url);
    assert_eq!(get_doc["taken_count"], 1);
    let count: i64 = app
        .pool
        .get()
        .expect("pool")
        .query_one("SELECT count(*) FROM public.checkouts", &[])
        .unwrap()
        .get(0);
    assert_eq!(count, 1);
}

// ---------------------------------------------------------------------------
// Confirm HTTP + watcher
// ---------------------------------------------------------------------------

#[test]
fn confirm_paid_replay_and_mismatch() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, checkout_id, address, url) = start_solana(&app);
    let reference = reference_from(&url);
    let signature = sample_sig();
    let fixture = good_tx(
        &signature,
        &address,
        cluster::DEVNET_MINT,
        "10000000",
        &reference,
        &checkout_id,
    );
    app.psp.respond_with(move |_| lazuar_api::transport::OutResponse {
        status: 200,
        body: fixture.clone(),
    });
    let paid = confirm(&app, &token, &signature);
    assert_eq!(paid.status(), 200, "{}", paid.into_string().unwrap_or_default());
    assert_eq!(checkout_status_of(&app, &checkout_id), "paid");
    let mut db = app.pool.get().expect("pool");
    let pref: Option<String> = db
        .query_one("SELECT \"ProviderRef\" FROM public.charges", &[])
        .unwrap()
        .get(0);
    let currency: String = db
        .query_one("SELECT \"Currency\" FROM public.charges", &[])
        .unwrap()
        .get(0);
    let number: String = db
        .query_one("SELECT \"Number\" FROM public.documents", &[])
        .unwrap()
        .get(0);
    let event: String = db
        .query_one("SELECT \"EventId\" FROM public.psp_webhook_events", &[])
        .unwrap()
        .get(0);
    let debit: rust_decimal::Decimal = db
        .query_one(
            "SELECT COALESCE(sum(\"Amount\"),0) FROM public.journal_lines WHERE \"Dc\" = 'D'",
            &[],
        )
        .unwrap()
        .get(0);
    drop(db);
    assert_eq!(pref.as_deref(), Some(signature.as_str()));
    assert_eq!(currency, "USDC");
    assert!(number.starts_with("RCPT-"), "{number}");
    assert_eq!(event, signature);
    assert_eq!(debit, rust_decimal::Decimal::from(10));

    let replay = confirm(&app, &token, &signature);
    assert!(
        replay.into_string().unwrap_or_default().contains("duplicate")
    );
    assert_eq!(docs_count(&app), 1);
}

#[test]
fn confirm_mismatch_consumes_zero_events() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, checkout_id, address, url) = start_solana(&app);
    let reference = reference_from(&url);
    let signature = sample_sig();
    let fixture = good_tx(
        &signature,
        &address,
        cluster::DEVNET_MINT,
        "1000",
        &reference,
        &checkout_id,
    );
    app.psp.respond_with(move |_| lazuar_api::transport::OutResponse {
        status: 200,
        body: fixture.clone(),
    });
    let res = confirm(&app, &token, &signature);
    assert_eq!(res.status(), 400);
    assert!(
        res.into_string().unwrap_or_default().contains("amount mismatch")
    );
    assert_eq!(events_count(&app), 0);
    assert_eq!(checkout_status_of(&app, &checkout_id), "open");
    assert_eq!(docs_count(&app), 0);
}

#[test]
fn get_does_not_fulfill() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, _, _, _) = start_solana(&app);
    app.psp.respond_with(|_| lazuar_api::transport::OutResponse {
        status: 200,
        body: r#"{"jsonrpc":"2.0","result":null}"#.into(),
    });
    let before = app.psp.send_count();
    let get = call(ureq::get(&format!("{}/v1/pay/{token}", app.base_url)));
    let doc: serde_json::Value = get.into_json().unwrap();
    assert_eq!(doc["status"], "open");
    assert_eq!(app.psp.send_count(), before);
}

#[test]
fn pause_does_not_consume_signature() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, _, _, _) = start_solana(&app);
    app.pool
        .get()
        .expect("pool")
        .execute(
            "UPDATE public.org_settings SET \"ChargesPaused\" = TRUE WHERE \"OrgId\" = 't1'",
            &[],
        )
        .unwrap();
    let res = confirm(&app, &token, &sample_sig());
    assert_eq!(res.status(), 409);
    assert_eq!(events_count(&app), 0);
}

#[test]
fn confirm_rejects_other_mint() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, checkout_id, address, url) = start_solana(&app);
    let reference = reference_from(&url);
    let signature = sample_sig();
    let fixture = good_tx(
        &signature,
        &address,
        cluster::MAINNET_MINT,
        "10000000",
        &reference,
        &checkout_id,
    );
    app.psp.respond_with(move |_| lazuar_api::transport::OutResponse {
        status: 200,
        body: fixture.clone(),
    });
    let res = confirm(&app, &token, &signature);
    assert_eq!(res.status(), 400);
    assert!(
        res.into_string().unwrap_or_default().contains("mint mismatch")
    );
    assert_eq!(events_count(&app), 0);
    assert_eq!(checkout_status_of(&app, &checkout_id), "open");
}

#[test]
fn confirm_decoy_self_transfer_is_destination_mismatch() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, checkout_id, address, url) = start_solana(&app);
    let reference = reference_from(&url);
    let signature = sample_sig();
    let fixture = decoy_tx(
        &signature,
        &address,
        cluster::DEVNET_MINT,
        "10000000",
        &reference,
        &checkout_id,
    );
    app.psp.respond_with(move |_| lazuar_api::transport::OutResponse {
        status: 200,
        body: fixture.clone(),
    });
    let res = confirm(&app, &token, &signature);
    assert_eq!(res.status(), 400);
    assert!(
        res.into_string()
            .unwrap_or_default()
            .contains("destination mismatch")
    );
    assert_eq!(events_count(&app), 0);
    assert_eq!(checkout_status_of(&app, &checkout_id), "open");
}

#[test]
fn poller_walks_past_junk_signature() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, checkout_id, address, url) = start_solana(&app);
    let _ = token;
    let reference = reference_from(&url);
    let junk = sample_sig();
    let good = sample_sig();
    let junk_tx = good_tx(&junk, &address, cluster::DEVNET_MINT, "1", &reference, &checkout_id);
    let good_tx_body = good_tx(
        &good,
        &address,
        cluster::DEVNET_MINT,
        "10000000",
        &reference,
        &checkout_id,
    );
    let sigs = format!(
        r#"{{"jsonrpc":"2.0","result":[{{"signature":"{junk}"}},{{"signature":"{good}"}}]}}"#
    );
    app.psp.respond_with(move |req| {
        let body = req.body.as_deref().unwrap_or("");
        let payload = if body.contains("getSignaturesForAddress") {
            sigs.clone()
        } else if body.contains(&junk) {
            junk_tx.clone()
        } else {
            good_tx_body.clone()
        };
        lazuar_api::transport::OutResponse {
            status: 200,
            body: payload,
        }
    });
    run_watcher(&app);
    assert_eq!(checkout_status_of(&app, &checkout_id), "paid");
    let pref: Option<String> = app
        .pool
        .get()
        .expect("pool")
        .query_one("SELECT \"ProviderRef\" FROM public.charges", &[])
        .unwrap()
        .get(0);
    assert_eq!(pref.as_deref(), Some(good.as_str()));
}

#[test]
fn poller_watches_beyond_first_twenty() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, checkout_id, address, url) = start_solana(&app);
    let _ = token;
    let reference = reference_from(&url);
    let signature = sample_sig();
    let mut db = app.pool.get().expect("pool");
    let org: String = db
        .query_one("SELECT \"OrgId\" FROM public.checkouts WHERE \"Id\" = $1", &[&checkout_id])
        .unwrap()
        .get(0);
    let amount: rust_decimal::Decimal = db
        .query_one("SELECT \"Amount\" FROM public.checkouts WHERE \"Id\" = $1", &[&checkout_id])
        .unwrap()
        .get(0);
    let psp: String = db
        .query_one(
            "SELECT \"PspRedirectUrl\" FROM public.checkouts WHERE \"Id\" = $1",
            &[&checkout_id],
        )
        .unwrap()
        .get(0);
    for i in 0..20 {
        let extra_id = uuid::Uuid::new_v4().to_string();
        let extra_ref = sample_address();
        db.execute(
            "INSERT INTO public.checkouts \
             (\"Id\",\"OrgId\",\"PublicToken\",\"Amount\",\"Currency\",\"Status\",\"Interval\",\
             \"Provider\",\"PspRedirectUrl\",\"ProviderSessionId\",\"CreatedAt\") \
             VALUES ($1,$2,$3,$4,'USDC','open','one_off','solana',$5,$6,$7)",
            &[
                &extra_id,
                &org,
                &uuid::Uuid::new_v4().simple().to_string(),
                &amount,
                &psp,
                &extra_ref,
                &(chrono::Utc::now() - chrono::Duration::minutes(i + 1)),
            ],
        )
        .unwrap();
    }
    drop(db);
    let fixture = good_tx(
        &signature,
        &address,
        cluster::DEVNET_MINT,
        "10000000",
        &reference,
        &checkout_id,
    );
    let sigs = format!(r#"{{"jsonrpc":"2.0","result":[{{"signature":"{signature}"}}]}}"#);
    app.psp.respond_with(move |req| {
        let body = req.body.as_deref().unwrap_or("");
        let payload = if body.contains("getSignaturesForAddress") {
            if body.contains(&reference) {
                sigs.clone()
            } else {
                r#"{"jsonrpc":"2.0","result":[]}"#.to_string()
            }
        } else {
            fixture.clone()
        };
        lazuar_api::transport::OutResponse {
            status: 200,
            body: payload,
        }
    });
    run_watcher(&app);
    assert_eq!(checkout_status_of(&app, &checkout_id), "paid");
}

#[test]
fn start_failed_is_409() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (token, checkout_id, _, _) = start_solana(&app);
    app.pool
        .get()
        .expect("pool")
        .execute(
            "UPDATE public.checkouts SET \"Status\" = 'failed' WHERE \"Id\" = $1",
            &[&checkout_id],
        )
        .unwrap();
    let again = start_pay(&app, &token, r#"{"name":"Ada"}"#);
    assert_eq!(again.status(), 409);
    let get = call(ureq::get(&format!("{}/v1/pay/{token}", app.base_url)));
    let doc: serde_json::Value = get.into_json().unwrap();
    assert_eq!(doc["status"], "failed");
    assert!(doc["solana_pay_url"].is_null());
}

#[test]
fn stale_open_checkout_emits_payment_failed() {
    let app = TestApp::spawn();
    owner_one(&app);
    let (_, checkout_id, _, _) = start_solana(&app);
    app.pool
        .get()
        .expect("pool")
        .execute(
            "UPDATE public.checkouts SET \"CreatedAt\" = $1 WHERE \"Id\" = $2",
            &[
                &(chrono::Utc::now() - chrono::Duration::minutes(31)),
                &checkout_id,
            ],
        )
        .unwrap();
    run_watcher(&app);
    assert_eq!(checkout_status_of(&app, &checkout_id), "failed");
    let event: String = app
        .pool
        .get()
        .expect("pool")
        .query_one("SELECT \"EventId\" FROM public.psp_webhook_events", &[])
        .unwrap()
        .get(0);
    assert!(event.starts_with("watch_timeout:"), "{event}");
    assert_eq!(docs_count(&app), 0);
}
