# R22 — Broadcast preview/status contract honesty

**Track:** TypeSpec · **Analysis:** `../08-typespec-wave-b.md`  
**Problem:** Impl has preview/status routes not fully in TypeSpec (or reverse)

---

## R22.1 Decision

- [x] **A:** Add routes + models to TypeSpec and use generated types in endpoints  
- [ ] **B:** Remove/internalize routes if not product  
- [x] Choice: **A** (OrgAdmin product surface under `/admin/communications`)

## R22.2 Implement A or B

- [x] TSP + gen **or** remove endpoints/docs
- [x] Endpoints use `Lazuar.ApiTypes` if A
- [x] Clients committed if policy requires *(regenerated; commit with PR — no commit this pass)*

## R22.3 Tests

- [x] Broadcast tests green

## R22.4 Exit

- [x] No honesty gap for preview/status
