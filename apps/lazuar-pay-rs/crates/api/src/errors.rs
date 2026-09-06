use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use axum::Json;
use serde::Serialize;
use storage::ApplyError;

#[derive(Serialize)]
pub struct PayProblem {
    pub status: u16,
    pub title: String,
    pub detail: String,
}

pub fn problem(status: StatusCode, title: &str, detail: &str) -> Response {
    let body = PayProblem {
        status: status.as_u16(),
        title: title.into(),
        detail: detail.into(),
    };
    (status, Json(body)).into_response()
}

pub fn from_apply(err: ApplyError, pause_as_mint: bool) -> Response {
    match err {
        ApplyError::NotFound => problem(StatusCode::NOT_FOUND, "Not Found", "checkout not found"),
        ApplyError::Integrity => problem(StatusCode::BAD_REQUEST, "Bad Request", "amount mismatch"),
        ApplyError::Paused if pause_as_mint => {
            problem(StatusCode::FORBIDDEN, "Forbidden", "Org charges are paused")
        }
        ApplyError::Paused => problem(StatusCode::CONFLICT, "Conflict", "Org charges are paused"),
        ApplyError::Conflict => problem(StatusCode::CONFLICT, "Conflict", "conflict"),
        ApplyError::NotStartable | ApplyError::LiveAttemptExists => {
            problem(StatusCode::CONFLICT, "Conflict", "not startable")
        }
        ApplyError::IdempotencyMismatch => problem(
            StatusCode::CONFLICT,
            "Conflict",
            "Idempotency-Key reused with a different body",
        ),
        other => problem(
            StatusCode::INTERNAL_SERVER_ERROR,
            "Internal Server Error",
            &other.to_string(),
        ),
    }
}
