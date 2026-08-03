<!-- Source subagent: 019fc650-3514-7d71-af79-066259e7c5e0 -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# Background Workers Gap Analysis

**Scope:** `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api`  
**Stack:** ASP.NET Core `BackgroundService` / `IHostedService` only — **no Hangfire, no Quartz, no Cap, no MassTransit**.  
**Deploy posture (prod):** single API container (`hub-api` in `deploy/prod/docker-compose.yml`), process-local workers co-located with HTTP.

---

## Worker Inventory (all jobs)

### Building-block bases

| Type | Path | Role |
|------|------|------|
| `OutboxPublisherJob<TDbContext>` | `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxPublisherJob.cs` | Poll outbox, publish to `InMemoryEventBus`, mark processed |
| `InboxConsumerJob<TDbContext>` | `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/BuildingBlocks/Infrastructure/InboxConsumerJob.cs` | Poll inbox, MediatR-publish as `INotification` |
| `DatabaseJobTrigger` | `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/BuildingBlocks/Infrastructure/DatabaseJobTrigger.cs` | In-process wake signal after `SaveChanges` |

### Module outbox/inbox hosted services (thin subclasses)

| Job | Module | Registered? |
|-----|--------|-------------|
| `CommerceOutboxPublisherJob` / `CommerceInboxConsumerJob` | Commerce | Yes |
| `BillingOutboxPublisherJob` / `BillingInboxConsumerJob` | Billing | Yes |
| `PaymentsOutboxPublisherJob` / `PaymentsInboxConsumerJob` | Payments | Yes |
| `OneOutboxPublisherJob` / `OneInboxConsumerJob` | One | Yes |
| `CommunicationsOutboxPublisherJob` / `CommunicationsInboxConsumerJob` | Communications | Yes |
| `MessagingOutboxPublisherJob` / `MessagingInboxConsumerJob` | Messaging | Yes |
| `OpsOutboxPublisherJob` / `OpsInboxConsumerJob` | Ops | Yes |
| **Lhdn outbox/inbox jobs** | Lhdn | **No** (tables + `OutboxEventBus` exist) |
| **CRM outbox/inbox jobs** | CRM | **No** (tables + `OutboxEventBus` exist) |

### Domain / operational workers

| Job | Module | Pattern | Interval / trigger |
|-----|--------|---------|-------------------|
| `BillingEngineJob` | Commerce | `BackgroundService` loop | Every **1 hour** |
| `DunningEngineJob` | Commerce | `BackgroundService` loop | Every **1 hour** |
| `RevenueRecognitionJob` | Billing | `BackgroundService` loop | Every **1 hour** |
| `B2cConsolidationJob` | Billing | `BackgroundService` + calendar schedule | **28th of month 02:00 MYT** |
| `BroadcastFanoutJob` | Communications | `BackgroundService` loop | Every **10s** |
| `OutboundWebhookDispatcherJob` | One | `BackgroundService` loop | Every **10s** |
| `LhdnSubmissionJob` | Lhdn | `BackgroundService` loop | Every **5s** |
| `LhdnStatusPollingJob` | Lhdn | `BackgroundService` loop | Every **10s** |
| `SystemGenesisBootstrapperJob` | One | `IHostedService` once at startup | Once |
| `LhdnReferenceDataSeederJob` | Lhdn | `IHostedService` once at startup | Once |

**Total concrete job types found:** 26 class declarations (2 bases + 14 thin outbox/inbox + 10 domain/bootstrap).  
**No `*Worker*.cs` files outside `Infrastructure/Workers` / Messaging roots.**  
**CRM:** zero hosted services. **Lhdn:** domain jobs only (no bus jobs).

---

## Scheduling Mechanism

### What is used

1. **In-process `BackgroundService` loops** with fixed `Task.Delay` (5s–1h).
2. **`DatabaseJobTrigger` (singleton)**  
   - Registered in `Program.cs`.  
   - Fired from `PlatformDbContext.SaveChangesAsync` when `result > 0`.  
   - Outbox/inbox jobs wait up to **5s** on the trigger, then poll.  
   - **One global TCS for all modules** — any successful save wakes every outbox/inbox job process-wide.
3. **Calendar-style delay** only in `B2cConsolidationJob` (MYT month-end style schedule).
4. **Startup seeders** via `IHostedService.StartAsync`.

### What is not used

