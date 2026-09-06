use domain::{Illegal, JournalError, MoneyError};

#[derive(Debug, thiserror::Error)]
pub enum ApplyError {
    #[error("payment or attempt not found")]
    NotFound,
    #[error("CAS lost or unique conflict")]
    Conflict,
    #[error("amount or currency mismatch")]
    Integrity,
    #[error("charges paused (Take blocked)")]
    Paused,
    #[error("refund exceeds remainder")]
    AlreadyRefunded,
    #[error("idempotency key reused with a different body")]
    IdempotencyMismatch,
    #[error("payment is not startable")]
    NotStartable,
    #[error("a live attempt already exists")]
    LiveAttemptExists,
    #[error(transparent)]
    Domain(#[from] Illegal),
    #[error(transparent)]
    Money(#[from] MoneyError),
    #[error(transparent)]
    Journal(#[from] JournalError),
    #[error(transparent)]
    Sql(#[from] sqlx::Error),
}

impl ApplyError {
    pub fn from_sql(err: sqlx::Error) -> Self {
        if is_unique_violation(&err) {
            Self::Conflict
        } else {
            Self::Sql(err)
        }
    }
}

pub fn is_unique_violation(err: &sqlx::Error) -> bool {
    matches!(err, sqlx::Error::Database(e) if e.code().as_deref() == Some("23505"))
}
