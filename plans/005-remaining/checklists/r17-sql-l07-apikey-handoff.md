# R17 — L-07 API key dual-read handoff

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-07  
**Goal:** Dual-read was intentional until R05 — not a Contracts-port fix. **R05 done:** confirm dual-read removed; no separate SQL PR.  
**Notes:** `../r17-notes.md` · Keys: `../r05-notes.md`

---

## R17.1 Confirm

- [x] Dual-read **removed** — host middleware is **One-only** (`one."ApiCredentials"`; no `LhdnLookupSql` / `lhdn."DeveloperApiKeys"`)  
  — Confirmed 2026-08-09 after R05

## R17.2 Handoff

- [x] Tracked exclusively under Keys R01–R05  
  — Cutover code in R05; deploy gate + table drop remain Keys track (R05.5 / R06)
- [x] No “fix” by moving Lhdn SQL into a module without cutover  
  — Obsolete path; One-only middleware already landed

## R17.3 Exit

- [x] Linked to R05; no separate SQL PR required
