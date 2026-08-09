# Phase 5 — Analysis: verification plan (before merge)

**Status:** Analysis only — **do not implement renames here**; execute the commands in §3–§8 as a gate before merge.  
**Date:** 2026-08-09  
**Branch:** `chore/rename-frontend-apps-lazuar-prefix`  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Related:** [`phase-4-done.md`](./phase-4-done.md), [`11-implementation-checklist.md`](./11-implementation-checklist.md) § Phase 5

---

## 1. Phase 5 goal

Phases 1–4 are complete (folders, Docker/CI paths, mprocs/Taskfile, lockfile, living docs). Phase 5 **proves mechanical completeness** of the rename before PR merge:

| # | Prove | How |
|---|-------|-----|
| 1 | No **functional** leftovers of `*-page` paths/names | Grep gates (§3) |
| 2 | pnpm package filters resolve for all four apps | Filter smoke (§4) |
| 3 | Apps typecheck / lint under new package identity | Preferred light builds (§5) |
| 4 | (Optional) Docker path correctness if time allows | Bake / docker build notes (§6) |
| 5 | Local FE orchestration still boots | `task fe` + ports (§7) |

**Not Phase 5:**

- Further renames, docs archaeology (`docs/001-gaps/**`, ADRs) — Phase 7
- GHCR image rebrand, prod compose/Caddy changes — out of scope forever for this PR
- Full monorepo `turbo build` or full `docker buildx bake` default group as a hard gate (recommended only if cheap)

---

## 2. Locked identity (post Phase 1–4)

| Old folder / package / filter | New folder | New package `"name"` | Bake target | GHCR image (**unchanged**) | Dev port |
|-------------------------------|------------|----------------------|-------------|----------------------------|----------|
| `developers-page` | `apps/lazuar-developers` | `lazuar-developers` | `lazuar-developers` | `lazuar-hub-developers` | 3002 |
| `ops-page` | `apps/lazuar-ops` | `lazuar-ops` | `lazuar-ops` | `lazuar-hub-ops` | 3003 |
| `portal-page` | `apps/lazuar-portal` | `lazuar-portal` | `lazuar-portal` | `lazuar-hub-portal` | 3004 |
| `superadmin-page` | `apps/lazuar-admin` | `lazuar-admin` | `lazuar-admin` | `lazuar-hub-superadmin` | 3005 |

**Also present (unchanged by rename):** `lazuar-api`, `lazuar-docs`, packages under `packages/*`.

All commands below assume:

```bash
cd /Users/akmalfirdaus/Code/lazuar/lazuar-pay
```

---

## 3. Grep gates (functional path leftovers)

### 3.0 Shared excludes

Reuse these globs on every broad search (build artifacts / deps):

```text
--glob '!**/node_modules/**' --glob '!**/.next/**' --glob '!**/dist/**' \
--glob '!**/bin/**' --glob '!**/obj/**' --glob '!**/.turbo/**'
```

Historical / inventory trees that **may** still say `*-page` (Phase 7 optional):

```text
docs/001-gaps/**
docs/architecture-decision-log/**
plans/002-change-name/**
```

---

### 3.1 HARD FAIL — path form `apps/*-page` (must be empty outside inventory)

Any remaining `apps/(developers|ops|portal|superadmin)-page` in tooling/source is a **P0** miss from Phases 1–4.

```bash
rg -n 'apps/(developers|ops|portal|superadmin)-page' \
  --glob '!**/node_modules/**' --glob '!**/.next/**' --glob '!**/dist/**' \
  --glob '!**/bin/**' --glob '!**/obj/**' \
  --glob '!plans/002-change-name/**' \
  --glob '!docs/001-gaps/**' \
  --glob '!docs/architecture-decision-log/**'
# PASS: no matches
# FAIL: any hit → fix before merge (Dockerfile / bake / compose / mprocs / lockfile / living docs)
```

**Surface-specific hard gates** (must also be empty):

