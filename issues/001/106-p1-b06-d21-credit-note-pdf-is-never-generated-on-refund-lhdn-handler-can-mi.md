---
number: "106"
id: B06-D21
severity: P1
status: resolved
resolved_branch: fix/106-credit-note-pdf
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 106 — B06-D21 — Credit note PDF is never generated on refund; Lhdn handler can mint a second `CN-`

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/106-credit-note-pdf`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D21 — Credit note PDF is never generated on refund; Lhdn handler can mint a second `CN-` (P1)

**Status:** open.

`GatewayRefundCompletedHandler` does **not** call `GenerateAndStoreDocumentCommand`. The only Credit Note PDF path is VALID regen (`LhdnDocumentValidatedIntegrationEventHandler.ResolveDocumentType` returns `"Credit Note"` for refund / CN- numbers). Partial refunds never VALID. Full refunds inside 72h cancel (no type `02`). Full refunds after 72h may VALID later.

Until then, ops “Download PDF Document” (`TaxInvoiceDetailPanel.tsx:77–81`) hits `GET /admin/billing/ledger/{id}/document`, which **always** presigns `vault/{tenant}/documents/{id}.pdf` with **no existence check** (`AdminLedgerEndpoints.cs:36–46`). Buyer portal does the same HMAC URL. The click 404s on R2.

Race: Lhdn refund handler looks up the refund ledger by `PaymentRecordId:EventId` (`173–184`). If it runs **before** Billing’s refund handler, it calls `GenerateNextSequenceNumberCommand` and files type `02` as `CN-00002` while Billing later stamps the ledger `CN-00001`. Two commercial numbers, one refund. `TaxDocuments` index on `(OrganizationId, InternalReferenceId)` is **not unique** (`LhdnDbContext.cs:76`).

