# R06 — Drop/archive `lhdn.DeveloperApiKeys`

**Status:** **DEFERRED** 2026-08-09  
**Track:** Keys · **Depends on:** R05 live ≥ **30 days** (or signed waiver)  
**Analysis:** `../01-api-key-one-only-cutover.md` § F04  
**Notes:** `../r06-notes.md`

**Deferral:** R05 One-only **code** is on branch (middleware no longer reads `DeveloperApiKeys`), but the table stays until R05 is **prod** and the 30-day soak completes. Clock has **not** started. Do **not** execute R06.2 now.

---

## R06.1 Preflight

- [ ] One-only since: ________ (≥30d? yes/waiver)
- [ ] Grep: no read/write of DeveloperApiKeys in app code
- [ ] Outbox: no residual Lhdn revoke events needed

## R06.2 Migration

- [ ] EF migration drop **or** rename to archive
- [ ] Remove dead domain/repo/DI for DeveloperApiKey if unused
- [ ] Clean Lhdn module leftovers

## R06.3 Exit

- [ ] Table gone/archived
- [ ] FW-1 fully closed in FUTURE-WORK.md
