# 07 — Deployment Runtime Rename Impact

**Scope:** `deploy/` directory, root `docker-compose*.yml`, `docker-bake.hcl`, `scripts/remote-deploy.sh`, GHCR workflow wiring that feeds production, reverse proxy (Caddy), env examples, volumes, networks, healthchecks, and production migration strategy for renaming monorepo frontend apps while live traffic continues.

**Proposed monorepo renames (app directories / package identity):**

| Old app directory | Proposed name |
|-------------------|---------------|
| `apps/developers-page` | `apps/lazuar-spec` |
| `apps/ops-page` | `apps/lazuar-ops` |
| `apps/portal-page` | `apps/lazuar-portal` |
| `apps/superadmin-page` | `apps/lazuar-admin` |

**Out of scope for this document:** application source code behavior, TypeSpec contracts, package internal imports beyond what Docker build contexts require. No app code changes are recommended here; this is an impact + migration plan only.

**Key finding (executive):** Production path-based routing on `hub.lazuar.com` already uses **short compose service names** (`ops`, `portal`, `superadmin`, `developers`) and **GHCR image names** (`lazuar-hub-ops`, etc.) that are **not identical** to the monorepo folder names (`ops-page`, etc.). Renaming the four app directories therefore hits **build paths and local compose** hard, while **production Caddy hostnames and public URL paths can stay unchanged** unless you deliberately choose to rename services/images as well.

---

## 1. Inventory of deployment runtime artifacts

### 1.1 Files that form the production control plane

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/deploy/prod/docker-compose.yml` | Production Compose project (`name: lazuar-hub`). Pulls GHCR images; defines service DNS used by Caddy. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/deploy/prod/Caddyfile` | Reverse proxy: single host `hub.lazuar.com` path routing. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/deploy/prod/env.example` | Template for server `/root/lazuar-hub-prod/.env` (secrets + `VERSION` pin). |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/deploy/prod/README.md` | Runbook: first-time setup, worker replica rules, secrets. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/scripts/remote-deploy.sh` | On-VPS: pin `VERSION`, `docker compose pull/up`, health-gate by **container_name**, Caddy reload, smoke curls. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/.github/workflows/ghcr.yml` | Build/push matrix → GHCR; rsync `deploy/prod/` + `remote-deploy.sh`; SSH deploy. |

### 1.2 Files that form local / GHCR-pull developer stacks

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docker-compose.yml` | Local build stack: `db` + `api` always; frontends under profile `full`. Service names = old app names (`ops-page`, …). **No `developers-page` service.** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docker-compose.ghcr.yml` | Pull prebuilt GHCR images for local/server-without-Caddy-in-compose. Service names = old app names. **No developers image.** Project `name: lazuar-hub`. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docker-bake.hcl` | Multi-image bake targets named after old apps; tags `ghcr.io/proxeon/lazuar-hub-*`. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/Taskfile.yml` | `docker:build`, `docker:push`, `docker:up:ghcr`, `docker:up:full` wrappers around bake/compose. |
| `apps/*/Dockerfile` (four frontends + api) | Build context paths hardcode `apps/<old-name>/…`; Next standalone CMD embeds that path. |

### 1.3 Reverse proxy / edge

| Technology | Present? | Location |
|------------|----------|----------|
| **Caddy** | Yes (production) | `deploy/prod/Caddyfile` + `caddy:2-alpine` service |
| Traefik | No | — |
| nginx | No as reverse proxy | Vite images use Node `serve`; no nginx config in repo |
| Host-level Caddy outside Compose | Documented historically for published ports | Root compose comments say “Host Caddy reverse_proxies published ports”; **current prod embeds Caddy in compose** |

### 1.4 Env files

| Path | Committed? | Notes |
|------|------------|-------|
| `deploy/prod/env.example` | Yes | Copied on VPS to `.env` |
| `deploy/prod/.env` | No (rsync excludes; never commit) | Live secrets on `/root/lazuar-hub-prod/.env` |
| Root `.env` / `.env.example` | Not part of deploy/prod contract | Local optional vars for compose build args (`VITE_*`, `NEXT_PUBLIC_*`, R2_*) |
| GitHub secret `HUB_ENV_FILE` | Optional full `.env` body overwrite on deploy | Workflow can clobber server `.env` |

No env key names currently embed `ops-page`, `portal-page`, `superadmin-page`, or `developers-page`. Public URL env values use path segments (`/portal`, `/api/v1`) and host `hub.lazuar.com`, not app directory names.

---

## 2. Current production topology (as coded)

### 2.1 Compose project and server layout

- **Compose project name:** `lazuar-hub` (`name: lazuar-hub` in `deploy/prod/docker-compose.yml`).
- **Server directory:** `/root/lazuar-hub-prod` (hardcoded in workflow rsync and `remote-deploy.sh` default `DIR`).
- **Remote deploy script install path:** `/root/lazuar-hub-remote-deploy.sh`.
- **Public host:** `hub.lazuar.com` (path-based hub; DNS A → VPS; grey-cloud for ACME).
- **Only published ports:** Caddy `80`, `443`, `443/udp`. All apps use `expose` only (internal Docker network).
- **Network:** Docker network name `hub` (`networks.hub.name: hub`).
- **Volumes:** `caddy_data`, `caddy_config` (TLS certs / Caddy config state). **No named volumes for the four frontend apps.** API uses external Neon (connection strings in `.env`), not a local Postgres volume in prod compose.

### 2.2 Service name ↔ container_name ↔ image ↔ upstream port

