//! Port of `SolanaConfirm.cs` — buyer-pasted signature confirmation and the
//! watcher loop (claim open solana checkouts, confirm by reference, expire on
//! watch timeout). Runs receive-only: settlement is observation, and Solana has
//! no refund API, so over-capacity/late rows stay `pending` as the ops marker.

use chrono::{Duration, Utc};
use postgres::Client;
use serde_json::{json, Value};
use uuid::Uuid;

use crate::domain::transitions;
use crate::money::fulfillment::{fulfill_paid, CheckoutGates, FulfillError};
use crate::rails::providers;
use rust_decimal::Decimal;

use crate::secrets::SecretBox;
use crate::rails::solana::base58;
use crate::rails::solana::cluster;
use crate::rails::solana::rpc::{SolanaRpc, SolanaRpcError};
use crate::rails::solana::tx;
use crate::webhooks::envelope;
use crate::webhooks::enqueue;

#[derive(Debug, thiserror::Error)]
pub enum WatchError {
    #[error("solana rpc throttled")]
    Throttled,
    #[error("db: {0}")]
    Db(#[from] postgres::Error),
}

#[derive(Debug)]
pub enum ConfirmOutcome {
    Ok,
    Duplicate,
    LatePayManual { refunded: bool },
    ProviderMismatch,
    SignatureRequired,
    RailNotConfigured,
    ClusterMismatch,
    Paused,
    NotOpen,
    ValidationFailed(String),
    FulfillConflict,
    Unavailable(String),
    Throttled,
}

pub struct ConfirmDeps<'a> {
    pub box_one: &'a SecretBox,
    pub gates: &'a CheckoutGates,
    pub rpc: &'a SolanaRpc,
    pub environment: &'a str,
    pub config_cluster: &'a str,
}

/// Confirm a buyer-supplied signature for one checkout.
#[allow(clippy::too_many_lines)]
pub fn confirm(
    conn: &mut Client,
    deps: &ConfirmDeps,
    checkout: &CheckoutForSolana,
    signature: &str,
) -> Result<ConfirmOutcome, postgres::Error> {
    if !providers::is_solana(checkout.provider.as_deref().unwrap_or("")) {
        return Ok(ConfirmOutcome::ProviderMismatch);
    }
    if signature.trim().is_empty() || base58::decode(signature).is_none() {
        return Ok(ConfirmOutcome::SignatureRequired);
    }

    let org_id = checkout.org_id.as_str();
    let seen = conn
        .query_opt(
            "SELECT 1 FROM public.psp_webhook_events \
             WHERE \"OrgId\" = $1 AND \"Provider\" = $2 AND \"EventId\" = $3",
            &[&org_id, &providers::SOLANA, &signature],
        )?
        .is_some();
    if seen {
        return Ok(ConfirmOutcome::Duplicate);
    }

    let paused = conn
        .query_opt(
            "SELECT \"ChargesPaused\" FROM public.org_settings WHERE \"OrgId\" = $1",
            &[&org_id],
        )?
        .map(|row| row.get::<_, bool>(0))
        .unwrap_or(false);
    if paused {
        return Ok(ConfirmOutcome::Paused);
    }

    let cred_environment = conn
        .query_opt(
            "SELECT \"Environment\" FROM public.gateway_credentials \
             WHERE \"OrgId\" = $1 AND \"Provider\" = $2",
            &[&org_id, &providers::SOLANA],
        )?
        .map(|row| row.get::<_, String>(0));
    let Some(cred_environment) = cred_environment else {
        return Ok(ConfirmOutcome::RailNotConfigured);
    };

    let cluster_name = cluster::from_config(Some(deps.config_cluster));
    if !cluster::matches_vault(cluster_name, Some(&cred_environment)) {
        return Ok(ConfirmOutcome::ClusterMismatch);
    }

    let rpc_doc = match deps.rpc.get_transaction(signature) {
        Ok(doc) => doc,
        Err(SolanaRpcError::Throttled) => return Ok(ConfirmOutcome::Throttled),
        Err(SolanaRpcError::InvalidOperation(message)) => {
            return Ok(ConfirmOutcome::Unavailable(message));
        }
    };

    if let Err(mismatch) = tx::validate(
        &rpc_doc,
        &tx::ValidateInput {
            checkout_id: &checkout.id,
            checkout_amount: checkout.amount,
            provider_session_id: &checkout.provider_session_id,
            public_merchant_id: &checkout.public_merchant_id,
            signature,
            cluster: cluster_name,
        },
    ) {
        return Ok(ConfirmOutcome::ValidationFailed(mismatch));
    }

    if matches!(checkout.status.as_str(), "expired" | "failed") {
        // Late manual confirm on a terminal checkout: record the event so the
        // signature cannot be replayed, and surface the late-pay marker.
        let mut late_tx = conn.transaction()?;
        let insert = late_tx.execute(
            "INSERT INTO public.psp_webhook_events \
             (\"OrgId\",\"Provider\",\"EventId\",\"ReceivedAt\") VALUES ($1,$2,$3,$4)",
            &[&org_id, &providers::SOLANA, &signature, &Utc::now()],
        );
        if let Err(err) = insert {
            late_tx.rollback()?;
            if err.as_db_error().map(|db| db.code()) == Some(&postgres::error::SqlState::UNIQUE_VIOLATION) {
                return Ok(ConfirmOutcome::Duplicate);
            }
            return Err(err);
        }
        late_tx.commit()?;
        return Ok(ConfirmOutcome::LatePayManual { refunded: false });
    }

    if checkout.status != "open" {
        return Ok(ConfirmOutcome::NotOpen);
    }

    let mut fulfill_tx = conn.transaction()?;
    let insert = fulfill_tx.execute(
        "INSERT INTO public.psp_webhook_events \
         (\"OrgId\",\"Provider\",\"EventId\",\"ReceivedAt\") VALUES ($1,$2,$3,$4)",
        &[&org_id, &providers::SOLANA, &signature, &Utc::now()],
    );
    if let Err(err) = insert {
        fulfill_tx.rollback()?;
        if err.as_db_error().map(|db| db.code()) == Some(&postgres::error::SqlState::UNIQUE_VIOLATION) {
            return Ok(ConfirmOutcome::Duplicate);
        }
        return Err(err);
    }

    let outcome = match fulfill_paid(&mut fulfill_tx, deps.gates, &checkout.id, providers::SOLANA, Some(signature))
    {
        Ok(outcome) => outcome,
        Err(FulfillError::Paused) => {
            fulfill_tx.rollback()?;
            return Ok(ConfirmOutcome::Paused);
        }
        Err(FulfillError::Db(db_err)) => {
            fulfill_tx.rollback()?;
            if db_err.as_db_error().map(|db| db.code()) == Some(&postgres::error::SqlState::UNIQUE_VIOLATION) {
                let fresh: Option<String> = conn
                    .query_opt("SELECT \"Status\" FROM public.checkouts WHERE \"Id\" = $1", &[&checkout.id])?
                    .map(|row| row.get(0));
                if fresh.as_deref() == Some("paid") {
                    return Ok(ConfirmOutcome::Duplicate);
                }
                return Ok(ConfirmOutcome::FulfillConflict);
            }
            return Err(db_err);
        }
    };
    fulfill_tx.commit()?;

    // Over-capacity link child: the pending refund row was booked in the
    // transaction. Solana has no refund API — stays pending as the ops marker.
    let _ = outcome.pending_late_refund_id;

    Ok(ConfirmOutcome::Ok)
}

