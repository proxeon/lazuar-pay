# R17 — L-07 API key dual-read handoff

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-07  
**Goal:** Dual-read is **intentional until R05** — not a Contracts-port fix

---

## R17.1 Confirm

- [ ] Host middleware still dual-reads `one` + `lhdn` for keys

## R17.2 Handoff

- [ ] Tracked exclusively under Keys R01–R05
- [ ] No “fix” by moving Lhdn SQL into a module without cutover

## R17.3 Exit

- [ ] Linked to R05; no separate SQL PR required
