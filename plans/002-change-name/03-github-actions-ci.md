# 03 — GitHub Actions / CI-CD rename impact

**Scope:** All files under `.github/`, workflows that build/push/deploy the four renamed apps, path filters, matrix strategies, cache keys, artifact names, GHCR push steps, and the deploy/bake/compose surfaces those workflows invoke.

**Proposed app renames (directory + package name):**

| Current (`apps/…`) | Proposed (`apps/…`) | Current GHCR image short name | Current compose service (prod) | Current container name (prod) |
|---|---|---|---|---|
| `developers-page` | `lazuar-spec` | `lazuar-hub-developers` | `developers` | `hub-developers` |
| `ops-page` | `lazuar-ops` | `lazuar-hub-ops` | `ops` | `hub-ops` |
| `portal-page` | `lazuar-portal` | `lazuar-hub-portal` | `portal` | `hub-portal` |
| `superadmin-page` | `lazuar-admin` | `lazuar-hub-superadmin` | `superadmin` | `hub-superadmin` |

**Important naming split:** The monorepo currently uses **three different naming layers**:

1. **App folder / pnpm package name** — `ops-page`, `portal-page`, `superadmin-page`, `developers-page` (what this rename targets).
2. **GHCR image repository name** — `lazuar-hub-ops`, `lazuar-hub-portal`, `lazuar-hub-superadmin`, `lazuar-hub-developers` (already shortened; **not** equal to folder names).
3. **Prod compose service / container names** — `ops` / `hub-ops`, `portal` / `hub-portal`, `superadmin` / `hub-superadmin`, `developers` / `hub-developers`.

The proposed renames align folder names with product branding (`lazuar-ops`, etc.) but **do not automatically equal** current GHCR image names (`lazuar-hub-ops`) or compose service names (`ops`). CI can rename Dockerfile paths without renaming published images.

---

## 1. Full workflow inventory under `.github/`

### 1.1 Directory tree

```
.github/
└── workflows/
    ├── ci.yml      # PR + main CI (contracts drift + .NET tests)
    └── ghcr.yml    # Build/push 5 images to GHCR + SSH deploy hub VPS
```

**Not present (confirmed by inventory):**

- No `.github/actions/**` composite actions.
- No reusable workflows (`workflow_call`).
- No Dependabot / Renovate config under `.github/`.
- No CODEOWNERS under `.github/`.
- No path-filter action usage (`dorny/paths-filter`, turbo affected, etc.).
- No frontend lint/test/build job in CI for the four apps.
- No release / tag / npm publish workflows.
- No matrix jobs that enumerate app package names outside Docker build.

**Only two workflow files exist. All CI/CD rename risk for GitHub Actions is concentrated in `ghcr.yml` (Dockerfile paths + optional image-name policy). `ci.yml` has zero direct references to the four app folder names.**

---

## 2. Workflow: `.github/workflows/ci.yml`

### 2.1 Identity

| Field | Value |
|---|---|
| File | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/.github/workflows/ci.yml` |
| `name` | `CI` |
| Triggers | `pull_request` → `main`; `push` → `main` |
| Path filters | **None** (runs on every PR/push to main) |
| Jobs | `contracts`, `dotnet` |
| Permissions | default (none explicit) |
| Concurrency | none |
| Secrets | none referenced |
| Artifacts | none uploaded |
| Cache | none explicit (setup-node/pnpm may use defaults; not keyed on app names) |
| Matrix | none |

### 2.2 Job: `contracts` (lines 11–47)

Purpose: force `task gen --force` and fail if generated OpenAPI clients are dirty.

Referenced paths (must stay green after any monorepo rename):

- `packages/api-types-ts/src`
- `packages/api-types-dotnet/Generated`
- `packages/api-types-dotnet/Lazuar.ApiContracts.cs`
- `packages/lhdn-sdk-ts/src/generated`
- `packages/lhdn-sdk-dotnet/src/Generated`

**References to the four apps:** **none**.

**Rename impact:** **None** for app folder renames, as long as:

- `task gen` / TypeSpec / api-spec package paths stay valid.
- Workspace install still works (`pnpm install --frozen-lockfile` at monorepo root).

**Indirect risk only:** if `package.json` / workspace graph breaks because pnpm package `name` fields and lockfile importers still say `apps/ops-page` etc., then `pnpm install --frozen-lockfile` in this job fails. That is a monorepo/lockfile concern, not a workflow-YAML concern.

### 2.3 Job: `dotnet` (lines 49–87)

Purpose: restore/build/test `apps/lazuar-api` against Postgres service.

Working directory: `apps/lazuar-api`.

**References to the four apps:** **none**.

**Rename impact:** **None**.

### 2.4 Exact lines needing change in `ci.yml`

**None for the four app renames.**

Optional future hardening (out of scope for pure rename, but noted):

- Add path filters so frontend-only renames do not re-run full .NET suite (cosmetic CI cost, not correctness).
- Add frontend build matrix later — would then need the new folder names.

---

## 3. Workflow: `.github/workflows/ghcr.yml`

### 3.1 Identity

| Field | Value |
|---|---|
| File | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/.github/workflows/ghcr.yml` |
| `name` | `GHCR + deploy` |
| Triggers | `push` to `main` with path filters; `workflow_dispatch` with optional version pin / skip_build |
| Permissions | `contents: read`, `packages: write` |
| Concurrency | `group: lazuar-hub-cd-${{ github.ref }}`, `cancel-in-progress: false` |
| Env | `REGISTRY: ghcr.io`, `IMAGE_PREFIX: ghcr.io/${{ github.repository_owner }}` |
| Jobs | `build-and-push` (matrix), `deploy` (needs build) |

### 3.2 Header comment (stale / incomplete)

Lines 1–12:

```yaml
# Build 4 hub images → GHCR, then SSH-deploy dedicated hub VPS.
#
# Images (flat names):
#   ghcr.io/proxeon/lazuar-hub-api|ops|portal|superadmin
#   tags: latest + sha-<short>
```

**Issues relative to actual matrix:**

