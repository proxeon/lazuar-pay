# 09 — Architecture, tenancy, workers, and tests (after Waves 0–4)

**Date:** 16 August 2026  
**Product:** Lazuar Pay  
**Codebase:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Slice:** modular monolith boundaries, per-module outbox/inbox, background workers, tenant query filters + IDOR tests, EF migrations on boot, Wave 3/4 schema, test inventory, Wave 3/4 holes, billing claim-loop ops risk.

This report is the **code as it is** after Waves 0–4. `plans/007-feats` tracker cells are historical. Wave 3/4 `*-done.md` notes are used only as pointers to files that were then re-read.

---

## 0. How this slice is organized in the repo

The API is a single ASP.NET Core host (`apps/lazuar-api/src/Lazuar.Api`) that composes nine modules. There is no process-per-module extract. Each module owns a PostgreSQL schema and an EF `*DbContext`. Cross-module writes go through **Contracts** (commands + integration events). Cross-module *async* delivery is intended to be **outbox → in-process bus → handler**, not a broker.

Physical layout:

| Layer | Path |
|-------|------|
| Host | `apps/lazuar-api/src/Lazuar.Api/` |
| BuildingBlocks | `apps/lazuar-api/BuildingBlocks/{Domain,Application,Infrastructure}/` |
| SharedKernel | `apps/lazuar-api/SharedKernel/` (empty marker) |
| Modules | `apps/lazuar-api/Modules/{One,Messaging,CRM,Payments,Ops,Billing,Lhdn,Commerce,Communications}/` |
| Architecture tests | `apps/lazuar-api/tests/Lazuar.ArchitectureTests/` |
| Module tests | `apps/lazuar-api/tests/Lazuar.ModuleTests/` |
| Integration tests | `apps/lazuar-api/tests/Lazuar.IntegrationTests/` |
| Legacy module test projects | `apps/lazuar-api/tests/Modules.Billing.Tests/`, `Modules.Ops.Tests/` |
| Shared test fakes | `apps/lazuar-api/tests/Lazuar.TestSupport/` |

Module internal layers (except CRM, which has no Application project):

- `*.Domain` — aggregates, entities, `IMustHaveTenant` types
- `*.Application` — MediatR handlers, ports
- `*.Infrastructure` — EF, endpoints, workers, event handlers, DI
- `*.Contracts` — commands, integration events, query-service interfaces consumed by other modules

The host composes **Infrastructure only**. That is both a comment on `Lazuar.Api.csproj` and a NetArchTest.

```18:22:apps/lazuar-api/src/Lazuar.Api/Lazuar.Api.csproj
    <!-- Host composes modules via Infrastructure entrypoints only. Application assemblies are
         transitive (Infrastructure → Application) and used for MediatR assembly markers; do not
         add direct *Application.csproj refs here (Phase 17.5 / ADR 001 alignment). -->
    <ProjectReference Include="..\..\Modules\Commerce\Infrastructure\Modules.Commerce.Infrastructure.csproj" />
```

`Program.cs` is composition + boot migrations. It does not contain endpoint maps.

```224:238:apps/lazuar-api/src/Lazuar.Api/Program.cs
builder.Services.AddLazuarMediatR();
builder.Services.AddAllModules(builder.Configuration);

var app = builder.Build();

// First boot / empty Neon: apply EF migrations for every module schema before hosted services run.
await app.MigrateAllModuleDatabasesAsync();

app.UseLazuarPipeline();
app.UseAllModuleSubscriptions();
app.UseHostEventSubscriptions();
app.MapHealthEndpoints();
app.MapAllModuleEndpoints();

await app.RunAsync();
```

`AddAllModules` order is One → Messaging → CRM → Payments → Ops → Billing → Lhdn → Commerce → Communications (`ModuleRegistrationExtensions.cs` 24–33). Subscriptions and endpoint maps follow the same order, except CRM has no HTTP surface and platform routes sit under `/api/v1/platform` with `SUPER_ADMIN`.

There is **no frontend test project** that participates in this architecture story. The only frontend test file found under portal is `apps/lazuar-portal/src/modules/checkout/i18n/i18n.test.mjs` (locale strings). Ops / admin / developers have no `*.test.ts` / `*.spec.ts` inventory. Architecture, tenancy, workers, and Wave 3/4 money loops live or die in the .NET test suite.

---

## 1. Modular monolith boundaries (NetArchTest)

### 1.1 What the architecture tests actually load

`Lazuar.ArchitectureTests` is a NetArchTest + NUnit project. It force-loads every module assembly in a static constructor because ProjectReferences do not load into the AppDomain until a type is touched:

```49:95:apps/lazuar-api/tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs
    static ModuleBoundaryTests()
    {
        // Force-load module assemblies. ProjectReferences alone do not load into AppDomain
        // until a type is touched; NetArchTest requires the assemblies to be present.
        Assembly[] anchors =
        [
            typeof(Modules.One.Domain.GlobalUser).Assembly,
            typeof(Modules.One.Application.DependencyInjection).Assembly,
            typeof(Modules.One.Infrastructure.DependencyInjection).Assembly,
            // ... Messaging, CRM (Domain+Infrastructure only), Payments, Ops,
            // Billing, Lhdn, Commerce, Communications ...
        ];
```

The nine namespaces under test:

```23:34:apps/lazuar-api/tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs
    private static readonly string[] ModuleNamespaces =
    [
        "Modules.One",
        "Modules.Messaging",
        "Modules.CRM",
        "Modules.Payments",
        "Modules.Ops",
        "Modules.Billing",
        "Modules.Lhdn",
        "Modules.Commerce",
        "Modules.Communications"
    ];
```

CRM is explicitly allowed to have no Application layer:

```36:41:apps/lazuar-api/tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs
    /// <summary>
    /// Modules intentionally without an Application layer (Infrastructure hosts handlers/ports).
    /// </summary>
    private static readonly HashSet<string> ModulesWithoutApplication = new(StringComparer.Ordinal)
    {
        "Modules.CRM"
    };
```

That exception is honest: CRM command handlers (`CreateClientProfileCommandHandler`, `ResolveClientProfileCommandHandler`, `AnonymizeClientProfileCommandHandler`) live in `Modules/CRM/Infrastructure/`.

### 1.2 Rules that are enforced (9 tests in `ModuleBoundaryTests`)

**Domain isolation** (`Domain_Should_Remain_Completely_Isolated`, lines 97–119). Each `*.Domain` assembly must not depend on any other module namespace, nor on its own `.Infrastructure` or `.Application`. Domain may depend on `BuildingBlocks.Domain` (not asserted here; asserted separately).

**Application must not reference its own Infrastructure** (`Application_Should_Not_Reference_Infrastructure`, lines 121–142). Skips CRM.

**Outer layers talk through Contracts only** (`Outer_Layers_Should_Only_Reference_Other_Modules_Through_Contracts`, lines 144–178). For each module, Application and Infrastructure must not depend on another module’s Domain / Application / Infrastructure. They *may* depend on `Modules.X.Contracts`. This is the rule that makes Commerce workers able to call `ICrmQueryService` / `IOneQueryService` without taking a project reference on CRM/One Infrastructure.

**Every module Infrastructure must define a concrete `*OutboxPublisherJob`** (`All_Modules_Should_Have_OutboxPublisherJob_In_Infrastructure`, lines 180–197). The test looks for a non-abstract type whose name ends with `OutboxPublisherJob`. It does **not** require an `*InboxConsumerJob`. It does **not** require the job to be registered in DI. Registration is a separate, weaker, CRM-only test.

**BuildingBlocks must not reference Modules.*** (`BuildingBlocks_Must_Not_Reference_Module_Assemblies`, lines 240–264). Scans Domain, Application, and Infrastructure BuildingBlocks assemblies against `Modules.*` including Contracts. This is why LLM/email/metrics work that used to live in BB was moved (R30–R35): the arch test would fail if BB referenced a module.

**SharedKernel is an empty marker** (`SharedKernel_Must_Not_Reference_Modules_Or_Contain_Entity_Types`, lines 269–297). Forbidden: any `Modules.*`, plus `BuildingBlocks.Application` and `BuildingBlocks.Infrastructure`. Also no `Entity` / `IAggregateRoot` subclasses. The assembly today is one file:

```1:12:apps/lazuar-api/SharedKernel/SharedKernelMarker.cs
namespace SharedKernel;

/// <summary>
/// Intentional empty assembly marker (Phase 15).
/// Used for architecture-test assembly scanning and as a ProjectReference anchor from module Domain projects.
/// SharedKernel must remain free of write-model business entities.
```

**Module Domain must not reference BuildingBlocks.Application or BuildingBlocks.Infrastructure** (`Module_Domain_Should_Not_Reference_BuildingBlocks_Application_Or_Infrastructure`, lines 302–318). Domain may use `BuildingBlocks.Domain` (`Entity`, `IMustHaveTenant`, `IDomainEvent`).

**Shared technical ports live in BuildingBlocks.Application** (`Shared_Technical_Ports_Must_Live_In_BuildingBlocks_Application`, lines 324–366). Required interfaces: `IJwtService`, `IR2StorageService`, `ITokenGeneratorService`, `ISecretVault`, `IPasswordService`, `ISqlConnectionFactory`. The same simple names must not reappear as public interfaces on BuildingBlocks.Infrastructure.

**Host csproj must not ProjectReference `Modules.*.Application`** (`Host_Csproj_Must_Not_Directly_Reference_Module_Application_Projects`, lines 373–402). Parses `Lazuar.Api.csproj` as text.

### 1.3 What NetArchTest does **not** enforce

These are not theoretical. They are visible in the code today:

1. **Inbox jobs are not required.** A module can ship an OutboxPublisherJob (satisfies the name test) and never consume an inbox. That is the live state of every module except Messaging (see §2).
2. **DI registration is not required** except CRM’s dedicated registration test. A leftover `*OutboxPublisherJob` type would pass even if `AddHostedService` were removed.
3. **Contracts can still leak domain shapes.** NetArchTest only looks at assembly references. A Contracts project that referenced Domain would fail if Infrastructure referenced that Domain assembly *through* Contracts — but Contracts projects in this repo typically duplicate DTOs / command records rather than re-export aggregates.
4. **Cross-schema SQL is invisible.** Dapper queries in `CommerceQueryService`, `BillingQueryService`, CRM lookup from Commerce, and LHDN metrics SQL are not architecture-tested. The ownership doc says this explicitly (`docs/009-building-blocks-ownership.md` line 21): architecture tests “do **not** catch conceptual leakage (schema names, LHDN SQL) inside BB.”
5. **Host may still reference module Contracts and Infrastructure types** (it must, to map endpoints and subscribe events). `UseHostEventSubscriptions` binds One events to host handlers (`ModuleRegistrationExtensions.cs` 57–62).
6. **CRM Application-less shape is frozen by the exception set.** Extracting CRM Application later requires changing the arch test, not just adding a project.
7. **No rule that workers IgnoreQueryFilters.** Fail-closed tenant filters plus empty ambient tenant on hosted services is a *runtime* contract (`PlatformDbContext.cs` 43–45). The arch test only string-searches `PlatformDbContext` and `OpsDbContext` source for fail-open patterns (see §4).

