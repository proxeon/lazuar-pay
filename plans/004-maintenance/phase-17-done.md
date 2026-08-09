# Phase 17 — Done

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Commit subject:** `chore(api): park deferred jobs and clean probes (phase 17)`

## What landed

### 1. RevenueRecognitionJob — **Parked** (decision 00.3)

| Artifact | Change |
|----------|--------|
| `Workers/RevenueRecognitionJob.cs` | XML `<remarks>`: unregistered by design until finance/Xero product epic owns schedule creation; entity/table may remain; no shipping claim recognition runs |
| `Billing/Infrastructure/DependencyInjection.cs` | Park comment aligned to decisions.md 00.3 / Phase 17 (still no `AddHostedService`) |
| `InvoiceIssuedHandler.cs` | Comment references park decision |
| `Modules/Billing/README.md` §6 | Park wording matches decisions.md required note |

**Not done (by design):** schedule writers, table drop, job re-enable, metrics implying live recognition.

### 2. `_scope-probe` — **Deleted**

| Before | After |
|--------|--------|
| `GET /one/integrations/payments/checkouts/_scope-probe` in `IntegrationProvisionEndpoints` | Removed |
| Phase 1 policy probe | Covered by real M2M `POST/GET /integrations/payments/checkouts` |

Not in TypeSpec; no test route coverage; no FE references.

### 3. Host Application refs — **Fixed** (17.5)

| Item | Result |
|------|--------|
| `Lazuar.Api.csproj` | Removed direct `Modules.Commerce.Application` + `Modules.Communications.Application` ProjectReferences |
| Comment | Host composes via Infrastructure only; Application is transitive for MediatR markers |
| `MediatRRegistrationExtensions` | Note that Application markers must stay transitive (no re-add of Application csproj refs) |
| Arch test | `Host_Csproj_Must_Not_Directly_Reference_Module_Application_Projects` |

Clean rebuild of host succeeds without direct Application refs.

### 4. Stale event twin — **Deleted** (17.3)

- Removed `Modules/Commerce/Contracts/Events/ExecuteOffSessionChargeIntegrationEvent.cs` (unused duplicate).
- Live path remains Payments contracts event + handler + Commerce publishers.

### 5. WhatsApp / Messaging freeze — **Documented** (17.4)

- `Modules/Messaging/README.md` §1 product freeze: no production WhatsApp; thin transport; no merge until 00.4 reopen; console WhatsApp is not live dunning.

### 6. Plans

- `phase-17-analysis.md` — inventory + decisions  
- `checklists/phase-17-deferred-jobs-and-flags.md` — 17.1–17.6 marked done  

## Verification

| Check | Result |
|-------|--------|
| `dotnet build` host (`Lazuar.Api`) clean | **0 warnings, 0 errors** |
| Architecture tests (incl. host csproj rule) | **13/13 passed** |
| `_scope-probe` grep under `apps/` | **Gone** (implementation) |
| `AddHostedService<RevenueRecognitionJob>` | **Still commented / unregistered** |
| Host csproj Application ProjectReference | **None** |

## Exit criteria

| Criterion | Status |
|-----------|--------|
| Every deferred surface is ship, park (documented), or delete | **Yes** |
| No probe endpoints without owners | **Yes** (`_scope-probe` deleted) |
| Host Application refs correct | **Yes** + arch test |

## Explicitly not done

| Item | Why |
|------|-----|
| Implement revenue recognition product path | Product epic (finance / Xero), not maintenance |
| Drop deferred revenue table | Needs product OK |
| MediatR Application registration only inside module DI | Optional ADR 001 polish; transitive refs sufficient |
| Messaging → Communications merge | Phase 16 gate not met |

## Next

Phase 18 — definition of done / residual freeze list honesty pass.
