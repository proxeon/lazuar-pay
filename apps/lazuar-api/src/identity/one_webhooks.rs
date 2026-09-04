//! Port of `Identity/OneWebhooks/OneWebhookEndpoints.cs`.

use postgres::Client;
use serde_json::{json, Value};
use uuid::Uuid;

use crate::hosting::PayError;
use crate::identity::one_webhook_signature;
use crate::identity::whoami_cache::OneWhoamiCache;
use crate::secrets::SecretBox;

fn read_org_id(root: &Value) -> Option<String> {
    if let Some(s) = root.get("org_id").and_then(Value::as_str).map(str::trim).filter(|s| !s.is_empty()) {
        return Some(s.to_string());
    }
    if let Some(s) = root
        .get("tenant_id")
        .and_then(Value::as_str)
        .map(str::trim)
        .filter(|s| !s.is_empty())
    {
        return Some(s.to_string());
    }
    root.get("data").and_then(read_org_id)
}

fn read_key_id(root: &Value) -> Option<String> {
    if let Some(s) = root.get("key_id").and_then(Value::as_str).map(str::trim).filter(|s| !s.is_empty()) {
        return Some(s.to_string());
    }
    root.get("data").and_then(read_key_id)
}

fn resolve_secret(
    conn: &mut Client,
    box_one: &SecretBox,
    process_secret: &str,
    raw_body: &str,
) -> Option<String> {
    let parsed: Value = serde_json::from_str(raw_body).unwrap_or(Value::Null);
    if let Some(org_id) = read_org_id(&parsed) {
        if let Some(row) = conn
            .query_opt(
                "SELECT \"OneWebhookCiphertext\" FROM public.org_settings WHERE \"OrgId\" = $1",
                &[&org_id],
            )
            .ok()
            .flatten()
        {
            let ct: Option<String> = row.get(0);
            if let Some(ct) = ct.map(|s| s.trim().to_string()).filter(|s| !s.is_empty()) {
                if let Ok(stored) = box_one.unprotect(&ct) {
                    let stored = stored.trim().to_string();
                    if !stored.is_empty() {
                        return Some(stored);
                    }
                }
            }
        }
    }
    let process = process_secret.trim();
    (!process.is_empty()).then(|| process.to_string())
}

