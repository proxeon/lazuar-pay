# W2-LP-102 — Quotes / proforma / custom checkout

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 2 `LP-102`. Tracker: *Quotes / proforma / custom checkout session* — Lazuar **B**. Aliases `LP-TAX-007`, `INV-001`–`004`, `INV-016`.  
**Not this ID:** Net-30 AR invoices (`LP-105` Wave 3). Sequential quote numbers (`LP-101`). TIN validate (`LP-112`). MyInvois submit (`LP-110`). Buyer document history (`LP-175`).

**Invariant:** A merchant can create an ad-hoc payment request, copy a **live** buyer URL, the buyer can see a proforma and pay (or staff can mark paid). That is a **payment request**, not a Xero quote state machine.

---

## 0. Scope lock

In scope:

- Remount ops `/invoicing/quotes` + sidebar
- Restore portal `/{tenant}/pay/{sessionId}` (remove `notFound()`)
- Create / list / copy link / mark-paid already on the API
- Draft HMAC PDF already on public GET
- Stamp `is_b2b_required` into **payment metadata** (today it dies on the session row)
- Custom success page that polls like LP-024 (session `COMPLETED` only)

Out of scope:

- Accepted / declined / revised quote workflow
- Due dates, reminders, partial pay (`LP-105`)
- Tax lines on quote (`LP-118`)
- Email “Quotation Ready” (template exists; optional same ticket if `DocumentPublished` is cheap)
- Self-billed (`LP-115`)

---

## 1. Verdict

**Un-hide is 80% of the merchant job. Closed loop is not.** Backend custom checkout is real. The pay URL 404s. B2B checkbox does not reach Billing. Custom success URL has **no portal route**.

| Surface | Status |
|---------|--------|
| `POST/GET /admin/commerce/custom-checkouts` | Live |
| `POST /admin/commerce/checkouts/{id}/mark-paid` | Live |
| `GET /public/commerce/{slug}/custom-checkouts/{id}` + `draft_pdf_url` | Live |
| `QuotesPage` / `CreateQuoteModal` / `QuoteDetailPanel` | **Unrouted**, not imported in `App.tsx` |
| Sidebar Invoicing | **Absent** (`MODULES` = commerce / developer / workspace) |
| `pay/[sessionId]/page.tsx` | `notFound()` |
| `QuoteView.tsx` | Complete, unreachable |
| Custom `successUrl` | `/{slug}/checkout/custom/success?sub_id=` — **no such page** |

---

## 2. Current files

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/App.tsx` | `[MVP-HIDE]` routes; invoicing pages **not imported** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/invoicing/pages/QuotesPage.tsx` | List + create |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/invoicing/components/CreateQuoteModal.tsx` | Lines, expiry, `is_b2b_required` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/invoicing/components/QuoteDetailPanel.tsx` | Copies `VITE_PORTAL_URL/{slug}/pay/{id}`; mark-paid |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/pay/[sessionId]/page.tsx` | Forced 404 |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/QuoteView.tsx` | Proforma HTML; “TIN collected at checkout”; pay via `product_slug: "custom"` + `session_id` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Commands/CreateCustomCheckoutCommandHandler.cs` | CRM upsert (name/email only); 30-day default expiry |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` | `SessionId` branch: hop-2 URL; metadata **without** `is_b2b_required` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicCustomCheckoutEndpoints.cs` | HMAC draft URL (7 days) |

`QuoteView.handleProceedToPayment` does **not** send `tax_id` / `company_name`. Copy promises TIN at checkout; product checkout TIN is still hidden (LP-022). Custom initiate **skips** `EnforceCheckoutConfiguration`.

Mark-paid toast: “Official receipt generation triggered.” Offline custom path books B2C receipt regardless of `IsB2bRequired` (Commerce mark-paid → billing enroll/offline handlers — confirm they stay B2C). Do not claim LHDN on mark-paid.

---

## 3. Exact gaps

### G1 — Route + nav lobotomy

`App.tsx` comment block is not enough: pages are not imported. Sidebar has no Invoicing module. `*` redirect sends `/invoicing/quotes` to dashboard even if someone types the URL.

### G2 — Buyer URL is a 404

Ops copies a link that `notFound()`s. This is the entire product.

### G3 — Custom success is a 404

After pay, Billplz/Stripe return to `/{tenant}/checkout/custom/success`. No `app/.../checkout/custom/` tree. LP-024 poller never runs. Buyer who paid sees Next 404 (fail-closed, but ugly). Reuse product success poller or add a thin custom success page keyed on `sub_id`.

### G4 — `is_b2b_required` is a landmine

Stored on the session. **Not** on gateway metadata. Billing always B2C → receipt + consolidation. QuoteView tells the buyer TIN will be collected; it is not. If LP-022 is not done, **force the checkbox off** or collect TIN **on QuoteView** before hop-2.

### G5 — No quotation email

`DocumentPublished` type `"Draft Quotation"` would send “Quotation Ready”. Create path never publishes. Merchants must copy the link. Acceptable for v1 if the URL works.

### G6 — Public billing profile on QuoteView

Uncommented page fetches `GET /public/billing/{slug}/profile` (full TIN + address). That is legal-plane leakage on a payment link (same concern as LP-025). Prefer workspace branding (`LP-025`) for the mark; show legal name/TIN only if a billing profile exists **and** this is a B2B quote.

---

## 4. Recommended model

```
ops Invoicing → Quotes
  POST /admin/commerce/custom-checkouts
  copy {portal}/{slug}/pay/{id}

