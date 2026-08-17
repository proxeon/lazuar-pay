---
number: "269"
id: B07-I29
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 269 — B07-I29 — `HasTenantAccess` ignores archive and role

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I29 — P2 — `HasTenantAccess` ignores archive and role

**Where.** `OneQueryService.cs:72–78`.

**What.** Any historical membership reads members/invites/audit. VIEWER included (intended). Archived included (not intended if archive means leave).

