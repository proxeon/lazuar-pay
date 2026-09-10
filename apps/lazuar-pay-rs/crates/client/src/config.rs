use crate::error::Error;

const DEFAULT_BASE: &str = "http://localhost:8081";

/// Env / flag bundle. Org is optional until an org-scoped command runs.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Config {
    pub base_url: String,
    pub api_key: String,
    pub org_id: Option<String>,
}

impl Config {
    pub fn new(
        base_url: impl Into<String>,
        api_key: impl Into<String>,
        org_id: Option<String>,
    ) -> Result<Self, Error> {
        let base_url = normalize_base(&base_url.into())?;
        let api_key = normalize_key(&api_key.into())?;
        let org_id = org_id
            .map(|s| s.trim().to_string())
            .filter(|s| !s.is_empty());
        Ok(Self {
            base_url,
            api_key,
            org_id,
        })
    }

    /// `LAZUAR_PAY_BASE_URL` / `LAZUAR_PAY_API_KEY` / `LAZUAR_PAY_ORG_ID`.
    pub fn from_env() -> Result<Self, Error> {
        Self::from_parts(
            std::env::var("LAZUAR_PAY_BASE_URL").ok(),
            std::env::var("LAZUAR_PAY_API_KEY").ok(),
            std::env::var("LAZUAR_PAY_ORG_ID").ok(),
        )
    }

    pub fn from_parts(
        base_url: Option<String>,
        api_key: Option<String>,
        org_id: Option<String>,
    ) -> Result<Self, Error> {
        let key = api_key.unwrap_or_default();
        if key.trim().is_empty() {
            return Err(Error::Config(
                "LAZUAR_PAY_API_KEY is required (One lzr_sk_ or Testing test-writer)".into(),
            ));
        }
        let base = base_url
            .filter(|s| !s.trim().is_empty())
            .unwrap_or_else(|| DEFAULT_BASE.into());
        Self::new(base, key, org_id)
    }

    pub fn org_id(&self) -> Result<&str, Error> {
        self.org_id
            .as_deref()
            .filter(|s| !s.is_empty())
            .ok_or_else(|| Error::Config("LAZUAR_PAY_ORG_ID is required for this command".into()))
    }

    pub fn url(&self, path: &str) -> String {
        format!("{}{}", self.base_url, path)
    }

    pub fn authorization(&self) -> String {
        format!("Bearer {}", self.api_key)
    }
}

fn normalize_base(raw: &str) -> Result<String, Error> {
    let s = raw.trim().trim_end_matches('/').to_string();
    if s.is_empty() {
        return Err(Error::Config("base URL is empty".into()));
    }
    Ok(s)
}

fn normalize_key(raw: &str) -> Result<String, Error> {
    let mut token = raw.trim();
    token = token
        .strip_prefix("Bearer ")
        .or_else(|| token.strip_prefix("bearer "))
        .unwrap_or(token)
        .trim();
    if token.is_empty() {
        return Err(Error::Config("API key is empty".into()));
    }
    // 032/13: Stripe/Hub sk_ is the wrong family. lzr_sk_ does not start with sk_.
    if token.starts_with("sk_") {
        return Err(Error::Config(
            "Authorization must be an One lzr_sk_ key, not a Stripe/Hub sk_".into(),
        ));
    }
    Ok(token.to_string())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn strips_trailing_slash() {
        let c = Config::new("http://localhost:8081/", "lzr_sk_test", Some("t1".into())).unwrap();
        assert_eq!(c.base_url, "http://localhost:8081");
        assert_eq!(c.url("/v1/whoami"), "http://localhost:8081/v1/whoami");
    }

    #[test]
    fn rejects_stripe_sk() {
        let err = Config::new("http://localhost:8081", "sk_test_abc", None).unwrap_err();
        assert!(err.to_string().contains("lzr_sk_"), "{err}");
    }

    #[test]
    fn rejects_sk_live() {
        let err = Config::new("http://localhost:8081", "sk_live_abc", None).unwrap_err();
        assert!(matches!(err, Error::Config(_)));
    }

    #[test]
    fn accepts_lzr_sk_and_test_writer() {
        Config::new("http://localhost:8081", "lzr_sk_test", None).unwrap();
        Config::new("http://localhost:8081", "test-writer", None).unwrap();
    }

    #[test]
    fn strips_bearer_prefix() {
        let c = Config::new(
            "http://localhost:8081",
            "Bearer lzr_sk_test",
            Some(" t1 ".into()),
        )
        .unwrap();
        assert_eq!(c.api_key, "lzr_sk_test");
        assert_eq!(c.org_id.as_deref(), Some("t1"));
        assert_eq!(c.authorization(), "Bearer lzr_sk_test");
    }

    #[test]
    fn org_id_required_on_scoped_commands() {
        let c = Config::new("http://localhost:8081", "lzr_sk_test", None).unwrap();
        assert!(c.org_id().is_err());
    }

    #[test]
    fn missing_key_from_parts() {
        let err = Config::from_parts(None, None, None).unwrap_err();
        assert!(err.to_string().contains("LAZUAR_PAY_API_KEY"), "{err}");
    }
}
