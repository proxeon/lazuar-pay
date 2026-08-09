# Phase 13 — Analysis (test fixtures, errors, light shared helpers)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Goal:** Kill copy-paste tax without a framework; fix Ops paging honesty; document Docker/test matrix; light ProblemDetails improvement.  
**Evidence:** `checklists/phase-13-test-fixtures-and-errors.md`, `07-tests-migrations-hygiene.md`, `09-duplication-tech-debt.md`

---

## 1. Pre-change inventory

### 1.1 Shared fixtures

| Item | Pre state |
|------|-----------|
| Shared test library | **None** — every ModuleTest re-rolled `Substitute.For<IExecutionContextAccessor>()` + `UseInMemoryDatabase(Guid.NewGuid())` |
| `IExecutionContextAccessor` fakes | NSubstitute only (~20+ ModuleTests) |
| InMemory options | Copy-pasted per test class |
| Per-module DbContext factory | Not shared (constructors differ: options + ctx + mediator + jobTrigger) |

### 1.2 Ops pagination lie

| Endpoint | Params | TotalCount |
|----------|--------|------------|
| `GET /ops/chat/conversations` | `limit`, `offset` | **Hard-coded `0`** → `TotalPages = 0` always |

`IOpsRepository` had list-only API; no count method.

### 1.3 Docker / test matrix docs

Phase 06 already added a short Integration/Postgres note in `apps/lazuar-api/README.md`. Gaps:

- Soft-skip vs hard-fail not tabulated by suite
- `Lazuar.TestSupport` did not exist yet
- Pagination / ProblemDetails conventions undocumented

### 1.4 ProblemDetails

| Layer | State |
|-------|-------|
| `GlobalExceptionHandler` | Maps `InvalidOperationException` + `BusinessRuleValidationException` → 400; no stable `code` extension |
| Payments M2M | `IntegrationEndpoints` already uses `ProblemDetails` + `code` — exemplar |
| Mass endpoint rewrite | Out of scope (explicit) |

### 1.5 Optional items (not this PR)

| Item | Decision |
|------|----------|
| `AddModuleOutboxInbox<T>` DI helper | Deferred (13.5) |
| Gateway name/amount extract | Deferred (13.6) unless still duplicated on touch |
| Migrate all ModuleTests | Pilot only (2 classes) |

---

## 2. Target design

### 2.1 `Lazuar.TestSupport` (start small)

```
tests/Lazuar.TestSupport/
  Lazuar.TestSupport.csproj   # IsTestProject=false; refs BB.Application + BB.Infrastructure
  FakeExecutionContextAccessor.cs
  InMemoryDb.cs               # CreateOptions<T> + NullMediator (Publish no-op)
  README.md                   # pilot path + expansion rules
```

**Not included:** per-module factory methods that would force TestSupport to reference every Infrastructure project.

### 2.2 Pilot migrations

| Test | Why low friction |
|------|------------------|
| `BroadcastClaimTests` | Single CreateDb helper; empty tenant + no Send |
| `DeductTenantCreditIdempotencyTests` | Same pattern on BillingDbContext |

### 2.3 Ops TotalCount honesty

1. `IOpsRepository.CountConversationsAsync`
2. `OpsRepository` implementation (same filter as list)
3. Endpoint uses `Paging.NormalizeOffset` + real total

### 2.4 Shared `Paging` (production)

`BuildingBlocks.Application.Paging` — `Normalize(page, limit)` and `NormalizeOffset(limit, offset)`.

### 2.5 ProblemDetails pilot

Split handler mapping for business rule vs invalid operation; add `code` extension keys aligned with Payments style. Document exemplar in README.

---

## 3. Risks

| Risk | Mitigation |
|------|------------|
| NullMediator breaks tests that Send | Throws on Send/CreateStream; pilots only Publish |
| Parallel builds file lock | Re-run; no code issue |
| Changing GlobalExceptionHandler Title/code | Additive; clients relying only on status still work |
| Ops offset → page math for TotalPages | Same as before, but TotalCount now real |

---

## 4. Explicit non-goals

- Full ModuleTests migration  
- Cursor pagination redesign for Ops  
- Force `page/limit` on Ops chat in this PR (legacy offset kept; documented)  
- Outbox/inbox DI helper  
- Endpoint-by-endpoint error rewrite  
