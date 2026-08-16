# W2-LP-101 — Sequential document numbers

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 2 `LP-101`. Tracker: *Sequential document numbers* — Lazuar **P**. Inventory `INV-005` / `INV-009`.  
**Not this ID:** PDF layout (`LP-107`). Tax invoice product (`LP-103`). Quote UX (`LP-102`). LHDN UUID as a number (never). Configurable “next number” migration UI (nice later).

**Invariant:** Every customer-facing commercial document has a **per-org, per-series** number (`PREFIX-yyyy-#####`) allocated atomically. LHDN UUID stays a **validation stamp**, never the invoice number.

---

## 0. Scope lock

In scope:

- Keep `RCPT-{yyyy}-#####` for B2C Official Receipts
- Add series for quotes, B2B tax invoices, credit notes
- Use `GenerateNextSequenceNumberCommand` (already atomic)
- Stamp `LedgerEntry.CustomerDocumentNumber` / quote PDF header / LHDN `internal_id` from that series

Out of scope:

- Gapless-after-rollback legal guarantee (Postgres upsert already consumes the number)
- Per-customer sequences (Stripe-class)
- Merchant-editable prefix / next number in ops
- Using LHDN UUID as `cbc:ID`

---

## 1. Verdict

Tracker **P** is honest: **receipts are sequential; everything else is a GUID costume.**

| Document | Scheme today | Sequential? |
|----------|--------------|-------------|
| B2C Official Receipt | `GenerateNextSequenceNumberCommand` prefix `RCPT-{yyyy}` → `RCPT-2026-00001` | **Yes** (org + prefix) |
| Quote / proforma PDF | `QUOTE-{sessionId[0..8]}` | No |
| B2B tax invoice PDF | `CustomerDocumentNumber` ?? `TaxInvoiceId` ?? first 8 of ledger GUID | Broken — B2B skips `AssignB2cReceipt` |
| LHDN `internal_id` | Receipt #, or `B2C-CONS-{yyyyMM}-{org}`, or event invoice number | Mixed |
| Credit note | `CN-{PaymentRecordId}` | No |

Handler comment claims the upsert “prevents sequence gaps during rollbacks.” False: `INSERT … ON CONFLICT DO UPDATE CurrentValue + 1 RETURNING` **commits the increment** in its own connection. A later PDF/LHDN failure still burns the number. Treat as **mostly sequential**, not IRBM-gapless.

---

## 2. Current files

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateNextSequenceNumberCommandHandler.cs` | Atomic `billing.DocumentSequences` upsert |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` | B2C only: `RCPT-{yyyy}` then `AssignB2cReceipt` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs` | `CustomerDocumentNumber` immutable; `TaxInvoiceId` still dual-use |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Queries/GenerateDraftDocumentQueryHandler.cs` | `QUOTE-{guid8}` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs` | `CN-{PaymentRecordId}` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs` | `B2C-CONS-{yyyyMM}-{org:N}` (unique, not a customer series) |

`AssignB2cReceipt` also copies the receipt # into legacy `TaxInvoiceId` if empty. `UpdateLhdnStatus` **overwrites `TaxInvoiceId` with the UUID**. PDF generator prefers `CustomerDocumentNumber` — correct for receipts. B2B rows never get a customer number.

---

## 3. Exact gaps

### G1 — Quotes are not a series

Auditors and Chargebee/Xero buyers expect `QT-2026-00012`. GUID slices can theoretically collide and look unfinished.

### G2 — B2B has no number

`is_b2b_required` skips `AssignB2cReceipt`. PDF falls through to UUID or 8-char GUID. LHDN `cbc:ID` would be ugly or collide with UUID rules.

### G3 — Credit notes are payment-id strings

Unique, not a series. Fine as a **correlation key**; not as the printed CN number.

### G4 — Consolidation ref is not a customer invoice number

