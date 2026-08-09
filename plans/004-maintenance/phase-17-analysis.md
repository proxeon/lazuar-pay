# Phase 17 — Analysis (Deferred jobs, probes, freeze list)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Goal:** Explicit ship / park / delete for residual surfaces (jobs, probes, host refs, freeze honesty).  
**Evidence:** `decisions.md` §00.3–00.4, `01-removable-dead-code.md` §2, checklist `phase-17-deferred-jobs-and-flags.md`

---

## 1. Inventory (pre-change)

| Surface | Location | Live? | Decision |
|---------|----------|-------|----------|
| `RevenueRecognitionJob` | `Modules/Billing/Infrastructure/Workers/RevenueRecognitionJob.cs` | **No** — DI commented out | **Park** (00.3) |
| DI registration | `AddBillingModule` | Comment block only | Keep unregistered |
| Entity/table | `DeferredRevenueSchedule` / `billing.DeferredRevenueSchedules` | Schema present; writers absent | Keep (no drop without product OK) |
| `GET …/_scope-probe` | `IntegrationProvisionEndpoints.cs` under `/one` | Yes — Phase 1 policy probe | **Delete** |
| Real M2M checkouts | `Payments/Infrastructure/IntegrationEndpoints.cs` `POST/GET /integrations/payments/checkouts` | Yes + auth policy | Covers need |
| Host Application refs | `Lazuar.Api.csproj` → Commerce + Communications Application | Direct + redundant | **Remove** |
| Stale event twin | `Commerce.Contracts.Events.ExecuteOffSessionChargeIntegrationEvent` | Unused; live path uses Payments | **Delete** |
| Messaging / WhatsApp | Module + console channel | Email live; WhatsApp not product | **Freeze** (00.4) |

### 1.1 RevenueRecognitionJob evidence

- Class fully implements hourly `BackgroundService` scan of non-`COMPLETED` schedules.
- `// services.AddHostedService<RevenueRecognitionJob>();` already present (C.1 residual).
- `InvoiceIssuedHandler` books `LIABILITY_DEFERRED_REVENUE` but does not create schedules.
- README §6 already said “Not registered”; no metrics/UI claim recognition runs (no FE hits).

### 1.2 Scope-probe evidence

- Comment: “Phase 1 policy probe for IntegrationPaymentsCheckoutsWrite (real M2M checkout routes land in Phase 2).”
- Path: `GET /api/v1/one/integrations/payments/checkouts/_scope-probe` (via One group).
- **Not** in TypeSpec; **no** FE references under apps/packages; **no** test hits the route (policy tests use `AuthorizeAsync` against the policy name only).
- Real write path: `POST /integrations/payments/checkouts` with same policy.

### 1.3 Host Application ProjectReferences

| Ref | Needed for compile? | Notes |
|-----|---------------------|-------|
| All module `*Infrastructure.csproj` | Yes | Composition entrypoints |
| `Modules.Commerce.Application` | **No** (transitive via Commerce.Infrastructure) | Historical WIP leftover (Vault-era) |
| `Modules.Communications.Application` | **No** (transitive) | Same |
| Other Application | Already transitive only | MediatR `typeof(….Application.DependencyInjection)` works transitively |

### 1.4 Stale integration event

- `Modules.Commerce.Contracts.Events.ExecuteOffSessionChargeIntegrationEvent` — **no publishers/subscribers**.
- Live: Commerce workers publish `Modules.Payments.Contracts.Events.ExecuteOffSessionChargeIntegrationEvent`; Payments handler subscribes.

---

## 2. Decisions applied

| Item | Choice | Rationale |
|------|--------|-----------|
| 17.1 RevenueRecognitionJob | **Park** | `decisions.md` 00.3; no schedule writers; no table drop |
| 17.2 `_scope-probe` | **Delete** | Debug leftover; M2M real routes exist; tests/TypeSpec/FE free |
| 17.3 Stale events | **Delete** Commerce twin only | Payments event remains active |
| 17.4 WhatsApp | **Freeze** (doc) | 00.4; no half-UI; no merge |
| 17.5 Host Application refs | **Remove direct refs** + arch test | ADR 001 / report 04 P1 |

---

## 3. Target edits

1. XML remarks + DI park note on `RevenueRecognitionJob` / `AddBillingModule` / Billing README / `InvoiceIssuedHandler`.
2. Remove `_scope-probe` map from `IntegrationProvisionEndpoints`.
3. Drop Application `ProjectReference`s from host; document Infrastructure-only composition.
4. Arch test: host csproj must not ProjectReference `Modules.*.Application`.
5. Delete Commerce `ExecuteOffSessionChargeIntegrationEvent.cs`.
6. Messaging README freeze section (00.4).

---

## 4. Out of scope

- Implementing deferred schedule writers / re-enabling the job (product epic).
- Dropping `DeferredRevenueSchedules` table/migration.
- Moving MediatR Application registration into per-module DI (optional follow-up; markers still on host via transitive refs).
- Messaging → Communications merge (Phase 16 gate not met).
- Outbox history cleanup for any historical event type names.
