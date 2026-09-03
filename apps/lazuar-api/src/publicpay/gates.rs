//! Reusable per-key gate map (C# `ConcurrentDictionary<string, SemaphoreSlim>`)
//! — used by the fulfiller (per checkout) and the Start flow (per checkout) and
//! link minting (per link).

use std::collections::HashMap;
use std::sync::{Arc, Mutex};

#[derive(Default)]
pub struct GateMap {
    inner: Mutex<HashMap<String, Arc<Mutex<()>>>>,
}

impl GateMap {
    pub fn new() -> Self {
        Self::default()
    }

    /// Hold this key's gate for the duration of `f`.
    pub fn with_gate<R>(&self, key: &str, f: impl FnOnce() -> R) -> R {
        let arc = {
            let mut map = self.inner.lock().expect("gate map");
            map.entry(key.to_string())
                .or_insert_with(|| Arc::new(Mutex::new(())))
                .clone()
        };
        let _guard = arc.lock().expect("gate");
        f()
    }
}
