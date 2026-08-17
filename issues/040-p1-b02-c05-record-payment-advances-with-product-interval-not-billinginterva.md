---
number: "040"
id: B02-C05
severity: P1
status: resolved
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
resolved_branch: fix/040-record-payment-billing-interval
---

# 040 — B02-C05 — Record-payment advances with product.Interval, not BillingInterval

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/040-record-payment-billing-interval`

Clerk record-payment advances with `ResolveInterval(sub, product)`. Clerk override date is unchanged.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C05 — P1 — Record-payment advances with product.Interval, not BillingInterval

**Evidence.** `RecordSubscriberPaymentCommandHandler.cs` 88–90 vs resume handler 131–133 which **does** call `ResolveInterval`.

**Repro.** Yearly sub, clerk logs a payment with no override date. `NextBillingDate` becomes now+1 month. Next billing job fires in a month and charges the yearly Gross.

**Blast radius.** Ops “Log Payment” on any non-default-interval seat. Tests `R1_ActivePaid_AdvancesFromNow` use monthly and would stay green.

**Fix direction.** `AdvanceFrom(periodEnd, SubscriptionBillingAmount.ResolveInterval(sub, product))`. Keep the clerk override.

---

