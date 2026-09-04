//! Merchant HTTP surface — rewrite of the Phase-3 dump (027/3). Not the
//! previous unmounted stub: checkout create persists, vault SQL arity is
//! correct, catalog create is transactional.

use postgres::Client;
use rust_decimal::Decimal;
use serde_json::{json, Value};
use uuid::Uuid;

use crate::domain::checkout_store::{self, NewCheckout};
use crate::domain::currency;
use crate::hosting::{clamp_limit, PayError};
use crate::rails::providers;
use crate::rails::solana::address as solana_address;
use crate::rails::solana::cluster;
use crate::rails::solana::money as solana_money;
use crate::secrets::SecretBox;

fn two_guid_token() -> String {
    format!(
        "{}{}",
        hex::encode_upper(*Uuid::new_v4().as_bytes()),
        hex::encode_upper(*Uuid::new_v4().as_bytes())
    )
}

fn session_json(session: &checkout_store::CheckoutSession, checkout_base: &str) -> Value {
    let pay_url = format!("{checkout_base}/c/{}", session.public_token);
    json!({
        "id": session.id,
        "org_id": session.org_id,
        "provider": session.provider,
        "product_id": session.product_id,
        "payment_link_id": session.payment_link_id,
        "slot_key": session.slot_key,
        "public_token": session.public_token,
        "amount": crate::hosting::decimal_json(session.amount),
        "currency": session.currency,
        "status": session.status,
        "pay_url": pay_url,
        "interval": session.interval,
        "success_url": session.success_url,
        "cancel_url": session.cancel_url,
        "payer_name": session.payer_name,
        "payer_email": session.payer_email,
        "created_at": session.created_at,
    })
}

fn ensure_org_settings(conn: &mut Client, org_id: &str) -> Result<(), postgres::Error> {
    conn.execute(
        "INSERT INTO public.org_settings (\"OrgId\",\"Currency\",\"ChargesPaused\") VALUES ($1,'MYR', FALSE) \
         ON CONFLICT (\"OrgId\") DO NOTHING",
        &[&org_id],
    )?;
    Ok(())
}

pub fn checkout_create(
    conn: &mut Client,
    environment: &str,
    checkout_base: &str,
    org_id: &str,
    body: &Value,
    idempotency_key: Option<&str>,
) -> Result<Result<(u16, Value), PayError>, postgres::Error> {
    let org_id = org_id.trim();
    if org_id.is_empty() {
        return Ok(Err(PayError::bad_request("org_id is required")));
    }
    ensure_org_settings(conn, org_id)?;
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

    let amount = crate::app::parse_decimal(body.get("amount")).unwrap_or(Decimal::ZERO);
    if amount <= Decimal::ZERO {
        return Ok(Err(PayError::bad_request("amount must be greater than 0")));
    }
    let Some(provider) = providers::try_normalize(body.get("provider").and_then(Value::as_str)) else {
        return Ok(Err(PayError::bad_request("unknown provider")));
    };
    if providers::is_test(provider) {
        if !providers::allows_test(environment) {
            return Ok(Err(PayError::bad_request("test processor is not enabled")));
        }
    } else {
        let has = conn
            .query_opt(
                "SELECT 1 FROM public.gateway_credentials WHERE \"OrgId\" = $1 AND \"Provider\" = $2",
                &[&org_id, &provider],
            )?
            .is_some();
        if !has {
            return Ok(Err(PayError::bad_request("rail not configured")));
        }
    }

    let interval = body
        .get("interval")
        .and_then(Value::as_str)
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .unwrap_or("one_off");
    if !matches!(interval, "one_off" | "mo" | "yr") {
        return Ok(Err(PayError::bad_request("interval must be one_off, mo, or yr")));
    }

    let product_id = body.get("product_id").and_then(Value::as_str).map(str::trim).filter(|s| !s.is_empty());
    if let Some(err) = solana_money::mint_error(provider, body.get("currency").and_then(Value::as_str), Some(interval), product_id, Some(amount)) {
        return Ok(Err(PayError::bad_request(err)));
    }

    let currency = body
        .get("currency")
        .and_then(Value::as_str)
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .map(|c| c.to_uppercase())
        .unwrap_or_else(|| "MYR".to_string());
    if !currency::is_supported(provider, &currency) {
        return Ok(Err(PayError::bad_request(format!(
            "currency {currency} is not supported on {provider}; supported: {}",
            currency::describe(provider)
        ))));
    }

    let id = Uuid::new_v4();
    let new = NewCheckout {
        id,
        org_id: org_id.to_string(),
        provider: Some(provider.to_string()),
        product_id: product_id.map(str::to_string),
        payment_link_id: None,
        slot_key: None,
        amount,
        currency,
        status: "open".into(),
        interval: Some(interval.to_string()),
        success_url: body.get("success_url").and_then(Value::as_str).map(str::to_string),
        cancel_url: body.get("cancel_url").and_then(Value::as_str).map(str::to_string),
        public_token: Some(two_guid_token()),
    };
    let minted_id = id.to_string();
    let session = match checkout_store::create(conn, &new, idempotency_key) {
        Ok(s) => s,
        Err(checkout_store::CreateError::Conflict) => {
            return Ok(Err(PayError::conflict("idempotency key reused with a different body")));
        }
        Err(checkout_store::CreateError::Db(e)) => return Err(e),
    };
    let created = session.id == minted_id;
    if created && (interval == "mo" || interval == "yr") {
        conn.execute(
            "INSERT INTO public.subscriptions \
             (\"Id\",\"OrgId\",\"CheckoutId\",\"Status\",\"Interval\",\"CreatedAt\") \
             VALUES ($1,$2,$3,'incomplete',$4,$5)",
            &[
                &Uuid::new_v4().simple().to_string(),
                &org_id,
                &session.id,
                &interval,
                &chrono::Utc::now(),
            ],
        )?;
    }
    let status = if created { 201 } else { 200 };
    Ok(Ok((status, session_json(&session, checkout_base))))
}

