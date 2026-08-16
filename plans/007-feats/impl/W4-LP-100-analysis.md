# W4-LP-100 — Commercial receipt honesty

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 4 `LP-100`. Tracker: *Commercial receipt / PDF* — Lazuar **P** (wave column **—** / tighten with Wave 2).  
**Not this ID:** Tax invoice + MyInvois (`LP-103` / `LP-110`). PDF branding completeness (`LP-107`). Sequential numbers (`LP-101`). Portal history (`LP-175`). Buyer single download (`LP-106`). AR (`LP-105`).

**Invariant:** Every successful **B2C** (and manual) collection produces a PDF titled **Official Receipt**, numbered `RCPT-`, emailed via `DocumentPublished`. The PDF must not look like an LHDN tax invoice. Missing legal profile must not print fake TIN `N/A` as if registered. SST line prints only when tax was booked. Docs/README must not call this an e-invoice.

---

## 0. Scope lock

In scope:

- QuestPDF copy/footer honesty  
- Hide empty TIN/SST rather than `"N/A"`  
- Tax row only if `Tax > 0` (`ShowZeroTax` false)  
- Footer: “Payment receipt. Not an LHDN e-invoice.”  
- Email path already exists — fix if document type / number is wrong  
- README / docs one-sentence honesty  

Out of scope:

- Un-hiding tax invoice UI  
- SST calculation (Billplz `TaxAmount=0` is a fee/tax **fidelity** hole — label empty tax, do not invent 8%)  
- B2B PDF on pay (LP-103)  
- Portal list (LP-175)

---

## 1. Verdict

B2C `GatewayPaymentCompletedHandler` assigns `RCPT-yyyy` and `GenerateAndStoreDocumentCommand` with `DocumentType = "Official Receipt"`. That is why the cell is **P**, not **N**. Honesty holes: fallback `CompanyName = "Lazuar Merchant"`, `CompanyTin = "N/A"`, tax line from ledger (usually 0), no disclaimer, buyer download hidden, people still say “invoice” in conversation.

Wave 4 is **tighten claims + stationery**, not a new document engine. Pair with Wave 2 LP-107/122 when those land; this ticket can ship the disclaimer **without** waiting if Wave 2 slips.

---

## 2. Current files

| Path | Role |
|------|------|
| `GatewayPaymentCompletedHandler` | B2C receipt + PDF |
| `ManualSubscriberEnrolledIntegrationEventHandler` | Official Receipt |
| `GenerateAndStoreDocumentCommandHandler` | Model + R2 + `DocumentPublished` |
| `InvoiceDocumentModel` / `BaseInvoiceDocument` | Title, TIN, tax row |
| `W2-LP-103-analysis.md` | Tax invoice is a different object |
| `W2-LP-107-analysis.md` | Branding / SST# / address |
| Portal download | `[MVP-HIDE]` |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | TIN `N/A` / “Lazuar Merchant” look official |
| G2 | Zero tax still a tax row (or unlabeled) |
| G3 | No “not an e-invoice” footer |
| G4 | Marketing “invoice” vs receipt |
| G5 | Buyer often only has email HMAC (Wave 2) — acceptable if email works |

---

## 4. Recommended model

```
CompanyName = profile.LegalName ?? workspace.Name ?? "Merchant"
CompanyTin  = print only if profile.Tin is non-blank
Tax row     = only if model.Tax > 0
Notes       = "This Official Receipt confirms payment. It is not a validated MyInvois tax invoice."
DocumentType stays "Official Receipt"
Number stays CustomerDocumentNumber (RCPT-), never LHDN UUID
```

Do not retitle to Tax Invoice here.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `GenerateAndStoreDocumentCommandHandler` | Fallbacks + empty TIN |
| `BaseInvoiceDocument` | Footer disclaimer; conditional tax |
| `InvoiceDocumentModel` | `ShowZeroTax` default false |
| README / `product-lines.md` | Receipt ≠ e-invoice |
| Tests | Snapshot or string assert footer; TIN omitted when null |

Must not: publish `InvoiceIssued`; submit MyInvois.

---

## 6. Tests

| Case | Expect |
|------|--------|
| No billing profile | Title Official Receipt; no `TIN: N/A`; no Lazuar-as-seller lie if workspace name exists |
| TaxAmount 0 | No SST line |
| TaxAmount > 0 | Tax row |
| Number | `RCPT-` prefix from sequence, not UUID |

---

## 7. Acceptance

1. Open a B2C PDF: it says Official Receipt and the disclaimer.  
2. A tenant without TIN does not print `TIN: N/A`.  
3. Docs do not call that PDF an e-invoice.  
4. Tax invoice remains Wave 2.

Tracker **P → Y** after 1–3. Stay **P** if only docs change or only PDF change.

---

## 8. Order

1. PDF fallbacks + footer + tax row  
2. One docs sentence  
3. Tests  

Can land before or after LP-107; do not block on LHDN.

Do **not** implement from this file.
