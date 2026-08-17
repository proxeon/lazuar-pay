---
number: "077"
id: B05-L07
severity: P1
status: resolved
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
resolved_branch: fix/077-refund-not-b2c-consolidation
---

# 077 — B05-L07 — `GATEWAY_REFUND` rows are B2C/null consolidation and enter `B2cConsolidationJob`

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/077-refund-not-b2c-consolidation`

Refund writer marks consolidation not required. B2cConsolidationJob excludes GATEWAY_REFUND rows.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L07 — P1 — `GATEWAY_REFUND` rows are B2C/null consolidation and enter `B2cConsolidationJob`

**Where.** Refund writer never calls `MarkConsolidationNotRequired`. Constructor default `CustomerType = "B2C"`. Both statuses null.

`B2cConsolidationJob` selects B2C where `ConsolidationStatus == PENDING` **or** (`ConsolidationStatus == null` and (`LhdnValidationStatus == B2C_RECEIPT` **or** `null`)) (`:157-160`, same predicate at `:111-114`).

A refund header **matches**.

Same-month: the job nets `REVENUE_GROSS − CONTRA_REVENUE_REFUNDS` (`:269-274`). Almost a feature.

Cross-month: the sale month already filed. The refund month computes `grossRevenue = 0 − contra` which is negative, fails `if (grossRevenue > 0)` (`:280`), and if no positive groups remain, **every** row in that month’s batch is `MarkConsolidationIgnored` (`:300-306`). There is no type-02 CN from consolidation. B2C refunds after month-end do not legally reverse the filed batch. If the refund month also has real sales, those sales still consolidate; the refund is simply omitted from the total (filed batch stays high).

W2-LP-101 allocated CN numbers. It did not teach the refund writer to mark consolidation, and it did not teach the job to exclude `GATEWAY_REFUND`.

`B2cConsolidationJobTests` never seeds a refund row.

---