- Hangfire / Quartz / cron expressions / distributed schedulers  
- Leader election / job leases  
- Separate worker process or queue broker  
- Configurable intervals via options/env  
- Catch-up / missed-run compensation (except B2C’s “already ran today” guard)

### Registration map (from module `DependencyInjection.cs`)

| Module | Hosted services |
|--------|-----------------|
| Commerce | Inbox, Outbox, BillingEngine, DunningEngine |
| Billing | Inbox, Outbox, RevenueRecognition, B2cConsolidation |
| Payments | Inbox, Outbox |
| One | Genesis bootstrap, Inbox, Outbox, OutboundWebhookDispatcher |
| Communications | Inbox, Outbox, BroadcastFanout |
| Messaging | Outbox, Inbox |
| Ops | Inbox, Outbox |
| Lhdn | Submission, StatusPolling, ReferenceDataSeeder |
| CRM | **(none)** |

---

## DunningEngineJob Deep Dive

**File:** `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs`  
**Registered:** `Commerce` DI as `AddHostedService<DunningEngineJob>()`.

### Loop & scope

- Starts log → infinite loop → `ProcessDunningAsync` → **always** `Task.Delay(1 hour)`.
- Errors in processing are logged; loop continues.
- Uses `IgnoreQueryFilters()` — intentional multi-tenant platform job.
- Single scope + single `SaveChanges` at end if any mutation (`requiresSave`).

### Phase A — Pre-dunning (ACTIVE, due in ≤14 days)

1. Load all **active** campaigns ordered by `PriorityOrder` desc, then `CreatedAt` desc.
2. Load ACTIVE subs with `NextBillingDate` in `(now, now+14d]`.
3. Infer payment method: empty vault token → `MANUAL`, else `ONLINE_GATEWAY`.
4. Pick first matching campaign (org + optional product IDs + payment methods).
5. Match step where `DayOffset < 0` and `Abs(DayOffset) == daysUntilDue` and action is EMAIL/WHATSAPP/ALL.
6. Dedupe via `ReminderLogs` (unique index on subscription/step/target date).
7. Dispatch `FulfillmentRequestedIntegrationEvent` → COMMUNICATIONS / `reminder.dunning`, then `RecordReminderDispatched`.

### Phase B — Past-due dunning (`Status == PAST_DUE`)

1. Respect `DunningPausedUntil`.
2. Assign `CurrentDunningCampaignId` if null (same matching rules).
3. If `daysOverdue >= GracePeriodDays`:
   - `CANCEL` → `sub.Cancel()` + `campaign.RecordChurn()` + fulfillment/webhook events  
   - `SUSPEND` → `sub.Suspend()` + events  
   - Always `continue` (including `FinalAction == NONE` — no further steps after grace).
4. Else match step with `DayOffset == daysOverdue`:
   - **AUTOCHARGE / AUTO_CHARGE:** count `ChargeAttemptLogs` for target date; if `< 4` and vault present, insert log + publish `ExecuteOffSessionChargeIntegrationEvent` (with campaign id + gateway).
   - Else communication step via same fulfillment event.
   - Always `RecordReminderDispatched` for the step.

### Correctness / reliability issues (high severity)

| Issue | Detail |
|-------|--------|
| **No multi-instance safety** | Full table load of campaigns/subs; no `FOR UPDATE SKIP LOCKED`, no row lease. Two API replicas would double-dispatch (reminder unique index helps comms; autocharge is broken — see below). |
| **AUTOCHARGE vs unique `ChargeAttemptLogs`** | Index is **unique** `(SubscriptionId, TargetBillingDate)`. Dunning tries up to **4** attempts by inserting more logs on the **same** target date → **2nd attempt fails uniqueness** (or never counts >1). Intent (`attemptCount < 4`) **contradicts schema**. |
| **Billing ↔ dunning state machine gap** | `BillingEngineJob` auto-debits **without** moving ACTIVE→PAST_DUE. Dunning past-due path only loads `PAST_DUE`. Failed off-session charges leave ACTIVE + stale `NextBillingDate` → **neither engine re-charges effectively, and dunning won’t run**. |
| **No transaction around event + state** | Outbox rows are co-saved with domain changes (good *if* `SaveChanges` succeeds once). Mid-loop exceptions lose partial work; no per-subscription transaction. |
| **Events before save** | `PublishAsync` only stages outbox; actual bus fire waits for Commerce outbox job after commit — OK. Crash after save is fine; crash before save loses in-memory work. |
| **Dead domain fields** | `CurrentDunningStepIndex` / `AdvanceDunningStep()` never used by the job; step selection is pure calendar `DayOffset` match. Missed day (job down >24h) **skips** that step forever. |
| **Campaign selection non-determinism** | `FirstOrDefault` after priority order — ties broken only by `CreatedAt`; no explicit “default campaign” semantics documented. |
| **Hourly cadence** | DayOffset steps can miss if job runs just after midnight UTC vs local billing date math (`Date` on UTC). |
| **Load pattern** | Loads **all** active campaigns and **all** candidate subscriptions into memory every hour — no paging. |
| **FinalAction NONE** | Grace elapsed → `continue` with no action and no permanent “exhausted” marker (re-enters every hour until campaign change). |
| **Fulfillment payload** | Pre-dunning only EMAIL/WHATSAPP/ALL; AUTOCHARGE handled separately; mixed “ALL” does not also charge. |
| **Cancel/Suspend idempotency** | After cancel, status changes so re-entry stops; good. Partial failure after cancel but before save could re-fire events next hour. |

