# W0-LP-078 — Terminal dunning action actually suspends or cancels after last step

**Date:** 16 August 2026  
**Status:** Analysis only — **do not implement from this file**  
**Tracker:** [00-checklist-tracker.md](../00-checklist-tracker.md) `LP-078` (Wave 0, Lazuar **P**)  
**Implement list:** [00-implement-ids.md](../00-implement-ids.md) “Terminal suspend / cancel after dunning”  
**Evidence (do not reopen):** [12-dunning-and-recovery.md](../12-dunning-and-recovery.md) DN-008 / DN-024; [11-subscriptions-lifecycle.md](../11-subscriptions-lifecycle.md) SL-040 / SL-041

**This ticket is not** LP-071 (enter PAST_DUE), LP-072 (AUTO_CHARGE), LP-073 (email send), LP-077 (recovered $), or LP-079 (campaign snapshot). Adjacent holes are listed only so implementers do not “fix” them here.

---

## 0. Verdict

The campaign **stores** a terminal action. The worker **can** call `Subscription.Cancel()` / `Suspend()`. Domain methods **do** flip status. Billing **does** stop charging `CANCELED` / `SUSPENDED`. Recovery **does** accept `SUSPENDED`.

It is still **P**, not **Y**, because the engine does **not** apply the terminal action after the last recovery step. It applies it on a **grace clock that runs first and returns**, which:

1. Skips any remaining step whose `DayOffset >= GracePeriodDays`.
2. Can cancel/suspend **before any past-due step** when `GracePeriodDays == 0` (`daysOverdue >= 0` is true on the first PAST_DUE tick).
3. Also `return`s when `FinalAction == NONE`, so later steps never run after the grace day.
4. Treats a step `ActionType` of `CANCEL` / `SUSPEND` as a **communication** (never a state change).

Default seed happens to work (last past-due step `+3`, grace `7`). New-campaign UI defaults (grace `3`, empty or later steps) and any merchant who puts a step on/after the grace day **do not** get “after last step, then suspend/cancel.”

There is **no `DunningEngineJob` test**. Domain tests never assert engine terminal.

**LP-078 is one engine reorder + one terminal-day formula + a job test matrix.** Do not add `ActionType` cancel/suspend steps. Do not invent a new status. Do not snapshot campaigns (LP-079).

---

## 1. Product contract for this ID

Sellable sentence after this ticket:

> When a PAST_DUE subscription finishes the campaign’s last past-due step **and** has waited at least `GracePeriodDays`, `FinalAction=CANCEL` sets `CANCELED` and `FinalAction=SUSPEND` sets `SUSPENDED`. Integrators get the matching typed event. Billing stops. Update-payment still works for `SUSPENDED`.

| Input | Result |
|-------|--------|
| `FinalAction=CANCEL` | `Status=CANCELED`, `RecordChurn()`, `LazuarMetrics.RecordDunningCancel()`, `SubscriptionCanceledIntegrationEvent`, internal `subscription.canceled` fulfillment |
| `FinalAction=SUSPEND` | `Status=SUSPENDED`, `SuspendedAt=now`, **no** `RecordChurn()`, `SubscriptionSuspendedIntegrationEvent`, internal `subscription.suspended` |
| `FinalAction=NONE` (or anything else) | Stay `PAST_DUE`; **do not** block remaining steps |
| Last past-due `DayOffset` **after** grace | Last step **must** dispatch, then terminal on that day (or later if job was down) |
| Last past-due `DayOffset` **before** grace | Terminal on the grace day (today’s default seed) |
| No past-due steps | Terminal on the grace day (builder empty-timeline copy) |
| `GracePeriodDays=0` + a day-0 step | Dispatch day 0, **then** terminal on the same tick |
| `DunningPausedUntil` in the future | Claim skips the row; terminal waits |
| Archived / missing campaign | Stuck `PAST_DUE` — **out of scope** (archive footgun, DN-023) |

