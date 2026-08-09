# Cross-schema SQL leaks — live status (R10)

**Verified:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Method:** Fresh greps of schema-qualified `FROM`/`JOIN`/`INTO`/`UPDATE` and Dapper / `FromSqlRaw` / `NpgsqlCommand` under `apps/lazuar-api`  
**Baseline:** [`06-cross-schema-sql-leaks.md`](./06-cross-schema-sql-leaks.md)  
**Constraint:** Inventory only — **no application code modified**

---

## Ticket table

| ID | Status | Path | Priority | Next phase |
|----|--------|------|----------|------------|
| L-01 | **fixed** (R11) | `DocumentPublishedIntegrationEventHandler.cs` — event payload only; no foreign-schema SQL | — | **R11 complete** |
| L-02 | **present** | `apps/lazuar-api/Modules/Payments/Infrastructure/PlatformEndpoints.cs` (~53, ~84: `one."GlobalUsers"`) | P0 | **R12** |
| L-03 | **present** | `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs` (~51–52: JOIN `crm` + `one`; GET remains commerce-only) | P0 | **R13** |
| L-04 | **present** (dead) | `apps/lazuar-api/Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs` (~125: `communications."MessageTemplates"`); interface only caller is definition | P2 | **R15** |
| L-05 | **present** | `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs` (~59: LEFT JOIN `crm."ClientProfiles"`) | P1 | **R14** |
| L-06 | **present** | `apps/lazuar-api/BuildingBlocks/Infrastructure/Observability/PlatformMetricsCollector.cs` (ModuleSchemas all nine; ~208: `lhdn."TaxDocuments"` stuck product SQL) | P1 | **R16** handoff **R35** |
| L-07 | **fixed** (R05) | `apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs` — One-only `one."ApiCredentials"`; **no** `LhdnLookupSql` / dual-read | — | **R17** handoff complete |
| new? | **none** (product paths) | No new consumer-module foreign-schema production leaks beyond L-01…L-06 | — | — |

---

## L-07 detail (R05)

| Check | Result |
|-------|--------|
| `LhdnLookupSql` constant | **Gone** |
| `FROM lhdn."DeveloperApiKeys"` in middleware | **Gone** |
| Remaining SQL | `FROM one."ApiCredentials"` only |
| Lookup path | `LookupCredentialAsync` → keyed `OneSqlConnectionFactory` only; Lhdn-only keys → 401 |
| Remarks | Documents One-only / R05; table drop still R06 |

**Classification:** Dual-read boundary debt from `06` is **closed**. Residual host hardcode of `one.ApiCredentials` is allowed composition-root auth (not a multi-schema leak). R17 is handoff-complete (no further dual-read fix work).

---

## Related non-leaks / out of ticket scope

| Item | Path | Why not a new L-## |
|------|------|---------------------|
| Host key migrator store | `src/Lazuar.Api/Jobs/ApiKeyMigration/SqlApiKeyMigrationStore.cs` (`lhdn` + `one`) | Intentional **R03** one-shot tooling, not request-path dual-read |
| H-01 migration | `Commerce/.../Migrations/20260704163126_RefactorDunningEngine.cs` | Historical only |
| Own-schema Dapper/FromSql | Billing, Lhdn, Comms query/workers, Commerce-only queries | Correct isolation |
| Outbox/Inbox `FromSqlRaw` | BB jobs + module workers | Per-schema messaging spine |

---

## Priority order (this wave)

1. **R11** — L-01 DocumentPublished multi-schema JOIN  
2. **R12** — L-02 PlatformEndpoints → `one.GlobalUsers`  
3. **R13** — L-03 PublicArrears update-payment → `crm` + `one`  
4. **R14** — L-05 CommerceDocumentLookup CRM join  
5. **R15** — L-04 dead `GetDefaultTemplateIdsAsync` delete  
6. **R16** — L-06 metrics handoff → **R35**  
7. **R17** — L-07 keys handoff **complete** (fixed by R05; no SQL fix phase)

---

## Grep evidence summary

| Leak | Still matches foreign schema SQL? |
|------|-----------------------------------|
| L-01 | **No** — handler uses event fields only (R11) |
| L-02 | Yes — `one."GlobalUsers"` (login + me) |
| L-03 | Yes — `crm."ClientProfiles"` + `one."Organizations"` on POST update-payment |
| L-04 | Yes — method body present; **zero** call sites outside interface/impl |
| L-05 | Yes — `crm."ClientProfiles"` in draft session lookup |
| L-06 | Yes — schema array + `lhdn."TaxDocuments"` |
| L-07 dual-read | **No** — middleware One-only |

---

## Diff vs `06-cross-schema-sql-leaks.md`

| ID | 06 status | Live (R10) |
|----|-----------|------------|
| L-01 | open | **fixed** by R11 (event denorm) |
| L-02…L-06 | open | **still present** (paths unchanged) |
| L-07 | tracked dual-read exception (FW-1) | **fixed** by R05 One-only middleware |
| New product leaks | n/a | **none found** |

R11 unblocked.

*End of R10 live inventory. No application code modified.*
