# 05 — Lazuar Pay One: identity, workspaces, roles, API keys, audit (after Waves 1–4)

**Date:** 16 August 2026  
**Codebase:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Branch named in parent:** `feat/007-waves-1-4-implement`  
**Product slice:** One (CIAM + workspace registry + platform credentials) plus host auth (`AuthAndCorsExtensions`, `TenantSecurityMiddleware`, `ApiKeyAuthenticationMiddleware`) plus ops Team / Audit pages.  
**This file is evidence.** It is not a plan, not an implementation, and not a substitute for reading the files it cites. Do not collapse it into a bullet list and throw the citations away.

Parent judgment: [00-evaluation.md](./00-evaluation.md) (when written). Sibling slices: commerce (01), payments (02), ledger (03), LHDN (04), comms (06), frontends (07), contracts (08), architecture (09), honesty (10).

These pages evaluate **the tree as it is after Waves 0–4**. `plans/007-feats` is historical research plus the wave ticket notes. Tracker cells are cited only when this report re-checked the code.

---

## 0. Method

### What was read (Pay repo, 2026-08-16)

Absolute paths unless noted.

| Concern | Path |
|---------|------|
| Wave parent + identity ticket IDs | `plans/008-evals/README.md`, `plans/007-feats/00-implement-ids.md`, `plans/007-feats/impl/W1-LP-006-done.md`, `W1-LP-184-done.md`, `W3-LP-166-done.md`, `W3-LP-167-done.md` |
| Stale pre-wave gap note (do not treat as current) | `docs/001-gaps/10-one-identity-module.md` |
| One HTTP surface | `Modules/One/Infrastructure/Endpoints.cs` + `Endpoints/*.cs` |
| Register / login / cookie | `Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs` |
| Register command | `Modules/One/Application/Commands/RegisterPublicUserCommand.cs` |
| Pricing query | `Modules/One/Application/Queries/GetPublicPricingQuery.cs`, `Infrastructure/Queries/GetPublicPricingQueryHandler.cs` |
| Rate limiter | `Modules/One/Infrastructure/Services/PublicRegisterRateLimiter.cs` |
| Workspace commands | `CreateWorkspaceCommand.cs`, `UpdateWorkspaceCommand.cs`, `ArchiveWorkspaceCommand.cs` |
| Invite / accept / remove / revoke | `InviteUserToWorkspaceCommand.cs`, `AcceptWorkspaceInvitationCommand.cs`, `RemoveWorkspaceMemberCommand.cs`, `RevokeWorkspaceInvitationCommand.cs` |
| Role allow-list | `Modules/One/Domain/WorkspaceStaffRoles.cs` |
| Membership / org / user | `TenantMembership.cs`, `Organization.cs`, `GlobalUser.cs`, `WorkspaceInvitation.cs` |
| Policy catalog | `src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs` |
| Tenant middleware | `src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs` |
| API key middleware | `src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs` |
| Pipeline order | `src/Lazuar.Api/Composition/MiddlewarePipelineExtensions.cs` |
| Endpoint map | `src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs` |
| Ambient context | `src/Lazuar.Api/ExecutionContextAccessor.cs` |
| Scopes | `Modules/One/Domain/PlatformApiScopes.cs` |
| Credentials | `ApiCredential.cs`, `GenerateApiCredentialCommand.cs`, `RevokeApiCredentialCommand.cs`, `ApiCredentialEndpoints.cs`, `ApiCredentialService.cs` |
| Audit | `AuditEvent.cs`, `IAuditRecorder.cs`, `AuditRecorder.cs`, `Migrations/20260820150000_AddAuditEvents.cs` |
| Invite email URL | `NotificationDispatchDomainEventHandlers.cs`, `OneLinkService.cs` |
| Ops funnel + Team + Audit | `apps/lazuar-ops/src/App.tsx`, `LoginPage.tsx`, `PricingPage.tsx`, `Sidebar.tsx`, `modules/workspace/pages/TeamPage.tsx`, `AuditLogPage.tsx`, `ApiKeysPage.tsx`, `lib/api-client.ts`, `modules/core/components/PageLayout.tsx` |
| Buyer legal pages reused as clickwrap | `apps/lazuar-portal/src/app/legal/{terms,privacy,refund}/page.tsx` |
| TypeSpec | `packages/api-spec/modules/one/routes.tsp`, `models/auth.tsp` |
| Tests | `tests/Lazuar.ModuleTests/One/*`, `Commerce/CommerceEndpointsAuthorizationTests.cs`, `ArchitectureTests/TenantIsolationArchitectureTests.cs` |
| Admin (platform) | `PlatformAuthEndpoints.cs`, `apps/lazuar-admin/src/App.tsx` |
| Ops chat policy leftover | `Modules/Ops/Infrastructure/Endpoints.cs` |
| Commerce / billing / comms policy attach | `Modules/Commerce/Infrastructure/Endpoints.cs`, `SubscriberEndpoints.cs`, `PaymentConfigEndpoints.cs`, `Modules/Billing/Infrastructure/Endpoints.cs`, `Modules/Communications/Infrastructure/Endpoints.cs` |
| README (partially stale) | `Modules/One/README.md` |

### Wave tickets that actually touched this slice

| Ticket | Wave | Job | What the code now does |
|--------|------|-----|------------------------|
| **LP-006** | 1 | Public signup + pricing page | `GET /one/public/pricing`, ops `/pricing` + `/signup`, `accepted_terms`, register rate limit 10 / 10 min |
| **LP-184** | 1 | Extra self-serve workspace | `POST /one/workspaces` any authenticated human; empty-entitlement Ops is “Create your workspace”, not “Access Denied” |
| **LP-131 / LP-137** | 1 | Key scopes + commerce M2M policies | `PlatformApiScopes` closed catalog; `Integration*` policies; humans ADMIN bypass most M2M policies except `IntegrationPaymentsMe` |
| **LP-166** | 3 | Staff roles beyond admin | `ADMIN` / `MEMBER` / `VIEWER` allow-list; `OrgMember` / `OrgRead`; Team page |
| **LP-167** | 3 | Audit log | `one.AuditEvents` + `IAuditRecorder` + ops `/workspace/audit` |

Wave 2 did not own identity. Wave 4 did not own identity (rails wrap). Wave 0 did not own identity.

The pre-wave gap note at `docs/001-gaps/10-one-identity-module.md` is **wrong as of this tree** on several headline claims: API keys are no longer LHDN-owned; `OrgAdmin` no longer includes `API_CLIENT`; invite role is no longer a free string; members/invites listing is no longer “auth only”; webhook GET no longer returns the full secret; public pricing and register clickwrap exist. That file is cited below only as a baseline of what Waves 1–4 were supposed to fix.

---

## 1. What One is after Waves 1–4

One is still the **global identity and tenant registry**. It is not the buyer portal (Commerce magic links), not the platform-admin product (cookie `lazuar_admin_auth` on `/api/v1/platform`), and not the merchant console itself (`lazuar-ops` is a SPA that consumes One).

### 1.1 Aggregates that exist in `one` schema

`OneDbContext` (`Modules/One/Infrastructure/OneDbContext.cs`) maps:

| DbSet | Table | Tenant-scoped? |
|-------|-------|----------------|
| `Organizations` | `one.Organizations` | No (root) |
| `GlobalUsers` | `one.GlobalUsers` | No |
| `TenantMemberships` | `one.TenantMemberships` | Yes |
| `TenantAppEntitlements` | `one.TenantAppEntitlements` | Yes |
| `WorkspaceInvitations` | `one.WorkspaceInvitations` | Yes |
| `TenantWebhookEndpoints` | `one.TenantWebhookEndpoints` | Yes |
| `WebhookDeliveryOutboxes` | `one.WebhookDeliveryOutboxes` | Yes |
| `ApiCredentials` | `one.ApiCredentials` | Yes |
| `AuditEvents` | `one.AuditEvents` | Yes (`IMustHaveTenant`) |
| Outbox / Inbox | `one.OutboxMessages` / `one.InboxMessages` | Platform infra |

There is **no** `AppAccessRequest` type or DbSet. `RegisterPublicUserCommandHandlerTests.Handler_And_OneDbContext_Have_No_AppAccessRequest` locks that. The README still talks about public register correctly (`Modules/One/README.md` lines 10–11) but still documents membership role as `ADMIN` / `CLIENT` (line 22) and still claims One “may grant a `CLIENT` membership” for portal access after payment (lines 33–34). **No such consumer exists in this tree.** `UserRegisteredDomainEvent` is raised in `GlobalUser` ctor (`GlobalUser.cs:44`) and has **zero handlers**.

### 1.2 HTTP map (implemented)

`MapOneEndpoints` (`Endpoints.cs:9–23`) mounts under `/api/v1` (`ModuleRegistrationExtensions.cs:67–69`):

| Method | Path | Auth as coded |
|--------|------|----------------|
| GET | `/one/public/pricing` | Anonymous |
| POST | `/one/public/register` | Anonymous + `accepted_terms` + rate limit |
| POST | `/one/auth/login` | Anonymous |
| POST | `/one/auth/logout` | Anonymous (deletes cookie) |
| POST | `/one/auth/forgot-password` | Anonymous |
| POST | `/one/auth/reset-password` | Anonymous |
| POST | `/one/auth/verify-email` | `RequireAuthorization` |
| POST | `/one/auth/resend-verification` | Anonymous |
| GET | `/one/auth/me` | `RequireAuthorization` + stamp check |
| PUT | `/one/me/profile` | Auth |
| PUT | `/one/me/security/password` | Auth |
| GET | `/one/me/entitlements` | Auth |
| POST | `/one/workspaces` | Auth (any human with `UserId`) |
| GET | `/one/workspaces` | `OrgAdmin` **and** `IsSystemAdmin` |
| GET/PUT/DELETE | `/one/workspaces/{id}` | Auth + command/query membership |
| GET | `/one/workspaces/{id}/members` | Auth + `HasTenantAccess` |
| POST | `/one/workspaces/{id}/invites` | `OrgAdmin` + command `CanManageMembers` |
| GET | `/one/workspaces/{id}/invites` | Auth + `HasTenantAccess` |
| DELETE | `/one/workspaces/{id}/invites/{inviteId}` | `OrgAdmin` + command |
| DELETE | `/one/workspaces/{id}/members/{userId}` | `OrgAdmin` + command |
| POST | `/one/workspaces/invites/accept` | Auth |
| GET | `/one/workspaces/{id}/audit` | Auth + `HasTenantAccess` (or system admin) |
| GET/POST | `/one/workspaces/{id}/apps`… | Superadmin + `OrgAdmin` |
| GET/POST/PUT/DELETE | `/one/workspaces/{id}/webhooks`… | Custom `CanAccessWorkspaceWebhooksAsync` |
| POST | `/one/storage/presigned-url` | Auth + required tenant |
| GET/POST/DELETE | `/one/api-keys` | `OrgAdmin` + required tenant |
| POST | `/one/integrations/workspaces/provision` | Provision secret or SUPER_ADMIN JWT |
| GET | `/public/one/{tenantSlug}/branding` | Anonymous |
| POST/GET | `/platform/auth/*` | Separate cookie realm |

