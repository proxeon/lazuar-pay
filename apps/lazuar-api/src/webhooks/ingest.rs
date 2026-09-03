//! Port of `Webhooks/WebhookEndpoints.Handle` — the `/v1/webhooks/{provider}/{orgId}`
//! conductor. Typed outcomes; the HTTP layer maps them to statuses.

use chrono::{DateTime, Utc};
use rust_decimal::Decimal;
use uuid::Uuid;

use crate::domain::transitions;
use crate::domain::currency;
use crate::money::fulfillment::{fulfill_paid, CheckoutGates, FulfillError};
use crate::rails::providers;
use crate::rails::remote::Refunder;
use crate::rails::{
    billplz_webhook, chip_webhook, razorpay_webhook, stripe_webhook, test_webhook, xendit_webhook,
};
use crate::secrets::SecretBox;
use crate::webhooks::envelope;
use crate::webhooks::enqueue;
use crate::webhooks::psp_parse::{Headers, ParsedWebhook, WebhookParseError};

pub struct IngestInput<'a> {
    pub provider_raw: &'a str,
    pub org_id: &'a str,
    pub raw_body: &'a str,
    pub headers: &'a [(String, String)],
    pub environment: &'a str,
    pub test_webhook_secret: &'a str,
    pub stripe_webhook_secret: &'a str,
}

#[derive(Debug)]
pub enum IngestOutcome {
    /// 400s
    UnknownProvider,
    EmptyBody,
    RailNotConfigured,
    CheckoutNotFound,
    ProviderMismatch,
    CurrencyMismatch,
    AmountMismatch,
    VerifyError(String),
    /// 503 — configured rail whose secret cannot be resolved is a server fault.
    MissingSecret(String),
    /// 409 — org charges paused.
    PausedConflict,
    /// Duplicate event: answer ok so the PSP stops retrying.
    Duplicate,
    Ignored { reason: Option<String> },
    Failed,
    /// Late capture on an expired/failed checkout; `refunded` reports settle result.
    LateRefunded { refunded: bool },
    PaidOk,
    /// 500 "fulfill conflict" — concurrent fulfill lost and winner had not paid.
    FulfillConflict,
    /// 500 "fulfill failed"
    FulfillFailed,
}

struct CheckoutLight {
    id: String,
    org_id: String,
    amount: Decimal,
    currency: String,
    status: String,
    provider: Option<String>,
}

