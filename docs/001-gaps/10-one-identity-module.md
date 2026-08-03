<!-- Source subagent: 019fc650-3512-7283-86ea-56858db1d216 -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# One Module (Identity & Workspace) Gap Analysis

**Scope:** `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/One/`, `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/packages/api-spec/modules/one/`, JWT/magic-link/password services in BuildingBlocks, and related host middleware (`ApiKeyAuthenticationMiddleware`, `TenantSecurityMiddleware`).

---

## Module Inventory

| Layer | Path | Contents |
|--------|------|----------|
| **Domain** | `Modules/One/Domain/` | Aggregates/entities: `GlobalUser`, `Organization`, `TenantMembership`, `TenantAppEntitlement`, `WorkspaceInvitation`, `TenantWebhookEndpoint`, `WebhookDeliveryOutbox`. Rules: `OrganizationSlugMustBeValidRule`. Domain events (8). |
| **Application** | `Modules/One/Application/` | 16 command handlers, 3 agent queries, domain→integration/email event handlers, `IOneRepository`, `IOneLinkService`. DI is a MediatR scan marker only. |
| **Contracts** | `Modules/One/Contracts/` | `IOneQueryService` + snapshot DTOs; integration events: `TenantProvisioned`, `TenantUpdated`, `WorkspaceUpdated`, `GlobalUserProfileUpdated`, `AppEntitlementGranted`. |
| **Infrastructure** | `Modules/One/Infrastructure/` | `OneDbContext` (schema `one`), `Endpoints.cs`, repository, query service, link service, outbox/inbox workers, genesis bootstrapper, webhook dispatcher, outbound webhook handler, EF migrations (2). |
| **API Spec** | `packages/api-spec/modules/one/` | `models.tsp`, `routes.tsp`; product doc entry `docs-one.tsp`. |
| **Host glue** | `src/Lazuar.Api/` | JWT cookie auth, `OrgAdmin` policy, `ApiKeyAuthenticationMiddleware` (reads **LHDN** keys), `TenantSecurityMiddleware` (uses `IOneQueryService`), platform group for SUPER_ADMIN. |
| **BuildingBlocks** | `BuildingBlocks/` | `JwtService`, `PasswordService` (BCrypt), `MagicLinkTokenService` (HMAC, **subscription**-scoped), `TokenGeneratorService` (SHA256 token hashes), `PlatformDbContext` (tenant filters + domain-event dispatch). |

**Documented but not implemented:** README claims `AppAccessRequest`, `CommunitySubscriptionActivatedIntegrationEvent` consumption, and table `one.AppAccessRequests`. **None of these exist** in domain, DbContext, or migrations.

**Implemented but not in README:** tenant outbound webhooks (`TenantWebhookEndpoint`, delivery outbox, dispatcher), storage presigned URL endpoint, agent tools under Ops.

---

## Domain Model (Users, Workspaces, Memberships, etc.)

### Present aggregates / entities

| Entity | Tenant-scoped? | Purpose | Notes / gaps |
|--------|----------------|---------|--------------|
| **`GlobalUser`** | No (global identity) | Email, name, BCrypt hash, `SecurityStamp`, `IsSystemAdmin`, `IsActive`, email-verify + password-reset token hashes/expiries | No soft-delete path beyond `IsActive` (no domain method to deactivate). No MFA fields. No last-login / lockout. Email immutable after create. `UserRegisteredDomainEvent` raised but **no handlers**. |
| **`Organization`** | No (root of tenant) | Name, slug, `IsActive`, timestamps | Create/update/archive. Slug rules + reserved list. Archive sets `IsActive=false` + `OrganizationArchivedDomainEvent` — **no integration-event publisher** for archive. |
| **`TenantMembership`** | Yes (`IMustHaveTenant`) | User ↔ org + free-string `Role` | Unique `(GlobalUserId, OrganizationId)`. Role is string (`ADMIN` / `CLIENT` by convention), **not enum, not permission set**. No `ChangeRole`, no invite-upgrade path. |
| **`TenantAppEntitlement`** | Yes | App module toggles (`OPS`, `BILLING`, `PAYMENTS`, `CRM`, `LHDN`, …) | Unique `(OrganizationId, AppId)`. Grant fires integration event; **revoke/disable does not emit** a revoked event. |
| **`WorkspaceInvitation`** | Yes | Email, role, token hash, status (`PENDING`/`ACCEPTED`/`REVOKED`), 7-day expiry | Partial unique index for pending org+email. Accept requires logged-in user with matching email. |
| **`TenantWebhookEndpoint`** | Yes | Outbound developer webhook URL + `whsec_` secret | One logical endpoint per org (query by org). Secret regenerated only on first create. |
| **`WebhookDeliveryOutbox`** | Yes | Delivery queue with exponential backoff (5 attempts) | Fed by Commerce `OutboundWebhookRequestedIntegrationEvent`. |
| **Outbox/Inbox** | Platform infra | Cross-module messaging | Standard module pattern. |

