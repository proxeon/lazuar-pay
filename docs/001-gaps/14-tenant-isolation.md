<!-- Source subagent: 019fc650-3513-7032-806d-65eb75bf0703 -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# Tenant Isolation Gap Analysis

Deep review of Lazuar Hub multi-tenancy based on middleware, BuildingBlocks, all module `DbContext`s, public/webhook surfaces, and docs under `apps/lazuar-api`.

---

## Isolation Model

### High-level design

| Layer | Mechanism |
|--------|-----------|
| Tenant identity | `Organization` in schema `one` (`Organization.Id` = tenant key) |
| Request binding | `HttpContext.Items["TenantId"]` set by API key middleware, `TenantSecurityMiddleware`, or platform hardcode |
| Context API | `IExecutionContextAccessor` → `TenantId`, `UserId`, `UserRole`, `IsSystemAdmin`, `IsTestMode`, `AuditSignature` |
| ORM enforcement | Global EF query filters on `IMustHaveTenant` entities |
| Write stamp | `SaveChangesAsync` auto-assigns `OrganizationId` when empty and context tenant is non-empty |
| AuthZ roles | JWT + cookie; membership role injected as `ClaimTypes.Role`; policy `OrgAdmin` = `SUPER_ADMIN` \| `ADMIN` \| `API_CLIENT` |
| Platform tenant | Fixed GUID `00000000-0000-0000-0000-000000000001` (“System Configuration”, slug `system`) seeded by `SystemGenesisBootstrapperJob` |

### Tenant-bound marker

```3:6:apps/lazuar-api/BuildingBlocks/Domain/IMustHaveTenant.cs
public interface IMustHaveTenant
{
    Guid OrganizationId { get; set; }
}
```

### Entities implementing `IMustHaveTenant` (filtered)

| Module | Types |
|--------|--------|
| **One** | `TenantMembership`, `TenantAppEntitlement`, `WorkspaceInvitation`, `TenantWebhookEndpoint`, `WebhookDeliveryOutbox` |
| **Commerce** | `Product`, `Coupon`, `Subscription`, `Order`, `CheckoutSession`, `DunningCampaign`, `CommerceTransactionLog` |
| **Billing** | `LedgerEntry`, `DeferredRevenueSchedule`, `TenantCreditBalance`, `CreditHold`, `CreditDeductionIdempotencyLog`, `TenantBillingProfile`, `DocumentSequence` |
| **Payments** | `TenantPaymentConfiguration` |
| **Communications** | `MessageTemplate`, `TenantEmailConfiguration`, `SuppressionEntry`, `Broadcast` |
| **CRM** | `ClientProfileEntity` |
| **Ops** | `OpsConversation`, `OpsMessage` |
| **Lhdn** | `LhdnTenantConfig`, `TaxDocument`, `WebhookSubscription`, `DeveloperApiKey`, `IdempotencyLog`, `TinValidateCache` |

### Intentionally non-tenant / global

| Entity / table | Rationale / risk |
|----------------|------------------|
| `Organization`, `GlobalUser` | Registry / identity |
| `PaymentWebhookLog` | Global idempotency by `(Provider, EventId)` — no `OrganizationId` |
| `MsicCode`, `CountryCode`, `TaxType` | Static LHDN lookups |
| `TenantReplica` (Messaging) | Mirror of org; **not** `IMustHaveTenant` |
| `OutboxMessage` / `InboxMessage` | Per-schema infrastructure; unscoped |
| Child entities without interface | `LedgerLine`, `CreditLedger`, `DunningStep`, `ChargeAttemptLog`, `ReminderDispatchLog` — rely on parent aggregate navigation |

### DbContexts (all inherit BuildingBlocks `PlatformDbContext`)

| Context | Schema |
|---------|--------|
| `OneDbContext` | `one` |
| `CommerceDbContext` | `commerce` |
| `BillingDbContext` | `billing` |
| `PaymentsDbContext` | `payments` |
| `CommunicationsDbContext` | `communications` |
| `CrmDbContext` | `crm` |
| `OpsDbContext` | `ops` |
| `LhdnDbContext` | `lhdn` |
| `MessagingDbContext` | `messaging` |

