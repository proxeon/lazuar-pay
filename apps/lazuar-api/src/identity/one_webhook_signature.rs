//! Port of `Identity/OneWebhooks/OneWebhookSignature.cs`.
//! Product One signs `X-Lazuar-Signature: v1=<hex>` and `X-Lazuar-Timestamp`
//! over `{unix}.{body}`. Combined `t=<unix>,v1=<hex>` is accepted as compat.

use chrono::Utc;
use hmac::{Hmac, Mac};
use sha2::Sha256;
use subtle::ConstantTimeEq;

pub fn compute(secret: &str, body: &str, unix_seconds: i64) -> String {
    let mut mac = <Hmac<Sha256> as Mac>::new_from_slice(secret.as_bytes())
        .expect("hmac accepts any key length");
    mac.update(format!("{unix_seconds}.{body}").as_bytes());
    hex::encode(mac.finalize().into_bytes())
}

fn parse(signature_header: &str, timestamp_header: Option<&str>) -> Option<(i64, String)> {
    // Combined header first: `t=` in the signature wins over X-Lazuar-Timestamp (S3).
    let mut timestamp = None;
    let mut v1 = None;
    for part in signature_header.split(',') {
        let part = part.trim();
        // S1: comma-part without `=` is skipped, not fatal (`t=123,junk,v1=abc`).
        let Some((key, value)) = part.split_once('=') else {
            continue;
        };
        match key.trim().to_ascii_lowercase().as_str() {
            // S2: t/v1 keys matched case-insensitively.
            "t" => timestamp = value.trim().parse().ok(),
            "v1" => v1 = Some(value.trim().to_string()),
            _ => {}
        }
    }
    if timestamp.is_some() && v1.is_some() {
        return Some((timestamp?, v1?));
    }
    // Fallback: separate X-Lazuar-Timestamp + `v1=<hex>` only when combined header has no `t`.
    if timestamp.is_none() {
        if let Some(ts) = timestamp_header.map(str::trim).filter(|s| !s.is_empty()) {
            let parsed: i64 = ts.parse().ok()?;
            let v1_hex = v1.or_else(|| {
                signature_header
                    .trim()
                    .strip_prefix("v1=")
                    .map(str::to_string)
            })?;
            return Some((parsed, v1_hex));
        }
    }
    Some((timestamp?, v1?))
}

pub fn try_verify(
    secret: &str,
    body: &str,
    signature_header: Option<&str>,
    timestamp_header: Option<&str>,
    tolerance_seconds: i64,
    now_unix_seconds: Option<i64>,
) -> bool {
    if secret.trim().is_empty() {
        return false;
    }
    let Some(sig_header) = signature_header.filter(|s| !s.trim().is_empty()) else {
        return false;
    };
    let Some((timestamp, v1_hex)) = parse(sig_header, timestamp_header) else {
        return false;
    };
    if tolerance_seconds > 0 {
        let now = now_unix_seconds.unwrap_or_else(|| Utc::now().timestamp());
        if (now - timestamp).abs() > tolerance_seconds {
            return false;
        }
    }
    let expected = compute(secret, body, timestamp);
    fixed_time_equals_hex(&v1_hex, &expected)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn junk_comma_part_is_skipped() {
        let secret = "whsec_test";
        let body = "{}";
        let ts = 1_700_000_000i64;
        let v1 = compute(secret, body, ts);
        let combined = format!("t={ts},junk,v1={v1}");
        assert!(try_verify(secret, body, Some(&combined), None, 0, Some(ts)));
    }

    #[test]
    fn keys_are_case_insensitive() {
        let secret = "whsec_test";
        let body = "{}";
        let ts = 1_700_000_000i64;
        let v1 = compute(secret, body, ts);
        let combined = format!("T={ts},V1={v1}");
        assert!(try_verify(secret, body, Some(&combined), None, 0, Some(ts)));
    }

    #[test]
    fn combined_t_wins_over_timestamp_header() {
        let secret = "whsec_test";
        let body = "{}";
        let ts = 1_700_000_000i64;
        let v1 = compute(secret, body, ts);
        let combined = format!("t={ts},v1={v1}");
        assert!(try_verify(
            secret,
            body,
            Some(&combined),
            Some("111"),
            0,
            Some(ts),
        ));
    }
}

fn fixed_time_equals_hex(provided: &str, expected: &str) -> bool {
    let left = provided.trim().to_lowercase();
    let right = expected.trim().to_lowercase();
    left.as_bytes().ct_eq(right.as_bytes()).into()
}