```bash
# Docker + CI surface
rg -n 'apps/(developers|ops|portal|superadmin)-page|(developers|ops|portal|superadmin)-page' \
  apps/lazuar-ops/Dockerfile \
  apps/lazuar-portal/Dockerfile \
  apps/lazuar-admin/Dockerfile \
  apps/lazuar-developers/Dockerfile \
  docker-bake.hcl \
  docker-compose.yml \
  docker-compose.ghcr.yml \
  .github/workflows/ghcr.yml
# PASS: no matches

# mprocs + Taskfile (local DX)
rg -n '(developers|ops|portal|superadmin)-page' \
  mprocs-dev.yaml Taskfile.yml
# PASS: no matches

# Lockfile importers (stale keys)
rg -n '^  apps/(developers|ops|portal|superadmin)-page:' pnpm-lock.yaml
# PASS: no matches
```

---

### 3.2 HARD FAIL — living commands / filters still using old package names

```bash
# Any living --filter old-name
rg -n 'pnpm --filter (developers-page|ops-page|portal-page|superadmin-page)' \
  --glob '!**/node_modules/**' \
  --glob '!plans/002-change-name/**' \
  --glob '!docs/001-gaps/**' \
  --glob '!docs/architecture-decision-log/**'
# PASS: no matches

# Living docs surfaces (Phase 4 contract)
rg -n 'developers-page|ops-page|portal-page|superadmin-page' \
  README.md apps/lazuar-docs docs/contracts plans/001-backend
# PASS: no matches

# Root package scripts still pointing at old names
rg -n 'developers-page|ops-page|portal-page|superadmin-page' package.json
# PASS: no matches
```

---

### 3.3 HARD FAIL — package `"name"` still old

```bash
rg -n '"name":\s*"(developers|ops|portal|superadmin)-page"' \
  apps/lazuar-developers/package.json \
  apps/lazuar-ops/package.json \
  apps/lazuar-portal/package.json \
  apps/lazuar-admin/package.json
# PASS: no matches

# Positive proof of new names
rg -n '"name":\s*"lazuar-(developers|ops|portal|admin)"' \
  apps/lazuar-developers/package.json \
  apps/lazuar-ops/package.json \
  apps/lazuar-portal/package.json \
  apps/lazuar-admin/package.json
# PASS: exactly four lines (one per file)
```

---

### 3.4 HARD FAIL — missing positive new paths (prove renames landed)

```bash
# Folders exist
test -d apps/lazuar-developers \
  && test -d apps/lazuar-ops \
  && test -d apps/lazuar-portal \
  && test -d apps/lazuar-admin \
  && echo "PASS: app dirs present"

# Old folders gone
test ! -d apps/developers-page \
  && test ! -d apps/ops-page \
  && test ! -d apps/portal-page \
  && test ! -d apps/superadmin-page \
  && echo "PASS: old *-page dirs absent"

# Dockerfiles + Next standalone CMDs (P0 historical failure mode)
rg -n 'apps/lazuar-(portal|developers)/Dockerfile' docker-bake.hcl .github/workflows/ghcr.yml docker-compose.yml
# PASS: hits for both apps

rg -n 'CMD \[\"node\", \"apps/lazuar-(portal|developers)/server\.js\"\]' \
  apps/lazuar-portal/Dockerfile apps/lazuar-developers/Dockerfile
# PASS: one CMD each (portal + developers)

# Lockfile importers present
rg -n '^  apps/lazuar-(developers|ops|portal|admin):' pnpm-lock.yaml
# PASS: four importer keys

# mprocs cwd paths
rg -n 'cd apps/lazuar-(developers|ops|portal|admin)' mprocs-dev.yaml
# PASS: four shells
```

---

### 3.5 HARD FAIL — GHCR / prod safety (must **still** use old image names)

Rename must **not** have rebranded images or prod services.

