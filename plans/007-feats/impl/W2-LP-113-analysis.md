# W2-LP-113 — LHDN QR on validated invoice

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 2 `LP-113`. Tracker: *LHDN QR on validated invoice* — Lazuar **B**. Alias `LP-TAX-004`, `INV-024`.  
**Not this ID:** Poll (`LP-111`). Buyer download (`LP-106`). Checkout branding QR / DuitNow (`LP-033`). HitPay “QR to pay the invoice”.

**Invariant:** After MyInvois `VALID`, the customer PDF and ops detail show a QR whose payload is `{portal}/{uuid}/share/{longId}`. No QR on Official Receipts that were never submitted. No QR on `PENDING` / `INVALID`.

---

## 0. Scope lock

In scope:

- Keep `ILhdnLinkService` + poller `qr_link` + QuestPDF `PngByteQRCode`
- Fix the VALID → ledger → `GenerateAndStoreDocument(..., QrLink)` path (same join as LP-111)
- Show QR / UUID on remounted TaxInvoiceDetailPanel
- Portal/email PDF already embeds QR once the handler passes `LhdnQrLink`

Out of scope:

- Custom QR styling
- Deep-link into Aura
- Printing QR on hop-1 checkout

---

## 1. Verdict

QR **generation** exists. QR **never reaches** the live B2C/B2B PDF because Billing cannot find the ledger row (LP-111 G1). Webhook payload already includes `qr_link` for integrators.

`BaseInvoiceDocument` footer draws QR when `LhdnQrLink` set. `LhdnUuid` printed when status is VALID. Official Receipts generated at pay time have neither (correct).

Default portal URL: `https://preprod.myinvois.hasil.gov.my` via `Lhdn:PortalUrl`. Prod must set the production share host or the QR points at preprod.

---

## 2. Current files

| Path | Role |
|------|------|
| `LhdnLinkService.cs` | Portal base |
| `LhdnStatusPollingJob.cs` | Builds `qrLink` on VALID |
| `GetLhdnDocumentStatusQueryHandler` | `Qr_link` on GET |
| `LhdnDocumentValidatedIntegrationEvent` | `QrLink` |
| `GenerateAndStoreDocumentCommandHandler` | `model.LhdnQrLink = request.LhdnQrLink` |
| `BaseInvoiceDocument.ComposeFooter` | QRCoder |
| Ops panel | No QR image; UUID only if `tax_invoice_id` looks like one |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | VALID handler miss → no regen → no QR on PDF |
| G2 | Ops panel has no QR |
| G3 | `Lhdn:PortalUrl` default preprod |
| G4 | `UpdateLhdnStatus` copies UUID into `TaxInvoiceId` — display confusion (number vs UUID) |
| G5 | Portal download hidden (LP-106) so buyer never sees the PDF QR |

---

## 4. Recommended model

Same as today, after LP-111 join fix:

```
VALID → UpdateLhdnStatus + GenerateAndStore(docType, qrLink)
PDF footer QR + UUID line
Ops: <img> or printed URL from GET qr_link
```

Do not invent a second QR format. Do not QR the Official Receipt until that sale is individually validated (rare). Consolidated B2C: QR belongs on the **consolidated** document, not on every RM 12 receipt (unless you regenerate each receipt — don’t).

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| VALID handler | Join fix (owned with LP-111; this ticket verifies QR argument is passed) |
| `TaxInvoiceDetailPanel` | Render `qr_link` from LHDN GET |
| Config / ops copy | Production portal URL checklist |
| Tests | Model `LhdnQrLink` set on VALID fixture |

Must not: QR on unverified receipts; DuitNow QR.

---

## 6. Tests

| Case | Expect |
|------|--------|
| VALID with uuid+longId | Event `QrLink` contains `/share/` |
| GenerateAndStore with QrLink | PDF bytes non-empty (smoke) |
| GET document VALID | `qr_link` non-null |
| GET PENDING | `qr_link` null |

---

## 7. Acceptance

1. Sandbox VALID invoice PDF shows a scannable MyInvois share QR.  
2. Ops detail shows the same URL/QR.  
3. Pre-VALID PDFs have no QR.  
4. Prod config uses production portal host.

Tracker **B → Y** when 1–2 are true. Webhook-only stays **B**.

---

Do **not** implement from this file.