- Comment says **4** images; matrix actually builds **5** (`api`, `portal`, `ops`, `superadmin`, **`developers`**).
- Comment omits `lazuar-hub-developers`.
- Owner in comment is hardcoded `proxeon`; runtime uses `github.repository_owner` (correct if repo owner is `proxeon`).

**Rename impact:** comment-only; should be updated for accuracy when touching the file.

### 3.3 Path filters (`on.push.paths`)

Lines 19–29:

```yaml
paths:
  - "apps/**"
  - "packages/**"
  - "package.json"
  - "pnpm-lock.yaml"
  - "pnpm-workspace.yaml"
  - "turbo.json"
  - "docker-bake.hcl"
  - "deploy/**"
  - "scripts/remote-deploy.sh"
  - ".github/workflows/ghcr.yml"
```

**Implications for rename:**

| Filter | Affected by folder rename? | Notes |
|---|---|---|
| `apps/**` | **No functional break** | Still matches `apps/lazuar-ops/**` etc. after rename. Broad glob, not per-app. |
| Per-app globs like `apps/ops-page/**` | **N/A — not used** | There are **no** per-app path filters today. |
| `packages/**` | No | Unrelated to four app renames. |
| `docker-bake.hcl` | Yes if bake targets renamed | Bake file will change as part of rename; still listed. |
| `deploy/**` | Only if compose/image policy changes | Deploy uses image names, not app folders. |
| `scripts/remote-deploy.sh` | Only if container health names change | Health checks use `hub-ops` etc., not `ops-page`. |
| `.github/workflows/ghcr.yml` | Yes when workflow edited | Self-trigger. |

**Path-filter migration requirement:** **None required** for correctness under current broad `apps/**` filter.

**Optional improvement (recommended later):** add explicit per-app paths after rename for documentation and future monorepo growth:

```yaml
- "apps/lazuar-ops/**"
- "apps/lazuar-portal/**"
- "apps/lazuar-admin/**"
- "apps/lazuar-spec/**"
- "apps/lazuar-api/**"
```

This is optional because `apps/**` already covers them. If you ever switch to **per-image path filtering** (build only changed apps), those exact globs **must** use the new names or CI will silently stop building an app.

### 3.4 Concurrency group

Line 47:

```yaml
group: lazuar-hub-cd-${{ github.ref }}
```

Uses product name `lazuar-hub`, **not** any of the four app folder names.

**Rename impact:** **None** for app renames. Only change if the product/repo rename (`lazuar-hub` → something else) is in scope of a broader rebrand.

### 3.5 Global env

Lines 50–52:

```yaml
env:
  REGISTRY: ghcr.io
  IMAGE_PREFIX: ghcr.io/${{ github.repository_owner }}
```

**No hardcoded app names.** Final image is `${IMAGE_PREFIX}/${{ matrix.name }}`.

### 3.6 Job: `build-and-push` — matrix strategy (CRITICAL)

Lines 55–86:

```yaml
jobs:
  build-and-push:
    name: Build ${{ matrix.name }}
    if: ${{ !(github.event_name == 'workflow_dispatch' && inputs.skip_build) }}
    runs-on: ubuntu-latest
    timeout-minutes: 60
    strategy:
      fail-fast: true
      matrix:
        include:
          - name: lazuar-hub-api
            dockerfile: apps/lazuar-api/Dockerfile
            build_args: ""
          - name: lazuar-hub-portal
            dockerfile: apps/portal-page/Dockerfile
            build_args: |
              NEXT_PUBLIC_API_URL=https://hub.lazuar.com/api/v1
              NEXT_BASE_PATH=/portal
          - name: lazuar-hub-ops
            dockerfile: apps/ops-page/Dockerfile
            build_args: |
              VITE_API_URL=https://hub.lazuar.com/api/v1
              VITE_PORTAL_URL=https://hub.lazuar.com/portal
              VITE_BASE_PATH=/
          - name: lazuar-hub-superadmin
            dockerfile: apps/superadmin-page/Dockerfile
            build_args: |
              VITE_API_URL=https://hub.lazuar.com/api/v1
              VITE_BASE_PATH=/admin/
          - name: lazuar-hub-developers
            dockerfile: apps/developers-page/Dockerfile
            build_args: |
              NEXT_BASE_PATH=/docs
```

#### 3.6.1 Matrix fields that **must** change when folders rename

| Matrix entry `name` (GHCR image) | Current `dockerfile` path | Required path after rename | `build_args` content change? |
|---|---|---|---|
| `lazuar-hub-api` | `apps/lazuar-api/Dockerfile` | unchanged (out of scope) | no |
| `lazuar-hub-portal` | `apps/portal-page/Dockerfile` | **`apps/lazuar-portal/Dockerfile`** | no (URLs/basePath stay) |
| `lazuar-hub-ops` | `apps/ops-page/Dockerfile` | **`apps/lazuar-ops/Dockerfile`** | no |
| `lazuar-hub-superadmin` | `apps/superadmin-page/Dockerfile` | **`apps/lazuar-admin/Dockerfile`** | no |
| `lazuar-hub-developers` | `apps/developers-page/Dockerfile` | **`apps/lazuar-spec/Dockerfile`** | no |

#### 3.6.2 Exact line-level edits required in `ghcr.yml`

| Line(s) | Current | Required after folder rename |
|---|---|---|
| 68 | `dockerfile: apps/portal-page/Dockerfile` | `dockerfile: apps/lazuar-portal/Dockerfile` |
| 73 | `dockerfile: apps/ops-page/Dockerfile` | `dockerfile: apps/lazuar-ops/Dockerfile` |
| 79 | `dockerfile: apps/superadmin-page/Dockerfile` | `dockerfile: apps/lazuar-admin/Dockerfile` |
| 84 | `dockerfile: apps/developers-page/Dockerfile` | `dockerfile: apps/lazuar-spec/Dockerfile` |

#### 3.6.3 Matrix `name` (job display + image + cache) — decision required

`matrix.name` is used for:

1. **Job display name:** `Build ${{ matrix.name }}` (line 56)
2. **GHCR image:** `${{ env.IMAGE_PREFIX }}/${{ matrix.name }}` (line 103)
3. **GHA cache scope:** `scope=${{ matrix.name }}` (lines 119–120)

