# 002 — Frontend app rename: implementation checklist

**Status:** Ready to implement (no code changed yet)  
**Date:** 2026-08-08  
**Related:** [README.md](./README.md) (decisions), analyses `01`–`10` (evidence)

---

## Final naming (locked for this checklist)

| Current folder / package | **Target folder / package** | GHCR image (unchanged) | Prod service (unchanged) | Public path (unchanged) | Dev port (unchanged) |
|--------------------------|-----------------------------|------------------------|--------------------------|-------------------------|----------------------|
| `apps/developers-page` | **`apps/lazuar-developers`** | `lazuar-hub-developers` | `developers` / `hub-developers` | `/docs` | 3002 |
| `apps/ops-page` | **`apps/lazuar-ops`** | `lazuar-hub-ops` | `ops` / `hub-ops` | `/` | 3003 |
| `apps/portal-page` | **`apps/lazuar-portal`** | `lazuar-hub-portal` | `portal` / `hub-portal` | `/portal` | 3004 |
| `apps/superadmin-page` | **`apps/lazuar-admin`** | `lazuar-hub-superadmin` | `superadmin` / `hub-superadmin` | `/admin` | 3005 |

> **Note:** Original idea used `lazuar-spec` for developers. This checklist uses **`lazuar-developers`** to avoid collision with `packages/api-spec` / `@repo/api-spec`. If you later insist on `lazuar-spec`, substitute that string everywhere below and add doc disambiguation work.

---

## Non-goals (do not do in this rename)

- [ ] Do **not** rename GHCR packages (`lazuar-hub-*` stays)
- [ ] Do **not** edit `deploy/prod/docker-compose.yml` image or service names
- [ ] Do **not** edit `deploy/prod/Caddyfile` reverse_proxy targets
- [ ] Do **not** edit `scripts/remote-deploy.sh` health-gate container names (unless they still say `*-page` — they should not)
- [ ] Do **not** change public URL base paths (`/`, `/portal`, `/docs`, `/admin`)
- [ ] Do **not** rename backend modules (`Modules/Ops`, `/api/v1/ops`, `/api/v1/admin`, etc.)
- [ ] Do **not** rename cookies / localStorage product keys (`lazuar_auth`, `lazuar_admin_auth`, sidebar keys)
- [ ] Do **not** rename UI product titles unless they hardcode the old **folder** string
- [ ] Do **not** bulk-replace bare tokens `ops`, `portal`, `developers`, `admin`, `superadmin`
- [ ] Do **not** rewrite every historical ADR / gap doc in the same PR (Phase 5 is optional follow-up)
- [ ] Do **not** phase one app at a time — all four apps in **one PR**

---

## Phase 0 — Align and prepare

**Goal:** One decision set, clean tree, no surprise local junk.

### 0.1 Decisions

- [x] Confirm developers app name: **`lazuar-developers`** (recommended) *or* document exception if using `lazuar-spec`
- [x] Confirm non-goals above with anyone who owns deploy secrets / VPS
- [x] Confirm PR strategy: **single atomic PR** for Phases 1–4; Phase 5+ optional later PRs

### 0.2 Working tree

- [x] Start from a clean git worktree (`git status` empty or only intentional files)
- [x] Create branch, e.g. `chore/rename-frontend-apps-lazuar-prefix`
- [x] Note current Node/pnpm versions match repo (`packageManager` in root `package.json`)
- [x] Optional: stash or delete local build artifacts under the four apps (`.next/`, `dist/`, `tsbuildinfo`) so moves are clean — do **not** commit those

### 0.3 Pre-flight inventory (sanity)

- [x] Re-run a scoped search so nothing new appeared:

  ```bash
  rg -n 'developers-page|ops-page|portal-page|superadmin-page' \
    --glob '!**/node_modules/**' --glob '!**/.next/**' --glob '!**/dist/**' \
    --glob '!**/bin/**' --glob '!**/obj/**' -l
  ```

- [x] Skim [10-master-reference-inventory.md](./10-master-reference-inventory.md) for must-change vs docs-only
- [x] Identify false positives you will **skip** (backend Ops module, product comments that only say “ops”, path headers that are cosmetic)

### 0.4 Phase 0 exit criteria

