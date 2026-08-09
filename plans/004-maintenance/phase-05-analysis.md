# Phase 05 — Analysis (TypeSpec contract honesty)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Evidence:** `05-typespec-contracts.md` Wave A / P0  
**Scope of this phase:** Dual edge DTOs (Commerce subscribers + Payments integration), payments checkout trailing slash, broadcast targeting honesty, regenerate clients.

---

## 1. Baseline

| Check | Result |
|---|---|
| `task gen` on clean-ish tree | **Succeeded** (tsp + openapi-typescript + NSwag + Kiota LHDN) |
| Clients before gen | Stale: monolith OpenAPI already had payments schemas in `dist/`, but committed `Lazuar.ApiContracts.cs` / `@repo/api-types-ts` **lacked** `CreateIntegrationCheckoutRequestDto` / checkout paths |
| `Generated/Models.cs` | Already gone (Phase 01) |
| P0 residual list | Dual DTOs, broadcast targeting phantom fields, payments trailing slash, Idempotency-Key prose-only |

---

## 2. Dual DTO inventory (P0)

### 2.1 Commerce subscribers — `SubscriberEndpoints.cs`

| Local (deleted) | TypeSpec / `Lazuar.ApiTypes` | Wire fields |
|---|---|---|
| `CreateManualSubscriberRequest` | `CreateManualSubscriberDto` | name, email, phone, product_id, payment_method, amount_paid, reference_number?, send_welcome_email?, start_date?, next_billing_date? |
| `GenerateCustomerPortalRequest` | `GenerateCustomerPortalRequestDto` | customer_email, return_url |
| `GenerateCustomerPortalResponse` | `GenerateCustomerPortalResponseDto` | url |
| `RecordSubscriberPaymentRequest` | `RecordPaymentRequestDto` | amount, payment_method, reference_number? |

**Money:** generated types use `double` (OpenAPI number); commands use `decimal` — cast at ACL (`(decimal)req.Amount_paid` / `(decimal)req.Amount`), same pattern as coupons.

**Status after phase:** endpoints bind generated types only; local records removed.

### 2.2 Payments integration checkout — `IntegrationEndpoints.cs`

| Local (deleted) | TypeSpec / `Lazuar.ApiTypes` | Notes |
|---|---|---|
| `CreateIntegrationCheckoutRequest` | `CreateIntegrationCheckoutRequestDto` | amount → cast to decimal; metadata `Dictionary<string,string>?` |
| `IntegrationCheckoutResponseDto` (local) | `IntegrationCheckoutResponseDto` (generated) | `checkout_id` is **string** in TSP (GUID as string); `expires_at` is `DateTimeOffset` |

**Pre-gen gap:** types existed in TypeSpec + `dist/openapi.yaml` but not in committed NSwag/TS clients — gen brought them in.

**ASP.NET route shape:** group rebased to `/integrations/payments` + `MapPost("/checkouts")` / `MapGet("/checkouts/{checkoutId:guid}")` so runtime path matches OpenAPI **without** trailing slash.

**ProblemDetails:** after importing `Lazuar.ApiTypes`, helper uses fully-qualified `Microsoft.AspNetCore.Mvc.ProblemDetails` to avoid CS0104.

---

## 3. Broadcast targeting (P0)

### Spec (before)

`CreateBroadcastRequestDto` advertised:

- `target_plan_id?`
- `target_status?`
- `target_is_reminder_only?`

### Runtime (before)

| Layer | Behavior |
|---|---|
| `BroadcastEndpoints` | Bound generated DTO but **dropped** targeting when building `SendBroadcastCommand` |
| `SendBroadcastCommand` | Optional targeting params present |
| `SendBroadcastCommandHandler` | Ignored targeting; `GetActiveSubscriberCountAsync` only |
| `Broadcast` aggregate | No columns for filters |
| `BroadcastFanoutJob` | Always `GetActiveSubscriberRecipientsAsync` (ACTIVE/PAST_DUE + marketing consent) |
| Frontend | No consumers of targeting fields |

### Decision

**Remove fields from TypeSpec** (honesty) and drop unused optional params from `SendBroadcastCommand`.  
Re-add only when product implements end-to-end: query filters + broadcast storage + fan-out. Comment left in TSP model.

**Not chosen:** endpoint-only mapping (would still be silent no-ops).

---

## 4. Payments path trailing slash (P0)

| Artifact | Before | After |
|---|---|---|
| TypeSpec | `@route("/integrations/payments/checkouts")` + `@route("/")` → OpenAPI `/integrations/payments/checkouts/` | `@route("/integrations/payments")` + `@route("/checkouts")` → `/integrations/payments/checkouts` |
| Minimal API | `MapGroup(.../checkouts).MapPost("/")` | `MapGroup(.../payments).MapPost("/checkouts")` |
| TS path key | (stale / missing) | `"/integrations/payments/checkouts"` |

Also added optional `@header("Idempotency-Key")` on create (mirror LHDN; still also accepts body `idempotency_key`).

---

## 5. P1 inventory decisions (document only this phase)

| Item | Decision | Action now |
|---|---|---|
| Billing signed final PDF | **document as internal** / impl-only until product needs typed client | Deferred |
| Broadcast preview/status GET | **document as internal** console UX (PascalCase module DTOs) | Deferred (Wave B) |
| Communications public unsubscribe/resend | **document as internal** / operational | Deferred |
| docs-payments security schemes | **implement later** (P1) | Deferred |
| Orphan CRM / LinkedCheckout / PaymentRecord | remove from emit later | Deferred (Wave B) |
| Money as float64 vs decimal | accept double at edge for v1; cast to decimal in ACL | Policy note only |
| CI OpenAPI↔Minimal path honesty | Phase 06 | Deferred |

---

## 6. Gen pipeline notes

- `task gen` completed successfully (local tools: tsp, pnpm, NSwag via `dotnet tool`, Kiota).
- Committed clients updated: `packages/api-types-ts/src/index.ts`, `packages/api-types-dotnet/Lazuar.ApiContracts.cs`.
- LHDN Kiota regen also touched lockfiles + minor One API-key model parity (side effect of full gen; keep for CI green).
- `dist/` remains gitignored; product OpenAPI rebuilt locally under `packages/api-spec/dist/`.

---

## 7. Residual dual-DTO / dual-write risk (out of P0 list)

Still local records (not this PR’s P0):

- Commerce `ProductEndpoints` — `CreateProductRequest` / `UpdateProductRequest` vs TypeSpec product DTOs
- Other modules may have similar patterns; track under later maintenance if they diverge

---

*End of phase 05 analysis.*
