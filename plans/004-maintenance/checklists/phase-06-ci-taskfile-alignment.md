# Phase 06 — CI and Taskfile alignment

**Goal:** What we run locally matches what we trust on main.  
**Evidence:** `../07-tests-migrations-hygiene.md`, CI workflows.

---

## 06.1 Inventory current CI

- [ ] Read `.github/workflows/ci.yml` jobs (contracts, dotnet, …)
- [ ] List every `dotnet test` project CI runs
- [ ] List every project `task api:test` runs
- [ ] Diff: projects in Taskfile but not CI; CI but not Taskfile

## 06.2 Ops tests gap

- [ ] Confirm `Modules.Ops.Tests` is in Taskfile `api:test`
- [ ] Confirm whether CI runs it (historically **no**)
- [ ] Add `Modules.Ops.Tests` to CI dotnet job **or** remove from Taskfile if intentionally local-only (prefer add to CI)
- [ ] Ensure CI has any secrets/env Ops tests need (or tests skip cleanly)

## 06.3 Integration tests policy

- [ ] Document in README or Taskfile: IntegrationTests need Docker/Postgres
- [ ] Align CI services (Postgres) with soft-skip vs hard-fail Testcontainers tests
- [ ] Note Commerce Testcontainers hard-fail vs credit concurrency soft-skip — document matrix in `apps/lazuar-api/README.md` or test README

## 06.4 Contracts job honesty

- [ ] Confirm contracts job runs `task gen` + dirty check on:
  - [ ] `packages/api-types-ts`
  - [ ] `packages/api-types-dotnet`
  - [ ] LHDN SDK generated trees if committed
- [ ] Fix pnpm version mismatch if still present (packageManager vs action version)
- [ ] Contracts job green on main after fix

## 06.5 Optional: architecture + module matrix table

- [ ] Add short “Testing” section to `apps/lazuar-api/README.md`:
  - [ ] ArchitectureTests
  - [ ] ModuleTests
  - [ ] Modules.Billing.Tests
  - [ ] Modules.Ops.Tests
  - [ ] IntegrationTests (Docker)
- [ ] Point to `task api:test`

## 06.6 Taskfile footguns (migrations)

- [ ] Fix `api:migrations:add` example `MODULE=Tenant` → real module names
- [ ] Document CRM context spelling: `Crm` not `CRM` for `CrmDbContext`
- [ ] Verify migrate task lists all 9 active contexts

## 06.7 Exit criteria

- [ ] Taskfile `api:test` ⊆ CI (or documented exceptions)
- [ ] Ops tests run in CI or explicitly excluded with reason
- [ ] Contracts gen gate uses correct pnpm/dotnet versions
