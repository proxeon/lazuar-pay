//! Claim deliveries, pin-dial, Plane C HMAC. Per-row persist (issue 005).

use std::net::{IpAddr, SocketAddr, ToSocketAddrs};
use std::time::Duration as StdDuration;

use reqwest::redirect::Policy;
use sqlx::PgPool;
use storage::ApplyError;
use time::{Duration, OffsetDateTime};
use uuid::Uuid;

use crate::hmac::sign_v1;
use crate::outbound_url::{host_is_loopback_name, is_disallowed};
use crate::secret_box::SecretBox;

pub struct OutboundCfg<'a> {
    pub pool: &'a PgPool,
    pub box_: SecretBox,
    pub allow_loopback: bool,
}

pub async fn process_batch(cfg: OutboundCfg<'_>) -> Result<usize, ApplyError> {
    let now = OffsetDateTime::now_utc();
    let claimed = storage::claim_deliveries(cfg.pool, now, Duration::seconds(60), 20).await?;
    let n = claimed.len();
    for row in claimed {
        if let Err(e) = deliver_one(cfg.pool, &cfg.box_, cfg.allow_loopback, &row).await {
            let attempts = row.attempt_count + 1;
            let backoff = backoff_secs(attempts);
            let _ = storage::mark_delivery(
                cfg.pool,
                row.id,
                "pending",
                attempts,
                OffsetDateTime::now_utc() + Duration::seconds(backoff),
                None,
                Some(&format!("dispatch:{e}")),
            )
            .await;
        }
    }
    Ok(n)
}

fn backoff_secs(attempt_count: i32) -> i64 {
    i64::from((15 * attempt_count).min(300))
}

async fn deliver_one(
    pool: &PgPool,
    box_: &SecretBox,
    allow_loopback: bool,
    row: &storage::DeliveryRow,
) -> Result<(), String> {
    let Some(ep) = storage::load_endpoint(pool, &row.tenant_id)
        .await
        .map_err(|e| e.to_string())?
    else {
        persist(
            pool,
            row.id,
            "poison",
            row.attempt_count + 1,
            None,
            Some("endpoint missing"),
        )
        .await;
        return Ok(());
    };
    let Ok(dest) = ep.url.parse::<reqwest::Url>() else {
        persist(
            pool,
            row.id,
            "poison",
            row.attempt_count + 1,
            None,
            Some("endpoint url invalid"),
        )
        .await;
        return Ok(());
    };
    if dest.scheme() != "http" && dest.scheme() != "https" {
        persist(
            pool,
            row.id,
            "poison",
            row.attempt_count + 1,
            None,
            Some("endpoint url invalid"),
        )
        .await;
        return Ok(());
    }
    let secret = match box_.unprotect_str(&ep.secret_ciphertext) {
        Ok(s) => s,
        Err(_) => return Err("SecretBox".into()),
    };

    let body = row.payload_json.clone();
    let unix = OffsetDateTime::now_utc().unix_timestamp();
    let sig = sign_v1(&secret, body.as_bytes(), unix);

    match pinned_post(
        &dest,
        allow_loopback,
        body.as_bytes(),
        &row.event_id,
        &row.event_type,
        &row.tenant_id,
        unix,
        &sig,
    )
    .await
    {
        Ok(code) => {
            let attempts = row.attempt_count + 1;
            if (200..300).contains(&code) {
                persist(pool, row.id, "succeeded", attempts, Some(code), None).await;
            } else if code == 401 || code == 403 || code == 410 {
                persist(pool, row.id, "poison", attempts, Some(code), None).await;
            } else {
                let next = OffsetDateTime::now_utc() + Duration::seconds(backoff_secs(attempts));
                let _ = storage::mark_delivery(
                    pool,
                    row.id,
                    "pending",
                    attempts,
                    next,
                    Some(code),
                    None,
                )
                .await;
            }
        }
        Err(e) if e == "private" => {
            persist(
                pool,
                row.id,
                "poison",
                row.attempt_count + 1,
                None,
                Some("url resolves to a private address"),
            )
            .await;
        }
        Err(e) => return Err(e),
    }
    Ok(())
}

async fn persist(
    pool: &PgPool,
    id: Uuid,
    status: &str,
    attempts: i32,
    http: Option<i32>,
    err: Option<&str>,
) {
    let _ = storage::mark_delivery(
        pool,
        id,
        status,
        attempts,
        OffsetDateTime::now_utc(),
        http,
        err,
    )
    .await;
}

#[allow(clippy::too_many_arguments)]
async fn pinned_post(
    dest: &reqwest::Url,
    allow_loopback: bool,
    body: &[u8],
    event_id: &str,
    event_type: &str,
    tenant_id: &str,
    unix: i64,
    sig: &str,
) -> Result<i32, String> {
    let host = dest.host_str().ok_or("endpoint url invalid")?.to_string();
    let port = dest.port_or_known_default().unwrap_or(80);
    let literal_ip = host.parse::<IpAddr>().ok();
    let addrs: Vec<SocketAddr> = if let Some(ip) = literal_ip {
        vec![SocketAddr::new(ip, port)]
    } else {
        (host.as_str(), port)
            .to_socket_addrs()
            .map_err(|e| e.to_string())?
            .collect()
    };
    if addrs.is_empty() {
        return Err("HostNotFound".into());
    }
    let allowed: Vec<SocketAddr> = addrs
        .into_iter()
        .filter(|a| !is_disallowed(a.ip(), allow_loopback))
        .collect();
    if allowed.is_empty() {
        return Err("private".into());
    }
    if !allow_loopback && host_is_loopback_name(&host) {
        return Err("private".into());
    }

    let mut builder = reqwest::Client::builder()
        .timeout(StdDuration::from_secs(10))
        .redirect(Policy::none())
        .no_proxy()
        .user_agent("Lazuar-Pay-Webhooks/1.0");
    if literal_ip.is_none() {
        for a in &allowed {
            builder = builder.resolve(&host, *a);
        }
    }
    let client = builder.build().map_err(|e| e.to_string())?;
    let res = client
        .post(dest.clone())
        .header("Content-Type", "application/json")
        .header("X-Lazuar-Signature", format!("v1={sig}"))
        .header("X-Lazuar-Timestamp", unix.to_string())
        .header("X-Lazuar-Event-Id", event_id)
        .header("X-Lazuar-Event-Type", event_type)
        .header("X-Lazuar-Tenant-Id", tenant_id)
        .body(body.to_vec())
        .send()
        .await
        .map_err(|e| e.to_string())?;
    Ok(res.status().as_u16() as i32)
}
