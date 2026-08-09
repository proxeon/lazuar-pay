# 04 — Local developer tooling rename impact

**Scope:** Taskfile.yml, mprocs-dev.yaml, `scripts/`, `script/`, root `package.json` / Turbo / pnpm workspace, Makefile-like runners, ports, process names, cwd path assumptions.

**Proposed renames:**

| Current folder / package name | Proposed name |
|-------------------------------|---------------|
| `developers-page` | `lazuar-spec` |
| `ops-page` | `lazuar-ops` |
| `portal-page` | `lazuar-portal` |
| `superadmin-page` | `lazuar-admin` |

**Out of scope for this doc (covered elsewhere):** Dockerfiles, docker-bake.hcl service wiring, app source code renames, production Caddy. They are mentioned only where Taskfile or local scripts *invoke* them or where ports cross the FE boundary.

**Investigation date:** 2026-08-08  
**Repo root:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`

---

## 1. Executive summary (tooling only)

Local frontend development is driven almost entirely by:

1. **`mprocs-dev.yaml`** — hardcodes process keys **and** `cd apps/<old-name> && pnpm dev` for all four apps.
2. **`task fe`** in `Taskfile.yml` — does **not** name the apps itself; it only runs `mprocs -c mprocs-dev.yaml`. If mprocs is updated and Taskfile is not, `task fe` still works. If folders rename without mprocs updates, **`task fe` hard-fails** for every frontend process.
3. **Per-app `package.json` `name` + `dev` scripts** — ports live in package.json (`next dev -p` / `vite --port=`). Package `name` fields must match any `pnpm --filter <name>` usage.
4. **Root `package.json` + Turbo + `pnpm-workspace.yaml`** — no hardcoded app names; discovery is via `apps/*` and each package’s `name`. Folder renames alone are fine for workspace discovery; **lockfile importer paths** and **filter-by-name** commands must still be updated.
5. **`scripts/` and `script/`** — **do not** reference the four frontend folder names. They hit API URLs / prod container names only.
6. **No Makefile / Justfile / mise / nx / moon / Tilt / .vscode tasks** exist in this repo.

**If folders rename without updating tooling:**

| Entry point | Breaks? |
|-------------|---------|
| `task fe` → mprocs | **Yes** — all four FE shells fail (`cd apps/ops-page` etc. no such directory) |
| `task dev` (API only) | **No** |
| `task gen` / infra / api:* / tunnel:api | **No** (no FE app paths) |
| `task tunnel:fe` | Already stale (targets port 3020 / “community-page”); not one of the four apps |
| `task docker:build` / `docker:push` / `docker:up:full` | **Yes** once bake/compose Dockerfiles still point at old paths (Taskfile itself only shells into bake/compose) |
| `pnpm dev` (turbo) | Works only if each app still has a `dev` script under `apps/*` after rename; package name renames affect filters, not turbo.json |
| `pnpm --filter developers-page dev` | **Yes** until package `name` is renamed |
| `scripts/lhdn_sandbox/*` | **No** (API-only) |
| `scripts/remote-deploy.sh` | **No** for folder names (uses prod container names `hub-ops` etc.) |
| `script/second-app-proof.md` | **No** for folder names (API-only; mentions “Hub Ops” in prose) |

---

## 2. Inventory of local task runners and config files

| File | Role | References the 4 apps? |
|------|------|------------------------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/Taskfile.yml` | Primary task runner (go-task) | **Indirect** via `mprocs-dev.yaml` (`task fe`); docker tasks via bake/compose (not path-hardcoded in Taskfile for FE apps) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/mprocs-dev.yaml` | Multiprocess FE + optional tunnels | **Yes — all four by process key + cwd** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/package.json` | Root scripts: turbo, docs | **No** app names |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/turbo.json` | Turbo pipeline | **No** app names |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/pnpm-workspace.yaml` | Workspace globs `apps/*`, `packages/*` | **No** explicit names; auto-discovers renamed folders |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/pnpm-lock.yaml` | Lock importers keyed by path | **Yes** — `apps/developers-page`, `apps/ops-page`, `apps/portal-page`, `apps/superadmin-page` |
| Per-app `apps/*/package.json` | `name` + `dev` port scripts | **Yes** — each package `name` is the old app id |
| `scripts/` | LHDN sandbox + remote deploy | **No** FE folder names |
| `script/` | Second-app proof markdown harness | **No** FE folder names |
| Makefile / Justfile / etc. | — | **Absent** |

---

## 3. `mprocs-dev.yaml` — complete process inventory

**Absolute path:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/mprocs-dev.yaml`

