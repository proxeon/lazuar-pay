---
number: "201"
id: B02-C22
severity: P2
status: resolved
resolved_branch: fix/201-applypending-same-product
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
---

# 201 — B02-C22 — ApplyPendingPlanChange returns true even when pending == current ProductId

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/201-applypending-same-product`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C22 — P2 — ApplyPendingPlanChange returns true even when pending == current ProductId

**Evidence.** `SchedulePlanChange` clears when ids match; `ApplyPendingPlanChange` does not. A SQL-stuck `PendingProductId = ProductId` re-snapshots from catalog (cousin of C04). Domain Schedule will not write that row. Speculation on how it appears.

