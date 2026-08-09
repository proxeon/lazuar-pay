# 004-maintenance / 02 — Large files & god-file chunking (BACKEND + TYPESPEC)

**Status:** Analysis only — **do not implement from this file alone**  
**Date:** 2026-08-09  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Scope:**
- `apps/lazuar-api` — hand-maintained `*.cs` (exclude `bin/`, `obj/`)
- `packages/api-spec` — hand-maintained `*.tsp`
- Generated clients (`packages/api-types-dotnet`, `packages/api-types-ts`) — inventory only; **do not hand-chunk**

**Goal:** Identify files that are too large and/or multi-responsibility (“god files”), propose idiomatic splits that match existing Lazuar modular-monolith conventions, rank by priority, and document risk.

**Thresholds used:**
| Band | Lines | Treatment |
|------|------:|-----------|
| Soft | ~300–400 | Review if multi-responsibility |
| Hard | >400 | Strong chunking candidate unless single cohesive unit |
| Extreme | >600 | P0/P1 unless generated or pure EF snapshot |

**Line-count method:** End-of-file line numbers via file reads (equivalent to `wc -l` for these sources). Migrations `*ModelSnapshot.cs` / `*Designer.cs` are large by nature and **excluded from hand-split proposals** (EF regenerates them).

**Existing idiomatic patterns already in-repo (use these as templates):**
1. **Commerce endpoints composition** — `Modules/Commerce/Infrastructure/Endpoints.cs` (thin orchestrator ~80 lines) + `Endpoints/*.cs` partial maps (`ProductEndpoints`, `SubscriberEndpoints`, `PublicEndpoints`, …).
2. **Commerce query partials** — `CommerceQueryService.cs` + `CommerceQueryService.{Checkout,Coupons,CustomCheckouts,Dunning,Portal,Products,Stats,Subscribers,Transactions}.cs`.
3. **Ops LLM partials** — `LlmOrchestratorService.cs` + `.Prompts.cs` + `.Tools.cs`.
4. **TypeSpec module split** — `packages/api-spec/modules/<module>/{models,routes}.tsp` (+ commerce already split `admin-routes` / `public-routes`).
5. **CQRS vertical slices** — one command/handler file per use case under `Application/Commands` (preferred over multi-handler bags).

---

## 1. Inventory — largest hand-maintained C# (production code)

> Absolute paths under `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/`.  
> “LOC” = approximate total lines.

### 1.1 Ranked table (hand-maintained production sources ≥ ~200 LOC)

| LOC | Path | Kind | Multi-resp? | Priority |
|----:|------|------|:-----------:|:--------:|
| 766 | `apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs` | Minimal API maps + auth helpers | **Yes** | **P0** |
| 646 | `apps/lazuar-api/Modules/One/Application/Commands/ProvisionAuraWorkspaceCommand.cs` | Command + result DTO + handler + normalize helpers | **Yes** | **P0** |
| 519 | `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs` | Hosted job: claim + pre-dunning + past-due + dispatch | **Yes** | **P0** |
| 485 | `apps/lazuar-api/src/Lazuar.Api/Program.cs` | Host composition root | Partially | **P1** |
| 433 | `apps/lazuar-api/Modules/Ops/Infrastructure/Services/LlmOrchestratorService.cs` | Chat non-stream + stream orchestration | Partially (already partials) | **P1** |
| 383 | `apps/lazuar-api/Modules/Lhdn/Infrastructure/Gateways/LhdnGatewayAdapter.cs` | MyInvois HTTP: token/submit/status/TIN/cancel + rate limit | Partially | **P1** |
| 375 | `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs` | Checkout complete + subscription payment + logging | **Yes** | **P1** |
| 371 | `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicEndpoints.cs` | Public product/portal/checkout/status/arrears | **Yes** | **P1** |
| 356 | `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` | Full gateway adapter | Cohesive adapter | **P2** |
| 351 | `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` | Full gateway adapter (webhook heavy) | Cohesive adapter | **P2** |
| 329 | `apps/lazuar-api/Modules/Billing/Infrastructure/Services/BillingQueryService.cs` | Ledger + summary + credits + profile reads | Partially | **P2** |
| 309 | `apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs` | Schedule + catch-up + per-org period | Partially | **P2** |
| 305 | `apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` | Webhook verify/log/emit + integration session metadata | Partially | **P1** |
| 302 | `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs` | Full gateway adapter | Cohesive adapter | **P2** |
| 276 | `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs` | Full gateway adapter | Cohesive adapter | **P2** |
| 276 | `apps/lazuar-api/Modules/Commerce/Infrastructure/CommerceDbContext.cs` | DbSets + SaveChanges hooks + fluent config | Cohesive EF | **P2** |
| 246 | `apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints.cs` | Documents + admin keys/webhooks/config | **Yes** | **P2** |
| 237 | `apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints.cs` | Admin ledger/credits + public documents | **Yes** | **P2** |
| 233 | `apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` | Public checkout orchestration | Cohesive use-case | **P2** |
| 228 | `apps/lazuar-api/Modules/Payments/Application/Commands/CreateIntegrationCheckoutCommandHandler.cs` | M2M checkout create | Cohesive use-case | **P2** |
| 225 | `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` | Recurring bill claim + process | Cohesive job | **P2** |
| 220 | `apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/IntegrationCheckoutGatewayEventsHandler.cs` | completed + failed dual handler | Mild | **P2** |
| 210 | `apps/lazuar-api/Modules/Ops/Infrastructure/Endpoints.cs` | Chat CRUD + stream + execute-action | Mild | **P2** |
| 207 | `apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs` | Message dispatch | Cohesive | **P2** (watch) |
| 202 | `apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs` | Outbound webhook delivery | Cohesive | **P2** (watch) |
| 196 | `apps/lazuar-api/Modules/Lhdn/Infrastructure/Workers/LhdnSubmissionJob.cs` | Submission worker | Cohesive | OK |
| 192 | `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/SubscriberEndpoints.cs` | Admin subscribers | Mild | OK / P2 if growing |
| 181 | `apps/lazuar-api/Modules/Billing/Infrastructure/BillingDbContext.cs` | EF config | Cohesive | OK |
| 178 | `apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs` | API key auth | Cohesive | OK |
| 177 | `apps/lazuar-api/Modules/Billing/Infrastructure/Documents/BaseInvoiceDocument.cs` | QuestPDF layout | Cohesive | OK |
| 176 | `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs` | Aggregate | Cohesive domain | OK |
| 174 | `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` | Failure path | Cohesive | OK |
| 167 | `apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs` | Tenant gate | Cohesive | OK |
| 166 | `apps/lazuar-api/Modules/One/Infrastructure/Services/OneQueryService.cs` | One reads | Mild | OK |
| 164 | `apps/lazuar-api/Modules/Payments/Infrastructure/PlatformEndpoints.cs` | Platform payment maps | Mild | OK |
| 162 | `apps/lazuar-api/Modules/Lhdn/Application/Commands/SubmitTaxDocumentCommand.cs` | Submit command | Cohesive | OK |
| 159 | `apps/lazuar-api/Modules/Lhdn/Application/Queries/LhdnQueries.cs` | Multi query types bag | Mild | **P2** |
| 155 | `apps/lazuar-api/Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs` | Aggregate repo | Mild | OK |
| 152 | `apps/lazuar-api/Modules/Payments/Infrastructure/IntegrationEndpoints.cs` | Integration checkout maps | Mild | OK |
| 151 | `apps/lazuar-api/Modules/One/Infrastructure/OneDbContext.cs` | EF config | Cohesive | OK |
| 146 | `apps/lazuar-api/Modules/One/Infrastructure/Repositories/OneRepository.cs` | Aggregate repo | Mild | OK |
| 146 | `apps/lazuar-api/Modules/Commerce/Application/Commands/DunningCampaignCommandHandlers.cs` | Multi-handler bag | Mild | **P2** |
| 145 | `apps/lazuar-api/Modules/Commerce/Application/Commands/RecordSubscriberPaymentCommandHandler.cs` | Use case | Cohesive | OK |
| 143 | `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/ProductEndpoints.cs` | Admin products | Mild | OK |

