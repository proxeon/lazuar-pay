//! Port of `SolanaTx.cs` — the on-chain validation: a transaction pays THIS
//! checkout only when the transfer-checked USDC movement lands on the merchant,
//! in the pinned mint, in the pinned amount, referencing THIS checkout's
//! reference key, carrying THIS checkout id as memo, under the correct token
//! program, in a succeeded transaction at `finalized` commitment.

use serde_json::Value;

use super::cluster;
use super::money::try_to_atomic;

pub const MEMO_PROGRAM: &str = cluster::MEMO_PROGRAM;

pub struct ValidateInput<'a> {
    pub checkout_id: &'a str,
    pub checkout_amount: Decimal,
    pub provider_session_id: &'a str,
    pub public_merchant_id: &'a str,
    pub signature: &'a str,
    pub cluster: &'a str,
}

use rust_decimal::Decimal;

/// `SolanaTx.Validate`: `Ok(())` when the transaction settles the checkout;
/// `Err(reason)` is the 400 detail.
pub fn validate(rpc: &Value, input: &ValidateInput) -> Result<(), String> {
    let Some(result) = rpc.get("result").filter(|r| !r.is_null()) else {
        return Err("transaction not found".into());
    };

    if let Some(meta) = result.get("meta").filter(|m| m.is_object()) {
        let errored = meta.get("err").is_some_and(|e| !e.is_null());
        if errored {
            return Err("transaction failed".into());
        }
    }

    let Some(tx) = result.get("transaction") else {
        return Err("transaction missing".into());
    };
    let Some(message) = tx.get("message").filter(|m| m.is_object()) else {
        return Err("transaction missing".into());
    };

    let mint = cluster::mint(input.cluster);
    let expected = try_to_atomic(input.checkout_amount).ok_or("amount mismatch")?;

    transfer_mismatch(message, result, input.public_merchant_id, mint, expected)?;

    if !has_reference(message, Some(input.provider_session_id)) {
        eprintln!("[dbg-tx] reference missing: session_id={:?}", input.provider_session_id);
        return Err("reference missing".into());
    }

    if !has_memo(message, input.checkout_id) {
        return Err("memo mismatch".into());
    }

    if let Some(sigs) = tx.get("signatures").and_then(Value::as_array) {
        let listed: Vec<&str> = sigs.iter().filter_map(Value::as_str).collect();
        if !listed.is_empty() && !listed.contains(&input.signature) {
            return Err("signature mismatch".into());
        }
    }

    Ok(())
}

fn transfer_mismatch(
    message: &Value,
    result: &Value,
    merchant: &str,
    mint: &str,
    expected: i64,
) -> Result<(), String> {
    if merchant.trim().is_empty() {
        return Err("destination mismatch".into());
    }
    let Some(ixs) = message.get("instructions").and_then(Value::as_array) else {
        return Err("transfer missing".into());
    };
    let Some(meta) = result.get("meta").filter(|m| m.is_object()) else {
        return Err("destination mismatch".into());
    };

    let keys = account_pubkeys(message);
    let pre = token_balances(meta, "preTokenBalances");
    let post = token_balances(meta, "postTokenBalances");
    let mut any_transfer = false;
    let mut token2022_to_merchant = false;
    let mut bound_wrong_mint = false;
    let mut bound_wrong_amount = false;

    for ix in ixs {
        eprintln!("[dbg-tx] ix program={:?} parsed={:?}", ix.get("programId").and_then(Value::as_str), ix.get("parsed").and_then(|p| p.get("type")).and_then(Value::as_str));
        let program_id = ix.get("programId").and_then(Value::as_str).unwrap_or("");
        let Some(parsed) = ix.get("parsed").filter(|p| p.is_object()) else {
            continue;
        };
        let Some(transfer_type) = parsed.get("type").and_then(Value::as_str) else {
            continue;
        };
        if transfer_type != "transferChecked" {
            continue;
        }

        any_transfer = true;
        let Some(info) = parsed.get("info") else { continue };
        let dest = info.get("destination").and_then(Value::as_str).unwrap_or("");
        let found_mint_dbg = info.get("mint").and_then(Value::as_str).unwrap_or("");
        let atomic_dbg = info
            .get("tokenAmount")
            .and_then(|t| t.get("amount"))
            .map(|a| a.as_str().map(str::to_string).unwrap_or_else(|| a.to_string()))
            .unwrap_or_default();
        eprintln!("[dbg-tx] transferChecked dest={dest} mint={found_mint_dbg} atomic={atomic_dbg}");
        let found_mint = info.get("mint").and_then(Value::as_str).unwrap_or("");
        let Some(token_amount) = info.get("tokenAmount") else { continue };
        let Some(atomic) = try_atomic(token_amount) else { continue };
        eprintln!("[dbg-tx] atomic={atomic}");

                let Some(dest_index) = keys.iter().position(|k| k == dest).map(|p| p as i64) else {
            eprintln!("[dbg-tx] continue: destination key not in accountKeys");
            continue;
        };
        let Some(post_row) = post.get(&dest_index) else {
            eprintln!("[dbg-tx] continue: no post balance at index {dest_index}");
            continue;
        };

        if !post_row.owner.eq_ignore_ascii_case(merchant) {
            eprintln!("[dbg-tx] continue: owner {} != merchant {}", post_row.owner, merchant);
            continue;
        }

        if program_id == cluster::TOKEN2022_PROGRAM {
            token2022_to_merchant = true;
            continue;
        }

        if program_id != cluster::TOKEN_PROGRAM {
            continue;
        }

        if found_mint != mint || post_row.mint != mint {
            bound_wrong_mint = true;
            continue;
        }

        let pre_amount = pre.get(&dest_index).map(|p| p.amount).unwrap_or(0);
        eprintln!("[dbg-tx] atomic={atomic} expected={expected} post={} pre={pre_amount}", post_row.amount);
        if atomic != expected || post_row.amount - pre_amount != expected {
            eprintln!("[dbg-tx] continue: bound_wrong_amount");
            bound_wrong_amount = true;
            continue;
        }

        return Ok(());
    }

    if token2022_to_merchant {
        return Err("token program mismatch".into());
    }
    if bound_wrong_mint {
        return Err("mint mismatch".into());
    }
    if bound_wrong_amount {
        return Err("amount mismatch".into());
    }
    if any_transfer {
        return Err("destination mismatch".into());
    }
    Err("transfer missing".into())
}

