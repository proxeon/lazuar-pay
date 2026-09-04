//! Port of `Webhooks/Outbound/OrgWebhookEndpoints.cs`.

use postgres::Client;
use rand::RngCore;
use serde_json::{json, Value};
use uuid::Uuid;

use crate::hosting::PayError;
use crate::secrets::SecretBox;
use crate::webhooks::envelope;
use crate::webhooks::outbound_url;

fn mint_secret() -> String {
    let mut bytes = [0u8; 16];
    rand::thread_rng().fill_bytes(&mut bytes);
    format!("whsec_{}", hex::encode(bytes))
}

pub fn validate_url(raw: Option<&str>, environment: &str) -> Result<String, PayError> {
    let Some(url) = raw.map(str::trim).filter(|s| !s.is_empty()) else {
        return Err(PayError::bad_request("url is not allowed"));
    };
    let parsed = url::Url::parse(url).map_err(|_| PayError::bad_request("url is not allowed"))?;
    if parsed.scheme() != "https" && !(outbound_url::allows_loopback(environment) && parsed.scheme() == "http")
    {
        return Err(PayError::bad_request("url is not allowed"));
    }
    let host = parsed.host_str().unwrap_or("");
    let port = parsed.port_or_known_default().unwrap_or(443);
    if host.parse::<std::net::IpAddr>().ok().is_some_and(|ip| outbound_url::is_disallowed(ip, environment)) {
        return Err(PayError::bad_request("url is not allowed"));
    }
    if host.parse::<std::net::IpAddr>().is_err() {
        let allowed = outbound_url::resolve_allowed(host, port, environment);
        if allowed.is_empty() && !outbound_url::allows_loopback(environment) {
            return Err(PayError::bad_request("url is not allowed"));
        }
    }
    Ok(url.to_string())
}

pub fn put(
    conn: &mut Client,
    box_one: &SecretBox,
    org_id: &str,
    url: &str,
) -> Result<Result<Value, PayError>, postgres::Error> {
    let secret = mint_secret();
    let wrapped = box_one.protect(&secret);
    let prefix = secret[secret.len().saturating_sub(4)..].to_string();
    let exists = conn
        .query_opt(
            "SELECT 1 FROM public.org_webhook_endpoints WHERE \"OrgId\" = $1",
            &[&org_id],
        )?
        .is_some();
    if exists {
        conn.execute(
            "UPDATE public.org_webhook_endpoints SET \"Url\" = $2, \"SecretCiphertext\" = $3, \
             \"SecretPrefix\" = $4, \"UpdatedAt\" = $5 WHERE \"OrgId\" = $1",
            &[&org_id, &url, &wrapped, &prefix, &chrono::Utc::now()],
        )?;
    } else {
        conn.execute(
            "INSERT INTO public.org_webhook_endpoints \
             (\"OrgId\",\"Url\",\"SecretCiphertext\",\"SecretPrefix\",\"UpdatedAt\") \
             VALUES ($1,$2,$3,$4,$5)",
            &[&org_id, &url, &wrapped, &prefix, &chrono::Utc::now()],
        )?;
    }
    conn.execute(
        "INSERT INTO public.audit_events (\"Id\",\"OrgId\",\"Action\",\"At\") VALUES ($1,$2,$3,$4)",
        &[
            &Uuid::new_v4().simple().to_string(),
            &org_id,
            &"org.webhook.upsert",
            &chrono::Utc::now(),
        ],
    )?;
    Ok(Ok(json!({
        "org_id": org_id,
        "url": url,
        "webhook_configured": true,
        "secret_prefix": prefix,
        "webhook_secret": secret,
    })))
}

pub fn get(conn: &mut Client, org_id: &str) -> Result<Value, postgres::Error> {
    match conn.query_opt(
        "SELECT \"Url\",\"SecretPrefix\" FROM public.org_webhook_endpoints WHERE \"OrgId\" = $1",
        &[&org_id],
    )? {
        Some(row) => Ok(json!({
            "org_id": org_id,
            "url": row.get::<_, String>("Url"),
            "webhook_configured": true,
            "secret_prefix": row.get::<_, Option<String>>("SecretPrefix"),
        })),
        None => Ok(json!({
            "org_id": org_id,
            "url": Value::Null,
            "webhook_configured": false,
            "secret_prefix": Value::Null,
        })),
    }
}

pub fn rotate(
    conn: &mut Client,
    box_one: &SecretBox,
    org_id: &str,
) -> Result<Result<Value, PayError>, postgres::Error> {
    let Some(row) = conn.query_opt(
        "SELECT \"Url\" FROM public.org_webhook_endpoints WHERE \"OrgId\" = $1",
        &[&org_id],
    )?
    else {
        return Ok(Err(PayError::not_found("webhook endpoint not found")));
    };
    let url: String = row.get(0);
    let secret = mint_secret();
    let wrapped = box_one.protect(&secret);
    let prefix = secret[secret.len().saturating_sub(4)..].to_string();
    conn.execute(
        "UPDATE public.org_webhook_endpoints SET \"SecretCiphertext\" = $2, \"SecretPrefix\" = $3, \
         \"UpdatedAt\" = $4 WHERE \"OrgId\" = $1",
        &[&org_id, &wrapped, &prefix, &chrono::Utc::now()],
    )?;
    Ok(Ok(json!({
        "org_id": org_id,
        "url": url,
        "webhook_configured": true,
        "secret_prefix": prefix,
        "webhook_secret": secret,
    })))
}

pub fn test_ping(conn: &mut Client, org_id: &str) -> Result<Result<Value, PayError>, postgres::Error> {
    if conn
        .query_opt(
            "SELECT 1 FROM public.org_webhook_endpoints WHERE \"OrgId\" = $1",
            &[&org_id],
        )?
        .is_none()
    {
        return Ok(Err(PayError::not_found("webhook endpoint not found")));
    }
    let event_id = format!("test-{}", Uuid::new_v4().simple());
    let payload = envelope::serialize("webhook.test", &event_id, org_id, json!({ "ok": true }));
    conn.execute(
        "INSERT INTO public.org_webhook_deliveries \
         (\"Id\",\"OrgId\",\"EventId\",\"EventType\",\"PayloadJson\",\"Status\",\
         \"AttemptCount\",\"NextAttemptAt\",\"CreatedAt\") \
         VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9)",
        &[
            &Uuid::new_v4().simple().to_string(),
            &org_id,
            &event_id,
            &"webhook.test",
            &payload,
            &"pending",
            &0i32,
            &chrono::Utc::now(),
            &chrono::Utc::now(),
        ],
    )?;
    Ok(Ok(json!({ "ok": true, "event_id": event_id })))
}
