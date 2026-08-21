# 08 — One in production for Pay (SPA, membership, `lzr_sk_`, HMAC) without Pay becoming Zitadel ops

**Family:** 013-prods  
**Paper:** 08 — production identity between Pay and One  
**Date:** 21 August 2026  
**Type:** Analysis only. **Do not implement** from this file. **Do not** register a Pay SPA, **do not** add `lzr_sk_` to Pay env, **do not** add `POST /v1/one/webhooks`, **do not** flip `NP-ONE-001` / `NP-ONE-004` / `NP-ONE-011` / `NP-ONE-014` / `NP-ONE-017` / `NP-ONE-018`.  
**Parent program:** [`plans/013-prods`](./) — production-ready new Pay, then replace the old tree.  
**Binding siblings in this family:** [`01-production-ready-bar.md`](./01-production-ready-bar.md), [`03-host-production-seams.md`](./03-host-production-seams.md), [`04-merchant-frontend.md`](./04-merchant-frontend.md), [`05-checkout-frontend.md`](./05-checkout-frontend.md), [`06-money-rails.md`](./06-money-rails.md). This paper is the **identity plane** those papers hang off — not a second money paper, not a Hub-parity paper.  
**Binding 012 family (already written; this paper does not reopen them):** [`../012-one-to-pay/02-one-authn-tokens.md`](../012-one-to-pay/02-one-authn-tokens.md), [`06-tenant-org.md`](../012-one-to-pay/06-tenant-org.md), [`07-authz-roles.md`](../012-one-to-pay/07-authz-roles.md), [`08-machine-keys.md`](../012-one-to-pay/08-machine-keys.md), [`09-webhooks-events.md`](../012-one-to-pay/09-webhooks-events.md), parked checklists [`p10-spa-oidc.md`](../012-one-to-pay/checklists/p10-spa-oidc.md), [`p20-machine-key.md`](../012-one-to-pay/checklists/p20-machine-key.md), [`p30-one-webhooks.md`](../012-one-to-pay/checklists/p30-one-webhooks.md), [`p40-one-repo.md`](../012-one-to-pay/checklists/p40-one-repo.md), [`decisions.md`](../012-one-to-pay/checklists/decisions.md).  
**Binding 011:** [`../011-new-lazuar-pay/02-one-integration.md`](../011-new-lazuar-pay/02-one-integration.md) entire.  
**One first-party contract:** `/Users/akmalfirdaus/Code/lazuar/lazuar-one/plans/017-evals/08-dogfood-then-serve.md` §6.

---

## SHAs considered

Recorded at write time. Re-open files on a later SHA before treating a line as still true.

| Repo | Path | Branch | Full SHA | Short | Tip |
|------|------|--------|----------|-------|-----|
| **lazuar-pay** | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `feat/012-connect-one` | `6f866ff0489a4de77d2fc1b1bbcfa87fbe72b80f` | `6f866ff0` | `feat(pay): scaffold merchant and checkout Vite apps` (2026-08-21 15:15:51 +0800) |
| **lazuar-one** | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `main` | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` | `0f79fe4` | `WIP: Thu Aug 20 21:24:22 +08 2026` (2026-08-20 21:24:22 +0800) |

**Honesty lock (inherited, not re-proven here):** One staging proof is **NOT PASSED**. Packages `@lazuar/one-client` / `one-react` / `one-cli` are unpublished workspace packages. There is no public hosted SKU. Pay may import the workspace client later; this paper does not wait on npm. Source: `plans/011-new-lazuar-pay/02-one-integration.md` lines 5–6; One `plans/017-evals/08-dogfood-then-serve.md` header; refuse row **NP-XX-022**.

**C-phases already landed on this Pay SHA (connected, not S0 complete):** `GET /v1/whoami`, `GET /v1/orgs/{orgId}/ready`, in-memory `POST`/`GET /v1/checkouts`, CORS for `:5178` / `:5179`, health that never calls One. Parked: P10 SPA/OIDC, P20 `lzr_sk_`, P30 HMAC receiver. Evidence: `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`, `One/WhoamiEndpoints.cs`, `One/OrgReadyEndpoints.cs`, `One/MemberGate.cs`, `One/OneClient.cs`, `Checkouts/CheckoutEndpoints.cs`, `plans/012-one-to-pay/checklists/c99-connected-done.md`.

**What this paper is for.** Local whoami already works with Ada’s JWT. Production needs the **browser origin** registered as a Pay SPA, redirects, CORS, machine keys, One webhooks that stop charges on `tenant.suspended`, and invites that remain One copy-link. Pay must do all of that **without becoming Zitadel ops**: no PAT, no login-client PAT, no OpenFGA admin, no masterkey, no Console as the happy path, no second IdP.

---

## Binding answers (read this first)

These are the decisions this paper exists to keep from being “clarified” into a second identity system or a second Zitadel project.

| # | Decision | Lock |
|---|----------|------|
| 1 | **Pay is Consumer-0 HTTP.** Pay never holds Zitadel PAT, login PAT, OpenFGA admin, masterkey, One `ApiKeys:Pepper`, One `Webhooks:SigningSecretEncryptionKey`. | NP-ONE-020, NP-XX-017, 011/02 Secrets |
| 2 | **`access_token` as Bearer. Never `id_token`.** Same header for `lzr_sk_`. | NP-ONE-003, M2M-14, 012/02 |
| 3 | **Path `{orgId}` / `{tenantId}` is SoT.** `X-Lazuar-Tenant-Id` is a hint Pay may forward to One; it must not authorize. | NP-ONE-007, 012/06 §4, 012/07 §7 |
| 4 | **`GET /v1/whoami` is an endpoint, not middleware.** Health never calls One. `GET /me` can write (JIT); do not hammer. | NP-ONE-006, C13, C15 |
| 5 | **VIEWER is not a One role.** Product roles are `owner` \| `admin` \| `member`. FGA `viewer` is type `app`. Do not mark NP-ONE-021 done. | C24, 012/07 §10 |
| 6 | **One staging SMTP / Okta / SCIM is One’s program.** Do not block Pay (NP-XX-022). Copy-link is the invite path until MEM-10 is One’s. | NP-XX-022, 011/12 step 4 |
| 7 | **One repo product changes are rare (P40).** Seed script for Pay SPA is optional convenience. Prefer `POST /tenants/{id}/apps`. | P10.4, P40 |
| 8 | **Login is `:5175`.** Not `:3005` product path. Not `:5173` merchants. Not Pay’s homepage. Merchant Vite is `:5178`. Checkout Vite is `:5179` and has **no** One account. | NP-ONE-005, NP-XX-018, NP-CHK-007 |
| 9 | **Stop charges on `tenant.suspended` (NP-ONE-018) even if the webhook is late.** Money in Pay stays true. PSP (Plane B) must not wait on One. | 011/02 Events, 012/09 §6 |
| 10 | **Workspace create/pick = `POST /tenants` or membership list.** No Pay `organizations` table. One tenant UUID **is** Pay `org_id`. | NP-ONE-009, NP-XX-014, 012/06 |
| 11 | **Invites stay One copy-link.** Pay does not grow `POST /v1/invites`. Deep-link to `lazuar-app` `/invites/accept?tenant_id=&token=` or post the same One API. | NP-ONE-011, NP-ONE-012, 011/12 step 4 |
| 12 | **One Pay SPA `client_id` for the merchant origin.** Not a new OIDC app per merchant tenant. Workspace switch is `/me.tenants[]`. | this paper §3.2 |
| 13 | **Checkout origin never joins One CORS / login allowlist as a login client.** Buyers are the Pay plane. | NP-XX-013, this paper §3.7 |

If an implementation PR adds `ZITADEL_PAT` to `apps/lazuar-pay`, registers Pay only in Zitadel Console, or inserts `CREATE TABLE organizations`, that PR fails this paper.

---

## 1. Method / SHAs

Nothing was implemented from this write. The following were opened in full or in the cited ranges.

### 1.1 Pay plans (consumer intent)

- `plans/011-new-lazuar-pay/02-one-integration.md` — entire. Identity of Pay, AuthN do/do-not, session/path SoT, HTTP tables (tenancy / people / machines / authz), events, secrets, two planes.
- `plans/011-new-lazuar-pay/03-first-slice.md` — S0 steps 1–7 (SPA, `:5175`, `POST /tenants`, copy-link, `lzr_sk_`, webhooks, **stop**).
- `plans/011-new-lazuar-pay/11-checklist.md` — `NP-ONE-001`…`022`, `NP-XX-007`…`024`.
- `plans/011-new-lazuar-pay/12-first-slice-tracker.md` — ordered steps 1–7 still `todo` for SPA/invite/keys/webhooks; whoami/authz mapping is connected, not “S0 done.”
- `plans/012-one-to-pay/02-one-authn-tokens.md` — entire. Ports, token types, Ada flow, PKCE later, secrets table, SPA deferred until a browser origin exists.
- `plans/012-one-to-pay/06-tenant-org.md` — entire. One tenant id is `org_id`; no Pay org table; path vs header; create = `POST /tenants`.
- `plans/012-one-to-pay/07-authz-roles.md` — entire. `POST /tenants/{id}/authz/check`; VIEWER honesty; dummy `/ready`.
- `plans/012-one-to-pay/08-machine-keys.md` — entire. `lzr_sk_` mint/use/scopes; Mode U vs Mode M; never PAT.
- `plans/012-one-to-pay/09-webhooks-events.md` — entire. HMAC push; `tenant.suspended` money gate; pull fallback; do not tail Zitadel.
- `plans/012-one-to-pay/01-one-http-surface.md` — One `/api/v1` prefix, CORS note, first-party identity of Pay.
- `plans/012-one-to-pay/10-dogfood-and-tests.md` — connected vs S0 vs S1 bars.
- `plans/012-one-to-pay/checklists/{p10,p20,p30,p40,c13,c15,c24,c99,decisions}.md`.
- `plans/013-prods/README.md` — this slice’s assignment.

### 1.2 Pay runtime (what is actually mapped on `6f866ff0`)

- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` — CORS `:5178`/`:5179`, `MapWhoami`, `MapOrgReady`, `MapCheckouts`, health.
- `apps/lazuar-pay/src/Lazuar.Pay/One/{OneClient,MemberGate,WhoamiEndpoints,OrgReadyEndpoints,OneMeMapper,Bearer,OneOptions,PayErrors,OneAuthz,OneCallResult,WhoamiResponse,OneMeResponse}.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` — MemberGate on create/get.
- `apps/lazuar-pay/src/Lazuar.Pay/{appsettings.json,appsettings.Development.json,.env.example}` — `One:BaseUrl` / `One:TimeoutSeconds` only.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/{WhoamiTests,OrgReadyTests,HealthTests,CorsTests,CheckoutTests,IsolationTests,PayApiFactory}.cs`
- `apps/lazuar-pay-merchant/{README.md,src/App.tsx,package.json,.env.example}` — Vite **5178**, health probe only, **no OIDC**.
- `apps/lazuar-pay-checkout/{README.md,src/App.tsx,.env.example}` — Vite **5179**, buyers have no One account.
- `packages/pay-spec/main.tsp` — health, whoami, org ready, checkout fixture. No webhook route. No invite route.

### 1.3 One TypeSpec + runtime (producer, SHA `0f79fe4`)

- `packages/api-spec/modules/platform/{routes,models}.tsp` — `GET /me`, `GET /me/invites`; `POST /platform/tenants` is staff (Pay must not call).
- `packages/api-spec/modules/tenants/{routes,models}.tsp` — `POST /tenants`, `GET /tenants`, `GET|PATCH /tenants/{tenantId}`, suspend/reactivate/retry-provision/transfer/leave/delete, members, invites, events pull.
- `packages/api-spec/modules/apps/{routes,models}.tsp` — `POST /tenants/{tenantId}/apps`, list/get/rotate/delete.
- `packages/api-spec/modules/api-keys/{routes,models}.tsp` — `POST|GET /tenants/{tenantId}/api-keys`, `DELETE …/{keyId}`.
- `packages/api-spec/modules/authz/{routes,models}.tsp` — `POST /tenants/{tenantId}/authz/check|batch-check|list-objects`.
- `packages/api-spec/modules/webhooks/{routes,models}.tsp` — webhook CRUD, rotate, test, deliveries, `GET /webhook-event-types`.
- `apps/lazuar-api/src/Lazuar.One.Api/Features/Webhooks/WebhookEventCatalog.cs` — closed 17 types.
- `apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Zitadel/HttpZitadelAdminClient.cs` — API-created apps: PKCE public or confidential web, `OIDC_TOKEN_TYPE_JWT`, refresh grant, `devMode` from `Zitadel:OidcDevMode`.
- `scripts/seed-platform-spa-clients.sh` — **lazuar-app** (`:5174/callback`) + **lazuar-admin** (`:5173/callback`) only. Requires `ZITADEL_PAT`. Not Pay’s script.
- `apps/lazuar-login/.env.example` — `REDIRECT_ALLOWLIST` today: `5173,5174,5177,8085,5175`. **No 5178.**
- `apps/lazuar-api/src/Lazuar.One.Api/appsettings.json` + `appsettings.Development.json` — `App:CorsOrigins` today: `5173,5174,5177,5180,5181` (+ 3000/3001 in Development). **No 5178. No 5179.** Staging/Production empty CORS **fails boot**.
- `apps/lazuar-app/src/{auth/oidcConfig.ts,lib/inviteLink.ts,.env.example}` — PKCE, `offline_access`, sessionStorage, copy-link `{origin}/invites/accept?tenant_id=&token=`.
- `plans/017-evals/08-dogfood-then-serve.md` §6.1–6.12.

### 1.4 What “production” means in this paper (and what it does not)

**Production identity** is: a merchant human can sign in through **One’s** login host, land on **Pay’s** merchant origin, present a Zitadel **access_token** to Pay `:8081`, and Pay can ask One who they are and whether they `member` a path `{orgId}` — then, before live charges, Pay can hear `tenant.suspended` and refuse new money. It is **not**:

- One’s staging SMTP / Okta / SCIM proof (One’s program; NP-XX-022).
- A hosted One SKU.
- Pay validating JWKS itself (allowed later; not required; forwarding Bearer to One is the connected design).
- Hub `lazuar-ops` `:3003` pointed at 8081.
- Pay becoming a Zitadel Management client.

Local dogfood of whoami already used Ada’s **lazuar-app** (`:5174`) token against Pay `:8081`. That proves the **resource-server client** half. Production still needs Pay’s **own** `client_id` and origin on the allowlists, because merchants must not live inside `lazuar-app` and must not be sent to `:5173`.

---

## 2. What is already proven locally (whoami, org ready, Ada workspace)

C-phases closed **connected**, not S0. `c99-connected-done.md` says this out loud: SPA / OIDC / copy-link (P10), `lzr_sk_` (P20), One webhooks (P30) remain parked. 011/12 step 2 is **not** `done` just because whoami exists — step 2 includes `:5175` login **into Pay’s origin**.

### 2.1 Process map on this SHA

| Process | Port | What it does today | Identity status |
|---------|------|--------------------|-----------------|
| One API | **8080** | `/api/v1/me`, tenants, apps, keys, authz, webhooks | SoT. Pay’s `One:BaseUrl=http://localhost:8080/api/v1`. |
| Focused Pay | **8081** | `/health`, `/v1/health`, `/v1/whoami`, `/v1/orgs/{orgId}/ready`, fixture checkouts | Consumer. Forwards Bearer. Does not mint tokens. |
| lazuar-app | **5174** | First-party customer SPA. PKCE. Where Ada’s token is stolen for dogfood. | **Not** Pay’s merchant UI. |
| lazuar-login | **5175** | Password / MFA / register. Zitadel 302 target. | Product login. Not Pay homepage. |
| lazuar-admin | **5173** | Lazuar staff. | **Never** a merchant destination. |
| Stock Login V2 | **3005** | Break-glass. Collides with old Pay admin. | **Never** ship. |
| **lazuar-pay-merchant** | **5178** | Vite `strictPort`. Fetches Pay `/health` only. | Origin exists. **OIDC unwired.** |
| **lazuar-pay-checkout** | **5179** | Vite `strictPort`. Fetches Pay `/health` only. | Buyers. **No Zitadel.** |
| Old Hub API | 8080 | Collides with One. Cookie JWT. | **Off** while dogfooding One. |
| Old ops / portal | 3003 / 3004 | Hub cookie. | Do not retarget to 8081. |

