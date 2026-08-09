# R11 — L-01 DocumentPublished cross-schema SQL notes

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** SQL  
**Checklist:** `checklists/r11-sql-l01-document-published.md`  
**Analysis:** `06-cross-schema-sql-leaks.md` L-01  
**Scope this pass:** Enrich event payload; remove multi-schema JOIN from Communications handler. **No** L-02…L-06.

---

## Summary

| Concern | State |
|---------|--------|
| Design | **Event denorm at publish** (not Contracts query ports) |
| Fields on event | `TenantSlug`, `BusinessName`, `CustomerName`, `CustomerEmail` (+ existing org/ledger/type/path) |
| Publisher | `GenerateAndStoreDocumentCommandHandler` — customer via `ICommerceDocumentLookup`; slug/name via `IOneQueryService.GetWorkspaceByIdAsync` |
| Handler | `DocumentPublishedIntegrationEventHandler` — event fields + local `MessageTemplates` only |
| Dapper multi-JOIN | **Deleted** (`billing` / `one` / `commerce`) |
| Handler deps removed | `ISqlConnectionFactory` (Communications keyed factory no longer required by this handler) |

---

## Verify

```bash
# Handler must not reference foreign schemas
rg 'billing\.|commerce\.|one\.' apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs
# expect: no matches

dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests --filter "FullyQualifiedName~DocumentPublished|FullyQualifiedName~Communications|FullyQualifiedName~Billing"
```

---

## Out of scope

- L-02…L-06 (R12+)
- Inbox message reprocessing of **old** thin payloads (new fields required; old inbox rows may no-op if re-delivered without slug/email)
