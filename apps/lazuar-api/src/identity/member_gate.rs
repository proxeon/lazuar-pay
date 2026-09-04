//! Port of `Identity/MemberGate.cs` — member/writer authorization against One.

use crate::hosting::PayError;
use crate::identity::bearer;
use crate::identity::one_client::OneClient;

fn forbidden_detail(detail: Option<&str>) -> Option<String> {
    detail
        .map(str::trim)
        .filter(|d| {
            d.to_lowercase().contains("suspend") || d.to_lowercase().contains("scope")
        })
        .map(str::to_string)
}

/// Machine-key path: whoami tenants must contain the org, active.
fn key_bound(
    one: &OneClient,
    transport: &dyn crate::transport::Transport,
    authorization: &str,
    org_id: &str,
    tenant_hint: Option<&str>,
) -> Result<(), PayError> {
    let who = match one.get_whoami(transport, authorization, tenant_hint) {
        Ok(me) => me,
        Err(call) => {
            if call.timed_out || call.transport_failed {
                return Err(PayError::unavailable("Identity provider unreachable"));
            }
            return Err(match call.status_code {
                401 => PayError::unauthorized("Identity provider rejected the token"),
                403 => PayError::forbidden(
                    forbidden_detail(call.detail.as_deref()).unwrap_or_else(|| "Not a member of this org".to_string()),
                ),
                400 => PayError::bad_request(call.detail.unwrap_or_else(|| "Identity provider rejected the request".into())),
                429 => PayError::new(429, "Too Many Requests", "Identity provider rate limited"),
                _ => PayError::unavailable("Identity provider failed"),
            });
        }
    };

    if who.tenants.is_empty() {
        return Err(PayError::forbidden("Not a member of this org"));
    }
    let tenant = who.tenants.iter().find(|t| t.id.as_deref() == Some(org_id));
    let Some(tenant) = tenant else {
        return Err(PayError::forbidden("Not a member of this org"));
    };
    if !tenant.status.as_deref().is_some_and(|s| s.eq_ignore_ascii_case("active"))
        && tenant.status.as_deref().is_some_and(|s| !s.trim().is_empty())
    {
        return Err(PayError::forbidden("Tenant is suspended."));
    }
    Ok(())
}

/// RequireMembership: machine keys check binding; humans check authz membership.
pub fn require_member(
    one: &OneClient,
    transport: &dyn crate::transport::Transport,
    auth_header: Option<&str>,
    tenant_hint: Option<&str>,
    org_id: &str,
) -> Result<(), PayError> {
    let Some(authorization) = bearer::try_get(auth_header) else {
        return Err(PayError::unauthorized("Missing bearer token"));
    };
    if let Some(wrong) = bearer::reject_wrong_family(&authorization) {
        return Err(wrong);
    }
    if org_id.trim().is_empty() {
        return Err(PayError::bad_request("org_id is required"));
    }

    if bearer::is_machine_key(&authorization) {
        key_bound(one, transport, &authorization, org_id, tenant_hint)?;
        return Ok(());
    }

    match one.check_member(transport, &authorization, org_id, tenant_hint) {
        Ok(true) => Ok(()),
        Ok(false) => Err(PayError::forbidden("Not a member of this org")),
        Err(call) => {
            if call.timed_out || call.transport_failed {
                return Err(PayError::unavailable("Identity provider unreachable"));
            }
            Err(match call.status_code {
                401 => PayError::unauthorized("Identity provider rejected the token"),
                403 => PayError::forbidden(
                    forbidden_detail(call.detail.as_deref())
                        .unwrap_or_else(|| "Not a member of this org".to_string()),
                ),
                400 => PayError::bad_request(
                    call.detail
                        .unwrap_or_else(|| "Identity provider rejected the request".into()),
                ),
                429 => PayError::new(429, "Too Many Requests", "Identity provider rate limited"),
                200 => PayError::forbidden("Not a member of this org"),
                _ => PayError::unavailable("Identity provider failed"),
            })
        }
    }
}

/// RequireWriter: member first, then the tenant role must be owner/admin.
pub fn require_writer(
    one: &OneClient,
    transport: &dyn crate::transport::Transport,
    auth_header: Option<&str>,
    tenant_hint: Option<&str>,
    org_id: &str,
) -> Result<(), PayError> {
    if let Some(authorization) = bearer::try_get(auth_header) {
        if bearer::is_machine_key(&authorization) {
            return require_member(one, transport, auth_header, tenant_hint, org_id);
        }
    }

    require_member(one, transport, auth_header, tenant_hint, org_id)?;

    let Some(authorization) = bearer::try_get(auth_header) else {
        return Err(PayError::unauthorized("Missing bearer token"));
    };
    let who = one
        .get_whoami(transport, &authorization, tenant_hint)
        .map_err(|_| PayError::unavailable("Identity provider failed"))?;
    let tenant = who.tenants.iter().find(|t| t.id.as_deref() == Some(org_id));
    let Some(tenant) = tenant else {
        return Err(PayError::forbidden("Not a member of this org"));
    };
    if !tenant.status.as_deref().is_some_and(|s| s.eq_ignore_ascii_case("active"))
        && tenant.status.as_deref().is_some_and(|s| !s.trim().is_empty())
    {
        return Err(PayError::forbidden("Tenant is suspended."));
    }
    match tenant.role.as_deref() {
        Some("owner") | Some("admin") => Ok(()),
        _ => Err(PayError::forbidden("Writer role required")),
    }
}
