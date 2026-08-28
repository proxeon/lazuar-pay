# 08 — Headless vs SPA: merchant :5178 and checkout :5179 as clients of `/v1`

**Date:** 28 August 2026  
**Program:** [020-evals](./README.md) — Production-ready Pay: second-app integration gaps  
**Slice:** Merchant Vite **:5178** and checkout Vite **:5179** as clients of `/v1` — versus a headless integrator that never uses those SPAs.  
**Type:** Uncondensed evaluation. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) Status cells. **Not** a project reference into `apps/lazuar-api`. **Not** a retarget of `lazuar-ops` (:3003) or `lazuar-portal` (:3004).  
**Audience:** the parent 020 judgment (`00-evaluation.md`, written after `01`–`10`) and anyone about to treat “open :5178 / :5179” as the only way to take money.

**Bezos door (this slice’s lock):** own UI should be a client of `/v1`, not of `internal/`. Verify whether merchant/checkout only use public HTTP or import host internals (**they must not**). Linux is the room (one Pay binary). This paper does not relitigate splitting Notify/Audit into processes.

Live files on **this SHA** are authority. [019-evals](../019-evals/README.md) audited the 018 hosted cashier and extracted [issues/002](../../issues/002/README.md) (001–080, marked resolved on this SHA). 019’s parent already said there is still no machine key and no outbound `payment.completed`. This paper re-opens the two Vite apps **and** the `/v1` doors they actually call, after the 002 UI commits on `fix/002-pay-host-bugs`. If 019 disagrees with live files, live files win.

Sibling 020 reports own the rest: [01](./01-public-http-api.md) `/v1` shape, [02](./02-machine-keys-m2m.md) `lzr_sk_`, [03](./03-outbound-webhooks.md) Plane C, [05](./05-identity-authz-tenancy.md) MemberGate, [06](./06-host-production.md) CORS/compose, [09](./09-spec-docs-sample.md) pay-spec/sample. This file pins **whether a stranger can take money without cloning these Vite trees**, and whether first-party UI is an honest `/v1` client.

Standing law this paper must not weaken:

| Lock | Meaning here |
|------|----------------|
| One Pay binary, one Pay database | Vite is not a second Pay. SPAs are browsers. |
| Bezos is the **door** (`/v1`); Linux is the **room** (in-process) | Merchant/checkout `fetch` `/v1`. They do not `import Lazuar.Pay`. |
| Pay talks to One over HTTP | Merchant may call One `/api/v1` for **workspace create**. Pay whoami is a façade of One `/me`. |
| Buyers are not One humans | Checkout has no OIDC, no Bearer, no `:5175`. |
| Receipt ≠ tax invoice | Checkout “Payment received” and merchant Receipts table say Official Receipt. |
| Steal HTTP **judgment** from Hub; Hub stays museum | `examples/hub-cashier-next` is **not** the Pay second-app sample. |
| IsolationTests stay red on cathedral strings | Vite `package.json` must not contain `@repo/api-types-ts`. |

---

## Coordinates

Recorded at write time. Re-open files on a later SHA before treating a line as still true.

| | |
|---|---|
| Repo | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` |
| Branch | `fix/002-pay-host-bugs` |
| HEAD | `6d730d15` — `fix(pay): store per-org One webhook secrets` |
| Today | 28 August 2026 |
| Pay host | `apps/lazuar-pay` → **http://localhost:8081** |
| Merchant Vite | `apps/lazuar-pay-merchant` → **http://localhost:5178** (`strictPort`) |
| Checkout Vite | `apps/lazuar-pay-checkout` → **http://localhost:5179** (`strictPort`) |
| Merchant preview | **http://localhost:4178** |
| Checkout preview | **http://localhost:4179** |
| One HTTP | **http://localhost:8080/api/v1** (`VITE_ONE_API_URL`) |
| One product login | **:5175** (README claim; issuer is Zitadel **:8085**) |
| Pay Postgres | **5435**, database `lazuar_pay` |
| 019 HEAD (historical) | `9f04ad58` — `fix(pay-ui): match receipts table to pay-link chrome` |
| 002 index | 001–080 marked **resolved** on this branch |

002 UI commits that this paper re-reads (not 019’s SHA):

| SHA | Subject |
|-----|---------|
| `1479b039` | `fix(pay-ui): stop org shell spinning and lying empty tables` (035–038, 046–047) |
| `21b3fe63` | `fix(pay-ui): hosted checkout honesty for buyers` (048, 051–061) |
| `66bb1cf9` | `fix(pay-ui): trust host rails and stop baking localhost URLs` (039–043, 050) |
| `d4519da0` | `fix(pay-ui): isolate silent renew and unblock stuck sessions` (044) |
| `1974ac10` | `fix(pay): add Pay images without retargeting Hub compose` (Dockerfiles, compose profile) |

`11e0dce4` only marked issues 035–061 resolved in `issues/002`. Live files below are the proof, not the YAML `status: resolved` line.

---

## Files opened

Nothing was implemented. The following were opened in full or in the cited ranges. Live files first.

### Merchant Vite (authority for staff client)

- `apps/lazuar-pay-merchant/package.json`
- `apps/lazuar-pay-merchant/vite.config.ts`
- `apps/lazuar-pay-merchant/README.md`
- `apps/lazuar-pay-merchant/.env.example`
- `apps/lazuar-pay-merchant/.env` (gitignored; local dogfood — `VITE_ZITADEL_CLIENT_ID` present, **`VITE_CHECKOUT_ORIGIN` absent**)
- `apps/lazuar-pay-merchant/Dockerfile`
- `apps/lazuar-pay-merchant/index.html`
- `apps/lazuar-pay-merchant/silent-renew.html`
- `apps/lazuar-pay-merchant/scripts/register-spa.sh`
- `apps/lazuar-pay-merchant/src/main.tsx`
- `apps/lazuar-pay-merchant/src/App.tsx`
- `apps/lazuar-pay-merchant/src/auth/oidcConfig.ts`
- `apps/lazuar-pay-merchant/src/auth/silentRenew.ts`
- `apps/lazuar-pay-merchant/src/auth/RequireAuth.tsx`
- `apps/lazuar-pay-merchant/src/auth/bearerToken.ts`
- `apps/lazuar-pay-merchant/src/auth/bearerToken.test.ts`
- `apps/lazuar-pay-merchant/src/layout/OrgLayout.tsx`
- `apps/lazuar-pay-merchant/src/layout/DashboardChrome.tsx`
- `apps/lazuar-pay-merchant/src/layout/nav.ts`
- `apps/lazuar-pay-merchant/src/layout/PageHeader.tsx`
- `apps/lazuar-pay-merchant/src/layout/WorkspaceSwitcher.tsx`
- `apps/lazuar-pay-merchant/src/lib/payApi.ts`
- `apps/lazuar-pay-merchant/src/lib/payApi.test.ts`
- `apps/lazuar-pay-merchant/src/lib/http.ts`
- `apps/lazuar-pay-merchant/src/lib/oneApi.ts`
- `apps/lazuar-pay-merchant/src/lib/checkoutOrigin.ts`
- `apps/lazuar-pay-merchant/src/lib/checkoutOrigin.test.ts`
- `apps/lazuar-pay-merchant/src/lib/occupancyDisplay.ts`
- `apps/lazuar-pay-merchant/src/lib/occupancyDisplay.test.ts`
- `apps/lazuar-pay-merchant/src/lib/processors.ts`
- `apps/lazuar-pay-merchant/src/lib/processors.test.ts`
- `apps/lazuar-pay-merchant/src/lib/roles.ts`
- `apps/lazuar-pay-merchant/src/lib/sessionKeys.ts`
- `apps/lazuar-pay-merchant/src/lib/homePath.ts`
- `apps/lazuar-pay-merchant/src/lib/workspaceStatus.ts`
- `apps/lazuar-pay-merchant/src/lib/workspaceStatus.test.ts`
- `apps/lazuar-pay-merchant/src/lib/staffDisplay.ts`
- `apps/lazuar-pay-merchant/src/pages/LoginPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/HomePage.tsx`
- `apps/lazuar-pay-merchant/src/pages/CallbackPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/CreateWorkspacePage.tsx`
- `apps/lazuar-pay-merchant/src/pages/CreateWorkspaceForm.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/OverviewPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/PaymentsPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/ReceiptsPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/CreateWorkspacePage.tsx`
- `apps/lazuar-pay-merchant/src/locks.test.ts`
- `apps/lazuar-pay-merchant/src/ui/components/app-sidebar/user-menu.tsx` (copied chrome; `onSettingsClick` unused by `DashboardChrome`)

### Checkout Vite (authority for buyer client)

- `apps/lazuar-pay-checkout/package.json`
- `apps/lazuar-pay-checkout/vite.config.ts`
- `apps/lazuar-pay-checkout/README.md`
- `apps/lazuar-pay-checkout/.env.example`
- `apps/lazuar-pay-checkout/.env` (gitignored; `VITE_PAY_API_URL=http://localhost:8081`)
- `apps/lazuar-pay-checkout/Dockerfile`
- `apps/lazuar-pay-checkout/index.html`
- `apps/lazuar-pay-checkout/src/main.tsx`
- `apps/lazuar-pay-checkout/src/App.tsx` (entire runtime)
- `apps/lazuar-pay-checkout/src/pay.ts`
- `apps/lazuar-pay-checkout/src/pay.test.ts`
- `apps/lazuar-pay-checkout/src/locks.test.ts`
- `apps/lazuar-pay-checkout/src/ui/components/card.tsx` (`CardTitle` is `<h1>`)

### Pay host doors these clients call (and the ones they do not)

- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayCors.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.json`
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.Development.json`
- `apps/lazuar-pay/.env.example`
- `apps/lazuar-pay/README.md` (curl mint of `/v1/checkouts`)
- `apps/lazuar-pay/docker-compose.pay.yml`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiResponse.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OrgReadyEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/Bearer.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs` (first 80)
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CreateCheckoutRequest.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutSession.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/CreatePaymentLinkRequest.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs` (first 80)
- `apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestHosted.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/CorsTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPay/PublicPayTests.cs` (first 80 + grep)
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs` (already_paid / slot_key)
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayTest.cs` (`SeedCheckout`, `StartPay`)

### Spec, sample, CI, papers (as they affect “can another app copy this”)

