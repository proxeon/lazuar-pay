---
number: "094"
id: B06-D06
severity: P1
status: resolved
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
resolved_branch: fix/094-tax-invoice-badge-honesty
---

# 094 — B06-D06 — Ops / portal teach “Tax Invoice” / `VALID` on objects that are not cleared

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/094-tax-invoice-badge-honesty`

Portal already titles Invoice until VALID. Consolidation VALID no longer stamps RCPT children. Ops empty state is sales documents; B2B without status is PENDING SUBMIT.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D06 — Ops / portal teach “Tax Invoice” / `VALID` on objects that are not cleared (P1)

Portal `Classify`:

```189:200:apps/lazuar-api/Modules/Commerce/Infrastructure/Services/PortalDocumentQueryService.cs
        if (ledger.ReferenceType is "GATEWAY_REFUND" or "LHDN_CANCELLATION"
            || DocumentSeries.IsCreditNoteNumber(ledger.CustomerDocumentNumber))
        {
            return "Credit Note";
        }

        if (ledger.CustomerType == "B2B" || DocumentSeries.IsInvoiceNumber(ledger.CustomerDocumentNumber))
            return "Tax Invoice";

        return "Official Receipt";
```

Ops Sales documents empty state: “No tax invoices found.” (`TaxInvoicesPage.tsx:169`) while the page title is the honest “Sales documents.” Badge for a pre-submit B2B row is **“NOT REQUIRED”** (`207–210`) — which is the opposite of what the PDF title said.

After consolidation VALID, `LhdnDocumentValidatedIntegrationEventHandler` updates **every** matching ledger row to `VALID` (`41–44`) and the test **asserts** that (`LhdnDocumentValidatedIntegrationEventHandlerTests.cs:62–65`). Those `RCPT-` rows are not individually validated e-invoices. Handler skips QR regen for the cons key (`51–53`) — correct — but the badge lies.

