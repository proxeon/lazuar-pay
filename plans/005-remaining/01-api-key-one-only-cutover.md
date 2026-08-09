# FW-1 — API key One-only cutover (implementation analysis)

**Status:** Analysis only — **no application code changes in this document**  
**Date:** 2026-08-09  
**Repo:** `lazuar-pay`  
**Workstream:** `plans/004-maintenance/FUTURE-WORK.md` §FW-1  
**Locked decision:** `plans/004-maintenance/decisions.md` §00.1  
**Design source of truth:** `plans/004-maintenance/api-key-cutover-design.md`  
**Inventory (interim):** `plans/004-maintenance/phase-03-analysis.md`  
**Phased checklists:** F01–F04 under `plans/004-maintenance/checklists-future/`

This document is the **how-to implement** write-up for FW-1: migrate any remaining `lhdn.DeveloperApiKeys` into `one.ApiCredentials`, then remove dual-read auth and dual revoke subscriptions so middleware is One-only. It expands the calendar-gated outline already locked in Phase 03 / FUTURE-WORK without re-deciding product policy.

---

## 0. Executive summary

| Today | Target |
|-------|--------|
| Mint / list / revoke already write **only** `one.ApiCredentials` | Unchanged (already done) |
| Host auth dual-reads **One first**, then `lhdn.DeveloperApiKeys` | Auth reads **only** One |
| Host dual-subscribes One + Lhdn `ApiKeyRevokedIntegrationEvent` | Subscribe **One event only** |
| Legacy table kept for dual-read residue | Optional drop/archive ≥ **30 days** after One-only in prod |
| Dual-read allowed until **2026-11-30**; One-only target **2026-12-15** | Same calendar, **or earlier if prod active legacy count = 0** |

**Critical invariant:** Do **not** remove the Lhdn middleware branch while any **active** integrator key exists only on `lhdn.DeveloperApiKeys`. Plain secrets are never stored; migration is a **hash-row copy**. Integrators keep the same `sk_live_*` / `sk_test_*` string.

**Early-cutover shortcut:** if staging and prod both show **zero active** legacy rows (and ops accepts residual inactive rows as non-auth), F02 migrator is a no-op and F03 (One-only code) may ship **before** 2026-11-30.

---

## 1. Locked policy (do not reopen casually)

From `decisions.md` §00.1:

1. **SSoT (long-term):** One `ApiCredentials` is the only mint/list/revoke store.
2. **Legacy:** `lhdn.DeveloperApiKeys` is dual-read only until cutover; not a permanent product surface.
3. **Cutover posture B:** dual-read until **2026-11-30**; target One-only middleware + One revoke event by **2026-12-15**.
4. **Scopes:** LHDN scopes are modeled on One credentials (`PlatformApiScopes` includes `lhdn.documents:*`).
5. **Revoke after cutover:** One `ApiKeyRevokedIntegrationEvent` only.
6. **Read order during dual-read:** One first, then Lhdn (already implemented; do not invert).
7. **Early exit:** if prod active row count is zero earlier, cutover may move **forward** — do not keep dual-read open “because it still works.”

| Milestone | Date | Meaning |
|-----------|------|---------|
| Dual-read **allowed until** | **2026-11-30** | Auth may still hit `lhdn.DeveloperApiKeys` |
| Target dual-read **removed by** | **2026-12-15** | Middleware One-only; dual revoke subscription gone |
| Table drop / archive | **≥ 30 days after** One-only in prod | Separate PR (F04) |

Non-decisions still open at product level (not blockers for auth cutover):

- Whether LHDN HTTP `/lhdn/api-keys` stays forever as a One façade vs is removed from public surface (TypeSpec honesty / product DX).
- Exact drop vs archive-rename of the legacy table after the monitoring window.

---

## 2. Current code map (as of 2026-08-09)

Paths are relative to repo root unless noted. All live under `apps/lazuar-api/` for code.

### 2.1 Auth path (dual-read — **must change in F03**)

| Artifact | Path | Role |
|----------|------|------|
| Middleware | `apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs` | Extracts `sk_live_` / `sk_test_` from `Authorization`; hashes; caches; builds `API_CLIENT` principal |
| One SQL | `OneLookupSql` → `one."ApiCredentials"` via keyed `OneSqlConnectionFactory` | `KeyHash` + `IsActive = true` → `CredentialId`, `OrganizationId`, `Scopes` |
| Lhdn SQL | `LhdnLookupSql` → `lhdn."DeveloperApiKeys"` via keyed `LhdnSqlConnectionFactory` | Same projection shape (legacy fallback) |
| Lookup method | `LookupCredentialAsync` | **One first**, then Lhdn; documented cutover dates in xmldoc |
| Token extract | `TryGetApiKey` | `Bearer sk_*` or raw `sk_*` |
| Hash | `BuildingBlocks/Infrastructure/TokenGeneratorService.cs` → `HashToken` | SHA-256 UTF-8 of **full** plain key (`prefix + secret`); lowercase hex |
| Scope claims | `Modules.One.Domain.PlatformApiScopes.Split` | One claim `"scope"` per token; role `API_CLIENT` |
| 401 body | middleware | `{ "error": "Invalid or revoked API Key." }` — keep stable at cutover |
| Test mode claim | `IsTestMode` | `true` if plain key starts with `sk_test_` |

**Cache:**

