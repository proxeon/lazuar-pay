---
number: "279"
id: B07-I39
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 279 — B07-I39 — No MFA, SSO, lockout, session list, password complexity

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I39 — P2 — No MFA, SSO, lockout, session list, password complexity

**Where.** `PasswordService` is BCrypt work factor 11 (`PasswordService.cs:15–16`; `appsettings.json:32–34`). `GlobalUser` has no lockout/MFA/last-login.

**What.** Procurement-questionnaire fail. Not a crash. Do not put “SSO” on a pricing page.

