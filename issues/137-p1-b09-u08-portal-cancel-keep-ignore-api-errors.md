---
number: "137"
id: B09-U08
severity: P1
status: resolved
resolved_branch: fix/137-portal-cancel-errors
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 137 — B09-U08 — Portal cancel / keep ignore API errors

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/137-portal-cancel-errors`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U08 — Portal cancel / keep ignore API errors (P1)

**Where:** `portal/page.tsx` 132–166, 181–188.  
**What:** Server actions `await` the POST and always `revalidatePath`. 401/400 look like success.  
**Walk:** Token expired. Buyer clicks Cancel Plan. Page reloads. Subscription is still ACTIVE. No error.

