# Phase 3 — Analysis & implement brief (mprocs / Taskfile / root tooling filters)

**Status:** Analysis only — **do not implement in this file’s authoring step**; implementers follow §3–§8.  
**Date:** 2026-08-09  
**Branch:** `chore/rename-frontend-apps-lazuar-prefix`  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Related:** [`phase-2-done.md`](./phase-2-done.md), [`04-taskfile-mprocs-scripts.md`](./04-taskfile-mprocs-scripts.md), [`11-implementation-checklist.md`](./11-implementation-checklist.md) § Phase 3

---

## 1. Phase 3 goal

After Phase 1 (folder + package `name`) and Phase 2 (Docker / bake / compose / CI), **local multi-frontend dev is still broken**: `task fe` → mprocs still `cd`s into deleted `apps/*-page` directories.

| # | In scope | Out of scope (later / never this phase) |
|---|----------|----------------------------------------|
| 1 | `mprocs-dev.yaml` process keys + shell cwd paths | Living docs (`README.md`, `apps/lazuar-docs/**`) — **Phase 4** |
| 2 | `Taskfile.yml` leftover `*-page` / bake-target strings (if any) | `pnpm-lock.yaml` importer regeneration — **Phase 4** |
| 3 | Root scripts / `package.json` filters naming old packages | `deploy/prod/**`, GHCR image renames, public URL paths |
| 4 | Confirm turbo / workspace globs need no edits | `tunnel:fe` community-page port cleanup (optional follow-up) |

**Locked identity (from Phase 1 done):**

| Folder (exists now) | package.json `"name"` | Old folder (gone) | Local port |
|---------------------|----------------------|-------------------|------------|
| `apps/lazuar-developers` | `lazuar-developers` | `apps/developers-page` | **3002** |
| `apps/lazuar-ops` | `lazuar-ops` | `apps/ops-page` | **3003** |
| `apps/lazuar-portal` | `lazuar-portal` | `apps/portal-page` | **3004** |
| `apps/lazuar-admin` | `lazuar-admin` | `apps/superadmin-page` | **3005** |

Do **not** use draft name `lazuar-spec` for developers.

---

## 2. Current state (post Phase 2, pre Phase 3)

### 2.1 What already works (no Phase 3 edit required)

| Artifact | Status | Evidence |
|----------|--------|----------|
| App folders | Renamed | `ls apps/` → `lazuar-{admin,api,developers,docs,ops,portal}` |
| Per-app `"name"` | Renamed | Each `apps/lazuar-*/package.json` already `lazuar-*` |
| Docker / bake / compose / CI | Phase 2 done | Bake targets `lazuar-{portal,ops,admin,developers}`; Dockerfiles path-correct |
| Root `package.json` scripts | Clean | Only `pnpm --filter lazuar-docs …`; turbo tasks; **no** `*-page` filters |
| `Taskfile.yml` FE path literals | Clean | **Zero** matches for `developers-page\|ops-page\|portal-page\|superadmin-page` |
| `Taskfile.yml` filters | Clean | `pnpm --filter lazuar-api`, `@repo/api-types-ts`, `@repo/api-types-dotnet` only |
| `turbo.json` | Clean | No package/app names |
| `pnpm-workspace.yaml` | Clean | Globs `apps/*`, `packages/*` only |
| `scripts/**` | Clean | No FE folder / package filter strings |
| `script/second-app-proof.md` | Clean | API-only harness |
| App-local `README` / `AGENTS` / `CLAUDE` under the four apps | Clean (no `*-page` hits in `*.md`/`*.json`) | Checklist §3.4 effectively already satisfied |

### 2.2 What is still broken

| File | Forced by folder rename? | Failure mode if unfixed |
|------|--------------------------|-------------------------|
| **`mprocs-dev.yaml`** | **YES — P0** | Every FE process: `cd: apps/*-page: No such file or directory` → empty ports 3002–3005 |
| `Taskfile.yml` | No path break | `task fe` still launches mprocs; fails **via** mprocs only. Optional echo polish only. |
| Root `package.json` | No | Already correct |
| Root scripts | No | None reference old filters |
| `pnpm-lock.yaml` importers | Stale keys remain | **Phase 4** — still lists `apps/developers-page`, `apps/ops-page`, etc. (not Phase 3) |
| Living docs filter cmds | Stale | **Phase 4** — e.g. `apps/lazuar-docs/docs/reference/openapi.md` still has `pnpm --filter developers-page dev` |

