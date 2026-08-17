---
number: "047"
id: B02-C12
severity: P1
status: open
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
---

# 047 — B02-C12 — Trial convert can stall in TRIALING after attempt 1 (webhook-dependent, job will not retry)

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C12 — P1 — Trial convert can stall in TRIALING after attempt 1 (webhook-dependent, job will not retry)

**Evidence.** Job leaves TRIALING on dispatch (248–286). Failed handler is the only job-adjacent path to PAST_DUE. Completed handler is the only path to ACTIVE from TRIALING. If neither event arrives, claim keeps picking a TRIALING+due+attempt1 row (B02-C01) and dunning will not, because status is not PAST_DUE.

**Repro.** TRIALING due, vaulted. RunOnce (attempt 1 published). Drop the payments inbox. Wait. Status TRIALING, `NextBillingDate` yesterday, one PENDING attempt, no further charges, no mint, no dunning.

**Blast radius.** Any trial whose off-session never completes. Combined with C01, it also blocks other dues.

**Tests.** `RunOnce_TrialNotDue_DoesNotCharge` only. No due-trial test at all.

**Fix direction.** After attempt 1 is already present and still TRIALING/ACTIVE past a grace, mark PAST_DUE (or re-publish). Add `RunOnce_TrialDueVaulted_PublishesAttempt1_StaysTrialing` and a convert test on the webhook. C01’s processedIds at least stops the starve.

---