**Full contents (evidence):**

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

### 3.1 Processes that reference the four apps

| Process key (mprocs UI name) | Shell | Path assumption | Autostart | Invoked package script |
|------------------------------|-------|-----------------|-----------|------------------------|
| `developers-page` | `cd apps/developers-page && pnpm dev` | Folder `apps/developers-page` exists relative to repo root (mprocs CWD = monorepo root when launched via `task fe`) | `true` | That app’s `package.json` → `"dev": "next dev -p 3002"` |
| `ops-page` | `cd apps/ops-page && pnpm dev` | `apps/ops-page` | `true` | `"dev": "vite --port=3003 --host=0.0.0.0"` |
| `superadmin-page` | `cd apps/superadmin-page && pnpm dev` | `apps/superadmin-page` | `true` | `"dev": "vite --port=3005 --host=0.0.0.0"` |
| `portal-page` | `cd apps/portal-page && pnpm dev` | `apps/portal-page` | `true` | `"dev": "next dev -p 3004"` |

### 3.2 Processes that do **not** reference the four apps

| Process key | Shell | Notes |
|-------------|-------|-------|
| `ngrok-api-tunnel` | `task tunnel:api` | Tunnels API port **8080**; no FE folder |
| `ngrok-fe-tunnel` | `task tunnel:fe` | Tunnels port **3020** for legacy “community-page” (see Taskfile §4.4); **not** ops/portal/superadmin/developers |

### 3.3 Path / cwd assumptions

- mprocs is started from **repository root** by `task fe` (`mprocs -c mprocs-dev.yaml`). Relative `cd apps/...` therefore resolves as `<repo>/apps/...`.
- Shells use **`pnpm dev` inside the app directory**, not `pnpm --filter <package-name> dev` from root. That means:
  - **Folder rename** must update the `cd apps/...` path.
  - **Package `name` rename alone** does **not** break mprocs, as long as the folder path and a local `package.json` with a `dev` script remain.
- Ports are **not** set in mprocs; they come from each app’s `package.json` `dev` script.

### 3.4 What breaks if folders rename without updating mprocs

For each of the four processes:

1. mprocs still starts (config parses).
2. Shell runs `cd apps/ops-page` (etc.).
3. `cd` fails → process exits / restarts with error; **no frontend on that port**.
4. Developer still sees process names like `ops-page` in the mprocs TUI, which would be **stale labels** even after path fix if keys are not renamed.

### 3.5 Recommended mprocs updates

Replace process keys and paths in lockstep with folder renames:

```yaml
# mprocs-dev.yaml (recommended after rename)
procs:
  lazuar-spec:
    shell: cd apps/lazuar-spec && pnpm dev
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

**Optional alternative** (does not require `cd` if package `name` is updated):

```yaml
  lazuar-ops:
    shell: pnpm --filter lazuar-ops dev
    autostart: true
```

This is more resilient to folder layout churn **only if** package names are updated; path-based `cd` is what the repo uses today.

**Optional cleanup (not required for rename):** redefine `tunnel:fe` / `ngrok-fe-tunnel` to a real port among 3002–3005 (or drop it), since it currently targets 3020 / community-page.

---

## 4. `Taskfile.yml` — complete task inventory vs the four apps

**Absolute path:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/Taskfile.yml`  
**Version:** Taskfile v3

### 4.1 Tasks that reference the four apps (directly or via subprocess)

| Task | Lines (approx) | How it touches the four apps | Hardcoded old names? |
|------|----------------|------------------------------|----------------------|
| `fe` | 41–44 | `mprocs -c mprocs-dev.yaml` — starts all four FE procs | **No names in Taskfile**; **yes via mprocs** |
| `docker:build` | 208–219 | `docker buildx bake --load` → bake group `default` includes `portal-page`, `ops-page`, `superadmin-page`, `developers-page` | Echo text says `"api, portal, ops, superadmin"` only (informal; omits developers). **Actual targets live in `docker-bake.hcl`**, not Taskfile |
| `docker:push` | 245–256 | Same bake default group | Same |
| `docker:up:full` | 277–280 | `docker compose --profile full up -d --build` | Compose service names/paths: `ops-page`, `portal-page`, `superadmin-page` (**no `developers-page` service** in root `docker-compose.yml`) |
| `docker:up:ghcr` | 271–275 | `docker-compose.ghcr.yml` | Same three FE services + api/db; no developers service |