`B2C-CONS-…` is an internal batch id. Keep it for LHDN `internal_id`. Do not print it as the buyer’s invoice number on individual receipts (`RCPT-` stays).

### G5 — No shared prefix constants

Callers invent strings. Easy to typo `RCPT` vs `INV`.

**Not this ticket:** printing SST/SSM (LP-107 / LP-118). Un-hiding the tax-invoice page (LP-103).

---

## 4. Recommended model

One command, four prefixes (year baked into prefix, same as today):

| Series | Prefix | When |
|--------|--------|------|
| Official Receipt | `RCPT-{yyyy}` | Non-B2B `GATEWAY_PAYMENT` / offline enroll (already) |
| Quote / proforma | `QT-{yyyy}` | `CreateCustomCheckout` **or** first draft PDF (once; persist on session) |
| Tax invoice (individual) | `INV-{yyyy}` | B2B sale **before** MyInvois submit; also the LHDN `internal_id` |
| Credit / debit / refund note | `CN-{yyyy}` | Post-72h LHDN note and printed CN |

Rules:

1. **SSoT = `GenerateNextSequenceNumberCommand`.** No GUID numbers on customer PDFs.
2. Persist quote number on `CheckoutSession` (new nullable `DocumentNumber`) so refresh does not mint `QT-00002`.
3. B2B: allocate `INV-` when booking the ledger (or when publishing the real submit trigger). Write `CustomerDocumentNumber`. Use that as LHDN `internal_id`.
4. Never put LHDN UUID in `CustomerDocumentNumber`. `UpdateLhdnStatus` already promised this for receipts — keep it.
5. Consolidation batch keeps `B2C-CONS-…` as **internal_id only**.
6. Do not add ops “set next number” in this ticket.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| New `DocumentSeries` constants in Billing | `Receipt`, `Quote`, `Invoice`, `CreditNote` prefix helpers |
| `CheckoutSession` + Commerce migration | `DocumentNumber` nullable |
| `CreateCustomCheckoutCommandHandler` | Allocate `QT-{yyyy}` once |
| `GenerateDraftDocumentQueryHandler` | Use session `DocumentNumber`, not GUID slice |
| `GatewayPaymentCompletedHandler` B2B branch | Allocate `INV-{yyyy}`, set `CustomerDocumentNumber` (do **not** assign B2C consolidation) |
| LHDN refund CN path | Allocate `CN-{yyyy}` as `internal_id` (keep `PaymentRecordId` in a correlation field / description) |
| `GenerateAndStoreDocumentCommandHandler` | Already prefers `CustomerDocumentNumber` — verify B2B |

Must not: change `RCPT-` format; use UUID as invoice #; claim gapless.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Two concurrent B2C payments | Distinct `RCPT-yyyy-#####`, no unique violation |
| Two quotes | `QT-yyyy-00001` then `00002`; second draft of same session **same** number |
| B2B payment | `INV-yyyy-#####` on `CustomerDocumentNumber`; `ConsolidationStatus=NOT_REQUIRED` |
| VALID event | UUID on `LhdnDocumentUuid`; customer number unchanged |
| CN after 72h | `CN-yyyy-#####` unique per org |

Existing `AssignB2cReceipt` / `UpdateLhdnStatus` tests stay green.

---

## 7. Acceptance

1. New B2C receipts still `RCPT-{year}-{5 digits}`.  
2. New quotes print `QT-{year}-{5 digits}` (same number on HTML + draft PDF).  
3. New B2B ledger rows have `INV-…` **before** any MyInvois UUID exists.  
4. PDF header never shows a raw UUID as “No:”.  
5. Tracker **P → Y** when quotes + B2B + CN all use the command. Receipts-only is still **P**.

---

## 8. Suggested implement order

1. Prefix helpers + quote persist  
2. B2B `INV-` on payment  
3. CN series when LP-104 wires submit  
4. Do not wait for LP-117  

Do **not** implement from this file.