| Cache key | TTL | Set by | Evicted by |
|-----------|-----|--------|------------|
| `ApiKey_{keyHash}` | 5 minutes | middleware after successful lookup | `ApiKeyRevokedIntegrationEventHandler` |
| `TenantKeys_{organizationId}` | 10 minutes | middleware (list of hashes) | `WorkspaceUpdatedIntegrationEventHandler` (evicts each `ApiKey_{hash}` + list) |

### 2.2 Dual revoke subscriptions (host composition — **must change in F03**)

| Artifact | Path |
|----------|------|
| Dual subscribe | `apps/lazuar-api/src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs` → `UseHostEventSubscriptions` |
| One event | `Modules.One.Contracts.Events.ApiKeyRevokedIntegrationEvent` |
| Lhdn event | `Modules.Lhdn.Contracts.Events.ApiKeyRevokedIntegrationEvent` |
| Handler | `apps/lazuar-api/src/Lazuar.Api/EventHandlers/ApiKeyRevokedIntegrationEventHandler.cs` (implements **both** `IIntegrationEventHandler<>`) |
| Handler DI | `Program.cs` registers handler as transient |

**Publisher reality today:**

| Event | Publisher |
|-------|-----------|
| One `ApiKeyRevoked` | `RevokeApiCredentialCommandHandler` via keyed `OneEventBus` |
| Lhdn `ApiKeyRevoked` | **No active application publisher** after façades moved to One (subscription is defensive for residual outbox / old messages) |

### 2.3 One SSoT (mint / list / revoke — **already correct**)

| Artifact | Path |
|----------|------|
| Aggregate | `Modules/One/Domain/ApiCredential.cs` |
| Scopes catalog | `Modules/One/Domain/PlatformApiScopes.cs` |
| Generate command | `Modules/One/Application/Commands/GenerateApiCredentialCommand.cs` |
| List query | `Modules/One/Application/Queries/ListApiCredentialsQuery.cs` |
| Revoke command | `Modules/One/Application/Commands/RevokeApiCredentialCommand.cs` |
| Service façade | `Modules/One/Infrastructure/Services/ApiCredentialService.cs` → `IApiCredentialService` |
| Contract | `Modules/One/Contracts/IApiCredentialService.cs` |
| DI | `Modules/One/Infrastructure/DependencyInjection.cs` (`AddScoped<IApiCredentialService, ApiCredentialService>`) |
| Repo | `IOneRepository` / `OneRepository` Get/List/Add `ApiCredential` |
| DbSet + EF | `Modules/One/Infrastructure/OneDbContext.cs` → `ApiCredentials` |
| Table | `one.ApiCredentials` — migration `20260803172637_CreateApiCredentials.cs` |
| HTTP | `Modules/One/Infrastructure/Endpoints/ApiCredentialEndpoints.cs` — `GET/POST/DELETE` under One group, **OrgAdmin** |
| Provision mint | `ProvisionAuraWorkspaceCommand` mints `ApiCredential` with `DefaultAuraIntegratorScopes` |

**`one.ApiCredentials` columns (EF / migration):**

| Column | Type notes |
|--------|------------|
| `Id` | uuid PK |
| `OrganizationId` | uuid, indexed |
| `Name`, `Prefix`, `KeyHash`, `Scopes` | text, required |
| `KeyHint` | varchar(16), required |
| `IsActive` | bool |
| `CreatedAt` | timestamptz |
| `CreatedByUserId` | uuid nullable |
| Unique index | `IX_ApiCredentials_KeyHash` **unique** on `KeyHash` |

### 2.4 Lhdn paths (façades + residue)

#### 2.4.1 Product surface already One-backed (keep as façade or document as such)

| Artifact | Path | Behavior |
|----------|------|----------|
| HTTP admin keys | `Modules/Lhdn/Infrastructure/Endpoints/AdminApiKeyEndpoints.cs` | `GET/POST/DELETE /lhdn/api-keys*` → `IApiCredentialService` only |
| `GenerateApiKeyCommand` | `Modules/Lhdn/Application/Commands/GenerateApiKeyCommand.cs` | `[Obsolete]`; delegates One service |
| `RevokeApiKeyCommand` | `Modules/Lhdn/Application/Commands/RevokeApiKeyCommand.cs` | `[Obsolete]`; delegates One service |
| `ListApiKeysQuery` | `Modules/Lhdn/Application/Queries/LhdnQueries.cs` | `[Obsolete]`; delegates One service |
| Scope split helper | `Modules/Lhdn/Domain/ApiKeyScopes.cs` | LHDN-only constants + `Split` (subset of platform) |

#### 2.4.2 Legacy domain / table still present (dual-read residue — gut later)

| Artifact | Path | Status |
|----------|------|--------|
| Aggregate `DeveloperApiKey` | `Modules/Lhdn/Domain/Aggregates/DeveloperApiKey.cs` | Table-backed; **no mint path constructs it for new keys** |
| Repo ports | `ILhdnRepository.Get/List/AddDeveloperApiKey` | Still declared |
| Repo impl | `Modules/Lhdn/Infrastructure/Repositories/LhdnRepository.cs` | `AddDeveloperApiKey` **unused** by application mint |
| DbSet + EF | `Modules/Lhdn/Infrastructure/LhdnDbContext.cs` | `DeveloperApiKeys`; unique index on `KeyHash` |
| Table | `lhdn.DeveloperApiKeys` | Initial schema + scopes/hint migration `20260803171454_*` |
| Lhdn revoke event type | `Modules/Lhdn/Contracts/Events/ApiKeyRevokedIntegrationEvent.cs` | Still exists; host still subscribes |

