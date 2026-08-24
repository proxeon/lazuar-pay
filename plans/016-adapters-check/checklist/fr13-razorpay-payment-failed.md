# fr13 — Razorpay payment.failed is ignored

**Track:** Fill Razorpay · **Depends:** S16  
**Analysis:** 09 method 47; R18  
**Goal:** `RailTests.Razorpay_payment_failed_is_ignored`

---

- [ ] `"event":"payment.failed"`, valid HMAC, notes checkout_id
- [ ] 200, body contains `payment_failed` or ignored, zero documents, checkout `open`
- [ ] Exit: green
