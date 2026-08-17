---
number: "117"
id: B07-I07
severity: P1
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 117 — B07-I07 — Security stamp is only enforced on `/auth/me` and platform `/auth/me`

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I07 — P1 — Security stamp is only enforced on `/auth/me` and platform `/auth/me`

**Where.** `AuthEndpoints.cs:148–153`; `PlatformAuthEndpoints.cs:91–96`. No stamp filter in JWT `TokenValidationParameters` (`AuthAndCorsExtensions.cs:40–49`).

**What.** Stolen cookie works on invite, keys, refunds, everything except the SPA’s session probe, until `ExpiryHours` (24). Password change rotates the stamp (`GlobalUser.cs:55–58`) and does not emit a session-revocation list.

Unchanged from 008 H4 / pre-wave.

