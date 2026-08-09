# Phase 13 — Test fixtures, errors, light shared helpers

**Goal:** Kill copy-paste tax without a framework.  
**Evidence:** `../07-tests-migrations-hygiene.md`, `../09-duplication-tech-debt.md`

---

## 13.1 Shared test support library (start small)

- [ ] Create `apps/lazuar-api/tests/Lazuar.TestSupport/` (or under ModuleTests) with:
  - [ ] InMemory DbContext factory helpers per module or generic builder
  - [ ] Fake `IExecutionContextAccessor`
  - [ ] Common mediator/mock setup if repeated
- [ ] Migrate **2–3** ModuleTests as pilot (not all at once)
- [ ] Expand adoption when pattern proven

## 13.2 Docker / Postgres test matrix documentation

- [ ] Document soft-skip vs hard-fail Testcontainers in test README
- [ ] Align at least one inconsistent test policy if easy (optional)
- [ ] Ensure CI docs match

## 13.3 ProblemDetails / exception taxonomy

- [ ] Inventory endpoint error return styles (string, anonymous, ProblemDetails, StatusResponse)
- [ ] Adopt Payments M2M-style codes as exemplar for **new** endpoints
- [ ] Extend `GlobalExceptionHandler` mapping for domain exceptions you already throw widely
- [ ] Do **not** rewrite every endpoint in one PR — pick One or Commerce public as pilot

## 13.4 Pagination consistency

- [ ] Grep `PaginatedResponse` usages
- [ ] Fix Ops `TotalCount = 0` lie if still present
- [ ] Prefer shared helper for page/limit → skip/take
- [ ] Document `page/limit` vs offset convention in one place

## 13.5 Outbox/inbox DI helper (optional, high value)

- [ ] Design `AddModuleOutboxInbox<TDbContext>()` extension in BuildingBlocks
- [ ] Pilot on one module (e.g. CRM or Messaging)
- [ ] Roll to others only if pilot reduces noise without magic

## 13.6 Gateway small utils

- [ ] Extract duplicated name/amount helpers used by payment adapters (if still duplicated)
- [ ] Keep adapters free of a “mega adapter base class”

## 13.7 Exit criteria

- [ ] Pilot tests use shared fixtures
- [ ] At least one error-style consistency improvement merged
- [ ] Ops pagination totals honest
