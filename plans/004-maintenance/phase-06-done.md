# Phase 06 — Done

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Commit subject:** `ci: align Taskfile and Ops tests with GitHub Actions (phase 06)`

## What landed

### CI (`.github/workflows/ci.yml`)

1. **`dotnet` job:** `Test (Ops)` step runs `tests/Modules.Ops.Tests/Modules.Ops.Tests.csproj` after Billing — matches `task api:test`.
2. **`contracts` job:** `pnpm/action-setup` version **9 → 11.5.2** (root `packageManager`: `pnpm@11.5.2`).

### Taskfile

- `api:migrations:add` usage: `MODULE=Tenant` → **`MODULE=Billing`**.
- Desc documents CRM: **`CrmDbContext` / `MODULE=Crm`** (not `CRM`); case-sensitive path note for Linux.

### Docs

- `apps/lazuar-api/README.md` §6 Testing — five projects, dependency matrix, Integration/Postgres notes, `task api:test`.

### Plans

- `phase-06-analysis.md` — inventory + gaps + fixes  
- `checklists/phase-06-ci-taskfile-alignment.md` — criteria marked done  

## Exit criteria

| Criterion | Status |
|-----------|--------|
| `task api:test` ⊆ CI | Yes (same five projects) |
| Ops tests in CI | Yes |
| Contracts pnpm/dotnet versions aligned | pnpm 11.5.2; dotnet 10.0.x unchanged |
| Migration example not Tenant | Billing + CRM spelling documented |

## Explicitly not done

- CI calling `task api:test` as a single step  
- CRM-aware migrations:add template (separate MODULE folder vs context vars)  
- Integration hard-fail / soft-skip matrix automation  

## Next

Phase 07 One endpoints split (or Phase 05 TypeSpec honesty if still open on the branch).
