# R14 — Fix L-05 CommerceDocumentLookup CRM JOIN

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-05  
**File (verify):** Commerce document lookup service used by Billing  
**Problem:** CRM joined inside Commerce port implementation

---

## R14.1 Design

- [ ] Session/document SQL stays commerce-only
- [ ] Customer profile fields via `ICrmQueryService`

## R14.2 Implement

- [ ] Split query; compose results in lookup service
- [ ] Keep `ICommerceDocumentLookup` external contract stable for Billing

## R14.3 Tests

- [ ] Billing draft/final document tests still pass
- [ ] Lookup unit tests updated

## R14.4 Exit

- [ ] No `crm.` SQL inside CommerceDocumentLookup
