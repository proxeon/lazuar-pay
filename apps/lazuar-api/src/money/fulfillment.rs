//! Port of `Money/Fulfillment.cs` — runs inside the caller's webhook transaction.
//!
//! Deliberately no catch on the final writes: a failure must unwind the caller's
//! transaction, which holds the PSP event dedupe row. Swallowing there acked the
//! webhook while charge, journal, and receipt silently never landed — a real
//! payment acknowledged lost. The gate + unique charges.CheckoutId index are the
//! dupes guard; receipt numbering is atomic, so a unique violation here means the
//! checkout was fulfilled concurrently and the caller answers "duplicate".

use std::collections::HashMap;
use std::sync::{Arc, Mutex};

use chrono::Utc;
use postgres::Transaction;
use rust_decimal::Decimal;
use uuid::Uuid;

use crate::domain::transitions;
use crate::domain::currency;
use crate::money::document_numbers;
use crate::money::malaysia_time;
use crate::rails::providers;
use crate::webhooks::envelope;
use crate::webhooks::enqueue;

#[derive(Debug, Default, Clone)]
pub struct FulfillOutcome {
    pub fulfilled: bool,
    /// Over-capacity late capture whose refund row was booked pending inside the
    /// transaction — the caller settles it AFTER commit, using this id as the
    /// processor idempotency key (issue 008/002 late-pay path).
    pub pending_late_refund_id: Option<String>,
    pub late_refund_amount_minor: Option<i64>,
}

