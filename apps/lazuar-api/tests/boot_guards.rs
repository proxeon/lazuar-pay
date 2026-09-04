//! Phase 1.1 — PayBoot B1–B14 fire with C# messages.

use lazuar_api::boot;
use lazuar_api::config::Config;

fn production_ok() -> Config {
    let mut c = Config::from_env();
    c.environment = "Production".into();
    c.wrap_key = Some("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=".into());
    c.connection_string = Some("Host=db".into());
    c.one_base_url = "https://one.example/api/v1".into();
    c.checkout_base_url = "https://checkout.example".into();
    c.cors_origins = vec!["https://checkout.example".into()];
    c.solana_rpc_url = String::new();
    c.solana_cluster = "devnet".into();
    c.start_max_per_minute = 20;
    c.one_api_key = None;
    c
}

#[test]
fn production_empty_wrap_key_throws() {
    let mut c = production_ok();
    c.wrap_key = None;
    let err = boot::run(&c).unwrap_err();
    assert!(err.0.contains("WrapKey"), "{err}");
}

#[test]
fn production_empty_cs_throws() {
    let mut c = production_ok();
    c.connection_string = None;
    let err = boot::run(&c).unwrap_err();
    assert!(err.0.contains("ConnectionStrings:Pay"), "{err}");
}

#[test]
fn production_localhost_one_url_throws() {
    let mut c = production_ok();
    c.one_base_url = "http://localhost:8080/api/v1".into();
    let err = boot::run(&c).unwrap_err();
    assert!(err.0.contains("One:BaseUrl"), "{err}");
}

#[test]
fn testing_allows_empty() {
    let mut c = Config::from_env();
    c.environment = "Testing".into();
    c.wrap_key = None;
    c.connection_string = None;
    boot::run(&c).unwrap();
}

#[test]
fn production_without_solana_rpc_does_not_require_cluster() {
    boot::run(&production_ok()).unwrap();
}

#[test]
fn production_devnet_cluster_throws() {
    let mut c = production_ok();
    c.solana_cluster = "devnet".into();
    c.solana_rpc_url = "https://rpc.example/devnet".into();
    let err = boot::run(&c).unwrap_err();
    assert!(err.0.contains("mainnet-beta"), "{err}");
}

#[test]
fn production_public_solana_rpc_throws() {
    let mut c = production_ok();
    c.solana_cluster = "mainnet-beta".into();
    c.solana_rpc_url = "https://api.mainnet-beta.solana.com".into();
    let err = boot::run(&c).unwrap_err();
    assert!(err.0.contains("RpcUrl"), "{err}");
}

#[test]
fn production_checkout_origin_must_be_in_cors() {
    let mut c = production_ok();
    c.cors_origins = vec!["https://merchant.example".into()];
    let err = boot::run(&c).unwrap_err();
    assert!(err.0.contains("CorsOrigins"), "{err}");
}

#[test]
fn production_http_cors_origin_throws() {
    let mut c = production_ok();
    c.cors_origins = vec!["http://checkout.example".into()];
    let err = boot::run(&c).unwrap_err();
    assert!(err.0.contains("https"), "{err}");
}

#[test]
fn worker_key_sk_is_rejected_in_any_env() {
    let mut c = Config::from_env();
    c.environment = "Testing".into();
    c.one_api_key = Some("sk_live_nope".into());
    let err = boot::run(&c).unwrap_err();
    assert!(err.0.contains("lzr_sk_"), "{err}");
}
