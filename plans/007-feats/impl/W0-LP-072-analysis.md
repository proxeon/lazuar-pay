# W0 — LP-072 analysis: Off-session AUTO_CHARGE retry

**ID:** LP-072  
**Wave:** 0 (close loops)  
**Tracker:** Off-session retry (AUTO_CHARGE) — Lazuar `P`  
**Date:** 2026-08-16  
**Codebase:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Status:** Analysis only — **do not implement from this file**  
**Feature contract:** During dunning, silently retry a **vaulted Stripe or CHIP** card. **Not Billplz.** Cap 4 attempts per billing cycle (billing owns 1; dunning owns 2–4). Success exits PAST_DUE. Failure stays in the run so a later offset can try again.

Sibling IDs (do not implement here):

| ID | Why it is adjacent, not this ticket |
|----|--------------------------------------|
| LP-047 | Honest vault story / Billplz reminder-only product copy |
| LP-052 | Billing attempt **1** actually runs |
| LP-071 | Failed renewal enters PAST_DUE + campaign assign |
| LP-073 | Email recovery sequence |
| LP-076 | Hard vs soft decline fork (Wave 3) |
| LP-078 | Grace CANCEL / SUSPEND |
| LP-079 | Campaign snapshot |
| LP-090 | Inbound webhook verify + business-key idempotency (broader than the one Stripe map this ticket needs) |

---

## 1. Done when

A merchant can add `AUTO_CHARGE` steps (or use an updated default campaign) and this is true without operator surgery:

1. `PAST_DUE` + vaulted Stripe/CHIP + due `AUTO_CHARGE` offset → insert `ChargeAttemptLog` 2..4 (`Source=DUNNING`) and publish `ExecuteOffSessionChargeIntegrationEvent` with `DunningCampaignId` + `ChargeAttemptId` + `product.GatewayName`.
2. Stripe/CHIP adapter charges the vaulted token with Commerce correlation metadata. Stripe create is idempotent on `ChargeAttemptId`.
3. Sync or async **failure** marks that attempt `FAILED`, subscription stays `PAST_DUE`, a later unused AUTO_CHARGE offset can fire.
4. **Success** (`payment_intent.succeeded` / CHIP `purchase.paid`) marks the attempt `SUCCEEDED`, `RecoverFromPayment` / `Resume`, `RecordRecovery`. Already implemented — do not rewrite.
5. **Billplz** never throws, never burns an attempt, never dead-letters the outbox. Offset is consumed (skip) so the hourly job does not spin.
6. Cap `ChargeAttemptLimits.MaxAttemptsPerBillingCycle = 4` is enforced in the engine. No 5th gateway call.
7. At most **one in-flight off-session charge** per `(SubscriptionId, TargetBillingDate)`. Do not stack attempt 3 while attempt 2 is still `PENDING` (Stripe `processing`, CHIP `pending_charge`).
8. Tests lock 1–7. There is **no** `DunningEngineJob` test today.

`P` → `Y` on the tracker only after 1–8. UI copy already promises “Stripe/CHIP, max 4, Billplz skips.” The hole is execution + tests, not marketing.

---

## 2. Sources read (this ticket)

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/00-implement-ids.md` | Wave 0 list |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/00-checklist-tracker.md` | LP-072 = `P` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/12-dunning-and-recovery.md` | Engine map; DN-019 / DN-033 / DN-034 |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob*.cs` | Claim, pre-dunning, past-due AUTO_CHARGE, dispatch |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` | Attempt 1 owner |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/ChargeAttemptLimits.cs` | Const 4 |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Entities/ChargeAttemptLog.cs` | Multi-row attempt |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Contracts/Events/ExecuteOffSessionChargeIntegrationEvent.cs` | Cross-module command event |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs` | Adapter call + failed event |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs` | `ChargeOffSessionAsync` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/{Stripe,ChipCollect,Billplz,Razorpay}GatewayAdapter.cs` | Rail behavior |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` | Attempt fail + PAST_DUE |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs` | Attempt succeed + recover |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` | `PAYMENT_FAILED` / `PAYMENT_COMPLETED` fan-out |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/{OutboxEventBus,OutboxPublisherJob,InMemoryEventBus}.cs` | At-least-once delivery |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/components/dunning/DunningStepEditor.tsx` | AUTO_CHARGE UI honesty |
| Tests under `apps/lazuar-api/tests/Lazuar.ModuleTests/{Commerce,Payments}/` | What is already locked |

