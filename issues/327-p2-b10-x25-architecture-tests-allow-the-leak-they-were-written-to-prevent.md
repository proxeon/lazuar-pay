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

## Evaluation (current tree, 2026-08-18)

### What the bug is
The architecture suite was sold as the lock on tenant isolation, outbox presence, and middleware allowlists. At audit HEAD it was 14 string/NetArch tests that did not prove the production leaks they were named after: no inbox/DI registration, no `AddHostedService` for the outbox type they required, no ban on `IgnoreQueryFilters` without an org predicate, no `IMustHaveTenant` on every `OrganizationId`, no Dapper `@OrgId` scrape, no `RequireAuthorization` scrape, no “every `IIntegrationEvent` has a subscriber.” The empty-tenant filter test is still a substring search for `TenantId == Guid.Empty ||`, so `== default ||` would pass.

### Still present?
**PARTIAL**

161–180 (and the helper from 180) closed several named holes. Current suite is 22 `[Test]` methods (was 14): `ModuleBoundaryTests` 9, `TenantIsolationArchitectureTests` 11, new `IntegrationEventSubscriptionTests` 2.

Now locked (would fail if reintroduced):

- Every module calls `AddModuleOutboxInbox<` (`TenantIsolationArchitectureTests.Every_Module_Registers_OutboxInbox_Via_Helper`, 116–126). Production DI matches (`Commerce/Infrastructure/DependencyInjection.cs` 51 and the other eight modules). The helper **does** `AddHostedService` for both jobs (`ModuleOutboxInboxServiceCollectionExtensions.cs` 26–28).
- Every `IIntegrationEventHandler<T>` has `Subscribe<T, Handler>` and every `IIntegrationEvent` is subscribed or explicitly unused (`IntegrationEventSubscriptionTests.cs` 25–73). Complements 161 (`InMemoryEventBus` now throws on no handlers).
- `MessageDeliveryLog` implements `IMustHaveTenant` (`MessageDeliveryLog.cs` 10; arch test 129–133). 179’s named leak for that type is closed.
- Commerce id lookups that ignore filters must include `OrganizationId` (`TenantIsolationArchitectureTests.CommerceRepository_IgnoreQueryFilters_Id_Lookups_Require_OrganizationId`, 177–187) — 164.
- `excludeIds` must be `<> ALL({0})` not concatenated GUID strings (141–151) — 178.
- Workspace id-scoped maps must contain `HasTenantAccessAsync` (154–165) — 177.
- Boot migrate must not swallow `PendingModelChanges` (168–174) — 175.

Still **not** locked (the original complaint):

```43:61:apps/lazuar-api/tests/Lazuar.ArchitectureTests/TenantIsolationArchitectureTests.cs
    public void PlatformDbContext_Filter_Must_Not_Treat_Empty_Tenant_As_All_Rows()
    {
        // ...
        Assert.That(
            source.Contains("TenantId == Guid.Empty ||", StringComparison.Ordinal)
            || source.Contains("TenantId == Guid.Empty||", StringComparison.Ordinal),
            Is.False,
```

Production filter is still the fail-closed one-liner (`PlatformDbContext.cs` 44–45: `e.OrganizationId == ExecutionContext.TenantId`). A rewrite to `ExecutionContext.TenantId == default ||` would pass this test.

- `All_Modules_Should_Have_OutboxPublisherJob_In_Infrastructure` still only requires a type name ending in `OutboxPublisherJob` (`ModuleBoundaryTests.cs` 181–196). Registration is a *different* test.
- No general scrape of `IgnoreQueryFilters()` without `OrganizationId` outside `CommerceRepository`.
- No scrape of a second `HasQueryFilter` on any DbContext other than Ops (grep of production `*.cs` is still only `PlatformDbContext` + `OpsDbContext`).
- No “every type with `OrganizationId` implements `IMustHaveTenant`.” `ChargeAttemptLog` is still `Entity` only (`ChargeAttemptLog.cs` 6).
- No Dapper “SQL must contain `@OrgId`.”
- No anonymous `MapGroup` / `RequireAuthorization` allowlist scrape. Messaging notify is `OrgAdmin` (`Messaging/Infrastructure/Endpoints.cs` 27) but nothing in ArchitectureTests reads that.