- [x] Branch ready, names locked, non-goals agreed
- [x] No open PR dependencies that would force merging half-renamed paths

---

## Phase 1 — Move apps + package identity

**Goal:** Folders and pnpm package names match the new convention. Workspace discovery keeps working via `apps/*`.

### 1.1 Directory moves (preserve history)

- [x] `git mv apps/developers-page apps/lazuar-developers`
- [x] `git mv apps/ops-page apps/lazuar-ops`
- [x] `git mv apps/portal-page apps/lazuar-portal`
- [x] `git mv apps/superadmin-page apps/lazuar-admin`
- [x] Verify: `ls apps/` shows `lazuar-api`, `lazuar-docs`, `lazuar-developers`, `lazuar-ops`, `lazuar-portal`, `lazuar-admin` (and no `*-page` frontends)

### 1.2 Package `"name"` fields

Update each app’s `package.json` `"name"` to match the folder:

- [x] `apps/lazuar-developers/package.json` → `"name": "lazuar-developers"`
- [x] `apps/lazuar-ops/package.json` → `"name": "lazuar-ops"`
- [x] `apps/lazuar-portal/package.json` → `"name": "lazuar-portal"`
- [x] `apps/lazuar-admin/package.json` → `"name": "lazuar-admin"`

### 1.3 Do **not** change (unless a real filter breaks)

- [x] Leave `pnpm-workspace.yaml` as `apps/*` / `packages/*` (no edit expected)
- [x] Leave `turbo.json` task graph generic (no app names expected)
- [x] Leave root `package.json` scripts that only filter `lazuar-docs` (no edit expected for these four apps)
- [x] Leave workspace deps on `@repo/api-types-ts` / `@repo/api-spec` as-is (package **names** of shared libs unchanged)

### 1.4 Path-header / source comments (optional in Phase 1, cheap)

Some files under ops/portal/admin only mention old paths in banner comments. Prefer updating when you touch the file, or do a careful replace of the full old path string only:

- [x] Optional: replace `apps/ops-page` → `apps/lazuar-ops` (and siblings) in comment headers only
- [x] Do **not** rename runtime keys, cookies, or UI strings here

### 1.5 Backend comments (optional)

Known comment-only mentions (not runtime):

- [x] Optional: `apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs` if it says “ops-page”
- [x] Optional: `apps/lazuar-api/Modules/One/Infrastructure/Workers/SystemGenesisBootstrapperJob.cs` if it says “ops-page”

### 1.6 Phase 1 exit criteria

- [x] Four new directories exist; old `*-page` frontend dirs gone
- [x] Four package names updated
- [x] No broken `workspace:` reverse deps (apps are leaves — should be fine)

---

## Phase 2 — Docker, bake, compose, CI paths

**Goal:** Every build context and Dockerfile path follows the new folders. **GHCR image names stay `lazuar-hub-*`.**

### 2.1 Dockerfiles (all four apps) — **critical**

For **each** of:

- `apps/lazuar-developers/Dockerfile`
- `apps/lazuar-ops/Dockerfile`
- `apps/lazuar-portal/Dockerfile`
- `apps/lazuar-admin/Dockerfile`

Update every occurrence of the old monorepo path / filter:

- [ ] `COPY apps/<old>/package.json …` → new path
- [ ] `pnpm install --filter ./apps/<old>…` → new path (or package name if used)
- [ ] `COPY apps/<old> apps/<old>` → new path both sides
- [ ] `pnpm --filter ./apps/<old> build` → new path
- [ ] Runtime `COPY … /app/apps/<old>/…` static/public paths → new path
- [ ] **Next only (developers + portal):**  
  - [ ] `CMD ["node", "apps/<old>/server.js"]` → `apps/<new>/server.js`  
  - [ ] Standalone static copy destinations under `./apps/<new>/.next/static` and `./apps/<new>/public`
- [ ] **Do not** change HEALTHCHECK URL paths (`/docs`, `/portal`, etc.)
- [ ] **Do not** change image titles that say `lazuar-hub-*` unless they literally embed `*-page` paths

### 2.2 `docker-bake.hcl`

