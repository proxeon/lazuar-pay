---
number: "159"
id: B09-U30
severity: P1
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 159 — B09-U30 — Accept-invite maps every 5xx to “already accepted”

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U30 — Accept-invite maps every 5xx to “already accepted” (P1)

**Where:** `AcceptInvitePage.tsx` 40–45.  
A down database looks like a used invite.

### P2 — lying labels, dead routes, i18n holes, museums

