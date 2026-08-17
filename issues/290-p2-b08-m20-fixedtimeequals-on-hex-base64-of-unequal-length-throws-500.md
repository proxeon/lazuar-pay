---
number: "290"
id: B08-M20
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 290 — B08-M20 — `FixedTimeEquals` on hex/base64 of unequal length throws 500

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M20 — P2 — `FixedTimeEquals` on hex/base64 of unequal length throws 500

**Where:** unsubscribe 51–52; webhook 131–133.

**What:** `CryptographicOperations.FixedTimeEquals` requires equal lengths. A 1-character `sig` or a truncated `v1=` is an unhandled exception, not `400 Invalid unsubscribe link`.

---

