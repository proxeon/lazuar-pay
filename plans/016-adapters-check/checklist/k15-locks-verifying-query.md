# K15 — Lock verifying query is not paid

**Track:** Checkout · **Depends:** K13  
**Analysis:** 09 §10.9 method 69  
**IDs:** K14 015  
**Goal:** `locks.test.ts` greps `status === 'verifying'` and paid UI gated on `pay.status === 'paid'`.

---

- [ ] `it('verifying query is not paid')`
- [ ] Exit: green
