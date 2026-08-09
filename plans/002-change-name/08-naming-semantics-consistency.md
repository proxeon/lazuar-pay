# 08 — Naming Semantics & Product Consistency Analysis

**Plan:** `002-change-name`  
**Focus:** Naming consistency and product semantics (not mechanical rename checklists)  
**Scope of evidence:** monorepo under `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` — `package.json` names, Docker/GHCR images, container names, UI document titles, ADR/domain language, backend module names, packages namespace conventions  
**Non-goals:** This document does **not** change application code. It evaluates proposed names and recommends a final naming table with rationale.

---

## 1. Proposed renames under evaluation

| Current app folder / package name | Proposed rename |
|-----------------------------------|-----------------|
| `developers-page` | `lazuar-spec` |
| `ops-page` | `lazuar-ops` |
| `portal-page` | `lazuar-portal` |
| `superadmin-page` | `lazuar-admin` |

Existing product-style apps already using the `lazuar-*` prefix (the pattern the rename is meant to match):

| App folder | `package.json` `"name"` | Role |
|------------|-------------------------|------|
| `apps/lazuar-api` | `lazuar-api` | .NET modular monolith API |
| `apps/lazuar-docs` | `lazuar-docs` | VitePress product + integrator guides |

The four `*-page` apps are the residual older naming scheme. The question is not merely “should we drop `-page`?” (yes, for consistency) but **which product noun** should replace it for each surface.

---

## 2. Inventory of existing naming layers (evidence)

Names in this monorepo are **not** one-to-one across layers. There are at least six parallel vocabularies:

1. **App directory + pnpm package name** (`apps/<name>`, `"name"` in `package.json`)
2. **Docker Compose service name** (often still `ops-page`, `portal-page`, …)
3. **Container `container_name`** (`lazuar-ops`, `hub-ops`, …)
4. **GHCR image name** (`ghcr.io/proxeon/lazuar-hub-<role>`)
5. **Public URL path / historical subdomain** (`/portal`, `/docs`, `/admin`, `ops.lazuar.com`, …)
6. **UI product title / browser `<title>`** (`Lazuar Ops`, `Lazuar Portal`, …)

A good rename makes (1) align with (3)–(6) and with product language in docs/ADRs, while remaining clearly distinct from packages and backend modules.

### 2.1 `package.json` `"name"` fields (apps + packages)

**Root**