---

## 3. How it works today

### 3.1 Ownership

```
BillingEngineJob          → attempt 1, Source=BILLING,   DunningCampaignId=null
DunningEngineJob AUTO_*   → attempts 2–4, Source=DUNNING, campaign + step ids
ChargeAttemptLimits       → MaxAttemptsPerBillingCycle = 4 (const, not a campaign field)
Unique index              → commerce.ChargeAttemptLogs (SubscriptionId, TargetBillingDate, AttemptNumber)
```

`BillingEngineJob` only inserts when `attemptCount == 0`. After that the due row stays `ACTIVE` until `GatewayPaymentFailed` marks `PAST_DUE`. Dunning claim is `Status = PAST_DUE` and pause expired. Pre-dunning **never** runs AUTO_CHARGE (filter is EMAIL / WHATSAPP / ALL only). That split is correct. Do not put off-session on negative offsets.

### 3.2 Past-due AUTO_CHARGE (the ticket)

`DunningEngineJob.PastDue.cs` `ProcessPastDueSubscriptionAsync`:

1. Infer method: empty `VaultedTokenId` → `MANUAL`, else `ONLINE_GATEWAY`.
2. Assign highest-priority matching **active** campaign if none pinned.
3. If `daysOverdue >= GracePeriodDays` and final is CANCEL/SUSPEND → terminal and **return** (later AUTO_CHARGE offsets never run). Default grace is **7**.
4. Due steps: `0 <= DayOffset <= daysOverdue` and no `ReminderDispatchLog` for `(TargetBillingDate, DayOffset)`.
5. For `AUTOCHARGE` or `AUTO_CHARGE`:
   - `nextAttempt = COUNT(logs for sub+targetDate) + 1`
   - Skip publish if `nextAttempt > 4` **or** missing `VaultedCustomerId` / `VaultedTokenId`
   - Else insert PENDING `ChargeAttemptLog` and publish `ExecuteOffSessionChargeIntegrationEvent`
6. **Always** `RecordReminderDispatched` after the step, including skips.

Idempotency of the **step** is DayOffset, not step GUID (`ReminderDispatchLogs` unique `(SubscriptionId, TargetBillingDate, DayOffset)`). Two steps on the same offset (EMAIL + AUTO_CHARGE) cannot both run. UI allows it; the second is filtered or unique-violates.

Catch-up is `DayOffset <= daysOverdue`. If the job was down across day 1 and day 5, one tick fires both AUTO_CHARGE steps in the same `foreach` and can insert attempt 2 **and** 3 before `SaveChanges`. Npgsql `CountAsync` in that transaction will not see the unsaved Added row, so the PENDING-stack bug is same-tick as well as cross-tick.

### 3.3 Event hop

```
DunningEngineJob  --CommerceEventBus/outbox-->  ExecuteOffSessionChargeIntegrationEvent
OutboxPublisher   --> InMemoryEventBus.PublishAsync (runtime type name)
                  --> ExecuteOffSessionChargeIntegrationEventHandler  (in-process)

Handler:
  no/inactive config     → GatewayPaymentFailed (failure_reason=gateway_not_configured)
  ChargeOffSession false → GatewayPaymentFailed (failure_reason=charge_declined)
  ChargeOffSession throw → UNCAUGHT → Commerce outbox retry / dead-letter; NO failed event
  ChargeOffSession true  → no Commerce event; wait for gateway webhook

GatewayPaymentFailed  --Payments outbox-->  GatewayPaymentFailedIntegrationEventHandler
  MarkFailed on charge_attempt_id or latest PENDING
  MarkAsPastDue + assign campaign (already PAST_DUE on retries: no re-assign, no second past_due webhook)

Webhook PAYMENT_COMPLETED --Payments outbox-->  HandleSubscriptionPaymentAsync
  RecoverFromPayment / Resume / Activate
  RecordRecovery if was PAST_DUE|SUSPENDED
  MarkSucceeded on charge_attempt_id or latest PENDING
```

