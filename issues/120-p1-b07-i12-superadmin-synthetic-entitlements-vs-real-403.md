---
number: "120"
id: B07-I12
severity: P1
status: resolved
resolved_branch: fix/120-superadmin-403
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 120 — B07-I12 — Superadmin synthetic entitlements vs real 403

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/120-superadmin-403`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I12 — P1 — Superadmin synthetic entitlements vs real 403

**Where.** `WorkspaceEndpoints.cs:145–159` vs `TenantSecurityMiddleware.cs:90–103` vs genesis membership only on system org (`SystemGenesisBootstrapperJob.cs:90–100`).

**What.** Support switcher shows every live tenant. Every `/admin/*` call 403s. Looks like Access Denied after LP-184 taught ops to treat empty entitlements as “create,” not “denied.” System admins are the one population that **cannot** use EmptyWorkspaceState to recover (their list is never empty if any org is active).

