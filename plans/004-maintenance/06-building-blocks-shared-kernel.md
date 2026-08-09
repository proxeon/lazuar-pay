# 06 — BuildingBlocks & SharedKernel Fatness Analysis

**Status:** Analysis only (no app code changes)  
**Date:** 2026-08-09  
**Repo:** `apps/lazuar-api`  
**Scope:** `BuildingBlocks/{Application,Domain,Infrastructure}`, `SharedKernel`, Observability, LLM helpers, outbox/inbox infrastructure  
**Related docs:**  
- [`apps/lazuar-api/docs/002-shared-kernel-vs-building-blocks.md`](../../apps/lazuar-api/docs/002-shared-kernel-vs-building-blocks.md)  
- [`apps/lazuar-api/docs/001-cross-module-communication.md`](../../apps/lazuar-api/docs/001-cross-module-communication.md)  
- Architecture gates: `tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs` (C.9)

---

## 1. Executive summary

`BuildingBlocks` is no longer a thin technical core. It is a **kitchen-sink shared assembly trio** that mixes:

| Layer | Stated intent (docs/002) | Actual state |
|---|---|---|
| **Domain** | Pure structural DDD primitives | Mostly lean; one multi-tenant marker that violates the “no business keyword” rule by name |
| **Application** | CQRS + messaging ports | CQRS + ports **plus** brand email HTML, Markdig, OpenAI-typed LLM ports, agent tooling metadata, and product metrics (`dunning`, webhook) |
| **Infrastructure** | Concrete technical adapters | Persistence + outbox/inbox **plus** email provider, R2 storage, JWT/magic-link, full LLM client stack, ASP.NET exception handler, **cross-schema SQL that names every module and LHDN tables**, and a global worker options bag that enumerates module jobs |
| **SharedKernel** | Cross-cutting domain-agnostic types | **Hollow**: one marker type; every module Domain still ProjectReferences it |

**Fatness is concentrated in `BuildingBlocks.Infrastructure` and secondarily in `BuildingBlocks.Application`.**  
`SharedKernel` is not fat — it is empty capacity that modules depend on without using.

There is **no compile-time circular dependency** between modules and BuildingBlocks today (architecture tests enforce BB ↛ Modules.*). The real risks are:

1. **Conceptual coupling** (BB knows module schema names, LHDN table shape, dunning, subscription magic links, document payload formats).  
2. **Package fan-out** (every Contracts project that references `BuildingBlocks.Application` transitively pulls **MediatR + Markdig + OpenAI**).  
3. **Port misplacement** (`IR2StorageService`, `IJwtService` live in Infrastructure, not Application).  
4. **Composition-root bloat** (`Program.cs` registers a large fraction of BB as global singletons).  
5. **Doc drift** (docs/002 vs code; dead duplicate `Lazuar.Api.Infrastructure.Data.PlatformDbContext`).

---

## 2. Current inventory (file-level)

### 2.1 Project graph (as built)

```
BuildingBlocks.Domain
        ▲
        │
BuildingBlocks.Application  ── Package: MediatR, Markdig, OpenAI
        ▲
        │
BuildingBlocks.Infrastructure ── Package: EF Core, Npgsql, JWT, BCrypt, AWSSDK.S3,
        │                         Dapper (unused?), Hosting, Http, OpenAI, …
        │
SharedKernel ──► BuildingBlocks.Domain only (no types besides marker)

Module.Domain     ──► BuildingBlocks.Domain + SharedKernel
Module.Application──► BuildingBlocks.Application (+ own Domain)
Module.Contracts  ──► BuildingBlocks.Application   ← important fan-out
Module.Infrastructure ──► BuildingBlocks.Infrastructure + module siblings
```

Architecture tests currently enforce:

- BuildingBlocks assemblies must not reference any `Modules.*` namespace.  
- SharedKernel must not reference Modules or BB Application/Infrastructure; no Entity/IAggregateRoot subtypes.  
- Module Domain must not reference BB Application/Infrastructure.

These tests catch **assembly edges**, not **business-concept leakage inside BB**.

### 2.2 BuildingBlocks.Domain (lean — keep)

| Type | Role | Verdict |
|---|---|---|
| `Entity` | Domain event collection + `CheckRule` | Core BB — stay |
| `ValueObject` | Structural equality | Core BB — stay |
| `IAggregateRoot` | Marker | Core BB — stay |
| `IBusinessRule` / `GenericBusinessRule` / `BusinessRuleValidationException` | Invariant pattern | Core BB — stay |
| `IDomainEvent : INotification` | Domain events via MediatR | Stay, but note **MediatR package on Domain** |
| `IMustHaveTenant` (`OrganizationId`) | Multi-tenant stamp/filter marker | Stay functionally; **rename/docs conflict** (see §5) |

**Line count / complexity:** small; no god types.  
**Package:** MediatR only (because `IDomainEvent : INotification`).

### 2.3 BuildingBlocks.Application (moderately fat)

#### 2.3.1 Legitimate application building blocks

