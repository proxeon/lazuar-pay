# R13 — Fix L-03 Commerce arrears update-payment multi-schema JOIN

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-03  
**File (verify):** `Commerce/.../PublicArrearsEndpoints.cs`  
**Problem:** JOIN `crm` + `one` (and commerce) in one query

---

## R13.1 Design

- [ ] Split into commerce-owned SQL + `ICrmQueryService` + `IOneQueryService` (or enrich domain)
- [ ] Document data flow for arrears update-payment

## R13.2 Implement

- [ ] Replace multi-schema Dapper with port composition
- [ ] Preserve HTTP contract/behavior

## R13.3 Tests

- [ ] Arrears / update-payment tests green
- [ ] Tenant isolation preserved

## R13.4 Exit

- [ ] No `crm.`/`one.` in that endpoint SQL