pub fn checkout_get(
    conn: &mut Client,
    checkout_base: &str,
    id: &str,
) -> Result<Option<checkout_store::CheckoutSession>, postgres::Error> {
    let _ = checkout_base;
    checkout_store::get(conn, id)
}

pub fn checkout_view(session: &checkout_store::CheckoutSession, checkout_base: &str) -> Value {
    session_json(session, checkout_base)
}

pub fn checkout_list(
    conn: &mut Client,
    checkout_base: &str,
    org_id: &str,
    limit: Option<i64>,
    after: Option<&str>,
) -> Result<Value, postgres::Error> {
    let take = clamp_limit(limit);
    let after_row: Option<(String, chrono::DateTime<chrono::Utc>)> = match after.map(str::trim).filter(|s| !s.is_empty()) {
        Some(after_id) => conn
            .query_opt(
                "SELECT \"Id\",\"CreatedAt\" FROM public.checkouts WHERE \"OrgId\" = $1 AND \"Id\" = $2",
                &[&org_id, &after_id],
            )?
            .map(|row| (row.get(0), row.get(1))),
        None => None,
    };
    let rows = match &after_row {
        Some((id, created)) => conn.query(
            "SELECT \"Id\" FROM public.checkouts \
             WHERE \"OrgId\" = $1 AND \"PaymentLinkId\" IS NULL \
             AND (\"CreatedAt\" < $2 OR (\"CreatedAt\" = $2 AND \"Id\" < $3)) \
             ORDER BY \"CreatedAt\" DESC, \"Id\" DESC LIMIT $4",
            &[&org_id, created, id, &(take + 1)],
        )?,
        None => conn.query(
            "SELECT \"Id\" FROM public.checkouts \
             WHERE \"OrgId\" = $1 AND \"PaymentLinkId\" IS NULL \
             ORDER BY \"CreatedAt\" DESC, \"Id\" DESC LIMIT $2",
            &[&org_id, &(take + 1)],
        )?,
    };
    let has_more = rows.len() as i64 > take;
    let page: Vec<_> = rows.into_iter().take(take as usize).collect();
    let mut items = Vec::new();
    for row in &page {
        let id: String = row.get(0);
        if let Some(session) = checkout_store::get(conn, &id)? {
            items.push(session_json(&session, checkout_base));
        }
    }
    let next_cursor = if has_more {
        page.last().map(|r| r.get::<_, String>(0))
    } else {
        None
    };
    Ok(json!({ "items": items, "next_cursor": next_cursor }))
}

