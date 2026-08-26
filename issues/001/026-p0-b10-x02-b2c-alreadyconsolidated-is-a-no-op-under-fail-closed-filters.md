---
number: "026"
id: B10-X02
severity: P0
status: resolved
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
resolved_branch: fix/026-b2c-already-consolidated-ignore-filters
---

# 026 — B10-X02 — B2C `alreadyConsolidated` is a no-op under fail-closed filters

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/026-b2c-already-consolidated-ignore-filters`

`alreadyConsolidated` now uses `IgnoreQueryFilters()` so an empty worker tenant still sees the issued consolidation ref.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X02 — P0 — B2C `alreadyConsolidated` is a no-op under fail-closed filters

**File:** `Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs` 209–219.

```209:219:apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs
        var alreadyConsolidated = await db.LedgerEntries.AnyAsync(e =>
            e.OrganizationId == orgId
            && e.TaxInvoiceId == consolidationRef, ct);

        if (alreadyConsolidated)
        {
            _logger.LogInformation(
                "Skipping B2C consolidation for Org {OrgId} period {Period} — already issued ({Ref}).",
                orgId, periodKey, consolidationRef);
            return;
        }
```

`LedgerEntry` is `IMustHaveTenant`. The worker’s ambient tenant is empty. `AnyAsync` without `IgnoreQueryFilters()` is **always false**. The skip never fires in a real hosted process.

Re-entry protection is the earlier pending-status query (that one **does** ignore filters). A crash **after** `PublishAsync` (outbox insert) and **before** `MarkConsolidatedPending` + `SaveChanges` can double-publish `ConsolidatedInvoiceIssuedIntegrationEvent`. LHDN must be idempotent on `B2C-CONS-{yyyyMM}-{org}`. That is an assumed consumer property, not a producer lock.

`B2cConsolidationJobTests` (7, InMemory) never assert the `alreadyConsolidated` branch under empty ambient + fail-closed filters. InMemory + empty tenant also hides rows, so the test job’s `IgnoreQueryFilters` pending query is what they exercise. The broken `AnyAsync` is untested.

008 §3.4 filed this. Still present.