- [ ] Update comment block that lists app roles if it still says folder-style names (optional clarity)
- [ ] `group "default"` targets: rename bake targets from `*-page` to new basenames **or** keep target aliases — prefer renaming targets to match folders for consistency:
  - [ ] `portal-page` → `lazuar-portal` (or `portal` — pick one scheme; recommend **match folder basename**)
  - [ ] `ops-page` → `lazuar-ops`
  - [ ] `superadmin-page` → `lazuar-admin`
  - [ ] `developers-page` → `lazuar-developers`
- [ ] Each target: `dockerfile = "apps/<new>/Dockerfile"`
- [ ] Keep `tags` as `${REGISTRY}/lazuar-hub-ops:…` etc. (**no image rename**)
- [ ] Keep build `args` / base paths (`NEXT_BASE_PATH`, `VITE_BASE_PATH`, …) unchanged
- [ ] Keep `api` target unchanged

### 2.3 `docker-compose.yml` (local build)

Services today: `ops-page`, `portal-page`, `superadmin-page` (developers often **missing**).

- [ ] Rename service keys to new basenames (e.g. `lazuar-ops`) **or** short names — recommend **align with bake targets / folder basenames** for local DX
- [ ] Update `dockerfile: apps/<new>/Dockerfile` for each
- [ ] Keep `image: ghcr.io/proxeon/lazuar-hub-*:local` unchanged
- [ ] Keep `container_name: lazuar-ops` / `lazuar-portal` / `lazuar-superadmin` unless you have a reason (optional; not required)
- [ ] Keep host ports `3003`/`3004`/`3005` and profiles
- [ ] **Optional hygiene (same PR or follow-up):** add `lazuar-developers` service mirroring prod (port 3002, image `lazuar-hub-developers:local`, dockerfile new path) so local compose matches bake/prod

### 2.4 `docker-compose.ghcr.yml`

- [ ] Same service key + image path discipline as local compose
- [ ] Keep pulling `ghcr.io/proxeon/lazuar-hub-*`
- [ ] Note: developers may also be missing here — optional add as above

### 2.5 `.github/workflows/ghcr.yml`

- [ ] Matrix entries: update **only** `dockerfile:` paths:
  - [ ] `apps/portal-page/Dockerfile` → `apps/lazuar-portal/Dockerfile`
  - [ ] `apps/ops-page/Dockerfile` → `apps/lazuar-ops/Dockerfile`
  - [ ] `apps/superadmin-page/Dockerfile` → `apps/lazuar-admin/Dockerfile`
  - [ ] `apps/developers-page/Dockerfile` → `apps/lazuar-developers/Dockerfile`
- [ ] Keep matrix `name:` values as `lazuar-hub-portal`, `lazuar-hub-ops`, `lazuar-hub-superadmin`, `lazuar-hub-developers`
- [ ] Keep build-args (API URLs, base paths) unchanged
- [ ] Keep path filters as broad `apps/**` (no change required)
- [ ] Keep cache scopes on image names (no change required)
- [ ] Confirm `ci.yml` needs **no** frontend path edits (contracts + dotnet only)

### 2.6 Explicitly leave production deploy alone

- [ ] Confirm no edits to `deploy/prod/docker-compose.yml`
- [ ] Confirm no edits to `deploy/prod/Caddyfile`
- [ ] Confirm no edits to `deploy/prod/env.example` for app folder names
- [ ] Confirm `scripts/remote-deploy.sh` still gates `hub-api`, `hub-ops`, `hub-portal`, `hub-superadmin`, `hub-developers`, `hub-caddy`

### 2.7 Phase 2 exit criteria

- [ ] `rg 'apps/(developers|ops|portal|superadmin)-page'` returns **zero** hits in Docker/CI/compose/bake (excluding historical docs and this plans folder if you keep old names in analyses)
- [ ] Every GHCR matrix dockerfile path resolves on disk
- [ ] Next `CMD` paths match new folders

---

## Phase 3 — Local developer tooling

**Goal:** Day-to-day `task fe` / mprocs / docs filters work after the move.

### 3.1 `mprocs-dev.yaml` (**P0 for local FE**)

- [ ] Process keys: `developers-page` → `lazuar-developers` (etc.)
- [ ] Shell cwd: `cd apps/<new> && pnpm dev` for all four
- [ ] Keep `autostart` flags as today
- [ ] Leave ngrok / tunnel procs alone unless they hardcode old FE paths (today they call Taskfile)

### 3.2 `Taskfile.yml`

