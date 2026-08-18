---
number: "242"
id: B05-L38
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 242 — B05-L38 — `TaxInvoiceId` is still the dual-use dumping ground

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L38 — P2 — `TaxInvoiceId` is still the dual-use dumping ground

UUID overwrite after validate. Consolidation ref overwrite after batch. `CustomerDocumentNumber` is the real commercial number. Lookup still searches `TaxInvoiceId`. `FirstOrDefault` on multiple matches has no type preference (`LedgerLhdnLookup`). A cancel whose internal id collides with a UUID-shaped `TaxInvoiceId` on the wrong row is theoretical.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
`TaxInvoiceId` was the single string used as receipt number, MyInvois UUID, and B2C consolidation batch ref. New columns exist (`CustomerDocumentNumber`, `LhdnDocumentUuid`, `LhdnValidationStatus`, `ConsolidationStatus`) and writers prefer them, but `TaxInvoiceId` is still written and still searched. After validate, the UUID used to overwrite `TaxInvoiceId`; that path now writes `LhdnDocumentUuid` only. After a B2C batch, `MarkConsolidatedPending` still **assigns** `TaxInvoiceId = consolidationRef`, so every receipt in the month shares the same `B2C-CONS-…` on that column. `LedgerLhdnLookup` matches `CustomerDocumentNumber` OR `TaxInvoiceId` OR `ReferenceId` and returns the list; it has no type preference. `LhdnDocumentCancelledIntegrationEventHandler` now prefers a `GATEWAY_PAYMENT` match (007) before `FirstOrDefault()`. A cancel whose internal id collides with a UUID-shaped `TaxInvoiceId` on the wrong row is still theoretical. Validated-document lookup in `LhdnDocumentValidatedIntegrationEventHandler` also matches `LhdnDocumentUuid` and `TaxInvoiceId`.

### Still present?
**PARTIAL**

UUID-overwrite-after-validate is **fixed** (W2-LP-101 / domain tests):

```140:148:apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs
    public void UpdateLhdnStatus(string? lhdnDocumentUuid, string status)
    {
        if (!string.IsNullOrWhiteSpace(lhdnDocumentUuid))
        {
            LhdnDocumentUuid = lhdnDocumentUuid;
        }

        LhdnValidationStatus = status;
    }
```

`LedgerEntryAndAccountTypesTests.UpdateLhdnStatus_DoesNotOverwriteCustomerDocumentNumber` asserts `TaxInvoiceId != "uuid-from-myinvois"`. Consolidation overwrite is **not** fixed:

```129:135:apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs
    public void MarkConsolidatedPending(string consolidationRef)
    {
        ConsolidationStatus = ConsolidationStatuses.Consolidated;
        LhdnValidationStatus = LhdnValidationStatuses.ConsolidatedPending;
        // Legacy correlation: batch internal ref still stored on TaxInvoiceId for LHDN linkage.
        TaxInvoiceId = consolidationRef;
    }
```

Lookup still searches the dumping ground and does not prefer type:

```16:22:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/LedgerLhdnLookup.cs
        entries
            .IgnoreQueryFilters()
            .Where(e => e.OrganizationId == organizationId
                && (e.CustomerDocumentNumber == internalId
                    || e.TaxInvoiceId == internalId
                    || e.ReferenceId == internalId))
            .ToListAsync();
```

Cancel caller now prefers payment (`LhdnDocumentCancelledIntegrationEventHandler.cs:38-39`) — **007**. Assign helpers still `TaxInvoiceId ??=` the commercial number (`LedgerEntry.cs:76, 91, 103`). README §4 calls the column “legacy dual-use … kept for back-compat”.

