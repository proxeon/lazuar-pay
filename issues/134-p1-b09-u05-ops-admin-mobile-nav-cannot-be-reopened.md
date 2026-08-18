---
number: "134"
id: B09-U05
severity: P1
status: resolved
resolved_branch: fix/134-mobile-nav-hamburger
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 134 — B09-U05 — Ops/admin mobile nav cannot be reopened

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/134-mobile-nav-hamburger`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U05 — Ops/admin mobile nav cannot be reopened (P1)

**Where:** `lazuar-ops/src/App.tsx` 52–61, 204–206; `PageLayout.tsx` (no hamburger); `lazuar-admin/src/App.tsx` 17–26; both `use-mobile.ts` unused.  
**Walk:** iPhone, `/commerce/dashboard`. Rail is off-screen. There is no button that calls `setIsSidebarOpen(true)`.  
008 filed this. Still open.

