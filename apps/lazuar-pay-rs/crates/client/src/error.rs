use serde_json::Value;

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
}

impl Error {
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

    pub fn exit_code(&self) -> i32 {
        match self {
            Self::Config(_) => 2,
            Self::Api { status, .. } if (400..500).contains(status) => 1,
            Self::Api { .. } | Self::Transport(_) => 1,
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
        match err {
            Error::Api { status, detail, .. } => {
                assert_eq!(status, 401);
                assert_eq!(detail, "Missing bearer token");
            }
            other => panic!("{other}"),
        }
    }
}