### 1.2 Large generated / EF artifacts (do **not** hand-chunk)

| LOC | Path | Notes |
|----:|------|-------|
| ~10860 | `packages/api-types-ts/src/index.ts` | openapi-typescript output — regenerate only |
| ~6514 | `packages/api-types-dotnet/Lazuar.ApiContracts.cs` | NSwag auto-generated — regenerate only |
| ~1940 | `packages/api-types-dotnet/Generated/Models.cs` | NSwag partial models — regenerate only |
| 693 | `Modules/Commerce/Infrastructure/Migrations/CommerceDbContextModelSnapshot.cs` | EF snapshot |
| 539 | `Modules/Billing/Infrastructure/Migrations/BillingDbContextModelSnapshot.cs` | EF snapshot |
| 461 | `Modules/Lhdn/Infrastructure/Migrations/LhdnDbContextModelSnapshot.cs` | EF snapshot |
| 449 | `Modules/One/Infrastructure/Migrations/OneDbContextModelSnapshot.cs` | EF snapshot |
| 285 | `Modules/Payments/Infrastructure/Migrations/PaymentsDbContextModelSnapshot.cs` | EF snapshot |
| (various) | `*Designer.cs`, `Initial*Schema.cs` | EF migrations — never hand-split |

If generated clients feel “too big,” the fix is **upstream TypeSpec modularity / OpenAPI product slices**, not editing the generated CS/TS.

### 1.3 Tests (context only — optional chunking)

| LOC | Path | Note |
|----:|------|------|
| 758 | `apps/lazuar-api/tests/Lazuar.ModuleTests/One/ProvisionAuraWorkspaceTests.cs` | Tracks provision complexity; split when production command is split |

---

## 2. Inventory — TypeSpec (`packages/api-spec`)

| LOC | Path | Responsibilities | Multi-resp? | Priority |
|----:|------|------------------|:-----------:|:--------:|
| 383 | `modules/commerce/models.tsp` | Products, portal, dunning, payment config, coupons, stats, checkout, custom checkout, arrears | **Yes** | **P1** |
| 297 | `modules/one/models.tsp` | Auth, workspace, webhooks, API keys, provision DTOs | **Yes** | **P1** |
| 234 | `modules/one/routes.tsp` | Entire One surface in one interface | **Yes** | **P1** |
| 203 | `modules/commerce/admin-routes.tsp` | Admin commerce ops | Mild | **P2** |
| 166 | `modules/lhdn/models.tsp` | LHDN models (+ aliases) | Mild | OK / P2 |
| 100 | `modules/billing/models.tsp` | Billing models | OK | — |
| 99 | `modules/commerce/public-routes.tsp` | Public commerce | OK | — |
| 92 | `modules/billing/routes.tsp` | Billing routes | OK | — |
| 92 | `modules/lhdn/routes.tsp` | LHDN routes | OK | — |
| 87 | `modules/communications/models.tsp` | Comm models | OK | — |
| 74 | `modules/ops/routes.tsp` | Ops routes | OK | — |
| 66 | `modules/communications/admin-routes.tsp` | Comm admin | OK | — |
| 65 | `modules/payments/models.tsp` | Payments models | OK | — |
| 58 | `modules/ops/models.tsp` | Ops models | OK | — |
| ≤46 | other `*.tsp` (`main`, `docs-*`, `platform`, `common`) | Entry/docs | OK | — |

Commerce already split routes into `admin-routes.tsp` + `public-routes.tsp` — **good pattern**. Models remain a single bag.

---

## 3. Detailed god-file analyses

### 3.1 P0 — `Modules/One/Infrastructure/Endpoints.cs` (766 LOC)

**Absolute path:**  
`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs`

