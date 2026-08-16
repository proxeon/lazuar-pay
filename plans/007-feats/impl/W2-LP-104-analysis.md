# W2-LP-104 — Credit / debit / refund notes

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 2 `LP-104`. Tracker: *Credit / debit / refund notes* — Lazuar **B**. Aliases `LP-TAX-006`, `INV-017`–`022`.  
**Not this ID:** Money refund loop (`LP-091`–`093`). Manual AR credit memo composer (refuse — page copy is right). Self-billed `11`–`14` (`LP-115` Wave 4). Debit-note **product** as first-class (`INV-018` later).

**Invariant:** A completed refund (or in-window cancel) leaves a **visible** contra document. If the original e-invoice was `VALID` and ≤72h → MyInvois **cancel**. If >72h → LHDN type **`02` Credit Note submitted through `SubmitTaxDocumentCommand`**, not a raw XML insert. Types `03`/`04` stay factory-capable, not ops buttons.

---

## 0. Scope lock

In scope:

- Remount ops Credit Notes page (ledger `type_filter=reversals`)
- Keep automated-only (no “create CN” form)
- Close the post-72h LHDN path: **submit**, XSD, credits, real buyer
- Use `CN-{yyyy}` (LP-101) as `internal_id`
- Full refund + in-window cancel already sketched in Lhdn refund handler

Out of scope:

- Debit note (`03`) UI
- Refund note (`04`) as distinct from `02` (LHDN distinguishes; we stay on `02` unless tax advisor says otherwise)
- Partial refund CN that still **cancels** the whole invoice (LP-092 must gate LHDN)
- Apply-credit-to-invoice-X balances

---

## 1. Verdict

Ops page is an honest **ledger reversal browser**. LHDN is half-wired.

| Path | Behavior |
|------|----------|
| Billing `GatewayRefundCompletedHandler` | Contra revenue + scaled tax. Live if refunds complete (Wave 1) |
| Lhdn refund, ≤72h since `ValidatedAt` | `CancelDocumentAsync` + `doc.Cancel()` |
| Lhdn refund, >72h | Build type `02` XML, `AddTaxDocument`, **no** `SubmitTaxDocumentCommand` (no XSD, no credits, no worker claim unless status is `PENDING` — it is created PENDING, so worker **may** pick it up) |
| Buyer on that CN | Stub `IG1234567890` |
| Debit / refund note | `DocumentStrategyFactory` `02|03|04` → same template |
| Ops | Unrouted `CreditNotesPage` |

`TaxDocument` constructor sets `PENDING`. So the “local save” CN **will** be claimed by `LhdnSubmissionJob` if credentials exist — **with stub buyer XML**. That is worse than not submitting.

---

## 2. Current files

| Path | Role |
|------|------|
| `…/invoicing/pages/CreditNotesPage.tsx` | Reversals list; copy: automated only |
| `TaxInvoiceDetailPanel.tsx` | Shared download + cancel |
| `Modules/Lhdn/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs` | 72h cancel vs CN insert |
| `Modules/Lhdn/Infrastructure/Services/DocumentStrategyFactory.cs` | `02/03/04` → `CreditNoteStrategy` |
| `Modules/Lhdn/Infrastructure/Templates/CreditNote.xml` | `doc_type_code` |
| `Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs` | Ledger contra |
| `CancelWindowMustBeValidRule.cs` | 72h from `ValidatedAt` |

Refund LHDN lookup: `GetTaxDocumentByInternalId(org, PaymentRecordId.ToString())`. Original submit `internal_id` is **not** the Commerce payment GUID (it is `RCPT-` / `INV-` / `B2C-CONS-`). **Original document is usually not found** → handler returns. Closed loop is broken even before the stub.

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | Ops unrouted |
| G2 | Refund handler cannot find original `TaxDocument` (wrong internal id) |
| G3 | Post-72h CN uses stub buyer and bypasses `SubmitTaxDocumentCommand` (still PENDING → may submit garbage) |
| G4 | No `CN-yyyy` series |
| G5 | Partial refund would cancel **whole** VALID invoice if G2 is fixed without an amount gate (coordinate LP-092) |
| G6 | Debit/refund notes are not products — do not add buttons |

---

## 4. Recommended model

```
GatewayRefundCompleted
  → find TaxDocument by ledger CustomerDocumentNumber / LhdnDocumentUuid / stored internal_id
  → if none: log, stop (B2C receipt-only sale)
  → if VALID && hours since ValidatedAt ≤ 72 && refund is FULL:
        CancelTaxDocumentCommand (same as ops cancel)
  → if VALID && (window expired || policy says CN):
        SubmitTaxDocument type 02, original_lhdn_uuid set,
        buyer from CRM (same profile as original sale),
        amounts = refunded amount + tax reverse,
        internal_id = CN-yyyy-#####
  → NEVER insert TaxDocument without going through SubmitTaxDocumentCommand
```

Ops: remount page. No composer.

Type `04` only if you later learn IRBM wants refund-note for card refunds. Default `02`.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `App.tsx` + Sidebar | `/invoicing/credit-notes` |
| Lhdn refund handler | Resolve original doc correctly; call `CancelTaxDocumentCommand` or `SubmitTaxDocumentCommand`; delete stub buyer |
| Persist original `internal_id` on ledger | If needed for lookup (`CustomerDocumentNumber` / `LhdnDocumentUuid`) |
| `SubmitTaxDocument` CN | `original_lhdn_uuid` + `adjustment_reason` already on DTO |
| LP-091/092 | Do not fire LHDN cancel on partial |

Must not: manual CN form; Payment Order as CN; self-billed UI.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Refund, no TaxDocument | No-op, no stub row |
| VALID, 1h later, full refund | Cancel API called; no new `02` |
| VALID, 80h later, full refund | One `SubmitTaxDocument` type `02`; buyer TIN from CRM fixture, **not** `IG1234567890` |
| CN row | `PENDING` only via submit command (idempotency key set) |
| Partial refund | No cancel of original (once 092 lands) |

---

## 7. Acceptance

1. Ops Credit Notes lists refund reversals; download works when a PDF exists.  
2. Full refund of a VALID e-invoice ≤72h cancels at MyInvois.  
3. After 72h, a type `02` is **submitted** (sandbox) with the real buyer and `CN-` number.  
4. No stub TINs in `lhdn.TaxDocuments`.  
5. No debit-note button.

Tracker **B → P** after 1 + 3. **Y** when 2–4 work on sandbox with a real original UUID.

---

Do **not** implement from this file.
