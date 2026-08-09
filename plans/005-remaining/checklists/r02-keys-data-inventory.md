# R02 — Keys data inventory

**Track:** Keys · **Analysis:** `../01-api-key-one-only-cutover.md`, `../../004-maintenance/api-key-cutover-design.md`  
**Goal:** Staging/prod counts; early-cutover decision.  
**Notes:** `../r02-notes.md` · **SQL handoff:** `../r02-inventory.sql`  
**Date:** 2026-08-09 · **Branch:** `chore/remaining-005`

---

## R02.1 Queries (run staging then prod)

- [x] Count active `lhdn."DeveloperApiKeys"` where `IsActive = true` — **SQL ready** / **ops pending** (staging+prod)
- [x] Count active `one."ApiCredentials"` where `IsActive = true` — **SQL ready** / **ops pending**
- [x] Count **active_legacy_only**: Lhdn active hash **not** in One — **SQL ready** / **ops pending** (cutover blocker)
- [x] Count inactive Lhdn rows (migrate all vs active-only — record choice: **`all_rows` recommended**) — **SQL ready** / **ops pending**
- [x] Sample scopes not in One allowlist (quarantine list) — **SQL ready** (Q12 + sample); allowlist source **`PlatformApiScopes.AllKnownScopes`**; **ops pending** on staging/prod

## R02.2 Record results

| Env | Active Lhdn | Active One | Active legacy-only | Notes |
|-----|-------------|------------|--------------------|-------|
| Staging | **blocked** | **blocked** | **blocked** | Needs ops DB access |
| Prod | **blocked** | **blocked** | **blocked** | Needs ops DB access |
| Local (`lazuar_mvp`) | 0 | 0 | 0 | Optional; empty tables; not a prod substitute |

## R02.3 Decision

- [ ] If prod **active_legacy_only = 0**: mark **accelerate** → R03 may be no-op / verify-only; R05 can proceed after staging One-only smoke — **not claimed** (prod counts blocked)
- [x] If prod **active_legacy_only > 0**: R03 migrator required before R05 — **treated as migrator required (safe path; counts pending ops)**
- [ ] Sign-off for accelerate: **N/A** (name/date) if used

**Decision:** **Migrator required (safe path; counts pending ops).**  
**R03 GO** implement full migrator.  
**R05 blocked** until prod `active_legacy_only = 0` (after R04).  
**migrate_policy:** `all_rows`.  
**Allowlist:** `PlatformApiScopes.AllKnownScopes`.

## R02.4 Exit

- [x] Numbers status committed in plan note (`../r02-notes.md`) — staging/prod blocked; local zeros recorded
- [x] R03 go / no-go clear: **GO implement migrator**
