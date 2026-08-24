# fb23 — Join via reference_1 when query missing

**Track:** Fill Billplz · **Depends:** S14  
**Analysis:** 09 method 32; B16/B17  
**Goal:** `RailTests.Billplz_join_via_reference_1_when_query_missing`

---

- [ ] No `?checkout_id=` query. Form `reference_1={checkoutId}` + paid HMAC
- [ ] 200, one `RCPT-`
- [ ] Live parser: query, then form `checkout_id`, then `reference_1`
- [ ] Exit: green
