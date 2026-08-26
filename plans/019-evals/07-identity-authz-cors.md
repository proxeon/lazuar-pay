# 07 — Identity, authz, CORS, staff session

**Family:** 019-evals  
**Paper:** 07 — Identity, OIDC, One membership gates, CORS, staff display on the newest Pay stack  
**Date:** 26 August 2026  
**Type:** Uncondensed evaluation. **Not an implementation.** Live files are authority. Do not copy `Modules/One`. Do not flip checklist cells from this file.

| | |
|--|--|
| Repo | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` |
| Branch | `feat/018-merchant-shell` |
| HEAD (short) | `9f04ad58` |
| Subject | `fix(pay-ui): match receipts table to pay-link chrome` |
| Sibling One (HMAC signer, `/me`, apps, CORS, FGA) | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` on `main` — `WebhookSigning.FormatHeader` is `v1=` + separate `X-Lazuar-Timestamp`. Development `App:CorsOrigins` includes `:5178`. |

**Standing law used as the ruler (not as evidence that the code matches):**

- Staff are One / Zitadel humans. Buyers are not. `VIEWER` is not a One tenant role. One product roles are `owner` \| `admin` \| `member`.
- Path `{orgId}` + One `POST /tenants/{id}/authz/check` is authorization SoT. `X-Lazuar-Tenant-Id` is a hint only.
- Pay never holds a Zitadel PAT, login PAT, or OpenFGA admin token.
- Merchant SPA sends a JWT `access_token` as Bearer. Never `id_token`. Tokens live in `sessionStorage`. Fetches omit cookies.
- Checkout (`:5179`) stays anonymous. No OIDC. No whoami.
- Plane A (One → Pay HMAC) is not Plane B (PSP → Pay). Do not verify One with a Stripe secret.

---

## Coordinates

Focused Pay host is `apps/lazuar-pay` on **http://localhost:8081**. Merchant Vite is `apps/lazuar-pay-merchant` on **:5178** (`strictPort`). Checkout Vite is `apps/lazuar-pay-checkout` on **:5179**. Identity plane is sibling One: API **:8080**, product login **:5175**, Zitadel issuer **:8085**. Old Hub ops **:3003** and portal **:3004** are a different product and must stay off Pay CORS.

Pay does **not** run ASP.NET JWT middleware. There is no `AddAuthentication` / `AddJwtBearer` in `Program.cs`. Staff identity is: browser PKCE against Zitadel → JWT `access_token` in `sessionStorage` → `Authorization: Bearer` on Pay → Pay forwards that same header to One `GET /me` and `POST …/authz/check`. One says 200 or Pay maps the failure. That is the whole AuthN loop.

One webhook HMAC is a **separate** door: `POST /v1/one/webhooks`, no Bearer. If that door does not verify what One actually sends, `tenant.suspended` never sets `org_settings.charges_paused`, and the **buyer** path (`POST /v1/pay/{token}/start`) keeps taking money. Staff membership 403 on a suspended tenant is not a substitute: buyers have no One token and never hit `MemberGate`.

---

## Files opened

### Pay host — Identity

- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/Bearer.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneAuthz.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneCallResult.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneMeMapper.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneMeResponse.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneOptions.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiResponse.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OrgReadyEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayErrors.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Hosting/HealthEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.json`
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.Development.json`
- `apps/lazuar-pay/src/Lazuar.Pay/Properties/launchSettings.json`
- `apps/lazuar-pay/.env.example`
- `apps/lazuar-pay/README.md`

### Pay host — gates on money / catalog / public

- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs` (`OneWebhookEventRow`, `OrgSettingsRow.ChargesPaused`)
- `apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs` (`one_webhook_events` unique `DeliveryId`)

### Pay host — tests

- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/WhoamiTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OrgReadyTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/CorsTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/FakeOneHandler.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayTest.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Checkouts/CheckoutTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Catalog/CatalogTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Money/PaymentQueryTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPay/PublicPayTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`

### Merchant (`:5178`)

- `apps/lazuar-pay-merchant/src/auth/bearerToken.ts`
- `apps/lazuar-pay-merchant/src/auth/bearerToken.test.ts`
- `apps/lazuar-pay-merchant/src/auth/oidcConfig.ts`
- `apps/lazuar-pay-merchant/src/auth/RequireAuth.tsx`
- `apps/lazuar-pay-merchant/src/lib/staffDisplay.ts`
- `apps/lazuar-pay-merchant/src/lib/staffDisplay.test.ts`
- `apps/lazuar-pay-merchant/src/lib/sessionKeys.ts`
- `apps/lazuar-pay-merchant/src/lib/roles.ts`
- `apps/lazuar-pay-merchant/src/lib/oneApi.ts`
- `apps/lazuar-pay-merchant/src/lib/payApi.ts`
- `apps/lazuar-pay-merchant/src/lib/http.ts`
- `apps/lazuar-pay-merchant/src/lib/homePath.ts`
- `apps/lazuar-pay-merchant/src/main.tsx`
- `apps/lazuar-pay-merchant/src/App.tsx`
- `apps/lazuar-pay-merchant/src/pages/LoginPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/CallbackPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/HomePage.tsx`
- `apps/lazuar-pay-merchant/src/pages/CreateWorkspacePage.tsx`
- `apps/lazuar-pay-merchant/src/pages/CreateWorkspaceForm.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/CreateWorkspacePage.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/OverviewPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/PaymentsPage.tsx`
- `apps/lazuar-pay-merchant/src/layout/OrgLayout.tsx`
- `apps/lazuar-pay-merchant/src/layout/DashboardChrome.tsx`
- `apps/lazuar-pay-merchant/src/layout/WorkspaceSwitcher.tsx`
- `apps/lazuar-pay-merchant/src/layout/nav.ts`
- `apps/lazuar-pay-merchant/src/ui/components/app-sidebar/user-menu.tsx`
- `apps/lazuar-pay-merchant/src/locks.test.ts`
- `apps/lazuar-pay-merchant/src/main.tsx`
- `apps/lazuar-pay-merchant/scripts/register-spa.sh`
- `apps/lazuar-pay-merchant/package.json`
- `apps/lazuar-pay-merchant/vite.config.ts`
- `apps/lazuar-pay-merchant/vitest.config.ts`
- `apps/lazuar-pay-merchant/README.md`

### Checkout (`:5179`) — anonymity proof

- `apps/lazuar-pay-checkout/src/App.tsx`
- `apps/lazuar-pay-checkout/src/main.tsx`
- `apps/lazuar-pay-checkout/src/locks.test.ts`
- `apps/lazuar-pay-checkout/package.json`
- `apps/lazuar-pay-checkout/vite.config.ts`

### Contract / isolation / checklists (honesty of ticks, not authority)

- `packages/pay-spec/main.tsp`
- `plans/012-one-to-pay/02-one-authn-tokens.md`
- `plans/012-one-to-pay/07-authz-roles.md`
- `plans/012-one-to-pay/09-webhooks-events.md`
- `plans/013-prods/08-one-identity-production.md`
- `plans/013-prods/checklists/m10-spa-register.md`, `m12-bearer-picker.md`, `m18-pick-org.md`, `m19-create-workspace.md`, `m21-no-id-token.md`, `m22-session-storage.md`, `m24-role-chrome.md`, `m25-allowlist-cors.md`
- `plans/013-prods/checklists/k14-cors-pay.md`, `k17-no-oidc-checkout.md`
- `plans/013-prods/checklists/o10-invite-copy-link.md`, `o12-member-sees-ops.md`, `o13-lzr-sk.md`, `o14-one-hmac-route.md`, `o15-one-hmac-verify.md`, `o16-tenant-suspended.md`, `q15-cors-still-denies-ops.md`

### Sibling One (what Pay must speak — not copied into Pay)

- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Webhooks/WebhookSigning.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Webhooks/WebhookDispatcher.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Platform/MeEndpoints.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Authz/AuthzObjectRules.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Domain/Tenants/MembershipRoles.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Tenants/TenantEndpoints.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Tenants/MemberEndpoints.cs`
- `lazuar-one/deploy/dev/openfga/model.fga`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/appsettings.Development.json` (`App:CorsOrigins`)
- `lazuar-one/apps/lazuar-docs/docs/recipes/register-oidc-app.md`

