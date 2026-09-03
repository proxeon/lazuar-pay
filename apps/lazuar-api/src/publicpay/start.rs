//! Port of `PublicPay/PublicPayEndpoints.Start` + `MintOrResume` — the buyer
//! start flow: limiter → link mint/resume or checkout retry → gate → hosted rail
//! mint → conditional persist.
//!
//! Issues carried:
//! - 007: conditional persist — exactly one minted session may land; the loser
//!   of the race returns the winner's URL so the payer never sees a redirect no
//!   confirmation will ever reference.
//! - 011: same-slot race recovery — the loser of a Start race resumes/recovers
//!   without a 500.
//! - 004: a failed checkout tied to a past_due subscription is retryable (CAS
//!   failed→open, dead PSP session dropped); failed one-offs and expired stay
//!   terminal.
//! - 016: the limiter caps unauthenticated junk before any DB work.

use chrono::Utc;
use postgres::Transaction;
use rust_decimal::Decimal;
use uuid::Uuid;

use crate::domain::transitions;
use crate::money::fulfillment::{fulfill_paid, CheckoutGates, FulfillError};
use crate::publicpay::buyer_email;
use crate::publicpay::gates::GateMap;
use crate::publicpay::limiter::PublicPayLimiter;
use crate::publicpay::occupancy;
use crate::rails::providers;
use crate::webhooks::envelope;
use crate::webhooks::enqueue;

/// The hosted-rail seam. Real rails mint a PSP session and return its URL;
/// the test rail returns the checkout URL and instantly fulfills.
pub trait HostedRail: Send + Sync {
    fn create_hosted_url(
        &self,
        checkout_id: &str,
        public_token: &str,
        org_id: &str,
    ) -> Result<HostedSession, StartRailError>;
}

#[derive(Debug, Clone)]
pub struct HostedSession {
    pub provider_session_id: String,
    pub url: String,
}

#[derive(Debug, thiserror::Error)]
pub enum StartRailError {
    /// 400-class: nothing was created at the PSP.
    #[error("{0}")]
    BadRequest(String),
    /// 503-class: the PSP refused or is unconfigured.
    #[error("{0}")]
    Rejected(String),
}

pub struct TestRail {
    pub checkout_base_url: String,
}

impl HostedRail for TestRail {
    fn create_hosted_url(
        &self,
        checkout_id: &str,
        public_token: &str,
        _org_id: &str,
    ) -> Result<HostedSession, StartRailError> {
        Ok(HostedSession {
            provider_session_id: format!("test:{checkout_id}"),
            url: format!("{}/c/{public_token}", self.checkout_base_url),
        })
    }
}

pub struct StartDeps<'a> {
    pub environment: &'a str,
    pub start_max_per_minute: i32,
    pub limiter: &'a PublicPayLimiter,
    pub start_gates: &'a GateMap,
    pub link_gates: &'a GateMap,
    pub fulfill_gates: &'a CheckoutGates,
    pub rail: &'a dyn HostedRail,
}

pub struct StartRequest<'a> {
    pub name: Option<&'a str>,
    pub email: Option<&'a str>,
    pub slot_key: Option<&'a str>,
}

#[derive(Debug)]
pub enum StartOutcome {
    Started { redirect_url: String },
    TooManyRequests,
    CheckoutNotFound,
    NotOpen,
    Paused,
    EmailRequired,
    SlotKeyRequired,
    RailNotConfigured,
    BadRequest(String),
}

struct CheckoutSnapshot {
    id: String,
    org_id: String,
    amount: Decimal,
    currency: String,
    status: String,
    interval: Option<String>,
    provider: Option<String>,
    payment_link_id: Option<String>,
    slot_key: Option<String>,
    public_token: String,
    psp_redirect_url: Option<String>,
    provider_session_id: Option<String>,
    payer_name: Option<String>,
    payer_email: Option<String>,
}

const CHECKOUT_COLUMNS: &str = "\
    SELECT \"Id\",\"OrgId\",\"Amount\",\"Currency\",\"Status\",\"Interval\",\"Provider\",\
    \"PaymentLinkId\",\"SlotKey\",\"PublicToken\",\"PspRedirectUrl\",\"ProviderSessionId\",\
    \"PayerName\",\"PayerEmail\" FROM public.checkouts";

