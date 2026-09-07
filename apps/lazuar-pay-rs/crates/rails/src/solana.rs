//! Solana Pay URI mint. Confirm is the chain watcher, not an inbound webhook.
//! URI `amount=` is display (`10`); SPL `tokenAmount.amount` is atomic (`10000000`).

use std::collections::HashMap;
use std::sync::{Arc, Mutex};

use domain::money::{Currency, Money};
use domain::rail::HostedSession;
use num_bigint::BigUint;
use num_traits::{One, Zero};
use serde_json::Value;

pub const MEMO_PROGRAM: &str = "MemoSq4gqABAXKb96qnH8TysNcWxMyWCqXgDLGmfcHr";
pub const TOKEN_PROGRAM: &str = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";
pub const TOKEN_2022_PROGRAM: &str = "TokenzQdBNbLqP5VEhdkAS6EPFLC1PHnBqCXEpPxuEb";
pub const MAINNET_MINT: &str = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";
pub const DEVNET_MINT: &str = "4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU";
pub const MAINNET_GENESIS: &str = "5eykt4UsFv8P8NJdTREpY1vzqKqZKvdpKuc147dw2N9d";
pub const DEVNET_GENESIS: &str = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
pub const MERCHANT_ATA: &str = "Dest11111111111111111111111111111111111112";
pub const BUYER_ATA: &str = "Buyr11111111111111111111111111111111111112";
pub const WEBHOOK_THROW: &str = "solana does not use inbound PSP webhooks";

const B58: &[u8] = b"123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

#[derive(Debug, thiserror::Error, PartialEq, Eq)]
pub enum SolanaError {
    #[error("solana does not use inbound PSP webhooks")]
    Webhook,
    #[error("amount is not a valid USDC amount")]
    InvalidAmount,
    #[error("rail not configured")]
    NotConfigured,
    #[error("{0}")]
    Mismatch(&'static str),
    #[error("solana RPC throttled")]
    Throttled,
    #[error("solana RPC rejected the method")]
    Rejected,
}

pub fn parse_webhook(_body: &[u8], _sig: Option<&str>, _secret: &str) -> Result<(), SolanaError> {
    Err(SolanaError::Webhook)
}

pub fn normalize_cluster(raw: &str) -> Option<String> {
    let mut c = raw.trim().to_ascii_lowercase();
    if c == "mainnet" {
        c = "mainnet-beta".into();
    }
    matches!(c.as_str(), "mainnet-beta" | "devnet").then_some(c)
}

pub fn normalize_vault_env(raw: &str) -> Option<String> {
    let mut e = raw.trim().to_ascii_lowercase();
    if e == "mainnet-beta" {
        e = "mainnet".into();
    }
    matches!(e.as_str(), "devnet" | "mainnet").then_some(e)
}

pub fn vault_environment(cluster: &str) -> &'static str {
    if cluster == "mainnet-beta" || cluster == "mainnet" {
        "mainnet"
    } else {
        "devnet"
    }
}

pub fn matches_vault(cluster: &str, vault_env: &str) -> bool {
    let Some(env) = normalize_vault_env(vault_env) else {
        return false;
    };
    env == vault_environment(cluster)
}

pub fn mint(cluster: &str) -> &'static str {
    if vault_environment(cluster) == "mainnet" {
        MAINNET_MINT
    } else {
        DEVNET_MINT
    }
}

pub fn genesis_hash(cluster: &str) -> &'static str {
    if vault_environment(cluster) == "mainnet" {
        MAINNET_GENESIS
    } else {
        DEVNET_GENESIS
    }
}

pub fn parse_genesis_hash(json: &str) -> Option<String> {
    let v: Value = serde_json::from_str(json).ok()?;
    let hash = v.get("result")?.as_str()?.trim();
    if hash.is_empty() {
        None
    } else {
        Some(hash.to_string())
    }
}

pub fn try_to_atomic(quoted: Money) -> Result<i64, SolanaError> {
    if quoted.currency() != Currency::USDC_SOLANA || quoted.minor() <= 0 {
        return Err(SolanaError::InvalidAmount);
    }
    i64::try_from(quoted.minor()).map_err(|_| SolanaError::InvalidAmount)
}