fn account_pubkeys(message: &Value) -> Vec<String> {
    let mut keys = Vec::new();
    if let Some(arr) = message.get("accountKeys").and_then(Value::as_array) {
        for key in arr {
            let pk = if let Some(pk) = key.as_str() {
                Some(pk.to_string())
            } else {
                key.get("pubkey").and_then(Value::as_str).map(str::to_string)
            };
            if let Some(pk) = pk.filter(|p| !p.trim().is_empty()) {
                keys.push(pk);
            }
        }
    }
    keys
}

struct TokenBalance {
    owner: String,
    mint: String,
    amount: i64,
}

fn token_balances(meta: &Value, name: &str) -> std::collections::HashMap<i64, TokenBalance> {
    let mut map = std::collections::HashMap::new();
    let Some(bals) = meta.get(name).and_then(Value::as_array) else {
        return map;
    };
    for balance in bals {
        let Some(idx) = balance.get("accountIndex").and_then(Value::as_i64) else { continue };
        let owner = balance.get("owner").and_then(Value::as_str).unwrap_or("").to_string();
        let mint = balance.get("mint").and_then(Value::as_str).unwrap_or("").to_string();
        let Some(ui) = balance.get("uiTokenAmount") else { continue };
        let Some(amount) = try_atomic(ui) else { continue };
        map.insert(idx, TokenBalance { owner, mint, amount });
    }
    map
}

fn try_atomic(token_amount: &Value) -> Option<i64> {
    let amount = token_amount.get("amount")?;
    if let Some(s) = amount.as_str() {
        return s.parse().ok();
    }
    amount.as_i64()
}

fn has_reference(message: &Value, reference: Option<&str>) -> bool {
    let Some(reference) = reference.filter(|r| !r.trim().is_empty()) else {
        return false;
    };
    let Some(keys) = message.get("accountKeys").and_then(Value::as_array) else {
        return false;
    };
    keys.iter().any(|key| {
        let pk = if let Some(pk) = key.as_str() {
            Some(pk)
        } else {
            key.get("pubkey").and_then(Value::as_str)
        };
        pk == Some(reference)
    })
}

fn has_memo(message: &Value, checkout_id: &str) -> bool {
    let Some(ixs) = message.get("instructions").and_then(Value::as_array) else {
        return false;
    };
    for ix in ixs {
        let program_id = ix.get("programId").and_then(Value::as_str);
        if program_id != Some(MEMO_PROGRAM) {
            continue;
        }
        let Some(parsed) = ix.get("parsed") else { continue };
        let text = if let Some(t) = parsed.as_str() {
            Some(t)
        } else if let Some(memo) = parsed.pointer("/info/memo").and_then(Value::as_str) {
            Some(memo)
        } else {
            parsed.get("memo").and_then(Value::as_str)
        };
        if text == Some(checkout_id) {
            return true;
        }
    }
    false
}
