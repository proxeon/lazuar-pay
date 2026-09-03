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
    if let Some(ts) = timestamp_header.map(str::trim).filter(|s| !s.is_empty()) {
        // Separate headers: X-Lazuar-Timestamp + `v1=<hex>`. Raw hex (no v1=
        // prefix) is rejected per the One dialect.
        let timestamp: i64 = ts.parse().ok()?;
        let v1 = signature_header.trim().strip_prefix("v1=")?.to_string();
        return Some((timestamp, v1));
    }
    // Combined header: t=<unix>,v1=<hex>
    let mut timestamp = None;
    let mut v1 = None;
    for part in signature_header.split(',') {
        let mut kv = part.trim().splitn(2, '=');
        let key = kv.next()?.trim();
        let value = kv.next()?.trim();
        match key {
            "t" => timestamp = value.parse().ok(),
            "v1" => v1 = Some(value.to_string()),
            _ => {}
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

fn fixed_time_equals_hex(provided: &str, expected: &str) -> bool {
    let left = provided.trim().to_lowercase();
    let right = expected.trim().to_lowercase();
    left.as_bytes().ct_eq(right.as_bytes()).into()
}

