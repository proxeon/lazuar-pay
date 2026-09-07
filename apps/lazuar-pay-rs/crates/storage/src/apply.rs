//! One TX composer of fold + journal + outbox (033/02).
//!
//! Order for Paid inject (load-bearing):
//! inbound unique → payment `FOR UPDATE` → link occupancy (008) → Exact integrity
//! → pause gate (Take only) → `propose` → persist charge xor late refund + journal
//! → CAS version (002) → outbound.
//!
//! Integrity mismatch and pause **rollback inbound** so the PSP retries.
//! Occupancy count **excludes this payment** (a lone open child must still Take).

use domain::command::check_start_attempt;
use domain::fold::{propose, FoldInput, IntakeKind, OccupancySnap, Projection};
use domain::journal::JournalEntry;
use domain::money::{integrity, AmountPolicy, Integrity, Money, RateLock};
use domain::proof::{sufficiency, ConfirmPolicy, Proof, ProofSufficiency};
use domain::rail::{ConnectorRefs, HostedSession, MethodId, RailId};
use domain::wire::outbound_event;
use domain::{
    Attempt, AttemptId, ChargeId, ExceptionStatus, Intake, Payment, PaymentId, PaymentLinkId,
    PaymentStatus, PublicToken, RefundId, Settlement, SettlementId, SettlementState, TenantId,
    TerminalReason,
};
use sqlx::{PgPool, Postgres, Row, Transaction};
use time::OffsetDateTime;
use uuid::Uuid;

use crate::error::ApplyError;
use crate::rows;

pub struct MintSpec {
    pub tenant_id: TenantId,
    pub public_token: PublicToken,
    pub quoted: Money,
    pub expires_at: OffsetDateTime,
    pub monitoring_until: OffsetDateTime,
    pub payment_link_id: Option<PaymentLinkId>,
    pub slot_key: Option<String>,
    pub success_url: Option<String>,
    pub cancel_url: Option<String>,
    pub rail: RailId,
}

pub enum ApplyCmd {
    Mint(MintSpec),
    StartAttempt {
        payment_id: PaymentId,
        rail: RailId,
    },
    RecordSession {
        attempt_id: AttemptId,
        session: HostedSession,
    },
    InjectPaid {
        tenant_id: TenantId,
        rail: RailId,
        proof_id: String,
        payment_id: PaymentId,
        attempt_id: AttemptId,
        received: Money,
        proof: Proof,
        now: OffsetDateTime,
        refs: ConnectorRefs,
    },
    InjectFailed {
        tenant_id: TenantId,
        rail: RailId,
        proof_id: String,
        payment_id: PaymentId,
        attempt_id: AttemptId,
        reason: TerminalReason,
        now: OffsetDateTime,
    },
    ExpireClock {
        payment_id: PaymentId,
        now: OffsetDateTime,
    },
    WatchTimeout {
        payment_id: PaymentId,
        now: OffsetDateTime,
    },
    MerchantRefund {
        payment_id: PaymentId,
        amount: Money,
        idempotency_key: String,
        request_hash: String,
        now: OffsetDateTime,
    },
}

#[derive(Debug)]
pub enum ApplyOutcome {
    Applied {
        payment_id: PaymentId,
        projection: Projection,
    },
    Minted {
        payment_id: PaymentId,
    },
    Started {
        payment_id: PaymentId,
        attempt_id: AttemptId,
    },
    Duplicate,
    SessionResume {
        url: String,
        session_id: String,
    },
    RefundReplay {
        refund_id: RefundId,
    },
}

pub async fn apply(pool: &PgPool, cmd: ApplyCmd) -> Result<ApplyOutcome, ApplyError> {
    let mut tx = pool.begin().await?;
    let out = match cmd {
        ApplyCmd::Mint(spec) => mint(&mut tx, spec).await?,
        ApplyCmd::StartAttempt { payment_id, rail } => {
            start_attempt(&mut tx, payment_id, rail).await?
        }
        ApplyCmd::RecordSession {
            attempt_id,
            session,
        } => record_session(&mut tx, attempt_id, session).await?,
        ApplyCmd::InjectPaid {
            tenant_id,
            rail,
            proof_id,
            payment_id,
            attempt_id,
            received,
            proof,
            now,
            refs,
        } => {
            inject_paid(
                &mut tx, tenant_id, rail, proof_id, payment_id, attempt_id, received, proof, now,
                refs,
            )
            .await?
        }
        ApplyCmd::InjectFailed {
            tenant_id,
            rail,
            proof_id,
            payment_id,
            attempt_id,
            reason,
            now,
        } => {
            inject_failed(
                &mut tx, tenant_id, rail, proof_id, payment_id, attempt_id, reason, now,
            )
            .await?
        }
        ApplyCmd::ExpireClock { payment_id, now } => {
            expire_clock(&mut tx, payment_id, now, false).await?
        }
        ApplyCmd::WatchTimeout { payment_id, now } => {
            watch_timeout(&mut tx, payment_id, now).await?
        }
        ApplyCmd::MerchantRefund {
            payment_id,
            amount,
            idempotency_key,
            request_hash,
            now,
        } => {
            merchant_refund(
                &mut tx,
                payment_id,
                amount,
                &idempotency_key,
                &request_hash,
                now,
            )
            .await?
        }
    };
    tx.commit().await?;
    Ok(out)
}

