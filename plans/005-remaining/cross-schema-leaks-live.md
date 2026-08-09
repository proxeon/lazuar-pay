# Cross-schema SQL leaks — live status (R10 + R11–R14)

**Verified:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Method:** Fresh greps of schema-qualified `FROM`/`JOIN`/`INTO`/`UPDATE` and Dapper / `FromSqlRaw` / `NpgsqlCommand` under `apps/lazuar-api`  
**Baseline:** [`06-cross-schema-sql-leaks.md`](./06-cross-schema-sql-leaks.md)

---

## Ticket table

| ID | Status | Path | Priority | Next phase |
|----|--------|------|----------|------------|
| L-01 | **fixed** (R11) | `DocumentPublishedIntegrationEventHandler.cs` — event payload only; no foreign-schema SQL | — | **R11 complete** |
| L-02 | **fixed** (R12) | Auth moved to One `PlatformAuthEndpoints` + `IPlatformAdminAuthQuery`; Payments payment-config only; no `one.GlobalUsers` SQL in Payments | — | **R12 complete** |
| L-03 | **fixed** (R13) | `PublicArrearsEndpoints.cs` — commerce SQL + `ICrmQueryService` + `IOneQueryService`; no `crm`/`one` JOIN | — | **R13 complete** |
| L-04 | **present** (dead) | `apps/lazuar-api/Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs` (~125: `communications."MessageTemplates"`); interface only caller is definition | P2 | **R15** |
| L-05 | **fixed** (R14) | `CommerceDocumentLookup.cs` — commerce SQL + `ICrmQueryService`; no `crm` JOIN | — | **R14 complete** |
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

1. **R11** — L-01 DocumentPublished multi-schema JOIN — **complete**  
2. **R12** — L-02 PlatformEndpoints → `one.GlobalUsers` — **complete**  
3. **R13** — L-03 PublicArrears update-payment → `crm` + `one` — **complete**  
4. **R14** — L-05 CommerceDocumentLookup CRM join — **complete**  
5. **R15** — L-04 dead `GetDefaultTemplateIdsAsync` delete  
6. **R16** — L-06 metrics handoff → **R35**  
7. **R17** — L-07 keys handoff **complete** (fixed by R05; no SQL fix phase)

---

## Grep evidence summary

| Leak | Still matches foreign schema SQL? |
|------|-----------------------------------|
| L-01 | **No** — handler uses event fields only (R11) |
| L-02 | **No** — Payments has no `one.` SQL; One owns platform auth (R12) |
| L-03 | **No** — commerce SQL + CRM/One ports (R13) |
| L-04 | Yes — method body present; **zero** call sites outside interface/impl |
| L-05 | **No** — commerce SQL + `ICrmQueryService` (R14) |
| L-06 | Yes — schema array + `lhdn."TaxDocuments"` |
| L-07 dual-read | **No** — middleware One-only |

---

## Diff vs `06-cross-schema-sql-leaks.md`

| ID | 06 status | Live |
|----|-----------|------|
| L-01 | open | **fixed** by R11 (event denorm) |
| L-02 | open | **fixed** by R12 (One auth port + endpoints) |
| L-03 | open | **fixed** by R13 (CRM + One contracts ports) |
| L-04 | open | **still present** (dead) |
| L-05 | open | **fixed** by R14 (CRM contracts port) |
| L-06 | open | **still present** |
| L-07 | tracked dual-read exception (FW-1) | **fixed** by R05 One-only middleware |
| New product leaks | n/a | **none found** |

---

## L-02 detail (R12)

| Check | Result |
|-------|--------|
| Payments Dapper `one.GlobalUsers` | **Gone** |
| Auth routes | `One/.../PlatformAuthEndpoints.cs` (`MapPlatformAuthEndpoints`) |
| Query port | `IPlatformAdminAuthQuery` / `PlatformAdminAuthQuery` |
| Payment-config | Still Payments `MapPlatformEndpoints` |
| Host | Maps auth then payment-config on `/api/v1/platform` |

---

## L-03 detail (R13)

| Check | Result |
|-------|--------|
| `JOIN crm."ClientProfiles"` | **Gone** |
| `JOIN one."Organizations"` | **Gone** |
| Commerce SQL | `Subscriptions` + `Products` only |
| Customer email | `ICrmQueryService.GetClientProfileAsync` |
| Tenant slug | `IOneQueryService.GetWorkspaceByIdAsync` |
| GET arrears | Unchanged (commerce-only) |
| Notes | [`r13-notes.md`](./r13-notes.md) |

---

## L-05 detail (R14)

| Check | Result |
|-------|--------|
| `LEFT JOIN crm."ClientProfiles"` | **Gone** |
| Commerce SQL | `CheckoutSessions` only (`AdHocLineItems`, `ClientProfileId`) |
| Customer name/email | `ICrmQueryService.GetClientProfileAsync` |
| `GetCustomerByGatewayTransactionAsync` | Unchanged (commerce-only) |
| Billing port | `ICommerceDocumentLookup` surface stable |
| Notes | [`r14-notes.md`](./r14-notes.md) |

*R14 complete. Next open product leak: L-04 (R15, dead code) or L-06 (R16 metrics).*
