---
number: "271"
id: B07-I31
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 271 — B07-I31 — `ExecutionContextAccessor.UserRole` is the first role claim

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I31 — P2 — `ExecutionContextAccessor.UserRole` is the first role claim

**Where.** `ExecutionContextAccessor.cs:38`.

**What.** After injection the first claim is JWT `CLIENT`, the second is `ADMIN`. Anything that reads `UserRole` thinks the owner is a CLIENT. Policies use `IsInRole` and are fine. New code that switches on `UserRole` will be wrong.