| Compose service | `container_name` | Image | Internal port | Health mechanism |
|-----------------|------------------|-------|---------------|------------------|
| `caddy` | `hub-caddy` | `caddy:2-alpine` | 80/443 | Deploy waits for `running` (no Docker healthcheck defined) |
| `api` | `hub-api` | `ghcr.io/proxeon/lazuar-hub-api:${VERSION}` | 8080 | Compose healthcheck: `curl -fsS http://127.0.0.1:8080/health` |
| `ops` | `hub-ops` | `ghcr.io/proxeon/lazuar-hub-ops:${VERSION}` | 3000 | Image HEALTHCHECK (wget `/`); deploy waits healthy/running |
| `portal` | `hub-portal` | `ghcr.io/proxeon/lazuar-hub-portal:${VERSION}` | 3000 | Image HEALTHCHECK (wget `/portal`) |
| `superadmin` | `hub-superadmin` | `ghcr.io/proxeon/lazuar-hub-superadmin:${VERSION}` | 3000 | Image HEALTHCHECK (wget `/`) — works with Caddy `handle_path` strip |
| `developers` | `hub-developers` | `ghcr.io/proxeon/lazuar-hub-developers:${VERSION}` | 3000 | Image HEALTHCHECK (wget `/docs`) |

**Critical coupling:** Caddyfile upstreams use **compose service DNS names**, not container_names:

```
api:8080
portal:3000
developers:3000
superadmin:3000
ops:3000
```

If you rename compose services without updating `Caddyfile` in the same atomic deploy, the proxy will fail to resolve upstreams.

### 2.3 Public path → service mapping (must stay stable for zero customer impact)

From `deploy/prod/Caddyfile` and `deploy/prod/README.md`:

| Public URL path | Backend service | App identity (monorepo) |
|-----------------|-----------------|-------------------------|
| `https://hub.lazuar.com/` | `ops:3000` | ops-page → lazuar-ops |
| `https://hub.lazuar.com/portal*` | `portal:3000` (Next `basePath=/portal`) | portal-page → lazuar-portal |
| `https://hub.lazuar.com/docs*` | `developers:3000` (Next `basePath=/docs`) | developers-page → lazuar-spec |
| `https://hub.lazuar.com/admin` → `/admin/` | `superadmin:3000` via `handle_path` strip | superadmin-page → lazuar-admin |
| `https://hub.lazuar.com/api/*` | `api:8080` | lazuar-api (unchanged by this rename) |
| `https://hub.lazuar.com/health` | `api:8080` | deploy smoke / liveness |

**Domain hostnames mapping conclusion:** There is **no per-app subdomain** in the live Caddyfile. ADR 016 still describes `ops.lazuar.com` / `portal.lazuar.com` / `api.lazuar.com` as an older/parallel conceptual model; **production code is single-host path routing**. Renaming monorepo folders does **not** require DNS changes if public paths remain `/`, `/portal`, `/docs`, `/admin`, `/api`.

Build-time URLs baked into frontend images (from bake/workflow):

| Build arg | Typical production value |
|-----------|--------------------------|
| `VITE_API_URL` | `https://hub.lazuar.com/api/v1` |
| `VITE_PORTAL_URL` | `https://hub.lazuar.com/portal` |
| `NEXT_PUBLIC_API_URL` | `https://hub.lazuar.com/api/v1` |
| `NEXT_BASE_PATH` (portal) | `/portal` |
| `NEXT_BASE_PATH` (developers) | `/docs` |
| `VITE_BASE_PATH` (ops) | `/` |
| `VITE_BASE_PATH` (superadmin) | `/admin/` |

These are **URL path / host strings**, not monorepo package names. They survive the rename unless product deliberately changes public paths.

### 2.4 Production env surface (`env.example` / live `.env`)

Relevant keys (none reference old app directory names):

| Variable | Consumer | Rename impact |
|----------|----------|---------------|
| `VERSION` | Image tag interpolation in compose | None from folder rename; still `latest` or `sha-<7>` |
| `ConnectionStrings__*` | API only | None |
| `Jwt__*` / `Kms__MasterKey` | API | None |
| `App__ClientUrl` | API (`…/portal`) | Path string, not app name |
| `App__ApiBaseUrl` | API public base for webhooks | None |
| `App__CorsOrigins` | API CORS | Host only |
| `NEXT_PUBLIC_API_URL` | Portal runtime env (and baked at build) | None |
| `OPENAPI_SPEC_ROOT` | developers container (compose hardcodes `/app/openapi-specs`) | Path inside image; independent of package name |
| R2 / Resend / AI / Messaging flags | API | None |

`portal` service also sets in-compose:

```yaml
API_URL: http://api:8080/api/v1   # Docker network service name "api"
```

That depends on service name `api`, not on frontend renames.

---

## 3. Local / GHCR-pull stacks (divergence from production)

### 3.1 Root `docker-compose.yml`

| Service | Build dockerfile | Image tag (local) | container_name | Host port | Profile |
|---------|------------------|-------------------|----------------|-----------|---------|
| `db` | postgres:16-alpine | — | `lazuar-db` | 5432 | default |
| `api` | `apps/lazuar-api/Dockerfile` | `ghcr.io/proxeon/lazuar-hub-api:local` | `lazuar-api` | 8080 | default |
| `ops-page` | `apps/ops-page/Dockerfile` | `…/lazuar-hub-ops:local` | `lazuar-ops` | 3003→3000 | `full` |
| `portal-page` | `apps/portal-page/Dockerfile` | `…/lazuar-hub-portal:local` | `lazuar-portal` | 3004→3000 | `full` |
| `superadmin-page` | `apps/superadmin-page/Dockerfile` | `…/lazuar-hub-superadmin:local` | `lazuar-superadmin` | 3005→3000 | `full` |
| *(missing)* developers | — | — | — | — | — |

**Observations:**

1. **Service names match old monorepo folders** (`ops-page`, etc.) — these **must** change when folders rename if dockerfiles move, or at least dockerfile paths must update.
2. **`container_name` values already use the proposed branding** for three apps: `lazuar-ops`, `lazuar-portal`, `lazuar-superadmin`. That is accidental alignment; production still uses `hub-*`.
3. **No developers / lazuar-spec service** in local compose — gap today; rename is a chance to add `lazuar-spec` if desired (not required for rename correctness).
4. Network: `lazuar-network` (named). Volume: `pgdata`.

### 3.2 `docker-compose.ghcr.yml`

