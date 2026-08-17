---
number: "095"
id: B06-D08
severity: P1
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 095 — B06-D08 — Offline product mark-paid drops `IsB2bRequired`

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D08 — Offline product mark-paid drops `IsB2bRequired` (P1)

**Status:** open.

Custom session:

```210:220:apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs
            await _eventBus.PublishAsync(new ManualSubscriberEnrolledIntegrationEvent(
                ...
                session.IsB2bRequired));
```

Product session (`166–175`) omits the bool. Event default is `false` (`ManualSubscriberEnrolledIntegrationEvent.cs:16`). A product flagged “Require Company Name & Tax ID” that is marked paid offline is booked **B2C**, gets `RCPT-`, and never publishes `B2bTaxInvoiceRequested`.

`ManualSubscriberEnrolledIntegrationEventHandler` trusts `@event.IsB2bRequired` (`43`). It also books **no SST** (`51–52`: cash = revenue = `AmountPaid`). Offline product B2B is a double miss: wrong document type and understated tax.

