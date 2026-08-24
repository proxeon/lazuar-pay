# fb22 — Billplz amount mismatch does not pay

**Track:** Fill Billplz · **Depends:** D12, D17  
**Analysis:** 09 method 31  
**Goal:** `RailTests.Billplz_amount_mismatch_does_not_pay`

---

- [ ] `paid_amount=999` vs checkout 10.00 (1000 sen). Valid HMAC
- [ ] 400, zero documents, no event row
- [ ] Exit: green
