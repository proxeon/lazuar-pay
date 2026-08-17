---
number: "273"
id: B07-I33
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 273 — B07-I33 — Storage presign is any member; path is tenant-required

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I33 — P2 — Storage presign is any member; path is tenant-required

**Where.** `StorageEndpoints.cs:27–48`; `TenantSecurityMiddleware.cs:160–164`.

**What.** Empty tenant 400s (pre-wave hole closed). VIEWER can still upload. Not OrgAdmin.

