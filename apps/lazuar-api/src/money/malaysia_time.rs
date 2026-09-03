//! Port of `Money/MalaysiaTime` — document years are Malaysian years (UTC+8, no DST).

use chrono::{DateTime, Datelike, FixedOffset, Utc};

pub fn offset() -> FixedOffset {
    FixedOffset::east_opt(8 * 3600).expect("UTC+8 is a valid offset")
}

/// The Malaysian-calendar year of `at` — document series reset on MY midnight.
pub fn year(at: DateTime<Utc>) -> i32 {
    at.with_timezone(&offset()).year()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn year_follows_malaysia_midnight_not_utc() {
        // 2026-12-31 17:00Z is already 2027-01-01 01:00 MYT.
        let at = DateTime::parse_from_rfc3339("2026-12-31T17:00:00Z").unwrap().with_timezone(&Utc);
        assert_eq!(year(at), 2027);
        // 2026-12-31 15:59Z is still 2026-12-31 23:59 MYT.
        let at = DateTime::parse_from_rfc3339("2026-12-31T15:59:00Z").unwrap().with_timezone(&Utc);
        assert_eq!(year(at), 2026);
    }
}
