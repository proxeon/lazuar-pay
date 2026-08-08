# 01 — Docker / GHCR / Compose rename impact

**Scope:** Docker local build, docker-compose, docker-bake, GHCR image names, container/service/target names, Dockerfile paths/contexts, OCI labels, ARG/ENV image-related build args, deploy compose, remote deploy script, GitHub Actions GHCR workflow.

**Proposed app directory renames:**

| Old path | Proposed path |
|----------|---------------|
| `apps/developers-page` | `apps/lazuar-spec` |
| `apps/ops-page` | `apps/lazuar-ops` |
| `apps/portal-page` | `apps/lazuar-portal` |
| `apps/superadmin-page` | `apps/lazuar-admin` |

**Out of scope for this file:** pnpm package renames, mprocs, turbo filters, application source renames, docs/ADR prose (except where they document Docker service hostnames). API (`apps/lazuar-api`) is unchanged by the four frontend renames but is listed for completeness where it shares image/compose infrastructure.

**Analysis date:** 2026-08-08  
**Repo root:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`

---

## 1. Executive summary of impact in this domain

### 1.1 Critical finding: multiple independent naming layers

The Docker/GHCR stack does **not** use a single name for each app. There are at least **six** parallel naming schemes that partially overlap. Renaming app directories only **forces** changes in a subset of them; the rest are optional product/branding decisions.

| Layer | Current naming pattern | Forced by folder rename? | Notes |
|-------|------------------------|--------------------------|-------|
| **A. App directory / monorepo path** | `apps/*-page` | **YES** | Source of truth for Dockerfile `COPY`, bake `dockerfile:`, compose `dockerfile:` |
| **B. Bake target names** | `ops-page`, `portal-page`, `superadmin-page`, `developers-page` | **YES (recommended)** | Used by `docker buildx bake <target>`; currently match folder basename |
| **C. Local/GHCR compose service keys** | `ops-page`, `portal-page`, `superadmin-page` | **Strongly recommended** | DNS name on compose network for that file; local stack only (no Caddy) |
| **D. Prod compose service keys** | `ops`, `portal`, `superadmin`, `developers` | **NO (unless you want it)** | Used by Caddy `reverse_proxy ops:3000` etc. |
| **E. Container names** | Local: `lazuar-ops`…; Prod: `hub-ops`… | **NO** | Used by `scripts/remote-deploy.sh` health gates |
| **F. GHCR image repository names** | `ghcr.io/proxeon/lazuar-hub-{ops,portal,superadmin,developers,api}` | **NO (folder rename alone)** | Flat GHCR package names; intentionally avoid nested packages; still branded **hub** not **pay** |

### 1.2 What breaks if folders move without updating Docker config

If you `git mv` the four apps and leave Docker files unchanged:

1. **`docker compose up --build` / `--profile full`** fails: `dockerfile: apps/ops-page/Dockerfile` (and siblings) not found.
2. **`docker buildx bake`** fails: same missing dockerfile paths; targets still named `*-page`.
3. **GitHub Actions `ghcr.yml`** matrix `file: apps/*/Dockerfile` fails for the four frontends.
4. **Dockerfile internal paths** fail even if you only fix the outer path: each Dockerfile hardcodes `apps/<old-name>/package.json`, `pnpm --filter ./apps/<old-name>...`, `COPY apps/<old-name>`, and for Next apps `CMD ["node", "apps/<old-name>/server.js"]` plus static COPY under `apps/<old-name>/.next/...`.
5. **Prod VPS pull path** continues to work **only if GHCR image names are left alone** — production does not mount monorepo paths; it pulls prebuilt images. Folder rename alone does not break a running VPS until the next **build** fails or you change image names without dual-publish.

### 1.3 What does *not* break from folder rename alone

- Running containers on the VPS (already pulled images).
- Caddy routing (uses prod service names `ops`, `portal`, `superadmin`, `developers` — not app folder names).
- `scripts/remote-deploy.sh` health checks on `hub-*` container names.
- GHCR packages already published under `lazuar-hub-*`.
- `.dockerignore` (no app-specific paths; uses broad globs).
- `pnpm-workspace.yaml` (`apps/*` glob — auto-picks new folders).

### 1.4 Mismatch between proposed app names and current GHCR image names

Proposed folder names and current image repos do **not** line up 1:1:

| Proposed app folder | Current GHCR image | Alignment |
|---------------------|--------------------|-----------|
| `lazuar-ops` | `lazuar-hub-ops` | Close; still has `hub` prefix |
| `lazuar-portal` | `lazuar-hub-portal` | Close; still has `hub` prefix |
| `lazuar-admin` | `lazuar-hub-superadmin` | **Mismatch** (`admin` vs `superadmin`) |
| `lazuar-spec` | `lazuar-hub-developers` | **Mismatch** (`spec` vs `developers`) |

**Recommendation for this domain:** treat **GHCR image rename** as a **separate, optional decision**. Folder rename can ship while continuing to publish/pull `lazuar-hub-*` images. Renaming GHCR packages requires dual-publish or cutover plan (see §5).

### 1.5 Secondary branding debt (hub vs pay)

Even without renaming the four apps, Docker/deploy still say **hub** everywhere:

- Image prefix: `lazuar-hub-*`
- Bake OCI source label: `https://github.com/proxeon/lazuar-hub`
- Compose project name: `lazuar-hub` (`docker-compose.ghcr.yml`, `deploy/prod/docker-compose.yml`)
- Server path: `/root/lazuar-hub-prod`
- Remote script path: `/root/lazuar-hub-remote-deploy.sh`
- Workflow concurrency: `lazuar-hub-cd-...`
- Public host still `hub.lazuar.com` in bake defaults / env examples

This is adjacent product rebranding (`lazuar-hub` → `lazuar-pay`), not strictly required for the four `*-page` folder renames, but it will confuse operators if folders become `lazuar-*` while images remain `lazuar-hub-*`.

### 1.6 Local stack incompleteness (pre-existing)

- Root `docker-compose.yml` and `docker-compose.ghcr.yml` define **ops / portal / superadmin** only — **no `developers-page` service**.
- `docker-bake.hcl` **and** prod `deploy/prod/docker-compose.yml` **and** `.github/workflows/ghcr.yml` **do** include developers.
- Rename work should not “lose” developers in bake/prod; optionally **add** `lazuar-spec` to local compose for parity.

### 1.7 Impact magnitude (this domain only)

| Category | Must-edit files | Optional / branding |
|----------|-----------------|---------------------|
| Bake | 1 (`docker-bake.hcl`) | Image tags/labels if rebranding GHCR |
| Local compose | 1–2 | Service keys, add developers/spec |
| GHCR compose | 0–1 (service keys only if desired) | Image refs if rebranding |
| Dockerfiles | 4 | — |
| `.dockerignore` | 0 | — |
| Deploy prod compose | 0 (folder-only) | Image refs / service names if rebrand |
| Caddyfile | 0 (folder-only) | If prod service keys change |
| remote-deploy.sh | 0 (folder-only) | If `container_name` / service keys change |
| GHCR workflow | 1 (dockerfile paths) | Matrix image `name` if rebrand |
| Taskfile docker tasks | 0 (folder-only; uses bake) | Echo strings if rebrand |
| **Total forced** | **~7–8 files** | + more if GHCR/service rebrand |

---

## 2. Inventory of every file/path that references old names (Docker domain)

Evidence is quoted with line references from the workspace at analysis time.

### 2.1 `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docker-bake.hcl`

**Role:** Multi-image build definitions for local bake and (conceptually) GHCR flat image names. Default group builds all five runtime images.

**Full relevant content summary:**

| Lines | Evidence | Rename impact |
|-------|----------|---------------|
| 1–7 | Comments list `ghcr.io/proxeon/lazuar-hub-{api,ops,portal,superadmin,developers}` | Optional if GHCR rebrand |
| 9–14 | Public path comments (`hub.lazuar.com`, `/portal`, `/docs`, `/admin`) | Unrelated to folder names |
| 17–18 | `REGISTRY` default `"ghcr.io/proxeon"` | Unrelated to `*-page` |
| 20–22 | `TAG` default `"latest"` | Unrelated |
| 24–42 | Build-arg variables `VITE_*`, `NEXT_*`, `VITE_BASE_PATH_ADMIN` | Unrelated to folder names |
| 44–46 | `PLATFORMS` default `linux/amd64` | Unrelated |
| 48–50 | `group "default" { targets = ["api", "portal-page", "ops-page", "superadmin-page", "developers-page"] }` | **MUST** rename targets in group |
| 52 | `target "docker-metadata-action" {}` | No change |
| 54–61 | `_common` labels: source `github.com/proxeon/lazuar-hub`, description “Lazuar Hub” | Optional hub→pay rebrand |
| 63–74 | `target "api"` → `apps/lazuar-api/Dockerfile`, tags `lazuar-hub-api` | Unchanged by 4 renames |
| 76–91 | `target "portal-page"`: `dockerfile = "apps/portal-page/Dockerfile"`, tags `lazuar-hub-portal`, label title | **MUST** path + target name; optional image |
| 93–109 | `target "ops-page"`: `apps/ops-page/Dockerfile`, tags `lazuar-hub-ops` | **MUST** path + target name; optional image |
| 111–126 | `target "superadmin-page"`: `apps/superadmin-page/Dockerfile`, tags `lazuar-hub-superadmin` | **MUST** path + target name; optional image |
| 128–142 | `target "developers-page"`: `apps/developers-page/Dockerfile`, tags `lazuar-hub-developers` | **MUST** path + target name; optional image |

**Exact target block names to rename (bake HCL identifiers):**

```hcl
target "portal-page" { ... }
target "ops-page" { ... }
target "superadmin-page" { ... }
target "developers-page" { ... }
```

**Proposed bake target names (recommended alignment with folders):**

```hcl
target "lazuar-portal" { ... }
target "lazuar-ops" { ... }
target "lazuar-admin" { ... }
target "lazuar-spec" { ... }
```

**Note:** Bake does not use package.json `name` fields; it only needs filesystem paths and target identifiers. Downstream CLI users of `docker buildx bake ops-page` would need to switch to the new target name.

---

### 2.2 `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docker-compose.yml`

**Role:** Local runtime stack. Default `up` = db + api. Profile `full` adds frontends built from monorepo Dockerfiles.

| Lines | Evidence | Rename impact |
|-------|----------|---------------|
| 1–4 | Comments; reference to `docker-compose.ghcr.yml` | None |
| 6–23 | `db` service, `container_name: lazuar-db` | None |
| 25–47 | `api`: `dockerfile: apps/lazuar-api/Dockerfile`, `image: ghcr.io/proxeon/lazuar-hub-api:local`, `container_name: lazuar-api` | None (api not in rename set) |
| **49–64** | **Service key `ops-page`:** `dockerfile: apps/ops-page/Dockerfile`, build args `VITE_API_URL` / `VITE_PORTAL_URL`, `image: ghcr.io/proxeon/lazuar-hub-ops:local`, `container_name: lazuar-ops`, ports `3003:3000`, profile `full` | **MUST** dockerfile path; **RECOMMENDED** service key → `lazuar-ops` (or keep short `ops`); image optional |
| **66–83** | **Service key `portal-page`:** `dockerfile: apps/portal-page/Dockerfile`, args `NEXT_PUBLIC_API_URL`, `image: .../lazuar-hub-portal:local`, `container_name: lazuar-portal`, env `API_URL: http://api:8080/api/v1`, ports `3004:3000` | **MUST** dockerfile path; **RECOMMENDED** service key; image optional |
| **85–99** | **Service key `superadmin-page`:** `dockerfile: apps/superadmin-page/Dockerfile`, `image: .../lazuar-hub-superadmin:local`, `container_name: lazuar-superadmin`, ports `3005:3000` | **MUST** dockerfile path; **RECOMMENDED** service key; image optional |
| — | **No `developers-page` / developers service** | Pre-existing gap; after rename consider adding `lazuar-spec` for full local parity |
| 101–106 | volumes / networks `lazuar-network` | None |

**Depends-on graph:** frontends `depends_on: api` (service key `api`). No frontend depends on another frontend by service name. Renaming frontend service keys does not break inter-service deps **within this file**.

**Compose DNS:** if anything resolved `http://ops-page:...` from another container, renaming the service key would break that. Grep of this file shows no such cross-frontend references; portal uses `http://api:8080/api/v1`.

---

### 2.3 `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docker-compose.ghcr.yml`

**Role:** Pull prebuilt linux/amd64 images from GHCR (no build section). Project name `lazuar-hub`.

| Lines | Evidence | Rename impact |
|-------|----------|---------------|
| 1–6 | Header comments | None |
| 8 | `name: lazuar-hub` | Optional hub rebrand |
| 11–28 | `db` | None |
| 30–49 | `api` → `ghcr.io/proxeon/lazuar-hub-api:${TAG:-latest}`, `container_name: lazuar-api` | None for page renames |
| **51–59** | **Service `ops-page`** → image `lazuar-hub-ops`, container `lazuar-ops`, port 3003 | **RECOMMENDED** service key rename for consistency; **image** only if GHCR rebrand |
| **61–72** | **Service `portal-page`** → `lazuar-hub-portal`, container `lazuar-portal`, port 3004, `API_URL: http://api:8080/api/v1` | Same |
| **74–82** | **Service `superadmin-page`** → `lazuar-hub-superadmin`, container `lazuar-superadmin`, port 3005 | Same |
| — | **No developers image/service** | Pre-existing gap vs bake/prod/CI |
| 84–89 | volumes/networks | None |

**Forced by folder rename alone:** **nothing** (no build context/dockerfile). Service keys still say `*-page` for human consistency; images still `lazuar-hub-*`.

---

### 2.4 `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/.dockerignore`

**Role:** Build context exclusions for monorepo-context Docker builds (`context: .` everywhere).

| Lines | Content | Rename impact |
|-------|---------|---------------|
| 1–46 | Globs: `.git`, `node_modules`, `.next`, `dist`, `docs`, `scripts`, `**/*.md`, `apps/lazuar-api/tests`, etc. | **None** — no `apps/*-page` hardcoding |

**Note:** Excluding `**/*.md` means Docker build context never includes README/AGENTS in apps; fine after rename. Excluding `scripts` means `scripts/remote-deploy.sh` is not in image context (correct; deploy is separate).

---

### 2.5 Dockerfiles for the four pages

All four use **monorepo root context** (`context: .` in compose/bake/CI). Paths inside Dockerfiles are relative to monorepo root.

#### 2.5.1 `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/ops-page/Dockerfile`

| Lines | Evidence | After rename to `apps/lazuar-ops` |
|-------|----------|----------------------------------|
| 11–13 | `COPY ... packages/...` + `COPY apps/ops-page/package.json apps/ops-page/` | → `apps/lazuar-ops/package.json apps/lazuar-ops/` |
| 15 | `pnpm install --filter ./apps/ops-page... --frozen-lockfile` | → `./apps/lazuar-ops...` |
| 17–19 | `COPY ... apps/ops-page apps/ops-page` | → `apps/lazuar-ops` |
| 21–28 | ARGs/ENVs `VITE_API_URL`, `VITE_PORTAL_URL`, `VITE_BASE_PATH`; `pnpm --filter ./apps/ops-page build` | filter path **MUST**; ARGs unchanged |
| 37 | `COPY --from=build ... /app/apps/ops-page/dist ./dist` | → `apps/lazuar-ops/dist` |
| 39–42 | HEALTHCHECK `/`, `CMD serve -s dist` | Unchanged (static serve of `dist`) |

**Does not** embed package.json `"name": "ops-page"` in Dockerfile; filter is path-based (`./apps/ops-page...`). Package name rename is still needed for pnpm filter-by-name elsewhere, but Docker uses path filters.

#### 2.5.2 `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/portal-page/Dockerfile`

| Lines | Evidence | After rename to `apps/lazuar-portal` |
|-------|----------|-------------------------------------|
| 13 | `COPY apps/portal-page/package.json apps/portal-page/` | **MUST** |
| 15 | `pnpm install --filter ./apps/portal-page...` | **MUST** |
| 19 | `COPY apps/portal-page apps/portal-page` | **MUST** |
| 21–27 | ARGs `NEXT_PUBLIC_API_URL`, `NEXT_BASE_PATH=/portal`; build filter | path **MUST**; basePath env **unchanged** (URL path, not folder) |
| 43–45 | Standalone COPY: `.next/standalone`, `.next/static` → `./apps/portal-page/.next/static`, `public` → `./apps/portal-page/public` | **MUST** (Next standalone preserves monorepo path) |
| 50 | HEALTHCHECK `http://127.0.0.1:3000/portal` | Unchanged (URL basePath) |
| 52 | `CMD ["node", "apps/portal-page/server.js"]` | **MUST** → `apps/lazuar-portal/server.js` |

