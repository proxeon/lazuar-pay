---
number: "175"
id: B10-X19
severity: P1
status: resolved
resolved_branch: fix/175-migrate-pending-model
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 175 — B10-X19 — Boot `MigrateAsync` continues on `PendingModelChanges`

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/175-migrate-pending-model`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X19 — P1 — Boot `MigrateAsync` continues on `PendingModelChanges`

```53:59:apps/lazuar-api/src/Lazuar.Api/Composition/DatabaseMigrationExtensions.cs
            catch (InvalidOperationException ex) when (ex.Message.Contains("PendingModelChanges", StringComparison.Ordinal))
            {
                migratorLog.LogError(ex,
                    "MigrateAsync blocked for {DbContext} by pending model changes. Module tables may be missing.", name);
            }
```

The process comes up. Workers then throw every hour on missing columns. Billing DI also `ConfigureWarnings(Ignore PendingModelChangesWarning)`, so a forgotten Billing migration is even less visible.

XML-doc on the same type admits multi-instance `MigrateAsync` races. Wave 3 commerce migrations include data backfills (`UPDATE` UnitAmount, `INSERT ProductPrices`). Two rolling pods can run those together.

No integration test calls `MigrateAllModuleDatabasesAsync`.