fn display_amount(quoted: Money) -> Result<String, SolanaError> {
    let d = quoted
        .to_quoted_display()
        .map_err(|_| SolanaError::InvalidAmount)?;
    Ok(d.normalize().to_string())
}

fn encode_component(s: &str) -> String {
    let mut out = String::new();
    for b in s.bytes() {
        match b {
            b'A'..=b'Z' | b'a'..=b'z' | b'0'..=b'9' | b'-' | b'_' | b'.' | b'~' => {
                out.push(b as char);
            }
            _ => out.push_str(&format!("%{b:02X}")),
        }
    }
    out
}

pub fn pay_uri(
    vault: &str,
    quoted: Money,
    payment_id: &str,
    cluster: &str,
) -> Result<HostedSession, SolanaError> {
    let Some(recipient) = try_normalize(vault) else {
        return Err(SolanaError::NotConfigured);
    };
    let _ = try_to_atomic(quoted)?;
    let amount = display_amount(quoted)?;
    let mint = mint(cluster);
    let mut raw = [0u8; 32];
    getrandom::getrandom(&mut raw).expect("rand");
    let reference = encode(&raw);
    let memo = encode_component(payment_id);
    let label = encode_component("Lazuar Pay");
    let url = format!(
        "solana:{recipient}?amount={amount}&spl-token={mint}&reference={reference}&label={label}&memo={memo}"
    );
    Ok(HostedSession {
        url,
        session_id: reference,
    })
}

pub fn last4(address: &str) -> &str {
    if address.len() >= 4 {
        &address[address.len() - 4..]
    } else {
        address
    }
}

pub fn looks_like_secret(raw: &str) -> bool {
    let t = raw.trim();
    if t.to_ascii_uppercase().contains("-----BEGIN")
        || t.to_ascii_uppercase().contains("-----END")
        || t.contains("-----")
        || t.contains(' ')
        || t.contains(':')
        || t.to_ascii_lowercase().contains("https://")
        || t.to_ascii_lowercase().contains("http://")
    {
        return true;
    }
    let lower = t.to_ascii_lowercase();
    for p in ["sk_", "rk_", "whsec_", "lzr_sk_"] {
        if lower.starts_with(p) {
            return true;
        }
    }
    if (t.len() == 64 || t.len() == 128) && t.bytes().all(|c| c.is_ascii_hexdigit()) {
        return true;
    }
    false
}

pub fn try_normalize(raw: &str) -> Option<String> {
    let trimmed = raw.trim();
    if trimmed.is_empty() || looks_like_secret(trimmed) {
        return None;
    }
    let bytes = decode(trimmed)?;
    if bytes.len() != 32 || !is_on_ed25519(&bytes) {
        return None;
    }
    let address = encode(&bytes);
    (address.len() >= 32 && address.len() <= 44).then_some(address)
}

pub fn sample_address() -> String {
    for _ in 0..64 {
        let mut raw = [0u8; 32];
        getrandom::getrandom(&mut raw).expect("rand");
        if let Some(a) = try_normalize(&encode(&raw)) {
            return a;
        }
    }
    panic!("could not sample an on-curve Solana address");
}

pub fn encode(data: &[u8]) -> String {
    let mut zeros = 0;
    while zeros < data.len() && data[zeros] == 0 {
        zeros += 1;
    }
    let size = data.len() * 138 / 100 + 1;
    let mut buf = vec![0u8; size];
    let mut length = 0;
    for &b in &data[zeros..] {
        let mut carry = b as usize;
        let mut j = 0;
        for k in (0..buf.len()).rev() {
            if carry == 0 && j >= length {
                break;
            }
            carry += 256 * buf[k] as usize;
            buf[k] = (carry % 58) as u8;
            carry /= 58;
            j += 1;
        }
        length = j;
    }
    let mut skip = 0;
    while skip < buf.len() && buf[skip] == 0 {
        skip += 1;
    }
    let mut chars = vec![b'1'; zeros];
    for &d in &buf[skip..] {
        chars.push(B58[d as usize]);
    }
    String::from_utf8(chars).unwrap_or_default()
}

