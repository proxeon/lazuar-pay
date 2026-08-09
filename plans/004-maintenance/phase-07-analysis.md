# Phase 07 — Analysis (One Endpoints split)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Goal:** Mechanical navigation win — split One god-file `Endpoints.cs` into Commerce-style composer + domain files. **Zero route/behavior change.**  
**Evidence:** `checklists/phase-07-one-endpoints-split.md`, `02-large-files-chunking.md` §3.1, Commerce `Infrastructure/Endpoints.cs` + `Endpoints/*`.

---

## 1. Prep inventory

### Commerce model (target pattern)

| Piece | Role |
|-------|------|
| `Modules/Commerce/Infrastructure/Endpoints.cs` | Thin composer: groups + `Map*Endpoints()` calls |
| `Endpoints/ProductEndpoints.cs` etc. | `RouteGroupBuilder` extension methods per domain |
| Namespace | All `Modules.Commerce.Infrastructure` (folder only for nav) |

### One pre-split

| Fact | Detail |
|------|--------|
| Path | `Modules/One/Infrastructure/Endpoints.cs` |
| Size | **767 LOC** god-file |
| Public surface | `MapOneEndpoints` only (host: `Program.cs` → `apiGroup.MapOneEndpoints()`) |
| Group root | `MapGroup("/one").RequireCors()` |

### Route groups by domain

| Domain | Paths (relative to `/one`) | Auth notes |
|--------|----------------------------|------------|
| **Auth** | `/public/register`, `/auth/login\|logout\|forgot-password\|reset-password\|verify-email\|resend-verification\|me` | Mixed public + `RequireAuthorization` |
| **Profile** | `/me/profile`, `/me/security/password` | JWT |
| **Workspace** | `/workspaces*`, invites, members, apps, `/me/entitlements` | JWT; some `OrgAdmin` / system-admin checks |
| **Webhook** | `/workspaces/{id}/webhooks`, logs | Custom `CanAccessWorkspaceWebhooksAsync` |
| **Storage** | `/storage/presigned-url` | JWT + tenant |
| **ApiCredential** | `/api-keys` (nested `OrgAdmin` subgroup) | OrgAdmin only |
| **IntegrationProvision** | `/integrations/workspaces/provision`, `/integrations/payments/checkouts/_scope-probe` | Provision secret / SUPER_ADMIN; scope policy |

### Shared helpers (single-owner after split)

| Helper | Pre-split visibility | Consumers | Placement decision |
|--------|---------------------|-----------|--------------------|
| `IssueCookie` | `private` | Auth only | `AuthEndpoints` private |
| `CanAccessWorkspaceWebhooksAsync` | `internal` (ModuleTests) | Webhooks + 4 unit tests | `WebhookEndpoints` internal |
| `FirstNonEmpty` | `private` | Provision only | `IntegrationProvisionEndpoints` private |

No multi-group shared helpers → **no** `OneEndpointHelpers.cs` required.

### Scope-probe path equivalence

Pre-split registered on root `endpoints`:

```csharp
endpoints.MapGet("/one/integrations/payments/checkouts/_scope-probe", ...)
    .RequireAuthorization("IntegrationPaymentsCheckoutsWrite")
    .RequireCors();
```

Post-split on `/one` group (inherits CORS):

```csharp
group.MapGet("/integrations/payments/checkouts/_scope-probe", ...)
    .RequireAuthorization("IntegrationPaymentsCheckoutsWrite");
```

Final route string and policy unchanged.

---

## 2. Target layout (implemented)

```
Modules/One/Infrastructure/
  Endpoints.cs                          # MapOneEndpoints composer only (~23 LOC)
  Endpoints/
    AuthEndpoints.cs
    ProfileEndpoints.cs
    WorkspaceEndpoints.cs
    WebhookEndpoints.cs
    StorageEndpoints.cs
    ApiCredentialEndpoints.cs
    IntegrationProvisionEndpoints.cs
```

---

## 3. Move rules applied

- [x] Paths, verbs, policies, status codes unchanged (mechanical cut)
- [x] `MapOneEndpoints` name stable
- [x] Group filters: single `/one` + CORS composer; OrgAdmin nested group stays in ApiCredential
- [x] Helpers co-located with only consumer; no circular static deps
- [x] Tests updated: `Endpoints.CanAccessWorkspaceWebhooksAsync` → `WebhookEndpoints.CanAccessWorkspaceWebhooksAsync` (InternalsVisibleTo still applies)

---

## 4. Host wiring

- `Program.cs` still calls `apiGroup.MapOneEndpoints()` only — no duplicate Map registration.

---

## 5. Verification

| Check | Result |
|-------|--------|
| `dotnet build` One Infrastructure | 0 errors |
| `dotnet build` Lazuar.Api host | 0 errors |
| ModuleTests `FullyQualifiedName~One` | **76 passed** |
| ArchitectureTests | **12 passed** |

---

## 6. Explicit non-goals

- Behavior/auth/policy changes  
- DTOs/commands extraction  
- Moving custom-checkout-style leftovers from Commerce composer  
- OpenAPI / TypeSpec regeneration  