- [ ] Grep for `*-page` and old folder paths — update any remaining references
- [ ] Confirm `task fe` still only launches mprocs config (path to yaml unchanged)
- [ ] Confirm Docker-related tasks still call bake/compose correctly after Phase 2 target renames (if Taskfile passes bake target names, update those strings)
- [ ] **Do not** “fix” `tunnel:fe` legacy community-page ports unless you intentionally expand scope (known stale; optional follow-up)

### 3.3 Root / package filters

- [ ] Search for `pnpm --filter developers-page` (and siblings); update to new package names
- [ ] Search for turbo `--filter=ops-page` style usage in scripts/docs; update if any

### 3.4 App-local README / AGENTS / CLAUDE

- [ ] `apps/lazuar-developers/README.md` (and siblings): folder/package name strings
- [ ] `AGENTS.md` / `CLAUDE.md` under developers: only if they mention the folder path (generic Next rules can move as-is with `git mv`)

### 3.5 Phase 3 exit criteria

- [ ] `mprocs-dev.yaml` has no `*-page` paths
- [ ] Living “how to run FE” docs use new package names

---

## Phase 4 — Lockfile, install, living docs (same PR)

**Goal:** Install graph and human entrypoints are honest.

### 4.1 Lockfile regeneration

- [ ] From repo root: remove stale app `node_modules` if needed, then:

  ```bash
  pnpm install
  ```

- [ ] Confirm `pnpm-lock.yaml` importers use `apps/lazuar-ops:` (etc.), not `apps/ops-page:`
- [ ] Commit lockfile with the rename (do **not** hand-edit importer keys)

### 4.2 Living documentation (must update)

**Root**

- [ ] `README.md` — architecture tree, port table, product bullets that say `ops-page` / `portal-page` / `superadmin-page` / developers hub
- [ ] Align domain examples if README still says `ops.lazuar.com` vs current `hub.lazuar.com` paths (only if you’re already editing those lines; don’t expand into full docs rewrite)

**Docs site / contracts**

- [ ] `apps/lazuar-docs/docs/reference/openapi.md` — any `pnpm --filter developers-page` → `lazuar-developers`
- [ ] `apps/lazuar-docs/docs/index.md` — path/name mentions if present
- [ ] `apps/lazuar-docs/docs/guide/how-to-maintain.md` — path/name mentions if present
- [ ] `apps/lazuar-docs/README.md` — path/name mentions if present
- [ ] `docs/contracts/openapi-vs-minimal-api.md` — monorepo path references

**Plans that people still execute**

- [ ] `plans/001-backend/001-backend-solidification-checklist.md` — if it still says `*-page` paths

### 4.3 Glossary blurb (short, living)

- [ ] In root README or `docs/` intro, one sentence:
  - monorepo apps: `lazuar-ops` / `lazuar-portal` / `lazuar-admin` / `lazuar-developers`
  - TypeSpec SSoT remains `packages/api-spec`
  - GHCR remains `lazuar-hub-*`
  - public hub paths unchanged

### 4.4 Phase 4 exit criteria

- [ ] `pnpm install` clean
- [ ] New contributor can find apps by new names in README
- [ ] No living command still uses `--filter developers-page` (etc.)

---

## Phase 5 — Local verification (before merge)

**Goal:** Prove the rename is mechanical-complete without relying on production rename tricks.

### 5.1 Workspace / types

- [ ] `pnpm install` (already done)
- [ ] Optional: `pnpm --filter lazuar-ops lint` (or `tsc --noEmit` script)
- [ ] Optional: `pnpm --filter lazuar-portal lint`
- [ ] Optional: `pnpm --filter lazuar-admin lint`
- [ ] Optional: `pnpm --filter lazuar-developers lint` / build

### 5.2 Local FE orchestration

- [ ] `task fe` (or `mprocs -c mprocs-dev.yaml`) starts all four processes
- [ ] Hit localhost ports:
  - [ ] 3002 developers / docs hub
  - [ ] 3003 ops
  - [ ] 3004 portal
  - [ ] 3005 admin
- [ ] Confirm API still expected on 8080 when using full stack (`task dev` / compose api)

### 5.3 Docker path verification (pick depth by time)

**Minimum**

