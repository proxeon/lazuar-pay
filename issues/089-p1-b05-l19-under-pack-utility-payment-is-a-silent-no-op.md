---
number: "089"
id: B05-L19
severity: P1
status: resolved
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
resolved_branch: fix/089-underpack-unmatched-cash
---

# 089 — B05-L19 — Under-pack utility payment is a silent no-op

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/089-underpack-unmatched-cash`

A utility payment below the cheapest pack books `SYSTEM_CREDIT_TOPUP` unmatched cash and does not grant credits.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L19 — P1 — Under-pack utility payment is a silent no-op

`PlatformTopUpEventHandler:53` `if (credits > 0)`. RM 49 against min pack 50: no wallet, no ledger, no error. System checkout collected money. `HandleAsync_Skips_When_GatewayTransactionId_Empty` and the already-processed test exist; there is **no** test that 49 MYR is either rejected or booked as unmatched cash.

---

