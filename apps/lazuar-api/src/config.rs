//! Runtime configuration. Mirrors the `Pay:*` / `One:*` / `ConnectionStrings:*`
//! settings consumed by `apps/lazuar-pay` (see
//! `tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs` for the exact keys
//! the reference tests set).

#[derive(Debug, Clone)]
pub struct Config {
    pub stripe_webhook_secret: String,
    pub test_webhook_secret: String,
    pub one_webhook_secret: String,
    pub public_base_url: String,
    pub checkout_base_url: String,
    /// C# default 20 (`PayBoot.cs:76`).
    pub start_max_per_minute: i64,
    pub solana_rpc_url: String,
    pub solana_cluster: String,
    pub one_api_key: Option<String>,
    pub one_worker_org_id: Option<String>,
    pub one_base_url: String,
    /// C# `OneOptions.TimeoutSeconds` default 5.
    pub one_timeout_secs: u64,
    /// C# `PaymentLinkOccupancy.ReservationTtlMinutes` default 30.
    pub reservation_ttl_minutes: i64,
    pub cors_origins: Vec<String>,
    pub environment: String,
    pub connection_string: Option<String>,
    pub wrap_key: Option<String>,
    pub listen_addr: String,
}

fn env_or(key: &str, default: &str) -> String {
    std::env::var(key).unwrap_or_else(|_| default.to_string())
}

fn env_opt(key: &str) -> Option<String> {
    std::env::var(key).ok().filter(|v| !v.is_empty())
}

impl Config {
    /// Every env key `from_env` reads. `.env.example` must list each one.
    pub const FROM_ENV_KEYS: &'static [&'static str] = &[
        "Pay__StripeWebhookSecret",
        "Pay__TestWebhookSecret",
        "Pay__OneWebhookSecret",
        "Pay__PublicBaseUrl",
        "Pay__CheckoutBaseUrl",
        "Pay__StartMaxPerMinute",
        "Pay__Solana__RpcUrl",
        "Pay__Solana__Cluster",
        "One__ApiKey",
        "One__WorkerOrgId",
        "One__BaseUrl",
        "One__TimeoutSeconds",
        "Pay__ReservationTtlMinutes",
        "Pay__CorsOrigins",
        "ASPNETCORE_ENVIRONMENT",
        "ConnectionStrings__Pay",
        "Pay__WrapKey",
        "LISTEN_ADDR",
    ];

    /// Defaults mirror the reference test factory so a bare `cargo test` needs no env.
    pub fn from_env() -> Self {
        Self {
            stripe_webhook_secret: env_or("Pay__StripeWebhookSecret", "whsec_test_local"),
            test_webhook_secret: env_or("Pay__TestWebhookSecret", "test_whsec_local"),
            one_webhook_secret: env_or("Pay__OneWebhookSecret", ""),
            public_base_url: env_or("Pay__PublicBaseUrl", "https://pay.test.example"),
            checkout_base_url: env_or("Pay__CheckoutBaseUrl", "http://pay-checkout.test.example"),
            // C# default is 20 (`PayBoot.cs:76`) — the earlier 200 was a divergence.
            start_max_per_minute: env_or("Pay__StartMaxPerMinute", "20").parse().unwrap_or(20),
            solana_rpc_url: env_or("Pay__Solana__RpcUrl", "http://solana.test/"),
            solana_cluster: env_or("Pay__Solana__Cluster", "devnet"),
            one_api_key: env_opt("One__ApiKey"),
            one_worker_org_id: env_opt("One__WorkerOrgId"),
            one_base_url: env_or("One__BaseUrl", "http://one.test/api/v1"),
            // C# OneOptions.TimeoutSeconds default 5.
            one_timeout_secs: env_or("One__TimeoutSeconds", "5").parse().unwrap_or(5),
            // C# PaymentLinkOccupancy.ReservationTtlMinutes default 30.
            reservation_ttl_minutes: env_or("Pay__ReservationTtlMinutes", "30")
                .parse()
                .unwrap_or(30),
            cors_origins: env_or("Pay__CorsOrigins", "")
                .split(',')
                .map(str::trim)
                .filter(|s| !s.is_empty())
                .map(str::to_string)
                .collect(),
            environment: env_or("ASPNETCORE_ENVIRONMENT", "Development"),
            // C# GetConnectionString("Pay") binds the `ConnectionStrings__Pay` env key
            // (`Pay__ConnectionString` was a porting bug — 025/04 §7).
            connection_string: env_opt("ConnectionStrings__Pay"),
            wrap_key: env_opt("Pay__WrapKey"),
            listen_addr: env_or("LISTEN_ADDR", "127.0.0.1:8095"),
        }
    }
}
