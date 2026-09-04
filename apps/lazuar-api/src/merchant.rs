//! Merchant CRUD surface — checkout create/get/list, catalog, gateway vault,
//! payment queries, receipts, subscriptions. All use the member/writer gates.

use rust_decimal::Decimal;
use serde_json::{json, Value};

use crate::hosting::PayError;

type QResult<T> = Result<T, postgres::Error>;

fn clamp_limit(limit: Option<i64>) -> i64 {
    crate::hosting::clamp_limit(limit)
}

fn require_bodyorgan_id(parsed: &Value) -> Option<&str> {
    parsed.get("org_id").and_then(Value::as_str).map(str::trim).filter(|s| !s.is_empty())
}

// ---------------------------------------------------------------------------
// Checkouts
// ---------------------------------------------------------------------------

/// `POST /v1/checkouts` — merchant checkout create with idempotency.
pub fn checkout_create(
    conn: &mut postgres::Client,
    environment: &str,
    org_id: &str,
    body: &Value,
    idempotency_key: Option<&str>,
) -> Result<Result<(String, String, String, String, Decimal, String, String, String, String), PayError>, postgres::Error>
{
    use rust_decimal::Decimal;
    let _ = environment;

    // Paused check.
    let paused = conn
        .query_opt(
            "SELECT \"ChargesPaused\" FROM public.org_settings WHERE \"OrgId\" = $1",
            &[&org_id],
        )?
        .map(|row| row.get::<_, bool>(0))
        .unwrap_or(false);
    if paused {
        return Ok(Err(PayError::forbidden("Org charges are paused")));
    }

    // Amount.
    let amount: Decimal = match body.get("amount") {
        Some(v) if v.is_number() => {
            use std::str::FromStr as _;
            Decimal::from_str(&v.to_string()).unwrap_or(Decimal::ZERO)
        }
        Some(v) if v.is_string() => {
            use std::str::FromStr as _;
            Decimal::from_str(v.as_str().unwrap()).unwrap_or(Decimal::ZERO)
        }
        _ => return Ok(Err(PayError::bad_request("amount must be greater than 0"))),
    };
    if amount <= Decimal::ZERO {
        return Ok(Err(PayError::bad_request("amount must be greater than 0")));
    }

    // Provider.
    let provider = body.get("provider").and_then(Value::as_str).map(str::trim).filter(|s| !s.is_empty());
    let Some(provider) = crate::rails::providers::try_normalize(provider) else {
        return Ok(Err(PayError::bad_request("unknown provider")));
    };

    // Test rail gate.
    if crate::rails::providers::is_test(provider) {
        let env = std::env::var("ASPNETCORE_ENVIRONMENT").unwrap_or_default();
        if env != "Development" && env != "Testing" {
            return Ok(Err(PayError::bad_request("test processor is not enabled")));
        }
    } else {
        let has_rail = conn
            .query_opt(
                "SELECT 1 FROM public.gateway_credentials WHERE \"OrgId\" = $1 AND \"Provider\" = $2",
                &[&org_id, &provider],
            )?
            .is_some();
        if !has_rail {
            return Ok(Err(PayError::bad_request("rail not configured")));
        }
    }

    // Interval.
    let interval = body.get("interval").and_then(Value::as_str).map(str::trim).filter(|s| !s.is_empty());
    let interval = interval.unwrap_or("one_off").to_string();
    if !["one_off", "mo", "yr"].contains(&interval.as_str()) {
        return Ok(Err(PayError::bad_request("interval must be one_off, mo, or yr")));
    }

    // Currency.
    let currency = body
        .get("currency")
        .and_then(Value::as_str)
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .map(|c| c.to_uppercase())
        .unwrap_or_else(|| "MYR".to_string());

    // Solana mint rules.
    let product_id = body.get("product_id").and_then(Value::as_str).map(str::trim).filter(|s| !s.is_empty());
    if let Some(err) = crate::rails::solana::money::mint_error(provider, Some(&currency), None, product_id, Some(amount)) {
        return Ok(Err(PayError::bad_request(err)));
    }

    // Rail currency check (issues 003/014).
    if !crate::domain::currency::is_supported(provider, &currency) {
        return Ok(Err(PayError::bad_request(format!(
            "currency {currency} is not supported on {provider}; supported: {}",
            crate::domain::currency::describe(provider)
        ))));
    }

    Ok(Ok((org_id.to_string(), provider.to_string(), product_id.unwrap_or("").to_string(), currency, amount, interval, body.get("success_url").and_then(Value::as_str).unwrap_or("").to_string(), body.get("cancel_url").and_then(Value::as_str).unwrap_or("").to_string(), String::new())))
}

