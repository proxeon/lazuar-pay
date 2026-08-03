<!-- Source subagent: 019fc650-3511-7762-8927-4f1203b0d748 -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# API Authentication & Integration Credentials Gap Analysis

## Current Auth Architecture

Lazuar Hub is a modular monolith (`apps/lazuar-api`) with **two concurrent authentication mechanisms** and a **tenant resolution layer**:

| Layer | Component | Path |
|--------|-----------|------|
| JWT (human/browser) | `AddJwtBearer` + cookies | `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/src/Lazuar.Api/Program.cs` |
| API key (machine) | Custom middleware | `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs` |
| Tenant context | Header / slug / API key | `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs` |
| Request context | Claims + `HttpContext.Items` | `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/src/Lazuar.Api/ExecutionContextAccessor.cs` |

### Middleware pipeline (order matters)

From `Program.cs`:

1. `UseExceptionHandler`
2. `UseCors`
3. `UseAuthentication` — JWT Bearer (cookie or `Authorization: Bearer <jwt>`)
4. **`ApiKeyAuthenticationMiddleware`** — if `Authorization` starts with `Bearer sk_live_` / `Bearer sk_test_`
5. **`TenantSecurityMiddleware`** — resolve `TenantId`, inject workspace role (JWT only)
6. `UseAuthorization` — policies like `OrgAdmin`

### Authorization policies

```183:190:apps/lazuar-api/src/Lazuar.Api/Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OrgAdmin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("SUPER_ADMIN", "ADMIN", "API_CLIENT");
    });
});
```

Platform routes:

```320:322:apps/lazuar-api/src/Lazuar.Api/Program.cs
var platformGroup = app.MapGroup("/api/v1/platform")
   .RequireCors()
   .RequireAuthorization(policy => policy.RequireRole("SUPER_ADMIN"));
```

### Dual-auth design intent vs reality

**Intent (partially implemented for LHDN):**

- Humans use JWT cookies via ops/superadmin UIs.
- Machines use `sk_live_` / `sk_test_` API keys for LHDN submissions.
- Keys live under `lhdn."DeveloperApiKeys"`.
- SDKs (`packages/lhdn-sdk-ts`, `packages/lhdn-sdk-dotnet`) authenticate with an API key header.

**Reality:**

- API keys are **product-scoped in storage** (LHDN schema only) but **platform-wide in authorization power** (`API_CLIENT` satisfies `OrgAdmin` everywhere).
- There is **no first-class platform credential model** (One-owned keys, scopes, product grants).
- Developers page is **docs-only** (Scalar OpenAPI), not a credentials console.
- Ops “Developer” UX is **webhooks only**, not API key lifecycle.

---

## JWT Human Auth Flow

### Issuance

JWT generation is a thin HMAC-SHA256 helper:

```6:26:apps/lazuar-api/BuildingBlocks/Infrastructure/JwtService.cs
public interface IJwtService
{
    string GenerateToken(IEnumerable<Claim> claims, string secret, string issuer, string audience, int expiryHours);
}
// ...
// issuer, audience, claims, expires = UtcNow + expiryHours, HmacSha256
```

Config (defaults in code if missing):

- Secret: `Jwt:Secret` or `"secure_development_key_minimum_32_characters_long"`
- Issuer: `Jwt:Issuer` or `"lazuar-api"`
- Audience: `Jwt:Audience` or `"lazuar-clients"`
- Expiry: `Jwt:ExpiryHours` (default **24 hours**)

### Two cookie channels

| Audience | Cookie name | Path/Domain | Role claim |
|----------|-------------|-------------|------------|
| Workspace ops users | `lazuar_auth` | Domain `.lazuar.com` (prod), full site | `CLIENT` or `SUPER_ADMIN` at issue time |
| Platform superadmin | `lazuar_admin_auth` | Path `/api/v1/platform` | always `SUPER_ADMIN` |

JwtBearer `OnMessageReceived` picks cookie by path:

```169:179:apps/lazuar-api/src/Lazuar.Api/Program.cs
var isPlatformRoute = context.Request.Path.StartsWithSegments("/api/v1/platform");
var cookieName = isPlatformRoute ? "lazuar_admin_auth" : "lazuar_auth";
if (context.Request.Cookies.TryGetValue(cookieName, out var token))
{
    context.Token = token;
}
```

### Login endpoints

**Workspace (`/api/v1/one/auth/login`)** — `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs`

1. Email/password against `one.GlobalUsers`.
2. Inactive or bad password → 400 with 401 detail (odd status mapping).
3. `IssueCookie`: JWT in **HttpOnly** cookie; body is only `LoginResponse { user }` — **no access token in JSON**.
4. Claims at issue:
   - `NameIdentifier` = user id
   - `Email`
   - `Role` = `SUPER_ADMIN` if system admin else **`CLIENT`**
   - `is_system_admin`
   - `is_email_verified`
   - `security_stamp` (rotated on password change)

