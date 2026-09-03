//! Port of `PublicPay/PublicPayLimiter.cs`.
//!
//! Issue 016 (issues/001): the key is the raw `{token}` route value from
//! unauthenticated callers, and TryAcquire runs before any token validation or
//! DB lookup — requests for nonexistent tokens used to allocate a permanent
//! dictionary entry each (no removal path at all), so distinct junk tokens grew
//! the process heap until OOM. Keys are capped (bounded per entry) and idle
//! entries are periodically swept.

use std::collections::HashMap;
use std::sync::Mutex;

const MAX_KEY_LENGTH: usize = 256;
const SWEEP_EVERY_CALLS: u64 = 4096;
const SWEEP_HORIZON_SECONDS: i64 = 3600; // comfortably above every window in use

#[derive(Default)]
pub struct PublicPayLimiter {
    inner: Mutex<Inner>,
}

#[derive(Default)]
struct Inner {
    hits: HashMap<String, Vec<i64>>,
    calls: u64,
}

impl PublicPayLimiter {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn try_acquire(&self, raw_key: &str, max: i32, window_seconds: i64) -> bool {
        let mut inner = self.inner.lock().expect("limiter");
        let now = Utc::now().timestamp();

        // Long junk tokens must not buy unbounded memory; truncation keeps them limited.
        let key = if raw_key.len() > MAX_KEY_LENGTH {
            raw_key[..MAX_KEY_LENGTH].to_string()
        } else {
            raw_key.to_string()
        };

        inner.calls += 1;
        if inner.calls % SWEEP_EVERY_CALLS == 0 {
            let cutoff = now - SWEEP_HORIZON_SECONDS;
            inner.hits.retain(|_, list| list.iter().any(|t| *t >= cutoff));
        }

        let list = inner.hits.entry(key).or_default();
        list.retain(|t| *t >= now - window_seconds);
        if list.len() >= max as usize {
            return false;
        }
        list.push(now);
        true
    }

    /// Test hook: how many keys are currently tracked.
    pub fn tracked_keys(&self) -> usize {
        self.inner.lock().expect("limiter").hits.len()
    }

    /// Test hook: drop keys whose every recorded hit is older than the cutoff.
    pub fn sweep(&self, cutoff_unix: i64) {
        self.inner
            .lock()
            .expect("limiter")
            .hits
            .retain(|_, list| list.iter().any(|t| *t >= cutoff_unix));
    }
}

use chrono::Utc;