### 4.2 Tasks that do **not** reference the four apps

| Task | Purpose | Notes |
|------|---------|-------|
| `infra:up` | `docker-compose up db -d` | DB only |
| `infra:down` | `docker-compose down` | |
| `infra:reset` | `docker-compose down -v` | Destructive volumes |
| `infra:logs` | `docker-compose logs -f` | |
| `dev` | deps `infra:up`; `pnpm --filter lazuar-api dev` | API hot-reload only — **does not start frontends** |
| `docs` / `docs:build` | VitePress `lazuar-docs` | Different app |
| `api:restore`, `api:build`, `api:test` | .NET under `apps/lazuar-api` | |
| `api:db:migrate`, `api:migrations:*` | EF Core modules | Paths under `apps/lazuar-api` only |
| `tunnel:stop` | `pkill -9 ngrok` | |
| `tunnel:api` | `ngrok http 8080` | API |
| `tunnel:fe` | `ngrok http 3020` | Stale community-page; **not** 3002–3005 |
| `tunnel:status` | curl ngrok agent API | |
| `gen`, `gen:spec`, `gen:types-ts`, `gen:types-dotnet`, `gen:sdk-lhdn` | TypeSpec → OpenAPI → clients | **Indirect product value for developers-page** (OpenAPI files), but **no path into apps/developers-page** |
| `docker:builder` | buildx setup | |
| `docker:build:api` / `docker:push:api` | API image only | |
| `docker:login:ghcr` | GHCR auth | |

### 4.3 Evidence — `fe` and hybrid dev workflow

From Taskfile:

```yaml
  dev:
    desc: Start Hybrid Development Mode (launches Docker dependencies, runs migrations, then runs C# Hot-Reload Watcher)
    deps: [infra:up]
    cmds:
      # - task: api:db:migrate
      - pnpm --filter lazuar-api dev

  fe:
    desc: Full dev stack (Docker infra + mprocs for frontends and tunnels)
    cmds:
      - mprocs -c mprocs-dev.yaml
```

**Note:** The `fe` description says “Docker infra + mprocs…” but the task **only** runs mprocs; it does **not** call `infra:up`. The documented workflow in root README is:

```bash
task infra:up
task dev      # terminal 1 — API
task fe       # terminal 2 — frontends via mprocs
```

Evidence (README Getting Started):

```bash
# 1. Start local Docker dependencies (PostgreSQL)
task infra:up

# 2. Run database migrations and start the .NET hot-reload API watcher
task dev

# 3. In a new terminal, launch the frontends via mprocs
task fe
```

### 4.4 Evidence — `tunnel:fe` staleness (rename-adjacent)

```yaml
  tunnel:fe:
    desc: Start ngrok tunnel for Next.js community-page on port 3020
    cmds:
      - ngrok http 3020
```

- None of the four apps use port **3020**.
- Ports in use by the four apps: **3002, 3003, 3004, 3005** (see §6).
- Renaming the four apps does not fix or break this further; it remains a **pre-existing drift** risk if someone enables `ngrok-fe-tunnel` in mprocs expecting a current frontend.

### 4.5 Evidence — docker tasks vs FE names

`docker:build` echo (Taskfile only; informal):

```text
echo "Building REGISTRY=$REGISTRY TAG=$TAG PLATFORMS=$PLATFORMS (api, portal, ops, superadmin)"
```

Actual bake default group (`docker-bake.hcl` lines 48–50):

```hcl
group "default" {
  targets = ["api", "portal-page", "ops-page", "superadmin-page", "developers-page"]
}
```

So Taskfile docker orchestration **will break after folder rename** not because Taskfile embeds paths, but because:

- bake targets use names `portal-page`, `ops-page`, `superadmin-page`, `developers-page`
- bake `dockerfile = "apps/<old-name>/Dockerfile"`
- compose `dockerfile: apps/<old-name>/Dockerfile` and service keys `ops-page` etc.

Those files are outside pure “scripts” but are **downstream of Taskfile docker tasks**. Recommended: update bake/compose/Dockerfiles in the same rename PR as mprocs, or `task docker:build` / `task docker:up:full` will fail while `task fe` (if mprocs fixed) still works.

