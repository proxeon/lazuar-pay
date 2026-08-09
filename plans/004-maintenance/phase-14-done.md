# Phase 14 — Done

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Commit subject:** `refactor(api-spec): split large TypeSpec model files (phase 14)`

## What landed

### 1. Commerce models split

| Subdomain | File | Models (representative) |
|-----------|------|-------------------------|
| Product | `models/product.tsp` | ProductDto, Create/Update, CheckoutConfiguration |
| Checkout | `models/checkout.tsp` | PublicCheckout, CheckoutResponse/Status, ValidateCoupon |
| Portal | `models/portal.tsp` | Portal sub/order, aggregated portal, arrears, cancel |
| Dunning | `models/dunning.tsp` | Campaign CRUD DTOs, steps, pause |
| Payment config | `models/payment-config.tsp` | PaymentConfigDto, Save… |
| Subscriber | `models/subscriber.tsp` | CommerceSubscription, transactions, manual enroll, refund |
| Coupon | `models/coupon.tsp` | Coupon CRUD |
| Stats | `models/stats.tsp` | CommerceStats, cashflow, payment methods |
| Custom checkout | `models/custom-checkout.tsp` | CustomCheckout + line items |

`models.tsp` is a barrel only (~11 LOC). Import sites (`admin-routes`, `public-routes`, `main`, `docs-commerce`, `platform`) unchanged.

### 2. One models split

| Subdomain | File |
|-----------|------|
| Auth / profile | `models/auth.tsp` |
| Workspace / invites / apps | `models/workspace.tsp` |
| Webhooks | `models/webhook.tsp` |
| Storage | `models/storage.tsp` |
| API keys | `models/api-keys.tsp` |
| Provision | `models/provision.tsp` |

`routes.tsp` remains a single `OneOperations` interface (TypeSpec does not merge same-name interfaces across files; multi-file routes would change tags).

### 3. Orphans

| Item | Resolution |
|------|------------|
| `PaymentRecordDto` | Removed from commerce |
| `LinkedCheckoutDto` | Removed from `common/models.tsp` |
| CRM models | **Kept** — documented in `packages/api-spec/README.md` as intentional models-only; backend uses `Lazuar.ApiTypes` |
| Messaging | **Intentionally thin** — ownership note expanded in `messaging/models.tsp` + README |

### 4. Docs

- `packages/api-spec/README.md` — directory tree, barrel pattern, models-only table, orphan resolutions
- `phase-14-analysis.md` — inventory + layout + decisions
- `checklists/phase-14-typespec-structure-polish.md` — 14.1–14.6 marked done

### 5. Regenerated clients

- `packages/api-types-ts/src/index.ts`
- `packages/api-types-dotnet/Lazuar.ApiContracts.cs`
- LHDN Kiota lockfiles (from `task gen`)

## Verification

| Check | Result |
|-------|--------|
| `pnpm build` (api-spec / all entrypoints) | **Success** |
| `task gen --force` | **Success** |
| `dotnet build` api-types-dotnet | **0 warnings, 0 errors** |
| `PaymentRecordDto` / `LinkedCheckoutDto` in clients | **Gone** |
| `ClientProfileDto` in clients | **Present** |
| Commerce/one barrel imports | Stable paths |

## Exit criteria

| Criterion | Status |
|-----------|--------|
| No single commerce/one models bag | Yes (max subdomain ~89 LOC provision) |
| Orphans resolved or justified in README | Yes |
| Gen clean | Yes |

## Explicitly not done

- Commerce `admin-routes.tsp` split (optional 14.4)
- One `routes.tsp` multi-file (compiler: no interface merge)
- CRM public HTTP surface
- Messaging TypeSpec notify/logs

## Next

Phase 15 — building-blocks thin (or phase 13 if test-fixtures land first).