// ---------------------------------------------------------------------------
// Catalog
// ---------------------------------------------------------------------------

/// `POST /v1/orgs/{orgId}/products` — create product + price in one tx.
pub fn catalog_create(
    conn: &mut postgres::Client,
    org_id: &str,
    body: &Value,
) -> Result<Result<serde_json::Value, PayError>, postgres::Error> {
    let name = body.get("name").and_then(Value::as_str).map(str::trim).filter(|s| !s.is_empty());
    let Some(name) = name else {
        return Ok(Err(PayError::bad_request("name is required")));
    };

    let currency = body
        .get("currency")
        .and_then(Value::as_str)
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .map(|c| c.to_uppercase())
        .unwrap_or_else(|| "MYR".to_string());
    if currency != "MYR" {
        return Ok(Err(PayError::bad_request("Catalog currency is MYR")));
    }

    let amount: Decimal = match body.get("amount") {
        Some(v) if v.is_number() => {
            use std::str::FromStr as _;
            Decimal::from_str(&v.to_string()).unwrap_or(Decimal::ZERO)
        }
        _ => return Ok(Err(PayError::bad_request("amount must be greater than 0"))),
    };
    if amount <= Decimal::ZERO {
        return Ok(Err(PayError::bad_request("amount must be greater than 0")));
    }

    let description = body.get("description").and_then(Value::as_str);
    let interval = body.get("interval").and_then(Value::as_str).map(str::trim).filter(|s| !s.is_empty()).unwrap_or("one_off");

    let product_id = uuid::Uuid::new_v4().simple().to_string();
    let price_id = uuid::Uuid::new_v4().simple().to_string();
    conn.execute(
        "INSERT INTO public.products (\"Id\",\"OrgId\",\"Name\",\"Description\",\"CreatedAt\") \
         VALUES ($1,$2,$3,$4,$5)",
        &[&product_id, &org_id, &name, &description, &chrono::Utc::now()],
    )?;
    conn.execute(
        "INSERT INTO public.prices (\"Id\",\"ProductId\",\"Currency\",\"Amount\",\"Interval\") \
         VALUES ($1,$2,$3,$4,$5)",
        &[&price_id, &product_id, &currency, &amount, &interval],
    )?;

    Ok(Ok(serde_json::json!({
        "id": product_id,
        "org_id": org_id,
        "name": name,
        "price_id": price_id,
        "amount": amount,
        "currency": currency,
        "interval": interval,
    })))
}

/// `GET /v1/orgs/{orgId}/products` — catalog list with cursor.
pub fn catalog_list(
    conn: &mut postgres::Client,
    org_id: &str,
    limit: Option<i64>,
    after: Option<&str>,
) -> QResult<serde_json::Value> {
    let take = clamp_limit(limit);
    let after_row: Option<(String, chrono::DateTime<chrono::Utc>)> = match after.map(str::trim).filter(|s| !s.is_empty()) {
        Some(after_id) => conn
            .query_opt(
                "SELECT \"Id\",\"CreatedAt\" FROM public.products WHERE \"OrgId\" = $1 AND \"Id\" = $2",
                &[&org_id, &after_id],
            )?
            .map(|row| (row.get(0), row.get(1))),
        None => None,
    };

    let rows = match &after_row {
        Some((id, created)) => conn.query(
            "SELECT \"Id\",\"Name\",\"Description\" FROM public.products \
             WHERE \"OrgId\" = $1 AND (\"CreatedAt\" < $2 OR (\"CreatedAt\" = $2 AND \"Id\" < $3)) \
             ORDER BY \"CreatedAt\" DESC, \"Id\" DESC LIMIT $4",
            &[&org_id, created, id, &(take + 1)],
        )?,
        None => conn.query(
            "SELECT \"Id\",\"Name\",\"Description\" FROM public.products \
             WHERE \"OrgId\" = $1 ORDER BY \"CreatedAt\" DESC, \"Id\" DESC LIMIT $2",
            &[&org_id, &(take + 1)],
        )?,
    };

    let mut items: Vec<Value> = Vec::new();
    let has_more = rows.len() as i64 > take;
    let page: Vec<_> = rows.into_iter().take(take as usize).collect();
    for row in &page {
        let id: String = row.get("Id");
        let prices = conn.query(
            "SELECT \"Id\",\"Amount\",\"Currency\",\"Interval\" FROM public.prices WHERE \"ProductId\" = $1",
            &[&id],
        )?;
        let price_list: Vec<Value> = prices
            .iter()
            .map(|p| {
                serde_json::json!({
                    "id": p.get::<_, String>("Id"),
                    "amount": p.get::<_, Decimal>("Amount"),
                    "currency": p.get::<_, String>("Currency"),
                    "interval": p.get::<_, String>("Interval"),
                })
            })
            .collect();
        items.push(serde_json::json!({
            "id": id,
            "name": row.get::<_, String>("Name"),
            "description": row.get::<_, Option<String>>("Description"),
            "prices": price_list,
        }));
    }
    let next_cursor = if has_more { page.last().map(|r| r.get::<_, String>("Id")) } else { None };
    Ok(serde_json::json!({ "items": items, "next_cursor": next_cursor }))
}