Same four app services as local (again **no developers**), but **pull-only** images:

| Service | Image |
|---------|-------|
| `ops-page` | `ghcr.io/proxeon/lazuar-hub-ops:${TAG:-latest}` |
| `portal-page` | `ghcr.io/proxeon/lazuar-hub-portal:${TAG:-latest}` |
| `superadmin-page` | `ghcr.io/proxeon/lazuar-hub-superadmin:${TAG:-latest}` |
| *(missing)* | `ghcr.io/proxeon/lazuar-hub-developers` exists in GHCR pipeline but not consumed here |

Publishes host ports `3003`, `3004`, `3005`, `8080`, `5432` for external host reverse proxy. Project name also `lazuar-hub` → volume/network namespace collisions if both root compose and prod-style stacks run on one machine.

### 3.3 Bake targets (`docker-bake.hcl`)

Group `default` targets (names = monorepo-ish identifiers):

```
api, portal-page, ops-page, superadmin-page, developers-page
```

Each target:

- `dockerfile = "apps/<old-name>/Dockerfile"`
- Tags: `ghcr.io/proxeon/lazuar-hub-<role>:${TAG}` and `:latest`
- Labels `org.opencontainers.image.title = lazuar-hub-<role>`
- Labels source still `https://github.com/proxeon/lazuar-hub` (repo branding separate from folder renames)

**Rename requirement:** After moving directories, **dockerfile paths and ideally bake target names** should follow (`lazuar-ops`, `lazuar-portal`, `lazuar-admin`, `lazuar-spec`). Image **repository names on GHCR** can stay `lazuar-hub-*` for zero prod pull disruption (recommended) or be renamed with dual-tag strategy (section 8).

### 3.4 Dockerfiles — hard path embedding (highest build breakage risk)

#### ops-page → lazuar-ops

- `COPY apps/ops-page/package.json apps/ops-page/`
- `pnpm install --filter ./apps/ops-page...`
- `COPY apps/ops-page apps/ops-page`
- `pnpm --filter ./apps/ops-page build`
- Runtime: `COPY … /app/apps/ops-page/dist ./dist`
- HEALTHCHECK: `wget … http://127.0.0.1:3000/`

#### portal-page → lazuar-portal

- Same filter/COPY pattern under `apps/portal-page`
- Standalone layout:
  - `COPY …/apps/portal-page/.next/standalone ./`
  - static → `./apps/portal-page/.next/static`
  - public → `./apps/portal-page/public`
- **CMD:** `node apps/portal-page/server.js`
- HEALTHCHECK: `wget …/portal`

Next.js standalone output **embeds the monorepo relative app path** in the runtime filesystem. Renaming the directory without updating Dockerfile COPY destinations and CMD will produce a broken image even if build succeeds partially.

#### superadmin-page → lazuar-admin

- Same pattern as ops (Vite + `serve`)
- HEALTHCHECK: `wget …/` (root of static server; Caddy strips `/admin` prefix)

#### developers-page → lazuar-spec

- Filters: `./apps/developers-page...` + `@repo/api-spec`
- Standalone:
  - static → `./apps/developers-page/.next/static`
  - public → `./apps/developers-page/public`
- **CMD:** `node apps/developers-page/server.js`
- HEALTHCHECK: `wget …/docs`
- Specs: `OPENAPI_SPEC_ROOT=/app/openapi-specs` (compose also sets this)

### 3.5 GHCR workflow matrix (`.github/workflows/ghcr.yml`)

| `matrix.name` (GHCR image repo suffix) | `dockerfile` path today |
|----------------------------------------|-------------------------|
| `lazuar-hub-api` | `apps/lazuar-api/Dockerfile` |
| `lazuar-hub-portal` | `apps/portal-page/Dockerfile` |
| `lazuar-hub-ops` | `apps/ops-page/Dockerfile` |
| `lazuar-hub-superadmin` | `apps/superadmin-page/Dockerfile` |
| `lazuar-hub-developers` | `apps/developers-page/Dockerfile` |

Tags published: `latest` (default branch), `sha-<short>`, full `github.sha`.

Deploy job:

1. rsync `deploy/prod/` → `/root/lazuar-hub-prod/` (**excludes `.env`**)
2. rsync `scripts/remote-deploy.sh` → `/root/lazuar-hub-remote-deploy.sh`
3. Optional `HUB_ENV_FILE` overwrite of `.env`
4. Optional GHCR login with pull token
5. SSH: `VERSION=sha-xxxxxxx /root/lazuar-hub-remote-deploy.sh`

**Concurrency group:** `lazuar-hub-cd-${{ github.ref }}` with `cancel-in-progress: false` (no overlapping deploys cancelled mid-flight).

### 3.6 `remote-deploy.sh` health-gate names

Hardcoded `docker inspect` targets:

```
hub-api, hub-ops, hub-portal, hub-superadmin, hub-developers, hub-caddy
```

Smoke HTTP (Host: `hub.lazuar.com` against localhost):

```
/health, /, /portal, /docs
```

(No `/admin` smoke today.)

**If `container_name` values change**, this script **must** change in the same deploy that recreates containers, or health-gate will hang / fail.

---

## 4. Name taxonomy — three layers that must not be confused

Production and monorepo use **three different naming layers**. The proposed rename primarily targets layer A.

| Layer | Examples today | Proposed app rename effect |
|-------|----------------|----------------------------|
| **A. Monorepo paths / package names / bake targets / local compose services** | `apps/ops-page`, service `ops-page`, bake `ops-page` | **Must change** |
| **B. Production compose services + Caddy DNS** | `ops`, `portal`, `superadmin`, `developers` | **Optional**. Keep stable for zero Caddy churn. |
| **C. GHCR image repositories + container_name prefixes** | `lazuar-hub-ops`, container `hub-ops` | **Optional**. Keep stable for dual-pull safety. |

### Recommended production strategy (minimal risk)

