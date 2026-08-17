---
number: "270"
id: B07-I30
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 270 — B07-I30 — GET members/workspace use 401 for IDOR; audit uses 403

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I30 — P2 — GET members/workspace use 401 for IDOR; audit uses 403

**Where.** `WorkspaceEndpoints.cs:33, 87, 103` vs `:177`.

**What.** Same predicate, two statuses. Clients that treat 401 as “login again” will bounce a VIEWER who typed the wrong GUID into a logout loop.

