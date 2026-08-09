# 08 — Composition root, DI registration, and endpoint surface area (lazuar-api)

**Status:** Analysis only — do not implement from this file alone  
**Date:** 2026-08-09  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Scope:**  
- `apps/lazuar-api/src/Lazuar.Api/**` (especially `Program.cs`, middleware, host event handlers)  
- each `apps/lazuar-api/Modules/*/Infrastructure/DependencyInjection.cs`  
- each `apps/lazuar-api/Modules/**/Endpoints*.cs` and `Endpoints/**/*.cs`  

**Goal of this note:** Characterize how fat the composition root is, how consistent module DI is, how large and organized the HTTP surface is, where auth/CORS/middleware live, and what concrete modularization opportunities remain — without changing app code.

---

## 1. Executive summary

| Dimension | Finding |
|-----------|---------|
| **Program.cs fatness** | **~486 lines** — moderately fat for a modular monolith. Modules already own DbContext, repositories, workers, and most routes, but the host still owns config bootstrap, cross-cutting infrastructure, authz policy catalog, CORS, MediatR assembly enumeration, boot-time EF migrate loop, middleware pipeline, dual API-key event subscriptions, health routes, and route map wiring. |
| **Module DI consistency** | **Strong shared skeleton**, with several intentional and accidental outliers (Messaging connection name, Payments missing SQL factory + connection null-check, CRM no HTTP surface / no SQL factory, Billing alone ignores `PendingModelChangesWarning`, Ops empty subscriptions). |
| **Endpoint organization** | **Two styles coexist:** (A) Commerce/Communications split into `Endpoints/` partials with a thin root mapper; (B) One/Billing/Lhdn/Ops/Messaging keep a single large file. **One is the outlier by size (~767 lines, ~36 HTTP maps).** |
| **Auth / CORS / middleware** | Pipeline order is clear and mostly correct for dual cookie+API-key auth. Auth **policies** and **JWT cookie selection** remain host-owned. Tenant path allow/deny lists live in `TenantSecurityMiddleware` (host), not modules. CORS is default policy only; some modules re-`RequireCors()` redundantly. |
| **Further modularization ROI** | Highest: extract Program “slices” (config, auth, infrastructure, migrations, health, pipeline); split One endpoints; centralize MediatR/module registration via a host composition helper or module interface; align DI edge-cases. Lowest urgency: empty Ops subscriptions stub (harmless). |

**Bottom line:** The modular monolith pattern is already real — nine `Add*Module` + `Use*Subscriptions` + `Map*Endpoints` extension pairs keep most domain wiring out of the host. Program.cs is not a god-file of business logic, but it is still the **cross-cutting catalog** and **boot orchestrator**. Endpoint surface is large (~130+ mapped HTTP verbs across modules + 3 health endpoints) and unevenly filed.

---

## 2. Program.cs anatomy (how fat is it?)

**Path:** [`apps/lazuar-api/src/Lazuar.Api/Program.cs`](../../apps/lazuar-api/src/Lazuar.Api/Program.cs)  
**Approx. size:** **486 lines** (including usings, top-level statements, `public partial class Program { }`).

### 2.1 Section map (line ranges approximate)

| Lines (approx.) | Concern | Belongs in host? | Extract candidate? |
|-----------------|---------|------------------|--------------------|
| 1–41 | Usings (Serilog, EF, JWT, Azure Key Vault, AWS S3, all module Infrastructure namespaces) | Host | No (or auto-generated if composition helpers live in same assembly) |
| 43–60 | Manual `.env` file loader into `Environment` | Host / local dev | Yes → `AddLocalEnvFile()` / `ConfigurationExtensions` |
| 62–78 | `WebApplication.CreateBuilder`, env vars, optional Azure Key Vault | Host | Yes → `AddLazuarConfiguration(builder)` |
| 80–88 | Serilog bootstrap | Host | Yes → `AddLazuarSerilog(builder)` |
| 90–97 | Options binding: Resend, BackgroundWorker, Observability, PlatformAdminSettings | Host (cross-cutting) | Yes → `AddPlatformOptions` |
| 99–108 | Metrics gauges + `PlatformMetricsCollector` + refresh job | Host / observability | Yes → `AddPlatformObservability` |
| 110–119 | Named HttpClient `"Resend"` | Host (shared email infra) | Yes → with email infra |
| 121–133 | Core platform services: `HttpContextAccessor`, memory cache, `IExecutionContextAccessor`, job trigger, password/JWT/messaging/email/secret vault/magic link, LLM factory, in-memory event bus | Host | Yes → `AddPlatformInfrastructure` |
| 135–159 | Conditional R2/S3 registration vs `DisabledR2StorageService` | Host | Yes → `AddObjectStorage` |
| 161–162 | Host integration event handlers (API key revoke cache eviction, workspace updated) | Host | Keep near middleware or `AddHostEventHandlers` |
| 164–208 | JWT secret guard (prod fail-closed) + `AddAuthentication` / `AddJwtBearer` with dual cookie names (`lazuar_auth` vs `lazuar_admin_auth`) | Host | Yes → `AddLazuarAuthentication` |
| 210–286 | **Authorization policy catalog** (OrgAdmin + 6 Integration* policies) | Host (shared policy names used by many modules) | Yes → `AddLazuarAuthorizationPolicies` (still host assembly; modules only *consume* policy names) |
| 288–308 | Default CORS policy from `App:CorsOrigins` | Host | Yes → `AddLazuarCors` |
| 310–314 | Snake_case JSON options | Host | Yes → with API conventions |
| 316–317 | Exception handler + problem details | Host | Yes |
| 319–340 | **MediatR assembly registration** (host + 8 Application + 9 Infrastructure assemblies; CRM Application absent) | Host | Yes → loop over module markers / `AddLazuarMediatR` |
| 342–350 | **Module DI:** `AddOneModule` … `AddCommunicationsModule` | Host thin | Already thin; can be one `AddAllModules` |
| 352–394 | **Boot-time `MigrateAsync` for 9 DbContexts** | Host | Yes → `MigrateAllModuleDatabasesAsync(app)` |
| 396–402 | Middleware pipeline | Host | Yes → `UseLazuarPipeline` |
| 404–412 | Module event bus subscriptions | Host thin | One `UseAllModuleSubscriptions` |
| 414–418 | Host dual-subscribe (One + legacy Lhdn API key revoke; WorkspaceUpdated) | Host | Keep with host handlers |
| 420–463 | Health liveness / readiness / metrics | Host | Yes → `MapHealthEndpoints` |
| 465–481 | API route groups + `Map*Endpoints` | Host thin | One `MapAllModuleEndpoints` |
| 483–486 | `RunAsync` + partial Program for tests | Host | Keep |