`ExecuteOffSessionChargeIntegrationEvent` already has `ChargeAttemptId`. Failed-event metadata stamps it. **Adapters never receive it.** Stripe PI / CHIP purchase metadata is only `type`, `subscription_id`, `tenant_id`, `receipt`, optional `dunning_campaign_id`. Success/async-fail webhooks therefore cannot name the attempt except by “latest PENDING.”

### 3.4 Adapters

| Rail | `ChargeOffSessionAsync` | Success predicate | Fail path | Notes |
|------|-------------------------|-------------------|-----------|-------|
| **Stripe** | New PI, `OffSession=true`, `Confirm=true` | `succeeded` **or** `processing` | `StripeException` → `false` | No `IdempotencyKey`. Decline code dropped. Amount `(long)(amount * 100)`. |
| **CHIP** | GET old purchase `{tokenId}` → POST purchase → POST `charge/` with `recurring_token` | `paid` **or** `pending_charge` | HTTP/exception → `false` | Metadata on new purchase. `pending_charge` is treated as adapter success; Commerce only succeeds on `purchase.paid`. |
| **Billplz** | `throw new NotSupportedException(...)` | — | Throw | UI says “skip.” Engine still publishes if vault fields exist. |
| **Razorpay** | Recurring payment, dummy `billing@lazuar.com` / `0000000000` | payment id present | exception → `false` | **Out of LP-072.** Do not demo. Do not expand. |

Factory: `GetAdapter` throws `InvalidOperationException` for unknown types — also uncaught in the handler.

Webhook map that matters for retries:

| Event | Mapped? | Effect on AUTO_CHARGE |
|-------|---------|------------------------|
| Stripe `payment_intent.succeeded` | Yes → `PAYMENT_COMPLETED` | Recover + MarkSucceeded |
| Stripe `payment_intent.payment_failed` | **No** (falls through as raw type; webhook handler returns) | Attempt stays PENDING; if we add a PENDING guard, **later retries never fire** |
| Stripe `checkout.session.completed` | Yes | N/A for off-session (no Checkout Session) |
| CHIP `purchase.paid` | Yes → `PAYMENT_COMPLETED` | Recover |
| CHIP `purchase.payment_failure` | Yes → `PAYMENT_FAILED` | MarkFailed |

### 3.5 Defaults and UI

`GenerateDefaultDunningCampaignsCommandHandler` seeds:

| Offset | Action |
|--------|--------|
| −3 | EMAIL |
| 0 | EMAIL |
| +3 | WHATSAPP (no email body → skipped on default deploy) |
| Grace 7 | CANCEL |

**No AUTO_CHARGE.** Seed is idempotent (`HasAnyDunningCampaign` → no-op). Existing tenants are not updated by changing this handler.

Ops `DunningStepEditor`: AUTO_CHARGE card already says Stripe/CHIP, max 4, Billplz does not support off-session. Product form repeats that. Copy is ahead of the engine.

### 3.6 What tests already lock

| Test | Locks | Missing for LP-072 |
|------|-------|--------------------|
| `ChargeAttemptLogTests` | Multi-row 1–4, MarkFailed/Succeeded no-ops | Nothing (domain is fine) |
| `GatewayPaymentFailedIntegrationEventHandlerTests` | PAST_DUE + assign; fail-by-`charge_attempt_id`; no re-assign | — |
| `SubscriptionRecoveryTests` | `RecoverFromPayment` / `Resume` clear dunning | — |
| `ExecuteOffSessionChargeIntegrationEventHandlerTests` | Args to adapter; failed metadata keys; inactive config | Adapter **throw**; `charge_attempt_id` to adapter |
| `BillingEngineJobTests` | No-vault → PAST_DUE | Vaulted attempt 1 (LP-052) |
| **`DunningEngineJob` tests** | **None** | **The whole ticket** |
| Stripe `ParseWebhook` | Not unit-tested for PI failed | Map `payment_intent.payment_failed` |

