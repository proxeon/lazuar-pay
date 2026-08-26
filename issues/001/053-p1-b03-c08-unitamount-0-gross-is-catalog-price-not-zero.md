---
number: "053"
id: B03-C08
severity: P1
status: resolved
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
resolved_branch: fix/053-zero-unit-gross
---

# 053 — B03-C08 — `UnitAmount == 0` Gross is catalog `Price`, not zero

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/053-zero-unit-gross`

Arrears / AUTO_CHARGE Gross uses `HasUnitSnapshot`. A written 0 stays 0.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C08 — P1 — `UnitAmount == 0` Gross is catalog `Price`, not zero

**Evidence.** `SubscriptionBillingAmount.Unit` (`> 0` else `product.Price`). Seats `Max(1, Quantity)`. Used by AUTO_CHARGE, dunning email, arrears Gross, billing (02).

**Repro.** Sub with `UnitAmount = 0`, catalog 100, qty 3, SST 8%, merchant registered → arrears / AUTO_CHARGE 324, not 0.

**Blast.** Coupon / $0 snapshot rows over-collect on recovery. Combined with B03-C06 this is a surprise first auto-debit.

**Tests.** `SubscriptionBillingAmountTests` only use `unitAmount: 100`. Add `UnitAmount=0` → decide product (0 vs catalog) and pin it.

**Fix direction.** Treat `UnitAmount` as the source of truth including zero; only fall back to catalog when the snapshot was never written (`Activate` without unit).

---