### 2.3 Severity matrix

| Area | Severity | Phase 3 action |
|------|----------|----------------|
| `mprocs-dev.yaml` | **P0** | **Must** rewrite keys + `cd` paths |
| Taskfile `fe` | P0 *via dependency* | No Taskfile string change required once mprocs fixed |
| Taskfile docker tasks | None (post Phase 2) | Bake default group already new names; Taskfile does not pass target names |
| Taskfile `docker:build` echo | P3 cosmetic | Optional string polish |
| Taskfile `tunnel:fe` | Pre-existing drift | **Do not** expand scope (checklist) |
| Root package filters | **None** | No edits |
| Root scripts | **None** | No edits |
| Living docs / lockfile | Deferred | Phase 4 |

---

## 3. Exact edits by file

### 3.1 `mprocs-dev.yaml` — **MUST** (only blocking edit)

**Absolute path:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/mprocs-dev.yaml`

**Full current contents:**

```yaml
# mprocs-dev.yaml
procs:
  developers-page:
    shell: cd apps/developers-page && pnpm dev
    autostart: true
  ops-page:
    shell: cd apps/ops-page && pnpm dev
    autostart: true
  superadmin-page:
    shell: cd apps/superadmin-page && pnpm dev
    autostart: true
  portal-page:
    shell: cd apps/portal-page && pnpm dev
    autostart: true
  ngrok-api-tunnel:
    shell: task tunnel:api
    autostart: false
  ngrok-fe-tunnel:
    shell: task tunnel:fe
    autostart: false
```

#### 3.1.1 Line-level replacement map

| Lines (current) | Old | New |
|-----------------|-----|-----|
| 3–4 | process key `developers-page:` + `cd apps/developers-page` | `lazuar-developers:` + `cd apps/lazuar-developers` |
| 6–7 | `ops-page:` + `cd apps/ops-page` | `lazuar-ops:` + `cd apps/lazuar-ops` |
| 9–10 | `superadmin-page:` + `cd apps/superadmin-page` | `lazuar-admin:` + `cd apps/lazuar-admin` |
| 12–13 | `portal-page:` + `cd apps/portal-page` | `lazuar-portal:` + `cd apps/lazuar-portal` |
| 16–20 | `ngrok-api-tunnel` / `ngrok-fe-tunnel` | **KEEP unchanged** |

#### 3.1.2 Exact file after edit (copy-paste target)

```yaml
# mprocs-dev.yaml
procs:
  lazuar-developers:
    shell: cd apps/lazuar-developers && pnpm dev
    autostart: true
  lazuar-ops:
    shell: cd apps/lazuar-ops && pnpm dev
    autostart: true
  lazuar-admin:
    shell: cd apps/lazuar-admin && pnpm dev
    autostart: true
  lazuar-portal:
    shell: cd apps/lazuar-portal && pnpm dev
    autostart: true
  ngrok-api-tunnel:
    shell: task tunnel:api
    autostart: false
  ngrok-fe-tunnel:
    shell: task tunnel:fe
    autostart: false
```

#### 3.1.3 Design notes (implementer)

| Decision | Choice | Reason |
|----------|--------|--------|
| Process keys | Match package/folder names (`lazuar-*`) | mprocs TUI labels stay honest; matches checklist §3.1 |
| Shell style | Keep `cd apps/<name> && pnpm dev` | Matches pre-rename design; does **not** depend on filter-by-name |
| Optional alternative | `pnpm --filter lazuar-ops dev` from repo root | Also valid now that package `name`s are updated; **not required** |
| Port / `dev` scripts | Do **not** change | Ports stay in each app `package.json` (3002–3005) |
| Process order | Developers → ops → admin → portal (same relative order as today) | Cosmetic only |
| Autostart | Keep four FE `true`, tunnels `false` | Unchanged |
| Ngrok procs | Leave alone | They shell into Taskfile; no FE folder paths |

#### 3.1.4 Token replace summary

| Old token | New token | Occurrences in this file |
|-----------|-----------|--------------------------|
| `developers-page` | `lazuar-developers` | 2 (key + path) |
| `ops-page` | `lazuar-ops` | 2 |
| `superadmin-page` | `lazuar-admin` | 2 |
| `portal-page` | `lazuar-portal` | 2 |

Safe global replace **inside this file only** of those four tokens works.

---

### 3.2 `Taskfile.yml` — mostly no-op; optional polish

**Absolute path:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/Taskfile.yml`

