# 05 — Pluginize PlatformMetricsCollector / kill multi-schema product SQL in BuildingBlocks (FW-3 / FW-4)

**Status:** Analysis only — no app code changes  
**Date:** 2026-08-09  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Related track items:**
- FW-3 item 8 (metrics contributors) — [`plans/004-maintenance/FUTURE-WORK.md`](../004-maintenance/FUTURE-WORK.md)
- FW-4 leak class: `PlatformMetricsCollector` multi-schema product SQL
- Future checklist F13 — [`plans/004-maintenance/checklists-future/phase-f13-bb-metrics-plugins.md`](../004-maintenance/checklists-future/phase-f13-bb-metrics-plugins.md)
- Ownership map — [`apps/lazuar-api/docs/009-building-blocks-ownership.md`](../../apps/lazuar-api/docs/009-building-blocks-ownership.md)
- Prior inventory — [`plans/004-maintenance/06-building-blocks-shared-kernel.md`](../004-maintenance/06-building-blocks-shared-kernel.md) §2.4 G, §4.2 (1), Phase M1.5 / M3
- Phase 15 left only a **comment** on the collector (plugin direction, no interface)

---

## 0. Executive summary

`PlatformMetricsCollector` is a temporary “god collector” living in BuildingBlocks Infrastructure. It:

1. Hardcodes the **inventory of all nine module schemas**.
2. Runs **generic technical SQL** against every module’s `OutboxMessages` / `InboxMessages` (same table shape — messaging spine).
3. Runs **product SQL** against `lhdn."TaxDocuments"` with LHDN domain status vocabulary (`PENDING` / `SUBMITTED`).
4. Merges **process-lifetime counters** from `LazuarMetrics`, including product counters (`dunning.cancels`, multi-source `webhook.failed`).

That violates the refined BB rule: **no private-schema product SQL that encodes one module’s domain vocabulary**, and it creates a **module-onboarding tax** (edit BB when adding a schema).

**Target end state:**

| Concern | Owner |
|---------|--------|
| DB ping + aggregate collect orchestration + publish gauges | BuildingBlocks (thin aggregator) |
| Generic outbox/inbox lag/pending/dead SQL | BuildingBlocks **or** shared helper, driven by **DI-registered schema names** (not a BB constant) |
| LHDN stuck TaxDocuments count + threshold options | **Lhdn** `IPlatformMetricsContributor` |
| Dunning cancel counter | **Commerce** (or generic tagged meter API; not BB-named product API) |
| Webhook failed counters | Already multi-source; keep tagged technical counter **or** per-module meters — clarify ownership |
| Readiness gate on max outbox lag | Host + BB `HealthReadiness` (unchanged consumers) |
| `/health`, `/health/ready`, `/health/metrics` | Host (`HealthEndpointExtensions`) |

Architecture constraint (enforced by `ModuleBoundaryTests`): **BuildingBlocks must not reference `Modules.*`**. Plugins invert the dependency: modules implement a BB-defined contributor interface and register in DI; the aggregator takes `IEnumerable<IPlatformMetricsContributor>`.

---

## 1. Current metrics surface (complete inventory)

### 1.1 Source tree (today)

```
apps/lazuar-api/BuildingBlocks/
  Application/Observability/
    LazuarMetrics.cs                          # process counters + Meter "Lazuar.Hub"
  Infrastructure/Observability/
    IPlatformMetricsCollector.cs              # CollectAsync + CanConnectAsync
    PlatformMetricsCollector.cs               # god SQL + schema list
    PlatformMetricsSnapshot.cs                # DTO + SchemaOutboxMetrics
    LazuarMetricsGauges.cs                    # observable gauges from snapshot
    PlatformMetricsRefreshJob.cs              # BackgroundService periodic CollectAsync
    HealthReadiness.cs                        # /health/ready evaluation
    ObservabilityOptions.cs                   # LhdnStuckThreshold, lag gate, refresh interval

apps/lazuar-api/src/Lazuar.Api/
  Program.cs                                  # DI: gauges, collector singleton, refresh job
  Composition/HealthEndpointExtensions.cs     # /health, /health/ready, /health/metrics
  appsettings.json → "Observability" section
```

There is **no** OpenTelemetry exporter / Prometheus scrape endpoint in the API project today. Production visibility is:

- System.Diagnostics.Metrics instruments (if anything attaches a listener later), and
- JSON snapshot via `GET /health/metrics`, and
- Gauge snapshots refreshed by `PlatformMetricsRefreshJob`.

### 1.2 Process counters — `LazuarMetrics` (Application)

**File:** `BuildingBlocks/Application/Observability/LazuarMetrics.cs`  
**Meter name:** `Lazuar.Hub` (version `1.0.0`)

| Instrument | API | Process total field | Call sites |
|------------|-----|---------------------|------------|
| Counter `lazuar.outbox.dead_letters` | `RecordDeadLetter()` | `DeadLettersTotal` | `MessageProcessingResultApplier.ApplyFailure` when status → `Dead` (technical messaging spine — **correct BB home**) |
| Counter `lazuar.webhook.failed` (+ optional tag `source`) | `RecordWebhookFailed(source?)` | `WebhookFailedTotal` | One `OutboundWebhookDispatcherJob` (`"outbound"`); Payments `ProcessGatewayWebhookCommandHandler` (`"payment"`); Lhdn `WebhookSenderService` (`"lhdn"`) |
| Counter `lazuar.dunning.cancels` | `RecordDunningCancel()` | `DunningCancelsTotal` | Commerce `DunningEngineJob.PastDue` only |

Notes:

- Counters also keep `Interlocked` process-lifetime totals so `/health/metrics` can show `counters.*_since_start` without a metrics backend.
- Application-layer placement is intentional so handlers/jobs need no Infrastructure reference for recording.
- **Product-shaped APIs on a shared static class** (`RecordDunningCancel`) couple BB’s public Application surface to Commerce vocabulary even though no SQL is involved.

### 1.3 Observable gauges — `LazuarMetricsGauges` (Infrastructure)

**File:** `BuildingBlocks/Infrastructure/Observability/LazuarMetricsGauges.cs`  
Registered once via `EnsureRegistered()` (Program.cs at boot + again on first publish).

