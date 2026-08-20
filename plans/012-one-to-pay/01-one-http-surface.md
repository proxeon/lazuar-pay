# 01 — One HTTP façade that focused Pay must call (and must not call)

**Date:** 20 August 2026  
**Slice:** first connection — `/me`, tenants, members/invites, apps, api-keys, `authz/check`  
**Kind:** analysis only. No C# implementation. No Pay/One product-code change.

**Repos / HEAD**

| Repo | Path | Short SHA | Full SHA | Tip |
|------|------|-----------|----------|-----|
| Focused Pay (this tree) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `6ca8f19f` | `6ca8f19f4b28c056f852b7b579b5b30428e48ad6` | `feat(pay): add TypeSpec package for the focused Pay host` (2026-08-20 21:00:06 +0800) |
| Lazuar One (sibling, HTTP SoT) | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `0f79fe4` | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` | `WIP:` (2026-08-20 21:24:22 +0800) |

`git rev-parse --short HEAD` and `git log -1` were run in both working copies on 20 Aug 2026. One’s tip is a WIP commit; Pay’s tip is the focused-host TypeSpec package. If either tree moves, re-pin the SHAs before treating path lists as frozen.

**What “Pay” means in this paper**

- The **new focused host** is `apps/lazuar-pay` (`Lazuar.Pay`), listening on **http://localhost:8081**. Today it only serves `GET /health` and `GET /v1/health`. It does not yet call One.
- The **old modular Pay** (`apps/lazuar-api` in this same Pay repo, `packages/api-spec/modules/one`) is **not** the caller. It is the museum this rewrite leaves. Its `/one/auth/me` and `/one/workspaces` routes are a second identity plane. Do not call them. Do not copy them into `packages/pay-spec`.
- The **HTTP server Pay calls** is One’s `lazuar-api` at **http://localhost:8080**, product surface **`/api/v1`**.

This paper maps One’s real TypeSpec + Minimal API onto Pay’s first-slice uses from:

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/02-one-integration.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/03-first-slice.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/plans/017-evals/08-dogfood-then-serve.md` §6 (from line 704)

skip / now / later in the route table:

- **now** — Pay must hit this route to prove “connected” or to finish One-side first-slice steps 1–5 (register SPA, `/me`, create-or-pick tenant, copy-link invite, mint `lzr_sk_`, `authz/check member`).
- **later** — still on this façade, still a Pay use, not required for the first two HTTP round-trips. Do not implement C# for these in this analysis slice.
- **skip** — Pay must not call this route (wrong door, staff-only, missing on purpose, or a Zitadel/SCIM/OpenFGA plane Pay does not hold).

---

## 1. Method — what was opened

Nothing was implemented. The following were read in full or in the cited ranges.

### Pay plans (consumer intent)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/01-product.md` — merchants in One; buyer plane is Pay; dogfood sentence (MEMBER sees ops, VIEWER cannot charge).
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/02-one-integration.md` — entire file. HTTP tables for tenancy, people, machines, authz; secrets Pay must not hold; “do not call `POST /platform/tenants`”.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/03-first-slice.md` — entire file. One-side stop-after-this list; Pay-side money list.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/11-checklist.md` — `NP-ONE-001` … `NP-ONE-022`.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/12-first-slice-tracker.md` — ordered S0 steps 1–7.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/05-language.md` — “Pay calls One over HTTP”; language debate is out of this paper’s implement scope.

### One first-party contract (producer intent)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/plans/017-evals/08-dogfood-then-serve.md` §6 (from “First-party product checklist — what a sibling Lazuar app must call”, ~line 704 through §6.12).

### One TypeSpec (authoritative HTTP contract)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/main.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/common/models.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/platform/models.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/platform/routes.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/tenants/models.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/tenants/routes.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/apps/models.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/apps/routes.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/api-keys/models.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/api-keys/routes.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/authz/models.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/authz/routes.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/enterprise/routes.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/webhooks/routes.tsp` (adjacent; first-slice step 6, not this HTTP slice’s implement target)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/scim/README.md` (protocol is **not** `/api/v1`)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/dist/openapi.yaml` — generated OpenAPI 3.0.0, `servers[0].url = http://localhost:8080/api/v1`

### One runtime (what the host actually maps)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Program.cs` — `MapGroup("/api/v1")`, JSON snake_case, endpoint map.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Properties/launchSettings.json` — `http://localhost:8080`.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/appsettings.Development.json` — CORS, `ApiBaseUrl`, Invite token return, Zitadel authority `:8085`, OpenFGA disabled by default.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/appsettings.json` — base CORS / Invite defaults.
- Endpoint files: `Features/Platform/MeEndpoints.cs`, `PlatformTenantEndpoints.cs`, `Features/Tenants/TenantEndpoints.cs`, `MemberEndpoints.cs`, `Features/Apps/AppEndpoints.cs`, `Features/ApiKeys/ApiKeyEndpoints.cs`, `Features/Authz/AuthzEndpoints.cs`, `AuthzObjectRules.cs`, `AuthzService.cs`, `Features/Enterprise/ScimUserEndpoints.cs`.
- Auth: `Infrastructure/Auth/AuthenticationExtensions.cs`, `JwtAccessTokenGuard.cs`, `ApiKeyAuthenticationHandler.cs`, `ApiKeyDefaults.cs`, `ApiKeyScopeHelper.cs`, `ScimTokenDefaults.cs`, `AuthorizationPolicies.cs`.
- Tenancy: `Infrastructure/Tenancy/ActiveTenantHint.cs`, `TenantAccessService.cs`, `TenantPermission.cs`.
- Domain: `Domain/Tenants/MembershipRoles.cs`, `TenantPermissions.cs`.
- Rate limit: `Infrastructure/RateLimiting/RateLimitPolicies.cs`, `Configuration/RateLimitOptions.cs`.
- Invite: `Configuration/InviteOptions.cs`, `Features/Tenants/MembershipService.cs` (owner-invite rejection).
- Webhook catalog (adjacent events Pay will subscribe later): `Features/Webhooks/WebhookEventCatalog.cs`.

### Generated / client / docs / examples

- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-type-dotnet/Lazuar.One.ApiContracts.cs` — `MeResponse`, `TenantSummary` (`JsonPropertyName` snake_case).
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/one-client/src/createClient.ts`, `authz.ts`, `index.ts`, `README.md`, `package.json` (`private: true`, unpublished).
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/README.md` — ports, `GET http://localhost:8080/api/v1/me`.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-docs/docs/reference/ports.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-docs/docs/integrations/authz.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-docs/docs/recipes/authz-check.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/examples/node-api-key/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-app/src/lib/inviteLink.ts`, `sessionKeys.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/issues/018-zitadel-invite-user-noop.md` — InviteUser closed by **deletion**.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/scripts/seed-platform-spa-clients.sh`

### Focused Pay host (callee-side emptiness)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Properties/launchSettings.json` — `http://localhost:8081`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/main.tsp` — health only; README forbids importing One routes.
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/dist/openapi.yaml` — `servers[0].url = http://localhost:8081`, path `/v1/health` only.
- Contrast (do not reuse): `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/one/routes.tsp` — old `/one/auth/me`, `/one/workspaces`.

### Isolation / tests used as evidence of wire paths

- `IsolationRouteMatrix.cs` lists `/api/v1/me` as caller-scoped.
- `RateLimitPoliciesTests.cs` asserts `Match("/api/v1/me")` is **null** (GET `/me` is not abuse-limited).
- `TenantCreateListGetTests.cs` asserts `/me.tenants[].permissions` is JSON array of ROLE-03 strings.

---

## 2. One base URL and path prefix as they actually are locally

### Host, port, path prefix

| Fact | Value | Evidence |
|------|-------|----------|
| API process | `apps/lazuar-api`, assembly `Lazuar.One.Api` | One README “API host (.NET)” |
| Local listen | **`http://localhost:8080`** | `Properties/launchSettings.json` `applicationUrl`; `pnpm api:dev` |
| Product path prefix | **`/api/v1`** | `Program.cs`: `var api = app.MapGroup("/api/v1").RequireCors();` comment: “matches TypeSpec servers base path /api/v1” |
| TypeSpec servers | `http://localhost:8080/api/v1` (local), `https://api.lazuar.com/api/v1` (placeholder production) | `packages/api-spec/main.tsp` `@server`; OpenAPI `servers` at end of `dist/openapi.yaml` |
| Config echo | `App:ApiBaseUrl` = `http://localhost:8080/api/v1` | `appsettings.Development.json` |
| Client package default in docs | `baseUrl: 'http://localhost:8080/api/v1'` | `packages/one-client/README.md`, `CreateClientOptions` comment |
| Example env | `LAZUAR_API_BASE=http://localhost:8080/api/v1` | `examples/node-api-key/.env.example` |
| Postman | `baseUrl` = `http://localhost:8080/api/v1`, `rootUrl` = `http://localhost:8080` | `examples/postman/local.postman_environment.json` |
| Unversioned liveness | `GET http://localhost:8080/health`, `/health/live`, `/health/ready` | `Infrastructure/Health/HealthEndpoints.cs`; **not** under `/api/v1` except the alias |
| Versioned liveness alias | `GET http://localhost:8080/api/v1/health` | `Program.cs` `api.MapGet("/health", …).AllowAnonymous()` |
| Versioned hello | `GET http://localhost:8080/api/v1/` → `{ "name": "lazuar-one-api", "version": "v1" }` | `Program.cs` |
| Built-in OpenAPI | `GET http://localhost:8080/openapi/v1.json` | One README |
| SCIM (not this prefix) | `http://localhost:8080/scim/v2/tenants/{tenantId}/Users` | `ScimUserEndpoints.cs` `MapGroup("/scim/v2/tenants/{tenantId:guid}/Users")` on the **app**, not on `api` |

