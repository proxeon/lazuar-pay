---
number: "252"
id: B06-D33
severity: P2
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 252 — B06-D33 — Buyer reject is not implemented

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D33 — Buyer reject is not implemented (P2, honestly labelled)

Ops footer: “Supplier cancel only… Buyer reject is not implemented.” (`TaxInvoiceDetailPanel.tsx:296–298`). True. No portal reject button, no IRBM reject webhook consumer. Domain cancel is 72h from **local** `ValidatedAt` (`CancelWindowMustBeValidRule.cs:12–26`), which is `DateTime.UtcNow` at `MarkAsValid`, not IRBM’s clock. Close enough for a first cut; not proven.

Cancel applies `doc.Cancel()` **before** the gateway call (`CancelTaxDocumentCommand.cs:50–58`). If the gateway succeeds and `SaveChanges` fails, MyInvois is cancelled and Lazuar still shows VALID. Next cancel attempt will 400 at LHDN. Narrow window, real split-brain.