| Gauge | Source field on `PlatformMetricsSnapshot` | Semantics |
|-------|-------------------------------------------|-----------|
| `lazuar.outbox.lag_seconds` | `OutboxLagSeconds` | **Max** unprocessed outbox age (seconds) across all schemas |
| `lazuar.outbox.pending_count` | `OutboxPendingCount` | Sum of unprocessed non-Dead outbox rows |
| `lazuar.outbox.dead_letters_count` | `DeadLetterCount` | Sum of Dead outbox + Dead inbox rows |
| `lazuar.lhdn.stuck_count` | `LhdnStuckCount` | TaxDocuments stuck in PENDING/SUBMITTED older than threshold |

Snapshot is held in a `volatile` static; gauge callbacks read it. `PlatformMetricsCollector.CollectAsync` and the refresh job call `PublishSnapshot`.

### 1.4 On-demand collector — `PlatformMetricsCollector`

**File:** `BuildingBlocks/Infrastructure/Observability/PlatformMetricsCollector.cs`

**Hardcoded inventory:**

```csharp
public static readonly string[] ModuleSchemas =
[
    "one", "messaging", "payments", "crm", "ops", "billing", "lhdn", "commerce", "communications"
];
```

Matches the nine live modules and docs/007 outbox runbook. **Not** derived from DI or module registration.

**SQL A — per-schema outbox (technical shape):**

```sql
SELECT
    COUNT(*) FILTER (WHERE "ProcessedAt" IS NULL AND "Status" IS DISTINCT FROM 'Dead') AS pending,
    COUNT(*) FILTER (WHERE "Status" = 'Dead') AS dead,
    COALESCE(
        MAX(EXTRACT(EPOCH FROM (NOW() AT TIME ZONE 'UTC' - "OccurredOn")))
            FILTER (WHERE "ProcessedAt" IS NULL AND "Status" IS DISTINCT FROM 'Dead'),
        0) AS lag_seconds
FROM "{schema}"."OutboxMessages"
```

Schema identifier is string-interpolated from the **fixed allow-list** only (not user input). Missing table → treat as empty (`UndefinedTable`).

**SQL B — per-schema inbox dead (technical shape):**

```sql
SELECT COUNT(*)
FROM "{schema}"."InboxMessages"
WHERE "Status" = 'Dead'
```

**SQL C — LHDN product stuck (domain vocabulary — wrong owner):**

```sql
SELECT COUNT(*)
FROM lhdn."TaxDocuments"
WHERE "ValidationStatus" IN ('PENDING', 'SUBMITTED')
  AND "UpdatedAt" < (NOW() AT TIME ZONE 'UTC') - @threshold
```

Threshold from `ObservabilityOptions.LhdnStuckThreshold` (default 1 hour; appsettings `"01:00:00"`). Missing table/schema → 0.

**Also folds process counters into every successful/failed snapshot:**

- `DeadLettersSinceStart` ← `LazuarMetrics.DeadLettersTotal`
- `WebhookFailedSinceStart` ← `LazuarMetrics.WebhookFailedTotal`
- `DunningCancelsSinceStart` ← `LazuarMetrics.DunningCancelsTotal`

**Connection model:** one `NpgsqlConnection` to `Default` connection string for the whole collect; sequential per-schema queries (not parallelized).

### 1.5 Snapshot DTO

**File:** `PlatformMetricsSnapshot.cs`

| Field | Kind |
|-------|------|
| `CollectedAtUtc` | meta |
| `OutboxLagSeconds` | aggregate technical gauge |
| `OutboxPendingCount` | aggregate technical gauge |
| `DeadLetterCount` | aggregate technical gauge |
| `LhdnStuckCount` | **first-class product field on a BB DTO** |
| `Schemas` (`SchemaOutboxMetrics[]`) | per-schema technical breakdown |
| `DeadLettersSinceStart` / `WebhookFailedSinceStart` / `DunningCancelsSinceStart` | process counters (mix of technical + product) |
| `DatabaseReachable` / `Error` | health meta |

`SchemaOutboxMetrics`: `Schema`, `OutboxPending`, `OutboxDead`, `InboxDead`, `OutboxLagSeconds`.

### 1.6 Options — `ObservabilityOptions`

Bound from config section `"Observability"`:

| Property | Default | Product coupling? |
|----------|---------|-------------------|
| `LhdnStuckThreshold` | 1 hour | **Yes** — Lhdn domain |
| `OutboxLagReadyThreshold` | null (disabled) | No — platform readiness |
| `MetricsRefreshInterval` | 30s | No — platform |

### 1.7 HTTP surface — host composition

**File:** `apps/lazuar-api/src/Lazuar.Api/Composition/HealthEndpointExtensions.cs`

| Route | Auth | Behavior |
|-------|------|----------|
| `GET /health` | none | `{ status: "ok" }` liveness |
| `GET /health/ready` | none | `HealthReadiness.EvaluateAsync`: DB `CanConnectAsync`; if `OutboxLagReadyThreshold` set, full `CollectAsync` and fail 503 when max lag exceeds threshold |
| `GET /health/metrics` | none | Full snapshot JSON (snake_case): lag/pending/dead, **`lhdn_stuck_count`**, process counters, `schemas[]` |

**Readiness does not use LHDN stuck count** — only connectivity and optional outbox lag. Product gauges are metrics/ops visibility, not k8s readiness today.

### 1.8 Background refresh

`PlatformMetricsRefreshJob`: delay 5s on start, then loop `CollectAsync` every `MetricsRefreshInterval`. Keeps gauges warm for any future Meter listener even when nobody hits `/health/metrics`.

### 1.9 DI registration (host)

**File:** `Program.cs` (approx. lines 72–81):

```csharp
LazuarMetricsGauges.EnsureRegistered();
builder.Services.AddSingleton<IPlatformMetricsCollector>(sp =>
    new PlatformMetricsCollector(
        defaultConnectionString,
        sp.GetRequiredService<IOptions<ObservabilityOptions>>(),
        sp.GetRequiredService<ILogger<PlatformMetricsCollector>>()));
builder.Services.AddHostedService<PlatformMetricsRefreshJob>();
```

Modules are registered later via `AddAllModules` (`ModuleRegistrationExtensions`): One, Messaging, CRM, Payments, Ops, Billing, Lhdn, Commerce, Communications — **none register metrics contributors today**.

### 1.10 Tests today