| Type | Used by | Verdict |
|---|---|---|
| `ICommand` / `IQuery` / handlers (CQRS.cs) | All modules | Stay |
| `IIntegrationEvent` / `IIntegrationEventHandler` / `IEventBus` / `IEventBusSubscriptions` | Cross-module async | Stay |
| `IExecutionContextAccessor` | PlatformDbContext, handlers, Ops agent | Stay |
| `ISqlConnectionFactory` | Query services across modules | Stay |
| `IPasswordService` (declared inside CQRS.cs) | One, Payments platform admin, genesis | Stay (move to own file) |
| `ISecretVault` + `SecretVaultExtensions` | Payments, Communications BYOK secrets | Stay as **crypto port** |
| `ITokenGeneratorService` / `GeneratedToken` | One API keys, invites, password reset | Stay |
| `PaginatedResponse<T>` | Query contracts | Stay |

#### 2.3.2 Borderline / product-shaped ports still living in Application

| Type | Used by | Fatness issue |
|---|---|---|
| `IEmailService` | **Messaging only** (`DispatchMessageIntegrationEventHandler`); composition in `Program.cs` | Port is platform-wide, but **parameters encode Resend BYOK + org tagging + List-Unsubscribe** — product rules, not generic email |
| `IMessagingService` | **Messaging only** (WhatsApp/SMS console stub) | Should be Messaging module port; BB holds a console impl forever |
| `IMagicLinkTokenService` | Commerce + Communications | API is `GenerateToken(Guid subscriptionId)` — **Commerce subscription concept** |
| `EmailTemplateBuilder` | Messaging dispatch | Brand HTML (“Powered by Lazuar”) — **product presentation**, not technical BB |
| `MarkdownParser` | Communications + One notification handlers | Content-rendering utility; pulls **Markdig into every Contracts consumer** |
| `AgentToolAttribute` | Ops `ToolRegistry` discovery | Agent/Ops feature metadata in shared Application |
| `Llm/IChatClientFactory` | Ops `LlmOrchestratorService` | Returns **`OpenAI.Chat.ChatClient`** → OpenAI package on Application |
| `Llm/ILlmTitleGenerator` | Ops only | Ops conversation title UX |
| `Llm/IAgentPromptProvider` | Ops + Billing prompt provider | Agent extension point — fine as shared **if** LLM stays shared; otherwise Ops contract |
| `Observability/LazuarMetrics` | BB outbox applier + One webhooks + Commerce dunning + Payments webhooks | **Business counters** hard-coded: dead letters (OK), webhook failed, **dunning cancels** |

### 2.4 BuildingBlocks.Infrastructure (fat — primary problem)

Rough grouping of **~40 public types** in one assembly:

#### A. Persistence / EF (core — stay)

- `PlatformDbContext` — tenant query filter, OrganizationId stamp, empty-org write guard, recursive domain-event dispatch via MediatR, `DatabaseJobTrigger` poke on save  
- `NpgsqlConnectionFactory`  
- (Modules each subclass `PlatformDbContext` with private schema)

#### B. Outbox / inbox / in-process bus (core — stay)

- `OutboxMessage`, `InboxMessage`  
- `OutboxEventBus<TDbContext>`  
- `OutboxPublisherJob<TDbContext>`, `InboxConsumerJob<TDbContext>`  
- `InMemoryEventBus` (implements both `IEventBus` and `IEventBusSubscriptions`; reflection `HandleAsync`)  
- `MessageProcessingStatus`, `MessageRetryPolicy`, `MessageProcessingResultApplier`, `IMessageProcessingState`  
- `DatabaseJobTrigger`  
- `TypeResolver`

All nine modules host thin `*OutboxPublisherJob` / `*InboxConsumerJob` subclasses. This is the **correct** shared technical spine for modular monolith messaging (docs/001 hybrid model).

#### C. Security adapters (core-ish — stay, but tidy ports)

- `PasswordService`  
- `JwtService` + **`IJwtService` defined in Infrastructure**  
- `AesSecretVault`  
- `TokenGeneratorService`  
- `MagicLinkTokenService` (subscription-shaped)  

#### D. Delivery adapters (module candidates)

- `ResendEmailService` + `ConsoleEmailService` + `Configuration/ResendOptions`  
- `ConsoleMessagingService`  

#### E. Object storage (multi-module shared port, wrong layer)

- `IR2StorageService` **defined in Infrastructure**  
- `R2StorageService`, `DisabledR2StorageService`  
- Consumers: **Billing** (PDF store), **One** (uploads)  

#### F. LLM stack (Ops-centric)

- `Llm/ChatClientFactory` (OpenAI / OpenRouter / DeepSeek / MiMo endpoints + policies)  
- `Llm/OpenRouterHeaderPolicy`, `ProviderQuirksPolicy`  
- `Llm/LlmTitleGenerator`  
- `Llm/LlmDependencyInjection.AddThinLlmFactory`  

Orchestration complexity correctly lives in `Modules.Ops.Infrastructure.Services.LlmOrchestratorService` (large, module-owned). BB holds the **provider client factory** and title helper.

#### G. Observability (platform-wide, but module-aware SQL)

