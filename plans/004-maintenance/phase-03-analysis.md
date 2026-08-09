# Phase 03 — Analysis (Dual API keys cutover)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Locked decision:** `decisions.md` §00.1 — One `ApiCredentials` SSoT; dual-read **allowed until 2026-11-30**; One-only target by **2026-12-15**.  
**This interim:** inventory + design only; dual-read **must stay** until after 2026-11-30 (or earlier only if prod `lhdn.DeveloperApiKeys` row count is zero and ops signs off).

---

## 03.1 Inventory map

### A. Dual-read middleware (auth path)

| Item | Path | Notes |
| :--- | :--- | :--- |
| Middleware | `apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs` | Dual SQL lookup; **One first**, then Lhdn |
| One SQL | `one."ApiCredentials"` via keyed `OneSqlConnectionFactory` | `KeyHash` + `IsActive = true` |
| Lhdn SQL | `lhdn."DeveloperApiKeys"` via keyed `LhdnSqlConnectionFactory` | Same shape: Id, OrganizationId, Scopes |
| Token extract | `TryGetApiKey` | `Authorization: Bearer sk_live_\|sk_test_...` or raw `sk_...` |
| Hash | `BuildingBlocks/Infrastructure/TokenGeneratorService.cs` → `HashToken` | SHA-256 hex lowercase of **full** plain key (prefix + secret) |
| Scope claims | `Modules.One.Domain.PlatformApiScopes.Split` | Claims `scope` per token; role `API_CLIENT` |
| 401 body | `{ "error": "Invalid or revoked API Key." }` | Both stores miss → 401 |

**Read order (already correct per 00.1):** One → Lhdn → null.

### B. IMemoryCache keys / invalidation

| Cache key | Set by | TTL | Evicted by |
| :--- | :--- | :--- | :--- |
| `ApiKey_{keyHash}` | Middleware after successful lookup | 5 minutes | `ApiKeyRevokedIntegrationEventHandler` (One **and** Lhdn event types) |
| `TenantKeys_{organizationId}` | Middleware (list of hashes for tenant) | 10 minutes | `WorkspaceUpdatedIntegrationEventHandler` (evicts each `ApiKey_{hash}` + list) |

| Handler | Path |
| :--- | :--- |
| Revoke cache eviction | `apps/lazuar-api/src/Lazuar.Api/EventHandlers/ApiKeyRevokedIntegrationEventHandler.cs` |
| Workspace update eviction | `apps/lazuar-api/src/Lazuar.Api/EventHandlers/WorkspaceUpdatedIntegrationEventHandler.cs` |
| Dual subscribe | `apps/lazuar-api/src/Lazuar.Api/Program.cs` (~416–417) |

### C. Lhdn mint / list / revoke (legacy surface)

**Application (obsolete façades → One):**

| Command/query | Path | Behavior |
| :--- | :--- | :--- |
| `GenerateApiKeyCommand` (+ handler) | `Modules/Lhdn/Application/Commands/GenerateApiKeyCommand.cs` | `[Obsolete]`; delegates `IApiCredentialService.GenerateAsync` |
| `RevokeApiKeyCommand` (+ handler) | `Modules/Lhdn/Application/Commands/RevokeApiKeyCommand.cs` | `[Obsolete]`; delegates `IApiCredentialService.RevokeAsync` |
| `ListApiKeysQuery` (+ handler) | `Modules/Lhdn/Application/Queries/LhdnQueries.cs` | `[Obsolete]`; delegates `IApiCredentialService.ListAsync` |

**HTTP façade (already One-backed):**

| Route | Path | Store |
| :--- | :--- | :--- |
| `GET/POST/DELETE /lhdn/api-keys*` | `Modules/Lhdn/Infrastructure/Endpoints.cs` (~107–175) | `IApiCredentialService` only |

**Legacy domain / ports still present (dual-read residue):**

| Artifact | Path | Status |
| :--- | :--- | :--- |
| Aggregate `DeveloperApiKey` | `Modules/Lhdn/Domain/Aggregates/DeveloperApiKey.cs` | Table-backed; no mint path writes it |
| `ApiKeyScopes` | `Modules/Lhdn/Domain/ApiKeyScopes.cs` | LHDN-only scope constants (subset of platform) |
| Repo ports | `ILhdnRepository.Get/List/AddDeveloperApiKey` | `Modules/Lhdn/Application/Ports/ILhdnRepository.cs` |
| Repo impl | `Modules/Lhdn/Infrastructure/Repositories/LhdnRepository.cs` | `AddDeveloperApiKey` **unused** by application mint (dead write path for new keys) |
| DbSet + EF config | `Modules/Lhdn/Infrastructure/LhdnDbContext.cs` | `DeveloperApiKeys` |
| Table | `lhdn.DeveloperApiKeys` | Migrations: Initial + scopes/hint (`20260627124829_*`, `20260803171454_*`) |
| Event type | `Modules/Lhdn/Contracts/Events/ApiKeyRevokedIntegrationEvent.cs` | Still subscribed by host; **no Lhdn publisher** after façades moved to One |

