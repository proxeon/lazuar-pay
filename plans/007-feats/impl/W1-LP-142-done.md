# W1-LP-142 — done

Public `POST /public/commerce/checkout` accepts optional `Idempotency-Key`. Same key + fingerprint (tenant, product, email, coupon, quantity, custom session id) replays the same gateway URL. Mismatch → **409**. Missing header keeps legacy new-session behavior. Portal stores a UUID in `sessionStorage` per product. CheckoutSessions gained IdempotencyKey / fingerprint / GatewayCheckoutUrl (unique org+key).

## Files

- `CommerceCheckoutIdempotency` + `InitiateCheckoutCommandHandler`
- Migration `20260818110000_AddCheckoutSessionIdempotency`
- Portal `submitCheckout` header
- `CommerceCheckoutIdempotencyTests`

## Tests run

- `CommerceCheckoutIdempotencyTests` — **passed**
- `CommerceProductCompletenessTests` — **passed**

Not committed. Not pushed.

Tracker `LP-142` **P → Y**.
