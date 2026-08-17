---
number: "121"
id: B07-I13
severity: P1
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 121 — B07-I13 — Login is unauthenticated and unlimited

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I13 — P1 — Login is unauthenticated and unlimited

**Where.** `AuthEndpoints.cs:75–101`; `PublicRegisterRateLimiter` is register-only.

**What.** Online brute force. 400-with-401 (`:88–90`) is a client-contract lie on top. Forgot/resend unlimited; reset is an oracle (B07-I17). Register limiter key can be spoofed (B07-I24). Empty limiter key **allows** (`PublicRegisterRateLimiter.cs:21–24`).