- `Observability/LazuarMetricsGauges`  
- `Observability/PlatformMetricsCollector` — **hardcoded** `ModuleSchemas = one, messaging, payments, crm, ops, billing, lhdn, commerce, communications` and **SQL against `lhdn."TaxDocuments"`**  
- `Observability/PlatformMetricsRefreshJob`  
- `Observability/PlatformMetricsSnapshot`, `SchemaOutboxMetrics`  
- `Observability/HealthReadiness`  
- `Observability/ObservabilityOptions` (`LhdnStuckThreshold`, outbox lag ready threshold)  
- `IPlatformMetricsCollector`  

#### H. Host / ASP.NET concerns

- `GlobalExceptionHandler` (`IExceptionHandler`) — maps `BusinessRuleValidationException` / `InvalidOperationException` → 400  

#### I. Cross-module product helpers

- `DocumentLinkSigner` — generic HMAC helpers **plus** `FinalDocumentPayload(tenantSlug, ledgerEntryId, …)` and `DraftDocumentPayload(tenantSlug, sessionId, …)` used by Billing + Commerce + Communications  

#### J. Configuration bag that names module jobs

`Configuration/BackgroundWorkerOptions`:

- `OutboundWebhookInterval` → One  
- `BroadcastFanoutInterval` → Communications  
- `LhdnSubmissionInterval` / `LhdnStatusPollingInterval` → Lhdn  
- `BillingEngineInterval` / `DunningEngineInterval` → Commerce  
- `ClaimLeaseDuration` → Lhdn/webhook claim pattern  

This is a **global options DTO for multiple modules**, registered once in `Program.cs`. Convenient, but it makes BuildingBlocks.Infrastructure a registry of every background product concern.

### 2.5 SharedKernel (empty)

```
SharedKernel/
  SharedKernelMarker.cs   // assembly anchor only
  SharedKernel.csproj     // refs BuildingBlocks.Domain
```

Every module Domain `.csproj` references SharedKernel. No value objects, IDs, or shared enums live here. Architecture tests treat it as a first-class boundary that is currently a **placeholder**.

### 2.6 Dead / parallel code outside BuildingBlocks

| Location | Note |
|---|---|
| `src/Lazuar.Api/Infrastructure/Data/PlatformDbContext.cs` | **Older, incomplete** abstract DbContext (reflection OrganizationId stamp, no domain events, no job trigger, no `IMustHaveTenant` filter). Modules inherit **BuildingBlocks** `PlatformDbContext`, not this one. Likely dead weight. |
| Docs/002 diagram | Shows SharedKernel under BuildingBlocks as a second tier of types; code has no such types. |

---

## 3. Consumption map (who depends on what)

### 3.1 Universally consumed (true building blocks)

| Concern | Consumers |
|---|---|
| Domain primitives (`Entity`, rules, VO) | All module Domains |
| CQRS interfaces | All Applications / many Contracts |
| `IEventBus` / outbox jobs / `PlatformDbContext` | All module Infrastructures |
| `ISqlConnectionFactory` / `NpgsqlConnectionFactory` | One, Billing, Commerce, Lhdn, Communications, Ops, Messaging (+ middleware) |
| `IExecutionContextAccessor` | Host + most Infrastructures |
| `IPasswordService` / `IJwtService` | One auth, platform admin, genesis bootstrap |
| `ISecretVault` | Payments, Communications |
| `ITokenGeneratorService` | One (API keys, invites, verify/forgot password) |
| `DatabaseJobTrigger` | Host singleton + all module DbContexts/jobs |
| `InMemoryEventBus` | Host + all outbox publishers |

### 3.2 Narrowly consumed (should not fatten BB forever)

| Concern | Primary consumers | Secondary |
|---|---|---|
| `IEmailService` / Resend / EmailTemplateBuilder | Messaging | — |
| `IMessagingService` | Messaging | — |
| `MarkdownParser` | Communications, One notification domain-event handlers | — |
| `IMagicLinkTokenService` | Commerce public portal, Communications fulfillment | — |
| `DocumentLinkSigner` | Billing endpoints, Commerce public, Communications document emails | — |
| `IR2StorageService` | Billing document store, One uploads | — |
| LLM factory / title / agent prompt | **Ops** orchestrator; **Billing** registers one `IAgentPromptProvider` | — |
| `AgentToolAttribute` | Ops ToolRegistry scans all loaded assemblies | Agent queries in modules (e.g. Billing agent query) |
| `LazuarMetrics.RecordDunningCancel` | Commerce `DunningEngineJob` | — |
| `LazuarMetrics.RecordWebhookFailed` | One outbound webhooks, Payments webhook handler | — |
| Platform metrics LHDN stuck | Host `/health/*` only via collector | — |
| `BackgroundWorkerOptions` fields | One, Communications, Lhdn, Commerce workers | — |

### 3.3 SharedKernel

| Concern | Consumers |
|---|---|
| Marker only | Architecture tests + Domain ProjectReferences |

---

## 4. Investigation answers

### 4.1 What belongs in BuildingBlocks vs should move to a module

#### Keep in BuildingBlocks (technical core)

