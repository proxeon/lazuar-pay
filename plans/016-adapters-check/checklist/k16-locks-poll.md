# K16 — Lock poll GET while verifying

**Track:** Checkout · **Depends:** K13  
**Analysis:** 09 method 70  
**IDs:** K13 015  
**Goal:** `it('polls public GET while verifying')` greps `/v1/pay/` inside interval.

---

- [ ] Source still has `setInterval` + GET
- [ ] Exit: green