---

## 4. Gaps (LP-072 only)

### P0 — retry does not actually close

| # | Gap | Why it breaks the demo |
|---|-----|------------------------|
| G1 | **No engine tests.** `RunOnceAsync` is `internal` and `InternalsVisibleTo` already includes `Lazuar.ModuleTests`. The AUTO_CHARGE branch has never been asserted. | Regressions are free. Pre-dunning inversion is a sibling smell; this ticket must not ship the same way. |
| G2 | **Default campaign has zero AUTO_CHARGE steps.** Deploy Recommended Strategy never silent-retries. | Feature is opt-in via builder only. Tracker stays `P` if we only fix the engine. |
| G3 | **Billplz `ChargeOffSession` throws.** Handler does not catch. Outbox retries then dead-letters. Attempt row stays `PENDING`. Step is already recorded as dispatched. | Any Billplz product that still has vault fields (gateway switch, bad data) poisons the outbox. UI promised skip. |
| G4 | **PENDING stacking.** Adapter `true` on Stripe `processing` / CHIP `pending_charge` does not complete Commerce. Next AUTO_CHARGE offset (same tick or next day) inserts another attempt and charges again. | Double charge. This is the money bug. |
| G5 | **Stripe `payment_intent.payment_failed` unmapped.** After `processing` → issuer fail, no `GatewayPaymentFailed`. Attempt stays PENDING forever. | With G4 fixed, the sub is **stuck**: no retry, no fail, wait for grace. |

### P1 — make the existing path safe

| # | Gap | Why |
|---|-----|-----|
| G6 | **No Stripe Idempotency-Key** on PI create. Outbox is at-least-once: handler runs, Stripe succeeds, then publisher throws before `ProcessedAt` → replay creates a second PI. | Real double-charge on a flaky publish. Key = `lazuar-offsession:{chargeAttemptId}`. |
| G7 | **`ChargeAttemptId` not passed into adapters.** Webhooks cannot correlate. Fallback “latest PENDING” is only safe if G4 holds. | Stamp metadata anyway once the port grows a last optional arg for G6. |
| G8 | **Handler does not catch adapter / factory exceptions.** Same as G3 for unknown gateway types. | One `try/catch` around `GetAdapter` + `ChargeOffSessionAsync` → publish `charge_declined` or `off_session_not_supported`. |
| G9 | **Skip vs burn-offset is one code path.** Missing token / max / Billplz still `RecordReminderDispatched`. PENDING skip must **not** record, or that offset is lost when the in-flight PI settles. | Split the skip reasons. |

### P2 — known, do not fix in this ticket

| # | Gap | Owner |
|---|-----|--------|
| Same-day EMAIL + AUTO_CHARGE | Unique DayOffset | LP-079 / later unique `(offset, action)` |
| Hard vs soft decline | Codes dropped on `StripeException` | LP-076 |
| Razorpay dummy PII | Recurring create with junk contact | Not LP-072; do not allow-list Razorpay |
| Pre-dunning inverted catch-up | `Abs(offset) <= daysUntilDue` | Not AUTO_CHARGE (pre-dunning excludes it) |
| CHIP no native idempotency | New purchase per call | Accept; G4 + G6-style attempt id in metadata is enough |
| Default seed does not update existing orgs | Idempotent no-op | Ops adds steps; no migrator |
| Recover anniversary reset | `UtcNow+interval` | Lifecycle, not retry |
| Capability matrix `SupportsOffSession` | Reserved LP-PAY-018 | Hardcode Stripe/CHIP allow-list here |

---

## 5. Minimal change set

Stay inside the existing files. No new aggregates, no campaign versioning, no decline taxonomy, no capability port.

### 5.1 Engine — `DunningEngineJob.PastDue.cs`

Keep the foreach. Change only the AUTO_CHARGE branch.

**Dispatch AUTO_CHARGE only when all of:**

