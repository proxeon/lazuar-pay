---
number: "108"
id: B06-D24
severity: P1
status: resolved
resolved_branch: fix/108-cons-banner-valid
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 108 — B06-D24 — B2C consolidation idempotency is dead in workers; banner dies after VALID

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/108-cons-banner-valid`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D24 — B2C consolidation idempotency is dead in workers; banner dies after VALID (P1)

**Status:** open. 008 §6.5 / §7.5 still true, with a sharper root cause.

`alreadyConsolidated`:

```209:211:apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs
        var alreadyConsolidated = await db.LedgerEntries.AnyAsync(e =>
            e.OrganizationId == orgId
            && e.TaxInvoiceId == consolidationRef, ct);
```

**No `IgnoreQueryFilters`.** Platform filter: `OrganizationId == ExecutionContext.TenantId` (`PlatformDbContext.cs:43–46`). Comment on that filter: *“empty ambient TenantId matches no rows (workers must IgnoreQueryFilters + explicit org).”* The same file’s pending-row queries **do** `IgnoreQueryFilters` (`107`, `152`). The safety-net query does not. In a worker with empty TenantId, `alreadyConsolidated` is **always false**.

The live defense is only `ConsolidationStatus == Consolidated` excluding rows from the select. That holds on the happy path. It does **not** hold if:

- publish succeeds and `SaveChanges` fails (event already out; rows still Pending; next run files again);
- any new Pending row appears for a previously consolidated month.

`ConsolidatedInvoiceIssuedIntegrationEventHandler` uses `idempotencyKey = Guid.CreateVersion7().ToString()` (`57`). Submit-level dedup on `Internal_id` does not exist. Two type `01` for `B2C-CONS-{yyyyMM}-{org}` is possible.

After VALID, `UpdateLhdnStatus` writes the UUID into `TaxInvoiceId` (`LedgerEntry.cs:142–147`). Ops banner searches `B2C-CONS-` (`TaxInvoicesPage.tsx:51–61`) against `ReferenceId` / `TaxInvoiceId` / `CustomerDocumentNumber` (`BillingQueryService.cs:51–53`). After VALID those are a gateway tx, a UUID, and `RCPT-…`. **The banner goes blank at the moment you would want it.**

`B2cConsolidationJobTests.SecondRun_SamePeriod_IsIdempotent` only double-runs **before** VALID. It never overwrites `TaxInvoiceId`. It does not exercise `alreadyConsolidated` under a worker filter.

