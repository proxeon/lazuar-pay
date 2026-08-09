# Phase 13 — Test fixtures, errors, light shared helpers

**Goal:** Kill copy-paste tax without a framework.  
**Evidence:** `../07-tests-migrations-hygiene.md`, `../09-duplication-tech-debt.md`  
**Status:** Practical subset done 2026-08-09 (`phase-13-done.md`)

---

## 13.1 Shared test support library (start small)

- [x] Create `apps/lazuar-api/tests/Lazuar.TestSupport/` (or under ModuleTests) with:
  - [x] InMemory DbContext factory helpers per module or generic builder — **generic `InMemoryDb.CreateOptions<T>` only** (no per-module factories yet)
  - [x] Fake `IExecutionContextAccessor`
  - [x] Common mediator/mock setup if repeated — **`NullMediator` (Publish no-op)**
- [x] Migrate **2–3** ModuleTests as pilot (not all at once) — **2** (`BroadcastClaimTests`, `DeductTenantCreditIdempotencyTests`)
- [ ] Expand adoption when pattern proven — **partial / deferred** (documented in TestSupport README)

## 13.2 Docker / Postgres test matrix documentation

- [x] Document soft-skip vs hard-fail Testcontainers in test README — **`apps/lazuar-api/README.md` §6 matrix** (Phase 06 note expanded)
- [ ] Align at least one inconsistent test policy if easy (optional) — **not done** (Commerce hard-fail left; documented)
- [x] Ensure CI docs match — same five runners + note that CI has Postgres + Docker

## 13.3 ProblemDetails / exception taxonomy

- [x] Inventory endpoint error return styles (string, anonymous, ProblemDetails, StatusResponse) — **light: documented Payments exemplar vs handler vs anonymous**
- [x] Adopt Payments M2M-style codes as exemplar for **new** endpoints — **documented in README**
- [x] Extend `GlobalExceptionHandler` mapping for domain exceptions you already throw widely — **`code` extensions + split business-rule title**
- [x] Do **not** rewrite every endpoint in one PR — pick One or Commerce public as pilot — **handler-level pilot only; no mass endpoint rewrite**

## 13.4 Pagination consistency

- [x] Grep `PaginatedResponse` usages
- [x] Fix Ops `TotalCount = 0` lie if still present
- [x] Prefer shared helper for page/limit → skip/take — **`BuildingBlocks.Application.Paging`**
- [x] Document `page/limit` vs offset convention in one place — **README §6**

## 13.5 Outbox/inbox DI helper (optional, high value)

- [ ] Design `AddModuleOutboxInbox<TDbContext>()` extension in BuildingBlocks — **deferred**
- [ ] Pilot on one module (e.g. CRM or Messaging) — **deferred**
- [ ] Roll to others only if pilot reduces noise without magic — **deferred**

## 13.6 Gateway small utils

- [ ] Extract duplicated name/amount helpers used by payment adapters (if still duplicated) — **deferred**
- [ ] Keep adapters free of a “mega adapter base class” — **N/A this phase**

## 13.7 Exit criteria

- [x] Pilot tests use shared fixtures
- [x] At least one error-style consistency improvement merged
- [x] Ops pagination totals honest
