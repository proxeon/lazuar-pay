# R03 — Implement API key migrator

**Track:** Keys · **Analysis:** `../01-api-key-one-only-cutover.md` § migration  
**Depends on:** R01, R02  
**Do not:** Remove dual-read (R05)

---

## R03.1 Implementation choice

- [ ] Choose: hosted one-shot job **or** ops SQL/script **or** admin command: ________
- [ ] Idempotent on `KeyHash` (skip if already in One)
- [ ] Copy fields: KeyHash, Prefix, KeyHint, Scopes, OrganizationId, Name, IsActive, CreatedAt
- [ ] CreatedByUserId = null for migrated
- [ ] Preserve Id if design requires; else new Guid (document choice: ________)
- [ ] Scope quarantine log for unknown scopes
- [ ] Dry-run mode if possible (count only)

## R03.2 Safety

- [ ] No plaintext key material logged
- [ ] Transaction per batch or single txn documented
- [ ] Failure leaves dual-read still valid

## R03.3 Tests

- [ ] Unit/module: empty Lhdn → no-op
- [ ] Unit: Lhdn row copies to One
- [ ] Unit: re-run idempotent
- [ ] Unit: collision hash already in One → skip/update policy documented
- [ ] Unit: unknown scope quarantine behavior

## R03.4 Docs

- [ ] Runbook section in design doc or ops README: how to run migrator
- [ ] Rollback: dual-read still on; no table drop

## R03.5 Exit

- [ ] Migrator merged; dual-read still enabled
- [ ] Ready for R04 execute on staging
