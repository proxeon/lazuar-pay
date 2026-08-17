# 10 — Tenancy, workers, contracts, architecture tests

**Date:** 17 August 2026  
**Branch:** `feat/007-waves-1-4-implement`  
**Pinned commit named in the 009 brief:** `297ba98` (`fix(one): add /accept-invite on ops and mint invite URLs there`)  
**Tree actually read:** workspace HEAD is `30d07d2` (`docs: add post-Wave 0–4 evaluation reports`). That tip is documentation only. Every C# / TypeSpec / script path quoted below is the same as `297ba98` plus the already-landed honesty fix `cbe17c2`.  
**Slice:** Cross-cutting tenant isolation, background workers (outbox / inbox / dead letter / expiry), TypeSpec vs Minimal remaining holes, architecture tests, tests that pin bugs or never run, DI optional services that fail closed vs open, concurrency SKIP LOCKED vs in-memory.  
**Not this file:** product feature gaps; refuse-list items; billing/dunning *claim-logic product* bugs owned by 02/03 (cited only when they are isolation or concurrency bugs).  
**Not implemented. Not committed. Analysis only.**

Honesty numbers in this file come from a live run of `node scripts/check-openapi-minimal-honesty.mjs --verbose` against the existing `packages/api-spec/dist/openapi.yaml` on this workstation. No `task gen` was required: the compiled spec was already present. Exit code **0**.

---

## 0. How to read this report

008 (`plans/008-evals/09-architecture-tenancy-tests.md`, `08-contracts-webhooks-dx.md`) evaluated the tree at `4624070`. This branch then landed a stack of P0/P1 fixes (`911d358` … `297ba98`). 009 re-reads **code as it is now**. A bug 008 filed is closed only if this tree no longer contains it. A bug 008 missed is still written up.

Severity in this slice:

| Tag | Meaning here |
|-----|----------------|
| **P0** | Silent money, silent event loss, or a live integrator/tenant path that does the wrong thing in production today |
| **P1** | Isolation hole, worker double-fire, fail-open optional, or a contract that will make a careful integrator build the wrong receiver |
| **P2** | Test that lies, architecture test that allows the leak, clock/config default, or ops-only hazard under scale |

Code wins. Quotes are from the files on disk. Architecture-test *names* are not proof the production path is safe.

---

## 1. Files table (absolute)

| Path | Why it is in this slice |
|------|-------------------------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Domain/IMustHaveTenant.cs` | Tenant marker |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/PlatformDbContext.cs` | Fail-closed global filter + empty-org write guard + domain-event dispatch + `JobTrigger` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxPublisherJob.cs` | SKIP LOCKED drain; retry/dead-letter via applier |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/InboxConsumerJob.cs` | Same shape; silent success if payload is not `INotification` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/MessageProcessingResultApplier.cs` | Attempt / backoff / Dead |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/MessageProcessingStatus.cs` | `MaxAttempts = 5`, `2^n` minutes |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxMessage.cs` | Row shape |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/InboxMessage.cs` | Row shape |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/InMemoryEventBus.cs` | Process-local fan-out; no-handler is success |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxEventBus.cs` | Insert-only |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/TypeResolver.cs` | Caches `null` forever |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/DatabaseJobTrigger.cs` | In-process TCS; not multi-host |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/ModuleOutboxInboxServiceCollectionExtensions.cs` | Helper used by **CRM only** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxInboxModelBuilderExtensions.cs` | Shared EF mapping; most modules still inline |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/Configuration/BackgroundWorkerOptions.cs` | Intervals + claim lease; expiry/reminder **not** here |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/DocumentLinkSigner.cs` | HMAC; default secret is the JWT dev string |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/Observability/HealthReadiness.cs` | `/health/ready` lag gate **off** when threshold null |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/Observability/ObservabilityOptions.cs` | `OutboxLagReadyThreshold` default null |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/Observability/PlatformMetricsCollector.cs` | Schema-interpolated outbox SQL |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/ExecutionContextAccessor.cs` | HTTP Items → `TenantId`; empty in workers |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs` | Require vs exempt trees |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/DatabaseMigrationExtensions.cs` | Boot migrate; PendingModelChanges continues |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs` | Production JWT secret guard |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Configuration/AppOptions.cs` | **Never bound**; `ClientUrl` default **3020** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/appsettings.json` | `ClientUrl` 3004; `OutboxLagReadyThreshold` null; `Jwt:Secret` empty |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Ops/Infrastructure/OpsDbContext.cs` | Soft-delete + tenant override (fixed) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` | Claim SQL + `excludeIds` concat + auto-debit starve |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs` | Loads all campaigns `IgnoreQueryFilters` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.Claim.cs` | SKIP LOCKED + `processedIds` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/CheckoutSessionExpiryJob.cs` | No claim |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/InvoiceReminderJob.cs` | No claim; `GetService`; UTC day; `portal.lazuar.com` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs` | Mixed IgnoreQueryFilters / fail-closed ID lookups |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/EventHandlers/OrderCompletedIntegrationEventHandler.cs` | `GetOrderByIdAsync` under empty ambient |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/EventHandlers/SubscriptionLifecycleIntegrationEventHandlers.cs` | Live status on activated; optional billing |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs` | Org-scoped session load (fixed vs 008) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/IntegrationSubscriptionEndpoints.cs` | Status filter after page |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Subscribers.cs` | Loads **all** non-PENDING then pages in memory |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs` | `billing == null` ⇒ SST false |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs` | `alreadyConsolidated` without `IgnoreQueryFilters` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/DependencyInjection.cs` | RevenueRecognition **parked** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs` | SKIP LOCKED + lease |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs` | 300s skew on **verify** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Services/OneLinkService.cs` | Reads `IConfiguration`; fallback 3004 / 3003 |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs` | Path-id membership checks; whole tree tenant-exempt |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/AcceptWorkspaceInvitationCommand.cs` | No existing-membership guard |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/Infrastructure/Endpoints.cs` | `OrgAdmin` on notify (fixed vs 008) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/TenantProvisionedIntegrationEventHandler.cs` | **Only** production `new InboxMessage` writer family |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/Domain/MessageDeliveryLog.cs` | `OrganizationId` **without** `IMustHaveTenant` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/CRM/Infrastructure/CrmQueryService.cs` | ID-only `IgnoreQueryFilters` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/CRM/Infrastructure/AnonymizeClientProfileCommandHandler.cs` | Org + id (good) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/Workers/BroadcastFanoutJob.cs` | SKIP LOCKED + `Jwt:Secret` fallback |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/PublicComplianceEndpoints.cs` | Resend fail-closed outside Dev; HMAC = JWT secret |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Commands/WebhookCommands.cs` | Register still writes unused table |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Queries/LhdnQueries.cs` | List invents `events` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs` | 9 NetArchTest rules; no inbox/DI |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ArchitectureTests/TenantIsolationArchitectureTests.cs` | Source scan of two files + middleware allowlist |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/TenantIsolation/TenantIsolationHardeningTests.cs` | Includes tautology storage test |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/TenantIsolation/CrossTenantIdorTests.cs` | 8 handler IDORs; no pause |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.IntegrationTests/` | 10 methods; 2 Docker-gated / 2 Postgres-skip / 1 InMemory mis-shelved |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/main.tsp` | Now imports `integration-routes.tsp` (`cbe17c2`) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/honesty-allowlist.yaml` | 8 `impl_only`; 0 phantoms |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/payments/models.tsp` | Flat `PaymentWebhookPayloadDto` still labeled as envelope |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/commerce/models/webhooks.tsp` | Status union includes `TRIALING`; envelope still five types |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/scripts/check-openapi-minimal-honesty.mjs` | Path honesty only |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/contracts/openapi-vs-minimal-api.md` | Human companion; ADR 023 paragraph is stale |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/reference/events.md` | Human event SSoT; silent on `TRIALING` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-developers/app/webhooks/page.tsx` | Still teaches dead `/lhdn/webhooks` + body-only HMAC |

---

## 2. Isolation and worker mechanics (as the code is now)

### 2.1 Tenant identity

Tenant key is `Organization.Id` in schema `one`. Request binding is `HttpContext.Items["TenantId"]` set by `ApiKeyAuthenticationMiddleware` (key hash → org) or `TenantSecurityMiddleware` (`X-Tenant-Id` / `X-Tenant-Slug` / route `tenantSlug`). Platform routes hardcode `00000000-0000-0000-0000-000000000001`.

The host accessor is HTTP-only:

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

Every hosted service therefore has `TenantId == Guid.Empty` unless a future synthetic accessor is introduced. That is the entire reason workers must `IgnoreQueryFilters()` and then re-scope by `OrganizationId`.

### 2.2 Fail-closed EF filter (this is no longer the 008 fail-open)

```41:76:apps/lazuar-api/BuildingBlocks/Infrastructure/PlatformDbContext.cs
    private void ConfigureGlobalFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IMustHaveTenant
    {
        // Fail-closed: empty ambient TenantId matches no rows (workers must IgnoreQueryFilters + explicit org).
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            e.OrganizationId == ExecutionContext.TenantId);
    }
    // ...
            if (entry.Entity is IMustHaveTenant tenantEntity && tenantEntity.OrganizationId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    $"Cannot save {entry.Entity.GetType().Name} with empty OrganizationId. " +
                    "Set OrganizationId explicitly or ensure ambient TenantId is present for stamp.");
            }