Current published images (assuming owner `proxeon`):

- `ghcr.io/proxeon/lazuar-hub-api`
- `ghcr.io/proxeon/lazuar-hub-portal`
- `ghcr.io/proxeon/lazuar-hub-ops`
- `ghcr.io/proxeon/lazuar-hub-superadmin`
- `ghcr.io/proxeon/lazuar-hub-developers`

**Recommendation for minimal-risk rename:** **keep `matrix.name` / GHCR image names unchanged** when only renaming app folders. Only change `dockerfile:` paths.

**If branding wants image names to match proposed app names**, map carefully:

| Current image | Possible new image | Notes |
|---|---|---|
| `lazuar-hub-ops` | `lazuar-ops` | Matches proposed app name; **breaking** for prod compose pulls |
| `lazuar-hub-portal` | `lazuar-portal` | Same |
| `lazuar-hub-superadmin` | `lazuar-admin` | Matches proposed `lazuar-admin`; **not** a simple suffix swap |
| `lazuar-hub-developers` | `lazuar-spec` | Matches proposed `lazuar-spec`; **semantic change** (developers → spec) |
| `lazuar-hub-api` | (unchanged or `lazuar-api`) | Out of app rename scope; note inconsistency if others drop `-hub-` |

**Do not partially rename images.** Prod `deploy/prod/docker-compose.yml` and local `docker-compose.ghcr.yml` hardcode the old image strings. Partial rename → deploy pulls old images while CI pushes new ones (or vice versa) → **production runs stale containers with green CI**.

### 3.7 Build / push steps (lines 87–121)

```yaml
steps:
  - uses: actions/checkout@v4
  - uses: docker/setup-buildx-action@v3
  - name: Log in to GHCR
    uses: docker/login-action@v3
    with:
      registry: ${{ env.REGISTRY }}
      username: ${{ secrets.GHCR_USERNAME || github.actor }}
      password: ${{ secrets.GHCR_TOKEN || secrets.CR_PAT || secrets.GITHUB_TOKEN }}
  - name: Docker metadata
    id: meta
    uses: docker/metadata-action@v5
    with:
      images: ${{ env.IMAGE_PREFIX }}/${{ matrix.name }}
      tags: |
        type=raw,value=latest,enable={{is_default_branch}}
        type=sha,prefix=sha-,format=short
        type=raw,value=${{ github.sha }},enable=true
  - name: Build and push
    uses: docker/build-push-action@v6
    with:
      context: .
      file: ${{ matrix.dockerfile }}
      platforms: linux/amd64
      push: true
      provenance: false
      tags: ${{ steps.meta.outputs.tags }}
      labels: ${{ steps.meta.outputs.labels }}
      cache-from: type=gha,scope=${{ matrix.name }}
      cache-to: type=gha,mode=max,scope=${{ matrix.name }}
      build-args: ${{ matrix.build_args }}
```

#### 3.7.1 Tags produced

For each matrix image:

- `latest` (default branch only)
- `sha-<7-char short SHA>` (used by deploy VERSION pin)
- full `${{ github.sha }}` raw tag

#### 3.7.2 Cache keys

```
cache-from: type=gha,scope=${{ matrix.name }}
cache-to:   type=gha,mode=max,scope=${{ matrix.name }}
```

Scoped by **image name**, not Dockerfile path.

| Scenario | Cache impact |
|---|---|
| Rename folder only; keep `matrix.name` | **Cache preserved** |
| Rename `matrix.name` (image rename) | **Cold cache** for that image (first builds slower) |
| Partial matrix rename | Some apps warm, some cold; easy to misread failures as “cache bugs” |

#### 3.7.3 Artifacts

No `actions/upload-artifact` usage. The only “artifacts” are **GHCR images**. No artifact name strings reference `ops-page` etc.

### 3.8 Job: `deploy` (lines 123–212)

#### 3.8.1 Condition / dependency

```yaml
deploy:
  name: Deploy hub VPS
  needs: [build-and-push]
  if: |
    always() &&
    github.ref == 'refs/heads/main' &&
    (needs.build-and-push.result == 'success' || (github.event_name == 'workflow_dispatch' && inputs.skip_build))
```

No app-name strings.

#### 3.8.2 VERSION resolution (lines 135–145)

Pins `sha-${GITHUB_SHA:0:7}` unless `workflow_dispatch` provides `inputs.version`. Matches image tags from metadata-action `type=sha,prefix=sha-`.

**Rename impact:** **None**.

#### 3.8.3 Rsync / remote paths (lines 161–171)

```bash
rsync ... deploy/prod/ → ${DEST}:/root/lazuar-hub-prod/
rsync ... scripts/remote-deploy.sh → ${DEST}:/root/lazuar-hub-remote-deploy.sh
```

Uses **hub** product path names, not app folder names.

**Rename impact for four apps:** **None** unless product path rebrand is separate.

#### 3.8.4 Secrets / env used by deploy

| Secret / env | Hardcodes app folder names? | Notes |
|---|---|---|
| `SSH_PRIVATE_KEY` | No | |
| `SSH_HOST` | No | |
| `SSH_USER` | No | |
| `HUB_ENV_FILE` | Unlikely / content-dependent | Multiline `.env` body written to server. `env.example` has no `ops-page` strings; only URLs and connection strings. **Audit live secret contents** outside repo. |
| `GHCR_PULL_TOKEN` | No | |
| `GHCR_TOKEN` / `CR_PAT` | No | |
| `GHCR_USERNAME` | No | Fallback actor string `allaboutevemirolive` on line 200 if username empty (org-specific, not app-name). |
| `GITHUB_TOKEN` | No | Used for package write when PAT absent. |

**No workflow secret *names* encode the four app renames.** Risk is only if someone stored path-specific values inside `HUB_ENV_FILE` (not evidenced in `env.example`).

#### 3.8.5 Server-side script invocation (lines 204–212)

```bash
ssh ... "export VERSION='${VERSION}' HEALTH_TIMEOUT=180; /root/lazuar-hub-remote-deploy.sh"
```

