---
number: "031"
id: B01-C05
severity: P1
status: open
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
---

# 031 — B01-C05 — Custom quote remints hop-2 every time; portal key is per slug not per quote

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C05 — Custom quote remints hop-2 every time; portal key is per slug not per quote

**Severity:** P1  
**One-sentence fault:** The custom branch never writes idempotency onto the quote session and always calls `GenerateCheckoutSessionQuery` again, so retries mint a second live processor session; the portal key `lazuar-checkout-idem:{tenant}:custom` also collides product checkout in the same tab.

**Evidence.** Custom branch (§4.1) has no `SetIdempotency`, no “if `GatewayCheckoutUrl` already set, return it”. Portal:

```34:45:apps/lazuar-portal/src/modules/checkout/lib/api.ts
function checkoutIdempotencyKey(tenantSlug: string, productSlug: string) {
  const storageKey = `lazuar-checkout-idem:${tenantSlug}:${productSlug}`;
  // random UUID persisted in sessionStorage
}
```

`QuoteView.handleProceedToPayment` posts `product_slug: "custom"` and `session_id`. `CheckoutForm` posts the real slug. After a product initiate in that tab, the key exists on a product session. A later quote pay sends the same header with a different fingerprint (`session_id` / slug) → `IDEMPOTENCY_CONFLICT`. Changing quantity or coupon on the product form after the first 200 also 409s; the form has no recovery other than `onError(err.message)`.

**Reproduction in words.** Buyer double-clicks “Pay” on a quote. Two Stripe Checkout sessions are created for the same OPEN quote. Buyer pays the first link in one window and the second in another. First webhook completes the session. Second webhook sees COMPLETED and no-ops. Merchant has two processor captures, one Commerce completion. Ledger (out of slice) may journal both if Payments emits two events with different gateway transaction ids.

Same tab: buy a product, then open a quote, click pay → 409 `IDEMPOTENCY_CONFLICT`.

**Blast radius.** Every custom quote. Double-click is the default browser behaviour. Two live processor URLs for one Commerce session is a money duplicate, not a polish issue.

**Why tests missed it.** `InitiateCheckout_CustomSession_StillSendsLineSumAndQuantityOne` and the B2B custom tests stub a single `GenerateCheckoutSessionQuery`. No second call. No header.

**Fix direction.** If the quote is OPEN and already has a URL and is not expired, return it. Persist the idempotency key on the quote session on first mint. Portal key must include `session_id` (and email / qty / coupon / interval / price_id for product). Rotate the key on 409.

---

