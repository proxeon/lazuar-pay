---
number: "200"
id: B02-C21
severity: P2
status: open
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
---

# 200 — B02-C21 — Activate-from-arrears no-op dates is a footgun

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C21 — P2 — Activate-from-arrears no-op dates is a footgun

**Evidence.** `Subscription.cs` 94–99. Webhook and record-payment do **not** use Activate for PAST_DUE. A future caller who “just Activate” after pay leaves the due date in the past → re-claim → second charge. `Activate_FromPastDue_DoesNotAdvanceBillingDates` documents the trap. Fix: throw from PAST_DUE/SUSPENDED so RecoverFromPayment is the only door.