Fingerprint One vs Hub: `GET http://localhost:8080/api/v1/` names `lazuar-one-api`. Both `/health` bodies can look like `{status:ok}`. Pay README already says this.

### 2.2 `GET /v1/whoami` — proven as an endpoint

Handler (`WhoamiEndpoints.Handle`):

1. Missing / non-Bearer `Authorization` → **401** `"Missing bearer token"` and **does not call One** (`Whoami_without_authorization_is_401_and_skips_one`).
2. Forwards the header **verbatim** (`Bearer.TryGet` keeps the `Bearer ` prefix) to One `GET {BaseUrl}/me`.
3. Forwards optional `X-Lazuar-Tenant-Id` as a hint. Does not authorize from it.
4. One 200 + `user_id` → Pay 200 projection (`OneMeMapper.ToWhoami`): `user_id`, `email`, `is_platform_admin`, `active_org_id` ← One `active_tenant_id`, `tenants[]` of `{ id, slug, name, role, status }`. `id` **is** `org_id`. Empty `tenants: []` is valid.
5. One 401 → Pay 401. One 403 → Pay 403. Timeout / transport / 5xx / unparseable body → Pay **503** `"Identity provider unreachable"` / `"Identity provider failed"`.
6. Calls `/me` **once** per whoami request. Not middleware. Not on `/health`.

`OneClient` default: `BaseUrl=http://localhost:8080/api/v1`, timeout **5s** (`OneOptions`; `appsettings.json`). Tests replace the typed client in `ConfigureTestServices` (`PayApiFactory`). `task pay:test` does **not** boot One.

Live dogfood (Pay README): Ada signs in on One (`:5175` via `:5174` today), copy **access_token** (three-segment JWT, not `id_token`), then:

```bash
curl -sS -H "Authorization: Bearer $ACCESS_TOKEN" http://localhost:8081/v1/whoami
```

That is Mode U (012/08 §7.1): Pay forwards the human token. There is **no** `ONE_API_KEY` / `One:ApiKey` in Pay env yet (`.env.example` is `One__BaseUrl` + `One__TimeoutSeconds` only).

### 2.3 `GET /v1/orgs/{orgId}/ready` — proven path SoT + `authz/check member`

`MemberGate.RequireMemberAsync`:

1. Missing Bearer → 401, skip One.
2. Empty `orgId` → 400.
3. `POST {One}/tenants/{orgId}/authz/check` with body **exactly**:

```json
{ "relation": "member", "object": { "type": "tenant", "id": "{orgId}" } }
```

No `user_id` (user JWT). No `type: payment` / `document`. No `relation: viewer`.

4. 200 `{allowed:true}` → continue (ready fixture `{ org_id, ready: true }`).
5. 200 `{allowed:false}` → Pay **403** `"Not a member of this org"`.
6. One 403 → Pay 403. One 401 → Pay 401. Timeout / transport / other → **503**.

`OrgReadyTests.Ready_checks_path_org_not_header`: path `path-org` + header `header-org` still checks **path**. Header is forwarded as hint only. This is NP-ONE-007 on the dummy.

Fixture checkouts reuse the same gate (`CheckoutEndpoints.Create` / `Get`). `org_id` on the session is the One tenant UUID. Cross-org get is 403. Health still skips One (`CheckoutTests.Health_still_skips_one`). Checkout is an in-memory `status: open` fixture — **not** a live charge, **not** proof of `tenant.suspended`.

### 2.4 Health never calls One — proven

`HealthTests.Health_does_not_call_one`: `ThrowOnSend = true` on the fake handler; `/health` and `/v1/health` still 200; `SendCount == 0`. C15 closed. Production probes must keep this: a down One must not fail liveness. Identity 503 belongs on **whoami / ready / merchant mutating routes**, not on the process.

### 2.5 CORS on Pay — proven for the two new origins, not for One

`Program.cs` default policy:

```text
http://localhost:5178
http://127.0.0.1:5178
http://localhost:5179
http://127.0.0.1:5179
```

`CorsTests`: merchant origin allowed; checkout origin allowed; old ops `http://localhost:3003` **not** allowed. `AllowAnyHeader` / `AllowAnyMethod`. **No credentials** (Pay is Bearer, not cookie). Production must replace this hardcoded Development list with an explicit env allowlist that **fails boot when empty in Staging/Production** (same idea as One `App:CorsOrigins`; old Hub already does this — copy the *fail-closed*, not the Hub origin set).

Pay CORS is **Pay’s** problem for browsers talking to **8081**. It does **not** put `:5178` on One. One CORS is a separate list (§3.6).

### 2.6 Ada workspace — proven as a **One** object, not a Pay row

There is no Pay `organizations` table on this SHA. Whoami tenants come from One `/me`. Checkout `org_id` is a string copy of that UUID. Ada creates/picks a workspace today in **lazuar-app** `:5174` (`POST /api/v1/tenants` on One). Pay’s merchant shell `:5178` does not yet call `POST /tenants` or render a switcher. That is P10 + NP-ONE-009 still `todo`, not a hole in the mapping law: when Pay grows the button, it is One’s POST, and the id in the URL is the same bytes.

### 2.7 What local proof does **not** cover (the rest of this paper)

