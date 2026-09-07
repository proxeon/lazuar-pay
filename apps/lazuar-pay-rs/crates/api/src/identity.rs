//! One doors. v1 forwards Bearer; tests inject [`FakeOne`] (032/13).

use std::collections::{HashMap, HashSet};
use std::sync::Mutex;
use std::time::{Duration, Instant};

use axum::extract::FromRequestParts;
use axum::http::request::Parts;
use axum::http::{header, StatusCode};
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};

use crate::errors::problem;
use crate::AppState;

#[derive(Clone, Debug, Serialize)]
pub struct WhoamiTenant {
    pub id: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub slug: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub name: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub role: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub status: Option<String>,
}

#[derive(Clone, Debug, Serialize)]
pub struct WhoamiResponse {
    pub user_id: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub email: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub name: Option<String>,
    pub is_platform_admin: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub active_org_id: Option<String>,
    pub tenants: Vec<WhoamiTenant>,
    /// Set by the bearer extractor. Never on the wire.
    #[serde(skip)]
    pub machine_key: bool,
}

#[derive(Debug)]
pub enum OneError {
    Unauthorized,
    Forbidden,
    BadRequest(String),
    RateLimited,
    Unreachable,
    Failed,
}

/// Identity backend. Tests inject [`FakeOne`]; serve uses HTTP One unless Testing.
#[derive(Clone)]
pub enum OneClient {
    Fake(FakeOne),
    Http(HttpOne),
}

impl OneClient {
    pub async fn whoami(
        &self,
        authorization: &str,
        tenant_hint: Option<&str>,
    ) -> Result<WhoamiResponse, OneError> {
        match self {
            Self::Fake(f) => f.whoami(authorization),
            Self::Http(h) => h.whoami(authorization, tenant_hint).await,
        }
    }
}

#[derive(Clone, Debug, Default)]
pub struct FakeOne {
    users: HashMap<String, WhoamiResponse>,
}

impl FakeOne {
    pub fn writer(org_id: &str) -> Self {
        let owner = WhoamiResponse {
            user_id: "u1".into(),
            email: Some("dev@lazuar.test".into()),
            name: Some("Dev".into()),
            is_platform_admin: false,
            active_org_id: Some(org_id.into()),
            tenants: vec![WhoamiTenant {
                id: org_id.into(),
                slug: None,
                name: Some("Test org".into()),
                role: Some("owner".into()),
                status: Some("active".into()),
            }],
            machine_key: false,
        };
        let machine = WhoamiResponse {
            user_id: "k1".into(),
            email: None,
            name: None,
            is_platform_admin: false,
            active_org_id: Some(org_id.into()),
            tenants: vec![WhoamiTenant {
                id: org_id.into(),
                slug: None,
                name: Some("Test org".into()),
                role: Some("member".into()),
                status: Some("active".into()),
            }],
            machine_key: true,
        };
        let other = WhoamiResponse {
            user_id: "u2".into(),
            email: None,
            name: None,
            is_platform_admin: false,
            active_org_id: Some("t2".into()),
            tenants: vec![WhoamiTenant {
                id: "t2".into(),
                slug: None,
                name: Some("Other org".into()),
                role: Some("owner".into()),
                status: Some("active".into()),
            }],
            machine_key: false,
        };
        let member = WhoamiResponse {
            user_id: "u3".into(),
            email: None,
            name: None,
            is_platform_admin: false,
            active_org_id: Some(org_id.into()),
            tenants: vec![WhoamiTenant {
                id: org_id.into(),
                slug: None,
                name: Some("Test org".into()),
                role: Some("member".into()),
                status: Some("active".into()),
            }],
            machine_key: false,
        };
        Self::default()
            .with("test-writer", owner)
            .with("lzr_sk_test", machine)
            .with("test-other", other)
            .with("test-member", member)
    }

    pub fn with(mut self, bearer: &str, whoami: WhoamiResponse) -> Self {
        self.users.insert(bearer.to_string(), whoami);
        self
    }

    fn whoami(&self, authorization: &str) -> Result<WhoamiResponse, OneError> {
        let token = strip_bearer(authorization).ok_or(OneError::Unauthorized)?;
        if is_sk_family(token) {
            return Err(OneError::Unauthorized);
        }
        self.users.get(token).cloned().ok_or(OneError::Unauthorized)
    }
}

#[derive(Clone)]
pub struct HttpOne {
    base: String,
    client: reqwest::Client,
}

impl HttpOne {
    pub fn new(base: impl Into<String>) -> Self {
        let base = base.into().trim_end_matches('/').to_string();
        let client = reqwest::Client::builder()
            .timeout(Duration::from_secs(5))
            .build()
            .unwrap_or_else(|_| reqwest::Client::new());
        Self { base, client }
    }

