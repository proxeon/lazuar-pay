# 07 — One: identity, workspaces, invites, roles, API keys, audit

**Date:** 17 August 2026  
**Branch:** `feat/007-waves-1-4-implement`  
**HEAD:** `297ba98` — `fix(one): add /accept-invite on ops and mint invite URLs there`  
**Product:** Lazuar Pay (Compliance CaaS / headless checkout)  
**Slice:** One module — signup/login, workspaces, memberships, roles, Team invites, accept-invite API, API keys (`sk_test_` / `sk_live_`), audit log, platform vs tenant auth.  
**This file is evidence.** It is not a plan and not a patch. Do not collapse it into a bullet list and throw the citations away.

Parent: [README.md](./README.md). Sibling slices own ops AcceptInvitePage *pixels* (09), commerce magic-link tokens (03), and payments adapter keys (04). This report still has to *read* the ops accept page and the host auth pipeline, because those files are the other half of One’s invite and cookie contracts. It does not audit checkout CSS.

008 evaluated this slice the day before (`plans/008-evals/05-identity-roles-keys-audit.md`). That report’s largest product hole was “the accept page is missing; invite mail points at `App:ClientUrl`.” `297ba98` shipped a page and flipped the invite URL to `App:OpsUrl`. This report re-reads the tree **after** that commit. A bug 008 filed is closed only if this tree no longer contains it. A bug 008 missed is still written up.

---

## 0. Method

Read-only. No fixes. No commits. Tests were not executed; claims about tests are from reading the test source. Email was not sent. Postgres unique-index behaviour is inferred from EF configuration plus `GlobalExceptionHandler`, not from a live 500.

### 0.1 What was read

Absolute paths unless noted. Line numbers are as of `297ba98`.

