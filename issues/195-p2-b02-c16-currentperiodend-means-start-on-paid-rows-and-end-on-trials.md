---
number: "195"
id: B02-C16
severity: P2
status: open
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
---

# 195 — B02-C16 — CurrentPeriodEnd means start on paid rows and end on trials

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C16 — P2 — CurrentPeriodEnd means start on paid rows and end on trials

**Evidence.** `SubscriptionActivation.Start` passes `instant` as `currentPeriodEnd`. Trial sets both to endsAt. Portal/webhooks advertise `NextBillingDate` as `current_period_end`. Write `CurrentPeriodEnd = next` on paid activate, or stop selecting the column.

