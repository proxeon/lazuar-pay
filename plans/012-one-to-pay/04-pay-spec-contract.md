# 04 — Pay TypeSpec vs One TypeSpec vs old `api-spec`

**Date:** 20 August 2026  
**Slice:** Should Pay’s TypeSpec grow `GET /v1/whoami`? Must Pay **not** copy One tenant/invite routes? How frontends (`@repo/api-types-ts`, ops) confuse this.  
**Type:** Contract analysis. **Not** an implementation order. **Does not** edit `packages/pay-spec` (draft TypeSpec lives in this paper).  
**Repos:**

| Repo | Path | HEAD SHA | Commit | When |
|------|------|----------|--------|------|
| Pay (this tree) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `6ca8f19f4b28c056f852b7b579b5b30428e48ad6` | `feat(pay): add TypeSpec package for the focused Pay host` | 2026-08-20 21:00:06 +0800 |
| One (sibling) | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` | `WIP: Thu Aug 20 21:24:22 +08 2026` | 2026-08-20 21:24:22 +0800 |

**Upstream product contract (Pay as Consumer-0):** [`plans/011-new-lazuar-pay/02-one-integration.md`](../011-new-lazuar-pay/02-one-integration.md). First-slice sequence: [`03-first-slice.md`](../011-new-lazuar-pay/03-first-slice.md). Public door: [`01-product.md`](../011-new-lazuar-pay/01-product.md) (`POST /v1/checkouts`). Tracker rows: `NP-ONE-*`, `NP-API-*`, `NP-XX-007` / `NP-XX-014` in [`11-checklist.md`](../011-new-lazuar-pay/11-checklist.md).

**Honesty about this paper:** One staging proof is **NOT PASSED** (011 §2). Packages `@lazuar/one-client` / `one-react` / `one-cli` are unpublished workspace packages. That does **not** change which TypeSpec owns which HTTP path. Pay may import the workspace One client later; Pay must not become the owner of One’s OpenAPI.

---

## 0. Answers, then the rest of the paper

1. **Should Pay’s TypeSpec grow `GET /v1/whoami`?**  
   **Yes, as a Pay-facing session/introspect of the caller as Pay sees them** — Pay’s JSON, Pay’s `/v1` door, Pay’s host (`:8081`). **No, not as a copy of One’s `GET /me` (`Platform.MeResponse`) and not as a rename of old Hub `GET /one/auth/me` (`One.AuthUser`).** One remains the source of truth for identity, membership, and invite inbox. Pay `whoami` is a **projection** Pay is willing to serve to *its* clients (ops-as-client-of-`/v1`, `lzr_sk_` workers, later SDKs). It must not become a second membership directory.

2. **Must Pay NOT copy One tenant/invite routes?**  
   **Must not.** `POST /tenants`, `GET /tenants/{id}/members`, `POST /tenants/{id}/members/invite`, `GET /me/invites`, `POST /tenants/{id}/members/accept-invite`, SSO/SCIM, `POST /platform/tenants` — those live in **One’s** TypeSpec (`lazuar-one/packages/api-spec`). Copying them into `packages/pay-spec` would re-implement `Modules/One` at the contract layer, which is the failure mode 011 exists to prevent (`NP-XX-007`, `NP-XX-014`). Pay UI that needs a roster or an invite **calls One**, not Pay.

3. **How frontends confuse this today**  
   `lazuar-ops` (and portal, admin) import `@repo/api-types-ts`, which is generated from **old** `packages/api-spec` (the modular monolith on **`:8080`**, base path **`/api/v1`**). Ops session is `client.GET("/one/auth/me")` against that host. That path is **not** One’s `GET /me`, **not** focused Pay, and **will not** exist on `:8081`. Connecting One does **not** mean Pay implements `POST /one/auth/login`. Pointing `VITE_API_URL` at 8081, or growing `pay-spec` with `/one/*`, would type-check a lie.

4. **When to generate `pay-types-ts`**  
   **Not now.** No frontend talks to `:8081`. `task gen` / `@repo/api-types-ts` stay bound to the old monolith. Spin `@repo/pay-types-ts` only when a Pay client (new ops, or a deliberately split ops money client) sets `baseUrl` to the Pay host.

5. **Honesty pipeline**  
   **Do not** hook `packages/pay-spec` into `task gen`, `task gen:spec`, `task contracts:honesty`, `packages/api-spec/honesty-allowlist.yaml`, or the CI `contracts` job that diffs `packages/api-types-ts`. Those tools scrape `apps/lazuar-api` and compile `packages/api-spec`. Mixing hosts would force allowlist lies or false greens.

The rest of this paper is the evidence, the three contracts as they exist at the SHAs above, the field-level JSON mismatch, the frontend call graph, the route-ownership map, and a **paper-only** TypeSpec draft for `GET /v1/whoami`.

---

## 1. Three contracts today

There are **three** TypeSpec trees that an engineer in this monorepo can open. They share a compiler (`@typespec/compiler` ~1.13) and an OpenAPI 3 emitter. They do **not** share a namespace, a server, a path prefix, a generated client, or a source of truth. Treating any two as “the same One API” is how you get a password form on Pay.

### 1.1 Focused Pay — `packages/pay-spec` (this repo)

| | |
|--|--|
| Package | `@repo/pay-spec` `0.1.0` (private) |
| Entrypoint | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/main.tsp` |
| OpenAPI | `packages/pay-spec/dist/openapi.yaml` (gitignored; compile with `task pay:spec` or `pnpm --filter @repo/pay-spec build`) |
| Namespace | `LazuarPay` |
| Service title | `Lazuar Pay` `0.1.0` |
| Server | `http://localhost:8081` — “Local focused Pay host” |
| Path prefix | **`/v1`** (not `/api/v1`) |
| Host | `apps/lazuar-pay` (`Program.cs`, listen **8081**) |
| Generated TS/C# clients | **None** |
| Honesty gate | **None** |
| README rule | “Not `packages/api-spec`. Do not import One, LHDN, or `/public/commerce` routes here.” “Grow `main.tsp` when `POST /v1/checkouts` exists.” |

**Entire current TypeSpec** (`main.tsp` at `6ca8f19`):

```tsp
import "@typespec/http";
import "@typespec/openapi";

using Http;
using OpenAPI;

/** Focused Pay HTTP contract. Not packages/api-spec. Grow when POST /v1/checkouts exists. */
@service(#{ title: "Lazuar Pay" })
@info(#{ version: "0.1.0" })
@server("http://localhost:8081", "Local focused Pay host")
namespace LazuarPay;

model HealthResponse {
  status: string;
}

@route("/v1")
@tag("Health")
interface Health {
  /** Process liveness for the focused Pay host. */
  @get
  @route("/health")
  check(): HealthResponse;
}
```

Emitted OpenAPI has one path: `GET /v1/health` → `{ status: string }`. No auth scheme. No `whoami`. No tenants. No invites. No checkouts. No webhooks.

**Host vs spec (already a small gap, not this slice’s job to “fix” by growing identity):**

| Method | Path | In `pay-spec`? | On `Program.cs`? | Tests |
|--------|------|----------------|------------------|-------|
| `GET` | `/v1/health` | Yes | Yes | `HealthTests.V1_health_returns_ok` |
| `GET` | `/health` | **No** | Yes | `HealthTests.Health_returns_ok` |

`Program.cs` at this SHA:

```csharp
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/v1/health", () => Results.Ok(new { status = "ok" }));
```

`GET /health` is a process probe (old monolith honesty script already excludes host `/health` from `/api/v1` product honesty). Leaving it off `pay-spec` is consistent with that. Do not use this gap as an excuse to dump `/one/auth/*` into `pay-spec` “for honesty.”

Isolation tests (`IsolationTests.cs`) only assert the Pay `.csproj` does not reference `lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api`. They do **not** assert TypeSpec isolation. The README and this paper have to.

### 1.2 Real One — `lazuar-one/packages/api-spec` (sibling repo)

| | |
|--|--|
| Package | `@repo/api-spec` `0.1.0` (private) — **same npm name as old Pay `api-spec`**, different git repo |
| Entrypoint | `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/main.tsp` |
| OpenAPI | `lazuar-one/packages/api-spec/dist/openapi.yaml` |
| Namespace | `LazuarOneApi` |
| Service title | `Lazuar One API` `0.1.0` |
| Servers | `https://api.lazuar.com/api/v1` (placeholder), `http://localhost:8080/api/v1` |
| Path prefix | **`/api/v1`** on the host; TypeSpec routes are **relative to that** (`/me`, `/tenants`, …) |
| Generated TS | `lazuar-one/packages/api-type-ts` (`@repo/api-type-ts`, **singular “type”**) |
| Hand-written client | `lazuar-one/packages/one-client` (`@lazuar/one-client`) wraps `GET /me`, tenants, api-keys, authz |
| C# contracts | `lazuar-one/packages/api-type-dotnet` |

`main.tsp` imports:

- `modules/platform` — `GET /health`, **`GET /me`**, **`GET /me/invites`**, `GET /platform/tenants` (staff), social IdPs
- `modules/tenants` — create/list/get/patch tenant, suspend/reactivate/retry-provision/transfer-ownership/leave/delete, **members**, **invites**, domains, custom roles, events, audit
- `modules/apps` — `POST /tenants/{tenantId}/apps` (OIDC SPA/web/cc)
- `modules/authz` — `POST /tenants/{tenantId}/authz/check|batch-check|list-objects`
- `modules/api-keys` — `POST/GET/DELETE /tenants/{tenantId}/api-keys` (`lzr_sk_`)
- `modules/webhooks` — tenant webhook endpoints + rotate + test
- `modules/enterprise` — SSO connections (SCIM protocol is a sibling `scim/` folder, not this file)

This is the **SoT for identity HTTP**. Pay as Consumer-0 (011 `02-one-integration.md`) is a **client** of these routes. Pay TypeSpec does not re-declare them.

Identity snapshot One actually serves (`GET /me` → `LazuarOneApi.Platform.MeResponse`):

```tsp
model MeResponse {
  user_id: string;          // JWT: Zitadel sub. API key: key GUID. Not a membership row id.
  email?: string;
  name?: string;
  tenants: TenantSummary[]; // always present as []
  is_platform_admin: boolean; // Platform:AdminEmails; keys never true
  active_tenant_id?: string;  // hint when X-Lazuar-Tenant-Id matches; never authorize from this
  active_role?: string;       // owner | admin | member when active_tenant_id is set
}

model TenantSummary {
  id: string;
  slug: string;
  name: string;
  role?: string;
  status?: string;
  permissions: string[];    // ROLE-03 chrome hint; never authorize from this alone
}
```

`@lazuar/one-client` `createClient().me.get()` is literally `GET {baseUrl}/me` with `Authorization: Bearer` + optional `X-Lazuar-Tenant-Id`. That is the call S0 step 2 (`NP-ONE-006`) tells Pay to make **to One**, not to itself.

There is **no** `POST /auth/login` in One’s TypeSpec. Login is OIDC against Zitadel (product login `:5175`). Password collection in a Pay form is `NP-XX-007`.

### 1.3 Old modular monolith — `packages/api-spec` (this repo, still live)

| | |
|--|--|
| Package | `@repo/api-spec` `1.0.0` — **collides in name** with One’s package |
| Entrypoint | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/main.tsp` |
| Product docs | `docs-one.tsp`, `docs-ops.tsp`, `docs-billing.tsp`, `docs-lhdn.tsp`, `docs-commerce.tsp`, `docs-payments.tsp` (ADR 007) |
| OpenAPI | `packages/api-spec/dist/openapi.yaml` (+ product-scoped `dist/{one,ops,billing,lhdn,commerce,payments}/`) |
| Namespace | `LazuarApi` |
| Service title | `Lazuar Platform API` `1.0.0` |
| Servers | `https://hub.lazuar.com/api/v1`, `http://localhost:8080/api/v1` |
| Path prefix | **`/api/v1`** |
| Host | `apps/lazuar-api` listen **8080** |
| Generated TS | `packages/api-types-ts` (`@repo/api-types-ts`, **plural “types”**) via `openapi-typescript` |
| Generated C# | `packages/api-types-dotnet` (`Lazuar.ApiContracts.cs`) via NSwag |
| Honesty | `packages/api-spec/honesty-allowlist.yaml` + `scripts/check-openapi-minimal-honesty.mjs` + CI `contracts` job |
| Pipeline | `task gen` → `gen:spec` → `gen:types-ts` → `gen:types-dotnet` → `gen:sdk-lhdn` |

`main.tsp` **imports the homemade One module**:

```tsp
import "./modules/one/models.tsp";
import "./modules/one/routes.tsp";
```

plus messaging, ops, commerce, communications, billing, lhdn, payments, crm, platform.

README still says this package is “the **single source of truth** for Lazuar Platform API contracts.” That sentence is true **for the old monolith**. It is **false** for focused Pay and false for sibling One. ADR 005/006/007 apply to `apps/lazuar-api`, not to `apps/lazuar-pay`.

**Old One identity HTTP** (the thing ops is typed against):

| Method | TypeSpec path (relative to `/api/v1`) | Returns |
|--------|----------------------------------------|---------|
| `POST` | `/one/public/register` | `LoginResponse` `{ user: AuthUser }` + cookie |
| `GET` | `/one/public/pricing` | `PublicPricingDto` (Hub price card, credits, SST notes) |
| `POST` | `/one/auth/login` | `LoginResponse` + `lazuar_auth` cookie |
| `POST` | `/one/auth/logout` | `StatusResponse` |
| `POST` | `/one/auth/forgot-password` | `StatusResponse` |
| `POST` | `/one/auth/reset-password` | `StatusResponse` |
| `POST` | `/one/auth/verify-email` | `StatusResponse` |
| `POST` | `/one/auth/resend-verification` | `StatusResponse` |
| `GET` | `/one/auth/me` | **`AuthUser`** |
| `PUT` | `/one/me/profile` | `StatusResponse` |
| `PUT` | `/one/me/security/password` | `StatusResponse` |
| `GET` | `/one/me/entitlements` | `EntitlementDto[]` |
| `GET/POST` | `/one/workspaces` | list / create |
| `GET/PUT/DELETE` | `/one/workspaces/{id}` | workspace |
| `GET` | `/one/workspaces/{id}/members` | `WorkspaceMemberDto[]` |
| `POST/GET` | `/one/workspaces/{id}/invites` | invite / list |
| `DELETE` | `/one/workspaces/{id}/invites/{inviteId}` | revoke |
| `DELETE` | `/one/workspaces/{id}/members/{userId}` | remove |
| `POST` | `/one/workspaces/invites/accept` | `{ token }` |
| `GET` | `/one/workspaces/{id}/audit` | paginated `AuditEventDto` |
| `GET/POST` | `/one/workspaces/{id}/apps` | Hub “app entitlements”, **not** One OIDC apps |
| `GET/POST/PUT/DELETE` | `/one/workspaces/{id}/webhooks` | Hub outbound webhooks |
| `GET/POST/DELETE` | `/one/api-keys` | Hub `sk_test_` / `sk_live_` |
| `POST` | `/one/integrations/workspaces/provision` | Aura-shaped provisioner |
| `POST` | `/one/storage/presigned-url` | R2 presign |
| `GET` | `/public/one/{tenantSlug}/branding` | public checkout chrome |
| `GET` | `/platform/auth/me` | same `AuthUser` (admin cookie realm) |
| `GET` | `/integrations/payments/me` | **machine-key introspect** (`PaymentsMeResponseDto`), **not** human session |

`AuthUser` in old TypeSpec (`modules/one/models/auth.tsp`):

```tsp
model AuthUser {
  email: string;
  name: string;
  role: string;              // JWT claim: CLIENT / SUPER_ADMIN / … — not owner|admin|member
  is_email_verified: boolean;
}
```

Runtime (`AuthEndpoints.cs` `GET /auth/me`) fills exactly those four fields from `GlobalUsers` + `ClaimTypes.Role`, after a **security-stamp** check that can delete `lazuar_auth`. There is no `user_id` in the JSON. There is no `tenants[]`. Workspace membership is a **second** call: `GET /one/me/entitlements` → `{ workspace_id, workspace_name, workspace_slug, role }[]`.

`docs-one.tsp` already warns, in a different direction:

> Machine-key introspect for Payments is `GET /integrations/payments/me` (Payments product), **not** `/one/auth/me`.

That warning is about **not conflating Hub human session with Hub K1 introspect**. It does **not** mention sibling One’s `GET /me`. The confusion this paper is about is **three** `/me`-shaped things, not two.

### 1.4 Side-by-side (the only table that matters)

| | Focused Pay `pay-spec` | Sibling One `api-spec` | Old Hub `api-spec` |
|--|------------------------|------------------------|--------------------|
| Process | `apps/lazuar-pay` **8081** | `lazuar-one` API **8080** (One’s) | `apps/lazuar-api` **8080** (Hub’s) |
| Sold door | `/v1/...` | `/api/v1/...` | `/api/v1/...` |
| Identity SoT? | No | **Yes** | Homemade (`Modules/One`) |
| Human “who am I?” | **not yet**; proposed `GET /v1/whoami` (Pay JSON) | `GET /me` → `MeResponse` | `GET /one/auth/me` → `AuthUser` |
| Login | **Must not** exist | OIDC / Zitadel (`:5175`) | `POST /one/auth/login` password + cookie |
| Tenants / workspaces | **Must not** exist | `/tenants`, `/tenants/{id}/members`, invites | `/one/workspaces`, `/one/me/entitlements` |
| Invites | **Must not** exist | `/tenants/{id}/members/invite`, `/me/invites`, accept-invite | `/one/workspaces/{id}/invites`, `/one/workspaces/invites/accept` |
| API keys | Pay may later mint **Pay** keys, or **use** One `lzr_sk_` | `/tenants/{id}/api-keys` (`lzr_sk_`) | `/one/api-keys` (`sk_test_`/`sk_live_`) |
| Money | `POST /v1/checkouts` (planned, `NP-API-001`) | None | `/public/commerce/*`, `/integrations/payments/checkouts` |
| Frontend types today | none | `@repo/api-type-ts` + `@lazuar/one-client` | **`@repo/api-types-ts`** (ops/portal/admin) |
| Compile task | `task pay:spec` | One’s `pnpm --filter @repo/api-spec build` | `task gen` / `task gen:spec` |
| Honesty | none (keep it that way for now) | One’s own (not this repo’s script) | `task contracts:honesty` scrapes **`apps/lazuar-api` only** |

Local port **8080** is already overloaded in docs: 011 tells Pay to call One at `http://localhost:8080/api/v1`, and old Hub **is** that port in this checkout. That is an ops-environment problem (Two processes cannot both bind 8080). It is **not** a reason to put One routes on Pay `:8081`. Pay stays 8081. One stays One’s 8080. Old Hub stays old Hub’s 8080 until it is turned off.

### 1.5 Name collisions that will bite copy-paste

| String | In Pay repo | In One repo |
|--------|-------------|-------------|
| `@repo/api-spec` | old Hub TypeSpec | real One TypeSpec |
| `@repo/api-types-ts` | Hub generated TS (plural) | — |
| `@repo/api-type-ts` | — | One generated TS (singular) |
| `@repo/api-types-dotnet` | Hub NSwag | — |
| `@repo/api-type-dotnet` | — | One NSwag |
| `namespace` | `LazuarApi` / `LazuarPay` | `LazuarOneApi` |
| `GET …/me` | Hub `/one/auth/me`, Hub `/platform/auth/me`, Hub `/integrations/payments/me` | One `/me` |
| `X-Tenant-Id` | ops `api-client.ts` sets this | One uses **`X-Lazuar-Tenant-Id`** (hint only) |

If someone adds `pay-types-ts` later, name it **`@repo/pay-types-ts`** (or `@lazuar/pay-types`). Do **not** reuse `@repo/api-types-ts`. Do **not** reuse `@repo/api-type-ts`.

---

## 2. Recommendation: Pay spec may describe Pay-facing `whoami` (Pay’s JSON)

### 2.1 What `whoami` is for

011 `NP-API-004`: “Merchant ops is a client of `/v1` (One user JWT or `lzr_sk_`). No back-door table reads.”

Once Pay accepts `Authorization: Bearer` (One access_token **or** a key Pay recognizes), a Pay client needs a cheap answer to: **does this process accept me, as which org, as user or machine, with which Pay scopes?** That is not One’s job. One can tell you membership. One cannot tell you “this principal may `checkouts:write` on this Pay org” or “Pay has bound this One tenant as `org_id`.”

So Pay **may** (and, when auth lands, **should**) declare:

```http
GET /v1/whoami
Authorization: Bearer <One access_token | Pay-recognized key>
X-Lazuar-Tenant-Id: <hint, optional>
```

on **`packages/pay-spec`**, implemented on **`apps/lazuar-pay`**, documented as **Pay JSON**.

### 2.2 What `whoami` is not

| Not | Why |
|-----|-----|
| A copy of `Platform.MeResponse` | That schema is One’s SoT. Duplicating `tenants[]`, `permissions[]`, `is_platform_admin`, `active_role` in Pay TypeSpec creates two compilers for one fact. Drift is guaranteed (One already documents that `GET /me` can **write** — domain auto-join, SSO JIT). |
| A rename of `One.AuthUser` | `{ email, name, role, is_email_verified }` is Hub cookie-session. `role` is `CLIENT`/`SUPER_ADMIN`, not One `owner`/`admin`/`member`. Email verification is One’s (or Hub’s leftover). Pay does not store passwords (`NP-XX-007`). |
| A replacement for `GET /me` | S0 step 2 is still “sign-in via `:5175`. `GET /me`” **on One**. Pay `whoami` does not list memberships, does not run JIT join, does not return invite inbox. |
| A replacement for `GET /integrations/payments/me` | That is Hub K1 introspect (`workspace_id`, `organization_id`, `is_test_mode`, `scopes`, `has_active_gateway`). New Pay may later have a **machine** shape; it should still live under `/v1`, not `/integrations/payments/me`. |
| `GET /v1/me` | Path collides conceptually with One `/me`. Clients will import the wrong types. `whoami` is deliberately a different word. |
| An unauthenticated probe | Unauthenticated liveness is `/v1/health` (and process `/health`). `whoami` is 401 without a bearer. |
| Something to add **this week** with no host route | Spec-without-impl is a phantom (old honesty exists to stop that). Impl-without-spec is the other phantom. Land them in the **same Pay PR**, not in a “grow the spec first” PR, and **not** in this paper’s follow-up as an edit to `main.tsp` until the host authenticates. |

### 2.3 Pay JSON (what is allowed on the wire)

Pay `whoami` should answer **Pay questions**:

- Who is the caller **as Pay binds them** (`user_id` opaque; for a human this **is** One’s `sub`, copied, not re-issued).
- Which **org** is in context (`org_id` **is** One tenant id unless Pay writes a reason to map — 011 binding decision 4; `NP-ONE-009`).
- Principal class: human JWT vs API key (Pay must not confuse a key GUID with a Zitadel user; One’s `MeResponse` already warns about this).
- **Pay-owned** scopes this principal may exercise **on this Pay process** (catalog Pay defines: e.g. `checkouts:write`, `refunds:write`, `keys:write` — **not** One’s ROLE-03 `tenant:delete` / `sso:manage` / `scim:manage`).
- Optional chrome copies: `email`, `name`, `org_role` as **hints**. Same rule as One: **never authorize from the hint alone**. Path `{org_id}` + One `authz/check` (or a Pay cache of a check Pay actually made) is SoT (`NP-ONE-007`, `NP-ONE-015`).

Pay `whoami` should **not** answer **One questions**:

- Full membership list (`tenants[]`) — client already has `GET /me` or `GET /tenants` on One.
- Invite inbox (`GET /me/invites`).
- `is_platform_admin` / `Platform:AdminEmails` — One staff break-glass. Pay must not grow a second admin email list (`NP-XX-018` merchants never go to `lazuar-admin`; Pay also must not invent Hub `SUPER_ADMIN` on `/v1`).
- Email verified flag, password change, profile update.
- Custom role catalog, domain enrollment, SSO connection status.
- Zitadel org id, provisioning_step, last_error.

If ops needs those fields, ops calls **One**. If Pay needs them to authorize a money route, Pay calls One **server-side** (`authz/check`) and does not echo the whole `MeResponse` out of `/v1/whoami`.

### 2.4 Timing relative to `POST /v1/checkouts`

`pay-spec` README: “Grow `main.tsp` when `POST /v1/checkouts` exists.”

That sentence is the **sold door** (Bezos, `NP-API-001`). `whoami` is **session chrome for the first client of that door** (`NP-API-004`). Order:

1. **Now (this paper):** draft only. Do not edit `packages/pay-spec`.
2. **When Pay accepts Bearer (S0/S1 boundary):** add `GET /v1/whoami` to TypeSpec **and** `MapGet` on the Pay host in one PR. Still no `pay-types-ts` until a TS client exists.
3. **When `POST /v1/checkouts` exists:** grow the rest of `/v1` (checkouts, payment status, provider webhook URL). `whoami` should already be there so ops-as-client can fail closed before painting a dashboard.

Do not grow `whoami` before Bearer exists: the route would 401 forever or, worse, return an anonymous stub that frontends treat as a session.

Do not skip `whoami` and tell ops to keep calling `/one/auth/me` on Pay: that is how you copy Hub identity onto 8081.

### 2.5 Authorization rules for the route itself

Copied from 011, applied to Pay:

- Send **access_token** as Bearer; never `id_token` (`NP-ONE-003`).
- `X-Lazuar-Tenant-Id` is a **hint**. If present and not a membership (Pay confirms with One, or with a cache Pay is honest about), omit `org_id` or 403 — do **not** authorize the hinted org (`NP-ONE-007`). Ops today sets **`X-Tenant-Id`** (short name) on the Hub client; do not carry that header name onto Pay without an explicit alias. Prefer One’s `X-Lazuar-Tenant-Id`.
- Do not parse `urn:zitadel:iam:org:project:roles` (`NP-XX-024`).
- API key: `user_id` is the key id; `principal: "api_key"`; keys are never `owner` (One already says this). VIEWER/member restrictions on charge/refund are **Pay policy** using One role + `authz` (`NP-ONE-021`), not a field on `whoami` that the UI “trusts.”
- Do not hammer One `GET /me` from a hot loop on every `whoami` if `whoami` becomes chatty; 011 says `GET /me` can **write**. Cache with a short TTL or rely on the JWT + a membership check Pay already did. This is an implementation note, not a TypeSpec field.

---

## 3. Must Pay NOT copy One tenant / invite routes?

**Yes. Must not.** This is the contract-layer restatement of “Do not rebuild `Modules/One` inside Pay.”

### 3.1 What “copy” would look like (and why it is attractive)

Old ops already has working UI for:

- Create workspace (`CreateWorkspaceModal` → `POST /one/workspaces`)
- Team roster + invite + revoke + remove (`TeamPage` → `/one/workspaces/{id}/members|invites`)
- Accept invite (`AcceptInvitePage` → `GET /one/auth/me` then `POST /one/workspaces/invites/accept`)
- General settings (`GET/PUT /one/workspaces/{id}`)
- Audit (`GET /one/workspaces/{id}/audit`)
- Hub webhooks and Hub API keys (Developer settings, ApiKeys pages)

The fastest way to “connect One” while keeping those files compiling against `@repo/api-types-ts` is to **re-declare the same paths on Pay** and proxy them. That is a second identity product. It is also how you keep the Hub path names (`/one/workspaces`, `CLIENT`/`ADMIN` roles, `X-Tenant-Id`) forever.

011 S0 already lists the **One** paths Pay should **call**:

| Pay use | Call **One** | Do **not** put on `pay-spec` |
|---------|--------------|------------------------------|
| Create workspace | `POST /tenants` | `POST /one/workspaces`, `POST /v1/tenants` |
| List memberships | `GET /me` or `GET /tenants` | `GET /one/me/entitlements`, `GET /v1/workspaces` |
| Profile | `GET/PATCH /tenants/{id}` | `PUT /one/workspaces/{id}` |
| Roster | `GET /tenants/{id}/members` | `GET /one/workspaces/{id}/members` |
| Invite | `POST /tenants/{id}/members/invite` | `POST /one/workspaces/{id}/invites` |
| Pending | `GET /tenants/{id}/invites` | `GET /one/workspaces/{id}/invites` |
| Revoke | `DELETE /tenants/{id}/invites/{inviteId}` | same Hub path |
| Resend | `POST /tenants/{id}/invites/{inviteId}/resend` | Hub has no resend in TypeSpec |
| Accept | `POST /tenants/{id}/members/accept-invite` | `POST /one/workspaces/invites/accept` |
| Inbox | `GET /me/invites` | (Hub has no inbox; mail token only) |
| Role change | `PATCH /tenants/{id}/members/{userId}` | (Hub invite role is string; change is thin) |
| Remove | `DELETE /tenants/{id}/members/{userId}` | Hub equivalent |
| Leave / transfer / delete tenant | One `POST .../leave|transfer-ownership|delete` | do not invent Pay org lifecycle |
| Staff directory | **do not call** `POST/GET /platform/tenants` (`NP-XX-023`) | |
| OIDC app for Pay SPA | `POST /tenants/{id}/apps` | Hub `/one/workspaces/{id}/apps` is **entitlement toggles**, a different noun |
| `lzr_sk_` | `POST /tenants/{id}/api-keys` | Hub `/one/api-keys` is a different key prefix (`sk_test_`) |
| Authz | `POST /tenants/{id}/authz/check` | do not add FGA types `payment`/`document` (`NP-XX-015`); Pay does not get `authz/write` (`NP-XX-016`) |
| SSO / SCIM | One enterprise; only when a named merchant asks | never on Pay TypeSpec in v1 |
| Login / logout / password / verify | One OIDC + Zitadel | **`/one/auth/*` must not appear on Pay** |

JSON is not a 1:1 rename. Examples:

| Hub (`LazuarApi.One`) | One (`LazuarOneApi`) |
|-----------------------|----------------------|
| `WorkspaceDto` `{ id, name, slug, is_active, logo_url, primary_color }` | `Tenant` `{ id, slug, name, status, zitadel_org_id?, provisioning_step?, metadata?, logo_url?, created_at, updated_at }` — **no `primary_color`** |
| `EntitlementDto` `{ workspace_id, workspace_name, workspace_slug, role }` | `TenantSummary` `{ id, slug, name, role?, status?, permissions[] }` |
| `WorkspaceMemberDto` `{ id, global_user_id, name, email, role, joined_at }` | `Member` `{ id, user_id, email?, name?, role: owner\|admin\|member, status, custom_role_id?, created_at }` |
| `CreateWorkspaceInvitationDto` `{ email, role }` role is free string (`MEMBER`/`ADMIN` in ops UI) | `InviteMemberRequest` `{ email, role?: owner\|admin\|member, custom_role_id? }` — **owner rejected**; omit role → tenant default |
| `AcceptWorkspaceInvitationDto` `{ token }` on `POST /one/workspaces/invites/accept` (no tenant in path) | `AcceptInviteRequest` `{ token }` on `POST /tenants/{tenantId}/members/accept-invite` (**tenant in path**) |
| Invite mail / ops `/accept-invite?token=` | Deep-link `lazuar-app` `/invites/accept?tenant_id=&token=` **or** post the same One API; **copy-link format must stay stable** (011) |
| `AuthUser.role` `CLIENT` / `SUPER_ADMIN` | `owner` / `admin` / `member` (+ optional custom role) |
| Cookie `lazuar_auth` + `credentials: "include"` | Bearer access_token; One client does not do cookie login |
| Header `X-Tenant-Id` | Header `X-Lazuar-Tenant-Id` |

A TypeSpec “facade” on Pay that kept Hub paths and mapped them to One would freeze the **wrong** names into Pay’s sold door. Strangers integrating `/v1` would learn Hub archaeology. **Do not.**

### 3.2 Branding / checkout chrome is Pay’s, membership is not

Hub `GET /public/one/{tenantSlug}/branding` (`PublicWorkspaceBrandingDto`: name, slug, logo, **primary_color**) is **checkout chrome**. Focused Pay will need **some** public branding on the hosted buyer page. That may live on Pay (`GET /v1/public/...` or similar) as **Pay catalog/branding**, with `logo_url` possibly copied from One `PATCH /tenants/{id}` (`logo_url`) or stored in Pay. That is **not** a tenant roster API. Do not smuggle `primary_color` into a copied `/tenants` resource “because Hub had it on WorkspaceDto.”

Hub `GET /one/workspaces/{id}/audit` is Hub’s workspace audit. Pay v1 audit is **Pay writes** in the same DB transaction (`NP-AUD-001`). One has `GET /tenants/{id}/audit` and `GET /tenants/{id}/events`. Pay may subscribe to One webhooks (`member.*`, `tenant.suspended`, …) and may pull events if push is missing. Pay TypeSpec still does not re-export One’s audit routes.

### 3.3 Provision / Aura / Hub API keys stay on the old spec until the old host dies

`POST /one/integrations/workspaces/provision`, `GET /one/api-keys`, Aura aliases, `sk_test_` prefixes — those are **Hub** integrator DX. New Pay does not need them in `pay-spec`. New Pay uses One `lzr_sk_` and Pay’s own `/v1` keys if Pay later mints a Pay-scoped key. Do not “port” the provisioner into Pay TypeSpec as compatibility.

### 3.4 What Pay TypeSpec **should** grow (ownership list)

**In `packages/pay-spec` (Pay process, `/v1`):**

- `GET /v1/health` (already)
- `GET /v1/whoami` (this paper; when Bearer exists)
- `POST /v1/checkouts` + `GET` payment/checkout status (`NP-API-001`, `NP-API-003`)
- Provider webhook URL(s) (`NP-API-002`) — inbound, likely untyped in public product docs or a dedicated machine tag
- Merchant catalog/products, gateway-key **Pay** APIs, refunds, receipts, subscriber list — as they are implemented, as the **sold door**
- Buyer magic-link / portal Pay APIs (payer plane, **not** Zitadel humans — `NP-XX-013`)
- Problem details / idempotency headers on money POSTs (`NP-API-006`)

**Not in `packages/pay-spec`:**

- Anything under `/one/*`
- Anything under `/tenants/*`, `/me`, `/me/invites`, `/platform/*`
- `/auth/login`, `/auth/logout`, forgot/reset/verify
- One OIDC app admin, One API keys, One webhooks, SSO, SCIM
- LHDN, Hub credits wallet, Hub `/ops/chat*`, Hub `/admin/commerce/*`, Hub `/public/commerce/*`
- `POST /platform/tenants`

**In One TypeSpec (already; Pay is a client):**

- Identity, tenancy, invites, authz, `lzr_sk_`, OIDC apps, One webhooks, enterprise

**In old `packages/api-spec` (frozen as Hub truth until Hub is off):**

- Keep generating `@repo/api-types-ts` for **current** ops/portal/admin **as long as they talk to 8080 Hub**
- Do not add focused-Pay routes here
- Do not delete Hub `/one/*` from here while ops still calls them
- Do not “move” Hub `/one/*` into `pay-spec`

---

## 4. How frontends confuse this

### 4.1 The typed client they actually have

`apps/lazuar-ops/src/lib/api-client.ts`:

```ts
import createClient from "openapi-fetch";
import type { paths, components } from "@repo/api-types-ts";

export const API_URL = import.meta.env.VITE_API_URL || "http://localhost:8080/api/v1";

export const client = createClient<paths>({
  baseUrl: API_URL,
  fetch: (input, init) => fetch(input, { ...init, credentials: "include" })
});

client.use({
  onRequest({ request }) {
    const tenantId = localStorage.getItem("ops_active_workspace_id");
    if (tenantId) {
      request.headers.set("X-Tenant-Id", tenantId);
    }
    return request;
  }
});

export type AuthUser = components["schemas"]["One.AuthUser"];
export type EntitlementDto = components["schemas"]["One.EntitlementDto"];
```

Facts packed into this one file:

1. Types come from **Hub** OpenAPI (`packages/api-types-ts/src/index.ts` generated from `packages/api-spec/dist/openapi.yaml`).
2. Default base URL is **Hub** `http://localhost:8080/api/v1` — **not** Pay `8081`, **not** One without Hub.
3. Auth is **cookie** (`credentials: "include"`, Hub `lazuar_auth`), not One Bearer.
4. Tenant hint is **`X-Tenant-Id`**, not One’s `X-Lazuar-Tenant-Id`.
5. Session type is **`One.AuthUser`** (`email`, `name`, `role`, `is_email_verified`), not `Platform.MeResponse`.

`lazuar-ops/package.json` depends on `"@repo/api-types-ts": "workspace:^"`. There is no `@lazuar/one-client`. There is no `@repo/pay-spec` consumer.

Same Hub client pattern:

- `apps/lazuar-admin/src/lib/api-client.ts` — `8080/api/v1`, `AuthUser = One.AuthUser`, session `GET /platform/auth/me` (admin cookie `lazuar_admin_auth`)
- `apps/lazuar-portal/src/modules/core/lib/server-client.ts` — `8080/api/v1`, forwards `lazuar_auth` cookie, pages call `GET /one/auth/me`

### 4.2 Ops call graph for “who am I?” and “who is on the team?”

| UI | Call (typed path) | Host it hits today | Real One equivalent | Pay `:8081` today |
|----|-------------------|--------------------|---------------------|-------------------|
| `App.tsx` `verifySession` | `GET /one/auth/me` | Hub 8080 | `GET /me` | **404 / no route** |
| `App.tsx` `HomeRedirect` | `GET /one/auth/me` | Hub 8080 | `GET /me` | no |
| `App.tsx` entitlements | `GET /one/me/entitlements` | Hub 8080 | `GET /me` / `GET /tenants` | no |
| `App.tsx` logout | `POST /one/auth/logout` | Hub 8080 | Zitadel end-session / One has no password logout | no |
| `LoginPage` sign-in | `POST /one/auth/login` `{ email, password }` | Hub 8080 | **OIDC to `:5175`** | **must never exist** |
| `LoginPage` sign-up | `POST /one/public/register` | Hub 8080 | `POST /tenants` after One login (no Pay password) | must never exist |
| Forgot / reset / verify | `/one/auth/forgot-password`, `reset-password`, `verify-email` | Hub 8080 | One/Zitadel | no |
| `CreateWorkspaceModal` | `POST /one/workspaces` | Hub 8080 | `POST /tenants` | no |
| `TeamPage` | `GET/POST/DELETE /one/workspaces/{id}/members\|invites` | Hub 8080 | `/tenants/{id}/members`, `/invites` | no |
| `AcceptInvitePage` | `GET /one/auth/me` + `POST /one/workspaces/invites/accept` | Hub 8080 | `POST /tenants/{id}/members/accept-invite` | no |
| `GeneralSettingsPage` | `GET/PUT /one/workspaces/{id}` | Hub 8080 | `GET/PATCH /tenants/{id}` | no |
| `AuditLogPage` | `GET /one/workspaces/{id}/audit` | Hub 8080 | One audit **or** Pay’s own audit later | no |
| Developer webhooks / API keys | `/one/workspaces/{id}/webhooks`, `/one/api-keys` | Hub 8080 | One webhooks / One api-keys **or** Pay’s own money webhooks | no |

`AcceptInvitePage` additionally diffs `GET /one/me/entitlements` before/after accept to guess `workspace_id`. One accept returns a `Member` and the tenant is **already in the path**. The Hub dance is compensating for Hub’s accept route not returning the workspace.

`TeamPage` invite roles in UI: `MEMBER` / `ADMIN`, and `canInvite` is `workspaceRole === "ADMIN" || "SUPER_ADMIN"`. Hub entitlements use those strings. One uses `owner|admin|member`. A mechanical path swap without a role-map will invite the wrong people and hide the invite button for One `owner`.

### 4.3 Why TypeScript will not save you

`openapi-fetch` `client.GET("/one/auth/me")` is typed against **`paths` from Hub**. That key exists in `@repo/api-types-ts` (`"/one/auth/me"` → `OneOperations_getMe` → `One.AuthUser`). It does **not** exist in:

- `@repo/pay-spec` / future `@repo/pay-types-ts` (only `/v1/health` today)
- `@repo/api-type-ts` (One) — One has `"/me"`, not `"/one/auth/me"`

So:

- Keep ops pointed at Hub 8080 → compiles, runs, **is the old product**.
- Point `VITE_API_URL` at `http://localhost:8081` or `http://localhost:8081/v1` → TypeScript still thinks `/one/auth/me` is legal; runtime 404.
- Point `VITE_API_URL` at One `http://localhost:8080/api/v1` (real One, once ports are honest) → TypeScript still thinks `/one/auth/login` exists; One 404s; password form is a product bug.
- Generate ops from One’s `api-type-ts` **and** keep money calls in the same `paths` object → money routes vanish from types (One has no checkouts) **or** someone unions the two OpenAPIs into one `paths` and the client forgets which host to call.

The honest frontend shape **after** S0/S1:

| Concern | Client | Base URL | Types |
|---------|--------|----------|-------|
| Login | Browser OIDC (Pay SPA `client_id`) | Zitadel authority (`:8085` in 011) + product login `:5175` | not `@repo/api-types-ts` |
| Identity, tenants, invites, authz | `@lazuar/one-client` (workspace package; `NP-XX-021` do not block on npm) | One API `/api/v1` | One `@repo/api-type-ts` / client’s `MeResponse` |
| Money, catalog, receipts, **Pay `whoami`** | new fetch client | Pay `http://localhost:8081` (paths already include `/v1`) | **future** `@repo/pay-types-ts` |
| Hub leftover (until Hub is off) | current `openapi-fetch` | Hub `8080/api/v1` | `@repo/api-types-ts` |

Until that split exists, **do not** grow `pay-spec` with Hub `/one/*` so ops can “just work” against 8081.

### 4.4 Portal and buyers

Portal checkout/layout calls `GET /one/auth/me` with the **merchant** cookie to paint “logged in” chrome on a **buyer** page. 011 is explicit: cardholders never become Zitadel users; buyer plane is Pay (magic link to payer mailbox). Connecting One does **not** mean portal should call One `GET /me` for buyers. Buyer session is a **Pay** problem (`NP-BUY-*`, `NP-XX-013`). If Pay later needs a buyer “whoami”, it is a **different** resource (payer profile / magic-link session), not `GET /v1/whoami` for merchants, and not One `/me`.

Mixing those three on one `/me` is how Hub got “cookie session on slug portal is a 404” (issue 022): FE treated Hub `/one/auth/me` success as enough to skip the magic-link form.

### 4.5 Admin (`lazuar-admin` `:5173`)

011: merchants never use `lazuar-admin`. `NP-XX-018`. Admin’s `GET /platform/auth/me` is Hub super-admin. One’s staff directory is `GET /platform/tenants` (Pay must not call it, `NP-XX-023`). Do not put either into `pay-spec`.

---

## 5. Connecting One does **not** mean Pay implements `/one/auth/login`

This is the operational version of §3–§4.

**What “connecting One” means (011 S0):**

1. Register Pay as a tenant OIDC SPA via One `POST /tenants/{id}/apps` (or seed like `lazuar-app`). Not a Console click.
2. User signs in at **`:5175`**. Pay receives an **access_token**.
3. Pay backend (and Pay browser client for One) send `Authorization: Bearer <access_token>` to **One**.
4. Pay reads `GET /me`, treats One tenant id as `org_id`, checks `authz/check` before merchant admin money routes.
5. Invites are One copy-link. Second engineer is a One member.

**What it does not mean:**

- Pay `MapPost("/one/auth/login")`
- Pay `MapGet("/one/auth/me")`
- Pay TypeSpec `interface OneOperations` copied from `packages/api-spec/modules/one/routes.tsp`
- Ops `client.POST("/one/auth/login", { body: { email, password } })` against `:8081`
- Keeping `lazuar_auth` as the Pay session cookie “because openapi-fetch already sends credentials”
- Dual cookie realms (`lazuar_auth` vs `lazuar_admin_auth`) on the Pay host
- Public register with `workspace_name` + `tenant_slug` + `accepted_terms` on Pay (`POST /one/public/register`)

Hub login is a **password form** that hits Hub Identity. One login is **OIDC + PKCE**. Those are incompatible UX and incompatible TypeSpec. If ops still shows email/password after S0, S0 has not happened (`NP-XX-007`, first-slice fail lock “No Pay password form”).

Logout: Hub `POST /one/auth/logout` clears `lazuar_auth`. One logout is ending the Zitadel session (and dropping the access_token Pay stored). Pay TypeSpec may later have `POST /v1/logout` **if** Pay stores a server-side session of its own; that is still not `/one/auth/logout`. Prefer not inventing Pay session state: Bearer in memory + One is enough for v1.

---

## 6. When to generate `pay-types-ts` (not now)

### 6.1 Current state

- No package `packages/pay-types-ts` / `@repo/pay-types-ts`.
- `packages/api-types-ts` `generate` script is hard-coded: `openapi-typescript ../api-spec/dist/openapi.yaml -o src/index.ts`.
- `task gen:types-ts` runs that filter. `task gen` sources are `packages/api-spec/**/*.tsp` only.
- `pnpm-workspace.yaml` includes `packages/*`, so `@repo/pay-spec` is already a workspace package (compile-only).
- No app `package.json` depends on `@repo/pay-spec`.
- Pay host tests do not read OpenAPI.

### 6.2 Do not generate now

Reasons:

1. **No consumer.** Generating types with a single `GET /v1/health` so someone can `client.GET("/v1/health")` is ceremony. `task pay:spec` already proves the compiler works.
2. **Wrong gravity.** If `@repo/pay-types-ts` exists, someone will import it **next to** `@repo/api-types-ts` in ops and union `paths`. That is the confusion in §4.
3. **CI cost.** Committing generated `index.ts` (ADR 005 does this for Hub) means `task gen` or a new `pay:gen` must stay honest. Not worth it before `/v1/checkouts` and a real TS caller.
4. **C# DTOs.** Pay host currently returns anonymous `{ status = "ok" }`. NSwag of `pay-spec` into `packages/pay-types-dotnet` is the Hub cathedral’s pipeline. Isolation tests exist to **not** drag that in. Hand-written Pay records at the edge are fine until a second consumer exists. ADR 006 (external vs internal contracts) was written for MediatR modules; new Pay should not rebuild that split as a religion, but it also should not NSwag a health check.

### 6.3 When it **is** time

Create `@repo/pay-types-ts` when **all** of these are true:

1. `packages/pay-spec` has at least one **product** route a TS app calls (`GET /v1/whoami` and/or `POST /v1/checkouts`, not only health).
2. That TS app’s `baseUrl` is the Pay host (`http://localhost:8081`, production Pay origin) — **not** Hub `/api/v1`, **not** One `/api/v1`.
3. The app does **not** import `@repo/api-types-ts` for those calls (Hub types stay Hub-only).
4. Compile is a **Pay** task (`task pay:types` or `pnpm --filter @repo/pay-types-ts generate`), **not** a new step inside `task gen`.

Optional C# generation: only if the Pay host wants compiler-checked DTOs shared with tests, **or** a .NET integrator exists. Not a prerequisite for `whoami`.

Do **not** generate Pay types from `packages/api-spec`. Do **not** add Pay paths to Hub OpenAPI “so ops types include them.” That recreates ADR 007’s ball of mud, now across processes.

---

## 7. Honesty: do not hook `pay-spec` into old `task gen` / honesty-allowlist

### 7.1 What the Hub honesty gate actually does

`scripts/check-openapi-minimal-honesty.mjs`:

- Reads **`packages/api-spec/dist/openapi.yaml`** (must run `task gen:spec` first).
- Reads **`packages/api-spec/honesty-allowlist.yaml`**.
- Scrapes `MapGet|Post|Put|Delete|Patch` under **`apps/lazuar-api/Modules`** and **`apps/lazuar-api/src/Lazuar.Api/Composition`**.
- Compares paths **relative to `/api/v1`**.
- Explicitly **out of scope:** host `/health`, `/health/ready`, `/health/metrics`, Swagger/Scalar static.

CI `contracts` job (`.github/workflows/ci.yml`):

1. `task gen --force`
2. `git diff --exit-code` on `packages/api-types-ts/src`, `packages/api-types-dotnet/...`, LHDN Kiota trees
3. `node scripts/check-openapi-minimal-honesty.mjs`

`task gen` sources/generates **only** Hub spec + Hub clients. `task pay:spec` is a **separate** Taskfile task (`dir: packages/pay-spec`).

`honesty-allowlist.yaml` header: “Product routes used by ops / portal / developers hub / SDKs must land in TypeSpec — not here.” That sentence is about **Hub**. If you add Pay `/v1/whoami` to this allowlist, you are classifying a Pay route as a Hub exception. That is a lie.

### 7.2 What would break if you naively hook Pay in

| Hook | Failure mode |
|------|----------------|
| Add `packages/pay-spec/**/*.tsp` to `task gen` sources | `gen:spec` still `dir: packages/api-spec`. Pay files never compile, or you concatenate two services into one OpenAPI with two `@server`s and mixed `/v1` vs `/api/v1`. |
| Point honesty script at `pay-spec/dist/openapi.yaml` **and** keep scraping `lazuar-api` | `GET /v1/health` is not a Hub Minimal map (Hub health is `/health` outside `/api/v1`). OpenAPI ⊈ Minimal. You add an `openapi_only_exceptions` row — a lie. |
| Point honesty scrape at `apps/lazuar-pay` **and** keep Hub OpenAPI | `GET /health` and `GET /v1/health` on Pay are not in Hub OpenAPI. Minimal ⊈ OpenAPI. You add `impl_only` rows for Pay on the **Hub** allowlist — a lie. |
| Union both OpenAPIs, scrape both hosts | Path `/me` vs `/one/auth/me` vs `/v1/whoami` all look “fine” in one set. The gate no longer tells you **which process** must implement which path. That is the original R25 purpose, destroyed. |
| Add `GET /one/auth/me` to `pay-spec` so a future Pay honesty pass greens when ops is pointed at 8081 | You have now **specified** that Pay implements Hub identity. The allowlist cannot save you; the spec **is** the product lie. |

### 7.3 What to do instead (later, not this paper)

When Pay has more than health:

1. Keep `task pay:spec` as the Pay compile.
2. Add `task pay:honesty` **later**, as a **new** script: OpenAPI from `packages/pay-spec/dist/openapi.yaml` vs `Map*` under `apps/lazuar-pay` only. Paths relative to **host origin**, not `/api/v1` (Pay’s sold door **is** `/v1/...` on 8081; do not strip a fake `/api/v1` prefix).
3. If process `/health` stays unspec’d, document it as impl-only **in a Pay allowlist**, not in `packages/api-spec/honesty-allowlist.yaml`.
4. CI: a **Pay** job (`pay:spec` + optional `pay:honesty` + `pay:test`). Do not overload the Hub `contracts` job.
5. Never require `task gen` to compile Pay in order for Hub PRs to merge.

Until then, the honest statement is: **Pay TypeSpec is not on the Hub honesty gate. That is correct.** Do not “fix” the `/health` vs `/v1/health` gap by joining the gates.

### 7.4 Do not grow Hub TypeSpec to describe Pay

Someone will suggest: “ops already has types; add `GET /v1/whoami` to `packages/api-spec` so `@repo/api-types-ts` includes it.” That puts a **8081** route in a document whose `@server` is **8080 `/api/v1`**. openapi-fetch would call `http://localhost:8080/api/v1/v1/whoami` or you’d special-case baseUrl. ADR 007 product-scoped docs (`docs-one.tsp`, `docs-payments.tsp`) were Hub audience splits, not a second process. **Refuse.**

---

## 8. Proposed TypeSpec for `GET /v1/whoami` (paper-only)

**Do not paste this into `packages/pay-spec/main.tsp` in the same change as this paper.** Land it with the host route. Shape below is the contract this analysis recommends.

### 8.1 Intent

- Namespace stays `LazuarPay`.
- Server stays `http://localhost:8081` (add production later when it exists; do not copy `https://hub.lazuar.com/api/v1`).
- Auth: HTTP Bearer. Same header One uses. Pay accepts One access_token and (when keys exist) a Pay-recognized key. TypeSpec does not need two schemes on day one; document in `@doc` that the bearer may be a user JWT or a key.
- Errors: RFC 7807. Copy a **small** `ProblemDetails` into `pay-spec` (do **not** `import` Hub `packages/api-spec/common/models.tsp` — that pulls `LazuarApi` into Pay). Do **not** import One’s common models as a TypeSpec project reference (packages are not published as a shared library; versions will drift). Duplicating the 6-field ProblemDetails is acceptable; it is a standard, not membership SoT.

### 8.2 Draft

```tsp
import "@typespec/http";
import "@typespec/openapi";

using Http;
using OpenAPI;

/** Focused Pay HTTP contract. Not packages/api-spec. Not lazuar-one. */
@service(#{ title: "Lazuar Pay" })
@info(#{ version: "0.1.0" })
@server("http://localhost:8081", "Local focused Pay host")
namespace LazuarPay;

// --- errors (local copy of RFC 7807; do not import Hub or One TypeSpec) ---

model ProblemDetails {
  type?: string;
  title?: string;
  status?: int32;
  detail?: string;
  instance?: string;
  request_id?: string;
}

@error
model ProblemDetailsResponse {
  @statusCode statusCode: 400 | 401 | 403 | 404 | 409 | 429 | 500;
  @body body: ProblemDetails;
}

// --- health (existing) ---

model HealthResponse {
  status: string;
}

@route("/v1")
@tag("Health")
interface Health {
  /** Process liveness for the focused Pay host. */
  @get
  @route("/health")
  check(): HealthResponse;
}

// --- session (Pay projection; One GET /me remains identity SoT) ---

enum PrincipalKind {
  user,
  api_key,
}

/**
 * Caller as this Pay process binds them.
 * Not LazuarOneApi.Platform.MeResponse.
 * Not LazuarApi.One.AuthUser.
 * Not Hub PaymentsMeResponseDto.
 *
 * user_id: One access_token `sub` for humans; key id for api_key.
 * org_id: One tenant id (Pay's org) when a membership/binding exists.
 * org_role / email / name: chrome hints. Never authorize from these alone.
 * scopes: Pay-owned catalog for this process, not One ROLE-03.
 */
model WhoamiResponse {
  user_id: string;
  principal: PrincipalKind;
  email?: string;
  name?: string;
  /** Omitted when the tenant hint is absent/invalid and no default binding exists. */
  org_id?: string;
  /** Hint only: owner | admin | member. Omit for keys unless Pay has a documented mapping. */
  org_role?: string;
  /** Always present as [] so clients lock to array. */
  scopes: string[];
}

@route("/v1")
@tag("Session")
interface Session {
  /**
   * Pay-facing introspect. 401 if bearer missing/invalid.
   * 403 if bearer is valid but not allowed to use Pay (no membership / revoked key).
   * Does not list tenants, invites, or platform-admin. Call One GET /me for that.
   * X-Lazuar-Tenant-Id is a hint; never authorize from the header alone.
   */
  @useAuth(BearerAuth)
  @get
  @route("/whoami")
  whoami(
    @header("X-Lazuar-Tenant-Id") tenantHint?: string,
  ): WhoamiResponse | ProblemDetailsResponse;
}
```

Emitted path: `GET /v1/whoami` (because `@route("/v1")` + `@route("/whoami")`), operationId `Session_whoami`, server `http://localhost:8081`.

### 8.3 Explicit non-fields

Do **not** add to `WhoamiResponse` without rewriting this paper:

- `tenants: TenantSummary[]`
- `permissions: string[]` (One ROLE-03)
- `is_platform_admin`
- `is_email_verified`
- `role` as Hub `CLIENT`/`SUPER_ADMIN`
- `active_tenant_id` as a second name for `org_id` (pick one: **`org_id`**)
- `workspace_id` (Hub noun; Pay + One say tenant/org)
- `has_active_gateway` / `gateway_names` (Hub K1 introspect; belongs on a Pay **account/config** route later, not on `whoami`)
- `security_stamp`

### 8.4 Status codes

| Code | When |
|------|------|
| 200 | Bearer accepted; body as above (`scopes` always `[]` at minimum) |
| 401 | Missing/invalid/expired bearer; unknown key; (if Pay ever copies stamp logic — prefer not; that is Hub) |
| 403 | Valid identity, not allowed to use this Pay org (not a member; viewer hitting an admin-only variant — prefer 403 on the **money** route, not on `whoami`) |
| 429 | If Pay rate-limits introspect (optional; One warns not to hammer `GET /me` because it can write; Pay `whoami` should be **read-only**) |

`whoami` must be **read-only**. JIT provision of Pay catalog rows on first seen tenant is `NP-ONE-019` (`tenant.created` webhook), not a side effect of chrome polling.

### 8.5 Header alias

Ops Hub client sends `X-Tenant-Id`. One and this draft send `X-Lazuar-Tenant-Id`. Pay should **not** accept `X-Tenant-Id` as a silent alias forever; if a compatibility alias exists, it is temporary and documented on the Pay host, not as a second `@header` in TypeSpec that freezes the Hub name into `/v1`.

---

## 9. Field-level identity JSON (so nobody “maps” them in TypeSpec)

### 9.1 Three bodies named “me”

**Hub human** `GET /api/v1/one/auth/me` → `One.AuthUser`:

| Field | Type | Source |
|-------|------|--------|
| `email` | string required | `GlobalUsers.Email` |
| `name` | string required | `GlobalUsers.Name` |
| `role` | string required | JWT `ClaimTypes.Role` or `SUPER_ADMIN`/`CLIENT` |
| `is_email_verified` | boolean required | `GlobalUsers.IsEmailVerified` |

No id. No orgs. Stamp mismatch → 401 + cookie delete.

**Hub machine** `GET /api/v1/integrations/payments/me` → `PaymentsMeResponseDto`:

| Field | Type |
|-------|------|
| `workspace_id` | string |
| `organization_id` | string |
| `is_test_mode` | boolean |
| `key_id` | string |
| `key_name` | string? |
| `scopes` | string[] |
| `has_active_gateway` | boolean? |
| `gateway_names` | string[]? |

**One human/machine** `GET /api/v1/me` → `Platform.MeResponse`:

| Field | Type |
|-------|------|
| `user_id` | string required |
| `email` | string optional |
| `name` | string optional |
| `tenants` | `TenantSummary[]` required (possibly empty) |
| `is_platform_admin` | boolean required |
| `active_tenant_id` | string optional |
| `active_role` | string optional |

**Proposed Pay** `GET /v1/whoami` → `WhoamiResponse` (§8): `user_id`, `principal`, optional chrome, optional `org_id`, `scopes[]`.

There is no lossless bijection. TypeSpec `model WhoamiResponse is MeResponse` (or Hub `AuthUser`) is a bug.

### 9.2 Hub entitlements vs One tenants vs Pay org

Ops chrome after login:

1. `AuthUser` (no workspace)
2. `GET /one/me/entitlements` → pick `ops_active_workspace_id` in `localStorage`
3. Every later request: `X-Tenant-Id: <that id>`

One chrome (one-react `WorkspaceSwitcher`, `UserButton`; one-client `me.get()`):

1. `MeResponse.tenants[]`
2. Hint `X-Lazuar-Tenant-Id`
3. `active_tenant_id` only if hint matches

Pay chrome (target):

1. One client still does (1)–(3) for **switcher / invite / team**
2. Pay `GET /v1/whoami` confirms Pay accepts the bearer **for money**
3. Pay money routes take `{org_id}` in the path (preferred) or a hint header, and **Pay** calls One `authz/check` (or uses a cache of a check)

Do not replace (1)–(3) with Pay `whoami` returning `tenants[]`. That is a second membership system (`NP-XX-014`).

---

## 10. Inventory: every old One TypeSpec surface vs owner

Complete list from `packages/api-spec/modules/one/routes.tsp` + related Hub “me” routes, with the owner for the **new** world.

| Old Hub path | New owner | Pay TypeSpec? |
|--------------|-----------|---------------|
| `POST /one/public/register` | One OIDC + `POST /tenants` | **No** |
| `GET /one/public/pricing` | Dead Hub commercial card (credits, `gmv_take_percent: 0`) | **No** (Pay pricing is catalog prices on Pay) |
| `POST /one/auth/login` | One OIDC `:5175` | **No** |
| `POST /one/auth/logout` | Zitadel end-session | **No** (optional later `POST /v1/logout` only if Pay stores a session) |
| `POST /one/auth/forgot-password` | One / Zitadel | **No** |
| `POST /one/auth/reset-password` | One / Zitadel | **No** |
| `POST /one/auth/verify-email` | One / Zitadel | **No** |
| `POST /one/auth/resend-verification` | One / Zitadel | **No** |
| `GET /one/auth/me` | One `GET /me` **and/or** Pay `GET /v1/whoami` (different JSON) | **Pay whoami only, not this path** |
| `PUT /one/me/profile` | One (profile claims / userinfo) | **No** |
| `PUT /one/me/security/password` | Zitadel | **No** |
| `GET /one/me/entitlements` | One `GET /me` / `GET /tenants` | **No** |
| `CRUD /one/workspaces` | One `/tenants` | **No** |
| `/one/workspaces/{id}/members` | One `/tenants/{id}/members` | **No** |
| `/one/workspaces/{id}/invites` | One `/tenants/{id}/invites` | **No** |
| `POST /one/workspaces/invites/accept` | One `POST /tenants/{id}/members/accept-invite` | **No** |
| `GET /one/workspaces/{id}/audit` | One audit **or** Pay-owned audit on Pay writes | Pay audit **Pay paths**, not this |
| `/one/workspaces/{id}/apps` (entitlement toggle) | Dead Hub “which Hub apps” | **No**; One `/tenants/{id}/apps` is OIDC |
| `/one/workspaces/{id}/webhooks` | One tenant webhooks **or** Pay money webhooks | Pay webhooks **Pay paths** (`/v1/...`), not `/one/...` |
| `POST /one/storage/presigned-url` | later Media / Pay files if needed | not v1 identity |
| `/one/api-keys` | One `/tenants/{id}/api-keys` | **No** (Pay may mint Pay keys under `/v1`) |
| `POST /one/integrations/workspaces/provision` | Hub Aura provisioner | **No** |
| `GET /public/one/{tenantSlug}/branding` | Pay hosted-page branding | **Maybe** as a **Pay public** route, new path |
| `GET /platform/auth/me` | Hub admin cookie; merchants never go here | **No** |
| `GET /integrations/payments/me` | Hub K1; new Pay machine introspect is `whoami` `principal=api_key` or a later `/v1/keys/me` | **Not this path** |

One-only routes that must not appear on Pay even as “new” names that clone the resource:

- `GET /me/invites`
- `POST /tenants/{id}/suspend|reactivate|retry-provision|transfer-ownership|leave|delete` (Pay may **react** to webhooks; it does not **expose** them)
- `POST /platform/tenants/**`
- `/tenants/{id}/sso-connections/**`, SCIM
- `/tenants/{id}/domains/**`, `/tenants/{id}/roles/**` (custom roles) — Pay does not become WorkOS
- `/tenants/{id}/authz/write` (does not exist; do not add) (`NP-XX-016`)

---

## 11. Frontend migration sketch (types only; not a build order)

This is to show **why** `pay-types-ts` is not now, and **why** ops cannot stay on `@repo/api-types-ts` forever if Pay is a different origin.

**Phase A — today (Hub).**  
ops/portal/admin: `@repo/api-types-ts` → 8080 Hub. Pay TypeSpec unused by FE. One TypeSpec unused by Pay FE.

**Phase B — S0 identity without moving money.**  
ops login replaced by OIDC. Team/invite/switcher use `@lazuar/one-client` → One. Money pages **still** call Hub `/admin/commerce/*` until focused Pay has catalog. Two clients in one SPA: One + Hub. **Still no `pay-types-ts`.**

**Phase C — S1 money on Pay.**  
Catalog/payments/receipts call Pay `:8081` `/v1/...`. Introduce `@repo/pay-types-ts` (or a thin hand-written client). `GET /v1/whoami` used as Pay session probe (in addition to One `GET /me`, not instead). Hub client shrinks. **Do not** keep `GET /one/auth/me` as a fallback “if Pay whoami 404s.”

**Phase D — Hub off.**  
Delete ops imports of `@repo/api-types-ts` **or** freeze the package as historical. Old `packages/api-spec` remains the contract of a dead host until the tree is removed. `task gen` / honesty-allowlist die with Hub, not get reused for Pay.

Connecting One **while skipping Phase B** and putting `/one/auth/login` on Pay is skipping to a fourth phase that 011 lists as **fail**.

---

## 12. Recommendations (binding for 012)

1. **Three contracts, three owners.** `packages/pay-spec` = Pay `:8081` `/v1`. One `packages/api-spec` = identity. Old `packages/api-spec` = Hub until Hub dies. No imports across those trees.

2. **Pay TypeSpec may grow `GET /v1/whoami`** as Pay JSON (§8), in the same PR as the host route, **not** in this paper’s commit, **not** as a copy of `MeResponse` / `AuthUser`.

3. **Pay TypeSpec must not grow tenant, invite, login, logout-as-Hub, entitlements, Hub provision, Hub `/one/api-keys`, SSO, SCIM, `POST /platform/tenants`.**

4. **Ops today is a Hub client.** `GET /one/auth/me` on 8080 is not evidence that Pay should implement it. Connecting One is OIDC + One `GET /me`.

5. **Do not generate `pay-types-ts` now.** First TS caller of Pay `:8081` product routes is the trigger. Separate package name. Separate task. Not `task gen`.

6. **Do not hook `pay-spec` into Hub honesty / allowlist / CI `contracts` job.** Later: `task pay:honesty` scraping `apps/lazuar-pay` only.

7. **Do not union OpenAPIs** into one `paths` type for a SPA that talks to two origins.

8. **Header:** Pay uses `X-Lazuar-Tenant-Id` (hint). Do not specify `X-Tenant-Id` on `/v1`.

9. **Buyers** do not use merchant `whoami` or One `/me`.

10. **Package names:** never reuse `@repo/api-spec` / `@repo/api-types-ts` for Pay. Pay stays `@repo/pay-spec` / future `@repo/pay-types-ts`.

---

## 13. Files read (absolute)

**Pay TypeSpec / host**

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/main.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/package.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/tspconfig.yaml`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/dist/openapi.yaml`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Properties/launchSettings.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/HealthTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`

**Old Hub TypeSpec / codegen / honesty**

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/main.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/docs-one.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/honesty-allowlist.yaml`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/common/models.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/one/routes.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/one/models.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/one/models/auth.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/one/models/workspace.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/one/models/api-keys.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/one/models/provision.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/one/models/pricing.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/payments/routes.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/payments/models.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-types-ts/package.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-types-ts/src/index.ts` (generated `paths` / `One.AuthUser`)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/scripts/check-openapi-minimal-honesty.mjs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/contracts/openapi-vs-minimal-api.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/005-typespec-api-contract-generation.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/006-separation-of-external-and-internal-contracts.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/007-product-scoped-api-references.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/Taskfile.yml`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/.github/workflows/ci.yml`

**Hub runtime / ops**

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/lib/api-client.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/App.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/components/LoginPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/AcceptInvitePage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/TeamPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/package.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-admin/src/lib/api-client.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/core/lib/server-client.ts`

**One TypeSpec / client**

- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/main.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/common/models.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/platform/routes.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/platform/models.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/tenants/routes.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/tenants/models.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/authz/routes.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/authz/models.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/apps/routes.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/api-keys/routes.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/webhooks/routes.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-spec/modules/enterprise/routes.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-type-ts/package.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/api-type-ts/src/index.ts` (`"/me"`, `Platform.MeResponse`)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/one-client/package.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/one-client/src/createClient.ts`

**011**

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/01-product.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/02-one-integration.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/03-first-slice.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/09-old-pay.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/11-checklist.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/12-first-slice-tracker.md`

---

## 14. Stop conditions

If a follow-up PR does any of the following, it has not read this paper:

- Adds `import` of `packages/api-spec/modules/one/**` into `packages/pay-spec`
- Adds `@route("/one")` or `@route("/tenants")` or `@route("/me")` to `LazuarPay`
- Copies `model AuthUser` or `model MeResponse` into `pay-spec` as the `whoami` body
- Points `lazuar-ops` `VITE_API_URL` at `8081` while still calling `/one/auth/login`
- Adds `packages/pay-spec` to `task gen` sources or honesty scrape roots
- Creates `@repo/pay-types-ts` with only `/v1/health` so turbo has another generate step
- Documents Pay as implementing `/one/auth/me` “for compatibility”

The sold door is `/v1`. Identity SoT is One. Hub TypeSpec is a third thing, still what ops compiles against, and not a template for Pay.
