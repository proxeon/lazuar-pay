//! `GET /v1/orgs/{org}/events` — Plane C cursor for agents that cannot host HTTPS (036/006 #29).

use axum::extract::{Path, Query, State};
use axum::response::{IntoResponse, Response};
use axum::Json;
use serde_json::json;

use crate::identity::{require_member, Bearer};
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
    let after = q.after.as_deref().map(str::trim).filter(|s| !s.is_empty());
    match storage::list_org_events(&st.pool, &org_id, limit, after).await {
        Ok((rows, next)) => {
            let items: Vec<_> = rows
                .iter()
                .map(|e| {
                    json!({
                        "event_id": e.event_id,
                        "type": e.event_type,
                        "status": e.status,
                        "created_at": e.created_at.format(&time::format_description::well_known::Rfc3339).unwrap_or_default(),
                        "data": e.payload,
                    })
                })
                .collect();
            Json(json!({"items": items, "next_cursor": next})).into_response()
        }
        Err(e) => crate::errors::from_apply(e, false),
    }
}
