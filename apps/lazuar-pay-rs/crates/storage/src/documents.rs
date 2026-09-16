//! Official Receipt / Refund document numbers (`RCPT-` / `REF-`).
//!
//! C# `DocumentNumbers.AllocateAsync` is one atomic upsert: the (tenant, number)
//! unique index is only a backstop. Racing two Takes and losing on that unique
//! rolls back the whole fulfill TX — a real payment the PSP already acked.
//! Allocation must therefore mint a unique n without a later conflict.

use sqlx::{Postgres, Transaction};
use time::{OffsetDateTime, UtcOffset};

use crate::error::ApplyError;

/// Malaysia has no DST. MYT is UTC+8 year-round (`Asia/Kuala_Lumpur` /
/// Windows `Singapore Standard Time` in C# `MalaysiaTime.Year`).
const MYT: UtcOffset = match UtcOffset::from_hms(8, 0, 0) {
    Ok(o) => o,
    Err(_) => panic!("UTC+8 is a valid offset"),
};

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum DocSeries {
    /// Official Receipt on Take.
    Receipt,
    /// Refund note when a pending refund settles (merchant or resolve).
    Refund,
}

impl DocSeries {
    pub fn as_sql(self) -> &'static str {
        match self {
            Self::Receipt => "RCPT",
            Self::Refund => "REF",
        }
    }
}

/// Calendar year in Malaysia time. A UTC New Year's Eve 16:00 is already 1 Jan MYT.
pub fn malaysia_year(utc: OffsetDateTime) -> i32 {
    utc.to_offset(MYT).year()
}

/// `{SERIES}-{year}-{n:05}` — C# `$"{series}-{year}-{n:00000}"`.
pub fn format_number(series: DocSeries, year: i32, n: i32) -> String {
    format!("{}-{year}-{n:05}", series.as_sql())
}

/// Next n for (tenant, series, MYT year), in the caller's TX.
///
/// `INSERT … ON CONFLICT DO UPDATE RETURNING last_n` is the lock: two sessions
/// cannot both read 0 and both write 1. First insert stores 1; the loser of the
/// PK takes the update path and gets 2.
pub async fn allocate(
    tx: &mut Transaction<'_, Postgres>,
    tenant_id: &str,
    series: DocSeries,
    now: OffsetDateTime,
) -> Result<String, ApplyError> {
    let year = malaysia_year(now);
    let n: i32 = sqlx::query_scalar(
        r#"
        INSERT INTO pay_rs.document_sequences AS s (tenant_id, series, year_myt, last_n)
        VALUES ($1, $2, $3, 1)
        ON CONFLICT (tenant_id, series, year_myt)
        DO UPDATE SET last_n = s.last_n + 1
        RETURNING s.last_n
        "#,
    )
    .bind(tenant_id)
    .bind(series.as_sql())
    .bind(year)
    .fetch_one(&mut **tx)
    .await?;
    Ok(format_number(series, year, n))
}

#[cfg(test)]
mod tests {
    use super::*;
    use time::format_description::well_known::Rfc3339;

    fn parse(s: &str) -> OffsetDateTime {
        OffsetDateTime::parse(s, &Rfc3339).expect(s)
    }

    #[test]
    fn myt_new_year_is_utc_1600() {
        // 2025-12-31 15:59:59 UTC = 23:59:59 MYT → still 2025.
        assert_eq!(malaysia_year(parse("2025-12-31T15:59:59Z")), 2025);
        // 2025-12-31 16:00:00 UTC = 2026-01-01 00:00:00 MYT.
        assert_eq!(malaysia_year(parse("2025-12-31T16:00:00Z")), 2026);
    }

    #[test]
    fn format_matches_csharp_five_digits() {
        assert_eq!(
            format_number(DocSeries::Receipt, 2026, 1),
            "RCPT-2026-00001"
        );
        assert_eq!(format_number(DocSeries::Refund, 2026, 42), "REF-2026-00042");
        assert_eq!(
            format_number(DocSeries::Receipt, 2026, 100000),
            "RCPT-2026-100000"
        );
    }
}
