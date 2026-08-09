# R06 — Drop/archive `lhdn.DeveloperApiKeys`

**Track:** Keys · **Depends on:** R05 live ≥ **30 days** (or signed waiver)  
**Analysis:** `../01-api-key-one-only-cutover.md` § F04

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
