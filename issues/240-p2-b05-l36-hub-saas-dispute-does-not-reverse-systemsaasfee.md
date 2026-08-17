---
number: "240"
id: B05-L36
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 240 — B05-L36 — Hub SaaS dispute does not reverse `SYSTEM_SAAS_FEE`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L36 — P2 — Hub SaaS dispute does not reverse `SYSTEM_SAAS_FEE`

`PAST_DUE` only. Expense/cash stay. A later win has nothing to unwind because nothing was reversed. A later loss that Stripe refunds is B05-L15. Period dates still grant access time.

---

