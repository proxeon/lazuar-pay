//! Port of `Identity/Client/OneWhoamiCache.cs` — 60s whoami cache keyed by
//! SHA-256 of the Authorization header; machine keys are additionally indexed
//! by One `user_id` so Plane A `api_key.revoked` can drop them without
//! waiting for the TTL.

use std::collections::{HashMap, HashSet};
use std::sync::Mutex;
use std::time::{Duration, Instant};

use sha2::{Digest, Sha256};

use crate::identity::whoami::WhoamiResponse;

const TTL: Duration = Duration::from_secs(60);

#[derive(Default)]
pub struct OneWhoamiCache {
    entries: Mutex<HashMap<String, (WhoamiResponse, Instant)>>,
    /// key_id (One user_id) → cache keys minted with that machine key.
    by_key_id: Mutex<HashMap<String, HashSet<String>>>,
}

pub fn token_hash(authorization: &str) -> String {
    let bytes = Sha256::digest(authorization.as_bytes());
    // C# Convert.ToHexString → uppercase.
    hex::encode_upper(bytes)
}

fn cache_key(token_hash: &str) -> String {
    format!("pay:whoami:{token_hash}")
}

impl Default for WhoamiResponse {
    fn default() -> Self {
        Self {
            user_id: String::new(),
            email: None,
            name: None,
            is_platform_admin: false,
            active_org_id: None,
            tenants: Vec::new(),
        }
    }
}

impl OneWhoamiCache {
    pub fn new() -> Self {
        Self::default()
    }

    fn sweep(entries: &mut HashMap<String, (WhoamiResponse, Instant)>) {
        let now = Instant::now();
        entries.retain(|_, (_, exp)| *exp > now);
    }

    pub fn try_get(&self, authorization: &str) -> Option<WhoamiResponse> {
        let key = cache_key(&token_hash(authorization));
        let mut entries = self.entries.lock().ok()?;
        Self::sweep(&mut entries);
        if let Some((who, expires_at)) = entries.get(&key) {
            if *expires_at > Instant::now() {
                return Some(who.clone());
            }
        }
        None
    }

    /// Cache a whoami. Machine keys are additionally indexed by One user_id
    /// so `invalidate_key` can find every cache entry minted with that key.
    pub fn set(&self, authorization: &str, who: &WhoamiResponse, machine_key: bool) {
        let hash = token_hash(authorization);
        if let Ok(mut entries) = self.entries.lock() {
            Self::sweep(&mut entries);
            entries.insert(
                cache_key(&hash),
                (who.clone(), Instant::now() + TTL),
            );
        }
        if machine_key && !who.user_id.trim().is_empty() {
            if let Ok(mut index) = self.by_key_id.lock() {
                index.entry(who.user_id.clone()).or_default().insert(hash);
            }
        }
    }

    pub fn remove_token(&self, authorization: &str) {
        let key = cache_key(&token_hash(authorization));
        if let Ok(mut entries) = self.entries.lock() {
            entries.remove(&key);
        }
    }

    /// Plane A: drop every cache entry minted with this machine key.
    pub fn invalidate_key(&self, key_id: &str) {
        if key_id.trim().is_empty() {
            return;
        }
        let Ok(mut index) = self.by_key_id.lock() else { return };
        if let Some(hashes) = index.remove(key_id) {
            if let Ok(mut entries) = self.entries.lock() {
                for hash in hashes {
                    entries.remove(&cache_key(&hash));
                }
            }
        }
    }

    #[cfg(test)]
    pub fn len(&self) -> usize {
        self.entries.lock().map(|e| e.len()).unwrap_or(0)
    }

    #[cfg(test)]
    pub fn insert_expired_for_test(&self, authorization: &str, who: &WhoamiResponse) {
        let hash = token_hash(authorization);
        if let Ok(mut entries) = self.entries.lock() {
            entries.insert(
                cache_key(&hash),
                (who.clone(), Instant::now() - Duration::from_secs(1)),
            );
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn try_get_drops_expired_entries() {
        let cache = OneWhoamiCache::new();
        let who = WhoamiResponse {
            user_id: "u1".into(),
            ..WhoamiResponse::default()
        };
        cache.insert_expired_for_test("Bearer lzr_sk_old", &who);
        assert_eq!(cache.len(), 1);
        assert!(cache.try_get("Bearer lzr_sk_old").is_none());
        assert_eq!(cache.len(), 0, "expired entries must be swept");
    }
}