1. Rename **layer A only** (folders, Dockerfiles, bake targets, local compose service names, workflow `dockerfile:` paths).
2. **Keep layer B service names** (`ops`, `portal`, `superadmin`, `developers`) in `deploy/prod/docker-compose.yml` and Caddyfile.
3. **Keep layer C image names** (`ghcr.io/proxeon/lazuar-hub-ops` etc.) and `hub-*` container names.

Result: production `docker compose pull && up` continues to pull the same image repos; only the **contents** of images change because Dockerfiles now build from `apps/lazuar-*`. No Caddyfile edit required. No dual-tag image migration required for correctness.

### Optional alignment strategy (more churn)

If the product wants full branding alignment:

| Current | Optional new |
|---------|--------------|
| Image `lazuar-hub-ops` | `lazuar-ops` or `lazuar-pay-ops` |
| Image `lazuar-hub-portal` | `lazuar-portal` |
| Image `lazuar-hub-superadmin` | `lazuar-admin` |
| Image `lazuar-hub-developers` | `lazuar-spec` |
| Service `ops` | `lazuar-ops` |
| Service `developers` | `lazuar-spec` |
| container `hub-ops` | `lazuar-ops` (matches local) |

That requires dual-tag push period + simultaneous Caddy + remote-deploy updates (section 8).

---

## 5. File-by-file change matrix (deploy runtime only)

### 5.1 Must change when directories rename (build will fail otherwise)

| File | What to update |
|------|----------------|
| `apps/lazuar-ops/Dockerfile` (moved) | All `apps/ops-page` path segments, pnpm filters |
| `apps/lazuar-portal/Dockerfile` | Paths + `CMD ["node", "apps/lazuar-portal/server.js"]` + static COPY destinations |
| `apps/lazuar-admin/Dockerfile` | All `apps/superadmin-page` path segments |
| `apps/lazuar-spec/Dockerfile` | All `apps/developers-page` path segments + CMD server.js path |
| `docker-bake.hcl` | `dockerfile` paths; target names in `group.default`; comments |
| `.github/workflows/ghcr.yml` | matrix `dockerfile:` paths (image `name:` can stay) |
| `docker-compose.yml` | `dockerfile:` paths; preferably service keys `ops-page`→`lazuar-ops` etc. |
| `docker-compose.ghcr.yml` | Service keys (cosmetic/consistency); image names can stay |
| `Taskfile.yml` | Echo strings only if they list old names; bake target names if bake group changes |
| `mprocs-dev.yaml` | `cd apps/...` paths (dev runtime, not prod) |

### 5.2 Should change only if renaming production compose services / containers / images

| File | Coupling |
|------|----------|
| `deploy/prod/docker-compose.yml` | `image:`, service keys, `container_name`, `depends_on`, portal `API_URL` host if `api` renamed (not proposed) |
| `deploy/prod/Caddyfile` | `reverse_proxy <service>:<port>` hostnames |
| `scripts/remote-deploy.sh` | `wait_healthy hub-*` names; smoke paths if public routes change |
| `deploy/prod/README.md` | Path table wording; image names if changed |
| `deploy/prod/env.example` | Only if public URLs or image pin scheme changes |

### 5.3 No change required for monorepo folder rename alone

| Artifact | Why safe |
|----------|----------|
| DNS for `hub.lazuar.com` | Host unchanged |
| Caddy path matchers `/portal*`, `/docs*`, `/admin/*`, `/api/*` | Path product surface, not folder names |
| Named volumes `caddy_data`, `caddy_config` | Unrelated to app dirs |
| Network `hub` | Unrelated |
| Neon / connection strings | Unrelated |
| JWT / KMS / BYOK env keys | Unrelated |
| Public webhook URLs under `App__ApiBaseUrl` | API path, not frontend rename |
| GHA cache scopes keyed by `matrix.name` (`lazuar-hub-ops` etc.) | Stay valid if image names kept |

### 5.4 `docker-compose.ghcr.yml` service name table (local pull stack)

| Current service | Suggested after rename | Image (keep) | container_name (already close) |
|-----------------|------------------------|--------------|--------------------------------|
| `ops-page` | `lazuar-ops` | `…/lazuar-hub-ops` | `lazuar-ops` |
| `portal-page` | `lazuar-portal` | `…/lazuar-hub-portal` | `lazuar-portal` |
| `superadmin-page` | `lazuar-admin` | `…/lazuar-hub-superadmin` | `lazuar-superadmin` → optional rename to `lazuar-admin` |
| *(add)* | `lazuar-spec` | `…/lazuar-hub-developers` | `lazuar-spec` or `lazuar-developers` |

**Note on superadmin container_name:** Local stack uses `lazuar-superadmin` while proposed app name is `lazuar-admin`. Aligning container_name is optional; only matters for `docker logs lazuar-superadmin` muscle memory.

---

## 6. Network aliases, depends_on, and inter-service DNS

### 6.1 Production (`hub` network)

| Client | Resolves | Purpose |
|--------|----------|---------|
| Caddy | `api`, `ops`, `portal`, `superadmin`, `developers` | reverse_proxy |
| portal container | `api:8080` | SSR `API_URL` |
| caddy | depends_on all app services | start order only (not health-gated depends) |
| portal | `depends_on: api: condition: service_healthy` | Wait for API health before portal start |

Frontend apps do **not** call each other over the Docker network (ops does not proxy to portal via compose DNS; browsers hit public URLs).

### 6.2 Local stacks (`lazuar-network`)

| Client | Resolves |
|--------|----------|
| portal-page | `api:8080` for `API_URL` |
| All frontends | `depends_on: api` (no health condition on frontends) |

Renaming local service `portal-page` → `lazuar-portal` does **not** break portal’s `API_URL` as long as service `api` stays named `api`.

### 6.3 Network aliases

No explicit `networks.*.aliases` are defined. Docker Compose default DNS name = **service key**. `container_name` is an additional hostname on the default bridge/network in practice for user-defined networks as well, but **Caddyfile correctly uses service keys**, which is the durable contract.

**Do not** rename production service keys without a coordinated Caddyfile change.

---