**Critical:** Next.js `output: "standalone"` (see `apps/portal-page/next.config.ts`) produces `server.js` under `apps/<folder-name>/` inside the standalone tree. Renaming the app folder **without** updating Dockerfile COPY/CMD breaks the runtime image even if the build stage succeeds.

#### 2.5.3 `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/superadmin-page/Dockerfile`

| Lines | Evidence | After rename to `apps/lazuar-admin` |
|-------|----------|------------------------------------|
| 13 | `COPY apps/superadmin-page/package.json apps/superadmin-page/` | **MUST** |
| 15 | `pnpm install --filter ./apps/superadmin-page...` | **MUST** |
| 19 | `COPY apps/superadmin-page apps/superadmin-page` | **MUST** |
| 21–26 | ARGs `VITE_API_URL`, `VITE_BASE_PATH=/admin/`; build filter | path **MUST**; base path URL **unchanged** |
| 34 | `COPY ... /app/apps/superadmin-page/dist ./dist` | **MUST** |
| 37–39 | HEALTHCHECK `/`, serve dist | Unchanged |

#### 2.5.4 `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/developers-page/Dockerfile`

| Lines | Evidence | After rename to `apps/lazuar-spec` |
|-------|----------|-----------------------------------|
| 2 | Comment “Developer Hub … hub.lazuar.com/docs” | Optional comment |
| 11 | `COPY apps/developers-page/package.json apps/developers-page/` | **MUST** |
| 13 | `pnpm install --filter ./apps/developers-page... --filter @repo/api-spec...` | path **MUST**; `@repo/api-spec` unchanged |
| 17 | `COPY apps/developers-page apps/developers-page` | **MUST** |
| 22–26 | `NEXT_BASE_PATH=/docs`; build filter | path **MUST**; URL basePath **unchanged** |
| 39–41 | Standalone + static + public under `apps/developers-page/...` | **MUST** |
| 43 | `packages/api-spec/dist` → `./openapi-specs` | Unchanged |
| 48 | HEALTHCHECK `http://127.0.0.1:3000/docs` | Unchanged |
| 50 | `CMD ["node", "apps/developers-page/server.js"]` | **MUST** → `apps/lazuar-spec/server.js` |

