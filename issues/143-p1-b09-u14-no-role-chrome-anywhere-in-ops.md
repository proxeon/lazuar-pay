---
number: "143"
id: B09-U14
severity: P1
status: resolved
resolved_branch: fix/143-ops-role-chrome
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 143 — B09-U14 — No role chrome anywhere in ops

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/143-ops-role-chrome`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U14 — No role chrome anywhere in ops (P1)

**Where:** `App.tsx` 36–40; `Sidebar.tsx` 287–291; `PageLayout.tsx` 75–76; every mutation button.  
**Walk:** Three humans, three roles, one UI. Failure is a Sonner toast — if the query layer surfaces `detail`.