TypeSpec `packages/api-spec/modules/one/routes.tsp` matches this surface for the identity routes listed above, including `GET /workspaces/{id}/audit` (lines 154–159) and `POST /workspaces/invites/accept` (lines 161–166). LHDN config is **no longer** on One routes (comment at `routes.tsp:184`). Spec still marks authenticated routes `@useAuth(BearerAuth)` while the live session is an HttpOnly cookie; Bearer still works if a client sends the JWT.

### 1.3 Two cookie realms

`AuthAndCorsExtensions.AddLazuarAuthentication` (`AuthAndCorsExtensions.cs:52–64`) picks cookie by path:

- `/api/v1/platform*` → `lazuar_admin_auth`
- everything else → `lazuar_auth`

Ops login issues `lazuar_auth` (`AuthEndpoints.IssueCookie`, lines 185–216). Platform login issues `lazuar_admin_auth` with `Path = /api/v1/platform` (`PlatformAuthEndpoints.cs:135–145`). They share the same JWT issuer/audience/secret. Production refuses the default secret (`AuthAndCorsExtensions.cs:22–29`). Repo `appsettings.json` has `"Jwt:Secret": ""` (line 24) so non-Production falls back to `secure_development_key_minimum_32_characters_long` (`AuthAndCorsExtensions.cs:14, 31` and `AuthEndpoints.cs:188`).

---

## 2. Public register, pricing, TOS, rate limit

This is the Wave 1 GTM surface. Before LP-006 a stranger could call `POST /one/public/register` but could not see a public price card or a `/signup` URL. That is no longer true.

### 2.1 Public pricing

**API.** `GET /api/v1/one/public/pricing` is anonymous (`AuthEndpoints.cs:26–30`). Handler `GetPublicPricingQueryHandler`:

- Hard-codes `GmvTakePercent = 0` (`GetPublicPricingQueryHandler.cs:15, 52`). Tests plant a fake 5% config key and still assert 0 (`GetPublicPricingQueryHandlerTests.cs:54–68`).
- Packs and starter grant come from `ICreditCostService` (lines 28–34, 53), same source as the wallet.
- Hub plan from `Saas:Plan` (`appsettings.json:86–93`): code `hub_starter`, name `Hub Starter`, **`AmountMyr: 0`**, interval `mo`, currency `MYR`.
- SST from `Saas:Seller` (`appsettings.json:94–101`): `SstRate: 0`, reason `"Supplier not SST-registered"`. Handler builds `sst_note` as `SST {rate}% — {reason}. Confirm with your accountant. We do not add SST at checkout today.` (`GetPublicPricingQueryHandler.cs:41–48`).
- Honesty flags are **constants in code**, not config: `Lhdn_credits_live = false`, `Whatsapp_credits_live = false` (lines 58–59). Even if `Messaging:WhatsAppEnabled` is true in a test config, the DTO still says WhatsApp is not billed live (`GetPublicPricingQueryHandlerTests` plants that flag).
- `Checkout_is_free` is `planAmount <= 0` (line 57).

Path is tenant-exempt (`TenantSecurityMiddleware.cs:134–135`; architecture test `TenantIsolationArchitectureTests.cs:103`).

**Ops page.** `apps/lazuar-ops/src/components/PricingPage.tsx` is a public route (`App.tsx:210`). Logged-out `/` goes here (`HomeRedirect`, `App.tsx:176–203`). Copy is honest: “RM 0 on your sales”, “not Merchant of Record”, “not a licensed acquirer”, “not a KYC bureau”, LHDN UI not live, WhatsApp not connected. Fallback constants match repo config (`PricingPage.tsx:8–31`) so a dead API still shows 0% GMV rather than inventing a take-rate.

**What it is not.** There is no marketing site. There is no `www`. Docs VitePress is integrator guides. Portal `/` is the buyer lock icon. Admin has no pricing. The company homepage **is** the ops SPA.

### 2.2 Public register

**API.** `POST /api/v1/one/public/register` (`AuthEndpoints.cs:32–73`):

1. Requires email, password, `workspace_name`, `tenant_slug` (lines 40–45).
2. Requires `accepted_terms == true` (lines 47–48). TypeSpec field is optional (`models/auth.tsp:14–15`); the **handler** is the enforcement, not the schema.
3. Rate-limits (see 2.4).
4. Sends `RegisterPublicUserCommand`.
5. Issues `lazuar_auth` via `IssueCookie`.
6. Returns `LoginResponse` with **body role `"ADMIN"`** (`AuthEndpoints.cs:69–71`).

**Command** (`RegisterPublicUserCommand.cs:34–75`):

1. Normalizes email and slug to lower-case.
2. Rejects duplicate email (`already exists`).
3. Rejects taken slug (`already taken`).
4. Constructs `Organization` **before** tracking the user (`lines 54–58`) so reserved/malformed slugs throw `BusinessRuleValidationException` and write nothing. Tests cover `admin`, `portal`, `system`, `billplz`, `ab`, `acme--corp`, `-acme` (`RegisterPublicUserCommandHandlerTests.cs:164–197`).
5. Creates `GlobalUser` unverified, not system admin.
6. Membership role **`"ADMIN"`** (line 61).
7. Entitlements hardcoded `OPS`, `BILLING`, `PAYMENTS`, `CRM`, `LHDN` (lines 22, 64–70). **Not `COMMERCE`.** Tests lock that (`RegisterPublicUserCommandHandlerTests.cs:107–108`). Commerce still works because commerce HTTP is not entitlement-gated in the host; entitlements are a registry / seed hook (`AppEntitlementGrantedIntegrationEvent` published per module).
8. Does **not** call `SetEmailVerificationToken`. `UserRegisteredDomainEvent` has no handler. Email verification is opt-in via `POST /auth/resend-verification`.

**Ops form.** `/signup` renders `LoginPage` in signup mode (`App.tsx:211`; `LoginPage.tsx:15–16, 201–318`). Fields: workspace name, slug (shared `slugify` / `validateSlug`), email, password, confirm, TOS checkbox. After 200 it hard-navigates to `/commerce/dashboard` (or `returnUrl`). Name sent to the API is the email local-part (`LoginPage.tsx:112`).

**What register still does not do.**

- No password complexity (length, breach list, zxcvbn). `PasswordService` is BCrypt only (`BuildingBlocks/Infrastructure/PasswordService.cs:15–16`), work factor 11 (`appsettings.json:32–34`).
- No CAPTCHA.
- No KYC, card, or phone.
- No approval queue.
- No email-verify gate on login (login never checks `IsEmailVerified`).
- No audit row for `user.registered` / `workspace.created`.

### 2.3 TOS / Privacy clickwrap

Ops signup checkbox (`LoginPage.tsx:275–294`) links to:

- `/portal/legal/terms`
- `/portal/legal/privacy`

Those are **buyer** pages on `lazuar-portal`, last-updated June/August 2026:

- `terms/page.tsx` is written for “you” the **purchaser of a Creator’s product**. Section 1: Lazuar is not a party to the Creator transaction. Section 3 claims **99.9% uptime** with no SLA document. Section 4 describes **buyer magic-link** access, not merchant password login.
- `privacy/page.tsx` says the **Creator** is the PDPA/GDPR controller and Lazuar is the processor. Sub-processors listed: Resend, Meta (WhatsApp), Cloudflare. “Creators can anonymize a buyer from Subscribers → Anonymize.” That is a buyer-PII story.
- `refund/page.tsx` is “we cannot refund you, talk to the Creator.”

The checkbox copy admits the gap: “Platform use is covered by these pages until a merchant MSA exists.” (`LoginPage.tsx:292`). There is **no** merchant DPA, no acceptable-use for the Hub tenant, no data-processing addendum, no “we process your staff emails as controller for the SaaS account.” A lawyer will not treat buyer TOS as a merchant contract. The boolean `accepted_terms` is stored **nowhere** — it is a request-time gate only. We cannot later prove *which* version a tenant accepted.

### 2.4 Rate limit

`PublicRegisterRateLimiter` (`PublicRegisterRateLimiter.cs`):

- Token bucket, 10 tokens / 10 minutes (`Limit = 10`, `Window = 10 minutes`, lines 14–15, 26–34).
- Key is `email:{email}|ip:{ip}` (`AuthEndpoints.ResolveRegisterClientKey`, lines 169–183).
- IP prefers first `X-Forwarded-For` hop (lines 172–178). If the process is not behind a trusted proxy that overwrites that header, a caller can mint a new bucket per spoofed IP.
- Empty key returns **allow** (`TryAcquireAsync` lines 21–24).
- 429 body is ProblemDetails; `Retry-After: 600` (`AuthEndpoints.cs:53–61`).
- In-process `ConcurrentDictionary`. Multi-instance / restart resets the budget. This is hygiene, not an edge WAF.
- Tests: 11th acquire denied; a different email+IP is independent (`PublicRegisterRateLimiterTests.cs:10–21`).

