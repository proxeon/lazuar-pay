---
number: "012"
id: B06-D02
severity: P0
status: resolved
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
resolved_branch: fix/012-inv-not-tax-invoice-until-valid
---

# 012 — B06-D02 — `INV-` PDF titled “Tax Invoice” on pay, before VALID

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/012-inv-not-tax-invoice-until-valid`

B2B pay now stores an `Invoice` with a pending-validation note. Portal labels `INV-` as Tax Invoice only after MyInvois `VALID`.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D02 — `INV-` PDF titled “Tax Invoice” on pay, before VALID (P0)

**Status:** open. Intentional per `W2-LP-103-done.md`. Still a legal lie if the merchant emails it.

```119:126:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs
        else
        {
            await _mediator.Send(new GenerateAndStoreDocumentCommand(
                @event.OrganizationId,
                entry.Id,
                "Tax Invoice",
                CorrelationId: correlation
            ));
```

Factory adds **no** pending note for that title (`InvoiceDocumentFactory.cs:90–93`). `InvoiceDocumentFactoryTests.CreateHeader_TaxInvoice_DoesNotAddReceiptDisclaimer` **locks the missing note in**.

`GatewayPaymentCompletedHandlerTests.HandleAsync_WhenB2B_BooksB2b_SkipsReceiptAndOfficialPdf` asserts `DocumentType == "Tax Invoice"` and that Official Receipt is **not** sent. The test treats the lie as the spec.

Portal classifies the same row as Tax Invoice whenever `CustomerType == "B2B"` or the number starts with `INV-` (`PortalDocumentQueryService.cs:197–198`), VALID or not. Subscription card label becomes “Download tax invoice” (`184–185`).

