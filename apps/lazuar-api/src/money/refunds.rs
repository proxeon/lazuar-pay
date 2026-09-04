//! Port of `Money/RefundEndpoints.Create` — the money-path heart.
//!
//! Flow: idempotency pre-check → reserve (charge FOR UPDATE, pending row counts
//! against remainder) → processor call → settle (re-lock, recompute, journal,
//! receipt, audit, webhook enqueue). Three processor outcomes map to three
//! different reservation behaviors (see `rails::remote::RefundRemoteError`).
//!
//! Invariants carried from issues/001, each enforced at a marked site:
//! - 001: ambiguous outcomes stay `pending` — capacity stays reserved.
//! - 001: deterministic refund id from (org, key) — retries reuse the processor
//!   idempotency key instead of minting a fresh one.
//! - 009/010/012: filtered unique index, settle-time recompute, unique-violation
//!   replay — the concurrency armor on the reserve and settle phases.

use std::str::FromStr;

use chrono::{DateTime, Utc};
use postgres::error::SqlState;
use rust_decimal::Decimal;
use serde::Serialize;
use sha2::{Digest, Sha256};
use uuid::Uuid;

use crate::money::document_numbers;
use crate::money::malaysia_time;
use crate::rails::remote::{ChargeRef, RefundRemoteError, Refunder};
use crate::webhooks::envelope;
use crate::webhooks::enqueue;

// ---------------------------------------------------------------------------
// Rows and views
// ---------------------------------------------------------------------------

#[derive(Debug, Clone)]
pub struct RefundRow {
    pub id: String,
    pub org_id: String,
    pub checkout_id: String,
    pub charge_id: Option<String>,
    pub amount: Decimal,
    pub currency: String,
    pub status: String,
    pub provider: String,
    pub provider_ref: Option<String>,
    pub reason: String,
    pub idempotency_key: Option<String>,
    pub created_at: DateTime<Utc>,
}

#[derive(Debug, Clone, Serialize)]
pub struct RefundView {
    pub id: String,
    pub org_id: String,
    pub checkout_id: String,
    pub charge_id: Option<String>,
    pub amount: Decimal,
    pub currency: String,
    pub status: String,
    pub provider: String,
    pub reason: String,
    pub number: Option<String>,
    pub created_at: DateTime<Utc>,
}

impl RefundRow {
    pub fn view(&self, number: Option<String>) -> RefundView {
        RefundView {
            id: self.id.clone(),
            org_id: self.org_id.clone(),
            checkout_id: self.checkout_id.clone(),
            charge_id: self.charge_id.clone(),
            amount: self.amount,
            currency: self.currency.clone(),
            status: self.status.clone(),
            provider: self.provider.clone(),
            reason: self.reason.clone(),
            number,
            created_at: self.created_at,
        }
    }
}

/// Input mirroring `CreateRefundRequest` + the `Idempotency-Key` header
/// (header wins when both are present).
#[derive(Debug, Clone)]
pub struct CreateRefund {
    pub checkout_id: String,
    pub amount: Option<Decimal>,
    pub idempotency_key: Option<String>,
}

#[derive(Debug)]
pub enum CreateRefundOutcome {
    Created { refund: RefundView, number: String },
    Replayed(RefundView),
    /// 400 "checkout_id is required"
    CheckoutIdRequired,
    /// 404 "charge not found"
    ChargeNotFound,
    /// 404 "checkout not found" (also covers cross-org reads)
    CheckoutNotFound,
    /// 409 "already refunded"
    AlreadyRefunded,
    /// 400 "amount must be within the refundable remainder"
    AmountOutOfRange,
    /// 409 "idempotency key reused with a different body"
    Conflict,
    /// 400 — unsupported/unconfigured rail; nothing moved, reservation released.
    UnsupportedRail(String),
    /// 502 — definitive processor no; nothing moved, reservation released.
    ProcessorRejected,
    /// 502 "refund outcome unknown — held pending for reconciliation" (issue 001).
    AmbiguousOutcome,
    /// 500 "refund reservation conflict" — the 012 loser when no winner is answerable.
    ReservationConflict,
}

// ---------------------------------------------------------------------------
// Deterministic refund id (issue 001)
// ---------------------------------------------------------------------------