pub fn decode(input: &str) -> Option<Vec<u8>> {
    let s = input.trim();
    if s.is_empty() {
        return None;
    }
    let mut zeros = 0;
    while zeros < s.len() && s.as_bytes()[zeros] == b'1' {
        zeros += 1;
    }
    let size = s.len() * 733 / 1000 + 1;
    let mut buf = vec![0u8; size];
    let mut length = 0;
    for ch in s[zeros..].chars() {
        let val = B58.iter().position(|&c| c == ch as u8)?;
        let mut carry = val;
        let mut j = 0;
        for k in (0..buf.len()).rev() {
            if carry == 0 && j >= length {
                break;
            }
            carry += 58 * buf[k] as usize;
            buf[k] = (carry % 256) as u8;
            carry /= 256;
            j += 1;
        }
        if carry != 0 {
            return None;
        }
        length = j;
    }
    let mut skip = 0;
    while skip < buf.len() && buf[skip] == 0 {
        skip += 1;
    }
    let mut bytes = vec![0u8; zeros];
    bytes.extend_from_slice(&buf[skip..]);
    Some(bytes)
}

/// RFC 8032 point decode: 32-byte compressed ed25519 public key is on the curve.
pub fn is_on_ed25519(pk: &[u8]) -> bool {
    if pk.len() != 32 {
        return false;
    }
    let p = (BigUint::one() << 255) - BigUint::from(19u32);
    let mut y_bytes = [0u8; 32];
    y_bytes.copy_from_slice(pk);
    y_bytes[31] &= 0x7f;
    let y = BigUint::from_bytes_le(&y_bytes);
    if y >= p {
        return false;
    }
    let d = {
        let inv = mod_pow(&BigUint::from(121666u32), &(&p - 2u32), &p);
        let n = BigUint::from(121665u32) * inv;
        (&p - (n % &p)) % &p
    };
    let y2 = (&y * &y) % &p;
    let u = if y2 >= BigUint::one() {
        (&y2 - BigUint::one()) % &p
    } else {
        (&p + &y2 - BigUint::one()) % &p
    };
    let v = (&d * &y2 + BigUint::one()) % &p;
    if v.is_zero() {
        return false;
    }
    let x2 = (&u * mod_pow(&v, &(&p - 2u32), &p)) % &p;
    if x2.is_zero() {
        return true;
    }
    mod_pow(&x2, &((&p - 1u32) / 2u32), &p).is_one()
}

fn mod_pow(base: &BigUint, exp: &BigUint, m: &BigUint) -> BigUint {
    base.modpow(exp, m)
}

fn json_str<'a>(v: &'a Value, key: &str) -> Option<&'a str> {
    v.get(key).and_then(|x| x.as_str())
}

fn pubkey_of(key: &Value) -> Option<&str> {
    if let Some(s) = key.as_str() {
        return Some(s);
    }
    json_str(key, "pubkey")
}

fn account_pubkeys(message: &Value) -> Vec<String> {
    let Some(arr) = message.get("accountKeys").and_then(|x| x.as_array()) else {
        return vec![];
    };
    arr.iter()
        .filter_map(pubkey_of)
        .filter(|s| !s.is_empty())
        .map(str::to_string)
        .collect()
}

pub fn locators_in_tx(raw: &str) -> Vec<String> {
    let Ok(v) = serde_json::from_str::<Value>(raw) else {
        return vec![];
    };
    let Some(result) = v.get("result") else {
        return vec![];
    };
    let Some(msg) = result
        .get("transaction")
        .and_then(|t| t.get("message"))
        .filter(|m| m.is_object())
    else {
        return vec![];
    };
    account_pubkeys(msg)
}

