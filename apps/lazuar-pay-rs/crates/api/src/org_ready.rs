use axum::extract::{Path, State};
use axum::response::{IntoResponse, Response};
use axum::Json;
use serde_json::json;

use crate::errors::from_apply;
use crate::identity::{require_member, Bearer};
use crate::AppState;

pub async fn get(
    State(st): State<AppState>,
    Bearer(who): Bearer,
    Path(org_id): Path<String>,
) -> Response {
    if let Err(r) = require_member(&who, &org_id) {
        return r;
    }
    let paused = match storage::read::charges_paused(&st.pool, &org_id).await {
        Ok(v) => v,
        Err(e) => return from_apply(e, false),
    };
    let vault = match storage::org_settings::has_vault(&st.pool, &org_id).await {
        Ok(v) => v,
        Err(e) => return from_apply(e, false),
    };
    let ready = !paused && (vault || st.env.allows_test());
    Json(json!({"org_id": org_id, "ready": ready})).into_response()
}