**How a Pay caller must concatenate.** TypeSpec paths are relative to the server URL. OpenAPI lists `/me`, not `/api/v1/me`. The ASP.NET group adds the prefix. Pay therefore calls:

```text
{ONE_API_BASE}/me
where ONE_API_BASE = http://localhost:8080/api/v1
absolute = http://localhost:8080/api/v1/me
```

Do **not** double the prefix (`http://localhost:8080/api/v1/api/v1/me`). Do **not** omit it (`http://localhost:8080/me` is not mapped). Do **not** use Pay’s own prefix `/v1` against One.

TypeSpec (`packages/api-spec/main.tsp`):

```tsp
@service(#{ title: "Lazuar One API" })
@info(#{ version: "0.1.0" })
@server("https://api.lazuar.com/api/v1", "Production server (placeholder)")
@server("http://localhost:8080/api/v1", "Local development server")
namespace LazuarOneApi;
```

Runtime (`Program.cs`):

```csharp
// Versioned API surface (matches TypeSpec servers base path /api/v1)
var api = app.MapGroup("/api/v1").RequireCors();
```

JSON on the wire is **snake_case**, matching TypeSpec property names:

```csharp
// --- JSON (snake_case to match OpenAPI / TypeSpec property names) ---
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});
```

NSwag DTOs already stamp `[JsonPropertyName("user_id")]` etc. A Pay C# `HttpClient` that deserializes with default PascalCase **will miss fields** unless it uses snake_case or the generated `Lazuar.One.ApiTypes` contracts. That is a Pay-side risk, not a reason to re-spec the routes in `packages/pay-spec`.

### Local port map (Pay vs One vs identity)

From One README “Ports” and `apps/lazuar-docs/docs/reference/ports.md`, plus focused Pay’s launchSettings:

| Service | Port | URL | Who uses it in the first slice |
|---------|------|-----|--------------------------------|
| **lazuar-api (One)** | **8080** | http://localhost:8080 | Pay backend → `/api/v1/…` |
| **lazuar-pay (focused)** | **8081** | http://localhost:8081 | Pay’s own `/health`, later `/v1/checkouts` |
| lazuar-admin (staff SPA) | 5173 | http://localhost:5173 | **Never** a merchant destination |
| lazuar-app (customer SPA) | 5174 | http://localhost:5174 | Optional accept-invite deep-link; copy-link host today |
| **lazuar-login** | **5175** | http://localhost:5175 | Product sign-in + Session BFF. Pay OIDC redirects here, then back to Pay. Not Pay’s homepage. |
| Login BFF loopback | 5176 | proxied by Vite on 5175 | Dev only |
| examples/vite-spa | 5177 | http://localhost:5177 | Integrator sample, not Pay |
| lazuar-docs | 5180 | http://localhost:5180 | Engineers |
| Scalar reference | 5181 | http://localhost:5181 | TypeSpec explorer |
| Zitadel API (authority) | 8085 | http://localhost:8085 | OIDC token issuer. Pay SPA talks here for code+PKCE. Pay **backend** does not hold a Zitadel PAT. |
| Zitadel Login V2 stock | 3005 | http://localhost:3005 | **Break-glass only.** Do not ship merchants here. |
| Postgres | 5432 | localhost:5432 | One’s DB. Pay’s money DB is separate (not this slice). |
| OpenFGA HTTP | 8090 | http://localhost:8090 | One ops. Pay never holds the store admin token. |
| OpenFGA playground | 3009 | http://localhost:3009/playground | Local debug, not a Pay client |

Focused Pay today (`apps/lazuar-pay/src/Lazuar.Pay/Program.cs`):

```csharp
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/v1/health", () => Results.Ok(new { status = "ok" }));
```

`packages/pay-spec/main.tsp`:

```tsp
@server("http://localhost:8081", "Local focused Pay host")
namespace LazuarPay;
@route("/v1")
interface Health {
  @get
  @route("/health")
  check(): HealthResponse;
}
```

Two different version prefixes on two different hosts: One `/api/v1`, Pay `/v1`. Do not unify them by copying One into `pay-spec`.

### Authn on One’s `/api/v1` (what Pay sends)

TypeSpec marks product routes `@useAuth(BearerAuth)`. OpenAPI:

```yaml
securitySchemes:
  BearerAuth:
    type: http
    scheme: Bearer
```

Runtime selector (`AuthenticationExtensions.cs`): `Authorization: Bearer <token>` then:

1. prefix `lzr_scim_` → SCIM scheme (product `/api/v1` then **403** via `TenantAccessService.RejectScim`)
2. prefix `lzr_sk_` → API key scheme (`ApiKeyDefaults.KeyPrefix`)
3. else JWT Bearer against `Zitadel:Authority` (`http://localhost:8085` in Development)

JWT access-token guard (`JwtAccessTokenGuard.cs`): Zitadel **access** JWTs have `jti`; ID tokens do not. Sending Pay’s `id_token` as Bearer fails. Do not parse `urn:zitadel:iam:org:project:roles`. Role SoT is `/me` + `authz/check`.

Header `X-Lazuar-Tenant-Id` (`ActiveTenantHint.HeaderName`) is a **hint only**. It populates `/me.active_tenant_id` when it matches an active membership (JWT) or the key’s bound tenant. Path `{tenantId}` + membership is authorization SoT. Cookie `lazuar_active_tenant` is **lazuar-app’s** UX cookie (`apps/lazuar-app/src/lib/sessionKeys.ts`). Pay may keep its own “active merchant”; it must not authorize from that cookie, and One will not read Pay’s cookies.

CORS: One Development `App:CorsOrigins` includes 5173, 5174, 5177, 5180, 5181 (and 3000/3001 in Development overlay). **8081 is not a CORS origin** (it is a server). **5175 is not an API CORS origin** (login is a same-origin BFF). A future Pay **browser** origin must be added to One `App:CorsOrigins` **and** login `REDIRECT_ALLOWLIST`. That is operator work on One, not a Pay TypeSpec route.

### First-party identity of Pay as a sibling (from One §6.1 and Pay 02)

Pay is:

1. A **browser origin** registered as a tenant OIDC **SPA** (or `web`) via `POST /api/v1/tenants/{tenantId}/apps` — same kind of object as seeded `lazuar-app`, **not** a Console click. Seed script today only creates `lazuar-app` (`:5174/callback`) and `lazuar-admin` (`:5173/callback`) (`scripts/seed-platform-spa-clients.sh`). Pay’s client_id does not exist until someone POSTs apps or extends the seed.
2. A **backend** (`:8081`) that calls One with the user’s **access_token** or a `lzr_sk_` key.
3. **Not** a second Zitadel project.

Local Pay env, when it exists, looks like `lazuar-app`: authority `http://localhost:8085`, `client_id`, One API `http://localhost:8080/api/v1`. Product login is `:5175`. Stock Login V2 `:3005` is break-glass.

---

## 3. Route table — Method, Path, Pay use, skip/now/later, evidence

Paths below are TypeSpec-relative. Absolute local URL is `http://localhost:8080/api/v1` + path. C# maps the same paths under `MapGroup("/api/v1")` with `{tenantId:guid}` / `{appId:guid}` / `{keyId:guid}` / `{inviteId:guid}` constraints (non-GUID path segments 404 at routing).

Auth column is implied: every row except the two public footnotes requires `Authorization: Bearer` (JWT or `lzr_sk_`). Human-only rows reject API keys with 403 `"This operation requires a user session, not an API key."`

### 3.1 Identity — `/me`