pub fn signatures_from_rpc(raw: &str) -> Vec<String> {
    let Ok(v) = serde_json::from_str::<Value>(raw) else {
        return vec![];
    };
    let Some(arr) = v.get("result").and_then(|x| x.as_array()) else {
        return vec![];
    };
    arr.iter()
        .filter_map(|item| {
            if let Some(s) = item.as_str() {
                return Some(s.to_string());
            }
            json_str(item, "signature").map(str::to_string)
        })
        .filter(|s| !s.is_empty())
        .collect()
}

fn try_atomic(token_amount: &Value) -> Option<i64> {
    let amt = token_amount.get("amount")?;
    if let Some(s) = amt.as_str() {
        return s.parse().ok();
    }
    amt.as_i64()
        .or_else(|| amt.as_u64().and_then(|u| i64::try_from(u).ok()))
}

struct Bal {
    owner: String,
    mint: String,
    amount: i64,
}

fn token_balances(meta: &Value, name: &str) -> HashMap<i32, Bal> {
    let mut map = HashMap::new();
    let Some(arr) = meta.get(name).and_then(|x| x.as_array()) else {
        return map;
    };
    for b in arr {
        let Some(idx) = b.get("accountIndex").and_then(|x| x.as_i64()) else {
            continue;
        };
        let Some(ui) = b.get("uiTokenAmount") else {
            continue;
        };
        let Some(amount) = try_atomic(ui) else {
            continue;
        };
        map.insert(
            idx as i32,
            Bal {
                owner: json_str(b, "owner").unwrap_or("").to_string(),
                mint: json_str(b, "mint").unwrap_or("").to_string(),
                amount,
            },
        );
    }
    map
}

fn has_reference(message: &Value, reference: &str) -> bool {
    if reference.is_empty() {
        return false;
    }
    account_pubkeys(message).iter().any(|k| k == reference)
}

fn has_memo(message: &Value, checkout_id: &str) -> bool {
    let Some(ixs) = message.get("instructions").and_then(|x| x.as_array()) else {
        return false;
    };
    for ix in ixs {
        if json_str(ix, "programId") != Some(MEMO_PROGRAM) {
            continue;
        }
        let Some(parsed) = ix.get("parsed") else {
            continue;
        };
        let text = if let Some(s) = parsed.as_str() {
            Some(s)
        } else if let Some(s) = parsed.get("info").and_then(|i| json_str(i, "memo")) {
            Some(s)
        } else {
            json_str(parsed, "memo")
        };
        if text == Some(checkout_id) {
            return true;
        }
    }
    false
}

fn transfer_mismatch(
    message: &Value,
    result: &Value,
    merchant: &str,
    mint: &str,
    expected: i64,
) -> Option<&'static str> {
    if merchant.is_empty() {
        return Some("destination mismatch");
    }
    let Some(ixs) = message.get("instructions").and_then(|x| x.as_array()) else {
        return Some("transfer missing");
    };
    let Some(meta) = result.get("meta").filter(|m| m.is_object()) else {
        return Some("destination mismatch");
    };
    let keys = account_pubkeys(message);
    let pre = token_balances(meta, "preTokenBalances");
    let post = token_balances(meta, "postTokenBalances");
    let mut any_transfer = false;
    let mut token2022 = false;
    let mut wrong_mint = false;
    let mut wrong_amount = false;

    for ix in ixs {
        let program_id = json_str(ix, "programId").unwrap_or("");
        let Some(parsed) = ix.get("parsed").filter(|p| p.is_object()) else {
            continue;
        };
        if json_str(parsed, "type") != Some("transferChecked") {
            continue;
        }
        any_transfer = true;
        let Some(info) = parsed.get("info") else {
            continue;
        };
        let dest = json_str(info, "destination").unwrap_or("");
        let found_mint = json_str(info, "mint").unwrap_or("");
        let Some(ta) = info.get("tokenAmount") else {
            continue;
        };
        let Some(atomic) = try_atomic(ta) else {
            continue;
        };
        let dest_index = keys.iter().position(|k| k == dest);
        let Some(dest_index) = dest_index else {
            continue;
        };
        let Some(post_row) = post.get(&(dest_index as i32)) else {
            continue;
        };
        if post_row.owner != merchant {
            continue;
        }
        if program_id == TOKEN_2022_PROGRAM {
            token2022 = true;
            continue;
        }
        if program_id != TOKEN_PROGRAM {
            continue;
        }
        if found_mint != mint || post_row.mint != mint {
            wrong_mint = true;
            continue;
        }
        let pre_amt = pre.get(&(dest_index as i32)).map(|r| r.amount).unwrap_or(0);
        if atomic != expected || post_row.amount - pre_amt != expected {
            wrong_amount = true;
            continue;
        }
        return None;
    }
    if token2022 {
        return Some("token program mismatch");
    }
    if wrong_mint {
        return Some("mint mismatch");
    }
    if wrong_amount {
        return Some("amount mismatch");
    }
    Some(if any_transfer {
        "destination mismatch"
    } else {
        "transfer missing"
    })
}