```

Implications that 008’s gap analysis no longer states correctly:

1. An HTTP request with no tenant on a **non-required** path sees **zero** tenant rows through EF. That is fail-closed for reads.
2. A worker that forgets `IgnoreQueryFilters()` sees **zero** tenant rows. That is fail-closed for reads and is how several handlers now silently no-op (see B10-X07).
3. A worker that `IgnoreQueryFilters()` and then looks up **by id only** sees **every** tenant (see B10-X08, B10-X09).
4. Writes of `IMustHaveTenant` with empty `OrganizationId` throw. Workers must stamp org themselves.

Ops still replaces the base filter (EF allows one filter per entity). The replacement now includes the org predicate:

```31:39:apps/lazuar-api/Modules/Ops/Infrastructure/OpsDbContext.cs
        modelBuilder.Entity<OpsConversation>(builder =>
        {
            builder.ToTable("Conversations");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.OrganizationId);
            // Soft-delete AND tenant isolation (fail-closed; replaces PlatformDbContext tenant-only filter).
            builder.HasQueryFilter(x =>
                !x.IsDeleted &&
                x.OrganizationId == ExecutionContext.TenantId);
```

`TenantIsolationArchitectureTests.OpsDbContext_HasQueryFilter_Override_Must_Include_OrganizationId` locks the source string. No other `HasQueryFilter` exists in production code (grep of `*.cs` is Platform + Ops + the architecture test). Child tables without `IMustHaveTenant` are unfiltered.

### 2.3 Who is `IMustHaveTenant` (complete production list)

**One:** `ApiCredential`, `AuditEvent`, `TenantMembership`, `WorkspaceInvitation`, `TenantAppEntitlement`, `WebhookDeliveryOutbox`, `TenantWebhookEndpoint`. Not: `Organization`, `GlobalUser`.

**Commerce:** `Product`, `Subscription`, `Coupon`, `Order`, `CheckoutSession`, `DunningCampaign`, `CommerceDispute`, `CommerceTransactionLog`. Not: `ChargeAttemptLog`, `InvoiceReminderDispatchLog`, `ReminderDispatchLog`, `ProductPrice`, `DunningStep`.

**Billing:** `LedgerEntry`, `TenantCreditBalance`, `CreditHold`, `CreditDeductionIdempotencyLog`, `DocumentSequence`, `TenantBillingProfile`, `WorkspaceSaasSubscription`, `DeferredRevenueSchedule`. Not: `LedgerLine`.

**Lhdn:** `TaxDocument`, `LhdnTenantConfig`, `DeveloperApiKey`, `WebhookSubscription`, `TinValidateCache`, `IdempotencyLog`.

**Communications:** `MessageTemplate`, `TenantEmailConfiguration`, `SuppressionEntry`, `Broadcast`.

**CRM:** `ClientProfileEntity`.

**Payments:** `TenantPaymentConfiguration`, `IntegrationCheckoutSession`. Not: `PaymentWebhookLog` (global idempotency by provider + event id).

**Ops:** `OpsConversation`, `OpsMessage`.

**Messaging:** `MessageDeliveryLog` has `OrganizationId` and is **not** `IMustHaveTenant`. Delivery-log GET filters `OrganizationId == ctx.TenantId` in SQL by hand.

Architecture tests do **not** require “every type with an `OrganizationId` property implements `IMustHaveTenant`.” That hole is how `MessageDeliveryLog` and `ChargeAttemptLog` stay unfiltered forever.

### 2.4 HTTP tenant binding

Pipeline (host): authentication → API key middleware → `TenantSecurityMiddleware` → authorization.

```22:34:apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs
        if (context.User.Identity?.AuthenticationType == "ApiKey")
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api/v1/platform"))
        {
            context.Items["TenantId"] = Guid.Parse("00000000-0000-0000-0000-000000000001");
            await _next(context);
            return;
        }
```

Required prefixes: `/api/v1/admin`, `/lhdn`, `/ops`, `/messaging`, `/one/storage`, `/one/api-keys`. Missing header → 400 problem+json.

Exempt prefixes: `/health`, `/api/v1/public`, `/api/v1/webhooks`, `/one/public`, `/one/auth`, `/one/me`, `/one/workspaces`, `/one/integrations/workspaces`.

`/one/workspaces` is the whole tree, including `{id}/members`, `{id}/invites`, `{id}/webhooks`, `{id}/audit`. Ambient `TenantId` stays empty on those routes. Isolation is **handler-level** (`HasTenantAccessAsync` / `CanManageMembers` / webhook `CanAccessWorkspaceWebhooksAsync`). Fail-closed EF would hide `TenantMembership` / `WorkspaceInvitation` if a future handler used `DbSet` without `IgnoreQueryFilters`. Current One query service already ignores filters and predicates by path `id`. That is correct for a tenant-exempt tree and is also why a missed membership check is immediately an IDOR.

`/integrations/*` is **not** in `RequiresTenantContext`. Machine keys skip the middleware (API key already bound tenant). A cookie JWT hitting M2M commerce/payments with no `X-Tenant-Id` sees `ctx.TenantId == Empty` and the commerce integration endpoints 401 themselves.

### 2.5 Outbox / inbox spine

Write:

```
handler → keyed OutboxEventBus<T>.PublishAsync → Add(OutboxMessage)
       → same SaveChanges as the business write
       → PlatformDbContext pokes DatabaseJobTrigger
```

`OutboxEventBus` does not dispatch. `DatabaseJobTrigger` is a process-local `TaskCompletionSource` swap. Multi-instance correctness is the SQL poll, not the trigger.

Publisher (`OutboxPublisherJob<TDbContext>`):

- Transaction.
- `SELECT * FROM "{schema}"."{table}" WHERE ProcessedAt IS NULL AND (NextAttemptAt IS NULL OR <= NOW()) AND OccurredOn <= NOW() ORDER BY OccurredOn LIMIT 20 FOR UPDATE SKIP LOCKED`.
- `TypeResolver.Resolve` → deserialize → `InMemoryEventBus.PublishAsync`.
- Success / failure via `MessageProcessingResultApplier`.
- Commit once per batch of 20.
- Drain (`Task.Yield`) while the batch was non-empty; else wait trigger or 5 seconds.

Retry policy (tested in `MessageProcessingResultApplierTests`):

- `MaxAttempts = 5`.
- Backoff after increment: `2^n` minutes → 2, 4, 8, 16, then Dead on the 5th failure.
- Dead sets `Status = Dead`, `ProcessedAt = now`, increments `LazuarMetrics.RecordDeadLetter()`.

That is **no longer** the 008 “always mark processed on first throw” poison policy. The remaining poison story is: after five failures the row is **gone from the poll forever** and there is **no redrive API**.

Inbox consumer is the same SKIP LOCKED loop, then `IMediator.Publish` **if** the payload is `INotification`. Integration events implement `INotification`, so the Messaging hop works. If the type resolves to something that is not `INotification`, the consumer still `ApplySuccess` (B10-X04).

Who **writes** `new InboxMessage` in production code:

| File | Event |
|------|--------|
| `Modules/Messaging/Infrastructure/EventHandlers/TenantProvisionedIntegrationEventHandler.cs` | `TenantProvisionedIntegrationEvent` |
| `TenantUpdatedIntegrationEventHandler.cs` | `TenantUpdatedIntegrationEvent` |
| `WorkspaceUpdatedIntegrationEventHandler.cs` | `WorkspaceUpdatedIntegrationEvent` |

Tests construct `InboxMessage` only in `MessageProcessingResultApplierTests`. Every other module’s `*InboxConsumerJob` polls an empty table every 5 seconds (or on `JobTrigger`). CRM’s DbContext comment still says the tables exist “to satisfy platform job patterns.”

Cross-module consumers of payment / commerce / billing / LHDN events are **direct** `IIntegrationEventHandler<T>` registrations on the singleton `InMemoryEventBus`. They run **on the replica that claimed the producer outbox row**, in a **new DI scope**, while the producer outbox transaction still holds `FOR UPDATE` locks. Handler side effects are other schemas. Classic at-least-once: handler committed + publisher crash before outbox commit ⇒ retry ⇒ handler again. Classic at-most-once-after-dead: five throws ⇒ Dead.

`InMemoryEventBus` keys handlers by **runtime type name**. Duplicate subscribe is locked. If there are **no** handlers, it logs Information and **returns**. The publisher then `ApplySuccess`. That is silent drop (B10-X05).

`TypeResolver` caches the first result, including `null`:

```9:33:apps/lazuar-api/BuildingBlocks/Infrastructure/TypeResolver.cs
    public static Type? Resolve(string typeName)
    {
        return TypeCache.GetOrAdd(typeName, name =>
        {
            var resolvedType = Type.GetType(name);
            if (resolvedType != null)
            {
                return resolvedType;
            }

            var cleanName = name.Split(',')[0].Trim();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                resolvedType = assembly.GetType(cleanName);
                if (resolvedType != null)
                {
                    return resolvedType;
                }
            }

            return null;
        });
    }
```

A first resolve before the assembly is loaded (or against a stale assembly-qualified name after a type move) poisons that type name for the life of the process. The outbox row then burns five attempts and dies.

### 2.6 Hosted-service inventory (live DI)

| Module | Outbox job | Inbox job | Other hosted | Helper? |
|--------|------------|-----------|--------------|---------|
| One | yes | yes | Genesis, `OutboundWebhookDispatcherJob` | no |
| Messaging | yes | yes | — | no |
| CRM | yes | yes | — | **yes** (`AddModuleOutboxInbox`) |
| Payments | yes | yes | — | no |
| Ops | yes | yes | — | no |
| Billing | yes | yes | `B2cConsolidationJob`; `RevenueRecognitionJob` **commented out** | no |
| Lhdn | **yes** (008 said no; now registered) | yes | Submit, poll, seeder | no |
| Commerce | yes | yes | BillingEngine, DunningEngine, CheckoutSessionExpiry, InvoiceReminder | no |
| Communications | yes | yes | BroadcastFanout | no |

Architecture test `All_Modules_Should_Have_OutboxPublisherJob_In_Infrastructure` only requires a **type whose name ends with `OutboxPublisherJob`**. It does not require `AddHostedService`. CRM has a dedicated registration test. Lhdn has `LhdnOutboxPublisherJobRegistrationTests` (outbox only — inbox registration is untested). Removing `AddHostedService<CommerceOutboxPublisherJob>()` would still pass NetArchTest.

### 2.7 Domain pollers vs SKIP LOCKED

| Worker | Relational claim | In-memory claim | Interval |
|--------|------------------|-----------------|----------|
| Outbox / Inbox (all 9+9) | `FOR UPDATE SKIP LOCKED` LIMIT 20 | N/A (SQL) | 5s + trigger |
| `OutboundWebhookDispatcherJob` | SKIP LOCKED LIMIT 50 + `ClaimLease` | Take 50 | 10s |
| `BroadcastFanoutJob` | SKIP LOCKED LIMIT 20 + `MarkSending` | Take 20 | 10s |
| `LhdnSubmissionJob` / `LhdnStatusPollingJob` | SKIP LOCKED LIMIT 50 + lease on `NextPollAt` | Take 50 | 5s / 10s |
| `BillingEngineJob` | SKIP LOCKED LIMIT 1 + in-process `failedIds` | LINQ + `failedIds` | 1h |
| `DunningEngineJob` | SKIP LOCKED LIMIT 1 + `failedIds` **and** `processedIds` | same | 1h |
| `CheckoutSessionExpiryJob` | **none** — load all expired OPEN | same | **5m hard-coded** |
| `InvoiceReminderJob` | **none** — load all OPEN custom with `DueAt` | same | **1h hard-coded** |
| `B2cConsolidationJob` | **none** — full pending scan | same | 28th 02:00 MYT + catch-up |
| `RevenueRecognitionJob` | exists, **unregistered** | — | — |

`BackgroundWorkerOptions` does not include expiry or invoice-reminder intervals. Those two cannot be tuned without a code change.

In-memory claim paths exist so EF InMemory module tests can call `RunOnceAsync`. They do **not** prove `FOR UPDATE SKIP LOCKED`. There is no Testcontainers two-worker claim test anywhere in `Lazuar.IntegrationTests` (10 methods total; none touch outbox or billing claim SQL).

### 2.8 Clock surfaces

| Surface | Clock | Skew / day-cut |
|---------|-------|----------------|
| Outbox poll `OccurredOn <= NOW()` / `NextAttemptAt <= NOW()` | Postgres `NOW()` | Session TZ vs UTC-stored timestamps |
| Billing / dunning claim SQL | `NOW()` | Same |
| Billing / dunning in-memory claim | `DateTime.UtcNow` | Tests pass; prod uses SQL |
| Invoice reminder offset | `DateTime.UtcNow.Date` vs `DueAt.Date` | A MYT-due quote at 08:00 MYT (00:00 UTC) fires the “day 0” mail one calendar day early in Malaysia |
| Outbound webhook **sign** | `DateTimeOffset.UtcNow` unix | Receiver default 300s (`TryVerify`) |
| Resend Svix | 300s on `svix-timestamp` | Same |
| Document HMAC | exact `exp` unix, no skew window | A slow clock on the API host expires links early |
| B2C schedule | `Asia/Kuala_Lumpur` / fallback `Singapore Standard Time` | Catch-up exists (008 missed-month bug is closed) |
| Magic-link / unsubscribe HMAC | no timestamp on unsubscribe; magic-link has its own exp | Unsubscribe never expires |

---

## 3. Quoted walk (the paths this slice actually executes)

### 3.1 A tenant-scoped admin GET

1. Cookie JWT authenticates.
2. `TenantSecurityMiddleware` requires `X-Tenant-Id` on `/api/v1/admin/...`.
3. Membership role is injected; no membership → 403.
4. EF global filter keeps `OrganizationId == TenantId`.
5. Dapper query services **also** pass `@OrgId` (defense in depth). This is the healthy path.

### 3.2 A worker tick (billing)

1. Hosted loop, empty ambient tenant.
2. If relational: `BEGIN`; `ClaimDueSubscriptionAsync` interpolates `failedIds` into `NOT IN ('guid',...)`; `FOR UPDATE SKIP LOCKED`; `IgnoreQueryFilters()`.
3. `ProcessOneSubscriptionAsync` loads product with `IgnoreQueryFilters()` by **product id only**.
4. Pause is now in SQL **and** added to `failedIds` (911d358). Tests `RunOnce_CollectionPausedDue_SiblingStillProcessed` and `RunOnce_CollectionPaused_SecondCycleDoesNotStarveSibling` lock that.
5. Auto-debit success path inserts `ChargeAttemptLog` (not tenant-filtered — so the count works with empty ambient) and **returns without adding `sub.Id` to `failedIds`**. Next iteration of the same `RunOnce` can reclaim the same due ACTIVE row (B10-X01).
6. Reminder-only path uses `GetService<ICrmQueryService>()` / `IMediator` / `IOneQueryService` / `IMagicLinkTokenService` / `IBillingQueryService`. CRM missing ⇒ no email ⇒ PAST_DUE **without** a checkout URL. Billing missing ⇒ SST treated as false (B10-X11, B10-X12).

### 3.3 An integration event (order completed)

1. Commerce outbox row claimed on some replica.
2. `InMemoryEventBus` opens a **new** scope (empty tenant).
3. `OrderCompletedIntegrationEventHandler` calls `GetOrderByIdAsync` — **no** `IgnoreQueryFilters`:

```78:81:apps/lazuar-api/Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs
    public async Task<CheckoutSession?> GetCheckoutSessionByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.CheckoutSessions.FirstOrDefaultAsync(s => s.Id == id, ct);
    }
```

```119:122:apps/lazuar-api/Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs
    public async Task<Order?> GetOrderByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
    }
```

4. Filter: `OrganizationId == Guid.Empty` matches nothing. `order` is null.
5. Webhook payload uses `quantity = order?.Quantity ?? 1`. A qty-3 one-time purchase notifies integrators of qty 1 (B10-X07).
6. Handler still publishes `order.completed` and `SaveChanges` (outbox insert for the outbound request). The lie is in `data.quantity`.

Contrast: `GetSubscriptionByIdAsync` **does** ignore filters (by id only). Lifecycle webhooks see the real subscription. That inconsistency is the isolation model: two methods on the same repository, two opposite default postures.

### 3.4 Inbox (Messaging only)

1. One writes `TenantProvisionedIntegrationEvent` to `one.OutboxMessages`.
2. `OneOutboxPublisherJob` publishes on `InMemoryEventBus`.
3. `TenantProvisionedIntegrationEventHandler` inserts `messaging.InboxMessages` with the event’s assembly-qualified name and JSON.
4. `MessagingInboxConsumerJob` SKIP LOCKED, deserializes, `mediator.Publish` because the type is `INotification`.
5. Seeding handlers run.

No other module has a step 3. Lhdn and CRM outbox publishers **do** exist now (008 P0 closed). Their **inbox** tables still have no writers.

### 3.5 Honesty gate (`cbe17c2` re-verify)

Live run:

```
unresolved call receiver 'SubscriberEndpoints.MapPreview' in MapPublicPortalEndpoints (...)
unresolved call receiver 'ResendWebhookParser.MapReason' in MapPublicComplianceEndpoints (...)
OpenAPI operations:  152
Minimal operations:  160
impl_only allowlist: 8
openapi_only_ex:     0
OpenAPI ↔ Minimal path honesty OK (152 OpenAPI, 160 Minimal, 8 impl_only).
exit=0
```

`main.tsp` line 18 now imports `./modules/commerce/integration-routes.tsp`. Allowlist has eight `impl_only` rows including **both** GET and POST `/public/communications/unsubscribe` (008’s fourth undocumented path). Combined OpenAPI therefore contains the three M2M commerce routes. Path honesty is closed.

The scraper still warns on `MapPreview` / `MapReason` because those are `Map*` name collisions, not HTTP maps. Soft. Not a fail.

Path honesty is **not** payload honesty. `PaymentWebhookPayloadDto`, VitePress `TRIALING`, and `/lhdn/webhooks` are outside the script (B10-X16, B10-X17, B10-X03).

---

## 4. Bug catalog

IDs are `B10-Xnn`. Severity is P0 / P1 / P2. “008” means the same shape appeared in `plans/008-evals/09` or `08`. “New” means 008 did not file it as a live bug in this slice (or filed it as already-fixed incorrectly).

### B10-X01 — P0 — Billing auto-debit claim starve (sibling of the pause bug 911d358 closed)

**File:** `Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` 253–286.

After a due vaulted subscription is claimed, the job writes attempt 1 and publishes `ExecuteOffSessionChargeIntegrationEvent`, then `return`s. It does **not** `failedIds.Add(sub.Id)`. `NextBillingDate` is still in the past. Status is still `ACTIVE` / `TRIALING`.

Next of the 50 iterations: new transaction, `FOR UPDATE SKIP LOCKED`, `ORDER BY "NextBillingDate"`. The same row is first. `attemptCount == 0` is now false, so it does not double-charge. It **does** consume the slot. One waiting-on-gateway subscription can burn the rest of the hourly batch the same way a paused row did before 911d358.

Dunning does **not** have this hole: it always `processedIds.Add(sub.Id)` after a successful process.

The existing pause tests do **not** cover this path. `RunOnce_CollectionPausedDue_SiblingStillProcessed` only asserts the paused sibling. There is no test “two due vaulted subs, first already has attempt 1, second still charges in the same `RunOnce`.”

008 §3.1 already named this as “milder form of the same claim loop.” 911d358 fixed pause SQL + `failedIds` on the pause skip. It did not add the dispatched-charge id to `failedIds`. The milder form is still live.

Cite only: this is an isolation/concurrency/ops bug, not 02’s product claim-logic.

### B10-X02 — P0 — B2C `alreadyConsolidated` is a no-op under fail-closed filters

**File:** `Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs` 209–219.

```209:219:apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs
        var alreadyConsolidated = await db.LedgerEntries.AnyAsync(e =>
            e.OrganizationId == orgId
            && e.TaxInvoiceId == consolidationRef, ct);

        if (alreadyConsolidated)
        {
            _logger.LogInformation(
                "Skipping B2C consolidation for Org {OrgId} period {Period} — already issued ({Ref}).",
                orgId, periodKey, consolidationRef);
            return;
        }
```

`LedgerEntry` is `IMustHaveTenant`. The worker’s ambient tenant is empty. `AnyAsync` without `IgnoreQueryFilters()` is **always false**. The skip never fires in a real hosted process.

Re-entry protection is the earlier pending-status query (that one **does** ignore filters). A crash **after** `PublishAsync` (outbox insert) and **before** `MarkConsolidatedPending` + `SaveChanges` can double-publish `ConsolidatedInvoiceIssuedIntegrationEvent`. LHDN must be idempotent on `B2C-CONS-{yyyyMM}-{org}`. That is an assumed consumer property, not a producer lock.

`B2cConsolidationJobTests` (7, InMemory) never assert the `alreadyConsolidated` branch under empty ambient + fail-closed filters. InMemory + empty tenant also hides rows, so the test job’s `IgnoreQueryFilters` pending query is what they exercise. The broken `AnyAsync` is untested.

008 §3.4 filed this. Still present.

### B10-X03 — P0 — `POST /lhdn/webhooks` is a live dead register; Developers hub still teaches it

**Files:** `Modules/Lhdn/Application/Commands/WebhookCommands.cs` 26–32; `Modules/Lhdn/Application/Queries/LhdnQueries.cs` 129–136; `apps/lazuar-developers/app/webhooks/page.tsx` 223–258.

Register persists `lhdn.WebhookSubscriptions` (url + secret, **no events column**). List invents `Events = ["invoice.valid", "invoice.invalid"]`. Dispatch does **not** read that table. Runtime LHDN deliveries are `OutboundWebhookRequestedIntegrationEvent` → One `TenantWebhookEndpoints` → `t=,v1=` envelope.

The Developers hub still says:

- register via `POST /lhdn/webhooks`
- emit JSON with top-level `event`
- “LHDN path currently signs with HMAC-SHA256 hex of the raw body”

All three sentences are false after R43. An ERP that follows Scalar LHDN / Kiota `WebhooksRequestBuilder` / that page will persist a row that never receives a delivery.

TypeSpec still documents `/lhdn/webhooks` as a first-class product route (`lhdn/routes.tsp`). Honesty is **green** because the Maps exist. Honesty does not ask “does anyone read the table.”

008 H1. Still present. `cbe17c2` did not touch this.

### B10-X04 — P1 — Inbox consumer marks success when the payload is not `INotification`

```70:76:apps/lazuar-api/BuildingBlocks/Infrastructure/InboxConsumerJob.cs
                            var inboxEvent = JsonSerializer.Deserialize(message.Data, eventType);
                            if (inboxEvent is INotification notification)
                            {
                                await mediator.Publish(notification, stoppingToken);
                            }

                            MessageProcessingResultApplier.ApplySuccess(message, DateTime.UtcNow);
```

If `TypeResolver` returns a type that deserializes but is not `INotification`, the row is processed with no handler and no error. Contrast outbox: non-`IIntegrationEvent` **throws** and goes through `ApplyFailure`.

Today the only writers serialize integration events (which are `INotification`). The branch is still a landmine for the next inbox writer who serializes a command DTO.

No test covers this branch. `MessageProcessingResultApplierTests` only test the applier, not the job loop.

### B10-X05 — P1 — `InMemoryEventBus` treats “no handlers” as success

```32:36:apps/lazuar-api/BuildingBlocks/Infrastructure/InMemoryEventBus.cs
        if (!_handlers.TryGetValue(eventName, out var handlers))
        {
            _logger.LogInformation("Event {EventName} was published but has no registered handlers.", eventName);
            return;
        }
```

Outbox then `ApplySuccess`. A missing `Use*Subscriptions` call, a typo in `Subscribe<TEvent, THandler>`, or a handler registered against the compile-time interface name rather than the runtime type, **drops the event permanently** (one Information log). This is how a forgotten Lhdn subscription would have failed closed-looking (“outbox drained”) while Billing never saw `LhdnDocumentValidated`.

There is no architecture test that every `IIntegrationEvent` has at least one `Subscribe`.

### B10-X06 — P1 — `TypeResolver` caches null for the process lifetime

Quoted in §2.5. Combined with B10-X05 / retry: unresolvable type → five `ApplyFailure` → Dead. Combined with a late-loaded plugin assembly: first outbox of a new event type after a partial deploy can Dead-letter every row of that type until restart — and after restart the AQN might work, but the Dead rows will not be polled (`ProcessedAt` set).

No test of `TypeResolver` exists under `Lazuar.ModuleTests/BuildingBlocks`.

### B10-X07 — P1 — Repository ID lookups that **keep** the fail-closed filter (workers see nothing)

`GetCouponByIdAsync`, `GetCheckoutSessionByIdAsync`, `GetOrderByIdAsync` do **not** call `IgnoreQueryFilters()`. Under empty ambient they return null.

HTTP handlers that run with `X-Tenant-Id` are fine (filter matches). Event handlers / workers are not.

Concrete lie: `OrderCompletedIntegrationEventHandler` (quoted in §3.3) emits `quantity: 1` whenever the real order is invisible.

`GetCouponByIdAsync` in `ProcessZeroAmountCheckoutCommand` / `MarkCheckoutAsPaidOfflineCommandHandler` is HTTP-scoped — OK today. If either is ever called from a bus handler, coupon reservation release silently no-ops.

`HasSubscriptionsAssignedToCampaignAsync` and `HasAnyDunningCampaignAsync` also keep the filter. A worker asking “is this campaign in use?” with empty ambient gets `false` and may allow a delete that still has PAST_DUE rows.

### B10-X08 — P1 — Repository ID lookups that **ignore** filters without an org predicate

```22:28:apps/lazuar-api/Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs
    public async Task<Product?> GetProductByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Products
            .IgnoreQueryFilters()
            .Include(p => p.Prices)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }
```

Same shape: `GetSubscriptionByIdAsync`, `GetTransactionLogByIdAsync`.

`CrossTenantIdorTests` prove **some** command handlers re-check `OrganizationId` after the load. They do not prove every caller. `SubscriptionLifecycleIntegrationEventHandlers` loads subscription by id only, then uses `sub.Status` and `sub.OrganizationId` from the **row**, not from the event, for payload status. If a future bug ever passed the wrong id, this would leak another tenant’s commercial fields into a webhook signed as the event’s org.

`GetProductByIdAsync` in the same handler is not org-scoped. Product GUIDs are unique; the residual risk is a swapped id, not a guess.

Architecture tests do **not** ban `IgnoreQueryFilters()` without an `OrganizationId` predicate.

### B10-X09 — P1 — `CrmQueryService.GetClientProfileAsync` is a global PII read by GUID

```54:62:apps/lazuar-api/Modules/CRM/Infrastructure/CrmQueryService.cs
    public async Task<ClientProfileDto?> GetClientProfileAsync(Guid profileId)
    {
        var profile = await _dbContext.ClientProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == profileId);
```

Returns name, email, phone, TIN, company, id numbers, address. Any in-process caller with a profile GUID (Commerce document lookup, lifecycle webhooks, billing engine, arrears) can read another tenant’s CRM row. `GetClientProfilesAsync(IEnumerable<Guid>)` is the same.

`GetClientProfileByEmailAsync` **does** take `organizationId`. The id-based overloads do not.

This is the widest remaining `IgnoreQueryFilters` leak that is not “worker must see all orgs.” CRM resolve/create **do** constrain org. The query service used everywhere else does not.

### B10-X10 — P1 — Invoice reminder and checkout expiry have no claim

`CheckoutSessionExpiryJob` loads every OPEN session with `ExpiresAt < now` (`IgnoreQueryFilters`), expires them, releases coupons, one `SaveChanges`. Two API replicas on the same 5-minute tick both expire the same set. `Expire()` is likely idempotent; `ReleaseReservation()` is **not** — coupon remaining uses can increment twice.

`InvoiceReminderJob` loads every OPEN custom session with `DueAt != null` (unbounded), computes UTC day offset, inserts `InvoiceReminderDispatchLog`. Unique index `(SessionId, DayOffset)` is the only interlock. Two replicas: one `SaveChanges` throws; the fulfillment event may already be in the Commerce outbox of **both** if `PublishAsync` ran before save. The job publishes **then** adds the log **then** saves once at the end. A mid-loop exception loses the log insert but keeps in-memory outbox entries that were never saved — actually `PublishAsync` only stages on the same `CommerceDbContext`, so a throw before `SaveChanges` loses both. Two successful replicas: both stage outbox + log; unique index fails one `SaveChanges`; the winner’s outbox commits. The loser rolls back. That is OK **if** EF InMemory is not production. On Postgres the unique violation is an exception in `SaveChanges` — the **entire** batch of reminders in that process rolls back, including ones that did not collide, because there is one save for the whole job.

No SKIP LOCKED. No per-session transaction. Tests: 3 reminder tests (in-process double `RunOnce` is unique-index safe); expiry is one method inside `CommerceProductCompletenessTests`.

### B10-X11 — P1 — `GetService` SST fail-open (undercharge)

```65:73:apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs
    public static async Task<bool> MerchantHasSstAsync(IBillingQueryService? billing, Guid organizationId)
    {
        if (billing == null)
        {
            return false;
        }
```

`BillingEngineJob` and `DunningEngineJob.Claim` resolve `IBillingQueryService` with `GetService` (optional). If the billing module is not composed (test host, future extract, registration typo), every renewal and dunning AUTO_CHARGE is **net, no SST**. Production `AddAllModules` registers billing, so this is a composition footgun, not today’s happy path.

Public arrears also `GetService<IBillingQueryService>()` (`PublicArrearsEndpoints.cs` 56, 143). Same fail-open on the buyer-facing amount.

`SubscriptionLifecycleIntegrationEventHandlers` takes `IBillingQueryService? billingQueryService = null`. In production DI this is resolved. In tests that construct the handler without it, webhook `amount` is net.

Fail-**open** (charge too little) rather than fail-closed (refuse to bill).

### B10-X12 — P1 — `GetService` CRM / One / tokens / config fail-open on money comms

`BillingEngineJob` reminder-only path:

- `crm == null` or no email → PAST_DUE **without** minting a renewal checkout (warning log).
- `mediator` / `one` / `tokens` null → **throws** (fail-closed for mint). Asymmetric.

`InvoiceReminderJob`:

```61:62:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/InvoiceReminderJob.cs
        var one = scope.ServiceProvider.GetService<IOneQueryService>();
        var config = scope.ServiceProvider.GetService<IConfiguration>();
```

```85:106:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/InvoiceReminderJob.cs
        var portalBase = (config?["App:ClientUrl"] ?? "https://portal.lazuar.com").TrimEnd('/');
        // ...
            var workspace = one == null ? null : await one.GetWorkspaceByIdAsync(session.OrganizationId);
            var slug = workspace?.Slug ?? "";
            var payUrl = string.IsNullOrEmpty(slug)
                ? $"{portalBase}/pay/{session.Id}"
                : $"{portalBase}/{slug}/pay/{session.Id}";
```

`one == null` or workspace miss ⇒ email contains `https://portal.lazuar.com/pay/{guid}` with **no tenant slug**. That URL is not the portal’s `/{tenantSlug}/pay/{sessionId}` route. Buyer gets a 404. Job still records the dispatch log. The −3 / 0 / +3 unique index then **prevents a correct retry** after One is fixed.

### B10-X13 — P1 — `AppOptions.ClientUrl` default 3020 is unbound; three other fallbacks disagree

```7:10:apps/lazuar-api/src/Lazuar.Api/Configuration/AppOptions.cs
    /// The primary client-facing frontend URL (portal / public checkout surfaces, typically port 3020).
    /// </summary>
    public string ClientUrl { get; init; } = "http://localhost:3020";
```

Grep of `Configure<AppOptions>` / `IOptions<AppOptions>` is **empty**. The type is documentation that lies. Port **3020** is `examples/hub-cashier-next`, not the portal (3004) and not ops (3003).

Live readers:

| Reader | Fallback if `App:ClientUrl` missing |
|--------|--------------------------------------|
| `appsettings.json` | `http://localhost:3004` (present, so OK when config is loaded) |
| `OneLinkService` | `http://localhost:3004` |
| `PublicArrearsEndpoints` | `http://localhost:3004` |
| `InvoiceReminderJob` | `https://portal.lazuar.com` |
| Communications fulfillment / lifecycle / portal-access / digital-delivery / payment-failed handlers | `https://portal.lazuar.com` |

`297ba98` correctly mints invite URLs from `App:OpsUrl` (3003). Buyer recovery links still have two hosts and a fictional `portal.lazuar.com`.

### B10-X14 — P1 — JWT secret is the HMAC key for documents, unsubscribe, magic links, and (fallback) vault

`DocumentLinkSigner.ResolveSecret` uses `Jwt:Secret` or `"secure_development_key_minimum_32_characters_long"`.

Same secret: unsubscribe query HMAC, broadcast unsubscribe URLs, magic-link tokens (`MagicLinkTokenService` fallback `"fallback_dev_secret_key"` — a **fourth** string), `AesSecretVault` if `Kms:MasterKey` empty, LHDN certificate vault same fallback.

`appsettings.json` has `"Jwt:Secret": ""`. Non-Production therefore signs JWTs and document links with the well-known 32-char dev string (`AuthAndCorsExtensions` 31). Production throws if empty or default. Staging-shaped environments that are not `IsProduction()` ship forgeable document URLs and unsubscribe tokens.

`DocumentLinkSigner.TryValidate` has **no** clock-skew window. A 1s skew past `exp` fails closed (good for security, bad for “link emailed at T-1s”).

### B10-X15 — P1 — M2M `?status=` filters the current page and rewrites `total_count`

```37:42:apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/IntegrationSubscriptionEndpoints.cs
            if (!string.IsNullOrWhiteSpace(status))
            {
                var filtered = response.Data.Where(s =>
                    string.Equals(s.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();
                response = new PaginatedResponse<CommerceSubscriptionDto>(filtered, filtered.Count, p, l);
            }
```

`GetSubscribersAsync` already loaded **every** non-`PENDING` row for the org (no SQL `LIMIT`), then paged in memory (`CommerceQueryService.Subscribers.cs` 52–72). The endpoint then filters **that page** and sets `total_count` to the filtered page size. `GET ?status=TRIALING&page=1` can return 3 rows and `total_count=3` when the tenant has 40 trials.

008 H9. Still present. `cbe17c2` added the paths to combined OpenAPI; it did not fix the semantics. Honesty cannot see this.

The unbounded load is itself a P2 ops risk at large subscriber counts (admin list and M2M share the query).

### B10-X16 — P1 — `PaymentWebhookPayloadDto` is still not the wire

```50:66:packages/api-spec/modules/payments/models.tsp
/**
 * Outbound payment.* webhook envelope (snake_case JSON body).
 * Signed with X-Lazuar-Signature: t=…,v1=… (HMAC-SHA256 of "{t}.{rawBody}").
 */
model PaymentWebhookPayloadDto {
  event_id: string;
  event_type: "payment.completed" | "payment.failed";
  checkout_id: string;
  workspace_id: string;
  ...
}
```

Runtime wrap is `{ id, event_type, created_at, data: { ... } }`. The DTO is flat, claims to be the envelope, invents `workspace_id` / `occurred_at`, omits `provider_session_id` / `description` / `customer_email` / `gateway` as `data` fields. Generated `FromJson` against a live delivery will not bind `data.*`.

VitePress `events.md` line 31 warns. TypeSpec comment still lies. Sample `examples/hub-cashier-next/lib/types.ts` is the honest client. `@repo/api-types-ts` is not.

008 H3. Still present after `cbe17c2`.

### B10-X17 — P1 — Human catalog and lifecycle tests still describe a four-status world

`apps/lazuar-docs/docs/reference/events.md` line 46: `subscription.activated` = “First paid period or recovery that lands `ACTIVE`.” After Wave 3 a trial start emits `event_type=subscription.activated` and `data.status=TRIALING`, `amount=0`. Grep of `apps/lazuar-docs/**/*.md` for `TRIALING` is empty.

`SubscriptionLifecycleWebhookTests.Payload_FiveEventTypes_ShareRequiredFields` parametrizes only `ACTIVE | PAST_DUE | CANCELED | SUSPENDED`. There is no case that `ActivateTrial` and asserts `data.status == TRIALING`.

Generated clients **do** include `TRIALING` after `cbe17c2` (`packages/api-types-ts/src/index.ts` 2798; C# enum member `TRIALING = 4`). 008 H4 is **half-closed**: clients caught up; catalog and the webhook test suite did not.

Ops picker hint still says “New **paid** subscription” (out of this slice’s primary trees, cited as contract honesty).

### B10-X18 — P1 — Dead letters have metrics and no redrive

`MessageProcessingResultApplier.ApplyFailure` at max attempts sets `ProcessedAt` so the poll (`ProcessedAt IS NULL`) never sees the row again. `PlatformMetricsCollector` counts `Status = 'Dead'`. `/health/ready` **does not** fail on dead letters.

`Observability:OutboxLagReadyThreshold` is **null** in `appsettings.json`. `HealthReadiness` then skips the lag gate:

```39:46:apps/lazuar-api/BuildingBlocks/Infrastructure/Observability/HealthReadiness.cs
        if (options.OutboxLagReadyThreshold is not { } lagThreshold || lagThreshold <= TimeSpan.Zero)
        {
            return new Result(
                IsReady: true,
                Status: "ready",
                ...
```

A replica with a 3-day outbox backlog and a pile of Dead LHDN events is **ready**. Docker healthcheck is HTTP liveness. Fail-open for ops.

No admin API resets `ProcessedAt` / `Status`. Replay is raw SQL.

### B10-X19 — P1 — Boot `MigrateAsync` continues on `PendingModelChanges`

```53:59:apps/lazuar-api/src/Lazuar.Api/Composition/DatabaseMigrationExtensions.cs
            catch (InvalidOperationException ex) when (ex.Message.Contains("PendingModelChanges", StringComparison.Ordinal))
            {
                migratorLog.LogError(ex,
                    "MigrateAsync blocked for {DbContext} by pending model changes. Module tables may be missing.", name);
            }
```

The process comes up. Workers then throw every hour on missing columns. Billing DI also `ConfigureWarnings(Ignore PendingModelChangesWarning)`, so a forgotten Billing migration is even less visible.

XML-doc on the same type admits multi-instance `MigrateAsync` races. Wave 3 commerce migrations include data backfills (`UPDATE` UnitAmount, `INSERT ProductPrices`). Two rolling pods can run those together.

No integration test calls `MigrateAllModuleDatabasesAsync`.

### B10-X20 — P1 — Accept-invite does not check existing membership and does not audit

```36:41:apps/lazuar-api/Modules/One/Application/Commands/AcceptWorkspaceInvitationCommand.cs
        invitation.Accept();

        var membership = new TenantMembership(user.Id, invitation.OrganizationId, invitation.Role);
        _repository.AddTenantMembership(membership);

        await _repository.SaveChangesAsync(ct);
```

`AcceptWorkspaceInvitationCommandHandlerTests` covers happy / expired / wrong email. It does **not** cover: already a member, second accept of a still-PENDING row (status check should stop this), inactive user, bad token, duplicate unique (org, user) if one exists.

Invite create writes `AuditEvent`. Accept does not (`IAuditRecorder` is not even a constructor parameter).

`297ba98` added the ops `/accept-invite` page and `OneLinkService.GetOpsBaseUrl`. The handler hole remains.

### B10-X21 — P1 — `/one/workspaces` exemption + empty ambient is a loaded gun

Middleware exempts the entire prefix. Endpoints now mostly call `HasTenantAccessAsync`. That is better than 008’s IDOR. Residual:

- `POST /workspaces/{id}/invites` and `DELETE .../members/{userId}` rely on the **handler** (`CanManageMembers` or `IsSystemAdmin`), not on middleware tenant match. Path `id` is the org. A logged-in ADMIN of org A cannot invite into org B (no membership). A `SUPER_ADMIN` / system admin **can** (intentional).
- VIEWER of org A can `GET .../members` and `GET .../audit` (access, not manage). That is product.
- Any new Map under `/workspaces/` that forgets `HasTenantAccessAsync` is an IDOR **and** the architecture middleware test will still pass, because the path is on the exempt list.

`TenantIsolationArchitectureTests.TenantSecurityMiddleware_Exempts_Public_Auth_Webhooks_And_Workspace_Surfaces` **locks the exemption in**. It is a test that documents the gun.

### B10-X22 — P1 — `excludeIds` SQL concatenation (billing + dunning)

```129:131:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs
        var excludeClause = excludeIds.Count == 0
            ? ""
            : $""" AND "Id" NOT IN ({string.Join(",", excludeIds.Select(id => $"'{id}'"))})""";
```

Same in `DunningEngineJob.Claim.cs` 99–101. Values are `Guid`s from our process, not user input. Injection risk is low. Residual:

- Not parameterized; plan cache churn every distinct set.
- Default Guid format in SQL is culture-sensitive in theory (`Guid.ToString()` is `D` format, invariant). Fine in practice.
- The hunt named this specifically. It is the only user-facing-adjacent dynamic SQL in the claim path. `FromSqlRaw` with interpolated GUIDs is the same family as BillingEngineJob `excludeIds` in the 009 brief.

Schema/table interpolation in outbox/inbox jobs comes from EF model metadata, not request data. Same pattern, trusted source.

### B10-X23 — P1 — Child / log tables with `OrganizationId` (or session id) and no tenant filter

`ChargeAttemptLog : Entity` — no org column. Count-by-subscription works globally. A guessed `SubscriptionId` + date is not HTTP-reachable today; workers see all orgs by design.

`InvoiceReminderDispatchLog` — `(SessionId, DayOffset)` only. Unique lock is global. Fine.

`MessageDeliveryLog` has `OrganizationId` and is **not** `IMustHaveTenant`. `GET /messaging/delivery-logs` filters by `ctx.TenantId` in LINQ. Any other `DbSet<MessageDeliveryLog>` query with empty ambient sees **all tenants’** recipient addresses. Architecture tests do not require the interface.

`PaymentWebhookLog` is intentionally global (provider EventId idempotency). Forensics are not org-partitioned. 008 noted this; still true.

### B10-X24 — P1 — Eight idle inbox pollers + one global trigger

Nine inbox consumers × `SELECT ... LIMIT 20 FOR UPDATE SKIP LOCKED` every 5 seconds, plus nine outbox pollers, plus `JobTrigger` waking **every** module on **any** successful `SaveChanges`. Cheap per query (filtered index), chatty on Neon (`Maximum Pool Size=50`). CRM and Ops inboxes are structurally unused.

`AddModuleOutboxInbox` exists to make this consistent and is used by CRM only. Other modules hand-register the same three lines. Drift risk, not a functional bug.

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

### B10-X26 — P2 — Tests that pin bugs, tautologies, or never run

**Tautology**

```279:288:apps/lazuar-api/tests/Lazuar.ModuleTests/TenantIsolation/TenantIsolationHardeningTests.cs
    public void Presigned_Storage_Rejects_Empty_Tenant_Contract()
    {
        var tenantId = Guid.Empty;
        tenantId.Should().Be(Guid.Empty);
        Assert.That(tenantId == Guid.Empty, Is.True);
    }
```

The real guard is `StorageEndpoints.cs` 27–32. This test does not call it. Name claims a contract. Body is `true == true`.

**Assert.Pass as a stand-in for a behavior test**

`LhdnDocumentSubmittedIntegrationEventHandlerTests.HandleAsync_CompletesWithoutWalletOrMediatorDependencies` ends `Assert.Pass("... does not call wallet...")`. The sibling test that inspects constructor parameters is the real lock. The Pass test is a comment.

**Always skipped**

`LhdnSandboxE2ETests` is `[Ignore("Requires active Sandbox credentials...")]`. Two `[Test]` methods never run in CI. The class `SetUp` throws if env vars are missing — dead code under Ignore.

**Skip when Docker / Postgres missing**

- `CreditDeductionConcurrencyTests` (3): Testcontainers; `_postgresReady` → `Assert.Ignore`. The **only** suite that proves Billing migrations + real `xmin`-adjacent concurrency.
- `BillingQueryServiceTests` (2): opens `localhost:5432` or `LAZUAR_TEST_PG`; Ignore if down. Then **creates ad-hoc** `LedgerEntries` / `LedgerLines` tables if missing — **not** the EF model. A Dapper query can pass against a toy schema that production migrations would have altered.
- `CommerceQueryServiceTests` (4): Testcontainers **without** try/catch. Docker down ⇒ fixture **throws**, not Ignore. CI `dotnet` job has a Postgres service but this fixture starts **its own** container. Depending on runner Docker-in-Docker, this either proves Wave 3 commerce migrations or reds the whole class.

**InMemory mis-shelved as Integration**

`BillingDbContextTests` (1): EF InMemory. Lives in `Lazuar.IntegrationTests` because of history. It does not integrate.

**Pins an incomplete world**

- `SubscriptionLifecycleWebhookTests` five-type matrix (B10-X17).
- `CrossTenantIdorTests`: 8 handlers. **No** `PauseCollectionCommand` / `ResumeCollectionCommand` / `ChangePlanCommand` / LHDN / Payments / Communications IDOR. Pause handlers have the org guard in production and **zero** tests.
- `LhdnOutboxPublisherJobRegistrationTests` does not assert inbox job registration.
- `ModuleBoundaryTests` does not assert outbox **registration**.
- No test of `OutboxPublisherJob` / `InboxConsumerJob` loop (SKIP LOCKED, poison, TypeResolver, non-INotification).
- No two-worker claim test.

**Test inventory (this tree, `rg [Test]`, bin/obj excluded)**

| Project | `[Test]` |
|---------|----------|
| `Lazuar.ArchitectureTests` | 14 |
| `Lazuar.IntegrationTests` | 10 |
| `Lazuar.ModuleTests` | 972 |
| `Modules.Billing.Tests` | 20 |
| `Modules.Ops.Tests` | 5 |
| **Sum** | **1021** |

008 counted 993. The delta is Wave-fix tests (pause sibling, accept-invite, honesty-adjacent), not a new integration spine.

### B10-X27 — P2 — `IAuditRecorder?` optional constructors fail open in any host that forgets the registration

`AddOneModule` does `services.AddScoped<IAuditRecorder, AuditRecorder>()`. Production invite/remove/refund/cancel should audit. The constructors default `= null`. A test host or a future composition that registers the command handlers without One’s DI **silently stops writing** `one.AuditEvents`. That is fail-open for compliance, fail-closed for nothing.

Accept-invite never took the dependency (B10-X20).

### B10-X28 — P2 — Honesty / docs residuals after `cbe17c2`

- Scraper unresolved `MapPreview` / `MapReason` (noise).
- `docs/contracts/openapi-vs-minimal-api.md` §“Intentional frontend dark matter” still says ops invoicing / BillingProfile are **unrouted** (ADR 023). Ops `App.tsx` routes them. The contracts doc is a second SSoT that 023-erased itself.
- Combined spec now has M2M commerce (fixed). Product-scoped Scalar already had it. Clients committed in `cbe17c2` grew ~2k lines.
- Superadmin `/platform/*` TypeSpec still thin (doc residual; not a new bug).
- `CommerceWebhookEnvelope.event_type` union is still the five subscription names; cannot describe `order.completed` / `payment_link.paid`. Schema island.

### B10-X29 — P2 — Pre-dunning SQL excludes `TRIALING` (comms hole, not a 02 claim bug)

```107:108:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.Claim.cs
                WHERE s."Status" = 'ACTIVE'
                  AND s."CancelAtPeriodEnd" IS NOT TRUE
```

A trial that ends in 14 days gets **no** pre-dunning “your trial ends” step from this engine. Billing will convert on the due tick (02’s product). This slice only notes the isolation of campaign matching: campaigns load with `IgnoreQueryFilters` and no org predicate in the load (all tenants’ campaigns in one list), then matchers re-scope. That load is intentional for a platform job.

### B10-X30 — P2 — Outbox publisher holds SKIP LOCKED rows while running all in-process handlers

`OutboxPublisherJob` begins a transaction, locks ≤20 rows, then `await eventBus.PublishAsync` which runs every handler (Billing + Commerce + Communications on `GatewayPaymentCompleted`, etc.) **before** commit. Long MyInvois-adjacent or HTTP-inside-handler work (there should be none; outbound HTTP is the dispatcher) extends lock time. A throwing handler mid-list fails the **event**; already-run handlers in that same `PublishAsync` have already committed their own DbContexts. Retry re-runs everyone. Idempotency is per-handler (`HasEntryBeenProcessedAsync`, unique disputes). Untested as a composition.

`InboxConsumerJob` same lock-across-mediatR shape.

### B10-X31 — P2 — `DatabaseJobTrigger` is a single process-wide TCS

Any module’s `SaveChanges` wakes **all** outbox/inbox jobs. Harmless extra polls. Does not cross replicas (those rely on 5s). Tests construct it; none prove multi-waiter correctness (the swap is racy-looking but `Interlocked.Exchange` + `TrySetResult` is the usual pattern).

### B10-X32 — P2 — Clock: invoice reminder UTC date vs `DueAt`

Quoted in §2.8. Offsets `[-3, 0, 3]` compare UTC calendar dates. A quote due “2026-08-20” stored as `2026-08-19T16:00:00Z` (00:00 MYT on the 20th) has `DueAt.Date == 2026-08-19` UTC. Day-0 mail goes out on the 19th UTC, i.e. the afternoon of the 19th in Malaysia — one local day early. The unique log then blocks a correct day-0 on the 20th.

No test uses a non-midnight `DueAt`. The three tests use in-process “today.”

---

## 5. 008 re-verify (same slice, this tree)

| 008 finding | 008 id / place | Live at 297ba98 / 30d07d2 |
|-------------|----------------|---------------------------|
| Fail-open filter `Empty \|\| org` | 09 §4.1; gap doc 14 | **Closed.** Filter is fail-closed. Arch test + `Empty_Tenant_EF_Filter_Returns_Zero_Rows`. |
| Ops soft-delete replaces tenant filter | 09 §4.1; gap 14 | **Closed.** Override includes org. Arch test locks source. |
| Messaging `/notify` unauthenticated | gap 14 | **Closed.** `.RequireAuthorization("OrgAdmin")`. Tenant required by middleware. |
| Lhdn / CRM outbox jobs missing | 09 §2.5; gap 17 | **Closed.** Both registered. CRM via helper. Lhdn has a registration test (outbox only). |
| Outbox always `ProcessedAt` on first throw | gap 17 | **Closed.** Retry + Dead after 5. Applier tests exist. **Redrive still missing** (B10-X18). |
| Pause claim starve | 09 §3.1 / §8.1 | **Closed for pause** (`CollectionPausedUntil` in SQL + `failedIds` + sibling tests). **Open for auto-debit waiting** (B10-X01). |
| Webhook session org not checked | gap 14 attack 5 | **Closed.** `GatewayPaymentCompleted` loads session/sub with `Id && OrganizationId == event.OrganizationId`. Hardening test `GatewayPaymentCompleted_CrossTenant_Session_Is_NoOp`. |
| One workspace member IDOR | gap 14 | **Mostly closed.** GET members/invites/audit require `HasTenantAccessAsync`. Invite/remove require manage role. Exemption remains (B10-X21). |
| CLIENT can invite | 008 / gap 14 | **Closed** if `WorkspaceStaffRoles.CanManageMembers` excludes MEMBER (invite tests cover MEMBER cannot). |
| Honesty fail: 3 M2M + POST unsubscribe | 08 §2.3 | **Closed** (`cbe17c2`). Live 152 / 160 / 8, exit 0. |
| `main.tsp` omits integration-routes | 08 H2 | **Closed.** |
| Generated clients omit `TRIALING` | 08 H4 | **Closed** for TS/C#. Catalog + webhook tests still omit (B10-X17). |
| POST unsubscribe not allowlisted | 08 H7 | **Closed.** |
| Accept-invite untested / URL 404 | 09 §7.1 | **Half-closed.** Handler tests exist (3). Ops page + `OpsUrl` mint in `297ba98`. Duplicate-membership / audit still open (B10-X20). |
| Presigned empty tenant | 09 §4.4 | Endpoint guards. **Test still tautology** (B10-X26). |
| B2C `alreadyConsolidated` | 09 §3.4 | **Open** (B10-X02). |
| Invoice reminder no claim | 09 §3.5 | **Open** (B10-X10). |
| Checkout expiry no claim | 09 §3.6 | **Open** (B10-X10). |
| Inbox unused except Messaging | 09 §2.4 | **Still true.** Jobs exist; writers do not. |
| `PaymentWebhookPayloadDto` flat | 08 H3 | **Open** (B10-X16). |
| `/lhdn/webhooks` dead | 08 H1 | **Open** (B10-X03). |
| M2M status filter | 08 H9 | **Open** (B10-X15). |
| VitePress silent on TRIALING | 08 §3.3 | **Open** (B10-X17). |
| AppOptions 3020 | hunt brief | **Open** (B10-X13). Type still unbound. |
| `excludeIds` concat | hunt brief | **Open** (B10-X22). |
| Architecture tests allow leak | 09 §1.3 | **Open** (B10-X25). |
| Integration tests skip / never run | 09 §6.3 | **Open** (B10-X26). |
| RevenueRecognition unregistered | 09 §3.6 | **Still parked.** Honest comment. Not a bug if we do not sell recognition. |
| Resend webhook no secret | gap 14 | **Closed outside Development** (503). Dev still accepts unsigned (intentional). |

---

## 6. Lying tests (explicit)

A test lies when its name or fixture location claims a property the body does not prove.

| Test | Why it lies |
|------|-------------|
| `Presigned_Storage_Rejects_Empty_Tenant_Contract` | Does not touch `StorageEndpoints` or `IR2StorageService`. Asserts `Guid.Empty == Guid.Empty`. |
| `HandleAsync_CompletesWithoutWalletOrMediatorDependencies` | `Assert.Pass` with a sentence. Constructor-shape sibling is the real test. |
| `LhdnSandboxE2ETests` (2) | `[Ignore]` — never run; named as sandbox proof. |
| `BillingDbContextTests` in `Lazuar.IntegrationTests` | InMemory. Not integration. |
| `BillingQueryServiceTests` | Can pass against a hand-built 2-table schema that is not the migrated model. |
| `CommerceQueryServiceTests` `DapperQueries_ShouldMatchEntityFrameworkSchema` | Proves “does not throw,” not that column lists match. Useful smoke; name overclaims. |
| `TenantSecurityMiddleware_Exempts_...` | Passes if the **exemption** remains. Does not prove workspace IDOR is gone. |
| `All_Modules_Should_Have_OutboxPublisherJob_In_Infrastructure` | Passes if a leftover type exists and DI forgot `AddHostedService`. |
| `PlatformDbContext_Filter_Must_Not_Treat_Empty_Tenant_As_All_Rows` | String search; rename the condition and it goes blind. |
| `Payload_FiveEventTypes_ShareRequiredFields` | Pins five statuses; runtime sixth is `TRIALING`. |
| `RunOnce_CollectionPaused_SkipsChargeAndKeepsActive` | Still does not assert batch progress (sibling tests added later do). Alone it would pass if the job no-op’d 50 times. |
| `LhdnOutboxPublisherJobRegistrationTests` | Name “so outbox rows drain.” Body only checks DI. Inbox not checked. Drain not checked. |
| `ModuleOutboxInboxExtensionsTests` | Proves the helper registers jobs. Production Commerce/One/… do not use the helper. |

Credit-deduction concurrency tests do **not** lie: they Ignore when Docker is down and say so. They also never run on a laptop without Docker, so CI is the only proof of Billing migrations.

---

## 7. Unread / not fully walked (honesty)

This slice did not re-read every worker line in LHDN submit/poll (owned in part by 06) or every dunning step matcher (02/03). Those were opened only for claim SQL, `IgnoreQueryFilters`, and `GetService`.

Not opened in full:

- Every Communications fulfillment handler beyond the `portal.lazuar.com` fallback grep.
- Every One webhook endpoint method beyond the IDOR header and list-without-secret.
- `PlatformMetricsCollector` LHDN stuck SQL (schema interpolation only).
- `AesSecretVault` key derivation.
- Frontend test absence (portal `i18n.test.mjs` only) — noted, not inventoried file-by-file.
- `packages/api-spec/dist/openapi.yaml` path-by-path vs TypeSpec source (honesty script is the machine read).
- Live `dotnet test` of ArchitectureTests / IntegrationTests (not run; this is a read audit).

If a later 009 slice owns LHDN submit hard-fail or dunning DayOffset exact-match, those are not duplicated here as product bugs.

---

## 8. Ranked open bugs (this slice only)

### P0 — fix or stop selling the path

1. **B10-X01** — Billing auto-debit waiting row starves the hourly batch. Same class of outage as the pause bug, still live. Add dispatched ids to `failedIds` (or a `processedIds` set like dunning) **and** a two-sub `RunOnce` test.
2. **B10-X03** — Dead `POST /lhdn/webhooks` + Developers hub body-only HMAC. Integrators will not receive `invoice.valid`. Dual-write or 410 the Maps; fix the hub page.
3. **B10-X02** — B2C consolidation idempotency check is invisible under fail-closed filters. Double `B2C-CONS-*` events after a crash window.

### P1 — isolation, silent drop, fail-open, contract lie

4. **B10-X07** — `GetOrderByIdAsync` (and coupon/session twins) under empty ambient → wrong `order.completed` quantity.
5. **B10-X09** — CRM get-by-id ignores tenant.
6. **B10-X08** — Commerce get-by-id ignores tenant.
7. **B10-X05 / B10-X04 / B10-X06** — Outbox success on no handlers; inbox success on non-notification; TypeResolver null cache. Silent event death.
8. **B10-X10** — Expiry + invoice reminder multi-instance / all-or-nothing save.
9. **B10-X11 / B10-X12** — Optional DI fail-open: SST 0; PAST_DUE without URL; reminder 404 URL then unique-index lockout.
10. **B10-X13 / B10-X14** — ClientUrl 3020 ghost type; `portal.lazuar.com` fallbacks; JWT secret = every HMAC.
11. **B10-X15 / B10-X16 / B10-X17** — M2M lying totals; flat payment DTO; catalog/tests vs `TRIALING`.
12. **B10-X18 / B10-X19** — Dead letters + null lag threshold + boot-continues-on-pending-model.
13. **B10-X20 / B10-X21** — Accept-invite membership/audit; workspace exemption as default.
14. **B10-X22 / B10-X23 / B10-X24** — Concat SQL; unfiltered org-bearing tables; idle inbox fleet.

### P2 — tests and architecture that will not catch the next leak

15. **B10-X25 / B10-X26 / B10-X27 / B10-X28 / B10-X29 / B10-X30 / B10-X31 / B10-X32** — Arch-test holes; tautology / Ignore / ad-hoc schema; optional audit; stale contracts doc; TRIALING pre-dunning skip; lock-across-handlers; process-local trigger; UTC reminder day.

---

## 9. What “fixed since 008” actually means for this slice

The **spine** is materially better than `4624070`:

- Fail-closed filters + write guard.
- Ops filter includes org.
- Lhdn and CRM outbox publishers exist (LHDN→Billing events can leave the Lhdn outbox).
- Outbox retry/dead-letter instead of one-shot drop.
- Pause no longer starves the billing batch.
- Payment-completed handlers re-check org.
- Messaging notify is OrgAdmin.
- Workspace GETs check membership.
- Path honesty is green (152 / 160 / 8).
- Accept-invite has a handler test and an ops URL.

The **cross-cutting residual** is the same shape it always was, just one layer down:

> Workers and bus handlers run with `TenantId == Guid.Empty`. Isolation is a social contract on every `IgnoreQueryFilters` / `GetXById` / `GetService` / raw SQL call. Architecture tests check two files and a type name. Integration tests barely touch Postgres. Inbox is still a Messaging-only costume. Contracts honesty is path-only. Optional DI and fallback URLs fail **open**.

That is the honest state of tenancy, workers, contracts, and tests on `297ba98`.

---

*End of slice 10. No code was changed except this report file.*
