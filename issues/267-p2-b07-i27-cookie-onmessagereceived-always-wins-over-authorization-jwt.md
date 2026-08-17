---
number: "267"
id: B07-I27
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 267 — B07-I27 — Cookie `OnMessageReceived` always wins over Authorization JWT

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I27 — P2 — Cookie `OnMessageReceived` always wins over Authorization JWT

**Where.** `AuthAndCorsExtensions.cs:52–64`.

**What.** Documented dual-realm. Integrators debugging with Bearer + a leftover ops cookie will not see their Bearer identity. Not a steal.