**`lhdn.DeveloperApiKeys` columns:**

| Column | Notes |
|--------|-------|
| `Id`, `OrganizationId`, `Name`, `Prefix`, `KeyHash`, `IsActive`, `CreatedAt` | original |
| `KeyHint` | added later; default `"****"` for pre-hint rows |
| `Scopes` | added later; default `"lhdn.documents:write lhdn.documents:read"` for pre-scopes rows |
| Unique index | `KeyHash` unique |
| **Missing vs One** | no `CreatedByUserId` |

### 2.5 Authorization policies (scope matrix — no change required for cutover)

Host policies in `Composition/AuthAndCorsExtensions.cs` use `PlatformApiScopes` claims (`lhdn.documents:*`, `payments.checkouts:*`, `payments.config:read`, `webhooks.endpoints:manage`). Middleware already emits those claims from the **scopes string** on the credential row, whether the row came from One or Lhdn dual-read. After migration, same scopes string on One rows preserves authorization.

### 2.6 TypeSpec / OpenAPI

| Surface | Spec | Implementation |
|---------|------|----------------|
| One `/api-keys` | `packages/api-spec/modules/one/routes.tsp` (and related) | One endpoints |
| Lhdn `/api-keys` | `packages/api-spec/modules/lhdn/routes.tsp` | Lhdn façade → One DTOs |

Cutover does **not** require removing Lhdn routes. Honesty requirement: they remain façades. Optional later product PR can deprecate them.

### 2.7 Tests that already exist (relevant)

| Test | Path | What it covers |
|------|------|----------------|
| One generate/list/revoke | `tests/Lazuar.ModuleTests/One/GenerateAndListApiCredentialsTests.cs` | mint metadata; list never returns secret; revoke publishes **One** event |
| Lhdn façade → One | `tests/Lazuar.ModuleTests/Lhdn/GenerateAndListApiKeysTests.cs` | obsolete commands/endpoints delegate |
| Middleware auth + policies | `tests/Lazuar.ModuleTests/One/ApiKeyAuthenticationTests.cs` | cached valid key claims; 401 unknown; revoke-after-eviction 401; OrgAdmin vs API_CLIENT; scope policies |
| Dual event handler eviction | `tests/Lazuar.ModuleTests/EventHandlers/ApiKeyRevokedIntegrationEventHandlerTests.cs` | **both** One and Lhdn event types clear `ApiKey_{hash}` |
| Provision mints One | `tests/Lazuar.ModuleTests/One/ProvisionAuraWorkspaceTests.cs` | Aura scopes on `ApiCredential` |

**Gaps for F03 (to add when implementing):**

- Explicit unit/module test that **Lookup is One-only** (no Lhdn factory call / no Lhdn SQL constant).
- Test that a credential present **only** in Lhdn shape is **not** authenticated after cutover (regression guard).
- Remove or rewrite the Lhdn-event handler test once dual implement interface is gone.
- Optional integration test: insert One row by hash → middleware auth OK with real SQL (if suite has DB harness).

### 2.8 Docs already describing the dual-read window

| Doc | Section |
|-----|---------|
| `Modules/One/README.md` | § platform credentials & dual-read window |
| `Modules/Lhdn/README.md` | § developer API keys façade + dual-read |
| Middleware xmldoc on `LookupCredentialAsync` | dates + design link |
| Handler summary comment | dual-window until cutover |
| `ModuleRegistrationExtensions.UseHostEventSubscriptions` | dual-subscribe comment |

These must be updated in the F03 PR when the window closes.

### 2.9 What is **not** dual-read residue

- New mint path: already One-only.
- Lhdn HTTP key routes: already One-only store.
- Hash algorithm / prefix format: stable; **do not change** during cutover.
- OrgAdmin policy denying `API_CLIENT`: intentional; unchanged.

---

## 3. Column / hash compatibility matrix

| Field | Lhdn `DeveloperApiKeys` | One `ApiCredentials` | Migrate action |
|-------|-------------------------|----------------------|----------------|
| `KeyHash` | SHA-256 hex of full plain key | Same via `ITokenGeneratorService.HashToken` | **Copy as-is** (never re-hash) |
| `Prefix` | `sk_live_` / `sk_test_` | Same | Copy |
| `KeyHint` | last 4 or `****` | max 16 | Copy; leave `****` if unknown |
| `Scopes` | space-separated; default LHDN docs | `PlatformApiScopes` allowlist | Copy if all tokens known; see §4.3 quarantine |
| `OrganizationId` | tenant Guid | same Guid space | Copy; validate org exists |
| `Name` | free text | free text | Copy |
| `IsActive` | bool | bool | Prefer migrate **all** for audit; auth only uses active |
| `CreatedAt` | timestamptz | timestamptz | Copy |
| `CreatedByUserId` | n/a | nullable | **null** for migrated |
| `Id` | Guid | Guid | **Prefer preserve Id** if free on One; else new Guid + log mapping |

Plain secrets are **never** in either table. Migration cannot “recover” a lost plain key; it only makes the **same** plain key hash resolve via One.

---

## 4. Migration algorithm (F02)