**Same Next standalone path coupling as portal.**

#### 2.5.5 API Dockerfile (not in rename set)

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Dockerfile` — no references to the four `*-page` apps. No change required for this rename set.

---

### 2.6 Deploy: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/deploy/prod/`

#### 2.6.1 `deploy/prod/docker-compose.yml`

| Lines | Evidence | Folder rename? | GHCR rebrand? |
|-------|----------|----------------|---------------|
| 1–2 | Comment server path `/root/lazuar-hub-prod` | No | Optional path rebrand |
| 9 | `name: lazuar-hub` | No | Optional |
| 12–35 | `caddy` depends_on: `api`, `ops`, `portal`, `superadmin`, `developers` | No | Only if service keys renamed |
| 40–59 | `api` image `lazuar-hub-api`, container `hub-api` | No | Image optional |
| **61–69** | **Service `ops`** image `lazuar-hub-ops`, container `hub-ops` | No folder ref | Image optional; service key ties to Caddy |
| **71–86** | **Service `portal`** image `lazuar-hub-portal`, container `hub-portal`, `API_URL: http://api:8080/api/v1` | No folder ref | Same |
| **88–96** | **Service `superadmin`** image `lazuar-hub-superadmin`, container `hub-superadmin` | No folder ref | Same |
| **98–108** | **Service `developers`** image `lazuar-hub-developers`, container `hub-developers`, `OPENAPI_SPEC_ROOT` | No folder ref | Image optional; name `developers` ≠ proposed `lazuar-spec` |
| 110–116 | network `hub` | No | Optional |

**Important:** Prod service keys are **already short** (`ops`, not `ops-page`). Folder rename does **not** require prod compose edits unless you intentionally align service names to `lazuar-ops` / `lazuar-spec` (which would force Caddyfile + remote-deploy changes).

#### 2.6.2 `deploy/prod/Caddyfile`

| Lines | Upstream hostname | Maps to compose service |
|-------|-------------------|-------------------------|
| 8–10 | `api:8080` | `api` |
| 13–15 | `api:8080` | `api` |
| 18–20 | `portal:3000` | `portal` |
| 23–25 | `developers:3000` | `developers` |
| 30–32 | `superadmin:3000` | `superadmin` |
| 35–37 | `ops:3000` | `ops` |

**No `*-page` strings.** Folder rename alone: **no Caddyfile change**.  
If prod service keys are renamed (e.g. `developers` → `lazuar-spec` or `spec`), **every** `reverse_proxy` host must update or Caddy fails DNS resolution inside the Docker network.

#### 2.6.3 `deploy/prod/env.example`

| Lines | Relevant | Rename impact |
|-------|----------|---------------|
| 7 | `VERSION=latest` | None |
| 22–32 | URLs `hub.lazuar.com/portal`, `App__ApiBaseUrl`, `NEXT_PUBLIC_API_URL` | URL paths/host, not app folders |
| — | No image names, no `*-page` paths | **None** for folder rename |

