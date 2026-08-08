# 06 — Source code & internal refs (app rename impact)

**Scope of this document:** Source code inside the four apps being renamed, plus cross-app references from `lazuar-api`, `packages/*`, `lazuar-docs`, root tooling, Docker/CI, and deploy configs that hardcode old app/package/folder names.

**Proposed renames:**

| Old folder / package name | New folder / package name |
|---------------------------|---------------------------|
| `apps/developers-page` / `developers-page` | `apps/lazuar-spec` / `lazuar-spec` |
| `apps/ops-page` / `ops-page` | `apps/lazuar-ops` / `lazuar-ops` |
| `apps/portal-page` / `portal-page` | `apps/lazuar-portal` / `lazuar-portal` |
| `apps/superadmin-page` / `superadmin-page` | `apps/lazuar-admin` / `lazuar-admin` |

**Classification legend used throughout:**

| Tag | Meaning |
|-----|---------|
| **(A)** | Pure mechanical rename — must update with folder/package rename or build/deploy breaks |
| **(B)** | Product / UX / domain string — intentionally **not** the app package name; leave alone unless product deliberately rebrands |
| **(C)** | False positive — same English word as an app, but refers to API modules, URL paths, cookies, domains, or unrelated concepts |

**Out of scope for code changes in this analysis pass:** This file is read-only analysis. Do not change app code as part of writing it.

**Related surfaces deliberately noted but secondary:** Historical docs under `docs/**` and ADRs contain many path strings; listed in a dedicated section. Primary “must fix for rename to work” items are package names, Docker COPY paths, CI/matrix Dockerfiles, mprocs, compose service keys that use `*-page`, and first-line path comments inside the four apps.

---

## 1. Executive summary

### 1.1 What breaks if folders rename without string updates

1. **pnpm package `name` fields** in each app’s `package.json` — filters like `pnpm --filter developers-page` and Docker `pnpm --filter ./apps/ops-page...` fail.
2. **All four Dockerfiles** — every `COPY apps/<old>/…` and `RUN pnpm --filter ./apps/<old>…` path, plus Next standalone `CMD ["node", "apps/<old>/server.js"]` and static asset COPY destinations.
3. **`docker-bake.hcl` target names + dockerfile paths** — bake targets currently named `portal-page`, `ops-page`, `superadmin-page`, `developers-page`.
4. **`docker-compose.yml` / `docker-compose.ghcr.yml`** — service keys `ops-page`, `portal-page`, `superadmin-page` and their `dockerfile:` paths (note: **prod** compose already uses short keys `ops` / `portal` / `superadmin` / `developers` and **GHCR image names**, not folder names).
5. **`.github/workflows/ghcr.yml`** — matrix `dockerfile: apps/*-page/Dockerfile` entries.
6. **`mprocs-dev.yaml`** — process names and `cd apps/*-page`.
7. **`pnpm-lock.yaml` importers** — keys `apps/developers-page`, `apps/ops-page`, `apps/portal-page`, `apps/superadmin-page` regenerate on next install after rename.
8. **First-line file path comments** inside ops/portal/superadmin source (`// apps/ops-page/...`) — cosmetic but stale after rename; superadmin still has **copied** comments pointing at `apps/ops-page` **(A optional / hygiene)**.

### 1.2 What does *not* need to change for a folder rename

- **HTTP cookie names:** `lazuar_auth`, `lazuar_admin_auth` **(B)** — auth protocol, not package names.
- **localStorage keys:** `ops_active_workspace_id`, `lazuar-ops-sidebar-*`, `lazuar-admin-sidebar-*` **(B)** — product storage keys; already partially aligned with new product names (`lazuar-ops`, `lazuar-admin`). Changing them logs users out of workspace selection / UI prefs.
- **HTML / Next metadata titles:** “Lazuar Ops”, “Lazuar Admin”, “Lazuar Portal”, “Lazuar API Documentation”, “Lazuar Developer Hub” **(B)**.
- **Production URL path prefixes:** `/portal`, `/docs`, `/admin/`, hub root `/` **(B)** — routing topology, independent of monorepo folder names.
- **GHCR image repository names:** `lazuar-hub-ops`, `lazuar-hub-portal`, `lazuar-hub-superadmin`, `lazuar-hub-developers` **(B relative to this rename)** — already “product-ish”; changing them is a separate registry/deploy concern, not required by folder rename.
- **Backend API route prefixes:** `/api/v1/ops`, `/api/v1/admin`, `/api/v1/platform`, public commerce `/{tenant}/portal/...` **(C)**.
- **TypeSpec / OpenAPI product “Ops”**, `docs-ops.tsp`, developers-page route `/ops` “Ops Console API” **(C)**.
- **`packages/*`:** **zero** matches for `developers-page|ops-page|portal-page|superadmin-page` **(none)**.
- **CORS origins:** listed by `http://localhost:3002`…`3005`, not by package name **(B/C)** — ports stay unless ports also change.
- **User-Agent / analytics tags:** **none found** in the four apps or API related to these package names.
- **`turbo.json`:** no per-app name references **(none)**.
- **Root `package.json` scripts:** no direct `*-page` filters (uses turbo + `lazuar-docs` only).

### 1.3 Critical Next.js standalone path coupling

Next.js `output: "standalone"` embeds the monorepo-relative package path into the runtime layout:

- Developers Dockerfile:
  - `COPY …/apps/developers-page/.next/standalone ./`
  - `COPY …/apps/developers-page/.next/static ./apps/developers-page/.next/static`
  - `COPY …/apps/developers-page/public ./apps/developers-page/public`
  - `CMD ["node", "apps/developers-page/server.js"]`
  - Healthcheck: `http://127.0.0.1:3000/docs` **(B path prefix, not package name)**
- Portal Dockerfile: same pattern with `apps/portal-page` and healthcheck `/portal`.

After rename, **every** occurrence of `apps/developers-page` / `apps/portal-page` inside those Dockerfiles must become `apps/lazuar-spec` / `apps/lazuar-portal` **(A)**, or the runtime image fails at `CMD` / static asset resolution.

Vite apps (ops, superadmin) copy only `dist/` into a generic `/app/dist` — less path-coupled at runtime, but build-stage COPY/`--filter` paths still **(A)**.

---

## 2. Package identity matrix

### 2.1 `package.json` `"name"` fields **(A)**

| File | Current `"name"` | Proposed |
|------|------------------|----------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/developers-page/package.json` | `"developers-page"` | `"lazuar-spec"` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/ops-page/package.json` | `"ops-page"` | `"lazuar-ops"` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/portal-page/package.json` | `"portal-page"` | `"lazuar-portal"` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/superadmin-page/package.json` | `"superadmin-page"` | `"lazuar-admin"` |

None of the four packages use scoped names (`@lazuar/...`); no `@lazuar/(developers|ops|portal|superadmin)` matches exist anywhere in the monorepo.

### 2.2 Dev ports (not package names, but mapped 1:1 to apps) **(B unless ports change)**

