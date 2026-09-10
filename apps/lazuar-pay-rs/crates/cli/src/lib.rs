//! `lazuar-pay` — TypeSpec `/v1` CLI. Talks HTTP only (035/02). Never imports storage.

#![forbid(unsafe_code)]

use clap::Subcommand;
use pay_client::{Client, Config, Error};
use rust_decimal::Decimal;
use serde_json::Value;
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
    /// Pay origin, no trailing slash. Env `LAZUAR_PAY_BASE_URL`.
    #[arg(long, env = "LAZUAR_PAY_BASE_URL")]
    pub base_url: Option<String>,
    /// One `lzr_sk_…` (or Testing `test-writer`). Env `LAZUAR_PAY_API_KEY`.
    #[arg(long, env = "LAZUAR_PAY_API_KEY")]
    pub api_key: Option<String>,
    /// One tenant id. Env `LAZUAR_PAY_ORG_ID`.
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
    /// `GET /v1/orgs/{orgId}/payments`
    Payments {
        #[arg(long)]
        limit: Option<u32>,
        #[arg(long)]
        after: Option<String>,
    },
    #[command(subcommand)]
    Receipts(ReceiptsCmd),
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
        #[arg(long)]
        idempotency_key: Option<String>,
    },
    /// `GET /v1/checkouts/{id}`
    Get { id: String },
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

pub fn config_from_cli(cli: &Cli) -> Result<Config, Error> {
    Config::from_parts(
        cli.base_url.clone(),
        cli.api_key.clone(),
        cli.org_id.clone(),
    )
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
                .checkout_create(&provider, amount, &currency, idempotency_key.as_deref())
                .await
        }
        Command::Checkout(CheckoutCmd::Get { id }) => client.checkout_get(&id).await,
        Command::Payments { limit, after } => client.payments_list(limit, after.as_deref()).await,
        Command::Receipts(ReceiptsCmd::List { limit, after }) => {
            client.receipts_list(limit, after.as_deref()).await
        }
        Command::Receipts(ReceiptsCmd::Get { id }) => client.receipts_get(&id).await,
    }
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
    fn create_requires_provider_and_amount() {
        let err = Cli::try_parse_from(["lazuar-pay", "checkout", "create"]).unwrap_err();
        let msg = err.to_string();
        assert!(
            msg.contains("provider") || msg.contains("required"),
            "{msg}"
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
}