```bash
# GHCR image names still hub-* (including superadmin, not admin)
rg -n 'lazuar-hub-(api|ops|portal|superadmin|developers)' \
  docker-bake.hcl docker-compose.yml docker-compose.ghcr.yml \
  .github/workflows/ghcr.yml deploy/prod/docker-compose.yml
# PASS: present across bake + compose + ghcr + prod

rg -n 'lazuar-hub-superadmin' \
  docker-bake.hcl docker-compose.yml docker-compose.ghcr.yml \
  .github/workflows/ghcr.yml deploy/prod/docker-compose.yml
# PASS: still present (admin app → superadmin image is intentional)

# ghcr matrix name: still hub-*; dockerfile: under apps/lazuar-*
rg -n 'name: lazuar-hub-|dockerfile: apps/lazuar-' .github/workflows/ghcr.yml
# PASS: five images; four FE dockerfiles under apps/lazuar-*

# Prod must NOT reference monorepo app folders
rg -n 'apps/(developers|ops|portal|superadmin)-page|apps/lazuar-(ops|portal|admin|developers)' \
  deploy/prod/
# PASS: no matches (images + short service names only)
```

---

### 3.6 SOFT / REVIEW — bare `*-page` tokens outside allowed history

Run this, then **triage** every hit. Do not bulk-delete without reading.

```bash
rg -n 'developers-page|ops-page|portal-page|superadmin-page' \
  --glob '!**/node_modules/**' --glob '!**/.next/**' --glob '!**/dist/**' \
  --glob '!**/bin/**' --glob '!**/obj/**'
```

**Triage rules:**

| Location of hit | Verdict |
|-----------------|---------|
| Dockerfiles, bake, compose, ghcr.yml, mprocs, Taskfile, `package.json` names, lockfile importers, root README living cmds, `apps/lazuar-docs/**` living cmds, `docs/contracts/**`, `plans/001-backend/**` | **FAIL** — fix now |
| `docs/001-gaps/**` | **ALLOW** — Phase 7 archaeology |
| `docs/architecture-decision-log/**` | **ALLOW** — ADR history |
| `plans/002-change-name/**` | **ALLOW** — this rename program |
| App source product copy / comments that are not paths | **Review** — cosmetic only; not a merge blocker unless it teaches a wrong `pnpm --filter` |
| Backend `Modules/Ops` etc. | **Not a match** for `*-page`; leave alone |

Inventory of allowed remaining (for PR body):

```bash
rg -n 'developers-page|ops-page|portal-page|superadmin-page' \
  --glob '!**/node_modules/**' \
  docs/001-gaps docs/architecture-decision-log plans/002-change-name
# PASS as “allowed remaining”: matches may exist; list count in PR body
```

---

### 3.7 Grep gate summary (copy into Phase 5 done / PR)

| Gate | Command family | Pass condition |
|------|----------------|----------------|
| G1 | `apps/*-page` outside history | 0 matches |
| G2 | Docker/CI/mprocs/Taskfile old tokens | 0 matches |
| G3 | Lockfile old importers | 0 matches |
| G4 | Living docs + `--filter *-page` | 0 matches |
| G5 | package.json old `"name"` | 0 matches |
| G6 | Positive new dirs / CMDs / importers / mprocs | present as specified |
| G7 | GHCR still `lazuar-hub-*` incl. `superadmin` | present |
| G8 | `deploy/prod` has no app folder paths | 0 matches |
| G9 | Bare `*-page` triage | only allowed history (or documented cosmetic) |

**Overall grep gate:** G1–G8 all PASS → functional path surface green. G9 reviewed and only ALLOW hits remain.

---

## 4. pnpm filter smoke

### 4.1 Install honesty (should already be clean from Phase 4)

```bash
pnpm install
# PASS: exit 0; no unexpected rewrite of importer keys back to *-page
# FAIL: missing workspace package, broken lockfile, or reintroduces apps/*-page importers
```

Re-check importers after install:

```bash
rg -n '^  apps/(developers|ops|portal|superadmin)-page:' pnpm-lock.yaml
# PASS: no matches

rg -n '^  apps/lazuar-(developers|ops|portal|admin):' pnpm-lock.yaml
# PASS: four keys
```

### 4.2 Filter resolves (required)

```bash
pnpm --filter lazuar-developers exec node -e "console.log('ok developers')"
pnpm --filter lazuar-ops exec node -e "console.log('ok ops')"
pnpm --filter lazuar-portal exec node -e "console.log('ok portal')"
pnpm --filter lazuar-admin exec node -e "console.log('ok admin')"
# PASS: prints ok developers / ops / portal / admin; exit 0 each
# FAIL: ERR_PNPM_FILTER_NOT_MATCHED or similar → package name / lockfile / workspace path wrong
```

Negative control (old names must **not** resolve):

```bash
pnpm --filter ops-page exec node -e "console.log('should not run')" ; echo "exit=$?"
pnpm --filter developers-page exec node -e "console.log('should not run')" ; echo "exit=$?"
# PASS: non-zero exit / filter not matched; must not print "should not run"
```

### 4.3 Optional package-list proof

```bash
pnpm list --depth -1 --filter lazuar-developers \
  --filter lazuar-ops \
  --filter lazuar-portal \
  --filter lazuar-admin
# PASS: lists all four packages under apps/lazuar-*
```

---

## 5. Preferred light builds (lint / tsc) — **do these before Docker**

Heavy Docker multi-stage builds are **not** required for Phase 5 exit if lint/tsc + grep + filter smoke are green. Docker is optional depth (§6).

### 5.1 What each app’s `lint` does

| Package | `"lint"` script | Notes |
|---------|-----------------|-------|
| `lazuar-ops` | `tsc --noEmit` | True typecheck — **preferred gate** |
| `lazuar-admin` | `tsc --noEmit` | True typecheck — **preferred gate** |
| `lazuar-portal` | `eslint` | Lint only (no `tsc` script); optional `npx tsc --noEmit` |
| `lazuar-developers` | `eslint` | Lint only; optional `npx tsc --noEmit` |

### 5.2 Required preferred smoke (Vite apps — cheapest real typecheck)

```bash
pnpm --filter lazuar-ops lint
pnpm --filter lazuar-admin lint
# PASS: exit 0
# FAIL: type errors — may be pre-existing; if so, note in PR body as unrelated to rename
#       and still require that the *command resolves* (filter works) and that errors
#       are not "cannot find package" / wrong path artifacts from rename
```

**Rename-specific fail signals (always block merge):**

- Filter not found
- Cannot resolve workspace package paths under `apps/*-page`
- Missing `tsconfig` because cwd/path wrong

**Pre-existing type errors:** document; do not invent refactors in Phase 5. Prefer proving **tooling path identity**, not app bugfixup.

### 5.3 Recommended Next apps (eslint + optional tsc)

```bash
pnpm --filter lazuar-portal lint
pnpm --filter lazuar-developers lint
# PASS: exit 0 (or known pre-existing lint debt documented)

# Stronger optional typecheck (no package script; run in package dir)
pnpm --filter lazuar-portal exec tsc --noEmit -p tsconfig.json
pnpm --filter lazuar-developers exec tsc --noEmit -p tsconfig.json
# PASS: exit 0; same triage rules as §5.2
```

### 5.4 Optional full app builds (medium weight — prefer if time, still lighter than Docker)

```bash
# Vite SPA builds (fast)
pnpm --filter lazuar-ops build
pnpm --filter lazuar-admin build
# PASS: dist/ produced; exit 0

# Next builds (heavier; needs more memory/time)
pnpm --filter lazuar-portal build
pnpm --filter lazuar-developers build
# PASS: .next produced; exit 0
# Note: developers build may need api-spec OpenAPI dist depending on app wiring;
#       local `next build` is still a good rename smoke even if OpenAPI path is separate.
```

### 5.5 Explicitly **not** required for Phase 5

