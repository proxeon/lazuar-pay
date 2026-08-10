# S42 — Sample create-checkout route

**Track:** Sample app · **Analysis:** `../04-checkout-create-contract.md`  
**Depends on:** S41  
**Goal:** Server-only Hub M2M create + persist checkout ids.

---

## S42.1 Hub client

- [ ] `lib/hub.ts` or `lib/lazuar/createCheckout.ts`
- [ ] `POST {HUB}/integrations/payments/checkouts`
- [ ] Headers: `Authorization: Bearer ${sk}`, `Content-Type: application/json`
- [ ] Prefer header `Idempotency-Key` (e.g. `sample-order-{orderId}`)
- [ ] Body snake_case: amount, currency, description, customer_email, success_url, cancel_url, metadata
- [ ] metadata includes `order_id`, `type` (e.g. `sample_order`)
- [ ] success/cancel absolute URLs from `NEXT_PUBLIC_APP_URL` / env
- [ ] Parse success JSON: checkout_id, checkout_url, gateway, status, …
- [ ] Parse ProblemDetails: status/title/detail/code → typed error

## S42.2 Route handler

- [ ] `POST /api/checkout` (or Server Action rejected unless documented)
- [ ] Load/create local order first
- [ ] Call Hub createCheckout
- [ ] Persist hubCheckoutId + checkout_url + status `checkout_open`
- [ ] Return JSON `{ checkout_url, checkout_id, order_id }` **or** 303 redirect
- [ ] Never expose sk_ in response

## S42.3 Validation (client or server)

- [ ] amount > 0; MYR min awareness (e.g. ≥ 2)
- [ ] email basic validation
- [ ] currency 3-letter default MYR

## S42.4 Error UX map

- [ ] `PAYMENTS_NOT_CONFIGURED` → configure BYOK message
- [ ] `AMOUNT_*` / `CURRENCY_*` / `URLS_REQUIRED` / `METADATA_INVALID` / `INVALID_REQUEST`
- [ ] `IDEMPOTENCY_CONFLICT` → start new order
- [ ] `GATEWAY_ERROR` / network → retry guidance
- [ ] `UNAUTHORIZED` / `FORBIDDEN` → key/scope message

## S42.5 Verify

- [ ] With valid sk + BYOK: returns checkout_url
- [ ] Without BYOK: clear 422-style message
- [ ] Double-submit same idempotency key does not explode

## S42.6 Exit

- [ ] Grep sample: no `billplz.com` / `api.stripe.com` create calls
