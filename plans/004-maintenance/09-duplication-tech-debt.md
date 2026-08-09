# 09 — Duplication & Cross-Cutting Tech Debt (lazuar-api)

**Scope:** `apps/lazuar-api/Modules/*` + `apps/lazuar-api/BuildingBlocks/*`  
**Out of scope:** Host `src/Lazuar.Api` middleware (referenced only when it shapes module patterns), tests, frontend apps, TypeSpec packages.  
**Constraint of this analysis:** No app code was modified. Findings and consolidations only.

---

## 0. Executive summary

Lazuar-api already has a solid modular monolith skeleton:

- Per-module `Contracts` / `Application` / `Domain` / `Infrastructure` (with exceptions).
- Shared CQRS markers (`ICommand` / `IQuery`) over MediatR.
- Shared multi-tenant base (`PlatformDbContext` + `IMustHaveTenant`).
- Shared transactional outbox (`OutboxEventBus<T>`, `OutboxPublisherJob<T>`) and opt-in inbox (`InboxConsumerJob<T>`).
- Shared pagination envelope (`PaginatedResponse<T>`).
- Shared global exception mapping for two exception types.

The debt is not “missing architecture.” It is **repeated mechanical glue**, **inconsistent layering of the same ideas**, and **divergent API/error surfaces** that make every new module or endpoint a mini reinvention.

Highest-value consolidations (maintainability without over-abstraction):

| Priority | Theme | Effort | Risk | Payoff |
|---|---|---|---|---|
| P0 | Unify HTTP error response shape + exception taxonomy | M | Low–Med | Every endpoint + client |
| P0 | Standardize `OrganizationId` vs `TenantId` naming | M | Med (contracts) | Cross-module mental model |
| P1 | Extract EF outbox/inbox model configuration + module DI helper | S–M | Low | 9 modules × boilerplate |
| P1 | Collapse thin outbox/inbox job subclasses via factory registration | S | Low | 18 near-empty files |
| P1 | Shared pagination request helper + Ops page/offset fix | S | Low | Consistent list APIs |
| P2 | Shared “entity not found / wrong tenant” helper in handlers | S | Low | Dozens of handlers |
| P2 | Payment gateway shared utilities (name extract, minor units) | S | Low | 4 adapters |
| P2 | Domain background job base loop | S | Low | ~9 workers |
| P3 | Align handler placement (Application vs Infrastructure) | M | Low–Med | Architecture consistency |
| P3 | CRM Application layer (or document intentional thinness) | S–M | Low | Layering story |
| P3 | Webhook signature / delivery alignment (One vs Lhdn) | M | Med | Security + DX |

**Anti-goals (do not do):**

- Do not invent a “generic CRUD command framework.”
- Do not force every query through MediatR when module query services already work.
- Do not put inbox in front of every integration handler (docs already say inbox is opt-in).
- Do not collapse payment gateway adapters into one mega-adapter — vendor differences are real.
- Do not move domain rules into BuildingBlocks (BuildingBlocks is domain-blind by design).

---

## 1. Inventory of what is already shared well

These pieces are **good** and should remain the consolidation surface rather than being reinvented:

| Building block | Path | Role |
|---|---|---|
| CQRS markers | `BuildingBlocks/Application/CQRS.cs` | `ICommand`, `ICommand<T>`, `IQuery<T>`, handler aliases over MediatR |
| Pagination envelope | `BuildingBlocks/Application/PaginatedResponse.cs` | `Data`, `TotalCount`, `CurrentPage`, `TotalPages` |
| Tenant marker | `BuildingBlocks/Domain/IMustHaveTenant.cs` | `OrganizationId` on tenant entities |
| Platform EF base | `BuildingBlocks/Infrastructure/PlatformDbContext.cs` | global filter, stamp, domain-event dispatch, job trigger |
| Outbox write bus | `BuildingBlocks/Infrastructure/OutboxEventBus.cs` | keyed per-module event bus |
| Outbox/inbox workers | `OutboxPublisherJob.cs`, `InboxConsumerJob.cs` | SKIP LOCKED, retry/dead-letter |
| Retry applier | `MessageProcessingResultApplier.cs` | success/failure/dead-letter transitions |
| In-process bus | `InMemoryEventBus.cs` | subscribe + dispatch handlers by event name |
| Global errors | `GlobalExceptionHandler.cs` | maps `InvalidOperationException` + `BusinessRuleValidationException` → 400 |
| Execution context | `IExecutionContextAccessor` | ambient `TenantId` |

Cross-module communication rules are already documented in `apps/lazuar-api/docs/001-cross-module-communication.md` (outbox required, inbox opt-in). That document is accurate relative to the code.

---

## 2. Module map (as of analysis)

| Module | Layers present | Outbox job | Inbox job | Notes |
|---|---|---|---|---|
| One | App + Contracts + Domain + Infra | Yes | Yes | Full identity; outbound webhook job |
| Commerce | App + Contracts + Domain + Infra | Yes | Yes | Largest command surface; split endpoint files |
| Billing | App + Contracts + Domain + Infra | Yes | Yes | Handlers mostly under **Infrastructure/Commands** |
| Payments | App + Contracts + Domain + Infra | Yes | Yes | Gateway adapters; typed integration errors |
| Lhdn | App + Contracts + Domain + Infra | Yes | Yes | Gateway adapter + submission/poll jobs |
| Communications | App + Contracts + Domain + Infra | Yes | Yes | Handlers in Application |
| Messaging | App + Contracts + Domain + Infra | Yes | Yes | **Only real inbox writer** for store-and-ack |
| Ops | App + Contracts + Domain + Infra | Yes | Yes | Chat + LLM; pagination uses offset |
| CRM | Contracts + Domain + Infra (**no Application**) | Yes | Yes | Handlers live flat under Infrastructure |

---

## 3. Copy-pasted command handlers / query patterns

### 3.1 Command handler “load → tenant check → mutate → save” clone

Commerce (and similar modules) repeat the same control flow dozens of times:

```csharp
var product = await _repository.GetProductByIdAsync(request.ProductId, ct);
if (product == null || product.OrganizationId != request.OrganizationId)
{
    throw new InvalidOperationException("Product not found.");
}
// mutate...
await _repository.SaveChangesAsync(ct);
```

Same shape for coupons, checkout sessions, subscriptions, dunning campaigns, templates, LHDN documents, etc. Differences are only:

- which repository/DbContext method,
- which property names,
- whether the message is `"X not found."` or a slightly different string,
- whether null-only is checked vs null **or** wrong org (IDOR-safe pattern).

**Evidence samples:**

- `Modules/Commerce/Application/Commands/UpdateProductCommandHandler.cs` — null or wrong org → `"Product not found."`
- `Modules/Commerce/Application/Commands/CouponCommandHandlers.cs` — same for coupons
- `Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs` — same for sessions
- `Modules/Communications/Application/Commands/MessageTemplateCommandHandlers.cs` — null → `InvalidOperationException("Template not found.")` (sometimes without re-checking org if repo already scopes)

**Consolidation (light):**

1. Add a small Application-layer helper in BuildingBlocks **or** a shared internal static in each module — prefer **BuildingBlocks.Application** only if domain-blind:

```csharp
// BuildingBlocks.Application — domain-blind helpers
public static class Ensure
{
    public static T Found<T>(T? entity, string message = "Resource not found.")
        where T : class
        => entity ?? throw new InvalidOperationException(message);

    // When entity has OrganizationId but we don't want BuildingBlocks to know the interface name:
    public static void SameTenant(Guid entityOrgId, Guid requestOrgId, string message = "Resource not found.")
    {
        if (entityOrgId != requestOrgId)
            throw new InvalidOperationException(message);
    }
}
```

Alternatively, a single method on repositories: `GetProductForOrgAsync(orgId, id)` that returns null if wrong tenant (already partly done in Communications repository with `IgnoreQueryFilters` + org filter). Prefer **repository/query methods that bake in org** over endpoint-level double checks.

2. Do **not** create a generic `UpdateEntityCommandHandler<T>` — that over-abstracts.

### 3.2 Handler placement inconsistency (same pattern, different folders)

| Placement | Modules |
|---|---|
| Handlers in **Application** | Commerce, Communications, One (often co-located with command record), Payments (most), Lhdn, Messaging, Ops |
| Handlers in **Infrastructure** | **Billing** (`Infrastructure/Commands/*`), **CRM** (flat under Infrastructure), Payments partial (`UpdatePaymentConfigCommandHandler` in Infra) |
| Command record + handler **same file** | One (`Application/Commands/*.cs`), Lhdn often, Communications `SaveEmailConfigCommand.cs` |
| Command in **Contracts**, handler elsewhere | Billing, Commerce, Payments (preferred for cross-module commands) |

**Why it matters:**

- New contributors cannot answer “where do I put a handler?” without tribal knowledge.
- Architecture tests scan Application + Infrastructure assemblies for MediatR; both work, so nothing fails — debt is human, not runtime.
- Billing handlers depend on `BillingDbContext` directly (Infrastructure-friendly); Commerce prefers `ICommerceRepository` (Application-friendly).

**Consolidation:**

Document a **default rule** and migrate only when touching files:

| Kind | Lives in | Depends on |
|---|---|---|
| Cross-module command DTO | `Contracts` | BuildingBlocks only |
| Module-private command DTO | `Application` or `Contracts` | Prefer Contracts if any other module might send it |
| Handler (pure domain + ports) | `Application` | Repositories/ports interfaces |
| Handler that must use EF, HTTP clients, secrets heavily | `Application` still, with ports — **or** Infrastructure if intentionally thin module (CRM) |

**Billing migration path (when convenient):** introduce `ILedgerRepository`/ports already exist; move handlers from `Infrastructure/Commands` → `Application/Commands` behind ports. No behavior change.

**CRM:** either introduce a thin `Modules.CRM.Application` for handlers + repository interfaces, or document “CRM is intentionally Infrastructure-only until CRM grows.” Prefer thin Application for consistency with Program.cs MediatR scanning (CRM currently only registers Infrastructure assembly — it works).

### 3.3 Query patterns: dual styles

Two coexisting query styles:

1. **Query service interface + Dapper/EF implementation**  
   - `IBillingQueryService` (Contracts), `ICommerceQueryService` (Application — **inconsistent placement**), `ICrmQueryService` (Contracts), `IOneQueryService` (Contracts), `ILhdnQueryService` (Application Ports), `ICommunicationsQueryService` (Contracts).

2. **MediatR `IQuery` / `IQueryHandler`**  
   - Used selectively: Payments config, Billing draft document, One list credentials, Lhdn TIN validation (oddly `ValidateTaxpayerTinCommand` implements **`IQueryHandler`** while named Command).

**Placement inconsistency of query service interfaces:**

| Interface | Assembly |
|---|---|
| `IBillingQueryService` | Contracts |
| `ICommerceQueryService` | Application |
| `ISubscriberQueryService` | Contracts |
| `ICrmQueryService` | Contracts |
| `IOneQueryService` | Contracts |
| `ILhdnQueryService` | Application/Ports |
| `ICommunicationsQueryService` | Contracts |

Cross-module consumers **must** reference Contracts. Putting `ICommerceQueryService` in Application forces other modules to either not use it or take a forbidden Application reference. Today subscribers cross-module API is correctly on Contracts (`ISubscriberQueryService`); main commerce lists live in Application.

**Consolidation:**

- Rule: **any interface used outside the module → Contracts.**  
- Move `ICommerceQueryService` (or a slim read subset) to Contracts when another module needs it; until then, at least document that Application placement means “internal HTTP endpoints only.”
- Prefer query services for list/detail HTTP; reserve MediatR queries for orchestration that needs pipeline behaviors later.

### 3.4 Dapper list query clone (COUNT OVER)

Billing ledger and Commerce transactions share the same SQL shape:

```sql
SELECT ..., (COUNT(*) OVER())::int AS "TotalCount"
FROM schema."Table" t
WHERE t."OrganizationId" = @OrgId
-- optional filters
ORDER BY ... DESC
LIMIT @Limit OFFSET @Offset
```

Plus C# glue:

```csharp
int offset = (page - 1) * limit;
// open connection if needed
// map raw DTO → public DTO
return new PaginatedResponse<T>(dtos, totalCount, page, limit);
```

Also: `if (connection.State != ConnectionState.Open) connection.Open();` appears in almost every Dapper query service method.

**Consolidation (light):**

1. `ISqlConnectionFactory` extension: `CreateOpenConnection()` that always returns Open.
2. Optional helper:

```csharp
public static class Paging
{
    public static (int Page, int Limit, int Offset) Normalize(int? page, int? limit, int defaultLimit = 50, int maxLimit = 100)
    {
        var p = page is null or < 1 ? 1 : page.Value;
        var l = limit is null or < 1 ? defaultLimit : Math.Min(limit.Value, maxLimit);
        return (p, l, (p - 1) * l);
    }
}
```

3. Do **not** generate SQL for all modules — filter shapes differ.

### 3.5 Subscribers pagination done in memory

`CommerceQueryService.Subscribers.cs` loads/filters then:

```csharp
var paginatedData = filteredList.Skip((page - 1) * limit).Take(limit);
```

Unlike transactions/ledger (SQL LIMIT/OFFSET). Not pure “duplication,” but inconsistent pagination **strategy** with O(n) risk as subscriber volume grows.

**Consolidation:** push filters to SQL when that endpoint is next touched; keep `PaginatedResponse` the same.

---

## 4. Repeated validation, tenant resolution, pagination DTOs

### 4.1 Validation: no shared pipeline

There is **no FluentValidation / DataAnnotations pipeline** on commands. Validation is ad hoc:

| Mechanism | Used for |
|---|---|
| `ArgumentException` / `ArgumentException.ThrowIfNullOrWhiteSpace` | Domain aggregate constructors (Commerce Product, Coupon, Communications Broadcast) |
| `BusinessRuleValidationException` + `GenericBusinessRule` | Business rules (credits, templates, email config, UBL) |
| `InvalidOperationException` | Not-found, state machine, config missing, webhook URL invalid |
| `PaymentIntegrationException` + `PaymentErrorCodes` | M2M checkout only (typed codes) |
| Endpoint-local checks | Min top-up RM 50, required headers, empty tenant, signature |

**Problems:**

- `GlobalExceptionHandler` treats **all** `InvalidOperationException` as HTTP 400 “Validation Error” — including genuine programmer errors and not-found cases that should often be 404.
- Payments integration has a superior model (`code` + status) that other modules do not use.
- Domain validators like `WebhookUrlValidator`, `PlatformApiScopes`, `IntegrationCheckoutMetadata` are good **local** NormalizeAndValidate utilities — pattern is fine; duplication is low.

**Consolidation (recommended P0):**

1. Introduce a small, domain-blind exception set in BuildingBlocks (or keep using existing types but map better):

| Exception | Suggested HTTP | When |
|---|---|---|
| `BusinessRuleValidationException` | 400/422 | Broken business rule |
| New `NotFoundException` (optional) | 404 | Missing resource / wrong tenant (IDOR-safe message) |
| `PaymentIntegrationException` (module-local) | its StatusCode | Keep |
| `InvalidOperationException` | 500 (or leave 400 temporarily) | Unexpected state — tighten gradually |
| `ArgumentException` | 400 | Bad input at boundary |

2. Prefer **throwing** and letting `GlobalExceptionHandler` map — stop per-endpoint `try/catch` for the same two exceptions (see §6).

3. Optional later: MediatR pipeline behavior for structural validation only if a real validation library is adopted. Not required now.

### 4.2 Tenant resolution: two names for one concept

| Surface | Name | Location |
|---|---|---|
| Ambient context | `TenantId` | `IExecutionContextAccessor` |
| Persistence / domain | `OrganizationId` | `IMustHaveTenant`, almost all tables |
| Commands (majority) | `OrganizationId` | Billing, Commerce, Lhdn, Communications commands |
| Commands (Messaging sample) | `TenantId` | `SendTenantNotificationCommand` |
| Integration events (majority) | `OrganizationId` | Payments, Commerce, Billing, Lhdn |
| Integration events (One identity-ish) | `TenantId` | `TenantProvisionedIntegrationEvent`, `TenantUpdatedIntegrationEvent`, `AppEntitlementGrantedIntegrationEvent`, `DefaultTemplatesSeededIntegrationEvent` |
| Integration events (also One) | `OrganizationId` | `WorkspaceUpdatedIntegrationEvent`, `ApiKeyRevokedIntegrationEvent` |

This is the single most confusing cross-cutting naming debt.

**Reality in code:**

- Middleware sets `HttpContext.Items["TenantId"]`.
- `PlatformDbContext` stamps and filters on `OrganizationId == ExecutionContext.TenantId`.
- Endpoints pass `ctx.TenantId` into commands as `OrganizationId` (usually).

**Consolidation (P0 naming):**

1. **Canonical persistence name remains `OrganizationId`** (matches `IMustHaveTenant`, migrations, indexes).
2. **Canonical ambient name can stay `TenantId`** on `IExecutionContextAccessor` (HTTP/product language) *or* rename to `OrganizationId` for 1:1 mapping — pick one and document.
3. **Integration events:** migrate new events to `OrganizationId`; for existing `TenantId` events, either:
   - rename with a coordinated consumer update (Messaging, Communications seeders), or
   - add a documented alias property (worse). Prefer rename when those event contracts are versioned only in-process (no external bus yet — lower risk than public webhooks).
4. Avoid introducing a third synonym (`WorkspaceId` is already used as org synonym in product language — keep Workspace as UI name only).

### 4.3 Pagination DTOs and request parameters

**Shared response exists:** `PaginatedResponse<T>` — good.

**Request parameters are not shared and disagree:**

| API | Params | Defaults / clamps |
|---|---|---|
| Billing `/admin/billing/ledger` | `page`, `limit` | page 1, limit 50; **no max clamp** |
| Commerce `/transactions` | `page`, `limit` | page &lt; 1 → 1; limit outside 1..100 → 50 |
| Commerce custom-checkouts | `page`, `limit` | similar |
| Ops `/ops/chat/conversations` | **`limit`, `offset`** | limit default 20; **TotalCount hard-coded to 0** |
| Messaging logs | `.Take(take)` | ad hoc |
| Internal jobs | `PageSize = 100`, `.Take(50)` | constants per job |

Ops builds:

```csharp
return Results.Ok(new PaginatedResponse<OpsConversationDto>(dtos, 0, currentPage, safeLimit));
```

So clients get `TotalPages = 0` always — broken for UI pagination.

**Consolidation (P1):**

1. Document **page + limit** as the platform list contract (matches TypeSpec / most admin UIs).
2. Shared `Paging.Normalize` helper used at endpoint boundary.
3. Fix Ops to either:
   - return real total count, or
   - use a dedicated cursor/limit DTO if infinite scroll — do not fake `PaginatedResponse` with total 0.
