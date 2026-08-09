# R04 — Execute key migration (staging then prod)

**Track:** Keys · **Depends on:** R03 (or R02 accelerate with zero rows)  
**Notes:** `../r04-notes.md` · **Runbook:** `../r03-keys-migrator-runbook.md` · **Verify:** `../r02-inventory.sql`  
**Do not:** Ship One-only middleware in this phase unless combined with R05 intentionally after verify  
**Do not:** Invent staging/prod counts; no secrets in checklist notes

---

## R04.0 Local

- [x] Local inventory empty (R02 Q1–Q12 all 0 on `lazuar_mvp`) — migrate no-op; **not** a substitute for staging/prod

---

## R04.1 Staging

**Status:** **Pending ops** (DB access / deploy not available from docs-only workstation)

- [ ] Snapshot / note DB backup approach
- [ ] Run dry-run if available; record counts
- [ ] Run migrator
- [ ] Verify `active_legacy_only` → 0 (or accepted remainder: ________)
- [ ] Auth smoke with a real/staging key that was Lhdn-only (should still work via dual-read **or** One after copy)
- [ ] List/revoke UI shows migrated keys
- [ ] Fix quarantine rows

## R04.2 Production

**Status:** **Pending ops** (change window + DB access required)

- [ ] Change window scheduled
- [ ] Backup / point-in-time recovery note
- [ ] Run migrator
- [ ] Record before/after counts in PR or ops log
- [ ] Auth smoke sample of integrators / smoke keys
- [ ] Monitor 401 rates 24h (still dual-read)

## R04.3 Exit

**R05 prod One-only not claimed** — blocked until prod Q8 `active_legacy_only = 0` (or signed residual list). Feature-branch R05 code may exist with gates; do not treat this exit as done.

- [ ] Prod `active_legacy_only` = 0 (or signed residual list)
- [ ] R05 unblocked