async fn mint(
    tx: &mut Transaction<'_, Postgres>,
    spec: MintSpec,
) -> Result<ApplyOutcome, ApplyError> {
    let id = PaymentId::from_uuid(Uuid::new_v4());
    let quoted = spec.quoted;
    sqlx::query(
        r#"
        INSERT INTO pay_rs.payments (
            id, tenant_id, public_token, amount_minor, currency, exponent,
            status, expires_at, monitoring_until, payment_link_id, slot_key,
            success_url, cancel_url, version
        ) VALUES (
            $1, $2, $3, $4, $5, $6,
            'open', $7, $8, $9, $10, $11, $12, 1
        )
        "#,
    )
    .bind(id.as_uuid())
    .bind(spec.tenant_id.as_str())
    .bind(spec.public_token.as_str())
    .bind(quoted.minor() as i64)
    .bind(quoted.currency().code.as_str())
    .bind(i16::from(quoted.currency().exponent))
    .bind(spec.expires_at)
    .bind(spec.monitoring_until)
    .bind(spec.payment_link_id.map(|l| l.as_uuid()))
    .bind(spec.slot_key.as_deref())
    .bind(spec.success_url.as_deref())
    .bind(spec.cancel_url.as_deref())
    .execute(&mut **tx)
    .await
    .map_err(ApplyError::from_sql)?;
    let attempt_id = AttemptId::from_uuid(Uuid::new_v4());
    let method = MethodId::new(quoted.currency().code, spec.rail);
    sqlx::query(
        r#"
        INSERT INTO pay_rs.attempts (
            id, payment_id, tenant_id, rail, method,
            amount_minor, currency, exponent, status, version
        ) VALUES (
            $1, $2, $3, $4, $5,
            $6, $7, $8, 'created', 1
        )
        "#,
    )
    .bind(attempt_id.as_uuid())
    .bind(id.as_uuid())
    .bind(spec.tenant_id.as_str())
    .bind(spec.rail.as_str())
    .bind(method.to_string())
    .bind(quoted.minor() as i64)
    .bind(quoted.currency().code.as_str())
    .bind(i16::from(quoted.currency().exponent))
    .execute(&mut **tx)
    .await
    .map_err(ApplyError::from_sql)?;
    Ok(ApplyOutcome::Minted { payment_id: id })
}

async fn start_attempt(
    tx: &mut Transaction<'_, Postgres>,
    payment_id: PaymentId,
    rail: RailId,
) -> Result<ApplyOutcome, ApplyError> {
    let payment = load_payment(tx, payment_id).await?;
    let attempts = load_attempts(tx, payment_id).await?;
    match check_start_attempt(&payment, &attempts, rail) {
        Err(domain::Illegal::NotStartable) => return Err(ApplyError::NotStartable),
        Err(domain::Illegal::LiveAttemptExists) => return Err(ApplyError::LiveAttemptExists),
        Err(e) => return Err(ApplyError::Domain(e)),
        Ok(()) => {}
    }
    if let Some(existing) = attempts
        .iter()
        .find(|a| a.status == domain::AttemptStatus::Created && a.rail == rail)
    {
        sqlx::query(
            r#"
            UPDATE pay_rs.attempts
               SET status = 'pending', updated_at = now(), version = version + 1
             WHERE id = $1 AND status = 'created'
            "#,
        )
        .bind(existing.id.as_uuid())
        .execute(&mut **tx)
        .await?;
        return Ok(ApplyOutcome::Started {
            payment_id,
            attempt_id: existing.id,
        });
    }
    let attempt_id = AttemptId::from_uuid(Uuid::new_v4());
    let method = MethodId::new(payment.quoted.currency().code, rail);
    sqlx::query(
        r#"
        INSERT INTO pay_rs.attempts (
            id, payment_id, tenant_id, rail, method,
            amount_minor, currency, exponent, status, version
        ) VALUES (
            $1, $2, $3, $4, $5,
            $6, $7, $8, 'created', 1
        )
        "#,
    )
    .bind(attempt_id.as_uuid())
    .bind(payment_id.as_uuid())
    .bind(payment.tenant_id.as_str())
    .bind(rail.as_str())
    .bind(method.to_string())
    .bind(payment.quoted.minor() as i64)
    .bind(payment.quoted.currency().code.as_str())
    .bind(i16::from(payment.quoted.currency().exponent))
    .execute(&mut **tx)
    .await
    .map_err(ApplyError::from_sql)?;
    sqlx::query(
        r#"
        UPDATE pay_rs.attempts
           SET status = 'pending', updated_at = now(), version = version + 1
         WHERE id = $1
        "#,
    )
    .bind(attempt_id.as_uuid())
    .execute(&mut **tx)
    .await?;
    Ok(ApplyOutcome::Started {
        payment_id,
        attempt_id,
    })
}