There is also a **dead/alternate** `Lazuar.Api.Infrastructure.Data.PlatformDbContext` (no global filters; reflection-based stamp only). Live modules use **BuildingBlocks** only.

### Dual isolation style (important)

1. **EF global filters** — automatic for `IMustHaveTenant` when `TenantId != Guid.Empty`.
2. **Explicit `organizationId` parameters** — Dapper/SQL and many command/query handlers.
3. **`IgnoreQueryFilters()` + manual `OrganizationId` predicate** — background jobs, event handlers, webhooks, public flows where request context has no tenant.

Isolation is **not** single-layer. Dapper paths and any missed `OrganizationId` predicate bypass EF filters entirely.

---

## Middleware & Context Propagation

### Pipeline order (`Program.cs`)

```
UseAuthentication → ApiKeyAuthenticationMiddleware → TenantSecurityMiddleware → UseAuthorization
```

### `ApiKeyAuthenticationMiddleware`

- Accepts `Bearer sk_live_*` / `sk_test_*`.
- Resolves tenant via Dapper against `lhdn.DeveloperApiKeys` (`KeyHash` → `OrganizationId`).
- Sets `Items["TenantId"]`, identity `AuthenticationType = "ApiKey"`, role `API_CLIENT`, claim `IsTestMode`.
- Caches key → tenant for 5 minutes.

### `TenantSecurityMiddleware`

1. **Early exit for ApiKey** — skips header resolution and membership checks (tenant already set). Correct for keys.
2. **`/api/v1/platform`** — hardcodes System Tenant `…0001` for all platform routes.
3. Resolves tenant from (in order): `X-Tenant-Id` → `X-Tenant-Slug` → route value `tenantSlug`.
4. **Mandatory tenant only for `/api/v1/admin/`** — otherwise missing header ⇒ `TenantId` remains unset ⇒ **`Guid.Empty`**.
5. If authenticated (non-ApiKey) **and** tenant resolved: loads membership role; **403** if none; else adds `ClaimTypes.Role`.

### `ExecutionContextAccessor`

```16:25:apps/lazuar-api/src/Lazuar.Api/ExecutionContextAccessor.cs
public Guid TenantId
{
    get
    {
        if (_httpContextAccessor.HttpContext?.Items.TryGetValue("TenantId", out var tenantIdObj) == true && tenantIdObj is Guid tenantId)
            return tenantId;
        return Guid.Empty;
    }
}
```

Background hosted services have **no HTTP context** ⇒ `TenantId == Guid.Empty` always unless synthetic context is introduced.

### Critical middleware gaps

| Gap | Severity | Detail |
|-----|----------|--------|
| **`Guid.Empty` disables all global filters** | **Critical** | Filter: `TenantId == Guid.Empty \|\| OrganizationId == TenantId` — empty context = full table visibility |
| **Tenant required only under `/admin/`** | **High** | `/lhdn`, `/ops`, `/one/...`, commerce payment-config under admin OK, but LHDN/Ops/One are **not** under `/admin/` |
| **Client-supplied tenant header** | Medium | Any valid member can switch workspace via header; spoof of non-member is blocked for JWT; API keys cannot rebind header (early exit) |
| **No active-org check** | Low–Med | Archived/inactive orgs can still resolve by `X-Tenant-Id` (slug path requires `IsActive`) |
| **Role injection only when tenant resolved** | Medium | Without tenant header, JWT may keep login role (`CLIENT` / `SUPER_ADMIN`) without workspace role |

---

## Data Access Patterns (query filters, explicit checks)

### Global filter (BuildingBlocks)

```41:45:apps/lazuar-api/BuildingBlocks/Infrastructure/PlatformDbContext.cs
modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
    ExecutionContext.TenantId == Guid.Empty || e.OrganizationId == ExecutionContext.TenantId);
```

