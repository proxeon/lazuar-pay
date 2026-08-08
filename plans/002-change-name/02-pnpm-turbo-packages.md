# 02 — PNPM / Turbo / Package Naming Impact

**Scope:** JavaScript/TypeScript monorepo packaging only — `package.json` name fields, pnpm workspace, turbo task graph, root scripts, lockfile importers, workspace dependency references, and filters.

**Proposed renames (folders):**

| Old folder | New folder |
|---|---|
| `apps/developers-page` | `apps/lazuar-spec` |
| `apps/ops-page` | `apps/lazuar-ops` |
| `apps/portal-page` | `apps/lazuar-portal` |
| `apps/superadmin-page` | `apps/lazuar-admin` |

**Out of scope for this document (covered by other rename plans):** Docker image tags beyond packaging filters, Caddy, domain strategy, C# paths, product docs content rewrites, app source logic.

**Do not change app application code as part of this packaging analysis.** This file is analysis only.

---

## 1. Executive conclusion (packaging)

| Question | Answer |
|---|---|
| Is **folder rename alone** enough for pnpm/turbo? | **No.** Folder rename is necessary for path-based tooling (Dockerfiles, `mprocs-dev.yaml`, Next standalone paths, lockfile importers). Package `name` must also be updated if you want `pnpm --filter <name>` / `turbo --filter=<name>` to use the new identifiers and to match monorepo naming consistency. |
| Is **`package.json` `"name"` rename alone** enough? | **No.** Dockerfiles, lockfile importers, and process managers hardcode `apps/<folder>` paths. Path filters like `--filter ./apps/ops-page` would keep working only if the folder stays the same. |
| Do other packages **depend on** these four apps as workspace packages? | **No.** Zero reverse workspace deps. These apps are leaves. |
| Do these apps depend on other workspace packages? | **Yes** (three of four): `@repo/api-types-ts` via `workspace:^`. That dependency is unaffected by renaming the *app* package. |
| Does root `turbo.json` hardcode app names? | **No.** Task graph is generic. |
| Does root `package.json` hardcode these four app names? | **No.** Only `lazuar-docs` is filtered by name today. |
| Does `pnpm-workspace.yaml` hardcode app names? | **No.** Glob `apps/*` auto-includes renamed folders. |
| Must `pnpm-lock.yaml` be regenerated? | **Yes**, after folder rename (importer keys are paths) and/or after `name` changes that affect resolution identity. Prefer one coordinated rename + `pnpm install` at repo root. |

**Recommended packaging policy:** rename **folder + `package.json` `"name"` in lockstep**, keep names unscoped and aligned with existing app pattern (`lazuar-api`, `lazuar-docs`), then regenerate the root lockfile once.

---

## 2. Inventory with evidence

### 2.1 Workspace membership — `pnpm-workspace.yaml`

**File:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/pnpm-workspace.yaml`

```yaml
packages:
  - "apps/*"
  - "packages/*"
allowBuilds:
  '@google/genai': true
  esbuild: true
  msw: true
  protobufjs: true
  sharp: true
  unrs-resolver: true
  vue-demi: true
```

**Evidence / implications:**

- Workspace membership is **path-glob based**, not name based.
- Renaming `apps/ops-page` → `apps/lazuar-ops` keeps the package inside the workspace automatically.
- No edit to `pnpm-workspace.yaml` is required for the four renames.
- `allowBuilds` entries are third-party package names, not app names — **no change**.

### 2.2 Root `package.json`

**File:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/package.json`

| Field | Value | Impact of rename |
|---|---|---|
| `"name"` | `"lazuar"` | Unrelated |
| `"packageManager"` | `"pnpm@11.5.2"` | Unrelated |
| scripts `build` / `dev` / `lint` / `test` / `check-types` | `turbo run <task>` | **No change** — turbo discovers packages by walking workspace; does not list the four apps |
| scripts `docs:dev` / `docs:build` / `docs:preview` | `pnpm --filter lazuar-docs …` | Filters **`lazuar-docs` only** — not one of the four targets |

**Evidence:**

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
  "packageManager": "pnpm@11.5.2"
}
```

**Conclusion:** Root scripts need **no mandatory edits** for the four renames. Optional future scripts like `pnpm --filter lazuar-ops dev` would use the **new package names**.

### 2.3 `turbo.json`

**File:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/turbo.json`

Tasks defined:

| Task | dependsOn | package-name-specific? |
|---|---|---|
| `build` | `["^build"]` | No |
| `test` | `["build"]` | No |
| `lint` | `["^lint"]` | No |
| `check-types` | `["^check-types"]` | No |
| `dev` | none (`cache: false`, `persistent: true`) | No |

