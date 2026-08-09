# R14 — Fix L-05 CommerceDocumentLookup CRM JOIN

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-05  
**File (verify):** Commerce document lookup service used by Billing  
**Problem:** CRM joined inside Commerce port implementation

---

## R14.1 Design

- [x] Session/document SQL stays commerce-only
- [x] Customer profile fields via `ICrmQueryService`

## R14.2 Implement

- [x] Split query; compose results in lookup service
- [x] Keep `ICommerceDocumentLookup` external contract stable for Billing

## R14.3 Tests

- [x] Billing draft/final document tests still pass
- [x] Lookup unit tests updated

## R14.4 Exit

- [x] No `crm.` SQL inside CommerceDocumentLookup
