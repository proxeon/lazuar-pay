# R15 — L-04 dead GetDefaultTemplateIdsAsync delete

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** SQL  
**Checklist:** `checklists/r15-sql-l04-dead-template-sql.md`  
**Analysis:** `06-cross-schema-sql-leaks.md` L-04  
**Scope this pass:** Delete dead Commerce→communications template SQL only. **No** L-03 / L-05.

---

## Summary

| Concern | State |
|---------|--------|
| Callers | **Zero** production call sites (interface + impl only) |
| Method | Removed `GetDefaultTemplateIdsAsync` from `ICommerceRepository` + `CommerceRepository` |
| Dead-only deps | Removed Dapper usage, `ISqlConnectionFactory` field/ctor param, unused usings |
| Behavior | No runtime change (method was never invoked) |

---

## Files

| Action | Path |
|--------|------|
| Edit | `Modules/Commerce/Application/ICommerceRepository.cs` — drop method |
| Edit | `Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs` — drop method + Dapper/factory wiring used only by it |
| Notes | `plans/005-remaining/r15-notes.md` |
| Checklist | `plans/005-remaining/checklists/r15-sql-l04-dead-template-sql.md` |
| Live status | `plans/005-remaining/cross-schema-leaks-live.md` — L-04 fixed |

---

## Verify

```bash
rg 'GetDefaultTemplateIdsAsync|communications\."MessageTemplates"' apps/lazuar-api --glob '*.cs'
# expect: no matches

dotnet build apps/lazuar-api
```

---

## Out of scope

- L-03 PublicArrears (R13)
- L-05 CommerceDocumentLookup (R14)
- Reintroducing template lookup via `ICommunicationsQueryService` (not needed; dead path)