#### Current responsibilities (mixed)

1. **Public auth / registration** — register, login, logout, forgot/reset password, verify/resend email, `/auth/me` (with inline password verify + JWT cookie issuance).
2. **Profile / security** — update profile, change password.
3. **Workspaces CRUD & membership** — create/update/archive/list workspaces, members, invites, accept invite, remove member.
4. **App entitlements** — list apps, toggle entitlement, `/me/entitlements`.
5. **Tenant webhooks** — list/create/update endpoints, delivery logs; custom `CanAccessWorkspaceWebhooksAsync` authZ (system admin / API_CLIENT scope / human membership).
6. **Storage** — R2 presigned upload URL.
7. **OrgAdmin API credentials** — list/generate/revoke platform API keys (JWT OrgAdmin only).
8. **Scope probe** — `/one/integrations/payments/checkouts/_scope-probe`.
9. **Integrator provision** — large inline handler (~150 LOC): provision-key auth, dual rate limits, DTO mapping, problem details, conflict handling.
10. **Helpers** — `IssueCookie`, `FirstNonEmpty`, `CanAccessWorkspaceWebhooksAsync`.

Commerce already proved the split model; One is the last major module with a monolithic `Endpoints.cs`.

#### Idiomatic split proposal

Mirror Commerce:

```
Modules/One/Infrastructure/
  Endpoints.cs                          # MapOneEndpoints orchestrator only (~40–60 LOC)
  Endpoints/
    AuthEndpoints.cs                    # register, login, logout, password/email verify, /auth/me
    ProfileEndpoints.cs                 # /me/profile, /me/security/password
    WorkspaceEndpoints.cs               # workspaces CRUD, members, invites, apps, entitlements
    WebhookEndpoints.cs                 # webhooks + logs + CanAccessWorkspaceWebhooksAsync
    StorageEndpoints.cs                 # presigned URL
    ApiCredentialEndpoints.cs           # OrgAdmin /api-keys*
    IntegrationProvisionEndpoints.cs    # provision + scope-probe
    AuthCookieHelper.cs                 # IssueCookie (shared by auth routes)
```

**Mapping style:** each file exposes `public static RouteGroupBuilder MapX(this RouteGroupBuilder group)` or `IEndpointRouteBuilder` extensions; `Endpoints.MapOneEndpoints` composes them (same as `MapCommerceEndpoints`).

Optional further cleanup (not required for first PR):
- Move login password verification fully into a `LoginCommand` (today login is inline EF + cookie).
- Keep provision HTTP thin — already delegates to `ProvisionAuraWorkspaceCommand`.

#### Risk of split

| Risk | Level | Mitigation |
|------|-------|------------|
| Route registration order / group prefixes | Medium | Keep single outer `MapGroup("/one")`; only extract map methods |
| Lost `internal` helper visibility | Low | Keep helpers `internal static` in same assembly |
| Auth cookie regressions | Medium | Module tests for login/register; manual smoke on cookie domain/secure flags |
| Provision rate-limit behavior | Medium | Existing `ProvisionAuraWorkspaceTests` + endpoint auth tests |
| Architecture tests | Low | No new project refs; same Infrastructure assembly |

#### Priority: **P0**

Highest LOC endpoint god-file; blocks readability of identity + integrator surface. Clear template already exists in Commerce.

---

### 3.2 P0 — `ProvisionAuraWorkspaceCommand.cs` (646 LOC)

**Absolute path:**  
`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/ProvisionAuraWorkspaceCommand.cs`

#### Current responsibilities (mixed)

1. **Public contracts** — `ProvisionAuraWorkspaceResult`, `ProvisionAuraWorkspaceCommand`.
2. **Handler orchestration** — create path vs idempotent re-entry (`EnsureAndBuildExistingAsync`), unique-violation race recovery.
3. **Bootstrap API key minting** — scopes, prefix sk_test_/sk_live_, hash/hint.
4. **Webhook ensure** — create once, never remint secret, default Connect events.
5. **Owner attach / ensure** — membership ADMIN/SUPER_ADMIN.
6. **Normalization & validation utilities** — product slug regex, external org id, owner role, webhook URL, event list, key name, unique violation detection, secret hint.
7. **Constants** — product/app IDs, statuses, length limits, default webhook events.

This is both a **use-case handler** and a **mini domain-service library** for integrator provisioning. Tests file is 758 LOC and mirrors this bulk.

#### Idiomatic split proposal

```
Modules/One/Application/Commands/
  ProvisionAuraWorkspaceCommand.cs           # record command only
  ProvisionAuraWorkspaceCommandHandler.cs    # Handle + EnsureAndBuildExistingAsync + BuildResult
  ProvisionAuraWorkspaceResult.cs            # result record (or keep next to command)

Modules/One/Application/Provisioning/        # OR Domain/ if pure, but currently uses IOneRepository
  ProvisionNormalization.cs                  # NormalizeExternalProduct/OrgId/OwnerRole, webhook events/URL
  ProvisionConstants.cs                      # statuses, default events, lengths, key names
  ProvisionOwnerAttacher.cs                  # TryAttachOwnerAsync / EnsureOwnerAsync
  ProvisionWebhookEnsuring.cs                # webhook match/create (optional)
```

Prefer **Application** helpers (not Domain) because they depend on `IOneRepository` / `ITokenGeneratorService`. Pure validators (`NormalizeExternalProduct`, role checks) *could* live under `Domain` next to `WebhookUrlValidator` if you want domain purity.

#### Risk of split

| Risk | Level | Mitigation |
|------|-------|------------|
| Idempotent race path breakage | **High** | Keep `EnsureAndBuildExistingAsync` + unique-violation catch in same PR; run full `ProvisionAuraWorkspaceTests` |
| Secret once-only semantics | **High** | Explicit tests that remint does not occur |
| Public static method moves break callers | Medium | `Normalize*` / `DefaultKeyNameFor` used from Endpoints + tests — update usings carefully; consider thin facade on handler class for backward compat |
| Over-fragmentation | Low | Cap at 3–5 files; avoid one-method files |