## 7. Volumes and state

| Volume | Project | Purpose | Impact of frontend rename |
|--------|---------|---------|---------------------------|
| `caddy_data` | `lazuar-hub` (prod) | TLS certificates / ACME | **None** if project name stays `lazuar-hub` |
| `caddy_config` | `lazuar-hub` (prod) | Caddy config state | None |
| `pgdata` | `lazuar-hub` (local/ghcr compose) | Local Postgres | None for frontend rename |

**Caution:** Changing Compose `name: lazuar-hub` to something else (e.g. `lazuar-pay`) would **orphan** volumes under the old project prefix (`lazuar-hub_caddy_data`) and force re-ACME or manual volume reattach. **Frontend app renames do not require project rename.** Keep `name: lazuar-hub` unless a separate hub→pay branding migration is planned.

No bind mounts of app source into production containers (images are immutable pulls). Only bind mount: `./Caddyfile` → `/etc/caddy/Caddyfile:ro`.

---

## 8. GHCR image rename strategy (if image repos change)

Today production pulls:

```
ghcr.io/proxeon/lazuar-hub-api
ghcr.io/proxeon/lazuar-hub-ops
ghcr.io/proxeon/lazuar-hub-portal
ghcr.io/proxeon/lazuar-hub-superadmin
ghcr.io/proxeon/lazuar-hub-developers
```

### 8.1 Recommendation: **do not rename GHCR repos** for this monorepo folder rename

Reasons:

1. Production compose pins image names independently of app folders.
2. GHCR package permissions, stars, and any external pull scripts use existing names.
3. GHA build cache scopes (`scope: lazuar-hub-ops`) stay warm.
4. Flat names already avoid nested-package 403 issues (commented in bake file).
5. Dual-tag period adds operational risk without user-facing benefit.

Workflow continues to build from new Dockerfiles into the **same** `matrix.name` image repositories.

### 8.2 If product insists on new image names (e.g. `lazuar-ops` instead of `lazuar-hub-ops`)

Use a **dual-tag / dual-push window**, then cut compose.

#### Phase D1 — Dual publish (no prod compose change yet)

For each frontend image, push **both** old and new repository names for the same digest:

```
ghcr.io/proxeon/lazuar-hub-ops:sha-abc1234
ghcr.io/proxeon/lazuar-ops:sha-abc1234        # new
ghcr.io/proxeon/lazuar-hub-ops:latest
ghcr.io/proxeon/lazuar-ops:latest
```

Implementation options:

- **A.** Expand workflow matrix or add a second `docker/metadata-action` images list with two prefixes.
- **B.** After push, `docker buildx imagetools create -t new:tag old:tag` (retag by digest, no rebuild).
- **C.** Bake `tags = [ old, new ]` arrays in `docker-bake.hcl`.

Keep this for at least **one successful production deploy cycle** (or N days of rollback window).

#### Phase D2 — Compose cutover

Update `deploy/prod/docker-compose.yml` `image:` lines to new names in the **same commit** that is deployed via the normal pipeline:

1. GHA builds and dual-tags (or new-only if dual already proven).
2. rsync new compose to VPS.
3. `remote-deploy.sh` → `docker compose pull` resolves **new** names.
4. `docker compose up -d` recreates containers with new image references.
5. Health-gate passes.

Downtime: same as any normal rolling recreate (see §9) — **not** inherently longer if tags exist before pull.

#### Phase D3 — Stop dual-tag; keep old packages immutable for rollback

- Leave old GHCR packages readable for 30–90 days.
- Document rollback: revert compose `image:` to old name + previous `VERSION` pin; do **not** delete old packages until rollback window ends.
- Optionally unpublish/deprecate old package names after window.

#### Failure modes if dual-tag skipped

| Mistake | Symptom |
|---------|---------|
| Compose points to new name before first successful push | `pull` fails; deploy aborts; old containers may remain if `up` never ran — actually script does pull then up; pull failure exits before up if `set -e` — **old stack stays up** (good) |
| New name pushed but typo in compose | Pull fails; old stack stays |
| Only `latest` retagged, `VERSION=sha-…` pin missing on new repo | Pull fails for sha tag; deploy fails |
| Delete old GHCR package immediately | Cannot rollback by VERSION pin |

---

## 9. Compose service rename downtime analysis

### 9.1 What Docker Compose does on service rename

If `ops` is renamed to `lazuar-ops` in the same project:

1. Compose treats `lazuar-ops` as a **new** service.
2. Old service `ops` becomes an **orphan** (container `hub-ops` still running unless removed).
3. `docker compose up -d --remove-orphans` (already used by `remote-deploy.sh`) **stops and removes** the orphan container.
4. New container starts with new service DNS name.
5. Caddy’s upstream `ops:3000` **breaks** until Caddyfile uses `lazuar-ops:3000` and Caddy reloads/recreates.

**Atomic requirement:** service rename + Caddyfile update + remote-deploy health names + container_name policy must land in **one rsync + one `compose up`**.

### 9.2 Expected downtime characteristics (single VPS, no blue/green)

Current deploy model is **in-place recreate**, not blue/green or Swarm rolling updates:

| Component | During `compose up -d` | User impact |
|-----------|------------------------|-------------|
| `api` | Container recreate; workers restart | Brief API 502/connection reset; background jobs pause until process up; `start_period: 120s` health |
| `ops` / portal / superadmin / developers | Recreate | Path-specific 502 until process listens on 3000 |
| `caddy` | Usually unchanged if only image tags change | If Caddyfile changes, reload at end of script; short config apply |

Typical frontend cold start: seconds (Vite static `serve`) to tens of seconds (Next standalone).

**There is no zero-downtime rolling update** for these services as currently defined (no `deploy.replicas`, no parallel old/new containers behind a shared VIP). Renaming services does not add *extra* downtime beyond a normal version deploy **if** Caddy and compose stay consistent; a **mismatched** rename causes **prolonged** 502 until fixed.

### 9.3 `container_name` rename specifically

