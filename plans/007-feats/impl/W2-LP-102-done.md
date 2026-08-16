# W2-LP-102 — done

Quotes / proforma is a live payment request again. Ops Invoicing → Quotes is routed. Buyer `/pay/{id}` is restored (no `notFound()`). Custom success `/checkout/custom/success` polls until session `COMPLETED`. Initiate with `session_id` stamps `is_b2b_required` on gateway metadata; B2B quotes collect TIN on QuoteView and persist it to CRM. Copy is **proforma / payment request**, not tax invoice / LHDN.

## Files

- Ops `App.tsx` + `Sidebar` Invoicing → Quotes
- Portal `pay/[sessionId]/page.tsx` + `checkout/custom/success/page.tsx`
- `QuoteView` branding (workspace) vs legal profile only when B2B; TIN fields when required
- `InitiateCheckoutCommandHandler` session branch metadata + TIN + success URL

## Tests run

- `CreateCustomCheckoutAndInitiateSessionTests` — **passed**
- `npx tsc --noEmit` portal + ops — **clean**

Not committed. Not pushed.

Tracker `LP-102` can move **B → Y**.
