# R01 — Keys code inventory notes

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** Keys  
**Checklist:** `checklists/r01-keys-code-inventory.md`  
**Analysis:** `01-api-key-one-only-cutover.md`  
**Scope:** Docs-only inventory — no application code changes.

---

## Summary

| Concern | State |
|---------|--------|
| Mint / list / revoke | **One-only** — writes only `one.ApiCredentials` |
| Auth | **Dual-read** — One first, then `lhdn.DeveloperApiKeys` |
| Revoke subscribe | **Dual** — host still subscribes One + Lhdn `ApiKeyRevokedIntegrationEvent` |
| Residual app mint to `DeveloperApiKeys` | **None** |

Mint/list/revoke already treat One as SSoT. Dual-read middleware and dual revoke subscription remain until R05 (after R02–R04 data migrate). No residual application path mints into `lhdn.DeveloperApiKeys`.

---

## R01.1 Middleware

**Path:** `apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs`

| Item | Detail |
|------|--------|
| Order | **One first**, then Lhdn fallback |
| One SQL | `one."ApiCredentials"` — `KeyHash` + `IsActive` (via keyed `OneSqlConnectionFactory`) |
| Lhdn SQL | `lhdn."DeveloperApiKeys"` — `KeyHash` + `IsActive` (via keyed `LhdnSqlConnectionFactory`) |
| 401 body | `{"error":"Invalid or revoked API Key."}` |
| Cache (hit) | `ApiKey_{hash}` — **5 minutes** |
| Cache (tenant list) | `TenantKeys_{orgId}` — **10 minutes** |
| Cutover dates (comments/xmldoc) | Dual-read allowed until **2026-11-30**; One-only target **2026-12-15** |

Token extract: `Bearer sk_*` or raw `sk_*`. Hash: SHA-256 of full plain key → lowercase hex (`TokenGeneratorService.HashToken`). Principal role `API_CLIENT`; test mode when key starts with `sk_test_`.

---

## R01.2 Mint / list / revoke map

| Surface | Implementation | Writes |
|---------|----------------|--------|
| One credential service | `IApiCredentialService` (or equivalent One module path) | **Only** mint path for app credentials |
| Lhdn `/api-keys` | Façade over One | No insert into `DeveloperApiKeys` |
| Aura provision | Mints One credentials only | One only |
| Revoke publish | One event only (`ApiKeyRevokedIntegrationEvent` via One event bus / revoke handler) | One |
| Dual subscribe | Still in composition | Listens One **and** Lhdn events |

**Dual subscribe location:**  
`apps/lazuar-api/src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs` → `UseHostEventSubscriptions`

- One event: `Modules.One.Contracts.Events.ApiKeyRevokedIntegrationEvent`
- Lhdn event: `Modules.Lhdn.Contracts.Events.ApiKeyRevokedIntegrationEvent`
- Handler: `apps/lazuar-api/src/Lazuar.Api/EventHandlers/ApiKeyRevokedIntegrationEventHandler.cs` (implements both)

Lhdn revoke event has **no active application publisher** after façades moved to One; dual subscribe is defensive for residual outbox / old messages until R05.

---

## R01.3 Dead write paths

| Finding | Notes |
|---------|--------|
| `AddDeveloperApiKey` | **Zero callers** — no live app mint into Lhdn |
| Residual repo / aggregate | Domain/repo surface may still exist for dual-read / legacy; **table drop / cleanup is R06** (after ≥30d One-only in prod) |
| App insert into `lhdn.DeveloperApiKeys` | **None** for mint |

Residual Lhdn key infrastructure is dual-read / defensive only until cutover and eventual drop.

---

## R01.4 Tests that encode dual-read

| Test / area | Role | R05 impact |
|-------------|------|------------|
| `ApiKeyAuthenticationTests` | Cache behavior; does **not** require Lhdn SQL seed for primary paths | Align when Lhdn branch removed |
| `ApiKeyRevokedIntegrationEventHandlerTests` | Covers **One + Lhdn** revoke handlers | **Must change in R05** (drop Lhdn dual path assertions) |
| `GenerateAndList*` | Mint/list against One SSoT | Expect stay One-only |
| `ProvisionAuraWorkspaceTests` | Provision mints One credentials | Expect stay One-only |

No production cutover in R01; tests document current dual-read / dual-subscribe behavior for later R05 edits.

---

## R01.5 Exit

- [x] Inventory note: this file (`plans/005-remaining/r01-notes.md`)
- [x] No behavior change (docs-only; map already accurate)

---

## Next

**R02 — Keys data inventory** (`checklists/r02-keys-data-inventory.md`): count active/inactive legacy rows, staging/prod inventory, decide early-cutover vs full migrate.