| Method | Path | Pay use | When | Evidence |
|--------|------|---------|------|----------|
| GET | `/me` | Whoami. Copy `user_id`, `email`, `name`, `tenants[]` (`id`, `slug`, `name`, `role`, `status`, `permissions`), `active_tenant_id`, `active_role`, `is_platform_admin`. Map One `tenants[].id` → Pay `org_id`. Chrome roles. **Command-on-GET** for JWT with `email_verified=true` (domain auto-join + SSO JIT). Do not hammer from a hot loop. | **now** — first “connected” call; first-slice step 2; NP-ONE-006 | TypeSpec `modules/platform/routes.tsp` `MeOperations`; `MeEndpoints.MapGet("/me", GetMe)`; One §6.3; Pay 02 “Session and active workspace” |
| GET | `/me/invites` | Inbox of pending invites for the signed-in **verified** email. Never includes `invite_token`. Human JWT only (`RejectApiKey`). Unverified / missing email → empty page, not 401. | **later** (roster/inbox; NP-ONE-013). Copy-link accept does not need this. | TypeSpec `MeInviteOperations`; `MeEndpoints.MapGet("/me/invites", ListMyInvites)` |

TypeSpec:

```tsp
@route("/me")
interface MeOperations {
  @useAuth(BearerAuth)
  @summary("Get the currently authenticated user")
  @get
  getMe(): MeResponse | LazuarOneApi.Core.ProblemDetailsResponse;
}

@route("/me/invites")
interface MeInviteOperations {
  @useAuth(BearerAuth)
  @summary("List pending invites for the caller's email")
  @get
  listMyInvites(
    @query page?: int32,
    @query page_size?: int32,
  ): LazuarOneApi.Core.PaginatedResponse<MyInvite> | LazuarOneApi.Core.ProblemDetailsResponse;
}
```

C#:

```csharp
api.MapGet("/me", GetMe)
    .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
    .WithName("GetMe");
api.MapGet("/me/invites", ListMyInvites)
    .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
    .WithName("ListMyInvites");
```

Rate limit: `GET /api/v1/me` is **not** in `RateLimitPolicies.Match` (test locks this). Authz POSTs **are** limited (`AuthzPerWindow` default 30 / 60s).

### 3.2 Tenancy — `/tenants`

| Method | Path | Pay use | When | Evidence |
|--------|------|---------|------|----------|
| POST | `/tenants` | “Create workspace” in Pay. Caller becomes **owner**. Body `{ name, slug }`. Optional `Idempotency-Key`. Human JWT only. Gated by `Platform:AllowSelfServeTenantCreate` (true in Development). 201 + `Location: /api/v1/tenants/{id}`. One tenant id **is** Pay `org_id`. | **now** — first-slice step 3; NP-ONE-009. May skip the POST if Pay reuses an existing `/me.tenants[]` row. | TypeSpec `TenantOperations.createTenant`; `TenantEndpoints.MapPost("/", CreateTenant)` |
| GET | `/tenants` | List memberships (`data[]` of `TenantListItem`: id, slug, name, status, role, logo_url, permissions). Paginated `page` / `page_size` (default 1 / 20). API keys need `tenant:read` and see only the bound tenant (0–1). Or trust `/me.tenants`. | **now** if Pay needs pagination/logo; else `/me` is enough for whoami | TypeSpec `listTenants`; `TenantEndpoints.MapGet("/", ListTenants)` |
| GET | `/tenants/{tenantId}` | Profile. Members may GET when **suspended** (`TenantAccessMode.AllowSuspended`). API keys need `tenant:read`. | **now** — confirm org after create; later for ops header | TypeSpec `getTenant`; `TenantEndpoints.MapGet("/{tenantId:guid}", GetTenant)` |
| PATCH | `/tenants/{tenantId}` | Name / metadata / logo. Human JWT. `tenant:update` (admin/owner or custom). API keys rejected. PATCH omit vs `{}` semantics on metadata. | **later** — NP-ONE-010, not connection | TypeSpec `patchTenant`; `TenantEndpoints.MapPatch` |
| POST | `/tenants/{tenantId}/suspend` | Staff or Pay-admin **policy**, not merchant self-serve default. Stop charges when One says suspended (prefer webhook; this is a write). | **later** — not first connection. Do not put this on merchant “settings”. | TypeSpec `suspendTenant`; `TenantEndpoints.MapPost("/{tenantId:guid}/suspend")` |
| POST | `/tenants/{tenantId}/reactivate` | Same policy door as suspend. | **later** | TypeSpec `reactivateTenant` |
| POST | `/tenants/{tenantId}/retry-provision` | Break-glass when saga `status=failed`. Owner or platform admin. | **later** — Pay should surface “workspace not ready” from GET tenant, not own the saga | TypeSpec `retryProvision` |
| POST | `/tenants/{tenantId}/transfer-ownership` | Body `{ user_id }`. Target must already be an active member. Previous owner → admin. Human JWT. Current owner (or platform admin). | **later** — billing owner; NP-ONE-017 listens to `ownership.transferred` | TypeSpec `transferOwnership` |
| POST | `/tenants/{tenantId}/leave` | 204. Owners must transfer first. Human JWT. | **later** | TypeSpec `leaveTenant` |
| POST | `/tenants/{tenantId}/delete` | Owner wipe + tombstone. **Not** HTTP DELETE. Idempotent if already deleted (creator only). Response includes `leftovers` (honest: audit_events, outbox_messages, revoked_api_keys, revoked_apps, slug, fga_repair_tickets, webhook_endpoints). Human JWT. | **later** — document leftovers; do not call from merchant “close shop” until Pay writes the honesty | TypeSpec `deleteTenant` **POST** `/{tenantId}/delete` |

TypeSpec create/list/get (quoted):

```tsp
@route("/tenants")
interface TenantOperations {
  @useAuth(BearerAuth)
  @summary("Create a tenant (caller becomes owner)")
  @post
  createTenant(
    @header("Idempotency-Key") idempotencyKey?: string,
    @body body: CreateTenantRequest,
  ): { @statusCode statusCode: 201; @body body: CreateTenantResponse; } | Err;

  @useAuth(BearerAuth)
  @summary("List tenants the caller belongs to. API keys need tenant:read.")
  @get
  listTenants(
    @query page?: int32,
    @query page_size?: int32,
  ): LazuarOneApi.Core.PaginatedResponse<TenantListItem> | Err;

  @useAuth(BearerAuth)
  @summary("Get a tenant by id. API keys need tenant:read.")
  @get
  @route("/{tenantId}")
  getTenant(@path tenantId: string): Tenant | Err;
  // … patch, POST delete/suspend/reactivate/retry-provision/transfer-ownership/leave
}
```

`CreateTenantRequest`: `{ name: string (1–200), slug: string (1–64) }`.  
`Tenant.status` enum: `provisioning | active | failed | suspended | deleted`. Pay must not treat `provisioning` / `failed` as a billable merchant. `POST …/apps` requires `status=active` and a `zitadel_org_id` (409 otherwise).

**Precision vs Pay 02:** Pay 02 says “Do **not** call `POST /platform/tenants` (staff directory).” One TypeSpec **has no** `POST /platform/tenants`. Staff directory is **`GET /platform/tenants`**. Create-tenant for a merchant is **`POST /tenants`**. See §6.

Rate limit: `POST /api/v1/tenants` (exact path) → policy `create-tenant`, default 5 / 60s.

### 3.3 People — members and invites

| Method | Path | Pay use | When | Evidence |
|--------|------|---------|------|----------|
| GET | `/tenants/{tenantId}/members` | Roster. Any member. API keys need `members:read`. Paginated `Member` (`user_id`, email, name, role, status, custom_role_*). | **later** — NP-ONE-013; dogfood second engineer can be invited without listing first | TypeSpec `MemberOperations.listMembers`; `MemberEndpoints.MapGet` |
| POST | `/tenants/{tenantId}/members/invite` | Invite by email + role (`admin` \| `member`; **owner rejected**). Optional `custom_role_id`, `Idempotency-Key`. Admin/owner. 201. In Development, `Invite:ReturnTokenInResponse=true` so the body includes `invite_token` for **copy-link**. Production default **omits** the token (email only) — Pay must keep a non-email path in **dev** and not assume token in prod. | **now** — first-slice step 4; NP-ONE-011 | TypeSpec `inviteMember`; `MemberEndpoints.MapPost("/invite", InviteMember)`; `InviteOptions` |
| POST | `/tenants/{tenantId}/members/accept-invite` | Body `{ token }`. Human JWT. Joins caller to tenant. Copy-link accept. API keys rejected. | **now** if Pay hosts its own accept page; **later** if Pay deep-links to lazuar-app `/invites/accept?tenant_id=&token=` | TypeSpec `acceptInvite`; `MemberEndpoints.MapPost("/accept-invite", AcceptInvite)` |
| PATCH | `/tenants/{tenantId}/members/{userId}` | Role change (`admin` \| `member`, xor `custom_role_id`). Last owner protected. Admin/owner. | **later** | TypeSpec `changeMemberRole` |
| DELETE | `/tenants/{tenantId}/members/{userId}` | Remove by Zitadel user id. Admin/owner. 204. | **later** | TypeSpec `removeMember` |
| GET | `/tenants/{tenantId}/invites` | Pending (or filtered `status`) invites. Admin/owner. **Never includes raw token.** | **now** for “pending” UI after invite; else **later** | TypeSpec `InviteOperations.listInvites` |
| DELETE | `/tenants/{tenantId}/invites/{inviteId}` | Revoke. Admin/owner. 204. | **later** — NP-ONE-011 | TypeSpec `revokeInvite` |
| POST | `/tenants/{tenantId}/invites/{inviteId}/resend` | Regenerates token; emails new link; 200 `InviteMemberResponse` (token only if ReturnTokenInResponse). | **later** — exists; Pay 02 said “if One has it” — **One has it** | TypeSpec `resendInvite`; `MemberEndpoints.MapPost("/{inviteId:guid}/resend")` |

