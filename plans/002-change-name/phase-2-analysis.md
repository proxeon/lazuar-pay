# Phase 2 — Analysis & implement brief (Docker / bake / compose / CI paths)

**Status:** Analysis only — **do not implement in this file’s authoring step**; implementers follow §3–§9.  
**Date:** 2026-08-09  
**Branch:** `chore/rename-frontend-apps-lazuar-prefix`  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Related:** [`phase-1-done.md`](./phase-1-done.md), [`01-docker-ghcr-compose.md`](./01-docker-ghcr-compose.md), [`11-implementation-checklist.md`](./11-implementation-checklist.md) § Phase 2

---

## 1. Phase 2 goal

After Phase 1 folder renames, **build context paths still point at deleted `apps/*-page` directories**. Phase 2 updates Docker/bake/compose/CI so images can build again.

| # | In scope | Out of scope (later / never this phase) |
|---|----------|----------------------------------------|
| 1 | Four frontend Dockerfiles: internal monorepo paths | GHCR image repository renames |
| 2 | `docker-bake.hcl` dockerfile paths + target names | `deploy/prod/**`, Caddyfile, `remote-deploy.sh` |
| 3 | Root `docker-compose.yml` dockerfile + service keys | `pnpm-lock.yaml`, mprocs, Taskfile package filters |
| 4 | Root `docker-compose.ghcr.yml` service key consistency | OCI label hub→pay rebrand |
| 5 | `.github/workflows/ghcr.yml` matrix `dockerfile:` paths | Public URL base paths (`/portal`, `/docs`, `/admin`) |

**Locked identity (from Phase 1 done):**

| Folder (exists now) | package.json `"name"` | Old folder (gone) |
|---------------------|----------------------|-------------------|
| `apps/lazuar-developers` | `lazuar-developers` | `apps/developers-page` |
| `apps/lazuar-ops` | `lazuar-ops` | `apps/ops-page` |
| `apps/lazuar-portal` | `lazuar-portal` | `apps/portal-page` |
| `apps/lazuar-admin` | `lazuar-admin` | `apps/superadmin-page` |

Do **not** use draft name `lazuar-spec` for developers.

---

## 2. Current state (post Phase 1, pre Phase 2)

### 2.1 App directories

```text
apps/lazuar-admin/Dockerfile        exists — still hardcodes apps/superadmin-page
apps/lazuar-developers/Dockerfile   exists — still hardcodes apps/developers-page
apps/lazuar-ops/Dockerfile          exists — still hardcodes apps/ops-page
apps/lazuar-portal/Dockerfile       exists — still hardcodes apps/portal-page
apps/lazuar-api/Dockerfile          already apps/lazuar-api — no Phase 2 change
```

Old dirs `apps/{developers,ops,portal,superadmin}-page` are **absent**. Any build that references them fails immediately.

### 2.2 Severity matrix

| File | Forced by folder rename? | Failure mode if unfixed |
|------|--------------------------|-------------------------|
| `apps/lazuar-*/Dockerfile` (×4) | **YES** | `COPY` / `pnpm --filter` / Next standalone `CMD` miss source |
| `docker-bake.hcl` | **YES** | `dockerfile: apps/*-page/Dockerfile` not found |
| `docker-compose.yml` | **YES** | same for `build.dockerfile` |
| `docker-compose.ghcr.yml` | **No** for images (pull-only); **yes for consistency** of service keys | Pull still works if GHCR images exist; service keys remain `*-page` |
| `.github/workflows/ghcr.yml` | **YES** | matrix `file:` paths 404 → CI red |
| `deploy/prod/docker-compose.yml` | **No** | image-only; no app folder paths |
| `.dockerignore` | **No** | no `*-page` hardcoding |

---

## 3. Exact replacements by file

### 3.1 `apps/lazuar-ops/Dockerfile`

**Strategy:** global path token replace inside this file only.

| Old string | New string | Occurrences (approx) |
|------------|------------|----------------------|
| `apps/ops-page` | `apps/lazuar-ops` | 5 lines |

**Line-level map (current → intended):**

