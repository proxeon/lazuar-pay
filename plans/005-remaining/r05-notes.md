# R05 — One-only middleware (remove dual-read) notes

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** Keys  
**Checklist:** `checklists/r05-keys-one-only-middleware.md`  
**Depends on:** R04 complete (or accelerate waiver from R02) for **deploy**  
**Analysis:** `01-api-key-one-only-cutover.md` § F03  
**Scope this pass:** **Code + docs** for One-only middleware. **No** table drop (R06). **No** claim of staging/prod cutover.

---

## Summary

| Concern | State |
|---------|--------|
| Middleware lookup | **One-only** — `one.ApiCredentials` via `OneSqlConnectionFactory` |
| Lhdn SQL branch | **Removed** (`LhdnLookupSql` deleted) |
| Revoke subscribe | **One only** — Lhdn `ApiKeyRevoked` dual-subscribe removed |
| Revoke handler | Implements **One** event interface only |
| Table `lhdn.DeveloperApiKeys` | **Still present** — drop/archive is **R06** |
| Staging/prod deploy | **Pending** — see **DEPLOY gate** |

---

## DEPLOY gate (mandatory)

**Do not deploy this build to an environment until:**

1. Inventory Q8 **`active_legacy_only = 0`** on that env  
   (query package: `r02-inventory.sql`), **or**
2. Signed residual quarantine list accepted by eng + ops,

**and** R04 post-migrate verify for that env is recorded.

Shipping One-only code while residual Lhdn-only active keys exist causes **401** for those integrators.

| Env | Gate | Deploy R05 One-only? |
|-----|------|----------------------|
| Local empty | Q8 = 0 (R02) | Safe for local smoke |
| Staging | Pending R04 verify | **Blocked** until Q8 = 0 |
| Prod | Pending R04 verify | **Blocked** until Q8 = 0 |

Rollback plan if premature 401 spike: re-enable dual-read from pre-R05 commit (restore `LhdnLookupSql` + dual subscribe + dual handler interface) and redeploy.

Table drop remains **R06** after ≥ **30 days** One-only in prod (or signed waiver).

---

## Code changes (R05.2)

| File | Change |
|------|--------|
| `src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs` | Delete `LhdnLookupSql`; One-only `LookupCredentialAsync`; DEPLOY gate xmldoc |
| `src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs` | Remove Lhdn `ApiKeyRevoked` subscribe; keep One revoke + `WorkspaceUpdated` |
| `src/Lazuar.Api/EventHandlers/ApiKeyRevokedIntegrationEventHandler.cs` | One event interface only |

**Non-changes:** hash algorithm, key prefixes, cache key format, `PlatformApiScopes`, Lhdn HTTP façades (still One-backed), Lhdn event **type** left for residual outbox deserialization.

---

## Tests (R05.3)

| Test | Expect |
|------|--------|
| Existing One/cached auth paths | Still green |
| `One_Only_Lookup_Lhdn_Only_Key_Returns_401_And_Does_Not_Call_Lhdn_Factory` | **401**; Lhdn factory never used |
| `ApiKeyRevokedIntegrationEventHandlerTests` | One event only (Lhdn test removed) |

Verify filter (from checklist):

```bash
cd apps/lazuar-api && dotnet test tests/Lazuar.ModuleTests --filter \
  "FullyQualifiedName~ApiKeyAuthenticationTests|FullyQualifiedName~ApiKeyRevokedIntegrationEventHandlerTests|FullyQualifiedName~GenerateAndListApi|FullyQualifiedName~LegacyApiKeyMigrator"
```

Also: `rg DeveloperApiKeys` on middleware path must be **empty**.

---

## Docs (R05.4)

| Doc | Update |
|-----|--------|
| One README §9 | One-only + DEPLOY gate |
| Lhdn README §6 | Dual-read closed; table waits R06 |
| `plans/004-maintenance/api-key-cutover-design.md` | Status: code on branch; deploy gated |
| `plans/004-maintenance/FUTURE-WORK.md` FW-1 | **Partial** (code done; deploy + R06 open) |
| This file | Deploy gate + change list |

---

## Checklist ticks

| Item | State |
|------|--------|
| R05.1 Preflight | **Pending** (ops inventory / migrate residual) |
| R05.2 Code | **Done** (this branch) |
| R05.3 Tests | **Done** (this branch; CI green locally) |
| R05.4 Docs | **Done** (this branch) |
| R05.5 Deploy / monitor | **Pending** |
| R05.6 Exit (One-only in prod + R06 clock) | **Pending** |

---

## Explicit non-goals

- Do **not** drop `lhdn.DeveloperApiKeys` (R06).
- Do **not** delete Lhdn `ApiKeyRevokedIntegrationEvent` type yet (residual outbox safety).
- Do **not** claim R04/R05 exit without env inventory paste.
