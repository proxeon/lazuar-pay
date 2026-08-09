# Phase 05 — TypeSpec ↔ API contract honesty

**Goal:** Generated contracts match Minimal API; kill dual DTO sources for public/admin surfaces.  
**Evidence:** `../05-typespec-contracts.md`  
**PR shape:** May split into several PRs (payments slash, commerce DTOs, broadcast fields, regen).

---

## 05.1 Baseline

- [ ] Run `task gen` (or documented gen pipeline) on clean tree
- [ ] Note dirty clients; start from clean gen if possible
- [ ] Skim gap residual list in `05-typespec-contracts.md` for P0 items

## 05.2 P0 — Dual DTOs (local C# vs generated)

### Commerce subscribers

- [ ] Find local subscriber DTO records in Commerce endpoints/services
- [ ] Map each field to TypeSpec models in `modules/commerce`
- [ ] Switch endpoints/handlers to `Lazuar.ApiTypes` / generated types **or** fix TypeSpec then regen
- [ ] Delete local DTO types if unused
- [ ] Tests still compile and pass

### Payments integration checkout

- [ ] Find local integration checkout request/response records
- [ ] Align with TypeSpec payments models/routes
- [ ] Switch to generated types
- [ ] Delete locals
- [ ] Tests pass

## 05.3 P0 — Broadcast targeting fields

- [ ] Compare TypeSpec communications broadcast models vs `Map`/endpoint body binding
- [ ] Either implement missing targeting fields in endpoint **or** remove from TypeSpec if not product
- [ ] Regen clients
- [ ] Test create/update broadcast with targeting

## 05.4 P0 — Payments OpenAPI path trailing slash

- [ ] Locate `/checkouts/` vs `/checkouts` mismatch in TSP routes vs Minimal API
- [ ] Pick one canonical form (prefer no trailing slash unless gateway requires)
- [ ] Align TypeSpec + endpoint Map
- [ ] Regen; grep OpenAPI yaml for consistency

## 05.5 P1 — Impl-only vs TSP-only inventory (decide each)

For each item, mark **implement | remove from API | document as internal**:

- [ ] Billing signed PDF download (if impl-only)
- [ ] Broadcast preview/status (if TSP-only or impl-only)
- [ ] Communications public compliance routes (if any)
- [ ] Other items listed in `05-typespec-contracts.md` § residual tables
- [ ] Execute decisions in code/TSP (may span PRs)

## 05.6 P1 — Docs security schemes

- [ ] Ensure payments product docs OpenAPI includes security schemes where routes need auth
- [ ] Align ops/one docs packages if missing schemes
- [ ] Rebuild docs OpenAPI outputs via gen

## 05.7 Dead gen / package hygiene (if not done in Phase 01)

- [ ] Confirm `Generated/Models.cs` gone
- [ ] Confirm `api-types-dotnet` builds single output file only

## 05.8 Optional CI honesty gate (can be Phase 06)

- [ ] Design check: OpenAPI paths ⊆ or = Minimal API route list (script or test)
- [ ] Or document manual honesty review until automated
- [ ] Prefer automated later; not blocking 05.2–05.4

## 05.9 Verification

- [ ] `task gen` clean (or committed clients match gen)
- [ ] `dotnet build` solution
- [ ] Relevant ModuleTests for commerce/payments/comms
- [ ] Spot-check Scalar/developers hub if local

## 05.10 Exit criteria

- [ ] No known dual DTO pairs for listed P0 surfaces
- [ ] Broadcast targeting honest
- [ ] Checkout path slash consistent
- [ ] Gen pipeline green