Outputs include `.next/**`, `dist/**`, `bin/**`, `obj/**` — path-agnostic patterns.

**Conclusion:**

- Turbo does **not** list packages by name or folder in config.
- Turbo’s internal package identity uses each workspace package’s `package.json` `"name"` + directory when building the graph / cache keys.
- After rename, a full rebuild may miss old remote/local cache keys (expected; not a correctness bug).
- **No `turbo.json` edit required.**

### 2.4 The four apps — `package.json` name fields and scripts

#### `apps/developers-page/package.json`

| Field | Current value |
|---|---|
| `"name"` | `"developers-page"` |
| `"version"` | `"0.1.0"` |
| `"private"` | `true` |
| scripts | `dev` (`next dev -p 3002`), `build`, `start`, `lint` |
| workspace deps | **none** |
| external deps | `@scalar/nextjs-api-reference`, `next@16.2.7`, `react`, `react-dom` |

#### `apps/ops-page/package.json`

| Field | Current value |
|---|---|
| `"name"` | `"ops-page"` |
| `"version"` | `"0.0.0"` |
| `"private"` | `true` |
| `"type"` | `"module"` |
| scripts | `dev` (`vite --port=3003`), `build`, `preview`, `clean`, `lint` |
| workspace deps | `"@repo/api-types-ts": "workspace:^"` |

#### `apps/portal-page/package.json`

| Field | Current value |
|---|---|
| `"name"` | `"portal-page"` |
| `"version"` | `"0.1.0"` |
| `"private"` | `true` |
| scripts | `dev` (`next dev -p 3004`), `build`, `start`, `lint` |
| workspace deps | `"@repo/api-types-ts": "workspace:^"` |

#### `apps/superadmin-page/package.json`

| Field | Current value |
|---|---|
| `"name"` | `"superadmin-page"` |
| `"version"` | `"0.0.0"` |
| `"private"` | `true` |
| `"type"` | `"module"` |
| scripts | `dev` (`vite --port=3005`), `build`, `preview`, `clean`, `lint` |
| workspace deps | `"@repo/api-types-ts": "workspace:^"` |

**Critical observation:** Today, for all four apps, **folder basename == package `"name"`**. That is the monorepo’s informal convention for unscoped apps (`lazuar-api`, `lazuar-docs` also match). After rename, **keep that invariant** (`apps/lazuar-ops` → `"name": "lazuar-ops"`).

### 2.5 Full monorepo package name inventory (for collision check)

| Path | `"name"` | Scoped? |
|---|---|---|
| `/` (root) | `lazuar` | no |
| `apps/developers-page` | `developers-page` | no |
| `apps/ops-page` | `ops-page` | no |
| `apps/portal-page` | `portal-page` | no |
| `apps/superadmin-page` | `superadmin-page` | no |
| `apps/lazuar-api` | `lazuar-api` | no |
| `apps/lazuar-docs` | `lazuar-docs` | no |
| `packages/api-spec` | `@repo/api-spec` | yes |
| `packages/api-types-ts` | `@repo/api-types-ts` | yes |
| `packages/api-types-dotnet` | `@repo/api-types-dotnet` | yes |
| `packages/typescript-config` | `@repo/typescript-config` | yes |
| `packages/eslint-config` | `@repo/eslint-config` | yes |
| `packages/ui` | `@repo/ui` | yes |
| `packages/lhdn-sdk-ts` | `@lazuar/lhdn-sdk` | yes |

**Proposed new names** (`lazuar-spec`, `lazuar-ops`, `lazuar-portal`, `lazuar-admin`):

- Do **not** collide with existing `lazuar-api` / `lazuar-docs`.
- Do **not** collide with `@repo/*` or `@lazuar/*` scopes.
- Align with the unscoped `lazuar-*` app naming already used by API and docs.

**Note:** Docker `container_name` values already use `lazuar-ops` / `lazuar-portal` (compose service vs package name are different namespaces). Package name `lazuar-ops` is fine and does not conflict with container_name.

### 2.6 Workspace dependency graph involving the four apps

**Inbound (other packages depend on these apps):** **none**.

Evidence: repo-wide `workspace:` references are only:

| Consumer | Dependency |
|---|---|
| `apps/ops-page` | `@repo/api-types-ts` (`workspace:^`) |
| `apps/portal-page` | `@repo/api-types-ts` (`workspace:^`) |
| `apps/superadmin-page` | `@repo/api-types-ts` (`workspace:^`) |
| `packages/ui` | `@repo/eslint-config`, `@repo/typescript-config` |

No package lists `developers-page`, `ops-page`, `portal-page`, or `superadmin-page` as a dependency.

**Outbound (apps depend on workspace packages):**

