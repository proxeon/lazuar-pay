# R15 — Remove L-04 dead cross-schema template SQL

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-04  
**Problem:** `GetDefaultTemplateIdsAsync` (or equivalent) queries `communications.MessageTemplates` but has no callers  
**Notes:** `../r15-notes.md`

---

## R15.1 Confirm dead

- [x] Grep all callers — zero production callers
- [x] Confirm safe to delete method + any private helpers only used by it

## R15.2 Delete

- [x] Remove dead method/SQL
- [x] Remove unused usings

## R15.3 Exit

- [x] Build green; no behavior change
