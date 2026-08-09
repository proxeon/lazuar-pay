# 09 — Git, Tooling & Operational Risks of App Folder Renames

**Scope:** Operational / git / tooling risks of renaming the four frontend app directories and related package identifiers. This document does **not** prescribe application feature changes; it analyzes rename mechanics and residual risk.

**Proposed renames:**

| Current folder (`apps/…`) | Current `package.json` `"name"` | Proposed folder | Proposed package name (recommended alignment) |
|---------------------------|---------------------------------|-----------------|-----------------------------------------------|
| `developers-page` | `developers-page` | `lazuar-spec` | `lazuar-spec` |
| `ops-page` | `ops-page` | `lazuar-ops` | `lazuar-ops` |
| `portal-page` | `portal-page` | `lazuar-portal` | `lazuar-portal` |
| `superadmin-page` | `superadmin-page` | `lazuar-admin` | `lazuar-admin` |

**Evidence base (repo state at analysis time):** monorepo root `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`; pnpm workspace `apps/*` + `packages/*`; Turbo 2.x; Taskfile + mprocs; Docker Bake + GitHub Actions GHCR pipeline; production compose under `deploy/prod/` with GHCR image names, not folder names.

---

## 1. Executive summary

The four renames are **directory + package-name renames**, not domain renames. Runtime production services already use shorter logical names (`ops`, `portal`, `superadmin`, `developers` as compose service names; `lazuar-hub-ops` etc. as GHCR images; `hub-ops` etc. as container names). That is good: **production image tags and Caddy upstream service names do not need to change solely because folders rename**.

The high-risk surface is entirely **local monorepo + CI build path coupling**:

1. **Git history** for each app tree (must use `git mv`, clean working tree, avoid copy/delete).
2. **pnpm lockfile importer paths** (`apps/developers-page` → `apps/lazuar-spec`, etc.) and package `name` fields used by `pnpm --filter`.
3. **Dockerfiles** that hardcode `apps/<old-name>/…` for COPY, filter, and especially Next.js standalone `CMD ["node", "apps/<old-name>/server.js"]`.
4. **CI matrix** (`.github/workflows/ghcr.yml`) dockerfile paths.
5. **docker-bake.hcl** target names + dockerfile paths.
6. **Local docker-compose.yml / docker-compose.ghcr.yml** service names and dockerfile paths (local/dev-oriented; prod uses different names).
7. **mprocs-dev.yaml** (`cd apps/<old-name> && pnpm dev`).
8. **Stale local artifacts** (`node_modules`, `.next`, `dist`, `tsconfig.tsbuildinfo`, `.turbo`) that will **not** move correctly with a rename and will poison the next build if left behind under old paths or half-linked under new paths.

**Tests do not assert folder names.** There is **no** checked-in VS Code / Cursor `launch.json`. `AGENTS.md` / `CLAUDE.md` exist only under `developers-page` and contain Next.js agent boilerplate (no path names). `ctx.include` currently lists a single C# test path and does not reference the four apps.

**Recommendation (see §12):** **Big-bang (single coordinated PR)** for all four app renames plus package names + tooling path updates, with a mandatory local artifact purge and lockfile regeneration. Phasing is technically possible app-by-app but leaves the monorepo in a longer half-renamed state and multiplies PR/review/deploy risk with almost no operational benefit, because shared files (lockfile, mprocs, bake group, CI matrix, root README) must be touched either way.

---

## 2. Inventory of path-coupled surfaces

### 2.1 Package identity (runtime tooling)

| App folder | `package.json` `"name"` | Dev port (scripts) | Stack |
|------------|-------------------------|--------------------|--------|
| `apps/developers-page` | `developers-page` | 3002 (`next dev`) | Next 16 standalone |
| `apps/ops-page` | `ops-page` | 3003 (`vite`) | Vite SPA |
| `apps/portal-page` | `portal-page` | 3004 (`next dev`) | Next 16 standalone |
| `apps/superadmin-page` | `superadmin-page` | 3005 (`vite`) | Vite SPA |

**Risk:** After a folder rename, if `"name"` is **not** updated, `pnpm --filter developers-page` still works by package name but human/docs/scripts that use path filters (`--filter ./apps/developers-page`) break. If `"name"` **is** updated without updating all `--filter` call sites, the inverse happens. **Best practice: rename folder and package name together, and update every filter/path in the same commit.**

Workspace discovery itself is low risk: `pnpm-workspace.yaml` uses globs:

```yaml
packages:
  - "apps/*"
  - "packages/*"
```

New folder names under `apps/` are picked up automatically. There is **no** explicit allowlist of the four old names in the workspace file.

### 2.2 pnpm lockfile (`pnpm-lock.yaml`)

Root lockfile importers are **path-keyed**:

- `apps/developers-page:`
- `apps/ops-page:`
- `apps/portal-page:`
- `apps/superadmin-page:`
- (also `apps/lazuar-api`, `apps/lazuar-docs`, packages, root)

**Risk:** Hand-editing four importer keys is error-prone. Correct procedure is:

1. `git mv` folders.
2. Update each app’s `package.json` `"name"`.
3. Run `pnpm install` at repo root so the lockfile rewrites importer paths and dependency graphs.
4. Commit the regenerated lockfile with the rename.

**Do not** attempt to surgically search-replace lockfile importer paths without `pnpm install`; the lockfile also embeds path-relative workspace references elsewhere and content hashes that assume a consistent graph.