- `packages/pay-spec/main.tsp` (CreateCheckoutRequest, PublicPay, PaymentLink, no `pay_url`)
- `Taskfile.yml` (`pay:dev` / `pay:merchant` / `pay:checkout`)
- `.github/workflows/ci.yml` (`pay` job: `dotnet test` + `pnpm --filter … build`; **no vitest**; **no `VITE_*`**)
- `.gitignore` (`dist/`, `.env`)
- `examples/README.md`
- `examples/hub-cashier-next/README.md` (Hub M2M + `payment.completed` — museum for this product)
- `plans/006-sample/README.md` (Hub sample program; runtime anchors are `apps/lazuar-api`)
- `plans/011-new-lazuar-pay/08-bezos-door.md` (law this slice enforces)
- `plans/019-evals/00-evaluation.md` (parent: silent list GET, Loading graveyard, Test injected)
- `plans/019-evals/02-merchant-frontend.md` (first 200; historical)
- `plans/019-evals/03-checkout-frontend.md` (first 200; historical)
- `issues/002/README.md`
- `issues/002/035-…` through `061-…` (UI slice; YAML `status: resolved`; re-verified live)
- `deploy/prod/Caddyfile` (Hub `hub.lazuar.com` only — **no** `/c/`, **no** 5178/5179)
- `deploy/dev/Caddyfile` (grep 5178/5179 → **no hits**)

Grep-only (absence is evidence):

- `from '@repo` / `from 'lazuar` / `internal/` under both Vite `src` → **no hits**.
- `Authorization` / `Bearer` / `oidc` under checkout `src` except locks → **no hits**.
- `/v1/checkouts` under merchant `src` → **no hits**. Merchant never calls the one-off mint door.
- `payment.completed` / `lzr_sk_` / `ApiKey` under `apps/lazuar-pay/src` → **no hits**.
- `vitest` under `.github/workflows` → **no hits**.
- `pay_url` under host `*.cs` → **no hits**. Only merchant `CheckoutsPage.tsx` types the field and never receives it.

`src/pages/WorkspacePage.tsx` was looked for and is **absent** (019 already said this). Merchant is a routed shell.

---

## 1. What this slice is asking

Two products sit in one repo and look like “Pay”:

1. **Hosted cashier (first-party dogfood).** Staff at `:5178` paste keys, mint a WhatsApp URL, buyer at `:5179/c/{token}` pays. That is 018 + 002.
2. **Kernel another app can swallow.** A second origin (or a server) mints a checkout, sends a human to a processor, learns `paid`, unlocks its own order — **without** cloning `apps/lazuar-pay-merchant` or serving `apps/lazuar-pay-checkout`.

019 already judged (1) as a hosted cashier that is not (2). 002 closed occupancy, HMAC, CORS-config, and the SPA lies that made (1) unshippable as a **cashier**. Kernel doors were **out of 002**. This paper asks the Bezos question on live files:

- Are the two Vite apps **clients of `/v1`**, or did 018 grow a back door (`internal/`, Hub types, in-process imports)?
- If they are honest clients, can a stranger copy the **HTTP sequence** without the Vite trees?
- What still forces Vite to take money?

The answer on `6d730d15`: the SPAs **are** `/v1` clients (Bezos door holds between UI and host). Taking money **without** those SPAs is **HTTP-possible today** for a writer who already has a One human JWT, and **product-impossible** for a second app that expected `lzr_sk_` + `payment.completed` + a hosted URL on the 201. Poll exists because Plane C does not. CORS exists because the first-party buyer page is a **browser**. A server-side integrator does not need CORS and still cannot authenticate as a machine.

---

## 2. Bezos door — live verdict

[011/08](../011-new-lazuar-pay/08-bezos-door.md): “your own UI is a client of `/v1`, not of `internal/`.” The sin is Team Retail compiling against Team Payments’ internals. The handler inside one Pay process may call `notify.Enqueue` as a function. A merchant browser may not.

### 2.1 What the Vite apps import

Merchant runtime dependencies (`apps/lazuar-pay-merchant/package.json`):

- `react` `^19.2.8`, `react-dom` `^19.2.8`
- `oidc-client-ts` `^3.4.1`, `react-oidc-context` `^3.3.0`, `react-router-dom` `^7.15.0`
- Radix primitives, `class-variance-authority`, `clsx`, `lucide-react`, `tailwind-merge`

Checkout runtime dependencies (`apps/lazuar-pay-checkout/package.json`):

- `react` / `react-dom`, `@radix-ui/react-slot`, `cva`, `clsx`, `lucide-react`, `tailwind-merge`

**Absent from both** (must stay absent): `@repo/api-types-ts`, `@repo/aura-ui`, `lazuar-ops`, `lazuar-portal`, `openapi-fetch`, `@tanstack/react-query`, `@stripe/stripe-js`, any `Lazuar.Pay.*` assembly, any `internal/` path.

Host `IsolationTests.Vite_apps_do_not_use_hub_types` greps both `package.json` files for `@repo/api-types-ts` only. Merchant `locks.test.ts` additionally bans `@repo/aura-ui` and `lazuar-ops`. Checkout `locks.test.ts` bans `oidc-client-ts`, `react-oidc-context`, `@repo/api-types-ts`.

Grep of both `src` trees for `from '@repo`, `from 'lazuar`, and `internal/` is empty. There is no TypeScript project reference into `apps/lazuar-pay`. There cannot be a C# import from Vite. **The back door 011 feared is not live.**

Chrome is **copied**, not linked: Card/Button live under each app’s `src/ui/`. That is ugly and honest. A second app that copies the HTTP sequence does not need those files.

### 2.2 What they `fetch`

Merchant `payApi.ts`:

```
const payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'
…
response = await fetch(`${payApi}/v1/whoami`, { headers })
…
return fetch(`${payApi}${path}`, { ...init, headers })
```

Every Pay call is `fetch(`${payApi}${path}`)` with `Authorization: Bearer ${accessToken}` and optional `X-Lazuar-Tenant-Id`. `credentials` is omitted on purpose (comment: localhost cookies are not port-scoped). Paths are string literals in pages (`/v1/orgs/${orgId}/gateways`, `/v1/payment-links`, …). No generated client.

Merchant `oneApi.ts` is the **other** Bezos door (Pay ↔ One):

```
const oneApi = import.meta.env.VITE_ONE_API_URL ?? 'http://localhost:8080/api/v1'
…
fetch(`${oneApi.replace(/\/$/, '')}/tenants`, { method: 'POST', … })
```

Workspace create is One’s HTTP, not Pay org CRUD. Pay README: “Pay does not store organizations.” CreateWorkspaceForm subtitle repeats it. That is correct between products. It is **not** a Pay `/v1` door. A headless integrator that already has an org_id never needs this call. A headless integrator that does not have an org_id must speak One, not Pay.

Checkout `pay.ts` / `App.tsx`:

```
GET  ${payApi}/v1/pay/${token}?slot_key=…
POST ${payApi}/v1/pay/${token}/start   { name, email, slot_key }
```

No other host. No One. No Bearer. Host test `Public_get_does_not_need_bearer` locks GET without Authorization; a second GET does not increment `One.SendCount`.

### 2.3 What they do **not** call (mapped, live, unused by first-party UI)

Host `Program.cs` maps more doors than either SPA:

| Door | Auth | Merchant | Checkout | Headless need |
|------|------|----------|----------|----------------|
| `GET /v1/whoami` | Bearer | **yes** (shell) | no | human session only |
| `GET /v1/orgs/{id}/ready` | member | **no** | no | optional ping |
| `POST /v1/checkouts` | writer | **no** | no | **yes** (one-off mint) |
| `GET /v1/checkouts/{id}` | member (Bearer first) | **no** | no | poll paid |
| `GET /v1/orgs/{id}/checkouts` | member | **no** | no | list one-offs |
| `POST /v1/payment-links` | writer | **yes** | no | shared URL mint |
| `GET /v1/orgs/{id}/payment-links` | member | **yes** | no | table |
| `POST /v1/orgs/{id}/products` | writer | **yes** (label) | no | optional |
| `GET /v1/orgs/{id}/products` | member | **no** | no | unused catalog |
| `GET /v1/orgs/{id}/gateways` | member | **yes** | no | vault cards / mint rails |
| `PUT /v1/orgs/{id}/gateway` | writer | **yes** | no | BYOK |
| `GET /v1/orgs/{id}/gateway?provider=` | member | **no** | no | singular |
| `GET /v1/pay/{token}` | public | no | **yes** | poll / start preflight |
| `POST /v1/pay/{token}/start` | public | no | **yes** | hosted session |
| `GET /v1/orgs/{id}/payments` | member | **yes** | no | staff table **or** poll |
| `GET /v1/orgs/{id}/receipts` | member | **yes** | no | staff table **or** poll |
| `GET /v1/orgs/{id}/receipts/{id}` | member | **no** | no | unused by SPA |
| `POST /v1/webhooks/{provider}/{orgId}` | Plane B HMAC | copy-hint only | no | PSP → Pay, not app → Pay |
| `POST /v1/one/webhooks` | Plane A HMAC | no | no | One → Pay |
| `PUT/GET /v1/orgs/{id}/one-webhook` | writer/member | **no** | no | per-org `whsec_` |
| `GET /health`, `GET /v1/health` | public | no | no | probe |
| `GET /ready` | public | no | no | DB probe |

The first-party **money path** is payment-links + hosted checkout SPA. The first-party UI **does not exercise** `POST /v1/checkouts`. Host tests and Pay README **do**. That split is the whole headless story: the kernel mint door is live and **orphaned by its own dashboard**.

### 2.4 Bezos between Pay and One

Pay `OneClient` calls One `GET me` and `POST tenants/{id}/authz/check` with the **same** human Bearer the SPA sent. Merchant does not call OpenFGA, does not hold `ZITADEL_PAT`, does not `SELECT` One. IsolationTests ban `Modules.One` in Pay source. Merchant register-spa.sh talks to One `POST /tenants/{id}/apps` with Ada’s access_token.

That is Bezos **between products**. It is also why a second app cannot mint without a One human (or, later, a machine key — sibling 02). Pay has no homemade API-key table. Grep `lzr_sk_` / `ApiKey` in Pay `src` is empty.

**Verdict:** Bezos door **holds** for the UIs. They are HTTP clients of `/v1` (and One `/api/v1` for tenant create). They do not import host internals. The remaining sin is not a back door. It is a **missing front door** (machine auth + outbound event + hosted URL on mint) plus laptop-shaped bake-ins that make the first-party clients hard to copy.

---

## 3. Merchant :5178 — how it authenticates, which doors it calls, can another app copy this

### 3.1 Package, port, scripts

`package.json` `dev`: `vite --port=5178 --host=0.0.0.0 --strictPort`. Preview 4178. `vite.config.ts` dual-pins 5178 and comments “never silently steal login :5175 or checkout :5179.” `Taskfile.yml` `pay:merchant` is `pnpm --filter lazuar-pay-merchant dev`.

Dockerfile: multi-stage `node:22-alpine`, `pnpm --filter ./apps/lazuar-pay-merchant build`, `serve -s dist -l 5178`. **Build fails** if `VITE_PAY_API_URL` or `VITE_CHECKOUT_ORIGIN` is empty (`RUN test -n "$VITE_PAY_API_URL" && test -n "$VITE_CHECKOUT_ORIGIN"`). That is image-time fail-closed. Local `pnpm build` (and CI `pnpm --filter lazuar-pay-merchant build`) does **not** run that test and does **not** have a Vite `requirePayApiUrl` equivalent. Source still defaults. See §11.

