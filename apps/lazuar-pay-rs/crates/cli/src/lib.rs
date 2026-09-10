//! `lazuar-pay` — TypeSpec `/v1` CLI. Talks HTTP only (035/02). Never imports storage.

#![forbid(unsafe_code)]

use clap::Subcommand;
use pay_client::{env_first, validate_gateway_put, CheckoutExtras, Client, Config, Error};
use rust_decimal::Decimal;
use serde_json::Value;
use std::path::{Path, PathBuf};
use std::str::FromStr;

/// Re-export so integration tests can `try_parse_from` without a clap dev-dep.
pub use clap::Parser;

/// Merchant client of focused Pay `:8081`. The money host is `lazuar-pay-rs serve`.
#[derive(Debug, Parser)]
#[command(
    name = "lazuar-pay",
    about = "Lazuar Pay /v1 client (HTTP). Not the payment host.",
    version
)]
pub struct Cli {
    /// Pay origin, no trailing slash. Env `LAZUAR_PAY_BASE_URL` or `PAY_API_URL`.
    #[arg(long, env = "LAZUAR_PAY_BASE_URL")]
    pub base_url: Option<String>,
    /// One `lzr_sk_…` (or Testing `test-writer`). Env `LAZUAR_PAY_API_KEY` or `PAY_API_KEY`.
    // 036/006 #1: hide_env_values so `--help` does not print the key.
    #[arg(long, env = "LAZUAR_PAY_API_KEY", hide_env_values = true)]
    pub api_key: Option<String>,
    /// One tenant id. Env `LAZUAR_PAY_ORG_ID` or `PAY_ORG_ID`.
    #[arg(long, env = "LAZUAR_PAY_ORG_ID")]
    pub org_id: Option<String>,
    /// One-line JSON on stdout (036/006 #10). Default is pretty-print.
    #[arg(long, global = true)]
    pub compact: bool,
    /// Success is exit 0 with empty stdout (036/006 #10). Errors still stderr JSON.
    #[arg(long, global = true)]
    pub quiet: bool,
    #[command(subcommand)]
    pub command: Command,
}

#[derive(Debug, Subcommand)]
pub enum Command {
    /// `GET /v1/whoami`
    Whoami,
    /// `GET /v1/orgs/{orgId}/ready`
    Ready,
    #[command(subcommand)]
    Checkout(CheckoutCmd),
    #[command(subcommand)]
    Refund(RefundCmd),
    /// Occupancy mint (SPA Pay links).
    #[command(name = "payment-link", subcommand)]
    PaymentLink(PaymentLinkCmd),
    /// `GET /v1/orgs/{orgId}/payments` — `list` matches `receipts list` (036/006 #12).
    #[command(subcommand)]
    Payments(PaymentsCmd),
    #[command(subcommand)]
    Receipts(ReceiptsCmd),
    /// BYOK vault. Write only via `--file` (035/03). No `--secret` flags.
    #[command(subcommand)]
    Gateway(GatewayCmd),
    /// Plane C org webhook (dashboard Webhooks page).
    #[command(subcommand)]
    Webhook(WebhookCmd),
    /// MYR one-off catalog (SPA creates a product then a payment-link).
    #[command(subcommand)]
    Product(ProductCmd),
}

#[derive(Debug, Subcommand)]
pub enum CheckoutCmd {
    /// `POST /v1/checkouts`. `--provider` is required (no silent test default).
    Create {
        /// stripe|chip|billplz|xendit|razorpay|solana|test. `solana` is USDC only.
        #[arg(long)]
        provider: String,
        /// Decimal string, at most 2 display places. Never parsed as f64.
        #[arg(long)]
        amount: String,
        /// Fiat default MYR. `solana` requires USDC (not MYR/USD) — 036/006 #14.
        #[arg(long, default_value = "MYR")]
        currency: String,
        /// Required so a retry does not mint a second charge (036/006 #7).
        #[arg(long)]
        idempotency_key: String,
        /// Buyer return URL after pay (TypeSpec; host persists).
        #[arg(long)]
        success_url: Option<String>,
        /// Buyer return URL on cancel (TypeSpec; host persists).
        #[arg(long)]
        cancel_url: Option<String>,
        /// TypeSpec optional. Catalog mint uses `payment-link create --product-id`.
        #[arg(long)]
        product_id: Option<String>,
    },
    /// `GET /v1/checkouts/{id}`
    Get { id: String },
    /// `GET /v1/orgs/{orgId}/checkouts`
    List {
        #[arg(long)]
        limit: Option<u32>,
        #[arg(long)]
        after: Option<String>,
    },
    /// Poll GET until wire status matches `--until` (036/006 #4). Not a buyer start.
    Wait {
        id: String,
        #[arg(long, default_value = "paid")]
        until: String,
        #[arg(long, default_value_t = 900)]
        timeout_secs: u64,
        #[arg(long, default_value_t = 500)]
        interval_ms: u64,
    },
}

