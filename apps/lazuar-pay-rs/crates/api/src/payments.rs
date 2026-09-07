use axum::extract::{Path, Query, State};
use axum::response::{IntoResponse, Response};
use axum::Json;
use serde_json::json;
use uuid::Uuid;

use crate::errors::from_apply;
use crate::identity::{require_member, Bearer};
use crate::json::money_number;
use crate::payment_links::ListQuery;
use crate::AppState;

pub async fn list(
    State(st): State<AppState>,
    Bearer(who): Bearer,
    Path(org_id): Path<String>,
    Query(q): Query<ListQuery>,
) -> Response {
    if let Err(r) = require_member(&who, &org_id) {
        return r;
    }
    let limit = storage::catalog::clamp_limit(q.limit);
    let after = q.after.as_deref().and_then(parse_uuid);
    match storage::money_query::list_charges(&st.pool, &org_id, limit, after).await {
        Ok((rows, next)) => {
            let items: Vec<_> = rows.iter().map(charge_json).collect();
            Json(json!({"items": items, "next_cursor": next})).into_response()
        }
        Err(e) => from_apply(e, false),
    }
}

fn charge_json(row: &storage::money_query::ChargeListItem) -> serde_json::Value {
    json!({
        "id": storage::money_query::uuid_wire(row.id),
        "org_id": row.tenant_id,
        "checkout_id": row.payment_id.to_wire(),
        "amount": money_number(row.quoted),
        "currency": row.quoted.currency().code.as_str(),
        "status": row.status,
        "provider": row.provider,
        "payer_name": row.payer_name,
        "created_at": row.created_at.format(&time::format_description::well_known::Rfc3339).unwrap_or_default(),
        "label": row.label,
    })
}

fn parse_uuid(s: &str) -> Option<Uuid> {
    domain::ChargeId::from_wire(s)
        .ok()
        .map(|id| id.as_uuid())
        .or_else(|| Uuid::parse_str(s).ok())
}