| Line | Current | After |
|------|---------|-------|
| 13 | `COPY apps/ops-page/package.json apps/ops-page/` | `COPY apps/lazuar-ops/package.json apps/lazuar-ops/` |
| 15 | `pnpm install --filter ./apps/ops-page... --frozen-lockfile` | `./apps/lazuar-ops...` |
| 19 | `COPY apps/ops-page apps/ops-page` | `COPY apps/lazuar-ops apps/lazuar-ops` |
| 28 | `pnpm --filter ./apps/ops-page build` | `./apps/lazuar-ops` |
| 36 | `COPY --from=build ... /app/apps/ops-page/dist ./dist` | `/app/apps/lazuar-ops/dist` |

**KEEP unchanged:** `VITE_API_URL`, `VITE_PORTAL_URL`, `VITE_BASE_PATH=/`, serve CMD, healthcheck, Node base image.

---

### 3.2 `apps/lazuar-portal/Dockerfile`

| Old string | New string |
|------------|------------|
| `apps/portal-page` | `apps/lazuar-portal` |

**Line-level map:**

| Line | Current | After |
|------|---------|-------|
| 13 | `COPY apps/portal-page/package.json apps/portal-page/` | `apps/lazuar-portal/...` |
| 15 | `pnpm install --filter ./apps/portal-page...` | `./apps/lazuar-portal...` |
| 19 | `COPY apps/portal-page apps/portal-page` | `apps/lazuar-portal` |
| 27 | `pnpm --filter ./apps/portal-page build` | `./apps/lazuar-portal` |
| 43 | `.../apps/portal-page/.next/standalone ./` | `apps/lazuar-portal` |
| 44 | `.../apps/portal-page/.next/static ./apps/portal-page/.next/static` | **both sides** → `lazuar-portal` |
| 45 | `.../apps/portal-page/public ./apps/portal-page/public` | **both sides** → `lazuar-portal` |
| 52 | `CMD ["node", "apps/portal-page/server.js"]` | `apps/lazuar-portal/server.js` |

**Critical:** Next.js `output: "standalone"` embeds the monorepo folder path in the standalone layout. Destination paths under runtime (`./apps/lazuar-portal/.next/static`, `CMD`) **must** match the folder used at build time.

**KEEP:** `NEXT_PUBLIC_API_URL`, `NEXT_BASE_PATH=/portal`, healthcheck `http://127.0.0.1:3000/portal`.

---

### 3.3 `apps/lazuar-admin/Dockerfile`

| Old string | New string |
|------------|------------|
| `apps/superadmin-page` | `apps/lazuar-admin` |

**Line-level map:**

| Line | Current | After |
|------|---------|-------|
| 13 | `COPY apps/superadmin-page/package.json apps/superadmin-page/` | `apps/lazuar-admin` |
| 15 | `pnpm install --filter ./apps/superadmin-page...` | `./apps/lazuar-admin...` |
| 19 | `COPY apps/superadmin-page apps/superadmin-page` | `apps/lazuar-admin` |
| 26 | `pnpm --filter ./apps/superadmin-page build` | `./apps/lazuar-admin` |
| 34 | `.../apps/superadmin-page/dist ./dist` | `apps/lazuar-admin/dist` |

**KEEP:** `VITE_API_URL`, `VITE_BASE_PATH=/admin/`, serve CMD.

---

### 3.4 `apps/lazuar-developers/Dockerfile`

| Old string | New string |
|------------|------------|
| `apps/developers-page` | `apps/lazuar-developers` |

**Line-level map:**

| Line | Current | After |
|------|---------|-------|
| 11 | `COPY apps/developers-page/package.json apps/developers-page/` | `apps/lazuar-developers` |
| 13 | `pnpm install --filter ./apps/developers-page... --filter @repo/api-spec...` | path → `./apps/lazuar-developers...`; **keep** `@repo/api-spec` |
| 17 | `COPY apps/developers-page apps/developers-page` | `apps/lazuar-developers` |
| 26 | `pnpm --filter ./apps/developers-page build` | `./apps/lazuar-developers` |
| 39 | `.../apps/developers-page/.next/standalone ./` | `lazuar-developers` |
| 40 | static dest `./apps/developers-page/.next/static` | `./apps/lazuar-developers/.next/static` |
| 41 | public dest `./apps/developers-page/public` | `./apps/lazuar-developers/public` |
| 50 | `CMD ["node", "apps/developers-page/server.js"]` | `apps/lazuar-developers/server.js` |