#### 2.6.4 `deploy/prod/README.md`

Documents path routing table (`ops`, `portal`, `docs`, `api`, `admin`), GHCR login, sync to `/root/lazuar-hub-prod/`, secrets. Mentions `.github/workflows/ghcr.yml`. No dockerfile paths. **Docs-only** updates if product names change; not a runtime break.

---

### 2.7 `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/scripts/remote-deploy.sh`

| Lines | Evidence | Folder rename? |
|-------|----------|----------------|
| 4 | Usage comment `lazuar-hub-remote-deploy.sh` | No |
| 7 | `DIR=/root/lazuar-hub-prod` | No |
| 61 | Requires `docker-compose.yml` in DIR | No |
| 68–69 | `docker compose pull` + `up -d --remove-orphans` | Pulls images named in **server** compose |
| **71–76** | `wait_healthy hub-api`, `hub-ops`, `hub-portal`, `hub-superadmin`, `hub-developers`, `hub-caddy` | **Must match** `container_name` in prod compose — **not** app folders |
| 82–92 | Smoke curls `/health`, `/`, `/portal`, `/docs` via Host header | URL paths, not folders |
| 94 | `docker ps` table | None |

**Folder rename alone: no script change.**  
If `container_name` values change, this script **must** update or deploy health-gate fails and aborts.

---

### 2.8 GitHub workflows under `.github/`

#### 2.8.1 `.github/workflows/ghcr.yml` — **primary CD path**

| Lines | Evidence | Impact |
|-------|----------|--------|
| 1–5 | Comment images `lazuar-hub-api\|ops\|portal\|superadmin` (developers added in matrix) | Docs; matrix is source of truth |
| 16–29 | Triggers on `apps/**`, `packages/**`, `docker-bake.hcl`, `deploy/**`, `scripts/remote-deploy.sh`, workflow itself | Still correct after rename (path globs) |
| 47–48 | concurrency `lazuar-hub-cd-...` | Optional rebrand |
| 50–52 | `REGISTRY: ghcr.io`, `IMAGE_PREFIX: ghcr.io/${{ github.repository_owner }}` | Owner-scoped; currently resolves to `ghcr.io/proxeon` when owner is proxeon |
| 63–86 | **Matrix (critical):** | |
| | `lazuar-hub-api` → `apps/lazuar-api/Dockerfile` | Unchanged |
| | `lazuar-hub-portal` → **`apps/portal-page/Dockerfile`** + NEXT build args | **MUST** dockerfile path |
| | `lazuar-hub-ops` → **`apps/ops-page/Dockerfile`** + VITE build args | **MUST** dockerfile path |
| | `lazuar-hub-superadmin` → **`apps/superadmin-page/Dockerfile`** | **MUST** dockerfile path |
| | `lazuar-hub-developers` → **`apps/developers-page/Dockerfile`** | **MUST** dockerfile path |
| 99–108 | metadata tags: `latest`, `sha-<short>`, full sha | Unchanged |
| 109–121 | build-push: context `.`, `file: ${{ matrix.dockerfile }}` | Inherits matrix path fix |
| 161–171 | rsync `deploy/prod/` → `/root/lazuar-hub-prod/`, remote-deploy script | Unchanged by folder rename |
| 204–212 | SSH invoke remote deploy with `VERSION` | Unchanged |

**Note:** CI matrix builds **5 images including developers**, while local `docker-compose.yml` builds only 3 frontends. Bake default group matches CI (5). Keep that consistency after rename.

**Bake file is path-triggered but CI does not call bake** — it uses `docker/build-push-action` with matrix. Both bake **and** matrix must stay in sync for dockerfile paths and build-args.

#### 2.8.2 `.github/workflows/ci.yml`

| Content | Impact |
|---------|--------|
| contracts + dotnet jobs only | **No Docker image build**, no `*-page` dockerfile paths |
| Postgres service for tests | Unrelated |
| No references to ops-page / portal-page / etc. | **No change** for this domain |

---

### 2.9 `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/Taskfile.yml` (Docker tasks)

| Lines / task | Evidence | Impact |
|--------------|----------|--------|
| 4–22 | `infra:up/down/reset/logs` via `docker-compose` (db only) | None for page renames |
| 200–207 | `docker:builder` — buildx builder `lazuar-builder` | None |
| 209–219 | `docker:build` — `docker buildx bake --load` with `REGISTRY`/`TAG`/`PLATFORMS` | Uses **bake targets**; after target renames, default group still builds all if group list updated. Echo text says “api, portal, ops, superadmin” (**omits developers** though bake includes it) |
| 221–231 | `docker:build:api` — bake target `api` only | Unchanged |
| 233–243 | `docker:login:ghcr` | Unchanged |
| 245–256 | `docker:push` — bake `--push`, echo `lazuar-hub/*` | Image brand optional; bake target names if changed |
| 258–269 | `docker:push:api` | Unchanged for pages |
| 271–275 | `docker:up:ghcr` — compose.ghcr pull/up | Service keys if renamed |
| 277–280 | `docker:up:full` — `docker compose --profile full up -d --build` | Depends on local compose dockerfile paths |

**Taskfile does not hardcode `apps/*-page` paths**; it delegates to bake/compose. Updating bake + compose is sufficient for Taskfile **unless** someone adds explicit `bake ops-page` invocations later.

---

### 2.10 Complete GHCR image name reference inventory

All occurrences of the four frontend image repos (plus api for completeness):

| Image | Files (absolute) | Lines / usage |
|-------|------------------|---------------|
| `ghcr.io/proxeon/lazuar-hub-ops` | `docker-bake.hcl` | comments 4; tags 98–99; label 107 |
| | `docker-compose.yml` | image 56 `:local` |
| | `docker-compose.ghcr.yml` | image 52 |
| | `deploy/prod/docker-compose.yml` | image 62 |
| | `.github/workflows/ghcr.yml` | matrix name `lazuar-hub-ops` 72–73 |
| `ghcr.io/proxeon/lazuar-hub-portal` | `docker-bake.hcl` | comments 5; tags 81–82; label 89 |
| | `docker-compose.yml` | 72 |
| | `docker-compose.ghcr.yml` | 62 |
| | `deploy/prod/docker-compose.yml` | 72 |
| | `.github/workflows/ghcr.yml` | matrix 67–68 |
| `ghcr.io/proxeon/lazuar-hub-superadmin` | `docker-bake.hcl` | comments 6; tags 116–117; label 124 |
| | `docker-compose.yml` | 91 |
| | `docker-compose.ghcr.yml` | 75 |
| | `deploy/prod/docker-compose.yml` | 89 |
| | `.github/workflows/ghcr.yml` | matrix 78–79 |
| `ghcr.io/proxeon/lazuar-hub-developers` | `docker-bake.hcl` | comments 7; tags 133–134; label 140 |
| | `deploy/prod/docker-compose.yml` | 99 |
| | `.github/workflows/ghcr.yml` | matrix 83–84 |
| | **Missing from** root `docker-compose.yml` and `docker-compose.ghcr.yml` | gap |
| `ghcr.io/proxeon/lazuar-hub-api` | bake, both root composes, prod compose, ghcr matrix, Taskfile echo | out of four-page rename set |

**Registry owner:** hardcoded `proxeon` in compose/bake defaults; CI uses `github.repository_owner` (must match for pulls).