### 3.2 OIDC via One login :5175

`oidcConfig.ts` is explicit:

```
 * Public SPA: authorization code + PKCE.
 * Login UI is One :5175 (issuer is Zitadel :8085). Homepage is :5178.
 * Tokens in sessionStorage — not cookies (localhost cookies are not port-scoped).
```

Live config:

| Env | Default if unset |
|-----|------------------|
| `VITE_ZITADEL_AUTHORITY` | `http://localhost:8085` |
| `VITE_ZITADEL_CLIENT_ID` | `''` (LoginPage alerts “Missing VITE_ZITADEL_CLIENT_ID”) |
| `VITE_ZITADEL_REDIRECT_URI` | `http://localhost:5178/callback` |
| `silent_redirect_uri` | `{redirect origin}/silent-renew.html` |
| `VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI` | `http://localhost:5178/` |
| `VITE_ZITADEL_SCOPE` | `openid profile email offline_access` |
| `response_type` | `code` |
| `automaticSilentRenew` | `true` |
| `userStore` | `sessionStorage` |

There is **no** `client_secret` in Vite. `register-spa.sh` POSTs One `type: "spa"` and **exits 1** if the response contains `client_secret`. README: “Never `ZITADEL_PAT`.” LoginPage is a button that calls `auth.signinRedirect()`, not a password form. `locks.test.ts` greps `type="password"` and `/one/auth/login` and `lazuar_auth` as forbidden.

`main.tsx` wraps the tree in `AuthProvider` + `BrowserRouter`. Routes (`App.tsx`):

| Path | Gate | Page |
|------|------|------|
| `/callback` | none (OIDC return) | `CallbackPage` |
| `/login` | none | `LoginPage` |
| `/` | `RequireAuth` | `HomePage` → whoami → last org |
| `/workspaces/new` | `RequireAuth` | first-workspace create |
| `/o/:orgId` | `RequireAuth` + `OrgLayout` | chrome |
| `/o/:orgId/overview` | member whoami | processors + role |
| `/o/:orgId/gateway` | member | vault cards |
| `/o/:orgId/checkouts` | member | pay-link table |
| `/o/:orgId/payments` | member | charges |
| `/o/:orgId/receipts` | member | Official Receipts |
| `/o/:orgId/new` | member | create another workspace |

`RequireAuth` (002/035, 036):

- `isLoading` → “Checking session…”
- `error` → alert + Retry login
- `!isAuthenticated` → `<Navigate to="/login">` with `from`
- `!pickApiBearerToken(auth.user)` → alert “This session has no JWT access token. Pay cannot call the API.” + Sign in. **Never** sends `id_token`.

`pickApiBearerToken` (`bearerToken.ts`): compact JWS only (three non-empty segments). Opaque / JWE / empty → `undefined`. Tests lock: JWT access is sent; companion `id_token` is never sent; opaque + JWT id_token still returns `undefined`.

### 3.3 Silent renew (002/044)

019: `automaticSilentRenew` with no `silent_redirect_uri` reused `/callback` as the iframe target; `CallbackPage` ate `returnTo`.

Live: `silent_redirect_uri = ${new URL(redirect_uri).origin}/silent-renew.html`. Vite `build.rollupOptions.input` includes `silent-renew.html`. That HTML loads `src/auth/silentRenew.ts`, which constructs a `UserManager` and calls `signinSilentCallback()` only. Locks: oidc config contains `/silent-renew.html` and does **not** match `silent_redirect_uri` … `/callback`; silent module contains `signinSilentCallback` and does **not** contain `takeReturnTo` or `Navigate`.

`register-spa.sh` registers **both** `REDIRECT_URI` and `SILENT_REDIRECT_URI` on the One app. Copying this SPA without registering `silent-renew.html` will break renew even if `/callback` is allow-listed.

`CallbackPage` uses `takeReturnToOnce()` (module-level latch) so React StrictMode remount does not drop the deep-link. `OrgLayout` sets `returnTo` before `signinRedirect` on missing JWT or whoami 401.

### 3.4 Org shell

`OrgLayout` is the membership gate **after** OIDC:

1. No JWT → `setReturnTo` + `signinRedirect` (“Signing in…”).
2. `getWhoami(token, orgId)` with `X-Lazuar-Tenant-Id`.
3. 401 (`unauthorized`) → `setReturnTo` + `signinRedirect` (002/036; not a stuck banner).
4. Other errors → alert + **Switch workspace** + **Sign out** (002/046).
5. Whoami 200 but org not in `tenants` → “Not a member of this org” + same exits. `setOrgHint` runs **only if** `match` (002/047). Stale `returnTo` to a foreign org is rejected in `resolvePostLoginPath` (`homePath.ts`: honor deep-link only when `tenants.some`).
6. Suspended tenant still **matches** membership; `workspaceStatusBanner` paints “This workspace is suspended. Charges are paused.” (002/065 sibling; `workspaceStatus.test.ts`). Writer mint will 403 from MemberGate (`Tenant is suspended.` / `Writer role required`).

`DashboardChrome`: copied `AppSidebar`, `WorkspaceSwitcher`, one chrome `<h1>` from `titleFromPath` (Overview / Processor / Pay links / Payments / Receipts). `getPayNavGroups` is five Money items. No Appointments, no Hub CRM. Locks: `onSettingsClick` absent from chrome (002/045). User-menu still **declares** `onSettingsClick` as a copied Aura prop; chrome does not pass it. Settings is not Processor.

`canWriteMoney`: `owner` || `admin`. `member` sees tables and vault metadata; cannot PUT gateway, cannot POST products/links. Matches `MemberGate.RequireWriterAsync` (role overlay on `/me`, not `authz/check admin` — sibling 05 / 002/030, still true, out of this UI slice).

`HomePage` / `CallbackPage` call `GET /v1/whoami` **without** org hint, then `resolvePostLoginPath`. Empty tenants → `/workspaces/new`. Hint org still a member → `/o/{id}/overview`.

`CreateWorkspacePage` (no tenants): Sign out in the header. Form POSTs One `/tenants`. Success `setOrgHint` + navigate `/o/{id}/overview`. Pay has not stored an organization row except lazily `OrgSettings` on first mint/put.

### 3.5 `VITE_PAY_API_URL`

Merchant `payApi.ts` line 3:

```
const payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'
```

`.env.example` sets the laptop URL. Local `.env` matches. Dockerfile requires the ARG. **Production `pnpm build` without the env still inlines `http://localhost:8081`.** 002/050 called this out as “merchant is staff laptop-shaped; checkout is the buyer” and **fixed checkout only**. That leftover is live. A copied merchant on `https://merchant.example` that forgot the env will whoami the developer’s laptop (mixed-content if the page is HTTPS).

`payJson` / `getWhoami` map network throw → `Pay unreachable`; 401 on whoami → `unauthorized`; other non-OK → host `detail` via `problemDetail`. Tests: 401 does not send `id_token`; 503 surfaces `Identity provider unreachable` not `whoami 503`; fetch throw is `Pay unreachable`.

### 3.6 Which `/v1` doors merchant actually calls

From live page source (not README):

| Page | Method | Path | When |
|------|--------|------|------|
| Home / Callback / OrgLayout / CreateWorkspace | GET | `/v1/whoami` | every authenticated mount |
| Overview | GET | `/v1/orgs/{orgId}/gateways` | load |
| Gateway | GET | `/v1/orgs/{orgId}/gateways` | load / after save |
| Gateway | PUT | `/v1/orgs/{orgId}/gateway` | Save key (writer) |
| Checkouts | GET | `/v1/orgs/{orgId}/payment-links` | load |
| Checkouts | GET | `/v1/orgs/{orgId}/gateways` | mint rail list |
| Checkouts | POST | `/v1/orgs/{orgId}/products` | Create pay link (writer) |
| Checkouts | POST | `/v1/payment-links` | after product 201 |
| Payments | GET | `/v1/orgs/{orgId}/payments` | load |
| Receipts | GET | `/v1/orgs/{orgId}/receipts` | load |
| CreateWorkspaceForm | POST | One `/tenants` | not Pay |

Headers on Pay calls: `Authorization: Bearer {access_token}`, `Accept: application/json`, `X-Lazuar-Tenant-Id: {orgId}` when `orgHint` is set. Writes add `Content-Type: application/json`. **No `Idempotency-Key`.** Host checkout mint supports it; merchant does not mint checkouts. Payment-link create has no idempotency header in `PaymentLinkEndpoints`. Double-click Create can mint two products + two links (038 leftover is the busy flag, not host idempotency).

### 3.7 Vault UI (GatewayPage)

Cards from `visibleRails(processors)`:

- If host list includes Test (`hostListsTest`), all six names including Test.
- If Production host omits Test (`PayProviders.Listed` = All without Test), SPA **hides** Test (002/042). `readyMintRails` / `visibleRails` tests lock “does not invent Test when the host omitted it.”

Test card: dashed, “Ready”, “No keys. Use this on Pay links.”, no Edit. PUT test on host is 400 `"test processor does not take secrets"`. SPA never offers the dialog.

Real rails: Empty / On file (last4 + webhook on file). Writer Edit opens a dialog: secret (or Razorpay key_id:key_secret), webhook secret (CHIP = **Textarea** “PEM from CHIP dashboard”), Brand/Collection ID for chip/billplz, Billplz environment select hydrated from GET (002/028 related). Save always sends a **fresh** `webhook_secret` (host requires it). Hint:

```
Webhook path: /v1/webhooks/{editing}/{orgId}
Dashboard callback must be public https on Pay:PublicBaseUrl. This SPA does not know that origin. Localhost will fail.
```

002/040: webhook hint used to be `{payApi}/v1/webhooks` (laptop 8081). Live locks: source contains `/v1/webhooks/` and `Pay:PublicBaseUrl` and does **not** contain `{payApi}/v1/webhooks` or `localhost:8081`. **The SPA still does not display the actual PublicBaseUrl** — it only warns that it does not know it. Staff must read host config. A copied app has the same hole unless it asks the host for the public origin (no such door).

Overview “On file” uses `vaultedNonTest` (002/039). Test listed → extra line “Test is always available.” Loading `…` until GET returns; `listError` is `role="alert"`, not “On file none.”

### 3.8 Pay-link table and occupancy copy (CheckoutsPage)

This is the first-party mint. It is **not** `POST /v1/checkouts`.

Flow:

1. Writer opens dialog. Provider `<Select>` from `readyMintRails` (configured rails the host listed). `defaultMintRail` prefers first **non-test** rail, else Test if listed, else `''` (002/043). Empty provider disables Create.
2. Capacity: `one` (max_payers 1) / `limited` (integer ≥ 2) / `unlimited`.
3. POST product `{ name, amount, currency: 'MYR' }` then POST `/v1/payment-links` `{ org_id, amount, currency: 'MYR', provider, product_id, max_payers?, unlimited? }`. **No `success_url`.** Host payment-link row has no SuccessUrl column usage at mint; children get baked CheckoutBaseUrl at **start** (see §8).
4. `finally { setBusy(false) }`. Product 201 + link fail → `A product was created. Pay link failed: …` (002/038). Catalog is a **label**; host still types amount at mint and 400s if it disagrees with the catalog price when a price row exists.

