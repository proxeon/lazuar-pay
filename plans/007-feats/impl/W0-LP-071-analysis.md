# W0-LP-071 — Failed renewal enters PAST_DUE and starts a dunning run

**Program:** `plans/007-feats` Wave 0  
**Date:** 2026-08-16  
**Status:** Analysis only — **do not implement from this file**  
**Tracker:** [00-checklist-tracker.md](../00-checklist-tracker.md) `LP-071` (Ours = **P**, Wave 0)  
**Product sentence:** Failed renewal enters `PAST_DUE` **and starts a dunning run** (not only an hourly scan that misses the row).

| Also read | Role |
|-----------|------|
| [docs/001-gaps/01-dunning-engine.md](../../../docs/001-gaps/01-dunning-engine.md) | Pre–Phase A inventory. Filename map still useful. **P0 “never enters PAST_DUE” is stale.** |
| [plans/001-backend/001-backend-solidification-checklist.md](../../001-backend/001-backend-solidification-checklist.md) § Phase A | What already landed |
| [plans/007-feats/12-dunning-and-recovery.md](../12-dunning-and-recovery.md) | Post–Phase A engine reread. `DN-002` = assign+PAST_DUE shipped; this ticket is the **run start** half. |

**Sibling tickets (do not implement here):** `LP-072` AUTO_CHARGE retries, `LP-073` email sequence/variables, `LP-076` hard/soft declines, `LP-077` recovered-revenue metrics, `LP-078` terminal cancel/suspend, `LP-079` campaign snapshot.

---

## Verdict

Phase A **did** close the June 2026 P0: a vaulted off-session decline (and a webhook `PAYMENT_FAILED` that carries `subscription_id`) marks the subscription `PAST_DUE`, stamps the pending `ChargeAttemptLog` failed, assigns a matching campaign, and emits `subscription.past_due` once.

Phase A **did not** start a dunning run. Day-0 EMAIL (default campaign offset `0`) is left to `DunningEngineJob`’s hourly past-due claim. That claim loop **re-selects the same oldest `PAST_DUE` row** after every successful save, because status does not change. Other newly past-due subscriptions wait until that oldest row leaves `PAST_DUE` (pay / cancel / suspend). A single-sub demo works. Two overdue subscribers do not.

`LP-071` stays **partial** until:

1. Failure (and the no-token billing path) **starts the run for that subscription now** — assign + dispatch due offsets, at least day 0.
2. The hourly past-due claim **excludes already-processed ids in the same cycle**, so catch-up cannot starve the rest.

Do **not** reopen Phase A.1 (“publish `GatewayPaymentFailed` + `MarkAsPastDue` + assign”). Do **not** add a `DunningRun` table (that is `LP-079`).

---

## Stale-document warning

`docs/001-gaps/01-dunning-engine.md` was correct when written. It is **not** the 2026-08-16 engine.

| Gap-doc claim | Current tree |
|---------------|--------------|
| `GatewayPaymentFailedIntegrationEvent` never published / never subscribed | Published from off-session failure **and** webhook `PAYMENT_FAILED`. Commerce handler subscribed in `UseCommerceSubscriptions`. |
| `ProcessGatewayWebhookCommandHandler` ignores non-completed events | Also accepts `PAYMENT_FAILED` (and `DISPUTE_CREATED`). |
| `BillingEngineJob` no-token emits `subscription.suspended` | Emits `subscription.past_due`. |
| `ChargeAttemptLogs` unique `(SubscriptionId, TargetBillingDate)` | Unique `(SubscriptionId, TargetBillingDate, AttemptNumber)`. |
| Past-due steps exact `DayOffset == daysOverdue` | Catch-up `0 <= DayOffset <= daysOverdue` + `ReminderDispatchLog` on `(SubscriptionId, TargetBillingDate, DayOffset)`. |
| `CurrentDunningStepIndex` dead | Synced from `LastCompletedDayOffset`. |
| No tests on the failure → PAST_DUE path | `GatewayPaymentFailedIntegrationEventHandlerTests` exist. **Still no `DunningEngineJob` tests.** |

