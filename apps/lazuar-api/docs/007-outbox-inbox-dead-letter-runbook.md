# 007 — Outbox / Inbox Dead-Letter Runbook

**Audience:** on-call / platform engineers  
**Related code:** `BuildingBlocks.Infrastructure` (`OutboxPublisherJob`, `InboxConsumerJob`, `MessageRetryPolicy`, `MessageProcessingStatus`)  
**Policy:** max **5** attempts; exponential backoff `2^attempt` minutes after each failed attempt; terminal state `Status = 'Dead'` with `ProcessedAt` set.

---

## 1. Schemas and tables

Each module owns private schema tables:

| Module | Schema | Outbox | Inbox |
|--------|--------|--------|-------|
| One | `one` | `OutboxMessages` | `InboxMessages` |
| Messaging | `messaging` | `OutboxMessages` | `InboxMessages` |
| Payments | `payments` | `OutboxMessages` | `InboxMessages` |
| CRM | `crm` | `OutboxMessages` | `InboxMessages` |
| Ops | `ops` | `OutboxMessages` | `InboxMessages` |
| Billing | `billing` | `OutboxMessages` | `InboxMessages` |
| Lhdn | `lhdn` | `OutboxMessages` | `InboxMessages` |
| Commerce | `commerce` | `OutboxMessages` | `InboxMessages` |
| Communications | `communications` | `OutboxMessages` | `InboxMessages` |

Replace `:schema` below with the target schema name.

---

## 2. List dead-letter rows

Dead letters are terminal: `Status = 'Dead'`. They keep the last error for support.

### Outbox

```sql
SELECT
    "Id",
    "Type",
    "AttemptCount",
    "Status",
    "OccurredOn",
    "ProcessedAt",
    "NextAttemptAt",
    LEFT("Error", 500) AS "ErrorPreview"
FROM :schema."OutboxMessages"
WHERE "Status" = 'Dead'
ORDER BY "ProcessedAt" DESC NULLS LAST
LIMIT 100;
```

### Inbox

```sql
SELECT
    "Id",
    "Type",
    "AttemptCount",
    "Status",
    "ReceivedAt",
    "ProcessedAt",
    "NextAttemptAt",
    LEFT("Error", 500) AS "ErrorPreview"
FROM :schema."InboxMessages"
WHERE "Status" = 'Dead'
ORDER BY "ProcessedAt" DESC NULLS LAST
LIMIT 100;
```

### All schemas (union)

```sql
SELECT 'one' AS schema, 'outbox' AS box, "Id", "Type", "AttemptCount", "ProcessedAt", LEFT("Error", 200) AS err
FROM one."OutboxMessages" WHERE "Status" = 'Dead'
UNION ALL
SELECT 'messaging', 'outbox', "Id", "Type", "AttemptCount", "ProcessedAt", LEFT("Error", 200)
FROM messaging."OutboxMessages" WHERE "Status" = 'Dead'
UNION ALL
SELECT 'payments', 'outbox', "Id", "Type", "AttemptCount", "ProcessedAt", LEFT("Error", 200)
FROM payments."OutboxMessages" WHERE "Status" = 'Dead'
UNION ALL
SELECT 'crm', 'outbox', "Id", "Type", "AttemptCount", "ProcessedAt", LEFT("Error", 200)
FROM crm."OutboxMessages" WHERE "Status" = 'Dead'
UNION ALL
SELECT 'ops', 'outbox', "Id", "Type", "AttemptCount", "ProcessedAt", LEFT("Error", 200)
FROM ops."OutboxMessages" WHERE "Status" = 'Dead'
UNION ALL
SELECT 'billing', 'outbox', "Id", "Type", "AttemptCount", "ProcessedAt", LEFT("Error", 200)
FROM billing."OutboxMessages" WHERE "Status" = 'Dead'
UNION ALL
SELECT 'lhdn', 'outbox', "Id", "Type", "AttemptCount", "ProcessedAt", LEFT("Error", 200)
FROM lhdn."OutboxMessages" WHERE "Status" = 'Dead'
UNION ALL
SELECT 'commerce', 'outbox', "Id", "Type", "AttemptCount", "ProcessedAt", LEFT("Error", 200)
FROM commerce."OutboxMessages" WHERE "Status" = 'Dead'
UNION ALL
SELECT 'communications', 'outbox', "Id", "Type", "AttemptCount", "ProcessedAt", LEFT("Error", 200)
FROM communications."OutboxMessages" WHERE "Status" = 'Dead'
ORDER BY "ProcessedAt" DESC NULLS LAST;
```

Repeat the same pattern for `"InboxMessages"` if needed.

---

## 3. Counts by status / backlog

```sql
-- Outbox status histogram
SELECT "Status", COUNT(*) AS n
FROM :schema."OutboxMessages"
GROUP BY "Status"
ORDER BY n DESC;

-- Pending claimable now (matches publisher claim predicate)
SELECT COUNT(*) AS claimable_now
FROM :schema."OutboxMessages"
WHERE "ProcessedAt" IS NULL
  AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= NOW())
  AND "OccurredOn" <= NOW();

-- Waiting on backoff (not yet claimable)
SELECT COUNT(*) AS waiting_backoff
FROM :schema."OutboxMessages"
WHERE "ProcessedAt" IS NULL
  AND "NextAttemptAt" IS NOT NULL
  AND "NextAttemptAt" > NOW();

-- Dead count
SELECT COUNT(*) AS dead
FROM :schema."OutboxMessages"
WHERE "Status" = 'Dead';
```

### Inbox variants

```sql
SELECT "Status", COUNT(*) AS n
FROM :schema."InboxMessages"
GROUP BY "Status";

SELECT COUNT(*) AS claimable_now
FROM :schema."InboxMessages"
WHERE "ProcessedAt" IS NULL
  AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= NOW());
```

