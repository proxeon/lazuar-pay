---
number: "263"
id: B07-I21
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 263 — B07-I21 — Human ADMIN bypass of Integration* policies (except PaymentsMe)

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I21 — P2 — Human ADMIN bypass of Integration* policies (except PaymentsMe)

**Where.** `AuthAndCorsExtensions.cs:96–182`.

**What.** Intentional per W1-LP-137. Still a scope hole relative to “machine keys are the only M2M.” Not a steal of another tenant.

