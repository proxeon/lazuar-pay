# R22 — Broadcast preview/status contract honesty

**Track:** TypeSpec · **Analysis:** `../08-typespec-wave-b.md`  
**Problem:** Impl has preview/status routes not fully in TypeSpec (or reverse)

---

## R22.1 Decision

- [ ] **A:** Add routes + models to TypeSpec and use generated types in endpoints  
- [ ] **B:** Remove/internalize routes if not product  
- [ ] Choice: ________

## R22.2 Implement A or B

- [ ] TSP + gen **or** remove endpoints/docs
- [ ] Endpoints use `Lazuar.ApiTypes` if A
- [ ] Clients committed if policy requires

## R22.3 Tests

- [ ] Broadcast tests green

## R22.4 Exit

- [ ] No honesty gap for preview/status