### 4.6 What breaks if folders rename without updating Taskfile

| Scenario | Result |
|----------|--------|
| Folders renamed; **mprocs not updated**; Taskfile unchanged | `task fe` runs but all four FE shells fail on `cd` |
| Folders renamed; **mprocs updated**; Taskfile unchanged | `task fe` works (Taskfile has no FE path literals) |
| Folders renamed; bake/compose not updated | `task docker:build`, `task docker:push`, `task docker:up:full` fail |
| Only Taskfile edited | **Not sufficient** — Taskfile does not contain the FE folder strings for local FE dev |

### 4.7 Recommended Taskfile updates

1. **No mandatory string replace in Taskfile for `task fe`** if mprocs alone is fixed.
2. **Optional docstring/desc polish:**
   - `fe` desc: mention new app names if you document them in prose.
   - `docker:build` echo: include developers / use new short names (`lazuar-spec`, etc.) for operator clarity.
3. **`tunnel:fe`:** either:
   - Point at a real app port (e.g. portal `3004` or ops `3003`), or
   - Remove / rename task and drop `ngrok-fe-tunnel` from mprocs until needed.
4. **Do not** add hardcoded `apps/lazuar-*` paths into Taskfile unless you introduce dedicated tasks like `fe:ops` — current design deliberately defers FE orchestration to mprocs.

---

## 5. Root `package.json`, Turbo, pnpm workspace

### 5.1 Root package.json

**Path:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/package.json`

```json
{
  "name": "lazuar",
  "private": true,
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
  },
  ...
}
```

| Script | Touches four apps? | Rename impact |
|--------|--------------------|---------------|
| `pnpm dev` → turbo | Runs `dev` in every workspace package that defines it, including the four FE apps **and** `lazuar-api` | Folder rename OK (workspace glob). Package `name` not required for turbo discovery. **Ports still from each package’s dev script.** Running all apps via turbo is an alternate path to mprocs; both must see correct package locations. |
| `pnpm build` / `lint` / `test` / `check-types` | Same — turbo graph | After rename, rebuild lockfile; no root script string changes needed |
| `docs:*` | `lazuar-docs` only | Unaffected |

**Evidence: no root scripts name `developers-page`, `ops-page`, `portal-page`, or `superadmin-page`.**

### 5.2 turbo.json

**Path:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/turbo.json`

- Tasks: `build`, `test`, `lint`, `check-types`, `dev` (persistent, uncached).
- **No package filters, no app names, no ports.**
- Rename impact: **none** on this file.

### 5.3 pnpm-workspace.yaml

```yaml
packages:
  - "apps/*"
  - "packages/*"
```

- Renaming `apps/ops-page` → `apps/lazuar-ops` is picked up automatically.
- Nested package.json `name` fields are independent of folder name.

### 5.4 pnpm-lock.yaml importers (path-keyed)

Evidence at top of lockfile:

```yaml
importers:
  ...
  apps/developers-page:
  apps/ops-page:
  apps/portal-page:
  apps/superadmin-page:
```

**After folder rename:** run `pnpm install` at repo root so importers rewrite to `apps/lazuar-spec`, etc. Until then, install/filter resolution can break or leave stale lock entries.

### 5.5 Per-app package.json (dev scripts + names) — evidence

#### `apps/developers-page/package.json`

```json
{
  "name": "developers-page",
  "scripts": {
    "dev": "next dev -p 3002",
    "build": "next build",
    "start": "next start",
    "lint": "eslint"
  }
}
```

#### `apps/ops-page/package.json`

```json
{
  "name": "ops-page",
  "scripts": {
    "dev": "vite --port=3003 --host=0.0.0.0",
    "build": "vite build",
    "preview": "vite preview",
    "clean": "rm -rf dist",
    "lint": "tsc --noEmit"
  }
}
```

#### `apps/portal-page/package.json`

```json
{
  "name": "portal-page",
  "scripts": {
    "dev": "next dev -p 3004",
    "build": "next build",
    "start": "next start",
    "lint": "eslint"
  }
}
```

#### `apps/superadmin-page/package.json`

```json
{
  "name": "superadmin-page",
  "scripts": {
    "dev": "vite --port=3005 --host=0.0.0.0",
    "build": "vite build",
    "preview": "vite preview",
    "clean": "rm -rf dist",
    "lint": "tsc --noEmit"
  }
}
```

