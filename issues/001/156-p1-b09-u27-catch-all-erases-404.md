---
number: "156"
id: B09-U27
severity: P1
status: resolved
resolved_branch: fix/156-catchall-404
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 156 — B09-U27 — Catch-all erases 404

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/156-catchall-404`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U27 — Catch-all erases 404 (P1)

**Where:** ops `App.tsx` 249; admin `App.tsx` 94.  
Bad bookmarks become the dashboard / gateways. `/ops/chat` does too.