```
developers-page     → (none)
ops-page            → @repo/api-types-ts
portal-page         → @repo/api-types-ts
superadmin-page     → @repo/api-types-ts
```

**Application imports** use `@repo/api-types-ts` (and npm packages). They do **not** import app packages by their own package names (apps are not libraries).

**Implication:** Renaming the four app package names cannot break inter-package TypeScript imports, because no one imports them as packages.

### 2.7 Filters in use today

#### Root / Taskfile / docs (package-name filters)

| Location | Filter | Targets one of the four? |
|---|---|---|
| root `package.json` | `--filter lazuar-docs` | No |
| `Taskfile.yml` | `--filter lazuar-api` | No |
| `Taskfile.yml` | `--filter @repo/api-types-ts` | No |
| `Taskfile.yml` | `--filter @repo/api-types-dotnet` | No |
| `apps/lazuar-docs/docs/reference/openapi.md` | `--filter developers-page dev` | **Yes** — docs-only, package **name** filter |

#### Dockerfiles (path filters — packaging-adjacent but critical)

All four Dockerfiles use **directory filters**, not package names:

| App | Install filter | Build filter |
|---|---|---|
| developers-page | `pnpm install --filter ./apps/developers-page... --filter @repo/api-spec... --frozen-lockfile` | `pnpm --filter ./apps/developers-page build` (+ `@repo/api-spec build`) |
| ops-page | `pnpm install --filter ./apps/ops-page... --frozen-lockfile` | `pnpm --filter ./apps/ops-page build` |
| portal-page | `pnpm install --filter ./apps/portal-page... --frozen-lockfile` | `pnpm --filter ./apps/portal-page build` |
| superadmin-page | `pnpm install --filter ./apps/superadmin-page... --frozen-lockfile` | `pnpm --filter ./apps/superadmin-page build` |

Also path-hardcoded in Dockerfiles:

- `COPY apps/<old>/package.json apps/<old>/`
- `COPY apps/<old> apps/<old>`
- Next standalone runtime paths:
  - `CMD ["node", "apps/developers-page/server.js"]`
  - `CMD ["node", "apps/portal-page/server.js"]`
  - static asset copy destinations under `./apps/<old>/…`

**pnpm filter semantics reminder:**

| Syntax | Matches by |
|---|---|
| `pnpm --filter developers-page` | package.json `"name"` |
| `pnpm --filter ./apps/developers-page` | filesystem path (relative to cwd) |
| `pnpm --filter ./apps/developers-page...` | path + **dependencies** of that package |
| `turbo run build --filter=ops-page` | package.json `"name"` (Turbo) |
| `turbo run build --filter=./apps/ops-page` | path |

Docker currently relies on **path** filters. Docs example relies on **name** filter. Both must stay consistent after rename.

### 2.8 Process manager — path shells (not package names)

**File:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/mprocs-dev.yaml`

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

- Proc keys are free-form labels (can stay or change for UX).
- Shells use **folder paths** — **must** update on folder rename.
- They run `pnpm dev` **inside** the package directory, so package `"name"` is irrelevant for this path.

### 2.9 Nested / orphan lockfiles inside apps

| Path | Observation |
|---|---|
| `apps/developers-page/pnpm-lock.yaml` | Present; local importer `.` only; **not** the monorepo lockfile |
| `apps/portal-page/pnpm-lock.yaml` | Present; same pattern |

**Evidence (developers-page nested lockfile starts as standalone):**

```yaml
lockfileVersion: '9.0'
importers:
  .:
    dependencies:
      next: …
```

**Implications:**

- Authoritative lockfile for the monorepo is **root** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/pnpm-lock.yaml`.
- Nested lockfiles look like leftover scaffolding from create-next-app (or standalone installs). They are **not** referenced by root workspace install or Docker (Docker copies root lockfile only).
- Folder rename moves them with the app.
- **Recommendation (packaging hygiene):** delete nested app lockfiles when renaming, or leave them but never `pnpm install` inside the app directory. Prefer delete to avoid dual-lockfile confusion. (Optional cleanup; not required for rename correctness if nobody uses them.)

### 2.10 CI packaging surface

**`.github/workflows/ci.yml`:**

- `pnpm install --frozen-lockfile` at monorepo root (uses root lockfile importers).
- No filters for the four apps.
- Contracts job regenerates API clients; does not build frontends by name.

**`.github/workflows/ghcr.yml`:**

- Matrix builds by **Dockerfile path**:
  - `apps/portal-page/Dockerfile`
  - `apps/ops-page/Dockerfile`
  - `apps/superadmin-page/Dockerfile`
  - `apps/developers-page/Dockerfile`
