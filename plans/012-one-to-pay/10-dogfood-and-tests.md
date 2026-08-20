# 10 — Dogfood proof, tests, sequencing, and anti-goals (One → new Pay)

**Date:** 20 August 2026  
**Family:** 012-one-to-pay  
**Paper:** 10 — dogfood and tests for the *first connection*  
**Type:** Program paper. Analysis only. **Do not implement from this file.**  
**Repos:**

| Tree | Path | Branch (this write) | SHA | Note |
|------|------|---------------------|-----|------|
| New Pay (this repo) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `feat/012-one-to-pay` | `6ca8f19f4b28c056f852b7b579b5b30428e48ad6` (`6ca8f19f`) | Tip: `feat(pay): add TypeSpec package for the focused Pay host`. Focused host lands in `apps/lazuar-pay` on **8081**. |
| Old Pay cathedral | same repo, `apps/lazuar-api` | same | same | Reference only. Waves 001–260 on `main` @ `e7bb07b0`. Issues **261–334** (74 P2s) still open on paper. |
| Lazuar One | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `main` | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` (`0f79fe4`) | Tip: `WIP: Thu Aug 20 21:24:22 +08 2026`. Staging proof **NOT PASSED**. `@lazuar/one-client` unpublished. |

**Sibling papers (011, already written; this paper does not reopen them):**

- [011/01-product.md](../011-new-lazuar-pay/01-product.md) — product law; the full dogfood *sentence*
- [011/02-one-integration.md](../011-new-lazuar-pay/02-one-integration.md) — HTTP Pay must call; secrets Pay must never hold
- [011/03-first-slice.md](../011-new-lazuar-pay/03-first-slice.md) — S0 then S1; fail locks
- [011/11-checklist.md](../011-new-lazuar-pay/11-checklist.md) — living `NP-*` tracker
- [011/12-first-slice-tracker.md](../011-new-lazuar-pay/12-first-slice-tracker.md) — ordered 12-step dogfood loop
- [011/10-tracker-schema.md](../011-new-lazuar-pay/10-tracker-schema.md) — how to flip a cell
- [011/08-bezos-door.md](../011-new-lazuar-pay/08-bezos-door.md) — `/v1` is the door; One is the other team
- [011/09-old-pay.md](../011-new-lazuar-pay/09-old-pay.md) — do not grow the cathedral; do not implement 261–334 on it
- [011/README.md](../011-new-lazuar-pay/README.md) — binding decisions, including “not 261–334 on the old tree”

**One’s matching program (already written; not restated as a new decision):** `lazuar-one/plans/017-evals/08-dogfood-then-serve.md` §6 (Pay is Consumer-0). First-party checklist §6.11–6.12 is the same contract this paper turns into **pass/fail + tests**.

**Other 012 papers:** at write time `plans/012-one-to-pay/` contained only this file. This paper is independent. Later 012 papers may sequence SPA registration, invites, keys, and One webhooks; they must not quietly redefine “connected” as the full S1 money loop.

**Living host this paper talks to:** `apps/lazuar-pay` (C# net10.0, one solution, one test project). Language paper 011/05 still says **Go** for a kernel rewrite. That rewrite is **not** this slice. First connection lands on the host that already exists. Do not start a second Pay tree in Go “because 05 said so” in the same PRs that prove whoami.

---

## 0. How to read this paper

011’s dogfood test is a **merchant money loop**:

> A merchant signs in through **One**, opens Pay, pastes CHIP or Stripe keys, a **buyer pays on the hosted page without a One account**, Pay shows one `RCPT-` and a balanced journal, a webhook retry no-ops, a One-invited MEMBER can see ops and a VIEWER cannot charge.

That sentence is still the **product** pass. It is **not** the pass for *this* paper.

This paper’s job is narrower and earlier:

**Prove that new Pay is connected to One.** Connected means: a Bearer that One would accept for `GET /api/v1/me` is enough for Pay to know *who the merchant staff is* and (optionally) to ask One whether they `member` a given tenant. It does **not** mean keys, checkout, receipts, ops UI, invites, or webhooks.

If you mark S0 done because whoami returned 200, you have lied. If you skip whoami and start `POST /v1/checkouts` with a homemade `organizations` table, you have rebuilt `Modules/One`. If you stub `POST /one/auth/login` on 8081 so old `lazuar-ops` can keep its password form, you have failed the slice even if the JSON looks like a session.

Three bars, three papers:

| Bar | What it proves | Where it lives |
|-----|----------------|----------------|
| **Connected** (this paper) | Pay process on **8081** can consume One’s HTTP façade with a real (or faked) Bearer. Whoami. Optional `authz/check member`. No password form. No second org table. | 012 / this file |
| **S0 façade** (011/12 steps 1–7) | SPA registered through One, login via `:5175`, create/pick tenant, copy-link invite, scoped `lzr_sk_`, One webhooks, **stop** | 011/03, 011/12; later 012 papers |
| **S1 money** (011/12 steps 8–12 + 01 dogfood sentence) | BYOK → product → hosted buyer pay → webhook + journal + `RCPT-` in one txn; MEMBER sees ops; VIEWER cannot charge | 011/01, 011/12; **not this paper’s DoD** |

Status cells in [011/11-checklist.md](../011-new-lazuar-pay/11-checklist.md) stay `todo` until the *job* is proven in new Pay. The old C# tree does not count. A green whoami test does not flip NP-CAT / NP-CHK / NP-GW.

---

## 1. Facts on these SHAs (do not pretend otherwise)

### 1.1 New Pay host

`apps/lazuar-pay` is a focused process. On `6ca8f19f`:

- `src/Lazuar.Pay/Program.cs` maps `GET /health` and `GET /v1/health` to `{ status: "ok" }`. Nothing else.
- Listen URL: `http://localhost:8081` (`Properties/launchSettings.json`). Old modular API keeps **8080** *when that process is the one running*. One’s API is **also** `http://localhost:8080`. See §11. You cannot run old Pay API and One on the same laptop both bound to 8080.
- One solution, one host csproj, one test csproj. **No** `ProjectReference` to `apps/lazuar-api`, `Modules.*`, `BuildingBlocks`, MediatR, or `Lazuar.Api`.
- `InternalsVisibleTo` = `Lazuar.Pay.Tests`.
- Tests today:

| File | What it proves |
|------|----------------|
| `tests/Lazuar.Pay.Tests/HealthTests.cs` | `WebApplicationFactory<Program>`: `/health` and `/v1/health` are success and contain `ok`. Two tests, two factories, no One, no bearer. |
| `tests/Lazuar.Pay.Tests/IsolationTests.cs` | Host csproj text does **not** contain `lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api`. Walks parents from `AppContext.BaseDirectory` until `src/Lazuar.Pay/Lazuar.Pay.csproj` exists. |

- TypeSpec: `packages/pay-spec/main.tsp` (`@repo/pay-spec`). Server `http://localhost:8081`. Only `GET /v1/health`. **Not** `packages/api-spec`. README of pay-spec is explicit: do not import One, LHDN, or `/public/commerce` routes. Grow `main.tsp` when Pay grows. First connect **may** add whoami (and optional authz) to **this** spec. First connect **must not** hook NSwag / honesty-allowlist / `docs-one.tsp` of the old tree.
- Taskfile: `task pay:test`, `task pay:dev`, `task pay:spec`. `task pay:test` today is “health + isolation.” After this slice it must also be whoami (+ authz) against a **fake** One. It must **not** require Compose, Zitadel, or `pnpm api:dev` in the One repo.
- No `One:BaseUrl` in `appsettings.json`. No `HttpClient`. No authentication middleware. That is the starting point, not a bug.

### 1.2 Old Pay (the museum you must not re-enter)

- `POST /api/v1/one/auth/login` — password form backend. Cookie `lazuar_auth`. Ops `LoginPage.tsx` posts `{ email, password }` and then `window.location = /commerce/dashboard`.
- `GET /one/auth/me` — session from that cookie. Dual cookie realm (`lazuar_auth` vs `lazuar_admin_auth`). Role vocabulary `ADMIN` in JSON vs `CLIENT` on the cookie is a closed harvest, not a pattern to copy.
- `apps/lazuar-ops` default `VITE_API_URL = http://localhost:8080/api/v1` (`apps/lazuar-ops/src/lib/api-client.ts`). Typed against **`@repo/api-types-ts` generated from `packages/api-spec`**. Sends `X-Tenant-Id` (old name), not One’s `X-Lazuar-Tenant-Id`.
- `mprocs-dev.yaml`: ops **:3003**, portal **:3004**, old admin **:3005** `/admin/`. Those ports are the *old* frontends. They are not the merchant door of new Pay.
- TypeSpec for that museum: `packages/api-spec/modules/one/routes.tsp` still has `/one/auth/login`, `/one/auth/me`, register, forgot, reset. Generated DTOs: `packages/api-types-dotnet` NSwag from `packages/api-spec/dist/openapi.yaml`.
- Issues **261–334**: identity oracles, cookie vs Bearer, API-key hash/prefix, genesis password, portal logout, more. Example: `issues/261-p2-b07-i17-reset-password-is-an-email-oracle.md` is `POST /one/auth/reset-password` on the **old** One module. Closing it is not Consumer-0. Implementing it on 8081 is how the museum teleports.

### 1.3 One (the other team)

Local topology (One `apps/lazuar-docs/docs/reference/ports.md`):

| Thing | Port | URL | Audience |
|-------|------|-----|----------|
| One API | 8080 | `http://localhost:8080` | All (authz varies) |
| One `GET /me` | 8080 | `http://localhost:8080/api/v1/me` | User JWT or `lzr_sk_` |
| Product SPA | 5174 | `http://localhost:5174` | Customers (`lazuar-app`) |
| Staff SPA | 5173 | `http://localhost:5173` | Lazuar operators (`lazuar-admin`) — **not merchants** |
| Product login | 5175 | `http://localhost:5175` | `lazuar-login` BFF |
| Stock Login V2 | 3005 | `http://localhost:3005` | Break-glass only |
| Zitadel | 8085 | `http://localhost:8085` | IdP. Pay must not hold a PAT. |
| OpenFGA | 8090 | `http://localhost:8090` | Engine. Pay must not hold admin. |
| Example SPA | 5177 | `http://localhost:5177` | Hours-to-hello inside One repo |
| Docs | 5180 | `http://localhost:5180` | Recipes R1–R6 |

JSON: One API uses `JsonNamingPolicy.SnakeCaseLower`. `GET /me` body fields are `user_id`, `is_platform_admin`, `active_tenant_id`, `active_role`, `tenants[]` with `id`, `slug`, `name`, `role`, `status`, `permissions`.