### Dependencies

- `CommerceDbContext`, keyed `IEventBus` `"CommerceEventBus"` (`OutboxEventBus`).
- Reminder uniqueness enforced in EF/migration.
- Charge path → Payments `ExecuteOffSessionChargeIntegrationEventHandler` (no retry if gateway returns false — log only).

---

## Outbox/Inbox Jobs

### Intended architecture (docs)

`/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/docs/001-cross-module-communication.md` describes:

**Module A Outbox → OutboxPublisher → InMemoryBus → Module B Inbox → InboxConsumer → local handlers.**

### Actual architecture

```
[Command] → OutboxEventBus.Add(OutboxMessage) → SaveChanges
     → DatabaseJobTrigger
     → OutboxPublisherJob: FOR UPDATE SKIP LOCKED LIMIT 20
     → InMemoryEventBus.PublishAsync
     → IIntegrationEventHandler.HandleAsync  (usually direct side effects)
```

**Inbox is almost unused.** Grep shows **only Messaging** writes `new InboxMessage(...)` (tenant provision/update/workspace). All other modules’ `InboxConsumerJob`s poll empty tables every 5s (or on trigger).

Handlers such as Commerce/Billing/Lhdn/Payments run **synchronously inside the outbox publisher’s process** via `InMemoryEventBus`, not via durable inbox.

### OutboxPublisherJob behavior

- Batch 20, `ProcessedAt IS NULL AND OccurredOn <= NOW()`, ordered by `OccurredOn`.
- `FOR UPDATE SKIP LOCKED` inside a transaction → **safe for multi-instance outbox draining**.
- Per message: resolve type → deserialize → `InMemoryEventBus.PublishAsync`.
- On failure: set `Error = ex.ToString()`, log, **still set `ProcessedAt`** (“poison message” fix — **no retry**).
- Continuous drain when work remains (`Task.Yield` + continue); else wait trigger/5s.

### InboxConsumerJob behavior

- Same SKIP LOCKED / batch 20 / always mark processed / same poison policy.
- Deserializes and `mediator.Publish` only if type is `INotification` (`IIntegrationEvent` extends `INotification` — OK).

### Message models

```csharp
// OutboxMessage / InboxMessage: Id, Type, Data, OccurredOn|ReceivedAt, ProcessedAt?, Error?
// No AttemptCount, NextAttemptAt, DeadLetter status, correlation, or partition key
```

### Critical module gaps

| Module | Outbox table | Outbox job | Inbox table | Inbox job | Impact |
|--------|--------------|------------|-------------|-----------|--------|
| **Lhdn** | Yes | **Missing** | Yes | **Missing** | `LhdnDocumentSubmitted/Validated/...` staged via `LhdnEventBus` **never leave outbox**. Billing credit deduction & status handlers **never fire** from Lhdn publishes. |
| **CRM** | Yes | **Missing** | Yes | **Missing** | `ClientProfileAnonymizedIntegrationEvent` from anonymize handler **stuck forever**. Incoming CRM subscriptions only work if some *other* module’s outbox publisher fires `InMemoryEventBus` (CRM does not need inbox for that). |

### InMemoryEventBus caveats