### 1.4 Tenant isolation architecture tests (5 more tests)

`TenantIsolationArchitectureTests` is source-text + middleware unit tests, not NetArchTest:

| Test | What it locks |
|------|----------------|
| `PlatformDbContext_Filter_Must_Not_Treat_Empty_Tenant_As_All_Rows` | Forbids `TenantId == Guid.Empty \|\|` fail-open. Requires `OrganizationId == ExecutionContext.TenantId`. |
| `OpsDbContext_HasQueryFilter_Override_Must_Include_OrganizationId` | OpsConversation soft-delete override must still include org match. |
| `TenantSecurityMiddleware_Requires_Tenant_For_OrgAdmin_Modules` | `/admin/commerce`, `/lhdn/documents`, `/ops/stream`, `/messaging/notify`, `/one/storage/...`, `/one/api-keys` require tenant. |
| `TenantSecurityMiddleware_Exempts_Public_Auth_Webhooks_And_Workspace_Surfaces` | `/health`, `/public/*`, `/webhooks/*`, `/one/auth`, `/one/public`, `/one/me`, `/one/workspaces`, integrator provision. |
| `DocumentLinkSigner_Draft_And_Final_Payloads_Differ` | Draft HMAC payload is `slug:draft:id:exp`; final is `slug:id:exp`. |

These are useful guardrails. They do **not** walk every `HasQueryFilter` override in every DbContext. Only Platform (base) and Ops are source-scanned. Commerce, Billing, Lhdn, One, Payments, Communications, Messaging, CRM rely on the generic `IMustHaveTenant` loop in `PlatformDbContext.OnModelCreating`.

Architecture test count: **2 files, 14 `[Test]` methods** (9 module-boundary + 5 tenant-isolation).

---

## 2. BuildingBlocks and the outbox/inbox spine

### 2.1 What BuildingBlocks is allowed to be

Policy (`docs/009-building-blocks-ownership.md`): technical spine only. Live contents after the maintenance moves:

**Domain:** `Entity`, `ValueObject`, `IAggregateRoot`, `IBusinessRule`, `IDomainEvent`, `IMustHaveTenant`, `BusinessRuleValidationException`, `GenericBusinessRule`.

**Application:** CQRS interfaces (`ICommand`, `IQuery`, handlers), `IEventBus` / `IIntegrationEvent` / `IIntegrationEventHandler` / `IEventBusSubscriptions`, `IExecutionContextAccessor`, `IJwtService`, `IR2StorageService`, `ISecretVault`, `ISqlConnectionFactory`, `ITokenGeneratorService`, `IPasswordService` (declared on `CQRS.cs` 29–34), paging, `MarkdownParser`, observability ports (`IOutboxSchemaRegistration`, `IPlatformMetricsContributor`, `LazuarMetrics`).

**Infrastructure:** `PlatformDbContext`, outbox/inbox types + jobs + DI helper, `InMemoryEventBus`, `OutboxEventBus<T>`, `TypeResolver`, `DatabaseJobTrigger`, `MessageProcessingResultApplier`, `MessageRetryPolicy`, `AesSecretVault`, `JwtService`, `PasswordService`, `NpgsqlConnectionFactory`, `R2StorageService`, `DocumentLinkSigner`, `GlobalExceptionHandler`, `BackgroundWorkerOptions`, platform metrics collector + refresh job, health readiness.

`IExecutionContextAccessor` is implemented by the **host**, not BuildingBlocks:

```16:26:apps/lazuar-api/src/Lazuar.Api/ExecutionContextAccessor.cs
    public Guid TenantId
    {
        get
        {
            if (_httpContextAccessor.HttpContext?.Items.TryGetValue("TenantId", out var tenantIdObj) == true && tenantIdObj is Guid tenantId)
            {
                return tenantId;
            }
            return Guid.Empty;
        }
    }
```

Hosted services have no HTTP context, so `TenantId` is `Guid.Empty` unless a worker sets it. Combined with fail-closed filters, that is why every worker that touches tenant tables must `IgnoreQueryFilters()` and filter `OrganizationId` itself.

### 2.2 Outbox write path

Modules register a **keyed** `OutboxEventBus<TDbContext>`:

```7:27:apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxEventBus.cs
public sealed class OutboxEventBus<TDbContext> : IEventBus where TDbContext : DbContext
{
    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : IIntegrationEvent
    {
        var outboxMessage = new OutboxMessage
        {
            Id = @event.Id,
            Type = @event.GetType().AssemblyQualifiedName ?? @event.GetType().FullName!,
            Data = JsonSerializer.Serialize(@event, @event.GetType()),
            OccurredOn = @event.OccurredOn
        };

        await _dbContext.Set<OutboxMessage>().AddAsync(outboxMessage);
    }
}
```

The row is **not** flushed here. It rides the same `SaveChangesAsync` as the business write. `PlatformDbContext.SaveChangesAsync` then pokes waiters:

```102:111:apps/lazuar-api/BuildingBlocks/Infrastructure/PlatformDbContext.cs
        var result = await base.SaveChangesAsync(cancellationToken);

        // 4. Trigger background outbox/inbox workers instantly on success
        if (result > 0)
        {
            JobTrigger.Trigger();
        }

        return result;
```

`DatabaseJobTrigger` is a process-local TCS swap (`DatabaseJobTrigger.cs` 7–26). It wakes jobs in **this process only**. Multi-instance correctness depends on the SQL `FOR UPDATE SKIP LOCKED` poll, not on the trigger.

### 2.3 Outbox publisher (every module)

`OutboxPublisherJob<TDbContext>` (`OutboxPublisherJob.cs` 13–114):

- Loop until cancelled.
- Open a transaction.
- `SELECT * FROM "{schema}"."{table}" WHERE "ProcessedAt" IS NULL AND (NextAttemptAt IS NULL OR <= NOW()) AND OccurredOn <= NOW() ORDER BY OccurredOn LIMIT 20 FOR UPDATE SKIP LOCKED`.
- Deserialize via `TypeResolver` (assembly-qualified name, then full-name scan of loaded assemblies).
- Publish onto the **singleton** `InMemoryEventBus` (not the keyed outbox bus).
- `MessageProcessingResultApplier.ApplySuccess` or `ApplyFailure`.
- If the batch was non-empty, `Task.Yield()` and immediately poll again (drain). If empty, wait on `DatabaseJobTrigger` with a 5-second cancel.

Retry policy (`MessageProcessingStatus.cs` 9–15, tested in `MessageRetryPolicyTests.cs`):

- `MaxAttempts = 5`
- Backoff after increment: `2^n` minutes → 2, 4, 8, 16, then dead on the 5th failure
- Dead letters set `Status = Dead`, `ProcessedAt = now`, increment `LazuarMetrics.RecordDeadLetter()`

The 5-second poll interval is **hard-coded** on the job (`_pollInterval = TimeSpan.FromSeconds(5)`). It is not in `BackgroundWorkerOptions`.

### 2.4 Inbox consumer (every module has the type; almost nobody writes rows)

`InboxConsumerJob<TDbContext>` (`InboxConsumerJob.cs` 14–109) is the same shape: `LIMIT 20 FOR UPDATE SKIP LOCKED`, then `IMediator.Publish` if the payload is `INotification`. Integration events already implement `INotification` (`IEventBus.cs` 5–8).

Who **writes** `InboxMessage` rows? Grep of `new InboxMessage` in production code finds **only Messaging**:

- `Modules/Messaging/Infrastructure/EventHandlers/TenantProvisionedIntegrationEventHandler.cs` 19–27
- `TenantUpdatedIntegrationEventHandler.cs` 19–26
- `WorkspaceUpdatedIntegrationEventHandler.cs` 20–27

Those handlers are subscribed on the **in-process** bus (`AddMessagingModule` / `UseMessagingSubscriptions`). Flow for a tenant provision:

1. One writes `TenantProvisionedIntegrationEvent` to `one.OutboxMessages`.
2. `OneOutboxPublisherJob` publishes it on `InMemoryEventBus`.
3. `TenantProvisionedIntegrationEventHandler` inserts a row into `messaging.InboxMessages`.
4. `MessagingInboxConsumerJob` dequeues it and `mediator.Publish`es, which runs `TenantProvisionedSeedingHandler` / `TenantCreatedEventHandler` (`INotificationHandler<TenantProvisionedIntegrationEvent>`).

That is the **only** module that uses inbox as a second hop. Commerce, Billing, Payments, Lhdn, Communications, One, Ops, CRM all register `*InboxConsumerJob` hosted services that poll empty tables every 5 seconds (or on `JobTrigger`). CRM’s comment is explicit:

```13:15:apps/lazuar-api/Modules/CRM/Infrastructure/CrmDbContext.cs
    // Outbox/Inbox tables to satisfy platform job patterns
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;
```

Cross-module consumers of payment/commerce/billing events are **direct** `IIntegrationEventHandler<T>` registrations on `InMemoryEventBus` (see each `Use*Subscriptions`). They run in the same process that drained the producer outbox. They do **not** go through the consumer module’s inbox. Consequence: if you scale to N API replicas, a given outbox row is processed once (SKIP LOCKED), and its handlers run **only on the replica that claimed the outbox row**. That is fine while handlers are idempotent and do not assume sticky in-memory state. It is not a durable inbox per subscriber.

`DispatchMessageIntegrationEventHandler` (Messaging) is **not** an inbox hop. It is a direct in-process handler that sends Resend / console WhatsApp (`DispatchMessageIntegrationEventHandler.cs` 55–186). Email failures throw so the **producer** outbox retries.

### 2.5 Per-module registration (live)

Shared helper exists but is used by **CRM only**:

```17:29:apps/lazuar-api/BuildingBlocks/Infrastructure/ModuleOutboxInboxServiceCollectionExtensions.cs
    public static IServiceCollection AddModuleOutboxInbox<TDbContext, TOutboxJob, TInboxJob>(
        this IServiceCollection services,
        string eventBusKey)
        ...
        services.AddKeyedScoped<IEventBus, OutboxEventBus<TDbContext>>(eventBusKey);
        services.AddHostedService<TOutboxJob>();
        services.AddHostedService<TInboxJob>();
```

