---
number: "261"
id: B07-I17
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 261 — B07-I17 — Reset-password is an email oracle

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I17 — P2 — Reset-password is an email oracle

**Where.** `ResetPasswordCommand.cs:25–33`. Missing user: `"Invalid request."` Bad token on a real user: `"Token is invalid or expired."`

**What.** Forgot is silent. Reset is not. Pair with B07-I02 (the link 404s) and you have an API that enumerates emails and a product that cannot complete the flow.

