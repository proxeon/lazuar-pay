# 02 — Merchant Vite (`lazuar-pay-merchant` :5178): staff shell, One OIDC, money UI

**Date:** 24 August 2026  
**Type:** Uncondensed evaluation. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) cells. **Not** a retarget of `lazuar-ops` at 8081. **Not** a design of the hosted checkout page (sibling [03-checkout-frontend.md](./03-checkout-frontend.md)).  
**Slice:** current state of `apps/lazuar-pay-merchant` (Vite **:5178**). Staff shell. One OIDC. Not `lazuar-ops`. How it talks to Pay **:8081** and One. What money/gateway UI exists. What is missing for pasting keys, listing receipts, creating products.  
**Parent program:** [`plans/014-evals`](./README.md) — evaluate new Lazuar Pay, then port Hub gateway adapters as HTTP judgment.  
**Historical sibling this file is allowed to disagree with:** [`plans/013-prods/04-merchant-frontend.md`](../013-prods/04-merchant-frontend.md) (21 August 2026). That paper was written when this app was a **health probe**. Live files on this SHA are authority.

Standing law (do not relitigate):

| Lock | Meaning |
|------|---------|
| New merchant UI is **this app** on **5178** | Do not retarget `lazuar-ops` (`:3003`) at 8081. P60. |
| Never put secrets in Vite | No `sk_live_`, CHIP Bearer, AES wrap key, `ZITADEL_PAT`, `client_secret`, `lzr_sk_` in `VITE_*`. Public surface is `VITE_PAY_API_URL`, `VITE_ONE_API_URL`, and OIDC `client_id` / authority / redirect. |
| Bearer is `access_token` | Never `id_token`. Picker: `pickApiBearerToken`. |
| VIEWER honesty | One has no membership role `viewer`. Roles are `owner` / `admin` / `member`. Enforce with One authz that exists. Do not fake a Viewer chip. |
| IsolationTests Vite ban | `package.json` must not depend on Hub `@repo/api-types-ts`. |

---

## 0. Method / SHA

Recorded at write time. Re-open files on a later SHA before treating a line as still true.

| Repo | Path | Branch | Full SHA | Short | Tip |
|------|------|--------|----------|-------|-----|
| **lazuar-pay** | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `main` | `ee2db8e5758305089a38298456c456d6bf0e97ca` | `ee2db8e5` | `feat(pay): Bar B receipts, webhook secret, merchant money UI` |

`.git/HEAD` is `ref: refs/heads/main`. Reflog tip is the fast-forward of `feat/013-bar-b` onto `main`. `git log -1 --oneline` at this write:

```text
ee2db8e5 feat(pay): Bar B receipts, webhook secret, merchant money UI
```

014 index ([README.md](./README.md)) recorded the same HEAD at analysis start. This paper re-opened the live tree rather than trusting 013’s SHA `6f866ff0` (`feat(pay): scaffold merchant and checkout Vite apps`, 21 August 2026).

Opened, in this repo:

