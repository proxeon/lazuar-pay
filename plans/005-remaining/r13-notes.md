# R13 — L-03 PublicArrears update-payment multi-schema JOIN

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** SQL  
**Checklist:** `checklists/r13-sql-l03-arrears-update.md`  
**Analysis:** `06-cross-schema-sql-leaks.md` L-03  
**Scope this pass:** Remove `crm` + `one` JOIN from public arrears update-payment. **No** L-04…L-06.

---

## Summary

| Concern | State |
|---------|--------|
| Design | Commerce SQL + `ICrmQueryService` + `IOneQueryService` port composition |
| Commerce SQL | `Subscriptions` ⋈ `Products` only; selects `ClientProfileId` for CRM port |
| CRM | `ICrmQueryService.GetClientProfileAsync` → customer email |
| One | `IOneQueryService.GetWorkspaceByIdAsync` → tenant slug |
| HTTP | Same public POST `/checkout/{subId}/update-payment` contract/behavior |
| GET arrears | Unchanged (already commerce-only) |
| Foreign-schema SQL | **Gone** from `PublicArrearsEndpoints.cs` |

---

## Data flow (update-payment)

```
POST /checkout/{subId}/update-payment
  │
  ├─ CommerceSqlConnectionFactory
  │    SELECT subscription + product (commerce only)
  │
  ├─ ICrmQueryService.GetClientProfileAsync(clientProfileId)
  │    → CustomerEmail
  │
  ├─ IOneQueryService.GetWorkspaceByIdAsync(organizationId)
  │    → TenantSlug (success/cancel portal URLs)
  │
  └─ MediatR GenerateCheckoutSessionQuery (Payments)
       → CheckoutResponse.Url
```

Missing subscription, profile, or workspace → `400 Subscription not found.` (matches former JOIN miss semantics).  
Status not `PAST_DUE`/`SUSPENDED` → same active-subscription error as before.

---

## Files

| Action | Path |
|--------|------|
| Fix | `Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs` |
| Test | `tests/Lazuar.ModuleTests/Commerce/PublicArrearsEndpointsBoundaryTests.cs` |
| Live table | `plans/005-remaining/cross-schema-leaks-live.md` |
| Checklist | `plans/005-remaining/checklists/r13-sql-l03-arrears-update.md` |

---

## Verify

```bash
# Endpoint must not reference foreign schemas
rg 'crm\.|one\.' apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs
# expect: no matches

dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests --filter "FullyQualifiedName~PublicArrears|FullyQualifiedName~Commerce"
```

---

## Out of scope

- L-04 (dead template SQL), L-05 (CommerceDocumentLookup CRM), L-06 (metrics)
- Extracting `GetArrearsPaymentUpdateContextQuery` (optional thin-API refactor)
