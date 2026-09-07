//! ThrowIfMisconfigured (032/14). Testing WrapKey fallback is SHA-256 only.

use base64::Engine;
use sha2::{Digest, Sha256};

use crate::cors;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Env {
    Testing,
    Development,
    Staging,
    Production,
}

impl Env {
    pub fn parse(s: &str) -> Self {
        match s.trim() {
            "Development" => Self::Development,
            "Staging" => Self::Staging,
            "Production" => Self::Production,
            _ => Self::Testing,
        }
    }

    pub fn allows_test(self) -> bool {
        matches!(self, Self::Testing | Self::Development)
    }

    pub fn skip_misconfigured(self) -> bool {
        matches!(self, Self::Testing | Self::Development)
    }
}

/// Process config for boot checks. Tests construct this without a host.
#[derive(Clone, Debug, Default)]
pub struct BootCfg {
    pub wrap_key: String,
    pub connection_string: String,
    pub one_base_url: String,
    pub one_api_key: String,
    pub one_worker_org_id: String,
    pub checkout_base_url: String,
    pub cors_origins_raw: String,
    pub start_max_per_minute: i32,
    pub solana_rpc_url: String,
    pub solana_cluster: String,
}

impl BootCfg {
    pub fn from_env() -> Self {
        let connection_string = std::env::var("ConnectionStrings__Pay")
            .or_else(|_| std::env::var("DATABASE_URL"))
            .unwrap_or_default();
        let start_max_per_minute = std::env::var("Pay__StartMaxPerMinute")
            .ok()
            .and_then(|s| s.parse().ok())
            .unwrap_or(20);
        Self {
            wrap_key: std::env::var("Pay__WrapKey").unwrap_or_default(),
            connection_string,
            one_base_url: std::env::var("One__BaseUrl").unwrap_or_default(),
            one_api_key: std::env::var("One__ApiKey").unwrap_or_default(),
            one_worker_org_id: std::env::var("One__WorkerOrgId").unwrap_or_default(),
            checkout_base_url: std::env::var("Pay__CheckoutBaseUrl").unwrap_or_default(),
            cors_origins_raw: std::env::var("Pay__CorsOrigins").unwrap_or_default(),
            start_max_per_minute,
            solana_rpc_url: std::env::var("Pay__Solana__RpcUrl").unwrap_or_default(),
            solana_cluster: std::env::var("Pay__Solana__Cluster").unwrap_or_default(),
        }
    }
}

/// SHA-256(`lazuar-pay-dev-wrap-key`) when Testing and WrapKey empty.
pub fn wrap_key_testing_fallback(env: Env, configured: &str) -> Option<[u8; 32]> {
    if !configured.is_empty() {
        return None;
    }
    if env != Env::Testing {
        return None;
    }
    let d = Sha256::digest(b"lazuar-pay-dev-wrap-key");
    Some(d.into())
}

pub fn wrap_key_bytes(env: Env, configured: &str) -> Result<[u8; 32], String> {
    if let Some(k) = wrap_key_testing_fallback(env, configured) {
        return Ok(k);
    }
    let trimmed = configured.trim();
    if trimmed.is_empty() {
        if env.skip_misconfigured() {
            return Ok([0u8; 32]);
        }
        return Err("Pay:WrapKey is required".into());
    }
    decode_wrap_key(trimmed)
}

fn decode_wrap_key(wrap: &str) -> Result<[u8; 32], String> {
    let bytes = base64::engine::general_purpose::STANDARD
        .decode(wrap.trim())
        .map_err(|_| "Pay:WrapKey must be 32 bytes base64".to_string())?;
    <[u8; 32]>::try_from(bytes.as_slice())
        .map_err(|_| "Pay:WrapKey must be 32 bytes base64".to_string())
}

fn throw_if_worker_key(cfg: &BootCfg) -> Result<(), String> {
    let key = cfg.one_api_key.trim();
    if key.is_empty() {
        return Ok(());
    }
    if key.starts_with("sk_") {
        return Err("One:ApiKey must be a One lzr_sk_ key, not sk_".into());
    }
    if !key.starts_with("lzr_sk_") {
        return Err("One:ApiKey must start with lzr_sk_".into());
    }
    if cfg.one_worker_org_id.trim().is_empty() {
        return Err("One:WorkerOrgId is required when One:ApiKey is set".into());
    }
    Ok(())
}

fn checkout_https_origin(raw: &str) -> Option<String> {
    let u = reqwest::Url::parse(raw.trim()).ok()?;
    if u.scheme() != "https" {
        return None;
    }
    let host = u.host_str()?;
    let origin = match u.port() {
        Some(p) => format!("{}://{host}:{p}", u.scheme()),
        None => format!("{}://{host}", u.scheme()),
    };
    Some(origin.trim_end_matches('/').to_string())
}