Table columns: Label, Amount, Processor, Payers, Status, Copy/Open.

`pay_url` is typed on the row and **never returned by the host** (`PaymentLinkView` has PublicToken, not PayUrl). Copy uses:

```
const url = row.pay_url || (row.public_token ? buyerUrl(row.public_token) : null)
```

`buyerUrl` is `{resolveCheckoutOrigin(VITE_CHECKOUT_ORIGIN, PROD)}/c/{token}`. Dev fallback `http://localhost:5179`. Production empty → `null`, dialog alert `VITE_CHECKOUT_ORIGIN is required in production`, Create disabled (002/041 **partial**). The longer-term fix in the issue (“payment-link 201 includes `pay_url` from `Pay:CheckoutBaseUrl`; SPA copies **that**”) is **not** live. Two configs still exist: Vite `VITE_CHECKOUT_ORIGIN` and host `Pay:CheckoutBaseUrl`. Nothing ties them. A merchant image can copy `https://checkout.example/c/…` while PSP success returns to `http://localhost:5179/c/…?status=verifying` if CheckoutBaseUrl was left at the Development default.

Local `.env` on this laptop **omits** `VITE_CHECKOUT_ORIGIN` even though `.env.example` has it. Dev therefore uses the hardcoded 5179 fallback. That matches local `appsettings.Development.json` `CheckoutBaseUrl`. It is still the 041 shape for anyone who `pnpm build`s without the env (merchant Vite does not fail the build).

Occupancy copy (002/004, 079):

- Dialog one-person: **“The link closes after one person starts Pay. Unpaid starts free after 30 minutes.”** Locks forbid the old lie “The link closes after one successful payment.”
- Unlimited: “Anyone with the URL can pay. It does not close on its own.”
- Table payers: `occupancyPayersLabel` → `Unlimited` / `{taken} started · unlimited` / `{taken} / {max}`.
- Status: host `over_capacity` → “over capacity” + page alert “A pay link has more payers than its cap. Money already moved — this is leftover over-admit…” One-person `full` with `paid_count >= 1` displays **paid** in the staff table (merchant remap, not buyer thank-you). Remaining on the merchant list is unclamped (`RemainingUnclamped`) so over-admit is visible.

Host occupancy (`PaymentLinkOccupancy`): a payer is `open` **or** `paid`. Stale `open` older than `Pay:ReservationTtlMinutes` (default 30) become `expired`. SPA must not invent expiry; copy now quotes start + 30 minutes. That is aligned after 002. Seat is still a **reservation**, not a successful payment. Staff table “paid” for max=1 full+paid is a display remap of `full`.

List GET failure (002/037): `payJson` throws → `listError` alert; empty illustration **hidden** when `listError && links.length === 0`. Same pattern on Payments / Receipts. Overview / Gateway show the alert instead of “On file none” / all Empty. `payApi.test.ts` locks non-OK does not look like an empty list.

### 3.9 Can another app copy this?

**HTTP: yes, as a reference client, if they already have a One human JWT and an org_id.** The sequence is not secret:

```
GET  /v1/whoami
GET  /v1/orgs/{org}/gateways
PUT  /v1/orgs/{org}/gateway          # writer, BYOK
POST /v1/orgs/{org}/products         # optional label
POST /v1/payment-links               # writer, explicit provider
GET  /v1/orgs/{org}/payment-links
GET  /v1/orgs/{org}/payments
GET  /v1/orgs/{org}/receipts
```

**As a product they clone: no, not without this repo’s OIDC ceremony.**

What a copy must also do that is **not** in `/v1`:

1. Register a public PKCE SPA with One (`type: spa`, callback + silent-renew + post-logout). `register-spa.sh` is the only runbook. It needs `ACCESS_TOKEN` + `TENANT_ID` already.
2. Put that `client_id` in `VITE_ZITADEL_CLIENT_ID`. Empty disables Sign in.
3. One login `REDIRECT_ALLOWLIST` must include the callback (README). One `App:CorsOrigins` must include the merchant origin if the copy POSTs `/tenants` from the browser.
4. Pay `Pay:CorsOrigins` must include the merchant origin (Production/Staging empty **throws** at boot — `PayCors.Resolve`). Development silently uses eight laptop URLs.
5. Bake `VITE_PAY_API_URL` to the **public** Pay origin. Merchant source will not fail closed.
6. Bake `VITE_CHECKOUT_ORIGIN` **and** host `Pay:CheckoutBaseUrl` to the same buyer origin, or Copy and PSP success diverge.
7. Copy the Aura shell, or don’t — the host does not care. The host cares about Bearer + org_id + writer role.

What they **must not** copy:

- Hub ops `:3003`, portal `:3004`, `@repo/api-types-ts`.
- Password form, `id_token` as Bearer, `ZITADEL_PAT` in Vite.
- Inventing Test when Production host omitted it.
- Treating list GET failure as empty.
- Hardcoding `:5179` in a production build (partially closed; source still can).

A **server-side** second app should not copy merchant at all. Merchant is a staff console. Mint from a backend with a writer credential (today: a user’s JWT, which is a terrible M2M story — sibling 02). Then either redirect the buyer to checkout Vite **or** skip Vite (§5).

**Hardcoded localhost inventory (merchant, live):**

| Site | Laptop default | Production fail-closed? |
|------|----------------|-------------------------|
| `payApi.ts` `VITE_PAY_API_URL` | `http://localhost:8081` | **No** (Dockerfile yes; Vite no) |
| `oneApi.ts` `VITE_ONE_API_URL` | `http://localhost:8080/api/v1` | No |
| `oidcConfig.ts` authority | `http://localhost:8085` | No |
| redirect / post-logout | `:5178` | No |
| `checkoutOrigin.ts` | `http://localhost:5179` | **Yes** (`PROD` → `null`) |
| compose `pay-merchant` build-args | all laptop URLs | operator must override |
| `appsettings.Development.json` CORS | 5178/5179/4178/4179 twins | Production requires `Pay:CorsOrigins` |

---

## 4. Checkout :5179 — no OIDC, poll verifying, slot_key, success URL is not paid

### 4.1 Package, port, no identity

`dev`: `vite --port=5179 --host=0.0.0.0 --strictPort`. Preview 4179. No `react-router-dom`. No `oidc-client-ts`. `main.tsx` is StrictMode + `<App />`. Path convention is the router: `tokenFromPath` = `^/c/([^/]+)/?$`. Extra segments (`/c/tok/receipt`) → null → “Link not found” (002/058).

README lock: “Buyers have **no** One account. Fail if this page asks for Zitadel login.” Locks: package has no oidc; App has no wallet tiles / PAN autocomplete; no `Sign in` on the retry card.

Dockerfile: `RUN test -n "$VITE_PAY_API_URL"` then build; `serve -l 5179`.

### 4.2 `VITE_PAY_API_URL` (002/050)

`pay.ts`:

```
export function payApiOrigin(): string {
  const raw = (import.meta.env.VITE_PAY_API_URL as string | undefined)?.trim()
  if (raw) return raw.replace(/\/+$/, '')
  if (import.meta.env.DEV) return 'http://localhost:8081'
  throw new Error('VITE_PAY_API_URL is required')
}
```

`vite.config.ts` `requirePayApiUrl`: production mode without env **throws** at config load. Locks grep `import.meta.env.DEV` and forbid `?? 'http://localhost:8081'` in `pay.ts`. Trailing slash stripped so `/v1/pay` is not doubled.

**CI hole:** `.github/workflows/ci.yml` `pay` job runs `pnpm --filter lazuar-pay-checkout build` with **no** `VITE_PAY_API_URL`. `.env` is gitignored. `vite build` is production mode. The 050 fail-closed check will fail that step unless the runner has a leftover env. Merchant build in the same step has **no** such check and will bake localhost. First-party CI does not run `vitest`. Honesty locks for this slice are local-only.

### 4.3 Public GET + start (the only two doors)

Boot:

```
GET ${payApi}/v1/pay/${token}?slot_key=${slotKey(token)}
```

- Network throw → “Can't reach Pay” retry card (002/048, 057).
- 404 → `error = 'missing'` → “Link not found. No sign-in.”
- Other non-OK → host `detail` or “Can't reach Pay”.
- 200 → `PayView`: amount, currency, status, email_required, started, mine, provider, redirect_url, payer_name, payer_email.

Start:

```
POST ${payApi}/v1/pay/${token}/start
Content-Type: application/json
{ name, email, slot_key }
```

No Authorization. Maps: 409 → refetch GET (full / not open); 503 → rail not configured; 400 → detail (email / callback base / slot_key); other non-OK → detail; 200 with `redirect_url` → `window.location.assign`; 200 without → “Processor did not return a pay URL” (002/061); fetch throw → “Can't reach Pay” (002/057). If `started && redirect_url`, Pay button is “Continue to processor” and assigns the stored URL (host start idempotency: same redirect, no second PSP HTTP — `PublicPayTests.Start_twice_returns_same_url_without_second_psp_http`).

Email: CHIP/Billplz/Xendit/Razorpay `email_required` (host `PayProviders.RequiresEmail` = not Stripe and not Test). SPA `usableEmail` rejects empty and `customer@example.com`. Required field has explanation + `aria-required` (002/053). Placeholder email shows “Use your real email.”

Prefill after cancel (002/054): GET `payer_name` / usable `payer_email` hydrate inputs (`prev || body…`).

### 4.4 `slot_key` (one browser ≈ one seat)

`pay.ts` `slotKey(token)`:

1. In-memory `Map` first.
2. `localStorage` then `sessionStorage` under `lazuar-pay-slot:{token}`.
3. Each store get/set is try/catch (private mode).
4. Miss → `crypto.randomUUID()`, remember in memory, try to persist.

002/051: localStorage throw used to mint a **new** UUID every call → two seats from one private-mode tab. Live test: `slotKey(token, [boom])` is stable across calls. Host `NormalizeSlotKey`: trim, length 8–128, else null. Payment-link **start without slot_key is 400** (`PaymentLinkTests.Start_link_without_slot_key_is_400`). Standalone checkout start **ignores** slot_key (`pay-spec` comment; `PublicPayEndpoints.Start` only requires it inside `MintOrResume` for links).

A **server-side** integrator minting a **payment-link** must invent a stable slot (cookie, logged-in buyer id, or a server UUID stored against their order). If they mint a **one-off checkout** (`POST /v1/checkouts`), they do not need a slot. That is the embed-vs-link fork.

Slot is still **client-supplied**. 002/019 (host P1, not this UI slice) remains: a hostile client can grief a capped link with many slots. SPA cannot fix that. Headless cannot either without host occupancy (002/001 closed the count-then-insert race; grief of many slots is a different bug).