Industry cousins (do not copy extra states): Stripe cancel / unpaid / leave past_due; Chargebee sub+invoice final action; Recurly expire at end of cycle; Paddle pause or cancel after exhaustion. We already have CANCEL / SUSPEND / NONE. LP-078 is making that clock honest.

---

## 2. What exists (read, not redesigned)

### 2.1 `DunningStep.ActionType` is not the terminal mechanism

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Entities/DunningStep.cs`

```12:28:apps/lazuar-api/Modules/Commerce/Domain/Entities/DunningStep.cs
    /// <summary>EMAIL, WHATSAPP, or AUTO_CHARGE</summary>
    public string ActionType { get; private set; }
    // ...
        ActionType = actionType.ToUpperInvariant();
```

| Fact | Detail |
|------|--------|
| Allowed in ops UI | `EMAIL` \| `WHATSAPP` \| `AUTO_CHARGE` (`DunningStepEditor.tsx`) |
| Also accepted by engine | `ALL` (pre-dunning comms), `AUTOCHARGE` (typo alias of auto-charge) |
| Persistence | Free `string`, max 50 (`CommerceDbContext`) |
| Domain invariant | **None.** `AddStep` uppercases whatever the API posts |
| TypeSpec | `action_type: string` — not an enum |

Engine branches (`DunningEngineJob.PastDue.cs`):

| `ActionType` | Branch |
|--------------|--------|
| `AUTO_CHARGE` / `AUTOCHARGE` | Off-session charge if vault + attempt &lt; 4; **always** `RecordReminderDispatched` after |
| anything else | `ResolveEffectiveCommunicationAction` → `FulfillmentRequested(COMMUNICATIONS, reminder.dunning)` |

So an API step with `action_type=CANCEL` or `SUSPEND` is **not** a terminal step. It is a comms payload with `action_type` set to that string. `Subscription.Cancel()` / `Suspend()` are never called.

**Do not** add CANCEL/SUSPEND to the step select. Terminal belongs on the campaign (`FinalAction`), same as Chargebee/Recurly “end of dunning,” not as another timeline chip.

### 2.2 Campaign terminal fields

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/DunningCampaign.cs`

- `FinalAction`: uppercased; blank → `"NONE"`. No allow-list. `"cancel"` works; `"EXPIRE"` is silent NONE.
- `GracePeriodDays`: any `int` (UI `min=0`; backend does not reject negative).
- `RecordChurn()`: `ChurnedSubscriptions++`. Engine calls this **only** on CANCEL.
- `RecordRecovery`: not this ticket (LP-077).

Ops copy (`CampaignSettingsPanel.tsx`): “Executes automatically after Grace Period ends.” List page: “After {n} Days.” Empty timeline (`CampaignTimeline.tsx`): “wait until the Grace Period ends, then execute the Terminal Action.”

Defaults:

| Surface | `FinalAction` | `GracePeriodDays` | Last past-due step |
|---------|---------------|-------------------|--------------------|
| Seed `/defaults` + tenant bootstrap | `CANCEL` | **7** | `+3` WHATSAPP (skipped on default deploy) |
| New campaign builder | `CANCEL` | **3** | none until the merchant adds one |

Create/update handlers (`DunningCampaignCommandHandlers.cs`) pass `FinalAction` / `GracePeriodDays` / `ActionType` through with **no validation**.

### 2.3 `DunningEngineJob` “terminal steps” — there are none

There is no last-step handler. Terminal is a **grace gate at the top** of `ProcessPastDueSubscriptionAsync`:

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PastDue.cs`

```54:118:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PastDue.cs
        if (daysOverdue >= campaign.GracePeriodDays)
        {
            if (campaign.FinalAction == "CANCEL" || campaign.FinalAction == "SUSPEND")
            {
                // Cancel() or Suspend() + events + (CANCEL only) RecordChurn + metric
            }
            return; // always — including FinalAction=NONE
        }

        var dueSteps = campaign.Steps
            .Where(s => s.DayOffset >= 0 && s.DayOffset <= daysOverdue)
            // not yet in ReminderLogs for this target date