fn snapshot(row: &postgres::Row) -> CheckoutSnapshot {
    CheckoutSnapshot {
        id: row.get("Id"),
        org_id: row.get("OrgId"),
        amount: row.get("Amount"),
        currency: row.get("Currency"),
        status: row.get("Status"),
        interval: row.get("Interval"),
        provider: row.get("Provider"),
        payment_link_id: row.get("PaymentLinkId"),
        slot_key: row.get("SlotKey"),
        public_token: row.get("PublicToken"),
        psp_redirect_url: row.get("PspRedirectUrl"),
        provider_session_id: row.get("ProviderSessionId"),
        payer_name: row.get("PayerName"),
        payer_email: row.get("PayerEmail"),
    }
}

fn load(conn: &mut postgres::Client, id: &str) -> Result<Option<CheckoutSnapshot>, postgres::Error> {
    conn.query_opt(&format!("{CHECKOUT_COLUMNS} WHERE \"Id\" = $1"), &[&id])
        .map(|opt| opt.map(|row| snapshot(&row)))
}

fn require_provider(provider: Option<&str>) -> Result<&'static str, StartOutcome> {
    providers::try_normalize(provider).ok_or(StartOutcome::RailNotConfigured)
}

fn requires_email(name: &str) -> bool {
    !providers::requires_email(name)
}

pub fn start(
    conn: &mut postgres::Client,
    deps: &StartDeps,
    token: &str,
    req: &StartRequest,
) -> Result<StartOutcome, postgres::Error> {
    if deps.start_max_per_minute > 0
        && !deps.limiter.try_acquire(token, deps.start_max_per_minute, 60)
    {
        return Ok(StartOutcome::TooManyRequests);
    }

    let link = conn.query_opt(
        "SELECT \"Id\",\"OrgId\",\"Provider\",\"Amount\",\"Currency\",\"MaxPayers\" \
         FROM public.payment_links WHERE \"PublicToken\" = $1",
        &[&token],
    )?;
    let checkout_id = if let Some(link) = link {
        let link = LinkRow {
            id: link.get("Id"),
            org_id: link.get("OrgId"),
            provider: link.get("Provider"),
            amount: link.get("Amount"),
            currency: link.get("Currency"),
            max_payers: link.get("MaxPayers"),
        };
        match mint_or_resume(conn, deps, &link, req)? {
            Ok(id) => id,
            Err(outcome) => return Ok(outcome),
        }
    } else {
        let session = crate::domain::checkout_store::get_by_public_token(conn, token)?;
        let Some(session) = session else {
            return Ok(StartOutcome::CheckoutNotFound);
        };

        if matches!(session.status.as_str(), "paid" | "expired" | "failed") {
            // Issue 004: a failed checkout tied to a past_due subscription is the
            // subscription's only recovery path. Failed ONE-OFF checkouts stay
            // terminal; expired stays terminal (late-pay refund logic depends on it).
            let has_subscription = conn
                .query_opt(
                    "SELECT 1 FROM public.subscriptions WHERE \"CheckoutId\" = $1",
                    &[&session.id],
                )?
                .is_some();
            if session.status != "failed" || !has_subscription {
                return Ok(StartOutcome::NotOpen);
            }
        }

        let paused = conn
            .query_opt(
                "SELECT \"ChargesPaused\" FROM public.org_settings WHERE \"OrgId\" = $1",
                &[&session.org_id],
            )?
            .map(|row| row.get::<_, bool>(0))
            .unwrap_or(false);
        if paused {
            return Ok(StartOutcome::Paused);
        }

        let row = load(conn, &session.id)?.expect("session id implies checkout row");
        if session.status == "failed" {
            // CAS failed→open so exactly one retryer wins; drop the dead PSP
            // session so a fresh hosted URL mints instead of resuming the spent one.
            let Ok(uuid) = Uuid::parse_str(&row.id) else {
                return Ok(StartOutcome::NotOpen);
            };
            if !transitions::try_transition(conn, uuid, "failed", "open")? {
                return Ok(StartOutcome::NotOpen);
            }
            conn.execute(
                "UPDATE public.checkouts SET \"PspRedirectUrl\" = NULL, \"ProviderSessionId\" = NULL \
                 WHERE \"Id\" = $1",
                &[&row.id],
            )?;
        }
        row.id
    };

    // Per-checkout gate: serialize double-clicks and same-checkout races.
    let outcome = deps.start_gates.with_gate(&checkout_id, || {
        start_gated(conn, deps, &checkout_id, req)
    })?;

    Ok(outcome)
}

