//! Per-process public limiter (issue 016). Two replicas = 2×.

use std::collections::HashMap;
use std::sync::Mutex;
use std::time::{Duration, Instant};

pub struct Limiter {
    max: u32,
    inner: Mutex<HashMap<String, (u32, Instant)>>,
}

impl Limiter {
    pub fn new(max: u32) -> Self {
        Self {
            max: max.max(1),
            inner: Mutex::new(HashMap::new()),
        }
    }

    pub fn try_acquire(&self, raw_token: &str) -> bool {
        let mut key = raw_token.to_string();
        key.truncate(256);
        let mut map = self.inner.lock().expect("limiter");
        if map.len() > 4096 {
            let horizon = Duration::from_secs(3600);
            map.retain(|_, (_, t)| t.elapsed() < horizon);
        }
        let now = Instant::now();
        let e = map.entry(key).or_insert((0, now));
        if e.1.elapsed() >= Duration::from_secs(60) {
            *e = (0, now);
        }
        if e.0 >= self.max {
            return false;
        }
        e.0 += 1;
        true
    }
}