### 4.5 Success URL is not paid — poll `?status=verifying`

`verifyingQuery`: `status=verifying` in the search string. Host `CheckoutUrls.Success` **appends** `?status=verifying` when it bakes the default. Cancel is the same `/c/{token}` **without** the query.

SPA never treats the query as paid. Locks: `pay.status === 'paid'` is the paid pixel; verifying copy is “The processor success URL is not paid. Waiting for the webhook.”

Poll (002/055, 056): while `verifying` and status not in `paid|expired|full|already_paid` and error not `missing`, `setInterval` 2s, max 15 (~30s). Each tick GET `payPath`. 404 → `missing`, stop. Non-OK → ignore this tick (do not paint Loading). `n >= 15` → `verifyTimedOut`. Footer: “Not paid yet. The success URL is not paid.” + **Refresh status** (`pollNonce` restarts the interval) + **Return to pay** if `open` (strips query via `replaceState`, `setVerifying(false)`). 016 stuck pixel is closed. Late webhook still needs Refresh or a new poll window — there is no EventSource / webhook to the **buyer** browser (and there should not be).

Test rail copy: “Test processor: Pay marks this paid. No card, no secret.” Host `TestHosted` returns `CheckoutUrls.Success` and `Start` auto-`FulfillPaidAsync`. Buyer still lands on `?status=verifying` and polls; the next GET is `paid`. That is honest: even Test’s redirect is not the book.

Paid pixel: “Payment received” + Official Receipt sentence + “not a membership login.”  
Already paid (002/052): host `already_paid` when max=1, paid≥1, **not mine**. SPA: “This link is already paid / Someone else already paid this link.” `PaymentLinkTests.One_person_link_shows_already_paid_without_slot_after_pay` locks GET-without-slot as `already_paid`, `mine=false`, no `payer_email`. Same slot GET is `paid`, `mine=true`.  
Full / expired: own cards. Expired is host-written after TTL or failed reservation; SPA does not invent it.

### 4.6 Can another app embed / hosted-redirect only?

Three honest shapes, only one of which is this SPA.

**A. Hosted-redirect to Pay’s checkout origin (first-party).**  
Mint a payment-link (or one-off checkout). Send the human to `{CheckoutBaseUrl}/c/{public_token}`. This SPA does GET, slot, start, PSP redirect, poll. Integrator waits for paid via poll of receipts/checkouts (no Plane C). **Requires this Vite app (or a clone of these two fetches) to be served on CheckoutBaseUrl.** Compose profile `pay-checkout` exists; `deploy/prod/Caddyfile` does **not** route `/c/`. Production dogfood of hosted-redirect is not on Hub’s Caddy.

**B. Hosted-redirect to the integrator’s own success/cancel, skip :5179.**  
`POST /v1/checkouts` with `success_url` / `cancel_url` pointing at the **app**. Then **the app** (browser or server) `POST /v1/pay/{token}/start` with name/email (and slot_key if the token is a **link**). 200 `redirect_url` → send the human to the PSP. PSP returns to **their** success_url. **They must not unlock on that URL.** They poll `GET /v1/pay/{token}` (public) or `GET /v1/checkouts/{id}` / receipts (member Bearer) until `paid`. This is **live HTTP**. It does not need :5179. It does need a writer Bearer today. It does need them to render a tiny start form **or** start from the server (CHIP email, etc.).

**C. Embed Pay’s public GET/start from a second browser origin.**  
`fetch` from `https://app.example` to `https://pay.example/v1/pay/…` is CORS. Production `Pay:CorsOrigins` must list `https://app.example`. Development list is laptop 5178/5179/4178/4179 only — a second Vite on :3020 is **denied** until config changes. `CorsTests.Health_allows_configured_extra_origin` and `Configured_origins_replace_laptop_list` lock that a configured origin is allowed **and** replaces the laptop list (5179 then denied). `Public_pay_get/post/options_allows_checkout_origin` lock `/v1/pay` not just `/health` (002/066). Ops :3003 / portal :3004 stay denied.

**Server-side M2M:** CORS does **not** apply. Then they need **keys** (sibling 02). Today there are no keys. A stolen staff JWT in an env file is not a product.

**Cannot:** collect PAN on the second app and call a Pay “charge card” door — there is none. Capability is `hosted_link`. Cannot pick a PSP on the buyer page — merchant bound `provider` at mint. Cannot use Hub sample `POST …/integrations/payments/checkouts` against 8081 — that door is museum `lazuar-api`.

---

## 5. Headless sequence — mint, hosted URL, wait for paid without SPA

The assignment’s path:

> Today: `POST /v1/checkouts` (writer Bearer) + `POST /v1/pay/{token}/start` (public) + poll `GET /v1/pay/{token}` or `GET receipts` (member Bearer). Document this path as live or broken. Missing webhook (03) is why poll exists.

### 5.1 One-off checkout (the kernel door the dashboard does not use)

**Live** in host + hermetic tests + Pay README curl. **Unused** by merchant SPA.

Mint (writer = One tenant role `owner`|`admin`, human JWT):

```
POST /v1/checkouts
Authorization: Bearer {access_token}
X-Lazuar-Tenant-Id: {org_id}          # optional hint; body.org_id is the shop
Idempotency-Key: {optional}
Content-Type: application/json

{
  "org_id": "{one tenant id}",
  "provider": "stripe",                # required; unknown → 400
  "amount": 10.00,                     # > 0
  "currency": "MYR",                   # default MYR
  "product_id": "{optional}",
  "success_url": "https://app.example/orders/1/success",
  "cancel_url": "https://app.example/orders/1/cancel"
}
```

`CheckoutEndpoints.Create`: MemberGate writer; charges paused → 403; amount ≤ 0 → 400; unknown provider → 400; Test outside Dev/Testing → 400 `"test processor is not enabled"`; non-test without vault row → 400 `"rail not configured"`; idempotency conflict → 409. 201 on mint, 200 on idempotent replay (`session.Id == mintedId` ? 201 : 200).

Response is `CheckoutSession` (snake_case): `id`, `org_id`, `provider`, `product_id`, `public_token`, `amount`, `currency`, `status: "open"`, `interval: "one_off"`, `success_url`, `cancel_url`, `created_at`. **No `pay_url`. No `hosted_url`. No `/c/` link.**

`PayTest.SeedCheckout` POSTs exactly this (no success_url) and reads `public_token` + `id`. README shows the same curl with success/cancel example.test.

Start (public):

```
POST /v1/pay/{public_token}/start
Content-Type: application/json

{ "name": "Ada", "email": "ada@acme.test" }
```

Slot not required for standalone. Email required when `RequiresEmail`. Rate limit `Pay:StartMaxPerMinute` (default 20 / 60s) → 429. Returns `{ "redirect_url": "https://…" }`. Integrator sends the human there.

Wait for paid **without SPA**:

```
GET /v1/pay/{public_token}                    # public; status == "paid"
GET /v1/checkouts/{id}                        # member Bearer; session.status
GET /v1/orgs/{org}/receipts                   # member Bearer; look for checkout_id
GET /v1/orgs/{org}/payments                   # member Bearer; charges
```

`GET /v1/checkouts/{id}`: **Bearer required first** (002/062 existence oracle closed). Missing token → 401 `"Missing bearer token"`. Then load; missing → 404; non-member without “suspend” in detail → 404 (hide existence); suspend → 403. SPA never calls this. Headless **should**.

There is **no** `payment.completed` POST to the integrator. Grep of Pay `src` is empty. Poll is the product. That is why checkout SPA polls for 30s and then asks a human to Refresh. A server integrator can poll longer. They still hold a **human** JWT that expires (silent renew is a **browser** iframe). That is the 02 hole wearing a 03 mask: even with poll, the credential is the wrong shape.

Test processor: start fulfills in-process; `redirect_url` **is** the success URL (`CheckoutUrls.Success`). If the integrator set `success_url` on mint, Test redirects **there**, not to :5179. If they omitted it, Test redirects to `{CheckoutBaseUrl}/c/{token}?status=verifying` — which **does** require the SPA (or a 404). **Headless must send `success_url` on mint** or they have secretly taken a dependency on Vite.

### 5.2 Payment-link (what the dashboard actually mints)

```
POST /v1/payment-links
Authorization: Bearer {access_token}
{ "org_id", "provider", "amount", "currency"?, "product_id"?, "max_payers"?, "unlimited"? }
```

201 `PaymentLink`: `public_token`, occupancy fields, **no `pay_url`**, **no success_url on the request type**. `CreatePaymentLinkRequest` has no SuccessUrl.

Hosted URL is a **convention** the SPA knows: `{CheckoutBaseUrl}/c/{public_token}`. Headless that wants the first-party pixel must know CheckoutBaseUrl out of band (env, docs). Headless that wants to skip the pixel:

```
POST /v1/pay/{public_token}/start
{ "name", "email", "slot_key": "{8-128 chars, stable per payer}" }
```

Host `MintOrResume` **requires** slot_key (400 otherwise), requires `CheckoutUrls.Base` even if the integrator will not use :5179 (503 `"Pay:CheckoutBaseUrl is required"` if unset outside Testing). Child row SuccessUrl/CancelUrl are **always** baked to CheckoutBaseUrl + `/c/` + **parent** token + verifying query — **not** the integrator’s body (there is no body field). PSP then returns to **Pay’s checkout SPA**. Skipping :5179 for **links** is therefore **broken as a hosted-redirect-to-app** unless the integrator also serves `/c/{token}` **on CheckoutBaseUrl** or we add success_url to payment-links (missing feature).

Occupancy: same slot resumes; different slot takes another seat until cap; 409 `"This pay link is full"`. Abandoned open expires after 30 minutes (002/003). This path is live for the **hosted cashier**. It is the wrong path for a second app that wants to return to **its** `/orders/1/success`.

### 5.3 Live vs broken table

| Step | One-off `POST /v1/checkouts` | Payment-link `POST /v1/payment-links` |
|------|------------------------------|----------------------------------------|
| Writer Bearer (human JWT) | **Live** | **Live** |
| Machine key `lzr_sk_` | **Missing** (02) | **Missing** |
| 201 includes `public_token` | **Live** | **Live** |
| 201 includes `pay_url` | **Missing** | **Missing** (SPA synthesizes) |
| Body `success_url` / `cancel_url` | **Live** (stored; `CheckoutUrls` prefers them) | **Absent** (baked at start) |
| Start without Vite | **Live** (POST start, follow `redirect_url`) | **Live** but PSP returns to :5179 |
| Start without slot_key | **Live** (ignored) | **400** |
| Requires `Pay:CheckoutBaseUrl` | Only if success_url omitted | **Always** at start |
| Poll public GET | **Live** | **Live** (`already_paid` / `full` / child) |
| Poll GET checkout / receipts | **Live** (member JWT) | receipts **Live**; GET checkout by child id unused |
| Outbound `payment.completed` | **Missing** (03) — poll exists | **Missing** |
| First-party UI exercises it | **No** | **Yes** |
| Sample in `examples/` | **No** (Hub sample is museum) | **No** |
| Spec describes it | **Yes** (`Checkouts.create`) | **Yes** (`PaymentLinks.create`) |