**Nested lockfiles observed on disk:**

- `apps/developers-page/pnpm-lock.yaml` (local tree listing)
- `apps/portal-page/pnpm-lock.yaml` (local tree listing)

These are **not** part of the monorepo’s intended single-root lockfile workflow (Docker and CI copy **root** `pnpm-lock.yaml`). If nested lockfiles are gitignored or untracked, they are still operational landmines: a developer who `cd`s into the app and runs `pnpm install` can create a parallel dependency tree. After rename, delete any nested lockfile under the new app folders and reinstall from root only.

### 2.3 Dockerfiles (critical path coupling)

Each frontend Dockerfile hardcodes the old folder path in multiple places.

#### Next apps — path appears in **runtime CMD** (highest severity)

`apps/developers-page/Dockerfile`:

- `COPY apps/developers-page/package.json apps/developers-page/`
- `pnpm install --filter ./apps/developers-page...`
- `COPY apps/developers-page apps/developers-page`
- `pnpm --filter ./apps/developers-page build`
- Standalone layout copies:
  - `.next/standalone` → `/app`
  - `.next/static` → `./apps/developers-page/.next/static`
  - `public` → `./apps/developers-page/public`
- **`CMD ["node", "apps/developers-page/server.js"]`**

`apps/portal-page/Dockerfile` mirrors the same pattern with `portal-page` and:

- **`CMD ["node", "apps/portal-page/server.js"]`**

Next.js `output: "standalone"` embeds the monorepo-relative app path into the standalone output tree. **Renaming the folder without updating the Dockerfile’s static/public copy destinations and CMD will produce an image that builds (or fails mid-build) and then fails at container start with “cannot find module” / missing server.js.** This is the single most important Docker risk of the rename.

#### Vite apps — path appears in build only (medium severity)

`apps/ops-page/Dockerfile` / `apps/superadmin-page/Dockerfile`:

- COPY package.json into `apps/<name>/`
- `pnpm install --filter ./apps/<name>...`
- COPY source, `pnpm --filter ./apps/<name> build`
- Runtime only needs `dist/` (served by `serve`), so the **runtime** layer is path-agnostic once `dist` is copied. Build stage still must match the new folder.

### 2.4 docker-bake.hcl

```hcl
group "default" {
  targets = ["api", "portal-page", "ops-page", "superadmin-page", "developers-page"]
}
```

Each target:

- `dockerfile = "apps/<old-name>/Dockerfile"`
- target **name** is the old app folder name (`portal-page`, `ops-page`, …)
- **image tags** are already product-oriented: `lazuar-hub-portal`, `lazuar-hub-ops`, `lazuar-hub-superadmin`, `lazuar-hub-developers` — **not** folder names

**Operational choice:**

| Option | Pros | Cons |
|--------|------|------|
| A. Rename bake **targets** to match new folders (`lazuar-portal`, …) | Consistency with monorepo naming | Breaks muscle memory / scripts that `docker buildx bake ops-page` |
| B. Keep bake **target** names as-is, only change `dockerfile` path | Fewer script breakages | Target name ≠ folder name (cognitive mismatch) |
| C. Alias both (HCL doesn’t give free aliases; need duplicate targets or docs) | — | Noise |

**Recommendation for bake:** rename targets to the new app names in the same PR as the folder rename (Option A). Image tags (`lazuar-hub-*`) stay stable so GHCR consumers and `deploy/prod` are unaffected.

### 2.5 docker-compose.yml (local / full profile)

Services named `ops-page`, `portal-page`, `superadmin-page` with:

- `dockerfile: apps/<old-name>/Dockerfile`
- Image tags already `lazuar-hub-ops:local` etc.
- Container names already `lazuar-ops`, `lazuar-portal`, `lazuar-superadmin` (note: already “lazuar-*” style)

**Note:** Local compose currently **does not** include `developers-page` in the snippet reviewed (profile `full` has ops/portal/superadmin). Bake and CI still build developers. Rename of ops/portal/superadmin compose keys is optional for Docker network DNS; if anything `depends_on` or external docs refer to service name `ops-page`, update together. Caddy in **local** compose is not path-routing the same way as prod.

### 2.6 docker-compose.ghcr.yml

Service keys: `ops-page`, `portal-page`, `superadmin-page` — **image** references only (`ghcr.io/proxeon/lazuar-hub-ops`, …). No dockerfile paths. Renaming compose **service** keys is optional; images stay the same. Prefer renaming service keys to match new product names for consistency (`lazuar-ops` or keep short `ops` like prod).

### 2.7 Production deploy (`deploy/prod/`)

| File | Coupled to folder rename? |
|------|---------------------------|
| `deploy/prod/docker-compose.yml` | **No** — services `ops`, `portal`, `superadmin`, `developers`; images `lazuar-hub-*` |
| `deploy/prod/Caddyfile` | **No** — reverse_proxy to `ops:3000`, `portal:3000`, `developers:3000`, `superadmin:3000` |
| `scripts/remote-deploy.sh` | **No** — health waits on `hub-api`, `hub-ops`, `hub-portal`, `hub-superadmin`, `hub-developers`, `hub-caddy` |
| GHCR image names | **No** (unless product wants image renames separately — out of scope for folder rename) |

**Implication:** A pure monorepo folder rename can ship without a production compose/Caddy change. Production risk is **indirect**: CI builds new Dockerfiles from new paths; if Dockerfile CMD is wrong, deploy pulls a broken image and health-gates fail (`hub-portal`, `hub-developers` especially).

