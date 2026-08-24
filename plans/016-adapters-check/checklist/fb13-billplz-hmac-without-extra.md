# fb13 — Billplz HMAC without extra (dual compute)

**Track:** Fill Billplz · **Depends:** fb12  
**Analysis:** 09 method 22; Hub with-extra first **fails**, without-extra **passes**  
**Goal:** `RailTests.Billplz_hmac_without_extra_fields_paid`

---

- [ ] Same extra fields **present** in the form
- [ ] Signature computed with `excludeExtra: true`
- [ ] 200, one `RCPT-`
- [ ] **Fresh** checkout so it does not collide with fb12
- [ ] Exit: green