Deploy correctness depends on **synced** `deploy/prod/docker-compose.yml` + `scripts/remote-deploy.sh` on the VPS — covered in §5–§6.

### 3.9 Complete `ghcr.yml` change checklist

**Must change (folder rename):**

1. Line 68: `apps/portal-page/Dockerfile` → `apps/lazuar-portal/Dockerfile`
2. Line 73: `apps/ops-page/Dockerfile` → `apps/lazuar-ops/Dockerfile`
3. Line 79: `apps/superadmin-page/Dockerfile` → `apps/lazuar-admin/Dockerfile`
4. Line 84: `apps/developers-page/Dockerfile` → `apps/lazuar-spec/Dockerfile`

**Should change (docs accuracy):**

5. Header comment lines 1–5: mention 5 images including developers/spec; reflect final image naming policy.

**Optional / separate decision (image rebrand):**

6. `matrix.name` values (`lazuar-hub-ops` → `lazuar-ops`, etc.) — only with coordinated compose + bake + pull consumers.
7. Concurrency group `lazuar-hub-cd-…` if product rename.
8. Build-arg public URLs if domains/paths change (they do **not** change solely due to folder rename).

**Must NOT leave half-done:**

- Dockerfile path updated in workflow but Dockerfiles still `COPY apps/ops-page` → **build fails**.
- Workflow path updated and Dockerfiles updated but GHCR image names changed without compose → **deploy pulls wrong/missing tags**.
- Image names changed in workflow but not in `deploy/prod/docker-compose.yml` → same.

---

## 4. Dockerfiles consumed by CI (build context paths)

GHCR builds with `context: .` and `file: apps/<app>/Dockerfile`. Each frontend Dockerfile **hardcodes the old folder path** in multiple `COPY` / `pnpm --filter` / runtime `CMD` lines. Folder rename without Dockerfile edits **breaks GHCR builds even if workflow YAML is updated**.

These are not under `.github/`, but they are **on the critical path of the GHCR workflow**.

### 4.1 `apps/ops-page/Dockerfile` → becomes `apps/lazuar-ops/Dockerfile`

Exact path occurrences today:

| Line | Content |
|---|---|
| 13 | `COPY apps/ops-page/package.json apps/ops-page/` |
| 15 | `RUN pnpm install --filter ./apps/ops-page... --frozen-lockfile` |
| 19 | `COPY apps/ops-page apps/ops-page` |
| 28 | `RUN pnpm --filter ./apps/ops-page build` |
| 36 | `COPY --from=build ... /app/apps/ops-page/dist ./dist` |

All `ops-page` path segments → `lazuar-ops`. Package `name` in `package.json` should match for filter-by-name usage elsewhere; Dockerfile filters by path (`./apps/...`), so path consistency is mandatory.

### 4.2 `apps/portal-page/Dockerfile` → `apps/lazuar-portal/Dockerfile`

| Line | Content |
|---|---|
| 13 | `COPY apps/portal-page/package.json apps/portal-page/` |
| 15 | `RUN pnpm install --filter ./apps/portal-page...` |
| 19 | `COPY apps/portal-page apps/portal-page` |
| 27 | `RUN pnpm --filter ./apps/portal-page build` |
| 43 | `COPY ... /app/apps/portal-page/.next/standalone ./` |
| 44 | `COPY ... /app/apps/portal-page/.next/static ./apps/portal-page/.next/static` |
| 45 | `COPY ... /app/apps/portal-page/public ./apps/portal-page/public` |
| 52 | `CMD ["node", "apps/portal-page/server.js"]` |

**Critical Next.js standalone detail:** With `output: "standalone"` (see `apps/portal-page/next.config.ts`), the server path inside the image embeds the monorepo app directory name (`apps/portal-page/server.js`). After rename, **static asset paths and CMD must all use `apps/lazuar-portal/...` consistently** or the container starts with missing assets / wrong entrypoint.

### 4.3 `apps/superadmin-page/Dockerfile` → `apps/lazuar-admin/Dockerfile`

| Line | Content |
|---|---|
| 13 | `COPY apps/superadmin-page/package.json apps/superadmin-page/` |
| 15 | `RUN pnpm install --filter ./apps/superadmin-page...` |
| 19 | `COPY apps/superadmin-page apps/superadmin-page` |
| 26 | `RUN pnpm --filter ./apps/superadmin-page build` |
| 34 | `COPY ... /app/apps/superadmin-page/dist ./dist` |

### 4.4 `apps/developers-page/Dockerfile` → `apps/lazuar-spec/Dockerfile`

| Line | Content |
|---|---|
| 11 | `COPY apps/developers-page/package.json apps/developers-page/` |
| 13 | `RUN pnpm install --filter ./apps/developers-page... --filter @repo/api-spec...` |
| 17 | `COPY apps/developers-page apps/developers-page` |
| 26 | `RUN pnpm --filter ./apps/developers-page build` |
| 39–41 | standalone/static/public under `apps/developers-page` |
| 50 | `CMD ["node", "apps/developers-page/server.js"]` |

Same Next.js standalone path embedding as portal.

### 4.5 Dockerfile vs workflow coupling summary

| Failure mode | Symptom in GHCR job |
|---|---|
| Workflow path not updated | `docker/build-push-action` cannot find Dockerfile |
| Workflow path updated; Dockerfile internal paths stale | `COPY apps/ops-page...` fails (file not found) |
| Dockerfile path segments updated; package.json `name` stale | Path filters still work; name-based `pnpm --filter ops-page` elsewhere breaks |
| CMD path not updated for Next apps | Image builds, container exits / healthcheck fails |
| Static path not updated for Next apps | HTML 200, assets 404 under basePath |

---

## 5. Deploy surfaces invoked by `ghcr.yml` (not under `.github/`, but CD-critical)

The deploy job does **not** reference app folders. It rsyncs `deploy/prod/` and runs `scripts/remote-deploy.sh`. These use **image names** and **compose service/container names**.

### 5.1 `deploy/prod/docker-compose.yml`

Image lines (today):

