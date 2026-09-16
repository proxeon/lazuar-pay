//! Late_pay settler. CHIP (`refund_idempotent = false`) is never claimed.

use domain::money::{Currency, Money};
use domain::proof::RefundOutcome;
use sqlx::PgPool;
use storage::{envelope, ApplyError};
use time::{Duration, OffsetDateTime};

pub trait RefundRemote: Send + Sync {
    fn refund(
        &self,
        rail: &str,
        refund_id: uuid::Uuid,
    ) -> impl std::future::Future<Output = RefundOutcome> + Send;
}

#[derive(Clone, Copy, Default)]
pub struct NoopRefund;

impl RefundRemote for NoopRefund {
    async fn refund(&self, _rail: &str, _id: uuid::Uuid) -> RefundOutcome {
        RefundOutcome::Unknown
    }
}

#[derive(Clone, Copy, Default)]
pub struct FakeSettled;

impl RefundRemote for FakeSettled {
    async fn refund(&self, _rail: &str, _id: uuid::Uuid) -> RefundOutcome {
        RefundOutcome::Settled
    }
}

pub async fn process_batch<R: RefundRemote>(
    pool: &PgPool,
    remote: &R,
) -> Result<usize, ApplyError> {
    let now = OffsetDateTime::now_utc();
    let claimed = storage::claim_refunds(pool, now, 20).await?;
    let n = claimed.len();
    for row in claimed {
        let attempts = row.attempt_count + 1;
        match remote.refund(&row.rail, row.id).await {
            RefundOutcome::Settled => {
                storage::mark_refund(pool, row.id, "succeeded", attempts, None, None).await?;
                let ccy = Currency::by_code(&row.currency).unwrap_or(Currency::MYR);
                let quoted = Money::from_minor(i128::from(row.amount_minor), ccy)
                    .unwrap_or_else(|_| Money::from_quoted_str("0.01", ccy).expect("min"));
                let event_id = format!("{}:refund.created", row.payment_id.to_wire());
                let payload = envelope(
                    &event_id,
                    "refund.created",
                    &row.tenant_id,
                    &row.payment_id.to_wire(),
                    quoted,
                    &row.rail,
                );
                let _ = storage::enqueue_outbound_pool(
                    pool,
                    &row.tenant_id,
                    &event_id,
                    "refund.created",
                    &payload,
                )
                .await;
            }
            RefundOutcome::Rejected => {
                storage::mark_refund(pool, row.id, "failed", attempts, None, Some("rejected"))
                    .await?;
            }
            RefundOutcome::Unknown => {
                let wait = refund_backoff(attempts);
                storage::mark_refund(
                    pool,
                    row.id,
                    "pending",
                    attempts,
                    Some(now + wait),
                    Some("unknown"),
                )
                .await?;
            }
        }
    }
    Ok(n)
}

fn refund_backoff(completed: i32) -> Duration {
    match completed {
        i if i <= 1 => Duration::minutes(1),
        2 => Duration::minutes(5),
        3 => Duration::minutes(30),
        4 => Duration::hours(2),
        _ => Duration::hours(8),
    }
}
