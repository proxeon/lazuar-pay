# R10 — Cross-schema SQL inventory refresh

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md`  
**Goal:** Confirm L-01…L-07 still accurate; open ticket table for R11+  
**No fixes in this phase** (except optional drive-by docs)

---

## R10.1 Re-grep

- [ ] Schema-qualified `FROM`/`JOIN` across modules
- [ ] Dapper / `FromSqlRaw` / `NpgsqlCommand` foreign schema
- [ ] `PlatformMetricsCollector` multi-schema + product SQL
- [ ] Host middleware dual-read (L-07 / keys)

## R10.2 Reconcile with 06 analysis

- [ ] L-01 DocumentPublished still present? path: ________
- [ ] L-02 PlatformEndpoints GlobalUsers? ________
- [ ] L-03 PublicArrears multi-schema? ________
- [ ] L-04 dead GetDefaultTemplateIdsAsync? ________
- [ ] L-05 CommerceDocumentLookup CRM join? ________
- [ ] L-06 metrics? ________
- [ ] L-07 dual-read keys? ________
- [ ] Any **new** leaks found? list: ________

## R10.3 Priority order for this wave

- [ ] Ordered fix list (default: R11→R15, R16 handoff metrics, R17 handoff keys): ________

## R10.4 Exit

- [ ] `plans/005-remaining/cross-schema-leaks-live.md` or updated section in 06 with “verified YYYY-MM-DD”
- [ ] R11 unblocked
