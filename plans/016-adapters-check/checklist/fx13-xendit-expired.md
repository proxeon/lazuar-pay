# fx13 — Xendit EXPIRED is ignored

**Track:** Fill Xendit · **Depends:** S15  
**Analysis:** 09 method 36; X17  
**Goal:** `RailTests.Xendit_expired_is_ignored`

---

- [ ] Valid token, `"status":"EXPIRED"`
- [ ] 200, ignored/EXPIRED, zero documents
- [ ] Must not: fulfill SETTLED/EXPIRED as Hub did for SETTLED
- [ ] Exit: green