TypeSpec invite/accept:

```tsp
@route("/tenants/{tenantId}/members")
interface MemberOperations {
  @post
  @route("/invite")
  inviteMember(
    @path tenantId: string,
    @header("Idempotency-Key") idempotencyKey?: string,
    @body body: InviteMemberRequest,
  ): { @statusCode statusCode: 201; @body body: InviteMemberResponse; } | Err;

  @post
  @route("/accept-invite")
  acceptInvite(
    @path tenantId: string,
    @body body: AcceptInviteRequest,
  ): { @statusCode statusCode: 200; @body body: Member; } | Err;
}
```

`InviteMemberRequest`: `{ email, role?: MembershipRole, custom_role_id?: string }` — TypeSpec: “Omit → tenant default role (ROLE-06). owner rejected.”  
`MembershipService` throws: `"Cannot invite as owner. Use transfer-ownership after the user joins."`

Copy-link format (**must stay stable**, LOCAL-03):

```text
{origin}/invites/accept?tenant_id={tenantId}&token={invite_token}
```

lazuar-app builder (`apps/lazuar-app/src/lib/inviteLink.ts`):

```ts
export function buildAcceptInviteLink(tenantId: string, token: string): string {
  const origin = window.location.origin.replace(/\/$/, '')
  return `${origin}/invites/accept?tenant_id=${encodeURIComponent(tenantId)}&token=${encodeURIComponent(token)}`
}
```

API email body uses `App:PublicAppBaseUrl` (`http://localhost:5174` in Development). Pay may deep-link that URL **or** post the same `accept-invite` API from a Pay page. Pay must not invent a second token format.

Rate limits: invite 20 / 60s; resend 10; accept 30.

**Do not call Zitadel InviteUser.** Issue 018 is **Done by deletion**. No production `InviteUser` path. One membership row + invite token is SoT. Zitadel org is a provision identifier for OIDC apps, not a people directory.

### 3.4 Machines — API keys

| Method | Path | Pay use | When | Evidence |
|--------|------|---------|------|----------|
| POST | `/tenants/{tenantId}/api-keys` | Mint Pay worker key. Secret returned **once** (`lzr_sk_…`). Admin/owner. Suspended/deleted tenant → 403. | **now** — first-slice step 5; NP-ONE-014 | TypeSpec `ApiKeyOperations.createApiKey`; `ApiKeyEndpoints.MapPost` |
| GET | `/tenants/{tenantId}/api-keys` | List metadata (prefix, scopes, no secret). JWT: any member. Keys need `keys:read`. | **now** (ops) / **later** (if mint-and-store is enough) | TypeSpec `listApiKeys` |
| DELETE | `/tenants/{tenantId}/api-keys/{keyId}` | Revoke. Admin/owner. 204. | **later** — rotate/revoke; listen `api_key.revoked` | TypeSpec `revokeApiKey` |

TypeSpec create body:

```tsp
model CreateApiKeyRequest {
  @minLength(1) @maxLength(200)
  name: string;
  /**
   * Scopes for the key. Omitted → defaults to ["tenant:read"].
   * Explicit empty array is rejected (400). Empty scopes are no longer full-admin (P12).
   * Catalog: authz:check, members:read, apps:read, keys:read, tenant:read,
   * webhooks:read, webhooks:write, events:read, audit:read, admin, *
   */
  scopes?: string[];
  expires_at?: utcDateTime;
}
```

Pay’s first worker key must **not** be `*` or `admin`. Prefer explicit:

```json
{
  "name": "pay-api",
  "scopes": ["tenant:read", "authz:check", "members:read"]
}
```

Add `webhooks:read` / `webhooks:write` only when Pay registers One webhooks via API (first-slice step 6, adjacent). `tenant:read` alone is the default if scopes are omitted — that key **cannot** call `authz/check` (needs `authz:check`). Do not omit scopes and then wonder why check 403s.

`ApiKeyCreatedResponse` adds `secret` (once). Store in Pay server env. Never log. Never put in `packages/pay-spec`.

Rate limit: create-api-key 20 / 60s.

### 3.5 Apps — OIDC clients on the One tenant

| Method | Path | Pay use | When | Evidence |
|--------|------|---------|------|----------|
| POST | `/tenants/{tenantId}/apps` | Register Pay SPA / web / m2m. 201 first create (confidential `client_secret` once); 200 secret-stripped replay of same `Idempotency-Key`. Admin/owner. Tenant must be **active** with `zitadel_org_id` (409 else). | **now** — first-slice step 1; NP-ONE-001. Alternative: extend One seed script like `lazuar-app` (still One’s door, not Console). | TypeSpec `AppOperations.createApp`; `AppEndpoints.MapPost` |
| GET | `/tenants/{tenantId}/apps` | List metadata, no secrets. API keys need `apps:read`. | **later** | TypeSpec `listApps` |
| GET | `/tenants/{tenantId}/apps/{appId}` | One app, no secret. | **later** | TypeSpec `getApp` |
| POST | `/tenants/{tenantId}/apps/{appId}/rotate-secret` | Confidential rotate; secret once. Admin/owner. SPA has no secret. | **later** | TypeSpec `rotateSecret` |
| DELETE | `/tenants/{tenantId}/apps/{appId}` | Revoke. Admin/owner. 204. | **later** | TypeSpec `deleteApp` |
| GET | `/public/oidc-apps/{clientId}/redirect-origins` | **Anonymous.** Login finalize allowlist. Pay **backend** does not need this. Pay **must not** treat it as an authz oracle. | **skip** for Pay product code (One login consumes it). Not in TypeSpec OpenAPI path list; **runtime-only**. | `AppEndpoints.MapGet("/public/oidc-apps/{clientId}/redirect-origins")` — **not** in `packages/api-spec/dist/openapi.yaml` paths |

TypeSpec create:

```tsp
model CreateOidcAppRequest {
  name: string;
  type: OidcAppType; // spa | web | m2m
  redirect_uris?: string[];          // required for spa/web; forbidden for m2m
  post_logout_redirect_uris?: string[];
}
```

Pay SPA: `type: "spa"`, PKCE public, JWT access tokens, redirect to Pay origin callback **and** whatever login allowlist needs. Register the same origin on login `REDIRECT_ALLOWLIST`. Do not add the origin only in Zitadel Console.

`m2m` is a **Zitadel** client-credentials app, **not** a One `lzr_sk_` key. Pay’s server-to-One credential is `lzr_sk_`, not an OIDC m2m secret, unless Pay is calling *other* APIs as that client.

### 3.6 Authz — check (this slice) plus siblings on the same group

| Method | Path | Pay use | When | Evidence |
|--------|------|---------|------|----------|
| POST | `/tenants/{tenantId}/authz/check` | Can this user `member` / `admin` / `owner` this tenant? Gate Pay merchant-admin routes (keys, refunds, products). Body `{ relation, object: { type, id }, user_id? }`. | **now** — second “connected” call; first-slice step 5; NP-ONE-015 | TypeSpec `AuthzOperations.check`; `AuthzEndpoints.MapPost("/check", Check)`; docs `integrations/authz.md`, recipe `recipes/authz-check.md` |
| POST | `/tenants/{tenantId}/authz/batch-check` | Permission chrome (max 50). Same allow-list. | **later** — NP-ONE-016 | TypeSpec `batchCheck` |
| POST | `/tenants/{tenantId}/authz/list-objects` | `type=app` inventory; `type=tenant` is a **0/1 Check**, not workspace inventory. Use `GET /tenants` to list workspaces. | **later** — do not use as merchant directory | TypeSpec `listObjects`; issue 086 honesty |
| POST | `/tenants/{tenantId}/authz/write` | **Does not exist.** Dual-write is platform-internal on membership. | **skip** — AUTHZ-06 never; NP-ONE-016 notes | Docs: “Public `authz/write` — Not available”; TypeSpec has no such route; `AuthzEndpoints` maps only check / batch-check / list-objects |