### 2.2 What is *not* in Program.cs (good)

- No business command handlers.
- No per-module repository registration (except host platform services).
- No large inline endpoint bodies for product modules (only health).
- Modules own schema-scoped EF migrations history tables (`one`, `messaging`, `payments`, …).

### 2.3 Fatness verdict

Program.cs is **orchestration-fat**, not **domain-fat**. Rough composition by concern volume:

| Bucket | Rough share of Program.cs |
|--------|---------------------------|
| AuthN + AuthZ policies | ~25% |
| Platform infra (R2, options, metrics, clients) | ~20% |
| Config bootstrap (.env, Key Vault, Serilog) | ~12% |
| MediatR + module Add*/Use*/Map* wiring | ~15% |
| Migrate-on-boot loop | ~10% |
| Middleware + host event subscriptions | ~8% |
| Health endpoints | ~10% |

A healthy modular host for this codebase could be **~80–120 lines** if the above were extracted into host-side extension methods under e.g. `Lazuar.Api/Composition/` without changing behavior.

---

## 3. Host supporting surface (Lazuar.Api project)

Beyond Program.cs, the host assembly owns true composition-root concerns:

| Path | Role |
|------|------|
| `Middleware/CorrelationIdMiddleware.cs` | `X-Correlation-Id` accept/generate + log scope |
| `Middleware/ApiKeyAuthenticationMiddleware.cs` | Bearer/header API key → claims (`API_CLIENT`, scopes, TenantId); dual lookup `one.ApiCredentials` then legacy `lhdn.DeveloperApiKeys`; 5-minute memory cache |
| `Middleware/TenantSecurityMiddleware.cs` | Resolve tenant from header/slug/route; inject membership role; path-based **exempt** vs **required** lists |
| `ExecutionContextAccessor.cs` | `IExecutionContextAccessor` from `HttpContext` Items/Claims |
| `EventHandlers/ApiKeyRevokedIntegrationEventHandler.cs` | Evict API key cache on revoke (One + Lhdn event types) |
| `EventHandlers/WorkspaceUpdatedIntegrationEventHandler.cs` | Host-level workspace change reaction |
| `Configuration/AppOptions.cs` | Host options types (if any app-level) |
| `Infrastructure/Data/PlatformDbContext.cs` | Present in tree (platform-oriented); **not** registered in the Program migrate list of nine module contexts |

These files correctly stay host-adjacent: they couple HTTP ambient context to multi-module identity/tenant rules.

---

## 4. Middleware pipeline and auth placement

### 4.1 Actual order (`Program.cs`)

```
UseExceptionHandler
UseMiddleware<CorrelationIdMiddleware>
UseCors
UseAuthentication          // JWT (cookie or Authorization bearer)
UseMiddleware<ApiKeyAuthenticationMiddleware>  // may replace/set User for API keys
UseMiddleware<TenantSecurityMiddleware>        // TenantId + role injection
UseAuthorization
// then Use*Subscriptions (not request middleware — event bus side-effect at startup)
// then Map* routes
```

**Assessment:** Order is intentional and mostly right:

1. Exception handler outermost for unified errors.  
2. Correlation ID early for logs.  
3. CORS before auth so preflight is not blocked by auth middleware.  
4. JWT authentication before API key middleware so cookie/bearer human sessions work; API key middleware can still establish identity when a key is presented.  
5. Tenant resolution **after** both auth mechanisms so membership checks see an authenticated principal.  
6. Authorization last among security middlewares.

### 4.2 Dual cookie scheme (host-owned)

`JwtBearerEvents.OnMessageReceived` selects:

| Path prefix | Cookie |
|-------------|--------|
| `/api/v1/platform` | `lazuar_admin_auth` |
| everything else | `lazuar_auth` |

Platform login lives in Payments’ `PlatformEndpoints` (issues admin cookie with path `/api/v1/platform`). One endpoints issue `lazuar_auth`. This is **host-coupled route knowledge inside auth config** — correct for security, but any new cookie realm must touch Program.

### 4.3 Authorization policies (host catalog)

Defined only in Program.cs:

| Policy name | Who passes |
|-------------|------------|
| `OrgAdmin` | Authenticated + role `SUPER_ADMIN` or `ADMIN` |
| `IntegrationLhdnDocumentsWrite` | Human admin **or** `API_CLIENT` + scope `lhdn.documents:write` (via `PlatformApiScopes`) |
| `IntegrationLhdnDocumentsRead` | Human admin **or** API_CLIENT with read **or** write scope |
| `IntegrationPaymentsCheckoutsWrite` | Human admin **or** API_CLIENT + payments checkouts write |
| `IntegrationPaymentsCheckoutsRead` | Human admin **or** API_CLIENT with read **or** write |
| `IntegrationPaymentsConfigRead` | Human admin **or** API_CLIENT + payments config read |
| `IntegrationWebhooksEndpointsManage` | Human admin **or** API_CLIENT + webhooks manage |

**Consumers:**

- Lhdn document groups use Integration LHDN policies.  
- Payments integration endpoints use Integration payments policies.  
- One scope probe uses `IntegrationPaymentsCheckoutsWrite`.  
- OrgAdmin used widely (`/admin/*`, messaging notify, Lhdn admin façade, One api-keys subgroup, Communications admin).  
- Ops uses **inline** `RequireRole("CLIENT", "ADMIN")` — **not** the named policy catalog (inconsistency).  
- Platform group uses **inline** `RequireRole("SUPER_ADMIN")` on the group (login/logout mark `AllowAnonymous`).

**Note:** `IntegrationPaymentsConfigRead` and `IntegrationWebhooksEndpointsManage` appear registered for future/M2M attachment; webhook manage for humans is largely custom logic inside One endpoints (`CanAccessWorkspaceWebhooksAsync`), not the named policy alone.

### 4.4 Tenant path rules (host-owned, fragile coupling)

`TenantSecurityMiddleware`:

**Exempt** (no required X-Tenant-Id):  
`/health`, `/api/v1/public`, `/api/v1/webhooks`, `/api/v1/one/public`, `/api/v1/one/auth`, `/api/v1/one/me`, `/api/v1/one/workspaces`, `/api/v1/one/integrations/workspaces`, and entire `/api/v1/platform` (hard-codes platform tenant GUID `…0001`).

**Requires tenant:**  
`/api/v1/admin`, `/api/v1/lhdn`, `/api/v1/ops`, `/api/v1/messaging`, `/api/v1/one/storage`, `/api/v1/one/api-keys`.

**Implications:**

- Adding a new admin module path under `/api/v1/admin/foo` inherits tenant requirement automatically (good).  
- Adding tenant-scoped routes outside those prefixes (e.g. `/api/v1/integrations/...`) does **not** force tenant via middleware — Payments integration relies on API key binding TenantId instead.  
- One workspace routes are exempt so users can list/create workspaces without ambient tenant; finer auth is per-endpoint.  
- Path knowledge is **centralized in the host middleware**, not declared by modules — a modularity gap if modules want self-describing security metadata.

### 4.5 CORS

- Single default policy from `App:CorsOrigins` (comma-split).  
- Empty config → `AllowAnyOrigin` + any header/method (**no credentials**).  
- Non-empty → `WithOrigins` + credentials.  
- Applied: `app.UseCors()` globally; `apiGroup` and `platformGroup` also `.RequireCors()`; One group and Payments integration group re-apply `.RequireCors()` again (redundant given parent group).

### 4.6 API key authentication details (host)

- Looks up hash in `one."ApiCredentials"` then legacy `lhdn."DeveloperApiKeys"`.  
- Caches by hash 5 minutes; revoke handler on host event bus clears cache.  
- Sets authentication type `"ApiKey"` so tenant middleware skips membership role injection (tenant already bound from key).  
- Cross-schema SQL in host middleware is a deliberate platform concern; depends on both schemas existing (migrate-on-boot helps).

---

## 5. Module DI registration patterns

### 5.1 Canonical skeleton (almost every module)

```csharp
public static IServiceCollection AddXxxModule(this IServiceCollection services, IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("...");

    services.AddDbContext<XxxDbContext>(options =>
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "xxx")));

    services.AddKeyedScoped<ISqlConnectionFactory, NpgsqlConnectionFactory>("XxxSqlConnectionFactory", ...);
    // repositories / query services / domain ports
    services.AddKeyedScoped<IEventBus, OutboxEventBus<XxxDbContext>>("XxxEventBus");
    // transient integration event handlers
    // hosted services: InboxConsumer + OutboxPublisher (+ domain jobs)
    return services;
}

public static IApplicationBuilder UseXxxSubscriptions(this IApplicationBuilder app)
{
    var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriptions>();
    // eventBus.Subscribe<TEvent, THandler>();
    return app;
}
```

Application-layer `DependencyInjection` types are **empty markers** for MediatR assembly scanning (One, Messaging, Payments, Ops, Billing, Lhdn, Commerce, Communications). CRM has **no Application project** — handlers live in Infrastructure and are scanned via Infrastructure assembly only.

### 5.2 Per-module DI matrix

