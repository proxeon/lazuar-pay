---
number: "096"
id: B06-D10
severity: P1
status: resolved
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
resolved_branch: fix/096-b2b-event-resolved-sst
---

# 096 — B06-D10 — B2B event `TaxAmount` is the raw gateway field, not the resolved SST

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/096-b2b-event-resolved-sst`

`B2bTaxInvoiceRequested` already receives resolved SST from `GatewayPaymentCompletedHandler` (076). Locked by `HandleAsync_B2bUsesResolvedSstNotRawEventTax`.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D10 — B2B event `TaxAmount` is the raw gateway field, not the resolved SST (P1)

**Status:** open.

Ledger books tax via `ResolveTaxAmount` (event field **or** metadata `sst_tax_amount`) (`GatewayPaymentCompletedHandler.cs:67`, `159–174`). Gross for the event is `AmountPaid - taxAmount` (resolved). The published event then sends **`@event.TaxAmount`**, not the resolved value (`134`).

Billplz’s `TaxAmount=0` with SST in metadata: ledger is right, type `01` is `Total_tax=0` and `Total_excluding_tax` already reduced by SST, so `Total_including_tax` does not equal amount paid. Pairs with B06-D09.

