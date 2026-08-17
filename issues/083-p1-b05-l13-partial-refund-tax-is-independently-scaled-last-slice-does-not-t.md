---
number: "083"
id: B05-L13
severity: P1
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 083 — B05-L13 — Partial refund tax is independently scaled; last slice does not take remainder

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L13 — P1 — Partial refund tax is independently scaled; last slice does not take remainder

See §4. 4 dp AwayFromZero per attempt. No remaining-tax field. Odd splits leak or overshoot the original `LIABILITY_TAX_PAYABLE`. Tests only cover 50%.

---