/// Issue 007: conditional session persist. 0 rows → resume the winner's URL.
async fn record_session(
    tx: &mut Transaction<'_, Postgres>,
    attempt_id: AttemptId,
    session: HostedSession,
) -> Result<ApplyOutcome, ApplyError> {
    let res = sqlx::query(
        r#"
        UPDATE pay_rs.attempts
           SET session_url = $2,
               session_id = $3,
               status = 'session_live',
               updated_at = now(),
               version = version + 1
         WHERE id = $1 AND status = 'pending' AND session_url IS NULL
        "#,
    )
    .bind(attempt_id.as_uuid())
    .bind(&session.url)
    .bind(&session.session_id)
    .execute(&mut **tx)
    .await?;
    if res.rows_affected() == 1 {
        if session.url.starts_with("solana:") {
            let row = sqlx::query(
                r#"
                SELECT a.payment_id, p.tenant_id
                  FROM pay_rs.attempts a
                  JOIN pay_rs.payments p ON p.id = a.payment_id
                 WHERE a.id = $1
                "#,
            )
            .bind(attempt_id.as_uuid())
            .fetch_one(&mut **tx)
            .await?;
            let tenant: String = row.try_get("tenant_id")?;
            sqlx::query(
                r#"
                INSERT INTO pay_rs.reservations (tenant_id, attempt_id, chain, locator)
                VALUES ($1, $2, 'solana', $3)
                "#,
            )
            .bind(&tenant)
            .bind(attempt_id.as_uuid())
            .bind(&session.session_id)
            .execute(&mut **tx)
            .await
            .map_err(ApplyError::from_sql)?;
        }
        let payment_id: Uuid =
            sqlx::query_scalar("SELECT payment_id FROM pay_rs.attempts WHERE id = $1")
                .bind(attempt_id.as_uuid())
                .fetch_one(&mut **tx)
                .await?;
        return Ok(ApplyOutcome::Applied {
            payment_id: PaymentId::from_uuid(payment_id),
            projection: Projection {
                status: PaymentStatus::Open,
                exception: ExceptionStatus::None,
                intake: Intake::Open,
                intake_kind: IntakeKind::Keep,
                terminal_reason: TerminalReason::None,
                attempt_updates: vec![],
            },
        });
    }
    let row = sqlx::query("SELECT session_url, session_id FROM pay_rs.attempts WHERE id = $1")
        .bind(attempt_id.as_uuid())
        .fetch_optional(&mut **tx)
        .await?
        .ok_or(ApplyError::NotFound)?;
    let url: Option<String> = row.try_get("session_url")?;
    let sid: Option<String> = row.try_get("session_id")?;
    Ok(ApplyOutcome::SessionResume {
        url: url.ok_or(ApplyError::Conflict)?,
        session_id: sid.ok_or(ApplyError::Conflict)?,
    })
}

#[allow(clippy::too_many_arguments)]
async fn inject_paid(
    tx: &mut Transaction<'_, Postgres>,
    tenant_id: TenantId,
    rail: RailId,
    proof_id: String,
    payment_id: PaymentId,
    attempt_id: AttemptId,
    received: Money,
    proof: Proof,
    now: OffsetDateTime,
    refs: ConnectorRefs,
) -> Result<ApplyOutcome, ApplyError> {
    if !insert_inbound(tx, tenant_id.as_str(), rail.as_str(), &proof_id).await? {
        return Ok(ApplyOutcome::Duplicate);
    }
    let payment = load_payment(tx, payment_id).await?;
    let attempts = load_attempts(tx, payment_id).await?;
    let mut settlements = load_settlements(tx, payment_id).await?;
    let occupancy = occupancy_snap(tx, &payment).await?;

    let quoted_rail = attempts
        .iter()
        .find(|a| a.id == attempt_id)
        .map(|a| a.quoted_rail)
        .unwrap_or(payment.quoted);
    if integrity(quoted_rail, received, AmountPolicy::Exact) != Integrity::Ok {
        return Err(ApplyError::Integrity);
    }

    let paused = charges_paused(tx, payment.tenant_id.as_str()).await?;
    let state = match sufficiency(&proof, &ConfirmPolicy::V1) {
        ProofSufficiency::Confirmed => SettlementState::Confirmed,
        _ => SettlementState::Seen,
    };
    let new_settlement = Settlement {
        id: SettlementId::from_uuid(Uuid::new_v4()),
        attempt_id,
        received,
        proof,
        state,
    };
    settlements.push(new_settlement.clone());
    let proj = propose(&FoldInput {
        now,
        payment: &payment,
        attempts: &attempts,
        settlements: &settlements,
        occupancy,
        force_clock_expired: false,
    });
    if proj.intake_kind == IntakeKind::Take && paused {
        return Err(ApplyError::Paused);
    }
    persist_fold(
        tx,
        &payment,
        &proj,
        Some(&new_settlement),
        Some((&proof_id, rail)),
    )
    .await?;
    sqlx::query(
        r#"
        UPDATE pay_rs.attempts
           SET capture_id = COALESCE($2, capture_id),
               network_id = COALESCE($3, network_id),
               session_id = COALESCE($4, session_id)
         WHERE id = $1
        "#,
    )
    .bind(attempt_id.as_uuid())
    .bind(refs.capture_id.as_deref())
    .bind(refs.network_id.as_deref())
    .bind(refs.session_id.as_deref())
    .execute(&mut **tx)
    .await?;
    Ok(ApplyOutcome::Applied {
        payment_id,
        projection: proj,
    })
}