| Module | Add method | Connection | DbContext schema history | SQL factory key | EventBus key | Hosted services (count / notes) | Transient handlers | Use*Subscriptions | HTTP Map* |
|--------|------------|------------|--------------------------|-----------------|--------------|----------------------------------|--------------------|-------------------|-----------|
| **One** | `AddOneModule` | `Default` throw | `one` | `OneSqlConnectionFactory` | `OneEventBus` | Genesis bootstrap, Inbox, Outbox, Outbound webhook dispatcher (4) | Outbound webhook handlers | 1 subscribe | `MapOneEndpoints` |
| **Messaging** | `AddMessagingModule` | **`MessagingConnection` only** throw | `messaging` | `MessagingSqlConnectionFactory` | `MessagingEventBus` | Outbox, Inbox (2) | Tenant provision/update, workspace, dispatch | 4 | `MapMessagingEndpoints` |
| **CRM** | `AddCrmModule` | `Default` throw | `crm` | **none** | `CrmEventBus` | Outbox, Inbox (2) | GlobalUserProfileUpdated | 1 | **none** |
| **Payments** | `AddPaymentsModule` | `Default` **no throw if null** | `payments` | **none** | `PaymentsEventBus` | Inbox, Outbox (2) | Refund, off-session charge, integration checkout gateway | 4 | `MapPaymentsEndpoints`, `MapPaymentsIntegrationEndpoints`, `MapPlatformEndpoints` |
| **Ops** | `AddOpsModule` | `Default` throw | `ops` | `OpsSqlConnectionFactory` | `OpsEventBus` | Inbox, Outbox (2) | none registered | **empty passthrough** | `MapOpsEndpoints` |
| **Billing** | `AddBillingModule` | `Default` throw | `billing` + **Ignore PendingModelChangesWarning** | `BillingSqlConnectionFactory` | `BillingEventBus` | Inbox, Outbox, B2cConsolidation; RevenueRecognition **commented out** (3 active) | ~11 handlers | ~11 | `MapBillingEndpoints` |
| **Lhdn** | `AddLhdnModule` | `Default` throw | `lhdn` | `LhdnSqlConnectionFactory` | `LhdnEventBus` | Submission, Status poll, Reference seeder, Outbox, Inbox (5) | Invoice/refund/consolidated | 3 | `MapLhdnEndpoints` |
| **Commerce** | `AddCommerceModule` | `Default` throw | `commerce` | `CommerceSqlConnectionFactory` | `CommerceEventBus` | Inbox, Outbox, BillingEngine, DunningEngine, Checkout expiry (5) | ~7 | ~9 | `MapCommerceEndpoints` |
| **Communications** | `AddCommunicationsModule` | `Default` throw | `communications` | `CommunicationsSqlConnectionFactory` | `CommunicationsEventBus` | Inbox, Outbox, BroadcastFanout (3) | ~6 | 7 | `MapCommunicationsEndpoints` |

### 5.3 DI consistency findings

#### Consistent (good)

1. **Keyed outbox event bus per module** — isolation of transactional outbox writes per DbContext.  
2. **Inbox + Outbox hosted jobs** almost everywhere.  
3. **MigrationsHistoryTable per PostgreSQL schema** — multi-schema modularity.  
4. **Extension method naming:** `Add{Module}Module` / `Use{Module}Subscriptions` / `Map{Module}Endpoints`.  
5. **Transient** registration for integration event handlers (matches `IEventBusSubscriptions` resolve pattern).  
6. Application marker classes for MediatR where Application layer exists.

#### Inconsistencies / risks

| # | Issue | Modules | Risk | Suggested direction |
|---|--------|---------|------|---------------------|
| 1 | **Connection string name differs** | Messaging → `MessagingConnection`; all others → `Default` | Misconfig only breaks Messaging | Document; or fall back `MessagingConnection ?? Default` if intentional single-DB |
| 2 | **Payments does not throw** on missing connection | Payments | Silent null into `UseNpgsql` → obscure boot failure | Align with others’ throw |
| 3 | **No `ISqlConnectionFactory` for Payments or CRM** | Payments, CRM | Dapper endpoints elsewhere use One factory (PlatformEndpoints uses One’s keyed factory) | Add factory for Payments if Dapper/raw SQL grows; CRM may stay EF-only |
| 4 | **Only Billing ignores PendingModelChangesWarning** | Billing | Other modules fail migrate hard on model drift; Billing continues | Either adopt host-wide policy or remove special case after migrations clean |
| 5 | **QuestPDF license set inside Billing DI** | Billing | Side effect at registration | Acceptable; could be explicit init step |
| 6 | **Ops `UseOpsSubscriptions` is no-op** | Ops | Harmless noise in Program pipeline | Remove from Program **or** keep for future symmetry |
| 7 | **CRM has no HTTP endpoints** | CRM | Correct if internal-only; easy to miss that CRM is “headless” | Document; no MapCrmEndpoints by design |
| 8 | **MediatR list is hand-maintained** | Program | Easy to forget a new module assembly | `IModule` marker scan or shared array |
| 9 | **Host migrate list hand-maintained** | Program | Same | Same list as Add modules |
| 10 | **Error message wording** | “was not found” vs “not found” | Cosmetic | Normalize |
| 11 | **PlatformEndpoints in Payments module** | Payments | Super-admin auth + payment-config co-located; One-sql coupling via keyed factory | Consider host or One for platform auth; keep payment-config in Payments |
| 12 | **Dual API key revoke subscription** | Host | Migration dual-write period | Temporary; remove Lhdn path when legacy keys gone |

### 5.4 What each module registers beyond the skeleton