```bash
# Whole monorepo turbo — broad, slow, not rename-specific
pnpm build          # turbo run build — SKIP as hard gate
pnpm lint           # turbo run lint — optional curiosity only
pnpm check-types    # only if packages define check-types — SKIP as hard gate
```

---

## 6. Optional Docker verification

### 6.1 Recommendation

| Depth | When | Cost |
|-------|------|------|
| **Skip Docker** | Lint/tsc + grep G1–G8 + filter smoke green; CI will build on merge via `ghcr.yml` | Lowest |
| **Static path proof only** | Always cheap; do even if skip image build | Seconds |
| **Single FE image** | Want extra confidence on Next `CMD` / COPY paths | Medium (minutes–tens of minutes) |
| **Full bake default group** | Pre-merge paranoia / no trust in CI | High — **not recommended** as local hard gate on every machine |

**Prefer:** static path proof (§6.2) always; at most one Next + one Vite image if machine is free. Do **not** block on full multi-arch bake unless CI is unavailable.

### 6.2 Static path proof (recommended, no image build)

```bash
# Dockerfiles exist
test -f apps/lazuar-ops/Dockerfile \
  && test -f apps/lazuar-portal/Dockerfile \
  && test -f apps/lazuar-admin/Dockerfile \
  && test -f apps/lazuar-developers/Dockerfile \
  && echo "PASS: Dockerfiles present"

# Bake lists new targets + old GHCR tags
docker buildx bake --print 2>/dev/null | head -c 4000 || true
# Manual check: targets lazuar-portal|ops|admin|developers; tags still lazuar-hub-*

# Or without docker: read bake file greps from §3.4–§3.5
rg -n 'target "lazuar-(portal|ops|admin|developers)"' docker-bake.hcl
# PASS: four targets

rg -n 'dockerfile = "apps/lazuar-(portal|ops|admin|developers)/Dockerfile"' docker-bake.hcl
# PASS: four dockerfile paths
```

### 6.3 Single-target bake (optional — recommended pick if doing any Docker)

**Highest value:** Next apps (standalone `server.js` path was the historical P0).

```bash
# Ensure builder exists (Taskfile helper)
task docker:builder

# Portal (Next standalone) — preferred single smoke
docker buildx bake lazuar-portal --set "*.platform=linux/amd64" --load

# Developers (Next + api-spec copy) — second-highest value
docker buildx bake lazuar-developers --set "*.platform=linux/amd64" --load

# One Vite app (ops or admin) — path-only validation of filter/COPY
docker buildx bake lazuar-ops --set "*.platform=linux/amd64" --load
```

Equivalent raw docker (no bake):

```bash
docker build -f apps/lazuar-portal/Dockerfile .
docker build -f apps/lazuar-developers/Dockerfile .
docker build -f apps/lazuar-ops/Dockerfile .
```

**PASS:** image builds to completion (exit 0).  
**FAIL:** `COPY failed: file not found`, `pnpm --filter ./apps/*-page`, missing `server.js` path under `apps/*-page`.

### 6.4 Optional runtime smoke (only after §6.3)

```bash
# Portal healthcheck path is /portal
docker run --rm -p 13004:3000 --name smoke-portal \
  "$(docker images --format '{{.Repository}}:{{.Tag}}' | rg 'lazuar-hub-portal' | head -1)" &
sleep 15
curl -sfS -o /dev/null -w "%{http_code}\n" http://127.0.0.1:13004/portal
# PASS: 200 (or 307/308 if Next redirects — not 000/connection refused)
docker stop smoke-portal 2>/dev/null || true

# Developers healthcheck path is /docs
docker run --rm -p 13002:3000 --name smoke-developers \
  "$(docker images --format '{{.Repository}}:{{.Tag}}' | rg 'lazuar-hub-developers' | head -1)" &
sleep 15
curl -sfS -o /dev/null -w "%{http_code}\n" http://127.0.0.1:13002/docs
# PASS: HTTP response (not connection refused)
docker stop smoke-developers 2>/dev/null || true
```

