# R04 — Execute key migration (staging then prod) notes

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** Keys  
**Checklist:** `checklists/r04-keys-migrate-staging-prod.md`  
**Depends on:** R03 (migrator + runbook), R02 inventory package  
**Analysis:** `01-api-key-one-only-cutover.md`  
**Runbook:** `r03-keys-migrator-runbook.md`  
**Verify SQL:** `r02-inventory.sql`  
**Scope:** Docs-only this pass — no application code changes; no staging/prod execute from this workstation.

---

## Summary

| Concern | State |
|---------|--------|
| Migrator (R03) | Implemented; dual-read unchanged |
| Local inventory | Empty (Q1–Q12 all 0) — migrate is no-op; not a substitute for staging/prod |
| Staging execute | **Pending ops** — DB access / change window not available here |
| Prod execute | **Pending ops** — same |
| R05 prod One-only | **BLOCKED** until prod **Q8 `active_legacy_only = 0`** (or signed residual quarantine) |
| R05 feature-branch code | Allowed behind gates; do **not** claim prod cutover or uncheck R04.3 / R05 exit |

**Do not** invent staging/prod counts. Paste real Q1–Q12 results after ops runs `r02-inventory.sql`.  
**Do not** put secrets, connection strings, or full `KeyHash` values in this note (prefix `left(KeyHash, 12)` only if ops samples are needed).

---

## Execution status matrix

| Env | Preflight inventory | Migrator dry-run | Migrator live | Post-verify Q8 | Auth smoke | R04 status |
|-----|---------------------|------------------|---------------|----------------|------------|------------|
| Local (`lazuar_mvp` @ `lazuar-postgres:5433`) | Empty (see R02) | N/A / no-op | N/A / no-op | 0 | N/A (no legacy keys) | **Local empty** — verified inventory only |
| Staging | **Pending ops** | **Pending ops** | **Pending ops** | **Pending ops** | **Pending ops** | **Pending ops** |
| Prod | **Pending ops** | **Pending ops** | **Pending ops** | **Pending ops** | **Pending ops** | **Pending ops** |

Local row reference (R02, 2026-08-09): Q1–Q12 all **0**. Re-run if local DB is reseeded with legacy rows.

---

## Ops run package

Hand this package to whoever has staging/prod DB + deploy access. Full flag and outcome-code detail: `r03-keys-migrator-runbook.md`.

### Prerequisites

- [ ] R03 migrator present on the build being deployed
- [ ] Dual-read middleware **still enabled** (R05 not live on this env)
- [ ] DB backup / PITR note recorded for the target env
- [ ] `migrate_policy` = **`all_rows`** (R02 decision)
- [ ] Connection only via existing ops secrets — **never** paste into this repo note

### Env flags

| Flag | Dry-run | Live insert | After success |
|------|---------|-------------|----------------|
| `API_KEY_MIGRATION_ENABLED` | `true` | `true` | set back to `false` (or omit) |
| `API_KEY_MIGRATION_DRY_RUN` | `true` | `false` | n/a |

Config section equivalent: `ApiKeyMigration:Enabled` / `ApiKeyMigration:DryRun` (env overrides section). Host registers the job only when `Enabled=true`.

### Steps (staging first, then prod)

1. **Preflight inventory** — run `plans/005-remaining/r02-inventory.sql` against target DB; record Q1–Q12 in the results table below (no secrets).
2. **Snapshot** — note backup / PITR approach for the env.
3. **Dry-run** — deploy or restart API with `ENABLED=true`, `DRY_RUN=true`; capture log summary:
   `DryRun=True Processed=… WouldInsert=… AlreadyMigrated=… Quarantined=…`