| Test | Path | Coverage |
|------|------|----------|
| `HealthReadinessTests` | `tests/.../Observability/HealthReadinessTests.cs` | DB down; ready with no lag gate; lag over/under threshold (mocked collector) |
| `LazuarMetricsTests` | `tests/.../Observability/LazuarMetricsTests.cs` | Dead-letter via applier; webhook + dunning counters |

**No** integration test that asserts `/health/metrics` JSON shape or real SQL collect. **No** architecture test that forbids schema-qualified product SQL inside BB (only assembly reference edges).

### 1.11 Call-site map (product metrics consumers)

| Metric | Module | File |
|--------|--------|------|
| Dead letter | BB (technical) | `MessageProcessingResultApplier.cs` |
| Webhook failed `outbound` | One | `OutboundWebhookDispatcherJob.cs` |
| Webhook failed `payment` | Payments | `ProcessGatewayWebhookCommandHandler.cs` |
| Webhook failed `lhdn` | Lhdn | `WebhookSenderService.cs` |
| Dunning cancel | Commerce | `DunningEngineJob.PastDue.cs` |
| LHDN stuck SQL | BB (wrong) | `PlatformMetricsCollector.QueryLhdnStuckAsync` |

---

## 2. What is wrong (problem taxonomy)

### 2.1 Conceptual reverse knowledge (FW-3 + FW-4)

Architecture tests ensure BB ↛ Modules assemblies. They **do not** catch:

- BB listing every private schema name as a constant.
- BB encoding `TaxDocuments` / `ValidationStatus` LHDN domain state.

docs/009 and plan 06 call this out explicitly. Phase 15 shipped **remarks only** on the collector.

### 2.2 Two different SQL smells — do not conflate them

| SQL | Shape | Verdict |
|-----|-------|---------|
| `"{schema}"."OutboxMessages"` / `InboxMessages` with Status/ProcessedAt/OccurredOn | **Shared messaging spine** tables owned by BB entities (`OutboxMessage`, `InboxMessage`) but **physically stored** in each module schema | Technical multi-schema scrape. Smell is **hardcoded inventory**, not “product vocabulary.” Fix: **registration of schema names**, keep shared SQL helper in BB. |
| `lhdn."TaxDocuments"` + PENDING/SUBMITTED | **Lhdn private aggregate** (`TaxDocument` domain) | Product SQL. **Must leave BB.** |

Killing “multi-schema product SQL in BB” means primarily **SQL C**. Schema-list registration is the companion fix so BB does not remain the module registry.

### 2.3 Module onboarding cost

Adding module N today requires:

1. Migrations + DbContext schema (module-owned — correct).
2. `Add*Module` outbox/inbox jobs (module-owned — correct).
3. **Edit `PlatformMetricsCollector.ModuleSchemas`** in BB (wrong).
4. Possibly docs/007 runbook union SQL (ops doc — acceptable if automated later).

After plugins: step 3 becomes `services.AddPlatformSchemaMetrics("newschema")` or automatic registration next to outbox job registration **inside the module DI**.

### 2.4 Product counters on shared Application API

`RecordDunningCancel` is soft leakage (no SQL, but BB Application names Commerce product). `RecordWebhookFailed` is multi-module with tags — closer to a **platform delivery failure** metric, still product-tagged by source.

### 2.5 First-class product fields on shared DTO / gauges / options / HTTP JSON

- `PlatformMetricsSnapshot.LhdnStuckCount`
- Gauge `lazuar.lhdn.stuck_count`
- `ObservabilityOptions.LhdnStuckThreshold`
- JSON key `lhdn_stuck_count`

Each is a frozen contract for anyone scraping `/health/metrics` or listening to the meter. Pluginization must either **preserve** these surfaces while sourcing them from Lhdn, or **version** the HTTP/gauge contract deliberately.

### 2.6 What is *not* broken

- Dead-letter counter tied to `MessageProcessingResultApplier` — correct BB technical metric.
- Host ownership of `/health*` and readiness lag gate — correct.
- Single Default connection string for cross-schema scrape in a modular monolith — acceptable for platform ops.
- Refresh job + observable gauges pattern — fine once snapshot is contributed.

---

## 3. Design goals and non-goals

### 3.1 Goals

1. **BB aggregator has zero product SQL** (no `TaxDocuments`, no status enums from modules).
2. **BB does not hardcode module schema inventory**; schemas come from DI registration.
3. **Modules can contribute** extra gauges/counts without editing BB collector source.
4. **Architecture tests stay green** (BB still has no ProjectReference to Modules).
5. **Preserve operational usefulness**: max outbox lag readiness, per-schema breakdown, process counters, LHDN stuck visibility (from Lhdn).
6. **Small PR slices** (FUTURE-WORK rule: one concern per PR; F08/F13 checklists).

### 3.2 Non-goals (this initiative)

- OpenTelemetry / Prometheus exporter (separate production-grade observability track).
- Moving LLM/email/messaging stacks (other FW-3 items).
- Fixing other cross-schema leaks (Communications receipts, etc.) — F07/F08 inventory; metrics path can land independently.
- Extracting BuildingBlocks into multiple csproj.
- Making metrics multi-tenant filtered (current SQL is platform-global counts — keep unless product asks).
- Inventing a Metrics module.

### 3.3 Invariants to preserve

- `IPlatformMetricsCollector.CanConnectAsync` / `CollectAsync` contract for readiness + endpoints.
- Readiness behavior: lag threshold optional; not blocked on LHDN stuck.
- Schema allow-list discipline for interpolated identifiers (never free-form user input).
- Missing table → empty metrics (graceful for partial migrations).

---

## 4. Contributor interface design

### 4.1 Recommended split of abstractions

Define **two** narrow registration types in BuildingBlocks (prefer Application for interfaces if collectors stay free of heavy deps; Infrastructure is also acceptable if only Infrastructure implements aggregation — either way modules’ Infrastructure can reference BB Application + Infrastructure as today).

#### A. Schema registration (technical outbox/inbox)

```csharp
// BuildingBlocks — name bikeshed ok; intent is "this schema participates in outbox metrics"
public interface IOutboxSchemaRegistration
{
    /// <summary>PostgreSQL schema that owns OutboxMessages / InboxMessages (allow-listed identifier).</summary>
    string Schema { get; }
}
```

Simple record implementation in BB:

```csharp
public sealed record OutboxSchemaRegistration(string Schema) : IOutboxSchemaRegistration;
```