1. **Domain structural patterns** — Entity, ValueObject, IAggregateRoot, IBusinessRule, exceptions, IDomainEvent.  
2. **CQRS abstractions** — ICommand/IQuery/handlers (optionally keep thin MediatR dependency).  
3. **Integration messaging contracts** — IIntegrationEvent, IEventBus, IEventBusSubscriptions, IIntegrationEventHandler.  
4. **Outbox/inbox infrastructure** — message entities, jobs, retry/dead-letter applier, TypeResolver, DatabaseJobTrigger, OutboxEventBus, InMemoryEventBus.  
5. **PlatformDbContext** — multi-tenant filter + domain event dispatch + job trigger (this is the modular monolith’s persistence spine).  
6. **SQL connection port** — ISqlConnectionFactory + Npgsql factory.  
7. **Execution context port** — IExecutionContextAccessor (implementation stays in host).  
8. **Generic security primitives** — IPasswordService, ISecretVault (+ AES impl), ITokenGeneratorService, JWT *generation* helper (port should live in Application).  
9. **Generic observability counters that are messaging-technical** — e.g. dead-letter counter tied to MessageProcessingResultApplier.  
10. **GlobalExceptionHandler** *or* move to host project — either is fine; it is host-facing, not module-facing.

#### Move (or demote) out of BuildingBlocks

| Item | Recommended home | Why |
|---|---|---|
| `IEmailService`, Resend/Console, ResendOptions, EmailTemplateBuilder | **Messaging** (primary dispatcher) or Communications | Only Messaging sends; BYOK/org tags are product rules owned by Communications config |
| `IMessagingService`, ConsoleMessagingService | **Messaging** | Already the module name for outbound channels |
| `MarkdownParser` | **Communications** (shared helper inside module) or a tiny `BuildingBlocks.Content` if One also needs it long-term | Markdig dependency should not ride on every Contracts project |
| `IMagicLinkTokenService` / MagicLinkTokenService | **Commerce** (subscription portal) | API is subscription-id shaped; Communications can depend on Commerce.Contracts if needed, or Messaging event carries pre-built URL |
| `DocumentLinkSigner` payload helpers (`FinalDocumentPayload`, `DraftDocumentPayload`) | **Billing** / **Commerce** respectively | Generic `Sign`/`TryValidate` can stay in BB Security; payload conventions are module protocols |
| Full LLM stack (`IChatClientFactory`, factory policies, title generator, DI extension) | **Ops** Infrastructure (+ Application ports) | Sole runtime orchestrator is Ops; Billing only supplies prompt text |
| `IAgentPromptProvider` | **Ops.Contracts** or Ops.Application | Cross-module *extension* of Ops agent; Billing implements via Contracts reference pattern |
| `AgentToolAttribute` | **Ops.Application** (or Ops.Contracts) | Tool discovery is Ops product feature |
| `LazuarMetrics.RecordDunningCancel` | **Commerce** metrics or generic `RecordCounter(name, tags)` | Product metric |
| LHDN stuck query inside `PlatformMetricsCollector` | **Lhdn** contribution / pluggable collectors | Crosses module private schema knowledge into BB |
| `BackgroundWorkerOptions` module-specific intervals | Per-module `IOptions<T>` | BB should not be the catalog of all workers |
| Brand-specific “Powered by Lazuar” HTML | Messaging/Communications templates | Branding is product |

#### Grey area — stay shared *if* multiple modules need a stable port

| Item | Recommendation |
|---|---|
| **R2 / object storage** | Keep a **thin port** shared (Application: `IObjectStorage` / `IR2StorageService`) because Billing + One both need it. Move interface to Application; keep AWS impl in Infrastructure or a `BuildingBlocks.Storage` package. Do **not** invent a Storage module unless lifecycle/billing of blobs becomes a product. |
| **Email port** | Prefer Messaging-owned. If other modules must send email *without* going through Messaging events, keep a thin `IEmailService` in Application — but force all product traffic through Messaging integration events (current design intent). |
| **Platform metrics aggregator** | Host composition root can own cross-schema SQL, *or* BB Observability stays but becomes **plugin-based** (`ISchemaMetricsSource` registered per module) so BB does not hardcode schema names / LHDN table columns. |
| **Outbox lag gauges** | Shared technical observability — stay, but schema list should come from registration, not a constant array inside BB. |

### 4.2 God services in Infrastructure

There is no single 2k-line god class, but several types hold **disproportionate cross-cutting knowledge**:

#### (1) `PlatformMetricsCollector` — highest severity “god”

- Knows **all nine module schema names**.  
- Issues raw SQL to every `"{schema}"."OutboxMessages"` / `InboxMessages`.  
- Issues product SQL to `lhdn."TaxDocuments"` with status vocabulary `PENDING`/`SUBMITTED`.  
- Merges process counters from `LazuarMetrics` (including dunning).  
- Feeds health readiness + gauges.

**Why it is a problem:** it is the exact anti-pattern docs/002 forbids — BuildingBlocks is not domain-blind; it encodes module inventory and LHDN domain state. Adding a module requires editing BB.

**Idiomatic fix:**  
- Each module registers an `IOutboxMetricsSource` (schema name + optional extra gauges).  
- Host or BB aggregator loops registered sources.  
- LHDN stuck count is an Lhdn-provided `IPlatformHealthContributor`.