```47:58:apps/lazuar-api/BuildingBlocks/Infrastructure/PlatformDbContext.cs
// On Added: if OrganizationId empty and TenantId non-empty → stamp TenantId
```

Implications:

- **Fail-open** when context is empty (webhooks, jobs, bad clients).
- Stamp does **not** overwrite a non-empty `OrganizationId` — caller can write arbitrary org if filters are off / ignored.
- No deny-on-empty policy for writes.

### **Ops soft-delete overwrites tenant filter** (Critical)

```31:37:apps/lazuar-api/Modules/Ops/Infrastructure/OpsDbContext.cs
modelBuilder.Entity<OpsConversation>(builder =>
{
    ...
    builder.HasQueryFilter(x => !x.IsDeleted);  // REPLACES tenant filter
});
```

EF Core allows **one** query filter per entity. Base applies tenant filter, then Ops replaces it with soft-delete only. **`OpsConversation` is not tenant-filtered at EF level.** Mitigation: repository methods usually pass `OrganizationId`, but any unscoped `DbSet` use is a leak.

`OpsMessage` keeps the tenant filter from base (no second filter).

### `IgnoreQueryFilters()` prevalence

Used heavily (intentionally) for:

- Background jobs: `BillingEngineJob`, `DunningEngineJob`, `BroadcastFanoutJob`, `LhdnStatusPollingJob`
- Event handlers (inbox / empty context)
- `OneQueryService` membership/webhooks
- Payment config + webhook log repositories
- CRM resolve/create (public-ish identity resolution)
- Communications repositories

Pattern is usually: ignore filter **then** filter by explicit `OrganizationId`. Residual risk is any call that ignores filters without org predicate.

### Repository ID-only lookups (context-dependent)

`CommerceRepository`:

- `GetProductByIdAsync(id)`, `GetCheckoutSessionByIdAsync(id)`, `GetSubscriptionByIdAsync(id)`, `GetCouponByIdAsync(id)` — **no org predicate**; rely on global filter when `TenantId` set, **or all tenants when empty**.

Webhook/event handlers that load by ID with empty context can touch any tenant’s rows (needed for async work, but must re-check `OrganizationId` against event).

### Dapper / raw SQL

Query services typically parameterize `@OrgId` / `@TenantId` (good). Public and arrears endpoints that key only by subscription/session GUID **skip org** when GUID is secret-by-obscurity.

---

## Public Endpoint Risks

### Commerce (`/api/v1/public/commerce`)

| Endpoint | Auth | Isolation notes |
|----------|------|-----------------|
| `GET /{tenantSlug}/products/{slug}` | None | Resolves org by slug; Dapper + query service by org — OK for public catalog |
| `GET /{tenantSlug}/validate-coupon` | None | Org-scoped coupon check — OK; enables coupon enumeration |
| `POST /checkout` | None | Resolves tenant by slug; validates session org vs slug — solid |
| `GET /checkout/{subId}/status` | None | **No tenant scope**; by session GUID only; on `COMPLETED` **mints magic-link portal token** — high value if GUID leaks/guessable |
| `GET /{tenantSlug}/portal?token=` | Magic link | Validates token → subId; loads portal with **org + subId** — good |
| `GET /{tenantSlug}/custom-checkouts/{sessionId}` | None | Org + session — OK; exposes commercial quote details if session ID known |
| `GET /checkout/{subId}/arrears` | None | **Cross-tenant by subscription GUID**; product name/price/status |
| `POST /checkout/{subId}/update-payment` | None | Loads sub + customer email via raw join; starts paid checkout — **IDOR-style** if sub GUID known (no customer proof) |

### Billing (`/api/v1/public/billing`)