- **One:** Integrator provision options + rate limiter; `IApiCredentialService`; HTTP client `DeveloperWebhooks`; genesis + outbound webhook jobs.  
- **Payments:** Multi-gateway adapters (`Stripe`, `Billplz`, `Razorpay`, `ChipCollect`) + factory; checkout cashier; no SQL factory.  
- **Lhdn:** Keyed UBL strategies; certificate vault; template renderer; gateway adapter; many workers.  
- **Billing:** Credit cost options; many cross-module event handlers (Payments, Lhdn, Commerce, One); LLM prompt provider.  
- **Commerce:** Extra engines (billing, dunning, session expiry); cross-module handlers (Payments, Communications, CRM).  
- **Communications:** Suppression + query services; fanout job; handlers from Commerce/Billing/One/CRM.  
- **Messaging:** Tenant replica repository; uses separate connection name.  
- **Ops:** Tool registry + LLM orchestrator (no integration event subscribers).  
- **CRM:** Query service only + one profile-updated handler.

### 5.5 Host-level MediatR registration detail

Program registers:

- `typeof(Program).Assembly`  
- Application: One, Messaging, Payments, Ops, Billing, Lhdn, Commerce, Communications (**not CRM**)  
- Infrastructure: all nine modules **including CRM**

This matches CRM’s structure (handlers in Infrastructure). Adding a CRM Application layer later would require a Program edit unless scanning is automated.

---

## 6. Endpoint surface area and route organization

### 6.1 Host mapping (thin)

```csharp
var apiGroup = app.MapGroup("/api/v1").RequireCors();
apiGroup.MapOneEndpoints();
apiGroup.MapMessagingEndpoints();
apiGroup.MapPaymentsEndpoints();
apiGroup.MapPaymentsIntegrationEndpoints();
apiGroup.MapOpsEndpoints();
apiGroup.MapBillingEndpoints();
apiGroup.MapLhdnEndpoints();
apiGroup.MapCommerceEndpoints();
apiGroup.MapCommunicationsEndpoints();

var platformGroup = app.MapGroup("/api/v1/platform")
   .RequireCors()
   .RequireAuthorization(policy => policy.RequireRole("SUPER_ADMIN"));
platformGroup.MapPlatformEndpoints();
```

Plus three host health routes **outside** `/api/v1`:

| Method | Path | Auth |
|--------|------|------|
| GET | `/health` | none (liveness) |
| GET | `/health/ready` | none (DB + optional outbox lag) |
| GET | `/health/metrics` | none (process/DB gauges) |

**Missing by design:** `MapCrmEndpoints` — CRM is event/query internal.

### 6.2 File inventory and sizes

| File | Approx. lines | Map methods / verbs | Style |
|------|---------------|---------------------|--------|
| `Modules/One/Infrastructure/Endpoints.cs` | **~767** | ~36 | Monolith file + helpers (`IssueCookie`, `CanAccessWorkspaceWebhooksAsync`) |
| `Modules/Billing/Infrastructure/Endpoints.cs` | **~238** | 12 | Single file admin+public |
| `Modules/Lhdn/Infrastructure/Endpoints.cs` | **~247** | 13 | Single file multi-policy groups |
| `Modules/Ops/Infrastructure/Endpoints.cs` | **~211** | 9 | Single file chat/agent |
| `Modules/Payments/Infrastructure/PlatformEndpoints.cs` | **~165** | 5 | Platform admin |
| `Modules/Payments/Infrastructure/IntegrationEndpoints.cs` | **~153** | 2 (+ DTOs) | M2M checkouts |
| `Modules/Communications/Infrastructure/Endpoints/PublicComplianceEndpoints.cs` | **~179** | 2 | Public + Resend webhook |
| `Modules/Commerce/Infrastructure/Endpoints/PublicEndpoints.cs` | **~360+** | 10 | Public storefront/portal |
| `Modules/Commerce/Infrastructure/Endpoints/SubscriberEndpoints.cs` | **~180+** | 8 | Admin subscribers |
| `Modules/Communications/Infrastructure/Endpoints/TemplateEndpoints.cs` | **~140** | 8 | Admin templates |
| `Modules/Payments/Infrastructure/Endpoints.cs` | **~91** | 1 | Inbound payment webhooks |
| `Modules/Communications/Infrastructure/Endpoints/BroadcastEndpoints.cs` | **~93** | 3 | Admin broadcasts |
| `Modules/Commerce/Infrastructure/Endpoints/ProductEndpoints.cs` | **~140** | 6 | Admin products |
| `Modules/Commerce/Infrastructure/Endpoints/DunningCampaignEndpoints.cs` | **~95** | 5 | Admin dunning |
| `Modules/Commerce/Infrastructure/Endpoints/CouponEndpoints.cs` | **~90** | 4 | Admin coupons |
| `Modules/Commerce/Infrastructure/Endpoints.cs` | **~81** | 3 + child maps | **Composition root for commerce routes** |
| `Modules/Messaging/Infrastructure/Endpoints.cs` | **~67** | 2 | Small |
| `Modules/Communications/Infrastructure/Endpoints.cs` | **~59** | 2 + child maps | **Composition root for communications routes** |
| `Modules/Commerce/Infrastructure/Endpoints/TransactionEndpoints.cs` | **~60** | 2 | Admin transactions |
| `Modules/Commerce/Infrastructure/Endpoints/PaymentConfigEndpoints.cs` | **~40** | 2 | Admin payment config |
| `Modules/Commerce/Infrastructure/Endpoints/StatsEndpoints.cs` | **~25** | 1 | Admin stats |

**Rough total HTTP verb maps:** ~130–140 across modules + 3 health ≈ **~135–145 endpoints**.

