# Phase 15 — Done (safe subset)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Commit subject:** `docs(api): BuildingBlocks ownership map (phase 15)`

## What landed

### 1. Ownership map (15.1)

| Artifact | Role |
|----------|------|
| [`apps/lazuar-api/docs/009-building-blocks-ownership.md`](../../apps/lazuar-api/docs/009-building-blocks-ownership.md) | Stay / move / grey / **defer** matrix from plan 06 + decisions 00.4/00.6 |
| [`apps/lazuar-api/docs/002-shared-kernel-vs-building-blocks.md`](../../apps/lazuar-api/docs/002-shared-kernel-vs-building-blocks.md) | Refined rules: platform tenancy **allowed**; forbid product aggregates & private-schema product SQL; link to 009 |

### 2. SharedKernel decision (15.1)

| Choice | Outcome |
|--------|---------|
| Populate vs marker | **Keep intentional empty marker** |
| Code | Expanded xmldoc on `SharedKernelMarker` |
| Docs | `SharedKernel/README.md` |

### 3. Dead host parallel type (15.7)

| Item | Result |
|------|--------|
| `src/Lazuar.Api/Infrastructure/Data/PlatformDbContext.cs` | **Deleted** (unused; modules use BB `PlatformDbContext`) |
| Empty `Infrastructure/` under host | Removed with Data |

### 4. Metrics god SQL (15.5 — note only)

| Item | Result |
|------|--------|
| `PlatformMetricsCollector` | Class remarks + schema field note: future `IPlatformMetricsContributor` / no more product SQL growth |
| Plugin interface | **Not implemented** (deferred) |

### 5. Plans hygiene

- `phase-15-analysis.md`, this file, checklist updated honestly

## Explicitly not done (deferred with map — not silent)

| Checklist | Status |
|-----------|--------|
| 15.2 Port placement (`IR2StorageService`, `IJwtService` → Application) | Deferred |
| 15.3 LLM stack → Ops | Deferred (non-trivial) |
| 15.4 Email / messaging move + template HTML | Deferred; ownership documented |
| 15.5 Metric contributors implementation | Deferred (comment only) |
| 15.6 Per-module worker options | Deferred |
| Product concern **moved** out of BB | **No code move** — deferred via 009 §6 |

## Verification

| Check | Result |
|-------|--------|
| Grep host `Lazuar.Api.Infrastructure.Data` | Only removed file; no remaining refs |
| Grep module DbContexts `: PlatformDbContext` | All → `BuildingBlocks.Infrastructure` |
| `dotnet build` BuildingBlocks.Infrastructure | **Success** |
| `dotnet build` SharedKernel | **Success** |
| Full `Lazuar.Api` build | **Not claimed** — workspace had unrelated missing Commerce Contracts file at time of phase work |

## Exit criteria

| Criterion | Status |
|-----------|--------|
| Written ownership map for LLM / email / metrics | **Yes** (009) |
| At least one product concern moved **or** explicitly deferred | **Deferred** (009 §6 + this file) |
| Architecture tests still enforce BB ↔ module direction | Unchanged tests; BB still has no Modules refs |

## Next

Phase 16 optional extract/merge (product triggers only) or Phase 17 deferred jobs/flags. LLM/email/metrics **implementation** moves are separate PRs when scheduled — not a gate for Horizon 1–2.