**Public register** issues the same cookie and returns role `"ADMIN"` in the **response DTO only**; JWT role claim is still based on `IsSystemAdmin` (so non-system users get JWT role `CLIENT`). Workspace admin comes later via membership.

**Platform (`/api/v1/platform/auth/login`)** — `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Payments/Infrastructure/PlatformEndpoints.cs`

1. Requires `IsSystemAdmin`.
2. Issues `lazuar_admin_auth` with path scoped to `/api/v1/platform`.
3. Group requires `SUPER_ADMIN`.

### Tenant membership role injection (critical for humans)

JWT alone does **not** carry workspace role `ADMIN`. After login:

1. Frontend stores `ops_active_workspace_id` and sends `X-Tenant-Id` on non-`/one/` requests (`apps/ops-page/src/lib/api-client.ts`).
2. `TenantSecurityMiddleware`:
   - Resolves tenant from `X-Tenant-Id`, `X-Tenant-Slug`, or route `tenantSlug`.
   - For `/api/v1/admin/*`, missing tenant → **400** ProblemDetails.
   - If user is authenticated as a real user Guid, loads membership via `IOneQueryService.GetTenantRoleAsync`.
   - No membership → **403**.
   - Membership found → **adds** `ClaimTypes.Role` = `ADMIN` or `CLIENT` (workspace role).

So `OrgAdmin` for humans effectively means: cookie JWT + tenant header + workspace role `ADMIN` (or system `SUPER_ADMIN`).

### Security stamp

`/one/auth/me` and platform `/auth/me` compare JWT `security_stamp` to DB. Password change rotates stamp and invalidates old cookies **only when `/auth/me` is called** — other endpoints do not re-check stamp on every request.

### Magic link (not API auth)

`MagicLinkTokenService` is HMAC over `subscriptionId:expiry` for commerce checkout access tokens — unrelated to developer API credentials.

### Frontend consumption pattern

Ops uses `credentials: "include"` cookie sessions. **There is no supported “copy JWT for Postman” product flow** in LoginResponse. Machine use of JWT would require either:

- Manually extracting the cookie, or  
- Sending a Bearer JWT (JwtBearer still accepts standard `Authorization: Bearer <jwt>` if present).

Product direction is correct: **integrations should not use JWT**.

---

## API Key / Machine Auth Flow

### Domain model

```6:35:apps/lazuar-api/Modules/Lhdn/Domain/Aggregates/DeveloperApiKey.cs
public class DeveloperApiKey : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; private set; }
    public string Prefix { get; private set; }   // "sk_live_" | "sk_test_" only
    public string KeyHash { get; private set; }  // SHA-256 hex of full plain key
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    // Revoke() sets IsActive = false
}
```

**Schema:** `lhdn."DeveloperApiKeys"`  
Columns: Id, OrganizationId, Name, Prefix, KeyHash, IsActive, CreatedAt  
Indexes: unique `KeyHash`, `OrganizationId`  
**Missing columns vs industry:** scopes, product, last_used_at, expires_at, created_by, rate_limit_tier, key_hint (public id), environment enum beyond prefix, rotation metadata.

### Generation

`GenerateApiKeyCommand`:

1. `ITokenGeneratorService.GenerateSecureToken(40)` → ~base64url entropy.
2. Prefix: `sk_test_` or `sk_live_`.
3. Full plain: `{prefix}{token}`.
4. Store **SHA-256 hash of full plain** only; return plain once.
5. Persist via LHDN repository.

Hashing implementation (shared with password-reset tokens, invites, etc.):

```23:28:apps/lazuar-api/BuildingBlocks/Infrastructure/TokenGeneratorService.cs
public string HashToken(string plainToken)
{
    var bytes = Encoding.UTF8.GetBytes(plainToken);
    var hashBytes = SHA256.HashData(bytes);
    return Convert.ToHexString(hashBytes).ToLowerInvariant();
}
```

Good: secrets not stored plaintext.  
Gaps: no pepper/HMAC with server secret; SHA-256 is fine for high-entropy keys but less flexible for “verify then upgrade” schemes; prefix stored is environment prefix, not a displayable key id.

### Authentication middleware

```29:85:apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs
// Requires Authorization: Bearer sk_live_... | Bearer sk_test_...
// Hash full token → cache ApiKey_{hash} (5 min) → Dapper query lhdn.DeveloperApiKeys
// Claims:
//   NameIdentifier = "api_client"  (NOT a Guid)
//   TenantId = OrganizationId from key
//   IsTestMode = true|false from prefix
//   Role = API_CLIENT
// Items["TenantId"] = tenantId
```