TypeSpec:

```tsp
@route("/tenants/{tenantId}/authz")
interface AuthzOperations {
  @useAuth(BearerAuth)
  @post
  @route("/check")
  check(@path tenantId: string, @body body: AuthzCheckRequest): AuthzCheckResponse | Err;
  // batch-check, list-objects
}
```

`AuthzCheckRequest`:

```tsp
model AuthzCheckRequest {
  user_id?: string;   // JWT: omit = caller sub; key: required, must not be key id
  relation: string;   // max 64
  object: AuthzObjectRef; // type + id
}
model AuthzCheckResponse { allowed: boolean; }
```

Allow-list (`AuthzObjectRules.cs` + TypeSpec comments):

| `object.type` | `object.id` | Allowed relations |
|---------------|-------------|-------------------|
| `tenant` | **must equal path `tenantId`** (else 400) | `owner`, `admin`, `member`, `can_view`, `can_manage_members`, `can_manage_tenant` |
| `app` | OIDC application UUID in that tenant | `viewer`, `admin` |
| anything else (`payment`, `merchant_document`, `invoice`, …) | — | **400** `"Authz only supports object type(s): \"app\", \"tenant\"."` |

Pay’s first check:

```http
POST /api/v1/tenants/{tenantId}/authz/check
Authorization: Bearer {access_token}
Content-Type: application/json

{
  "relation": "member",
  "object": { "type": "tenant", "id": "{tenantId}" }
}
```

```json
{ "allowed": true }
```

API key variant **must** include `user_id` of the human (Zitadel `sub` from `/me` when the user called whoami). Omit → 400 `"user_id is required when authenticating with an API key."` Key id as `user_id` → 400. Key needs scope `authz:check` (or `admin` / `*`).

Local Development: `OpenFga:Enabled=false` → checks use SQL membership (`AuthzService.EvaluateLocalAsync` / `RelationSatisfiedByRole`). `member` is true for owner, admin, and member. `owner` is true only for owner. When FGA is enabled, fail-closed 503 if OpenFGA is down — Pay must not fail-open money routes on 503.

Rate limit: authz 30 / 60s (issue 088 residual closed with a limiter; still an oracle if you spray `user_id`s as admin).

Custom ROLE-03 permissions (`tenant:update`, `sso:manage`, …) are **SQL overlay**, not FGA relations, not JWT claims. `/me.tenants[].permissions` is chrome **hint**. Pay must not authorize refunds from that array alone. Pay-specific verbs (`refund`, `change_gateway_keys`) **do not exist on One**. Enforce those in Pay using One `role` + `authz/check` (e.g. require `admin` for keys/refunds, allow `member` to view ops). One has **no** built-in `viewer` membership role — see open questions.

### 3.7 Adjacent One routes on `/api/v1` that this slice does not call (inventory, not implement)

Listed so Pay does not “discover” them and copy them into `pay-spec`. Evidence: OpenAPI path list in `packages/api-spec/dist/openapi.yaml`.

| Method | Path | First-slice stance |
|--------|------|--------------------|
| GET | `/health` (under `/api/v1`) | Optional Pay readiness of One; not whoami |
| POST | `/hrd`, `/hrd/idp` | **skip** — login product, anonymous; Pay does not implement HRD |
| GET/PUT | `/platform/social-idps`, `/{provider}` | **skip** — platform staff |
| GET | `/public/social-idps` | **skip** — login flags |
| GET | `/platform/tenants`, `/platform/tenants/by-slug/{slug}` | **skip** — staff directory |
| POST | `/platform/tenants/{tenantId}/reconcile-fga` | **skip** — staff FGA repair |
| GET | `/tenants/{tenantId}/events` | **later** (adjacent) — pull if Pay cannot take webhooks |
| GET | `/tenants/{tenantId}/audit` | **later** — One audit, not Pay money audit |
| CRUD | `/tenants/{tenantId}/domains…` | **later / skip for v1** — enterprise |
| CRUD | `/tenants/{tenantId}/roles…` | **later** — custom roles; do not invent VIEWER here without a written Pay check |
| CRUD | `/tenants/{tenantId}/sso-connections…` | **skip for first slice** — named merchant later |
| CRUD | `/tenants/{tenantId}/scim/token…` | **skip** — see §6 |
| CRUD | `/tenants/{tenantId}/audit-stream…` | **skip for first slice** |
| CRUD | `/tenants/{tenantId}/webhooks…` | **later** — first-slice step 6 (`member.*`, `tenant.suspended`); not this paper’s implement target but **do not skip forever** |
| GET | `/webhook-event-types` | **later** with webhooks |

Closed webhook event catalog Pay will eventually subscribe to (`WebhookEventCatalog.cs`): `tenant.created`, `tenant.deleted`, `tenant.suspended`, `tenant.reactivated`, `member.invited`, `member.accepted`, `member.removed`, `member.left`, `member.role_changed`, `ownership.transferred`, `api_key.created`, `api_key.revoked`, `oidc_app.created`, `oidc_app.revoked`, `invite.revoked`, `invite.resent`, `webhook.test`. There is no `payment.*` here. Money events stay in Pay.

### 3.8 `@lazuar/one-client` coverage vs this table

Unpublished workspace package `packages/one-client` (`private: true`, not npm). `createClient` wraps:

- `me.get` → `GET /me`
- `tenants.list/create/get/patch`
- `apiKeys.create/list` (**no revoke**)
- `authz.check/batchCheck/listObjects`

It does **not** wrap members, invites, apps, suspend, webhooks. Pay’s focused host is **C#** (`apps/lazuar-pay`). Importing a TypeScript workspace client into that process is the wrong linker. Pay C# should HTTP the same URLs (or, later, generate C# DTOs from **One’s** OpenAPI — not from `pay-spec`). First-party TS SPAs may `file:` / tarball import the client. Do not wait on npm (DX-03 is sell-blocking, not dogfood-blocking).

---

## 4. Request / response shapes for `GET /me` (fields Pay will copy)

### 4.1 Request

```http
GET /api/v1/me HTTP/1.1
Host: localhost:8080
Authorization: Bearer {access_token | lzr_sk_…}
Accept: application/json
X-Lazuar-Tenant-Id: {optional uuid}
```

- No body. No required query.
- `X-Lazuar-Tenant-Id` optional. Invalid / non-member hint → `active_tenant_id` omitted; request still 200.
- Unauthenticated → 401.
- SCIM bearer `lzr_scim_…` → 403 `"SCIM tokens cannot call product APIs."` (`MeEndpoints.GetMe` calls `RejectScim` first).
- Missing `sub` → 401 `"Token is missing a subject (sub) claim."`

curl (JWT), from One README / examples:

```bash
curl -sS "http://localhost:8080/api/v1/me" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Accept: application/json"
```

curl (API key), from `examples/node-api-key/README.md`:

```bash
export LAZUAR_API_BASE=http://localhost:8080/api/v1
curl -sS "$LAZUAR_API_BASE/me" \
  -H "Authorization: Bearer $LAZUAR_API_KEY" \
  -H "Accept: application/json"
```

`@lazuar/one-client`:

```ts
const client = createClient({
  baseUrl: 'http://localhost:8080/api/v1',
  getAccessToken: () => accessToken, // access_token only
  getTenantId: () => activeTenantId, // optional X-Lazuar-Tenant-Id
})
const me = await client.me.get()
```

### 4.2 TypeSpec model (authoritative)

From `packages/api-spec/modules/platform/models.tsp`:

```tsp
model MeResponse {
  /** Caller subject. User JWT: Zitadel `sub`. API key: the key GUID. */
  user_id: string;
  email?: string;
  name?: string;
  /** Always present as [] so clients can lock to array type. */
  tenants: TenantSummary[];
  /** Always present. API keys never receive true. */
  is_platform_admin: boolean;
  /**
   * Set when X-Lazuar-Tenant-Id matches an active membership (JWT)
   * or the key's bound tenant. Omitted when the hint is absent or invalid.
   * Never authorize from this field alone.
   */
  active_tenant_id?: string;
  /** Role for active_tenant_id when that field is set (owner | admin | member). */
  active_role?: string;
}

model TenantSummary {
  id: string;
  slug: string;
  name: string;
  /** owner | admin | member. API key: scope-derived admin|member; never owner. */
  role?: string;
  status?: string;
  /**
   * Effective ROLE-03 catalog strings for chrome. Hint only.
   * Owner = all nine; admin = all except tenant:delete; member = custom-role list or [].
   * Always present so clients lock to array.
   */
  permissions: string[];
}
```

