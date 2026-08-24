# fs13 — Unknown Stripe type is ignored

**Track:** Fill Stripe · **Depends:** A00  
**Analysis:** 09 method 4; NP-XX-012  
**Goal:** `WebhookTests.Unknown_event_type_is_ignored`

---

## fs13.1

- [ ] Signed `type: customer.subscription.updated` (Stripe.net must parse)
- [ ] 200, body `ignored`, zero documents, checkout `open`

## fs13.2 Must not

- [ ] Do not fulfill refunds / Billing
- [ ] Do not use `charge.refunded` if Stripe.net rejects the fixture — pick a type ConstructEvent accepts

## fs13.3 Exit

- [ ] Green