**Login has no rate limit.** `POST /one/auth/login` (`AuthEndpoints.cs:75–101`) is unauthenticated, unlimited, and returns `400` with embedded `Status = 401` on bad password (lines 88–90). Forgot-password and resend-verification are also unlimited (enumeration is partially mitigated by silent no-op on missing users — `ForgotPasswordCommand.cs:24`, `ResendVerificationEmailCommand.cs:28–29`).

Integrator provision has its **own** limiter (`IntegrationProvisionEndpoints.cs:81–114`). That is not the public signup limiter.

### 2.5 Verdict on the public funnel

A stranger can open Hub `/pricing`, read an honest 0% GMV card, click through clickwrap that is the **wrong legal object**, create a live workspace, and land on the commerce dashboard with an ADMIN membership. That is a sellable **time-to-first-workspace**. It is not a sellable **trust** surface (TOS, 99.9%, no MSA, no login lockout, no verify). Compared to Stripe/HitPay/Paddle public signup, we now have the URL. We do not have the contract.

---

## 3. Workspace create

There are four create paths. They are not equivalent.

### 3.1 Path A — Public register (workspace #1)

Covered above. Always ADMIN + five core entitlements. Cookie issued.

### 3.2 Path B — `POST /one/workspaces` (workspace #2+)

`WorkspaceEndpoints.cs:19–26`: `RequireAuthorization()`, reject empty `UserId`, send `CreateWorkspaceCommand`.

Handler (`CreateWorkspaceCommand.cs:34–67`):

- Requires the user row to exist. **Does not require any existing membership.** LP-184’s point: a human with zero entitlements can still create.
- Slug unique + `Organization` ctor rules.
- Owner membership **`ADMIN`** (line 52).
- Entitlements **only** for `ProvisionApps` the client sent (lines 55–62). Register hard-codes the five; this path is caller-defined.

Ops `CreateWorkspaceModal` always sends `provision_apps: ["OPS", "BILLING", "PAYMENTS", "CRM", "LHDN"]` (`CreateWorkspaceModal.tsx:32–37`). A raw API caller can send `[]` and get a workspace with **no** entitlements. Ops empty-entitlement state would then bounce them back to “Create your workspace” only if `/me/entitlements` is empty — but `/me/entitlements` lists **memberships**, not app entitlements (`WorkspaceEndpoints.cs:162–163`). Zero app entitlements still produces a row. The empty state is “no memberships”, not “no apps.”

`CreateWorkspaceCommandHandlerTests.Authenticated_User_With_Zero_Memberships_Creates_Admin_Workspace` locks the zero-membership case (`CreateWorkspaceCommandHandlerTests.cs:59–78`). `WorkspaceCreateAuthorizationTests` is a **source-string** test that `MapPost("/workspaces")` has `RequireAuthorization` (`WorkspaceCreateAuthorizationTests.cs:11–17`) — it does not spin up the host.

No plan gate. No SaaS invoice. No credit-card. No cap on how many workspaces one email can mint. Abuse control is the register rate limit (path A) and “be authenticated” (path B). Path B is not rate-limited.

### 3.3 Path C — Empty-entitlement Ops UI

`App.tsx:126–132`: if `/one/auth/me` succeeds and `/one/me/entitlements` is `[]`, render `EmptyWorkspaceState` instead of “Access Denied.” That page (`EmptyWorkspaceState.tsx`) mounts the same modal. LP-184 done note is accurate.

`GET /me/entitlements` (`WorkspaceEndpoints.cs:141–165`):

- Normal user: memberships ⋈ organizations, `Role = m.Role`.
- **System admin:** every **active** organization, synthetic role `"SUPER_ADMIN"` (lines 146–159). A platform operator opening ops therefore sees the entire tenant directory in the workspace switcher. That is a support feature and a data-exposure feature.

Switcher lives in `PageLayout.tsx:69–109` (“Create New Workspace” at lines 101–106). Sidebar does not switch workspaces.

### 3.4 Path D — Integrator provision

`POST /one/integrations/workspaces/provision` (`IntegrationProvisionEndpoints.cs`). Auth is `X-Lazuar-Provision-Key` / Bearer provision secret **or** SUPER_ADMIN JWT (`IntegratorProvisionAuth`). Tenant-exempt. Rate-limited globally and per external org. Creates (or reuses) a workspace bound to `(ExternalProduct, ExternalOrgId)`, optional owner attach, optional `sk_test_`/`sk_live_` key, optional webhook. This is the Aura Connect hatch, not the human signup path. Owner attach is the **only** way today to add a human to a workspace without the broken invite UI (see §7). Provision returns `plain_key` and webhook `secret_key` **once**.

### 3.5 Update / archive

`UpdateWorkspaceCommand` (`UpdateWorkspaceCommand.cs:29–49`): membership must be **exactly** `"ADMIN"`. Workspace `SUPER_ADMIN` membership **cannot** update (string compare, line 32). System admin with no membership cannot update through this command. Slug format re-checked if changed (`Organization.UpdateDetails`, `Organization.cs:51–54`). **Slug uniqueness is not re-checked.** `IsSlugUniqueAsync` is only called from register, create, and provision. Two workspaces can collide on update until the unique index on `Organizations.Slug` (`OneDbContext.cs:49`) throws at save.

Branding (`logo_url`, `primary_color`) updates without raising `OrganizationUpdatedDomainEvent` (`Organization.cs:63–68`). Name/slug updates do raise it → `WorkspaceUpdatedIntegrationEvent`.

`ArchiveWorkspaceCommand` (`ArchiveWorkspaceCommand.cs:23–38`): same exact `"ADMIN"` check. Sets `IsActive = false`, raises `OrganizationArchivedDomainEvent`. **No handler exists** (grep: only the record and `Organization.Archive`). No integration event. No key revoke. No membership wipe. `HasTenantAccessAsync` does not consult `Organization.IsActive` (`OneQueryService.cs:72–78`). An archived workspace remains fully readable (members, invites, audit) and, if the client still sends its id as `X-Tenant-Id`, still injectable. Archive is a boolean, not a lifecycle.

### 3.6 System tenant

`SystemGenesisBootstrapperJob` raw-SQL upserts id `00000000-0000-0000-0000-000000000001`, slug `system` (`SystemGenesisBootstrapperJob.cs:49–53`), bypassing the reserved-slug rule (`OrganizationSlugMustBeValidRule` includes `"system"` at line 12). Superadmins from `PLATFORM_ADMIN_EMAILS` / `PLATFORM_ADMIN_PASSWORD` are upserted, password rotated if env hash does not verify (lines 75–79), elevated with raw SQL (lines 82–87), and granted membership role `"SUPER_ADMIN"` on the system org (lines 90–100). Every boot can rotate the superadmin password from env. That is convenient and a foot-gun.

`/api/v1/platform` forces `TenantId` to that system GUID (`TenantSecurityMiddleware.cs:29–33`) and requires JWT role `SUPER_ADMIN` (`ModuleRegistrationExtensions.cs:80–82`).

---

## 4. JWT `CLIENT` vs membership `ADMIN` / `MEMBER` / `VIEWER`

This is the most important identity fact in the product. Waves 1–4 **did not remove the dual model**. They added a third membership role and taught the middleware to inject it.

### 4.1 Layer 1 — JWT / cookie role (global)

`IssueCookie` (`AuthEndpoints.cs:193–201`) always writes:

```
ClaimTypes.Role = user.IsSystemAdmin ? "SUPER_ADMIN" : "CLIENT"
is_system_admin = bool
is_email_verified = bool
security_stamp = Guid
NameIdentifier = user.Id
Email = user.Email
```

Login **response** body uses the same SUPER_ADMIN / CLIENT split (`AuthEndpoints.cs:93, 97–100`). Register **response** body says `"ADMIN"` (`line 71`) while the cookie still says `"CLIENT"`. That mismatch is unchanged from the pre-wave gap note.

`/auth/me` (`AuthEndpoints.cs:155`) returns `principal` role if present, else the same SUPER_ADMIN/CLIENT fallback. After TenantSecurityMiddleware has injected a membership role, `/auth/me` can return `ADMIN` / `MEMBER` / `VIEWER` **if** the request also carried `X-Tenant-Id`. Ops `verifySession` calls `/one/auth/me` **without caring about the role string** (`App.tsx:65–70`). Sidebar user chip shows name/email, not role (`Sidebar.tsx:288–290`).

TTL is `Jwt:ExpiryHours` default 24 (`appsettings.json:27`, `AuthEndpoints.cs:191`). Cookie: HttpOnly, Secure outside Development, SameSite=Lax, Domain `.lazuar.com` outside dev (`AuthEndpoints.cs:206–213`). No refresh token. No server-side session table. Logout deletes the cookie only (`AuthEndpoints.cs:103–107`). Security stamp rotates on password change (`GlobalUser.ChangePassword` / `ResetPassword`, `GlobalUser.cs:55–58, 71–77`) but is **checked only on `/auth/me` and platform `/auth/me`** (`AuthEndpoints.cs:148–153`, `PlatformAuthEndpoints.cs:91–96`). A stolen cookie works on every other route until expiry.

### 4.2 Layer 2 — Membership role (workspace)

`TenantMembership.Role` is still a free string on the entity (`TenantMembership.cs:10` comment still says `"ADMIN", "CLIENT"`). The **invite path** now allow-lists via `WorkspaceStaffRoles` (`WorkspaceStaffRoles.cs:1–28`):

| Constant | Value | Who can hold it |
|----------|-------|-----------------|
| `Admin` | `ADMIN` | Owner-shaped. Invite, remove, keys, gateways, legal, email BYOK, billing admin, workspace update/archive |
| `Member` | `MEMBER` | Commerce operate (products, subscribers, refunds, dunning, coupons) |
| `Viewer` | `VIEWER` | GET / list |
| `SuperAdmin` | `SUPER_ADMIN` | Recognized by `CanManageMembers`; **not** invitable (`NormalizeInvitedRole` rejects it) |

`NormalizeInvitedRole` throws `"Role must be ADMIN, MEMBER, or VIEWER."` (`WorkspaceStaffRoles.cs:12–20`). Tests reject `HACKER`, `banana`, **`CLIENT`** (`InviteUserToWorkspaceCommandHandlerTests.cs:46–65`). `CLIENT` is therefore **not** a staff role. It remains the JWT role for every non-system human.

