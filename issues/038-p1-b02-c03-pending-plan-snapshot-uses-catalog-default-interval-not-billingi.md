---
number: "038"
id: B02-C03
severity: P1
status: resolved
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
resolved_branch: fix/038-pending-plan-billing-interval
---

# 038 — B02-C03 — Pending plan snapshot uses catalog default interval, not BillingInterval

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/038-pending-plan-billing-interval`

Plan-change guard, preview, and billing snapshot use the subscription BillingInterval price row, not the catalog default.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C03 — P1 — Pending plan snapshot uses catalog default interval, not BillingInterval

**Evidence.** `PlanChangePolicy.GuardTargetProduct` compares `target.Interval` to `current.Interval` (catalog defaults). Preview `nextUnit = targetProduct.Price`. Job: `Prices.FirstOrDefault(p => p.Interval == product.Interval) ?? DefaultPrice()`. `SubscriptionActivation` and hop-1 (out of slice, but it writes `BillingInterval`) can put `BillingInterval = "yr"` on a product whose default is `"mo"`.

**Repro.** Product Basic default `mo` RM 50, yearly price row RM 500. Sub ACTIVE, `BillingInterval=yr`, `UnitAmount=500`, Quantity=1. Schedule change to Pro, also default `mo` RM 80 with yearly RM 800. Due tick. Snapshot becomes 80, `BillingInterval` becomes `mo`. Off-session amount 80 (or 86.40 with SST), not 800.

**Blast radius.** Every yearly (or non-default) seat that uses change-plan. Ops picker lists `p.interval` / `p.price` (the default). Merchants with both prices on one product are the Wave 3 shape.

**Tests.** `PlanChangePolicyTests` and `RunOnce_AppliesPendingProductThenChargesNewPrice` are monthly-only.

**Fix direction.** Guard and snapshot via `ResolveInterval(sub, current)` / `target.GetPrice(interval)`. Preview `NextAmount` from that price × seats. Refuse the change if the target has no row for the subscription’s interval (same message as interval swap).

---

