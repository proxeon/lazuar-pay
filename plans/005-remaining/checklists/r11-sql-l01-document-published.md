# R11 — Fix L-01 DocumentPublished cross-schema SQL

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-01  
**File (verify):** `Communications/.../DocumentPublishedIntegrationEventHandler.cs`  
**Problem:** JOIN/read `billing` + `one` + `commerce` from Communications

---

## R11.1 Design

- [x] Prefer enrich `DocumentPublishedIntegrationEvent` at publish site with fields Comms needs
- [x] Or add Contracts query ports on owning modules (document choice: **event denorm at publish; not query ports**)
- [x] List fields currently loaded via SQL: **TenantSlug, BusinessName, CustomerName, CustomerEmail**

## R11.2 Implement

- [x] Publisher (Billing/Commerce path) supplies customer/doc fields
- [x] Handler uses event payload only (no foreign-schema SQL)
- [x] Delete Dapper multi-schema query

## R11.3 Tests

- [x] Handler unit/module test with enriched event
- [x] Regression: document published still triggers communications behavior

## R11.4 Exit

- [x] Grep handler: no foreign schema SQL
- [ ] Single-purpose PR merged
