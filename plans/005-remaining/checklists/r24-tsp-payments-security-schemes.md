# R24 — Payments OpenAPI security schemes

**Track:** TypeSpec · **Analysis:** `../08-typespec-wave-b.md`  
**Problem:** Payments docs package may lack auth schemes while routes require auth  
**Notes:** `../r24-notes.md`

---

## R24.1 Fix

- [x] Add `@useAuth` / security to payments docs TSP (mirror LHDN/one pattern)
- [x] Rebuild docs OpenAPI via gen
- [x] Spot-check `dist/payments` or docs output has securitySchemes

## R24.2 Exit

- [x] Authenticated payments routes documented with security
