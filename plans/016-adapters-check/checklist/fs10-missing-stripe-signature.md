# fs10 — Missing Stripe-Signature header is 400

**Track:** Fill Stripe · **Depends:** S12  
**Analysis:** 09 §10.1 method 1  
**Goal:** `WebhookTests.Missing_stripe_signature_header_is_400`

---

## fs10.1

- [ ] `Owner`, `SeedRailAndCheckout`
- [ ] POST `/v1/webhooks/stripe/t1` completed-session JSON **no** `Stripe-Signature`
- [ ] 400, `Documents.Count == 0`

## fs10.2 Must not

- [ ] Do not reuse `Invalid_signature_is_400` (that is bad v1, not missing header)

## fs10.3 Exit

- [ ] Green
