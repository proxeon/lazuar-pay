<!-- Source subagent: 019fc650-3513-7032-806d-65c429e0e168 -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# BuildingBlocks & API Host Gap Analysis

**Scope:** `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api`  
**Focus:** `BuildingBlocks/`, `SharedKernel/`, `src/Lazuar.Api/`, and docs `001`–`002` (with related module wiring for evidence).  
**Date context:** 2026-08-03

---

## Inventory

### SharedKernel (almost empty)

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/SharedKernel/SharedKernel.csproj` | References `BuildingBlocks.Domain` only |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/SharedKernel/SharedKernelMarker.cs` | Empty marker type for assembly scanning |

**References:** All module `*.Domain` projects reference SharedKernel (`One`, `Messaging`, `CRM`, `Payments`, `Ops`, `Billing`, `Lhdn`, `Commerce`, `Communications`).  
**Usage:** No C# file in the solution `using SharedKernel` except the marker itself. SharedKernel is a **placeholder shell**.

Doc intent (`002`, README): IDs, audit markers, pure global value types. **None of that exists yet.**

---

### BuildingBlocks.Domain

| File | Purpose |
|------|---------|
| `Entity.cs` | Domain-event collection + `CheckRule` |
| `ValueObject.cs` | Structural equality |
| `IAggregateRoot.cs` | Marker |
| `IBusinessRule.cs` / `GenericBusinessRule.cs` / `BusinessRuleValidationException.cs` | Invariant pattern |
| `IDomainEvent.cs` | Extends MediatR `INotification` |
| `IMustHaveTenant.cs` | `Guid OrganizationId { get; set; }` + global filter hook |

