# Phase 07 — Split One `Endpoints.cs` (house style)

**Goal:** Mechanical navigation win; **zero behavior change**.  
**Model after:** `Modules/Commerce/Infrastructure/Endpoints.cs` + `Endpoints/*`  
**Evidence:** `../02-large-files-chunking.md` §3.1  
**PR shape:** One focused PR.

---

## 07.1 Prep

- [ ] Read Commerce `MapCommerceEndpoints` composer pattern
- [ ] Inventory routes in `Modules/One/Infrastructure/Endpoints.cs` (group by domain)
- [ ] List private helpers used by multiple groups (auth cookies, workspace access checks)
- [ ] Decide shared helper location (`Endpoints/OneEndpointHelpers.cs` or keep private in one file)

## 07.2 Create composer + files (names can match analysis)

Target layout under `Modules/One/Infrastructure/`:

- [ ] Keep thin `Endpoints.cs` with `MapOneEndpoints` only
- [ ] Add `Endpoints/AuthEndpoints.cs` — register, login, logout, password/email verify, `/auth/me`
- [ ] Add `Endpoints/ProfileEndpoints.cs` — `/me/profile`, password change
- [ ] Add `Endpoints/WorkspaceEndpoints.cs` — workspaces, members, invites, apps, entitlements
- [ ] Add `Endpoints/WebhookEndpoints.cs` — webhook CRUD + logs + access checks
- [ ] Add `Endpoints/StorageEndpoints.cs` — presigned URL
- [ ] Add `Endpoints/ApiCredentialEndpoints.cs` — org API keys
- [ ] Add `Endpoints/IntegrationProvisionEndpoints.cs` — Aura provision (+ scope-probe if still present)

## 07.3 Move rules

- [ ] Move route maps **without** changing path strings, verbs, policies, or status codes
- [ ] Keep public method name `MapOneEndpoints` stable
- [ ] Preserve middleware order assumptions (group filters)
- [ ] Shared helpers: single place; no circular static deps

## 07.4 Host wiring

- [ ] Confirm `Program.cs` still only calls `MapOneEndpoints` (or equivalent)
- [ ] No duplicate Map* registration

## 07.5 Tests

- [ ] Build One Infrastructure + host
- [ ] Run One-related ModuleTests (auth, api keys, provision, webhooks as applicable)
- [ ] Manual smoke optional: login + list workspaces

## 07.6 Exit criteria

- [ ] Original god-file ≤ ~80 LOC composer (or deleted if renamed consistently)
- [ ] No route behavior change
- [ ] Architecture tests still green