#[derive(Debug, Subcommand)]
pub enum RefundCmd {
    /// `POST /v1/orgs/{orgId}/refunds`
    Create {
        #[arg(long)]
        checkout: String,
        #[arg(long)]
        amount: Option<String>,
        #[arg(long)]
        idempotency_key: String,
    },
    /// `GET /v1/orgs/{orgId}/refunds`
    List {
        #[arg(long)]
        limit: Option<u32>,
        #[arg(long)]
        after: Option<String>,
    },
}

#[derive(Debug, Subcommand)]
pub enum PaymentLinkCmd {
    /// `POST /v1/payment-links`
    Create {
        /// stripe|chip|billplz|xendit|razorpay|solana|test. `solana` is USDC only.
        #[arg(long)]
        provider: String,
        #[arg(long)]
        amount: String,
        /// Fiat default MYR. `solana` requires USDC (not MYR/USD) — 036/006 #14.
        #[arg(long, default_value = "MYR")]
        currency: String,
        #[arg(long)]
        max_payers: Option<i32>,
        #[arg(long)]
        unlimited: bool,
        #[arg(long)]
        label: Option<String>,
        /// Catalog product (SPA: product create then link).
        #[arg(long)]
        product_id: Option<String>,
    },
    /// `GET /v1/orgs/{orgId}/payment-links`
    List {
        #[arg(long)]
        limit: Option<u32>,
        #[arg(long)]
        after: Option<String>,
    },
}

#[derive(Debug, Subcommand)]
pub enum PaymentsCmd {
    /// `GET /v1/orgs/{orgId}/payments`
    List {
        #[arg(long)]
        limit: Option<u32>,
        #[arg(long)]
        after: Option<String>,
    },
}

#[derive(Debug, Subcommand)]
pub enum ReceiptsCmd {
    /// `GET /v1/orgs/{orgId}/receipts`
    List {
        #[arg(long)]
        limit: Option<u32>,
        #[arg(long)]
        after: Option<String>,
    },
    /// `GET /v1/orgs/{orgId}/receipts/{id}`
    Get { id: String },
}

#[derive(Debug, Subcommand)]
pub enum GatewayCmd {
    /// `PUT /v1/orgs/{orgId}/gateway`. Secrets stay in the file, not argv / MCP args.
    Put {
        #[arg(long, value_name = "PATH")]
        file: PathBuf,
    },
    /// `GET /v1/orgs/{orgId}/gateway?provider=`
    Get {
        #[arg(long)]
        provider: String,
    },
    /// `GET /v1/orgs/{orgId}/gateways`
    List,
}

#[derive(Debug, Subcommand)]
pub enum WebhookCmd {
    /// `PUT /v1/orgs/{orgId}/webhooks`. URL only — host mints `whsec_`.
    Put {
        #[arg(long)]
        url: String,
    },
    /// `GET /v1/orgs/{orgId}/webhooks` (no secret, prefix only).
    Get,
    /// `POST /v1/orgs/{orgId}/webhooks/rotate` — new `whsec_` once.
    Rotate,
    /// `POST /v1/orgs/{orgId}/webhooks/test` — enqueue `webhook.test`.
    Test,
}

#[derive(Debug, Subcommand)]
pub enum ProductCmd {
    /// `POST /v1/orgs/{orgId}/products`. Currency is MYR; no recurring interval.
    Create {
        #[arg(long)]
        name: String,
        #[arg(long)]
        amount: String,
        #[arg(long, default_value = "MYR")]
        currency: String,
        #[arg(long)]
        description: Option<String>,
    },
    /// `GET /v1/orgs/{orgId}/products`
    List {
        #[arg(long)]
        limit: Option<u32>,
        #[arg(long)]
        after: Option<String>,
    },
}