/// SHA-256 → Guid ("N" format): retries of the same logical refund reuse both
/// the row id and the processor idempotency key, so a retry after a lost
/// response cannot execute a second processor refund.
pub fn stable_refund_id(org_id: &str, idempotency_key: &str) -> String {
    let hash = Sha256::digest(format!("lazuar-refund:{org_id}:{idempotency_key}").as_bytes());
    let uuid = Uuid::from_slice(&hash[0..16]).expect("16 bytes is a valid uuid");
    uuid.simple().to_string()
}

// ---------------------------------------------------------------------------
// Create flow
// ---------------------------------------------------------------------------

const CHARGE_COLUMNS: &str = "\
    SELECT \"Id\",\"OrgId\",\"CheckoutId\",\"Provider\",\"ProviderRef\",\"Amount\",\
    \"Currency\",\"Status\" FROM public.charges";

fn charge_from_row(row: &postgres::Row) -> ChargeRef {
    ChargeRef {
        id: row.get("Id"),
        org_id: row.get("OrgId"),
        checkout_id: row.get("CheckoutId"),
        provider: row.get("Provider"),
        provider_ref: row.get("ProviderRef"),
        amount: row.get("Amount"),
        currency: row.get("Currency"),
        status: row.get("Status"),
    }
}

fn refund_row_from_row(row: &postgres::Row) -> RefundRow {
    RefundRow {
        id: row.get("Id"),
        org_id: row.get("OrgId"),
        checkout_id: row.get("CheckoutId"),
        charge_id: row.get("ChargeId"),
        amount: row.get("Amount"),
        currency: row.get("Currency"),
        status: row.get("Status"),
        provider: row.get("Provider"),
        provider_ref: row.get("ProviderRef"),
        reason: row.get("Reason"),
        idempotency_key: row.get("IdempotencyKey"),
        created_at: row.get("CreatedAt"),
    }
}

const REFUND_COLUMNS: &str = "\
    SELECT \"Id\",\"OrgId\",\"CheckoutId\",\"ChargeId\",\"Amount\",\"Currency\",\
    \"Status\",\"Provider\",\"ProviderRef\",\"Reason\",\"IdempotencyKey\",\"CreatedAt\" \
    FROM public.refunds";

fn normalized_key(key: &Option<String>) -> Option<&str> {
    key.as_deref().map(str::trim).filter(|k| !k.is_empty())
}

