# API key cutover design (Phase 03.2)

**Status:** Design for interim + future cutover  
**Date:** 2026-08-09  
**Locks:** `plans/004-maintenance/decisions.md` §00.1  
**Companion inventory:** `plans/004-maintenance/phase-03-analysis.md`

---

## 1. End-state

| Concern | Target |
| :--- | :--- |
| Mint / list / revoke store | **Only** `one.ApiCredentials` |
| Auth middleware lookup | **Only** One SQL |
| Revoke cache event | **Only** `Modules.One.Contracts.Events.ApiKeyRevokedIntegrationEvent` |
| LHDN product routes | Optional One-backed façade (`/lhdn/api-keys`) or removed (Phase 05 honesty) |
| `lhdn.DeveloperApiKeys` | Archive or drop **≥ 30 days** after One-only in prod |

---

## 2. Dates (from decisions.md)

| Milestone | Date | Meaning |
| :--- | :--- | :--- |
| Dual-read **allowed until** | **2026-11-30** | Auth may still query `lhdn.DeveloperApiKeys` |
| Dual-read **removed by** (target) | **2026-12-15** | Middleware One-only; Lhdn revoke subscription gone |
| Table drop / archive | **≥ 30 days after** One-only live in prod | Phase 03.6 |

If production active row count is zero earlier, cutover may move **forward** — do not keep dual-read open “because it still works.”

---

## 3. Dual-read window behavior (until 2026-11-30)

### 3.1 Read order (LOCKED)

1. **One first** — `one."ApiCredentials"` where `KeyHash = @hash AND IsActive = true`  
2. **Lhdn second** — `lhdn."DeveloperApiKeys"` same predicate  
3. Miss → **401** `{ "error": "Invalid or revoked API Key." }`

Implemented in `ApiKeyAuthenticationMiddleware.LookupCredentialAsync`. Do **not** invert order (Lhdn-first would hide One collisions and prolong legacy dependence).

### 3.2 New keys during the window

- All mint paths write **only** One (already true: Lhdn façade → `IApiCredentialService`, One endpoints, Aura provision).  
- **No new inserts** into `lhdn.DeveloperApiKeys` (application mint path already dead; do not reintroduce).  
- Integrators minting after migration to One never need Lhdn dual-read.

### 3.3 Revoke during the window

| Key location | Revoke path | Cache eviction |
| :--- | :--- | :--- |
| One credential | `RevokeApiCredential` / One or Lhdn DELETE façade | One `ApiKeyRevoked` event |
| Legacy Lhdn-only row | No product UI path today that revokes Lhdn rows (list is One-only) | Dual handler still accepts Lhdn event if any residual publisher |

**Gap to address in 03.4:** list/revoke UI only sees One rows. Migrating legacy keys into One is required so integrators can revoke via dashboard; until then, legacy keys remain valid until dual-read ends or SQL deactivate.

### 3.4 Cache

- `ApiKey_{keyHash}` TTL 5m; revoke event must continue to fire for One keys.  
- Keep dual handler until Lhdn event type is unused in outbox and dual-read is gone.

---

## 4. Migration algorithm (for 03.4 — not executed in interim)

### 4.1 Hash / format compatibility

| Field | Lhdn `DeveloperApiKeys` | One `ApiCredentials` | Migrate? |
| :--- | :--- | :--- | :--- |
| `KeyHash` | SHA-256 hex of full plain key (`sk_*` + secret) | Same via `ITokenGeneratorService.HashToken` | **Copy as-is** (no re-hash) |
| `Prefix` | `sk_live_` / `sk_test_` | Same | Copy |
| `KeyHint` | last 4 / `****` default for pre-hint rows | max 16 | Copy; leave `****` if unknown |
| `Scopes` | space-separated; default `lhdn.documents:write lhdn.documents:read` | `PlatformApiScopes` allowlist | Copy if all scopes known; see §4.3 |
| `OrganizationId` | tenant id | same Guid space | Copy |
| `Name` | free text | free text | Copy |
| `IsActive` | bool | bool | Copy (only migrate active if product chooses; prefer migrate all for audit) |
| `CreatedAt` | timestamptz | timestamptz | Copy |
| `CreatedByUserId` | n/a on Lhdn | nullable on One | **null** for migrated |
| `Id` | Guid | Guid | **Preserve Id** if no collision (simplifies external references); else new Guid + mapping table |

