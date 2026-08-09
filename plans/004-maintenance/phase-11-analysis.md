# Phase 11 — Analysis (remaining god-file splits)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Goal:** Behavior-preserving splits of remaining P1 god files: Commerce public endpoints, payment-completed handler, webhook handler.  
**Evidence:** `checklists/phase-11-more-god-file-splits.md`, `02-large-files-chunking.md` §3.7–3.9

---

## 1. Inventory (pre-split)

| File | LOC | Responsibilities |
|------|-----|------------------|
| `Modules/Commerce/Infrastructure/Endpoints/PublicEndpoints.cs` | **372** | Product by slug, coupon validate, portal + cancel, checkout + status (preferred + legacy), custom checkout, arrears + update-payment |
| `Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs` | **376** | Route by metadata type → open checkout session vs subscription recovery; coupon confirm; order/sub create; arrears recover; charge attempt; transaction log |
| `Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` | **306** | Verify/parse → idempotency (event-id + business key) → session metadata merge → emit dispute/failed/completed → unique-race save |
| `Modules/Lhdn/Infrastructure/Gateways/LhdnGatewayAdapter.cs` | **384** | (optional this PR — deferred if capacity used on webhook partials) |

### 1.1 Public endpoints route map

| Area | Routes (under `/public/commerce`) |
|------|-----------------------------------|
| **Product** | `GET /{tenantSlug}/products/{slug}`, `GET /{tenantSlug}/validate-coupon` |
| **Portal** | `GET /{tenantSlug}/portal`, `POST /{tenantSlug}/portal/cancel` |
| **Checkout** | `POST /checkout`, `GET /{tenantSlug}/checkout/{sessionId}/status`, `GET /checkout/{subId}/status` (legacy) |
| **Custom checkout** | `GET /{tenantSlug}/custom-checkouts/{sessionId:guid}` |
| **Arrears** | `GET /checkout/{subId:guid}/arrears`, `POST /checkout/{subId:guid}/update-payment` |

Composer entry: `MapPublicCommerceEndpoints` (called from `Endpoints.MapCommerceEndpoints` on `publicGroup`).

### 1.2 GatewayPaymentCompleted paths

| Path | Entry condition | Side effects |
|------|-----------------|--------------|
| **Router** | type ∈ {commerce_subscription, custom_payment_link}; optional tenant_id match; correlation id | Load OPEN session by correlation |
| **Open checkout** | OPEN session found | Coupon confirm; session complete; custom link webhook OR product → sub/order + events; log tx |
| **Subscription payment** | No open session | PAST_DUE/SUSPENDED recover; period advance; dunning RecordRecovery; vault token; charge attempt success; log tx |
| **Helpers** | — | `TryResolveCorrelationId` (subscription_id → receipt); `LogTransactionAsync`; `MarkChargeAttemptSucceededAsync` |

Stability: type name + DI registration (`AddTransient` + `eventBus.Subscribe`) + ctor signature used by tests.

### 1.3 ProcessGatewayWebhook stages

| Stage | Members |
|-------|---------|
| Orchestration | `Handle` metrics wrapper + `HandleCoreAsync` |
| Metadata merge | `MergeSessionMetadataAsync` |
| Logging | `LogProcessed` |
| Idempotency | `BuildBusinessKey`, `TrySaveChangesAsync`, `IsUniqueConstraintViolation` (**public static**, tests call it) |

Stability: handler type name, public `IsUniqueConstraintViolation`, MediatR command type.

---

## 2. Target layout

### 2.1 Public endpoints (composer + domain map files)

```
Modules/Commerce/Infrastructure/Endpoints/
  PublicEndpoints.cs                 # MapPublicCommerceEndpoints composer only
  PublicProductEndpoints.cs          # product slug + validate-coupon
  PublicPortalEndpoints.cs           # portal aggregate + cancel
  PublicCheckoutEndpoints.cs         # initiate + preferred/legacy status
  PublicCustomCheckoutEndpoints.cs   # custom checkout read + draft PDF sig
  PublicArrearsEndpoints.cs          # arrears summary + update-payment
```

All types stay in namespace `Modules.Commerce.Infrastructure` (folder-only nav), matching One/Commerce endpoint house style.

### 2.2 GatewayPaymentCompleted (partials)

```
EventHandlers/
  GatewayPaymentCompletedIntegrationEventHandler.cs              # fields, ctor, HandleAsync router
  GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs # HandleOpenCheckoutSessionAsync
  GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs # HandleSubscriptionPaymentAsync + MarkChargeAttemptSucceededAsync
  GatewayPaymentCompletedIntegrationEventHandler.Helpers.cs      # TryResolveCorrelationId + LogTransactionAsync
```

### 2.3 ProcessGatewayWebhook (partials)

```
Commands/
  ProcessGatewayWebhookCommandHandler.cs              # fields, ctor, Handle + HandleCoreAsync
  ProcessGatewayWebhookCommandHandler.Metadata.cs     # MergeSessionMetadataAsync
  ProcessGatewayWebhookCommandHandler.Logging.cs      # LogProcessed
  ProcessGatewayWebhookCommandHandler.Idempotency.cs  # BuildBusinessKey, TrySaveChangesAsync, IsUniqueConstraintViolation
```

---

## 3. Move rules

- [x] `MapPublicCommerceEndpoints` name and return type unchanged
- [x] Public route paths, methods, auth attributes unchanged (none added/removed)
- [x] `GatewayPaymentCompletedIntegrationEventHandler` type + ctor + `IIntegrationEventHandler<>` unchanged
- [x] `ProcessGatewayWebhookCommandHandler` type + public `IsUniqueConstraintViolation` unchanged
- [x] No DI registration changes
- [x] No behavior / event payload / idempotency key format changes
- [x] Prefer partials for handlers (zero DI surface); separate static classes for endpoint maps (One/Commerce style)

---

## 4. Risk mitigations

| Risk | Mitigation |
|------|------------|
| Public route regression | Mechanical move of Map* lambdas; composer order same; host still `publicGroup.MapPublicCommerceEndpoints()` |
| Double fulfillment on payment complete | Same router branches; partials only relocate private methods |
| Webhook double-publish / unique race | Same pre-checks + `TrySaveChangesAsync` swallow 23505; unit suite |
| Test ctor/static breakage | Type names + public static stay on partial class |

---

## 5. Deferred this phase

- `LhdnGatewayAdapter` partials (11.4) — optional; capacity used on webhook partials
- `LlmOrchestratorService` (11.5)
- P2 adapters / BillingQueryService / endpoint monoliths ~210–246 LOC