**Why separate from full contributors:** every module needs the same outbox SQL. Duplicating that SQL nine times is worse than a shared helper. Modules only declare *identity*.

**Registration helper** (BB Infrastructure extension):

```csharp
public static IServiceCollection AddOutboxSchemaMetrics(
    this IServiceCollection services,
    string schema)
{
    // Validate identifier: ^[a-z][a-z0-9_]*$  (reject quotes, dots, uppercase surprises)
    services.AddSingleton<IOutboxSchemaRegistration>(new OutboxSchemaRegistration(schema));
    return services;
}
```

#### B. Product / extension contributors

```csharp
public interface IPlatformMetricsContributor
{
    /// <summary>Stable key for merge / diagnostics (e.g. "lhdn", "commerce").</summary>
    string Name { get; }

    Task ContributeAsync(
        PlatformMetricsCollectContext context,
        CancellationToken cancellationToken = default);
}
```

```csharp
public sealed class PlatformMetricsCollectContext
{
    public required NpgsqlConnection Connection { get; init; }  // already open, same Default DB
    public required DateTime CollectedAtUtc { get; init; }
    public required IConfiguration? Configuration { get; init; } // optional; prefer IOptions in contributor ctor
    public PlatformMetricsContributionBag Bag { get; } = new();
}
```

```csharp
public sealed class PlatformMetricsContributionBag
{
    // Product gauges / counters keyed by stable names
    private readonly Dictionary<string, long> _longs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _doubles = new(StringComparer.Ordinal);

    public void SetLong(string key, long value) => _longs[key] = value;
    public void SetDouble(string key, double value) => _doubles[key] = value;

    public IReadOnlyDictionary<string, long> Longs => _longs;
    public IReadOnlyDictionary<string, double> Doubles => _doubles;
}
```

**Why a bag instead of typed LHDN fields forever:** first-class `LhdnStuckCount` on the BB snapshot is exactly how product knowledge accretes. A bag lets Lhdn set `"lhdn.stuck_count"` without BB knowing TaxDocuments. Compatibility mapping can still copy known keys into legacy first-class fields / HTTP JSON for one release train.

### 4.2 Aggregator responsibilities (rewritten `PlatformMetricsCollector`)

Pseudocode of target `CollectAsync`:

```
open connection
for each IOutboxSchemaRegistration (order by Schema):
    run shared QueryOutbox / QueryInboxDead
    append SchemaOutboxMetrics
    track max lag, sum pending, sum dead
for each IPlatformMetricsContributor:
    try ContributeAsync(context) catch log + continue (or fail-soft per contributor)
merge bag → snapshot (incl. legacy LhdnStuckCount = bag["lhdn.stuck_count"] if present)
fold LazuarMetrics process totals
PublishSnapshot
return snapshot
```

**CanConnectAsync** stays pure `SELECT 1` — no contributors.

**Failure policy:**

- Connection open failure → `DatabaseReachable = false` (today).
- One contributor throws → **recommended:** log warning, set that contribution missing, still return partial snapshot with `DatabaseReachable = true` if technical scrape succeeded. Avoid one Lhdn query breaking readiness when lag gate is on.
- Alternatively: hard-fail entire collect (current behavior for any exception inside try). Prefer **fail-soft per contributor** after plugins land.

### 4.3 Shared outbox SQL location

Keep `QueryOutboxAsync` / `QueryInboxDeadAsync` as **private methods or internal static helper** on the aggregator (or `OutboxSchemaMetricsQuery` type in BB Infrastructure). Contributors must **not** re-implement outbox SQL unless a module has a nonstandard layout (none today).

Do **not** move outbox SQL into nine modules — that is anti-DRY for a shared table shape owned by BB message entities.

### 4.4 Lhdn contributor (product SQL move)

**Location:** `Modules/Lhdn/Infrastructure/Observability/LhdnStuckMetricsContributor.cs` (path suggestion).

**Responsibility:**

- Read threshold from **module-local options** (see §5).
- Execute current `QueryLhdnStuckAsync` SQL (moved verbatim).
- `context.Bag.SetLong("lhdn.stuck_count", count)`.

**Does not:** touch outbox; register schema (Lhdn already registers `"lhdn"` via outbox registration).

### 4.5 Optional future product contributors (not required for v1)

| Module | Possible contribution | Priority |
|--------|----------------------|----------|
| Commerce | dunning past-due subscription counts (DB gauge) | P2 — today only process counter |
| One | webhook delivery outbox lag on `WebhookDeliveryOutboxes` (separate from integration outbox) | P2 |
| Payments | failed gateway webhook process rate (already counter) | P3 |
| Messaging | delivery log failures | P3 |

v1 success criterion is **LHDN out of BB + schema list from DI**, not a zoo of product gauges.

### 4.6 Gauge registration strategy after plugins

**Option G1 — keep fixed gauges (minimal churn)**  
`LazuarMetricsGauges` continues to expose the four gauges; aggregator maps bag key → `LhdnStuckCount` for publish.  
**Pros:** zero meter name change. **Cons:** still a LHDN-named gauge in BB Infrastructure.

**Option G2 — dynamic gauges (later)**  
Contributors declare gauge descriptors; hard with `System.Diagnostics.Metrics` observable gauges (registration is usually once). Prefer process counters or fixed known gauges for now.

**Recommendation:** **G1 for v1**. Accept LHDN gauge *name* as a frozen public instrument; **implementation** of the value moves to Lhdn. A later cleanup can rename to a generic instrument if product agrees.

### 4.7 Process counters (`LazuarMetrics`) — parallel soft track

Not required to unblock SQL move; schedule as PR-C (see §8).

| Counter | Recommendation |
|---------|----------------|
| Dead letters | Stay BB static API. |
| Webhook failed | Stay BB with `source` tag (multi-module platform delivery). Document as **platform technical** with sources owned by modules. Alternative: each module uses its own Meter — more fragmentation for little gain. |
| Dunning cancels | **Prefer:** Commerce-local `CommerceMetrics.RecordDunningCancel()` using meter name `Lazuar.Commerce` or shared meter with tag `module=commerce`. Snapshot field `dunning_cancels_since_start` either stays (reads Commerce static) or becomes bag key. **Acceptable interim:** leave method on `LazuarMetrics` with `[Obsolete]` until Commerce migrates. |