**Tag schemes:**

| Surface | Tags |
|---------|------|
| Bake | `${TAG}` + always also `latest` |
| Local compose | `:local` fixed |
| GHCR compose | `${TAG:-latest}` |
| Prod compose | `${VERSION:-latest}` |
| CI metadata-action | `latest` (default branch), `sha-<short>`, full `github.sha` |

---

### 2.11 Service name / container name / network inventory

#### Local (`docker-compose.yml` / `docker-compose.ghcr.yml`)

| Compose service key | container_name | Host port | Image (local / ghcr) | Profile |
|---------------------|----------------|-----------|----------------------|---------|
| `db` | `lazuar-db` | 5432 | postgres:16-alpine | default |
| `api` | `lazuar-api` | 8080 | `lazuar-hub-api:local` / `:${TAG}` | default |
| `ops-page` | `lazuar-ops` | 3003→3000 | `lazuar-hub-ops` | `full` (local only) |
| `portal-page` | `lazuar-portal` | 3004→3000 | `lazuar-hub-portal` | `full` (local only) |
| `superadmin-page` | `lazuar-superadmin` | 3005→3000 | `lazuar-hub-superadmin` | `full` (local only) |
| *(missing)* | — | — | developers | — |

Network: `lazuar-network` (named).

#### Prod (`deploy/prod/docker-compose.yml`)

| Compose service key | container_name | Published ports | Image | Caddy upstream |
|---------------------|----------------|-----------------|-------|----------------|
| `caddy` | `hub-caddy` | 80, 443, 443/udp | caddy:2-alpine | n/a |
| `api` | `hub-api` | expose 8080 | `lazuar-hub-api` | `api:8080` |
| `ops` | `hub-ops` | expose 3000 | `lazuar-hub-ops` | `ops:3000` |
| `portal` | `hub-portal` | expose 3000 | `lazuar-hub-portal` | `portal:3000` |
| `superadmin` | `hub-superadmin` | expose 3000 | `lazuar-hub-superadmin` | `superadmin:3000` |
| `developers` | `hub-developers` | expose 3000 | `lazuar-hub-developers` | `developers:3000` |

Network: `hub` (named).

#### Bake targets (not containers)

| Target | Dockerfile | Image tags |
|--------|------------|------------|
| `api` | `apps/lazuar-api/Dockerfile` | `lazuar-hub-api` |
| `portal-page` | `apps/portal-page/Dockerfile` | `lazuar-hub-portal` |
| `ops-page` | `apps/ops-page/Dockerfile` | `lazuar-hub-ops` |
| `superadmin-page` | `apps/superadmin-page/Dockerfile` | `lazuar-hub-superadmin` |
| `developers-page` | `apps/developers-page/Dockerfile` | `lazuar-hub-developers` |

---

### 2.12 Build-args / ENV inventory (image-related, not folder names)

These do **not** contain `*-page` folder names but are part of Docker image build contracts and must remain consistent when retargeting Dockerfiles.

| App | ARG / ENV | Default (Dockerfile or bake) | Purpose |
|-----|-----------|------------------------------|---------|
| ops | `VITE_API_URL` | `https://hub.lazuar.com/api/v1` | Browser API base (baked into Vite) |
| ops | `VITE_PORTAL_URL` | `https://hub.lazuar.com/portal` | Portal deep-link base |
| ops | `VITE_BASE_PATH` | `/` (bake) | Vite asset base |
| portal | `NEXT_PUBLIC_API_URL` | hub API URL | Client-visible API |
| portal | `NEXT_BASE_PATH` | `/portal` | Next basePath (URL) |
| portal (runtime compose) | `API_URL` | `http://api:8080/api/v1` | SSR server-side API inside Docker network |
| superadmin | `VITE_API_URL` | hub API URL | Browser API |
| superadmin | `VITE_BASE_PATH` | `/admin/` (bake var `VITE_BASE_PATH_ADMIN`) | Vite base under /admin |
| developers | `NEXT_BASE_PATH` | `/docs` | Next basePath (URL) |
| developers (runtime prod) | `OPENAPI_SPEC_ROOT` | `/app/openapi-specs` | Spec files path in container |

**Folder rename does not change these URL path bases** (`/portal`, `/docs`, `/admin/`). Confusing `lazuar-portal` (folder) with `/portal` (URL) should be avoided in docs.

---

### 2.13 Related non-Docker files that still mention `*-page` (context only)

These are **outside** pure Docker config but operators may hit them during the same rename PR. Listed for inventory completeness; full treatment belongs in other domain analyses:

| Path | Relevance |
|------|-----------|
| `mprocs-dev.yaml` | `cd apps/*-page` for local dev (not Docker) |
| `pnpm-lock.yaml` | importers `apps/developers-page` etc. |
| `apps/*/package.json` | `"name": "ops-page"` etc. — Docker uses path filters, but package renames should match folders |
| ADR `016-platform-domain-strategy.md` | Example Caddy: `reverse_proxy ops-page:3000` (stale vs prod which uses `ops:3000`) |
| Many docs under `docs/001-gaps/`, ADRs | Path references |

---

## 3. Mapping table: current vs proposed names

### 3.1 App directory & package (source of Dockerfile paths)

| Concept | Current | Proposed | Docker-forced? |
|---------|---------|----------|----------------|
| Developers app dir | `apps/developers-page` | `apps/lazuar-spec` | Yes |
| Ops app dir | `apps/ops-page` | `apps/lazuar-ops` | Yes |
| Portal app dir | `apps/portal-page` | `apps/lazuar-portal` | Yes |
| Superadmin app dir | `apps/superadmin-page` | `apps/lazuar-admin` | Yes |
| package.json name (developers) | `developers-page` | *recommend* `lazuar-spec` | Indirect (pnpm); Docker uses path |
| package.json name (ops) | `ops-page` | *recommend* `lazuar-ops` | Indirect |
| package.json name (portal) | `portal-page` | *recommend* `lazuar-portal` | Indirect |
| package.json name (superadmin) | `superadmin-page` | *recommend* `lazuar-admin` | Indirect |

### 3.2 Bake targets

| Current target | Proposed target | dockerfile path current → proposed |
|----------------|-----------------|------------------------------------|
| `api` | `api` (keep) | `apps/lazuar-api/Dockerfile` unchanged |
| `portal-page` | `lazuar-portal` | `apps/portal-page/...` → `apps/lazuar-portal/Dockerfile` |
| `ops-page` | `lazuar-ops` | `apps/ops-page/...` → `apps/lazuar-ops/Dockerfile` |
| `superadmin-page` | `lazuar-admin` | `apps/superadmin-page/...` → `apps/lazuar-admin/Dockerfile` |
| `developers-page` | `lazuar-spec` | `apps/developers-page/...` → `apps/lazuar-spec/Dockerfile` |

### 3.3 Local / GHCR compose service keys

| Current | Proposed (recommended for consistency) | Alternative (minimal churn) |
|---------|----------------------------------------|-----------------------------|
| `ops-page` | `lazuar-ops` | `ops` (align with prod) |
| `portal-page` | `lazuar-portal` | `portal` |
| `superadmin-page` | `lazuar-admin` | `superadmin` |
| *(missing developers)* | `lazuar-spec` (add) | `developers` (add) |

### 3.4 Prod compose service keys (already short)

