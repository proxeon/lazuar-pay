# Phase 07 — Split One `Endpoints.cs` (house style)

**Goal:** Mechanical navigation win; **zero behavior change**.  
**Model after:** `Modules/Commerce/Infrastructure/Endpoints.cs` + `Endpoints/*`  
**Evidence:** `../02-large-files-chunking.md` §3.1  
**PR shape:** One focused PR.

---

## 07.1 Prep

- [x] Read Commerce `MapCommerceEndpoints` composer pattern
- [x] Inventory routes in `Modules/One/Infrastructure/Endpoints.cs` (group by domain)
- [x] List private helpers used by multiple groups (auth cookies, workspace access checks)  
  → Each helper had a single consumer after split; no shared helpers file needed.
- [x] Decide shared helper location (`Endpoints/OneEndpointHelpers.cs` or keep private in one file)  
  → Co-locate: `IssueCookie` → Auth; `CanAccessWorkspaceWebhooksAsync` → Webhook; `FirstNonEmpty` → IntegrationProvision.

## 07.2 Create composer + files (names can match analysis)

Target layout under `Modules/One/Infrastructure/`:

- [x] Keep thin `Endpoints.cs` with `MapOneEndpoints` only
- [x] Add `Endpoints/AuthEndpoints.cs` — register, login, logout, password/email verify, `/auth/me`
- [x] Add `Endpoints/ProfileEndpoints.cs` — `/me/profile`, password change
- [x] Add `Endpoints/WorkspaceEndpoints.cs` — workspaces, members, invites, apps, entitlements
- [x] Add `Endpoints/WebhookEndpoints.cs` — webhook CRUD + logs + access checks
- [x] Add `Endpoints/StorageEndpoints.cs` — presigned URL
- [x] Add `Endpoints/ApiCredentialEndpoints.cs` — org API keys
- [x] Add `Endpoints/IntegrationProvisionEndpoints.cs` — Aura provision (+ scope-probe if still present)

## 07.3 Move rules

- [x] Move route maps **without** changing path strings, verbs, policies, or status codes
- [x] Keep public method name `MapOneEndpoints` stable
- [x] Preserve middleware order assumptions (group filters)
- [x] Shared helpers: single place; no circular static deps

## 07.4 Host wiring

- [x] Confirm `Program.cs` still only calls `MapOneEndpoints` (or equivalent)
- [x] No duplicate Map* registration

## 07.5 Tests

- [x] Build One Infrastructure + host
- [x] Run One-related ModuleTests (auth, api keys, provision, webhooks as applicable)  
  → 76 passed (`FullyQualifiedName~One`)
- [ ] Manual smoke optional: login + list workspaces

## 07.6 Exit criteria

- [x] Original god-file ≤ ~80 LOC composer (or deleted if renamed consistently)  
  → ~23 LOC composer
- [x] No route behavior change
- [x] Architecture tests still green  
  → 12/12