| Module | Event bus key | Outbox job | Inbox job | Helper? | Other hosted services |
|--------|---------------|------------|-----------|---------|------------------------|
| One | `OneEventBus` | `OneOutboxPublisherJob` | `OneInboxConsumerJob` | no | `SystemGenesisBootstrapperJob`, `OutboundWebhookDispatcherJob` |
| Messaging | `MessagingEventBus` | `MessagingOutboxPublisherJob` | `MessagingInboxConsumerJob` | no | — |
| CRM | `CrmEventBus` | `CrmOutboxPublisherJob` | `CrmInboxConsumerJob` | **yes** (`DependencyInjection.cs` 29) | — |
| Payments | `PaymentsEventBus` | `PaymentsOutboxPublisherJob` | `PaymentsInboxConsumerJob` | no | — |
| Ops | `OpsEventBus` | `OpsOutboxPublisherJob` | `OpsInboxConsumerJob` | no | — (`UseOpsSubscriptions` is a no-op, line 49–52) |
| Billing | `BillingEventBus` | `BillingOutboxPublisherJob` | `BillingInboxConsumerJob` | no | `B2cConsolidationJob`; `RevenueRecognitionJob` **commented out** |
| Lhdn | `LhdnEventBus` | `LhdnOutboxPublisherJob` | `LhdnInboxConsumerJob` | no | `LhdnSubmissionJob`, `LhdnStatusPollingJob`, `LhdnReferenceDataSeederJob` |
| Commerce | `CommerceEventBus` | `CommerceOutboxPublisherJob` | `CommerceInboxConsumerJob` | no | `BillingEngineJob`, `DunningEngineJob`, `CheckoutSessionExpiryJob`, `InvoiceReminderJob` |
| Communications | `CommunicationsEventBus` | `CommunicationsOutboxPublisherJob` | `CommunicationsInboxConsumerJob` | no | `BroadcastFanoutJob` |

Host-level extras (`Program.cs`): `PlatformMetricsRefreshJob` always; `LegacyApiKeyMigrationHostedService` and `LegacyWebhookSubscriptionMigrationHostedService` only when their `Enabled` flags are true.

EF mapping of outbox/inbox tables: CRM uses `modelBuilder.ApplyOutboxInbox()` (`CrmDbContext.cs` 36). Every other module **inlines** the same `ToTable` + filtered index (e.g. `BillingDbContext.cs` 179–191, `OpsDbContext.cs` 49–61, `CommerceDbContext` similarly after line 270). The helper comment says filter SQL must stay byte-identical to avoid migration noise (`OutboxInboxModelBuilderExtensions.cs` 6–7). The inline copies are a drift risk; only CRM is on the helper.

Each module stores EF history in its own schema, e.g. Commerce:

```32:36:apps/lazuar-api/Modules/Commerce/Infrastructure/DependencyInjection.cs
        services.AddDbContext<CommerceDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "commerce");
            }));
```

Messaging is the only module that reads `ConnectionStrings:MessagingConnection` instead of `Default` (`Messaging/Infrastructure/DependencyInjection.cs` 24). In `appsettings.json` both strings point at the same `lazuar_mvp` database (lines 18–21). The split is leftover from a would-be separate messaging DB; it is not a second cluster today.

### 2.6 In-process bus after outbox

`InMemoryEventBus` (`InMemoryEventBus.cs` 13–63) keys handlers by **runtime type name** (`@event.GetType().Name`), not the compile-time generic. That avoids the “publish as `IIntegrationEvent` and dispatch to the wrong overload” bug. Handlers are resolved from a new DI scope per publish. Duplicate subscribe is locked (`AvoidDuplicateAdd`).

There is no out-of-process broker. Restarting the process mid-handler relies on outbox retry (unprocessed row) or inbox retry (Messaging only). Handlers that already committed side effects must be idempotent. Several are (webhook business keys, credit deduction logs, dispute unique index). Several Wave 3/4 paths are not proven (see §8).

---

## 3. Background workers (the ones this eval asked for, plus the rest)

`BackgroundWorkerOptions` (`BuildingBlocks/Infrastructure/Configuration/BackgroundWorkerOptions.cs`) binds section `Workers`:

| Option | Default | Used by |
|--------|---------|---------|
| `OutboundWebhookInterval` | 10s | `OutboundWebhookDispatcherJob` |
| `BroadcastFanoutInterval` | 10s | `BroadcastFanoutJob` |
| `LhdnSubmissionInterval` | 5s | `LhdnSubmissionJob` |
| `LhdnStatusPollingInterval` | 10s | `LhdnStatusPollingJob` |
| `BillingEngineInterval` | 1h | `BillingEngineJob` |
| `DunningEngineInterval` | 1h | `DunningEngineJob` |
| `ClaimLeaseDuration` | 2m | LHDN submit/poll + outbound webhook claim |

`appsettings.json` 106–114 repeats those values. **Not** in this options type: invoice reminder interval (hard-coded 1 hour), checkout expiry interval (hard-coded 5 minutes), B2C consolidation schedule (28th 02:00 MYT), outbox/inbox 5s poll, metrics refresh (Observability section, default 30s).

### 3.1 `BillingEngineJob` (Commerce) — the money loop

Registered at `Commerce/Infrastructure/DependencyInjection.cs` 55. Source: `Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs`.

Hosted loop (45–61): every `BillingEngineInterval` (1 hour), call `ProcessBillingAsync`. `RunOnceAsync` is the test hook (64).

`ProcessBillingAsync` (66–118) is a **serial claim loop**, `BatchSize = 50`:

1. New DI scope per iteration (new DbContext).
2. If relational: `BeginTransaction` + `ClaimDueSubscriptionAsync` (`FOR UPDATE SKIP LOCKED` one row).
3. Else (EF InMemory tests): `ClaimDueSubscriptionInMemoryAsync`.
4. `ProcessOneSubscriptionAsync`; `SaveChanges`; commit.
5. On exception: add `sub.Id` to `failedIds`, rollback, continue.

Claim SQL (129–143):

```sql
SELECT * FROM commerce."Subscriptions"
WHERE "NextBillingDate" IS NOT NULL
  AND "NextBillingDate" <= NOW()
  AND "Status" NOT IN ('PENDING', 'PAST_DUE', 'SUSPENDED', 'CANCELED')
  {exclude failedIds}
ORDER BY "NextBillingDate"
LIMIT 1
FOR UPDATE SKIP LOCKED;
```

There is **no** `CollectionPausedUntil` predicate. There is **no** lease column. There is **no** “already claimed this cycle” flag other than `failedIds` in process memory.

`ProcessOneSubscriptionAsync` then:

- Loads product with `IgnoreQueryFilters` (176). Missing product → `failedIds.Add`, return.
- Skips `one_time` interval → `failedIds.Add`, return.
- **Collection pause** (193–197): if `sub.IsCollectionPaused(UtcNow)`, **log and `return`**. Does **not** add to `failedIds`. Does **not** advance `NextBillingDate`. Does **not** write a lease.
- `CancelAtPeriodEnd` → `Cancel()` + `SubscriptionCanceledIntegrationEvent`, return.
- Apply pending plan / quantity, compute charge.
- If vault + `SupportsOffSession`: write `ChargeAttemptLog` attempt 1 only, publish `ExecuteOffSessionChargeIntegrationEvent`, **return without changing `NextBillingDate` or status**.
- Else: mint renewal checkout (or warn), `MarkAsPastDue()`, start past-due dunning, publish fulfillment + outbound webhook.

`IsCollectionPaused` is a flag on an otherwise ACTIVE subscription (`Subscription.cs` 171–199). W3-LP-057-done says this is intentional: “status does not become SUSPENDED or PAUSED.”

**The pause claim loop (live bug / ops risk).** Because claim SQL still selects a due ACTIVE paused row, and the skip path does not put the id in `failedIds`, the next of the 50 iterations claims the **same row** again. On InMemory this is deterministic. On Postgres, `FOR UPDATE SKIP LOCKED` releases at commit; the next iteration’s new transaction sees the same due paused row first (`ORDER BY NextBillingDate`). One paused-and-due subscription can consume the entire batch of 50 no-op claims. Other due subscriptions wait until the next hourly tick.

The existing test **documents** the stuck `NextBillingDate` and does not assert batch progress:

```582:601:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs
    public async Task RunOnce_CollectionPaused_SkipsChargeAndKeepsActive()
    {
        ...
        sub.PauseCollection(DateTime.UtcNow.AddDays(10));
        ...
        await _job.RunOnceAsync(CancellationToken.None);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("ACTIVE");
        reloaded.NextBillingDate.Should().BeCloseTo(DateTime.UtcNow.AddDays(-1), TimeSpan.FromMinutes(2));
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<ExecuteOffSessionChargeIntegrationEvent>());
    }
```

That test passes whether the job claimed the row once or 50 times. There is no test that a second due subscription in the same `RunOnce` is processed while another is paused. There is no test of `ClaimDueSubscriptionAsync` SQL against Postgres. Dunning **does** exclude collection pause in SQL (see 3.2); billing does not. W3-LP-057-done line 8 (“Billing and pre-dunning skip while paused”) is half-true: billing skips **after** claim.

The same `failedIds` hole exists for the auto-debit success path: after dispatching attempt 1 the method returns without adding the id. A second iteration would re-claim the same ACTIVE due row. The charge is gated by `attemptCount == 0` (247–274), so it will not double-dispatch in the same billing date. It **will** burn remaining batch slots on a subscription that is already waiting on the gateway. That is a milder form of the same claim loop.

`ChargeAttemptLog` is **not** `IMustHaveTenant` (`ChargeAttemptLog.cs` 6). The count query at line 247–248 does not need `IgnoreQueryFilters`. Subscriptions and Products do.

HTTP surface for pause: `POST /admin/commerce/subscribers/{id}/collection/pause|resume` (`SubscriberEndpoints.cs` 212–243) sends `PauseCollectionCommand` / `ResumeCollectionCommand` with `ctx.TenantId`. Handlers (`ChangePlanCommandHandler.cs` 89–138) refuse foreign `OrganizationId` with `"Subscription not found."`. There is **no** `PauseCollectionCommandHandler` test and no IDOR test for pause/resume (see §8).

`BillingEngineJobTests` has 19 `[Test]` methods covering past-due independence, skip of PAST_DUE/SUSPENDED/CANCELED/PENDING/future, reminder-only, vault charge, trial, pause skip, pending plan, quantity. All InMemory. None exercise `FOR UPDATE SKIP LOCKED`. None exercise the 50-iteration starve.

