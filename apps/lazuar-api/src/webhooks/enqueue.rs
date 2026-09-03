//! Port of `Webhooks/Outbound/OutboundWebhookEnqueue.cs` — `TryAddAsync`.
//!
//! Must run inside the caller's transaction: the delivery row commits together
//! with the money write it describes. One endpoint per org (PK on OrgId);
//! deliveries dedupe on unique EventId.

use chrono::Utc;
use postgres::Transaction;
use uuid::Uuid;

use super::envelope;

/// Enqueue a delivery for the org's endpoint, if one exists and the event id
/// has not been delivered before. Best-effort by contract: no endpoint, no enqueue.
pub fn try_add(
    tx: &mut Transaction,
    org_id: &str,
    event_id: &str,
    event_type: &str,
    data: serde_json::Value,
) -> Result<(), postgres::Error> {
    if org_id.is_empty() || event_id.is_empty() {
        return Ok(());
    }

    let endpoint = tx.query_opt(
        "SELECT \"OrgId\" FROM public.org_webhook_endpoints WHERE \"OrgId\" = $1",
        &[&org_id],
    )?;
    if endpoint.is_none() {
        return Ok(());
    }

    let exists = tx
        .query_opt(
            "SELECT 1 FROM public.org_webhook_deliveries WHERE \"EventId\" = $1",
            &[&event_id],
        )?
        .is_some();
    if exists {
        return Ok(());
    }

    let payload = envelope::serialize(event_type, event_id, org_id, data);
    tx.execute(
        "INSERT INTO public.org_webhook_deliveries \
         (\"Id\",\"OrgId\",\"EventId\",\"EventType\",\"PayloadJson\",\"Status\",\
         \"AttemptCount\",\"NextAttemptAt\",\"CreatedAt\") \
         VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9)",
        &[
            &Uuid::new_v4().simple().to_string(),
            &org_id,
            &event_id,
            &event_type,
            &payload,
            &"pending",
            &0i32,
            &Utc::now(),
            &Utc::now(),
        ],
    )?;
    Ok(())
}
