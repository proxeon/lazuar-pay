# 02 — Merchant Vite (:5178) after Aura shell

**Date:** 26 August 2026  
**Type:** Uncondensed evaluation. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) cells. **Not** a retarget of `lazuar-ops` at 8081. **Not** a design of checkout Vite internals (sibling `03`), except the `/c/{token}` URL this SPA mints.  
**Slice:** newest `apps/lazuar-pay-merchant` after the 018 Aura-style shell. Bugs and gaps versus the live Pay host on **8081**. For each finding: evidence, impact, how to solve (analysis, not a patch).  
**Parent program:** [`plans/019-evals`](./README.md). Live files on this SHA are authority. [014-evals/02-merchant-frontend.md](../014-evals/02-merchant-frontend.md) and [016-adapters-check/02-merchant-frontend.md](../016-adapters-check/02-merchant-frontend.md) are background; both described a single `WorkspacePage.tsx` that **does not exist** on this SHA.

Standing law (do not relitigate):

| Lock | Meaning |
|------|---------|
| New merchant UI is **this app** on **5178** | Do not retarget `lazuar-ops` (`:3003`) at 8081. P60. |
| Never put secrets in Vite | No `sk_live_`, CHIP Bearer, AES wrap key, `ZITADEL_PAT`, `client_secret`, `lzr_sk_` in `VITE_*`. Public surface is Pay URL, One URL, checkout origin, OIDC `client_id` / authority / redirect. |
| Bearer is `access_token` | Never `id_token`. Picker: `pickApiBearerToken`. |
| VIEWER honesty | One has no membership role `viewer`. Roles are `owner` / `admin` / `member`. Do not fake a Viewer chip. |
| IsolationTests Vite ban | `package.json` must not depend on Hub `@repo/api-types-ts`. Merchant `locks.test.ts` also bans `@repo/aura-ui` and `lazuar-ops`. |

---

## Coordinates

Recorded at write time. Re-open files on a later SHA before treating a line as still true.

| | |
|---|---|
| Repo | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` |
| Branch | `feat/018-merchant-shell` (task assignment; `.git/HEAD` not re-read via a shell in this pass) |
| HEAD named by the 019 index | `9f04ad58` — `fix(pay-ui): match receipts table to pay-link chrome` |
| Today | 26 August 2026 |
| Merchant origin | `http://localhost:5178` (`strictPort`) |
| Pay host | `http://localhost:8081` (`VITE_PAY_API_URL`) |
| One HTTP | `http://localhost:8080/api/v1` (`VITE_ONE_API_URL`) — workspace create only |
| OIDC issuer | `http://localhost:8085` (`VITE_ZITADEL_AUTHORITY`) |
| One login UI (README claim) | `:5175` |
| Checkout origin this SPA mints | `http://localhost:5179` (`VITE_CHECKOUT_ORIGIN`) |
| Preview | `http://localhost:4178` (`strictPort`) |

019 index ([README.md](./README.md)) recorded the same HEAD at analysis start. This paper re-opened the live tree rather than trusting 014 (`ee2db8e5`, one `WorkspacePage`) or 016 (`c621ceba`, same one page, PEM in `<input>`, `POST /v1/checkouts`, `keys ${status}`).

---

## Files opened

Every path below was opened (entire file, or the cited region) in this pass. Historical 014/016 merchant papers were opened as **background** and then disagreed with.

### Merchant app (authority for this slice)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/package.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/vite.config.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/vitest.config.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/tsconfig.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/tsconfig.app.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/tsconfig.node.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/index.html`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/.env.example`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/scripts/register-spa.sh`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/public/favicon.svg`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/main.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/App.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/index.css`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/locks.test.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/auth/bearerToken.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/auth/bearerToken.test.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/auth/oidcConfig.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/auth/RequireAuth.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/layout/DashboardChrome.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/layout/nav.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/layout/OrgLayout.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/layout/PageHeader.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/layout/WorkspaceSwitcher.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/lib/homePath.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/lib/http.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/lib/oneApi.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/lib/payApi.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/lib/processors.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/lib/roles.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/lib/sessionKeys.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/lib/staffDisplay.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/lib/staffDisplay.test.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/CallbackPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/CreateWorkspaceForm.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/CreateWorkspacePage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/HomePage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/LoginPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/org/CreateWorkspacePage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/org/OverviewPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/org/PaymentsPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/org/ReceiptsPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/ui/components/app-sidebar/app-sidebar.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/ui/components/app-sidebar/index.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/ui/components/app-sidebar/location-header.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/ui/components/app-sidebar/nav-item.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/ui/components/app-sidebar/types.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/ui/components/app-sidebar/user-menu.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/ui/components/avatar.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/ui/components/button.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/ui/components/card.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/ui/components/dialog.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/ui/components/dropdown-menu.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/ui/components/input.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/ui/components/label.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/ui/components/select.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/ui/components/table.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/ui/components/textarea.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/ui/hooks/use-mobile.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/ui/lib/utils.ts`

`src/pages/WorkspacePage.tsx` and `src/App.css` were **looked for and are absent**. `dist/assets/index-DuegEQIu.js` is listed on disk (gitignored leftover).

### Pay host (contract this SPA claims to call)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/appsettings.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/appsettings.Development.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayErrors.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs` (`PutGatewayRequest`, PUT/GET/List, `GatewayJson`, `TestGatewayJson`)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/CreatePaymentLinkRequest.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiResponse.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/OrgReadyEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/Bearer.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneMeMapper.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneMeResponse.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneOptions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipWebhook.cs` (`ImportFromPem`)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Razorpay/RazorpayHosted.cs` (first 40 lines; `TrySplit` cited historically from 016, live join still client-side)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Billplz/BillplzHosted.cs` (`Pay:PublicBaseUrl` grep)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` (callback-base 400 mapping, grep)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` (first 80 lines — merchant no longer POSTs this)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CreateCheckoutRequest.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs` (first 120 lines)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/README.md` (merchant/checkout ports, BYOK, Test)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Catalog/CatalogTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Money/PaymentQueryTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/WhoamiTests.cs` (first 80 lines)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/CorsTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`

### Adjacent (URL shape / workspace / CI / spec / papers)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-checkout/src/App.tsx` (tokenFromPath `/c/{token}` only)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/main.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/Taskfile.yml` (`pay:merchant`)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/pnpm-workspace.yaml`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/.gitignore`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/.github/workflows/ci.yml` (`pay` job)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/019-evals/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/018-evals/001-evals.md` (product paper; not merchant source)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/014-evals/02-merchant-frontend.md` (historical)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/016-adapters-check/02-merchant-frontend.md` (historical; PEM input, `POST /v1/checkouts`, `keys 400`)

Not opened as product design: `lazuar-portal`, Hub `lazuar-admin`, One login source, webhook crypto internals beyond CHIP `ImportFromPem` and Billplz `PublicBaseUrl`. Deep rail HTTP, TypeSpec paper, checkout poll internals belong to other 019 files.

---

## What exists (routes, auth, pages, API client)

### Package, port, scripts

`apps/lazuar-pay-merchant/package.json` on this SHA:

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

Dependencies now include the 018 chrome stack **and** the OIDC/router stack:

- `react` `^19.2.8`, `react-dom` `^19.2.8`
- `oidc-client-ts` `^3.4.1`, `react-oidc-context` `^3.3.0`, `react-router-dom` `^7.15.0`
- Radix: avatar, dialog, dropdown-menu, label, select, slot
- `class-variance-authority`, `clsx`, `lucide-react`, `tailwind-merge`

Dev: `@tailwindcss/vite` `^4.3.0`, `tailwindcss` `^4.3.3`, `tw-animate-css`, `vite` `^8.2.0`, `vitest` `^3.2.4`, `oxlint`, TypeScript `~6.0.2`.

**Still absent (must stay absent):** `@repo/api-types-ts`, `@repo/aura-ui`, `lazuar-ops`, `openapi-fetch`, `@tanstack/react-query`, Express, cookie session. `locks.test.ts` greps the package file for the first three. Host `IsolationTests.Vite_apps_do_not_use_hub_types` only greps `@repo/api-types-ts`.

Workspace membership: root `pnpm-workspace.yaml` is `apps/*` (so this package is included without being named). `Taskfile.yml`:

```133:136:Taskfile.yml
  pay:merchant:
    desc: Merchant Vite shell on http://localhost:5178 (not lazuar-ops)
    cmds:
      - pnpm --filter lazuar-pay-merchant dev
```

### Vite dual-pin (unchanged, still correct)

```1:19:apps/lazuar-pay-merchant/vite.config.ts
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Dual-pinned with package.json `vite --port=5178`.
// strictPort: fail loud if 5178 is busy — never silently steal login :5175 or checkout :5179.
export default defineConfig({
  plugins: [react(), tailwindcss()],
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

Tailwind is a Vite plugin now (014 had only `react()`). Port pins are the same. `index.html` title is still `Lazuar Pay — merchant`. Favicon is the Lazuar mark (`public/favicon.svg`).

`vitest.config.ts` is still **node**, not jsdom:

```1:7:apps/lazuar-pay-merchant/vitest.config.ts
import { defineConfig } from 'vitest/config'

export default defineConfig({
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
})
```

There is no Playwright. There is no component test of `GatewayPage` / `CheckoutsPage` / `DashboardChrome`.

### Env (public only)

```1:15:apps/lazuar-pay-merchant/.env.example
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