### Missing domain concepts (gaps)

1. **`AppAccessRequest` / B2B onboarding queue** — README-only.
2. **API keys / integration credentials** — live in **LHDN** (`DeveloperApiKey`), not One.
3. **LHDN MyInvois config** — lives in **LHDN** (`LhdnTenantConfig`), while TypeSpec puts GET/PUT under `/one/workspaces/{id}/lhdn-config`.
4. **Refresh tokens / sessions table** — cookie JWT only.
5. **Fine-grained permissions** — role string only.
6. **Membership role change**, transfer ownership, last-admin protection.
7. **User deactivation / force-logout** as domain operations (stamp rotation exists only on password change).
8. **Magic-link login for GlobalUser** — not in One; Commerce uses `IMagicLinkTokenService` for **subscriber portal** by subscription id.
9. **Password policy** (length/complexity) — not in domain or commands.
10. **Audit log** of auth/admin actions.

### Relationships (conceptual)

```
GlobalUser 1──* TenantMembership *──1 Organization
Organization 1──* TenantAppEntitlement
Organization 1──* WorkspaceInvitation
Organization 1──* TenantWebhookEndpoint 1──* WebhookDeliveryOutbox
```

Downstream modules store `OrganizationId` / `GlobalUserId` as bare GUIDs (no FKs into `one`).

---

## Auth Flows (login, magic link, password)

### BuildingBlocks primitives

| Service | Behavior | Used by One? |
|---------|----------|--------------|
| **`PasswordService`** | BCrypt, work factor from `Security:PasswordWorkFactor` (default 11) | Login, register, change/reset password, genesis seed |
| **`JwtService`** | HMAC-SHA256 JWT, claims + `expiryHours` | Cookie issue on register/login |
| **`TokenGeneratorService`** | CSPRNG token + SHA256 hash (URL-safe base64) | Email verify, password reset, invites, webhook secrets |
| **`IMagicLinkTokenService`** | HMAC over `{subscriptionId}:{expiry}` Base64, 24h, secret = `Jwt:Secret` | **Not used by One** — Commerce portal only |

### Password login (`POST /api/v1/one/auth/login`)

1. Normalize email; load `GlobalUser`.
2. Reject if missing / inactive / password fail → **400 ProblemDetails with status 401** (odd status mapping; not 401 HTTP).
3. Role in response: `SUPER_ADMIN` if `IsSystemAdmin`, else **`CLIENT`** (not workspace role).
4. `IssueCookie`: claims `NameIdentifier`, `Email`, `Role` (same SUPER_ADMIN/CLIENT), `is_system_admin`, `is_email_verified`, `security_stamp`.
5. Cookie name `lazuar_auth`, HttpOnly, Secure outside dev, SameSite=Lax, domain `.lazuar.com` in non-dev, TTL = `Jwt:ExpiryHours` (24).

**Gaps:**

- No rate limiting / lockout / CAPTCHA.
- No “upgrade-on-login” for legacy hashes (doc `008-password-hashing…` is unimplemented blueprint for a fictional `UserAccess` module).
- Email verification **not required** to log in.
- Workspace role **not** in JWT; only injected later by `TenantSecurityMiddleware` when `X-Tenant-Id` / slug present.
- Login returns HTTP 400 for auth failure (spec says ProblemDetails; clients may mis-handle).

### Public register (`POST /api/v1/one/public/register`)

Atomic-ish handler creates:

1. `GlobalUser` (unverified),
2. `Organization` + membership `ADMIN`,
3. Core entitlements: `OPS`, `BILLING`, `PAYMENTS`, `CRM`, `LHDN` + `AppEntitlementGranted` outbox rows each,
4. Cookie as login with response role hard-coded **`ADMIN`** (while JWT role is still `CLIENT` unless system admin).