### 2.8 GitHub Actions (`.github/workflows/ghcr.yml`)

Matrix hardcodes:

```yaml
dockerfile: apps/portal-page/Dockerfile
dockerfile: apps/ops-page/Dockerfile
dockerfile: apps/superadmin-page/Dockerfile
dockerfile: apps/developers-page/Dockerfile
```

Image **names** in the matrix are already `lazuar-hub-portal`, `lazuar-hub-ops`, etc. (stable).

GHA cache scopes use matrix name (`scope=${{ matrix.name }}` → `lazuar-hub-ops`, …), **not** folder names — **no cache invalidation required for scope keys** when folders rename. Build context path changes will still produce different layer hashes; first post-rename builds will be colder (expected).

Path filters on `push` include `apps/**` — still valid after rename.

### 2.9 mprocs-dev.yaml (local multi-process dev)

```yaml
procs:
  developers-page:
    shell: cd apps/developers-page && pnpm dev
  ops-page:
    shell: cd apps/ops-page && pnpm dev
  superadmin-page:
    shell: cd apps/superadmin-page && pnpm dev
  portal-page:
    shell: cd apps/portal-page && pnpm dev
```

**Risk:** After `git mv` without updating this file, `task fe` starts four failing shells. Process **keys** (left-hand names) should be renamed for UX consistency (`lazuar-spec`, …) in the same change.

### 2.10 Taskfile.yml

Reviewed tasks use:

- `pnpm --filter lazuar-api` (unaffected)
- `pnpm docs:dev` / docs (unaffected)
- `mprocs -c mprocs-dev.yaml` (indirect coupling via mprocs)
- Docker bake targets by default group (indirect; bake target names if scripts ever pass them)

No Taskfile task currently hardcodes `apps/ops-page` paths for the four frontends. Low direct risk; update only if any task is added later that filters by old package name.

### 2.11 turbo.json

Turbo config does **not** list package names. Tasks are generic (`build`, `dev`, `lint`, …). Package graph discovery is via workspace packages.

**Cache risk:** Local and remote Turbo caches key on package identity + input hashes. After rename:

- Package name change → **new cache namespace** for that package (old entries orphaned — safe, just cold).
- Leftover `.turbo/` at repo root should be deleted for cleanliness (see §4).

Outputs patterns (`.next/**`, `dist/**`) remain valid under new folders.

### 2.12 Root package.json scripts

```json
"build": "turbo run build",
"dev": "turbo run dev",
...
```

No explicit filter on the four old names. **Low risk.**

### 2.13 Documentation & human path references (large blast radius, low runtime risk)

Hundreds of references across:

- Root `README.md` (structure tree, port table)
- `docs/001-gaps/**` (many absolute and relative paths under old `lazuar-hub` paths as well as current names)
- `docs/architecture-decision-log/**` (ADR 007, 013, 014, 016, 017, 018, 022, 023, …)
- `apps/lazuar-docs/**` (`pnpm --filter developers-page dev`, prose)
- `plans/001-backend/**`
- Inline source comments (e.g. `// apps/portal-page/src/...` file headers; `// apps/ops-page/src/hooks/use-debounce.ts` in superadmin)

**These do not break builds** if left stale, but they **will break AI agents, onboarding, and search**. Treat docs as a **second wave** if needed, but **must-update** for operational continuity:

1. Root `README.md` structure + ports table  
2. `apps/lazuar-docs` commands that use `pnpm --filter developers-page`  
3. Any script or ADR that is still used as a living runbook  

Historical gap reports and ADRs can keep old names with a one-line “renamed to …” note, or bulk-update — product decision, not a git risk.

### 2.14 Source file path comments

Several portal files begin with a comment of the form `// apps/portal-page/src/...`. Superadmin has a copy-paste comment pointing at `apps/ops-page/...`. These are cosmetic; grep-driven refactors may want to update them to avoid confusion.

---

## 3. Git history: best practices for the rename

### 3.1 Goals

1. Preserve `git log --follow` / blame continuity for files under each app.
2. Produce a reviewable PR: renames visible as renames, not delete+add of entire trees.
3. Avoid committing build artifacts or `node_modules`.
4. Avoid a multi-commit half-state on `main` where CI builds broken Dockerfiles.

### 3.2 Preconditions (mandatory)

1. **Clean working tree** for the four apps (or stash everything unrelated). Dirty untracked `node_modules`, `.next`, `dist` under apps should not be mixed into the rename commit.
2. Confirm **no process has cwd** inside the old folders (dev servers, IDE terminals, `mprocs`, file watchers). macOS/Linux will allow rename of directories with open files in many cases, but watchers and Vite/Next will thrash or write into deleted paths.
3. Confirm **no other branch** is mid-flight with heavy edits under old paths without a plan to rebase (see §3.6).
4. Prefer running the rename on a **fresh branch from latest `main`**.

### 3.3 Preferred sequence (single branch, one PR)

