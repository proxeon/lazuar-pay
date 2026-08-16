# W2-LP-106 — Buyer document download

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 2 `LP-106`. Tracker: *Buyer download of documents* — Lazuar **B**. Inventory `INV-008`.  
**Not this ID:** Portal **history list** (`LP-175`). PDF branding (`LP-107`). Ops admin download (already on hidden panel). Quotation email (`LP-102` optional).

**Invariant:** A buyer who paid (or received a quote) can open a **time-limited HMAC (or magic-token) link** and get the PDF. The portal must not advertise “Download Tax Invoice” unless a stored document exists. The dead href `/api/billing/invoice?subscription=` must never ship.

---

## 0. Scope lock

In scope:

- Un-hide portal download **only** when a real URL exists
- Wire that URL to existing public billing HMAC (`GET /public/billing/{slug}/documents/{ledgerEntryId}`)
- Quote “Download PDF Quote” on restored `/pay/{id}` (`draft_pdf_url`)
- Keep email Official Receipt HMAC (already works)

Out of scope:

- Building `/api/billing/invoice` as a Next rewrite
- Listing every historical PDF (LP-175)
- Minting documents on GET (except drafts, already)
- Authenticated buyer JWT document API beyond magic token

---

## 1. Verdict

Download **as email** is shipped for Official Receipts. Download **as portal chrome** is a commented lie.

| Path | Works? |
|------|--------|
| `DocumentPublished` → Communications HMAC (30d) → public 302 to R2 | Yes, if template exists and email is configured |
| Draft quote HMAC (7d) on `CustomCheckoutDto.draft_pdf_url` | Yes, page 404s |
| Portal `<a href={`/api/billing/invoice?subscription=${sub.id}`}>` | **No such API.** Commented `[MVP-HIDE]` |
| Admin `GET /admin/billing/ledger/{id}/document` | Presign, no existence check |
| Tax Invoice / Credit Note email | `DocumentPublished` only mails `"Official Receipt"` and `"Draft Quotation"` — VALID regen does **not** email |

---

## 2. Current files

| Path | Role |
|------|------|
| `apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx` | Hidden dead tax-invoice anchor |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/PublicBillingEndpoints.cs` | Profile + final HMAC + draft HMAC |
| `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs` | Receipt / quotation email only |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/AdminLedgerEndpoints.cs` | Staff presign |
| `QuoteView.tsx` | Draft download if URL present |

Public final-document route is **intentionally not in TypeSpec** (honesty allowlist). Keep it that way; portal can still link it.

There is **no** `GET /public/commerce/{slug}/portal/documents`. LP-175 adds the list; this ticket needs **one working href per known ledger id**.

---

## 3. Exact gaps

### G1 — Hidden button points at fiction

Uncommenting as-is 404s. Worse than hidden.

### G2 — Portal has no ledger id

`GET /public/commerce/{tenantSlug}/portal` returns subscriptions (product, status, period). No `ledger_entry_id`, no HMAC. Cannot attach a real download without a new field or a new endpoint.

### G3 — Quotes cannot download

`/pay/{id}` 404 (LP-102). Draft URL is unused.

### G4 — Tax Invoice email missing

VALID regen publishes `DocumentPublished` with type `"Tax Invoice"` / `"Credit Note"`; communications **returns** (templateName null). Buyer never gets the legal PDF unless they have a portal link.

---

## 4. Recommended model

Smallest closed loop **without** waiting for LP-175:

1. Add `latest_receipt_url?` (or `document_url?`) on **each** portal subscription DTO — server builds the same HMAC as email, only if a ledger PDF key exists for that subscriber’s last `GATEWAY_PAYMENT`.
2. Portal: if URL present, show **Download receipt** (or **Download tax invoice** only when `lhdn_validation_status == VALID`). If absent, omit the control.
3. Restore quote draft button via LP-102.
4. Extend `DocumentPublished` mailer to `"Tax Invoice"` / `"Credit Note"` (reuse Official Receipt template or a thin twin).

Do **not** invent `/api/billing/invoice`. Do **not** proxy R2 through Next.

If lookup-by-subscription is messy (many payments), ship LP-175’s list first and this ticket is “render the links.” Either order is fine; do not ship the dead href.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| Commerce portal query + TypeSpec | Optional signed `document_url` + `document_label` |
| `portal/page.tsx` | Un-hide **only** if `document_url`; label from API |
| `DocumentPublishedIntegrationEventHandler` | Mail Tax Invoice / Credit Note |
| Quote page | LP-102 |

Must not: new Next API route; TypeSpec claim that public final PDF is a documented integrator API.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Portal DTO, no PDF | `document_url` null |
| After B2C receipt stored | HMAC verifies; 302 to R2 |
| Expired `exp` | 400 existing message |
| Bad `sig` | 401 |
| Tax Invoice published | Email dispatched (or documented skip if no template) |

---

## 7. Acceptance

1. Buyer with a magic link sees a download **only** when a PDF exists; the link opens a PDF.  
2. No `/api/billing/invoice` href.  
3. Quote draft download works once `/pay/{id}` is live.  
4. Receipt email still works.

Tracker **B → Y** when 1–3 are true. Email-only stays **B**.

---

Do **not** implement from this file.