Important behaviors:

1. **Lookup is hard-coded to LHDN DB** via keyed `LhdnSqlConnectionFactory`.
2. **No product / scope check** — any active key authenticates the principal for the whole API.
3. **5-minute memory cache** of `keyHash → tenantId`.
4. Reverse index `TenantKeys_{tenantId}` for workspace-update eviction.
5. On invalid key: **401** and **short-circuit** (does not fall through).
6. If Authorization is a normal JWT, middleware leaves user as JwtBearer set it.

### Tenant middleware bypass for API keys

```22:26:apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs
if (context.User.Identity?.AuthenticationType == "ApiKey")
{
    await _next(context);
    return;
}
```

API clients:

- Do **not** need `X-Tenant-Id` (tenant is embedded in the key).
- Do **not** go through membership/role checks.
- Already have `TenantId` in `HttpContext.Items`.

### Revocation & cache eviction

`RevokeApiKeyCommand`:

- Org ownership check.
- Soft revoke `IsActive = false`.
- Publishes `ApiKeyRevokedIntegrationEvent(OrganizationId, KeyHash)`.
- Handler removes `ApiKey_{KeyHash}` from `IMemoryCache` (closes 5-minute revoke window for that entry).

`WorkspaceUpdatedIntegrationEventHandler` bulk-evicts all cached keys for a tenant.

**Gaps:**

- No multi-instance distributed cache invalidation (memory-only; multi-pod revoke lag = up to TTL on other instances).
- No audit log of who revoked / when used.
- `listApiKeys` is in TypeSpec/SDK but **not implemented** in C# endpoints.

### Management endpoints (dashboard-intended)

All under `/api/v1/lhdn` with **`RequireAuthorization("OrgAdmin")`**:

| Method | Path | Implemented? |
|--------|------|----------------|
| POST | `/api-keys` | Yes — generate |
| DELETE | `/api-keys/{id}` | Yes — revoke |
| GET | `/api-keys` | **No** (spec + SDK yes) |
| POST | `/documents` | Yes |
| GET | `/documents/{internalId}` | Yes |
| POST | `/documents/{internalId}/cancel` | Yes |
| POST/GET/DELETE | `/webhooks` | Yes |
| PUT | `/workspaces/{id}/lhdn-certificate` | Yes |
| POST | `/taxpayer/validate` | Spec/SDK only — **not in Endpoints.cs** |

Generate returns only:

```json
{ "plain_key": "sk_live_..." }
```

No id, prefix, created_at, environment, or “copy once” metadata envelope.

### Test mode semantics

- Key prefix drives claim `IsTestMode`.
- `SubmitTaxDocumentCommand` uses `_executionContext.IsTestMode`:
  - **Skips credit balance check**
  - **Skips credit deduction**
  - Flags `TaxDocument.IsTestMode` for UI filtering
- This is **billing sandbox**, not necessarily a separate LHDN MyInvois environment switch at the gateway level (tenant LHDN config may still point wherever credentials are configured).

### Rate limiting

- **No inbound** ASP.NET rate limiting on API-key or JWT routes.
- **Outbound** rate limiting only inside `LhdnGatewayAdapter` toward MyInvois (token buckets per clientId for login/submit/poll/TIN/cancel).
- Billing credits act as a coarse commercial throttle for **live** LHDN submits, not a request-rate limiter.

### SDKs

TS (`packages/lhdn-sdk-ts/src/index.ts`) and .NET use Kiota `ApiKeyAuthenticationProvider` with header name `"Authorization"` and the raw `apiKey` string.

Middleware **requires** the literal prefix `Bearer sk_...`.  
If the SDK sets `Authorization: sk_live_...` **without** `Bearer `, authentication fails.  
Integrators must pass `apiKey: "Bearer sk_live_..."` or the factory must prepend `Bearer ` — neither is documented clearly. This is a real DX footgun.

### Postman collection note

`docs/postman/postman_collection.json` is **MyInvois / LHDN IdentityServer `client_credentials`** against Malaysia’s portal — **not** Lazuar Hub OAuth. Do not confuse government OAuth with Lazuar integration auth.

---

## Developers Page Reality Check

App: `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/developers-page`

| What it is | What it is not |
|------------|----------------|
| Next.js hub landing with module cards | Credential generation UI |
| Scalar OpenAPI per product (`/one`, `/ops`, `/billing`, `/lhdn`) | Login / workspace selector for keys |
| ADR 007 product-scoped docs rendering | “Get API keys” onboarding |
| Reads `packages/api-spec/dist/<module>/openapi.yaml` | Key vault, rotation, usage analytics |

