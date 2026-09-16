//! Claim expired payments; fold via `storage::apply` only.

use sqlx::PgPool;
use storage::{apply, ApplyCmd, ApplyError};
use time::OffsetDateTime;

pub async fn once(pool: &PgPool) -> Result<usize, ApplyError> {
    let now = OffsetDateTime::now_utc();
    let claimed = storage::claim_expired(pool, now, 20).await?;
    let n = claimed.len();
    for c in claimed {
        let cmd = if c.watch_timeout {
            ApplyCmd::WatchTimeout {
                payment_id: c.payment_id,
                now,
            }
        } else {
            ApplyCmd::ExpireClock {
                payment_id: c.payment_id,
                now,
            }
        };
        match apply(pool, cmd).await {
            Ok(_) | Err(ApplyError::Conflict) | Err(ApplyError::NotFound) => {}
            Err(e) => return Err(e),
        }
    }
    Ok(n)
}