#[allow(clippy::too_many_arguments)]
async fn inject_failed(
    tx: &mut Transaction<'_, Postgres>,
    tenant_id: TenantId,
    rail: RailId,
    proof_id: String,
    payment_id: PaymentId,
    attempt_id: AttemptId,
    reason: TerminalReason,
    now: OffsetDateTime,
) -> Result<ApplyOutcome, ApplyError> {
    if !insert_inbound(tx, tenant_id.as_str(), rail.as_str(), &proof_id).await? {
        return Ok(ApplyOutcome::Duplicate);
    }
    sqlx::query(
        r#"
        UPDATE pay_rs.attempts
           SET status = 'failed', fail_reason = $2, updated_at = now(), version = version + 1
         WHERE id = $1
        "#,
    )
    .bind(attempt_id.as_uuid())
    .bind(rows::terminal_sql(reason))
    .execute(&mut **tx)
    .await?;
    fold_no_new_money(tx, payment_id, now, false).await
}

async fn expire_clock(
    tx: &mut Transaction<'_, Postgres>,
    payment_id: PaymentId,
    now: OffsetDateTime,
    force: bool,
) -> Result<ApplyOutcome, ApplyError> {
    fold_no_new_money(tx, payment_id, now, force).await
}

async fn watch_timeout(
    tx: &mut Transaction<'_, Postgres>,
    payment_id: PaymentId,
    now: OffsetDateTime,
) -> Result<ApplyOutcome, ApplyError> {
    let payment = load_payment(tx, payment_id).await?;
    if payment.occupancy.is_some() {
        return expire_clock(tx, payment_id, now, true).await;
    }
    sqlx::query(
        r#"
        UPDATE pay_rs.attempts
           SET status = 'failed', fail_reason = 'watch_timeout',
               updated_at = now(), version = version + 1
         WHERE payment_id = $1 AND status IN ('created','pending','session_live')
        "#,
    )
    .bind(payment_id.as_uuid())
    .execute(&mut **tx)
    .await?;
    fold_no_new_money(tx, payment_id, now, false).await
}

async fn fold_no_new_money(
    tx: &mut Transaction<'_, Postgres>,
    payment_id: PaymentId,
    now: OffsetDateTime,
    force_clock_expired: bool,
) -> Result<ApplyOutcome, ApplyError> {
    let payment = load_payment(tx, payment_id).await?;
    let attempts = load_attempts(tx, payment_id).await?;
    let settlements = load_settlements(tx, payment_id).await?;
    let occupancy = occupancy_snap(tx, &payment).await?;
    let proj = propose(&FoldInput {
        now,
        payment: &payment,
        attempts: &attempts,
        settlements: &settlements,
        occupancy,
        force_clock_expired,
    });
    persist_fold(tx, &payment, &proj, None, None).await?;
    Ok(ApplyOutcome::Applied {
        payment_id,
        projection: proj,
    })
}

