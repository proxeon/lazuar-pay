# R11 — Fix L-01 DocumentPublished cross-schema SQL

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-01  
**File (verify):** `Communications/.../DocumentPublishedIntegrationEventHandler.cs`  
**Problem:** JOIN/read `billing` + `one` + `commerce` from Communications

---

## R11.1 Design

- [ ] Prefer enrich `DocumentPublishedIntegrationEvent` at publish site with fields Comms needs
- [ ] Or add Contracts query ports on owning modules (document choice: ________)
- [ ] List fields currently loaded via SQL: ________

## R11.2 Implement

- [ ] Publisher (Billing/Commerce path) supplies customer/doc fields
- [ ] Handler uses event payload only (no foreign-schema SQL)
- [ ] Delete Dapper multi-schema query

## R11.3 Tests

- [ ] Handler unit/module test with enriched event
- [ ] Regression: document published still triggers communications behavior

## R11.4 Exit

- [ ] Grep handler: no foreign schema SQL
- [ ] Single-purpose PR merged
