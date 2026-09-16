use axum::extract::{Path, Query, State};
use axum::response::{IntoResponse, Response};
use axum::Json;
use serde_json::json;

use crate::identity::{require_member, Bearer};
use crate::payment_links::ListQuery;
use crate::AppState;

pub async fn list(
    State(_st): State<AppState>,
    Bearer(who): Bearer,
    Path(org_id): Path<String>,
    Query(_q): Query<ListQuery>,
) -> Response {
    if let Err(r) = require_member(&who, &org_id) {
        return r;
    }
    Json(json!({"items": [], "next_cursor": serde_json::Value::Null})).into_response()
}