Copying the old executive verdict (“failed vaulted charges never enter PAST_DUE”) into a Wave 0 impl would be a lie. The remaining lie is the opposite: **PAST_DUE + campaign FK is not a started run.**

`12-dunning-and-recovery.md` `DN-002` (“Payment-failed → PAST_DUE + assign — do not reopen”) is about the **bridge**. This ticket is the next sentence in the Wave 0 backlog: *“Failed vaulted renewal enters a dunning run.”*

---

## Phase A already closed (do not redo)

From `plans/001-backend/001-backend-solidification-checklist.md` **A.1 / A.3 / A.4 / A.10**, present in source:

| Closed item | Evidence |
|-------------|----------|
| Publish `GatewayPaymentFailed` from webhook `PAYMENT_FAILED` | `ProcessGatewayWebhookCommandHandler` (~L130–138) |
| Publish `GatewayPaymentFailed` from off-session `false` / missing config | `ExecuteOffSessionChargeIntegrationEventHandler.PublishPaymentFailedAsync` |
| Commerce: `MarkAsPastDue` if not already, skip `CANCELED`/`SUSPENDED` | `GatewayPaymentFailedIntegrationEventHandler` (~L63–79) |
| Commerce: assign highest-priority matching campaign if none | Same handler (~L81–110); same predicate as the engine |
| Emit `subscription.past_due` **once** (only when status first flips) | `PublishPastDueAsync` gated on `becamePastDue` |
| Stamp pending attempt failed (`charge_attempt_id` or latest `PENDING`) | `MarkChargeAttemptFailedAsync` |
| Resolve sub id from `subscription_id` or legacy `receipt` | `TryResolveSubscriptionId` |
| Off-session metadata `type` / `subscription_id` / `tenant_id` / `dunning_campaign_id` / `charge_attempt_id` | Off-session publisher + Stripe `ChargeOffSessionAsync` metadata |
| Multi-row attempts | Unique includes `AttemptNumber`; `ChargeAttemptLimits.MaxAttemptsPerBillingCycle = 4` |
| Past-due catch-up inequality | `DunningEngineJob.PastDue.cs` `DayOffset <= daysOverdue` |
| Dispatch idempotency on day offset, not step GUID | `ReminderDispatchLogs` unique `(SubscriptionId, TargetBillingDate, DayOffset)` |
| No-token billing event name is `subscription.past_due` | `BillingEngineJob` (~L222–223) |
| Handler tests for PAST_DUE + assign + priority + receipt fallback | `GatewayPaymentFailedIntegrationEventHandlerTests` |
| Off-session / webhook publish tests | `ExecuteOffSessionChargeIntegrationEventHandlerTests`, `ProcessGatewayWebhookCommandHandlerTests` |
| Domain `MarkAsPastDue` / `AssignDunningCampaign` / `RecoverFromPayment` | `SubscriptionRecoveryTests` |

Phase A A.1 explicitly allowed **either** “fire day-0 immediately **or** leave to engine catch-up.” The tree chose catch-up. That choice is why `LP-071` is still open.

---

## Current paths (as coded)

### Intended money loop (renewal fail)