### 4.1 Goals

1. Idempotent: re-running does not double-insert (unique `KeyHash` on One).
2. Online-safe: pure inserts into One; dual-read still One-first so behavior improves as soon as a row lands.
3. Auditable: dry-run counts + execution report (inserted / skipped / quarantined).
4. Dashboard-visible: after insert, list/revoke UI (One or Lhdn façade) sees the key so integrators can revoke without SQL.

### 4.2 Preflight inventory SQL (F01)

Run on **staging then prod**. Record results in the PR description or a short ops note (do not invent counts).

```sql
-- Active legacy keys
SELECT COUNT(*) AS active_legacy
FROM lhdn."DeveloperApiKeys"
WHERE "IsActive" = true;

-- Inactive legacy (archive decision only)
SELECT COUNT(*) AS inactive_legacy
FROM lhdn."DeveloperApiKeys"
WHERE "IsActive" = false;

-- Already present on One by hash (migrated or dual-era collision)
SELECT COUNT(*) AS legacy_hashes_already_on_one
FROM lhdn."DeveloperApiKeys" d
WHERE EXISTS (
  SELECT 1 FROM one."ApiCredentials" a WHERE a."KeyHash" = d."KeyHash"
);

-- Active legacy-only (THE cutover blocker metric)
SELECT COUNT(*) AS active_legacy_only
FROM lhdn."DeveloperApiKeys" d
WHERE d."IsActive" = true
  AND NOT EXISTS (
    SELECT 1 FROM one."ApiCredentials" a WHERE a."KeyHash" = d."KeyHash"
  );

-- Optional: sample non-migratable candidates (orphan orgs)
SELECT d."Id", d."OrganizationId", d."Name", d."Scopes", d."KeyHash"
FROM lhdn."DeveloperApiKeys" d
WHERE d."IsActive" = true
  AND NOT EXISTS (
    SELECT 1 FROM one."Organizations" o WHERE o."Id" = d."OrganizationId"
  )
LIMIT 50;

-- Optional: scope distribution
SELECT d."Scopes", COUNT(*)
FROM lhdn."DeveloperApiKeys" d
WHERE d."IsActive" = true
GROUP BY d."Scopes"
ORDER BY COUNT(*) DESC;
```

**Cutover readiness metric:** `active_legacy_only = 0` before F03 merge to prod.

### 4.3 Idempotent job outline (pseudocode)

Implementation choice (pick one in F02 PR; do not invent two systems):

| Option | Pros | Cons |
|--------|------|------|
| **A. SQL ops script** (`INSERT … SELECT` + report queries) | Minimal deploy surface; easy dry-run | Scope quarantine logic weaker in pure SQL |
| **B. One-time hosted job / admin command** in Ops or One | Validation, logging, quarantine list, unit-testable | More code; must disable after run |

Recommended default: **SQL for bulk copy of clean rows** + **small C# dry-run/report helper** if scope/org validation is needed. Prefer **not** a permanent recurring job.

```
for each row in lhdn.DeveloperApiKeys (recommend: all rows, not only active):
  if KeyHash is null/empty:
    quarantine(row, "empty_hash"); continue

  if exists one.ApiCredentials with same KeyHash:
    skip already_migrated; continue

  if OrganizationId not in one.Organizations:
    quarantine(row, "orphan_org"); continue

  scopes := normalize_scopes(row.Scopes)
  if scopes has unknown tokens not in PlatformApiScopes.AllKnownScopes:
    known := intersection(scopes, AllKnownScopes)
    if known empty:
      quarantine(row, "unknown_scopes_only"); continue
    else:
      scopes := known  # log dropped tokens
      OR quarantine if product requires remint (choose one policy; document)

  if exists one.ApiCredentials with same Id and different KeyHash:
    newId := generate Guid; record mapping(source_id → newId)
  else:
    newId := row.Id

  INSERT one.ApiCredentials (
    Id, OrganizationId, Name, Prefix, KeyHash, KeyHint, Scopes,
    IsActive, CreatedAt, CreatedByUserId
  ) VALUES (
    newId, row.OrganizationId, row.Name, row.Prefix, row.KeyHash,
    coalesce(nullif(row.KeyHint,''), '****'),
    scopes,
    row.IsActive, row.CreatedAt, NULL
  )
  -- ON CONFLICT (KeyHash) DO NOTHING for race safety

  watermark / log: source_id, key_hash, new_id, at, result
```

**Scope policy recommendation (aligns with design §4.3):**

1. Default legacy scopes string `"lhdn.documents:write lhdn.documents:read"` is fully known → copy as-is.
2. Partial unknown tokens: keep known subset, log dropped tokens; if known empty → quarantine and ticket remint.
3. Do **not** invent new scope strings during migrate.

**Id preservation:** Prefer preserve Lhdn `Id` so any external reference to key id (revoke URLs, support tickets) remains valid. `ApiCredential` aggregate constructor always creates a new version-7 Guid in C#; **SQL insert must set `Id` explicitly** (do not go through `new ApiCredential(...)` unless a migration factory is added). If implementing in C#, either:

- raw Dapper/SQL insert into One schema, or  
- add an internal rehydration/factory used only by migrator (not product mint).

**Collision: same KeyHash already on One with different org:** do **not** overwrite. Flag for security review. Dual-read One-first already prefers One’s org; migrating would violate unique KeyHash.

