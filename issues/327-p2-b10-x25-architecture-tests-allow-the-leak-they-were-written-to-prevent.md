---
number: "327"
id: B10-X25
severity: P2
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 327 — B10-X25 — Architecture tests allow the leak they were written to prevent

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X25 — P2 — Architecture tests allow the leak they were written to prevent

What they lock (14 tests):

- Domain isolation, Application ↛ Infrastructure, Contracts-only cross-module, OutboxPublisherJob **type exists**, BuildingBlocks ↛ Modules, SharedKernel empty, Domain ↛ BB Application/Infrastructure, ports live in BB Application, host csproj ↛ `*Application`.
- Platform filter must not contain `TenantId == Guid.Empty ||`.
- Ops override must contain `OrganizationId == ExecutionContext.TenantId`.
- Middleware require/exempt string lists.
- Draft vs final HMAC payload differ.

What they do **not** lock:

- Inbox job type or DI registration (except CRM’s separate test).
- `AddHostedService` for the outbox type they require.
- `IgnoreQueryFilters` without `OrganizationId`.
- Second `HasQueryFilter` on any DbContext other than Ops.
- `IMustHaveTenant` on every `OrganizationId` property.
- Anonymous `MapGroup` allowlist (Messaging notify is now `OrgAdmin`, but the arch test does not scrape `RequireAuthorization`).
- Dapper SQL must contain `@OrgId`.
- Every `IIntegrationEvent` has a subscriber.

`PlatformDbContext_Filter_Must_Not_Treat_Empty_Tenant_As_All_Rows` is a **string search**. A future filter written as `ExecutionContext.TenantId == default ||` would pass.