1. `nextAttempt <= ChargeAttemptLimits.MaxAttemptsPerBillingCycle`
2. Both vault ids present
3. `product.GatewayName` is `STRIPE` or `CHIP` (already uppercased on `Product`)
4. **No** `ChargeAttemptLog` for this `(SubscriptionId, TargetBillingDate)` is `PENDING`
5. **No** `SUCCEEDED` attempt for this cycle (belt: webhook recovered state should have cleared PAST_DUE anyway)
6. This tick has not already published an off-session charge for this sub (in-memory flag — `CountAsync` will not see unsaved Added rows)

**Record reminder:**

| Outcome | `RecordReminderDispatched`? |
|---------|-----------------------------|
| Published charge | Yes |
| Skip: max / no vault / not Stripe|CHIP (Billplz, Razorpay, empty) | Yes — do not spin hourly |
| Skip: PENDING or already SUCCEEDED or already charged this tick | **No** — retry the offset next hour |

Do not create a `ChargeAttemptLog` on skip. Billplz must not burn 2–4.

Suggested helper (private static on the partial, or a one-liner next to the branch) — keep it in the worker, not a new domain service:

```text
SupportsOffSessionRetry(gatewayName) =>
    gatewayName is "STRIPE" or "CHIP"
```

Do not invent `IPaymentGatewayAdapter.SupportsOffSession` in this ticket.

### 5.2 Defaults — `GenerateDefaultDunningCampaignsCommandHandler`

Add two steps so Deploy Recommended Strategy actually retries **before** grace 7:

| Offset | Action | Why that day |
|--------|--------|----------------|
| +1 | AUTO_CHARGE | Day 0 is already EMAIL (unique offset). Billing already spent attempt 1. |
| +5 | AUTO_CHARGE | After +3 WA (skipped). Still `< 7` grace. |

Leave −3 / 0 EMAIL and +3 WHATSAPP alone (those are LP-073 / honesty). New orgs only.

### 5.3 Billplz adapter

`ChargeOffSessionAsync`: `return Task.FromResult(false);` plus a warning log. Keep the message string in the log. Defense in depth if a future caller forgets the allow-list.

### 5.4 Handler

`ExecuteOffSessionChargeIntegrationEventHandler`:

1. Wrap `GetAdapter` + `ChargeOffSessionAsync` in try/catch. Any exception → `PublishPaymentFailedAsync(..., "off_session_not_supported")` or `"charge_declined"` (pick **one** reason and test it; prefer `off_session_not_supported` for `NotSupportedException`, `charge_declined` for the rest).
2. Pass `ChargeAttemptId` into the adapter (new trailing optional arg).
3. Do **not** publish `GatewayPaymentCompleted` on adapter `true`. `processing` / `pending_charge` are not money. Webhook stays the success source.

### 5.5 Port + adapters

`IPaymentGatewayAdapter.ChargeOffSessionAsync` — add `Guid? chargeAttemptId = null` after `dunningCampaignId`.

| Adapter | Use of `chargeAttemptId` |
|---------|--------------------------|
| Stripe | Metadata `charge_attempt_id`; `RequestOptions.IdempotencyKey = "lazuar-offsession:{id}"` when present |
| CHIP | Metadata `charge_attempt_id` on the new purchase |
| Razorpay | Metadata/notes only (signature compile). No dummy-PII fix. |
| Billplz | Ignore (returns false) |

### 5.6 Stripe webhook — `StripeGatewayAdapter.ParseWebhookAsync`

Map `payment_intent.payment_failed` (object is `PaymentIntent`) to `EventType = PAYMENT_FAILED`, `GatewayTransactionId = pi.Id`, `Metadata = pi.Metadata`. `ProcessGatewayWebhookCommandHandler` already publishes `GatewayPaymentFailedIntegrationEvent` for that enum. Commerce already marks the attempt.

Do **not** map `invoice.payment_failed` (we do not use Stripe Billing invoices).

### 5.7 UI (optional, 5 lines)

`DunningStepEditor` AUTO_CHARGE card: “One action per day-offset. Put email on a different day than auto-retry.” Not required to flip the tracker cell.

