# fr14 — Failed then captured still pays

**Track:** Fill Razorpay · **Depends:** fr13, J15  
**Analysis:** 09 method 48; never bare `pay_`  
**Goal:** `RailTests.Razorpay_failed_then_captured_still_pays`

---

- [ ] Same `pay_1`: failed (event id `failed:pay_1` if no header), then captured **without** `X-Razorpay-Event-Id` so paid grain is `captured:pay_1`
- [ ] One `RCPT-`
- [ ] Exit: green