**Gaps:**

- No email verification token issued on register (`UserRegisteredDomainEvent` unused).
- Response role `ADMIN` ≠ JWT claim role `CLIENT` → **client/UI inconsistency**.
- No password strength checks.
- Unlimited workspace creation for any authenticated user later via `POST /workspaces`.

### Logout

Deletes cookie `lazuar_auth` only (no server-side session revocation list). Security stamp invalidation is not applied on logout.

### Forgot / reset password

- Forgot: silent no-op if user missing; token 24h; domain event → `DispatchMessageIntegrationEvent` email with link to `App:ClientUrl/reset-password?...`.
- Reset: hash compare + `ResetPassword` (rotates stamp).
- **No automatic cookie invalidation** for other devices except stamp; stamp is **only checked on `/auth/me`**, not globally in JWT middleware.

### Email verification

- `POST /auth/verify-email` **requires auth** and uses `ctx.UserId`’s email + body token.
- Resend is public by email.
- Spec links use email query params; API verify path is session-bound → **spec/UI vs API mismatch risk** (email link may land unauthenticated).
- Register never starts verification unless client calls resend.

### Change password / profile

- Change password verifies current password, rotates stamp.
- Profile updates name only → `GlobalUserProfileUpdated` integration event (CRM consumer exists).

### Magic link

| Kind | Owner | Status |
|------|--------|--------|
| Staff invite “magic” token | One (`WorkspaceInvitation` + email link) | Implemented (hash at rest, 7 days) |
| Platform passwordless login | — | **Absent** |
| Subscriber portal magic link | Commerce + `IMagicLinkTokenService` | Implemented outside One; not identity SSO |

### Superadmin / platform auth

- Genesis job seeds SUPER_ADMIN from `PLATFORM_ADMIN_EMAILS` / `PLATFORM_ADMIN_PASSWORD`.
- Separate platform cookie path `/api/v1/platform` uses `lazuar_admin_auth` (Payments `PlatformEndpoints`) — parallel auth surface, not One endpoints.

---

## Workspace / Tenant Lifecycle

| Stage | Implementation | Gaps |
|-------|----------------|------|
| **System tenant** | Raw SQL upsert id `00000000-…-0001`, slug `system` | Bypasses slug reserved rule; superadmins **not** auto-added as members |
| **Public signup** | User + workspace + core entitlements | Always grants fixed core apps; no approval queue |
| **Create additional workspace** | Any authenticated user; ADMIN membership; optional `provision_apps` | No entitlement of core apps by default (unlike signup); no plan/billing gate |
| **Update** | ADMIN membership required; raises `WorkspaceUpdatedIntegrationEvent` | Slug uniqueness not re-checked on update (only domain format rule if slug changes) |
| **Archive** | ADMIN only; `IsActive=false` | **No** `TenantUpdated` / archive integration event; members still have membership; apps still entitled; no cascade to billing/API keys |
| **Resolve tenant** | Middleware: `X-Tenant-Id`, `X-Tenant-Slug`, or route `tenantSlug` | `/one/*` workspace routes use path GUID and often **skip** tenant header membership enforcement |
| **Onboarding queue** | Documented only | Missing |
| **Subscription → membership** | Documented Community event | Missing (Community module removed) |

### Entitlements lifecycle

- **Grant** on signup (hardcoded core list), create-workspace `provision_apps`, or superadmin toggle.
- **Consumers of `AppEntitlementGranted`:** Communications (template seed), Billing (starter credits when `BILLING`).
- **Disable:** DB toggle only; no “entitlement revoked” event → downstream modules may keep serving features.

### Provisioning events

- `OrganizationCreated` → `TenantProvisionedIntegrationEvent` (Messaging replica handlers).
- Update → `WorkspaceUpdatedIntegrationEvent` (Messaging + API cache invalidation for API keys).
- **`TenantUpdatedIntegrationEvent` is never published** though Messaging still subscribes.

---

## API Key Presence or Absence

### Finding: **One does not own API keys**