#### Priority: **P0**

Core integrator path; density of branching + security-sensitive minting; largest Application handler in the backend.

---

### 3.3 P0 — `DunningEngineJob.cs` (519 LOC)

**Absolute path:**  
`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs`

#### Current responsibilities (mixed)

1. **Hosted service loop** — interval from `BackgroundWorkerOptions`, error logging.
2. **Campaign load + batch orchestration** — `ProcessDunningAsync`.
3. **Claim modes** — Postgres `FOR UPDATE SKIP LOCKED` vs in-memory claim for tests (`ClaimMode`, `ClaimSubscriptionAsync`, `ClaimSubscriptionInMemoryAsync`).
4. **Pre-dunning (DayOffset < 0)** — reminder steps before due date.
5. **Past-due pipeline (DayOffset ≥ 0)** — communication + charge/retry steps, pause, cancel, metrics (`LazuarMetrics`), charge attempt logs.
6. **Communication dispatch** — `ResolveEffectiveCommunicationAction`, `DispatchCommunicationStepAsync` (integration events to Communications).

#### Idiomatic split proposal

```
Modules/Commerce/Infrastructure/Workers/
  DunningEngineJob.cs                 # BackgroundService loop + RunOnceAsync + ProcessDunningAsync orchestration only
  Dunning/
    DunningSubscriptionClaimer.cs     # ClaimMode + Postgres/in-memory claim
    PreDunningProcessor.cs            # ProcessPreDunningSubscriptionAsync
    PastDueDunningProcessor.cs        # ProcessPastDueSubscriptionAsync (largest block ~lines 289–468)
    DunningCommunicationDispatcher.cs # ResolveEffective + DispatchCommunicationStepAsync
```

Alternative (partials, lower ceremony — matches `CommerceQueryService` / Ops LLM):

```
DunningEngineJob.cs
DunningEngineJob.Claim.cs
DunningEngineJob.PreDunning.cs
DunningEngineJob.PastDue.cs
DunningEngineJob.Communication.cs
```

**Recommendation:** partials first (fast, no DI churn), extract classes later if unit testing claim logic in isolation becomes valuable.

#### Risk of split

| Risk | Level | Mitigation |
|------|-------|------------|
| Transaction / claim semantics across methods | **High** | Keep claim + process in same scoped DbContext flow; do not introduce nested scopes mid-claim |
| Double-dispatch reminders | **High** | Existing dunning domain/module tests; golden paths for pre vs past-due |
| Metrics double-count | Medium | Centralize metric increments in PastDue processor only |
| WhatsApp fallback behavior | Medium | Preserve `ResolveEffectiveCommunicationAction` exactly |

#### Priority: **P0**

Money + customer comms path; past-due block alone is a god-method. Chunking improves reviewability without changing architecture.

---

### 3.4 P1 — `Program.cs` (485 LOC)

**Absolute path:**  
`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Program.cs`

#### Current responsibilities

1. Optional monorepo `.env` load into environment variables.
2. WebApplication builder, Azure Key Vault optional config.
3. Serilog setup.
4. Options binding (Resend, workers, observability, platform admin).
5. Metrics collector + refresh job registration.
6. Cross-cutting DI (password, JWT, email, vault, LLM factory, event bus, R2/S3).
7. Dual API-key event handler registration.
8. Authentication (JWT Bearer) + large **authorization policies** block (OrgAdmin, LHDN read/write, payments checkouts, webhooks manage, etc.).
9. CORS, JSON options, exception handler, MediatR assembly scan.
10. Module `Add*Module` registrations.
11. Boot-time EF migrate-all-modules loop.
12. Middleware pipeline + `Use*Subscriptions`.
13. Health/ready/metrics endpoints.
14. Module endpoint mapping + platform group.

#### Idiomatic split proposal

```
src/Lazuar.Api/
  Program.cs                              # thin: CreateBuilder → extensions → Run
  Configuration/
    AppOptions.cs                         # (existing)
    EnvFileLoader.cs                      # .env load
  Hosting/
    ServiceCollectionExtensions.cs        # AddLazuarInfrastructure / AddLazuarAuth / AddLazuarModules
    AuthorizationPolicies.cs              # all AddAuthorization policies
    WebApplicationExtensions.cs           # middleware pipeline, health maps, endpoint maps
    DatabaseMigrationBootstrap.cs         # first-boot migrate loop
```

This matches common ASP.NET modular host style and keeps `Program.cs` under ~80 lines.

#### Risk of split

| Risk | Level | Mitigation |
|------|-------|------------|
| Boot order bugs (migrate before hosted services, auth before middleware) | **High** | Preserve call order exactly; smoke boot + `/health/ready` |
| Policy name typos | Medium | Architecture/integration tests that hit `RequireAuthorization("…")` routes |
| Integration tests using `WebApplicationFactory<Program>` | Medium | Keep `public partial class Program`; do not change entry semantics |
| Over-abstracting host | Low | Prefer static extension methods, not a DI “framework” |

#### Priority: **P1**

Large but **expected** for a composition root. Split improves maintainability; not a logic god-class. Do after or alongside One endpoints split.

---

### 3.5 P1 — `LlmOrchestratorService.cs` (433 LOC) (+ existing partials)

