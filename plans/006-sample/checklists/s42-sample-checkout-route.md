# S42 — Sample create-checkout route

**Track:** Sample app · **Analysis:** `../04-checkout-create-contract.md`  
**Depends on:** S41  
**Goal:** Server-only Hub M2M create + persist checkout ids.

---

## S42.1 Hub client

- [x] `lib/hub.ts` or `lib/lazuar/createCheckout.ts`
- [x] `POST {HUB}/integrations/payments/checkouts`
- [x] Headers: `Authorization: Bearer ${sk}`, `Content-Type: application/json`
- [x] Prefer header `Idempotency-Key` (e.g. `sample-order-{orderId}`)
- [x] Body snake_case: amount, currency, description, customer_email, success_url, cancel_url, metadata
- [x] metadata includes `order_id`, `type` (e.g. `sample_order`)
- [x] success/cancel absolute URLs from `NEXT_PUBLIC_APP_URL` / env
- [x] Parse success JSON: checkout_id, checkout_url, gateway, status, …
- [x] Parse ProblemDetails: status/title/detail/code → typed error

## S42.2 Route handler

- [x] `POST /api/checkout` (or Server Action rejected unless documented)
- [x] Load/create local order first
- [x] Call Hub createCheckout
- [x] Persist hubCheckoutId + checkout_url + status `checkout_open`
- [x] Return JSON `{ checkout_url, checkout_id, order_id }` **or** 303 redirect
- [x] Never expose sk_ in response

## S42.3 Validation (client or server)

- [x] amount > 0; MYR min awareness (e.g. ≥ 2)
- [x] email basic validation
- [x] currency 3-letter default MYR

## S42.4 Error UX map

- [x] `PAYMENTS_NOT_CONFIGURED` → configure BYOK message
- [x] `AMOUNT_*` / `CURRENCY_*` / `URLS_REQUIRED` / `METADATA_INVALID` / `INVALID_REQUEST`
- [x] `IDEMPOTENCY_CONFLICT` → start new order
- [x] `GATEWAY_ERROR` / network → retry guidance
- [x] `UNAUTHORIZED` / `FORBIDDEN` → key/scope message

## S42.5 Verify

- [x] With valid sk + BYOK: returns checkout_url
- [x] Without BYOK: clear 422-style message
- [x] Double-submit same idempotency key does not explode

## S42.6 Exit

- [x] Grep sample: no `billplz.com` / `api.stripe.com` create calls
