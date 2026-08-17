---
number: "025"
id: B10-X01
severity: P0
status: resolved
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
resolved_branch: fix/002-billing-batch-starve
resolved_by: "002"
---

# 025 — B10-X01 — Billing auto-debit claim starve (sibling of the pause bug 911d358 closed)

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/002-billing-batch-starve` (same change as `002`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X01 — P0 — Billing auto-debit claim starve (sibling of the pause bug 911d358 closed)

**File:** `Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` 253–286.

After a due vaulted subscription is claimed, the job writes attempt 1 and publishes `ExecuteOffSessionChargeIntegrationEvent`, then `return`s. It does **not** `failedIds.Add(sub.Id)`. `NextBillingDate` is still in the past. Status is still `ACTIVE` / `TRIALING`.

Next of the 50 iterations: new transaction, `FOR UPDATE SKIP LOCKED`, `ORDER BY "NextBillingDate"`. The same row is first. `attemptCount == 0` is now false, so it does not double-charge. It **does** consume the slot. One waiting-on-gateway subscription can burn the rest of the hourly batch the same way a paused row did before 911d358.

Dunning does **not** have this hole: it always `processedIds.Add(sub.Id)` after a successful process.

The existing pause tests do **not** cover this path. `RunOnce_CollectionPausedDue_SiblingStillProcessed` only asserts the paused sibling. There is no test “two due vaulted subs, first already has attempt 1, second still charges in the same `RunOnce`.”

008 §3.1 already named this as “milder form of the same claim loop.” 911d358 fixed pause SQL + `failedIds` on the pause skip. It did not add the dispatched-charge id to `failedIds`. The milder form is still live.

Cite only: this is an isolation/concurrency/ops bug, not 02’s product claim-logic.

