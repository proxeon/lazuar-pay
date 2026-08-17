---
number: "139"
id: B09-U10
severity: P1
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 139 — B09-U10 — Update-payment `err=1` is never shown

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U10 — Update-payment `err=1` is never shown (P1)

**Where:** `update-payment/[subId]/page.tsx` 48–49 vs the render (no `err` read).  
**Walk:** POST fails. Redirect back. Same card. Buyer retries forever.