```bash
# 0) From repo root, clean artifacts first (see §4) — optional but recommended
# so git status is not flooded with untracked noise under old trees.

# 1) Rename directories with git mv (preserves rename detection)
git mv apps/developers-page apps/lazuar-spec
git mv apps/ops-page        apps/lazuar-ops
git mv apps/portal-page     apps/lazuar-portal
git mv apps/superadmin-page apps/lazuar-admin

# 2) Edit package.json "name" in each app to match new folder
# 3) Update Dockerfiles, docker-bake.hcl, docker-compose*.yml,
#    mprocs-dev.yaml, .github/workflows/ghcr.yml, README critical paths
# 4) Regenerate lockfile
pnpm install

# 5) Verify rename detection
git status   # expect "renamed:" for paths, not pure delete/add
git diff --stat --find-renames

# 6) Smoke (see §11)
# 7) Commit — ideally ONE commit for renames+tooling, or:
#    - commit 1: git mv only
#    - commit 2: package names + lockfile + tooling path updates
#    Both must land together before merge to main.
```

### 3.4 Why `git mv` (not Finder / `mv` + `git add`)

- `git mv` stages the rename atomically and maximizes similarity detection.
- Plain filesystem `mv` **can** still be detected if you `git add -A` and similarity is high, but is more fragile when package.json content also changes in the same commit (similarity score drops).
- **Never** `cp -R` old → new then `rm -rf` old: history splits and the PR becomes unreadable.

### 3.5 Similarity threshold and large edits

If you combine rename with large internal refactors in the same commit, Git may fail to detect renames (`git config diff.renames` / `-M` threshold). **Do not** mix product code rewrites with folder renames. This PR should be **rename + identifier/path updates only**.

For review:

```bash
git log --follow -- apps/lazuar-ops/src/App.tsx
git blame -C -C -C apps/lazuar-ops/src/App.tsx
```

Use `-C` (copy detection) if reviewers need extra help after rename.

### 3.6 Concurrent work and rebase risk

Anyone with a long-lived branch that edited `apps/ops-page/**` will face painful rebases after this lands. Mitigations:

1. **Announce freeze window:** no non-critical frontend work until rename merges.
2. Or land rename first on a quiet day; give contributors:

   ```bash
   git fetch origin
   git rebase origin/main
   # if Git loses renames:
   git checkout origin/main -- apps/lazuar-ops   # example recovery — prefer redoing patch on new paths
   ```

3. For in-flight PRs, GitHub sometimes shows weird delete/add; rebasing onto the rename commit is the cleanest fix.

### 3.7 Case sensitivity and filesystem

All renames are **not** case-only changes (`ops-page` → `lazuar-ops`). No macOS case-insensitive APFS gotcha of the “Foo → foo” variety. Low risk.

### 3.8 Submodule / worktree / sparse-checkout

No evidence of submodules for these apps. If anyone uses **sparse-checkout** including `apps/ops-page`, they must update sparse patterns. Document in the PR description.

### 3.9 What history will look like for Docker and package.json

Dockerfiles and package.json will show as renames **plus** content edits. That is expected. The bulk of `src/` / `app/` trees should appear as pure renames.

---

## 4. Local artifacts that must be cleaned

These paths are **gitignored** (mostly) but **exist on developer machines and CI runners’ caches**. A folder rename does **not** migrate them correctly.

### 4.1 Per-app artifacts (observed / expected)

| Artifact | Where | Gitignored? | After rename risk |
|----------|--------|-------------|-------------------|
| `node_modules/` | each app + root + packages | yes (`node_modules/`) | Symlinks point at pnpm store by path; **orphan old app `node_modules` directories** if rename done outside git while modules exist; new folder needs fresh `pnpm install` from root |
| `.next/` | developers + portal | yes (root + app gitignore) | Stale cache under **old** path if `mv` without delete; new path cold start OK; **corrupt if partial copy** |
| `dist/` | ops-page (observed on disk), superadmin after build | yes (app `.gitignore` for ops/superadmin; root `dist/`) | Orphan dist under old path; harmless but confusing |
| `tsconfig.tsbuildinfo` | developers-page observed at app root | app gitignore `*.tsbuildinfo` | Stale; TypeScript may read wrong incremental graph if left behind |
| `node_modules/.tmp/*.tsbuildinfo` | ops/superadmin tsconfig `tsBuildInfoFile` | under node_modules | Goes away with node_modules purge |
| Nested `pnpm-lock.yaml` | developers-page, portal-page (disk) | may be untracked | Delete; never commit nested lockfiles for workspace packages |
| `.turbo/` | repo root | yes | Orphan cache entries for old package names; delete for clean slate |
| `.task/` | Taskfile cache | yes | Low risk; delete if tasks change |
| Next `next-env.d.ts` | developers/portal gitignore lists it | gitignored in app | Regenerated by `next dev` / build |

### 4.2 Recommended purge commands (developer machine, after rename branch checked out)

```bash
# From monorepo root — destructive to local caches only
rm -rf node_modules .turbo .task

# If old folders somehow still exist (failed rename, leftover):
rm -rf apps/developers-page apps/ops-page apps/portal-page apps/superadmin-page

# New folders: drop any carried-over junk
rm -rf \
  apps/lazuar-spec/node_modules apps/lazuar-spec/.next apps/lazuar-spec/tsconfig.tsbuildinfo \
  apps/lazuar-spec/pnpm-lock.yaml \
  apps/lazuar-ops/node_modules apps/lazuar-ops/dist \
  apps/lazuar-portal/node_modules apps/lazuar-portal/.next apps/lazuar-portal/pnpm-lock.yaml \
  apps/lazuar-admin/node_modules apps/lazuar-admin/dist

# Reinstall once from root
pnpm install
```

### 4.3 What not to delete