**KEEP:** `@repo/api-spec` filter, `NEXT_BASE_PATH=/docs`, `OPENAPI_SPEC_ROOT`, healthcheck `/docs`, api-spec dist copy.

---

### 3.5 `docker-bake.hcl`

#### 3.5.1 Dockerfile paths (MUST)

| Old | New |
|-----|-----|
| `apps/portal-page/Dockerfile` | `apps/lazuar-portal/Dockerfile` |
| `apps/ops-page/Dockerfile` | `apps/lazuar-ops/Dockerfile` |
| `apps/superadmin-page/Dockerfile` | `apps/lazuar-admin/Dockerfile` |
| `apps/developers-page/Dockerfile` | `apps/lazuar-developers/Dockerfile` |

#### 3.5.2 Bake target renames (RECOMMENDED — match folder basenames)

| Old target id | New target id |
|---------------|---------------|
| `portal-page` | `lazuar-portal` |
| `ops-page` | `lazuar-ops` |
| `superadmin-page` | `lazuar-admin` |
| `developers-page` | `lazuar-developers` |
| `api` | `api` (**keep**) |

**`group "default"` (line 48–50):**

```hcl
# before
targets = ["api", "portal-page", "ops-page", "superadmin-page", "developers-page"]

# after
targets = ["api", "lazuar-portal", "lazuar-ops", "lazuar-admin", "lazuar-developers"]
```

**Target blocks:** rename `target "portal-page"` → `target "lazuar-portal"` (and siblings). Body fields:

| Field | Action |
|-------|--------|
| `dockerfile` | update path (§3.5.1) |
| `tags` (`lazuar-hub-*`) | **KEEP** |
| `labels` title (`lazuar-hub-*`) | **KEEP** |
| `args` values (`/portal`, `/docs`, `/admin/`, vite URLs) | **KEEP** |
| `target "api"` | **KEEP** as-is |

#### 3.5.3 Comments (optional polish)

Header comments listing GHCR names stay accurate (`lazuar-hub-*`). No change required for correctness. Public path comments (`/portal`, `/docs`, …) **KEEP**.

#### 3.5.4 Example: `lazuar-portal` target after edit

```hcl
target "lazuar-portal" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/lazuar-portal/Dockerfile"
  tags = [
    "${REGISTRY}/lazuar-hub-portal:${TAG}",
    "${REGISTRY}/lazuar-hub-portal:latest",
  ]
  args = {
    NEXT_PUBLIC_API_URL = NEXT_PUBLIC_API_URL
    NEXT_BASE_PATH      = NEXT_BASE_PATH
  }
  labels = {
    "org.opencontainers.image.title" = "lazuar-hub-portal"
  }
}
```

Same pattern for ops / admin / developers with paths `apps/lazuar-ops|admin|developers` and tags still `lazuar-hub-ops|superadmin|developers`.

**Note:** Image title `lazuar-hub-superadmin` vs folder `lazuar-admin` is an intentional KEEP mismatch (see §6).

---

### 3.6 `docker-compose.yml`

#### 3.6.1 Dockerfile paths (MUST)

| Service (current key) | Old dockerfile | New dockerfile |
|-----------------------|----------------|----------------|
| `ops-page` | `apps/ops-page/Dockerfile` | `apps/lazuar-ops/Dockerfile` |
| `portal-page` | `apps/portal-page/Dockerfile` | `apps/lazuar-portal/Dockerfile` |
| `superadmin-page` | `apps/superadmin-page/Dockerfile` | `apps/lazuar-admin/Dockerfile` |
| `api` | `apps/lazuar-api/Dockerfile` | **unchanged** |

#### 3.6.2 Service key renames (RECOMMENDED)