| Lines | Service | Image | Container |
|---|---|---|---|
| 40–42 | `api` | `ghcr.io/proxeon/lazuar-hub-api:${VERSION:-latest}` | `hub-api` |
| 61–63 | `ops` | `ghcr.io/proxeon/lazuar-hub-ops:${VERSION:-latest}` | `hub-ops` |
| 71–73 | `portal` | `ghcr.io/proxeon/lazuar-hub-portal:${VERSION:-latest}` | `hub-portal` |
| 88–90 | `superadmin` | `ghcr.io/proxeon/lazuar-hub-superadmin:${VERSION:-latest}` | `hub-superadmin` |
| 98–100 | `developers` | `ghcr.io/proxeon/lazuar-hub-developers:${VERSION:-latest}` | `hub-developers` |

**Folder rename only:** **no change required** in this file (service names and images independent of `apps/*` folders).

**If GHCR `matrix.name` / image rebrand is chosen:** every `image:` line above **must** change in the same commit as the workflow, and the next deploy must pull the new names (or dual-tag old+new during transition).

### 5.2 `scripts/remote-deploy.sh`

Health waits (lines 71–76):

```bash
wait_healthy hub-api 180
wait_healthy hub-ops 60
wait_healthy hub-portal 90
wait_healthy hub-superadmin 60
wait_healthy hub-developers 90
wait_healthy hub-caddy 60
```

Smoke paths (lines 83–92): `/health`, `/`, `/portal`, `/docs` — **URL paths**, not app folder names.

**Folder rename only:** no change.

**If container_name values change as part of rebrand:** update wait_healthy list in lockstep or deploy health-gate fails while containers are actually fine.

### 5.3 `deploy/prod/Caddyfile`

Upstream service names: `api`, `portal`, `developers`, `superadmin`, `ops` — compose DNS names, **not** app folders.

**Folder rename only:** no change.

### 5.4 `deploy/prod/env.example` / secret `HUB_ENV_FILE`

No `ops-page` / `portal-page` / `superadmin-page` / `developers-page` strings.

Contains public URLs (`hub.lazuar.com/portal`, etc.) and image pin `VERSION=latest`.

**Folder rename only:** no change.

### 5.5 `deploy/prod/README.md`

References `.github/workflows/ghcr.yml` and path routing table (ops/portal/docs/admin). No Dockerfile folder paths for the four apps beyond conceptual service names.

---

## 6. Local / manual CD parity (CI-adjacent, used by humans + Taskfile)

These are **not** executed by GitHub Actions today, but they build/push the **same images** and will diverge dangerously if only `.github` is updated.

### 6.1 `docker-bake.hcl`

GHCR workflow **does not** call bake; Taskfile does (`task docker:build`, `task docker:push`).

Bake targets named after **current app folders**:

| Lines | Target name | `dockerfile` | Image tags |
|---|---|---|---|
| 48–49 | group `default` targets list | includes `portal-page`, `ops-page`, `superadmin-page`, `developers-page` | — |
| 76–91 | `portal-page` | `apps/portal-page/Dockerfile` | `lazuar-hub-portal` |
| 93–109 | `ops-page` | `apps/ops-page/Dockerfile` | `lazuar-hub-ops` |
| 111–126 | `superadmin-page` | `apps/superadmin-page/Dockerfile` | `lazuar-hub-superadmin` |
| 128–142 | `developers-page` | `apps/developers-page/Dockerfile` | `lazuar-hub-developers` |

**Required for folder rename (bake target names can stay or change):**

- Every `dockerfile = "apps/<old>/Dockerfile"` → new path.
- Group `targets = [...]` must list renamed target identifiers if target blocks are renamed.
- Image tags (`lazuar-hub-*`) independent of folder rename unless image rebrand policy applies.

**Risk of partial rename:** GHCR workflow fixed, bake left stale → local `task docker:push` still builds old paths and fails; operators may force-push wrong assumptions.

### 6.2 `docker-compose.yml` (local build profile `full`)

Service keys and dockerfile paths:

| Lines | Service key | dockerfile | image tag |
|---|---|---|---|
| 49–64 | `ops-page` | `apps/ops-page/Dockerfile` | `ghcr.io/proxeon/lazuar-hub-ops:local` |
| 66–83 | `portal-page` | `apps/portal-page/Dockerfile` | `ghcr.io/proxeon/lazuar-hub-portal:local` |
| 85–99 | `superadmin-page` | `apps/superadmin-page/Dockerfile` | `ghcr.io/proxeon/lazuar-hub-superadmin:local` |

Note: **developers-page is not in local `docker-compose.yml`** (gap already present). GHCR/prod **does** deploy developers.

**Folder rename:** update service keys (optional for DX), dockerfile paths (required), container names optional (`lazuar-ops` already used as `container_name` for ops).

### 6.3 `docker-compose.ghcr.yml` (local pull of GHCR images)

Service keys: `ops-page`, `portal-page`, `superadmin-page` (no developers service here either).

Images: `lazuar-hub-ops|portal|superadmin` — same as prod short names.

**Folder rename only:** service key renames are cosmetic; image strings only change if GHCR image rebrand.

### 6.4 `Taskfile.yml` docker tasks

| Task | Role | App-folder strings? |
|---|---|---|
| `docker:builder` | create buildx builder | no |
| `docker:build` | `docker buildx bake --load` | comment mentions “api, portal, ops, superadmin” (omits developers) |
| `docker:build:api` | bake target `api` | no |
| `docker:login:ghcr` | docker login | no |
| `docker:push` | bake `--push` all default targets | image prefix comment `lazuar-hub/*` |
| `docker:push:api` | bake `api --push` | `lazuar-hub-api` in echo |
| `docker:up:ghcr` | compose.ghcr pull/up | no app folders |
| `docker:up:full` | local full profile | no app folders |

Bake target names are the coupling surface; Taskfile itself has **no** `apps/ops-page` strings.

### 6.5 `mprocs-dev.yaml`

Dev process manager (not CI):

```yaml
developers-page: cd apps/developers-page && pnpm dev
ops-page:        cd apps/ops-page && pnpm dev
superadmin-page: cd apps/superadmin-page && pnpm dev
portal-page:     cd apps/portal-page && pnpm dev
```