### Related files
- `apps/lazuar-api/tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs` — NetArch layer rules + type-exists outbox.
- `apps/lazuar-api/tests/Lazuar.ArchitectureTests/TenantIsolationArchitectureTests.cs` — source scans; still stringly.
- `apps/lazuar-api/tests/Lazuar.ArchitectureTests/IntegrationEventSubscriptionTests.cs` — 161-era subscriber lock.
- `apps/lazuar-api/BuildingBlocks/Infrastructure/{PlatformDbContext,ModuleOutboxInboxServiceCollectionExtensions}.cs` — filter + DI helper the tests now require.
- `apps/lazuar-api/Modules/Ops/Infrastructure/OpsDbContext.cs` — only production `HasQueryFilter` override.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs` — the only IgnoreQueryFilters shape the arch test names.
- `apps/lazuar-api/Modules/Commerce/Domain/Entities/ChargeAttemptLog.cs` — still unfiltered `OrganizationId` hole.
- Issues 161, 162, 164, 175, 177, 178, 179, 180 (resolved on main).

### Tests
- Existing tests that touch this path: the 22 ArchitectureTests above; `CrmOutboxInboxRegistrationTests`; `LhdnOutboxPublisherJobRegistrationTests` (outbox type only); `ModuleOutboxInboxExtensionsTests`.
- Whether any test would fail if the remaining leak is still there: **no**. The suite is green while `== default ||` is unblocked, Dapper can omit `@OrgId`, and `ChargeAttemptLog` stays untenanted.
- First regression test: compile the Platform filter expression (or forbid `||` next to `TenantId`/`default`); fail CI on `IgnoreQueryFilters()` in a method whose body does not mention `OrganizationId`; assert every public `OrganizationId` property type implements `IMustHaveTenant` except an explicit allowlist (`PaymentWebhookLog`, maybe `ChargeAttemptLog` if product says so).

### Reproduction today
Arrange: change `PlatformDbContext.ConfigureGlobalFilter` to `e.OrganizationId == ExecutionContext.TenantId || ExecutionContext.TenantId == default`. Act: `dotnet test Lazuar.ArchitectureTests`. Assert: `PlatformDbContext_Filter_Must_Not_Treat_Empty_Tenant_As_All_Rows` still passes. Separately, add `IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id)` on a new repository method with no org predicate: ArchitectureTests stay green.

### Blast radius
Engineers, not buyers. A future filter/DI/IgnoreQueryFilters regression can re-open silent cross-tenant reads (P1/P0 in 163–165). Frequency: every PR that touches tenancy and trusts the suite. Money/PII only if the untested leak is reintroduced; `ChargeAttemptLog` is worker-only today.

### Suggested fix
Keep the 161–180 locks. Replace the Empty-tenant string search with an assertion on the actual filter (no `||`, no `default`/`Guid.Empty` disjunct). Add three scrapes: `IgnoreQueryFilters` ⇒ `OrganizationId` in the same method; `HasQueryFilter` only on Platform + Ops; `OrganizationId` property ⇒ `IMustHaveTenant` except an allowlist. Optional: Dapper `@OrgId` and a `RequireAuthorization` allowlist. No TypeSpec regen. No Wave 5.

### Evaluation notes
Severity stays P2 (test honesty, not a live isolation hole). 161–180 already tightened EventBus/TypeResolver/outbox/inbox; this issue is the remaining *test* surface, not those runtime bugs. Duplicate of “arch tests allow leak” in 008 §1.3; not a duplicate of 164/179 themselves. Do not close until the string-search and “every OrganizationId is tenanted” holes are locked or explicitly allowlisted.


