//! Plane C: HMAC-SHA256 hex of `{unix}.{body}` (021/i15).

use hmac::{Hmac, Mac};
use sha2::Sha256;

type HmacSha256 = Hmac<Sha256>;

pub fn sign_v1(secret: &str, body: &[u8], unix: i64) -> String {
    let mut mac = HmacSha256::new_from_slice(secret.as_bytes()).expect("hmac key");
    mac.update(unix.to_string().as_bytes());
    mac.update(b".");
    mac.update(body);
    hex::encode(mac.finalize().into_bytes())
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
}