pub fn handle(
    conn: &mut postgres::Client,
    box_one: &SecretBox,
    gates: &CheckoutGates,
    remote: &dyn Refunder,
    input: &IngestInput,
) -> Result<IngestOutcome, postgres::Error> {
    let Some(name) = providers::try_normalize(Some(input.provider_raw)) else {
        return Ok(IngestOutcome::UnknownProvider);
    };

    if input.raw_body.trim().is_empty() {
        return Ok(IngestOutcome::EmptyBody);
    }

    let headers = Headers(input.headers);

    let mut cred_webhook_ciphertext: Option<String> = None;
    if providers::is_test(name) {
        if !providers::allows_test(input.environment) {
            return Ok(IngestOutcome::RailNotConfigured);
        }
    } else {
        let cred = conn.query_opt(
            "SELECT \"WebhookCiphertext\" FROM public.gateway_credentials \
             WHERE \"OrgId\" = $1 AND \"Provider\" = $2",
            &[&input.org_id, &name],
        )?;
        match cred {
            Some(row) => cred_webhook_ciphertext = row.get(0),
            None => return Ok(IngestOutcome::RailNotConfigured),
        }
    }

    let parse_error_to_outcome = |err: WebhookParseError| match err {
        WebhookParseError::Verify(message) => IngestOutcome::VerifyError(message),
        WebhookParseError::MissingSecret(message) => IngestOutcome::MissingSecret(message),
    };

    let parsed: ParsedWebhook = match name {
        providers::STRIPE => {
            let whsec = stripe_webhook::resolve_secret(
                cred_webhook_ciphertext.as_deref(),
                box_one,
                Some(input.stripe_webhook_secret),
                input.environment,
            )
            .unwrap_or_default();
            match stripe_webhook::parse(input.raw_body, &headers, &whsec) {
            Ok(p) => p,
            Err(e) => return Ok(parse_error_to_outcome(e)),
        }
        }
        providers::CHIP => match chip_webhook::parse(input.raw_body, &headers, cred_webhook_ciphertext.as_deref(), box_one) {
            Ok(p) => p,
            Err(e) => return Ok(parse_error_to_outcome(e)),
        },
        providers::BILLPLZ => {
            match billplz_webhook::parse(input.raw_body, cred_webhook_ciphertext.as_deref(), box_one) {
                Ok(p) => p,
                Err(e) => return Ok(parse_error_to_outcome(e)),
            }
        }
        providers::XENDIT => match xendit_webhook::parse(input.raw_body, &headers, cred_webhook_ciphertext.as_deref(), box_one) {
            Ok(p) => p,
            Err(e) => return Ok(parse_error_to_outcome(e)),
        },
        providers::RAZORPAY => match razorpay_webhook::parse(input.raw_body, &headers, cred_webhook_ciphertext.as_deref(), box_one) {
            Ok(p) => p,
            Err(e) => return Ok(parse_error_to_outcome(e)),
        },
        // Solana is receive-only: money is confirmed by chain watching, not webhooks.
        providers::SOLANA => {
            return Ok(IngestOutcome::VerifyError("solana does not use inbound PSP webhooks".into()));
        }
        providers::TEST => {
            match test_webhook::parse(input.raw_body, &headers, input.test_webhook_secret) {
                Ok(p) => p,
                Err(e) => return Ok(parse_error_to_outcome(e)),
            }
        }
        _ => return Ok(IngestOutcome::UnknownProvider),
    };

    // Per (Org, Provider, EventId) dedupe: a replayed event answers ok so the
    // PSP stops retrying. Issue 018 rides here for Razorpay — the rotating
    // header id is the dedupe id when the rail provides it.
    let dup = conn
        .query_opt(
            "SELECT 1 FROM public.psp_webhook_events \
             WHERE \"OrgId\" = $1 AND \"Provider\" = $2 AND \"EventId\" = $3",
            &[&input.org_id, &name, &parsed.event_id],
        )?
        .is_some();
    if dup {
        return Ok(IngestOutcome::Duplicate);
    }

    if parsed.ignored && !parsed.failed {
        insert_event_ignore(conn, input.org_id, name, &parsed.event_id)?;
        return Ok(IngestOutcome::Ignored { reason: parsed.ignore_reason });
    }

    // Resolve the checkout: direct id first, then hosted session id.
    let mut checkout_id = parsed.checkout_id.clone().filter(|s| !s.trim().is_empty());
    if checkout_id.is_none() {
        if let Some(session_id) = parsed.hosted_session_id.as_deref().filter(|s| !s.trim().is_empty()) {
            checkout_id = conn
                .query_opt(
                    "SELECT \"Id\" FROM public.checkouts \
                     WHERE \"OrgId\" = $1 AND \"Provider\" = $2 AND \"ProviderSessionId\" = $3",
                    &[&input.org_id, &name, &session_id],
                )?
                .map(|row| row.get(0));
        }
    }
    let Some(checkout_id) = checkout_id else {
        return Ok(IngestOutcome::CheckoutNotFound);
    };

    let Some(checkout) = conn.query_opt(
        "SELECT \"Id\",\"OrgId\",\"Amount\",\"Currency\",\"Status\",\"Provider\" \
         FROM public.checkouts WHERE \"Id\" = $1",
        &[&checkout_id],
    )?
    .map(|row| CheckoutLight {
        id: row.get("Id"),
        org_id: row.get("OrgId"),
        amount: row.get("Amount"),
        currency: row.get("Currency"),
        status: row.get("Status"),
        provider: row.get("Provider"),
    })
    else {
        return Ok(IngestOutcome::CheckoutNotFound);
    };

    if checkout.org_id != input.org_id {
        return Ok(IngestOutcome::CheckoutNotFound);
    }
    if checkout
        .provider
        .as_deref()
        .map(|p| !p.eq_ignore_ascii_case(name))
        .unwrap_or(true)
    {
        return Ok(IngestOutcome::ProviderMismatch);
    }

    let paused = conn
        .query_opt(
            "SELECT \"ChargesPaused\" FROM public.org_settings WHERE \"OrgId\" = $1",
            &[&input.org_id],
        )?
        .map(|row| row.get::<_, bool>(0))
        .unwrap_or(false);
    if paused {
        return Ok(IngestOutcome::PausedConflict);
    }

    if parsed.failed {
        return handle_failed(conn, input, name, &parsed, &checkout);
    }

    if matches!(checkout.status.as_str(), "expired" | "failed") {
        // Money arrived late. Book the refund pending BEFORE any money moves; the
        // amount is the actual capture, which can differ from the quoted checkout.
        let refund_id = Uuid::new_v4().simple().to_string();
        let mut late_tx = conn.transaction()?;
        let event_insert = late_tx.execute(
            "INSERT INTO public.psp_webhook_events \
             (\"OrgId\",\"Provider\",\"EventId\",\"ReceivedAt\") VALUES ($1,$2,$3,$4)",
            &[&input.org_id, &name, &parsed.event_id, &Utc::now()],
        );
        if let Err(err) = event_insert {
            late_tx.rollback()?;
            if err.as_db_error().map(|db| db.code()) == Some(&postgres::error::SqlState::UNIQUE_VIOLATION) {
                return Ok(IngestOutcome::Duplicate);
            }
            return Err(err);
        }

        // Issue 009: one late-pay refund per checkout — the in-tx check covers this
        // replica; the filtered unique index covers concurrent webhooks across
        // replicas (its violation lands in the catch below).
        let already_reserved = late_tx
            .query_opt(
                "SELECT 1 FROM public.refunds WHERE \"CheckoutId\" = $1 AND \"Reason\" = 'late_pay'",
                &[&checkout.id],
            )?
            .is_some();
        if !already_reserved {
            let amount_minor = parsed.amount_minor.unwrap_or(0);
            let refund_amount = if amount_minor > 0 {
                currency::from_minor(Decimal::from(amount_minor))
            } else {
                checkout.amount
            };
            late_tx.execute(
                "INSERT INTO public.refunds \
                 (\"Id\",\"OrgId\",\"CheckoutId\",\"ChargeId\",\"Amount\",\"Currency\",\"Status\",\
                 \"Provider\",\"ProviderRef\",\"Reason\",\"IdempotencyKey\",\"CreatedAt\") \
                 VALUES ($1,$2,$3,NULL,$4,$5,'pending',$6,$7,'late_pay',NULL,$8)",
                &[
                    &refund_id,
                    &input.org_id,
                    &checkout.id,
                    &refund_amount,
                    &checkout.currency,
                    &name,
                    &parsed.provider_ref,
                    &Utc::now(),
                ],
            )?;
        }
        late_tx.commit()?;

        let refunded = if already_reserved {
            false
        } else {
            settle_late(remote, &refund_id, parsed.provider_ref.as_deref(), parsed.amount_minor)
        };
        return Ok(IngestOutcome::LateRefunded { refunded });
    }

    if parsed
        .currency
        .as_deref()
        .is_some_and(|c| !c.eq_ignore_ascii_case(&checkout.currency))
    {
        return Ok(IngestOutcome::CurrencyMismatch);
    }

    if let Some(minor) = parsed.amount_minor {
        if minor != currency::to_minor(checkout.amount) {
            return Ok(IngestOutcome::AmountMismatch);
        }
    }

    // Fulfill inside one transaction with the event dedupe row. "duplicate" only
    // if the concurrent winner actually paid; anything else must 5xx so the PSP
    // retries — answering ok on a rolled-back fulfill is how a real payment
    // disappears.
    let mut tx = conn.transaction()?;
    let insert = tx.execute(
        "INSERT INTO public.psp_webhook_events \
         (\"OrgId\",\"Provider\",\"EventId\",\"ReceivedAt\") VALUES ($1,$2,$3,$4)",
        &[&input.org_id, &name, &parsed.event_id, &Utc::now()],
    );
    if let Err(err) = insert {
        tx.rollback()?;
        if err.as_db_error().map(|db| db.code()) == Some(&postgres::error::SqlState::UNIQUE_VIOLATION) {
            return Ok(IngestOutcome::Duplicate);
        }
        return Err(err);
    }

    let outcome = match fulfill_paid(&mut tx, gates, &checkout.id, name, parsed.provider_ref.as_deref()) {
        Ok(outcome) => outcome,
        Err(FulfillError::Paused) => {
            tx.rollback()?;
            return Ok(IngestOutcome::PausedConflict);
        }
        Err(FulfillError::Db(db_err)) => {
            tx.rollback()?;
            if db_err.as_db_error().map(|db| db.code()) == Some(&postgres::error::SqlState::UNIQUE_VIOLATION) {
                // Duplicate only if the concurrent winner actually paid this checkout.
                let fresh = conn.query_opt(
                    "SELECT \"Status\" FROM public.checkouts WHERE \"Id\" = $1",
                    &[&checkout.id],
                )?;
                if fresh.map(|r| r.get::<_, String>(0)).as_deref() == Some("paid") {
                    return Ok(IngestOutcome::Duplicate);
                }
                return Ok(IngestOutcome::FulfillConflict);
            }
            return Err(db_err);
        }
    };
    tx.commit()?;

    // Settle the over-capacity late refund only after commit: the pending row is
    // durable and its id (the processor idempotency key) is stable across retries.
    if let Some(late_id) = outcome.pending_late_refund_id {
        settle_late(remote, &late_id, parsed.provider_ref.as_deref(), outcome.late_refund_amount_minor);
    }

    Ok(IngestOutcome::PaidOk)
}

