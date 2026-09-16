use axum::extract::{Path, Query, State};
use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use axum::Json;
use serde_json::json;
use uuid::Uuid;

use crate::errors::{from_apply, problem};
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
    match storage::money_query::list_documents(&st.pool, &org_id, limit, after).await {
        Ok((rows, next)) => {
            let items: Vec<_> = rows.iter().map(doc_json).collect();
            Json(json!({"items": items, "next_cursor": next})).into_response()
        }
        Err(e) => from_apply(e, false),
    }
}

pub async fn get(
    State(st): State<AppState>,
    Bearer(who): Bearer,
    Path((org_id, id)): Path<(String, String)>,
) -> Response {
    if let Err(r) = require_member(&who, &org_id) {
        return r;
    }
    let Some(uid) = parse_uuid(&id) else {
        return problem(StatusCode::NOT_FOUND, "Not Found", "Receipt not found");
    };
    match storage::money_query::document_by_id(&st.pool, &org_id, uid).await {
        Ok(Some(row)) => Json(doc_json(&row)).into_response(),
        Ok(None) => problem(StatusCode::NOT_FOUND, "Not Found", "Receipt not found"),
        Err(e) => from_apply(e, false),
    }
}

fn doc_json(row: &storage::money_query::DocumentListItem) -> serde_json::Value {
    let issued = !row.number.is_empty();
    json!({
        "id": storage::money_query::uuid_wire(row.id),
        "org_id": row.tenant_id,
        "number": row.number,
        "title": row.title,
        "checkout_id": row.payment_id.to_wire(),
        "amount": money_number(row.quoted),
        "currency": row.quoted.currency().code.as_str(),
        "payer_name": row.payer_name,
        "created_at": row.created_at.format(&time::format_description::well_known::Rfc3339).unwrap_or_default(),
        "label": row.label,
        "status": if issued { "issued" } else { "pending" },
    })
}

fn parse_uuid(s: &str) -> Option<Uuid> {
    domain::ChargeId::from_wire(s)
        .ok()
        .map(|id| id.as_uuid())
        .or_else(|| Uuid::parse_str(s).ok())
}
