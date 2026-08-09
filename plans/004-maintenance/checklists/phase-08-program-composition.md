# Phase 08 — Thin `Program.cs` composition root

**Goal:** Host stays orchestration-only; helpers live under clear folders.  
**Evidence:** `../08-composition-di-endpoints.md`  
**PR shape:** Mechanical extract; behavior-preserving.

---

## 08.1 Map current Program.cs sections

- [x] Config / options binding
- [x] Services: JWT, CORS, auth policies
- [x] MediatR assembly registration list
- [x] Module `Add*` / `Use*Subscriptions`
- [x] Migrate-on-boot (9 DbContexts)
- [x] Middleware pipeline order
- [x] Health endpoints
- [x] `Map*Endpoints`

## 08.2 Create host composition helpers (suggested)

Under `apps/lazuar-api/src/Lazuar.Api/` (names flexible):

- [x] `Composition/AuthAndCorsExtensions.cs` (or similar) — JWT, policies, CORS
- [x] `Composition/ModuleRegistrationExtensions.cs` — Add all modules + subscriptions
- [x] `Composition/DatabaseMigrationExtensions.cs` — migrate-on-boot loop
- [x] `Composition/MiddlewarePipelineExtensions.cs` — exception → correlation → CORS → JWT → ApiKey → Tenant → AuthZ
- [x] Optional: `Composition/MediatRRegistration.cs` — assembly list  
  → `MediatRRegistrationExtensions.cs`; also `HealthEndpointExtensions.cs` for health maps

## 08.3 Rules

- [x] Middleware **order unchanged**
- [x] Policy names unchanged
- [x] Module registration order unchanged unless proven safe
- [x] No business logic moved into host from modules

## 08.4 Multi-instance note (document only unless fixing)

- [x] Document migrate-on-boot risk under multi-instance in README or composition comment  
  → XML docs on `DatabaseMigrationExtensions`
- [x] Optional follow-up ticket: migrate job vs boot (do not block this phase)  
  → noted in analysis/done as follow-up; not implemented

## 08.5 Verification

- [x] Host builds  
  → 0 warnings / 0 errors
- [x] App starts against local DB (smoke)  
  → `/health`, `/health/ready`, `/health/metrics` all 200
- [x] Integration or module smoke still pass  
  → ArchitectureTests 12/12
- [x] `Program.cs` line count substantially reduced (target well under ~200 if helpers extract well)  
  → **~488 → ~166 LOC**

## 08.6 Exit criteria

- [x] Program.cs is readable top-level story
- [x] Pipeline order documented next to middleware registration
- [x] No behavior change