Landing modules: One, Ops, Billing, LHDN — all **references**, not consoles.

`docs-lhdn.tsp` declares:

```tsp
@useAuth(ApiKeyAuth<ApiKeyLocation.header, "Authorization"> | BearerAuth)
```

`modules/lhdn/routes.tsp` still annotates operations with `@useAuth(BearerAuth)` only — docs mixed-message between JWT Bearer and API key.

ADR 006 correctly separates TypeSpec edge contracts from internal MediatR contracts; that is orthogonal but explains why auth semantics must be modeled carefully at the edge without leaking into module contracts.

### Ops-page “Developer” section

Routes:

- `/developer/webhooks` — outbound workspace webhooks (`One` module)
- `/developer/logs` — delivery logs

**No page** for:

- List / create / revoke Lazuar API keys  
- Show `sk_live_` / `sk_test_` once  
- Product scopes  
- Usage / last used  

Sidebar labels this “Developer” but it is **outbound webhook settings**, not integration credentials.

**Conclusion:** Product owner assessment is accurate — developers-page is backend API docs, not the integration credentials flow people expect (Stripe-like “create key → use key on API”).

---

## External vs Internal Contract Separation

### ADR 006 (edge vs internal)

- TypeSpec = HTTP edge (React apps, third parties, mobile).
- Module Contracts = MediatR commands/events only.
- Endpoints map DTO → Command with **security context injected** (`ctx.TenantId`, etc.).

Implication for integration auth: credentials and principal type must be resolved **before** endpoint mapping (`ExecutionContextAccessor`), then commands receive `OrganizationId` without trusting client body for tenant.

### ADR 007 (product-scoped docs)

- Separate OpenAPI per product for DX and reduced domain leakage.
- Does **not** implement product-scoped **credentials**.
- Gap: docs are product-scoped; **authz is not**.

### Spec vs implementation drift (auth-relevant)

| Spec | Implementation |
|------|----------------|
| GET `/lhdn/api-keys` | Missing |
| POST `/lhdn/taxpayer/validate` | Missing |
| LHDN docs dual ApiKey + Bearer | C# accepts JWT (OrgAdmin roles) or sk_ key |
| One routes BearerAuth | Reality is cookie session for browsers |
| LoginResponse has no token field | Matches cookie-only design |

---

## Security Gaps

### Critical

1. **`API_CLIENT` is over-privileged**  
   Same `OrgAdmin` policy gates LHDN **and** `/admin/commerce`, `/admin/billing`, `/admin/communications`.  
   A key meant for e-invoice submit can, if used against those routes, act as org admin machine identity for:
   - products, subscribers, payment configs  
   - billing ledger, credit top-ups  
   - email configuration  
   - **generating more API keys and rotating LHDN certificates**  
   There is **no scope** like `lhdn:documents:write`.

2. **API key auth bypasses membership but reuses admin policy**  
   Design couples “machine can call admin surfaces” to “human org admin.” That is unsafe for third-party integrations.

3. **Messaging `/messaging/notify` has no `RequireAuthorization`**  
   Unauthenticated surface if reachable.

4. **Default JWT secret and cookie setup in non-prod**  
   Hardcoded development secret fallback if config missing — catastrophic if that ships.

### High

5. **No inbound rate limits / abuse controls** on public API key endpoints (brute force on keys is mitigated by high entropy, but request flooding / credit burn / dependency abuse is not).

6. **In-memory API key cache only**  
   Multi-instance revoke/workspace update is eventually consistent up to 5 minutes per instance; event handlers only clear **local** cache.

7. **Key management endpoints protected only by OrgAdmin**  
   With a stolen API key, attacker can mint/revoke keys (`POST/DELETE /lhdn/api-keys`) unless routes are split.

8. **No last-used tracking / anomaly detection** for keys.

9. **Security stamp not enforced on every request**  
   Stolen JWT cookie remains valid until expiry unless `/auth/me` path is hit (or stamp-aware middleware is added).

### Medium

10. **`listApiKeys` missing** → operators cannot inventory keys without DB access; encourages long-lived orphan keys.

11. **Prefix is only `sk_live_` / `sk_test_`** — cannot display “key ending in …” without storing a public hint.

12. **SDK Authorization header format ambiguity** (`Bearer` prefix).

13. **Generate response is plain_key only** — no key id to revoke later without listing (and list is missing).

14. **Idempotency is LHDN-submit only** — not a general API platform feature.

15. **Platform vs workspace cookie split is good**, but platform login lives under Payments `PlatformEndpoints` — odd ownership for identity.

16. **Ops role policy** requires `CLIENT` or `ADMIN` JWT/workspace roles — `API_CLIENT` **cannot** call Ops chat (good), but still can call other OrgAdmin modules (bad asymmetry).