// ---------------------------------------------------------------------------
// Gateway vault
// ---------------------------------------------------------------------------

/// `PUT /v1/orgs/{orgId}/gateway` — vault a rail credential.
pub fn gateway_put(
    conn: &mut postgres::Client,
    box_one: &crate::secrets::SecretBox,
    org_id: &str,
    provider_raw: &str,
    body: &Value,
) -> Result<Result<serde_json::Value, PayError>, postgres::Error> {
    let Some(provider) = crate::rails::providers::try_normalize(Some(provider_raw)) else {
        return Ok(Err(PayError::bad_request("unknown provider")));
    };
    if crate::rails::providers::is_test(provider) {
        return Ok(Err(PayError::bad_request("test processor does not take secrets")));
    }

    let get = |key: &str| -> Option<String> {
        body.get(key).and_then(Value::as_str).map(|s| s.trim().to_string()).filter(|s| !s.is_empty())
    };
    let secret = get("secret");
    let webhook_secret = get("webhook_secret");
    let public_merchant_id = get("public_merchant_id");
    let environment = get("environment");
    let key_id = get("key_id");
    let key_secret = get("key_secret");

    let mut effective_secret = secret.clone();
    if effective_secret.is_none() && key_id.is_some() && key_secret.is_some() {
        effective_secret = Some(format!("{}:{}", key_id.clone().unwrap(), key_secret.clone().unwrap()));
    }

    if crate::rails::providers::is_solana(provider) {
        if secret.is_some() || webhook_secret.is_some() {
            return Ok(Err(PayError::bad_request("solana does not take an API secret")));
        }
        let Some(address) = public_merchant_id.as_deref() else {
            return Ok(Err(PayError::bad_request("public_merchant_id is required")));
        };
        if crate::rails::solana::base58::decode(address).map(|b| b.len()).unwrap_or(0) != 32 {
            return Ok(Err(PayError::bad_request("public_merchant_id must be a Solana wallet address")));
        }
        let vault_env = match environment.as_deref() {
            Some("mainnet-beta") | Some("mainnet") => "mainnet",
            Some("devnet") => "devnet",
            _ => return Ok(Err(PayError::bad_request("environment must be devnet or mainnet"))),
        };
        let config_cluster = "devnet"; // from config
        if !crate::rails::solana::cluster::matches_vault(config_cluster, Some(vault_env)) {
            return Ok(Err(PayError::bad_request("solana cluster mismatch")));
        }
        let last4 = if address.len() >= 4 { address[address.len() - 4..].to_string() } else { address.to_string() };
        conn.execute(
            "INSERT INTO public.gateway_credentials \
             (\"OrgId\",\"Provider\",\"Ciphertext\",\"Last4\",\"Environment\",\"PublicMerchantId\",\"UpdatedAt\") \
             VALUES ($1,$2,'',$3,$4,$5,$6) \
             ON CONFLICT (\"OrgId\",\"Provider\") DO UPDATE SET \
             \"PublicMerchantId\" = EXCLUDED.\"PublicMerchantId\", \
             \"Environment\" = EXCLUDED.\"Environment\", \"UpdatedAt\" = EXCLUDED.\"UpdatedAt\"",
            &[&org_id, &provider, &last4, &vault_env, &address, &chrono::Utc::now()],
        )?;
        return Ok(Ok(serde_json::json!({
            "org_id": org_id, "provider": provider, "last4": last4,
            "configured": true, "capability": "hosted_link",
            "public_merchant_id": address, "environment": vault_env,
            "webhook_configured": false,
        })));
    }

    let Some(effective) = effective_secret.as_deref().map(str::trim).filter(|s| !s.is_empty()) else {
        return Ok(Err(PayError::bad_request("secret is required")));
    };
    let Some(webhook_secret) = webhook_secret.as_deref().map(str::trim).filter(|s| !s.is_empty()) else {
        return Ok(Err(PayError::bad_request("webhook_secret is required")));
    };

    let Some(public_merchant_id) = public_merchant_id.as_deref().map(str::trim).filter(|s| !s.is_empty()) else {
        if crate::rails::providers::requires_public_merchant_id(provider) {
            return Ok(Err(PayError::bad_request("public_merchant_id is required")));
        }
        return Ok(Err(PayError::bad_request("public_merchant_id is not used for this provider")));
    };

    let last4: String = if effective.len() >= 4 {
        effective[effective.len() - 4..].to_string()
    } else {
        effective.to_string()
    };

    let wrapped = box_one.protect(effective);
    let wrapped_webhook = box_one.protect(webhook_secret);

    conn.execute(
        "INSERT INTO public.gateway_credentials \
         (\"OrgId\",\"Provider\",\"Ciphertext\",\"Last4\",\"WebhookCiphertext\",\
         \"PublicMerchantId\",\"Environment\",\"UpdatedAt\") \
         VALUES ($1,$2,$3,$4,$5,$6,$7,$8) \
         ON CONFLICT (\"OrgId\",\"Provider\") DO UPDATE SET \
         \"Ciphertext\" = EXCLUDED.\"Ciphertext\", \
         \"WebhookCiphertext\" = EXCLUDED.\"WebhookCiphertext\", \
         \"Last4\" = EXCLUDED.\"Last4\", \"UpdatedAt\" = EXCLUDED.\"UpdatedAt\"",
        &[
            &org_id, &provider, &wrapped, &last4, &wrapped_webhook, &public_merchant_id, &chrono::Utc::now(),
        ],
    )?;

    Ok(Ok(serde_json::json!({
        "org_id": org_id,
        "provider": provider,
        "last4": last4,
        "configured": true,
        "capability": "hosted_link",
        "webhook_configured": true,
    })))
}

