use serde_json::{json, Value};

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("{0}")]
    Config(String),
    #[error("pay {status}: {detail}")]
    Api {
        status: u16,
        title: String,
        detail: String,
    },
    #[error("transport: {0}")]
    Transport(#[from] reqwest::Error),
    /// Poll loop hit `--timeout-secs` before `status` matched `--until`.
    #[error("checkout wait timed out (until {until})")]
    WaitTimeout { until: String, last: Value },
    /// Wire status is already terminal and will not become `until` (paid/failed/expired).
    #[error("checkout is {status}, not {until}")]
    WaitConflict {
        until: String,
        status: String,
        last: Value,
    },
}

impl Error {
    /// Problem+json for stderr (036/006 #8). Agents parse this; do not pretty-print secrets.
    pub fn to_json(&self) -> Value {
        match self {
            Self::Config(detail) => json!({
                "status": 400,
                "title": "Config",
                "detail": detail,
            }),
            Self::Api {
                status,
                title,
                detail,
            } => json!({
                "status": status,
                "title": title,
                "detail": detail,
            }),
            Self::Transport(e) => json!({
                "status": 0,
                "title": "Transport",
                "detail": e.to_string(),
            }),
            Self::WaitTimeout { until, last } => json!({
                "status": 408,
                "title": "Timeout",
                "detail": format!("checkout wait timed out (until {until})"),
                "last": last,
            }),
            Self::WaitConflict {
                until,
                status,
                last,
            } => json!({
                "status": 409,
                "title": "Conflict",
                "detail": format!("checkout is {status}, not {until}"),
                "last": last,
            }),
        }
    }

    pub fn from_problem(status: u16, body: &Value) -> Self {
        let title = body
            .get("title")
            .and_then(Value::as_str)
            .unwrap_or("Error")
            .to_string();
        let detail = body
            .get("detail")
            .and_then(Value::as_str)
            .unwrap_or_else(|| body.get("title").and_then(Value::as_str).unwrap_or("error"))
            .to_string();
        Self::Api {
            status,
            title,
            detail,
        }
    }

    /// Process exit for `lazuar-pay` (036/006 #11).
    ///
    /// 0 ok · 1 other 4xx · 2 config · 3 auth (401/403) · 4 not found ·
    /// 5 server 5xx · 6 transport · 8 checkout-wait timeout.
    pub fn exit_code(&self) -> i32 {
        match self {
            Self::Config(_) => 2,
            Self::WaitTimeout { .. } => 8,
            Self::WaitConflict { .. } => 1,
            Self::Transport(_) => 6,
            Self::Api {
                status: 401 | 403, ..
            } => 3,
            Self::Api { status: 404, .. } => 4,
            Self::Api { status, .. } if (500..600).contains(status) => 5,
            Self::Api { .. } => 1,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    #[test]
    fn problem_json_maps_detail() {
        let err = Error::from_problem(
            401,
            &json!({"status":401,"title":"Unauthorized","detail":"Missing bearer token"}),
        );
        match &err {
            Error::Api { status, detail, .. } => {
                assert_eq!(*status, 401);
                assert_eq!(detail, "Missing bearer token");
            }
            other => panic!("{other}"),
        }
        let j = err.to_json();
        assert_eq!(j["status"], 401);
        assert_eq!(j["detail"], "Missing bearer token");
    }

    #[test]
    fn config_to_json() {
        let j = Error::Config("no key".into()).to_json();
        assert_eq!(j["title"], "Config");
        assert_eq!(j["detail"], "no key");
    }

    #[test]
    fn exit_codes_distinguish_auth_not_found_server_transport() {
        assert_eq!(Error::Config("x".into()).exit_code(), 2);
        assert_eq!(
            Error::from_problem(401, &json!({"title": "Unauthorized"})).exit_code(),
            3
        );
        assert_eq!(
            Error::from_problem(403, &json!({"title": "Forbidden"})).exit_code(),
            3
        );
        assert_eq!(
            Error::from_problem(404, &json!({"title": "Not Found"})).exit_code(),
            4
        );
        assert_eq!(
            Error::from_problem(400, &json!({"title": "Bad Request"})).exit_code(),
            1
        );
        assert_eq!(
            Error::from_problem(409, &json!({"title": "Conflict"})).exit_code(),
            1
        );
        assert_eq!(
            Error::from_problem(500, &json!({"title": "Internal"})).exit_code(),
            5
        );
        assert_eq!(
            Error::WaitTimeout {
                until: "paid".into(),
                last: json!({"status": "open"}),
            }
            .exit_code(),
            8
        );
        assert_eq!(
            Error::WaitConflict {
                until: "paid".into(),
                status: "failed".into(),
                last: json!({"status": "failed"}),
            }
            .exit_code(),
            1
        );
    }
}