/// `SolanaTx.Validate`. Success is `Ok(())`; mismatch is a C# detail string.
pub fn validate(
    rpc_json: &str,
    vault: &str,
    expected_atomic: i64,
    mint: &str,
    reference: &str,
    payment_id: &str,
    signature: &str,
) -> Result<(), SolanaError> {
    let v: Value = serde_json::from_str(rpc_json)
        .map_err(|_| SolanaError::Mismatch("transaction not found"))?;
    let result = v.get("result");
    if result.is_none() || result.is_some_and(|r| r.is_null()) {
        return Err(SolanaError::Mismatch("transaction not found"));
    }
    let result = result.unwrap();
    if let Some(err) = result.get("meta").and_then(|m| m.get("err")) {
        if !err.is_null() {
            return Err(SolanaError::Mismatch("transaction failed"));
        }
    }
    let Some(tx) = result.get("transaction") else {
        return Err(SolanaError::Mismatch("transaction missing"));
    };
    let Some(message) = tx.get("message").filter(|m| m.is_object()) else {
        return Err(SolanaError::Mismatch("transaction missing"));
    };
    if let Some(detail) = transfer_mismatch(message, result, vault, mint, expected_atomic) {
        return Err(SolanaError::Mismatch(detail));
    }
    if !has_reference(message, reference) {
        return Err(SolanaError::Mismatch("reference missing"));
    }
    if !has_memo(message, payment_id) {
        return Err(SolanaError::Mismatch("memo mismatch"));
    }
    if let Some(sigs) = tx.get("signatures").and_then(|x| x.as_array()) {
        if !sigs.is_empty() {
            let listed: Vec<&str> = sigs.iter().filter_map(|x| x.as_str()).collect();
            if !listed.contains(&signature) {
                return Err(SolanaError::Mismatch("signature mismatch"));
            }
        }
    }
    Ok(())
}

pub fn tx_json(
    signature: &str,
    owner: &str,
    mint: &str,
    atomic: &str,
    reference: &str,
    memo: &str,
) -> String {
    format!(
        r#"{{
          "jsonrpc": "2.0",
          "result": {{
            "slot": 1,
            "meta": {{
              "err": null,
              "preTokenBalances": [
                {{ "accountIndex": 1, "mint": "{mint}", "owner": "{owner}", "uiTokenAmount": {{ "amount": "0", "decimals": 6 }} }}
              ],
              "postTokenBalances": [
                {{ "accountIndex": 1, "mint": "{mint}", "owner": "{owner}", "uiTokenAmount": {{ "amount": "{atomic}", "decimals": 6 }} }}
              ]
            }},
            "transaction": {{
              "signatures": ["{signature}"],
              "message": {{
                "accountKeys": [
                  {{ "pubkey": "11111111111111111111111111111111", "signer": true, "writable": true }},
                  {{ "pubkey": "{MERCHANT_ATA}", "signer": false, "writable": true }},
                  {{ "pubkey": "{TOKEN_PROGRAM}", "signer": false, "writable": false }},
                  {{ "pubkey": "{reference}", "signer": false, "writable": false }}
                ],
                "instructions": [
                  {{
                    "programId": "{TOKEN_PROGRAM}",
                    "parsed": {{
                      "type": "transferChecked",
                      "info": {{
                        "mint": "{mint}",
                        "destination": "{MERCHANT_ATA}",
                        "tokenAmount": {{ "amount": "{atomic}", "decimals": 6 }}
                      }}
                    }}
                  }},
                  {{
                    "programId": "{MEMO_PROGRAM}",
                    "parsed": "{memo}"
                  }}
                ]
              }}
            }}
          }}
        }}"#
    )
}