### D. One mint / list / revoke (SSoT)

| Artifact | Path |
| :--- | :--- |
| Aggregate | `Modules/One/Domain/ApiCredential.cs` |
| Scopes | `Modules/One/Domain/PlatformApiScopes.cs` (includes `lhdn.*`, `payments.*`, webhooks) |
| Generate | `Modules/One/Application/Commands/GenerateApiCredentialCommand.cs` |
| List | `Modules/One/Application/Queries/ListApiCredentialsQuery.cs` |
| Revoke | `Modules/One/Application/Commands/RevokeApiCredentialCommand.cs` → publishes **One** `ApiKeyRevokedIntegrationEvent` |
| Service façade | `Modules/One/Infrastructure/Services/ApiCredentialService.cs` implements `IApiCredentialService` |
| Contract | `Modules/One/Contracts/IApiCredentialService.cs` |
| DI | `Modules/One/Infrastructure/DependencyInjection.cs` |
| Repo | `IOneRepository` / `OneRepository` Get/List/Add `ApiCredential` |
| Table | `one.ApiCredentials` — migration `20260803172637_CreateApiCredentials.cs` |
| HTTP | `Modules/One/Infrastructure/Endpoints.cs` — `GET/POST/DELETE /one/api-keys*` (OrgAdmin) |
| Provision mint | `ProvisionAuraWorkspaceCommand.cs` mints `ApiCredential` with Aura integrator scopes |

### E. Dual revoke event subscriptions

| Event | Publisher today | Host handler |
| :--- | :--- | :--- |
| `Modules.One.Contracts.Events.ApiKeyRevokedIntegrationEvent` | `RevokeApiCredentialCommandHandler` via `OneEventBus` | Cache `Remove(ApiKey_{hash})` |
| `Modules.Lhdn.Contracts.Events.ApiKeyRevokedIntegrationEvent` | **None** in active mint/revoke path (legacy only if old rows/code path) | Same handler |

`Program.cs` dual-subscribes both until cutover (keep for safety if any residual Lhdn outbox message still exists).

### F. TypeSpec / product surface

| Surface | Spec | Implementation |
| :--- | :--- | :--- |
| One `/api-keys` | `packages/api-spec/modules/one/routes.tsp` | One endpoints |
| Lhdn `/api-keys` | `packages/api-spec/modules/lhdn/routes.tsp` | Lhdn façade → One (models alias One DTOs) |

### G. Tests (relevant)

| Test | Path |
| :--- | :--- |
| One generate/list/revoke | `tests/Lazuar.ModuleTests/One/GenerateAndListApiCredentialsTests.cs` |
| Lhdn façade delegates to One | `tests/Lazuar.ModuleTests/Lhdn/GenerateAndListApiKeysTests.cs` |
| Middleware auth + revoke cache | `tests/Lazuar.ModuleTests/One/ApiKeyAuthenticationTests.cs` |
| Dual event handler eviction | `tests/Lazuar.ModuleTests/EventHandlers/ApiKeyRevokedIntegrationEventHandlerTests.cs` |
| Provision mints One credential | `tests/Lazuar.ModuleTests/One/ProvisionAuraWorkspaceTests.cs` |

### H. Remaining rows in `lhdn.DeveloperApiKeys`

| Env | Count | How to measure |
| :--- | :--- | :--- |
| Local / this workspace | **Unknown** (no live DB from this phase) | `SELECT COUNT(*) FROM lhdn."DeveloperApiKeys" WHERE "IsActive" = true;` |
| Staging / prod | **Ops to run** before 03.4 migrate / 03.5 remove dual-read | Same SQL; also inactive rows for archive decision |

**Gate for early cutover:** if prod active count is **0**, dual-read may be removed **before** 2026-11-30 (per decisions.md). Otherwise dual-read stays through the dated window.

---

## Findings (SSoT posture today)

1. **New mint already One-only** — Lhdn commands/endpoints and provision paths write `one.ApiCredentials` only. No application code constructs `new DeveloperApiKey` or calls `AddDeveloperApiKey` for mint.
2. **Dual-read is legacy-auth only** — needed solely for integrators still holding keys hashed into `lhdn.DeveloperApiKeys`.
3. **Revoke of new keys** already publishes One event only; Lhdn event subscription is defensive for residual outbox / old publishers.
4. **Do not** drop `lhdn.DeveloperApiKeys` or remove dual-read middleware in this interim.

## Out of scope this interim

- Migrator job / production migration (03.4) — after design sign-off + row inventory  
- Remove dual-read / dual subscribe (03.5) — **after 2026-11-30** (target complete by 2026-12-15)  
- Table drop (03.6) — ≥ 30 days after One-only in prod  
- TypeSpec route removal vs façade honesty polish (03.5 + Phase 05)
