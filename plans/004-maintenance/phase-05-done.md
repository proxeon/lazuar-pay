# Phase 05 — Done

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Commit subject:** `fix(api): TypeSpec contract honesty (phase 05)`  
**Evidence:** `05-typespec-contracts.md` Wave A / checklist `phase-05-typespec-contract-honesty.md`

## What landed

### TypeSpec

1. **Payments routes** (`packages/api-spec/modules/payments/routes.tsp`)
   - Path: `/integrations/payments/checkouts` (no trailing slash)
   - Optional `Idempotency-Key` header on create
2. **Communications broadcast model** (`packages/api-spec/modules/communications/models.tsp`)
   - Removed unused `target_plan_id` / `target_status` / `target_is_reminder_only`
   - Doc comment: re-add only when fan-out + storage honor filters

### Backend endpoints (SSoT)

3. **Commerce** `SubscriberEndpoints.cs` — binds `Lazuar.ApiTypes`:
   - `CreateManualSubscriberDto`
   - `GenerateCustomerPortalRequestDto` / `GenerateCustomerPortalResponseDto`
   - `RecordPaymentRequestDto`
   - Local dual records deleted; `double` → `decimal` at command boundary
4. **Payments** `IntegrationEndpoints.cs` — binds generated:
   - `CreateIntegrationCheckoutRequestDto` / `IntegrationCheckoutResponseDto`
   - Group path aligned to OpenAPI; local DTO classes deleted
5. **Communications** `SendBroadcastCommand` — dropped dead targeting parameters (honest with v1 all-active fan-out)

### Generated clients

6. `task gen` succeeded and committed:
   - `packages/api-types-ts/src/index.ts` (checkout paths + payments schemas; no broadcast targeting)
   - `packages/api-types-dotnet/Lazuar.ApiContracts.cs` (includes integration checkout DTOs)
   - LHDN Kiota lock/model refresh as gen side effect

### Docs (this phase)

7. `plans/004-maintenance/phase-05-analysis.md`
8. `plans/004-maintenance/phase-05-done.md`
9. Checklist `checklists/phase-05-typespec-contract-honesty.md` marked honestly

## Explicitly deferred (not blocking P0)

| Item | Why deferred |
|---|---|
| Billing signed PDF in TypeSpec | Impl-only; no typed client need yet |
| Broadcast preview/status in TypeSpec | Admin UX; PascalCase module DTOs; Wave B |
| Communications public compliance routes | Operational allowlist |
| docs-payments `@useAuth` / security schemes | P1 product DX |
| Orphan models (CRM, LinkedCheckout, PaymentRecord) | Wave B hygiene |
| Product dual DTOs (`CreateProductRequest` etc.) | Outside listed P0 surfaces |
| Money type policy (string decimal) | v1 keeps float64/double at edge |
| Automated OpenAPI vs Minimal path CI | Phase 06 |
| Full broadcast targeting implementation | Product not ready; fields removed for honesty |

## Verification

- `task gen` — green
- `dotnet build apps/lazuar-api/src/Lazuar.Api/Lazuar.Api.csproj` — green
- Focused ModuleTests (`IntegrationCheckout*`, `CreateIntegrationCheckout*`, `Broadcast*`, `Subscriber*`) — **40 passed**, 0 failed
- OpenAPI paths: `/integrations/payments/checkouts` (no trailing `/`); TS path key matches
- Grep: no remaining local `CreateManualSubscriberRequest` / `CreateIntegrationCheckoutRequest` (non-Dto)

## Next

- Phase 06 CI/Taskfile alignment (optional path honesty gate)
- Wave B TypeSpec completeness (broadcast status/preview, payments security schemes, orphan cleanup)
- When product wants broadcast targeting: query filters + Broadcast storage + fan-out + restore TSP fields in one PR
