# Phase 01 — Secrets and confident dead residue

**Goal:** Remove dangerous and zero-risk dead artifacts.  
**PR shape:** Small delete-only PR(s). Prefer one PR for secrets, one for dead code.  
**Evidence:** `../01-removable-dead-code.md` §1

---

## 01.1 Secrets (do first, same day)

- [ ] Delete `scripts/lhdn_sandbox/cookies.txt` if present
- [ ] Add gitignore rule for `scripts/lhdn_sandbox/cookies.txt` and/or `**/cookies.txt` under sandbox
- [ ] Grep for other committed cookies/JWT jars under `scripts/`, `apps/lazuar-api/`, `packages/`
- [ ] If JWT was real: rotate/invalidate `sysadmin@lazuars.io` (or affected) session secrets as ops practice
- [ ] Confirm `git log` / history scrub is **out of scope** unless security policy requires (note decision)

## 01.2 Dead NSwag twin

- [ ] Confirm `packages/api-types-dotnet/Lazuar.ApiContracts.csproj` only compiles `Lazuar.ApiContracts.cs`
- [ ] Delete `packages/api-types-dotnet/Generated/Models.cs` if still present
- [ ] Remove empty `Generated/` directory if empty
- [ ] Grep for `Generated/Models` references in docs/ADR/nswag; fix ADR 005 text if it still points at `Generated/Models.cs`
- [ ] `dotnet build packages/api-types-dotnet` (or solution) succeeds

## 01.3 Dead LHDN golden test residue

- [ ] Delete or replace fully commented `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/Strategies/UblStrategyTests.cs`
  - [ ] If delete: ensure no csproj explicit include breaks
  - [ ] If restore later: track as separate ticket, not partial comments
- [ ] Delete unused `apps/lazuar-api/tests/Lazuar.ArchitectureTests/TestData/lhdn-golden-master.json`
- [ ] Remove `<EmbeddedResource Include="TestData\lhdn-golden-master.json" />` from ArchitectureTests csproj
- [ ] `dotnet test tests/Lazuar.ArchitectureTests` still green

## 01.4 Orphan proof note (optional)

- [ ] Confirm no Taskfile/docs link to `script/second-app-proof.md`
- [ ] Delete `script/second-app-proof.md` **or** move under `plans/` if still useful
- [ ] Remove empty `script/` dir if empty

## 01.5 Solution clutter (optional same PR)

- [ ] Open `apps/lazuar-api/Lazuar.slnx`
- [ ] Remove empty `<Folder>` nodes that do not contain projects (Lhdn/Billing empty folder noise)
- [ ] Solution still opens / `dotnet build Lazuar.slnx` works

## 01.6 Local hygiene (do not commit)

- [ ] Optionally wipe local `bin/`/`obj/`/`packages/api-spec/dist/` (gitignored)
- [ ] Do **not** commit `dist/` or build outputs

## 01.7 Verification

- [ ] Grep: no `cookies.txt` tracked by git
- [ ] Grep: no `Generated/Models.cs`
- [ ] Architecture + ModuleTests (or at least Architecture) green
- [ ] PR description lists every deleted path

## 01.8 Exit criteria

- [ ] No known secret cookie jar in tree
- [ ] No uncompiled NSwag twin
- [ ] No no-op fully-commented UBL test class + orphan golden resource