```
ACTIVE + NextBillingDate <= now
  BillingEngineJob claims FOR UPDATE SKIP LOCKED (batch 50)
    vaulted + no ChargeAttemptLog for that date
      → attempt #1 source=BILLING
      → ExecuteOffSessionCharge (DunningCampaignId=null)
    else no vault
      → MarkAsPastDue + subscription.past_due
      → does NOT assign a campaign
      → does NOT dispatch day-0

ExecuteOffSessionCharge (Payments inbox / in-process from Commerce outbox)
  config missing → GatewayPaymentFailed (failure_reason=gateway_not_configured)
  adapter returns false → GatewayPaymentFailed (failure_reason=charge_declined)
  adapter throws (Billplz NotSupportedException) → UNHANDLED; no failed event
  adapter true including Stripe status "processing" → silence; wait for webhook

GatewayPaymentFailed → InMemoryEventBus (Payments outbox publisher)
  Commerce handler:
    resolve subscription_id | receipt
    MarkFailed on PENDING ChargeAttemptLog
    if CANCELED/SUSPENDED: save attempt only
    else MarkAsPastDue + assign campaign if CurrentDunningCampaignId is null
         + subscription.past_due if newly PAST_DUE
    does NOT load campaign.Steps
    does NOT RecordReminderDispatched
    does NOT publish reminder.dunning / AUTO_CHARGE

DunningEngineJob every Workers:DunningEngineInterval (default 1h)
  claim Status=PAST_DUE (pause null or expired), ORDER BY NextBillingDate LIMIT 1
  assign campaign if missing, catch-up due steps, grace CANCEL/SUSPEND
```

Delivery of `GatewayPaymentFailed` is **not** Commerce inbox. Payments `OutboxEventBus` writes `payments.OutboxMessages`; `PaymentsOutboxPublisherJob` deserializes and `InMemoryEventBus.PublishAsync` fans out to every subscribed handler, including Commerce’s. Commerce then writes its own outbox (`subscription.past_due`, later `reminder.dunning`). That fan-out **does** run today.

### `Subscription.MarkAsPastDue`

```87:91:apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs
    public void MarkAsPastDue()
    {
        Status = "PAST_DUE";
        UpdatedAt = DateTime.UtcNow;
    }
```

Status only. Does not assign a campaign, does not touch `LastCompletedDayOffset`, does not set a “dunning started at” clock. Overdue days are always `(UtcNow.Date - NextBillingDate.Date).Days`. A same-day decline is `daysOverdue == 0` — default day-0 EMAIL is due immediately **if something actually runs**.

`AssignDunningCampaign` resets progress (`LastCompletedDayOffset = null`, step index `0`) and does **not** clear `DunningPausedUntil`.

There is **no** `DunningRun` aggregate. The run is `Status == PAST_DUE` plus `CurrentDunningCampaignId` plus `ReminderDispatchLog` rows for that `NextBillingDate`.

### Commerce `GatewayPaymentFailedIntegrationEventHandler`

