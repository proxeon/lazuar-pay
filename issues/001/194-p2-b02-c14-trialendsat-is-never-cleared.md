---
number: "194"
id: B02-C14
severity: P2
status: resolved
resolved_branch: fix/194-clear-trial-ends-at
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
---

# 194 — B02-C14 — TrialEndsAt is never cleared

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/194-clear-trial-ends-at`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C14 — P2 — TrialEndsAt is never cleared

**Evidence.** `ActivateTrial` sets it. `Activate` / `RecoverFromPayment` / `Resume` / `Cancel` do not. Portal hides via Status == TRIALING; ops/API still return `trial_ends_at` on ACTIVE/PAST_DUE/CANCELED. Clear in `Activate` / `RecoverFromPayment`.

