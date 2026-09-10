//! `lazuar-pay` — TypeSpec `/v1` CLI. Talks HTTP only (035/02). Never imports storage.

#![forbid(unsafe_code)]

use clap::Subcommand;
use pay_client::{env_first, validate_gateway_put, Client, Config, Error};
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
    /// `hide_env_values`: `--help` must not print the key (036/006 #1).
    #[arg(long, env = "LAZUAR_PAY_API_KEY", hide_env_values = true)]
    pub api_key: Option<String>,
    /// One tenant id. Env `LAZUAR_PAY_ORG_ID` or `PAY_ORG_ID`.
    #[arg(long, env = "LAZUAR_PAY_ORG_ID")]
    pub org_id: Option<String>,
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
    /// `GET /v1/orgs/{orgId}/payments`
    Payments {
        #[arg(long)]
        limit: Option<u32>,
        #[arg(long)]
        after: Option<String>,
    },
    #[command(subcommand)]
    Receipts(ReceiptsCmd),
    /// BYOK vault. Write only via `--file` (035/03). No `--secret` flags.
    #[command(subcommand)]
    Gateway(GatewayCmd),
}

#[derive(Debug, Subcommand)]
pub enum CheckoutCmd {
    /// `POST /v1/checkouts`. `--provider` is required (no silent test default).
    Create {
        #[arg(long)]
        provider: String,
        /// Decimal string, at most 2 display places. Never parsed as f64.
        #[arg(long)]
        amount: String,
        #[arg(long, default_value = "MYR")]
        currency: String,
        /// Required so a retry does not mint a second charge (036/006 #7).
        #[arg(long)]
        idempotency_key: String,
    },
    /// `GET /v1/checkouts/{id}`
    Get { id: String },
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
}

#[derive(Debug, Subcommand)]
pub enum PaymentLinkCmd {
    /// `POST /v1/payment-links`
    Create {
        #[arg(long)]
        provider: String,
        #[arg(long)]
        amount: String,
        #[arg(long, default_value = "MYR")]
        currency: String,
        #[arg(long)]
        max_payers: Option<i32>,
        #[arg(long, default_value_t = false)]
        unlimited: bool,
        #[arg(long)]
        label: Option<String>,
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

pub fn config_from_cli(cli: &Cli) -> Result<Config, Error> {
    // clap reads LAZUAR_PAY_*; pay-node aliases fill the rest (036/006 #3).
    Config::from_parts(
        nonempty(cli.base_url.clone()).or_else(|| env_first(&["PAY_API_URL"])),
        nonempty(cli.api_key.clone()).or_else(|| env_first(&["PAY_API_KEY"])),
        nonempty(cli.org_id.clone()).or_else(|| env_first(&["PAY_ORG_ID"])),
    )
}

fn nonempty(s: Option<String>) -> Option<String> {
    s.map(|v| v.trim().to_string()).filter(|v| !v.is_empty())
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
        }) => {
            let amount = parse_amount(&amount)?;
            client
                .checkout_create(&provider, amount, &currency, &idempotency_key)
                .await
        }
        Command::Checkout(CheckoutCmd::Get { id }) => client.checkout_get(&id).await,
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
        Command::PaymentLink(PaymentLinkCmd::Create {
            provider,
            amount,
            currency,
            max_payers,
            unlimited,
            label,
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
                )
                .await
        }
        Command::Payments { limit, after } => client.payments_list(limit, after.as_deref()).await,
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
    }
}

/// Read PutGateway JSON. Errors name the path, never the file bytes (sk_ / PEM).
pub fn read_gateway_file(path: &Path) -> Result<Value, Error> {
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
        ])
        .unwrap();
        match cli.command {
            Command::Checkout(CheckoutCmd::Create {
                provider, amount, ..
            }) => {
                assert_eq!(provider, "test");
                assert_eq!(amount, "10.00");
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
        let err = Cli::try_parse_from(["lazuar-pay", "--help"]).unwrap_err();
        let msg = err.to_string();
        assert!(
            msg.contains("LAZUAR_PAY_API_KEY") || msg.contains("api-key"),
            "{msg}"
        );
        assert!(!msg.contains("lzr_sk_live"), "{msg}");
        assert!(!msg.contains("sk_live"), "{msg}");
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
        let err = read_gateway_file(&path).unwrap_err();
        let msg = err.to_string();
        assert!(msg.contains("test processor"), "{msg}");
        assert!(!msg.contains("sk_should_not_leak"), "{msg}");
        let _ = std::fs::remove_file(&path);
    }
}