### 4.8 Interface naming (align with docs)

docs/009 and Phase 15 remarks already use **`IPlatformMetricsContributor`**. Prefer that name for product plugins; use **`IOutboxSchemaRegistration`** (or `IPlatformOutboxSchema`) for the schema list so “contributor” is not overloaded for a one-string registration.

---

## 5. Which SQL / config moves where

### 5.1 Move matrix

| Artifact | From | To | Notes |
|----------|------|-----|-------|
| `QueryLhdnStuckAsync` SQL | BB `PlatformMetricsCollector` | **Lhdn** `LhdnStuckMetricsContributor` | Exact SQL + UndefinedTable handling |
| `ObservabilityOptions.LhdnStuckThreshold` | BB options | **Lhdn** `LhdnObservabilityOptions` (or `LhdnWorkerOptions`) section e.g. `Lhdn:StuckThreshold` | Host appsettings: move/rename key with dual-bind period if needed |
| Hardcoded `ModuleSchemas` array | BB constant | **Each module DI** `AddOutboxSchemaMetrics("…")` | BB may keep a **test-only** default empty list |
| Outbox/inbox query methods | BB collector | Stay BB as shared helper used by aggregator | Not product |
| `SELECT 1` connectivity | BB collector | Stay | |
| Process counter fold-in | BB collector | Stay (or bag merge for product counters later) | |
| Gauge `lazuar.lhdn.stuck_count` registration | BB gauges | Stay registration; value from bag | |
| HTTP `lhdn_stuck_count` | Host endpoint | Stay key for compatibility; source from snapshot field filled from bag | |

### 5.2 Schema registration ownership (per module)

| Module | Schema string | Register in |
|--------|---------------|-------------|
| One | `one` | `AddOneModule` |
| Messaging | `messaging` | `AddMessagingModule` |
| Payments | `payments` | `AddPaymentsModule` |
| CRM | `crm` | `AddCrmModule` |
| Ops | `ops` | `AddOpsModule` |
| Billing | `billing` | `AddBillingModule` |
| Lhdn | `lhdn` | `AddLhdnModule` (+ contributor) |
| Commerce | `commerce` | `AddCommerceModule` |
| Communications | `communications` | `AddCommunicationsModule` |

Place the call **next to** outbox/inbox hosted service registration so “schema has messaging tables” and “schema is scraped” stay co-located.

### 5.3 SQL that must **not** move into BB

Any future product health SQL (stuck submissions, past-due subscriptions, webhook claim leases, etc.) must be a **module contributor**, never new methods on `PlatformMetricsCollector`. Class remarks already say “do not grow more module-specific SQL here” — interface makes that enforceable by review + optional architecture test (grep for `TaxDocuments` under BuildingBlocks).

### 5.4 Cross-schema technical scrape: approved exception?

docs/FW-4 says multi-schema product SQL is a leak. **Technical outbox scrape across schemas** is a **platform observability exception**:

- Same physical table shape as BB `OutboxMessage` / `InboxMessage`.
- Platform-level lag is a **host readiness** concern.
- Alternative (each module exposes lag via Contracts query service) is heavier and still ends up aggregated in the host.

**Record as approved exception in 009** after plugins land:

> Platform metrics aggregator may query `{schema}.OutboxMessages` / `InboxMessages` for schemas registered via `IOutboxSchemaRegistration`. It must not query module business tables.

---

## 6. DI registration design

### 6.1 Multi-implementation pattern

.NET DI natively supports:

```csharp
services.AddSingleton<IOutboxSchemaRegistration>(new OutboxSchemaRegistration("one"));
// ...
services.AddSingleton<IPlatformMetricsContributor, LhdnStuckMetricsContributor>();
```

Aggregator constructor:

```csharp
public PlatformMetricsCollector(
    string connectionString,
    IOptions<ObservabilityOptions> options,
    IEnumerable<IOutboxSchemaRegistration> schemas,
    IEnumerable<IPlatformMetricsContributor> contributors,
    ILogger<PlatformMetricsCollector> logger)
```

If **zero schemas** registered (misconfigured host), collect returns empty schemas / zero lag (or log error once). Prefer failing CI with a host test that asserts nine schemas registered when all modules are loaded.

### 6.2 Lifetime

| Type | Lifetime | Why |
|------|----------|-----|
| `IPlatformMetricsCollector` | Singleton (today) | Used by hosted job + concurrent HTTP; no scoped DbContext |
| `IOutboxSchemaRegistration` | Singleton | Immutable string |
| `IPlatformMetricsContributor` | Singleton | Stateless SQL; inject `IOptions<T>` not scoped services |
| `PlatformMetricsRefreshJob` | Hosted | unchanged |

**Do not** inject scoped `LhdnDbContext` into the contributor — raw Npgsql on the shared open connection matches today’s collector and avoids scope-in-singleton issues. If a contributor needs EF, open a scope via `IServiceScopeFactory` (discouraged for v1).

### 6.3 Host wiring change

Today Program constructs collector manually with connection string. After plugins:

```csharp
// Option A — still manual factory but resolve enumerables
builder.Services.AddSingleton<IPlatformMetricsCollector>(sp =>
    new PlatformMetricsCollector(
        defaultConnectionString,
        sp.GetRequiredService<IOptions<ObservabilityOptions>>(),
        sp.GetServices<IOutboxSchemaRegistration>(),
        sp.GetServices<IPlatformMetricsContributor>(),
        sp.GetRequiredService<ILogger<PlatformMetricsCollector>>()));

// Option B — cleaner: register type + IOptions connection via ISqlConnectionFactory / options
builder.Services.AddSingleton<IPlatformMetricsCollector, PlatformMetricsCollector>();
// requires connection string via options or IConfiguration injection
```

Prefer **Option B** if connection string can be read from `IConfiguration` inside the collector (removes factory lambda). Not mandatory for pluginization.

**Order:** Register aggregator + job **before or after** `AddAllModules` — DI is order-independent for resolution. Schemas must be registered **by the time the first Collect runs** (runtime), which is always after full service provider build.

### 6.4 Module DI snippet (example Lhdn)

```csharp
// AddLhdnModule
services.AddOutboxSchemaMetrics("lhdn");
services.AddSingleton<IPlatformMetricsContributor, LhdnStuckMetricsContributor>();
services.AddOptions<LhdnObservabilityOptions>()
    .BindConfiguration(LhdnObservabilityOptions.SectionName);
```

