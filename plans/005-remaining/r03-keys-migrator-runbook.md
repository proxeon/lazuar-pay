# R03 — API key migrator runbook

**Track:** Keys · **Checklist:** `checklists/r03-keys-migrator-implement.md`  
**Depends on:** R01 (code inventory), R02 (data inventory)  
**Does not:** Remove dual-read middleware (that is **R05**)

---

## What it does

One-shot hosted job that **copies** rows from `lhdn.DeveloperApiKeys` → `one.ApiCredentials` (hash rows only; **no plaintext secrets**).

| Field | Action |
|-------|--------|
| `Id` | Prefer preserve; if One already has that Id with a **different** `KeyHash` → `Guid.CreateVersion7()` + log remap |
| `OrganizationId`, `Name`, `Prefix`, `KeyHash`, `KeyHint`, `IsActive`, `CreatedAt` | Copy as-is (`KeyHint` empty → `****`) |
| `Scopes` | Keep known tokens via `PlatformApiScopes.Split` + `IsKnownScope`; drop unknown (log); unknown-only / empty → **quarantine** |
| `CreatedByUserId` | Always `NULL` for migrated rows |

**Idempotency:** skip when `KeyHash` already exists on One.  
**Hash + different org on One:** skip with code `hash_collision_different_org` (do not overwrite).  
**Race safety:** `INSERT … ON CONFLICT ("KeyHash") DO NOTHING`.  
**Policy:** migrate **all** rows (`all_rows`), not only active. Inactive rows keep `IsActive = false`.

Code: `apps/lazuar-api/src/Lazuar.Api/Jobs/ApiKeyMigration/`.

---

## Configuration

| Source | Keys |
|--------|------|
| `appsettings.json` section `ApiKeyMigration` | `Enabled` (default **false**), `DryRun` (default **true**), `BatchSize` (default **500**) |
| Environment (overrides section) | `API_KEY_MIGRATION_ENABLED`, `API_KEY_MIGRATION_DRY_RUN` |

Host registers the hosted service **only when `Enabled=true`** (after env override). Dual-read auth is never changed by this job.

---

## How to run

### 1. Dry-run (recommended first)

```bash
export API_KEY_MIGRATION_ENABLED=true
export API_KEY_MIGRATION_DRY_RUN=true
# start API against the target DB (staging then prod)
```

Watch logs for:

```text
API key migration finished. DryRun=True Processed=… WouldInsert=… AlreadyMigrated=… Quarantined=…
```

Quarantine / collision rows log `SourceId` + `Code` + optional `Detail` — **never** plaintext keys.

### 2. Live insert

```bash
export API_KEY_MIGRATION_ENABLED=true
export API_KEY_MIGRATION_DRY_RUN=false
```

Restart the API once. Job runs a few seconds after boot (post-migration settle). Set `Enabled=false` again after a successful run so the one-shot does not re-register on every deploy (re-run is safe/idempotent but noisy).

### 3. Verify (SQL)

Use `plans/005-remaining/r02-inventory.sql`. Target gate for R05:

```sql
-- active legacy-only should be 0 (or residual signed off)
SELECT COUNT(*) AS active_legacy_only
FROM lhdn."DeveloperApiKeys" d
WHERE d."IsActive" = true
  AND NOT EXISTS (
    SELECT 1 FROM one."ApiCredentials" a WHERE a."KeyHash" = d."KeyHash"
  );
```

### 4. Auth smoke

- Known legacy plain key (if fixture exists) still authenticates (One-first dual-read hits migrated hash).
- Mint new One key still works.
- Revoke migrated credential via One / Lhdn façade → cache eviction → 401.

---

## Outcome codes

| Code | Meaning |
|------|---------|
| `inserted` | Row written to One |
| `would_insert` | Dry-run would write |
| `already_migrated` | Same `KeyHash` already on One (same org) |
| `hash_collision_different_org` | Same `KeyHash` on One for another org — **skip / security review** |
| `insert_conflict` | `ON CONFLICT DO NOTHING` race |
| `quarantine_empty_hash` | Null/blank `KeyHash` |
| `quarantine_orphan_org` | `OrganizationId` missing from `one.Organizations` |
| `quarantine_unknown_scopes_only` | No known `PlatformApiScopes` tokens |

Partial scopes: insert with known subset; `Detail` starts with `dropped_scopes:…`.

---

## Safety / transactions

- **Per-row** insert (not one giant transaction): a mid-run failure leaves already-inserted rows on One; dual-read stays valid; re-run is idempotent on `KeyHash`.
- Failure of the hosted job is logged; it does **not** crash the host or touch middleware.
- **Never** log full plain API keys. Ops samples may use `left(KeyHash, 12)` only if needed.

---

## Rollback

| State | Action |
|-------|--------|
| After dry-run | Nothing written; dual-read unchanged |
| After live insert, before R05 | Leave dual-read on. Optionally delete wrongly inserted One rows by report `TargetId` / source mapping |
| Do **not** drop `lhdn.DeveloperApiKeys` here | That is R06 after R05 + monitoring window |

---

## Exit (R03)

- [x] Hosted one-shot migrator implemented
- [x] Dual-read still enabled
- [x] Unit tests for `LegacyApiKeyMigrator` (in-memory fake store)
- Ready for **R04** execute on staging then prod
