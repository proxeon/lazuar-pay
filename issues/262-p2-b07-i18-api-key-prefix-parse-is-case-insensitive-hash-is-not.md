---
number: "262"
id: B07-I18
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 262 — B07-I18 — API key prefix parse is case-insensitive; hash is not

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I18 — P2 — API key prefix parse is case-insensitive; hash is not

**Where.** `ApiKeyAuthenticationMiddleware.cs:35, 158–162` vs `TokenGeneratorService.cs:23–27`.

**What.** `SK_TEST_…` is recognized as a key and as test mode, then 401s. Confusing, not a bypass.

