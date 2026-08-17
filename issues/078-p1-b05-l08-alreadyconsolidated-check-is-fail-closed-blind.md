---
number: "078"
id: B05-L08
severity: P1
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 078 — B05-L08 — `alreadyConsolidated` check is fail-closed-blind

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L08 — P1 — `alreadyConsolidated` check is fail-closed-blind

**Where.** `B2cConsolidationJob.ProcessOrgPeriodAsync:209-211`:

```csharp
        var alreadyConsolidated = await db.LedgerEntries.AnyAsync(e =>
            e.OrganizationId == orgId
            && e.TaxInvoiceId == consolidationRef, ct);
```

No `IgnoreQueryFilters()`. Workers run with empty ambient `TenantId`. `PlatformDbContext` filter is `OrganizationId == TenantId`. `AnyAsync` is **always false** in production.

The job still “works” for the happy path because already-`CONSOLIDATED` rows fail the **select** predicate (which **does** `IgnoreQueryFilters`). The `alreadyConsolidated` short-circuit is dead.

If leftover `PENDING` / null-status rows appear later in a period that already issued `B2C-CONS-{yyyyMM}-{org}` (B05-L07 refunds; late backfill), the job will publish a **second** `ConsolidatedInvoiceIssuedIntegrationEvent` with the **same** `InternalReferenceId`. Lhdn idempotency on that key is the only thing between us and a second type-01. That is slice 06’s problem if it happens; the Billing job is the one that emits the duplicate.

`SecondRun_SamePeriod_IsIdempotent` passes because the first run flipped status, not because `alreadyConsolidated` worked. The test uses empty-tenant InMemory, so the check is false there too.

---