pub fn config_from_cli(cli: &Cli) -> Result<Config, Error> {
    // clap reads LAZUAR_PAY_*; pay-node aliases fill the rest (036/006 #3).
    Config::from_parts(
        nonempty(cli.base_url.clone()).or_else(|| env_first(&["PAY_API_URL"])),
        nonempty(cli.api_key.clone()).or_else(|| env_first(&["PAY_API_KEY"])),
        nonempty(cli.org_id.clone()).or_else(|| env_first(&["PAY_ORG_ID"])),
    )
}

/// `sk_live` JSON at mode 0644 must not be accepted (036/006 #21). Unix only.
fn refuse_world_readable(path: &Path) -> Result<(), Error> {
    #[cfg(unix)]
    {
        use std::os::unix::fs::PermissionsExt;
        let mode = std::fs::metadata(path)
            .map_err(|e| Error::Config(format!("cannot read {}: {e}", path.display())))?
            .permissions()
            .mode();
        if mode & 0o077 != 0 {
            return Err(Error::Config(format!(
                "{} is group/world-readable; chmod 600 and retry",
                path.display()
            )));
        }
    }
    let _ = path;
    Ok(())
}

fn nonempty(s: Option<String>) -> Option<String> {
    s.map(|v| v.trim().to_string()).filter(|v| !v.is_empty())
}

/// `--quiet` wins. `--compact` is one line. Neither → pretty JSON (036/006 #10).
pub fn stdout_json(body: &Value, compact: bool, quiet: bool) -> Option<String> {
    if quiet {
        return None;
    }
    if compact {
        serde_json::to_string(body).ok()
    } else {
        serde_json::to_string_pretty(body).ok()
    }
}

/// `--api-key` on argv lands in `ps` / shell history (036/006 #22). Prefer env.
pub fn api_key_flag_on_argv<I, S>(args: I) -> bool
where
    I: IntoIterator<Item = S>,
    S: AsRef<str>,
{
    args.into_iter().any(|a| a.as_ref() == "--api-key")
}

pub async fn run(cli: Cli) -> Result<Value, Error> {
    let cfg = config_from_cli(&cli)?;
    let client = Client::new(cfg)?;
    match cli.command {
        Command::Whoami => client.whoami().await,
        Command::Ready => client.ready().await,
        Command::Checkout(CheckoutCmd::Create {
            provider,
            amount,
            currency,
            idempotency_key,
            success_url,
            cancel_url,
            product_id,
        }) => {
            let amount = parse_amount(&amount)?;
            client
                .checkout_create(
                    &provider,
                    amount,
                    &currency,
                    &idempotency_key,
                    CheckoutExtras {
                        success_url: success_url.as_deref(),
                        cancel_url: cancel_url.as_deref(),
                        product_id: product_id.as_deref(),
                    },
                )
                .await
        }
        Command::Checkout(CheckoutCmd::Get { id }) => client.checkout_get(&id).await,
        Command::Checkout(CheckoutCmd::List { limit, after }) => {
            client.checkout_list(limit, after.as_deref()).await
        }
        Command::Checkout(CheckoutCmd::Wait {
            id,
            until,
            timeout_secs,
            interval_ms,
        }) => {
            client
                .checkout_wait(
                    &id,
                    &until,
                    std::time::Duration::from_secs(timeout_secs),
                    std::time::Duration::from_millis(interval_ms),
                )
                .await
        }
        Command::Refund(RefundCmd::Create {
            checkout,
            amount,
            idempotency_key,
        }) => {
            let amount = match amount {
                Some(a) => Some(parse_amount(&a)?),
                None => None,
            };
            client
                .refund_create(&checkout, amount, &idempotency_key)
                .await
        }
        Command::Refund(RefundCmd::List { limit, after }) => {
            client.refund_list(limit, after.as_deref()).await
        }
        Command::PaymentLink(PaymentLinkCmd::Create {
            provider,
            amount,
            currency,
            max_payers,
            unlimited,
            label,
            product_id,
        }) => {
            let amount = parse_amount(&amount)?;
            client
                .payment_link_create(
                    &provider,
                    amount,
                    &currency,
                    max_payers,
                    unlimited,
                    label.as_deref(),
                    product_id.as_deref(),
                )
                .await
        }
        Command::PaymentLink(PaymentLinkCmd::List { limit, after }) => {
            client.payment_link_list(limit, after.as_deref()).await
        }
        Command::Payments(PaymentsCmd::List { limit, after }) => {
            client.payments_list(limit, after.as_deref()).await
        }
        Command::Receipts(ReceiptsCmd::List { limit, after }) => {
            client.receipts_list(limit, after.as_deref()).await
        }
        Command::Receipts(ReceiptsCmd::Get { id }) => client.receipts_get(&id).await,
        Command::Gateway(GatewayCmd::Put { file }) => {
            let body = read_gateway_file(&file)?;
            client.gateway_put(body).await
        }
        Command::Gateway(GatewayCmd::Get { provider }) => client.gateway_get(&provider).await,
        Command::Gateway(GatewayCmd::List) => client.gateway_list().await,
        Command::Webhook(WebhookCmd::Put { url }) => client.webhook_put(&url).await,
        Command::Webhook(WebhookCmd::Get) => client.webhook_get().await,
        Command::Webhook(WebhookCmd::Rotate) => client.webhook_rotate().await,
        Command::Webhook(WebhookCmd::Test) => client.webhook_test().await,
        Command::Product(ProductCmd::Create {
            name,
            amount,
            currency,
            description,
        }) => {
            let amount = parse_amount(&amount)?;
            client
                .product_create(&name, amount, &currency, description.as_deref())
                .await
        }
        Command::Product(ProductCmd::List { limit, after }) => {
            client.product_list(limit, after.as_deref()).await
        }
    }
}

