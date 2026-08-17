---
number: "107"
id: B06-D22
severity: P1
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 107 — B06-D22 — Original document resolution can walk the wrong key; cancel+refund double row

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D22 — Original document resolution can walk the wrong key; cancel+refund double row (P1)

**Status:** open.

```150:156:apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs
        var candidates = new[]
        {
            payment?.CustomerDocumentNumber,
            payment?.LhdnDocumentUuid,
            payment?.TaxInvoiceId,
            @event.PaymentRecordId.ToString()
        }
```

After VALID, `TaxInvoiceId` is the UUID — looking up by UUID is correct. `PaymentRecordId.ToString()` as last candidate is a Guid. `GetTaxDocumentByInternalIdAsync` is FirstOrDefault on a **non-unique** index. Unlikely unless someone submitted with that Guid as `internal_id`. The more common “wrong doc” is the **missing** original (skip) or the **second CN** (B06-D21).

Full refund ≤72h sends `CancelTaxDocumentCommand` (`68–74`). Cancel also posts `LHDN_CANCELLATION` mirroring every original line (`LhdnDocumentCancelledIntegrationEventHandler.cs:41–65`). Billing already posted `GATEWAY_REFUND` with a `CN-`. Credit Notes page lists both (`BillingQueryService.cs:60–62`). Trigger labels: “Refund” vs “Cancellation” (`CreditNotesPage.tsx:157`). A 72h refund+cancel appears **twice**. Cancellation row has **no** `CN-` (`AssignCustomerDocumentNumber` is never called).

