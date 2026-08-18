---
number: "315"
id: B09-U47
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 315 — B09-U47 — Credit-note rows open a tax-invoice cancel panel

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U47 — Credit-note rows open a tax-invoice cancel panel (P2)

`CreditNotesPage.tsx` mounts `TaxInvoiceDetailPanel`.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Credit Notes is a ledger list (`GET /admin/billing/ledger?type_filter=reversals`: `GATEWAY_REFUND` + `LHDN_CANCELLATION`). Clicking a row still mounts `TaxInvoiceDetailPanel` — the same side sheet used for sales documents. At audit HEAD that panel was titled “Tax Document Details” and showed “Cancel e-Invoice (LHDN)” whenever `lhdn_validation_status === "VALID"`, so a credit-note row could offer a supplier-cancel of a document that is already a reversal. Issue 141 taught the panel `classifySalesDocument` and gated cancel to `documentKind === "Tax Invoice"` plus the 72h window. Refund rows that received a `CN-YYYY-…` number now classify as Credit Note and lose the cancel button. The mount is still the invoice panel: sales-shaped math (`REVENUE_GROSS` + `Math.abs`), “Download PDF Document”, and no credit-note-specific chrome. `LHDN_CANCELLATION` rows never get a `CN-` number (`LhdnDocumentCancelledIntegrationEventHandler` does not call `AssignCustomerDocumentNumber`), so they classify as Official Receipt or Invoice, not Credit Note.

### Still present?
**PARTIAL**

Mount is unchanged:

```229:233:apps/lazuar-ops/src/modules/invoicing/pages/CreditNotesPage.tsx
      {selectedNote && (
        <TaxInvoiceDetailPanel 
          invoice={selectedNote} 
          onClose={() => setSelectedNote(null)} 
        />
      )}
```

141’s gate (cancel no longer follows VALID alone):

```154:156:apps/lazuar-ops/src/modules/invoicing/components/TaxInvoiceDetailPanel.tsx
  const documentKind = classifySalesDocument(invoice);
  const isTaxInvoice = documentKind === "Tax Invoice";
  const isCancelable = isTaxInvoice && isLhdnValidated && hoursSinceValid < 72;
```

```313:321:apps/lazuar-ops/src/modules/invoicing/components/TaxInvoiceDetailPanel.tsx
              {isTaxInvoice && (
                <>
                {isCancelable ? (
                  <button 
                    onClick={() => setIsCancelModalOpen(true)}
                    className="h-9 w-full border border-rose-200 bg-rose-50 text-[11px] font-bold uppercase tracking-widest text-rose-700 hover:bg-rose-100 transition-colors flex items-center justify-center gap-1.5 rounded-sm"
                  >
                    <AlertTriangle size={14} /> Cancel e-Invoice (LHDN)
```

Ops classifier is a weaker clone of portal `Classify`: it keys only on `CN-` / `INV-` / `customer_type`, not `reference_type`:

```1:17:apps/lazuar-ops/src/modules/invoicing/lib/salesDocumentType.ts
export function classifySalesDocument(entry: {
  customer_type?: string | null;
  customer_document_number?: string | null;
  lhdn_validation_status?: string | null;
}): "Credit Note" | "Tax Invoice" | "Invoice" | "Official Receipt" {
  const number = entry.customer_document_number ?? "";
  if (number.toUpperCase().startsWith("CN-")) return "Credit Note";
  const isInvoice =
    entry.customer_type === "B2B" || number.toUpperCase().startsWith("INV-");
  ...
}
```

Portal (correct) also treats `GATEWAY_REFUND` / `LHDN_CANCELLATION` as Credit Note (`PortalDocumentQueryService.cs` 194–200). Refunds assign `CN-` (`GatewayRefundCompletedHandler.cs` 92–95). LHDN cancel entries do not (`LhdnDocumentCancelledIntegrationEventHandler.cs` 63–83). Panel math still sums `REVENUE_GROSS` with `Math.abs` (`TaxInvoiceDetailPanel.tsx` 100–115), so a contra row can look like a sale.