- Workspace root: `"name": "lazuar"` (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/package.json`)

**Apps**

| Path | `"name"` |
|------|----------|
| `apps/lazuar-api/package.json` | `lazuar-api` |
| `apps/lazuar-docs/package.json` | `lazuar-docs` |
| `apps/developers-page/package.json` | `developers-page` |
| `apps/ops-page/package.json` | `ops-page` |
| `apps/portal-page/package.json` | `portal-page` |
| `apps/superadmin-page/package.json` | `superadmin-page` |

**Packages (two namespaces)**

| Path | `"name"` | Intent |
|------|----------|--------|
| `packages/api-spec` | `@repo/api-spec` | Private monorepo TypeSpec source of truth |
| `packages/api-types-ts` | `@repo/api-types-ts` | Generated TS clients |
| `packages/api-types-dotnet` | `@repo/api-types-dotnet` | Generated C# contracts |
| `packages/ui` | `@repo/ui` | Shared UI package |
| `packages/eslint-config` | `@repo/eslint-config` | Shared lint config |
| `packages/typescript-config` | `@repo/typescript-config` | Shared TS config |
| `packages/lhdn-sdk-ts` | `@lazuar/lhdn-sdk` | **Publishable** product SDK |

**Pattern already in force**

- **Deployable product apps** use bare names: `lazuar-api`, `lazuar-docs`, or legacy `*-page`.
- **Internal shared libraries** use `@repo/*`.
- **Externally publishable SDKs** use `@lazuar/*` (only LHDN SDK today).

Therefore: renaming apps to `lazuar-*` is consistent with `lazuar-api` / `lazuar-docs`. Renaming apps into `@repo/*` or `@lazuar/*` would **break** the product-app vs package distinction and should not be done.

### 2.2 Docker images (GHCR) and bake targets

From `docker-bake.hcl`:

```text
ghcr.io/proxeon/lazuar-hub-api
ghcr.io/proxeon/lazuar-hub-ops
ghcr.io/proxeon/lazuar-hub-portal
ghcr.io/proxeon/lazuar-hub-superadmin
ghcr.io/proxeon/lazuar-hub-developers
```

Public path comments in the same file:

```text
https://hub.lazuar.com/           ops
https://hub.lazuar.com/portal     portal
https://hub.lazuar.com/docs       developers
https://hub.lazuar.com/api/v1     api
https://hub.lazuar.com/admin      superadmin
```

Bake **targets** still use folder-style names: `api`, `portal-page`, `ops-page`, `superadmin-page`, `developers-page`.

**Semantic takeaway:** Docker has already partially productized the short roles `ops`, `portal`, `superadmin`, `developers` under a `lazuar-hub-*` image prefix. App folders lag behind images for the three frontends + developers hub. Notably, Docker never called the developers image `*-spec`; it is **`lazuar-hub-developers`**.

### 2.3 Compose container names

**Local / GHCR compose** (`docker-compose.yml`, `docker-compose.ghcr.yml`):

| Service key (legacy) | `container_name` | Image short role |
|----------------------|------------------|------------------|
| `api` | `lazuar-api` | `lazuar-hub-api` |
| `ops-page` | `lazuar-ops` | `lazuar-hub-ops` |
| `portal-page` | `lazuar-portal` | `lazuar-hub-portal` |
| `superadmin-page` | `lazuar-superadmin` | `lazuar-hub-superadmin` |
| `db` | `lazuar-db` | postgres |

**Production** (`deploy/prod/docker-compose.yml`) uses `hub-*` container names:

| Service | `container_name` | Image |
|---------|------------------|-------|
| `api` | `hub-api` | `lazuar-hub-api` |
| `ops` | `hub-ops` | `lazuar-hub-ops` |
| `portal` | `hub-portal` | `lazuar-hub-portal` |
| `superadmin` | `hub-superadmin` | `lazuar-hub-superadmin` |
| `developers` | `hub-developers` | `lazuar-hub-developers` |
| `caddy` | `hub-caddy` | caddy |

**Semantic takeaway:** Runtime identity is already `lazuar-ops` / `lazuar-portal` / `lazuar-superadmin` (local) and `hub-ops` / `hub-portal` / `hub-superadmin` / `hub-developers` (prod). The proposed app renames to `lazuar-ops` and `lazuar-portal` **close the gap** between folder name and container name. `lazuar-admin` would **diverge** from local container `lazuar-superadmin` and image `lazuar-hub-superadmin` unless those are also retargeted (recommended as a follow-up consistency pass, not a blocker for the app name decision).

### 2.4 UI product titles (browser / metadata)

| App | Evidence | User-facing product title |
|-----|----------|---------------------------|
| `ops-page` | `apps/ops-page/index.html` → `<title>Lazuar Ops</title>`; chat copy “Lazuar Ops”, “Lazuar Ops Core” | **Lazuar Ops** |
| `portal-page` | `apps/portal-page/src/app/layout.tsx` → `title: "Lazuar Portal"`; description “Secure checkout and buyer dashboard”; footer “Lazuar Platform” | **Lazuar Portal** |
| `superadmin-page` | `apps/superadmin-page/index.html` → `<title>Lazuar Admin</title>`; localStorage key `lazuar-admin-sidebar-collapsed`; platform page “Platform Gateway Vault” | **Lazuar Admin** (UI) / platform control plane (docs) |
| `developers-page` | `layout.tsx` metadata `title: "Lazuar API Documentation"`; hub chrome and H1: **“Lazuar Developer Hub”**; page titles like “Authentication — Lazuar Developer Hub” | **Lazuar Developer Hub** (primary brand) + “API Documentation” (secondary) |

**Semantic takeaway:** The UI already speaks `Lazuar Ops`, `Lazuar Portal`, `Lazuar Admin`, and `Lazuar Developer Hub`. The proposed `lazuar-ops` / `lazuar-portal` / `lazuar-admin` map cleanly onto those titles. **`lazuar-spec` does not appear anywhere as product language.**

### 2.5 Domain / path product language

**ADR 016** (historical three-tier domain strategy; still semantically authoritative for product nouns even though production routing shifted to path-based hub):

- `api.lazuar.com` → API  
- `ops.lazuar.com` → Superapp console (Vite CSR)  
- `portal.lazuar.com` → Transactional portal / cash register  

**Production path routing** (`deploy/prod/Caddyfile`, `deploy/prod/README.md`):

| Path | Service role |
|------|----------------|
| `/` | ops (creator console) |
| `/portal*` | portal (Next) |
| `/docs*` | developers (Scalar + hub guides) |
| `/api/*` | API |
| `/admin/*` | superadmin |

**Root README** product structure (still uses old folder names and subdomain fantasy):

```text
lazuar-api/       # The Brain  -> api.lazuar.com
ops-page/         # The Back-Office -> ops.lazuar.com
portal-page/      # The Cash Register -> portal.lazuar.com
superadmin-page/  # The Global Control Plane -> admin.lazuar.com
```

Developers-page is **omitted** from that README tree (called out as a DX gap in `docs/001-gaps/04-developers-page-dx.md`). Production mounts it at `/docs`, not at a dedicated `developers.*` host (ADR 007 examples once suggested `developers.lazuar.com`; ADR 016 never listed it).

### 2.6 Backend / TypeSpec “Ops” module (collision surface for `lazuar-ops`)

There is a real backend bounded context named **Ops**:

- Folder: `apps/lazuar-api/Modules/Ops/`
- Projects: `Modules.Ops.*`, tests `Modules.Ops.Tests`
- EF: `OpsDbContext`
- HTTP surface: `/ops/*` (chat stream, conversations, execute-action, …)
- TypeSpec docs entry: `packages/api-spec/docs-ops.tsp` → Scalar route on developers hub at `/ops` titled **“Lazuar Ops API”**
- ADR 014 describes Ops module as **AI agent orchestration** (hibernating), distinct from the full creator console

The **ops frontend** is a multi-module console (Commerce, Developer, Workspace, plus optional Ops chat UI). It is *not* “only the Ops backend module.” Historically the name “ops-page” meant **operations console for the tenant** (AWS-console analogy in README/ADRs), not “the UI for Modules/Ops only.”

**Collision assessment:** Medium conceptual overlap, **low practical confusion** if we keep hierarchical language:

| Layer | Name | Meaning |
|-------|------|---------|
| App | `lazuar-ops` | Tenant creator console product |
| Backend module | `Modules/Ops` | AI / tool-orchestration subdomain |
| API path | `/ops/*` | Ops-module endpoints |
| OpenAPI product card | “Ops Console API” (internal) | Same backend surface documented for operators |

This collision **already exists** today as `ops-page` vs `Modules/Ops`. Renaming to `lazuar-ops` does not make it worse; it slightly improves clarity by making the **product** noun match “Lazuar Ops” UI branding while the module stays under `Modules/Ops`.

**Do not rename** the backend module as part of this app rename. Mixing “rename product apps” with “rename DDD modules” is a different, higher-risk initiative.

### 2.7 “Portal” term usage (collision surface for `lazuar-portal`)

“Portal” is overloaded in a **deliberate product sense**:

| Usage | Meaning |
|-------|---------|
| App `portal-page` → proposed `lazuar-portal` | Buyer checkout + buyer dashboard product |
| Path `/portal` on hub | Mount point for that app |
| Route `/[tenantSlug]/portal` | Buyer-side fulfillment dashboard |
| Docs/README “cash register” / “transactional portal” | Same product metaphor (ADR 016, 017, 023) |
| Commerce API “public checkout/portal routes” | Integrator-facing public surfaces, not the app name |
| Stripe-style “customer portal” language | Generic industry term; not a separate Lazuar app |

**Collision assessment:** High frequency, **low ambiguity** among humans who know the product. Portal is the established product noun for the buyer SSR surface. Alternatives like `lazuar-checkout` would be more precise for MVP (ADR 023 pure CaaS) but would **fight** ADR 016/017, Caddy `/portal`, image `lazuar-hub-portal`, and UI title “Lazuar Portal.” **Keep portal.**

### 2.8 Superadmin / admin / platform language (collision surface for `lazuar-admin`)

Layered vocabulary today:

| Layer | Term used |
|-------|-----------|
| App folder / package | `superadmin-page` |
| Docker image | `lazuar-hub-superadmin` |
| Local container | `lazuar-superadmin` |
| Prod container / Caddy upstream | `superadmin` / path `/admin` |
| Browser title | **Lazuar Admin** |
| Cookie | `lazuar_admin_auth` (platform cookie; see auth gap docs) |
| localStorage | `lazuar-admin-sidebar-collapsed` |
| API routes | `/api/v1/platform/*` |
| Auth role | `SUPER_ADMIN` claim / `IsSystemAdmin` |
| Workspace membership string | also can be `SUPER_ADMIN` (workspace-scoped, not global) |
| Docs | “Global control plane”, “Platform Infrastructure Admin”, “superadmin” |

**Collision assessment:**

- **`lazuar-admin`** matches UI title, path `/admin`, cookie prefix `lazuar_admin_*`, and natural speech (“the admin app”).
- **`lazuar-superadmin`** matches Docker image/container and role jargon; it is more precise against tenant **OrgAdmin** confusion.
- Risk of `lazuar-admin`: someone might confuse “admin” with tenant org admins who use **Ops**, not Superadmin. Mitigated by path `/admin`, separate cookie, and platform-only routes.
- Risk of `lazuar-superadmin`: longer, fights existing UI title “Lazuar Admin”, over-emphasizes the *role* rather than the *product surface*.

**Recommendation preference:** `lazuar-admin` for the **app product name**, with docs continuing to say “platform superadmin role” for the capability. Optionally align image to `lazuar-hub-admin` later for full stack consistency.

### 2.9 Developers hub vs `lazuar-docs` vs `packages/api-spec` (critical for `lazuar-spec`)

Three different “docs/spec” things exist:

| Artifact | What it is | Audience |
|----------|------------|----------|
| `apps/lazuar-docs` | VitePress site: product + integrator **guides** | Humans reading prose |
| `apps/developers-page` | Next app: **Developer Hub** landing + Scalar **OpenAPI** + quickstarts/auth/webhooks guides | Integrators exploring live API refs |
| `packages/api-spec` (`@repo/api-spec`) | TypeSpec source + generated OpenAPI YAML | Engineers / codegen (`task gen`) |

Evidence of product language for developers-page:

- UI: “Lazuar Developer Hub”, “Lazuar API Documentation”
- ADR 007 title: **Product-Scoped API References (Developer Hub Segmentation)**
- Gap doc: “Developer Hub app”
- Docker image: `lazuar-hub-developers`
- Prod path: `/docs` (unfortunate historical alias; “docs” is generic)
- Product card on hub landing for internal surface: “Ops Console API” etc.

**Why `lazuar-spec` is a poor product name**

1. **Collides with `@repo/api-spec` / `packages/api-spec`.** Engineers will say “spec” and mean TypeSpec sources or OpenAPI YAML, not the Next.js hub app. Searching, onboarding, and AI context will mix them constantly.
2. **“Spec” is implementation jargon**, not the user-facing product noun. The product calls itself a **Developer Hub** and serves **API references** + guides.
3. **Does not match Docker (`developers`), path semantics (`/docs`), or ADR 007 (“Developer Hub”).**
4. **Ambiguity with future TypeSpec packaging.** If someone later publishes or renames the contract package, “spec” becomes even more contested.
5. **Does not distinguish from `lazuar-docs`.** Both “docs” and “spec” sound like documentation; the real split is *guides (VitePress)* vs *interactive API hub (Scalar)*. Names should encode that split, not deepen it.

**Alternatives evaluated for `developers-page`**

| Candidate | Pros | Cons | Verdict |
|-----------|------|------|---------|
| `lazuar-spec` | Short; hints OpenAPI | Collides with `api-spec`; not product language; not in Docker/UI | **Reject** |
| `lazuar-developers` | Matches Docker `lazuar-hub-developers`; clear audience; parallel to “Developer Hub” | Longer; slightly generic | **Strong accept** |
| `lazuar-devhub` | Compact; maps “Developer Hub” brand | Less formal; “devhub” slang | Acceptable alternate |
| `lazuar-api-reference` | Accurate for Scalar pages | Misses guides/quickstarts; very long; implies only OpenAPI | Weak |
| `developers-hub` | Matches UI brand | Drops `lazuar-*` prefix consistency with `lazuar-api` / `lazuar-docs` | Reject for monorepo apps |
| `lazuar-reference` | Clean | Still vague; could mean VitePress reference section | Weak |
| `lazuar-openapi` | Technically honest about Scalar | Undersells hub; couples name to one renderer | Reject |
| Keep `developers-page` | Zero rename cost | Breaks `lazuar-*` product pattern | Reject if rename wave proceeds |

**Recommended name for this app: `lazuar-developers`.**

If a shorter second choice is required: `lazuar-devhub`.

### 2.10 `lazuar-docs` vs production path `/docs`

Pre-existing semantic debt (not introduced by this rename, but relevant):

- App **`lazuar-docs`** = VitePress guides (often local-only / draft status per its README).
- Path **`/docs` on hub.lazuar.com** = **developers-page** (Scalar + hub), **not** VitePress.

Renaming developers-page to `lazuar-developers` **helps** this: folder name no longer pretends to be generic “docs,” and the path can stay `/docs` as a public URL convenience (Stripe-style “docs” URL) without the app being named docs/spec.

Do **not** rename `lazuar-docs` as part of this effort unless intentionally consolidating doc products.

---

## 3. Prefix consistency doctrine (apps vs packages)

### 3.1 Recommended doctrine (codify in the rename plan)

| Kind of artifact | Naming pattern | Examples (target end-state) |
|------------------|----------------|-----------------------------|
| Deployable product apps under `apps/` | `lazuar-<product-noun>` | `lazuar-api`, `lazuar-docs`, `lazuar-ops`, `lazuar-portal`, `lazuar-admin`, `lazuar-developers` |
| Private monorepo libraries under `packages/` | `@repo/<thing>` | `@repo/api-spec`, `@repo/api-types-ts`, `@repo/ui` |
| Publishable public SDKs under `packages/` | `@lazuar/<sdk>` | `@lazuar/lhdn-sdk` |
| Backend DDD modules | PascalCase under `Modules/` | `Modules/Ops`, `Modules/Commerce` |
| GHCR images | `lazuar-hub-<role>` (existing) | `lazuar-hub-ops`, … — optional later alignment of `superadmin` → `admin` |
| Drop suffix | No `-page` on product apps | Historical `*-page` means “frontend shell”; these are products |

### 3.2 Why not `@lazuar/*` for apps?

Apps are not imported as npm packages by external consumers. Using `@lazuar/ops` as a private workspace package name would:

- Blur the line with publishable SDKs.
- Force awkward pnpm filter strings vs simple `lazuar-ops`.
- Diverge from already-shipped `lazuar-api` / `lazuar-docs` bare names.

### 3.3 Why not keep `*-page`?

The `-page` suffix:

- Understates products (Ops is a multi-module console; Portal is SSR checkout; Admin is a control plane; Developers is a hub).
- Is inconsistent with `lazuar-api` and `lazuar-docs`.
- Encourages treating frontends as disposable “pages” rather than product surfaces governed by ADRs (013, 016, 017, 023).

---

## 4. Per-rename evaluation

### 4.1 `ops-page` → `lazuar-ops`

| Dimension | Assessment |
|-----------|------------|
| Match to UI title | Excellent — “Lazuar Ops” |
| Match to Docker image role | Excellent — `lazuar-hub-ops`, container `lazuar-ops` |
| Match to product docs | Excellent — README “Back-Office”, ADR “Superapp Console”, creator dashboard |
| Collision with backend `Modules/Ops` | Acceptable, pre-existing; document hierarchy |
| Collision with `/ops` API paths | Acceptable; ops-page already calls `/ops/*` among many modules |
| Consistency with `lazuar-*` apps | Excellent |

**Opinion:** This is the cleanest of the four renames. Adopt **`lazuar-ops`**.

Product one-liner for docs after rename:  
*“`lazuar-ops` is the tenant creator console (Lazuar Ops). It is not the same artifact as backend `Modules/Ops` (AI orchestration).”*

### 4.2 `portal-page` → `lazuar-portal`

| Dimension | Assessment |
|-----------|------------|
| Match to UI title | Excellent — “Lazuar Portal” |
| Match to Docker / path | Excellent — `lazuar-hub-portal`, `/portal` |
| Match to ADRs | Excellent — ADR 016/017 portal architecture |
| Overload of “portal” | High frequency, intentional product term |
| MVP accuracy (checkout-only) | Slightly broad vs pure checkout, but product strategy still uses “portal” for buyer dashboard + checkout |

**Opinion:** Adopt **`lazuar-portal`**. Do not switch to `lazuar-checkout` unless product deliberately rebrands away from “Portal.”

### 4.3 `superadmin-page` → `lazuar-admin`

| Dimension | Assessment |
|-----------|------------|
| Match to UI title | Excellent — already “Lazuar Admin” |
| Match to path `/admin` | Excellent |
| Match to cookie/localStorage | Strong — `lazuar_admin_auth`, `lazuar-admin-*` |
| Match to Docker image | Weak today — image is `lazuar-hub-superadmin` |
| Role precision | Weaker than `lazuar-superadmin` |
| Confusion with tenant OrgAdmin | Mild; mitigated by platform cookie + `/platform/*` |

**Alternatives:**

- `lazuar-superadmin`: better Docker/role alignment; worse UI/product branding.
- `lazuar-platform`: matches `/platform/*` API prefix and “platform control plane” docs; risks sounding like the whole platform (API+all apps).

**Opinion:** Adopt **`lazuar-admin`**. Prefer a follow-up (not blocking) to rename image/container `superadmin` → `admin` for stack consistency. Keep role name `SUPER_ADMIN` / `IsSystemAdmin` unchanged in backend.

### 4.4 `developers-page` → `lazuar-spec` (proposed) vs better options

| Dimension | `lazuar-spec` | `lazuar-developers` (recommended) |
|-----------|---------------|-------------------------------------|
| UI brand “Developer Hub” | Poor | Good |
| Docker `lazuar-hub-developers` | Poor | Excellent |
| Collision with `@repo/api-spec` | **Severe** | None |
| Distinction from `lazuar-docs` | Weak (both “doc-ish”) | Strong (audience = developers) |
| ADR 007 language | Poor | Good |
| Future SDK / OpenAPI packaging | Contaminates “spec” word | Leaves “spec” for contracts |

**Opinion:** **Reject `lazuar-spec`.** Adopt **`lazuar-developers`**.

---

## 5. Product semantics map (what each app *is*)

After rename, the monorepo product story should read as a coherent CaaS platform (ADR 021/023), not a “pages” folder:

```text
lazuar-api          The brain — modular monolith API (api / /api)
lazuar-ops          Creator console — configure commerce, keys, webhooks (ops / /)
lazuar-portal       Buyer cash register — checkout + buyer dashboard (/portal)
lazuar-admin        Platform control plane — cross-tenant / system gateways (/admin)
lazuar-developers   Integrator hub — guides + product-scoped Scalar OpenAPI (/docs)
lazuar-docs         Prose docs site — VitePress product/integrator guides (local/draft)
```

**Audience split (important for naming):**

| App | Primary human |
|-----|----------------|
| `lazuar-ops` | Tenant org admin / creator staff |
| `lazuar-portal` | End buyer / subscriber |
| `lazuar-admin` | Lazuar platform operator (system admin) |
| `lazuar-developers` | External integrator / ERP developer |
| `lazuar-docs` | Integrator + product reader (prose) |
| `lazuar-api` | Machines + all of the above via HTTP |

This audience model is why **admin ≠ ops** and why **developers ≠ docs ≠ api-spec**.

---

## 6. Collision risk register

| Risk ID | Names | Severity | Notes | Mitigation |
|---------|-------|----------|-------|------------|
| C1 | `lazuar-ops` app vs `Modules/Ops` | Medium | Pre-existing; AI/chat module vs full console | Docs glossary; never rename modules in same PR |
| C2 | `lazuar-ops` vs OpenAPI product “Ops Console API” | Low | Internal Scalar surface | Keep “Ops Console API” as API product title |
| C3 | `lazuar-portal` vs buyer route `.../portal` | Low | Same product | No action |
| C4 | `lazuar-portal` vs Commerce “portal routes” | Low | API language | Optional future API wording cleanup only |
| C5 | `lazuar-admin` vs tenant OrgAdmin | Medium | Shared English “admin” | Path `/admin`, cookie `lazuar_admin_auth`, copy “Platform Admin” |
| C6 | `lazuar-admin` vs role `SUPER_ADMIN` | Low | Role ≠ app | Keep role strings; document mapping |
| C7 | `lazuar-admin` vs image `lazuar-hub-superadmin` | Medium (ops debt) | Stack inconsistency | Follow-up image/container rename |
| C8 | **`lazuar-spec` vs `@repo/api-spec`** | **High** | **Why proposed name fails** | **Do not use `lazuar-spec`** |
| C9 | developers app vs `lazuar-docs` vs path `/docs` | Medium (pre-existing) | Path mounts developers, not VitePress | Rename app to `lazuar-developers`; leave path; clarify in README |
| C10 | Root workspace `"name": "lazuar"` vs apps `lazuar-*` | Low | Normal monorepo | No change |
| C11 | Repo folder `lazuar-pay` / historical `lazuar-hub` branding | Medium (org-level) | Images still `lazuar-hub-*`, compose project `lazuar-hub` | Out of scope for app rename; note for brand plan |
| C12 | Package `@lazuar/lhdn-sdk` vs apps `lazuar-*` | Low | Different layers by design | Preserve doctrine |

---

## 7. Alignment scorecard (proposed vs recommended)

Scoring: **5** = perfect semantic fit with existing product language; **1** = fights existing language or creates high collision.

| Current | Proposed | Score | Recommended | Score | Notes |
|---------|----------|-------|-------------|-------|-------|
| `ops-page` | `lazuar-ops` | **5** | `lazuar-ops` | **5** | Same; adopt |
| `portal-page` | `lazuar-portal` | **5** | `lazuar-portal` | **5** | Same; adopt |
| `superadmin-page` | `lazuar-admin` | **4** | `lazuar-admin` | **4** | Prefer over `lazuar-superadmin` (3.5) for UI/path; accept Docker lag |
| `developers-page` | `lazuar-spec` | **1.5** | `lazuar-developers` | **4.5** | Spec collides with api-spec; developers matches Docker + hub |

---

## 8. Recommended final naming table

### 8.1 App renames (authoritative recommendation)

| Current folder / `package.json` name | **Recommended final name** | Rejected / secondary | Rationale (short) |
|--------------------------------------|----------------------------|----------------------|-------------------|
| `ops-page` | **`lazuar-ops`** | — | Matches UI “Lazuar Ops”, image `lazuar-hub-ops`, container `lazuar-ops`, product “creator console” |
| `portal-page` | **`lazuar-portal`** | `lazuar-checkout` (only if rebrand) | Matches UI, path `/portal`, ADR 016/017, image `lazuar-hub-portal` |
| `superadmin-page` | **`lazuar-admin`** | `lazuar-superadmin` (Docker-literal alternate) | Matches UI “Lazuar Admin”, path `/admin`, cookie `lazuar_admin_*`; role stays `SUPER_ADMIN` |
| `developers-page` | **`lazuar-developers`** | **`lazuar-spec` (reject)**; secondary `lazuar-devhub` | Matches Developer Hub brand + Docker `lazuar-hub-developers`; avoids `api-spec` collision |

### 8.2 Names that should **not** change in this rename wave

| Artifact | Keep as | Why |
|----------|---------|-----|
| `apps/lazuar-api` | `lazuar-api` | Already canonical |
| `apps/lazuar-docs` | `lazuar-docs` | Different product (VitePress); not the Scalar hub |
| `packages/api-spec` / `@repo/api-spec` | unchanged | Contract source of truth; owns the word “spec” |
| `packages/*` `@repo/*` | unchanged | Package namespace doctrine |
| `@lazuar/lhdn-sdk` | unchanged | Publishable SDK doctrine |
| `Modules/Ops` | unchanged | Backend BC; not a product app |
| Public paths `/portal`, `/docs`, `/admin`, `/api` | unchanged unless product rebrands URLs | URL stability > folder aesthetics |
| Role `SUPER_ADMIN`, cookie names | unchanged in same wave | Auth compatibility |

### 8.3 Optional follow-ups (consistency, not blockers)

| Follow-up | From | To | Why optional |
|-----------|------|----|--------------|
| GHCR image superadmin | `lazuar-hub-superadmin` | `lazuar-hub-admin` | Align with app `lazuar-admin`; requires registry + deploy cutover |
| Local container superadmin | `lazuar-superadmin` | `lazuar-admin` | Same |
| Prod compose service/container | `superadmin` / `hub-superadmin` | `admin` / `hub-admin` | Same |
| Bake target names | `ops-page` etc. | `lazuar-ops` etc. | Mirror app folders after move |
| Compose service keys | `ops-page` | `lazuar-ops` or short `ops` | Prod already uses short `ops` |
| README project tree | include developers; use new names | — | Fix omission of developers hub |
| Glossary ADR or README section | Ops app vs Ops module | — | Reduce C1 confusion |

### 8.4 End-state `apps/` tree (recommended)

```text
apps/
├── lazuar-api/          # package name: lazuar-api
├── lazuar-docs/         # package name: lazuar-docs
├── lazuar-ops/          # was ops-page
├── lazuar-portal/       # was portal-page
├── lazuar-admin/        # was superadmin-page
└── lazuar-developers/   # was developers-page  (NOT lazuar-spec)
```

### 8.5 Suggested glossary blurb (for root README after rename)

Use something equivalent to:

> **Naming:** Product apps are `lazuar-<surface>`. Shared libraries are `@repo/*`. Publishable SDKs are `@lazuar/*`.  
> **Ops** means the creator console app (`lazuar-ops`) unless qualified as **Ops module** (`Modules/Ops`, AI tools).  
> **Admin** means the platform control plane (`lazuar-admin` at `/admin`), not a tenant OrgAdmin in Ops.  
> **Developers** means the public integrator hub (`lazuar-developers` at `/docs`). **Docs** (`lazuar-docs`) is the VitePress prose site. **Spec** means TypeSpec/OpenAPI contracts in `packages/api-spec`, never an app.

---

## 9. Opinion summary (uncondensed)

### 9.1 What the rename is really solving

The monorepo is mid-transition:

- Backend and two apps already speak **product** (`lazuar-api`, `lazuar-docs`).
- Four frontends still speak **implementation scaffolding** (`*-page`).
- Docker and UI titles already speak product nouns (`ops`, `portal`, `admin`/`superadmin`, `developers`).

The rename should complete the productization of app folders so that:

- `pnpm --filter lazuar-ops` matches mental model and container `lazuar-ops`.
- New contributors stop asking “is ops-page a single page?”
- AI/docs context stops treating these as random Vite/Next toys.

### 9.2 Three proposed names are good; one is a footgun

- **`lazuar-ops`**: Correct. Do it.
- **`lazuar-portal`**: Correct. Do it.
- **`lazuar-admin`**: Correct enough, and better than keeping `superadmin-page`. Prefer it over `lazuar-superadmin` because the *product* is already branded Admin while *superadmin* is a *role*. Accept temporary Docker string drift or schedule image rename.
- **`lazuar-spec`**: Incorrect product semantics. It steals the word owned by `@repo/api-spec`, ignores “Developer Hub” branding, and fails to match `lazuar-hub-developers`. This would create permanent onboarding confusion between “edit the spec package” and “run the developers app.”

### 9.3 Why not over-rotate to pure role or pure path names?

- Naming every app after its path (`lazuar-docs` for `/docs`) is wrong because `/docs` is already ambiguous with VitePress.
- Naming every app after its auth role (`lazuar-superadmin`) overfits backend claims.
- Naming every app after its framework (`lazuar-vite-ops`) is an anti-pattern already being escaped.

The right noun is the **product surface noun** used in UI and ADRs.

### 9.4 Hub branding note (out of scope but relevant)

Images and prod compose still say **Lazuar Hub** (`lazuar-hub-*`, `name: lazuar-hub`, `hub.lazuar.com`), while the workspace path is `lazuar-pay` and root package is `lazuar`. That brand layering (Hub vs Pay vs Platform) is a **separate** naming decision. App renames should use the stable brand prefix **`lazuar-`**, not `lazuar-hub-` folders (hub is an image/deploy namespace, not an app folder namespace).

### 9.5 Consistency with CaaS pivot (ADR 021/023)

Product truth watermark: Compliance / Checkout-as-a-Service, not 15-app superapp.

- Ops remains the **creator console** for commerce/dunning/keys — name `lazuar-ops` still fits “operations console” without implying 15 modules.
- Portal remains the **transactional buyer surface** — name `lazuar-portal` still fits.
- Developers hub is more important post-pivot (API-first CaaS) — name should advertise **developers**, not **spec**.
- Admin remains thin platform vault UI — `lazuar-admin` is proportionate (not “superapp admin”).

### 9.6 Final recommendation in one sentence

**Rename `ops-page` → `lazuar-ops`, `portal-page` → `lazuar-portal`, `superadmin-page` → `lazuar-admin`, and `developers-page` → `lazuar-developers` (not `lazuar-spec`); keep packages on `@repo/*` / `@lazuar/*` and keep backend `Modules/Ops` unchanged.**

---

## 10. Evidence index (paths cited)

| Concern | Paths |
|---------|-------|
| App package names | `apps/*/package.json`, root `package.json` |
| Package namespaces | `packages/*/package.json` |
| Docker images / paths | `docker-bake.hcl`, `docker-compose.yml`, `docker-compose.ghcr.yml`, `deploy/prod/docker-compose.yml` |
| Prod routing | `deploy/prod/Caddyfile`, `deploy/prod/README.md` |
| UI titles | `apps/ops-page/index.html`, `apps/superadmin-page/index.html`, `apps/portal-page/src/app/layout.tsx`, `apps/developers-page/app/layout.tsx`, `apps/developers-page/app/page.tsx`, `apps/developers-page/app/components/HubShell.tsx` |
| Product structure language | root `README.md` |
| Domain strategy | `docs/architecture-decision-log/016-platform-domain-strategy.md` |
| Portal architecture | `docs/architecture-decision-log/017-portal-frontend-architecture.md` |
| Developer hub segmentation | `docs/architecture-decision-log/007-product-scoped-api-references.md` |
| CaaS pivot / UI lobotomy | `docs/architecture-decision-log/021-compliance-caas-pivot.md`, `023-pure-caas-mvp-ui-lobotomy.md` |
| Developers DX / surfaces | `docs/001-gaps/04-developers-page-dx.md` |
| VitePress vs Scalar | `apps/lazuar-docs/README.md` |
| Backend Ops module | `apps/lazuar-api/Modules/Ops/`, `Taskfile.yml` Ops migrations/tests |
| TypeSpec ops docs | `packages/api-spec/package.json` build script (`docs-ops.tsp`) |
| mprocs process names | `mprocs-dev.yaml` |

---

## 11. Decision table for implementers

| Decision | Choice |
|----------|--------|
| Use `lazuar-*` prefix for the four apps? | **Yes** |
| Drop `-page` suffix? | **Yes** |
| `ops-page` final name | **`lazuar-ops`** |
| `portal-page` final name | **`lazuar-portal`** |
| `superadmin-page` final name | **`lazuar-admin`** |
| `developers-page` final name | **`lazuar-developers`** |
| Accept proposed `lazuar-spec`? | **No** |
| Prefer `lazuar-superadmin` over `lazuar-admin`? | **No** (admin wins) |
| Rename `@repo/api-spec` as part of this? | **No** |
| Rename `Modules/Ops` as part of this? | **No** |
| Rename GHCR images in same PR? | Optional follow-up; not required for folder rename correctness |
| Change public URL paths? | **No** (unless product rebrand) |

---

*End of naming semantics analysis. Mechanical rename blast radius and file-by-file checklists belong in sibling plan docs under `plans/002-change-name/`, not in this file.*
