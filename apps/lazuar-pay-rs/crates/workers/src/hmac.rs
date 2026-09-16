//! Plane C / Plane A: HMAC-SHA256 hex of `{unix}.{body}` (021/i15, 09 lock 8).

use hmac::{Hmac, Mac};
use sha2::Sha256;

type HmacSha256 = Hmac<Sha256>;

pub const SKEW_SECS: i64 = 300;

pub fn sign_v1(secret: &str, body: &[u8], unix: i64) -> String {
    let mut mac = HmacSha256::new_from_slice(secret.as_bytes()).expect("hmac key");
    mac.update(unix.to_string().as_bytes());
    mac.update(b".");
    mac.update(body);
    hex::encode(mac.finalize().into_bytes())
}

/// Accept `v1=<hex>` + `X-Lazuar-Timestamp`, or combined `t=<unix>,v1=<hex>`.
/// Raw body hex is rejected. Skew 300s.
pub fn verify_v1(
    secret: &str,
    body: &[u8],
    signature_header: &str,
    timestamp_header: Option<&str>,
    now_unix: i64,
) -> bool {
    if secret.is_empty() || signature_header.is_empty() {
        return false;
    }
    let Some((ts, v1_hex)) = parse_sig(signature_header, timestamp_header) else {
        return false;
    };
    if (now_unix - ts).abs() > SKEW_SECS {
        return false;
    }
    let Ok(got) = hex::decode(v1_hex.to_ascii_lowercase()) else {
        return false;
    };
    let mut mac = HmacSha256::new_from_slice(secret.as_bytes()).expect("hmac key");
    mac.update(ts.to_string().as_bytes());
    mac.update(b".");
    mac.update(body);
    mac.verify_slice(&got).is_ok()
}

fn parse_sig(signature_header: &str, timestamp_header: Option<&str>) -> Option<(i64, String)> {
    let mut t: Option<i64> = None;
    let mut v1: Option<String> = None;
    for part in signature_header.split(',') {
        let part = part.trim();
        let Some(eq) = part.find('=') else {
            continue;
        };
        if eq == 0 {
            continue;
        }
        let key = &part[..eq];
        let value = &part[eq + 1..];
        if key.eq_ignore_ascii_case("t") {
            t = value.parse().ok();
        } else if key.eq_ignore_ascii_case("v1") {
            v1 = Some(value.to_string());
        }
    }
    if t.is_none() {
        if let Some(h) = timestamp_header {
            t = h.trim().parse().ok();
        }
    }
    Some((t?, v1?))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn known_shape() {
        let hex = sign_v1("whsec_test", b"{}", 1);
        assert_eq!(hex.len(), 64);
        assert_eq!(hex, sign_v1("whsec_test", b"{}", 1));
        assert_ne!(hex, sign_v1("whsec_test", b"{}", 2));
    }

    #[test]
    fn split_and_combined_verify() {
        let body = br#"{"ok":true}"#;
        let unix = 1_700_000_000i64;
        let hex = sign_v1("whsec_abc", body, unix);
        assert!(verify_v1(
            "whsec_abc",
            body,
            &format!("v1={hex}"),
            Some(&unix.to_string()),
            unix
        ));
        assert!(verify_v1(
            "whsec_abc",
            body,
            &format!("t={unix},v1={hex}"),
            None,
            unix
        ));
        assert!(!verify_v1("whsec_abc", body, &hex, None, unix));
        assert!(!verify_v1(
            "whsec_abc",
            body,
            &format!("t={},v1={hex}", unix - 1000),
            None,
            unix
        ));
    }
}