**Paths:**
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Ops/Infrastructure/Services/LlmOrchestratorService.cs` (433)
- `LlmOrchestratorService.Prompts.cs` (98)
- `LlmOrchestratorService.Tools.cs` (86)

#### Current responsibilities

1. Non-streaming chat completion (`ProcessChatAsync`).
2. Streaming multi-turn tool loop (`ProcessChatStreamAsync`) — conversation create/load, tool execution iterations, cost tracking, title generation hooks.
3. Tenant validation + cost logging.
4. (Partials) prompt assembly + tool call handling.

#### Idiomatic split proposal

Continue the partial approach:

```
LlmOrchestratorService.cs              # ctor + public API facades
LlmOrchestratorService.NonStream.cs    # ProcessChatAsync
LlmOrchestratorService.Stream.cs       # ProcessChatStreamAsync body
LlmOrchestratorService.Prompts.cs      # existing
LlmOrchestratorService.Tools.cs        # existing
LlmOrchestratorService.Cost.cs         # TrackAndLogCost + GetValidatedTenantId (optional)
```

Or extract `ChatStreamOrchestrator` injected service if stream logic needs isolated unit tests.

#### Risk of split

| Risk | Level | Mitigation |
|------|-------|------------|
| Stream enumeration / yield state bugs | Medium | `LlmOrchestratorServiceTests` + manual stream smoke |
| Scope lifetime (scoped repo inside singleton-ish orchestrator) | Medium | Preserve existing `IServiceScopeFactory` patterns |

#### Priority: **P1**

Already partially chunked; finish splitting stream vs non-stream for readability.

---

### 3.6 P1 — `LhdnGatewayAdapter.cs` (383 LOC)

**Absolute path:**  
`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Gateways/LhdnGatewayAdapter.cs`

#### Current responsibilities

1. Shared HTTP client factory + token cache + configuration base URL.
2. **Per-operation rate limiters** (login/submit/poll/tin/cancel) via static concurrent dictionaries + token buckets.
3. Intermediary TIN header helper + Retry-After parsing.
4. **GetTokenAsync** (login).
5. **SubmitDocumentAsync**.
6. **GetDocumentStatusAsync** (largest response mapping).
7. **ValidateTaxpayerTinAsync**.
8. **CancelDocumentAsync**.

#### Idiomatic split proposal

```
Gateways/
  LhdnGatewayAdapter.cs                 # façade implementing ILhdnGatewayAdapter, delegates
  Lhdn/
    LhdnHttpClientBase.cs               # base URL, intermediary header, retry-after
    LhdnRateLimiterRegistry.cs          # limiter maps + EnforceRateLimitAsync
    LhdnTokenClient.cs                  # GetTokenAsync + cache
    LhdnDocumentClient.cs               # submit + status + cancel
    LhdnTaxpayerClient.cs               # TIN validate
```

Or **partials** on `LhdnGatewayAdapter` (`*.Token.cs`, `*.Submit.cs`, `*.Status.cs`) if you want zero DI surface change.

#### Risk of split

| Risk | Level | Mitigation |
|------|-------|------------|
| Rate-limiter static state shared incorrectly | Medium | Keep registry singleton-static or inject shared registry |
| MyInvois response parsing regressions | **High** | Sandbox E2E + golden master JSON tests |
| Token cache key collisions | Medium | Keep cache key construction in one place |

#### Priority: **P1**

External compliance boundary; splitting reduces merge conflicts when status/submit evolve independently.

---

### 3.7 P1 — `GatewayPaymentCompletedIntegrationEventHandler.cs` (375 LOC)

**Absolute path:**  
`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs`

#### Current responsibilities

1. Entry routing on completed gateway payment (open checkout session vs subscription payment).
2. **Open checkout completion** — order/subscription creation, CRM profile, events, transaction log.
3. **Subscription payment application** — period advance, arrears, charge attempt success, events.
4. Correlation id resolution + transaction log write helpers.

#### Idiomatic split proposal

```
EventHandlers/
  GatewayPaymentCompletedIntegrationEventHandler.cs   # HandleAsync router only
  PaymentCompleted/
    OpenCheckoutPaymentCompleter.cs
    SubscriptionPaymentApplier.cs
    CommerceTransactionLogger.cs                      # LogTransactionAsync
    PaymentCorrelation.cs                             # TryResolveCorrelationId
```

Partials also fine:

```
GatewayPaymentCompletedIntegrationEventHandler.cs
GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs
GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs
```

#### Risk of split

| Risk | Level | Mitigation |
|------|-------|------------|
| Double fulfillment / missed outbox events | **High** | Module tests for payment completed; idempotent event handling tests |
| CRM profile side effects | Medium | Keep CRM calls in one completer |
| Cross-path shared state assumptions | Medium | Explicit parameters; no hidden instance fields |

#### Priority: **P1**

Critical revenue path; two distinct business flows already named in private methods — clean extraction.

---

### 3.8 P1 — `PublicEndpoints.cs` (Commerce, 371 LOC)

**Absolute path:**  
`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicEndpoints.cs`

#### Current responsibilities

Already extracted from admin endpoints, but **still multi-feature**:

1. Public product by slug.
2. Coupon validation.
3. Customer portal aggregate + cancel.
4. Initiate checkout (POST `/checkout`).
5. Checkout session status (tenant-scoped + legacy subId).
6. Custom checkout read.
7. Arrears summary + update payment method.

#### Idiomatic split proposal

```
Endpoints/
  PublicProductEndpoints.cs
  PublicPortalEndpoints.cs
  PublicCheckoutEndpoints.cs          # initiate + status routes
  PublicCustomCheckoutEndpoints.cs
  PublicArrearsEndpoints.cs
  PublicEndpoints.cs                  # MapPublicCommerceEndpoints composer
```

Aligns with TypeSpec `public-routes.tsp` further splits if desired.

#### Risk of split

| Risk | Level | Mitigation |
|------|-------|------------|
| Public route path regressions | Medium | Contract tests / TypeSpec alignment; portal FE smoke |
| Auth optional vs required mix-ups | Medium | Keep attributes with each map |

#### Priority: **P1**

Largest remaining Commerce endpoint file; natural second step after One endpoints.

---

### 3.9 P1 — `ProcessGatewayWebhookCommandHandler.cs` (305 LOC)

**Absolute path:**  
`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs`

#### Current responsibilities

1. Public `Handle` entry + logging wrapper.
2. Config load, secret decrypt, adapter resolve, `ParseWebhookAsync`.
3. Webhook log persistence + business key uniqueness.
4. Integration checkout session metadata merge.
5. Event emission of gateway completed/failed/refund events.
6. Unique constraint helpers.

#### Idiomatic split proposal

```
Commands/
  ProcessGatewayWebhookCommand.cs
  ProcessGatewayWebhookCommandHandler.cs     # orchestration
