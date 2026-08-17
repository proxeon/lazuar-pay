---
number: "002"
id: B02-C01
severity: P0
status: resolved
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
resolved_branch: fix/002-billing-batch-starve
---

# 002 — B02-C01 — Vaulted due row starves the 50-slot batch (failedIds / processedIds hole)

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/002-billing-batch-starve`
- **Also closes:** `025` (B10-X01, same starve)

After a successful ProcessOne (including off-session dispatch / attempt-1 no-op) the id is added to `processedIds` and excluded from the next claim. `NextBillingDate` is not rolled.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C01 — P0 — Vaulted due row starves the 50-slot batch (failedIds / processedIds hole)

**Evidence.** `ProcessBillingAsync` only excludes `failedIds`. Off-session ProcessOne `return`s after dispatch or after “already has attempt 1” **without** `failedIds.Add`. `NextBillingDate` is intentionally not advanced. Claim predicate still matches. Next slot reclaimes the same `ORDER BY NextBillingDate` row. Dunning’s sibling worker adds `processedIds` after every successful process; Billing does not.

Quote: BillingEngineJob.cs 70, 248–286 (the `return` with no `failedIds`), 129–142 (claim does not exclude attempt-1 rows), DunningEngineJob.Claim.cs 43–77 (the pattern they already know).

**Repro.**

1. Insert two ACTIVE Stripe vaulted subs, both `NextBillingDate` yesterday, A earlier than B.
2. `RunOnceAsync`.
3. Observe one `ExecuteOffSessionChargeIntegrationEvent` (A) and zero for B.
4. `RunOnceAsync` again. Observe still no event for B (A still due, attemptCount=1, occupies the claim).
5. Optional: 50 vaulted dues + 1 reminder due. The reminder is never minted in that hour.

Existing tests do **not** fail: `RunOnce_StripeVaulted_PublishesOffSessionAttempt1_DoesNotAdvanceDates` is one row; `RunOnce_VaultedAlreadyHasAttempt1_DoesNotPublishAgain` is one row; `RunOnce_FiftyPausedDue_DoesNotBlockOneSibling` is the pause predicate, not this path.

**Blast radius.** Every Stripe/CHIP auto-renew in the same hour after the first dispatch. One worker ≈ one off-session per interval. Reminder-only siblings behind a vaulted due also wait. Trials that dispatched attempt 1 and hang sit on the same queue.

**Tests that should exist and do not.** Two vaulted dues in one `RunOnce` → two events. Vaulted A with attempt 1 + due sibling B → B still dispatched. Same shape as the pause tests they added in `911d358`.

**Fix direction.** Mirror dunning: `processedIds.Add(sub.Id)` after every successful ProcessOne (including no-op attempt-1 and successful dispatch). Or add `failedIds.Add` on both off-session returns. Or exclude rows that already have a ChargeAttemptLog for `NextBillingDate::date` in the claim SQL. Do **not** roll `NextBillingDate` on dispatch to “fix” this; that re-opens double-charge races the unique attempt log is there to prevent.

---

