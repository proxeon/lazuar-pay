# Phase 01 — Done

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Commit subject:** `chore(api): remove secrets and dead residue (phase 01)`

## What landed

### Deleted paths

1. `scripts/lhdn_sandbox/cookies.txt`
2. `packages/api-types-dotnet/Generated/Models.cs` (+ empty `Generated/`)
3. `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/Strategies/UblStrategyTests.cs`
4. `apps/lazuar-api/tests/Lazuar.ArchitectureTests/TestData/lhdn-golden-master.json` (+ empty `TestData/`)
5. `script/second-app-proof.md` (+ empty `script/`)

### Edited

- `.gitignore` — ignore `scripts/lhdn_sandbox/cookies.txt` and `scripts/lhdn_sandbox/**/cookies.txt`
- `docs/architecture-decision-log/005-typespec-api-contract-generation.md` — NSwag output → `Lazuar.ApiContracts.cs`
- `apps/lazuar-api/tests/Lazuar.ArchitectureTests/Lazuar.ArchitectureTests.csproj` — remove EmbeddedResource
- `apps/lazuar-api/Lazuar.slnx` — remove empty Folder nodes (`/src/`, empty Lhdn subfolders, empty Billing/Infrastructure, empty `/Modules/`)
- Checklist marked complete: `checklists/phase-01-secrets-and-dead-residue.md`

## Verification

- `git ls-files` has no `cookies.txt`
- No `Generated/Models.cs` in tree
- `dotnet build` api-types-dotnet OK
- `dotnet test` ArchitectureTests green

## Next

Phase 02+ per `plans/004-maintenance/checklists/`.