| Endpoint | Risk |
|----------|------|
| `GET /{tenantSlug}/profile` | **Public TIN, legal name, registration, SST, address** — intentional for invoices but PII/tax data exposure |
| `GET /{tenantSlug}/documents/{id}?sig&exp` | HMAC over `tenantSlug:ledgerEntryId:exp` with JWT secret — good pattern; **shared secret = Jwt:Secret** |
| `GET /{tenantSlug}/documents/draft/{sessionId}` | **No signature/token** — anyone with slug + session GUID gets proforma PDF (customer name/email/line items) |

### Communications public

| Endpoint | Risk |
|----------|------|
| `GET /public/communications/unsubscribe` | HMAC `orgId:email` — good; fixed-time compare |
| `POST /public/communications/webhooks/resend` | Svix verify **optional** if `Resend:WebhookSecret` empty — **accepts unauthenticated suppressions in that mode**; tenant from tags only |

### One public

| Endpoint | Risk |
|----------|------|
| `POST /one/public/register` | Creates user + workspace; issues auth cookie; login response Role `ADMIN` (workspace role is separate) |
| Auth login/forgot/reset | Standard public auth |

### Messaging (**Critical**)

```15:19:apps/lazuar-api/Modules/Messaging/Infrastructure/Endpoints.cs
group.MapPost("/notify", async (SendTenantNotificationCommand command, IMediator mediator) =>
{
    await mediator.Send(command);
    return Results.Accepted();
});
```

**No authentication, no authorization.** Body can target any `TenantId`.

---

## Webhook Routing Isolation

### Payment webhooks

Route: `POST /api/v1/webhooks/payments/{gatewayType}/{tenantId}`

Flow:

1. Tenant taken **from URL path** (not body).
2. Load `TenantPaymentConfiguration` with `IgnoreQueryFilters` for that tenant + gateway.
3. Verify signature with **that tenant’s** webhook secret.
4. Idempotency: global `PaymentWebhookLog` by `(EventId, Provider)` — **not per-tenant**.
5. Publish integration events with `OrganizationId = request.TenantId` (URL).

**Strengths**

- Path-scoped tenant + per-tenant secret verification.
- Stateless metadata via query string for Billplz (ADR 009).

**Gaps**

| Issue | Severity |
|-------|----------|
| **No check that metadata resources belong to URL tenant** | **High** | `GatewayPaymentCompletedIntegrationEventHandler` loads checkout/subscription by metadata ID and **does not assert** `session.OrganizationId == @event.OrganizationId` |
| Cross-tenant completion | Attacker tenant B completes payment with `subscription_id` of tenant A session; signed by B’s secret → A’s session/sub may be completed under A’s `OrganizationId` while fee/attribution uses B’s event org inconsistently |
| Query-string metadata spoofing | Low if signature covers URL; depends on gateway adapter |
| Global webhook log | EventId collision across providers unlikely; same EventId two tenants rare; not org-partitioned for forensics |
| Empty execution context | Relies on `IgnoreQueryFilters` + explicit IDs; correct if handlers always re-scope |

### Resend webhook

- Org from email tags; missing tag ⇒ no suppress (safe fail).
- Dev mode without secret is dangerous if exposed publicly.

### Tenant outbound webhooks (One)

- Stored per org; delivery outbox is `IMustHaveTenant`.
- Management APIs mostly check membership; **GET webhook returns `Secret_key` in clear** to any member with access.

### LHDN webhooks

- Admin CRUD under `/lhdn` with `ctx.TenantId` — depends on client sending tenant header (see middleware gap).

---

## Superadmin / Platform vs Tenant Boundaries

### Platform API (`/api/v1/platform`)

- Group requires role `SUPER_ADMIN`.
- Login: `IsSystemAdmin` users only; cookie `lazuar_admin_auth` path-scoped to `/api/v1/platform`.
- Middleware forces `TenantId = System Tenant (…0001)`.
- Endpoints: platform payment-config GET/PUT for **system tenant only** (not arbitrary tenants).

### System genesis

- Seeds org `…0001` and elevates `PLATFORM_ADMIN_EMAILS` to `IsSystemAdmin` (password rotate from env).

### Superadmin vs tenant admin confusion