| Concern | Location |
|---------|----------|
| Aggregate `DeveloperApiKey` | `Modules/Lhdn/Domain/Aggregates/DeveloperApiKey.cs` → table `lhdn.DeveloperApiKeys` |
| Generate / revoke HTTP | `POST/DELETE /api/v1/lhdn/api-keys` (OrgAdmin) |
| Auth middleware | Host `ApiKeyAuthenticationMiddleware` queries **`lhdn."DeveloperApiKeys"`** by SHA256 hash |
| Claims | `NameIdentifier=api_client`, `Role=API_CLIENT`, `TenantId`, `IsTestMode` |
| Cache invalidation | `ApiKeyRevokedIntegrationEvent` (LHDN); `WorkspaceUpdated` clears cached key hashes |

### One’s related “credentials”

| Credential | Owner | Notes |
|------------|-------|-------|
| JWT session cookie | One issue / host validate | Identity session |
| Invite / reset / verify tokens | One + `ITokenGeneratorService` | Hashed at rest |
| Outbound webhook signing secret (`whsec_…`) | One `TenantWebhookEndpoint` | **Returned in clear on GET webhooks** |
| MyInvois client id/secret | LHDN `LhdnTenantConfig` | Spec incorrectly under One routes |
| Platform Resend / OpenAI keys | Host config | Not tenant |

**Absence in One:** no `one.ApiKeys`, no personal access tokens, no OAuth clients, no service accounts, no scoped keys (read-only vs full).

---

## Authorization Model (roles, permissions)

### Role sources (three layers — easy to confuse)

1. **Global JWT role:** `SUPER_ADMIN` | `CLIENT` from `IsSystemAdmin`.
2. **Workspace membership role:** free string, typically `ADMIN` | `CLIENT` (invite accepts arbitrary role string uppercased).
3. **API key role:** `API_CLIENT`.

### Host policy

```csharp
// Program.cs
"OrgAdmin" => RequireRole("SUPER_ADMIN", "ADMIN", "API_CLIENT")
```

`TenantSecurityMiddleware` **adds** `ClaimTypes.Role` from membership when tenant header/slug resolves. Superadmin without membership gets **403** on tenant-scoped requests (no membership role) unless middleware skipped (ApiKey path).

### Enforcement map (One endpoints)

| Endpoint class | Policy / check | Gap |
|----------------|----------------|-----|
| Public register/login/forgot/reset/resend | None | OK for public |
| `/auth/me`, profile, password | `RequireAuthorization` | Stamp check only on `/auth/me` |
| Create/update/archive workspace | Auth + **command** membership (update/archive ADMIN) | Create: any user |
| List all workspaces / apps / toggle apps | Auth + **`IsSystemAdmin`** + OrgAdmin policy | Correct for superadmin ops |
| Members / invites list | Auth only | **No membership check** — IDOR risk |
| Invite | Auth + command `HasMembership` only | **Any member** can invite, not just ADMIN; role not validated allow-list |
| Revoke invite | Command requires ADMIN | OK |
| Remove member | `HasMembership` only | Non-admin can remove; no last-admin / self rules |
| Accept invite | Auth + email match | OK |
| Webhooks GET/logs | Membership or superadmin | OK |
| Webhooks PUT | Role ADMIN / SUPER_ADMIN / system admin | SUPER_ADMIN string is global, not membership |
| Presigned storage URL | Auth only | Uses `ctx.TenantId` (empty if no header) → **can write under `vault/{EmptyGuid}/…`** |
| LHDN config in TypeSpec | Spec auth | **Endpoints not implemented in One** |

### Permission model gap

- No RBAC matrix (e.g. `billing:read`, `lhdn:submit`).
- Agent tools declare role strings on attributes (`SUPER_ADMIN`, `ADMIN`) but enforcement is Ops-side, not re-checked in One commands for system admin vs membership.

---

## Endpoints Surface

Base: **`/api/v1/one`** (from `MapOneEndpoints` under `/api/v1`).

### Implemented (code)

