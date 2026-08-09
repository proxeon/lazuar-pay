# R35 — Metrics plugins + schema registration

**Track:** BB · **Analysis:** `../05-bb-metrics-plugins.md`  
**Also closes:** SQL L-06 handoff from R16  
**Notes:** [`../r35-notes.md`](../r35-notes.md) · **Status:** complete

---

## R35.1 Schema registration (M1)

- [x] `IOutboxSchemaRegistration` / `AddOutboxSchemaMetrics("one")` etc.
- [x] Remove hardcoded 9-schema constant array
- [x] Each module registers its schema in DI

## R35.2 Contributor + LHDN stuck (M2)

- [x] `IPlatformMetricsContributor` + contribution bag
- [x] Move `lhdn.TaxDocuments` stuck SQL to Lhdn contributor
- [x] Aggregator uses `IEnumerable<>` contributors
- [x] Preserve `/health/metrics` field names (`lhdn_stuck_count` etc.)

## R35.3 Hardening (M3 optional)

- [x] Fail-soft per contributor
- [ ] Dunning counter ownership cleanup if still in BB (M4) — **deferred** (process counter remains BB; optional later)

## R35.4 Docs / tests

- [x] Metrics endpoint smoke (field names preserved; registration + boundary tests)
- [x] 009 + FUTURE-WORK updated

## R35.5 Exit

- [x] No product table SQL in BB collector
- [x] No hardcoded schema inventory
