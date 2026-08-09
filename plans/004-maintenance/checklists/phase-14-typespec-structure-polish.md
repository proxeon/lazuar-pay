# Phase 14 — TypeSpec structure polish

**Goal:** Large TSP files become navigable; orphans gone or justified.  
**Depends on:** Phase 05 honesty first (structure without honesty wastes work).  
**Evidence:** `../05-typespec-contracts.md`, `../02-large-files-chunking.md` §4

---

## 14.1 Commerce models split

- [ ] Split `modules/commerce/models.tsp` by subdomain e.g.:
  - [ ] product models
  - [ ] checkout / portal models
  - [ ] subscription / dunning models
  - [ ] coupon / stats models
- [ ] Update imports in `routes.tsp` / `admin-routes` / `public-routes` / `main.tsp`
- [ ] `task gen` succeeds

## 14.2 One models + routes split

- [ ] Split `modules/one/models.tsp` (auth, workspace, webhooks, api keys, provision)
- [ ] Split `modules/one/routes.tsp` by same seams **or** keep routes and only split models first
- [ ] Gen succeeds; clients compile

## 14.3 Orphan models

For each orphan, **wire or delete**:

- [ ] CRM models without routes — add routes or mark internal-only / remove from public OpenAPI
- [ ] `LinkedCheckoutDto` / `PaymentRecordDto` if unused
- [ ] Blank `messaging/models.tsp` — fill minimal ownership note or remove from compile if empty noise
- [ ] Document intentional thin modules in api-spec README

## 14.4 Admin routes size

- [ ] Optionally split `commerce/admin-routes.tsp` if still painful after models split

## 14.5 Gen + consumers

- [ ] Commit regenerated clients if repo policy commits them
- [ ] `api-types-ts` / `api-types-dotnet` build
- [ ] No accidental breaking rename without changelog note

## 14.6 Exit criteria

- [ ] No single commerce/one models file still “bag of everything”
- [ ] Orphans resolved or justified in README
- [ ] Gen clean