fn rpc_is_loopback(u: &reqwest::Url) -> bool {
    let Some(host) = u.host_str() else {
        return true;
    };
    if host.eq_ignore_ascii_case("localhost") {
        return true;
    }
    host.parse::<std::net::IpAddr>()
        .map(|ip| ip.is_loopback())
        .unwrap_or(false)
}

pub fn throw_if_misconfigured(env: Env, cfg: &BootCfg) -> Result<(), String> {
    if env.skip_misconfigured() {
        return Ok(());
    }

    if cfg.wrap_key.trim().is_empty() {
        return Err("Pay:WrapKey is required".into());
    }
    if cfg.connection_string.trim().is_empty() {
        return Err("ConnectionStrings:Pay is required".into());
    }

    let one = cfg.one_base_url.trim();
    if one.is_empty() || one.to_ascii_lowercase().contains("localhost") || one.contains("127.0.0.1")
    {
        return Err("One:BaseUrl must be a public URL in Production and Staging".into());
    }

    throw_if_worker_key(cfg)?;
    decode_wrap_key(&cfg.wrap_key)?;

    let checkout = cfg.checkout_base_url.trim();
    let Some(checkout_origin) = checkout_https_origin(checkout) else {
        return Err("Pay:CheckoutBaseUrl must be public https in Production and Staging".into());
    };

    let cors_origins = cors::resolve(&cfg.cors_origins_raw, env)?;
    if env == Env::Production {
        for origin in &cors_origins {
            let ok = reqwest::Url::parse(origin)
                .ok()
                .is_some_and(|u| u.scheme() == "https" && u.host_str().is_some());
            if !ok {
                return Err("Pay:CorsOrigins must be https in Production".into());
            }
        }
    }

    if !cors_origins
        .iter()
        .any(|o| o.eq_ignore_ascii_case(&checkout_origin))
    {
        return Err("Pay:CheckoutBaseUrl origin must be in Pay:CorsOrigins".into());
    }

    if cfg.start_max_per_minute <= 0 {
        return Err("Pay:StartMaxPerMinute must be greater than 0".into());
    }

    let rpc = cfg.solana_rpc_url.trim();
    if rpc.is_empty() {
        return Ok(());
    }

    let cluster = cfg.solana_cluster.trim();
    if cluster != "mainnet-beta" && cluster != "devnet" {
        return Err("Pay:Solana:Cluster must be mainnet-beta or devnet".into());
    }
    if env == Env::Production && cluster != "mainnet-beta" {
        return Err("Pay:Solana:Cluster must be mainnet-beta in Production".into());
    }

    let parsed = reqwest::Url::parse(rpc).ok();
    let public_https = parsed
        .as_ref()
        .is_some_and(|u| u.scheme() == "https" && u.host_str().is_some() && !rpc_is_loopback(u));
    if !public_https
        || rpc.contains("VITE_")
        || rpc
            .to_ascii_lowercase()
            .contains("api.mainnet-beta.solana.com")
        || rpc.to_ascii_lowercase().contains("api.devnet.solana.com")
    {
        return Err("Pay:Solana:RpcUrl must be a public https RPC".into());
    }

    if cluster == "mainnet-beta" && rpc.to_ascii_lowercase().contains("devnet") {
        return Err("Pay:Solana:RpcUrl genesis hash mismatch".into());
    }
    if cluster == "devnet" && rpc.to_ascii_lowercase().contains("mainnet") {
        return Err("Pay:Solana:RpcUrl genesis hash mismatch".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    const WRAP: &str = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    fn production_ok() -> BootCfg {
        BootCfg {
            wrap_key: WRAP.into(),
            connection_string: "Host=db".into(),
            one_base_url: "https://one.example/api/v1".into(),
            checkout_base_url: "https://checkout.example".into(),
            cors_origins_raw: "https://checkout.example".into(),
            start_max_per_minute: 20,
            ..BootCfg::default()
        }
    }

    #[test]
    fn testing_empty_wrap_key_is_sha256() {
        let k = wrap_key_testing_fallback(Env::Testing, "").expect("fallback");
        let d = Sha256::digest(b"lazuar-pay-dev-wrap-key");
        assert_eq!(&k[..], d.as_slice());
    }

    #[test]
    fn production_empty_wrap_key_does_not_fallback() {
        assert!(wrap_key_testing_fallback(Env::Production, "").is_none());
        assert!(wrap_key_testing_fallback(Env::Staging, "").is_none());
        assert!(wrap_key_testing_fallback(Env::Development, "").is_none());
    }

    #[test]
    fn wrap_key_bytes_production_empty_is_err() {
        let err = wrap_key_bytes(Env::Production, "").expect_err("zeros forbidden");
        assert!(err.contains("WrapKey"), "{err}");
        let err = wrap_key_bytes(Env::Staging, "").expect_err("zeros forbidden");
        assert!(err.contains("WrapKey"), "{err}");
    }

    #[test]
    fn wrap_key_bytes_testing_empty_is_sha256() {
        let k = wrap_key_bytes(Env::Testing, "").expect("fallback");
        let d = Sha256::digest(b"lazuar-pay-dev-wrap-key");
        assert_eq!(&k[..], d.as_slice());
    }

    #[test]
    fn throw_if_misconfigured_skips_testing_and_development() {
        let empty = BootCfg::default();
        assert!(throw_if_misconfigured(Env::Testing, &empty).is_ok());
        assert!(throw_if_misconfigured(Env::Development, &empty).is_ok());
    }

    #[test]
    fn production_empty_wrap_key_throws() {
        let mut cfg = production_ok();
        cfg.wrap_key.clear();
        let err = throw_if_misconfigured(Env::Production, &cfg).unwrap_err();
        assert!(err.contains("WrapKey"), "{err}");
    }

    #[test]
    fn production_empty_cs_throws() {
        let mut cfg = production_ok();
        cfg.connection_string.clear();
        let err = throw_if_misconfigured(Env::Production, &cfg).unwrap_err();
        assert!(err.contains("ConnectionStrings:Pay"), "{err}");
    }

    #[test]
    fn production_localhost_one_url_throws() {
        let mut cfg = production_ok();
        cfg.one_base_url = "http://localhost:8080/api/v1".into();
        let err = throw_if_misconfigured(Env::Production, &cfg).unwrap_err();
        assert!(err.contains("One:BaseUrl"), "{err}");
    }

    #[test]
    fn testing_allows_empty() {
        assert!(throw_if_misconfigured(Env::Testing, &BootCfg::default()).is_ok());
    }

    #[test]
    fn production_without_solana_rpc_does_not_require_cluster() {
        assert!(throw_if_misconfigured(Env::Production, &production_ok()).is_ok());
    }

    #[test]
    fn production_devnet_cluster_throws() {
        let mut cfg = production_ok();
        cfg.solana_cluster = "devnet".into();
        cfg.solana_rpc_url = "https://rpc.example/devnet".into();
        let err = throw_if_misconfigured(Env::Production, &cfg).unwrap_err();
        assert!(err.contains("mainnet-beta"), "{err}");
    }

    #[test]
    fn production_public_solana_rpc_throws() {
        let mut cfg = production_ok();
        cfg.solana_cluster = "mainnet-beta".into();
        cfg.solana_rpc_url = "https://api.mainnet-beta.solana.com".into();
        let err = throw_if_misconfigured(Env::Production, &cfg).unwrap_err();
        assert!(err.contains("RpcUrl"), "{err}");
    }

    #[test]
    fn production_checkout_origin_must_be_in_cors() {
        let mut cfg = production_ok();
        cfg.cors_origins_raw = "https://merchant.example".into();
        let err = throw_if_misconfigured(Env::Production, &cfg).unwrap_err();
        assert!(err.contains("CorsOrigins"), "{err}");
    }

    #[test]
    fn production_http_cors_origin_throws() {
        let mut cfg = production_ok();
        cfg.cors_origins_raw = "http://checkout.example".into();
        let err = throw_if_misconfigured(Env::Production, &cfg).unwrap_err();
        assert!(err.contains("https"), "{err}");
    }

    #[test]
    fn staging_empty_cors_throws() {
        let mut cfg = production_ok();
        cfg.cors_origins_raw.clear();
        let err = throw_if_misconfigured(Env::Staging, &cfg).unwrap_err();
        assert!(err.contains("Pay:CorsOrigins"), "{err}");
    }

    #[test]
    fn worker_sk_family_throws() {
        let mut cfg = production_ok();
        cfg.one_api_key = "sk_live_xxx".into();
        cfg.one_worker_org_id = "t1".into();
        let err = throw_if_misconfigured(Env::Production, &cfg).unwrap_err();
        assert!(err.contains("lzr_sk_"), "{err}");
    }

    #[test]
    fn worker_key_requires_org() {
        let mut cfg = production_ok();
        cfg.one_api_key = "lzr_sk_job".into();
        let err = throw_if_misconfigured(Env::Production, &cfg).unwrap_err();
        assert!(err.contains("WorkerOrgId"), "{err}");
    }

    #[test]
    fn worker_lzr_sk_with_org_ok() {
        let mut cfg = production_ok();
        cfg.one_api_key = "lzr_sk_job".into();
        cfg.one_worker_org_id = "t1".into();
        assert!(throw_if_misconfigured(Env::Production, &cfg).is_ok());
    }

    #[test]
    fn start_max_must_be_positive() {
        let mut cfg = production_ok();
        cfg.start_max_per_minute = 0;
        let err = throw_if_misconfigured(Env::Production, &cfg).unwrap_err();
        assert!(err.contains("greater than 0"), "{err}");
    }

    #[test]
    fn wrap_key_not_32_bytes() {
        let mut cfg = production_ok();
        cfg.wrap_key = "AAAA".into();
        let err = throw_if_misconfigured(Env::Production, &cfg).unwrap_err();
        assert!(err.contains("Pay:WrapKey must be 32 bytes base64"), "{err}");
    }
}