/// The full Create flow. Returns typed outcomes; the HTTP layer maps them
/// to statuses (Phase 5).
#[allow(clippy::too_many_lines)]
pub fn create_refund(
    conn: &mut postgres::Client,
    org_id: &str,
    input: &CreateRefund,
    remote: &dyn Refunder,
) -> Result<CreateRefundOutcome, postgres::Error> {
    let checkout_id = input.checkout_id.trim();
    if checkout_id.is_empty() {
        return Ok(CreateRefundOutcome::CheckoutIdRequired);
    }

    let idempotency = normalized_key(&input.idempotency_key).map(str::to_string);

    // Read-side idempotency pre-check: same key answers the existing row.
    if let Some(key) = &idempotency {
        if let Some(existing) =
            find_by_idempotency_key(conn, org_id, key)?
        {
            if existing.checkout_id != checkout_id
                || input.amount.is_some_and(|amt| amt != existing.amount)
            {
                return Ok(CreateRefundOutcome::Conflict);
            }
            return Ok(CreateRefundOutcome::Replayed(existing.view(None)));
        }
    }

    let charge = conn
        .query_opt(
            &format!(
                "{CHARGE_COLUMNS} WHERE \"OrgId\" = $1 AND \"CheckoutId\" = $2"
            ),
            &[&org_id, &checkout_id],
        )?
        .map(|row| charge_from_row(&row));
    let Some(charge) = charge else {
        return Ok(CreateRefundOutcome::ChargeNotFound);
    };

    let checkout = conn.query_opt(
        "SELECT \"OrgId\" FROM public.checkouts WHERE \"Id\" = $1",
        &[&checkout_id],
    )?;
    match checkout {
        None => return Ok(CreateRefundOutcome::CheckoutNotFound),
        Some(row) => {
            let row_org: String = row.get(0);
            if row_org != org_id {
                return Ok(CreateRefundOutcome::CheckoutNotFound);
            }
        }
    }

    // Issue 001: deterministic refund id — see `stable_refund_id`.
    let refund_id = match &idempotency {
        Some(key) => stable_refund_id(org_id, key),
        None => Uuid::new_v4().simple().to_string(),
    };

    // ----- Reserve: pending row + charge lock, before money moves -----
    let refund_amount; // the amount reserved — what the processor and journal see
    {
        let mut reserve_tx = conn.transaction()?;

        let locked = reserve_tx.query_one(
            &format!("{CHARGE_COLUMNS} WHERE \"Id\" = $1 FOR UPDATE"),
            &[&charge.id],
        )?;
        let charge = charge_from_row(&locked);

        if charge.status == "refunded" {
            reserve_tx.rollback()?;
            return replay_or(
                conn,
                org_id,
                checkout_id,
                &idempotency,
                input.amount,
                CreateRefundOutcome::AlreadyRefunded,
            );
        }

        let reserved: Decimal = reserve_tx
            .query_one(
                "SELECT COALESCE(SUM(\"Amount\"), 0) FROM public.refunds \
                 WHERE \"ChargeId\" = $1 AND (\"Status\" = 'succeeded' OR \"Status\" = 'pending')",
                &[&charge.id],
            )?
            .get(0);
        let remaining = charge.amount - reserved;
        if remaining <= Decimal::ZERO {
            reserve_tx.rollback()?;
            return replay_or(
                conn,
                org_id,
                checkout_id,
                &idempotency,
                input.amount,
                CreateRefundOutcome::AlreadyRefunded,
            );
        }

        let amount = input.amount.unwrap_or(remaining);
        if amount <= Decimal::ZERO || amount > remaining {
            reserve_tx.rollback()?;
            return Ok(CreateRefundOutcome::AmountOutOfRange);
        }
        refund_amount = amount;

        let insert = reserve_tx.execute(
            "INSERT INTO public.refunds \
             (\"Id\",\"OrgId\",\"CheckoutId\",\"ChargeId\",\"Amount\",\"Currency\",\
             \"Status\",\"Provider\",\"ProviderRef\",\"Reason\",\"IdempotencyKey\",\"CreatedAt\") \
             VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)",
            &[
                &refund_id,
                &org_id,
                &checkout_id,
                &charge.id,
                &amount,
                &charge.currency,
                &"pending",
                &charge.provider,
                &charge.provider_ref,
                &"merchant",
                &idempotency,
                &Utc::now(),
            ],
        );

        if let Err(err) = insert {
            // Issue 012: two concurrent same-key refunds both pass the read-side
            // pre-check; the loser hits the filtered unique index. The loser is a
            // replay by contract — answer with the winner's row, never a raw 500.
            let is_unique = err.as_db_error().map(|db| db.code()) == Some(&SqlState::UNIQUE_VIOLATION);
            reserve_tx.rollback()?;
            if is_unique && idempotency.is_some() {
                return replay_or(
                    conn,
                    org_id,
                    checkout_id,
                    &idempotency,
                    input.amount,
                    CreateRefundOutcome::ReservationConflict,
                );
            }
            return Err(err);
        }

        reserve_tx.commit()?;
        charge.id
    };

    // ----- Processor call — the three-way outcome taxonomy -----
    let charge_for_remote = charge.clone();
    let remote_result =
        remote.refund_charge(&charge_for_remote, refund_amount, &refund_id);
    match remote_result {
        Err(RefundRemoteError::UnsupportedRail(message)) => {
            // Nothing could have moved — releasing the reservation is safe.
            conn.execute(
                "UPDATE public.refunds SET \"Status\" = 'failed' WHERE \"Id\" = $1",
                &[&refund_id],
            )?;
            return Ok(CreateRefundOutcome::UnsupportedRail(message));
        }
        Err(RefundRemoteError::ProcessorRejected(_)) => {
            // Definitive <500 answer: no money moved — safe to release.
            conn.execute(
                "UPDATE public.refunds SET \"Status\" = 'failed' WHERE \"Id\" = $1",
                &[&refund_id],
            )?;
            return Ok(CreateRefundOutcome::ProcessorRejected);
        }
        Err(RefundRemoteError::OutcomeUnknown(_)) => {
            // Issue 001: the row STAYS pending. Capacity remains reserved, same-key
            // retries replay this row, and ops reconcile before releasing it.
            return Ok(CreateRefundOutcome::AmbiguousOutcome);
        }
        Ok(()) => {}
    }

    // ----- Settle: fresh transaction, re-lock, recompute (issue 010) -----
    let number = {
        let mut settle_tx = conn.transaction()?;

        let locked = settle_tx.query_one(
            &format!("{CHARGE_COLUMNS} WHERE \"Id\" = $1 FOR UPDATE"),
            &[&charge.id],
        )?;
        let charge = charge_from_row(&locked);

        // Pending rows count as refunded for status purposes, mirroring reservation
        // semantics: a pending row reserves capacity, so the charge must not read as
        // more refundable than it is. The reserve-time `remaining` snapshot is stale
        // by now — a concurrent partial refund may have committed while we were at
        // the processor — so the total is recomputed from persisted rows.
        let refunded_total: Decimal = settle_tx
            .query_one(
                "SELECT COALESCE(SUM(\"Amount\"), 0) FROM public.refunds \
                 WHERE \"ChargeId\" = $1 AND (\"Status\" = 'succeeded' OR \"Status\" = 'pending')",
                &[&charge.id],
            )?
            .get(0);
        let final_status = if refunded_total >= charge.amount {
            "refunded"
        } else {
            "partially_refunded"
        };
        settle_tx.execute(
            "UPDATE public.charges SET \"Status\" = $1 WHERE \"Id\" = $2",
            &[&final_status, &charge.id],
        )?;
        settle_tx.execute(
            "UPDATE public.refunds SET \"Status\" = 'succeeded' WHERE \"Id\" = $1",
            &[&refund_id],
        )?;

        // Two-line journal: revenue D / cash C, same transaction as everything above.
        let entry_id = Uuid::new_v4().simple().to_string();
        settle_tx.execute(
            "INSERT INTO public.journal_entries (\"Id\",\"OrgId\",\"CheckoutId\",\"Currency\",\"CreatedAt\") \
             VALUES ($1,$2,$3,$4,$5)",
            &[&entry_id, &org_id, &checkout_id, &charge.currency, &Utc::now()],
        )?;
        settle_tx.execute(
            "INSERT INTO public.journal_lines (\"Id\",\"EntryId\",\"Account\",\"Dc\",\"Amount\") \
             VALUES ($1,$2,$3,$4,$5)",
            &[
                &Uuid::new_v4().simple().to_string(),
                &entry_id,
                &"revenue",
                &"D",
                &refund_amount,
            ],
        )?;
        settle_tx.execute(
            "INSERT INTO public.journal_lines (\"Id\",\"EntryId\",\"Account\",\"Dc\",\"Amount\") \
             VALUES ($1,$2,$3,$4,$5)",
            &[
                &Uuid::new_v4().simple().to_string(),
                &entry_id,
                &"cash",
                &"C",
                &refund_amount,
            ],
        )?;

        let number = document_numbers::allocate(
            &mut settle_tx,
            org_id,
            "REF",
            malaysia_time::year(Utc::now()),
        )?;
        settle_tx.execute(
            "INSERT INTO public.documents (\"Id\",\"OrgId\",\"CheckoutId\",\"Number\",\"Title\",\"CreatedAt\") \
             VALUES ($1,$2,$3,$4,$5,$6)",
            &[
                &Uuid::new_v4().simple().to_string(),
                &org_id,
                &checkout_id,
                &number,
                &"Refund",
                &Utc::now(),
            ],
        )?;

        settle_tx.execute(
            "INSERT INTO public.audit_events (\"Id\",\"OrgId\",\"Action\",\"At\") \
             VALUES ($1,$2,$3,$4)",
            &[
                &Uuid::new_v4().simple().to_string(),
                &org_id,
                &"refund.created",
                &Utc::now(),
            ],
        )?;

        // The pending refund row flips to succeeded only now that the processor
        // accepted the refund — the enqueue must reflect that state.
        enqueue::try_add(
            &mut settle_tx,
            org_id,
            &refund_id,
            envelope::REFUND_CREATED,
            serde_json::json!({
                "refund_id": refund_id,
                "checkout_id": checkout_id,
                "charge_id": charge.id,
                "amount": refund_amount,
                "currency": charge.currency,
                "number": number,
                "provider": charge.provider,
            }),
        )?;

        settle_tx.commit()?;
        number
    };

    let row = find_by_id(conn, &refund_id)?.expect("settled refund must be readable");
    Ok(CreateRefundOutcome::Created { refund: row.view(Some(number.clone())), number })
}