`GET /me` **writes** when `email_verified == true`: domain auto-join and SSO JIT (`MeEndpoints.GetMe`). It is a command-on-GET. Pay must not hammer it from a hot loop (NP-ONE-006). First-connect whoami is an **explicit** route, not middleware on `/health` and not a per-charge introspect.

`POST /tenants/{tenantId}/authz/check`:

- Body: `{ user_id?, relation, object: { type, id } }`.
- Allow-list types: `tenant`, `app`. Other types → **400**.
- Tenant relations: `owner`, `admin`, `member`, `can_view`, `can_manage_members`, `can_manage_tenant`.
- App relations: `viewer`, `admin`. **`viewer` on `app` is not Pay’s VIEWER.** See §8.
- Caller must be a member of `{tenantId}` or One returns **403** (not `allowed: false`). `allowed: false` means “you may use the façade, but this relation is denied.”
- User JWT: omit `user_id` to check as `sub`.
- API key: `user_id` **required** and must not be the key id; key needs scope `authz:check` (or admin / `*`).

Membership roles in SQL/FGA: **`owner` ⊂ `admin` ⊂ `member`**. There is no built-in role string `viewer` on a tenant. Custom roles exist as a SQL overlay. Pay’s product sentence says VIEWER cannot charge — that is a **Pay policy** on top of One’s role + `authz`, not a new FGA type `payment` (NP-XX-015).

Demo human (One seed, local only): `ada@acme.test` / `Password1!`. Staff: `zitadel-admin@zitadel.localhost` / `Password1!` → `:5173`. Pay must never document staff login as the merchant path.

Recipes Pay’s curl script is allowed to clone (not reimplement):

- R1 — User JWT → `GET /me` (`examples/oidc-spa-notes`, `docs/recipes/user-oidc-spa.md`)
- R2 — `lzr_sk_` → `GET /me` (`examples/node-api-key`)
- R4 — `authz/check` (`docs/recipes/authz-check.md`)
- R6 — isolation **403** (`docs/recipes/isolation-403.md`)

`@lazuar/one-client` already wraps `me.get` and `authz.check`. It is **private**, unpublished, TypeScript. New Pay is C#. **Do not wait for npm** (NP-XX-021). **Do not add a Node BFF** just to import the workspace client. Pay speaks HTTP. Copy the *routes*, not the package.

One staging proof remains **NOT PASSED**. Laptop `./scripts/prove-local-stack.sh` is not staging. First connect does not require staging. It requires a fake One in CI and, for a human, a laptop One on 8080.

### 1.4 What “Consumer-0” already forbade (restated so this paper can fail a PR)

From 011/02 and One 017-08 §6, Pay:

- Does **not** hold Zitadel PAT, login-client PAT, OpenFGA admin, masterkey, webhook AES/pepper, `Platform:AdminEmails`.
- Does **not** parse `urn:zitadel:iam:org:project:roles`.
- Does **not** authorize from `X-Lazuar-Tenant-Id` alone.
- Does **not** call `POST /platform/tenants`.
- Does **not** add FGA types `payment` / `document`.
- Does **not** call `authz/write`.
- Does **not** send merchants to `lazuar-admin` (`:5173`) or stock Login V2 (`:3005`).
- Does **not** create a Zitadel human per cardholder.
- Does **not** invent a second membership system “just for merchants” and also use One members.

Those are fail locks for *every* 012 PR, including whoami.

---

## 2. Definition of done for first connection

**Connected** is true when all of the following hold. Anything not in this list is out of this DoD even if it is in S0.

### 2.1 Whoami (required)

1. New Pay process listens on **8081**. `GET /health` and `GET /v1/health` still return ok **without** One running and **without** a Bearer. Health is not whoami. Health does not call One.
2. Pay exposes **`GET /v1/whoami`**. Prefix is `/v1`, not `/api/v1`, not `/one`, not `/me`. Bezos door on this host is `/v1`. One’s door is `/api/v1` on a different origin. Do not “look like the old API” by adding `/api`.
3. Missing `Authorization` header → **401**. Empty `Bearer` → **401**. Pay does not call One in that case (no anonymous `/me` probe).
4. Present `Authorization: Bearer <token>` → Pay’s named `HttpClient` (`"one"`) sends **the same** `Authorization` value to One `GET {One:BaseUrl}/me`. Pay does not swap in an `id_token`. Pay does not attach a Zitadel PAT. Pay does not mint a cookie.
5. One **200** + JSON body → Pay **200** + a whoami JSON that makes the synonym explicit: **One `tenants[].id` is Pay `org_id`**. Required fields on 200:

   | Pay field | Source |
   |-----------|--------|
   | `user_id` | One `user_id` (Zitadel `sub` or API-key GUID) |
   | `email` | One `email` (optional) |
   | `name` | One `name` (optional) |
   | `orgs` | One `tenants[]`; each item `org_id` = that tenant’s `id` (UUID string). Also echo `slug`, `name`, `role`, `status`. `permissions` may pass through as chrome only. |
   | `active_org_id` | One `active_tenant_id` when present (hint matched). Omitted when One omitted it. |
   | `active_role` | One `active_role` when present. |
   | `is_platform_admin` | One `is_platform_admin`. Always present. API keys are never true. |

   Do not also emit a parallel `tenants` array. One noun on Pay’s door: **org**. The value is the One tenant id. No second UUID.
6. One **401** → Pay **401**. Do not translate to 200 with empty orgs. Do not retry.
7. One **403** → Pay **403**.
8. One unreachable, DNS fail, timeout, 5xx → Pay **503** (or **502** if you distinguish bad gateway; pick one and test it). **Never 200.** Never invent memberships from the JWT claims.
9. Optional request header `X-Lazuar-Tenant-Id` is **forwarded** to One on the `/me` call (hint). It is not copied into `active_org_id` by Pay if One omitted `active_tenant_id`. Pay does not treat the header as authorization.
10. JSON on Pay’s `/v1` for whoami is **snake_case**, matching One. Health can stay `{ "status": "ok" }` (same in camel and snake). Do not emit `userId` / `orgId` on whoami.
11. **No row is written** in Pay. First connect has **no** `organizations` table, no `memberships` table, no EF `DbContext`, no MediatR, no outbox. Whoami is HTTP in, HTTP out, map, return.
12. `GET /me` on One can JIT-join. Pay whoami therefore **can** have side effects **on One**. That is One’s contract. Pay must: (a) not call whoami from health or from a tight loop; (b) not add its own JIT into a Pay table; (c) document that whoami is not a cache fill.
13. Bearer may be a user JWT **or** `lzr_sk_…`. Pay does not special-case the prefix except to forward it. One decides. If the key is valid, whoami `user_id` is the key GUID and `orgs` has 0–1 entries. That is a pass for machine whoami.
14. Automated tests prove 1–13 with a **fake One** (`HttpMessageHandler`). See §7. CI does not boot One.
15. A human can run the curl script in §6 against a **live** local One and get a 200 whose `org_id`s equal One `/me` `tenants[].id` for the same token. That human proof is **not** a CI gate. It is the laptop dogfood for “connected.” If One is down, the human sees Pay 503, not a fake Ada.

### 2.2 Optional authz (part of DoD if the PR claims “whoami + authz”; not required to land whoami first)

011 and One §6.11 step 5: `check` `member` before merchant admin routes. First connect does not yet have merchant admin routes. The proof is therefore a **narrow Pay route that exists only to show the call**:

**`POST /v1/orgs/{orgId}/authz/check`**

- `{orgId}` is the One tenant id (path is SoT).
- Body: `{ "relation": "member" }` (default `member` if body empty but content-type json). Allowed relations on this Pay route in first connect: **`member`**, and if you must stretch, **`admin`** and **`owner`**. Nothing else.
- Pay sends to One:

  ```http
  POST {One:BaseUrl}/tenants/{orgId}/authz/check
  Authorization: Bearer <same>
  Content-Type: application/json

  {"relation":"member","object":{"type":"tenant","id":"{orgId}"}}
  ```

- Pay **does not** send `type: "payment"` or `type: "document"`. Pay **does not** send `type: "app"` in first connect (app.viewer is not merchant VIEWER).
- Pay **does not** expose a generic reverse-proxy of One’s authz API.
- Missing Bearer → **401**, no One call.
- One 200 `{ "allowed": true }` → Pay 200 `{ "org_id": "{orgId}", "relation": "member", "allowed": true }`.
- One 200 `{ "allowed": false }` → Pay **403** `{ "org_id", "relation", "allowed": false }` (or RFC 7807 with `allowed` in extensions). Do not 200 a deny. Merchant-admin-shaped routes must fail closed. Whoami stays 200 with the role list; authz is the gate.
- One 403 (caller is not a member of that tenant — R6) → Pay **403**. Do not turn this into 404. Guessing a UUID must not leak “this org exists in Pay.” First connect has no Pay org rows anyway; still lock the status code so S1 copies it.
- One 400 (Pay sent a bad type/relation) → Pay **500** or **502** with “Pay sent a bad authz request” — that is **our** bug, not the caller’s. Tests must not send `type=payment`.
- `X-Lazuar-Tenant-Id: <A>` plus path `{orgId}=<B>`: Pay calls One with path **B**. Header may still be forwarded as hint on a prior whoami; it does **not** select the org for authz.
- API-key Bearer on this route in first connect: **allowed to 400/403 from One** (keys need `user_id` in the check body). First connect does **not** require Pay to invent a subject for a key. Document: authz-as-yourself is a **user JWT** proof. Machine authz is S0 NP-ONE-014/015 after keys exist. Do not block whoami on this.
- No OpenFGA SDK. No playground URL. No `authz/write`. No `batch-check` in first connect (NP-ONE-016 stays todo).

If a PR ships whoami without this route, the PR description must say **“whoami only; authz is the next commit.”** Do not silently skip the second commit forever. Suggested sequence is §5.

### 2.3 Isolation / refuse (required in both whoami-only and whoami+authz)

1. `IsolationTests` still pass. Expand them as §7.3. Host csproj still has no old-API references.
2. Source of `apps/lazuar-pay/src` contains **no** `POST /one/auth/login`, no `/one/auth/me`, no password-verify, no BCrypt user table.
3. No `organizations` / `memberships` / `global_users` table, migration, or EF entity.
4. No link, redirect, env default, or comment in Pay that sends a merchant to `http://localhost:5173` or `lazuar-admin`.
5. No `packages/api-spec` import, no `Lazuar.ApiContracts` (old NSwag), no `Lazuar.ApiTypes` from this repo’s `packages/api-types-dotnet`.
6. `task pay:test` is hermetic: fake One only.
7. Issues 261–334 are **not** touched. Old `apps/lazuar-api` is **not** committed to in the same PRs except if you must fix a merge conflict you did not create — and even then, no feature work.