- [ ] `docker build -f apps/lazuar-portal/Dockerfile .` succeeds **or** bake single target
- [ ] `docker build -f apps/lazuar-developers/Dockerfile .` succeeds (OpenAPI/spec copy paths)
- [ ] Spot-check one Vite app Dockerfile (`lazuar-ops` or `lazuar-admin`)

**Better**

- [ ] `docker buildx bake` default group (or Taskfile docker task if present) for all frontend targets
- [ ] Run portal container and confirm process starts (standalone `server.js` path)
- [ ] Run developers container and confirm `/docs` healthcheck path still valid

### 5.4 Regression grep gate

- [ ] Fail the PR if these remain outside `plans/002-change-name/**` and intentional historical docs:

  ```bash
  rg -n 'apps/(developers|ops|portal|superadmin)-page' \
    --glob '!**/node_modules/**' --glob '!**/.next/**' --glob '!**/dist/**' \
    --glob '!plans/002-change-name/**'
  ```

- [ ] Review remaining bare `developers-page|ops-page|portal-page|superadmin-page` hits:
  - [ ] Must-fix: tooling, Docker, package names, living commands
  - [ ] Allowed to remain until Phase 7: historical ADR/gap snapshots (list them in PR body)

### 5.5 Phase 5 exit criteria

- [ ] Local FE boots
- [ ] Critical Docker builds green
- [ ] Grep gate clean for **functional** paths

---

## Phase 6 — PR, merge, and production observe

**Goal:** Ship as a normal frontend rebuild, not a deploy architecture change.

### 6.1 PR hygiene

- [ ] Title e.g. `chore: rename frontend apps to lazuar-* prefix`
- [ ] PR body includes:
  - [ ] Old → new mapping table
  - [ ] Explicit non-goals (GHCR/prod/Caddy unchanged)
  - [ ] Verification performed
  - [ ] Link to `plans/002-change-name/`
- [ ] Keep PR focused: rename + path coupling + living docs only

### 6.2 CI expectations

- [ ] `ci.yml` contracts + dotnet jobs still pass (should be unaffected)
- [ ] On merge to `main`, `ghcr.yml` builds all five images using **new Dockerfile paths** and **same image names**
- [ ] Deploy job still rsyncs `deploy/prod` unchanged and pulls same GHCR names

### 6.3 Post-merge production check (observe only)

- [ ] Workflow: build matrix all green
- [ ] Deploy: health-gate for `hub-api`, `hub-ops`, `hub-portal`, `hub-superadmin`, `hub-developers`, `hub-caddy`
- [ ] Smoke: `hub.lazuar.com/` , `/portal`, `/docs`, `/admin`, `/api` (or `/health`) as you already smoke in remote-deploy
- [ ] If anything fails: treat as **image build/path bug**, not as “need to rename prod services”

### 6.4 Phase 6 exit criteria

- [ ] Main green, hub healthy, no emergency compose rollback for service renames (there should be none)

---

## Phase 7 — Optional follow-ups (separate PRs)

Do **not** block the rename on these.

### 7.1 Documentation archaeology

- [ ] `docs/001-gaps/**` path renames or a single banner: “frontend apps renamed 2026-…; old `*-page` names are historical”
- [ ] ADRs that are active SOPs (`013`, `017`, etc.): path-refresh or watermark
- [ ] ADRs that are pure history: leave or watermark only
- [ ] Optionally rename gap file `04-developers-page-dx.md` for consistency (purely cosmetic)

### 7.2 Local compose parity

- [ ] Add developers (`lazuar-developers`) to root `docker-compose.yml` and `docker-compose.ghcr.yml` if still missing after Phase 2
- [ ] Document profile `full` includes all four frontends + api + db

### 7.3 GHCR image rebrand (only if product demands)

Separate playbook — **not** part of rename:

- [ ] Decide new image names (e.g. keep `lazuar-hub-*` forever vs shorten)
- [ ] Dual-tag push old+new for N releases
- [ ] Update `deploy/prod/docker-compose.yml` images atomically
- [ ] Update bake tags + ghcr matrix `name:` + any pull docs
- [ ] Retire old tags after cutover window

### 7.4 DX nits

- [ ] Fix stale `tunnel:fe` / community-page leftovers in Taskfile if still wrong
- [ ] Align README domain story with single-host `hub.lazuar.com` path routing if still multi-domain

