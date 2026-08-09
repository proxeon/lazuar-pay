# R12 — Fix L-02 Payments platform super-admin SQL into `one`

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-02  
**File (verify):** `Payments/.../PlatformEndpoints.cs`  
**Problem:** Dapper/SQL against `one.GlobalUsers` (or similar) from Payments

---

## R12.1 Design

- [ ] Define One Contracts query/auth port for super-admin validation (or reuse existing One service)
- [ ] Payments only calls Contracts — no `one.` SQL

## R12.2 Implement

- [ ] Add/use One port implementation
- [ ] Replace PlatformEndpoints SQL
- [ ] DI registration

## R12.3 Tests

- [ ] Platform endpoint auth paths covered
- [ ] No Payments project SQL string referencing `one.`

## R12.4 Exit

- [ ] L-02 closed; PR focused only on this leak family