portal /pay/{id}
  GET custom-checkout + optional branding
  QuoteView: proforma + draft PDF + Pay
  Pay → POST public checkout { session_id } → gateway
  success → poll GET …/checkout/{id}/status until COMPLETED
```

Copy everywhere: **Proforma / payment request**, not “tax invoice”.

B2B: either (a) LP-022 fields on QuoteView when `is_b2b_required`, or (b) hide the checkbox until LP-022 ships. Do not ship the checkbox as a no-op.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `lazuar-ops/src/App.tsx` | Import + route `/invoicing/quotes` |
| `Sidebar.tsx` | New **Invoicing** module: Quotes (Tax Invoices / Credit Notes on 103/104) |
| `pay/[sessionId]/page.tsx` | Restore the commented fetch + `QuoteView` |
| New `checkout/custom/success/page.tsx` **or** redirect to a shared poller | `COMPLETED` only (LP-024) |
| `InitiateCheckoutCommandHandler` SessionId branch | Metadata `is_b2b_required`; success URL that exists |
| `QuoteView.tsx` | Collect TIN if B2B (or wait for LP-022); do not call product checkout TIN-less |
| Optional | `DocumentPublished("Draft Quotation")` after create |

Must not: build accept/decline; due dates; AR aging.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Public GET unknown / other tenant | 404 |
| Public GET OPEN | 200 + `draft_pdf_url` HMAC |
| Initiate with `session_id` | Gateway URL; metadata includes `is_b2b_required` matching session |
| Initiate completed session | 400 existing message |
| Mark-paid OPEN custom | `COMPLETED`; one receipt **if** not B2B |
| Status poll | `COMPLETED` only after mark-paid / webhook |

Manual: copy link → proforma → pay sandbox → success after webhook, not on landing.

---

## 7. Acceptance

1. Sidebar **Quotes** works. Create lines → copy URL → buyer sees proforma (not 404).  
2. Pay or mark-paid completes the session. Success UI waits for `COMPLETED`.  
3. Draft PDF downloads via HMAC.  
4. Copy never says LHDN / tax invoice.  
5. If B2B checkbox is visible, TIN is collected and metadata is stamped; otherwise the checkbox is hidden.

Tracker **B → Y** when 1–4 are demoable. Stay **B** if only ops is remounted and `/pay/{id}` still 404s.

---

## 8. Suggested implement order

1. Ops route + sidebar + imports  
2. Restore `/pay/{id}`  
3. Fix success URL + poller  
4. Metadata + TIN policy (with or after LP-022)  
5. Optional quotation email  

Do **not** implement from this file.