No TypeSpec change. No migration. `ChargeAttemptLimits` stays `4`.

---

## 6. Tests (required)

Mirror `BillingEngineJobTests`: in-memory `CommerceDbContext`, keyed `CommerceEventBus` substitute, `DunningEngineJob.RunOnceAsync`. `IConfiguration` = empty builder (WhatsApp flag false). Internals already visible.

New file:  
`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/DunningEngineJobTests.cs`

### 6.1 Engine matrix

| Test | Arrange | Assert |
|------|---------|--------|
| `PastDue_VaultedStripe_AutoChargeDue_PublishesAttempt2` | PAST_DUE, vault, campaign +1 AUTO_CHARGE, existing BILLING attempt 1 FAILED, `NextBillingDate` yesterday | `ExecuteOffSessionCharge` once; `GatewayName=STRIPE`; `DunningCampaignId` set; `ChargeAttemptId` = new DUNNING row attempt **2**; reminder logged |
| `PastDue_VaultedChip_UsesProductGatewayName` | Same, product `CHIP` | Event `GatewayName=CHIP` |
| `PastDue_Billplz_DoesNotPublish_AndConsumesOffset` | Vault present, product `BILLPLZ`, AUTO_CHARGE due | No event; **no** new ChargeAttemptLog; reminder logged |
| `PastDue_Razorpay_DoesNotPublish` | Product `RAZORPAY` | Same as Billplz (allow-list) |
| `PastDue_NoVault_DoesNotPublish_ConsumesOffset` | Empty tokens | No event; reminder logged |
| `PastDue_MaxAttempts_DoesNotPublish` | 4 existing logs | No event; reminder logged |
| `PastDue_PendingAttempt_DoesNotPublish_DoesNotConsumeOffset` | Attempt 2 PENDING | No event; **no** new reminder for that offset |
| `PastDue_TwoAutoChargeOffsetsDue_OnlyOneChargeThisTick` | +1 and +5 both due, no PENDING | Exactly **one** event / one new attempt; only the earlier offset recorded |
| `PastDue_AlreadyDispatchedOffset_IsIdempotent` | Reminder log for +1 | No second event |
| `PastDue_Paused_NotClaimed` | `DunningPausedUntil` future | No event |
| `PastDue_GraceReached_SkipsRemainingAutoCharge` | Grace 3, daysOverdue 3, AUTO_CHARGE on 5 | CANCEL/SUSPEND path; no charge |
| `PreDunning_DoesNotAutoCharge` | ACTIVE, due in 3 days, step −3 AUTO_CHARGE (if someone stored it) | No `ExecuteOffSessionCharge` |

Helper: create product + campaign + `AddStep` the same way `DunningCampaignDomainTests` does.

### 6.2 Handler

Extend `ExecuteOffSessionChargeIntegrationEventHandlerTests`:

- Adapter throws `NotSupportedException` → publishes failed (`off_session_not_supported` or the single chosen reason); does **not** rethrow.
- Adapter throws generic `InvalidOperationException` (factory miss) → publishes failed.
- Success path: `ChargeOffSessionAsync` received `chargeAttemptId` (new arg index). Update existing NSubstitute `Arg.Any<Guid?>()` call.

### 6.3 Billplz adapter

`BillplzGatewayAdapterTests`: `ChargeOffSessionAsync` returns `false`, does not throw.

### 6.4 Stripe parse (small)

If adding a focused test is cheap (signed fixture is not cheap): extract the `payment_intent.payment_failed` → `PAYMENT_FAILED` mapping into a package-visible helper, **or** add a ProcessGatewayWebhook test where the mocked adapter returns `PAYMENT_FAILED` with PI metadata containing `subscription_id` + `charge_attempt_id` — that path is already handler-tested. Prefer **not** to stand up Stripe signature crypto in this ticket.

Minimum: a comment in the Stripe adapter next to the new branch and the Commerce failed-handler test already covering `charge_attempt_id`. Optional: unit-test a new internal `StripeWebhookEventMapper` if the parse method is too fat to touch safely — only if you split; do not split “for cleanliness.”

### 6.5 Domain / defaults