```

Claim (`DunningEngineJob.Claim.cs`): `Status = 'PAST_DUE'` and pause expired. `SUSPENDED` / `CANCELED` never re-enter. After a successful terminal, the row leaves the query. No hourly re-cancel.

`daysOverdue = (UtcNow.Date - NextBillingDate.Date).Days`. First PAST_DUE tick is usually **0**.

Worked clocks (today):

| Campaign | Day overdue | What runs |
|----------|-------------|-----------|
| Seed: steps −3 / 0 / +3, grace 7, CANCEL | 0 | day-0 EMAIL |
| | 3 | day-3 WHATSAPP consumed (skipped if no email body) |
| | 7 | **CANCEL** (last step already done — this is the lucky path) |
| New UI: grace 3, CANCEL, step +7 EMAIL | 3 | **CANCEL**, day-7 email **never** |
| grace 0, CANCEL, step 0 EMAIL | 0 | **CANCEL immediately**, day-0 email **never** |
| grace 3, NONE, step +7 | ≥3 | `return`; day-7 email **never** |
| grace 7, CANCEL, last step +3, job down until day 10 | 10 | CANCEL; no leftover past-due step to skip |
| Campaign archived (`IsActive=false`) | any | `campaign == null` → **return**; never terminals |

Pre-dunning (`DunningEngineJob.PreDunning.cs`) never looks at `FinalAction`. Correct.

Dispatch (`DunningEngineJob.Dispatch.cs`) never looks at `FinalAction`. Correct.

### 2.4 `Subscription.Suspend` / `Cancel`

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs`

```93:127:apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs
    public void Suspend()
    {
        Status = "SUSPENDED";
        SuspendedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
    // ...
    public void Cancel()
    {
        Status = "CANCELED";
        UpdatedAt = DateTime.UtcNow;
    }
```

| | `Suspend()` | `Cancel()` |
|--|-------------|------------|
| Status | `SUSPENDED` | `CANCELED` |
| Timestamp | `SuspendedAt` | **none** (`CanceledAt` does not exist) |
| Clears dunning? | **No** | **No** (`CurrentDunningCampaignId` stays) |
| Status guard | **None** (engine only calls from PAST_DUE) | **None** (admin/portal handlers guard) |
| Publishes events? | No — callers do | No — callers do |
| Used by | Dunning grace only | Dunning grace, admin cancel, portal cancel, GDPR anonymize |

Related methods (do not change for LP-078):

- `Resume(newNextBilling)` — payment / record-pay from SUSPENDED; **does** `ClearDunning()`.
- `RecoverFromPayment` — payment from PAST_DUE; **does** `ClearDunning()`.
- `ClearDunning()` — unpins campaign; does **not** change `Status`.

Domain tests (`SubscriptionRecoveryTests.cs`) cover Resume / Recover / ClearDunning. **No test** that `Cancel()` / `Suspend()` themselves set status. **No test** that the engine calls them.

### 2.5 Downstream — already enough if the engine calls the methods

| Path | After `Cancel()` | After `Suspend()` |
|------|------------------|-------------------|
| Billing claim | Excluded (`NOT IN PAST_DUE, SUSPENDED, CANCELED`) | Same |
| Dunning claim | Excluded (PAST_DUE only) | Same — **no further campaign messages** |
| Failed-payment handler | Skips PAST_DUE assign | Same |
| Update-payment / arrears | Rejected (not PAST_DUE/SUSPENDED) | **Allowed** |
| Ops Log Payment | Rejected (`CANCELED`) | `Resume()` + `SubscriptionResumed` (**no** `RecordRecovery` — LP-077) |
| Gateway pay success | Not arrears | `Resume()` + `RecordRecovery` if campaign id still on the row or in checkout metadata |
| Outbound webhook | `subscription.canceled` via `SubscriptionLifecycleIntegrationEventHandlers` | `subscription.suspended` |
| Lifecycle email | “Subscription Cancelled” template | Misnamed “Payment Failed” + hardcoded `portal.lazuar.com` — **out of scope** (LP-151) |
| Ops recovery panel | Hidden (`status === "PAST_DUE"` only) | Hidden — merchant loses the dunning widget; update-payment still works |
| Delete campaign | `HasSubscriptionsAssignedToCampaignAsync` is **any** row with `CurrentDunningCampaignId` — canceled/suspended rows **pin** the campaign | Same |