| Method | Path | Auth |
|--------|------|------|
| POST | `/public/register` | Public |
| POST | `/auth/login` | Public |
| POST | `/auth/logout` | Public |
| POST | `/auth/forgot-password` | Public |
| POST | `/auth/reset-password` | Public |
| POST | `/auth/verify-email` | Auth |
| POST | `/auth/resend-verification` | Public |
| GET | `/auth/me` | Auth |
| PUT | `/me/profile` | Auth |
| PUT | `/me/security/password` | Auth |
| GET | `/me/entitlements` | Auth |
| GET | `/workspaces` | Superadmin |
| POST | `/workspaces` | Auth |
| PUT | `/workspaces/{id}` | Auth + ADMIN membership |
| DELETE | `/workspaces/{id}` | Auth + ADMIN membership |
| GET | `/workspaces/{id}/members` | Auth (weak) |
| POST | `/workspaces/{id}/invites` | Auth (weak) |
| GET | `/workspaces/{id}/invites` | Auth (weak) |
| DELETE | `/workspaces/{id}/invites/{inviteId}` | Auth + ADMIN |
| DELETE | `/workspaces/{id}/members/{userId}` | Auth (weak) |
| POST | `/workspaces/invites/accept` | Auth |
| GET | `/workspaces/{id}/apps` | Superadmin |
| POST | `/workspaces/{id}/apps/{appId}` | Superadmin |
| GET/PUT | `/workspaces/{id}/webhooks` | Membership / ADMIN |
| GET | `/workspaces/{id}/webhooks/logs` | Membership |
| POST | `/storage/presigned-url` | Auth |

### TypeSpec-only / missing implementation

| Spec route | Status |
|------------|--------|
| `GET /workspaces/{id}/lhdn-config` | **Missing in One.Endpoints** (and not under LHDN list either as GET config) |
| `PUT /workspaces/{id}/lhdn-config` | **Missing**; MyInvois credentials only via domain methods + certificate PUT under **`/lhdn/workspaces/{id}/lhdn-certificate`** |

### Spec vs code deltas

- Spec marks many routes `@useAuth(BearerAuth)`; implementation is **cookie JWT** primary (`OnMessageReceived` from cookie). Bearer still works if client sends token.
- Spec login `password?` optional; code requires password.
- Spec has no separate agent APIs (agent tools are MediatR + Ops, not HTTP One routes).

### Agent query surface (MediatR, not HTTP)

- `GetWorkspaceDetailsAgentQuery`
- `ListWorkspaceMembersAgentQuery`
- `ListAppEntitlementsAgentQuery`
- Agent commands: invite, remove member, toggle entitlement

---

## Event Emissions to Other Modules

### Domain events (in-process MediatR during `SaveChanges`)

| Event | Raised when | Handled? |
|-------|-------------|----------|
| `UserRegisteredDomainEvent` | New user | **No handler** |
| `GlobalUserProfileUpdatedDomainEvent` | Name change | → `GlobalUserProfileUpdatedIntegrationEvent` |
| `PasswordResetRequestedDomainEvent` | Forgot password | → email via Messaging `DispatchMessageIntegrationEvent` |
| `EmailVerificationRequestedDomainEvent` | Resend verify | → email |
| `WorkspaceInvitationCreatedDomainEvent` | Invite | → email (tenant org id) |
| `OrganizationCreatedDomainEvent` | New org | → `TenantProvisionedIntegrationEvent` |
| `OrganizationUpdatedDomainEvent` | Name/slug | → `WorkspaceUpdatedIntegrationEvent` |
| `OrganizationArchivedDomainEvent` | Archive | **No handler / no integration event** |

### Integration events published (outbox `one.OutboxMessages`)

| Event | Publishers | Known consumers |
|-------|------------|-----------------|
| `TenantProvisionedIntegrationEvent` | Org created handler | Messaging (replica / seed) |
| `WorkspaceUpdatedIntegrationEvent` | Org updated handler | Messaging; host API key cache clearer |
| `GlobalUserProfileUpdatedIntegrationEvent` | Profile handler | CRM |
| `AppEntitlementGrantedIntegrationEvent` | Register, CreateWorkspace, Toggle (grant only) | Communications, Billing starter credits |
| `DispatchMessageIntegrationEvent` (Messaging contracts) | Notification handlers | Messaging pipeline |
| `TenantUpdatedIntegrationEvent` | **Never published** | Messaging still subscribed (dead contract) |

### Integration events consumed by One

| Event | Handler | Role |
|-------|---------|------|
| `OutboundWebhookRequestedIntegrationEvent` (Commerce) | `OutboundWebhookEventHandlers` | Enqueue signed delivery |

**Not consumed (README fiction):** `CommunitySubscriptionActivatedIntegrationEvent`.

### Outbox timing note

Command handlers call `_eventBus.PublishAsync` **before** `SaveChangesAsync`; `OutboxEventBus` only stages rows on the same `OneDbContext`, so they commit with the aggregate — correct. Domain-event handlers also stage outbox rows **before** the final `base.SaveChangesAsync` — correct.

