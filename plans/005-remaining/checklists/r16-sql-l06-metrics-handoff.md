# R16 — L-06 metrics SQL handoff

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-06, `../05-bb-metrics-plugins.md`  
**Goal:** Do **not** half-fix metrics here — link to R35  
**Notes:** `../r16-notes.md`

---

## R16.1 Confirm still present

- [x] `PlatformMetricsCollector` still has hardcoded schemas + `lhdn.TaxDocuments` (or current equivalent)  
  — Confirmed 2026-08-09: `ModuleSchemas` (9) + `QueryLhdnStuckAsync` → `lhdn."TaxDocuments"`

## R16.2 Handoff

- [x] Ensure R35 is on the wave plan if L-06 is P1 this wave  
  — BB track YES; ordered start list: R35 after R16 (`wave-decisions.md`)
- [x] If metrics out of wave: ticket id ________ and stop  
  — N/A (R35 **in** wave; not deferred)
- [x] Do not leave a partial “move one query” without contributor design  
  — No app code in R16

## R16.3 Exit

- [x] Explicit: fixed in R35 **or** deferred ticket  
  — **Fixed in R35** (`checklists/r35-bb-metrics-plugins.md`)