| Current | Proposed folder-aligned? | Recommendation |
|---------|--------------------------|----------------|
| `ops` | could become `lazuar-ops` | **Keep `ops`** unless full rebrand; Caddy + depends_on + mental model stable |
| `portal` | could become `lazuar-portal` | **Keep `portal`** |
| `superadmin` | could become `lazuar-admin` or `admin` | **Keep `superadmin`** or rename carefully with Caddy |
| `developers` | could become `lazuar-spec` or `spec` | **Keep `developers`** unless intentional; Caddy uses `developers:3000` |

### 3.5 Container names

| Environment | Current | Proposed folder-only | If aligning to new product names |
|-------------|---------|----------------------|----------------------------------|
| Local ops | `lazuar-ops` | keep | keep (already good) |
| Local portal | `lazuar-portal` | keep | keep |
| Local superadmin | `lazuar-superadmin` | keep | optional → `lazuar-admin` |
| Local developers | *(n/a)* | add `lazuar-spec` if service added | — |
| Prod ops | `hub-ops` | keep | optional → `lazuar-ops` / `pay-ops` (requires remote-deploy) |
| Prod portal | `hub-portal` | keep | optional |
| Prod superadmin | `hub-superadmin` | keep | optional → `hub-admin` |
| Prod developers | `hub-developers` | keep | optional → `hub-spec` |

### 3.6 GHCR image repositories

| Current image | Folder-only rename | Optional aligned image names (decision required) |
|---------------|--------------------|--------------------------------------------------|
| `ghcr.io/proxeon/lazuar-hub-ops` | **keep** | `lazuar-ops` or `lazuar-pay-ops` |
| `ghcr.io/proxeon/lazuar-hub-portal` | **keep** | `lazuar-portal` or `lazuar-pay-portal` |
| `ghcr.io/proxeon/lazuar-hub-superadmin` | **keep** | `lazuar-admin` or `lazuar-pay-admin` |
| `ghcr.io/proxeon/lazuar-hub-developers` | **keep** | `lazuar-spec` or `lazuar-pay-spec` |
| `ghcr.io/proxeon/lazuar-hub-api` | **keep** | `lazuar-api` or `lazuar-pay-api` (out of page set but same brand family) |

### 3.7 OCI labels (`org.opencontainers.image.title`)

| Current | Folder-only | Optional rebrand |
|---------|-------------|------------------|
| `lazuar-hub-ops` | keep | match new image name |
| `lazuar-hub-portal` | keep | match |
| `lazuar-hub-superadmin` | keep | match |
| `lazuar-hub-developers` | keep | match |
| source URL `.../lazuar-hub` | keep | `.../lazuar-pay` if repo renamed on GitHub |

### 3.8 URL path bases (must **not** be confused with folder renames)

| Surface | Path | Tied to folder name? |
|---------|------|----------------------|
| Ops | `/` | No |
| Portal | `/portal` | No |
| Developers/docs | `/docs` | No |
| Superadmin | `/admin/` | No |
| API | `/api/*`, `/health` | No |

Renaming `portal-page` → `lazuar-portal` does **not** imply changing public URL `/portal`.

---

## 4. What must change for each workflow

### 4.1 Docker local build (`docker compose --profile full up -d --build` / Taskfile `docker:up:full`)

**Must:**

1. Rename directories (or ensure Dockerfiles live at new paths).
2. Update **each of the three existing frontend Dockerfiles** (and developers if building it) for all `apps/<old>/` path references, pnpm filters, and Next CMD/COPY paths.
3. Update `docker-compose.yml`:
   - `build.dockerfile` for ops/portal/superadmin → new paths.
   - Optionally rename service keys.
4. Update `docker-bake.hcl` if using Taskfile `docker:build` (default group targets + dockerfile paths).

**Should:**

5. Add `lazuar-spec` (developers) service to local compose for parity with bake/CI/prod.
6. Rename package.json `name` fields so path and package name stay aligned (helps humans; Docker path filters still work either way).

**Need not:**

7. Change GHCR image repository names (local compose tags `:local` under same repo names).
8. Change `container_name` values.
9. Change `.dockerignore`.

**Verification commands (after edits):**

```bash
# Single-target bake load (example)
docker buildx bake lazuar-ops --load REGISTRY=ghcr.io/proxeon TAG=local

# Full local stack
docker compose --profile full up -d --build
docker compose ps
curl -fsS -o /dev/null -w "%{http_code}\n" http://localhost:3003/
curl -fsS -o /dev/null -w "%{http_code}\n" http://localhost:3004/portal
curl -fsS -o /dev/null -w "%{http_code}\n" http://localhost:3005/
```

### 4.2 Compose up from GHCR (`docker compose -f docker-compose.ghcr.yml up -d` / Taskfile `docker:up:ghcr`)

**Must for folder rename alone:**

- **Nothing**, if images continue to be published under `lazuar-hub-*`.

**Should:**

- Rename service keys `*-page` → new names for operator consistency.
- Add developers/`lazuar-spec` service pulling `lazuar-hub-developers` (or new image name).

**Must if GHCR image repos renamed:**

- Update all `image:` lines.
- Ensure new packages exist and are pullable with existing `docker login ghcr.io`.
- Keep old tags available or document cutover `TAG`/`VERSION`.

### 4.3 Bake (`docker buildx bake` / Taskfile `docker:build` / `docker:push`)

**Must:**

1. Rename targets in `group "default"`.
2. Rename `target "…"` blocks (or keep old target names as aliases — HCL has no built-in alias; could duplicate targets temporarily pointing at new dockerfile paths if you need dual CLI names).
3. Update `dockerfile = "apps/…/Dockerfile"` for all four.
4. Ensure Dockerfiles at those paths are already path-fixed.

**Optional:**

5. Change `tags = ["${REGISTRY}/lazuar-hub-…"]` and labels.
6. Update source label from `lazuar-hub` to `lazuar-pay`.
7. Fix Taskfile echo that omits developers while bake includes it.

**Verification:**

```bash
task docker:builder
TAG=test REGISTRY=ghcr.io/proxeon task docker:build
docker images | grep lazuar-hub   # or new names
```

### 4.4 GHCR publish (`.github/workflows/ghcr.yml`)

**Must:**

1. Update matrix `dockerfile` paths for four frontends to `apps/lazuar-{ops,portal,admin,spec}/Dockerfile`.
2. Ensure monorepo `apps/**` path filter still covers new dirs (it does via glob).

**Optional / separate decision:**

3. Change matrix `name: lazuar-hub-ops` etc. → new image names (creates **new GHCR packages**).
4. Dual-publish: temporarily push both old and new image names for one release.
5. Update concurrency group string branding.

**Deploy half of workflow** (rsync prod compose, remote-deploy) continues to pull **whatever images prod compose specifies**. If CI renames packages but prod compose still lists `lazuar-hub-*`, deploy keeps working on old names until prod compose is updated — or fails if old names stop being pushed.

**Recommended publish rule during transition:**

| Phase | CI pushes | Prod compose pulls |
|-------|-----------|--------------------|
| A. Folder rename only | same `lazuar-hub-*` | same |
| B. Dual-publish | old + new tags/names | still old |
| C. Cutover | new only | new |
| D. Cleanup | new only | new; document old package deprecation |

### 4.5 Production VPS (`deploy/prod` + remote-deploy)

**Folder rename only:**

- No change required on server if CI still produces the same image names and tags.
- Next successful CD after matrix dockerfile path fix + Dockerfile path fix redeploys new code under same image names.