### 2.4 What “done” does **not** include (so nobody “finishes” S0 here)

- Registering a Pay SPA (`POST /tenants/{id}/apps`) — NP-ONE-001
- OIDC code + PKCE in a Pay frontend — NP-ONE-002
- Login allowlist / redirects — NP-ONE-004
- Product login via `:5175` as a Pay UX — NP-ONE-005
- Create workspace from Pay (`POST /tenants`) — NP-ONE-009
- Invites, roster, copy-link — NP-ONE-011..013, 022
- Mint `lzr_sk_` from Pay — NP-ONE-014
- HMAC One webhooks — NP-ONE-017, 018, 019
- VIEWER cannot charge — NP-ONE-021 (needs a charge route)
- `POST /v1/checkouts`, gateways, journal, `RCPT-` — all NP-CAT / NP-CHK / NP-GW / NP-FUL / NP-MON / NP-DOC
- A merchant ops SPA pointed at 8081
- npm publish of `@lazuar/one-client`
- Go rewrite
- Wiring old TypeSpec gen

Pass for **connected** is: §2.1 + §2.3, and §2.2 if the claim is “whoami + authz.”

---

## 3. Pass / fail for “connected”

This is the grade sheet. Use it in PR review. If a fail row is true, the slice **fails**. Do not mark NP-ONE-003 or NP-ONE-006 `done`.

### 3.1 Pass

| # | Pass condition | Evidence |
|---|----------------|----------|
| P1 | `GET /v1/whoami` with a live One user access token returns 200, `user_id` equals One `/me` `user_id`, every `orgs[].org_id` equals a One `tenants[].id` | Manual script §6, side-by-side curl |
| P2 | Same token without Pay, `GET http://localhost:8080/api/v1/me` still 200. Pay did not “eat” the session. Pay holds no cookie. | Manual |
| P3 | `GET /v1/whoami` without Bearer is 401. `/health` without Bearer is 200. | `HealthTests` + `WhoamiTests` |
| P4 | Fake One 200 → Pay 200 with mapped orgs. Fake One 401 → Pay 401. Fake One down → Pay 503. | `WhoamiTests` |
| P5 | Fake handler captured request: method GET, path ends with `/me` (not `/one/auth/me`, not `/platform/tenants`), header `Authorization` bitwise equal to what the test client sent | `WhoamiTests` |
| P6 | Fake handler never saw a `ZITADEL_PAT`, `Authorization: Bearer <PAT>`, OpenFGA store id, or login-client PAT | `WhoamiTests` |
| P7 | Optional: `POST /v1/orgs/{orgId}/authz/check` with `{ "relation": "member" }` → fake One `POST /tenants/{orgId}/authz/check` with `object.type=tenant` and `object.id={orgId}` | `AuthzTests` |
| P8 | Optional: One `{allowed:true}` → 200; `{allowed:false}` → 403; One 403 → 403 | `AuthzTests` |
| P9 | Path org B + header tenant A → One authz path is **B** | `AuthzTests` |
| P10 | `IsolationTests` (expanded) green. No `/one/auth/login` in Pay source. No old-API project references. | `IsolationTests` |
| P11 | `task pay:test` green on a machine with One **stopped** | CI / local |
| P12 | pay-spec (if grown) documents `/v1/whoami` on server 8081, not api-spec | `task pay:spec` |
| P13 | Whoami does not persist. Process restart + same token still works (One is SoT). | Design review + no migration in the PR |

### 3.2 Fail (do not paper over)

These include the 03 fail locks that already apply, plus connection-specific fails.

| # | Fail | Why it is a fail | Tracker |
|---|------|------------------|---------|
| F1 | Pay ships a password form or `POST /v1/login` or `POST /one/auth/login` | Reimplements Modules/One. Merchants never type a password into Pay. | NP-XX-007, 03 fail lock |
| F2 | Pay creates a second org / organizations / workspaces table as membership SoT | Two directories. Whoami would become a sync job. | NP-XX-014, 03 fail lock |
| F3 | Merchant docs, redirects, or env send people to `lazuar-admin` `:5173` | Wrong door. Staff console. | NP-XX-018, NP-ONE-005, 03 fail lock |
| F4 | Buyer / payer is created as a Zitadel human, or whoami is called on the hosted checkout with a required login | Mixes planes. First connect must not even start this. | NP-XX-013, NP-CHK-007 |
| F5 | Tests pass only because they hit live One, or CI starts Zitadel | Not hermetic. Will flake. Not Consumer-0 proof; it is a demo glued to CI. | (process) |
| F6 | Whoami mocks `IOneClient` / `IMembershipDirectory` and never uses `HttpMessageHandler` | You tested a stub, not the HTTP mapping. Assignment is fake One at the handler. | (process) |
| F7 | Pay validates Zitadel JWT locally as the **only** membership SoT and skips `/me` | Roles from token claims. NP-ONE-008 / NP-XX-024. JWKS is not membership. | NP-ONE-006, NP-ONE-008, NP-XX-024 |
| F8 | Pay sends `id_token` as Bearer | One issue 002 class. NP-ONE-003. | NP-ONE-003 |
| F9 | Pay holds Zitadel PAT / FGA admin to “make whoami work” | Secrets table in 011/02. | NP-ONE-020, NP-XX-017 |
| F10 | `VITE_API_URL` of **old** `lazuar-ops` pointed at 8081 | Ops speaks `/one/auth/login` + old TypeSpec. 8081 will either 404 or grow stubs. See §4. | NP-API-004 (ops is a *future* client of `/v1`, not this SPA) |
| F11 | PR waits on `npm publish @lazuar/one-client` | NP-XX-021. C# host does not need the TS pack. | NP-XX-021 |
| F12 | PR implements an issue in 261–334 on `apps/lazuar-api` | Cathedral work. 011 README binding #1 and #10. | (refuse old-tree work) |
| F13 | PR hooks `packages/api-spec` / old NSwag / honesty-allowlist so whoami appears on hub.lazuar.com OpenAPI | Wrong contract tree. | (pay-spec only) |
| F14 | `/v1/whoami` returns 200 when One is down, using cached orgs or JWT parse | Fail-open identity. | NP-ONE-006 |
| F15 | Health starts calling One | Health is liveness of Pay, not of Ada. | (health tests must stay One-free) |
| F16 | Authz proxy accepts arbitrary `object.type` | Next intern sends `payment` and then files AUTHZ-05 without a named check. | NP-XX-015 |
| F17 | Whoami writes Pay DB “so S1 is easier” | Second org table in disguise. | NP-XX-014 |
| F18 | First connect adds MediatR, per-module DbContext, inbox, or a project reference into `apps/lazuar-api` | IsolationTests must fail. Museum toolkit. | IsolationTests, 011/05 |
| F19 | Pay documents Console as the way to register the Pay SPA, *or* first-connect PRs click Console as the happy path | NP-ONE-001 notes: not a Console click. SPA registration is later S0, still not Console. | NP-ONE-001 |
| F20 | `allowed: false` from One becomes HTTP 200 on a Pay **gate** route | VIEWER-shaped fail-open. | NP-ONE-015, NP-ONE-021 (later) |

03’s other fail locks (setup counted as paid; receipt titled Tax Invoice; UUID document number; webhook retry double-journals) are **money**. They are not testable in first connect. They **remain locks**. A whoami PR that “prepares” a `TaxInvoice` entity or a UUID receipt helper has already started a fail. Do not add those files “for later.”

---

## 4. Explicit non-goals (what NOT to do)

This table is the program. Deleting a row is how the museum comes back.