### 3.2 `DunningEngineJob` (Commerce)

Registered at DI 56. Split across `DunningEngineJob.cs` + `.Claim.cs` + `.Dispatch.cs` + `.PastDue.cs` + `.PreDunning.cs`.

Loop: every `DunningEngineInterval` (1 hour), load all active campaigns with `IgnoreQueryFilters` (66–73), then `ProcessClaimedBatchAsync` for `ClaimMode.PreDunning` and `ClaimMode.PastDue` (`DunningEngineJob.cs` 77–87).

Claim SQL (`DunningEngineJob.Claim.cs` 101–127) **does** exclude collection pause on pre-dunning:

```
STATUS = 'ACTIVE'
AND CancelAtPeriodEnd IS NOT TRUE
AND (CollectionPausedUntil IS NULL OR <= NOW())
AND NextBillingDate in (NOW, NOW+14d)
FOR UPDATE SKIP LOCKED
```

Past-due claim excludes `DunningPausedUntil`, not collection pause (PAST_DUE is already not collection-paused as a product action — `PauseCollection` throws if status ≠ ACTIVE, `Subscription.cs` 173–176).

Unlike billing, the claim loop tracks **both** `failedIds` and `processedIds` (`Claim.cs` 30–31, 41–42, 75). A successfully processed subscription is excluded from the next claim in the same cycle. That is why dunning does not have the pause re-claim bug: paused ACTIVE rows never enter the pre-dunning result set.

`DunningEngineJobTests` is the largest worker suite: **40** `[Test]` methods (lines 78–1196). InMemory only. No Postgres SKIP LOCKED test.

### 3.3 LHDN submit + poll

`LhdnSubmissionJob` and `LhdnStatusPollingJob` (`Modules/Lhdn/Infrastructure/Workers/`). Registered DI 67–68.

Both use a **lease** via `TaxDocument.ClaimProcessingLease(leaseUntil)` which writes `NextPollAt`. Claim SQL:

Submit (`LhdnSubmissionJob.cs` 149–156):

```
WHERE ValidationStatus = 'PENDING'
  AND (NextPollAt IS NULL OR NextPollAt <= NOW())
ORDER BY CreatedAt
LIMIT 50
FOR UPDATE SKIP LOCKED
```

Poll (`LhdnStatusPollingJob.cs` 152–159):

```
WHERE ValidationStatus = 'SUBMITTED'
  AND SubmissionUid IS NOT NULL
  AND (NextPollAt IS NULL OR NextPollAt <= NOW())
ORDER BY NextPollAt NULLS FIRST
LIMIT 50
FOR UPDATE SKIP LOCKED
```

Lease is committed **before** gateway I/O (`LhdnSubmissionJob.cs` 174–175, `LhdnStatusPollingJob.cs` 177–178). A crash during MyInvois HTTP leaves the row hidden until `ClaimLeaseDuration` (2 minutes). That is the pattern billing does not have.

InMemory branch skips SKIP LOCKED and just `Take(50)` (tests).

`TaxDocumentClaimLeaseTests` (2 tests) only assert the domain method sets `NextPollAt`. `MyInvoisLoopTests` covers more of submit/poll state. There is no Testcontainers test that two workers cannot claim the same PENDING document.

`LhdnReferenceDataSeederJob` is a one-shot seeder, not a poller.

### 3.4 `B2cConsolidationJob` (Billing)

Registered at `Billing/Infrastructure/DependencyInjection.cs` 81. `RevenueRecognitionJob` is parked in the same file (76–80) and must not be re-enabled without schedule writers.

The job is **calendar-driven**, not interval-driven (`B2cConsolidationJob.cs` 36–65):

1. `CatchUpClosedPeriodsAsync` immediately on start (downtime past the 28th must not skip months).
2. Sleep until next 28th 02:00 Asia/Kuala_Lumpur (fallback `Singapore Standard Time`).
3. Catch-up again.
4. On unexpected exception, wait 5 minutes and retry.

Catch-up (`94–137`): look back 24 closed MYT months; find pending B2C ledger rows (`CustomerType == "B2C"` and consolidation pending / legacy null+receipt); group by calendar month; process each org independently; on org failure, detach tracked entities so the next org is not poisoned (181–196).

Idempotency intent (`209–219`): skip if a ledger row already has `TaxInvoiceId == $"B2C-CONS-{yyyyMM}-{orgId:N}"`.

**Filter hole on that check:** `alreadyConsolidated` uses `db.LedgerEntries.AnyAsync` **without** `IgnoreQueryFilters()` (209). `LedgerEntry` is `IMustHaveTenant`. The worker’s ambient tenant is empty. Fail-closed filter ⇒ `AnyAsync` is always false in a real hosted process. The skip is therefore a no-op at runtime. Re-entry protection actually comes from the pending-status filter on the earlier `IgnoreQueryFilters()` query (151–161): once rows are `MarkConsolidatedPending`, they drop out. A crash **after** `PublishAsync` (outbox insert) and **before** `MarkConsolidatedPending` + `SaveChanges` can double-publish a consolidation event. There is no Testcontainers test of that race.

`B2cConsolidationJobTests`: **7** InMemory tests (eligible month, already done, old month, legacy null, B2B/not-required/current skipped, threshold split).

### 3.5 `InvoiceReminderJob` (Commerce)

Registered DI 58. Hourly, hard-coded (`InvoiceReminderJob.cs` 52), not in `BackgroundWorkerOptions`.

Selects **all** OPEN custom (quote) checkouts with `DueAt != null` via `IgnoreQueryFilters` (65–70). No `FOR UPDATE`, no lease, no tenant batching. Offsets `[-3, 0, 3]` days from due date. Dedup table `InvoiceReminderDispatchLogs` unique on `(SessionId, DayOffset)` (migration `20260820140000_AddCommerceDisputes.cs` 62–67 — the log table was shipped in the disputes migration).

`InvoiceReminderDispatchLog` is **not** `IMustHaveTenant` (`InvoiceReminderDispatchLog.cs` 6: `class InvoiceReminderDispatchLog : Entity`). The load of existing logs (78–80) therefore does not need `IgnoreQueryFilters`. That is correct, and easy to break if someone later adds a tenant column + interface without updating the job.

Publishes `FulfillmentRequestedIntegrationEvent` to Communications (`invoice.reminder`) via keyed `CommerceEventBus` (127–131). Does **not** mark the session PAST_DUE (class comment 18–20).

Two API replicas on the same hour will both select the same sessions. The unique index is the only interlock; one `SaveChanges` will throw. There is no SKIP LOCKED claim. Tests (`InvoiceReminderJobTests`, 3 tests): day-0 sends once even if `RunOnce` is called twice in-process; completed skipped; product sessions ignored. No concurrency test. No −3 / +3 offset tests.

### 3.6 Other workers that are in the boot path (not the eval headline, but they run)

- **`CheckoutSessionExpiryJob`**: every 5 minutes; expire OPEN sessions past `ExpiresAt`; release coupon reservations (`CheckoutSessionExpiryJob.cs` 15–93). Tested only as one method inside `CommerceProductCompletenessTests` (not its own fixture). No SKIP LOCKED; loads all expired OPEN sessions.
- **`BroadcastFanoutJob`**: 10s; claims QUEUED broadcasts with SKIP LOCKED (`BroadcastFanoutJob.cs` 78–80). Has `BroadcastClaimTests` (1 test).
- **`OutboundWebhookDispatcherJob`**: 10s; leases deliveries (`ClaimLeaseDuration`). Has `OutboundWebhookClaimTests` + `OutboundWebhookTests`.
- **`SystemGenesisBootstrapperJob`**: `IHostedService.StartAsync` once; system tenant + platform admins. No dedicated test that it is registered.
- **`PlatformMetricsRefreshJob`**: 30s gauges (outbox lag, dead letters, LHDN stuck). Delayed 5s after boot so migrations settle (`PlatformMetricsRefreshJob.cs` 31–40).
- **`RevenueRecognitionJob`**: exists, **unregistered**. XML-doc forbids uncommenting (`RevenueRecognitionJob.cs` 18–29; DI 76–80).
- Optional one-shot migrators: API keys, LHDN webhook registry (`Program.cs` 85–169).

Outbox/inbox jobs: 9 publishers + 9 consumers, all process-local, 5s idle poll.

---

## 4. Tenant query filters and IDOR tests

### 4.1 Fail-closed filter

```41:46:apps/lazuar-api/BuildingBlocks/Infrastructure/PlatformDbContext.cs
    private void ConfigureGlobalFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IMustHaveTenant
    {
        // Fail-closed: empty ambient TenantId matches no rows (workers must IgnoreQueryFilters + explicit org).
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            e.OrganizationId == ExecutionContext.TenantId);
    }
```

Applied automatically to every entity whose CLR type implements `IMustHaveTenant` (`OnModelCreating` 29–37).

Write guards in the same `SaveChangesAsync` (50–76):

- On `Added`, stamp `OrganizationId` from ambient tenant if the entity still has `Guid.Empty` **and** ambient is non-empty.
- After stamp, **throw** if any Added/Modified `IMustHaveTenant` still has empty `OrganizationId`.

Workers with empty ambient therefore **cannot** insert tenant rows unless they set `OrganizationId` explicitly. They also cannot *read* tenant rows without `IgnoreQueryFilters`.

Ops overrides the filter for soft-delete **and** tenant:

```37:39:apps/lazuar-api/Modules/Ops/Infrastructure/OpsDbContext.cs
            builder.HasQueryFilter(x =>
                !x.IsDeleted &&
                x.OrganizationId == ExecutionContext.TenantId);
```

EF replaces the base filter when you call `HasQueryFilter` again; the override must repeat the org predicate. The architecture test locks that (`TenantIsolationArchitectureTests.cs` 65–81).

### 4.2 Who implements `IMustHaveTenant` (complete list from the repo)

**One:** `ApiCredential`, `AuditEvent`, `TenantMembership`, `WorkspaceInvitation`, `TenantAppEntitlement`, `WebhookDeliveryOutbox`, `TenantWebhookEndpoint`.  
**Not** tenant-filtered: `Organization` itself (`Organization.cs` 9: `Entity, IAggregateRoot` only), `GlobalUser`.

**Commerce:** `Product`, `Subscription`, `Coupon`, `Order`, `CheckoutSession`, `DunningCampaign`, `CommerceDispute`, `CommerceTransactionLog`.  
**Not** tenant-filtered: `ChargeAttemptLog`, `InvoiceReminderDispatchLog`, `ReminderDispatchLog`, `ProductPrice`, `DunningStep`.