4. Optional request record in BuildingBlocks:

```csharp
public record PageRequest(int Page = 1, int Limit = 50);
```

Do not force every internal job to use it.

### 4.4 Tenant empty-check duplication

Endpoints repeatedly:

```csharp
if (ctx.TenantId == Guid.Empty) throw new InvalidOperationException("Active workspace context required.");
// or return ProblemDetails unauthorized
```

Payments IntegrationEndpoints return typed ProblemDetails; Ops throws; others assume middleware always set tenant.

**Consolidation:** middleware should fail closed for org-scoped routes; endpoints that are public/webhook skip. For org-admin groups, prefer a single filter/endpoint convention rather than repeating empty checks. A minimal helper `ctx.RequireTenantId()` reduces noise.

---

## 5. Outbox / inbox job duplication per module

### 5.1 What is already consolidated

Base implementations are excellent and shared:

- `OutboxPublisherJob<TDbContext>` — poll, SKIP LOCKED, deserialize, `InMemoryEventBus.PublishAsync`, retry applier.
- `InboxConsumerJob<TDbContext>` — same for MediatR `INotification`.
- `OutboxEventBus<TDbContext>` — write row only.
- `MessageProcessingResultApplier` — dead-letter + backoff.

### 5.2 What is still duplicated (mechanical)

**A. Thin subclasses (18 types, ~10–15 lines each):**

```
BillingOutboxPublisherJob / BillingInboxConsumerJob
CommerceOutboxPublisherJob / CommerceInboxConsumerJob
Communications* / Crm* / Lhdn* / Messaging* / One* / Ops* / Payments*
```

Every subclass is only:

```csharp
public class XOutboxPublisherJob : OutboxPublisherJob<XDbContext>
{
    public XOutboxPublisherJob(IServiceScopeFactory scopeFactory, ILogger<XOutboxPublisherJob> logger, DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger) { }
}
```

Purpose of subclass today: **typed logger category** + **concrete hosted service type for DI**.

**B. DbContext model config (copy-pasted in every module):**

```csharp
modelBuilder.Entity<OutboxMessage>(builder =>
{
    builder.ToTable("OutboxMessages");
    builder.HasKey(x => x.Id);
    builder.HasIndex(x => new { x.NextAttemptAt, x.OccurredOn }).HasFilter("\"ProcessedAt\" IS NULL");
});
modelBuilder.Entity<InboxMessage>(builder => { /* same */ });
```

Seen in Commerce, Payments, Messaging, Communications, Billing, One, Lhdn, Ops, CRM.

**C. DI registration pattern (every module):**

```csharp
services.AddKeyedScoped<IEventBus, OutboxEventBus<XDbContext>>("XEventBus");
services.AddHostedService<XOutboxPublisherJob>();
services.AddHostedService<XInboxConsumerJob>();
```

**D. Empty inbox consumers:**  
Docs state inbox is opt-in; Messaging is the module that actually **writes** `InboxMessages` from `IIntegrationEventHandler` wrappers. Other modules still host inbox jobs “for symmetry.” That is intentional but still costs process loops and schema surface.

### 5.3 Messaging-only inbox enqueue clone

Three near-identical handlers:

- `TenantProvisionedIntegrationEventHandler`
- `TenantUpdatedIntegrationEventHandler`
- `WorkspaceUpdatedIntegrationEventHandler`

Each:

```csharp
var inboxMessage = new InboxMessage
{
    Id = @event.Id,
    Type = typeof(T).AssemblyQualifiedName ?? typeof(T).FullName!,
    Data = JsonSerializer.Serialize(@event)
};
await _context.InboxMessages.AddAsync(inboxMessage);
await _context.SaveChangesAsync();
```

Then Application `INotificationHandler` does the real work after `InboxConsumerJob` republishes via MediatR.

**Consolidation:**

```csharp
// BuildingBlocks or Messaging infrastructure
public static class InboxEnvelope
{
    public static InboxMessage FromIntegrationEvent(IIntegrationEvent @event) => new()
    {
        Id = @event.Id,
        Type = @event.GetType().AssemblyQualifiedName ?? @event.GetType().FullName!,
        Data = JsonSerializer.Serialize(@event, @event.GetType())
    };
}
```

Or one generic `InboxEnqueuingHandler<TEvent>` registered thrice — only if generic DI stays readable.

### 5.4 Consolidation proposals (outbox/inbox)

**P1a — EF configuration extension (safe, high clarity):**

```csharp
// BuildingBlocks.Infrastructure
public static class OutboxInboxModelBuilderExtensions
{
    public static void ApplyOutboxInbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(...);
        modelBuilder.Entity<InboxMessage>(...);
    }
}
```

Each DbContext: `modelBuilder.ApplyOutboxInbox();`

**P1b — Hosted service registration without 18 files:**

Option 1 (simple): keep thin subclasses (logger category is useful in ops). Accept the boilerplate as “module tax.”

Option 2 (less files): register open generic hosted services if the host supports it:

```csharp
services.AddHostedService<OutboxPublisherJob<CommerceDbContext>>();
```

Requires making `OutboxPublisherJob<T>` non-abstract and injecting `ILogger<OutboxPublisherJob<T>>` (logger name becomes generic — acceptable). Same for inbox.

Option 3 (factory): `services.AddModuleOutboxInbox<TDbContext>("CommerceEventBus")` extension that registers keyed bus + both hosted services.

**Recommend Option 3 + P1a.** Keep abstract jobs OR switch to concrete generics; delete empty subclasses only after metrics/dashboard still identify module (schema name is already in metrics via `PlatformMetricsCollector` module schemas list).

**P1c — Do not remove empty inbox tables/jobs yet** unless ops cost is measured; docs already bless empty consumers. Optionally stop registering inbox hosted services for modules with zero inbox writers (CRM, Ops, Payments, …) after confirming no writes — reduces noise.

**Do not** build a multi-schema single outbox worker (one process scanning all schemas) unless process count becomes a real problem — current design matches modular isolation and SKIP LOCKED per schema.

---

## 6. Similar gateway adapter patterns

### 6.1 Payments gateways (good port, repeated glue)