**If changing image names:**

1. Edit `deploy/prod/docker-compose.yml` `image:` lines.
2. Ensure VPS `docker login ghcr.io` can pull new packages (permissions on new package names).
3. Deploy once with dual availability or accept pull failures.
4. `scripts/remote-deploy.sh` unchanged unless `container_name` changes.

**If changing prod service keys:**

1. `deploy/prod/docker-compose.yml` service keys + `caddy.depends_on`.
2. `deploy/prod/Caddyfile` all `reverse_proxy <service>:port`.
3. Possibly remote-deploy only if containers renamed.

**If changing container names:**

1. Prod compose `container_name`.
2. `scripts/remote-deploy.sh` `wait_healthy hub-*` list.
3. Expect brief orphan containers until `--remove-orphans` (script already uses it).

---

## 5. Risks

### 5.1 Breaking running deploys

| Risk | Severity | When it triggers | Mitigation |
|------|----------|------------------|------------|
| CI matrix still points at old Dockerfile paths after `git mv` | **High** | First push to `main` after rename | Same PR: move apps + fix matrix + Dockerfiles + bake + local compose |
| Next.js standalone CMD path wrong | **High** | Image builds but container crash-loops | Update `CMD` and static COPY paths; healthcheck will fail in remote-deploy |
| GHCR package rename without dual-publish | **High** | Prod `docker compose pull` 404 | Dual-publish or update prod compose in same deploy |
| Prod service key rename without Caddy update | **High** | Caddy cannot resolve upstream | Atomic change: compose + Caddyfile together |
| Container rename without remote-deploy update | **Medium** | Health-gate dies looking for `hub-ops` | Update script in same rsync |
| Local developers missing forever | **Low** | Local DX only | Add service when convenient |
| Orphan containers after service rename | **Low** | Local/prod compose service rename | `up --remove-orphans` (prod script already) |
| Bake target rename breaks muscle memory / scripts | **Low** | External docs/scripts use `bake ops-page` | Document; temporary dual targets if needed |
| GHA cache scope keyed by `matrix.name` | **Low** | Image rename loses cache | Accept cold cache once |
| Private package permissions on new GHCR names | **Medium** | New package may default to private / need admin grant | Verify org package settings after first push |
| `IMAGE_PREFIX` owner vs hardcoded `proxeon` in compose | **Medium** (pre-existing) | Forks/other owners | Keep compose REGISTRY and CI owner aligned |
| Confusing URL `/portal` with folder `lazuar-portal` | **Low** | Human error changing `NEXT_BASE_PATH` | Leave URL bases alone |

### 5.2 Old image tags still referenced

- Prod may pin `VERSION=sha-xxxxxxx` in server `.env`.
- Those digests/tags remain on **old package names** forever unless deleted.
- After package rename, historical pins only work if old packages remain and are still pullable.
- `:latest` floating tag is dangerous during dual-name cutover if some hosts pull old and some new.

### 5.3 Pull paths

Current pull path (prod):

```text
ghcr.io/proxeon/lazuar-hub-{api,ops,portal,superadmin,developers}:${VERSION}
```

Local GHCR compose same with `TAG`.

No nested path (`ghcr.io/proxeon/lazuar-hub/ops`) — bake comments explicitly avoid nested-package 403 from `GITHUB_TOKEN`. **Keep flat names** if inventing new image names (`lazuar-pay-ops` not `lazuar-pay/ops`).

### 5.4 In-flight CI / concurrency

Workflow concurrency group `lazuar-hub-cd-${{ github.ref }}` with `cancel-in-progress: false` means deploys queue. A mid-rename broken commit on `main` will deploy broken images if build somehow succeeds partially (`fail-fast: true` on matrix helps). Prefer a **single atomic PR** for all forced Docker path updates.

### 5.5 Registry branding vs workspace path

Workspace is already `lazuar-pay` while images/deploy still say `lazuar-hub`. Operators searching GHCR for “pay” will not find packages. Separate cleanup track.

### 5.6 ADR / docs drift

`docs/architecture-decision-log/016-platform-domain-strategy.md` still shows `reverse_proxy ops-page:3000` / subdomain model; prod uses path-based host and short service names. Rename will increase doc drift unless ADR section is updated later (not runtime risk).

---

## 6. Recommended order of changes for this domain

### Phase 0 — Decisions (before editing)

1. Confirm the four folder names: `lazuar-spec`, `lazuar-ops`, `lazuar-portal`, `lazuar-admin`.
2. Decide **explicitly**: keep GHCR names `lazuar-hub-*` for now? (**Recommended: yes, keep.**)
3. Decide local compose service keys: rename to match folders vs align with prod short names.
4. Decide whether to add developers/`lazuar-spec` to root compose files in the same PR.

### Phase 1 — Filesystem + Dockerfiles (build correctness)

1. `git mv` the four app directories.
2. Update package.json `"name"` fields (same PR as monorepo rename domain).
3. Edit all four Dockerfiles:
   - Every `apps/<old>/` path
   - Every `pnpm --filter ./apps/<old>...`
   - Next: standalone COPY destinations + `CMD server.js` paths
4. Do **not** change `NEXT_BASE_PATH` / `VITE_BASE_PATH` URL values.

### Phase 2 — Bake + local compose + CI matrix (build entrypoints)

1. `docker-bake.hcl`: group list, target names, dockerfile paths (leave image tags as `lazuar-hub-*` unless Phase 4).
2. `docker-compose.yml`: dockerfile paths; optional service key renames; optional add `lazuar-spec`.
3. `.github/workflows/ghcr.yml`: matrix `dockerfile` paths only.
4. `docker-compose.ghcr.yml`: optional service key renames only.

### Phase 3 — Local verification (before merge)

1. `docker buildx bake <each frontend target> --load` (or Taskfile `docker:build`).
2. `docker compose --profile full up -d --build`.
3. Hit health endpoints / host ports 3003–3005; portal `/portal`; if spec added, `/docs`.
4. Confirm Next containers start (not crash on missing `server.js`).

### Phase 4 — Optional GHCR image rebrand (separate PR recommended)

1. Choose new flat names (`lazuar-ops` vs `lazuar-pay-ops`, etc.).
2. Dual-publish in CI matrix (two image names per app) for N releases.
3. Update bake tags/labels.
4. Update `deploy/prod/docker-compose.yml` image lines + root ghcr compose.
5. Deploy once; verify pull + health.
6. Remove dual-publish; document deprecation of `lazuar-hub-*` packages.
7. Optionally rename prod `container_name` / server dir / compose project name (hub → pay) as a third PR.

### Phase 5 — Prod service/Caddy renames (only if desired; avoid with folder rename)

1. Compose service keys + Caddyfile + depends_on atomically.
2. remote-deploy container waits only if container names change.
3. Prefer **not** bundling this with app directory renames.

### Suggested PR boundaries

| PR | Contents |
|----|----------|
| **PR1 (this domain min)** | `git mv` apps + Dockerfiles + bake dockerfile/targets + compose dockerfile paths + ghcr.yml matrix paths + package names |
| **PR2 (optional local DX)** | Add developers to root compose files; align service keys |
| **PR3 (optional GHCR rebrand)** | Image names dual-publish → cutover |
| **PR4 (optional hub→pay infra)** | Server path, compose project name, container_name hub-*, OCI source label |

---

## 7. Open questions