    async fn whoami(
        &self,
        authorization: &str,
        tenant_hint: Option<&str>,
    ) -> Result<WhoamiResponse, OneError> {
        let url = format!("{}/me", self.base);
        let mut req = self
            .client
            .get(&url)
            .header(header::AUTHORIZATION, authorization);
        if let Some(hint) = tenant_hint.filter(|s| !s.is_empty()) {
            req = req.header("X-Lazuar-Tenant-Id", hint);
        }
        let res = req.send().await.map_err(|_| OneError::Unreachable)?;
        let status = res.status();
        if status == reqwest::StatusCode::UNAUTHORIZED {
            return Err(OneError::Unauthorized);
        }
        if status == reqwest::StatusCode::FORBIDDEN {
            return Err(OneError::Forbidden);
        }
        if status == reqwest::StatusCode::TOO_MANY_REQUESTS {
            return Err(OneError::RateLimited);
        }
        if status == reqwest::StatusCode::BAD_REQUEST {
            let detail = res.text().await.unwrap_or_default();
            return Err(OneError::BadRequest(detail));
        }
        if !status.is_success() {
            return Err(OneError::Failed);
        }
        let me: OneMe = res.json().await.map_err(|_| OneError::Failed)?;
        map_me(me).ok_or(OneError::Failed)
    }
}

#[derive(Deserialize)]
struct OneMe {
    user_id: Option<String>,
    email: Option<String>,
    name: Option<String>,
    #[serde(default)]
    is_platform_admin: bool,
    active_tenant_id: Option<String>,
    #[serde(default)]
    tenants: Vec<OneTenant>,
}

#[derive(Deserialize)]
struct OneTenant {
    id: Option<String>,
    slug: Option<String>,
    name: Option<String>,
    role: Option<String>,
    status: Option<String>,
}

fn map_me(me: OneMe) -> Option<WhoamiResponse> {
    let user_id = me.user_id.filter(|s| !s.is_empty())?;
    let tenants = me
        .tenants
        .into_iter()
        .filter_map(|t| {
            let id = t.id.filter(|s| !s.is_empty())?;
            Some(WhoamiTenant {
                id,
                slug: t.slug,
                name: t.name,
                role: t.role,
                status: t.status,
            })
        })
        .collect();
    Some(WhoamiResponse {
        user_id,
        email: me.email,
        name: me.name,
        is_platform_admin: me.is_platform_admin,
        active_org_id: me.active_tenant_id.filter(|s| !s.is_empty()),
        tenants,
        machine_key: false,
    })
}

/// SHA-256 of the full Authorization header; 60s TTL; machine keys only.
#[derive(Default)]
pub struct WhoamiCache {
    inner: Mutex<CacheInner>,
}

#[derive(Default)]
struct CacheInner {
    by_auth: HashMap<[u8; 32], (WhoamiResponse, Instant)>,
    by_org: HashMap<String, HashSet<[u8; 32]>>,
    by_key: HashMap<String, HashSet<[u8; 32]>>,
}

impl WhoamiCache {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn get(&self, authorization: &str) -> Option<WhoamiResponse> {
        let key = cache_key(authorization);
        let map = self.inner.lock().ok()?;
        let (who, at) = map.by_auth.get(&key)?;
        if at.elapsed() < Duration::from_secs(60) {
            Some(who.clone())
        } else {
            None
        }
    }

    pub fn set(&self, authorization: &str, who: WhoamiResponse) {
        let Ok(mut map) = self.inner.lock() else {
            return;
        };
        let hash = cache_key(authorization);
        forget_hash(&mut map, hash);
        for t in &who.tenants {
            map.by_org.entry(t.id.clone()).or_default().insert(hash);
        }
        if !who.user_id.is_empty() {
            map.by_key
                .entry(who.user_id.clone())
                .or_default()
                .insert(hash);
        }
        map.by_auth.insert(hash, (who, Instant::now()));
    }

    pub fn remove(&self, authorization: &str) {
        let Ok(mut map) = self.inner.lock() else {
            return;
        };
        forget_hash(&mut map, cache_key(authorization));
    }

    pub fn invalidate_org(&self, org_id: &str) {
        let Ok(mut map) = self.inner.lock() else {
            return;
        };
        let hashes: Vec<[u8; 32]> = map
            .by_org
            .remove(org_id)
            .map(|s| s.into_iter().collect())
            .unwrap_or_default();
        for h in hashes {
            forget_hash(&mut map, h);
        }
    }

    pub fn invalidate_key(&self, key_id: &str) {
        if key_id.is_empty() {
            return;
        }
        let Ok(mut map) = self.inner.lock() else {
            return;
        };
        let hashes: Vec<[u8; 32]> = map
            .by_key
            .remove(key_id)
            .map(|s| s.into_iter().collect())
            .unwrap_or_default();
        for h in hashes {
            forget_hash(&mut map, h);
        }
    }
}

fn forget_hash(map: &mut CacheInner, hash: [u8; 32]) {
    if let Some((who, _)) = map.by_auth.remove(&hash) {
        for t in &who.tenants {
            if let Some(set) = map.by_org.get_mut(&t.id) {
                set.remove(&hash);
                if set.is_empty() {
                    map.by_org.remove(&t.id);
                }
            }
        }
        if let Some(set) = map.by_key.get_mut(&who.user_id) {
            set.remove(&hash);
            if set.is_empty() {
                map.by_key.remove(&who.user_id);
            }
        }
    }
}

