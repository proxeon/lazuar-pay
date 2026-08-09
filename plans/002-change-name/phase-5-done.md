# Phase 5 — Done: verification (before merge)

**Status:** **PASS** (merge-ready for rename; optional Docker image builds skipped)  
**Date:** 2026-08-09  
**Branch:** `chore/rename-frontend-apps-lazuar-prefix`  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Related:** [`phase-5-analysis.md`](./phase-5-analysis.md), [`11-implementation-checklist.md`](./11-implementation-checklist.md) § Phase 5

---

## 1. Summary

| Criterion | Result |
|-----------|--------|
| G1–G8 hard grep gates | **PASS** |
| G9 soft triage | **PASS** (only ALLOW history) |
| Dirs present / old dirs gone | **PASS** |
| pnpm install + lockfile importers | **PASS** |
| pnpm `--filter lazuar-*` smoke ×4 | **PASS** |
| Vite lint/tsc (`lazuar-ops`, `lazuar-admin`) | **PASS** (exit 0) |
| Next lint (`lazuar-portal`, `lazuar-developers`) | **PARTIAL** — developers exit 0; portal exit 1 pre-existing eslint debt (not rename-induced) |
| Local FE smoke (single-app Vite + Next) | **PASS** (ops :3003 → 200; portal :3004 → 200) |
| Next Dockerfile CMD static proof | **PASS** |
| Docker image bake | **SKIPPED** (CI `ghcr.yml` covers on merge; see §6) |
| GHCR + prod safety | **PASS** |
| Fixable rename leftovers | **None found** |

**Overall Phase 5:** **PASS** per §9.1 of analysis (P1–P9 hard criteria met; Docker image optional).

---

## 2. Identity / folders (P3)

```text
PASS: apps/lazuar-developers, apps/lazuar-ops, apps/lazuar-portal, apps/lazuar-admin present
PASS: apps/{developers,ops,portal,superadmin}-page absent
```

Package `"name"` positive proof:

| File | name |
|------|------|
| `apps/lazuar-developers/package.json` | `lazuar-developers` |
| `apps/lazuar-ops/package.json` | `lazuar-ops` |
| `apps/lazuar-portal/package.json` | `lazuar-portal` |
| `apps/lazuar-admin/package.json` | `lazuar-admin` |

---

## 3. Grep gates

### 3.1 Hard gates G1–G8

| Gate | Check | Result |
|------|-------|--------|
| **G1** | `apps/(developers\|ops\|portal\|superadmin)-page` outside history excludes | **PASS** (0 matches; rg exit 1) |
| **G2** | Dockerfiles, bake, compose, ghcr, mprocs, Taskfile old `*-page` tokens | **PASS** (0 matches) |
| **G3** | Lockfile old importers `apps/*-page:` | **PASS** (0 matches) |
| **G4** | Living `--filter *-page`; living docs (`README`, `lazuar-docs`, `docs/contracts`, `plans/001-backend`); root `package.json` | **PASS** (0 matches) |
| **G5** | Old package `"name": "*-page"` | **PASS** (0 matches); new names ×4 present |
| **G6** | Positive new dirs, CMD, lockfile importers, mprocs cwd | **PASS** (see below) |
| **G7** | GHCR still `lazuar-hub-*` incl. `lazuar-hub-superadmin`; matrix dockerfile under `apps/lazuar-*` | **PASS** |
| **G8** | `deploy/prod/` no monorepo app folder paths | **PASS** (0 matches) |

#### G6 positive evidence

**Bake / CI / compose Dockerfiles:**

- `docker-bake.hcl`: `dockerfile = "apps/lazuar-{portal,ops,admin,developers}/Dockerfile"`
- `.github/workflows/ghcr.yml`: `dockerfile: apps/lazuar-{portal,ops,admin,developers}/Dockerfile` (ops/admin included)
- `docker-compose.yml`: same for portal + developers (and ops/admin)

**Next CMDs:**

```text
apps/lazuar-portal/Dockerfile:52:CMD ["node", "apps/lazuar-portal/server.js"]
apps/lazuar-developers/Dockerfile:50:CMD ["node", "apps/lazuar-developers/server.js"]
```

**Lockfile importers:**

```text
apps/lazuar-admin:
apps/lazuar-developers:
apps/lazuar-ops:
apps/lazuar-portal:
```

**mprocs:**

```text
cd apps/lazuar-developers && pnpm dev
cd apps/lazuar-ops && pnpm dev
cd apps/lazuar-admin && pnpm dev
cd apps/lazuar-portal && pnpm dev
```

#### G7 GHCR / matrix

| Surface | Evidence |
|---------|----------|
| Bake tags | `lazuar-hub-api|ops|portal|superadmin|developers` |
| ghcr matrix `name:` | five hub images; FE dockerfiles under `apps/lazuar-*` |
| Prod compose images | `ghcr.io/proxeon/lazuar-hub-*` only |
| `lazuar-hub-superadmin` | still present (admin app → superadmin image intentional) |

### 3.2 Soft gate G9

```text
Bare developers-page|ops-page|portal-page|superadmin-page
outside node_modules/.next/dist/bin/obj:
  ONLY under:
    docs/001-gaps/**
    docs/architecture-decision-log/**
    plans/002-change-name/**
Hits OUTSIDE allowed history: (none)
Allowed history match count (approx): ~1897 lines
```

**Verdict:** **PASS** — no functional leftovers; all bare tokens are Phase 7 archaeology / ADR history / this rename program.

---

## 4. pnpm filter smoke (P4–P5)

### 4.1 Install honesty

