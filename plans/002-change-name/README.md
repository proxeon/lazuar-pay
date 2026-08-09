# 002 — Change frontend app names

**Status:** Rename shipped (Phases 0–6) · Phase 7 polish done  
**Date:** 2026-08-08 · **Closed:** 2026-08-09  

**Phase closeouts:** [`phase-6-done.md`](./phase-6-done.md) (merge + deploy) · [`phase-7-done.md`](./phase-7-done.md) (docs banners + tunnel nit)  


**Implementation checklist (start here to execute):**  
→ [`11-implementation-checklist.md`](./11-implementation-checklist.md)

**Locked target mapping (checklist default):**

| Current folder / package | Target |
|--------------------------|--------|
| `apps/developers-page` | `apps/lazuar-developers` (not `lazuar-spec`) |
| `apps/ops-page` | `apps/lazuar-ops` |
| `apps/portal-page` | `apps/lazuar-portal` |
| `apps/superadmin-page` | `apps/lazuar-admin` |

Original user idea used `lazuar-spec` for developers; investigation preferred `lazuar-developers` to avoid clashing with `packages/api-spec`.

This directory holds **uncondensed subagent analyses** (10 reports), the implementation checklist, and an orchestrator evaluation below.

---

## Subagent reports (full text — do not treat this README as a substitute)

| # | File | Focus |
|---|------|--------|
| 01 | [`01-docker-ghcr-compose.md`](./01-docker-ghcr-compose.md) | Dockerfiles, bake, compose, GHCR image layers |
| 02 | [`02-pnpm-turbo-packages.md`](./02-pnpm-turbo-packages.md) | package.json names, workspace, lockfile, turbo |
| 03 | [`03-github-actions-ci.md`](./03-github-actions-ci.md) | `.github/workflows` CI/CD |
| 04 | [`04-taskfile-mprocs-scripts.md`](./04-taskfile-mprocs-scripts.md) | Taskfile, mprocs, local scripts/ports |
| 05 | [`05-documentation-references.md`](./05-documentation-references.md) | README, docs/, ADRs, lazuar-docs |
| 06 | [`06-source-code-internal-refs.md`](./06-source-code-internal-refs.md) | App source, CORS, cookies, false positives |
| 07 | [`07-deploy-runtime-env.md`](./07-deploy-runtime-env.md) | deploy/prod, Caddy, remote-deploy, prod runtime |
| 08 | [`08-naming-semantics-consistency.md`](./08-naming-semantics-consistency.md) | Naming fitness vs lazuar-api/docs pattern |
| 09 | [`09-git-tooling-operational-risks.md`](./09-git-tooling-operational-risks.md) | git mv, caches, phased vs big-bang |
| 10 | [`10-master-reference-inventory.md`](./10-master-reference-inventory.md) | Exhaustive grep inventory + must-change shortlist |
| 11 | [`11-implementation-checklist.md`](./11-implementation-checklist.md) | **Phase-by-phase implementation checklist** |

Approx. **~9,000 lines** of analysis total, plus the executable checklist.

---

## Orchestrator evaluation (synthesized)

See conversation reply or section below if copied into a decision doc.

### Bottom line

1. **Rename is worth doing** for monorepo consistency with `lazuar-api` / `lazuar-docs` and dropping the redundant `-page` suffix.
2. **You do *not* need to rename GHCR packages or prod compose services** for folder rename to succeed. Those layers are already decoupled (`lazuar-hub-ops`, prod service `ops`, etc.).
3. **`lazuar-spec` is the only weak name** in the proposal — collides conceptually with `packages/api-spec` / `@repo/api-spec`. Prefer **`lazuar-developers`** unless you intentionally want “spec” branding and will disambiguate everywhere.
4. **Blast radius is mechanical, medium size, low production risk** if GHCR image names stay put: ~10–15 critical files + lockfile, not a prod service rewrite.
5. **Do it as one atomic PR** (all four apps + tooling). Phasing only multiplies lockfile/CI churn.

### Six naming layers (critical mental model)

| Layer | Today | Forced by folder rename? |
|-------|--------|---------------------------|
| App directory | `apps/*-page` | **Yes** |
| pnpm package `name` | same as folder | **Yes** |
| Bake targets / local compose services | `ops-page`, … | **Yes** (path + keys) |
| GHCR image | `ghcr.io/proxeon/lazuar-hub-{ops,portal,superadmin,developers}` | **No** (keep) |
| Prod compose service + container | `ops` / `hub-ops`, … | **No** (keep) |
| Public URL path | `/`, `/portal`, `/docs`, `/admin` | **No** (keep) |

### Must-change shortlist (functional)

- `git mv` four app directories
- Four `package.json` `"name"` fields
- Four Dockerfiles (`COPY`, `pnpm --filter`, **Next standalone `CMD` for portal + developers**)
- `docker-bake.hcl` targets + dockerfile paths
- `docker-compose.yml` (+ `docker-compose.ghcr.yml` service keys / dockerfile paths)
- `mprocs-dev.yaml`
- `.github/workflows/ghcr.yml` matrix `dockerfile:` paths only
- `pnpm install` → commit regenerated `pnpm-lock.yaml`
- Living docs: root `README.md`, `apps/lazuar-docs/.../openapi.md` (`pnpm --filter`)

### Explicitly do **not** need to change for a successful rename

- `deploy/prod/docker-compose.yml` image names
- `deploy/prod/Caddyfile` reverse_proxy service DNS
- `scripts/remote-deploy.sh` health-gate container names
- GHCR package names (`lazuar-hub-*`)
- Public paths `/docs`, `/portal`, `/admin`
- Backend `Modules/Ops`, CORS ports, cookies, UI product titles

### Recommended final names

| Current | Recommended | Notes |
|---------|-------------|--------|
| `ops-page` | **`lazuar-ops`** | Accept proposal |
| `portal-page` | **`lazuar-portal`** | Accept proposal |
| `superadmin-page` | **`lazuar-admin`** | Accept proposal (UI already says “Lazuar Admin”) |
| `developers-page` | **`lazuar-developers`** (not `lazuar-spec`) | Avoid clash with `packages/api-spec` |

Optional later (separate PR): dual-tag / rebrand GHCR `lazuar-hub-superadmin` → `lazuar-hub-admin` and/or `lazuar-hub-developers` → align with folder — only if branding demands it.

### Risk ranking

| Risk | Severity | Mitigation |
|------|----------|------------|
| Next standalone `CMD` still `apps/portal-page/server.js` | **P0** | Update both Next Dockerfiles atomically |
| GHCR workflow dockerfile path stale | **P0** | Update matrix in same PR |
| mprocs `cd apps/old` after rename | **P0** for local DX | Update `mprocs-dev.yaml` |
| Renaming GHCR images without dual-tag | **P0 prod outage** | **Don't** in this PR |
| Partial rename (one app only) | Medium confusion | Big-bang PR |
| Bulk-replace bare `ops` / `portal` | High false positives | Only match `*-page` tokens / paths |
| Docs/ADRs historical language | Low | Defer; optional banner |

### Suggested execution order (when you decide to implement)

1. Confirm final names (`lazuar-developers` vs `lazuar-spec`).
2. Single PR: `git mv` + package names + Docker/bake/compose/mprocs/ghcr.yml + lockfile + critical README/docs filters.
3. Local verify: `pnpm install`, `task fe` (mprocs), bake or compose build one Next + one Vite image.
4. Merge to `main` → normal GHCR build + deploy (image names unchanged → prod compose untouched).
5. Follow-up PR (optional): bulk doc path refresh; optional GHCR image rebrand with dual-tag playbook.
