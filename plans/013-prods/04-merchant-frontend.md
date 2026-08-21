# 04 — Production merchant UI (`lazuar-pay-merchant` :5178), not `lazuar-ops`

**Date:** 21 August 2026  
**Type:** Uncondensed analysis. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) cells to `done`. **Not** a design of the hosted checkout page (paper [05](./05-checkout-frontend.md)). **Not** a retarget of `lazuar-ops` at 8081.  
**Slice:** `apps/lazuar-pay-merchant` only, plus what that origin must call (Pay `/v1` and One OIDC).  
**Parent program:** [`plans/013-prods`](./) — production-ready new Pay, then replace the old tree.  
**Binding siblings:** [011 `01-product.md`](../011-new-lazuar-pay/01-product.md), [011 `02-one-integration.md`](../011-new-lazuar-pay/02-one-integration.md), [011 `11-checklist.md`](../011-new-lazuar-pay/11-checklist.md); [012 `02-one-authn-tokens.md`](../012-one-to-pay/02-one-authn-tokens.md), [012 `p10-spa-oidc.md`](../012-one-to-pay/checklists/p10-spa-oidc.md), [012 `p60-old-frontends.md`](../012-one-to-pay/checklists/p60-old-frontends.md).

---

## 0. What this paper is for

New Pay already has a **browser origin**. It is `apps/lazuar-pay-merchant` on **http://localhost:5178** (`strictPort`). It is a health probe. It has no OIDC, no router, no whoami call, no password form, and no `@repo/api-types-ts`. That is the correct starting point. This paper says how that origin becomes the **merchant ops client of Pay `/v1`** (NP-API-004) without becoming a clone of `lazuar-ops` (`:3003`).

The product sentence this UI must make true is [011 `01-product.md`](../011-new-lazuar-pay/01-product.md):

> A merchant signs in through **One**, opens Pay, pastes CHIP or Stripe keys, a **buyer pays on the hosted page without a One account**, Pay shows one `RCPT-` and a balanced journal, a webhook retry no-ops, a One-invited MEMBER can see ops and a VIEWER cannot charge.

The **buyer** half of that sentence is paper 05 (`lazuar-pay-checkout` `:5179`). This paper is the **merchant** half: sign-in, workspace, catalog, keys, payments list, receipt. If a screen is not on that path, it is not v1 merchant UI.

Locked (do not relitigate):

| Lock | Meaning |
|------|---------|
| New Vite origin **`:5178`** `strictPort` | Not ops `:3003`. Not admin `:5173`. Not login `:5175` as homepage. Not checkout `:5179`. |
| OIDC **code + PKCE** | `access_token` as Bearer. Copy `pickApiBearerToken` from One `lazuar-app`. Never `id_token` as Bearer. No password form in Pay. |
| Register the SPA via One **`POST /tenants/{id}/apps`** (or a One **seed** script) **+ login `REDIRECT_ALLOWLIST`** | Not Console-only. |
| Merchant ops is a **client of Pay `/v1`** (NP-API-004) | Workspace = One tenant id from whoami. No back-door table reads. |
| Steal **judgment** from `lazuar-ops` | Not the route catalog, not Hub cookie `lazuar_auth`, not `@repo/api-types-ts`. |
| VIEWER cannot charge / keys / refund | Enforce using **One role in Pay**. UI hides or fails honestly. One has no `VIEWER` membership role today ([012 `07`](../012-one-to-pay/07-authz-roles.md) §10). |

---

## 1. Method / SHAs

Recorded at write time. Re-open files on a later SHA before treating a line as still true.

