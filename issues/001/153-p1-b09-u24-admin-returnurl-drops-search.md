---
number: "153"
id: B09-U24
severity: P1
status: resolved
resolved_branch: fix/153-admin-returnurl-search
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 153 — B09-U24 — Admin returnUrl drops search

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/153-admin-returnurl-search`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U24 — Admin returnUrl drops search (P1)

**Where:** `lazuar-admin/src/App.tsx` 33. Ops includes search (`68`). Admin does not. Low traffic (admin has no query-string pages today) but the pattern is the one 297ba98 just fixed on ops.

