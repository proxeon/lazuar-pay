//! Port of `Webhooks/Outbound/OutboundWebhookDispatch.cs` + `OutboundWebhookWorker.cs`.
//!
//! Issue 005: claim the batch with a 60s lease under FOR UPDATE SKIP LOCKED (the
//! lease outlives the 10s send timeout, so a crashed worker's rows become
//! claimable again), persist EACH ROW's outcome in its own transaction, and let
//! a poison row error itself while the batch moves on. Retry backoff
//! `min(300, 15 * attempts)`; 401/403/410 dead-letter; everything else retries.
//!
//! Issue 017: DNS re-resolves per attempt through the pinning resolver, so the
//! dialed address is filtered at connect time — the pre-send advisory check alone
//! was the rebinding hole.

use std::net::ToSocketAddrs;
use std::sync::Arc;

use chrono::Utc;
use postgres::Transaction;
use uuid::Uuid;

use crate::identity::one_webhook_signature;
use crate::secrets::SecretBox;
use crate::transport::{OutRequest, Transport};

pub const BATCH_SIZE: i64 = 20;
pub const LEASE_SECONDS: i64 = 60; // > send timeout (10s): crashed workers self-heal
pub const SEND_TIMEOUT_SECS: u64 = 10;

struct ClaimedDelivery {
    id: String,
    org_id: String,
    event_id: String,
    event_type: String,
    payload_json: String,
    attempt_count: i32,
}

struct RowOutcome {
    id: String,
    status: Option<&'static str>,
    last_http_status: Option<i32>,
    last_error: Option<String>,
    next_attempt_at: Option<chrono::DateTime<Utc>>,
    attempt_count: i32,
}

/// One worker pass. Claims a batch and delivers each row with per-row
/// persistence; returns the number of rows processed.
pub fn process_batch(
    conn: &mut postgres::Client,
    box_one: &SecretBox,
    transport: &dyn Transport,
    environment: &str,
) -> Result<usize, postgres::Error> {
    let now = Utc::now();
    let lease_until = now + chrono::Duration::seconds(LEASE_SECONDS);

    let claimed: Vec<ClaimedDelivery> = {
        let mut tx = conn.transaction()?;
        let rows = tx.query(
            "UPDATE public.org_webhook_deliveries AS d \
             SET \"NextAttemptAt\" = $1 \
             FROM ( \
                 SELECT \"Id\" FROM public.org_webhook_deliveries \
                 WHERE \"Status\" = 'pending' AND \"NextAttemptAt\" <= $2 \
                 ORDER BY \"CreatedAt\" \
                 LIMIT $3 \
                 FOR UPDATE SKIP LOCKED \
             ) AS pick \
             WHERE d.\"Id\" = pick.\"Id\" \
             RETURNING d.\"Id\", d.\"OrgId\", d.\"EventId\", d.\"EventType\", \
                       d.\"PayloadJson\", d.\"AttemptCount\"",
            &[&lease_until, &now, &BATCH_SIZE],
        )?;
        let mut claimed = Vec::with_capacity(rows.len());
        for row in &rows {
            claimed.push(ClaimedDelivery {
                id: row.get(0),
                org_id: row.get(1),
                event_id: row.get(2),
                event_type: row.get(3),
                payload_json: row.get(4),
                attempt_count: row.get(5),
            });
        }
        tx.commit()?;
        claimed
    };

    let count = claimed.len();
    for row in claimed {
        let outcome = match deliver(conn, box_one, transport, environment, &row) {
            Ok(outcome) => outcome,
            // Issue 005: a poison row errors ITSELF and the batch moves on — it can
            // no longer abort the pipeline or erase outcomes behind it.
            Err(message) => RowOutcome {
                id: row.id.clone(),
                status: None,
                last_http_status: None,
                last_error: Some(format!("dispatch:{message}")),
                next_attempt_at: Some(Utc::now() + backoff(row.attempt_count + 1)),
                attempt_count: row.attempt_count + 1,
            },
        };
        persist_outcome(conn, &outcome)?;
    }
    Ok(count)
}

fn backoff(attempt_count: i32) -> chrono::Duration {
    let secs = 15i64.saturating_mul(i64::from(attempt_count)).min(300);
    chrono::Duration::seconds(secs)
}