#### (2) `PlatformDbContext` — large but legitimate “framework base”

- Tenant filter, stamp, write guard, domain-event cascade, job trigger.  
- This is **intentional centralization** for modular monolith consistency.  
- Not a god *service*; it is a base class. Risk is **overloading it** with more product concerns (audit ActorId reflection already appears in the dead Lazuar.Api copy — do not reintroduce into BB without design).

#### (3) `InMemoryEventBus` — dual-role dispatcher

- Subscription registry + publish with reflection.  
- OutboxPublisherJob depends on **concrete** `InMemoryEventBus`, not `IEventBus` — intentional (outbox must not re-enter outbox). Document this; consider a dedicated `IIntegrationEventDispatcher` interface in Application to avoid concrete type coupling from jobs.

#### (4) `ResendEmailService` — policy-heavy adapter

- Hard-codes system tenant GUID `00000000-…0001`.  
- Enforces “no platform fallback for tenant emails” product rule.  
- Tags Resend messages with org for bounce webhooks.

These rules belong with Messaging/Communications product ownership, not BB forever.

#### (5) `ChatClientFactory` + provider policies — multi-provider god factory

- Acceptable as a focused adapter if LLM stays shared; oversized for BB if only Ops uses it.  
- Provider quirks (OpenRouter reasoning, MiMo max_tokens rename) are pure infrastructure — fine technically, wrong *package home* given single consumer.

#### (6) `BackgroundWorkerOptions` — configuration god bag

- Not a service, but a **god options type** that couples BB.Infrastructure.Configuration to every module’s worker cadence.  
- Splitting per module removes BB churn when adding workers.

#### (7) Module-side god (for context, not BB)

- `LlmOrchestratorService` (Ops) is large and complex but **correctly modularized**. Do not pull it into BB.  
- `DispatchMessageIntegrationEventHandler` (Messaging) is the real email/SMS orchestration god — correctly in Messaging, but it depends on BB email/messaging ports.

### 4.3 Should LLM, metrics, email, R2 storage stay shared?

| Capability | Stay shared? | Nuanced recommendation |
|---|---|---|
| **LLM** | **No (as BB fat); Yes (as Ops-owned platform capability)** | Move factory + title generator into Ops.Infrastructure. Keep only a *optional* shared package if a second module starts calling models directly (unlikely — agent tools already funnel through Ops). Billing’s `IAgentPromptProvider` becomes an Ops.Contracts extension point. **OpenAI package leaves BuildingBlocks.Application.** |
| **Metrics** | **Partially** | Technical: dead-letter + outbox lag gauges stay shared. Product: dunning cancel, webhook failed can stay as thin named counters *or* move to module meters with a shared meter name convention (`Lazuar.Hub`). LHDN stuck must not live as hardcoded SQL in BB. Prefer **plugin collectors**. |
| **Email** | **No as BB product surface** | Runtime registration can remain host-level, but **ownership** should be Messaging (+ Communications for BYOK config). Today Messaging is the only sender; BB Resend adapter is convenience that freezes product rules in the wrong project. |
| **R2 storage** | **Yes, thin shared port** | Two modules need object storage. Keep shared interface + S3/R2 impl. Move `IR2StorageService` to Application (or `BuildingBlocks.Application.Storage`). Avoid a full Storage module unless you need multi-tenant quotas, virus scan pipelines, etc. |

### 4.4 Circular risk / layering violations

#### Confirmed safe (no cycles)

```
Modules.* ──► BuildingBlocks.*
SharedKernel ──► BuildingBlocks.Domain
BuildingBlocks.Infrastructure ──► BuildingBlocks.Application ──► BuildingBlocks.Domain
BuildingBlocks ↛ Modules.*   (enforced by NetArchTest)
```

#### Soft layering / package violations (not compile cycles)

1. **Contracts → BuildingBlocks.Application package fan-out**  
   Almost every `Modules.*.Contracts.csproj` references Application. That assembly references **Markdig + OpenAI**. Contract DTOs/events rarely need either. Result: every module’s public surface assembly is heavier and more change-coupled than necessary.

2. **Domain → MediatR**  
   `IDomainEvent : INotification` couples pure domain to MediatR. Common tradeoff; alternative is a marker interface in Domain and a mapping in Infrastructure. Not urgent, but it is a purity leak.

3. **Application → OpenAI concrete types**  
   `IChatClientFactory.CreateClient(...)` returns `OpenAI.Chat.ChatClient`. Ports should not leak vendor types. Application layers of non-Ops modules should never see OpenAI.

4. **Infrastructure-defined ports**  
   - `IR2StorageService` in Infrastructure  
   - `IJwtService` in Infrastructure  
   Application/handlers that need them either depend downward or re-declare. Move ports to Application.

5. **OutboxPublisherJob → concrete InMemoryEventBus**  
   Prevents accidental outbox recursion, but couples job base class to a concrete dispatcher. Prefer `IIntegrationEventDispatcher` registered to the same singleton instance.