| Do not | Do instead | Why | Tracker |
|--------|------------|-----|---------|
| Stub `POST /one/auth/login` (or `/v1/auth/login`, `/v1/one/auth/login`) on Pay 8081 | No login route on Pay. Sign-in is One `:5175`. Whoami consumes a Bearer One already minted. | Old ops `LoginPage` posts `/one/auth/login` with email/password. Re-hosting that on 8081 is Modules/One. 03 fail: “Pay password form.” | NP-XX-007 |
| Point **old** `lazuar-ops` (`apps/lazuar-ops`, :3003) at 8081 (`VITE_API_URL=http://localhost:8081/...`) | Leave ops on old 8080 until that process is retired. New merchant UI is a **new** client of `/v1` after S1 exists. First connect is curl + tests. | Ops OpenAPI types are old `packages/api-spec`. It will 404 every commerce route, then someone will “just add login.” | NP-API-004 (later), NP-XX-007 |
| Wait for npm publish of `@lazuar/one-client` / `one-react` / `one-cli` | C# `HttpClient` to One. Copy R1/R4 JSON. Workspace TS import is One’s first-party apps’ problem, not Pay’s C# host. | One 017-08: publish is sell-blocking, **not** dogfood-blocking. NP-XX-021. | NP-XX-021 |
| Put buyers / payers in Zitadel (InviteUser, Console human, Pay-triggered register) | Buyer plane is Pay checkout profile **later** (NP-BUY). First connect does not create a payer table at all. | 01: “Do not create a Zitadel human per cardholder.” 03 fail lock. | NP-XX-013, NP-CHK-007, NP-BUY-001 |
| Implement issues 261–334 on `apps/lazuar-api` | Leave them. Steal *judgment* only. New identity bugs are One’s tracker or new Pay’s NP-ONE rows. | 011 README: not a revert of 001–260; not a plan to implement 261–334 on the old tree. 09-old-pay: 74 P2s are why we leave. | (out of NP-*) |
| Hook old TypeSpec gen (`packages/api-spec`, `docs-one.tsp`, `packages/api-types-dotnet`, honesty-allowlist, Scalar hub `/one`) | Grow `packages/pay-spec` only. `task pay:spec`. | pay-spec README: not api-spec. Old `/one/auth/login` would leak onto the new door. | NP-API-* later use pay-spec |
| Add Pay JWT validation against Zitadel JWKS as a substitute for `/me` | Forward Bearer; One `/me` is membership SoT. Optional later: local JWT *integrity* check **in addition to** `/me`, never instead. | Project-role claims are refuse. Membership is SQL+FGA behind `/me`. | NP-ONE-006, NP-ONE-008, NP-XX-024 |
| Parse `urn:zitadel:iam:org:project:roles` | Use `/me` `role` + `authz/check` | NP-XX-024 | NP-XX-024 |
| Authorize from `X-Lazuar-Tenant-Id` or old `X-Tenant-Id` alone | Path `{orgId}` + One membership / authz | Old ops uses `X-Tenant-Id`. Do not revive it as SoT. | NP-ONE-007 |
| `POST /platform/tenants` from Pay | `POST /tenants` is later S0 (create workspace). Platform directory is staff. | NP-XX-023 | NP-XX-023 |
| Send merchants to `:5173` or `:3005` | `:5175` product login (later S0). `:5174` is One’s own app, not Pay’s homepage. | NP-ONE-005, NP-XX-018 | NP-ONE-005 |
| Import `Modules/One`, `BuildingBlocks`, MediatR, `Lazuar.Api` | Keep IsolationTests red if anyone does | 04-linux / 05-language: the toolkit rebuilt the museum | IsolationTests |
| Stand up Notify/Audit processes for whoami | No mail, no audit table required to say who you are | NP-XX-019 | NP-XX-019 |
| Add FGA types `payment` / `document` in One “for Pay” | `authz/check` `member` on `type=tenant` only | NP-XX-015, AUTHZ-05 wants a named consumer and a written check | NP-XX-015 |
| Call One `authz/write` | Never | NP-XX-016 | NP-XX-016 |
| Tail Zitadel events for membership | Later: One webhooks HMAC. Not first connect. | NP-ONE-017 | NP-ONE-017 |
| npm-install Clerk/Better Auth inside Pay “until One is ready” | One HTTP now. Staging NOT PASSED is not a license to grow a third IdP. | 011 binding #5 | NP-XX-007 |
| Mega-merge One into Pay’s binary so whoami is a function call | HTTP. One is the justified extract. | 011/13, 07 | (locked) |
| Five-deploy (Pay, Notify, Audit, Media, a second One) | One process + existing One | 011/13 | (locked) |
| Hosted One SKU / Okta / SCIM as the next Pay ticket | Stop after connected; then rest of S0; then money | NP-XX-022 | NP-XX-022 |
| Copy-link invites, roster UI, mint keys, One webhook receiver | Later S0 commits | 011/12 steps 3–6 | NP-ONE-009..019 |
| `POST /v1/checkouts`, Stripe/CHIP, journal, `RCPT-` | After S0 façade is real enough to paste keys as a *merchant who exists in One* | 011/12 steps 8–12 | NP-CAT, NP-CHK, NP-GW, … |
| Grow `lazuar-admin` (old Pay :3005 **or** One :5173) as merchant ops | New ops client of `/v1` later | 03 fail: merchant sent to lazuar-admin | NP-XX-018 |
| Use One `app.viewer` as Pay VIEWER | VIEWER is a Pay money policy. `app.viewer` is “can see this OIDC app object.” | §8 | NP-ONE-021 later |
| Cache `/me` in Redis/memory as SoT | Optional short cache **after** S0 if One latency hurts; invalidate on `member.*` webhooks. Not first connect. First connect is live HTTP. | GET /me writes; caching is a product | NP-ONE-006 |
| Put whoami on `GET /v1/health` | Keep health dumb | HealthTests | — |
| Accept One BaseUrl `http://localhost:8080` without `/api/v1` silently concatenating wrong | Config `One:BaseUrl` **includes** `/api/v1`. Client calls `{base}/me`. Test the join. | one-client does the same | — |
| Log the full Bearer | Log `sub` after whoami, or token prefix. Never the secret. | Secrets | NP-ONE-020 |
| Fail-open when One times out | 503 | Identity fail-closed | — |
| Add FluentAssertions / NSubstitute / MediatR test toolkit because old ModuleTests had them | Stay NUnit + `HttpMessageHandler` + `WebApplicationFactory`, matching `HealthTests` | Isolation of *taste* | — |
| `IClassFixture` hitting a real socket | Per-test `await using var factory` as HealthTests already does | Existing style | — |
| Deep-link `:5175` as Pay’s homepage | Later SPA starts OIDC from Pay origin; Zitadel redirects to login with `authRequest` | NP-ONE-005 | NP-ONE-005 |

---

## 5. Sequence of PRs / commits (after this analysis)

Do this **after** this paper. Do not implement in the same change set as the paper unless a later instruction says so.

The assignment order is **whoami, then authz, then money**. Money is **not** first connect. It is the next *program* after S0 is far enough.

### 5.1 Suggested commits (small, reviewable)

**Commit 0 — this paper (docs only).** `plans/012-one-to-pay/10-dogfood-and-tests.md`. No `Program.cs` change.

**PR / commit 1 — whoami (connected, minimum).**

Touch:

- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` (or a small file it calls): named `HttpClient` `"one"`, config `One:BaseUrl`, `GET /v1/whoami`.
- `appsettings.json` / `appsettings.Development.json`: `One:BaseUrl` default `http://localhost:8080/api/v1`. Missing value must **not** fail boot; whoami then 503.
- `tests/Lazuar.Pay.Tests/WhoamiTests.cs`
- `tests/Lazuar.Pay.Tests/FakeOneHandler.cs` (or nested type)
- Expand `IsolationTests.cs` with the extra greps in §7.3
- `packages/pay-spec/main.tsp`: `Whoami` interface under `/v1`
- `apps/lazuar-pay/README.md`: curl snippet pointing at §6 of this paper (short), BaseUrl, 8081 vs One 8080

Do not: EF, login, ops, SPA, authz route, webhooks, checkout.

Tracker flips when P1–P6, P10, P11, P13 are true (not when the route exists untested):

| ID | After whoami PR | Notes |
|----|-----------------|-------|
| NP-ONE-003 | `doing` → `done` **only** if tests prove Bearer is forwarded and id_token is not substituted | Partial if SPA still does not exist — still mark `done` for **Pay API** behavior; SPA sending the right token is NP-ONE-002 later |
| NP-ONE-006 | `doing` → **not** full `done` | Whoami *calls* `/me`. Chrome, JIT discipline, “do not hammer” on a SPA are later. Flip to `done` only when Pay’s only membership read is `/me` and tests lock it. First whoami PR: Notes “whoami calls /me; SPA not yet.” Status `doing` is honest. If you need a binary: `done` on the **server** job, Notes “no SPA.” Prefer `doing` until a client exists. **Recommendation: `doing`.** |
| NP-ONE-007 | `doing` | Header is forwarded as hint; path SoT is proven in authz PR. |
| NP-ONE-008 | `doing` | Roles taken from `/me` JSON, not JWT. |
| NP-ONE-020 | `doing` | Host still holds no PAT; add a test that config keys are only BaseUrl (+ later client_id). |

Do **not** flip NP-ONE-001, 002, 004, 005, 009–022.

**PR / commit 2 — optional authz (connected, complete).**

Touch:

- `POST /v1/orgs/{orgId}/authz/check`
- `AuthzTests.cs`
- pay-spec
- FakeOneHandler script for POST

Tracker:

| ID | After authz PR |
|----|----------------|
| NP-ONE-015 | `doing` — the **call** exists. “Before merchant admin routes” is true only once those routes exist. Notes: “member check route proven; no admin routes yet.” Do not mark `done` until a real admin route (keys, refund, product write) uses it. |
| NP-ONE-007 | `doing` → `done` if path vs header test is green |
| NP-ONE-008 | `doing` → `done` if we never parse Zitadel roles and tests grep the claim string out of Pay source |

**PR / commit 3 — money (NOT this DoD; listed so sequencing is honest).**

Only after more S0 (011/12 steps 1–7) is actually lived: at least a merchant who can sign in through One and whose tenant id Pay uses as `org_id`. Money PRs are NP-CAT, NP-CHK, NP-GW, NP-FUL, NP-MON, NP-DOC, NP-API-001. They use whoami/authz as **middleware on `/v1/checkouts`**, not as the feature.

Do not start money in the same PR as whoami. Do not add a `charges` table “while we are in Program.cs.”

### 5.2 Ordered implementation steps (the build list after this analysis)

This is the implementer’s punch list. Still not the code.

1. Keep `HealthTests` and current `IsolationTests` green after every step. If whoami registration breaks health-without-One, you failed F15.
2. Add `One:BaseUrl` configuration. Default in Development: `http://localhost:8080/api/v1`. Production: required env, no localhost default in shipped compose until you mean it.
3. `AddHttpClient("one")` with timeout **5s**, BaseAddress from config (trim, ensure trailing slash policy documented: BaseUrl has no trailing slash; code joins with `"/me"`).
4. Map `GET /v1/whoami`. Read `Authorization`. If missing/invalid scheme → 401. Create `HttpRequestMessage` GET `me`, copy Authorization, copy `X-Lazuar-Tenant-Id` if present, copy `X-Request-Id` if present (or mint one). `SendAsync`. Map status as §2.1. Deserialize One JSON **snake_case** (case-insensitive parse is acceptable; emit snake_case). Map `tenants` → `orgs` with `org_id`.
5. Do not add authentication middleware that challenges on `/health`. If you add a global auth filter, exclude health.
6. Write `FakeOneHandler`: records requests; dictionary of scripted responses by `(method, path)`; default 404 so unexpected One calls fail tests.
7. Write `WhoamiTests` (§7.4). Must inject handler via `ConfigurePrimaryHttpMessageHandler` on the `"one"` client (or equivalent `HttpMessageHandler` seam). Must **not** mock an application interface as the only proof.
8. Expand `IsolationTests` (§7.3).
9. Grow pay-spec whoami. `task pay:spec`. Do not touch `packages/api-spec`.
10. README: how to curl; how **not** to point ops at 8081; One must occupy 8080 for live dogfood.
11. Stop. Open PR 1.
12. Map `POST /v1/orgs/{orgId}/authz/check`. Allow-list relations. Build One body. Map status as §2.2.
13. `AuthzTests` (§7.5).
14. Grow pay-spec. Stop. Open PR 2.
15. **Do not** implement money here. Next program: remaining S0 (SPA, `:5175`, invite, key) **or** a later 012 paper. 011/12 is the order for the *product* loop; first connect only unlocked “Pay can see One humans.”

### 5.3 What a reviewer should reject on sight

- Files under `apps/lazuar-api/` in the whoami PR (except unrelated merge noise).
- `packages/api-spec/**` diffs.
- `apps/lazuar-ops/**` env pointing at 8081.
- New `LoginPage` in any Pay host.
- `Microsoft.AspNetCore.Identity`, `AddIdentity`, cookie auth for merchants.
- `OpenFga.Sdk` package on Lazuar.Pay.csproj.
- `Zitadel` client package used to list members.
- ProjectReference to `packages/api-types-dotnet` (old).
- `MediatR` package.

---

## 6. Manual curl script (live One local token)

This is the **human** proof. It is not CI. It requires One’s laptop loop, not staging PASSED.

### 6.1 Topology for the live run

```text
One API     :8080   GET /api/v1/me
Pay         :8081   GET /v1/whoami
lazuar-app  :5174   start login from here (not :5175 as a homepage)
lazuar-login:5175   password / TOTP UI
Zitadel     :8085   authority (Pay does not call it)
```

