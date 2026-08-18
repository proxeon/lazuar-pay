---
number: "196"
id: B02-C17
severity: P2
status: resolved
resolved_branch: fix/196-resume-period-end
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
---

# 196 — B02-C17 — Resume() does not set CurrentPeriodEnd

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/196-resume-period-end`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C17 — P2 — Resume() does not set CurrentPeriodEnd

**Evidence.** `Subscription.Resume` (300–308) vs `RecoverFromPayment` (315–325). Webhook uses Resume for SUSPENDED; clerk uses RecoverFromPayment. Use RecoverFromPayment for both.