`HasSubscriptionsAssignedToCampaignAsync` (`CommerceRepository.cs` L104–108) does not filter status. After terminal, delete stays blocked. Small hygiene; not the honesty bug.

---

## 3. Gaps (in scope for LP-078)

| # | Gap | Why LP-078 fails |
|---|-----|------------------|
| G1 | Grace checked **before** due steps; always `return` | Last step on/after grace never runs. “After last step” is false. |
| G2 | Terminal day = `GracePeriodDays` only | Contradicts the ticket. Need **later of grace and last past-due `DayOffset`**. |
| G3 | `FinalAction=NONE` shares the same `return` | Steps after grace are dead even when the merchant asked to leave unpaid. |
| G4 | `GracePeriodDays=0` ⇒ `0 >= 0` on first tick | Immediate cancel/suspend; day-0 EMAIL/AUTO_CHARGE skipped. |
| G5 | `ActionType` CANCEL/SUSPEND is comms, not domain | Anyone posting those types via API thinks they configured a terminal step. Engine must not grow that API; document + skip. |
| G6 | No engine tests | G1–G4 are unasserted. `RunOnceAsync` is already `internal` + `InternalsVisibleTo` `Lazuar.ModuleTests`. |

### Not LP-078 (do not touch)

| Item | Owner |
|------|--------|
| Pre-dunning inverted catch-up | DN-015 / not an ID on the Wave 0 implement list as its own row |
| Campaign snapshot / live mutate | LP-079 |
| Hard vs soft decline | LP-076 (Wave 3) |
| WhatsApp / default +3 skipped | LP-074 / honesty DN-013 |
| `RecordChurn` on SUSPEND | DN-008 hygiene; SUSPEND is recoverable, not churn |
| `RecordRecovery` on Log Payment | LP-077 |
| Lifecycle suspend URL | LP-151 / DN-031 |
| Archive-while-assigned stuck PAST_DUE | DN-023 |
| `CanceledAt` column | SL-031 family; Wave 1 cancel-at-period-end |
| Enum TypeSpec for `final_action` / `action_type` | Nice; not required to close the loop |
| Ops: hide recovery panel on SUSPENDED | UX; update-payment already works |

---

## 4. Recommended semantics (lock this, then code)

Introduce one number, computed from the **live** campaign (LP-079 will freeze it later):

```
lastPastDueDay = max(DayOffset of steps where DayOffset >= 0), or 0 if none
terminalDay    = max(max(0, GracePeriodDays), lastPastDueDay)
```

| `GracePeriodDays` | Past-due steps | `terminalDay` |
|-------------------|----------------|---------------|
| 7 | 0, 3 | 7 |
| 3 | 0, 7 | **7** |
| 0 | 0 | 0 (dispatch 0, then terminal same tick) |
| 0 | *(none)* | 0 (first PAST_DUE tick) |
| 7 | *(none)* | 7 |
| 3 | −3 only | 3 (pre-dunning does not delay terminal) |

**Per PAST_DUE claim, in this order:**

1. Assign campaign if missing (unchanged).
2. If campaign snapshot missing (`IsActive` load miss) → return (unchanged; not this ticket).
3. Dispatch **all** due past-due steps: `DayOffset >= 0 && DayOffset <= daysOverdue` and not yet logged. Same AUTO_CHARGE / comms / `RecordReminderDispatched` as today.
4. If `daysOverdue >= terminalDay` **and** `FinalAction` is `CANCEL` or `SUSPEND` → existing terminal block (Cancel/Suspend + events + CANCEL metrics).
5. Else return. `NONE` falls through after steps; later offsets still fire on later days.