| Old key | New key |
|---------|---------|
| `ops-page` | `lazuar-ops` |
| `portal-page` | `lazuar-portal` |
| `superadmin-page` | `lazuar-admin` |

YAML structure after rename (ops example):

```yaml
  lazuar-ops:
    build:
      context: .
      dockerfile: apps/lazuar-ops/Dockerfile
      args:
        VITE_API_URL: ${VITE_API_URL:-http://localhost:8080/api/v1}
        VITE_PORTAL_URL: ${VITE_PORTAL_URL:-http://localhost:3004}
    image: ghcr.io/proxeon/lazuar-hub-ops:local
    container_name: lazuar-ops
    # ports / depends_on / profiles unchanged
```

| Field | Action |
|-------|--------|
| `image: ghcr.io/proxeon/lazuar-hub-*:local` | **KEEP** |
| `container_name: lazuar-ops` / `lazuar-portal` / `lazuar-superadmin` | **KEEP** (optional to rename `lazuar-superadmin` → `lazuar-admin` later; **not** required for Phase 2 builds) |
| ports `3003`/`3004`/`3005` | **KEEP** |
| `profiles: ["full"]` | **KEEP** |
| env `API_URL: http://api:8080/api/v1` | **KEEP** |
| `db` / `api` / networks / volumes | **KEEP** |

#### 3.6.3 Optional: add `lazuar-developers` service (local parity)

**Status:** missing today (pre-existing gap). Prod already has `developers` via GHCR image.

**Recommendation:** add under `profiles: ["full"]` for local parity with bake/prod:

```yaml
  lazuar-developers:
    build:
      context: .
      dockerfile: apps/lazuar-developers/Dockerfile
      args:
        NEXT_BASE_PATH: /docs
    image: ghcr.io/proxeon/lazuar-hub-developers:local
    container_name: lazuar-developers
    ports:
      - "3002:3000"   # align with common local docs port if documented; adjust if conflict
    environment:
      OPENAPI_SPEC_ROOT: /app/openapi-specs
    depends_on:
      - api
    networks:
      - lazuar-network
    profiles: ["full"]
```

**Port note:** Confirm against `mprocs-dev.yaml` / docs (historically developers often on **3002**). If 3002 is free in compose, use it; else pick an unused host port and document. **Do not** invent a conflict with 3003–3005 or 8080/5432.

If implementer prefers minimal Phase 2, skip this block and only fix the three existing services + Dockerfiles + bake + ghcr.

---

### 3.7 `docker-compose.ghcr.yml`

**No build sections** — folder rename does not break pulls.

#### Service key renames (RECOMMENDED consistency with local compose)

| Old key | New key |
|---------|---------|
| `ops-page` | `lazuar-ops` |
| `portal-page` | `lazuar-portal` |
| `superadmin-page` | `lazuar-admin` |

| Field | Action |
|-------|--------|
| `image: ghcr.io/proxeon/lazuar-hub-ops|portal|superadmin:${TAG:-latest}` | **KEEP** |
| `name: lazuar-hub` | **KEEP** |
| `container_name: lazuar-*` | **KEEP** |
| ports, env, network | **KEEP** |

Optional parity: add `lazuar-developers` pull-only service mirroring prod (`image: .../lazuar-hub-developers:${TAG:-latest}`, port e.g. `3002:3000`). Same optional rule as §3.6.3.

---

### 3.8 `.github/workflows/ghcr.yml`

#### Matrix `dockerfile` paths (MUST)

| Matrix `name` (KEEP) | Old `dockerfile` | New `dockerfile` |
|----------------------|------------------|------------------|
| `lazuar-hub-api` | `apps/lazuar-api/Dockerfile` | **unchanged** |
| `lazuar-hub-portal` | `apps/portal-page/Dockerfile` | `apps/lazuar-portal/Dockerfile` |
| `lazuar-hub-ops` | `apps/ops-page/Dockerfile` | `apps/lazuar-ops/Dockerfile` |
| `lazuar-hub-superadmin` | `apps/superadmin-page/Dockerfile` | `apps/lazuar-admin/Dockerfile` |
| `lazuar-hub-developers` | `apps/developers-page/Dockerfile` | `apps/lazuar-developers/Dockerfile` |