### Low / design debt

17. Magic link and password-reset tokens share `ITokenGeneratorService` hashing — fine, but mixed concerns.

18. TypeSpec auth annotations do not match cookie reality for One.

19. `Login` returns 400 for bad credentials instead of 401 (info-leak / client handling inconsistency).

---

## Missing Integration Credential Lifecycle

Ideal lifecycle vs current:

| Stage | Industry norm | Lazuar today |
|-------|---------------|--------------|
| Discover | Developer portal “API keys” | Docs only |
| Create | Named key, env, scopes, once-shown secret | LHDN POST only; name + is_test_mode; no UI |
| Store | Hash at rest; show prefix | Hash yes; weak prefix |
| Use | `Authorization: Bearer sk_...` on integration APIs | Works for LHDN if Bearer+sk; overpowered |
| Scope | Per product/action | None |
| Restrict | IP allowlist, rate limit | Credits on live LHDN only |
| Observe | last_used, request logs | None |
| Rotate | dual-key grace period | Revoke + create only |
| Revoke | instant, audited | Soft flag + local cache eviction |
| Expire | optional TTL | None |
| OAuth M2M | client_id + client_secret → short JWT | **None** |
| Webhooks | separate signing secret | One workspace webhooks + LHDN webhook subs (partial) |

### Product-scoped vs platform-wide

| Dimension | Current |
|-----------|---------|
| **Storage** | LHDN-only table |
| **Issuance API** | Under `/lhdn/api-keys` |
| **Auth middleware** | Global (any route) |
| **Authorization** | Global `API_CLIENT` ∈ `OrgAdmin` |
| **Docs** | Product-scoped Scalar |
| **UX** | No credentials UX |

So credentials are **LHDN-owned artifacts with platform-wide effective power** — the worst of both models: not reusable cleanly as platform keys, not constrained to LHDN.

### Human JWT for integrations (anti-pattern status)

| Concern | Status |
|---------|--------|
| Cookie-bound to browser apps | Yes |
| 24h long-lived without refresh token | Yes |
| Tied to user security stamp | Weak enforcement |
| Requires X-Tenant-Id for admin | Yes for JWT |
| User can leave workspace / change password | Brittle for machines |
| Product says “don’t use JWT for integrations” | Correct; machine path is incomplete |

---

## Industry Comparison (Stripe, Paddle, Twilio keys)

| Capability | Stripe | Twilio | Paddle | Lazuar (now) |
|------------|--------|--------|--------|--------------|
| Secret keys `sk_live` / `sk_test` | Yes | Auth token / API key | API key + vendor auth | Prefix mimic only for LHDN |
| Restricted keys / scopes | Yes (Stripe) | IAM / permissions | Limited | **No** |
| Publishable vs secret | `pk_` vs `sk_` | Account SID + token | Client-side tokens separate | **No pk_ model** |
| Dashboard create/reveal once | Yes | Yes | Yes | **No UI** |
| Roll / expire | Yes | Yes | Yes | Revoke only |
| Last used | Yes | Yes | Varies | **No** |
| Webhook signing secret separate | `whsec_` | Yes | Yes | Workspace webhook secret (One); LHDN has own |
| OAuth for apps / Connect | Extensive | Yes | Seller auth | **None** |
| Docs + “use this key” | Coupled | Coupled | Coupled | Docs **decoupled** from keys |
| Per-product keys | Product-scoped permissions | Service SIDs | Product APIs | LHDN table only |
| Rate limits | Strong | Strong | Strong | Outbound MyInvois only |
| Test mode free of charges | Yes | Trial | Sandbox | Credits skipped for `sk_test_` |

Stripe-like pattern Lazuar partially copied:

- `sk_live_` / `sk_test_` naming  
- Hash storage  
- OrgAdmin manage endpoints  

Stripe-like pattern **not** copied:

- Restricted keys  
- Dashboard  
- Separation of secret key from dashboard session  
- Request logging per key  
- Clear rule: secret keys never call account-management endpoints that mint more secrets without tighter policy  

---

## Auth Matrix (Public vs Admin vs Integration)

### Route classes

