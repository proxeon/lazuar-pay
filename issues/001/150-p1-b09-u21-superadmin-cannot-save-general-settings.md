---
number: "150"
id: B09-U21
severity: P1
status: resolved
resolved_branch: fix/150-superadmin-general-settings
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 150 — B09-U21 — Superadmin cannot Save General Settings

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/150-superadmin-general-settings`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U21 — Superadmin cannot Save General Settings (P1)

**Where:** `UpdateWorkspaceCommand.cs` 32–35; `WorkspaceEndpoints.cs` 147–157; `GeneralSettingsPage.tsx` 79–90.  
Role must be the string `ADMIN`. Superadmin entitlement is `SUPER_ADMIN`. Save → “Unauthorized to update workspace.”