4. **Live insert** — `DRY_RUN=false`; one restart; job runs shortly after boot.
5. **Disable job** — `ENABLED=false` after successful run (idempotent re-run is safe but noisy).
6. **Post-verify** — re-run `r02-inventory.sql`; gate for that env: **Q8 `active_legacy_only = 0`** (or signed residual list + quarantine fixes).
7. **Auth smoke** — former Lhdn-only key (if any) still authenticates via dual-read / One; mint new One key; revoke → cache eviction → 401.
8. **Monitor** (prod) — 401 rates ~24h while dual-read remains on.
9. **Quarantine** — fix `quarantine_empty_hash` / `quarantine_orphan_org` / `quarantine_unknown_scopes_only` (and review `hash_collision_different_org`) before claiming R04.3.

### Verify query (cutover gate metric)

From `r02-inventory.sql` (Q8):

```sql
SELECT COUNT(*) AS active_legacy_only
FROM lhdn."DeveloperApiKeys" d
WHERE d."IsActive" = true
  AND NOT EXISTS (
    SELECT 1 FROM one."ApiCredentials" a WHERE a."KeyHash" = d."KeyHash"
  );
```

Target: **0** on that env before One-only middleware is enabled there.

---

## Results (fill after ops — do not invent)

| Env | Q1 active Lhdn | Q4 active One | **Q8 active_legacy_only** | Q2 inactive Lhdn | Dry-run WouldInsert | Live inserted | Quarantined | Notes |
|-----|----------------|---------------|---------------------------|------------------|---------------------|---------------|-------------|-------|
| Staging | _pending ops_ | _pending ops_ | _pending ops_ | _pending ops_ | _pending ops_ | _pending ops_ | _pending ops_ | Needs ops DB + deploy |
| Prod | _pending ops_ | _pending ops_ | _pending ops_ | _pending ops_ | _pending ops_ | _pending ops_ | _pending ops_ | Needs ops DB + change window |
| Local | 0 | 0 | 0 | 0 | n/a | n/a | n/a | Empty dataset (R02) |

---

## R05 posture

| Claim | Allowed? |
|-------|----------|
| Prod One-only middleware **live** / R04.3 exit / “R05 unblocked for prod” | **No** until prod **Q8 = 0** (or signed residual quarantine) after R04 execute |
| Staging One-only after staging Q8 = 0 | Yes for staging-only validation; still does not unblock prod |
| Feature-branch **R05 code** (remove Lhdn dual-read + dual revoke subscribe) | **Allowed** on branch with gates: do not merge/enable on prod until R04.3; keep dual-read on deployed prod until gate |
| Accelerate without migrator | Only if ops inventory proves prod active legacy-only already 0 (R02 early-cutover); **not claimed** — counts still pending ops |

**Invariant:** Dual-read stays on every env that may still have active legacy-only hashes. R04 does **not** ship One-only middleware.

---

## Safety

- Hash-row copy only — no plaintext API keys in logs or this note.
- Per-row insert; re-run idempotent on `KeyHash`.
- Failure leaves dual-read valid; do **not** drop `lhdn.DeveloperApiKeys` (R06 after R05 + ≥30d).
- Rollback before R05: leave dual-read; optionally delete bad One inserts by ops report `TargetId` (runbook).

---

## R04 exit (this docs pass)

- [x] Execution status matrix: local empty; staging/prod **pending ops**
- [x] Ops run package documented (flags, steps, `r02-inventory.sql` verify)
- [x] R05 posture: prod One-only **BLOCKED** until Q8 = 0; feature-branch R05 code allowed with gates
- [x] No secrets; no invented staging/prod counts
- [ ] Staging migrate + verify (ops)
- [ ] Prod migrate + verify (ops)
- [ ] R04.3 — prod Q8 = 0 (or signed residual) — **not claimed**

---

## Next

1. **Ops:** execute package on **staging**, then **prod**; paste counts into results table.  
2. **R05:** code may proceed on feature branch behind gates; **prod One-only deploy blocked** until R04.3.  
3. Checklist: `checklists/r04-keys-migrate-staging-prod.md` — R04.0 local empty checked; staging/prod pending ops; R04.3 unchecked.