| Proven | Not proven |
|--------|------------|
| Forward Ada’s **lazuar-app** access_token to whoami | Pay’s own `client_id` / `:5178/callback` |
| Path + `authz/check member` | SPA PKCE on `:5178` |
| Health One-free | Login allowlist includes `:5178` |
| Pay CORS 5178/5179 | One CORS includes `:5178` |
| Empty tenants 200 | Create workspace from Pay UI |
| Fixture checkout + MemberGate | Live charge stop on `tenant.suspended` |
| Hermetic fake One | `lzr_sk_` in Pay env |
| IsolationTests ban cathedral | HMAC receiver, invites in Pay chrome |

Using Ada’s **app** token against Pay is a valid **Consumer-0 HTTP** proof (012/02 §9 backend-only first). Shipping merchants that way would make `lazuar-app` Pay’s login shell. Production forbids that. Pay gets its own origin and its own public SPA client, still against the **same** Zitadel authority and the **same** `:5175` login host.

---

## 3. Production OIDC: `client_id`, redirects, allowlist, PKCE, token lifetimes, refresh, CORS on One **and** Pay

P10 is the parked checklist this section unrolls for production. Analysis already exists in 012/02 §4 and §8.2. The new facts since that paper: **the Pay browser origins now exist** (`lazuar-pay-merchant` `:5178`, `lazuar-pay-checkout` `:5179`). OIDC is still unwired (`App.tsx` health probe only; `package.json` has no `oidc-client-ts` / `react-oidc-context`).

### 3.1 Who is the login host vs authority vs API (do not collapse)

Copy 012/02 §1.5. It does not change in production; only hostnames do.

```text
Browser (Pay merchant :5178 / prod origin)
  --OIDC authorize-->  Zitadel authority (local :8085 / prod issuer)
                         |
                         | 302  /login?authRequest=V2_…
                         v
                    lazuar-login (:5175 / prod login host)
                         |
                  Session API + OIDC finalize (login-client PAT, server-only)
                         |
                         v
                    Pay /callback?code&state
                         |
                  PKCE token exchange at the issuer
                         |
                  access_token (JWT) + id_token + refresh_token
                         |
                  Pay SPA sends access_token --> Pay API :8081  GET /v1/whoami
                         |
                  Pay API forwards access_token --> One API  GET /api/v1/me
```

| Host | Role | Pay does |
|------|------|----------|
| Zitadel issuer | OIDC **authority**, JWKS, token endpoint | SPA `authority`. **Never** Management PAT calls. |
| Login UI | Password / MFA / register | Users land here because **Zitadel** redirected. Not homepage. |
| One API `/api/v1` | Resource server | Pay backend `GET /me`, `authz/check`, later keys/webhooks. |
| Pay API `:8081` | Money + whoami BFF | Forwards Bearer. Own CORS. |
| Pay merchant `:5178` | Merchant SPA | PKCE public client. `pickApiBearerToken`. |
| Pay checkout `:5179` | Buyer cash register | **No** OIDC. **No** One. |
| `:3005` | Stock Login V2 | Break-glass on One. Collision with old admin. Never ship. |
| `:5173` | Staff console | Never ship merchants. |

SPA env **does not contain the login host** (D92 / 08-dogfood §6.2). `VITE_ZITADEL_AUTHORITY` is the issuer, not `:5175`. Switching product login from `:3005` to `:5175` does not change `client_id` or redirect URIs.

### 3.2 One Pay SPA, one `client_id` — not a client per merchant tenant

`POST /api/v1/tenants/{tenantId}/apps` creates an OIDC app **on a tenant**. First-party `lazuar-app` is **not** N apps; it is one public client, and `GET /me` lists every workspace the human belongs to. Pay must copy that:

- **Merchant origin** (local `http://localhost:5178`, production `https://<pay-merchant-host>`) is **one** public SPA.
- **One `client_id`** for that origin, used by every merchant staff.
- Workspace switch = `GET /v1/whoami` → `tenants[]` → navigate to `/v1/orgs/{id}/…` (or `/w/{id}/…`). Path id is SoT.
- Do **not** `POST /tenants/{eachMerchant}/apps` for Pay itself. That would mint N `client_id`s, N redirect lists, and a Console-shaped ops burden — the thing Consumer-0 exists to avoid.
- Merchants may still `POST /tenants/{theirId}/apps` for **their** second apps (Bezos door, later). That is not Pay’s login client.

Where does the Pay app row live on One?

| Option | How | Verdict |
|--------|-----|---------|
| **A. First-party seed** (like `lazuar-app`) | Extend `seed-platform-spa-clients.sh` **in the One repo** with `PAY_REDIRECT_URI=http://localhost:5178/callback`. Requires `ZITADEL_PAT` on **One ops**, not Pay. | Allowed as **P40 convenience**. Not required. Must not put the PAT in Pay. |
| **B. `POST /tenants/{id}/apps`** with a human JWT | Ada (owner/admin) on some existing workspace (dogfood tenant, or a dedicated “Pay platform” tenant if One ever has one) registers `name: "lazuar-pay-merchant"`, `type: "spa"`, Pay redirects. Recipe **R3**. | **Preferred product path** (NP-ONE-001, P10.2). Pay UI can call this once, or an engineer curls it. |
| **C. Zitadel Console click** | Create USER_AGENT app in Console, paste `client_id` into Pay `.env`. | **Refuse** as happy path (NP-ONE-001, NP-ONE-004). Break-glass only, same as One’s spa-oidc-setup Step 5 for platform leftovers. |

Option B still needs a **tenant id** to hang the app on. Dogfood: Ada’s Acme tenant is enough. Production: One ops may seed a first-party client (A) so Pay is not blocked on “create a tenant before you can log in to create a tenant.” Chicken-and-egg is real:

1. To `POST /tenants/{id}/apps` you need a user JWT of an owner/admin of `{id}`.
2. To get a Pay-branded JWT you need Pay’s `client_id`.
3. Therefore **first** Pay `client_id` is created either by seed (A, One PAT, One repo) **or** by Ada using **lazuar-app’s** JWT to register Pay’s SPA on her tenant (B), then Pay env is pointed at that `client_id`.

Do not solve the chicken by putting `ZITADEL_PAT` in Pay. That is how Pay becomes Zitadel ops (012/02 §10.3).

### 3.3 Exact register call (R3, TypeSpec)

```http
POST /api/v1/tenants/{tenantId}/apps
Authorization: Bearer <user access_token>
Content-Type: application/json
Accept: application/json
Idempotency-Key: <uuid>   # optional; replay → 200, client_secret always null
```

```json
{
  "name": "lazuar-pay-merchant",
  "type": "spa",
  "redirect_uris": ["http://localhost:5178/callback"],
  "post_logout_redirect_uris": ["http://localhost:5178/"]
}
```

Production body uses **HTTPS** origins, no localhost (`Zitadel:OidcDevMode` is **forbidden** in Staging/Production — `ZitadelOptionsValidator`, D25). Local `http://localhost:5178` requires One `Zitadel:OidcDevMode=true` (Development default) **or** the seed script `DEV_MODE=true`.

TypeSpec: `packages/api-spec/modules/apps/routes.tsp` `AppOperations.createApp`; models `CreateOidcAppRequest` (`type: spa | web | m2m`). Runtime (`HttpZitadelAdminClient.CreateOidcApplicationAsync`):

| Field | SPA (`type: spa`) | Web BFF (`type: web`) |
|-------|-------------------|------------------------|
| Zitadel `appType` | `OIDC_APP_TYPE_USER_AGENT` | `OIDC_APP_TYPE_WEB` |
| `authMethodType` | `OIDC_AUTH_METHOD_TYPE_NONE` | `OIDC_AUTH_METHOD_TYPE_BASIC` |
| `responseTypes` | `OIDC_RESPONSE_TYPE_CODE` | same |
| `grantTypes` | authorization_code + **refresh_token** | same |
| `accessTokenType` | **`OIDC_TOKEN_TYPE_JWT`** (not configurable on the request) | same |
| `client_secret` | omitted / null | returned **once** on 201 / rotate |
| PKCE | required (public) | recommended |

Pay **should ship `type: spa`** on `:5178` (copy `lazuar-app`). A confidential `web` BFF is allowed later if Pay wants tokens out of the browser; then `client_secret` lives in Pay server secrets, **never** `VITE_*`. Do not mix: a Vite app with a secret is a leak.

`m2m` is Zitadel client_credentials for **the integrator’s APIs**, not a One `lzr_sk_`. Pay’s worker credential to One is Family A `lzr_sk_` (§6), not an OIDC m2m app.

Related One routes (admin of the app, not login):

| Method | Path | Pay use |
|--------|------|---------|
| `POST` | `/api/v1/tenants/{tenantId}/apps` | Register Pay SPA (once) |
| `GET` | `/api/v1/tenants/{tenantId}/apps` | List metadata; no secret |
| `GET` | `/api/v1/tenants/{tenantId}/apps/{appId}` | Inspect redirects |
| `POST` | `/api/v1/tenants/{tenantId}/apps/{appId}/rotate-secret` | Confidential only |
| `DELETE` | `/api/v1/tenants/{tenantId}/apps/{appId}` | Revoke; login breaks; OIDC failure is the detector |

JWT gate: owner/admin. API keys need `admin`/`*` to create. First registration uses a **human** JWT (Ada via lazuar-app, or seed).

### 3.4 Redirects and login `REDIRECT_ALLOWLIST` (NP-ONE-004)

Two lists. Both must include the Pay **merchant** origin. Console-only is not enough — Zitadel app `redirectUris` **and** login BFF allowlist.

**A. One app `redirect_uris` / `post_logout_redirect_uris`** (the object from §3.3).

Local:

```text
redirect:      http://localhost:5178/callback
post_logout:   http://localhost:5178/
```

Also register `http://127.0.0.1:5178/callback` if engineers mix hostnames (One CORS already learned `localhost` vs `127.0.0.1` — issue 077). Prefer **one** hostname in docs (`localhost`) and stick to it.

Production:

```text
redirect:      https://<pay-merchant-host>/callback
post_logout:   https://<pay-merchant-host>/
```

No path other than `/callback` unless the SPA router says so. `strictPort` locally means `:5178` is the origin; do not silently fall to 5179.

**B. Login BFF `REDIRECT_ALLOWLIST`** (`apps/lazuar-login/.env.example` today):

```text
http://localhost:5173,http://localhost:5174,http://localhost:5177,http://localhost:8085,http://localhost:5175
```

**`:5178` is absent.** Finalize `callbackUrl` after password will **reject** Pay until One ops adds:

```text
http://localhost:5178
```

and in production the HTTPS merchant origin. Production: empty `REDIRECT_ALLOWLIST` **exits** the BFF (`NODE_ENV=production`). No localhost insert. This is **One login config**, not a Pay env var. Pay must not grow a second allowlist of its own that “helps” by skipping the BFF.

Do **not** add `:5179` (checkout) to the login allowlist. Buyers never complete OIDC. Adding it invites someone to “just log the buyer in.”

Do **not** add `:8081` (Pay API). It is not a browser OIDC client.

Do **not** add `:3005` or `:5173` as Pay docs destinations.

### 3.5 PKCE, picker, scopes, token lifetimes, refresh