pub fn handle(
    conn: &mut Client,
    box_one: &SecretBox,
    cache: &OneWhoamiCache,
    process_secret: &str,
    raw_body: &str,
    signature: Option<&str>,
    timestamp: Option<&str>,
    event_id_header: Option<&str>,
) -> Result<Result<Value, PayError>, postgres::Error> {
    let Some(secret) = resolve_secret(conn, box_one, process_secret, raw_body) else {
        return Ok(Err(PayError::unavailable("One webhook secret missing")));
    };
    if !one_webhook_signature::try_verify(&secret, raw_body, signature, timestamp, 300, None) {
        return Ok(Err(PayError::unauthorized("Invalid HMAC")));
    }
    if raw_body.trim().is_empty() {
        return Ok(Err(PayError::bad_request("invalid event")));
    }
    let parsed: Value = match serde_json::from_str(raw_body) {
        Ok(v) => v,
        Err(_) => return Ok(Err(PayError::bad_request("invalid event"))),
    };

    let event_type = parsed
        .get("type")
        .and_then(Value::as_str)
        .unwrap_or("unknown")
        .to_string();
    let body_id = parsed.get("id").and_then(Value::as_str).map(str::trim).filter(|s| !s.is_empty());
    let header_id = event_id_header.map(str::trim).filter(|s| !s.is_empty());
    let Some(delivery) = body_id.map(str::to_string).or_else(|| header_id.map(str::to_string)) else {
        return Ok(Err(PayError::bad_request("event id required")));
    };

    let org_id = read_org_id(&parsed);
    let exists = conn
        .query_opt(
            "SELECT 1 FROM public.one_webhook_events WHERE \"DeliveryId\" = $1",
            &[&delivery],
        )?
        .is_some();
    if exists {
        return Ok(Ok(json!({ "duplicate": true })));
    }

    conn.execute(
        "INSERT INTO public.one_webhook_events (\"Id\",\"DeliveryId\",\"EventType\",\"ReceivedAt\") \
         VALUES ($1,$2,$3,$4)",
        &[
            &Uuid::new_v4().simple().to_string(),
            &delivery,
            &event_type,
            &chrono::Utc::now(),
        ],
    )?;

    if event_type == "tenant.suspended" {
        if let Some(org_id) = org_id.as_deref() {
            let existing = conn
                .query_opt(
                    "SELECT 1 FROM public.org_settings WHERE \"OrgId\" = $1",
                    &[&org_id],
                )?
                .is_some();
            if existing {
                conn.execute(
                    "UPDATE public.org_settings SET \"ChargesPaused\" = TRUE WHERE \"OrgId\" = $1",
                    &[&org_id],
                )?;
            } else {
                conn.execute(
                    "INSERT INTO public.org_settings (\"OrgId\",\"ChargesPaused\",\"Currency\") \
                     VALUES ($1, TRUE, 'MYR')",
                    &[&org_id],
                )?;
            }
        }
    }

    if event_type == "tenant.reactivated" {
        if let Some(org_id) = org_id.as_deref() {
            conn.execute(
                "UPDATE public.org_settings SET \"ChargesPaused\" = FALSE WHERE \"OrgId\" = $1",
                &[&org_id],
            )?;
        }
    }

    if event_type == "api_key.revoked" {
        if let Some(key_id) = read_key_id(&parsed) {
            cache.invalidate_key(&key_id);
        }
    }

    Ok(Ok(json!({ "ok": true })))
}

pub fn put_secret(
    conn: &mut Client,
    box_one: &SecretBox,
    org_id: &str,
    webhook_secret: Option<&str>,
) -> Result<Result<Value, PayError>, postgres::Error> {
    let Some(secret) = webhook_secret.map(str::trim).filter(|s| !s.is_empty()) else {
        return Ok(Err(PayError::bad_request("webhook_secret is required")));
    };
    let wrapped = box_one.protect(secret);
    let exists = conn
        .query_opt(
            "SELECT 1 FROM public.org_settings WHERE \"OrgId\" = $1",
            &[&org_id],
        )?
        .is_some();
    if exists {
        conn.execute(
            "UPDATE public.org_settings SET \"OneWebhookCiphertext\" = $2 WHERE \"OrgId\" = $1",
            &[&org_id, &wrapped],
        )?;
    } else {
        conn.execute(
            "INSERT INTO public.org_settings (\"OrgId\",\"OneWebhookCiphertext\",\"Currency\",\"ChargesPaused\") \
             VALUES ($1,$2,'MYR', FALSE)",
            &[&org_id, &wrapped],
        )?;
    }
    conn.execute(
        "INSERT INTO public.audit_events (\"Id\",\"OrgId\",\"Action\",\"At\") VALUES ($1,$2,$3,$4)",
        &[
            &Uuid::new_v4().simple().to_string(),
            &org_id,
            &"one.webhook_secret.upsert",
            &chrono::Utc::now(),
        ],
    )?;
    Ok(Ok(json!({ "org_id": org_id, "webhook_configured": true })))
}

pub fn get_secret(conn: &mut Client, org_id: &str) -> Result<Value, postgres::Error> {
    let configured = conn
        .query_opt(
            "SELECT \"OneWebhookCiphertext\" FROM public.org_settings WHERE \"OrgId\" = $1",
            &[&org_id],
        )?
        .and_then(|row| row.get::<_, Option<String>>(0))
        .map(|s| !s.trim().is_empty())
        .unwrap_or(false);
    Ok(json!({ "org_id": org_id, "webhook_configured": configured }))
}