/// Issue 010 remainder under `FOR UPDATE` on the charge. 012 idempotency key+hash.
async fn merchant_refund(
    tx: &mut Transaction<'_, Postgres>,
    payment_id: PaymentId,
    amount: Money,
    idempotency_key: &str,
    request_hash: &str,
    _now: OffsetDateTime,
) -> Result<ApplyOutcome, ApplyError> {
    let payment = load_payment(tx, payment_id).await?;
    let tenant = payment.tenant_id.as_str();
    let existing = sqlx::query(
        r#"
        SELECT resource_id, request_hash FROM pay_rs.idempotency_keys
         WHERE tenant_id = $1 AND key = $2
        "#,
    )
    .bind(tenant)
    .bind(idempotency_key)
    .fetch_optional(&mut **tx)
    .await?;
    if let Some(row) = existing {
        let hash: String = row.try_get("request_hash")?;
        if hash != request_hash {
            return Err(ApplyError::IdempotencyMismatch);
        }
        let rid: Uuid = row.try_get("resource_id")?;
        return Ok(ApplyOutcome::RefundReplay {
            refund_id: RefundId::from_uuid(rid),
        });
    }

    let charge = sqlx::query(
        r#"
        SELECT id, amount_minor, currency, exponent
          FROM pay_rs.charges
         WHERE payment_id = $1
         FOR UPDATE
        "#,
    )
    .bind(payment_id.as_uuid())
    .fetch_optional(&mut **tx)
    .await?
    .ok_or(ApplyError::NotFound)?;
    let charge_id: Uuid = charge.try_get("id")?;
    let charge_amt = rows::money_from_parts(
        charge.try_get("amount_minor")?,
        charge.try_get("currency")?,
        charge.try_get("exponent")?,
    )?;
    let used: i64 = sqlx::query_scalar(
        r#"
        SELECT COALESCE(SUM(amount_minor), 0)::bigint
          FROM pay_rs.refunds
         WHERE payment_id = $1 AND status IN ('pending','succeeded')
        "#,
    )
    .bind(payment_id.as_uuid())
    .fetch_one(&mut **tx)
    .await?;
    let remainder = charge_amt.minor() - i128::from(used);
    if amount.minor() > remainder {
        return Err(ApplyError::AlreadyRefunded);
    }
    let refund_id = RefundId::from_uuid(Uuid::new_v4());
    sqlx::query(
        r#"
        INSERT INTO pay_rs.idempotency_keys (
            tenant_id, key, resource_kind, resource_id, request_hash
        ) VALUES ($1, $2, 'refund', $3, $4)
        "#,
    )
    .bind(tenant)
    .bind(idempotency_key)
    .bind(refund_id.as_uuid())
    .bind(request_hash)
    .execute(&mut **tx)
    .await
    .map_err(ApplyError::from_sql)?;
    let rail: String = sqlx::query_scalar(
        r#"
        SELECT rail FROM pay_rs.attempts
         WHERE payment_id = $1
         ORDER BY created_at DESC
         LIMIT 1
        "#,
    )
    .bind(payment_id.as_uuid())
    .fetch_one(&mut **tx)
    .await?;
    sqlx::query(
        r#"
        INSERT INTO pay_rs.refunds (
            id, tenant_id, payment_id, charge_id,
            amount_minor, currency, exponent, status, rail, reason, idempotency_key
        ) VALUES (
            $1, $2, $3, $4,
            $5, $6, $7, 'pending', $8, 'merchant', $9
        )
        "#,
    )
    .bind(refund_id.as_uuid())
    .bind(tenant)
    .bind(payment_id.as_uuid())
    .bind(charge_id)
    .bind(amount.minor() as i64)
    .bind(amount.currency().code.as_str())
    .bind(i16::from(amount.currency().exponent))
    .bind(rail)
    .bind(idempotency_key)
    .execute(&mut **tx)
    .await?;
    Ok(ApplyOutcome::Applied {
        payment_id,
        projection: Projection {
            status: payment.status,
            exception: payment.exception,
            intake: payment.intake,
            intake_kind: IntakeKind::Keep,
            terminal_reason: payment.terminal_reason,
            attempt_updates: vec![],
        },
    })
}

async fn insert_inbound(
    tx: &mut Transaction<'_, Postgres>,
    tenant: &str,
    rail: &str,
    proof_id: &str,
) -> Result<bool, ApplyError> {
    let row = sqlx::query(
        r#"
        INSERT INTO pay_rs.inbound_events (tenant_id, rail, proof_id)
        VALUES ($1, $2, $3)
        ON CONFLICT ON CONSTRAINT inbound_events_pk DO NOTHING
        RETURNING proof_id
        "#,
    )
    .bind(tenant)
    .bind(rail)
    .bind(proof_id)
    .fetch_optional(&mut **tx)
    .await?;
    Ok(row.is_some())
}