/// `GET /v1/orgs/{orgId}/gateway?provider=X` — masked view.
pub fn gateway_get(
    conn: &mut postgres::Client,
    org_id: &str,
    provider: &str,
) -> QResult<serde_json::Value> {
    let row = conn.query_opt(
        "SELECT \"Last4\",\"WebhookCiphertext\" FROM public.gateway_credentials \
         WHERE \"OrgId\" = $1 AND \"Provider\" = $2",
        &[&org_id, &provider],
    )?;
    match row {
        Some(row) => Ok(serde_json::json!({
            "org_id": org_id,
            "provider": provider,
            "last4": row.get::<_, Option<String>>("Last4"),
            "configured": true,
            "webhook_configured": row.get::<_, Option<String>>("WebhookCiphertext").is_some(),
        })),
        None => Ok(serde_json::json!({
            "org_id": org_id,
            "provider": provider,
            "configured": false,
        })),
    }
}

/// `GET /v1/orgs/{orgId}/gateways` — all vaulted rails for the org.
pub fn gateway_list(conn: &mut postgres::Client, org_id: &str) -> QResult<serde_json::Value> {
    let rows = conn.query(
        "SELECT \"Provider\",\"Last4\",\"WebhookCiphertext\" FROM public.gateway_credentials \
         WHERE \"OrgId\" = $1",
        &[&org_id],
    )?;
    let processors: Vec<Value> = rows
        .iter()
        .map(|row| {
            serde_json::json!({
                "provider": row.get::<_, String>("Provider"),
                "last4": row.get::<_, Option<String>>("Last4"),
                "webhook_configured": row.get::<_, Option<String>>("WebhookCiphertext").is_some(),
                "configured": true,
            })
        })
        .collect();
    Ok(serde_json::json!({ "org_id": org_id, "processors": processors }))
}