### 6.3 Route tree (organization by URL prefix)

All product routes hang under `/api/v1` unless noted.

```
/health, /health/ready, /health/metrics          (host)

/api/v1/one/...                                  (identity, workspaces, webhooks, api-keys, storage, provision)
/api/v1/messaging/...                            (notify, delivery-logs)
/api/v1/webhooks/payments/{gateway}/{tenantId}   (inbound PSPs — no auth middleware tenant requirement; path-tenant)
/api/v1/integrations/payments/checkouts...       (M2M checkouts — API key scopes)
/api/v1/ops/...                                  (agent chat)
/api/v1/admin/billing/...                        (org admin finance)
/api/v1/public/billing/...                       (signed document links, public profile)
/api/v1/lhdn/...                                 (integration docs + org admin façade)
/api/v1/admin/commerce/...                       (catalog, subs, dunning, …)
/api/v1/public/commerce/...                      (storefront, portal, checkout)
/api/v1/admin/communications/...                 (templates, broadcasts, email config)
/api/v1/public/communications/...                (unsubscribe, resend webhook)

/api/v1/platform/...                             (super-admin: auth + platform payment-config)
```

### 6.4 AuthZ attachment patterns by module

| Module | Group-level auth | Per-route auth | Notes |
|--------|------------------|----------------|-------|
| One | `/one` only RequireCors; mixed per route | Many `RequireAuthorization()`; OrgAdmin subgroup for api-keys; Integration probe; provision uses custom secret/JWT | Largest auth diversity |
| Messaging | none on group | OrgAdmin per route | Small |
| Payments webhooks | none | none (gateway signature inside handler) | Public webhook |
| Payments integration | RequireCors on group | Write/Read policies per verb | M2M |
| Platform | SUPER_ADMIN on group | login/logout AllowAnonymous | Cookie realm |
| Ops | CLIENT or ADMIN on group | (inherits) | Inline role, not named policy |
| Billing | OrgAdmin on admin; public open | — | Signed query params for documents |
| Lhdn | Three parallel `/lhdn` groups | Integration write/read + OrgAdmin | Cleanest multi-policy split |
| Commerce | OrgAdmin admin; public open | — | Best file split |
| Communications | OrgAdmin admin; public open | — | Good file split |

### 6.5 Endpoint modularization quality

**Gold standard in-repo:** Commerce and Communications.

```
MapCommerceEndpoints
  adminGroup /admin/commerce + OrgAdmin
    MapProductEndpoints, MapDunning…, MapPaymentConfig…,
    MapSubscriber…, MapTransaction…, MapCoupon…, MapStats…
    + a few custom-checkout maps still inline in root
  publicGroup /public/commerce
    MapPublicCommerceEndpoints

MapCommunicationsEndpoints
  adminGroup /admin/communications + OrgAdmin
    MapTemplateEndpoints, MapBroadcastEndpoints
    + email-config GET/PUT inline
  MapPublicComplianceEndpoints
```

**Needs the same treatment:** One (auth, workspaces, webhooks, api-keys, storage, integrations), secondarily Billing and Lhdn (already coherent groups but single files), Ops (agent vs execute-action), Payments Platform auth vs payment-config.

**Largest remaining inline logic smell:** One’s integrator provision handler (~150 lines of rate limit + DTO mapping inside the endpoint), One webhook CRUD with custom authZ helper, Commerce `PublicEndpoints` Dapper SQL for update-payment, Ops `execute-action` dynamic tool dispatch.

### 6.6 Endpoint vs application layer purity

Endpoints generally:

- Map DTO → MediatR command/query, or  
- Call module query services / repositories, or  
- Occasionally hit DbContext / Dapper directly (One auth/me, Messaging delivery-logs, Platform auth SQL, Commerce public update-payment).

This is typical for minimal APIs in a modular monolith; the worse offenders for composition health are **file size** and **cross-module SQL in endpoint bodies**, not the DI graph.

---

## 7. Cross-cutting composition: event bus

### 7.1 In-process bus

- Host: `InMemoryEventBus` singleton as both `IEventBus` (direct publish?) and `IEventBusSubscriptions`.  
- Modules: keyed `OutboxEventBus<TDbContext>` for durable publish inside module transactions.  
- At startup, each `Use*Subscriptions` registers handlers onto the **global** subscription bus.

### 7.2 Host dual-subscribe (migration)

```csharp
eventBus.Subscribe<One.ApiKeyRevoked..., Host.ApiKeyRevoked...>();
eventBus.Subscribe<Lhdn.ApiKeyRevoked..., Host.ApiKeyRevoked...>();
eventBus.Subscribe<One.WorkspaceUpdated..., Host.WorkspaceUpdated...>();
```

This is correct temporary composition for API key cache coherence. It is **not** expressible as a module subscription without the host knowing about middleware cache keys — fine as host concern.

### 7.3 Multi-handler same event

Billing deliberately double-subscribes `GatewayPaymentCompletedIntegrationEvent` to two handlers (payment complete + platform top-up). Composition allows multi-cast; document as intentional.

---

## 8. Boot-time database migration composition

Program builds a **hard-coded array** of 9 DbContexts and `MigrateAsync` each before accepting traffic / before hosted services meaningfully depend on schema.