- `apps/lazuar-pay-merchant/**` — every source file under `src/`, plus `package.json`, `vite.config.ts`, `vitest.config.ts`, `tsconfig*.json`, `index.html`, `README.md`, `.env.example`, `scripts/register-spa.sh`.
- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`, `Gateways/GatewayEndpoints.cs`, `Money/PaymentQueryEndpoints.cs`, `Catalog/CatalogEndpoints.cs`, `Checkouts/{CheckoutEndpoints,CheckoutSession,CreateCheckoutRequest,CheckoutStore}.cs`, `One/{WhoamiEndpoints,WhoamiResponse,MemberGate,Bearer,OneClient,OneMeMapper,OrgReadyEndpoints,PayErrors,OneAuthz}.cs`, `PublicPay/PublicPayEndpoints.cs`, `Secrets/SecretBox.cs`, `Data/Rows.cs`, `tests/Lazuar.Pay.Tests/{IsolationTests,CorsTests,CatalogTests,CheckoutTests,WebhookTests,WhoamiTests,PayApiFactory}.cs`.
- `packages/pay-spec/main.tsp` (catalog/whoami/checkouts; **no** gateway/payments/receipts in the spec).
- `apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx` — **judgment only**. Not copied.
- `apps/lazuar-ops/src/lib/api-client.ts` — negative example (Hub types + cookie).
- `Taskfile.yml` `pay:merchant`, `.github/workflows/ci.yml` `pay` job, `pnpm-workspace.yaml`, root `.gitignore`.
- 011 `11-checklist.md` rows `NP-ONE-*` (merchant-facing), `NP-GW-009`, `NP-CAT-005`, `NP-FUL-*` receipt/payments UI, `NP-DOC-005`, `NP-API-004`.
- 013 `04-merchant-frontend.md` (historical); checklists `m10`–`m27`, `cat10`–`cat13`/`cat15`, `g11`–`g15`, `f19`–`f21`, `o12`, `q10`–`q12`.
- 014 `README.md`.

Not opened as product design: `lazuar-portal`, Hub `lazuar-admin`, One `lazuar-admin`. Checkout internals belong to paper 03.

---

## 1. Answers, then the rest of the paper

1. **`lazuar-pay-merchant` today is no longer a health probe.** It is a React 19 + Vite staff shell on **http://localhost:5178** (`strictPort`), with `react-router-dom` routes, `react-oidc-context` PKCE against Zitadel `:8085` (password UI on One login `:5175`), sessionStorage tokens, and a workspace page that pastes a Stripe secret, creates a product + pay link, and lists products / payments / receipts from Pay `:8081`. 013/04 §2 is **stale**. Name the disagreement in §12.

2. **It talks to two HTTP hosts, not Hub.** Pay `:8081` for whoami projection, catalog, gateway PUT, checkouts, payments, receipts. One `:8080/api/v1` for `POST /tenants` only. It does **not** call One `GET /me` as the session door. It does **not** call Hub `:8080`, ops `:3003`, or admin `:5173`. Pay CORS already allows 5178 and denies 3003.

3. **Money UI that exists is one page (`WorkspacePage`), not six polished screens.** Owner/admin see a plaintext Stripe `sk_test_` input and a “Create pay link” form. Everyone who is a member sees three `<ul>` lists. There is no GET of gateway metadata, no last4 hint, no receipt-by-id click, no product edit, no subscribers, no wrap-rails copy, no CHIP/Billplz tabs.

4. **What a merchant can actually click today:** Sign in → One login `:5175` → callback → workspace list (or create tenant on One) → open `/o/:orgId` → (if owner/admin) Save key / Create pay link → copy a `:5179/c/{token}` URL for a buyer who has no One account → look at name-only products, amount/currency/status payments, number/title receipts. Member sees the lists and a sentence that they cannot paste keys. Sign out lives only on the home list.

5. **Missing for the dogfood sentence (paste keys, list receipts, create products):** the verbs exist as HTTP and as chrome, but they are thin, silent on list errors, not wired to GET metadata, not linked (product → checkout has no `product_id`), not covered by merchant tests, and 011 still marks `NP-CAT-005` / `NP-GW-009` / `NP-FUL-003` / `NP-DOC-005` / `NP-API-004` as **todo**. Checklist cells in 013 were ticked; this paper does not flip 011.

6. **VIEWER:** `canWriteMoney` is `owner || admin`. Copy says “VIEWER-class / member cannot.” There is no `viewer` branch. Pay write routes use `MemberGate.RequireWriterAsync` (membership via `authz/check member`, then **whoami role string** `owner`/`admin` — not `authz/check admin`). API 403 is the real gate; chrome hide is not.

---

## 2. What merchant Vite is today (live tree)

### 2.1 Package, port, scripts

`apps/lazuar-pay-merchant/package.json` on `ee2db8e5`:

| Field | Value |
|-------|-------|
| name | `lazuar-pay-merchant` |
| private | true |
| version | `0.0.0` |
| type | `module` |
| `dev` | `vite --port=5178 --host=0.0.0.0 --strictPort` |
| `preview` | `vite preview --port=4178 --strictPort` |
| `build` | `tsc -b && vite build` |
| `lint` | `oxlint` |
| `test` | `vitest run` |
| `check-types` | `tsc -b` |
| `clean` | `rm -rf dist` |
| dependencies | `react` `^19.2.8`, `react-dom` `^19.2.8`, **`oidc-client-ts` `^3.4.1`**, **`react-oidc-context` `^3.3.0`**, **`react-router-dom` `^7.15.0`** |
| devDependencies | `@types/node`, `@types/react`, `@types/react-dom`, `@vitejs/plugin-react`, `oxlint`, `typescript` `~6.0.2`, `vite` `^8.2.0`, **`vitest` `^3.2.4`** |

**Not present (and still must not be added as Hub leftovers):** `@repo/api-types-ts`, `openapi-fetch`, `@tanstack/react-query`, Tailwind/shadcn cathedral, `@google/genai`, Express, cookie session, `lazuar-ops` as a package.

013/04 §2.1 said dependencies were **only** `react` and `react-dom`, and listed `react-router-dom` / `oidc-client-ts` / `react-oidc-context` as “will be needed later — from **One app**, not from ops.” Those three are now in the file. That was the intended steal. Hub `openapi-fetch` + `@repo/api-types-ts` still is not.

Workspace membership: root `pnpm-workspace.yaml` includes `apps/*`. `Taskfile.yml`:

```yaml
pay:merchant:
  desc: Merchant Vite shell on http://localhost:5178 (not lazuar-ops)
  cmds:
    - pnpm --filter lazuar-pay-merchant dev
```

`apps/lazuar-pay/README.md` still documents `task pay:merchant` → `:5178` staff shell (not `lazuar-ops` `:3003`). Compose still points at old `apps/lazuar-api`. That dual-run is honest: this origin is the new merchant UI even while Hub ops still exists on 3003.

### 2.2 Vite config — dual-pinned `strictPort` (unchanged from 013, still correct)

`apps/lazuar-pay-merchant/vite.config.ts`:

```ts
// Dual-pinned with package.json `vite --port=5178`.
// strictPort: fail loud if 5178 is busy — never silently steal login :5175 or checkout :5179.
export default defineConfig({
  plugins: [react()],
  server: {
    host: true,
    port: 5178,
    strictPort: true,
  },
  preview: {
    host: true,
    port: 4178,
    strictPort: true,
  },
})
```

`package.json` and `vite.config.ts` **both** pin 5178. Preview is **4178**. Checkout preview is 4179. If 5178 is taken, Vite **fails**; it does not hop to login or checkout.

`index.html` title: `Lazuar Pay — merchant`. Favicon: `public/favicon.svg` (Lazuar mark). No “Lazuar Console”.

`vitest.config.ts`:

```ts
export default defineConfig({
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
})
```

Tests run in **node**, not jsdom. There is no component test of `WorkspacePage`. There is no Playwright.

### 2.3 Source tree (complete — this is the whole app)

There is still no `src/modules`. There is now `src/pages`, `src/auth`, `src/lib`. Every file:

| Path | What it is |
|------|------------|
| `index.html` | Root HTML; title “Lazuar Pay — merchant”; script `/src/main.tsx` |
| `src/main.tsx` | `StrictMode` + `AuthProvider` + `BrowserRouter` + `<App />` |
| `src/App.tsx` | **Five routes.** Not a health probe. |
| `src/App.css` | Layout (`max-width: 40rem`), kicker, form fonts |
| `src/index.css` | Page defaults (`#f6f5f3`, system-ui) |
| `src/auth/oidcConfig.ts` | PKCE config; sessionStorage user store |
| `src/auth/bearerToken.ts` | `pickApiBearerToken` — JWT `access_token` only |
| `src/auth/bearerToken.test.ts` | Never returns `id_token`; opaque/JWE → undefined |
| `src/auth/RequireAuth.tsx` | Gate: loading / error / redirect to `/login` |
| `src/lib/payApi.ts` | `VITE_PAY_API_URL` default `http://localhost:8081`; `getWhoami`; `payFetch` |
| `src/lib/oneApi.ts` | `VITE_ONE_API_URL` default `http://localhost:8080/api/v1`; `createTenant` |
| `src/lib/roles.ts` | `canWriteMoney` = owner/admin |
| `src/lib/sessionKeys.ts` | `returnTo` + org hint in sessionStorage |
| `src/pages/LoginPage.tsx` | Sign in button. No password fields. |
| `src/pages/CallbackPage.tsx` | Completes OIDC; never prints tokens |
| `src/pages/HomePage.tsx` | Whoami tenant list / empty create CTA / sign out |
| `src/pages/CreateWorkspacePage.tsx` | One `POST /tenants` |
| `src/pages/WorkspacePage.tsx` | **The money UI.** Keys, product+link, lists. |
| `src/locks.test.ts` | Grep: no password form, no Hub login, no Hub types |
| `tsconfig.json` / `tsconfig.app.json` / `tsconfig.node.json` | Project refs; `"types": ["vite/client"]`; no `src/vite-env.d.ts` |
| `.env.example` | Pay URL + Zitadel public OIDC + One URL. Empty `CLIENT_ID`. |
| `README.md` | Origin, register SPA, allowlist, live whoami, must-not |
| `scripts/register-spa.sh` | One `POST /tenants/{id}/apps` type `spa`; optional `WRITE_ENV=1` |
| `vitest.config.ts` | node env |
| `dist/` | Local leftover of the **old health-probe bundle** (`index-DuegEQIu.js` — the same hash 013 quoted). Root `.gitignore` includes `dist/`. Dev does not use it. `vite preview` without a rebuild would lie. |

013/04 §2.3 said “There is no `src/modules`, no `src/pages`, no `src/auth`, no `src/lib`. The entire application is: `main.tsx` + `App.tsx` + CSS.” That inventory is **false on this SHA**. New screens are new files, as 013 predicted they would be. They landed.

Source weight vs the cathedral: merchant `src/` is **18** files (including two test files). 013/01 contrasted “Merchant+checkout `src/` is **8** files (4+4), vs ops **122** `.tsx`.” The merchant half grew from 4 to 18. It did **not** import 122 ops tsx files. That is the right direction.

### 2.4 Env vars (public only)

`apps/lazuar-pay-merchant/.env.example`:

```text
# Focused Pay host. Never Hub :8080. Never point lazuar-ops here.
VITE_PAY_API_URL=http://localhost:8081

# Public SPA OIDC (PKCE). No client_secret. Never ZITADEL_PAT.
VITE_ZITADEL_AUTHORITY=http://localhost:8085
VITE_ZITADEL_CLIENT_ID=
VITE_ZITADEL_REDIRECT_URI=http://localhost:5178/callback
VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI=http://localhost:5178/
VITE_ZITADEL_SCOPE=openid profile email offline_access

# One HTTP for workspace create (Ada Bearer). Not Pay org CRUD.
VITE_ONE_API_URL=http://localhost:8080/api/v1
```

| Env | Default in code if unset | Secret? | Used by |
|-----|--------------------------|---------|---------|
| `VITE_PAY_API_URL` | `http://localhost:8081` | no | `payApi.ts` |
| `VITE_ONE_API_URL` | `http://localhost:8080/api/v1` | no | `oneApi.ts` |
| `VITE_ZITADEL_AUTHORITY` | `http://localhost:8085` | no (issuer, **not** `:5175`) | `oidcConfig.ts` |
| `VITE_ZITADEL_CLIENT_ID` | `''` | **public**; empty disables Sign in | `oidcConfig.ts`, `LoginPage` |
| `VITE_ZITADEL_REDIRECT_URI` | `http://localhost:5178/callback` | no | `oidcConfig.ts` |
| `VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI` | `http://localhost:5178/` | no | `oidcConfig.ts` |
| `VITE_ZITADEL_SCOPE` | `openid profile email offline_access` | no | `oidcConfig.ts` |

Grep of the merchant app for `sk_live`, `VITE_STRIPE`, `VITE_CHIP`, `ZITADEL_PAT` as a Vite value: **none** in source. `client_secret` appears only in `register-spa.sh` as a **reject** if One returns one for `type: spa`. Wrap key `Pay:WrapKey` lives on the **Pay process** (`SecretBox.cs`), never here.

Root `.gitignore` ignores `.env` and allows `.env.example`. `WRITE_ENV=1` writes gitignored `.env`.

**Honesty:** G11 said “`:5178` / `:5179` stay `VITE_PAY_API_URL` only.” Live merchant also has OIDC + One URL. That is the public SPA surface 013/04 §4.5 already listed as the later env table. It is not a Stripe secret. The G11 sentence is still true for **PSP keys**.

Missing from env, and the SPA hardcodes it: checkout origin `http://localhost:5179` in `WorkspacePage` when it prints the buyer URL. There is no `VITE_CHECKOUT_URL`. Staging that origin will 404 Ada’s copied link unless the string is edited.

### 2.5 Routes (live `App.tsx`)

`src/App.tsx` in full:

```tsx
export default function App() {
  return (
    <Routes>
      <Route path="/callback" element={<CallbackPage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/"
        element={
          <RequireAuth>
            <HomePage />
          </RequireAuth>
        }
      />
      <Route
        path="/workspaces/new"
        element={
          <RequireAuth>
            <CreateWorkspacePage />
          </RequireAuth>
        }
      />
      <Route
        path="/o/:orgId"
        element={
          <RequireAuth>
            <WorkspacePage />
          </RequireAuth>
        }
      />
    </Routes>
  )
}
```

| Path | Auth | Page | Job |
|------|------|------|-----|
| `/callback` | public (OIDC return) | `CallbackPage` | Code exchange landing. Not the product homepage. |
| `/login` | public | `LoginPage` | Sign-in CTA. No password form. |
| `/` | `RequireAuth` | `HomePage` | Workspaces from Pay whoami. |
| `/workspaces/new` | `RequireAuth` | `CreateWorkspacePage` | One `POST /tenants`. |
| `/o/:orgId` | `RequireAuth` | `WorkspacePage` | Money UI. Path org id is SoT. |

No `/health` UI. No `/pricing`. No `/forgot-password`. No `/invoicing/*`. No `/commerce/*`. No `/ops/chat`. Nav is not a Hub sidebar.

`main.tsx` wraps the router with `AuthProvider`:

```tsx
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AuthProvider {...getOidcConfig()}>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </AuthProvider>
  </StrictMode>,
)
```

That is the One `lazuar-app` pattern 013/04 §4.4 asked for. Ops `LoginPage` + cookie is not in this tree.

---

## 3. Auth flow (what the browser actually does)

### 3.1 OIDC config

`src/auth/oidcConfig.ts`:

```ts
export function getOidcConfig(): AuthProviderProps {
  const authority =
    import.meta.env.VITE_ZITADEL_AUTHORITY || 'http://localhost:8085'
  const client_id = import.meta.env.VITE_ZITADEL_CLIENT_ID || ''
  const redirect_uri =
    import.meta.env.VITE_ZITADEL_REDIRECT_URI ||
    'http://localhost:5178/callback'
  const post_logout_redirect_uri =
    import.meta.env.VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI ||
    'http://localhost:5178/'
  const scope =
    import.meta.env.VITE_ZITADEL_SCOPE ||
    'openid profile email offline_access'

  return {
    authority,
    client_id,
    redirect_uri,
    post_logout_redirect_uri,
    scope,
    response_type: 'code',
    automaticSilentRenew: true,
    userStore: new WebStorageStateStore({ store: window.sessionStorage }),
    onSigninCallback: () => {
      window.history.replaceState({}, document.title, window.location.pathname)
    },
  }
}
```

Facts:

| Fact | Value |
|------|--------|
| Grant | Authorization code. PKCE S256 is the `oidc-client-ts` default. |
| Authority | Zitadel **:8085**, not login :5175. |
| Tokens | `sessionStorage` key `oidc.user:{authority}:{client_id}` on origin `:5178` (port-scoped). |
| `client_secret` | **not in the settings object** |
| Callback URL strip | `history.replaceState` so `?code` does not stay in the address bar |

### 3.2 Bearer picker (never `id_token`)

`src/auth/bearerToken.ts`:

```ts
export function isJwtLike(token: string | undefined | null): boolean {
  if (!token) return false
  const parts = token.split('.')
  return parts.length === 3 && parts.every((p) => p.length > 0)
}

export function pickApiBearerToken(user: User | null | undefined): string | undefined {
  if (!user) return undefined
  if (isJwtLike(user.access_token)) return user.access_token
  return undefined
}
```

`bearerToken.test.ts` locks:

- signed out → `undefined`
- JWT `access_token` + JWT `id_token` → returns **access**, not id
- opaque access + JWT id → `undefined` (no fallback)
- empty access + JWT id → `undefined`
- JWE (5 dots) → `undefined`

This is the One `lazuar-app` / `examples/vite-spa` policy 013/04 quoted. It is live.

`HomePage` / `WorkspacePage` / `CreateWorkspacePage` call `pickApiBearerToken(auth.user)` **during render**, not in a `useEffect` that would 401 the first paint. That matches M12.3.

**UX hole:** if Zitadel ever issues an opaque `access_token`, `HomePage` sees `!token` and immediately `auth.signinRedirect()`. That is an infinite login loop, not a “token is not a JWT” alert. The picker is still right (do not heal with `id_token`). The chrome is not.

### 3.3 Sequence (happy path, live)

```text
1. Ada opens http://localhost:5178
2. RequireAuth: not authenticated → <Navigate to="/login" state.from=/ >
3. LoginPage: if VITE_ZITADEL_CLIENT_ID empty → alert + disabled button
               else Sign in → setReturnTo(from) if from !== '/'
               → auth.signinRedirect()
4. Browser hits Zitadel :8085 /oauth/v2/authorize
     client_id    = Pay merchant public SPA
     redirect_uri = http://localhost:5178/callback
     response_type= code
     scope        = openid profile email offline_access
     PKCE         = S256
5. Zitadel 302 → http://localhost:5175/login?authRequest=…
6. Ada types password on :5175 (not on :5178)
7. Browser returns to http://localhost:5178/callback?code&state
8. react-oidc-context exchanges code at :8085
     Tokens in sessionStorage
     onSigninCallback strips the query
9. CallbackPage: takeReturnTo() or Navigate to /
10. HomePage: pickApiBearerToken → GET http://localhost:8081/v1/whoami
      Authorization: Bearer <access_token>
      Accept: application/json
      (no X-Lazuar-Tenant-Id on the home call)
11. Pay OneClient GET http://localhost:8080/api/v1/me with the same Authorization
12. SPA renders tenants[]. Click → sessionStorage org hint + navigate /o/{id}
13. WorkspacePage: setOrgHint; GET /v1/whoami with X-Lazuar-Tenant-Id: {orgId}
      then GET products, payments, receipts in parallel
```

Login copy (`LoginPage.tsx`):

```tsx
<p>
  Sign-in uses One product login at <code>:5175</code>. This page is not a
  password form. Not <code>lazuar-ops</code> (<code>:3003</code>), not
  staff admin (<code>:5173</code>).
</p>
```

There is **no** `type="password"` in merchant `src` (locks.test.ts greps for it). There is **no** `POST /one/auth/login`. There is **no** `lazuar_auth`.

`RequireAuth` on error shows `auth.error.message` and a Retry login button. It does not dump tokens.

`CallbackPage` on error: “Login failed” + message + Try again. On success: `Navigate` to `returnTo || '/'`. `/callback` is not a product page.

### 3.4 Register the SPA (M10 script — in-repo tool, not a proof that One has the app object)

`scripts/register-spa.sh`:

- Requires `ACCESS_TOKEN` (JWT access, not id) and `TENANT_ID`.
- `ONE_API_BASE` default `http://localhost:8080/api/v1`.
- `POST $API_BASE/tenants/$TENANT_ID/apps` body `{name, type:"spa", redirect_uris:[http://localhost:5178/callback], post_logout_redirect_uris:[http://localhost:5178/]}`.
- Prints `client_id`. **Fails** if `client_secret` is present.
- `WRITE_ENV=1` rewrites gitignored `.env` `VITE_ZITADEL_CLIENT_ID=…`.
- Explicitly refuses `ZITADEL_PAT` (“That is One ops, not Pay”).

Chicken and egg is still real: Way B needs a user JWT from **lazuar-app** `:5174` before this origin can log in. The script is the in-repo happy path. A One-repo seed like `seed-platform-spa-clients.sh` is **not** in this Pay tree. Whether Ada’s One tenant actually has the app object is a **runbook** fact (M26), not something IsolationTests can see.

Login `REDIRECT_ALLOWLIST` and One `App:CorsOrigins` live in the **One** repo. Merchant README documents both. This evaluation cannot prove they are set on the developer’s laptop.

### 3.5 Token storage and cookies

`sessionKeys.ts`:

```ts
/** sessionStorage only — not an authz cookie. */
export const RETURN_TO_KEY = 'lazuar-pay-merchant:returnTo'
export const ORG_HINT_KEY = 'lazuar-pay-merchant:orgId'
```

`isSafeReturnPath`: must start with `/` and must not start with `//` (blocks protocol-relative open redirects).