6. **Conceptual “cycles” via SharedKernel emptiness**  
   Modules Domain → SharedKernel → BB.Domain, while also Domain → BB.Domain directly. Redundant path; if SharedKernel later gains types that reference module concepts, pressure for cycles increases. Keep SharedKernel free of entities (already tested).

7. **PlatformMetricsCollector reverse knowledge**  
   BB does not *reference* Modules assemblies, but it *names* their schemas and LHDN tables. This is a **logical layering violation** of docs/002 “domain-blind” rule. Future microservice extraction would drag this collector along or force a rewrite.

8. **IMustHaveTenant vs docs/002**  
   Docs say BB must never mention “Tenant”. Code has `IMustHaveTenant` and comments about multi-tenancy throughout PlatformDbContext. Multi-tenancy *is* platform technical policy here — the doc is overly absolute. Prefer updating the doc to allow **platform multi-tenancy markers** while forbidding **business aggregates** (User, Subscription, TaxDocument, etc.).

9. **Duplicate PlatformDbContext**  
   Dead host copy can confuse contributors into depending on the wrong base → divergent tenant behavior (already diverged: no query filters on the host copy).

10. **Magic link / document signer product shapes in BB**  
    Not a cycle, but creates reverse dependency pressure: Commerce/Billing protocol changes require BB edits.

### 4.5 Idiomatic split of BuildingBlocks.Infrastructure

Prefer **incremental extraction** (folders → projects) over a big-bang rewrite. Two viable end states:

#### Option A — Multiple projects under `BuildingBlocks/` (recommended medium-term)

```
BuildingBlocks/
  Domain/                         # as today (lean)
  Application/                    # CQRS, events, execution context, sql, password, secret vault, pagination
  Application.Observability/      # optional: LazuarMetrics technical counters only
  Infrastructure.Persistence/     # PlatformDbContext, NpgsqlConnectionFactory
  Infrastructure.Messaging/       # outbox/inbox, InMemoryEventBus, TypeResolver, DatabaseJobTrigger
  Infrastructure.Security/        # Password, Jwt, AesSecretVault, TokenGenerator, DocumentLinkSigner (generic HMAC only)
  Infrastructure.Hosting/         # GlobalExceptionHandler (or keep in host)
  # Optional feature packages (only if still multi-module after moves):
  Infrastructure.Storage/         # R2
  Infrastructure.Email/           # only if not moved to Messaging
  Infrastructure.Llm/             # only if not moved to Ops
  Infrastructure.Observability/   # gauges + aggregator (plugin-based)
```

**Reference rules:**

- Module.Infrastructure may reference only the BB Infrastructure slices it needs (e.g. Commerce needs Persistence+Messaging+Security, not Llm/Storage necessarily — though single meta-package is OK short-term).  
- Module.Application references Application only.  
- Module.Contracts should ideally reference a **minimal** `BuildingBlocks.Application.Abstractions` (CQRS + IIntegrationEvent only) — see Option C.

#### Option B — Folders only inside one Infrastructure assembly (low-churn first step)

```
BuildingBlocks/Infrastructure/
  Persistence/
  Messaging/
  Security/
  Storage/
  Email/
  Llm/
  Observability/
  Hosting/
  Configuration/   # shrink over time
```

Same mental model, zero csproj churn. Use architecture tests later to forbid Modules from depending on disallowed namespaces if needed (`NetArchTest` namespace rules).

#### Option C — Split Application for Contracts hygiene (high leverage)

```
BuildingBlocks.Application.Abstractions  # ICommand, IQuery, IIntegrationEvent, IEventBus, PaginatedResponse
BuildingBlocks.Application               # remaining ports used by module Application layers
```

Contracts projects reference **Abstractions only** → drops Markdig/OpenAI from contract fan-out even before moving Markdown/LLM.

#### Registration story after split

Today `Program.cs` is the composition root for:

- Observability collector + refresh job  
- Resend HttpClient + IEmailService  
- IMessagingService console  
- Password/Jwt/SecretVault/MagicLink  
- AddThinLlmFactory  
- InMemoryEventBus singleton  
- R2 optional wiring  
- DatabaseJobTrigger  
- GlobalExceptionHandler  

Idiomatic future: each BB slice exposes `AddXxx(IServiceCollection)` extension methods; modules expose `Add{Module}Module`; host only composes. Avoid a second god `BuildingBlocksDependencyInjection` that re-hides all fat.

---

## 5. Doc vs code contradictions to resolve (docs only / later PR)

| Docs/002 claim | Code reality |
|---|---|
| BB never mentions Tenant/Client/Organization/Subscription | `IMustHaveTenant`, OrganizationId filters, MagicLink `subscriptionId`, DocumentLink `ledgerEntryId`, system org GUID in Resend |
| SharedKernel holds global IDs / pure domain-agnostic value types | Only `SharedKernelMarker` |
| BuildingBlocks is “completely domain-blind” | PlatformMetricsCollector knows LHDN TaxDocuments + all module schemas; LazuarMetrics knows dunning |
| PlatformDbContext listed as multi-tenancy + event dispatch | Accurate for BB; host has a stale second class |

**Recommendation:** rewrite docs/002 to:

