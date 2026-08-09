# 004 — Backend + TypeSpec maintenance analysis

**Status:** Investigation complete · implementation checklists ready  
**Date:** 2026-08-09  
**Scope:** `apps/lazuar-api`, `packages/api-spec`, related generated contracts / tests / gen pipeline  
**Out of scope:** Frontend apps, Caddy/DX gateway work

## Implementation checklists (phases 00–18 — largely executed)

→ **[`checklists/README.md`](./checklists/README.md)** — phase map 00–18, execution order, PR rules  

Many small phase files (not one fat checklist): secrets, dual keys, TypeSpec honesty, CI, One endpoints split, Program composition, dunning, folders, fixtures, BB thinning, optional extracts, definition of done.

## Future work (remaining modifications after 00–18)

→ **[`FUTURE-WORK.md`](./FUTURE-WORK.md)** — deferred workstreams FW-1…FW-7  

→ **[`checklists-future/README.md`](./checklists-future/README.md)** — **phased checklists F00–F16** (same style as 00–18; many phases, not one mega-PR)

Covers: API key cutover, LHDN webhooks → One, BuildingBlocks moves, cross-schema SQL, optional extracts, TypeSpec Wave B, polish. Execute phase-by-phase; do not squash into a single commit.

Also: locked **[`decisions.md`](./decisions.md)**, cutover design **[`api-key-cutover-design.md`](./api-key-cutover-design.md)**, close-out **[`phase-18-done.md`](./phase-18-done.md)**.

## Subagent reports (full text)

| # | File | Focus |
|---|------|--------|
| 01 | [`01-removable-dead-code.md`](./01-removable-dead-code.md) | Delete candidates, orphans, secrets |
| 02 | [`02-large-files-chunking.md`](./02-large-files-chunking.md) | God-files and split plans |
| 03 | [`03-folder-organization.md`](./03-folder-organization.md) | Module layout consistency |
| 04 | [`04-module-boundaries-modularization.md`](./04-module-boundaries-modularization.md) | Fat monolith / new modules |
| 05 | [`05-typespec-contracts.md`](./05-typespec-contracts.md) | TypeSpec structure & drift |
| 06 | [`06-building-blocks-shared-kernel.md`](./06-building-blocks-shared-kernel.md) | BB / SharedKernel fatness |
| 07 | [`07-tests-migrations-hygiene.md`](./07-tests-migrations-hygiene.md) | Tests & EF migrations |
| 08 | [`08-composition-di-endpoints.md`](./08-composition-di-endpoints.md) | Program.cs, DI, endpoints |
| 09 | [`09-duplication-tech-debt.md`](./09-duplication-tech-debt.md) | Duplication & patterns |
| 10 | [`10-maintenance-questions-roadmap.md`](./10-maintenance-questions-roadmap.md) | Missed questions + roadmap |

Orchestrator evaluation: conversation reply + summary below when committed with evaluation notes.

## Snapshot metrics (investigation day)

| Module | ~.cs files | ~LOC |
|--------|------------|------|
| Commerce | 129 | ~17.8k |
| One | 87 | ~8.6k |
| Lhdn | 79 | ~7.0k |
| Billing | 71 | ~7.4k |
| Payments | 70 | ~6.5k |
| Communications | 47 | ~4.8k |
| Ops | 25 | ~2.3k |
| Messaging | 27 | ~1.7k |
| CRM | 24 | ~1.9k |

Largest hotspots (excl. migrations): `One/Endpoints.cs` (~767), `ProvisionAuraWorkspaceCommand` (~647), `DunningEngineJob` (~520), `Program.cs` (~485).
