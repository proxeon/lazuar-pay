---
number: "087"
id: B05-L17
severity: P1
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 087 — B05-L17 — `HasOpenDispute` never clears; `MarkDisputed` overwrites `REFUNDED`

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L17 — P1 — `HasOpenDispute` never clears; `MarkDisputed` overwrites `REFUNDED`

Set-only latch. Refund-then-dispute paints a fully refunded log `DISPUTED` while `RefundedAmount == Amount`. Remaining 0 → `ALREADY_REFUNDED`. Ops list and CSV show `DISPUTED` after the money already went back. Not a second journal. Status lie.

---

