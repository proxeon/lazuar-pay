# Phase 2 — Done

**Date:** 2026-08-09  
**Branch:** `chore/rename-frontend-apps-lazuar-prefix`  
**Commit message:** `chore(docker): update paths for lazuar-* frontend apps`

---

## What landed

| Area | Change |
|------|--------|
| `apps/lazuar-ops/Dockerfile` | `apps/ops-page` → `apps/lazuar-ops` (COPY / filter / dist) |
| `apps/lazuar-portal/Dockerfile` | `apps/portal-page` → `apps/lazuar-portal` (incl. standalone static dest + CMD) |
| `apps/lazuar-admin/Dockerfile` | `apps/superadmin-page` → `apps/lazuar-admin` |
| `apps/lazuar-developers/Dockerfile` | `apps/developers-page` → `apps/lazuar-developers` (incl. CMD + static; kept `@repo/api-spec`) |
| `docker-bake.hcl` | dockerfile paths + target ids → `lazuar-{portal,ops,admin,developers}`; **tags stay `lazuar-hub-*`** |
| `docker-compose.yml` | service keys + dockerfile paths; **images stay `lazuar-hub-*:local`**; added `lazuar-developers` on `3002` profile `full` |
| `docker-compose.ghcr.yml` | service keys; **images stay `lazuar-hub-*`**; added pull service `lazuar-developers` on `3002` |
| `.github/workflows/ghcr.yml` | matrix `dockerfile:` paths only; **`name:` stays `lazuar-hub-*`** |

**Not touched (by design):** `deploy/prod/**`, `scripts/remote-deploy.sh`, mprocs, `pnpm-lock.yaml`, GHCR image repository names, public base paths (`/portal`, `/docs`, `/admin/`).

---

## Verification (grep proof)

### Stale `apps/*-page` paths gone from docker/ci/compose/bake

```bash
rg -n 'apps/(ops|portal|developers|superadmin)-page' \
  apps/lazuar-ops/Dockerfile \
  apps/lazuar-portal/Dockerfile \
  apps/lazuar-admin/Dockerfile \
  apps/lazuar-developers/Dockerfile \
  docker-bake.hcl \
  docker-compose.yml \
  docker-compose.ghcr.yml \
  .github/workflows/ghcr.yml
# → no matches
```

### New Dockerfiles referenced

```bash
rg -n 'apps/lazuar-(ops|portal|admin|developers)/Dockerfile' \
  docker-bake.hcl docker-compose.yml .github/workflows/ghcr.yml
```

```text
docker-bake.hcl:79:  dockerfile = "apps/lazuar-portal/Dockerfile"
docker-bake.hcl:96:  dockerfile = "apps/lazuar-ops/Dockerfile"
docker-bake.hcl:114:  dockerfile = "apps/lazuar-admin/Dockerfile"
docker-bake.hcl:131:  dockerfile = "apps/lazuar-developers/Dockerfile"
.github/workflows/ghcr.yml:68:            dockerfile: apps/lazuar-portal/Dockerfile
.github/workflows/ghcr.yml:73:            dockerfile: apps/lazuar-ops/Dockerfile
.github/workflows/ghcr.yml:79:            dockerfile: apps/lazuar-admin/Dockerfile
.github/workflows/ghcr.yml:84:            dockerfile: apps/lazuar-developers/Dockerfile
docker-compose.yml:52:      dockerfile: apps/lazuar-ops/Dockerfile
docker-compose.yml:69:      dockerfile: apps/lazuar-portal/Dockerfile
docker-compose.yml:88:      dockerfile: apps/lazuar-admin/Dockerfile
docker-compose.yml:104:      dockerfile: apps/lazuar-developers/Dockerfile
```

### Old bake/compose service target names gone

```bash
rg -n '(portal-page|ops-page|superadmin-page|developers-page)' \
  docker-bake.hcl docker-compose.yml docker-compose.ghcr.yml
# → no matches
```

### GHCR image names still `lazuar-hub-*` (KEEP)

```bash
rg -n 'lazuar-hub-(api|ops|portal|superadmin|developers)' \
  docker-bake.hcl docker-compose.yml docker-compose.ghcr.yml \
  .github/workflows/ghcr.yml deploy/prod/docker-compose.yml
# → present (all five image families)
```

`lazuar-hub-superadmin` retained (folder is `lazuar-admin`; intentional mismatch).

### Next CMD paths

```text
apps/lazuar-portal/Dockerfile:52:CMD ["node", "apps/lazuar-portal/server.js"]
apps/lazuar-developers/Dockerfile:50:CMD ["node", "apps/lazuar-developers/server.js"]
```

### Matrix `name:` unchanged

```text
name: lazuar-hub-api
name: lazuar-hub-portal
name: lazuar-hub-ops
name: lazuar-hub-superadmin
name: lazuar-hub-developers
```

### Deploy / remote-deploy untouched

```bash
git status --short deploy/prod scripts/remote-deploy.sh
# → empty
```

```bash
rg -n 'apps/.*-page|apps/lazuar-(ops|portal|admin|developers)' deploy/prod/
# → no matches (images only)
```

### Dockerfiles present

```bash
test -f apps/lazuar-ops/Dockerfile \
  && test -f apps/lazuar-portal/Dockerfile \
  && test -f apps/lazuar-admin/Dockerfile \
  && test -f apps/lazuar-developers/Dockerfile \
  && echo "Dockerfiles present"
# → Dockerfiles present
```

---

## Optional parity added

| File | Service | Port | Image |
|------|---------|------|-------|
| `docker-compose.yml` | `lazuar-developers` | `3002:3000` | `lazuar-hub-developers:local` (build) |
| `docker-compose.ghcr.yml` | `lazuar-developers` | `3002:3000` | `lazuar-hub-developers:${TAG:-latest}` (pull) |

Local compose keeps `profiles: ["full"]` for all four frontends.

---

## Expected remaining breakage (later phases)

| Phase | Still broken until |
|-------|--------------------|
| 3 | `mprocs-dev.yaml` still `cd apps/*-page` |
| 4 | `pnpm-lock.yaml` importers still old paths; living docs may cite old bake target names |

Docker/CI/compose/bake path surface is green after this phase (full image builds still need a valid workspace install inside the image; lockfile importer paths are Phase 4).
