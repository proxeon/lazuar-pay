---
number: "105"
id: B06-D20
severity: P1
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 105 — B06-D20 — Partial refunds skip LHDN entirely; commercial `CN-` still issued

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D20 — Partial refunds skip LHDN entirely; commercial `CN-` still issued (P1)

**Status:** open.

```49:55:apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs
        if (!@event.IsFullRefund)
        {
            _logger.LogInformation(
                "Skipping LHDN cancel/CN for partial refund PaymentRecordId {PaymentRecordId}.",
                @event.PaymentRecordId);
            return;
        }
```

Billing still allocates `CN-` on every refund row (`GatewayRefundCompletedHandler.cs:73–76`). Ops Credit Notes shows a CN number with LHDN “NOT REQUIRED.” Portal classifies it Credit Note and offers a download. No PDF was generated (B06-D21). The legal note does not exist.