### 4.4 Example idempotent SQL (clean path only)

Illustrative; adjust for quarantine policy and org table name if different:

```sql
INSERT INTO one."ApiCredentials" (
  "Id", "OrganizationId", "Name", "Prefix", "KeyHash", "KeyHint",
  "Scopes", "IsActive", "CreatedAt", "CreatedByUserId"
)
SELECT
  d."Id",
  d."OrganizationId",
  d."Name",
  d."Prefix",
  d."KeyHash",
  COALESCE(NULLIF(d."KeyHint", ''), '****'),
  d."Scopes",
  d."IsActive",
  d."CreatedAt",
  NULL
FROM lhdn."DeveloperApiKeys" d
WHERE d."KeyHash" IS NOT NULL
  AND length(trim(d."KeyHash")) > 0
  AND EXISTS (SELECT 1 FROM one."Organizations" o WHERE o."Id" = d."OrganizationId")
  AND NOT EXISTS (
    SELECT 1 FROM one."ApiCredentials" a WHERE a."KeyHash" = d."KeyHash"
  )
  AND NOT EXISTS (
    SELECT 1 FROM one."ApiCredentials" a WHERE a."Id" = d."Id"
  )
ON CONFLICT ("KeyHash") DO NOTHING;
```

Handle `Id` collisions (same Id, different hash) in a second pass with `gen_random_uuid()` / new Guid + mapping table/log.

### 4.5 Post-migration optional deactivation of Lhdn rows

Two acceptable strategies (document which you pick):

| Strategy | Effect |
|----------|--------|
| **Leave Lhdn active** until F03 | Dual-read still harmless (One-first wins for migrated hashes) |
| **Set `IsActive = false` on Lhdn after successful One insert** | Dual-read becomes a no-op for those hashes even if One row later revoked — **careful:** if One insert succeeded but app bug prevents list, you still have the One row |

Recommendation: leave Lhdn rows as-is until F03 lands in prod; deactivation is optional and not required for correctness once hashes exist on One.

### 4.6 Who cannot be migrated automatically

| Case | Action |
|------|--------|
| Empty / corrupt `KeyHash` | Skip; key already broken under dual-read |
| Unknown scopes only | Quarantine; remint ticket with known scopes |
| Orphan `OrganizationId` | Skip; resolve org or revoke at source |
| KeyHash on One with **different** org | Security review; do not overwrite |
| Inactive historical keys | Optional archive-only path; no auth need for F03 |

### 4.7 Verification after migrate

1. Re-run inventory: `active_legacy_only = 0` (or only quarantined rows with signed residual risk).
2. Staging auth smoke:
   - Mint new One key → 200 on a scoped integration route.
   - Known legacy plain key (if any test fixture exists) → still 200 after hash present on One.
3. Revoke migrated credential via `DELETE /api/v1/.../api-keys/{id}` (One or Lhdn façade) → cache eviction → next request **401**.
4. List UI shows migrated keys (One list / Lhdn façade list — both hit One).

---

## 5. Staging / production steps (runbook)

### 5.1 Staging sequence

| Step | Action | Exit criteria |
|------|--------|---------------|
| S1 | F01 inventory SQL on staging | Counts recorded |
| S2 | If `active_legacy_only = 0` → jump to early-cutover path (§8) | Skip S3–S4 migrator |
| S3 | Dry-run migrator (count would-insert / would-skip / would-quarantine) | Report reviewed; quarantines fixed or accepted |
| S4 | Execute migrator on staging | `active_legacy_only = 0` or residual signed off |
| S5 | Auth smoke + revoke smoke | No unexpected 401 on known good keys; revoke works |
| S6 | Deploy F03 One-only build to staging (dual-read removed) | Auth still green; metrics 401 rate not spiking |
| S7 | Soak staging (recommended ≥ 24–48h if traffic) | Ready for prod calendar |

### 5.2 Production sequence

| Step | Action | Exit criteria |
|------|--------|---------------|
| P0 | Communicate dual-read end date to integrators if any remints needed | Notice sent (if residual keys exist) |
| P1 | F01 inventory on prod (fresh counts) | Decision: migrate vs early cutover |
| P2 | Backup / snapshot note (standard DB ops) | Recovery path known |
| P3 | Run migrator in prod (if needed) during low traffic optional window | Report: inserted / skipped / quarantined |
| P4 | Verify sample keys + list/revoke for a canary org | Auth OK |
| P5 | Confirm `active_legacy_only = 0` (or signed residual) | Gate for F03 |
| P6 | Deploy F03 One-only | Middleware One-only live |
| P7 | Monitor 401s for API-key paths (dashboard/logs) for ≥ 48h | No unexplained spike |
| P8 | Mark design doc **executed** with date; update FUTURE-WORK FW-1 | Documentation honest |
| P9 | Calendar F04 table drop ≥ **30 days** later | Separate PR |

**Online safety:** hash-row insert does not require downtime. F03 deploy is a normal rolling deploy; worst case for unmigrated keys is **401** after F03.

### 5.3 Rollback

| Phase | Rollback |
|-------|----------|
| After migrator, before F03 | Leave dual-read; One inserts are harmless (idempotent). Optionally delete wrongly inserted One rows by report ids. |
| After F03 deploy | Redeploy previous build with dual-read **or** re-enable Lhdn branch in a hotfix if unmigrated keys found. Prefer fixing data (migrate missing hash) over long dual-read re-open. |
| After F04 table drop | Harder: restore table from backup/archive; avoid F04 until monitoring window complete. |