`CanManageMembers` is `ADMIN` or `SUPER_ADMIN` (`WorkspaceStaffRoles.cs:23–27`). Invite, revoke invite, and remove use that (plus `IsSystemAdmin` on the user row).

There is **no** `ChangeRole` method, no transfer-ownership, no last-admin guard, no unique-owner column. Role is write-once at membership create.

### 4.3 Layer 3 — Machine role

`ApiKeyAuthenticationMiddleware` sets `ClaimTypes.Role = API_CLIENT`, `NameIdentifier = "api_client"`, `TenantId` from the credential row, `IsTestMode` from the **presented token prefix**, plus one `scope` claim per stored scope (`ApiKeyAuthenticationMiddleware.cs:70–93`). Humans cannot become `API_CLIENT`. Keys cannot become `ADMIN`.

### 4.4 How the layers collide

Ops always attaches `X-Tenant-Id` from `localStorage.ops_active_workspace_id` (`api-client.ts:13–24`). Middleware then **adds** the membership role as a second `ClaimTypes.Role` (`TenantSecurityMiddleware.cs:83–88`). `IsInRole("ADMIN")` becomes true for an owner even though the JWT still says `CLIENT`.

If the header is missing (curl, a future SPA, Scalar “try it”):

- JWT role is `CLIENT`.
- `OrgAdmin` / `OrgMember` / `OrgRead` all **fail** (`RequireRole` does not include `CLIENT`).
- `RequireAuthorization()`-only routes still work (profile, create workspace, accept invite, GET members if the handler’s `HasTenantAccess` passes).

That is why “I am ADMIN of my workspace” and “my JWT says CLIENT” are both true. Docs and `/auth/me` will keep confusing integrators until someone stops putting the global role and the workspace role on the same claim type.

`ExecutionContextAccessor.UserRole` returns **one** `ClaimTypes.Role` (`ExecutionContextAccessor.cs:38`) — whichever the principal finds first. Do not use `UserRole` as the membership role.

`Modules/Ops/Infrastructure/Endpoints.cs:10` still authorizes ops-chat with `RequireRole("CLIENT", "ADMIN")`. That matches the **JWT** role, not membership. A VIEWER (JWT still `CLIENT`) would pass this policy if ADR 023 remounted the chat. Membership VIEWER is not represented here.

---

## 5. `OrgAdmin` / `OrgMember` / `OrgRead`

`AddLazuarAuthorizationPolicies` (`AuthAndCorsExtensions.cs:71–183`) is the catalog. Policy names are a shared contract; the comment at lines 8–10 says do not rename lightly.

### 5.1 Human policies (Wave 3)

| Policy | Roles | Intended job |
|--------|-------|----------------|
| `OrgAdmin` | `SUPER_ADMIN`, `ADMIN` | Keys, certs, payment/email config, member admin, billing wallet, communications admin |
| `OrgMember` | those + `MEMBER` | Operate commerce: products, subscribers, refunds, record-pay, dunning, coupons, custom checkout |
| `OrgRead` | those + `VIEWER` | GET / list |

`OrgAdmin` **no longer includes `API_CLIENT`**. That is a deliberate Wave 1/3 fix. Pre-wave `docs/001-gaps/10-one-identity-module.md` line 214 is obsolete. Keys cannot mint keys.

`SUPER_ADMIN` in these policies is the **JWT** role (system admin) **or** a membership role string if someone stored `SUPER_ADMIN` on a membership (genesis does, on the system org). A platform operator with JWT `SUPER_ADMIN` passes `OrgAdmin` even without a membership **on routes that do not require tenant membership**. On `/admin/*` the middleware **does** require membership (see §6) and will 403 a superadmin who is not a member of the header tenant — unless they use the synthetic entitlements list and switch into a workspace. Genesis only auto-memberships them to the **system** org, not to customer orgs. Superadmin support of a customer tenant therefore depends on `/me/entitlements` listing every org (it does, for system admins) **and** on middleware injecting… wait.

System admin `/me/entitlements` returns role `"SUPER_ADMIN"` as a DTO field. It does **not** create a `TenantMembership` row. When they switch to customer org X and send `X-Tenant-Id: X`:

- `GetTenantRoleAsync` finds **no** membership (`OneQueryService.cs:80–88`).
- Middleware: authenticated, role empty, path is `/admin/...` (not exempt) → **403** (`TenantSecurityMiddleware.cs:90–103`).

So the synthetic entitlements list is a trap: ops will offer every workspace in the switcher, then every `/admin/*` call 403s unless a real membership exists. System admins can still hit One routes that check `ctx.IsSystemAdmin` directly (list all workspaces, toggle apps, webhook manage, GET members). They **cannot** operate commerce as a support user without a membership row. That is probably correct and currently undoc’d.

### 5.2 Where the human policies attach

**Commerce** (`Modules/Commerce/Infrastructure/Endpoints.cs:23`): the admin group default is **`OrgRead`**. Mutations re-declare:

- Products POST/PUT/DELETE → `OrgMember` (`ProductEndpoints.cs`)
- Subscribers mutations (cancel, record payment, enroll, dunning pause/resume) → `OrgMember`; **anonymize → `OrgAdmin`** (`SubscriberEndpoints.cs:279`)
- Transactions refund → `OrgMember` (`TransactionEndpoints.cs:116`)
- Coupons write → `OrgMember`
- Dunning campaign write → `OrgMember`
- Custom checkout create / mark-paid → `OrgMember` (`Endpoints.cs:56, 65`)
- Payment-config GET/PUT → nested group `OrgAdmin` (`PaymentConfigEndpoints.cs:19`)

`CommerceEndpointsAuthorizationTests` locks: anonymize OrgAdmin, payment-config OrgAdmin, product POST OrgMember / GET OrgRead, refund OrgMember, subscribers GET OrgRead.

**One.** Invite / revoke invite / remove member → `OrgAdmin` (`WorkspaceEndpoints.cs:97, 113, 119`). API keys → `OrgAdmin` (`ApiCredentialEndpoints.cs:21`). Workspace **list** and app toggle → `OrgAdmin` plus an extra `IsSystemAdmin` check. GET members / invites / audit → **not** `OrgRead`; they are `RequireAuthorization()` + `HasTenantAccess`. A VIEWER who is a member can read them. A MEMBER who is a member can read them. That matches “viewer can see the roster.”

**Billing** entire `/admin/billing` → `OrgAdmin` (`Billing/Infrastructure/Endpoints.cs:10`). MEMBER cannot read credits, ledger, or SaaS invoice. VIEWER cannot either. LP-166 analysis said MEMBER cannot touch billing profile; the implementation is stricter (no read).

**Communications** `/admin/communications` → `OrgAdmin` (`Communications/Infrastructure/Endpoints.cs:18`). Templates and Resend BYOK are admin-only. Commerce “Notification Templates” in the ops sidebar will 403 for MEMBER/VIEWER if that page talks to this group.

**Messaging** `/messaging/notify` and `/messaging/delivery-logs` → `OrgAdmin` (`Messaging/Infrastructure/Endpoints.cs:27, 53`).

**LHDN** admin surfaces (keys façade, tenant config, admin webhooks) → `OrgAdmin`.

### 5.3 Machine policies (Wave 1, still current)

These are `RequireAssertion` mixes: human `SUPER_ADMIN`/`ADMIN` **bypass**, or `API_CLIENT` + a `scope` claim.

| Policy | Machine scope | Human bypass |
|--------|---------------|--------------|
| `IntegrationLhdnDocumentsWrite` | `lhdn.documents:write` | SUPER_ADMIN, ADMIN |
| `IntegrationLhdnDocumentsRead` | read **or** write | SUPER_ADMIN, ADMIN |
| `IntegrationPaymentsCheckoutsWrite` | `payments.checkouts:write` | SUPER_ADMIN, ADMIN |
| `IntegrationPaymentsCheckoutsRead` | read or write | SUPER_ADMIN, ADMIN |
| `IntegrationWebhooksEndpointsManage` | `webhooks.endpoints:manage` | SUPER_ADMIN, ADMIN |
| `IntegrationCommerceSubscriptionsWrite` | `commerce.subscriptions:write` | SUPER_ADMIN, ADMIN |
| `IntegrationCommerceSubscriptionsRead` | read or write | SUPER_ADMIN, ADMIN |
| `IntegrationPaymentsMe` | any payments.* | **none** — humans must not pass |

`IntegrationPaymentsMe` is the only policy that **excludes** humans (`AuthAndCorsExtensions.cs:153–161`). That is the K1 introspect contract: a cashier key can ask “who am I / what tenant / what mode” without a person impersonating a key.

Human ADMIN bypass on the other integration policies means an ops curl with a cookie can hit M2M checkout-create. That is intentional (W1-LP-137 analysis offered both options; the code chose admin bypass).

### 5.4 What the policies do **not** model

- No `billing:read` vs `billing:write`.
- No `settings:read` for VIEWER who should see “you have a gateway connected” without seeing the secret.
- No location / cashier scope (HitPay).
- No custom permission matrix (Stripe IAM).
- No “owner” distinct from ADMIN (anyone can invite another ADMIN).
- Frontend does not read these policies at all (see §11).

---

## 6. `TenantSecurityMiddleware` + `X-Tenant-Id`

### 6.1 Pipeline

`MiddlewarePipelineExtensions.cs:19–28` (order is load-bearing):

1. Exception handler  
2. Correlation id  
3. CORS  
4. JWT authentication (cookie or Bearer)  
5. **API key middleware** (may **replace** `context.User`)  
6. **Tenant security**  
7. Authorization  

A request that sends both a cookie and `Authorization: Bearer sk_live_…` becomes an `API_CLIENT`. The human is gone.

### 6.2 Resolution order

`TenantSecurityMiddleware.InvokeAsync` (`TenantSecurityMiddleware.cs:20–109`):

