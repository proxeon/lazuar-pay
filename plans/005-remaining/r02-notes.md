# R02 — Keys data inventory notes

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** Keys  
**Checklist:** `checklists/r02-keys-data-inventory.md`  
**Analysis:** `01-api-key-one-only-cutover.md` §4.2–4.4, `../004-maintenance/api-key-cutover-design.md`  
**Scope:** Docs + ops SQL package only — no application code / middleware changes.  
**SQL handoff:** `r02-inventory.sql`

---

## Env access

| Env | Access | Notes |
|-----|--------|-------|
| Staging | **Blocked** | Needs ops DB access (credentials / network not available from this workstation) |
| Prod | **Blocked** | Needs ops DB access |
| Local docker | **Optional — ran** | `lazuar-postgres` healthy on host `:5433` → container `5432`; DB `lazuar_mvp` has `lhdn` + `one` schemas |

**Do not** invent staging/prod counts. Re-run `r02-inventory.sql` when ops access is available and paste results into the results table below.

---

## Allowlist source

Scope quarantine / migrator normalize uses:

- **Source:** `Modules.One.Domain.PlatformApiScopes.AllKnownScopes`
- **Path:** `apps/lazuar-api/Modules/One/Domain/PlatformApiScopes.cs`

Known tokens (as of this inventory):

| Scope |
|-------|
| `lhdn.documents:write` |
| `lhdn.documents:read` |
| `payments.checkouts:write` |
| `payments.checkouts:read` |
| `payments.config:read` |
| `webhooks.endpoints:manage` |

Default LHDN string `lhdn.documents:write lhdn.documents:read` is fully known → copy as-is.

---

## migrate_policy

| Policy | Choice |
|--------|--------|
| **migrate_policy** | **`all_rows`** (recommended) |
| Rationale | Prefer migrate inactive historical rows for audit/list consistency; active-only would leave inactive hashes un-mirrored if ever reactivated or needed for forensics |
| Active-only alternative | Acceptable only if product explicitly rejects archive copy; **not** chosen here |

---

## Full SQL package (Q1–Q12)

Copy of preflight inventory from analysis §4.2 plus operational metrics for R03 dry-run sizing. Full runnable script: `r02-inventory.sql`.

```sql
-- Q1 Active legacy keys
SELECT COUNT(*) AS active_legacy
FROM lhdn."DeveloperApiKeys"
WHERE "IsActive" = true;

-- Q2 Inactive legacy (archive / migrate_policy input)
SELECT COUNT(*) AS inactive_legacy
FROM lhdn."DeveloperApiKeys"
WHERE "IsActive" = false;

-- Q3 Total legacy
SELECT COUNT(*) AS total_legacy
FROM lhdn."DeveloperApiKeys";

-- Q4 Active One credentials
SELECT COUNT(*) AS active_one
FROM one."ApiCredentials"
WHERE "IsActive" = true;

-- Q5 Inactive One credentials
SELECT COUNT(*) AS inactive_one
FROM one."ApiCredentials"
WHERE "IsActive" = false;

-- Q6 Total One credentials
SELECT COUNT(*) AS total_one
FROM one."ApiCredentials";

-- Q7 Legacy hashes already present on One (migrated or dual-era)
SELECT COUNT(*) AS legacy_hashes_already_on_one
FROM lhdn."DeveloperApiKeys" d
WHERE EXISTS (
  SELECT 1 FROM one."ApiCredentials" a WHERE a."KeyHash" = d."KeyHash"
);

-- Q8 Active legacy-only — THE cutover blocker metric
SELECT COUNT(*) AS active_legacy_only
FROM lhdn."DeveloperApiKeys" d
WHERE d."IsActive" = true
  AND NOT EXISTS (
    SELECT 1 FROM one."ApiCredentials" a WHERE a."KeyHash" = d."KeyHash"
  );

-- Q9 Inactive legacy-only (all_rows migrator volume beyond active)
SELECT COUNT(*) AS inactive_legacy_only
FROM lhdn."DeveloperApiKeys" d
WHERE d."IsActive" = false
  AND NOT EXISTS (
    SELECT 1 FROM one."ApiCredentials" a WHERE a."KeyHash" = d."KeyHash"
  );

-- Q10 Empty / blank KeyHash (quarantine: empty_hash)
SELECT COUNT(*) AS empty_or_blank_hash
FROM lhdn."DeveloperApiKeys" d
WHERE d."KeyHash" IS NULL
   OR length(trim(d."KeyHash")) = 0;

-- Q11 Active legacy with OrganizationId missing from one.Organizations
SELECT COUNT(*) AS orphan_org_active_legacy
FROM lhdn."DeveloperApiKeys" d
WHERE d."IsActive" = true
  AND NOT EXISTS (
    SELECT 1 FROM one."Organizations" o WHERE o."Id" = d."OrganizationId"
  );

-- Q12 Scope distribution (active legacy) — sample for allowlist quarantine
SELECT d."Scopes", COUNT(*) AS n
FROM lhdn."DeveloperApiKeys" d
WHERE d."IsActive" = true
GROUP BY d."Scopes"
ORDER BY COUNT(*) DESC;
```