Copy `lazuar-app` (`oidcConfig.ts` + `pickApiBearerToken`). Do not invent a fourth policy.

| Item | Value | Evidence |
|------|-------|----------|
| Grant | Authorization Code + **PKCE** | `response_type: 'code'`; seed / API `OIDC_AUTH_METHOD_TYPE_NONE` |
| Client type | Public SPA | No secret in Vite |
| Authority | Local `http://localhost:8085`; prod = One’s issuer | `.env.example` |
| Scopes | `openid profile email offline_access` | `VITE_ZITADEL_SCOPE` |
| Staging/prod audience | Add `urn:zitadel:iam:org:project:id:{Zitadel__Audience}:aud` when One `RequireAudience=true` | 012/02 §2.1; empty audience **fails boot** in strict env |
| Access token type | JWT (`jti` required) | `JwtAccessTokenGuard`; ID token 401 |
| Clock skew on One | **60 seconds** | `ZitadelJwtBearerDefaults.ClockSkew` |
| Refresh | `offline_access` + `OIDC_GRANT_TYPE_REFRESH_TOKEN` already on API-created apps | `HttpZitadelAdminClient` grantTypes |
| Silent renew | `automaticSilentRenew: true`, `WebStorageStateStore(sessionStorage)` locally | `oidcConfig.ts` comment: XSS-sensitive; **prefer BFF / short-lived tokens in prod** |
| Picker | JWT-like `access_token` only; never `id_token`; opaque → omit Authorization | `pickApiBearerToken` |

**Lifetimes.** Neither the seed script nor `HttpZitadelAdminClient` set `accessTokenLifetime` / `idTokenLifetime` / `refreshTokenExpiration`. They take **Zitadel application defaults** (typically hours for access, longer for refresh — confirm on the env’s Console / Management read, do not hard-code a number in Pay). Pay must:

1. Treat access tokens as **short-lived**. Whoami and MemberGate will 401 when expired; the SPA refreshes silently or redirects to authorize.
2. **Not** stash Ada’s access_token in Pay server env as a standing secret (012/02 §8.1 was engineer-exported dogfood only).
3. **Not** lengthen tokens by holding a PAT and minting custom JWTs.
4. In production, prefer **short access TTL + refresh** over sessionStorage-as-vault. A Pay BFF (`type: web`) is the honest upgrade if XSS on `:5178` is unacceptable; it is not a C-phase or a reason to skip SPA register. If Pay stays public SPA, document the XSS residual the same way `lazuar-app` does.

Pay API **does not validate the JWT signature today**. It trusts One’s 200/401 on `/me` and `authz/check`. That remains correct in production: Pay is not a second resource server of Zitadel unless a later paper adds JWKS validation **without** a PAT (public JWKS is not a secret). Do not add JwtBearer on 8081 “to be a real API” if the effect is duplicating One’s audience/jti rules badly. Forwarding is the Consumer-0 shape.

`id_token` stays forbidden as Bearer even if it has email (012/02 §2.2, M2M-14). Access tokens often omit profile; whoami `email` may be null. Do not “fix” that by sending `id_token`.

### 3.6 CORS on **One** (browser → One `/api/v1`)

If the Pay SPA calls One **directly** (whoami alternative: browser `GET {ONE}/me` plus Pay `/v1/{orgId}/…`), One must allow the Pay origin.

One Development CSV (`appsettings.Development.json` `App:CorsOrigins`) today:

```text
http://localhost:5173,http://localhost:5174,http://localhost:5180,http://localhost:5181,
http://localhost:3000,http://localhost:3001,http://localhost:5177,
http://127.0.0.1:5173,http://127.0.0.1:5174,http://127.0.0.1:5177,
http://127.0.0.1:5180,http://127.0.0.1:5181
```

**`:5178` is absent. `:5179` is absent.** A browser on `:5178` that `fetch`es `http://localhost:8080/api/v1/me` will fail preflight until One ops adds:

```text
http://localhost:5178,http://127.0.0.1:5178
```

Staging/Production: `App:CorsOrigins` empty **fails boot**. Set exact HTTPS merchant origin. Never `*`. Never localhost in prod.

**`:5175` is not an API CORS origin** — login BFF is same-origin to the login UI.

**`:8081` is not an API CORS origin** — it is a server. Server-to-server Pay→One is not a CORS request.

**`:5179` must not be added to One CORS.** Checkout must not call One. If a future “need CORS for 5179” ticket appears, it is a plane mix (NP-XX-013).

This CORS change is **One config** (P40.2), not a One product feature, not a Pay TypeSpec route.

### 3.7 CORS on **Pay** (browser → Pay `:8081`)

Today hardcoded in `Program.cs` (Development convenience). Tests lock 5178/5179 allow and 3003 deny.

Production:

| Origin | Pay CORS | One CORS | Login allowlist | OIDC client |
|--------|----------|----------|-----------------|-------------|
| Pay merchant HTTPS | **Yes** | **Yes** if SPA calls One directly; optional if Pay BFF proxies `/me` | **Yes** | **Yes** (`client_id`) |
| Pay checkout HTTPS | **Yes** (public pay page talking to Pay) | **No** | **No** | **No** |
| `lazuar-app` | No | Already yes | Already yes | Different `client_id` |
| `lazuar-ops :3003` | **No** | No | No | No |
| `lazuar-admin :5173` | **No** | Staff only | Staff only | Staff client |

Pay CORS policy for production should:

1. Bind `Pay:CorsOrigins` (CSV) from env.
2. **Fail boot** in Staging/Production if empty (copy One / old Hub fail-closed, not Hub’s origin list).
3. Allow the headers the SPA actually sends: `Authorization`, `Content-Type`, `X-Lazuar-Tenant-Id`, `Idempotency-Key`. `AllowAnyHeader` is acceptable locally; production can stay any-header if Bearer is the only credential (no cookie CSRF). Do **not** `AllowCredentials` unless Pay starts a cookie session — and this paper refuses cookie JWT (`lazuar_auth`).
4. Keep checkout origin on Pay CORS so the hosted page can `GET /v1/health` and later `GET /v1/pay/{id}` **without** a Bearer from One.

If the SPA **only** talks to Pay, and Pay always proxies `/me`, One CORS for `:5178` is still required **as soon as** the SPA uses `@lazuar/one-client` or fetches One for invites/settings. Honest default: **add `:5178` to both**, because merchant chrome will call One for create-workspace / roster even if whoami is proxied.

### 3.8 Recommended Pay merchant env (P10, not implemented)

Mirror `lazuar-app/.env.example`, not old ops:

```bash
# issuer — not :5175
VITE_ZITADEL_AUTHORITY=http://localhost:8085
VITE_ZITADEL_CLIENT_ID=          # from POST …/apps or seed; public
VITE_ZITADEL_REDIRECT_URI=http://localhost:5178/callback
VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI=http://localhost:5178/
VITE_ZITADEL_SCOPE=openid profile email offline_access
VITE_PAY_API_URL=http://localhost:8081
# optional if SPA talks to One directly:
VITE_ONE_API_URL=http://localhost:8080/api/v1
```

Pay API (already):

```bash
One__BaseUrl=http://localhost:8080/api/v1
One__TimeoutSeconds=5
```

Later: `One__ApiKey=lzr_sk_…` (P20), `One__WebhookSecret=whsec_…` (P30). Never `ZITADEL_PAT`, never `VITE_ZITADEL_CLIENT_SECRET`, never `OpenFga__ApiToken`.

Checkout env stays `VITE_PAY_API_URL` only. **No** authority, **no** client_id.

### 3.9 What Pay still must not implement as “OIDC”

- Password form / ROPC / `POST /one/auth/login` on 8081.
- Login-client PAT in Pay or in `VITE_*`.
- Treating `:5175` as the product shell.
- Shipping `:3005` or `:5173`.
- `id_token` as Bearer; fallback when access is opaque.
- Parsing `urn:zitadel:iam:org:project:roles` (NP-XX-024). `accessTokenRoleAssertion: true` on create does **not** make those claims SoT.
- Registering Pay by Console as the day-2 path.

---

## 4. Workspace create/pick = `POST /tenants` or membership list — no Pay `organizations` table

012/06 remains the storage law. Production does not add a reason to map otherwise (012/06 §7 last-resort conditions are still false: One ids are stable UUIDs, Pay did not have merchants first, no regulator-required Pay-issued merchant SoT).

### 4.1 The rule

> For every Pay row that is merchant-scoped, `org_id` equals the One tenant UUID that `POST /tenants` returned and that `GET /me.tenants[].id` lists. There is no translation step.

Pay SQL (when S1 money exists): `uuid` columns, **no** `REFERENCES organizations(id)`. Isolation is `WHERE org_id = :pathOrgId` after One membership check.

Whoami is One’s list. Pay `GET /v1/whoami` is a projection, not a catalog. Cache TTL if any is a snapshot; membership SoT remains One.

### 4.2 HTTP Pay uses for workspace (One `/api/v1`)

| Method | Path | Production Pay use |
|--------|------|--------------------|
| `GET` | `/me` | Directory. Whoami. Can **write** (domain auto-join, SSO JIT). Session start / switch only. |
| `POST` | `/tenants` | **Create workspace.** Human JWT only (`RejectApiKey`). Body `{ name, slug }`. Optional `Idempotency-Key`. Caller becomes **owner**. 201 includes `id`. |
| `GET` | `/tenants` | Paginated memberships; `/me` is enough for the switcher. API keys need `tenant:read`. |
| `GET` | `/tenants/{tenantId}` | Profile + **`status`**. Members may GET when **suspended** (`AllowSuspended`). Charge path fail-closed helper. |
| `PATCH` | `/tenants/{tenantId}` | Name / metadata / logo — One SoT (NP-ONE-010). Human JWT; `tenant:update` or admin/owner. |
| `POST` | `/tenants/{tenantId}/retry-provision` | Break-glass if create landed `failed`. Not a Pay healer. |
| `POST` | `/tenants/{tenantId}/suspend` | Staff / Pay-admin policy — **not** merchant self-serve default. |
| `POST` | `/tenants/{tenantId}/reactivate` | Same. |
| `POST` | `/tenants/{tenantId}/transfer-ownership` | Owner change. Human JWT. |
| `POST` | `/tenants/{tenantId}/leave` | User leaves. |
| `POST` | `/tenants/{tenantId}/delete` | Owner wipe. Honest leftovers (Zitadel org may remain). |
| `POST` | `/tenants/{tenantId}/authz/check` | Before merchant admin / money routes. |

Do **not** call `POST /api/v1/platform/tenants` (NP-XX-023). Do **not** call Zitadel Management to create an org.

`Platform:AllowSelfServeTenantCreate` is One’s kill-switch. Pay does not implement a second kill-switch by writing `pay.organizations` while hiding the button.

### 4.3 Create vs pick in Pay UI (`:5178`)

