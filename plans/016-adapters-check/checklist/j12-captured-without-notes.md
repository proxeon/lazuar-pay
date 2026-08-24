# J12 — Captured without notes and without plink match is 400

**Track:** Razorpay join · **Depends:** J11  
**Analysis:** P0-C; live null CheckoutId → `"checkout not found"`  
**IDs:** —  
**Goal:** Never silent-pay a random open checkout.

---

## J12.1

- [ ] `payment.captured`, good HMAC, no `notes.checkout_id`, no matching `ProviderSessionId` → 400 `"checkout not found"`
- [ ] Zero documents
- [ ] **No** unique insert (same as other 400-before-insert paths)

## J12.2 Must not

- [ ] Do not pick the latest open checkout for the org
- [ ] Do not fulfill `order.paid` as a workaround (J14)

## J12.3 Exit

- [ ] J16 method