#### 3.2.1 Grep proof (live tree, 2026-08-09)

```bash
rg -n 'developers-page|ops-page|portal-page|superadmin-page' Taskfile.yml
# → no matches
```

Only adjacent stale string (not one of the four apps):

| Line | Content | Phase 3 action |
|------|---------|----------------|
| 142 | `desc: Start ngrok tunnel for Next.js community-page on port 3020` | **KEEP** (checklist: do not expand into `tunnel:fe` cleanup) |
| 144 | `ngrok http 3020` | **KEEP** |

#### 3.2.2 Tasks vs FE rename

| Task | Touches four apps? | Hardcoded old names? | Phase 3 edit |
|------|--------------------|----------------------|--------------|
| `fe` (L41–44) | Via `mprocs -c mprocs-dev.yaml` | **No** names in Taskfile | **None** — fix mprocs only |
| `dev` | `pnpm --filter lazuar-api dev` | Already correct | **None** |
| `docs` / `docs:build` | `lazuar-docs` via root scripts | N/A | **None** |
| `docker:build` / `docker:push` | `docker buildx bake` **without** naming FE targets | Relies on bake `group "default"` (Phase 2 already lists `lazuar-*`) | **None** for correctness |
| `docker:build:api` / `docker:push:api` | Target `api` only | N/A | **None** |
| `docker:up:full` | compose `--profile full` | Service keys fixed in Phase 2 | **None** |
| `docker:up:ghcr` | compose.ghcr | Phase 2 | **None** |
| `gen*` / `api:*` / `infra:*` / `tunnel:api` / `tunnel:stop` / `tunnel:status` | No FE folders | N/A | **None** |

**Critical confirmation:** Taskfile does **not** pass bake target names such as `portal-page` on the CLI. Example:

```yaml
docker buildx bake --load \
  --builder lazuar-builder \
  REGISTRY="$REGISTRY" TAG="$TAG" PLATFORMS="$PLATFORMS"
```

After Phase 2, default group is:

```hcl
targets = ["api", "lazuar-portal", "lazuar-ops", "lazuar-admin", "lazuar-developers"]
```

So **no Taskfile string is required** for docker tasks to pick new bake targets.

#### 3.2.3 Optional polish only (not blocking)

**A. `docker:build` echo (L217)**

| Current | Suggested |
|---------|-----------|
| `echo "… (api, portal, ops, superadmin)"` | `echo "… (api, lazuar-portal, lazuar-ops, lazuar-admin, lazuar-developers)"` |

Or shorter: `(api + 4 frontends)`. Omits developers today (pre-existing incomplete echo). **Optional.**

**B. `fe` desc (L42)**

| Current | Suggested |
|---------|-----------|
| `Full dev stack (Docker infra + mprocs for frontends and tunnels)` | Keep, or clarify: `mprocs frontends (lazuar-*) + optional tunnels` |

Note: desc claims “Docker infra” but task only runs mprocs — pre-existing inaccuracy; **do not** expand scope to rewrite workflow docs here.

**C. Explicitly out of scope**

- Do **not** retarget `tunnel:fe` (3020 / community-page) in Phase 3.
- Do **not** add new Taskfile tasks like `fe:ops` unless requested later.

---

### 3.3 Root `package.json` — **no edits**

