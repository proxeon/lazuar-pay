# W0 — LP-079 analysis: campaign snapshot (do not mutate in-flight)

**Program:** `plans/007-feats`  
**ID:** LP-079 — *Campaign snapshot (don’t mutate in-flight)*  
**Wave:** 0 (`00-implement-ids.md`, `00-checklist-tracker.md` row LP-079 = **N** today)  
**Date:** 2026-08-16  
**Status:** Analysis only — **do not implement from this file**  
**Related tracker:** DN-022 in `plans/007-feats/12-dunning-and-recovery.md` (Recurly-shaped “settings history”). LP-079 is the **smallest freeze** that closes skip/spam, not a version table.

**Feature in one sentence:** When a subscription **enters** PAST_DUE dunning, freeze the campaign definition used for the rest of that run so a merchant edit cannot skip remaining steps or catch-up-spam new ones.

---

## 1. Verdict

| Question | Answer |
|----------|--------|
| Do we have a snapshot today? | **No.** We pin `CurrentDunningCampaignId` and re-read live `DunningCampaign` + `DunningSteps` every hourly tick. |
| Smallest model? | **JSON column on `commerce.Subscriptions`**, written once at assign, cleared with dunning. |
| Run table? | **No for LP-079.** Right later if we want Settings History / per-run analytics. |
| Campaign version rows? | **No for LP-079.** That is Recurly DN-022, not Wave 0. |
| Migration needed? | **Yes.** One nullable `jsonb` column. Recommended SQL backfill for already-assigned rows. No new table, no FK. |
| API / TypeSpec / ops UI? | **No.** Engine + domain + tests only. |
| Pre-dunning (ACTIVE, DayOffset &lt; 0)? | **Out of scope.** There is no assign, so it still live-mutates. |

---

## 2. What exists (read, not assumed)

### 2.1 Campaign aggregate — mutable CMS, no version

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/DunningCampaign.cs`

- Mutable: `Name`, `FinalAction`, `GracePeriodDays`, `PriorityOrder`, targeting lists, `IsActive`.
- Steps live in child `DunningStep` rows (`DayOffset`, `ActionType`, `Subject`, `EmailBody`, `WhatsAppBody`).
- `ClearSteps()` + `AddStep(...)` is the only edit path. Every update **drops and recreates step GUIDs**.
- Counters (`RecoveredRevenue`, `SavedSubscriptions`, `ChurnedSubscriptions`) are campaign-level, not per-run.
- No `Version`, no Settings History, no immutable copy.

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Commands/DunningCampaignCommandHandlers.cs` (`UpdateDunningCampaignCommandHandler`):

1. `UpdateDetails(...)` — grace, final action, targeting, priority, name.  
2. `ClearSteps()` then re-`AddStep` for the posted list.  
3. Optional `Archive()` / `Restore()`.

Delete is blocked while any subscription has `CurrentDunningCampaignId == campaign` (`HasSubscriptionsAssignedToCampaignAsync`). Archive is **not** blocked.