---

## 6. Code changes for One-only (F03) — file-level checklist

**Prerequisite:** F02 complete or early-cutover zero-row path (§8).

### 6.1 Middleware — remove Lhdn branch

**File:** `apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs`

| Change | Detail |
|--------|--------|
| Delete `LhdnLookupSql` constant | No legacy SQL |
| `LookupCredentialAsync` | Keep only One factory + `OneLookupSql`; return null if miss |
| Xmldoc | Replace dual-read window text with “One-only as of \<date\>; see executed design” |
| Keep | Token extract, cache, claims, 401 JSON body, test-mode detection |

**Do not** change hash algorithm, prefix rules, or cache key format (`ApiKey_{hash}`).

### 6.2 Host composition — One revoke only

**File:** `apps/lazuar-api/src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs`

- Remove:
  ```csharp
  eventBus.Subscribe<Modules.Lhdn.Contracts.Events.ApiKeyRevokedIntegrationEvent, ...>();
  ```
- Keep One revoke + workspace updated subscriptions.
- Update comment: dual-subscribe window closed.

### 6.3 Revoke event handler — collapse dual interface

**File:** `apps/lazuar-api/src/Lazuar.Api/EventHandlers/ApiKeyRevokedIntegrationEventHandler.cs`

| Change | Detail |
|--------|--------|
| Implement only | `IIntegrationEventHandler<Modules.One.Contracts.Events.ApiKeyRevokedIntegrationEvent>` |
| Remove | Lhdn `HandleAsync` overload |
| Keep | `Evict` → `_cache.Remove($"ApiKey_{keyHash}")` |
| Comment | One-only after cutover |

Optional later (not required for F03): delete `Modules.Lhdn.Contracts.Events.ApiKeyRevokedIntegrationEvent` type once no residual outbox rows / no code references remain. Prefer **leave type** one release if old outbox messages might still deserialize — subscription removal alone is enough for “no dual path.” If outbox still contains Lhdn event type strings, ensure outbox processor does not hard-fail; verify TypeResolver behavior for unsubscribed types.

### 6.4 Lhdn residue (F03 minimal vs F04 deep)

**F03 minimum (required):**

- No application path reintroduces inserts into `lhdn.DeveloperApiKeys`.
- Middleware and host no longer **read** or **subscribe** Lhdn key revoke for auth.

**F03 optional cleanup (nice-to-have, can wait F04):**

| Item | Notes |
|------|-------|
| Mark `AddDeveloperApiKey` obsolete / throw | Prevent accidental reintroduction |
| Leave aggregate + DbSet until table drop | Avoid EF migration thrash in F03 |
| Obsolete Lhdn `ApiKeyRevokedIntegrationEvent` | xmldoc only |
| Obsolete commands already marked | Keep façades |

**F04 (later, ≥ 30 days):**

- EF migration: drop or rename `lhdn.DeveloperApiKeys` to archive schema.
- Remove `DeveloperApiKey` aggregate, repo methods, DbSet config, Lhdn revoke event type if unused.
- Architecture / module tests green.

### 6.5 Lhdn HTTP façades

**No functional change required.** `AdminApiKeyEndpoints` already uses `IApiCredentialService`.

Document in Lhdn README: dual-read closed; routes remain One façades.

### 6.6 Docs to update in F03 PR

| File | Update |
|------|--------|
| `Modules/One/README.md` | Dual-read window **closed** + executed date |
| `Modules/Lhdn/README.md` | Same; legacy table wait for F04 |
| `plans/004-maintenance/api-key-cutover-design.md` | Status → **executed** + date + PR link |
| `plans/004-maintenance/FUTURE-WORK.md` FW-1 | Mark done / partial (if F04 pending) |
| `decisions.md` | Only if cutover completed **early** (record accelerated date) |
| This analysis | Cross-link executed note |

### 6.7 Explicit non-changes (F03)

- Do **not** change `PlatformApiScopes` allowlist unless migrating requires a new known scope (product decision).
- Do **not** change key prefix format or hash function.
- Do **not** drop `lhdn.DeveloperApiKeys` in the same PR as middleware cutover unless early-cutover **and** product wants aggressive cleanup (still prefer ≥ 30 day F04).
- Do **not** invert residual dual-read “for safety” after migrate.

---

## 7. Tests plan (F02 + F03)

### 7.1 Migrator tests (if C# migrator)

| Case | Expect |
|------|--------|
| Empty One + one Lhdn row | Inserts One row; same KeyHash, scopes, org |
| Same KeyHash already on One | Skip; no throw |
| Same Id different hash on One | New Id path; mapping logged |
| Unknown scopes only | Quarantine; no insert |
| Orphan org | Quarantine; no insert |
| Idempotent second run | Zero inserts |
| Inactive Lhdn row | Inserted if “migrate all”; `IsActive=false` on One |

### 7.2 Auth / cutover tests (F03)

