# fs14 — Rail not configured with non-empty body is 400

**Track:** Fill Stripe · **Depends:** A00  
**Analysis:** 09 method 5; P24. Empty body hits P23 first  
**Goal:** `WebhookTests.Rail_not_configured_is_400_when_body_present`

---

## fs14.1

- [ ] **No** PUT gateway
- [ ] POST `/v1/webhooks/stripe/t1` body `{"id":"evt_x"}` (non-empty)
- [ ] 400, body contains `rail not configured`

## fs14.2 Must not

- [ ] Do not use `""` (that is `Empty_webhook_is_400`)

## fs14.3 Exit

- [ ] Green