Must update when folders rename; **not** used by GitHub Actions.

---

## 7. Secrets and environment variables inventory

### 7.1 Referenced in workflows

| Name | Where | App-folder hardcode? |
|---|---|---|
| `GHCR_USERNAME` | ghcr.yml login + server login | No |
| `GHCR_TOKEN` | ghcr.yml login | No |
| `CR_PAT` | ghcr.yml login fallback | No |
| `GITHUB_TOKEN` | ghcr.yml login last fallback | No |
| `GHCR_PULL_TOKEN` | server docker login | No |
| `SSH_PRIVATE_KEY` | deploy SSH agent | No |
| `SSH_HOST` | deploy steps | No |
| `SSH_USER` | deploy steps | No |
| `HUB_ENV_FILE` | inject server `.env` | Content audit recommended; example has no app folder names |
| `inputs.version` / `inputs.skip_build` | workflow_dispatch | No |

### 7.2 Build-args baked into images (not secrets, but env)

| App (current) | Build-arg | Value in matrix | Depends on folder name? |
|---|---|---|---|
| portal-page | `NEXT_PUBLIC_API_URL` | `https://hub.lazuar.com/api/v1` | No |
| portal-page | `NEXT_BASE_PATH` | `/portal` | No |
| ops-page | `VITE_API_URL` | `https://hub.lazuar.com/api/v1` | No |
| ops-page | `VITE_PORTAL_URL` | `https://hub.lazuar.com/portal` | No |
| ops-page | `VITE_BASE_PATH` | `/` | No |
| superadmin-page | `VITE_API_URL` | `https://hub.lazuar.com/api/v1` | No |
| superadmin-page | `VITE_BASE_PATH` | `/admin/` | No |
| developers-page | `NEXT_BASE_PATH` | `/docs` | No |

Public path prefixes (`/portal`, `/docs`, `/admin/`) are **product URL design**, independent of `apps/*` folder renames. Renaming `portal-page` → `lazuar-portal` does **not** require changing `NEXT_BASE_PATH=/portal`.

### 7.3 Runtime env in prod compose

- Portal: `API_URL`, `NEXT_PUBLIC_API_URL`
- Developers: `OPENAPI_SPEC_ROOT=/app/openapi-specs`
- API: full `.env` via `env_file`

None embed `apps/ops-page` paths.

---

## 8. Path filter implications (deep dive)

### 8.1 Current behavior

`ghcr.yml` triggers on **any** change under `apps/**`. Renaming four folders in one PR:

1. Triggers GHCR workflow (paths under `apps/**` change).
2. Builds **all five** matrix images (no change-detection per app).
3. Deploys entire hub stack with new `sha-…` VERSION.

`ci.yml` has **no** path filters → always runs contracts + dotnet on PR/push to main, even for pure frontend renames.

### 8.2 What breaks if someone later adds per-app filters without updating names

Hypothetical future filter:

```yaml
paths:
  - "apps/ops-page/**"
```

After rename to `apps/lazuar-ops/**`, this filter **never matches** → ops image never rebuilds → production silently freezes on old ops image while other apps advance.

**Mitigation rule:** any introduction of per-app path filters **must** use the new names in the same PR as the rename (or never use old names again).

### 8.3 `docker-bake.hcl` in path filters

Listed explicitly. Renaming bake targets/paths will still trigger GHCR because that file is listed — good, because GHCR uses Dockerfiles not bake, but any bake fix that coincides with Dockerfile path fixes will still run the deploy pipeline.

### 8.4 What does **not** need path-filter updates for this rename

- `packages/**` — unchanged unless package renames elsewhere.
- `deploy/**` — only if image/service rebrand.
- `scripts/remote-deploy.sh` — only if health container names change.
- `pnpm-workspace.yaml` — uses `apps/*` glob; **folder renames auto-included**; package `name` fields still need updates in each `package.json` + lockfile.

---

## 9. Matrix job names, cache, and artifact semantics

### 9.1 Job names (Actions UI)

`Build lazuar-hub-portal`, `Build lazuar-hub-ops`, etc.

After folder rename only: **UI names unchanged** (image-based).

After image rebrand: UI names change; historical run names remain old (cosmetic).

### 9.2 Cache scopes

`scope=lazuar-hub-ops` etc.

Folder rename only: warm cache retained (good).

Image rename: cold start (acceptable one-time cost).

### 9.3 Artifacts / packages on GHCR

Packages are GitHub Packages container images under the repository owner.

Renaming images creates **new package repositories** on GHCR. Old packages remain unless deleted. Consumers (VPS compose) must be pointed at new names.

There is **no** workflow step that deletes old packages.

### 9.4 Fail-fast matrix

`strategy.fail-fast: true` — one Dockerfile path miss fails the whole matrix and **blocks deploy** (deploy needs success unless `skip_build`). This is protective during rename: you will not deploy a half-built set if one of the four Dockerfiles still points at old paths… **unless** you use `workflow_dispatch` with `skip_build: true`, which deploys existing VERSION without rebuilding — dangerous mid-rename if compose already expects new image names.

---

## 10. Risks of partial rename (CI/CD-focused)

### 10.1 Severity matrix

| Partial state | CI (`ci.yml`) | GHCR build | Deploy | Production user impact |
|---|---|---|---|---|
| Folders renamed; `ghcr.yml` dockerfile paths not updated | green (no frontend build) | **red** (missing Dockerfile) | skipped / previous | no new deploys |
| `ghcr.yml` updated; Dockerfiles still COPY old paths | green | **red** (COPY fail) | skipped | no new deploys |
| Workflow + Dockerfiles updated; package.json `name`/lockfile stale | **red** on `pnpm install` if workspace broken; contracts job fails | red if install fails in image | skipped | no new deploys |
| Image `matrix.name` renamed; compose still old images | green | green (new packages) | **pulls old images** or fails if tags missing | **stale or broken prod** |
| Compose image strings updated; workflow still pushes old names | green | green (old packages) | **pull fails** (new names empty) | **outage** |
| Next CMD path not updated | green | green | health-gate fail on portal/developers | **partial outage** |
| Bake/Taskfile left stale; GHCR fixed | green Actions | green Actions | OK | local operator push broken |
| Only 2 of 4 apps renamed in matrix | mixed | mixed | may deploy old+new combo | **inconsistent hub** |
| `skip_build` deploy mid-rename with new compose image names | n/a | skipped | **pull missing tags** | **outage** |

