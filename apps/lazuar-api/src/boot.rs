//! Port of `Hosting/PayBoot.cs` — fail-closed production configuration guards.
//!
//! Every guard is skipped in Development/Testing (C# `PayBoot.cs:10-13`) except
//! B4 (One key family — always-on, `Program.cs:110`). Exact C# exception
//! messages are preserved as the error strings: a failed boot must say the
//! same thing the .NET host would.

use crate::config::Config;

#[derive(Debug, thiserror::Error)]
#[error("{0}")]
pub struct BootError(pub String);

/// Guard helper: fires `message` only when the environment is strict
/// (Production/Staging) and the condition failed.
fn check(env_strict: bool, condition: bool, message: &'static str) -> Result<(), BootError> {
    if env_strict && !condition {
        Err(BootError(message.into()))
    } else {
        Ok(())
    }
}

/// Always-on key-family validation (`OneWorkerClient.ThrowIfInvalid`,
/// `Program.cs:110`) — runs in every environment.
fn validate_worker_key(config: &Config) -> Result<(), BootError> {
    let Some(api_key) = config.one_api_key.as_deref().map(str::trim).filter(|k| !k.is_empty()) else {
        return Ok(());
    };
    if api_key.starts_with("sk_") {
        return Err(BootError("One:ApiKey must be a One lzr_sk_ key, not sk_".into()));
    }
    if !api_key.starts_with("lzr_sk_") {
        return Err(BootError("One:ApiKey must start with lzr_sk_".into()));
    }
    match config.one_worker_org_id.as_deref().map(str::trim).filter(|s| !s.is_empty()) {
        Some(_) => Ok(()),
        None => Err(BootError("One:WorkerOrgId is required when One:ApiKey is set".into())),
    }
}

/// The full boot validation. B4 is always-on; B1–B3 and B6–B14 apply outside
/// Development/Testing.
pub fn run(config: &Config) -> Result<(), BootError> {
    validate_worker_key(config)?;

    let env = config.environment.as_str();
    let strict = env != "Development" && env != "Testing";

    // B1 + B5: WrapKey present and exactly 32 bytes base64.
    let wrap_key_present = config.wrap_key.as_deref().map(str::trim).filter(|k| !k.is_empty()).is_some();
    check(strict, wrap_key_present, "Pay:WrapKey is required")?;
    if let Some(key) = config.wrap_key.as_deref().map(str::trim).filter(|k| !k.is_empty()) {
        use base64::Engine as _;
        let decoded = base64::engine::general_purpose::STANDARD
            .decode(key)
            .map_err(|_| BootError("Pay:WrapKey must be 32 bytes base64".into()))?;
        check(strict, decoded.len() == 32, "Pay:WrapKey must be 32 bytes base64")?;
    }

    // B2: connection string required (strict envs).
    check(
        strict,
        config.connection_string.as_deref().map(str::trim).filter(|s| !s.is_empty()).is_some(),
        "ConnectionStrings:Pay is required",
    )?;

    // B3: One:BaseUrl must be a public URL (no localhost/127.0.0.1).
    let one_url = config.one_base_url.trim();
    check(
        strict,
        !one_url.is_empty() && !one_url.contains("localhost") && !one_url.contains("127.0.0.1"),
        "One:BaseUrl must be a public URL in Production and Staging",
    )?;

    // B6: CheckoutBaseUrl must be public https.
    check(
        strict,
        config.checkout_base_url.starts_with("https://"),
        "Pay:CheckoutBaseUrl must be public https in Production and Staging",
    )?;

    // B7: CORS origins configured outside Dev/Testing.
    check(strict, !config.cors_origins.is_empty(), "Pay:CorsOrigins must be configured in Production and Staging.")?;

    // B8: Production CORS origins must all be https.
    if env == "Production" {
        let all_https = config.cors_origins.iter().all(|o| o.starts_with("https://"));
        check(env == "Production", all_https, "Pay:CorsOrigins must be https in Production")?;
    }

    // B9: the checkout origin must be covered by a CORS origin.
    let origin = config
        .checkout_base_url
        .split("//")
        .nth(1)
        .and_then(|rest| rest.split('/').next())
        .unwrap_or("")
        .to_string();
    if !origin.is_empty() {
        let covered = config.cors_origins.iter().any(|o| {
            let authority = o
                .split("//")
                .nth(1)
                .and_then(|rest| rest.split('/').next())
                .unwrap_or("");
            authority.eq_ignore_ascii_case(&origin)
        });
        check(strict, covered, "Pay:CheckoutBaseUrl origin must be in Pay:CorsOrigins")?;
    }

    // B10: limiter positive.
    check(strict, config.start_max_per_minute > 0, "Pay:StartMaxPerMinute must be greater than 0")?;

    // B11: when an RPC URL is configured the cluster must be pinned.
    if !config.solana_rpc_url.trim().is_empty() {
        let valid = config.solana_cluster == "mainnet-beta" || config.solana_cluster == "devnet";
        check(strict, valid, "Pay:Solana:Cluster must be mainnet-beta or devnet")?;
    }

    // B12: Production requires the mainnet cluster.
    if env == "Production" {
        check(
            env == "Production",
            config.solana_cluster == "mainnet-beta",
            "Pay:Solana:Cluster must be mainnet-beta in Production",
        )?;
    }

    // B13: the RPC URL must be a public https endpoint.
    if !config.solana_rpc_url.trim().is_empty() {
        let url = &config.solana_rpc_url;
        let url_ok = url.starts_with("https://")
            && !url.contains("localhost")
            && !url.contains("127.0.0.1")
            && !url.contains("VITE_")
            && !url.contains("api.mainnet-beta.solana.com")
            && !url.contains("api.devnet.solana.com");
        check(strict, url_ok, "Pay:Solana:RpcUrl must be a public https RPC")?;
    }

    // B14: cluster/RPC cross-check.
    if !config.solana_rpc_url.trim().is_empty() {
        let wrong = (config.solana_cluster == "mainnet-beta" && config.solana_rpc_url.contains("devnet"))
            || (config.solana_cluster == "devnet" && config.solana_rpc_url.contains("mainnet"));
        check(strict, !wrong, "Pay:Solana:RpcUrl genesis hash mismatch")?;
    }

    Ok(())
}

/// B15: live genesis probe (`PayBoot.ProbeSolanaRpcAsync`) — Production/Staging
/// only, and only when an RPC URL is configured. A wrong hash fails the boot:
/// the watcher would otherwise confirm payments against the wrong chain.
pub fn probe_solana_rpc(config: &Config) -> Result<(), BootError> {
    if config.solana_rpc_url.trim().is_empty()
        || config.environment == "Development"
        || config.environment == "Testing"
    {
        return Ok(());
    }
    let transport = crate::transport::UreqTransport::new(10);
    let rpc = crate::rails::solana::rpc::SolanaRpc {
        rpc_url: Some(config.solana_rpc_url.clone()),
        transport: Box::new(transport),
    };
    let cluster = crate::rails::solana::cluster::from_config(Some(&config.solana_cluster));
    let hash = rpc
        .get_genesis_hash()
        .map_err(|e| BootError(format!("Pay:Solana:RpcUrl is unreachable ({e})")))?;
    if hash != crate::rails::solana::cluster::genesis_hash(cluster) {
        return Err(BootError("Pay:Solana:RpcUrl genesis hash mismatch".into()));
    }
    Ok(())
}