Services/   # Application layer helpers
  WebhookBusinessKey.cs
  IntegrationCheckoutMetadataMerger.cs       # MergeSessionMetadataAsync
  WebhookProcessingLogger.cs                 # optional
```

Keep a **single handler entry** for MediatR registration stability.

#### Risk of split

| Risk | Level | Mitigation |
|------|-------|------------|
| Idempotency / duplicate event publish | **High** | `ProcessGatewayWebhookCommandHandlerTests` + unique business key tests |
| Metadata merge wrong for integration vs commerce | Medium | Explicit unit tests for MergeSessionMetadataAsync |

#### Priority: **P1**

Cross-gateway webhook spine; worth clarity even if not “god” in the OOP sense.

---

### 3.10 P2 — Payment gateway adapters (276–356 LOC each)

**Paths:**
- `.../Gateways/ChipCollectGatewayAdapter.cs` (356)
- `.../Gateways/StripeGatewayAdapter.cs` (351)
- `.../Gateways/BillplzGatewayAdapter.cs` (302)
- `.../Gateways/RazorpayGatewayAdapter.cs` (276)

#### Current responsibilities

Each implements full `IPaymentGatewayAdapter`:
- GenerateCheckout
- ParseWebhook (often largest)
- ChargeOffSession
- IssueRefund
- GenerateCustomerPortal

This is **interface-cohesive** rather than multi-domain god-class. Size comes from provider API surface.

#### Idiomatic split proposal (per adapter, only when webhook parsing dominates)

```
Gateways/Stripe/
  StripeGatewayAdapter.cs              # façade
  StripeCheckoutClient.cs
  StripeWebhookParser.cs               # ParseWebhookAsync
  StripeOffSessionClient.cs
```

Or partials: `StripeGatewayAdapter.Webhook.cs`, etc.

**Do not** invent a shared “BaseGateway” that couples providers — keep adapter isolation.

#### Risk of split

| Risk | Level | Mitigation |
|------|-------|------------|
| Webhook signature verification bugs | **High** | Adapter unit tests with fixtures; sandbox webhooks |
| Partial method accessibility | Low | Same class partials |

#### Priority: **P2**

Split when actively editing; not urgent if stable. Prefer extracting **webhook parser** first when a file exceeds ~350 LOC.

---

### 3.11 P2 — `BillingQueryService.cs` (329 LOC)

**Path:** `.../Billing/Infrastructure/Services/BillingQueryService.cs`

#### Responsibilities

Dapper read models:
- Paginated ledger (+ line join assembly)
- Financial summary
- Net profit series
- Credit balance / sufficiency
- Billing profile

#### Split proposal (mirror CommerceQueryService)

```
BillingQueryService.cs                 # ctor + shared connection helper
BillingQueryService.Ledger.cs
BillingQueryService.Summary.cs
BillingQueryService.Credits.cs
BillingQueryService.Profile.cs
```

#### Risk

Low–medium SQL regressions; integration tests already exist (`BillingQueryServiceTests`).

#### Priority: **P2**

---

### 3.12 P2 — `B2cConsolidationJob.cs` (309 LOC)

**Path:** `.../Billing/Infrastructure/Workers/B2cConsolidationJob.cs`

#### Responsibilities

1. Hosted schedule (Malaysia timezone next-run math).
2. Catch-up closed periods.
3. Per-org period consolidation (ledger + LHDN document interaction).

#### Split proposal

```
B2cConsolidationJob.cs                 # loop + schedule
B2c/
  MalaysiaSchedule.cs                  # timezone + CalculateTimeToNextConsolidation
  B2cPeriodCatchUp.cs
  B2cOrgPeriodProcessor.cs             # ProcessOrgPeriodAsync
```

#### Risk

High domain risk if period boundaries change; low structural risk if pure moves. Tests: `B2cConsolidationJobTests`.

#### Priority: **P2**

---

### 3.13 P2 — `CommerceDbContext.cs` (276 LOC)

**Path:** `.../Commerce/Infrastructure/CommerceDbContext.cs`

#### Responsibilities

DbSets, SaveChanges coercion for append-only dunning logs, OnModelCreating for all commerce entities + outbox/inbox.

#### Split proposal (optional)

```
CommerceDbContext.cs
Configurations/
  ProductConfiguration.cs
  SubscriptionConfiguration.cs
  DunningConfigurations.cs
  CheckoutSessionConfiguration.cs
  OutboxInboxConfiguration.cs