| Surface | Behavior |
|---------|----------|
| One `GET /workspaces` | Requires `IsSystemAdmin` + OrgAdmin policy — platform listing |
| One apps toggle | System admin only |
| One members/invites | **Any authenticated user** can call `GET .../members` / invites for **any workspace id** (IDOR) — only checks `UserId != Empty` |
| Invite / remove member handlers | `HasMembershipAsync` only — **any member (including CLIENT)** can invite/remove |
| Update/archive workspace | Requires membership role `ADMIN` — better |
| Webhooks GET/PUT | Explicit access / ADMIN checks — better |
| OrgAdmin policy | Includes `API_CLIENT` and `SUPER_ADMIN` — superadmin JWT can hit tenant admin APIs **if** they also pass tenant membership (middleware 403 if not a member) unless ApiKey |

### Login role vs workspace role

- `/one/auth/login` sets cookie; response role is `SUPER_ADMIN` if system admin else `CLIENT`.
- Workspace role is applied later by `TenantSecurityMiddleware` when `X-Tenant-*` present.
- Ops group: `RequireRole("CLIENT", "ADMIN")` — **excludes** pure `SUPER_ADMIN` role string unless membership injects ADMIN/CLIENT after tenant header.

### Dual payment-config surfaces

- Tenant: `/admin/commerce/payment-config` with `ctx.TenantId` (OrgAdmin).
- Platform: `/platform/payment-config` for system tenant only.

---

## Known Backfill Gaps from Docs

From `apps/lazuar-api/docs/005-tenant-isolation-mapping-backfilling.md` and `006-payment-webhook-idempotency-backfilling.md`:

### Doc 005

1. **Orphan `OrganizationId` (empty/zero GUID)** under global filters ⇒ invisible forever / FK breakage.
2. **System tenant fallback** for global assets (`…0001`).
3. **Dynamic owner matching** (email domain ↔ org slug) for CRM-like data.
4. Partitioned vs global classification (docs still mention Community module entities that may no longer exist post-pivot).
5. **`IgnoreQueryFilters` for cross-tenant reports** — must stay explicit and rare.

### Doc 006

1. Pre-cutover seed of `PaymentWebhookLogs` for last 30 days of gateway event IDs.
2. Metadata schema: `type`, `subscription_id`, `tenant_id` (Stripe); Billplz refs reconstructed from query string.
3. Risk of dual-processing during legacy → new webhook URL switch.

### Operational risks implied (not fully closed in code)

- No automated migration job in-repo that audits `OrganizationId = '000…000'`.
- Doc examples use schema names (`tenant.Organizations`) that differ from live `one.Organizations`.
- Community-centric naming in docs vs current Commerce-centric code.

---

## Attack Scenarios

### 1. Empty-tenant filter bypass (Critical)

**Precondition:** Authenticated user (or any code path) hits EF-backed endpoint without `X-Tenant-Id` on a non-`/admin/` route, **or** any service uses DbContext with empty accessor.

**Effect:** Global filter becomes no-op; reads/writes can span all orgs unless handlers re-filter.

**Examples:** LHDN with empty `ctx.TenantId`; OpsConversation EF without repository org clause; ad-hoc admin tools.

### 2. OpsConversation filter replacement (High)

**Precondition:** Code queries `OpsDbContext.Conversations` without explicit `OrganizationId`.

**Effect:** Soft-delete filter only — cross-tenant conversation listing/mutation.

### 3. Workspace member/invite IDOR (High)

**Precondition:** Any logged-in user.

**Action:** `GET /api/v1/one/workspaces/{victimOrgId}/members` (and invites).

**Effect:** Enumerate emails, names, roles of any workspace.

### 4. CLIENT privilege escalation on invite/remove (High)

**Precondition:** CLIENT membership.

**Action:** Invite as ADMIN or remove admins via membership-only handlers.

**Effect:** Workspace takeover.

### 5. Payment webhook metadata cross-tenant fulfill (High)