Org hint is UX only. `WorkspacePage` still authorizes by matching `who.tenants` to the **path** `orgId`. A forged sessionStorage id does not mint membership.

`payApi.ts` comment: “credentials omitted on purpose: localhost cookies are not port-scoped.” Grep of merchant `src` for `credentials: "include"`: **none**. Fetch default on this cross-origin call does not send Hub `lazuar_auth`. Pay CORS still does **not** `AllowCredentials` (`Program.cs`).

013/04 §6.2 cookie foot-gun still applies if someone later adds `credentials: "include"` and then “fixes” CORS. Live code has not done that.

Logout: `HomePage` only, `auth.signoutRedirect()`. Workspace page has no Sign out. Clearing session without `end_session` would leave Zitadel SSO; this button does the OIDC path.

---

## 4. How the SPA talks to Pay :8081 and One

### 4.1 Pay client

`src/lib/payApi.ts` (entire module, because it is the only Pay client):

```ts
const payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'

export async function getWhoami(
  accessToken: string,
  orgHint?: string | null,
): Promise<Whoami> {
  const headers: Record<string, string> = {
    Authorization: `Bearer ${accessToken}`,
    Accept: 'application/json',
  }
  if (orgHint) {
    headers['X-Lazuar-Tenant-Id'] = orgHint
  }
  const response = await fetch(`${payApi}/v1/whoami`, { headers })
  if (response.status === 401) {
    throw new Error('unauthorized')
  }
  if (!response.ok) {
    throw new Error(`whoami ${response.status}`)
  }
  return (await response.json()) as Whoami
}

export async function payFetch(
  accessToken: string,
  path: string,
  init?: RequestInit & { orgHint?: string },
): Promise<Response> {
  const headers = new Headers(init?.headers)
  headers.set('Authorization', `Bearer ${accessToken}`)
  headers.set('Accept', 'application/json')
  if (init?.orgHint) headers.set('X-Lazuar-Tenant-Id', init.orgHint)
  return fetch(`${payApi}${path}`, { ...init, headers })
}
```

Hand-written `Whoami` / `WhoamiTenant` types match Pay `WhoamiResponse.cs` snake_case (`user_id`, `is_platform_admin`, `active_org_id`, `tenants[]` of `id/slug/name/role/status`). No `@repo/pay-types-ts` yet. No Hub `@repo/api-types-ts`. IsolationTests and `locks.test.ts` both ban the Hub package.

Header name is **`X-Lazuar-Tenant-Id`**, One’s name. Ops uses **`X-Tenant-Id`** and Hub **authorizes** by it (`api-client.ts` + `localStorage ops_active_workspace_id`). Merchant must not copy that. Live merchant does not: grep for `X-Tenant-Id` in the merchant app is empty.

`payFetch` spreads `init` including `orgHint`. Browsers ignore unknown `RequestInit` keys. Harmless. Trailing slash on `VITE_PAY_API_URL` would produce `http://host:8081//v1/...`. `oneApi.ts` strips trailing slash; `payApi.ts` does not.

Pay `Bearer.TryGet` requires a `Bearer ` prefix and a non-empty token. It does **not** distinguish JWT vs opaque vs `id_token` — it forwards to One. The SPA picker is the first guard.

### 4.2 Every Pay HTTP call the SPA makes

These are **all** of them. If a route is not in this table, the merchant origin does not call it.