1. If `AuthenticationType == "ApiKey"`: **return immediately**. Tenant is already in `Items` from the key row. No membership check. No header required. Test: `TenantIsolationHardeningTests.Middleware_ApiKey_Skips_Tenant_Header_Requirement` (lines 155–176).
2. If path starts `/api/v1/platform`: force system tenant GUID, return.
3. Else resolve tenant from, in order:
   - `X-Tenant-Id` (GUID parse)
   - `X-Tenant-Slug` → `GetTenantIdBySlugAsync` (active orgs only)
   - route value `tenantSlug` → same
4. If not exempt **and** `RequiresTenantContext` **and** no tenant → **400** ProblemDetails “Missing Tenant Context Header. X-Tenant-Id is required for this route.” (`lines 55–70`). JWT on `/api/v1/lhdn/documents` without header is the hardening test (lines 132–152).
5. If a tenant resolved **and** the user is authenticated as a human:
   - Look up membership role.
   - If present: `AddClaim(Role, role)`.
   - If absent **and not exempt**: **403** JSON “You do not have access to this workspace…” (`lines 90–103`).
   - If absent **and exempt**: continue without injecting a role.

Exempt paths never 400 for missing tenant and never 403 for missing membership (`IsTenantExemptPath`, lines 115–144):

- `/health`
- `/api/v1/public` (storefront + `/public/one/{slug}/branding`)
- `/api/v1/webhooks`
- `/api/v1/one/public`
- `/api/v1/one/auth`
- `/api/v1/one/me`
- **`/api/v1/one/workspaces` (the entire prefix)**
- `/api/v1/one/integrations/workspaces`

Required-tenant paths (`RequiresTenantContext`, lines 149–167):

- `/api/v1/admin`
- `/api/v1/lhdn`
- `/api/v1/ops`
- `/api/v1/messaging`
- `/api/v1/one/storage`
- `/api/v1/one/api-keys`

Architecture tests lock the exempt/required lists (`TenantIsolationArchitectureTests.cs:90–111`).

### 6.3 The `/one/workspaces` exemption is the IDOR hinge

Every members / invites / accept / audit / webhook / apps route lives under `/api/v1/one/workspaces`, so **middleware will not require `X-Tenant-Id` and will not 403 a non-member just for omitting it.** Authorization is whatever the endpoint and command do.

That is why Wave 3 had to put `HasTenantAccessAsync` on GET members / invites / audit, and `CanManageMembers` inside invite/remove, and `CanAccessWorkspaceWebhooksAsync` (path id == key tenant, or membership) on webhooks. The pre-wave “any authenticated user who guesses a GUID” bug is **fixed on those handlers**, not by taking the prefix off the exempt list.

Cost of keeping the exemption:

- Create/list/accept can run before a tenant exists (legitimate).
- `OrgAdmin` on `POST /workspaces/{id}/invites` depends on the **header** to inject `ADMIN`. Ops always sends the header. A raw client must send `X-Tenant-Id` matching a workspace they admin, **or** be JWT `SUPER_ADMIN`.
- Header tenant and path `{id}` can diverge (see §10).

### 6.4 What the header means in Ops

`api-client.ts:15–21` sets `X-Tenant-Id` on **every** openapi-fetch call when a workspace is selected, including One routes that do not need it. `AuditLogPage` sets it again by hand (`AuditLogPage.tsx:25–28`) because it uses raw `fetch`, not the client. Subscribers page also raw-fetches with the header.

Switching workspace (`App.tsx:110–114`) writes localStorage and navigates to `/commerce/dashboard`. The next request’s header is the new id. There is no server-side “active tenant” other than this header.

### 6.5 Fail-closed EF

Empty ambient `TenantId` matches no `IMustHaveTenant` rows (`TenantIsolationHardeningTests.Empty_Tenant_EF_Filter_Returns_Zero_Rows`). One list/get members endpoints **IgnoreQueryFilters** and filter by path id instead (`OneQueryService.cs:74–76, 112–115`). That is correct for exempt routes; it also means a bug that forgets the `OrganizationId == id` predicate is a cross-tenant read. Current queries include the predicate.

---

## 7. Invite / accept loop — the accept page is missing

This is the largest **product** hole in identity after Waves 1–4. LP-166 shipped the allow-list and the Team page. It did not ship the loop.

### 7.1 What the backend does

**Invite** (`InviteUserToWorkspaceCommand.cs:31–76`):

1. Inviter must `CanManageMembers` or be `IsSystemAdmin`. MEMBER cannot invite (test `Member_CannotInvite`).
2. Role allow-listed.
3. If the email already has a `GlobalUser` who is already a member → throw.
4. If the email has a `GlobalUser` who is **not** a member → still create an invitation (they must accept while logged in as that user).
5. If the email has **no** user → still create an invitation.
6. Token: CSPRNG + SHA256 hash at rest (`TokenGeneratorService.cs`), 7-day expiry (`InviteUserToWorkspaceCommand.cs:48–49`).
7. Domain event carries the **plain** token (`WorkspaceInvitation.cs:35`).
8. Audit `member.invited` with `{ email, role }`, not the token (lines 64–72; test `Invite_RecordsAuditWithoutSecrets`).

Pending index on `(OrganizationId, Email)` filtered to `Status = 'PENDING'` (`OneDbContext.cs:88`) is **not unique**. Two pending invites for the same email can coexist.

**Email** (`NotificationDispatchDomainEventHandlers.cs:65–79`):

```
{App:ClientUrl}/accept-invite?token={plainToken}
```

`App:ClientUrl` defaults to `http://localhost:3004` (`appsettings.json:41`; `OneLinkService.cs:17`). That is the ops SPA. The mail is dispatched with `OrganizationId` of the **workspace** (line 79), not the system tenant. Password-reset and verify-email use `Guid.Empty` system tenant (lines 19, 44–45, 61–62). So: **invite mail is tenant-scoped and requires that tenant’s Resend BYOK.** A brand-new workspace that has not configured Email Provider yet will not deliver the invite. Report `plans/007-feats/16-communications-whatsapp-email.md` already called this a chicken-and-egg. Waves 1–4 did not change it.

**Accept** (`AcceptWorkspaceInvitationCommand.cs:22–42`):

1. Caller must already be an authenticated, active `GlobalUser`.
2. Token hash must match a PENDING, unexpired invite.
3. **`user.Email` must equal `invitation.Email`** (line 33–34).
4. `invitation.Accept()`; insert `TenantMembership` with the invited role.
5. **No audit row.**
6. No “already a member” pre-check. A double-accept hits the unique `(GlobalUserId, OrganizationId)` index (`OneDbContext.cs:73`) and surfaces as a 500-class failure unless the exception middleware maps it.

Endpoint: `POST /one/workspaces/invites/accept` with `{ token }`, `RequireAuthorization()` (`WorkspaceEndpoints.cs:121–125`). TypeSpec has the same route (`routes.tsp:161–166`).

**Revoke** requires `CanManageMembers` (`RevokeWorkspaceInvitationCommand.cs:25–30`). Only PENDING can be revoked.

### 7.2 What Ops does

`TeamPage.tsx`:

- Lists **members** (`GET /one/workspaces/{id}/members`).
- Invite form: email + role select ADMIN/MEMBER/VIEWER (`lines 82–90`).
- Remove button on every row including yourself (`lines 114–122`).
- On invite success, invalidates `workspace-members` only (`line 40`). It never fetches `GET /workspaces/{id}/invites`. **Pending invites are invisible.** There is no revoke button.
- No last-admin warning. No “you are about to remove yourself.”
- Copy claims “Members operate commerce; Viewers can only read.” (`line 62`). The page itself does not hide the invite form from a VIEWER. A VIEWER who opens Team will 403 on submit (policy `OrgAdmin`). They still see the form.

Sidebar: Workspace → Team (`Sidebar.tsx:269`). No role gating.

### 7.3 What does **not** exist

Grep of `accept-invite` across `*.tsx` / `*.ts` / `*.cs` finds **only** the email builder in `NotificationDispatchDomainEventHandlers.cs:67`. There is no route in:

- `apps/lazuar-ops/src/App.tsx` (public routes: `/`, `/pricing`, `/signup`, `/login`; catch-all `*` → `/commerce/dashboard`, line 247)
- `apps/lazuar-portal`
- `apps/lazuar-admin`
- `apps/lazuar-developers`

Same for `reset-password` and `verify-email` **pages**. The emails those handlers send (`NotificationDispatchDomainEventHandlers.cs:31, 50`) also point at routes that do not exist. Ops has **zero** references to forgot-password / reset-password / verify-email.

So the click path is:

1. Admin invites `bookkeeper@example.com`.
2. If tenant Resend is configured, they get a link to `https://hub…/accept-invite?token=…` (or localhost:3004).
3. Ops router misses `/accept-invite`. Catch-all sends them to `/commerce/dashboard`.
4. If they have no cookie, `OpsLayout` redirects to `/login?returnUrl=/commerce/dashboard` — **token is gone**.
5. If they sign up (the only way to get an account for a new email), **register creates a second workspace** of which they are ADMIN. There is no “I was invited, don’t create a workspace” flag on `PublicRegisterRequestDto`.
6. After signup they land on **their** dashboard, not the inviter’s. They still have no UI to POST the token.
7. Even a perfectly crafted `POST /one/workspaces/invites/accept` requires they log in as the **same email**. A user who signed up with a different email cannot accept.

The accept **API** is real. The accept **product** is not. LP-166 acceptance criterion “Owner invites a bookkeeper as Viewer; they see subscribers and cannot refund” cannot be executed through the UI without a hand-built HTTP call and a pre-existing GlobalUser with that email.

### 7.4 The only working “add a human” loops today

| Loop | Works? |
|------|--------|
| You register, you are ADMIN of workspace #1 | Yes |
| You create workspace #2 from the switcher | Yes (you are ADMIN again) |
| Integrator provision with `owner_email` on an existing GlobalUser | Yes (attach) |
| Integrator provision with a new email | Creates/attaches per provision handler (Aura path) |
| Team page invite → email → click → Viewer in the same workspace | **No** (page missing + register-creates-workspace + BYOK mail) |
| Team page invite → already-logged-in same email → API accept | API only |
| Share the owner password | Works, destroys auditability |

### 7.5 Related missing auth pages

