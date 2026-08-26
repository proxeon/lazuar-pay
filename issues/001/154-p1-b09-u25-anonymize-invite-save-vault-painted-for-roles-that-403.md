---
number: "154"
id: B09-U25
severity: P1
status: resolved
resolved_branch: fix/154-role-gated-buttons
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 154 — B09-U25 — Anonymize / Invite / Save vault painted for roles that 403

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/154-role-gated-buttons`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U25 — Anonymize / Invite / Save vault painted for roles that 403 (P1)

**Where:** `SubscribersPage.tsx` 676–688; `TeamPage.tsx` 66–97; `PaymentSettingsPage.tsx` 433–440.  
Member sees Anonymize. Viewer sees Invite and Save Credentials. Toasts only.

