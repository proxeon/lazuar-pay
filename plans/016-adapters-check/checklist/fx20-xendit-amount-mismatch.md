# fx20 — Xendit amount mismatch does not pay

**Track:** Fill Xendit · **Depends:** D11, D17  
**Analysis:** 09 method 43  
**Goal:** `RailTests.Xendit_amount_mismatch_does_not_pay`

---

- [ ] `paid_amount: 9.99` vs checkout 10 (major units)
- [ ] **Do not** send `paid_amount: 1000` thinking Xendit is cents
- [ ] 400, zero documents, no event row
- [ ] Exit: green
