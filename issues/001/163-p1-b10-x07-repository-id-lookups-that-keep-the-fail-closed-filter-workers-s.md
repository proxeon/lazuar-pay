---
number: "163"
id: B10-X07
severity: P1
status: resolved
resolved_branch: fix/163-repo-id-workers-see-rows
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 163 — B10-X07 — Repository ID lookups that **keep** the fail-closed filter (workers see nothing)

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/163-repo-id-workers-see-rows`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X07 — P1 — Repository ID lookups that **keep** the fail-closed filter (workers see nothing)

`GetCouponByIdAsync`, `GetCheckoutSessionByIdAsync`, `GetOrderByIdAsync` do **not** call `IgnoreQueryFilters()`. Under empty ambient they return null.

HTTP handlers that run with `X-Tenant-Id` are fine (filter matches). Event handlers / workers are not.

Concrete lie: `OrderCompletedIntegrationEventHandler` (quoted in §3.3) emits `quantity: 1` whenever the real order is invisible.

`GetCouponByIdAsync` in `ProcessZeroAmountCheckoutCommand` / `MarkCheckoutAsPaidOfflineCommandHandler` is HTTP-scoped — OK today. If either is ever called from a bus handler, coupon reservation release silently no-ops.

`HasSubscriptionsAssignedToCampaignAsync` and `HasAnyDunningCampaignAsync` also keep the filter. A worker asking “is this campaign in use?” with empty ambient gets `false` and may allow a delete that still has PAST_DUE rows.