Path: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs`

Does:

- Skip when metadata has no Guid `subscription_id` / `receipt`.
- Skip unknown sub / org mismatch (`IgnoreQueryFilters` + `OrganizationId`).
- Fail the attempt; still `PAST_DUE` even if no pending log exists.
- `CANCELED` / `SUSPENDED`: attempt only.
- Campaign match duplicated inline (priority desc, then `CreatedAt` desc, org + product list + `ONLINE_GATEWAY`/`MANUAL` from `VaultedTokenId`).
- Loads campaigns **without** `Include(Steps)`.

Does not:

- Dispatch day-0 / catch-up steps.
- Respect `DunningPausedUntil` (irrelevant for assign; would matter if it dispatched).
- Publish `FulfillmentRequested(COMMUNICATIONS, reminder.dunning)`.
- Start AUTO_CHARGE (correct — billing already owns attempt 1; day-0 default is EMAIL).

Tests cover assign, no reassign when a campaign is already pinned, cancel skip, missing id no-op, priority, receipt fallback, attempt fail-by-id. **None** assert a dunning dispatch.

### Other `GatewayPaymentFailed` handler

`IntegrationCheckoutGatewayEventsHandler` is M2M checkout only (`payment.failed` outbound). It is not a Commerce recovery path. Leave it alone.

### Payments publishers

**Off-session** (`ExecuteOffSessionChargeIntegrationEventHandler`):

- Metadata always has `type=commerce_subscription`, `subscription_id`, `tenant_id`, `receipt`, `failure_source=off_session`, `failure_reason`, `gateway_name`, optional `dunning_campaign_id` / `charge_attempt_id`.
- `GatewayTransactionId` is `"off_session:" + subscriptionId` (not a PI id).
- **No try/catch** around `ChargeOffSessionAsync`. Billplz throws `NotSupportedException`; the attempt stays `PENDING`, the sub stays `ACTIVE`, billing will not insert attempt 1 again (`attemptCount > 0`). That row never becomes `PAST_DUE`.
- Decline codes are not copied (`LP-076`, out of scope).

**Webhook** (`ProcessGatewayWebhookCommandHandler`):

- Publishes `GatewayPaymentFailed` with adapter metadata (merged with integration-checkout session metadata when a session exists).
- Idempotent on event id + business key `PAYMENT_FAILED:{gatewayTxId}`.
- Commerce only recovers if that metadata contains `subscription_id` or `receipt`. Off-session Stripe PI metadata **does** stamp those keys (Phase A A.2). A failure webhook **without** them is a silent Commerce no-op.

**Stripe adapter:**

- `ParseWebhookAsync` handles `checkout.session.completed`, `payment_intent.succeeded`, `charge.dispute.created` only. **`payment_intent.payment_failed` is not mapped.**
- `ChargeOffSessionAsync` returns **true** for `succeeded` **or** `processing`. Async issuer decline after `processing` therefore produces **no** off-session failed event **and** no webhook failed event. Sub stays `ACTIVE` with a `PENDING` attempt.

**CHIP:** `purchase.payment_failure` → `PAYMENT_FAILED`. Metadata comes from the purchase node; if ChargeOffSession stamped `subscription_id`, Commerce can recover via webhook as a second path.

### `BillingEngineJob`

Path: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs`

Claim (relational):

```sql
WHERE "NextBillingDate" IS NOT NULL
  AND "NextBillingDate" <= NOW()
  AND "Status" NOT IN ('PAST_DUE', 'SUSPENDED', 'CANCELED')
ORDER BY "NextBillingDate"
LIMIT 1
FOR UPDATE SKIP LOCKED
```

Important contrast with dunning: the **no-token** branch calls `MarkAsPastDue()`, so the next iteration **cannot** reclaim that row. `BillingEngineJobTests.RunOnce_MarksEachDueSubscriptionPastDue_Independently` is green for that reason — two reminder-only dues both flip in one cycle.

Vaulted branch: insert attempt 1 + publish charge; **status stays `ACTIVE`**. The next claim **is** the same row (no-op because `attemptCount > 0`). That is `LP-052` starvation, not this ticket. Failure entry for vaulted is the event, not a second billing pass.

No-token branch does **not** assign `CurrentDunningCampaignId`. MANUAL / Billplz / reminder-only wait for the dunning scan both to attach a campaign and to send day 0.

### `DunningEngineJob`

Paths under `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/`:

| File | Role |
|------|------|
| `DunningEngineJob.cs` | Hosted loop; `RunOnceAsync` for tests; loads all active campaigns `AsNoTracking` + `Include(Steps)` once per tick |
| `DunningEngineJob.Claim.cs` | Pre-dunning + past-due claim; per-row scope + transaction; `failedIds` **only on exception** |
| `DunningEngineJob.PastDue.cs` | Assign if missing; grace CANCEL/SUSPEND; catch-up steps; AUTO_CHARGE 2–4; comms |
| `DunningEngineJob.PreDunning.cs` | ACTIVE in next 14 days; **inverted** catch-up (`Abs(offset) <= daysUntilDue`) — **not LP-071** |
| `DunningEngineJob.Dispatch.cs` | WA demote; `reminder.dunning` payload with plan/amount/currency/days_overdue |

Past-due claim:

```sql
WHERE s."Status" = 'PAST_DUE'
  AND s."NextBillingDate" IS NOT NULL
  AND (s."DunningPausedUntil" IS NULL OR s."DunningPausedUntil" <= NOW())
ORDER BY s."NextBillingDate"
LIMIT 1
FOR UPDATE SKIP LOCKED
```

