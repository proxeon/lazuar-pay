---
number: "324"
id: B09-U56
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 324 — B09-U56 — AppOptions default ClientUrl 3020

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U56 — AppOptions default ClientUrl 3020 (P2, FE-adjacent)

`AppOptions.cs` 8–10. Comment says portal is “typically port 3020.” Portal is 3004. Sample app is 3020.