### 5.6 Recommended package.json updates (local tooling)

| Current `name` | Recommended `name` (align with folder) | Port in `dev` | Change port? |
|----------------|----------------------------------------|---------------|--------------|
| `developers-page` | `lazuar-spec` | 3002 | **No** (stable URL for bookmarks/CORS) |
| `ops-page` | `lazuar-ops` | 3003 | **No** |
| `portal-page` | `lazuar-portal` | 3004 | **No** |
| `superadmin-page` | `lazuar-admin` | 3005 | **No** |

Keeping ports stable avoids churn in:

- Root README port table
- `apps/lazuar-api` `CorsOrigins` / `ClientUrl` (see §6.3)
- Compose host port mappings `3003:3000`, `3004:3000`, `3005:3000`
- Ops UI `VITE_PORTAL_URL` default `http://localhost:3004`

### 5.7 External filter-by-name usage (docs, not scripts/)

**Path:** `apps/lazuar-docs/docs/reference/openapi.md`

```bash
pnpm --filter developers-page dev
```

After package rename this must become:

```bash
pnpm --filter lazuar-spec dev
```

This is documentation, not `scripts/`, but it is a **developer tooling command** that will fail post-rename if left stale.

---

## 6. Port assignments (local)

### 6.1 Canonical local ports for the four apps

| App (current) | App (proposed) | Dev command source | Host port | How bound |
|---------------|----------------|--------------------|-----------|-----------|
| `developers-page` | `lazuar-spec` | `next dev -p 3002` | **3002** | Next CLI flag in package.json |
| `ops-page` | `lazuar-ops` | `vite --port=3003 --host=0.0.0.0` | **3003** | Vite CLI flags in package.json |
| `portal-page` | `lazuar-portal` | `next dev -p 3004` | **3004** | Next CLI flag in package.json |
| `superadmin-page` | `lazuar-admin` | `vite --port=3005 --host=0.0.0.0` | **3005** | Vite CLI flags in package.json |

**Vite configs** (`apps/ops-page/vite.config.ts`, `apps/superadmin-page/vite.config.ts`) do **not** set `server.port`; ports come only from package.json CLI.

**Next configs** do not set ports either; package.json `-p` does.

### 6.2 README standardized port table (stale w.r.t. developers)

Root README (Getting Started) lists:

| App | Port | URL |
|-----|------|-----|
| `lazuar-api` | 8080 | `http://localhost:8080` |
| `ops-page` | 3003 | `http://localhost:3003` |
| `portal-page` | 3004 | `http://localhost:3004` |
| `superadmin` | 3005 | `http://localhost:3005` |

**Missing:** `developers-page` / port **3002**, even though mprocs autostarts it.

Recommended post-rename table:

| App | Port | URL | Description |
|-----|------|-----|-------------|
| `lazuar-api` | 8080 | `http://localhost:8080` | API |
| `lazuar-spec` | 3002 | `http://localhost:3002` | Scalar OpenAPI / developer hub |
| `lazuar-ops` | 3003 | `http://localhost:3003` | Ops console |
| `lazuar-portal` | 3004 | `http://localhost:3004` | Checkout / portal |
| `lazuar-admin` | 3005 | `http://localhost:3005` | Superadmin |

### 6.3 Related ports outside the four apps (but affect local FE)

| Port | Consumer | Relevance |
|------|----------|-----------|
| 8080 | `lazuar-api`, `task tunnel:api` | API base for all FE clients |
| 5432 | Postgres via compose | `task infra:up` |
| 3020 / 3021 | Listed in API CORS; `tunnel:fe` uses 3020 | Legacy community-page; still in CorsOrigins |
| 5180 | `task docs` / VitePress (per Taskfile desc) | Separate docs site |
| 4040 | ngrok agent local API | `task tunnel:status` |

**API CORS allowlist** (`apps/lazuar-api/src/Lazuar.Api/appsettings.Development.json` and `appsettings.json`):

```text
CorsOrigins includes:
http://localhost:3000, 3001, 3002, 3003, 3004, 3005, 3020, 3021, 8080, 8090
```

As long as FE ports stay 3002–3005, **CORS does not need renames**. Renaming folders alone does not touch CORS.

**API ClientUrl** defaults to `http://localhost:3004` (portal). Folder rename does not change this; only a portal **port** change would.