| When | Method | Path | Headers | Body | Who |
|------|--------|------|---------|------|-----|
| Home load; workspace load | `GET` | `/v1/whoami` | Bearer; workspace adds `X-Lazuar-Tenant-Id` | — | any signed-in user |
| Workspace refresh | `GET` | `/v1/orgs/{orgId}/products` | Bearer + hint | — | member+ |
| Workspace refresh | `GET` | `/v1/orgs/{orgId}/payments` | Bearer + hint | — | member+ |
| Workspace refresh | `GET` | `/v1/orgs/{orgId}/receipts` | Bearer + hint | — | member+ |
| Save key (owner/admin chrome) | `PUT` | `/v1/orgs/{orgId}/gateway` | Bearer + hint + JSON | `{ provider: 'stripe', secret: sk }` | chrome: write; API: writer |
| Create pay link (owner/admin chrome) | `POST` | `/v1/orgs/{orgId}/products` | Bearer + hint + JSON | `{ name, amount: Number(amount), currency: 'MYR' }` | chrome: write; API: writer |
| Create pay link (same click) | `POST` | `/v1/checkouts` | Bearer + hint + JSON | `{ org_id, amount, currency: 'MYR' }` | chrome: write; API: **member** |

Not called from this SPA:

| Host route | Why it matters |
|------------|----------------|
| `GET /health`, `GET /v1/health` | Health probe **removed**. 013/04’s only fetch is gone. |
| `GET /v1/orgs/{orgId}/ready` | Dummy `check(member)`. Optional badge. Unused. |
| `GET /v1/orgs/{orgId}/gateway` | Masked last4 / `configured` — the G13 chrome source. **Unused.** |
| `GET /v1/orgs/{orgId}/receipts/{id}` | F20 “open receipt”. **Unused.** List is the only view. |
| `GET /v1/checkouts/{id}` | Merchant could poll a session. Unused. |
| `GET /v1/pay/{token}` / `POST …/start` | Buyer plane on `:5179`. Correctly not here. |
| `POST /v1/webhooks/{provider}/{orgId}` | PSP → Pay. Not a browser job. |
| `POST /v1/one/webhooks` | One → Pay. Not a browser job. |
| PATCH/PUT product, delete product | **No edit catalog.** NP-CAT-005 includes edit. |
| Any `/admin/commerce/*`, `/one/auth/*`, `/lhdn/*` | Hub. Absent. |

Pay host maps those unused routes (`Program.cs`): `MapWhoami`, `MapOrgReady`, `MapCheckouts`, `MapCatalog`, `MapPublicPay`, `MapGateways`, `MapWebhooks`, `MapPaymentQueries`, `MapOneWebhooks`. The SPA is a **partial** client of `/v1`, not a full one.

### 4.3 One client (create workspace only)

`src/lib/oneApi.ts`:

```ts
const oneApi =
  import.meta.env.VITE_ONE_API_URL ?? 'http://localhost:8080/api/v1'

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
  if (!response.ok) {
    throw new Error(`create tenant ${response.status}`)
  }
  return (await response.json()) as { id: string; slug: string; name: string }
}
```

This is 013/04 §5’s recommendation: **do not** make Pay a BFF that re-exports `/tenants`. Create = One. Tenant id **is** Pay `org_id`. No `POST /platform/tenants`. No Pay `organizations` table (IsolationTests still ban `ToTable("organizations")` / `"users"` / `"members"`).

Direct One calls the SPA does **not** make: `GET /me`, `GET /tenants`, `PATCH /tenants/{id}`, invites, roster, `authz/check`, `lzr_sk_` mint. Invite-accept is still a hole (NP-ONE-011/012/022 as a **product** path — member can *see* a workspace they already belong to, but this origin cannot invite them).

One CORS: if `App:CorsOrigins` omits `http://localhost:5178`, create-workspace fails in the browser even though Pay whoami works (Pay→One is server-to-server, not CORS). README M25 says this. Live One config is out of this repo.

### 4.4 Pay CORS vs this origin

`Program.cs`:

```csharp
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
        p.WithOrigins(
                "http://localhost:5178",
                "http://127.0.0.1:5178",
                "http://localhost:5179",
                "http://127.0.0.1:5179")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
```

`CorsTests`:

- Origin `http://localhost:5178` on `GET /health` → `Access-Control-Allow-Origin` contains 5178.
- Origin `http://localhost:5179` allowed (checkout).
- Origin `http://localhost:3003` (ops) → **no** ACAO header.
- Origin `http://localhost:3004` (portal) → **no** ACAO header.

There is still no test that `OPTIONS /v1/whoami` from 5178 with `Access-Control-Request-Headers: authorization` succeeds. `AllowAnyHeader` should cover it. 013/04 asked for that test “when whoami is called from the browser.” Whoami is now called from the browser. The test was not added.

---

## 5. Role chrome (VIEWER honesty)

`src/lib/roles.ts`:

```ts
/** One tenant roles. Pay: owner/admin write money; member is read-only. */
export function canWriteMoney(role: string | undefined | null): boolean {
  return role === 'owner' || role === 'admin'
}
```

No `viewer`. No `ADMIN`. No `SUPER_ADMIN`. No `MEMBER` uppercase Hub vocabulary.

`WorkspacePage` sets `write = canWriteMoney(tenant?.role)` from **whoami `tenants[].role` for the path org**, not from a Zitadel claim, not from `is_platform_admin`.

If `write`:

- Stripe keys heading + input + Save key
- Product + pay link heading + two inputs + Create pay link

Else:

```tsx
<p>Member can see payments. Cannot paste keys or create charges.</p>
```

Copy also says “VIEWER-class / member cannot” next to the key input — only rendered when `write` is true, so a member never sees that sentence; they see the other one. Slightly confused copy, but it does not invent a Viewer option.

`is_platform_admin` is typed on `Whoami` and **never read** in the UI. 013/04: do not use it as “can paste keys.” Live code complies.

**Chrome is not authorization.** Pay:

```csharp
// MemberGate.RequireWriterAsync
var denied = await RequireMemberAsync(...); // authz/check relation=member on tenant/{orgId}
var who = await one.GetWhoamiAsync(...);
var role = who.Value.Tenants.FirstOrDefault(t => t.Id == orgId)?.Role;
if (role is not ("owner" or "admin"))
    return PayErrors.Status(403, "Forbidden", "Writer role required");
```

`RequireMemberAsync` is `POST tenants/{orgId}/authz/check` with `{ relation: "member", object: { type: "tenant", id: orgId } }`. Writer is **not** `authz/check admin`. G12/G14 checklists said “`authz/check` **admin** (owner has admin).” Live host uses whoami role string after a member check. The SPA chrome matches the **live** host (owner/admin), not the checklist’s FGA relation. One still has no `viewer`. Do not mark NP-ONE-021 done from `/ready` `check(member)` — README of the Pay host still says that dummy is “has the tenant,” not “cannot charge.”

Checkout `POST /v1/checkouts` uses **`RequireMemberAsync` only**. A `member` who POSTs a checkout from curl gets 201 if they are a member. The SPA hides the button. NP-ONE-021 “cannot charge” is therefore **chrome-only** on the pay-link path. Product POST and gateway PUT are API-enforced writer.

HomePage shows `role` and `status` next to each tenant. A member can still **open** `/o/:id` (NP-ONE-022). Workspace then hides write forms.

---

## 6. Workspace money UI (the clickable product)

`WorkspacePage.tsx` is the entire money surface. Quote the types and the three verbs:

```ts
type Product = { id: string; name: string; prices?: { amount: number; currency: string; interval: string }[] }
type Payment = { id: string; amount: number; currency: string; status: string; checkout_id: string }
type Receipt = { id: string; number: string; title: string; checkout_id: string }
```

### 6.1 What the human sees

1. Kicker “Lazuar Pay”. H1 = tenant name or raw org id.
2. “Role `{role}`. Path org id is authorization SoT.”
3. `{error && <p role="alert">{error}</p>}` — whoami failures, “Not a member of this org”, `keys {status}`, `product {status}`, `checkout {status}`.
4. **If writer:** Stripe keys (placeholder `sk_test_…`, **not** `type="password"`), Save key; product name (default `Dogfood`) + amount (default `10`) + “MYR” + Create pay link; optional buyer URL.
5. **If member:** one sentence, no inputs.
6. Products `<ul>` of `p.name` only (prices ignored even though the type has them).
7. Payments `<ul>` of `{amount} {currency} {status}` — no payer, no gateway id, no receipt number, no checkout link.
8. Receipts `<ul>` of `{number} — {title}` — not clickable; no GET by id.
9. Footer: `Pay API {payApi}` and `Link to="/"`.

Empty lists render an empty `<ul>`. There is no “no products yet” / “no payments yet” copy. Failed list fetches (`!ok`) are **swallowed**: `refresh` only `setProducts` when `plist.ok`. A 403/500 list looks like an empty org.

Save key success: `setError(null)` only. No “saved · last4 ···x”. The secret stays in React state as **plaintext** in a visible text input (ops used `type="password"` and zeroed fields after GET — see §10).

Create pay link success: `setPayUrl('http://localhost:5179/c/' + public_token)` and `refresh`. The product row and the checkout are **not** linked: `CreateCheckoutRequest` has no `product_id`; `CheckoutEndpoints.Create` never writes `CheckoutRow.ProductId`. Ada can create a product named Dogfood at RM10 and a checkout at RM10 that is a sibling, not a child.

