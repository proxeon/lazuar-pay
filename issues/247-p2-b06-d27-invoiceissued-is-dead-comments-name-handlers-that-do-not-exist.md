---
number: "247"
id: B06-D27
severity: P2
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 247 — B06-D27 — InvoiceIssued is dead; comments name handlers that do not exist

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D27 — InvoiceIssued is dead; comments name handlers that do not exist (P2)

```8:25:apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/InvoiceIssuedIntegrationEventHandler.cs
/// InvoiceIssued has no honest buyer identity. MyInvois submit is
/// <see cref="B2bSaleSubmitHandler"/>. This handler must never file stub TIN C1234567890.
...
            "Ignoring InvoiceIssued {Invoice} — MyInvois submit uses B2bSaleReadyForEinvoice only.",
```

`B2bSaleSubmitHandler` does not exist. `B2bSaleReadyForEinvoice` does not exist. The live type is `B2bTaxInvoiceRequestedIntegrationEventHandler`. Grep of `new InvoiceIssuedIntegrationEvent` in production: zero. `MyInvoisLoopTests.InvoiceIssuedHandler_DoesNotSubmitStubTin` only asserts the no-op does not throw.