### 7.5 Naming debt that stays by design

Document and stop worrying:

- [ ] Backend `Modules/Ops` vs app `lazuar-ops`
- [ ] Public `/docs` = developers hub UI; `lazuar-docs` = VitePress product docs on another port
- [ ] GHCR `lazuar-hub-superadmin` vs app `lazuar-admin`
- [ ] Prod containers `hub-*` vs local containers `lazuar-*`

---

## Quick reference — files that **must** change in the rename PR

| File / area | Why |
|-------------|-----|
| `apps/{developers,ops,portal,superadmin}-page/` → new dirs | Identity |
| Each app `package.json` `"name"` | pnpm package id |
| Each app `Dockerfile` | COPY/filter/CMD paths |
| `docker-bake.hcl` | targets + dockerfile paths |
| `docker-compose.yml` | dockerfile + service keys |
| `docker-compose.ghcr.yml` | service keys (images stay) |
| `mprocs-dev.yaml` | local FE entrypoint |
| `.github/workflows/ghcr.yml` | matrix dockerfile paths |
| `pnpm-lock.yaml` | regenerated importers |
| Root `README.md` | living structure/ports |
| `apps/lazuar-docs/**` living filter commands | DX honesty |
| `docs/contracts/openapi-vs-minimal-api.md` | living contract doc |

## Quick reference — files that **must not** change for success

| File / area | Why leave alone |
|-------------|-----------------|
| `deploy/prod/docker-compose.yml` | already short services + `lazuar-hub-*` images |
| `deploy/prod/Caddyfile` | service DNS `ops`/`portal`/… |
| `scripts/remote-deploy.sh` | `hub-*` health gates |
| GHCR image **names** in bake tags / matrix `name:` | avoid prod pull break |
| Public base paths / Vite-Next env URL values | product routing |
| Backend module folders and API routes | different domain |

---

## Suggested PR commit shape (optional)

Prefer one commit or a short stack on the same branch:

1. `git mv` four apps + package.json names  
2. Docker / bake / compose / ghcr paths  
3. mprocs + Taskfile + living docs  
4. `pnpm install` lockfile  

Or a single squash commit on merge — either is fine if the branch is atomic.

---

## Definition of done (rename project)

- [ ] No frontend app folder uses `*-page`
- [ ] Package names are `lazuar-{developers,ops,portal,admin}`
- [ ] Local mprocs + README teach the new names
- [ ] CI builds images from new Dockerfile paths under the **same** GHCR names
- [ ] Production deploy path unchanged and healthy after merge
- [ ] Historical docs may still say `*-page`, but living commands do not
- [ ] Optional GHCR rebrand and docs archaeology tracked separately (Phase 7), not as incomplete rename debt

---

## If something goes wrong

| Symptom | Likely cause | Fix direction |
|---------|--------------|---------------|
| `cd: no such file` in mprocs | Phase 3 incomplete | Fix `mprocs-dev.yaml` |
| Docker build “file not found” COPY | Dockerfile still old path | Phase 2.1 |
| Container exits immediately (Next) | `CMD` still `apps/*-page/server.js` | Phase 2.1 Next CMD |
| GHCR job can’t find Dockerfile | `ghcr.yml` matrix path stale | Phase 2.5 |
| pnpm filter not found | package `"name"` or lockfile stale | Phase 1.2 + 4.1 |
| Prod 502 after deploy | Usually image crashloop (CMD) or unrelated API — **not** missing GHCR rename | Check container logs; do not rename Caddy services reactively |
| Prod pull “manifest unknown” after image rebrand | Accidental GHCR rename without dual-tag | Revert image names; dual-tag playbook only in Phase 7.3 |

---

## Order summary

| Phase | Name | Required for rename? |
|-------|------|----------------------|
| 0 | Align & prepare | Yes |
| 1 | Move apps + package names | Yes |
| 2 | Docker / bake / compose / CI paths | Yes |
| 3 | mprocs / Taskfile / filters | Yes |
| 4 | Lockfile + living docs | Yes |
| 5 | Local / Docker verification | Yes before merge |
| 6 | PR + merge + observe prod | Yes |
| 7 | Docs archaeology, compose parity, GHCR rebrand | Optional later |