Plain secrets are **never stored** — migration is hash-row copy only. Integrators keep the same plain key string.

### 4.2 Idempotent job outline

```
for each row in lhdn.DeveloperApiKeys (optionally WHERE IsActive):
  if exists one.ApiCredentials with same KeyHash:
    skip (already migrated or dual mint collision)
  else if exists one.ApiCredentials with same Id and different KeyHash:
    assign new Id; record mapping
  else:
    INSERT into one.ApiCredentials (... mirrored columns, CreatedByUserId = null)
  mark migration watermark (outbox table or ops log: source_id, key_hash, at)
```

- Prefer **idempotent SQL or hosted job** in ops/One, dry-run first (count would-insert / would-skip).  
- Unique index on `one.ApiCredentials.KeyHash` enforces no double insert.  
- After successful migrate + auth smoke: optionally set `lhdn.DeveloperApiKeys.IsActive = false` for migrated rows (keeps dual-read harmless) **or** leave active until dual-read removal (middleware still One-first so One row wins).

### 4.3 Who cannot be migrated automatically

| Case | Action |
| :--- | :--- |
| `KeyHash` empty / corrupt | Skip; flag for ops; key already broken in dual-read |
| Scopes contain tokens **not** in `PlatformApiScopes.AllKnownScopes` | Map known subset; unknown → either default LHDN document scopes or skip + remint ticket |
| `OrganizationId` not present in `one.Organizations` | Skip; orphan tenant — resolve org or revoke |
| Duplicate `KeyHash` already on One with **different** org | Security review; do not overwrite |
| Inactive historical keys | Optional archive-only path; no auth need |

### 4.4 Post-migration verification

1. Staging: mint One key → auth OK; sample migrated legacy hash → auth OK with same plain key.  
2. Revoke One credential → cache eviction → next request 401.  
3. Count: `active lhdn rows not present in one by KeyHash` → drive cutover readiness.  
4. Prod runbook: maintenance window optional (hash copy is online-safe); announce dual-read end date.

---

## 5. Remove dual-read (03.5) — after 2026-11-30

Checklist (do **not** run in interim):

1. Middleware: delete `LhdnLookupSql` + Lhdn factory branch; keep One-only.  
2. `Program.cs`: unsubscribe Lhdn `ApiKeyRevokedIntegrationEvent`.  
3. Handler: drop Lhdn `IIntegrationEventHandler` implementation (or leave obsolete no-op briefly).  
4. Deprecate Lhdn domain aggregate/repo key methods when no dual-read readers remain.  
5. TypeSpec: document LHDN key routes as One façade or remove (Phase 05).  
6. Tests: no sole-seed of Lhdn `DeveloperApiKeys` for auth; architecture tests if dual path was asserted.

### 5.1 Post-cutover failure mode

- Unknown / legacy-only key → **401** same JSON: `{ "error": "Invalid or revoked API Key." }`  
- Optional later: richer `error_code: "api_key_legacy_retired"` + hub docs — not required for cutover if integrators were migrated/notified.  
- Integrator note: remint via One or Lhdn façade `/api-keys` before dual-read end.

---

## 6. Phase 00 sign-off alignment

| 00.1 rule | Design compliance |
| :--- | :--- |
| One SSoT mint/list/revoke | Already implemented; migration copies into One |
| Lhdn dual-read only until cutover | Middleware keeps Lhdn branch until after 2026-11-30 |
| Cutover B dated | Dates above; early exit if zero rows |
| LHDN scopes on One | `PlatformApiScopes` includes `lhdn.documents:*` |
| Revoke after cutover: One event only | 03.5 collapses dual subscribe |
| Read order One then Lhdn | Documented + implemented |

**Sign-off for this design doc:** matches locked 00.1. Full migration + dual-read removal remain gated by dates / row counts (checklist 03.4–03.6).

---

## 7. Explicit non-goals (interim PR)

- Dropping `lhdn.DeveloperApiKeys`  
- Removing dual-read middleware or dual event subscribe  
- Changing hash algorithm or key prefix format  
- Blocking Lhdn HTTP `/api-keys` façade (it already mints One)
