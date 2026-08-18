---
number: "241"
id: B05-L37
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 241 — B05-L37 — Platform invoice fallback can print a Guid slice

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L37 — P2 — Platform invoice fallback can print a Guid slice

`GenerateAndStorePlatformSaasInvoiceCommandHandler:63-65`: `CustomerDocumentNumber ?? TaxInvoiceId ?? entry.Id.ToString()[..8]`. Sequence usually ran first. If sequence returned whitespace, `AssignPlatformDocumentNumber` is skipped (`string.IsNullOrWhiteSpace` check on the refund path; SaaS always assigns if the mediator returns). Low likelihood.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
Hub SaaS PDF numbering does not use `DocumentSeries.CustomerFacingNumber` (which refuses raw UUIDs and falls back to `"PENDING"`). The platform invoice handler prints `CustomerDocumentNumber ?? TaxInvoiceId ?? entry.Id.ToString()[..8].ToUpperInvariant()`. On the happy path `PlatformSaasFeeHandler` allocates `SAAS-yyyy-#####` on the **system** org sequence and `AssignPlatformDocumentNumber` before generate, so the PDF shows `SAAS-…`. The Guid-slice fallback runs when both commercial fields are null — a replay of generate on a row that never got a number, or a test/manual `GenerateAndStorePlatformSaasInvoiceCommand` against a bare entry. `AssignPlatformDocumentNumber` now **throws** on whitespace (`LedgerEntry.cs:87-88`), so a blank sequence no longer silently skips assign (the 009 “whitespace skip” story is stale for SaaS). Likelihood is still low; the fallback is still a Guid slice, not `PENDING`.

### Still present?
**STILL BROKEN**

```63:65:apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateAndStorePlatformSaasInvoiceCommandHandler.cs
        var invoiceNumber = entry.CustomerDocumentNumber
            ?? entry.TaxInvoiceId
            ?? entry.Id.ToString()[..8].ToUpperInvariant();
```

Happy path still assigns first (`PlatformSaasFeeHandler.cs:87-102`). Merchant documents use the safer helper (`DocumentSeries.cs:37-46`; locked by `DocumentSeriesTests.CustomerFacingNumber_NeverUsesRawUuid`). `PlatformSaasInvoiceTests.StoreHandler_UploadsPdf_DoesNotPublishInvoiceIssued` seeds `SAAS-2026-00003` and never hits the fallback. No test asserts the `[..8]` branch.

### Related files
- [`apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateAndStorePlatformSaasInvoiceCommandHandler.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateAndStorePlatformSaasInvoiceCommandHandler.cs) — Guid-slice fallback.
- [`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/PlatformSaasFeeHandler.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/PlatformSaasFeeHandler.cs) — sequence + assign before PDF.
- [`apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs`](apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs) — `AssignPlatformDocumentNumber` throws on whitespace.
- [`apps/lazuar-api/Modules/Billing/Contracts/DocumentSeries.cs`](apps/lazuar-api/Modules/Billing/Contracts/DocumentSeries.cs) — the helper this handler should reuse.
- [`apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/Commands/PlatformSaasInvoiceTests.cs`](apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/Commands/PlatformSaasInvoiceTests.cs) — happy-path number.
- [`apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/DocumentSeriesTests.cs`](apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/DocumentSeriesTests.cs) — merchant “never UUID”, not Hub.

### Tests
- Existing: `PlatformSaasInvoiceTests.Factory_*`, `StoreHandler_UploadsPdf_DoesNotPublishInvoiceIssued`; `LedgerEntryAndAccountTypesTests.AssignPlatformDocumentNumber_DoesNotStartB2cConsolidation`; `PlatformSaasFeeHandlerTests` asserts `CustomerDocumentNumber == "SAAS-2026-00001"` when the mediator returns that string.
- None fail on the Guid-slice fallback. `CustomerFacingNumber_NeverUsesRawUuid` does not apply to this handler.
- First regression: handle generate on a `SYSTEM_SAAS_FEE` row with both numbers null and assert the printed number is `"PENDING"` (or a newly assigned `SAAS-`) — **not** `entry.Id.ToString()[..8]`.

### Reproduction today
Insert a `SYSTEM_SAAS_FEE` ledger row without calling `AssignPlatformDocumentNumber`. Send `GenerateAndStorePlatformSaasInvoiceCommand`. Open the PDF (or intercept `PlatformSaasInvoiceFactory.Create`’s `invoiceNumber`). Assert: 8 hex chars, uppercase, from the entry Guid — not `SAAS-yyyy-#####`. Happy path with a real Hub payment will not show this.

### Blast radius
Hub invoice PDF “No:” only. Low likelihood on the live pay path. Confusing for a tenant if generate is retried after a failed assign, or if ops re-runs the command. Not tax/LHDN (Hub invoices are not MyInvois). Still P2.

### Suggested fix
Replace the fallback with `DocumentSeries.CustomerFacingNumber(entry.CustomerDocumentNumber, entry.TaxInvoiceId)` (prints `PENDING`, never a Guid). Optionally, if both are null, allocate `SAAS-yyyy` and `AssignPlatformDocumentNumber` inside the generate handler before render. Do not TypeSpec-regen. Do not publish `InvoiceIssued`.

### Evaluation notes
009’s “whitespace skips assign” is **false** for current `AssignPlatformDocumentNumber` (throws). Fallback code still present → still broken, not speculation. Still P2. Not blocked. 161–200 did not touch this handler.