**Billing:** `LedgerEntry`, `TenantCreditBalance`, `CreditHold`, `CreditDeductionIdempotencyLog`, `DocumentSequence`, `TenantBillingProfile`, `WorkspaceSaasSubscription`, `DeferredRevenueSchedule`.  
**Not:** `LedgerLine` (child of entry).

**Lhdn:** `TaxDocument`, `LhdnTenantConfig`, `DeveloperApiKey`, `WebhookSubscription`, `TinValidateCache`, `IdempotencyLog`.

**Communications:** `MessageTemplate`, `TenantEmailConfiguration`, `SuppressionEntry`, `Broadcast`.

**CRM:** `ClientProfileEntity`.

**Payments:** `TenantPaymentConfiguration`, `IntegrationCheckoutSession`.

**Ops:** `OpsConversation`, `OpsMessage`.

**Messaging:** tenant replica types (not `IMustHaveTenant` on the delivery log in the same way; Messaging is a replica schema).

A child table without the interface is **not** filtered. That is acceptable when it is only reached through a filtered parent include, and dangerous when workers/query services query it directly (the invoice-reminder log is the example that happens to be global-by-design).

### 4.3 HTTP tenant binding

Pipeline order is load-bearing (`MiddlewarePipelineExtensions.cs` 7–27):

1. Exception handler  
2. Correlation id  
3. CORS  
4. JWT authentication (cookie `lazuar_auth` or `lazuar_admin_auth` on `/platform`)  
5. API key middleware  
6. `TenantSecurityMiddleware`  
7. Authorization  

`TenantSecurityMiddleware` (`TenantSecurityMiddleware.cs`):

- API key principals skip the header requirement (the key middleware already bound `TenantId`) (22–27).
- `/api/v1/platform` forces tenant `00000000-0000-0000-0000-000000000001` (29–34).
- Else resolve `X-Tenant-Id` header, or `X-Tenant-Slug`, or `{tenantSlug}` route (38–49).
- Exempt paths (`IsTenantExemptPath` 115–144): `/health`, `/api/v1/public`, `/api/v1/webhooks`, `/api/v1/one/public`, `/one/auth`, `/one/me`, `/one/workspaces`, `/one/integrations/workspaces`.
- Required paths (`RequiresTenantContext` 149–167): `/api/v1/admin`, `/lhdn`, `/ops`, `/messaging`, `/one/storage`, `/one/api-keys`.
- If required and missing/empty → 400 problem+json “Missing Tenant Context” (55–70).
- If tenant resolved and user authenticated: look up membership role and **inject** `ClaimTypes.Role` (77–89). No membership on a non-exempt path → 403 (90–104).

Wave 3 staff roles (`AuthAndCorsExtensions.cs` 76–94):

- `OrgAdmin` = SUPER_ADMIN | ADMIN (keys, gateways, members, legal).
- `OrgMember` = those + MEMBER (commerce mutations).
- `OrgRead` = those + VIEWER (GETs).

Commerce admin group is `OrgRead` (`Endpoints.cs` 23). Individual mutation endpoints then `RequireAuthorization("OrgMember")` or `OrgAdmin`. `GET /admin/commerce/disputes` sits on the group with **no extra policy** (67–77), so VIEWER can list disputes. List SQL is `WHERE OrganizationId = @OrgId` (`CommerceQueryService.CustomCheckouts.cs` 131–145).

Invite **accept** is under `/one/workspaces/invites/accept`, which is tenant-**exempt** (prefix `/api/v1/one/workspaces`). The handler hashes the token and loads the invitation with `IgnoreQueryFilters` (`OneRepository.cs` 86–91). That is the correct shape for a token that must work before the user has a membership (and therefore before they can send `X-Tenant-Id` for that org).

### 4.4 IDOR / isolation tests that exist

`CrossTenantIdorTests` (8 tests):

| Test | Handler | Assertion |
|------|---------|-----------|
| `CancelAdminSubscription_ForeignOrg_ThrowsNotFound` | cancel now | status stays ACTIVE, no SaveChanges |
| `CancelAdminSubscription_ForeignOrg_AtPeriodEnd_ThrowsNotFound` | cancel at period end | `CancelAtPeriodEnd` stays false |
| `KeepAdminSubscription_ForeignOrg_ThrowsNotFound` | keep | flag stays true |
| `AnonymizeSubscriber_ForeignOrg_ThrowsNotFound` | anonymize | no CRM command |
| `RecordRefund_ForeignOrg_ThrowsNotFound` | refund | no SaveChanges |
| `UpdateCoupon_ForeignOrg_ThrowsNotFound` | update coupon | code unchanged |
| `DeleteCoupon_ForeignOrg_ThrowsNotFound` | delete coupon | still active |
| `BillingLedger_AmbientTenantFilter_HidesOtherOrgRows` | EF filter on `LedgerEntry` | 1 of 2 visible |

These are **handler-level** tests with a stub repository that returns the foreign aggregate. They prove the `OrganizationId != request.OrganizationId` guard, not the HTTP stack.

`TenantIsolationHardeningTests` (9 tests): empty-tenant EF filter returns 0; ambient tenant sees only own products; SaveChanges rejects empty OrganizationId; middleware 400 on LHDN without tenant; API key skips header; `GatewayPaymentCompleted` cross-tenant session is a no-op; HMAC reject missing/invalid/expired; a **placeholder** “presigned storage rejects empty tenant” test that only asserts `Guid.Empty == Guid.Empty` (`TenantIsolationHardeningTests.cs` 279–288). That last test is not a test.

Architecture middleware allowlist tests: 5, listed in §1.4.

### 4.5 Isolation tests that do **not** exist (and the code they would hit)

Wave 3/4 added several org-scoped writes that are not in `CrossTenantIdorTests`:

- `PauseCollectionCommandHandler` / `ResumeCollectionCommandHandler` (`ChangePlanCommandHandler.cs` 89–138) — **has** the org guard, **no** IDOR test.
- `ChangePlanCommandHandler` / portal plan change — module tests exist for happy path (`ChangePlanCommandHandlerTests`, `ChangePortalPlanCommandHandlerTests`); no foreign-org case in `CrossTenantIdorTests`.
- `AcceptWorkspaceInvitationCommandHandler` — token-based; IDOR is “wrong email” / “expired”, not org header. **Untested** (see §8).
- `GET /admin/commerce/disputes` — Dapper filters `OrganizationId`; **no** test that a second tenant’s dispute id cannot be fetched (there is no get-by-id; only list).
- Commerce dispute **write** path is an integration-event handler using `@event.OrganizationId` + unique `(OrganizationId, GatewayTransactionId)`. No cross-tenant event test.
- Invite **create** is tested for role allow-list and MEMBER cannot invite; not for “invite into someone else’s workspace id” beyond whatever the endpoint passes as `id` vs `ctx`.
- LHDN / Payments / Communications admin commands are not in the IDOR fixture at all.
- No WebApplicationFactory test that sends `X-Tenant-Id` of org B with a membership in org A (middleware 403). The middleware tests use a stub `IOneQueryService` and do not exercise the membership lookup miss on `/admin/*`.

`IgnoreQueryFilters` is used widely in workers and in CRM resolve/anonymize (intentional: those run with empty ambient tenant or need to see the target org). Every such call is a potential leak if the subsequent LINQ does not constrain `OrganizationId`. The IDOR suite does not scan for that.

---

## 5. EF migrations apply on boot; Wave 3/4 migrations present

### 5.1 Boot apply

`DatabaseMigrationExtensions.MigrateAllModuleDatabasesAsync` (`src/Lazuar.Api/Composition/DatabaseMigrationExtensions.cs` 25–66) runs **after** `builder.Build()` and **before** hosted services are a problem for empty schema (Program.cs 229–230). Order:

1. `OneDbContext`
2. `MessagingDbContext`
3. `PaymentsDbContext`
4. `CrmDbContext`
5. `OpsDbContext`
6. `BillingDbContext`
7. `LhdnDbContext`
8. `CommerceDbContext`
9. `CommunicationsDbContext`

Each `MigrateAsync()` is logged. `PendingModelChanges` is caught, **logged, and boot continues** (53–59) — the process can come up with a module schema missing new tables. Any other exception fails boot (60–63).

The method’s own XML doc (14–21) states the multi-instance race: concurrent hosts each run `MigrateAsync` against the same Neon database; EF history usually serializes, but lock contention is real. There is no init-container / migrate job. This is the production apply path.

Billing’s DbContext additionally ignores `PendingModelChangesWarning` so drift does not block `MigrateAsync` on empty local DBs (`Billing/Infrastructure/DependencyInjection.cs` 43–46). Integration credit tests copy that warning ignore (`CreditDeductionConcurrencyTests.cs` 73–74).

### 5.2 Migration inventory (all modules)

Counts are **Up** migrations (excluding Designer/Snapshot):

| Schema | Migrations on disk | Latest |
|--------|--------------------|--------|
| `one` | Initial, DropLegacySchemas, OutboxInboxRetry, CreateApiCredentials, WebhookEndpointEnabledEvents, OrganizationExternalRef, **AddOrganizationCheckoutBranding (20260818100000)**, **AddAuditEvents (20260820150000)** | Wave 3 audit |
| `messaging` | Initial, OutboxInboxRetry, AddMessageDeliveryLogs | 20260804 |
| `payments` | Initial, RemoveAccountingOverrides, OutboxInboxRetry, WebhookBusinessKey, PaymentConfigIsActive, IntegrationCheckoutSessions, WebhookOutboxMessageId, **AddPaymentConfigEnvironment (20260818120000)** | Wave 1 sandbox/live |
| `crm` | Initial, OutboxInboxRetry, ConsentDefaultFalse, **AddClientProfileCompanyName (20260818120000)** | Wave 2 B2B name |
| `ops` | Initial, OutboxInboxRetry | 20260803 |
| `billing` | Initial, ProfilesAndSequences, CreditHolds, OutboxInboxRetry, Receipt/Consolidation fields, **AddWorkspaceSaasSubscriptions (20260816120000)** | Wave 1 Hub SaaS |
| `lhdn` | Initial, OutboxInboxRetry, DeveloperApiKeyScopes, TenantLegalAddress | 20260803 — **no Wave 3/4 migration** |
| `commerce` | 24 Up files from Initial (20260627) through **AddCommerceDisputes (20260820140000)** | Wave 3 |
| `communications` | Initial, Suppressions, Broadcasts, EmailConfig, RemoveBroadcasts, OutboxInboxRetry | 20260803 — **no Wave 3/4 migration** |