pub fn catalog_create(
    conn: &mut Client,
    org_id: &str,
    body: &Value,
) -> Result<Result<(u16, Value), PayError>, postgres::Error> {
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
        return Ok(Err(PayError::bad_request("Bar B currency is MYR")));
    }
    let amount = crate::app::parse_decimal(body.get("amount")).unwrap_or(Decimal::ZERO);
    if amount <= Decimal::ZERO {
        return Ok(Err(PayError::bad_request("amount must be greater than 0")));
    }
    let interval = body
        .get("interval")
        .and_then(Value::as_str)
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .unwrap_or("one_off");
    let description = body.get("description").and_then(Value::as_str);
    let product_id = Uuid::new_v4().simple().to_string();
    let price_id = Uuid::new_v4().simple().to_string();
    let mut tx = conn.transaction()?;
    tx.execute(
        "INSERT INTO public.products (\"Id\",\"OrgId\",\"Name\",\"Description\",\"CreatedAt\") \
         VALUES ($1,$2,$3,$4,$5)",
        &[&product_id, &org_id, &name, &description, &chrono::Utc::now()],
    )?;
    tx.execute(
        "INSERT INTO public.prices (\"Id\",\"ProductId\",\"Currency\",\"Amount\",\"Interval\") \
         VALUES ($1,$2,$3,$4,$5)",
        &[&price_id, &product_id, &currency, &amount, &interval],
    )?;
    tx.commit()?;
    Ok(Ok((
        201,
        json!({
            "id": product_id,
            "org_id": org_id,
            "name": name,
            "price_id": price_id,
            "amount": crate::hosting::decimal_json(amount),
            "currency": currency,
            "interval": interval,
        }),
    )))
}

pub fn catalog_list(
    conn: &mut Client,
    org_id: &str,
    limit: Option<i64>,
    after: Option<&str>,
) -> Result<Value, postgres::Error> {
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
            "SELECT \"Id\",\"OrgId\",\"Name\" FROM public.products \
             WHERE \"OrgId\" = $1 AND (\"CreatedAt\" < $2 OR (\"CreatedAt\" = $2 AND \"Id\" < $3)) \
             ORDER BY \"CreatedAt\" DESC, \"Id\" DESC LIMIT $4",
            &[&org_id, created, id, &(take + 1)],
        )?,
        None => conn.query(
            "SELECT \"Id\",\"OrgId\",\"Name\" FROM public.products \
             WHERE \"OrgId\" = $1 ORDER BY \"CreatedAt\" DESC, \"Id\" DESC LIMIT $2",
            &[&org_id, &(take + 1)],
        )?,
    };
    let has_more = rows.len() as i64 > take;
    let page: Vec<_> = rows.into_iter().take(take as usize).collect();
    let mut items = Vec::new();
    for row in &page {
        let id: String = row.get("Id");
        let prices = conn.query(
            "SELECT \"Id\",\"Amount\",\"Currency\",\"Interval\" FROM public.prices WHERE \"ProductId\" = $1",
            &[&id],
        )?;
        let price_list: Vec<Value> = prices
            .iter()
            .map(|p| {
                json!({
                    "id": p.get::<_, String>("Id"),
                    "amount": crate::hosting::decimal_json(p.get::<_, Decimal>("Amount")),
                    "currency": p.get::<_, String>("Currency"),
                    "interval": p.get::<_, String>("Interval"),
                })
            })
            .collect();
        items.push(json!({
            "id": id,
            "org_id": row.get::<_, String>("OrgId"),
            "name": row.get::<_, String>("Name"),
            "prices": price_list,
        }));
    }
    let next_cursor = if has_more {
        page.last().map(|r| r.get::<_, String>("Id"))
    } else {
        None
    };
    Ok(json!({ "items": items, "next_cursor": next_cursor }))
}

fn chip_pem_ok(pem: &str) -> bool {
    use rsa::pkcs8::DecodePublicKey;
    use rsa::traits::PublicKeyParts;
    rsa::RsaPublicKey::from_public_key_pem(pem)
        .ok()
        .is_some_and(|k| k.n().bits() >= 2048)
}

