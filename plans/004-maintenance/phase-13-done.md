# Phase 13 — Done

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Commit subject:** `test(api): shared test support and Ops paging honesty (phase 13)`

## What landed

### 1. `tests/Lazuar.TestSupport/` (library, not a runner)

| Type | Role |
|------|------|
| `FakeExecutionContextAccessor` | Mutable real stand-in for `IExecutionContextAccessor` |
| `InMemoryDb.CreateOptions<T>()` | Unique EF InMemory options |
| `InMemoryDb.NullMediator` | Publish no-op; Send/CreateStream throw |
| `README.md` | Pilot path + when **not** to expand yet |

Registered in `Lazuar.slnx` under `/tests/`. Referenced by `Lazuar.ModuleTests`.

### 2. Pilot ModuleTests (2)

| File | Change |
|------|--------|
| `Communications/BroadcastClaimTests.cs` | Fake + InMemoryDb |
| `Billing/Commands/DeductTenantCreditIdempotencyTests.cs` | Fake + InMemoryDb |

### 3. Ops paging honesty

- `IOpsRepository.CountConversationsAsync` + repository impl  
- `ChatEndpoints` `GET /chat/conversations` uses real `TotalCount` (no more hard-coded `0`)  
- Uses `Paging.NormalizeOffset` for limit/offset clamps  

### 4. Shared `Paging` helper

`BuildingBlocks.Application.Paging` — page/limit and limit/offset normalization.

### 5. Docs (`apps/lazuar-api/README.md` §6)

- Soft-skip vs hard-fail Docker/Postgres matrix table  
- TestSupport row + pilot pointer  
- Pagination convention (`page/limit` preferred; Ops legacy offset)  
- ProblemDetails / Payments M2M exemplar note  

### 6. ProblemDetails pilot

`GlobalExceptionHandler`: split business-rule vs invalid-operation; stable `code` extensions (`business_rule_violation`, `invalid_operation`, `internal_error`).

### Plans

- `phase-13-analysis.md`  
- `checklists/phase-13-test-fixtures-and-errors.md` — honest partials  

## Verification

| Check | Result |
|-------|--------|
| `Lazuar.TestSupport` build | **0 warnings, 0 errors** |
| Ops Infrastructure build | **0 warnings, 0 errors** |
| Pilot ModuleTests (Broadcast + Deduct idempotency) | **5/5 passed** |
| Modules.Ops.Tests | **4/4 passed** |
| Architecture tests | **12/12 passed** |

## Exit criteria

| Criterion | Status |
|-----------|--------|
| Pilot tests use shared fixtures | **Yes** (2 classes) |
| At least one error-style consistency improvement | **Yes** (handler `code` + README) |
| Ops pagination totals honest | **Yes** |

## Explicitly not done (partials)

| Item | Why deferred |
|------|----------------|
| Migrate remaining ModuleTests | Intentional gradual adoption |
| Per-module InMemory factory in TestSupport | Avoid fan-in to all Infrastructure projects |
| Soft-skip policy change for Commerce Testcontainers | Doc-only; hard-fail left as-is |
| Mass ProblemDetails endpoint rewrite | Out of scope |
| `AddModuleOutboxInbox<T>` (13.5) | Optional high-value later |
| Gateway name/amount utils (13.6) | Not touched this phase |
| Force Ops chat onto `page/limit` | Legacy offset kept; documented |

## Next

Phase 14 — TypeSpec structure polish (or remaining god-file partials when touching those files).