Do **not** start `apps/lazuar-api` (old Pay) on 8080 at the same time. One owns 8080 for this proof.

Do **not** start `apps/lazuar-ops`. Do **not** export `VITE_API_URL=http://localhost:8081`.

### 6.2 Bring One up (One repo)

From `/Users/akmalfirdaus/Code/lazuar/lazuar-one`:

```bash
cp .env.example .env
./scripts/bootstrap-local.sh
pnpm install
# three long-running processes:
pnpm login:dev    # :5175
pnpm api:dev      # :8080
pnpm app:dev      # :5174
```

Prove One, not staging:

```bash
curl -sf http://localhost:8080/health
curl -sf http://localhost:8080/health/ready   # must not contain "skipped": true
```

Sign in at **http://localhost:5174** as `ada@acme.test` / `Password1!` (seed). Create a workspace if `GET /me` has empty `tenants`. Product login UI is `:5175` reached **from the app**, not bookmarked.

### 6.3 Take a token

**Path A — user JWT (preferred for whoami + authz).**

In the browser on `:5174`, Network tab, any `GET /api/v1/me` or One API call. Copy the `Authorization: Bearer eyJ…` **access_token**. It must be a JWT (three segments). It must **not** be the `id_token`. Opaque access tokens mean a Console leftover app — recreate via One apps API (R3), do not “fix” Pay to accept opaque.

```bash
export ACCESS_TOKEN='eyJ…'   # never commit
```

**Path B — API key (whoami only; authz-as-self will 400 without user_id).**

In lazuar-app → Settings → API keys, mint with explicit scopes e.g. `tenant:read` (and `authz:check` only if you will pass a `user_id` later). Copy `lzr_sk_…` once.

```bash
export ACCESS_TOKEN='lzr_sk_…'   # never commit
```

### 6.4 Control: One `/me` still works

```bash
export ONE_BASE=http://localhost:8080/api/v1
export PAY_BASE=http://localhost:8081/v1
export TENANT_HINT=   # optional UUID of Ada’s workspace

echo "== One /me (control) =="
curl -sS -D - "$ONE_BASE/me" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Accept: application/json" \
  ${TENANT_HINT:+-H "X-Lazuar-Tenant-Id: $TENANT_HINT"} \
  -o /tmp/one-me.json
echo
cat /tmp/one-me.json
echo
```

Expect **200**. Note `user_id` and `tenants[0].id` (this is Pay `org_id`).

Unauthenticated control:

```bash
curl -sS -o /dev/null -w "one /me no bearer → %{http_code}\n" "$ONE_BASE/me"
# expect 401
```

### 6.5 Pay health (One may be up; health must not care)

```bash
# Pay process:
#   cd /Users/akmalfirdaus/Code/lazuar/lazuar-pay
#   task pay:dev
# One:BaseUrl=http://localhost:8080/api/v1

echo "== Pay health =="
curl -sS "$PAY_BASE/../health"   # http://localhost:8081/health
curl -sS "http://localhost:8081/health"
curl -sS "http://localhost:8081/v1/health"
```

Expect `{ "status": "ok" }` without a Bearer.

### 6.6 Pay whoami — pass

```bash
echo "== Pay /v1/whoami =="
curl -sS -D - "$PAY_BASE/whoami" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Accept: application/json" \
  ${TENANT_HINT:+-H "X-Lazuar-Tenant-Id: $TENANT_HINT"} \
  -o /tmp/pay-whoami.json
echo
cat /tmp/pay-whoami.json
echo
```

**Pass:** HTTP 200. `user_id` equals `/tmp/one-me.json` `user_id`. Each `orgs[].org_id` equals some `tenants[].id` in the One body. If `TENANT_HINT` matched a membership, `active_org_id` equals that hint (One echoed it). JSON is snake_case (`user_id`, not `userId`).

**jq check (optional):**

```bash
python3 - <<'PY'
import json
one=json.load(open("/tmp/one-me.json"))
pay=json.load(open("/tmp/pay-whoami.json"))
assert pay["user_id"]==one["user_id"]
one_ids=sorted(t["id"] for t in one.get("tenants") or [])
pay_ids=sorted(o["org_id"] for o in pay.get("orgs") or [])
assert one_ids==pay_ids, (one_ids, pay_ids)
print("whoami maps One tenants → Pay orgs: OK", pay_ids)
PY
```

### 6.7 Pay whoami — fail cases you must run once

```bash
echo "== no bearer =="
curl -sS -o /dev/null -w "%{http_code}\n" "$PAY_BASE/whoami"
# expect 401

echo "== garbage bearer =="
curl -sS -o /dev/null -w "%{http_code}\n" \
  -H "Authorization: Bearer not-a-token" \
  "$PAY_BASE/whoami"
# expect 401 (One 401 mapped)

echo "== id_token must not be sent; if you only have id_token, stop and get access_token =="
# If you deliberately send a known id_token:
# curl ... -H "Authorization: Bearer $ID_TOKEN" → 401 from One → Pay 401
```

### 6.8 Optional authz (after commit 2)

```bash
ORG_ID="$(python3 -c 'import json; print(json.load(open("/tmp/one-me.json"))["tenants"][0]["id"])')"
echo "ORG_ID=$ORG_ID"

echo "== member check (self) =="
curl -sS -D - -X POST "$PAY_BASE/orgs/$ORG_ID/authz/check" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json" \
  -d '{"relation":"member"}'
# expect 200 {"org_id":"...","relation":"member","allowed":true}

echo "== header must not override path =="
FAKE=00000000-0000-0000-0000-000000000001
curl -sS -D - -o /dev/null -X POST "$PAY_BASE/orgs/$FAKE/authz/check" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "X-Lazuar-Tenant-Id: $ORG_ID" \
  -H "Content-Type: application/json" \
  -d '{"relation":"member"}'
# expect 403 (Ada is not a member of the fake org). Must NOT 200 because header was the real org.

echo "== One façade directly (control) =="
curl -sS -X POST "$ONE_BASE/tenants/$ORG_ID/authz/check" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"relation\":\"member\",\"object\":{\"type\":\"tenant\",\"id\":\"$ORG_ID\"}}"
# expect {"allowed":true}
```

API key without `user_id` on One’s authz is **400**. If `ACCESS_TOKEN` is `lzr_sk_`, skip this subsection or expect Pay to surface One’s 400. That is honest. Do not have Pay forge a `user_id` from the key GUID (One rejects that).

### 6.9 503 when One is down

Stop One API. Repeat whoami with a still-valid-looking Bearer.

```bash
curl -sS -D - -o /dev/null -w "%{http_code}\n" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  "$PAY_BASE/whoami"
# expect 503 (or 502). Never 200.
```

Start One again. Health on Pay should have stayed 200 the whole time.

### 6.10 Script shape (copy-pasteable)

```bash
#!/usr/bin/env bash
# Laptop proof: One token → Pay /v1/whoami.
# Not CI. Not staging PASSED. Never commit tokens.
set -euo pipefail
ONE_BASE="${ONE_BASE:-http://localhost:8080/api/v1}"
PAY_HOST="${PAY_HOST:-http://localhost:8081}"
: "${ACCESS_TOKEN:?export ACCESS_TOKEN=... from :5174 Network tab (access_token) or lzr_sk_}"

echo "== One health =="
curl -sf "${ONE_BASE%/api/v1}/health" >/dev/null

echo "== Pay health (no bearer) =="
curl -sf "$PAY_HOST/health" | grep -q ok
curl -sf "$PAY_HOST/v1/health" | grep -q ok

echo "== One /me =="
curl -sfS "$ONE_BASE/me" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Accept: application/json" | tee /tmp/one-me.json >/dev/null

echo "== Pay /v1/whoami =="
curl -sfS "$PAY_HOST/v1/whoami" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Accept: application/json" | tee /tmp/pay-whoami.json >/dev/null

python3 - <<'PY'
import json, sys
one=json.load(open("/tmp/one-me.json"))
pay=json.load(open("/tmp/pay-whoami.json"))
if pay.get("user_id") != one.get("user_id"):
    sys.exit(f"user_id mismatch one={one.get('user_id')!r} pay={pay.get('user_id')!r}")
one_ids=sorted(t["id"] for t in one.get("tenants") or [])
pay_ids=sorted(o["org_id"] for o in pay.get("orgs") or [])
if one_ids != pay_ids:
    sys.exit(f"org map mismatch one={one_ids} pay={pay_ids}")
print("PASS connected whoami", {"user_id": pay["user_id"], "orgs": pay_ids})
PY
```

Save later as `apps/lazuar-pay/scripts/whoami-dogfood.sh` if you want; **not** required to land whoami. Do not add it to `task pay:test`.

---

## 7. Automated tests with fake One (`HttpMessageHandler`)

### 7.1 Why the handler is the seam (and a mock interface is not)

Pay talks to **another origin** over HTTP. The defect we are hunting is “wrong path, wrong header, wrong JSON, fail-open on timeout,” not “a C# interface returned a DTO.”

`IOneClient.GetMeAsync` mocked with NSubstitute can stay green while production still:

- calls `/one/auth/me`,
- sends `id_token`,
- skips `Authorization`,
- parses JWT roles,
- 200s on exception.

`HttpMessageHandler` sits under `HttpClient`. Production mapping code runs. The test scripts One’s bytes. That is the assignment.

Old tree already uses this pattern for outbound HTTP (do **not** copy those projects; copy the *idea*):

- `apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/ResendEmailServiceTests.cs` — `CaptureHandler : HttpMessageHandler`
- CHIP adapter tests — `SequenceHandler`, `RecordingHandler`

New Pay tests stay in `tests/Lazuar.Pay.Tests`, NUnit, `WebApplicationFactory<Program>`, no FluentAssertions required.

### 7.2 FakeOneHandler specification

A test double, not production. Behavior:

1. Inherit `HttpMessageHandler`. Override `SendAsync`.
2. Append every `HttpRequestMessage` to a public `List<HttpRequestMessage>` (or a record of method, path, headers, body). **Do not** dispose the request before the test asserts; copy what you need.
3. Lookup a scripted `HttpResponseMessage` by `(request.Method, request.RequestUri.AbsolutePath)` (ignore host — BaseAddress is `http://one.test/api/v1` or similar).
4. If nothing scripted: return **404** with body `{"title":"unexpected One call","path":"..."}` so the Pay test fails loudly. Do not 200 empty.
5. Helpers:

   ```text
   OnGetMe(status, json)
   OnAuthzCheck(tenantId, status, json)
   OnDown() → throw HttpRequestException
   OnTimeout() → throw TaskCanceledException
   ```