**Absolute path:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/package.json`

**Current scripts (live):**

```json
"scripts": {
  "build": "turbo run build",
  "dev": "turbo run dev",
  "lint": "turbo run lint",
  "test": "turbo run test",
  "format": "prettier --write \"**/*.{ts,tsx,md}\"",
  "check-types": "turbo run check-types",
  "docs:dev": "pnpm --filter lazuar-docs dev",
  "docs:build": "pnpm --filter lazuar-docs build",
  "docs:preview": "pnpm --filter lazuar-docs preview"
}
```

| Script | Filter / mechanism | Touches four FE apps? | Edit? |
|--------|--------------------|----------------------|-------|
| `build` / `dev` / `lint` / `test` / `check-types` | turbo discovery via workspace | Yes (by glob), **no name hardcoding** | **No** |
| `docs:*` | `--filter lazuar-docs` | No | **No** |

```bash
rg -n 'developers-page|ops-page|portal-page|superadmin-page' package.json
# → no matches
```

**Phase 3 conclusion:** root package filters already aligned. No optional scripts required.

---

### 3.4 Root scripts / other tooling filters — **no edits**

| Path | Role | Old FE names? | Edit? |
|------|------|---------------|-------|
| `scripts/lhdn_sandbox/*` | API sandbox curls | **No** | **No** |
| `scripts/remote-deploy.sh` | Prod container health (`hub-ops` etc.) | **No** monorepo folder names | **No** (prod layer; keep) |
| `script/second-app-proof.md` | API proof harness | **No** | **No** |
| `pnpm-workspace.yaml` | `apps/*` globs | **No** explicit names | **No** |
| `turbo.json` | Pipeline only | **No** | **No** |
| Makefile / Justfile / `.vscode/tasks` | — | **Absent** | — |

```bash
rg -n 'developers-page|ops-page|portal-page|superadmin-page' scripts/ script/
# → no matches (tooling surfaces)
```

---

### 3.5 Adjacent findings (document only — **not** Phase 3 implement)

| Finding | Owner phase | Detail |
|---------|-------------|--------|
| `pnpm-lock.yaml` importers still `apps/developers-page`, `apps/ops-page`, `apps/portal-page`, `apps/superadmin-page` | **Phase 4** | Run `pnpm install` at root; do **not** hand-edit keys |
| `apps/lazuar-docs/docs/reference/openapi.md` → `pnpm --filter developers-page dev` | **Phase 4** | Must become `--filter lazuar-developers` |
| Root `README.md` still documents `ops-page` / `portal-page` / `superadmin-page` | **Phase 4** | Port table + structure tree |
| `tunnel:fe` → port 3020 / “community-page” | Optional later | Pre-existing; not one of the four renamed apps |

---

## 4. Checklist §3 mapping (implementer todo)

Copy into commit notes / PR; tick when done.

### 4.1 `mprocs-dev.yaml` (**P0**)

- [ ] Process keys: `lazuar-developers`, `lazuar-ops`, `lazuar-admin`, `lazuar-portal`
- [ ] Shells: `cd apps/<new> && pnpm dev` for all four
- [ ] Keep `autostart: true` on FE, `false` on tunnels
- [ ] Leave ngrok procs as-is

### 4.2 `Taskfile.yml`

- [x] Grep for `*-page` four-app names — **already clean** (confirm again after edits)
- [x] `task fe` only launches mprocs yaml — path to yaml unchanged
- [x] Docker tasks do not pass old bake target names — bake group already Phase 2
- [ ] Optional: polish `docker:build` echo text
- [ ] Do **not** “fix” `tunnel:fe` in this phase

### 4.3 Root / package filters

- [x] No root `pnpm --filter *-page` to update
- [x] No turbo `--filter=ops-page` style usage in root tooling
- [x] App package names already `lazuar-*` (Phase 1)

### 4.4 App-local README / AGENTS / CLAUDE

- [x] No `*-page` hits under the four app trees in living `*.md` / package metadata (spot-check complete)

### 4.5 Phase 3 exit criteria

- [ ] `rg 'developers-page|ops-page|portal-page|superadmin-page' mprocs-dev.yaml` → **zero**
- [ ] `rg 'apps/(developers|ops|portal|superadmin)-page' mprocs-dev.yaml Taskfile.yml package.json` → **zero**
- [ ] `task fe` (or `mprocs -c mprocs-dev.yaml`) starts four green processes with keys `lazuar-*`
- [ ] Local URLs still: 3002 developers, 3003 ops, 3004 portal, 3005 admin

---

## 5. Implementation order (minimal PR slice)

1. **Only required change:** rewrite `mprocs-dev.yaml` as in §3.1.2.
2. **Optional:** one-line echo polish in `Taskfile.yml` `docker:build` (§3.2.3 A).
3. **Verify** (§6) — do **not** touch lockfile or living docs in this phase unless folding Phase 4 into the same PR deliberately.

**Suggested commit message:**

```text
chore(tooling): point mprocs at lazuar-* frontend apps
```

---

## 6. Verification commands (post-implement)

```bash
# 1. No stale mprocs paths
rg -n 'developers-page|ops-page|portal-page|superadmin-page' mprocs-dev.yaml
# expect: no matches

# 2. New paths present
rg -n 'lazuar-(developers|ops|admin|portal)' mprocs-dev.yaml
# expect: keys + cd paths

# 3. Folders exist
test -d apps/lazuar-developers && test -d apps/lazuar-ops \
  && test -d apps/lazuar-portal && test -d apps/lazuar-admin \
  && echo "dirs OK"

# 4. Package names resolve by filter (sanity; names already Phase 1)
pnpm --filter lazuar-developers exec node -p "require('./package.json').name"
pnpm --filter lazuar-ops exec node -p "require('./package.json').name"
pnpm --filter lazuar-portal exec node -p "require('./package.json').name"
pnpm --filter lazuar-admin exec node -p "require('./package.json').name"
# expect: each prints its own name

# 5. mprocs launch (interactive) — or non-interactive smoke:
#    start task fe; confirm four processes autostart and bind:
#    3002, 3003, 3004, 3005
task fe
```

**Do not require for Phase 3 exit:**

- `pnpm install` / lockfile rewrite (Phase 4)
- README / openapi.md filter updates (Phase 4)
- Docker rebuilds (Phase 2 already proven independently)

---

## 7. Breakage matrix — “Phase 3 not done”

| Action | Result today (pre Phase 3) |
|--------|----------------------------|
| `task fe` | mprocs starts; all four FE shells fail on `cd apps/*-page` |
| `task dev` | API still works |
| `task docker:build` | Should work post Phase 2 (bake paths fixed) |
| `pnpm --filter lazuar-ops dev` | Works (package name already new) |
| `pnpm --filter ops-page dev` | Fails (name gone since Phase 1) |
| `pnpm dev` (turbo) | Discovers packages under new folders; alternate path to mprocs |
| Living docs `pnpm --filter developers-page` | Still stale until Phase 4 |

---

## 8. Suggested final local workflow after Phase 3

```bash
# Terminal 1 — infrastructure + API
task infra:up
task dev

# Terminal 2 — all frontends (mprocs)
task fe
# expect processes: lazuar-developers, lazuar-ops, lazuar-admin, lazuar-portal
# URLs:
#   http://localhost:3002  lazuar-developers
#   http://localhost:3003  lazuar-ops
#   http://localhost:3004  lazuar-portal
#   http://localhost:3005  lazuar-admin

# Optional single-app (name filters already valid post Phase 1)
pnpm --filter lazuar-ops dev
pnpm --filter lazuar-developers dev
```

---

## 9. Evidence index (absolute paths)

| Artifact | Absolute path | Phase 3 edit? |
|----------|---------------|---------------|
| mprocs | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/mprocs-dev.yaml` | **YES — must** |
| Taskfile | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/Taskfile.yml` | Optional echo only |
| Root package.json | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/package.json` | No |
| turbo.json | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/turbo.json` | No |
| pnpm-workspace | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/pnpm-workspace.yaml` | No |
| scripts/ | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/scripts/` | No |
| docker-bake (already Phase 2) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docker-bake.hcl` | No (confirm only) |
| lockfile (Phase 4) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/pnpm-lock.yaml` | No |
| openapi filter doc (Phase 4) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/reference/openapi.md` | No |

---

## 10. One-line severity summary

| Area | Severity if Phase 3 skipped | Edit required? |
|------|----------------------------|----------------|
| **`mprocs-dev.yaml`** | **P0** — local multi-FE dev dead | **Yes** |
| **`Taskfile.yml` FE path strings** | None remaining | **No** |
| **`Taskfile.yml` docker bake target names** | None (never hardcoded; Phase 2 fixed bake) | **No** |
| **Root `package.json` filters** | None | **No** |
| **Root `scripts/`** | None | **No** |
| **turbo / workspace globs** | None | **No** |
| **Living docs / lockfile** | Deferred Phase 4 | **No this phase** |

---

*End of Phase 3 analysis. No application or tooling files were modified by this document’s authoring step.*