Same-day last step + terminal: step runs, log written, then status flips, then `SaveChanges` once (already per-sub at end of claim). Unique `(SubscriptionId, TargetBillingDate, DayOffset)` still prevents double dispatch.

Job-down catch-up: day 10 with `terminalDay=7` dispatches any unsent offsets `<= 10` that are on the campaign (all past-due offsets are `<= lastPastDueDay <= terminalDay`), then terminals. No new spam beyond today’s catch-up rules.

Pause: unchanged (claim filter).

**Do not** put CANCEL/SUSPEND on `DunningStep`. If `ActionType` is `CANCEL` or `SUSPEND` (or unknown), skip dispatch, still `RecordReminderDispatched` so the offset is consumed, log a warning. Prevents a bogus comms send.

---

## 5. Minimal code changes

One behavioral file plus tests. Optional two-line hygiene.

### Must

1. **`DunningEngineJob.PastDue.cs`**
   - Extract `ResolveTerminalDayOffset(grace, steps)` (internal static on the partial — unit-testable without EF).
   - Delete the grace-`if` / `return` **above** the step loop.
   - After the `foreach (dueSteps)`, if `daysOverdue >= terminalDay` and action is CANCEL/SUSPEND, run the **existing** terminal block unchanged (Cancel vs Suspend, tracked `RecordChurn` only on CANCEL, metric, typed events, internal fulfillment fan-out).
   - In the comms `else`, if action is not EMAIL / WHATSAPP / ALL (after the AUTO_CHARGE branch), skip publish.

2. **Do not change** `Subscription.Cancel` / `Suspend` signatures or status strings. They already do the job once called.

3. **Do not** add columns, migrations, TypeSpec, or ops pages.

### Should (still this ticket, tiny)

4. **`HasSubscriptionsAssignedToCampaignAsync`**: count only `Status == "PAST_DUE"`. Terminal cancel/suspend must not pin the campaign forever. In-flight definition is “still in the PAST_DUE claim set.” SUSPENDED can keep `CurrentDunningCampaignId` so `GatewayPaymentCompleted` `RecordRecovery` fallback still works (do **not** `ClearDunning` inside `Suspend()`).

5. **Ops one-liner** (optional): in `CampaignSettingsPanel`, show “Terminal on day +{max(grace, last +offset)}.” Stops merchants thinking grace 3 + step 14 cancels on day 3. No new API.

### Must not

- `ClearDunning()` inside `Suspend()` — drops recovery campaign id.
- `RecordChurn()` on SUSPEND — recoverable; LP-077/metrics, not this loop.
- New `ActionType` values in the builder.
- Changing default seed grace or +3 WHATSAPP (other IDs).
- Clock / timezone / Hangfire.

---

## 6. Tests

No `DunningEngineJob` tests exist. Mirror `BillingEngineJobTests`:

- Path: `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/DunningEngineJobTests.cs`
- In-memory `CommerceDbContext`, keyed `IEventBus` `"CommerceEventBus"`, `IConfiguration` with `Messaging:WhatsAppEnabled=false`, `Options.Create(new BackgroundWorkerOptions())`, `RunOnceAsync`.
- Helper: product + PAST_DUE sub with `NextBillingDate = UtcNow.Date.AddDays(-daysOverdue)` + `AssignDunningCampaign`.

**Formula unit tests** (no DB):

| Case | grace | offsets | expect `terminalDay` |
|------|------:|---------|---------------------:|
| seed | 7 | −3, 0, 3 | 7 |
| last after grace | 3 | 0, 7 | 7 |
| grace 0 + day 0 | 0 | 0 | 0 |
| empty past-due | 7 | −3 | 7 |
| empty all | 3 | *(none)* | 3 |
| negative grace | −1 | 5 | 5 (`max(0, grace)`) |