| Order in array | Context |
|----------------|---------|
| 1 | OneDbContext |
| 2 | MessagingDbContext |
| 3 | PaymentsDbContext |
| 4 | CrmDbContext |
| 5 | OpsDbContext |
| 6 | BillingDbContext |
| 7 | LhdnDbContext |
| 8 | CommerceDbContext |
| 9 | CommunicationsDbContext |

**Behavior:**

- Logs progress per context.  
- `PendingModelChanges` `InvalidOperationException` → log error, **continue** (partial boot).  
- Other exceptions → fail boot.  

**Coupling:** Any new module requires Program edits in **three places**: MediatR assemblies, `Add*Module`, migrate list (+ usually Map/Use). This is the strongest remaining composition-root tax.

---

## 9. Opportunities to modularize the composition root further

Ranked by impact / effort.

### 9.1 High impact, medium effort — Host composition folders

Introduce host-only static classes (no module boundary change):

```
Lazuar.Api/Composition/
  ConfigurationExtensions.cs   // .env, Key Vault, Serilog
  PlatformInfrastructure.cs    // options, R2, Resend client, vault, event bus, metrics
  AuthenticationExtensions.cs  // JWT + cookies + policies
  CorsAndJsonExtensions.cs
  MediatRExtensions.cs
  ModuleRegistration.cs        // AddAllModules, UseAllSubscriptions, MapAllEndpoints
  DatabaseMigration.cs         // MigrateAllAsync
  PipelineExtensions.cs        // middleware order
  HealthEndpoints.cs
```

**Target Program.cs shape:**

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddLazuarConfiguration();
builder.AddLazuarSerilog();
builder.Services.AddPlatformInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddLazuarAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddLazuarAuthorizationPolicies();
builder.Services.AddLazuarCors(builder.Configuration);
builder.Services.AddLazuarMediatR();
builder.Services.AddAllModules(builder.Configuration);

var app = builder.Build();
await app.MigrateAllModuleDatabasesAsync();
app.UseLazuarPipeline();
app.UseAllModuleSubscriptions();
app.MapHostEventSubscriptions();
app.MapHealthEndpoints();
app.MapAllModuleEndpoints();
await app.RunAsync();
```

**Does not change** module public APIs; only shrinks Program and groups concerns for review.

### 9.2 High impact, higher effort — `ILazuarModule` contract

```csharp
public interface ILazuarModule
{
    void AddServices(IServiceCollection services, IConfiguration config);
    void UseSubscriptions(IApplicationBuilder app);
    void MapEndpoints(IEndpointRouteBuilder endpoints); // may no-op
    Type? ApplicationMarker { get; }
    Type InfrastructureMarker { get; }
    Type DbContextType { get; } // for migrate
}
```

Each module implements and host enumerates `IEnumerable<ILazuarModule>` (manual list still OK for explicitness). Collapses three Program lists into one.

**Trade-off:** Modules gain a shared contracts package dependency or BuildingBlocks interface; CRM/Ops empty maps become explicit no-ops.

### 9.3 High impact for maintainability — Split One endpoints

Proposed split under `Modules/One/Infrastructure/Endpoints/`:

| File | Routes |
|------|--------|
| `AuthEndpoints.cs` | register, login, logout, forgot/reset, verify, resend, me |
| `ProfileEndpoints.cs` | me/profile, me/security, me/entitlements |
| `WorkspaceEndpoints.cs` | workspaces CRUD, members, invites, apps |
| `WebhookEndpoints.cs` | workspace webhooks + logs (+ `CanAccessWorkspaceWebhooksAsync`) |
| `ApiKeyEndpoints.cs` | OrgAdmin api-keys |
| `StorageEndpoints.cs` | presigned URL |
| `IntegrationEndpoints.cs` | provision + scope probe |
| `Endpoints.cs` | root `MapOneEndpoints` composing groups |

Mirrors Commerce/Communications. **Highest endpoint-file ROI.**

### 9.4 Medium impact — Policy and tenant metadata co-location

Options (pick one philosophy):

1. **Keep policies host-owned** (current) — modules only reference string names. Extract policies to `AddLazuarAuthorizationPolicies` for readability.  
2. **Module contributes policies** via `ILazuarModule.AddServices` — risk of duplicate names; need registry.  
3. **Tenant requirements** declared per route group metadata instead of path prefix lists in `TenantSecurityMiddleware` — reduces host path coupling when new modules appear.

Recommendation: (1) now; consider (3) if more modules add non-`/admin` tenant routes.

### 9.5 Medium impact — Align DI edge cases

Checklist (implementation later):

- [ ] Payments: throw if `Default` connection missing.  
- [ ] Payments: add keyed SQL factory if Platform/Dapper stays or move platform auth off Payments.  
- [ ] Messaging: document why separate connection; consider fallback.  
- [ ] Billing: decide PendingModelChanges policy globally.  
- [ ] Ops: either remove empty `UseOpsSubscriptions` from Program or add a comment-only marker interface.  
- [ ] Normalize connection missing exception messages.  
- [ ] Ops roles: consider named policy `OpsUser` vs inline RequireRole for catalog consistency.  
- [ ] Platform auth: evaluate moving to One or host; leave payment-config maps in Payments.

### 9.6 Medium impact — Split remaining large endpoint files

| Target | Suggested split |
|--------|-----------------|
| Billing | `AdminBillingEndpoints` + `PublicBillingEndpoints` |
| Lhdn | `IntegrationDocumentEndpoints` + `AdminLhdnEndpoints` (keys, webhooks, config, cert) |
| Ops | `ChatEndpoints` + `AgentActionEndpoints` |
| Commerce root | move custom-checkout trio into `CustomCheckoutEndpoints.cs` |
| Communications root | move email-config into `EmailConfigEndpoints.cs` |
| Payments | already split three ways (good) |

### 9.7 Lower impact — CORS RequireCors redundancy

Parent `/api/v1` already requires CORS; child `RequireCors()` on One and Payments integration is noise. Keep global `UseCors` + one group-level require; drop duplicates when touching those files.

### 9.8 Lower impact — Health & metrics

Already cleanly isolated as three maps; extract to `MapHealthEndpoints` for Program thinness only.

### 9.9 What **not** to do

- **Do not** push JWT secret validation or cookie names into every module.  
- **Do not** move `ApiKeyAuthenticationMiddleware` into One only while Lhdn legacy keys remain.  
- **Do not** auto-discover modules via full assembly scan of the whole AppDomain without an allowlist (startup cost + accidental registration).  
- **Do not** scatter authorization policy definitions across modules without a uniqueness rule.  
- **Do not** remove migrate-on-boot without an alternative for Neon empty DB / first deploy story (ops docs dependency).

---

## 10. Consistency scorecard

| Area | Score (1–5) | Notes |
|------|-------------|-------|
| Module Add*Module shape | **4** | Clear pattern; few outliers |
| Connection string handling | **3** | Messaging + Payments diverge |
| Keyed SQL factories | **3** | Missing Payments/CRM |
| Event bus + subscriptions | **4** | Strong; Ops empty is fine |
| MediatR registration | **3** | Manual list, CRM special |
| Host Program thinness | **2.5** | Orchestration-fat |
| Endpoint file organization | **3** | Commerce/Comms good; One bad |
| Auth policy centralization | **4** | Good catalog; Ops/Platform inline roles |
| Middleware placement | **4.5** | Sound order |
| Tenant path coupling | **3** | Host path lists |
| Route map in Program | **4.5** | Already thin |
| Testability (`partial Program`) | **4** | Integration tests can use host |

**Overall composition health:** **solid modular monolith with a classic fat composition root** — not a distributed mess, not yet a textbook thin host.

---

## 11. Recommended sequencing (if implementing later)

1. **Extract Program composition helpers** (behavior-preserving, PR-friendly).  
2. **Split One Endpoints** into `Endpoints/` (mirror Commerce).  
3. **Normalize DI outliers** (Payments connection throw; optional SQL factory).  
4. **Introduce `AddAllModules` / migrate registry** single list.  
5. **Optional `ILazuarModule`** if module count grows past ~12 or third-party module loading appears.  
6. **Tenant middleware metadata** only if new non-`/admin` tenant surfaces proliferate.

---

## 12. Quick reference — absolute paths

### Host

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Program.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Middleware/CorrelationIdMiddleware.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/ExecutionContextAccessor.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/EventHandlers/ApiKeyRevokedIntegrationEventHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/EventHandlers/WorkspaceUpdatedIntegrationEventHandler.cs`

