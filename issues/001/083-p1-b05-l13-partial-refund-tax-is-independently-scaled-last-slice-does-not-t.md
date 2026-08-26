---
number: "083"
id: B05-L13
severity: P1
status: resolved
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
resolved_branch: fix/083-refund-tax-remainder
---

# 083 — B05-L13 — Partial refund tax is independently scaled; last slice does not take remainder

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/083-refund-tax-remainder`

Last refund slice takes remaining `LIABILITY_TAX_PAYABLE` so 4 dp rounding cannot leak or overshoot.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L13 — P1 — Partial refund tax is independently scaled; last slice does not take remainder

See §4. 4 dp AwayFromZero per attempt. No remaining-tax field. Odd splits leak or overshoot the original `LIABILITY_TAX_PAYABLE`. Tests only cover 50%.

---