Other modules: only `AddOutboxSchemaMetrics("…")`.

### 6.5 Architecture boundary compliance

```
BuildingBlocks.Application  → (interfaces IOutboxSchemaRegistration, IPlatformMetricsContributor, maybe bag DTOs)
BuildingBlocks.Infrastructure → aggregator, outbox SQL helper, gauges, job
Modules.*.Infrastructure → implements contributor; references BB; does NOT create reverse BB→Module ref
Lazuar.Api → composition only
```

Modules already reference BuildingBlocks.Infrastructure for DbContext/outbox — no new layering violation.

### 6.6 Testing DI registration

Add a host/architecture-style test (or module test with full DI):

- When `AddAllModules` is invoked, exactly the nine schema names are registered (set equality).
- At least one `IPlatformMetricsContributor` named `lhdn` exists after `AddLhdnModule`.

---

## 7. Snapshot / HTTP / options compatibility plan

### 7.1 Compatibility strategy (recommended)

**v1 (plugin land):**

- Keep `PlatformMetricsSnapshot.LhdnStuckCount` property.
- Aggregator sets it from bag key `lhdn.stuck_count` (0 if contributor absent).
- Keep `/health/metrics` JSON keys unchanged.
- Keep gauge name `lazuar.lhdn.stuck_count`.
- Move config: support **both** `Observability:LhdnStuckThreshold` (obsolete) and `Lhdn:StuckThreshold` for one release; Lhdn contributor reads new first, falls back to old.

**v2 (optional cleanup, separate PR):**

- Add `extras` object on JSON for arbitrary bag keys.
- Deprecate first-class LHDN fields in xmldoc; keep property for binary compat until no readers remain.
- Remove obsolete options binding.

Do **not** break silent dashboards that scrape `lhdn_stuck_count` without a deliberate version bump.

### 7.2 `/health/metrics` response shape (current contract to preserve)

```json
{
  "collected_at_utc": "...",
  "database_reachable": true,
  "error": null,
  "outbox_lag_seconds": 0,
  "outbox_pending_count": 0,
  "dead_letter_count": 0,
  "lhdn_stuck_count": 0,
  "counters": {
    "dead_letters_since_start": 0,
    "webhook_failed_since_start": 0,
    "dunning_cancels_since_start": 0
  },
  "schemas": [
    {
      "schema": "one",
      "outboxPending": 0,
      "outboxDead": 0,
      "inboxDead": 0,
      "outboxLagSeconds": 0
    }
  ]
}
```

Note: ASP.NET default JSON for the endpoint uses anonymous types with snake_case for top-level fields; `schemas` currently serializes `SchemaOutboxMetrics` with **PascalCase property names** unless global JSON policy camelCases (verify before changing). **Do not drive renames in the plugin PR.**

### 7.3 Readiness contract

Unchanged: DB + optional `OutboxLagReadyThreshold`. Plugin fail-soft must not make lag unreadable when Lhdn contributor fails.

---

## 8. PR sequence (recommended)

Aligned with FUTURE-WORK suggested commit  
`refactor(metrics): pluginize PlatformMetricsCollector contributors (FW-3/FW-4)`  
but **split into smaller merges**.

### PR-M1 — Schema registration (no product move yet)

**Scope:**

1. Introduce `IOutboxSchemaRegistration` + `AddOutboxSchemaMetrics`.
2. Change `PlatformMetricsCollector` to inject `IEnumerable<IOutboxSchemaRegistration>` instead of `ModuleSchemas` constant (delete constant or mark obsolete private).
3. Call `AddOutboxSchemaMetrics` from all nine `Add*Module` methods.
4. Keep LHDN SQL **temporarily** in collector (still wrong, but inventory fixed).
5. Unit test: collector with 0 schemas → empty; with 1 fake schema name + missing tables → zeros.
6. Optional: host test that nine schemas register.

**Risk:** Low. Behavior identical if all nine register correctly.  
**Exit:** BB no longer lists module inventory; module onboarding documented.

### PR-M2 — Contributor interface + LHDN move (main FW-3/FW-4 win)

**Scope:**

1. Introduce `IPlatformMetricsContributor` + `PlatformMetricsCollectContext` + contribution bag.
2. Aggregator loops contributors after schema scrape; maps `lhdn.stuck_count` → snapshot field.
3. Implement `LhdnStuckMetricsContributor` in Lhdn Infrastructure; register in `AddLhdnModule`.
4. Delete `QueryLhdnStuckAsync` from BB.
5. Move threshold options to Lhdn (with dual-bind fallback).
6. Update `ObservabilityOptions` xmldoc / remove Lhdn property when dual-bind ends (same PR or PR-M2b).
7. Tests: contributor unit test with mocked connection or Testcontainers if available; assert BB sources contain no `TaxDocuments`.
8. Update docs/009 §3/§4/§7; class remarks on collector; FUTURE-WORK FW-3 item 8 / FW-4 row; F13 checklist.

**Risk:** Medium (options binding, snapshot mapping).  
**Exit:** No product SQL in BB; LHDN owns stuck metric.

### PR-M3 — Fail-soft + empty-registration guardrails

**Scope:**

1. Per-contributor try/catch with structured logs.
2. Startup log of registered schemas + contributor names.
3. Optional health detail: contributors that failed last collect (not required for k8s).

**Can merge with M2** if small.

### PR-M4 — LazuarMetrics product counter hygiene (soft)

**Scope:**

1. Commerce-local dunning counter (or generic `RecordTaggedCounter`).
2. Update `DunningEngineJob.PastDue` call site.
3. Snapshot `DunningCancelsSinceStart` reading strategy (Commerce static total vs bag).
4. Tests update `LazuarMetricsTests`.
5. Webhook failed: document ownership in 009; no move unless product wants separate meters.

**Risk:** Low-medium (process total continuity across process — counters reset on restart anyway).  
**Exit:** BB Application API no longer names dunning.

### PR-M5 — Docs / runbook / inventory only

If not done in M2:

1. docs/007: note schemas also registered for metrics via DI.
2. plans F13 checkboxes.
3. Optional architecture test: BuildingBlocks must not contain string `TaxDocuments` / `ValidationStatus` in Observability folder.
4. Cross-link F07 inventory row for metrics as **fixed**.