Batch size 50. Each iteration: new scope, begin tx, claim, process, `SaveChanges`, **commit (releases the row lock)**. Successful ids are **not** added to `excludeIds`. The next `ORDER BY NextBillingDate LIMIT 1` returns the **same subscription** unless process changed status (grace CANCEL/SUSPEND only).

Default seeded campaign (`GenerateDefaultDunningCampaignsCommand`): −3 EMAIL, **0 EMAIL**, +3 WHATSAPP, grace 7, `CANCEL`. Day 0 is the first customer-visible past-due action. +3 WA is skipped on default deploy (`Messaging:WhatsAppEnabled=false`) and still logged as dispatched if the engine reaches it (`LP-073` / honesty, not this ticket).

There is **no** `DunningEngineJob` test fixture.

---

## Why the hourly scan misses the row

This is the defect the ticket name points at.

`BillingEngineJob` consumes a row by flipping status (`ACTIVE` → `PAST_DUE`). `DunningEngineJob` does **not**. After a successful past-due tick the row is still `PAST_DUE` with the same `NextBillingDate`. `failedIds` is unused. The 50-iteration batch is 50 passes over **one** subscription.

Worked example:

| Hour | PAST_DUE set (by NextBillingDate) | What the job does | Who got day-0 EMAIL |
|------|-----------------------------------|-------------------|---------------------|
| T0 | A (older due), B (newer due, just failed) | Process A 50×. First pass sends A’s due steps; 49 no-ops (`ReminderLogs`). B never claimed. | A only |
| T0+1h … T0+6d | A, B still PAST_DUE (grace 7) | Same | B still silent |
| T0+7d | A hits grace → CANCEL | Next claim can finally take B | B’s day-0 fires **a week late**, then catch-up may also fire +3 in the same pass |

Postgres does not need a clock skew for this. `ORDER BY NextBillingDate` is stable enough that the oldest due date wins every time. Equal due dates still typically return the same heap/index first row.

`FOR UPDATE SKIP LOCKED` does **not** help inside one process after commit. Multi-replica is claim-safe **per row**, not “fair across the PAST_DUE set.”

Phase A’s “leave day-0 to catch-up” therefore only works when **at most one** subscription is `PAST_DUE`, or when the new row happens to have the oldest `NextBillingDate`. That is why a founder demo with one declining card looks “shipped” and a second overdue customer never gets the email.

A newly failed renewal that **never** becomes `PAST_DUE` (adapter throw, Stripe `processing` + unmapped `payment_intent.payment_failed`, webhook without `subscription_id`) is also “missed,” but by **entry**, not by the scan. Those are smaller holes on the same ticket.

---

## Remaining gaps (LP-071 only)

### In scope

| # | Gap | Why it blocks the product sentence |
|---|-----|------------------------------------|
| G1 | Failure handler assigns a campaign and stops. No day-0 dispatch. | Run is not started. Customer waits up to 1h **even in the one-row case**. |
| G2 | Past-due claim does not exclude successfully processed ids. | Hourly catch-up **starves every PAST_DUE except the oldest**. This is “the scan that misses the row.” |
| G3 | Billing no-token `MarkAsPastDue` does not assign or start a run. | Billplz / reminder-only / offline dues become `PAST_DUE` in batch (status flip works) then sit until the same broken scan attaches a campaign. |
| G4 | `ChargeOffSessionAsync` exceptions are not turned into `GatewayPaymentFailed`. | Billplz-with-vault (or any throw) leaves `ACTIVE` + `PENDING` attempt. No PAST_DUE, no run. |
| G5 | Stripe `payment_intent.payment_failed` unmapped; off-session treats `processing` as success. | Async decline never enters `PAST_DUE`. Secondary to G1–G3; cheap and the same entry loop. |

### Explicitly out of scope

