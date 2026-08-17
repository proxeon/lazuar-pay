---
number: "123"
id: B07-I20
severity: P1
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 123 — B07-I20 — `IntegratorProvisionAuth` treats injected membership `SUPER_ADMIN` as platform admin

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I20 — P1 — `IntegratorProvisionAuth` treats injected membership `SUPER_ADMIN` as platform admin

**Where.** `IntegratorProvisionAuth.cs:73–76`:

```csharp
var isSystemAdmin =
    string.Equals(principal.FindFirst("is_system_admin")?.Value, "true", StringComparison.OrdinalIgnoreCase)
    || principal.IsInRole("SUPER_ADMIN");
```

Provision path is tenant-exempt (`TenantSecurityMiddleware.cs:138`). Middleware still **injects** membership role when `X-Tenant-Id` is present (`:85–88`). Ops always sends the header. `NormalizeOwnerRole` allows `SUPER_ADMIN` (`ProvisionAuraWorkspaceCommandHandler.Normalizers.cs:51–66`).

**What.** An integrator who attached `owner_role=SUPER_ADMIN` (documented as “workspace membership, not global system admin”) hands that human a claim that opens `POST /one/integrations/workspaces/provision` without the provision secret. They can mint new workspaces, bootstrap `sk_*` keys, and attach owners. JWT is still `CLIENT`; `is_system_admin` is false. The `|| IsInRole` is the bug.

Platform `/api/v1/platform` also `RequireRole("SUPER_ADMIN")` (`ModuleRegistrationExtensions.cs:82`) but `OnMessageReceived` only reads `lazuar_admin_auth` on that path, so an ops cookie does **not** walk into the platform group unless they also send Bearer JWT. A Bearer of the ops JWT with only `CLIENT` fails; after injection… injection requires the tenant middleware to have run with a header, which it does for `/api/v1/platform`? **No** — platform short-circuits before membership lookup (`TenantSecurityMiddleware.cs:29–33`). Platform group is safe from this injection. Provision is not.

Invite cannot mint `SUPER_ADMIN`. Default provision role is `ADMIN`. The hole is real but needs the hatch to have chosen `SUPER_ADMIN`.

