# Phase 0 — Analysis & action brief

**Status:** Complete (analysis only — **no renames executed**)  
**Date:** 2026-08-09  
**Branch:** `chore/rename-frontend-apps-lazuar-prefix`  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Related:** [`11-implementation-checklist.md`](./11-implementation-checklist.md), [`README.md`](./README.md), inventory [`10-master-reference-inventory.md`](./10-master-reference-inventory.md)

---

## 1. Decisions locked

| Current folder / package | **Target folder / package** | GHCR image (**unchanged**) | Prod service (**unchanged**) | Public path (**unchanged**) | Dev port (**unchanged**) |
|--------------------------|-----------------------------|----------------------------|------------------------------|-----------------------------|--------------------------|
| `apps/developers-page` | **`apps/lazuar-developers`** | `lazuar-hub-developers` | `developers` / `hub-developers` | `/docs` | 3002 |
| `apps/ops-page` | **`apps/lazuar-ops`** | `lazuar-hub-ops` | `ops` / `hub-ops` | `/` | 3003 |
| `apps/portal-page` | **`apps/lazuar-portal`** | `lazuar-hub-portal` | `portal` / `hub-portal` | `/portal` | 3004 |
| `apps/superadmin-page` | **`apps/lazuar-admin`** | `lazuar-hub-superadmin` | `superadmin` / `hub-superadmin` | `/admin` | 3005 |

**Rationale (developers name):** Prefer `lazuar-developers` over historical investigation draft `lazuar-spec` to avoid clashing with `packages/api-spec` / `@repo/api-spec`. Some older analysis files under `plans/002-change-name/` still say `lazuar-spec` — treat those as superseded by this lock.

**PR strategy:** Single atomic PR for implementation Phases 1–4 (all four apps together). Phase 5+ historical docs optional later.

---

## 2. Non-goals (do **not** change in this rename)

- GHCR image package names (`ghcr.io/.../lazuar-hub-*`)
- `deploy/prod/**` (compose service keys, image refs, env)
- `deploy/prod/Caddyfile` reverse_proxy targets / public routing
- `scripts/remote-deploy.sh` health-gate / container names
- Public URL base paths (`/`, `/portal`, `/docs`, `/admin`)
- Backend modules / routes (`Modules/Ops`, `/api/v1/ops`, `/api/v1/admin`, etc.)
- Cookies / localStorage product keys (`lazuar_auth`, `lazuar_admin_auth`, sidebar keys)
- Bare tokens `ops`, `portal`, `developers`, `admin`, `superadmin` outside the `*-page` package strings
- Bulk rewrite of every historical ADR / `docs/001-gaps/**` (optional follow-up)
- Phasing one frontend app at a time

**Confirmed pre-flight:** `deploy/**`, `scripts/**`, and `Taskfile.yml` have **zero** matches for `developers-page|ops-page|portal-page|superadmin-page`.

---

## 3. Current tree status

| Item | Value |
|------|--------|
| **Git branch** | `chore/rename-frontend-apps-lazuar-prefix` |
| **Branch tip** | `b36adafd9a7ae8eedf633eaf54dbc60545510f9f` (same as `main`; branch created via checkout from main — **no rename commits yet**) |
| **Last committed subject on tip** | `fix(ops): make Developer webhooks New button scroll to create form` |
| **packageManager** | `pnpm@11.5.2` (root `package.json`) |
| **Workspace** | `pnpm-workspace.yaml` → `apps/*`, `packages/*` (no hard-coded app names) |
| **Node engines** | `>=18` |
| **App dirs today** | `apps/developers-page`, `apps/ops-page`, `apps/portal-page`, `apps/superadmin-page` still present with old names |
| **Rename code changes** | **None** (Phase 0 is analysis + planning commit only) |

### Working tree / planning artifacts

`plans/` has **uncommitted reorganization** that must be included in the **Phase 0 commit** as planning artifacts:

| Path | Role |
|------|------|
| `plans/001-backend/` | Backend solidification checklist moved/organized under numbered plans |
| `plans/002-change-name/` | Full rename investigation (reports `01`–`10` + `11-implementation-checklist` + this brief) |
| `plans/002-change-name/phase-0-analysis.md` | **This file** |

Phase 0 does **not** require a clean empty worktree before commit; it requires an intentional commit of planning artifacts only (no app renames). Optional: leave local build junk (`.next/`, `dist/`, nested app `node_modules`) untracked — do not commit them.

---

## 4. Pre-flight inventory (re-run 2026-08-09)

### Command shape used

```text
pattern: developers-page|ops-page|portal-page|superadmin-page
exclude: node_modules, .next, dist, bin, obj
```

### Line-hit scale (approx.; ripgrep caps “at least N”)

| Scope | Combined pattern | Notes |
|-------|------------------|--------|
| Whole repo excl. build noise | **≥ ~393** matching lines | Includes heavy self-reference in `plans/002-change-name/**` |
| Excl. `plans/**` as well | **≥ ~141** matching lines | Operational + living docs + gap/ADR narrative |
| `deploy/**` | **0** | Non-goal confirmed |
| `scripts/**` | **0** | Non-goal confirmed |
| `Taskfile.yml` | **0** | Bake group carries names; Taskfile does not hardcode `*-page` |
| `apps/lazuar-api/**` | **2** comment-only (`ops-page` product wording) | Cosmetic / optional |

### Per-token scale (whole repo excl. build noise; includes plans)

| Pattern | Approx. line hits (lower bound) |
|---------|----------------------------------|
| `developers-page` | ≥ ~177 |
| `ops-page` | ≥ ~127 |
| `portal-page` | ≥ ~161 |
| `superadmin-page` | ≥ ~173 |