- Image names are already `lazuar-hub-portal`, `lazuar-hub-ops`, etc. (product image names ≠ package names).
- Path triggers include `apps/**`, `package.json`, `pnpm-lock.yaml`, `pnpm-workspace.yaml`, `turbo.json` — folder renames still under `apps/**`.

**Packaging takeaway:** CI will keep working **iff** Dockerfiles + lockfile are updated with folder renames. Package `"name"` changes do not directly appear in CI YAML.

### 2.11 `pnpm-lock.yaml` (root) — how packages are keyed

**File:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/pnpm-lock.yaml`  
**lockfileVersion:** `9.0`  
**packageManager:** pnpm 11.x (root declares 11.5.2)

#### Importers section (workspace packages)

Importers are keyed by **relative path from repo root**, not by package `"name"`:

| Importer key (path) | Notes |
|---|---|
| `.` | root: prettier, turbo, typescript |
| `apps/developers-page` | deps: scalar, next 16.2.7, react… |
| `apps/lazuar-api` | empty importer `{}` (dotnet wrapper) |
| `apps/lazuar-docs` | vitepress, vue |
| `apps/ops-page` | large Vite deps; `@repo/api-types-ts: link:../../packages/api-types-ts` |
| `apps/portal-page` | Next 16.2.9 + workspace link to api-types-ts |
| `apps/superadmin-page` | Vite deps + workspace link to api-types-ts |
| `packages/api-spec` | TypeSpec stack |
| `packages/api-types-dotnet` | empty |
| `packages/api-types-ts` | openapi-typescript, typescript |
| `packages/eslint-config` | eslint plugins |
| `packages/lhdn-sdk-ts` | … |
| `packages/typescript-config` | empty |
| `packages/ui` | … |

**Exact line anchors for the four apps (importers):**

- `apps/developers-page:` → line 21
- `apps/ops-page:` → line 72
- `apps/portal-page:` → line 217
- `apps/superadmin-page:` → line 317

#### Workspace links in lockfile

For consumers of `@repo/api-types-ts`:

```yaml
'@repo/api-types-ts':
  specifier: workspace:^
  version: link:../../packages/api-types-ts