| Class | Path pattern | Auth today | Tenant |
|-------|--------------|------------|--------|
| Health | `/health` | None | N/A |
| One public auth | `/one/public/register`, `/one/auth/login`, forgot/reset | Anonymous | N/A |
| One session | `/one/auth/me`, `/me/*`, workspaces… | JWT cookie / Bearer JWT | Membership checks ad hoc |
| Admin surfaces | `/admin/commerce`, `/admin/billing`, `/admin/communications` | `OrgAdmin` = SUPER_ADMIN \| ADMIN \| **API_CLIENT** | JWT: `X-Tenant-Id` required; API key: from key |
| LHDN “SDK” | `/lhdn/*` | Same `OrgAdmin` | Same |
| Ops AI | `/ops/*` | Roles `CLIENT` or `ADMIN` only | Tenant required in handlers |
| Platform | `/api/v1/platform/*` | `SUPER_ADMIN` | Forced system tenant Guid in middleware |
| Public commerce/billing | `/public/commerce`, `/public/billing` | Mostly anonymous (checkout) | Public tokens / magic links |
| Payment webhooks | `/webhooks/payments` | Gateway signature (not JWT) | Derived from payload |
| Messaging | `/messaging/notify` | **None** | N/A |

### Who can call what with an LHDN API key?

| Surface | Allowed by policy? | Intended? |
|---------|--------------------|-----------|
| POST `/lhdn/documents` | Yes | Yes |
| Manage `/lhdn/api-keys` | Yes | **Should be dashboard/JWT only** |
| Update LHDN certificate | Yes | **Should be dashboard only** |
| `/admin/commerce/*` | Yes (policy) | **No** |
| `/admin/billing/*` | Yes | **No** (maybe read credits later with scope) |
| `/ops/*` | No (role list) | N/A |
| `/one/*` authenticated | Authenticated as `api_client` string user id — dangerous if endpoints assume Guid user | **No** |
| Platform | No | N/A |

### JWT human (workspace ADMIN) intended matrix

| Surface | Expected |
|---------|----------|
| Ops UI + admin APIs | Yes with `X-Tenant-Id` |
| Generate LHDN keys | Yes (when UI exists) |
| Day-to-day LHDN from ERP | Prefer API key, not user JWT |
| Platform superadmin | Separate cookie / role |

---

## What “Proper Integration Credentials” Should Look Like vs Current State

### Target model (recommended)

**Platform credential aggregate in One (or shared Identity), not buried only in LHDN:**

```
ApiCredential
  Id, OrganizationId
  Name, PublicId (e.g. key_abc123)
  KeyHash
  Environment: live | test
  Scopes: [ "lhdn.documents:write", "lhdn.documents:read", ... ]
  Products: [ "lhdn" ]  // or scope prefixes
  CreatedByUserId, CreatedAt
  LastUsedAt, ExpiresAt?
  IsActive, RevokedAt, RevokedBy
```

**Auth pipeline:**

1. Accept `Authorization: Bearer sk_live_...` (normalize; also accept raw key → rewrite).
2. Resolve credential → set `TenantId`, `CredentialId`, `Scopes`, `IsTestMode`.
3. Authorization: policies like `Scope:lhdn.documents:write`, **not** blanket `OrgAdmin`.
4. Management APIs: JWT + workspace ADMIN only; never `API_CLIENT`.

**Optional later:** OAuth2 client_credentials for partners (`client_id`/`client_secret` → short-lived access token with scopes) when multi-tenant SaaS apps need delegated access. Not required for v1 if restricted secret keys exist.

**UX:**

- Ops (or developers portal authenticated section): Create / list / revoke / rotate.  
- Reveal plain secret once.  
- Link to product docs with “use this key.”  
- developers-page can remain public docs + deep link “Manage keys in console.”

### Current state (one-liner)

**LHDN-local hashed keys with Stripe-like prefixes, global OrgAdmin power, no scopes, no list endpoint, no management UI, docs-only developer hub.**

---

## Recommendations (Prioritized)

### P0 — Security (do before promoting API keys)

1. **Split policies**  
   - `OrgAdmin` = human SUPER_ADMIN \| ADMIN only.  
   - `Integration` / scope policies for machine principals.  
   - Explicitly **deny** `API_CLIENT` on key mint/revoke, certificate upload, payment config, email config.

2. **Scope enforcement on LHDN**  
   At minimum hardcode: API keys may only access document submit/status/cancel (+ optional TIN validate). Everything else JWT-only.

3. **Fix `/messaging/notify` auth** (or internal-only network policy).

4. **Ensure production JWT secret is mandatory** (fail boot if default/missing).

### P1 — Complete LHDN credential MVP (product-honest)

5. Implement **GET `/lhdn/api-keys`** (and taxpayer validate if product needs it).  
6. Return richer create response: `{ id, name, prefix/public_id, plain_key, created_at, is_test_mode }`.  
7. Store **key hint** (e.g. last 4 of secret or random public id) for list UI.  
8. **Ops UI** under Developer: API Keys (create test/live, copy once, revoke).  
9. SDK: always send `Authorization: Bearer ${apiKey}`; accept key with or without `Bearer`.  
10. Middleware: accept both `Bearer sk_` and raw `sk_` for robustness.

### P2 — Platform credentials (product owner’s real ask)