- Process-local; fine for single modular monolith, not multi-node “bus”.
- Handler exceptions surface to outbox job → message marked processed with Error → **lost**.
- Multiple handlers for one event (e.g. Billing double-subscribe on `GatewayPaymentCompleted`) run sequentially in one scope; failure mid-list can leave partial handler success + poisoned outbox row.
- Uses runtime type name for dispatch — OK given concrete types.

### Dual-write / ordering

- Outbox write is transactional with domain `SaveChanges` (good).
- Outbox **publish** runs handlers **before** outbox commit completes (inside open transaction while rows locked). Handler side effects in other schemas are **not** in the same DB transaction → classic at-least-once / partial-failure surface.
- No global ordering across modules; per-schema outbox order only.

---

## Failure Handling, Retries, Poison Messages

### Outbox / Inbox

| Concern | Behavior |
|---------|----------|
| Handler failure | Logged + `Error` set + **`ProcessedAt` set** → **no automatic retry** |
| Poison / infinite loop | Intentionally avoided by always marking processed |
| Dead-letter queue | **None** (only `Error` column) |
| Retry with backoff | **None** |
| Max attempts | **N/A (single attempt)** |
| Replay tooling | **None** |
| Idempotency of consumers | Ad hoc per handler; not enforced by bus |

**Net:** failure mode is **at-most-once on error**, **at-least-once on crash after handler success / before outbox commit**.

### Domain workers with retry-ish behavior

| Worker | Retry model |
|--------|-------------|
| `OutboundWebhookDispatcherJob` | Best: `AttemptCount`, exponential backoff `2^attempt` minutes, max **5** then `FAILED` |
| `LhdnStatusPollingJob` | `ScheduleNextPoll` exponential `3^min(attempts,10)` seconds or gateway `Retry-After` |
| `LhdnSubmissionJob` | Rate-limit delay via `DelayPendingSubmission`; other errors → **immediate FAILED** (no retry) |
| `BillingEngineJob` | Relies on unique ChargeAttemptLog; **no** failure-driven retry/past-due transition |
| `DunningEngineJob` | Intended multi autocharge broken by unique index; missed DayOffset steps not retried |
| `BroadcastFanoutJob` | Whole broadcast → FAILED on exception; no resume from mid-page |
| `RevenueRecognitionJob` / `B2cConsolidationJob` | Outer catch + delay; business idempotency partial only |
| Seeders | One-shot; log and continue |

### Notable poison / stuck states

1. Failed outbox rows forever with `ProcessedAt` + `Error` — silent business loss.  
2. Lhdn / CRM outbox rows never drained.  
3. Webhooks after 5 failures stay FAILED with no admin redrive path in workers.  
4. Lhdn submission hard-fails on transient exceptions (`MarkAsFailed` in catch).  
5. Broadcast mid-fanout crash marks FAILED; already-published `DispatchMessage` events may have been outboxed — partial send.

---

## Observability (logging, metrics, alerting)

### Present

- `ILogger` Information/Warning/Error in most workers (start messages, some success counts, errors).
- API `/health` liveness only (`Program.cs`) — **does not** probe job heartbeats, outbox lag, or stuck PENDING docs.
- Docker healthcheck = HTTP `/health` only.

### Absent

- Metrics (counters/histograms) for processed/failed messages, lag (`NOW() - OccurredOn`), batch size, job duration  
- OpenTelemetry traces spanning outbox → handler  
- Structured correlation IDs on outbox messages  
- Alerts on: outbox `Error IS NOT NULL`, age of unprocessed outbox, webhook FAILED, LHDN PENDING/SUBMITTED stuck, dunning cancel spikes  
- Dashboard / admin APIs for poison message redrive  
- Worker-specific health checks (`IHealthCheck` for last successful run)

**Bottom line:** failures are log-only; ops cannot see lag or poison piles without raw SQL.

---

## Concurrency & Multi-instance Safety

### Safe (with SKIP LOCKED)

- All registered **OutboxPublisher** / **InboxConsumer** jobs (Postgres row locks).

### Unsafe under ≥2 API instances