| Topic | Ticket / note |
|-------|----------------|
| AUTO_CHARGE attempts 2–4, unique-index retries, pending-attempt pile-up | `LP-072` |
| Pre-dunning inverted `Abs(offset) <= daysUntilDue` | `LP-073` / `DN-015` — do not touch `PreDunning.cs` predicates |
| `{{plan_name}}` / link substitution (already Phase A) | `LP-153` / `LP-073` |
| Default +3 WHATSAPP with no email body | Honesty / `LP-073` |
| Terminal grace CANCEL/SUSPEND behavior | `LP-078` |
| Campaign snapshot / freeze steps on assign | `LP-079` — **no new `DunningRun` table** |
| Hard vs soft decline fork / Stripe `DeclineCode` | `LP-076` |
| `RecordRecovery` / RM metrics | `LP-077` |
| Billing vaulted claim re-selecting the same `ACTIVE` row | `LP-052` (same pattern, different job) |
| WhatsApp transport | Decision 00.4 — leave the demote helper as-is |

Do not invent invoice entities, payday ML, or campaign versioning to close `LP-071`.

---

## Minimal changes

Keep the existing event bridge. Add **per-subscription run start** and **make the hourly claim iterate unique rows**.

### 1. Extract past-due processing so two callers can share it

Move the body of `ProcessPastDueSubscriptionAsync` (+ dispatch helpers it needs) to a small Infrastructure type, e.g.

`apps/lazuar-api/Modules/Commerce/Infrastructure/Dunning/PastDueDunningProcessor.cs`

Signature shape (names flexible):

```csharp
Task ProcessAsync(
    CommerceDbContext db,
    IEventBus eventBus,
    Subscription sub,                 // ReminderLogs loaded
    IReadOnlyList<DunningCampaign> campaigns, // Steps included
    bool whatsAppEnabled,
    CancellationToken ct);
```

Job `ClaimMode.PastDue` calls this. No behavior change inside the processor except what G1–G2 require (it already catch-up-dispatches).

Optionally extract the duplicated match predicate to a pure helper on `DunningCampaign` or a static `DunningCampaignMatcher` in Domain/Application so the handler and processor cannot drift. One method:

`empty targets = match all; else product ∈ list AND method ∈ list; caller sorts by PriorityOrder desc, CreatedAt desc, same org.`

Do **not** put `CommerceDbContext` in Application. Handler and job already live in Infrastructure.

### 2. Start the run on `GatewayPaymentFailed` (G1)

After `MarkAsPastDue` + assign (existing), if status is `PAST_DUE` and (`DunningPausedUntil` is null or ≤ now):

- Load `ReminderLogs` if not already on the instance.
- Load **active** campaigns for matching **with `Steps`** (handler today omits steps).
- Call `PastDueDunningProcessor.ProcessAsync`.
- Then the existing `becamePastDue` webhook + `SaveChanges`.

Idempotency: second webhook / outbox retry hits `ReminderLogs` / unique `(SubscriptionId, TargetBillingDate, DayOffset)` and is a no-op. Call the processor on every failure, not only `becamePastDue`, so a first pass that assigned but failed before dispatch can catch up.

Do **not** fire grace CANCEL on this path any differently than the engine — same method. Default grace 7 means day 0 EMAIL only.

Respect pause: assign+PAST_DUE still happen (ops can see arrears); skip `ProcessAsync` while paused.

### 3. Start the run on billing no-token PAST_DUE (G3)

In `BillingEngineJob` else-branch, after `MarkAsPastDue`, call the same processor (load campaigns+steps, WA flag from config). Keep the existing `subscription.past_due` fulfillment + outbound webhook.

If no matching campaign, stay `PAST_DUE` and log (same as today). Do not invent a default campaign here (`/defaults` already exists).

### 4. Exclude processed ids in the dunning claim loop (G2)

In `DunningEngineJob.Claim.cs` `ProcessClaimedBatchAsync`:

- Keep `failedIds`.
- Add `processedIds`.
- After successful `SaveChanges`/`Commit`, `processedIds.Add(sub.Id)`.
- Pass `failedIds ∪ processedIds` into both claim SQL and in-memory claim.

Do this for **both** `ClaimMode` values while touching the loop (pre-dunning has the same re-claim bug). Do **not** change the pre-dunning due-step inequality (`LP-073`).

No schema change. No new claim column. `FOR UPDATE SKIP LOCKED` stays.

### 5. Off-session throw → failed event (G4)

In `ExecuteOffSessionChargeIntegrationEventHandler`, wrap `ChargeOffSessionAsync` in try/catch (`Exception` or at least `NotSupportedException` + gateway exceptions). Publish `GatewayPaymentFailed` with `failure_reason=charge_declined` (or `gateway_not_supported`) and the same metadata keys. Then Commerce G1 starts the run.

Do **not** change Billplz to pretend it can vault (`LP-047`). Returning false / publishing failed is enough.

### 6. Stripe async failure (G5) — same PR if it stays small

In `StripeGatewayAdapter.ParseWebhookAsync`, map `payment_intent.payment_failed` → `PAYMENT_FAILED` with PI metadata (already has `subscription_id` / `receipt` from ChargeOffSession). Existing webhook handler + Commerce handler do the rest.

Do **not** treat `processing` as failure in `ChargeOffSessionAsync` (would false-PAST_DUE charges that later succeed). Webhook is the right second signal.

### What not to change

- No `DunningRun` / version snapshot table.
- No new public API, TypeSpec, or ops UI.
- No change to `MarkAsPastDue` semantics (status only is fine if the processor is invoked next).
- No change to billing vaulted attempt-1 ownership.
- No WhatsApp enablement.

---

## Tests

Existing coverage to **keep** (extend, do not rewrite):

| Fixture | Already asserts |
|---------|-----------------|
| `GatewayPaymentFailedIntegrationEventHandlerTests` | PAST_DUE + assign, no reassign, cancel skip, missing id, priority, receipt, attempt fail-by-id |
| `ExecuteOffSessionChargeIntegrationEventHandlerTests` | metadata keys; `false` → failed event; missing config → `gateway_not_configured` |
| `ProcessGatewayWebhookCommandHandlerTests` | `PAYMENT_FAILED` publish + business key |
| `SubscriptionRecoveryTests` | `MarkAsPastDue` does not assign; recover/clear |
| `BillingEngineJobTests` | two no-token dues both `PAST_DUE` (in-memory) |
| `DunningCampaignDomainTests` | targeting comments mirroring the handler predicate |

**Add / extend:**

### A. Handler starts the run (`GatewayPaymentFailedIntegrationEventHandlerTests`)

Reuse InMemory Commerce + fake `IEventBus` + `ICrmQueryService`.

1. **Day-0 EMAIL on first fail.** ACTIVE vaulted sub, due today, campaign with offset `0` EMAIL (+ optional `3` EMAIL). After `HandleAsync`: `Status=PAST_DUE`, `CurrentDunningCampaignId` set, one `ReminderDispatchLog` with `DayOffset=0` and `TargetBillingDate=NextBillingDate.Date`, `LastCompletedDayOffset=0`, `FulfillmentRequestedIntegrationEvent` once (`internalApp=COMMUNICATIONS`, `eventType=reminder.dunning`). **No** `ExecuteOffSessionCharge` (day 0 is EMAIL). `subscription.past_due` still once.
2. **Idempotent second fail.** Same event again (or new tx id, same sub): still one reminder log; **no** second `reminder.dunning`.
3. **Already PAST_DUE + campaign, day 0 already logged.** No extra dispatch; no second `subscription.past_due`.
4. **Paused.** `DunningPausedUntil` in the future: still PAST_DUE + assigned; **no** reminder log / comms event.
5. **No matching campaign.** PAST_DUE, `CurrentDunningCampaignId` null, no comms event.
6. **Catch-up if NextBillingDate is 3 days ago** and campaign has `0` and `3`: both offsets logged in this handle (same as engine catch-up). Proves we did not invent a “day-0 only” fork.
7. Keep existing cancel / missing-id / attempt-fail tests.