**Not opened on purpose:** Hub `Modules/One/**`, rail HTTP adapters, occupancy algorithm internals beyond `MemberGate` on the payment-link create/list routes, TypeSpec as a whole paper.

---

## What exists

### 1. Composition (`Program.cs`)

`OneOptions` binds from config section `One`. Typed `HttpClient<OneClient>` is registered. CORS is a **hardcoded** default policy (not `App:CorsOrigins`). Pipeline is `UseCors()` then the maps. Identity maps are `MapWhoami`, `MapOrgReady`, `MapOneWebhooks`. Money maps sit beside them. There is **no** cookie auth, **no** JWT bearer handler, **no** `lazuar_auth`.

Origins allowed:

- `http://localhost:5178` and `http://127.0.0.1:5178` (merchant dev)
- `http://localhost:5179` and `http://127.0.0.1:5179` (checkout dev)
- `http://localhost:4178` / `4179` and `127.0.0.1` twins (Vite preview)

`AllowAnyHeader` + `AllowAnyMethod`. **No** `AllowCredentials`. That is the right shape for a Bearer SPA: `Authorization` is a non-safelisted header, so browsers preflight; the policy must allow the header; cookies must not ride along.

`3003` / `3004` / `5173` / `5174` / `3005` are absent. That is the deny list that `CorsTests` actually proves for `3003` and `3004` on `GET /health`.

### 2. Bearer extraction (host)

`Bearer.TryGet` (`Identity/Client/Bearer.cs:5-20`) reads `Authorization`, requires a `Bearer ` prefix (case-insensitive), and rejects empty remainder. It does **not** look at cookies. It does **not** inspect JWT shape. Opaque `lzr_sk_…` would pass this gate and be forwarded to One. JWT-likeness is a **merchant SPA** rule, not a host rule. That split is correct: the host must accept whatever One accepts.

### 3. `GET /v1/whoami` → One `GET /me`

`WhoamiEndpoints.Handle` (`WhoamiEndpoints.cs:13-23`): missing Bearer → 401 `"Missing bearer token"` and **does not call One** (`Whoami_without_authorization_is_401_and_skips_one`). With Bearer, it forwards `Authorization` and optional `X-Lazuar-Tenant-Id` to `OneClient.GetWhoamiAsync`, which `GET`s `{BaseUrl}/me` (default `http://localhost:8080/api/v1/me`).

`OneMeMapper.ToWhoami` maps One snake_case `/me` into Pay's `WhoamiResponse`:

| One `/me` | Pay `/v1/whoami` |
|-----------|------------------|
| `user_id` | `user_id` |
| `email` | `email` |
| `name` | `name` (mapped; **missing from `packages/pay-spec` `WhoamiResponse`**) |
| `is_platform_admin` | `is_platform_admin` (forwarded, **unused** by merchant chrome) |
| `active_tenant_id` | `active_org_id` |
| `active_role` | **dropped** |
| `tenants[].id/slug/name/role/status` | same, skip rows with empty `id` |

Missing `user_id` on a 200 from One → mapper returns null → Pay **503**. Fail closed. Timeouts and transport failures → 503 `"Identity provider unreachable"`. One 401 → Pay 401. One 403 → Pay 403. Other codes → 503.

This is Mode U: Pay does not introspect the JWT. One is the IdP resource server.

### 4. `GET /v1/orgs/{orgId}/ready` — dummy after member

`OrgReadyEndpoints.Handle` (`OrgReadyEndpoints.cs:13-26`) calls `MemberGate.RequireMemberAsync` then always returns `{ org_id, ready: true }`. It does **not** read `org_settings`, vault, catalog, or `charges_paused`. `OrgReadyTests.Ready_when_one_allows_member` locks that dummy. The merchant SPA **never calls this route**. Org chrome uses `GET /v1/whoami` + `tenants.find(id === orgId)`.

### 5. `MemberGate` vs One `authz/check` vs writer

**Member** (`MemberGate.cs:8-43`):