OpenAPI required fields (`Platform.MeResponse`): `user_id`, `tenants`, `is_platform_admin`.  
OpenAPI required on `Platform.TenantSummary`: `id`, `slug`, `name`, `permissions`.

Generated C# (`Lazuar.One.ApiContracts.cs`) — JSON names Pay will see:

```csharp
[JsonPropertyName("user_id")]           public string User_id { get; set; }
[JsonPropertyName("email")]             public string? Email { get; set; }
[JsonPropertyName("name")]              public string? Name { get; set; }
[JsonPropertyName("tenants")]           public List<TenantSummary> Tenants { get; set; }
[JsonPropertyName("is_platform_admin")] public bool Is_platform_admin { get; set; }
[JsonPropertyName("active_tenant_id")]  public string? Active_tenant_id { get; set; }
[JsonPropertyName("active_role")]       public string? Active_role { get; set; }
```

### 4.3 Wire JSON Pay should copy (JWT user)

Illustrative — field set from TypeSpec + `MeEndpoints.GetMe` mapping, not a live capture:

```json
{
  "user_id": "258392106498392123",
  "email": "ada@acme.test",
  "name": "Ada",
  "tenants": [
    {
      "id": "3f1c0a7e-2b4d-4c8a-9e10-111111111111",
      "slug": "acme",
      "name": "Acme",
      "role": "owner",
      "status": "active",
      "permissions": [
        "tenant:update",
        "tenant:delete",
        "domains:manage",
        "roles:manage",
        "events:read",
        "audit:read",
        "sso:manage",
        "scim:manage",
        "streams:manage"
      ]
    }
  ],
  "is_platform_admin": false,
  "active_tenant_id": "3f1c0a7e-2b4d-4c8a-9e10-111111111111",
  "active_role": "owner"
}
```

`permissions` for owner = `TenantPermissions.All` (nine strings). Admin = same minus `tenant:delete`. Plain member = `[]` unless a custom role is assigned (`TenantCreateListGetTests` locks custom `sso:manage` on `/me` and `GET /tenants`).

**Pay must copy (whoami / org binding):**

| Field | Pay use |
|-------|---------|
| `user_id` | Merchant staff id. Do not mint a second user table keyed differently. This is Zitadel `sub` for JWT. |
| `email` | Display; invite matching happens on One. May be absent on some access tokens. |
| `name` | Display. Composed from `name` or given+family on the token. Not userinfo. |
| `tenants[].id` | **Pay `org_id`.** UUID. |
| `tenants[].slug` | Display / URL if Pay wants; One slug is unique among non-deleted. |
| `tenants[].name` | Workspace name. |
| `tenants[].role` | `owner` \| `admin` \| `member`. Chrome. **Not** Pay VIEWER. |
| `tenants[].status` | If `suspended`, stop staff mutations and (via webhook too) charges. |
| `tenants[].permissions` | Chrome hint only. Never authorize Pay refunds from this list. |
| `active_tenant_id` | UX default if Pay sent the hint. Never SoT. |
| `active_role` | Built-in role for the hint. Custom role **name is not this field** (`active_role` stays owner/admin/member). |
| `is_platform_admin` | If true, this human is Lazuar **staff**. Pay must not treat that as merchant owner. Do not send them to `:5173` as the product path. Keys always false. |

**Pay must not copy / must not trust as authz:**

- `permissions[]` as a capability bitset for money.
- `active_tenant_id` as the tenant for a Pay route that did not also take `{tenantId}` / Pay `org_id` in the path.
- `is_platform_admin` as “can refund”.
- JWT `org_id` / Zitadel project roles (not on this document; do not add them).

### 4.4 Wire JSON when the caller is `lzr_sk_`

`GetMeForApiKey`:

- `user_id` = **key GUID**, not a Zitadel user. Do not use this as `authz/check.user_id`.
- `tenants` = 0 or 1 bound workspace; `role` is `admin` if key has `admin`/`*`, else `member`; keys are **never** `owner`.
- `active_tenant_id` = bound tenant if it exists and is not deleted.
- `is_platform_admin` = always false in product (keys never receive true).
- **No** domain auto-join / SSO JIT (those run only on the JWT branch when `email_verified == true`).

Expected (example README): 200 with `tenants` containing the key’s workspace and `active_tenant_id` equal to that bound tenant.

### 4.5 Side effects (why Pay must not hammer `/me`)

JWT branch (`MeEndpoints.GetMe`):

```csharp
if (TenantAccessService.GetEmailVerified(user) == true)
{
    var joins = httpContext.RequestServices.GetRequiredService<IDomainJoinService>();
    await joins.AutoJoinAsync(sub, email, cancellationToken);
    if (TenantAccessService.HasIdpAuthentication(user))
    {
        var ssoJoins = httpContext.RequestServices.GetRequiredService<ISsoJoinService>();
        await ssoJoins.AutoJoinAsync(sub, email, cancellationToken);
    }
}
```

`GET /me` is a **command-on-GET**. Fail-closed: missing or false `email_verified` must not JIT-join. Residuals (email_verified, disabled membership revive) are One isolation issues; Pay should not paper over them with a second join API. Cache `/me` per session; refresh on workspace switch / 401; do not call it per Pay ledger line.

Errors: RFC 7807 `ProblemDetails` with `request_id`. Status union in TypeSpec: 400, 401, 403, 404, 409, 429, 500, 502, 503.

---

## 5. Recommended first two Pay-side calls for “connected”

These two calls, in this order, are the proof that Pay lives on One. They match One §6.11 steps 1–5 compressed into HTTP, and Pay 03 steps 2 + 5.

### Call 1 — whoami: `GET /api/v1/me`

**Who:** Pay backend (or Pay BFF) with the **user’s access_token** obtained via OIDC code+PKCE against `http://localhost:8085` with Pay’s `client_id`. Not the id_token. Not a password form.

**URL:** `http://localhost:8080/api/v1/me`

**Pass:** 200, JSON has `user_id` (non-empty), `tenants` is an array (maybe empty if the human has no workspace yet), `is_platform_admin` is a boolean.

**If `tenants` is empty:** Pay’s “create workspace” is `POST http://localhost:8080/api/v1/tenants` with `{ "name", "slug" }` and the same Bearer. 201 body `id` becomes Pay `org_id`. Then GET `/me` again (or use the 201 body). Do not insert a row into a Pay `organizations` table that is not that UUID.

**If `tenants` is non-empty:** pick `tenants[0].id` or honor `X-Lazuar-Tenant-Id` / Pay’s own active-merchant UX. Still do not authorize Pay money routes from the hint alone.

**Fail (not connected):** 401 (wrong token kind, expired, id_token, audience); CORS if a browser origin is not allow-listed; connection refused if One `:8080` is down. Pay must not fail-open.

### Call 2 — `POST /api/v1/tenants/{tenantId}/authz/check`

**When:** immediately after whoami has a `tenantId` (existing membership or freshly created).

**Who:** same user JWT, **omit** `user_id` (check as caller). This is the honest “is this human a member of this org?” question.

**URL:** `http://localhost:8080/api/v1/tenants/{tenantId}/authz/check`

**Body:**

```json
{
  "relation": "member",
  "object": {
    "type": "tenant",
    "id": "{tenantId}"
  }
}
```

`object.id` **must** equal the path tenant id.

**Pass:** 200 `{ "allowed": true }`. Pay may then serve merchant ops for that org (still enforce Pay-side VIEWER/refund rules using `tenants[].role` / a second check for `admin` where needed).

**Fail:** 200 `{ "allowed": false }` — authenticated but not a member (or relation not satisfied). 403 — not a member of path tenant so the façade will not even check (membership gate in `RequireMembershipAsync` runs first). 400 — unknown type, id ≠ path, missing relation. 503 — FGA enabled and down (fail-closed).

**Worker variant (not the first “connected” pair):** Pay cron uses `lzr_sk_` with scopes including `authz:check`, and **must** pass `user_id` of the human being authorized. That is how Pay API → One checks “may Ada refund?” when the request is not impersonating Ada’s JWT. The first connected proof should still be **user JWT `/me` then user JWT `check`**, because that is the browser merchant path.

Do not invert the order (check without knowing `tenantId` / `user_id`). Do not skip `/me` and parse JWT org claims. Do not call OpenFGA at `:8090` from Pay.

After these two succeed, the rest of S0 (invite copy-link, mint key, webhook subscribe) is still required for the dogfood sentence, but **“connected”** is these two round-trips.

Suggested subsequent calls (not the first two, still this façade):

3. `POST /tenants/{id}/apps` (or seed) — if not already registered.  
4. `POST /tenants/{id}/members/invite` + copy-link.  
5. `POST /tenants/{id}/api-keys` with explicit scopes including `authz:check`.  
6. (Adjacent paper) webhook subscribe `member.*` + `tenant.suspended`.

