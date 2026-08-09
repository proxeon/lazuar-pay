# R12 — Fix L-02 Payments platform super-admin SQL into `one`

**Track:** SQL · **Analysis:** `../06-cross-schema-sql-leaks.md` L-02  
**File (verify):** `Payments/.../PlatformEndpoints.cs` (auth **moved** to One)  
**Problem:** Dapper/SQL against `one.GlobalUsers` (or similar) from Payments → **fixed**

---

## R12.1 Design

- [x] Define One Contracts query/auth port for super-admin validation (or reuse existing One service)
- [x] Payments only calls Contracts — no `one.` SQL

## R12.2 Implement

- [x] Add/use One port implementation
- [x] Replace PlatformEndpoints SQL
- [x] DI registration

## R12.3 Tests

- [x] Platform endpoint auth paths covered
- [x] No Payments project SQL string referencing `one.`

## R12.4 Exit

- [x] L-02 closed; PR focused only on this leak family
