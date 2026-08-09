# R24 — Payments OpenAPI security schemes

**Track:** TypeSpec · **Analysis:** `../08-typespec-wave-b.md`  
**Problem:** Payments docs package may lack auth schemes while routes require auth

---

## R24.1 Fix

- [ ] Add `@useAuth` / security to payments docs TSP (mirror LHDN/one pattern)
- [ ] Rebuild docs OpenAPI via gen
- [ ] Spot-check `dist/payments` or docs output has securitySchemes

## R24.2 Exit

- [ ] Authenticated payments routes documented with security