1. Bearer required (401).
2. Empty `orgId` → 400 `"org_id is required"`.
3. `OneClient.CheckMemberAsync` POSTs `{ relation: "member", object: { type: "tenant", id: orgId } }` to `tenants/{orgId}/authz/check`, forwarding the hint header.
4. One 200 + `allowed: true` → pass.
5. One 200 + `allowed: false` → 403 `"Not a member of this org"`.
6. One 401 → 401 `"Identity provider rejected the token"`.
7. One 403 → 403 `"Not a member of this org"` (this is also how a **suspended** tenant comes back from One's membership gate).
8. Timeout / transport / other → 503.

The check body **omits `user_id`**. One infers the subject from the token. `OrgReadyTests.Ready_when_one_allows_member` asserts `LastBody` does not contain `user_id`. Path org is SoT: `Ready_checks_path_org_not_header` posts `id: path-org` even when the hint is `header-org`.

Live One `AuthzObjectRules.ValidateObject` requires `object.id` to be a **UUID equal to the path tenant**. Hermetic tests use `"t1"`. Fake One never validates GUIDs. Against live One, `/v1/orgs/t1/ready` would 400 from One, which Pay maps to **503**, not 400.

FGA on One (`model.fga`):

```
define owner: [user]
define admin: [user] or owner
define member: [user] or admin
```

So `relation=member` is true for owner, admin, and member. That is the read gate. It is **not** the write gate.

**Writer** (`MemberGate.cs:45-71`):

1. `RequireMemberAsync` first (so a non-member never gets a "writer" 403).
2. Then **`GET /me` again** and read `tenants.FirstOrDefault(t => t.Id == orgId)?.Role`.
3. Role must be `"owner"` or `"admin"`. Else 403 `"Writer role required"`.
4. `/me` failure after member passed → 503.

`OneAuthz` DTOs exist. There is **no** `CheckWriterAsync` that posts `relation: "admin"`. Writer authorization is a `/me` role string overlay, not an OpenFGA check. One already allow-lists tenant relation `admin` (and `owner`). FGA `admin` includes `owner`. The host could have asked One once.

Merchant chrome `canWriteMoney` (`roles.ts:2-4`) is the same string test. Hide-button is **not** authorization; APIs still 403.

`VIEWER` does not appear in merchant source. One `MembershipRoles` is `owner` / `admin` / `member` only. OpenFGA `viewer` is on type **`app`** (OIDC registry), not tenant. Old Hub `VIEWER` is museum.

### 6. Which routes are writer-gated vs member-gated

| Route | Gate | Evidence |
|-------|------|----------|
| `POST /v1/checkouts` | **Writer** | `CheckoutEndpoints.cs:29` |
| `GET /v1/checkouts/{id}` | **Member** of `session.OrgId` | `CheckoutEndpoints.cs:115` (after 404 if missing) |
| `GET /v1/orgs/{orgId}/checkouts` | **Member** | `CheckoutEndpoints.cs:131` |
| `POST /v1/payment-links` | **Writer** | `PaymentLinkEndpoints.cs:27` |
| `GET /v1/orgs/{orgId}/payment-links` | **Member** | `PaymentLinkEndpoints.cs:112` |
| `PUT /v1/orgs/{orgId}/gateway` | **Writer** | `GatewayEndpoints.cs:30` |
| `GET /v1/orgs/{orgId}/gateway` | **Member** | `GatewayEndpoints.cs:152` |
| `GET /v1/orgs/{orgId}/gateways` | **Member** | `GatewayEndpoints.cs:190` |
| `POST /v1/orgs/{orgId}/products` | **Writer** | `CatalogEndpoints.cs:24` |
| `GET /v1/orgs/{orgId}/products` | **Member** | `CatalogEndpoints.cs:72` |
| `GET /v1/orgs/{orgId}/payments` | **Member** | `PaymentQueryEndpoints.cs:24` |
| `GET /v1/orgs/{orgId}/receipts` | **Member** | `PaymentQueryEndpoints.cs:71` |
| `GET /v1/orgs/{orgId}/receipts/{id}` | **Member** | `PaymentQueryEndpoints.cs:128` |
| `GET /v1/orgs/{orgId}/ready` | **Member** | `OrgReadyEndpoints.cs:19` |
| `GET /v1/whoami` | Bearer only (no org) | `WhoamiEndpoints.cs:15` |
| `GET /v1/pay/{token}` | **None** | `PublicPayEndpoints.cs:27-32` — no `MemberGate`, no Bearer |
| `POST /v1/pay/{token}/start` | **None** (anonymous); `ChargesPaused` 403 | `PublicPayEndpoints.cs:80-125` and `MintOrResume` at `214-216` |
| `POST /v1/one/webhooks` | HMAC, not Bearer | `OneWebhookEndpoints.cs:13` |
| `POST /v1/webhooks/{provider}/{orgId}` | Plane B (out of scope) | — |
| `GET /health`, `/v1/health` | None | `HealthEndpoints.cs:9-10` |

Yes: **POST checkouts, PUT gateway, POST payment-links (and POST products) are writer-gated.** **GET lists are member-gated.** That matches the standing law. The merchant mint path used by `:5178` is `POST /v1/payment-links` plus `POST /v1/orgs/{orgId}/products`, both writer.

`ChargesPaused` is checked on writer mint (`CheckoutEndpoints.cs:43-46`, `PaymentLinkEndpoints.cs:41-44`) and on **buyer start**. Fulfillment also throws `ChargesPausedException`. Pause is real **if the flag is set**. The flag is set only from Plane A.

### 7. Plane A HMAC (what Pay implements vs what One sends)

Pay route: `POST /v1/one/webhooks`. Secret: process env `Pay:OneWebhookSecret` (`.env.example` line 21, commented). Missing secret → 503 `"One webhook secret missing"`.

Pay verifier (`OneWebhookSignature.cs:7-43`) documents itself as Standard Webhooks–style:

> header `t={unix},v1={lowercase hex}` over `{unix}.{body}`  
> “Judgment stolen from One's signer.”

That comment is **false** against live One.

Live One (`WebhookSigning.cs:7-26`, `WebhookDispatcher.cs:122-141`):

```
signed_payload = "{unix_seconds}." + raw_body_bytes
digest         = HMAC-SHA256(key = full whsec_ UTF-8, msg = signed_payload)
X-Lazuar-Signature: v1=<lowercase hex>          // FormatHeader = "v1=" + hex
X-Lazuar-Timestamp: <unix seconds>              // separate header
X-Lazuar-Event-Id: <outbox guid>
X-Lazuar-Delivery-Id: <delivery row guid>
X-Lazuar-Tenant-Id: <tenant guid>
X-Lazuar-Event-Type: <catalog type>
```

Envelope body (012/09, 013/08, One dispatcher payload): `{ id, type, created_at, tenant_id, api_version, data }`. **No `org_id`.** Pay `ReadOrgId` looks at `org_id` then `tenant_id` (`OneWebhookEndpoints.cs:76-97`). The `tenant_id` branch would work **if verify succeeded**. Tests also send `org_id` as a Pay-invented field.

Pay `TryParseHeader` requires **both** `t=` and `v1=` **inside** `X-Lazuar-Signature`, split on commas. One's `v1=<hex>` has no `t=`. Parse fails. `TryVerify` returns false. Handler returns **401 Invalid HMAC**. `tenant.suspended` is never applied. `ChargesPaused` stays false. Buyer `start` keeps charging.

`OneWebhookTests.Sign` (`OneWebhookTests.cs:13-16`) mints `t={unix},v1={hex}` over `{unix}.{body}` — **Pay's dialect, not One's**. `Body_only_uppercase_hex_is_401` rejects the **old Hub** dialect (HMAC of body only, uppercase hex, no `t=`/`v1=`). That reject is correct for Hub museum. It is **not** a test that One's live header works.

On a verified (test-dialect) `tenant.suspended`, Pay inserts `one_webhook_events` and sets `OrgSettings.ChargesPaused = true` (creates the row if missing). `tenant.reactivated` clears the flag only if a settings row already exists. Idempotency key is body `id` stored as `DeliveryId` (unique index). One's retry identity is `X-Lazuar-Event-Id` (same guid as envelope `id`) with a **new** `X-Lazuar-Delivery-Id`. Using body `id` is the right idempotency tuple **if** the body is One's envelope. The column name `DeliveryId` is a lie.

`member.*`, `tenant.deleted`, `api_key.revoked` are stored as rows with `EventType` and otherwise ignored. Membership SoT remains `/me` + `authz/check`. That ignore is acceptable for staff chrome. `tenant.deleted` not pausing is a gap for leftover public links.

There is **no** Pay code that `POST`s One `/tenants/{id}/webhooks` to register this URL. Ops must do it. One SSRF blocks loopback, so `http://localhost:8081/v1/one/webhooks` will not receive laptop push without One's `Webhooks:UrlHostAllowlist`. That is One ops, not a Pay PAT.

One secret is **per endpoint / per tenant**, shown once as `whsec_…`. Pay holds **one** process env var. N shops cannot share it. 014-evals already named this hatch. Still true on this SHA.

### 8. Pay never holds a Zitadel PAT — still true

Grep of `apps/lazuar-pay/src` for `ZITADEL_PAT`, `OpenFga`, `client_secret`, `AddAuthentication`, `AddJwtBearer`, `lazuar_auth`: **empty**. Host `.env.example` says “no PAT, no OpenFGA admin”. `register-spa.sh` refuses to run on PAT and tells the operator to export Ada's `ACCESS_TOKEN`. IsolationTests bans `Modules.One` / `namespace Lazuar.Pay.One;` / Hub project references. The SPA client_id is public PKCE (`VITE_ZITADEL_CLIENT_ID`). Pay does not hold the login-client PAT. **Still true.**

### 9. Merchant OIDC, picker, session

`getOidcConfig` (`oidcConfig.ts:9-36`): authorization code + PKCE, `response_type: 'code'`, `automaticSilentRenew: true`, `userStore: sessionStorage`. Authority default `:8085`. Redirect default `http://localhost:5178/callback`. Scope default `openid profile email offline_access`. **No** `extraQueryParams` / `urn:zitadel:iam:org:project:id:…:aud`. Login UI is One `:5175` via the issuer, not a Pay password form.

`pickApiBearerToken` (`bearerToken.ts:14-18`): JWT-like `access_token` only (`three` non-empty `.` parts). Opaque, JWE, empty, signed-out → `undefined`. **Never** returns `id_token`. Tests lock that (`bearerToken.test.ts:23-38`).

`RequireAuth` gates on `auth.isAuthenticated` only. It does **not** require `pickApiBearerToken`. `HomePage` / `OrgLayout` / `CreateWorkspacePage` call `signinRedirect()` when the picker is empty. An authenticated session with an opaque access token can loop.

`payApi.ts` comment at line 20: credentials omitted because localhost cookies are not port-scoped. `fetch` never sets `credentials: "include"`. Grep of merchant `src` for `credentials` is that one comment. Cookie vs Bearer SPA: **Bearer + sessionStorage**. Correct.

`locks.test.ts` bans `type="password"`, `/one/auth/login`, `lazuar_auth`, Hub `@repo/api-types-ts`.

### 10. Last workspace after login

`dashboardPath` (`homePath.ts:5-9`): empty tenants → `/workspaces/new`. Else sessionStorage `ORG_HINT_KEY` if that id is still in `tenants[]`, else `tenants[0]`. One `/me` orders tenants by **name** (`MeEndpoints.cs:79`). `HomePage` calls `getWhoami(token)` **without** org hint, so `active_org_id` is null unless One had some other default. Last-used is **not** One's active tenant. It is a per-tab `sessionStorage` key.

`CallbackPage` navigates to `takeReturnTo() ?? '/'`. Deep links skip `HomePage`. `OrgLayout` `setOrgHint(orgId)` on every org URL **before** membership is proven (`OrgLayout.tsx:38-39`). Non-member sees `"Not a member of this org"` and does **not** fall through to `dashboardPath`.

Same-tab hint after a removal: `dashboardPath` drops the stale id (not in `tenants[]`) and uses `tenants[0]`. That path is correct **only if the user hits `/`**. ReturnTo to `/o/{stale}/…` is not.

### 11. Staff email vs sub in the sidebar

`staffDisplay` (`staffDisplay.ts:12-27`) prefers whoami email/name, then OIDC profile email/name, then email local-part. `usable()` rejects empty and **pure numeric** strings (`/^\d+$/`). Zitadel numeric `sub` (whoami `user_id` like `387725576103826436`) never becomes the label. Fallback name is `"Signed in"`. Tests lock all three cases.

`DashboardChrome` spreads `staffDisplay(who, auth.user)` into the sidebar user and sets `roleLabel: tenant.role ?? 'member'`. `user-menu.tsx` shows `user.name` on the rail and `user.email` in the account dropdown (`"—"` if missing). Numeric sub is not rendered.

### 12. Register SPA vs One apps

`register-spa.sh` POSTs One `/tenants/$TENANT_ID/apps` with `{ name, type: "spa", redirect_uris, post_logout_redirect_uris }`. That **is** One's recipe (`register-oidc-app.md` curl). It fails if the 201 includes `client_secret` (confidential leak). Optional `WRITE_ENV=1` writes only `VITE_ZITADEL_CLIENT_ID` to gitignored `.env`. It does **not** register the `127.0.0.1:5178/callback` twin. One `Zitadel:UseStub=true` (Development default on One) returns a stub `client_id` that cannot complete login — documented on One, not on Pay's register script.

### 13. Create workspace vs invite

Create: `oneApi.createTenant` POSTs One `/tenants` with Ada Bearer (`oneApi.ts:6-24`). Caller becomes owner. Form sets `ORG_HINT_KEY` and navigates to `/o/{id}/overview`. IsolationTests forbids Pay `organizations` / `users` / `members` tables. **Create via One API from merchant exists.**

Invite: merchant `src` grep `invite` is **empty**. One `POST /tenants/{tenantId}/members/invite` exists (`MemberEndpoints.cs`, minRole admin). One `GET /me/invites` exists. Pay merchant has no Team page, no copy-link, no deep-link to `lazuar-app` accept. Checklist `o10-invite-copy-link.md` is ticked. Live files are authority: **invite from merchant is missing.**

### 14. Checkout remains anonymous

`lazuar-pay-checkout/package.json` has no `oidc-client-ts`, no `react-oidc-context`, no `@lazuar/one-client`. `main.tsx` is `StrictMode` + `App` — no `AuthProvider`. `App.tsx` talks only to `/v1/pay/{token}` and `/start`. `locks.test.ts` `has no OIDC dependency`. PublicPay handlers take no `OneClient` and call no `MemberGate`. Copy on the paid screen: “This page is not a membership login.” Buyers have no One account. **Confirmed.**

### 15. Machine key `lzr_sk_` (kernel gap, no cathedral)

Host `Bearer.TryGet` will forward `Bearer lzr_sk_…` to One. One `/me` for API keys (`GetMeForApiKey`) sets `user_id` to the **key id**, role `admin` if tenant-admin-equivalent scopes else `member`. Writer overlay would then use that synthetic role.

Merchant picker **rejects** `lzr_sk_` because it is not JWT-like. Correct for a human SPA.

There is **no** Pay mint UI, no `POST /v1/api-keys`, no second-app docs in the merchant, and **no** hermetic test that `Authorization: Bearer lzr_sk_…` on `/v1/whoami` maps Fake One 200. Checklist `o13-lzr-sk.md` claims that test. Grep `lzr_sk_` under `apps/lazuar-pay` is empty. Kernel door: a second app still cannot be a first-class Pay customer with a machine token **productized** here. Do not design a Pay key vault. One already mints `lzr_sk_`. Pay should keep forwarding.

### 16. Cookie vs Bearer SPA (summary)

| Surface | What it uses |
|---------|----------------|
| Merchant OIDC session | `sessionStorage` via `WebStorageStateStore` |
| Merchant → Pay | `Authorization: Bearer <JWT access_token>` |
| Merchant → One (`POST /tenants`) | same Bearer |
| Pay → One | forwarded `Authorization` + optional hint |
| Pay CORS | origins allowlist, **no** `AllowCredentials` |
| Pay host cookies | none |
| Checkout | no tokens at all |
| Hub `lazuar_auth` | not read, banned in merchant locks |

---

## Bugs

### B1. One HMAC header dialect does not match live One — suspend never pauses charges

Pay requires `X-Lazuar-Signature: t=<unix>,v1=<hex>` and ignores `X-Lazuar-Timestamp`. One sends `X-Lazuar-Signature: v1=<hex>` and `X-Lazuar-Timestamp: <unix>`. The signed payload algorithm (`{unix}.{body}`, HMAC-SHA256, lowercase hex, `whsec_…` UTF-8 key, 300s skew) is the same. The **packaging is not**.

Live One POST → Pay `TryParseHeader` fails (`t` null) → 401 Invalid HMAC → `OrgSettings.ChargesPaused` unchanged → `POST /v1/pay/{token}/start` does not 403 on pause.

Staff belt: One membership 403s `"Tenant is suspended."` before `authz/check`. Pay maps that 403 to `"Not a member of this org"`. Buyers never send Bearer. **The buyer belt is only `ChargesPaused`.** It is unwired on the real wire.

Tests **lock the wrong dialect** (`OneWebhookTests.Sign`) and correctly reject Hub body-only uppercase hex. Hermetic green does not mean One can pause a shop.

The file comment “Judgment stolen from One's signer” is a factual error. One's signer is `WebhookSigning.FormatHeader` = `"v1=" + hex`.

### B2. Single process secret `Pay:OneWebhookSecret` vs One per-tenant `whsec_`

Even after B1, One issues a unique secret per webhook endpoint. Pay verifies with one env var. Two workspaces cannot both deliver unless they somehow share a secret (they do not) or Pay stores N secrets (it does not). 014 already called this a one-shop hatch. Still a production bug for anything beyond dogfood of one tenant.

### B3. Stale `returnTo` / `setOrgHint` before membership

`CallbackPage` honors `lazuar-pay-merchant:returnTo` over `dashboardPath`. `OrgLayout` writes `ORG_HINT_KEY` for the URL org before `tenants.find`. A staff member who lost org A, still has org B, and logs in from a bookmarked `/o/A/overview` gets an error page, not B. Hint is now A (not a member). Next visit to `/` recovers via `dashboardPath` (hint not in list → `tenants[0]`). The login completion itself is wrong.

### B4. `GET /v1/checkouts/{id}` 404s before Bearer

Unknown id → 404 and **skips One** even with a Bearer (`Get_unknown_is_404`). Missing Bearer on a **known** id → 401 after the row lookup. Missing Bearer on unknown → 404. That is an existence oracle on checkout ids. Member check should run after Bearer, or 401 without looking up.

### B5. Opaque / JWE access token + `isAuthenticated` login loop

`RequireAuth` only checks `auth.isAuthenticated`. `pickApiBearerToken` returns undefined for opaque/JWE. `HomePage` then `auth.signinRedirect()`. One's API-provisioned SPA apps request JWT access tokens, so dogfood of a **new** `type=spa` app is fine. An old opaque Zitadel app, or a mis-provisioned client, livelocks. Do not “heal” by sending `id_token` (M21). Fail the session instead.

### B6. Invalid JSON on Plane A after a (test-dialect) HMAC success is an unhandled 500

`JsonDocument.Parse` is not in try/catch (`OneWebhookEndpoints.cs:36`). Empty body is coerced to `"{}"` only after verify. Empty body **with** a valid HMAC of empty bytes would 200 `{ ok: true }` and apply nothing. Checklist O15.2 claimed empty body → 4xx. Missing signature is 401 (4xx). Empty **signed** body is 200. Parse of garbage is 500.

### B7. One 400 / 429 on `authz/check` become Pay 503

`MemberGate` maps anything other than 401/403/200 to 503. Live One 400s non-GUID `object.id`. Rate limit 429 becomes 503. Operators will chase “identity provider failed” for a bad URL org id.

### B8. Suspended tenant copy

One 403 on suspend is mapped to `"Not a member of this org"`. The human still **is** a member; the tenant is suspended. Overview still renders if `/me` lists `status: "suspended"` because `OrgLayout` only checks `tenants.find`, not `status`. Subsequent money GETs 403 with the wrong sentence.

### B9. CORS tests do not prove `/v1/pay/*` or OPTIONS (K14 ticked anyway)

`CorsTests` only `GET /health` with `Origin`. Default policy applies to all routes **if** `UseCors()` runs, so 5179 would likely work. K14.4 says tests cover public pay GET/POST/OPTIONS. They do not. `CorsTests` also use bare `WebApplicationFactory<Program>()` (Development, real `AddDbContext` + `MigrateAsync`), not `PayApiFactory` Testing. CORS assertions can fail for database reasons.

### B10. Production CORS is localhost forever

`Program.cs:58-72` hardcodes laptop origins. There is no `Pay:CorsOrigins`. Staging/prod merchant HTTPS origin will get **no** `Access-Control-Allow-Origin`. One's rule (empty CORS fails boot) is the opposite of Pay's silent hardcoded list. Merchant `POST /tenants` depends on **One** CORS (`:5178` is on One Development CSV as of One's `fix(api): allow Pay merchant :5178 as a CORS origin`). Pay whoami/money depends on **Pay** CORS. Two allowlists. Pay's cannot be changed without a code edit.

---

## Gaps

### G1. Writer is `/me` role, not `authz/check` `admin`

NP-ONE-015 wanted `member` / `admin` / `owner` checks on the same façade. The host always posts `relation: "member"`. Writer then parses `/me.tenants[].role`. That works when `/me` and FGA agree. It doubles One RTTs. It ignores One custom-role overlays (`MeEndpoints` `Permissions` are dropped by `OneMeMapper`). A `member` with a custom permission that One would treat as `can_manage_tenant` still cannot write — fail closed, probably right for Pay money. An `admin` who is briefly missing from `/me.tenants` but still FGA-admin cannot write — fail closed, noisy.

The smaller fix is `CheckMemberAsync`-shaped `CheckWriterAsync` with `relation: "admin"` (FGA `admin` includes `owner`). Keep `/me` for chrome only.

### G2. `/v1/orgs/{orgId}/ready` is a dummy and unused

After member, `ready: true` always. Does not mean vault, products, or charges enabled. SPA never calls it. Leaving it is fine as a connection probe **if docs say dummy**. Do not teach integrators that `ready` means “can take money.”

### G3. Invite from merchant is not implemented

Create workspace via One `POST /tenants` is real. Invite via One `POST /tenants/{id}/members/invite` is not called. No copy-link. No `GET /me/invites`. Second engineer today: use `lazuar-app`. O10 tick is checklist rot.

### G4. `lzr_sk_` is a kernel gap, not a missing cathedral

Pay forwards Bearer. One mints keys. Merchant must not put `lzr_sk_` in `VITE_*`. Missing: one hermetic whoami test with `Bearer lzr_sk_test`, Fake One 200; a sentence in Pay README that second apps mint on One and call Pay with that Bearer. Do **not** add a Pay API-key table.

### G5. Last workspace is per-tab hint, not `/me` order or `active_org_id`

New tab after login → `tenants[0]` alphabetically (One `OrderBy Name`). Same tab → sessionStorage hint if still a member. `HomePage` ignores `active_org_id`. `whoami.active_org_id` is only set when a hint is forwarded, which HomePage does not.

### G6. `register-spa.sh` twins and audience

Script registers `http://localhost:5178/callback` only. README tells humans to add `127.0.0.1` to One `REDIRECT_ALLOWLIST` if they use that host; the script does not. `oidcConfig` has no Zitadel audience pin. Local One may not require `Zitadel:Audience`. Staging/prod One that does will 401 whoami with a syntactically JWT access token. One recipe already documents `urn:zitadel:iam:org:project:id:{id}:aud`.

### G7. Plane A catalog / registration / SSRF / `tenant.deleted`

No auto-register. No pull `GET /tenants/{id}/events` fallback (and pull 403s after suspend anyway). `tenant.deleted` does not pause. `tenant.reactivated` without a prior settings row is a no-op (harmless if never paused). Unique `DeliveryId` insert is check-then-add (race → 500 not `{ duplicate: true }`). Concurrent One retries can 500.

### G8. SPA membership chrome is `/me`, not `authz/check`

`OrgLayout` trusts `who.tenants.find`. Money routes re-check FGA. Domain auto-join / SSO JIT **do** show up on next `/me` (One runs join inside `GetMe`) but **do not** emit `member.accepted`. A webhook-only staff cache would miss them. Pay does not cache membership in DB (good). Chrome can still show a workspace that `authz` will 403 (stale tab) or hide one that FGA would allow until refresh.

### G9. TypeSpec `WhoamiResponse` omits `name`

Host and merchant both have `name`. Spec does not. Honesty gap only.

### G10. `IsPlatformAdmin` is forwarded and unused

Do not later treat it as Pay superuser. Lazuar staff support is One admin (`:5173`), not a Pay backdoor.

### G11. `authz/batch-check` never called

NP-ONE-016 is chrome later. Current chrome uses one `/me`. Fine for v1.

### G12. Member GET payments/receipts has no dedicated member-token test

Code path is `RequireMemberAsync`. `PaymentQueryTests` uses `PayTest.Owner`. `GatewayTests.Member_can_get_gateway_metadata` is the only member-read proof. `o12-member-sees-ops.md` wanted fake One member on GET payments/receipts.

### G13. `POST /v1/payment-links` has no `Member_cannot_create` test

The handler is writer-gated. Checkout and catalog have member-cannot tests. Payment links do not.

### G14. Admin-as-writer is untested (only `owner` and `member`)

`canWriteMoney` and `RequireWriterAsync` both accept `admin`. Tests never send `"role":"admin"`.

### G15. Whoami never asserts `lzr_sk_` or One 403

`Whoami_maps_one_401` exists. The 403 mapper (`WhoamiEndpoints.cs:40`) is untested. O13's machine-key test is untested.

### G16. Checklist rot (ticks vs live)

| Checklist | Tick | Live |
|-----------|------|------|
| O13 Fake One `lzr_sk_` whoami | x | no test, no `lzr_sk_` string in host tests |
| O10 invite copy-link on `:5178` | x | no invite code |
| O15 empty body 4xx | x | signed empty → 200 `{}` |
| O15 “judgment stolen from One” | implied by O14/O15 | verifier is Standard Webhooks combined header, not One |
| K14 OPTIONS + `/v1/pay` CORS | x | only `GET /health` |
| M19 refresh whoami after create | x | navigate; `OrgLayout` fetches; no explicit refresh on the form |

---

## Tests vs missing

### Present and honest

| Test | What it actually proves |
|------|-------------------------|
| `Whoami_maps_org_id_from_one_me` | `/me` → `active_org_id`, email, name; One called once with `Bearer tok` |
| `Whoami_allows_empty_tenants` | empty Ada can 200 |
| `Whoami_without_authorization_is_401_and_skips_one` | no leak to One |
| `Whoami_maps_one_401` / timeout / 500 | fail closed |
| `Ready_when_one_allows_member` | dummy `ready: true`; body is `relation=member` `type=tenant` |
| `Ready_forbidden_when_allowed_false` / One 403 | 403 |
| `Ready_401_without_bearer_skips_one` | |
| `Ready_checks_path_org_not_header` | path SoT |
| `Member_cannot_put_gateway` / `Member_can_get_gateway_metadata` | writer vs member on keys |
| `Member_cannot_create_product` | writer on catalog |
| `Member_cannot_create_checkout` | writer on `POST /v1/checkouts` |
| `Create_for_other_org_is_403` / `List_other_org_is_403` | path org |
| `pickApiBearerToken` never `id_token` | JWT access only; opaque/JWE no fallback |
| `staffDisplay` numeric sub | `"Signed in"`, not Zitadel sub |
| `locks.test.ts` merchant | no password, no Hub login, no `lazuar_auth` |
| checkout `has no OIDC dependency` | anonymity |
| `CorsTests` 5178/5179/4179 allow, 3003/3004 deny on **`/health`** | Q15 subset |
| `OneWebhookTests` pause/reactivate/replay/stale/missing secret | **only** for `t=,v1=` Pay dialect |
| `Body_only_uppercase_hex_is_401` | Hub museum rejected |
| `Start_paused_is_403_even_with_stored_url` | buyer belt **if flag is set** |
| Isolation no `Modules.One`, no org/user tables | |

### Missing (should exist before claiming the loop closed)

1. **One live HMAC shape:** `X-Lazuar-Signature: v1=<hex>` + `X-Lazuar-Timestamp` + HMAC over `{unix}.{body}` → 200 and `ChargesPaused`.
2. Same body, Hub uppercase hex of body only → still 401 (keep).
3. Combined `t=,v1=` should either stay as a compatibility alias with an explicit test, or **die** when Pay matches One. Do not keep green tests that One cannot satisfy.
4. `Authorization: Bearer lzr_sk_test` whoami → Fake One 200 (O13).
5. `Member_cannot_create` on `POST /v1/payment-links`.
6. Member token `GET /v1/orgs/t1/payments` and receipts 200; PUT gateway 403 (O12 hermetic two-token).
7. Writer role `admin` (not only `owner`).
8. CORS `GET`/`POST`/`OPTIONS` on `/v1/pay/{token}` Origin 5179; deny 3003/3004 on that path; 127.0.0.1:5178 twin on `/v1/whoami` if you care about the twin.
9. `CorsTests` on `PayApiFactory` Testing so they do not migrate Postgres.
10. Whoami One 403 mapper.
11. `RequireAuth` / HomePage: authenticated + non-JWT access does not `signinRedirect` forever (assert picker undefined → error, not loop).
12. `dashboardPath` unit tests (locks only grep the file). Stale hint not in list → first tenant. Empty → `/workspaces/new`.
13. Empty One webhook body → 4xx if you keep O15's sentence.
14. Invalid JSON after valid HMAC → 4xx not 500.

---

## Ranked findings

1. **P0 — HMAC dialect (B1).** Live One cannot pause charges. Tests hide it. Fix verifier + tests before any live tenant.suspend dogfood.
2. **P0 — Pause is the buyer belt (B1 consequence).** Staff 403 on suspend is not enough. PublicPay only reads `ChargesPaused`.
3. **P1 — One process `whsec` (B2) + no register (G7).** Dialect fix alone does not make multi-tenant Plane A true.
4. **P1 — Writer via `/me` (G1).** Works for owner/member tests; weaker than `authz/check admin`; extra One hop.
5. **P1 — Stale org after login (B3, G5).** Deep link / hint-before-membership.
6. **P2 — Invite missing (G3)** vs O10 tick. Create workspace is real.
7. **P2 — `lzr_sk_` kernel (G4)** vs O13 tick. Forwarding exists. Productization does not. Do not cathedral.
8. **P2 — CORS production + OPTIONS honesty (B9, B10, G16).** Local 5178/5179 vs 3003/3004 is real on `/health`. Not a production allowlist. Not `/v1/pay` OPTIONS.
9. **P2 — Checkout id existence oracle (B4).**
10. **P2 — Opaque token loop (B5).** Unlikely for API-provisioned JWT SPAs; nasty if it happens.
11. **P3 — Dummy ready (G2), spec `name` (G9), 400→503 (B7), suspend copy (B8), payment-link member test (G13), admin writer test (G14).**
12. **Confirmed good:** no PAT; no checkout OIDC; picker never `id_token`; sessionStorage not cookies; no `AllowCredentials`; no VIEWER; POST checkout / PUT gateway / POST payment-links writer; GET lists member; staffDisplay not numeric sub; `register-spa.sh` speaks One apps; Isolation no `Modules/One`.

---

## How to solve

Do not implement from this paper. When a later slice does:

### S1. Match One's HMAC (B1) — this unblocks pause

Change `OneWebhookSignature.TryVerify` to:

1. Read `X-Lazuar-Signature`. Accept `v1=<hex>` (One `FormatHeader`). Reject raw uppercase hex of the body (keep `Body_only_uppercase_hex_is_401`).
2. Read `X-Lazuar-Timestamp` as unix **seconds**. Reject missing / non-integer / `abs(now - ts) > 300`.
3. HMAC the **raw** body bytes as `{timestamp}.{body}` with UTF-8 `Pay:OneWebhookSecret` (full `whsec_…` string, same as One).
4. Constant-time compare to the hex (One's `FixedTimeEqualsHex` already accepts `v1=` prefix or bare hex).

**Delete or relegate** the combined `t=,v1=` parser unless you explicitly want a dual dialect. Dual dialect is how Hub vs One got confused. Prefer One-only.

Rewrite `OneWebhookTests.Sign` to set both headers the way `WebhookDispatcher` does. Add a test that a Standard Webhooks combined header **401s** if you drop compatibility, or 200s if you keep it — say so in the test name.

Use envelope `id` **or** `X-Lazuar-Event-Id` as the unique key (they are the same guid). Prefer the header with body `id` fallback so a truncated body does not mint a random key (`Guid.NewGuid()` today when `id` missing).

Wrap `JsonDocument.Parse` in try/catch → 400. Empty body after successful verify → 400, not 200 `{}`.

Do **not** import Hub `OutboundWebhookSignature`. Do **not** copy One's `WebhookSigning.cs` into Pay as a project reference. Copy the **algorithm**, six lines, Pay-owned.

### S2. Pause charges once verify works (B1 + O16)

Keep the existing `tenant.suspended` / `tenant.reactivated` handlers. They are correct **given a verified body** with `tenant_id`. Add a hermetic test that uses **only** `tenant_id` (already have `Valid_tenant_id_field_sets_charges_paused`) **with One's headers**. PublicPay `Start_paused_is_403` already proves the buyer belt once the flag is on.

Map One membership 403 detail that contains `suspended` to `"Org is suspended"` instead of `"Not a member of this org"` (B8). Optional chrome: `OrgLayout` if `tenant.status === 'suspended'` show that, don't pretend membership is missing.

`tenant.deleted`: set pause (and stop mint) the same as suspend. Do not CASCADE money rows.

### S3. One secret and registration (B2, G7) — hatch, not cathedral

Document the hatch: one `Pay__OneWebhookSecret` is **one** One endpoint, **one** tenant, dogfood only. Register that URL on One with an owner JWT (`POST /tenants/{id}/webhooks`, events at least `tenant.suspended`, `tenant.reactivated`, `webhook.test`). Store the shown-once `whsec_…` in Pay env, never `VITE_*`.

For N merchants later: persist per-`org_id` HMAC secrets Pay received at register, or terminate TLS at a single first-party endpoint that One calls with **one** platform webhook if One ever grows that product. **Do not design that vault in this paper.** Until then, saying “Pay consumes One webhooks” is only true for one shop.

Laptop push needs One `Webhooks:UrlHostAllowlist`. That is One ops.

### S4. Writer = `authz/check` `admin` (G1)

Add `CheckWriterAsync` posting `relation: "admin"`, `object.type: "tenant"`, `object.id: orgId`. `RequireWriterAsync` should not call `/me`. Keep `/me` for whoami and sidebar. Tests: Fake One returns `allowed: true` for admin check on owner and admin; `allowed: false` on member even if `/me` were to lie. That makes `/me` non-authorizing.

Until then, the current overlay is **fail-closed** for members (proven). It is the wrong SoT.

### S5. Last workspace / stale org (B3, G5)

- `OrgLayout`: call `setOrgHint` **only after** `tenants.find` succeeds.
- `CallbackPage`: if `returnTo` is `/o/{id}/…` and whoami (or a cheap membership test) does not contain `id`, ignore returnTo and go `dashboardPath`.
- `HomePage`: already uses `dashboardPath`; keep it as the `/` behavior.
- Do not treat `ORG_HINT_KEY` as authz (already true on the host).
- Optional: prefer `who.active_org_id` if still a member, then hint, then `tenants[0]`. Requires forwarding the hint or teaching One another default. Not required for v1 if deep-link stale is fixed.

### S6. Cookie vs Bearer — keep (no change)

Do not add `AllowCredentials`. Do not invent a Pay session cookie. Do not read `document.cookie`. `pickApiBearerToken` stays JWT access only. If `access_token` is opaque, show a hard error (“this app must issue JWT access tokens; re-register `type=spa` via One”) rather than `signinRedirect` loop (B5). `RequireAuth` should treat “authenticated but picker undefined” as that error.

### S7. CORS (B9, B10)

Keep the deny of `3003`/`3004`/`5173`/`3005`. Add hermetic tests on `/v1/pay/{token}` OPTIONS/GET/POST Origin 5179 and deny 3003/3004 **using `PayApiFactory`**. For production, bind origins from config (`Pay:CorsOrigins`) with a Development default equal to today's list; empty in Staging/Production should fail boot (copy One's honesty, do not copy One's code). Never add ops.

### S8. Register SPA (G6)

Keep One `POST /tenants/{id}/apps` `type=spa`. Optionally append `127.0.0.1` twins when a flag is set. Document `Zitadel:UseStub=false` and audience scope in `apps/lazuar-pay-merchant/README.md` next to the script. Do not put PAT in the script.

### S9. Invite (G3)

Do **not** add `POST /v1/invites` or a Pay members table. Either:

- Deep-link `lazuar-app` invite UI, or
- `fetch` One `POST /tenants/{orgId}/members/invite` with the staff JWT from the merchant, role `member` only, and copy the returned accept URL.

Owner/admin chrome only (`canWriteMoney` or a tighter `canInvite`). Accept stays on One.

### S10. `lzr_sk_` (G4) — document, one test, stop

Add `Whoami_forwards_machine_key_shape` : `Authorization: Bearer lzr_sk_test`, Fake One 200 `/me`, Pay 200. README: mint on One with explicit scopes; Pay forwards; merchant SPA never sends this. No Pay key table. No Stripe `sk_` as One key.

### S11. Dummy ready (G2)

Leave the route as a connection probe **or** make `ready` mean something small and true (e.g. `charges_paused` inverted, still not “vault configured”). If left dummy, pay-spec comment already says dummy; merchant should not grow a dependency on it.

### S12. Checkout GET auth order (B4)

`Bearer.TryGet` 401 first on `GET /v1/checkouts/{id}`, then lookup, then member. 404 only for members of that org (or 404 for everyone after Bearer to avoid cross-org existence — pick one and test it). Public pay stays token-in-URL, no Bearer.

### S13. Status mapping (B7, B8)

Pass through One 400 as 400 when `authz/check` validation fails. Pass through 429. Keep 5xx/timeout as 503. Distinguish suspend 403.

### S14. Tests to add (see missing list)

Especially S1's One-header test, payment-link member 403, member GET payments, `admin` writer, CORS on `/v1/pay`, `lzr_sk_` whoami.

---

## Refuse

- Copy `apps/lazuar-api/Modules/One` or `namespace Lazuar.Pay.One`.
- Hold `ZITADEL_PAT`, login-client PAT, OpenFGA admin, or One `Webhooks:SigningSecretEncryptionKey`.
- Parse `urn:zitadel:iam:org:project:roles`.
- Invent Pay `VIEWER`. One tenant roles are `owner` / `admin` / `member`. App-type `viewer` is not a Pay money role.
- OIDC / whoami / `RequireAuth` on `:5179`.
- Cookie session on Pay (`lazuar_auth`, `AllowCredentials`, port-unscoped localhost cookies).
- Add `:3003`, `:3004`, `:5173`, `:3005` to Pay CORS “temporarily.”
- Verify One webhooks with Hub body-only uppercase hex, or with `Pay:StripeWebhookSecret`.
- Dual-use Plane A and Plane B tables or routes.
- Pay `organizations` / `users` / `members` / homemade `sk_*` tables.
- `POST /platform/tenants` from merchant.
- Treat `is_platform_admin` as Pay superuser.
- Heal opaque access tokens by sending `id_token`.
- Design a multi-tenant HMAC secret cathedral or a Pay-side `lzr_sk_` mint in this slice. Hatch + forward.
- Tail Zitadel events instead of One's catalog.

---

## Appendix: quoted evidence

### A. CORS hardcoded; no credentials; 5178/5179; not 3003

```58:72:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
        p.WithOrigins(
                "http://localhost:5178",
                "http://127.0.0.1:5178",
                "http://localhost:5179",
                "http://127.0.0.1:5179",
                "http://localhost:4178",
                "http://127.0.0.1:4178",
                "http://localhost:4179",
                "http://127.0.0.1:4179")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
```

`CorsTests.cs:8-18` allows 5178 on `/health`. `CorsTests.cs:45-54` denies 3003. `CorsTests.cs:57-66` denies 3004. No `/v1/pay` OPTIONS test in that file.

### B. Whoami forwards Bearer to One `/me`

```13:22:apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiEndpoints.cs
    static async Task<IResult> Handle(HttpRequest request, OneClient one, CancellationToken cancellationToken)
    {
        if (!Bearer.TryGet(request, out var authorization))
        {
            return PayErrors.Status(401, "Unauthorized", "Missing bearer token");
        }

        request.Headers.TryGetValue("X-Lazuar-Tenant-Id", out var hint);
        var result = await one.GetWhoamiAsync(authorization, hint.ToString(), cancellationToken);
        return Map(result);
    }
```

```35:45:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs
    internal async Task<OneCallResult<WhoamiResponse>> GetWhoamiAsync(
        string authorization,
        string? tenantHint,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "me");
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        if (!string.IsNullOrWhiteSpace(tenantHint))
        {
            request.Headers.TryAddWithoutValidation("X-Lazuar-Tenant-Id", tenantHint);
        }
```

### C. Member = authz `member`; writer = `/me` role overlay

```24:27:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs
        request.Headers.TryGetValue("X-Lazuar-Tenant-Id", out var hint);
        var result = await one.CheckMemberAsync(authorization, orgId, hint.ToString(), cancellationToken);
        if (result.StatusCode == 200 && result.Value)
        {
            return null;
```

```45:71:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs
    public static async Task<IResult?> RequireWriterAsync(
        HttpRequest request,
        OneClient one,
        string orgId,
        CancellationToken cancellationToken)
    {
        var denied = await RequireMemberAsync(request, one, orgId, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        Bearer.TryGet(request, out var authorization);
        request.Headers.TryGetValue("X-Lazuar-Tenant-Id", out var hint);
        var who = await one.GetWhoamiAsync(authorization, hint.ToString(), cancellationToken);
        if (who.Value is null)
        {
            return PayErrors.Status(503, "Service Unavailable", "Identity provider failed");
        }

        var role = who.Value.Tenants.FirstOrDefault(t => t.Id == orgId)?.Role;
        if (role is not ("owner" or "admin"))
        {
            return PayErrors.Status(403, "Forbidden", "Writer role required");
        }

        return null;
    }
```

```84:90:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs
        request.Content = JsonContent.Create(
            new OneAuthzCheckRequest
            {
                Relation = "member",
                Object = new OneAuthzObject { Type = "tenant", Id = orgId }
            },
            options: Json);
```

FGA (One): `admin: [user] or owner`; `member: [user] or admin`. Tenant relations allow-list includes `owner`, `admin`, `member` — not `viewer`. App type has `viewer`.

### D. Dummy ready:true after member

```19:25:apps/lazuar-pay/src/Lazuar.Pay/Identity/OrgReadyEndpoints.cs
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        return Results.Json(new OrgReadyResponse { OrgId = orgId, Ready = true }, OneClient.Json);
```

### E. Writer/member on mint vs lists vs public pay

```29:29:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs
        var denied = await MemberGate.RequireWriterAsync(request, one, orgId ?? "", cancellationToken);
```

```27:27:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs
        var denied = await MemberGate.RequireWriterAsync(request, one, orgId ?? "", cancellationToken);
```

```30:30:apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs
        var denied = await MemberGate.RequireWriterAsync(request, one, orgId, ct);
```

```152:152:apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId, ct);
```

```27:32:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
    static async Task<IResult> Get(
        string token,
        string? slot_key,
        CheckoutStore store,
        PayDbContext db,
        CancellationToken ct)
```

No `OneClient` / `MemberGate` on that Get. Start pause:

```121:125:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
            var settings = await db.OrgSettings.FindAsync([session.OrgId], ct);
            if (settings?.ChargesPaused == true)
            {
                return PayErrors.Status(403, "Forbidden", "Org charges are paused");
            }
```

### F. Pay HMAC vs One HMAC

Pay parser requires `t=` **in the signature header**:

```45:79:apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs
    internal static bool TryParseHeader(string headerValue, out long timestamp, out string v1Hex)
    {
        timestamp = 0;
        v1Hex = string.Empty;
        long? t = null;
        string? v1 = null;
        foreach (var part in headerValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // ...
            if (key.Equals("t", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTs))
            {
                t = parsedTs;
            }
            else if (key.Equals("v1", StringComparison.OrdinalIgnoreCase))
            {
                v1 = value;
            }
        }

        if (t is null || string.IsNullOrEmpty(v1))
        {
            return false;
        }
```

One signer:

```7:26:lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Webhooks/WebhookSigning.cs
/// HMAC-SHA256 webhook signatures.
/// signed_payload = "{unix_seconds}.{raw_body}"; header = "v1=" + hex(digest).
public static string FormatHeader(string hexDigest) => "v1=" + hexDigest;
```

One dispatcher (`WebhookDispatcher.cs:136-141`) sets `X-Lazuar-Event-Id`, `X-Lazuar-Event-Type`, `X-Lazuar-Tenant-Id`, `X-Lazuar-Timestamp`, `X-Lazuar-Signature`, `X-Lazuar-Delivery-Id`.

Pay test signer (wrong packaging vs One):

```13:16:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs
    static string Sign(string body, long unix)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes($"{unix}.{body}"));
        return $"t={unix},v1={Convert.ToHexString(mac).ToLowerInvariant()}";
    }
```

Hub museum reject (correct):

```55:68:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs
    public async Task Body_only_uppercase_hex_is_401()
    {
        // ...
        var hex = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(body)));
        // ...
        req.Headers.TryAddWithoutValidation("X-Lazuar-Signature", hex);
        // ...
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
```

Pause handler (never reached on live One headers):

```53:63:apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs
        if (type == "tenant.suspended" && !string.IsNullOrWhiteSpace(orgId))
        {
            var settings = await db.OrgSettings.FindAsync([orgId], ct);
            if (settings is null)
            {
                db.OrgSettings.Add(new OrgSettingsRow { OrgId = orgId, ChargesPaused = true });
            }
            else
            {
                settings.ChargesPaused = true;
            }
        }
```

### G. Picker never `id_token`; sessionStorage; no cookies on fetch

```10:18:apps/lazuar-pay-merchant/src/auth/bearerToken.ts
/**
 * Pick a Bearer token for Pay / One APIs.
 * Send only a JWT access_token. Never send id_token (not an API credential).
 */
export function pickApiBearerToken(user: User | null | undefined): string | undefined {
  if (!user) return undefined
  if (isJwtLike(user.access_token)) return user.access_token
  return undefined
}
```

```29:31:apps/lazuar-pay-merchant/src/auth/oidcConfig.ts
    response_type: 'code',
    automaticSilentRenew: true,
    userStore: new WebStorageStateStore({ store: window.sessionStorage }),
```

```20:32:apps/lazuar-pay-merchant/src/lib/payApi.ts
/** credentials omitted on purpose: localhost cookies are not port-scoped. */
export async function getWhoami(
  accessToken: string,
  orgHint?: string | null,
): Promise<Whoami> {
  const headers: Record<string, string> = {
    Authorization: `Bearer ${accessToken}`,
    Accept: 'application/json',
  }
```

### H. Last workspace; stale hint; staff display

```4:9:apps/lazuar-pay-merchant/src/lib/homePath.ts
/** Last used org if still a member, else first tenant. Empty → create workspace. */
export function dashboardPath(tenants: WhoamiTenant[]): string {
  if (tenants.length === 0) return '/workspaces/new'
  const hint = getOrgHint()
  const match = hint ? tenants.find((t) => t.id === hint) : undefined
  return `/o/${(match ?? tenants[0]).id}/overview`
```

```38:46:apps/lazuar-pay-merchant/src/layout/OrgLayout.tsx
  useEffect(() => {
    setOrgHint(orgId)
    if (!token) return
    getWhoami(token, orgId)
      .then((body) => {
        setWho(body)
        const match = body.tenants.find((t) => t.id === orgId) ?? null
        setTenant(match)
        setError(match ? null : 'Not a member of this org')
```

```11:26:apps/lazuar-pay-merchant/src/lib/staffDisplay.ts
/** Sidebar label. Prefer email/name from whoami or OIDC profile. Never a Zitadel numeric sub. */
export function staffDisplay(
  who: Whoami,
  user?: User | null,
): { name: string; email: string | null } {
  // usable() rejects /^\d+$/
```

### I. Register SPA via One apps, never PAT

```1:8:apps/lazuar-pay-merchant/scripts/register-spa.sh
# Register lazuar-pay-merchant as a public OIDC SPA via One apps API.
# Not Zitadel Console. Pay never holds ZITADEL_PAT — use Ada's access_token.
```

```36:40:apps/lazuar-pay-merchant/scripts/register-spa.sh
body="$(jq -n \
  --arg name "$NAME" \
  --arg redir "$REDIRECT_URI" \
  --arg post "$POST_LOGOUT_URI" \
  '{name:$name, type:"spa", redirect_uris:[$redir], post_logout_redirect_uris:[$post]}')"
```

### J. Checkout has no OIDC

`apps/lazuar-pay-checkout/package.json` dependencies: react, radix slot, cva, clsx, lucide, tailwind-merge. No `oidc-client-ts`. `locks.test.ts:9-14` asserts the package file does not contain it. `App.tsx` fetches `${payApi}/v1/pay/${token}` only.

### K. Create tenant from merchant; no invite

```6:18:apps/lazuar-pay-merchant/src/lib/oneApi.ts
export async function createTenant(
  accessToken: string,
  name: string,
  slug: string,
): Promise<{ id: string; slug: string; name: string }> {
  const response = await fetch(`${oneApi.replace(/\/$/, '')}/tenants`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${accessToken}`,
      'Content-Type': 'application/json',
      Accept: 'application/json',
    },
    body: JSON.stringify({ name, slug }),
  })
```

Merchant `src` contains no `invite` string.

### L. Isolation: no Modules/One, no org tables

```5:17:apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
        "MediatR", "Modules.One", "BuildingBlocks", ...
        "namespace Lazuar.Pay.Gateways",
        "namespace Lazuar.Pay.One;"
```

```49:58:apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
    public void Source_does_not_create_org_or_user_tables()
    {
        // forbids ToTable("organizations" | "users" | "members")
    }
```

### M. One `/me` for humans vs API keys (kernel note)

Human `GetMe` uses Zitadel `sub`, email, name, memberships ordered by tenant name. API key `GetMeForApiKey` sets `User_id = keyId` and role `admin` or `member` from scopes — Pay would treat that role as writer/member if a second app sent `lzr_sk_`. Merchant picker will not send it.

---

**End of paper 07.** Live files on `9f04ad58` (`feat/018-merchant-shell`). Sibling One signer checked on One `main`. Not an implementation.