1. Allow **platform multi-tenancy and messaging infrastructure** in BB.  
2. Forbid **module business vocabulary and private schema SQL** in BB.  
3. State SharedKernel is for **shared value objects/IDs when they exist**; until then marker-only is intentional.  
4. List explicit allowed BB Infrastructure folders (Persistence, Messaging, Security, …).

---

## 6. Fatness scoring (heuristic)

| Assembly | Types (approx public) | Packages | Coupling to modules | Fat score |
|---|---|---|---|---|
| BuildingBlocks.Domain | ~8 | MediatR | Low (IMustHaveTenant name) | **Low** |
| BuildingBlocks.Application | ~20+ | MediatR, Markdig, OpenAI | Medium (metrics, email shape, LLM, agent) | **Medium-High** |
| BuildingBlocks.Infrastructure | ~40+ | EF, Npgsql, JWT, BCrypt, S3, Hosting, OpenAI, … | High (schemas list, LHDN SQL, worker options, document payloads, magic link) | **High** |
| SharedKernel | 1 | — | None (unused) | **Empty** |

“Fat” here means **responsibility sprawl and wrong-owner knowledge**, not only LOC. Infrastructure is not huge by enterprise standards, but every new concern currently defaults to BB because it is the only shared drawer.

---

## 7. Risk if left unchanged

1. **Module onboarding cost** — new module must be hand-edited into `PlatformMetricsCollector.ModuleSchemas` and possibly `BackgroundWorkerOptions`.  
2. **Contract assembly bloat** — OpenAI/Markdig version bumps and security advisories touch the entire solution surface.  
3. **False sense of SharedKernel** — contributors may dump domain VOs into SharedKernel “because the project exists,” or never use it and invent parallel shared folders.  
4. **Microservice extraction friction** — Messaging cannot own email without cutting BB; Ops cannot own LLM without cutting BB; metrics collector assumes single DB with all schemas.  
5. **Test and ownership ambiguity** — BB tests cover retry/vault; product rules in Resend/MagicLink/Document payloads lack clear module owners.  
6. **Inconsistent port placement** — some ports in Application (`IEmailService`), some in Infrastructure (`IR2StorageService`, `IJwtService`).

---

## 8. Recommended remediation roadmap (analysis → future work)

No code is changed in this plan document. Suggested sequencing for a later maintenance implementation:

### Phase M0 — Clarify boundaries (docs + tests only)

1. Update `docs/002-shared-kernel-vs-building-blocks.md` with the refined rules in §5.  
2. Extend architecture tests optionally:  
   - Forbid `OpenAI` / `Markdig` references from `Modules.*.Contracts` once Abstractions split lands.  
   - Optionally assert SharedKernel type count stays non-entity (already present).  
3. Delete or quarantine dead `Lazuar.Api.Infrastructure.Data.PlatformDbContext` in a small cleanup PR (verify no references first).

### Phase M1 — Stop the bleeding (low risk, high leverage)

1. Move `IR2StorageService` and `IJwtService` to Application (port hygiene).  
2. Split `IPasswordService` out of `CQRS.cs`.  
3. Introduce folder structure under Infrastructure (Option B) without new projects.  
4. Extract `BackgroundWorkerOptions` fields into module-local options types; leave BB without Lhdn/Commerce/Broadcast knowledge.  
5. Replace `PlatformMetricsCollector.ModuleSchemas` constant with DI-registered schema list (modules register their schema name when adding outbox). Keep collector in BB temporarily.

### Phase M2 — Move product-shaped features to modules

1. **Email + IMessagingService + EmailTemplateBuilder → Messaging** (impl + registration). Host wires Messaging module only.  
2. **MarkdownParser → Communications** (One handlers depend on Communications.Contracts helper or duplicate thin wrapper / integration event already carries HTML).  
3. **MagicLinkTokenService → Commerce**.  
4. **Document payload helpers → Billing/Commerce**; keep generic HMAC in BB Security.  
5. **LLM stack + AgentToolAttribute + IAgentPromptProvider → Ops** (+ Ops.Contracts for prompt provider). Billing implements Ops contract.  
6. Remove OpenAI and Markdig from BuildingBlocks.Application when nothing remains.

### Phase M3 — Observability plugins

1. `IPlatformMetricsContributor` with default outbox/inbox contributor per module registration.  
2. Lhdn contributes stuck TaxDocuments metric.  
3. Commerce/One/Payments own product counters or use shared meter with module tags.  
4. Health readiness stays host-owned using aggregated snapshot.

### Phase M4 — Optional project splits (Option A/C)

1. `BuildingBlocks.Application.Abstractions` for Contracts.  
2. `Infrastructure.Persistence` + `Infrastructure.Messaging` + `Infrastructure.Security` projects if compile times / ownership demand it.  
3. Populate SharedKernel only when a **real** shared VO appears (e.g. `OrganizationId` strong type) — do not force-fill.

### Explicit non-goals

- Do not create empty modules for Storage/Email/LLM “for purity.”  
- Do not move outbox/inbox out of BB — it is the modular monolith backbone.  
- Do not put write-model entities in SharedKernel.  
- Do not merge SharedKernel into Domain; keep the assembly as a future pressure valve and architecture test anchor.