**Job matrix** (`RunOnceAsync`):

| Test | Setup | Assert |
|------|--------|--------|
| `Cancel_AfterLastStep_WhenLastStepAfterGrace` | grace 3, EMAIL day 7, overdue 7, CANCEL | day-7 `FulfillmentRequested` **and** `Status=CANCELED` + `SubscriptionCanceledIntegrationEvent` + `ChurnedSubscriptions==1` + `RecordDunningCancel` |
| `Cancel_DoesNotFire_BeforeLastStep` | same campaign, overdue 3 | still `PAST_DUE`; no cancel event; day-0/3 catch-up only if those steps exist |
| `Suspend_AfterLastStep_SameDayAsGrace` | grace 7, EMAIL day 3, overdue 7, SUSPEND | `SUSPENDED`, `SuspendedAt` set, **no** `RecordChurn`, `SubscriptionSuspendedIntegrationEvent` |
| `GraceZero_DispatchesDayZeroThenCancels` | grace 0, EMAIL day 0, overdue 0, CANCEL | comms **and** `CANCELED` on the same tick |
| `None_DoesNotBlockLaterSteps` | grace 3, NONE, EMAIL day 7, overdue 7 | still `PAST_DUE`; day-7 comms fired |
| `Cancel_WhenNoPastDueSteps_OnGraceDay` | grace 3, no steps, overdue 3, CANCEL | `CANCELED` (empty-timeline promise) |
| `Paused_SkipsTerminal` | overdue 10, `DunningPausedUntil=now+1d` | still `PAST_DUE` |
| `UnknownActionType_Cancel_DoesNotCallDomainCancel` | step `ActionType=CANCEL` day 0, grace 7, overdue 0, FinalAction NONE | still `PAST_DUE`; **no** `SubscriptionCanceled`; offset logged |
| `AlreadyCanceled_NotReclaimed` | control: CANCELED row with old next-billing | status unchanged (claim filter) |

**Domain (cheap, same file or `SubscriptionRecoveryTests`):**

- `Suspend_SetsSuspendedAtAndStatus`
- `Cancel_SetsCanceledStatus_DoesNotClearSuspendedAtUnlessWasSuspended` (document current: Cancel does not touch `SuspendedAt`; engine never Cancel()s a SUSPENDED row)

Do **not** require Postgres SKIP LOCKED for this ticket; in-memory claim path is the test seam.

---

## 7. Files to touch (when implementing)

| File | Change |
|------|--------|
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PastDue.cs` | Reorder; `terminalDay`; skip bogus ActionTypes |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs` | Delete-guard: PAST_DUE only |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/DunningEngineJobTests.cs` | **New** |
| `apps/lazuar-ops/src/modules/commerce/components/dunning/CampaignSettingsPanel.tsx` | Optional computed terminal day |

No migration. No TypeSpec. No `Subscription.cs` change unless a later review insists `Cancel()` should `ClearDunning()` — prefer the repository filter instead so SUSPENDED recovery metrics stay intact.

---

## 8. Acceptance (flip LP-078 to **Y** when)

1. A campaign with grace **shorter** than the last past-due step still sends that last step, then CANCEL/SUSPEND on that day.
2. Default seed (grace 7, last +3) still CANCEL on day 7 (no behavior change).
3. `GracePeriodDays=0` does not skip a day-0 step.
4. `FinalAction=NONE` never flips status and does not eat later steps.
5. `ActionType` on a step never suspends or cancels.
6. `DunningEngineJobTests` above are green.
7. Tracker cell LP-078 Lazuar **P → Y**. Do not flip DN-008’s “RecordChurn on SUSPEND” or LP-079.

---

*Read-only analysis of Commerce domain + `DunningEngineJob` as of 16 August 2026. No product code changed.*
