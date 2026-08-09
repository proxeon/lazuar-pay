# R20 — TypeSpec dual DTO: products

**Track:** TypeSpec · **Analysis:** `../08-typespec-wave-b.md`  
**Files (verify):** `Commerce/.../ProductEndpoints.cs` local `CreateProductRequest` / `UpdateProductRequest`

---

## R20.1 Align types

- [ ] Confirm generated `CreateProductRequestDto` / `UpdateProductRequestDto` exist after gen
- [ ] Diff fields local vs generated; fix TypeSpec if gap
- [ ] `task gen` if TSP changed

## R20.2 Switch endpoints

- [ ] Bind Minimal API to generated types
- [ ] Map to commands (decimal/double ACL if needed)
- [ ] Delete local request records

## R20.3 Tests

- [ ] Product completeness / endpoint tests green
- [ ] Build Commerce + host

## R20.4 Exit

- [ ] No local product create/update DTOs remain