### 10.2 Highest-risk footguns specific to this repo

1. **Believing CI covers frontends.** `ci.yml` will stay green while GHCR is broken — PRs can merge frontend renames with zero signal until post-merge deploy pipeline fails.

2. **Image name vs folder name confusion.** People will assume renaming `ops-page` → `lazuar-ops` requires image `lazuar-ops`. Today image is already `lazuar-hub-ops`. Changing it is a **separate breaking CD change**.

3. **developers naming is the most semantic jump.** Folder `developers-page` → `lazuar-spec`, but image is `lazuar-hub-developers` and compose service is `developers`. Three names already; rename adds a fourth if image/service not carefully decided.

4. **Next.js standalone path embedding** for portal + developers/spec is easy to miss when bulk-renaming with search-replace that only hits Docker `COPY` lines but not `CMD`.

5. **`fail-fast: true` + multi-image deploy** means one broken Dockerfile blocks all new deploys — good for safety, bad if emergency API-only hotfix needed without fixing frontend Docker paths first. Mitigation already exists: `workflow_dispatch` + `skip_build` + version pin, but only works if compose still points at existing images.

6. **Local compose gaps** (`developers` missing from `docker-compose.yml` / `docker-compose.ghcr.yml`) mean local testing may not catch developers/spec image path bugs that only appear in GHCR matrix + prod compose.

### 10.3 Secrets partial-update risks

None inherent to app rename. Risk only if operators hand-edit server `.env` with experimental image overrides not in repo (not evidenced).

---

## 11. Recommended CI migration steps

Execute as **one coordinated PR** (or tightly ordered stacked PRs that never leave main undeployable). Preferred: single PR for folders + Dockerfiles + `ghcr.yml` + bake + local compose, **without** renaming GHCR image repositories.

### Phase A — Decide image naming policy (before coding)

**Option A1 — Recommended: keep GHCR image names**

- Keep `lazuar-hub-ops`, `lazuar-hub-portal`, `lazuar-hub-superadmin`, `lazuar-hub-developers`.
- Only change monorepo folder / package / Dockerfile paths / workflow `dockerfile:` entries.
- Zero prod pull breakage; cache preserved; compose untouched.

**Option A2 — Align images to new product names**

- e.g. `lazuar-ops`, `lazuar-portal`, `lazuar-admin`, `lazuar-spec`.
- Requires dual-tag transition (§ Phase D).

Document the choice in the rename PR description.

### Phase B — Atomic monorepo + CI path update (Option A1)

Order inside the PR:

1. `git mv` the four app directories to new names.
2. Update each app `package.json` `"name"` field to match.
3. Update all four Dockerfiles path segments + Next CMD/static paths.
4. Update `.github/workflows/ghcr.yml` matrix `dockerfile:` lines (exact lines 68, 73, 79, 84).
5. Update `docker-bake.hcl` target dockerfile paths (+ target names if desired).
6. Update `docker-compose.yml` / `docker-compose.ghcr.yml` service keys & dockerfile paths (DX consistency).
7. Update `mprocs-dev.yaml`, root README, docs (outside pure CI but required for workspace).
8. Regenerate `pnpm-lock.yaml` importers (`pnpm install`) so `apps/lazuar-*` paths appear.
9. Do **not** change `deploy/prod/docker-compose.yml` image strings under A1.
10. Do **not** change `scripts/remote-deploy.sh` health names under A1.
11. Refresh stale comments in `ghcr.yml` header (5 images, include developers/spec).

### Phase C — Validation before merge

1. **Local Dockerfile dry-run (at least one Next + one Vite):**
   - `docker build -f apps/lazuar-ops/Dockerfile .`
   - `docker build -f apps/lazuar-portal/Dockerfile .`
   - `docker build -f apps/lazuar-admin/Dockerfile .`
   - `docker build -f apps/lazuar-spec/Dockerfile .`
2. **Bake parity:** `task docker:build` or `docker buildx bake --load` for default group.
3. **Contracts CI parity:** `pnpm install --frozen-lockfile` + `task gen --force` (matches `ci.yml`).
4. **PR checks:** ensure `ci.yml` green.
5. **Post-merge watch:** `GHCR + deploy` matrix all five `Build lazuar-hub-*` jobs green; deploy health-gates pass for `hub-ops`, `hub-portal`, `hub-superadmin`, `hub-developers`.

### Phase D — Optional image rebrand (Option A2 only)

1. Dual-tag period: in `ghcr.yml` metadata `images:` list **both** old and new image names for each matrix entry (or push twice).
2. Update `deploy/prod/docker-compose.yml` + `docker-compose.ghcr.yml` + `docker-bake.hcl` tags to new names **while old tags still receive pushes**.
3. Deploy once on dual-tag.
4. Remove old image names from push list after confirmation.
5. Accept cold GHA cache scopes for new `matrix.name` values.
6. Optionally rename GHCR packages later; do not delete old packages until retention policy allows rollback.

### Phase E — Post-rename CI hygiene (optional)

1. Consider adding a lightweight frontend job to `ci.yml` (pnpm filter build for the four apps) so path mistakes fail on PR, not only on main deploy.
2. If adding path filters per app, use **new** names exclusively.
3. Align comment “4 images” → “5 images” and include developers/spec everywhere.
4. Decide whether local compose should gain a `lazuar-spec` / developers service for parity with prod (pre-existing gap).

### Phase F — Rollback plan

| Failure | Rollback action |
|---|---|
| GHCR matrix red after merge | Revert PR; or hotfix Dockerfile/workflow paths; deploy remains on last good `sha-…` until green build |
| Deploy health-gate red | Fix container/image; or `workflow_dispatch` with `skip_build=true` + last known good `version=sha-xxxxxxx` **if compose image names unchanged** |
| Image rebrand pull fail | Point compose back to old image names; dual-tag if needed |
| Cache weirdness | Ignore (self-heals); or change scope deliberately |

