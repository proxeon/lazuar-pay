# R16 — L-06 metrics SQL handoff

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** SQL  
**Checklist:** `checklists/r16-sql-l06-metrics-handoff.md`  
**Analysis:** `06-cross-schema-sql-leaks.md` L-06, `05-bb-metrics-plugins.md`  
**Scope this pass:** **Handoff only** — confirm L-06 still present; route fix to **R35**. **Do not** half-fix metrics SQL in this phase.

---

## Summary

| Concern | State |
|---------|--------|
| Leak | **Still present** — multi-schema product SQL in BuildingBlocks |
| Collector | `PlatformMetricsCollector` hardcoded `ModuleSchemas` (9 schemas) + `lhdn."TaxDocuments"` stuck SQL |
| This phase | **No code change** — intentional |
| Fix owner | **R35** (BB metrics plugins + schema registration) |
| Half-fix ban | Do **not** move one query without contributor design |

---

## Grep confirmation (2026-08-09)

**File:** `apps/lazuar-api/BuildingBlocks/Infrastructure/Observability/PlatformMetricsCollector.cs`

| Signal | Result |
|--------|--------|
| `ModuleSchemas` | **Present** — `one`, `messaging`, `payments`, `crm`, `ops`, `billing`, `lhdn`, `commerce`, `communications` |
| Outbox/inbox loop | Per-schema `FROM "{schema}"."OutboxMessages"` / `InboxMessages` |
| Product SQL | **Present** — `QueryLhdnStuckAsync` → `FROM lhdn."TaxDocuments"` (~line 208) |
| Class remarks | Document temporary “god collector”; prefer `IPlatformMetricsContributor` / schema registration |

L-06 remains a **P1** cross-schema / BB boundary leak until R35.

---

## Why not fix here

R16 is an SQL-track **handoff**, not a metrics rewrite.

- Pluginizing the collector needs DI schema registration + per-module contributors (`05-bb-metrics-plugins.md`).
- Moving only `TaxDocuments` SQL without the contributor design leaves the hardcoded schema list and creates a partial state.
- Wave plan already sequences **R35 after R16 handoff** (`wave-decisions.md`).

**Rule:** Do not land a partial “move one query” PR under R16.

---

## Handoff target — R35

| Item | Detail |
|------|--------|
| Phase | R35 — Metrics plugins + schema registration |
| Checklist | `checklists/r35-bb-metrics-plugins.md` |
| Analysis | `05-bb-metrics-plugins.md` |
| Wave | BuildingBlocks track **YES** (R00 / `wave-decisions.md`) |
| Also closes | SQL L-06 (this handoff) |

R35 expected outcomes (from checklist):

1. Replace hardcoded `ModuleSchemas` with DI-registered schema list  
2. Move `lhdn.TaxDocuments` stuck SQL to an **Lhdn** `IPlatformMetricsContributor`  
3. Aggregator only composes registered sources — no product-table SQL in BB  

---

## Files (docs only)

| Action | Path |
|--------|------|
| Notes | `plans/005-remaining/r16-notes.md` |
| Checklist | `plans/005-remaining/checklists/r16-sql-l06-metrics-handoff.md` |
| Live status | `plans/005-remaining/cross-schema-leaks-live.md` — L-06 handoff R35 |
| FULL-CHECKLIST | R16 section checked |

---

## Exit

| Criterion | Result |
|-----------|--------|
| L-06 still present (confirmed) | **Yes** |
| R35 on wave plan | **Yes** (BB track selected) |
| No partial metrics fix in R16 | **Yes** — zero app code |
| Explicit fix owner | **Fixed in R35** (not deferred ticket) |

**R16 complete.** L-06 remains open until R35.