- `packages/api-spec/dist/**` OpenAPI YAML — used by developers-page / lazuar-spec Scalar loader and sometimes treated as committed build output in this monorepo’s workflow. **Do not** wipe as part of app rename cleanup unless regenerating via `task gen` / `pnpm --filter @repo/api-spec build`.
- `.env` files under apps if any exist locally (gitignored) — copy/rename carefully; do not commit.

### 4.4 Docker build cache

Local `docker buildx` layers for old dockerfile paths remain until pruned. Not harmful; wastes disk. Optional:

```bash
docker builder prune -f
# or more aggressive
docker buildx prune -f
```

GHA cache scopes (`lazuar-hub-ops`, etc.) remain valid because they are image-name-based.

### 4.5 pnpm store path coupling

pnpm’s **content-addressable store** is global (e.g. `~/Library/pnpm/store` on macOS). It is **not** keyed by monorepo folder name. Renames do **not** corrupt the store.

What **is** path-sensitive:

- App-local `node_modules` symlinks into the store / virtual store
- Workspace protocol links between packages

Therefore: **never** manually move `node_modules` with the folder. Always reinstall from root after rename.

`pnpm-workspace.yaml` `allowBuilds` list is package-name based (`sharp`, `esbuild`, …), not app-folder based — no change required.

---

## 5. IDE, Turbo, and editor tooling

### 5.1 VS Code / Cursor `launch.json`

- Root `.vscode/` is **gitignored** (`.gitignore` line `.vscode/`).
- No `.vscode` or `.cursor` directory exists in the workspace at analysis time.
- **No checked-in launch configurations** to update.

**Residual risk:** Developers may have **user-local** or **workspace-local untracked** launch configs / multi-root workspace files pointing at `apps/ops-page`. Those break silently (debug button starts wrong cwd). Call out in PR description: “Update any personal launch configs.”

### 5.2 TypeScript project references / path mappings

App tsconfigs use local relative paths and standard Next/Vite layouts. No monorepo-wide `paths` alias of the form `@/apps/ops-page`. **Low rename risk** inside tsconfig, except incremental info files (§4).

### 5.3 ESLint

Per-app `eslint.config.mjs` (portal, developers). No evidence of root eslint path list of the four apps. **Low risk.**

### 5.4 Turbo remote cache (if enabled later)

Not configured with explicit package name filters in `turbo.json`. Package renames naturally create new cache keys. If a remote cache is enabled with team tokens, old entries expire; no security issue.

### 5.5 JetBrains / `.idea`

`.idea/` is gitignored. Local run configurations may hardcode paths — same residual risk as VS Code.

---

## 6. `.gitignore` analysis

### 6.1 Root `.gitignore` (relevant entries)

| Pattern | Effect on rename |
|---------|------------------|
| `node_modules/` | Covers all nested modules; no path list of apps |
| `.next/` | Covers Next caches under any app |
| `dist/` | Covers Vite outputs |
| `.turbo/` | Turbo cache |
| `.task/` | Task cache |
| `.vscode/`, `.idea/` | IDE dirs not tracked |
| `ctx.ignore` | AI ignore file |
| No explicit `apps/ops-page` paths | **No gitignore update required for rename** |

### 6.2 Per-app `.gitignore`

- **developers-page / portal-page:** Next template style; `*.tsbuildinfo`, `/.next/`, `/node_modules`, ignores `next-env.d.ts`.
- **ops-page / superadmin-page:** `node_modules/`, `dist/`, `.env*`.

These files **move with `git mv`**. Patterns are relative to the app directory — **no content change required**.

### 6.3 Gap: root does not list `*.tsbuildinfo`

Root relies on app-level ignore for tsbuildinfo (developers/portal). Ops/superadmin put tsbuildinfo under `node_modules/.tmp` via `tsBuildInfoFile`. **No rename-specific gap.** Optional hygiene: add `*.tsbuildinfo` at root later (out of scope).

### 6.4 Risk of accidentally committing artifacts during rename

If a developer runs `git add apps/` after a messy `mv` that left `node_modules` un-ignored due to a force-add habit (`git add -f`), disaster. Stick to `git mv` + selective adds of source and config. Review `git status` before commit; **node_modules must never appear as staged**.

---

## 7. `ctx.include` and AI context files

### 7.1 `ctx.include` (repo root)

Current content (single line):

```text
apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/BroadcastTests.cs
```

**No references** to `developers-page`, `ops-page`, `portal-page`, or `superadmin-page`.

**Action:** None required for the four renames unless future include lists grow.

### 7.2 `ctx.ignore`

Gitignored path (`ctx.ignore` in `.gitignore`). Local-only. Developers who listed old app paths in a personal `ctx.ignore` should update; not a repo concern.

### 7.3 README “Development Context” section

Documents:

```sh
fd -t f --ignore-file ctx.ignore | ctx | hxn
cat ctx.include | ctx | hxn
```

Unaffected by folder names.

### 7.4 Agent instruction files

| File | Location | Content relevance |
|------|----------|-------------------|
| `AGENTS.md` | `apps/developers-page/AGENTS.md` only | Next.js agent rules; **no path strings** |
| `CLAUDE.md` | `apps/developers-page/CLAUDE.md` | `@AGENTS.md` only |
| Root AGENTS/CLAUDE | **Absent** | — |

After `git mv developers-page → lazuar-spec`, these files **move automatically**. **No text edit required** for correctness. Optional: add a one-line note that the app is the Scalar/OpenAPI hub named `lazuar-spec` — product docs decision.