fn find_by_idempotency_key(
    conn: &mut postgres::Client,
    org_id: &str,
    key: &str,
) -> Result<Option<RefundRow>, postgres::Error> {
    conn.query_opt(
        &format!("{REFUND_COLUMNS} WHERE \"OrgId\" = $1 AND \"IdempotencyKey\" = $2"),
        &[&org_id, &key],
    )
    .map(|opt| opt.map(|row| refund_row_from_row(&row)))
}

fn find_by_id(conn: &mut postgres::Client, id: &str) -> Result<Option<RefundRow>, postgres::Error> {
    conn.query_opt(&format!("{REFUND_COLUMNS} WHERE \"Id\" = $1"), &[&id])
        .map(|opt| opt.map(|row| refund_row_from_row(&row)))
}

/// Two concurrent requests with the same key serialize on the charge lock; the
/// loser reaches an already-reserved charge. That loser is a replay by contract
/// — it gets the original row, not the fallback error.
#[allow(clippy::too_many_arguments)]
fn replay_or(
    conn: &mut postgres::Client,
    org_id: &str,
    checkout_id: &str,
    idempotency: &Option<String>,
    amount: Option<Decimal>,
    fallback: CreateRefundOutcome,
) -> Result<CreateRefundOutcome, postgres::Error> {
    let Some(key) = idempotency else {
        return Ok(fallback);
    };
    let Some(existing) = find_by_idempotency_key(conn, org_id, key)? else {
        return Ok(fallback);
    };
    if existing.checkout_id != checkout_id || amount.is_some_and(|amt| amt != existing.amount) {
        return Ok(CreateRefundOutcome::Conflict);
    }
    Ok(CreateRefundOutcome::Replayed(existing.view(None)))
}