### 2.2 Subscription — pin the id, not the definition

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs`

| Field | Role |
|-------|------|
| `CurrentDunningCampaignId` | Sticky campaign **id** until `ClearDunning` |
| `CurrentDunningStepIndex` | Legacy; kept in sync with last completed offset |
| `LastCompletedDayOffset` | Highest dispatched offset this run (ops) |
| `DunningPausedUntil` | PAST_DUE claim filter only |
| `MetadataJson` | Checkout metadata (`jsonb`). **Not** a home for a campaign snapshot |

`AssignDunningCampaign(campaignId)` sets the id, zeros step progress, **does not** copy steps/grace/copy.  
`ClearDunning()` (also called from `RecoverFromPayment` / `Resume`) nulls the id and progress.  
`MarkAsPastDue()` does **not** assign.  
There is no snapshot field.

`CurrentDunningCampaignId` is a bare `uuid` — **no FK** to `DunningCampaigns`. Same pattern as `ChargeAttemptLog.DunningCampaignId` / `DunningStepId` (correlation only; `ClearSteps` already orphans those GUIDs).

### 2.3 Who assigns

Two production writers. Both must snapshot.

| Site | When | What it does today |
|------|------|--------------------|
| `GatewayPaymentFailedIntegrationEventHandler` | First fail (or already PAST_DUE with id null) | Match active campaigns (priority DESC, `CreatedAt` DESC, product + `ONLINE_GATEWAY`/`MANUAL`). `AssignDunningCampaign(id)` only. Query does **not** `Include(Steps)`. |
| `DunningEngineJob.PastDue` | Claimed `PAST_DUE` with id still null (no-token billing path, missed fail event) | Same match against the cycle’s in-memory active list. Then `AssignDunningCampaign(id)`. |

`BillingEngineJob` marks no-token due subs `PAST_DUE` and **does not** assign. Next dunning tick is the assign site. Leave that split; snapshot belongs at assign, not at `MarkAsPastDue`.

`HandleAsync_AlreadyPastDue_DoesNotReassignWhenCampaignPresent` is the existing “pin is sticky” lock. Snapshot must be equally sticky: a later fail or tick must **not** rewrite JSON.

### 2.4 Claim / dispatch (the mutate surface)

Hourly hosted job, partials:

| File | Job |
|------|-----|
| `DunningEngineJob.cs` | Load **active** campaigns + steps `AsNoTracking`, then PreDunning batch then PastDue batch |
| `DunningEngineJob.Claim.cs` | `SELECT s.* … FOR UPDATE SKIP LOCKED`; PAST_DUE + pause filter; load `ReminderLogs` |
| `DunningEngineJob.PastDue.cs` | Assign if null → resolve campaign **by id from the active list** → grace/final → due steps |
| `DunningEngineJob.PreDunning.cs` | No pin. Match live campaign every tick |
| `DunningEngineJob.Dispatch.cs` | WhatsApp demotion + `FulfillmentRequestedIntegrationEvent` using **that step’s copy** |

Past-due execution today:

1. If `CurrentDunningCampaignId` is null → match + assign id.  
2. `campaigns.FirstOrDefault(c => c.Id == sub.CurrentDunningCampaignId)`.  
3. If missing (`IsActive=false` so it was not loaded) → **`return`**. Archive-while-assigned is a stuck PAST_DUE.  
4. If `daysOverdue >= campaign.GracePeriodDays` and final is CANCEL/SUSPEND → terminal, then `return` (later snapshot steps never run).  
5. Due steps: `DayOffset >= 0 && DayOffset <= daysOverdue` and no `ReminderLogs` row for `(DayOffset, TargetBillingDate.Date)`.  
6. AUTO_CHARGE vs communication; then `RecordReminderDispatched(step.Id, targetDate, step.DayOffset)` even on skip.

Idempotency (Phase A A.4): unique index

`ReminderDispatchLogs (SubscriptionId, TargetBillingDate, DayOffset)`

**not** step GUID. Same offset after `ClearSteps` will not re-fire. That is the only anti-spam we have.

Catch-up: any **new** live offset already `<= daysOverdue` fires on the next tick. That is the spam. Removing an unsent offset is the skip.

`ChargeAttemptLog.DunningStepId` is not an FK. Storing a snapshot step id after `ClearSteps` is already legal.

### 2.5 Tests that exist vs missing

| Coverage | File | Snapshot-relevant? |
|----------|------|--------------------|
| Assign / clear / recover / reassign progress | `SubscriptionRecoveryTests.cs` | Extend: snapshot write + clear |
| Campaign match / archive / counters | `DunningCampaignDomainTests.cs` | Factory unit tests only |
| Fail → PAST_DUE + assign; no reassign | `GatewayPaymentFailedIntegrationEventHandlerTests.cs` | Assert snapshot written; not rewritten |
| Billing claim / PAST_DUE no-token | `Workers/BillingEngineJobTests.cs` | Pattern to copy; no snapshot here |
| **Engine job** | **none** | **Must add** — skip/spam is an engine behavior |

`RunOnceAsync` is already `internal` on `DunningEngineJob` for module tests. In-memory claim path exists (`ClaimSubscriptionInMemoryAsync`).

---

## 3. Skip / spam matrix (why the pin is not enough)

Recurly: *“changes to a campaign won't affect invoices already in dunning.”*  
Us: freeze the **id**, re-read the **definition**.

Assume assigned, `LastCompletedDayOffset = 0`, `daysOverdue = 5`, live/snapshot steps at 0 / 3 / 7, grace 14, final CANCEL.

| Merchant edit after assign | Today | After LP-079 |
|----------------------------|-------|--------------|
| Change remaining EMAIL body / subject | Next dispatch uses **new** copy | Snapshot copy |
| Add offset `5` (or `1`) | Catch-up **fires now** (spam) | Ignored |
| Delete unsent offset `3` | Never sent (skip) | Still sent at day 3 (or on catch-up if already overdue) |
| Move `3` → `10` (`ClearSteps` new GUID, new offset) | Offset 3 skipped; 10 fires at day 10 | Offset 3 still in plan |
| Shrink grace 14 → 5 | Next tick CANCEL/SUSPEND; 7 never runs | Grace 14 still applies |
| Widen grace / change CANCEL → NONE | Terminal delayed or dropped | Original final + grace |
| Archive (`IsActive=false`) | Engine cannot load id → **stuck** | Snapshot still executes (side effect) |
| New higher-priority campaign | No steal (id sticky) | Unchanged |
| Same offset, new step GUID | No re-fire (DayOffset unique) | Unchanged |
| Recover / resume / log-pay `ClearDunning` | Id cleared | JSON cleared |
| Next cycle PAST_DUE | New assign from **then**-live campaign | New snapshot |

Phase A only closed the “edit regenerates GUIDs → re-spam same offset” cell. LP-079 is the rest of that table.

---

## 4. Options

### A — JSON on `Subscriptions` (choose this)

Add `DunningCampaignSnapshotJson` `jsonb` NULL. Serialize a small immutable DTO at assign. Past-due engine reads that DTO for grace, final action, and steps. Live campaign remains the CMS + recovery/churn counters.

**Why smallest:**

- Same pattern as `MetadataJson` (`20260814184123_AddSubscriptionAndCheckoutMetadataJson`).
- Claim is already `SELECT s.* FOR UPDATE`. Snapshot travels on the locked row; no join, no second aggregate.
- Default campaign is ~3 steps. Bodies TOAST if large. Fine for `BatchSize = 50`.
- `ClearDunning` already owns run teardown — null the JSON there.
- No open-run uniqueness, no run lifecycle, no repository.

**Costs:** duplicated JSON per in-flight sub; no Settings History; lost after clear (recovery still uses live campaign id captured **before** clear, as today).

### B — `DunningRuns` table (reject for LP-079)

New row per `(SubscriptionId, TargetBillingDate)` with snapshot JSON + `CurrentDunningRunId`. Better for audit after clear and per-run recovered $. Requires new entity, EF, unique open-run, close-on-clear, claim join. That is DN-022 / dashboard work, not skip/spam.

### C — Immutable campaign versions (reject for LP-079)

Every `UpdateDunningCampaign` inserts `DunningCampaignVersion` + step rows; pin `VersionId`. Dedupes snapshots and gives Recurly Settings History. Forces versioning on the CMS write path. Too big for Wave 0.

### D — Stuff it into `MetadataJson` (reject)

Checkout persistence (`CommerceCheckoutMetadata`) is a different map. `ClearDunning` would have to surgically delete keys. Mixing recovery plan with `aura_org_id` / `billing_interval` is a footgun.

---

## 5. Proposed snapshot (option A)

### 5.1 Column

| Item | Value |
|------|--------|
| Table | `commerce.Subscriptions` |
| CLR | `string? DunningCampaignSnapshotJson` on `Subscription` |
| SQL | `jsonb NULL` |
| Index | None (not filtered/joined) |
| FK | None |
| EF | `HasColumnType("jsonb")` next to `MetadataJson` |

Invariant: `CurrentDunningCampaignId` and snapshot are both null, or both set. After backfill, treat “id set, JSON null” as **lazy backfill**, not as “run without a plan.”

### 5.2 JSON shape (`v: 1`)

Snake_case, same family as `CommerceCheckoutMetadata` / dunning fulfillment payload.

```json
{
  "v": 1,
  "campaign_id": "018f…",
  "captured_at": "2026-08-16T04:00:00Z",
  "name": "Standard Recovery Strategy",
  "final_action": "CANCEL",
  "grace_period_days": 7,
  "steps": [
    {
      "id": "018f…",
      "day_offset": 0,
      "action_type": "EMAIL",
      "subject": "Action Required: {{plan_name}} renewal due today",
      "email_body": "…",
      "whatsapp_body": null
    }
  ]
}
```

| Include | Why |
|---------|-----|
| `v` | Evolve without a second column |
| `campaign_id` | Must match `CurrentDunningCampaignId` |
| `captured_at` | Ops / debug |
| `name` | Display only; engine ignores |
| `final_action`, `grace_period_days` | Terminal path |
| `steps[].id` | `ReminderDispatchLog.ScheduleId` + `ChargeAttemptLog.DunningStepId` stay stable for this run |
| `steps[]` offset / action / copy | Dispatch + AUTO_CHARGE calendar |

| Exclude | Why |
|---------|-----|
| Targeting, priority, `IsActive` | Already not re-evaluated after pin |
| Counters | Still increment **live** campaign by id (`RecordRecovery` / `RecordChurn`) |
| Product price / currency | Dispatch still reads `Product` today (`LP-153` / price snapshot is not this ticket) |

Copy **all** steps including `DayOffset < 0`. Past-due filter stays `DayOffset >= 0`. Cheaper than a special case. Pre-dunning still does not read this JSON.

Factory: `DunningCampaignSnapshot.From(DunningCampaign)` (steps must be loaded). Parse unknown `v` → treat as corrupt → lazy-rebuild from live or skip the tick (do not throw the batch).

### 5.3 Domain API (smallest)

Keep one assign method so tests and production cannot pin an id without a plan:

- `AssignDunningCampaign(Guid campaignId, DunningCampaignSnapshot snapshot)` — set id, serialize JSON, reset step progress (same as today). Reject if `snapshot.CampaignId != campaignId`.  
- `ClearDunning` / `RecoverFromPayment` / `Resume` — also null JSON.  
- Re-assign replaces JSON and resets progress (today’s progress reset). Production still must not re-assign mid-run.

Existing unit tests that call `AssignDunningCampaign(id)` need a minimal snapshot argument (or a test helper `AssignDunningCampaignForTest(id)` that writes an empty-steps `v:1` object). Prefer the explicit argument so “assign without freeze” cannot regress.

Pause/resume dunning: unchanged. Pause is not a campaign edit.

### 5.4 Engine / fail-handler behavior

**Assign sites**

1. Failed handler: `Include(c => c.Steps)` (or load steps before factory). `AssignDunningCampaign(id, From(campaign))`.  
2. PastDue: same, using the already-included in-memory campaign.  
3. If id is already set: do **not** touch JSON (same as no reassign).

**PastDue resolve (replace live-only lookup)**

1. If id is null → match live **active** campaigns → assign + snapshot.  
2. If JSON is null but id is set → **lazy backfill**: load campaign by id with `IgnoreQueryFilters` + steps, **including archived**. Write snapshot. (Covers pre-migration rows and in-memory tests.)  
3. If JSON still missing (campaign hard-deleted — should be impossible while assigned) → `return` (same stuck as today).  
4. Grace / final / due-step loop use the **snapshot**, not `campaigns.FirstOrDefault`.  
5. `RecordChurn` still reloads the **tracked live** campaign by `CurrentDunningCampaignId` (today’s AsNoTracking comment stays valid).  
6. AUTO_CHARGE `dunningCampaignId` remains the live campaign id; `dunningStepId` is the **snapshot** step id.

Do **not** filter `IsActive` when executing a snapshot. That is how archive-while-assigned stops being a stuck PAST_DUE without a separate DN-023 project.

**PreDunning:** no change. Document as leftover: adding a −1 email after some ACTIVE subs already entered the 14-day window still catch-up-spams. LP-079 text is “when a subscription **enters dunning**” = PAST_DUE assign.

**Load of active campaigns at cycle start** stays. Needed for unassigned match + pre-dunning.

### 5.5 What not to build

- Re-snapshot / “apply edit to in-flight” API. CS tool remains Pause.  
- Ops “running vs current” UI.  
- TypeSpec / DTO field.  
- Pre-dunning pin on ACTIVE.  
- Version list on the campaign.  
- Changing DayOffset uniqueness or same-day EMAIL+AUTO_CHARGE (DN-024).  
- Putting snapshot write in `MarkAsPastDue` or `BillingEngineJob`.

---

## 6. Migration

**Yes. Required.**

Precedent: `Modules/Commerce/Infrastructure/Migrations/20260814184123_AddSubscriptionAndCheckoutMetadataJson.cs`.

| Step | Detail |
|------|--------|
| Add column | `DunningCampaignSnapshotJson jsonb NULL` on `commerce.Subscriptions` |
| Backfill | Recommended, same migration, after `AddColumn`. Cannot recover the definition they *originally* entered; freeze **current** live campaign+steps so the first post-deploy edit cannot rewrite remaining journey. |
| Filter | `CurrentDunningCampaignId IS NOT NULL AND DunningCampaignSnapshotJson IS NULL` |
| Join | `DunningCampaigns` by id (include archived). `jsonb_agg` steps ordered by `DayOffset` (empty `[]` if none). |
| Down | `DropColumn` |

Lazy backfill in the engine is belt-and-suspenders for rows the SQL misses (in-memory, failed deploy). Do not skip SQL and hope the first tick wins a race with a merchant save.

No data on `DunningCampaigns` / `DunningSteps`. No unique index. Designer + `CommerceDbContextModelSnapshot` update via the module’s usual `dotnet ef migrations add`.

---

## 7. Tests (required to call LP-079 done)

There is no `DunningEngineJob` test today. Skip/spam cannot be proven in domain tests alone. Add `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/DunningEngineJobTests.cs` beside `BillingEngineJobTests` (in-memory `CommerceDbContext`, keyed `IEventBus`, `IConfiguration` for `Messaging:WhatsAppEnabled=false`, `RunOnceAsync`).

### 7.1 Domain — extend `SubscriptionRecoveryTests`

| Test | Assert |
|------|--------|
| Assign stores snapshot JSON / parsed `campaign_id` + grace + steps | Not null; matches factory input |
| `ClearDunning` / `RecoverFromPayment` / `Resume` null JSON | Snapshot gone with the id |
| Re-assign replaces JSON and resets `LastCompletedDayOffset` | New campaign_id in JSON |
| Assign rejects snapshot whose `campaign_id` ≠ argument | Throw |

### 7.2 Factory

| Test | Assert |
|------|--------|
| `From(campaign)` copies final, grace, every step field including bodies and ids | Deep equal |
| Round-trip serialize / parse `v:1` | Stable |
| Parse empty / garbage | Non-throwing miss → engine lazy path |

### 7.3 Failed handler — extend `GatewayPaymentFailedIntegrationEventHandlerTests`

| Test | Assert |
|------|--------|
| First fail writes snapshot that includes steps (handler must load steps) | JSON present; step count / offset / body match |
| Already assigned + live campaign edited (new step, new grace) → handle again | Id **and** JSON unchanged |
| Priority match still picks high campaign; snapshot is **that** campaign | Existing prio test + JSON `campaign_id` |

### 7.4 Engine — new fixture (acceptance)

Seed: PAST_DUE, `NextBillingDate` in the past, vault token, campaign with steps 0 EMAIL / 3 EMAIL / 7 AUTO_CHARGE, grace 14, CANCEL. `RunOnce` once so day-0 (and catch-up of any `<= daysOverdue`) records against the **snapshot**. Then mutate the **live** campaign via `UpdateDetails` + `ClearSteps` + `AddStep` (same as the command handler). `RunOnce` again.

| # | After live edit | Expect |
|---|-----------------|--------|
| E1 | Add offset already `<= daysOverdue` | **No** new `ReminderDispatchLog` / no extra fulfillment publish (anti-spam) |
| E2 | Delete an unsent snapshot offset (e.g. drop 3 while `daysOverdue >= 3`) | That offset **still** dispatches from snapshot (anti-skip) |
| E3 | Change remaining EMAIL body | Publish payload `email_body` is **snapshot** text |
| E4 | Shrink grace to `<= daysOverdue`, final CANCEL | Status stays PAST_DUE; no cancel event |
| E5 | Archive campaign | Next tick still dispatches remaining snapshot steps (not stuck) |
| E6 | Recover / `ClearDunning` | JSON null; later PAST_DUE + new assign takes **new** live definition |
| E7 | Id set, JSON null, live campaign still present | First tick backfills then executes live-at-backfill plan |
| E8 | Unique `(SubscriptionId, TargetBillingDate, DayOffset)` | Second tick does not re-insert same offset |
| E9 | Pre-dunning ACTIVE + live add of a negative offset already in window | **Still fires** (documents leftover; not a fail) |

AUTO_CHARGE rows: `DunningCampaignId` = campaign id, `DunningStepId` = snapshot step id (even after `ClearSteps` deleted the live row).

Wire `WhatsAppEnabled=false` so WHATSAPP-only steps skip the same way as production; snapshot still records the offset (today’s `RecordReminderDispatched` on skip).

---

## 8. Touch list (when a later program implements)

| Area | Path | Change |
|------|------|--------|
| Domain | `Subscription.cs` | Column + assign/clear |
| Domain | new snapshot record (e.g. `Domain/ValueObjects/DunningCampaignSnapshot.cs`) | From / parse / serialize |
| EF | `CommerceDbContext.cs` | `jsonb` mapping |
| EF | new Commerce migration | Column + backfill SQL |
| Fail path | `GatewayPaymentFailedIntegrationEventHandler.cs` | Include steps; pass snapshot; never rewrite |
| Engine | `DunningEngineJob.PastDue.cs` | Assign+snapshot; execute snapshot; lazy backfill; ignore `IsActive` for execution |
| Tests | recovery + failed-handler + **new** engine fixture | §7 |
| Leave alone | TypeSpec, ops builder, `CommerceQueryService.Dunning`, pre-dunning, billing job, pause handlers, `MetadataJson` | |

---

## 9. Done when

- A PAST_DUE assign persists a `v:1` snapshot of grace + final + steps.  
- Mid-run campaign save cannot add catch-up steps, drop unsent snapshot steps, change remaining copy, or retarget grace/final for that sub.  
- Clear/recover/resume drop the JSON. Next run snapshots the campaign as it is **then**.  
- Already-assigned fail events do not rewrite the JSON.  
- Commerce migration adds the column; existing assigned rows backfilled or lazily backfilled.  
- Engine tests E1–E8 green. E9 exists so we do not accidentally “fix” pre-dunning inside this ticket.

**Still N after this ticket (honest):** Recurly Settings History, per-version analytics, pre-dunning immutability, ops “this subscriber’s frozen plan” UI.

Tracker flip after implement: LP-079 Lazuar **N → Y** (or **P** if pre-dunning leftover is judged to keep the row partial — recommend **Y** because the sold path is PAST_DUE recovery, and the tracker sentence is “snapshot campaign at run start”).
