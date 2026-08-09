# Phase 01 — Secrets and confident dead residue

**Goal:** Remove dangerous and zero-risk dead artifacts.  
**PR shape:** Small delete-only PR(s). Prefer one PR for secrets, one for dead code.  
**Evidence:** `../01-removable-dead-code.md` §1  
**Status:** ✅ Done (2026-08-09)

---

## 01.1 Secrets (do first, same day)

- [x] Delete `scripts/lhdn_sandbox/cookies.txt` if present
- [x] Add gitignore rule for `scripts/lhdn_sandbox/cookies.txt` and/or `**/cookies.txt` under sandbox
- [x] Grep for other committed cookies/JWT jars under `scripts/`, `apps/lazuar-api/`, `packages/`
- [x] If JWT was real: rotate/invalidate `sysadmin@lazuars.io` (or affected) session secrets as ops practice
  - **Note:** Ops action outside repo; tree no longer holds the jar. History scrub out of scope.
- [x] Confirm `git log` / history scrub is **out of scope** unless security policy requires (note decision)

## 01.2 Dead NSwag twin

- [x] Confirm `packages/api-types-dotnet/Lazuar.ApiContracts.csproj` only compiles `Lazuar.ApiContracts.cs`
- [x] Delete `packages/api-types-dotnet/Generated/Models.cs` if still present
- [x] Remove empty `Generated/` directory if empty
- [x] Grep for `Generated/Models` references in docs/ADR/nswag; fix ADR 005 text if it still points at `Generated/Models.cs`
- [x] `dotnet build packages/api-types-dotnet` (or solution) succeeds

## 01.3 Dead LHDN golden test residue

- [x] Delete or replace fully commented `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/Strategies/UblStrategyTests.cs`
  - [x] If delete: ensure no csproj explicit include breaks
  - [x] If restore later: track as separate ticket, not partial comments
- [x] Delete unused `apps/lazuar-api/tests/Lazuar.ArchitectureTests/TestData/lhdn-golden-master.json`
- [x] Remove `<EmbeddedResource Include="TestData\lhdn-golden-master.json" />` from ArchitectureTests csproj
- [x] `dotnet test tests/Lazuar.ArchitectureTests` still green

## 01.4 Orphan proof note (optional)

- [x] Confirm no Taskfile/docs link to `script/second-app-proof.md`
  - **Note:** Taskfile has no link. Residual doc/UI mentions remain (payments-integration-quickstart, lazuar-docs, developers page) — cleanup deferred.
- [x] Delete `script/second-app-proof.md` **or** move under `plans/` if still useful
- [x] Remove empty `script/` dir if empty

## 01.5 Solution clutter (optional same PR)

- [x] Open `apps/lazuar-api/Lazuar.slnx`
- [x] Remove empty `<Folder>` nodes that do not contain projects (Lhdn/Billing empty folder noise)
- [x] Solution still opens / `dotnet build Lazuar.slnx` works

## 01.6 Local hygiene (do not commit)

- [x] Optionally wipe local `bin/`/`obj/`/`packages/api-spec/dist/` (gitignored)
- [x] Do **not** commit `dist/` or build outputs

## 01.7 Verification

- [x] Grep: no `cookies.txt` tracked by git
- [x] Grep: no `Generated/Models.cs`
- [x] Architecture + ModuleTests (or at least Architecture) green
- [x] PR description lists every deleted path

## 01.8 Exit criteria

- [x] No known secret cookie jar in tree
- [x] No uncompiled NSwag twin
- [x] No no-op fully-commented UBL test class + orphan golden resource