| Repo | Path | Branch | Full SHA | Short | Tip |
|------|------|--------|----------|-------|-----|
| **lazuar-pay** | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `feat/012-connect-one` | `6f866ff0489a4de77d2fc1b1bbcfa87fbe72b80f` | `6f866ff0` | `feat(pay): scaffold merchant and checkout Vite apps` (2026-08-21 15:15:51 +0800) |
| **lazuar-one** | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `main` | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` | `0f79fe4` | `WIP: Thu Aug 20 21:24:22 +08 2026` (2026-08-20 21:24:22 +0800) |

Pay working tree at analysis: `plans/013-prods/` untracked (this folder). One working tree: clean at the SHA above.

**Honesty lock (inherited, not re-proven here):** One staging proof is **NOT PASSED**. Packages `@lazuar/one-client` / `one-react` / `one-cli` are unpublished workspace packages. There is no public hosted SKU. Pay merchant may import the One workspace client later; this paper does not wait on npm (`NP-XX-021`). Source: [011 `02-one-integration.md`](../011-new-lazuar-pay/02-one-integration.md) header; One `plans/017-evals/08-dogfood-then-serve.md` header.

**012 papers this write treats as evidence, not as live host state:** [012 `05-local-topology.md`](../012-one-to-pay/05-local-topology.md) said focused Pay had **no CORS** (SHA `6ca8f19f`). That is **stale**. On `6f866ff0`, `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` adds a default CORS policy for `:5178` and `:5179` (localhost and `127.0.0.1`), and `CorsTests.cs` asserts ops `:3003` is **not** allowed. [012 `04-pay-spec-contract.md`](../012-one-to-pay/04-pay-spec-contract.md) described `pay-spec` as health-only; on this SHA `packages/pay-spec/main.tsp` already has `GET /v1/whoami`, `GET /v1/orgs/{orgId}/ready`, and fixture checkouts. This paper uses the **live tree**, and cites 012 for the decisions that did not move (Bearer, no Hub types, no tenant-route copy, VIEWER honesty).

### 1.1 How this paper was built

Opened, in this repo:

- `apps/lazuar-pay-merchant/**` (every source file; there are few).
- `apps/lazuar-pay-checkout/{vite.config.ts,src/App.tsx,.env.example}` — contrast only (paper 05).
- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`, `One/WhoamiEndpoints.cs`, `One/WhoamiResponse.cs`, `One/OneClient.cs`, `One/OneMeMapper.cs`, `One/Bearer.cs`, `tests/Lazuar.Pay.Tests/CorsTests.cs`, `README.md`, `.env.example`.
- `packages/pay-spec/main.tsp`, `packages/pay-spec/README.md`.
- `apps/lazuar-ops/package.json`, `vite.config.ts`, `src/lib/api-client.ts`, `src/App.tsx`, `src/main.tsx`, `src/components/{LoginPage,Sidebar,EmptyWorkspaceState,PricingPage,OpsChatWorkspace}.tsx`, every path under `src/modules/` and `src/pages/` (file names listed in §3.4), plus the judgment pages (`PaymentSettingsPage`, `ProductsPage`, `TransactionsPage`, `TransactionDetailPanel`, `RefundModal`, `TeamPage`, `ApiKeysPage`, `CreateWorkspaceModal`, `SubscribersPage`, `salesDocumentType.ts`).
- `Taskfile.yml` `pay:merchant`.
- 011: `01-product.md`, `02-one-integration.md`, `03-first-slice.md`, `11-checklist.md` (`NP-ONE`, `NP-CAT`, `NP-GW-009`, `NP-FUL-003`, `NP-DOC-005`, `NP-API-004`), `12-first-slice-tracker.md`.
- 012: `02-one-authn-tokens.md`, `04-pay-spec-contract.md` §4–§6, `05-local-topology.md` CORS (stale bits called out), `06-tenant-org.md` binding table, `07-authz-roles.md` §10 VIEWER, `10-dogfood-and-tests.md` anti-goals, `checklists/{p10-spa-oidc,p60-old-frontends,c24-viewer-honesty,decisions}.md`.

Opened, in the sibling One repo (`/Users/akmalfirdaus/Code/lazuar/lazuar-one`):

- `apps/lazuar-app/src/auth/{oidcConfig.ts,bearerToken.ts,bearerToken.test.ts,RequireAuth.tsx}`, `src/pages/{LoginPage,CallbackPage}.tsx`, `src/App.tsx`, `src/main.tsx`, `.env.example`.
- `examples/vite-spa/src/{bearerToken.ts,oidc.ts,App.tsx}`, `examples/oidc-spa-notes/README.md`.
- `apps/lazuar-login/.env.example` (`REDIRECT_ALLOWLIST`).
- `apps/lazuar-api/src/Lazuar.One.Api/appsettings.Development.json` (`App:CorsOrigins`), `Program.cs` CORS (`AllowCredentials`).
- `scripts/seed-platform-spa-clients.sh` (lazuar-app + lazuar-admin only).
- `apps/lazuar-docs/docs/recipes/register-oidc-app.md` (R3).

Not opened as product design: `apps/lazuar-portal`, `apps/lazuar-admin` (Hub staff `:3005`), One `lazuar-admin` (`:5173`) except as refuse. Checkout page internals are paper 05.

### 1.2 Answers, then the rest of the paper

1. **`lazuar-pay-merchant` today** is a Vite React 19 shell on **5178** `strictPort`. It `fetch`es Pay `GET /health` with no Authorization. OIDC is unwired. That is P10.1 done and P10.2–P10.3 still open.
2. **v1 dogfood screens** are six: login redirect, workspace pick/create, products, paste gateway keys, payments list, open receipt. Map to NP-ONE-001/002/004/005/006/009, NP-CAT-001…005, NP-GW-001/009, NP-FUL-003, NP-DOC-001/003/005, NP-API-004, NP-ONE-021. Everything else in ops is **not** rebuilt.
3. **Auth sequence** is `:5178` → Zitadel `:8085` authorize → product login `:5175` → callback `:5178/callback` → JWT `access_token` in **sessionStorage** → `Authorization: Bearer` to Pay `GET /v1/whoami` (Pay forwards to One `GET /me`). Login host is **not** the homepage.
4. **Merchant should call One directly** for tenants / invites / roster (or deep-link `lazuar-app` for invite-accept). **Merchant should call Pay `/v1` only** for whoami projection, catalog, keys, payments, receipts. Do **not** grow `pay-spec` with One tenant routes. Do **not** make Pay a BFF that re-exports `/tenants`. Cite 011/02 and 012/04.
5. **Token storage:** copy `lazuar-app` — `sessionStorage` via `WebStorageStateStore`. A Pay BFF cookie is a later production option, not v1 dogfood. **Cookies on `localhost` are not port-scoped.** Hub `lazuar_auth` on `:3003` and login `lazuar_login_sess` on `:5175` are visible to `:5178` if the SPA ever uses `credentials: "include"`. Do not.
6. **Types:** later `@repo/pay-types-ts` from `packages/pay-spec`. Never Hub `@repo/api-types-ts`. One calls use `@lazuar/one-client` / One `@repo/api-type-ts`, not a union `paths` object.
7. **VIEWER:** One membership is `owner` \| `admin` \| `member`. Do not copy Hub’s invite dropdown. Hide key-paste unless `admin`/`owner`. Charge/refund: Pay-enforced `member` until One ships a real viewer; do not fake a Viewer chip; fail 403 on the API even if chrome is wrong.

---

## 2. What merchant Vite is today (health probe)

### 2.1 Package, port, scripts

`apps/lazuar-pay-merchant/package.json` on `6f866ff0`:

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
| `check-types` | `tsc -b` |
| dependencies | **only** `react` `^19.2.8`, `react-dom` `^19.2.8` |
| devDependencies | `@types/node`, `@types/react`, `@types/react-dom`, `@vitejs/plugin-react`, `oxlint`, `typescript`, `vite` `^8.2.0` |

**Not present (and must not be added as Hub leftovers):** `@repo/api-types-ts`, `openapi-fetch`, `react-router-dom` (will be needed later — from **One app**, not from ops), `oidc-client-ts` / `react-oidc-context` (will be needed — from **One app**), `@tanstack/react-query` (optional later), Tailwind/shadcn cathedral, `@google/genai`, Express, cookie session.

Workspace membership: root `pnpm-workspace.yaml` includes `apps/*`, so `pnpm --filter lazuar-pay-merchant dev` works. `Taskfile.yml`:

```yaml
pay:merchant:
  desc: Merchant Vite shell on http://localhost:5178 (not lazuar-ops)
  cmds:
    - pnpm --filter lazuar-pay-merchant dev
```

`apps/lazuar-pay/README.md` documents the same: `task pay:merchant` → `:5178` staff shell (not `lazuar-ops` `:3003`).

### 2.2 Vite config — dual-pinned `strictPort`

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

`package.json` and `vite.config.ts` **both** pin 5178. That is the same dual-pin pattern ops uses for 3003 (`apps/lazuar-ops/vite.config.ts` comment: “never silently steal 3004/3005”). Steal **that** judgment: if 5178 is taken, **fail**. Do not let Vite hop to 5179 (checkout) or 5175 (login).

Preview is **4178**, not 5178, so `vite preview` cannot collide with `vite dev`. Checkout preview is 4179. Keep that pairing.

`index.html` title: `Lazuar Pay — merchant`. Favicon: `public/favicon.svg`. No Hub branding, no “Lazuar Console” (ops `Sidebar.tsx` still says that).

### 2.3 Source tree (complete)

There is no `src/modules`, no `src/pages`, no `src/auth`, no `src/lib`. The entire application is:

| Path | What it is |
|------|------------|
| `index.html` | Root HTML; title “Lazuar Pay — merchant”; script `/src/main.tsx` |
| `src/main.tsx` | `StrictMode` + `createRoot` + `<App />`. **No** `BrowserRouter`, **no** `AuthProvider`, **no** React Query. |
| `src/App.tsx` | Health probe UI (quoted below). |
| `src/App.css` | Layout for the probe card (`max-width: 40rem`). |
| `src/index.css` | Page defaults (`#f6f5f3` background, system-ui). |
| `tsconfig.json` | Project references to `tsconfig.app.json` + `tsconfig.node.json`. There is **no** `src/vite-env.d.ts`; `tsconfig.app.json` sets `"types": ["vite/client"]`. |
| `tsconfig.app.json` | `include: ["src"]`, `jsx: react-jsx`, `verbatimModuleSyntax`. |
| `tsconfig.node.json` | `include: ["vite.config.ts"]`. |
| `.env.example` | `VITE_PAY_API_URL=http://localhost:8081` only. Comment: “Never Hub :8080. Never point lazuar-ops here.” |
| `README.md` | Origin, API, login host; “OIDC is not wired yet. Do not add a password form.” |
| `dist/` | Built probe (`index-DuegEQIu.js`). Not a product. |

That is the whole app. There is nothing to “migrate” from ops into this tree. New screens are **new files**.

### 2.4 The health probe (what the browser actually does)

`src/App.tsx` in full:

```ts
const payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'

function App() {
  const [health, setHealth] = useState('checking')

  useEffect(() => {
    const ac = new AbortController()
    fetch(`${payApi}/health`, { signal: ac.signal })
      .then((r) => (r.ok ? r.json() : Promise.reject(r.status)))
      .then((body: { status?: string }) => setHealth(body.status ?? 'ok'))
      .catch(() => {
        if (!ac.signal.aborted) {
          setHealth('unreachable')
        }
      })
    return () => ac.abort()
  }, [])
  // …
}
```

Facts about this call:

| Fact | Value | Why it matters |
|------|-------|----------------|
| URL | `{VITE_PAY_API_URL}/health` default `http://localhost:8081/health` | Process probe, **not** `GET /v1/whoami`. No Bearer. |
| Credentials | omitted (`fetch` default `same-origin`; this is cross-origin, so **no cookies**) | Correct. Do not add `credentials: "include"` (ops does; that is the Hub cookie). |
| CORS | Pay `Program.cs` allows `http://localhost:5178` and `http://127.0.0.1:5178` | `CorsTests.Health_allows_merchant_origin`. |
| Auth | none | Health must stay anonymous ([012 C15](../012-one-to-pay/checklists/c15-health-never-calls-one.md): health never calls One). |
| Copy on the page | “Sign-in is One login at `:5175`. This origin is not `lazuar-ops` (`:3003`).” | Honest. `:5175` is named as **login**, not as this app’s homepage. |

The kicker/h1 on screen: “Lazuar Pay” / “Merchant”. Description: “Staff shell for products, keys, and receipts.” That sentence already names the v1 job. Do not grow it into “Lazuar Console” with Invoicing / Chat / Hub plans.

Checkout (`apps/lazuar-pay-checkout`) is the same probe pattern on **5179**, talking to the same Pay host, with copy “Buyers have no One account.” Do not merge the two apps. Do not put OIDC on checkout (paper 05).

### 2.5 What Pay already allows this origin (server side)

`apps/lazuar-pay/src/Lazuar.Pay/Program.cs`:

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

| CORS knob | Pay `:8081` today | One `:8080` today | Hub `:8080` (ops) |
|-----------|-------------------|-------------------|-------------------|
| Origins | **5178 + 5179** only (localhost and 127) | 5173, 5174, 5177, 5180, 5181 (and 3000/3001 in Dev JSON). **No 5178.** | 3000–3005, 3020, 9080, … **No 517x.** |
| Credentials | **Not** `AllowCredentials` | `AllowCredentials()` | `AllowCredentials` + cookie JWT |
| Ops `:3003` | **Denied** (`CorsTests.Health_does_not_allow_ops_origin`) | Not listed | Listed |

Pay CORS is already the right **shape** for a Bearer SPA: exact origins, any header (so `Authorization` preflight works), any method, **no** cookies. When whoami is called from the browser, the preflight `OPTIONS /v1/whoami` with `Access-Control-Request-Headers: authorization` must succeed. `AllowAnyHeader` covers that. Do **not** add `AllowCredentials` “to match One” unless the merchant SPA starts sending cookies. It must not, in v1.

**Gap:** One `App:CorsOrigins` does **not** include `http://localhost:5178` or `http://127.0.0.1:5178`. If the merchant SPA calls One **directly** (recommended in §5), One will CORS-fail `GET /me`, `POST /tenants`, invites. That is One-side config (Development CSV + Staging `App__CorsOrigins`), not a Pay TypeSpec change. Login `:5175` is correctly **not** an API CORS origin (BFF is same-origin to login).

### 2.6 What Pay already serves that this SPA does not call yet

On this SHA the focused host maps:

| Method | Path | Auth | Merchant SPA today |
|--------|------|------|--------------------|
| `GET` | `/health` | none | **called** |
| `GET` | `/v1/health` | none | not called (spec has it; probe uses `/health`) |
| `GET` | `/v1/whoami` | Bearer | **not called** |
| `GET` | `/v1/orgs/{orgId}/ready` | Bearer + One `authz/check` `member` | not called |
| `POST` | `/v1/checkouts` | Bearer + member | not called (fixture; checkout UI is paper 05, but **merchant** will create pay links) |
| `GET` | `/v1/checkouts/{id}` | Bearer + member | not called |

`WhoamiEndpoints`: missing Bearer → 401 `"Missing bearer token"`. One 401 → Pay 401 `"Identity provider rejected the token"`. One down → 503. Projection via `OneMeMapper`: `user_id`, `email`, `is_platform_admin`, `active_org_id` (One `active_tenant_id`), `tenants[]` of `{ id, slug, name, role, status }` where `id` **is** Pay `org_id` ([012 decisions.md](../012-one-to-pay/checklists/decisions.md)).

The first product fetch this SPA should grow is **`GET /v1/whoami` with `pickApiBearerToken`**, not another `/health`. Do not hammer it (One `GET /me` can JIT-write; [011 `02`](../011-new-lazuar-pay/02-one-integration.md), `NP-ONE-006`).

### 2.7 Contrast: what `lazuar-ops` is today (so we do not retarget it)

| | `lazuar-pay-merchant` | `lazuar-ops` |
|--|------------------------|--------------|
| Port | **5178** `strictPort` | **3003** `strictPort` |
| API env | `VITE_PAY_API_URL=http://localhost:8081` | `VITE_API_URL=http://localhost:8080/api/v1` (**Hub**) |
| Types | none | `@repo/api-types-ts` (Hub OpenAPI) |
| Session | none | Cookie `lazuar_auth` via `credentials: "include"` |
| Login | unwired; copy says `:5175` | Password form `POST /one/auth/login` |
| Me | — | `GET /one/auth/me` (Hub `One.AuthUser`, not One `GET /me`) |
| Tenant header | — | `X-Tenant-Id` from `localStorage ops_active_workspace_id` (**authorizing** in Hub) |
| Router | none | Commerce / invoicing / developer / workspace + forgot/reset/verify + Hub pricing |
| P60 | this origin | Keep `VITE_API_URL` → Hub. Do not point at 8081. |

[012 `p60-old-frontends.md`](../012-one-to-pay/checklists/p60-old-frontends.md): ops stays on Hub. New merchant UI is this origin + OIDC + Pay `/v1`. Pointing ops `VITE_API_URL` at 8081 is fail F10 in [012 `10`](../012-one-to-pay/10-dogfood-and-tests.md). Pay CORS already refuses `:3003`. Keep it that way.

---

## 3. Screen inventory for v1 dogfood ONLY

[011 `01-product.md`](../011-new-lazuar-pay/01-product.md) merchant ops bullets: **products, gateway keys, payments, subscribers**. Dogfood test: sign in through One, open Pay, paste CHIP or Stripe keys, see `RCPT-`, invited MEMBER sees ops, VIEWER cannot charge. [011 `03-first-slice.md`](../011-new-lazuar-pay/03-first-slice.md) steps 1–4 and 8–9 and 12 are the merchant-UI steps; step 10 is the **buyer** page (paper 05).

This paper’s v1 **screen list** is the assigned slice, mapped onto those IDs. Subscribers is called out as a thin companion of payments (`NP-FUL-003`), not a port of Hub `SubscribersPage.tsx`.

### 3.1 The six screens (build these)

#### Screen A — Login redirect (not a password form)

| | |
|--|--|
| Job | Unauthenticated visit to `:5178` starts OIDC Authorization Code + PKCE against Zitadel `:8085`. Password UI is `:5175`. After callback, land in the shell. |
| Must look like | One `lazuar-app` `LoginPage.tsx`: a **Sign in** button, copy “No password form on this app,” optional “Create account” (`prompt=create`). No email/password fields. No forgot-password link to Pay. |
| Must not look like | `lazuar-ops` `components/LoginPage.tsx` (`handleLoginSubmit` → `POST /one/auth/login` `{ email, password }`; signup → `POST /one/public/register` with workspace slug). |
| 011 IDs | **NP-ONE-001**, **NP-ONE-002**, **NP-ONE-004**, **NP-ONE-005**, **NP-XX-007** |
| 012 | P10.2, P10.3; 02 §3.2 / §4 / §7 |
| Pay `/v1` | none yet |
| One | authorize at `:8085`; login UI `:5175`; token at `:8085/oauth/v2/token` (via `oidc-client-ts`) |

`:5175` is **not** this app’s homepage. Do not `window.location = 'http://localhost:5175/'` as the product URL. The SPA calls `auth.signinRedirect()`. Zitadel 302s to `:5175/login?authRequest=…`. Ada types `ada@acme.test` / `Password1!` **there**.

#### Screen B — Workspace pick / create

| | |
|--|--|
| Job | After whoami, if `tenants.length === 0`, create a workspace. If one tenant, select it. If many, pick. Selection is Pay `org_id` = One tenant `id` (same bytes). |
| Must look like | One `lazuar-app` `CreateWorkspacePage` / `WorkspaceSwitcher` **judgment**: name + slug, caller becomes **owner**. Ops `EmptyWorkspaceState.tsx` judgment: “signed in but no workspace; create or paste invite token.” |
| Must not look like | Ops `CreateWorkspaceModal.tsx` body `{ provision_apps: ["OPS", "BILLING", "PAYMENTS", "CRM", "LHDN"] }` or `POST /one/workspaces` (Hub). |
| 011 IDs | **NP-ONE-006**, **NP-ONE-007**, **NP-ONE-009**, **NP-ONE-010** (name later), **NP-XX-014** |
| HTTP | `GET /v1/whoami` on Pay (session start). Create = **`POST /api/v1/tenants` on One** with the same access_token (011/02 Tenancy table; 012/06 binding #5). Optional hint `X-Lazuar-Tenant-Id` on later Pay calls — **never authorization**. |
| Ready probe | `GET /v1/orgs/{orgId}/ready` is the dummy admin (`check(member)`). Useful as a “can I see this org” ping. Not a money gate. |

Empty `tenants: []` is **valid** (One R1; 012/02 §5.3). Do not invent a Pay org row to make the picker non-empty.

Invite accept can be a **seventh** micro-screen or a deep-link to `lazuar-app` `/invites/accept?tenant_id=&token=` (011/02 People: “Pay UI may deep-link … or post the same API from Pay. Keep a **non-email** accept path.” **NP-ONE-011**, **NP-ONE-012**, **NP-ONE-022**). v1 dogfood needs a second engineer on the MEMBER path; that is copy-link, not Hub email-only.

#### Screen C — Products (list / create / edit)

| | |
|--|--|
| Job | Product: name (optional description), prices monthly and/or yearly, currency **MYR**, optional seats. List / create / edit. Shareable pay link (`NP-CHK-006`) is a field on the product, not a Hub “Checkout Links” museum. |
| Steal from ops | `ProductsPage.tsx` **judgment**: warn if no gateway is pasted; don’t let Ada sell into a void. `QuickCopy` for the pay URL. Keep forms small. |
| Do not steal | `CreateProductForm` “Require Company Name & Tax ID (LHDN B2B)”, “Require WhatsApp Number”, Hub fulfillment-target badges, Resend “you must connect a Resend API key to activate checkout links,” `collectionModeLabel`, `/admin/commerce/products`. |
| 011 IDs | **NP-CAT-001** name, **NP-CAT-002** prices, **NP-CAT-003** MYR, **NP-CAT-004** seats (SST later; steal **math** from old `SstTaxMath`, not the form), **NP-CAT-005** merchant ops list/create/edit, **NP-API-004** |
| HTTP | Pay `/v1` catalog routes **do not exist yet** on `pay-spec`. They must be added to **Pay**, not copied from Hub `/admin/commerce/products`. This paper does not invent the JSON; paper 06/07 own money. The UI waits on those routes and stays a client of `/v1`. |

#### Screen D — Paste / rotate gateway keys

| | |
|--|--|
| Job | Encrypted BYOK per workspace. Dogfood **Stripe or CHIP/Billplz**, not five adapters (`NP-GW-001`…`003`, **NP-GW-009**). VIEWER (when it exists) / non-admin **cannot** save. |
| Steal from ops `PaymentSettingsPage.tsx` | Never populate password fields from GET (`applyConfig` zeros `apiKey` / `webhookSecret` / `secretKey`). Show **hints** (`api_key_hint`), not secrets. Billplz X-Signature **exactly 128 chars**. CHIP **Brand ID** (`collectionId`). Stripe **secret key** (not publishable as the vault). `canSaveVault` is a role gate — but **re-express** it in One roles (`admin`/`owner`), not Hub `ADMIN`/`SUPER_ADMIN`. |
| Do not steal | Five-gateway tab (`STRIPE \| BILLPLZ \| RAZORPAY \| CHIP \| XENDIT`) as day-one UI. Hub `PUT /admin/commerce/payment-config`. `PaymentSettingsModal` toast “credentials saved securely” as a substitute for Pay encryption (`NP-GW-001` is a **server** job). |
| 011 IDs | **NP-GW-001**, **NP-GW-009**, **NP-ONE-021**, **NP-AUD-003** (audit row on key change, same DB transaction — server, not a Hub audit page) |
| HTTP | Pay `/v1` key-vault routes **do not exist yet**. Same rule: add to `pay-spec` + host; UI is a client. |

Ops currently hides save unless `role === "ADMIN" \|\| role === "SUPER_ADMIN"`. Hub MEMBER cannot paste keys; Hub VIEWER cannot. New Pay: **`check(admin)`** on the Pay route ([012 `07` §10.3](../012-one-to-pay/07-authz-roles.md)). Chrome: hide the paste form unless whoami `role` is `admin` or `owner` **or** a later `batch-check` says `admin`. Always 403 on the API if a `member` POSTs anyway.

#### Screen E — Payments list

| | |
|--|--|
| Job | After a buyer pays on `:5179` (paper 05), Ada sees the payment on `:5178`. Status, amount, currency, payer, gateway id, receipt number. |
| Steal from ops `TransactionsPage.tsx` | Match `external_reference` to Billplz bill / Stripe PaymentIntent / CHIP id. Fees are **what Pay journaled**, not the bank payout file (ops copy already says this). Don’t poll forever; Hub’s 2s poll for `REFUND_PENDING` is a specific state, not a whoami loop. |
| Do not steal | Hub `GET /admin/commerce/transactions`, Hub status enum as TypeScript from `@repo/api-types-ts`, CSV export of Hub rows, “Audit Hub-recorded money rows” title. |
| 011 IDs | **NP-FUL-003**, **NP-API-003** / payment GET, **NP-API-004**, **NP-ONE-022** |
| Companion | A **thin** subscribers list is in 011 (`NP-FUL-003` “payments + subscribers list” and 01-product “payments, subscribers”). v1 is “the seat that this payment created,” not Hub `SubscribersPage.tsx` (anonymize, WhatsApp column, mark-paid-offline, Hub dunning). |

#### Screen F — Open receipt

| | |
|--|--|
| Job | Merchant opens the Official Receipt / payment receipt. Number `RCPT-…`, never a UUID; missing number is `PENDING`. **Do not** title it Tax Invoice. **Do not** print MyInvois VALID. |
| Steal from ops | The **distinction** in `salesDocumentType.ts` that “Official Receipt” ≠ “Tax Invoice”. The honesty that VALID is a **provider** status. `QuickCopy` for the document number. |
| Do not steal | `TaxInvoicesPage.tsx`, `TaxInvoiceDetailPanel.tsx` (`GET /lhdn/documents/{internalId}`, QR, cancel-reason required by LHDN), `classifySalesDocument` returning `"Tax Invoice"` because `lhdn_validation_status === "VALID"`, credit notes, quotes-as-tax. |
| 011 IDs | **NP-DOC-001**, **NP-DOC-002**, **NP-DOC-003**, **NP-DOC-005**, **NP-XX-003** |

Refund from this screen (later V1 `NP-MON-005`) must 403 a future viewer and, until then, follow 012/07 Option A: `member` may refund, `admin`/`owner` may refund, **do not show a Viewer option**. Steal wrap-rails copy from `RefundModal.tsx`: Billplz has **no** bill-refund API — mark refunded after the dashboard; Stripe/CHIP actually send money back. That is judgment. The Hub modal itself is not the v1 screen.

### 3.2 Mapping table (screens → 011 IDs)

| Screen | Primary IDs | Supporting IDs | Wave |
|--------|-------------|----------------|------|
| A Login redirect | NP-ONE-001, NP-ONE-002, NP-ONE-004, NP-ONE-005 | NP-ONE-003 (Bearer), NP-ONE-020 (no PAT in SPA), NP-XX-007 | S0 |
| B Workspace pick/create | NP-ONE-006, NP-ONE-009 | NP-ONE-007, NP-ONE-010, NP-ONE-011/012 (invite), NP-XX-014, NP-XX-023 | S0 |
| C Products | NP-CAT-001 … NP-CAT-005 | NP-CHK-006 (pay link), NP-API-004, NP-CAT-004 seats | S1 |
| D Paste gateway keys | NP-GW-001, NP-GW-009 | NP-GW-002 or NP-GW-003, NP-ONE-021, NP-AUD-003 | S1 |
| E Payments list | NP-FUL-003 | NP-API-003, NP-API-004, NP-ONE-022, NP-FUL-001 (server) | S1 |
| F Open receipt | NP-DOC-005 | NP-DOC-001, NP-DOC-002, NP-DOC-003, NP-XX-003 | S1 |

First-slice tracker ([011/12](../011-new-lazuar-pay/12-first-slice-tracker.md)) steps this UI participates in: **1, 2, 3, 4, 8, 9, 12**. Steps 5–7 are Pay-server / One-webhook. Steps 10–11 are checkout origin + Pay webhook handler.

`NP-API-004` is the **architectural** ID for the whole app: merchant ops is a client of `/v1` (One user JWT or `lzr_sk_` for workers). This SPA uses the **user JWT**. Workers are not this origin.

### 3.3 What v1 may **link out** to (not rebuild)

| Need | Where it already lives | Why not rebuild in `:5178` |
|------|------------------------|------------------------------|
| Password, MFA, register, reset | One `lazuar-login` `:5175` | NP-ONE-005, NP-XX-007 |
| First-party account home, IT, SSO, SCIM | One `lazuar-app` `:5174` | 011/02 Enterprise; NP-LAT-006 |
| Invite accept (optional deep-link) | `lazuar-app` `/invites/accept` | NP-ONE-012 |
| Hosted pay (buyer) | `lazuar-pay-checkout` `:5179` | paper 05; NP-CHK-005/007 |
| Staff directory | One `lazuar-admin` `:5173` | **Never** (NP-XX-018) |
| Hub ops | `lazuar-ops` `:3003` | P60; leave on 8080 until kill |

### 3.4 Ops modules and pages — complete file inventory, and what MUST NOT be rebuilt

`lazuar-ops/src` has **no** `modules/lhdn`, **no** `modules/crm`, **no** `modules/whatsapp`, **no** `modules/chat`. Those jobs leak through **invoicing**, **commerce dunning/templates**, **prompt-library**, **OpsChatWorkspace**, **PricingPage**, and `provision_apps`. The inventory below is every `.ts`/`.tsx` under `apps/lazuar-ops/src` on this SHA (excluding `components/ui/*` shadcn primitives, which are not product screens).

#### 3.4.1 Routed pages (`src/App.tsx`) — do not port the catalog

| Route | File | v1 merchant? |
|-------|------|----------------|
| `/` | `App.tsx` `HomeRedirect` → dashboard or `/pricing` | **No.** New app: unauthenticated → login redirect; authenticated → workspace or products. No Hub pricing. |
| `/pricing` | `components/PricingPage.tsx` | **MUST NOT.** Hub plan, LHDN credits, WhatsApp credits, “Lazuar Hub.” |
| `/signup`, `/login` | `components/LoginPage.tsx` | **MUST NOT** as a password form. Steal nothing but the idea of a return URL. |
| `/forgot-password` | `pages/ForgotPasswordPage.tsx` | **MUST NOT.** `POST /one/auth/forgot-password` is Hub IdP. Reset lives on `:5175`. |
| `/reset-password` | `pages/ResetPasswordPage.tsx` | **MUST NOT.** |
| `/verify-email` | `pages/VerifyEmailPage.tsx` | **MUST NOT.** (`credentials: "include"`.) |
| `/accept-invite` | `modules/workspace/pages/AcceptInvitePage.tsx` | **Do not port Hub accept.** Re-implement against One `POST /tenants/{id}/members/accept-invite` **or** deep-link `lazuar-app`. |
| `/commerce/dashboard` | `modules/commerce/pages/DashboardPage.tsx` | **MUST NOT** as v1. Hub P&L, “Hub/pack spend excluded.” A later money home can exist; it is not dogfood. |
| `/commerce/products` | `modules/commerce/pages/ProductsPage.tsx` | **Judgment only** (gateway warning, pay URL copy). New page against Pay `/v1`. |
| `/commerce/subscribers` | `modules/commerce/pages/SubscribersPage.tsx` | **MUST NOT** port. WhatsApp column, anonymize, mark-paid-offline, Hub `canWrite = role !== "VIEWER"`. Thin list later if `NP-FUL-003` needs a seat row. |
| `/commerce/transactions` | `modules/commerce/pages/TransactionsPage.tsx` | **Judgment only** (gateway ids, don’t lie about fees). New payments list. |
| `/commerce/disputes` | `modules/commerce/pages/DisputesPage.tsx` | **MUST NOT** (V1 `NP-MON-006` later). |
| `/commerce/coupons` | `modules/commerce/pages/CouponsPage.tsx` | **MUST NOT.** |
| `/commerce/dunning-campaigns` | `modules/commerce/pages/DunningCampaignsPage.tsx` | **MUST NOT.** WhatsApp dunning is **NP-XX-004 refuse**. Email dunning is `NP-SOON-004`. |
| `/commerce/dunning-campaigns/new`, `/:id` | `modules/commerce/pages/CampaignBuilderPage.tsx` | **MUST NOT.** |
| `/commerce/templates` | `modules/commerce/pages/TemplatesPage.tsx` | **MUST NOT.** WhatsApp body required. |
| `/developer/api-keys` | `modules/workspace/pages/ApiKeysPage.tsx` | **MUST NOT** as Hub `sk_*` / `lhdn.documents:*` scopes. One `lzr_sk_` minting is **lazuar-app** or One API (`NP-ONE-014`). Pay merchant is not the One key console. |
| `/developer/webhooks` | `modules/workspace/pages/DeveloperSettingsPage.tsx` | **MUST NOT** Hub outbound. One webhooks are One’s; Pay provider webhook URL is a **server** route (`NP-API-002`), maybe a read-only “paste this URL into Stripe” hint on Screen D. |
| `/developer/logs` | `modules/workspace/pages/DeliveryLogsPage.tsx` | **MUST NOT.** |
| `/workspace/general` | `modules/workspace/pages/GeneralSettingsPage.tsx` | Name/slug later via One `PATCH /tenants/{id}` (`NP-ONE-010`), not Hub general. |
| `/workspace/team` | `modules/workspace/pages/TeamPage.tsx` | **Do not port.** Hub invite roles `ADMIN`/`MEMBER`/**`VIEWER`**. New team chrome calls **One** members/invites (`NP-ONE-011`…`013`). No Viewer option until One has the role. |
| `/workspace/audit` | `modules/workspace/pages/AuditLogPage.tsx` | **MUST NOT** as a Hub feed. Pay audit is a **row in the same DB transaction** (`NP-AUD-*`, `NP-XX-019`). No Audit process. |
| `/workspace/billing-profile` | `modules/workspace/pages/BillingProfilePage.tsx` | **MUST NOT** Hub “Legal & Billing” / LHDN taxpayer profile as v1. SST registration fail-closed is server math (`NP-MON-004`), not a MyInvois onboarding wizard. |
| `/workspace/payment-gateways` | `modules/workspace/pages/PaymentSettingsPage.tsx` | **Judgment only** (Screen D). |
| `/workspace/email` | `modules/workspace/pages/EmailSettingsPage.tsx` | **MUST NOT** Hub Resend BYOK as a v1 screen. Transactional mail lives **in Pay** (`NP-MAIL-001`). |
| `/workspace/billing` | `modules/workspace/pages/BillingSettingsPage.tsx` | **MUST NOT** Hub software-fee billing. |
| `/workspace/ledger` | `modules/workspace/pages/UtilityLedgerPage.tsx` | **MUST NOT.** Hub **credits** (`GET /admin/billing/credits`) — LHDN/WhatsApp pack meter, not the money journal. |
| `/invoicing/quotes` | `modules/invoicing/pages/QuotesPage.tsx` | **MUST NOT.** Quotes-as-tax. Custom amount/quote is `NP-SOON-001` (proforma PDF, **not** a tax invoice). |
| `/invoicing/tax-invoices` | `modules/invoicing/pages/TaxInvoicesPage.tsx` | **MUST NOT.** Homemade LHDN UI. **NP-XX-001**, **NP-XX-003**. |
| `/invoicing/credit-notes` | `modules/invoicing/pages/CreditNotesPage.tsx` | **MUST NOT.** **NP-XX-010**. |
| `/ops/chat` (commented) | `components/OpsChatWorkspace.tsx` | **MUST NOT.** ADR 023 already hid it. Do not re-mount. |

#### 3.4.2 `src/modules/` — every file

**`modules/commerce/pages/`**

- `CampaignBuilderPage.tsx` — **MUST NOT** (WhatsApp steps, `whatsapp_body`).
- `CouponsPage.tsx` — **MUST NOT**.
- `DashboardPage.tsx` — **MUST NOT** as v1.
- `DisputesPage.tsx` — **MUST NOT** v1.
- `DunningCampaignsPage.tsx` — **MUST NOT**.
- `ProductsPage.tsx` — judgment only (Screen C).
- `SubscribersPage.tsx` — **MUST NOT** port; `canWrite = workspaceRole !== "VIEWER"` is Hub vocabulary.
- `TemplatesPage.tsx` — **MUST NOT** (WhatsApp required).
- `TransactionsPage.tsx` — judgment only (Screen E).

**`modules/commerce/components/`**

- `CouponDetailPanel.tsx`, `CreateCouponModal.tsx` — **MUST NOT**.
- `CreateProductForm.tsx`, `CreateProductModal.tsx`, `ProductForm.tsx`, `ProductDetailPanel.tsx` — judgment on **small product fields**; **MUST NOT** copy LHDN B2B TIN, WhatsApp required, Hub fulfillment targets.
- `CreateSubscriberModal.tsx` — **MUST NOT**.
- `MessageTemplateEditor.tsx` — **MUST NOT**.
- `RefundModal.tsx` — **steal wrap-rails copy**; new modal against Pay refund when V1 exists.
- `TransactionDetailPanel.tsx` — judgment for Screen E/F (amount, payer, gateway); **MUST NOT** as Hub DTO.
- `transactionStatus.ts` — Hub status labels; do not import. New status enum from `pay-spec`.
- `dunning/CampaignSettingsPanel.tsx`, `CampaignTimeline.tsx`, `DunningStepEditor.tsx`, `types.ts` — **MUST NOT**. `DunningStepEditor` offers “Send WhatsApp (not connected)” and documents Billplz wrap-rails (that **sentence** is judgment for paper 06, not a dunning builder in this SPA).

**`modules/core/components/`**

- `PageLayout.tsx`, `QuickCopy.tsx`, `SidePanel.tsx` — **optional steal as layout primitives**, not as a module boundary. Re-implement in merchant; do not import from ops.

**`modules/invoicing/` — entire tree MUST NOT be rebuilt**

- `pages/CreditNotesPage.tsx`
- `pages/QuotesPage.tsx`
- `pages/TaxInvoicesPage.tsx`
- `components/CreateQuoteModal.tsx`
- `components/QuoteDetailPanel.tsx`
- `components/TaxInvoiceDetailPanel.tsx` (live LHDN document + QR + cancel)
- `lib/salesDocumentType.ts` — steal the **Official Receipt vs Tax Invoice** honesty; do not copy the VALID→“Tax Invoice” branch into Pay.

**`modules/workspace/pages/`**

- `AcceptInvitePage.tsx` — One API or deep-link; not Hub.
- `ApiKeysPage.tsx` — **MUST NOT**. Scope catalog includes **“LHDN documents”** (`lhdn.documents:write` / `:read`) and Hub `payments.checkouts:*`. That is the cathedral. One keys: `lazuar-app` Settings → API keys, prefix `lzr_sk_`.
- `AuditLogPage.tsx` — **MUST NOT**.
- `BillingProfilePage.tsx` — **MUST NOT** as LHDN taxpayer UI.
- `BillingSettingsPage.tsx` — **MUST NOT** Hub subscription.
- `DeliveryLogsPage.tsx` — **MUST NOT**.
- `DeveloperSettingsPage.tsx` — **MUST NOT** Hub webhooks.
- `EmailSettingsPage.tsx` — **MUST NOT**.
- `GeneralSettingsPage.tsx` — later One `PATCH /tenants/{id}` only.
- `PaymentSettingsPage.tsx` — judgment (Screen D).
- `TeamPage.tsx` — **MUST NOT** port; **MUST NOT** keep `<option value="VIEWER">`.
- `UtilityLedgerPage.tsx` — **MUST NOT** (Hub credits).

**`modules/workspace/components/`**

- `CreateWorkspaceModal.tsx` — **MUST NOT** copy `provision_apps: ["OPS", "BILLING", "PAYMENTS", "CRM", "LHDN"]`. New modal → One `POST /tenants`.
- `PaymentSettingsModal.tsx` — duplicate of gateway paste; judgment only.

#### 3.4.3 `src/pages/` (auth leftovers)

- `ForgotPasswordPage.tsx` — **MUST NOT**.
- `ResetPasswordPage.tsx` — **MUST NOT**.
- `VerifyEmailPage.tsx` — **MUST NOT**.

These three files exist **because** Hub is an IdP. One login already owns them. Shipping them on `:5178` is a second password product.

#### 3.4.4 `src/components/` product islands (not `ui/`)

| File | v1 merchant? |
|------|----------------|
| `LoginPage.tsx` | **MUST NOT** (password + Hub register + workspace slug). |
| `Sidebar.tsx` | **MUST NOT** the module catalog (Commerce / Invoicing / Developer / Workspace; “Lazuar Console”). Optional: steal **collapsed rail + workspace role chip** as UX, rebuilt. |
| `EmptyWorkspaceState.tsx` | steal empty-state judgment; new copy (One tenant, not Hub slug rules as SoT). |
| `PricingPage.tsx` | **MUST NOT.** Hub Starter, LHDN credits, WhatsApp credits. |
| `OpsChatWorkspace.tsx` | **MUST NOT.** Ops AI chat. |
| `ConversationsDirectory.tsx` | **MUST NOT.** |
| `ActionApprovalCard.tsx` | **MUST NOT** (chat proposed actions). |
| `PaymentSettingsModal.tsx` | judgment only (keys). |
| `chat/ChatEmptyState.tsx` | **MUST NOT.** |
| `chat/ChatInputArea.tsx` | **MUST NOT.** |
| `chat/ChatMessageBubble.tsx` | **MUST NOT.** |
| `chat/CopyButton.tsx` | ignore. |
| `chat/FormRegistry.ts` | **MUST NOT.** |
| `chat/MarkdownContent.tsx` | **MUST NOT.** |
| `chat/PromptLibrary.tsx` | **MUST NOT.** |
| `chat/UiRequestCard.tsx` | **MUST NOT.** |
| `forms/AutoForm.tsx` | **MUST NOT** (chat forms). |
| `forms/CreateProductForm.tsx` | **MUST NOT** (LHDN B2B + WhatsApp). |
| `forms/types.ts` | ignore. |
| `ui/*` (55 shadcn files) | Not a product. Merchant may stay CSS-small; do not import ops `ui/` as a shortcut to the cathedral. |

#### 3.4.5 `src/hooks/`, `src/lib/`, `src/types/`

| File | v1 merchant? |
|------|----------------|
| `hooks/use-chat-stream.ts` | **MUST NOT** (`credentials: "include"`). |
| `hooks/use-debounce.ts` | optional primitive. |
| `hooks/use-mobile.ts` | optional primitive. |
| `hooks/use-product-associations.ts` | **MUST NOT** (Hub associations). |
| `lib/api-client.ts` | **MUST NOT copy.** `openapi-fetch` + `@repo/api-types-ts` + `credentials: "include"` + `X-Tenant-Id`. This is the file P60 exists to quarantine. |
| `lib/prompt-library.ts` | **MUST NOT.** WhatsApp prompts (“share on WhatsApp”, “Send a direct WhatsApp or Email”). |
| `lib/utils.ts` | optional `cn`; do not import Hub `filterHiddenFulfillmentTargets`. |
| `lib/workspace-slug.ts` | slug rules may be **One’s** (One validates slug). Do not keep Hub reserved-name list as Pay SoT. |
| `types/chat.ts` | **MUST NOT.** |

#### 3.4.6 Explicit refuse list (ops features that MUST NOT be rebuilt)

These are named because they will be “just one more page”:

| Feature | Where it lives in ops | Why refuse |
|---------|----------------------|------------|
| **Homemade LHDN / MyInvois / UBL / QR / VALID** | `TaxInvoicesPage`, `TaxInvoiceDetailPanel`, `CreditNotesPage`, `salesDocumentType` VALID branch, `ApiKeysPage` LHDN scopes, `PricingPage` LHDN credits, `CreateProductForm` TIN-at-checkout, `CreateWorkspaceModal` `provision_apps` includes `"LHDN"` | **NP-XX-001**, **NP-XX-002**, **NP-XX-003**, **NP-LAT-001** |
| **Quotes as tax invoices / sales documents** | `QuotesPage`, `CreateQuoteModal`, `QuoteDetailPanel`, sidebar “Sales documents” | **NP-SOON-001** is a **proforma**, not this; **NP-XX-003** |
| **Credit & debit notes** | `CreditNotesPage` | **NP-XX-010** |
| **WhatsApp dunning / WhatsApp required / WhatsApp credits** | `DunningStepEditor`, `CampaignBuilderPage`, `TemplatesPage`, `ProductForm`, `SubscribersPage` WhatsApp column, `prompt-library.ts`, `PricingPage` | **NP-XX-004** |
| **Ops AI chat / conversations / proposed actions** | `OpsChatWorkspace`, `ConversationsDirectory`, `components/chat/*`, `use-chat-stream.ts`, `types/chat.ts` | Cathedral. ADR 023 already disconnected it. |
| **Hub CRM** | No `modules/crm`, but `provision_apps` includes `"CRM"`; Hub entitlements; buyer as Hub `GlobalUser` | Buyer plane is Pay (`NP-BUY-*`, **NP-XX-013**). No Hub CRM shell. |
| **Hub cookie IdP** | `LoginPage`, forgot/reset/verify, `api-client.ts` `credentials: "include"` | **NP-XX-007**, P60 |
| **Hub credits / utility ledger / Hub plan pricing** | `UtilityLedgerPage`, `BillingSettingsPage`, `PricingPage` | Not money-in. Pack meters for LHDN/WhatsApp. |
| **Hub API keys with LHDN scopes** | `ApiKeysPage` | Wrong prefix, wrong catalog. One `lzr_sk_` lives in One. |
| **Email provider BYOK as v1 screen** | `EmailSettingsPage`, ProductsPage Resend warning | `NP-MAIL-*` is in-process, not a Hub Resend form. |
| **Coupons, dashboard P&L, disputes UI, notification templates** | commerce pages | Not on the dogfood sentence. |
| **Team invite VIEWER option** | `TeamPage.tsx` | One cannot store VIEWER ([012 `07` §10](../012-one-to-pay/07-authz-roles.md), C24). |
| **Shipping merchants to `lazuar-admin`** | ops never does this; do not start | **NP-XX-018**, `:5173` |

Sidebar (`Sidebar.tsx`) currently advertises four modules and these links — **none** of the invoicing/developer/workspace-billing set is v1:

```
Commerce: Dashboard, Checkout Links, Subscribers, Transaction Logs, Disputes,
          Promotions, Dunning Campaigns, Notification Templates
Invoicing: Quotes, Sales documents, Credit Notes
Developer: API Keys, Outbound Webhooks, Delivery Logs
Workspace: General, Team, Audit, Legal & Billing, Payment Gateways,
           Plan & billing, Email Provider
```

v1 merchant nav is closer to:

```
Products | Keys | Payments
(+ Receipt on a payment row)
(+ Workspace switcher in the chrome)
(+ Sign out → OIDC end_session)
```

Team/invite can be a small Settings item that **calls One**, or a “manage members in lazuar-app” link for week one. It is not Hub TeamPage.

### 3.5 Judgment worth stealing (not files)

Copy these **rules**, in new components:

1. **Never echo secrets** from GET into password inputs (`PaymentSettingsPage.applyConfig`).
2. **Hints, not keys** (`api_key_hint`, `has_api_key`).
3. **Wrap-rails honesty** (`RefundModal`: Billplz has no refund API; Stripe/CHIP do). Paper 06 owns the matrix; the UI must not imply silent debit on reminder-only rails (`NP-GW-007`).
4. **Gateway warning** before Ada shares a pay link (`ProductsPage`).
5. **Receipt ≠ tax invoice**; number is not a UUID (`NP-DOC-*`).
6. **Chrome hides, API 403s** (old issue 326; 012/07 §10.4). Hiding a button is not authorization.
7. **Empty membership is a first-run screen**, not a crash (`EmptyWorkspaceState`).
8. **strictPort dual-pin** (ops `vite.config.ts` comment — apply to 5178).
9. **SST exclusive on the unit, then × seats; fail closed if SST registration unknown** — from old **backend** judgment ([011 `01`](../011-new-lazuar-pay/01-product.md)), not from an ops form. The product form may later have a seats field (`NP-CAT-004`); it must not invent tax UI.

Do not steal: Hub role strings `SUPER_ADMIN` / `ADMIN` / `MEMBER` / `VIEWER`, header `X-Tenant-Id`, cookie session, `@repo/api-types-ts`, `provision_apps`, LHDN VALID chrome, WhatsApp, chat, Hub credits.

---

## 4. Auth sequence: `:5178` → `:5175` → callback `:5178/callback` → `access_token` → Pay whoami

This is the same Path A as [012 `02` §1.5 / §3.2](../012-one-to-pay/02-one-authn-tokens.md), with the **SPA origin** now known.

### 4.1 Ports (this app’s world)

| Host | Port | Role in this sequence | Merchant SPA |
|------|------|------------------------|--------------|
| `lazuar-pay-merchant` | **5178** | **This origin.** Authorize starts here. Callback `http://localhost:5178/callback`. Homepage after login. | **Yes** |
| `lazuar-pay-merchant` preview | **4178** | `vite preview` only. If used as a login test, it needs its **own** redirect URI on the One app + allowlist. Prefer not to dogfood preview as the SPA. | no |
| Zitadel issuer | **8085** | OIDC **authority**. Discovery, authorize, token, JWKS, end_session. | SPA `authority`. Never Management. |
| `lazuar-login` | **5175** | **Password UI** + BFF. Zitadel 302 `?authRequest=`. | Users land here because Zitadel redirected. **Not homepage.** |
| login BFF loopback | **5176** | Vite on 5175 proxies `/api` here. Holds login-client PAT. | **Never** from Pay. Never `VITE_*`. |
| One API | **8080** | Resource server `/api/v1`. `GET /me`, `POST /tenants`, invites, `POST /tenants/{id}/apps`. | Browser may call (CORS gap, §5). Pay **server** already calls `/me`. |
| focused Pay | **8081** | Money + whoami projection `/v1`. | Browser **does** call (CORS already allows 5178). |
| `lazuar-app` | **5174** | One customer SPA. Ada’s first JWT **before** Pay `client_id` exists, and invite-accept deep-link. | Not Pay homepage. |
| `lazuar-admin` (One) | **5173** | Lazuar staff. | **Never** (NP-XX-018). |
| stock Login V2 | **3005** | Break-glass; collides with old Pay admin. | **Never** (NP-ONE-005). |
| `lazuar-ops` | **3003** | Hub merchant console. | **Never retarget.** |
| `lazuar-pay-checkout` | **5179** | Buyer cash register. | Not this app; no OIDC. |
| examples/vite-spa | **5177** | Integrator sample of the same picker. | Copy code, not the port. |
| OpenFGA | **8090** | One only. | **Never.** |
| old Hub API | **8080** | Collision with One. | **Down** when dogfooding. |

`NP-ONE-005` note: product login via `:5175`; `:5175` is **not** Pay’s homepage; merchant Vite is `:5178`. That row is this paper.

### 4.2 Sequence (happy path)

```text
1. Ada opens http://localhost:5178
2. Unauthenticated shell renders Sign in (no password fields).
3. Click Sign in → oidc-client-ts / react-oidc-context signinRedirect()
     client_id     = Pay merchant public client (seeded or POST /apps)
     authority     = http://localhost:8085
     redirect_uri  = http://localhost:5178/callback
     response_type = code
     scope         = openid profile email offline_access
     PKCE          = S256 (library default)
4. Browser hits Zitadel :8085 /oauth/v2/authorize
5. Zitadel 302 → http://localhost:5175/login?authRequest=V2_…
     (ZITADEL_OIDC_DEFAULTLOGINURLV2; not in SPA env)
6. Ada enters ada@acme.test / Password1! on :5175
     Login BFF uses login-client PAT (server-only) → Session API v2 → OIDC finalize
     callbackUrl must be on login REDIRECT_ALLOWLIST  AND  the One app redirect_uris
7. Browser returns to http://localhost:5178/callback?code&state
8. react-oidc-context exchanges code at :8085 (token endpoint)
     Tokens: access_token (JWT) + id_token + refresh_token
     Stored in sessionStorage key oidc.user:http://localhost:8085:<client_id>
     onSigninCallback: history.replaceState → strip code from URL
9. SPA pickApiBearerToken(user) → JWT access_token or undefined
10. GET http://localhost:8081/v1/whoami
      Authorization: Bearer <access_token>
      Accept: application/json
      optional X-Lazuar-Tenant-Id: <guid>   // hint only
11. Pay OneClient GET http://localhost:8080/api/v1/me with the same Authorization
12. Pay returns WhoamiResponse (projection). SPA picks tenants[].id as org_id.
```

`GET /me` is a **write** when `email_verified == true` (domain auto-join / SSO JIT). Call whoami on **login / identity refresh / workspace switch**, not per table row, not in a 2s poll, not on `/health`.

### 4.3 `client_id`

There is **no** Pay merchant `client_id` on this SHA. `seed-platform-spa-clients.sh` hard-codes names `lazuar-app` / `lazuar-admin` and redirects `:5174` / `:5173`. Login `REDIRECT_ALLOWLIST` default:

```text
http://localhost:5173,http://localhost:5174,http://localhost:5177,http://localhost:8085,http://localhost:5175
```

**No 5178.** Adding the origin is **NP-ONE-004**. Two honest ways (both allowed by NP-ONE-001; **neither** is Console-only):

**Way A — One first-party seed (preferred for a product SPA used by all merchants).**  
A script in **lazuar-one**, modeled on `seed-platform-spa-clients.sh`: name `lazuar-pay-merchant`, `OIDC_APP_TYPE_USER_AGENT`, `OIDC_AUTH_METHOD_TYPE_NONE`, `OIDC_TOKEN_TYPE_JWT`, PKCE, Dev Mode for http, redirect `http://localhost:5178/callback`, post-logout `http://localhost:5178/`. One ops holds `ZITADEL_PAT`. **Pay never does.** Write `VITE_ZITADEL_CLIENT_ID` into `apps/lazuar-pay-merchant/.env` (gitignored) the way `WRITE_ENV=1` writes app/admin. Also add `http://localhost:5178` (and `http://127.0.0.1:5178` if that twin is used) to login `REDIRECT_ALLOWLIST`. P10.4: One-repo seed is convenience, not a One product feature.

**Way B — `POST /api/v1/tenants/{tenantId}/apps` (preferred if seed is not ready; required as the **day-2** path for tenant-owned apps).**  
Ada already has a workspace in **lazuar-app**. As owner/admin, R3:

```bash
curl -sS -X POST "$API_BASE/tenants/$TENANT_ID/apps" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "lazuar-pay-merchant",
    "type": "spa",
    "redirect_uris": ["http://localhost:5178/callback"],
    "post_logout_redirect_uris": ["http://localhost:5178/"]
  }'
```

Response: `client_id`, `issuer`, redirects. **No** `client_secret` for `type: spa`. API-provisioned apps request **JWT** access tokens. Then still add `:5178` to `REDIRECT_ALLOWLIST`.

**Chicken and egg:** Way B needs a user JWT **before** Pay’s own login works. That JWT comes from **lazuar-app** `:5174`. That is acceptable for dogfood (012/02 §9 already used Ada’s app token for backend whoami). Way A removes the chicken and egg for every developer after One ops runs the seed.

**Forbidden:** creating the client only in Zitadel Console (`NP-ONE-001` notes: “Not a Zitadel Console click”; P10.3 “Console-only client_id with no One app object”). Console is break-glass for leftover opaque apps (One issue 001), not the happy path. Opaque access tokens must **not** be healed by sending `id_token`.

Public SPA: **no secret in the Vite app.** `VITE_ZITADEL_CLIENT_ID` is public (NP-ONE-020). Confidential `type: web` + `client_secret` is only if Pay later chooses a **BFF** (§6). v1 dogfood is public SPA, like `lazuar-app`.

### 4.4 Copy the OIDC client from One, not from ops

Mirror `lazuar-app`, not ops:

| Item | Copy from | Value for merchant |
|------|-----------|--------------------|
| Library | `apps/lazuar-app/src/main.tsx` | `AuthProvider` from `react-oidc-context` wrapping the router |
| Config | `src/auth/oidcConfig.ts` | `response_type: 'code'`, `automaticSilentRenew: true`, `WebStorageStateStore({ store: window.sessionStorage })`, `onSigninCallback` replaceState |
| Picker | `src/auth/bearerToken.ts` | `pickApiBearerToken` — JWT `access_token` only; opaque/JWE/empty → `undefined` |
| Tests | `src/auth/bearerToken.test.ts` | Lock: never returns `id_token`; no fallback when access is opaque |
| Login page | `src/pages/LoginPage.tsx` | No password fields |
| Callback | `src/pages/CallbackPage.tsx` | Friendly errors; never print tokens |
| Gate | `src/auth/RequireAuth.tsx` | Redirect to `/login` with `from` state |
| Example (no react-oidc-context) | `examples/vite-spa/src/{oidc.ts,bearerToken.ts,App.tsx}` | Same picker; `UserManager.signinRedirectCallback` on `/callback` |

`pickApiBearerToken` as it exists in One `lazuar-app` and is copied into `examples/vite-spa` (do not invent a fourth policy):

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

Wire it like `ApiClientBridge` in `lazuar-app/src/App.tsx`: `getAccessToken: () => pickApiBearerToken(auth.user)` **synchronously**, not in `useEffect` (first paint 401).

`examples/oidc-spa-notes/README.md`: “Send the **access** token — never the `id_token`.” One M2M-14 / issue 002 is closed. Pay must not reopen it.

### 4.5 Recommended merchant env (later; not on this SHA)

`apps/lazuar-pay-merchant/.env.example` today:

```text
# Focused Pay host. Never Hub :8080. Never point lazuar-ops here.
VITE_PAY_API_URL=http://localhost:8081
```

When OIDC is wired, **add** (names from 012/02 §8.2, ports from this paper):

| Env | Local value | Secret? |
|-----|-------------|---------|
| `VITE_PAY_API_URL` | `http://localhost:8081` | no |
| `VITE_ZITADEL_AUTHORITY` | `http://localhost:8085` | no (issuer, **not** `:5175`) |
| `VITE_ZITADEL_CLIENT_ID` | seeded / POST `/apps` | **public** |
| `VITE_ZITADEL_REDIRECT_URI` | `http://localhost:5178/callback` | no |
| `VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI` | `http://localhost:5178/` | no |
| `VITE_ZITADEL_SCOPE` | `openid profile email offline_access` | no |
| `VITE_ONE_API_URL` (only if SPA calls One directly, §5) | `http://localhost:8080/api/v1` | no |

Do **not** put in any `VITE_*`: login-client PAT, `ZITADEL_PAT`, OpenFGA token, `lzr_sk_`, `client_secret`, Ada’s password, Hub `Jwt:Secret`. `NP-ONE-020`: Pay holds only OIDC `client_id`, `lzr_sk_` (on the **Pay server**, not the SPA), One-webhook HMAC (server).

Login host remains **out of SPA OIDC env** (012/02; One D92). Switching product login from `:3005` to `:5175` does not change `client_id` or redirect URIs.

Staging: no localhost authority/redirects (One `deploy/staging/spa.env.app.staging.example` pattern). Exact HTTPS origins on the One app + allowlist + CORS.

### 4.6 CORS on Pay vs CORS on One vs login allowlist

Three different lists. Mixing them is the usual outage.

| List | Who checks it | What 5178 needs |
|------|---------------|-----------------|
| **Pay CORS** `Program.cs` WithOrigins | Pay `:8081` when the **browser** calls Pay | **Already has** 5178 / 127.0.0.1:5178. Keep. Do not add 3003. Do not `AllowCredentials` for v1. |
| **One CORS** `App:CorsOrigins` | One `:8080` when the **browser** calls One | **Missing 5178.** Required **if and only if** the SPA calls One directly (§5). Add localhost **and** 127.0.0.1 twins (One issue 077). Staging empty CSV **fails boot**. |
| **Login `REDIRECT_ALLOWLIST`** | `lazuar-login` BFF on finalize `callbackUrl` | **Missing 5178.** Required as soon as OIDC callback is `:5178/callback`, even if the SPA never calls One HTTP. Production empty list **crashes** the BFF. |
| **One app `redirect_uris`** | Zitadel, via the app object created by POST `/apps` or seed | **Missing** until NP-ONE-001. Must include `http://localhost:5178/callback`. |

Server-to-server Pay `:8081` → One `:8080` (`OneClient.GetWhoamiAsync`) is **not** a CORS request. It already works without 5178 on One’s list.

Pay CORS tests to keep:

- 5178 allowed (`CorsTests.Health_allows_merchant_origin`)
- 5179 allowed (checkout, paper 05)
- **3003 not allowed** (`Health_does_not_allow_ops_origin`)

Add, when whoami is called from the browser: a test that `OPTIONS /v1/whoami` from Origin `http://localhost:5178` with `Access-Control-Request-Headers: authorization` returns 204/200 and `Access-Control-Allow-Headers` includes `Authorization`. Do not implement that test in this analysis file.

### 4.7 What the SPA sends to Pay whoami

```http
GET /v1/whoami HTTP/1.1
Host: localhost:8081
Origin: http://localhost:5178
Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.…
Accept: application/json
```

Pay `Bearer.TryGet` requires the `Bearer ` prefix and a non-empty token. It does **not** parse JWT vs `lzr_sk_` vs `id_token` — it forwards. The SPA picker is the first guard; One `JwtAccessTokenGuard` is the second (`jti` required; `id_token` 401). Do not “helpfully” send `id_token` if `access_token` is opaque.

Optional:

```http
X-Lazuar-Tenant-Id: <guid>
```

Pay forwards this as a **hint** to One (`WhoamiEndpoints` reads the header). Hub ops sends **`X-Tenant-Id`** (different name) and Hub **authorizes** by it. New merchant must use One’s header name and must not treat it as SoT (`NP-ONE-007`). Path `{orgId}` on money routes is SoT.

### 4.8 Logout

`lazuar-app` uses OIDC `end_session` at the authority with `post_logout_redirect_uri` = origin `/`. Copy that. Do **not** `POST /one/auth/logout` (ops `handleLogout` — Hub cookie clear). Clearing `sessionStorage` without end_session leaves the Zitadel SSO session; the next Sign in may skip `:5175`. That may be fine for local DX; document it.

Do not deep-link `:5175/logout` as a product button without going through the SPA’s `signoutRedirect` (Zitadel `post_logout` must match the app object).

---

## 5. Whether merchant should call One APIs directly (tenants, invites) or only through Pay

**Recommendation: the merchant browser calls two hosts. Identity/tenancy/invites → One. Money/whoami-projection/catalog/keys/payments/receipts → Pay `/v1`. Do not make Pay a BFF that re-exports One’s tenant API.**

### 5.1 What 011/02 already assigned

[011 `02-one-integration.md`](../011-new-lazuar-pay/02-one-integration.md) “HTTP Pay should use” is the **Consumer-0** list. It is not “put these paths on `:8081`.”

Tenancy: `POST /tenants` (create workspace, caller becomes owner), `GET /tenants`, `GET/PATCH /tenants/{id}`, leave/delete/transfer — **One**. “Create workspace = `POST /tenants`; One tenant id **is** Pay `org_id`” (`NP-ONE-009`). “Do **not** call `POST /platform/tenants`” (`NP-XX-023`).

People: members, invite, pending, revoke, resend, accept-invite, roster, `GET /me/invites` — **One**. “Pay UI may deep-link to `lazuar-app` `/invites/accept?tenant_id=&token=` **or** post the same API from Pay.”

Machines: `lzr_sk_` mint/list/revoke, `POST /tenants/{id}/apps` — **One**. The merchant SPA is not the One API-key console in v1; Ada can mint keys in `lazuar-app`. Pay **server** holds a scoped `lzr_sk_` later (`NP-ONE-014`).

Authz: `POST /tenants/{id}/authz/check` — Pay **server** already does this for `/v1/orgs/{orgId}/ready`. The SPA should not second-guess money routes with a browser-side FGA call. Chrome may later `batch-check` (`NP-ONE-016`) from the SPA **to One** or from Pay as a projection — still not SoT.

### 5.2 What 012/04 forbids

[012 `04-pay-spec-contract.md`](../012-one-to-pay/04-pay-spec-contract.md) answer 2:

> **Must not** copy One tenant/invite routes into `packages/pay-spec`. `POST /tenants`, members, invites, `GET /me/invites`, SSO/SCIM, `POST /platform/tenants` live in **One’s** TypeSpec. Copying them would re-implement `Modules/One` at the contract layer (`NP-XX-007`, `NP-XX-014`). **Pay UI that needs a roster or an invite calls One, not Pay.**

Answer 3: pointing ops `VITE_API_URL` at 8081, or growing `pay-spec` with `/one/*`, type-checks a lie.

The honest frontend shape after S0/S1 (012/04 §4.3), with this paper’s origin filled in:

| Concern | Client | Base URL | Types |
|---------|--------|----------|-------|
| Login | Browser OIDC (Pay merchant `client_id`) | Zitadel `:8085` + product login `:5175` | not Hub types |
| Identity, tenants, invites, authz chrome | `@lazuar/one-client` (workspace; do not wait on npm) **or** `fetch` + picker | One `http://localhost:8080/api/v1` | One `@repo/api-type-ts` / client `MeResponse` |
| Money, catalog, receipts, **Pay `whoami`** | new fetch / later `@repo/pay-types-ts` | Pay `http://localhost:8081` (paths already include `/v1`) | **future** `@repo/pay-types-ts` |
| Hub leftover | `lazuar-ops` only, until kill | Hub `8080/api/v1` | `@repo/api-types-ts` |

`GET /v1/whoami` is used as a **Pay session probe** (in addition to One `GET /me` if the SPA also talks to One, **not instead of** One as SoT). Do not keep Hub `GET /one/auth/me` as a fallback.

### 5.3 Pay-as-BFF (rejected for v1)

A tempting design: SPA talks **only** to `:8081`; Pay proxies `POST /v1/orgs` → One `POST /tenants`, `POST /v1/invites` → One invite. That is:

- `Modules/One` with extra latency.
- A second TypeSpec for One’s nouns (`NP-XX-007`).
- A place to accidentally store a Pay `organizations` table (`NP-XX-014`).
- A place to mint a Pay cookie “because we already have a session” (012/02 §7 refuse).

Pay **already** proxies **one** identity read: `GET /v1/whoami` → One `GET /me`, as a **projection** Pay is willing to serve to **its** clients (012/04 answer 1; 012 decisions). That is enough for the money host to know `org_id` without the SPA parsing One’s `MeResponse` for the charge path. It is **not** a license to proxy the rest of One.

Exception that is **not** a BFF: Pay server continues to call One `authz/check` on **Pay** money routes. The browser does not need to.

### 5.4 Direct One calls — what the SPA needs on One

If Screen B create-workspace and invite live in `:5178`:

1. Add `http://localhost:5178` and `http://127.0.0.1:5178` to One `App:CorsOrigins` (Development JSON and any compose overlay). Staging/Production: exact HTTPS origin, required at boot.
2. SPA `fetch` to One **without** `credentials: "include"` unless you have a deliberate cookie; Authorization header is enough. One’s policy is `AllowCredentials` + exact origins — that is compatible with a header-only client as long as you do not send `Access-Control-Allow-Origin: *`. Do not send Hub cookies to One.
3. Use `pickApiBearerToken` for **both** hosts (same access_token).
4. One `GET /me` is still a write — prefer Pay `GET /v1/whoami` as the **session start** call, and One tenant POST only on explicit create. Do not double-GET `/me` and `/v1/whoami` in a hot loop. Reasonable v1: whoami on login; One `POST /tenants` on button click; whoami again after create to refresh `tenants[]`.
5. Header hint: `X-Lazuar-Tenant-Id`, not `X-Tenant-Id`.

`@lazuar/one-client` `createClient({ baseUrl, getAccessToken, getTenantId })` is the TS equivalent (`examples/vite-spa/src/App.tsx`). Workspace import is enough (`NP-XX-021`). Do not block merchant UI on npm publish.

### 5.5 Deep-link fallback (honest, smaller)

011/02 and NP-ONE-012 explicitly allow deep-link to `lazuar-app` for invite-accept. 012/02 §9 dogfood even created the workspace **in lazuar-app** before Pay existed.

For a **smaller** first wiring of `:5178`:

- Login + whoami + org picker from `tenants[]` **in merchant**.
- “Create workspace” button → `https://localhost:5174/workspaces/new` (or whatever `lazuar-app` route is) **or** in-app One POST.
- “Invite a teammate” → `lazuar-app` members page **or** in-app One invite.

The **dogfood sentence** still requires a second engineer on MEMBER (`NP-ONE-022`). Copy-link can be produced by `lazuar-app` while merchant only **picks** the workspace. That is a sequencing choice, not a product split. This paper’s Screen B still **names** in-app create as the intended merchant surface (011/03 step 3: “Create workspace **in Pay** = `POST /tenants`”).

### 5.6 What the SPA must never call

| Call | Why |
|------|-----|
| One `POST /platform/tenants` | NP-XX-023 staff directory |
| One `authz/write` | NP-XX-016 |
| Zitadel Management / PAT | NP-XX-017, NP-ONE-020 |
| OpenFGA `:8090` | Pay never |
| Hub `/one/auth/*`, `/admin/commerce/*`, `/lhdn/*` | Wrong host, wrong product |
| Pay `/one/*` | Does not exist; do not add |
| Checkout origin APIs as merchant session | Buyers are a different plane |

### 5.7 `lzr_sk_` is not this SPA’s Bearer

Merchant ops as a **browser** uses the **user JWT**. `lzr_sk_` is for Pay workers / cron / Pay API → One (`NP-ONE-014`). Putting a secret key in `VITE_*` or sessionStorage next to the OIDC user is a leak. ApiKeysPage in ops is Hub’s key UI; do not rebuild it here so Ada can “test Bearer.” If Ada needs a machine key, she uses `lazuar-app`.

---

## 6. Token storage (sessionStorage like `lazuar-app` vs BFF)

### 6.1 Recommendation for v1 dogfood: sessionStorage, same as `lazuar-app`

`lazuar-app/src/auth/oidcConfig.ts`:

```ts
userStore: new WebStorageStateStore({ store: window.sessionStorage }),
```

Comment in that file (steal this honesty, not a different store):

> Silent renew stores tokens in sessionStorage — convenient for local DX but XSS can exfiltrate them. Prefer short-lived tokens / BFF for prod.

Key shape from `oidc-client-ts`: `oidc.user:{authority}:{client_id}` e.g. `oidc.user:http://localhost:8085:<pay-merchant-client-id>` on origin `http://localhost:5178`.

**sessionStorage is origin-scoped** (scheme + host + **port**). Tokens on `:5178` are **not** readable by `:5174`, `:5175`, `:5179`, or `:3003`. That is the correct isolation for a multi-port laptop.

`localStorage` would also be origin-scoped including port, but would survive the tab; `lazuar-app` chose sessionStorage. Copy that. Do not invent `localStorage` “so Ada stays logged in after closing the tab” as a v1 fork.

Ops today uses **neither** for the session: Hub cookie `lazuar_auth` + `localStorage ops_active_workspace_id` for the **workspace hint**. The workspace hint may live in `sessionStorage` on `:5178` (e.g. `pay_active_org_id`) as UX only. Authorization remains path + One membership.

### 6.2 WARN: cookies on `localhost` are not port-scoped

RFC 6265 cookies are keyed by **host** (and path, Secure, __Host-), **not port**. On a developer laptop:

| Cookie | Set by | Also sent to |
|--------|--------|----------------|
| `lazuar_auth` | Hub API `:8080` / read by ops `:3003` (`credentials: "include"`) | **Any** `http://localhost:*` path that matches, including **`:5178`**, **`:8081`**, **`:5175`**, if the SPA or a proxy uses `credentials: "include"` |
| `lazuar_admin_auth` | Hub platform | same host `localhost` |
| `lazuar_login_sess` | login BFF `:5175` (`SESSION_COOKIE_NAME`) | **`:5178`** too, if a credentialed fetch is made |
| `lazuar_login_csrf` | login `:5175` | same |
| `lazuar_active_tenant` | `lazuar-app` | UX cookie; One does not authorize Pay from it |

If merchant `fetch(payApi, { credentials: "include" })`:

1. The browser will attach **all** `localhost` cookies that match Path, including Hub `lazuar_auth` if Hub was used on this machine.
2. Pay CORS today does **not** `AllowCredentials`, so a credentialed fetch **fails CORS** (the browser hides the response). Someone will “fix” that by adding `AllowCredentials` on Pay. Then Pay might start parsing cookies “to help.” That is how you rebuild Hub `OnMessageReceived`.
3. A `Set-Cookie` from Pay `:8081` is stored for host `localhost` and will be sent to One login, ops, and this SPA.

**Rules:**

- v1 merchant fetch: **no** `credentials: "include"`. Authorization header only.
- Pay CORS: **keep** credentials off until there is a written BFF design with **distinct cookie names**, `__Host-` + `Secure` + `Path`, and **different hostnames** in staging (not N ports on `localhost`).
- Never name a Pay cookie `lazuar_auth`, `lazuar_login_sess`, or `lazuar_active_tenant`.
- Do not read `document.cookie` in the merchant SPA to “find the login session.”

This is the same class of foot-gun as One issue 010 (login BFF cookies) and Hub cookie-vs-Bearer (011 `09-old-pay.md`). 012/02 §7.3: One’s resource server does not read cookies for `/api/v1/me`. Pay should not start.

### 6.3 BFF later (production hardening, not v1)

A confidential `type: web` app with `client_secret` held by a **same-origin** BFF (Vite proxy in dev, edge in prod) would:

- Keep tokens off the JS heap (XSS).
- Need a cookie on the **merchant hostname** (staging `pay.example`, not `localhost:5178` next to five other apps).
- Still register via One `POST /tenants/{id}/apps` with `type: "web"` (R3 confidential sample) + allowlist.
- **Never** put `client_secret` in `VITE_*`.

012/02 §8.2: confidential secret only if `type=web`. P10 does not require a BFF to call the SPA “connected.” `lazuar-app` itself is still sessionStorage on HEAD `0f79fe4`. Merchant matching app is the honest copy.

XSS note: sessionStorage + `pickApiBearerToken` means a script on `:5178` can `fetch` Pay as Ada. Mitigations later: short access TTL, silent renew, CSP (§8), then BFF. Do not delay v1 dogfood on a BFF rewrite.

### 6.4 Where tokens must **not** live

| Place | Why not |
|-------|---------|
| `localStorage` as a fork from app | Survives tab; still XSS; inconsistent with the template we copy |
| Pay `session` table / cookie JWT | NP-XX-007; 012/02 §7 |
| Query string | One issue 081 class; login session tokens on URLs |
| Logs, Sentry breadcrumbs, `console.log(user)` | 012/02 §10: never log Authorization |
| Git, `.env` committed | `.env` gitignored; `WRITE_ENV=1` writes local `.env` only |
| `VITE_ONE_ACCESS_TOKEN` | Engineer-exported backend dogfood is 012’s **C#** path, not a SPA env |

---

## 7. Types: later `@repo/pay-types-ts` from `pay-spec`; never Hub `api-types-ts`

### 7.1 Three contracts, three names

| Package | Repo | Host | Merchant imports? |
|---------|------|------|-------------------|
| `@repo/pay-spec` → future **`@repo/pay-types-ts`** | Pay | `:8081` `/v1` | **Yes, later**, for whoami / catalog / keys / payments / receipts |
| One `@repo/api-spec` → `@repo/api-type-ts` (singular “type”) + `@lazuar/one-client` | One | `:8080` `/api/v1` | **Yes, if SPA calls One** (§5). Workspace import. |
| Hub `@repo/api-spec` → **`@repo/api-types-ts`** (plural “types”) | Pay monorepo, **old** host `:8080` `/api/v1` | **Never** in `lazuar-pay-merchant` |

012/04: “If someone adds `pay-types-ts` later, name it **`@repo/pay-types-ts`**. Do **not** reuse `@repo/api-types-ts`. Do **not** reuse `@repo/api-type-ts`.” Compile with a **Pay** task (`task pay:types`), **not** a new step inside `task gen` / Hub honesty allowlist.

Ops `lib/api-client.ts` today:

```ts
import type { paths, components } from "@repo/api-types-ts";
export const client = createClient<paths>({
  baseUrl: API_URL,
  fetch: (input, init) => fetch(input, { ...init, credentials: "include" })
});
```

That `paths` object contains `/one/auth/me`, `/admin/commerce/products`, `/lhdn/documents/{internalId}`. It will **never** contain `/v1/whoami`. Importing it into merchant “to save time” is how you type-check Hub routes against Pay and then “just add login.”

### 7.2 When to generate `@repo/pay-types-ts`

012/04 §6 said **not now** because no frontend talked to `:8081`. P60: “Generate `@repo/pay-types-ts` only when that UI calls `/v1` for real.”

Trigger for this program: the PR that makes `lazuar-pay-merchant` call `GET /v1/whoami` (and then money routes as they land in `pay-spec`). Until then, a hand-written `WhoamiResponse` type in the SPA is acceptable and smaller than a generate pipeline. Do **not** generate a package that only wraps `GET /v1/health` so turbo has another job (012/04 anti-goal).

`packages/pay-spec/main.tsp` on this SHA already has `WhoamiResponse`, `WhoamiTenant`, `OrgReadyResponse`, checkout fixture models. `task pay:spec` emits OpenAPI to `packages/pay-spec/dist/openapi.yaml` (gitignored). A later `openapi-typescript` step should read **that** file, not Hub `packages/api-spec`.

Do not hook `pay-spec` into `task contracts:honesty` / `honesty-allowlist.yaml`. Those scrape `apps/lazuar-api`.

### 7.3 Whoami JSON the SPA should type against

Pay projection (`WhoamiResponse.cs` + `pay-spec`), snake_case on the wire:

```json
{
  "user_id": "<zitadel-sub>",
  "email": "ada@acme.test",
  "is_platform_admin": false,
  "active_org_id": "<guid-if-hint-matched>",
  "tenants": [
    {
      "id": "<guid>",
      "slug": "acme",
      "name": "Acme",
      "role": "owner",
      "status": "active"
    }
  ]
}
```

`tenants[].id` **is** `org_id`. `role` is One `owner` \| `admin` \| `member` (or absent). It will **not** be `VIEWER`, `ADMIN`, `MEMBER`, `SUPER_ADMIN`, or Hub `CLIENT`. Do not write a TS union that includes Hub roles “for compatibility.”

Email may be null if the access token omitted profile. **Do not** send `id_token` to fill it (012/02 §2.2; One 018-design/07).

`is_platform_admin` true means One `Platform:AdminEmails`. Merchants in this UI should not get a staff-only chrome. Do not use it as “can paste keys.”

### 7.4 Two OpenAPI objects, two `baseUrl`s — never a union `paths`

If merchant imports both `@repo/pay-types-ts` and One’s types, keep **two clients**:

```text
pay  = createClient<PayPaths>({ baseUrl: "http://localhost:8081", getAccessToken })
one  = createClient<OnePaths>({ baseUrl: "http://localhost:8080/api/v1", getAccessToken })
```

Same picker, different hosts, different `paths`. Unioning the two OpenAPIs into one `paths` is 012/04’s failure mode: the client forgets which host to call and Hub leftovers sneak in.

---

## 8. Production hardening: `strictPort`, env, CSP later, no secrets in SPA besides public `client_id`

### 8.1 Already true on this SHA (keep)

- `strictPort` on dev **and** preview.
- Dual-pin 5178 in `package.json` + `vite.config.ts`.
- Preview 4178, not a silent steal of 5178.
- Host `0.0.0.0` (`--host=0.0.0.0` / `host: true`) for Docker/mprocs.
- `.env.example` points at Pay `:8081`, comments “Never Hub :8080. Never point lazuar-ops here.”
- Pay CORS allowlist is exact origins, no `*`, no ops `:3003`.
- No `@repo/api-types-ts` dependency.
- README forbids password form and `id_token` as Bearer.

### 8.2 Env discipline

| Class | Examples | Where |
|-------|----------|--------|
| Public SPA | `VITE_PAY_API_URL`, `VITE_ZITADEL_*`, `VITE_ONE_API_URL` | merchant `.env` gitignored; `.env.example` committed **without** `client_id` filled, or with a comment “from seed / POST apps” |
| Pay server | `One__BaseUrl`, later `ONE_API_KEY` (`lzr_sk_`), webhook HMAC | `apps/lazuar-pay/.env` / user-secrets / vault. **Not** `VITE_*`. |
| One ops | `ZITADEL_PAT`, login-client PAT, FGA admin, masterkey, pepper | **One repo only** |
| Forbidden in merchant | any PAT, `client_secret`, `lzr_sk_`, Hub `Jwt:Secret`, Ada’s password | PR fail |

Staging builds: follow One’s split (`deploy/staging/spa.env.app.staging.example`) — no localhost authority/redirects in a staging bundle.

`localhost` vs `127.0.0.1`: list **both** on Pay CORS (already), One CORS (when SPA→One), redirect URIs if Ada actually opens the 127 origin. Do not assume they are the same origin; they are not.

### 8.3 CSP later (not a v1 dogfood blocker)

When this origin is served as a real site:

| Directive | Intent |
|-----------|--------|
| `default-src 'self'` | |
| `script-src 'self'` | no inline as a goal; Vite hashes in prod |
| `connect-src 'self' https://pay-api… https://one-api… https://issuer…` | Pay `/v1`, One `/api/v1` if direct, Zitadel token/authorize |
| `form-action 'self'` | no password form posting off-origin; there is no password form |
| `frame-ancestors 'none'` | |
| `base-uri 'self'` | |

Do not set `connect-src *`. Do not add Hub `:8080` “just in case.” Do not add `:3003`.

CSP is **later** because local Vite HMR needs `unsafe-eval` / ws to the Vite port; tightening is a production nginx/Caddy concern (paper 10). Record the intent here so a “temporary `*`” does not ship.

### 8.4 XSS / token theft

sessionStorage + Bearer is XSS-sensitive (One app already says so). v1 mitigations: copy picker tests, short-lived access tokens, silent renew, no `innerHTML` from receipt HTML, no `eval` of webhook payloads in the SPA. Production: CSP + consider BFF (§6.3). Do not store `id_token` in a second key “for display name.”

### 8.5 What “production-ready” means for **this** origin (not Hub parity)

Aligned with 013 paper 01’s bar (production-ready ≠ feature-complete Hub):

- Ada can complete the dogfood sentence **without** opening ops `:3003` or admin `:5173`.
- OIDC is registered on One (app object + allowlist), not a Console screenshot.
- Bearer to Pay whoami works; `id_token` 401s honestly.
- VIEWER-equivalent cannot paste keys; chrome matches API.
- No secrets in the JS bundle except public `client_id`.
- `strictPort` / pinned origin in compose and Taskfile.
- Health remains unauthenticated.
- Checkout stays a **different** origin without Zitadel.

Not required for this origin to be “prod”: Hub dashboard P&L, LHDN, WhatsApp, chat, credits, quotes, Hub webhooks UI, `@repo/api-types-ts` honesty green.

### 8.6 Deploy shape (pointer, not a compose rewrite)

`task pay:merchant` is a **host Vite**. Compose still points at old `apps/lazuar-api` (`apps/lazuar-pay/README.md`). Swapping compose to 8081 + 5178 is paper 10 / 03. Do not serve merchant static files from the C# host as a sneaky same-origin BFF in v1; keep **separate origin** (011/02: “Pay is a **separate origin**”). Same-origin later is a BFF decision, not a “mount SPA on 8081” convenience that collapses CORS math.

---

## 9. Anti-goals

| # | Anti-goal | Why | IDs |
|---|-----------|-----|-----|
| 1 | Retarget `lazuar-ops` `VITE_API_URL` at 8081 | Hub types, Hub login, Hub routes. Pay CORS already refuses 3003. | P60, F10 in 012/10, NP-API-004 |
| 2 | Password form / forgot / reset / verify on `:5178` | Second IdP. Ops `LoginPage` + `pages/ForgotPasswordPage.tsx` are the template of the bug. | NP-ONE-005, NP-XX-007 |
| 3 | Treat `:5175` as Pay homepage | Login is a redirect target. | NP-ONE-005 |
| 4 | Ship merchants to `:3005` or `:5173` | Stock Login V2 **and** Hub admin collide on 3005; One admin is staff. | NP-ONE-005, NP-XX-018 |
| 5 | Send `id_token` as Bearer; fall back when access is opaque | Closed One issue 002 / M2M-14. Copy `pickApiBearerToken`. | NP-ONE-003 |
| 6 | Console-only `client_id` | Redirects drift; leftover opaque tokens. | NP-ONE-001, NP-ONE-004, P10.3 |
| 7 | Import `@repo/api-types-ts` / `openapi-fetch` Hub `paths` | Type-checks `/one/auth/me` against the wrong host. | 012/04, P60 |
| 8 | Grow `pay-spec` with `/one/*`, `/tenants`, `/lhdn`, `/admin/commerce` | Re-implements Hub + One at the contract layer. | NP-XX-007, NP-XX-014, 012/04 |
| 9 | Pay-as-BFF re-export of One tenants/invites | Same. Whoami projection is the only identity façade Pay owns. | §5 |
| 10 | Cookie session (`lazuar_auth` or new name) on localhost | Cookies are not port-scoped. Hub cookie will ride along. | 012/02 §7, §6 of this paper |
| 11 | `credentials: "include"` on merchant `fetch` | Activates the cookie foot-gun; ops `api-client.ts` is the negative example. | §6 |
| 12 | Authorize with `X-Tenant-Id` / `X-Lazuar-Tenant-Id` alone | Hint only. Path + membership. | NP-ONE-007 |
| 13 | Parse Zitadel project-role claims | Role SoT is `/me` + `authz/check`. | NP-XX-024, NP-ONE-008 |
| 14 | Rebuild LHDN / tax invoices / VALID / TIN-at-checkout | Homemade tax. | NP-XX-001, NP-XX-002, NP-XX-003 |
| 15 | Rebuild WhatsApp dunning / chat / Hub CRM / quotes-as-tax / credits ledger / Hub pricing | Not on the dogfood sentence; several are refuse. | NP-XX-004, ADR 023, NP-SOON-001 |
| 16 | Rebuild Hub TeamPage with a **VIEWER** invite option | One cannot store VIEWER. | NP-ONE-021, C24, 012/07 §10 |
| 17 | Mark NP-ONE-021 `done` because `check(member)` passed | Dummy ready is “has the tenant,” not “cannot charge.” | C24, 012/07 |
| 18 | Hold PAT / FGA admin / login-client PAT in merchant or Pay | NP-ONE-020. Seed lives in One. | NP-XX-017 |
| 19 | Put `lzr_sk_` or `client_secret` in `VITE_*` | Public bundle. | NP-ONE-020 |
| 20 | OIDC on `:5179` checkout | Buyers are not One humans. Paper 05. | NP-CHK-007, NP-XX-013 |
| 21 | Merge merchant + checkout into one Vite app | Different auth planes, different ports, dual-pin comments exist to stop this. | 013 README |
| 22 | Silent Vite port hop | `strictPort` is the lock. | §2.2 |
| 23 | Hammer `/v1/whoami` or One `/me` from a table poll | JIT writes; not 429 by default. | NP-ONE-006 note |
| 24 | Title receipt Tax Invoice / print UUID / print VALID | Honesty lock. | NP-DOC-003, NP-XX-003 |
| 25 | Five gateway adapters in the paste UI on day one | 011: Stripe **or** CHIP/Billplz. | NP-GW-003, NP-SOON-008 |
| 26 | Wait on npm publish of `@lazuar/one-client` | Workspace import. | NP-XX-021 |
| 27 | Create a Zitadel human per cardholder from this UI | Wrong plane. | NP-XX-013 |
| 28 | Second `organizations` table to make the picker easier | One tenant id is `org_id`. | NP-XX-014 |
| 29 | Design the hosted checkout page in this app | Paper 05. | NP-CHK-005 |
| 30 | Flip 011/11 cells to `done` from this analysis | 013 README. | — |

---

## 10. Open questions

These are unresolved on purpose. Do not pretend the analysis closed them.

1. **First-party seed vs per-tenant `POST /apps` as the committed happy path for `client_id`.**  
   NP-ONE-001 allows both. Way A (One seed like `lazuar-app`) is the right **product** identity (one public client for Lazuar Pay merchant). Way B (Ada’s workspace registers the SPA) is the right **dogfood** if One does not want to touch `seed-platform-spa-clients.sh` yet. Who runs the One-side allowlist + CORS change? That is a both-sides ticket, not a Pay-only PR.

2. **When does One grow a `viewer` membership role?**  
   Until then NP-ONE-021 cannot be literally true. This paper follows 012/07 Option A: no Viewer in v1 UI; keys require `admin`/`owner`; charge/refund allow `member`; do not fake a chip. If product wants “only admin moves money,” that is Option B and **not** what 011’s MEMBER-sees-ops sentence says. Needs a product call, not a Hub `VIEWER` dropdown.

3. **Invite accept: in-merchant vs deep-link `lazuar-app`.**  
   011/02 allows both. In-merchant needs One CORS on 5178 + accept-invite UI. Deep-link is less code and keeps copy-link format stable (`NP-ONE-012`). Dogfood of NP-ONE-022 (second engineer) can use either. Prefer deep-link for week one if CORS-on-One is blocked; prefer in-app if merchant is supposed to be the only Ada URL.

4. **Does v1 merchant include a subscribers list screen, or only payments + receipt?**  
   011 `01` lists subscribers next to payments. Assigned slice listed payments list + open receipt. Recommendation: payment row links to the seat it created if `NP-FUL-003` returns it; do not port `SubscribersPage.tsx`. Confirm in paper 07 (fulfillment).

5. **CSP and BFF for production XSS.**  
   sessionStorage is the v1 copy of `lazuar-app`. When Pay merchant is on a real hostname, is a confidential BFF in scope for 013 or a later program? This paper says **later**. Paper 01/08/10 may tighten.

6. **Preview origin 4178.**  
   If anyone dogfoods `vite preview`, it is a **different origin** and needs its own redirect URI. Recommend: do not. Preview is a static-hosting check, not a login check.

7. **`127.0.0.1:5178` vs `localhost:5178`.**  
   Pay CORS already lists both. One CORS and `redirect_uris` must match whatever Ada types. Pick **localhost** as the documented URL (`README` already does) and still list 127 twins to avoid issue 077.

8. **Workspace switcher persistence.**  
   Ops used `localStorage ops_active_workspace_id` + Hub entitlements. New: `sessionStorage` hint + whoami `tenants[]`. Is a full-page reload allowed to forget the pick (sessionStorage yes, tab-close yes)? Fine for dogfood. Staging may want a remembered org on the hostname — still a hint, still not `X-Tenant-Id` authz.

9. **React Query / router / CSS.**  
   Merchant has none. `lazuar-app` has router + `react-oidc-context`. Steal **that** stack, not ops’ Tailwind/shadcn/55 `ui/` files. Exact visual design is not this paper. Do not import ops `components/ui`.

10. **Whoami vs One `GET /me` both from the SPA.**  
    If §5 direct-One is in, the SPA might call both. Recommendation: session chrome from **Pay whoami** (so the money host’s projection is what the money UI trusts); create/invite against **One**. Refresh whoami after One writes. Do not cache whoami `role` as a capability for money POSTs — Pay re-checks `authz` on the route.

11. **Staging One CORS + allowlist ownership.**  
    Empty `App:CorsOrigins` and empty `REDIRECT_ALLOWLIST` **fail boot** on One in strict env. Adding Pay’s production origin is One ops. 013 paper 08 (One in prod) should list the exact origin strings once they exist. This paper only locks the **local** ones.

12. **Whether `GET /v1/orgs/{orgId}/ready` is shown in the UI.**  
    It is a dummy to prove `check(member)`. A “workspace ready” badge is optional. Do not build a Hub dashboard on it.

---

## 11. Evidence index (paths opened)

### Pay repo (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay`) — SHA `6f866ff0`

- `apps/lazuar-pay-merchant/package.json`, `vite.config.ts`, `index.html`, `README.md`, `.env.example`, `src/{main.tsx,App.tsx,App.css,index.css}`, `tsconfig*.json`
- `apps/lazuar-pay-checkout/{vite.config.ts,src/App.tsx,.env.example}` (contrast)
- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`, `One/{WhoamiEndpoints,WhoamiResponse,OneClient,OneMeMapper,Bearer}.cs`, `README.md`, `.env.example`, `tests/Lazuar.Pay.Tests/CorsTests.cs`
- `packages/pay-spec/main.tsp`, `README.md`
- `Taskfile.yml` `pay:merchant`
- `pnpm-workspace.yaml`
- `apps/lazuar-ops/package.json`, `vite.config.ts`, `src/main.tsx`, `src/App.tsx`, `src/lib/api-client.ts`, `src/components/{LoginPage,Sidebar,EmptyWorkspaceState,PricingPage,OpsChatWorkspace}.tsx`, `src/pages/{ForgotPassword,ResetPassword,VerifyEmail}Page.tsx`, every `src/modules/**` file listed in §3.4, `src/hooks/*`, `src/lib/*`, `src/types/chat.ts`
- `plans/011-new-lazuar-pay/{01-product,02-one-integration,03-first-slice,11-checklist,12-first-slice-tracker}.md`
- `plans/012-one-to-pay/{02-one-authn-tokens,04-pay-spec-contract,05-local-topology,06-tenant-org,07-authz-roles,10-dogfood-and-tests}.md`
- `plans/012-one-to-pay/checklists/{p10-spa-oidc,p60-old-frontends,c24-viewer-honesty,decisions}.md`
- `plans/013-prods/README.md`

### One repo (`/Users/akmalfirdaus/Code/lazuar/lazuar-one`) — SHA `0f79fe4`

- `apps/lazuar-app/src/auth/{oidcConfig.ts,bearerToken.ts,bearerToken.test.ts,RequireAuth.tsx}`, `src/pages/{LoginPage,CallbackPage}.tsx`, `src/App.tsx`, `src/main.tsx`, `.env.example`
- `examples/vite-spa/src/{bearerToken.ts,oidc.ts,App.tsx}`, `examples/oidc-spa-notes/README.md`
- `apps/lazuar-login/.env.example` (`REDIRECT_ALLOWLIST`)
- `apps/lazuar-api/src/Lazuar.One.Api/appsettings.Development.json`, `Program.cs` CORS
- `scripts/seed-platform-spa-clients.sh`
- `apps/lazuar-docs/docs/recipes/register-oidc-app.md`

---

## 12. What “done” looks like for this slice (analysis bar, not a code bar)

This paper is done if a later implementer can, without re-deriving the origin:

1. Grow **`apps/lazuar-pay-merchant`** on **5178** `strictPort`, not ops, not admin, not login-as-home, not checkout.
2. Wire OIDC code+PKCE like `lazuar-app`, register via One `POST /tenants/{id}/apps` or a One seed, add `:5178` to `REDIRECT_ALLOWLIST` (and One CORS if the SPA calls One).
3. Send **only** JWT `access_token` via a copied `pickApiBearerToken` to Pay `GET /v1/whoami`.
4. Build **only** the six dogfood screens; leave Hub LHDN, chat, WhatsApp, CRM, quotes-as-tax, credits, password pages, and the ops route catalog on the refuse list.
5. Call **One** for tenants/invites (or deep-link `lazuar-app`); call **Pay `/v1`** for money; never Hub types.
6. Store tokens in **sessionStorage**; never `credentials: "include"` on localhost; never a Pay password cookie.
7. Hide or 403 key-paste for non-admin; do not ship a Viewer invite that One cannot store.

Implementation is a later checklist program, not this file.
