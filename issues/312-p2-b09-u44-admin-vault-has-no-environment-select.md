---
number: "312"
id: B09-U44
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 312 — B09-U44 — Admin vault has no environment select

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U44 — Admin vault has no environment select (P2)

Ops does (`230:242:PaymentSettingsPage.tsx`). Admin does not. Hub SaaS top-ups cannot mark test vs live in the UI.