No `Idempotency-Key` on the SPA checkout POST. Double-click can mint two sessions. Host supports the header (`CheckoutTests.Create_idempotent_on_key`).

Amount is `Number(amount)` from a string input. `Number('')` is `0` → Pay 400 “amount must be greater than 0”. `Number('10rm')` is `NaN`. Error is `product {status}` with no `detail` from `PayErrors` JSON `{ status, title, detail }`.

Currency is hardcoded `'MYR'` in both bodies. Catalog host rejects non-MYR with 400 “Bar B currency is MYR”. Checkout host uppercases whatever is sent; SPA always sends MYR. Interval is omitted → product price `one_off`. There is no monthly/yearly toggle (NP-CAT-002 is “monthly and/or yearly”; Bar B CAT11 allowed `one_off`).

Buyer copy: “Buyer (no One account):” + `<a href={payUrl}>`. That is NP-CHK-007 honesty on the **merchant** side of the sentence. The page itself is paper 03.

### 6.2 Gateway paste vs host

Host `GatewayEndpoints.cs`:

- `PUT /v1/orgs/{orgId}/gateway` — `RequireWriterAsync`; provider must be `stripe`; secret required; AES-GCM via `SecretBox`; returns `{ org_id, provider, last4, capability: "hosted_link" }`.
- `GET /v1/orgs/{orgId}/gateway` — `RequireMemberAsync`; no row → `{ configured: false }`; else `{ last4, configured: true, capability: "hosted_link" }`. **Never the secret.**

SPA PUT body `{ provider: 'stripe', secret: sk }` matches `PutGatewayRequest`. SPA **ignores** the JSON response (including `last4`). SPA **never GET**s. After reload, the input is empty (React state), and there is no “configured · last4 ummy” chrome. A member cannot see that a key exists either.

Non-stripe provider from a modified client: host 400 “Bar B first rail is stripe”. The UI has no CHIP/Billplz/Razorpay/Xendit selector. That is the correct Bar B refuse of ops’ five-tab vault.

`SecretBox` wrap key is `Pay:WrapKey` (32-byte base64) or a **dev hash of a literal** `"lazuar-pay-dev-wrap-key"` if unset. That is a **Pay process** concern. Vite does not see it.

Webhook signing secret is **not** on this form. Host webhook verify uses `Pay:StripeWebhookSecret` (see `PayApiFactory.StripeWebhookSecret`), not the org’s pasted `sk_test_`. Ops Stripe tab had a `whsec_` field. New SPA does not. Ada cannot paste a per-org webhook secret here.

### 6.3 Catalog vs host

Host `CatalogEndpoints.cs`:

- `POST` writer; name required; currency default MYR, reject others; amount > 0; creates `ProductRow` + `PriceRow`; 201 `{ id, org_id, name, price_id, amount, currency, interval }`.
- `GET` member; array of `{ id, org_id, name, prices: [{ Id, Amount, Currency, Interval }] }`.

JSON naming: request is snake_case (`HttpJsonOptions.PropertyNamingPolicy = SnakeCaseLower`). List payload uses **anonymous** `prices` with **Pascal `Id`** on the nested anonymous type (`x.Id` in the select). System.Text.Json will snake `id` if the policy applies to those properties — `Id` becomes `id`. The SPA type uses `id` on the product and never reads `prices`.

SPA create does not send `description` or `interval`. SPA list does not show amount. **Edit does not exist** (no PATCH). NP-CAT-005 is “list / create / **edit**.” Create+list is a dogfood slice; edit is missing.

Hermetic host tests (`CatalogTests.cs`) are **two cases**: owner 201, member 403. CAT15’s 401 / other-org 403 / member GET list / health-skips-One were ticked in the checklist; they are **not** in that file. This paper does not flip CAT15. It records the disagreement because the SPA’s list/create depends on those routes.

### 6.4 Payments and receipts vs host

Host `PaymentQueryEndpoints.cs`:

```csharp
app.MapGet("/v1/orgs/{orgId}/payments", List);
app.MapGet("/v1/orgs/{orgId}/receipts", ListReceipts);
app.MapGet("/v1/orgs/{orgId}/receipts/{id}", Receipt);
```

List payments (member): charges `{ id, org_id, checkout_id, amount, currency, status }`. **No** payer email, **no** receipt number, **no** provider ref — F19.2 asked for those. Live JSON is thinner. The SPA type matches the thin JSON.

List receipts (member): documents `{ id, org_id, number: d.Number ?? "PENDING", title, checkout_id }`. Fulfillment writes `Title = "Official Receipt"` and `Number = $"RCPT-{year}-{seq.LastN:00000}"`. Missing number is the string `PENDING`, never a UUID as the document number. SPA shows `number — title`. It does not say “Tax Invoice”. It does not print VALID. That honesty is **present as data**, not as a warning banner.

GET by id: 404 `{ title: "Not Found", detail: "Receipt not found" }` or the same JSON as a list row. SPA never calls it. “Open receipt” in F21 is therefore **the list row**, not a detail panel. JSON-only was allowed (“JSON view is enough”). A click that 404s is still missing.

There is **no** `PaymentQueryTests.cs`. WebhookTests asserts a document `Number` starts with `RCPT-` after a signed Stripe event, via the DB, not via the merchant list route. O12’s “two-token hermetic” for payments GET is not a dedicated file.

`pay-spec/main.tsp` on this SHA has catalog products and checkouts and whoami. It does **not** declare gateway, payments, or receipts. The SPA is a client of the **host**, not of a generated types package. Q13 “pay-spec not Hub gen” still holds; the spec is **behind** the host for money reads.

### 6.5 Home and create-workspace (not money, but the door)

`HomePage`:

- No token → `signinRedirect()` (the opaque-token loop).
- 401 whoami → `signinRedirect()`.
- Other whoami error → “Whoami failed” + `role="alert"`.
- Loading → “Loading workspaces…”.
- `tenants.length === 0` → “No workspaces yet. Create one in One (not a Pay org table).”
- Else buttons labelled `name ?? slug ?? id`, plus `role` and `status`.
- Always a Create workspace link + Sign out.

Empty membership is a first-run screen, not a crash. 013/04 Screen B. Isolation: no INSERT into Pay orgs.

`CreateWorkspacePage`: form name + slug (`pattern="[a-z0-9-]{1,64}"`), POST One, `setOrgHint(tenant.id)`, `navigate(/o/{id})`. Copy: “Calls One `POST /tenants`. The tenant id becomes Pay `org_id`. No Pay organizations table.” On error: `create tenant {status}` or `create failed`. **Does not** re-GET whoami on Home first (M19.2 asked to refresh whoami then pick). It jumps to the workspace; WorkspacePage whoami then must include the new tenant. If One’s create is eventually consistent, Ada sees “Not a member of this org.”

Busy flag disables submit. Token missing → signinRedirect.

---

## 7. Empty / error / loading states (inventory)

| Surface | Loading | Empty | Error |
|---------|---------|-------|-------|
| `RequireAuth` | “Checking session…” | n/a | `auth.error.message` + Retry login |
| `LoginPage` | “Loading…” | n/a | Missing `VITE_ZITADEL_CLIENT_ID` alert; button disabled |
| `CallbackPage` | “Completing sign-in…” | n/a | “Login failed” + Try again |
| `HomePage` | “Loading workspaces…” | “No workspaces yet” + create link | “Whoami failed”; 401 redirects |
| `CreateWorkspacePage` | button disabled while `busy` | n/a | `role="alert"` status text |
| `WorkspacePage` whoami | role shows `…` until tenant arrives | products/payments/receipts empty `<ul>` | “Not a member of this org”; `whoami failed`; `keys/product/checkout {status}` |
| `WorkspacePage` lists | no spinner; lists stay `[]` until ok | blank ul, no copy | **silent** if GET not ok |
| Save key | none | n/a | `keys {status}` only |
| Create pay link | none | n/a | status code only; product may already exist if checkout then fails |

No toast library. No skeleton. `role="alert"` is used in several places; list failures are the dishonest gap.

---

## 8. Tests that actually sit on this origin

### 8.1 Merchant vitest

Two files, node environment:

**`bearerToken.test.ts`** — picker policy (quoted in §3.2). This is the M12/M21 lock.

**`locks.test.ts`** — walks `src/**/*.ts,tsx,css` excluding `*.test.*`:

- blob must not match `type=["']password["']`
- must not contain `/one/auth/login`
- must not contain `lazuar_auth`
- `package.json` must not contain `@repo/api-types-ts`
- `package.json` must not contain `lazuar-ops`

There is **no** test that `WorkspacePage` hides Save key for `member`. There is **no** test that PUT uses `provider: 'stripe'`. There is **no** test that checkout URLs point at 5179 not 5178. CI (`ci.yml` job `pay`) runs:

```yaml
- name: Test focused Pay host
  run: dotnet test apps/lazuar-pay/Lazuar.Pay.slnx …
- name: Build merchant and checkout
  run: |
    pnpm --filter lazuar-pay-merchant build
    pnpm --filter lazuar-pay-checkout build
```

`pnpm --filter lazuar-pay-merchant test` is **not** in CI. `tsc -b && vite build` will catch type errors. It will not catch a picker regression unless someone runs vitest locally (`README` documents `pnpm --filter lazuar-pay-merchant test`).

Q12 ticked “CI build merchant.” That is true. Merchant **unit** tests are a local script.

### 8.2 IsolationTests Vite ban (must quote)

`apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`:

```csharp
[Test]
public void Vite_apps_do_not_use_hub_types()
{
    var repo = FindPayRoot();
    while (repo is not null && !Directory.Exists(Path.Combine(repo, "apps", "lazuar-pay-merchant")))
    {
        repo = Directory.GetParent(repo)?.FullName;
    }

    Assert.That(repo, Is.Not.Null);
    foreach (var name in new[] { "lazuar-pay-merchant", "lazuar-pay-checkout" })
    {
        var pkg = Path.Combine(repo, "apps", name, "package.json");
        Assert.That(File.Exists(pkg), Is.True, pkg);
        var text = File.ReadAllText(pkg);
        Assert.That(text, Does.Not.Contain("@repo/api-types-ts"), pkg);
    }
}
```

Live `lazuar-pay-merchant/package.json` does not contain `@repo/api-types-ts`. The test would fail a Hub-types smuggle.

Q10.2 also asked to fail if Vite `package.json` contains `MediatR` or `apps/lazuar-api`. **Live IsolationTests do not assert those two strings** on the Vite packages. Host csproj bans still exist separately. Checklist Q10 is over-ticked relative to the Vite scan. The Hub-types ban that 014 standing law named **is** in code.

---

## 9. Steal-vs-refuse from ops `PaymentSettingsPage` (judgment only)

Opened `apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx` as a **negative and a judgment source**. Do not copy the file. Do not import it.

### 9.1 What ops actually is (so we know what we refused)

- Hub client: `import { client } from "../../../lib/api-client"` → `openapi-fetch` + **`@repo/api-types-ts`** + `credentials: "include"` + **`X-Tenant-Id`**.
- `GET /admin/commerce/payment-config` and `PUT /admin/commerce/payment-config`.
- Five-gateway union: `"STRIPE" | "BILLPLZ" | "RAZORPAY" | "CHIP" | "XENDIT"`.
- Role gate: `canSaveVault = role === "ADMIN" || role === "SUPER_ADMIN"` (Hub strings).
- `applyConfig` **zeros** `apiKey` / `webhookSecret` / `secretKey` after GET (“Never populate password fields with stored secrets”).
- Hints: `api_key_hint`, `has_api_key`, `secret_key_hint`, `webhook_secret_hint`.
- Billplz X-Signature **exactly 128 chars**; CHIP Brand ID; Stripe `sk_live_...` placeholder + `whsec_`; Razorpay `KeyId:KeySecret`; Xendit `xnd_…` + callback token.
- Wrap-rails amber banners for Billplz (“cannot vault”) and Xendit (“Hosted invoice only”).
- Save button **omitted** from the DOM if `!canSaveVault`.
- Toast “credentials saved securely.”

### 9.2 Steal (rules, not files)

| Rule | Ops | Live `:5178` |
|------|-----|----------------|
| Never echo secrets from GET | `applyConfig` zeros password fields | SPA never GETs, so it cannot echo. Weaker: it also cannot show last4. |
| Hints not keys | `api_key_hint` | Host GET returns `last4`. SPA unused. |
| Hide save unless admin-class | Hub ADMIN/SUPER_ADMIN | One `owner`/`admin` via `canWriteMoney` |
| Chrome hide is not authz | Hub still 403s VIEWER on the API | Pay `RequireWriterAsync` 403 |
| One rail on day one | — | Stripe only. Correct refuse of the five-tab `<select>`. |
| Receipt ≠ tax invoice | `salesDocumentType.ts` (not this file) | Host title “Official Receipt”; SPA prints `title` |

### 9.3 Refuse (do not port)

| Ops thing | Why refuse |
|-----------|------------|
| Five-gateway tab as day-one UI | NP-GW-003; Bar B stripe; Razorpay/Xendit later (014 papers 06/07) |
| `PUT /admin/commerce/payment-config` | Wrong host, Hub DTO |
| Hub role strings + VIEWER | One cannot store VIEWER |
| `type="password"` copied as a **Hub form** with three secrets per rail | New form may use password **type** as UX; must not grow CHIP Brand ID + Billplz 128 + Xendit callback on day one |
| Toast “saved securely” as a substitute for encryption | Encryption is `SecretBox` on 8081 |
| “Lazuar will autonomously fetch your RSA Public Key and configure your webhook endpoints” (CHIP copy) | That is Hub CHIP registrar. New host does not |
| Soft-disable `is_active` + test/live environment enum as Hub config | Host row is `org_id+provider` ciphertext + last4. No environment column in `GatewayCredentialRow` |
| Webhook signing secret field on Stripe | Host uses process `Pay:StripeWebhookSecret`, not BYOK `whsec_` per org, on this SHA |

### 9.4 Where live `:5178` is **worse** than ops judgment it should have stolen

1. Secret input is **visible text**, not `type="password"`.
2. No GET last4 / `configured` after save or on load — Ada cannot tell if the vault has a key without pasting again.
3. No “leave blank to keep existing” rotate UX (ops). Rotate is “paste the whole `sk_` again.”
4. Success is silent.
5. No wrap-rails sentence at all (G15). Bar B `capability: "hosted_link"` is returned by PUT and ignored. The UI does not say “hosted Checkout; we will not silent-debit.” For Stripe hosted, that sentence is still worth having so CHIP/Billplz later cannot land as a green Auto-debit switch.

Steal those five as **rules** into a later WorkspacePage revision. Do not steal the five-tab JSX.

---

## 10. What is missing for pasting keys, listing receipts, creating products

The 014 prompt named three jobs. Live code has a **thin yes** for each and a **product no** for the 011 wording.

### 10.1 Pasting keys (NP-GW-009)

**Present:** owner/admin can PUT `{provider:'stripe', secret}` to `/v1/orgs/{orgId}/gateway`. Member chrome hides the form. Host encrypts. CORS allows 5178. Vite does not hold the wrap key.

**Missing / dishonest:**

| Gap | Evidence |
|-----|----------|
| No GET metadata | `GatewayEndpoints.Get` unused. G13 “Masked GET is the merchant chrome source” is host-true, SPA-false. |
| No last4 / configured | PUT response ignored. |
| Visible secret field | not `type="password"` |
| No rotate-without-retype | |
| No webhook secret / Brand ID | Correct for Bar B stripe-hosted; document it so nobody “just adds CHIP fields” from ops |
| Member cannot see that a key exists | GET is member-allowed on the host |
| Writer gate is whoami role, not `authz/check admin` | `MemberGate.RequireWriterAsync` |
| No dedicated GatewayTests for member 403 PUT | G14 ticked; grep of focused Pay tests finds PUT only as WebhookTests seed |
| 011 `NP-GW-009` still **todo** | Do not flip from this paper |

A merchant **can click Save key today** if they are owner/admin, Pay is up, One membership is real, and the secret is non-empty. That is dogfood paste, not a vault UI.

### 10.2 Listing receipts (NP-DOC-005 / F21 / NP-FUL-003 companion)

**Present:** `GET /v1/orgs/{orgId}/receipts` after whoami; render `number — title`; member can see; title comes out “Official Receipt”; number is `RCPT-…` or `PENDING`.

**Missing:**

| Gap | Evidence |
|-----|----------|
| No open-by-id | `GET …/receipts/{id}` unused |
| No click | `<li>` is not a `<Link>` |
| No PDF | 013/07 said JSON first; still JSON-only |
| Payments list does not show receipt number | Charge JSON has no `number`; SPA does not join lists |
| Silent empty on 403 | `refresh` |
| No `PaymentQueryTests` | host |
| Spec lag | `pay-spec` has no receipts |
| Subscribers list | NP-FUL-003 “payments + subscribers”. No UI. Host `SubscriptionRow` exists after `mo`/`yr` fulfill; SPA creates `one_off` only |
| 011 `NP-DOC-005` / `NP-FUL-003` still **todo** | |

A merchant **can see a receipt row today** if fulfillment has written a document for that org. They cannot “open” it beyond the list line.

### 10.3 Creating products (NP-CAT-005)

**Present:** writer POST `{name, amount, currency:'MYR'}`; GET list of names; member cannot POST (API 403 + chrome hide); no TIN/WhatsApp/LHDN fields; origin is 5178.

**Missing:**

| Gap | Evidence |
|-----|----------|
| No edit | NP-CAT-005 includes edit; no PATCH |
| No description field | host accepts `description` |
| Prices not shown | list JSON has `prices`; UI prints `name` |
| No monthly/yearly | always `one_off` default |
| Seats (NP-CAT-004) | absent |
| Create is glued to pay-link | cannot create a product without also POSTing a checkout (unless checkout then fails — then a product exists with no link) |
| Checkout not bound to product | `CreateCheckoutRequest` has no `product_id` |
| No “warn if no gateway pasted” | ops ProductsPage judgment 013/04 Screen C asked to steal |
| No QuickCopy of pay URL | raw `<a>` |
| Hardcoded `:5179` | no env |
| CatalogTests thin vs CAT15 | |
| 011 `NP-CAT-005` still **todo** | |

A merchant **can click Create pay link today** and get a product row named Dogfood plus a buyer URL. That is not “merchant ops list/create/edit products” as a catalog console.

### 10.4 Other dogfood holes that sit on this origin

| Hole | 011 / 013 | Live |
|------|-----------|------|
| Invite copy-link / accept | NP-ONE-011, 012, 022 as *invite* | No invite UI; member can open a workspace they already have |
| Tenant profile PATCH | NP-ONE-010 | No |
| `lzr_sk_` console | NP-ONE-014 | Correctly absent (One app) |
| SST registered tri-state | NP-MON-004; 013/07 Q | No org-settings UI. Checkout create inserts `OrgSettings { SstRegistered = false }` if missing — fail-closed `null` is avoided by that insert, which is a **host** choice, not merchant chrome |
| Refund | NP-MON-005 | No button |
| Health probe as DX | 013/04 only fetch | **Removed.** Unreachable Pay now looks like whoami error after login, not a card on the homepage |
| Playwright Ada | M26 not CI | README runbook only |
| `prompt=create` | 013 Screen A optional | LoginPage has no Create account |