# Buyer SPA origin for minted /c/{token} links. Not a secret.
VITE_CHECKOUT_ORIGIN=http://localhost:5179
```

014’s `.env.example` had no checkout origin. 016’s `WorkspacePage` hardcoded `http://localhost:5179`. Live code still **falls back** to that string if the env is unset (`CheckoutsPage.checkoutOrigin`). `.env` is gitignored; `!.env.example` is kept.

`scripts/register-spa.sh` POSTs One ` /tenants/{TENANT_ID}/apps` with `type: "spa"`, `redirect_uris: [http://localhost:5178/callback]`, `post_logout_redirect_uris: [http://localhost:5178/]`. It refuses a returned `client_secret`. `WRITE_ENV=1` writes only `VITE_ZITADEL_CLIENT_ID`. It does not register `127.0.0.1:5178` (README tells the human to add that twin to One `REDIRECT_ALLOWLIST` separately). It does not write `VITE_CHECKOUT_ORIGIN`.

### Routes (`App.tsx`)

```15:53:apps/lazuar-pay-merchant/src/App.tsx
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
            <OrgLayout />
          </RequireAuth>
        }
      >
        <Route index element={<Navigate to="overview" replace />} />
        <Route path="overview" element={<OverviewPage />} />
        <Route path="new" element={<OrgCreateWorkspacePage />} />
        <Route path="gateway" element={<GatewayPage />} />
        <Route path="checkouts" element={<CheckoutsPage />} />
        <Route path="payments" element={<PaymentsPage />} />
        <Route path="receipts" element={<ReceiptsPage />} />
      </Route>
    </Routes>
  )
}
```

There is **no** splat 404. Unknown paths render a blank router outlet. There is no `/health` probe page (013/014 health-probe `App.tsx` is gone). There is no `WorkspacePage`.

Nav labels vs URL leafs (`layout/nav.ts` + `OrgLayout.titleFromPath`):

| URL | Chrome title | Sidebar |
|-----|--------------|---------|
| `/o/:id/overview` | Overview | Overview |
| `/o/:id/gateway` | Processor | Processor |
| `/o/:id/checkouts` | Pay links | Pay links |
| `/o/:id/payments` | Payments | Payments |
| `/o/:id/receipts` | Receipts | Receipts |
| `/o/:id/new` | Create workspace | (not in nav; switcher item) |

The route is still `checkouts`. The words on screen are **Pay links**. That is 018 naming, not a second resource.

### Auth (OIDC PKCE, bearer, RequireAuth)

`main.tsx` wraps the tree in `AuthProvider {...getOidcConfig()}` then `BrowserRouter`.

```8:36:apps/lazuar-pay-merchant/src/auth/oidcConfig.ts
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

- `response_type: 'code'` is authorization-code. `oidc-client-ts` v3 default is PKCE on (`disablePKCE` not set). There is no `client_secret` in env or config. That is a public SPA.
- Tokens live in **sessionStorage**, not cookies. Comment in this file and in `payApi.ts`: localhost cookies are not port-scoped.
- `automaticSilentRenew: true` with **no** `silent_redirect_uri`. Silent renew uses `redirect_uri` (`/callback`) in an iframe. `CallbackPage` is a full-app `<Navigate>`. See Bugs.
- There is no `loadUserInfo: false` override; library default is to hit UserInfo. `staffDisplay` can still use `id_token` profile `email` / `name` if whoami omits them.
- Login UI copy and README say One `:5175`. This config never navigates to `:5175`. `signinRedirect()` goes to **Zitadel `:8085`**. See Gaps.

Bearer picker — JWT `access_token` only, never `id_token`, never opaque, never JWE:

```10:17:apps/lazuar-pay-merchant/src/auth/bearerToken.ts
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

`bearerToken.test.ts` locks: signed-out → undefined; JWT access returned and is not the companion `id_token`; opaque / empty / JWE access → undefined even when `id_token` is a JWT.

`RequireAuth` gates on `auth.isAuthenticated` (OIDC user present), **not** on `pickApiBearerToken`:

```5:37:apps/lazuar-pay-merchant/src/auth/RequireAuth.tsx
export function RequireAuth({ children }: { children: ReactNode }) {
  const auth = useAuth()
  const location = useLocation()

  if (auth.isLoading) {
    return <p>Checking session…</p>
  }

  if (auth.error) {
    return (
      <div role="alert">
        <p>{auth.error.message}</p>
        <button type="button" onClick={() => void auth.signinRedirect()}>
          Retry login
        </button>
      </div>
    )
  }

  if (!auth.isAuthenticated) {
    return (
      <Navigate
        to="/login"
        replace
        state={{
          from: { pathname: location.pathname, search: location.search },
        }}
      />
    )
  }

  return children
}
```

Deep-link return path: `LoginPage` writes `sessionStorage` `lazuar-pay-merchant:returnTo` when `from !== '/'`; `CallbackPage` `takeReturnTo()` then `<Navigate to={returnTo ?? '/'} />`. `isSafeReturnPath` requires `startsWith('/') && !startsWith('//')`.

`LoginPage` is not a password form. It disables Sign in when `VITE_ZITADEL_CLIENT_ID` is empty. `locks.test.ts` greps `type=["']password["']`, `/one/auth/login`, `lazuar_auth`.

### Session keys and last workspace

```1:27:apps/lazuar-pay-merchant/src/lib/sessionKeys.ts
/** sessionStorage only — not an authz cookie. */
export const RETURN_TO_KEY = 'lazuar-pay-merchant:returnTo'
export const ORG_HINT_KEY = 'lazuar-pay-merchant:orgId'
// isSafeReturnPath / setReturnTo / takeReturnTo / getOrgHint / setOrgHint
```

```4:10:apps/lazuar-pay-merchant/src/lib/homePath.ts
/** Last used org if still a member, else first tenant. Empty → create workspace. */
export function dashboardPath(tenants: WhoamiTenant[]): string {
  if (tenants.length === 0) return '/workspaces/new'
  const hint = getOrgHint()
  const match = hint ? tenants.find((t) => t.id === hint) : undefined
  return `/o/${(match ?? tenants[0]).id}/overview`
}
```

`HomePage` calls `GET /v1/whoami` **without** `X-Lazuar-Tenant-Id`, then `dashboardPath`, then `setOrgHint` if the path is `/o/{id}/…`. `OrgLayout` and `WorkspaceSwitcher.openOrg` also `setOrgHint`. Whoami JSON `active_org_id` is **typed and unused** as a fallback (see Gaps).

Empty tenants → chrome-less `/workspaces/new`. If the user **already has** tenants and hits `/workspaces/new`, `CreateWorkspacePage` redirects into `/o/{id}/new` (create **inside** the dashboard shell).

### Staff email vs Zitadel sub

```11:26:apps/lazuar-pay-merchant/src/lib/staffDisplay.ts
/** Sidebar label. Prefer email/name from whoami or OIDC profile. Never a Zitadel numeric sub. */
export function staffDisplay(
  who: Whoami,
  user?: User | null,
): { name: string; email: string | null } {
  const profile = user?.profile
  const email =
    usable(who.email) ??
    usable(profile?.email) ??
    (profile?.preferred_username?.includes('@') ? usable(profile.preferred_username) : null)
  const name =
    usable(who.name) ??
    usable(typeof profile?.name === 'string' ? profile.name : undefined) ??
    (email ? email.split('@')[0]! : null)
  return { name: name ?? 'Signed in', email }
}
```

`usable` rejects empty and **all-digit** strings (Zitadel numeric `sub` / `user_id`). `staffDisplay.test.ts` locks: whoami email+name; OIDC profile when whoami email missing; numeric `user_id` alone → `{ name: 'Signed in', email: null }`.

`DashboardChrome` passes `{ ...staffDisplay(who, auth.user), roleLabel: tenant.role ?? 'member' }` into `AppSidebarUserMenu`. The trigger shows **name + role**, not email. Email is in the dropdown “Account” block. `user_id` is never rendered.

Host whoami does return `email` and `name` when One `/me` has them (`WhoamiTests.Whoami_maps_org_id_from_one_me` asserts both). TypeSpec `WhoamiResponse` **omits** `name`; live host and SPA include it.

### Role chrome (owner / admin / member vs VIEWER)

```1:4:apps/lazuar-pay-merchant/src/lib/roles.ts
/** One tenant roles. Pay: owner/admin write money; member is read-only. */
export function canWriteMoney(role: string | undefined | null): boolean {
  return role === 'owner' || role === 'admin'
}
```

No `viewer` branch. Unknown / missing role → `write === false`. `OrgLayout` computes `write` once and puts it on outlet context.

Host `MemberGate.RequireWriterAsync` is the same string test after a successful `authz/check member`:

```65:69:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs
        var role = who.Value.Tenants.FirstOrDefault(t => t.Id == orgId)?.Role;
        if (role is not ("owner" or "admin"))
        {
            return PayErrors.Status(403, "Forbidden", "Writer role required");
        }
```

UI hide is not the gate. API 403 is. `is_platform_admin` is unused on both sides for money writes.

Writer vs member honesty on this SHA (re-verified):

| Surface | owner/admin | member |
|---------|-------------|--------|
| Processor cards | Edit (except Test) | metadata only; “Member can see metadata. Cannot paste keys.” |
| Overview | “Paste keys” link | “Member can view. Cannot paste keys.” |
| Pay links | “Create pay link” | “Member cannot create charges.” Copy/Open still shown |
| Payments / receipts | list | list (member GET) |
| Create workspace (One POST) | form | **also the form** — not gated by `write` |