```

These links appear under **ops-page**, **portal-page**, **superadmin-page** importers. Relative link path is from each app folder to `packages/api-types-ts`. After folder rename under `apps/`, the relative path `../../packages/api-types-ts` **remains valid** (same depth).

#### packages: snapshot section

The large `packages:` section at the bottom of the lockfile keys **npm registry packages** (e.g. `next@16.2.7`, `react@19.2.4`), **not** the four apps. Apps are private importers only.

**There are no** lockfile entries like `ops-page@0.0.0` in the packages snapshot — apps are not published and not linked as products of other importers.

#### What changes on rename

| Change | Lockfile impact |
|---|---|
| Folder rename only | Importer keys must become `apps/lazuar-ops` etc. Content of dependency trees mostly identical. Relative workspace links stay `link:../../packages/api-types-ts`. |
| Package `name` only | Importer path keys **unchanged**. Rarely needs content changes if nothing depends on the old name. Still safer to reinstall so pnpm rewrites metadata. |
| Folder + name | Importer keys rename; reinstall regenerates cleanly. |

**Do not hand-edit `pnpm-lock.yaml`.** Run root:

```bash
pnpm install
```

(or `pnpm install --no-frozen-lockfile` if CI-style frozen would fail mid-migration). Commit the resulting lockfile.

**`--frozen-lockfile` risk:** Docker and CI use frozen installs. Until lockfile importer keys match new folder paths, Docker builds and CI will fail.

### 2.12 Turbo behavior after rename (detail)

- `turbo run build` walks all workspace packages with a `build` script.
- All four apps define `build` → they remain in the graph under new paths/names.
- `dependsOn: ["^build"]` means “build my workspace dependencies first.”
  - ops/portal/superadmin depend on `@repo/api-types-ts`, which has `"build": "pnpm generate"`.
  - developers-page has no workspace deps; its OpenAPI content is resolved at runtime / Docker-built via separate `@repo/api-spec build`.
- Turbo filter by package name will use **new** names after `"name"` change.
- Local `.turbo` caches under each package may be left behind if directories are `git mv`’d; usually harmless. Cleaning `.turbo` is optional.

### 2.13 Next.js standalone path coupling (packaging-adjacent)

Both Next apps set `output: "standalone"`:

- `apps/developers-page/next.config.ts`
- `apps/portal-page/next.config.ts`

Standalone output embeds the monorepo-relative app path into the server layout. Docker runtime commands currently assume:

- `apps/developers-page/server.js`
- `apps/portal-page/server.js`

After folder rename, standalone will emit under the **new** path (e.g. `apps/lazuar-spec/server.js`). Dockerfiles **must** update COPY/CMD accordingly. This is not a package.json field, but it is a hard packaging path dependency of the monorepo layout.

### 2.14 Runtime path that is *not* package-name based

`apps/developers-page/lib/openapi.ts` resolves specs via:

```ts
path.join(process.cwd(), "../../packages/api-spec/dist")
```

Relative depth `apps/<any-name>` → `packages/` stays valid after rename. **No package name involved.**

---

## 3. Mapping table — folders AND package names

### 3.1 Recommended mapping (folder + name lockstep)

| # | Old folder | New folder | Old package `"name"` | New package `"name"` | Notes |
|---|---|---|---|---|---|
| 1 | `apps/developers-page` | `apps/lazuar-spec` | `developers-page` | `lazuar-spec` | Specs/OpenAPI hub; name matches product “spec” intent |
| 2 | `apps/ops-page` | `apps/lazuar-ops` | `ops-page` | `lazuar-ops` | Aligns with existing container_name `lazuar-ops` |
| 3 | `apps/portal-page` | `apps/lazuar-portal` | `portal-page` | `lazuar-portal` | Aligns with container_name `lazuar-portal` |
| 4 | `apps/superadmin-page` | `apps/lazuar-admin` | `superadmin-page` | `lazuar-admin` | Matches proposed folder; shorter than superadmin |

### 3.2 What does **not** need renaming for packaging

| Artifact | Reason |
|---|---|
| `pnpm-workspace.yaml` globs | Already `apps/*` |
| root turbo scripts | Name-agnostic |
| `@repo/api-types-ts` package name | Consumer only; not renamed |
| Workspace protocol strings `workspace:^` | Still correct |
| App internal npm deps (`next`, `vite`, etc.) | Unrelated |
| GHCR image names (`lazuar-hub-ops`, …) | Already productized; optional later alignment |

### 3.3 Optional renames (outside pure packaging but coupled)

| Artifact | Old | Suggested new | Why |
|---|---|---|---|
| mprocs proc keys | `ops-page` | `lazuar-ops` | DX consistency |
| docker-compose service names | `ops-page` | `lazuar-ops` | Optional; not package.json |
| docker-bake target names | `ops-page` | `lazuar-ops` | Optional; bake target ≠ package name |
| Docs `pnpm --filter developers-page` | name filter | `lazuar-spec` | Correctness after name change |

---

## 4. Folder rename alone vs package `"name"` field

### 4.1 Folder rename alone (keep old package names)

| System | Works? | Detail |
|---|---|---|
| pnpm workspace membership | Yes | Glob still matches |
| `pnpm --filter ./apps/lazuar-ops` | Yes | Path filter |
| `pnpm --filter ops-page` | Yes | Still old name |
| `pnpm --filter lazuar-ops` | No | Name never set |
| Dockerfiles (old paths) | **Broken** until paths updated |
| lockfile importers | **Broken** until regen (keys still `apps/ops-page`) |
| turbo no-filter full run | Yes after lockfile/path fix |
| Mental model / docs | Confusing | folder `lazuar-ops`, package `ops-page` |

### 4.2 Package `"name"` rename alone (keep old folders)

| System | Works? | Detail |
|---|---|---|
| Docker path filters `./apps/ops-page` | Yes | Path unchanged |
| lockfile importer keys | Yes (paths) | Name rarely stored in importer key |
| `pnpm --filter ops-page` | **Broken** | Must use new name |
| Docs filters | **Broken** until updated |
| Consistency with `lazuar-api` / folder names | Poor | folder `ops-page`, name `lazuar-ops` |

### 4.3 Recommendation

**Always do both** for these four apps:

1. `git mv` folder  
2. Edit `"name"` in that app’s `package.json`  
3. Update all path-based packaging tooling (Docker, mprocs)  
4. Update any name-based filters (docs)  
5. `pnpm install` at root → commit lockfile  

Folder-only or name-only renames create long-lived inconsistency and partial breakage.

---

## 5. Turbo / pnpm filter impacts (complete)

### 5.1 Filters that break if **package name** changes and call sites are not updated

| Call site | Current | Required after rename |
|---|---|---|
| `apps/lazuar-docs/docs/reference/openapi.md` | `pnpm --filter developers-page dev` | `pnpm --filter lazuar-spec dev` |
| Any local habit / README examples using old names | `pnpm --filter ops-page dev` etc. | new names |
| Turbo CLI | `turbo run build --filter=portal-page` | `--filter=lazuar-portal` |

Root `package.json` and `Taskfile.yml` do **not** currently filter the four apps by name → low immediate breakage from name change alone.

### 5.2 Filters that break if **folder** changes and call sites are not updated

| Call site | Current path filter / path |
|---|---|
| `apps/developers-page/Dockerfile` | `./apps/developers-page`, COPY/CMD paths |
| `apps/ops-page/Dockerfile` | `./apps/ops-page`, COPY paths |
| `apps/portal-page/Dockerfile` | `./apps/portal-page`, COPY/CMD paths |
| `apps/superadmin-page/Dockerfile` | `./apps/superadmin-page`, COPY paths |
| `mprocs-dev.yaml` | `cd apps/<old>` |
| `docker-bake.hcl` | `dockerfile = "apps/<old>/Dockerfile"` |
| `docker-compose.yml` | `dockerfile: apps/<old>/Dockerfile` |
| `.github/workflows/ghcr.yml` | `dockerfile: apps/<old>/Dockerfile` |

### 5.3 Filters / configs that do **not** break

| Item | Why |
|---|---|
| `pnpm-workspace.yaml` | globs |
| `turbo.json` tasks | generic |
| root `pnpm build` / `pnpm dev` | turbo discovery |
| workspace deps on `@repo/api-types-ts` | package name of dependency unchanged |
| `Taskfile.yml` gen/api filters | different packages |

### 5.4 Suggested post-rename filter cheat sheet

```bash
# By package name (preferred for DX)
pnpm --filter lazuar-spec dev
pnpm --filter lazuar-ops dev
pnpm --filter lazuar-portal dev
pnpm --filter lazuar-admin dev

# By path (Docker-style)
pnpm --filter ./apps/lazuar-spec... build
pnpm --filter ./apps/lazuar-ops... build
pnpm --filter ./apps/lazuar-portal... build
pnpm --filter ./apps/lazuar-admin... build

# Turbo
turbo run build --filter=lazuar-ops
turbo run dev --filter=lazuar-portal
```

---

## 6. Lockfile regeneration notes

### 6.1 What must happen

1. Rename directories (`git mv` preferred to preserve history).
2. Update each app’s `"name"` field.
3. From repo root:

   ```bash
   pnpm install
   ```

4. Verify importers:

   ```bash
   # expect these keys present, old keys absent
   rg '^  apps/lazuar-(spec|ops|portal|admin):' pnpm-lock.yaml
   rg '^  apps/(developers|ops|portal|superadmin)-page:' pnpm-lock.yaml  # should be empty
   ```

5. Commit `pnpm-lock.yaml` in the same PR as renames.

### 6.2 What to expect in the diff

- **Importer keys renamed** (4 path key renames).
- Dependency trees under those importers largely unchanged (same packages/versions).
- Workspace links remain `link:../../packages/api-types-ts` for three apps.
- Snapshot `packages:` section should be mostly unchanged unless install resolves newer metadata; ideally pin/frozen behavior keeps versions stable if `package.json` ranges unchanged.
- Root importer `.` unchanged.
- Nested orphan lockfiles (if not deleted) move with folders but are not updated by root install.

### 6.3 Frozen lockfile failure modes

| Scenario | Symptom |
|---|---|
| Folders renamed, lockfile not updated | `pnpm install --frozen-lockfile` fails: missing importers / outdated lockfile |
| Docker build mid-migration | `RUN pnpm install --filter ./apps/ops-page... --frozen-lockfile` fails (path gone or lock mismatch) |
| Only `"name"` changed, lockfile not reinstalled | Often still works for path filters; may confuse tooling that echoes package names; still reinstall to be safe |

### 6.4 Node_modules / symlinks

- After rename, root `pnpm install` rewrites `node_modules` workspace links.
- Old `apps/<old>/node_modules` goes away with the directory.
- Do not copy `node_modules` manually; always reinstall.
- If using a dirty tree, `rm -rf node_modules apps/*/node_modules packages/*/node_modules && pnpm install` is a valid recovery.

### 6.5 packageManager / Corepack

- Root and Docker pin `pnpm@11.5.2` (or prepare that version).
- Rename does not require changing packageManager version.

### 6.6 CI pnpm version note (pre-existing, not rename-caused)

- Root packageManager: **pnpm@11.5.2**
- `.github/workflows/ci.yml` action-setup: **version: 9**

This is a pre-existing mismatch. Rename work should not “fix” it unless you choose to, but be aware frozen lockfile v9 vs pnpm 11 can be sensitive. Docker uses 11.5.2 via corepack and matches root more closely.

---

## 7. Risks (packaging-focused)

| ID | Risk | Severity | Mitigation |
|---|---|---|---|
| R1 | Lockfile importer keys out of sync with folders | **High** | Always regenerate + commit lockfile in same change as `git mv` |
| R2 | Dockerfiles still reference old paths / path filters | **High** | Update all four Dockerfiles; verify bake/compose/ghcr matrix |
| R3 | Next standalone CMD still points at old `apps/<old>/server.js` | **High** | Update developers + portal Docker runtime stages |
| R4 | Docs / human muscle memory use `pnpm --filter developers-page` | Medium | Update docs filters; announce new names |
| R5 | Folder/name mismatch if only one is renamed | Medium | Enforce lockstep rename checklist |
| R6 | Nested app lockfiles confuse developers into local install | Low | Delete nested `pnpm-lock.yaml` under apps |
| R7 | Turbo cache misses / rebuild cost | Low | Accept; optional clean `.turbo` |
| R8 | Parallel PR conflicts on lockfile | Medium | Land renames as a single focused PR |
| R9 | Service names in compose still `ops-page` while package is `lazuar-ops` | Low | Document dual namespaces; optional later alignment |
| R10 | Accidentally publishing or depending on old names in new scripts | Low | Grep for old names after rename |
| R11 | `mprocs-dev.yaml` still `cd apps/old` | Medium | Update shells when folders move |
| R12 | Someone uses `workspace:` protocol on an app package in future | Low | Apps stay `private: true`; leaves only |

### 7.1 Non-risks (packaging)

- No reverse workspace dependencies to update.
- No turbo pipeline package list to rewrite.
- No root script filters for these four apps.
- Workspace depth for `link:../../packages/...` stays the same under `apps/*`.

---

## 8. Recommended order of operations (packaging)

Execute in this order to minimize broken intermediate states:

### Phase A — Preparation

1. Ensure clean git working tree (or dedicated branch).
2. Confirm current install works: `pnpm install --frozen-lockfile` at root.
3. Inventory greps for packaging touchpoints (paths + package names):

   ```bash
   rg -n 'developers-page|ops-page|portal-page|superadmin-page' \
     package.json pnpm-workspace.yaml turbo.json pnpm-lock.yaml \
     mprocs-dev.yaml docker-bake.hcl docker-compose.yml docker-compose.ghcr.yml \
     .github/workflows \
     apps/*/package.json apps/*/Dockerfile
   ```

### Phase B — Rename packages (atomic as practical)

For each of the four apps:

1. `git mv apps/<old> apps/<new>`
2. Edit `apps/<new>/package.json` → `"name": "<new-package-name>"`
3. Do **not** change dependency versions unless required.

Suggested sequence (any order is fine; group in one commit):

| Step | `git mv` | `"name"` |
|---|---|---|
| B1 | `apps/developers-page` → `apps/lazuar-spec` | `lazuar-spec` |
| B2 | `apps/ops-page` → `apps/lazuar-ops` | `lazuar-ops` |
| B3 | `apps/portal-page` → `apps/lazuar-portal` | `lazuar-portal` |
| B4 | `apps/superadmin-page` → `apps/lazuar-admin` | `lazuar-admin` |

### Phase C — Path-based packaging tooling (same PR)

1. Update all four Dockerfiles (COPY, filters, standalone CMD/COPY).
2. Update `mprocs-dev.yaml` shell `cd` paths (and optionally proc keys).
3. Update `docker-bake.hcl` dockerfile paths (and optionally target names).
4. Update `docker-compose.yml` dockerfile paths.
5. Update `.github/workflows/ghcr.yml` matrix dockerfile paths.
6. Update docs filters that use package names (`--filter developers-page` → `--filter lazuar-spec`).

### Phase D — Lockfile regeneration

1. From repo root: `pnpm install`
2. Confirm new importer keys; no old importer keys.
3. Optional: remove nested `apps/lazuar-spec/pnpm-lock.yaml` and `apps/lazuar-portal/pnpm-lock.yaml` if still present.
4. Optional clean install if weird link issues.

### Phase E — Verification (packaging)

```bash
# Workspace sees new packages by name
pnpm list -r --depth -1

# Filters by new names
pnpm --filter lazuar-spec exec node -p "require('./package.json').name"
pnpm --filter lazuar-ops exec node -p "require('./package.json').name"
pnpm --filter lazuar-portal exec node -p "require('./package.json').name"
pnpm --filter lazuar-admin exec node -p "require('./package.json').name"

# Path filters (Docker style)
pnpm --filter ./apps/lazuar-ops... exec pwd

# Turbo discovery
pnpm exec turbo run build --dry-run
# or
pnpm exec turbo run build --filter=lazuar-ops --dry-run

# Frozen install still works
pnpm install --frozen-lockfile
```

Optional deeper checks (beyond pure packaging, but proves Docker path filters):

```bash
# after Dockerfile path updates
docker build -f apps/lazuar-ops/Dockerfile .
```

### Phase F — Commit strategy

Prefer **one PR** containing:

1. `git mv` of four apps  
2. package.json name edits  
3. packaging path updates (Docker, mprocs, bake, compose, ghcr)  
4. regenerated `pnpm-lock.yaml`  
5. docs filter fixes that would otherwise lie  

Avoid splitting folder rename and lockfile into separate merges on main.

---

## 9. Checklist — files that packaging rename must touch

### Must touch

| File | Change |
|---|---|
| `apps/developers-page/` → `apps/lazuar-spec/` | directory rename |
| `apps/ops-page/` → `apps/lazuar-ops/` | directory rename |
| `apps/portal-page/` → `apps/lazuar-portal/` | directory rename |
| `apps/superadmin-page/` → `apps/lazuar-admin/` | directory rename |
| each new app’s `package.json` | `"name"` field |
| root `pnpm-lock.yaml` | regenerate importers |
| each app `Dockerfile` | paths + path filters + Next CMD |
| `mprocs-dev.yaml` | `cd apps/...` paths |
| `docker-bake.hcl` | dockerfile paths |
| `docker-compose.yml` | dockerfile paths |
| `.github/workflows/ghcr.yml` | matrix dockerfile paths |

### Must not need changes (for packaging)

| File | Why |
|---|---|
| `pnpm-workspace.yaml` | globs |
| `turbo.json` | generic tasks |
| root `package.json` scripts | no filters on these apps |
| `packages/*/package.json` | no deps on apps |
| app workspace dep `"@repo/api-types-ts": "workspace:^"` | still valid |

### Should update (correctness / DX)

| File | Why |
|---|---|
| `apps/lazuar-docs/docs/reference/openapi.md` | `--filter developers-page` |
| Root / app README structure diagrams | document old folder names |
| gap docs under `docs/001-gaps/*` | historical paths (optional bulk replace) |

---

## 10. Evidence appendix (key excerpts)

### 10.1 Root workspace + scripts

- Workspace packages: `"apps/*"`, `"packages/*"`.
- Root filters only `lazuar-docs`.
- Turbo tasks: build, test, lint, check-types, dev — no package lists.

### 10.2 Package names of the four apps

```json
// apps/developers-page/package.json
{ "name": "developers-page" }

// apps/ops-page/package.json
{ "name": "ops-page", "dependencies": { "@repo/api-types-ts": "workspace:^" } }

// apps/portal-page/package.json
{ "name": "portal-page", "dependencies": { "@repo/api-types-ts": "workspace:^" } }

// apps/superadmin-page/package.json
{ "name": "superadmin-page", "dependencies": { "@repo/api-types-ts": "workspace:^" } }
```

### 10.3 Lockfile importers (path keys only)

```
importers:
  apps/developers-page:
  apps/ops-page:
  apps/portal-page:
  apps/superadmin-page:
```

Workspace link example under ops/portal/superadmin:

```
'@repo/api-types-ts':
  specifier: workspace:^
  version: link:../../packages/api-types-ts
```

### 10.4 Docker path filter pattern (ops-page representative)

```dockerfile
COPY apps/ops-page/package.json apps/ops-page/
RUN pnpm install --filter ./apps/ops-page... --frozen-lockfile
COPY apps/ops-page apps/ops-page
RUN pnpm --filter ./apps/ops-page build
```

### 10.5 Next standalone CMD (developers-page)

```dockerfile
CMD ["node", "apps/developers-page/server.js"]
```

### 10.6 Docs name filter

```bash
pnpm --filter developers-page dev
```

(from `apps/lazuar-docs/docs/reference/openapi.md`)

---

## 11. Final packaging judgment

| Decision | Verdict |
|---|---|
| Rename folders as proposed | **Yes** |
| Rename package.json `"name"` to match new folders | **Yes — required for consistent pnpm/turbo name filters and monorepo convention** |
| Edit `pnpm-workspace.yaml` | **No** |
| Edit `turbo.json` | **No** |
| Edit root package.json scripts | **No** (optional later convenience scripts) |
| Regenerate root `pnpm-lock.yaml` | **Yes — mandatory** |
| Update path filters in Docker / mprocs / bake / compose / ghcr | **Yes — mandatory for usable builds/dev** |
| Update reverse workspace deps | **N/A — none exist** |
| Can app TS/JS source stay unchanged for packaging rename? | **Yes** for pure package identity; Docker/runtime paths and docs filters are outside “app feature code” but still required for the rename to ship |

**Bottom line:** In this monorepo, the four apps are **private leaf packages**. Packaging impact is concentrated in (1) each app’s `"name"`, (2) **path-keyed lockfile importers**, and (3) **path-based install/build filters** in Docker and local process config. Turbo and pnpm workspace globs are rename-friendly. Folder rename alone is not enough; package name rename alone is not enough; do both and regenerate the lockfile once.
