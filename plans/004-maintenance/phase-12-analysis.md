# Phase 12 — Analysis (folder alignment)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Goal:** Align Messaging Workers/EventHandlers + split remaining endpoint monoliths (Billing, Lhdn, Ops) to Commerce style. Document CRM / Billing handler-layer exceptions. DI-safe only.  
**Evidence:** `checklists/phase-12-folder-alignment.md`, `03-folder-organization.md`

---

## 1. Pre-change inventory

### 1.1 Messaging (Tier C — navigation)

| Item | Pre | Target |
|------|-----|--------|
| Inbox/Outbox jobs | Infrastructure **root** | `Infrastructure/Workers/` |
| Integration handlers | Mix of root + `EventHandlers/` | All under `Infrastructure/EventHandlers/` |
| Application notification handlers | Root (`TenantCreated*`, `TenantUpdated*`) + `EventHandlers/` | All under `Application/EventHandlers/` |
| Namespaces | `Modules.Messaging.Infrastructure` for jobs/handlers | Match folder: `.Workers` / `.EventHandlers` / `.Application.EventHandlers` |
| DI | Explicit `AddHostedService` + `AddTransient` + subscribe | Same types; add `using` for new namespaces |

**Stability:** Class names, `AddMessagingModule` / `UseMessagingSubscriptions` surface, MediatR assembly scan unchanged.

### 1.2 Endpoint monoliths

| Module | Pre LOC | Split plan |
|--------|---------|------------|
| Billing | **238** | Composer + AdminLedger / AdminCredits / AdminProfile / PublicBilling |
| Lhdn | **247** | Composer + Document / AdminApiKey / AdminWebhook / TenantConfig |
| Ops | **211** | Composer + Chat / ChatStream / ExecuteAction |
| Payments | Already split (`IntegrationEndpoints`, `PlatformEndpoints` siblings) | **No change** this phase |
| Messaging HTTP | **67** (thin) | Leave monolithic |

**House style:** `namespace Modules.X.Infrastructure` for endpoint partials (folder-only nav), same as One/Commerce/Communications. Public map names stable: `MapBillingEndpoints`, `MapLhdnEndpoints`, `MapOpsEndpoints`.

### 1.3 Documentation gaps

| Module | Gap |
|--------|-----|
| Billing | Handlers inverted into Infrastructure — not called out in README |
| CRM | No Application is arch-test exception — README silent |
| Ops.Contracts | Empty project with no explanation |

### 1.4 Solution hygiene

| Item | State |
|------|-------|
| Empty slnx folders | Already cleaned (Phase 01) |
| `api-types-dotnet` under `/Modules/Lhdn/` | Misleading — move to `/Packages/` |

---

## 2. Target layouts

### 2.1 Messaging

```
Modules/Messaging/
  Application/
    EventHandlers/
      TenantCreatedEventHandler.cs
      TenantUpdatedEventHandler.cs
      WorkspaceUpdatedEventHandler.cs
    SendTenantNotificationCommandHandler.cs
    ITenantReplicaRepository.cs
    DependencyInjection.cs
  Infrastructure/
    Workers/
      MessagingInboxConsumerJob.cs
      MessagingOutboxPublisherJob.cs
    EventHandlers/
      DispatchMessageIntegrationEventHandler.cs
      TenantProvisionedIntegrationEventHandler.cs
      TenantUpdatedIntegrationEventHandler.cs
      TenantProvisionedSeedingHandler.cs
      WorkspaceUpdatedIntegrationEventHandler.cs
    Endpoints.cs                    # unchanged (thin)
    TenantReplicaRepository.cs
    MessagingDbContext.cs
    DependencyInjection.cs
```

### 2.2 Billing endpoints

```
Infrastructure/
  Endpoints.cs                      # MapBillingEndpoints composer
  Endpoints/
    AdminLedgerEndpoints.cs         # ledger, document URL, summary, net-profit
    AdminCreditsEndpoints.cs        # credits, packages, top-up
    AdminProfileEndpoints.cs        # GET/PUT profile
    PublicBillingEndpoints.cs       # public profile + signed documents
```

### 2.3 Lhdn endpoints

```
Infrastructure/
  Endpoints.cs                      # MapLhdnEndpoints composer
  Endpoints/
    DocumentEndpoints.cs            # write + read docs + taxpayer validate
    AdminApiKeyEndpoints.cs         # OrgAdmin api-keys façade
    AdminWebhookEndpoints.cs        # webhook CRUD
    TenantConfigEndpoints.cs        # lhdn-config + certificate
```

### 2.4 Ops endpoints

```
Infrastructure/
  Endpoints.cs                      # MapOpsEndpoints composer
  Endpoints/
    ChatEndpoints.cs                # conversations CRUD + chat + resolve
    ChatStreamEndpoints.cs          # SSE stream
    ExecuteActionEndpoints.cs       # proposed-action execute
```

---

## 3. Move rules

- [x] Folder moves within same assembly (DI/MediatR scan still whole assembly)
- [x] Namespaces updated to match Workers/EventHandlers convention
- [x] Hosted services still registered via `AddHostedService<T>` (type names stable)
- [x] Integration handler types + `Subscribe<>` unchanged
- [x] `Map*Endpoints` public names and route paths/methods/policies unchanged
- [x] No Billing Application rebalance (handlers stay in Infrastructure)
- [x] No CRM Application introduction
- [x] No Messaging→Communications merge
- [x] Payments endpoint siblings left as-is

---

## 4. Explicitly out of scope

- Full Billing handler move to Application
- CRM Contracts `Commands/`/`Events/` foldering
- Messaging Contracts single-file → `Events/` (optional 12.3; skip — single type)
- Payments move into `Endpoints/` subfolder
- LhdnGatewayAdapter / remaining phase-11 partials