### Layout / mobile chrome

`OrgLayout` loads whoami with org hint, 404s membership as “Not a member of this org” **without** the sky rail (no switcher). On success it wraps `<Outlet>` in `DashboardChrome`.

`DashboardChrome`:

- Sky `AppSidebar` (copied presentational rail; comment still says “Ops day rail”).
- Header slot = `WorkspaceSwitcher` (not `AppSidebarLocationHeader`, which is dead Storybook chrome).
- Mobile `<768px`: sidebar starts closed; hamburger in the top bar; dim overlay `z-30`; aside `z-40`; `AppSidebar` auto-calls `onClose` on pathname change.
- Desktop `md:`: aside is `relative` and `translate-x-0` **even when `isOpen` is false** — there is no desktop collapse.
- Top bar `<h1>` is the section title. Page canvas **also** renders `PageHeader` `<h1>` (duplicate titles on Processor / Pay links / Payments / Receipts / Create workspace). Overview is the exception: chrome “Overview”, canvas = tenant name.
- User menu Settings → `navigate(/o/{orgId}/gateway)` (Processor), not a settings page.
- Sign out → `auth.signoutRedirect()` (Zitadel end-session + `post_logout_redirect_uri`).

`WorkspaceSwitcher` lists `who.tenants`, check-marks the current org, “Create workspace” → `/o/{orgId}/new`.

### API client

Two hosts. No generated client. No Hub types.

**Pay** (`payApi.ts`):

```1:1:apps/lazuar-pay-merchant/src/lib/payApi.ts
const payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'
```

- `getWhoami(token, orgHint?)` → `GET {payApi}/v1/whoami` with `Authorization` + optional `X-Lazuar-Tenant-Id`. 401 → thrown `'unauthorized'`. Other non-OK → `` `whoami ${status}` ``. **Does not** read `PayErrors.detail`.
- `payFetch(token, path, init?)` sets Bearer, `Accept: application/json`, optional tenant header. Does not interpret status. `credentials` left at fetch default (`same-origin`). Comment: localhost cookies are not port-scoped.
- Whoami TypeScript: `user_id`, `email?`, `name?`, `is_platform_admin`, `active_org_id?`, `tenants[]` with `id/slug/name/role/status`. Matches live host snake_case JSON (`Program.cs` `PropertyNamingPolicy = SnakeCaseLower`).

**One** (`oneApi.ts`):

- `createTenant` → `POST {VITE_ONE_API_URL}/tenants` `{ name, slug }`. Uses `problemDetail`. This is the only non-8081 write.

**Errors** (`http.ts`):

```1:9:apps/lazuar-pay-merchant/src/lib/http.ts
export async function problemDetail(response: Response, fallback: string): Promise<string> {
  try {
    const body = (await response.json()) as { detail?: string }
    if (body.detail && body.detail.trim()) return body.detail
  } catch {
    /* ignore */
  }
  return fallback
}
```

Used on: gateway PUT, product POST, payment-link POST, One create tenant. **Not** used on whoami or any list GET.

Host `PayErrors.Status` is `{ status, title, detail }`. So a 400 with `detail: "webhook_secret is required"` now shows that sentence, not `keys 400`. 016’s “all 400s one sentence” is **false on this SHA for writes**. It is still true for whoami (`whoami 503`) and for list GETs (silent).

### Complete HTTP inventory from `:5178`

| # | When | Method | Path | Host | Body / notes | Response used? |
|---|------|--------|------|------|--------------|----------------|
| A1 | HomePage, CreateWorkspacePage | GET | `/v1/whoami` | Pay 8081 | no tenant hint | tenants → `dashboardPath` / redirect-to-shell-create |
| A2 | OrgLayout | GET | `/v1/whoami` | Pay | `X-Lazuar-Tenant-Id` | match tenant; else “Not a member” |
| A3 | Overview, Gateway, Checkouts | GET | `/v1/orgs/{orgId}/gateways` | Pay | — | `processors[]`. Fail → silent / Test-only picker |
| A4 | Gateway Edit Save | PUT | `/v1/orgs/{orgId}/gateway` | Pay | see contract matrix | `problemDetail`; then re-GET A3 |
| A5 | Checkouts load | GET | `/v1/orgs/{orgId}/payment-links` | Pay | — | table. `!ok` → silent empty |
| A6 | Create pay link | POST | `/v1/orgs/{orgId}/products` | Pay | `{ name, amount, currency: "MYR" }` | `id` → A7 |
| A7 | after A6 201 | POST | `/v1/payment-links` | Pay | `{ org_id, amount, currency, provider, product_id, max_payers?, unlimited }` | status; then A5 |
| A8 | PaymentsPage | GET | `/v1/orgs/{orgId}/payments` | Pay | — | table. `!ok` silent |
| A9 | ReceiptsPage | GET | `/v1/orgs/{orgId}/receipts` | Pay | — | table. `!ok` silent |
| A10 | CreateWorkspaceForm | POST | `/tenants` | **One** 8080 | `{ name, slug }` | `{ id }` → `/o/{id}/overview` |

Printed, not fetched: webhook URL `{payApi}/v1/webhooks/{rail}/{orgId}`; buyer URL `{VITE_CHECKOUT_ORIGIN}/c/{public_token}`.

**Host doors this SPA never calls** (they exist on 8081):

| Host route | Why it matters |
|------------|----------------|
| `GET /v1/orgs/{orgId}/gateway?provider=` | inspect one row (bare GET now aliases List) |
| `POST /v1/checkouts`, `GET /v1/checkouts/{id}`, `GET /v1/orgs/{orgId}/checkouts` | 016 mint door; 018 mints **payment-links** |
| `GET /v1/orgs/{orgId}/products` | catalog list — unused |
| `GET /v1/orgs/{orgId}/receipts/{id}` | receipt detail — unused |
| `GET /v1/orgs/{orgId}/ready` | dummy ready |
| `GET /health` | 013 probe; gone from this SPA |
| `GET /v1/pay/{token}`, `POST /v1/pay/{token}/start` | checkout `:5179` |

CORS on the host allows `http://localhost:5178`, `http://127.0.0.1:5178`, and preview `4178` (plus checkout 5179/4179). Ops `:3003` and portal `:3004` are denied (`CorsTests`). README: One `App:CorsOrigins` must include `http://localhost:5178` before `POST /tenants` works from this origin. That One config is not in this repo.

---

## 018 UI delta (re-verified)

016/02 described **one** `WorkspacePage.tsx` (310 lines) at `/o/:orgId`: a provider `<select>`, inline secret boxes, PEM in a **single-line `<input>`**, `POST /v1/checkouts` without `provider` / `product_id`, hardcoded `http://localhost:5179/c/{token}`, lists as `<ul>`, `keys ${status}` for every PUT 400, GET extras unused, “Active rail”. **None of that file exists.**

Live 018 shape, re-read:

| 018 claim (019 index) | Live |
|-----------------------|------|
| Aura-style merchant shell | Copied `AppSidebar` sky rail + shadcn-like primitives **in-tree**. `package.json` does **not** depend on `@repo/aura-ui`. Lock asserts that. Comment in `app-sidebar.tsx` still says “Ops day rail”. |
| Last workspace after login | `dashboardPath` + `ORG_HINT_KEY`. Home is a redirect, not a tenant list. |
| Staff email in sidebar | `staffDisplay` + user-menu dropdown email. Trigger shows name+role. Numeric sub rejected. |
| Vault processors independently | GET `/gateways` → one card per `rails[]`. PUT body `provider` per rail. Saving does not set `OrgSettings.ActiveProvider` (host test asserts it stays null). Copy: “Saving a secret does not pick the rail for pay links.” |
| Bind rail at mint | Create-dialog `<Select>` of configured processors + Test. POST `/v1/payment-links` with `provider`. |
| Local Test processor, no secrets | Card `r === 'test'`: Ready, “No keys. Use this on Pay links.”, no Edit. Host PUT test is 400 (`GatewayTests.Put_test_processor_is_400`). SPA never PUTs test. |
| Processor keys in an Edit dialog | `openEdit` → Radix `Dialog`. Square tiles dropped: `locks.test.ts` `not.toContain('aspect-square')` **on GatewayPage only**. (`avatar.tsx` still has `aspect-square` for the avatar image.) |
| Always offer Test when minting | `withTest()` unshifts `{ provider: 'test', configured: true }` if missing. Initial provider state is `'test'`. If GET `/gateways` fails, picker is still Test-only. |
| Pay links as a table; mint from a dialog | `CheckoutsPage` bordered table + “Create pay link” dialog. Empty chrome matches payments/receipts. |
| Capacity | `Capacity = 'one' \| 'limited' \| 'unlimited'`. POST `max_payers: 1` / `N>=2` / `unlimited: true`. Labels “1 person only”. |
| Payments / receipts tables matched to pay-link chrome | Same `rounded-xl border border-slate-200`, uppercase `tracking-wider` heads, empty-state block, **no** `CardContent`. Locked by `locks.test.ts`. HEAD commit message is this match. |
| CHIP PEM textarea | `editing === 'chip'` → `Textarea` placeholder `PEM from CHIP dashboard`. Locked. |
| Hydrate environment from GET | `openEdit` `setEnvironment(row.environment)` when `test` or `live`. Locked (`setEnvironment(row.environment)` + `/gateways`). |
| `product_id` on mint | Create reads product `id` from 201 and sends it on the payment-link POST. 016’s “catalog is an unused side effect” is **half-false**: the name becomes `label`. The **price row** is still unused for charging. |
| `problemDetail` on writes | Gateway / product / pay-link / One tenant. 016 `keys 400` is fixed **for those four**. |
| `VITE_CHECKOUT_ORIGIN` | `.env.example` + `checkoutOrigin()`. Fallback still `http://localhost:5179`. |

