# Phase 6 — Analysis: open PR (merge + prod observe)

**Status:** Analysis only — **do not create the PR in this phase** (analyzer). Implementer may create (and optionally merge) using the command below.  
**Date:** 2026-08-09  
**Branch:** `chore/rename-frontend-apps-lazuar-prefix`  
**Base:** `main`  
**Repo:** `proxeon/lazuar-pay` (`git@github.com:proxeon/lazuar-pay.git`)  
**Related:** [`phase-5-done.md`](./phase-5-done.md), [`11-implementation-checklist.md`](./11-implementation-checklist.md) § Phase 6

---

## 1. Goal

Ship the frontend app rename as a **normal monorepo rebuild**, not a deploy-architecture change:

1. Open one atomic PR (Phases 0–5 already on branch).
2. Let PR CI (`ci.yml`) run.
3. Decide merge carefully: **merge to `main` auto-triggers `ghcr.yml` → build 5 images → SSH deploy hub VPS**.
4. Observe prod via Actions + health-gate / smoke URLs.

---

## 2. Branch vs `main`

### 2.1 Refs (local + origin, read from git refs 2026-08-09)

| Ref | SHA |
|-----|-----|
| `main` (local + `origin/main`) | `b36adafd9a7ae8eedf633eaf54dbc60545510f9f` |
| `chore/rename-frontend-apps-lazuar-prefix` (local + `origin/...`) | `5b48f6575efc551412e22c8ab6b9ee21faa2a30b` |
| Branch tracking | `origin/chore/rename-frontend-apps-lazuar-prefix` configured |

**Branch is already pushed** to origin at the same tip as local HEAD.  
**Ancestry:** branch created from `main` @ `b36adaf…` — **no main commits missing**; branch is **8 commits ahead**, **0 behind**.

### 2.2 Commits on branch not on `main` (oldest → newest)

| # | Short SHA | Subject | Phase |
|---|-----------|---------|-------|
| 1 | `7c4e4f0` | `docs(plans): add app rename investigation and phase-0 prep` | 0 |
| 2 | `b1f9dd3` | `docs(plans): mark phase 0 complete` | 0 |
| 3 | `4359e61` | `chore(apps): rename frontend apps to lazuar-* prefix` | 1 |
| 4 | `50a8788` | `chore(docker): update paths for lazuar-* frontend apps` | 2 |
| 5 | `b1aac71` | `docs(plans): add phase 2 docker path analysis` | 2 docs |
| 6 | `f71b22a` | `chore(dev): point mprocs at lazuar-* frontend apps` | 3 |
| 7 | `c75e1a9` | `docs: update living docs and lockfile for lazuar-* apps` | 4 |
| 8 | `5b48f65` | `test: verify lazuar-* frontend app rename` | 5 |

Tip message body (Phase 5): G1–G8 clean, G9 history-only, pnpm filter smoke ×4, Vite tsc green, single-app dev smoke ops/portal HTTP 200; Docker bake **skipped** (static path proof + CI).

### 2.3 What the PR changes (summary)

| Layer | Change |
|-------|--------|
| App dirs | `*-page` → `lazuar-{developers,ops,portal,admin}` |
| Package names | same as dirs |
| Docker / bake / compose / `ghcr.yml` | **paths** updated; **GHCR image names stay `lazuar-hub-*`** |
| mprocs / Taskfile polish | new cwd / process keys |
| Living docs + `pnpm-lock.yaml` | new names / importers |
| **Unchanged** | `deploy/prod/**` images & services, Caddy routes, remote-deploy container names, public paths `/` `/portal` `/docs` `/admin` |

---

## 3. `gh` availability

**Analyzer could not execute shell** in this environment. Implementer **must** verify before create:

```bash
cd /Users/akmalfirdaus/Code/lazuar/lazuar-pay
which gh && gh --version
gh auth status
gh repo view --json nameWithOwner,defaultBranchRef -q .
git status -sb
git log --oneline main..HEAD
```

Expect:

- `gh` installed and authenticated to `proxeon/lazuar-pay` (or the GitHub org that owns the remote).
- Clean working tree (or only intentional uncommitted phase-6 notes — prefer **not** blocking PR on analysis-only files; either commit analysis under `plans/002-change-name/` or leave uncommitted).
- Branch pushed: `git push -u origin HEAD` if remote tip drifts.