**Scoping rule reminder:** Project instruction files apply to the directory tree they live in. After rename, agents working under `apps/lazuar-spec/` still see `AGENTS.md`. Agents working under other apps still have no AGENTS.md (unchanged).

---

## 8. Tests asserting on folder names

### 8.1 Frontend tests

Per `docs/001-gaps/16-testing-coverage.md` and package.json review:

- ops / portal / superadmin / developers: **no test scripts**, no vitest/jest/playwright suites asserting paths.

### 8.2 Backend / architecture tests

Grep under `apps/lazuar-api/tests` for `developers-page|ops-page|portal-page|superadmin-page`: **no matches**.

Architecture tests target .NET modules and project structure, not frontend app folder names.

### 8.3 Conclusion

**Zero automated test failures expected solely from folder renames.** Risk is CI **build** (Docker/pnpm), not `dotnet test` / frontend unit tests.

Smoke tests after rename are **manual / script-driven** (see §11), not existing assertions.

---

## 9. Cursor / VS Code launch and workspace files

| Item | Status |
|------|--------|
| Checked-in `.vscode/launch.json` | **None** (`.vscode/` gitignored; dir absent) |
| Checked-in `.cursor/` rules | **None** |
| Multi-root `*.code-workspace` in repo | **None found** |

**Action:** Document for humans only. No repo file to patch for IDE launch configs.

---

## 10. Blast radius: simultaneous 4-app rename vs phased

### 10.1 Shared files touched either way

These files reference **multiple** old names in one place:

| File | How many of the four apps |
|------|---------------------------|
| `pnpm-lock.yaml` | All four importers |
| `mprocs-dev.yaml` | All four |
| `docker-bake.hcl` | All four targets |
| `.github/workflows/ghcr.yml` | All four matrix rows |
| `docker-compose.yml` | Three (ops, portal, superadmin) |
| `docker-compose.ghcr.yml` | Three |
| Root `README.md` | At least three (+ developers omitted historically) |
| Docs corpus | Dense cross-links between ops/portal/developers |

A **phased** approach still rewrites the lockfile and mprocs **four times**, each time risking merge conflicts with other PRs.

### 10.2 Cross-app coupling (runtime)

- ops `VITE_PORTAL_URL` points at portal origin (URL, not folder).
- developers/lazuar-spec loads specs from `packages/api-spec/dist` via relative `../../packages/...` (path depth **unchanged** if still one level under `apps/`).
- No import of source code across the four apps as workspace packages (they depend on `@repo/api-types-ts`, not on each other).

**Source graph coupling is low; tooling coupling is high.**

### 10.3 Simultaneous (big-bang) blast radius

**Must change in one atomic merge to `main`:**

1. Four `git mv` directory renames  
2. Four `package.json` `"name"` fields  
3. Four Dockerfiles (all path strings + Next CMD)  
4. `docker-bake.hcl` targets + dockerfile paths  
5. `.github/workflows/ghcr.yml` matrix dockerfile paths  
6. `docker-compose.yml` / `docker-compose.ghcr.yml` service keys + dockerfile paths (local)  
7. `mprocs-dev.yaml`  
8. Root `pnpm-lock.yaml` via `pnpm install`  
9. Critical human entrypoints: root `README.md`, `apps/lazuar-docs` filter commands  

**Need not change for rename alone:**

- `deploy/prod/*` service names and Caddy upstreams  
- GHCR image repository names (`lazuar-hub-ops`, …)  
- `scripts/remote-deploy.sh` container health names  
- Backend tests  
- TypeSpec packages  

**Optional / deferred:**

- Historical ADRs and gap docs bulk path rewrites  
- Inline `// apps/portal-page/...` file header comments  
- GHCR image renames (product branding of images — separate initiative)  
- Compose service renames in prod (already short names)

### 10.4 Phased blast radius (one app per PR)

Example phase order sometimes proposed: portal → ops → superadmin → developers (or reverse).

| Phase | Touches | Residual inconsistency |
|-------|---------|------------------------|
| Rename only `portal-page` → `lazuar-portal` | lockfile, bake, CI matrix row, compose portal, mprocs portal line, portal Dockerfile | mprocs/README still mixed old/new names; reviewers keep relearning |
| Then ops | same class of files again | Second lockfile churn; second CI path-filter full rebuild of all apps under `apps/**` anyway |
| … | … | Four deploy-adjacent CI runs if each merges to main |

**CI note:** workflow path filter is `apps/**`. **Any** single app rename under `apps/` triggers **full matrix build of all images** on push to main (api + all frontends). Phasing therefore does **not** reduce CI cost per merge; it multiplies full pipeline runs ×4.

### 10.5 Failure modes unique to phasing

1. **Docs and agents** say `ops-page` while tree has `lazuar-ops` for half the apps — higher confusion than one clean cut.  
2. **Developer muscle memory** and shell history: `cd apps/ops-page` fails for some apps only.  
3. **Cherry-picks** across half-renamed history are harder.  
4. **No progressive production value:** folder names are not user-facing; partial rename delivers zero product benefit.

### 10.6 Failure modes unique to big-bang

1. **Larger single PR** — harder review if docs are included; mitigate by **excluding historical doc rewrites** from the PR and limiting to tooling + package names + critical README.  
2. **All four frontends break at once** if a systematic Dockerfile mistake is made (e.g. forgetting Next CMD path). Mitigate with checklist (§11) and optional PR build workflow before merge.  
3. **Rebase pain for all open frontend branches at once** — mitigate with a short freeze announcement.