pub fn gateway_put(
    conn: &mut Client,
    box_one: &SecretBox,
    config_cluster: &str,
    org_id: &str,
    body: &Value,
) -> Result<Result<Value, PayError>, postgres::Error> {
    let Some(provider) = providers::try_normalize(body.get("provider").and_then(Value::as_str)) else {
        return Ok(Err(PayError::bad_request("unknown provider")));
    };
    if providers::is_test(provider) {
        return Ok(Err(PayError::bad_request("test processor does not take secrets")));
    }
    let get = |key: &str| -> Option<String> {
        body.get(key)
            .and_then(Value::as_str)
            .map(|s| s.trim().to_string())
            .filter(|s| !s.is_empty())
    };

    let (wrapped, wrapped_wh, last4, public_id, environment) = if providers::uses_receive_address(provider) {
        if get("secret").is_some() {
            return Ok(Err(PayError::bad_request("solana does not take an API secret")));
        }
        if get("webhook_secret").is_some() {
            return Ok(Err(PayError::bad_request("solana does not take a webhook secret")));
        }
        let Some(address) = get("public_merchant_id").and_then(|a| solana_address::try_normalize(&a)) else {
            return Ok(Err(PayError::bad_request("public_merchant_id must be a Solana wallet address")));
        };
        let env = match get("environment").as_deref() {
            Some("mainnet-beta") | Some("mainnet") => "mainnet",
            Some("devnet") => "devnet",
            _ => return Ok(Err(PayError::bad_request("environment must be devnet or mainnet"))),
        };
        if !cluster::matches_vault(cluster::from_config(Some(config_cluster)), Some(env)) {
            return Ok(Err(PayError::bad_request("solana cluster mismatch")));
        }
        ("".into(), None, solana_address::last4(&address), Some(address), Some(env.to_string()))
    } else {
        let mut secret = get("secret");
        if secret.is_none() && get("key_id").is_some() && get("key_secret").is_some() {
            secret = Some(format!("{}:{}", get("key_id").unwrap(), get("key_secret").unwrap()));
        }
        let Some(secret) = secret else {
            return Ok(Err(PayError::bad_request("secret is required")));
        };
        let Some(webhook_secret) = get("webhook_secret") else {
            return Ok(Err(PayError::bad_request("webhook_secret is required")));
        };
        let public_id = get("public_merchant_id");
        if providers::requires_public_merchant_id(provider) && public_id.is_none() {
            return Ok(Err(PayError::bad_request("public_merchant_id is required")));
        }
        if !providers::allows_public_merchant_id(provider) && public_id.is_some() {
            return Ok(Err(PayError::bad_request("public_merchant_id is not used for this provider")));
        }
        if provider == providers::BILLPLZ && get("environment").is_none() {
            return Ok(Err(PayError::bad_request("environment is required")));
        }
        let environment = get("environment").map(|e| e.to_lowercase());
        if let Some(env) = environment.as_deref() {
            if env != "test" && env != "live" {
                return Ok(Err(PayError::bad_request("environment must be test or live")));
            }
        }
        if provider == providers::RAZORPAY && !secret.contains(':') {
            return Ok(Err(PayError::bad_request("secret must be key_id:key_secret")));
        }
        if provider == providers::CHIP && !chip_pem_ok(&webhook_secret) {
            return Ok(Err(PayError::bad_request("webhook_secret must be a CHIP PEM")));
        }
        let mut last4 = if secret.len() >= 4 { secret[secret.len() - 4..].to_string() } else { secret.clone() };
        if provider == providers::RAZORPAY {
            if let Some((key_id, _)) = secret.split_once(':') {
                last4 = if key_id.len() >= 4 { key_id[key_id.len() - 4..].to_string() } else { key_id.to_string() };
            }
        }
        (box_one.protect(&secret), Some(box_one.protect(&webhook_secret)), last4, public_id, environment)
    };

    ensure_org_settings(conn, org_id)?;
    // Insert: omitted environment defaults to 'test' (C# `environment ?? "test"`).
    // Update: omitted environment keeps the existing live/test value (C# assigns
    // only when the body sent environment).
    conn.execute(
        "INSERT INTO public.gateway_credentials \
         (\"OrgId\",\"Provider\",\"Ciphertext\",\"Last4\",\"WebhookCiphertext\",\
         \"PublicMerchantId\",\"Environment\",\"UpdatedAt\") \
         VALUES ($1,$2,$3,$4,$5,$6,COALESCE($7, 'test'),$8) \
         ON CONFLICT (\"OrgId\",\"Provider\") DO UPDATE SET \
         \"Ciphertext\" = EXCLUDED.\"Ciphertext\", \
         \"WebhookCiphertext\" = EXCLUDED.\"WebhookCiphertext\", \
         \"Last4\" = EXCLUDED.\"Last4\", \
         \"PublicMerchantId\" = EXCLUDED.\"PublicMerchantId\", \
         \"Environment\" = COALESCE($7, public.gateway_credentials.\"Environment\"), \
         \"UpdatedAt\" = EXCLUDED.\"UpdatedAt\"",
        &[
            &org_id,
            &provider,
            &wrapped,
            &last4,
            &wrapped_wh,
            &public_id,
            &environment,
            &chrono::Utc::now(),
        ],
    )?;
    conn.execute(
        "INSERT INTO public.audit_events (\"Id\",\"OrgId\",\"Action\",\"At\") VALUES ($1,$2,$3,$4)",
        &[
            &Uuid::new_v4().simple().to_string(),
            &org_id,
            &"gateway.credentials.upsert",
            &chrono::Utc::now(),
        ],
    )?;
    Ok(Ok(json!({
        "org_id": org_id,
        "provider": provider,
        "last4": last4,
        "configured": true,
        "capability": providers::CAPABILITY,
        "public_merchant_id": public_id,
        "environment": environment,
        "webhook_configured": wrapped_wh.is_some(),
    })))
}

