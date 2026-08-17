---
number: "232"
id: B05-L28
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 232 — B05-L28 — `RefundedFee` is always 0

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L28 — P2 — `RefundedFee` is always 0

Mark-refunded hard-codes 0. Payments adapter success hard-codes 0 (“adapters currently do not return reclaimed fee”). Billing never reverses `EXPENSE_GATEWAY_FEE`. Matrix asserts −3 after a full refund. Fine if labelled. Not fine if we sell “exact gateway fees”.

---