```text
Ada @ Pay merchant origin
  → PKCE → :5175 → callback → access_token
  → GET {PAY}/v1/whoami  (proxies One GET /me)
  → if tenants.length >= 1: switcher; selecting id writes a UX hint
     (cookie/sessionStorage, same idea as lazuar_active_tenant)
     AND navigates to path /{id}/…
  → if “Create workspace”:
       POST {ONE}/api/v1/tenants
       Authorization: Bearer {access_token}
       Idempotency-Key: {uuid}
       { "name": "Acme", "slug": "acme" }
  → 201 { id, slug, name, status, ... }
  → Pay does not INSERT an org row. Pay puts `id` in the URL.
  → If status is still provisioning, poll GET /tenants/{id} or wait.
     Do not create a Pay “pending org” row.
```

The SPA may call One directly for `POST /tenants` (needs One CORS, §3.6) or Pay may proxy `POST /v1/tenants` as a convenience BFF. A Pay proxy is **not** a Pay org table: it forwards JSON and returns One’s `id`. Do not persist. Do not mint a second uuid.

Pick existing: `GET /me.tenants[]` already has `id`, `slug`, `name`, `role`, `status`. If `status != active`, do not pretend live charges work (§8).

### 4.4 `tenant.created` is not provision-catalog (NP-ONE-019 honesty)

012/06 §6.3 and 012/09 §5: `TenantService.CreateAsync` awaits provisioning **before** HTTP 201 on the success path. Pay already has an active id in the response. Do **not** insert empty products / ledger accounts / a tenant replica schema on the webhook. S1 inserts happen when Ada creates a product or a charge lands. `tenant.created` is useful as “this uuid exists,” especially for tenants born in `lazuar-app` then opened in Pay (lazy upsert on first Pay write). It is not a second SoT.

### 4.5 Header vs path in production

Same as 012/06 §4.3:

- Merchant routes: `/v1/{tenantId}/…` or `/v1/orgs/{orgId}/…` — pick one grammar in TypeSpec and keep it. Dummy today is `/v1/orgs/{orgId}/ready`. Checkout fixture takes `org_id` in the **body** (create) because there is no path yet; **money production should put org in the path** so the header cannot become SoT.
- Buyer routes: no One session, no `X-Lazuar-Tenant-Id` required. Tenant is on the checkout row Pay minted.
- If header present and disagrees with path: **path wins**. Do not 403 merely because the switcher is stale. Do not authorize the header’s tenant.

---

## 5. Invites stay One copy-link (011/12 step 4)

NP-ONE-011 / NP-ONE-012 / NP-ONE-022. 011/12: “Invite a second engineer with One **copy-link**.” Notes: keep non-email accept. “One’s next honesty (staging SMTP, staging proof) is **One’s**. Do not paper over a failed step 4 with a homemade invite.”

MEM-10 (One SMTP unproven) is **Both** dogfood- and sell-blocking **for One**, and **not** a Pay ticket (NP-XX-022). Copy-link is LOCAL-03: **keep**.

### 5.1 One HTTP (people)

Base: `/api/v1`.

| Method | Path | Pay use |
|--------|------|---------|
| `GET` | `/tenants/{tenantId}/members` | Roster. API keys need `members:read`. |
| `POST` | `/tenants/{tenantId}/members/invite` | Invite by email + role (`admin` \| `member`; **not** `owner`). Optional `Idempotency-Key`. 201 `InviteMemberResponse`. |
| `GET` | `/tenants/{tenantId}/invites` | Pending. **Never** raw token. |
| `DELETE` | `/tenants/{tenantId}/invites/{inviteId}` | Revoke |
| `POST` | `/tenants/{tenantId}/invites/{inviteId}/resend` | Regenerates token; emails new link |
| `POST` | `/tenants/{tenantId}/members/accept-invite` | Body `{ token }`. Human JWT. Keys **rejected**. |
| `PATCH` | `/tenants/{tenantId}/members/{userId}` | Role `admin` \| `member` |
| `DELETE` | `/tenants/{tenantId}/members/{userId}` | Remove |
| `GET` | `/me/invites` | Inbox for signed-in email. **No token.** Discovery only. Keys rejected. |

TypeSpec: `MemberOperations` / `InviteOperations` / `MeInviteOperations`. `InviteMemberResponse.invite_token` is present **only** when `Invite:ReturnTokenInResponse` is true. Development defaults ON (`Program.cs` if unset); Production JSON **`false`**. Staging/prod copy-link in UI therefore depends on One returning the token to the **inviter** in the 201 (Dev) or on the email (prod). Pay must not scrape a token out of `GET …/invites` — it is not there.

Do not call Zitadel InviteUser. Issue 018 closed that façade. One membership is SoT.

### 5.2 Copy-link format (LOCAL-03 — keep stable)

`lazuar-app` `inviteLink.ts`:

```text
{origin}/invites/accept?tenant_id={tenantId}&token={token}
```

Today `origin` is **lazuar-app** (`http://localhost:5174`). Email body uses One `PublicAppBaseUrl` the same way.

Pay may:

1. **Deep-link to lazuar-app** accept page (allowed by 011/02). Second engineer lands on `:5174` / prod app host, joins, then opens Pay `:5178` and sees the tenant on `/me`.
2. **Implement Pay’s own accept page** that posts `POST /api/v1/tenants/{tenantId}/members/accept-invite` with `{ token }` after Pay PKCE. Copy-link host becomes Pay origin **only if** One’s email `PublicAppBaseUrl` (or a Pay-specific link Ada copies from Pay chrome) points there. Changing the email template is **One config**, not a Pay SMTP stack.

Either way: **keep a non-email path**. Token in the URL is the accept credential. Inbox `GET /me/invites` cannot join (issue 039 class). Do not “fix” SMTP by building Pay `POST /v1/invites` with a second token table.

### 5.3 Roles you can invite

One `MembershipRoles`: `owner` \| `admin` \| `member`. Invite `owner` → 400 (“Use transfer-ownership”). There is **no** `viewer`. NP-ONE-021 is not satisfiable as a One invite (012/07 §10). Dogfood second engineer: invite **`member`**. After accept:

- `GET /me.tenants[]` contains the tenant, `role: "member"`.
- Pay `authz/check` `member` → allowed → merchant ops chrome (NP-ONE-022).
- Keys / refunds: Pay policy on money routes (`check(admin)` for keys; charge/refund Option A = `member` until One grows `viewer`). Do not fake VIEWER with a custom role (FGA stays `member`).

### 5.4 What Pay UI may wrap vs what Pay must not store

Allowed: Pay chrome that calls the One routes above with the user’s access_token; copy-link button; pending list; revoke; resend.

Forbidden: `pay.invites`, `pay.members`, `GlobalUser`, password hash, Pay-sent invite email as SoT, Zitadel InviteUser.

### 5.5 Production SMTP does not block Pay identity

If staging invite email never arrives, copy-link still joins. That is the designed mitigation (08-dogfood §8.3 MEM-10 / LOCAL-03). Pay production-ready **identity** does not wait on One `prove-smtp.sh`. Pay production-ready **money** does not wait on it either. A merchant who cannot invite a bookkeeper uses copy-link until One’s MEM-10 is PASSED.

---

## 6. `lzr_sk_` for workers (P20): scopes explicit, Pay validates via One

012/08 remains the key paper. This section is the **production** reading: when the env key exists, what it is allowed to do, and how Pay authenticates a merchant’s key without holding One’s pepper.

### 6.1 Family split (do not mix)

| Family | Prefix | Holder | Production |
|--------|--------|--------|------------|
| **A. One product key** | `lzr_sk_` | Minter, once. Pay worker holds **one** in env for **one** tenant. Merchants hold **theirs**. | This section. |
| **B. BYOK Stripe/CHIP/Billplz** | `sk_live_` / vendor | Pay encrypted vault | S1 money. Never Bearer to One. |
| **C. Old Hub integrator key** | `sk_test_` / `sk_live_` | Museum | **Do not rebuild.** |

Nearby non-credentials: `id_token`, Zitadel PAT, login PAT, OpenFGA admin, `lzr_scim_`, `whsec_…` (HMAC, not Bearer), One pepper.

### 6.2 Mint (One HTTP) — still a human admin job

```http
POST /api/v1/tenants/{tenantId}/api-keys
Authorization: Bearer <owner|admin access_token>
Content-Type: application/json
```

```json
{
  "name": "pay-worker",
  "scopes": ["tenant:read", "authz:check"]
}
```

201 includes `secret` (`lzr_sk_…`) **once**. List/get never return it. Empty `scopes: []` → **400**. Omitted scopes → `["tenant:read"]` — Pay helpers must still **send** an explicit array. `*` / `admin` are full-tenant equivalent; Pay must not request them for the worker.

| Method | Path | JWT | Key |
|--------|------|-----|-----|
| `POST` | `/api/v1/tenants/{tenantId}/api-keys` | admin\|owner | `admin`/`*` |
| `GET` | `/api/v1/tenants/{tenantId}/api-keys` | any member | `keys:read` |
| `DELETE` | `/api/v1/tenants/{tenantId}/api-keys/{keyId}` | admin\|owner | `admin`/`*` |

Revoke → 204; subsequent Bearer → 401. Cross-tenant / missing → **403**, not 404.

UI: lazuar-app Settings → API keys. Pay chrome may wrap the same API. Store is One.

### 6.3 Scopes Pay should request (explicit)

First worker mint (dogfood / production jobs that read status and check membership):

```json
["tenant:read", "authz:check"]
```

| Job | Add (still explicit) |
|-----|----------------------|
| `GET /me` as the key | none required (any valid key) |
| `GET /tenants/{id}` status (suspend belt) | `tenant:read` |
| `POST …/authz/check` as worker | `authz:check` + **human** `user_id` in body (never the key id) |
| Register Pay’s webhook receiver | `webhooks:write` **and** `webhooks:read` (write does not imply read) |
| Pull `GET …/events` | `events:read` |
| Mint more keys as a machine | `admin`/`*` — **refuse** unless a written job cannot use a user JWT |

Do not add `members:read` “just in case.” Do not send Family C strings (`payments.checkouts:write`) — unknown → 400.

Keys **cannot**: `POST /tenants`, accept-invite, leave, transfer-ownership, `GET /me/invites`, become `is_platform_admin`, call `POST /platform/tenants`. Bound to **one** tenant.

### 6.4 How Pay validates a `lzr_sk_` (no pepper)

Pay **cannot** hash the secret. Introspection = HTTP to One with that Bearer:

```http
GET /api/v1/me
Authorization: Bearer lzr_sk_…
```

200: `user_id` = key GUID, `tenants` 0–1 bound workspace, `is_platform_admin` false, `active_tenant_id` = bound tenant (header ignored). Timeout / 401 / 5xx → fail **closed** for that Pay route.

MemberGate today forwards whatever Bearer the caller sent. A merchant M2M into Pay `/v1` (later, NP-API-004) presents **their** `lzr_sk_`; Pay replays it to One `authz/check` with `user_id` required on One’s side — wait: MemberGate’s current body **omits** `user_id`. That is correct for **user JWT**. For an API-key caller, One returns **400** `"user_id is required when authenticating with an API key."` Production MemberGate must branch:

- JWT (no `lzr_sk_` prefix) → omit `user_id` (current).
- `lzr_sk_` → either (a) **do not** use MemberGate’s `check(member)` as the merchant-M2M door until a written `user_id` policy exists, or (b) for **Pay’s own worker** talking to One, pass a real member `user_id` from the job payload — never the key id.

Pay’s **interactive** merchant SPA continues to send the **user access_token**. Never fall back from missing user JWT to `One:ApiKey` on an interactive route (012/08 §7.3). That would let a logged-out caller act as the worker’s tenant.

### 6.5 `ONE_API_KEY` / `One:ApiKey` in production env

- **One row**, bound to **one** One tenant (dogfood workspace or a dedicated ops tenant).
- Value starts with `lzr_sk_`. Prefix-check before any outbound call. Reject `sk_live_`, JWTs, PATs.
- Secret store / env / vault. Never git. Never logs. Never `VITE_*`.
- Does **not** scale to all merchants. Multi-tenant Pay uses Mode U (per-request user JWT) + per-tenant HMAC webhooks. If a job must call One for tenant B without a user: do **not** hold a PAT; prefer the webhook envelope’s `tenant_id` or fail. Storing N merchant `lzr_sk_` in Pay is a secret vault for One keys — worse than HMAC.

`NP-ONE-020`: Pay holds only OIDC `client_id` (public), `lzr_sk_` (once), One-webhook HMAC (`whsec_`, once). That is the entire Pay-owned secret set for One integration.

### 6.6 `api_key.revoked`

One already publishes it (012/08 §4.7; catalog live despite stale “planned” sentence in one doc). Pay handles it **when Pay caches introspection**. Mode U has no cache of merchant secrets — ignore until NP-API-004. If Pay caches: HMAC verify, idempotent on `X-Lazuar-Event-Id`, drop by `data.key_id` (prefix is not unique). Late webhook: re-introspect fail-closed on One 401. Money already booked stays booked.

---

## 7. HMAC webhooks Pay must subscribe: `member.*`, `tenant.created` / `suspended` / `reactivated`, `ownership.transferred`, `api_key.revoked`

012/09 remains the event paper. Connection skipped the receiver. **Production money must not.** NP-ONE-017 / NP-ONE-018 earn their keep **before the first live charge**, not before whoami.

### 7.1 Three webhook planes (do not share a table)

| Plane | Direction | Auth | Production |
|-------|-----------|------|------------|
| **A. One → Pay** (this section) | One POSTs signed JSON to Pay | `whsec_…`, `X-Lazuar-Signature: v1=` | Membership, tenant lifecycle, key revoke |
| **B. PSP → Pay** | Stripe / CHIP / Billplz | Provider HMAC / SDK | Money. Different route, different secret, different idempotency tuple |
| **C. Pay → merchant** | Pay POSTs out | Later Bezos door | Not v1 first-party |

Do not implement A in order to practice B. Do not verify Stripe with `whsec_` from One.

### 7.2 Register (One HTTP)

```http
POST /api/v1/tenants/{tenantId}/webhooks
Authorization: Bearer <admin|owner JWT, or lzr_sk_ with webhooks:write>
Content-Type: application/json
```

```json
{
  "url": "https://<pay-host>/v1/one/webhooks",
  "events": [
    "tenant.suspended",
    "tenant.reactivated",
    "tenant.created",
    "tenant.deleted",
    "member.accepted",
    "member.removed",
    "member.left",
    "member.role_changed",
    "ownership.transferred",
    "api_key.revoked",
    "webhook.test"
  ],
  "description": "lazuar-pay control plane"
}
```

201 includes `secret: "whsec_…"` **once**. Store immediately (per tenant — One’s product is per-tenant; N merchants ⇒ N secrets). List/get: `secret_prefix` only.

Prefer an **explicit subset** (unknown types 400 at register; future catalog additions do not hit an unready handler). Omit `events` / `[]` = all seventeen — then ignore unknown at the receiver with **200**.

Related routes:

| Method | Path | Notes |
|--------|------|-------|
| `POST` | `/api/v1/tenants/{tenantId}/webhooks` | 201 secret once. JWT admin\|owner; key `webhooks:write` |
| `GET` | `/api/v1/tenants/{tenantId}/webhooks` | Metadata. Key `webhooks:read` |
| `GET` | `/api/v1/tenants/{tenantId}/webhooks/{webhookId}` | |
| `PATCH` | `/api/v1/tenants/{tenantId}/webhooks/{webhookId}` | URL / events / `status` |
| `DELETE` | `/api/v1/tenants/{tenantId}/webhooks/{webhookId}` | 204 |
| `POST` | `/api/v1/tenants/{tenantId}/webhooks/{webhookId}/rotate-secret` | New secret once; **no dual-verify window** |
| `POST` | `/api/v1/tenants/{tenantId}/webhooks/{webhookId}/test` | 202; enqueues `webhook.test` even if filter omitted it |
| `GET` | `/api/v1/tenants/{tenantId}/webhooks/{webhookId}/deliveries` | Log |
| `GET` | `/api/v1/webhook-event-types` | Closed catalog |
| `GET` | `/api/v1/tenants/{tenantId}/events` | Pull catch-up. `events:read`. **403 after suspend** (not AllowSuspended) |

Local URL `http://localhost:8081/v1/one/webhooks` **fails One SSRF** (loopback) unless `Webhooks:UrlHostAllowlist` on **One** contains a non-loopback hostname. Staging/prod: public **HTTPS**, port **443** in strict env. Do not weaken One’s CIDR list from Pay.

Max **10** endpoints per tenant. Pay uses **one** URL per tenant, not one per event type.

### 7.3 Catalog types this paper names (closed v1, `WebhookEventCatalog`)

| Type | Pay production use |
|------|--------------------|
| `tenant.suspended` | **Mandatory.** Stop **new** charges and staff mutating money ops. Do not reverse the journal. |
| `tenant.reactivated` | **Mandatory pair.** Re-enable new charges. Subscribe or the shop stays dead. |
| `tenant.created` | Lazy upsert / “uuid exists.” Not seed catalog (NP-ONE-019 honesty). |
| `tenant.deleted` | Stop everything; leftover Pay rows are money history, not a live merchant. Do not CASCADE delete charges. |
| `member.accepted` / `removed` / `left` / `role_changed` | Optional staff chrome cache. **`/me` remains SoT.** Domain auto-join / SSO JIT do **not** emit `member.accepted`. |
| `member.invited` / `invite.revoked` / `invite.resent` | Optional. One is invite SoT. |
| `ownership.transferred` | Billing owner if Pay prints a legal owner. `/me` role is enough for chrome. |
| `api_key.revoked` | Drop cached One secrets if any. Env-held Pay worker key: next One call 401s; ops rotate. |
| `api_key.created` / `oidc_app.*` | Optional inventory. Login failure detects Pay’s own SPA revoke. |
| `webhook.test` | Prove HMAC (08 §6.12 checkbox moves here, not to whoami). |

011/02 and 08 §6.8 subset is the product intent: `member.accepted|removed|left|role_changed`, `ownership.transferred`, `tenant.suspended|reactivated`, `tenant.created`, `api_key.revoked`. This paper **includes** `tenant.deleted` and `webhook.test` as operational necessities.

### 7.4 Wire: HMAC, headers, idempotency

Dispatcher POST body (snake_case envelope): `id`, `type`, `created_at`, `tenant_id`, `api_version`, `data`. Headers: `X-Lazuar-Event-Id` (idempotency), `X-Lazuar-Event-Type`, `X-Lazuar-Tenant-Id` (metadata, not authz), `X-Lazuar-Timestamp` (unix **seconds**), `X-Lazuar-Signature: v1=<hex>`, `X-Lazuar-Delivery-Id` (**not** the idempotency key).

```text
signed_payload = "{unix_seconds}." + raw_body_bytes
HMAC-SHA256(key = full whsec_ UTF-8, msg = signed_payload)
```

Skew 300s. Constant-time compare. Re-serializing JSON before verify fails. 2xx quickly (One HTTP timeout 10s). Non-2xx → retries (7 attempts, backoff to ~24h, auto-disable at 15 consecutive failures). At-least-once: crash after Pay 2xx retries the **same** `event_id`. Pay table keyed by `event_id` (+ `source=one` if sharing a processed-events table with Stripe — **do not** collide).

Rotate: previous `whsec_` stops immediately. Cut during quiet; accept one retry.

### 7.5 Pay receiver sketch (P30 — not this file’s PR)

Suggested route: `POST /v1/one/webhooks` on 8081. No Bearer. HMAC is the auth. Not under Hub `/api/v1/webhooks/payments/{gateway}`. Not under `/one/*` inside Pay.

Handlers that must exist before live charges:

- `webhook.test` → persist event id, 200.
- `tenant.suspended` → `charges_enabled=false` (same txn as processed row).
- `tenant.reactivated` → `charges_enabled=true`.
- Others → 200 ignore until caches exist.

Checkout / off-session charge: if `!charges_enabled` **or** One `GET /tenants/{id}` says `suspended` → refuse. If One is unreachable: **fail closed** on live charges (§8). Do not fail open “so dogfood works when One is rebooting.”

Pull: `GET /tenants/{id}/events` while tenant is **active**. After suspend, members get **403** on events — use `GET /tenants/{id}` (`AllowSuspended`) or `/me.tenants[].status`. Do not tail Zitadel.

---

## 8. Fail-closed if One down (503 already on whoami/ready) vs money webhooks that must not depend on One availability

This is the production split 011/02 already chose: *If the webhook is late, **money in Pay is still true**; staff access may lag. Do not put buyer entitlement in One.*

### 8.1 What already fail-closes on One (staff / identity)

| Pay route | One down / timeout / 5xx | Evidence |
|-----------|--------------------------|----------|
| `GET /v1/whoami` | **503** | `Whoami_maps_one_timeout_to_503`, `Whoami_maps_one_500_to_503` |
| `GET /v1/orgs/{orgId}/ready` | **503** | `Ready_503_when_one_500` |
| `POST /v1/checkouts` (fixture, MemberGate) | **503** (via MemberGate) | Same mapper |
| Missing Bearer on those | **401**, One not called | |
| `GET /health`, `GET /v1/health` | **200**, One not called | C15 |

Keep this. Production merchant ops **must not** fail open on a fake membership because One timed out. 503 is the honest identity answer. Do not serve a cached `/me` as authorization (012/07 §14: `/me` lists, `authz` may deny).

One `authz/check` itself fail-closes on FGA down (**503** `"Authorization service is unavailable. Fail-closed."`). Pay maps that to 503. Do not translate to 200 fixture. Rate limit 429 → Pay 429; do not retry-storm (`AuthzPerWindow` default 30/60s).