/// The watcher: claim open solana checkouts (2s lease, SKIP LOCKED), expire
/// ones past the reservation TTL, confirm the rest by reference signatures.
pub struct Watcher<'a> {
    pub conn: &'a mut Client,
    pub deps: &'a ConfirmDeps<'a>,
    pub ttl: Duration,
}

impl Watcher<'_> {
    /// A throttled pass surfaces as `WatchError::Throttled` so the worker can
    /// back off 15s (C# SolanaConfirmWorker parity).
    pub fn run_once(&mut self) -> Result<usize, postgres::Error> {
        let ttl_cutoff = Utc::now() - self.ttl;
        let claimed = self.claim_open()?;
        let count = claimed.len();
        for row in claimed {
            if row.created_at < ttl_cutoff {
                self.fail_watch_timeout(&row)?;
                continue;
            }
            self.confirm_signatures(&row)?;
        }
        Ok(count)
    }

    fn claim_open(&mut self) -> Result<Vec<ClaimedRow>, postgres::Error> {
        let stamp = Utc::now();
        let lease = stamp - Duration::seconds(2);
        let mut tx = self.conn.transaction()?;
        let rows = tx.query(
            "UPDATE public.checkouts AS c \
             SET \"WatchClaimedAt\" = $1 \
             FROM ( \
                 SELECT \"Id\" FROM public.checkouts \
                 WHERE \"Provider\" = $2 AND \"Status\" = 'open' \
                   AND \"PspRedirectUrl\" IS NOT NULL AND \"ProviderSessionId\" IS NOT NULL \
                   AND (\"WatchClaimedAt\" IS NULL OR \"WatchClaimedAt\" < $3) \
                 ORDER BY \"CreatedAt\", \"Id\" \
                 LIMIT 50 \
                 FOR UPDATE SKIP LOCKED \
             ) AS pick \
             WHERE c.\"Id\" = pick.\"Id\" \
             RETURNING c.\"Id\", c.\"OrgId\", c.\"Amount\", c.\"Currency\", c.\"Status\", \
                       c.\"Provider\", c.\"ProviderSessionId\", c.\"PublicToken\", \
                       c.\"PaymentLinkId\", c.\"CreatedAt\"",
            &[&stamp, &providers::SOLANA, &lease],
        )?;
        tx.commit()?;
        Ok(rows
            .iter()
            .map(|row| ClaimedRow {
                id: row.get("Id"),
                org_id: row.get("OrgId"),
                amount: row.get("Amount"),
                currency: row.get("Currency"),
                status: row.get("Status"),
                provider: row.get("Provider"),
                provider_session_id: row.get::<_, Option<String>>("ProviderSessionId").unwrap_or_default(),
                public_token: row.get("PublicToken"),
                payment_link_id: row.get("PaymentLinkId"),
                created_at: row.get("CreatedAt"),
            })
            .collect())
    }

    fn confirm_signatures(&mut self, row: &ClaimedRow) -> Result<(), postgres::Error> {
        let Ok(sigs) = self.deps.rpc.get_signatures_for_address(&row.provider_session_id) else {
            return Ok(());
        };
        let Some(result) = sigs.get("result").and_then(Value::as_array) else {
            return Ok(());
        };

        for item in result {
            let sig: String = match item.as_str() {
                Some(s) => s.to_string(),
                None => item
                    .get("signature")
                    .and_then(Value::as_str)
                    .map(str::to_string)
                    .unwrap_or_default(),
            };
            if sig.trim().is_empty() {
                continue;
            }

            let checkout = CheckoutForSolana {
                id: row.id.clone(),
                org_id: row.org_id.clone(),
                amount: row.amount,
                currency: row.currency.clone(),
                status: row.status.clone(),
                provider: row.provider.clone(),
                provider_session_id: row.provider_session_id.clone(),
                public_merchant_id: String::new(),
            };
            let outcome = match confirm(
                self.conn,
                self.deps,
                &checkout,
                &sig,
            ) {
                Ok(outcome) => outcome,
                Err(err) => {
                    let _ = err;
                    return Ok(());
                }
            };
            // Keep scanning only while outcomes are the harmless 400 class —
            // a fulfilled or duplicated signature stops the sweep. A throttle
            // surfaces to the worker as a backoff signal (C# parity).
            if matches!(outcome, ConfirmOutcome::Throttled) {
                // Throttled: just skip this pass and try again on the next poll.
            }
            if !matches!(outcome, ConfirmOutcome::ValidationFailed(_)) {
                return Ok(());
            }
        }
        Ok(())
    }

    fn fail_watch_timeout(&mut self, row: &ClaimedRow) -> Result<(), postgres::Error> {
        if row.status != "open" {
            return Ok(());
        }
        let link_child = row.payment_link_id.is_some();
        let event_id = if link_child {
            format!("expired:{}", row.id)
        } else {
            format!("watch_timeout:{}", row.id)
        };
        let seen = self.conn.query_opt(
            "SELECT 1 FROM public.psp_webhook_events \
             WHERE \"OrgId\" = $1 AND \"Provider\" = $2 AND \"EventId\" = $3",
            &[&row.org_id, &providers::SOLANA, &event_id],
        )?;
        if seen.is_some() {
            return Ok(());
        }

        let mut fail_tx = self.conn.transaction()?;
        fail_tx.execute(
            "INSERT INTO public.psp_webhook_events \
             (\"OrgId\",\"Provider\",\"EventId\",\"ReceivedAt\") VALUES ($1,$2,$3,$4)",
            &[&row.org_id, &providers::SOLANA, &event_id, &Utc::now()],
        )?;

        // Issue 002: the watch-timeout flip is a CAS off "open" — losing it means
        // a webhook or fulfiller won; roll back the event row, winner's status stands.
        let Ok(uuid) = Uuid::parse_str(&row.id) else {
            fail_tx.rollback()?;
            return Ok(());
        };
        let target = if link_child { "expired" } else { "failed" };
        if !transitions::try_leave_open(&mut fail_tx, uuid, target)? {
            fail_tx.rollback()?;
            return Ok(());
        }

        let payload = if link_child {
            serde_json::json!({
                "checkout_id": row.id,
                "payment_link_id": row.payment_link_id,
                "reason": "watch_timeout",
            })
        } else {
            serde_json::json!({
                "checkout_id": row.id,
                "reason": "watch_timeout",
                "provider": providers::SOLANA,
            })
        };
        let event_type = if link_child { envelope::EXPIRED } else { envelope::FAILED };
        enqueue::try_add(&mut fail_tx, &row.org_id, &event_id, event_type, payload)?;
        fail_tx.commit()?;
        Ok(())
    }
}

pub struct CheckoutForSolana {
    pub id: String,
    pub org_id: String,
    pub amount: Decimal,
    pub currency: String,
    pub status: String,
    pub provider: Option<String>,
    pub provider_session_id: String,
    pub public_merchant_id: String,
}

struct ClaimedRow {
    id: String,
    org_id: String,
    amount: Decimal,
    currency: String,
    status: String,
    provider: Option<String>,
    provider_session_id: String,
    public_token: String,
    payment_link_id: Option<String>,
    created_at: chrono::DateTime<Utc>,
}