fn deliver(
    conn: &mut postgres::Client,
    box_one: &SecretBox,
    transport: &dyn Transport,
    environment: &str,
    row: &ClaimedDelivery,
) -> Result<RowOutcome, String> {
    let mut outcome = RowOutcome {
        id: row.id.clone(),
        status: None,
        last_http_status: None,
        last_error: None,
        next_attempt_at: None,
        attempt_count: row.attempt_count,
    };

    let endpoint = conn
        .query_opt(
            "SELECT \"Url\",\"SecretCiphertext\" FROM public.org_webhook_endpoints WHERE \"OrgId\" = $1",
            &[&row.org_id],
        )
        .map_err(|e| e.to_string())?
        .map(|r| (r.get::<_, String>(0), r.get::<_, String>(1)));
    let Some((url, secret_ciphertext)) = endpoint else {
        outcome.status = Some("dead");
        outcome.last_error = Some("endpoint missing".into());
        return Ok(outcome);
    };

    // Re-resolve per attempt at CONNECT time through the pinning resolver: the
    // DNS answer at registration is not the answer at send time.
    if url.parse::<url::Url>().is_err() {
        outcome.status = Some("dead");
        outcome.last_error = Some("endpoint url invalid".into());
        return Ok(outcome);
    }

    // Advisory pre-send resolve (C# DeliverAsync): a URL that resolves into
    // private space dead-rows in production instead of pointing signed payloads
    // at the internal network. The pinning resolver below is the connect-time
    // half of the same defense (issue 017).
    if let Ok(u) = url::Url::parse(&url) {
        if let Some(host) = u.host_str() {
            let port = u.port_or_known_default().unwrap_or(80);
            if let Ok(addrs) = (host, port).to_socket_addrs() {
                let addrs: Vec<std::net::SocketAddr> = addrs.collect();
                let any_private = addrs.iter().any(|a| crate::webhooks::outbound_url::is_disallowed(a.ip(), environment));
                if any_private {
                    outcome.status = Some("dead");
                    outcome.last_error = Some("url resolves to a private address".into());
                    return Ok(outcome);
                }
            }
        }
    }

    let secret = box_one
        .unprotect(&secret_ciphertext)
        .map_err(|_| "secret unwrap failed".to_string())?;

    let unix = Utc::now().timestamp();
    let v1 = one_webhook_signature::compute(&secret, &row.payload_json, unix);

    // The transport carries the pinning resolver — the dial lands on a validated
    // address or fails closed (issue 017).
    let request = OutRequest {
        method: "POST".into(),
        url: url.clone(),
        headers: vec![
            ("Content-Type".into(), "application/json".into()),
            ("X-Lazuar-Signature".into(), format!("v1={v1}")),
            ("X-Lazuar-Timestamp".into(), unix.to_string()),
            ("X-Lazuar-Event-Id".into(), row.event_id.clone()),
            ("X-Lazuar-Event-Type".into(), row.event_type.clone()),
            ("X-Lazuar-Tenant-Id".into(), row.org_id.clone()),
            ("User-Agent".into(), "Lazuar-Pay-Webhooks/1.0".into()),
        ],
        body: Some(row.payload_json.clone()),
    };

    match transport.send(request) {
        Ok(response) => {
            outcome.last_http_status = Some(i32::from(response.status));
            outcome.attempt_count = row.attempt_count + 1;
            if (200..300).contains(&response.status) {
                outcome.status = Some("succeeded");
            } else if matches!(response.status, 401 | 403 | 410) {
                outcome.status = Some("dead");
            } else {
                outcome.next_attempt_at =
                    Some(Utc::now() + backoff(row.attempt_count + 1));
            }
            Ok(outcome)
        }
        Err(transport_error) => {
            outcome.attempt_count = row.attempt_count + 1;
            outcome.last_error = Some(format!("{transport_error}"));
            outcome.next_attempt_at = Some(Utc::now() + backoff(row.attempt_count + 1));
            Ok(outcome)
        }
    }
}

fn persist_outcome(conn: &mut postgres::Client, outcome: &RowOutcome) -> Result<(), postgres::Error> {
    // Per-row persistence: whatever happened to this row survives even if the
    // next row throws (issue 005).
    let mut tx = conn.transaction()?;
    tx.execute(
        "UPDATE public.org_webhook_deliveries \
         SET \"Status\" = COALESCE($1, \"Status\"), \
             \"LastHttpStatus\" = COALESCE($2, \"LastHttpStatus\"), \
             \"LastError\" = COALESCE($3, \"LastError\"), \
             \"NextAttemptAt\" = COALESCE($4, \"NextAttemptAt\"), \
             \"AttemptCount\" = $5 \
         WHERE \"Id\" = $6",
        &[
            &outcome.status,
            &outcome.last_http_status,
            &outcome.last_error,
            &outcome.next_attempt_at,
            &outcome.attempt_count,
            &outcome.id,
        ],
    )?;
    tx.commit()?;
    Ok(())
}

/// Build the webhook transport with the connect-time pinning resolver.
pub fn webhook_transport(environment: &str) -> Arc<dyn Transport> {
    Arc::new(WebhookTransport {
        inner: ureq::AgentBuilder::new()
            .timeout(std::time::Duration::from_secs(SEND_TIMEOUT_SECS))
            .resolver(crate::webhooks::outbound_url::PinningResolver {
                environment: environment.to_string(),
            })
            .build(),
    })
}

struct WebhookTransport {
    inner: ureq::Agent,
}

impl Transport for WebhookTransport {
    fn send(&self, request: OutRequest) -> Result<crate::transport::OutResponse, crate::transport::TransportError> {
        let mut req = self.inner.request(&request.method, &request.url);
        for (name, value) in &request.headers {
            req = req.set(name, value);
        }
        let dispatched = match &request.body {
            Some(body) => req.send_string(body),
            None => req.call(),
        };
        match dispatched {
            Ok(resp) => Ok(crate::transport::OutResponse {
                status: resp.status(),
                body: resp.into_string().unwrap_or_default(),
            }),
            Err(ureq::Error::Status(code, resp)) => Ok(crate::transport::OutResponse {
                status: code,
                body: resp.into_string().unwrap_or_default(),
            }),
            Err(ureq::Error::Transport(t)) => Err(crate::transport::TransportError::Transport(t.to_string())),
        }
    }
}

/// The worker loop (C# `OutboundWebhookWorker`), with the silent-swallow fix:
/// every iteration failure is LOGGED (the C# worker had no logger at all).
pub fn worker_loop(conn_string: String, box_one: SecretBox, environment: String) -> ! {
    loop {
        let result = (|| -> Result<usize, String> {
            let mut conn = postgres::Client::connect(&conn_string, postgres::NoTls)
                .map_err(|e| format!("db connect: {e}"))?;
            let transport = webhook_transport(&environment);
            process_batch(&mut conn, &box_one, transport.as_ref(), &environment)
                .map_err(|e| format!("batch: {e}"))
        })();
        match result {
            Ok(0) => {}
            Ok(n) => log::info!("webhook dispatch processed {n} deliveries"),
            Err(message) => log::error!("webhook dispatch pass failed: {message}"),
        }
        std::thread::sleep(std::time::Duration::from_secs(5));
    }
}