---

## 6. What Pay must never call

### 6.1 Platform tenant directory (staff)

| Method | Path | Why never |
|--------|------|-----------|
| GET | `/api/v1/platform/tenants` | Platform admin only (`Platform:AdminEmails`). Global directory, not membership-scoped. Pay 02 / One §6.4: Pay is not staff console. |
| GET | `/api/v1/platform/tenants/by-slug/{slug}` | Same gate. Existence oracle for staff. |
| POST | `/api/v1/platform/tenants/{tenantId}/reconcile-fga` | Staff FGA replay. Pay does not hold OpenFGA admin. |

There is **no** `POST /api/v1/platform/tenants` in TypeSpec or `PlatformTenantEndpoints.cs`. Pay 02’s sentence “Do not call `POST /platform/tenants`” is directionally right (do not use the staff door to create orgs) but the create door is `POST /tenants`. If someone later adds `POST /platform/tenants`, still skip it.

C# maps (`PlatformTenantEndpoints.cs`):

```csharp
var group = api.MapGroup("/platform/tenants")
    .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser);
group.MapGet("/", ListAllTenants);
group.MapGet("/by-slug/{slug}", GetBySlug);
group.MapPost("/{tenantId:guid}/reconcile-fga", ReconcileFga);
```

Non-admin → 403 `"Platform admin required. Configure Platform:AdminEmails…"`.

Also skip: `GET/PUT /platform/social-idps`. Skip sending merchants to **`:5173`**. `lazuar-admin` is Lazuar operators.

### 6.2 Zitadel InviteUser / Console people directory

- Issue **018** (`/Users/akmalfirdaus/Code/lazuar/lazuar-one/issues/018-zitadel-invite-user-noop.md`): closed by **deletion**. No production path named `InviteUser`. Do not implement Zitadel org invite.
- Pay invites via `POST /api/v1/tenants/{id}/members/invite` only.
- Do not call Zitadel Management API with `ZITADEL_PAT` from Pay. That PAT is One seed / provisioner (`seed-platform-spa-clients.sh`). Pay does not hold it (Pay 02 secrets table; One §6.10).
- Do not tell merchants to add users in Zitadel Console.
- Do not attach Pay buyers as Zitadel humans (Pay 01 / 03 fail lock).

### 6.3 `authz/write`

Does not exist in TypeSpec, OpenAPI, or `AuthzEndpoints`. Docs (`apps/lazuar-docs/docs/integrations/authz.md`):

> Public `authz/write` | **Not available** — platform dual-write on membership only

Pay must not:

- POST a fictional `/authz/write`
- dual-write OpenFGA tuples
- add FGA types `payment` / `document` in One because Pay wishes to check them (AUTHZ-05 requires Pay as **named consumer** with a real check call; first slice’s check is `tenant`/`member`)
- hold OpenFGA store admin (`deploy/dev/openfga/.env.local`)

### 6.4 SCIM (token + protocol)

| Surface | Path | Why never (first slice and until a named merchant deal) |
|---------|------|----------------------------------------------------------|
| Token CRUD | `/api/v1/tenants/{tenantId}/scim/token` (+ `/rotate`) | Enterprise directory. One §6.9 / §6.11: **Stop. Do not start SCIM.** |
| Protocol | `/scim/v2/tenants/{tenantId}/Users` | **Not** `/api/v1`. Bearer `lzr_scim_…` only. JWT / `lzr_sk_` on this path → 401. SCIM token on `/api/v1` → 403 (`RejectScim` / ISO-44). |

`packages/api-spec/scim/README.md`: media `application/scim+json`; Groups / `/Me` / Bulk **absent**. Pay is not an IdP. Pay buyers are not SCIM Users.

`Program.cs`: `app.MapScimUserEndpoints();` **outside** the `/api/v1` group.

### 6.5 Other never-hold / never-call (secrets and wrong doors)

From One §6.10 and Pay 02:

| Secret / door | Who |
|---------------|-----|
| Zitadel masterkey / first-instance | One ops |
| Login-client PAT | `lazuar-login` only (`apps/lazuar-login/.secrets/`) |
| `ZITADEL_PAT` Management | One seed / provisioner |
| OpenFGA store admin | One ops |
| Webhook AES / pepper | One API config |
| `Platform:AdminEmails` | One API |
| Stock Login V2 `:3005` | Break-glass; not Pay UX |
| `lazuar-admin` `:5173` | Staff; not merchants |
| One `/hrd` from Pay checkout | Login’s anonymous HRD; Pay checkout is the **buyer** plane |

Pay holds: OIDC `client_id` (public), `lzr_sk_` (once), One-webhook HMAC (`whsec`, once), Pay’s own gateway BYOK keys (Pay DB, later slice).

### 6.6 Old Pay identity façade (this repo, not One)

Never call the museum:

- `POST /one/auth/login`, `GET /one/auth/me`, `GET /one/workspaces`, … in `packages/api-spec/modules/one/routes.tsp`
- Old `apps/lazuar-api` Modules/One

Those are the plane Pay 00 left. Focused host README: “Merchants come from **lazuar-one** (not yet wired). Do not copy `Modules/One`.”

---

## 7. Risks if Pay re-specifies these routes in `packages/pay-spec`

`packages/pay-spec` is the focused Pay host contract. README:

> TypeSpec for the **focused Pay host** (`apps/lazuar-pay`, port 8081).  
> Not `packages/api-spec` (old modular API on 8080). **Do not import One, LHDN, or `/public/commerce` routes here.**

`main.tsp` today is health only. Growing it with `POST /v1/checkouts` is correct. Growing it with `GET /me` is not.

Concrete failure modes if someone pastes One’s `/me` / `/tenants` / `/authz/check` into `pay-spec`:

1. **Two sources of truth.** One’s TypeSpec (`lazuar-one/packages/api-spec`) is the producer contract. Pay’s `pay-spec` would drift on the next One `pnpm gen` (new field on `MeResponse`, new 429, `permissions` always-present, invite resend). Pay CI would green on a lie.

2. **Wrong server URL and prefix.** Pay spec server is `http://localhost:8081` with paths under `/v1`. One server is `http://localhost:8080/api/v1` with paths `/me`. A merged spec teaches clients to call `http://localhost:8081/v1/me` or `http://localhost:8080/me`. Neither is mapped.

3. **NSwag / Kiota generation into the Pay host.** Old Pay already generates `packages/api-types-dotnet` from the museum spec, including One DTOs under a different namespace. Generating `MeResponse` inside Pay’s process invites Pay to *implement* `/me` (password form, second org table) — exactly the 03 fail lock.

4. **Field rename / casing.** One wire is snake_case (`user_id`, `is_platform_admin`). A Pay-side re-spec that “C#-ifies” to `userId` will not round-trip One JSON even if the path is right.

5. **Auth scheme confusion.** One `BearerAuth` means JWT **or** `lzr_sk_` **or** (rejected) `lzr_scim_`. Pay’s future merchant JWT is **One’s** access_token. Re-specifying Bearer on Pay `/v1/me` makes it look like Pay verifies Zitadel itself in the spec, hiding that One is the verifier.

6. **Allow-list drift.** Authz types `{ tenant, app }` live in One `AuthzObjectRules`. If Pay spec lists `object.type: payment`, implementers will send it and get 400. If Pay spec omits `user_id` required-for-keys, workers will 400 in production.

7. **Command-on-GET disappears.** A Pay-owned `GET /me` documented as read-only will surprise on JIT join. The honesty belongs in One’s spec comments, which already say JWT identity + memberships from Lazuar DB and that `/me` writes exist in architecture papers.

8. **Invite token policy.** `invite_token` is env-gated (`Invite:ReturnTokenInResponse`). A Pay spec that requires the token in every 201 will fail against staging/production One.

9. **Idempotency and leftovers.** Create-tenant / create-app / invite headers and wipe `leftovers` are One product rules. Copying a subset into Pay spec will miss 200-vs-201 app replay (secret stripped).

10. **Museum gravity.** `packages/api-spec/modules/one` already re-specified identity badly (`/one/auth/login` password). Putting One routes in `pay-spec` repeats that pattern under a cleaner folder name.

**What Pay spec should contain:** Pay money (`/v1/checkouts`, webhooks from Stripe/CHIP, receipts). Maybe a **note** in Pay docs: “merchant identity is One `GET http://localhost:8080/api/v1/me`” — a pointer, not a duplicate path.

**What Pay C# may generate:** DTOs from **One’s** `packages/api-spec/dist/openapi.yaml` (sibling path or vendored snapshot with a drift check), or hand-written records that match snake_case fields listed in §4. That is consuming One’s spec, not re-owning it.

