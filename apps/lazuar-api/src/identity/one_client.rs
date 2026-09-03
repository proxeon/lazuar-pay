//! Port of `Identity/Client/OneClient.cs` — whoami + authz-check over the
//! Transport seam (tests inject `FakeTransport`).

use serde::Deserialize;

use crate::transport::{OutRequest, Transport, TransportError};

#[derive(Debug, Clone, Deserialize)]
pub struct OneMeTenant {
    #[serde(default)]
    pub id: Option<String>,
    #[serde(default)]
    pub slug: Option<String>,
    #[serde(default)]
    pub name: Option<String>,
    #[serde(default)]
    pub role: Option<String>,
    #[serde(default)]
    pub status: Option<String>,
}

#[derive(Debug, Clone, Deserialize, Default)]
pub struct OneMeResponse {
    #[serde(default)]
    pub user_id: Option<String>,
    #[serde(default)]
    pub email: Option<String>,
    #[serde(default)]
    pub name: Option<String>,
    #[serde(default)]
    pub is_platform_admin: bool,
    #[serde(default)]
    pub active_tenant_id: Option<String>,
    #[serde(default)]
    pub tenants: Vec<OneMeTenant>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct OneAuthzCheckResponse {
    #[serde(default)]
    pub allowed: bool,
}

#[derive(Debug, Clone, Default)]
pub struct OneCall {
    pub status_code: u16,
    pub body: Option<String>,
    pub detail: Option<String>,
    pub timed_out: bool,
    pub transport_failed: bool,
}

pub struct OneClient {
    pub base_url: String,
    pub timeout_secs: u64,
}

fn read_detail(body: &str) -> Option<String> {
    let v: serde_json::Value = serde_json::from_str(body).ok()?;
    ["detail", "title", "message"]
        .iter()
        .find_map(|k| v.get(k).and_then(serde_json::Value::as_str))
        .map(str::to_string)
}

impl OneClient {
    fn send(&self, transport: &dyn Transport, request: OutRequest) -> OneCall {
        match transport.send(request) {
            Ok(resp) => OneCall {
                status_code: resp.status,
                body: Some(resp.body),
                ..OneCall::default()
            },
            Err(TransportError::Timeout { .. }) => OneCall { timed_out: true, ..OneCall::default() },
            Err(TransportError::Transport(_)) => OneCall { transport_failed: true, ..OneCall::default() },
        }
    }

    /// GET /me — whoami. 200 with an unparsable/absent user id surfaces as 503
    /// (the mapping-parity rule from `WhoamiEndpoints.Map`).
    pub fn get_whoami(
        &self,
        transport: &dyn Transport,
        authorization: &str,
        tenant_hint: Option<&str>,
    ) -> Result<OneMeResponse, OneCall> {
        let mut request = OutRequest {
            method: "GET".into(),
            url: format!("{}/me", self.base_url.trim_end_matches('/')),
            headers: vec![("Authorization".into(), authorization.to_string())],
            body: None,
        };
        if let Some(hint) = tenant_hint.filter(|h| !h.trim().is_empty()) {
            request.headers.push(("X-Lazuar-Tenant-Id".into(), hint.to_string()));
        }
        let call = self.send(transport, request);
        if call.status_code == 200 {
            let body = call.body.clone().unwrap_or_default();
            let me: OneMeResponse = match serde_json::from_str(&body) {
                Ok(me) => me,
                Err(_) => return Err(OneCall { status_code: 503, ..OneCall::default() }),
            };
            if me.user_id.as_deref().is_none_or(str::is_empty) {
                // Mapper parity: no user id → treated as a failed identity read.
                return Err(OneCall { status_code: 503, ..OneCall::default() });
            }
            return Ok(me);
        }
        Err(OneCall {
            detail: call.body.as_deref().and_then(read_detail),
            ..call
        })
    }

    /// POST /tenants/{orgId}/authz/check — membership check.
    pub fn check_member(
        &self,
        transport: &dyn Transport,
        authorization: &str,
        org_id: &str,
        tenant_hint: Option<&str>,
    ) -> Result<bool, OneCall> {
        let mut request = OutRequest {
            method: "POST".into(),
            url: format!(
                "{}/tenants/{}/authz/check",
                self.base_url.trim_end_matches('/'),
                org_id
            ),
            headers: vec![("Authorization".into(), authorization.to_string())],
            body: Some(
                serde_json::json!({
                    "relation": "member",
                    "object": { "type": "tenant", "id": org_id }
                })
                .to_string(),
            ),
        };
        if let Some(hint) = tenant_hint.filter(|h| !h.trim().is_empty()) {
            request.headers.push(("X-Lazuar-Tenant-Id".into(), hint.to_string()));
        }
        let call = self.send(transport, request);
        if call.status_code == 200 {
            let body = call.body.clone().unwrap_or_default();
            let parsed: OneAuthzCheckResponse = match serde_json::from_str(&body) {
                Ok(parsed) => parsed,
                Err(_) => return Err(OneCall { status_code: 503, ..OneCall::default() }),
            };
            return Ok(parsed.allowed);
        }
        Err(OneCall {
            detail: call.body.as_deref().and_then(read_detail),
            ..call
        })
    }
}