---

## 11. 013 checklists M10–M27 — spot-check against **code** (not against ticked boxes)

013 `checklists/m10`–`m27` are all `[x]`. Live code vs those exits:

| ID | Claim | In merchant / Pay code? | Notes |
|----|-------|-------------------------|-------|
| **M10** | Register SPA via One POST `/apps` type spa | **Script yes.** App object **not in this repo.** | `register-spa.sh` is the tool. Exit “One app object exists” is runbook. |
| **M11** | `.env.example` Pay + Zitadel public | **Yes.** Also `VITE_ONE_API_URL`, post-logout. | Empty `CLIENT_ID`. No `VITE_API_URL`. No secret. |
| **M12** | Copy picker + tests + sync wire | **Yes.** | `bearerToken.ts` + test; render-time pick. |
| **M13** | `react-oidc-context` code+PKCE, sessionStorage, replaceState | **Yes.** | `oidcConfig.ts` / `main.tsx`. |
| **M14** | `/callback`, AuthProvider wraps router | **Yes.** | Exact `http://localhost:5178/callback`. |
| **M15** | Sign in = `signinRedirect`; password on `:5175`; title merchant | **Yes.** | No `window.location = :5175`. |
| **M16** | After user, GET `/v1/whoami` Bearer access; 401 → signin; don’t hammer | **Yes.** | Home once; workspace on org/token change. Health not polled. |
| **M17** | Render `tenants[]`; empty valid; no Pay org insert | **Yes.** | HomePage empty copy. Does not always show raw `id` if `name` exists (M17.1 listed id/name/slug/role/status). |
| **M18** | Path org id; header hint only; sessionStorage UX | **Yes.** | `/o/:orgId`; `X-Lazuar-Tenant-Id`; `ORG_HINT_KEY`. |
| **M19** | Create = One `POST /tenants` | **Yes.** | Does **not** refresh whoami on Home before navigate (M19.2). No `provision_apps`. |
| **M20** | No password form; grep lock | **Yes.** | `locks.test.ts`. |
| **M21** | Never id_token; tests | **Yes.** | |
| **M22** | sessionStorage; no `credentials: include` | **Yes.** | Comment in `payApi.ts`. |
| **M23** | No `@repo/api-types-ts` | **Yes.** | IsolationTests + locks. Hand-written whoami types. |
| **M24** | Hide writes unless owner/admin; no fake VIEWER | **Yes chrome.** | API writer on keys/products; checkout POST still member-gated. NP-ONE-021 still open in 011. |
| **M25** | Login allowlist + One CORS | **Documented in README.** | Cannot prove One-repo lists from this tree. Pay CORS 5178 / not 3003 is tested. |
| **M26** | README runbook Hub off, ports, Ada, fingerprint | **Yes.** | Not CI. |
| **M27** | Not ops; no LHDN/chat/WhatsApp; don’t retarget ops | **Yes.** | No import from `apps/lazuar-ops`. Nav is not Commerce/Invoicing. |

M-track chrome is **largely true in files**. The over-tick is M10/M25 **environment** (One must be configured) and M19.2/M24 charge-path (checkout member POST).

Catalog / rails / fulfillment checklists that claim **this SPA**:

| ID | Ticked claim | Live SPA |
|----|--------------|----------|
| CAT13 | Page can create (name) and list products | Create is bundled with checkout; list names only; no edit |
| CAT15 | Hermetic 201/401/403/list/health | CatalogTests is **two** tests |
| G12 | Merchant PUT keys on `/v1` | SPA does PUT stripe secret |
| G13 | GET metadata never secret | Host yes, **SPA no** |
| G14 | member 403 PUT | Host `RequireWriter`; **no focused test file** named for gateway |
| G15 | UI must not say auto-charge on hosted_link | UI says nothing about capability |
| F19 | List payments member-gated | Host JSON thinner than F19.2; SPA lists amount/currency/status |
| F20 | GET receipt by id | Host yes; **SPA no** |
| F21 | `:5178` list + open receipt | List yes; open = list line |
| O12 | Invited member sees payment + RCPT; cannot paste keys | Chrome yes if whoami role is member; two-token hermetic not a SPA test |
| Q10 | IsolationTests read both Vite package.json for Hub types | **Yes** for `@repo/api-types-ts`; not MediatR/apps/lazuar-api on Vite |
| Q12 | CI build merchant | **Yes** (`ci.yml` `pay` job). No merchant vitest |

---

## 12. Disagreement with `plans/013-prods/04-merchant-frontend.md`

013/04 is dated **21 August 2026**, SHA **`6f866ff0`**, branch `feat/012-connect-one`. It is historical. Live authority is `ee2db8e5` `main`. Named disagreements:

### 12.1 The health probe is gone

013/04 §1.2 answer 1: “`lazuar-pay-merchant` today is a Vite React 19 shell on 5178 `strictPort`. It `fetch`es Pay `GET /health` with no Authorization. OIDC is unwired.”

013/04 §2.4 quoted `App.tsx` as a `useEffect` `fetch(${payApi}/health)`.

**Live `App.tsx` is a router.** Grep of merchant `src` for `/health`: **no matches**. The `dist/assets/index-DuegEQIu.js` hash 013 recorded is a **stale local build** of that probe, not the source of truth.

### 12.2 OIDC is wired

013: “OIDC is unwired. That is P10.1 done and P10.2–P10.3 still open.”

**Live:** `oidc-client-ts`, `react-oidc-context`, `/callback`, `/login`, `pickApiBearerToken`, register script, `.env.example` Zitadel keys. P10.2-class work is in the tree. Whether Ada’s One tenant has `client_id` and login allowlist is still a laptop fact.

### 12.3 Whoami is called from the browser

013 §2.6 table: `GET /v1/whoami` “**not called**”. First product fetch “should grow is GET /v1/whoami”.

**Live:** HomePage and WorkspacePage both call it. M16 is code.

### 12.4 Catalog / keys / payments routes “do not exist yet”

013 Screen C: “Pay `/v1` catalog routes **do not exist yet** on `pay-spec`.” Screen D: “key-vault routes **do not exist yet**.”

**Live host** has `MapCatalog`, `MapGateways`, `MapPaymentQueries`. **Live SPA** calls them. **`pay-spec`** has catalog products only — spec is behind the host. 013’s “UI waits on those routes” is done for a thin subset.

### 12.5 Dependencies and file inventory

013 §2.1: dependencies only react + react-dom; no router; no oidc.

**Live:** three extra runtime deps from One’s stack, as 013 §4.4 instructed to copy.

013 §2.3: no `src/pages`, no `src/auth`, no `src/lib`.

**Live:** those directories exist and are the product.

### 12.6 Env table

013 §4.5: “Recommended merchant env (**later; not on this SHA**)”. Listed Zitadel + optional One URL.

**Live `.env.example` is that table.** 013’s “later” happened.

### 12.7 What 013 got **right** that is still true

- Origin 5178 `strictPort`; preview 4178; dual-pin.
- Do not retarget ops; Pay CORS denies 3003.
- `access_token` as Bearer; copy the picker; never `id_token`.
- sessionStorage; no `credentials: "include"` because localhost cookies are not port-scoped.
- Call One for tenants; Pay `/v1` for money; do not BFF-re-export `/tenants`.
- One has no VIEWER; do not ship a Viewer invite; chrome hide ≠ 403.
- Never `@repo/api-types-ts`.
- Six screens as the v1 job: login redirect, workspace pick/create, products, keys, payments, receipt. Live has **skeletons of all six on two routes** (`/` + `/o/:orgId`), not six pages.
- Refuse list (LHDN, chat, WhatsApp, Hub CRM, quotes-as-tax, credits, password pages, Sidebar catalog) — still absent from this app.
- Do not flip 011 cells from an analysis paper.

### 12.8 What 013 recommended that live **half-did**

| 013 recommendation | Live |
|--------------------|------|
| Screen C steal “warn if no gateway” | Not stolen |
| Screen D steal GET-never-secrets + hints | Host GET exists; SPA does not use it; input is not password |
| Screen E steal gateway ids / fees honesty | Payments list is amount/currency/status |
| Screen F open receipt; number not UUID; not Tax Invoice | List line; number from host; title Official Receipt |
| `GET /v1/orgs/{orgId}/ready` optional badge | Unused |
| `@repo/pay-types-ts` when whoami is real | Still hand-written types; spec missing money routes |
| OPTIONS whoami CORS test | Not added |
| Invite in-app or deep-link `lazuar-app` | Neither |

Treat 013/04 as the **design paper that shipped**. Treat this file as the **as-built**. Do not “fix” live by restoring the health probe.

---

## 13. 011 merchant-facing IDs vs live (status column stays — do not flip)

011/11 counts on this SHA still say S0 5 done / 17 todo, S1 5 done / 37 todo. Merchant-facing rows:

| ID | 011 status | What live `:5178` actually does |
|----|------------|----------------------------------|
| NP-ONE-001 | todo | Register **script** exists. App object not in repo. |
| NP-ONE-002 | todo | PKCE wired in Vite. `client_id` is local `.env`. |
| NP-ONE-003 | **done** | Host forwards Bearer; SPA picker never sends `id_token`. |
| NP-ONE-004 | todo | README documents allowlist; One repo not verified here. |
| NP-ONE-005 | todo | Login copy + flow use `:5175`; homepage is `:5178`. Code matches; tracker not flipped. |
| NP-ONE-006 | **done** (host whoami) | SPA consumes it. |
| NP-ONE-007 | **done** (path SoT) | `/o/:orgId`; header hint. |
| NP-ONE-008 | **done** | SPA uses whoami `role`, not Zitadel claims. |
| NP-ONE-009 | todo | SPA `POST` One `/tenants`. Tracker still todo. |
| NP-ONE-010 | todo | No PATCH tenant UI. |
| NP-ONE-011…013 | todo | No invite/roster UI. |
| NP-ONE-014 | todo | No key console (correct). |
| NP-ONE-015 | **done** (dummy ready) | SPA does not call `/ready`. |
| NP-ONE-016 | todo | No batch-check chrome. |
| NP-ONE-020 | todo | SPA holds public `client_id` only. Pay server wrap key / webhook HMAC are not Vite. |
| NP-ONE-021 | todo | Keys/products writer-gated; checkout create is member on API. No viewer role. |
| NP-ONE-022 | todo | Member can open workspace and see lists. Invite path missing. |
| NP-CAT-001…004 | todo | Name+MYR+amount+one_off price on create. No seats, no mo/yr UI. |
| NP-CAT-005 | todo | Thin create+list; no edit. |
| NP-GW-009 | todo | Thin Stripe paste. |
| NP-FUL-003 | todo | Payments list; no subscribers. |
| NP-DOC-005 | todo | Receipt list; no open-by-id. |
| NP-API-004 | todo | SPA **is** a client of `/v1` for the calls in §4.2. Tracker still todo — flipping is a human job after dogfood, not this file. |
| NP-CHK-006 | todo | SPA prints a `:5179/c/{token}` link. Shareable in the thin sense. |

The tracker is **behind** the tree for several S0 login/workspace rows and **honestly still todo** for catalog/keys/receipts as **product** jobs. 014 standing law: do not treat 013 checklist `[x]` as 011 `done`.

---

## 14. Contrast: this origin vs `lazuar-ops` (do not retarget)

| | `lazuar-pay-merchant` live | `lazuar-ops` |
|--|----------------------------|--------------|
| Port | **5178** `strictPort` | **3003** `strictPort` |
| API env | `VITE_PAY_API_URL=http://localhost:8081` | `VITE_API_URL=http://localhost:8080/api/v1` (**Hub**) |
| Types | hand-written whoami | `@repo/api-types-ts` |
| Session | OIDC sessionStorage + Bearer | Cookie `lazuar_auth` via `credentials: "include"` |
| Login | Sign in → `:5175` | Password form `POST /one/auth/login` |
| Tenant header | `X-Lazuar-Tenant-Id` hint | `X-Tenant-Id` from localStorage, Hub-authorizing |
| Money | WorkspacePage → `/v1/orgs/...` | PaymentSettingsPage → `/admin/commerce/payment-config` |
| P60 | this origin | Keep Hub. Pay CORS denies 3003. |

Ops `api-client.ts` remains the file P60 exists to quarantine. Merchant `payApi.ts` is the replacement shape: `fetch` + Bearer + optional One header, two hosts, no generated Hub `paths`.

---

## 15. What a merchant can actually click today (walkthrough)

Assume One login allowlist includes `http://localhost:5178/callback`, One CORS includes 5178, `.env` has a real `VITE_ZITADEL_CLIENT_ID`, Pay `:8081` and One `:8080` are up, Hub compose is off.

1. Open `http://localhost:5178` → redirected to `/login`.
2. If `CLIENT_ID` missing: disabled Sign in and an alert naming `scripts/register-spa.sh`. Stop.
3. Click **Sign in** → Zitadel → **One login :5175** password form (not this app) → back to `/callback` → `/`.
4. **Workspaces**: email or user id; list of tenant buttons; **Create workspace**; **Sign out**.
5. Empty: click **Create one in One** / **Create workspace** → name + slug → One POST → `/o/{new-id}`.
6. Click a tenant → `/o/{orgId}`.
7. **Owner/admin:** paste `sk_test_…` → **Save key** (no confirmation). Type a product name and amount → **Create pay link** → blue link to `http://localhost:5179/c/{64 hex chars}`. Open that as a **buyer** (paper 03). Come back; **Products** shows the name; **Payments** / **Receipts** fill only after a paid webhook fulfillment.
8. **Member:** no key/product forms; three lists; “Member can see payments. Cannot paste keys or create charges.”
9. **All workspaces** returns home. Sign out is not on the workspace page.

That is the whole clickable product. There is no settings nav, no team page, no tax invoice, no chat.

---

## 16. Anti-goals still holding on this origin

| # | Anti-goal | Live |
|---|-----------|------|
| 1 | Retarget ops `VITE_API_URL` at 8081 | Not done. CorsTests still deny 3003. |
| 2 | Password form on `:5178` | `locks.test.ts` would fail. |
| 3 | Treat `:5175` as homepage | Copy forbids it. |
| 4 | Ship merchants to `:5173` / `:3005` | Login copy names them as not-this. |
| 5 | `id_token` as Bearer | Picker tests. |
| 6 | `@repo/api-types-ts` | IsolationTests + locks. |
| 7 | Pay BFF `/tenants` | Create hits One. |
| 8 | `credentials: "include"` | Absent. |
| 9 | Authorize with tenant header alone | Path + whoami membership. |
| 10 | Fake VIEWER | `canWriteMoney` only owner/admin. |
| 11 | LHDN / WhatsApp / chat / Hub credits | Absent. |
| 12 | Secrets in `VITE_*` besides public client_id | `.env.example` + grep. |
| 13 | OIDC on checkout `:5179` | Not this app; Workspace only **links** to it. |
| 14 | Merge merchant + checkout | Separate packages, dual-pin comments. |
| 15 | Silent Vite port hop | `strictPort`. |
| 16 | Five gateway adapters in the paste UI | Stripe only. |

---

## 17. Evidence index (paths opened)

### Pay repo — `main` `ee2db8e5`

- `apps/lazuar-pay-merchant/package.json`, `vite.config.ts`, `vitest.config.ts`, `index.html`, `README.md`, `.env.example`, `scripts/register-spa.sh`, `tsconfig*.json`
- `apps/lazuar-pay-merchant/src/main.tsx`, `App.tsx`, `App.css`, `index.css`, `locks.test.ts`
- `apps/lazuar-pay-merchant/src/auth/{oidcConfig.ts,bearerToken.ts,bearerToken.test.ts,RequireAuth.tsx}`
- `apps/lazuar-pay-merchant/src/lib/{payApi.ts,oneApi.ts,roles.ts,sessionKeys.ts}`
- `apps/lazuar-pay-merchant/src/pages/{LoginPage,CallbackPage,HomePage,CreateWorkspacePage,WorkspacePage}.tsx`
- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs`, `StripeHosted.cs`, `WebhookEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/PaymentQueryEndpoints.cs`, `Fulfillment.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/{CheckoutEndpoints,CreateCheckoutRequest,CheckoutSession,CheckoutStore}.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/One/{WhoamiEndpoints,WhoamiResponse,MemberGate,Bearer,OneClient,OneMeMapper,OneMeResponse,OrgReadyEndpoints,PayErrors,OneAuthz}.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs`, `Data/Rows.cs`, `PublicPay/PublicPayEndpoints.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/{IsolationTests,CorsTests,CatalogTests,CheckoutTests,WebhookTests,WhoamiTests,PayApiFactory}.cs`
- `packages/pay-spec/main.tsp`
- `Taskfile.yml`, `.github/workflows/ci.yml`, `pnpm-workspace.yaml`, `.gitignore`
- `apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx`, `apps/lazuar-ops/src/lib/api-client.ts` (judgment / refuse)
- `plans/011-new-lazuar-pay/11-checklist.md`
- `plans/013-prods/04-merchant-frontend.md`
- `plans/013-prods/checklists/m10-spa-register.md` … `m27-not-ops.md`, `cat10`–`cat13`, `cat15`, `g11`–`g15`, `f19`–`f21`, `o12`, `q10`–`q12`, `parked-p60-old-frontends.md`
- `plans/014-evals/README.md`

---

## 18. What “done” would mean for the next implementer (analysis bar)

This paper is done if a later implementer can, without re-deriving the origin:

1. Treat **`apps/lazuar-pay-merchant` on 5178** as the staff shell that already has OIDC, whoami, workspace create-on-One, and a thin money page — not as the 013 health probe.
2. Keep secrets off Vite. Keep `access_token` as Bearer. Keep ops on Hub.
3. Grow **GET gateway metadata**, **password-type key field**, **list error states**, **receipt-by-id**, **product list prices / edit**, **product_id on checkout**, **Idempotency-Key**, **checkout origin env**, without importing `lazuar-ops`.
4. Enforce writer on **checkout create** if product wants “member cannot charge”; today only chrome hides that button.
5. Add merchant vitest (or a host PaymentQuery/Gateway writer test) that CI actually runs — IsolationTests already ban Hub types.
6. Leave 011 cells to a human after a live Ada walkthrough. 013 `[x]` is not 011 `done`.

Implementation is a later program, not this file.

*End of 02 — Merchant Vite (`lazuar-pay-merchant` :5178). Evaluation only. 24 August 2026. Pay `main` `ee2db8e5`.*
