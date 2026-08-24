# fb14 — Billplz unpaid is ignored

**Track:** Fill Billplz · **Depends:** S14  
**Analysis:** 09 method 23; B21  
**Goal:** `RailTests.Billplz_unpaid_is_ignored`

---

- [ ] Valid HMAC, `paid=false`, `state=due`
- [ ] 200, body contains `unpaid`, zero documents, checkout `open`
- [ ] Must not: Hub `PAYMENT_FAILED` name
- [ ] Exit: green