1. **GHCR image names:** Keep `lazuar-hub-*` indefinitely, or plan rename to match `lazuar-{ops,portal,admin,spec}` / `lazuar-pay-*`? Who owns GHCR package permissions for new names under `proxeon`?

2. **`lazuar-hub-superadmin` vs `lazuar-admin`:** If images are renamed, prefer `lazuar-admin`, `lazuar-superadmin`, or `lazuar-pay-admin`?

3. **`lazuar-hub-developers` vs `lazuar-spec`:** Image name `developers` matches product URL role (`/docs`); folder `lazuar-spec` matches TypeSpec/OpenAPI role. Should the image follow the folder (`spec`) or the product surface (`developers`/`docs`)?

4. **Local compose service naming convention:** Match new folder names (`lazuar-ops`), match prod short names (`ops`), or leave as-is until a later cleanup?

5. **Should root `docker-compose.yml` / `docker-compose.ghcr.yml` gain a developers/`lazuar-spec` service** as part of this rename, or track separately?

6. **Repo / product rebrand scope:** Is renaming GHCR packages and `/root/lazuar-hub-prod` part of “002-change-name” or a later “hub → pay” initiative? Bake still labels source as `github.com/proxeon/lazuar-hub`.

7. **GitHub repository name:** Does `github.repository_owner` / actual GitHub repo rename affect anything besides OCI source label? (CI `IMAGE_PREFIX` uses owner only, not repo name — good for flat packages.)

8. **Dual bake target aliases:** Do any external runbooks/scripts invoke `docker buildx bake ops-page` that need a deprecation window?

9. **Prod container names `hub-*`:** Keep for operational continuity, or align to `lazuar-*` / `pay-*` when rebranding?

10. **Next.js standalone path coupling:** Accept hardcoding `apps/lazuar-portal` in Dockerfile forever, or invest in a more portable standalone layout (usually not worth it; document the coupling)?

11. **Registry hardcoding:** Should compose files switch from hardcoded `ghcr.io/proxeon/...` to `${REGISTRY}/...` for consistency with bake variables?

12. **Tag policy:** Bake always also tags `latest`; CI tags `latest` only on default branch. Any change desired during rename? (No functional need.)

13. **Package visibility:** After any new GHCR package creation, confirm org settings (private/public) and that the VPS pull token (`GHCR_PULL_TOKEN`) can read them.

14. **Historical docs paths:** Are Docker-domain docs (deploy README, gap docs mentioning Docker bake) updated in the same rename epic or left stale intentionally?

---

## 8. Checklist — forced edits for folder rename only (copy/paste)

Use this as the implementation checklist for the **minimum** Docker-domain work.

### Dockerfiles (path internals)

- [ ] `apps/lazuar-ops/Dockerfile` (from ops-page) — all path/filter references
- [ ] `apps/lazuar-portal/Dockerfile` (from portal-page) — paths + standalone COPY + `CMD` `server.js`
- [ ] `apps/lazuar-admin/Dockerfile` (from superadmin-page) — all path/filter references
- [ ] `apps/lazuar-spec/Dockerfile` (from developers-page) — paths + standalone COPY + `CMD` `server.js`

### Orchestration entrypoints

- [ ] `docker-bake.hcl` — group targets + four target names + four `dockerfile` paths
- [ ] `docker-compose.yml` — three `dockerfile:` lines (and service keys if desired)
- [ ] `.github/workflows/ghcr.yml` — four matrix `dockerfile` values

### Explicitly out of minimum PR (unless deciding otherwise)

- [ ] GHCR image repository renames
- [ ] `deploy/prod/docker-compose.yml` image lines
- [ ] `deploy/prod/Caddyfile`
- [ ] `scripts/remote-deploy.sh`
- [ ] `.dockerignore`
- [ ] `.github/workflows/ci.yml`
- [ ] OCI labels / hub branding strings
- [ ] Adding developers to root compose files

---

## 9. Appendix — side-by-side “after folder rename” expected snippets

### 9.1 Bake target example (ops)

**Before:**

```hcl
target "ops-page" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/ops-page/Dockerfile"
  tags = [
    "${REGISTRY}/lazuar-hub-ops:${TAG}",
    "${REGISTRY}/lazuar-hub-ops:latest",
  ]
  ...
}
```

**After (folder-only; image name kept):**

```hcl
target "lazuar-ops" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/lazuar-ops/Dockerfile"
  tags = [
    "${REGISTRY}/lazuar-hub-ops:${TAG}",
    "${REGISTRY}/lazuar-hub-ops:latest",
  ]
  ...
}
```

### 9.2 GHCR workflow matrix row example (portal)

**Before:**

```yaml
- name: lazuar-hub-portal
  dockerfile: apps/portal-page/Dockerfile
  build_args: |
    NEXT_PUBLIC_API_URL=https://hub.lazuar.com/api/v1
    NEXT_BASE_PATH=/portal
```

**After (folder-only):**

```yaml
- name: lazuar-hub-portal
  dockerfile: apps/lazuar-portal/Dockerfile
  build_args: |
    NEXT_PUBLIC_API_URL=https://hub.lazuar.com/api/v1
    NEXT_BASE_PATH=/portal
```

### 9.3 Portal Dockerfile CMD example

**Before:** `CMD ["node", "apps/portal-page/server.js"]`  
**After:** `CMD ["node", "apps/lazuar-portal/server.js"]`

### 9.4 Prod compose (no change for folder-only)

```yaml
ops:
  image: ghcr.io/proxeon/lazuar-hub-ops:${VERSION:-latest}
  container_name: hub-ops
```

Remains valid after folder rename if CI continues publishing `lazuar-hub-ops`.

---

## 10. Summary table — file × action

| Absolute path | Action for folder rename | Action for GHCR rebrand | Action for prod service rename |
|---------------|--------------------------|-------------------------|--------------------------------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docker-bake.hcl` | **Edit** targets + dockerfile paths | Edit tags + labels + comments | n/a |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docker-compose.yml` | **Edit** dockerfile paths (+ optional service keys) | Edit `image:` lines | n/a (local) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docker-compose.ghcr.yml` | Optional service keys | **Edit** `image:` lines | n/a |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/.dockerignore` | None | None | None |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/ops-page/Dockerfile` → under `lazuar-ops` | **Edit** all internal paths | None | None |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/portal-page/Dockerfile` → under `lazuar-portal` | **Edit** paths + standalone + CMD | None | None |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/superadmin-page/Dockerfile` → under `lazuar-admin` | **Edit** all internal paths | None | None |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/developers-page/Dockerfile` → under `lazuar-spec` | **Edit** paths + standalone + CMD | None | None |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/deploy/prod/docker-compose.yml` | None | **Edit** `image:` | **Edit** service keys + depends_on + optional container_name |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/deploy/prod/Caddyfile` | None | None | **Edit** reverse_proxy hosts |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/deploy/prod/env.example` | None | None | None |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/deploy/prod/README.md` | Docs optional | Docs | Docs |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/scripts/remote-deploy.sh` | None | None | Edit if container_name changes |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/.github/workflows/ghcr.yml` | **Edit** matrix dockerfile paths | Edit matrix `name` (+ dual push) | None |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/.github/workflows/ci.yml` | None | None | None |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/Taskfile.yml` | None (delegates) | Optional echo strings | None |

---

*End of Docker / GHCR / Compose rename impact analysis.*
