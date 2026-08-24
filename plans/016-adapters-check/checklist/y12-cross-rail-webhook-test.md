# Y12 — CHIP checkout + Stripe webhook path is 400

**Track:** Webhook rail bind · **Depends:** Y10  
**Analysis:** H13 clone  
**IDs:** —  
**Goal:** Path `/v1/webhooks/stripe/{org}` cannot pay a `chip` checkout.

---

## Y12.1 Method `WebhookTests.Cross_rail_checkout_is_400`

- [ ] PUT chip + brand + PEM, PUT stripe keys too (leftover row)
- [ ] Start CHIP (FakePsp) so `checkout.Provider == chip`
- [ ] POST signed Stripe `checkout.session.completed` for that checkout id on `/v1/webhooks/stripe/t1`
- [ ] 400, zero documents, checkout still `open`

## Y12.2 Must not

- [ ] Do not skip because “signature would fail” — Stripe row exists so verify may succeed
- [ ] This is not `Cross_org_checkout_is_400` (that stays)

## Y12.3 Exit

- [ ] Green hermetic