fn cache_key(authorization: &str) -> [u8; 32] {
    Sha256::digest(authorization.as_bytes()).into()
}

pub fn is_machine_key(token: &str) -> bool {
    token.starts_with("lzr_sk_")
}

pub fn is_sk_family(token: &str) -> bool {
    token.starts_with("sk_")
}

fn strip_bearer(raw: &str) -> Option<&str> {
    let rest = raw
        .strip_prefix("Bearer ")
        .or_else(|| raw.strip_prefix("bearer "))?;
    let token = rest.trim();
    if token.is_empty() {
        None
    } else {
        Some(token)
    }
}

pub struct Bearer(pub WhoamiResponse);

impl FromRequestParts<AppState> for Bearer {
    type Rejection = axum::response::Response;

    async fn from_request_parts(
        parts: &mut Parts,
        state: &AppState,
    ) -> Result<Self, Self::Rejection> {
        let Some(raw) = parts
            .headers
            .get(header::AUTHORIZATION)
            .and_then(|v| v.to_str().ok())
        else {
            return Err(problem(
                StatusCode::UNAUTHORIZED,
                "Unauthorized",
                "Missing bearer token",
            ));
        };
        let Some(token) = strip_bearer(raw) else {
            return Err(problem(
                StatusCode::UNAUTHORIZED,
                "Unauthorized",
                "Missing bearer token",
            ));
        };
        if is_sk_family(token) {
            return Err(problem(
                StatusCode::UNAUTHORIZED,
                "Unauthorized",
                "Invalid bearer",
            ));
        }
        let hint = parts
            .headers
            .get("X-Lazuar-Tenant-Id")
            .and_then(|v| v.to_str().ok());
        let machine = is_machine_key(token);
        if machine {
            if let Some(mut cached) = state.whoami_cache.get(raw) {
                cached.machine_key = true;
                return Ok(Bearer(cached));
            }
        }
        match state.one.whoami(raw, hint).await {
            Ok(mut w) => {
                w.machine_key = machine;
                if machine {
                    state.whoami_cache.set(raw, w.clone());
                }
                Ok(Bearer(w))
            }
            Err(OneError::Unauthorized) => {
                if machine {
                    state.whoami_cache.remove(raw);
                }
                Err(problem(
                    StatusCode::UNAUTHORIZED,
                    "Unauthorized",
                    "Identity provider rejected the token",
                ))
            }
            Err(OneError::Forbidden) => Err(problem(
                StatusCode::FORBIDDEN,
                "Forbidden",
                "Identity provider forbade this caller",
            )),
            Err(OneError::BadRequest(d)) => Err(problem(
                StatusCode::BAD_REQUEST,
                "Bad Request",
                if d.is_empty() {
                    "Identity provider rejected the request"
                } else {
                    &d
                },
            )),
            Err(OneError::RateLimited) => Err(problem(
                StatusCode::TOO_MANY_REQUESTS,
                "Too Many Requests",
                "Identity provider rate limited",
            )),
            Err(OneError::Unreachable) => Err(problem(
                StatusCode::SERVICE_UNAVAILABLE,
                "Service Unavailable",
                "Identity provider unreachable",
            )),
            Err(OneError::Failed) => Err(problem(
                StatusCode::SERVICE_UNAVAILABLE,
                "Service Unavailable",
                "Identity provider failed",
            )),
        }
    }
}

#[allow(clippy::result_large_err)]
pub fn require_member(who: &WhoamiResponse, org_id: &str) -> Result<(), axum::response::Response> {
    if org_id.is_empty() {
        return Err(problem(
            StatusCode::BAD_REQUEST,
            "Bad Request",
            "org_id is required",
        ));
    }
    let Some(t) = who.tenants.iter().find(|t| t.id == org_id) else {
        return Err(problem(StatusCode::FORBIDDEN, "Forbidden", "Not a member"));
    };
    if t.status.as_deref() != Some("active") {
        return Err(problem(
            StatusCode::FORBIDDEN,
            "Forbidden",
            "Tenant is suspended.",
        ));
    }
    Ok(())
}

#[allow(clippy::result_large_err)]
pub fn require_writer(who: &WhoamiResponse, org_id: &str) -> Result<(), axum::response::Response> {
    require_member(who, org_id)?;
    if who.machine_key {
        return Ok(());
    }
    let t = who
        .tenants
        .iter()
        .find(|t| t.id == org_id)
        .expect("require_member");
    match t.role.as_deref() {
        Some("owner") | Some("admin") => Ok(()),
        _ => Err(problem(
            StatusCode::FORBIDDEN,
            "Forbidden",
            "Writer role required",
        )),
    }
}

pub async fn whoami(Bearer(w): Bearer) -> axum::Json<WhoamiResponse> {
    axum::Json(w)
}
