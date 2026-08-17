---
number: "233"
id: B05-L29
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 233 — B05-L29 — Manual enrollment is 100% cash, 0 tax, 0 fee

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L29 — P2 — Manual enrollment is 100% cash, 0 tax, 0 fee

`ManualSubscriberEnrolledIntegrationEventHandler:51-52`. Offline money is booked as if it hit the bank at 100% with no SST split. B2B still requests a type-01 with `TaxAmount: 0m`. Tests lock save-before-PDF and per-log-id idempotency, not tax.

---

