# R20 — TypeSpec dual DTO: products

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** TypeSpec · Wave B  
**Checklist:** `checklists/r20-tsp-dual-dto-products.md`  
**Analysis:** `08-typespec-wave-b.md` §2.2 A  
**Scope this pass:** Bind product create/update admin endpoints to generated `Lazuar.ApiTypes` DTOs; remove local dual records. No TypeSpec change.

---

## Summary

| Concern | State |
|---------|--------|
| Generated `CreateProductRequestDto` | Present in `packages/api-types-dotnet/Lazuar.ApiContracts.cs` |
| Generated `UpdateProductRequestDto` | Present (same package) |
| Field parity local ↔ generated | Match (name/slug/price/pricing_model/minimum_price/currency/interval/gateway + requires_* + fulfillment_targets; update + `is_active`) |
| TypeSpec edit / `task gen` | **Not required** |
| Endpoint bind | `CreateProductRequestDto` / `UpdateProductRequestDto` |
| ACL | `(decimal)req.Price`, `(decimal)req.Minimum_price` → commands |
| Local records deleted | `CreateProductRequest`, `UpdateProductRequest` |
| GET already on `ProductDto` | Unchanged |

---

## Diff (local → generated)

| Local | Generated | Notes |
|-------|-----------|--------|
| `CreateProductRequest` record | `CreateProductRequestDto` class | Wire JSON unchanged (snake_case props) |
| `UpdateProductRequest` record | `UpdateProductRequestDto` class | + `is_active` |
| `decimal Price` / `Minimum_price` | `double Price` / `Minimum_price` | Cast at command ACL |
| `List<string> Fulfillment_targets` | `List<string>` (default empty) | Still null-coalesce empty list |

TSP models already in `packages/api-spec/modules/commerce/models/product.tsp` with `float64` price fields — no gap.

---

## Code change

**File:** `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/ProductEndpoints.cs`

1. Removed local `CreateProductRequest` / `UpdateProductRequest` records.
2. POST `/products` binds `CreateProductRequestDto`; maps to `CreateProductCommand` with double→decimal casts.
3. PUT `/products/{id}` binds `UpdateProductRequestDto`; same cast pattern for `UpdateProductCommand`.
4. `Lazuar.ApiTypes` already imported (`ProductDto`, `IdResponse`, `StatusResponse`).

---

## Verification

| Check | Result |
|-------|--------|
| `rg 'CreateProductRequest\|UpdateProductRequest' apps/lazuar-api` | Only `*RequestDto` binds in `ProductEndpoints.cs` |
| `dotnet build` Commerce Infrastructure | Succeeded 0 warnings / 0 errors |
| `dotnet build` `Lazuar.Api` host | Succeeded 0 warnings / 0 errors |
| `dotnet test` filter `CommerceProduct` | **Passed 7 / 7** |

```
Passed!  - Failed: 0, Passed: 7, Skipped: 0, Total: 7
```

---

## Residual

| Item | Owner | Note |
|------|-------|------|
| Record refund dual DTO | R21 | `TransactionEndpoints.RecordRefundRequest` |
| Re-intro dual DTOs | Review | Prefer bind `Lazuar.ApiTypes` or add TSP first |

---

## Files

| Action | Path |
|--------|------|
| Edited | `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/ProductEndpoints.cs` |
| Notes | `plans/005-remaining/r20-notes.md` |
| Checklist | `plans/005-remaining/checklists/r20-tsp-dual-dto-products.md` |
| FULL-CHECKLIST | R20 section checked |
