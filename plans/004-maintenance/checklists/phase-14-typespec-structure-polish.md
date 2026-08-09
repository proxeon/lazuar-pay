# Phase 14 — TypeSpec structure polish

**Goal:** Large TSP files become navigable; orphans gone or justified.  
**Depends on:** Phase 05 honesty first (structure without honesty wastes work).  
**Evidence:** `../05-typespec-contracts.md`, `../02-large-files-chunking.md` §4

---

## 14.1 Commerce models split

- [x] Split `modules/commerce/models.tsp` by subdomain e.g.:
  - [x] product models
  - [x] checkout / portal models
  - [x] subscription / dunning models
  - [x] coupon / stats models
- [x] Update imports in `routes.tsp` / `admin-routes` / `public-routes` / `main.tsp`  
  *(barrel `models.tsp` keeps paths stable — no importer edits required)*
- [x] `task gen` succeeds

## 14.2 One models + routes split

- [x] Split `modules/one/models.tsp` (auth, workspace, webhooks, api keys, provision)
- [x] Split `modules/one/routes.tsp` by same seams **or** keep routes and only split models first  
  *(kept single `routes.tsp` / `OneOperations` — TypeSpec 1.13 rejects duplicate interface name across files)*
- [x] Gen succeeds; clients compile

## 14.3 Orphan models

For each orphan, **wire or delete**:

- [x] CRM models without routes — add routes or mark internal-only / remove from public OpenAPI  
  *(documented intentional models-only; kept for `Lazuar.ApiTypes` consumers)*
- [x] `LinkedCheckoutDto` / `PaymentRecordDto` if unused  
  *(both deleted)*
- [x] Blank `messaging/models.tsp` — fill minimal ownership note or remove from compile if empty noise  
  *(ownership note; intentionally thin)*
- [x] Document intentional thin modules in api-spec README

## 14.4 Admin routes size

- [x] Optionally split `commerce/admin-routes.tsp` if still painful after models split  
  *(skipped — ~204 LOC; optional; models split is the main win)*

## 14.5 Gen + consumers

- [x] Commit regenerated clients if repo policy commits them
- [x] `api-types-ts` / `api-types-dotnet` build
- [x] No accidental breaking rename without changelog note  
  *(only removals: unused `PaymentRecordDto`, `LinkedCheckoutDto`)*

## 14.6 Exit criteria

- [x] No single commerce/one models file still “bag of everything”
- [x] Orphans resolved or justified in README
- [x] Gen clean