#### KEEP entirely

| Item | Value / reason |
|------|----------------|
| `matrix.name` image ids | `lazuar-hub-api|portal|ops|superadmin|developers` — GHCR package names |
| `build_args` values | `NEXT_BASE_PATH=/portal`, `/docs`, `VITE_BASE_PATH=/admin/`, hub.lazuar.com URLs |
| path triggers `apps/**` | still correct after rename |
| concurrency `lazuar-hub-cd-...` | hub rebrand out of scope |
| deploy rsync to `/root/lazuar-hub-prod/` | out of scope |
| remote-deploy script path | out of scope |
| metadata tags (`latest`, `sha-*`) | out of scope |

Header comment still says “4 hub images” while matrix has 5 (api + 4 frontends) — optional comment fix only.

---

## 4. Bake target rename summary

| Layer | Old | New | Required? |
|-------|-----|-----|-----------|
| Bake target id | `portal-page` | `lazuar-portal` | Recommended |
| Bake target id | `ops-page` | `lazuar-ops` | Recommended |
| Bake target id | `superadmin-page` | `lazuar-admin` | Recommended |
| Bake target id | `developers-page` | `lazuar-developers` | Recommended |
| Bake `dockerfile` | `apps/*-page/...` | `apps/lazuar-*/...` | **Must** |
| Bake `tags` / label title | `lazuar-hub-*` | same | **Keep** |
| Compose service key | `*-page` | `lazuar-*` | Recommended |
| Compose `image` | `lazuar-hub-*` | same | **Keep** |
| GHCR matrix `name` | `lazuar-hub-*` | same | **Keep** |
| GHCR matrix `dockerfile` | `apps/*-page` | `apps/lazuar-*` | **Must** |

If bake targets are renamed, any ad-hoc `docker buildx bake ops-page` docs or muscle memory break — update living docs in the docs phase, not necessarily Phase 2. Taskfile uses generic bake groups (no hard-coded `ops-page` target names today).

---

## 5. Local compose service key renames

| File | Old service key | New service key | Notes |
|------|-----------------|-----------------|-------|
| `docker-compose.yml` | `ops-page` | `lazuar-ops` | profile `full` |
| `docker-compose.yml` | `portal-page` | `lazuar-portal` | profile `full` |
| `docker-compose.yml` | `superadmin-page` | `lazuar-admin` | profile `full` |
| `docker-compose.yml` | — | `lazuar-developers` | **optional add** |
| `docker-compose.ghcr.yml` | `ops-page` | `lazuar-ops` | no profile |
| `docker-compose.ghcr.yml` | `portal-page` | `lazuar-portal` | |
| `docker-compose.ghcr.yml` | `superadmin-page` | `lazuar-admin` | |
| `docker-compose.ghcr.yml` | — | `lazuar-developers` | **optional add** |

**Prod (`deploy/prod/docker-compose.yml`) service keys stay short:** `ops`, `portal`, `superadmin`, `developers`. **Do not** rename them in Phase 2 (Caddy + remote-deploy coupled).

---

## 6. Explicit KEEP list

Do **not** change these as part of Phase 2 path fixes:

### 6.1 GHCR / image repository names

```text
ghcr.io/proxeon/lazuar-hub-api
ghcr.io/proxeon/lazuar-hub-ops
ghcr.io/proxeon/lazuar-hub-portal
ghcr.io/proxeon/lazuar-hub-superadmin
ghcr.io/proxeon/lazuar-hub-developers
```

Including compose tags `:local` / `${TAG:-latest}` / bake `${TAG}` + `latest`, and workflow `matrix.name`.

### 6.2 Container names (optional keep)

| Environment | Names |
|-------------|-------|
| Local root compose | `lazuar-db`, `lazuar-api`, `lazuar-ops`, `lazuar-portal`, `lazuar-superadmin` |
| Prod | `hub-caddy`, `hub-api`, `hub-ops`, `hub-portal`, `hub-superadmin`, `hub-developers` |

