# R14 — L-05 CommerceDocumentLookup CRM JOIN

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** SQL  
**Checklist:** `checklists/r14-sql-l05-document-lookup-crm.md`  
**Analysis:** `06-cross-schema-sql-leaks.md` L-05  
**Scope this pass:** Remove `crm` JOIN from `CommerceDocumentLookup.GetDraftCheckoutSessionAsync`. **No** L-04 / L-06.

---

## Summary

| Concern | State |
|---------|--------|
| Design | Commerce SQL + `ICrmQueryService` port composition |
| Commerce SQL | `CheckoutSessions` only (`AdHocLineItems`, `ClientProfileId`) |
| CRM | `ICrmQueryService.GetClientProfileAsync` → name / email |
| Port surface | `ICommerceDocumentLookup` unchanged for Billing |
| `GetCustomerByGatewayTransactionAsync` | Unchanged (already commerce-only) |
| Foreign-schema SQL | **Gone** from `CommerceDocumentLookup.cs` |

---

## Data flow (draft session)

```
GetDraftCheckoutSessionAsync(orgId, sessionId)
  │
  ├─ CommerceSqlConnectionFactory
  │    SELECT AdHocLineItems, ClientProfileId
  │    FROM commerce.CheckoutSessions
  │
  ├─ ICrmQueryService.GetClientProfileAsync(clientProfileId)
  │    → Full_name / Email (defaults if missing — former LEFT JOIN semantics)
  │
  └─ DraftCheckoutSessionDisplay(CustomerName, CustomerEmail, AdHocLineItemsJson)
```

Missing session → `null` (Billing throws "Custom checkout session not found.").  
Missing CRM profile → `"Customer"` / `""` (same as LEFT JOIN nulls).

---

## Files

| Action | Path |
|--------|------|
| Fix | `Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs` |
| Test | `tests/Lazuar.ModuleTests/Commerce/CommerceDocumentLookupBoundaryTests.cs` |
| Live table | `plans/005-remaining/cross-schema-leaks-live.md` |
| Checklist | `plans/005-remaining/checklists/r14-sql-l05-document-lookup-crm.md` |

---

## Verify

```bash
# Lookup must not reference crm schema
rg 'crm\.|ClientProfiles' apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs
# expect: no matches for crm./ClientProfiles (ICrmQueryService only)

dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests --filter "FullyQualifiedName~CommerceDocumentLookup|FullyQualifiedName~PublicArrears|FullyQualifiedName~Commerce"
```

---

## Out of scope

- L-04 (dead template SQL — R15), L-06 (metrics — R16)
- Changing `ICommerceDocumentLookup` contract for Billing
