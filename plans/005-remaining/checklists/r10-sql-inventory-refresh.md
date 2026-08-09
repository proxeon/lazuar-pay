# R10 — Cross-schema SQL inventory refresh

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md`  
**Goal:** Confirm L-01…L-07 still accurate; open ticket table for R11+  
**No fixes in this phase** (except optional drive-by docs)  
**Live table:** [`../cross-schema-leaks-live.md`](../cross-schema-leaks-live.md)  
**Verified:** 2026-08-09

---

## R10.1 Re-grep

- [x] Schema-qualified `FROM`/`JOIN` across modules
- [x] Dapper / `FromSqlRaw` / `NpgsqlCommand` foreign schema
- [x] `PlatformMetricsCollector` multi-schema + product SQL
- [x] Host middleware dual-read (L-07 / keys)

## R10.2 Reconcile with 06 analysis

- [x] L-01 DocumentPublished still present? path: `Modules/Communications/.../DocumentPublishedIntegrationEventHandler.cs` (**present**)
- [x] L-02 PlatformEndpoints GlobalUsers? `Modules/Payments/.../PlatformEndpoints.cs` (**present**)
- [x] L-03 PublicArrears multi-schema? `Modules/Commerce/.../PublicArrearsEndpoints.cs` (**present**)
- [x] L-04 dead GetDefaultTemplateIdsAsync? `CommerceRepository.cs` (**present**, dead callers)
- [x] L-05 CommerceDocumentLookup CRM join? `CommerceDocumentLookup.cs` (**present**)
- [x] L-06 metrics? `PlatformMetricsCollector.cs` (**present**)
- [x] L-07 dual-read keys? **FIXED** by R05 — One-only; no `LhdnLookupSql`
- [x] Any **new** leaks found? list: **none** on product paths (host `SqlApiKeyMigrationStore` is R03 tooling, not a new L-##)

## R10.3 Priority order for this wave

- [x] Ordered fix list (default: R11→R15, R16 handoff metrics, R17 handoff keys): **R11 L-01 → R12 L-02 → R13 L-03 → R14 L-05 → R15 L-04 → R16/R35 L-06 → R17 complete (L-07 fixed)**

## R10.4 Exit

- [x] `plans/005-remaining/cross-schema-leaks-live.md` or updated section in 06 with “verified YYYY-MM-DD”
- [x] R11 unblocked