Renaming local `lazuar-superadmin` → `lazuar-admin` is cosmetic and **not** required for builds. Prod `hub-*` names are required by `scripts/remote-deploy.sh` health gates — **do not touch**.

### 6.3 Public URL base paths & build args

| Path / arg | Value |
|------------|-------|
| Ops base | `/` |
| Portal | `/portal` (`NEXT_BASE_PATH`) |
| Developers | `/docs` |
| Admin | `/admin/` (`VITE_BASE_PATH`) |
| Default API URLs in bake | `https://hub.lazuar.com/api/v1` etc. |

### 6.4 Deploy / prod / remote

| Path | Reason |
|------|--------|
| `deploy/prod/docker-compose.yml` | image-only; service keys short |
| `deploy/prod/Caddyfile` | reverse_proxy hostnames = compose service keys |
| `deploy/prod/env.example`, README | not folder-path-bound |
| `scripts/remote-deploy.sh` | `hub-*` container health |
| Workflow deploy rsync destinations | `/root/lazuar-hub-prod/` |

### 6.5 Other infrastructure

| Item | KEEP |
|------|------|
| Compose project `name: lazuar-hub` | yes |
| Network `lazuar-network` | yes |
| Concurrency group `lazuar-hub-cd-*` | yes |
| OCI source label `github.com/proxeon/lazuar-hub` | yes (rebrand later) |
| `apps/lazuar-api/Dockerfile` | already correct |
| `.dockerignore` | no `*-page` literals |

### 6.6 Identity mismatches that stay intentional

| Folder | GHCR image suffix | Prod service | Notes |
|--------|-------------------|--------------|-------|
| `lazuar-admin` | `superadmin` | `superadmin` | admin vs superadmin |
| `lazuar-developers` | `developers` | `developers` | aligned enough |
| `lazuar-ops` | `ops` | `ops` | ok |
| `lazuar-portal` | `portal` | `portal` | ok |

---

## 7. Implementation order (for implementer)

1. Patch four Dockerfiles (`lazuar-ops`, `lazuar-portal`, `lazuar-admin`, `lazuar-developers`) — pure path replace.
2. Patch `docker-bake.hcl` — dockerfile paths + target ids + default group.
3. Patch `docker-compose.yml` — dockerfile + service keys; optional developers service.
4. Patch `docker-compose.ghcr.yml` — service keys; optional developers service.
5. Patch `.github/workflows/ghcr.yml` — matrix dockerfile paths only.
6. Run verification greps (§8). Smoke-build at least one frontend if time allows (§9).

**Do not** run full monorepo `pnpm install` solely for Phase 2 (lockfile phase later). Docker builds use their own `pnpm install` inside the image with the workspace files as copied.

---

## 8. Verification grep commands

From repo root:

```bash
cd /Users/akmalfirdaus/Code/lazuar/lazuar-pay

# --- MUST be empty after Phase 2 (stale page paths in Docker/CI surface) ---
rg -n 'apps/(ops|portal|developers|superadmin)-page' \
  apps/lazuar-ops/Dockerfile \
  apps/lazuar-portal/Dockerfile \
  apps/lazuar-admin/Dockerfile \
  apps/lazuar-developers/Dockerfile \
  docker-bake.hcl \
  docker-compose.yml \
  docker-compose.ghcr.yml \
  .github/workflows/ghcr.yml
# expect: no matches

# --- Bake / compose / workflow must reference new Dockerfiles ---
rg -n 'apps/lazuar-(ops|portal|admin|developers)/Dockerfile' \
  docker-bake.hcl docker-compose.yml .github/workflows/ghcr.yml
# expect: hits for all four apps in bake + ghcr; three (or four) in local compose

# --- GHCR image names still hub-* (must still exist) ---
rg -n 'lazuar-hub-(api|ops|portal|superadmin|developers)' \
  docker-bake.hcl docker-compose.yml docker-compose.ghcr.yml \
  .github/workflows/ghcr.yml deploy/prod/docker-compose.yml
# expect: present (KEEP)

# --- Old bake/compose service target names gone (if renamed) ---
rg -n '(portal-page|ops-page|superadmin-page|developers-page)' \
  docker-bake.hcl docker-compose.yml docker-compose.ghcr.yml
# expect: no matches (comments-only residual = fail polish)

# --- Deploy/prod untouched by path renames ---
rg -n 'apps/.*-page|apps/lazuar-(ops|portal|admin|developers)' \
  deploy/prod/
# expect: no app folder paths (images only)

# --- Superadmin image name still superadmin (not admin) ---
rg -n 'lazuar-hub-superadmin' docker-bake.hcl docker-compose.yml \
  docker-compose.ghcr.yml .github/workflows/ghcr.yml deploy/prod/docker-compose.yml
# expect: still present
```

