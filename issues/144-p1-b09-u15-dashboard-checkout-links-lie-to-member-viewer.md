---
number: "144"
id: B09-U15
severity: P1
status: resolved
resolved_branch: fix/144-dashboard-member-403
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 144 — B09-U15 — Dashboard + Checkout Links lie to Member/Viewer

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/144-dashboard-member-403`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U15 — Dashboard + Checkout Links lie to Member/Viewer (P1)

**Where:** `DashboardPage.tsx` 27–34, 75–76, 85–86, 111–155; `ProductsPage.tsx` 64–65, 105–133.  
**Walk:** Member operates commerce. Net Cash is RM 0.00. Getting started never completes. Rose “gateway not configured” bar even when CHIP is live.

