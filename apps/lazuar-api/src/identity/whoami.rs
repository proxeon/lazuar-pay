//! Port of `Identity/WhoamiEndpoints.cs` — GET /v1/whoami.

use chrono::{DateTime, Utc};
use serde::Serialize;

use crate::hosting::PayError;
use crate::identity::bearer;
use crate::identity::one_client::OneClient;

#[derive(Debug, Clone, Serialize)]
pub struct WhoamiTenant {
    pub id: String,
    pub slug: Option<String>,
    pub name: Option<String>,
    pub role: Option<String>,
    pub status: Option<String>,
}

#[derive(Debug, Clone, Serialize)]
pub struct WhoamiResponse {
    pub user_id: String,
    pub email: Option<String>,
    pub name: Option<String>,
    pub is_platform_admin: bool,
    pub active_org_id: Option<String>,
    pub tenants: Vec<WhoamiTenant>,
}

#[derive(Debug)]
pub enum WhoamiOutcome {
    Ok(WhoamiResponse),
    Error(PayError),
}

pub fn whoami(
    one: &OneClient,
    transport: &dyn crate::transport::Transport,
    cache: Option<&crate::identity::whoami_cache::OneWhoamiCache>,
    auth_header: Option<&str>,
    tenant_hint: Option<&str>,
) -> WhoamiOutcome {
    let Some(authorization) = bearer::try_get(auth_header) else {
        return WhoamiOutcome::Error(PayError::unauthorized("Missing bearer token"));
    };
    if let Some(wrong) = bearer::reject_wrong_family(&authorization) {
        return WhoamiOutcome::Error(wrong);
    }

    if let Some(cache) = cache {
        if let Some(cached) = cache.try_get(&authorization) {
            return WhoamiOutcome::Ok(cached);
        }
    }

    match one.get_whoami(transport, &authorization, tenant_hint) {
        Ok(me) => {
            let resp = WhoamiResponse {
                user_id: me.user_id.clone().unwrap_or_default(),
                email: me.email,
                name: me.name,
                is_platform_admin: me.is_platform_admin,
                active_org_id: me.active_tenant_id,
                tenants: me
                    .tenants
                    .into_iter()
                    .filter_map(|t| {
                        t.id.map(|id| WhoamiTenant {
                            id,
                            slug: t.slug,
                            name: t.name,
                            role: t.role,
                            status: t.status,
                        })
                    })
                    .collect(),
            };
            if let Some(cache) = cache {
                cache.set(&authorization, &resp, bearer::is_machine_key(&authorization));
            }
            WhoamiOutcome::Ok(resp)
        }
        // Mapper parity: unparsable/absent identity reads surface as 503.
        Err(call) => WhoamiOutcome::Error(if call.timed_out || call.transport_failed {
            PayError::unavailable("Identity provider unreachable")
        } else {
            match call.status_code {
                401 => PayError::unauthorized("Identity provider rejected the token"),
                403 => PayError::forbidden("Identity provider forbade this caller"),
                _ => PayError::unavailable("Identity provider failed"),
            }
        }),
    }
}

/// Unused-timestamp parity placeholder: `created_at` stamps ride RFC 3339 (D010).
pub type CreatedAt = DateTime<Utc>;
