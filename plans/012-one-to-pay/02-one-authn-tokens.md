# 02 — AuthN: One tokens, login host, secrets Pay must never hold

**Date:** 20 August 2026  
**Type:** Analysis only. **Do not implement product code from this file.**  
**Slice:** OIDC PKCE; `access_token` vs `id_token` as Bearer; ports 8080 / 5175 / 8085 / 3005 / 5173 / 5174; secrets Pay must never hold; what a C# `HttpClient` must send today for `GET /me`.  
**Parent program:** [`plans/012-one-to-pay`](./) — new Pay (`apps/lazuar-pay` on **8081**) as Consumer-0 of Lazuar One.  
**Binding sibling paper:** [`plans/011-new-lazuar-pay/02-one-integration.md`](../011-new-lazuar-pay/02-one-integration.md) (20 August 2026).  
**One first-party contract (source of the Pay checklist):** `/Users/akmalfirdaus/Code/lazuar/lazuar-one/plans/017-evals/08-dogfood-then-serve.md` §6.

---

## SHAs considered

Recorded at write time. Re-open files on a later SHA before treating a line as still true.

| Repo | Path | Branch | Full SHA | Short | Tip |
|------|------|--------|----------|-------|-----|
| **lazuar-pay** | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `feat/012-one-to-pay` | `6ca8f19f4b28c056f852b7b579b5b30428e48ad6` | `6ca8f19f` | `feat(pay): add TypeSpec package for the focused Pay host` (2026-08-20 21:00:06 +0800) |
| **lazuar-one** | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `main` | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` | `0f79fe4` | `WIP: Thu Aug 20 21:24:22 +08 2026` |

**Honesty lock (inherited, not re-proven here):** One staging proof is **NOT PASSED**. Packages `@lazuar/one-client` / `one-react` / `one-cli` are unpublished workspace packages. There is no public hosted SKU. Pay may import the workspace client later; this paper does not wait on npm. Source: `plans/011-new-lazuar-pay/02-one-integration.md` lines 5–6; One `plans/017-evals/08-dogfood-then-serve.md` header.

**New Pay host at this SHA:** `apps/lazuar-pay` is a focused ASP.NET process that listens on **http://localhost:8081**. It currently maps only `/health` and `/v1/health`. It does **not** yet call One, does **not** yet hold an OIDC `client_id`, and does **not** yet forward a Bearer token. Evidence: `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`, `Properties/launchSettings.json` (`applicationUrl: http://localhost:8081`), `apps/lazuar-pay/README.md`, `Taskfile.yml` `pay:dev`.

**Language note (do not relitigate here):** `plans/011-new-lazuar-pay/05-language.md` argued Go for a greenfield Pay. The focused host that exists is **C#**. This paper therefore answers “what a **C# `HttpClient`** must send today,” because that is the process on 8081.

---

## 0. What this paper is for

New Pay is a **separate origin** and a **separate process**. Users are One humans. Merchants are One tenants. Pay is not a second Zitadel project. Pay is not `Modules/One` copied out of the old monolith.

The AuthN question is smaller than “how do we log people in?” and larger than “put Bearer on HttpClient.” It is:

1. **Who is the login host?** Zitadel issuer `:8085` plus product login UI `:5175`. Never Pay’s own password form. Never stock Login V2 `:3005` as the shipped path. Never `lazuar-admin` `:5173` as a merchant destination.
2. **Which bytes are an API credential?** A JWT **access_token** or a `lzr_sk_…` secret. Not an `id_token`. Not a login-session cookie. Not a Zitadel PAT. Not an OpenFGA admin token. Not old Pay’s `lazuar_auth` cookie JWT.
3. **What does Pay’s backend send today** to prove Ada exists, so `GET http://localhost:8080/api/v1/me` returns 200?
4. **Which secrets must never land in `apps/lazuar-pay` configuration**, even “just for local”?

The One-side checklist that this paper implements as AuthN detail is already written:

- `plans/011-new-lazuar-pay/02-one-integration.md` — “What Pay must not implement (AuthN)”, “Secrets”, “HTTP Pay should use”.
- `plans/011-new-lazuar-pay/03-first-slice.md` step 2 — “Sign-in via `:5175`. `GET /me`.”
- `plans/011-new-lazuar-pay/11-checklist.md` `NP-ONE-001` … `NP-ONE-006`, `NP-ONE-020`.
- `plans/011-new-lazuar-pay/12-first-slice-tracker.md` step 2.

This paper does **not** register a Pay SPA. It does **not** add env files to `apps/lazuar-pay`. It does **not** copy `Modules/One`. It does **not** teach Pay to talk to Zitadel Management or OpenFGA.

**Backend-only first** is an explicit recommendation in §9: until Pay has a browser origin, do not create a Pay OIDC client. Dogfood `GET /me` from C# using a token Ada already obtained through **lazuar-app** (`:5174`) + **lazuar-login** (`:5175`), or using a `lzr_sk_` minted in that workspace.

---

## 1. Port map — what is listening where, and the 8080 collision