**Verdict:** the assignment’s three-call sequence is **live HTTP** for one-off checkouts, **broken as a product** (wrong auth shape, no event, no hosted URL, no sample, dashboard doesn’t use it), and **the wrong sequence** for payment-links if the integrator expected to skip Vite.

### 5.4 Worked curl (one-off, headless, no Vite)

Preconditions: One running, Ada JWT in `$ACCESS_TOKEN`, org `$ORG_ID`, vault PUT already done for `stripe` (or use `test` in Development), Pay on 8081, `Pay:CheckoutBaseUrl` set **or** success_url supplied.

```
# 1. mint
curl -sS -X POST http://localhost:8081/v1/checkouts \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"org_id":"'"$ORG_ID"'","amount":10,"currency":"MYR","provider":"test","success_url":"https://app.example/ok","cancel_url":"https://app.example/no"}'
# → { "id", "public_token", "status":"open", "success_url":"https://app.example/ok", … }

# 2. start (no Bearer)
curl -sS -X POST http://localhost:8081/v1/pay/$PUBLIC_TOKEN/start \
  -H "Content-Type: application/json" \
  -d '{"name":"Ada","email":"ada@acme.test"}'
# → { "redirect_url": "https://app.example/ok?status=verifying" }  # Test: success URL itself
# real rail: Stripe Checkout / CHIP / … URL; send the human there

# 3. wait (no webhook)
curl -sS http://localhost:8081/v1/pay/$PUBLIC_TOKEN
# → { "status":"open"|"paid"|… }

curl -sS -H "Authorization: Bearer $ACCESS_TOKEN" \
  http://localhost:8081/v1/orgs/$ORG_ID/receipts
# → Official Receipt rows when Plane B (or Test start) booked
```

That is enough to take Test money **without either Vite app**, on Development, with a human JWT. It is **not** enough to sell as “second-app integration.”

---

## 6. CORS — second-app browser origin vs server-side M2M

`PayCors` (`Hosting/PayCors.cs`):

- Config key `Pay:CorsOrigins` (comma-separated).
- Parse non-empty → that list **replaces** the laptop list.
- Empty in Development or Testing → eight origins: localhost + 127.0.0.1 × 5178, 5179, 4178, 4179.
- Empty in Production or Staging → **throw** `"Pay:CorsOrigins must be configured"`.
- Policy: `WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()`. **Not** `AllowAnyOrigin`. **Not** `AllowCredentials` (SPAs omit cookies).

`appsettings.Development.json` repeats the eight. `appsettings.json` has **no** CorsOrigins (Production must set env). `.env.example` documents Docker/production HTTPS origins and “Never add ops :3003 or portal :3004.”

Compose `pay` service default: `Pay__CorsOrigins: http://localhost:5178,http://127.0.0.1:5178,http://localhost:5179,http://127.0.0.1:5179` — **preview 4178/4179 omitted** in the compose default (host Development json includes them). Operator override required for a second origin.

Tests (live, stronger than 019):

| Test | Meaning |
|------|---------|
| Health allows 5178 / 5179 / 4179 | first-party + preview |
| Health denies 3003 / 3004 | Hub museum stay out |
| Configured extra origin allowed | second-app can be listed |
| Configured list **replaces** laptop | 5179 denied when only `https://checkout.example` |
| Public GET/POST `/v1/pay/missing` allow 5179 | not only `/health` |
| OPTIONS `/v1/pay/missing` allow 5179, deny 3003 | preflight |
| Empty CORS Production/Staging throws | boot fail-closed |

**Second-app browser:** add their origin to `Pay:CorsOrigins`. If they forget, GET `/v1/pay` is a CORS failure; checkout 048 retry card is what first-party buyers would see; a foreign SPA sees a browser console error and an empty UI. Merchant whoami from a foreign origin fails the same way — staff think Pay is down.

**Second-app server:** no Origin header that matters. CORS is irrelevant. Auth is the blocker (02).

**Two allow-lists:** Pay CORS (this host) and One CORS (workspace create + OIDC). Copying merchant requires both. Copying checkout requires Pay CORS only. Headless server requires neither.

**002/049** is resolved as **config exists**. Leftover: defaults and compose are still laptop-shaped; `deploy/prod/Caddyfile` has no Pay origin at all; a second app is not in any example env.

---

## 7. Redirect URLs — `CheckoutBaseUrl` baked vs body `success_url`

`CheckoutUrls.cs`:

```
Success = checkout.SuccessUrl is blank
  ? Base + "/c/" + checkout.PublicToken + "?status=verifying"
  : checkout.SuccessUrl

Cancel = checkout.CancelUrl is blank
  ? Base + "/c/" + checkout.PublicToken
  : checkout.CancelUrl

Base = Pay:CheckoutBaseUrl trimmed, else Testing → localhost:5179, else throw
```

`appsettings.json`: **no** CheckoutBaseUrl (prod must set). Development: `http://localhost:5179`. `.env.example` comments it as buyer return origin, **not** the Billplz callback (`Pay:PublicBaseUrl`).

Rails: Stripe `SuccessUrl = CheckoutUrls.Success(...)`; CHIP success/failure/cancel redirects; Billplz `redirect_url` = success only; Xendit success+failure; Razorpay `callback_url` = success; Test returns Success.

**One-off mint:** body `success_url` / `cancel_url` are stored on the row and **win**. Headless should always send them. If they omit them, they have opted into Vite.

**Payment-link start:** `MintOrResume` **always** writes:

```
SuccessUrl = baseUrl + "/c/" + link.PublicToken + "?status=verifying"
CancelUrl  = baseUrl + "/c/" + link.PublicToken
```

No request field exists. Spec `CreatePaymentLinkRequest` has no success_url. Merchant dialog copy: “MYR. Success URL defaults to checkout ?status=verifying (not paid).” Honest for the cashier. A second app that minted a **link** cannot choose their own return URL without a new field.

**Two buyer origins:**

| Config | Owner | Used for |
|--------|-------|----------|
| `Pay:CheckoutBaseUrl` | host | PSP return default; link-child bake; Test redirect default |
| `VITE_CHECKOUT_ORIGIN` | merchant build | Copy/Open button |
| `VITE_PAY_API_URL` | both Vite builds | `fetch` target (API, not the pixel) |

Nothing checks they match. 022 (host throw in MintOrResume) is now a 503 with the exception message, not an uncaught 500. Production host without CheckoutBaseUrl **cannot start a payment-link**. One-off with body URLs still can.

Billplz additionally needs **public https** `Pay:PublicBaseUrl` for Plane B. Localhost callback 400 `"callback base not public"`. SPA start maps 400 to the form alert. Headless sees the same JSON problem. That is not a Vite requirement; it is a tunnel requirement.

---

## 8. 019 SPA bugs (035–061) after 002 UI commits — still live?

019 parent one-liner: “Remaining SPA holes are laptop CORS/API defaults, silent list GETs, Loading graveyard, Test injected even when Production host omits it.”

002 marked 035–061 resolved. Live re-read:

### 8.1 Merchant 035–047, 037–043

| # | 019 bug | Live on `6d730d15` | Leftover? |
|---|---------|--------------------|-----------|
| 035 | Org layout spins if access_token not JWT | `RequireAuth` blocks; `OrgLayout` `signinRedirect` on `!token` | **Closed.** Opaque Zitadel app gets a Sign-in alert, not Loading forever. |
| 036 | Whoami 401 stuck banner | `unauthorized` → `signinRedirect` | **Closed.** |
| 037 | List GETs silent empty tables | `payJson` + `listError` + hide empty illustration | **Closed** as a lie. Vitest greps + `payApi.test.ts`. No component test of the table. |
| 038 | Writer busy no `finally`; catalog orphans | `finally` on Gateway + Checkouts; orphan sentence | **Closed** for stuck busy. Double-submit can still mint two products (no Idempotency-Key). |
| 039 | Overview counts Test as On file | `vaultedNonTest` | **Closed.** |
| 040 | Webhook hint is `VITE_PAY_API_URL` | path-only + PublicBaseUrl sentence | **Closed** as a localhost leak. SPA still cannot **show** the public origin (no door). |
| 041 | Copy URL hardcoded :5179 | `resolveCheckoutOrigin`; PROD fail-closed | **Partial.** Host still no `pay_url`. Dev fallback 5179. Local `.env` omits the var. Merchant `pnpm build` does not fail. |
| 042 | Always offer Test | `visibleRails` / `readyMintRails` trust host list | **Closed.** Production host omits Test → SPA hides it. |
| 043 | Mint defaults to Test | `defaultMintRail` prefers first real | **Closed.** |
| 044 | Silent renew uses `/callback` | `silent-renew.html` + `signinSilentCallback` | **Closed.** Must register the extra redirect on One. |
| 045 | Duplicate h1; Settings is Processor | money pages omit `PageHeader title`; chrome h1; no `onSettingsClick` | **Mostly closed.** Overview still passes `title={tenant.name}` → **second `<h1>`** under chrome “Overview”. User-menu still has unused Settings prop. |
| 046 | Not a member / first workspace no way out | Sign out + Switch workspace | **Closed.** |
| 047 | Stale returnTo / setOrgHint before membership | hint only on match; `resolvePostLoginPath` membership check; `takeReturnToOnce` | **Closed.** |

### 8.2 Checkout 048–061

| # | 019 bug | Live on `6d730d15` | Leftover? |
|---|---------|--------------------|-----------|
| 048 | Non-404 GET paints Loading forever | retry card “Can't reach Pay” | **Closed.** |
| 049 | CORS laptop-only | `Pay:CorsOrigins`; prod throw; tests on `/v1/pay` | **Closed as config.** Defaults still laptop. Second-app origin is operator work. |
| 050 | Checkout API fallback localhost | DEV-only fallback; prod throw; Dockerfile test | **Closed for checkout source.** Merchant `payApi.ts` **still** `?? localhost:8081`. CI checkout build may fail closed without env. |
| 051 | localStorage throw mints new slot every call | in-memory Map + tests | **Closed.** |
| 052 | One-person paid shows Thank you to strangers | `already_paid` + “Someone else already paid” | **Closed.** Host test locks it. |
| 053 | Email required, Pay disabled, no explanation | helper text + real-email alert | **Closed.** |
| 054 | No prefill after cancel | GET payer_* hydrate | **Closed.** |
| 055 | Verifying timeout does not restart | `pollNonce` + Return to pay | **Closed.** |
| 056 | Poll ignores missing / disagrees with 404 pixel | 404 stops interval, sets `missing` | **Closed.** |
| 057 | startPay network throw unhandled | try/catch → Can't reach Pay | **Closed.** |
| 058 | Path regex no `$` | `^/c/([^/]+)/?$` | **Closed.** |
| 059 | Checked-in dist is not this SPA | root `dist/` gitignore; checkout `.gitignore` `dist`; lock greps `^dist/$` | **Closed.** `list_dir` may still show a local leftover `dist/`; it is not the source of truth. |
| 060 | Card titles not headings; confirming no live region | `CardTitle` is `<h1>`; `aria-live` on confirming | **Closed.** |
| 061 | Start 200 no redirect_url silent | “Processor did not return a pay URL” | **Closed.** |