| App (old) | Port | Script |
|-----------|------|--------|
| developers-page | 3002 | `next dev -p 3002` |
| ops-page | 3003 | `vite --port=3003` |
| portal-page | 3004 | `next dev -p 3004` |
| superadmin-page | 3005 | `vite --port=3005` |

These ports appear in API CORS lists (see §5). Renaming folders does **not** require port changes.

### 2.3 Workspace membership

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/pnpm-workspace.yaml` uses `apps/*` and `packages/*` globs only — **no hard-coded app names**. Folder rename is enough for workspace discovery **(A via filesystem only)**.

### 2.4 Lockfile importers **(A — regenerate)**

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/pnpm-lock.yaml` contains importer keys:

- `apps/developers-page:`
- `apps/ops-page:`
- `apps/portal-page:`
- `apps/superadmin-page:`

Update by renaming directories + `pnpm install` (do not hand-edit unless tooling requires).

---

## 3. Per-app source inventory

### 3.1 `apps/developers-page` → `lazuar-spec`

#### 3.1.1 Mechanical package / path refs **(A)**

| Location | Detail | Class |
|----------|--------|-------|
| `package.json` `"name": "developers-page"` | pnpm filter identity | **(A)** |
| `Dockerfile` (full file) | All `apps/developers-page` COPY/filter/build/static/public/`server.js` paths | **(A)** |
| Comment in Dockerfile line 2 | “Lazuar Developer Hub … → hub.lazuar.com/docs” | **(B)** product URL story |

#### 3.1.2 Config: Next basePath **(B)**

File: `apps/developers-page/next.config.ts`

```ts
basePath: process.env.NEXT_BASE_PATH || "",
// Production: https://hub.lazuar.com/docs
```

- Env `NEXT_BASE_PATH=/docs` set in Dockerfile ARG and `docker-bake.hcl` / `ghcr.yml`.
- **Do not rename** `/docs` as part of package rename. That is the public product path for the Spec/docs host **(B)**.

#### 3.1.3 Spec path resolution **(B paths; A only if package moves break relative resolve)**

File: `apps/developers-page/lib/openapi.ts`

- Local: `path.join(process.cwd(), "../../packages/api-spec/dist")` — relative monorepo climb; still valid after folder rename **as long as app stays under `apps/`**.
- Docker: `OPENAPI_SPEC_ROOT=/app/openapi-specs` **(B env contract)**.

#### 3.1.4 Product metadata / titles **(B)**

| File | String | Class |
|------|--------|-------|
| `app/layout.tsx` | `title: "Lazuar API Documentation"`, description “API Reference for the Lazuar Platform” | **(B)** |
| `app/page.tsx` | H1 “Lazuar Developer Hub”; product cards LHDN, Payments, One, Commerce, Billing, **Ops Console API** | **(B)** / Ops card is **(C)** vs ops-page |
| `app/auth/page.tsx` | `title: "Authentication — Lazuar Developer Hub"` | **(B)** |
| `app/quickstart/page.tsx` | `title: "Quickstart — Lazuar Developer Hub"` | **(B)** |
| `app/webhooks/page.tsx` | `title: "Event catalog — Lazuar Developer Hub"` | **(B)** |
| `app/payments-cashier/page.tsx` | `title: "Payments cashier quickstart — Lazuar Developer Hub"` | **(B)** |
| Scalar routes (`billing`, `commerce`, `payments`, `one`, `lhdn`, `ops`) | `title: "Lazuar … API"` | **(B)** |
| `app/page.tsx` copy | “Create keys in Ops → Developer → API Keys” | **(B)** product UX name “Ops”, not package `ops-page` |
| `app/page.tsx` footer | `https://hub.lazuar.com/api/v1`, SDK package names | **(B)** |

#### 3.1.5 Route `/ops` inside developers-page **(C)**

- Path: `apps/developers-page/app/ops/route.ts`
- Title: “Lazuar Ops API”
- Meaning: OpenAPI reference for the **backend Ops module / console API**, not the monorepo app `ops-page`.
- **Do not rename this route** when renaming `ops-page` → `lazuar-ops`.

#### 3.1.6 No localStorage / cookies / analytics in this app

Developers hub is public/docs-oriented; no `localStorage` keys, no auth cookies of its own, no user-agent branding strings found.

#### 3.1.7 README / AGENTS **(A optional docs hygiene)**

- `README.md` — generic create-next-app boilerplate; no package name in body (title not set to developers-page).
- `AGENTS.md` / `CLAUDE.md` — Next.js guidance only; no package rename impact.

---

### 3.2 `apps/ops-page` → `lazuar-ops`

#### 3.2.1 Mechanical package / path refs **(A)**

| Location | Detail | Class |
|----------|--------|-------|
| `package.json` `"name": "ops-page"` | pnpm identity | **(A)** |
| `Dockerfile` | `COPY apps/ops-page/...`, `pnpm install --filter ./apps/ops-page...`, build filter, `COPY …/apps/ops-page/dist` | **(A)** |
| First-line path comments (list below) | Historical path stamps | **(A hygiene)** |

**Files with `// apps/ops-page/...` path comments:**

| Absolute path |
|---------------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/ops-page/src/hooks/use-chat-stream.ts` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/ops-page/src/hooks/use-debounce.ts` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/ops-page/src/types/chat.ts` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/ops-page/src/components/OpsChatWorkspace.tsx` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/ops-page/src/components/chat/ChatMessageBubble.tsx` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/ops-page/src/components/chat/MarkdownContent.tsx` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/ops-page/src/components/forms/AutoForm.tsx` (comment wrongly says `.../chat/AutoForm.tsx`) |

#### 3.2.2 Vite base **(B)**

File: `apps/ops-page/vite.config.ts`

```ts
base: process.env.VITE_BASE_PATH || "/",
// Ops serves at hub root (/).
```

Production: hub root `/` (Caddy `handle { reverse_proxy ops:3000 }`). Independent of folder name **(B)**.

#### 3.2.3 HTML title **(B)**

File: `apps/ops-page/index.html`

```html
<title>Lazuar Ops</title>
```

Product brand string. Aligns with new package name `lazuar-ops` thematically; **no required change** for rename **(B)**.

#### 3.2.4 Env vars used by ops frontend (values are URLs, not package names) **(B)**

| Env | Default / bake value | Used for | Class |
|-----|----------------------|----------|-------|
| `VITE_API_URL` | `https://hub.lazuar.com/api/v1` or local `http://localhost:8080/api/v1` | API client base | **(B)** |
| `VITE_PORTAL_URL` | bake `https://hub.lazuar.com/portal`; compose default `http://localhost:3004` | Checkout/quote link generation | **(B)** URL of portal app, not string `portal-page` |
| `VITE_BASE_PATH` | `/` | Vite asset base | **(B)** |
| `VITE_DOCS_URL` | optional; code default `"/docs"` | Links from API Keys page into Spec hub | **(B)** |

**Call sites for `VITE_PORTAL_URL`:**

| File | Behavior |
|------|----------|
| `src/modules/commerce/components/ProductDetailPanel.tsx` | `generateCheckoutUrl` → `${baseUrl}/${slug}/checkout/${productSlug}` |
| `src/modules/invoicing/components/QuoteDetailPanel.tsx` | `generatePaymentUrl` → `${baseUrl}/${slug}/pay/${sessionId}` |

**Call site for docs base:**

| File | Behavior |
|------|----------|
| `src/modules/workspace/pages/ApiKeysPage.tsx` | `const DOCS_BASE = import.meta.env.VITE_DOCS_URL \|\| "/docs"` then links to `${DOCS_BASE}/lhdn`, `/one`, `/auth` |

These reference **deployed product paths**, not monorepo package names **(B)**.

#### 3.2.5 localStorage keys **(B — do not rename casually)**

| Key | Files | Notes |
|-----|-------|-------|
| `ops_active_workspace_id` | `App.tsx`, `lib/api-client.ts`, `hooks/use-chat-stream.ts`, `LoginPage.tsx`, `SubscribersPage.tsx` | Workspace selection for `X-Tenant-Id`. Prefix is product “ops”, not package `ops-page`. |
| `lazuar-ops-sidebar-collapsed` | `App.tsx` | UI pref; already `lazuar-ops` style |
| `lazuar-ops-sidebar-sections` | `components/Sidebar.tsx` | UI pref |

**Classification:** **(B)**. Optional future rename of `ops_active_workspace_id` → something like `lazuar_ops_active_workspace_id` is a **product migration**, not required by folder rename. Would break existing browser state.

#### 3.2.6 Cookies used by ops **(B/C)**

| Cookie | Who sets | Class |
|--------|----------|-------|
| `lazuar_auth` | API `Modules/One` login | **(B)** shared session cookie for ops + portal humans |
| `sidebar_state` | shadcn `components/ui/sidebar.tsx` | **(C)** generic UI kit |

Ops does not hardcode the cookie name in app code for auth; it relies on `credentials: "include"` on the API client **(B)**.

#### 3.2.7 Component / module names containing “Ops” **(C vs package)**

These are **product/domain** identifiers, not the monorepo package name:

- `OpsChatWorkspace.tsx`, types `Ops.ProposedActionDto`, API paths `/ops/chat/...`
- Sidebar/product language “Ops”, “Developer”, “Commerce”
- Backend-typed schemas under `Ops.*` in `@repo/api-types-ts`

**Do not rename** as part of `ops-page` → `lazuar-ops` folder rename.

#### 3.2.8 API path prefix `/admin/...` heavily used in ops-page **(C)**

Nearly all commerce/billing/comms client calls use OpenAPI paths like `/admin/commerce/products`, `/admin/billing/ledger`, etc. That is the **backend admin surface**, not the superadmin app folder `superadmin-page`. **(C)**

#### 3.2.9 README

`apps/ops-page/README.md` is literally `# Ops` — product title **(B)**.

---

### 3.3 `apps/portal-page` → `lazuar-portal`

#### 3.3.1 Mechanical package / path refs **(A)**

| Location | Detail | Class |
|----------|--------|-------|
| `package.json` `"name": "portal-page"` | pnpm identity | **(A)** |
| `Dockerfile` | all `apps/portal-page` paths + standalone `server.js` + static/public COPY + healthcheck path uses `/portal` **(B)** | **(A)** for paths; **(B)** for healthcheck URL path |
| First-line path comments | list below | **(A hygiene)** |

**Files with `// apps/portal-page/...` comments:**

| Absolute path |
|---------------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/portal-page/src/modules/checkout/components/PromoCodeInput.tsx` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/portal-page/src/modules/checkout/components/CheckoutLayout.tsx` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/portal-page/src/modules/checkout/components/IdentityBanner.tsx` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/portal-page/src/modules/core/lib/server-client.ts` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/portal-page/src/app/not-found.tsx` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/portal-page/src/app/page.tsx` |

#### 3.3.2 Next basePath **(B)**

File: `apps/portal-page/next.config.ts`

```ts
basePath: process.env.NEXT_BASE_PATH || "",
// Production: https://hub.lazuar.com/portal
```

Bake/CI: `NEXT_BASE_PATH=/portal`. **(B)** product path.

#### 3.3.3 Metadata titles **(B)**

| File | String |
|------|--------|
| `src/app/layout.tsx` | `title: "Lazuar Portal"`, description “Secure checkout and buyer dashboard” |
| `src/app/legal/terms/page.tsx` | “Terms of Service \| Lazuar” |
| `src/app/legal/privacy/page.tsx` | “Privacy Policy \| Lazuar” |
| `src/app/legal/refund/page.tsx` | “Refund Policy \| Lazuar” |

#### 3.3.4 Auth cookie forward **(B)**

File: `apps/portal-page/src/modules/core/lib/server-client.ts`

```ts
const authCookie = cookieStore.get("lazuar_auth");
```

Must stay synchronized with API cookie issuance in One module **(B)**. Not related to package name `portal-page`.

#### 3.3.5 App-router paths containing “portal” **(C / B product routes)**

Examples (not exhaustive):

- `/{tenantSlug}/portal` buyer dashboard links in checkout success / quote views
- API: `POST /public/commerce/{tenantSlug}/portal/cancel` (OpenAPI + `CommunityPortalView` / portal cancel flows)
- These are **product URL and API design**, not monorepo package names **(C/B)**.

#### 3.3.6 Env

| Env | Role | Class |
|-----|------|-------|
| `NEXT_PUBLIC_API_URL` | Browser API base | **(B)** |
| `API_URL` | SSR server-side API base inside Docker (`http://api:8080/api/v1`) | **(B)** |
| `NEXT_BASE_PATH` | `/portal` in prod | **(B)** |

#### 3.3.7 No package-name localStorage keys

Portal relies on URL tokens / cookies more than workspace localStorage. shadcn `sidebar_state` cookie **(C)** if used.

#### 3.3.8 README

Generic create-next-app boilerplate — no `portal-page` string in body **(none operational)**.

---

### 3.4 `apps/superadmin-page` → `lazuar-admin`

#### 3.4.1 Mechanical package / path refs **(A)**

| Location | Detail | Class |
|----------|--------|-------|
| `package.json` `"name": "superadmin-page"` | pnpm identity | **(A)** |
| `Dockerfile` | all `apps/superadmin-page` COPY/filter/build/dist paths | **(A)** |
| Stale path comments still saying `apps/ops-page` | Copied from ops; see below | **(A hygiene)** |

**Stale cross-app comments inside superadmin (currently wrong even before rename):**

| File | Comment today |
|------|----------------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/superadmin-page/src/hooks/use-debounce.ts` | `// apps/ops-page/src/hooks/use-debounce.ts` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/superadmin-page/src/types/chat.ts` | `// apps/ops-page/src/types/chat.ts` |

After rename hygiene, these should become `// apps/lazuar-admin/...` (or be removed).

#### 3.4.2 Vite base **(B)**

File: `apps/superadmin-page/vite.config.ts`

```ts
base: process.env.VITE_BASE_PATH || "/",
// Production: https://hub.lazuar.com/admin/
```

Bake/CI: `VITE_BASE_PATH=/admin/`. Caddy strips `/admin/*` via `handle_path`. **(B)**

#### 3.4.3 HTML title **(B)**

```html
<title>Lazuar Admin</title>
```

Already product-aligned with `lazuar-admin` **(B)**.

#### 3.4.4 localStorage **(B)**

| Key | File |
|-----|------|
| `lazuar-admin-sidebar-collapsed` | `src/App.tsx` |
| `lazuar-admin-sidebar-sections` | `src/components/Sidebar.tsx` |

Already use `lazuar-admin` prefix — **no change required** for rename **(B)**.

#### 3.4.5 Auth cookie (API-side name) **(B)**

Platform login uses `lazuar_admin_auth` (path-scoped to `/api/v1/platform`). Superadmin client only does `credentials: "include"`. Cookie name is **not** `superadmin-page` **(B)**.

#### 3.4.6 README

`# Ops` (stale copy of ops README title) — product/docs noise **(B/C hygiene)**.

---

## 4. Cross-app references (other packages & apps)

### 4.1 `packages/*`

**Result:** No matches for `developers-page`, `ops-page`, `portal-page`, or `superadmin-page` under `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages`.

Implications:

- `@repo/api-types-ts`, `@repo/api-spec`, eslint-config, ui package do not hardcode app folder names.
- TypeSpec product file `docs-ops.tsp` / module `ops` is **(C)** backend/product contract, not frontend app name.

### 4.2 `apps/lazuar-docs` **(A for docs accuracy)**

| File | Snippet / usage | Class |
|------|-----------------|-------|
| `apps/lazuar-docs/README.md` | Table row `` `apps/developers-page` \| Live Scalar OpenAPI `` | **(A)** path doc |
| `apps/lazuar-docs/docs/reference/openapi.md` | “Run **developers-page**”; `pnpm --filter developers-page dev` | **(A)** command must become `lazuar-spec` |
| `apps/lazuar-docs/docs/index.md` | “Scalar OpenAPI is under **developers-page** (`/payments`)” | **(A)** app name; **(B)** route `/payments` |
| `apps/lazuar-docs/docs/guide/how-to-maintain.md` | “production developers-page URL” | **(A)** naming; URL itself **(B)** |
| `apps/lazuar-docs/docs/integrations/payments-cashier.md` | “Scalar developers page **Payments** product” | **(B)** prose “developers page”, not package id |

### 4.3 `apps/lazuar-api` — string constants & comments

#### 4.3.1 Direct mentions of `ops-page` (comments only) **(A hygiene / C semantic)**

| File | Line context | Class |
|------|--------------|-------|
| `Modules/One/Infrastructure/Workers/SystemGenesisBootstrapperJob.cs` | `// Ensure superadmin can open ops-page (memberships drive /me/entitlements…)` | **(A hygiene)** comment only; behavior is membership grant |
| `Modules/One/Infrastructure/Endpoints.cs` | `// Platform superadmins can operate any active workspace (ops-page requires ≥1 entitlement).` | **(A hygiene)** comment only |

No C# identifiers, config keys, or CORS entries use the literal package names `ops-page` / `portal-page` / etc.

#### 4.3.2 CORS origins **(B — ports, not package names)**

Files:

- `apps/lazuar-api/src/Lazuar.Api/appsettings.json`
- `apps/lazuar-api/src/Lazuar.Api/appsettings.Development.json`

```json
"CorsOrigins": "http://localhost:3000,http://localhost:3001,http://localhost:3002,http://localhost:3003,http://localhost:3004,http://localhost:3005,http://localhost:3020,http://localhost:3021,http://localhost:8080,http://localhost:8090"
```

| Port | Typical local app |
|------|-------------------|
| 3002 | developers-page |
| 3003 | ops-page |
| 3004 | portal-page |
| 3005 | superadmin-page |

**Class:** **(B/C)** — origin list is host:port based. Rename does not require CORS change unless ports change. Production uses `App__CorsOrigins=https://hub.lazuar.com` (single origin path-based hub) in `deploy/prod/env.example` **(B)**.

Program.cs: `policy.WithOrigins(origins)` from config — no app names **(C)**.

#### 4.3.3 `App:ClientUrl` / portal base URLs **(B)**

| Location | Value / fallback | Purpose | Class |
|----------|------------------|---------|-------|
| `appsettings*.json` `App:ClientUrl` | `http://localhost:3004` | Portal base for magic links / checkout return URLs | **(B)** |
| `deploy/prod/env.example` | `https://hub.lazuar.com/portal` | Prod path-based portal | **(B)** |
| `Configuration/AppOptions.cs` default | `http://localhost:3020` (legacy default; overridden by appsettings) | **(B/C)** stale default relative to current portal port 3004 |
| `OneLinkService.cs` | fallback `http://localhost:3004` | Password reset / one-links | **(B)** |
| `InitiateCheckoutCommandHandler.cs` | fallback `http://localhost:3004` | Checkout client URL | **(B)** |
| `Commerce PublicEndpoints.cs` | fallback `http://localhost:3004` | Public commerce | **(B)** |
| `FulfillmentRequestedIntegrationEventHandler.cs` | fallback `https://portal.lazuar.com` | Digital delivery links | **(B)** historical subdomain style |
| `OrderCompletedDigitalDeliveryHandler.cs` | same | **(B)** |
| `LifecycleEventHandlers.cs` | hard-coded `https://portal.lazuar.com/checkout/update` | **(B)** product URL debt (not package name) |
| `MessageTemplateCommandHandlers.cs` / `TemplateEndpoints.cs` | sample `https://portal.lazuar.com/workspace/portal?token=test_token` | Preview substitution **(B)** |
| Tests `DunningTemplateVariableSubstitutionTests.cs` | `https://portal.test` | **(C)** test fixture |

**None of these strings are `portal-page`.** They are product host/URL configuration. Folder rename does not force changes. Aligning hard-coded `portal.lazuar.com` with path-based `hub.lazuar.com/portal` is a **separate product debt** item **(B)**.

#### 4.3.4 Auth cookies in API **(B)**

| Cookie | Issuance | Class |
|--------|----------|-------|
| `lazuar_auth` | `Modules/One/Infrastructure/Endpoints.cs` Append/Delete; `Program.cs` JwtBearer `OnMessageReceived` | **(B)** |
| `lazuar_admin_auth` | `Modules/Payments/Infrastructure/PlatformEndpoints.cs`; Program.cs platform path branch | **(B)** |

#### 4.3.5 Backend path segments that look like app names **(C)**

| Path / concept | Meaning | Class |
|----------------|---------|-------|
| `/api/v1/ops/**` | Ops module (chat, etc.) | **(C)** |
| `/api/v1/admin/**` | Tenant admin APIs used by ops console | **(C)** not superadmin app |
| `/api/v1/platform/**` | Superadmin / platform APIs | **(C)** |
| `Modules/Ops/**` | Backend module folder | **(C)** |
| Public `.../portal/cancel`, portal token flows | Buyer portal product API | **(C)** |
| Tenant middleware checks for `/api/v1/ops`, `/api/v1/admin` | Security routing | **(C)** |

#### 4.3.6 `Modules.Ops.Tests` / Taskfile `api:test`

Taskfile runs `Modules.Ops.Tests` — **backend Ops module tests**, not `ops-page` **(C)**.

---

## 5. Root tooling, Docker, CI, deploy

### 5.1 `mprocs-dev.yaml` **(A)**

```yaml
developers-page:
  shell: cd apps/developers-page && pnpm dev
ops-page:
  shell: cd apps/ops-page && pnpm dev
superadmin-page:
  shell: cd apps/superadmin-page && pnpm dev
portal-page:
  shell: cd apps/portal-page && pnpm dev
```

All four process keys + `cd` paths must update **(A)**. Process key names can become `lazuar-spec`, `lazuar-ops`, etc. for consistency.

### 5.2 `docker-bake.hcl` **(A for targets/paths; B for image names & public paths)**

| Construct | Current | Class |
|-----------|---------|-------|
| `group "default" targets` | `portal-page`, `ops-page`, `superadmin-page`, `developers-page` | **(A)** |
| `target "portal-page"` + `dockerfile = "apps/portal-page/Dockerfile"` | **(A)** |
| `target "ops-page"` + dockerfile path | **(A)** |
| `target "superadmin-page"` + dockerfile path | **(A)** |
| `target "developers-page"` + dockerfile path | **(A)** |
| Image tags `lazuar-hub-portal`, `lazuar-hub-ops`, `lazuar-hub-superadmin`, `lazuar-hub-developers` | Already non-`*-page` | **(B)** — optional later rebrand to `lazuar-portal` etc. is a **registry** decision |
| Comments listing public paths `/portal`, `/docs`, `/admin` | Product topology | **(B)** |
| `VITE_PORTAL_URL` default `https://hub.lazuar.com/portal` | Product URL | **(B)** |
| Labels `org.opencontainers.image.title` = `lazuar-hub-*` | Image metadata | **(B)** |

**Note:** Bake target names are independent of GHCR image names. You can rename targets to `lazuar-portal` while keeping image `lazuar-hub-portal` **(A vs B split)**.

### 5.3 `docker-compose.yml` (local full profile) **(A)**

| Service key | dockerfile | container_name | image |
|-------------|------------|----------------|-------|
| `ops-page` | `apps/ops-page/Dockerfile` | `lazuar-ops` | `ghcr.io/proxeon/lazuar-hub-ops:local` |
| `portal-page` | `apps/portal-page/Dockerfile` | `lazuar-portal` | `…/lazuar-hub-portal:local` |
| `superadmin-page` | `apps/superadmin-page/Dockerfile` | `lazuar-superadmin` | `…/lazuar-hub-superadmin:local` |

**Observations:**

- Service **keys** still use `*-page` → **(A)** rename candidates (`lazuar-ops`, etc.).
- **container_name** already mostly product-style (`lazuar-ops`, `lazuar-portal`) **(B already good)**.
- **developers-page is missing** from this compose file entirely (only bake + prod compose ship it). Not a rename issue; inventory gap.
- Args `VITE_PORTAL_URL` default `http://localhost:3004` **(B)**.

### 5.4 `docker-compose.ghcr.yml` **(A for service keys)**

Same `ops-page` / `portal-page` / `superadmin-page` service keys + GHCR images. No dockerfile paths (prebuilt images). Rename service keys optionally for consistency **(A optional / consistency)**; image names **(B)**.

### 5.5 `deploy/prod/docker-compose.yml` **(B — already short names)**

Service keys already:

- `ops` → image `lazuar-hub-ops`
- `portal` → `lazuar-hub-portal`
- `superadmin` → `lazuar-hub-superadmin`
- `developers` → `lazuar-hub-developers`

**No `*-page` strings** in prod compose. **No folder paths** (images only). Folder rename does not touch this file unless you also rebrand GHCR image names **(B)**.

### 5.6 `deploy/prod/Caddyfile` **(B)**

Routes by path to compose **service names** `portal`, `developers`, `superadmin`, `ops`, `api` — not monorepo folders. **(B)**

### 5.7 `deploy/prod/env.example` **(B)**

- `App__ClientUrl=https://hub.lazuar.com/portal`
- `App__CorsOrigins=https://hub.lazuar.com`
- No `*-page` package names.

### 5.8 `deploy/prod/README.md` **(B)**

Path table uses service roles (`ops`, `portal`, `docs`, `superadmin`), not package names.

### 5.9 `scripts/remote-deploy.sh` **(B)**

Health-waits: `hub-api`, `hub-ops`, `hub-portal`, `hub-superadmin`, `hub-developers` — **container names from prod compose**, not monorepo folders. **(B)**

### 5.10 `.github/workflows/ghcr.yml` **(A for dockerfile paths)**

Matrix:

| Image name (B) | dockerfile (A) |
|----------------|----------------|
| `lazuar-hub-api` | `apps/lazuar-api/Dockerfile` |
| `lazuar-hub-portal` | `apps/portal-page/Dockerfile` → `apps/lazuar-portal/Dockerfile` |
| `lazuar-hub-ops` | `apps/ops-page/Dockerfile` → `apps/lazuar-ops/Dockerfile` |
| `lazuar-hub-superadmin` | `apps/superadmin-page/Dockerfile` → `apps/lazuar-admin/Dockerfile` |
| `lazuar-hub-developers` | `apps/developers-page/Dockerfile` → `apps/lazuar-spec/Dockerfile` |

Build-args (`NEXT_BASE_PATH=/portal`, `/docs`, `VITE_BASE_PATH=/admin/`, `VITE_PORTAL_URL=https://hub.lazuar.com/portal`) stay **(B)**.

### 5.11 `.github/workflows/ci.yml`

No frontend app names; only `apps/lazuar-api` and packages. **No rename impact.**

### 5.12 `Taskfile.yml`

- `task fe` → mprocs config (impact via mprocs **A**).
- `docker:build` echo text mentions “api, portal, ops, superadmin” product words **(B)**.
- No `pnpm --filter *-page` hardcodes.
- `tunnel:fe` still mentions “community-page” port 3020 — **legacy (C/B debt)**, unrelated to this four-app rename.

### 5.13 Root `README.md` **(A docs)**

| Section | Content | Class |
|---------|---------|-------|
| Key Separation | `` `ops-page` (Admin) ``, `` `portal-page` (Checkout) `` | **(A)** package names in prose |
| Project structure tree | `ops-page/`, `portal-page/`, `superadmin-page/` + domains `ops.lazuar.com` etc. | **(A)** folders; domains **(B historical)** vs current hub paths |
| Port table | `ops-page`, `portal-page`, `superadmin` | **(A)** |
| Omits developers-page | Pre-existing doc gap | note only |

### 5.14 `turbo.json`

No per-package name list. **No impact.**

---

## 6. Complete **(A)** checklist — mechanical updates required for rename

### 6.1 Inside the four apps

| # | Item | Action |
|---|------|--------|
| A1 | `apps/developers-page/package.json` name | → `lazuar-spec` |
| A2 | `apps/ops-page/package.json` name | → `lazuar-ops` |
| A3 | `apps/portal-page/package.json` name | → `lazuar-portal` |
| A4 | `apps/superadmin-page/package.json` name | → `lazuar-admin` |
| A5 | Folder renames under `apps/` | git mv four directories |
| A6 | `apps/lazuar-spec/Dockerfile` (was developers) | Replace all `apps/developers-page` path segments including standalone `server.js` and static/public destinations |
| A7 | `apps/lazuar-ops/Dockerfile` | Replace all `apps/ops-page` |
| A8 | `apps/lazuar-portal/Dockerfile` | Replace all `apps/portal-page` including standalone layout |
| A9 | `apps/lazuar-admin/Dockerfile` | Replace all `apps/superadmin-page` |
| A10 | Path comments in ops sources (7 files) | Optional hygiene → `apps/lazuar-ops/...` |
| A11 | Path comments in portal sources (6 files) | Optional hygiene → `apps/lazuar-portal/...` |
| A12 | Path comments in superadmin (2 files still saying ops-page) | Hygiene → `apps/lazuar-admin/...` |

### 6.2 Monorepo root / infra

| # | Item | Action |
|---|------|--------|
| A13 | `mprocs-dev.yaml` | Keys + `cd apps/...` |
| A14 | `docker-bake.hcl` | Target names + `dockerfile =` paths (image tags optional **B**) |
| A15 | `docker-compose.yml` | Service keys + `dockerfile:` paths |
| A16 | `docker-compose.ghcr.yml` | Service keys (images **B**) |
| A17 | `.github/workflows/ghcr.yml` | Matrix `dockerfile:` paths |
| A18 | `pnpm-lock.yaml` | Regenerate via `pnpm install` |
| A19 | Root `README.md` structure + ports table | Update package names |
| A20 | `apps/lazuar-docs/**` refs listed in §4.2 | Update filter command + path prose |
| A21 | API comments mentioning `ops-page` (2 sites) | Optional comment hygiene |

### 6.3 Not required but often confused with **(A)**

| Item | Why not A |
|------|-----------|
| GHCR image names `lazuar-hub-*` | Separate deploy/registry contract |
| Prod compose service names | Already short |
| Caddy paths `/portal`, `/docs`, `/admin` | Product URLs |
| Cookie names | Auth protocol |
| localStorage keys | Browser state / product |
| HTML titles | Product brand |
| OpenAPI `/admin`, `/ops` | Backend modules |

---

## 7. Complete **(B)** inventory — product strings that should stay

### 7.1 Public product paths (path-based hub)

| Path | Serves | Config locus |
|------|--------|--------------|
| `/` | Ops console (Vite) | Caddy default handle; `VITE_BASE_PATH=/` |
| `/portal` (+ `*`) | Portal Next | `NEXT_BASE_PATH=/portal`; Caddy `handle /portal*` |
| `/docs` (+ `*`) | Spec/docs Next | `NEXT_BASE_PATH=/docs`; Caddy `handle /docs*` |
| `/admin` / `/admin/*` | Superadmin Vite | `VITE_BASE_PATH=/admin/`; Caddy strip prefix |
| `/api/*` | API | Caddy |
| `/health` | API liveness | Caddy |

### 7.2 Browser titles / metadata

| App | Title(s) |
|-----|----------|
| Ops | `Lazuar Ops` (`index.html`) |
| Admin | `Lazuar Admin` (`index.html`) |
| Portal | `Lazuar Portal` + legal page titles |
| Spec | `Lazuar API Documentation`, `Lazuar Developer Hub`, per-guide titles |

### 7.3 Cookies

| Name | Role |
|------|------|
| `lazuar_auth` | Human session (ops + portal SSR forward) |
| `lazuar_admin_auth` | Platform superadmin session (`/api/v1/platform`) |
| `sidebar_state` | shadcn UI (generic) |

### 7.4 localStorage

| Name | App |
|------|-----|
| `ops_active_workspace_id` | Ops |
| `lazuar-ops-sidebar-collapsed` | Ops |
| `lazuar-ops-sidebar-sections` | Ops |
| `lazuar-admin-sidebar-collapsed` | Admin |
| `lazuar-admin-sidebar-sections` | Admin |

### 7.5 Env var *names* and URL *values*

- `VITE_API_URL`, `VITE_PORTAL_URL`, `VITE_BASE_PATH`, `VITE_DOCS_URL`
- `NEXT_PUBLIC_API_URL`, `NEXT_BASE_PATH`, `API_URL`, `OPENAPI_SPEC_ROOT`
- `App__ClientUrl`, `App__CorsOrigins`, `App__ApiBaseUrl`
- Defaults pointing at `localhost:3004` or `hub.lazuar.com/portal` / `portal.lazuar.com`

### 7.6 GHCR / container product names (already “new style”)

- Images: `lazuar-hub-api`, `lazuar-hub-ops`, `lazuar-hub-portal`, `lazuar-hub-superadmin`, `lazuar-hub-developers`
- Containers (prod): `hub-ops`, `hub-portal`, `hub-superadmin`, `hub-developers`
- Containers (local compose): `lazuar-ops`, `lazuar-portal`, `lazuar-superadmin`

### 7.7 UX copy referencing “Ops” as product

- Developers hub: “Create keys in Ops → Developer → API Keys”
- Page titles “Ops Console API”
- README “Ops” headings

These describe the **product**, not the npm package string `ops-page`.

### 7.8 Analytics / user-agent

**None found** for these four apps. No GTM, Segment, PostHog, Sentry DSN, or custom User-Agent branding tied to package names.

---

## 8. Complete **(C)** false-positive catalog

### 8.1 “ops” ≠ `ops-page`

| Occurrence | Why false positive |
|------------|-------------------|
| Backend `Modules/Ops/` | Domain module |
| TypeSpec `docs-ops.tsp`, OpenAPI `Ops.*` schemas | Contract product surface |
| HTTP `/api/v1/ops/**` | Module route prefix |
| developers-page app route `/ops` | Documents Ops **API** |
| Component `OpsChatWorkspace`, types `OpsConversationDto` | Feature names |
| Taskfile `Modules.Ops.Tests` | Backend tests |
| Comment “ops-page requires ≥1 entitlement” | Only the word pair is about the frontend app; the **code** is entitlement logic |
| localStorage `ops_active_workspace_id` | Product storage key (classed **B** for rename policy; **C** relative to package id) |
| Compose/prod service `ops` | Deploy role name |

### 8.2 “portal” ≠ `portal-page`

| Occurrence | Why false positive |
|------------|-------------------|
| Next routes `/{tenant}/portal` | Buyer product route |
| API `.../portal/cancel`, portal-link generation | Commerce product API |
| Template vars `{{portal_magic_link}}` | Email template language |
| `App:ClientUrl` “portal base” | Config role |
| Historical `portal.lazuar.com` | Domain strategy ADRs / hard-coded fallbacks |
| Caddy `/portal*` | Deploy path |
| Image `lazuar-hub-portal` | Registry product name |

### 8.3 “admin” / “superadmin” ≠ `superadmin-page`

| Occurrence | Why false positive |
|------------|-------------------|
| API `/api/v1/admin/**` | Tenant admin Minimal APIs used primarily by **ops** console |
| Cookie `lazuar_admin_auth` | Platform auth cookie |
| Path `/admin` hub route | Superadmin SPA mount |
| Role `SUPER_ADMIN` | Authz role |
| “Platform superadmins can operate any active workspace” | Domain rule comment |
| Image `lazuar-hub-superadmin` | Registry name (may stay even if package is `lazuar-admin`) |

### 8.4 “developers” ≠ `developers-page`

| Occurrence | Why false positive |
|------------|-------------------|
| “Developer” nav group inside ops UI | Feature area for webhooks/API keys |
| Prod service `developers` / image `lazuar-hub-developers` | Deploy names |
| Prose “developer hub”, “developers page” | Product language |
| Path `/docs` | Spec host path (not “developers” in URL) |
| SDK audience copy “for developers” | English |

### 8.5 Absolute monorepo paths

| Kind | Finding |
|------|---------|
| Live source (`*.ts`, `*.tsx`, `*.cs` in apps) | **No** absolute `/Users/akmalfirdaus/.../apps/*-page` paths in runtime code |
| Historical gap docs under `docs/001-gaps/**` | Many absolute paths under old repo name `lazuar-hub` and `apps/*-page` — documentation only **(A docs optional)** |
| `lib/openapi.ts` | Relative `../../packages/api-spec/dist` only |

### 8.6 packages comments/paths

No package source comments reference the four app folder names.

---

## 9. Interaction map (who points at whom)

```
                    ┌─────────────────────────────┐
                    │  hub.lazuar.com (Caddy)     │
                    │  / → ops                    │
                    │  /portal → portal           │
                    │  /docs → developers(spec)   │
                    │  /admin → superadmin        │
                    │  /api → api                 │
                    └─────────────┬───────────────┘
                                  │
        ┌─────────────────────────┼──────────────────────────┐
        ▼                         ▼                          ▼
 [lazuar-ops]              [lazuar-portal]             [lazuar-spec]
  VITE_API_URL ──────────► API                   OPENAPI from api-spec dist
  VITE_PORTAL_URL ────────► portal public URLs
  VITE_DOCS_URL default /docs ──► spec hub
  cookie lazuar_auth       cookie lazuar_auth (SSR)
        │                         │
        └──────────► App:ClientUrl (portal base for emails/checkout)
        
 [lazuar-admin]
  VITE_API_URL → /api/v1/platform (+ cookie lazuar_admin_auth)
```

Cross-app **string** dependencies that are **URLs/env**, not package names:

1. Ops → Portal: `VITE_PORTAL_URL` (build-time bake + runtime default localhost:3004).
2. Ops → Spec: `VITE_DOCS_URL` or `"/docs"`.
3. API → Portal: `App:ClientUrl`.
4. Portal → API: `API_URL` / `NEXT_PUBLIC_API_URL`.
5. Spec → api-spec package: relative path or `OPENAPI_SPEC_ROOT`.

None of 1–5 embed the strings `ops-page` or `portal-page`.

---

## 10. Docs & ADR surface (secondary; full list of hit files)

These do **not** break builds, but confuse humans/AI after rename. Class: **(A docs)** when they name packages/paths; **(B)** when they describe product; **(C)** when they discuss modules.

### 10.1 High-signal root / app docs

| Path |
|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/README.md` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/README.md` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/reference/openapi.md` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/index.md` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/guide/how-to-maintain.md` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/001-backend/001-backend-solidification-checklist.md` |

### 10.2 Gap analyses (`docs/001-gaps/`) with package path hits

| File |
|------|
| `docs/001-gaps/README.md` |
| `docs/001-gaps/00-what-we-need-to-do-next.md` |
| `docs/001-gaps/01-dunning-engine.md` |
| `docs/001-gaps/03-api-auth-credentials.md` |
| `docs/001-gaps/04-developers-page-dx.md` (filename itself contains old app name — **(A docs)** rename of file is optional historical preservation choice) |
| `docs/001-gaps/06-payments-module.md` |
| `docs/001-gaps/07-commerce-module.md` |
| `docs/001-gaps/08-communications-module.md` |
| `docs/001-gaps/09-lhdn-module.md` |
| `docs/001-gaps/11-ops-crm-messaging.md` |
| `docs/001-gaps/13-typespec-api-contracts.md` |
| `docs/001-gaps/16-testing-coverage.md` |
| `docs/001-gaps/18-outbound-customer-webhooks.md` |
| `docs/001-gaps/19-frontend-backend-integration.md` |
| `docs/001-gaps/20-architecture-intent-vs-implementation.md` |
| `docs/contracts/openapi-vs-minimal-api.md` |

### 10.3 ADRs with package names

| File | Notes |
|------|-------|
| `docs/architecture-decision-log/007-product-scoped-api-references.md` | `developers-page` routes |
| `docs/architecture-decision-log/013-frontend-module-implementation.md` | Entire ADR titled around `ops-page` |
| `docs/architecture-decision-log/014-apps.md` | `ops-page/src/modules/{appName}/` |
| `docs/architecture-decision-log/016-platform-domain-strategy.md` | Caddy examples `ops-page:3000` **(historical service names)** |
| `docs/architecture-decision-log/017-portal-frontend-architecture.md` | `apps/portal-page/` |
| `docs/architecture-decision-log/018-marketplace-and-structured-content-strategy.md` | ops-page / portal-page product flow |
| `docs/architecture-decision-log/022-remove-community-vault-modules.md` | ops-page / portal-page / superadmin-page |
| `docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md` | ops-page / portal-page |

**Policy recommendation for ADRs:** Leave historical ADRs as-is with a header note that package names were renamed on date X, **or** add a one-line “Superseded path:” footnote. Do not rewrite history unless the rename program explicitly includes docs.

---

## 11. Risk notes specific to this rename set

### 11.1 Name collision: `lazuar-admin` vs API `/admin` and cookie `lazuar_admin_*`

New package `lazuar-admin` is **closer** to many existing “admin” product terms. Risk is **human/search confusion**, not compile breakage. Mitigations:

- Keep backend path `/api/v1/admin` as tenant admin APIs **(C stays)**.
- Keep cookie `lazuar_admin_auth` **(B stays)**.
- Prefer speaking of the app as “platform admin” / `lazuar-admin` package vs “admin APIs”.

### 11.2 Name collision: `lazuar-ops` vs `Modules/Ops` vs `/ops` docs route

Same pattern: package rename improves monorepo clarity but **does not** resolve overloaded English “Ops”. Do not mass-rename backend Ops module as part of this program **(C stay)**.

### 11.3 Name: `lazuar-spec` vs `packages/api-spec`

New app name `lazuar-spec` sits next to package `@repo/api-spec` / `packages/api-spec`. They are related (UI that renders specs) but different artifacts. Document the distinction in root README after rename **(B docs)**.

### 11.4 Next standalone path sensitivity

Highest runtime risk of the rename is **forgetting** to update Dockerfile `CMD` / static COPY destinations for Next apps. Vite apps fail at **build** instead of runtime if filter paths wrong — easier to notice in CI.

### 11.5 Image name drift vs folder name

After rename:

| Folder | Likely package | Current GHCR image |
|--------|----------------|--------------------|
| `lazuar-spec` | `lazuar-spec` | `lazuar-hub-developers` |
| `lazuar-ops` | `lazuar-ops` | `lazuar-hub-ops` |
| `lazuar-portal` | `lazuar-portal` | `lazuar-hub-portal` |
| `lazuar-admin` | `lazuar-admin` | `lazuar-hub-superadmin` |

`developers` ↔ `spec` and `superadmin` ↔ `admin` remain **asymmetric**. That is OK if intentional; call it out in deploy docs so no one assumes image rename is included **(B decision)**.

### 11.6 pnpm filter consumers outside repo

Any local scripts, shell history, or external runbooks using:

```bash
pnpm --filter developers-page dev
pnpm --filter ops-page build
```

must switch to new names. In-repo: `lazuar-docs` openapi guide is the known consumer **(A)**.

---

## 12. Suggested execution order (analysis only; not executed here)

1. Rename directories with `git mv`.
2. Update four `package.json` names.
3. Update four Dockerfiles (especially Next standalone paths).
4. Update `docker-bake.hcl`, both compose files (local), `ghcr.yml` matrix, `mprocs-dev.yaml`.
5. `pnpm install` to refresh lockfile importers.
6. Smoke: `pnpm --filter lazuar-ops build`, `lazuar-portal build`, `lazuar-spec build`, `lazuar-admin build`.
7. Smoke Docker bake/build for each target.
8. Update root README + lazuar-docs filter commands.
9. Optional: path comments, API comments, gap-doc bulk replace.
10. **Do not** touch cookies, localStorage keys, basePaths, Caddy paths, GHCR image names, or backend `/ops` `/admin` routes in the same change set unless product explicitly expands scope.

---

## 13. Negative findings (explicit absences)

| Search target | Result |
|---------------|--------|
| `@lazuar/(developers\|ops\|portal\|superadmin)` package imports | **None** |
| `packages/*` hardcoding `*-page` | **None** |
| User-Agent strings naming apps | **None** |
| Analytics tags naming apps | **None** |
| `turbo.json` package allowlists | **None** (generic tasks only) |
| Runtime absolute `/Users/.../apps/*-page` in app/API code | **None** |
| `developers-page` service in local `docker-compose.yml` | **Absent** (gap, not rename blocker) |
| Cookie named `ops-page` / `portal-page` / etc. | **None** |
| localStorage key containing `ops-page` or `portal-page` full package string | **None** (`ops_active_*` is product prefix only) |

---

## 14. Per-file raw inventory — Dockerfiles (every path token)

### 14.1 developers-page Dockerfile tokens **(A)**

- `COPY apps/developers-page/package.json apps/developers-page/`
- `pnpm install --filter ./apps/developers-page... --filter @repo/api-spec...`
- `COPY apps/developers-page apps/developers-page`
- `pnpm --filter ./apps/developers-page build`
- `COPY --from=build … /app/apps/developers-page/.next/standalone ./`
- `COPY --from=build … /app/apps/developers-page/.next/static ./apps/developers-page/.next/static`
- `COPY --from=build … /app/apps/developers-page/public ./apps/developers-page/public`
- `CMD ["node", "apps/developers-page/server.js"]`
- Healthcheck URL `/docs` **(B)**

### 14.2 portal-page Dockerfile tokens **(A)**

- `COPY apps/portal-page/package.json apps/portal-page/`
- `pnpm install --filter ./apps/portal-page...`
- `COPY apps/portal-page apps/portal-page`
- `pnpm --filter ./apps/portal-page build`
- standalone/static/public COPY triad with `apps/portal-page`
- `CMD ["node", "apps/portal-page/server.js"]`
- Healthcheck `/portal` **(B)**

### 14.3 ops-page Dockerfile tokens **(A)**

- `COPY apps/ops-page/package.json apps/ops-page/`
- `pnpm install --filter ./apps/ops-page...`
- `COPY apps/ops-page apps/ops-page`
- `pnpm --filter ./apps/ops-page build`
- `COPY --from=build … /app/apps/ops-page/dist ./dist`
- Runtime uses generic `serve -s dist` (no package path left at runtime)

### 14.4 superadmin-page Dockerfile tokens **(A)**

Symmetric to ops with `apps/superadmin-page` throughout.

---

## 15. Final classification summary table

| Category | Count / density | Rename action |
|----------|-----------------|---------------|
| **(A)** package.json names | 4 | Required |
| **(A)** Dockerfiles path tokens | 4 files, many lines | Required |
| **(A)** bake/compose/ghcr/mprocs | ~6 files | Required |
| **(A)** pnpm-lock importers | 4 keys | Required via install |
| **(A)** lazuar-docs filter/commands | ~4 files | Required for docs accuracy |
| **(A)** path comments in apps | ~15 lines | Optional hygiene |
| **(A)** API comments `ops-page` | 2 | Optional hygiene |
| **(B)** titles, cookies, localStorage, basePaths, ClientUrl, GHCR names | many | **Keep** |
| **(C)** Ops module, /admin API, portal product routes, domain ADRs | many | **Ignore** for package rename |
| **packages/*** | 0 hits | Nothing |

---

## 16. Appendix — command greps used (for reproducibility)

Patterns searched across the monorepo (representative):

- `developers-page|ops-page|portal-page|superadmin-page`
- `@lazuar/(developers|ops|portal|superadmin)`
- `lazuar_auth|lazuar_admin_auth|ops_active|lazuar-ops|lazuar-admin`
- `VITE_PORTAL|VITE_DOCS|NEXT_BASE_PATH|ClientUrl|CorsOrigins`
- `// apps/(developers-page|ops-page|portal-page|superadmin-page)`
- User-agent / analytics keywords (no package hits)
- Absolute `/Users/akmalfirdaus/Code/lazuar/.../apps/*-page` (docs only)

Workspace root: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`.

---

*End of 06-source-code-internal-refs.md — complete uncondensed analysis; no application code was modified.*