11. Move credential aggregate to **One** (or `Platform.Identity`) with **product + scope**.  
12. LHDN generation becomes “create platform key with scopes including `lhdn.*`” or product-specific keys that still resolve centrally.  
13. `ApiKeyAuthenticationMiddleware` reads **One** table (or shared store), not hard-coded LHDN SQL.  
14. developers-page: “Authentication” guide per product + link to console; optional authenticated key page later.  
15. Document clearly: **JWT/cookies = humans; secret keys = machines; never embed user JWT in ERP.**

### P3 — Lifecycle & scale

16. `LastUsedAt` async update (throttled).  
17. Distributed cache (Redis) for key lookups + revoke pub/sub.  
18. Inbound rate limits per key / per tenant.  
19. Key rotation (create new, dual-valid window, revoke old).  
20. Optional expiry and IP allowlists.  
21. Audit log stream for credential events.  
22. Consider OAuth2 client_credentials when third-party *apps* (not just tenant ERPs) appear.

### P4 — Docs & contract hygiene

23. Align TypeSpec `@useAuth` with reality (ApiKey for integration routes; cookie/Bearer for session).  
24. Product-scoped OpenAPI should **exclude** key-management routes from pure “integration” docs or mark them “Dashboard only.”  
25. ADR: “Separation of session auth and integration credentials.”

---

## File-by-File Notes

### Middleware & host

| File | Notes |
|------|-------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/src/Lazuar.Api/Program.cs` | JWT setup, cookie picker, `OrgAdmin` includes `API_CLIENT`, pipeline order, module maps, platform SUPER_ADMIN group, event bus subscriptions for key revoke / workspace update. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs` | Sole machine auth; LHDN-only SQL; claims `api_client` + `API_CLIENT` + `IsTestMode`; 5m cache; 401 on bad key. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs` | Skips all membership logic for ApiKey; admin routes need tenant header for JWT; injects workspace Role claim. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/src/Lazuar.Api/ExecutionContextAccessor.cs` | TenantId from Items; UserId parses NameIdentifier (fails for `api_client`); IsTestMode from claim. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/src/Lazuar.Api/EventHandlers/ApiKeyRevokedIntegrationEventHandler.cs` | Local cache eviction on revoke. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/src/Lazuar.Api/EventHandlers/WorkspaceUpdatedIntegrationEventHandler.cs` | Evicts all cached keys for tenant. |

### Building blocks

| File | Notes |
|------|-------|
| `.../BuildingBlocks/Infrastructure/JwtService.cs` | Symmetric JWT writer only; no refresh, no validation helper. |
| `.../BuildingBlocks/Infrastructure/MagicLinkTokenService.cs` | Commerce access tokens; reuses Jwt secret; not API keys. |
| `.../BuildingBlocks/Infrastructure/TokenGeneratorService.cs` | CSPRNG + SHA-256 hex; used for API keys and One tokens. |
| `.../BuildingBlocks/Application/IExecutionContextAccessor.cs` | Documents IsTestMode = sandbox API key. |
| `.../BuildingBlocks/Application/ITokenGeneratorService.cs` | Generate + Hash interface. |

### One (identity / session)

| File | Notes |
|------|-------|
| `.../Modules/One/Infrastructure/Endpoints.cs` | Register/login/logout/me; cookie issue; workspace CRUD; **webhooks** (not API keys); JWT claims; no credential APIs. |
| `.../Modules/One/Domain/TenantMembership.cs` | Roles ADMIN/CLIENT per workspace. |
| `.../Modules/One/Domain/GlobalUser.cs` | SecurityStamp rotation on password change. |
| `.../Modules/One/Infrastructure/Services/OneQueryService.cs` | GetTenantRoleAsync used by TenantSecurityMiddleware. |
| `packages/api-spec/modules/one/routes.tsp` | BearerAuth annotations; no api-keys routes. |
| `packages/api-spec/modules/one/models.tsp` | LoginResponse = user only (no token). |

### LHDN (only API key product)