**Ops portal deep links** (app code, not Taskfile, but port-coupled):

- `VITE_PORTAL_URL || "http://localhost:3004"` in ops-page product/quote panels.

### 6.4 Docker host ports (invoked by Taskfile docker tasks)

From root `docker-compose.yml` (profile `full`):

| Service key (current) | Host:container | Container name |
|-----------------------|----------------|----------------|
| `ops-page` | 3003:3000 | `lazuar-ops` |
| `portal-page` | 3004:3000 | `lazuar-portal` |
| `superadmin-page` | 3005:3000 | `lazuar-superadmin` |
| *(no developers service)* | — | — |

Interesting inconsistency for local tooling:

- **mprocs** runs developers-page on **3002**.
- **Root docker-compose full profile** does **not** include a developers service at all.
- **Production** `deploy/prod/docker-compose.yml` **does** include `developers` → container `hub-developers`.

Rename planning should treat “local FE via mprocs” and “local FE via compose full” as **different matrices**.

---

## 7. Path assumptions used by local FE processes

### 7.1 mprocs cwd chain

```
repo root
  └─ task fe
       └─ mprocs -c mprocs-dev.yaml
            └─ shell: cd apps/<name> && pnpm dev
                 └─ process.cwd() = apps/<name>
```

### 7.2 developers-page OpenAPI relative path (local monorepo)

**File:** `apps/developers-page/lib/openapi.ts`

```ts
const root =
  process.env.OPENAPI_SPEC_ROOT ||
  path.join(process.cwd(), "../../packages/api-spec/dist");
```

Assumptions:

1. Dev server CWD is the app directory (`apps/developers-page` or future `apps/lazuar-spec`).
2. Specs live at `packages/api-spec/dist/<module>/openapi.yaml` two levels up.
3. **Folder rename at the same depth (`apps/<new-name>`) does not break this formula.**
4. Moving the app outside `apps/` or nesting deeper **would** break the relative path.
5. Docker uses `OPENAPI_SPEC_ROOT=/app/openapi-specs` (prod) — independent of local rename.

Prerequisite for useful local Scalar: `task gen` / `pnpm` build of `packages/api-spec` must have produced `dist/`.

### 7.3 gen pipeline vs developers-page

`task gen` updates:

- `packages/api-spec/dist/...`
- `packages/api-types-ts`
- `packages/api-types-dotnet`
- LHDN SDKs

It does **not** start or rebuild `developers-page`. After gen, a running `next dev` for developers-page typically picks up new YAML on next request (file read each time via `readOpenApiSpec`). No Taskfile coupling to app name.

---

## 8. `scripts/` — full analysis

**Directory:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/scripts/`

```
scripts/
  lhdn_sandbox/
    00_provision.sh
    01_test_b2b.sh
    02_test_credit_note.sh
    03_test_b2c.sh
    04_upload_dummy_cert.sh
    05_test_b2b_v1_1.sh
    06_test_cancel.sh
    07_test_self_billed.sh
    cookies.txt
    run_all.sh
  remote-deploy.sh
