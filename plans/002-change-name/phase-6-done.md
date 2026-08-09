# Phase 6 — Done: PR merge + production observe

**Status:** **PASS** (PR merged; GHCR matrix + deploy green; hub healthy)  
**Date:** 2026-08-09  
**Branch (source):** `chore/rename-frontend-apps-lazuar-prefix`  
**Base:** `main`  
**Repo:** `proxeon/lazuar-pay`  
**Related:** [`phase-6-analysis.md`](./phase-6-analysis.md), [`11-implementation-checklist.md`](./11-implementation-checklist.md) § Phase 6

---

## 1. Summary

| Step | Result |
|------|--------|
| Commit `phase-6-analysis.md` | **PASS** (`ba77321`) |
| Push branch | **PASS** |
| Open PR | **PASS** [#12](https://github.com/proxeon/lazuar-pay/pull/12) |
| Merge style | **merge commit** (matches repo history: `Merge pull request #N`) |
| Merge to `main` | **PASS** |
| Merge SHA | `ea2d5d9a4fd993efddd48e50593eaee1507a5732` |
| Deployed `VERSION` | `sha-ea2d5d9` |
| GHCR build matrix ×5 | **PASS** |
| Deploy hub VPS | **PASS** |
| Health-gate | **PASS** (all hub-* healthy) |
| Public smoke | **PASS** (see §4; deploy-local smoke + external check) |
| PR `ci.yml` contracts | **FAIL** (pre-existing pnpm version mismatch — not rename) |
| `ci.yml` dotnet on main | **PASS** |
| Prod compose service rename | **None** (by design) |

**Overall Phase 6:** **PASS** — rename shipped; production rebuilt from new Dockerfile paths under **unchanged** GHCR image names.

---

## 2. PR

| Field | Value |
|-------|--------|
| URL | https://github.com/proxeon/lazuar-pay/pull/12 |
| Title | `chore: rename frontend apps to lazuar-* prefix` |
| Number | 12 |
| Merged at | 2026-08-09T00:06:17Z |
| Merge commit | `ea2d5d9a4fd993efddd48e50593eaee1507a5732` |
| Head (pre-merge tip) | `ba773210dd19945b0567dfbc2fe0885b3ce5956d` |

### PR body content delivered

- Mapping table (old `*-page` → `lazuar-*`, GHCR/prod/public paths **unchanged**)
- Explicit non-goals (GHCR `lazuar-hub-*`, `deploy/prod`, Caddy paths)
- Phase 5 verification checklist
- Deploy impact note (merge triggers GHCR + SSH deploy)
- Link to `plans/002-change-name/`

### PR CI (pre-merge)

| Job | Result | Notes |
|-----|--------|-------|
| contracts | **failure** | `pnpm/action-setup@v4`: Action `version: 9` vs `package.json` `packageManager: pnpm@11.5.2` — **pre-existing**, unrelated to rename. Same class of failure on recent main CI runs. |
| dotnet | in progress at merge time / later **success** on main push | Unaffected by FE path rename |

Merge proceeded with deploy risk acknowledged (user-authorized free merge; no enforced branch protection API on this private free-tier repo).

---

## 3. Post-merge Actions

### 3.1 GHCR + deploy

| Field | Value |
|-------|--------|
| Workflow | **GHCR + deploy** (`.github/workflows/ghcr.yml`) |
| Run URL | https://github.com/proxeon/lazuar-pay/actions/runs/31285457904 |
| Run ID | `31285457904` |
| Trigger | push to `main` (merge commit) |
| Duration | ~3m55s |
| Conclusion | **success** |

| Job | Conclusion |
|-----|------------|
| Build `lazuar-hub-api` | success |
| Build `lazuar-hub-ops` | success |
| Build `lazuar-hub-portal` | success |
| Build `lazuar-hub-superadmin` | success |
| Build `lazuar-hub-developers` | success |
| Deploy hub VPS | success |

**Proof:** first full multi-stage FE image builds for `apps/lazuar-{portal,ops,admin,developers}/Dockerfile` succeeded in CI (local bake was skipped in Phase 5).

### 3.2 Deploy health-gate (from Deploy job log)

```text
✓ hub-api healthy
✓ hub-ops healthy
✓ hub-portal healthy
✓ hub-superadmin healthy
✓ hub-developers healthy
✓ hub-caddy running
▶ done VERSION=sha-ea2d5d9
```

Containers (excerpt):

| Name | Status | Image |
|------|--------|-------|
| hub-api | healthy | `ghcr.io/proxeon/lazuar-hub-api:sha-ea2d5d9` |
| hub-ops | healthy | `ghcr.io/proxeon/lazuar-hub-ops:sha-ea2d5d9` |
| hub-portal | healthy | `ghcr.io/proxeon/lazuar-hub-portal:sha-ea2d5d9` |
| hub-superadmin | healthy | `ghcr.io/proxeon/lazuar-hub-superadmin:sha-ea2d5d9` |
| hub-developers | healthy | `ghcr.io/proxeon/lazuar-hub-developers:sha-ea2d5d9` |
| hub-caddy | running | `caddy:2-alpine` |

Local Host-header smoke inside deploy reported HTTP **308** for `/health`, `/`, `/portal`, `/docs` (HTTPS redirect — expected for that smoke style; containers already health-gated).

### 3.3 Main branch CI (push after merge)

| Run | Conclusion | Notes |
|-----|------------|-------|
| https://github.com/proxeon/lazuar-pay/actions/runs/31285457902 | **failure** | contracts fail (pnpm mismatch); **dotnet success** |

Not a rename regression; recent main CI history already red on contracts.

---

## 4. Public smoke

External public HTTPS checks post-deploy (Python urllib, 2026-08-09):

| URL | HTTP |
|-----|------|
| `https://hub.lazuar.com/health` | **200** |
| `https://hub.lazuar.com/` | **200** |
| `https://hub.lazuar.com/portal` | **200** |
| `https://hub.lazuar.com/docs` | **200** |
| `https://hub.lazuar.com/admin` | **200** |
| `https://hub.lazuar.com/admin/` | **200** |

Primary production acceptance: **Actions deploy health-gate + public smoke 200** on `sha-ea2d5d9` images. No emergency compose rename required.

---

## 5. Mapping shipped (unchanged prod surfaces)

| Old | New | GHCR (unchanged) | Prod service (unchanged) | Public path (unchanged) |
|-----|-----|------------------|--------------------------|-------------------------|
| `developers-page` | `lazuar-developers` | `lazuar-hub-developers` | `hub-developers` | `/docs` |
| `ops-page` | `lazuar-ops` | `lazuar-hub-ops` | `hub-ops` | `/` |
| `portal-page` | `lazuar-portal` | `lazuar-hub-portal` | `hub-portal` | `/portal` |
| `superadmin-page` | `lazuar-admin` | `lazuar-hub-superadmin` | `hub-superadmin` | `/admin` |

---

## 6. Phase 6 exit criteria

- [x] PR opened with mapping, non-goals, verification, plan link
- [x] Merge decision made with deploy risk acknowledged
- [x] GHCR matrix + deploy green
- [x] Hub health-gate OK; images `sha-ea2d5d9`
- [x] No emergency prod compose service rename

---

## 7. Follow-ups (not Phase 6)

- **Optional Phase 7** history docs / ADRs / gap archaeology — not started
- **CI hygiene:** align `ci.yml` `pnpm/action-setup` version with `packageManager` field (pre-existing red contracts on main)
- No rollback needed

---

*End of Phase 6 done. Frontend rename is live on hub production via rebuild, not architecture rename.*
