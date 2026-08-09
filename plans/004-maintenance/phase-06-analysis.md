# Phase 06 — Analysis (CI ↔ Taskfile alignment)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Goal:** Local `task api:test` and contracts generation match what CI trusts on main.  
**Evidence:** `checklists/phase-06-ci-taskfile-alignment.md`, `07-tests-migrations-hygiene.md`, `.github/workflows/ci.yml`, root `Taskfile.yml`, root `package.json`.

---

## 1. CI inventory (pre-fix)

### Jobs in `.github/workflows/ci.yml`

| Job | Runner | Purpose |
|-----|--------|---------|
| `contracts` | ubuntu-latest | `pnpm install` → `dotnet tool restore` → `task gen --force` → dirty check on committed clients |
| `dotnet` | ubuntu-latest + Postgres 16 service | restore/build `Lazuar.slnx`, run test projects under `apps/lazuar-api` |

### `dotnet test` projects — CI vs Taskfile (pre-fix)

| Project | `task api:test` | CI `dotnet` job (before) |
|---------|-----------------|--------------------------|
| `Lazuar.ArchitectureTests` | Yes | Yes |
| `Lazuar.IntegrationTests` | Yes | Yes |
| `Lazuar.ModuleTests` | Yes | Yes |
| `Modules.Billing.Tests` | Yes | Yes |
| `Modules.Ops.Tests` | Yes | **No** |

**Diff:** Taskfile ⊆ CI failed solely on **Ops** (`Modules.Ops.Tests` local-only). No CI-only test projects.

### Contracts dirty paths (already correct)

- `packages/api-types-ts/src`
- `packages/api-types-dotnet/Generated`
- `packages/api-types-dotnet/Lazuar.ApiContracts.cs`
- `packages/lhdn-sdk-ts/src/generated`
- `packages/lhdn-sdk-dotnet/src/Generated`

`packages/api-spec/dist` remains gitignored; honesty gate is on committed clients only.

---

## 2. Ops tests gap

| Fact | Detail |
|------|--------|
| Project | `apps/lazuar-api/tests/Modules.Ops.Tests/Modules.Ops.Tests.csproj` |
| In solution | Yes (`Lazuar.slnx`) |
| Suite | `Services/LlmOrchestratorServiceTests.cs` — NUnit + NSubstitute + in-memory `IConfiguration` |
| Secrets / env | **None** — pure unit; no live OpenRouter/AI keys |
| Decision | **Add to CI** (prefer honesty over “local-only” exclusion) |

No CI env/secret changes required.

---

## 3. Integration / Postgres policy

| Mechanism | Role |
|-----------|------|
| CI service `postgres:16-alpine` | `LAZUAR_TEST_PG` for soft-skip Billing-style PG tests |
| Testcontainers | Integration suites may start extra Postgres; runner Docker required |
| Soft-skip vs hard-fail | BillingQuery soft-skip if PG down; Commerce Testcontainers hard-fail in OneTimeSetUp |

Documented in `apps/lazuar-api/README.md` § Testing (this phase). No CI service change needed for Ops.

---

## 4. Contracts job honesty — pnpm mismatch

| Source | Version |
|--------|---------|
| Root `package.json` `packageManager` | `pnpm@11.5.2` |
| CI `pnpm/action-setup@v4` (before) | **`version: 9`** |

Risk: lockfile / install behavior drift; frozen lockfile install may fail or resolve differently under pnpm 9 vs 11.

**Fix:** set CI `version: 11.5.2` to match `packageManager`.

.NET for contracts remains `10.0.x` (aligned with API).

---

## 5. Taskfile migration footguns

| Issue | Before | After (this phase) |
|-------|--------|---------------------|
| `api:migrations:add` example | `MODULE=Tenant` (no Tenant module) | `MODULE=Billing` |
| CRM spelling | Undocumented | Desc: context **`CrmDbContext`** → `MODULE=Crm`; folder/csproj remain `CRM` / `Modules.CRM.*`; Linux case-sensitive path note |
| `api:db:migrate` contexts | 9 hardcoded | Unchanged — One, Messaging, Payments, CRM, Ops, Billing, Lhdn, Commerce, Communications |

**CRM path vs context:** generic task uses `{{.MODULE}}` for both `--context {{.MODULE}}DbContext` and `Modules/{{.MODULE}}/...`. `MODULE=CRM` yields wrong type `CRMDbContext`; `MODULE=Crm` yields correct context but folder `Modules/Crm` only works on case-insensitive FS. Documented; no template split this phase.

---

## 6. Actions taken (summary)

1. CI `dotnet`: add `Test (Ops)` → `Modules.Ops.Tests`.
2. CI `contracts`: pnpm **11.5.2**.
3. Taskfile `api:migrations:add` desc: real example + CRM note.
4. `apps/lazuar-api/README.md`: short Testing matrix + `task api:test` pointer.
5. Checklist + analysis/done for phase 06.

---

## 7. Out of scope

- Making CI invoke `task api:test` literally (duplicated steps remain; lists now match).
- Fixing CRM migrations:add template to split folder vs context vars.
- Squashing migrations, Billing hand-rolled DDL vs EF migrate, LHDN sandbox E2E.
- Phase 05 TypeSpec honesty (separate checklist; not mixed into this commit).
