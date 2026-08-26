---
number: "157"
id: B09-U28
severity: P1
status: resolved
resolved_branch: fix/157-portal-plan-trialing
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 157 — B09-U28 — Portal plan change is ACTIVE+token only

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/157-portal-plan-trialing`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U28 — Portal plan change is ACTIVE+token only (P1)

**Where:** `portal/page.tsx` 108–116. Cookie buyers (already 404) and TRIALING token buyers do not see the control. Ops can still change a trial’s plan (U04). Two products, two rules.