```

Apply via `modelBuilder.ApplyConfigurationsFromAssembly(...)` or explicit `IEntityTypeConfiguration<T>`.

#### Risk

EF model snapshot churn if fluent API rewritten carelessly; prefer mechanical move of configuration blocks. **No migration** if model identical.

#### Priority: **P2**

Nice-to-have; Commerce has the most entities. One/Billing/Lhdn DbContexts are smaller and can wait.

---

### 3.14 P2 — Remaining endpoint monoliths

| File | LOC | Proposed split |
|------|----:|----------------|
| `Lhdn/Infrastructure/Endpoints.cs` | 246 | `DocumentEndpoints`, `AdminApiKeyEndpoints`, `AdminWebhookEndpoints`, `TenantConfigEndpoints` |
| `Billing/Infrastructure/Endpoints.cs` | 237 | `AdminLedgerEndpoints`, `AdminCreditsEndpoints`, `AdminProfileEndpoints`, `PublicDocumentEndpoints` |
| `Ops/Infrastructure/Endpoints.cs` | 210 | `ChatEndpoints`, `ChatStreamEndpoints`, `ExecuteActionEndpoints` |

#### Priority: **P2**

Do after One/Commerce public; same extension-method composition pattern.

---

### 3.15 P2 — Multi-handler “bags”

| File | LOC | Proposal |
|------|----:|----------|
| `Commerce/.../DunningCampaignCommandHandlers.cs` | 146 | One handler file per command (create/update/pause/…) |
| `Commerce/.../CouponCommandHandlers.cs` | 91 | Same |
| `Lhdn/.../Queries/LhdnQueries.cs` | 159 | Split query records/handlers per file |
| `Lhdn/.../Commands/WebhookCommands.cs` | 60 | Already small; split if growing |
| `Payments/.../IntegrationCheckoutGatewayEventsHandler.cs` | 220 | Split completed vs failed into two handler classes (or partials) |

#### Priority: **P2**

Idiomatic CQRS hygiene more than size crisis.

---

### 3.16 Borderline / OK (do not prioritize)

- Domain aggregates (`Subscription` 176, `TaxDocument` 128, etc.) — size is normal for rich models; split only if methods cluster into distinct subdomains.
- Middleware (`ApiKeyAuthenticationMiddleware` 178, `TenantSecurityMiddleware` 167) — single responsibility.
- Individual command handlers ~100–230 LOC with one `Handle` method — **prefer leave** unless multi-phase extractable algorithms appear.
- Messaging/Outbound webhook jobs ~200 LOC — monitor growth.

---

## 4. TypeSpec detailed analyses

### 4.1 P1 — `modules/commerce/models.tsp` (383 LOC)

#### Responsibilities

Single namespace bag of ~38 models spanning:
- Product + checkout configuration
- Public checkout / portal
- Dunning campaigns & steps
- Payment config (commerce-facing)
- Subscribers / payment records / transaction logs
- Coupons
- Stats / cashflow
- Custom checkout + arrears

#### Idiomatic split proposal

```
modules/commerce/
  models/
    product.tsp
    checkout.tsp
    portal.tsp
    dunning.tsp
    payment-config.tsp
    subscriber.tsp
    coupon.tsp
    stats.tsp
    custom-checkout.tsp
  models.tsp                 # import all (barrel) OR import directly from routes
  admin-routes.tsp
  public-routes.tsp
```

TypeSpec supports file imports; keep `namespace LazuarApi.Commerce` consistent.

#### Risk

| Risk | Level | Mitigation |
|------|-------|------------|
| OpenAPI emit path / operationId drift | Medium | Diff `packages/api-spec/dist/**/openapi.yaml` before/after |
| NSwag / openapi-typescript client churn | Medium | Regenerated clients only; no hand edits; FE compile |
| Circular model refs | Low | Keep shared small models in `checkout.tsp` or `common.tsp` |

#### Priority: **P1**

Largest TypeSpec model bag; aligns with admin/public route split already done.

---

### 4.2 P1 — `modules/one/models.tsp` (297) + `routes.tsp` (234)

#### Responsibilities

**models.tsp:** auth DTOs, workspace/member/invite, entitlements, webhooks, presigned storage, API keys, **provision** request/response tree.

**routes.tsp:** one mega `interface OneOperations` covering all of the above.

#### Idiomatic split proposal

```
modules/one/
  models/
    auth.tsp
    workspace.tsp
    webhook.tsp
    api-keys.tsp
    storage.tsp
    provision.tsp
  models.tsp                 # barrel imports
  routes/
    auth-routes.tsp
    workspace-routes.tsp
    webhook-routes.tsp
    api-key-routes.tsp
    storage-routes.tsp
    provision-routes.tsp
  routes.tsp                 # compose interfaces or re-export