### Related files
- [`apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs`](apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs) — dual-use comment; assign; consolidation overwrite; UpdateLhdnStatus.
- [`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/LedgerLhdnLookup.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/LedgerLhdnLookup.cs) — OR search, no type rank.
- [`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/LhdnDocumentCancelledIntegrationEventHandler.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/LhdnDocumentCancelledIntegrationEventHandler.cs) — now prefers `GATEWAY_PAYMENT`.
- [`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/LhdnDocumentValidatedIntegrationEventHandler.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/LhdnDocumentValidatedIntegrationEventHandler.cs) — still matches `TaxInvoiceId`.
- [`apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs) — writes/reads consolidation via `TaxInvoiceId`.
- [`apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/Domain/LedgerEntryAndAccountTypesTests.cs`](apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/Domain/LedgerEntryAndAccountTypesTests.cs) — UUID no longer lands on `TaxInvoiceId`; cons ref does.
- [`issues/007-p0-b05-l01-full-b2b-refund-72h-double-reverses-cash-and-tax.md`](issues/007-p0-b05-l01-full-b2b-refund-72h-double-reverses-cash-and-tax.md) / [`issues/107-p1-b06-d22-original-document-resolution-can-walk-the-wrong-key-cancel-refun.md`](issues/107-p1-b06-d22-original-document-resolution-can-walk-the-wrong-key-cancel-refun.md) — resolved lookup/double-row work.

### Tests
- Existing: `UpdateLhdnStatus_DoesNotOverwriteCustomerDocumentNumber`; `UpdateLhdnStatus_KeepsConsolidationTaxInvoiceId`; `MarkConsolidatedPending_KeepsCustomerDocument_SetsConsolidated` (locks `TaxInvoiceId == B2C-CONS-…`); `DocumentSeriesTests.CustomerFacingNumber_NeverUsesRawUuid`; `LhdnDocumentCancelledIntegrationEventHandlerTests` (007 skip-if-refunded).
- Tests **pass while consolidation still dumps into `TaxInvoiceId`**. `UpdateLhdnStatus_DoesNotOverwrite…` would fail if UUID overwrite returned. No test that `LedgerLhdnLookup` prefers `GATEWAY_PAYMENT` (the cancel handler test covers the caller).
- First remaining regression: lookup of `B2C-CONS-…` must return the consolidation header (or all children **on purpose**), and a cancel of `INV-…` must never pick a refund/CN row even if `TaxInvoiceId` collided. Do not remove the cons-ref write without updating `B2cConsolidationJob` readers.

### Reproduction today
Pay a B2C sale → `TaxInvoiceId` starts as `RCPT-…`. VALID → `LhdnDocumentUuid` set, `TaxInvoiceId` still `RCPT-…` (fixed). Run monthly consolidation → that receipt’s `TaxInvoiceId` becomes `B2C-CONS-yyyyMM-…` while `CustomerDocumentNumber` stays `RCPT-…`. `GET /admin/billing/ledger` search on the cons ref hits every receipt in the batch (`BillingQueryService` searches `TaxInvoiceId`). Cancel by cons ref: lookup returns many rows; cancel handler now takes the first `GATEWAY_PAYMENT` if any.

### Blast radius
Ops search / LHDN correlation, not a live double-reverse after **007**. Theoretical wrong-row cancel if `internalId` equals another row’s `TaxInvoiceId` and no `GATEWAY_PAYMENT` is in the match set. Consolidation still *wants* many receipts to share the cons ref. Customer-facing numbers are safe (`CustomerDocumentNumber` + `CustomerFacingNumber`). Still P2 residue.

### Suggested fix
Stop treating `TaxInvoiceId` as a write target in new code. Keep reading it for old rows. Consolidation should correlate via `ConsolidationStatus` + a dedicated batch id (or `ReferenceId` on the cons header) instead of overwriting `TaxInvoiceId`; that is a B2C job change, not a drive-by. Add `LhdnDocumentUuid` to `LedgerLhdnLookup` (validate handler already has it). Keep the cancel handler’s `GATEWAY_PAYMENT` preference. Do not TypeSpec-regen. Do not invent a second commercial number.

### Evaluation notes
009 “UUID overwrite after validate” is **false** now. “Consolidation ref overwrite” is **true**. “FirstOrDefault no type preference” is **false** on the cancel caller, **true** on the helper. Residual after **007 / 107** fail-closed money work. Still P2. Do not reopen L01. Pair with README §4 (already more honest than 009).

