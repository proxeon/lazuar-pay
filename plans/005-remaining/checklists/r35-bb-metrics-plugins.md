# R35 — Metrics plugins + schema registration

**Track:** BB · **Analysis:** `../05-bb-metrics-plugins.md`  
**Also closes:** SQL L-06 handoff from R16

---

## R35.1 Schema registration (M1)

- [ ] `IOutboxSchemaRegistration` / `AddOutboxSchemaMetrics("one")` etc.
- [ ] Remove hardcoded 9-schema constant array
- [ ] Each module registers its schema in DI

## R35.2 Contributor + LHDN stuck (M2)

- [ ] `IPlatformMetricsContributor` + contribution bag
- [ ] Move `lhdn.TaxDocuments` stuck SQL to Lhdn contributor
- [ ] Aggregator uses `IEnumerable<>` contributors
- [ ] Preserve `/health/metrics` field names (`lhdn_stuck_count` etc.)

## R35.3 Hardening (M3 optional)

- [ ] Fail-soft per contributor
- [ ] Dunning counter ownership cleanup if still in BB (M4)

## R35.4 Docs / tests

- [ ] Metrics endpoint smoke
- [ ] 009 + FUTURE-WORK updated

## R35.5 Exit

- [ ] No product table SQL in BB collector
- [ ] No hardcoded schema inventory