Login / callback / RequireAuth error / Home loading / chrome-less first-workspace create were **not** restyled to Aura. They are still system-ui paragraphs and a raw `<button>`.

`dist/assets/index-DuegEQIu.js` is the **same hash 014 quoted as the health-probe leftover**. An 018 rebuild would change the hash. `vite preview` without a rebuild would lie. Root `.gitignore` includes `dist/`.

---

## Bugs (evidence, impact, how to solve)

These are live-code facts that make the SPA wrong, stuck, or dishonest **against the host that is already running**. Gaps (missing product) are the next section.

### B1. Org layout spins forever if `access_token` is not a JWT

`RequireAuth` lets any OIDC `isAuthenticated` user through. `OrgLayout` does:

```33:69:apps/lazuar-pay-merchant/src/layout/OrgLayout.tsx
  const token = pickApiBearerToken(auth.user)
  // ...
  useEffect(() => {
    setOrgHint(orgId)
    if (!token) return
    getWhoami(token, orgId)
      .then(...)
  }, [orgId, token])
  // ...
  if (!who || !tenant || !token) {
    return (
      <div className="flex min-h-dvh items-center justify-center p-6 text-sm text-slate-500">
        Loading workspace…
      </div>
    )
  }
```

If Zitadel ever issues an opaque (or JWE) access token, `pickApiBearerToken` is `undefined`, the effect returns, and the page never leaves “Loading workspace…”. `HomePage` and `CreateWorkspacePage` at least `signinRedirect()` on `!token`. Org routes do not.

**Impact:** staff looks signed in (OIDC) and sees an infinite loader. No retry.

**How to solve:** treat missing JWT the same as signed-out: `signinRedirect()`, or a banner that says the access token is not a JWT and a Retry button. Do not invent an `id_token` fallback (`bearerToken.test.ts` forbids it). Optionally fail `RequireAuth` when `pickApiBearerToken` is empty so `/o/*` never mounts.

### B2. Whoami 401 on org routes is a stuck banner; HomePage redirects

`getWhoami` maps HTTP 401 to thrown `'unauthorized'`. `HomePage` then `signinRedirect()`. `OrgLayout` `catch` sets `error` to that string and renders `<p role="alert">unauthorized</p>` with **no** retry and **no** chrome. Expired access after silent-renew failure lands here.

**Impact:** Ada is trapped on a red sentence with no Sign in.

**How to solve:** same as HomePage: on `'unauthorized'` (and maybe 401 `detail` once whoami uses `problemDetail`) call `signinRedirect()`, preserving `returnTo`. Surface One 503 `detail` (“Identity provider unreachable”) instead of `whoami 503`.

### B3. List GETs fail closed-silent — empty tables lie

| Call | Non-OK |
|------|--------|
| Overview `GET /gateways` | `if (!r.ok) return` — “On file none” |
| Gateway `refresh` | `if (!gw.ok) return` — all rails Empty, Test still Ready |
| Checkouts `loadLinks` | `if (!r.ok) return` — “No pay links yet” |
| Checkouts `GET /gateways` | `setConfigured(withTest([]))` — Test-only picker (this one is honest) |
| Payments / receipts | `if (r.ok) set…` else ignore |

Host 403/503 `detail` is discarded. A paused identity blip looks like a brand-new workspace.

`loadLinks` / Gateway `refresh` are `async` without `.catch` on the `useEffect` `void` call. A **thrown** `fetch` (Pay down) is an unhandled rejection, not a banner. Overview/payments/receipts `.catch(() => undefined)` swallows the throw too.

**Impact:** staff cannot tell “none” from “Pay 503”. They may mint again or paste keys into a host that is not up.

**How to solve:** one shared `payJson` helper: non-OK → `problemDetail` into a page-level `role="alert"`; network throw → “Pay unreachable”. Empty-state copy only when HTTP 200 and `[]`. Do not reuse the empty illustration for errors.

### B4. Writer busy flags have no `try/finally` — dialogs stick; catalog orphans

`GatewayPage.pasteKey`:

```83:116:apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx
  async function pasteKey() {
    if (!write || !editing) return
    setSaving(true)
    const payload: Record<string, string> = { ... }
    const response = await payFetch(...)
    setSaving(false)
    if (!response.ok) setError(await problemDetail(...))
    else { ... closeEdit() }
  }
```

If `payFetch` throws, `setSaving(false)` never runs. Save stays disabled. `createProductAndLink` is worse: `setBusy(true)`, product POST, then payment-link POST. Product `!ok` resets busy. A **throw** on either fetch, or a throw while reading JSON, leaves `busy === true`. If product is 201 and the link throws or 400s, the product row remains (`CatalogEndpoints.Create` already `SaveChanges`) and there is no catalog UI to see or delete it.

**Impact:** stuck dialog; orphan `products` + `prices` rows; Ada retries and creates another product.

**How to solve:** `try/finally` for `saving`/`busy`. If link fails after product 201, show both the host `detail` **and** that a product was already created (or, better, stop creating a product per link — see G4). Do not close the dialog on link failure.

### B5. Overview counts the Test processor as “On file”

Host `GET /gateways` includes Test with `configured: true` (`TestGatewayJson`). Overview:

```23:40:apps/lazuar-pay-merchant/src/pages/org/OverviewPage.tsx
  const onFile = processors.filter((p) => p.configured)
  // ...
              On file </span>
              {onFile.length === 0 ? 'none' : `${onFile.length}`}
```

A workspace with **zero** pasted keys shows **On file 1** (Test) and then a “Paste keys” link. Processor page is honest (Test = Ready, others Empty). Overview is not.

**Impact:** Ada thinks a rail is vaulted.

**How to solve:** count `configured && provider !== 'test'`, or list names the way the cards do, and say “Test is always available” in the copy that already exists on Processor.

### B6. Webhook URL hint is `VITE_PAY_API_URL`, not `Pay:PublicBaseUrl`

```304:315:apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx
                <p className="text-xs leading-relaxed text-slate-500">
                  Webhook URL:{' '}
                  <code>
                    {payApi}/v1/webhooks/{editing}/{orgId}
                  </code>
                </p>
                {editing === 'billplz' ? (
                  <p className="text-xs text-slate-500">
                    Dashboard callback is registered at start from Pay:PublicBaseUrl (public https). This path
                    is the shape; localhost will fail.
                  </p>
                ) : null}
```

Default print: `http://localhost:8081/v1/webhooks/chip/{orgId}` (etc.). Billplz **start** registers `{Pay:PublicBaseUrl}/v1/webhooks/billplz/{orgId}?checkout_id=…` and 400s if the base is not public https (`BillplzHosted.TryPublicBase`). CHIP copy does **not** warn. 016 ranked this #6; 018 added a Billplz sentence but still prints the loopback origin and still omits `checkout_id`.

**Impact:** staff who paste the hint into CHIP/Billplz dashboards configure a URL the PSP cannot reach, and (Billplz) a path shape that is not the callback start emits.

**How to solve:** do not pretend this SPA knows `Pay:PublicBaseUrl` (that would be a Vite secret-adjacent host config). Either (a) have the host GET `/gateways` return a `webhook_url_hint` built from PublicBaseUrl, or (b) print only the **path** `/v1/webhooks/{provider}/{orgId}` and the Billplz sentence for **every** rail that needs a public callback. Never print `http://localhost:8081` as if it were the dashboard value.

### B7. Buyer copy URL is a second origin, defaulting to hardcoded `:5179`

```40:48:apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx
function checkoutOrigin(): string {
  return ((import.meta.env.VITE_CHECKOUT_ORIGIN as string | undefined) ?? 'http://localhost:5179').replace(
    /\/$/,
    '',
  )
}

function buyerUrl(token: string): string {
  return `${checkoutOrigin()}/c/${token}`
}
```

Checkout Vite consumes `/c/{token}` (`apps/lazuar-pay-checkout/src/App.tsx` `tokenFromPath`). Host PSP return URLs use `Pay:CheckoutBaseUrl` (`CheckoutUrls.Base`; Development `http://localhost:5179`; required outside Testing). Nothing ties the two configs. A production merchant **build** bakes `VITE_CHECKOUT_ORIGIN`; if it is unset, every Copy/Open button is localhost.

016 hardcoded the same string with no env. 018 added the env **and kept the hardcoded fallback**. Local dogfood matches Development `CheckoutBaseUrl`. Production does not fail loud.

**Impact:** Ada copies a 5179 URL in a deployed dashboard; buyers 404. PSP return can go to a different origin than the link she pasted in WhatsApp.

**How to solve:** fail the mint dialog if `VITE_CHECKOUT_ORIGIN` is empty **in production builds** (`import.meta.env.PROD`). Locally, keep the 5179 default but print it so it is visible. Longer-term: host payment-link 201 should include `pay_url` built from `Pay:CheckoutBaseUrl`, and the SPA should copy **that**, not mint a parallel URL.