**Precondition:** Attacker operates tenant B with valid gateway + webhook secret.

**Action:** Checkout metadata points at tenant A’s `CheckoutSession` / subscription GUID; pay on B; webhook to B’s URL.

**Effect:** A’s session completed / subscription activated without A’s money path integrity; accounting/events can mix orgs.

### 6. Unauthenticated messaging notify (Critical)

**Action:** `POST /api/v1/messaging/notify` with victim `TenantId`.

**Effect:** Unauthorized tenant notifications / side effects depending on handler.

### 7. Public checkout status → portal token mint (High)

**Action:** `GET /public/commerce/checkout/{sessionId}/status` after completion.

**Effect:** Obtain portal magic token without email possession; full portal data for that customer within tenant.

### 8. Public draft PDF / arrears / update-payment (Medium–High)

**Action:** Guess/leak of session or subscription GUIDs.

**Effect:** PII (name, email), commercial amounts, payment update sessions without customer auth.

### 9. Public billing profile (Medium)

**Action:** Known tenant slug.

**Effect:** Tax identifiers and legal entity data for scraping/fraud.

### 10. Resend webhook without secret (Medium–High in misconfig)

**Action:** Forge bounce/complaint for org tags.

**Effect:** Force suppressions (email delivery denial).

### 11. Shared JWT secret for document links (Medium)

**Effect:** If JWT secret leaks, all signed document URLs forgeable; coupling auth and link integrity.

### 12. System tenant platform payment config (Low–Med)

Platform always acts as system tenant — correct for platform keys; ensure no UI confuses this with customer tenant keys.

### 13. LHDN genesis seed hardcodes a real-looking OrganizationId (Low)

Seeded `LhdnTenantConfig` org id is not system `…0001` — confusing multi-tenant bootstrap / possible orphan config.

---

## Recommendations

### P0 — Fix immediately

1. **Fail-closed global filter**
   - Prefer: require tenant for all tenant-scoped modules; throw if empty when handling HTTP admin/SDK traffic.
   - Filter shape: always `e.OrganizationId == ExecutionContext.TenantId` for request-scoped contexts; use a dedicated **system/job accessor** (`IExecutionContextAccessor` with ambient tenant or `IgnoreQueryFilters` + mandatory org) for workers — never “empty means all”.

2. **Combine Ops filters**  
   ```csharp
   builder.HasQueryFilter(x => !x.IsDeleted &&
       (ExecutionContext.TenantId == Guid.Empty /* only if you keep fail-open */ 
        || x.OrganizationId == ExecutionContext.TenantId));
   ```
   Prefer never fail-open; always include `OrganizationId` equality.

3. **Authorize `/messaging/notify`** (internal-only auth, network policy, or remove public map).

4. **Webhook resource ownership**  
   After loading session/subscription, require  
   `entity.OrganizationId == request.TenantId` (and prefer metadata `tenant_id` match). Reject otherwise.

5. **One workspace IDOR**  
   On all `/workspaces/{id}/*` reads: `HasTenantAccessAsync` or system admin.  
   On invite/remove: require role `ADMIN` (not mere membership).

### P1 — Strong isolation hardening

6. **Expand mandatory tenant middleware** beyond `/admin/` to `/lhdn`, `/ops`, and any OrgAdmin module; reject empty tenant with 400.

7. **Public commerce**
   - Bind checkout status to tenant slug or require signed session token.
   - Do not mint portal magic link from unauthenticated status alone; email OTP or HMAC tied to email.
   - Protect update-payment/arrears with portal token or signed link.

8. **Draft document public route** — require same HMAC pattern as final documents.

9. **Repository methods** — prefer `GetXByIdAsync(orgId, id)` everywhere; ban ID-only for multi-tenant aggregates.

10. **Resend webhook** — fail closed if secret missing in non-Development environments.

### P2 — Defense in depth

11. **PaymentWebhookLog** — add optional `OrganizationId` for audit; keep unique on `(Provider, EventId)` if gateways guarantee global IDs.