---

## 9. Decision matrix (quick reference)

| Component | Stay BB | Move module | Host-only | Notes |
|---|---|---|---|---|
| Entity/VO/Rules/IDomainEvent | ✅ | | | |
| IMustHaveTenant | ✅ | | | Rename optional; docs allow platform tenancy |
| CQRS + IIntegrationEvent + IEventBus | ✅ | | | |
| Outbox/Inbox jobs + retry | ✅ | | | |
| PlatformDbContext | ✅ | | | |
| ISqlConnectionFactory | ✅ | | | |
| IExecutionContextAccessor | ✅ | | | Impl in host |
| IPasswordService / ISecretVault / ITokenGenerator | ✅ | | | |
| IJwtService port | ✅ App | | Impl BB/host | Move interface to Application |
| IR2StorageService | ✅ thin port | | | Move interface to Application; keep R2 impl shared |
| IEmailService + Resend | | ✅ Messaging | | |
| IMessagingService | | ✅ Messaging | | |
| EmailTemplateBuilder / brand HTML | | ✅ Messaging | | |
| MarkdownParser | | ✅ Communications | | |
| MagicLinkTokenService | | ✅ Commerce | | |
| DocumentLinkSigner payloads | | ✅ Billing/Commerce | Generic sign stays BB | |
| LLM factory/title/DI | | ✅ Ops | | |
| AgentToolAttribute / IAgentPromptProvider | | ✅ Ops (+ Contracts) | | |
| LazuarMetrics dead-letter | ✅ | | | Technical |
| LazuarMetrics dunning/webhook | | ✅ or tagged | | Product |
| PlatformMetricsCollector schema list | ✅ if pluginized | contributors per module | | |
| LHDN stuck SQL | | ✅ Lhdn contributor | | |
| BackgroundWorkerOptions (as-is) | | ✅ per module | | |
| GlobalExceptionHandler | ✅ or host | | ✅ acceptable | |
| SharedKernel marker | ✅ keep empty | | | Fill only with true shared VOs |
| Dead Lazuar.Api PlatformDbContext | | | delete | Cleanup |

---

## 10. Evidence index (absolute paths)

### BuildingBlocks sources

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Domain/`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Application/`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/`  
- Notable fat nodes:  
  - `.../Infrastructure/Observability/PlatformMetricsCollector.cs`  
  - `.../Infrastructure/Configuration/BackgroundWorkerOptions.cs`  
  - `.../Infrastructure/Llm/*`  
  - `.../Infrastructure/R2StorageService.cs`  
  - `.../Infrastructure/ResendEmailService.cs`  
  - `.../Infrastructure/DocumentLinkSigner.cs`  
  - `.../Application/Observability/LazuarMetrics.cs`  
  - `.../Application/Llm/IChatClientFactory.cs` (OpenAI leak)

### SharedKernel

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/SharedKernel/SharedKernelMarker.cs`

### Composition root

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Program.cs` (BB service registration block ~L90–160, exception handler, health metrics)

### Architecture enforcement

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs` (C.9 tests)

### Design docs

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/docs/002-shared-kernel-vs-building-blocks.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/docs/001-cross-module-communication.md`

### Representative module consumers

- Ops LLM: `.../Modules/Ops/Infrastructure/Services/LlmOrchestratorService.cs`  
- Messaging email: `.../Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs`  
- Billing R2: `.../Modules/Billing/Infrastructure/Commands/GenerateAndStoreDocumentCommandHandler.cs`  
- Billing agent prompt: `.../Modules/Billing/Application/Llm/BillingPromptProvider.cs`  
- Commerce dunning metric: `.../Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs`

### Dead parallel

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Infrastructure/Data/PlatformDbContext.cs`

---

## 11. Bottom line

- **BuildingBlocks.Domain** is healthy.  
- **BuildingBlocks.Application** is the start of fatness: product ports, content utilities, vendor-typed LLM, agent metadata, product metrics.  
- **BuildingBlocks.Infrastructure** is the real dumping ground: correct messaging/persistence spine coexists with email, R2, LLM, host exception handling, document/product helpers, god metrics SQL, and a global worker options catalog.  
- **SharedKernel is not fat — it is vacant**, while still referenced by every Domain project.  
- **Stay shared:** persistence spine, outbox/inbox, CQRS/events, tenancy marker, generic crypto/password/token, thin object-storage port, technical dead-letter metrics.  
- **Do not stay shared as currently shaped:** LLM stack, brand email pipeline, WhatsApp console port, subscription magic links, LHDN-specific health SQL, module worker option bag, dunning product counters.  
- **Circular compile risk is low; conceptual reverse-dependency risk is high.**  
- **Idiomatic split:** Persistence + Messaging + Security (+ optional Storage) as BB infrastructure slices; Email/LLM/product metrics owned by Messaging/Ops/modules; Contracts depend on a thinner Application.Abstractions; SharedKernel remains a strict empty-or-VO-only cell.

This document is analysis-only for maintenance planning. Implementation should follow Phase M0→M4 above in separate PRs with architecture tests updated at each boundary move.
