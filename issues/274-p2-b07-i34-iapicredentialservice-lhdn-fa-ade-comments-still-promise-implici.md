---
number: "274"
id: B07-I34
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 274 — B07-I34 — IApiCredentialService + LHDN façade comments still promise implicit LHDN defaults

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I34 — P2 — IApiCredentialService + LHDN façade comments still promise implicit LHDN defaults

**Where.** `IApiCredentialService.cs:32–34`; `AdminApiKeyEndpoints.cs:51`; `Lhdn/Domain/ApiKeyScopes.cs:14–17`.

**What.** Command rejects omit (`GenerateApiCredentialCommand.cs:57`; tests). Comments are a lying interface. High odds of a “compat” “fix” that re-opens the default.

