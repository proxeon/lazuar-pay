# Phase 12 — Done

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Commit subject:** `refactor(api): folder alignment Messaging and endpoints (phase 12)`

## What landed

### 1. Messaging layout

| Move | From | To | Namespace |
|------|------|----|-----------|
| Inbox/Outbox jobs | Infrastructure root | `Infrastructure/Workers/` | `…Infrastructure.Workers` |
| Tenant provision/update/seeding handlers | Infrastructure root | `Infrastructure/EventHandlers/` | `…Infrastructure.EventHandlers` |
| TenantCreated/Updated notification handlers | Application root | `Application/EventHandlers/` | `…Application.EventHandlers` |

DI: `using Modules.Messaging.Infrastructure.Workers` added; EventHandlers using already present. Hosted services + bus subscriptions unchanged.

### 2. Endpoint splits (Commerce-style composers)

| Module | Composer | Partials |
|--------|----------|----------|
| **Billing** | `Endpoints.cs` (~20) | AdminLedger, AdminCredits, AdminProfile, PublicBilling |
| **Lhdn** | `Endpoints.cs` (~17) | Document, AdminApiKey, AdminWebhook, TenantConfig |
| **Ops** | `Endpoints.cs` (~18) | Chat, ChatStream, ExecuteAction |

Public maps stable: `MapBillingEndpoints`, `MapLhdnEndpoints`, `MapOpsEndpoints`. Namespace remains `Modules.*.Infrastructure` for all endpoint files.

### 3. Documentation

| Doc | Note |
|-----|------|
| `Billing/README.md` §3.1 | Handlers live in Infrastructure today; rebalance = separate epic |
| `CRM/README.md` §3.1 | No Application project; arch-test exception; internal-only |
| `Ops/Contracts/README.md` | Intentionally hollow agent module |
| `Messaging/README.md` | Workers + EventHandlers paths |

### 4. Solution hygiene

- `Lazuar.ApiContracts` moved from solution folder `/Modules/Lhdn/` → `/Packages/` (alongside Lhdn SDK).

### Plans

- `phase-12-analysis.md` — inventory, layouts, rules  
- `checklists/phase-12-folder-alignment.md` — 12.1–12.6 marked done  

## Verification

| Check | Result |
|-------|--------|
| Messaging Infrastructure build | **0 warnings, 0 errors** |
| Billing / Lhdn / Ops Infrastructure builds | **0 warnings, 0 errors** |
| Architecture tests | **12/12 passed** |
| Map* public names | Stable |
| DI / MediatR | Same assemblies; Worker/EventHandler namespaces fixed |

## Exit criteria

| Criterion | Status |
|-----------|--------|
| Messaging matches Workers/EventHandlers convention | Yes |
| Billing + Lhdn (+ Ops) endpoints match Commerce style | Yes (One was Phase 07) |
| Build + architecture tests green | Yes |
| Handler-layer docs (Billing, CRM) | Yes |

## Explicitly not done

- Billing Application rebalance  
- CRM Application introduction / Contracts foldering  
- Messaging Contracts → `Events/` subfolder  
- Payments → `Endpoints/` subfolder tidy  
- Messaging thin `Endpoints.cs` split (not needed at ~67 LOC)  

## Next

Phase 13 — test fixtures and errors (or remaining god-file partials when touching those files).
