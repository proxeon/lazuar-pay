---
number: "266"
id: B07-I24
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 266 — B07-I24 — Register rate-limit key trusts first `X-Forwarded-For` hop; empty key allows

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I24 — P2 — Register rate-limit key trusts first `X-Forwarded-For` hop; empty key allows

**Where.** `AuthEndpoints.cs:169–183`; `PublicRegisterRateLimiter.cs:21–24`.

**What.** Spoof a new IP → new bucket. Empty key → allow. In-process `ConcurrentDictionary`; multi-instance resets. Hygiene, not a WAF. Tests only cover 11th acquire on one key (`PublicRegisterRateLimiterTests.cs:10–21`).