`container_name` is a hard singleton. Renaming `hub-ops` → `lazuar-ops`:

1. Compose cannot rename in place; it must remove old container and create new one (name conflict otherwise).
2. `remote-deploy.sh` must wait on the **new** name.
3. Any external monitoring/scripts that `docker logs hub-ops` break.

**Recommendation:** Keep `hub-*` container names in production even if monorepo apps rename. Local stack can keep/adjust `lazuar-*` names independently.

### 9.4 Orphan and project name pitfalls

- Always keep `--remove-orphans` (already present) when renaming services so old containers do not keep listening on the old Docker DNS name while unused.
- Do not change Compose project `name` in the same migration as service renames if avoidable — multiplies orphan/volume confusion.
- If both old and new services briefly exist **without** `--remove-orphans`, you waste RAM (`mem_limit` on each) and risk confusion, but Caddy still only points at one name.

### 9.5 Order of operations for a **safe production deploy** that only renames monorepo apps (recommended path)

1. Merge PR: move `apps/*-page` → `apps/lazuar-*`, fix all Dockerfiles, bake, workflow dockerfile paths, local compose. **Do not change** `deploy/prod/docker-compose.yml` image names or service keys.
2. GHA builds new images into **existing** GHCR names (`lazuar-hub-ops`, …) with new `sha-*` tag.
3. Deploy job rsyncs deploy/prod (unchanged image lines) and runs remote-deploy with new `VERSION`.
4. Pull + recreate containers; health-gate; smoke `/`, `/portal`, `/docs`, `/health`.
5. Verify Scalar at `/docs`, checkout at `/portal`, admin at `/admin/`, ops at `/`.

**Expected downtime:** identical to any normal frontend+api release (seconds to ~2 minutes for API health start_period worst case if API image also rebuilt).

### 9.6 Order of operations if **also** renaming compose services + images

1. Dual-tag images (old + new) for at least one release.
2. Single PR that updates:
   - `deploy/prod/docker-compose.yml` (service keys, images, depends_on, container_names if any)
   - `deploy/prod/Caddyfile` (upstream hostnames)
   - `scripts/remote-deploy.sh` (wait_healthy names)
   - bake/workflow image names if permanently switching
3. Deploy once; confirm smoke; watch error budget.
4. After rollback window, stop dual-tagging.

**Do not** split Caddyfile service rename and compose service rename across two deploys.

---

## 10. Healthcheck detail and rename sensitivity

### 10.1 Image-level HEALTHCHECK paths

| App (new name) | HEALTHCHECK path | Depends on |
|----------------|------------------|------------|
| lazuar-ops | `/` | Vite base `/` |
| lazuar-portal | `/portal` | `NEXT_BASE_PATH=/portal` |
| lazuar-admin | `/` | Static server root; Caddy strips `/admin` |
| lazuar-spec | `/docs` | `NEXT_BASE_PATH=/docs` |
| api | compose: `/health` | ASP.NET endpoint |

Folder rename does not change HEALTHCHECK URLs unless base paths change.

### 10.2 Superadmin strip-prefix nuance

Caddy:

```
handle_path /admin/* {
  reverse_proxy superadmin:3000
}
```

`handle_path` strips `/admin` before proxying, so the container serves assets at `/` while the browser sees `/admin/…`. Image HEALTHCHECK correctly probes container-local `/`, not `/admin`. **Unaffected by package rename.**

### 10.3 Developers vs docs path naming

Public path remains `/docs` while app may become `lazuar-spec`. That mismatch is intentional product naming (spec app, docs URL). **Do not** change Caddy `/docs*` merely because the package is renamed to `lazuar-spec` unless product wants a public URL change (separate decision; higher customer impact).

---

## 11. Env files, secrets, and CI deploy coupling

### 11.1 What lives only on the server

| Item | Path |
|------|------|
| Live env | `/root/lazuar-hub-prod/.env` |
| Compose + Caddyfile | `/root/lazuar-hub-prod/` (synced each deploy) |
| Deploy script | `/root/lazuar-hub-remote-deploy.sh` |

`.env` is **not** overwritten unless `HUB_ENV_FILE` secret is set. Folder renames never require touching JWT/Neon/KMS keys.

### 11.2 `VERSION` pin interaction with image renames

`remote-deploy.sh` normalizes git SHAs to `sha-<7>` and writes `VERSION=` into `.env`. Compose interpolates:

```yaml
image: ghcr.io/proxeon/lazuar-hub-ops:${VERSION:-latest}
```

If images are dual-tagged, the same `VERSION` works for both old and new repository names during cutover. If only new repos receive `sha-*` tags, compose must already point at new names.

### 11.3 Build-arg vs runtime env

| Concern | Where set | Rename impact |
|---------|-----------|---------------|
| Vite API URL | Docker build-arg (baked into JS) | Rebuild required for URL changes; not for folder rename |
| Next public API URL | Build-arg + optional runtime for portal | Folder rename: rebuild only |
| Portal SSR API | Runtime `API_URL=http://api:8080/api/v1` | Depends on service name `api` |
| OpenAPI root | Runtime `OPENAPI_SPEC_ROOT` | Independent of app name |

---

## 12. Gaps and inconsistencies discovered (relevant to rename work)

1. **`docker-compose.ghcr.yml` and root `docker-compose.yml` omit developers / lazuar-spec**, while production and GHCR pipeline include it. Local “full” stack is not parity with prod.
2. **Comment drift:** `ghcr.yml` header says “Build 4 hub images” but matrix has **5** (api + 4 frontends).
3. **ADR 016** documents subdomain routing (`ops.lazuar.com`); **live Caddyfile** is path-based on `hub.lazuar.com`. Rename docs should reference live Caddyfile as source of truth for runtime.
4. **Local container_names** already `lazuar-ops` / `lazuar-portal` / `lazuar-superadmin` while **prod** uses `hub-*`. Two conventions coexist; do not “fix” by renaming prod containers without script updates.
5. **Image prefix `lazuar-hub-*` vs monorepo folder `lazuar-pay` workspace path** — branding “hub” remains in deploy even if apps become `lazuar-ops`. Treat hub→pay product rename as a **separate** migration (project name, server path, GHCR prefix, concurrency group, domain).
6. **No Traefik/nginx** configs to update beyond Caddy.
7. **Smoke tests omit `/admin`** — optional improvement, not rename-blocking.
8. **Task `docker:build` echo** says “api, portal, ops, superadmin” and omits developers even though bake default group includes developers-page.