### B8. Test is always offered; the host refuses Test in Production

SPA `rails` includes `'test'`. `withTest` always injects it. Cards always show Test. Host `PayProviders.Listed` **drops** Test when `env.IsProduction()`. `Create` payment-link / checkout with `provider=test` then 400 `"test processor is not enabled"`. The dialog will show that `detail` **after** a product 201 (orphan, B4).

**Impact:** a production merchant build of this same JS mints Test, wastes a product, and 400s.

**How to solve:** if GET `/gateways` `processors` does not include `test`, do not `unshift` it and do not render the Test card as Ready. Trust the host list. Keep `withTest` only as a Development convenience when the list **does** include Test (it already does in non-Production).

### B9. `automaticSilentRenew` + `/callback` as the iframe target

`getOidcConfig` sets `automaticSilentRenew: true` and no `silent_redirect_uri`. `CallbackPage` on `isAuthenticated` runs `takeReturnTo()` (destructive) and `<Navigate to={…} />`. A silent-renew iframe that loads `/callback` can eat `returnTo` and run a client-side navigate inside the iframe.

**Impact:** flaky renew, lost deep-link, confusing nested navigates. Not the local first-login path.

**How to solve:** add a **minimal** `silent-renew.html` (or a route that does not `Navigate`) and set `silent_redirect_uri`. Or set `automaticSilentRenew: false` until that page exists. Do not reuse `CallbackPage`.

### B10. Duplicate `<h1>` on money pages; Settings is Processor

`DashboardChrome` renders `<h1>{title}</h1>` in the top bar. `CheckoutsPage` / `GatewayPage` / `PaymentsPage` / `ReceiptsPage` / create-workspace also render `PageHeader` `<h1>` with the same word. Screen readers hear “Pay links Pay links”. Overview is the only page where the two titles differ (Overview vs tenant name) — that one is fine.

User menu **Settings** navigates to `/gateway`. There is no settings page. Staff looking for email/password land on Processor keys.

**Impact:** chrome noise; mis-click into secrets.

**How to solve:** drop the canvas `PageHeader` title when it equals the chrome title (keep subtitle). Rename the menu item “Processor”, or remove it (sidebar already has Processor). Do not invent a Hub settings cathedral.

### B11. “Not a member of this org” and first-workspace create have no way out

`OrgLayout` error is a centered `<p>` — no switcher, no Home, no Sign out. A typo in `/o/{uuid}` traps the session.

Chrome-less `/workspaces/new` (zero tenants) has a “Lazuar Pay” header and the form. No Sign out. First-time Ada who landed on the wrong account cannot leave without clearing sessionStorage.

**Impact:** support footgun, not a money bug.

**How to solve:** both states need Sign out + “Switch workspace” (link to `/` so `HomePage` re-runs whoami). Do not mount the full sky rail for a membership miss if that implies the org exists.

---

## Gaps (intended vs live, how to close)

### G1. README / LoginPage say One login `:5175`; live OIDC authority is Zitadel `:8085`

```34:38:apps/lazuar-pay-merchant/src/pages/LoginPage.tsx
      <p className="text-sm text-slate-600">
        Sign-in uses One product login at <code>:5175</code>. This page is not a
        password form. Not <code>lazuar-ops</code> (<code>:3003</code>), not
        staff admin (<code>:5173</code>).
      </p>
```

`signinRedirect()` uses `authority` default `http://localhost:8085`. Merchant never `window.location`s to `:5175`. If One login is a **separate** SPA that also talks to Zitadel, Ada’s merchant session does not go through that skin unless Zitadel’s hosted login UI is configured to `:5175`.

**How to close:** either (a) tell the truth — “issuer is Zitadel `:8085`; password UI is whatever that issuer hosts” — or (b) document the Zitadel login-UI setting that makes 8085 show 5175, and lock it in One’s repo. Do not add a password form here.

### G2. Login / callback / RequireAuth are not on the Aura shell

018 restyled the **authenticated dashboard**. Sign-in, callback, OIDC errors, Home “Opening workspace…”, whoami-failed, first create, membership-miss are still unstyled `<p>`/`<button>`. Inconsistent, not a contract bug.

**How to close:** a tiny unauthenticated canvas (kicker + card) reused by Login/Callback/errors. Do not pull ops login. Do not put the sky rail on `/login`.

### G3. No catch-all; route still named `checkouts`; no poll/refresh

Unknown paths blank. Sidebar says Pay links, URL says `/checkouts`. Payments/receipts/pay-links load once; a buyer paying in another tab does not update the table until reload.

**How to close:** splat → “Not found” + link home. Optionally alias `/pay-links`. A modest reload button (or focus-triggered refetch) is enough; do not build a websocket.

### G4. Catalog is a label sidecar, not a catalog

Intended (011/016): create a product, mint a pay link for it. Live: every “Create pay link” **also** `POST /products` with the same amount, then mints a payment-link with that `product_id`. Host charging uses **payment-link `amount`**, not `prices.amount`. `GET /products` is never called. There is no products page, no edit, no delete. Interval is omitted (host defaults `one_off`). Currency is hardcoded `MYR` (host catalog 400s otherwise: `"Bar B currency is MYR"`). Default label is `"Dogfood"`.

So: not fully decorative (the name becomes list `label` / payments `label` / receipts `label`). Not a catalog either.

**How to close (pick one, do not do both silently):**

1. **Honest sidecar:** stop POSTing products; put `label` on `CreatePaymentLinkRequest` (host change; out of this paper’s implement scope). The SPA then stops lying that a Product resource is the thing staff manage.
2. **Real catalog:** a Products page (list GET, create, reuse `product_id` on mint, amount from the price). Mint dialog picks an existing product instead of inventing one.

Until then, treat orphan products (B4) as expected residue.

### G5. Receipt / payment rows are not drill-down

Host `GET /v1/orgs/{orgId}/receipts/{id}` returns number/title/checkout_id (no amount). The table already has amount/payer/status from the **list**. Click does nothing. Payments have no GET-by-id. Official Receipt vs Tax Invoice copy is on the page subtitle (“Never a Tax Invoice”) and in empty state — good.

**How to close:** either drop the unused GET from the mental model, or add a dialog that calls it **and** expand the host DTO so the dialog is not poorer than the table. Do not title anything Tax Invoice.

### G6. `active_org_id` ignored; tenant order is One’s

`dashboardPath` uses session hint then `tenants[0]`. Host maps One `active_tenant_id` → `active_org_id`. First login with no hint can land on a different org than One considers active.

**How to close:** `match ?? tenants.find(t => t.id === who.active_org_id) ?? tenants[0]`. Still prefer the session hint after the user has switched.

### G7. Create-workspace is not writer-gated; slug HTML pattern is locked

Two routes, **one** form:

| Route | Wrapper | Chrome |
|-------|---------|--------|
| `/workspaces/new` | `pages/CreateWorkspacePage.tsx` | header-only, or redirect to `/o/{id}/new` if tenants exist |
| `/o/:orgId/new` | `pages/org/CreateWorkspacePage.tsx` (`OrgCreateWorkspacePage`) | full dashboard |

This is not accidental duplication of the form. The org file is four lines. The 019 question “duplicate CreateWorkspacePage paths” is **two entry points, shared `CreateWorkspaceForm`**. Keep both: zero-tenant bootstrap cannot use `/o/:orgId`.

Slug:

```82:82:apps/lazuar-pay-merchant/src/pages/CreateWorkspaceForm.tsx
                pattern="[a-z0-9\\-]{1,64}"
```

`locks.test.ts` asserts that escaped hyphen (unicode-sets HTML `pattern` would treat a raw `-` as a range). Submit hits One `POST /tenants`. Any **member** of org A can open the switcher item and create org B if One allows authenticated POST. Pay `write` is not consulted.

**How to close:** if One’s rule is “any Ada can create a tenant”, the switcher is honest. If create is owner-only, hide the item using One’s role (not Pay `write` on the **current** org — creating a new tenant is not a Pay money write). Do not invent Pay org CRUD.

### G8. Processor cards hide environment and Brand/Collection

`openEdit` hydrates `environment` and `public_merchant_id`. Cards show last4 + webhook boolean only. A Billplz **live** row looks identical to test until Edit. Overview does not show environment either.

016’s re-save live→test bug is **fixed** for the dialog (hydrate). It is still easy to miss which world you are in.

**How to close:** on Billplz (and any rail that stores environment) print `test`/`live` on the card. Print Brand/Collection id (not secrets) on CHIP/Billplz cards — host GET already returns `public_merchant_id`.

### G9. Capability `hosted_link` is copy, not a chip

Host always sends `capability: "hosted_link"`. SPA `Processor` type has `capability?`. Cards and tables never render it. Overview subtitle says “Capability is hosted_link.” That is enough if it stays the only capability. Do not draw a five-logo wall.

### G10. No pagination, no receipt PDF, no subscribers, no Hub leftovers

Intended S1: paste keys, mint a link, see payments and Official Receipts. Live does that for Test (and for a configured rail). Missing on purpose vs Hub: SST, e-invoice, LHDN, chat, WhatsApp, CRM, credits, appointments, quotes-as-tax. `locks.test.ts` nav must not contain `Appointments`. Do not close these as Pay bugs.

### G11. CI builds merchant; it does not run merchant tests

```113:116:.github/workflows/ci.yml
      - name: Build merchant and checkout
        run: |
          pnpm --filter lazuar-pay-merchant build
          pnpm --filter lazuar-pay-checkout build
```

