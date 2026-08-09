# F02 — Migrate legacy LHDN API keys → One

**Goal:** Idempotent migration of remaining `lhdn.DeveloperApiKeys` into `one.ApiCredentials`.  
**Depends on:** F01  
**Do not:** Remove dual-read yet (that is F03)

---

## F02.1 Migrator design

- [ ] Implement idempotent migrator (hosted job **or** ops script) per `api-key-cutover-design.md`
- [ ] Copy `KeyHash` as-is (no re-hash)
- [ ] Copy prefix, hint, scopes, org, name, active, created_at
- [ ] `CreatedByUserId` null for migrated rows
- [ ] Handle collisions (same hash already in One)
- [ ] Quarantine unknown scopes; log for manual fix

## F02.2 Staging

- [ ] Dry-run / execute on staging
- [ ] Auth smoke: legacy plain keys still work via dual-read **or** after copy via One
- [ ] List/revoke UI sees migrated keys
- [ ] Document failures and remediations

## F02.3 Production

- [ ] Backup / snapshot note
- [ ] Run migrator in prod
- [ ] Verify sample keys
- [ ] Record migration report (counts before/after)

## F02.4 Exit

- [ ] Active legacy-only keys remaining: ________ (target 0 before F03)
- [ ] Dual-read still enabled until F03
- [ ] Runbook attached to PR