If `gh` missing: install GitHub CLI, or open PR via GitHub UI with the same title/body.

Check for existing PR (avoid duplicate):

```bash
gh pr list --head chore/rename-frontend-apps-lazuar-prefix --base main
```

---

## 4. Auto-deploy risk on merge to `main` — **HIGH process impact, medium residual tech risk**

### 4.1 Will merge deploy production?

**YES — almost certainly.**

`.github/workflows/ghcr.yml`:

```yaml
on:
  push:
    branches: [main]
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

This PR touches at least:

- `apps/**` (four frontend moves + Dockerfiles + living docs under apps)
- `pnpm-lock.yaml`
- `docker-bake.hcl`
- `.github/workflows/ghcr.yml`

→ **path filters match** → on push to `main` after merge:

1. **Job `build-and-push`** (matrix ×5): builds and pushes  
   `lazuar-hub-api`, `lazuar-hub-portal`, `lazuar-hub-ops`, `lazuar-hub-superadmin`, `lazuar-hub-developers`  
   tags: `latest`, `sha-<short>`, full SHA.
2. **Job `deploy`** (needs build success, `ref == main`):  
   - rsync `deploy/prod/` → VPS `/root/lazuar-hub-prod/`  
   - rsync `scripts/remote-deploy.sh`  
   - optional `.env` inject  
   - SSH run: `VERSION=sha-<7> /root/lazuar-hub-remote-deploy.sh`  
   - pull images, `compose up`, **health-gate all hub containers**, curl smoke.

### 4.2 What is *not* changing in prod (good)

| Surface | Status |
|---------|--------|
| GHCR package names | still `lazuar-hub-*` (incl. `lazuar-hub-superadmin`) |
| Prod compose service/container names | still `ops`/`hub-ops`, etc. |
| Caddy public paths | `/`, `/portal`, `/docs`, `/admin`, `/api` |
| Deploy script health names | `hub-api`, `hub-ops`, `hub-portal`, `hub-superadmin`, `hub-developers`, `hub-caddy` |

So this is **not** a “rename prod services” deploy. It is a **rebuild of FE images from new Dockerfile paths** under the **same** image tags the VPS already pulls.

### 4.3 Residual failure modes (why bake skip matters)

Phase 5 **skipped** full `docker buildx bake`. First real multi-stage image build for the renamed paths is **CI on merge**.

| Failure | Symptom | Likely cause |
|---------|---------|--------------|
| Build matrix red | Dockerfile not found / COPY fail | Stale path (Phase 5 greps say clean — low likelihood) |
| Next container crash loop | Deploy health-gate fails on `hub-portal` / `hub-developers` | Wrong standalone `CMD` path |
| Vite apps fail | `hub-ops` / `hub-superadmin` unhealthy | Dockerfile filter/dist path |
| API also rebuilt | Longer deploy; API risk is “same source, new image digest” | Normal CD, not rename-specific |
| Secrets / SSH | Deploy job fails after green builds | Ops secrets, not rename |

**If deploy fails:** treat as **image build/path bug**, **not** “rename prod compose services.” Do **not** edit `deploy/prod` service names as a “fix.” Rollback = redeploy previous good `VERSION=sha-…` via `workflow_dispatch` (`skip_build` + version pin) or re-run deploy of last known-good tag.

### 4.4 CI on PR (pre-merge)

`.github/workflows/ci.yml` runs on `pull_request` → `main`:

- **contracts** (pnpm + task gen honesty)
- **dotnet** (restore/build/tests)

It does **not** build frontend Docker images on PR. So PR green ≠ image path proven in CI — only local static proof + Phase 5 greps.

---

## 5. What “observe prod” means after merge

### 5.1 GitHub Actions

1. Open Actions for repo after merge:
   - Workflow: **“GHCR + deploy”** (`.github/workflows/ghcr.yml`)
   - Also **“CI”** on push to `main`.
2. Confirm **all 5 build matrix cells green**.
3. Confirm **Deploy hub VPS** job green.
4. Read deploy log for health lines from `remote-deploy.sh`:

```text
✓ hub-api healthy
✓ hub-ops healthy
✓ hub-portal healthy
✓ hub-superadmin healthy
✓ hub-developers healthy
✓ hub-caddy healthy
http /health → 200 (or expected)
http / → …
http /portal → …
http /docs → …
```

5. Note deployed `VERSION=sha-<short>` (matches merge commit short SHA).

### 5.2 Public smoke (browser or curl)

| URL | Expect |
|-----|--------|
| `https://hub.lazuar.com/health` | API liveness OK |
| `https://hub.lazuar.com/` | ops UI |
| `https://hub.lazuar.com/portal` | portal |
| `https://hub.lazuar.com/docs` | developers docs |
| `https://hub.lazuar.com/admin` (or `/admin/`) | superadmin |

No public path rename — if a surface 500s, inspect **that image’s** container logs on VPS (`docker logs hub-portal`, etc.), not Caddy path rewrites.

### 5.3 Optional VPS (if SSH access)

```bash
docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Image}}'
# images should show ghcr.io/proxeon/lazuar-hub-*:sha-...
```

### 5.4 Phase 6 exit criteria (checklist)

- [ ] PR opened; CI on PR green (or failures explained as pre-existing non-rename)
- [ ] Merge decision made with deploy risk acknowledged
- [ ] If merged: GHCR matrix + deploy green
- [ ] Hub health-gate + public smoke OK
- [ ] No emergency prod compose service rename (should never be needed)

---

## 6. Draft PR title

```text
chore: rename frontend apps to lazuar-* prefix
```

---

## 7. Draft PR body (use with `gh pr create`)

Copy exactly (or via HEREDOC in §8):

```markdown
## Summary

Rename monorepo frontend app folders and package names from `*-page` to the `lazuar-*` convention used by `lazuar-api` / `lazuar-docs`. Single atomic PR (Phases 0–5).

## Mapping

| Old folder / package | New folder / package | GHCR image (**unchanged**) | Prod service (**unchanged**) | Public path (**unchanged**) | Dev port |
|----------------------|----------------------|----------------------------|------------------------------|-----------------------------|----------|
| `apps/developers-page` | `apps/lazuar-developers` | `lazuar-hub-developers` | `developers` / `hub-developers` | `/docs` | 3002 |
| `apps/ops-page` | `apps/lazuar-ops` | `lazuar-hub-ops` | `ops` / `hub-ops` | `/` | 3003 |
| `apps/portal-page` | `apps/lazuar-portal` | `lazuar-hub-portal` | `portal` / `hub-portal` | `/portal` | 3004 |
| `apps/superadmin-page` | `apps/lazuar-admin` | `lazuar-hub-superadmin` | `superadmin` / `hub-superadmin` | `/admin` | 3005 |

## Non-goals (explicit)

- **Do not** rename GHCR packages (`lazuar-hub-*` stays, including `lazuar-hub-superadmin`).
- **Do not** change `deploy/prod` compose service/image names or Caddy reverse_proxy targets.
- **Do not** change public URL base paths.
- **Do not** rewrite historical ADRs / `docs/001-gaps/**` (Phase 7 optional).
- **Do not** rename backend modules, cookies, or product API routes.

## What changed

- `git mv` four frontend apps + package.json `"name"` fields
- Dockerfiles (incl. Next standalone `CMD`), `docker-bake.hcl`, local compose, `ghcr.yml` **dockerfile paths only**
- `mprocs-dev.yaml` + Taskfile polish
- Living docs (`README.md`, `lazuar-docs`, contracts SOP labels) + regenerated `pnpm-lock.yaml`
- Plan/investigation under `plans/002-change-name/`

## Verification (Phase 5)

- [x] Grep gates G1–G8 clean (functional paths)
- [x] Allowed remaining: `docs/001-gaps/**`, ADRs, `plans/002-change-name/**` only
- [x] `pnpm --filter lazuar-{developers,ops,portal,admin}` smoke OK
- [x] lint/tsc: `lazuar-ops`, `lazuar-admin` exit 0; `lazuar-developers` eslint 0; `lazuar-portal` eslint pre-existing debt (exit 1, not path/rename)
- [x] Single-app dev smoke: ops :3003 → 200, portal :3004 → 200
- [ ] Optional docker buildx bake: **skipped** — static CMD/bake path proof green; **CI `ghcr.yml` builds images on merge to main**
- [x] GHCR still `lazuar-hub-*` incl. superadmin; prod monorepo paths untouched

## Deploy impact ⚠️

**Merging to `main` will trigger workflow `GHCR + deploy`:**

1. Build/push all five hub images (new Dockerfile paths, **same** image names).
2. SSH deploy to hub VPS with `VERSION=sha-<short>`; health-gate + smoke.

This is intentional CD, not a prod architecture rename. Residual risk: first full multi-stage FE image build for new paths happens in CI (local bake was skipped). If deploy fails, fix image paths / rebuild — do **not** rename prod services.

## Plan / checklist

- Investigation + checklist: [`plans/002-change-name/`](../tree/chore/rename-frontend-apps-lazuar-prefix/plans/002-change-name)
- Implementation checklist Phase 6: `11-implementation-checklist.md`
- Done notes: `phase-0-done` … `phase-5-done`

## Test plan (post-merge observe)

- [ ] Actions: `GHCR + deploy` matrix ×5 green
- [ ] Deploy job: hub-* containers healthy
- [ ] Smoke: `https://hub.lazuar.com/health`, `/`, `/portal`, `/docs`, `/admin`
```

---

## 8. Exact `gh pr create` command

Run from repo root on branch `chore/rename-frontend-apps-lazuar-prefix` (already tracking origin):

```bash
cd /Users/akmalfirdaus/Code/lazuar/lazuar-pay
git checkout chore/rename-frontend-apps-lazuar-prefix
git status -sb
# ensure push is current:
git push -u origin HEAD

gh pr create \
  --base main \
  --head chore/rename-frontend-apps-lazuar-prefix \
  --title "chore: rename frontend apps to lazuar-* prefix" \
  --body "$(cat <<'EOF'
## Summary

Rename monorepo frontend app folders and package names from `*-page` to the `lazuar-*` convention used by `lazuar-api` / `lazuar-docs`. Single atomic PR (Phases 0–5).

## Mapping

| Old folder / package | New folder / package | GHCR image (**unchanged**) | Prod service (**unchanged**) | Public path (**unchanged**) | Dev port |
|----------------------|----------------------|----------------------------|------------------------------|-----------------------------|----------|
| `apps/developers-page` | `apps/lazuar-developers` | `lazuar-hub-developers` | `developers` / `hub-developers` | `/docs` | 3002 |
| `apps/ops-page` | `apps/lazuar-ops` | `lazuar-hub-ops` | `ops` / `hub-ops` | `/` | 3003 |
| `apps/portal-page` | `apps/lazuar-portal` | `lazuar-hub-portal` | `portal` / `hub-portal` | `/portal` | 3004 |
| `apps/superadmin-page` | `apps/lazuar-admin` | `lazuar-hub-superadmin` | `superadmin` / `hub-superadmin` | `/admin` | 3005 |

## Non-goals (explicit)

- **Do not** rename GHCR packages (`lazuar-hub-*` stays, including `lazuar-hub-superadmin`).
- **Do not** change `deploy/prod` compose service/image names or Caddy reverse_proxy targets.
- **Do not** change public URL base paths.
- **Do not** rewrite historical ADRs / `docs/001-gaps/**` (Phase 7 optional).
- **Do not** rename backend modules, cookies, or product API routes.

## What changed

- `git mv` four frontend apps + package.json `"name"` fields
- Dockerfiles (incl. Next standalone `CMD`), `docker-bake.hcl`, local compose, `ghcr.yml` **dockerfile paths only**
- `mprocs-dev.yaml` + Taskfile polish
- Living docs (`README.md`, `lazuar-docs`, contracts SOP labels) + regenerated `pnpm-lock.yaml`
- Plan/investigation under `plans/002-change-name/`

## Verification (Phase 5)

- [x] Grep gates G1–G8 clean (functional paths)
- [x] Allowed remaining: `docs/001-gaps/**`, ADRs, `plans/002-change-name/**` only
- [x] `pnpm --filter lazuar-{developers,ops,portal,admin}` smoke OK
- [x] lint/tsc: `lazuar-ops`, `lazuar-admin` exit 0; `lazuar-developers` eslint 0; `lazuar-portal` eslint pre-existing debt (exit 1, not path/rename)
- [x] Single-app dev smoke: ops :3003 → 200, portal :3004 → 200
- [ ] Optional docker buildx bake: **skipped** — static CMD/bake path proof green; **CI `ghcr.yml` builds images on merge to main**
- [x] GHCR still `lazuar-hub-*` incl. superadmin; prod monorepo paths untouched

## Deploy impact ⚠️

**Merging to `main` will trigger workflow `GHCR + deploy`:**

1. Build/push all five hub images (new Dockerfile paths, **same** image names).
2. SSH deploy to hub VPS with `VERSION=sha-<short>`; health-gate + smoke.

This is intentional CD, not a prod architecture rename. Residual risk: first full multi-stage FE image build for new paths happens in CI (local bake was skipped). If deploy fails, fix image paths / rebuild — do **not** rename prod services.

## Plan / checklist

- Investigation + checklist: `plans/002-change-name/`
- Implementation checklist Phase 6: `11-implementation-checklist.md`
- Done notes: `phase-0-done` … `phase-5-done`

## Test plan (post-merge observe)

- [ ] Actions: `GHCR + deploy` matrix ×5 green
- [ ] Deploy job: hub-* containers healthy
- [ ] Smoke: `https://hub.lazuar.com/health`, `/`, `/portal`, `/docs`, `/admin`
EOF
)"
```

After create, print URL:

```bash
gh pr view --web
# or
gh pr view --json url -q .url
```

### Optional: wait for PR CI then merge

```bash
# Prefer: create PR, wait for checks, then merge only if ready for prod rebuild
gh pr checks --watch

# Merge (squash optional; either fine for atomic rename branch)
gh pr merge --merge   # or: --squash
# Do NOT use --admin to skip failing checks unless intentional
```

### Optional: merge without waiting (higher risk)

Only if policy allows shipping with deploy eyes open:

```bash
gh pr merge --merge --auto   # merges when checks pass
# or immediate:
gh pr merge --merge
```

---

## 9. Recommendation (implementer policy)

| Action | Recommended? | Notes |
|--------|--------------|-------|
| **Create PR** | **YES — do this** | Safe; no prod impact until merge. |
| Wait for PR CI green | **YES** | contracts + dotnet; does not prove Docker images. |
| **Merge to main** | **YES only with deploy awareness** | User allowed free merge; **must** note that **prod deploy will run**. Prefer create → CI green → merge → watch Actions. |
| Merge without watching deploy | **No** | Residual risk is image build on first CD. |
| Skip PR / push straight to main | **No** | Lose PR review + contracts CI signal. |
| Edit prod compose to “match” folder names | **Never for this PR** | Out of scope; breaks intentional decoupling. |

**Bottom line for implementer:**

1. Run the `gh pr create` command in §8 (after `gh auth status` OK).  
2. Tell the user: **PR opened; merging will rebuild and redeploy hub production via `ghcr.yml`.**  
3. Merge only when ready for that deploy (user said freely OK — allowed, but **document deploy risk in PR body** which §7 already does).  
4. After merge, **observe** Actions + public smoke (§5).  
5. Phase 6 done when PR merged (or opened + handed off) and, if merged, prod healthy.

---

## 10. Analyzer non-actions

- Did **not** run `gh pr create`.
- Did **not** merge.
- Did **not** push new commits (this analysis file may be committed by implementer if desired; not required for the rename to ship).

---

## 11. Phase 6 implementer checklist (copy)

- [ ] `gh auth status` OK; branch pushed
- [ ] No duplicate open PR for this head
- [ ] `gh pr create` with title/body from §8
- [ ] Notify: merge triggers prod GHCR build + deploy
- [ ] (Optional) wait `gh pr checks --watch`
- [ ] (If merging) `gh pr merge` + watch **GHCR + deploy**
- [ ] Observe health-gate + public smoke
- [ ] Record outcome in `phase-6-done.md` (implementer)

---

*End of Phase 6 analysis. Create PR; merge only with explicit deploy-risk awareness.*
