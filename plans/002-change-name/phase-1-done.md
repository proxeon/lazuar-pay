# Phase 1 — Done

**Date:** 2026-08-09  
**Branch:** `chore/rename-frontend-apps-lazuar-prefix`  
**Commit message:** `chore(apps): rename frontend apps to lazuar-* prefix`

---

## What landed

| From | To | package.json `"name"` |
|------|----|----------------------|
| `apps/developers-page` | `apps/lazuar-developers` | `lazuar-developers` |
| `apps/ops-page` | `apps/lazuar-ops` | `lazuar-ops` |
| `apps/portal-page` | `apps/lazuar-portal` | `lazuar-portal` |
| `apps/superadmin-page` | `apps/lazuar-admin` | `lazuar-admin` |

Optional (done):

- Path-header comments under ops / portal / admin (including AutoForm `chat` → `forms` fix)
- Superadmin stale `ops-page` headers → `lazuar-admin`
- Backend comment-only `ops-page` → `lazuar-ops` in `Endpoints.cs` and `SystemGenesisBootstrapperJob.cs`

**Not touched (by design):** Dockerfiles, docker-bake, compose, mprocs, lockfile, README/living docs, ghcr.yml, deploy/

---

## Verification

### Directories

```text
$ ls apps/
lazuar-admin
lazuar-api
lazuar-developers
lazuar-docs
lazuar-ops
lazuar-portal
```

```bash
test -d apps/lazuar-developers && test -d apps/lazuar-ops \
  && test -d apps/lazuar-portal && test -d apps/lazuar-admin \
  && test ! -e apps/developers-page && test ! -e apps/ops-page \
  && test ! -e apps/portal-page && test ! -e apps/superadmin-page \
  && echo "dirs OK"
# → dirs OK
```

### Package names

```text
apps/lazuar-developers/package.json → lazuar-developers  OK
apps/lazuar-ops/package.json        → lazuar-ops         OK
apps/lazuar-portal/package.json     → lazuar-portal      OK
apps/lazuar-admin/package.json      → lazuar-admin       OK
```

### Path headers

```bash
rg -n '// apps/(ops|portal|developers|superadmin)-page/' \
  apps/lazuar-ops apps/lazuar-portal apps/lazuar-admin apps/lazuar-developers \
  --glob '!node_modules/**' --glob '!dist/**' --glob '!.next/**'
# → no matches
```

### Backend comments

```bash
rg -n 'ops-page' \
  apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs \
  apps/lazuar-api/Modules/One/Infrastructure/Workers/SystemGenesisBootstrapperJob.cs
# → no matches (both updated to lazuar-ops)
```

---

## Expected mid-PR breakage

Until Phase 2–4: Dockerfiles, bake, compose, mprocs, root lockfile importers, and some living docs still reference `*-page`. Do **not** merge Phase 1 alone. Do **not** run `pnpm install` until the lockfile phase.

**Next:** Phase 2 — Docker / bake / compose / ghcr paths.