| Email link | API | Page |
|------------|-----|------|
| `/accept-invite?token=` | `POST /one/workspaces/invites/accept` (auth) | **Missing** |
| `/reset-password?email=&token=` | `POST /one/auth/reset-password` (anon) | **Missing** |
| `/verify-email?email=&token=` | `POST /one/auth/verify-email` (**auth**, uses `ctx.UserId` not the email query) | **Missing**, and the API is session-bound so the email query `email=` is unused |

Verify-email is doubly broken: register never issues a token; resend does; the email link cannot call the API without a session; the API ignores the email in the URL and uses the logged-in user. Pre-wave gap note §Email verification is still accurate.

---

## 8. API key scopes, test / live

This is the strongest identity-adjacent DX in the repo after Waves 1–4. Keys moved to One (R05 / remaining-005). LHDN is a façade.

### 8.1 Storage

`ApiCredential` (`ApiCredential.cs`) in `one.ApiCredentials`: `OrganizationId`, `Name`, `Prefix` (`sk_test_` / `sk_live_`), `KeyHash` (SHA256 of the **full** plain token including prefix), `KeyHint` (last 4), `Scopes` (space-separated), `IsActive`, `CreatedAt`, `CreatedByUserId`. Unique on `KeyHash` (`OneDbContext.cs:133`). No plaintext at rest. No expiry column. No last-used column.

### 8.2 Mint

`GenerateApiCredentialCommand` (`GenerateApiCredentialCommand.cs:46–91`):

- 40-byte CSPRNG token, prefixed `sk_test_` or `sk_live_` from `IsTestMode` (lines 48–51).
- Hash of the **full** string (prefix + secret).
- `PlatformApiScopes.NormalizeAndValidate` — **null / empty / unknown rejected**. No implicit LHDN default (`PlatformApiScopes.cs:67–106`; tests `GenerateApiCredential_Omit_Scopes_Throws`, `Empty_Scopes_Array_Throws`, `Unknown_Scope_Throws`, `PaymentsConfigRead_Is_Unknown`).
- Audit `api_key.created` with `{ name, prefix, hint }` — not the plain key (lines 73–80).

Closed catalog (`PlatformApiScopes.cs:43–52`):

- `lhdn.documents:write` / `lhdn.documents:read`
- `payments.checkouts:write` / `payments.checkouts:read`
- `webhooks.endpoints:manage`
- `commerce.subscriptions:read` / `commerce.subscriptions:write`

`payments.config:read` is **gone** (explicit test). Keys cannot read or write gateway secrets. `DefaultAuraIntegratorScopes` is payments write+read + webhook manage (`PlatformApiScopes.cs:37–38`). `DefaultDocumentScopes` remains as a named pair for LHDN UI presets but is **never implied on omit**.

HTTP: `POST /one/api-keys` under `OrgAdmin` (`ApiCredentialEndpoints.cs:31–62`). Uses `ctx.TenantId` (header), not a path id. Returns `plain_key` once. List never returns it.

### 8.3 Comment drift (do not trust these)

`IApiCredentialService` XML still says “Null/omitted uses LHDN document defaults” (`IApiCredentialService.cs:32–34`). That is **false**. The command rejects omit.

`Lhdn/Infrastructure/Endpoints/AdminApiKeyEndpoints.cs:51` comment: “Null/omitted scopes → LHDN document defaults (product façade compat).” The façade passes `scopes` through to the same `GenerateAsync`. Omit now **400**s. The LHDN façade is a compatibility URL, not a different policy. `Lhdn/Domain/ApiKeyScopes.cs` still documents “Default scopes granted to newly minted keys (v1 matrix)” against a `DeveloperApiKey` type that R05 stopped using for mint.

### 8.4 AuthN of a key

`ApiKeyAuthenticationMiddleware`:

- Accepts `Authorization: Bearer sk_live_|sk_test_…` **or** raw `Authorization: sk_…` (`TryGetApiKey`, lines 138–165). Other Bearer tokens (JWTs) are ignored; JWT middleware already ran.
- Lookup SQL is **One-only** (`OneLookupSql` lines 17–22; `LookupCredentialAsync` lines 114–133). Lhdn-only leftover keys 401. Deploy gate comment is still in the source (lines 99–112): ship only when `active_legacy_only = 0`.
- Cache 5 minutes by hash; tenant key-hash list 10 minutes (lines 51, 67).
- Revoke publishes `ApiKeyRevokedIntegrationEvent`; host handler evicts `ApiKey_{hash}` immediately (`ApiKeyRevokedIntegrationEventHandler.cs:24–32`). The 5-minute window is closed on the happy path. A revoke that fails to publish leaves a 5-minute ghost.

`IsTestMode` is **not a column**. It is “does the presented secret start with `sk_test_`?” (`ApiKeyAuthenticationMiddleware.cs:35, 75`). Payments cashier uses that flag to refuse a test key against a live gateway (`CheckoutSessionCashier.EnsureKeyModeMatchesGateway` / `EnsureKeyModeMatchesConfigEnvironment`). LHDN submit reads `IExecutionContextAccessor.IsTestMode`. A confused operator who mints `sk_live_` and pastes it into a sandbox still looks “live” to the platform.

### 8.5 Ops UI

`ApiKeysPage.tsx` is a real developer console: name, test/live select, scope catalog matching `PlatformApiScopes`, presets “LHDN documents” and “Payments integrator”, reveal-once + copy, Stripe-prefix warning (`sk_test_` collision with Stripe secrets, lines 381–390), revoke confirm, links to docs `/lhdn`, `/one`, `/auth`. MEMBER/VIEWER can **open** the page (no frontend role check); POST 403s.

### 8.6 Test vs live as a product

What exists:

- Prefix distinction.
- Payments hop refuses mode mismatch.
- LHDN documents record `IsTestMode`.

What does **not** exist:

- A sandbox **environment** product. `sk_test_` is not a separate database. It is a bit on the request. Commerce products, subscribers, and the ledger are the same tenant data.
- Clock / fixtures / test clocks.
- Per-key IP allowlist, expiry, rotation reminder.
- Last-used timestamp on the list UI (column does not exist).

Sellable to an Aura integrator: “mint a payments key, put it in Guest payments, receive signed webhooks.” Not sellable as “Stripe test mode.”

---

## 9. `AuditEvent` recorder

### 9.1 Model

`AuditEvent` (`AuditEvent.cs`): `Id` (UUIDv7), `OrganizationId`, `ActorUserId?`, `ActorEmail?` (lowercased), `Action` (max 100), `EntityType` (max 64), `EntityId` (max 64), `MetadataJson` (jsonb), `CreatedAt`. Index `(OrganizationId, CreatedAt)` (`OneDbContext.cs:141–150`; migration `20260820150000_AddAuditEvents.cs`). No hash chain, no WORM, no IP column, no “before/after” snapshot.

`IAuditRecorder` (`IAuditRecorder.cs:7–8`) is documented **fire-and-forget: implementations must never throw to callers.**

`AuditRecorder` (`AuditRecorder.cs:35–84`):

- Resolves actor email from `GlobalUsers` if not passed.
- Serializes metadata snake_case; strings pass through.
- `SaveChanges` on `OneDbContext`.
- `catch (Exception)` logs and swallows (lines 77–83).

Tests: persists without `sk_live` / `password` in metadata; disposed DbContext does not throw; foreign-org GET is Forbid (`AuditRecorderTests.cs`).

### 9.2 Who writes

| Action | Entity | Writer |
|--------|--------|--------|
| `member.invited` | `invitation` | `InviteUserToWorkspaceCommandHandler` |
| `member.removed` | `membership` | `RemoveWorkspaceMemberCommandHandler` |
| `api_key.created` | `api_credential` | `GenerateApiCredentialCommandHandler` |
| `api_key.revoked` | `api_credential` | `RevokeApiCredentialCommandHandler` |
| `refund.created` | `transaction` | `RecordRefundCommandHandler` (`amount`, `status`, `reason`) |
| `subscriber.canceled` | `subscription` | `CancelAdminSubscriptionCommandHandler` (`at_period_end`, `status`) |
| `subscriber.payment_recorded` | `subscription` | `RecordSubscriberPaymentCommandHandler` (`amount`, `method`, `transaction_id`) |

Optional constructor injection (`IAuditRecorder? = null`) on every handler. Production DI registers the scoped recorder (`DependencyInjection.cs:60`). Tests that omit it silently skip audit.

### 9.3 Who does **not** write (LP-167 analysis asked for some of these)

W3-LP-167 analysis (`plans/007-feats/impl/W3-LP-167-analysis.md` lines 16–17) listed: refund, cancel/keep, record-payment, **change-plan/qty**, **collection pause**, invite/remove, **payment-config upsert**, API key mint/revoke.

Shipped: refund, cancel, record-payment, invite, remove, key mint/revoke.  
**Not shipped:** payment-config upsert, plan/qty change, dunning pause/resume, workspace create/update/archive, register, login, accept invite, revoke invite, entitlement toggle, webhook create/rotate, provision, anonymize, email-config upsert, storage upload.

Reads are not logged (as designed). Failed money paths are not logged (recorder runs after success).

### 9.4 Read API + Ops page

`GET /one/workspaces/{id}/audit?page&limit` (`WorkspaceEndpoints.cs:167–202`):

- Unauthorized if no `UserId`.
- **Forbid** if no membership and not system admin (line 177). This is one of the few One routes that returns 403 rather than 401 for IDOR.
- Page default 1, limit clamp 1–100 default 50.
- `IgnoreQueryFilters` + `OrganizationId == id`.
- Returns `PaginatedResponse<AuditEventDto>`.
- **`RequireAuthorization()` only** — not `OrgRead`. Any member including VIEWER can read the audit. That is reasonable for “who refunded this” and means a Viewer sees API key create/revoke events (ids and hints in metadata, not secrets).

Ops `AuditLogPage.tsx`: table of when / actor email / action / entity. 403 → empty list (line 29), not an error toast. Description: “Who changed money or identity in this workspace. Reads are not logged.” Pagination if `total_pages > 1`. Does not render `metadata_json`. No filter by action. No export.