Commerce Wave 3/4-relevant files (present, applied on boot via CommerceDbContext):

| Migration | What it adds |
|-----------|----------------|
| `20260817120000_AddSubscriptionCancelAtPeriodEnd` | Wave 1 leftover still in the chain |
| `20260817180000_AddTransactionRefundFields` | refund columns |
| `20260817190000_AddTransactionLogSubscriptionId` | link tx → sub |
| `20260818110000_AddCheckoutSessionIdempotency` | idempotency key + fingerprint |
| `20260818140000_AddProductSst` | SST type/rate |
| `20260819120000_AddCheckoutSessionDocumentNumber` | quote numbers |
| **`20260820120000_AddWave3SubscriptionBilling`** | quantity, pending qty/product, PriceId, UnitAmount, BillingInterval, TrialEndsAt, **CollectionPausedUntil**, HasOpenDispute, Product.TrialDays, CheckoutSession.PriceId + DueAt, **ProductPrices** table + backfill |
| **`20260820130000_AddChargeAttemptDeclineClass`** | `ChargeAttemptLogs.DeclineClass` varchar(16) |
| **`20260820140000_AddCommerceDisputes`** | `commerce.Disputes` + `InvoiceReminderDispatchLogs` |

`AddWave3SubscriptionBilling.Up` (file lines 14–151) is the subscription billing reshape: default Quantity=1, backfill `UnitAmount` from `Products.Price`, seed `ProductPrices` from each product’s interval/price. `HasOpenDispute` is created (`false` default) and mapped on the snapshot (`CommerceDbContext.cs` 181) but **never written** by any handler (grep: only the property, Activate-reset, EF config, and this migration). Wave 3 shipped a column without a writer.

`AddCommerceDisputes.Up` (14–67) creates `Disputes` with unique `(OrganizationId, GatewayTransactionId)` and `InvoiceReminderDispatchLogs` with unique `(SessionId, DayOffset)`.

One Wave 3: `20260820150000_AddAuditEvents` creates `one.AuditEvents` (jsonb metadata, index `(OrganizationId, CreatedAt)`). Invite-side audit is tested (`InviteUserToWorkspaceCommandHandlerTests.Invite_RecordsAuditWithoutSecrets`). Accept-invite does **not** write an audit row (`AcceptWorkspaceInvitationCommandHandler` has no `IAuditRecorder`).

Payments `20260818120000_AddPaymentConfigEnvironment` adds `Environment` default `'test'` then **SQL-updates existing rows to `'live'`** (23–24) so incumbents are not silently pointed at sandbox. That is a Wave 1 honesty migration still in the boot chain; Xendit (Wave 4) did not add a payments migration.

No Wave 3/4 migration exists for Lhdn, Communications, Ops, or Messaging. Xendit did not need a new table. Invite accept did not need a new table (`WorkspaceInvitations` is from `InitialOneSchema`). Pause did not need a new table beyond `CollectionPausedUntil` in `AddWave3SubscriptionBilling`.

Designer files: several 20260818–20260820 migrations have **no** `.Designer.cs` (e.g. `AddWave3SubscriptionBilling`, `AddCommerceDisputes`, `AddAuditEvents`, `AddPaymentConfigEnvironment`). Snapshots (`*DbContextModelSnapshot.cs`) do include the new columns. Boot `MigrateAsync` uses the `Up` methods + history table, not the designers. The missing designers make `dotnet ef migrations add` noisier; they do not block boot.

### 5.3 Integration tests that actually run `MigrateAsync`

`CreditDeductionConcurrencyTests` starts Testcontainers Postgres and `await migrateCtx.Database.MigrateAsync()` (`CreditDeductionConcurrencyTests.cs` 48–49). If Docker is down, the fixture sets `_postgresReady = false` and tests skip (33–56). This is the only suite that proves the **billing** migration chain (including `AddWorkspaceSaasSubscriptions`) applies to a real server.

`CommerceQueryServiceTests` also starts Testcontainers and migrates Commerce (`CommerceQueryServiceTests.cs` 26–37+). That is the proof that Wave 3 commerce migrations apply, but only if Docker is available in CI.

There is **no** integration test that boots `MigrateAllModuleDatabasesAsync` across all nine contexts. A missing Lhdn/Communications designer or a pending-model warning would not fail these two fixtures.

---

## 6. Test inventory — counts and holes

Counted on 16 August 2026 with `rg '^\s*\[Test\]'` over `apps/lazuar-api/tests` excluding bin/obj.

### 6.1 Totals

| Project | Test files | `[Test]` methods |
|---------|------------|------------------|
| `Lazuar.ArchitectureTests` | 2 | **14** |
| `Lazuar.IntegrationTests` | 4 | **10** |
| `Lazuar.ModuleTests` | 157 | **944** |
| `Modules.Billing.Tests` | 2 | **20** |
| `Modules.Ops.Tests` | 2 | **5** |
| **Sum** | **167** | **993** |

`Lazuar.TestSupport` is not a test project (`README.md` 54–56).

### 6.2 `Lazuar.ModuleTests` by folder

| Folder | Files | Methods | What is actually covered |
|--------|-------|---------|--------------------------|
| Commerce | 44 | **330** | Checkout, dunning (40 worker tests), billing engine (19), invoices reminder (3), coupons, refunds, SST, trials, pause **domain**, plan change, disputes **handler**, endpoints auth metadata |
| One | 21 | **168** | API keys, webhooks, invite **create**, audit recorder, register, provision, branding — **not invite accept** |
| Payments | 20 | **148** | Stripe/Billplz/CHIP/Razorpay/Xendit **adapters**, webhook process, off-session, capabilities, env, secrets |
| Billing | 21 | **95** | Ledger matrix, credits, SaaS fee, clawback **utility/SaaS**, B2C job (7), documents |
| Communications | 16 | **75** | Templates, suppressions, broadcasts, lifecycle email, Resend parse |
| Lhdn | 13 | **48** | MyInvois loop, claim lease **domain**, cancel, secrets, rate limit, sandbox e2e (conditional) |
| Messaging | 6 | **20** | Console WA, Resend, delivery log, dispatch handler, endpoint auth |
| Observability | 6 | **18** | Metrics, health, correlation, plugin registration |
| TenantIsolation | 2 | **17** | Filters + 7 handler IDORs + middleware |
| BuildingBlocks | 4 | **12** | Vault, retry, applier, outbox helper |
| CRM | 3 | **11** | Anonymize event, company name, outbox **DI registration** |
| EventHandlers (host) | 1 | **2** | API key revoked cache |

Commerce is a third of all module tests. Ops product code is almost untested in ModuleTests (LLM lives in `Modules.Ops.Tests`, 5 tests). CRM Application-less handlers have 11 tests total.

### 6.3 Integration tests (10 methods, 4 files)

| File | Methods | Backend |
|------|---------|---------|
| `BillingDbContextTests` | 1 | EF InMemory (mis-shelved; not a container) |
| `BillingQueryServiceTests` | 2 | InMemory + Dapper? (file exists; not container-first) |
| `CommerceQueryServiceTests` | 4 | **Testcontainers PostgreSQL** |
| `CreditDeductionConcurrencyTests` | 3 | **Testcontainers PostgreSQL**, skips without Docker |

`Lazuar.IntegrationTests.csproj` references only Billing Infrastructure, Commerce Infrastructure, and BuildingBlocks. It cannot boot the host or migrate One/Lhdn/Payments. There is no `WebApplicationFactory` suite in this project.

What integration tests **do not** cover: outbox publish → in-memory bus → handler on real Postgres; SKIP LOCKED claim under two workers; boot `MigrateAllModuleDatabasesAsync`; tenant middleware + JWT; Xendit HTTP; invite accept; dispute → ledger.

### 6.4 Architecture tests (14) — holes

See §1.3. Additionally: no test that every module **registers** the outbox job (only that the type exists; CRM also tests registration). No test that inbox tables are unused. No test that `AddModuleOutboxInbox` is used consistently (it is not).

### 6.5 Worker tests vs workers

| Worker | Tests | Relational claim tested? |
|--------|-------|--------------------------|
| BillingEngineJob | 19 | no (InMemory claim path) |
| DunningEngineJob | 40 | no |
| InvoiceReminderJob | 3 | n/a (no claim) |
| B2cConsolidationJob | 7 | no |
| Lhdn submit/poll lease | 2 domain + MyInvois loop | no two-worker SKIP LOCKED |
| CheckoutSessionExpiryJob | 1 method in another fixture | no |
| BroadcastFanoutJob | 1 claim test | InMemory |
| OutboundWebhookDispatcherJob | claim + dispatch tests | InMemory |
| OutboxPublisherJob / InboxConsumerJob | retry/applier/DI only | **no job loop test** |
| RevenueRecognitionJob | none (parked) | — |

### 6.6 Frontend / contract tests

- No Playwright / Cypress in this workspace for ops/portal against the Wave 3/4 UI.
- TypeSpec honesty is a separate eval (`08`). Not counted here.
- Portal `i18n.test.mjs` does not touch tenancy or workers.

### 6.7 Structural holes (pre-Wave-3, still true)

- Almost all 944 module tests are EF InMemory or pure domain. `FOR UPDATE SKIP LOCKED`, filtered indexes, and `xmin` concurrency are invisible except credit deduct (3 tests, Docker-gated).
- `Modules.Billing.Tests` (20) duplicates credit/hold domain that ModuleTests also cover. Two homes.
- `Lazuar.SandboxE2E` style LHDN tests are conditional on sandbox credentials (`LhdnSandboxE2ETests`).
- Endpoint “authorization tests” assert metadata (`RequireAuthorization` policy names), not a real HTTP 403.
- `Presigned_Storage_Rejects_Empty_Tenant_Contract` is a tautology.

---

## 7. What is untested after rapid Wave 3/4

The four holes named in the eval brief, then the adjacent ones the same waves created.

### 7.1 Invite accept

**What shipped**

- Domain: `WorkspaceInvitation.Accept()` (`WorkspaceInvitation.cs` 38–45) throws if not PENDING or expired; sets `ACCEPTED`.
- Handler: `AcceptWorkspaceInvitationCommandHandler` (`AcceptWorkspaceInvitationCommand.cs` 11–42):
  1. Load user; require active.
  2. Hash token; `GetInvitationByHashAsync` (**IgnoreQueryFilters**).
  3. Reject if missing / not PENDING / expired.
  4. Reject if `user.Email != invitation.Email`.
  5. `invitation.Accept()`; `AddTenantMembership(user, org, invitation.Role)`; SaveChanges.