### Explicit PR order graph

```
PR-M1 (schema DI)
   └── PR-M2 (IPlatformMetricsContributor + LHDN SQL move)  [depends on M1 or can include M1]
         ├── PR-M3 (fail-soft) [optional merge into M2]
         ├── PR-M4 (dunning counter) [independent of M2 technically; after M2 preferred for narrative]
         └── PR-M5 (docs if not in M2)
```

**Minimum shippable for FW-3/FW-4 metrics:** **M1+M2**.  
**Nice complete:** +M4.  
Do **not** bundle with LLM/email moves or Communications cross-schema receipt fixes.

---

## 9. Testing plan (implementation time)

### 9.1 Unit

| Test | Asserts |
|------|---------|
| Aggregator with empty schemas | Zero lag/pending/dead; schemas empty |
| Aggregator with contributor setting bag key | Snapshot `LhdnStuckCount` mapped |
| Contributor exception | Fail-soft: technical metrics still present |
| Schema identifier validation | Rejects `"lhdn\";drop"` style |
| Lhdn contributor threshold | Uses options; default 1h |
| HealthReadiness | Unchanged (mock collector) |
| LazuarMetrics dead letter | Unchanged |

### 9.2 Integration (if Testcontainers / existing DB fixture)

| Test | Asserts |
|------|---------|
| Real empty DB | Missing tables → zeros not 500 |
| Seeded outbox row unprocessed | lag/pending > 0 for that schema |
| Seeded stuck TaxDocument | Lhdn contributor count ≥ 1; BB binary has no TaxDocuments SQL |

### 9.3 Contract / smoke

| Test | Asserts |
|------|---------|
| `GET /health/metrics` shape | Required keys present after pluginization |
| Nine schemas in `schemas` array | Set equality with module list when all modules loaded |

### 9.4 Architecture

| Test | Asserts |
|------|---------|
| Existing ModuleBoundaryTests | Still green |
| New: no `TaxDocuments` under `BuildingBlocks/**/Observability/**` | Optional string/source scan |

---

## 10. Risks and mitigations

| Risk | Mitigation |
|------|------------|
| Forgot to register a schema after M1 | Host test set-equality of nine schemas; startup log; metrics `schemas` empty for missing module is visible |
| Double-registration of same schema | Deduplicate by schema name in aggregator (OrdinalIgnoreCase or Ordinal — PG schemas are case-sensitive quoted; our names are lowercase) |
| Lhdn contributor fails and breaks readiness | Fail-soft contributors; readiness only needs lag aggregate |
| Options key move breaks deployed config | Dual-bind period; document in appsettings.example / deploy env docs |
| JSON shape change accidental | Explicit anonymous type in endpoint stays; snapshot fields preserved |
| Contributors open extra connections / pool pressure | Pass shared open connection in context; sequential contribute |
| Parallel collect from job + HTTP | Same as today; Npgsql connection per collect call (no shared connection across calls) |
| Future module authors add SQL back into BB | 009 rule + PR checklist + optional architecture string ban |
| Gauge name still says lhdn in BB | Accept for v1; value ownership is the real leak |

---

## 11. Relation to other workstreams

| Stream | Relation |
|--------|----------|
| **F07 cross-schema inventory** | Metrics collector is one inventory row; can be closed by M2 without waiting for Communications leaks |
| **F08 fix leaks** | F08.1 metrics path == this doc’s M1–M2 |
| **F13 checklist** | Implementation checklist; this analysis is the design input |
| **FW-3 order** | FUTURE-WORK lists metrics as item 8 after LLM/email; **metrics can ship earlier** — lower risk than LLM move, high boundary value |
| **FW-4** | Closing LHDN product SQL is a FW-4 win; schema scrape becomes documented exception |
| **Phase 15** | Already shipped ownership map + comments; this is the deferred code move |
| **Production OTel** | Orthogonal; plugins make multi-module gauges easier later |

---

## 12. Definition of done (metrics plugins)

- [ ] No hardcoded `ModuleSchemas` array in BB; nine modules register via DI.
- [ ] No `lhdn."TaxDocuments"` (or other product table) SQL under BuildingBlocks.
- [ ] `IPlatformMetricsContributor` exists; Lhdn implements stuck-document contribution.
- [ ] Aggregator only: connectivity, registered outbox/inbox scrape, contributor loop, process counters, publish gauges.
- [ ] `/health`, `/health/ready`, `/health/metrics` behavior preserved (including `lhdn_stuck_count` value still meaningful when Lhdn module present).
- [ ] Architecture tests green; optional TaxDocuments ban under BB Observability.
- [ ] docs/009 updated (pluginized aggregator = stay BB; LHDN SQL = moved).
- [ ] FUTURE-WORK / F13 notes residual (e.g. dunning counter if PR-M4 deferred).
- [ ] Class remarks on collector updated from “future direction” to “pluginized; do not add product SQL.”

**Dunning/webhook counter hygiene** can remain residual if M4 deferred — call out explicitly so “FW-3 metrics” is not silently half-done.

---

## 13. Concrete file touch list (implementation map — not done in this analysis)

### BuildingBlocks (new / edit)

| Path | Change |
|------|--------|
| `.../Application/Observability/IOutboxSchemaRegistration.cs` | **New** |
| `.../Application/Observability/IPlatformMetricsContributor.cs` | **New** (+ context/bag types or sibling files) |
| `.../Infrastructure/Observability/PlatformMetricsCollector.cs` | **Edit** — inject registrations + contributors; delete LHDN SQL + ModuleSchemas |
| `.../Infrastructure/Observability/OutboxSchemaMetricsQuery.cs` | **Optional extract** of outbox/inbox SQL |
| `.../Infrastructure/Observability/ObservabilityServiceCollectionExtensions.cs` | **New** — `AddOutboxSchemaMetrics` |
| `.../Infrastructure/Observability/ObservabilityOptions.cs` | **Edit** — remove/obsolete `LhdnStuckThreshold` after move |
| `.../Infrastructure/Observability/PlatformMetricsSnapshot.cs` | **Edit** — xmldoc; optional Extras bag |
| `.../Infrastructure/Observability/LazuarMetricsGauges.cs` | Unchanged for v1 (or xmldoc only) |
| `.../Application/Observability/LazuarMetrics.cs` | **Edit in M4 only** |

