# Phase 4 — Done

**Date:** 2026-08-09  
**Branch:** `chore/rename-frontend-apps-lazuar-prefix`  
**Commit message:** `docs: update living docs and lockfile for lazuar-* apps`

---

## What landed

| Area | Change |
|------|--------|
| `pnpm-lock.yaml` | Regenerated via `pnpm install` only — importers `apps/*-page` → `apps/lazuar-{developers,ops,portal,admin}` |
| Root `README.md` | Product bullets, structure tree (+ developers + docs), ports table, glossary blurb |
| `apps/lazuar-docs/**` | README path table; `openapi.md` filter command; `index.md` prose; `how-to-maintain.md` prod URL note |
| `docs/contracts/openapi-vs-minimal-api.md` | Product-route + residual-UI labels → `lazuar-ops` / `lazuar-portal` / `lazuar-developers` |
| `plans/001-backend/001-backend-solidification-checklist.md` | App identity labels for residual work |

**Not touched (by design):**

- `docs/001-gaps/**` (historical gap snapshots)
- `docs/architecture-decision-log/**` (ADRs)
- `plans/002-change-name/**` inventory/analyses (except checklist + this done note)
- GHCR image names (`lazuar-hub-*`), prod compose/Caddy paths
- App source / Docker / mprocs (Phases 1–3)

---

## Naming map (applied)

| Old folder / package / filter | New |
|-------------------------------|-----|
| `developers-page` | **`lazuar-developers`** |
| `ops-page` | **`lazuar-ops`** |
| `portal-page` | **`lazuar-portal`** |
| `superadmin-page` | **`lazuar-admin`** |

---

## Verification

### Lockfile importers

```bash
rg -n '^  apps/(developers-page|ops-page|portal-page|superadmin-page):' pnpm-lock.yaml
# → no matches

rg -n '^  apps/lazuar-(developers|ops|portal|admin):' pnpm-lock.yaml
```

```text
apps/lazuar-admin:
apps/lazuar-developers:
apps/lazuar-ops:
apps/lazuar-portal:
```

### Filter smoke

```bash
pnpm --filter lazuar-developers exec node -e "console.log('ok developers')"
pnpm --filter lazuar-ops exec node -e "console.log('ok ops')"
pnpm --filter lazuar-portal exec node -e "console.log('ok portal')"
pnpm --filter lazuar-admin exec node -e "console.log('ok admin')"
# → ok developers / ops / portal / admin
```

### Living docs clean of old tokens

```bash
rg -n 'developers-page|ops-page|portal-page|superadmin-page' \
  README.md apps/lazuar-docs docs/contracts plans/001-backend
# → no matches
```

### Critical command fixed

`apps/lazuar-docs/docs/reference/openapi.md`:

```bash
pnpm --filter lazuar-developers dev
```

### Glossary (README)

> **Monorepo app names:** `lazuar-ops`, `lazuar-portal`, `lazuar-admin`, `lazuar-developers`.  
> TypeSpec SSoT remains `packages/api-spec`. GHCR images remain `lazuar-hub-*`. Public hub paths unchanged (`/`, `/portal`, `/docs`, `/admin`).

---

## Allowed remaining (historical — Phase 7 optional)

These still mention `*-page` and are **not** living onboarding commands:

- `docs/001-gaps/**` (including filename `04-developers-page-dx.md`)
- `docs/architecture-decision-log/**` (e.g. 007, 012–014, 017)
- `plans/002-change-name/**` inventory + phase analyses/done notes

```bash
rg -n 'developers-page|ops-page|portal-page|superadmin-page' \
  --glob '!**/node_modules/**' \
  docs/001-gaps docs/architecture-decision-log plans/002-change-name
```

---

## Phase 4 exit criteria

- [x] `pnpm install` clean; lockfile importers use `apps/lazuar-*` for the four frontends
- [x] New contributor can find apps by new names in root `README.md` (tree + ports)
- [x] No living command uses `--filter developers-page` (etc.) — especially openapi.md
- [x] Contracts SOP + backend checklist app labels updated
- [x] Historical leftovers listed as allowed remaining (not blocking)

---

## Next

**Phase 5** — local / Docker verification before merge (`task fe`, port smoke, optional bake).
