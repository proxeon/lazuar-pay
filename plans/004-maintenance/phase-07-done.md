# Phase 07 — Done

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Commit subject:** `refactor(one): split Endpoints into Commerce-style files (phase 07)`

## What landed

### Composer

- `Modules/One/Infrastructure/Endpoints.cs` — thin `MapOneEndpoints` (~23 LOC): `/one` + CORS group, then domain `Map*Endpoints()` calls.

### Domain files (`Endpoints/`)

| File | Responsibility |
|------|----------------|
| `AuthEndpoints.cs` | Register, login/logout, password/email verify, `/auth/me` + `IssueCookie` |
| `ProfileEndpoints.cs` | `/me/profile`, password change |
| `WorkspaceEndpoints.cs` | Workspaces, members, invites, apps, entitlements |
| `WebhookEndpoints.cs` | Webhook CRUD + logs + `CanAccessWorkspaceWebhooksAsync` |
| `StorageEndpoints.cs` | Presigned URL |
| `ApiCredentialEndpoints.cs` | Org API keys (`OrgAdmin` subgroup) |
| `IntegrationProvisionEndpoints.cs` | Aura provision + scope-probe + `FirstNonEmpty` |

### Tests

- `ProvisionAuraWorkspaceTests` companion auth helpers now call `WebhookEndpoints.CanAccessWorkspaceWebhooksAsync`.

### Plans

- `phase-07-analysis.md` — inventory, helpers, verification  
- `checklists/phase-07-one-endpoints-split.md` — criteria marked done  

## Exit criteria

| Criterion | Status |
|-----------|--------|
| God-file ≤ ~80 LOC composer | Yes (~23 LOC) |
| No route behavior change | Mechanical split; paths/verbs/policies preserved |
| Architecture tests green | 12/12 |
| One ModuleTests green | 76/76 |

## Explicitly not done

- Manual smoke (login + list workspaces)  
- Further god-file splits (Program, provision command, dunning — later phases)  

## Next

Phase 08 Program composition (or next checklist item on the maintenance track).