**What Pay TS SPA may import:** `@lazuar/one-client` via `file:` / tarball after `dist/` exists. Still One’s types (`src/generated.ts` copied from One OpenAPI). Not `pay-spec`.

---

## 8. Open questions

1. **Pay SPA origin and port.** Focused Pay is `:8081` (API). There is no Pay Vite app in `apps/lazuar-pay`. Where does the merchant UI live for OIDC redirects — a new SPA port (not in One CORS today), reuse of some Pay-repo portal, or deep-link into `lazuar-app` `:5174` for identity chrome only? Until that origin exists, `POST …/apps` redirect_uris and One `App:CorsOrigins` + login `REDIRECT_ALLOWLIST` cannot be filled honestly.

2. **Seed vs `POST …/apps`.** First-slice step 1 allows either. Seed script only knows `lazuar-app` / `lazuar-admin`. Who runs the extra client create — Pay engineer with a user JWT after they already have a tenant (chicken/egg), or One bootstrap extended with `lazuar-pay` redirects? Platform first-party clients today are seeded **in the platform Zitadel project**, not necessarily on a customer tenant. One §6.1: “in practice, each Pay-facing client is an app **on a One tenant**, or a first-party platform client seeded like `lazuar-app`.” Which of those two is the local dogfood object is not pinned.

3. **VIEWER vs One roles.** Pay dogfood: “VIEWER cannot change keys or refund”; NP-ONE-021. One built-ins are `owner | admin | member` only. Options: (a) Pay treats One `member` as VIEWER (read ops) and One `admin`/`owner` as charger; (b) One custom role with empty ROLE-03 catalog — but that catalog is One settings (`sso:manage`, …), not Pay refund; (c) Pay-side overlay table — **second membership system**, forbidden unless written. (a) is the only mapping that does not invent a plane. **Not decided in 011.** This paper recommends (a) until someone writes otherwise.

4. **`GET /me` vs `GET /tenants` as membership directory.** Both exist. `/me.tenants` lacks `logo_url` and pagination; `GET /tenants` has them and requires `tenant:read` for keys. First connected call is `/me`. Does Pay ops ever need `GET /tenants` page 2?

5. **Accept-invite host.** Deep-link `http://localhost:5174/invites/accept?tenant_id=&token=` (stable) vs Pay-hosted page that POSTs One `accept-invite`. Copy-link format must stay `tenant_id` + `token`. If Pay hosts accept, Pay origin must be OIDC-redirectable and logged-in as the invitee email (`Invite:RequireEmailMatch` is **false** in Development, **true** in base `appsettings.json`).

6. **API key subject for Pay workers.** When Pay’s server uses `lzr_sk_` to check a user, it must already know that user’s Zitadel `sub`. That implies Pay stored `user_id` from `/me` at session start (or the incoming request still has the user JWT and Pay should use **that** instead of the key). Using the key to check without a user_id is impossible (400). Using the key id as user_id is impossible (400).

7. **OpenFGA disabled locally.** Development default `OpenFga:Enabled=false` makes `authz/check` a membership-role function. Staging/prod fail-closed. Pay’s “connected” test on a laptop can pass without OpenFGA. A 200 `{allowed:true}` locally is not proof of ReBAC. Do not document laptop DX as production authz.

8. **Staging One is not passed.** Pay 02 honesty: One staging proof is **NOT PASSED**. Packages unpublished. First-party Pay may still call local `:8080`. Do not block dogfood on npm or a hosted SKU.

9. **Language of the Pay host.** `011-05-language.md` argues Go. `apps/lazuar-pay` is C# on 8081. This HTTP façade does not care. A Go rewrite would still call the same URLs. Do not let the language debate spawn a second `/me` in `pay-spec`.

10. **Header name leftover.** `TenantAccessService` XML-doc still says “Never trust `X-Tenant-Id` alone” while the implemented header is `X-Lazuar-Tenant-Id`. Pay must send the latter. If Pay sends `X-Tenant-Id`, `/me.active_tenant_id` will not populate.

11. **Platform admin humans using Pay.** `/me.is_platform_admin` can be true. `RequireMembershipAsync` lets platform admins into tenant routes without a membership row. Pay should not special-case that for merchant money. Staff support stays `:5173` / One.

12. **Suspend lag.** If One webhook `tenant.suspended` is late, “money in Pay is still true; staff access may lag” (Pay 02). Call 2 (`authz/check`) after suspend: members may still GET tenant; mutating One routes 403. Pay must decide whether a **stale** allowed=true is acceptable for a refund during lag. Recommendation: check on mutating Pay routes; accept the race; do not put buyer entitlement in One.

13. **`POST /tenants` self-serve flag.** `Platform:AllowSelfServeTenantCreate` is true in Development. If someone turns it off, Pay “create workspace” 403s. Is local dogfood allowed to require a pre-seeded tenant instead?

14. **Runtime-only `GET /public/oidc-apps/{clientId}/redirect-origins`.** Not in TypeSpec OpenAPI. Login depends on it. Pay should not re-spec it; Pay should not rely on it except insofar as login finalize needs Pay’s redirects **on the app object** created in step 1.

15. **one-client is incomplete for invites/apps.** Even a TS Pay SPA cannot invite or register apps through `createClient` without raw fetch. That is fine. Do not treat client coverage as the façade.

16. **Rate limit 429 on check.** 30/minute/user. Pay must not check on every keystroke. Cache `allowed` per `(user_id, tenantId, relation)` for a short TTL; still re-check on mutating money routes.

17. **Wipe leftovers vs Pay rows.** One `POST …/delete` does not drop Pay’s Stripe keys / journal. Pay needs its own wipe policy when One tenant is deleted (`tenant.deleted` webhook, later). Out of this slice; do not assume One cascade.

18. **Old `packages/api-spec` on 8080 in the Pay repo.** Focused Pay README: “Listen on **8081** so the old API can keep **8080**.” Locally, **One** also wants **8080**. Two processes cannot bind the same port. Dogfood machine layout: run **One** on 8080, focused Pay on 8081, **do not** run old `apps/lazuar-api` at the same time. Unwritten in 011; will break the first curl if ignored.

---

## 9. Absolute URL cheat sheet (local)

Assume `ONE=http://localhost:8080/api/v1`, `TENANT` is a UUID, Bearer is a user access_token unless noted.

| # | Call | Absolute |
|---|------|----------|
| 1 | Whoami | `GET http://localhost:8080/api/v1/me` |
| 2 | Check member | `POST http://localhost:8080/api/v1/tenants/{TENANT}/authz/check` |
| 3 | Create org | `POST http://localhost:8080/api/v1/tenants` |
| 4 | Get org | `GET http://localhost:8080/api/v1/tenants/{TENANT}` |
| 5 | Invite | `POST http://localhost:8080/api/v1/tenants/{TENANT}/members/invite` |
| 6 | Accept | `POST http://localhost:8080/api/v1/tenants/{TENANT}/members/accept-invite` |
| 7 | Mint key | `POST http://localhost:8080/api/v1/tenants/{TENANT}/api-keys` |
| 8 | Register SPA | `POST http://localhost:8080/api/v1/tenants/{TENANT}/apps` |

Pay liveness (not One): `GET http://localhost:8081/health`, `GET http://localhost:8081/v1/health`.

Zitadel authority (OIDC, not One API): `http://localhost:8085`.  
Product login: `http://localhost:5175`.  
Accept-invite page today: `http://localhost:5174/invites/accept?tenant_id={TENANT}&token=…`.

---

## 10. Mapping first-slice tracker → this façade

From `12-first-slice-tracker.md` One side (S0):

| Step | Tracker job | HTTP in this paper |
|------|-------------|--------------------|
| 1 | Register Pay SPA | `POST /tenants/{id}/apps` **now** (or One seed — not an HTTP Pay call) |
| 2 | Sign-in `:5175`. `GET /me` | OIDC (not One API) + **`GET /me` now** |
| 3 | Create workspace = `POST /tenants` | **`POST /tenants` now** (or pick `/me.tenants[]`) |
| 4 | Invite copy-link | **`POST …/members/invite` now**; accept **now or deep-link** |
| 5 | Mint `lzr_sk_`; `authz/check` member | **`POST …/api-keys` now**; **`POST …/authz/check` now** (second connected call) |
| 6 | Subscribe `member.*` / `tenant.suspended` | Adjacent webhook routes — **later** relative to this slice’s implement target; **not skip** |
| 7 | Stop: no SCIM, no custom FGA types, no npm, no hosted SKU | **skip** those doors forever in v1 |

Pass lock from 03: no Pay password form, no second org table, buyer is not a Zitadel human, merchant not sent to `lazuar-admin`. This façade is how those locks stay true: identity HTTP terminates on One `:8080/api/v1`, not on Pay `:8081` and not on the museum `/one/auth/*`.