12. **Separate secrets** — document link HMAC key ≠ JWT signing key.

13. **Architecture tests**
    - Every entity with `OrganizationId` implements `IMustHaveTenant` (except justified).
    - No second `HasQueryFilter` without including tenant predicate.
    - No anonymous MapGroup under module paths except allowlisted public/webhook.

14. **Backfill audits**
    - SQL: count rows with `OrganizationId = '000…000'` per schema.
    - Align docs to `one.Organizations` and current modules (Commerce, not Community).

15. **API key scope** — keys are LHDN-module table but used as platform auth identity; document and optionally scope keys to product capabilities.

16. **Presigned storage** — reject `TenantId == Empty` before writing under `vault/{tenantId}/...`.

17. **Strip webhook secrets from default GET** (or mask); show once on rotate.

---

## File Evidence Notes

| Path | Role in isolation |
|------|-------------------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/BuildingBlocks/Domain/IMustHaveTenant.cs` | Tenant marker |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/BuildingBlocks/Infrastructure/PlatformDbContext.cs` | Global filters + stamp; **Empty bypass** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/BuildingBlocks/Application/IExecutionContextAccessor.cs` | Context contract |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/src/Lazuar.Api/ExecutionContextAccessor.cs` | HTTP Items → TenantId |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs` | Header/slug/route resolution; admin-only enforce; ApiKey skip; platform system tenant |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs` | Key → OrganizationId |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/src/Lazuar.Api/Program.cs` | Pipeline; OrgAdmin policy; platform group |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/src/Lazuar.Api/Infrastructure/Data/PlatformDbContext.cs` | Unused alternate base (no filters) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/*/Infrastructure/*DbContext.cs` | Per-schema models |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Ops/Infrastructure/OpsDbContext.cs` | Soft-delete filter overwrites tenant filter on conversations |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Payments/Infrastructure/Endpoints.cs` | Webhook route + query→header injection |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` | Verify + publish with URL tenant |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Payments/Infrastructure/Repositories/PaymentRepositories.cs` | IgnoreQueryFilters for configs/logs |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs` | Missing org ownership check on session |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicEndpoints.cs` | Public checkout/portal/arrears risks |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Checkout.cs` | Status + magic token mint |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints.cs` | Public profile/docs/draft |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/PublicComplianceEndpoints.cs` | Unsubscribe HMAC; Resend optional verify |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Messaging/Infrastructure/Endpoints.cs` | Unauthenticated notify |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs` | Workspace IDOR; webhook secret return |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/One/Application/Commands/InviteUserToWorkspaceCommand.cs` | Membership-only, not ADMIN |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/One/Application/Commands/RemoveWorkspaceMemberCommand.cs` | Membership-only |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/One/Infrastructure/Workers/SystemGenesisBootstrapperJob.cs` | System tenant seed |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Payments/Infrastructure/PlatformEndpoints.cs` | Superadmin auth + system payment config |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/docs/005-tenant-isolation-mapping-backfilling.md` | Orphan filter hazards / backfill playbook |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/docs/006-payment-webhook-idempotency-backfilling.md` | Webhook cutover + metadata schema |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/docs/architecture-decision-log/009-stateless-webhook-metadata-transmission.md` | Query-string metadata design |

---

### Summary judgment

Lazuar Hub has a **clear multi-tenant spine** (OrganizationId + `IMustHaveTenant` + middleware context + many explicit org parameters). The isolation model is **fail-open when `TenantId` is empty**, **inconsistently enforced at the HTTP boundary** (only `/admin/`), and **partially defeated by EF filter replacement (Ops)** and **heavy `IgnoreQueryFilters` without a uniform ownership assert**. Highest-impact gaps today: **empty-context filter bypass**, **Ops conversation filter**, **unauthenticated messaging**, **One workspace IDOR / CLIENT privilege**, **webhook metadata not bound to path tenant**, and **public commerce/billing unauthenticated data and token issuance**.
