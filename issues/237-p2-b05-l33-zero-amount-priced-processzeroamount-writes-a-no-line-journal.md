---
number: "237"
id: B05-L33
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 237 — B05-L33 — `$0`-priced `ProcessZeroAmount` writes a no-line journal

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L33 — P2 — `$0`-priced `ProcessZeroAmount` writes a no-line journal

`OriginalAmount = 0` → skip `AddLine` → `ValidateBalanced` on empty → header with `ZERO_AMOUNT_CHECKOUT` and no lines. Harmless noise. No test in Billing.

---

