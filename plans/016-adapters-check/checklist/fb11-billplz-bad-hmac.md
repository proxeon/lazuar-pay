# fb11 — Billplz bad HMAC is 400

**Track:** Fill Billplz · **Depends:** S14  
**Analysis:** 09 method 20  
**Goal:** `RailTests.Billplz_bad_hmac_is_400`

---

- [ ] Valid form `paid=true` with `x_signature=deadbeef`
- [ ] 400, zero documents
- [ ] Exit: green