/// HTTP-layer placeholder refunder: nothing settles at a processor here;
/// ambiguous rows stay pending as the ops marker. Real rails replace this
/// per-environment when the hosted mints wire the live remotes.
pub struct NoopRefunder;

impl Refunder for NoopRefunder {
    fn refund_charge(&self, _: &ChargeRef, _: Decimal, _: &str) -> Result<(), RefundRemoteError> {
        Err(RefundRemoteError::OutcomeUnknown(
            "no processor configured for this rail — held pending for reconciliation".into(),
        ))
    }
}

#[derive(Debug, Serialize)]
pub struct RefundPage {
    pub items: Vec<RefundView>,
    pub next_cursor: Option<String>,
}

/// Org-scoped listing, newest first, cursor = last id (issue 015 org-scope).
pub fn list_refunds(
    conn: &mut postgres::Client,
    org_id: &str,
    limit: Option<i64>,
    after: Option<&str>,
) -> Result<RefundPage, postgres::Error> {
    let take = limit.unwrap_or(50).clamp(1, 200);

    let cursor: Option<RefundRow> = match after.map(str::trim).filter(|a| !a.is_empty()) {
        Some(after_id) => conn
            .query_opt(
                &format!("{REFUND_COLUMNS} WHERE \"OrgId\" = $1 AND \"Id\" = $2"),
                &[&org_id, &after_id],
            )?
            .map(|row| refund_row_from_row(&row)),
        None => None,
    };

    let rows = match &cursor {
        Some(cursor_row) => conn.query(
            &format!(
                "{REFUND_COLUMNS} WHERE \"OrgId\" = $1 \
                 AND (\"CreatedAt\" < $2 OR (\"CreatedAt\" = $2 AND \"Id\" < $3)) \
                 ORDER BY \"CreatedAt\" DESC, \"Id\" DESC LIMIT $4"
            ),
            &[&org_id, &cursor_row.created_at, &cursor_row.id, &(take + 1)],
        )?,
        None => conn.query(
            &format!(
                "{REFUND_COLUMNS} WHERE \"OrgId\" = $1 \
                 ORDER BY \"CreatedAt\" DESC, \"Id\" DESC LIMIT $2"
            ),
            &[&org_id, &(take + 1)],
        )?,
    };

    let mut items: Vec<RefundView> = rows.iter().map(|row| refund_row_from_row(row).view(None)).collect();
    let mut next_cursor = None;
    if items.len() as i64 > take {
        items.truncate(take as usize);
        next_cursor = items.last().map(|last| last.id.clone());
    }
    Ok(RefundPage { items, next_cursor })
}