| Case | File / approach | Expect |
|------|-----------------|--------|
| Valid One credential (cached path) | existing `ApiKeyAuthenticationTests` | still green |
| Unknown key | existing | 401; next not called |
| Revoke → cache evict → 401 | existing + handler tests | green |
| **One-only lookup** | new test with keyed factories: One returns null, Lhdn factory would return a row | **still 401** after cutover (proves Lhdn branch gone) |
| One factory returns active row | new or integration | principal set |
| Handler **One only** | update `ApiKeyRevokedIntegrationEventHandlerTests` | remove Lhdn-event test **or** assert type no longer handled via interface |
| No sole-seed of Lhdn `DeveloperApiKeys` for auth | code search / test inventory | tests use One or cache entries |
| Façade still mints One | existing Lhdn generate tests | green |
| Provision still mints One | existing provision tests | green |

### 7.3 Suggested new unit sketch (behavior, not code to commit here)

For One-only regression:

1. Register mock/fake `ISqlConnectionFactory` keyed `"OneSqlConnectionFactory"` that returns no row.
2. Optionally register `"LhdnSqlConnectionFactory"` that would return a credential if called.
3. Invoke middleware with `Bearer sk_live_...`.
4. Assert 401 and (if spyable) that Lhdn factory was never used — or simply that after deleting Lhdn SQL, compile-time absence is enough plus 401 when One empty.

### 7.4 Architecture tests

No mandatory arch-test change for F03 unless you want a forbid on string `"DeveloperApiKeys"` inside `ApiKeyAuthenticationMiddleware`. Optional defensive test is high value and small.

---

## 8. Early-cutover if zero rows

### 8.1 Gate

From `decisions.md` / FUTURE-WORK:

> If **active** legacy row count is **zero** earlier, cutover may move **forward**.

Operational definition:

```text
prod:  COUNT(*) FROM lhdn."DeveloperApiKeys" WHERE "IsActive" = true  = 0
staging: same (or staging reset accepted)
ops + eng sign-off recorded (PR description or ops ticket)
```

**Inactive-only rows:** do not block early F03 (auth ignores `IsActive = false`). They still matter for F04 archive decision.

**Active rows all already mirrored on One by KeyHash:** also allows F03 without bulk insert (migrator may no-op). Prefer still running inventory query `active_legacy_only` not merely `active_legacy`.

### 8.2 Early path procedure

| Step | Action |
|------|--------|
| E1 | Run F01 inventory on staging + prod |
| E2 | If both `active_legacy_only = 0` (and preferably `active_legacy = 0`) → document “accelerate cutover” |
| E3 | **Skip** F02 migrator implementation **or** land a no-op script + report for audit |
| E4 | Proceed to F03 code PR immediately (do not wait for 2026-11-30) |
| E5 | Update `decisions.md` note or FUTURE-WORK that cutover accelerated on \<date\> with counts |
| E6 | Still wait ≥ 30 days after One-only prod before F04 table drop |

### 8.3 What early-cutover does **not** allow

- Shipping F03 while prod still has active legacy-only hashes “because staging is clean.”
- Dropping the table in the same PR without monitoring window.
- Changing hash/format “while we’re here.”

---

## 9. Risks and mitigations

| Risk | Severity | Mitigation |
|------|----------|------------|
| Integrator key only on Lhdn table after F03 | **High** — 401 outage | Complete migration; gate on `active_legacy_only = 0`; canary org smoke |
| Scope allowlist rejects / drops legacy scopes | Medium | Quarantine list; remint with known scopes; default LHDN scopes already on allowlist |
| Same KeyHash, different org on One vs Lhdn | High / rare | Do not overwrite; security review; dual-read One-first already preferred One |
| Cache stale after revoke | Medium | Keep One revoke event + `ApiKey_{hash}` eviction; unchanged after cutover |
| Residual Lhdn outbox revoke messages after unsubscribe | Low | TypeResolver / unsubscribed event handling; keep Lhdn event type one release if needed |
| List/revoke UI never saw legacy-only keys | Medium (pre-F03) | Migration into One fixes dashboard revoke; until migrate, only SQL deactivate works for pure Lhdn rows |
| Id remapped on collision | Low | Log mapping; support uses KeyHash/hint |
| Accidental reintroduction of Lhdn inserts | Medium | Optional throw on `AddDeveloperApiKey`; code review; F04 delete path |
| Monitoring noise / 401 spike misattributed | Low | Compare 401 rate before/after; filter API_CLIENT auth failures |
| Early dual-read removal without inventory | **Critical process risk** | Checklist F01 mandatory; no F03 without counts |

---

## 10. PR breakdown (recommended)

Do **not** ship migrate + One-only + table drop as one mega-PR. Align with F01–F04.

### PR-A — Inventory only (F01)

| Include | Exclude |
|---------|---------|
| Runbook SQL + recorded counts (staging/prod) | Middleware changes |
| Go / no-go for migrate vs early cutover | Dual-read removal |
| Optional: short note in design doc appendix | App behavior change |

**Title idea:** `docs(ops): API key legacy inventory for FW-1 (F01)`

### PR-B — Migrator (F02)