6. Assert helpers:

   ```text
   Last.Authorization == "Bearer " + token
   Last.Path == "/api/v1/me" or "/me" depending on BaseAddress
   Last.Headers["X-Lazuar-Tenant-Id"]
   Never contains header "ZITADEL_PAT"
   Authz body JSON: relation, object.type, object.id
   ```

**BaseAddress contract in tests:** set `One:BaseUrl` to `http://one.test/api/v1`. Then `GET me` is `http://one.test/api/v1/me`. Handler path is `/api/v1/me`. If production wrongly uses BaseUrl `http://one.test` and path `/api/v1/me`, tests should still be written against the **documented** join (`BaseUrl` includes `/api/v1`, relative `me`). One wrong join is a failed test.

**Injection:** `WebApplicationFactory` `WithWebHostBuilder`:

- `ConfigureAppConfiguration` → in-memory `One:BaseUrl=http://one.test/api/v1`
- `ConfigureTestServices` → `AddHttpClient("one").ConfigurePrimaryHttpMessageHandler(() => handler)`

If double-registration fights the production `AddHttpClient("one")`, the implementer must pick a seam that still runs production `HttpClient` **usage** (the GET/POST mapping). Acceptable alternatives:

- production `ConfigurePrimaryHttpMessageHandler` uses `IHttpMessageHandlerFactory` / a registered `HttpMessageHandler` singleton if present;
- tests replace the named client with `RemoveAll` + add one client.

**Not acceptable:** skip `HttpClient` and new-up a fake `IOneGateway` in the endpoint.

`HealthTests` must keep using a **plain** `WebApplicationFactory<Program>()` with **no** handler. Production must boot without a fake and without One. If `AddHttpClient` requires a handler instance, production uses `SocketsHttpHandler`.

### 7.3 IsolationTests expansion (museum lock)

Keep the existing csproj test. Add tests in the same file (or `ForbiddenSurfaceTests.cs`) that read **source** under `src/Lazuar.Pay` (exclude `obj/`, `bin/`):

| Assert | Strings that must not appear in host source or csproj |
|--------|------------------------------------------------------|
| Existing | csproj: `lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api` |
| Login stub | `/one/auth/login`, `one/auth/me` as a route, `lazuar_auth` cookie name |
| Old TypeSpec | `packages/api-spec`, `Lazuar.ApiContracts`, `Lazuar.ApiTypes` (old NSwag namespace) |
| Wrong door | `localhost:5173`, `lazuar-admin` as a destination (allow this paper’s comments? **Prefer to grep `.cs` only**, not `.md`) |
| Engine | `OpenFga`, `Zitadel:Pat`, `ZITADEL_PAT` |
| Second org table | `DbSet<Organization`, `class Organization`, `CREATE TABLE organizations` — if you add SQL later, that is a different paper; first connect has none |

Walk the same parent-directory finder as today so tests run from `bin/Debug/net10.0`.

Do not grep the whole monorepo. Old ops will always contain `/one/auth/login`. The lock is **the focused host**.

### 7.4 Whoami test cases (required)

NUnit. One factory per test (`await using`). Names below are the catalog; keep them as `[Test]` method names or as comments if the implementer splits.

**Health unchanged**

| ID | Test | Expect |
|----|------|--------|
| H1 | `Health_returns_ok` | existing |
| H2 | `V1_health_returns_ok` | existing |
| H3 | `Health_does_not_call_One` | factory with recording handler; GET `/health` and `/v1/health`; handler request list empty |

**Unauthenticated**

| ID | Test | Expect |
|----|------|--------|
| W1 | `Whoami_without_authorization_is_401` | no One call |
| W2 | `Whoami_with_empty_bearer_is_401` | `Authorization: Bearer ` → 401; no One call |
| W3 | `Whoami_with_basic_scheme_is_401` | do not forward Basic to One |

**Happy path (user JWT shape)**

| ID | Test | Expect |
|----|------|--------|
| W4 | `Whoami_forwards_bearer_to_One_me_and_maps_orgs` | Fake 200 One body (fixture below); Pay 200; `user_id` match; `orgs[0].org_id` == One tenant id; `orgs[0].slug/name/role`; no `tenants` property on Pay JSON |
| W5 | `Whoami_captured_request_is_GET_me` | path ends with `/me`; method GET; Accept json |
| W6 | `Whoami_forwards_tenant_hint_header` | send `X-Lazuar-Tenant-Id: <uuid>`; captured One request has the same header |
| W7 | `Whoami_omits_active_org_id_when_One_omits_it` | One JSON without `active_tenant_id` → Pay JSON without `active_org_id` (or null; pick one and spec it — **omit**) |
| W8 | `Whoami_snake_case_payload` | raw body contains `user_id` and `org_id`, not `userId` / `orgId` |

**One error mapping**

| ID | Test | Expect |
|----|------|--------|
| W9 | `Whoami_when_One_401_is_401` | fake 401 RFC 7807; Pay 401; no 200 empty orgs |
| W10 | `Whoami_when_One_403_is_403` | |
| W11 | `Whoami_when_One_5xx_is_503` | fake 500 → Pay 503 |
| W12 | `Whoami_when_One_down_is_503` | handler throws `HttpRequestException` |
| W13 | `Whoami_when_One_times_out_is_503` | `TaskCanceledException` |
| W14 | `Whoami_does_not_retry_me` | fake 500; captured GET `/me` count == 1 |

**Fail-closed / refuse**

| ID | Test | Expect |
|----|------|--------|
| W15 | `Whoami_does_not_send_zitadel_pat` | captured headers have no PAT; Pay config in test has no PAT key |
| W16 | `Whoami_does_not_call_one_auth_me` | path does not contain `/one/auth` |
| W17 | `Whoami_does_not_call_platform_tenants` | |
| W18 | `Whoami_api_key_bearer_is_forwarded` | `Bearer lzr_sk_test`; fake 200 with key-shaped `/me` (`user_id` = key guid, one org, `is_platform_admin: false`) |

**Boot**

| ID | Test | Expect |
|----|------|--------|
| W19 | `Whoami_without_BaseUrl_is_503_not_throw` | in-memory config empty `One:BaseUrl`; GET whoami with Bearer → 503; health still 200 |
| W20 | `Factory_without_One_config_still_serves_health` | existing HealthTests remain valid |

**One `/me` fixture (user):**

```json
{
  "user_id": "user-ada",
  "email": "ada@acme.test",
  "name": "Ada Lovelace",
  "is_platform_admin": false,
  "tenants": [
    {
      "id": "11111111-1111-1111-1111-111111111111",
      "slug": "acme",
      "name": "Acme",
      "role": "owner",
      "status": "active",
      "permissions": []
    }
  ],
  "active_tenant_id": "11111111-1111-1111-1111-111111111111",
  "active_role": "owner"
}
```

Mapped Pay body:

```json
{
  "user_id": "user-ada",
  "email": "ada@acme.test",
  "name": "Ada Lovelace",
  "is_platform_admin": false,
  "orgs": [
    {
      "org_id": "11111111-1111-1111-1111-111111111111",
      "slug": "acme",
      "name": "Acme",
      "role": "owner",
      "status": "active"
    }
  ],
  "active_org_id": "11111111-1111-1111-1111-111111111111",
  "active_role": "owner"
}
```

**One `/me` fixture (API key):**

```json
{
  "user_id": "22222222-2222-2222-2222-222222222222",
  "is_platform_admin": false,
  "tenants": [
    {
      "id": "11111111-1111-1111-1111-111111111111",
      "slug": "acme",
      "name": "Acme",
      "role": "member",
      "status": "active",
      "permissions": ["tenant:read"]
    }
  ],
  "active_tenant_id": "11111111-1111-1111-1111-111111111111",
  "active_role": "member"
}
```

Empty tenants (Ada signed in, no workspace yet) is **valid 200**:

```json
{ "user_id": "user-ada", "is_platform_admin": false, "tenants": [] }
```

Pay: `"orgs": []`. Do not 404.

### 7.5 Authz test cases (required for commit 2)

| ID | Test | Expect |
|----|------|--------|
| A1 | `Authz_without_bearer_is_401` | no One call |
| A2 | `Authz_member_allowed_true_is_200` | fake One 200 `{"allowed":true}`; Pay 200 `allowed: true`, `org_id` path, `relation: member` |
| A3 | `Authz_captured_body_is_tenant_member` | One POST path `/api/v1/tenants/{orgId}/authz/check`; JSON `object.type==tenant`, `object.id==orgId`, `relation==member` |
| A4 | `Authz_allowed_false_is_403` | One `{"allowed":false}` → Pay 403, not 200 |
| A5 | `Authz_One_403_is_403` | foreign tenant façade; Pay 403 |
| A6 | `Authz_path_not_header_is_SoT` | path org B, header org A; captured One URL contains **B**; does not check A |
| A7 | `Authz_unknown_relation_is_400_without_One_call` | body `{ "relation": "payment" }` or `"viewer"` as a *tenant* relation Pay does not allow-list → 400; handler empty. (`viewer` is an **app** relation on One; Pay must not send it on this route.) |
| A8 | `Authz_does_not_send_type_payment` | even if a client JSON includes `"object":{"type":"payment"}`, Pay ignores client object and sends `type=tenant` **or** 400s extra fields. Prefer **ignore client object**; path org is the object. Tight door. |
| A9 | `Authz_One_down_is_503` | fail closed |
| A10 | `Authz_does_not_call_openfga` | captured URI host is `one.test`, not `8090` |
| A11 | `Authz_batch_not_exposed` | `POST /v1/orgs/{id}/authz/batch-check` is 404 in first connect |
| A12 | `Authz_write_not_exposed` | no Pay route contains `authz/write` |

### 7.6 Isolation / refuse test cases

| ID | Test | Expect |
|----|------|--------|
| I1 | existing `Host_csproj_does_not_reference_the_old_api` | |
| I2 | `Host_source_does_not_contain_one_auth_login` | |
| I3 | `Host_source_does_not_contain_lazuar_admin_destination` | `.cs` files |
| I4 | `Host_csproj_does_not_reference_old_api_spec_or_nswag_contracts` | |
| I5 | `Host_source_does_not_parse_zitadel_project_roles` | string `urn:zitadel:iam:org:project:roles` absent from `src/Lazuar.Pay` |
| I6 | `Pay_spec_does_not_import_old_one_routes` | read `packages/pay-spec/main.tsp`; does not import `api-spec` or define `/one/auth/login` |

### 7.7 Tests that must **not** exist yet

Do not add these in first connect; they pull S1 into the room:

- Checkout, Stripe webhook, journal balance, `RCPT-` format
- Invite email
- VIEWER cannot refund (no refund route)
- Buyer magic link
- Live One integration test in `task pay:test`

