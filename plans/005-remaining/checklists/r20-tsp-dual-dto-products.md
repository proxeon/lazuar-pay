# R20 — TypeSpec dual DTO: products

**Track:** TypeSpec · **Analysis:** `../08-typespec-wave-b.md`  
**Files (verify):** `Commerce/.../ProductEndpoints.cs` local `CreateProductRequest` / `UpdateProductRequest`

---

## R20.1 Align types

- [x] Confirm generated `CreateProductRequestDto` / `UpdateProductRequestDto` exist after gen
- [x] Diff fields local vs generated; fix TypeSpec if gap
- [x] `task gen` if TSP changed *(N/A — shapes already match; no TSP edit)*

## R20.2 Switch endpoints

- [x] Bind Minimal API to generated types
- [x] Map to commands (decimal/double ACL if needed)
- [x] Delete local request records

## R20.3 Tests

- [x] Product completeness / endpoint tests green
- [x] Build Commerce + host

## R20.4 Exit

- [x] No local product create/update DTOs remain
