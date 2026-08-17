---
number: "282"
id: B08-M12
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 282 — B08-M12 — Unique `(Email, Phone)` vs resolve-by-email

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M12 — P2 — Unique `(Email, Phone)` vs resolve-by-email

**Where:** `ClientProfileConfiguration.cs` 15; `ResolveClientProfileCommandHandler.cs` 26–28.

**What:** Two rows with the same email and different phones can exist (Create path, or a future writer). Resolve picks one without `OrderBy`. Concurrent first inserts of `(org, email, "")` race the unique index and 500.

---