Sidebar: Workspace → Audit log (`Sidebar.tsx:270`). Utility ledger remains a **different** page (`/workspace/ledger`) and is not this audit.

### 9.5 Compliance honesty

This is a merchant activity feed, not a SIEM, not PDPA access log, not Stripe “security history.” Swallow-on-error means a broken `one.AuditEvents` write **cannot** be detected from the money path. For “which staff member refunded this at 16:02” it is enough **if** the recorder ran and the actor email resolved. `RecordRefundCommandHandler` does not pass `actorUserId` / `actorEmail` (it relies on ambient `UserId`). API-key revoke audit also omits actor email in the call (`RevokeApiCredentialCommand.cs:53–59`) — ambient user should still fill it. A background job with empty `UserId` would write a row with null actor.

---

## 10. IDOR risks (after Waves 1–4)

Pre-wave criticals from `docs/001-gaps/10-one-identity-module.md` §Security Gaps, re-checked.

| # | Pre-wave claim | After Waves 1–4 |
|---|----------------|-----------------|
| 1 | Any auth user can GET members/invites by guessing GUID | **Fixed.** `HasTenantAccessAsync` or system admin (`WorkspaceEndpoints.cs:86–87, 102–103`). Tests on audit Forbid. |
| 2 | Any member can invite / remove | **Fixed** on write: `OrgAdmin` + `CanManageMembers`. GET still any member. |
| 3 | Webhook GET returns full secret | **Fixed.** GET returns `has_secret` + hint; create/rotate return secret once (`WebhookEndpoints.cs:33–41, 73–80`). |
| 4 | Stamp only on `/auth/me` | **Unchanged.** Stolen JWT works until expiry after password change. |
| 5 | Presigned upload with empty TenantId | **Hardened.** Endpoint 400s if `TenantId` empty (`StorageEndpoints.cs:27–32`); middleware requires tenant on `/one/storage`. Any **member** of that tenant can still upload (no OrgAdmin). |
| 6 | API keys in LHDN, `API_CLIENT` is OrgAdmin-equivalent | **Fixed.** One-owned keys; `OrgAdmin` is human-only; scopes on machine policies. |
| 7 | No login rate limit | **Unchanged** for login. Register now limited. |
| 8 | Invite role any string / CLIENT can invite as ADMIN | **Fixed** allow-list + inviter check. |

### 10.1 Remaining IDOR / confused-deputy

**H1 — Header tenant ≠ path id (One exempt prefix).**  
Ops never does this. A crafted request can:

- Send `X-Tenant-Id: A` (user is ADMIN of A) so `OrgAdmin` passes.
- Call `POST /one/workspaces/{B}/invites`.  
  Command checks membership on **B**. MEMBER of B → throw Unauthorized. ADMIN of B → invite succeeds. Fail-closed for privilege; succeed if they really admin B. Not a steal.  
- Send `X-Tenant-Id: A` and `GET /one/api-keys` → keys for **A** (`ctx.TenantId`). Path is not involved. Fine.  
- Send `X-Tenant-Id: A` and `GET /admin/commerce/subscribers` → subscribers for **A**. Fine.

The dangerous version would be an endpoint that authorizes on the header and **reads the path id**. Webhooks explicitly compare them for API keys (`WebhookEndpoints.cs:283–288`). Human webhook manage uses path id membership, not the header. **Do not add a new One route that uses `ctx.TenantId` for auth and `{id}` for data without comparing them.**

**H2 — `HasTenantAccess` ignores archive and role.**  
Any historical membership, including on an archived org, can list members, invites, and audit. No `IsActive` check.

**H3 — Superadmin synthetic entitlements vs real 403.**  
Covered in §5.1. Not classic IDOR; it is a broken support path that looks like access.

**H4 — `GET /one/workspaces` lists every org for system admins.**  
Intended. Do not put that route on a merchant JWT.

**H5 — Unlimited `POST /workspaces`.**  
Any authenticated human (JWT `CLIENT`) can create tenants. Not IDOR; it is resource creation abuse (50 starter credits per new org via `AppEntitlementGranted` → billing seeder when `BILLING` is in `provision_apps`).

**H6 — Invite index not unique.**  
Duplicate pending invites; two accept links.

**H7 — Remove last admin / remove self.**  
`RemoveWorkspaceMemberCommand` does not count remaining ADMINs and does not forbid `TargetUserId == RequesterUserId`. A workspace can be orphaned. Team UI offers the button on every row.

**H8 — Accept is session-swap.**  
If Alice is logged in and posts Bob’s token, she gets “invitation belongs to a different email.” Good. If Alice’s cookie is stolen, the thief can accept an invite **to Alice’s email**. Same as any cookie theft.

**H9 — Rate-limit key spoof.**  
`X-Forwarded-For` first hop (`AuthEndpoints.cs:172–178`) without a documented trusted-proxy constraint.

**H10 — CORS default allow-any when `App:CorsOrigins` empty** (`AuthAndCorsExtensions.cs:208–212`). Repo appsettings sets origins. Production must not clear the key. `AllowCredentials` + listed origins is the configured path (lines 199–205).

**H11 — CSRF.**  
Cookie SameSite=Lax, no anti-CSRF token. Cross-site POST from another subdomain on `.lazuar.com` is the remaining browser surface. Lax blocks most foreign POST. It does not block same-site sibling apps.

**H12 — `/ops` role `CLIENT`.**  
Any merchant JWT passes. Membership VIEWER included. Chat is unrouted (ADR 023) so this is latent.

**H13 — Audit GET is membership-wide.**  
A VIEWER reads key mint/revoke events. Acceptable for a three-role product; say it out loud.

**H14 — Platform cookie path vs ops cookie.**  
Different cookies. A SUPER_ADMIN who also uses ops has two cookies. Stealing `lazuar_auth` does not yield platform payment-config. Stealing `lazuar_admin_auth` only works on `/api/v1/platform` (Path scoped). Good.

---

## 11. Ops Team page vs actual staff product

LP-166’s analysis (`W3-LP-166-analysis.md`) said the acceptance bar was:

1. Owner invites a bookkeeper as Viewer; they see subscribers and cannot refund.  
2. Member can enroll / record-payment and cannot rotate API keys.  
3. Team page is the only staff UX.  
4. Existing single-admin workspaces unchanged.

**Backend of (1) and (2) is real** if you can get the membership row onto the user (API accept or provision attach). Policies and tests exist. **Product of (1) is not real** — see §7.

**Frontend of (2) and (3) is a costume.**

Grep of `lazuar-ops/src` for `VIEWER`, `e.role`, `isViewer`, `user.role` used as authorization: **only** the Team page `<option value="VIEWER">`. Entitlement DTOs include `role` (`WorkspaceEndpoints.cs:163`) and the switcher **never displays it** (`PageLayout.tsx:90–98`). Sidebar is the same 20 links for every human (`Sidebar.tsx:249–275`): Dashboard, Checkout Links, Subscribers, Transactions, Disputes, Promotions, Dunning, Templates, API Keys, Webhooks, Delivery Logs, General, Team, Audit, Legal & Billing, Payment Gateways, Plan & billing, Email Provider, plus remounted Invoicing.

A VIEWER therefore:

- Sees **Create Key**, **Invite**, **Remove**, **Payment Gateways**, **Email Provider**, **Legal & Billing**, refund buttons (wherever the commerce pages put them).
- Clicks them and gets 403 / toast.
- Can read subscribers (OrgRead) and audit.
- Cannot read billing wallet (OrgAdmin) — the Plan & billing page will fail its fetches.
- Cannot read payment-config (OrgAdmin) — gateways page fails.
- JWT is still `CLIENT`, so any leftover `RequireRole("CLIENT")` passes.

A MEMBER:

- Can refund (OrgMember). LP-166 explicitly wanted that (“bookkeeper operates commerce”). There is no “refund requires ADMIN” split. Stripe’s analyst-vs-admin is not here.
- Cannot mint keys, cannot PUT payment-config, cannot invite, cannot anonymize, cannot change email BYOK, cannot touch `/admin/billing`.
- Still **sees** all of those nav items.

There is no:

- Pending-invite inbox
- Role change
- Owner vs admin
- Last-admin lock
- SSO / SCIM / Google login
- 2FA
- Per-user session list
- “You’re a Viewer” badge
- Hide-nav-by-role (the analysis called this in-scope at `W3-LP-166-analysis.md` line 20: “Ops nav hide for Viewer (mutations)”; **not implemented**)

Compare to what the 007 merchant-dashboard report said merchants are trained to expect (Stripe IAM, HitPay Owner/Admin/Manager/Cashier, Chargebee roles). We shipped **three strings and a form**. That is not a staff product. It is a policy scaffold plus a roster widget.

Admin app (`lazuar-admin`) is **not** a staff product either. It is platform gateway vault + `lazuar_admin_auth`. No tenant directory UI beyond whatever that one page is. Superadmin support of customer workspaces is the broken synthetic-entitlements path in ops.

---

## 12. File-by-file notes (post-wave)

### Domain

| File | After Waves 1–4 |
|------|-----------------|
| `GlobalUser.cs` | Still no deactivate/MFA/lockout/last-login. `UserRegistered` still orphaned. Stamp rotation on password only. |
| `Organization.cs` | Branding + external-ref bind added (Waves 1/provision). Archive event still unpublished. |
| `TenantMembership.cs` | Comment still `ADMIN`/`CLIENT`. No `ChangeRole`. |
| `WorkspaceStaffRoles.cs` | **New (LP-166).** Allow-list + `CanManageMembers`. |
| `WorkspaceInvitation.cs` | Status machine unchanged. No resend. Plain token only in the domain event. |
| `PlatformApiScopes.cs` | **New (LP-131).** Closed catalog, reject-on-omit. |
| `ApiCredential.cs` | **New (R05 / Wave 1).** One-owned machine key. |
| `AuditEvent.cs` | **New (LP-167).** |
| `Rules/OrganizationSlugMustBeValidRule.cs` | Reserved set includes `login`, `admin`, `portal`, `system`, `billplz`, `stripe`, `lazuar`, `one`, `auth`. Genesis SQL bypasses it. |

### Application commands