Port: `Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs`  
Factory: `PaymentGatewayFactory` (resolve by `GatewayType` string).  
Adapters: Stripe, Billplz, Chip Collect, Razorpay.

**Shared well:**

- Result records (`GatewayCheckoutResult`, `GatewayWebhookParsedResult`).
- Factory injection of `IEnumerable<IPaymentGatewayAdapter>`.

**Duplicated glue across Billplz / Chip / Razorpay (and partially Stripe):**

| Concern | Duplication |
|---|---|
| Customer display name from email | private `ExtractName` in Billplz, Chip, Razorpay |
| Amount → minor units | `(int)(amount * quantity * 100)` variants |
| Default product name | `"Lazuar Payment"` string |
| Default placeholder email | `"customer@example.com"` |
| Failure logging | `LogError(... Tenant {TenantId} ...)` + return `GatewayCheckoutResult(false, ...)` |
| Inject `IHttpClientFactory` + `IConfiguration` + logger | constructor pattern |

Stripe uses official SDK (different shape) but still shares metadata stamping (`metadata["tenant_id"] = tenantId`).

**Consolidation (P2, keep adapters separate):**

```csharp
// Modules.Payments.Infrastructure.Gateways/GatewayCommon.cs
internal static class GatewayCommon
{
    public static string ExtractName(string? email) { ... }
    public static int ToMinorUnits(decimal amount, int quantity = 1) =>
        (int)Math.Round(amount * quantity * 100m, 0, MidpointRounding.AwayFromZero);
    public static string DefaultProductName(string? productName, int quantity) => ...;
}
```

Do **not** unify HTTP call paths — Billplz Basic auth vs CHIP Bearer vs Razorpay SDK vs Stripe SDK are inherently different.

### 6.2 LHDN gateway (different domain, same “adapter” word)

`LhdnGatewayAdapter` is large: token cache, per-operation rate limiters, intermediary headers, MyInvois HTTP. It correctly lives behind `ILhdnGatewayAdapter`. Little value in sharing code with payment adapters beyond generic HTTP helpers.

### 6.3 Outbound webhooks (Two implementations, diverging semantics)

| Aspect | One `OutboundWebhookDispatcherJob` | Lhdn `WebhookSenderService` |
|---|---|---|
| Durability | Domain outbox rows + claim lease + retries | Fire-and-forget in-process |
| Signature | `OutboundWebhookSignature` (timestamped header) | HMAC-SHA256 hex of body only |
| Headers | `X-Lazuar-Signature`, `X-Lazuar-Event`, `X-Lazuar-Delivery-Id` | `X-Lazuar-Signature` only |
| Metrics | `LazuarMetrics.RecordWebhookFailed` | log only |
| HTTP client | named `"DeveloperWebhooks"` | default client |

**Consolidation (P3):**

1. Document **one** public webhook signature scheme for developer-facing webhooks (prefer One’s timestamped scheme — replay-resistant).
2. Extract `WebhookHmac` helper used by both.
3. Long-term: LHDN developer webhooks should use durable delivery (same as One) — product decision, not drive-by refactor.
4. Communications Svix-style inbound verification is a third scheme — keep separate (provider-imposed).

### 6.4 Credential verification patterns

`UpdatePaymentConfigCommandHandler` and `SaveEmailConfigCommandHandler` both:

- accept optional new secret,
- decrypt existing via `ISecretVault`,
- call external API to validate,
- encrypt and store,
- throw `BusinessRuleValidationException` on failure.

**Consolidation:** optional small helper `SecretVaultExtensions` already exists; a `ValidateThenSeal` pattern document is enough — do not force a shared “BYOK config command base class.”

---

## 7. Inconsistent error handling

### 7.1 Global handler (narrow)

`BuildingBlocks/Infrastructure/GlobalExceptionHandler.cs`:

- `InvalidOperationException` **or** `BusinessRuleValidationException` → 400 ProblemDetails, Title `"Validation Error"`.
- Everything else → 500 with **exception message in Detail** (information leak risk in production).

Missing:

- 404 mapping
- 401/403 mapping
- 409 conflict
- stable `code` extension (Payments has it locally only)
- hiding internal exception details on 500

### 7.2 Endpoint-level chaos matrix

Endpoints **sometimes** catch and map, **sometimes** rely on global handler, with **different body shapes**:

| Module / area | Catch style | Body shape |
|---|---|---|
| Communications Broadcast | catch `BusinessRuleValidationException` | plain string via `TypedResults.BadRequest(ex.Message)` |
| Lhdn endpoints | catch BusinessRule + InvalidOperation | `ProblemDetails { Status, Detail }` |
| One endpoints | catch InvalidOperation | mix of string, ProblemDetails |
| Commerce public | catch InvalidOperation (message contains `"not found"` → NotFound) | string / BadRequest |
| Commerce admin transactions | catch InvalidOperation | `StatusResponse { Status = ex.Message }` (**abuses Status field for error text**) |
| Billing | catch + BadRequest string | string |
| Payments webhooks | anonymous `{ error = ... }` | JSON object |
| Payments integration | `PaymentIntegrationException` → ProblemDetails + **`code`** | best-in-class for M2M |
| Ops agent write | catch → ProblemDetails | ProblemDetails |
| Many happy paths | no try/catch | global handler |

Also mixed `Results.*` vs `TypedResults.*` and mixed `IResult` vs generic `Results<Ok<T>, BadRequest<T>>` return types (OpenAPI quality varies).

### 7.3 Semantic bugs enabled by inconsistency

1. **Not-found as 400** — handlers throw `InvalidOperationException("Product not found.")` → global 400, not 404. Commerce public sometimes special-cases message substring `"not found"` — fragile.
2. **IDOR masking** — correct security practice (not found vs forbidden) is good; wrong status code confuses clients.
3. **500 Detail leaks** — global handler puts `exception.Message` on 500 responses.
4. **Double mapping** — catch in endpoint returns string; uncaught path returns ProblemDetails — same error class, different clients see different schemas.

### 7.4 Consolidation proposal (P0)

**A. Exception taxonomy (BuildingBlocks):**

```text
BusinessRuleValidationException → 422 (or keep 400)
NotFoundException                 → 404
ConflictException                 → 409  (optional)
PaymentIntegrationException       → module status/code (register in handler)
Unauthorized / forbid             → 401/403 via ASP.NET auth (not exceptions)
Unhandled                         → 500 with generic detail in Production
```