Prior master inventory (~2026-08-08) estimated ~269 combined content hits before the `plans/002` corpus grew; current totals are higher **because planning docs document every hit**.

### File inventory — must-change (functional; **~18 path groups**)

| # | Path / group | Why |
|---|--------------|-----|
| 1–4 | `apps/{developers,ops,portal,superadmin}-page/` → new dirs | Workspace package roots (`git mv`) |
| 5–8 | Four `package.json` `"name"` fields | pnpm identity / `--filter` by name |
| 9–12 | Four `Dockerfile`s | `COPY`, `pnpm --filter ./apps/...`, Next standalone static + **CMD** (`portal`, `developers`) |
| 13 | `docker-bake.hcl` | Target names + dockerfile paths (image tags stay `lazuar-hub-*`) |
| 14 | `docker-compose.yml` | Service keys + dockerfile paths for ops/portal/superadmin |
| 15 | `docker-compose.ghcr.yml` | Service keys for three frontends |
| 16 | `mprocs-dev.yaml` | Proc keys + `cd apps/...` for all four |
| 17 | `.github/workflows/ghcr.yml` | Matrix `dockerfile:` paths only (image `name:` stays) |
| 18 | `pnpm-lock.yaml` | Importer keys `apps/*-page` — regenerate via install |

**Runnable living docs (must if command must keep working):**

- `apps/lazuar-docs/docs/reference/openapi.md` — `pnpm --filter developers-page dev`

**Recommended living docs (same PR or Phase docs):**

- Root `README.md` (structure tree, port table, product bullets)
- `apps/lazuar-docs/README.md`, `docs/index.md`, `guide/how-to-maintain.md` (narrative)

### File inventory — optional / skip in core rename

| Category | Count / notes |
|----------|----------------|
| Path-header comments in app TS | ~15 files (`// apps/ops-page/...`, `// apps/portal-page/...`, stale superadmin copy-from-ops headers) |
| Backend C# comments | 2 files under `Modules/One/Infrastructure/` |
| Historical gaps + ADRs | ~25+ docs under `docs/001-gaps/**`, `docs/architecture-decision-log/**`, `docs/contracts/**` |
| `docs/001-gaps/04-developers-page-dx.md` | Filename itself is historical — leave unless dedicated doc PR |
| Nested lockfiles | `apps/developers-page/pnpm-lock.yaml`, `apps/portal-page/pnpm-lock.yaml` if present — do not treat as workspace source of truth |

### Confirmed non-participants for package-string rename

`pnpm-workspace.yaml`, root `package.json`, `turbo.json`, `Taskfile.yml`, `.github/workflows/ci.yml`, `deploy/**`, `scripts/**` — no `*-page` package strings.

---

## 5. Exact Phase 0 implement steps (what to commit)

Phase 0 **commits planning only**. Do **not** `git mv` apps, edit Dockerfiles, bake, compose, mprocs, CI, package names, or lockfile.

### 0.1 Confirm locks (done in this brief)

- [x] Developers target = **`lazuar-developers`** (not `lazuar-spec`)
- [x] Non-goals listed and deploy/scripts verified empty of `*-page`
- [x] Single atomic PR for later implementation phases

### 0.2 Stage planning artifacts only

```bash
# From repo root — planning only
git add plans/001-backend/
git add plans/002-change-name/
# includes: 01–11 analyses, README, phase-0-analysis.md (this file)

git status   # expect only plans/** (and nothing under apps/* renames)
```

Do **not** stage:

- Any `apps/*` renames or path edits
- `docker-bake.hcl`, compose, mprocs, workflows, `pnpm-lock.yaml`
- Local artifacts: `node_modules/`, `.next/`, `dist/`, `*.tsbuildinfo`, nested lockfiles

### 0.3 Commit (suggested message)

```text
docs(plans): Phase 0 rename prep — 001-backend + 002-change-name

Capture frontend app rename investigation and backend plan layout.
Locks: developers-page→lazuar-developers, ops→lazuar-ops,
portal→lazuar-portal, superadmin→lazuar-admin.
No app renames in this commit.
```

### 0.4 After Phase 0 commit (exit → handoff)

- Branch tip advances with **plans only**
- Implementation starts at Phase 1 per `11-implementation-checklist.md` (`git mv` four apps, package names, Docker/CI/compose/mprocs, lockfile regen)

---

## 6. Exit criteria (Phase 0)

| Criterion | Met when |
|-----------|----------|
| Names locked | Table in §1 is authoritative; implementers use `lazuar-developers` not `lazuar-spec` |
| Non-goals agreed | §2 + deploy/scripts/Taskfile pre-flight = no accidental prod/GHCR renames |
| Branch ready | On `chore/rename-frontend-apps-lazuar-prefix` |
| Tooling noted | `packageManager: pnpm@11.5.2` |
| Inventory fresh | Pre-flight grep re-run; must-change shortlist still ~18 path groups |
| Planning committed | `plans/001-backend/**` + `plans/002-change-name/**` (incl. this file) on the branch |
| No half-rename | Working tree has **no** partial app renames; code still builds under old folder names |
| Handoff clear | Next step is Phase 1 of `11-implementation-checklist.md`, not more investigation |

---

## 7. Explicit out of scope for this Phase 0 deliverable

- No `git mv` of frontend apps  
- No package.json / Dockerfile / bake / compose / mprocs / CI / lockfile edits  
- No doc path bulk-replace outside what is staged under `plans/`  

**Next phase:** Phase 1 — Move apps + package identity (`11-implementation-checklist.md` § Phase 1).
