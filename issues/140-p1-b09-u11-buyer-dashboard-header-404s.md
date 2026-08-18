---
number: "140"
id: B09-U11
severity: P1
status: resolved
resolved_branch: fix/140-buyer-dashboard-header
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 140 — B09-U11 — “Buyer Dashboard” header 404s

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/140-buyer-dashboard-header`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U11 — “Buyer Dashboard” header 404s (P1)

**Where:** `portal/layout.tsx` 21–26; no `app/[tenantSlug]/page.tsx`.  
**Walk:** Click the only brand link on the portal. Localized 404.

