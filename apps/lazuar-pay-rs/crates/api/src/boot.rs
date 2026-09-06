//! Testing WrapKey fallback. Production ThrowIfMisconfigured is later (14).

use sha2::{Digest, Sha256};

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Env {
    Testing,
    Development,
    Staging,
    Production,
}

impl Env {
    pub fn parse(s: &str) -> Self {
        match s.trim() {
            "Development" => Self::Development,
            "Staging" => Self::Staging,
            "Production" => Self::Production,
            _ => Self::Testing,
        }
    }

    pub fn allows_test(self) -> bool {
        matches!(self, Self::Testing | Self::Development)
    }

    pub fn skip_misconfigured(self) -> bool {
        matches!(self, Self::Testing | Self::Development)
    }
}

/// SHA-256(`lazuar-pay-dev-wrap-key`) when Testing and WrapKey empty.
pub fn wrap_key_testing_fallback(env: Env, configured: &str) -> Option<[u8; 32]> {
    if !configured.is_empty() {
        return None;
    }
    if env != Env::Testing {
        return None;
    }
    let d = Sha256::digest(b"lazuar-pay-dev-wrap-key");
    Some(d.into())
}

pub fn throw_if_misconfigured(env: Env) -> Result<(), String> {
    if env.skip_misconfigured() {
        return Ok(());
    }
    Err("ThrowIfMisconfigured is not implemented for Staging/Production in this slice".into())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn testing_empty_wrap_key_is_sha256() {
        let k = wrap_key_testing_fallback(Env::Testing, "").expect("fallback");
        let d = Sha256::digest(b"lazuar-pay-dev-wrap-key");
        assert_eq!(&k[..], d.as_slice());
    }

    #[test]
    fn production_empty_wrap_key_does_not_fallback() {
        assert!(wrap_key_testing_fallback(Env::Production, "").is_none());
        assert!(wrap_key_testing_fallback(Env::Staging, "").is_none());
        assert!(wrap_key_testing_fallback(Env::Development, "").is_none());
    }

    #[test]
    fn throw_if_misconfigured_skips_testing_and_development() {
        assert!(throw_if_misconfigured(Env::Testing).is_ok());
        assert!(throw_if_misconfigured(Env::Development).is_ok());
        assert!(throw_if_misconfigured(Env::Production).is_err());
        assert!(throw_if_misconfigured(Env::Staging).is_err());
    }
}