- Keep `ChargeAttemptLogTests` as-is (already max 4).
- Add one test on `GenerateDefaultDunningCampaignsCommandHandler`: empty org → steps include AUTO_CHARGE at +1 and +5; second call is still no-op.

### 6.6 Do not add

- Live Stripe/CHIP HTTP.
- Razorpay recurring success.
- Hard-decline tables.
- Full in-process outbox soak.

---

## 7. Suggested touch list (implementation later)

| File | Change |
|------|--------|
| `Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PastDue.cs` | Allow-list, PENDING/SUCCEEDED/one-per-tick, split reminder |
| `Modules/Commerce/Application/Commands/DunningCampaignCommandHandlers.cs` | Default +1 / +5 AUTO_CHARGE |
| `Modules/Payments/Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs` | try/catch; pass attempt id |
| `Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs` | Optional `chargeAttemptId` |
| `Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` | Metadata + idempotency; map PI failed |
| `Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` | Metadata attempt id |
| `Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs` | Signature + notes only |
| `Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs` | Return false |
| `tests/.../Commerce/Workers/DunningEngineJobTests.cs` | **New** |
| `tests/.../Payments/ExecuteOffSessionChargeIntegrationEventHandlerTests.cs` | Throw + new arg |
| `tests/.../Payments/BillplzGatewayAdapterTests.cs` | No throw |
| `tests/.../Commerce/DunningCampaignDomainTests.cs` or command-handler test | Default steps |

Do not touch `ChargeAttemptLimits.cs` unless a test wants to reference the const (it should).

---

## 8. Sequence after the minimal fix

```
PAST_DUE Stripe/CHIP, day >= AUTO_CHARGE offset, attemptCount < 4, no PENDING
  DunningEngineJob
    insert ChargeAttemptLog #N PENDING Source=DUNNING
    publish ExecuteOffSessionCharge (campaign + attempt + gateway)
    RecordReminderDispatched
    SaveChanges (outbox + attempt + reminder)

  Outbox → handler
    Stripe PI confirm (Idempotency-Key=lazuar-offsession:{attemptId})
      or CHIP charge/recurring_token
    false / throw → GatewayPaymentFailed → MarkFailed; stay PAST_DUE
    true (succeeded|processing|pending_charge) → wait

  Webhook
    PI succeeded / purchase.paid → RecoverFromPayment + MarkSucceeded + RecordRecovery
    PI payment_failed / purchase.payment_failure → MarkFailed; stay PAST_DUE
      next unused AUTO_CHARGE offset (or same offset if we did not consume — only PENDING case) can fire
```

Billplz / no vault / Razorpay: consume offset, zero gateway calls.

---

## 9. Residual risk after this ticket

- CHIP replay of the **same** `ExecuteOffSessionCharge` outbox row can still create two purchases (no CHIP idempotency API). Stripe is safe via Idempotency-Key. Accept for Wave 0.
- If Stripe returns `succeeded` and the webhook never arrives, G4 leaves a permanent PENDING and **stops** further AUTO_CHARGE. CS: record-payment or wait for webhook config. Do not publish completed from the handler (double `Activate` would shift the anniversary twice).
- Existing tenants keep email-only defaults until ops adds AUTO_CHARGE. Document in the implement PR, not a data backfill.
- Same-day email + charge still impossible. Use adjacent offsets.
- Stolen-card retries still execute if the campaign says so (LP-076).

---

## 10. Verdict

LP-072 is **partially built**: schema, attempt cap, past-due publish, failed/completed handlers, and ops copy already describe the feature. It is not sellable as “we retry the card” because (1) defaults never schedule a retry, (2) Billplz can throw, (3) in-flight `processing`/`pending_charge` can double-charge, (4) Stripe async failure cannot fail the attempt, (5) there are zero engine tests.

The smallest honest close is: **allow-list Stripe/CHIP, one in-flight attempt, Billplz returns false, catch handler exceptions, Stripe PI failed + idempotency, default +1/+5 AUTO_CHARGE, `DunningEngineJobTests`.** That is this ticket. Everything else is a neighbor ID.
