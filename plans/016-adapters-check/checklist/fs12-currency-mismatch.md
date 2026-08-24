# fs12 — Currency mismatch does not mint receipt

**Track:** Fill Stripe · **Depends:** D16  
**Analysis:** 09 §10.1 method 3  
**Goal:** `WebhookTests.Currency_mismatch_does_not_mint_receipt`

---

## fs12.1

- [ ] Checkout MYR. Signed session `currency: usd`, `amount_total:1000`
- [ ] 400, zero documents, checkout `open`, **no** event row

## fs12.2 Must not

- [ ] Do not default webhook currency to MYR

## fs12.3 Exit

- [ ] Green