Suspended tenant: One membership gate 403s `"Tenant is suspended."` before Check. Pay dummy 403s even if the One→Pay webhook is late. That is the **belt** for **staff**. It is not sufficient for **buyer checkout**, because the buyer has no One token and never hits MemberGate.

### 8.2 What must **not** depend on One being up (money already in flight)

| Path | One down | Why |
|------|----------|-----|
| PSP webhook (Stripe/CHIP/Billplz) → journal + `RCPT-` | **Must still commit** | Fulfillment is Pay’s handler, same DB transaction (NP-FUL-001). Waiting on One recreates parked-event tax. |
| Idempotent PSP replay | No-op without One | `(org_id, provider, event_id)` is Pay’s tuple |
| Health / process liveness | 200 | C15 |
| Buyer opening an **already created** hosted page whose shop was active at mint time | Product choice: prefer also checking local `charges_enabled` | Do not call One on the hot buyer path if the flag was set by HMAC; **do** fail closed if flag is false |
| Refund of a captured charge | Pay money op | Not an One event |

If Ada’s buyer pays, PSP fires, Pay journals, **then** One is down — the receipt exists. Staff chrome catches up on next whoami. That lag is the cost of the split.

### 8.3 Live charge **start** (new session / off-session / pay link)

Here One availability **does** matter, because suspend must stop **new** money (NP-ONE-018) even when the HMAC is late.

Belt and suspenders (012/09 §6.3):

1. **HMAC push** sets Pay-local `charges_enabled=false` for that `org_id`.
2. **Request-path** `GET /api/v1/tenants/{id}` with Pay’s `lzr_sk_` (`tenant:read`) **or** the merchant JWT when a merchant is creating a pay link.

If One is reachable and `status == suspended` → refuse, even if local flag is still true (late webhook).  
If One is **unreachable**:

| Local flag | Production choice |
|------------|-------------------|
| `charges_enabled == false` | Refuse (webhook already arrived). |
| `charges_enabled == true` (or missing) | **Fail closed** on **live** charges. Do not take a buyer’s card because One rebooted. Document the residual: a brief outage of One stops **new** checkout starts. Already-open PSP captures still complete (row above). |
| Local flag missing because Pay never registered webhooks | Treat as **not production-ready for charges**. Connection whoami is not a license to charge. |

Do not fail open. Do not “queue the charge until One returns.” Do not put the buyer in Zitadel so you can “suspend users.”

`GET /tenants/{id}/events` is **not** the suspend detector (403 after suspend). Status GET is.

### 8.4 Staff lag vs money truth (examples)

1. Ada removes Bob; Bob’s Pay tab still shows ops until next whoami/`authz`. **Acceptable.** Do not build a membership replica “so chrome is instant.”
2. Staff suspends tenant; HMAC delayed; Bob still passes MemberGate until One has suspended (SQL gate). Once One suspends, next ready/checkout-create 403s **even without** the webhook. Buyer checkout **without** status GET / local flag can still take money — that is the hole P30 closes.
3. HMAC arrives after a capture: **do not** reverse the journal. Refunds are Pay (`NP-MON-005`).
4. One 503 on invite accept is retry-the-verb (persist-then-FGA). Pay does not call `POST /platform/tenants/{id}/reconcile-fga` (NP-XX-023). Pay does not hold FGA admin to self-heal.

### 8.5 Timeouts

Pay `One:TimeoutSeconds` default **5**. One webhook HTTP timeout **10**. Do not raise Pay’s client timeout to “wait out One restarts” on the charge path — fail closed faster. Health stays independent.

---

## 9. What must change in One repo vs Pay-only

P40: **C-phases: zero One product PRs.** Production identity still needs **config / seed** on One, not a new IdP, not Pay routes on One TypeSpec.

### 9.1 Pay-only (this repo) — the bulk of the work

| Change | Where | Notes |
|--------|-------|-------|
| Wire OIDC PKCE on `lazuar-pay-merchant` `:5178` | `apps/lazuar-pay-merchant` | Copy `oidcConfig` + `pickApiBearerToken`. No password form. |
| Env: authority, `client_id`, redirect `:5178/callback` | merchant `.env` | Public client_id. |
| Whoami from the SPA | fetch Pay `/v1/whoami` with access_token | Already implemented on the host. |
| Create/pick workspace UI | SPA calls `POST /tenants` or Pay proxy | No org table. |
| Production CORS on Pay from env, fail-closed empty | `Program.cs` | Keep 5178/5179 locally; no 3003. |
| `One:ApiKey` worker path | Pay host | Prefix-check `lzr_sk_`. MemberGate JWT vs key branch. |
| `POST /v1/one/webhooks` + `charges_enabled` | Pay host + storage | HMAC; idempotent `event_id`. |
| Charge path consults flag + optional `GET /tenants/{id}` | Checkout / pay link / off-session | Fail closed if One down and live. |
| TypeSpec grow webhook route | `packages/pay-spec` | Not `packages/api-spec`. |
| Tests: fake One + HMAC vectors | `Lazuar.Pay.Tests` | Still no live Zitadel in `task pay:test`. |
| Checkout SPA stays One-free | `lazuar-pay-checkout` | Fail the slice if Zitadel login appears. |

### 9.2 One repo — allowed later (config / seed, not a new product)

| Change | Kind | Required? |
|--------|------|-----------|
| Add `http://localhost:5178` (and 127.0.0.1) to `App:CorsOrigins` Development CSV | Config | **Yes** before SPA calls One from the browser |
| Add `http://localhost:5178` to login `REDIRECT_ALLOWLIST` | Config | **Yes** before PKCE finalize |
| Staging/prod: HTTPS merchant origin on both lists | Config | **Yes** for production login |
| Optional: `seed-platform-spa-clients.sh` third client `lazuar-pay-merchant` redirects `:5178/callback` | Seed convenience | **No** if Pay uses `POST …/apps` with Ada’s app JWT |
| `Webhooks:UrlHostAllowlist` for laptop receiver | One ops hatch | Laptop only; prod uses public HTTPS |
| Staff VIEWER as membership role | **Product** | Only if NP-ONE-021 must be literal; until then Pay enforces money routes (C24). Do not sneak into Pay. |

None of these require Pay to hold `ZITADEL_PAT`. Seed stays in **lazuar-one**. CORS/allowlist are One env.

### 9.3 One repo — forbidden “for Pay” (P40.3)

- Checkout / ledger / receipts in One.
- Pay routes on One TypeSpec (`/v1/whoami` is Pay’s).
- FGA types `payment` / `document` without a written Pay `authz/check` call (NP-XX-015). Dummy/member check is `type=tenant` only.
- Giving Pay a Zitadel PAT or OpenFGA admin.
- Holding Pay whoami on One staging-proof / SMTP / npm publish (NP-XX-021, NP-XX-022).
- Public `authz/write`.
- SCIM Groups / IdP-initiated as a Pay ticket.
- Changing copy-link query names (`tenant_id`, `token`) without a migration — LOCAL-03.

### 9.4 What One already has (do not rebuild in Pay)

`GET /me`, `POST /tenants`, invites + copy-link, `POST …/apps`, `POST …/api-keys`, `POST …/authz/check`, webhook catalog + HMAC dispatcher, `GET …/events` pull. Pay is a client.

---

## 10. Anti-goals

Restated so production pressure cannot “temporarily” reopen them.

| Anti-goal | Why it fails this paper |
|-----------|-------------------------|
| Pay password form / `POST /one/auth/login` / cookie `lazuar_auth` | Second IdP. NP-XX-007. 012/02 §7. |
| `ZITADEL_PAT` / login-client PAT / OpenFGA admin / masterkey / pepper / webhook AES in Pay | Pay **is** Zitadel ops. NP-XX-017, NP-ONE-020. |
| Console-only `client_id` as happy path | NP-ONE-001, NP-ONE-004. Redirects drift; leftover opaque tokens. |
| `id_token` as Bearer | M2M-14. One 401 (`jti`). |
| Authorize from `X-Lazuar-Tenant-Id` or old `X-Tenant-Id` | NP-ONE-007. |
| `CREATE TABLE organizations` / `users` / `memberships` / `invites` / `org_map` | NP-XX-014, NP-XX-007. 012/06. |
| `GET /me` as global middleware; whoami on `/health` | JIT writes; probes couple to One. C13, C15. |
| Hammer `/me` on every charge / React render | Membership writes; authz 429. |
| Parse `urn:zitadel:iam:org:project:roles` | NP-XX-024. |
| Invite VIEWER / mark NP-ONE-021 done via `check(member)` | VIEWER is not a One role. C24. |
| Custom role named Viewer as money ACL | FGA stays `member`. 012/07 §10. |
| FGA types `payment` / `document`; `authz/write`; OpenFGA HTTP from Pay | NP-XX-015, NP-XX-016. |
| Homemade `sk_test_` / `sk_live_` integrator keys | Family C. Prefix collision with Stripe. |
| Stripe secret as `ONE_API_KEY` | Family B vs A. |
| Empty / `*` key scopes | P12 / D68. |
| Buyer as Zitadel human; checkout OIDC | NP-XX-013, NP-CHK-007. |
| Add `:5179` to One CORS or login allowlist | Plane mix. |
| Ship merchants to `:5173` or `:3005` | NP-ONE-005, NP-XX-018. |
| Pay homepage = `:5175` | Login is a redirect target. |
| Block Pay on npm `@lazuar/one-client` | NP-XX-021. |
| Block Pay on One SMTP / Okta / SCIM / hosted SKU | NP-XX-022. |
| Homemade Pay invite table because email is unproven | Paper over step 4. Keep copy-link. |
| Tail Zitadel events | NP-ONE-017 notes. One outbox is the catalog. |
| Reverse journal because `tenant.suspended` arrived late | Money in Pay stays true. |
| Fail **open** on live charges when One is down | NP-ONE-018 hole. |
| Fail **closed** on PSP capture / health because One is down | Opposite plane. |
| Wait to write `RCPT-` until One ACKs | Parked-event tax. |
| Retarget `lazuar-ops` / `lazuar-portal` at 8081 | P60 refuse. New UIs are 5178/5179. |
| MediatR / `Modules/One` / `BuildingBlocks` / `Lazuar.slnx` | IsolationTests. Cathedral. |
| `POST /platform/tenants` from Pay | NP-XX-023. |

---

## 11. Open questions

These are not invitations to weaken the locks. They are the remaining **product/ops** choices a later implementer must write down — or they will be decided accidentally in a PR.

1. **Pay merchant production hostname** (and checkout hostname). Local `:5178` / `:5179` are pinned (`strictPort`). Production origins must be exact strings on One CORS, login allowlist, and the OIDC app. Paper 04/05 own the names; this paper only requires they are **not** `:5173`, `:5175`, `:3005`, `:3003`, `:3004`.

2. **Chicken-and-egg first `client_id`.** Seed in One (P40, PAT stays in One) vs Ada registers Pay’s SPA via `POST …/apps` using a **lazuar-app** JWT, then Pay env is updated. Recommendation: **B for dogfood, A optional for shared staging** so two humans do not need `:5174` to log into Pay. Do not invent a third path (Console).

