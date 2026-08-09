# R12 — L-02 Platform super-admin auth out of Payments

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** SQL  
**Checklist:** `checklists/r12-sql-l02-platform-superadmin.md`  
**Analysis:** `06-cross-schema-sql-leaks.md` L-02  
**Scope this pass:** Move platform super-admin auth SQL/ownership into One. **No** L-03…L-06.

---

## Summary

| Concern | State |
|---------|--------|
| Design | One Contracts port + One endpoints; Payments payment-config only |
| Contracts | `IPlatformAdminAuthQuery` + `PlatformAdminLoginUserDto` / `PlatformAdminUserDto` |
| Implementation | `PlatformAdminAuthQuery` (EF → `one.GlobalUsers` only) |
| Auth routes | `One/Infrastructure/Endpoints/PlatformAuthEndpoints.cs` → `MapPlatformAuthEndpoints` |
| Payment-config | Still `Payments/.../PlatformEndpoints.cs` → `MapPlatformEndpoints` |
| Host | `platformGroup.MapPlatformAuthEndpoints()` then `MapPlatformEndpoints()` |
| Payments `one.` SQL | **Gone** (no Dapper / keyed One factory on platform auth) |
| Cookie | Unchanged: `lazuar_admin_auth`, path `/api/v1/platform` |

---

## Files

| Action | Path |
|--------|------|
| Add | `Modules/One/Contracts/IPlatformAdminAuthQuery.cs` |
| Add | `Modules/One/Infrastructure/Services/PlatformAdminAuthQuery.cs` |
| Add | `Modules/One/Infrastructure/Endpoints/PlatformAuthEndpoints.cs` |
| Slim | `Modules/Payments/Infrastructure/PlatformEndpoints.cs` (payment-config only) |
| DI | `One/Infrastructure/DependencyInjection.cs` — `IPlatformAdminAuthQuery` |
| Host | `src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs` |
| Tests | `tests/Lazuar.ModuleTests/One/PlatformAdminAuthQueryTests.cs` |

---

## Verify

```bash
# Payments must not reference one schema SQL
rg 'one\.' apps/lazuar-api/Modules/Payments --glob '*.cs'
# expect: no matches (or only comments / non-SQL)

dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests --filter "FullyQualifiedName~PlatformAdminAuth|FullyQualifiedName~Payments"
```

---

## Out of scope

- L-03…L-06 (R13+)
- Changing cookie name/path or JWT claim shape
- Merging platform login into workspace `/one/auth/*`