### 8.3 019 one-liners vs live

| 019 leftover | After 002 UI |
|--------------|--------------|
| Silent list GET | **Fixed.** |
| Loading graveyard | **Fixed** on checkout boot GET. Overview still shows “Loading workspace…” until whoami (legitimate). Home “Opening workspace…”. CreateWorkspace “Loading…” until whoami — if whoami hangs, that spinner stays (no timeout). |
| Test injected | **Fixed** (trust host list). Test still **appears** in Dev because the host lists it as `configured: true`. That is honest. |
| Laptop CORS/API defaults | **Partially fixed.** Config + checkout fail-closed. Merchant source, compose defaults, CI merchant build, missing `VITE_CHECKOUT_ORIGIN` in local `.env` still laptop. |

**002 UI 035–061 are resolved as the bugs 019 named.** They are not a production kernel. Leftover holes that **block production dogfood vs integrator DX** are in §11 — mostly missing features (02, 03, `pay_url`, sample) plus laptop bake-ins the Dockerfile closed but Vite/CI did not.

---

## 9. First-party vs integrator — two DX problems that look like one

### 9.1 Production dogfood of One + merchant + checkout

To take Test money on a laptop today:

```
task pay:db:up          # Postgres 5435
task pay:dev            # 8081
task pay:merchant       # 5178
task pay:checkout       # 5179
# One API 8080, login 5175, Zitadel 8085  (not started by these tasks)
```

Then: register SPA, Sign in, paste keys or pick Test, Create pay link, Open :5179, Pay. That path is what 002 made honest.

Compose `--profile apps` now builds three images (`1974ac10`). Defaults still point Vite at localhost:8081/5179 and CORS at 5178/5179. `deploy/prod/Caddyfile` is Hub-only. There is no production URL for `/c/{token}` in this repo’s prod Caddy. First-party production dogfood is **operator folklore**, not a file.

OIDC: One must allow-list callback + silent-renew. Merchant README says so. Forgotten allow-list looks like “Pay is down” after Sign in.

WrapKey, PublicBaseUrl, per-org Plane B secrets, One inbound `whsec_` are host production (06 / 04) — staff SPA will 503/400 with `detail` now, not a silent empty table.

### 9.2 Integrator DX (another app, no clone)

What they can do on this SHA with a **human** JWT:

- Mint one-off checkout with **their** success_url.
- POST start from **their** server.
- Redirect buyer to PSP.
- Poll GET pay / receipts until paid.

What they cannot do:

- Authenticate as an app (`lzr_sk_`).
- Receive `payment.completed` (must poll; JWT expires).
- `npm install @lazuar/pay` — no SDK. pay-spec exists; honesty vs host is sibling 09.
- Copy `examples/hub-cashier-next` onto 8081 — wrong host, wrong path, Hub `sk_` / `whsec_`.
- Mint a **payment-link** and return to **their** URL — bake is CheckoutBaseUrl.
- Learn `pay_url` from 201 — they must know the convention.
- Skip CORS config if they start from a **browser** on a new origin.

The Hub sample (006) proved the **shape** they want: server mint, redirect, verify signed webhook, unlock a toy order. That shape is **museum**. Focused Pay has the mint and the redirect and **dropped** the webhook and the key. Poll is a regression vs Hub integrator DX, not an accident.

---

## 10. Spec vs SPA vs host (only as it affects copy-paste)

`packages/pay-spec/main.tsp` describes `POST /checkouts` with `success_url` / `cancel_url`, `POST /payment-links` **without** those, `PublicPay` GET/start with optional `slot_key`, `StartPayResponse.redirect_url`. It does **not** describe `pay_url`. PaymentLink model matches host `PaymentLinkView` (no URL). CheckoutSession matches host (no URL).

Both Vite apps follow the **host**, not a generated client (IsolationTests ban Hub types; they also do not depend on pay-spec TS). A stranger who compiles TypeSpec gets a map of doors and still has to learn from merchant source that Copy is `{checkoutOrigin}/c/{public_token}` and from checkout source that verifying is a query, not a status.

CI compiles pay-spec and runs `check-pay-openapi-honesty.mjs` (Map* vs OpenAPI). It does **not** run merchant/checkout vitest. Honesty locks in `locks.test.ts` are the SPA contract; they are grep, not browser tests. Good enough to stop 019 regressions. Not a second-app SDK.

---

## 11. Leftover holes that still block production dogfood vs integrator DX

Ranked for **this slice** (not occupancy P0 — 002 closed those on the host). Bugs vs missing vs refuse.

### 11.1 Missing features (kernel; not SPA bugs)

1. **No machine credential.** Writer/member gates accept `Bearer` that One `/me` understands. There is no `lzr_sk_`. Headless today means stuffing a **user** JWT in a server env, which dies when silent-renew is not running. Sibling 02.
2. **No outbound `payment.completed`.** Poll of public GET or member receipts is the only wait. Checkout SPA’s 30s poll is a UI for that hole. Sibling 03. Hub sample cannot be ported until this exists.
3. **Mint 201 has no `pay_url`.** Integrators and the merchant Copy button reconstruct `{origin}/c/{token}` from two unjoined configs. 041’s “longer-term” fix is still the right door.
4. **Payment-links cannot take `success_url`.** Second app that uses the dashboard’s mint door is stuck returning to Vite. One-off checkout **can**; the dashboard does not offer it.
5. **No Pay second-app sample.** `examples/hub-cashier-next` is Hub. 006’s runtime anchors are `apps/lazuar-api`. Integrator DX is a README curl for `/v1/checkouts` that first-party UI never runs.
6. **No SDK / published cookbook** of the three-call sequence. pay-spec is closer than 019 (it now has payment-links, slot_key, receipts) but still no `pay_url` and no auth scheme other than implied Bearer.

### 11.2 Bugs / leftover lies (first-party)

7. **Merchant `VITE_PAY_API_URL` still defaults to localhost in source.** Checkout does not. Asymmetric 050. Production `pnpm build` of merchant without env ships laptop API.
8. **Merchant `pnpm build` / CI does not fail closed on missing `VITE_*`.** Dockerfile does. Compose bake-args default to localhost, so images can be “green” and still laptop.
9. **CI `pay` job builds checkout in production mode without `VITE_PAY_API_URL`.** 050’s vite.config throw vs a job that never exports the var. Either CI is red on this SHA or a hidden env exists; the workflow file has none. CI also does not run vitest.
10. **Two buyer origins can diverge.** `VITE_CHECKOUT_ORIGIN` vs `Pay:CheckoutBaseUrl`. Local merchant `.env` omits the former.
11. **Overview duplicate `<h1>`.** Chrome “Overview” + `PageHeader title={tenant.name}`. 045 lock did not cover Overview. Cosmetic.
12. **HomePage whoami failure has no Sign out** (CreateWorkspace and OrgLayout do). Minor trap if Pay is 503.
13. **No timeout on whoami spinner.** 035 closed the opaque-token livelock; a hung 8081 still sits on “Opening workspace…” / “Loading workspace…”.
14. **Webhook path hint still not a copyable absolute URL.** Staff must know PublicBaseUrl. Integrator pasting Plane B into Stripe Dashboard has the same problem — host should advertise `{PublicBaseUrl}/v1/webhooks/{provider}/{orgId}`.
15. **Merchant never sends `Idempotency-Key`.** Double-click Create → two products. Host supports it only on `/v1/checkouts`.
16. **`GET /v1/orgs/{id}/ready` is unused.** Spec says ready = not paused and (vault or Test). Dashboard Overview invents its own “On file” count instead. Headless could ping ready; first-party does not teach it.
17. **`GET /v1/checkouts/{id}` unused** by both SPAs. Headless poll door is undocumented in the merchant README (host README curl mentions it).
18. **Vitest is grep + unit, not in CI.** 035–061 can regress without the `pay` job noticing (it only `tsc` + `vite build`).

### 11.3 Production topology (dogfood)

19. **`deploy/prod/Caddyfile` has no Pay, no `/c/`, no 5178.** Hub `hub.lazuar.com` only. Shipping the cashier to production is not a Caddy edit in this file; it is a new site.
20. **Compose CORS/CheckoutBaseUrl/VITE_* defaults are laptop.** `1974ac10` added images; it did not add production values. Empty Production CORS **fails boot** (good) if someone remembers not to export the laptop default.
21. **OIDC allow-list is One’s config, not Pay’s.** A production merchant origin that forgot One `REDIRECT_ALLOWLIST` cannot dogfood. Runbook is README prose.

### 11.4 Refuse (do not “solve” by undoing law)

- Do not import `Lazuar.Pay` internals into Vite. Do not add `@repo/api-types-ts`.
- Do not put `client_secret`, `lzr_sk_`, wrap key, CHIP PEM, `ZITADEL_PAT` in `VITE_*`.
- Do not send `id_token` as Bearer to heal opaque access tokens.
- Do not make buyers log into One.
- Do not retarget `lazuar-ops` / `lazuar-portal` at 8081.
- Do not collect PAN on :5179 or on a second app.
- Do not treat success_url / `?status=verifying` as paid.
- Do not AllowAnyOrigin.
- Do not invent Test in Production because the SPA wants a default.
- Do not split Pay into four processes so the SPA can “be headless.” Headless is an HTTP sequence, not a fleet.
- Do not copy Hub’s sample onto 8081 and call it done — wrong doors.

---

## 12. How to solve (ranked)

Treat the SPAs as **reference clients**. Publish the headless sequence. **Do not require Vite to take money.** Ranked for this slice; 02/03 are named because they are the actual blockers.

### R1 — Publish the live three-call path as the kernel (docs + host `pay_url`) — first

The HTTP is already there for one-off checkouts. What is missing is honesty in the response and a page a stranger can open without reading this evaluation.

- Add `pay_url` (and maybe `start_url`) on `CheckoutSession` and `PaymentLinkView`, built from `Pay:CheckoutBaseUrl` + `/c/{public_token}`. Merchant Copy uses **that** field; delete SPA synthesis as the primary path. 041 longer-term, still right.
- Document in Pay README (already has mint curl) the start + poll loop, and that **success_url is not paid**.
- Merchant dialog: optional success/cancel for **one-off** mint, or a second button “Checkout for my app” that POSTs `/v1/checkouts` instead of `/v1/payment-links`. First-party UI should exercise the kernel door it claims to be a client of. Bezos is not only “don’t import internals”; it is “dogfood the door you sell.”