`pnpm --filter lazuar-pay-merchant test` is documented in the app README and is **not** in CI. `tsc -b && vite build` catches types. It will not catch a PEM `<input>` regression unless someone runs vitest. See Tests.

### G12. `pay-spec` does not describe what the SPA actually calls

`packages/pay-spec/main.tsp` has whoami (without `name`), catalog, `PUT/GET /orgs/{id}/gateway` as a **single** `GatewayView`, checkouts, public pay. It does **not** have `GET /gateways` `processors[]`, `POST /payment-links`, occupancy fields, payments, receipts, Test provider, `key_id`/`key_secret`. Deep spec honesty is sibling `08`. For this slice: the SPA is a client of **live C#**, not of the spec. Do not “fix” the SPA to match TypeSpec.

### G13. Dead Aura copies

`AppSidebarLocationHeader` is unused (switcher replaced it). `onProfileClick` is deprecated on the user menu. `location-header.tsx` comment still says “Live app uses LocationSwitcher”. Harmless drift. Close by deleting unused exports when someone is in the file; do not keep Storybook-only chrome in a product app that has no Storybook.

### G14. Mobile chrome exists; desktop cannot collapse; overlay is fine

`useIsMobile` breakpoint 768 matches `md:`. Hamburger `md:hidden`. Overlay click closes. Nav click closes on mobile (`AppSidebar` pathname effect). Desktop sidebar cannot hide (`md:translate-x-0` wins over `-translate-x-full`). Acceptable for S1. If a tablet in landscape wants more canvas, that is a later `isOpen` on `md` too — not a host mismatch.

---

## SPA vs host contract mismatches (field names, last4, Test processor, capacity, environment, webhook_configured, PEM, hardcoded localhost:5179)

Host JSON is snake_case, case-insensitive. SPA sends snake_case. Binding is not the 016 problem anymore.

### Field names (PUT `/v1/orgs/{orgId}/gateway`)

Host `PutGatewayRequest`: `provider`, `secret`, `webhook_secret`, `public_merchant_id`, `environment`, `key_id`, `key_secret`.

| Host JSON | stripe | chip | billplz | xendit | razorpay | test |
|-----------|--------|------|---------|--------|----------|------|
| `provider` | yes | yes | yes | yes | yes | **not sent** (no Edit) |
| `secret` | API input | API input | API input | API input | **joined** `keyId:keySecret` | — |
| `webhook_secret` | `whsec_` input | **Textarea PEM** | X-Signature input | `x-callback-token` | webhook input | — |
| `public_merchant_id` | omitted | Brand ID | Collection ID | omitted | omitted | — |
| `environment` | omitted (host default `test`) | omitted | **select**, hydrated | omitted | omitted | — |
| `key_id` / `key_secret` JSON | — | — | — | — | **not sent** (client join) | — |

Host: Test PUT 400 `"test processor does not take secrets"`. SPA never PUTs it. Aligned.

Host: `webhook_secret` **always** required. SPA always sends the key (empty string → 400 `detail`, now visible). Dialog copy: “Webhook secret on file. Saving again requires a fresh value.” Aligned with P12-style require-both.

Host: CHIP/Billplz require `public_merchant_id`; others 400 if it is present. SPA only attaches the key for chip/billplz. Aligned.

Host: Billplz requires `environment` if omitted; SPA always sends it for billplz. Hydrate on open. Aligned **in the dialog**. Cards still hide it (G8).

Razorpay: SPA joins into `secret`; host also accepts empty `secret` + `key_id`+`key_secret`. Empty boxes send `":"` → `TrySplit` fails → `"secret must be key_id:key_secret"` (now shown). last4 on GET is last four of **key_id**, not key_secret (`GatewayEndpoints` special case). SPA displays `…{last4}` without saying which side. Fine if staff know it is the key id.

### GET `/gateways` processors

Host list item: `org_id`, `provider`, `last4`, `configured`, `capability`, `public_merchant_id`, `environment`, `webhook_configured`.

SPA `Processor` type: all of those except `org_id`.

| Field | Shown on card | Hydrated into Edit | Notes |
|-------|---------------|--------------------|-------|
| `provider` | title via `railLabel` | dialog title | |
| `last4` | `…{last4}` or “No key on file” | no (secrets are write-only) | Test has `last4: null` |
| `configured` | On file / Empty; Test ignores and says Ready | — | Overview mis-counts Test (B5) |
| `capability` | no | no | copy on Overview only |
| `public_merchant_id` | no | yes | G8 |
| `environment` | no | yes if test/live | G8; 016 re-save bug fixed |
| `webhook_configured` | “webhook on file” / “no webhook” | note if true | Test JSON is `true`; Test card does not show the line |

Bare `GET /gateway` without query now **aliases List** (host `Get` empty provider → `List`). SPA calls `/gateways`. Either works. Spec still types GET as a single `GatewayView`. Live wins.

### last4

Host computes last4 from the **API secret** (Razorpay: key_id). GET never echoes ciphertext (`GatewayTests.Put_and_get_does_not_echo_secret`). SPA never displays a secret after save (inputs clear). Member sees last4. Aligned.

A truncated CHIP **PEM** still stores (PUT does not parse PEM) and sets `webhook_configured: true`. The boolean is “ciphertext present”, not “ImportFromPem will succeed”. SPA cannot know validity. Do not draw a green “verified” chip from this boolean.

### Test processor

Host `TestGatewayJson`: `configured: true`, `environment: "test"`, `webhook_configured: true`, `last4: null`, `capability: hosted_link`. Listed only when `!IsProduction()`.

SPA: always six cards; Test is dashed Ready; mint picker always has Test (`withTest`). Development: aligned (host list includes Test; `withTest` is a no-op duplicate-guard). Production: mismatch (B8).

### Capacity

Host `CreatePaymentLinkRequest`: `MaxPayers`, `Unlimited`. Default if not unlimited: `max_payers = body.MaxPayers ?? 1`; `< 1` → 400. Unlimited → `MaxPayers = null`.

SPA:

- `one` → `max_payers: 1`, `unlimited: false`
- `limited` → integer `maxPayers >= 2` (client); else banner “Limited links need at least 2 people”
- `unlimited` → `unlimited: true`, `max_payers` **omitted** (`undefined` stripped by `JSON.stringify`)

List DTO: `status` `open`|`full`, `max_payers`, `unlimited`, `paid_count`, `taken_count`, `remaining`, `label`, `public_token`, `provider`, `amount`, `currency`, `created_at`. SPA table uses all of those except `remaining`. `statusLabel`: if `full && max_payers === 1 && paid_count >= 1` show **paid** (matches host public GET for one-person paid links). Occupancy: host `taken` counts child checkouts `open` or `paid`. A started-unpaid one-person link is `full` with `paid_count 0` — SPA shows `full`, not `paid`. Honest.

### environment

Billplz select hydrated from GET. Non-billplz: not sent, host stores default `test` even for `sk_live_`. SPA does not pretend Stripe environment is a Pay toggle. CHIP environment column is unused at HTTP time (016/S12). Aligned.

### webhook_configured

Displayed on non-Test cards. Not a form field (cannot be: host does not echo the secret). Aligned with the boolean’s real meaning.

### PEM

016 P0: webhook PEM in `<input>`, no textarea in `src/`. Live:

```241:249:apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx
                  {editing === 'chip' ? (
                    <Textarea
                      id="webhook_secret"
                      value={webhookSecret}
                      onChange={(e) => setWebhookSecret(e.target.value)}
                      autoComplete="off"
                      rows={6}
                      placeholder="PEM from CHIP dashboard"
                    />
```

`JSON.stringify` keeps U+000A. Host `Trim`s the whole string (leading/trailing whitespace only). `ChipWebhook.Parse` `rsa.ImportFromPem(pem)`. **CHIP API secret** is still a single-line `Input` — that is correct; the PEM is the **webhook** public key, not the brand API secret. `locks.test.ts` asserts `Textarea` + `PEM from CHIP dashboard`.

A one-line paste that is not a PEM still PUT 200s. Validity is at webhook time. SPA cannot fix that without a host parse-on-PUT.

### hardcoded `http://localhost:5179`

Three places, same string:

1. SPA fallback in `checkoutOrigin()` (B7)
2. `.env.example` `VITE_CHECKOUT_ORIGIN=http://localhost:5179`
3. Host Development `Pay:CheckoutBaseUrl` and Testing `CheckoutUrls.Base`

016 additionally hardcoded it in `setPayUrl` with **no** env. That assignment is gone. The leftover fallback is the local default, not a second unrelated constant — but production must not rely on it.

Buyer path shape `/c/{token}` matches checkout Vite. Do not mint `/pay/{token}` or Hub `/{slug}/pay/{id}`.

### Amount / currency / money types

Host amounts are `decimal` major units (PaymentQueryTests `10m`). SPA `Number(amount)` on a text box; `""` → `0` → host 400 `"amount must be greater than 0"` (now shown). `"abc"` → `NaN` → JSON `null` → same 400. No client-side amount UX. Currency locked `MYR` on both product and link; catalog host refuses anything else; payment-links would accept another currency if the SPA sent it. Bar B is MYR. Fine.

`formatMoney` is copy-pasted in three pages (`en-MY` currency). Not a contract mismatch.

### Writer vs member (repeat, contract)

