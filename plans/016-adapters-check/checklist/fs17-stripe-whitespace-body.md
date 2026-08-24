# fs17 — Stripe whitespace body is 400

**Track:** Fill Stripe · **Depends:** A00  
**Analysis:** 09 method 65; chip already uses `"  "`  
**Goal:** `PublicPayTests.Stripe_whitespace_webhook_is_400`

---

## fs17.1

- [ ] POST `/v1/webhooks/stripe/t1` content `" \n"`
- [ ] 400 `empty body`

## fs17.2 Exit

- [ ] Green
