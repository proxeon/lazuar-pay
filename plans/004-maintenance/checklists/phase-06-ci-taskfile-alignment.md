# Phase 06 — CI and Taskfile alignment

**Goal:** What we run locally matches what we trust on main.  
**Evidence:** `../07-tests-migrations-hygiene.md`, CI workflows.

---

## 06.1 Inventory current CI

- [x] Read `.github/workflows/ci.yml` jobs (contracts, dotnet, …)
- [x] List every `dotnet test` project CI runs
- [x] List every project `task api:test` runs
- [x] Diff: projects in Taskfile but not CI; CI but not Taskfile  
  → Pre-fix: **Ops only in Taskfile**. Post-fix: lists match.

## 06.2 Ops tests gap

- [x] Confirm `Modules.Ops.Tests` is in Taskfile `api:test`
- [x] Confirm whether CI runs it (historically **no**)
- [x] Add `Modules.Ops.Tests` to CI dotnet job **or** remove from Taskfile if intentionally local-only (prefer add to CI)
- [x] Ensure CI has any secrets/env Ops tests need (or tests skip cleanly)  
  → Pure unit (NSubstitute + in-memory config); no secrets.

## 06.3 Integration tests policy

- [x] Document in README or Taskfile: IntegrationTests need Docker/Postgres
- [x] Align CI services (Postgres) with soft-skip vs hard-fail Testcontainers tests  
  → Documented; service Postgres + runner Docker already present.
- [x] Note Commerce Testcontainers hard-fail vs credit concurrency soft-skip — document matrix in `apps/lazuar-api/README.md` or test README

## 06.4 Contracts job honesty

- [x] Confirm contracts job runs `task gen` + dirty check on:
  - [x] `packages/api-types-ts`
  - [x] `packages/api-types-dotnet`
  - [x] LHDN SDK generated trees if committed
- [x] Fix pnpm version mismatch if still present (packageManager vs action version)  
  → `version: 11.5.2` matches `pnpm@11.5.2`
- [ ] Contracts job green on main after fix  
  → Verify on PR / merge; not asserted in this commit alone.

## 06.5 Optional: architecture + module matrix table

- [x] Add short “Testing” section to `apps/lazuar-api/README.md`:
  - [x] ArchitectureTests
  - [x] ModuleTests
  - [x] Modules.Billing.Tests
  - [x] Modules.Ops.Tests
  - [x] IntegrationTests (Docker)
- [x] Point to `task api:test`

## 06.6 Taskfile footguns (migrations)

- [x] Fix `api:migrations:add` example `MODULE=Tenant` → real module names
- [x] Document CRM context spelling: `Crm` not `CRM` for `CrmDbContext`
- [x] Verify migrate task lists all 9 active contexts  
  → One, Messaging, Payments, CRM, Ops, Billing, Lhdn, Commerce, Communications

## 06.7 Exit criteria

- [x] Taskfile `api:test` ⊆ CI (or documented exceptions)
- [x] Ops tests run in CI or explicitly excluded with reason
- [x] Contracts gen gate uses correct pnpm/dotnet versions