Host GET lists: `RequireMemberAsync`. Host PUT gateway / POST product / POST payment-links: `RequireWriterAsync`. SPA hides the write widgets. A member who forges `fetch` gets 403 `"Writer role required"`. Chrome honesty matches H18. There is no VIEWER chip.

---

## Tests that lock this slice vs missing

### What runs today

Merchant vitest (node), three files:

| File | What it actually locks |
|------|------------------------|
| `src/auth/bearerToken.test.ts` | compact JWS vs opaque/JWE; picker never returns `id_token` |
| `src/lib/staffDisplay.test.ts` | whoami email/name; OIDC profile fallback; numeric sub → “Signed in” |
| `src/locks.test.ts` | **grep** of `src/**/*.ts(x|css)` excluding tests |

`locks.test.ts` inventory (this is the 018 lockfile, not a behavior test):

1. No `type="password"`, no `/one/auth/login`, no `lazuar_auth`
2. `package.json` has no `@repo/api-types-ts`, `@repo/aura-ui`, `lazuar-ops`
3. GatewayPage contains `Textarea` and `PEM from CHIP dashboard`
4. GatewayPage contains `setEnvironment(row.environment)` and `/gateways`
5. Processor vault is cards (`CardTitle`), not `aspect-square`, not `One active rail`, copy includes `does not pick the rail for pay links`
6. Edit + `DialogContent` + `openEdit`
7. Test: `r === 'test'`, `No keys. Use this on Pay links.`, `processors.ts` contains `'test'`
8. Receipts table chrome + `Official Receipt` + not `CardContent`
9. Payments table chrome + `formatMoney` + not `CardContent`
10. Checkouts: `provider`, `/gateways`, `/payment-links`, `'test'`, `Create pay link`, `DialogContent`, `Table`, `unlimited`, `max_payers`, `1 person only`
11. Overview: `/gateways`, not `Active rail`, `On file`
12. Chrome: `AppSidebar`, `WorkspaceSwitcher`, nav `Processor`, not `Appointments`
13. Switcher: `Create workspace`, `Switch workspace`, `/new`
14. `homePath.ts`: `/overview`, `/workspaces/new`
15. slug `pattern="[a-z0-9\\\\-]{1,64}"`
16. Create form `Card` + `workspace_name`

Host tests that **back** the SPA contract (not merchant vitest):

- `GatewayTests`: member 403 PUT, webhook_secret required, no echo, CHIP brand id, unknown provider, member GET metadata, list six processors + Test configured, PUT test 400, Billplz collection required, Razorpay colon required, `ActiveProvider` stays null
- `CatalogTests`: owner 201, member 403
- `PaymentLinkTests`: default one payer, unlimited null max, max 0 = 400, 401 without bearer, list newest-first, other org 403, occupancy
- `PaymentQueryTests`: list payments provider/label/payer; receipts `RCPT-` + Official Receipt + issued
- `WhoamiTests`: email, **name**, `active_org_id`, empty tenants, 401 skips One
- `CorsTests`: 5178 allowed, 3003 denied
- `IsolationTests.Vite_apps_do_not_use_hub_types`: `@repo/api-types-ts` only

CI: `dotnet test` Pay sln + **build** merchant/checkout. Not `pnpm --filter lazuar-pay-merchant test`.

### Missing (one hole → one method, named)

None of these exist. They are the tests this slice still needs if 018 is not to rot.

| Hole | Why grep is not enough | Proposed lock (analysis) |
|------|------------------------|--------------------------|
| `canWriteMoney` | `roles.ts` untested; a `viewer` or `Owner` typo would hide/show the wrong chrome | unit: owner/admin true; member/undefined/VIEWER/empty false |
| `dashboardPath` | lock only greps `homePath.ts` for two strings | unit with fake session hint: empty → `/workspaces/new`; hint still member → that org; hint stale → first tenant |
| `checkoutOrigin` | fallback 5179 can return without anyone noticing env was forgotten | unit: env set → that origin; unset → 5179; trailing slash stripped |
| `withTest` | duplicate Test rows or dropping Test on GET fail | unit: configured stripe+test unchanged; empty list → one test; already has test → no dup |
| `statusLabel` / `payersLabel` | one-person paid shown as `full` would be a regression | unit table of occupancy DTOs |
| `problemDetail` | writes depend on it; a `title`-only body would fall back | unit with fake Response JSON |
| `isSafeReturnPath` | open-redirect | unit: `/o/x` ok; `//evil` rejected |
| PEM control | lock greps `Textarea` anywhere in GatewayPage; a second unused import would pass | assert the chip branch contains `Textarea` **and** the stripe branch does not use it for `whsec_` |
| Member cannot mint | no component test; chrome could show the button and host would 403 | jsdom or RTL: `write={false}` → no “Create pay link” / no Edit |
| Busy/saving finally | B4 | if you add try/finally, a unit of the helper; until then this is untested |
| CI | build ≠ test | add `pnpm --filter lazuar-pay-merchant test` next to build |
| IsolationTests vs aura-ui | host IsolationTests would not catch adding `@repo/aura-ui` | either extend IsolationTests or keep the merchant lock and run it in CI |
| Playwright dogfood | none | out of S1 unless someone funds it; do not stand up Playwright to lock grep tests |

`vitest` `environment: 'node'` cannot mount React. Changing it to jsdom is a prerequisite for component tests; grep tests can stay node.

There is still **no** test that the SPA sends `product_id`, `provider`, `unlimited`, or snake_case keys — locks grep source strings, which is how 018 prevented a silent revert to `POST /v1/checkouts` without `provider`. That is a weak but real ratchet. A payload unit (pure function extracting the JSON) would be stronger than grep.

---

## Ranked findings (P0/P1/P2)

**P0** means the local Test dogfood sentence is false: cannot sign in, cannot mint a `:5179/c/{token}` link as owner, or would charge the wrong rail / drop PEM newlines / map every 400 to `keys 400` so Ada cannot paste CHIP.

On this SHA, **there is no P0 on the local Test path.** 016’s P0-class items (PEM `<input>`, unused `product_id`, `POST /v1/checkouts` without `provider`, `keys ${status}` only) are fixed. Aura chrome did not re-break PKCE, bearer picking, or CORS 5178.

### P1

1. **Silent list GETs (B3).** Empty “No pay links / payments / receipts yet” and Overview “On file none” on 403/503. **Solve:** `problemDetail` + error chrome; empty state only on 200 `[]`.
2. **OrgLayout JWT / 401 handling (B1, B2).** Opaque access → infinite loader; API 401 → stuck `unauthorized`. HomePage already redirects. **Solve:** missing JWT and `'unauthorized'` → `signinRedirect` with `returnTo`; show whoami `detail` for 503.
3. **Write `saving`/`busy` without `finally`; product orphan (B4).** **Solve:** try/finally; stop creating a product per link or surface the leftover.
4. **Webhook hint origin (B6).** Prints `VITE_PAY_API_URL` loopback; Billplz start uses `Pay:PublicBaseUrl` + `checkout_id`; CHIP has no public-https sentence. **Solve:** path-only hint or host-provided public URL; copy Billplz warning onto CHIP.
5. **Buyer URL vs `Pay:CheckoutBaseUrl` (B7).** Fallback hardcoded `http://localhost:5179`. **Solve:** prod build refuses empty `VITE_CHECKOUT_ORIGIN`; later, host returns `pay_url`.
6. **Test offered when host Production disables it (B8).** **Solve:** trust GET `processors`; do not inject Test if the host omitted it.
7. **Silent renew iframe hits `CallbackPage` (B9).** **Solve:** dedicated silent-renew document, or disable silent renew.

### P2

8. Overview counts Test as “On file” (B5).
9. Duplicate `<h1>`; Settings → Processor (B10).
10. Membership-miss / first-create have no Sign out (B11).
11. Login copy `:5175` vs authority `:8085` (G1); unstyled auth pages (G2).
12. No splat 404; URL still `/checkouts`; no refetch (G3).
13. Catalog sidecar / no products page (G4); no receipt drill-down (G5).
14. `active_org_id` unused (G6); create-workspace not Pay-writer-gated (G7).
15. Cards hide environment and Brand/Collection (G8); capability unused (G9).
16. CI does not run merchant vitest (G11); IsolationTests does not ban `@repo/aura-ui`.
17. TypeSpec stale vs live doors (G12) — do not retarget the SPA at the spec.
18. Stale `dist/index-DuegEQIu.js`; dead `location-header`; `formatMoney` triplicated.
19. Razorpay `key_id`/`key_secret` JSON keys unused (client join works).
20. Amount box has no client validation; lists unpaginated.
21. `is_platform_admin` unused (aligned with host write gate — leave it).

---

## Refuse

- Do **not** retarget `lazuar-ops` (`:3003`) at 8081. This origin is `:5178`.
- Do **not** add a password form, `POST /one/auth/login`, or Hub `lazuar_auth` cookie.
- Do **not** send `id_token` as Bearer if `access_token` is opaque. Fail visible instead.
- Do **not** depend on `@repo/api-types-ts` or `@repo/aura-ui`. Copied sidebar is the allowed steal; a package import is not.
- Do **not** fake a VIEWER role. One has `owner` / `admin` / `member`.
- Do **not** put processor secrets, `ZITADEL_PAT`, wrap keys, or `lzr_sk_` in `VITE_*`.
- Do **not** register checkout `:5179` as an OIDC SPA.
- Do **not** flip 011/11 cells from this paper.
- Do **not** implement from this file. Analysis only.
- Do **not** mount LHDN, SST, WhatsApp, Hub CRM, appointments, or quotes-as-tax on this sidebar.
- Do **not** treat GET `webhook_configured` as “PEM is valid”.
- Do **not** make Test a vault PUT. Host 400 is the law.
- Do **not** “open” member GET of secrets. last4 + flags only.

