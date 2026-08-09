# R35 — Metrics plugins + schema registration (notes)

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** BB  
**Checklist:** `checklists/r35-bb-metrics-plugins.md`  
**Analysis:** `05-bb-metrics-plugins.md`  
**Also closes:** SQL L-06 (handoff from R16)  
**No commit** (per task).

---

## Summary

| Concern | State |
|---------|--------|
| Hardcoded `ModuleSchemas` | **Removed** — DI `IOutboxSchemaRegistration` via `AddOutboxSchemaMetrics` |
| LHDN product SQL in BB | **Removed** — `LhdnStuckMetricsContributor` in Lhdn Infrastructure |
| Aggregator | Thin BB: outbox/inbox scrape + contributor bag merge + process counters |
| HTTP/gauges | **Compatible** — `lhdn_stuck_count` / `lazuar.lhdn.stuck_count` unchanged |
| Fail-soft | Per-contributor try/catch (M3) |
| M4 dunning counter | **Deferred** — still `LazuarMetrics.RecordDunningCancel` |

---

## Design landed

### M1 — Schema registration

- `BuildingBlocks.Application.Observability.IOutboxSchemaRegistration` + `OutboxSchemaRegistration`
- `BuildingBlocks.Infrastructure.Observability.AddOutboxSchemaMetrics(schema)` validates `^[a-z][a-z0-9_]*$`
- All nine modules call it next to outbox hosted services

### M2 — Contributors + LHDN

- `IPlatformMetricsContributor` + `PlatformMetricsCollectContext` + `PlatformMetricsContributionBag`
- Bag key `lhdn.stuck_count` → snapshot `LhdnStuckCount` (HTTP `lhdn_stuck_count`)
- `Modules.Lhdn.Infrastructure.Observability.LhdnStuckMetricsContributor`
- `LhdnObservabilityOptions` (`Lhdn:StuckThreshold`) with dual-bind fallback to `Observability:LhdnStuckThreshold`

### M3 — Fail-soft

- Contributor exceptions logged; collect continues; lag/readiness still usable

### Approved exception (009)

Platform metrics aggregator may query `{schema}.OutboxMessages` / `InboxMessages` for registered schemas only. Must not query module business tables.

---

## Files

| Action | Path |
|--------|------|
| New | `BuildingBlocks/Application/Observability/IOutboxSchemaRegistration.cs` |
| New | `BuildingBlocks/Application/Observability/IPlatformMetricsContributor.cs` |
| New | `BuildingBlocks/Infrastructure/Observability/OutboxSchemaMetricsServiceCollectionExtensions.cs` |
| Edit | `BuildingBlocks/Infrastructure/Observability/PlatformMetricsCollector.cs` |
| Edit | `ObservabilityOptions`, snapshot, collector interface remarks |
| New | `Modules/Lhdn/Infrastructure/Observability/LhdnStuckMetricsContributor.cs` |
| New | `Modules/Lhdn/Infrastructure/Observability/LhdnObservabilityOptions.cs` |
| Edit | All nine module `DependencyInjection.cs` + host `Program.cs` |
| Edit | `appsettings.json` (`Lhdn:StuckThreshold`) |
| Tests | `PlatformMetricsPluginRegistrationTests`, `PlatformMetricsCollectorTests`, `BuildingBlocksMetricsBoundaryTests` |
| Docs | `009`, `FUTURE-WORK`, checklist, `cross-schema-leaks-live` |

---

## Verification

```bash
dotnet build apps/lazuar-api/Lazuar.slnx

dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests \
  --filter "FullyQualifiedName~Observability"

# Gates
rg 'ModuleSchemas|QueryLhdnStuckAsync' apps/lazuar-api/BuildingBlocks   # expect zero
rg 'TaxDocuments' apps/lazuar-api/BuildingBlocks --glob '*.cs'          # comments only
rg 'AddOutboxSchemaMetrics' apps/lazuar-api/Modules --glob 'DependencyInjection.cs'  # nine
```

---

## Exit

| Criterion | Result |
|-----------|--------|
| No product table SQL in BB collector | **Yes** |
| No hardcoded schema inventory | **Yes** |
| `/health/metrics` field names preserved | **Yes** (`lhdn_stuck_count`) |
| L-06 closed | **Yes** |
| Host build | **Green** |
| Observability tests | **18 passed** |

**R35 complete.** L-06 fixed.