---

## Security Gaps

### Critical / high

1. **IDOR on members & invites listing** — any authenticated user who can guess a workspace GUID can list members/invites.
2. **Invite / remove member without ADMIN role** — `HasMembership` only; CLIENT can invite and remove.
3. **Webhook secret exposure** — GET returns full `secret_key`.
4. **Security stamp not enforced on every request** — only `/auth/me`; stolen JWT works until expiry after password change.
5. **Presigned upload without tenant membership / empty TenantId** — auth-only + `ctx.TenantId` default empty.
6. **Default JWT secret / keys in appsettings** (dev secrets, Resend key, OpenRouter key present in repo config) — ops risk.
7. **API keys living in LHDN schema while middleware is platform-global** — any module route can be called with LHDN-issued keys as `API_CLIENT` if OrgAdmin policy applies; ownership/scoping unclear.
8. **No login rate limit / lockout**.

### Medium

9. Register response role vs JWT claim mismatch (`ADMIN` vs `CLIENT`).
10. Email verification not required; not auto-sent on signup.
11. Verify-email requires authenticated session — breaks email-link UX.
12. Archive workspace does not revoke keys, memberships, or emit cross-module archive.
13. Invite `Role` accept-any string (privilege escalation if client sends `ADMIN` when inviter is CLIENT — already possible given invite auth gap).
14. Superadmin genesis re-hashes password from env on every boot if verify fails pattern, and elevates via raw SQL.
15. System admin without membership cannot act on tenant routes (403) — may be intentional but blocks superadmin support tooling unless they use OrgAdmin-only One endpoints that check `IsSystemAdmin`.
16. Cookie `SameSite=Lax` + multi-subdomain; CSRF surface on state-changing cookie auth (no anti-CSRF tokens).
17. MagicLinkTokenService reuses `Jwt:Secret` and is subscription-scoped only (commerce blast radius if secret leaked).
18. Outbound webhook dispatcher no SSRF controls (private IP allowlist).

### Low / hygiene

19. Login returns 400 with embedded status 401.
20. Password complexity / breach checks absent.
21. No MFA / passkeys / SSO (OIDC/SAML).
22. No refresh-token rotation or absolute session cap beyond JWT hours.
23. README drift (AppAccessRequest, Community event) misleads security reviews.

---

## Recommendations for Integration Credentials Ownership

### Principle

**One should own “who can call Lazuar as a tenant” (platform identity & access).**  
**Product modules should own “secrets to call external systems” (MyInvois, payment BYOK, Resend BYOK).**

### Recommended ownership matrix

| Credential type | Owner module | Store | Issue / revoke API | Auth consumer |
|-----------------|--------------|-------|--------------------|---------------|
| Human session (JWT cookie) | **One** | N/A (stateless + stamp) | One auth endpoints | Host JWT bearer |
| Developer / SDK API keys (`sk_live_` / `sk_test_`) | **One** (move from LHDN) | `one.DeveloperApiKeys` (or `one.IntegrationCredentials`) | `/one/workspaces/{id}/api-keys` | Host `ApiKeyAuthenticationMiddleware` |
| Outbound Lazuar→customer webhooks | **One** (already) | `TenantWebhookEndpoint` | Already under One | Dispatcher job |
| MyInvois client id/secret + cert | **LHDN** | `LhdnTenantConfig` | `/lhdn/...` (not `/one/.../lhdn-config`) | LHDN gateway only |
| Payment gateway BYOK | **Payments** | Payments schema | Payments admin APIs | Payments webhooks/adapters |
| Tenant email provider BYOK | **Communications** | `TenantEmailConfiguration.ApiKey` | Communications admin | Email send path |

### Concrete moves

1. **Migrate `DeveloperApiKey` from LHDN → One**  
   - Middleware already treats keys as **platform auth**, not LHDN-specific.  
   - Keep LHDN document APIs requiring OrgAdmin/API_CLIENT + tenant context; stop coupling key storage to tax schema.

2. **Fix TypeSpec:** remove `lhdn-config` from `packages/api-spec/modules/one/`; place under LHDN routes with certificate + credentials together. Implement missing GET/PUT against `LhdnTenantConfig`.