| Worker | Risk |
|--------|------|
| `BillingEngineJob` | Duplicate off-session charge events (ChargeAttempt unique helps *after* first insert race; race window remains between check and insert) |
| `DunningEngineJob` | Duplicate cancel/suspend/events; reminder unique index mitigates double email |
| `RevenueRecognitionJob` | Double ledger recognition |
| `B2cConsolidationJob` | Double consolidation events (day-level guard races) |
| `BroadcastFanoutJob` | Two workers process same `QUEUED` broadcast (status flip not locked) |
| `OutboundWebhookDispatcherJob` | Duplicate HTTP deliveries (no SKIP LOCKED on outbox rows) |
| `LhdnSubmissionJob` | Double submit to MyInvois |
| `LhdnStatusPollingJob` | Duplicate polls (mostly harmless) + duplicate side-effect events |
| Seeders | Mostly OK (existence checks / ON CONFLICT) |

### Other concurrency notes

- **Single prod replica today** masks these bugs; horizontal scale of `api` is unsafe without redesign.  
- `DatabaseJobTrigger` is **in-memory singleton** — does not cross instances (instances rely on 5s poll).  
- Outbox publisher holds locks while invoking **all** handlers for a batch — long handlers block other publishers on those rows and extend transaction time.  
- Broadcast pages recipients and saves progressively without claiming lease.

### Deploy reality

`deploy/prod/docker-compose.yml`: one `api` container, `mem_limit: 1024m`, restart unless-stopped — **workers share process and memory with HTTP**.

---

## Gaps & Recommendations

### P0 — Correctness / silent data loss

1. **Register Lhdn outbox (and inbox if desired) jobs**  
   Without this, credit deduction and LHDN→Billing events from Lhdn publishes never run.
2. **Register CRM outbox (and inbox) jobs**  
   Anonymization and any CRM-originated events never leave the CRM outbox.
3. **Outbox failure policy: stop “always mark processed” without retry**  
   Prefer: attempt counter + `NextAttemptAt` + max N → `DEAD` status; only then stop retrying. Keep poison protection *without* single-shot drop.
4. **Billing auto-charge failure path**  
   On failed off-session charge (or after N attempts), transition to `PAST_DUE` so dunning can own recovery; clear/reconcile ChargeAttempt semantics.
5. **Fix AUTOCHARGE attempt model**  
   Either drop uniqueness of `(SubscriptionId, TargetBillingDate)`, or store `AttemptNumber`, or use a separate dunning-attempt table; align code (`< 4`) with schema.

### P1 — Reliability under load / multi-instance

6. **Claim pattern for all domain polls**  
   `FOR UPDATE SKIP LOCKED` (or `UPDATE … RETURNING` status=`PROCESSING` with lease expiry) for: webhooks, broadcasts, LHDN docs, billing/dunning candidate batches.  
7. **Do not scale API replicas until (6)** or extract workers to a single “worker” deployment with replica=1.  
8. **Per-entity transactions** in dunning/billing (process N with SKIP LOCKED, commit per sub) to avoid long unit-of-work and partial batch loss.  
9. **Missed dunning steps:** use “last completed DayOffset” / step index rather than exact calendar match so downtime doesn’t skip actions.

### P2 — Architecture integrity

10. **Either implement real inbox for all modules or document the hybrid model**  
    Today docs promise inbox-backed isolation; code mostly runs handlers inside outbox publisher — weaker isolation, harder retry per consumer.  
11. **Make inbox writes the standard `IIntegrationEventHandler` pattern** (Messaging already does) *or* remove dead InboxConsumer jobs to cut load.  
12. **Transactional outbox publish separation:** dequeue → bus → mark processed in a way that handler failure doesn’t commit “done” without retry metadata.

### P3 — Operability

13. Metrics: `outbox_unprocessed`, `outbox_errors`, `job_last_success_unix`, `webhook_failed`, `lhdn_stuck`.  
14. Health: degraded if outbox lag > threshold or worker heartbeat stale.  
15. Admin redrive: reset `ProcessedAt`/`Status` for poison messages and failed webhooks.  
16. Configurable intervals via `IOptions` (billing/dunning hourly is coarse for SaaS).  
17. Tests: **zero worker reliability tests** (only `DatabaseJobTrigger` construction in integration tests). Add tests for SKIP LOCKED, poison, dunning day match, charge attempt uniqueness.

### P4 — Smaller issues