**B. Expand `GlobalExceptionHandler`:**

- Map new types.
- Add `extensions["code"]` when present.
- Stop returning raw exception messages on 500 in non-Development.
- Consider **not** treating all `InvalidOperationException` as validation forever — migrate call sites gradually to NotFound/BusinessRule.

**C. Endpoint policy:**

- Prefer **no try/catch** for domain exceptions once global mapping is complete.
- Keep try/catch only for:
  - translating external SDK failures,
  - webhook signature failures with specific public messages,
  - multi-status batch endpoints.

**D. Align admin error body to ProblemDetails** (RFC 7807 already partially used). Deprecate `StatusResponse.Status = error message` pattern.

**E. Mirror Payments `code` constants** for other public/M2M surfaces when they stabilize (LHDN, integration commerce) — module-local code tables are fine; BuildingBlocks only needs the ProblemDetails writer.

---

## 8. Naming inconsistencies

### 8.1 Tenant / Organization / Workspace

Covered in §4.2. Additional product language:

- Routes: `/admin/...`, tenant slug headers, “workspace” in Ops/One UX.
- Events: `TenantProvisioned*` vs `WorkspaceUpdated*` (both touch org replica in Messaging).

### 8.2 Event handler type naming

| Style | Examples |
|---|---|
| `*IntegrationEventHandler` | Most modules |
| `*Handler` short | Billing `InvoiceIssuedHandler`, `CommissionAccruedHandler`, `PlatformTopUpEventHandler`, `ZeroAmountCheckoutHandler` |
| Multi-event class | `LifecycleEventHandlers`, `SubscriptionLifecycleIntegrationEventHandlers`, `IntegrationCheckoutGatewayEventsHandler` |

Prefer suffix `IntegrationEventHandler` for bus handlers; short names for domain event handlers is OK if folder makes it obvious.

### 8.3 Command vs query naming bugs

- `ValidateTaxpayerTinCommand` handled by `IQueryHandler` — should be `ValidateTaxpayerTinQuery` or stay command if it has side effects (cache). Name should match intent.
- `ProcessZeroAmountCheckoutCommand.cs` filename holds command + handler mixed patterns differently across modules.

### 8.4 Gateway type strings

`"STRIPE"`, `"BILLPLZ"`, `"CHIP"`, `"RAZORPAY"` — magic strings. Acceptable if centralized constants exist; if not, add `PaymentGatewayTypes` static class to avoid drift with DB values.

### 8.5 Auth policy / role names

Endpoints use:

- `.RequireAuthorization("OrgAdmin")`
- `.RequireAuthorization(policy => policy.RequireRole("CLIENT", "ADMIN"))`
- implicit auth from groups

Naming of policies vs roles is host-level but affects every module endpoint file — document a single matrix.

### 8.6 Schema / keyed service names

Keyed services: `"BillingEventBus"`, `"CommerceSqlConnectionFactory"`, etc. Consistent enough. Keep.

### 8.7 Module folder conventions

| Convention | Modules |
|---|---|
| `Infrastructure/Endpoints.cs` monolithic | Billing, One, Ops, Messaging, Payments (partial) |
| `Infrastructure/Endpoints/*.cs` split | Commerce, Communications |
| `Infrastructure/Workers/` | most |
| Jobs next to DI (no Workers folder) | Messaging (`MessagingOutboxPublisherJob.cs` at Infrastructure root) |
| EventHandlers under Infra | most |
| EventHandlers under Application | Messaging partial, Commerce Application has some |

**Consolidation:** prefer `Infrastructure/Endpoints/` when file exceeds ~200 lines; prefer `Infrastructure/Workers/` for all hosted services including Messaging.

### 8.8 Query service / repository naming

- `ILedgerRepository` vs `ICommerceRepository` (god repository) vs fine-grained Payments ports (`ITenantPaymentConfigRepository`, …).
- Commerce repository growth is a maintainability risk (not pure duplication) — split by aggregate when files hurt.

---

## 9. Dependency injection & host wiring duplication

### 9.1 Module DI skeleton (copy-paste)

Every `AddXModule`:

1. Read `ConnectionStrings:Default` (throw if missing — **Payments does not throw**, can null-ref later).
2. `AddDbContext` + migrations history table schema.
3. Often `AddKeyedScoped<ISqlConnectionFactory, NpgsqlConnectionFactory>(...)`.
4. Keyed `IEventBus` → `OutboxEventBus<T>`.
5. Register repositories / query services.
6. `AddTransient` each integration handler.
7. `AddHostedService` workers.
8. `UseXSubscriptions` subscribes handlers on `IEventBusSubscriptions`.

**Inconsistency:** Billing ignores `PendingModelChangesWarning`; others do not. Payments skips null check on connection string.

### 9.2 MediatR assembly registration in host

`Program.cs` manually lists every Application + Infrastructure assembly (CRM Application missing because it does not exist). Adding a module requires 2+ lines here + DI + subscriptions.

**Consolidation (light):** convention-based scan of assemblies named `Modules.*.Application` / `Modules.*.Infrastructure` — only if team accepts reflection at startup. Otherwise a single `ModuleAssemblyCatalog` list is enough to avoid forgetting CRM-style exceptions.

### 9.3 Subscription registration

`AddTransient<Handler>` + `eventBus.Subscribe<TEvent, THandler>()` pairs are easy to desync (register DI but forget Subscribe → silent no-op with log “no registered handlers”).

**Consolidation:** helper:

```csharp
eventBus.SubscribeAndRegister<TEvent, THandler>(services); // or reverse at startup validation
```

Or startup diagnostic that warns if handler type is registered in DI but not in subscription map (architecture test already may cover some — strengthen if not).

---

## 10. Background domain jobs — repeated host loop

Jobs sharing the same skeleton:

- Commerce: `BillingEngineJob`, `DunningEngineJob`, `CheckoutSessionExpiryJob`
- Billing: `B2cConsolidationJob`, `RevenueRecognitionJob` (unregistered)
- Lhdn: `LhdnSubmissionJob`, `LhdnStatusPollingJob`
- One: `OutboundWebhookDispatcherJob`
- Communications: `BroadcastFanoutJob`

