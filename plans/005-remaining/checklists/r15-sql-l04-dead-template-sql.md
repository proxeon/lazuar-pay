# R15 — Remove L-04 dead cross-schema template SQL

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-04  
**Problem:** `GetDefaultTemplateIdsAsync` (or equivalent) queries `communications.MessageTemplates` but has no callers

---

## R15.1 Confirm dead

- [ ] Grep all callers — zero production callers
- [ ] Confirm safe to delete method + any private helpers only used by it

## R15.2 Delete

- [ ] Remove dead method/SQL
- [ ] Remove unused usings

## R15.3 Exit

- [ ] Build green; no behavior change