Handler must `Include` steps + reminder logs or the new tests fail on empty `dueSteps`.

### B. Claim no longer starves (`DunningEngineJob` tests — new fixture)

Mirror `BillingEngineJobTests` (`RunOnceAsync`, InMemory, keyed `CommerceEventBus`).

1. **Two PAST_DUE, both due, both unassigned, one campaign with day-0 EMAIL.** After `RunOnceAsync`, **both** have a day-0 `ReminderDispatchLog` and both received `reminder.dunning`. **This test is red on current Claim.cs** (only the oldest is processed 50×). It is the lock for G2.
2. **Processed id not re-dispatched in the same run** (unique offset).
3. **Paused row skipped**; unpaused sibling still processed.
4. **Do not** assert pre-dunning inequalities here.

### C. Billing no-token starts the run (`BillingEngineJobTests`)

1. Due, no vault, campaign with day-0 EMAIL → `PAST_DUE` + campaign assigned + day-0 log + `reminder.dunning` **and** existing `subscription.past_due`.
2. Two such subs in one `RunOnceAsync` both get day-0 (billing already flips status; processor must run per row).

### D. Off-session throw (G4)

`ExecuteOffSessionChargeIntegrationEventHandlerTests`: adapter throws → still `GatewayPaymentFailed` with `subscription_id` + a `failure_reason`.

### E. Stripe `payment_intent.payment_failed` (G5, if shipped)

Existing Stripe adapter webhook tests (or add one): mapped type `PAYMENT_FAILED`, metadata includes PI `subscription_id` when present. Unmapped types still verify-and-ignore (no retry storm).

### F. Domain matcher (only if extracted)

Move the inline predicates in `DunningCampaignDomainTests` onto the real helper; keep empty-target and product/method filter cases.

**Do not** add a full host e2e (Stripe decline → Resend). Phase A already left that as operator residual. ModuleTests on handler + `RunOnceAsync` are the acceptance bar.

**Suggested new file:**  
`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/DunningEngineJobTests.cs`

---

## Acceptance (when someone implements)

- Vaulted renewal decline → subscriber is `PAST_DUE`, has a campaign, **and** day-0 EMAIL is outboxed **in the failure handle**, not “sometime this hour.”
- Two PAST_DUE subscribers in one dunning `RunOnceAsync` both receive due steps.
- Reminder-only / no-token due → `PAST_DUE` + same day-0 start in the billing tick.
- Redelivered `GatewayPaymentFailed` does not double-send day 0.
- Paused subscriber is not emailed.
- Adapter throw and (if G5 lands) Stripe `payment_intent.payment_failed` still enter `PAST_DUE`.
- No schema migration. No `DunningRun`. No WhatsApp flag flip.

---

## File list (read / likely touch)

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` | Start run after assign |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.Claim.cs` | Exclude processed ids |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PastDue.cs` | Extract processor |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.Dispatch.cs` | Move with processor or leave as partial used by both |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs` | Job remains thin host |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` | No-token start run |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs` | `MarkAsPastDue` / assign — read only unless matcher lives here |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/DunningCampaign.cs` | Optional `Matches(...)` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs` | Catch + publish |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` | Optional `payment_intent.payment_failed` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` | Already publishes — read only |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Contracts/Events/GatewayPaymentFailedIntegrationEvent.cs` | Shape unchanged |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/GatewayPaymentFailedIntegrationEventHandlerTests.cs` | Extend |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs` | Extend |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/DunningEngineJobTests.cs` | **New** — G2 lock |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/ExecuteOffSessionChargeIntegrationEventHandlerTests.cs` | Throw → failed event |

---

*Analysis based on Lazuar Pay source as of 2026-08-16. No runtime soak. No product code from this file.*