This is a small host + SPA change. It does not invent keys or webhooks. It stops requiring folklore to know the hosted URL.

### R2 — Do not require Vite to take money — already true; say it and test it

Add a hermetic (or documented) path:

1. `POST /v1/checkouts` with success_url on example.test
2. `POST /v1/pay/{token}/start`
3. For Test, assert fulfill happened **without** any request to :5179
4. `GET /v1/pay/{token}` is `paid`

`SeedCheckout` + Test rail already almost is this. Name it `Headless_test_processor_pays_without_checkout_spa` so CI guards the claim. README: “Vite is a reference pixel, not a dependency of `FulfillPaidAsync`.”

Payment-link start still bakes CheckoutBaseUrl into child success URLs — either add optional success_url on links **or** document that **links are the cashier product** and **checkouts are the kernel product**. Mixing them is how integrators get stuck on :5179.

### R3 — Machine keys (02) — without this, “headless” is a stolen user JWT

A second app cannot keep a staff browser open for silent renew. MemberGate must accept something that is not `GET /me` of a human, or One must mint `lzr_sk_` that Pay understands. Until then, every sentence that says “writer Bearer” is a first-party staff token. Rank this immediately after R1/R2 because R1 without R3 still cannot leave the building.

Do **not** put the key in Vite. Merchant stays PKCE. Server sample holds the key.

### R4 — Outbound `payment.completed` (03) — without this, poll is the product

Checkout’s verifying pixel is a 30-second apology. Servers can poll receipts with a member token; they should not have to. Hub sample’s whole point was verify-then-unlock. Port **that** ritual to focused Pay (algorithm, retries, `whsec_`), not the Hub modules.

Until 03 exists, published headless docs must say **poll**, and must say JWT expiry makes poll worse than it looks.

### R5 — A Pay sample under `examples/` that is not Hub and not these SPAs

Clone the **shape** of `hub-cashier-next` (Next route handler, toy order, success page that does not unlock, webhook route) onto **8081** `/v1/checkouts` + `/v1/pay/{token}/start`. Until 03, the sample polls. After 03, the sample verifies. Exclude from product turbo. No `@repo/api-types-ts`. No gateway SDKs. Port **3020** is free.

This is 006’s G3 aimed at the **new** host. Leaving Hub’s sample in-tree without a banner “does not speak 8081” is an integrator foot-gun.

### R6 — CORS / bake-ins for a second origin (06 + leftover 049/050/041)

- Merchant Vite: fail production build if `VITE_PAY_API_URL` or `VITE_CHECKOUT_ORIGIN` empty (copy checkout’s `requirePayApiUrl`). Kill `?? 'http://localhost:8081'` in `payApi.ts` the same way.
- CI `pay` job: export dummy public origins (`http://pay.test`, `http://checkout.test`) so production builds are the thing CI proves; run `pnpm --filter lazuar-pay-merchant test` and `…-checkout test`.
- Compose example values for a **second** origin commented, not only 5178/5179.
- Host door `GET /v1/meta` or extend `/v1/orgs/{id}/ready` with `public_base_url` + `checkout_base_url` so SPA/integrator do not guess. Optional; `pay_url` on mint (R1) is better.

### R7 — Treat :5178 / :5179 as reference implementations, not the SDK

Extract a tiny `packages/pay-client-ts` **only** after the host is stable (sibling 09). Until then, a 40-line `fetch` snippet in docs is more honest than a generated client of a moving spec. IsolationTests should keep Vite free of Hub types; they may allow a future `@repo/pay-client` that is Pay’s, not Hub’s — that is a later argument.

Do **not** rewrite merchant in Next to look like the sample. Do **not** serve checkout from the C# host as Razor pages “to be headless.” The pixel can stay Vite. The **door** is `/v1`.

### R8 — Polish after money is boring (this slice)

Overview single h1; HomePage Sign out on whoami 503; whoami timeout; copyable absolute webhook URL; Idempotency-Key on payment-link create or disable the button while `busy` (already disabled — still no key); document `GET /v1/checkouts/{id}` in merchant README as the poll door.

---

## 13. Honesty table — what we may say on `6d730d15`

**May say:**

- Merchant :5178 and checkout :5179 are HTTP clients of focused Pay `/v1`. They do not import host internals or Hub `@repo/api-types-ts`.
- Staff sign-in is One product login :5175 (Zitadel :8085), public PKCE, `access_token` only, sessionStorage, silent renew on `/silent-renew.html`.
- Buyers have no One account. Checkout sends no Bearer. Success URL is not paid; the page polls until Plane B (or Test start) writes Official Receipt.
- A writer with a human JWT can `POST /v1/checkouts` and `POST /v1/pay/{token}/start` and poll `GET /v1/pay/{token}` **without either Vite app**, especially on Test in Development with `success_url` set.
- 019 silent list GET, Loading graveyard, Test-injected, Thank-you-to-strangers, silent-renew-on-callback, opaque-token livelock are **fixed** in the SPA source.
- CORS is configurable; Production empty fails boot; 3003/3004 stay denied.

**Must not say:**

- Another app can integrate without cloning this repo. They still need a human JWT, poll, folklore for `pay_url`, and CORS/OIDC ceremony if they are browsers.
- Vite is required to take money. It is required only if you want **this** pixel or you omitted `success_url` on a one-off / used payment-links.
- We have a Stripe-shaped kernel. No `lzr_sk_`, no `payment.completed`, no Pay sample.
- Merchant production builds fail closed on missing API URL. Only checkout + Dockerfiles do.
- Copy button URL is the host’s CheckoutBaseUrl. It is the Vite env.
- `examples/hub-cashier-next` proves focused Pay. It proves Hub.
- `deploy/prod/Caddyfile` serves `/c/{token}`.
- List GET / Loading / Test bugs are still the 019 SHA. They are not; do not re-open 035–061 as open P1s without a new live miss.

---

## 14. Sequence diagram (headless vs SPA)

### 14.1 First-party cashier (what :5178 / :5179 actually do)

```
staff browser :5178
  → OIDC :5175 / :8085
  → GET /v1/whoami (Bearer JWT)
  → PUT /v1/orgs/{org}/gateway          (optional, writer)
  → POST /v1/orgs/{org}/products        (label)
  → POST /v1/payment-links              (writer; no success_url)
  → Copy {VITE_CHECKOUT_ORIGIN}/c/{token}

buyer browser :5179
  → GET /v1/pay/{token}?slot_key=uuid   (no Bearer)
  → POST /v1/pay/{token}/start {name,email,slot_key}
  → 302-by-assign to PSP (or Test success URL)
  → return ?status=verifying
  → poll GET /v1/pay/{token} until paid | timeout

staff :5178
  → GET /v1/orgs/{org}/payments
  → GET /v1/orgs/{org}/receipts
```

No `POST /v1/checkouts`. No machine key. No app webhook.

### 14.2 Headless one-off (live HTTP, undogfooded by UI)

```
app server
  → POST /v1/checkouts {org_id, amount, provider, success_url, cancel_url}  Bearer writer JWT
  → POST /v1/pay/{public_token}/start {name, email}                         no Bearer
  → redirect human to redirect_url
  → loop GET /v1/pay/{token}  OR  GET /v1/checkouts/{id}  OR  GET …/receipts
  → unlock toy order when status==paid
```

Vite never appears **if** `success_url` is the app. Missing: key, event, sample, dashboard button.

### 14.3 What people will try and how it fails

| Attempt | Result on this SHA |
|---------|-------------------|
| Point Hub sample at 8081 | 404 on `/api/v1/integrations/payments/checkouts`; no `sk_` |
| Embed checkout fetches from :3020 | CORS deny unless `Pay:CorsOrigins` includes :3020 |
| Mint payment-link, expect success_url on their app | PSP returns to :5179; their app never sees the buyer |
| Build merchant without `VITE_PAY_API_URL` | Inlines localhost:8081 |
| Build checkout without `VITE_PAY_API_URL` | Vite throws (good) / CI job as written has no env |
| Send `id_token` to whoami | 401; picker would have refused in first-party |
| Buyer Sign in | There is no such pixel |
| Poll forever with a JWT | Token expires; silent renew is a browser iframe, not a server |

---

## 15. Door inventory the SPAs teach (copy-paste for 09)

If 09 needs a “reference client taught these paths” list, this is the live set.

Merchant teaches:

- `GET /v1/whoami`
- `GET /v1/orgs/{id}/gateways`
- `PUT /v1/orgs/{id}/gateway`
- `POST /v1/orgs/{id}/products`
- `POST /v1/payment-links`
- `GET /v1/orgs/{id}/payment-links`
- `GET /v1/orgs/{id}/payments`
- `GET /v1/orgs/{id}/receipts`
- One `POST /tenants` (not Pay)
- One `POST /tenants/{id}/apps` via `register-spa.sh` (not runtime)

Checkout teaches:

- `GET /v1/pay/{token}?slot_key=`
- `POST /v1/pay/{token}/start`

Host tests additionally teach (SPA silent):

- `POST /v1/checkouts`
- `GET /v1/checkouts/{id}`
- `GET /v1/orgs/{id}/checkouts` (one-offs only; `PaymentLinkId == null`)
- public GET/start CORS
- Test auto-fulfill

A second app that only copies **SPA** traffic will never discover the kernel mint door. A second app that only copies **README curl** will never discover payment-links occupancy. Both doors are real. The product has not picked which one it sells. That is the headless-vs-SPA bug above the UI bugs 002 already closed.

---

## 16. Verdict for the parent 00

**Bezos door holds.** `:5178` and `:5179` are clients of `/v1` (and One `/api/v1` for tenant create). They do not import `internal/`. IsolationTests and locks still ban Hub types. 002 UI 035–061 are live-fixed: silent list GET, Loading graveyard, Test injection, hardcoded checkout API on the **buyer** app, Thank-you-to-strangers, silent-renew iframe, occupancy copy vs start+TTL.

**Vite is not required to take money** on the one-off checkout door with `success_url` set, a writer JWT, and poll. **Vite is required** to run the product the dashboard actually sells (payment-links + hosted pixel), and is accidentally required if you omit `success_url` or mint a link.

**A stranger cannot integrate without cloning this repo** in the sense 020 cares about: no machine key, no outbound event, no `pay_url`, no Pay sample, laptop bake-ins on the staff app, Hub sample pointing at the museum. They can copy **fetch** lines from these SPAs and from Pay README. That is a reference client, not a kernel.

**How to solve, this slice only:** (1) return `pay_url` and dogfood `POST /v1/checkouts` from somewhere first-party; (2) CI-lock headless Test pay without :5179; (3) sibling 02 keys; (4) sibling 03 events; (5) a Pay `examples/` that is not Hub; (6) fail-closed merchant `VITE_*` like checkout. Do not require Vite. Do not split the binary. Do not reopen 035–061 as open P1s.

End of 08. Parent 00 must not treat this file as a substitute for 01–07, 09, 10. Live files on `6d730d15` win if they disagree with 019.