### Related files
- `apps/lazuar-ops/src/modules/invoicing/pages/CreditNotesPage.tsx` — mounts the invoice panel; list math uses `CONTRA_REVENUE_REFUNDS`.
- `apps/lazuar-ops/src/modules/invoicing/components/TaxInvoiceDetailPanel.tsx` — shared sheet, cancel gate, sales math.
- `apps/lazuar-ops/src/modules/invoicing/lib/salesDocumentType.ts` — missing `reference_type`.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/PortalDocumentQueryService.cs` `Classify` — the SSoT to copy.
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs` — assigns `CN-`.
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/LhdnDocumentCancelledIntegrationEventHandler.cs` — reversal without `CN-`.
- `apps/lazuar-api/Modules/Billing/Contracts/DocumentSeries.cs` — `IsCreditNoteNumber`.
- Issue 141 (`fix/141-sales-docs-receipts`) — cancel gate / titles.
- Issue 094 — VALID badge honesty on uncleared objects.

### Tests
- Existing tests that touch this path: none on `CreditNotesPage` / `classifySalesDocument` / `TaxInvoiceDetailPanel`. Billing tests cover refund/CN sequence and LHDN cancel *ledger* balance; they do not open the ops panel. Portal `Classify` is not imported by ops.
- Whether any test would fail if the remaining bug is still there: **No.** 141’s cancel gate has no frontend test; a VALID `CN-` row silently hiding cancel would not fail CI. A VALID B2B reversal *without* `CN-` that showed cancel would also not fail CI.
- What a first regression test should assert: `classifySalesDocument` (or a shared helper) returns `"Credit Note"` for `{ reference_type: "GATEWAY_REFUND" }` and `{ reference_type: "LHDN_CANCELLATION" }` even when `customer_document_number` is empty; Credit Notes panel does not render “Cancel e-Invoice (LHDN)”; title is not “Tax Invoice details”.

### Reproduction today
Issue a refund on a B2B sale that produced a VALID tax invoice. Open Invoicing → Credit Notes → the `CN-` row. Assert: sheet title is “Credit Note details” (141); no Cancel e-Invoice button (141). Assert: ledger breakdown still looks like a positive sale if the refund only booked `CONTRA_REVENUE_REFUNDS` (list column shows -RM; panel `REVENUE_GROSS` path may show 0 or abs’d originals). Cancel a VALID tax invoice from Sales documents. Open Credit Notes → the `LHDN_CANCELLATION` row (no `CN-`). Assert: title is “Official Receipt details” or “Invoice details”, not “Credit Note details”.

### Blast radius
Merchants who refund or cancel e-invoices. The dangerous “cancel this credit note at LHDN as if it were a tax invoice” path is mostly closed for `CN-` rows (141). Remaining harm: wrong title/math on reversals; `LHDN_CANCELLATION` misclassified; a theoretical cancel if a reversal is B2B + VALID + not `CN-` (I did not find a writer that stamps VALID on the reversal row itself). Compliance chrome, not a second cash movement by itself — but a mistaken LHDN cancel is irreversible inside 72h. Frequency: every credit-note click.

### Suggested fix
Teach `classifySalesDocument` the same `reference_type is GATEWAY_REFUND | LHDN_CANCELLATION` rule as `PortalDocumentQueryService.Classify` (pass `reference_type` from the DTO; it is already on `LedgerEntryDto`). Optionally assign a `CN-` number on LHDN cancel persist. Keep using one panel if you must, but drive title/math/actions from `documentKind` (contra accounts, no supplier-cancel on Credit Note). Do not call LHDN cancel on a CN UUID. No TypeSpec regen. No homemade e-mandate / WhatsApp / Xero.

### Evaluation notes
U47 overlaps 141 (P1 receipts-as-invoices) and 094 (VALID on uncleared objects). 141 closed the VALID→cancel path for CN-numbered rows; U47 is the leftover mount + classifier hole. Severity as “cancel a credit note at LHDN” is now closer to P3; as “wrong panel/math” still P2 honesty. Not blocked. Do not reopen 141 unless cancel appears on a `CN-` row.

