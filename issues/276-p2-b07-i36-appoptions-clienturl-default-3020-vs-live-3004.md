---
number: "276"
id: B07-I36
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 276 — B07-I36 — AppOptions ClientUrl default 3020 vs live 3004

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I36 — P2 — AppOptions ClientUrl default 3020 vs live 3004

**Where.** `AppOptions.cs:10` vs `appsettings.json:41` vs `OneLinkService.cs:17`.

**What.** Dead default today. Future bind-to-options foot-gun for reset/verify (already 404 on 3004).

