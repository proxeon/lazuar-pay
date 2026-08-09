# Backend Folder Organization & Consistency Analysis

**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Scope:** `apps/lazuar-api` module layout (Domain / Application / Infrastructure / Contracts), `tests/`, BuildingBlocks, SharedKernel, and packages related to the API.  
**Modules compared:** One, Ops, Billing, Commerce, Payments, Lhdn, CRM, Messaging, Communications.  
**Constraint for this document:** analysis and proposed reorganization only — **no app code was modified**.  
**Date:** 2026-08-09  

---

## 1. Executive summary

The Lazuar API is a modular monolith with a **documented 4-layer project layout** per module (`Contracts`, `Domain`, `Application`, `Infrastructure`), plus shared foundations (`BuildingBlocks/*`, `SharedKernel`) and external HTTP contracts living in monorepo `packages/` (TypeSpec → OpenAPI → generated C#/TS).

**What works well**

- Physical `.csproj` boundaries exist for all nine modules (with one intentional exception: CRM has no Application project).
- Host composition is consistent: `Add[Module]Module`, `Use[Module]Subscriptions`, `Map[Module]Endpoints`, MediatR assembly scan of Application + Infrastructure.
- Every module has an Outbox publisher job (enforced by architecture tests).
- Cross-module references go through `*.Contracts` (enforced by architecture tests).
- External API contracts (`packages/api-spec`, `packages/api-types-dotnet`, `packages/api-types-ts`) are correctly **separated** from internal MediatR contracts (ADR 006).

**What is inconsistent**

- **Command/handler placement:** Application vs Infrastructure varies by module (and sometimes within a module).
- **Endpoints:** monolithic `Endpoints.cs` vs split `Endpoints/` folder; Payments also uses sibling files (`IntegrationEndpoints.cs`, `PlatformEndpoints.cs`).
- **Workers:** most modules use `Infrastructure/Workers/`; Messaging leaves inbox/outbox jobs at Infrastructure root.
- **EventHandlers:** folder vs root; Application vs Infrastructure; naming suffixes (`*Handler` vs `*IntegrationEventHandler` vs short names).
- **Domain folder taxonomy:** some modules use `Aggregates/`, `Entities/`, `ValueObjects/`, `Events/`, `Rules/`; others flatten everything at Domain root.
- **Contracts packaging:** some modules put Commands/Events under subfolders; others dump files at Contracts root; Ops Contracts is effectively empty.
- **Ports / repository interfaces:** `Ports/` (Lhdn, Payments) vs root `I*Repository` (One, Commerce, Ops, Billing, Messaging, Communications) vs none (CRM).
- **Tests:** dual patterns — umbrella `Lazuar.ModuleTests` **and** per-module projects (`Modules.Billing.Tests`, `Modules.Ops.Tests`).
- **Solution file hygiene:** empty solution folders and odd placement of `api-types-dotnet` under `/Modules/Lhdn/`.
- **SharedKernel** is a marker-only project; BuildingBlocks is healthy and layered.
- **packages/api-spec** README still references removed modules (`community`, `auth`) and does not fully document current modules.

**Risk posture of reorganization**

Most folder moves **within the same assembly** are DI-safe (MediatR scans whole assemblies). Moves that change which assembly owns a handler require updating MediatR registration only if Application is introduced (CRM) or if a layer is dropped. Architecture tests already encode the CRM-no-Application exception.

---

## 2. Canonical intended pattern (source of truth)

### 2.1 Documented module shape

From `apps/lazuar-api/README.md` and ADR `docs/architecture-decision-log/001-implementing-new-module.md`:

```
Modules/[Name]/
  Contracts/        Modules.[Name].Contracts.csproj
  Domain/           Modules.[Name].Domain.csproj
  Application/      Modules.[Name].Application.csproj
  Infrastructure/   Modules.[Name].Infrastructure.csproj
```

**Reference rules**

| Project | May reference |
|---------|----------------|
| Contracts | `BuildingBlocks.Application` only (and transitive BB Domain) |
| Domain | `BuildingBlocks.Domain`, `SharedKernel` — nothing else |
| Application | Domain, Contracts, `BuildingBlocks.Application` (+ other modules' **Contracts** only) |
| Infrastructure | Application, `BuildingBlocks.Infrastructure` (+ other modules' **Contracts** only) |

**Host (`Lazuar.Api`)** references **only** each module's Infrastructure project.

### 2.2 Documented contents per layer

| Layer | Intended contents |
|-------|-------------------|
| **Contracts** | Public integration events; public commands/queries other modules may send; query-service interfaces; no domain entities |
| **Domain** | Aggregates, entities, value objects, domain events, business rules — pure C# |
| **Application** | Use-case command/query **handlers**, domain-event handlers, repository **ports**, validators; MediatR DI marker |
| **Infrastructure** | DbContext, migrations, repositories, external adapters/gateways, Endpoints (ACL to TypeSpec DTOs), workers (inbox/outbox + domain jobs), integration-event handlers that need infra, `DependencyInjection` (`Add*Module` / `Use*Subscriptions`) |

### 2.3 Documented host integration

For each module `X`:

1. `cfg.RegisterServicesFromAssembly(typeof(Modules.X.Application.DependencyInjection).Assembly)`
2. `cfg.RegisterServicesFromAssembly(typeof(Modules.X.Infrastructure.DependencyInjection).Assembly)`
3. `builder.Services.AddXModule(configuration)`
4. `app.UseXSubscriptions()`
5. `apiGroup.MapXEndpoints()`

**Actual host note:** CRM is registered only via Infrastructure assembly for MediatR (no Application). All modules' Infrastructure assemblies are scanned so handlers living in Infrastructure still resolve.

### 2.4 External vs internal contracts (ADR 006)

| Concern | Location |
|---------|----------|
| HTTP edge DTOs / routes | `packages/api-spec` → generated into `packages/api-types-dotnet` (`Lazuar.ApiTypes`) and `packages/api-types-ts` |
| Internal CQRS + integration events | `Modules/*/Contracts` (and sometimes Application for module-private commands) |

Endpoints must map TypeSpec DTOs ↔ internal commands (anti-corruption layer). This separation is **correct** and should be preserved; reorganizing folders must **not** merge TypeSpec types into module Contracts.

### 2.5 Architecture-test invariants (folder-relevant)

File: `apps/lazuar-api/tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs`

- Module namespaces: One, Messaging, CRM, Payments, Ops, Billing, Lhdn, Commerce, Communications.
- Explicit exception: `ModulesWithoutApplication = { "Modules.CRM" }`.
- Domain isolation; Application must not reference Infrastructure; outer layers may only cross modules via Contracts.
- Every Infrastructure assembly must define a concrete `*OutboxPublisherJob`.
- BuildingBlocks must not reference module assemblies; SharedKernel must not hold business entities.

Any reorganization that renames modules or adds Application to CRM **must** update these tests.

---

## 3. Top-level `apps/lazuar-api` layout

```
apps/lazuar-api/
  BuildingBlocks/
    Application/     # CQRS ports, IEventBus, IEmailService, LLM ports, pagination, metrics facade
    Domain/          # Entity, ValueObject, IDomainEvent, IBusinessRule, IMustHaveTenant
    Infrastructure/  # PlatformDbContext, Outbox/Inbox jobs, EventBus, vault, email, JWT, R2, LLM impl
  SharedKernel/      # SharedKernelMarker only (plus csproj reference to BB.Domain)
  Modules/
    Billing/
    Commerce/
    Communications/
    CRM/
    Lhdn/
    Messaging/
    One/
    Ops/
    Payments/
  src/
    Lazuar.Api/      # Host: Program.cs, middleware, host-level EventHandlers, ExecutionContextAccessor
  tests/
    Lazuar.ArchitectureTests/
    Lazuar.IntegrationTests/
    Lazuar.ModuleTests/          # umbrella multi-module unit/auth tests
    Modules.Billing.Tests/       # isolated Billing tests
    Modules.Ops.Tests/           # isolated Ops tests
  docs/              # module-adjacent backend docs
  Lazuar.slnx
  Directory.Build.props
  Directory.Packages.props
```

### 3.1 BuildingBlocks assessment

**Follows pattern well.** Clean three-layer split mirrors modules.

| Layer | Notable folders/files |
|-------|------------------------|
| Domain | Flat: Entity, ValueObject, rules/events interfaces |
| Application | Flat + `Llm/`, `Observability/` |
| Infrastructure | Flat jobs + `Configuration/`, `Llm/`, `Observability/` |

No business modules leak into BuildingBlocks (guarded by tests). Folder depth is intentionally shallow — appropriate for a technical core.

**Minor note:** Infrastructure is “baggy” (many root-level services). Optional future grouping (`Email/`, `Security/`, `Messaging/`, `Storage/`, `Jobs/`) would improve navigability without DI impact, since registration is explicit in host/DI, not convention-by-folder.

### 3.2 SharedKernel assessment

**Structurally present, content-empty.**

- Only `SharedKernelMarker.cs` and a project reference to `BuildingBlocks.Domain`.
- README claims SharedKernel holds “business-neutral types, global value objects, system identifiers”; **none exist yet**.
- Domain projects still reference SharedKernel (dependency edge ready for future shared VOs).

**Consistency implication:** either (a) start placing truly shared primitives here when they appear, or (b) document that SharedKernel is a reserved assembly and keep it marker-only. Do not move module-specific types here (architecture tests forbid business entities).

### 3.3 Host (`src/Lazuar.Api`) assessment

Host-owned concerns (correct location):

- `Middleware/` — ApiKey, CorrelationId, TenantSecurity
- `EventHandlers/` — cache invalidation for `ApiKeyRevoked`, `WorkspaceUpdated` (cross-cutting host concerns, not module domain)
- `ExecutionContextAccessor.cs`
- `Configuration/AppOptions.cs`
- `Infrastructure/Data/PlatformDbContext.cs` — host-level (distinct from BB `PlatformDbContext` base)

Host does **not** define module endpoints (good). Host MediatR-registers Program assembly + every module Application (except CRM) + every module Infrastructure.

### 3.4 Solution file (`Lazuar.slnx`) hygiene

Issues:

1. Empty folder entries: `/Modules/Lhdn/Application/`, `/Modules/Lhdn/Domain/`, `/Modules/Lhdn/Infrastructure/`, `/Modules/Billing/Infrastructure/`, `/Modules/`, `/src/`.
2. `packages/api-types-dotnet/Lazuar.ApiContracts.csproj` is nested under solution folder `/Modules/Lhdn/` even though it is a monorepo package, not LHDN-owned.
3. `lhdn-sdk-dotnet` correctly sits under `/Packages/`.
4. Mixed path separators (`\` vs `/`) — cosmetic for SDK-style solutions on macOS.

These do not affect runtime DI but hurt IDE navigation consistency.

---

## 4. Per-module inventory (as-is)

Legend for layers: ✅ present · ⚠ present but sparse/atypical · ❌ missing · N/A not applicable.

### 4.1 One (identity / workspaces / API credentials / outbound webhooks)

| Layer | Structure |
|-------|-----------|
| **Contracts** | Flat events at root + `Events/ApiKeyRevokedIntegrationEvent.cs`; query/service interfaces at root. **No Commands/ subfolder.** |
| **Domain** | Flat entities at root + `Events/` + `Rules/`. **No Aggregates/Entities split.** |
| **Application** | `Commands/` (command + handler co-located), `Queries/` (+ `Agent/`), `EventHandlers/` (domain events), `IOneRepository`, `IOneLinkService`, DI marker. |
| **Infrastructure** | `Endpoints.cs` (large monolithic), `EventHandlers/`, `Workers/`, `Repositories/`, `Services/`, `Configuration/`, `Migrations/`, DbContext, DI. |

**Pattern score: Good (reference-quality Application command co-location).**

Strengths:

- Application owns use-case handlers.
- Infrastructure owns outbound integration webhook handlers + workers.
- Clear Workers folder: inbox, outbox, outbound webhook dispatcher, genesis bootstrapper.

Weaknesses:

- Contracts events split between root and `Events/`.
- Domain flat (no Aggregates folder) despite multiple aggregate roots (`Organization`, `GlobalUser`, `ApiCredential`, …).
- Single huge `Endpoints.cs` (auth, workspaces, webhooks, API keys, integrator provision).

### 4.2 Ops (AI chat / agent tools)

| Layer | Structure |
|-------|-----------|
| **Contracts** | **Empty of business types** — only csproj. |
| **Domain** | Flat: `OpsConversation`, `OpsMessage`. |
| **Application** | `Commands/` (handler co-located), `Services/` (ILlmOrchestratorService, ToolRegistry), `IOpsRepository`. |
| **Infrastructure** | Monolithic `Endpoints.cs`, `Services/` (LLM orchestration partials), `Repositories/`, `Workers/` (inbox/outbox only), Migrations, DI. |

**Pattern score: Good for small module; Contracts hollow.**

Notes:

- Ops is mostly host-facing UI backend; empty Contracts is acceptable if no other module publishes/consumes Ops messages.
- No domain events / integration events foldering needed today.
- Application correctly owns chat command handlers; Infrastructure owns LLM orchestration implementation.

### 4.3 Billing (ledger / credits / documents)

| Layer | Structure |
|-------|-----------|
| **Contracts** | `Commands/`, `Events/`, `IBillingQueryService`, `ICreditCostService`. **Strong foldering.** |
| **Domain** | `Aggregates/`, `Entities/`, `ValueObjects/`, plus `AccountTypes.cs` at root. **Best Domain taxonomy in the repo.** |
| **Application** | Sparse: `ILedgerRepository`, `Queries/` (+ Agent), `Llm/BillingPromptProvider` — **almost no command handlers**. |
| **Infrastructure** | `Commands/` (all write handlers), `Queries/`, `EventHandlers/`, `Workers/`, `Repositories/`, `Services/`, `Documents/`, monolithic `Endpoints.cs`, Migrations, DI. |

**Pattern score: Mixed — Domain/Contracts excellent; Application inverted.**

This is the clearest **Application-vs-Infrastructure inversion** of handlers:

- README/ADR say Application owns handlers.
- Billing implements nearly all command handlers under **Infrastructure/Commands**.
- Application holds ports + a few queries + LLM prompt provider.

Likely historical reason: handlers need QuestPDF / storage / multi-contract services. Still, other modules (Lhdn, Commerce) keep complex handlers in Application via ports.

Workers: `BillingInboxConsumerJob`, `BillingOutboxPublisherJob`, `B2cConsolidationJob`, `RevenueRecognitionJob` — good grouping.

### 4.4 Commerce (products / checkout / subscriptions / dunning)

| Layer | Structure |
|-------|-----------|
| **Contracts** | `Commands/`, `Events/`, query interfaces at root. **Strong.** |
| **Domain** | `Aggregates/`, `Entities/`, `Events/`, `ValueObjects/`, + limits constant at root. **Strong.** |
| **Application** | `Commands/` (**handlers**), `EventHandlers/` (some integration handlers), `Queries/`, `ICommerceRepository`. |
| **Infrastructure** | `Endpoints.cs` **facade** + `Endpoints/` split files, `EventHandlers/`, `Workers/`, `Repositories/`, `Services/` (large query service partials), Migrations, DI. **No Infrastructure/Commands.** |

**Pattern score: Best overall module layout (gold standard for this codebase).**

Why gold standard:

1. Domain taxonomy complete.
2. Contracts well partitioned.
3. Application owns command handlers (as documented).
4. Infrastructure splits endpoints by concern (`ProductEndpoints`, `CouponEndpoints`, `DunningCampaignEndpoints`, `PublicEndpoints`, …).
5. Workers folder complete (billing engine, dunning, checkout expiry, inbox/outbox).
6. Integration event handlers mostly in Infrastructure (need DbContext) — sensible; a few in Application for pure orchestration.

Minor inconsistencies:

- `ICommerceQueryService` lives under Application/Queries, while Contracts also exposes `ISubscriberQueryService` / `ICommerceDocumentLookup` — dual query surface (intentional ACL vs internal).
- Application has some integration event handlers (`OrderCompleted…`, subscription lifecycle) while similar gateway handlers sit in Infrastructure — dual home for EventHandlers.

### 4.5 Payments (gateways / webhooks / integration checkout)

| Layer | Structure |
|-------|-----------|
| **Contracts** | `Commands/`, `Events/`, `Queries/`, `Results/`. **Strong, includes Queries subfolder.** |
| **Domain** | `Aggregates/`, `Entities/`. Compact and clean. |
| **Application** | `Commands/` (handlers for webhook process + integration checkout), `Queries/` (+ Agent), `Ports/`, `Services/`, `Exceptions/`. |
| **Infrastructure** | `Endpoints.cs` (gateway webhooks only), **`IntegrationEndpoints.cs`**, **`PlatformEndpoints.cs`** (siblings, not under Endpoints/), `EventHandlers/`, `Gateways/`, `Commands/` (UpdatePaymentConfig), `Queries/` (GetPaymentConfig), `Repositories/`, `Configurations/`, `Workers/`, Migrations, DI. |

**Pattern score: Good with endpoint-file naming outliers.**

Notable:

- Host maps three entry points: `MapPaymentsEndpoints`, `MapPaymentsIntegrationEndpoints`, `MapPlatformEndpoints` (platform group is host-level route group, not under `/api/v1` module map only).
- Handler split: most payment use cases in Application; config update handler + config query handler in Infrastructure (EF-heavy).
- Uses `Ports/` naming like Lhdn (good internal consistency between these two modules).
- `Configurations/` (EF) vs One's `Configuration/` (options) — naming pluralization inconsistency.

### 4.6 Lhdn (e-invoice / UBL / taxpayer)

| Layer | Structure |
|-------|-----------|
| **Contracts** | **Events only** (`Events/`). No Commands/Queries in Contracts — commands are Application-private. |
| **Domain** | `Aggregates/`, `Entities/`, `Rules/`, + `ApiKeyScopes` at root. |
| **Application** | `Commands/` (command + handler co-located), `Queries/` (+ Agent), `Ports/`, `Services/` (port interfaces for vault, strategies, webhooks, templates). |
| **Infrastructure** | Monolithic `Endpoints.cs`, `EventHandlers/`, `Gateways/`, `Repositories/`, `Services/` (+ `Strategies/`, `ViewModels/`), `Schemas/` (XSD), `Templates/` (XML), `Workers/`, Migrations, DI. |

**Pattern score: Strong Application/ports design; Contracts minimal by design.**

Strengths:

- Clear ports in Application; adapters in Infrastructure (gateway, vault, Scriban, UBL validator).
- Heavy asset folders (`Schemas/`, `Templates/`) correctly in Infrastructure.
- Workers complete (inbox, outbox, status polling, submission, reference data seeder).

Weaknesses:

- Large monolithic Endpoints (integration + admin mixed).
- Contracts only events — fine if no other module needs to **send** Lhdn commands; Billing/others only react to Lhdn events.
- Module-private commands live in Application (not Contracts) — correct for encapsulation, but differs from Billing/Commerce which put many commands in Contracts for cross-module use.

### 4.7 CRM (PII registry)

| Layer | Structure |
|-------|-----------|
| **Contracts** | Flat root: commands + one event + `ICrmQueryService`. **No Commands/ or Events/ folders.** |
| **Domain** | Flat: `ClientProfileEntity`, `BillingAddress`. No Aggregates folder. |
| **Application** | **❌ Missing** (documented exception in architecture tests). |
| **Infrastructure** | Handlers at **root** (`*CommandHandler.cs`), `EventHandlers/`, `Workers/`, `Configurations/`, `CrmQueryService`, DbContext, Migrations, DI. **No Endpoints.cs** (no HTTP surface). |

**Pattern score: Intentional 3-layer exception; internally flat and least aligned with “Application owns handlers”.**

Implications:

- MediatR only scans CRM Infrastructure.
- No public HTTP endpoints — pure internal service module (correct product-wise).
- Handlers sit next to DbContext (simplest possible layout for a small module).
- README still mentions Community in places (stale product naming).

### 4.8 Messaging (tenant replica / delivery / notify)

| Layer | Structure |
|-------|-----------|
| **Contracts** | Flat single event `DispatchMessageIntegrationEvent.cs`. |
| **Domain** | Flat: `TenantReplica`, `MessageDeliveryLog`. |
| **Application** | Sparse: one `EventHandlers/` file, root-level `TenantCreatedEventHandler`, `TenantUpdatedEventHandler`, `SendTenantNotificationCommandHandler`, `ITenantReplicaRepository`. |
| **Infrastructure** | Monolithic `Endpoints.cs`, **partial** `EventHandlers/` folder, **workers at root** (`MessagingInboxConsumerJob`, `MessagingOutboxPublisherJob`), repository at root, more handlers at root (`TenantProvisioned*`, `TenantUpdated*`). |

**Pattern score: Weakest folder consistency among modules that still have 4 projects.**

Problems:

1. **No `Workers/` folder** — only module (with jobs) that leaves inbox/outbox at Infrastructure root.
2. Event handlers duplicated across Application root, Application/EventHandlers, Infrastructure root, Infrastructure/EventHandlers.
3. Naming: `TenantCreatedEventHandler` vs `TenantProvisionedIntegrationEventHandler` — parallel concepts with inconsistent suffixes.
4. Application event handlers for tenant lifecycle **and** Infrastructure handlers for similar events — hard to know which layer owns “tenant provisioning”.

### 4.9 Communications (templates / email config / broadcasts)

| Layer | Structure |
|-------|-----------|
| **Contracts** | `Commands/`, `Events/`, DTOs + query/suppression interfaces at root. |
| **Domain** | `Aggregates/` + `DefaultMessageTemplates` at root. |
| **Application** | `Commands/` (handlers), `ICommunicationsRepository`. No Queries folder. |
| **Infrastructure** | `Endpoints.cs` facade + `Endpoints/` split, `EventHandlers/`, `Workers/`, `Repositories/`, `Services/`, Migrations, DI. |

**Pattern score: Strong (second to Commerce).**

Strengths:

- Endpoint split pattern matches Commerce.
- Application owns write handlers.
- Workers include domain job (`BroadcastFanoutJob`) + inbox/outbox.

Minor:

- Some DTOs (`BroadcastDtos.cs`) live in Contracts rather than TypeSpec-only — acceptable if used as internal result types; watch for drift vs TypeSpec DTOs.

---

## 5. Cross-module comparison matrices

### 5.1 Layer presence

| Module | Contracts | Domain | Application | Infrastructure | HTTP Endpoints |
|--------|-----------|--------|-------------|----------------|----------------|
| One | ✅ | ✅ | ✅ | ✅ | ✅ monolithic |
| Ops | ⚠ empty | ✅ | ✅ | ✅ | ✅ monolithic |
| Billing | ✅ | ✅ | ⚠ thin | ✅ | ✅ monolithic |
| Commerce | ✅ | ✅ | ✅ | ✅ | ✅ **split** |
| Payments | ✅ | ✅ | ✅ | ✅ | ✅ multi-file siblings |
| Lhdn | ⚠ events only | ✅ | ✅ | ✅ | ✅ monolithic |
| CRM | ✅ flat | ✅ flat | ❌ | ✅ | ❌ none |
| Messaging | ⚠ thin | ✅ flat | ⚠ thin | ✅ | ✅ monolithic |
| Communications | ✅ | ✅ | ✅ | ✅ | ✅ **split** |

### 5.2 Endpoints placement

| Module | Pattern | Files |
|--------|---------|-------|
| Commerce | **Facade + Endpoints/** | `Endpoints.cs` + 8 files under `Endpoints/` |
| Communications | **Facade + Endpoints/** | `Endpoints.cs` + 3 files under `Endpoints/` |
| Payments | **Sibling files at Infrastructure root** | `Endpoints.cs`, `IntegrationEndpoints.cs`, `PlatformEndpoints.cs` |
| One, Ops, Billing, Lhdn, Messaging | **Single Endpoints.cs** | one large file each |
| CRM | N/A | no HTTP API |

**Inconsistency:** three different endpoint organization styles.

### 5.3 Workers placement

| Module | Workers location | Jobs present |
|--------|------------------|--------------|
| One | `Infrastructure/Workers/` | Inbox, Outbox, OutboundWebhookDispatcher, SystemGenesisBootstrapper (+ helper) |
| Ops | `Infrastructure/Workers/` | Inbox, Outbox |
| Billing | `Infrastructure/Workers/` | Inbox, Outbox, B2cConsolidation, RevenueRecognition |
| Commerce | `Infrastructure/Workers/` | Inbox, Outbox, BillingEngine, DunningEngine, CheckoutSessionExpiry |
| Payments | `Infrastructure/Workers/` | Inbox, Outbox |
| Lhdn | `Infrastructure/Workers/` | Inbox, Outbox, StatusPolling, Submission, ReferenceDataSeeder |
| CRM | `Infrastructure/Workers/` | Inbox, Outbox |
| Communications | `Infrastructure/Workers/` | Inbox, Outbox, BroadcastFanout |
| **Messaging** | **Infrastructure root** | Inbox, Outbox |

**Inconsistency:** Messaging is the sole outlier for Workers folder.

### 5.4 EventHandlers placement & naming

| Module | Application EventHandlers | Infrastructure EventHandlers | Root-level handlers |
|--------|---------------------------|------------------------------|---------------------|
| One | Domain event handlers folder | Outbound webhook handlers folder | — |
| Commerce | Some integration handlers | Gateway / template handlers | — |
| Billing | — | Many integration handlers | — |
| Payments | — | Integration handlers | — |
| Lhdn | — | Invoice/refund handlers | — |
| Communications | — | Lifecycle + fulfillment handlers | — |
| CRM | N/A | One profile-updated handler | Command handlers at infra root |
| Messaging | Mixed folder + root | Folder + **root** | Heavy root scatter |
| Ops | — | — | — |

**Naming variants observed:**

- `*IntegrationEventHandler` (majority, preferred)
- `*EventHandler` (Messaging tenant handlers, Billing `PlatformTopUpEventHandler`)
- `*Handler` short form (Billing `ApiCreditPurchasedHandler`, `ChargebackClawbackHandler`, …)
- Multi-handler files: `LifecycleEventHandlers`, `OutboundWebhookEventHandlers`, `SubscriptionLifecycleIntegrationEventHandlers`

### 5.5 Command / handler ownership

| Module | Command definitions | Handler home |
|--------|---------------------|--------------|
| One | Application (module-private) | Application |
| Ops | Application | Application |
| Commerce | **Contracts** | **Application** |
| Communications | **Contracts** | **Application** |
| Payments | Contracts (some) + Application (ProcessGatewayWebhook) | Application primary; Infrastructure for config |
| Lhdn | Application (module-private) | Application |
| Billing | **Contracts** | **Infrastructure** |
| CRM | Contracts | Infrastructure (only layer) |
| Messaging | Application (SendTenantNotification) | Application + Infrastructure for integration |

**Core inconsistency:** Billing (and CRM by necessity) put handlers in Infrastructure while Commerce/One/Lhdn/Ops/Communications put them in Application.

### 5.6 Domain taxonomy

| Module | Aggregates/ | Entities/ | ValueObjects/ | Events/ | Rules/ | Flat root types |
|--------|-------------|-----------|---------------|---------|--------|-----------------|
| Billing | ✅ | ✅ | ✅ | — | — | AccountTypes |
| Commerce | ✅ | ✅ | ✅ | ✅ | — | ChargeAttemptLimits |
| Lhdn | ✅ | ✅ | — | — | ✅ | ApiKeyScopes |
| Communications | ✅ | — | — | — | — | DefaultMessageTemplates |
| Payments | ✅ | ✅ | — | — | — | — |
| One | — | — | — | ✅ | ✅ | many aggregates as root files |
| Ops | — | — | — | — | — | 2 types |
| CRM | — | — | — | — | — | 2 types |
| Messaging | — | — | — | — | — | 2 types |

### 5.7 Contracts packaging style

| Module | Commands/ | Events/ | Queries/ | Root interfaces / DTOs |
|--------|-----------|---------|----------|------------------------|
| Billing | ✅ | ✅ | — | IBillingQueryService, ICreditCostService |
| Commerce | ✅ | ✅ | — | ICommerceDocumentLookup, ISubscriberQueryService |
| Communications | ✅ | ✅ | — | BroadcastDtos, ICommunicationsQueryService, ISuppressionService |
| Payments | ✅ | ✅ | ✅ | — |
| Lhdn | — | ✅ | — | — |
| One | — | partial | — | interfaces + events at root |
| CRM | — | — | — | all flat |
| Messaging | — | — | — | single event file |
| Ops | — | — | — | empty |

### 5.8 Ports / repository interface placement

| Pattern | Modules |
|---------|---------|
| `Application/Ports/` | Lhdn, Payments |
| Root `I*Repository` / service ports in Application | One, Ops, Commerce, Billing, Messaging, Communications |
| Interfaces in Contracts for cross-module query | Billing, Commerce, CRM, Communications, One (`IOneQueryService`, `IApiCredentialService`) |
| No Application ports (handlers use DbContext directly) | CRM |

### 5.9 Configuration folder naming

| Name | Modules | Purpose |
|------|---------|---------|
| `Configuration/` (singular) | One | Options/settings classes |
| `Configurations/` (plural) | CRM, Payments | EF Core `IEntityTypeConfiguration` |
| (none) | Most others | EF config often inline in DbContext |

---

## 6. Packages related to API (contracts placement)

### 6.1 Layout

```
packages/
  api-spec/                 # TypeSpec source of truth (HTTP)
    common/models.tsp
    modules/
      billing/, commerce/, communications/, crm/, lhdn/,
      messaging/, one/, ops/, payments/, platform/
    docs-*.tsp, main.tsp, dist/**/openapi.yaml
  api-types-dotnet/         # NSwag-generated C# DTOs (Lazuar.ApiTypes / Lazuar.ApiContracts.csproj)
  api-types-ts/             # openapi-typescript client types for frontends
  lhdn-sdk-dotnet/          # Kiota-generated consumer SDK for LHDN product API
  lhdn-sdk-ts/              # TS consumer SDK
  eslint-config/, typescript-config/, ui/   # not API domain
```

### 6.2 Contracts placement rules (as implemented)

| Contract kind | Placement | Referenced by |
|---------------|-----------|---------------|
| External HTTP DTO | `packages/api-types-dotnet` (`Lazuar.ApiTypes`) | Endpoints, some Application commands (Lhdn uses DTO in command payload) |
| Internal integration event / cross-module command | `Modules/*/Contracts` | Other modules' Application/Infrastructure |
| Module-private command | Often `Modules/*/Application/Commands` | Same module only |
| Consumer SDK | `packages/lhdn-sdk-*` | External integrators; **not** used by modular monolith internals |

### 6.3 Issues

1. **api-spec README is stale** — still documents `auth/` and `community/` modules that no longer exist; under-documents current modules (billing, payments, lhdn, communications, crm, platform).
2. **Solution placement of api-types-dotnet under `/Modules/Lhdn/`** is misleading; package is platform-wide.
3. **TypeSpec coverage vs modules:** `crm` and `messaging` have models only (no routes tsp) — aligns with CRM having no endpoints and Messaging having a thin internal notify API that may not be fully TypeSpec-driven.
4. **Internal Contracts must stay inside Modules** — do **not** move MediatR events into `packages/`; that would reverse ADR 006 and couple packages to BuildingBlocks.Application.
5. **LHDN SDK packages** are correctly productized separately; regeneration paths must stay independent of module Infrastructure folders.

### 6.4 Dependency direction (contracts)

```
packages/api-spec  --generate-->  packages/api-types-dotnet  -->  referenced by module Infra/App + host
packages/api-spec  --generate-->  packages/api-types-ts      -->  frontends

Modules.A.Contracts  -->  BuildingBlocks.Application
Modules.B.Application/Infrastructure  -->  Modules.A.Contracts   (allowed)
Modules.B.Domain  -/->  anything module-related               (forbidden)
```

---

## 7. Test project organization

### 7.1 As-is structure

```
apps/lazuar-api/tests/
  Lazuar.ArchitectureTests/     # NetArchTest boundary + tenant isolation guards
    ModuleBoundaryTests.cs
    TenantIsolationArchitectureTests.cs
    TestData/
  Lazuar.IntegrationTests/      # DB/integration-ish tests (Billing/Commerce focused, flat)
    BillingDbContextTests.cs
    BillingQueryServiceTests.cs
    CommerceQueryServiceTests.cs
    CreditDeductionConcurrencyTests.cs
  Lazuar.ModuleTests/           # UMBRELLA unit/authorization tests by module folders
    Billing/{Commands,Domain,EventHandlers,Workers}/
    BuildingBlocks/
    Commerce/{Workers}/
    Communications/
    CRM/
    EventHandlers/              # HOST event handlers (not under a module)
    Lhdn/{Strategies}/
    Messaging/
    Observability/
    One/
    Payments/
    TenantIsolation/
  Modules.Billing.Tests/        # SEPARATE project (CreditHold, TenantCreditBalance)
  Modules.Ops.Tests/            # SEPARATE project (Services/LlmOrchestratorServiceTests)
```

### 7.2 Inconsistencies

1. **Two competing patterns for module tests**
   - Prefer umbrella: `Lazuar.ModuleTests/<Module>/…`
   - Prefer per-module project: `Modules.<Name>.Tests`
   - Billing uses **both** (umbrella has many Billing tests; separate project has 2 more).
   - Ops uses only the separate project (no Ops folder under ModuleTests).

2. **Inconsistent depth under ModuleTests**
   - Billing has Commands/Domain/EventHandlers/Workers subfolders.
   - Most modules dump tests as flat files under module name.
   - Lhdn has Strategies/; Commerce has Workers/; others flat.

3. **Host concerns mixed into ModuleTests**
   - `EventHandlers/ApiKeyRevoked…` and `Observability/*` test host/BB — not a “module”.
   - Project references `Lazuar.Api` host assembly (heavy) for a few host-handler tests.

4. **IntegrationTests is thin and not foldered by module** — only Billing/Commerce currently.

5. **ArchitectureTests** is clean and should remain separate.

6. **No dedicated test projects** for One, Commerce, Payments, Lhdn, Messaging, Communications, CRM — all live in umbrella (except Ops/Billing partials).

### 7.3 What works

- Architecture tests as a dedicated project with anchors for every module assembly.
- ModuleTests folders roughly mirror Modules names.
- Authorization metadata tests co-located with module name (Payments, Lhdn, Commerce, Messaging).

---

## 8. Which modules follow the pattern well

### 8.1 Tier A — Follow the pattern well (use as templates)

| Rank | Module | Why |
|------|--------|-----|
| 1 | **Commerce** | Full Domain taxonomy; Contracts Commands/Events; Application owns handlers; Infrastructure splits Endpoints + EventHandlers + Workers + Services; DI host hooks complete. |
| 2 | **Communications** | Same endpoint-split pattern; Application handlers; Contracts partitioned; Workers complete. |
| 3 | **Lhdn** | Excellent Ports/Services split; Application co-located commands+handlers; Infrastructure adapters + assets; Workers rich. Contracts intentionally event-only. |
| 4 | **Payments** | Ports folder; Contracts has Commands/Events/Queries; Application primary for use cases; Workers present. Endpoint file layout is the main wart. |
| 5 | **One** | Application-first handlers; Domain events/rules; Workers complete. Endpoint monolith and Contracts event scatter are the main warts. |

### 8.2 Tier B — Acceptable but incomplete / inverted

| Module | Why tier B |
|--------|------------|
| **Ops** | Correct 4-layer skeleton for a small module; empty Contracts; simple Domain; fine as-is until cross-module contracts appear. |
| **Billing** | Best Domain + Contracts packaging; **handler layer inverted** into Infrastructure; Application thin. |
| **CRM** | Documented 3-layer exception; small and coherent, but flat Contracts/Domain and no Application. |

### 8.3 Tier C — Needs folder cleanup

| Module | Why tier C |
|--------|------------|
| **Messaging** | 4 projects exist, but EventHandlers and Workers are scattered; root-level infra noise; naming inconsistency. Highest navigation friction per lines of code. |

---

## 9. Inconsistencies catalog (detailed)

### 9.1 Endpoints

| ID | Issue | Seen in | Preferred convention |
|----|-------|---------|----------------------|
| E1 | Monolithic Endpoints.cs vs split folder | One, Ops, Billing, Lhdn, Messaging vs Commerce, Communications | Facade `Endpoints.cs` + `Endpoints/*.cs` when file exceeds ~150–200 lines or >1 route group |
| E2 | Payments uses sibling `*Endpoints.cs` at root instead of `Endpoints/` | Payments | Move to `Endpoints/WebhooksEndpoints.cs`, `Endpoints/IntegrationEndpoints.cs`, `Endpoints/PlatformEndpoints.cs` |
| E3 | CRM has no endpoints (OK) but also no comment/README section on “internal-only module” | CRM | Document internal-only in module README |
| E4 | Route group prefixes inconsistent in style (`/one`, `/admin/commerce`, `/webhooks/payments`, `/lhdn`) | all | Product decision; not a folder issue — keep as-is |

**DI impact of E1/E2:** None if extension method names and namespaces stay stable, or if Program.cs usings updated when namespaces change. Prefer keeping `namespace Modules.X.Infrastructure` for Map* methods.

### 9.2 Workers

| ID | Issue | Seen in | Preferred |
|----|-------|---------|-----------|
| W1 | Inbox/Outbox jobs not under `Workers/` | Messaging | `Infrastructure/Workers/` |
| W2 | Domain-specific jobs sometimes mixed with inbox/outbox (OK) | Billing, Commerce, Lhdn, One, Communications | Keep together under Workers |
| W3 | Messaging jobs not named with module prefix consistently? | Messaging uses `MessagingInboxConsumerJob` — OK | Continue `ModuleNameInboxConsumerJob` / `ModuleNameOutboxPublisherJob` |

**DI impact:** None if class names and DI registration (`AddHostedService<T>`) stay the same; folder moves within project do not affect DI.

### 9.3 EventHandlers

| ID | Issue | Seen in | Preferred |
|----|-------|---------|-----------|
| EH1 | Handlers at Infrastructure root | Messaging, CRM (commands) | Folders: `EventHandlers/` for events; `Commands/` for command handlers if kept in Infra |
| EH2 | Application vs Infrastructure dual homes without rule | Commerce, Messaging | **Rule:** Domain event handlers → Application; Integration event handlers that touch DbContext/gateways → Infrastructure |
| EH3 | Naming suffix chaos | Billing short names; Messaging `EventHandler` | Standardize on `*IntegrationEventHandler` / `*DomainEventHandler` |
| EH4 | Multi-handler mega-files | Communications Lifecycle, One Outbound | Allow multi-handler only when tightly related; prefer one type per file for discoverability |

### 9.4 Command handlers Application vs Infrastructure

| ID | Issue | Modules | Preferred |
|----|-------|---------|-----------|
| CH1 | Handlers in Infrastructure despite Application existing | Billing, Payments (partial) | Application owns handlers; inject ports |
| CH2 | No Application layer | CRM | Keep exception **or** introduce thin Application later |
| CH3 | Command types in Contracts vs Application | Commerce/Billing vs One/Lhdn | **Rule:** Contracts only if another module must send/handle; else Application |

### 9.5 Domain structure

| ID | Issue | Modules | Preferred |
|----|-------|---------|-----------|
| D1 | Flat Domain for multi-aggregate modules | One | Introduce Aggregates/ (or Entities/) when >2 roots |
| D2 | ValueObjects folder only in Billing/Commerce | others | Use when VO count >1 or shared within module |
| D3 | Domain Events folder only in One/Commerce | others | Add when domain events exist |

### 9.6 Contracts structure

| ID | Issue | Modules | Preferred |
|----|-------|---------|-----------|
| C1 | Flat dump | CRM, Messaging, One (partial) | `Commands/`, `Events/`, `Queries/` when any count >2 |
| C2 | Empty Contracts project | Ops | Keep empty project for future + reference symmetry, or document “placeholder” |
| C3 | DTOs in Contracts that might belong in TypeSpec | Communications BroadcastDtos | Prefer TypeSpec for HTTP; keep internal-only DTOs in Contracts |

### 9.7 Ports / Services naming

| ID | Issue | Preferred |
|----|-------|-----------|
| P1 | `Ports/` vs root interfaces | Use `Application/Ports/` when ≥2 ports; root OK for single repository |
| P2 | `Services/` in Application (interfaces) vs Infrastructure (impl) | Application: interfaces only under Ports or Services; Infrastructure: implementations under Services/Gateways/Repositories |
| P3 | EF `Configurations/` vs options `Configuration/` | Keep plural for EF; singular for options — document both |

### 9.8 Tests

| ID | Issue | Preferred |
|----|-------|-----------|
| T1 | Dual test project strategies | Pick one primary: **umbrella ModuleTests by default**; per-module projects only when isolation/reference graph requires it |
| T2 | Billing split across ModuleTests + Modules.Billing.Tests | Merge into one home |
| T3 | Ops only in Modules.Ops.Tests | Either move under ModuleTests/Ops or keep per-module and migrate others later — avoid hybrid long-term |
| T4 | Flat IntegrationTests | Folder by module when more tests land |
| T5 | Host tests under ModuleTests/EventHandlers | Rename to `Host/` or `Lazuar.Api/` |

### 9.9 Solution / packages / docs

| ID | Issue | Preferred |
|----|-------|-----------|
| S1 | Empty solution folders | Remove dead folder nodes |
| S2 | api-types-dotnet under Lhdn solution folder | Move to `/Packages/` |
| S3 | Stale api-spec README | Update module list to current |
| S4 | SharedKernel empty vs documented purpose | Document marker-only reality in README |
| S5 | Module READMEs stale (Community references) | Sweep product names |

---

## 10. DI registration map (reorganization safety baseline)

Understanding current registration is required so moves do not break boot.

### 10.1 MediatR assembly scan (Program.cs)

Scanned assemblies:

- `Lazuar.Api` (host handlers)
- Application: One, Messaging, Payments, Ops, Billing, Lhdn, Commerce, Communications (**not CRM**)
- Infrastructure: One, Messaging, Payments, **CRM**, Ops, Billing, Lhdn, Commerce, Communications

**Implication:** A handler class is discovered **if it lives in any scanned assembly**, regardless of folder. Moving a handler from Application to Infrastructure (or reverse) stays DI-safe **only if both assemblies remain registered** for that module. Moving CRM handlers into a new Application project requires adding that assembly to the scan list.

### 10.2 Module DI entry points

| Extension | Location | Registers (typical) |
|-----------|----------|---------------------|
| `AddXModule` | `Modules.X.Infrastructure.DependencyInjection` | DbContext, repos, services, HostedServices (workers) |
| `UseXSubscriptions` | same | Event bus subscriptions for cross-module integration events |
| `MapXEndpoints` | `Modules.X.Infrastructure.Endpoints` (or siblings) | Minimal API routes |

**Implication:** Renaming `DependencyInjection` type or moving it to another assembly breaks host `typeof` anchors. Folder moves **within** Infrastructure are safe. Renaming extension methods requires Program.cs edits.

### 10.3 Architecture test anchors

`ModuleBoundaryTests` static constructor uses `typeof` on Domain/Application/Infrastructure types. Adding CRM Application or renaming marker types requires updating anchors and `ModulesWithoutApplication`.

### 10.4 Safe vs unsafe moves

| Change | DI-safe? | Notes |
|--------|----------|-------|
| Move file within same `.csproj` | ✅ Yes | Namespace optional update |
| Rename folder only | ✅ Yes | |
| Split Endpoints.cs into Endpoints/*.cs same namespace | ✅ Yes | Keep Map* method signatures |
| Move handler Application → Infrastructure | ✅ Yes | Both assemblies scanned (except CRM has no App) |
| Move handler Infrastructure → Application | ✅ Yes | For modules with Application scanned |
| Create CRM Application + move handlers | ⚠ Careful | Add MediatR scan + remove from ModulesWithoutApplication + csproj refs + slnx |
| Move type between modules | ❌ Unsafe without redesign | Breaks boundaries/tests |
| Move integration events to packages | ❌ Do not | Violates ADR 006 |
| Rename MapBillingEndpoints | ⚠ Requires Program.cs | |

---

## 11. Proposed target convention (canonical folder template)

Recommend adopting the following as the **house style**. Commerce is the closest living example.

```
Modules/[Name]/
  README.md
  Contracts/
    Modules.[Name].Contracts.csproj
    Commands/                 # only if cross-module or multi-consumer
      XxxCommand.cs
    Events/
      XxxIntegrationEvent.cs
    Queries/                  # optional cross-module query DTOs
    I[Name]QueryService.cs    # optional
  Domain/
    Modules.[Name].Domain.csproj
    Aggregates/               # when ≥1 aggregate root
    Entities/                 # non-root entities
    ValueObjects/             # when needed
    Events/                   # domain events
    Rules/                    # IBusinessRule implementations
  Application/
    Modules.[Name].Application.csproj
    DependencyInjection.cs    # MediatR assembly marker (may be empty static class)
    Commands/
      XxxCommand.cs           # if not in Contracts
      XxxCommandHandler.cs    # preferred separate file as handlers grow
    Queries/
      XxxQuery.cs
      XxxQueryHandler.cs
      Agent/                  # ops-agent queries when present
    EventHandlers/            # domain-event handlers primarily
    Ports/                    # IRepository, gateway ports (preferred name)
  Infrastructure/
    Modules.[Name].Infrastructure.csproj
    DependencyInjection.cs    # AddXModule + UseXSubscriptions
    [Name]DbContext.cs
    Endpoints.cs              # MapXEndpoints facade (thin)
    Endpoints/                # one file per route area when split needed
    EventHandlers/            # integration-event handlers (I/O bound)
    Workers/                  # *InboxConsumerJob, *OutboxPublisherJob, domain jobs
    Repositories/
    Services/                 # query services, adapters
    Gateways/                 # external HTTP providers when present
    Configurations/           # EF IEntityTypeConfiguration
    Configuration/            # Options/settings bindings when present
    Migrations/
```

### 11.1 Placement rules (decision table)

| Artifact | Put it in |
|----------|-----------|
| Integration event other modules consume | Contracts/Events |
| Command other modules send via MediatR | Contracts/Commands |
| Command only used by this module's endpoints | Application/Commands |
| Command/query handler | Application (preferred) |
| Handler that must reference EF types heavily | Prefer still Application via repository port; if exceptional, Infrastructure/Commands with comment |
| Domain event handler (react within module) | Application/EventHandlers |
| Integration event handler (inbox-driven, I/O) | Infrastructure/EventHandlers |
| Minimal API mapping | Infrastructure/Endpoints* |
| Background job | Infrastructure/Workers |
| DbContext / migrations | Infrastructure |
| Aggregate root | Domain/Aggregates |
| TypeSpec HTTP DTO | packages/api-spec → generated packages |
| Consumer SDK | packages/*-sdk-* |

### 11.2 Naming rules

| Kind | Pattern |
|------|---------|
| Integration event | `SomethingHappenedIntegrationEvent` |
| Integration handler | `SomethingHappenedIntegrationEventHandler` |
| Domain event | `SomethingHappenedDomainEvent` |
| Domain handler | `SomethingHappenedDomainEventHandler` |
| Inbox job | `{Module}InboxConsumerJob` |
| Outbox job | `{Module}OutboxPublisherJob` |
| Map endpoints | `Map{Module}Endpoints` (+ optional `Map{Module}{Area}Endpoints`) |
| DI | `Add{Module}Module`, `Use{Module}Subscriptions` |

---

## 12. Proposed reorganization plan (phased, without breaking DI)

Principles for every phase:

1. Prefer **folder moves within the same project**.
2. Keep public extension method names stable.
3. Keep namespaces stable where possible (`Modules.X.Infrastructure` for Map* methods).
4. Run `dotnet test` on ArchitectureTests + ModuleTests after each phase.
5. No functional behavior changes — structure only.

### Phase 0 — Documentation & solution hygiene (zero runtime risk)

**Actions**

1. Update `packages/api-spec/README.md` module tree to current modules; remove community/auth examples or mark historical.
2. Clean `Lazuar.slnx`:
   - Remove empty folder nodes.
   - Move `Lazuar.ApiContracts.csproj` solution entry from `/Modules/Lhdn/` to `/Packages/`.
3. Clarify in `apps/lazuar-api/README.md`:
   - SharedKernel is currently marker-only.
   - Document endpoint-split convention and Workers folder requirement.
   - Document CRM Application exception.
4. Fix stale Community references in module READMEs (CRM, etc.).

**DI impact:** None.

### Phase 1 — Messaging cleanup (highest friction / lowest risk)

**Actions**

1. Move `MessagingInboxConsumerJob.cs`, `MessagingOutboxPublisherJob.cs` → `Infrastructure/Workers/`.
2. Move root Infrastructure event handlers into `Infrastructure/EventHandlers/`:
   - `TenantProvisionedIntegrationEventHandler.cs`
   - `TenantProvisionedSeedingHandler.cs`
   - `TenantUpdatedIntegrationEventHandler.cs`
3. Consolidate Application handlers under `Application/EventHandlers/` (move root handlers into folder).
4. Rename handlers for suffix consistency (`*IntegrationEventHandler`) **only if** no string-based type resolution depends on names (Outbox stores type names — **verify** `TypeResolver` / outbox payload type full names before renaming types).

**Critical warning on renames:** Outbox/Inbox message type strings often store assembly-qualified or full type names. **Renaming event types or handler types that are used as subscription keys can break in-flight messages.** Prefer folder moves first; rename types only after confirming serialization keys.

**DI impact:** Folder moves safe. Type renames need verification of event bus + outbox stored types.

### Phase 2 — Endpoints consolidation (navigability)

**Actions**

1. **Payments:** create `Infrastructure/Endpoints/` and move:
   - `Endpoints.cs` → `Endpoints/WebhookEndpoints.cs` (or keep name)
   - `IntegrationEndpoints.cs` → `Endpoints/IntegrationEndpoints.cs`
   - `PlatformEndpoints.cs` → `Endpoints/PlatformEndpoints.cs`
   - Keep a thin `Endpoints.cs` facade **or** leave Map methods in their files if Program already calls three methods — **do not change** Program call sites in this phase.
2. **One:** split `Endpoints.cs` into `Endpoints/AuthEndpoints.cs`, `WorkspacesEndpoints.cs`, `WebhooksEndpoints.cs`, `ApiCredentialsEndpoints.cs`, `IntegratorEndpoints.cs` + facade `MapOneEndpoints`.
3. **Lhdn:** split integration vs admin groups into `Endpoints/IntegrationEndpoints.cs` and `Endpoints/AdminEndpoints.cs`.
4. **Billing / Ops / Messaging:** split only if still large after other work; Messaging is small enough to leave monolithic.

**DI impact:** None if extension method names unchanged.

### Phase 3 — EventHandlers naming & folder pass (cosmetic + discoverability)

**Actions**

1. Ensure every Infrastructure integration handler lives under `EventHandlers/`.
2. Prefer renaming Billing short handlers (`ApiCreditPurchasedHandler` → `ApiCreditPurchasedIntegrationEventHandler`) **after** confirming no type-name coupling in tests only (tests can update). Avoid renaming integration **event** types.
3. Document dual-location rule for Commerce Application vs Infrastructure event handlers; optionally migrate pure orchestration handlers with a comment trail rather than mass move.

**DI impact:** Folder moves safe; type renames of **handlers** are usually safe (MediatR open-generic registration by interface); event type renames are not.

### Phase 4 — Billing handler rebalancing (optional, higher effort)

**Goal:** Align Billing with Commerce so Application owns command handlers.

**Actions (careful)**

1. Introduce/keep ports already present (`ILedgerRepository`).
2. Move Infrastructure/Commands handlers → Application/Commands **only if** they do not need Infrastructure-only types (QuestPDF documents, R2). If they need those, introduce Application ports (`IDocumentRenderer`, `IObjectStorage`) implemented in Infrastructure.
3. Move Infrastructure/Queries handlers similarly where pure.
4. Leave EventHandlers in Infrastructure (I/O + multi-contract).

**DI impact:** Safe while both assemblies remain MediatR-scanned. Requires project references: Application must not gain Infrastructure references — use ports.

**Do not do this phase as a pure “drag file”** without extracting ports; that would create Application → Infrastructure dependency and fail architecture tests.

### Phase 5 — CRM Application layer (optional product decision)

**Option A — Keep 3-layer exception (recommended short-term)**

- Document prominently.
- Still apply folder hygiene: `Contracts/Commands`, `Contracts/Events`, `Infrastructure/Commands` for handlers.

**Option B — Add Application project**

1. Create `Modules/CRM/Application`.
2. Move command handlers + any pure logic.
3. Infrastructure references Application.
4. Update:
   - `Lazuar.slnx`
   - Host MediatR scan
   - ArchitectureTests anchors + remove from `ModulesWithoutApplication`
   - Any test project references
5. Keep endpoints absent.

**DI impact:** Requires host + test updates; mechanical but not free.

### Phase 6 — Domain taxonomy normalization

**Actions**

1. **One:** introduce `Domain/Aggregates/` (or keep files, add folders gradually: Organization, GlobalUser, ApiCredential, …).
2. **CRM:** optionally `Aggregates/ClientProfile.cs` rename from Entity suffix — only if product language wants Aggregate naming; low priority.
3. Do not force Aggregates folders on Ops/Messaging while they remain 1–2 types.

**DI impact:** None (same assembly). Namespace changes need usings updated.

### Phase 7 — Contracts packaging normalization

**Actions**

1. CRM: `Commands/`, `Events/` folders.
2. One: move root integration events into `Events/`.
3. Messaging: keep single event but place under `Events/` for consistency.
4. Ops: leave empty or add README note “no public contracts yet”.

**DI impact:** Namespace changes only; update usings across consumers.

### Phase 8 — Tests reorganization

**Recommended target**

```
tests/
  Lazuar.ArchitectureTests/          # unchanged role
  Lazuar.IntegrationTests/
    Billing/
    Commerce/
    ...
  Lazuar.ModuleTests/                # primary unit home for all modules
    Host/                            # was EventHandlers for host
    BuildingBlocks/
    Observability/
    TenantIsolation/
    One/
    Ops/                             # absorb Modules.Ops.Tests
    Billing/                         # absorb Modules.Billing.Tests
      Commands/
      Domain/
      EventHandlers/
      Workers/
    Commerce/
    ...
```

**Actions**

1. Move `Modules.Billing.Tests/*` → `Lazuar.ModuleTests/Billing/…` and delete empty project (or keep project temporarily with type-forward — prefer delete).
2. Move `Modules.Ops.Tests/*` → `Lazuar.ModuleTests/Ops/…`.
3. Update slnx test folder.
4. Folder IntegrationTests by module as tests grow.
5. Keep ArchitectureTests separate forever.

**DI impact:** None (test assemblies only). Update CI scripts if they pass specific csproj paths (check Taskfile / CI).

**Alternative long-term:** go the opposite direction — one test project per module (`Modules.*.Tests`) for parallelization. That is valid but larger churn; the codebase currently centers on ModuleTests, so absorbing orphans is cheaper.

### Phase 9 — BuildingBlocks optional grouping (lowest priority)

Only if navigation pain grows:

```
BuildingBlocks/Infrastructure/
  Jobs/           # OutboxPublisherJob, InboxConsumerJob, DatabaseJobTrigger
  Security/       # Jwt, Password, MagicLink, AesSecretVault
  Email/
  Messaging/
  Storage/
  Llm/
  Observability/
  Configuration/
```

**DI impact:** None if host registrations stay explicit.

---

## 13. Module-by-module target deltas

### One

| Current | Target |
|---------|--------|
| Monolithic Endpoints | Split under Endpoints/ |
| Contracts events at root + Events/ | All under Events/ |
| Domain flat aggregates | Aggregates/ (gradual) |
| Application solid | Keep |
| Workers solid | Keep |

### Ops

| Current | Target |
|---------|--------|
| Empty Contracts | Keep + document |
| Simple Domain | Keep flat |
| Application + Workers good | Keep |
| Endpoints small | Keep monolithic |

### Billing

| Current | Target |
|---------|--------|
| Domain/Contracts excellent | Keep |
| Handlers in Infrastructure | Phase 4 extract to Application via ports |
| Monolithic Endpoints | Split when needed (admin vs public groups already in one file) |
| Workers solid | Keep |

### Commerce

| Current | Target |
|---------|--------|
| Already gold standard | Use as template; only clarify EventHandlers dual home in README |

### Payments

| Current | Target |
|---------|--------|
| Sibling endpoint files | Phase 2 Endpoints/ folder |
| Handlers mostly Application | Keep; optionally move config handlers up when ports allow |
| Ports/ present | Keep as model for others |

### Lhdn

| Current | Target |
|---------|--------|
| Application/ports excellent | Keep |
| Monolithic Endpoints | Split integration vs admin |
| Contracts events-only | Keep (document) |
| Schemas/Templates location | Keep in Infrastructure |

### CRM

| Current | Target |
|---------|--------|
| No Application | Keep exception (Phase 5 optional) |
| Flat Contracts | Commands/ + Events/ |
| Root command handlers | Infrastructure/Commands/ |
| No endpoints | Keep |

### Messaging

| Current | Target |
|---------|--------|
| Workers at root | Workers/ |
| Scattered EventHandlers | Folders only |
| Thin Contracts | Events/ folder |
| Application thin | Keep; ensure single home for tenant lifecycle handling |

### Communications

| Current | Target |
|---------|--------|
| Near gold standard | Keep; minor DTO ownership review |

---

## 14. Packages reorganization notes

### Keep

- TypeSpec in `packages/api-spec` mirrored under `modules/{name}`.
- Generated C#/TS in sibling packages.
- LHDN SDKs separate for external consumers.
- Module internal Contracts **inside** `apps/lazuar-api/Modules/*/Contracts`.

### Change

1. Solution folder for `api-types-dotnet` → `/Packages/`.
2. api-spec README module list.
3. Optionally add `packages/api-spec/modules/README.md` mapping TypeSpec modules ↔ backend Modules ↔ OpenAPI dist slices.

### Do not

- Do not create `packages/modules-contracts` NuGet for MediatR contracts without a hard product reason (would complicate modular monolith and versioning).
- Do not reference `lhdn-sdk-dotnet` from internal modules as a substitute for Contracts (SDK is external-facing).

---

## 15. Acceptance checklist for “organized enough”

Use this as the exit criteria for maintenance work:

- [ ] Every module with background jobs has `Infrastructure/Workers/`.
- [ ] Every module with HTTP API has either a thin facade `Endpoints.cs` or documented multi-map methods; large modules use `Endpoints/`.
- [ ] Every integration-event handler lives under `*/EventHandlers/` (not project root).
- [ ] Handler ownership rule documented: Application default; Infrastructure only for I/O-bound integration handlers (and CRM exception).
- [ ] Contracts use `Commands/` / `Events/` folders when file count > 2.
- [ ] Domain uses `Aggregates/` when multiple aggregate roots exist.
- [ ] Test projects: single primary strategy (umbrella **or** per-module), no Billing/Ops hybrid.
- [ ] ArchitectureTests still green; CRM exception still explicit if Application absent.
- [ ] `Lazuar.slnx` has no empty folders; Packages grouped correctly.
- [ ] api-spec README matches actual modules.
- [ ] No MediatR registration or Map* method renames left unupdated in Program.cs.
- [ ] Outbox/inbox stored type names unchanged (or migration plan for in-flight messages).

---

## 16. Suggested priority order (effort vs payoff)

| Priority | Work | Payoff | Risk |
|----------|------|--------|------|
| P0 | Phase 0 docs + slnx | Clarity | None |
| P1 | Phase 1 Messaging Workers/EventHandlers | Removes worst inconsistency | Low (folders) |
| P2 | Phase 2 Payments Endpoints folder | Aligns with Commerce style | Low |
| P3 | Phase 2 One/Lhdn endpoint split | Navigability | Low |
| P4 | Phase 7 Contracts folders (CRM/One/Messaging) | Consistency | Low (usings) |
| P5 | Phase 8 Tests absorb Billing/Ops projects | One test story | Low–med (CI paths) |
| P6 | Phase 3 handler naming | Discoverability | Med if careless with type names |
| P7 | Phase 4 Billing Application rebalance | Architectural purity | Higher (ports extraction) |
| P8 | Phase 5 CRM Application | Full 4-layer symmetry | Med (host + tests) |
| P9 | Phase 6 One Domain Aggregates | Taxonomy | Low |
| P10 | Phase 9 BuildingBlocks grouping | Nice-to-have | Low |

---

## 17. Reference paths (absolute)

### Core layout

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/SharedKernel/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Program.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Lazuar.slnx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/`

### Architecture sources

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/001-implementing-new-module.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/006-separation-of-external-and-internal-contracts.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs`

### Packages

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-types-dotnet/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-types-ts/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/lhdn-sdk-dotnet/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/lhdn-sdk-ts/`

### Gold-standard module to copy

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/`

### Highest-friction module to fix first

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/`

---

## 18. Summary judgment

The backend **already has the right physical architecture** (per-module four projects, BuildingBlocks, host composition, architecture tests, external TypeSpec packages). Folder **organization inside projects** has drifted: Commerce/Communications show the intended end-state; Messaging and Billing show the extremes of scatter vs layer inversion; CRM is a deliberate small exception; tests and solution packaging lag the module story.

A reorganization that prioritizes **Workers/Endpoints/EventHandlers consistency** and **test project unification** will deliver most of the cognitive benefit **without** risky DI or outbox type renames. Deeper purity (Billing handlers into Application, CRM Application project) should be scheduled separately with port extraction and host updates.

**No application code was modified for this analysis.** This document is the maintenance plan input for subsequent implementation phases.