---

## Appendix: quoted evidence

### A. Routes and duplicate create entry

See `App.tsx` 15–53 (quoted in full under **What exists**). `pages/org/CreateWorkspacePage.tsx` in full:

```1:8:apps/lazuar-pay-merchant/src/pages/org/CreateWorkspacePage.tsx
import { useOutletContext } from 'react-router-dom'
import type { OrgOutletContext } from '../../layout/OrgLayout'
import { CreateWorkspaceForm } from '../CreateWorkspaceForm'

export function OrgCreateWorkspacePage() {
  const { token } = useOutletContext<OrgOutletContext>()
  return <CreateWorkspaceForm token={token} />
}
```

### B. PKCE config + bearer picker

```23:34:apps/lazuar-pay-merchant/src/auth/oidcConfig.ts
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
```

```14:17:apps/lazuar-pay-merchant/src/auth/bearerToken.ts
export function pickApiBearerToken(user: User | null | undefined): string | undefined {
  if (!user) return undefined
  if (isJwtLike(user.access_token)) return user.access_token
  return undefined
}
```

### C. Last workspace + staff display

```4:10:apps/lazuar-pay-merchant/src/lib/homePath.ts
export function dashboardPath(tenants: WhoamiTenant[]): string {
  if (tenants.length === 0) return '/workspaces/new'
  const hint = getOrgHint()
  const match = hint ? tenants.find((t) => t.id === hint) : undefined
  return `/o/${(match ?? tenants[0]).id}/overview`
}
```

```11:26:apps/lazuar-pay-merchant/src/lib/staffDisplay.ts
export function staffDisplay(
  who: Whoami,
  user?: User | null,
): { name: string; email: string | null } {
  const profile = user?.profile
  const email =
    usable(who.email) ??
    usable(profile?.email) ??
    (profile?.preferred_username?.includes('@') ? usable(profile.preferred_username) : null)
  const name =
    usable(who.name) ??
    usable(typeof profile?.name === 'string' ? profile.name : undefined) ??
    (email ? email.split('@')[0]! : null)
  return { name: name ?? 'Signed in', email }
}
```

### D. Independent vault + Test + Edit dialog + PEM textarea + environment hydrate

```67:81:apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx
  function openEdit(next: Rail) {
    setError(null)
    setSecret('')
    setWebhookSecret('')
    setKeyId('')
    setKeySecret('')
    const row = processors.find((p) => p.provider === next)
    if (row?.environment === 'test' || row?.environment === 'live') {
      setEnvironment(row.environment)
    } else {
      setEnvironment('test')
    }
    setPublicMerchantId(row?.public_merchant_id ?? '')
    setEditing(next)
  }
```

```126:174:apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx
        {rails.map((r) => {
          const row = processors.find((p) => p.provider === r)
          const isTest = r === 'test'
          const on = isTest || Boolean(row?.configured)
          return (
            <Card key={r} className={cn('gap-3 py-4 shadow-none', isTest ? 'border-dashed ...' : '...')}>
              {/* Ready / On file / Empty; last4 · webhook; Test: no Edit */}
```

```241:249:apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx
                  {editing === 'chip' ? (
                    <Textarea
                      id="webhook_secret"
                      value={webhookSecret}
                      onChange={(e) => setWebhookSecret(e.target.value)}
                      autoComplete="off"
                      rows={6}
                      placeholder="PEM from CHIP dashboard"
                    />
```

Host Test JSON and PUT refusal:

```240:250:apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs
    static object TestGatewayJson(string orgId) => new
    {
        org_id = orgId,
        provider = PayProviders.Test,
        last4 = (string?)null,
        configured = true,
        capability = PayProviders.Capability,
        public_merchant_id = (string?)null,
        environment = "test",
        webhook_configured = true
    };
```

```41:44:apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs
        if (PayProviders.IsTest(provider))
        {
            return PayErrors.Status(400, "Bad Request", "test processor does not take secrets");
        }
```

### E. Pay links: Test always offered, capacity, product_id, checkout origin

```30:48:apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx
const testProcessor: Processor = { provider: 'test', configured: true }

function withTest(list: Processor[]): Processor[] {
  const ready = list.filter((p) => p.configured && isRail(p.provider))
  if (!ready.some((p) => p.provider === 'test')) {
    ready.unshift(testProcessor)
  }
  return ready
}

function checkoutOrigin(): string {
  return ((import.meta.env.VITE_CHECKOUT_ORIGIN as string | undefined) ?? 'http://localhost:5179').replace(
    /\/$/,
    '',
  )
}
```

```161:185:apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx
    const created = await payFetch(token, `/v1/orgs/${orgId}/products`, {
      method: 'POST',
      orgHint: orgId,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: productName, amount: Number(amount), currency: 'MYR' }),
    })
    // ...
        body: JSON.stringify({
        org_id: orgId,
        amount: Number(amount),
        currency: 'MYR',
        provider,
        product_id: product.id,
        max_payers: capacity === 'one' ? 1 : capacity === 'limited' ? limited : undefined,
        unlimited: capacity === 'unlimited',
      }),
```

Host default one payer / unlimited:

```73:85:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs
        int? maxPayers;
        if (body.Unlimited)
        {
            maxPayers = null;
        }
        else
        {
            maxPayers = body.MaxPayers ?? 1;
            if (maxPayers < 1)
            {
                return PayErrors.Status(400, "Bad Request", "max_payers must be at least 1");
            }
        }
```

### F. problemDetail vs silent GET vs whoami

```1:9:apps/lazuar-pay-merchant/src/lib/http.ts
export async function problemDetail(response: Response, fallback: string): Promise<string> {
  try {
    const body = (await response.json()) as { detail?: string }
    if (body.detail && body.detail.trim()) return body.detail
  } catch {
    /* ignore */
  }
  return fallback
}
```

```33:38:apps/lazuar-pay-merchant/src/lib/payApi.ts
  if (response.status === 401) {
    throw new Error('unauthorized')
  }
  if (!response.ok) {
    throw new Error(`whoami ${response.status}`)
  }
```

```117:120:apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx
  async function loadLinks() {
    const r = await payFetch(token, `/v1/orgs/${orgId}/payment-links`, { orgHint: orgId })
    if (!r.ok) return
    setLinks((await r.json()) as PayLink[])
  }
```

Host error shape:

```5:6:apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayErrors.cs
    public static IResult Status(int status, string title, string detail) =>
        Results.Json(new { status, title, detail }, statusCode: status);
```

### G. Role + CORS + checkout URL on the host

```1:4:apps/lazuar-pay-merchant/src/lib/roles.ts
/** One tenant roles. Pay: owner/admin write money; member is read-only. */
export function canWriteMoney(role: string | undefined | null): boolean {
  return role === 'owner' || role === 'admin'
}
```

```58:71:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
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

```8:32:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs
    public static string Success(...) =>
        string.IsNullOrWhiteSpace(checkout.SuccessUrl)
            ? Base(config, env) + "/c/" + checkout.PublicToken + "?status=verifying"
            : checkout.SuccessUrl;
    // Base: Pay:CheckoutBaseUrl, else Testing → http://localhost:5179, else throw
```

### H. Whoami live includes `name`; TypeSpec does not

```4:11:apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiResponse.cs
    public required string UserId { get; init; }
    public string? Email { get; init; }
    public string? Name { get; init; }
    public bool IsPlatformAdmin { get; init; }
    public string? ActiveOrgId { get; init; }
    public IReadOnlyList<WhoamiTenant> Tenants { get; init; } = [];
```

```25:31:packages/pay-spec/main.tsp
model WhoamiResponse {
  user_id: string;
  email?: string;
  is_platform_admin: boolean;
  active_org_id?: string;
  tenants: WhoamiTenant[];
}
```

SPA `Whoami` type includes `name?: string` (`payApi.ts`). Live host + SPA win over the spec.

### I. Slug pattern lock target

```74:82:apps/lazuar-pay-merchant/src/pages/CreateWorkspaceForm.tsx
              <Input
                id="workspace_slug"
                value={slug}
                onChange={(ev) => {
                  setSlugTouched(true)
                  setSlug(ev.target.value)
                }}
                required
                pattern="[a-z0-9\\-]{1,64}"
```

### J. 016 facts that are **false** on this SHA (do not reuse)

| 016 sentence | Live |
|--------------|------|
| The money UI is `WorkspacePage.tsx` | File gone; six org pages + chrome |
| Zero `<textarea>` in merchant `src` | CHIP webhook is `Textarea` |
| PUT errors are `` `keys ${status}` `` | `problemDetail` → host `detail` |
| Mint is `POST /v1/checkouts` without `provider` / `product_id` | `POST /v1/payment-links` with both |
| Buyer URL is a hardcoded template with no env | `VITE_CHECKOUT_ORIGIN` + 5179 fallback |
| GET `environment` / `webhook_configured` unused | hydrated / shown |
| “Active rail” | Forbidden by locks; independent cards |

End of paper. Nothing here was implemented. Re-open live files before treating a quoted line as still true after the next SHA.