Optional existence checks:

```bash
test -f apps/lazuar-ops/Dockerfile \
  && test -f apps/lazuar-portal/Dockerfile \
  && test -f apps/lazuar-admin/Dockerfile \
  && test -f apps/lazuar-developers/Dockerfile \
  && echo "Dockerfiles present"

# Bake target names resolve (list only; no push)
docker buildx bake --print 2>/dev/null | head -c 2000 || true
```

---

## 9. Optional smoke builds (post-implement)

Prefer linux/amd64 if matching GHCR; local arm64 Mac may still validate Dockerfile path correctness:

```bash
# Single-target bake (after target rename)
docker buildx bake lazuar-ops --set "*.platform=linux/amd64" --load

# Or compose build one service
docker compose --profile full build lazuar-ops

# Full group (heavy)
# docker buildx bake
```

Next apps (`lazuar-portal`, `lazuar-developers`) are the highest risk (standalone path layout). Prioritize those if only two smoke builds are feasible.

---

## 10. Out of scope reminders (next phases)

| Phase | Items |
|-------|--------|
| 3 | pnpm-lock importers, turbo/package filters, workspace package consumers |
| 4 | mprocs, Taskfile filter strings if any, scripts |
| 5+ | Living docs, ADRs, README docker examples mentioning `ops-page` bake targets |
| Never in this rename | GHCR package cutover, prod service/Caddy renames, public path rebrand |

---

## 11. Quick implement checklist

- [ ] `apps/lazuar-ops/Dockerfile` — `apps/ops-page` → `apps/lazuar-ops`
- [ ] `apps/lazuar-portal/Dockerfile` — `apps/portal-page` → `apps/lazuar-portal` (incl. CMD + static dest)
- [ ] `apps/lazuar-admin/Dockerfile` — `apps/superadmin-page` → `apps/lazuar-admin`
- [ ] `apps/lazuar-developers/Dockerfile` — `apps/developers-page` → `apps/lazuar-developers` (incl. CMD + static dest)
- [ ] `docker-bake.hcl` — paths + target ids + default group; **keep** `lazuar-hub-*` tags
- [ ] `docker-compose.yml` — paths + service keys; **keep** images/container_names/ports
- [ ] `docker-compose.ghcr.yml` — service keys only; **keep** images
- [ ] `.github/workflows/ghcr.yml` — matrix dockerfile paths only; **keep** `matrix.name`
- [ ] Optional: `lazuar-developers` service on local compose (+ ghcr compose)
- [ ] Verification greps (§8) clean
- [ ] Do **not** edit `deploy/prod/**` or `scripts/remote-deploy.sh`

---

## 12. Diff cardinality estimate

| File | Rough change size |
|------|-------------------|
| Each Vite Dockerfile (ops, admin) | ~5 path lines |
| Each Next Dockerfile (portal, developers) | ~8 path lines (incl. dual-side COPY) |
| `docker-bake.hcl` | ~4 dockerfile strings + 4 target renames + 1 group line |
| `docker-compose.yml` | 3 dockerfile + 3 service key renames (+ optional ~15-line developers block) |
| `docker-compose.ghcr.yml` | 3 service key renames only |
| `ghcr.yml` | 4 matrix dockerfile lines |

No new files required. Analysis artifact: this file only.