### 6.5 Full bake / compose (recommended note only — **not** a hard gate)

```bash
# Full default group (api + 4 FE) — HEAVY; prefer CI on merge
task docker:build
# or: docker buildx bake --load --set "*.platform=linux/amd64"

# Local compose build one service
docker compose build lazuar-portal
docker compose build lazuar-developers
```

**CI expectation (Phase 6, observe):** on merge to `main`, `.github/workflows/ghcr.yml` builds all five images using **new** `dockerfile:` paths and **same** `lazuar-hub-*` names. Local full bake is redundant if CI is trusted.

---

## 7. Local FE orchestration smoke

### 7.1 mprocs / task fe (required for “local FE boots”)

```bash
# Either:
task fe
# or:
mprocs -c mprocs-dev.yaml
```

**PASS criteria:**

| Check | Expect |
|-------|--------|
| Process list | Four autostart procs: `lazuar-developers`, `lazuar-ops`, `lazuar-admin`, `lazuar-portal` |
| No `cd: no such file or directory` | Paths `apps/lazuar-*` exist |
| Dev servers bind | Ports below respond |

### 7.2 Port smoke

```bash
# After servers are up (few seconds for Vite; Next may take longer)
curl -sfS -o /dev/null -w "developers %{http_code}\n" http://127.0.0.1:3002/ || true
curl -sfS -o /dev/null -w "ops         %{http_code}\n" http://127.0.0.1:3003/ || true
curl -sfS -o /dev/null -w "portal      %{http_code}\n" http://127.0.0.1:3004/ || true
curl -sfS -o /dev/null -w "admin       %{http_code}\n" http://127.0.0.1:3005/ || true
```

**PASS:** each port accepts TCP and returns some HTTP status (2xx/3xx typical).  
**FAIL:** connection refused after generous wait → mprocs cwd / package `dev` script / port conflict.

**Port map (locked):**

| App | Port | `dev` script source |
|-----|------|---------------------|
| `lazuar-developers` | 3002 | `next dev -p 3002` |
| `lazuar-ops` | 3003 | `vite --port=3003` |
| `lazuar-portal` | 3004 | `next dev -p 3004` |
| `lazuar-admin` | 3005 | `vite --port=3005` |

### 7.3 API port (optional full stack)

```bash
# Only if running API separately
task dev
# API expected on 8080 when full stack is up — not a rename gate if API already worked
curl -sfS -o /dev/null -w "api %{http_code}\n" http://127.0.0.1:8080/ || true
```

Rename does not change API routes or CORS ports; API smoke is **nice-to-have**, not required to call Phase 5 done.

### 7.4 Single-app dev without mprocs (fallback smoke)

```bash
pnpm --filter lazuar-ops dev
# Ctrl+C after homepage loads on :3003

pnpm --filter lazuar-portal dev
# Ctrl+C after homepage loads on :3004
```

---

## 8. Ordered verification runbook (copy-paste)

Run from repo root. Stop on first **hard** failure (G1–G8, filter smoke).

