---
number: "088"
id: B05-L18
severity: P1
status: resolved
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
resolved_branch: fix/088-clawback-granted-credits
---

# 088 — B05-L18 — Utility clawback uses dispute amount vs pack table, not credits granted

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/088-clawback-granted-credits`

Clawback uses credits granted on the original top-up (description, else pack for the original paid amount). Missing original skips claw.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L18 — P1 — Utility clawback uses dispute amount vs pack table, not credits granted

Same `FirstOrDefault` pack function as grant, keyed on `AmountDisputed`. Partial dispute → 0 claw + full ledger reverse (if original exists). Oversize dispute → wrong (larger) pack. Missing original top-up → warning, skip journal, credits may still have been clawed (`ChargebackClawbackHandler:132-137`).

---

