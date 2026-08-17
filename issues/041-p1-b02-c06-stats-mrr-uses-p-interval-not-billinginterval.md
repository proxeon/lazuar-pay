---
number: "041"
id: B02-C06
severity: P1
status: open
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
---

# 041 — B02-C06 — Stats MRR uses p.Interval, not BillingInterval

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C06 — P1 — Stats MRR uses p.Interval, not BillingInterval

**Evidence.** `CommerceQueryService.Stats.cs` 33–54. Helper is correct; the argument is not.

**Repro.** One ACTIVE yearly seat, `UnitAmount=1200`, product default `mo`. Dashboard MRR = 1200, ARR = 14400. Honest monthly equivalent is 100 / 1200.

**Blast radius.** Every mixed-interval catalog. LP-161 “honest snapshot MRR” is false for interval. Unit snapshot is used (good) until B02-C04 overwrites it.

**Tests.** `CommerceMrrTests` never open Stats.cs.

**Fix direction.** `COALESCE(s."BillingInterval", p."Interval") as Interval`.

---

