# Phase 3 — Done

**Date:** 2026-08-09  
**Branch:** `chore/rename-frontend-apps-lazuar-prefix`  
**Commit message:** `chore(dev): point mprocs at lazuar-* frontend apps`

---

## What landed

| Area | Change |
|------|--------|
| `mprocs-dev.yaml` | Process keys + `cd` paths: `*-page` → `lazuar-{developers,ops,admin,portal}`; ngrok procs unchanged; `autostart` unchanged |
| `Taskfile.yml` | Optional polish only: `docker:build` echo lists bake targets `(api, lazuar-portal, lazuar-ops, lazuar-admin, lazuar-developers)` |
| Root `package.json` / `scripts/` / turbo / workspace | **No edits** — already clean (no `*-page` filters) |
| App-local README / AGENTS / CLAUDE | **No edits** — already clean under the four apps |

**Not touched (by design / Phase 4+):**

- Living docs (`README.md`, `apps/lazuar-docs/**`, `docs/contracts/**`) still mention old names
- `pnpm-lock.yaml` importers still stale
- `tunnel:fe` community-page / port 3020 (pre-existing; out of scope)
- `deploy/prod/**`, GHCR image names, public URL paths

---

## Mapping applied (`mprocs-dev.yaml`)

| Old process key / path | New |
|------------------------|-----|
| `developers-page` / `apps/developers-page` | `lazuar-developers` / `apps/lazuar-developers` |
| `ops-page` / `apps/ops-page` | `lazuar-ops` / `apps/lazuar-ops` |
| `superadmin-page` / `apps/superadmin-page` | `lazuar-admin` / `apps/lazuar-admin` |
| `portal-page` / `apps/portal-page` | `lazuar-portal` / `apps/lazuar-portal` |

Shells remain `cd apps/<name> && pnpm dev` (not filter-based). Ports stay in each app `package.json` (3002–3005).

---

## Verification (grep proof)

### Stale names gone from mprocs

```bash
rg -n 'developers-page|ops-page|portal-page|superadmin-page' mprocs-dev.yaml
# → no matches
```

### New keys + paths present

```bash
rg -n 'lazuar-(developers|ops|admin|portal)' mprocs-dev.yaml
```

```text
lazuar-developers: + cd apps/lazuar-developers
lazuar-ops: + cd apps/lazuar-ops
lazuar-admin: + cd apps/lazuar-admin
lazuar-portal: + cd apps/lazuar-portal
```

### Tooling surfaces clean (no FE `*-page`)

```bash
rg -n 'developers-page|ops-page|portal-page|superadmin-page' \
  mprocs-dev.yaml Taskfile.yml package.json scripts/ script/ \
  turbo.json pnpm-workspace.yaml
# → no matches
```

### Folders exist

```bash
test -d apps/lazuar-developers && test -d apps/lazuar-ops \
  && test -d apps/lazuar-portal && test -d apps/lazuar-admin \
  && echo "dirs OK"
# → dirs OK
```

### Deferred (still stale — Phase 4)

| Surface | Examples |
|---------|----------|
| Root `README.md` | architecture tree + port table `ops-page` / `portal-page` |
| `apps/lazuar-docs/**` | e.g. `pnpm --filter developers-page`, path tables |
| `pnpm-lock.yaml` | importers `apps/*-page` |
| Historical `docs/001-gaps/**`, ADRs | leave until Phase 7 |

Checklist §3.5 living-docs checkbox left **open** intentionally (Phase 4 owns it).

---

## Local workflow after Phase 3

```bash
task infra:up
task dev          # API
task fe           # mprocs: lazuar-developers, lazuar-ops, lazuar-admin, lazuar-portal
# http://localhost:3002 developers
# http://localhost:3003 ops
# http://localhost:3004 portal
# http://localhost:3005 admin
```

---

## Checklist

Phase 3 items in [`11-implementation-checklist.md`](./11-implementation-checklist.md) §3.1–§3.4 and mprocs exit criterion marked complete. Living-docs exit criterion deferred to Phase 4.

---

*End of Phase 3 done report.*