fn start_gated(
    conn: &mut postgres::Client,
    deps: &StartDeps,
    checkout_id: &str,
    req: &StartRequest,
) -> Result<StartOutcome, postgres::Error> {
    // The row was loaded before this request acquired the gate — reload so the
    // resume guard decides on committed state, not a stale copy.
    let Some(mut row) = load(conn, checkout_id)? else {
        return Ok(StartOutcome::CheckoutNotFound);
    };

    if let Some(name) = req.name.map(str::trim).filter(|s| !s.is_empty()) {
        row.payer_name = Some(name.to_string());
        conn.execute(
            "UPDATE public.checkouts SET \"PayerName\" = $1 WHERE \"Id\" = $2",
            &[&row.payer_name, &row.id],
        )?;
    }
    if let Some(email) = req.email.map(str::trim).filter(|s| !s.is_empty()) {
        row.payer_email = Some(email.to_string());
        conn.execute(
            "UPDATE public.checkouts SET \"PayerEmail\" = $1 WHERE \"Id\" = $2",
            &[&row.payer_email, &row.id],
        )?;
    }

    let name = match require_provider(row.provider.as_deref()) {
        Ok(name) => name,
        Err(outcome) => return Ok(outcome),
    };

    if providers::requires_email(name) && !buyer_email::is_usable(row.payer_email.as_deref()) {
        return Ok(StartOutcome::EmailRequired);
    }

    // Resume guard: an already-minted session re-starts at its hosted URL.
    if row
        .psp_redirect_url
        .as_deref()
        .is_some_and(|s| !s.trim().is_empty())
        || row
            .provider_session_id
            .as_deref()
            .is_some_and(|s| !s.trim().is_empty())
    {
        if row.psp_redirect_url.as_deref().is_none_or(|s| s.trim().is_empty()) {
            return Ok(StartOutcome::NotOpen);
        }
        return Ok(StartOutcome::Started {
            redirect_url: row.psp_redirect_url.unwrap(),
        });
    }

    // PSP HTTP first, then persist. A persistence failure after the processor
    // already created a session is the 007 conditional-persist problem.
    let hosted = deps
        .rail
        .create_hosted_url(&row.id, &row.public_token, &row.org_id)
        .map_err(|err| match err {
            StartRailError::BadRequest(message) => StartOutcome::BadRequest(message),
            StartRailError::Rejected(message) => StartOutcome::RailNotConfigured,
        });

    let hosted = match hosted {
        Ok(hosted) => hosted,
        Err(outcome) => {
            // ExpireFailedReservation: the checkout never started — CAS it expired
            // (issue 002) and enqueue the start_failed webhook.
            if let Ok(uuid) = Uuid::parse_str(&row.id) {
                if transitions::try_leave_open(conn, uuid, "expired")? {
                    enqueue::try_add(
                        conn,
                        &row.org_id,
                        &format!("expired:{}", row.id),
                        envelope::EXPIRED,
                        serde_json::json!({
                            "checkout_id": row.id,
                            "payment_link_id": row.payment_link_id,
                            "reason": "start_failed",
                        }),
                    )?;
                }
            }
            return Ok(outcome);
        }
    };

    if providers::is_test(name) {
        // The test rail fulfills instantly through the same pipeline.
        let mut tx = conn.transaction()?;
        let event_insert = tx.execute(
            "INSERT INTO public.psp_webhook_events \
             (\"OrgId\",\"Provider\",\"EventId\",\"ReceivedAt\") VALUES ($1,$2,$3,$4)",
            &[
                &row.org_id,
                &name,
                &hosted.provider_session_id,
                &Utc::now(),
            ],
        );
        if let Err(err) = event_insert {
            // A concurrent same-checkout start already booked this event: its
            // fulfillment committed or is committing. Adopt its hosted URL.
            tx.rollback()?;
            let winner = load(conn, &row.id)?.expect("winner's row must exist");
            return Ok(match winner.psp_redirect_url.as_deref().filter(|u| !u.trim().is_empty()) {
                Some(url) => StartOutcome::Started { redirect_url: url.to_string() },
                None => StartOutcome::NotOpen,
            });
        }
        if let Err(err) =
            fulfill_paid(&mut tx, deps.fulfill_gates, &row.id, name, Some(&hosted.provider_session_id))
        {
            match err {
                FulfillError::Paused => {
                    tx.rollback()?;
                    return Ok(StartOutcome::Paused);
                }
                FulfillError::Db(db) => return Err(db),
            }
        }
        tx.commit()?;
        return Ok(StartOutcome::Started { redirect_url: hosted.url });
    }

    // Issue 007: conditional persist — exactly one minted session may land.
    let claimed = conn.execute(
        "UPDATE public.checkouts \
         SET \"PspRedirectUrl\" = $1, \"ProviderSessionId\" = $2, \"Provider\" = $3 \
         WHERE \"Id\" = $4 AND \"PspRedirectUrl\" IS NULL AND \"ProviderSessionId\" IS NULL",
        &[&hosted.url, &hosted.provider_session_id, &name, &row.id],
    )?;
    if claimed == 0 {
        let winner = load(conn, &row.id)?.expect("winner's row must exist");
        return match winner.psp_redirect_url.as_deref().filter(|u| !u.trim().is_empty()) {
            Some(url) => Ok(StartOutcome::Started { redirect_url: url.to_string() }),
            None => Ok(StartOutcome::NotOpen),
        };
    }

    Ok(StartOutcome::Started { redirect_url: hosted.url })
}

