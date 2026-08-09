# R41 — Webhook registry backfill runbook

**Track:** Webhooks · **Checklist:** `checklists/r41-webhooks-registry-backfill.md`  
**Depends on:** R40 (product lock)  
**Does not:** Dual-write `/lhdn/webhooks` register API · enqueue path (R42) · retire fire-and-forget (R43)

---

## What it does

One-shot hosted job that **copies active** rows from `lhdn.WebhookSubscriptions` → `one.TenantWebhookEndpoints`.

| Field | Action |
|-------|--------|
| `Id` | **New** `Guid.CreateVersion7()` via `TenantWebhookEndpoint` domain constructor (Lhdn Id not preserved) |
| `OrganizationId`, `Url` | Copy (URL normalized via `WebhookUrlValidator`) |
| `Secret` → `SecretKey` | **Preserve as-is** (no `whsec_` remint) so dual-verify / customer HMAC stays valid |
| `IsActive` | Always `true` on insert (source query is active-only) |
| `EnabledEvents` | **`["invoice.valid","invoice.invalid"]`** — not empty (avoids commerce fan-out to e-invoice-only URLs) |
| `CreatedAt` | Prefer Lhdn `CreatedAt` |
| `UpdatedAt` | Migration time (domain `UtcNow`) |

**Idempotency:** skip when One already has same `OrganizationId` + `Url`.  
**Race safety:** `INSERT … WHERE NOT EXISTS (Org+Url)`.  
**Policy:** migrate **active only** (`IsActive = true`). Inactive Lhdn rows are ignored.  
**Invalid URLs / empty secrets / missing orgs:** quarantine (log + skip; no insert).

Code: `apps/lazuar-api/src/Lazuar.Api/Jobs/WebhookSubscriptionMigration/`.

**Not in this job:** dual-write of Lhdn register/list/delete API over One (optional later / R43 façade).

---

## Configuration

| Source | Keys |
|--------|------|
| `appsettings.json` section `WebhookSubscriptionMigration` | `Enabled` (default **false**), `DryRun` (default **true**), `BatchSize` (default **500**) |
| Environment (overrides section) | `WEBHOOK_SUBSCRIPTION_MIGRATION_ENABLED`, `WEBHOOK_SUBSCRIPTION_MIGRATION_DRY_RUN` |

Host registers the hosted service **only when `Enabled=true`** (after env override). Lhdn fire-and-forget dispatcher is never changed by this job.

---

## How to run

### 0. Pre-check counts

```sql
SELECT COUNT(*) AS active_lhdn
FROM lhdn."WebhookSubscriptions"
WHERE "IsActive" = true;

SELECT COUNT(*) AS active_one
FROM one."TenantWebhookEndpoints"
WHERE "IsActive" = true;

-- Active Lhdn with no matching One endpoint (Org + Url)
SELECT COUNT(*) AS active_lhdn_missing_on_one
FROM lhdn."WebhookSubscriptions" l
WHERE l."IsActive" = true
  AND NOT EXISTS (
    SELECT 1
    FROM one."TenantWebhookEndpoints" e
    WHERE e."OrganizationId" = l."OrganizationId"
      AND e."Url" = l."Url"
  );
```

Paste staging/prod results into ops log / R41 notes.

### 1. Dry-run (recommended first)

```bash
export WEBHOOK_SUBSCRIPTION_MIGRATION_ENABLED=true
export WEBHOOK_SUBSCRIPTION_MIGRATION_DRY_RUN=true
# start API against the target DB (staging then prod)
```

Watch logs for:

```text
Webhook subscription migration finished. DryRun=True Processed=… WouldInsert=… AlreadyMigrated=… Quarantined=…
```

Quarantine rows log `SourceId` + `Code` + optional `Detail` — **never** secrets.

### 2. Live insert

```bash
export WEBHOOK_SUBSCRIPTION_MIGRATION_ENABLED=true
export WEBHOOK_SUBSCRIPTION_MIGRATION_DRY_RUN=false
```

Restart the API once. Job runs a few seconds after boot (post-migration settle). Set `Enabled=false` again after a successful run so the one-shot does not re-register on every deploy (re-run is safe/idempotent but noisy).

### 3. Verify (SQL)

```sql
-- Residual active Lhdn not on One by Org+Url (should be 0, or only quarantined residuals)
SELECT l."Id", l."OrganizationId", left(l."Url", 80) AS url_prefix
FROM lhdn."WebhookSubscriptions" l
WHERE l."IsActive" = true
  AND NOT EXISTS (
    SELECT 1
    FROM one."TenantWebhookEndpoints" e
    WHERE e."OrganizationId" = l."OrganizationId"
      AND e."Url" = l."Url"
  );

-- Sample migrated event filters (should be invoice.* only, not empty)
SELECT e."Id", e."OrganizationId", e."EnabledEvents", e."IsActive"
FROM one."TenantWebhookEndpoints" e
WHERE e."EnabledEvents" @> '["invoice.valid"]'::jsonb
LIMIT 20;
```

**Do not** `SELECT "SecretKey"` / `"Secret"` in shared logs.

### 4. Smoke (optional)

- Org that had Lhdn active URL now appears under One webhook list (if product API available).
- After R42 enqueue is enabled: validated invoice should fan-out to migrated URL with preserved secret signature path (dual-verify window per R40 §3).

---

## Outcome codes

| Code | Meaning |
|------|---------|
| `inserted` | Row written to One |
| `would_insert` | Dry-run would write |
| `already_migrated` | Same `OrganizationId` + `Url` already on One |
| `insert_conflict` | Concurrent insert race on Org+Url |
| `quarantine_invalid_url` | Failed `WebhookUrlValidator` (e.g. http non-loopback) |
| `quarantine_empty_secret` | Null/blank Lhdn `Secret` |
| `quarantine_orphan_org` | `OrganizationId` missing from `one.Organizations` |

---

## Safety / transactions

- **Per-row** insert (not one giant transaction): mid-run failure leaves already-inserted rows on One; Lhdn table untouched; re-run is idempotent on Org+Url.
- Failure of the hosted job is logged; it does **not** crash the host or touch fire-and-forget.
- **Never** log full secrets or full customer webhook URLs in aggregate logs (SourceId + codes only for warnings).
- Domain path: `new TenantWebhookEndpoint(org, url, secret, true, invoiceEvents)` — **not** `CreateWebhookEndpointCommand` (which mints `whsec_`).

---

## Rollback

| State | Action |
|-------|--------|
| After dry-run | Nothing written; Lhdn path unchanged |
| After live insert, before R42 | Leave Lhdn fire-and-forget on. Optionally delete wrongly inserted One rows by report `TargetId` |
| Do **not** drop `lhdn.WebhookSubscriptions` here | That is post-R43 after dual-read/façade window |

---

## Exit (R41 implement)

- [x] Hosted one-shot migrator implemented
- [x] Secrets preserved; EnabledEvents = invoice.valid / invoice.invalid
- [x] Unit tests for `LegacyWebhookSubscriptionMigrator` (in-memory fake store)
- [x] Dual-write of register API **skipped** (optional; not required for backfill)
- Ready for staging then prod execute (counts + dry-run → live)