```bash
cd /Users/akmalfirdaus/Code/lazuar/lazuar-pay

# ---------- A. Identity / folders ----------
test -d apps/lazuar-developers && test -d apps/lazuar-ops \
  && test -d apps/lazuar-portal && test -d apps/lazuar-admin \
  && test ! -d apps/developers-page && test ! -d apps/ops-page \
  && test ! -d apps/portal-page && test ! -d apps/superadmin-page \
  && echo "A PASS dirs"

# ---------- B. Grep hard gates ----------
rg -n 'apps/(developers|ops|portal|superadmin)-page' \
  --glob '!**/node_modules/**' --glob '!**/.next/**' --glob '!**/dist/**' \
  --glob '!**/bin/**' --glob '!**/obj/**' \
  --glob '!plans/002-change-name/**' \
  --glob '!docs/001-gaps/**' \
  --glob '!docs/architecture-decision-log/**'
# expect empty

rg -n '(developers|ops|portal|superadmin)-page' \
  apps/lazuar-ops/Dockerfile apps/lazuar-portal/Dockerfile \
  apps/lazuar-admin/Dockerfile apps/lazuar-developers/Dockerfile \
  docker-bake.hcl docker-compose.yml docker-compose.ghcr.yml \
  .github/workflows/ghcr.yml mprocs-dev.yaml Taskfile.yml package.json
# expect empty

rg -n '^  apps/(developers|ops|portal|superadmin)-page:' pnpm-lock.yaml
# expect empty

rg -n 'developers-page|ops-page|portal-page|superadmin-page' \
  README.md apps/lazuar-docs docs/contracts plans/001-backend
# expect empty

rg -n 'pnpm --filter (developers-page|ops-page|portal-page|superadmin-page)' \
  --glob '!**/node_modules/**' \
  --glob '!plans/002-change-name/**' \
  --glob '!docs/001-gaps/**' \
  --glob '!docs/architecture-decision-log/**'
# expect empty

rg -n 'CMD \[\"node\", \"apps/lazuar-(portal|developers)/server\.js\"\]' \
  apps/lazuar-portal/Dockerfile apps/lazuar-developers/Dockerfile
# expect 2 hits

rg -n 'lazuar-hub-superadmin' docker-bake.hcl deploy/prod/docker-compose.yml .github/workflows/ghcr.yml
# expect present

rg -n 'apps/(developers|ops|portal|superadmin)-page|apps/lazuar-(ops|portal|admin|developers)' deploy/prod/
# expect empty

# ---------- C. pnpm filter smoke ----------
pnpm install
pnpm --filter lazuar-developers exec node -e "console.log('ok developers')"
pnpm --filter lazuar-ops exec node -e "console.log('ok ops')"
pnpm --filter lazuar-portal exec node -e "console.log('ok portal')"
pnpm --filter lazuar-admin exec node -e "console.log('ok admin')"

# ---------- D. Preferred lint/tsc ----------
pnpm --filter lazuar-ops lint
pnpm --filter lazuar-admin lint
# recommended:
pnpm --filter lazuar-portal lint
pnpm --filter lazuar-developers lint

# ---------- E. Optional medium builds ----------
# pnpm --filter lazuar-ops build
# pnpm --filter lazuar-admin build

# ---------- F. Optional Docker (skip if time-constrained) ----------
# task docker:builder
# docker buildx bake lazuar-portal --set "*.platform=linux/amd64" --load

# ---------- G. Local FE (interactive) ----------
# task fe
# then curl ports 3002–3005

# ---------- H. Allowed remaining inventory (for PR body) ----------
rg -n 'developers-page|ops-page|portal-page|superadmin-page' \
  --glob '!**/node_modules/**' \
  docs/001-gaps docs/architecture-decision-log plans/002-change-name \
  | head -n 50
```

---

## 9. Pass / fail definition

### 9.1 Phase 5 **PASS** (merge-ready for rename)

All of the following:

| ID | Criterion | Hard? |
|----|-----------|-------|
| P1 | G1–G8 grep gates pass (§3) | **Yes** |
| P2 | G9 triaged: only ALLOW history (or documented cosmetic) | **Yes** |
| P3 | Four dirs present; old `*-page` dirs absent | **Yes** |
| P4 | `pnpm install` exit 0; lockfile importers `apps/lazuar-*` | **Yes** |
| P5 | Four `pnpm --filter lazuar-*` smoke cmds print `ok …` | **Yes** |
| P6 | `lazuar-ops` + `lazuar-admin` `lint` (`tsc --noEmit`) run under new filters (exit 0 **or** only pre-existing type errors documented, no path/resolution breakage) | **Yes** (at least: commands resolve + no rename-induced path errors) |
| P7 | `task fe` / mprocs starts four processes without `cd` failures; ports 3002–3005 accept connections **or** single-app `pnpm --filter … dev` proven for at least one Vite + one Next | **Yes** |
| P8 | Next Dockerfiles still `CMD ["node", "apps/lazuar-{portal,developers}/server.js"]` | **Yes** (static; image build optional) |
| P9 | GHCR names still `lazuar-hub-*` incl. `lazuar-hub-superadmin`; prod has no monorepo app paths | **Yes** |