async fn persist_fold(
    tx: &mut Transaction<'_, Postgres>,
    payment: &Payment,
    proj: &Projection,
    new_settlement: Option<&Settlement>,
    proof_meta: Option<(&str, RailId)>,
) -> Result<(), ApplyError> {
    if let Some(s) = new_settlement {
        let (kind, pid) = match &s.proof {
            Proof::PspWebhook { event_id, .. } => ("psp_webhook", event_id.as_str().to_string()),
            Proof::PspSync {
                connector_txn_id, ..
            } => ("psp_sync", connector_txn_id.clone()),
            Proof::ChainTx { txid, .. } => ("chain_tx", txid.as_str().to_string()),
        };
        let _ = proof_meta;
        sqlx::query(
            r#"
            INSERT INTO pay_rs.settlements (
                id, attempt_id, payment_id, tenant_id,
                amount_minor, currency, exponent, state, proof_kind, proof_id, finalized
            ) VALUES (
                $1, $2, $3, $4,
                $5, $6, $7, $10, $8, $9, $11
            )
            "#,
        )
        .bind(s.id.as_uuid())
        .bind(s.attempt_id.as_uuid())
        .bind(payment.id.as_uuid())
        .bind(payment.tenant_id.as_str())
        .bind(s.received.minor() as i64)
        .bind(s.received.currency().code.as_str())
        .bind(i16::from(s.received.currency().exponent))
        .bind(kind)
        .bind(pid)
        .bind(match s.state {
            SettlementState::Confirmed => "confirmed",
            SettlementState::Seen => "seen",
            SettlementState::Reorged => "reorged",
            SettlementState::Ignored => "ignored",
        })
        .bind(s.state == SettlementState::Confirmed)
        .execute(&mut **tx)
        .await?;
    }

    let mut intake = proj.intake;
    match proj.intake_kind {
        IntakeKind::Take => {
            let charge_id = ChargeId::from_uuid(Uuid::new_v4());
            sqlx::query(
                r#"
                INSERT INTO pay_rs.charges (
                    id, tenant_id, payment_id, amount_minor, currency, exponent, status
                ) VALUES ($1, $2, $3, $4, $5, $6, 'paid')
                "#,
            )
            .bind(charge_id.as_uuid())
            .bind(payment.tenant_id.as_str())
            .bind(payment.id.as_uuid())
            .bind(payment.quoted.minor() as i64)
            .bind(payment.quoted.currency().code.as_str())
            .bind(i16::from(payment.quoted.currency().exponent))
            .execute(&mut **tx)
            .await
            .map_err(ApplyError::from_sql)?;
            insert_journal(tx, payment, IntakeKind::Take).await?;
            insert_rcpt(tx, payment).await?;
            intake = Intake::Taken { charge_id };
        }
        IntakeKind::ReturnLate | IntakeKind::ReturnOverCapacity => {
            let refund_id = RefundId::from_uuid(Uuid::new_v4());
            let reason = rows::refund_reason_sql(proj.intake_kind).unwrap();
            let rail: String = sqlx::query_scalar(
                r#"
                SELECT COALESCE(
                    (SELECT rail FROM pay_rs.attempts WHERE payment_id = $1
                      ORDER BY created_at DESC LIMIT 1),
                    'test'
                )
                "#,
            )
            .bind(payment.id.as_uuid())
            .fetch_one(&mut **tx)
            .await?;
            sqlx::query(
                r#"
                INSERT INTO pay_rs.refunds (
                    id, tenant_id, payment_id, charge_id,
                    amount_minor, currency, exponent, status, rail, reason
                ) VALUES (
                    $1, $2, $3, NULL,
                    $4, $5, $6, 'pending', $7, $8
                )
                "#,
            )
            .bind(refund_id.as_uuid())
            .bind(payment.tenant_id.as_str())
            .bind(payment.id.as_uuid())
            .bind(payment.quoted.minor() as i64)
            .bind(payment.quoted.currency().code.as_str())
            .bind(i16::from(payment.quoted.currency().exponent))
            .bind(rail)
            .bind(reason)
            .execute(&mut **tx)
            .await
            .map_err(ApplyError::from_sql)?;
            insert_journal(tx, payment, proj.intake_kind).await?;
            let kind = rows::return_kind_from_intake(proj.intake_kind).unwrap();
            intake = Intake::Returned { kind, refund_id };
        }
        IntakeKind::Keep => {}
    }

    for (id, status) in &proj.attempt_updates {
        sqlx::query(
            r#"
            UPDATE pay_rs.attempts
               SET status = $2, updated_at = now(), version = version + 1
             WHERE id = $1
            "#,
        )
        .bind(id.as_uuid())
        .bind(rows::attempt_status_sql(*status))
        .execute(&mut **tx)
        .await?;
    }

    let (intake_s, cid, rid, rk) = rows::intake_parts(intake);
    // Issue 002: payment CAS on status+version. 0 rows → another writer won.
    let cas = sqlx::query(
        r#"
        UPDATE pay_rs.payments
           SET status = $2,
               exception = $3,
               intake = $4,
               intake_charge_id = $5,
               intake_refund_id = $6,
               return_kind = $7,
               terminal_reason = $8,
               version = version + 1
         WHERE id = $1 AND status = $9 AND version = $10
        "#,
    )
    .bind(payment.id.as_uuid())
    .bind(rows::payment_status_sql(proj.status))
    .bind(rows::exception_sql(proj.exception))
    .bind(intake_s)
    .bind(cid)
    .bind(rid)
    .bind(rk)
    .bind(rows::terminal_sql(proj.terminal_reason))
    .bind(rows::payment_status_sql(payment.status))
    .bind(payment.version as i64)
    .execute(&mut **tx)
    .await?;
    if cas.rows_affected() != 1 {
        return Err(ApplyError::Conflict);
    }

    if let Some(ev) = outbound_event(proj.intake_kind, proj.status, proj.terminal_reason) {
        let event_id = format!("{}:{ev}", payment.id.to_wire());
        let provider: String = sqlx::query_scalar(
            "SELECT rail FROM pay_rs.attempts WHERE payment_id = $1 ORDER BY created_at DESC LIMIT 1",
        )
        .bind(payment.id.as_uuid())
        .fetch_optional(&mut **tx)
        .await?
        .unwrap_or_else(|| "test".into());
        let payload = crate::lease::envelope(
            &event_id,
            ev,
            payment.tenant_id.as_str(),
            &payment.id.to_wire(),
            payment.quoted,
            &provider,
        );
        crate::lease::enqueue_outbound(tx, payment.tenant_id.as_str(), &event_id, ev, &payload)
            .await?;
    }
    Ok(())
}