| File | Notes |
|------|-------|
| `.../Modules/Lhdn/Domain/Aggregates/DeveloperApiKey.cs` | Minimal aggregate; revoke soft-delete. |
| `.../Modules/Lhdn/Application/Commands/GenerateApiKeyCommand.cs` | sk_test_/sk_live_ mint. |
| `.../Modules/Lhdn/Application/Commands/RevokeApiKeyCommand.cs` | Ownership + event. |
| `.../Modules/Lhdn/Application/Commands/SubmitTaxDocumentCommand.cs` | IsTestMode skips credits; idempotency; billing deduct. |
| `.../Modules/Lhdn/Infrastructure/Endpoints.cs` | Entire group OrgAdmin; generate/revoke keys; **no list**; no TIN validate. |
| `.../Modules/Lhdn/Infrastructure/LhdnDbContext.cs` | Unique KeyHash. |
| `.../Modules/Lhdn/Infrastructure/Migrations/20260627124829_InitialLhdnSchema.cs` | Table shape without lifecycle columns. |
| `.../Modules/Lhdn/Contracts/Events/ApiKeyRevokedIntegrationEvent.cs` | Cache invalidation event. |
| `.../Modules/Lhdn/Infrastructure/Gateways/LhdnGatewayAdapter.cs` | Outbound MyInvois rate limits only. |
| `.../Modules/Lhdn/README.md` | Compliance focus; little on developer keys. |
| `packages/api-spec/modules/lhdn/routes.tsp` | Full key CRUD in contract + webhooks + docs. |
| `packages/api-spec/modules/lhdn/models.tsp` | ApiKeyDto without last_used/scopes. |
| `packages/api-spec/docs-lhdn.tsp` | Dual ApiKeyAuth \| BearerAuth at service level. |
| `packages/lhdn-sdk-ts/src/index.ts` | ApiKey on Authorization header. |
| `packages/lhdn-sdk-dotnet/src/LhdnClientFactory.cs` | Same + auto Idempotency-Key. |

### Other modules (auth posture)

| File | Notes |
|------|-------|
| `.../Modules/Commerce/Infrastructure/Endpoints.cs` | `/admin/commerce` OrgAdmin; `/public/commerce` open. |
| `.../Modules/Billing/Infrastructure/Endpoints.cs` | `/admin/billing` OrgAdmin (API_CLIENT can hit). |
| `.../Modules/Communications/Infrastructure/Endpoints.cs` | OrgAdmin. |
| `.../Modules/Ops/Infrastructure/Endpoints.cs` | CLIENT\|ADMIN only — excludes API_CLIENT. |
| `.../Modules/Messaging/Infrastructure/Endpoints.cs` | **No auth.** |
| `.../Modules/Payments/Infrastructure/PlatformEndpoints.cs` | Superadmin cookie login. |
| `.../Modules/Payments/Infrastructure/Endpoints.cs` | Payment webhooks group. |

### Frontends & docs

| File | Notes |
|------|-------|
| `apps/developers-page/app/page.tsx` | Module cards → Scalar only. |
| `apps/developers-page/app/lhdn/route.ts` | LHDN OpenAPI Scalar. |
| `apps/developers-page/lib/openapi.ts` | Loads dist YAML. |
| `apps/ops-page/src/lib/api-client.ts` | Cookie session + X-Tenant-Id. |
| `apps/ops-page/src/modules/workspace/pages/DeveloperSettingsPage.tsx` | Webhooks only. |
| `apps/ops-page/src/App.tsx` / `Sidebar.tsx` | Developer = webhooks + logs. |
| `docs/architecture-decision-log/006-...` | Edge vs internal contracts. |
| `docs/architecture-decision-log/007-...` | Product-scoped API references. |
| `docs/architecture-decision-log/014-apps.md` | Documents DeveloperApiKey as LHDN entity. |
| `docs/architecture-decision-log/020-...` | Integration roadmap (gateways, tax, etc.) — not Lazuar credential model. |
| `docs/postman/postman_collection.json` | **MyInvois** OAuth client_credentials, not Lazuar. |

### Tests

| File | Notes |
|------|-------|
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/LhdnRateLimitingTests.cs` | Named rate limiting; actually tests submit happy-path / credits setup — not inbound API rate limits. |

---

## Executive Summary for Product Owner

**What works today**

- Solid **human session** model: HttpOnly JWT cookies, workspace membership, admin tenant header, platform admin isolation.  
- **Prototype machine auth** for LHDN: hashed `sk_live_` / `sk_test_` keys, test mode skips credits, revoke + cache eviction, SDKs exist.  
- **Docs hub** is product-scoped (ADR 007) and fine as a reference site.

**What does not meet “proper integration credentials”**

- Keys are **LHDN-table-scoped but authorization-global**.  
- **No scopes, no UI, no list, no last-used, no rotation, no inbound rate limits.**  
- **developers-page does not generate credentials**; ops Developer is webhooks.  
- **JWT is correctly unsuitable** for ERP/server integrations; the replacement is incomplete and overpowered.  
- **No OAuth2 client credentials** for Lazuar (Postman file is government MyInvois, not Lazuar).

**Strategic fix**

Treat integration credentials as a **platform capability** (issue/manage in console under the workspace; hash at rest; authenticate globally; **authorize by scope/product**). Keep JWT strictly for humans. Until scopes exist, **never** put `API_CLIENT` in `OrgAdmin`.