| File | After Waves 1–4 |
|------|-----------------|
| `RegisterPublicUserCommand.cs` | Slug validated before user track. Still no verify token, no audit, no COMMERCE entitlement. |
| `CreateWorkspaceCommand.cs` | Zero-membership create. Provision apps caller-defined. No rate limit. |
| `UpdateWorkspaceCommand.cs` | Exact `ADMIN`. No slug uniqueness. |
| `ArchiveWorkspaceCommand.cs` | Exact `ADMIN`. No cascade. |
| `InviteUserToWorkspaceCommand.cs` | Allow-list + CanManageMembers + audit. |
| `AcceptWorkspaceInvitationCommand.cs` | Email bind. No audit. No already-member guard. |
| `RemoveWorkspaceMemberCommand.cs` | CanManageMembers + audit. No last-admin. |
| `RevokeWorkspaceInvitationCommand.cs` | CanManageMembers. No audit. |
| `GenerateApiCredentialCommand.cs` / `RevokeApiCredentialCommand.cs` | Scopes + audit + revoke event. |
| `ForgotPasswordCommand.cs` / `ResetPasswordCommand.cs` / `VerifyEmailCommand.cs` / `ResendVerificationEmailCommand.cs` | Unchanged; pages missing. |
| `ProvisionAuraWorkspaceCommand*.cs` | Integrator hatch; only reliable multi-human attach. |

### Host

| File | After Waves 1–4 |
|------|-----------------|
| `AuthAndCorsExtensions.cs` | OrgMember/OrgRead added. OrgAdmin human-only. Integration* scope policies. JWT prod secret guard. Dual cookie. |
| `TenantSecurityMiddleware.cs` | Exempt `/one/public`, `/one/workspaces`, provision. Require tenant on storage + api-keys + admin/lhdn/ops/messaging. Inject membership role. |
| `ApiKeyAuthenticationMiddleware.cs` | One-only SQL. Prefix → IsTestMode. Scope claims. |
| `MiddlewarePipelineExtensions.cs` | JWT then API key then tenant. |
| `ExecutionContextAccessor.cs` | `IsTestMode`, `AuditSignature` (agent prefix unused by One audit). |
| `ApiKeyRevokedIntegrationEventHandler.cs` | Cache evict, One event only. |

### Ops

| File | After Waves 1–4 |
|------|-----------------|
| `App.tsx` | Public `/pricing` `/signup` `/login`; HomeRedirect; Team; Audit; empty-workspace create. No `/accept-invite`. |
| `LoginPage.tsx` | Signup + TOS checkbox + slug helpers. No forgot-password link. |
| `PricingPage.tsx` | Honest card. |
| `TeamPage.tsx` | Roster + invite + remove. No pending invites. No role hide. |
| `AuditLogPage.tsx` | Thin table. 403 → empty. |
| `ApiKeysPage.tsx` | Actual DX. |
| `Sidebar.tsx` | Team + Audit links. No role hide. |
| `api-client.ts` | Always `X-Tenant-Id`. |
| `PageLayout.tsx` | Switcher + create workspace. Role not shown. |

### Tests that lock the new behavior

- `RegisterPublicUserCommandHandlerTests` — happy path, dup email, taken/reserved/malformed slug, no AppAccessRequest.
- `GetPublicPricingQueryHandlerTests` — GMV always 0, packs, SST, checkout free when amount 0.
- `PublicRegisterRateLimiterTests` — 11th denied.
- `CreateWorkspaceCommandHandlerTests` — zero memberships → ADMIN + apps.
- `WorkspaceCreateAuthorizationTests` — source contains `RequireAuthorization` on POST workspaces; pricing anonymous.
- `InviteUserToWorkspaceCommandHandlerTests` — MEMBER role stored, banana rejected, MEMBER cannot invite, SUPER_ADMIN membership can invite, audit has no token.
- `GenerateAndListApiCredentialsTests` — omit/empty/unknown scopes throw; payments-only does not imply LHDN; `payments.config:read` unknown.
- `AuditRecorderTests` — persist, swallow, foreign GET forbid.
- `CommerceEndpointsAuthorizationTests` — OrgRead/OrgMember/OrgAdmin split.
- `TenantIsolationArchitectureTests` / `TenantIsolationHardeningTests` — exempt paths, required tenant, API key skip, empty tenant filter.
- `ApiKeyAuthenticationTests` — sk_test_ claim, policies.

There is **no** test that an accept page exists. There is **no** test that Ops hides nav by role. There is **no** end-to-end invite test.

### README drift that still misleads

`Modules/One/README.md`:

- Line 22: roles `ADMIN`, `CLIENT` — wrong for staff; `CLIENT` is JWT-only.
- Lines 33–34: paid subscription may grant `CLIENT` membership — **no handler**.
- Line 68: schema list omits `AuditEvents` (exists).
- Public register paragraph (lines 10–11) is now correct.

---

## 13. Auth flows still around the identity core (not Wave-owned, still true)

### Login

Email + password, BCrypt verify, inactive or missing → 400-with-401. No lockout, no CAPTCHA, no verify required. Role in body CLIENT/SUPER_ADMIN. Cookie 24h.

### Logout

Delete `lazuar_auth`. Stamp untouched. Other devices keep working.

### Forgot / reset

Silent if missing. 24h token. Email link to a **non-existent** `/reset-password`. Reset rotates stamp. No Ops UI to request a reset.

### Change password / profile

`PUT /me/security/password` verifies current, rotates stamp. `PUT /me/profile` name only → `GlobalUserProfileUpdated` integration event (CRM consumer). No email-change path (email immutable after create).

### Magic link

Still **not** One. Commerce subscriber portal only (`IMagicLinkTokenService`). Buyer TOS talking about magic links is correct for portal and wrong for merchant signup.

### Platform admin

`POST /api/v1/platform/auth/login` (`PlatformAuthEndpoints.cs:27–64`) requires `IsSystemAdmin`. Separate cookie. Admin SPA has **no signup** (`lazuar-admin` login only).

---

## 14. Verdict: sellable DX or not

Split the question. “Identity” is not one product.

### 14.1 Sellable today (with honest packaging)

**Time-to-first-workspace.** A stranger can open `/pricing`, see 0% GMV and a free Hub Starter, click wrap, and get an ADMIN workspace + cookie + dashboard. That is what LP-006/LP-184 were for. HitPay/Stripe/Paddle all have this URL. We now have it. Sell “create a workspace in a minute.” Do not sell “we have a merchant contract.”

**Integrator keys.** One-owned `sk_test_` / `sk_live_`, closed scopes, reveal-once UI, Stripe-prefix warning, cache-evict on revoke, machine policies that are not OrgAdmin-equivalent, `IntegrationPaymentsMe` humans-cannot-pass. This is the closest thing in the company to Stripe-shaped DX. Sell “mint a payments key, lock it to checkouts + webhooks.” Do not sell “test mode” as a second universe.

**Hosted checkout tenancy.** `X-Tenant-Id` + fail-closed EF + API key binds tenant without a header. That is enough for Aura and the cashier sample.

### 14.2 Not sellable today (do not put on a pricing page or a sales deck)

**Team.** The accept page is missing. Invite email points at `/accept-invite` on the ops host and is swallowed by the `*` → dashboard redirect. New emails cannot join without creating a **second** workspace. Invite mail requires tenant Resend BYOK the new tenant does not have. Pending invites are invisible. No last-admin. No role change. No nav hide. Calling this “staff roles” in a competitive matrix (tracker LP-166 = Y) is a **policy-layer truth** and a **product lie**. A bookkeeper cannot be onboarded by a non-engineer.

**Audit as compliance.** Seven action types, swallow-on-error, no payment-config, no login, no export, no hash chain. Enough to answer “who refunded this” **sometimes**. Not enough to sell “audit log” next to Stripe security history or Chargebee Events.

**Auth hardness.** No MFA, no SSO, no lockout, login 400/401, stamp not global, no session list, no forgot-password UI, verify-email API/page mismatch, TOS is the buyer document, 99.9% sentence still on that document. A bank or a procurement questionnaire will fail us.

**Support / superadmin.** Synthetic entitlements vs middleware 403. Dual cookies. Ops and admin are different apps. Fine internally; not a sellable “we can jump into your workspace.”

### 14.3 Dual-role model: shippable, not teachable

JWT `CLIENT` + injected membership `ADMIN|MEMBER|VIEWER` + machine `API_CLIENT` works **if and only if** the client sends `X-Tenant-Id` (ops does). It is a foot-gun for every new frontend and every Scalar “try it.” Register body saying `ADMIN` while the cookie says `CLIENT` is still there. `TenantMembership` comments and the One README still say `CLIENT`. Fixing the comments is not a wave; leaving them is how the next agent re-introduces `CLIENT` as a staff role (the invite tests now **reject** that string — keep those tests).

### 14.4 Compared to the pre-wave gap note

Waves 1–4 actually closed the structural debts that note named around **credentials ownership**, **invite stringly roles**, **members IDOR**, **webhook secret GET**, and **no public pricing**. They did **not** close **authorization consistency in the UI**, **invite completion**, **archive lifecycle**, **stamp-on-every-request**, or **login abuse**. The skeleton is a CIAM. The sellable product is “one human, one workspace, keys, checkout.” Everything that looks like a company account (seats, SSO, audit, legal) is a façade or a half-loop.

### 14.5 One-line judgment

**Sell the self-serve workspace and the scoped key. Do not sell Team, SSO, or audit-grade identity. The accept page is missing; until that loop is closed, LP-166 is backend costume.**

---

## 15. Evidence index (absolute paths)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/MiddlewarePipelineExtensions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/ExecutionContextAccessor.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/ApiCredentialEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WebhookEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/PlatformAuthEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/IntegrationProvisionEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Services/AuditRecorder.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Services/PublicRegisterRateLimiter.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/WorkspaceStaffRoles.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/PlatformApiScopes.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/EventHandlers/NotificationDispatchDomainEventHandlers.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/App.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/components/LoginPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/components/PricingPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/TeamPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/AuditLogPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/ApiKeysPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/lib/api-client.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/legal/terms/page.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/one/routes.tsp`