```

### 8.1 `scripts/lhdn_sandbox/*`

| Item | References four apps? | Ports / URLs |
|------|----------------------|--------------|
| All shell tests | **No** | `LAZUAR_API="http://localhost:8080/api/v1"` in `00_provision.sh` |
| `run_all.sh` | **No** | Chains sandbox scripts only |
| `cookies.txt` | **No** | Cookie domain `localhost` for API auth cookie |

**Rename impact:** none for folder/package renames of the four FE apps.  
**Requires:** API running (`task dev` or compose api), not mprocs frontends.

### 8.2 `scripts/remote-deploy.sh`

**Purpose:** Run **on the production VPS** after configs sync; not a local FE launcher.

Evidence — health waits:

```bash
wait_healthy hub-api 180
wait_healthy hub-ops 60
wait_healthy hub-portal 90
wait_healthy hub-superadmin 60
wait_healthy hub-developers 90
wait_healthy hub-caddy 60
```

| String | Kind | Equals folder name? |
|--------|------|---------------------|
| `hub-ops` | Docker `container_name` in deploy/prod | **No** — prod already uses `hub-ops` while folder is `ops-page` |
| `hub-portal` | container_name | No |
| `hub-superadmin` | container_name | No |
| `hub-developers` | container_name | No |

Smoke curls use Host `hub.lazuar.com` paths `/`, `/portal`, `/docs` — **URL paths**, not monorepo folder names.

**Rename impact on remote-deploy.sh:** **None**, unless production container_names are deliberately changed in a separate effort (not required by monorepo app folder rename).

Default deploy dir: `DIR="${DIR:-/root/lazuar-hub-prod}"` — product branding path on VPS, not `apps/*`.

---

## 9. `script/` — full analysis

**Directory:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/script/`

Only file:

- `script/second-app-proof.md` — curl harness documentation for payments integration against Hub API.

Evidence:

```bash
export HUB="${HUB:-http://localhost:8080/api/v1}"
```

Prose mentions “Open Hub Ops for `$WORKSPACE_ID`” as a human step — product name, not a path to `apps/ops-page`.

**Rename impact:** none on tooling paths. Optional later doc polish if product marketing renames “Hub Ops” → something else (out of scope for folder rename).

---

## 10. Makefile-like / other runners — negative inventory

Searched for Makefile, Justfile, mise.toml, nx.json, moon.yml, Tiltfile, skaffold, Procfile, Earthfile, `.vscode` tasks:

**None present** under the monorepo root.

Local orchestration surface is exactly:

1. **Task** (`Taskfile.yml`)
2. **mprocs** (`mprocs-dev.yaml`)
3. **pnpm / turbo** (root package.json)
4. **docker compose / buildx bake** (invoked by Taskfile docker tasks)

---

## 11. Documented developer entrypoints that mention the four apps

| Location | Command / mention | Needs update? |
|----------|-------------------|---------------|
| Root `README.md` Getting Started | `task fe`; port table names `ops-page`, `portal-page`, `superadmin` | **Yes** — names (+ add developers/spec row) |
| Root `README.md` project structure | `ops-page/`, `portal-page/`, `superadmin-page/` | **Yes** (structure docs) |
| `mprocs-dev.yaml` | process keys + `cd apps/...` | **Yes — blocking** |
| `apps/lazuar-docs/docs/reference/openapi.md` | `pnpm --filter developers-page dev` | **Yes** |
| `apps/lazuar-docs` index / README | prose “developers-page” | **Yes** (docs) |
| `docs/001-gaps/04-developers-page-dx.md` | mprocs + port 3002 | Historical gap doc; optional |
| Taskfile `fe` | mprocs only | No string change required if mprocs fixed |

---

## 12. Breakage matrix — “folders renamed only”

Assume:

- `apps/developers-page` → `apps/lazuar-spec` (etc.)
- package.json `name` fields **not** updated
- mprocs **not** updated
- Taskfile **not** updated
- lockfile **not** regenerated

| Action | Result |
|--------|--------|
| `task fe` | All four FE processes fail: `cd: apps/ops-page: No such file or directory` (and siblings) |
| mprocs UI labels | Still show old names; red/failed procs |
| `task dev` | Still starts API |
| `task gen` | Still works |
| `pnpm install` | May rewrite or warn; importers still point at old paths until install refresh |
| `pnpm --filter ops-page dev` | Fails if package name still `ops-page` but path missing from workspace; after install, filter-by-name might still work **if** package.json moved and name unchanged — **path filter** `./apps/ops-page` fails |
| `pnpm --filter ./apps/ops-page...` (Dockerfile style) | Fails — path gone |
| `pnpm dev` (turbo) | Discovers packages under new folders if install succeeds; runs their `dev` scripts; **ports unchanged** |
| Browser `localhost:3003` etc. | Dead until something successfully starts vite/next |
| `scripts/lhdn_sandbox` | Unaffected |
| `scripts/remote-deploy.sh` | Unaffected |
| `task docker:build` | Fails at Dockerfile path `apps/ops-page/Dockerfile` etc. (via bake) |

---

## 13. Breakage matrix — “folders + package names renamed; tooling not”

| Action | Result |
|--------|--------|
| `task fe` / mprocs | Still fails on old `cd` paths |
| `pnpm --filter developers-page dev` | Fails (name gone) |
| `pnpm --filter lazuar-spec dev` | Works **if** package name updated and workspace install OK |
| Turbo `pnpm build` | Works after lockfile refresh |
| Docker bake/compose | Still broken until paths/targets updated |

---

## 14. Recommended update checklist (local tooling only)

### 14.1 Must update (blocking for `task fe`)

1. **`mprocs-dev.yaml`**
   - Process keys: `lazuar-spec`, `lazuar-ops`, `lazuar-portal`, `lazuar-admin`
   - Shells: `cd apps/<new> && pnpm dev`
2. **Each app `package.json` `name` field** (recommended same PR for filter consistency)
3. **`pnpm install`** to rewrite `pnpm-lock.yaml` importers

### 14.2 Should update (docs / DX consistency)

4. Root **README** Getting Started port table + structure tree names  
5. `apps/lazuar-docs/docs/reference/openapi.md` filter command  
6. Any other hub docs that say `pnpm --filter developers-page`

### 14.3 Taskfile-specific

7. **No path edits required** for `fe` / `dev` / `gen` / `infra:*` / `api:*` / `tunnel:api`  
8. Optionally refresh `docker:build` echo text  
9. Optionally fix or retire **`tunnel:fe`** / **`ngrok-fe-tunnel`** (stale 3020)

### 14.4 Taskfile docker path (adjacent; blocks `task docker:*`)

10. `docker-bake.hcl` target names + `dockerfile` paths  
11. Root `docker-compose.yml` / `docker-compose.ghcr.yml` service keys + dockerfile paths  
12. Each app Dockerfile internal `COPY apps/...` and `pnpm --filter ./apps/...`  
    (Not scripts/, but Taskfile `docker:build` depends on them)

### 14.5 Explicitly no change needed in

- `scripts/lhdn_sandbox/**`  
- `scripts/remote-deploy.sh` (container names already `hub-*`)  
- `script/second-app-proof.md`  
- `turbo.json`  
- `pnpm-workspace.yaml` globs  
- Port numbers in package.json **if** keeping 3002–3005  
- API CORS list **if** ports unchanged  

---

## 15. Suggested final local workflow after rename

```bash
# Terminal 1 — infrastructure + API
task infra:up
task dev

# Terminal 2 — all frontends (mprocs)
task fe
# expect processes: lazuar-spec, lazuar-ops, lazuar-portal, lazuar-admin
# URLs:
#   http://localhost:3002  lazuar-spec
#   http://localhost:3003  lazuar-ops
#   http://localhost:3004  lazuar-portal
#   http://localhost:3005  lazuar-admin

# Optional single-app
pnpm --filter lazuar-ops dev
pnpm --filter lazuar-spec dev

# After TypeSpec edits
task gen
# refresh Scalar by reloading lazuar-spec routes
```

---

## 16. Evidence index (absolute paths)

| Artifact | Absolute path |
|----------|---------------|
| Taskfile | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/Taskfile.yml` |
| mprocs | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/mprocs-dev.yaml` |
| Root package.json | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/package.json` |
| turbo.json | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/turbo.json` |
| pnpm-workspace | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/pnpm-workspace.yaml` |
| pnpm-lock importers | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/pnpm-lock.yaml` |
| developers package | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/developers-page/package.json` |
| ops package | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/ops-page/package.json` |
| portal package | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/portal-page/package.json` |
| superadmin package | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/superadmin-page/package.json` |
| openapi path helper | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/developers-page/lib/openapi.ts` |
| README ports / task fe | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/README.md` |
| LHDN sandbox scripts | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/scripts/lhdn_sandbox/` |
| remote deploy script | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/scripts/remote-deploy.sh` |
| second-app proof | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/script/second-app-proof.md` |
| filter docs example | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/reference/openapi.md` |
| compose (task docker:up:full) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docker-compose.yml` |
| bake (task docker:build) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docker-bake.hcl` |

---

## 17. One-line severity summary

| Area | Severity if rename without updates |
|------|-------------------------------------|
| **mprocs-dev.yaml** | **P0** — local multi-FE dev completely broken |
| **package.json names** | **P1** — filter-by-name and docs commands break |
| **pnpm-lock importers** | **P1** — install graph until regenerated |
| **Taskfile.yml FE tasks** | **P0 via mprocs dependency**; Taskfile itself has no FE path strings |
| **Taskfile docker tasks** | **P0 for docker path** via bake/compose (separate files) |
| **scripts/** | **P3 / none** for monorepo folder renames |
| **script/** | **none** |
| **turbo.json / workspace globs** | **none** |
| **Ports** | Prefer **keep** 3002–3005; rename does not force port changes |

---

*End of 04-taskfile-mprocs-scripts analysis. No application code was modified.*