struct LinkRow {
    id: String,
    org_id: String,
    provider: String,
    amount: Decimal,
    currency: String,
    max_payers: Option<i32>,
}

/// Ok(id) resumes/mints to the gate phase; Err(outcome) is a terminal start error.
fn mint_or_resume(
    conn: &mut postgres::Client,
    deps: &StartDeps,
    link: &LinkRow,
    req: &StartRequest,
) -> Result<Result<String, StartOutcome>, postgres::Error> {
    let paused = conn
        .query_opt(
            "SELECT \"ChargesPaused\" FROM public.org_settings WHERE \"OrgId\" = $1",
            &[&link.org_id],
        )?
        .map(|row| row.get::<_, bool>(0))
        .unwrap_or(false);
    if paused {
        return Ok(Err(StartOutcome::Paused));
    }

    let Some(slot) = buyer_email::normalize_slot_key(req.slot_key) else {
        return Ok(Err(StartOutcome::SlotKeyRequired));
    };

    let Some(provider) = providers::try_normalize(Some(&link.provider)) else {
        return Ok(Err(StartOutcome::RailNotConfigured));
    };

    if providers::requires_email(provider) && !buyer_email::is_usable(req.email) {
        return Ok(Err(StartOutcome::EmailRequired));
    }

    deps.link_gates.with_gate(&link.id, || {
        let mut tx = conn.transaction()?;

        occupancy::lock_parent(&mut tx, &link.id)?;
        let ttl = occupancy::reservation_ttl(None);
        occupancy::expire_stale(&mut tx, &link.id, ttl)?;

        let existing = tx.query_opt(
            "SELECT \"Id\",\"Status\" FROM public.checkouts \
             WHERE \"PaymentLinkId\" = $1 AND \"SlotKey\" = $2",
            &[&link.id, &slot],
        )?;
        match existing {
            Some(row) => {
                let id: String = row.get("Id");
                let status: String = row.get("Status");
                if matches!(status.as_str(), "paid" | "expired" | "failed") {
                    tx.commit()?;
                    return Ok(Err(StartOutcome::NotOpen));
                }
                // Resume the open reservation; persist payer fields.
                if let Some(name) = req.name.map(str::trim).filter(|s| !s.is_empty()) {
                    tx.execute(
                        "UPDATE public.checkouts SET \"PayerName\" = $1 WHERE \"Id\" = $2",
                        &[&name, &id],
                    )?;
                }
                if let Some(email) = req.email.map(str::trim).filter(|s| !s.is_empty()) {
                    tx.execute(
                        "UPDATE public.checkouts SET \"PayerEmail\" = $1 WHERE \"Id\" = $2",
                        &[&email, &id],
                    )?;
                }
                tx.commit()?;
                Ok(Ok(id))
            }
            None => {
                let token = uuid::Uuid::new_v4().simple().to_string();
                tx.execute(
                    "INSERT INTO public.checkouts \
                     (\"Id\",\"OrgId\",\"PublicToken\",\"Amount\",\"Currency\",\"Status\",\"Interval\",\
                     \"Provider\",\"PaymentLinkId\",\"SlotKey\",\"CreatedAt\") \
                     VALUES ($1,$2,$3,$4,$5,'open','one_off',$6,$7,$8,$9)",
                    &[
                        &Uuid::new_v4().to_string(),
                        &link.org_id,
                        &token,
                        &link.amount,
                        &link.currency,
                        &link.provider,
                        &link.id,
                        &slot,
                        &Utc::now(),
                    ],
                )?;
                let id: String = tx
                    .query_one(
                        "SELECT \"Id\" FROM public.checkouts WHERE \"PaymentLinkId\" = $1 AND \"SlotKey\" = $2",
                        &[&link.id, &slot],
                    )?
                    .get(0);
                tx.commit()?;
                Ok(Ok(id))
            }
        }
    })
}