---

## 4. Pending lag (oldest unprocessed)

```sql
-- Outbox: age of oldest unprocessed (includes backoff / not-yet-due)
SELECT
    MIN("OccurredOn") AS oldest_occurred_on,
    NOW() - MIN("OccurredOn") AS lag,
    COUNT(*) AS pending_rows
FROM :schema."OutboxMessages"
WHERE "ProcessedAt" IS NULL;

-- Outbox: age of oldest *claimable* message
SELECT
    MIN("OccurredOn") AS oldest_claimable_on,
    NOW() - MIN("OccurredOn") AS claimable_lag,
    COUNT(*) AS claimable_rows
FROM :schema."OutboxMessages"
WHERE "ProcessedAt" IS NULL
  AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= NOW())
  AND "OccurredOn" <= NOW();

-- Inbox lag
SELECT
    MIN("ReceivedAt") AS oldest_received_at,
    NOW() - MIN("ReceivedAt") AS lag,
    COUNT(*) AS pending_rows
FROM :schema."InboxMessages"
WHERE "ProcessedAt" IS NULL;
```

---

## 5. Inspect a single message

```sql
SELECT *
FROM :schema."OutboxMessages"
WHERE "Id" = '<message-guid>';

-- Full error (can be large)
SELECT "Error"
FROM :schema."OutboxMessages"
WHERE "Id" = '<message-guid>';
```

---

## 6. Replay procedure (manual reset)

**When:** root cause of failure is fixed (handler bug, missing type registration, transient dependency restored).  
**Risk:** replaying side-effectful handlers may double-apply work if the original attempt partially succeeded. Prefer idempotent handlers; otherwise verify domain state before replay.

### Replay one outbox row

```sql
UPDATE :schema."OutboxMessages"
SET
    "Status" = 'Pending',
    "ProcessedAt" = NULL,
    "NextAttemptAt" = NULL,          -- claim immediately on next poll
    "AttemptCount" = 0,              -- full retry budget; keep previous Error for audit if preferred
    "Error" = NULL                   -- optional: clear, or leave last error until success
WHERE "Id" = '<message-guid>'
  AND "Status" = 'Dead';
```

### Replay one inbox row

```sql
UPDATE :schema."InboxMessages"
SET
    "Status" = 'Pending',
    "ProcessedAt" = NULL,
    "NextAttemptAt" = NULL,
    "AttemptCount" = 0,
    "Error" = NULL
WHERE "Id" = '<message-guid>'
  AND "Status" = 'Dead';
```

### Replay a type (careful)

```sql
UPDATE :schema."OutboxMessages"
SET
    "Status" = 'Pending',
    "ProcessedAt" = NULL,
    "NextAttemptAt" = NULL,
    "AttemptCount" = 0
WHERE "Status" = 'Dead'
  AND "Type" = 'Fully.Qualified.Event.Type.Name'
  AND "ProcessedAt" >= NOW() - INTERVAL '7 days';
```

Publishers/consumers poll about every **5 seconds** (or sooner on `DatabaseJobTrigger`). After reset, the row should be claimed when `OccurredOn`/`NextAttemptAt` allow.

### Soft replay (preserve attempt history)

If you only want one more try without resetting the budget:

```sql
UPDATE :schema."OutboxMessages"
SET
    "Status" = 'Pending',
    "ProcessedAt" = NULL,
    "NextAttemptAt" = NULL,
    "AttemptCount" = GREATEST("AttemptCount" - 1, 0)  -- free one attempt
WHERE "Id" = '<message-guid>'
  AND "Status" = 'Dead';
```

---

## 7. Retry / poison policy reference

| Attempt after failure | Backoff before next claim |
|----------------------|---------------------------|
| 1 | 2 minutes |
| 2 | 4 minutes |
| 3 | 8 minutes |
| 4 | 16 minutes |
| 5 | **Dead** (`Status = 'Dead'`, `ProcessedAt = UtcNow`) |

Formula: `NextAttemptAt = UtcNow + 2^AttemptCount minutes` where `AttemptCount` is the value **after** increment.  
`MaxAttempts = 5` (`MessageRetryPolicy.MaxAttempts`).

**Success path:** `ProcessedAt = UtcNow`, `Error = null`, `NextAttemptAt = null` (Status remains `Pending` unless previously dead—success does not rewrite Status; normal path never leaves Pending until Dead).

**Failure path (attempt &lt; 5):** do **not** set `ProcessedAt`; set `Error`, increment `AttemptCount`, schedule `NextAttemptAt`.

---

## 8. Operational tips

1. **Spike of Dead rows for one Type** → usually a deploy-time type resolver break or a permanently failing handler.
2. **High `waiting_backoff` with low `claimable_now`** → expected under retry storms; check lag and dead counts separately.
3. **`ProcessedAt` set and `Status = 'Pending'`** → successful completion (legacy/success path). Dead rows always have `Status = 'Dead'`.
4. Prefer single-row replay while validating a fix; batch replay only after confidence.
5. Admin/API replay endpoints are out of scope for Phase 0.2 — SQL is the supported path.

---

## 9. Apply schema migrations

From `apps/lazuar-api/src/Lazuar.Api` (or via `task` / Taskfile `api:db:update`):

```bash
dotnet ef database update --context OneDbContext \
  --project ../../Modules/One/Infrastructure/Modules.One.Infrastructure.csproj \
  --startup-project Lazuar.Api.csproj
# …repeat for Messaging, Payments, CrmDbContext, Ops, Billing, Lhdn, Commerce, Communications
```

Migration name: `AddOutboxInboxRetryAndDeadLetter`.