A separate **optional** `Lazuar.Pay.LocalDogfood` project that is `[Explicit]` / not in default `dotnet test` is allowed later. Not commit 1.

### 7.8 How `task pay:test` must feel

```text
task pay:test
→ HealthTests
→ IsolationTests (expanded)
→ WhoamiTests (fake One)
→ AuthzTests (after commit 2)
```

No Docker. No `localhost:8080`. No network to One. If a developer has One running, tests still use `http://one.test` from in-memory config and never hit 8080.

---

## 8. VIEWER vs `member` vs `app.viewer` (do not confuse them)

011/01 dogfood sentence: “a One-invited **MEMBER** can see ops and a **VIEWER** cannot charge.”

One built-in membership roles (`MembershipRoles.cs`): `owner`, `admin`, `member` only.

One authz:

- On **`type=tenant`**: relations `owner`, `admin`, `member`, `can_view`, `can_manage_members`, `can_manage_tenant`.
- On **`type=app`**: relations `viewer`, `admin` (OIDC application object).

Pay product VIEWER (cannot change keys, cannot refund, cannot charge) is **not** `app.viewer`. Using `app.viewer` would mean “this human can see the Pay OIDC app registration,” which is true of every tenant member via inheritance and says nothing about CHIP keys.

First connect:

- Whoami returns `orgs[].role` as One sent it (`owner|admin|member` or a custom role string).
- Authz proves `member` on `type=tenant`.

Later (NP-ONE-021, S1 ops):

- **See ops:** One `authz/check` `member` (or `can_view`) on the org → allow GET.
- **Charge / change keys / refund:** require `admin` or `owner` (and/or a Pay-local overlay). A One **custom role** named something merchants read as Viewer can map to “member without admin.” Do **not** add FGA type `payment` to encode “can refund.”
- Do not invent a Pay `viewers` table.

NP-ONE-021 stays **out** of first connect because there is nothing to charge.

NP-ONE-022 (invited MEMBER sees ops) stays **out** because there is no ops UI and no invite proof.

---

## 9. Mapping `NP-ONE-001` … `NP-ONE-022` (in / out of first connect)

Wave S0 is 22 rows. First connect is a **subset**. Flip Status in [011/11-checklist.md](../011-new-lazuar-pay/11-checklist.md) only per §5.1 honesty.

Legend: **IN** = this DoD must prove it or a slice of it. **OUT** = still S0/S1 but after connected. **REFUSE-adjacent** = keep refuse; first connect must not violate.

| ID | Feature | In first connect? | What “in” means here | What would be a false `done` |
|----|---------|-------------------|----------------------|------------------------------|
| NP-ONE-001 | Register Pay SPA via `POST /tenants/{id}/apps` (or seed like `lazuar-app`) | **OUT** | Curl whoami does not need a Pay `client_id`. SPA is later S0 step 1. | Seeding a Console SPA and marking 001 done |
| NP-ONE-002 | OIDC code + PKCE; Pay `client_id`; Zitadel authority | **OUT** | Backend forwards a token; it does not run PKCE. | Adding JwtBearer against 8085 and calling that “OIDC” |
| NP-ONE-003 | Send **access_token** as `Authorization: Bearer` | **IN** | Pay forwards the request Bearer to One. Tests lock no id_token swap. | SPA still missing — OK to `done` the **API** behavior with Notes “no SPA yet”; do not `done` if Pay mints a cookie |
| NP-ONE-004 | Register Pay redirects on One app + login `REDIRECT_ALLOWLIST` | **OUT** | No Pay origin login yet | Adding `:8081` to allowlist “just in case” without an SPA |
| NP-ONE-005 | Product login via `:5175`; never `:3005` or `:5173` | **OUT** as a Pay UX. **IN as a refuse lock:** Pay must not link those ports. Isolation grep. | Building a password page on 8081 and calling it “temporary” |
| NP-ONE-006 | `GET /me` for user, tenants, roles, `active_tenant_id` | **IN (call exists)** | Whoami is the `/me` consumer. Status `doing` until a real client uses it without hammering. | Caching `/me` into a Pay users table |
| NP-ONE-007 | Path `{tenantId}` + membership is authz SoT; header is hint | **IN on authz commit** | Path `{orgId}` for authz; header forwarded only on whoami | Authorizing whoami from header without One |
| NP-ONE-008 | Roles from `/me` + `authz/check`, not Zitadel project-role claims | **IN** | Map role from `/me` JSON; grep claim URN out of source | `JwtSecurityToken.Claims["roles"]` |
| NP-ONE-009 | Create workspace = `POST /tenants`; One tenant id **is** Pay `org_id` | **OUT** create. **IN as mapping law:** `org_id` := One tenant id on whoami | Creating `pay_organizations` with a new Guid FK to One |
| NP-ONE-010 | GET/PATCH tenant profile | **OUT** | | |
| NP-ONE-011 | Copy-link invite + pending + revoke + resend | **OUT** | | Homemade invite table in Pay |
| NP-ONE-012 | Accept-invite; non-email path | **OUT** | | |
| NP-ONE-013 | Roster; role change; remove; `GET /me/invites` | **OUT** | | |
| NP-ONE-014 | Mint / list / revoke `lzr_sk_` with explicit scopes | **OUT** | Whoami **accepts** a key if One does. Pay does not mint. | Minting `lzr_sk_` inside Pay crypto |
| NP-ONE-015 | `authz/check` `member` / `admin` / `owner` before merchant admin routes | **IN (optional route)** | The check call is proven. “Before admin routes” waits for those routes → keep Notes; do not full `done` | Proxying full One authz with open types |
| NP-ONE-016 | `authz/batch-check` for chrome | **OUT** | A11: 404 | |
| NP-ONE-017 | HMAC webhooks `member.*`, `tenant.*`, `api_key.revoked` | **OUT** | | Tailing Zitadel |
| NP-ONE-018 | Stop charges on `tenant.suspended` | **OUT** (no charges) | | |
| NP-ONE-019 | Provision Pay catalog/ledger rows on `tenant.created` | **OUT** | Whoami must **not** insert catalog rows | |
| NP-ONE-020 | Pay holds only OIDC `client_id`, `lzr_sk_`, One-webhook HMAC | **IN as negative** | First connect holds **BaseUrl** only. No PAT. Tests. `client_id` / HMAC come later. | Putting `ZITADEL_PAT` in Pay appsettings “for local” |
| NP-ONE-021 | VIEWER cannot charge, change keys, or refund | **OUT** | No charge route. §8 mapping. Isolation must not add FGA `payment`. | Checking `app.viewer` and calling it VIEWER |
| NP-ONE-022 | Invited MEMBER can see merchant ops | **OUT** | No ops. Copy-link is later. | Pointing old ops at 8081 and logging in as Ada |

**Count:** IN (required whoami): **003, 006 (call), 008 (negative), 009 (mapping law), 020 (negative), 005 (refuse lock).** IN (authz commit): **007, 015 (call).** OUT: **001, 002, 004, 010–014, 016–019, 021, 022.** 001–007 in 011/12 step 1–5 are **not** all first connect.

### 9.1 011/12 steps vs this paper

| 12 step | IDs | First connect? |
|---------|-----|----------------|
| 1 Register Pay SPA | 001, 002, 004 | **No** |
| 2 Sign-in `:5175`. `GET /me` | 003, 005, 006 | **Partial:** 003/006 call yes; 005 UX no |
| 3 Create workspace / org_id | 007, 009 | **Partial:** mapping yes; POST /tenants no |
| 4 Copy-link invite | 011, 012, 022 | **No** |
| 5 Mint `lzr_sk_`; authz member | 014, 015 | **Partial:** 015 call optional; 014 no |
| 6 One webhooks | 017, 018 | **No** |
| 7 Stop (no SCIM, no FGA types, no npm, no SKU) | XX-015, 021, 022 | **Yes as refuse** — first connect must honor the stop |
| 8–12 money | CAT/CHK/GW/… | **No** |

If someone flips 12-tracker steps 1–2 to `done` after whoami, revert the flip. Step 2’s note is “Access token as Bearer” — the **server** part can be noted; the step as a whole includes sign-in via `:5175`.

---

## 10. Fail locks from 03 (must stay true during first connect)

Copy of 011/03 and 011/12 pass/fail locks, with **what first connect must do about each**.

| Lock | Related IDs | First-connect obligation | How it fails in a whoami PR |
|------|-------------|--------------------------|-----------------------------|
| No Pay password form | NP-XX-007 | No login route, no password field, no BCrypt, no `POST /one/auth/login` | Stubbing login so ops works |
| No second org table | NP-XX-014 | No `organizations` entity. `org_id` is One tenant id in JSON only | “We’ll cache tenants in Postgres so we don’t call /me” |
| Buyer is not a Zitadel human | NP-XX-013, NP-CHK-007 | Do not add payer signup. Do not call InviteUser. Whoami is **staff** | Creating a user for `buyer@` to test whoami |
| Setup session is not counted as paid | NP-GW-008 | Do not add Stripe setup helpers in this PR | Drive-by payments code |
| Receipt is not titled Tax Invoice; number is not a UUID | NP-DOC-002, NP-DOC-003, NP-XX-003 | Do not add document types | Drive-by `TaxInvoice` class |
| Webhook retry does not double-journal | NP-GW-006 | No webhook receiver yet | |
| Merchant is not sent to `lazuar-admin` | NP-ONE-005, NP-XX-018 | Isolation grep `:5173` / `lazuar-admin` in Pay `.cs`. README says `:5174`/`:5175` for identity, 8081 for Pay API | README “open admin to mint a token” |

If a lock fails, 011/12 says: do not mark steps 1–12 `done`. This paper adds: do not mark **connected** passed.

---

## 11. Ports, processes, and the 8080 trap

| Process | Port | First-connect live dogfood | First-connect CI |
|---------|------|----------------------------|------------------|
| One API | 8080 | **Up** | **Down** / unreferenced |
| Old Pay API (`apps/lazuar-api`) | 8080 | **Down** (collision) | Down |
| New Pay | 8081 | **Up** | In-process `WebApplicationFactory` |
| lazuar-app | 5174 | Up if you need a JWT | Down |
| lazuar-login | 5175 | Up if you need a JWT | Down |
| lazuar-admin (One) | 5173 | **Do not open for merchants** | Down |
| lazuar-ops (old) | 3003 | **Down** | Down |
| old lazuar-admin | 3005 | **Down** (also One Login V2) | Down |
| Zitadel | 8085 | Up for live JWT | Down |
| OpenFGA | 8090 | Up if One API needs it live | Down |

