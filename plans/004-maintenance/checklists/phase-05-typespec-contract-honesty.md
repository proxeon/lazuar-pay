# Phase 05 — TypeSpec ↔ API contract honesty

**Goal:** Generated contracts match Minimal API; kill dual DTO sources for public/admin surfaces.  
**Evidence:** `../05-typespec-contracts.md`  
**PR shape:** May split into several PRs (payments slash, commerce DTOs, broadcast fields, regen).  
**Status:** ✅ P0 done (2026-08-09); P1/optional deferred — see `phase-05-done.md`

---

## 05.1 Baseline

- [x] Run `task gen` (or documented gen pipeline) on clean tree
- [x] Note dirty clients; start from clean gen if possible
  - Clients were stale on payments checkout schemas/paths; regen brought them in
- [x] Skim gap residual list in `05-typespec-contracts.md` for P0 items

## 05.2 P0 — Dual DTOs (local C# vs generated)

### Commerce subscribers

- [x] Find local subscriber DTO records in Commerce endpoints/services
- [x] Map each field to TypeSpec models in `modules/commerce`
- [x] Switch endpoints/handlers to `Lazuar.ApiTypes` / generated types **or** fix TypeSpec then regen
- [x] Delete local DTO types if unused
- [x] Tests still compile and pass

### Payments integration checkout

- [x] Find local integration checkout request/response records
- [x] Align with TypeSpec payments models/routes
- [x] Switch to generated types
- [x] Delete locals
- [x] Tests pass

## 05.3 P0 — Broadcast targeting fields

- [x] Compare TypeSpec communications broadcast models vs `Map`/endpoint body binding
- [x] Either implement missing targeting fields in endpoint **or** remove from TypeSpec if not product
  - **Chose remove:** fields were phantom (endpoint dropped, handler/fan-out ignored, no storage)
- [x] Regen clients
- [x] Test create/update broadcast with targeting
  - N/A after removal; existing Broadcast* module tests still pass

## 05.4 P0 — Payments OpenAPI path trailing slash

- [x] Locate `/checkouts/` vs `/checkouts` mismatch in TSP routes vs Minimal API
- [x] Pick one canonical form (prefer no trailing slash unless gateway requires)
- [x] Align TypeSpec + endpoint Map
- [x] Regen; grep OpenAPI yaml for consistency
  - Also added optional `Idempotency-Key` header on create

## 05.5 P1 — Impl-only vs TSP-only inventory (decide each)

For each item, mark **implement | remove from API | document as internal**:

- [x] Billing signed PDF download (if impl-only) → **document as internal** (deferred implement)
- [x] Broadcast preview/status (if TSP-only or impl-only) → **document as internal** (deferred TSP)
- [x] Communications public compliance routes (if any) → **document as internal**
- [x] Other items listed in `05-typespec-contracts.md` § residual tables → see `phase-05-analysis.md` §5
- [ ] Execute decisions in code/TSP (may span PRs) — **deferred Wave B** except P0 removals already done

## 05.6 P1 — Docs security schemes

- [ ] Ensure payments product docs OpenAPI includes security schemes where routes need auth
- [ ] Align ops/one docs packages if missing schemes
- [ ] Rebuild docs OpenAPI outputs via gen
- **Deferred** to Wave B / later PR

## 05.7 Dead gen / package hygiene (if not done in Phase 01)

- [x] Confirm `Generated/Models.cs` gone (Phase 01)
- [x] Confirm `api-types-dotnet` builds single output file only

## 05.8 Optional CI honesty gate (can be Phase 06)

- [ ] Design check: OpenAPI paths ⊆ or = Minimal API route list (script or test)
- [x] Or document manual honesty review until automated
  - Documented in `phase-05-analysis.md` / deferred to Phase 06
- [ ] Prefer automated later; not blocking 05.2–05.4

## 05.9 Verification

- [x] `task gen` clean (or committed clients match gen)
- [x] `dotnet build` solution (Lazuar.Api)
- [x] Relevant ModuleTests for commerce/payments/comms (40 focused tests passed)
- [ ] Spot-check Scalar/developers hub if local — **not run** (needs local hub + rebuilt dist in image)

## 05.10 Exit criteria

- [x] No known dual DTO pairs for listed P0 surfaces (subscribers + integration checkouts)
- [x] Broadcast targeting honest (removed until productized)
- [x] Checkout path slash consistent
- [x] Gen pipeline green