async fn insert_journal(
    tx: &mut Transaction<'_, Postgres>,
    payment: &Payment,
    kind: IntakeKind,
) -> Result<(), ApplyError> {
    let Some(entry) =
        JournalEntry::for_intake_kind(kind, payment.tenant_id.clone(), payment.id, payment.quoted)?
    else {
        return Ok(());
    };
    let eid = Uuid::new_v4();
    sqlx::query(
        r#"
        INSERT INTO pay_rs.journal_entries (id, tenant_id, payment_id, currency, exponent)
        VALUES ($1, $2, $3, $4, $5)
        "#,
    )
    .bind(eid)
    .bind(payment.tenant_id.as_str())
    .bind(payment.id.as_uuid())
    .bind(payment.quoted.currency().code.as_str())
    .bind(i16::from(payment.quoted.currency().exponent))
    .execute(&mut **tx)
    .await?;
    for line in entry.lines() {
        sqlx::query(
            r#"
            INSERT INTO pay_rs.journal_lines (entry_id, account, dc, amount_minor)
            VALUES ($1, $2, $3, $4)
            "#,
        )
        .bind(eid)
        .bind(rows::account_sql(line.account))
        .bind(rows::dc_sql(line.dc))
        .bind(line.amount.minor() as i64)
        .execute(&mut **tx)
        .await?;
    }
    Ok(())
}

async fn insert_rcpt(
    tx: &mut Transaction<'_, Postgres>,
    payment: &Payment,
) -> Result<(), ApplyError> {
    let number = format!("RCPT-TEST-{}", payment.id.to_wire());
    sqlx::query(
        r#"
        INSERT INTO pay_rs.documents (tenant_id, payment_id, series, number, title)
        VALUES ($1, $2, 'RCPT', $3, 'Official Receipt')
        "#,
    )
    .bind(payment.tenant_id.as_str())
    .bind(payment.id.as_uuid())
    .bind(number)
    .execute(&mut **tx)
    .await?;
    Ok(())
}

async fn load_payment(
    tx: &mut Transaction<'_, Postgres>,
    id: PaymentId,
) -> Result<Payment, ApplyError> {
    let row = sqlx::query(
        r#"
        SELECT tenant_id, public_token, amount_minor, currency, exponent,
               status, exception, intake, intake_charge_id, intake_refund_id,
               return_kind, terminal_reason, expires_at, monitoring_until,
               payment_link_id, slot_key, version
          FROM pay_rs.payments
         WHERE id = $1
         FOR UPDATE
        "#,
    )
    .bind(id.as_uuid())
    .fetch_optional(&mut **tx)
    .await?
    .ok_or(ApplyError::NotFound)?;
    let quoted = rows::money_from_parts(
        row.try_get("amount_minor")?,
        row.try_get("currency")?,
        row.try_get("exponent")?,
    )?;
    let expires_at: OffsetDateTime = row.try_get("expires_at")?;
    let monitoring_until: OffsetDateTime = row.try_get("monitoring_until")?;
    let link: Option<Uuid> = row.try_get("payment_link_id")?;
    let slot: Option<String> = row.try_get("slot_key")?;
    let occupancy = link.map(|l| domain::OccupancyRef {
        link_id: PaymentLinkId::from_uuid(l),
        slot_key: slot,
    });
    let cid: Option<Uuid> = row.try_get("intake_charge_id")?;
    let rid: Option<Uuid> = row.try_get("intake_refund_id")?;
    let rk: Option<String> = row.try_get("return_kind")?;
    let version: i64 = row.try_get("version")?;
    Ok(Payment {
        id,
        tenant_id: TenantId::new(row.try_get::<String, _>("tenant_id")?),
        public_token: PublicToken::new(row.try_get::<String, _>("public_token")?),
        quoted,
        rate: RateLock::identity(quoted, expires_at, expires_at),
        status: rows::parse_payment_status(row.try_get("status")?)?,
        exception: rows::parse_exception(row.try_get("exception")?)?,
        intake: rows::parse_intake(row.try_get("intake")?, cid, rid, rk.as_deref())?,
        terminal_reason: rows::parse_terminal(row.try_get("terminal_reason")?)?,
        expires_at,
        monitoring_until,
        occupancy,
        version: version as u64,
    })
}