/// Read PutGateway JSON. Errors name the path, never the file bytes (sk_ / PEM).
pub fn read_gateway_file(path: &Path) -> Result<Value, Error> {
    refuse_world_readable(path)?;
    let raw = std::fs::read_to_string(path)
        .map_err(|e| Error::Config(format!("cannot read {}: {e}", path.display())))?;
    let body: Value = serde_json::from_str(&raw)
        .map_err(|_| Error::Config(format!("{} is not JSON", path.display())))?;
    validate_gateway_put(&body)?;
    Ok(body)
}

fn parse_amount(raw: &str) -> Result<Decimal, Error> {
    let s = raw.trim();
    Decimal::from_str(s)
        .map_err(|_| Error::Config(format!("amount must be a decimal, got {raw:?}")))
        .and_then(|d| {
            // Host refuses >2 display decimals (issue 003 / quoted display).
            if d.round_dp(2) != d {
                return Err(Error::Config(
                    "amount must have at most 2 decimal places".into(),
                ));
            }
            if d <= Decimal::ZERO {
                return Err(Error::Config("amount must be greater than 0".into()));
            }
            Ok(d)
        })
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::Mutex;

    static ENV_LOCK: Mutex<()> = Mutex::new(());

    fn restore_env(key: &str, prev: Option<String>) {
        match prev {
            Some(v) => std::env::set_var(key, v),
            None => std::env::remove_var(key),
        }
    }

    #[test]
    fn create_requires_provider_amount_and_idempotency() {
        let err = Cli::try_parse_from(["lazuar-pay", "checkout", "create"]).unwrap_err();
        let msg = err.to_string();
        assert!(
            msg.contains("provider") || msg.contains("required"),
            "{msg}"
        );
        let err = Cli::try_parse_from([
            "lazuar-pay",
            "checkout",
            "create",
            "--provider",
            "test",
            "--amount",
            "10",
        ])
        .unwrap_err();
        assert!(
            err.to_string().contains("idempotency-key"),
            "{}",
            err.to_string()
        );
    }

    #[test]
    fn create_parses_flags() {
        let cli = Cli::try_parse_from([
            "lazuar-pay",
            "--base-url",
            "http://127.0.0.1:8081",
            "--api-key",
            "lzr_sk_test",
            "--org-id",
            "t1",
            "checkout",
            "create",
            "--provider",
            "test",
            "--amount",
            "10.00",
            "--idempotency-key",
            "k1",
            "--success-url",
            "https://app.example/ok",
            "--cancel-url",
            "https://app.example/no",
            "--product-id",
            "prod_1",
        ])
        .unwrap();
        match cli.command {
            Command::Checkout(CheckoutCmd::Create {
                provider,
                amount,
                success_url,
                cancel_url,
                product_id,
                ..
            }) => {
                assert_eq!(provider, "test");
                assert_eq!(amount, "10.00");
                assert_eq!(success_url.as_deref(), Some("https://app.example/ok"));
                assert_eq!(cancel_url.as_deref(), Some("https://app.example/no"));
                assert_eq!(product_id.as_deref(), Some("prod_1"));
            }
            other => panic!("{other:?}"),
        }
    }

    #[test]
    fn sk_key_rejected_before_http() {
        let cli = Cli::try_parse_from([
            "lazuar-pay",
            "--api-key",
            "sk_test_abc",
            "--org-id",
            "t1",
            "whoami",
        ])
        .unwrap();
        let err = config_from_cli(&cli).unwrap_err();
        assert!(err.to_string().contains("lzr_sk_"), "{err}");
    }

    #[test]
    fn parse_amount_rejects_three_decimals() {
        let err = parse_amount("10.001").unwrap_err();
        assert!(err.to_string().contains("2 decimal"), "{err}");
    }

    #[test]
    fn parse_amount_rejects_zero() {
        assert!(parse_amount("0").is_err());
        assert!(parse_amount("-1").is_err());
    }

    #[test]
    fn api_key_flag_on_argv_detects_long_flag() {
        assert!(api_key_flag_on_argv([
            "lazuar-pay",
            "--api-key",
            "lzr_sk_x",
            "whoami"
        ]));
        assert!(!api_key_flag_on_argv(["lazuar-pay", "whoami"]));
        assert!(!api_key_flag_on_argv([
            "lazuar-pay",
            "--org-id",
            "t1",
            "whoami"
        ]));
    }

    #[test]
    fn compact_and_quiet_are_global_flags() {
        let cli = Cli::try_parse_from(["lazuar-pay", "--quiet", "whoami"]).unwrap();
        assert!(cli.quiet);
        assert!(!cli.compact);
        let cli = Cli::try_parse_from(["lazuar-pay", "ready", "--compact"]).unwrap();
        assert!(cli.compact);
        assert!(!cli.quiet);
        let cli = Cli::try_parse_from(["lazuar-pay", "--compact", "--quiet", "whoami"]).unwrap();
        assert!(cli.compact && cli.quiet);
    }

    #[test]
    fn stdout_json_quiet_wins_then_compact() {
        let body = serde_json::json!({"ready": true});
        assert!(stdout_json(&body, false, true).is_none());
        assert!(stdout_json(&body, true, true).is_none());
        let compact = stdout_json(&body, true, false).unwrap();
        assert!(!compact.contains('\n'), "{compact}");
        assert!(compact.contains("\"ready\":true") || compact.contains("\"ready\": true"));
        let pretty = stdout_json(&body, false, false).unwrap();
        assert!(pretty.contains('\n'), "{pretty}");
    }

    #[test]
    fn gateway_put_requires_file_not_secret_flag() {
        let err = Cli::try_parse_from(["lazuar-pay", "gateway", "put", "--secret", "sk_test_x"])
            .unwrap_err();
        let msg = err.to_string();
        assert!(
            msg.contains("unexpected") || msg.contains("file") || msg.contains("required"),
            "{msg}"
        );
        assert!(Cli::try_parse_from(["lazuar-pay", "gateway", "put"]).is_err());
    }

    #[test]
    fn gateway_put_parses_file_flag() {
        let cli = Cli::try_parse_from([
            "lazuar-pay",
            "--api-key",
            "lzr_sk_test",
            "--org-id",
            "t1",
            "gateway",
            "put",
            "--file",
            "/tmp/stripe.json",
        ])
        .unwrap();
        match cli.command {
            Command::Gateway(GatewayCmd::Put { file }) => {
                assert_eq!(file, PathBuf::from("/tmp/stripe.json"));
            }
            other => panic!("{other:?}"),
        }
    }

    #[test]
    fn help_hides_api_key_value() {
        let _g = ENV_LOCK.lock().expect("env lock");
        const SENTINEL: &str = "lzr_sk_live_sentinel_do_not_print";
        let prev = std::env::var("LAZUAR_PAY_API_KEY").ok();
        std::env::set_var("LAZUAR_PAY_API_KEY", SENTINEL);
        let err = Cli::try_parse_from(["lazuar-pay", "--help"]).unwrap_err();
        let msg = err.to_string();
        restore_env("LAZUAR_PAY_API_KEY", prev);
        assert!(msg.contains("--api-key"), "{msg}");
        assert!(msg.contains("LAZUAR_PAY_API_KEY"), "{msg}");
        // clap would otherwise render `[env: LAZUAR_PAY_API_KEY=lzr_sk_…]`.
        assert!(!msg.contains(SENTINEL), "{msg}");
        assert!(!msg.contains("LAZUAR_PAY_API_KEY="), "{msg}");
    }

    #[test]
    fn pay_aliases_fill_when_canonical_missing() {
        let _g = ENV_LOCK.lock().expect("env lock");
        let prev_key = std::env::var("PAY_API_KEY").ok();
        let prev_org = std::env::var("PAY_ORG_ID").ok();
        let prev_url = std::env::var("PAY_API_URL").ok();
        std::env::set_var("PAY_API_KEY", "lzr_sk_alias");
        std::env::set_var("PAY_ORG_ID", "org-alias");
        std::env::set_var("PAY_API_URL", "http://127.0.0.1:9");
        let cli = Cli {
            base_url: None,
            api_key: None,
            org_id: None,
            compact: false,
            quiet: false,
            command: Command::Whoami,
        };
        let cfg = config_from_cli(&cli);
        restore_env("PAY_API_KEY", prev_key);
        restore_env("PAY_ORG_ID", prev_org);
        restore_env("PAY_API_URL", prev_url);
        let cfg = cfg.expect("PAY_* aliases");
        assert_eq!(cfg.api_key, "lzr_sk_alias");
        assert_eq!(cfg.org_id.as_deref(), Some("org-alias"));
        assert_eq!(cfg.base_url, "http://127.0.0.1:9");
    }

    #[test]
    fn product_create_parses_name_and_amount() {
        let cli = Cli::try_parse_from([
            "lazuar-pay",
            "product",
            "create",
            "--name",
            "Seat",
            "--amount",
            "10.00",
        ])
        .unwrap();
        match cli.command {
            Command::Product(ProductCmd::Create { name, amount, .. }) => {
                assert_eq!(name, "Seat");
                assert_eq!(amount, "10.00");
            }
            other => panic!("{other:?}"),
        }
        assert!(Cli::try_parse_from(["lazuar-pay", "product", "list"]).is_ok());
    }

    #[test]
    fn list_subcommands_parse() {
        let link =
            Cli::try_parse_from(["lazuar-pay", "payment-link", "list", "--limit", "5"]).unwrap();
        match link.command {
            Command::PaymentLink(PaymentLinkCmd::List { limit, .. }) => {
                assert_eq!(limit, Some(5));
            }
            other => panic!("{other:?}"),
        }
        let refunds = Cli::try_parse_from(["lazuar-pay", "refund", "list"]).unwrap();
        match refunds.command {
            Command::Refund(RefundCmd::List { .. }) => {}
            other => panic!("{other:?}"),
        }
        let checkouts = Cli::try_parse_from(["lazuar-pay", "checkout", "list"]).unwrap();
        match checkouts.command {
            Command::Checkout(CheckoutCmd::List { .. }) => {}
            other => panic!("{other:?}"),
        }
    }

    #[test]
    fn refund_create_requires_idempotency() {
        let err = Cli::try_parse_from(["lazuar-pay", "refund", "create", "--checkout", "abc"])
            .unwrap_err();
        assert!(
            err.to_string().contains("idempotency-key"),
            "{}",
            err.to_string()
        );
    }

    #[test]
    fn webhook_put_requires_url_not_secret() {
        let err = Cli::try_parse_from(["lazuar-pay", "webhook", "put"]).unwrap_err();
        assert!(err.to_string().contains("url"), "{}", err.to_string());
        let err = Cli::try_parse_from(["lazuar-pay", "webhook", "put", "--secret", "whsec_x"])
            .unwrap_err();
        assert!(
            err.to_string().contains("unexpected") || err.to_string().contains("url"),
            "{}",
            err.to_string()
        );
        let cli = Cli::try_parse_from([
            "lazuar-pay",
            "webhook",
            "put",
            "--url",
            "http://127.0.0.1:9/hook",
        ])
        .unwrap();
        match cli.command {
            Command::Webhook(WebhookCmd::Put { url }) => {
                assert_eq!(url, "http://127.0.0.1:9/hook");
            }
            other => panic!("{other:?}"),
        }
    }

    #[test]
    fn checkout_create_help_states_solana_usdc() {
        let err = Cli::try_parse_from(["lazuar-pay", "checkout", "create", "--help"]).unwrap_err();
        let msg = err.to_string();
        assert!(msg.contains("USDC"), "{msg}");
        assert!(msg.contains("solana"), "{msg}");
        assert!(msg.contains("MYR"), "{msg}");
    }

    #[test]
    fn payments_list_matches_receipts_shape() {
        let err = Cli::try_parse_from(["lazuar-pay", "payments"]).unwrap_err();
        let msg = err.to_string();
        assert!(msg.contains("list") || msg.contains("required"), "{msg}");
        let cli = Cli::try_parse_from([
            "lazuar-pay",
            "payments",
            "list",
            "--limit",
            "10",
            "--after",
            "abc",
        ])
        .unwrap();
        match cli.command {
            Command::Payments(PaymentsCmd::List { limit, after }) => {
                assert_eq!(limit, Some(10));
                assert_eq!(after.as_deref(), Some("abc"));
            }
            other => panic!("{other:?}"),
        }
    }

    #[test]
    fn unlimited_is_presence_flag() {
        let on = Cli::try_parse_from([
            "lazuar-pay",
            "payment-link",
            "create",
            "--provider",
            "test",
            "--amount",
            "10",
            "--unlimited",
        ])
        .unwrap();
        match on.command {
            Command::PaymentLink(PaymentLinkCmd::Create { unlimited, .. }) => {
                assert!(unlimited);
            }
            other => panic!("{other:?}"),
        }
        let off = Cli::try_parse_from([
            "lazuar-pay",
            "payment-link",
            "create",
            "--provider",
            "test",
            "--amount",
            "10",
        ])
        .unwrap();
        match off.command {
            Command::PaymentLink(PaymentLinkCmd::Create { unlimited, .. }) => {
                assert!(!unlimited);
            }
            other => panic!("{other:?}"),
        }
        let err = Cli::try_parse_from([
            "lazuar-pay",
            "payment-link",
            "create",
            "--provider",
            "test",
            "--amount",
            "10",
            "--unlimited",
            "true",
        ])
        .unwrap_err();
        assert!(
            err.to_string().contains("unexpected argument"),
            "{}",
            err.to_string()
        );
    }

    #[test]
    fn read_gateway_file_rejects_test_without_echoing_secret() {
        let path =
            std::env::temp_dir().join(format!("lazuar-pay-gw-test-{}.json", std::process::id()));
        std::fs::write(
            &path,
            r#"{"provider":"test","secret":"sk_should_not_leak","webhook_secret":"x"}"#,
        )
        .unwrap();
        chmod_owner_rw(&path);
        let err = read_gateway_file(&path).unwrap_err();
        let msg = err.to_string();
        assert!(msg.contains("test processor"), "{msg}");
        assert!(!msg.contains("sk_should_not_leak"), "{msg}");
        let _ = std::fs::remove_file(&path);
    }

    #[cfg(unix)]
    fn chmod_owner_rw(path: &std::path::Path) {
        use std::os::unix::fs::PermissionsExt;
        let mut p = std::fs::metadata(path).unwrap().permissions();
        p.set_mode(0o600);
        std::fs::set_permissions(path, p).unwrap();
    }

    #[cfg(not(unix))]
    fn chmod_owner_rw(_: &std::path::Path) {}

    #[cfg(unix)]
    #[test]
    fn read_gateway_file_rejects_world_readable() {
        use std::os::unix::fs::PermissionsExt;
        let path =
            std::env::temp_dir().join(format!("lazuar-pay-gw-mode-{}.json", std::process::id()));
        std::fs::write(
            &path,
            r#"{"provider":"stripe","secret":"sk_live_should_not_leak","webhook_secret":"whsec_x","environment":"live"}"#,
        )
        .unwrap();
        let mut p = std::fs::metadata(&path).unwrap().permissions();
        p.set_mode(0o644);
        std::fs::set_permissions(&path, p).unwrap();
        let err = read_gateway_file(&path).unwrap_err();
        let msg = err.to_string();
        assert!(
            msg.contains("chmod 600") || msg.contains("world-readable"),
            "{msg}"
        );
        assert!(!msg.contains("sk_live_should_not_leak"), "{msg}");
        let _ = std::fs::remove_file(&path);
    }
}
