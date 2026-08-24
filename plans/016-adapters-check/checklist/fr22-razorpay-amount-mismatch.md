# fr22 — Razorpay amount mismatch does not pay

**Track:** Fill Razorpay · **Depends:** D14, D17  
**Analysis:** 09 method 56  
**Goal:** `RailTests.Razorpay_amount_mismatch_does_not_pay`

---

- [ ] Entity `amount: 999` (already minor) vs checkout 10
- [ ] 400, zero documents, no event row
- [ ] Exit: green
