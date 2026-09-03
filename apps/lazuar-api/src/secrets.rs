//! Port of `Secrets/SecretBox.cs` — AES-256-GCM wrap for BYOK.
//! Wrapped format: base64(nonce[12] || tag[16] || ciphertext). Key from
//! `Pay:WrapKey` (32-byte base64); dev/test fallback derives from a fixed
//! phrase — production boot fails closed without a real key (PayBoot).

use aes_gcm::aead::{Aead, AeadCore, AeadInPlace, KeyInit, OsRng};
use aes_gcm::{Aes256Gcm, Key, Nonce};
use base64::engine::general_purpose::STANDARD as B64;
use base64::Engine;
use sha2::{Digest, Sha256};

#[derive(Debug, thiserror::Error)]
pub enum SecretBoxError {
    #[error("Pay:WrapKey is required")]
    KeyRequired,
    #[error("Pay:WrapKey must be 32 bytes base64")]
    KeyInvalid,
    #[error("wrapped secret is not valid base64")]
    NotBase64,
    #[error("wrapped secret is too short")]
    TooShort,
    #[error("unwrap failed (wrong key or corrupted ciphertext)")]
    UnwrapFailed,
}

pub struct SecretBox {
    key: Key<Aes256Gcm>,
}

impl SecretBox {
    /// `Testing`-environment semantics: fall back to the derived dev key when
    /// no `Pay:WrapKey` is configured. Production resolves the key before boot.
    pub fn from_env_testing(wrap_key: Option<&str>) -> Result<Self, SecretBoxError> {
        match wrap_key.map(str::trim).filter(|k| !k.is_empty()) {
            Some(b64) => Self::from_wrap_key_b64(b64),
            None => {
                let key = Sha256::digest(b"lazuar-pay-dev-wrap-key");
                Ok(Self { key: *Key::<Aes256Gcm>::from_slice(&key) })
            }
        }
    }

    pub fn from_wrap_key_b64(b64: &str) -> Result<Self, SecretBoxError> {
        let key = B64.decode(b64).map_err(|_| SecretBoxError::KeyInvalid)?;
        if key.len() != 32 {
            return Err(SecretBoxError::KeyInvalid);
        }
        Ok(Self { key: *Key::<Aes256Gcm>::from_slice(&key) })
    }

    pub fn protect(&self, plaintext: &str) -> String {
        let cipher = Aes256Gcm::new(&self.key);
        let nonce = Aes256Gcm::generate_nonce(&mut OsRng);
        let sealed = cipher
            .encrypt(&nonce, plaintext.as_bytes())
            .expect("aes-gcm encrypt cannot fail with valid key/nonce");
        // sealed = ciphertext || tag(16) — C# layout is nonce || tag || cipher.
        let (ct, tag) = sealed.split_at(sealed.len() - 16);
        let mut wrapped = Vec::with_capacity(12 + 16 + ct.len());
        wrapped.extend_from_slice(nonce.as_slice());
        wrapped.extend_from_slice(tag);
        wrapped.extend_from_slice(ct);
        B64.encode(wrapped)
    }

    pub fn unprotect(&self, wrapped: &str) -> Result<String, SecretBoxError> {
        let raw = B64.decode(wrapped).map_err(|_| SecretBoxError::NotBase64)?;
        if raw.len() < 12 + 16 {
            return Err(SecretBoxError::TooShort);
        }
        let (nonce, rest) = raw.split_at(12);
        let (tag, ct) = rest.split_at(16);
        let cipher = Aes256Gcm::new(&self.key);
        let mut plain = ct.to_vec();
        cipher
            .decrypt_in_place_detached(Nonce::from_slice(nonce), &[], &mut plain, tag.into())
            .map_err(|_| SecretBoxError::UnwrapFailed)?;
        String::from_utf8(plain).map_err(|_| SecretBoxError::UnwrapFailed)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn protect_unprotect_round_trips() {
        let box_one = SecretBox::from_env_testing(None).unwrap();
        let wrapped = box_one.protect("whsec_live_abc123");
        assert_eq!(box_one.unprotect(&wrapped).unwrap(), "whsec_live_abc123");
        // Layout check: same plaintext wraps differently (random nonce).
        assert_ne!(wrapped, box_one.protect("whsec_live_abc123"));
    }

    #[test]
    fn wrong_key_fails_closed() {
        let box_one = SecretBox::from_env_testing(None).unwrap();
        let other =
            SecretBox::from_wrap_key_b64(&B64.encode([7u8; 32])).unwrap();
        let wrapped = box_one.protect("secret");
        assert!(matches!(other.unprotect(&wrapped), Err(SecretBoxError::UnwrapFailed)));
    }

    #[test]
    fn non_32_byte_key_rejected() {
        assert!(matches!(
            SecretBox::from_wrap_key_b64(&B64.encode([1u8; 16])),
            Err(SecretBoxError::KeyInvalid)
        ));
    }
}
