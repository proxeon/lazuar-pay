---
number: "209"
id: B03-C21
severity: P2
status: resolved
resolved_branch: fix/209-pending-charge-timeout
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 209 — B03-C21 — PENDING ChargeAttempt never times out

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/209-pending-charge-timeout`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C21 — P2 — PENDING ChargeAttempt never times out

`hasInFlightOrSettled` defers AUTO_CHARGE forever while a row is PENDING (`PastDue_PendingAttempt_DoesNotPublish_DoesNotConsumeOffset`). Lost webhook = no further card retries; EMAIL steps and terminal still run. Conservative, but a stuck PENDING is silent.

---

