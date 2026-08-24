# fb10 — Billplz empty body 400

**Track:** Fill Billplz · **Depends:** S14  
**Analysis:** 09 method 19; P23  
**Goal:** `RailTests.Billplz_empty_body_400`

---

- [ ] PUT billplz collection+env
- [ ] POST `/v1/webhooks/billplz/t1` content `"  "` `application/x-www-form-urlencoded`
- [ ] 400
- [ ] Exit: green
