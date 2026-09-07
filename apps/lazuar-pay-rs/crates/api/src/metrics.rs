use axum::extract::State;
use axum::http::{header, HeaderMap, StatusCode};
use axum::response::{IntoResponse, Response};
use sqlx::Row;

use crate::errors::problem;
use crate::AppState;

pub async fn scrape(State(st): State<AppState>, headers: HeaderMap) -> Response {
    if !st.metrics_token.is_empty() {
        let token = headers
            .get("X-Metrics-Token")
            .and_then(|v| v.to_str().ok())
            .map(str::trim)
            .filter(|s| !s.is_empty())
            .or_else(|| {
                headers
                    .get(header::AUTHORIZATION)
                    .and_then(|v| v.to_str().ok())
                    .and_then(|s| s.strip_prefix("Bearer "))
                    .map(str::trim)
                    .filter(|s| !s.is_empty())
            });
        if token != Some(st.metrics_token.as_str()) {
            return problem(StatusCode::UNAUTHORIZED, "Unauthorized", "Invalid token");
        }
    }
    obs::reset_refunds_pending();
    if let Ok(rows) = sqlx::query(
        r#"
        SELECT rail,
               count(*)::bigint AS n,
               COALESCE(EXTRACT(EPOCH FROM (now() - min(created_at)))::bigint, 0) AS oldest
          FROM pay_rs.refunds
         WHERE status = 'pending'
         GROUP BY rail
        "#,
    )
    .fetch_all(&st.pool)
    .await
    {
        for row in rows {
            let rail: String = row.try_get("rail").unwrap_or_default();
            let n: i64 = row.try_get("n").unwrap_or(0);
            let oldest: i64 = row.try_get("oldest").unwrap_or(0);
            if !rail.is_empty() {
                obs::set_refunds_pending(&rail, n, oldest);
            }
        }
    }
    let poison: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.org_webhook_deliveries WHERE status = 'poison'",
    )
    .fetch_one(&st.pool)
    .await
    .unwrap_or(0);
    let leased: i64 = sqlx::query_scalar(
        "SELECT count(*)::bigint FROM pay_rs.org_webhook_deliveries WHERE leased_until IS NOT NULL",
    )
    .fetch_one(&st.pool)
    .await
    .unwrap_or(0);
    obs::set_jobs(poison, leased);
    let body = obs::encode();
    (
        StatusCode::OK,
        [(
            header::CONTENT_TYPE,
            "text/plain; version=0.0.4; charset=utf-8",
        )],
        body,
    )
        .into_response()
}