Pattern:

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    try { await Process...(stoppingToken); }
    catch (Exception ex) { _logger.LogError(...); }
    await Task.Delay(_options.SomeInterval, stoppingToken);
}
// + internal RunOnceAsync for tests
```

**Consolidation (P2):**

```csharp
public abstract class PollingBackgroundService : BackgroundService
{
    protected abstract TimeSpan Interval { get; }
    protected abstract Task TickAsync(CancellationToken ct);
    // loop + log + RunOnceAsync
}
```

Keep job-specific claim/lease SQL inside subclasses. Outbox/inbox jobs already have their own base — do not merge polling domain jobs with outbox base (different trigger: `DatabaseJobTrigger` vs pure delay).

---

## 11. Cross-cutting secrets, SQL, and multi-tenant filters

### 11.1 `IgnoreQueryFilters` + explicit OrganizationId

Workers and integration handlers correctly bypass global filters (no ambient tenant) and filter by org explicitly. This is repeated but **necessary** — not accidental duplication. A repository base:

```csharp
protected IQueryable<T> ForOrg<T>(Guid orgId) where T : class, IMustHaveTenant
    => Set<T>().IgnoreQueryFilters().Where(x => x.OrganizationId == orgId);
```

on module DbContexts could reduce mistakes (forgetting org filter after IgnoreQueryFilters is a security bug class).

### 11.2 Dapper always filters `OrganizationId = @OrgId`

Good consistency for read models. Keep.

### 11.3 System tenant special cases

`Guid.Empty` / well-known system tenant checks appear in Communications email config and Messaging dispatch. Document the system tenant ID constant once (One module or BuildingBlocks config) — avoid scattering magic GUIDs (middleware already uses `00000000-...-0001` in one path).

---

## 12. Layering debt summary by module

### CRM — intentionally thin or incomplete?

- No Application project.
- Handlers use `CrmDbContext` directly.
- Still full outbox/inbox workers + migrations.
- Fine for size today; inconsistent with architecture docs that describe Application handlers.

### Billing — Infrastructure-heavy handlers

- Commands in Contracts (good for cross-module deduct/hold).
- Handlers in Infrastructure (DbContext).
- Application holds little beyond ledger port + agent prompts.

### Payments — best error model, mixed handler placement

- Integration exceptions are the template for M2M APIs.
- Gateway adapters are the right granularity.
- Config update handler in Infrastructure (HTTP validation) is justified.

### Messaging — unique inbox pattern

- Only module fully using store-and-ack inbox.
- Triple-cloned enqueue handlers.
- Tenant replica is a classic read-model sync — good pattern for other modules if needed.

### Commerce — largest surface, best split endpoints, god repository risk

### One — large Endpoints.cs, co-located command files, outbound webhooks reference implementation

---

## 13. Proposed consolidation roadmap (pragmatic)

### Phase A — Policy & docs only (1–2 days, no behavior change)

1. Write/extend module implementation checklist:
   - Where handlers live
   - OrganizationId vs TenantId
   - Error throwing rules
   - When to use inbox
   - Pagination page/limit
2. Document Payments ProblemDetails `code` as the M2M standard.
3. Document empty inbox consumer policy.

### Phase B — BuildingBlocks small helpers (low risk)

1. `ApplyOutboxInbox()` EF extension.
2. `AddModuleMessaging<TDbContext>(name)` DI extension (keyed bus + hosted jobs).
3. Make outbox/inbox jobs concrete generic hosted services **or** keep subclasses; either way register via extension.
4. `Paging.Normalize` + `CreateOpenConnection()`.
5. `Ensure.Found` / `Ensure.SameTenant` helpers.
6. `InboxEnvelope.FromIntegrationEvent`.
7. `PollingBackgroundService` base for domain jobs.
8. Expand `GlobalExceptionHandler` (NotFound, hide 500 details, optional codes).

### Phase C — Module alignments (touch on edit)

1. Rename/standardize event property `TenantId` → `OrganizationId` on identity events (coordinate Messaging + Communications).
2. Move `ICommerceQueryService` to Contracts if cross-module need appears; else leave.
3. Fix Ops pagination total count or DTO type.
4. Payments `GatewayCommon` utilities.
5. Endpoint cleanup: remove redundant try/catch as global mapping improves; unify BadRequest body to ProblemDetails.
6. Billing handlers → Application when ports are complete.
7. Messaging enqueue handlers → shared helper.
8. Align LHDN webhook signature with One when public docs are updated.

### Phase D — Explicit non-goals

1. No shared “Module bootstrap framework” NuGet beyond a few extensions.
2. No single DbContext.
3. No replacing MediatR.
4. No forcing FluentValidation everywhere.
5. No micro-abstraction for every 5-line handler.
6. No merging payment gateways.
7. No SharedKernel pollution with business types (see `docs/002-shared-kernel-vs-building-blocks.md`).

---

## 14. Concrete file-level duplication catalog

### 14.1 Outbox/inbox job shells (delete or keep via generic host)

| File |
|---|
| `Modules/*/Infrastructure/Workers/*OutboxPublisherJob.cs` (8 modules) |
| `Modules/*/Infrastructure/Workers/*InboxConsumerJob.cs` (8 modules) |
| `Modules/Messaging/Infrastructure/MessagingOutboxPublisherJob.cs` |
| `Modules/Messaging/Infrastructure/MessagingInboxConsumerJob.cs` |

### 14.2 Outbox/inbox EF config blocks

| File (OnModelCreating tail) |
|---|
| `Modules/Billing/Infrastructure/BillingDbContext.cs` |
| `Modules/Commerce/Infrastructure/CommerceDbContext.cs` |
| `Modules/Communications/Infrastructure/CommunicationsDbContext.cs` |
| `Modules/CRM/Infrastructure/CrmDbContext.cs` |
| `Modules/Lhdn/Infrastructure/LhdnDbContext.cs` |
| `Modules/Messaging/Infrastructure/MessagingDbContext.cs` |
| `Modules/One/Infrastructure/OneDbContext.cs` |
| `Modules/Ops/Infrastructure/OpsDbContext.cs` |
| `Modules/Payments/Infrastructure/PaymentsDbContext.cs` |

### 14.3 Inbox enqueue clones

| File |
|---|
| `Modules/Messaging/Infrastructure/TenantProvisionedIntegrationEventHandler.cs` |
| `Modules/Messaging/Infrastructure/TenantUpdatedIntegrationEventHandler.cs` |
| `Modules/Messaging/Infrastructure/EventHandlers/WorkspaceUpdatedIntegrationEventHandler.cs` |

### 14.4 Pagination / list query clones

| File |
|---|
| `Modules/Billing/Infrastructure/Services/BillingQueryService.cs` |
| `Modules/Commerce/Infrastructure/Services/CommerceQueryService.Transactions.cs` |
| `Modules/Commerce/Infrastructure/Services/CommerceQueryService.CustomCheckouts.cs` |
| `Modules/Commerce/Infrastructure/Services/CommerceQueryService.Subscribers.cs` (in-memory) |
| `Modules/Ops/Infrastructure/Endpoints.cs` (offset + fake total) |
| `BuildingBlocks/Application/PaginatedResponse.cs` (shared envelope — good) |

### 14.5 Gateway glue clones

| File |
|---|
| `Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs` |
| `Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` |
| `Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs` |
| `Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` (partial) |

### 14.6 Webhook delivery clones (semantic cousins)

| File |
|---|
| `Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs` |
| `Modules/Lhdn/Infrastructure/Services/WebhookSenderService.cs` |

### 14.7 Domain polling job shells

| File |
|---|
| `Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` |
| `Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs` |
| `Modules/Commerce/Infrastructure/Workers/CheckoutSessionExpiryJob.cs` |
| `Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs` |
| `Modules/Billing/Infrastructure/Workers/RevenueRecognitionJob.cs` |
| `Modules/Lhdn/Infrastructure/Workers/LhdnSubmissionJob.cs` |
| `Modules/Lhdn/Infrastructure/Workers/LhdnStatusPollingJob.cs` |
| `Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs` |
| `Modules/Communications/Infrastructure/Workers/BroadcastFanoutJob.cs` |

### 14.8 Error handling exemplars (to align toward)

| Quality | File |
|---|---|
| Best M2M | `Modules/Payments/Infrastructure/IntegrationEndpoints.cs` + `PaymentIntegrationException.cs` |
| Global baseline | `BuildingBlocks/Infrastructure/GlobalExceptionHandler.cs` |
| Fragile message parse | `Modules/Commerce/Infrastructure/Endpoints/PublicEndpoints.cs` (`ex.Message.Contains("not found")`) |
| Abused DTO | `Modules/Commerce/Infrastructure/Endpoints/TransactionEndpoints.cs` (`StatusResponse.Status = ex.Message`) |

### 14.9 Tenant naming exemplars

| Name | File examples |
|---|---|
| `OrganizationId` events | `Modules/Payments/Contracts/Events/GatewayPaymentCompletedIntegrationEvent.cs` |
| `TenantId` events | `Modules/One/Contracts/TenantProvisionedIntegrationEvent.cs`, `AppEntitlementGrantedIntegrationEvent.cs` |
| Ambient TenantId | `BuildingBlocks/Application/IExecutionContextAccessor.cs` |
| Persist OrganizationId | `BuildingBlocks/Domain/IMustHaveTenant.cs` |

---

## 15. Risk notes for consolidations

| Change | Risk | Mitigation |
|---|---|---|
| Rename event `TenantId` → `OrganizationId` | Break in-flight outbox/inbox payloads if any rows store old JSON property names | Deploy consumers that accept both; drain outbox before rename; or version event types |
| Generic hosted outbox jobs | Logger category / metrics cardinality changes | Include schema name in log scopes |
| Stricter exception → HTTP mapping | Clients relying on 400 for not-found | Coordinate with admin/ops frontends; changelog |
| Remove empty inbox jobs | If a handler starts writing inbox later without registering job | Checklist + architecture test: if InboxMessages written, job must be registered |
| Move Billing handlers to Application | Project reference adjustments | Do per-handler with tests |
| Shared webhook signature | External LHDN webhook clients | Version header or dual-verify period |

---

## 16. Suggested acceptance criteria for “tech debt reduced”

1. **No** duplicated Outbox/Inbox EF configuration blocks — one extension method.
2. Module DI for messaging is ≤ ~5 lines (`AddModuleOutboxInbox<T>`).
3. All **public** list endpoints use `page`+`limit` (or documented cursor) and truthful `TotalCount`.
4. Global exception handler maps NotFound vs BusinessRule vs 500 without leaking internals in Production.
5. New integration events use `OrganizationId` only; existing `TenantId` events either migrated or listed as known debt with owners.
6. Payment adapters share `GatewayCommon` for name/minor units; still separate classes.
7. Messaging inbox enqueue uses one helper (no triple clone).
8. Architecture / contributor doc answers: handler placement, error types, inbox when, pagination params.
9. Zero new endpoint returns error text inside `StatusResponse.Status`.
10. Ops chat list pagination either fixed total or non-`PaginatedResponse` type.

---

## 17. Appendix — “good patterns to copy” inside the repo

When implementing new features, prefer these existing exemplars over inventing new ones:

| Need | Copy from |
|---|---|
| Cross-module command DTO | `Modules/Billing/Contracts/Commands/*` |
| Application handler + repository port | `Modules/Commerce/Application/Commands/*` + `ICommerceRepository` |
| Keyed outbox publish | `OutboxEventBus<T>` via module keyed `IEventBus` |
| Integration handler | Commerce `GatewayPaymentCompletedIntegrationEventHandler` (idempotent, command dispatch) |
| M2M error codes | `PaymentIntegrationException` / `IntegrationEndpoints` |
| Tenant-safe not found | Commerce product handlers (`null || wrong org`) |
| Durable developer webhooks | One `OutboundWebhookDispatcherJob` |
| Store-and-ack inbox | Messaging provision/update handlers + Application notification handlers |
| Paginated SQL read | Billing `GetLedgerEntriesAsync` / Commerce `GetTransactionsAsync` |
| Secrets | `ISecretVault` + `DecryptOrPlaintext` for migration |

---

## 18. Closing judgment

The backend’s **macro** architecture (modules, outbox, tenant filters, contracts) is coherent. The debt is **micro-repetition and inconsistency** at the glue layer: 18 job shells, 9 EF configs, divergent error JSON, dual tenant names, dual pagination dialects, and handler folders that vary by module age.

Best ROI consolidations are **small BuildingBlocks helpers + exception/pagination/naming policy**, applied opportunistically when files are touched — not a rewrite. That improves maintainability without creating a framework that fights the modular monolith.

---

*Generated as maintenance analysis plan `plans/004-maintenance/09-duplication-tech-debt.md`. No application code was modified.*
