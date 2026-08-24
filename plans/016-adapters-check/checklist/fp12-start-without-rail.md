# fp12 — Start without rail is 503

**Track:** Fill public · **Depends:** A00  
**Analysis:** 09 method 64; P24.2  
**Goal:** `PublicPayTests.Start_without_rail_is_503`

---

- [ ] Create checkout, **no** PUT gateway
- [ ] POST start `{"email":"ada@acme.test"}`
- [ ] 503 `rail not configured`
- [ ] Exit: green