### Supplemental (ops / R03; not required for exit table)

```sql
-- Orphan org sample (no secrets: KeyHash truncated)
SELECT d."Id", d."OrganizationId", d."Name", d."Scopes",
       left(d."KeyHash", 12) AS keyhash_prefix
FROM lhdn."DeveloperApiKeys" d
WHERE d."IsActive" = true
  AND NOT EXISTS (
    SELECT 1 FROM one."Organizations" o WHERE o."Id" = d."OrganizationId"
  )
LIMIT 50;

-- Same Id on One with different KeyHash (migrator must mint new Id)
SELECT COUNT(*) AS id_collision_diff_hash
FROM lhdn."DeveloperApiKeys" d
JOIN one."ApiCredentials" a
  ON a."Id" = d."Id" AND a."KeyHash" <> d."KeyHash";

-- Scopes rows that may contain tokens outside AllKnownScopes
-- (human review of Q12 distribution; exact token split is migrator C# /
--  unnest logic — pure SQL allowlist must match PlatformApiScopes)
```

**Cutover readiness metric:** `active_legacy_only = 0` (Q8) before R05 One-only middleware in that env.

---

## Results

| Env | Active Lhdn (Q1) | Active One (Q4) | Active legacy-only (Q8) | Inactive Lhdn (Q2) | Notes |
|-----|------------------|-----------------|-------------------------|--------------------|-------|
| Staging | **blocked** | **blocked** | **blocked** | **blocked** | Needs ops DB access |
| Prod | **blocked** | **blocked** | **blocked** | **blocked** | Needs ops DB access |
| Local (`lazuar_mvp` @ `lazuar-postgres:5433`) | 0 | 0 | 0 | 0 | Tables present; empty dataset (dev). Not a substitute for staging/prod |

### Local Q1–Q12 (2026-08-09)

| Metric | Value |
|--------|------:|
| Q1 active_legacy | 0 |
| Q2 inactive_legacy | 0 |
| Q3 total_legacy | 0 |
| Q4 active_one | 0 |
| Q5 inactive_one | 0 |
| Q6 total_one | 0 |
| Q7 legacy_hashes_already_on_one | 0 |
| Q8 active_legacy_only | 0 |
| Q9 inactive_legacy_only | 0 |
| Q10 empty_or_blank_hash | 0 |
| Q11 orphan_org_active_legacy | 0 |
| Q12 scope_distribution | _(no rows)_ |

Docker: `lazuar-postgres` (postgres:17.10-alpine) Up healthy; `docker compose ps` in repo showed no compose-managed stack (host container still usable).

---

## Decision

| Item | Status |
|------|--------|
| Staging/prod counts | **Blocked** — needs ops DB access |
| Early cutover (accelerate) | **Not claimed** — cannot assert prod `active_legacy_only = 0` without prod inventory |
| **R03** | **GO — implement full migrator (safe path)** even if later staging/prod prove zero rows (migrator may no-op / verify-only). Prefer ready path over waiting on ops. |
| **R04** | After R03; execute on staging then prod with dry-run first |
| **R05** | **Blocked** until prod **`active_legacy_only = 0`** (or signed residual quarantine) after R04 |
| **migrate_policy** | **`all_rows`** |
| Allowlist | `PlatformApiScopes.AllKnownScopes` |

Sign-off for accelerate: **N/A** (counts pending ops).

---

## R02.4 Exit

- [x] Numbers status committed in this note (staging/prod blocked; local zero)
- [x] R03 go / no-go clear: **GO implement full migrator (safe path)**
- [x] SQL package for ops handoff: `r02-inventory.sql`
- [x] No app code / middleware change
- [x] No secrets in this note

---

## Next

**R03 — Implement API key migrator** (`checklists/r03-keys-migrator-implement.md`):

- Idempotent insert into `one.ApiCredentials` from `lhdn.DeveloperApiKeys` (`all_rows`)
- Skip already-on-One by `KeyHash`; quarantine empty hash / orphan org / unknown-scopes-only
- Prefer preserve `Id`; on Id collision with different hash → new Id + mapping log
- Dry-run counts before write; do **not** remove dual-read (R05)
- Ops: when DB access available, run `r02-inventory.sql` on staging + prod and update results table