3. **SPA vs confidential web BFF in production.** Local copy of `lazuar-app` (sessionStorage + silent renew) is XSS-honest. Production may want `type: web` so refresh tokens never touch the merchant origin. That is a Pay host seam (paper 03), not a reason to hold a PAT. Until chosen, implement **spa** — it is NP-ONE-002 as written.

4. **Does the merchant SPA call One directly, or only Pay?** Direct: must land on One CORS + use `pickApiBearerToken` for both bases. Proxy: Pay grows `POST /v1/tenants` convenience; CORS on One can wait until invites/settings are embedded. Honest default: **both**, because create-workspace and copy-link are One’s UI jobs that Pay will wrap.

5. **Checkout create org-in-body vs path.** Fixture `POST /v1/checkouts` with `org_id` in JSON is fine for connected. Production merchant money routes should put `{orgId}` in the **path** so header/body cannot disagree. Pick in `pay-spec` when P50 lands.

6. **Where `charges_enabled` lives.** Per-tenant row in Pay (not an org SoT — a **money gate flag** keyed by One uuid) vs in-memory until a DB exists. First live charge cannot be honest in-memory across replicas. Paper 03 (host seams / DB) owns the table; this paper owns the semantics.

7. **Per-tenant `whsec_` storage.** N secrets. Vault vs column encrypted with **Pay’s** key (never One’s AES key). Rotate runbook (no dual-verify window).

8. **Worker key tenant.** Which One tenant is `One:ApiKey` bound to in multi-merchant production? Recommendation: **do not** use one env key for all shops; Mode U + per-tenant webhooks. Env key is dogfood / platform-ops tenant only. Confirm in paper 03 secrets.

9. **MemberGate + `lzr_sk_`.** Current body omits `user_id`. Production M2M into Pay needs an explicit design (skip check and introspect `/me` bound tenant == path, vs pass a human `user_id`). Do not silently 400 in prod.

10. **VIEWER.** Still One product-pull (Option C in 012/07). Pay v1 copy: “Roles are Owner, Admin, Member. There is no read-only Viewer until One ships it.” NP-ONE-021 stays `todo`. Do not fake it.

11. **Audience pin (issue 076 residual).** Development One may have audience off. Staging/prod require `Zitadel:Audience`. Pay SPA must add the reserved scope the day One strict env is on. Confirm the project id with One ops — Pay does not guess it from a PAT.

12. **Token TTL numbers.** Confirm Zitadel defaults on the **production** application after register (Management read or Console break-glass **read**, not Pay holding a PAT). Document access TTL + refresh idle in Pay’s runbook. This paper refuses to invent a number One does not set on create.

13. **Invite email `PublicAppBaseUrl`.** Stay on lazuar-app accept URL, or switch to Pay origin once Pay has an accept page. Must stay one format (LOCAL-03). Dual links without a written cutover will strand tokens.

14. **Laptop 8080/8081 collisions** (Aura, One remapped to 8081, Pay on 8081). Operational, not identity law. When registering webhook URLs, bind the host Pay actually listens on. Do not “fix” with SSRF weaken.

15. **Whether Pay validates JWTs locally later.** Optional performance/independence. Public JWKS ≠ PAT. Not required for production identity; forwarding to One is already fail-closed. If added, still never authorize from `org_id` claims One does not mint (ORG-08).

---

## 12. Tracker mapping (do not flip cells from this paper)

| ID | 011 text | 013/08 reading |
|----|----------|----------------|
| NP-ONE-001 | Register Pay SPA via `POST /tenants/{id}/apps` (or seed) | **Production INCLUDE.** Not Console. One `client_id` for `:5178` origin. |
| NP-ONE-002 | OIDC code + PKCE; Pay `client_id` | **INCLUDE** on merchant Vite. Checkout excluded. |
| NP-ONE-003 | access_token as Bearer | **Connected done.** Keep in SPA picker. |
| NP-ONE-004 | Redirects on One app + login allowlist | **INCLUDE.** `:5178` missing on One today. |
| NP-ONE-005 | Login `:5175`; never `:3005`/`:5173` | **INCLUDE** in Pay merchant copy + env. Homepage is `:5178`. |
| NP-ONE-006 | `GET /me` | **Connected done** as `/v1/whoami`. Do not hammer. |
| NP-ONE-007 | Path + membership SoT | **Connected done** on `/ready`. Grow onto money paths. |
| NP-ONE-009 | Create = `POST /tenants`; id is `org_id` | **INCLUDE** in merchant UI. No org table. |
| NP-ONE-011/012 | Copy-link invite; non-email accept | **INCLUDE** as One HTTP / deep-link. No Pay invite table. |
| NP-ONE-014 | Mint `lzr_sk_` explicit scopes | **INCLUDE** before workers / webhook register as machine. Whoami stays Mode U. |
| NP-ONE-015 | `authz/check` before admin | **Connected done** (`member` on dummy). Money: `admin` for keys. |
| NP-ONE-017 | HMAC webhooks named subset | **INCLUDE before live charges.** Explicit event list §7. |
| NP-ONE-018 | Stop charges on `tenant.suspended` | **INCLUDE before live charges.** Late webhook: money stays true; new charges fail closed. |
| NP-ONE-019 | Provision on `tenant.created` | Lazy upsert; **do not** seed catalog. |
| NP-ONE-020 | Hold only `client_id`, `lzr_sk_`, HMAC | **INCLUDE** as env shape. Still no PAT. |
| NP-ONE-021 | VIEWER cannot charge | **Still blocked on One role.** Pay-side honesty. |
| NP-ONE-022 | Invited MEMBER sees ops | After copy-link + `check(member)`. |
| NP-XX-017/022/014/013/007 | PAT / One SKU / org table / buyer humans / password | **Refuse, unchanged.** |

011/12 steps 1, 4, 5 (mint), 6 stay `todo` until those jobs run on new Pay. This analysis does not edit the tracker.

---

## 13. Fail modes (production)

| Fail | Symptom | Fix |
|------|---------|-----|
| SPA authorize without `:5178` on login allowlist | Finalize rejects `callbackUrl` | One `REDIRECT_ALLOWLIST` |
| SPA `fetch` One without `:5178` on `App:CorsOrigins` | Browser preflight fail | One CORS CSV |
| Pay CORS still only localhost in prod | Merchant origin blocked | Env allowlist, fail boot if empty |
| `OidcDevMode=true` in staging | Boot fail, or http redirects that should not work | D25; HTTPS redirects |
| Empty One CORS in staging | One API fail boot | One ops |
| Console-only client, opaque access | 401 on `/me`; engineer sends `id_token` | Recreate via `POST …/apps` JWT |
| `id_token` as Bearer | 401 while DevTools shows signed-in | Picker |
| Ada’s **app** `:5174` client used as Pay prod | Merchants live in lazuar-app | Pay `client_id` |
| Checkout OIDC | Cardholder in Zitadel | Remove; NP-XX-013 |
| `ZITADEL_PAT` in Pay vault | Pay is Zitadel ops | Delete; use user JWT / seed in One |
| `CREATE TABLE organizations` | Dual SoT | 012/06 |
| Webhook URL `http://127.0.0.1:8081` | SSRF reject | Allowlist hostname or public HTTPS |
| Live charge without receiver | Suspended shop still charges | P30 before live money |
| Fail open on One timeout at checkout | Charge during identity outage | Fail closed |
| PSP handler waits on One | Paid, no receipt | Plane B independent |
| Health calls whoami | Probe death when One down | C15 |
| Invite table in Pay | Second membership | Copy-link |
| `ONE_API_KEY=sk_live_…` | Stripe in Bearer | Prefix-check |
| MemberGate + key without `user_id` | One 400 | Branch §6.4 |
| VIEWER chip / `check(viewer)` on tenant | 400 unsupported relation | C24 |

---

## 14. Evidence index

### Pay (`6f866ff0`)

- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` — CORS 5178/5179; maps whoami, ready, checkouts, health.
- `apps/lazuar-pay/src/Lazuar.Pay/One/OneClient.cs` — `GET me`, `POST tenants/{id}/authz/check`; 5s timeout; 503 on transport.
- `apps/lazuar-pay/src/Lazuar.Pay/One/MemberGate.cs` — path org; `{allowed:false}` → 403; One down → 503.
- `apps/lazuar-pay/src/Lazuar.Pay/One/WhoamiEndpoints.cs` — endpoint not middleware.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/{WhoamiTests,OrgReadyTests,HealthTests,CorsTests,CheckoutTests}.cs`
- `apps/lazuar-pay-merchant/src/App.tsx` — health only; mentions `:5175`; no OIDC.
- `apps/lazuar-pay-checkout/src/App.tsx` — no One account.
- `packages/pay-spec/main.tsp` — whoami / ready / checkout; no webhook.
- `plans/012-one-to-pay/checklists/{p10,p20,p30,p40,c99,decisions}.md`
- `plans/011-new-lazuar-pay/{02-one-integration,03-first-slice,11-checklist,12-first-slice-tracker}.md`

### One (`0f79fe4`)

- TypeSpec `packages/api-spec/modules/{platform,tenants,apps,api-keys,authz,webhooks}/{routes,models}.tsp`
- `Features/Webhooks/WebhookEventCatalog.cs`
- `Infrastructure/Zitadel/HttpZitadelAdminClient.cs` — JWT access tokens, PKCE, refresh grant.
- `scripts/seed-platform-spa-clients.sh` — app+admin only; `ZITADEL_PAT`.
- `apps/lazuar-login/.env.example` — `REDIRECT_ALLOWLIST` without 5178.
- `apps/lazuar-api/.../appsettings.json` + `appsettings.Development.json` — CORS without 5178/5179.
- `apps/lazuar-app/src/auth/oidcConfig.ts`, `src/lib/inviteLink.ts`
- `plans/017-evals/08-dogfood-then-serve.md` §6

---

## 15. One-paragraph restatement

Local Pay already trusts One over HTTP: Ada’s access_token reaches `GET /v1/whoami`, path `{orgId}` plus `POST /api/v1/tenants/{id}/authz/check` with `relation=member` gates `/ready` and fixture checkouts, and health never asks One. Production identity is that loop **with Pay’s own merchant origin** (`:5178` / HTTPS), a public SPA `client_id` minted through `POST /api/v1/tenants/{id}/apps` (or an optional One-side seed — never a Pay-held PAT), PKCE against the Zitadel issuer, password UI still `:5175`, redirects and CORS on **both** One and Pay, workspace create still `POST /api/v1/tenants`, invites still One copy-link, a scoped `lzr_sk_` for workers that Pay introspects via One, and HMAC push of `tenant.suspended` **before live charges**. Staff routes fail closed (503) when One is down; captured money and PSP webhooks do not wait on One; new charges fail closed if One is down or the tenant is suspended, even when the webhook is late. Pay does not become Zitadel ops.

Do not implement from this file.