| Include | Exclude |
|---------|---------|
| Idempotent migrator (SQL and/or C#) + dry-run mode | Middleware Lhdn branch removal |
| Tests for migrator if C# | Table drop |
| Staging execution report in PR body | Dual revoke unsubscribe |
| Prod execution as follow-up commit/comment or separate ops change request | |

**Title idea:** `feat(one): migrate legacy LHDN API keys into ApiCredentials (FW-1 F02)`

### PR-C — One-only code cutover (F03)

| Include | Exclude |
|---------|---------|
| Middleware One-only | Dropping `lhdn.DeveloperApiKeys` |
| Host subscribe One-only; handler collapse | Unrelated BB / webhook work |
| Test updates (Lhdn handler test removal / One-only regression) | TypeSpec route deletion unless product-ready |
| README + design “executed” + FUTURE-WORK partial close | |

**Title idea:** `feat(auth): One-only API key middleware after dual-read window (FW-1 F03)`

**Merge gates:**

1. `active_legacy_only = 0` in target env **or** early-cutover zero-row sign-off.  
2. Staging soak green.  
3. Calendar: after **2026-11-30** **unless** early gate met.  
4. Target complete by **2026-12-15**.

### PR-D — Table archive/drop (F04, later)

| Include | Exclude |
|---------|---------|
| EF migration drop/rename | Any dual-read reintroduction |
| Remove Lhdn domain key aggregate/repo if unused | Product feature work |
| Docs FW-1 fully closed | |

**Title idea:** `chore(lhdn): drop/archive DeveloperApiKeys after One-only soak (FW-1 F04)`  
**Gate:** One-only live in prod ≥ **30 days** (or explicit waiver).

### Optional micro-PRs

- PR-C1: middleware only  
- PR-C2: event subscribe + handler + tests  
- PR-C3: docs  

Useful if review bandwidth is tight; not required if PR-C stays focused.

---

## 11. Done criteria (FW-1 complete)

### After F03 (functional cutover)

- [ ] Middleware queries **only** `one.ApiCredentials`
- [ ] Only **One** `ApiKeyRevokedIntegrationEvent` subscribed for cache eviction
- [ ] No application writes to `lhdn.DeveloperApiKeys`
- [ ] Staging + prod verified; no unexplained API-key 401 spike
- [ ] Tests: One-only auth; revoke invalidates; no Lhdn sole-seed auth path
- [ ] One/Lhdn READMEs: dual-read window closed
- [ ] `api-key-cutover-design.md` status = **executed** with date + PR
- [ ] `FUTURE-WORK.md` FW-1 marked done for middleware (note F04 if pending)

### After F04 (full cleanup)

- [ ] Monitoring window ≥ 30 days
- [ ] Table dropped or archived
- [ ] Lhdn domain residue removed or justified
- [ ] FW-1 fully closed in FUTURE-WORK

---

## 12. Implementation order (cheat sheet)

```text
F01 Inventory
  │  counts: active_legacy, active_legacy_only, already_on_one
  ▼
  ┌── zero active_legacy_only? ──yes──► Early cutover → F03 code
  │
  no
  ▼
F02 Migrator (staging → prod)
  │  active_legacy_only → 0
  ▼
F03 One-only middleware + One revoke only (staging → prod)
  │  calendar or early gate
  ▼
  (wait ≥ 30 days)
  ▼
F04 Drop/archive lhdn.DeveloperApiKeys
```

---

## 13. File index (absolute paths for implementers)

### Policy / design

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/FUTURE-WORK.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/decisions.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/api-key-cutover-design.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/phase-03-analysis.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/phase-03-done.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/checklists-future/phase-f01-api-key-inventory.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/checklists-future/phase-f02-api-key-migrate.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/checklists-future/phase-f03-api-key-one-only.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/checklists-future/phase-f04-api-key-table-drop.md`

### Host auth / events

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/EventHandlers/ApiKeyRevokedIntegrationEventHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/EventHandlers/WorkspaceUpdatedIntegrationEventHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Program.cs`

### One SSoT

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/ApiCredential.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/PlatformApiScopes.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Contracts/IApiCredentialService.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Contracts/Events/ApiKeyRevokedIntegrationEvent.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Services/ApiCredentialService.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/ApiCredentialEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/OneDbContext.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Migrations/20260803172637_CreateApiCredentials.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/README.md`

### Lhdn legacy / façades

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Domain/Aggregates/DeveloperApiKey.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Domain/ApiKeyScopes.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/AdminApiKeyEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Repositories/LhdnRepository.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/LhdnDbContext.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Contracts/Events/ApiKeyRevokedIntegrationEvent.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Commands/GenerateApiKeyCommand.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Commands/RevokeApiKeyCommand.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/README.md`

### Hashing

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/TokenGeneratorService.cs`

### Tests

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/One/ApiKeyAuthenticationTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/One/GenerateAndListApiCredentialsTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/GenerateAndListApiKeysTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/EventHandlers/ApiKeyRevokedIntegrationEventHandlerTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/One/ProvisionAuraWorkspaceTests.cs`

---

## 14. Open ops actions (not code)

1. **Run inventory SQL** on staging and production; fill blanks in F01 checklist.  
2. Decide migrator form (SQL vs C#) based on row volume and quarantine needs.  
3. If non-zero residual after migrate: integrator remint communication before F03.  
4. Calendar: F03 after **2026-11-30** unless early zero-row gate; complete by **2026-12-15**.  
5. Schedule F04 ≥ 30 days after One-only prod.

---

## 15. Document control

| Field | Value |
|-------|-------|
| Analysis only | Yes — **no app code changes** in this delivery |
| Supersedes | Nothing; complements Phase 03 design/inventory |
| Next artifact | Ops F01 counts → F02 migrator PR → F03 One-only PR |
| Authoring date | 2026-08-09 |

When F03 ships, append an **Executed** subsection here (date, PR links, final counts, early vs calendar path).