async fn load_attempts(
    tx: &mut Transaction<'_, Postgres>,
    payment_id: PaymentId,
) -> Result<Vec<Attempt>, ApplyError> {
    let rows_db = sqlx::query(
        r#"
        SELECT id, rail, method, amount_minor, currency, exponent, status,
               session_url, session_id, capture_id, network_id, fail_reason, version
          FROM pay_rs.attempts
         WHERE payment_id = $1
        "#,
    )
    .bind(payment_id.as_uuid())
    .fetch_all(&mut **tx)
    .await?;
    let mut out = Vec::new();
    for row in rows_db {
        let rail = RailId::parse(row.try_get("rail")?)?;
        let quoted_rail = rows::money_from_parts(
            row.try_get("amount_minor")?,
            row.try_get("currency")?,
            row.try_get("exponent")?,
        )?;
        let url: Option<String> = row.try_get("session_url")?;
        let sid: Option<String> = row.try_get("session_id")?;
        let session = match (url.clone(), sid.clone()) {
            (Some(url), Some(session_id)) => Some(HostedSession { url, session_id }),
            _ => None,
        };
        let fr: Option<String> = row.try_get("fail_reason")?;
        let version: i64 = row.try_get("version")?;
        out.push(Attempt {
            id: AttemptId::from_uuid(row.try_get("id")?),
            payment_id,
            rail,
            method: MethodId::new(quoted_rail.currency().code, rail),
            quoted_rail,
            status: rows::parse_attempt_status(row.try_get("status")?)?,
            session,
            refs: ConnectorRefs {
                session_id: sid,
                capture_id: row.try_get("capture_id")?,
                network_id: row.try_get("network_id")?,
            },
            fail_reason: fr.as_deref().map(rows::parse_terminal).transpose()?,
            version: version as u64,
        });
    }
    Ok(out)
}

async fn load_settlements(
    tx: &mut Transaction<'_, Postgres>,
    payment_id: PaymentId,
) -> Result<Vec<Settlement>, ApplyError> {
    let rows_db = sqlx::query(
        r#"
        SELECT id, attempt_id, amount_minor, currency, exponent, state, proof_kind, proof_id
          FROM pay_rs.settlements
         WHERE payment_id = $1
        "#,
    )
    .bind(payment_id.as_uuid())
    .fetch_all(&mut **tx)
    .await?;
    let mut out = Vec::new();
    for row in rows_db {
        let kind: String = row.try_get("proof_kind")?;
        let pid: String = row.try_get("proof_id")?;
        out.push(Settlement {
            id: SettlementId::from_uuid(row.try_get("id")?),
            attempt_id: AttemptId::from_uuid(row.try_get("attempt_id")?),
            received: rows::money_from_parts(
                row.try_get("amount_minor")?,
                row.try_get("currency")?,
                row.try_get("exponent")?,
            )?,
            proof: rows::dummy_proof(&kind, &pid)?,
            state: rows::parse_settlement_state(row.try_get("state")?)?,
        });
    }
    Ok(out)
}

/// Issue 008: lock the parent link, then count occupying **others**.
async fn occupancy_snap(
    tx: &mut Transaction<'_, Postgres>,
    payment: &Payment,
) -> Result<OccupancySnap, ApplyError> {
    let Some(occ) = &payment.occupancy else {
        return Ok(OccupancySnap { full: false });
    };
    sqlx::query("SELECT 1 FROM pay_rs.payment_links WHERE id = $1 FOR UPDATE")
        .bind(occ.link_id.as_uuid())
        .fetch_optional(&mut **tx)
        .await?
        .ok_or(ApplyError::NotFound)?;
    let max: Option<i32> =
        sqlx::query_scalar("SELECT max_payers FROM pay_rs.payment_links WHERE id = $1")
            .bind(occ.link_id.as_uuid())
            .fetch_one(&mut **tx)
            .await?;
    let others: i64 = sqlx::query_scalar(
        r#"
        SELECT count(*)::bigint FROM pay_rs.payments
         WHERE payment_link_id = $1
           AND id <> $2
           AND status IN ('open','processing','settled')
        "#,
    )
    .bind(occ.link_id.as_uuid())
    .bind(payment.id.as_uuid())
    .fetch_one(&mut **tx)
    .await?;
    let full = max.map(|m| others >= i64::from(m)).unwrap_or(false);
    Ok(OccupancySnap { full })
}

async fn charges_paused(
    tx: &mut Transaction<'_, Postgres>,
    tenant: &str,
) -> Result<bool, ApplyError> {
    let v: Option<bool> =
        sqlx::query_scalar("SELECT charges_paused FROM pay_rs.org_settings WHERE tenant_id = $1")
            .bind(tenant)
            .fetch_optional(&mut **tx)
            .await?;
    Ok(v.unwrap_or(false))
}