---

## 13. Explicit non-goals / non-impacts

- **No change** to Billplz/Stripe webhook public URLs (`App__ApiBaseUrl` + `/webhooks/...`) from frontend renames.
- **No change** to Cors origin host if still `https://hub.lazuar.com`.
- **No volume migration** for frontends (stateless).
- **No database migration** for renames.
- **No multi-replica worker rebalancing** (API remains single replica rule per README).
- **pnpm workspace** uses `apps/*` glob — directory rename is enough; no workspace glob edit required.
- **API image and service** (`lazuar-api` / `lazuar-hub-api`) are outside the four-app rename list.

---

## 14. Migration strategy — production playbooks

### Playbook A — Recommended: monorepo rename only (lowest risk)

**Goal:** Ship `apps/lazuar-{ops,portal,admin,spec}` without production config churn.

| Step | Action | Downtime |
|------|--------|----------|
| A1 | PR: move directories; fix Dockerfiles (paths + Next CMD); update bake targets & dockerfile paths; update local compose/ghcr compose service keys; update GHA matrix dockerfile paths; keep GHCR image names and prod compose/Caddy untouched | Dev only |
| A2 | CI green; GHA push images to **existing** `lazuar-hub-*` repos | None for users |
| A3 | Auto-deploy or workflow_dispatch deploy with new `sha-*` | Brief recreate (normal) |
| A4 | Smoke `/health`, `/`, `/portal`, `/docs`, `/admin/` | — |
| A5 | Keep previous `sha-*` images on GHCR for rollback: re-run deploy with `version=sha-old` | Rollback = same as today |

**Rollback:** Workflow dispatch `skip_build=true` + `version=sha-<previous>`; no compose file revert needed if image names unchanged.

### Playbook B — Align local service names only; prod untouched

Same as A, with explicit checklist that `deploy/prod/*` service keys stay `ops|portal|superadmin|developers`.

### Playbook C — Full branding alignment (images + services + containers)

| Step | Action |
|------|--------|
| C1 | Dual-tag all four frontend images (and api if renaming) for ≥1 release |
| C2 | Verify VPS can `docker pull` both old and new names with a test tag |
| C3 | Single atomic PR: compose services, images, Caddyfile, remote-deploy wait list, docs |
| C4 | Deploy during low traffic; watch health-gate |
| C5 | Confirm Docker network DNS: `docker compose exec caddy ping lazuar-ops` (or new names) |
| C6 | Hold dual-tag 30 days; then stop publishing old names |

**Compose service rename downtime:** one recreate cycle; if Caddyfile mismatches, **extended** 502 until hotfix. Prefer maintenance window if team is small.

**Dual-tag images?** **Yes, required for Playbook C.** **No, unnecessary for Playbook A.**

### Playbook D — Emergency rollback after bad rename deploy

1. SSH to VPS.
2. If only image contents bad: set `VERSION` to last known good `sha-*` in `.env`; run `/root/lazuar-hub-remote-deploy.sh`.
3. If compose/Caddy broken mid-migration: restore previous `docker-compose.yml` + `Caddyfile` from git (`rsync` old commit or `git show`), then compose up; ensure `--remove-orphans` cleans half-migrated services.
4. Do not delete GHCR packages during incident.

---

## 15. Rolling update considerations (summary table)

| Change type | Needs dual-tag GHCR? | Needs Caddy change? | Needs remote-deploy change? | Expected extra downtime vs normal deploy |
|-------------|----------------------|---------------------|-----------------------------|------------------------------------------|
| App directory rename only | No | No | No | None |
| Bake target rename only | No | No | No | None |
| Local compose service rename | No | No (local) | No | N/A |
| Prod image repository rename | **Yes** (recommended) | No | No (if only image lines change) | None if dual-tag pre-seeded; pull-fail keeps old stack |
| Prod compose service rename | No | **Yes (same deploy)** | If container_name changes | Normal recreate; **severe** if Caddy stale |
| container_name rename | No | No | **Yes** | Normal recreate |
| Public path change (`/docs`→`/spec`) | Rebuild with new basePath | **Yes** | Smoke URLs | SEO + bookmarks break; not part of rename proposal |
| Compose project `name` change | No | No | Possibly DIR docs | **Volume orphan risk for Caddy certs** |

---

## 16. Suggested target end-state (deploy layer)

### 16.1 Minimal end-state (Playbook A)

| Concern | Value |
|---------|-------|
| App dirs | `apps/lazuar-ops`, `lazuar-portal`, `lazuar-admin`, `lazuar-spec` |
| Prod services | still `ops`, `portal`, `superadmin`, `developers` |
| Prod containers | still `hub-*` |
| GHCR | still `lazuar-hub-ops`, `…-portal`, `…-superadmin`, `…-developers` |
| Caddy host | `hub.lazuar.com` paths unchanged |
| Local services | `lazuar-ops`, `lazuar-portal`, `lazuar-admin`, optionally `lazuar-spec` |
| Local containers | `lazuar-ops`, `lazuar-portal`, `lazuar-admin` / `lazuar-superadmin`, optional `lazuar-spec` |

### 16.2 Aspirational end-state (Playbook C — only if branding mandates)

| Concern | Value |
|---------|-------|
| Prod services | `lazuar-ops`, `lazuar-portal`, `lazuar-admin`, `lazuar-spec` |
| Caddy upstreams | matching service names |
| GHCR | `lazuar-ops`, `lazuar-portal`, `lazuar-admin`, `lazuar-spec` (or keep `lazuar-hub-*` to reduce registry churn) |
| Containers | match services or keep `hub-*` deliberately documented |