18. `B2cConsolidationJob` schedule: fixed 28th 02:00 MYT with `AddHours(8)` — brittle around DST-less MYT but OK; no catch-up if instance down on the 28th beyond “next month” delay calc (if down past target, waits until *next* month’s 28th — **missed month** risk when process starts after target).  
19. `RevenueRecognitionJob` loads all open schedules; add filter `Status IN (...)` + paging + idempotent ledger external ref uniqueness.  
20. `LhdnReferenceDataSeederJob` path `../../../../lhdn_docs/codes` fragile in container; seed may silently skip.  
21. `SystemGenesisBootstrapperJob` rotates superadmin password from env every boot if hash mismatch — intentional but operationally sharp.  
22. No Hangfire dashboard; if you adopt a scheduler later, prefer one library end-to-end rather than mixing more `BackgroundService` loops.

---

## File-by-File Notes

### BuildingBlocks

| File | Notes |
|------|------|
| `.../BuildingBlocks/Infrastructure/OutboxPublisherJob.cs` | Core drain loop; SKIP LOCKED; 5s poll + trigger; **always marks processed**; batch 20; publishes via `InMemoryEventBus` inside open transaction. |
| `.../BuildingBlocks/Infrastructure/InboxConsumerJob.cs` | Mirror of outbox; MediatR publish; same poison policy; **largely idle** outside Messaging. |
| `.../BuildingBlocks/Infrastructure/OutboxMessage.cs` | Minimal schema — no attempts/backoff. |
| `.../BuildingBlocks/Infrastructure/InboxMessage.cs` | Same. |
| `.../BuildingBlocks/Infrastructure/OutboxEventBus.cs` | Inserts outbox row only; does not dispatch. Uses event `Id` as message id. |
| `.../BuildingBlocks/Infrastructure/InMemoryEventBus.cs` | Sync multi-handler dispatch; process-local; exceptions bubble. |
| `.../BuildingBlocks/Infrastructure/DatabaseJobTrigger.cs` | Singleton TCS swap; cross-module wake; not multi-host. |
| `.../BuildingBlocks/Infrastructure/PlatformDbContext.cs` | Domain events then save then `JobTrigger.Trigger()`; foundation for low-latency outbox. |
| `.../BuildingBlocks/Infrastructure/TypeResolver.cs` | Cached type resolve for AQNs; failure → poison path. |

### Commerce

| File | Notes |
|------|------|
| `.../Commerce/Infrastructure/Workers/BillingEngineJob.cs` | Hourly; due subs; autocharge once via unique ChargeAttempt; manual → PAST_DUE + fulfillment; **no SKIP LOCKED**; **no charge-failure → PAST_DUE**. |
| `.../Commerce/Infrastructure/Workers/DunningEngineJob.cs` | Hourly pre + past-due engine; see deep dive; **unique ChargeAttempt breaks multi autocharge**; DayOffset exact match; no row claims. |
| `.../Commerce/Infrastructure/Workers/CommerceOutboxPublisherJob.cs` | Thin base wrapper. |
| `.../Commerce/Infrastructure/Workers/CommerceInboxConsumerJob.cs` | Thin; no inbox writers in Commerce. |
| `.../Commerce/Infrastructure/DependencyInjection.cs` | Registers all four hosted services. |
| `.../Commerce/Domain/Aggregates/Subscription.cs` | Dunning fields; `AdvanceDunningStep` unused by job. |
| `.../Commerce/Domain/Entities/ChargeAttemptLog.cs` | One row shape; unique index in DbContext. |
| `.../Commerce/Infrastructure/CommerceDbContext.cs` | Unique ChargeAttempt + ReminderDispatchLog indexes. |

### Billing

| File | Notes |
|------|------|
| `.../Billing/Infrastructure/Workers/RevenueRecognitionJob.cs` | Hourly full scan; ledger lines; concurrent double-post risk; no metrics. |
| `.../Billing/Infrastructure/Workers/B2cConsolidationJob.cs` | Monthly calendar; MYT hardcode; day-level idempotency; missed-run if late start after target. |
| `.../Billing/Infrastructure/Workers/BillingOutboxPublisherJob.cs` / `BillingInboxConsumerJob.cs` | Thin wrappers. |
| `.../Billing/Infrastructure/DependencyInjection.cs` | Subscribes heavily to Lhdn events — **depends on Lhdn outbox job that doesn’t exist**. |
| `.../Billing/Domain/Aggregates/DeferredRevenueSchedule.cs` | Straight-line recognize; status COMPLETED/RECOGNIZING. |

### One