fn gateway_object(
    org_id: &str,
    provider: &str,
    last4: Option<&str>,
    webhook: bool,
    public_id: Option<&str>,
    environment: Option<&str>,
    configured: bool,
) -> Value {
    json!({
        "org_id": org_id,
        "provider": provider,
        "last4": last4,
        "configured": configured,
        "capability": providers::CAPABILITY,
        "public_merchant_id": public_id,
        "environment": environment,
        "webhook_configured": webhook,
    })
}

pub fn gateway_get(
    conn: &mut Client,
    environment: &str,
    org_id: &str,
    provider_raw: &str,
) -> Result<Result<Value, PayError>, postgres::Error> {
    if provider_raw.trim().is_empty() {
        return Ok(Err(PayError::bad_request("provider is required")));
    }
    let Some(provider) = providers::try_normalize(Some(provider_raw)) else {
        return Ok(Err(PayError::bad_request("unknown provider")));
    };
    if providers::is_test(provider) && providers::allows_test(environment) {
        return Ok(Ok(gateway_object(
            org_id,
            provider,
            None,
            true,
            None,
            Some("test"),
            true,
        )));
    }
    match conn.query_opt(
        "SELECT \"Last4\",\"WebhookCiphertext\",\"PublicMerchantId\",\"Environment\" \
         FROM public.gateway_credentials WHERE \"OrgId\" = $1 AND \"Provider\" = $2",
        &[&org_id, &provider],
    )? {
        Some(row) => {
            let last4: Option<String> = row.get("Last4");
            let wh: Option<String> = row.get("WebhookCiphertext");
            let public_id: Option<String> = row.get("PublicMerchantId");
            let env: Option<String> = row.get("Environment");
            Ok(Ok(gateway_object(
                org_id,
                provider,
                last4.as_deref(),
                wh.as_deref().is_some_and(|s| !s.is_empty()),
                public_id.as_deref(),
                env.as_deref(),
                true,
            )))
        }
        None => Ok(Ok(json!({
            "org_id": org_id,
            "provider": provider,
            "configured": false,
        }))),
    }
}

pub fn gateway_list(conn: &mut Client, environment: &str, org_id: &str) -> Result<Value, postgres::Error> {
    let mut processors = Vec::new();
    for provider in providers::listed(environment) {
        if providers::is_test(provider) {
            processors.push(gateway_object(
                org_id,
                provider,
                None,
                true,
                None,
                Some("test"),
                true,
            ));
            continue;
        }
        match conn.query_opt(
            "SELECT \"Last4\",\"WebhookCiphertext\",\"PublicMerchantId\",\"Environment\" \
             FROM public.gateway_credentials WHERE \"OrgId\" = $1 AND \"Provider\" = $2",
            &[&org_id, &provider],
        )? {
            Some(row) => {
                let last4: Option<String> = row.get("Last4");
                let wh: Option<String> = row.get("WebhookCiphertext");
                let public_id: Option<String> = row.get("PublicMerchantId");
                let env: Option<String> = row.get("Environment");
                processors.push(gateway_object(
                    org_id,
                    provider,
                    last4.as_deref(),
                    wh.as_deref().is_some_and(|s| !s.is_empty()),
                    public_id.as_deref(),
                    env.as_deref(),
                    true,
                ));
            }
            None => processors.push(gateway_object(org_id, provider, None, false, None, None, false)),
        }
    }
    Ok(json!({ "org_id": org_id, "processors": processors }))
}
