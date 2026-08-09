-- R02 / F01 API key legacy inventory
-- Branch: chore/remaining-005
-- Date: 2026-08-09
-- Analysis: plans/005-remaining/01-api-key-one-only-cutover.md §4.2
-- Notes: plans/005-remaining/r02-notes.md
--
-- Run on staging then prod (ops). Local optional: lazuar_mvp on lazuar-postgres.
-- Read-only. Do not paste secrets or full KeyHash into tickets; use left(KeyHash,12) if sampling.
-- Allowlist for scope quarantine (migrator): PlatformApiScopes.AllKnownScopes
-- migrate_policy (recommended): all_rows

-- =============================================================================
-- Condensed metrics (Q1–Q12 one result set)
-- =============================================================================
SELECT 'Q1_active_legacy' AS metric, COUNT(*)::text AS value
FROM lhdn."DeveloperApiKeys" WHERE "IsActive" = true
UNION ALL
SELECT 'Q2_inactive_legacy', COUNT(*)::text
FROM lhdn."DeveloperApiKeys" WHERE "IsActive" = false
UNION ALL
SELECT 'Q3_total_legacy', COUNT(*)::text
FROM lhdn."DeveloperApiKeys"
UNION ALL
SELECT 'Q4_active_one', COUNT(*)::text
FROM one."ApiCredentials" WHERE "IsActive" = true
UNION ALL
SELECT 'Q5_inactive_one', COUNT(*)::text
FROM one."ApiCredentials" WHERE "IsActive" = false
UNION ALL
SELECT 'Q6_total_one', COUNT(*)::text
FROM one."ApiCredentials"
UNION ALL
SELECT 'Q7_legacy_hashes_already_on_one', COUNT(*)::text
FROM lhdn."DeveloperApiKeys" d
WHERE EXISTS (
  SELECT 1 FROM one."ApiCredentials" a WHERE a."KeyHash" = d."KeyHash"
)
UNION ALL
SELECT 'Q8_active_legacy_only', COUNT(*)::text
FROM lhdn."DeveloperApiKeys" d
WHERE d."IsActive" = true
  AND NOT EXISTS (
    SELECT 1 FROM one."ApiCredentials" a WHERE a."KeyHash" = d."KeyHash"
  )
UNION ALL
SELECT 'Q9_inactive_legacy_only', COUNT(*)::text
FROM lhdn."DeveloperApiKeys" d
WHERE d."IsActive" = false
  AND NOT EXISTS (
    SELECT 1 FROM one."ApiCredentials" a WHERE a."KeyHash" = d."KeyHash"
  )
UNION ALL
SELECT 'Q10_empty_or_blank_hash', COUNT(*)::text
FROM lhdn."DeveloperApiKeys" d
WHERE d."KeyHash" IS NULL OR length(trim(d."KeyHash")) = 0
UNION ALL
SELECT 'Q11_orphan_org_active_legacy', COUNT(*)::text
FROM lhdn."DeveloperApiKeys" d
WHERE d."IsActive" = true
  AND NOT EXISTS (
    SELECT 1 FROM one."Organizations" o WHERE o."Id" = d."OrganizationId"
  )
UNION ALL
SELECT 'Q12_id_collision_diff_hash', COUNT(*)::text
FROM lhdn."DeveloperApiKeys" d
JOIN one."ApiCredentials" a
  ON a."Id" = d."Id" AND a."KeyHash" <> d."KeyHash";

-- =============================================================================
-- Individual queries (same metrics; use when ops prefers step-by-step)
-- =============================================================================

-- Q1 Active legacy keys
SELECT COUNT(*) AS active_legacy
FROM lhdn."DeveloperApiKeys"
WHERE "IsActive" = true;

-- Q2 Inactive legacy
SELECT COUNT(*) AS inactive_legacy
FROM lhdn."DeveloperApiKeys"
WHERE "IsActive" = false;

-- Q3 Total legacy
SELECT COUNT(*) AS total_legacy
FROM lhdn."DeveloperApiKeys";

-- Q4 Active One
SELECT COUNT(*) AS active_one
FROM one."ApiCredentials"
WHERE "IsActive" = true;

-- Q5 Inactive One
SELECT COUNT(*) AS inactive_one
FROM one."ApiCredentials"
WHERE "IsActive" = false;

-- Q6 Total One
SELECT COUNT(*) AS total_one
FROM one."ApiCredentials";

-- Q7 Legacy hashes already on One
SELECT COUNT(*) AS legacy_hashes_already_on_one
FROM lhdn."DeveloperApiKeys" d
WHERE EXISTS (
  SELECT 1 FROM one."ApiCredentials" a WHERE a."KeyHash" = d."KeyHash"
);

-- Q8 Active legacy-only (cutover blocker)
SELECT COUNT(*) AS active_legacy_only
FROM lhdn."DeveloperApiKeys" d
WHERE d."IsActive" = true
  AND NOT EXISTS (
    SELECT 1 FROM one."ApiCredentials" a WHERE a."KeyHash" = d."KeyHash"
  );

-- Q9 Inactive legacy-only
SELECT COUNT(*) AS inactive_legacy_only
FROM lhdn."DeveloperApiKeys" d
WHERE d."IsActive" = false
  AND NOT EXISTS (
    SELECT 1 FROM one."ApiCredentials" a WHERE a."KeyHash" = d."KeyHash"
  );

-- Q10 Empty / blank KeyHash
SELECT COUNT(*) AS empty_or_blank_hash
FROM lhdn."DeveloperApiKeys" d
WHERE d."KeyHash" IS NULL
   OR length(trim(d."KeyHash")) = 0;

-- Q11 Orphan org count (active legacy)
SELECT COUNT(*) AS orphan_org_active_legacy
FROM lhdn."DeveloperApiKeys" d
WHERE d."IsActive" = true
  AND NOT EXISTS (
    SELECT 1 FROM one."Organizations" o WHERE o."Id" = d."OrganizationId"
  );

-- Q12 Scope distribution (active legacy) — review against PlatformApiScopes.AllKnownScopes
SELECT d."Scopes", COUNT(*) AS n
FROM lhdn."DeveloperApiKeys" d
WHERE d."IsActive" = true
GROUP BY d."Scopes"
ORDER BY COUNT(*) DESC;

-- =============================================================================
-- Samples (optional)
-- =============================================================================

-- Orphan org sample (KeyHash truncated — not full secret material)
SELECT d."Id",
       d."OrganizationId",
       d."Name",
       d."Scopes",
       left(d."KeyHash", 12) AS keyhash_prefix
FROM lhdn."DeveloperApiKeys" d
WHERE d."IsActive" = true
  AND NOT EXISTS (
    SELECT 1 FROM one."Organizations" o WHERE o."Id" = d."OrganizationId"
  )
LIMIT 50;

-- Would-insert under clean path (all_rows; no scope quarantine in pure SQL)
SELECT COUNT(*) AS would_insert_clean_path
FROM lhdn."DeveloperApiKeys" d
WHERE d."KeyHash" IS NOT NULL
  AND length(trim(d."KeyHash")) > 0
  AND EXISTS (SELECT 1 FROM one."Organizations" o WHERE o."Id" = d."OrganizationId")
  AND NOT EXISTS (
    SELECT 1 FROM one."ApiCredentials" a WHERE a."KeyHash" = d."KeyHash"
  )
  AND NOT EXISTS (
    SELECT 1 FROM one."ApiCredentials" a WHERE a."Id" = d."Id"
  );
