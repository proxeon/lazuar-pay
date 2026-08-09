# Phase 18 — Definition of done (maintenance track healthy)

**Goal:** Know when to stop “maintenance mode” and return to product work.  
**Use:** Revisit after each wave; not a single PR.  
**Assessed:** 2026-08-09 on `chore/backend-maintenance-004` (phases 00–17 executed).

---

## 18.1 Safety

- [x] No secrets/cookie jars tracked — Phase 01: `cookies.txt` deleted + gitignored; no secrets in tree
- [x] No uncompiled dead gen twins — Phase 01: `packages/api-types-dotnet/Generated/Models.cs` removed
- [x] Dual API key path closed **or** dated dual-read with calendar — **dated dual-read** (Phase 03 interim): allowed until **2026-11-30**; One-only target **2026-12-15** (`api-key-cutover-design.md`, middleware comments). Full dual-read removal **not** done this track (by design)
- [x] Webhook story single **or** frozen special-case documented — Phase 04: LHDN **C freeze** + observability; end-state **A** (converge through One) deferred; option B rejected in `decisions.md` / Lhdn README

## 18.2 Contracts

- [x] TypeSpec P0 dual DTOs gone — Phase 05: Commerce subscriber + Payments integration checkout bind `Lazuar.ApiTypes`; local dual records deleted
- [x] Path slash / broadcast honesty fixed — Phase 05: `/integrations/payments/checkouts` (no trailing slash); broadcast targeting fields dropped
- [x] `task gen` + CI contracts green — Phase 05/14 gen committed; Phase 06 contracts job pnpm **11.5.2**
- [x] api-spec README matches tree — Phase 02 honesty + Phase 14 barrel/models tree and models-only table

## 18.3 Navigability

- [x] One endpoints split to Commerce style — Phase 07: thin `MapOneEndpoints` + `Endpoints/*`
- [x] Program.cs thinned — Phase 08: ~488 → ~166 LOC + `Composition/*` helpers
- [x] At least provision or dunning split done if those areas were touch-heavy — **both**: Phase 09 provision partials; Phase 10 `DunningEngineJob` partials
- [x] Messaging folder convention aligned — Phase 12: `Workers/` + `EventHandlers/`; Billing/Lhdn/Ops endpoint composers

## 18.4 Quality loops

- [x] CI runs same critical test projects as Taskfile (Ops included or excluded with reason) — Phase 06: Ops tests in CI; five projects match `task api:test`
- [x] Architecture tests green on main — green throughout track on this branch (incl. host Application-ref rule, Phase 17); merge gate remains PR review
- [ ] Known cross-schema SQL leaks tracked as issues if not fixed — **remaining:** inventoried in `04-module-boundaries` §7.1, `009-building-blocks-ownership`, Phase 15 metrics note; **not fixed** this track (e.g. Communications receipt joins, `PlatformMetricsCollector` product SQL). Promote to normal backlog tickets on track close — not silent

## 18.5 Structural debt accepted consciously

- [x] No new modules without Phase 16 trigger — Phase 16: gate **not met**; Credits/Webhooks/Messaging extract-merge **skipped**
- [x] BuildingBlocks thinning plan exists (even if incomplete) — Phase 15: `docs/009-building-blocks-ownership.md` (stay/move/grey/defer); code moves deferred
- [x] SharedKernel decision documented — Phase 15: keep intentional empty marker + README/xmldoc
- [x] Migration squash **not** required for “done” — not done; not a gate

## 18.6 Product honesty

- [x] Deferred revenue / WhatsApp / probes decided — Phase 17: `RevenueRecognitionJob` **parked**; WhatsApp freeze documented (00.4); `_scope-probe` **deleted**
- [x] Community/Vault not taught as live backend modules — Phase 02 docs honesty; modules stay deleted

## 18.7 Stop criteria

When 18.1–18.6 are true:

- [x] Close maintenance track as “healthy enough” — residual dual-read cutover (post **2026-11-30**), webhook **A** convergence, BB code moves, and cross-schema SQL cleanup are **dated/documented backlog**, not open maintenance program work
- [x] Remaining items become normal backlog tickets, not a special program — see `../phase-18-done.md` deferred list

---

## Residual (explicit backlog — not blockers for “healthy enough”)

| Item | Owner window | Source |
|------|----------------|--------|
| Dual-read remove + migrate `lhdn.DeveloperApiKeys` | after **2026-11-30** (One-only by **2026-12-15**) | Phase 03 |
| LHDN webhooks → One dispatcher (**A**) | product schedule | Phase 04 |
| BB port/LLM/email/metrics code moves | when touching those areas | Phase 15 |
| Cross-schema SQL hygiene | normal boundary tickets | plan 04 / 009 |
| Credits / Webhooks extract / Messaging merge | Phase 16 product trigger only | Phase 16 |
| Revenue recognition product path | finance / Xero epic | 00.3 / Phase 17 |