**Package dep:** MediatR on Domain (violates README claim “Dependency: None (Pure C#)”).

---

### BuildingBlocks.Application

| File | Purpose |
|------|---------|
| `CQRS.cs` | `ICommand` / `IQuery` / handlers + **`IPasswordService`** |
| `IEventBus.cs` | `IIntegrationEvent`, `IIntegrationEventHandler<T>`, `IEventBus` |
| `IEventBusSubscriptions.cs` | Subscribe API |
| `IExecutionContextAccessor.cs` | Tenant/User/Role/SystemAdmin/TestMode/AuditSignature |
| `IEmailService.cs` | Email port (BYOK tenant + platform) |
| `IMessagingService.cs` | SMS/WhatsApp-ish port |
| `ISqlConnectionFactory.cs` | Dapper connection factory |
| `ITokenGeneratorService.cs` | Secure tokens + SHA256 hash |
| `IMagicLinkTokenService.cs` | HMAC magic links (`subscriptionId`-shaped API) |
| `PaginatedResponse.cs` | Paged envelope |
| `EmailTemplateBuilder.cs` | Brand HTML wrapper (“Powered by Lazuar”) |
| `MarkdownParser.cs` | Markdig HTML/plain |
| `AgentToolAttribute.cs` | Ops tool metadata |
| `Llm/IChatClientFactory.cs` | OpenAI.Chat client factory |
| `Llm/ILlmTitleGenerator.cs` | Conversation title gen |
| `Llm/IAgentPromptProvider.cs` | Per-app system prompt rules |

**Deps:** Domain, MediatR, Markdig, OpenAI.

---

### BuildingBlocks.Infrastructure

| File | Purpose |
|------|---------|
| `PlatformDbContext.cs` | Tenant query filter, auto-assign `OrganizationId`, recursive domain-event MediatR publish, `DatabaseJobTrigger` |
| `OutboxMessage.cs` / `InboxMessage.cs` | Messaging persistence models |
| `OutboxEventBus<TDbContext>.cs` | Keyed `IEventBus` → insert outbox row |
| `OutboxPublisherJob<T>.cs` | SKIP LOCKED poll → `InMemoryEventBus` |
| `InboxConsumerJob<T>.cs` | SKIP LOCKED poll → MediatR `Publish` |
| `InMemoryEventBus.cs` | In-process fan-out + subscription registry |
| `DatabaseJobTrigger.cs` | Wake workers after `SaveChanges` |
| `TypeResolver.cs` | Type name resolution for serialized events |
| `NpgsqlConnectionFactory.cs` | `ISqlConnectionFactory` impl |
| `PasswordService.cs` | BCrypt (work factor from config) |
| `JwtService.cs` | **`IJwtService` defined here** (not Application) |
| `TokenGeneratorService.cs` | Crypto token gen/hash |
| `MagicLinkTokenService.cs` | HMAC using `Jwt:Secret` |
| `ResendEmailService.cs` / `ConsoleEmailService.cs` | Email adapters |
| `ConsoleMessagingService.cs` | Log-only messaging |
| `R2StorageService.cs` | **`IR2StorageService` + Disabled + S3/R2** (interface in Infrastructure) |
| `GlobalExceptionHandler.cs` | 400 for validation / 500 otherwise |
| `Configuration/ResendOptions.cs` | Resend options |
| `Llm/*` | Chat factory, title gen, OpenRouter/provider policies, `AddThinLlmFactory` |

**Missing vs docs:** No centralized `AddBuildingBlocks(...)` composition extension; host wires pieces ad hoc in `Program.cs`.

---

### Lazuar.Api (composition root)

| Path | Role |
|------|------|
| `Program.cs` | Env/KeyVault, Serilog, auth, CORS, MediatR assembly scan, all modules, migrate-on-boot, middleware, subscriptions, endpoints |
| `ExecutionContextAccessor.cs` | `IExecutionContextAccessor` from `HttpContext` |
| `Middleware/ApiKeyAuthenticationMiddleware.cs` | `sk_live_` / `sk_test_` → Lhdn `DeveloperApiKeys` via keyed SQL factory |
| `Middleware/TenantSecurityMiddleware.cs` | Tenant resolve + membership role injection |
| `Configuration/AppOptions.cs` | ClientUrl / ApiBaseUrl / CorsOrigins (**never bound in DI**) |
| `EventHandlers/*` | Cache eviction for API keys / workspace updates |
| `Infrastructure/Data/PlatformDbContext.cs` | **Dead duplicate** of BB PlatformDbContext (no modules inherit it) |
| `appsettings*.json` | Conn strings, Jwt, Resend, Ai, Credits, secrets present in repo |

---

### Module outbox/inbox worker matrix

| Module | Schema | OutboxEventBus | Outbox job | Inbox job | Writes to Inbox? |
|--------|--------|----------------|------------|-----------|------------------|
| One | `one` | Yes | Yes | Yes | No (handlers sync on bus) |
| Messaging | `messaging` | Yes | Yes | Yes | **Yes** (only module) |
| CRM | `crm` | Yes | **No** | **No** | No |
| Payments | `payments` | Yes | Yes | Yes | No |
| Ops | `ops` | Yes | Yes | Yes | No |
| Billing | `billing` | Yes | Yes | Yes | No |
| **Lhdn** | `lhdn` | Yes | **No** | **No** | No |
| Commerce | `commerce` | Yes | Yes | Yes | No |
| Communications | `communications` | Yes | Yes | Yes | No |

**Critical:** Lhdn publishes via `LhdnEventBus` (`ApiKeyRevoked`, `LhdnDocumentSubmitted`, `LhdnDocumentValidated`, `LhdnDocumentCancelled`, etc.) into `lhdn.OutboxMessages` **with no publisher job** → those integration events never leave the module. This breaks host cache eviction for revoked API keys and Billing credit side-effects from Lhdn.

---

## Cross-Cutting Concerns

### Auth

**What exists**
- JWT Bearer + dual cookies (`lazuar_auth` / `lazuar_admin_auth` for `/api/v1/platform`).
- `IJwtService` / `JwtService` (interface lives in Infrastructure).
- `IPasswordService` / BCrypt `PasswordService`.
- API key middleware (Lhdn developer keys, test vs live mode claims).
- Authorization policy `OrgAdmin` (roles SUPER_ADMIN, ADMIN, API_CLIENT).
- Platform group requires SUPER_ADMIN.

**Gaps**
1. **No upgrade-on-login for legacy hashes** despite doc `008` — `PasswordService` is BCrypt-only; no `LEGACY_*` path in code.
2. **API key auth hard-coupled to Lhdn schema** (`lhdn."DeveloperApiKeys"` + keyed `"LhdnSqlConnectionFactory"`) inside host middleware → host depends on module data layout; not a BuildingBlocks abstraction.
3. **API key cache TTL 5 minutes**; eviction relies on `ApiKeyRevokedIntegrationEvent` which **cannot fire** without Lhdn outbox publisher.
4. **`IJwtService` not in Application** — Application cannot depend on JWT abstraction without referencing Infrastructure (only works because endpoints live in Infra/host).
5. **Magic link secret reuses JWT secret**; interface names `subscriptionId` (Commerce-shaped), not domain-agnostic.
6. **No centralized auth options type** (`JwtOptions`); raw `IConfiguration["Jwt:…"]` with insecure defaults in code and appsettings.
7. **No refresh tokens / security stamp invalidation** on JWT (claims-only lifetime).
8. **TenantSecurityMiddleware** injects role after JWT auth but does not re-validate JWT `TenantId` claim against `X-Tenant-Id` (header-driven tenant for cookie users).

### Tenancy

**What exists**
- `IMustHaveTenant` + EF global filter:  
  `TenantId == Guid.Empty || OrganizationId == TenantId`  
  → **empty tenant = bypass isolation**.
- Auto-assign `OrganizationId` on insert when empty and context has tenant.
- Host middleware resolves tenant via `X-Tenant-Id`, `X-Tenant-Slug`, route `tenantSlug`; platform path forces system tenant `…0001`.
- API key path sets `Items["TenantId"]` and skips membership checks.
- Widespread `.IgnoreQueryFilters()` in repos/handlers (expected for workers/event handlers; risk if overused on HTTP paths).

**Gaps**
1. **Doc `002` domain-blind rule broken:** BuildingBlocks owns `IMustHaveTenant` and `OrganizationId` — explicitly business vocabulary.
2. **No Ambient/system execution context for background jobs** — workers use empty `TenantId` (filter off) or `IgnoreQueryFilters`; no first-class `IExecutionContextAccessor` system mode (e.g. `RunAsSystem` / `RunAsTenant`).
3. **Duplicate dead PlatformDbContext** in `Lazuar.Api.Infrastructure.Data` (reflection-based audit fields, no domain events / no outbox trigger) — confusion risk.
4. **No shared “tenant-aware Dapper” helper** — SQL factories are raw; callers must remember to filter by org.
5. **Architecture tests** do not enforce tenancy / SharedKernel / BuildingBlocks boundary rules claimed in README.

### Outbox / event bus

**What exists**
- Transactional outbox pattern via `OutboxEventBus<TDbContext>`.
- In-process bus (`InMemoryEventBus`) + per-module keyed buses.
- Domain events: dispatched **in-process via MediatR before `SaveChanges`** (not written to outbox by PlatformDbContext).
- Integration events: handlers usually run **synchronously inside OutboxPublisherJob** after publish to in-memory bus.
- Messaging is the only module that **enqueues inbox** then processes via `InboxConsumerJob` → MediatR handlers.

**Gaps (major architecture drift vs docs `001` / README)**
1. **README false claim:** PlatformDbContext does **not** serialize domain events into `OutboxMessages`; it MediatR-publishes them pre-commit. Domain-event side effects (email, integration outbox writes) run **before** DB commit → dual-write / partial failure risk if later persist fails.
2. **Inbox pattern largely unused** — only Messaging writes `InboxMessage`. Other modules register inbox jobs that effectively no-op.
3. **Lhdn + CRM missing publisher/consumer jobs** despite tables + `OutboxEventBus` registration.
4. **Poison messages permanently marked processed** (`finally { ProcessedAt = UtcNow }`) — no retry/backoff/DLQ/admin replay.
5. **No handler isolation for failures** — InMemoryEventBus sequential await; one handler throw is caught per-message in outbox job, but multi-handler fan-out: if first handler succeeds and second fails, **no partial-success tracking**.
6. **No multi-instance safety beyond SKIP LOCKED** for process-local bus — correct for monolith single process; **not ready for multi-replica** (each replica would process own outbox; OK) but **in-memory bus is per-process** (fine). Scaling out is OK for outbox; **not** for shared in-memory subscriptions across processes (each process has its own).
7. **TypeResolver uses AssemblyQualifiedName** — fragile across assembly version upgrades.
8. **No shared base for “inbox write then ack” handlers** — Messaging duplicates three nearly identical handlers.

### Email

**What exists**
- `IEmailService` with platform Resend + tenant BYOK + org tags.
- `ResendEmailService` refuses platform fallback for tenant emails without BYOK.
- `EmailTemplateBuilder` branding.
- Communications tenant email config + Messaging dispatch path.
- Console fallback when platform key missing (system tenant only).

**Gaps**
1. **No real SMS/WhatsApp provider** — `ConsoleMessagingService` only; production WhatsApp path logs only.
2. **Resend HTTP client:** Named client sets platform Bearer at factory time; per-send overwrites Authorization — OK, but **no resilience** (retry/circuit breaker) in BuildingBlocks.
3. **No email outbox/retry** for provider failures — throw bubbles; depends on caller.
4. **Secrets in committed `appsettings.json`** (Resend API key, OpenRouter key appear present) — security gap.
5. **`Resend:WebhookSecret`** used in Communications public endpoints but not modeled beyond options partial.
6. **Brand copy hardcoded** in BuildingBlocks (`Powered by Lazuar`) — product coupling in “domain-blind” layer.

### LLM

**What exists**
- Thin factory: OpenAI / OpenRouter / DeepSeek / MiMo endpoints + quirks policies.
- Title generator with fallbacks.
- `IAgentPromptProvider` registered from Billing; Ops orchestrator consumes providers + tools.
- `AgentToolAttribute` for tool discovery.

**Gaps**
1. **No rate limiting / cost tracking / tenant quota in BuildingBlocks** — Billing credits exist for WhatsApp/Lhdn, not for LLM tokens.
2. **No streaming abstraction** at BuildingBlocks level (Ops implements own orchestration).
3. **`IChatClientFactory` returns OpenAI SDK `ChatClient`** — Application layer coupled to OpenAI package (leaky abstraction).
4. **No options class for `Ai:*`** — raw config; hard to validate at startup.
5. **Secrets/defaults** in appsettings for Ai provider keys.

### Storage

**What exists**
- Optional R2/S3 boot; disabled no-op service.
- Presigned URL endpoint in One module.

**Gaps**
1. **`IR2StorageService` in Infrastructure** — Application cannot depend without infra reference.
2. **Config split:** Program uses `R2_ENDPOINT` / `R2_ACCESS_KEY` env-style keys; Development appsettings has nested `R2` section unused by Program.
3. **No virus scan / content-type validation / size limits** in BuildingBlocks.

### Other cross-cuts missing entirely from shared infra

- Correlation / request ID middleware  
- Rate limiting (module tests mention Lhdn rate limiting domain logic; no host ASP.NET rate limiter)  
- OpenTelemetry / metrics / structured request logging beyond Serilog console  
- FluentValidation pipeline behavior (package in Directory.Packages.props but **not wired** in BuildingBlocks)  
- Health checks beyond bare `/health` (no DB/deps readiness)  
- Feature flags  
- Clock abstraction (`IClock`) for testability  
- Unit-of-work / transaction scope helper across multiple DbContexts  

---

## Middleware Pipeline

Actual order in `Program.cs`:

1. **Startup:** `.env` file load → env vars → optional Azure Key Vault  
2. **Boot:** EF `MigrateAsync` for **all 9 module DbContexts** (blocks readiness; no health-gated migration mode)  
3. `UseExceptionHandler()` → `GlobalExceptionHandler`  
4. `UseCors()`  
5. `UseAuthentication()` (JWT + cookie token extraction)  
6. **`ApiKeyAuthenticationMiddleware`** (may replace `context.User` with API_CLIENT principal)  
7. **`TenantSecurityMiddleware`** (tenant resolve / membership; skips for ApiKey and platform)  
8. `UseAuthorization()`  
9. Module `Use*Subscriptions()` (event bus subscribe)  
10. Host event bus subscriptions (API key revoked, workspace updated)  
11. Endpoints: `/health`, `/api/v1/*` modules, `/api/v1/platform` SUPER_ADMIN  

**Pipeline gaps**
| Gap | Impact |
|-----|--------|
| No HTTPS redirection / HSTS in Program | Depends on reverse proxy |
| No request logging middleware (Serilog request enrichment) | Weak observability |
| No correlation ID | Hard to trace multi-module outbox chains |
| Exception handler leaks `exception.Message` on 500 | Info disclosure |
| API key middleware before tenant middleware, but API keys skip tenant security entirely | Correct for keys; no entitlement checks (app-level) at host |
| Migrations at boot | Long startup; multi-instance race (EF migrate concurrent) |
| MediatR registration in host lists every Application + Infrastructure assembly manually | Easy to forget new module; CRM Application missing from list (CRM has no Application project) |

---

## Configuration & Options

### Bound / used

| Key | Where |
|-----|--------|
| `Resend` → `ResendOptions` | Program `BindConfiguration` |
| `PLATFORM_ADMIN_EMAILS` / `PLATFORM_ADMIN_PASSWORD` → `PlatformAdminSettings` | Program manual Configure |
| `ConnectionStrings:Default` / `MessagingConnection` | Module DI |
| `Jwt:*` | Program JWT, MagicLink, various endpoints (stringly) |
| `Security:PasswordWorkFactor` | PasswordService |
| `Ai:Provider`, `Ai:Model`, `Ai:ProviderKeys:*` | ChatClientFactory |
| `OpenRouter:SiteUrl/SiteName` | Optional headers |
| `Credits` → `CreditCostOptions` | Billing module |
| `R2_ENDPOINT`, `R2_ACCESS_KEY`, `R2_SECRET_KEY`, `R2_BUCKET_NAME` | Program / endpoints |
| `App:CorsOrigins` | CORS |
| Azure Key Vault | Optional |

### Options types present but incomplete

| Type | Location | Status |
|------|----------|--------|
| `AppOptions` | `Lazuar.Api.Configuration` | **Never registered / never `IOptions<AppOptions>`** |
| `ResendOptions` | BuildingBlocks | Bound; WebhookSecret not on options class but used as `Resend:WebhookSecret` |
| `PlatformAdminSettings` | One.Infrastructure | Host configures; module-owned type |

### Config gaps
1. **No `AddBuildingBlocksInfrastructure(IConfiguration)`** to standardize options validation (`ValidateOnStart`).  
2. **Inconsistent R2 config models** (env flat keys vs appsettings nested `R2`).  
3. **Committed secrets** in `appsettings.json` (Jwt default, Resend, OpenRouter, ngrok ApiBaseUrl).  
4. **Duplicate connection string names** (`TenantConnection` appears unused by current DI).  
5. **No environment-specific secret discipline** documented in BuildingBlocks (relies on KeyVault optional).  

---

## DI Composition

### Host-registered (Program.cs)

| Service | Lifetime |
|---------|----------|
| `IExecutionContextAccessor` → `ExecutionContextAccessor` | Scoped |
| `DatabaseJobTrigger` | Singleton |
| `IPasswordService` | Singleton |
| `IJwtService` | Singleton |
| `IMessagingService` → Console | Singleton |
| `IEmailService` → Resend | Singleton |
| `IMagicLinkTokenService` | Singleton |
| `IChatClientFactory` / `ILlmTitleGenerator` via `AddThinLlmFactory` | Singleton / Scoped |
| `InMemoryEventBus` + `IEventBusSubscriptions` | Singleton |
| `IR2StorageService` | Singleton (real or disabled) |
| HttpClient `"Resend"` | Named |
| MemoryCache, HttpContextAccessor | — |
| Auth, CORS, ExceptionHandler, MediatR | — |
| All `Add*Module` | — |
| Host event handlers | Transient |

### Module-registered patterns
- Each module: `AddDbContext`, keyed `OutboxEventBus`, often keyed `ISqlConnectionFactory`, hosted workers, transient integration handlers, `Use*Subscriptions`.
- **`ITokenGeneratorService` registered only in One module** as Singleton; host middleware depends on it → **order coupling** (`AddOneModule` must run; currently does). Lhdn command handlers also use it without registering locally.

### DI gaps
1. **No BuildingBlocks composition root** — registration scatter makes optional adapters (Console vs Resend email) host-only decisions.  
2. **Interface location inconsistency:**  
   - Application: `IPasswordService`, `IEmailService`, …  
   - Infrastructure: `IJwtService`, `IR2StorageService`  
3. **Missing Lhdn/CRM hosted jobs** (see matrix).  
4. **CRM has OutboxEventBus but nothing publishes / no jobs** — dead registration.  
5. **Ops `UseOpsSubscriptions` is empty** — no cross-module events.  
6. **Double registration risk** for `ITokenGeneratorService` if host later adds it.  
7. **MediatR scans Infrastructure** (handlers living in Infra) — works but blurs Application vs Infrastructure boundaries.  
8. **No keyed or ambient system `IExecutionContextAccessor` for workers.**  

---

## Gaps in Shared Infrastructure for Integration APIs

Targeted at **integration-style APIs** (developer API keys, webhooks, outbox-driven side effects, multi-tenant external calls) — especially Lhdn + Payments + host auth.

### P0 — Broken / correctness

1. **Lhdn outbox never published**  
   - Events required by host (`ApiKeyRevokedIntegrationEvent`) and Billing (`LhdnDocument*`) stuck in DB.  
   - **Fix:** Add `LhdnOutboxPublisherJob` / `LhdnInboxConsumerJob` (or stop using outbox and use a defined alternative — currently inconsistent).

2. **Domain events before commit**  
   - Side effects (integration outbox insert, emails) can run even if `SaveChanges` fails later, or leave partial state.  
   - Docs claim outbox serialization of domain events — **implementation differs**.

3. **Inbox policy violated**  
   - Doc `001`: receive → write inbox → ack → async process.  
   - Reality: most modules execute business mutations **inline on InMemoryEventBus** during outbox publish (long transactions, failure coupling, no per-consumer isolation).

4. **API key revocation path broken end-to-end** without Lhdn outbox job + host handler.

### P1 — Integration API platform primitives missing in BuildingBlocks

| Primitive | Today | Needed for integration APIs |
|-----------|-------|-----------------------------|
| Idempotency middleware | Per-module tables (Lhdn, Billing, Payments webhooks) | Shared `IIdempotencyStore` + HTTP middleware for `Idempotency-Key` |
| Webhook signature verification | Scattered (Payments gateways, Resend webhook secret) | Shared HMAC/timestamp verifier helpers |
| Outbound HTTP resilience | Ad-hoc HttpClients | Shared Polly/HttpClient defaults, timeouts, retry budgets |
| API key abstraction | Hardcoded Lhdn SQL in host middleware | Port in Application + host adapter; multi-module keys or platform keys table |
| Sandbox/test mode | Claim `IsTestMode` on API keys | Consistent filter/enforcement in PlatformDbContext or explicit convention |
| Rate limiting | Not in host | Shared rate limiter policies for public/webhook/API key routes |
| Request correlation | None | Middleware + log scope + outbox message correlation id |
| ProblemDetails standardization | Partial GlobalExceptionHandler | Map domain exceptions, 401/403/404/409 consistently; hide 500 details |
| Clock / Guid generators | Static `DateTime.UtcNow` / `Guid.CreateVersion7` | Injectable for tests |
| Multi-DbContext transactions | None | Documented pattern or reject (currently cross-module only via events) |

### P2 — SharedKernel emptiness vs intended use

For integration contracts across modules, teams currently:
- Put shared shapes in `*.Contracts` (correct), or  
- Leak vocabulary into BuildingBlocks (`IMustHaveTenant`, magic-link subscriptionId), or  
- Duplicate Guids/string statuses.

**SharedKernel should hold** (per docs, still missing):
- Strongly typed IDs if desired (`OrganizationId`, `UserId` readonly structs)  
- Shared money/currency VO if cross-module  
- Audit markers (`IHasAuditSignature`) currently half-implemented only in **dead** host PlatformDbContext  

### P3 — Operational integration readiness

- Single-process in-memory bus is OK for modular monolith; **no dead-letter UI/API** for failed outbox/inbox rows.  
- Jobs always mark processed → **silent data loss** for integration consumers.  
- No metrics: outbox lag, inbox depth, handler failures.  
- Boot migrations for all schemas — integration test / deploy friction.  

---

## Recommendations

### Immediate (fix production correctness)

1. **Register Lhdn outbox (and optionally inbox) hosted services** mirroring other modules; verify `ApiKeyRevoked` cache eviction and Billing Lhdn handlers fire.  
2. **Decide and document the real messaging model:**  
   - **Option A (current de-facto):** Outbox → InMemory bus → sync handlers (drop unused inbox jobs / update docs).  
   - **Option B (doc `001`):** Outbox → bus → **inbox write only** → InboxConsumer → handlers; enforce with architecture tests.  
3. **Move domain-event dispatch after successful SaveChanges**, or write domain→integration mappings only via outbox inside the same transaction **after** entities are about to save (standard transactional outbox).  
4. **Stop marking poison messages processed without retry policy** — add `Attempts`, exponential backoff, max attempts, `DeadLetteredAt`.  
5. **Rotate/remove secrets from `appsettings.json`**; use User Secrets / Key Vault only.

### BuildingBlocks hardening

6. Add **`BuildingBlocks.Infrastructure.DependencyInjection.AddBuildingBlocks(...)`** registering email/messaging/jwt/password/token/R2/LLM/options with environment switches.  
7. Move **`IJwtService`, `IR2StorageService`** to Application (or thin Abstractions project).  
8. Introduce **`JwtOptions`, `AiOptions`, `R2Options`, `AppOptions`** with `ValidateOnStart`.  
9. Replace empty-tenant filter bypass with explicit **`IExecutionContextAccessor.IsAvailable` / `BypassTenantFilter`** used only by workers (force `IgnoreQueryFilters` consciously).  
10. Extract **API key authentication** into a BuildingBlocks middleware/service with a port `IApiKeyValidator` implemented by Lhdn (or platform module).  
11. Add **correlation ID + Serilog enricher + ProblemDetails factory**.  
12. Add **idempotency and webhook crypto helpers** for integration endpoints.  
13. Consider **removing MediatR dependency from Domain** (`IDomainEvent` as plain marker; adapter in Infrastructure maps to `INotification`) to restore pure Domain.  
14. Revisit **`IMustHaveTenant`**: keep in BuildingBlocks as technical multi-tenant infrastructure **or** rename to `IHasTenantKey` / move to SharedKernel with documented exception to “domain-blind” rule.

### SharedKernel fill-out (only when needed)

15. Do **not** dump entities into SharedKernel.  
16. Add only cross-module primitives if duplication appears (e.g. `TenantId` VO, `Money`, status enums used in multiple contracts).  
17. Add architecture test: SharedKernel must not reference Application/Infrastructure/Modules.

### Host composition

18. Delete or merge **dead** `Lazuar.Api.Infrastructure.Data.PlatformDbContext`.  
19. Bind **`AppOptions`**; stop scattering `App:ClientUrl` reads if any.  
20. Split **liveness vs readiness** (`/health/live`, `/health/ready` with DB check).  
21. Consider **not migrating all schemas on every boot** in multi-instance deploys (init job / migrate once).  
22. Align **R2 configuration** keys.  
23. Implement **doc 008 upgrade-on-login** in `PasswordService` or a dedicated authenticator.  
24. Wire **FluentValidation** behaviors or remove unused package noise.  
25. Expand **architecture tests** for: Domain pure deps, BuildingBlocks no module refs, SharedKernel emptiness rules, every module with OutboxEventBus has publisher job.

### Docs alignment

26. Fix README §3 PlatformDbContext description (domain events → MediatR, not outbox).  
27. Fix doc diagram naming (`Tenant`/`Community` vs actual `One`/`Commerce`).  
28. Document **de-facto sync handler model** vs aspirational inbox model to stop new modules copying the wrong pattern.

---

## File-by-File Notes

### SharedKernel

| File | Notes |
|------|-------|
| `SharedKernel.csproj` | References Domain only; no packages. All Domain projects reference this but gain nothing. |
| `SharedKernelMarker.cs` | Placeholder; no assembly scanning found that uses it. |

### BuildingBlocks.Domain

| File | Notes |
|------|-------|
| `Entity.cs` | Solid minimal aggregate base. No concurrency token helpers. |
| `ValueObject.cs` | Classic; null components in hash use 0 (collision risk). |
| `IAggregateRoot.cs` | Empty marker — fine. |
| `IBusinessRule.cs` | Fine. |
| `GenericBusinessRule.cs` | Always broken when constructed — odd API (`new GenericBusinessRule(msg)` then CheckRule). |
| `BusinessRuleValidationException.cs` | Mapped to HTTP 400 in GlobalExceptionHandler. |
| `IDomainEvent.cs` | **MediatR leak into Domain.** |
| `IMustHaveTenant.cs` | **Business keyword + mutable set** for EF; filter bypass when TenantId empty. |

### BuildingBlocks.Application

| File | Notes |
|------|-------|
| `CQRS.cs` | Thin MediatR aliases; `IPasswordService` colocated (OK for ports). No validation pipeline. |
| `IEventBus.cs` | Integration event = `INotification` (enables inbox→MediatR). Handler interface separate from MediatR (good for bus). |
| `IEventBusSubscriptions.cs` | Host/module startup subscription. |
| `IExecutionContextAccessor.cs` | HTTP-centric concepts (TestMode, SystemAdmin) without system/worker mode. |
| `IEmailService.cs` | Strong multi-tenant email port. |
| `IMessagingService.cs` | Minimal; no media/templates/status callbacks. |
| `ISqlConnectionFactory.cs` | Fine for Dapper. |
| `ITokenGeneratorService.cs` | Good for API keys. |
| `IMagicLinkTokenService.cs` | Parameter name `subscriptionId` couples to Commerce. |
| `PaginatedResponse.cs` | No `PageSize` field; total pages calc risks divide-by-zero if limit 0. |
| `EmailTemplateBuilder.cs` | Brand-hardcoded; newlines only → HTML. |
| `MarkdownParser.cs` | Fine utility. |
| `AgentToolAttribute.cs` | Ops-oriented but domain-agnostic enough. |
| `Llm/IChatClientFactory.cs` | OpenAI types in Application surface. |
| `Llm/ILlmTitleGenerator.cs` | Fine. |
| `Llm/IAgentPromptProvider.cs` | Fine extension point. |
| `BuildingBlocks.Application.csproj` | Markdig + OpenAI pull heavy deps into all Application consumers. |

### BuildingBlocks.Infrastructure

| File | Notes |
|------|-------|
| `PlatformDbContext.cs` | Core tenancy + domain events + job trigger. Domain events pre-save. Filter bypass on empty tenant. |
| `OutboxMessage.cs` / `InboxMessage.cs` | No Attempts/Headers/CorrelationId. |
| `OutboxEventBus.cs` | Does not call `SaveChanges` (correct — caller transaction). Uses AssemblyQualifiedName. |
| `OutboxPublisherJob.cs` | SKIP LOCKED batch 20; always marks processed; uses process-local InMemoryEventBus. |
| `InboxConsumerJob.cs` | Same poison handling; MediatR publish requires handlers as `INotificationHandler`. |
| `InMemoryEventBus.cs` | Reflection `HandleAsync`; scoped per publish; no parallelism; logs missing handlers. |
| `DatabaseJobTrigger.cs` | Efficient wake-up; single global trigger for all modules (noisy but OK). |
| `TypeResolver.cs` | Cache + fallback; version-sensitive. |
| `NpgsqlConnectionFactory.cs` | Opens new connection per create — callers must dispose (typical). |
| `PasswordService.cs` | No legacy verify path (doc 008 unimplemented). |
| `JwtService.cs` | Interface should move up; no refresh/jti. |
| `TokenGeneratorService.cs` | URL-safe base64-ish; SHA256 hex hash. |
| `MagicLinkTokenService.cs` | 24h expiry; JWT secret reuse; no audience binding. |
| `ResendEmailService.cs` | Solid BYOK rules; mutates DefaultRequestHeaders on shared client (thread-safety subtlety). |
| `ConsoleEmailService.cs` | Dev-friendly; not selected in Program (Resend always). |
| `ConsoleMessagingService.cs` | Production gap for WhatsApp. |
| `R2StorageService.cs` | Interface + disabled + real; good boot resilience. |
| `GlobalExceptionHandler.cs` | Maps only two exception types; 500 returns Detail=message; no trace id. |
| `Configuration/ResendOptions.cs` | Incomplete vs webhook secret usage. |
| `Llm/ChatClientFactory.cs` | Multi-provider; throws if key missing. |
| `Llm/LlmTitleGenerator.cs` | Defensive fallbacks. |
| `Llm/OpenRouterHeaderPolicy.cs` / `ProviderQuirksPolicy.cs` | Provider-specific body rewrite (stream rewrite risks Content-Length — partially guarded). |
| `Llm/LlmDependencyInjection.cs` | Only thin factory registration. |
| `BuildingBlocks.Infrastructure.csproj` | Broad package set appropriate for adapters. |

### Lazuar.Api host

| File | Notes |
|------|-------|
| `Program.cs` | God composition root; migrate-all; manual MediatR assembly list; R2 optional; module endpoint map; CRM has no HTTP endpoints mapped (contracts only — intentional?). |
| `ExecutionContextAccessor.cs` | Empty tenant/user when no HTTP (workers); AuditSignature supports agent flag via Items. |
| `Middleware/ApiKeyAuthenticationMiddleware.cs` | Module-specific SQL; cache index by tenant for bulk eviction; depends on One’s token service registration. |
| `Middleware/TenantSecurityMiddleware.cs` | Membership role add; admin routes require tenant header; ApiKey short-circuit; platform system tenant. |
| `Configuration/AppOptions.cs` | Defined, unused in DI. |
| `EventHandlers/ApiKeyRevokedIntegrationEventHandler.cs` | Correct idea; blocked by Lhdn outbox gap. |
| `EventHandlers/WorkspaceUpdatedIntegrationEventHandler.cs` | Evicts all cached API keys for org. |
| `Infrastructure/Data/PlatformDbContext.cs` | **Orphan/dead code**; reflection audit for RecordedBy/ActorId not in BB version. |
| `appsettings.json` | **Contains live-looking secrets**; Credits and Ai config; Jwt weak default. |
| `appsettings.Development.json` | Nested R2 unused by Program; Lhdn reference path. |
| `Lazuar.Api.csproj` | References module Infrastructure projects only (+ two Application); no direct BuildingBlocks ref (transitive). |

### Docs (architecture intent vs code)

| Doc | Alignment |
|-----|-----------|
| `001-cross-module-communication.md` | Intent clear; **inbox-backed default not practiced** except Messaging; examples reference Community module (renamed Commerce). |
| `002-shared-kernel-vs-building-blocks.md` | Boundaries clear; **SharedKernel empty**; **IMustHaveTenant violates domain-blind rule**; Domain MediatR violates “no deps”. |
| `003`–`006`, `008` | Migration playbooks; **008 not implemented in PasswordService**. |
| README BuildingBlocks section | **Incorrect** claim that PlatformDbContext writes domain events to outbox. |

### Representative module wiring (evidence)

| File | Notes |
|------|-------|
| `Modules/Lhdn/Infrastructure/DependencyInjection.cs` | EventBus + domain jobs; **no outbox/inbox jobs**. |
| `Modules/CRM/Infrastructure/DependencyInjection.cs` | EventBus; **no jobs**; sync profile update handler. |
| `Modules/Messaging/.../TenantProvisionedIntegrationEventHandler.cs` | Correct inbox write pattern. |
| `Modules/Billing/.../GatewayPaymentCompletedHandler.cs` | Sync business work on bus (no inbox). |
| `Modules/One/.../OrganizationCreatedDomainEventHandler.cs` | Domain event → OneEventBus integration event inside pre-save MediatR dispatch. |
| `Modules/Lhdn/.../RevokeApiKeyCommand.cs` | Publishes revoke via LhdnEventBus then SaveChanges — **stuck without publisher**. |

---

## Summary Scorecard

| Area | Status |
|------|--------|
| Modular monolith module isolation (csproj + NetArchTest) | **Good baseline** |
| BuildingBlocks technical adapters | **Broad but uneven** |
| SharedKernel | **Placeholder only** |
| Outbox publish path (most modules) | **Works** |
| Outbox publish path (Lhdn) | **Broken (P0)** |
| Inbox isolation pattern | **Mostly unused / docs drift** |
| Domain event transactional safety | **Weak (pre-SaveChanges)** |
| Host auth + tenancy middleware | **Functional with coupling** |
| Integration-API shared primitives | **Large gaps** |
| Config/secrets hygiene | **Poor in repo** |
| Doc ↔ code fidelity | **Material mismatches** |

---

### Highest-leverage next steps (ordered)

1. Ship **Lhdn OutboxPublisherJob** (and tests that revoke key clears cache and Lhdn events reach Billing).  
2. **Reconcile docs + code** for outbox/inbox and PlatformDbContext domain-event behavior.  
3. **Poison-message retry/DLQ** on outbox/inbox jobs.  
4. **`AddBuildingBlocks` + options validation**; move auth/storage ports to Application.  
5. **API key port** out of host middleware hardcoding.  
6. **Either implement or delete** SharedKernel references / fill with true shared primitives.  
7. **Architecture tests** for outbox job completeness and foundation boundaries.
