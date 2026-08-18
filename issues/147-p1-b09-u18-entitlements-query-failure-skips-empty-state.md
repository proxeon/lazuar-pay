---
number: "147"
id: B09-U18
severity: P1
status: resolved
resolved_branch: fix/147-entitlements-error
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 147 — B09-U18 — Entitlements query failure skips empty state

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/147-entitlements-error`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U18 — Entitlements query failure skips empty state (P1)

**Where:** `App.tsx` 81–89, 127–140.  
**Walk:** `/one/me/entitlements` 500. Full chrome, stale `ops_active_workspace_id`, every page 403s. No create. No error.

