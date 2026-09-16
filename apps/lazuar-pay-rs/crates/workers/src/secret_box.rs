//! AES-GCM wrap. Layout: nonce(12) || tag(16) || cipher. Never log plaintext.

use aes_gcm::aead::{Aead, KeyInit};
use aes_gcm::{Aes256Gcm, Nonce};
use sha2::{Digest, Sha256};

#[derive(Debug, thiserror::Error)]
pub enum BoxError {
    #[error("secret box")]
    Crypto,
}

#[derive(Clone, Copy)]
pub struct SecretBox {
    key: [u8; 32],
}

impl SecretBox {
    pub fn new(key: [u8; 32]) -> Self {
        Self { key }
    }

    /// SHA-256(`lazuar-pay-dev-wrap-key`) — same bytes as `api::boot` Testing fallback.
    pub fn testing_fallback_key() -> [u8; 32] {
        Sha256::digest(b"lazuar-pay-dev-wrap-key").into()
    }

    pub fn protect(&self, plaintext: &[u8]) -> Result<Vec<u8>, BoxError> {
        let cipher = Aes256Gcm::new_from_slice(&self.key).map_err(|_| BoxError::Crypto)?;
        let mut nonce_bytes = [0u8; 12];
        getrandom::getrandom(&mut nonce_bytes).map_err(|_| BoxError::Crypto)?;
        let nonce = Nonce::from_slice(&nonce_bytes);
        let out = cipher
            .encrypt(nonce, plaintext)
            .map_err(|_| BoxError::Crypto)?;
        if out.len() < 16 {
            return Err(BoxError::Crypto);
        }
        let (ct, tag) = out.split_at(out.len() - 16);
        let mut stored = Vec::with_capacity(12 + 16 + ct.len());
        stored.extend_from_slice(&nonce_bytes);
        stored.extend_from_slice(tag);
        stored.extend_from_slice(ct);
        Ok(stored)
    }

    pub fn unprotect(&self, wrapped: &[u8]) -> Result<Vec<u8>, BoxError> {
        if wrapped.len() < 28 {
            return Err(BoxError::Crypto);
        }
        let nonce = Nonce::from_slice(&wrapped[..12]);
        let tag = &wrapped[12..28];
        let ct = &wrapped[28..];
        let mut combined = Vec::with_capacity(ct.len() + 16);
        combined.extend_from_slice(ct);
        combined.extend_from_slice(tag);
        let cipher = Aes256Gcm::new_from_slice(&self.key).map_err(|_| BoxError::Crypto)?;
        cipher
            .decrypt(nonce, combined.as_slice())
            .map_err(|_| BoxError::Crypto)
    }

    pub fn protect_str(&self, s: &str) -> Result<Vec<u8>, BoxError> {
        self.protect(s.as_bytes())
    }

    pub fn unprotect_str(&self, wrapped: &[u8]) -> Result<String, BoxError> {
        let p = self.unprotect(wrapped)?;
        String::from_utf8(p).map_err(|_| BoxError::Crypto)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn round_trip() {
        let b = SecretBox::new(SecretBox::testing_fallback_key());
        let ct = b.protect_str("whsec_test").unwrap();
        assert_eq!(b.unprotect_str(&ct).unwrap(), "whsec_test");
        assert_eq!(ct.len(), 12 + 16 + b"whsec_test".len());
    }

    #[test]
    fn testing_key_is_sha256_of_dev_phrase() {
        let k = SecretBox::testing_fallback_key();
        let d = Sha256::digest(b"lazuar-pay-dev-wrap-key");
        assert_eq!(&k[..], d.as_slice());
    }

    #[test]
    fn garbage_ciphertext_fails() {
        let b = SecretBox::new(SecretBox::testing_fallback_key());
        assert!(b.unprotect(b"!!!not-a-wrapped-secret!!!").is_err());
    }
}
