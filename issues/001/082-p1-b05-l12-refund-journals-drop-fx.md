---
number: "082"
id: B05-L12
severity: P1
status: resolved
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
resolved_branch: fix/082-refund-fx
---

# 082 — B05-L12 — Refund journals drop FX

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/082-refund-fx`

Refund journals copy FX rate and base currency from the event or the original sale.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L12 — P1 — Refund journals drop FX

Sale: `BaseCurrencyAmount = amount * fxRate`, `BaseCurrency = event.BaseCurrency`.  
Refund event: no `FxRate`, no `BaseCurrency`.  
Refund lines: `Amount = BaseCurrencyAmount = cashOutflow`, `BaseCurrency = event.Currency`.

A USD sale booked at `fxRate = 4.7` into MYR is reversed as if USD **were** MYR. `ValidateBalanced` still passes. `GetFinancialSummaryAsync` hardcodes display currency `'MYR'` (`BillingQueryService.cs:151`) and sums `BaseCurrencyAmount`. The refund’s “base” is the wrong currency. Net MYR after a full USD refund is garbage.

No test uses `FxRate != 1` on a refund. Matrix tests are all `MYR` / `1`.

---