#[derive(Debug, thiserror::Error)]
pub enum FulfillError {
    #[error("Org charges are paused")]
    Paused,
    #[error("database: {0}")]
    Db(#[from] postgres::Error),
}

/// In-process per-checkout gates (C# `CheckoutGates` semaphore map). They
/// serialize same-checkout fulfills within one process; cross-process safety
/// comes from the DB unique index on charges.CheckoutId. Scaling out keeps this
/// correct — see the single-replica caveat in plans/023-evals/02.
#[derive(Default)]
pub struct CheckoutGates {
    inner: Mutex<HashMap<String, Arc<Mutex<()>>>>,
}

pub struct CheckoutGateGuard {
    _arc: Arc<Mutex<()>>,
}

impl CheckoutGates {
    /// Hold the per-checkout gate for the duration of `f`.
    pub fn with_checkout_gate<R>(&self, checkout_id: &str, f: impl FnOnce() -> R) -> R {
        let arc = {
            let mut map = self.inner.lock().expect("gates map");
            map.entry(checkout_id.to_string())
                .or_insert_with(|| Arc::new(Mutex::new(())))
                .clone()
        };
        let _guard = arc.lock().expect("checkout gate");
        f()
    }
}

/// Over-capacity helper — `MaxPayers` null means unlimited.
pub fn is_full(max_payers: Option<i32>, taken: i64) -> bool {
    max_payers.is_some_and(|max| taken >= i64::from(max))
}

/// The payment-path fulfill: checkout → charge + payer + subscription + journal
/// + RCPT receipt + audit + outbound enqueue, all in the caller's transaction.
pub fn fulfill_paid(
    tx: &mut Transaction,
    gates: &CheckoutGates,
    checkout_id: &str,
    provider: &str,
    provider_ref: Option<&str>,
) -> Result<FulfillOutcome, FulfillError> {
    gates.with_checkout_gate(checkout_id, || {
        fulfill_paid_core(tx, checkout_id, provider, provider_ref)
    })
}

fn fulfill_paid_core(
    tx: &mut Transaction,
    checkout_id: &str,
    provider: &str,
    provider_ref: Option<&str>,
) -> Result<FulfillOutcome, FulfillError> {
    let Some(checkout) = tx
        .query_opt(
            "SELECT \"Id\",\"OrgId\",\"Amount\",\"Currency\",\"Status\",\"Interval\",\
             \"PaymentLinkId\",\"Provider\",\"PayerName\",\"PayerEmail\" \
             FROM public.checkouts WHERE \"Id\" = $1",
            &[&checkout_id],
        )?
        .map(|row| CheckoutSnapshot {
            id: row.get("Id"),
            org_id: row.get("OrgId"),
            amount: row.get("Amount"),
            currency: row.get("Currency"),
            status: row.get("Status"),
            interval: row.get("Interval"),
            payment_link_id: row.get("PaymentLinkId"),
            payer_name: row.get("PayerName"),
            payer_email: row.get("PayerEmail"),
        })
    else {
        return Ok(FulfillOutcome::default());
    };
    let checkout = checkout;
    let Ok(checkout_uuid) = Uuid::parse_str(&checkout.id) else {
        return Ok(FulfillOutcome::default());
    };

    if checkout.amount <= Decimal::ZERO || checkout.status != "open" {
        return Ok(FulfillOutcome::default());
    }

    let paused = tx
        .query_opt(
            "SELECT \"ChargesPaused\" FROM public.org_settings WHERE \"OrgId\" = $1",
            &[&checkout.org_id],
        )?
        .map(|row| row.get::<_, bool>(0))
        .unwrap_or(false);
    if paused {
        return Err(FulfillError::Paused);
    }

    if let Some(link_id) = &checkout.payment_link_id {
        // Issue 008: serialize the capacity check against minting and other
        // fulfillers on the parent link row (FOR UPDATE). The old unlocked count
        // let two concurrent late captures on a full link both read paid = max-1
        // and both fulfill, exceeding MaxPayers with no refund for the excess.
        let link = tx
            .query_opt(
                "SELECT \"Id\",\"MaxPayers\" FROM public.payment_links WHERE \"Id\" = $1 FOR UPDATE",
                &[&link_id],
            )?
            .map(|row| (row.get::<_, String>("Id"), row.get::<_, Option<i32>>("MaxPayers")));
        if let Some((_id, max_payers)) = link {
            let paid: i64 = tx
                .query_one(
                    "SELECT count(*) FROM public.checkouts \
                     WHERE \"PaymentLinkId\" = $1 AND \"Status\" = 'paid'",
                    &[&link_id],
                )?
                .get(0);
            if is_full(max_payers, paid) {
                // Over capacity: money already arrived. Book the refund pending NOW
                // (same transaction) and let the caller settle it after commit with
                // this row id as the idempotency key — a fresh Guid per attempt
                // previously let a retry move money twice.
                transitions::try_leave_open(tx, checkout_uuid, "expired")?;
                enqueue::try_add(
                    tx,
                    &checkout.org_id,
                    &format!("expired:{}", checkout.id),
                    envelope::EXPIRED,
                    serde_json::json!({
                        "checkout_id": checkout.id,
                        "payment_link_id": checkout.payment_link_id,
                        "reason": "over_capacity",
                    }),
                )?;
                return book_late_refund(tx, &checkout, provider, provider_ref);
            }
        }
    }

    // Issue 002: claim the checkout with CAS off "open". The previous blind "paid"
    // write could land over a concurrently committed "expired", turning a late
    // capture into a fulfilled order with a spurious expired webhook behind it.
    let checkout_uuid = match Uuid::parse_str(&checkout.id) {
        Ok(id) => id,
        Err(_) => return Ok(FulfillOutcome::default()),
    };
    if !transitions::try_leave_open(tx, checkout_uuid, "paid")? {
        // Another writer moved the row between our read and here. If it was
        // fulfilled, this delivery is a duplicate; if it expired/failed, the money
        // arrived late and follows the late-pay refund route instead of a forced
        // fulfillment.
        let current: Option<String> = tx
            .query_opt("SELECT \"Status\" FROM public.checkouts WHERE \"Id\" = $1", &[&checkout.id])?
            .map(|row| row.get(0));
        if matches!(current.as_deref(), Some("expired") | Some("failed")) {
            return book_late_refund(tx, &checkout, provider, provider_ref);
        }
        return Ok(FulfillOutcome::default());
    }

    let charge_id = Uuid::new_v4().simple().to_string();
    tx.execute(
        "INSERT INTO public.charges \
         (\"Id\",\"OrgId\",\"CheckoutId\",\"Provider\",\"ProviderRef\",\"Amount\",\"Currency\",\"Status\") \
         VALUES ($1,$2,$3,$4,$5,$6,$7,$8)",
        &[
            &charge_id, &checkout.org_id, &checkout.id, &provider, &provider_ref,
            &checkout.amount, &checkout.currency, &"paid",
        ],
    )?;

    let mut payer_id: Option<String> = None;
    if checkout.payer_name.as_deref().is_some_and(|s| !s.trim().is_empty())
        || checkout.payer_email.as_deref().is_some_and(|s| !s.trim().is_empty())
    {
        payer_id = Some(Uuid::new_v4().simple().to_string());
        tx.execute(
            "INSERT INTO public.payers (\"Id\",\"OrgId\",\"Email\",\"Name\") VALUES ($1,$2,$3,$4)",
            &[&payer_id, &checkout.org_id, &checkout.payer_email, &checkout.payer_name],
        )?;
    }

    if checkout.interval.as_deref() == Some("mo") || checkout.interval.as_deref() == Some("yr") {
        let existing = tx.query_opt(
            "SELECT \"Status\",\"PayerId\" FROM public.subscriptions WHERE \"CheckoutId\" = $1",
            &[&checkout.id],
        )?;
        match existing {
            None => {
                tx.execute(
                    "INSERT INTO public.subscriptions \
                     (\"Id\",\"OrgId\",\"CheckoutId\",\"PayerId\",\"Status\",\"Interval\",\"CreatedAt\") \
                     VALUES ($1,$2,$3,$4,$5,$6,$7)",
                    &[
                        &Uuid::new_v4().simple().to_string(),
                        &checkout.org_id,
                        &checkout.id,
                        &payer_id,
                        &"active",
                        &checkout.interval,
                        &Utc::now(),
                    ],
                )?;
            }
            Some(_row) => {
                tx.execute(
                    "UPDATE public.subscriptions \
                     SET \"Status\" = 'active', \"PayerId\" = COALESCE($1, \"PayerId\") \
                     WHERE \"CheckoutId\" = $2",
                    &[&payer_id, &checkout.id],
                )?;
            }
        }
    }

    let entry_id = Uuid::new_v4().simple().to_string();
    tx.execute(
        "INSERT INTO public.journal_entries (\"Id\",\"OrgId\",\"CheckoutId\",\"Currency\",\"CreatedAt\") \
         VALUES ($1,$2,$3,$4,$5)",
        &[&entry_id, &checkout.org_id, &checkout.id, &checkout.currency, &Utc::now()],
    )?;
    tx.execute(
        "INSERT INTO public.journal_lines (\"Id\",\"EntryId\",\"Account\",\"Dc\",\"Amount\") \
         VALUES ($1,$2,$3,$4,$5)",
        &[&Uuid::new_v4().simple().to_string(), &entry_id, &"cash", &"D", &checkout.amount],
    )?;
    tx.execute(
        "INSERT INTO public.journal_lines (\"Id\",\"EntryId\",\"Account\",\"Dc\",\"Amount\") \
         VALUES ($1,$2,$3,$4,$5)",
        &[&Uuid::new_v4().simple().to_string(), &entry_id, &"revenue", &"C", &checkout.amount],
    )?;

    let year = malaysia_time::year(Utc::now());
    let number = document_numbers::allocate(tx, &checkout.org_id, "RCPT", year)?;
    tx.execute(
        "INSERT INTO public.documents (\"Id\",\"OrgId\",\"CheckoutId\",\"Number\",\"Title\",\"CreatedAt\") \
         VALUES ($1,$2,$3,$4,$5,$6)",
        &[
            &Uuid::new_v4().simple().to_string(),
            &checkout.org_id,
            &checkout.id,
            &number,
            &"Official Receipt",
            &Utc::now(),
        ],
    )?;
    tx.execute(
        "INSERT INTO public.audit_events (\"Id\",\"OrgId\",\"Action\",\"At\") VALUES ($1,$2,$3,$4)",
        &[&Uuid::new_v4().simple().to_string(), &checkout.org_id, &"checkout.paid", &Utc::now()],
    )?;

    enqueue::try_add(
        tx,
        &checkout.org_id,
        &charge_id,
        envelope::COMPLETED,
        serde_json::json!({
            "checkout_id": checkout.id,
            "charge_id": charge_id,
            "amount": checkout.amount,
            "currency": checkout.currency,
            "provider": provider,
            "provider_ref": provider_ref,
            "number": number,
            "payer_name": checkout.payer_name,
        }),
    )?;

    // Deliberately no catch — see the module comment.
    Ok(FulfillOutcome { fulfilled: true, ..Default::default() })
}

struct CheckoutSnapshot {
    id: String,
    org_id: String,
    amount: Decimal,
    currency: String,
    status: String,
    interval: Option<String>,
    payment_link_id: Option<String>,
    payer_name: Option<String>,
    payer_email: Option<String>,
}

/// Book a pending late_pay refund for money that arrived after the checkout left
/// "open" — over capacity (008), or expired/failed concurrently (002). The caller
/// settles the returned id via the processor only after commit; rails with no
/// refund API leave the row pending as the ops marker.
fn book_late_refund(
    tx: &mut Transaction,
    checkout: &CheckoutSnapshot,
    provider: &str,
    provider_ref: Option<&str>,
) -> Result<FulfillOutcome, FulfillError> {
    let refund_id = Uuid::new_v4().simple().to_string();
    tx.execute(
        "INSERT INTO public.refunds \
         (\"Id\",\"OrgId\",\"CheckoutId\",\"ChargeId\",\"Amount\",\"Currency\",\"Status\",\
         \"Provider\",\"ProviderRef\",\"Reason\",\"IdempotencyKey\",\"CreatedAt\") \
         VALUES ($1,$2,$3,NULL,$4,$5,'pending',$6,$7,'late_pay',NULL,$8)",
        &[
            &refund_id,
            &checkout.org_id,
            &checkout.id,
            &checkout.amount,
            &checkout.currency,
            &provider,
            &provider_ref,
            &Utc::now(),
        ],
    )?;
    Ok(FulfillOutcome {
        fulfilled: false,
        pending_late_refund_id: Some(refund_id),
        late_refund_amount_minor: Some(currency::to_minor(checkout.amount)),
    })
}