### 9.2 Phase 5 **FAIL** (do not merge)

Any of:

| ID | Failure |
|----|---------|
| F1 | Any `apps/*-page` hit outside allowed history |
| F2 | Docker / bake / compose / ghcr / mprocs / Taskfile still reference `*-page` |
| F3 | Lockfile importers still `apps/*-page` or missing `apps/lazuar-*` |
| F4 | Living docs or `--filter *-page` commands remain |
| F5 | `pnpm --filter lazuar-*` does not resolve |
| F6 | mprocs `cd apps/*-page` or missing `apps/lazuar-*` |
| F7 | Next Dockerfile `CMD` still under `apps/*-page/server.js` |
| F8 | Accidental GHCR rename (e.g. missing `lazuar-hub-superadmin` or image retagged to `lazuar-hub-admin` only) |
| F9 | `deploy/prod` rewritten to monorepo paths |

### 9.3 Soft fails / non-blockers

| Item | Handling |
|------|----------|
| Pre-existing `tsc` / eslint debt unrelated to paths | Document in PR; do not expand Phase 5 into bugfix |
| Full Docker bake not run locally | OK if P1–P9 static + pnpm green; CI builds on merge |
| Full Next `pnpm build` not run | OK if lint/tsc + static Dockerfile CMD proof |
| Historical `*-page` in gaps/ADRs/plans | Expected; list in PR body |
| API not running on 8080 during FE smoke | Not a rename failure |
| `tunnel:fe` / community leftovers | Phase 7.4 DX nits |

### 9.4 Mapping to checklist §5.5

| Checklist exit | Covered by |
|----------------|------------|
| Local FE boots | P7 |
| Critical Docker builds green | P8 static required; §6.3 image optional but recommended for Next |
| Grep gate clean for **functional** paths | P1–P3, P9 |

---

## 10. Suggested evidence block for PR body

```markdown
## Phase 5 verification

- [x] Grep gates G1–G8 clean (functional paths)
- [x] Allowed remaining: docs/001-gaps, ADRs, plans/002-change-name only
- [x] pnpm --filter lazuar-{developers,ops,portal,admin} smoke OK
- [x] lint/tsc: lazuar-ops, lazuar-admin (and …)
- [x] task fe / ports 3002–3005 (or single-app dev smoke)
- [ ] Optional docker buildx bake lazuar-portal: (ran / skipped — CI covers)
- [x] GHCR still lazuar-hub-*; prod paths untouched
```

---

## 11. Out of scope for this analysis document

- Implementing any code/doc fixes (if a gate fails, fix in the appropriate Phase 1–4 surface, not by inventing Phase 5 work)
- Running full Docker bake as part of **authoring** this plan
- Phase 6 PR merge / prod observe
- Phase 7 docs archaeology / GHCR rebrand

---

## 12. Phase 5 exit criteria (checklist mirror)

- [ ] Grep functional gates pass (G1–G8); G9 only ALLOW
- [ ] pnpm filter smoke for all four `lazuar-*` apps
- [ ] Preferred lint/tsc on Vite apps; Next lint recommended
- [ ] Local FE orchestration or single-app dev smoke
- [ ] Next Dockerfile CMDs path-correct (static); Docker image optional
- [ ] GHCR + prod safety greps still green
- [ ] Evidence recorded for PR / `phase-5-done.md` when executed

---

*End of Phase 5 analysis. No application or tooling files were modified by this document’s authoring step. Commands are for the implementer/verifier to run.*
