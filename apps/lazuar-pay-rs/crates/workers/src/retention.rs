use sqlx::PgPool;
use storage::{sweep_retention, ApplyError, RetentionCfg};

pub async fn sweep(pool: &PgPool, cfg: RetentionCfg) -> Result<u64, ApplyError> {
    sweep_retention(pool, cfg).await
}
