# Phase 14 — Analysis (TypeSpec structure polish)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Goal:** Large commerce/one TypeSpec model bags become navigable subdomain files; orphans gone or justified; gen clean.  
**Evidence:** `checklists/phase-14-typespec-structure-polish.md`, `02-large-files-chunking.md` §4, `05-typespec-contracts.md`

---

## 1. Pre-change inventory

| File | LOC | Responsibilities |
|------|-----|------------------|
| `modules/commerce/models.tsp` | **384** | Product, checkout, portal, dunning, payment-config, subscribers, coupons, stats, custom-checkout (~38 models) |
| `modules/one/models.tsp` | **298** | Auth, workspace/members/invites, webhooks, storage, API keys, provision tree |
| `modules/one/routes.tsp` | **235** | Single `OneOperations` under `/one` |
| `modules/commerce/admin-routes.tsp` | **204** | Admin bag (optional split deferred) |
| `modules/crm/models.tsp` | **46** | Models only — no routes; used via `Lazuar.ApiTypes` in CRM backend |
| `modules/messaging/models.tsp` | **4** | Blank + short comment |
| `common/models.tsp` | | Includes orphan `LinkedCheckoutDto` |
| Commerce | | Orphan `PaymentRecordDto` unused by any route |

### 1.1 Import graph (unchanged entrypoints)

| Consumer | Imports |
|----------|---------|
| `main.tsp` | `commerce/models.tsp`, `one/models.tsp` + routes, crm, messaging, … |
| `docs-commerce.tsp` | `commerce/models.tsp` + admin/public routes |
| `docs-one.tsp` | `one/models.tsp` + routes |
| `docs-lhdn.tsp` | `one/models.tsp` (API key DTO façades) |
| `platform/routes.tsp` | commerce + one model barrels |
| commerce admin/public routes | `./models.tsp` |

Barrel keeps these import paths stable — no `main.tsp` / `docs-*.tsp` path rewrites required.

### 1.2 Orphan classification

| Model | Used by HTTP? | Used by C# via ApiTypes? | Action |
|-------|:-------------:|:------------------------:|--------|
| `Crm.ClientProfileDto` (+ create/update, address) | No | **Yes** (`ICrmQueryService`, commands, tests) | **Keep** + document |
| `Core.LinkedCheckoutDto` | No | No | **Delete** |
| `Commerce.PaymentRecordDto` | No | No | **Delete** |
| Messaging namespace | No | N/A | **Keep thin** + ownership note |

---

## 2. Target layout

### 2.1 Commerce models

```
modules/commerce/
  models.tsp                    # barrel imports only
  models/
    product.tsp
    checkout.tsp
    portal.tsp
    dunning.tsp
    payment-config.tsp
    subscriber.tsp              # no PaymentRecordDto
    coupon.tsp
    stats.tsp
    custom-checkout.tsp
  admin-routes.tsp              # unchanged (14.4 optional)
  public-routes.tsp             # unchanged
```

All files: `namespace LazuarApi.Commerce;`

### 2.2 One models

```
modules/one/
  models.tsp                    # barrel imports only
  models/
    auth.tsp
    workspace.tsp
    webhook.tsp
    storage.tsp
    api-keys.tsp
    provision.tsp
  routes.tsp                    # single OneOperations (see §3)
```

### 2.3 One routes decision

Attempted split into `routes/{auth,workspace,...}.tsp` reusing interface name `OneOperations` for merge → **TypeSpec 1.13 `duplicate-symbol`** (interfaces do not merge across files).

**Choice:** keep one `routes.tsp` / `OneOperations` so OpenAPI tags and operationIds stay identical. Document that future route splits need distinct interface names (and accept tag churn) or wait for a compiler merge story.

---

## 3. Stability rules

- [x] Barrel path `modules/*/models.tsp` preserved for all existing importers
- [x] No model renames, field renames, or default changes
- [x] No route path / method / auth changes
- [x] Delete only confirmed-unused schemas (`LinkedCheckoutDto`, `PaymentRecordDto`)
- [x] CRM remains in `main.tsp` import graph (backend depends on generated types)
- [x] Messaging remains importable, intentionally empty
- [x] Admin commerce routes not split this phase (optional 14.4)

---

## 4. Explicitly out of scope

- Commerce `admin-routes.tsp` resource split
- One routes multi-file split (blocked by interface non-merge)
- Wiring CRM HTTP routes
- Filling Messaging TypeSpec with notify/logs (impl-only internal edge)
- Dual local DTO cleanup on Commerce subscriber endpoints (phase 05 residual)