- HTTP: `POST /api/v1/one/workspaces/invites/accept` (`WorkspaceEndpoints.cs` 121–123) with `AcceptWorkspaceInvitationDto.Token` and `ctx.UserId`. Path is tenant-exempt.
- Email: `WorkspaceInvitationCreatedDomainEvent` carries `PlainToken`. `NotificationDispatchDomainEventHandlers.Handle` (65–80) sends `DispatchMessageIntegrationEvent` to the **organization** (not system tenant) with link `{App:ClientUrl}/accept-invite?token=...`.
- `App:ClientUrl` default is `http://localhost:3004` (`appsettings.json` 41; `OneLinkService.cs` 15–18). 3004 is **lazuar-portal**.

**What is tested**

- Invite **create** only: `InviteUserToWorkspaceCommandHandlerTests` (5 tests) — MEMBER stored uppercase, banana role rejected, MEMBER cannot invite, SUPER_ADMIN membership can invite, audit omits the plaintext token.

**What is not tested (and is broken or incomplete in product)**

1. **No `AcceptWorkspaceInvitationCommandHandler` test file.** Zero tests for happy path, expired, already accepted, wrong email, inactive user, bad token, duplicate membership.
2. **Handler does not check existing membership.** A second accept (if status check were bypassed) or a user who was already a member would `AddTenantMembership` again. Untested.
3. **Accept does not write `AuditEvent`.** Invite does. Asymmetry untested.
4. **No accept UI anywhere.** Grep of `accept-invite` / `AcceptInvite` across `*.tsx`/`*.ts` returns **only** the C# email template. Portal app routes (`apps/lazuar-portal/src/app/`) have checkout, pay, portal magic-link, update-payment, legal — **no** `accept-invite`. Ops `TeamPage.tsx` can **send** invites (POST `/one/workspaces/{id}/invites`) and list **members**, not pending invitations, and has no accept form. The email link 404s on the portal.
5. **Invite email uses `notification.OrganizationId`**, so it goes through the tenant’s Resend config (`DispatchMessageIntegrationEventHandler` tenant credentials). A workspace that has not saved email BYOK will fail the invite email (or send with platform key only if that path allows — it does not; non-system tenants need tenant credentials for branded send, and missing config still calls `SendEmailAsync` with nulls). Untested.
6. **Team page does not invalidate or list invitations** (`TeamPage.tsx` 17–27 loads members only). W3-LP-166-done claims “Ops Team page.” The page is invite+remove members, not the accept loop.

Net: Wave 3 shipped invite **create** + roles + an email URL that no app implements, and left accept as an untested API.

### 7.2 Xendit UI

**What shipped (Wave 4 wrap)**

- Adapter: `XenditGatewayAdapter.cs` — hosted invoice POST `/v2/invoices`, `x-callback-token` verify, refunds POST `/refunds`, `ChargeOffSessionAsync` **always false** (155–171).
- Capabilities: reminder-only, DuitNow QR / hosted wallets advertised as Xendit’s hosted page, not ours (`PaymentGatewayCapabilitiesTests` 53–58).
- Factory + webhook allow-list + M2M checkout allow-list include `XENDIT` (`Payments/Infrastructure/DependencyInjection.cs` 38; `CreateIntegrationCheckoutCommandHandler.cs` 19).
- Ops/admin **dropdown option** exists (`PaymentSettingsPage.tsx` 211, admin `PlatformPaymentSettingsPage.tsx` 206, two modals).

**What is tested**

- `XenditGatewayAdapterTests` (6): missing callback token, PAID → COMPLETED, EXPIRED → FAILED, paid without currency does not invent MYR, `xendit_payment_methods` filter drops `FAKEWALLET`, off-session false.
- Capabilities cases.

**What is not tested / not built**

1. **There is no `gatewayType === "XENDIT"` credential form.** Grep for that string in all ts/tsx is empty. `PaymentSettingsPage.tsx` has CHIP, BILLPLZ, STRIPE, RAZORPAY blocks (246–391) and then the form **ends**. Selecting Xendit shows Target Provider + environment + Save, **no API key field, no callback-token field**.
2. Client-side validation in `handleSubmit` (84–128) covers Billplz / CHIP / Stripe / Razorpay only. Xendit save can POST empty `api_key` / `webhook_secret`.
3. Environment helper text still says “Billplz sandbox, Stripe sk_test_” (232). No Xendit test-vs-live hostname note (the adapter always uses `https://api.xendit.co`, `XenditGatewayAdapter.cs` 22).
4. No UI for `xendit_payment_methods` (GRABPAY / QR_CODE / DD_FPX / …). The adapter only honors that metadata key if a checkout puts it in metadata (`ResolveRequestedPaymentMethods` 226–237). Ops product/checkout screens do not expose it.
5. No frontend test. No module test that `PUT /admin/commerce/payment-config` with `gateway_type=XENDIT` persists vault fields.
6. W4-LP-045-done says “ops/admin dropdown include XENDIT” and marks the tracker **W** (wrap). That is accurate. It is **not** a complete merchant setup path.

A merchant who picks Xendit in the vault UI cannot enter an `xnd_` secret or callback token without using the API directly. Wave 4 shipped the rail and left the form.

### 7.3 Dispute ledger

**What shipped (W3-LP-094)**

Two handlers on the same `GatewayDisputeCreatedIntegrationEvent`:

1. **Commerce** `CommerceGatewayDisputeCreatedHandler` (`EventHandlers/CommerceGatewayDisputeCreatedHandler.cs`):
   - Returns immediately if metadata `type` is platform-collected (`IsPlatformCollected`) (35–40).
   - Idempotent on `(OrganizationId, GatewayTransactionId)` (42–51).
   - Resolves subscription / checkout from metadata; does **not** cancel the subscription (class comment 15; tests assert ACTIVE).
   - Inserts `CommerceDispute` status OPEN.
   - `transactionLog.MarkDisputed()` if a log matches the gateway tx.
   - If `AmountDisputed > 0`, publishes `GatewayRefundCompletedIntegrationEvent` with **`Id = dispute.Id`** (101–115) — this is the “ledger contra” W3-LP-094-done describes.
   - Does **not** set `Subscription.HasOpenDispute` (column exists, unused).

2. **Billing** `ChargebackClawbackHandler` (`ChargebackClawbackHandler.cs`):
   - `platform_saas_fee` → mark Hub SaaS PAST_DUE, no credit clawback (50–54, 85–108).
   - `utility_credit_topup` → claw credits + reverse `SYSTEM_CREDIT_TOPUP` as `SYSTEM_CREDIT_CHARGEBACK` (56–82, 110–166).
   - Anything else (including `commerce_subscription`) → **return** (comment 56–58: “commerce chargebacks are intentionally out of scope for MVP”).

GMV ledger contra is therefore **not** in the clawback handler. It is supposed to be the existing `GatewayRefundCompletedHandler`, which posts `GATEWAY_REFUND` lines (`GatewayRefundCompletedHandler.cs` 32–80) keyed by `PaymentRecordId + event.Id`.

**What is tested**

- `CommerceGatewayDisputeCreatedHandlerTests` (5): replay one row; utility no-op; SaaS no-op; sub not canceled + log DISPUTED; no-metadata still inserts. The refund event is asserted with `Received(1).PublishAsync(GatewayRefundCompletedIntegrationEvent)` on a **substitute** bus. The Billing handler never runs.
- `ChargebackClawbackHandlerTests` (4): utility reverses ledger + sends `ClawbackCreditsCommand`; second dispute idempotent; SaaS PAST_DUE no claw; `commerce_subscription` is a no-op on Billing (no ledger rows). That last test **locks in** “commerce dispute does not write Billing ledger in the clawback handler.”
- `GatewayRefundCompletedHandlerTests`: refund math when **you construct a refund event**. Not wired from a dispute.

**What is not tested**

1. **No test that a commerce dispute results in a balanced `GATEWAY_REFUND` ledger entry.** The two handlers are never composed. W3-LP-094-done’s sentence “Ledger contra is the existing Billing `GatewayRefundCompleted` consumer (event id = dispute id)” is an integration claim with zero tests.
2. If the in-process bus **does** deliver that event, `GatewayRefundCompletedHandler` looks up the original `GATEWAY_PAYMENT` by `GatewayTransactionId` to prorate tax (`ResolveTaxRefundAmountAsync` 94–124). Dispute events set `TaxAmount` default 0 and `RefundedFee` 0. Untested with a real prior payment row + dispute.
3. `HasOpenDispute` is never flipped; no test notices.
4. No test for `GET /admin/commerce/disputes` Dapper paging or tenant filter.
5. Ops `DisputesPage.tsx` is a fetch+table. No UI test. Empty copy says “No open disputes” even though the API returns all statuses and the entity has only `OPEN` (no won/lost).
6. No Stripe/Xendit webhook → `ProcessGatewayWebhook` → `GatewayDisputeCreated` → both handlers test. `ProcessGatewayWebhookCommandHandlerTests` has a case that a **non-dispute** event does not publish `GatewayDisputeCreated` (line 595 in that file). The positive dispute path from a signed webhook body is thin relative to the new table.
7. Unique index + InMemory: EF InMemory does not enforce the unique `(OrganizationId, GatewayTransactionId)` the same way Postgres does. Replay test uses a handler-level `FirstOrDefault` check, which is good; it does not prove the migration index.

The honest status: **dispute row + DISPUTED stamp + no cancel** are tested. **Dispute as a ledger event** is a comment in a done-note.

### 7.4 Pause claim loop

Covered as a worker defect in §3.1. Test gap specifically:

- Domain pause/resume: `SubscriptionCollectionPauseTests` (4 tests) — flag, PAST_DUE throws, past resume throws, resume pushes `NextBillingDate`.
- Engine: one test that a **single** paused due sub is not charged and stays ACTIVE with a **still-due** `NextBillingDate`.
- No test that claim SQL omits paused rows (because it does not).
- No test that `failedIds` / `processedIds` include skipped paused ids.
- No test that billing processes sibling due subscriptions in the same `RunOnce` when one is paused.
- No `PauseCollectionCommandHandler` test; no IDOR test; no HTTP test.
- Dunning pre-dunning SQL **does** omit paused rows (`DunningEngineJob.Claim.cs` 107, 158). There is no explicit test named for that predicate (it may be implicit in a “not in 14-day window” case). A regression that dropped the SQL clause would not fail a dedicated test.

W3-LP-057-done: “Tests run — `SubscriptionCollectionPauseTests`, `BillingEngineJobTests` (paused due tick).” That is exactly the suite that missed the loop.

### 7.5 Adjacent Wave 3/4 holes (same rapid merge)