### 10.7 Quantitative sense of “how large”

| Category | Approx. impact |
|----------|----------------|
| Tracked source files moved | Entire trees under four apps (hundreds of files) — mostly rename detection |
| High-risk config files to edit | ~10–15 files (Dockerfiles ×4, bake, compose ×2, mprocs, ghcr.yml, package.json ×4, lockfile) |
| Production deploy configs | 0 required |
| Automated tests to update | 0 |
| Docs references (optional) | 100+ prose hits across gaps/ADRs — **do not block rename** |
| Local artifact purge | Every developer machine once |

---

## 11. Post-rename verification checklist (operational)

Run on the rename branch before merge:

### 11.1 Git

- [ ] `git status` shows renames, not untracked copies of whole trees  
- [ ] `git log --follow -- apps/lazuar-ops/package.json` (and siblings) shows prior history  
- [ ] No `node_modules` staged  
- [ ] Lockfile importer keys are `apps/lazuar-spec`, `apps/lazuar-ops`, `apps/lazuar-portal`, `apps/lazuar-admin`

### 11.2 pnpm / Turbo

- [ ] `pnpm install` clean  
- [ ] `pnpm --filter lazuar-ops build`  
- [ ] `pnpm --filter lazuar-portal build`  
- [ ] `pnpm --filter lazuar-admin build`  
- [ ] `pnpm --filter lazuar-spec build`  
- [ ] `pnpm --filter lazuar-ops dev` / portal / admin / spec start on ports 3003 / 3004 / 3005 / 3002  
- [ ] `task fe` (mprocs) starts all four without `cd: no such file`

### 11.3 Docker (critical for Next)

- [ ] `docker build -f apps/lazuar-portal/Dockerfile .` succeeds  
- [ ] Container start: `node apps/lazuar-portal/server.js` path exists inside image  
- [ ] Same for `lazuar-spec`  
- [ ] Vite apps: image serves `dist` on :3000  
- [ ] Optional: `docker buildx bake` with updated targets

### 11.4 CI

- [ ] ghcr.yml matrix dockerfile paths resolve  
- [ ] On merge to main, matrix builds all five images (api + 4 FE) green  
- [ ] Deploy health-gates still use `hub-*` names (unchanged)

### 11.5 Developers / Scalar path depth

- [ ] From `apps/lazuar-spec`, `path.join(process.cwd(), "../../packages/api-spec/dist")` still resolves (same depth as before)  
- [ ] Docker `OPENAPI_SPEC_ROOT` path unchanged  

---

## 12. Phased vs big-bang — recommendation

### Recommendation: **Big-bang, single PR, tooling-scoped**

**Do in one PR / one merge to `main`:**

1. `git mv` all four app directories.  
2. Rename all four package.json `"name"` fields to match.  
3. Update all Dockerfiles (especially Next standalone CMD and static copy paths).  
4. Update bake, local compose files, mprocs, GHCR workflow matrix.  
5. Regenerate root `pnpm-lock.yaml` via `pnpm install`.  
6. Update root README structure/ports and living docs commands (`lazuar-docs` filter).  
7. Instruct all developers to purge local caches (§4) and reinstall.

**Defer (separate PRs, no rush):**

- Bulk rewrite of historical ADRs / gap analyses.  
- GHCR image repository renames (`lazuar-hub-developers` → something with `spec`, etc.).  
- Production compose service renames (already short and stable).  
- Cosmetic source path comments.

### Why not phased

1. **CI cost multiplies** (each merge rebuilds full image matrix).  
2. **Lockfile and mprocs churn multiplies** with no user-visible benefit.  
3. **Half-renamed monorepo** maximizes human and agent confusion.  
4. **Production is already decoupled** from folder names — phasing does not reduce production risk; correctness of Dockerfiles does. Getting all four Dockerfiles right in one focused review is easier than remembering the Next CMD footgun four times over weeks.

### When phased would make sense (not this case)

- If each “app” were a separately versioned deployable with independent release trains and independent lockfiles.  
- If rename required downtime windows per service.  
- If folder rename were entangled with incompatible runtime URL changes.

None of those apply: one monorepo, one lockfile, one GHCR workflow, production names already stable.

### Suggested PR title / commit message direction

```text
chore: rename frontend apps to lazuar-{spec,ops,portal,admin}

- git mv apps/*-page → apps/lazuar-*
- align package.json names and pnpm lockfile importers
- update Dockerfiles (Next standalone paths), bake, compose, mprocs, GHCR matrix
```

Keep the PR free of feature work.

---

## 13. Risk register (summary table)

