# Phase 11 — Done

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Commit subject:** `refactor(api): split remaining god files (phase 11)`

## What landed

### 1. Commerce public endpoints (`Modules/Commerce/Infrastructure/Endpoints/`)

| File | Responsibility |
|------|----------------|
| `PublicEndpoints.cs` | Thin `MapPublicCommerceEndpoints` composer (~20 LOC) |
| `PublicProductEndpoints.cs` | Product by slug + validate-coupon |
| `PublicPortalEndpoints.cs` | Portal aggregate + portal cancel |
| `PublicCheckoutEndpoints.cs` | Initiate checkout + preferred/legacy status |
| `PublicCustomCheckoutEndpoints.cs` | Custom checkout read + draft PDF signed URL |
| `PublicArrearsEndpoints.cs` | Arrears summary + update-payment |

Host entry unchanged: `publicGroup.MapPublicCommerceEndpoints()` from `Endpoints.cs`.

### 2. GatewayPaymentCompleted partials (`EventHandlers/`)

| File | Responsibility |
|------|----------------|
| `GatewayPaymentCompletedIntegrationEventHandler.cs` | Fields, ctor, `HandleAsync` router |
| `…Handler.OpenCheckout.cs` | Open session: coupon, custom link, sub/order create |
| `…Handler.Subscription.cs` | Recovery/renewal + charge attempt success |
| `…Handler.Helpers.cs` | Correlation id resolve + transaction log |

Type / DI / subscribe surface unchanged.

### 3. ProcessGatewayWebhook partials (`Payments/Application/Commands/`)

| File | Responsibility |
|------|----------------|
| `ProcessGatewayWebhookCommandHandler.cs` | Fields, ctor, `Handle` + `HandleCoreAsync` orchestration |
| `…Handler.Metadata.cs` | `MergeSessionMetadataAsync` |
| `…Handler.Logging.cs` | `LogProcessed` |
| `…Handler.Idempotency.cs` | Business key, unique-race save, **public** `IsUniqueConstraintViolation` |

### Size

| Monolith (before) | Largest piece after |
|-------------------|---------------------|
| PublicEndpoints **372** | PublicCheckout **~149** (composer **~20**) |
| GatewayPaymentCompleted **376** | Subscription **~152** (router **~67**) |
| ProcessGatewayWebhook **306** | Orchestration **~161** |

### Plans

- `phase-11-analysis.md` — inventory, layout, rules  
- `checklists/phase-11-more-god-file-splits.md` — 11.1–11.3 + exit criteria marked done  

## Verification

| Check | Result |
|-------|--------|
| `Modules.Commerce.Infrastructure` build | **0 warnings, 0 errors** |
| `Modules.Payments.Application` build | **0 warnings, 0 errors** |
| Smoke filter: ProcessGatewayWebhook + CommerceProductCompleteness + GatewayPayment + TenantIsolationHardening | **34/34 passed** |
| Public type names | Stable (`PublicEndpoints.MapPublicCommerceEndpoints`, both handlers, `IsUniqueConstraintViolation`) |
| DI registration | Unchanged |

## Exit criteria

| Criterion | Status |
|-----------|--------|
| Public routes composed from domain maps | Yes |
| Payment-completed paths readable in isolation | Yes — OpenCheckout / Subscription / Helpers |
| Webhook verify/merge/emit stages separated | Yes — Metadata / Logging / Idempotency partials |
| Behavior parity | Smoke suite green; mechanical move only |

## Explicitly not done

- `LhdnGatewayAdapter` partials (11.4)  
- `LlmOrchestratorService` further partials (11.5)  
- P2 adapter / BillingQueryService / endpoint monoliths (11.6)  
- New route contract tests for public commerce maps  

## Next

Phase 12 folder alignment (or LhdnGatewayAdapter / remaining 11.4–11.5 when touching those areas).