**Never** use `skip_build` after changing compose image repository names unless those names already exist on GHCR with the pinned tag.

---

## 12. Exact change map (quick reference)

### 12.1 Files under `.github/`

| File | Action |
|---|---|
| `.github/workflows/ci.yml` | **No required edits** for the four renames |
| `.github/workflows/ghcr.yml` | **Required:** 4× `dockerfile:` path updates (lines 68, 73, 79, 84); optional comment/image-name policy |
| `.github/actions/**` | **N/A** (does not exist) |

### 12.2 Files outside `.github/` that GHCR/CD requires in lockstep

| File | Folder rename required? | Image rebrand required? |
|---|---|---|
| `apps/*/Dockerfile` (four apps) | **Yes** | No |
| `docker-bake.hcl` | **Yes** (dockerfile paths; targets optional) | Yes if tags change |
| `deploy/prod/docker-compose.yml` | No | **Yes** if image names change |
| `scripts/remote-deploy.sh` | No | Only if container_name / health names change |
| `deploy/prod/Caddyfile` | No | Only if compose service DNS names change |
| `docker-compose.yml` | **Yes** (dockerfile paths) | Yes if local image names change |
| `docker-compose.ghcr.yml` | Cosmetic service keys | **Yes** if image names change |
| `Taskfile.yml` | No (unless comments) | Only echo strings if image prefix changes |
| `pnpm-lock.yaml` / app `package.json` | **Yes** (workspace integrity for any pnpm install in Docker/CI) | No |

### 12.3 Mapping table: old → new path strings for workflow + Dockerfiles

| Old path string | New path string |
|---|---|
| `apps/developers-page` | `apps/lazuar-spec` |
| `apps/ops-page` | `apps/lazuar-ops` |
| `apps/portal-page` | `apps/lazuar-portal` |
| `apps/superadmin-page` | `apps/lazuar-admin` |

### 12.4 Mapping table: GHCR images (default keep)

| Keep (recommended) | Only if rebranding images |
|---|---|
| `lazuar-hub-developers` | `lazuar-spec` or `lazuar-hub-spec` |
| `lazuar-hub-ops` | `lazuar-ops` or keep |
| `lazuar-hub-portal` | `lazuar-portal` or keep |
| `lazuar-hub-superadmin` | `lazuar-admin` or `lazuar-hub-admin` |

---

## 13. Non-findings (explicitly checked, no hit)

- No composite actions referencing the four apps.
- No reusable workflows.
- No `paths-ignore` blocks.
- No `concurrency` groups keyed on app folder names.
- No `upload-artifact` / `download-artifact` names containing the four apps.
- No environment protection rules encoded in YAML (may exist in GitHub UI — cannot see from repo files).
- No Dependabot directory entries for the four apps.
- No frontend test job that would need vitest/playwright path updates (frontends currently untested in CI per repo docs).
- `ci.yml` does not install or build any of the four frontend apps.

---

## 14. Summary

1. **`.github/` contains exactly two workflows; zero composite actions.**
2. **`ci.yml` has no references to `developers-page` / `ops-page` / `portal-page` / `superadmin-page`.** Folder renames do not require YAML edits there; lockfile/workspace must still install cleanly.
3. **`ghcr.yml` is the only GitHub Actions file that must change for folder renames**, and only on four `dockerfile:` matrix lines (plus optional comment/image policy).
4. **Path filters use broad `apps/**`**, so renamed folders still trigger CD. There are **no** per-app filters to update today; if added later, use new names only.
5. **Cache keys and job names use GHCR image names (`lazuar-hub-*`), not folder names.** Folder rename alone preserves cache; image rename cold-starts cache and requires compose coordination.
6. **Secrets do not hardcode app folder names.** Audit live `HUB_ENV_FILE` content outside the repo as a caution.
7. **Dockerfiles (and Next standalone CMD paths) are the real build breakers**, not the workflow YAML volume of changes.
8. **Prod deploy compose uses image + short service names already different from folder names** — good isolation. Do not casually rename images as part of the folder rename.
9. **Partial rename risk is high for deploy outages if image names and compose diverge; low for folder-only renames if workflow + Dockerfiles + lockfile land together.**
10. **Recommended migration:** single PR, Option A1 (keep GHCR image names), fix workflow dockerfile paths + Dockerfiles + bake + lockfile; verify all five matrix builds and deploy health-gates; optionally add frontend PR builds later so this class of failure is caught before merge.

---

## 15. Appendix — full `ghcr.yml` matrix after folder rename (Option A1)

```yaml
matrix:
  include:
    - name: lazuar-hub-api
      dockerfile: apps/lazuar-api/Dockerfile
      build_args: ""
    - name: lazuar-hub-portal
      dockerfile: apps/lazuar-portal/Dockerfile
      build_args: |
        NEXT_PUBLIC_API_URL=https://hub.lazuar.com/api/v1
        NEXT_BASE_PATH=/portal
    - name: lazuar-hub-ops
      dockerfile: apps/lazuar-ops/Dockerfile
      build_args: |
        VITE_API_URL=https://hub.lazuar.com/api/v1
        VITE_PORTAL_URL=https://hub.lazuar.com/portal
        VITE_BASE_PATH=/
    - name: lazuar-hub-superadmin
      dockerfile: apps/lazuar-admin/Dockerfile
      build_args: |
        VITE_API_URL=https://hub.lazuar.com/api/v1
        VITE_BASE_PATH=/admin/
    - name: lazuar-hub-developers
      dockerfile: apps/lazuar-spec/Dockerfile
      build_args: |
        NEXT_BASE_PATH=/docs
```

Note `matrix.name` for developers remains `lazuar-hub-developers` under Option A1 even though the folder becomes `lazuar-spec`. That inconsistency is intentional to avoid a breaking image rename; resolve later only via Phase D dual-tag if product wants image ≡ app name.

---

*End of CI/CD rename impact analysis. No application source code was modified for this document.*