**Collision:** 011 Pay README says 8081 so old API can keep 8080. One README says One API is 8080. Both are true in their repos. **On one laptop, 8080 is a single process.** Connected dogfood chooses **One**. Old Pay API is off. That is another reason not to point old ops at 8081: ops wants the old API, which is not there.

Pay Development config `One:BaseUrl=http://localhost:8080/api/v1` is correct **only** when One occupies 8080. If a developer still runs old Pay on 8080, whoami will call old `/me` or 404 and look like One is broken. README must say this in one sentence.

---

## 12. Do not implement 261+ on the old API; do not hook old TypeSpec gen

### 12.1 Issues 261–334

`issues/` contains 74 files from `261-p2-b07-i17-reset-password-is-an-email-oracle.md` through `334-p2-b10-x32-clock-invoice-reminder-utc-date-vs-dueat.md`. They were filed against the cathedral (`plans/009-bugs`, HEAD notes around `297ba98` / later `e7bb07b0`). Identity examples:

- 261 — reset-password email oracle on **old** `POST /one/auth/reset-password`
- 262 — API key prefix parse
- 270 — members IDOR status codes
- Cookie vs Bearer, genesis password, portal logout, …

**Binding (011 README):** not a plan to implement 261–334 on the old tree. First connect PRs that “quickly fix login while we are here” are out of scope. If a whoami engineer discovers an old-API bug, they file it (if missing) and **leave**. Identity SoT is lazuar-one; money SoT will be new Pay.

### 12.2 Old TypeSpec / NSwag

| Artifact | Role | First connect |
|----------|------|----------------|
| `packages/api-spec/` (`docs-one.tsp`, `modules/one/routes.tsp` with `/one/auth/login`) | Old hub contract | **Do not edit** for whoami |
| `packages/api-spec/honesty-allowlist.yaml` | Old honesty product | **Do not add** Pay whoami |
| `packages/api-types-dotnet` + `nswag.json` from `../api-spec/dist/openapi.yaml` | Old C# DTOs | **Do not reference** |
| `packages/api-types-ts` | Old ops/portal types | **Do not reference** from focused host |
| `packages/pay-spec/` | Focused Pay contract on 8081 | **Grow** `/v1/whoami` and optional authz |
| One `packages/api-spec` (sibling repo) | One’s contract | **Read**; do not vendored-copy into Pay api-spec |

Pay may **hand-copy** the few JSON shapes it consumes (`MeResponse`, `AuthzCheckRequest`) as internal records in `Lazuar.Pay`. That is Consumer-0. It may **not** add a git submodule of One’s NSwag output as a way to drag Identity DTOs into money. If a later paper wants generated One clients in C#, that is a new dependency with a review; it is not required to pass whoami.

---

## 13. NP-XX rows that first connect must not violate

All 24 refuse rows stay `refuse`. First connect can *break* them by accident. Explicit watchlist:

| ID | Risk in whoami PRs |
|----|-------------------|
| NP-XX-007 | Password form / login stub / Identity package |
| NP-XX-008 | Dual JWT vs membership roles (local JWT roles + `/me`) |
| NP-XX-013 | Creating Zitadel humans for tests |
| NP-XX-014 | Organizations table |
| NP-XX-015 | Sending `type=payment` or asking One to add it |
| NP-XX-016 | authz/write |
| NP-XX-017 | PAT / FGA admin in Pay config |
| NP-XX-018 | lazuar-admin links |
| NP-XX-021 | Blocking on npm |
| NP-XX-022 | Hosted SKU / SCIM as next ticket |
| NP-XX-023 | `/platform/tenants` |
| NP-XX-024 | Parsing project-role URN |
| NP-XX-003 | “While we’re here” Tax Invoice types |
| NP-XX-009 | Inbox/events between whoami and authz in-process |

---

## 14. Suggested Pay config and route table (specification, not code)

### 14.1 Config

```json
{
  "One": {
    "BaseUrl": "http://localhost:8080/api/v1"
  }
}
```

Env: `One__BaseUrl`. Timeout 5s on the named client. No PAT keys. No `Zitadel` section in first connect.

### 14.2 Routes on 8081 after connected

| Method | Path | Auth | One call | Status |
|--------|------|------|----------|--------|
| GET | `/health` | none | none | 200 `{status:ok}` |
| GET | `/v1/health` | none | none | 200 `{status:ok}` |
| GET | `/v1/whoami` | Bearer | `GET /me` | 200 / 401 / 403 / 503 |
| POST | `/v1/orgs/{orgId}/authz/check` | Bearer | `POST /tenants/{orgId}/authz/check` | 200 / 400 / 401 / 403 / 503 |

Not mapped: `/one/*`, `/api/v1/*`, `/login`, `/me`, `/v1/checkouts`.

### 14.3 Header names

| Header | Who | Role |
|--------|-----|------|
| `Authorization: Bearer` | Client → Pay → One | Authn |
| `X-Lazuar-Tenant-Id` | Client → Pay → One `/me` | Hint only |
| `X-Tenant-Id` (old ops) | **Ignore** | Do not treat as SoT; do not document |
| `X-Request-Id` | Optional forward | One problem details use `request_id` |

---

## 15. Risks and honesty leftovers

1. **`GET /me` is a write.** Whoami in a dashboard poll will JIT-join on One. First connect does not add polling. Document it in README.
2. **One local FGA disabled** (`OpenFga:Enabled=false`) makes `authz/check` membership-derived and often `allowed: true`. Live laptop authz is a weak proof of deny. **Tests** must fake `{allowed:false}` and One 403. Do not claim NP-ONE-015 `done` from a laptop where FGA is off.
3. **Staging NOT PASSED.** Connected on a laptop is not “Pay merchants can live on a shared host.” 017-08 A8/OPS-03 still apply to *shared* dogfood.
4. **API key whoami ≠ user whoami.** Key `user_id` is the key GUID. Do not look up memberships by that GUID in a future Pay table.
5. **Language tension.** 011/05 says Go. This host is C#. First connect does not resolve that. It must still refuse MediatR-as-architecture. A later Go rewrite re-proves whoami with the same curl and the same pass/fail table.
6. **Cookie vs Bearer.** Old Pay preferred cookies. One SPAs send Bearer. Pay whoami is Bearer-only. Do not add `lazuar_auth` “for compatibility.”
7. **Problem details casing.** One 401s are RFC 7807 snake_case (`request_id`). Pay may pass through or emit its own. Pick one in implementation; tests should accept Pay 401 without requiring One’s body verbatim. Do not 200.
8. **CORS.** First connect is curl. Do not add `AllowAnyOrigin` “for the SPA that does not exist.” When a SPA appears, allowlist its origin explicitly (NP-ONE-004).
9. **Rate limits.** One rate-limits `/me` and authz. Pay whoami is not a hot path. Do not retry storms.
10. **False completion.** The most likely process failure: mark 011/12 steps 1–7 done because Ada’s token printed on 8081. This paper exists so that is a reject.

---

## 16. Tracker flip rules (repeat, operational)

1. Flip Status only in [011/11-checklist.md](../011-new-lazuar-pay/11-checklist.md) (and 12 for slice steps).
2. After whoami PR: do **not** set S0 count `done=22`. Update Notes. `doing` on 003/006/008/020 is the honest set; 003 may be `done` for API forwarding if reviewers agree.
3. After authz PR: 007 can `done`; 015 stays `doing` until an admin route uses it.
4. Dogfood `Y` rows that this paper does not prove stay `todo`.
5. Refuse rows stay refuse.
6. Do not create a parallel tracker in 012. This paper maps; 11 is living.

---

## 17. Printable definition of done (reviewer card)

**Connected (whoami) is PASSED when:**

- [ ] `GET /v1/whoami` + One access_token (or `lzr_sk_`) → 200, `org_id`s = One tenant ids
- [ ] No Bearer → 401; health → 200; One down → 503
- [ ] Tests use `HttpMessageHandler` fake One; `task pay:test` without One process
- [ ] IsolationTests green; no `/one/auth/login`; no old api-spec/NSwag; no MediatR; no org table
- [ ] Pay does not hold PAT/FGA admin; does not parse Zitadel project roles; does not send id_token
- [ ] Old ops not pointed at 8081; no npm wait; no 261–334 work; pay-spec not api-spec
- [ ] Manual script §6 run once on a laptop (not CI)

**Connected (whoami + authz) is PASSED when, additionally:**

- [ ] `POST /v1/orgs/{orgId}/authz/check` `{relation:member}` → One check `type=tenant`
- [ ] `allowed true` 200; `allowed false` 403; One 403 403; header ≠ path SoT
- [ ] No generic type proxy; no batch-check; no authz/write

**S0 is not PASSED. S1 is not PASSED. 01 dogfood sentence is not PASSED.**

---

## 18. After connected (so this paper does not swallow the program)

Ordered, still analysis, still not this DoD:

1. **S0 remainder (011/12 steps 1–7):** Pay SPA via One apps API; PKCE; `:5175`; create/pick tenant; copy-link invite; mint scoped key; HMAC `member.*` / `tenant.suspended`; **stop** (no SCIM, no custom FGA types, no npm, no SKU).
2. **S1 money (steps 8–12):** BYOK, product, hosted buyer **without** One account, webhook+journal+`RCPT-` one txn, MEMBER sees ops, VIEWER cannot charge.
3. **V1 / soon / later** per 011/11.

Each of those deserves its own tests. Money tests will still fake **One** for staff routes and fake **CHIP/Stripe** for rails. They will not call live PSPs in `task pay:test`. Buyer tests will fail if a Zitadel login page is required (NP-CHK-007).

The first connection exists so those later tests have a real `org_id` (One tenant id) to hang money on — **without** a Pay-side people directory.

---

## 19. Appendix — One JSON and paths (copy from live TypeSpec, this SHA)

**Whoami upstream**

```
GET http://localhost:8080/api/v1/me
Authorization: Bearer <access_token|lzr_sk_>
X-Lazuar-Tenant-Id: <optional hint>
```

**Authz upstream**

```
POST http://localhost:8080/api/v1/tenants/{tenantId}/authz/check
Authorization: Bearer <access_token>
Content-Type: application/json

{"relation":"member","object":{"type":"tenant","id":"{tenantId}"}}
```

Response `{ "allowed": true|false }` or 403 ProblemDetails.

**Pay**

```
GET http://localhost:8081/v1/whoami
POST http://localhost:8081/v1/orgs/{orgId}/authz/check
```

**Demo user (One seed, local only):** `ada@acme.test` / `Password1!`. Not a production secret. Not a Pay user row.

---

*End of program paper. Do not implement from this file. Do not condense this file into a checklist that drops the fail rows. The living Status cells remain [011/11-checklist.md](../011-new-lazuar-pay/11-checklist.md).*
