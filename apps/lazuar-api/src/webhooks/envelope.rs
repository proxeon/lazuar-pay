//! Port of `Webhooks/Outbound/PayWebhookEnvelope.cs`.

use chrono::Utc;
use serde_json::{json, Value};

pub const COMPLETED: &str = "payment.completed";
pub const FAILED: &str = "payment.failed";
pub const EXPIRED: &str = "checkout.expired";
pub const REFUND_CREATED: &str = "refund.created";
pub const TEST: &str = "webhook.test";

/// The Plane C envelope: `{ id, type, created_at, org_id, api_version, data }`.
/// D010: `created_at` is RFC 3339 UTC — the .NET default serializer emits the
/// same ISO-8601 instant with a numeric offset; consumers parse both.
pub fn serialize(event_type: &str, id: &str, org_id: &str, data: Value) -> String {
    json!({
        "id": id,
        "type": event_type,
        "created_at": Utc::now(),
        "org_id": org_id,
        "api_version": "0.1.0",
        "data": data,
    })
    .to_string()
}
