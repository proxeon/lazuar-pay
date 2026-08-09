# Phase 08 — Analysis (Program.cs composition thinning)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Goal:** Host stays orchestration-only; helpers live under `Lazuar.Api/Composition/`. Mechanical extract; **zero behavior change**.  
**Evidence:** `checklists/phase-08-program-composition.md`, `08-composition-di-endpoints.md` §2 / §9.1.

---

## 1. Pre-extract Program.cs map

**Path:** `apps/lazuar-api/src/Lazuar.Api/Program.cs`  
**Size before:** **~488 LOC**

| Section | Concern | Extract target |
|---------|---------|----------------|
| .env loader + Key Vault + Serilog | Config bootstrap | Stay in Program (optional later) |
| Options, metrics, Resend client, platform services, R2 | Platform infra | Stay in Program (optional later) |
| Host event handler DI | Host | Stay in Program (register) |
| JWT + policies + CORS | AuthN/Z + CORS | `AuthAndCorsExtensions` |
| JSON + exception handler | API conventions | Stay in Program (thin) |
| MediatR assembly list | MediatR | `MediatRRegistrationExtensions` |
| `Add*Module` | Module DI | `ModuleRegistrationExtensions.AddAllModules` |
| Migrate-on-boot (9 DbContexts) | Boot | `DatabaseMigrationExtensions` |
| Middleware pipeline | Pipeline | `MiddlewarePipelineExtensions` |
| `Use*Subscriptions` + host dual-subscribe | Event bus | `ModuleRegistrationExtensions` |
| Health endpoints | Health | `HealthEndpointExtensions` |
| `Map*Endpoints` | Routes | `ModuleRegistrationExtensions.MapAllModuleEndpoints` |

### Middleware order (load-bearing — must preserve)

```
UseExceptionHandler
→ CorrelationIdMiddleware
→ UseCors
→ UseAuthentication          // JWT cookie/bearer
→ ApiKeyAuthenticationMiddleware
→ TenantSecurityMiddleware
→ UseAuthorization
```

### Policy catalog (names unchanged)

`OrgAdmin`, `IntegrationLhdnDocumentsWrite`, `IntegrationLhdnDocumentsRead`, `IntegrationPaymentsCheckoutsWrite`, `IntegrationPaymentsCheckoutsRead`, `IntegrationPaymentsConfigRead`, `IntegrationWebhooksEndpointsManage`

### Module registration order (unchanged)

One → Messaging → CRM → Payments → Ops → Billing → Lhdn → Commerce → Communications  
(same order for Add / UseSubscriptions; Map order matches prior Program)

### Migrate list (9 contexts, order unchanged)

One, Messaging, Payments, Crm, Ops, Billing, Lhdn, Commerce, Communications

---

## 2. Target layout (implemented)

```
apps/lazuar-api/src/Lazuar.Api/
  Program.cs                              # orchestration story (~166 LOC)
  Composition/
    AuthAndCorsExtensions.cs              # JWT, policies, CORS
    MediatRRegistrationExtensions.cs      # assembly list
    ModuleRegistrationExtensions.cs       # AddAll / UseSubscriptions / host events / MapAll
    DatabaseMigrationExtensions.cs        # MigrateAllModuleDatabasesAsync + multi-instance note
    MiddlewarePipelineExtensions.cs       # UseLazuarPipeline + order docs
    HealthEndpointExtensions.cs           # /health, /health/ready, /health/metrics
```

### Target Program story

```csharp
// config / platform infra (still inline)
builder.Services.AddLazuarAuthentication(...);
builder.Services.AddLazuarAuthorizationPolicies();
builder.Services.AddLazuarCors(...);
builder.Services.AddLazuarMediatR();
builder.Services.AddAllModules(...);

var app = builder.Build();
await app.MigrateAllModuleDatabasesAsync();
app.UseLazuarPipeline();
app.UseAllModuleSubscriptions();
app.UseHostEventSubscriptions();
app.MapHealthEndpoints();
app.MapAllModuleEndpoints();
await app.RunAsync();
```

---

## 3. Rules applied

- [x] Middleware **order unchanged**
- [x] Policy names unchanged
- [x] Module Add/Use/Map order unchanged
- [x] No business logic moved from modules into host
- [x] Multi-instance migrate-on-boot risk documented on `DatabaseMigrationExtensions` XML doc

---

## 4. Explicitly not extracted (optional follow-ups)

- `.env` loader / Key Vault / Serilog → `ConfigurationExtensions`
- Options + metrics + Resend + R2 + platform singletons → `PlatformInfrastructure`
- These remain readable in Program and can be sliced later without changing module APIs.

---

## 5. Verification plan

| Check | How |
|-------|-----|
| Host builds | `dotnet build src/Lazuar.Api/Lazuar.Api.csproj` |
| Architecture tests | `dotnet test tests/Lazuar.ArchitectureTests` |
| App starts + health | `dotnet run` against local Postgres; `GET /health`, `/health/ready`, `/health/metrics` |
| Line count | Program.cs well under ~200 |