| File | Notes |
|------|------|
| `.../One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs` | 10s poll; Take 50; HMAC; **no SKIP LOCKED**; good attempt/backoff via domain. |
| `.../One/Domain/WebhookDeliveryOutbox.cs` | Max 5 attempts; `2^AttemptCount` minutes. |
| `.../One/Infrastructure/Workers/SystemGenesisBootstrapperJob.cs` | Startup org + superadmin upsert/password rotate. |
| `.../One/Infrastructure/Workers/OneOutboxPublisherJob.cs` / `OneInboxConsumerJob.cs` | Thin. |
| `.../One/Infrastructure/DependencyInjection.cs` | HttpClient `DeveloperWebhooks` 15s timeout. |

### Lhdn

| File | Notes |
|------|------|
| `.../Lhdn/Infrastructure/Workers/LhdnSubmissionJob.cs` | 5s; Take 50 PENDING; per-doc SaveChanges; transient → often FAILED; publishes integration events to **undrained outbox**. |
| `.../Lhdn/Infrastructure/Workers/LhdnStatusPollingJob.cs` | 10s; SUBMITTED poll; events + webhook command; backoff on domain. |
| `.../Lhdn/Infrastructure/Workers/LhdnReferenceDataSeederJob.cs` | Startup JSON seed; path may miss in Docker. |
| `.../Lhdn/Domain/Aggregates/TaxDocument.cs` | Status machine + poll backoff. |
| `.../Lhdn/Infrastructure/DependencyInjection.cs` | **No outbox/inbox hosted services** despite `OutboxEventBus` + tables. |
| `.../Lhdn/Infrastructure/LhdnDbContext.cs` | Declares Outbox/Inbox DbSets. |

### Communications

| File | Notes |
|------|------|
| `.../Communications/Infrastructure/Workers/BroadcastFanoutJob.cs` | 10s; all QUEUED; pages 100; outboxes DispatchMessage; no claim; failure marks whole broadcast. |
| `.../Communications/Domain/Aggregates/Broadcast.cs` | DRAFT→QUEUED→SENDING→COMPLETED/FAILED. |
| Outbox/Inbox jobs | Present; inbox likely idle. |

### Messaging / Payments / Ops

| File | Notes |
|------|------|
| Messaging outbox/inbox | **Only real inbox writers** in platform. |
| Payments outbox/inbox | Present; charge handler logs gateway failure without bus retry. |
| Ops outbox/inbox | Present; no domain background engines. |

### CRM

| File | Notes |
|------|------|
| `.../CRM/Infrastructure/DependencyInjection.cs` | EventBus + handler registration; **zero hosted services**. |
| `.../CRM/Infrastructure/CrmDbContext.cs` | Outbox/Inbox tables configured. |
| `.../CRM/Infrastructure/AnonymizeClientProfileCommandHandler.cs` | Publishes via CrmEventBus → **stuck without publisher job**. |

### Host / deploy / docs / tests

| File | Notes |
|------|------|
| `.../src/Lazuar.Api/Program.cs` | `DatabaseJobTrigger` singleton; `/health` liveness only. |
| `.../deploy/prod/docker-compose.yml` | Single API instance; health = HTTP only. |
| `.../docs/001-cross-module-communication.md` | Describes inbox-backed model **not fully implemented**. |
| Tests | No worker/outbox reliability tests. |

---

## Summary Scorecard

| Area | Grade | Comment |
|------|-------|---------|
| Outbox multi-instance drain | **B+** | SKIP LOCKED done right where jobs exist |
| Outbox completeness | **D** | Lhdn + CRM publishers missing |
| Inbox pattern | **D** | Documented but only Messaging uses it |
| Poison / retry | **D** | Prevents CPU spin by **dropping** failed messages |
| Dunning/Billing engines | **C-** | Core flows exist; state machine + charge attempts inconsistent |
| Webhook delivery | **B-** | Solid attempt model; weak concurrency |
| LHDN jobs | **C** | Functional polling; hard-fail submit; events may not egress |
| Observability | **F** | Logs only; no lag/poison SLOs |
| Multi-instance domain jobs | **F** | Unsafe if API scales out |
| Test coverage (workers) | **F** | Essentially none |

**Highest-impact reliability work:** (1) add **Lhdn + CRM outbox publishers**, (2) **retryable outbox** instead of one-shot poison mark, (3) **Billing↔dunning charge-failure state machine + ChargeAttempt schema**, (4) **SKIP LOCKED claims** on all domain pollers before any horizontal scale.