| Concern | Path |
|---------|------|
| One HTTP mount | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs` |
| Register / login / cookie / rate-limit key | `.../Endpoints/AuthEndpoints.cs` |
| Workspaces / members / invites / accept / entitlements / audit GET | `.../Endpoints/WorkspaceEndpoints.cs` |
| Profile / change password | `.../Endpoints/ProfileEndpoints.cs` |
| API keys HTTP | `.../Endpoints/ApiCredentialEndpoints.cs` |
| Integrator provision HTTP | `.../Endpoints/IntegrationProvisionEndpoints.cs` |
| Storage presign | `.../Endpoints/StorageEndpoints.cs` |
| Webhook authZ helper | `.../Endpoints/WebhookEndpoints.cs` (from `CanAccessWorkspaceWebhooksAsync`) |
| Platform cookie realm | `.../Endpoints/PlatformAuthEndpoints.cs` |
| Register command | `.../Application/Commands/RegisterPublicUserCommand.cs` |
| Create / update / archive workspace | `CreateWorkspaceCommand.cs`, `UpdateWorkspaceCommand.cs`, `ArchiveWorkspaceCommand.cs` |
| Invite / accept / revoke / remove | `InviteUserToWorkspaceCommand.cs`, `AcceptWorkspaceInvitationCommand.cs`, `RevokeWorkspaceInvitationCommand.cs`, `RemoveWorkspaceMemberCommand.cs` |
| Forgot / reset / verify / resend | `ForgotPasswordCommand.cs`, `ResetPasswordCommand.cs`, `VerifyEmailCommand.cs`, `ResendVerificationEmailCommand.cs` |
| Keys mint / revoke | `GenerateApiCredentialCommand.cs`, `RevokeApiCredentialCommand.cs` |
| Provision hatch | `ProvisionAuraWorkspaceCommandHandler*.cs` |
| Invite / reset / verify URL builder | `.../Application/EventHandlers/NotificationDispatchDomainEventHandlers.cs` |
| Link service | `.../Infrastructure/Services/OneLinkService.cs`, `.../Application/IOneLinkService.cs` |
| Repo + query + rate limiter + audit + credentials façade + provision auth + genesis | `OneRepository.cs`, `OneQueryService.cs`, `PublicRegisterRateLimiter.cs`, `AuditRecorder.cs`, `ApiCredentialService.cs`, `IntegratorProvisionAuth.cs`, `SystemGenesisBootstrapperJob.cs` |
| Domain | `GlobalUser.cs`, `Organization.cs`, `TenantMembership.cs`, `WorkspaceInvitation.cs`, `WorkspaceStaffRoles.cs`, `PlatformApiScopes.cs`, `ApiCredential.cs`, `AuditEvent.cs`, `TenantAppEntitlement.cs`, `Rules/OrganizationSlugMustBeValidRule.cs` |
| Db model | `OneDbContext.cs` |
| Host auth / CORS / pipeline / tenant / API key / accessor | `apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs`, `MiddlewarePipelineExtensions.cs`, `ModuleRegistrationExtensions.cs`, `Middleware/TenantSecurityMiddleware.cs`, `Middleware/ApiKeyAuthenticationMiddleware.cs`, `ExecutionContextAccessor.cs`, `EventHandlers/ApiKeyRevokedIntegrationEventHandler.cs` |
| Exception mapping | `BuildingBlocks/Infrastructure/GlobalExceptionHandler.cs` |
| Domain-event vs persist order | `BuildingBlocks/Infrastructure/PlatformDbContext.cs` |
| Token hash / password / JWT / outbox | `TokenGeneratorService.cs`, `PasswordService.cs`, `JwtService.cs`, `OutboxEventBus.cs` |
| Invite email delivery | `Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs`, `Email/ResendEmailService.cs` |
| Ops accept / login / empty / team / audit / client | `apps/lazuar-ops/src/App.tsx`, `components/LoginPage.tsx`, `components/EmptyWorkspaceState.tsx`, `modules/workspace/pages/AcceptInvitePage.tsx`, `TeamPage.tsx`, `AuditLogPage.tsx`, `lib/api-client.ts`, `lib/workspace-slug.ts` |
| Portal leftover redirect | `apps/lazuar-portal/src/app/accept-invite/page.tsx`, `not-found.tsx` |
| Config | `apps/lazuar-api/src/Lazuar.Api/appsettings.json`, `appsettings.Development.json`, `Configuration/AppOptions.cs`, `deploy/prod/env.example` |
| TypeSpec | `packages/api-spec/modules/one/routes.tsp`, `models/auth.tsp` |
| Tests | `apps/lazuar-api/tests/Lazuar.ModuleTests/One/*` (all 24 files listed below), plus `ArchitectureTests/TenantIsolationArchitectureTests.cs` (exempt-path block) |
| 008 baseline | `plans/008-evals/05-identity-roles-keys-audit.md` |
| One README (drift) | `Modules/One/README.md` |
| LHDN key façade (comment lie) | `Modules/Lhdn/Infrastructure/Endpoints/AdminApiKeyEndpoints.cs`, `Modules/Lhdn/Domain/ApiKeyScopes.cs` |

### 0.2 Tests in `Lazuar.ModuleTests/One/`

`AcceptWorkspaceInvitationCommandHandlerTests.cs`, `ApiKeyAuthenticationTests.cs`, `AuditRecorderTests.cs`, `CreateWorkspaceCommandHandlerTests.cs`, `GenerateAndListApiCredentialsTests.cs`, `GetPublicPricingQueryHandlerTests.cs`, `InviteUserToWorkspaceCommandHandlerTests.cs`, `LegacyApiKeyMigratorTests.cs`, `LegacyWebhookSubscriptionMigratorTests.cs`, `OneLinkServiceTests.cs`, `OrganizationBrandingTests.cs`, `OrganizationSlugMustBeValidRuleTests.cs`, `OutboundWebhookClaimTests.cs`, `OutboundWebhookTests.cs`, `PlatformAdminAuthQueryTests.cs`, `ProvisionAuraWorkspaceTests.cs`, `PublicRegisterRateLimiterTests.cs`, `PublicWorkspaceBrandingTests.cs`, `RedeliverWebhookDeliveryTests.cs`, `RegisterPublicUserCommandHandlerTests.cs`, `WebhookEndpointLifecycleTests.cs`, `WebhookSecretVaultTests.cs`, `WorkspaceCreateAuthorizationTests.cs`.

Webhook / branding / migrator tests are in this folder but are **not** the identity slice except where they prove key hashing or tenant filters. They were skimmed, not line-walked.

### 0.3 Unread / not executed

- Live HTTP against a running API. No `dotnet test`. No browser click of `/accept-invite`.
- `apps/lazuar-admin` login UI pixels (platform cookie is in scope; the admin SPA chrome is 09).
- Commerce magic-link token service (03).
- Payments adapter secrets / webhook HMAC keys (04).
- Outbound webhook dispatcher internals beyond the membership check (10 / 04).
- TypeSpec honesty CI (`packages/api-spec/honesty-allowlist.yaml`) as a contract program.
- Production Caddy path map beyond `deploy/prod/env.example` (`App__OpsUrl=https://hub.lazuar.com`, `App__ClientUrl=https://hub.lazuar.com/portal`).
- Whether a deployed env actually has `active_legacy_only = 0` (R05 deploy gate). The middleware is One-only in this tree; leftover Lhdn-only keys 401. That is a deploy risk, not a code bug in One.

---

## 1. What One is in this tree

One is the **global identity and tenant registry**. It is not the buyer portal (Commerce magic links), not the merchant console itself (`lazuar-ops` is a SPA that consumes One), and not the platform-admin product (cookie `lazuar_admin_auth` on `/api/v1/platform`).

`MapOneEndpoints` mounts under `/api/v1` (`ModuleRegistrationExtensions.cs:67–69`) via `MapGroup("/one")` (`Endpoints.cs:11`).

### 1.1 Aggregates in `one` schema (`OneDbContext.cs`)

| DbSet | Table | Tenant-scoped (`IMustHaveTenant`)? |
|-------|-------|-------------------------------------|
| `Organizations` | `one.Organizations` | No (root). Unique `Slug`. Filtered unique `(ExternalProduct, ExternalOrgId)`. |
| `GlobalUsers` | `one.GlobalUsers` | No. Unique `Email`. Filtered unique hashes for verify/reset tokens. |
| `TenantMemberships` | `one.TenantMemberships` | Yes. Unique `(GlobalUserId, OrganizationId)`. |
| `TenantAppEntitlements` | `one.TenantAppEntitlements` | Yes. Unique `(OrganizationId, AppId)`. |
| `WorkspaceInvitations` | `one.WorkspaceInvitations` | Yes. Unique `TokenHash`. **Non-unique** filtered index `(OrganizationId, Email) WHERE Status = 'PENDING'`. |
| `TenantWebhookEndpoints` / `WebhookDeliveryOutboxes` | yes | Yes |
| `ApiCredentials` | `one.ApiCredentials` | Yes. Unique `KeyHash`. |
| `AuditEvents` | `one.AuditEvents` | Yes. Index `(OrganizationId, CreatedAt)`. |
| Outbox / Inbox | `one.OutboxMessages` / `one.InboxMessages` | Platform infra |

There is still **no** `AppAccessRequest` type. `RegisterPublicUserCommandHandlerTests.Handler_And_OneDbContext_Have_No_AppAccessRequest` still locks that.

`UserRegisteredDomainEvent` is still raised in `GlobalUser` ctor (`GlobalUser.cs:44`) and still has **zero handlers** (grep hits only the record and the ctor).

EF fail-closed: `PlatformDbContext` stamps `IMustHaveTenant` with ambient `TenantId` and refuses empty `OrganizationId` (`PlatformDbContext.cs:50–76`). Global filter: `e.OrganizationId == ExecutionContext.TenantId` (`:45–46`). Empty ambient tenant matches **no** rows. One list/get members/invites/audit **IgnoreQueryFilters** and predicate on the path id. That is correct for the exempt `/one/workspaces` prefix; it also means a forgotten `OrganizationId == id` predicate is a cross-tenant read.

### 1.2 HTTP map (identity-relevant)

| Method | Path | Auth as coded |
|--------|------|----------------|
| GET | `/one/public/pricing` | Anonymous |
| POST | `/one/public/register` | Anonymous + `accepted_terms` + in-process rate limit |
| POST | `/one/auth/login` | Anonymous, **no** rate limit |
| POST | `/one/auth/logout` | Anonymous (deletes cookie, see B07-I06) |
| POST | `/one/auth/forgot-password` | Anonymous, silent miss, **no** rate limit |
| POST | `/one/auth/reset-password` | Anonymous |
| POST | `/one/auth/verify-email` | `RequireAuthorization` + session user, **not** the email query |
| POST | `/one/auth/resend-verification` | Anonymous, silent miss |
| GET | `/one/auth/me` | Auth + **security stamp** |
| PUT | `/one/me/profile` | Auth |
| PUT | `/one/me/security/password` | Auth |
| GET | `/one/me/entitlements` | Auth (memberships, or every active org if `IsSystemAdmin`) |
| POST | `/one/workspaces` | Auth, any human with `UserId` |
| GET | `/one/workspaces` | `OrgAdmin` **and** `ctx.IsSystemAdmin` |
| GET/PUT/DELETE | `/one/workspaces/{id}` | Auth + membership / exact `ADMIN` in command |
| GET | `/one/workspaces/{id}/members` | Auth + `HasTenantAccess` |
| POST | `/one/workspaces/{id}/invites` | `OrgAdmin` + command `CanManageMembers` |
| GET | `/one/workspaces/{id}/invites` | Auth + `HasTenantAccess` |
| DELETE | `/one/workspaces/{id}/invites/{inviteId}` | `OrgAdmin` + command |
| DELETE | `/one/workspaces/{id}/members/{userId}` | `OrgAdmin` + command |
| POST | `/one/workspaces/invites/accept` | Auth |
| GET | `/one/workspaces/{id}/audit` | Auth + `HasTenantAccess` or system admin (**403** Forbid, not 401) |
| GET/POST | `/one/workspaces/{id}/apps`… | Superadmin + `OrgAdmin` |
| GET/POST/DELETE | `/one/api-keys` | `OrgAdmin` + required tenant |
| POST | `/one/storage/presigned-url` | Auth + required tenant |
| POST | `/one/integrations/workspaces/provision` | Provision secret or `IsInRole("SUPER_ADMIN")` (see B07-I20) |
| GET | `/public/one/{tenantSlug}/branding` | Anonymous |
| POST/GET | `/platform/auth/*` | Separate cookie realm |

TypeSpec `packages/api-spec/modules/one/routes.tsp` matches this surface for the identity routes, including accept (`:161–166`) and audit (`:154–159`). Authenticated routes are still `@useAuth(BearerAuth)` while the live session is an HttpOnly cookie. Bearer JWT still works if a client sends it **and** the cookie is absent (see §3.4).

### 1.3 Two cookie realms

`AuthAndCorsExtensions.AddLazuarAuthentication` (`AuthAndCorsExtensions.cs:52–64`) picks the cookie by path:

- `/api/v1/platform*` → `lazuar_admin_auth`
- everything else → `lazuar_auth`

They share issuer, audience, and signing secret. Production refuses the default secret (`:22–29`). Repo `appsettings.json` has `"Jwt:Secret": ""` (`:24`) so non-Production falls back to `secure_development_key_minimum_32_characters_long` (`AuthAndCorsExtensions.cs:14, 31` and `AuthEndpoints.cs:188`).

Ops login/register issues `lazuar_auth` via `IssueCookie` (`AuthEndpoints.cs:185–216`). Platform login issues `lazuar_admin_auth` with `Path = /api/v1/platform` (`PlatformAuthEndpoints.cs:135–145`).

Pipeline order is load-bearing (`MiddlewarePipelineExtensions.cs:19–28`):

1. Exception handler  
2. Correlation id  
3. CORS  
4. JWT authentication (cookie or Bearer)  
5. **API key middleware** (may **replace** `context.User`)  
6. **Tenant security**  
7. Authorization  

A request that sends both a cookie and `Authorization: Bearer sk_live_…` becomes an `API_CLIENT`. The human is gone.

---

## 2. Mechanics — JWT `CLIENT` vs membership `ADMIN` / `MEMBER` / `VIEWER` vs machine `API_CLIENT`

Waves 1–4 did not remove the dual model. `297ba98` did not touch it.

### 2.1 Layer 1 — JWT / cookie role (global)

`IssueCookie` always writes (`AuthEndpoints.cs:193–201`):

```
ClaimTypes.NameIdentifier = user.Id
ClaimTypes.Email = user.Email
ClaimTypes.Role = user.IsSystemAdmin ? "SUPER_ADMIN" : "CLIENT"
is_system_admin = bool
is_email_verified = bool
security_stamp = Guid
```

Login **body** uses the same SUPER_ADMIN / CLIENT split (`AuthEndpoints.cs:93, 97–100`). Register **body** says `"ADMIN"` (`:71`) while the cookie still says `"CLIENT"`. That mismatch is unchanged from 008 and from the pre-wave gap note.

TTL is `Jwt:ExpiryHours` default 24 (`appsettings.json:27`, `AuthEndpoints.cs:191`). Cookie: HttpOnly, Secure outside Development, SameSite=Lax, Domain `.lazuar.com` outside dev (`AuthEndpoints.cs:206–213`). No refresh token. No server-side session table.

Security stamp rotates on password change / reset (`GlobalUser.cs:55–58, 71–77`) and is **checked only on** `/one/auth/me` and `/platform/auth/me` (`AuthEndpoints.cs:148–153`, `PlatformAuthEndpoints.cs:91–96`). Every other route accepts a stolen cookie until expiry.

### 2.2 Layer 2 — Membership role (workspace)

`TenantMembership.Role` is still a free string. The entity comment still says `"ADMIN", "CLIENT"` (`TenantMembership.cs:10`). The **invite path** allow-lists via `WorkspaceStaffRoles` (`WorkspaceStaffRoles.cs:1–28`):

| Constant | Value | Who can hold it |
|----------|-------|-----------------|
| `Admin` | `ADMIN` | Invite, remove, keys, gateways, legal, email BYOK, billing admin, workspace update/archive |
| `Member` | `MEMBER` | Commerce operate |
| `Viewer` | `VIEWER` | GET / list |
| `SuperAdmin` | `SUPER_ADMIN` | Recognized by `CanManageMembers`; **not** invitable (`NormalizeInvitedRole` rejects it). **Provision can attach it** (`ProvisionAuraWorkspaceCommandHandler.Normalizers.cs:51–66`, `AllowedOwnerRoles` includes `SUPER_ADMIN` at `ProvisionAuraWorkspaceCommandHandler.cs:40–44`). |

`NormalizeInvitedRole` throws `"Role must be ADMIN, MEMBER, or VIEWER."` (`WorkspaceStaffRoles.cs:12–20`). Tests reject `HACKER`, `banana`, **`CLIENT`** (`InviteUserToWorkspaceCommandHandlerTests.cs:46–65`). `CLIENT` is therefore **not** a staff role. It remains the JWT role for every non-system human.

`CanManageMembers` is `ADMIN` or `SUPER_ADMIN` (`WorkspaceStaffRoles.cs:23–27`).

There is still **no** `ChangeRole`, no transfer-ownership, no last-admin guard. Role is write-once at membership create.

Update/archive commands compare membership to **exactly** `"ADMIN"` (`UpdateWorkspaceCommand.cs:32`, `ArchiveWorkspaceCommand.cs:25`). A `SUPER_ADMIN` membership **cannot** rename or archive the workspace. That is the inverse of “SUPER_ADMIN is more powerful.”

### 2.3 Layer 3 — Machine role

`ApiKeyAuthenticationMiddleware` sets `ClaimTypes.Role = API_CLIENT`, `NameIdentifier = "api_client"`, `TenantId` from the credential row, `IsTestMode` from the **presented token prefix**, plus one `scope` claim per stored scope (`ApiKeyAuthenticationMiddleware.cs:70–93`). Humans cannot become `API_CLIENT`. Keys cannot become `ADMIN`. `OrgAdmin` no longer includes `API_CLIENT` (`AuthAndCorsExtensions.cs:76–80`). Tests lock that (`ApiKeyAuthenticationTests.cs:237–266`).

Closed catalog (`PlatformApiScopes.cs:43–52`):

- `lhdn.documents:write` / `lhdn.documents:read`
- `payments.checkouts:write` / `payments.checkouts:read`
- `webhooks.endpoints:manage`
- `commerce.subscriptions:read` / `commerce.subscriptions:write`

`payments.config:read` is rejected (`GenerateAndListApiCredentialsTests.GenerateApiCredential_PaymentsConfigRead_Is_Unknown`). Omit / empty scopes throw. No implicit LHDN default.

### 2.4 How the layers collide

Ops always attaches `X-Tenant-Id` from `localStorage.ops_active_workspace_id` (`api-client.ts:13–24`). Middleware then **adds** the membership role as a second `ClaimTypes.Role` (`TenantSecurityMiddleware.cs:83–88`). `IsInRole("ADMIN")` becomes true for an owner even though the JWT still says `CLIENT`.

If the header is missing (curl, Scalar “try it”):

- JWT role is `CLIENT`.
- `OrgAdmin` / `OrgMember` / `OrgRead` all **fail** (`RequireRole` does not include `CLIENT`).
- `RequireAuthorization()`-only routes still work (profile, create workspace, accept invite).

`ExecutionContextAccessor.UserRole` returns **one** `ClaimTypes.Role` (`ExecutionContextAccessor.cs:38`) — whichever the principal finds first. Do not use `UserRole` as the membership role. `IsSystemAdmin` reads the JWT bool claim (`:39`), not the membership string. That split is load-bearing and is also how the provision confused-deputy in B07-I20 happens.

`IntegratorProvisionAuth` treats `principal.IsInRole("SUPER_ADMIN")` as platform superadmin **even when `is_system_admin` is false** (`IntegratorProvisionAuth.cs:73–76`). Combined with tenant middleware injecting a membership role of `SUPER_ADMIN` on an exempt path, a provisioned “workspace SUPER_ADMIN” who still has JWT `CLIENT` can call `POST /one/integrations/workspaces/provision`. That is not “invite role escalation.” It is “the hatch’s own role string leaks into the hatch’s auth.”

---

## 3. Quoted walk — public register, login, cookie, entitlements

### 3.1 Register

`POST /api/v1/one/public/register` (`AuthEndpoints.cs:32–73`):

1. Requires email, password, `workspace_name`, `tenant_slug` (`:40–45`).
2. Requires `accepted_terms == true` (`:47–48`). TypeSpec field is **optional** (`models/auth.tsp:14–15`). The handler is the enforcement, not the schema.
3. Rate-limits via `PublicRegisterRateLimiter` (10 / 10 min, key `email:{email}|ip:{ip}`).
4. Sends `RegisterPublicUserCommand`.
5. Issues `lazuar_auth`.
6. Returns `LoginResponse` with body role `"ADMIN"`.

Command (`RegisterPublicUserCommand.cs:34–75`):

1. Normalizes email and slug to lower-case.
2. Rejects duplicate email (`already exists`).
3. Rejects taken slug (`already taken`).
4. Constructs `Organization` **before** tracking the user (`:54–58`) so reserved/malformed slugs throw `BusinessRuleValidationException` and write nothing. Tests cover `admin`, `portal`, `system`, `billplz`, `ab`, `acme--corp`, `-acme`.
5. Creates `GlobalUser` unverified, not system admin. **Does not** call `SetEmailVerificationToken`.
6. Membership role `"ADMIN"` (`:61`).
7. Entitlements hardcoded `OPS`, `BILLING`, `PAYMENTS`, `CRM`, `LHDN` (`:22, 64–70`). **Not `COMMERCE`.** Tests lock that (`RegisterPublicUserCommandHandlerTests.cs:107–108`). Commerce HTTP is not entitlement-gated in the host.
8. Publishes `AppEntitlementGrantedIntegrationEvent` per module **before** `SaveChanges` (`:70` then `:72`). Those writes go to the One outbox (`OutboxEventBus`) and persist with the user/org in the same `SaveChanges`. Domain events themselves (`UserRegistered`, `OrganizationCreated`) are dispatched by `PlatformDbContext` **before** the SQL commit (`PlatformDbContext.cs:78–103`).

`accepted_terms` is stored **nowhere**. We cannot later prove which version a tenant accepted.

Ops `/signup` (`LoginPage.tsx:80–132`) still **always** sends `workspace_name` + `tenant_slug`. There is no “I was invited, skip workspace” flag on `PublicRegisterRequestDto`. After 200 it hard-navigates to `returnUrl` or `/commerce/dashboard` (`:126`).

Known leftover, as the task said: register always creates a personal workspace. **This does not break accept** if the invited email matches — see §4.6. It *does* mint a second tenant + five entitlements + starter-credit side effects for every new invitee who uses Sign up.

### 3.2 Login

`POST /one/auth/login` (`AuthEndpoints.cs:75–101`):

- Unauthenticated, **unlimited**.
- Missing user, inactive user, or bad password → `400` ProblemDetails with **embedded `Status = 401`** (`:88–90`). HTTP status is 400. Clients that key off HTTP status see “bad request.” Clients that key off `status` in the body see 401.
- Does **not** check `IsEmailVerified`.
- No lockout, no CAPTCHA, no audit row.

Forgot-password and resend-verification are also unlimited. Enumeration on forgot/resend is partially mitigated by silent no-op (`ForgotPasswordCommand.cs:24`, `ResendVerificationEmailCommand.cs:28–29`). Reset-password is **not** silent — see B07-I17.

### 3.3 Logout and stamp

Logout (`AuthEndpoints.cs:103–107`):

```csharp
ctx.Response.Cookies.Delete("lazuar_auth");
return TypedResults.Ok(new StatusResponse { Status = "logged_out" });
```

Issue uses `Domain = ".lazuar.com"` outside Development (`:211`). Delete is called **without** `CookieOptions.Domain` (and without matching `Secure` / `SameSite`). ASP.NET Core deletes a cookie by emitting an expired Set-Cookie; Path/Domain/Secure/SameSite must match the cookie that was set. In production the logout (and the stamp-mismatch deletes at `:144` and `:151`) can **leave `lazuar_auth` in the browser**. Platform logout specifies `Path` but also omits `Domain` (`PlatformAuthEndpoints.cs:68, 87, 94`).

Stamp is not rotated on logout. Other devices keep working until expiry even if delete succeeds.

### 3.4 Cookie vs Bearer

`OnMessageReceived` (`AuthAndCorsExtensions.cs:52–64`) **overwrites** `context.Token` whenever the realm cookie is present. A client that sends `Authorization: Bearer <other-user-jwt>` **and** `lazuar_auth` is authenticated as the cookie, not the header. That is the right confused-deputy default for a cookie SPA. It is a foot-gun for “I put a Bearer token in Scalar and also have an ops session.”

API key middleware then inspects `Authorization`. If it looks like `sk_test_` / `sk_live_` (Bearer or raw), it **replaces** `context.User` entirely (`ApiKeyAuthenticationMiddleware.cs:33–94`). Cookie human + cashier key in the same request is a machine.

### 3.5 Entitlements empty-state

`GET /one/me/entitlements` (`WorkspaceEndpoints.cs:141–165`):

- Normal user: memberships ⋈ organizations, `Role = m.Role`. **No `OrderBy`.** **No `o.IsActive` filter.** Archived orgs still appear.
- System admin: every **active** organization, synthetic role `"SUPER_ADMIN"` (`:146–159`). This does **not** insert a `TenantMembership` row.

Ops `App.tsx`:

- `/me/entitlements === []` → `EmptyWorkspaceState` (“Create your workspace”), not Access Denied (`:127–134`). LP-184 still holds.
- If the entitlements query **errors**, `entitlements` is `undefined`, `isEntitlementsLoading` is false, the empty-state branch does not run (`entitlements?.length === 0` is falsy), and OpsLayout renders the chrome with `entitlements || []` (`:153–157`). The switcher is empty; `X-Tenant-Id` may still be a stale localStorage id. That is a lockout-shaped hole (B07-I22).

Empty-state is **not** “no app entitlements.” `/me/entitlements` lists memberships. A workspace created with `provision_apps: []` still produces a row. `CreateWorkspaceModal` always sends the five core apps (`CreateWorkspaceModal.tsx:32–37`). A raw API caller can send `[]`.

Accept-invite is **outside** `OpsLayout` (`App.tsx:214`). A logged-in user with zero memberships can still open `/accept-invite` and POST the token. Empty-state does not block accept. Empty-state also does not mention pending invites — there is no inbox.

System-admin synthetic list **is** a lockout for support: they pick customer org X in the switcher, ops sends `X-Tenant-Id: X`, `GetTenantRoleAsync` finds no membership, path is `/admin/...` (not exempt) → **403** (`TenantSecurityMiddleware.cs:90–103`). Genesis only auto-memberships them to the **system** org (`SystemGenesisBootstrapperJob.cs:90–100`). They can still hit One routes that check `ctx.IsSystemAdmin` (list all workspaces, toggle apps, GET members). They cannot operate commerce as support without a real membership row.

---

## 4. Quoted walk — invite / accept (the 297ba98 loop)

This is the walk 008 said was missing. It exists now. It is not closed.

### 4.1 Invite command

`InviteUserToWorkspaceCommandHandler` (`InviteUserToWorkspaceCommand.cs:31–76`):

1. Inviter must `CanManageMembers` or `inviter.IsSystemAdmin`. MEMBER cannot invite (test `Member_CannotInvite`).
2. Role allow-listed (`NormalizeInvitedRole`).
3. If the email already has a `GlobalUser` who is already a member → throw `"User is already a member of this workspace."`
4. If the email has a `GlobalUser` who is **not** a member → still create an invitation.
5. If the email has **no** user → still create an invitation.
6. Token: `GenerateSecureToken()` (32 random bytes, URL-safe base64, SHA-256 hex at rest — `TokenGeneratorService.cs:9–28`). 7-day expiry (`InviteUserToWorkspaceCommand.cs:48–49`).
7. `WorkspaceInvitation` ctor lowercases email, uppercases role, stores `tokenHash`, raises `WorkspaceInvitationCreatedDomainEvent` with the **plain** token (`WorkspaceInvitation.cs:23–36`).
8. Audit `member.invited` with `{ email, role }`, not the token (`:64–72`). Test `Invite_RecordsAuditWithoutSecrets` asserts the metadata string does not contain `"secret-token"`.

Pending index (`OneDbContext.cs:88`):

```csharp
builder.HasIndex(x => new { x.OrganizationId, x.Email }).HasFilter("\"Status\" = 'PENDING'");
```

**Not unique.** Two pending invites for the same email can coexist. Two tokens, two emails, first accept wins, second accept tries to insert a second membership → unique `(GlobalUserId, OrganizationId)` (`OneDbContext.cs:73`) → `DbUpdateException` → 500 (B07-I03 / B07-I04).

Double-click on Team’s Invite button is enough.

`GET /one/workspaces/{id}/invites` exists and returns id / email / role / status / expires (`WorkspaceEndpoints.cs:99–107`). **No token, no hash.** If mail never arrives the token is unrecoverable. Team page never calls this GET (B07-I09).

There is no resend command.

### 4.2 Invite email URL — 297ba98 actually flipped this

`NotificationDispatchDomainEventHandlers.Handle(WorkspaceInvitationCreatedDomainEvent)` (`:65–79`):

```csharp
var acceptLink = $"{_linkService.GetOpsBaseUrl()}/accept-invite?token={notification.PlainToken}";
```

`OneLinkService.GetOpsBaseUrl()` (`OneLinkService.cs:20–23`):

```csharp
return _configuration["App:OpsUrl"]?.TrimEnd('/') ?? "http://localhost:3003";
```

`GetClientBaseUrl()` still defaults to `http://localhost:3004` (`:15–18`) and is **still** used for password reset and verify-email (`NotificationDispatchDomainEventHandlers.cs:31, 50`).

Repo config:

- `appsettings.json:41–42` — `ClientUrl` `http://localhost:3004`, `OpsUrl` `http://localhost:3003`
- `appsettings.Development.json:30–31` — same
- `deploy/prod/env.example:23–24` — `App__ClientUrl=https://hub.lazuar.com/portal`, `App__OpsUrl=https://hub.lazuar.com`

`AppOptions.cs` documents ClientUrl as “typically port 3020” with default `"http://localhost:3020"` (`:7–10`) and OpsUrl `"http://localhost:3003"` (`:12–15`). `OneLinkService` does **not** bind `IOptions<AppOptions>`. It reads `IConfiguration` keys. The 3020 default is dead unless someone constructs `AppOptions` without config. Drift, not a runtime mis-wire, unless a future caller switches to `AppOptions.ClientUrl` and silently moves reset links to a port that is not the portal.

Tests that lock the invite URL (`OneLinkServiceTests.cs:20–74`):

- `GetOpsBaseUrl_UsesOpsUrl_AndInviteUrlDoesNotContainClientUrl` — built URL is `http://localhost:3003/accept-invite?token=invite-token`, does not contain `localhost:3004`.
- `InviteEmail_UsesOpsAcceptUrl_NotClientUrl` — handler HTML contains the ops URL and not the client URL.

Those tests are honest about **this** commit. They do not assert that a page exists. They do not assert that the mail is delivered.

### 4.3 Invite mail is still tenant-scoped (chicken-and-egg)

Same handler, line 78–79:

```csharp
await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
    notification.OrganizationId, notification.Email, null, subject, htmlBody, null, "EMAIL"));
```

Password-reset and verify-email use `_systemTenantId = Guid.Empty` (`:19, 44–45, 61–62`). Invite uses the **workspace** id.

`OneEventBus` is `OutboxEventBus<OneDbContext>` (`DependencyInjection.cs:69`). The domain event runs **inside** `SaveChangesAsync` before the SQL commit (`PlatformDbContext.cs:78–103`). The handler writes a `DispatchMessageIntegrationEvent` row into the same context’s outbox. Then the invitation **and** the outbox row commit together. HTTP 200 / Team toast “Invitation sent” means “row exists,” not “Resend accepted the message.”

Later, Messaging’s `DispatchMessageIntegrationEventHandler` (`:55–146`):

- Non-system tenant: look up tenant Resend BYOK (`:117–125`).
- If no active key + sender: `tenantApiKey` stays null.
- `ResendEmailService` for a non-system org **throws** (`ResendEmailService.cs:66–68`):

```csharp
throw new InvalidOperationException(
    "No platform fallback allowed for tenant emails. You must configure a valid BYOK Resend API key and Sender Email to dispatch tenant communications.");
```

A brand-new workspace has not configured Email Provider. The invite outbox row retries and dies. The invitation row stays `PENDING`. The token lives only in that failed payload / logs. Team cannot show it. This is the same chicken-and-egg 008 named. `297ba98` did not change the `OrganizationId` on the dispatch. **The accept page cannot run if the mail never arrives.**

### 4.4 Accept command — email compare, expiry, replay, hash

`AcceptWorkspaceInvitationCommandHandler` (`AcceptWorkspaceInvitationCommand.cs:22–42`):

```csharp
var user = await _repository.GetUserByIdAsync(request.UserId, ct);
if (user == null || !user.IsActive) throw new InvalidOperationException("Invalid user session.");

var inputHash = _tokenGenerator.HashToken(request.Token);
var invitation = await _repository.GetInvitationByHashAsync(inputHash, ct);

if (invitation == null || invitation.Status != "PENDING" || invitation.ExpiresAt < DateTime.UtcNow)
    throw new InvalidOperationException("Invitation is invalid or expired.");

if (user.Email != invitation.Email)
    throw new InvalidOperationException("This invitation belongs to a different email address.");

invitation.Accept();

var membership = new TenantMembership(user.Id, invitation.OrganizationId, invitation.Role);
_repository.AddTenantMembership(membership);

await _repository.SaveChangesAsync(ct);
```

What this does well:

- **Hash compare, not plaintext.** Invite stores SHA-256 hex of the plain token (`TokenGeneratorService.HashToken`). Accept hashes the presented token the same way. Unique index on `TokenHash` (`OneDbContext.cs:87`). `GetInvitationByHashAsync` uses `IgnoreQueryFilters` (`OneRepository.cs:86–90`) so the exempt path can find the row without ambient tenant.
- **Email bind.** Both sides are stored lowercased (`GlobalUser.cs:34`, `WorkspaceInvitation.cs:27`). `!=` is ordinal. Alice logged in cannot accept Bob’s invite. Test `Accept_WrongEmail_Throws` locks the message `*different email*` and asserts status stays `PENDING`.
- **Expiry.** Handler checks `ExpiresAt < UtcNow` **and** `Accept()` checks `UtcNow > ExpiresAt` (`WorkspaceInvitation.cs:40–41`). Test `Accept_ExpiredInvite_Throws` plants `AddHours(-1)` and asserts no `SaveChanges`.
- **Replay of an already-ACCEPTED invite** is **not** a 500. Status is no longer `PENDING`, so the handler throws `"Invitation is invalid or expired."` → `InvalidOperationException` → `GlobalExceptionHandler` maps to **400** (`GlobalExceptionHandler.cs:40–50`). Same for REVOKED. An attacker with the old URL gets 400, not a second membership.

What this does not do:

- **No trim** of `request.Token`. AcceptInvitePage trims the query (`AcceptInvitePage.tsx:71`). A raw POST with trailing whitespace hashes differently → “invalid or expired.”
- **No `HasMembershipAsync` pre-check.** If the user is already a member (provision attach, a previous invite that succeeded, a second pending invite), `AddTenantMembership` + unique `(GlobalUserId, OrganizationId)` throws `DbUpdateException`. That is **not** `InvalidOperationException`. `GlobalExceptionHandler` maps it to **500** and puts `exception.Message` in `Detail` (`:54–62`) — Postgres unique-violation text leaks.
- **No audit row** on accept.
- **No distinction** between unknown token, expired, revoked, already accepted. Good for enumeration. Bad for the SPA, which then special-cases HTTP 500 (see §4.5).

Email compare is **not** a case-sensitivity bug in the normal path. It *is* a Unicode-dotless-i / JS `toLowerCase()` vs .NET `ToLowerInvariant()` foot-gun if someone invites `İ` addresses from the Team form (`TeamPage.tsx:33` uses JS `toLowerCase()`). P2.

Token hash mismatch between mint and accept is **not** present for the happy path. `GenerateSecureToken` hashes the **plain** token (no prefix). Accept hashes the query token. The email link uses `notification.PlainToken`. They match. API keys are the ones that hash `sk_test_` + secret; invites do not.

### 4.5 Accept API + AcceptInvitePage

Endpoint (`WorkspaceEndpoints.cs:121–125`):

```csharp
group.MapPost("/workspaces/invites/accept", async Task<Ok<StatusResponse>> (AcceptWorkspaceInvitationDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
{
    await mediator.Send(new AcceptWorkspaceInvitationCommand(ctx.UserId, req.Token));
    return TypedResults.Ok(new StatusResponse { Status = "accepted" });
}).RequireAuthorization();
```

Unauthenticated → 401. Domain throws → 400 via global handler. Unique violation → 500.

Ops route exists (`App.tsx:214`):

```tsx
<Route path="/accept-invite" element={<AcceptInvitePage />} />
```

It is a **public** route, not inside `OpsLayout`. Catch-all `*` → `/commerce/dashboard` (`:249`) no longer swallows `/accept-invite`. That is the 008 hole, closed.

`AcceptInvitePage.tsx` walk:

1. Read `token` from the query, trim. Missing → “Invalid invite.”
2. Module-level `acceptByToken` `Map<string, Promise<AcceptOutcome>>` (`:17`) so React Strict Mode / remount shares one POST.
3. `GET /one/auth/me`. 401 → navigate to `/login?returnUrl=/accept-invite?token=…` (`:19–20, 82–85`) and **delete** the cache entry.
4. Snapshot `/one/me/entitlements` (`previousIds`).
5. `POST /one/workspaces/invites/accept` `{ token }`.
6. 401 → treat as unauth (delete cache, login redirect).
7. **`status >= 500` → `{ message: "This invite may already have been accepted. Try signing in.", wrongEmail: false }`** (`:40–46`). **Any** 500 is narrated as double-accept. A real unique-violation 500, a down database, or a bug in `SaveChanges` all get the same sentence. The cache **keeps** that error. Clicking the non-wrongEmail “Sign in” link (`:175–177`) does **not** `acceptByToken.delete`. Coming back with the same token replays the cached lie without retrying (B07-I08).
8. 4xx with `detail` matching `/different email/i` → Sign out button, which **does** delete the cache (`:119–123`).
9. Success: find the entitlements id that was not in `previousIds`; if none and exactly one entitlement, use that; else `null`. Write `ops_active_workspace_id`. Redirect `/commerce/dashboard` after 800 ms.

Login page now knows invite return URLs (`LoginPage.tsx:37, 149, 208–210`): copy becomes “Sign in with the invited email.” Signup mode **still** shows workspace name + slug + “Create workspace” (`:214–307`). `inviteReturn` only changes the subtitle. Submit still calls `/one/public/register`.

`isSafeReturnUrl` (`LoginPage.tsx:12–14`) requires `startsWith("/")` and not `startsWith("//")`. That blocks `//evil.com`. It does not block `/\\evil` or `/%2F%2F…` open-redirect classics. P2.

Portal leftover: `apps/lazuar-portal/src/app/accept-invite/page.tsx` 302s to `NEXT_PUBLIC_OPS_URL` (default `http://localhost:3003`) preserving `token`. Pre-`297ba98` mails that used `App:ClientUrl` still land on ops. New mails never need this page.

### 4.6 Does register-always-creates-workspace break accept?

No, if the emails match.

1. Admin invites `book@example.com`.
2. If BYOK is configured, book gets `https://hub…/accept-invite?token=…` (or `:3003`).
3. Page → 401 → `/login?returnUrl=/accept-invite?token=…`.
4. Book has no account. Clicks Sign up. **Must** invent a workspace name/slug. Register creates workspace W2, membership ADMIN, cookie `CLIENT`.
5. `window.location.href = returnUrl`. Accept page runs with a session. `user.Email == invitation.Email`. Accept inserts membership on W1. Book now has **two** workspaces.
6. Page diffs entitlements, sets `ops_active_workspace_id` to the new id (W1), redirects to dashboard.
7. OpsLayout validates localStorage against the entitlements list (`App.tsx:91–102`). The invited id is in the list → they stay on W1.

If they sign up with a **different** email, accept throws different-email. They now own a stray workspace. Sign out (cache cleared) and the invited address still has no account unless they register again.

If they already have an account, Sign in + accept works without a second workspace.

So: leftover, resource leak (extra tenant + starter credits), confusing copy, **not** an accept-breaker. Task instruction honored: only a bug if it breaks accept. Filed as P2 leftover (B07-I14), not P0.

### 4.7 Working vs broken “add a human” loops **after** `297ba98`

| Loop | Works? |
|------|--------|
| You register, you are ADMIN of workspace #1 | Yes |
| You create workspace #2 from the switcher | Yes |
| Integrator provision with `owner_email` on an existing GlobalUser | Yes (attach) |
| Team invite → email delivered → click → login/signup as **same** email → accept API → Viewer/Member/Admin in the inviter’s workspace | **Yes, if tenant Resend BYOK is configured** |
| Team invite → new tenant, no Email Provider → toast “Invitation sent” | **No mail, no token, pending invisible** |
| Team invite → already-logged-in wrong email | Page offers Sign out (new) |
| Team invite → double-click Invite → two PENDING rows → first accept 200, second 500 | Yes, 500 |
| Share the owner password | Works, destroys auditability |

---

## 5. Quoted walk — password reset and verify-email (still 404)

Reset and verify were **not** in `297ba98`. 008 said the pages were missing. They are still missing.

### 5.1 Forgot / reset

`ForgotPasswordCommand` (`:22–31`): silent if missing/inactive; 24h token; `GeneratePasswordResetToken` raises `PasswordResetRequestedDomainEvent` with the plain token.

Email (`NotificationDispatchDomainEventHandlers.cs:31`):

```csharp
var resetLink = $"{_linkService.GetClientBaseUrl()}/reset-password?email={…}&token={notification.PlainToken}";
```

`GetClientBaseUrl()` → `App:ClientUrl` → local `:3004`, prod `https://hub.lazuar.com/portal`.

`apps/lazuar-portal/src/app/` has **no** `reset-password` segment. `not-found.tsx` renders a buyer 404. Ops has **no** `/reset-password` route; if anyone pointed reset at OpsUrl, `App.tsx:249` catch-all would send them to `/commerce/dashboard` and **drop the token** (same class of bug 008 filed for accept, still live for reset).

Ops `LoginPage` has **zero** “Forgot password?” link.

`ResetPasswordCommand` (`:25–38`): lookup by email; inactive/missing → `"Invalid request."`; missing/expired/wrong hash → `"Token is invalid or expired."`; success rotates stamp and clears the hash.

Both failures are 400s with **different** messages. Forgot is silent. Reset is an email oracle (B07-I17).

Endpoint is typed `Ok<StatusResponse>` (`AuthEndpoints.cs:115–118`) but exceptions still hit the global handler. Invalid token is not a silent 200.

### 5.2 Verify-email (still doubly broken)

Register never issues a token. `UserRegisteredDomainEvent` has no handler. Login does not require verify.

`POST /auth/resend-verification` can mint a token (`ResendVerificationEmailCommand.cs:26–36`). Email (`NotificationDispatchDomainEventHandlers.cs:50`):

```csharp
var verifyLink = $"{_linkService.GetClientBaseUrl()}/verify-email?email={…}&token={…}";
```

No `/verify-email` page on portal or ops.

API (`AuthEndpoints.cs:121–128`) is `RequireAuthorization()`. It loads `ctx.UserId`, then sends `VerifyEmailCommand(user.Email, req.Token)`. The email query string in the mail is **unused**. `VerifyEmailRequestDto` is `{ token }` only (`auth.tsp:39–40`). You cannot click the mail from a logged-out inbox and succeed. You must already have a session, and the session’s email must own the hash.

`VerifyEmailCommand` (`:22–36`): already-verified is a silent return; otherwise hash + expiry.

Pre-wave gap note §Email verification is still accurate. 008 §7.5 is still accurate.

---

## 6. Quoted walk — API keys

### 6.1 Mint

`GenerateApiCredentialCommandHandler` (`:46–91`):

- 40-byte CSPRNG, prefix `sk_test_` or `sk_live_` from `IsTestMode`.
- Hash of the **full** string (prefix + secret). Persist hash + last-4 hint. No plaintext column. No expiry. No last-used.
- `PlatformApiScopes.NormalizeAndValidate` — null / empty / unknown rejected.
- Audit `api_key.created` with `{ name, prefix, hint }`.

HTTP `POST /one/api-keys` is `OrgAdmin` + `ctx.TenantId` (`ApiCredentialEndpoints.cs:20–62`). Middleware **requires** tenant on this prefix (`TenantSecurityMiddleware.cs:160–164`) and **403s** a non-member. JWT `SUPER_ADMIN` without a membership on the header tenant cannot mint customer keys. `API_CLIENT` cannot mint (OrgAdmin human-only).

Provision mint (`ProvisionAuraWorkspaceCommandHandler.Keys.cs:10–36`) uses `DefaultAuraIntegratorScopes` (payments write+read + webhook manage) in the same `SaveChanges` as the org. That is the integrator hatch, not the human console.

### 6.2 AuthN

`TryGetApiKey` accepts `Authorization: Bearer sk_live_|sk_test_…` or raw `Authorization: sk_…` (`ApiKeyAuthenticationMiddleware.cs:138–165`). Prefix check is `OrdinalIgnoreCase`. **Hash is case-sensitive SHA-256 of the presented string.** Keys are minted with lowercase `sk_test_` / `sk_live_`. A client that sends `SK_LIVE_…` passes `TryGetApiKey` and fails lookup → 401 (B07-I18). `IsTestMode` would have been true for `SK_TEST_` if the hash had hit.

Lookup SQL is One-only (`:17–22, 114–133`). Lhdn-only leftover keys 401. Cache 5 minutes by hash (`:51`). Revoke publishes `ApiKeyRevokedIntegrationEvent` to the One outbox **before** `SaveChanges` in the command (`RevokeApiCredentialCommand.cs:48–49`); host handler evicts `ApiKey_{hash}` (`ApiKeyRevokedIntegrationEventHandler.cs:24–32`). Happy path closes the 5-minute window after the outbox is consumed. A revoke that never publishes, or an inbox that never runs, leaves a **5-minute ghost** plus whatever is still in `TenantKeys_{org}` (10-minute list, never consulted for authN — dead weight).

`IsTestMode` is not a column. Payments cashier uses the request flag to refuse test key vs live gateway. There is no sandbox database. `sk_test_` is a bit on the request. Commerce products and the ledger are the same tenant data. Do not sell “Stripe test mode.”

### 6.3 Scope holes that remain

Closed catalog + policy tests are the strongest part of this slice. Remaining holes:

1. **Human ADMIN / SUPER_ADMIN bypass** on every Integration* policy except `IntegrationPaymentsMe` (`AuthAndCorsExtensions.cs:96–182`). An ops cookie with injected `ADMIN` can hit M2M checkout-create. 008 called this intentional. It is still a scope hole if the sales story is “keys are least-privilege.”
2. **Membership `SUPER_ADMIN` is `IsInRole("SUPER_ADMIN")`.** Integration policies and `IntegratorProvisionAuth` cannot tell it from JWT platform admin (B07-I20).
3. **Comment lies** invite the next agent to restore implicit LHDN defaults: `IApiCredentialService.cs:32–34` (“Null/omitted uses LHDN document defaults”); `AdminApiKeyEndpoints.cs:51` (“Null/omitted scopes → LHDN document defaults”); `Lhdn/Domain/ApiKeyScopes.cs:14–17` (“Default scopes granted to newly minted keys”). The **command** rejects omit. The LHDN façade passes `scopes` through to the same `GenerateAsync`. Omit 400s. Tests lock the command. The comments are how implicit defaults come back.
4. **No IP allowlist, no expiry, no rotation reminder, no last-used.**
5. **Storage presign** is any authenticated member of the header tenant (`StorageEndpoints.cs:27–48`), not OrgAdmin. Any MEMBER/VIEWER who can call it can upload under `vault/{tenantId}/…`.

`IntegrationPaymentsMe` correctly **excludes** humans (`AuthAndCorsExtensions.cs:153–161`). Tests lock that (`ApiKeyAuthenticationTests.cs:463–470`). Not a bug.

---

## 7. Quoted walk — audit

`AuditEvent` (`AuditEvent.cs`): UUIDv7, org, optional actor, action ≤100, entity type ≤64, entity id ≤64, jsonb metadata, timestamp. No hash chain, no WORM, no IP, no before/after.

`IAuditRecorder` is fire-and-forget. `AuditRecorder` swallows all exceptions (`AuditRecorder.cs:77–83`). A broken `one.AuditEvents` write cannot fail a refund.

Who writes today (One-owned plus the three commerce money actions 008 already listed):

| Action | Writer |
|--------|--------|
| `member.invited` | `InviteUserToWorkspaceCommandHandler` |
| `member.removed` | `RemoveWorkspaceMemberCommandHandler` |
| `api_key.created` | `GenerateApiCredentialCommandHandler` |
| `api_key.revoked` | `RevokeApiCredentialCommandHandler` (actor email omitted in the call; ambient `UserId` should fill it) |

Who still does **not** write in One:

`user.registered`, `workspace.created` / `updated` / `archived`, `member.accepted` / `invitation.revoked`, login / logout / failed login, password change / reset, email verify, entitlement toggle, webhook create/rotate, provision, storage upload.

W3-LP-167 analysis asked for payment-config upsert, plan/qty, dunning pause. Those are not One; they are still missing and belong in 01/02/04 as well. This slice owns the identity gaps.

GET `/one/workspaces/{id}/audit` (`WorkspaceEndpoints.cs:167–202`): `RequireAuthorization` only (not `OrgRead`); **Forbid** if no membership and not system admin; page clamp 1–100 default 50; `IgnoreQueryFilters` + `OrganizationId == id`. VIEWER can read key mint/revoke metadata (hints, not secrets).

Ops `AuditLogPage.tsx`: 403 → empty list (`:29`), not an error. Does not render `metadata_json`. No export. Description: “Who changed money or identity in this workspace. Reads are not logged.”

Optional `IAuditRecorder? = null` on handlers. Tests that omit it silently skip audit. Production DI registers the scoped recorder (`One/Infrastructure/DependencyInjection.cs:60`).

---

## 8. Workspace isolation, slug collision, archive

### 8.1 The `/one/workspaces` exemption is still the IDOR hinge

`IsTenantExemptPath` includes the **entire** `/api/v1/one/workspaces` prefix (`TenantSecurityMiddleware.cs:137`). Architecture tests lock that (`TenantIsolationArchitectureTests.cs:104`). Middleware will not 400 for missing `X-Tenant-Id` and will not 403 a non-member just for omitting it.

Authorization is whatever the endpoint and command do:

- GET members / invites / workspace: `HasTenantAccessAsync` or system admin. Fail-closed. GET members returns **401** (not 403) on miss (`WorkspaceEndpoints.cs:86–87`). Audit returns **403**. Same check, two status codes.
- Invite / revoke / remove: `OrgAdmin` (needs injected ADMIN or JWT SUPER_ADMIN) **plus** `CanManageMembers` / `IsSystemAdmin` in the command. A crafted `X-Tenant-Id: A` (ADMIN of A) + `POST /workspaces/{B}/invites` fails the command unless they also manage B. Not a steal.
- Accept: token hash + email. No tenant header required. Correct.
- Webhooks: `CanAccessWorkspaceWebhooksAsync` compares API-key tenant to **path** id (`WebhookEndpoints.cs:283–288`).

`HasTenantAccessAsync` (`OneQueryService.cs:72–78`) is membership-only. It ignores `Organization.IsActive` and ignores role. Historical membership on an archived org can still list members, invites, and audit.

### 8.2 Slug collision

Create and register call `IsSlugUniqueAsync` then insert. Unique index on `Organizations.Slug` (`OneDbContext.cs:49`). Two concurrent registers of `acme-corp` can both pass the read and die on the unique index → 500 + leaked Postgres detail (B07-I16).

`UpdateWorkspaceCommand` re-checks slug **format** via `Organization.UpdateDetails` (`Organization.cs:47–54`) and does **not** call `IsSlugUniqueAsync`. Two live workspaces can collide on update until the unique index throws.

`IsSlugUniqueAsync` (`OneRepository.cs:39–42`) does not ignore archived rows. Good: slugs are not recycled. Combined with archive-is-a-boolean, a dead slug is reserved forever.

Reserved set (`OrganizationSlugMustBeValidRule.cs:10–15`) includes `login`, `admin`, `portal`, `system`, `billplz`, `stripe`, `lazuar`, `one`, `auth`. Genesis raw-SQL upserts slug `system` (`SystemGenesisBootstrapperJob.cs:49–53`), bypassing the rule. Ops `workspace-slug.ts` reserved set matches the rule.

### 8.3 Archive is a boolean

`ArchiveWorkspaceCommand` (`:23–38`): exact `ADMIN`, sets `IsActive = false`, raises `OrganizationArchivedDomainEvent`. **No handler.** No integration event. No key revoke. No membership wipe. `HasTenantAccess` still true. Client still sending that id as `X-Tenant-Id` still injects the role. API keys for that org still authenticate (`ApiKeyAuthenticationMiddleware` does not join `Organizations.IsActive`). Archive is not a lifecycle.

### 8.4 Last admin / self-remove

`RemoveWorkspaceMemberCommand` (`:33–56`) does not count remaining ADMINs and does not forbid `TargetUserId == RequesterUserId`. Team UI offers Remove on every row including yourself (`TeamPage.tsx:114–122`). A workspace can be orphaned. The orphaned owner then hits EmptyWorkspaceState and can create a **new** workspace; they cannot recover the old one through the product.

### 8.5 Header tenant ≠ path id

Ops never does this. A crafted request can authorize on A and read B only if the handler uses path id without a membership check. Current One handlers that read `{id}` check membership on `{id}`. API keys use `ctx.TenantId` (header), no path id. **Do not add** a new One route that authorizes on the header and reads the path without comparing them. 008 H1 still stands as a writing rule, not an open steal.

---

## 9. 008 re-verify (closed only if this tree no longer contains it)

008 file: `plans/008-evals/05-identity-roles-keys-audit.md`. Re-checked against `297ba98`.

| 008 claim | Status in this tree | Evidence |
|-----------|---------------------|----------|
| Accept page missing; ops `*` swallows `/accept-invite`; token lost on login redirect | **FIXED** | `App.tsx:214`; `AcceptInvitePage.tsx` exists; login `returnUrl` preserved (`LoginPage.tsx:33–37, 72`) |
| Invite mail uses `App:ClientUrl` (`:3004` / portal) | **FIXED** | `NotificationDispatchDomainEventHandlers.cs:67`; `OneLinkService.GetOpsBaseUrl`; `OneLinkServiceTests` |
| Portal leftover `:3004` links 404 | **FIXED** (compat) | `apps/lazuar-portal/src/app/accept-invite/page.tsx` 302 to ops |
| Accept **API** already existed | Still true | `WorkspaceEndpoints.cs:121–125` |
| Register always creates a personal workspace | **Still true; does not break accept** | `RegisterPublicUserCommand.cs:54–62`; `LoginPage.tsx:113–126` still requires workspace fields even when `inviteReturn` |
| Invite mail requires tenant Resend BYOK | **STILL OPEN** (now the remaining P0 of the loop) | dispatch `OrganizationId` = workspace; `ResendEmailService.cs:66–68` |
| Pending invites invisible on Team | **STILL OPEN** | `TeamPage.tsx` never GETs `/invites` |
| No last-admin / remove-self guard | **STILL OPEN** | `RemoveWorkspaceMemberCommand.cs:33–43` |
| Double-accept / already-member → unique index 500 | **STILL OPEN** | `AcceptWorkspaceInvitationCommand.cs:36–41`; no pre-check; `OneDbContext.cs:73` |
| Pending invite index not unique | **STILL OPEN** | `OneDbContext.cs:88` |
| Accept has no audit | **STILL OPEN** | handler has no `IAuditRecorder` |
| Reset / verify pages missing | **STILL OPEN** | no routes; emails still `GetClientBaseUrl()` |
| Verify API is session-bound; register never issues token | **STILL OPEN** | `AuthEndpoints.cs:121–128`; register handler |
| Login unlimited; 400-with-401 | **STILL OPEN** | `AuthEndpoints.cs:75–101` |
| Stamp only on `/auth/me` | **STILL OPEN** | `AuthEndpoints.cs:148–153` |
| Superadmin synthetic entitlements vs middleware 403 | **STILL OPEN** | `WorkspaceEndpoints.cs:146–159` vs `TenantSecurityMiddleware.cs:90–103` |
| Archive no cascade | **STILL OPEN** | `ArchiveWorkspaceCommand.cs`; no event handler |
| Dual JWT `CLIENT` + injected membership | **STILL OPEN** | `IssueCookie` + middleware `:83–88` |
| Register body `ADMIN` / cookie `CLIENT` | **STILL OPEN** | `AuthEndpoints.cs:71` vs `:197` |
| `IApiCredentialService` XML lies about LHDN default | **STILL OPEN** | `IApiCredentialService.cs:32–34` |
| `OrgAdmin` no longer includes `API_CLIENT` | Still fixed | `AuthAndCorsExtensions.cs:76–80`; tests |
| Invite role allow-list; `CLIENT` rejected | Still fixed | `WorkspaceStaffRoles.cs`; invite tests |
| Members/invites IDOR (any auth user, guess GUID) | Still fixed on handlers | `HasTenantAccessAsync` |
| Webhook GET full secret | Still fixed (out of this slice’s product claim; verified in 008, not re-broken here) | — |
| Keys moved to One; omit scopes rejected | Still fixed | `GenerateApiCredentialCommand.cs:57`; tests |
| Public pricing + TOS checkbox | Still present | not re-audited as a bug beyond clickwrap honesty |
| `accepted_terms` not stored; buyer TOS as merchant contract | **STILL OPEN** | `LoginPage.tsx:289–298`; no column |
| CORS allow-any if `App:CorsOrigins` empty | **STILL OPEN** | `AuthAndCorsExtensions.cs:208–212` |
| `X-Forwarded-For` first hop | **STILL OPEN** | `AuthEndpoints.cs:172–178` |
| Logout cookie Domain | **Not named in 008; OPEN** | `AuthEndpoints.cs:105` vs `:211` |
| `IntegratorProvisionAuth` `IsInRole("SUPER_ADMIN")` | **Not named in 008; OPEN** | `IntegratorProvisionAuth.cs:73–76` |

008 one-line judgment was: “Sell the self-serve workspace and the scoped key. Do not sell Team. The accept page is missing.”  
This tree: **the accept page is present.** Team is still not sellable without BYOK mail, pending-invite UX, and a non-500 already-member path. Sell the self-serve workspace and the scoped key remains the honest packaging.

---

## 10. Bug catalog

Ids are `B07-Ixx`. Severity: **P0** = broken core loop or privilege/data steal in the product as shipped; **P1** = real break, session/auth hole, or orphaning; **P2** = leftover, honesty, DX, misconfig foot-gun.

### B07-I01 — P0 — Invite mail still requires tenant Resend BYOK; token is unrecoverable

**Where.** `NotificationDispatchDomainEventHandlers.cs:78–79` publishes `DispatchMessageIntegrationEvent` with `notification.OrganizationId`. `ResendEmailService.cs:47–68` refuses platform fallback for non-system tenants.

**What.** `297ba98` fixed the URL host. It did not fix delivery. A new Hub tenant inviting a bookkeeper has no Email Provider. The invite row commits. The outbox retry throws `"No platform fallback allowed for tenant emails…"`. Team toasts “Invitation sent” (`TeamPage.tsx:38`). GET invites (unused by the page) would show PENDING without a token. There is no resend. The only secret is gone.

**Why P0.** After the accept page shipped, this is the remaining break in the staff-onboarding loop 008 called the largest product hole. The page cannot run without the mail. Password reset uses `Guid.Empty` and *can* use platform Resend; invite deliberately does not.

**Not a test gap only.** No test asserts delivery, BYOK, or system-tenant dispatch for invites. `OneLinkServiceTests` only asserts the URL string inside the HTML payload of a **substituted** `IEventBus`.

### B07-I02 — P1 — Password-reset and verify-email links still 404

**Where.** `NotificationDispatchDomainEventHandlers.cs:31, 50` use `GetClientBaseUrl()` → `App:ClientUrl`. Portal has no `/reset-password` or `/verify-email` (`apps/lazuar-portal/src/app/` listing). Ops has neither route; `*` would drop the token. `LoginPage.tsx` has no forgot-password link.

**What.** Forgot-password API is real. Reset API is real. Clicking the mail is a buyer 404 (`not-found.tsx`). Token sits in the 404 URL (history, Referer). Verify is worse: even a future page must be logged in as that user (`AuthEndpoints.cs:121–128`), and register never minted a token.

**008.** Open. `297ba98` commit message: “ClientUrl stays portal.” That is correct for checkout. It is wrong for merchant recovery if portal has no pages.

### B07-I03 — P1 — Double-accept / already-member / second pending token → 500

**Where.** `AcceptWorkspaceInvitationCommand.cs:36–41` always inserts `TenantMembership`. Unique index `TenantMemberships (GlobalUserId, OrganizationId)` (`OneDbContext.cs:73`). `GlobalExceptionHandler.cs:52–62` maps non-`InvalidOperationException` to 500 and **echoes `exception.Message`**.

**Triggers.**

1. Two concurrent POSTs of the same still-PENDING token (the SPA Map prevents this in one tab; two browsers do not).
2. Two PENDING invites for the same email (non-unique index, B07-I04); first accept 200, second token 500.
3. User already a member (provision `EnsureOwnerAsync`, or they accepted the other invite) and a PENDING invite remains.

Replay of a single ACCEPTED row is **400**, not 500. The SPA’s “status >= 500 means already accepted” (`AcceptInvitePage.tsx:40–46`) is a bandage that also fires on real outages (B07-I08).

**Tests.** `AcceptWorkspaceInvitationCommandHandlerTests` covers happy, expired, wrong email. **No** already-member, **no** second pending, **no** concurrent, **no** replay-after-accept.

### B07-I04 — P1 — Pending invite index is not unique

**Where.** `OneDbContext.cs:88`.

**What.** Team double-submit, or invite → fail to notice → invite again. Two tokens in two emails. First accept works. Second is B07-I03. Also two in-flight “you’re invited” mails with different roles: last writer of membership wins only if they were not already a member; otherwise 500. Role is write-once; there is no “upgrade this invite.”

### B07-I05 — P1 — Accept does not pre-check membership and writes no audit

**Where.** `AcceptWorkspaceInvitationCommand.cs` has no `HasMembershipAsync` and no `IAuditRecorder`. Invite and remove do.

**What.** The unique index is the only guard (500). LP-167’s identity story cannot answer “when did this email join.” Viewer reading `/audit` never sees `member.accepted`.

### B07-I06 — P1 — Production logout / stamp-mismatch may not delete `lazuar_auth`

**Where.** Set: `AuthEndpoints.cs:206–215` (`Domain = ".lazuar.com"` outside dev). Delete: `:105, :144, :151` — `Cookies.Delete("lazuar_auth")` with default options. Platform: set `Domain + Path` (`PlatformAuthEndpoints.cs:135–145`); delete Path only (`:68, 87, 94`).

**What.** Cookie delete must match Domain/Path/Secure/SameSite. In Production, Sign out can return `logged_out` while the browser keeps sending the JWT. Stamp mismatch on `/auth/me` tries the same broken delete and 401s **that** request; the next navigation still has the cookie, `/auth/me` 401s again, user appears logged out in ops **only because `/auth/me` checks the stamp**. Every other API still accepts the cookie until expiry (B07-I07). Combined, “I changed my password / I logged out” is not a session kill in prod.

### B07-I07 — P1 — Security stamp is only enforced on `/auth/me` and platform `/auth/me`

**Where.** `AuthEndpoints.cs:148–153`; `PlatformAuthEndpoints.cs:91–96`. No stamp filter in JWT `TokenValidationParameters` (`AuthAndCorsExtensions.cs:40–49`).

**What.** Stolen cookie works on invite, keys, refunds, everything except the SPA’s session probe, until `ExpiryHours` (24). Password change rotates the stamp (`GlobalUser.cs:55–58`) and does not emit a session-revocation list.

Unchanged from 008 H4 / pre-wave.

### B07-I08 — P2 — AcceptInvitePage maps every 500 to “already accepted” and caches errors

**Where.** `AcceptInvitePage.tsx:17, 40–46, 64, 175–177`.

**What.** Honest for the unique-index 500. Dishonest for everything else. Module-level `Map` is the right Strict-Mode fix for **in-flight** accepts. Leaving a rejected Promise in the Map means a later visit with the same token in the same JS heap does not retry. Wrong-email Sign out deletes (`:120`). The generic “Sign in” link does not.

Out of scope for 09’s *pixels*; this is control-flow that lies about One’s API.

### B07-I09 — P2 — Team page never lists or revokes pending invites

**Where.** `TeamPage.tsx:17–43` invalidates `workspace-members` only. GET `/one/workspaces/{id}/invites` and DELETE `.../invites/{inviteId}` exist (`WorkspaceEndpoints.cs:99–113`).

**What.** Admin cannot see that an invite is PENDING, expired, or doubled. They cannot revoke from the UI. LP-166’s “Team page is the only staff UX” is still a roster widget plus a form that 403s for VIEWER (who still see the form).

### B07-I10 — P1 — Last admin can be removed; self-remove is offered

**Where.** `RemoveWorkspaceMemberCommand.cs:33–43`; `TeamPage.tsx:114–122`.

**What.** Orphaned workspace. Keys keep working. No owner transfer. EmptyWorkspaceState offers create-new, not recover.

### B07-I11 — P1 — Archive does not revoke keys, drop memberships, or unpublish

**Where.** `ArchiveWorkspaceCommand.cs:23–38`; `Organization.Archive` (`Organization.cs:140–146`); grep of `OrganizationArchivedDomainEvent` is the record + `Archive()` only.

**What.** `IsActive = false`. `HasTenantAccess` still true. `sk_live_` still authenticates. `/me/entitlements` for mortals still lists the org. Public branding GET filters `IsActive` (`OneQueryService.cs:48`); the console does not.

### B07-I12 — P1 — Superadmin synthetic entitlements vs real 403

**Where.** `WorkspaceEndpoints.cs:145–159` vs `TenantSecurityMiddleware.cs:90–103` vs genesis membership only on system org (`SystemGenesisBootstrapperJob.cs:90–100`).

**What.** Support switcher shows every live tenant. Every `/admin/*` call 403s. Looks like Access Denied after LP-184 taught ops to treat empty entitlements as “create,” not “denied.” System admins are the one population that **cannot** use EmptyWorkspaceState to recover (their list is never empty if any org is active).

### B07-I13 — P1 — Login is unauthenticated and unlimited

**Where.** `AuthEndpoints.cs:75–101`; `PublicRegisterRateLimiter` is register-only.

**What.** Online brute force. 400-with-401 (`:88–90`) is a client-contract lie on top. Forgot/resend unlimited; reset is an oracle (B07-I17). Register limiter key can be spoofed (B07-I24). Empty limiter key **allows** (`PublicRegisterRateLimiter.cs:21–24`).

### B07-I14 — P2 — Register always creates a workspace (invite leftover)

**Where.** `RegisterPublicUserCommand.cs:54–62`; `LoginPage.tsx:208–307` (`inviteReturn` only changes the subtitle).

**What.** New invitee who uses Sign up becomes ADMIN of a stray tenant and MEMBER/VIEWER/ADMIN of the invited one. Accept still works (email match). Extra starter-credit / entitlement events fire for W2. Not an accept-breaker. Do not “fix” this by blocking register-from-invite without a join-without-workspace API.

### B07-I15 — P2 — Dual role model + register body `ADMIN` vs cookie `CLIENT`

**Where.** `AuthEndpoints.cs:71, 93, 197`; `TenantSecurityMiddleware.cs:83–88`; `TenantMembership.cs:10` comment; `Modules/One/README.md:22, 33–34`.

**What.** Teachability hole. Scalar without `X-Tenant-Id` fails `OrgAdmin`. README still says membership roles `ADMIN` / `CLIENT` and that a paid subscription “may grant a `CLIENT` membership.” No such handler exists. Next agent who “aligns invite with the README” will try to re-introduce `CLIENT` as staff; invite tests currently reject that string — **keep those tests**.

### B07-I16 — P2 — Slug uniqueness is check-then-act; update skips the check

**Where.** `RegisterPublicUserCommand.cs:45–49`; `CreateWorkspaceCommand.cs:42–47`; `UpdateWorkspaceCommand.cs:43` (no `IsSlugUniqueAsync`); `OneDbContext.cs:49`.

**What.** Concurrent create → 500 + leaked unique-violation (B07-I19). Update collision → same. Not an IDOR.

### B07-I17 — P2 — Reset-password is an email oracle

**Where.** `ResetPasswordCommand.cs:25–33`. Missing user: `"Invalid request."` Bad token on a real user: `"Token is invalid or expired."`

**What.** Forgot is silent. Reset is not. Pair with B07-I02 (the link 404s) and you have an API that enumerates emails and a product that cannot complete the flow.

### B07-I18 — P2 — API key prefix parse is case-insensitive; hash is not

**Where.** `ApiKeyAuthenticationMiddleware.cs:35, 158–162` vs `TokenGeneratorService.cs:23–27`.

**What.** `SK_TEST_…` is recognized as a key and as test mode, then 401s. Confusing, not a bypass.

### B07-I19 — P1 — `GlobalExceptionHandler` puts `exception.Message` on 500s

**Where.** `GlobalExceptionHandler.cs:54–62`.

**What.** Unique-index failures (accept, slug, email) leak provider text. Combined with B07-I03 this is how a bookkeeper sees a Postgres constraint in the accept page **if** the SPA did not overwrite it — and how a raw client always sees it.

`InvalidOperationException` → 400 with the domain string is intentional and is how accept “wrong email” reaches the SPA (`:40–50`).

### B07-I20 — P1 — `IntegratorProvisionAuth` treats injected membership `SUPER_ADMIN` as platform admin

**Where.** `IntegratorProvisionAuth.cs:73–76`:

```csharp
var isSystemAdmin =
    string.Equals(principal.FindFirst("is_system_admin")?.Value, "true", StringComparison.OrdinalIgnoreCase)
    || principal.IsInRole("SUPER_ADMIN");
```

Provision path is tenant-exempt (`TenantSecurityMiddleware.cs:138`). Middleware still **injects** membership role when `X-Tenant-Id` is present (`:85–88`). Ops always sends the header. `NormalizeOwnerRole` allows `SUPER_ADMIN` (`ProvisionAuraWorkspaceCommandHandler.Normalizers.cs:51–66`).

**What.** An integrator who attached `owner_role=SUPER_ADMIN` (documented as “workspace membership, not global system admin”) hands that human a claim that opens `POST /one/integrations/workspaces/provision` without the provision secret. They can mint new workspaces, bootstrap `sk_*` keys, and attach owners. JWT is still `CLIENT`; `is_system_admin` is false. The `|| IsInRole` is the bug.

Platform `/api/v1/platform` also `RequireRole("SUPER_ADMIN")` (`ModuleRegistrationExtensions.cs:82`) but `OnMessageReceived` only reads `lazuar_admin_auth` on that path, so an ops cookie does **not** walk into the platform group unless they also send Bearer JWT. A Bearer of the ops JWT with only `CLIENT` fails; after injection… injection requires the tenant middleware to have run with a header, which it does for `/api/v1/platform`? **No** — platform short-circuits before membership lookup (`TenantSecurityMiddleware.cs:29–33`). Platform group is safe from this injection. Provision is not.

Invite cannot mint `SUPER_ADMIN`. Default provision role is `ADMIN`. The hole is real but needs the hatch to have chosen `SUPER_ADMIN`.

### B07-I21 — P2 — Human ADMIN bypass of Integration* policies (except PaymentsMe)

**Where.** `AuthAndCorsExtensions.cs:96–182`.

**What.** Intentional per W1-LP-137. Still a scope hole relative to “machine keys are the only M2M.” Not a steal of another tenant.

### B07-I22 — P2 — Entitlements query error skips empty-state and renders a hollow shell

**Where.** `App.tsx:81–89, 123–157`.

**What.** `useQuery` error → `data` undefined → not `length === 0` → chrome with `[]` entitlements and whatever `ops_active_workspace_id` still says. Not the LP-184 empty-state. Not Access Denied. A failed One query looks like a logged-in product with no workspace switcher.

### B07-I23 — P2 — `accepted_terms` is a request-time gate; TOS is the buyer document

**Where.** `AuthEndpoints.cs:47–48`; `LoginPage.tsx:9–10, 289–298` links `/portal/legal/terms` and `/privacy`.

**What.** 008 §2.3 still holds. No merchant MSA, no stored version, 99.9% sentence still on the buyer terms. Legal, not a crash.

### B07-I24 — P2 — Register rate-limit key trusts first `X-Forwarded-For` hop; empty key allows

**Where.** `AuthEndpoints.cs:169–183`; `PublicRegisterRateLimiter.cs:21–24`.

**What.** Spoof a new IP → new bucket. Empty key → allow. In-process `ConcurrentDictionary`; multi-instance resets. Hygiene, not a WAF. Tests only cover 11th acquire on one key (`PublicRegisterRateLimiterTests.cs:10–21`).

### B07-I25 — P1 — CORS default allow-any when `App:CorsOrigins` is empty

**Where.** `AuthAndCorsExtensions.cs:196–212`. Empty → `AllowAnyOrigin` + any header/method (**no** credentials). Non-empty → listed origins + `AllowCredentials`.

**What.** Repo appsettings sets origins. `AppOptions.CorsOrigins` default is `""`. Production `env.example` sets `App__CorsOrigins=https://hub.lazuar.com`. Clearing the key in prod disables credentialed CORS (SPA cookie calls fail) **or**, if a client does not need credentials, opens the API to any origin. Misconfig foot-gun. 008 H10.

### B07-I26 — P1 — API key 5-minute cache if revoke never consumes

**Where.** `ApiKeyAuthenticationMiddleware.cs:51`; `RevokeApiCredentialCommand.cs:48–49`; `ApiKeyRevokedIntegrationEventHandler.cs:24–32`.

**What.** Happy path evicts. Outbox/inbox stall → stolen key lives until TTL. `Revoked_Key_After_Cache_Eviction_Returns_401` **simulates** an already-empty cache (`ApiKeyAuthenticationTests.cs:201–212`). It does not run the handler against a warm cache and then prove the next request 401s through SQL. Adjacent test `ApiKeyRevokedIntegrationEventHandlerTests` does evict. The pair is close to honest; the “revoked key” middleware test name overclaims (see §11).

### B07-I27 — P2 — Cookie `OnMessageReceived` always wins over Authorization JWT

**Where.** `AuthAndCorsExtensions.cs:52–64`.

**What.** Documented dual-realm. Integrators debugging with Bearer + a leftover ops cookie will not see their Bearer identity. Not a steal.

### B07-I28 — P2 — No invite resend; revoke has no audit

**Where.** No command. `RevokeWorkspaceInvitationCommand.cs:43–45` sets REVOKED, no recorder.

**What.** Completes the “mail failed, now what?” dead-end with B07-I01.

### B07-I29 — P2 — `HasTenantAccess` ignores archive and role

**Where.** `OneQueryService.cs:72–78`.

**What.** Any historical membership reads members/invites/audit. VIEWER included (intended). Archived included (not intended if archive means leave).

### B07-I30 — P2 — GET members/workspace use 401 for IDOR; audit uses 403

**Where.** `WorkspaceEndpoints.cs:33, 87, 103` vs `:177`.

**What.** Same predicate, two statuses. Clients that treat 401 as “login again” will bounce a VIEWER who typed the wrong GUID into a logout loop.

### B07-I31 — P2 — `ExecutionContextAccessor.UserRole` is the first role claim

**Where.** `ExecutionContextAccessor.cs:38`.

**What.** After injection the first claim is JWT `CLIENT`, the second is `ADMIN`. Anything that reads `UserRole` thinks the owner is a CLIENT. Policies use `IsInRole` and are fine. New code that switches on `UserRole` will be wrong.

### B07-I32 — P2 — Genesis rotates superadmin password from env every boot

**Where.** `SystemGenesisBootstrapperJob.cs:75–79`.

**What.** Convenient. A leaked `PLATFORM_ADMIN_PASSWORD` in the runtime env is a standing password reset. Dev `appsettings.Development.json:17–18` has `PLATFORM_ADMIN_EMAILS` / `PLATFORM_ADMIN_PASSWORD` in the repo. Dev-only.

### B07-I33 — P2 — Storage presign is any member; path is tenant-required

**Where.** `StorageEndpoints.cs:27–48`; `TenantSecurityMiddleware.cs:160–164`.

**What.** Empty tenant 400s (pre-wave hole closed). VIEWER can still upload. Not OrgAdmin.

### B07-I34 — P2 — IApiCredentialService + LHDN façade comments still promise implicit LHDN defaults

**Where.** `IApiCredentialService.cs:32–34`; `AdminApiKeyEndpoints.cs:51`; `Lhdn/Domain/ApiKeyScopes.cs:14–17`.

**What.** Command rejects omit (`GenerateApiCredentialCommand.cs:57`; tests). Comments are a lying interface. High odds of a “compat” “fix” that re-opens the default.

### B07-I35 — P2 — One README still documents `CLIENT` membership and omits `AuditEvents`

**Where.** `Modules/One/README.md:22, 33–34, 59–68`.

**What.** Drift. Public-register paragraph (`:10–11`) is correct.

### B07-I36 — P2 — AppOptions ClientUrl default 3020 vs live 3004

**Where.** `AppOptions.cs:10` vs `appsettings.json:41` vs `OneLinkService.cs:17`.

**What.** Dead default today. Future bind-to-options foot-gun for reset/verify (already 404 on 3004).

### B07-I37 — P2 — Invite token in the query string

**Where.** Mail URL; `AcceptInvitePage` reads `searchParams`.

**What.** Server access logs, browser history, Referer if the success page ever loads a third party. Accept is first-party today. Prefer POST-only after a fragment, or a one-time exchange. P2.

### B07-I38 — P2 — CSRF residual: SameSite=Lax, no anti-CSRF token, Domain `.lazuar.com`

**Where.** `AuthEndpoints.cs:206–213`.

**What.** Lax blocks most cross-site POST. Same-site sibling apps on `*.lazuar.com` can POST with the cookie. 008 H11. Hub path-based deploy (`hub.lazuar.com` + `/portal`) is same-site by definition.

### B07-I39 — P2 — No MFA, SSO, lockout, session list, password complexity

**Where.** `PasswordService` is BCrypt work factor 11 (`PasswordService.cs:15–16`; `appsettings.json:32–34`). `GlobalUser` has no lockout/MFA/last-login.

**What.** Procurement-questionnaire fail. Not a crash. Do not put “SSO” on a pricing page.

### B07-I40 — P2 — `UserRegisteredDomainEvent` is orphaned; verify never starts at register

**Where.** `GlobalUser.cs:44`; grep of handlers.

**What.** Completes B07-I02’s verify half. Resend is the only mint.

---

## 11. Lying tests and test gaps

A test lies when its name or comment asserts a runtime property it does not exercise, or when it re-implements production so it cannot fail when production changes.

### 11.1 Tests that lie or overclaim

| Test | Why it lies |
|------|-------------|
| `WorkspaceCreateAuthorizationTests.Post_Workspaces_Requires_Authorization` | Reads `WorkspaceEndpoints.cs` as text and asserts the substrings `RequireAuthorization()` and `UserId == Guid.Empty` appear. Does not spin up the host. A comment above `RequireAuthorization` would still pass. A policy rename would still pass. |
| `WorkspaceCreateAuthorizationTests.Get_Public_Pricing_Is_Anonymous` | Same source-string technique on `AuthEndpoints.cs`. Honest as a “do not accidentally add RequireAuthorization between pricing and register” tripwire. Not an anonymous HTTP test. |
| `AuditRecorderTests.ForeignOrg_GetAudit_Forbidden` | Copies the endpoint’s three-line auth check into the test (`AuditRecorderTests.cs:99–107`) and never calls `MapGet("/workspaces/{id}/audit")`. If the real endpoint dropped the Forbid, this test would still pass. |
| `ApiKeyAuthenticationTests.BuildAuthorizationService` | **Re-duplicates** the entire policy catalog (`:488–593`). Host `AuthAndCorsExtensions` can add `API_CLIENT` back to `OrgAdmin` and this suite stays green. The tests lock a **copy**. |
| `ApiKeyAuthenticationTests.Revoked_Key_After_Cache_Eviction_Returns_401` | Name says “after eviction.” Body **asserts the cache is already empty** and then looks up with no SQL factory. It tests “unknown key 401,” which the previous test already did. Eviction is tested in `ApiKeyRevokedIntegrationEventHandlerTests` (different project folder). |
| `ApiKeyAuthenticationTests.Valid_Cached_Key_*` | Plants a cache entry. Never hits `LookupCredentialAsync` SQL. Honest as a claims-shaping test if named that way. |
| `InviteUserToWorkspaceCommandHandlerTests.Invite_RecordsAuditWithoutSecrets` | `m.ToString()!.Contains(...)` on an anonymous object. Relies on the compiler’s `ToString` for anonymous types including the email and not the token. Brittle; happened to work. Does not serialize the way `AuditRecorder` does (snake_case JSON). |
| `OneLinkServiceTests.InviteEmail_UsesOpsAcceptUrl_NotClientUrl` | Honest about the HTML string. Comment/name do not claim a page exists or that mail sends. **Not a lie.** Do not “upgrade” it to imply the loop is closed. |
| `AcceptWorkspaceInvitationCommandHandlerTests` (the fixture) | Honest for the three cases it has. The **gap** (not a lie): no replay, no already-member, no hash-mismatch, no double-pending, no inactive user. A reader of the fixture name could think accept is done. |
| `RegisterPublicUserCommandHandlerTests` / `CreateWorkspaceCommandHandlerTests` | NSubstitute repos; `IsSlugUniqueAsync` is in-memory. Honest as command tests. They cannot see the unique-index 500. |
| `PublicRegisterRateLimiterTests.Blocks_After_Budget` | Honest for one process, one key. Does not cover empty key allow, XFF spoof, or multi-instance reset. |

### 11.2 Tests that do not exist (and 008 already noted most)

- No test that `apps/lazuar-ops` routes `/accept-invite`.
- No test that invite dispatch uses system tenant (it does not — that test would fail today and would be the right lock for B07-I01).
- No test that register-from-invite-returnUrl still accepts.
- No test that logout Set-Cookie matches Domain.
- No test that stamp is checked on a non-`/me` route (it is not).
- No test that `IntegratorProvisionAuth` **rejects** membership-only `SUPER_ADMIN`.
- No end-to-end invite test (008: “There is no end-to-end invite test”). Still true.
- No test that Ops hides nav by role (09; still true).

`297ba98` added `AcceptWorkspaceInvitationCommandHandlerTests` and `OneLinkServiceTests`. Those are real and useful. They do not cover the 500 path the SPA special-cases.

---

## 12. Ranked open bugs

Rank is “fix this before you sell Team / recovery / support jump-in,” not a sprint board.

| Rank | Id | Sev | One-line |
|------|----|-----|----------|
| 1 | B07-I01 | P0 | Invite mail still tenant-BYOK; token unrecoverable; Team lies “sent.” Accept page cannot save a loop that never delivers. |
| 2 | B07-I20 | P1 | Membership `SUPER_ADMIN` + ops `X-Tenant-Id` opens integrator provision. |
| 3 | B07-I06 + B07-I07 | P1 | Prod logout/stamp delete may not clear `.lazuar.com` cookie; stamp not global. Stolen/left-behind JWT works for 24h on every money route. |
| 4 | B07-I03 + B07-I04 + B07-I05 | P1 | Second accept / already-member is a 500 with leaked SQL; pending index not unique; no accept audit. |
| 5 | B07-I02 + B07-I40 | P1 | Reset/verify URLs 404 on portal; verify API is session-bound; register never starts verify. |
| 6 | B07-I13 | P1 | Login unlimited, 400-with-401. |
| 7 | B07-I10 + B07-I11 | P1 | Last-admin remove; archive does not revoke keys. |
| 8 | B07-I12 | P1 | Superadmin switcher 403s every `/admin/*`. |
| 9 | B07-I19 + B07-I26 | P1 | 500 Detail leak; key cache if revoke outbox stalls. |
| 10 | B07-I25 | P1 | CORS allow-any if origins key cleared. |
| 11 | B07-I08 + B07-I09 + B07-I28 | P2 | SPA 500 lie + cached errors; Team ignores pending GET/DELETE; no resend. |
| 12 | B07-I14 | P2 | Register-from-invite still mints a stray workspace (does **not** break accept). |
| 13 | B07-I15 + B07-I31 + B07-I34 + B07-I35 | P2 | Dual-role teachability; `UserRole` first claim; comment/README lies waiting to re-introduce `CLIENT` or implicit LHDN scopes. |
| 14 | B07-I16 + B07-I17 + B07-I18 | P2 | Slug races; reset oracle; `SK_LIVE_` 401. |
| 15 | B07-I21–I24, I27, I29, I30, I32, I33, I36–I39 | P2 | Remainder: ADMIN M2M bypass, entitlements error shell, TOS, XFF, cookie-vs-Bearer, archive read, 401-vs-403, genesis rotate, storage, AppOptions 3020, query-token, CSRF siblings, no MFA. |

**Do not rank as open:** accept page missing (fixed), invite URL on ClientUrl (fixed), portal leftover 404 for accept (fixed via 302), `OrgAdmin` includes `API_CLIENT` (fixed), invite role is a free string (fixed on the invite path), members GET IDOR without `HasTenantAccess` (fixed on handlers).

---

## 13. What 297ba98 actually changed (so the next agent does not re-fix it)

Commit `297ba987d8dffbbecab983099eba55cfc76f2d01`, 13 files, +408 / −19.

- `NotificationDispatchDomainEventHandlers.cs` — invite link `GetOpsBaseUrl()` instead of `GetClientBaseUrl()`. **Reset and verify untouched.**
- `IOneLinkService` / `OneLinkService` — added `GetOpsBaseUrl()`, default `:3003`.
- `AppOptions` + appsettings + `deploy/prod/env.example` — `OpsUrl`.
- New `AcceptWorkspaceInvitationCommandHandlerTests` (happy / expired / wrong email).
- New `OneLinkServiceTests` (ops URL, not client).
- Ops `App.tsx` public `/accept-invite`.
- Ops `LoginPage` invite copy + `returnUrl` on signup/signin links.
- New `AcceptInvitePage.tsx` (session probe, accept POST, wrong-email sign-out, 500 bandage, entitlements diff).
- Portal `/accept-invite` 302 to ops.

It did **not** change: register command, accept command (no already-member, no audit), invite dispatch tenant id, Team page, logout cookie options, stamp checks, login limiter, archive, provision auth, comments, README.

---

## 14. Files table (absolute) — identity-critical

| File | Role in this audit |
|------|--------------------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/AcceptWorkspaceInvitationCommand.cs` | Email bind, expiry, missing already-member, no audit |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/InviteUserToWorkspaceCommand.cs` | Allow-list, 7-day token, audit without secret |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/RegisterPublicUserCommand.cs` | Always creates workspace + ADMIN + five apps |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/RemoveWorkspaceMemberCommand.cs` | No last-admin |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/ArchiveWorkspaceCommand.cs` | Boolean archive |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/ForgotPasswordCommand.cs` | Silent mint |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/ResetPasswordCommand.cs` | Oracle messages |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/VerifyEmailCommand.cs` | Hash/expiry; unused by register |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/GenerateApiCredentialCommand.cs` | Closed scopes, full-string hash |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/RevokeApiCredentialCommand.cs` | Outbox revoke event |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/EventHandlers/NotificationDispatchDomainEventHandlers.cs` | OpsUrl invite; ClientUrl reset/verify; invite tenant-scoped |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Services/OneLinkService.cs` | Ops vs Client defaults |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs` | Cookie, login, register, stamp, XFF, logout delete |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs` | Accept, entitlements, audit Forbid, members 401 |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/ApiCredentialEndpoints.cs` | OrgAdmin keys |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/PlatformAuthEndpoints.cs` | Admin cookie realm |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/OneDbContext.cs` | Unique membership; non-unique pending invite |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Services/IntegratorProvisionAuth.cs` | `IsInRole("SUPER_ADMIN")` hatch |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs` | Inject role; exempt `/one/workspaces`; 403 non-member |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs` | One-only SQL; 5 min cache; prefix case |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs` | Policies, dual cookie, CORS |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/GlobalExceptionHandler.cs` | 400 vs 500 mapping |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/PlatformDbContext.cs` | Domain events before persist; fail-closed tenant |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/TokenGeneratorService.cs` | SHA-256 hex |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/Infrastructure/Email/ResendEmailService.cs` | No tenant fallback |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/AcceptInvitePage.tsx` | New accept UI |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/App.tsx` | Public `/accept-invite`; empty-state; catch-all |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/components/LoginPage.tsx` | Invite copy; still creates workspace |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/TeamPage.tsx` | Roster + invite; no pending |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/accept-invite/page.tsx` | Leftover 302 |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/One/OneLinkServiceTests.cs` | Locks OpsUrl |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/One/AcceptWorkspaceInvitationCommandHandlerTests.cs` | Three cases; no 500 path |

---

## 15. One-line judgment

**The accept page is real and the invite URL is on `App:OpsUrl`. Team is still not a product: invite mail dies without tenant BYOK, pending invites are invisible, a second accept is a 500, and register-from-invite still mints a stray workspace (which does not break accept). Sell the self-serve workspace and the scoped key. Do not sell staff onboarding, password recovery, or “we jump into your tenant” until B07-I01, B07-I02, B07-I06/I07, B07-I03, and B07-I12 are gone.**