| ID | Risk | Severity | Likelihood | Mitigation |
|----|------|----------|------------|------------|
| R1 | Next standalone CMD still points at `apps/portal-page/server.js` or `apps/developers-page/server.js` | **Critical** | Medium if checklist skipped | Dockerfile checklist; local docker run smoke |
| R2 | pnpm lockfile importers stale / hand-edited wrong | High | Medium | Always `pnpm install` after mv |
| R3 | mprocs / `task fe` broken | High (dev) | High if forgotten | Update mprocs in same PR; smoke `task fe` |
| R4 | GHCR workflow dockerfile path 404 | High (CD) | High if forgotten | Update matrix; CI must go green before merge trust |
| R5 | Stale `node_modules` / `.next` poison local dev | Medium | High | Document purge; reinstall from root |
| R6 | Nested app lockfiles confuse installs | Medium | Low–Med | Delete nested lockfiles; root-only install |
| R7 | Git history appears as delete+add | Medium (review) | Low with `git mv` | Use `git mv`; avoid mixed refactors |
| R8 | Open feature branches rebase hell | Medium | Medium | Freeze window; announce |
| R9 | Docs / AI agents still say `ops-page` | Low (runtime) / Med (DX) | Certain if deferred | Living docs in PR; history later |
| R10 | Prod Caddy/compose broken | Low | Low | No change required for folder rename |
| R11 | Tests fail on folder name assertions | None observed | None | — |
| R12 | launch.json / IDE configs | Low | Low (local only) | Note in PR |
| R13 | Turbo cache misses | Low | Certain | Accept cold cache; optional `.turbo` delete |
| R14 | Accidental commit of artifacts | High if happens | Low with discipline | Review `git status`; never `git add -f node_modules` |
| R15 | Relative path `../../packages/api-spec/dist` breaks | Low | Low if stay under `apps/` | Keep single segment under `apps/`; verify Scalar |

---

## 14. Exact high-risk string patterns to find/replace (implementation aid)

When implementing (separate task), search the repo for at least:

```text
apps/developers-page
apps/ops-page
apps/portal-page
apps/superadmin-page
developers-page
ops-page
portal-page
superadmin-page
```

**Careful exclusions / false positives:**

- Prose “developers page” / product language “ops page” in marketing sense.  
- Backend module names: `Modules/Ops`, `OpsDbContext`, route prefixes `/ops` — **not** frontend folder renames.  
- URL paths `/portal`, `/docs`, `/admin` — **not** folder names.  
- Image names `lazuar-hub-ops` — **do not rename** as part of this effort unless explicitly scoped.  
- Container names `hub-ops`, `lazuar-ops` — already good; leave prod alone.  
- File `docs/001-gaps/04-developers-page-dx.md` — historical doc filename; renaming the markdown file is optional and churny.

**Package filter forms to update:**

- `pnpm --filter developers-page` → `pnpm --filter lazuar-spec`  
- `pnpm --filter ./apps/developers-page` → `pnpm --filter ./apps/lazuar-spec`  
- `pnpm --filter ./apps/developers-page...` (Docker) → new path  

---

## 15. Developer communication template (for PR / Slack)

> We are renaming monorepo frontend folders in one PR:
>
> - `apps/developers-page` → `apps/lazuar-spec`  
> - `apps/ops-page` → `apps/lazuar-ops`  
> - `apps/portal-page` → `apps/lazuar-portal`  
> - `apps/superadmin-page` → `apps/lazuar-admin`  
>
> Package names match. Production URLs, Caddy paths, and GHCR image names are unchanged.
>
> After pulling main:
>
> ```bash
> rm -rf node_modules .turbo apps/*/node_modules apps/*/.next apps/*/dist
> pnpm install
> task fe
> ```
>
> Please rebase open frontend branches promptly. Avoid landing large FE feature PRs during the freeze.

---

## 16. Final decision record

| Decision | Choice |
|----------|--------|
| Strategy | **Big-bang** four-app rename in one tooling-focused PR |
| git method | **`git mv` only**, no copy/delete |
| package.json names | **Align with new folder names** |
| GHCR image names | **Keep** (`lazuar-hub-ops`, etc.) |
| deploy/prod compose + Caddy | **No change required** |
| Local artifact policy | **Purge + reinstall** mandatory after pull |
| Historical docs | **Deferred** bulk update; fix living entrypoints only |
| Tests | **No folder-name assertions found**; rely on build/smoke |
| IDE launch configs | **None in repo**; local only |
| AGENTS.md / CLAUDE.md | **Move with developers-page → lazuar-spec**; no text dependency on old path |
| ctx.include | **No app paths**; no change |

---

## 17. Appendix — production vs monorepo naming (why prod is safe)

| Layer | Ops | Portal | Superadmin | Developers/Spec |
|-------|-----|--------|------------|-----------------|
| Folder today | `apps/ops-page` | `apps/portal-page` | `apps/superadmin-page` | `apps/developers-page` |
| Folder proposed | `apps/lazuar-ops` | `apps/lazuar-portal` | `apps/lazuar-admin` | `apps/lazuar-spec` |
| package.json name today | `ops-page` | `portal-page` | `superadmin-page` | `developers-page` |
| Local compose service | `ops-page` | `portal-page` | `superadmin-page` | (not in local full profile) |
| Local container_name | `lazuar-ops` | `lazuar-portal` | `lazuar-superadmin` | — |
| Prod compose service | `ops` | `portal` | `superadmin` | `developers` |
| Prod container_name | `hub-ops` | `hub-portal` | `hub-superadmin` | `hub-developers` |
| GHCR image | `lazuar-hub-ops` | `lazuar-hub-portal` | `lazuar-hub-superadmin` | `lazuar-hub-developers` |
| Public path | `/` | `/portal` | `/admin` | `/docs` |

The rename closes the gap between monorepo folder names and the already-preferred `lazuar-*` / short prod vocabulary. It is **almost entirely a developer-experience and build-path change**, not a production topology change — provided Dockerfiles and CI paths are updated correctly in the same atomic merge.

---

*End of analysis. No application source behavior was changed by this document.*