/// Processor-side settle of a booked late_pay refund (C# `SettlePendingRefundAsync`).
/// The real rail call lands with Phase 4; the pending row remains the ops marker
/// for rails with no refund API.
fn settle_late(
    remote: &dyn Refunder,
    refund_id: &str,
    provider_ref: Option<&str>,
    amount_minor: Option<i64>,
) -> bool {
    let _ = (remote, refund_id, provider_ref, amount_minor);
    false
}

fn handle_failed(
    conn: &mut postgres::Client,
    input: &IngestInput,
    name: &str,
    parsed: &ParsedWebhook,
    checkout: &CheckoutLight,
) -> Result<IngestOutcome, postgres::Error> {
    if checkout.status == "paid" {
        insert_event_ignore(conn, input.org_id, name, &parsed.event_id)?;
        return Ok(IngestOutcome::Ignored { reason: Some("already_paid".into()) });
    }

    let mut fail_tx = conn.transaction()?;
    let event_insert = fail_tx.execute(
        "INSERT INTO public.psp_webhook_events \
         (\"OrgId\",\"Provider\",\"EventId\",\"ReceivedAt\") VALUES ($1,$2,$3,$4)",
        &[&input.org_id, &name, &parsed.event_id, &Utc::now()],
    );
    if let Err(err) = event_insert {
        fail_tx.rollback()?;
        if err.as_db_error().map(|db| db.code()) == Some(&postgres::error::SqlState::UNIQUE_VIOLATION) {
            return Ok(IngestOutcome::Duplicate);
        }
        return Err(err);
    }

    if checkout.status == "open" {
        // Issue 002: the failed-flip is a CAS off "open" — the TTL sweep may have
        // committed "expired" between the read and here. Losing the CAS means the
        // row is terminal; the subscription stays untouched and the event records.
        let Ok(checkout_uuid) = Uuid::parse_str(&checkout.id) else {
            fail_tx.commit()?;
            return Ok(IngestOutcome::Failed);
        };
        if !transitions::try_leave_open(&mut fail_tx, checkout_uuid, "failed")? {
            fail_tx.commit()?;
            return Ok(IngestOutcome::Failed);
        }

        let sub = fail_tx.query_opt(
            "SELECT \"AttemptCount\",\"PastDueAt\" FROM public.subscriptions WHERE \"CheckoutId\" = $1",
            &[&checkout.id],
        )?;
        if let Some(row) = sub {
            let attempts: i32 = row.get(0);
            let has_past_due: Option<DateTime<Utc>> = row.get(1);
            fail_tx.execute(
                "UPDATE public.subscriptions \
                 SET \"Status\" = 'past_due', \"PastDueAt\" = COALESCE($1::timestamptz, $2), \"AttemptCount\" = $3 \
                 WHERE \"CheckoutId\" = $4",
                &[&has_past_due, &Utc::now(), &(attempts + 1), &checkout.id],
            )?;
        }

        enqueue::try_add(
            &mut fail_tx,
            &checkout.org_id,
            &parsed.event_id,
            envelope::FAILED,
            serde_json::json!({
                "checkout_id": checkout.id,
                "reason": parsed.ignore_reason.clone().unwrap_or_else(|| "payment_failed".into()),
                "provider": name,
            }),
        )?;
    }

    fail_tx.commit()?;
    Ok(IngestOutcome::Failed)
}

fn insert_event_ignore(
    conn: &mut postgres::Client,
    org_id: &str,
    provider: &str,
    event_id: &str,
) -> Result<(), postgres::Error> {
    match conn.execute(
        "INSERT INTO public.psp_webhook_events \
         (\"OrgId\",\"Provider\",\"EventId\",\"ReceivedAt\") VALUES ($1,$2,$3,$4)",
        &[&org_id, &provider, &event_id, &Utc::now()],
    ) {
        Ok(_) => Ok(()),
        Err(err) if err.as_db_error().map(|db| db.code()) == Some(&postgres::error::SqlState::UNIQUE_VIOLATION) => Ok(()),
        Err(err) => Err(err),
    }
}