---

## 17. Checklist for implementers (deploy-focused)

### Build path checklist

- [ ] `apps/lazuar-ops/Dockerfile` paths and filters
- [ ] `apps/lazuar-portal/Dockerfile` paths, static COPY, **CMD server.js path**
- [ ] `apps/lazuar-admin/Dockerfile` paths and filters
- [ ] `apps/lazuar-spec/Dockerfile` paths, static COPY, **CMD server.js path**, OPENAPI copy
- [ ] `docker-bake.hcl` dockerfiles + target names + default group
- [ ] `.github/workflows/ghcr.yml` matrix dockerfiles (image names decision documented)
- [ ] `docker-compose.yml` dockerfile + service keys
- [ ] `docker-compose.ghcr.yml` service keys; consider adding developers/spec
- [ ] Taskfile bake target references if any hardcode old target names

### Production safety checklist (before merge to main)

- [ ] Confirm **no unintended** edits to `deploy/prod/Caddyfile` upstream hostnames
- [ ] Confirm **no unintended** GHCR image renames without dual-tag plan
- [ ] Confirm `remote-deploy.sh` wait list still matches `container_name`s
- [ ] Confirm public paths `/portal`, `/docs`, `/admin`, `/` still match app basePath build-args
- [ ] Confirm previous `sha-*` tags remain on GHCR for rollback
- [ ] Plan smoke: health, ops root, portal, docs, admin

### Post-deploy verification

- [ ] `docker ps` shows expected image digests for `VERSION`
- [ ] `curl -H 'Host: hub.lazuar.com' http://127.0.0.1/health`
- [ ] Browser: login on ops, open portal checkout shell, Scalar docs, superadmin
- [ ] Caddy logs clean of `dial tcp: lookup ops: no such host` (or new names)
- [ ] No orphan containers: `docker ps -a | grep -E 'ops-page|portal-page|hub-'`

---

## 18. Decision record (for plan 002)

| Decision | Recommendation | Rationale |
|----------|----------------|-----------|
| Rename prod compose services to match new app names? | **No** (default) | Avoids Caddy + DNS churn; services already short and stable |
| Rename GHCR image repos? | **No** (default) | Avoid dual-tag complexity; images already `lazuar-hub-*` role names |
| Rename prod `container_name`s? | **No** (default) | remote-deploy hardcodes `hub-*` |
| Rename local compose services? | **Yes** | Align with folders; developer UX |
| Dual-tag images? | **Only if** renaming GHCR repos | Playbook A needs no dual-tag |
| Change public URL paths? | **No** | Out of scope; high external impact |
| Change Compose project name / `/root/lazuar-hub-prod`? | **No** in this rename | Separate hub→pay infra branding migration |
| Add lazuar-spec to local/ghcr compose? | Optional improvement | Closes prod parity gap; not strictly required for rename |

---

## 19. Appendix — current source excerpts (reference)

### A. Production compose image + service keys

File: `deploy/prod/docker-compose.yml`

- Services: `caddy`, `api`, `ops`, `portal`, `superadmin`, `developers`
- Images: `ghcr.io/proxeon/lazuar-hub-{api,ops,portal,superadmin,developers}:${VERSION:-latest}`
- Network name: `hub`
- Volumes: `caddy_data`, `caddy_config`

### B. Caddy path routing

File: `deploy/prod/Caddyfile`

- Host: `hub.lazuar.com`
- `/health` → `api:8080`
- `/api/*` → `api:8080`
- `/portal*` → `portal:3000`
- `/docs*` → `developers:3000`
- `/admin` redirect + `/admin/*` strip → `superadmin:3000`
- default → `ops:3000`

### C. GHCR image list (bake comments)

```
ghcr.io/proxeon/lazuar-hub-api
ghcr.io/proxeon/lazuar-hub-ops
ghcr.io/proxeon/lazuar-hub-portal
ghcr.io/proxeon/lazuar-hub-superadmin
ghcr.io/proxeon/lazuar-hub-developers
```

### D. remote-deploy health containers

```
hub-api, hub-ops, hub-portal, hub-superadmin, hub-developers, hub-caddy
```

### E. Mapping summary (all layers)

| Proposed app | Local service (suggested) | Prod service (keep) | Prod container (keep) | GHCR image (keep) | Public path |
|--------------|---------------------------|---------------------|----------------------|-------------------|-------------|
| `lazuar-ops` | `lazuar-ops` | `ops` | `hub-ops` | `lazuar-hub-ops` | `/` |
| `lazuar-portal` | `lazuar-portal` | `portal` | `hub-portal` | `lazuar-hub-portal` | `/portal` |
| `lazuar-admin` | `lazuar-admin` | `superadmin` | `hub-superadmin` | `lazuar-hub-superadmin` | `/admin` |
| `lazuar-spec` | `lazuar-spec` | `developers` | `hub-developers` | `lazuar-hub-developers` | `/docs` |

---

## 20. Conclusion

The monorepo rename of `developers-page`, `ops-page`, `portal-page`, and `superadmin-page` into `lazuar-spec`, `lazuar-ops`, `lazuar-portal`, and `lazuar-admin` is **primarily a build-context and local-compose problem**, not a DNS or certificate problem.

Production already decouples:

- **public paths** (stable product URLs),
- **compose service DNS** (short names used by Caddy),
- **GHCR image repositories** (`lazuar-hub-*`),
- **monorepo directory names** (`*-page`).

Therefore the **safest production migration is Playbook A**: rename directories and fix every Dockerfile/bake/workflow **path**, keep pulling the same GHCR image names into the same prod services, accept normal in-place recreate downtime only, and **do not** dual-tag unless intentionally renaming registries.

If stakeholders later require image or service names to match `lazuar-ops` / `lazuar-spec` exactly, execute **Playbook C** with dual-tagged images and an atomic Caddy + compose + remote-deploy cutover—never service rename without simultaneous reverse-proxy update.
