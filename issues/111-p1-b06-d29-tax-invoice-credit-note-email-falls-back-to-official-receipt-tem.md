---
number: "111"
id: B06-D29
severity: P1
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 111 — B06-D29 — Tax Invoice / Credit Note email falls back to Official Receipt template

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D29 — Tax Invoice / Credit Note email falls back to Official Receipt template (P1)

```38:50:apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs
        var preferredTemplate = @event.DocumentType switch
        {
            "Official Receipt" => "Official Receipt",
            "Draft Quotation" => "Quotation Ready",
            "Tax Invoice" => "Tax Invoice",
            "Credit Note" => "Credit Note",
            _ => null
        };
        ...
        var fallbackTemplate = preferredTemplate is "Tax Invoice" or "Credit Note"
            ? "Official Receipt"
            : null;
```

If the merchant never created a Tax Invoice template, the buyer gets an email that talks like a receipt, linking a PDF titled Tax Invoice. Draft quotes use document type `"Proforma Invoice"`, which is **not** in the switch (`GenerateDraftDocumentQueryHandler.cs:71`). `"Draft Quotation"` is never published. Quote emails, if any, are the invoice-reminder job (out of this slice except to note they exist).