3. **API key scopes (future):** optional claims `scopes: ["lhdn:documents", "webhooks:read"]` stored on One keys so `API_CLIENT` is not unrestricted OrgAdmin-equivalent.

4. **Webhook secrets:** return secret **once on create**; GET returns metadata only (`has_secret`, `created_at`).

5. **Do not put MyInvois secrets in One** — different threat model (outbound government API vs inbound Lazuar API).

6. **Archive workspace cascade:** One publishes `TenantArchivedIntegrationEvent`; LHDN revokes keys; middleware cache flush (already partially on workspace update).

---

## File-by-File Notes

### Domain

| File | Notes |
|------|-------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/One/Domain/GlobalUser.cs` | Solid token + stamp model; no deactivate/MFA; `UserRegistered` orphaned. |
| `Organization.cs` | Clean lifecycle; archive event not integrated. |
| `TenantMembership.cs` | Minimal; mutable role missing; `OrganizationId` setter public (EF). |
| `TenantAppEntitlement.cs` | Toggle only; no revoke event. |
| `WorkspaceInvitation.cs` | Status machine OK; no resend-invite. |
| `TenantWebhookEndpoint.cs` | Secret stored plaintext (HMAC key — expected but sensitive). |
| `WebhookDeliveryOutbox.cs` | Backoff OK; no dead-letter UI beyond logs API. |
| `Rules/OrganizationSlugMustBeValidRule.cs` | Strong reserved list; system insert bypasses via SQL. |
| `Events/*` | Eight domain events; archive + register unused for integrations. |

### Application commands

| File | Notes |
|------|-------|
| `RegisterPublicUserCommand.cs` | Full bootstrap; hardcoded core modules; publishes grants pre-save. |
| `CreateWorkspaceCommand.cs` | No core entitlements unless requested; no authz beyond “user exists”. |
| `UpdateWorkspaceCommand.cs` | ADMIN membership; no slug uniqueness re-check. |
| `ArchiveWorkspaceCommand.cs` | No cascade events. |
| `ToggleAppEntitlementCommand.cs` | Agent-exposed; no system-admin check inside command (endpoint gates). |
| `InviteUserToWorkspaceCommand.cs` | **Membership ≠ ADMIN**; role not validated. |
| `AcceptWorkspaceInvitationCommand.cs` | Email bind OK. |
| `RevokeWorkspaceInvitationCommand.cs` | Proper ADMIN check. |
| `RemoveWorkspaceMemberCommand.cs` | Weak authz; no last-admin guard. |
| `ForgotPasswordCommand.cs` | Silent fail OK. |
| `ResetPasswordCommand.cs` | Stamp rotation OK. |
| `ChangePasswordCommand.cs` | OK. |
| `VerifyEmailCommand.cs` / `ResendVerificationEmailCommand.cs` | Work; not wired to register. |
| `UpdateProfileCommand.cs` | Name only. |
| `SaveWebhookCommand.cs` | Secret only on first create. |

### Application event handlers / queries

| File | Notes |
|------|-------|
| `OrganizationCreatedDomainEventHandler.cs` | Publishes `TenantProvisioned`. |
| `OrganizationUpdatedDomainEventHandler.cs` | Publishes `WorkspaceUpdated` only (not `TenantUpdated`). |
| `GlobalUserProfileUpdatedDomainEventHandler.cs` | CRM sync path. |
| `NotificationDispatchDomainEventHandlers.cs` | Builds client URLs via `IOneLinkService`; password/verify/invite emails. System tenant id `Guid.Empty` for some emails. |
| Agent queries (`Queries/Agent/*`) | Read-only snapshots for Ops tools. |

### Contracts

| File | Notes |
|------|-------|
| `IOneQueryService.cs` | Critical cross-module read port (tenant resolve, role, entitlements, webhooks). |
| `TenantProvisionedIntegrationEvent.cs` | Active. |
| `TenantUpdatedIntegrationEvent.cs` | **Dead publish path**. |
| `WorkspaceUpdatedIntegrationEvent.cs` | Active. |
| `GlobalUserProfileUpdatedIntegrationEvent.cs` | Active. |
| `AppEntitlementGrantedIntegrationEvent.cs` | Active; no revoked twin. |

### Infrastructure

| File | Notes |
|------|-------|
| `OneDbContext.cs` | Schema `one`; no AppAccessRequests; indexes solid. |
| `Endpoints.cs` | Entire HTTP surface; authz inconsistencies; cookie issuer; **no lhdn-config**. |
| `Repositories/OneRepository.cs` | Uses `IgnoreQueryFilters` appropriately for cross-tenant admin ops. |
| `Services/OneQueryService.cs` | Same; webhook secret exposed to callers. |
| `Services/OneLinkService.cs` | `App:ClientUrl` only. |
| `DependencyInjection.cs` | Registers workers, query, outbox bus; subscribes Commerce webhook event. |
| `Workers/SystemGenesisBootstrapperJob.cs` | System org + superadmin upsert. |
| `Workers/OneOutboxPublisherJob.cs` / `OneInboxConsumerJob.cs` | Thin wrappers. |
| `Workers/OutboundWebhookDispatcherJob.cs` | Poll 10s; HMAC hex signature header `X-Lazuar-Signature`. |
| `EventHandlers/OutboundWebhookEventHandlers.cs` | Match endpoint by org + URL. |
| `Configuration/PlatformAdminSettings.cs` | Env-bound in Program.cs. |
| `Migrations/20260627124757_InitialOneSchema.cs` | Full current schema. |
| `Migrations/20260704104342_DropLegacySchemas.cs` | Drops community/vault schemas (platform cleanup). |
| `README.md` | **Out of date** vs code (AppAccessRequest, Community consumer, missing webhooks). |

### API Spec

| File | Notes |
|------|-------|
| `packages/api-spec/modules/one/models.tsp` | Auth, workspace, webhook, storage, **LHDN config models misplaced**. |
| `packages/api-spec/modules/one/routes.tsp` | Full One surface + **unimplemented lhdn-config**. |
| `packages/api-spec/docs-one.tsp` | Core product OpenAPI bundle (One + Billing). |

### BuildingBlocks (auth-related)

| File | Notes |
|------|-------|
| `JwtService.cs` | Minimal HS256 generator. |
| `PasswordService.cs` | BCrypt only. |
| `MagicLinkTokenService.cs` | Subscription magic links; not CIAM. |
| `IMagicLinkTokenService.cs` | Misleading name for platform-wide reuse. |
| `TokenGeneratorService.cs` | Shared hashing for One tokens & LHDN API keys. |
| `PlatformDbContext.cs` | Tenant filter + domain event recursion + job trigger. |

### Host middleware / related modules

| File | Notes |
|------|-------|
| `ApiKeyAuthenticationMiddleware.cs` | Hard dependency on LHDN schema — primary credentials ownership smell. |
| `TenantSecurityMiddleware.cs` | Role injection; admin routes require tenant header. |
| `ExecutionContextAccessor.cs` | TenantId from Items; system admin from claim. |
| `Modules/Lhdn/.../DeveloperApiKey.cs` + `GenerateApiKeyCommand.cs` | Current API key lifecycle. |
| `Modules/Lhdn/.../LhdnTenantConfig.cs` | External tax credentials (should stay LHDN). |
| `Modules/Lhdn/Infrastructure/Endpoints.cs` | API keys + cert; no MyInvois config GET/PUT in HTTP list. |
| `Modules/Commerce/.../PublicEndpoints.cs` | Portal magic link validation. |

---

## Executive Gap Summary

| Area | Maturity | Headline gap |
|------|----------|--------------|
| Global user + password auth | **MVP solid** | No MFA, rate limits, stamp-on-every-request, legacy upgrade |
| Workspace provisioning | **MVP solid** | Archive incomplete; onboarding queue missing |
| Memberships / invites | **Partial** | Authz holes (IDOR, non-admin invite/remove) |
| App entitlements | **MVP** | Grant-only events; superadmin-only HTTP toggle |
| Developer API keys | **Wrong module** | Owned by LHDN; should be One platform credentials |
| LHDN config in One API | **Spec drift** | TypeSpec without implementation; belongs in LHDN |
| Magic link login | **N/A in One** | Commerce-only subscriber links |
| README / events docs | **Stale** | AppAccessRequest + Community event fictional |
| Cross-module archive/update | **Incomplete** | `TenantUpdated` dead; archive silent |

This is the current state of **Lazuar One** as CIAM + workspace registry: strong skeleton for multi-tenant identity, with the largest structural debt in **authorization consistency**, **API-key ownership**, **spec/implementation drift on LHDN config**, and **lifecycle events for archive/revoke**.
