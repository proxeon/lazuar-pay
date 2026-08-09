# Phase 08 — Thin `Program.cs` composition root

**Goal:** Host stays orchestration-only; helpers live under clear folders.  
**Evidence:** `../08-composition-di-endpoints.md`  
**PR shape:** Mechanical extract; behavior-preserving.

---

## 08.1 Map current Program.cs sections

- [ ] Config / options binding
- [ ] Services: JWT, CORS, auth policies
- [ ] MediatR assembly registration list
- [ ] Module `Add*` / `Use*Subscriptions`
- [ ] Migrate-on-boot (9 DbContexts)
- [ ] Middleware pipeline order
- [ ] Health endpoints
- [ ] `Map*Endpoints`

## 08.2 Create host composition helpers (suggested)

Under `apps/lazuar-api/src/Lazuar.Api/` (names flexible):

- [ ] `Composition/AuthAndCorsExtensions.cs` (or similar) — JWT, policies, CORS
- [ ] `Composition/ModuleRegistrationExtensions.cs` — Add all modules + subscriptions
- [ ] `Composition/DatabaseMigrationExtensions.cs` — migrate-on-boot loop
- [ ] `Composition/MiddlewarePipelineExtensions.cs` — exception → correlation → CORS → JWT → ApiKey → Tenant → AuthZ
- [ ] Optional: `Composition/MediatRRegistration.cs` — assembly list

## 08.3 Rules

- [ ] Middleware **order unchanged**
- [ ] Policy names unchanged
- [ ] Module registration order unchanged unless proven safe
- [ ] No business logic moved into host from modules

## 08.4 Multi-instance note (document only unless fixing)

- [ ] Document migrate-on-boot risk under multi-instance in README or composition comment
- [ ] Optional follow-up ticket: migrate job vs boot (do not block this phase)

## 08.5 Verification

- [ ] Host builds
- [ ] App starts against local DB (smoke)
- [ ] Integration or module smoke still pass
- [ ] `Program.cs` line count substantially reduced (target well under ~200 if helpers extract well)

## 08.6 Exit criteria

- [ ] Program.cs is readable top-level story
- [ ] Pipeline order documented next to middleware registration
- [ ] No behavior change
