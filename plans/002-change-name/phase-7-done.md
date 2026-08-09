# Phase 7 — Done: rename archaeology polish (minimal)

**Status:** **PASS** (docs banners + tunnel nit; GHCR/compose untouched)  
**Date:** 2026-08-09  
**Branch:** `chore/phase-7-rename-docs-polish`  
**Base:** `main`  
**Repo:** `proxeon/lazuar-pay`  
**PR:** [#13](https://github.com/proxeon/lazuar-pay/pull/13) (merged `003e32b`)  
**Related:** [`phase-7-analysis.md`](./phase-7-analysis.md), [`11-implementation-checklist.md`](./11-implementation-checklist.md) § Phase 7

---

## 1. Summary

| Item | Result |
|------|--------|
| Gaps index rename banner | **DONE** (`docs/001-gaps/README.md`) |
| ADR 013 path note | **DONE** |
| ADR 017 path note | **DONE** |
| ADR 007 path note | **DONE** |
| Bulk gap body rewrite | **SKIPPED** (historical snapshots) |
| Rename `04-developers-page-dx.md` | **SKIPPED** (link churn) |
| Local compose developers parity | **SKIPPED — already done Phase 2** |
| GHCR image rebrand | **SKIPPED forever (plan 002)** |
| `tunnel:fe` retarget portal :3004 | **DONE** |
| README multi-domain tree polish | **SKIPPED** (optional; not blocking) |
| Naming debt documented | **DONE** (this file §3) |
| Deploy / Docker / bake / prod | **UNTOUCHED** |
| PR merge | **PASS** [#13](https://github.com/proxeon/lazuar-pay/pull/13) → `003e32b` |

**Overall Phase 7:** **PASS** — minimal DX polish only; rename project closed.

---

## 2. Changes shipped

### 2.1 Documentation banners

| File | Banner |
|------|--------|
| `docs/001-gaps/README.md` | Frontend path rename map (`*-page` → `lazuar-*`) |
| `docs/architecture-decision-log/013-frontend-module-implementation.md` | `ops-page` → `lazuar-ops` |
| `docs/architecture-decision-log/017-portal-frontend-architecture.md` | `portal-page` → `lazuar-portal` |
| `docs/architecture-decision-log/007-product-scoped-api-references.md` | `developers-page` → `lazuar-developers` |

No ADR titles rewritten. No gap report bodies edited.

### 2.2 Taskfile

```yaml
tunnel:fe:
  desc: Start ngrok tunnel for lazuar-portal (Next.js) on port 3004
  cmds:
    - ngrok http 3004
```

(was: community-page on port 3020 — Aura leftover; community app removed per ADR 022)

### 2.3 Plan bookkeeping

- `phase-7-analysis.md` committed
- `11-implementation-checklist.md` Phase 7 items marked done vs skipped
- this `phase-7-done.md`

---

## 3. Naming debt that stays by design

Do **not** “fix” by renaming. These layers are intentional:

| Layer A | Layer B | Why OK |
|---------|---------|--------|
| Backend `Modules/Ops`, routes `/api/v1/ops` | App `lazuar-ops` | Different domains (backend module vs FE package) |
| Public path `/docs` (Developer Hub UI) | App `lazuar-docs` (VitePress guides) | Different products; `/docs` is Scalar hub (`lazuar-developers`) |
| GHCR `lazuar-hub-superadmin` | App `lazuar-admin` | Image name frozen; folder matches UI “Admin” |
| Prod containers `hub-*` / compose services short names | Local compose `lazuar-*` | Prod short names + Caddy DNS; local monorepo names |
| GHCR `lazuar-hub-*` prefix | App folders `lazuar-*` | Deploy brand vs monorepo package names |
| Package `lazuar-developers` | `packages/api-spec` | Spec SSoT vs Scalar UI — do **not** rename app to `lazuar-spec` |

GHCR rebrand (e.g. `lazuar-hub-superadmin` → `lazuar-hub-admin`) remains a **separate product-branding project** if ever needed — dual-tag + prod cutover playbook, not incomplete rename debt.

---

## 4. Explicit non-changes

| Surface | Action |
|---------|--------|
| `deploy/**` | none |
| `docker-bake.hcl` / `docker-compose*.yml` | none |
| `.github/workflows/ghcr.yml` | none |
| App source / package names | none |
| Gap report bodies under `docs/001-gaps/0*.md` | none |
| Pure-history ADRs (014, 016, 018, 022, 023, …) | none |

---

## 5. Verification

```bash
rg -n 'plan 002|Path note \(plan 002' \
  docs/001-gaps/README.md \
  docs/architecture-decision-log/007-product-scoped-api-references.md \
  docs/architecture-decision-log/013-frontend-module-implementation.md \
  docs/architecture-decision-log/017-portal-frontend-architecture.md

rg -n '3020|community-page' Taskfile.yml
# → no matches

git diff --stat
# expect: docs + Taskfile + plans only
```

---

## 6. Exit criteria

- [x] Gaps index has rename map banner
- [x] ADR 013 + 017 + 007 have path notes
- [x] GHCR rebrand **not** started
- [x] Compose parity confirmed already done
- [x] Naming debt documented (this file)
- [x] `tunnel:fe` no longer references community-page :3020

---

*End of Phase 7. Plan 002 rename is complete; optional polish closed.*