One documents ports at `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-docs/docs/reference/ports.md` (the README table at `lazuar-one/README.md` “## Ports” is the same numbers). Old Pay documents ports at `lazuar-pay/README.md` “### Standardized Port Mapping”. New Pay documents 8081 at `apps/lazuar-pay/README.md`.

The collision is not theoretical. Both products default to **host port 8080** for “the API.” They cannot both bind it on one laptop. New Pay’s 8081 exists **because** of that. Other collisions (3005, 8090, 5432) are equally real if someone boots **old Pay compose / mprocs** and **One compose** on the same machine.

### 1.1 One (identity plane) — local defaults

Evidence: `lazuar-one/apps/lazuar-docs/docs/reference/ports.md`, `lazuar-one/README.md` lines 163–176, `lazuar-one/docker-compose.yml`, `lazuar-one/.env.example`, `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Properties/launchSettings.json` (`applicationUrl: http://localhost:8080`).

| Service | Host port | URL | What it is | Who talks to it |
|---------|-----------|-----|------------|-----------------|
| **lazuar-api (One)** | **8080** | `http://localhost:8080` | ASP.NET resource server. Product routes under `/api/v1`. JWT + `lzr_sk_` + (separately) `lzr_scim_`. | Pay **backend** (this paper). lazuar-app, lazuar-admin, examples, login HRD. |
| **Zitadel API / issuer** | **8085** | `http://localhost:8085` | OIDC authority. Discovery at `/.well-known/openid-configuration`. Console at `/ui/console`. Container internal port is 8080; published as 8085. | SPA authorize; login BFF Session/OIDC v2; One API JWKS. **Not Pay.** |
| **lazuar-login** | **5175** | `http://localhost:5175` | Product universal login UI + Session BFF. Password form lives **here**. | Browser after Zitadel 302 `?authRequest=`. |
| **lazuar-login BFF loopback** | **5176** | proxied by Vite on 5175 | Express BFF. Holds login-client PAT. | Vite proxy only. |
| **Zitadel Login V2 (stock)** | **3005** | `http://localhost:3005` | Break-glass / rollback / passkey enroll / OTP SMS-email. Compose publishes `ZITADEL_LOGIN_PUBLISHED_PORT:-3005` → container 3000. | Operators rolling back. **Not merchants. Not Pay UI.** |
| **lazuar-app** | **5174** | `http://localhost:5174` | Customer product SPA. Authorization Code + PKCE. Redirect `http://localhost:5174/callback`. | Ada. Path A. Where Ada’s `access_token` is stored after login. |
| **lazuar-admin** | **5173** | `http://localhost:5173` | **Lazuar staff only.** Redirect `http://localhost:5173/callback`. | Operators. Merchants never. Pay never ships users here. |
| **examples/vite-spa** | **5177** | `http://localhost:5177` | Integrator starter. Same token picker as app. | Optional. |
| **lazuar-docs** | **5180** | `http://localhost:5180` | VitePress. | Engineers. |
| **lazuar-reference** | **5181** | `http://localhost:5181` | Scalar. | Engineers. |
| **Postgres (One compose)** | **5432** (or `POSTGRES_PUBLISHED_PORT`) | `localhost:5432` | One + Zitadel + OpenFGA databases. | One stack. |
| **OpenFGA HTTP** | **8090** | `http://localhost:8090` | Authz store. Compose maps host 8090 → container 8080. | **One API only** (`OpenFga:ApiUrl`). Pay never. |
| **OpenFGA gRPC** | **8091** | — | Maps host 8091 → container **8081**. | One. Host **8081** itself stays free (see §1.4). |
| **OpenFGA Playground** | **3009** | `http://localhost:3009/playground` | Local debug. | Operators. |

Compose **runtime** login URLs (`.env.example` + `docker-compose.yml`):

```text
ZITADEL_OIDC_DEFAULTLOGINURLV2=http://localhost:5175/login?authRequest=
ZITADEL_OIDC_DEFAULTLOGOUTURLV2=http://localhost:5175/logout?post_logout_redirect=
ZITADEL_LOGINV2_BASEURI=http://localhost:5175/
```

Zitadel **appends** the auth-request id after `authRequest=`. SPA OIDC env does **not** list `:5175` — the issuer (`:8085`) is the authority; Zitadel redirects to whatever Login V2 URL is configured. Evidence: `lazuar-one/apps/lazuar-app/.env.example` comments; `apps/lazuar-docs/docs/local/spa-oidc-setup.md` “Login cutover”; `apps/lazuar-login/README.md` architecture diagram.

Existing Zitadel volumes may **ignore** first-instance DEFAULT env. Cutover/rollback: `./scripts/login-dogfood-setup.sh --apply-root-cutover` / `--apply-root-rollback`, or Console instance Login V2 URLs. Recreate alone may not flip sticky volumes.

One API CORS Development defaults (`apps/lazuar-api/src/Lazuar.One.Api/appsettings.Development.json` `App:CorsOrigins`) include `5173`, `5174`, `5177`, `5180`, `5181` on both `localhost` and `127.0.0.1`. **Login `:5175` is not an API CORS origin** — the BFF is same-origin to the login UI. Staging/Production: empty `App:CorsOrigins` **fails boot**.

### 1.2 Old Pay (modular monolith) — same laptop, different product

Evidence: `lazuar-pay/README.md` lines 170–180, `Taskfile.yml` dual-run comments, `apps/lazuar-admin/package.json` `"dev": "vite --port=3005"`, `docker-compose.yml` `"5432:5432"` and `"3005:3000"`.

| Service | Host port | URL | What it is |
|---------|-----------|-----|------------|
| **Old `lazuar-api`** | **8080** | `http://localhost:8080` | Modular monolith. Cookie JWT + homemade `sk_*`. **Collides with One API.** |
| Dual-run next to Aura | **8090** | `http://localhost:8090` | Old Pay when Aura owns 8080. **Collides with One OpenFGA HTTP.** |
| `lazuar-developers` | 3002 | `http://localhost:3002` | Scalar hub. |
| `lazuar-ops` | 3003 | `http://localhost:3003` | Merchant console. Cookie `lazuar_auth`. |
| `lazuar-portal` | 3004 | `http://localhost:3004` | Checkout / buyer. |
| **Old `lazuar-admin`** | **3005** | `http://localhost:3005` | Platform admin. **Collides with Zitadel Login V2.** |
| Example cashier | 3020 | `http://localhost:3020` | Integrator sample. |
| Caddy gateway | 9080 | `http://localhost:9080` | Path router to old apps. |
| Old Postgres | **5432** | `localhost:5432` | **Collides with One Postgres.** |

Old Pay local demo accounts (do **not** use these against One):

| Role | App | URL | Email | Password |
|------|-----|-----|-------|----------|
| Superadmin | old admin | `:3005` | `admin@lazuar.com` | `Password123!` |
| Tenant admin | old ops | `:3003` | `founder@acme.test` | `Password123!` |

One’s Ada is a **different human** in a **different IdP**. Mixing these tables is a fail mode (§10).

### 1.3 New Pay (focused host)

Evidence: `apps/lazuar-pay/README.md`, `apps/lazuar-pay/src/Lazuar.Pay/Properties/launchSettings.json`, `Taskfile.yml` `pay:dev`.

| Service | Host port | URL | What it is |
|---------|-----------|-----|------------|
| **New `lazuar-pay`** | **8081** | `http://localhost:8081` | Focused money process. `task pay:dev`. Health at `/health` and `/v1/health`. **Not One. Not old Pay.** |

New Pay has **no Vite origin yet**. There is no Pay SPA port. There is no Pay callback URI. That is why §9 defers OIDC client registration.

### 1.4 Collision table (run both stacks on one machine)

| Host port | One | Old Pay | New Pay | If both bind |
|-----------|-----|---------|---------|--------------|
| **8080** | **One API** | **Old Pay API** | — | Second process fails. Pay’s C# `HttpClient` to One **must** target One, not old Pay. Confirm with `GET /health` body and `/api/v1/me` 401 shape. |
| **8081** | OpenFGA gRPC is **8091→container 8081** (host 8091 free) | — | **New Pay** | Host 8081 is intended for new Pay. Do not publish OpenFGA gRPC onto 8081. |
| **8085** | Zitadel issuer | — | — | Keep free. Pay never binds this. |
| **5173** | lazuar-admin (staff) | — | — | Not a merchant URL. |
| **5174** | lazuar-app (Ada) | — | — | Where Ada logs in via PKCE. |
| **5175** | lazuar-login | — | — | Password UI. Pay does not bind this. Pay does not deep-link it as homepage. |
| **5176** | login BFF loopback | — | — | Dev only. |
| **3005** | **Stock Login V2** | **Old Pay admin** | — | Shipping merchants to `:3005` is ambiguous **and** forbidden by `NP-ONE-005`. |
| **8090** | **OpenFGA HTTP** | **Old Pay dual-run** | — | One’s FGA client (`OpenFga:ApiUrl=http://localhost:8090`) will hit old Pay if dual-run won the bind. Pay must never talk to 8090. |
| **5432** | One Postgres | Old Pay Postgres | — | Compose fight. One’s `ConnectionStrings__Lazuar` and Zitadel DSN assume this. |

**Operational rule for this program:** when dogfooding One AuthN from new Pay, **do not start old Pay on 8080**. Start One API on 8080, new Pay on 8081, identity compose (Zitadel 8085, login UI 5175, Postgres, OpenFGA). Leave old `task fe` / old admin `:3005` down.

**How Pay’s C# process should address One today:**

```text
ONE_API_URL=http://localhost:8080/api/v1
```

Not `http://localhost:8081` (that is Pay itself). Not `http://localhost:8085` (that is Zitadel, not `/me`). Not `http://localhost:8090` (OpenFGA). Not `http://localhost:8090/api/v1` (old Pay dual-run leftover).

### 1.5 Login host vs authority vs API (three different hosts)

This is the single most common mix-up.

```text
Browser  --OIDC authorize-->  Zitadel :8085
                                |
                                | 302  /login?authRequest=V2_…
                                v
                       lazuar-login :5175   (password / MFA / register)
                                |
                     Session API + OIDC finalize (login-client PAT, server-only)
                                |
                                v
                       SPA /callback?code&state   (lazuar-app :5174 today)
                                |
                     PKCE token exchange at :8085
                                |
                     access_token (JWT) + id_token + refresh_token
                                |
                     SPA sends access_token --> One API :8080  GET /api/v1/me
```

Evidence: `lazuar-one/apps/lazuar-login/README.md` “Architecture”; `apps/lazuar-app/src/auth/oidcConfig.ts`; `apps/lazuar-app/src/pages/LoginPage.tsx` (“No password form on this app.”).

| Host | Role | Pay does |
|------|------|----------|
| `:8085` | OIDC **authority** / token issuer / JWKS | Later: SPA `authority`. Never: Management PAT calls. |
| `:5175` | **Password UI** + BFF | Users land here because **Zitadel** redirected. Pay does not treat this as homepage (`NP-ONE-005` note). |
| `:8080` | **Resource server** `/api/v1` | C# `HttpClient` `GET /me` with Bearer. |
| `:8081` | Pay money API | Own health. Later: merchant ops after One identity is proven. |
| `:3005` | Stock Login V2 | Break-glass on One. Collision with old Pay admin. Never ship. |
| `:5173` | Staff console | Never ship merchants. |

SPA env **does not contain the login host**. `VITE_ZITADEL_AUTHORITY=http://localhost:8085`. Switching product login from `:3005` to `:5175` does not change client ids or redirect URIs. Evidence: `apps/lazuar-app/.env.example`; spa-oidc-setup “Login cutover.”

---

## 2. Token types — what is a credential, what is not

One’s resource server is a **Smart** authentication scheme. Detection order is prefix on `Authorization: Bearer …`:

1. `lzr_scim_` → SCIM token scheme  
2. `lzr_sk_` → API key scheme  
3. else → JWT Bearer against Zitadel authority  

Evidence: `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Auth/AuthenticationExtensions.cs` lines 10–47 (`ForwardDefaultSelector`).

Pay should only ever send **(a)** a user JWT access token or **(b)** a tenant API key. Pay should never send a SCIM token (issue 050: SCIM tokens historically could call `/me` — not Pay’s credential).

### 2.1 User `access_token` (the human Bearer)

| Property | Value | Evidence |
|----------|-------|----------|
| How minted | OIDC Authorization Code + **PKCE** against Zitadel. Public SPA: `authMethodType: OIDC_AUTH_METHOD_TYPE_NONE`, `responseTypes: OIDC_RESPONSE_TYPE_CODE`, `grantTypes: authorization_code + refresh_token`. | `scripts/seed-platform-spa-clients.sh` `create_spa`; `apps/lazuar-app/src/auth/oidcConfig.ts` (`response_type: 'code'`, PKCE via `oidc-client-ts` / `react-oidc-context`) |
| Shape One accepts | Compact **JWS** JWT (`header.payload.sig`, three segments). Opaque / JWE → JwtBearer fails → **401**. | `apps/lazuar-docs/docs/local/api.md`; issue 001 |
| Access token type in Zitadel | **`OIDC_TOKEN_TYPE_JWT`**. Seed and `POST /tenants/{id}/apps` both send JWT. Leftover Console/old apps may still mint opaque. | issue 001; `docs/integrations/oidc-apps.md` “Tokens One will accept” / “Existing apps” |
| Required claim for API use | **`jti`**. Zitadel **access** JWTs have it. Zitadel **ID tokens do not**. | `JwtAccessTokenGuard.cs`; issue 002 acceptance; `JwtBearerMeTests.Id_token_shape_returns_401` |
| `token_use` | Zitadel **does not emit** `token_use=access_token`. Guard **rejects** `token_use=id_token` **if present**. Never require `token_use=access_token`. | `JwtAccessTokenGuard` comments; issue 002 resolution |
| Issuer | Must match API `Zitadel:Authority` (local `http://localhost:8085`). Container-internal `http://zitadel-api:8080` is for other containers, not the host API. | `docs/local/api.md` “Issuer pitfall” |
| Audience (Development) | May be empty; `ValidateAudience=false` (D05). Any JWT from the issuer with `jti` works in Dev. Residual: issue 076. | `appsettings.Development.json`; `ZitadelJwtBearerDefaults` |
| Audience (Staging/Production) | `RequireAudience=true`; empty audience **fails boot**. Wrong `aud` → 401. Scope to request: `urn:zitadel:iam:org:project:id:{Zitadel__Audience}:aud`. | issue 003; `docs/local/api.md`; recipe R1 errors table |
| Clock skew | **60 seconds** (JwtBearer only). | `ZitadelJwtBearerDefaults.ClockSkew` |
| `azp` | Optional allowlist `Zitadel:AuthorizedParties`. Empty = no azp pin. | `JwtAccessTokenGuard`; issue 076 |
| What it identifies | `sub` → `user_id` (Zitadel human). Email/name from access-token claims, **not** from `id_token`, **not** from userinfo. | `MeEndpoints.GetMe`; `TenantAccessService.GetEmail` / `GetDisplayName`; plan 018-design/07-home-and-account.md (access token often omits profile claims) |
| Scopes first-party SPA requests | `openid profile email offline_access` | `apps/lazuar-app/.env.example` `VITE_ZITADEL_SCOPE` |
| Where stored in Path A | `sessionStorage` via `WebStorageStateStore` (`oidcConfig.ts`). Key shape from `oidc-client-ts`: `oidc.user:{authority}:{client_id}`. XSS-sensitive; documented as local-DX convenient. | `apps/lazuar-app/src/auth/oidcConfig.ts`; `apps/lazuar-app/README.md` “Silent renew / storage” |

**This is what Pay’s C# client sends as Bearer for a human.**

### 2.2 `id_token` — forbidden as Bearer (M2M-14)

**Tracker ID:** One `M2M-14` — “Reject ID token as API bearer.” Status **Y** on HEAD considered. Do not reopen as 017 work.

**Issue:** `lazuar-one/issues/002-spas-send-id-token-as-bearer.md` (Status: Done). Related: 001 (opaque access tokens), 003 (audience).

**What went wrong historically:** first-party SPAs preferred a JWT-looking access token and **otherwise sent the OIDC `id_token`**. Combined with opaque tenant access tokens (001) and audience-off (003), the API accepted an ID token as an API credential.

**What is true now (One side):**

1. **SPA pickers never send `id_token`.** `pickApiBearerToken` returns only a JWT-like `access_token` or `undefined`. Opaque / JWE / empty → omit `Authorization` → honest 401. Tests lock app + admin together (`apps/lazuar-app/src/auth/bearerToken.test.ts` imports both pickers). Copied into `examples/vite-spa/src/bearerToken.ts`.  
   Evidence: `apps/lazuar-app/src/auth/bearerToken.ts`:

   ```ts
   export function pickApiBearerToken(user: User | null | undefined): string | undefined {
     if (!user) return undefined
     if (isJwtLike(user.access_token)) return user.access_token
     return undefined
   }
   ```

   Wired in `apps/lazuar-app/src/App.tsx` `ApiClientBridge`: `getAccessToken: () => pickApiBearerToken(auth.user)`.

2. **API rejects ID-token shape.** `JwtAccessTokenGuard.RejectReason` requires `jti` (“JWT access tokens must include a jti claim (ID tokens are not API credentials).”) and rejects `token_use=id_token`. `OnTokenValidated` fails the ticket when reason is non-null.  
   Evidence: `JwtAccessTokenGuard.cs`; `AuthenticationExtensions.cs` `OnTokenValidated`; tests `JwtBearerMeTests.Id_token_shape_returns_401`, `JwtBearerValidationTests.Id_token_shape_missing_jti_is_rejected`.

3. **Docs / recipes say it in the error table.** R1: “`401` while **your** R3 SPA shows user → You sent `id_token`…”. R3: “In DevTools, copy `access_token` (not `id_token`).” `examples/oidc-spa-notes/README.md`: “Send the **access** token — never the `id_token`.” One 08-dogfood §6.2: “Send **access_token** as `Authorization: Bearer` | Send `id_token` (M2M-14).”

**What Pay must not do:**

- Do not “if access looks opaque, send `id_token`.” That is the closed 002 bug.
- Do not send `id_token` so `/me` has email. One `plans/018-design/07-home-and-account.md` explicitly refuses this. Access tokens often omit profile; that is the steady state of a correct picker, not a reason to cheat.
- Do not parse `id_token` in Pay to invent membership. Role SoT is `GET /me` + `authz/check`, not Zitadel `urn:zitadel:iam:org:project:roles` (`NP-ONE-008`).

If Pay later has a browser origin, **copy `pickApiBearerToken`**, do not invent a fourth policy, and do not pass `user.id_token` into `@lazuar/one-client` `getAccessToken`. The TS client “sends whatever `getAccessToken` returns” (`packages/one-client/src/createClient.ts`; `plans/016-bugs/06-frontend-spa-ui-bugs.md`). The guard lives in the picker and in One’s JWT handler, not in the package.

### 2.3 `lzr_sk_` — machine Bearer

| Property | Value | Evidence |
|----------|-------|----------|
| Prefix | `lzr_sk_` (`ApiKeyDefaults.KeyPrefix`) | `Infrastructure/Auth/ApiKeyDefaults.cs` |
| Format | `lzr_sk_` + base64url(32 random bytes) | `ApiKeyService.GenerateSecret` |
| How minted | `POST /api/v1/tenants/{tenantId}/api-keys` with a **user JWT** (owner/admin). Secret returned **once**. | Recipe R2 `docs/recipes/service-api-key.md`; TypeSpec apps |
| How presented | `Authorization: Bearer lzr_sk_…` — **same header** as the user JWT | `examples/node-api-key/index.mjs`; R2 curl |
| How verified | HMAC-SHA256 of secret with `ApiKeys:Pepper`; lookup by hash; fixed-time verify; reject revoked / expired | `ApiKeyAuthenticationHandler.cs`; `ApiKeyHasher.cs` |
| Principal | `sub` = **key GUID**, not a Zitadel user. `tenant_id` claim = bound workspace. `auth_type=api_key`. Scopes as `scope` claims. | Handler claims list; `MeEndpoints.GetMeForApiKey`; TypeSpec `MeResponse` comments |
| `GET /me` as key | `user_id` = key id; `tenants` = bound workspace 0–1; `active_tenant_id` = that tenant; `is_platform_admin` **never true**; role is scope-derived (`admin` if admin-equivalent scopes, else `member`); keys are never `owner` | `MeEndpoints.GetMeForApiKey`; `models.tsp` `MeResponse` / `TenantSummary` |
| Sample | `lazuar-one/examples/node-api-key/` | README + `index.mjs` |

Pay workers / crons / Pay API → One should use a **scoped** `lzr_sk_`. Empty/`*` scopes are a footgun (`02-one-integration.md` “Machines and apps”). Prefer explicit `tenant:read` plus the routes Pay actually hits. `authz/check` with a key requires scope `authz:check` and a real member `user_id` (not the key id) — R2.

**Pay holds the secret once** (NP-ONE-020). Pay does not hold the pepper. Pepper is One’s (`ApiKeys:Pepper`; Development default `local-dev-api-key-pepper-change-me` in `appsettings.Development.json`).

### 2.4 Things that look like tokens but are not One API credentials

| Artifact | Where | Why Pay must not send it to `/me` |
|----------|-------|-----------------------------------|
| OIDC **`id_token`** | SPA user object | M2M-14 / issue 002. No `jti`. Not an API credential. |
| Opaque access token | Leftover / Console app | One does not introspect. 401. Do not fall back to `id_token`. |
| Login cookie `lazuar_login_sess` | `:5175` BFF | AES-256-GCM session for the login UI (prod). Not a One API Bearer. PAT is server-side. |
| CSRF cookie `lazuar_login_csrf` | `:5175` | CSRF for BFF POSTs. |
| Cookie `lazuar_active_tenant` | lazuar-app | UX hint. One reads **header** `X-Lazuar-Tenant-Id`, not this cookie from Pay’s server. |
| Old Pay cookie **`lazuar_auth`** | old ops/portal | Homemade HS256 JWT, issuer `lazuar-api`, audience `lazuar-clients`. One will not validate it. See §7. |
| Old Pay cookie **`lazuar_admin_auth`** | old `/api/v1/platform` | Same homemade scheme, different cookie name. |
| Old Pay `sk_live_` / `sk_test_` | old Integrations | Different prefix, different table. One keys are `lzr_sk_` only. |
| **`ZITADEL_PAT`** / `Zitadel:ServiceUserToken` | One seed / provisioner | Management API. If Pay holds this, Pay **is** Zitadel ops. Forbidden. |
| **login-client PAT** | `apps/lazuar-login/.secrets/login-client.pat` | `IAM_LOGIN_CLIENT`. Session + OIDC finalize. Never `VITE_*`. Never Pay. |
| **OpenFGA `ApiToken`** | One `OpenFga:ApiToken` | Preshared Bearer to FGA HTTP. Pay never calls FGA. |
| **SCIM `lzr_scim_`** | One enterprise | Wrong scheme. Not a merchant credential. |
| Zitadel project role claims | JWT `urn:zitadel:iam:org:project:roles` | Not SoT. `NP-ONE-008`. |

### 2.5 `X-Lazuar-Tenant-Id` is not AuthN

It is a **hint**. `ActiveTenantHint.HeaderName = "X-Lazuar-Tenant-Id"`. `GET /me` echoes `active_tenant_id` + `active_role` only when the hint matches an active membership (JWT) or the key’s bound tenant. A bad hint is omitted; response still 200. Path `{tenantId}` + membership is authorization SoT. Never authorize by header alone.

Evidence: `Infrastructure/Tenancy/ActiveTenantHint.cs`; `MeEndpoints.GetMe`; `packages/one-client/src/createClient.ts`; recipe R1.

Pay may send the hint. Pay must not treat a 200 `/me` with a hint as “this user is admin of that tenant.” Call `POST /tenants/{id}/authz/check` (later slice). This paper only needs `/me` for identity.

---

## 3. How Ada gets a token locally

Ada is the seeded **customer** human, not the Zitadel instance admin.

Evidence: `lazuar-one/scripts/seed-dev-demo.sh`, `scripts/seed-dev-demo.py`, `lazuar-one/README.md` “First-time local”:

| Field | Default |
|-------|---------|
| Email / login name | `ada@acme.test` (`DEMO_CUSTOMER_EMAIL`) |
| Password | `Password1!` (`DEMO_CUSTOMER_PASSWORD`) |
| Given / family | Ada Lovelace |
| Staff (not Ada) | `zitadel-admin@zitadel.localhost` / `Password1!` → **`:5173` only** |

`seed-dev-demo.sh` is **local Development only** (exits if `ASPNETCORE_ENVIRONMENT` is Production/Staging or `NODE_ENV=production`). It uses the **login-client PAT file** (`apps/lazuar-login/.secrets/login-client.pat`) to create the human, then optionally runs `seed-platform-spa-clients.sh` which requires **`ZITADEL_PAT`** (Management) — a **different** token. Pay holds neither.

### 3.1 Stack Ada needs (Pay does not replace this)

From One README + `docs/quickstart/index.md` + `docs/local/bootstrap-platform.md`:

```bash
# in lazuar-one
cp .env.example .env
./scripts/bootstrap-local.sh          # compose wait, FGA, login PAT, demo users + SPA clients
pnpm install
pnpm login:dev                        # :5175
pnpm api:dev                          # :8080  — this is One, not Pay
pnpm app:dev                          # :5174
```

Confirm:

```bash
curl -sf http://localhost:8080/health          # One liveness {"status":"ok"}
curl -sf http://localhost:5175/health          # login UI
curl -sf http://localhost:8085/.well-known/openid-configuration | jq '.issuer, .jwks_uri'
```

Issuer must be `http://localhost:8085` for host-run One API.

If `ZITADEL_PAT` was unset, seed skipped SPA clients. Then Ada cannot finish PKCE until `WRITE_ENV=1 ./scripts/seed-platform-spa-clients.sh` (or break-glass Console — **not** Pay’s job).

### 3.2 What Ada actually types (password form is `:5175`, not Pay, not `:5174`)

1. Open **http://localhost:5174** (lazuar-app).
2. Click **Sign in**. `LoginPage` calls `auth.signinRedirect()` — **no password fields** on this app (`apps/lazuar-app/src/pages/LoginPage.tsx`: “New accounts register on the Lazuar sign-in screen. No password form on this app.”).
3. Browser hits Zitadel `:8085` authorize (client_id = seeded `lazuar-app`, redirect `http://localhost:5174/callback`, PKCE, `response_type=code`, scopes `openid profile email offline_access`).
4. Zitadel 302 → **http://localhost:5175/login?authRequest=…** (product default). If this lands on `:3005/ui/v2/login/…`, cutover did not stick — still not Pay’s form.
5. On `:5175`, Ada enters `ada@acme.test` then `Password1!` (HRD may split identifier/password steps). Login BFF uses **login-client PAT** against Zitadel Session API v2, then `POST /v2/oidc/auth_requests/{id}` finalize → `callbackUrl` (must be on `REDIRECT_ALLOWLIST`).
6. Browser returns to **http://localhost:5174/callback?code&state**. `react-oidc-context` exchanges the code at the issuer. Tokens land in **sessionStorage**. History is replaced (`onSigninCallback`).
7. App calls One `GET /api/v1/me` with `Authorization: Bearer <access_token>` via `pickApiBearerToken`. Empty `tenants: []` is valid until Ada creates/joins a workspace.

`REDIRECT_ALLOWLIST` on login (`.env.example`) today:

```text
http://localhost:5173,http://localhost:5174,http://localhost:5177,http://localhost:8085,http://localhost:5175
```

There is **no Pay origin** on that list. Adding one is a later SPA step (§9), together with One `POST /tenants/{id}/apps` redirects — **not** Console-only (`NP-ONE-004`).

### 3.3 How an engineer copies Ada’s access token for C# (backend-only first)

Preferred for this slice: **do not build Pay login**. Steal the access token Ada already has.

1. Chrome/Firefox DevTools on `http://localhost:5174` after sign-in.
2. Application → Session Storage → origin `http://localhost:5174` → key `oidc.user:http://localhost:8085:<lazuar-app-client-id>` (authority and client_id from env).
3. JSON field **`access_token`**. Confirm three segments. **Do not copy `id_token`.**
4. Alternatively: Network tab → request to `http://localhost:8080/api/v1/me` → request header `Authorization: Bearer eyJ…`.
5. Export for the Pay process (never commit):

```bash
export ONE_API_URL=http://localhost:8080/api/v1
export ONE_ACCESS_TOKEN='eyJ…'    # Ada's JWT access_token only
```

Recipe R1 says the same: `export ACCESS_TOKEN='…'   # from browser Network tab or SPA storage — JWT only`.

**Machine path (no Ada session):** in lazuar-app, create a workspace, Settings → API keys (or `POST /tenants/{id}/api-keys` with Ada’s JWT), copy `lzr_sk_…` once:

```bash
export ONE_API_URL=http://localhost:8080/api/v1
export ONE_API_KEY='lzr_sk_…'
```

Runnable One sample: `lazuar-one/examples/node-api-key/` (`LAZUAR_API_BASE`, `LAZUAR_API_KEY`). Pay’s C# equivalent is §5.

### 3.4 What Ada must not use

| Temptation | Why not |
|------------|---------|
| `zitadel-admin@zitadel.localhost` in Pay | Staff. Admin SPA `:5173`. `is_platform_admin` may become true if email is on `Platform:AdminEmails`. Not a merchant. |
| `admin@lazuar.com` / `Password123!` | Old Pay seed. Different database, different cookie. |
| Password grant / ROPC against `:8085` | Login README: “Redirect-only OIDC … Is not ROPC / password grant to lazuar-api.” Pay must not collect Ada’s password. |
| Typing Ada’s password into Pay | Rebuilds old Pay `POST /one/auth/login`. Forbidden §7. |
| Opening `:3005` as “login” | Break-glass Login V2 **and** old Pay admin. `NP-ONE-005`. |
| Opening `:5175` as Pay homepage | Login is a redirect target, not a product shell. |
| `Zitadel:UseStub=true` stub `client_id` for a new Pay SPA | R3: stub client cannot complete real login. Development One defaults `UseStub: true` (`appsettings.Development.json`). Backend-only `/me` with Ada’s **real** app token or a real `lzr_sk_` does not need Pay to create an app yet. |

---

## 4. OIDC PKCE (what Pay will do later, what it must not do now)

First-party pattern Pay will copy when it has a browser origin:

| Item | Value | Evidence |
|------|-------|----------|
| Grant | Authorization Code + PKCE | `oidcConfig.ts` `response_type: 'code'`; seed `OIDC_AUTH_METHOD_TYPE_NONE` |
| Client type | Public SPA (`OIDC_APP_TYPE_USER_AGENT`) **or** confidential `web` if Pay chooses a BFF | `oidc-apps.md` confidential vs public |
| Secret in SPA | **None** | Seed `authMethodType: NONE`; app README |
| Authority | `http://localhost:8085` | `.env.example` |
| Redirect | Pay origin `/callback` — **does not exist yet** | app uses `http://localhost:5174/callback` |
| Post-logout | Pay origin `/` | |
| Scopes | `openid profile email offline_access` plus audience reserved scope in strict envs | |
| Access token type | JWT (API create / seed already set this; not a Console happy-path toggle) | issue 001 closed |
| Login UI | Zitadel DEFAULTLOGINURLV2 → `:5175/login?authRequest=` | compose / `.env.example` |
| Allowlist | login `REDIRECT_ALLOWLIST` **and** the One app’s `redirect_uris` | `NP-ONE-004` |

Pay **now** (no origin): skip authorize, skip PKCE, skip client_id. Forward a token obtained on Path A, or a `lzr_sk_`.

Pay **must not** implement:

- Resource Owner Password Credentials against Zitadel.
- A Pay-owned Session API client using login-client PAT.
- Registering Pay by clicking Zitadel Console as the day-2 path (`02-one-integration.md`: “Not a second Zitadel project the Pay team maintains in Console”).
- Shipping `:3005` or `:5173`.

When Pay does register, it is `POST /api/v1/tenants/{tenantId}/apps` with Ada’s user JWT (owner/admin), `type: "spa"` or `"web"`, Pay redirect URIs. Recipe R3. First-party seed (`seed-platform-spa-clients.sh`) is **lazuar-app + lazuar-admin only** — do not silently add `lazuar-pay` to that script in this slice; that is a later both-sides change and still needs a browser origin.

Development `Zitadel:UseStub=true` returns a stub `client_id` that cannot log in (R3 common errors). Real app create needs `Zitadel:UseStub=false` and provisioner PAT **on One**, not on Pay.

---

## 5. Exact `Authorization` header Pay should forward — C# `HttpClient` today for `GET /me`

### 5.1 Wire contract

| Piece | Value |
|-------|--------|
| Method | `GET` |
| URL | `http://localhost:8080/api/v1/me` |
| Required header | `Authorization: Bearer <token>` |
| `<token>` | Either compact JWT **access_token** **or** `lzr_sk_…` |
| Recommended header | `Accept: application/json` |
| Optional hint | `X-Lazuar-Tenant-Id: <tenant-guid>` |
| Optional correlation | `X-Request-Id` (One echoes; ProblemDetails may include `request_id`) |
| Must **not** send | `id_token`; login cookies; old `lazuar_auth`; `ZITADEL_PAT`; OpenFGA token; `lzr_scim_` |
| AuthN scheme on One | TypeSpec `@useAuth(BearerAuth)` on `MeOperations.getMe` (`packages/api-spec/modules/platform/routes.tsp`) |
| Unauthenticated | **401** ProblemDetails (`JwtBearerMeTests.Anonymous_returns_401`; challenge writer in `AuthenticationExtensions`) |
| Success | **200** `MeResponse` |

Anonymous curl (One docs):

```bash
curl -i http://localhost:8080/api/v1/me
# 401 ProblemDetails
```

Authenticated curl (R1 / node-api-key README):

```bash
curl -sS "$ONE_API_URL/me" \
  -H "Authorization: Bearer $ONE_ACCESS_TOKEN" \
  -H "Accept: application/json"
```

or

```bash
curl -sS "$ONE_API_URL/me" \
  -H "Authorization: Bearer $ONE_API_KEY" \
  -H "Accept: application/json"
```

One’s own JwtBearer test does exactly this in C#:

```csharp
client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {jwt}");
return await client.GetAsync("/api/v1/me");
```

Evidence: `lazuar-one/apps/lazuar-api/tests/Lazuar.One.Api.Tests/Integration/JwtBearerMeTests.cs` private `GetMe`.

### 5.2 What a Pay `HttpClient` must send (copy-paste shape, not an implementation order)

This is the **on-the-wire** requirement. Do not add it to `Program.cs` from this paper.

```csharp
using System.Net.Http.Headers;

var oneApiBase = Environment.GetEnvironmentVariable("ONE_API_URL")
    ?? "http://localhost:8080/api/v1";
var token = Environment.GetEnvironmentVariable("ONE_ACCESS_TOKEN")
    ?? Environment.GetEnvironmentVariable("ONE_API_KEY")
    ?? throw new InvalidOperationException(
        "Set ONE_ACCESS_TOKEN (JWT access_token) or ONE_API_KEY (lzr_sk_). Never an id_token.");

// Guard in Pay before the wire: three-segment JWT, or lzr_sk_ prefix.
// Do not "helpfully" send id_token if access is opaque.
if (token.StartsWith("lzr_sk_", StringComparison.Ordinal))
{
    // machine path
}
else if (token.Count(c => c == '.') != 2)
{
    throw new InvalidOperationException(
        "ONE_ACCESS_TOKEN is not a compact JWT. Copy access_token, not id_token, not opaque.");
}

using var http = new HttpClient { BaseAddress = new Uri(oneApiBase.TrimEnd('/') + "/") };
http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

// Optional hint — never authorization:
// http.DefaultRequestHeaders.TryAddWithoutValidation("X-Lazuar-Tenant-Id", tenantId);

using var response = await http.GetAsync("me"); // BaseAddress already includes /api/v1/
var body = await response.Content.ReadAsStringAsync();
```

**Header line, literally:**

```http
GET /api/v1/me HTTP/1.1
Host: localhost:8080
Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.…
Accept: application/json
```

or

```http
GET /api/v1/me HTTP/1.1
Host: localhost:8080
Authorization: Bearer lzr_sk_…
Accept: application/json
```

`AuthenticationHeaderValue("Bearer", token)` serializes to `Authorization: Bearer {token}` with a single space. Do not double-prefix (`Bearer Bearer …`). Do not use `Basic`. Do not put the token in a query string (One issue 081 is about **login** session tokens on query strings — still a pattern Pay must not copy).

Prefer `IHttpClientFactory` named client (`One`) once this is product code: `BaseAddress = http://localhost:8080/api/v1/`, timeout bounded, **never** log the Authorization header (One’s JwtBearer `OnAuthenticationFailed` logs failure type only — “never Authorization header / raw token”).

`@lazuar/one-client` is the TS equivalent (`createClient({ baseUrl, getAccessToken })` → `Authorization: Bearer ${token}`). C# has no published One SDK. Typed DTOs exist in One as `Lazuar.One.ApiTypes` (`packages/api-type-dotnet`) — Pay may generate from One OpenAPI later; do not project-reference `apps/lazuar-api` (old Pay) or One’s host project.

### 5.3 `MeResponse` Pay should expect (200)

TypeSpec: `lazuar-one/packages/api-spec/modules/platform/models.tsp` `MeResponse` / `TenantSummary`. Handler: `MeEndpoints.GetMe`.

**User JWT:**

```json
{
  "user_id": "<zitadel-sub>",
  "email": "ada@acme.test",
  "name": "Ada Lovelace",
  "is_platform_admin": false,
  "tenants": [
    {
      "id": "<guid>",
      "slug": "acme",
      "name": "Acme",
      "role": "owner",
      "status": "active",
      "permissions": ["…"]
    }
  ],
  "active_tenant_id": "<guid-if-hint-matched>",
  "active_role": "owner"
}
```

Empty `tenants: []` is valid before create/join (R1 Success). Email/name may be **null** if the access token omitted profile claims — **do not “fix” that by sending `id_token`.**

**API key:**

```json
{
  "user_id": "<api-key-guid>",
  "email": null,
  "name": null,
  "tenants": [
    {
      "id": "<bound-tenant-id>",
      "slug": "…",
      "name": "…",
      "role": "member",
      "status": "active",
      "permissions": []
    }
  ],
  "is_platform_admin": false,
  "active_tenant_id": "<bound-tenant-id>",
  "active_role": "member"
}
```

Evidence: R2 expected shape; `examples/node-api-key/README.md` (“`user_id` is the key id, not a Zitadel user”).

### 5.4 `GET /me` is a write — do not hammer it

Handler (`MeEndpoints.GetMe`), after SCIM reject and `sub` check:

1. If API key → `GetMeForApiKey` (no domain/SSO join).
2. If user JWT **and** `GetEmailVerified(user) == true`:
   - `IDomainJoinService.AutoJoinAsync(sub, email)` — verified auto-join domains → **insert/activate membership** + FGA write/ticket.
   - If `HasIdpAuthentication(user)` (`amr` contains `idp`): `ISsoJoinService.AutoJoinAsync` — matching SSO domain → membership + FGA.
3. Then read memberships and return.

Evidence: `Features/Platform/MeEndpoints.cs` lines 58–68; `Features/Tenants/DomainJoinService.cs`; `Features/Enterprise/SsoJoinService.cs`; `TenantAccessService.GetEmailVerified` / `HasIdpAuthentication`.

Issue **025** (Done): `/me` used to auto-join from JWT email **without** `email_verified`. Now fail-closed: missing or false `email_verified` **must not** JIT-join. Comment in handler: “Fail-closed: missing or false email_verified must not JIT-join.” Residual risk: Zitadel **access** tokens often **omit** `email_verified` (`GetEmailVerified` returns `null` when claim absent) — then the `== true` check skips join. That is fail-closed, not “Pay should send id_token to get the claim.”

Issue **046** (Done): SSO JIT used to join on email host alone; password JWT could join an SSO tenant. Now `HasIdpAuthentication` must be true (`amr` contains `idp`). Password-only JWTs must not SSO-JIT.

Architecture (One `plans/015-dimension/01-architecture-to-a-plus.md`): “GET /me is a write (auto-join + JIT) on a read verb.” Pay 02-one-integration: “`GET /me` can **write** (domain auto-join, SSO JIT). Do not hammer it from a hot loop.” `NP-ONE-006` note: same.

**Rate limit:** `RateLimitOptions` does **not** include `GET /me`. Limits exist for create tenant, invite, resend, accept, API key create, webhooks, HRD, SCIM, **authz check**. Hammering `/me` will not 429 by default. It **can** create memberships, enqueue FGA repair, and race unique constraints. Pay should call `/me` on **session start / identity refresh**, not per payment webhook, not per ledger line, not in a poll loop.

`GET /me/invites` is a different route (also Bearer; **rejects API keys**; unverified email returns empty list). Not required for this AuthN slice.

---

## 6. Secret split table — who holds what

Expanded from `plans/011-new-lazuar-pay/02-one-integration.md` “## Secrets” with evidence of **where the bytes live today**. Pay’s column is the point of this paper.

| Secret / material | Who holds it | Where it lives today | Pay |
|-------------------|--------------|----------------------|-----|
| **Zitadel masterkey** (exactly 32 chars) | One ops | `lazuar-one/.env.example` `ZITADEL_MASTERKEY=MasterkeyNeedsToHave32Characters` (local insecure default). Changing after init breaks ciphertext. | **Never** |
| **First-instance / bootstrap volume** | One compose | `zitadel_bootstrap` volume; `login-client.pat` path inside container `/zitadel/bootstrap/login-client.pat` | **Never** |
| **Login-client PAT** (`IAM_LOGIN_CLIENT`) | **`lazuar-login` only** (+ stock Login V2 container) | `apps/lazuar-login/.secrets/login-client.pat`; env `ZITADEL_SERVICE_USER_TOKEN` / `_FILE`. Compose `ZITADEL_SERVICE_USER_TOKEN_FILE` on `zitadel-login`. **Never `VITE_*`.** | **Never** |
| **`ZITADEL_PAT` / `Zitadel:ServiceUserToken`** (Management provisioner) | One seed / One API provisioner | `seed-platform-spa-clients.sh` requires it. One API `Zitadel__ServiceUserToken` / `ServiceUserTokenFile`. **Not** the login-client PAT (`deploy/dev/README.md` “Login-client PAT ≠ provisioner PAT”). | **Never** |
| **OpenFGA store admin / `OpenFga:ApiToken`** | One ops / One API | `OpenFga:ApiToken` + `AuthMode=Preshared` → One’s `HttpClient` `Authorization: Bearer {ApiToken}` (`OpenFgaServiceCollectionExtensions.cs`). Local default `AuthMode: None`, empty token. Optional compose `OPENFGA_AUTHN_PRESHARED_KEYS`. Store/model ids from `deploy/dev/openfga/bootstrap.sh` → gitignored `.env.local`. | **Never.** Pay does not call `:8090`. Pay does not get `authz/write`. |
| **OpenFGA StoreId / AuthorizationModelId** | One API config | `appsettings` / env `OpenFga__StoreId`. Required when `Enabled=true` (fail-closed D07). | **Never** (Pay uses One `authz/check` HTTP later) |
| **Webhook AES key** (`Webhooks:SigningSecretEncryptionKey`) | One API config | Base64 32-byte AES-GCM key. Required Staging/Production when webhooks enabled (`WebhooksOptionsValidator`). Encrypts **One’s** stored signing secrets (and SSO OIDC secrets, audit streams — validator comment). | **Never** the AES key. Pay holds **Pay’s receiver HMAC** for One→Pay webhooks (shown once) — later slice. |
| **API key pepper** (`ApiKeys:Pepper`) | One API | HMAC key for hashing `lzr_sk_`. Strict env required. Dev default `local-dev-api-key-pepper-change-me`. | **Never** the pepper. Pay holds the **plaintext `lzr_sk_`** it was shown once. |
| **Platform admin email list** | One `Platform:AdminEmails` | CSV. `is_platform_admin` on `/me`. | **Never** as Pay’s staff gate. Pay may **read** `is_platform_admin` but must not invent a second list. |
| **Postgres passwords** (One) | One compose | `.env.example` `postgres/postgres` local. | Pay’s own DB later; do not reuse One’s DSN to “join memberships.” |
| **Pay OIDC `client_id`** | Pay (public) | Does not exist yet. When it does: Pay SPA/BFF env, like `VITE_ZITADEL_CLIENT_ID`. | **Yes (public).** Not a secret for `spa`. |
| **Pay OIDC `client_secret`** | Pay, **only if** `type=web` confidential | Returned **once** on create/rotate (`oidc-apps.md`). | **Yes, if web BFF.** Never in a Vite `VITE_*`. Not needed for backend-only `/me`. |
| **Pay `lzr_sk_`** | Pay (once, secret) | Minted via One API / lazuar-app UI. | **Yes.** Env `ONE_API_KEY`. Never logs, never git. |
| **Pay receiver HMAC** for One webhooks | Pay (shown once) | Later (`NP-ONE-017`). | **Yes, later.** |
| **Ada’s access_token / refresh_token** | Browser (lazuar-app sessionStorage) or Pay BFF later | `oidc-client-ts` user store. | Backend-only: engineer-exported env for dogfood. Product: SPA/BFF holds user tokens; Pay API may **forward** the access_token it was given, not mint one. |
| **Old Pay `Jwt:Secret`** | Old monolith | `AuthAndCorsExtensions` default `secure_development_key_minimum_32_characters_long`; issues `lazuar_auth`. | **Never copy into new Pay.** That is the cookie-JWT product this paper refuses. |

`NP-ONE-020`: “Pay holds only OIDC `client_id`, `lzr_sk_`, One-webhook HMAC.” That is the entire Pay-owned secret set for One integration. Everything else in the table is One ops.

**Do not swap login-client PAT and provisioner PAT.** Login README table:

| Credential | Consumer | Role |
|------------|----------|------|
| login-client PAT | lazuar-login BFF (+ stock Login V2) | Session + OIDC finalize |
| provisioner PAT | lazuar-api `Zitadel__ServiceUserToken` | Org/project/app Management API |

If Pay ever “needs a PAT to register an app,” the answer is: **Ada’s user JWT** calls `POST /tenants/{id}/apps` on One, or One ops runs the seed script. Pay does not become a Zitadel Management client.

---

## 7. Why Pay must not implement a password form / cookie JWT

This is not aesthetic. It is the reason Pay left the old tree **and** the reason One exists as a sibling.

### 7.1 One already owns the password UI

- lazuar-app / lazuar-admin are **pure OIDC PKCE clients**. They never see passwords or the login-client PAT (`lazuar-login/README.md`).
- The password form is `apps/lazuar-login/src/web/pages/LoginPage.tsx` (`login-password` test id, HRD, TOTP, recovery, passkey).
- Finalize uses Session API v2 with a **server-only** PAT. Putting that PAT in Pay (or in `VITE_*`) re-opens issue 010-class foot-guns (allowlist, Secure cookie, session token in cookie) on a second app.
- `02-one-integration.md` Do/Do-not table: “OIDC code + PKCE … | Password form in Pay”.
- `03-first-slice.md` Fail: “Pay password form or second org table.”
- `11-checklist.md` `NP-ONE-002`, `NP-ONE-005`; refuse list ships merchants to `:5173`.

A password form in Pay would be a **second IdP**. Ada’s One human would not be Pay’s user unless Pay also called Zitadel with a PAT — which Pay must not hold.

### 7.2 Old Pay already implemented the thing we are refusing

Evidence (old tree, still in this repo):

- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs`  
  `POST /one/auth/login` verifies email/password against **Pay’s** `GlobalUser` hash, then `IssueCookie`.
- `IssueCookie` builds a **symmetric** JWT (`Jwt:Secret`, issuer `lazuar-api`, audience `lazuar-clients`, TTL `Jwt:ExpiryHours` default 24) with claims `NameIdentifier`, `Email`, `Role` (`SUPER_ADMIN` / `CLIENT`), `is_system_admin`, `is_email_verified`, `security_stamp`, then `Cookies.Append("lazuar_auth", token, …)`.
- `apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs` `OnMessageReceived`: if path starts with `/api/v1/platform`, read cookie **`lazuar_admin_auth`**, else **`lazuar_auth`**, and set `context.Token`. Cookie **wins** as the JWT Bearer token source for browsers.
- Policies `OrgAdmin` / `OrgMember` / `OrgRead` require roles `SUPER_ADMIN`, `ADMIN`, `MEMBER`, `VIEWER` — **Pay’s** role vocabulary, not One’s `owner|admin|member`.
- Docs: `docs/001-gaps/03-api-auth-credentials.md`, `10-one-identity-module.md`, `04-developers-page-dx.md` — “cookie JWT primary”; “Spec implies Bearer; browsers use cookies.”

That design is exactly:

1. Password form in the product.
2. Homemade user table.
3. Homemade HS256 JWT in an HttpOnly cookie.
4. Dual cookie realm (ops vs admin).
5. OpenAPI saying Bearer while the browser sends a cookie.

New Pay’s job is money. One’s job is humans and tenants. Re-implementing `IssueCookie` on `:8081` would:

- Split Ada into two passwords (`Password1!` vs `Password123!`).
- Make `GET /me` on One unreachable without a second mapping table.
- Recreate “cookie vs Bearer” confusion (`plans/011-new-lazuar-pay/09-old-pay.md` still lists cookie-vs-Bearer among remaining defects of the old binary).
- Force Pay to hold `Jwt:Secret` — a secret that is **not** on the NP-ONE-020 list.

### 7.3 Cookie JWT is the wrong session for a sibling API

One’s resource server does **not** read cookies for `/api/v1/me`. It reads `Authorization`. A C# `HttpClient` on 8081 has no browser cookie jar to `lazuar-app` anyway. Forwarding `lazuar_auth` to One would 401 (wrong issuer, wrong signature, no `jti` of the Zitadel kind). Forwarding `lazuar_login_sess` would 401 (not a JWT access token, not `lzr_sk_`).

The honest session for Pay merchant ops (later, when a browser exists) is:

1. PKCE against `:8085` with **Pay’s** `client_id`.
2. Password on `:5175`.
3. Pay SPA or Pay BFF holds the **access_token**.
4. Pay API (8081) either validates nothing about One and **forwards** the access_token to One, or (if Pay also protects its own routes) validates the **same** Zitadel JWT as a resource server **without** becoming an IdP.

This paper does not require Pay to validate JWTs itself for the first `/me` dogfood. Pay can treat One as the identity oracle: call `/me` with the forwarded Bearer, cache the JSON briefly, do not mint a Pay cookie from it.

### 7.4 Buyers are a different plane

`02-one-integration.md` “Two planes”: merchant staff = One humans; buyer/payer = Pay checkout profile. Cardholders never become Zitadel users because they bought an ebook. Buyer magic-link on Pay is **not** this AuthN slice and **not** a reason to build `POST /auth/login` for merchants.

---

## 8. Recommended env vars for `apps/lazuar-pay`

Nothing below is implemented at SHA `6ca8f19f`. Names are recommendations so later code does not invent `ZITADEL_PAT` “for convenience.”

### 8.1 Backend-only first (this slice)

| Env | Local value | Required now | Notes |
|-----|-------------|--------------|-------|
| **`ONE_API_URL`** | `http://localhost:8080/api/v1` | **Yes** for `/me` dogfood | No trailing slash, or trim in code. Includes `/api/v1` (matches login’s `LAZUAR_ONE_API_URL`, app’s `VITE_API_URL`, node-api-key `LAZUAR_API_BASE`). |
| **`ONE_ACCESS_TOKEN`** | Ada’s JWT access_token | One of this **or** `ONE_API_KEY` | Engineer-exported, short-lived, never committed. Prefer this to prove **human** `/me`. |
| **`ONE_API_KEY`** | `lzr_sk_…` | One of this **or** `ONE_ACCESS_TOKEN` | Worker path. Prefix-check. Not a Zitadel user. |
| `ONE_TENANT_ID` | GUID | Optional | Only to send `X-Lazuar-Tenant-Id`. Not authorization. |

ASP.NET double-underscore alternative if bound to options later: `One__ApiUrl`, `One__AccessToken`, `One__ApiKey` — still **not** `Zitadel__ServiceUserToken`.

**Do not add to Pay env (even commented-in as “copy from One”):**

- `ZITADEL_PAT`
- `ZITADEL_SERVICE_USER_TOKEN` / `ZITADEL_SERVICE_USER_TOKEN_FILE`
- `Zitadel__ServiceUserToken`
- `ZITADEL_MASTERKEY`
- `OpenFga__ApiToken` / `OpenFga__StoreId` / `OPENFGA_AUTHN_PRESHARED_KEYS`
- `Webhooks__SigningSecretEncryptionKey` (One’s AES)
- `ApiKeys__Pepper`
- `PLATFORM_ADMIN_EMAILS` / `Platform__AdminEmails`
- `Jwt:Secret` (old Pay)
- Any `VITE_*` until there is a Pay SPA

### 8.2 Later — when Pay has a browser origin (not this slice)

Mirror **lazuar-app**, not old Pay:

| Env | Local analog | Notes |
|-----|--------------|-------|
| `VITE_ZITADEL_AUTHORITY` or Pay BFF `Zitadel:Authority` | `http://localhost:8085` | Issuer. Not `:5175`. |
| **`VITE_ZITADEL_CLIENT_ID` / `One:OidcClientId`** | seeded later | Public. From `POST /tenants/{id}/apps` or a first-party seed **after** origin exists. |
| `VITE_ZITADEL_REDIRECT_URI` | `http://localhost:<pay-spa>/callback` | Must be on One app + login allowlist. |
| `VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI` | Pay origin `/` | |
| `VITE_ZITADEL_SCOPE` | `openid profile email offline_access` | Add audience reserved scope in Staging. |
| `VITE_API_URL` (if SPA talks to One directly) or Pay BFF still uses `ONE_API_URL` | One `http://localhost:8080/api/v1` | SPA talking to **Pay** `:8081` is a different URL. Do not collapse Pay and One bases. |
| Confidential `client_secret` | only `type=web` | User-secrets / vault. Never `VITE_`. |

Login host remains **out of SPA OIDC env** (D92 / 08-dogfood §6.2). Product login stays `:5175`.

Pay API CORS (when a SPA exists) is **Pay’s** `CorsOrigins` for `:8081`, plus One’s `App:CorsOrigins` if the SPA calls One **directly**. Backend-only `/me` from 8081→8080 is **not** a CORS request.

---

## 9. SPA registration — defer until Pay has a browser origin; backend-only first

`NP-ONE-001` says “Register Pay SPA via One `POST /tenants/{id}/apps` (or seed like `lazuar-app`).” That is **true for the product** and **premature for this AuthN paper**.

Reasons to wait:

1. **No Pay browser origin exists.** `apps/lazuar-pay` is a health-only API on 8081. There is no `/callback`, no Vite port, nothing to put in `redirect_uris`. R3 without a real redirect produces a client Ada cannot use.
2. **Seed script is first-party app/admin only.** `seed-platform-spa-clients.sh` hard-codes names `lazuar-app` / `lazuar-admin` and redirects `:5174` / `:5173`. Adding Pay there is a One-side change that still needs a port and allowlist.
3. **Development `UseStub=true`** on One API makes API-created clients **non-loginable** until One runs with a provisioner PAT and stub off. That is One ops, not Pay AuthN.
4. **Login allowlist does not include Pay.** `REDIRECT_ALLOWLIST` would need a new origin (`NP-ONE-004`). Doing that before the origin exists is fiction.
5. **`GET /me` does not require Pay’s `client_id`.** One accepts **any** JWT access token from the configured issuer (Dev, audience off) or a `lzr_sk_`. Ada’s **lazuar-app** token is a valid human proof that Pay is Consumer-0. A Pay-branded `azp` is a later pin (issue 076 residual), not a week-one blocker.

**Backend-only first procedure (recommended dogfood for this slice):**

1. Run One stack + `pnpm login:dev` + `pnpm api:dev` + `pnpm app:dev` as §3.
2. Ada signs in on `:5174` via `:5175`.
3. Ada creates a workspace in **lazuar-app** (Pay should not `POST /tenants` until identity is proven — tracker step 3 is later).
4. Copy `access_token` **or** mint `lzr_sk_` with explicit scopes.
5. From Pay process (or a scratch `HttpClient` / curl on the side), `GET $ONE_API_URL/me` with `Authorization: Bearer …`.
6. Treat `user_id` + `tenants[].id` as Pay `org_id` **when** money code exists. Do not write a parallel users table.

**When to register the SPA (later, still not Console):**

- A Pay merchant UI origin is chosen (port pinned, `strictPort`, not 8080/5173/5174/5175/3005/8090).
- `POST /api/v1/tenants/{id}/apps` with that origin’s callback (R3) **or** a dedicated first-party seed modeled on `seed-platform-spa-clients.sh` (`OIDC_TOKEN_TYPE_JWT`, PKCE public, Dev Mode for http).
- Add origin to login `REDIRECT_ALLOWLIST`.
- Add origin to One `App:CorsOrigins` if the browser calls One directly.
- Picker: JWT `access_token` only (copy `pickApiBearerToken`).
- Still no password form in Pay.

Until then, **Pay is a resource-server client of One**, not an OIDC client of Zitadel.

---

## 10. Fail modes

### 10.1 Sending `id_token` as Bearer

**Symptom:** 401 ProblemDetails while DevTools shows a signed-in user / a pretty JWT.

**Why:** `JwtAccessTokenGuard` requires `jti`. Zitadel ID tokens lack it. If `token_use=id_token` is present, explicit reject. M2M-14 / issue 002.

**How Pay gets here:** copying the wrong JSON field from sessionStorage; “the ID token has email”; falling back when access is opaque; passing `user.id_token` into a generic client.

**Fix:** send `access_token` only. If access is opaque, **stop** — leftover app (oidc-apps “Existing apps”) or seed missed JWT. Recreate via One apps API or (first-party only) spa-oidc-setup Step 5. Do not heal with `id_token`.

**Test lock on One:** `JwtBearerMeTests.Id_token_shape_returns_401`; picker tests `never returns id_token` / `does not fall back to JWT id_token when access is opaque`.

### 10.2 Hammering `GET /me`

**Symptom:** surprising new memberships (domain auto-join / SSO JIT); FGA repair tickets; unique-constraint noise; identity read coupled to lifecycle writes. **Not** necessarily 429 (`RateLimitOptions` has no `/me` policy).

**Why:** `GetMe` is a command-on-GET when `email_verified == true` (and `amr` contains `idp` for SSO). Architecture debt H in One 015-dimension/01.

**How Pay gets here:** middleware that calls `/me` on every Pay request; payment webhook handler that “re-checks identity”; polling for `active_tenant_id`; retry storm on 503 after a join that already committed (One’s accept-invite docs warn about this class of retry; `/me` join has similar persist-then-FGA shape).

**Fix:** call `/me` on login / identity refresh / explicit “who am I.” Cache. Use `authz/check` for permission chrome (rate-limited). Use webhooks (`member.*`, `tenant.suspended`) for changes, later slice. Do not put `/me` in the charge path.

### 10.3 Holding a PAT (Zitadel or login-client) or OpenFGA admin token

**Symptom:** Pay env contains `ZITADEL_PAT`, `ZITADEL_SERVICE_USER_TOKEN`, `Zitadel__ServiceUserToken`, `OpenFga__ApiToken`, or a file copy of `login-client.pat`.

**Why this is fatal to the Consumer-0 story:** 08-dogfood header — a sibling product must treat One as identity **without** holding a Zitadel PAT or OpenFGA admin token, and without opening `lazuar-admin` as a customer workshop. If Pay holds the provisioner PAT, Pay **is** a Zitadel operator and will start creating orgs in Console. If Pay holds login-client PAT, Pay **is** a second login BFF (issue 010 residuals: allowlist, Secure, session cookie encryption). If Pay holds FGA admin, Pay will write tuples and skip `authz/check`.

**How Pay gets here:** “we need to register an SPA”; “bootstrap is easier if Pay seeds Ada”; copy-paste from `deploy/dev/README.md` into Pay `appsettings`; using OpenFGA playground token “to debug membership.”

**Fix:** Ada’s **user JWT** or a **scoped `lzr_sk_`**. App registration through One HTTP as that user. Seed scripts stay in **lazuar-one**. FGA stays behind One.

`NP-ONE-020` is the checklist row. Fail the PR that adds these keys to `apps/lazuar-pay`.

### 10.4 Adjacent fail modes (same slice, same laptop)

| Fail | Symptom | Fix |
|------|---------|-----|
| **HttpClient hits old Pay :8080** | `/one/auth/me` cookie world; no `/api/v1/me` of One’s shape; or 404 | Stop old API. One `GET /health` → `{"status":"ok"}`. One anonymous `/api/v1/me` → 401 ProblemDetails. |
| **HttpClient hits :8081** | Pay health JSON, not `MeResponse` | `ONE_API_URL` is One, not self. |
| **HttpClient hits :8085 `/me`** | Zitadel 404 / HTML | `:8085` is issuer. `/api/v1/me` is `:8080`. |
| **Opaque access token** | 401; picker would have omitted Authorization | Recreate app as JWT; do not send `id_token`. |
| **Wrong `iss`** | 401 | Host API `Zitadel:Authority=http://localhost:8085`. Do not use `http://zitadel-api:8080` from Pay on the host. |
| **Staging/prod wrong `aud`** | 401 after cutover | Reserved audience scope. Do not “fix” with `id_token`. Local Dev may hide this (audience off). |
| **Clock skew > 60s** | 401 `exp`/`nbf` | One skew is 60s, not 5 minutes (issue 003). |
| **Prefix mix-up** | `lzr_sk_` vs old `sk_live_` vs `lzr_scim_` | Smart scheme: only `lzr_sk_` is Pay’s machine key. |
| **API key `/me` treated as Ada** | `user_id` is a GUID key id | Do not invite that id; do not use it as FGA user. Mint keys with a human JWT. |
| **Ada password from old Pay table** | `Password123!` / `founder@acme.test` | Wrong IdP. `ada@acme.test` / `Password1!` on `:5175`. |
| **Staff Ada** | Using `zitadel-admin@…` in Pay | Merchants never `:5173`. |
| **Ship `:3005`** | Login V2 **or** old admin | `NP-ONE-005`. Product login `:5175`. |
| **Pay homepage = `:5175`** | Users bookmark login BFF | Authorize from Pay/app origin; Zitadel redirects. |
| **Password grant** | Pay collects `Password1!` | Forbidden. PKCE + login UI. |
| **Cookie `lazuar_auth` forwarded to One** | 401 | Wrong JWT. See §7. |
| **CORS panic on C# `/me`** | — | Server-to-server has no CORS. CORS matters when a **browser** on a Pay origin calls One. Then allowlist that origin on One. |
| **Register SPA in Console only** | Redirects drift; leftover opaque | One apps API + login allowlist (`NP-ONE-004`). |
| **Parse project roles from JWT** | Chrome lies | `/me` + `authz/check`. |
| **Authorize from `X-Lazuar-Tenant-Id` alone** | Cross-tenant | Path id + membership. Hint only. |
| **Log Authorization** | Secret leak | One already refuses to log the header. Pay must too. |
| **Commit tokens** | git | `.env` gitignored. Same rule as login `.secrets/`. |

---

## 11. Evidence index (paths opened for this paper)

### Pay repo (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay`)

- `plans/011-new-lazuar-pay/02-one-integration.md` — identity of Pay, AuthN do/do-not, secrets table, `GET /me` write warning.
- `plans/011-new-lazuar-pay/03-first-slice.md` — step 2 sign-in `:5175` + `/me`; fail on password form.
- `plans/011-new-lazuar-pay/11-checklist.md` — `NP-ONE-001`…`006`, `NP-ONE-020`.
- `plans/011-new-lazuar-pay/12-first-slice-tracker.md` — ordered step 2.
- `plans/011-new-lazuar-pay/00-why-leave.md`, `05-language.md`, `09-old-pay.md` — context only.
- `apps/lazuar-pay/README.md`, `Program.cs`, `Properties/launchSettings.json`, `Taskfile.yml` `pay:dev` — 8081, no One client yet.
- `README.md` port table; old demo accounts.
- `apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs` — cookie JWT `OnMessageReceived`.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs` — `IssueCookie` / `POST /one/auth/login`.
- `docs/001-gaps/03-api-auth-credentials.md`, `10-one-identity-module.md`.

### One repo (`/Users/akmalfirdaus/Code/lazuar/lazuar-one`)

- `README.md` — ports, Ada creds, first-time local, `GET /api/v1/me`.
- `apps/lazuar-docs/docs/reference/ports.md`
- `apps/lazuar-docs/docs/local/api.md`, `spa-oidc-setup.md`, `bootstrap-platform.md`, `quickstart/index.md`
- `apps/lazuar-docs/docs/recipes/user-oidc-spa.md` (R1), `service-api-key.md` (R2), `register-oidc-app.md` (R3)
- `apps/lazuar-docs/docs/integrations/oidc-apps.md`
- `docker-compose.yml`, `.env.example`
- `scripts/seed-platform-spa-clients.sh`, `scripts/seed-dev-demo.sh`, `scripts/seed-dev-demo.py`
- `apps/lazuar-app/.env.example`, `src/auth/oidcConfig.ts`, `src/auth/bearerToken.ts`, `src/auth/bearerToken.test.ts`, `src/pages/LoginPage.tsx`, `src/App.tsx`, `src/api/client.ts`, `README.md`
- `apps/lazuar-login/README.md`, `.env.example`, `src/web/pages/LoginPage.tsx`
- `apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Auth/{AuthenticationExtensions,JwtAccessTokenGuard,ZitadelJwtBearerDefaults,ApiKeyAuthenticationHandler,ApiKeyDefaults,ApiKeyHasher}.cs`
- `Features/Platform/MeEndpoints.cs`, `Features/Tenants/DomainJoinService.cs`, `Features/Enterprise/SsoJoinService.cs`, `Infrastructure/Tenancy/{TenantAccessService,ActiveTenantHint}.cs`
- `Configuration/{OpenFgaOptions,WebhooksOptions,ApiKeysOptions,RateLimitOptions}.cs`
- `Infrastructure/OpenFga/OpenFgaServiceCollectionExtensions.cs`
- `appsettings.Development.json`
- `tests/.../JwtBearerMeTests.cs`, `JwtBearerValidationTests.cs`
- `packages/api-spec/modules/platform/{routes,models}.tsp`
- `packages/one-client/{README.md,src/createClient.ts}`
- `examples/node-api-key/{README.md,index.mjs}`, `examples/oidc-spa-notes/README.md`, `examples/vite-spa/src/bearerToken.ts`
- `issues/001-tenant-oidc-apps-mint-opaque-tokens.md`, `002-spas-send-id-token-as-bearer.md`, `003-audience-validation-off-in-strict-defaults.md`, `010-login-bff-production-footguns.md`, `025-me-jit-ignores-email-verified.md`, `046-sso-jit-joins-password-jwt.md`, `076-jwt-no-azp-dev-audience-off.md`
- `plans/017-evals/08-dogfood-then-serve.md` §6; `FEATURE-CHECKLIST.md` M2M-14
- `plans/013-feats/wave-0-implementation-analysis.md` (M2M-14 already shipped)
- `deploy/dev/README.md`

---

## 12. What “done” looks like for this slice (analysis bar, not a code bar)

This paper is done if a later implementer can, without re-deriving AuthN:

1. Bind **One** on **8080** and **Pay** on **8081** without confusing either with old Pay, Login V2, or OpenFGA.
2. Send **exactly** `Authorization: Bearer <JWT access_token|lzr_sk_>` to `GET /api/v1/me`.
3. Refuse `id_token`, PATs, FGA admin tokens, password forms, and cookie JWTs.
4. Get Ada’s token via `:5174` → `:8085` → `:5175` → callback, creds `ada@acme.test` / `Password1!`.
5. Defer Pay SPA `client_id` until a browser origin exists.
6. Call `/me` rarely enough that JIT writes stay a login concern, not a payment loop.

Implementation of the `HttpClient` is a later checklist flip (`NP-ONE-003`, `NP-ONE-006`), not this file.
