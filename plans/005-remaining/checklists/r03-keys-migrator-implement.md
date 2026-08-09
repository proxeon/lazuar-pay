# R03 — Implement API key migrator

**Track:** Keys · **Analysis:** `../01-api-key-one-only-cutover.md` § migration  
**Depends on:** R01, R02  
**Do not:** Remove dual-read (R05)  
**Runbook:** `../r03-keys-migrator-runbook.md`

---

## R03.1 Implementation choice

- [x] Choose: hosted one-shot job **or** ops SQL/script **or** admin command: **hosted one-shot job** (`LegacyApiKeyMigrationHostedService` under `Lazuar.Api/Jobs/ApiKeyMigration/`, gated by `ApiKeyMigration:Enabled` / `API_KEY_MIGRATION_ENABLED`)
- [x] Idempotent on `KeyHash` (skip if already in One)
- [x] Copy fields: KeyHash, Prefix, KeyHint, Scopes, OrganizationId, Name, IsActive, CreatedAt
- [x] CreatedByUserId = null for migrated
- [x] Preserve Id if design requires; else new Guid (document choice: **prefer preserve source Id**; on Id collision with different KeyHash → `Guid.CreateVersion7()` + remap flag)
- [x] Scope quarantine log for unknown scopes
- [x] Dry-run mode if possible (count only)

## R03.2 Safety

- [x] No plaintext key material logged
- [x] Transaction per batch or single txn documented: **per-row insert**; re-run idempotent; failure leaves dual-read valid
- [x] Failure leaves dual-read still valid

## R03.3 Tests

- [x] Unit/module: empty Lhdn → no-op
- [x] Unit: Lhdn row copies to One
- [x] Unit: re-run idempotent
- [x] Unit: collision hash already in One → skip/update policy documented (`already_migrated` / `hash_collision_different_org`)
- [x] Unit: unknown scope quarantine behavior

## R03.4 Docs

- [x] Runbook section in design doc or ops README: how to run migrator → `plans/005-remaining/r03-keys-migrator-runbook.md`
- [x] Rollback: dual-read still on; no table drop

## R03.5 Exit

- [x] Migrator merged; dual-read still enabled
- [x] Ready for R04 execute on staging