### Modules

| Path | Change |
|------|--------|
| Each `Modules/*/Infrastructure/DependencyInjection.cs` | `AddOutboxSchemaMetrics("<schema>")` |
| `Modules/Lhdn/Infrastructure/Observability/LhdnStuckMetricsContributor.cs` | **New** |
| `Modules/Lhdn/Infrastructure/DependencyInjection.cs` | Register contributor + options |
| `Modules/Lhdn/.../LhdnObservabilityOptions.cs` | **New** threshold options |
| `Modules/Commerce/...` | M4 dunning metrics only |

### Host

| Path | Change |
|------|--------|
| `src/Lazuar.Api/Program.cs` | Collector DI for new ctor deps |
| `src/Lazuar.Api/appsettings.json` | Threshold key move / dual bind |
| `src/Lazuar.Api/Composition/HealthEndpointExtensions.cs` | Prefer **no change** in v1 |

### Docs / plans

| Path | Change |
|------|--------|
| `apps/lazuar-api/docs/009-building-blocks-ownership.md` | Plugin done status |
| `apps/lazuar-api/docs/007-outbox-inbox-dead-letter-runbook.md` | Optional metrics registration note |
| `plans/004-maintenance/FUTURE-WORK.md` | FW-3/4 progress |
| `plans/004-maintenance/checklists-future/phase-f13-bb-metrics-plugins.md` | Checkboxes |
| This file | Link from F13 / FUTURE-WORK as design SSoT |

### Tests

| Path | Change |
|------|--------|
| `tests/.../Observability/*` | Aggregator + contributor tests |
| Optional architecture string ban | `Lazuar.ArchitectureTests` |

---

## 14. Worked example: collect path after M2

1. Ops engineer hits `GET /health/metrics`.
2. Host resolves singleton `IPlatformMetricsCollector`.
3. Collector opens Default Npgsql connection.
4. Enumerates `IOutboxSchemaRegistration` → nine schemas registered by modules → nine outbox + nine inbox queries (shared SQL helper).
5. Enumerates `IPlatformMetricsContributor` → Lhdn stuck contributor runs TaxDocuments SQL inside **Lhdn** assembly → sets bag `lhdn.stuck_count`.
6. Snapshot: aggregate lag/pending/dead; `LhdnStuckCount` from bag; process counters from `LazuarMetrics`; `Schemas` populated.
7. `LazuarMetricsGauges.PublishSnapshot`.
8. Endpoint returns same JSON shape as before.

Adding module **Foo**:

1. Create schema + outbox tables.
2. `AddFooModule` → `AddOutboxSchemaMetrics("foo")` + outbox jobs.
3. **No** BB edit.
4. Optional product health → `IPlatformMetricsContributor` in Foo.

---

## 15. Decision log (recommendations locked by this analysis)

| # | Decision | Choice |
|---|----------|--------|
| D1 | Keep aggregator in BB? | **Yes** — platform observability spine |
| D2 | Outbox multi-schema SQL home? | **BB helper** + DI schema registration (not 9 copies) |
| D3 | LHDN stuck SQL home? | **Lhdn contributor only** |
| D4 | Product contrib API? | `IPlatformMetricsContributor` + bag (docs-aligned name) |
| D5 | Schema API? | Separate `IOutboxSchemaRegistration` |
| D6 | Preserve HTTP `lhdn_stuck_count`? | **Yes** for v1 via snapshot mapping |
| D7 | Preserve gauge name `lazuar.lhdn.stuck_count`? | **Yes** for v1 |
| D8 | Dunning counter in same PR as SQL? | **No** — PR-M4 optional residual |
| D9 | Webhook failed counter? | Stay BB tagged; document multi-owner sources |
| D10 | Connection model? | Shared open connection in contribute context |
| D11 | Contributor failure? | Fail-soft after M2/M3 |
| D12 | BB → Modules references? | Still forbidden; plugins invert dependency |
| D13 | Ship before LLM move? | **Yes** — lower risk, high boundary value |

---

## 16. Evidence index (absolute paths)

### Implementation (read for this analysis)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/Observability/PlatformMetricsCollector.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/Observability/IPlatformMetricsCollector.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/Observability/PlatformMetricsSnapshot.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/Observability/LazuarMetricsGauges.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/Observability/PlatformMetricsRefreshJob.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/Observability/HealthReadiness.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/Observability/ObservabilityOptions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Application/Observability/LazuarMetrics.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/MessageProcessingResultApplier.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Program.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/HealthEndpointExtensions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/appsettings.json` (`Observability` section)
- Module DI: `Modules/{One,Messaging,Payments,CRM,Ops,Billing,Lhdn,Commerce,Communications}/Infrastructure/DependencyInjection.cs`
- Call sites: Lhdn `WebhookSenderService.cs`, One `OutboundWebhookDispatcherJob.cs`, Payments `ProcessGatewayWebhookCommandHandler.cs`, Commerce `DunningEngineJob.PastDue.cs`
- Lhdn domain statuses: `Modules/Lhdn/Domain/Aggregates/TaxDocument.cs`
- Tests: `tests/Lazuar.ModuleTests/Observability/{HealthReadinessTests,LazuarMetricsTests}.cs`
- Architecture: `tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs`

### Policy / prior plans

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/docs/009-building-blocks-ownership.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/docs/007-outbox-inbox-dead-letter-runbook.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/FUTURE-WORK.md` (FW-3, FW-4)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/06-building-blocks-shared-kernel.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/phase-15-analysis.md` / `phase-15-done.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/checklists-future/phase-f13-bb-metrics-plugins.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/checklists-future/phase-f07-cross-schema-inventory.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/checklists-future/phase-f08-cross-schema-fix-leaks.md`

---

## 17. One-paragraph brief for implementers

Pluginize by introducing DI-registered outbox schema names and `IPlatformMetricsContributor` plugins: keep a thin BB aggregator that scrapes only registered `{schema}.OutboxMessages`/`InboxMessages` and merges contributor bags; move `lhdn.TaxDocuments` stuck SQL and its threshold into an Lhdn contributor; leave `/health*` contracts and `lhdn_stuck_count` wired through the snapshot for compatibility; register schemas from each `Add*Module`; ship schema registration first (M1), LHDN move second (M2), optional dunning counter ownership third (M4); never reintroduce product table SQL into BuildingBlocks.