pub fn decoy_json(
    signature: &str,
    owner: &str,
    mint: &str,
    atomic: &str,
    reference: &str,
    memo: &str,
) -> String {
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
                  {{ "pubkey": "{BUYER_ATA}", "signer": false, "writable": true }},
                  {{ "pubkey": "{MERCHANT_ATA}", "signer": false, "writable": true }},
                  {{ "pubkey": "{TOKEN_PROGRAM}", "signer": false, "writable": false }},
                  {{ "pubkey": "{reference}", "signer": false, "writable": false }}
                ],
                "instructions": [
                  {{
                    "programId": "{TOKEN_PROGRAM}",
                    "parsed": {{
                      "type": "transferChecked",
                      "info": {{
                        "mint": "{mint}",
                        "destination": "{BUYER_ATA}",
                        "tokenAmount": {{ "amount": "{atomic}", "decimals": 6 }}
                      }}
                    }}
                  }},
                  {{
                    "programId": "{MEMO_PROGRAM}",
                    "parsed": "{memo}"
                  }}
                ]
              }}
            }}
          }}
        }}"#
    )
}

#[derive(Clone)]
pub struct FakeSolanaRpc {
    txs: Arc<Mutex<HashMap<String, String>>>,
    sigs: Arc<Mutex<HashMap<String, String>>>,
    throttled: Arc<Mutex<bool>>,
    rejected: Arc<Mutex<bool>>,
}

impl Default for FakeSolanaRpc {
    fn default() -> Self {
        Self {
            txs: Arc::new(Mutex::new(HashMap::new())),
            sigs: Arc::new(Mutex::new(HashMap::new())),
            throttled: Arc::new(Mutex::new(false)),
            rejected: Arc::new(Mutex::new(false)),
        }
    }
}

impl FakeSolanaRpc {
    pub fn set_tx(&self, signature: &str, body: &str) {
        self.txs
            .lock()
            .expect("lock")
            .insert(signature.to_string(), body.to_string());
    }

    pub fn set_sigs(&self, reference: &str, signatures: &[&str]) {
        let items: Vec<String> = signatures
            .iter()
            .map(|s| format!(r#"{{"signature":"{s}"}}"#))
            .collect();
        let body = format!(r#"{{"jsonrpc":"2.0","result":[{}]}}"#, items.join(","));
        self.sigs
            .lock()
            .expect("lock")
            .insert(reference.to_string(), body);
    }

    pub fn set_throttled(&self, v: bool) {
        *self.throttled.lock().expect("lock") = v;
    }

    pub fn set_rejected(&self, v: bool) {
        *self.rejected.lock().expect("lock") = v;
    }

    pub fn get_transaction(&self, signature: &str) -> Result<String, SolanaError> {
        if *self.throttled.lock().expect("lock") {
            return Err(SolanaError::Throttled);
        }
        if *self.rejected.lock().expect("lock") {
            return Err(SolanaError::Rejected);
        }
        Ok(self
            .txs
            .lock()
            .expect("lock")
            .get(signature)
            .cloned()
            .unwrap_or_else(|| r#"{"jsonrpc":"2.0","result":null}"#.into()))
    }

    pub fn get_signatures(&self, reference: &str) -> Result<String, SolanaError> {
        if *self.throttled.lock().expect("lock") {
            return Err(SolanaError::Throttled);
        }
        if *self.rejected.lock().expect("lock") {
            return Err(SolanaError::Rejected);
        }
        Ok(self
            .sigs
            .lock()
            .expect("lock")
            .get(reference)
            .cloned()
            .unwrap_or_else(|| r#"{"jsonrpc":"2.0","result":[]}"#.into()))
    }
}