### Module Infrastructure DependencyInjection

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/DependencyInjection.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/Infrastructure/DependencyInjection.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/CRM/Infrastructure/DependencyInjection.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/DependencyInjection.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Ops/Infrastructure/DependencyInjection.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/DependencyInjection.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/DependencyInjection.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/DependencyInjection.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/DependencyInjection.cs`

### Endpoints (roots + notable partials)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/Infrastructure/Endpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Endpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/IntegrationEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/PlatformEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Ops/Infrastructure/Endpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/*.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/*.cs`

---

## 13. Appendix — Program.cs responsibility checklist (for future PR checklist)

When shrinking Program, each extraction PR should preserve:

- [ ] `.env` load before configuration builders that read env  
- [ ] Key Vault optional failure soft-fallback  
- [ ] Production JWT secret hard-fail on default  
- [ ] R2 disabled service when endpoint missing (no hard fail)  
- [ ] Middleware order: Exception → Correlation → CORS → AuthN → ApiKey → Tenant → AuthZ  
- [ ] Dual cookie name selection for platform vs app  
- [ ] All 7 named auth policies  
- [ ] MediatR assemblies including CRM Infrastructure  
- [ ] All 9 module Add* + Use* (including empty Ops)  
- [ ] Migrate loop order and PendingModelChanges soft-continue  
- [ ] Host dual API-key revoke + workspace subscriptions  
- [ ] Health three routes unauthenticated  
- [ ] `/api/v1` module maps + `/api/v1/platform` SUPER_ADMIN group  
- [ ] `public partial class Program` for tests  

---

## 14. Appendix — Endpoint count by module (verb maps)

| Module | Approx. verb count | Primary prefixes |
|--------|--------------------|------------------|
| One | ~36 | `/one/*` |
| Commerce (all files) | ~41 | `/admin/commerce/*`, `/public/commerce/*` |
| Communications (all) | ~15 | `/admin/communications/*`, `/public/communications/*` |
| Lhdn | 13 | `/lhdn/*` |
| Billing | 12 | `/admin/billing/*`, `/public/billing/*` |
| Ops | 9 | `/ops/*` |
| Payments (all three files) | 8 | `/webhooks/payments/*`, `/integrations/payments/*`, `/platform/*` |
| Messaging | 2 | `/messaging/*` |
| CRM | 0 | n/a |
| Host health | 3 | `/health*` |
| **Total** | **~139** | |

---

*End of analysis. No application code was modified for this document.*