- **`HasOpenDispute`** column + snapshot, zero writers, zero tests that a dispute sets it.
- **Portal plan change / quantity / trial / multiple prices** have module tests (`ChangePortalPlanCommandHandlerTests`, `SubscriptionTrialTests`, `CommerceCheckoutQuantityTests`) but no integration test that billing engine + pending plan + pause interact.
- **Decline class** migration + `DeclineClassifierTests` exist; billing claim loop does not use decline class.
- **Audit log** (`AddAuditEvents`, `AuditRecorderTests`) does not cover accept-invite, pause, or dispute.
- **Xendit webhook** tests are adapter-only; `ProcessGatewayWebhookCommandHandler` + Xendit headers is not a dedicated fixture.
- **Invoice reminder** unique index is the only multi-instance lock; untested under two workers.
- **B2C `alreadyConsolidated`** query filter bug (§3.4); tests use InMemory where global filters + empty tenant also hide rows unless the test used `IgnoreQueryFilters` on seed — they seed then query through the job’s `IgnoreQueryFilters` path for pending rows, so the broken `AnyAsync` is never asserted.

---

## 8. Performance and ops risks (billing claim loop and friends)

### 8.1 Billing claim loop (P0 ops)

Mechanism, again, with the exact control flow:

1. Hourly tick, `BatchSize = 50`, one transaction per subscription (`BillingEngineJob.cs` 32, 66–118).
2. Claim the earliest due non-terminal subscription. Paused ACTIVE rows qualify.
3. Skip pause **after** claim, commit (no row change), **do not exclude the id**.
4. Next iteration claims the same row.
5. Repeat until `i == 50`.
6. Sleep one hour.

Blast radius:

- **Throughput:** one paused-and-overdue subscription = 50 empty transactions/hour on the primary, and **zero** other renewals that hour.
- **Locking:** each iteration takes `FOR UPDATE` on that row then commits. Short locks, high rate. Harmless alone; not harmless if the same pattern is copied.
- **Auto-debit sibling:** a successfully dispatched off-session charge also leaves status ACTIVE and `NextBillingDate` in the past. Attempt log prevents a second `ExecuteOffSessionCharge`, but the row still occupies the other 49 slots if it sorts first.
- **Multi-instance:** SKIP LOCKED means two replicas will not process the same row concurrently, but each replica’s 50-iteration loop can still serialize on the same paused row after the other commits. You get 50×N empty claims per hour across N replicas, still starving others if they all see the same `ORDER BY NextBillingDate`.
- **Observability:** the skip is an Information log (`Billing skipped collection-paused subscription {Id}`). Metrics gauges (`PlatformMetricsCollector`) do not count “claim wasted on pause.” A merchant with one holiday-paused overdue sub will silently stop renewing everyone else and the only signal is log volume.

Compare LHDN: lease `NextPollAt` **before** slow I/O, so the next poll cannot see the row. Compare dunning: `processedIds` + SQL pause predicate. Billing has neither.

**Fix shape (not implemented in this eval):** add `(CollectionPausedUntil IS NULL OR CollectionPausedUntil <= NOW())` to claim SQL **and** add skipped ids to `failedIds` (or a `skippedIds` set) **and** consider a lease or “not billed since” stamp for the auto-debit waiting state. Tests that must exist: two due subs, one paused, `RunOnce` marks/charges the other; Postgres SKIP LOCKED two-worker test.

### 8.2 Other ops risks in the same spine

**Boot migrate on every replica** (`DatabaseMigrationExtensions.cs` 14–21, 48–50). Two rolling pods can run `MigrateAsync` together. Wave 3 added several Commerce migrations with data backfill (`UPDATE ... UnitAmount`, `INSERT ProductPrices`). Those are not purely lock-safe under two migrators. Prefer a single migrate job before scale-out.

**PendingModelChanges continues boot** (53–59). A forgotten Wave 3 migration on a module that set `ConfigureWarnings(Ignore PendingModelChanges)` (Billing) can start the API with missing columns; workers then throw every hour.

**Empty inbox pollers.** 9 inbox consumers × `SELECT ... LIMIT 20 FOR UPDATE SKIP LOCKED` every 5 seconds on empty tables, plus 9 outbox pollers. Cheap per query (filtered index on `ProcessedAt IS NULL`), but it is 18 chatty connections from every replica. CRM and Ops inboxes are structurally unused.

**Invoice reminder full-table pull.** Every hour, all OPEN custom sessions with `DueAt` are loaded into memory (`InvoiceReminderJob.cs` 65–70). No date window. At large quote volume this is an unbounded read. Two replicas double the read and contend on the unique dispatch index.

**B2C catch-up** loads pending timestamps then re-queries full entries with lines (`B2cConsolidationJob.cs` 106–161). 24-month lookback is capped. The broken `alreadyConsolidated` filter (§3.4) plus outbox-before-mark ordering can double-emit `ConsolidatedInvoiceIssuedIntegrationEvent` after a crash. LHDN side must be idempotent on `InternalReferenceId` (`B2C-CONS-{yyyyMM}-{org}`).

**Outbox drain is single-threaded per module, 20 rows, then yield.** A burst of checkout completions writes one outbox row each. Nine publishers share one Postgres. This has been fine at current volume. It is not partitioned by tenant.

**`TypeResolver` scans all loaded assemblies** on cache miss (`TypeResolver.cs` 21–28). First outbox of a new event type after deploy pays that cost once per process.

**In-process bus = no poison isolation across modules.** A throwing Commerce handler and a Billing handler on the same `GatewayPaymentCompleted` run sequentially in `InMemoryEventBus.PublishAsync` (38–53). One throw does **not** automatically fail the outbox row — the publisher already treats the **event** as published if `PublishAsync` returns. Wait: `OutboxPublisherJob` awaits `eventBus.PublishAsync` inside try/catch per message (74–86). If **any** handler throws, `PublishAsync` throws, the outbox row is retried, and **every** handler runs again. Billing + Commerce + Communications all subscribe to payment completed. A transient Communications throw re-delivers to Billing. Billing handlers must stay idempotent (`HasEntryBeenProcessedAsync` on ledger). This is older than Wave 3 but Wave 3 added more subscribers (dispute, SaaS, clawback) to the same pattern.

**WhatsApp / invite email on the organization tenant.** Invite accept email is billed/sent as that workspace (`NotificationDispatchDomainEventHandlers.cs` 78–79). A brand-new workspace with no Resend config fails the domain-event dispatch during `SaveChanges` (domain events are published **inside** `PlatformDbContext.SaveChangesAsync` before persist, lines 78–98). If the handler throws, the invite row may not commit. That path is untested; it is an ops surprise the first time someone uses Team → Invite without email settings.

**Hard-coded intervals.** Invoice reminder and checkout expiry cannot be tuned via `Workers` without a code change. Billing/dunning 1 hour is tunable. A “run billing now” admin endpoint does not exist; tests call `RunOnceAsync` internally.

**Connection pools.** `Default` `Maximum Pool Size=50`, Messaging `20` (`appsettings.json` 19–21). One process: 9 DbContexts + Dapper factories + workers. At one replica this is fine. At many replicas × noisy 5s polls, watch `remaining connection slots` on Neon.

---

## 9. How the pieces fit (honest picture after Waves 0–4)

Lazuar Pay is still a **modular monolith with compile-time walls and a runtime in-process bus**. NetArchTest is real and is why CRM is the odd module, why the host cannot reference Application, and why BuildingBlocks cannot mention Commerce. It does not make inbox real, it does not make workers tenant-safe, and it does not make Wave 3/4 features tested.

Outbox exists in every schema. Inbox exists in every schema. **Inbox is a Messaging implementation detail** plus eight idle pollers. Cross-module money events (payment completed, refund, dispute, off-session charge, fulfillment, consolidation) are outbox → `InMemoryEventBus` → direct handlers.

Tenancy is fail-closed at EF and mostly fail-closed at middleware for `/admin/*`. Workers correctly `IgnoreQueryFilters` when they remember. The billing engine remembers to ignore filters and forgets to exclude paused rows from the claim set. IDOR tests cover a 2026-08 maintenance slice of Commerce admin commands, not the Wave 3 pause/plan/dispute/invite surface.

Migrations **do** apply on boot, including the Wave 3 Commerce/One files (`AddWave3SubscriptionBilling`, `AddChargeAttemptDeclineClass`, `AddCommerceDisputes`, `AddAuditEvents`). That is necessary and not sufficient: `HasOpenDispute` shipped empty, invite accept shipped without a page, Xendit shipped without a form, dispute ledger shipped as an untested event hop, pause shipped with a claim loop that the only engine test ratifies.

The test pyramid after the waves is **~993 NUnit methods, ~95% InMemory module tests, 10 integration methods, 14 architecture methods, 0 worker SKIP LOCKED tests, 0 invite-accept tests, 0 Xendit UI tests, 0 dispute-to-ledger tests, 0 pause-claim-progress tests.** That is the architecture, tenancy, worker, and test state of Lazuar Pay after Waves 0–4.

---

## 10. File index (absolute paths cited)

Architecture / host:

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Program.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Lazuar.Api.csproj`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/DatabaseMigrationExtensions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/MiddlewarePipelineExtensions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/ExecutionContextAccessor.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/appsettings.json`

BuildingBlocks:

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Domain/IMustHaveTenant.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/PlatformDbContext.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxEventBus.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxPublisherJob.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/InboxConsumerJob.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/InMemoryEventBus.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/ModuleOutboxInboxServiceCollectionExtensions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxInboxModelBuilderExtensions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/Configuration/BackgroundWorkerOptions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/MessageProcessingResultApplier.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/MessageProcessingStatus.cs`

Workers:

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.Claim.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/InvoiceReminderJob.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Workers/LhdnSubmissionJob.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Workers/LhdnStatusPollingJob.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs`

Wave 3/4 product files named in §7–8:

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/AcceptWorkspaceInvitationCommand.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/EventHandlers/NotificationDispatchDomainEventHandlers.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/TeamPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/XenditGatewayAdapter.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/CommerceGatewayDisputeCreatedHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Migrations/20260820120000_AddWave3SubscriptionBilling.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Migrations/20260820140000_AddCommerceDisputes.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Migrations/20260820150000_AddAuditEvents.cs`

Tests:

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ArchitectureTests/TenantIsolationArchitectureTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/TenantIsolation/CrossTenantIdorTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/TenantIsolation/TenantIsolationHardeningTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/One/InviteUserToWorkspaceCommandHandlerTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/XenditGatewayAdapterTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceGatewayDisputeCreatedHandlerTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/ChargebackClawbackHandlerTests.cs`