```

TypeSpec pattern options:
1. Multiple interfaces under same `@route("/one")` namespace (preferred if emitter merges cleanly).
2. Single interface file that only re-exports operations via `...` spreads if supported by version — verify against current `@typespec/http` in package.

#### Risk

| Risk | Level | Mitigation |
|------|-------|------------|
| OpenAPI path merge / tag changes | Medium | Golden diff OpenAPI; fix `tspconfig` if needed |
| Docs `docs-one.tsp` import breakage | Low | Update imports |
| Client regeneration noise | Medium | Single dedicated PR for TypeSpec reorg + regenerate |

#### Priority: **P1**

Pairs with C# One endpoint split for end-to-end navigability.

---

### 4.3 P2 — `modules/commerce/admin-routes.tsp` (203)

Optional further split by resource (products, subscribers, dunning, coupons, stats) once models are split. Not urgent.

### 4.4 Generated clients (out of scope for hand chunking)

| Package | Role | Action if “too large” |
|---------|------|------------------------|
| `packages/api-types-dotnet` | NSwag from OpenAPI | Regenerate after TypeSpec product slices; never manual edit |
| `packages/api-types-ts` | openapi-typescript | Same |

Product-scoped OpenAPI already partially exists under `packages/api-spec/dist/{billing,commerce,lhdn,one,ops,payments}/` — prefer consuming **product slices** in clients rather than the monorepo mega-client when possible (see ADR 006/007).

---

## 5. Priority roadmap

### P0 — do first (high confusion / multi-resp / critical paths)

| # | Item | Suggested PR shape |
|---|------|--------------------|
| 1 | Split `One/Infrastructure/Endpoints.cs` | Mechanical file moves + composer; no behavior change |
| 2 | Split `ProvisionAuraWorkspaceCommand.cs` | Extract normalize/helpers; keep handler behavior; full provision tests |
| 3 | Split `DunningEngineJob.cs` | Partials or processors; dunning module tests |

### P1 — next maintenance window

| # | Item |
|---|------|
| 4 | `Program.cs` host extensions |
| 5 | Commerce `PublicEndpoints.cs` sub-split |
| 6 | `GatewayPaymentCompletedIntegrationEventHandler` split |
| 7 | `ProcessGatewayWebhookCommandHandler` helpers extract |
| 8 | `LhdnGatewayAdapter` rate-limit + operation clients / partials |
| 9 | Finish `LlmOrchestratorService` stream/non-stream partials |
| 10 | TypeSpec `commerce/models.tsp` + `one/{models,routes}.tsp` reorg + regenerate clients |

### P2 — when touching those areas

- Billing/Lhdn/Ops endpoint composition
- BillingQueryService partials
- B2cConsolidationJob extract
- CommerceDbContext `IEntityTypeConfiguration`
- Gateway adapters webhook partials
- Multi-handler bag → one file per handler
- Commerce admin-routes further TypeSpec split

### Explicit non-goals

- Hand-editing NSwag / openapi-typescript output
- Splitting EF `ModelSnapshot` / Designer files
- Creating shared base classes that violate module boundaries
- “Refactor while fixing a bug” without tests green

---

## 6. Cross-cutting guidelines for any chunking PR

1. **Behavior-preserving first.** Prefer pure moves (cut/paste methods into partials or new types) over logic rewrites.
2. **Follow existing patterns.** Commerce Endpoints + CommerceQueryService partials are the house style.
3. **One module / one concern per PR** when possible (e.g. One endpoints only).
4. **Regenerate, don’t edit clients** after TypeSpec moves.
5. **Keep public type names stable** (`MapOneEndpoints`, command type names, MediatR handler classes) to minimize blast radius.
6. **Tests:**
   - Architecture: `ModuleBoundaryTests` (should stay green with pure Infrastructure/Application moves).
   - Module: provision, dunning, webhooks, gateway adapters.
   - Boot: `WebApplicationFactory` / `/health/ready`.
7. **OpenAPI diff** for TypeSpec PRs: `packages/api-spec/dist/**` should be intentional.
8. **Do not** introduce cross-module references to “share” endpoint helpers.
9. **Line budget after split:** target ≤250 LOC for multi-responsibility files; ≤400 LOC acceptable for cohesive adapters/handlers with clear single use-case.
10. **Avoid premature abstraction.** Partials > deep inheritance for god-file chunking.

---

## 7. Suggested acceptance criteria (when implementing later)

### For a C# endpoint split PR
- [ ] Composer `Map*Endpoints` still registers identical routes (path + method + auth policy).
- [ ] `dotnet test` architecture + relevant module tests pass.
- [ ] No changes to OpenAPI unless intentional.
- [ ] No new warnings for unused usings / missing accessibility.

### For provision / dunning / payment-completed splits
- [ ] Existing module tests pass without rewriting assertions (or only namespace fixes).
- [ ] Idempotent paths still covered (provision race, webhook business key, dunning claim).
- [ ] No new hosted-service registration changes unless required.

### For TypeSpec splits
- [ ] `tsp compile` succeeds for main + docs entrypoints.
- [ ] OpenAPI diff reviewed (paths, schemas, operationIds).
- [ ] Dotnet + TS clients regenerated in the same change set if packaging expects it.
- [ ] FE packages still typecheck against regenerated client.

---

## 8. Appendix A — module health snapshot (endpoints style)

| Module | Endpoints style | God-file risk |
|--------|-----------------|---------------|
| Commerce | Composer + `Endpoints/*` | Medium (`PublicEndpoints` still large) |
| Communications | Composer + `Endpoints/*` | Low |
| One | **Single 766-line file** | **High** |
| Lhdn | Single 246-line file | Medium |
| Billing | Single 237-line file | Medium |
| Payments | Split (`Endpoints`, `IntegrationEndpoints`, `PlatformEndpoints`) | Low–medium |
| Ops | Single 210-line file | Low–medium |
| Messaging | Small | Low |

| Module | Query service style | Notes |
|--------|---------------------|-------|
| Commerce | Partials by feature | **Best practice reference** |
| Billing | Single 329-line file | Candidate for partials |
| One | Single 166-line file | OK |
| Lhdn | Thin query service | OK |
| Ops | LLM orchestrator partials | Good |

---

## 9. Appendix B — “Is it a god file?” checklist

Mark **god-file** if ≥2 of:

1. LOC ≥ 300 **and** multiple feature areas (auth + workspaces + webhooks, etc.).
2. Multiple public entry methods for unrelated use cases in one type.
3. Mixing infrastructure concerns (HTTP + persistence + crypto + policy) without delegation.
4. Team repeatedly conflicts in Git on the same file for unrelated features.
5. Tests for the type exceed ~500 LOC with unrelated scenarios.

**Not** god-files merely because large:

- Single-interface gateway adapters
- EF fluent configs / snapshots
- Generated clients
- Rich domain aggregates with cohesive invariants

---

## 10. Appendix C — recommended first three implementation tickets (for later)

### Ticket A — One endpoints composition (P0)
- Extract `Endpoints/` files listed in §3.1.
- Keep `MapOneEndpoints` signature.
- Smoke: login cookie, list workspaces, provision endpoint, API key mint.

### Ticket B — Provision command decomposition (P0)
- Extract normalization + owner/webhook ensure helpers.
- No change to result shape or secret-once rules.
- Run `ProvisionAuraWorkspaceTests` (758 LOC).

### Ticket C — Dunning engine partials (P0)
- `Claim` / `PreDunning` / `PastDue` / `Communication` partials.
- Run dunning domain + worker tests.
- Verify metrics counters unchanged.

---

## 11. Summary

The Lazuar backend is **already mostly well-factored** (module boundaries, CQRS files, Commerce endpoint/query splits). The remaining pain concentrates in:

1. **One module HTTP surface** (766 LOC god endpoints).
2. **Integrator provision use-case** (646 LOC command bag).
3. **Dunning engine past-due pipeline** (519 LOC job).
4. A second tier of **payment completion / webhooks / LHDN gateway / public commerce / host Program**.
5. **TypeSpec model bags** for Commerce and One that should follow the same multi-file discipline as Commerce routes.

Generated clients and EF snapshots dominate raw line counts but are **out of scope for hand chunking** — improve them only by regenerating from cleaner TypeSpec.

**Do not modify application code from this document alone.** Use as the maintenance backlog for deliberate, test-backed PRs.
