use axum::extract::State;
use axum::http::StatusCode;
use axum::Json;
use serde_json::{json, Value};

use crate::AppState;

pub async fn health() -> Json<Value> {
    Json(json!({"status": "ok"}))
}

pub async fn ready(State(st): State<AppState>) -> (StatusCode, Json<Value>) {
    match sqlx::query_scalar::<_, i32>("SELECT 1")
        .fetch_one(&st.pool)
        .await
    {
        Ok(_) => (StatusCode::OK, Json(json!({"status": "ok"}))),
        Err(_) => (
            StatusCode::SERVICE_UNAVAILABLE,
            Json(json!({"status": "not_ready"})),
        ),
    }
}
