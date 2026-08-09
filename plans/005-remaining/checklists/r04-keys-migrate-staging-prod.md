# R04 — Execute key migration (staging then prod)

**Track:** Keys · **Depends on:** R03 (or R02 accelerate with zero rows)  
**Do not:** Ship One-only middleware in this phase unless combined with R05 intentionally after verify

---

## R04.1 Staging

- [ ] Snapshot / note DB backup approach
- [ ] Run dry-run if available; record counts
- [ ] Run migrator
- [ ] Verify `active_legacy_only` → 0 (or accepted remainder: ________)
- [ ] Auth smoke with a real/staging key that was Lhdn-only (should still work via dual-read **or** One after copy)
- [ ] List/revoke UI shows migrated keys
- [ ] Fix quarantine rows

## R04.2 Production

- [ ] Change window scheduled
- [ ] Backup / point-in-time recovery note
- [ ] Run migrator
- [ ] Record before/after counts in PR or ops log
- [ ] Auth smoke sample of integrators / smoke keys
- [ ] Monitor 401 rates 24h (still dual-read)

## R04.3 Exit

- [ ] Prod `active_legacy_only` = 0 (or signed residual list)
- [ ] R05 unblocked
