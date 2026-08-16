# W2-LP-103 — Tax invoice (commercial + trigger for legal)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 2 `LP-103`. Tracker: *Tax invoice (commercial)* — Lazuar **B**. Inventory `INV-006` / `INV-019`.  
**Not this ID:** Official Receipt already generated for B2C (`P` on `LP-100`). MyInvois XML submit (`LP-110`). QR (`LP-113`). Buyer portal history (`LP-175`). AR open invoices (`LP-105`).

**Invariant:** A B2B paid sale produces a **customer tax-invoice PDF** with a real `INV-` number and real buyer identity. After MyInvois `VALID`, the **same** PDF is regenerated with UUID + QR. A QuestPDF titled “Tax Invoice” without a submit is stationery, not compliance.

---

## 0. Scope lock

In scope:

- Remount ops Tax Invoices page (ledger `type_filter=sales`)
- B2B path: allocate `INV-` (LP-101), generate PDF **on pay** (not only after VALID)
- Wire a **real** submit trigger (not stub `InvoiceIssued`)
- After VALID, regenerate PDF as “Tax Invoice” with QR (existing handler, once lookup works)

Out of scope:

- Creating invoices by hand in a composer
- Due dates / reminders
- Debit notes
- Self-billed
- Calling the page a MyInvois console (that is LP-110/111)

---

## 1. Verdict

Two objects share a name:

| Object | Exists? | Merchant-visible? |
|--------|---------|-------------------|
| Official Receipt PDF (B2C) | Yes, on `GATEWAY_PAYMENT` | Email HMAC only |
| QuestPDF `DocumentType = "Tax Invoice"` | Yes, on LHDN `VALID` | Ops page unrouted; portal download hidden |
| LHDN type `01` individual | Submit command yes; **B2B trigger dead** | No |
| Ops “Tax Invoices & Receipts” | Ledger browser | Unrouted |

`InvoiceIssuedIntegrationEvent` has Billing + Lhdn consumers and **zero publishers**. The Lhdn consumer hardcodes buyer `"Resolved via CRM"` / TIN `C1234567890`. **Do not turn the publisher on.**

B2B `GatewayPaymentCompletedHandler` already skips receipt PDF. So a B2B sale (once LP-022 stamps metadata) is **silent**: no receipt, no tax invoice, no submit.

---

## 2. Current files

| Path | Role |
|------|------|
| `…/invoicing/pages/TaxInvoicesPage.tsx` | `GET /admin/billing/ledger?type_filter=sales` |
| `…/invoicing/components/TaxInvoiceDetailPanel.tsx` | Download `GET /admin/billing/ledger/{id}/document`; cancel uses **ledger GUID** as LHDN `internalId` (wrong — LP-116) |
| `GatewayPaymentCompletedHandler.cs` | B2C receipt; B2B `MarkConsolidationNotRequired` only |
| `GenerateAndStoreDocumentCommandHandler.cs` | QuestPDF; number = `CustomerDocumentNumber` ?? `TaxInvoiceId` ?? GUID8 |
| `LhdnDocumentValidatedIntegrationEventHandler.cs` | Lookup `Ledger.ReferenceId == InternalReferenceId`; then “Tax Invoice” or “Credit Note” |
| `InvoiceIssuedIntegrationEventHandler.cs` (Lhdn) | Stub buyer — **do not publish** |
| `InvoiceIssuedHandler.cs` (Billing) | Books AR + deferred revenue; no cash apply |

### Lookup bug (blocks PDF after VALID)

Poller / submit use LHDN `internal_id` = receipt #, `B2C-CONS-…`, or invoice number.

Payment ledger `ReferenceId` = **gateway transaction id**.

`LhdnDocumentValidated` matches `ReferenceId == InternalReferenceId`. **It will not find B2C receipts or consolidation batches.** Tax Invoice PDF + QR after VALID is dead for the live money path. Consolidation sets `TaxInvoiceId` to the batch ref — lookup should use `CustomerDocumentNumber` **or** `TaxInvoiceId` **or** `ReferenceId`.

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | Ops page unrouted / not imported / no sidebar |
| G2 | No B2B PDF on pay |
| G3 | No honest B2B submit trigger (orphan event + stub TIN) |
| G4 | VALID → PDF lookup uses the wrong ledger key |
| G5 | B2B rows have no `INV-` (LP-101) |
| G6 | Panel cancel internal id is ledger GUID (LP-116) |

Un-hiding G1 without G2–G4 shows a list of B2C receipts titled “Tax Invoices” with badge `B2C_RECEIPT` / `NOT REQUIRED`. That is acceptable **if** copy says “Sales documents” and download works.

---

## 4. Recommended model

```
B2C pay
  → RCPT- + Official Receipt PDF (already)
  → monthly consolidation (LP-114) → type 01 consolidated
  → VALID → update those rows (fix lookup) → optional “consolidated” stamp, keep RCPT- on the receipt

B2B pay (TIN present)
  → INV- on CustomerDocumentNumber
  → GenerateAndStore "Tax Invoice" immediately (commercial)
  → SubmitTaxDocument type 01 with CRM Tin + name + address (LP-110)
  → VALID → regenerate PDF + QR (LP-113)
```

Do **not** publish `InvoiceIssued` until buyer fields are real **and** you want AR booking. Prefer a new event `B2bTaxInvoiceRequested` (org, ledgerId, customerProfileId, invoiceNumber) consumed only by Lhdn. Leave the orphan event dead.

Ops page: remount as **Sales documents**. Badge copy already distinguishes B2C receipt vs VALID.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `App.tsx` + `Sidebar` | Route `/invoicing/tax-invoices` |
| `GatewayPaymentCompletedHandler` | B2B: sequence `INV-` + `GenerateAndStoreDocument("Tax Invoice")` |
| New publisher **or** inline mediator send | `SubmitTaxDocument` with CRM buyer (LP-110). This ticket owns the **hook**; LP-110 owns payload honesty |
| `LhdnDocumentValidatedIntegrationEventHandler` | Match `CustomerDocumentNumber` / `TaxInvoiceId` / `ReferenceId` |
| `TaxInvoicesPage` | Show `customer_document_number` if DTO exposes it (add to TypeSpec if missing) |

Must not: publish stub `InvoiceIssued`; title B2C receipts “Tax Invoice” in the PDF.

---

## 6. Tests

| Case | Expect |
|------|--------|
| B2C pay | Official Receipt; no Tax Invoice title |
| B2B pay | `INV-` + stored PDF; consolidation not pending |
| VALID event with `internal_id = RCPT-…` | Ledger status VALID; PDF regen **found** |
| VALID with `internal_id = B2C-CONS-…` | Rows with that `TaxInvoiceId` updated |
| No `InvoiceIssued` published on pay | `DidNotReceive` |

---

## 7. Acceptance

1. Ops **Sales documents** lists receipts and (when B2B exists) tax invoices; download returns a PDF when one was stored.  
2. B2B sandbox pay → PDF headed **Tax Invoice** + `INV-` **without** waiting for VALID.  
3. After VALID (LP-111), same PDF gains UUID + QR.  
4. B2C PDFs remain **Official Receipt**.  
5. Stub TIN `C1234567890` never submitted.

Tracker **B → P** after 1–2. **Y** when 3 works with real buyer data (LP-022 + LP-110 + LP-111).

---

Do **not** implement from this file.