```text
pnpm install → exit 0; Already up to date
old importers apps/*-page: 0
new importers apps/lazuar-*: 4
```

### 4.2 Filter resolves

```text
pnpm --filter lazuar-developers exec … → ok developers
pnpm --filter lazuar-ops exec …        → ok ops
pnpm --filter lazuar-portal exec …     → ok portal
pnpm --filter lazuar-admin exec …      → ok admin
```

**Negative control (old names):**

```text
pnpm --filter ops-page exec …         → "No projects matched the filters" (did not print "should not run")
pnpm --filter developers-page exec …  → same
```

Note: pnpm may exit 0 when no projects match filters; pass condition is **no exec body ran** / filter not matched.

### 4.3 Package list

```text
lazuar-developers@0.1.0 …/apps/lazuar-developers
lazuar-ops@0.0.0 …/apps/lazuar-ops
lazuar-portal@0.1.0 …/apps/lazuar-portal
lazuar-admin@0.0.0 …/apps/lazuar-admin
```

---

## 5. Lint / typecheck (P6)

| Package | Script | Exit | Notes |
|---------|--------|------|-------|
| `lazuar-ops` | `tsc --noEmit` | **0** | Clean |
| `lazuar-admin` | `tsc --noEmit` | **0** | Clean |
| `lazuar-developers` | `eslint` | **0** | Clean |
| `lazuar-portal` | `eslint` | **1** | Pre-existing debt (see below) |

### 5.1 Portal lint debt (non-blocking for rename)

Filter and package path resolve correctly; failures are application/eslint rules, **not** missing packages or `*-page` paths:

- `react-hooks/set-state-in-effect` — carousel, use-mobile, CheckoutSuccessView
- `react/no-unescaped-entities` — legal pages
- `@typescript-eslint/no-explicit-any` — checkout modules
- unused vars / prefer-const / purity (`Date.now` in QuoteView)
- ~28 problems (20 errors, 8 warnings)

**Rename-specific fail signals:** none (no filter-not-found, no `apps/*-page` resolution, no missing tsconfig).

---

## 6. Docker verification

### 6.1 Static path proof — **PASS** (required)

| Check | Result |
|-------|--------|
| Four Dockerfiles exist under `apps/lazuar-*` | **PASS** |
| Bake targets `lazuar-{portal,ops,admin,developers}` | **PASS** (4) |
| Bake `dockerfile = "apps/lazuar-…/Dockerfile"` | **PASS** (4) |
| Next CMD under `apps/lazuar-{portal,developers}/server.js` | **PASS** (2) |

### 6.2 Image bake — **SKIPPED**

**Reason:** Phase 5 analysis §6.1 prefers skip when grep G1–G8 + filter smoke + lint/tsc are green; full/multi-stage FE image builds are heavy and redundant with CI `.github/workflows/ghcr.yml` on merge (new dockerfile paths, same `lazuar-hub-*` names).

Not a hard fail per §9.3.

---

## 7. Local FE orchestration (P7)

### 7.1 mprocs config (static)

Process keys + cwd paths all `lazuar-*` / `apps/lazuar-*`; autostart on four FE apps. Ports locked in package scripts:

| App | Port | `dev` script |
|-----|------|--------------|
| `lazuar-developers` | 3002 | `next dev -p 3002` |
| `lazuar-ops` | 3003 | `vite --port=3003` |
| `lazuar-portal` | 3004 | `next dev -p 3004` |
| `lazuar-admin` | 3005 | `vite --port=3005` |

### 7.2 Single-app dev smoke (executed)

| App | Command | Port | HTTP |
|-----|---------|------|------|
| `lazuar-ops` | `pnpm dev` from package dir | 3003 | **200** |
| `lazuar-portal` | `pnpm dev` from package dir | 3004 | **200** |

Logs showed Vite ready and Next 16 ready; no `cd: no such file` / path errors. Full `task fe` / mprocs interactive session not required once single-app Vite + Next proven.

API :8080 not exercised (not a rename gate).

---

## 8. Fixes applied during Phase 5

**None.** No fixable functional rename leftovers (G9 empty outside ALLOW history). Pre-existing portal eslint debt left untouched (Phase 5 non-goal).

---

## 9. Evidence block for PR body

```markdown
## Phase 5 verification

- [x] Grep gates G1–G8 clean (functional paths)
- [x] Allowed remaining: docs/001-gaps, ADRs, plans/002-change-name only
- [x] pnpm --filter lazuar-{developers,ops,portal,admin} smoke OK
- [x] lint/tsc: lazuar-ops, lazuar-admin exit 0; lazuar-developers eslint 0;
      lazuar-portal eslint pre-existing debt (exit 1, not path/rename)
- [x] Single-app dev smoke: ops :3003 → 200, portal :3004 → 200
- [ ] Optional docker buildx bake: skipped — static CMD/bake path proof green; CI covers images
- [x] GHCR still lazuar-hub-* incl. superadmin; prod monorepo paths untouched
```

---

## 10. Phase 5 exit criteria checklist

- [x] Grep functional gates pass (G1–G8); G9 only ALLOW
- [x] pnpm filter smoke for all four `lazuar-*` apps
- [x] Preferred lint/tsc on Vite apps; Next lint attempted (developers green; portal debt documented)
- [x] Local FE single-app dev smoke (Vite + Next)
- [x] Next Dockerfile CMDs path-correct (static); Docker image optional skipped
- [x] GHCR + prod safety greps still green
- [x] Evidence recorded in this file

---

*End of Phase 5 done. No application source fixes required. Ready for Phase 6 PR (do not auto-create).*
