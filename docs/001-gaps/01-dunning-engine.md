<!-- Source subagent: 019fc650-3511-7762-8927-4ef164cae184 -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# Dunning Engine Gap Analysis

**Scope:** Lazuar Hub monorepo (`/Users/akmalfirdaus/Code/lazuar/lazuar-hub`)  
**Focus:** Commerce dunning domain, worker, API, ops UI, Payments off-session charges, Communications/Messaging delivery, migrations, tests  
**Product promise (from ADRs):** Native WhatsApp dunning + automated revenue recovery as a core CaaS differentiator  

---

## Inventory of Implementation

### Domain layer
| Artifact | Path |
|---|---|
| Campaign aggregate | `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/DunningCampaign.cs` |
| Step entity | `apps/lazuar-api/Modules/Commerce/Domain/Entities/DunningStep.cs` |
| Subscription dunning state | `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs` |
| Dispatch idempotency log | `apps/lazuar-api/Modules/Commerce/Domain/Entities/ReminderDispatchLog.cs` |
| Charge attempt log | `apps/lazuar-api/Modules/Commerce/Domain/Entities/ChargeAttemptLog.cs` |

### Application / contracts
| Artifact | Path |
|---|---|
| Campaign commands | `.../Contracts/Commands/DunningCampaignCommands.cs` |
| Subscriber pause/resume commands | `.../Contracts/Commands/ManageSubscriberDunningCommands.cs` |
| Campaign handlers | `.../Application/Commands/DunningCampaignCommandHandlers.cs` |
| Pause/resume handlers | `.../Application/Commands/ManageSubscriberDunningCommandHandlers.cs` |
| Repository interface | `.../Application/ICommerceRepository.cs` |
| Query interface | `.../Application/Queries/ICommerceQueryService.cs` |

### Infrastructure
| Artifact | Path |
|---|---|
| Engine worker | `.../Infrastructure/Workers/DunningEngineJob.cs` |
| Billing worker (PAST_DUE entry) | `.../Infrastructure/Workers/BillingEngineJob.cs` |
| Endpoints | `.../Infrastructure/Endpoints/DunningCampaignEndpoints.cs`, `SubscriberEndpoints.cs` |
| Query read model | `.../Infrastructure/Services/CommerceQueryService.Dunning.cs` |
| Subscriber DTO projection | `.../Infrastructure/Services/CommerceQueryService.Subscribers.cs` |
| Repository | `.../Infrastructure/Repositories/CommerceRepository.cs` |
| EF config | `.../Infrastructure/CommerceDbContext.cs` |
| Recovery on payment success | `.../Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs` |
| Auto-seed defaults | `.../Infrastructure/EventHandlers/DefaultTemplatesSeededIntegrationEventHandler.cs` |
| Migrations | `20260629175822_AddDunningEngine.cs`, `20260704163126_RefactorDunningEngine.cs` |

### Payments / Communications / Messaging
| Artifact | Path |
|---|---|
| Off-session event | `Modules/Payments/Contracts/Events/ExecuteOffSessionChargeIntegrationEvent.cs` |
| Off-session handler | `Modules/Payments/Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs` |
| Failed-payment event (orphaned) | `Modules/Payments/Contracts/Events/GatewayPaymentFailedIntegrationEvent.cs` |
| Webhook processor (ignores failures) | `Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` |
| Stripe/CHIP off-session | Gateway adapters under `Modules/Payments/Infrastructure/Gateways/` |
| Dunning message hydrate | `Modules/Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs` |
| Channel send + credit deduct | `Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs` |

### Frontend (ops)
| Artifact | Path |
|---|---|
| List page | `apps/ops-page/src/modules/commerce/pages/DunningCampaignsPage.tsx` |
| Builder | `.../pages/CampaignBuilderPage.tsx` |
| Components | `.../components/dunning/*` |
| Subscriber recovery panel | `.../pages/SubscribersPage.tsx` |
| Routes | `apps/ops-page/src/App.tsx` |

### API contract
| Artifact | Path |
|---|---|
| TypeSpec models/routes | `packages/api-spec/modules/commerce/models.tsp`, `admin-routes.tsp` |
| Generated TS/C# | `packages/api-types-ts`, `packages/api-types-dotnet` |

### Tests
- Only smoke: `apps/lazuar-api/tests/Lazuar.IntegrationTests/CommerceQueryServiceTests.cs` calls `GetDunningCampaignsAsync` and asserts no throw.
- **No unit/integration tests for `DunningEngineJob`, campaign mutation, recovery metrics, or failure → PAST_DUE path.**

### Evolution history (important)
1. **Initial** (`AddDunningEngine`): campaigns + steps with `TemplateId` + `Channel`; backfill from legacy `ReminderSchedules`; subscription fields `CurrentDunningCampaignId`, `CurrentDunningStepIndex`, `DunningPausedUntil`.
2. **Refactor** (`RefactorDunningEngine`): dropped template FK; inlined `Subject`/`EmailBody`/`WhatsAppBody`/`ActionType`; added `PriorityOrder`, recovery counters, `SuspendedAt`.

---

## Domain Model Analysis

### `DunningCampaign` — what works

```38:112:apps/lazuar-api/Modules/Commerce/Domain/Aggregates/DunningCampaign.cs
public DunningCampaign(..., string finalAction, int gracePeriodDays, int priorityOrder = 0,
    IEnumerable<Guid>? targetProductIds = null, IEnumerable<string>? targetPaymentMethods = null)
{
    FinalAction = string.IsNullOrWhiteSpace(finalAction) ? "NONE" : finalAction.ToUpperInvariant();
    // ...
}
public void AddStep(int dayOffset, string actionType, string? subject, string? emailBody, string? whatsAppBody)
public void RecordRecovery(decimal amount)
public void RecordChurn()
public void Archive() / Restore()
```

**Works:**
- Tenant-scoped campaign aggregate with targeting (product IDs + payment method strings as jsonb lists).
- Priority ordering for multi-campaign selection.
- Terminal actions: `CANCEL` / `SUSPEND` / `NONE` (via empty → `NONE`).
- In-memory metrics counters for recovered revenue / saved / churned.
- Soft archive via `IsActive`.

**Gaps / flaws:**
1. **No domain invariants** on `FinalAction`, `ActionType`, `DayOffset` uniqueness, or grace period vs max step day. Any string is accepted.
2. **Steps are fully replaced on update** (`ClearSteps` + re-`AddStep`) → new GUIDs every save. Engine idempotency keys off `step.Id` (`ReminderDispatchLog.ScheduleId`). Editing a live campaign can **re-dispatch every step** for still-overdue subscriptions, or orphan old logs.
3. **`RecordRecovery` is never linked to subscription identity** — pure counters; no audit trail of *which* sub recovered, when, or via which channel/step.
4. **`RecordChurn` only on CANCEL**, not on SUSPEND terminal action (asymmetric metrics).
5. **No versioning / immutability** of running campaign instances. Industry tools freeze the campaign version assigned at dunning start so mid-flight edits do not rewrite the journey.

### `DunningStep` — what works

```6:32:apps/lazuar-api/Modules/Commerce/Domain/Entities/DunningStep.cs
/// <summary>EMAIL, WHATSAPP, or AUTO_CHARGE</summary>
public string ActionType { get; private set; }
// DayOffset, Subject, EmailBody, WhatsAppBody
```

**Works:** Simple value carrier for day-offset actions with inlined copy.

**Gaps:**
1. **Comment claims EMAIL/WHATSAPP/AUTO_CHARGE**, but engine also matches `ALL` and `AUTOCHARGE` (typo alias). No enum / value object.
2. **One action per step** — cannot “Email + WhatsApp on day 3” without two steps same day; engine uses `FirstOrDefault` so **only one step fires per day offset**.
3. **No channel metadata** (template category, WhatsApp template name, interactive buttons, CTA URL).
4. **No retry policy fields** on `AUTO_CHARGE` (max attempts, backoff, decline-code rules).
5. **No time-of-day** (legacy `ReminderSchedules.TimeOfDay` was dropped and never replaced).

### `Subscription` dunning state — what works

```21:23:apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs
public Guid? CurrentDunningCampaignId { get; private set; }
public int CurrentDunningStepIndex { get; private set; }
public DateTime? DunningPausedUntil { get; private set; }
```

**Works:**
- Assign / clear / pause / resume APIs exist on the aggregate.
- `Resume` and payment success clear dunning.
- Reminder logs collection for dispatch idempotency.

**Critical dead / misleading state:**
1. **`CurrentDunningStepIndex` is never advanced.** `AdvanceDunningStep()` exists but has **zero call sites** outside the aggregate itself. Engine matches by `DayOffset == daysOverdue`, not by index. Ops UI still shows “Step N” from this dead field → always `0` after assign.
2. **Campaign assignment is sticky** until clear/resume — if campaign is deleted, engine does `if (campaign == null) continue` and **leaves sub stuck in PAST_DUE forever** with no terminal action.
3. **No explicit “dunning started at” timestamp** — overdue days always computed from `NextBillingDate`, so if billing date is wrong, entire schedule shifts.
4. **`Activate` when already PAST_DUE preserves period dates** (good for arrears), but path depends on payment handler calling Activate/ClearDunning correctly (often broken for off-session — see below).

### Schema / EF notes
- `ChargeAttemptLogs` unique index: `(SubscriptionId, TargetBillingDate)` — **one row per billing cycle**, not per attempt.
- `ReminderDispatchLogs` unique: `(SubscriptionId, ScheduleId, TargetBillingDate)`.
- Campaign targets stored as **jsonb arrays** (no relational join) → hard to query “which campaigns cover product X” efficiently; empty = global.

---

## Job/Worker Execution Analysis

### Architecture
Both `BillingEngineJob` and `DunningEngineJob` are `BackgroundService` loops with **`Task.Delay(1 hour)`**, registered in `Commerce.Infrastructure.DependencyInjection`.

```45:49:apps/lazuar-api/Modules/Commerce/Infrastructure/DependencyInjection.cs
services.AddHostedService<BillingEngineJob>();
services.AddHostedService<DunningEngineJob>();
```

They use `IgnoreQueryFilters()` and load **all tenants’** active campaigns / candidates into memory each hour.

### Intended lifecycle (as coded)

```
ACTIVE + vaulted token, due
  → BillingEngineJob: ChargeAttemptLog + ExecuteOffSessionCharge (DunningCampaignId=null)
ACTIVE + no token, due
  → BillingEngineJob: MarkAsPastDue + fulfillment "subscription.suspended" (mislabeled)
ACTIVE, within 14d of due
  → DunningEngineJob pre-dunning: negative DayOffset EMAIL/WHATSAPP/ALL
PAST_DUE + not paused
  → Assign campaign by priority + product + payment method
  → Match step where DayOffset == daysOverdue
  → AUTO_CHARGE or DispatchCommunicationStep (FulfillmentRequested reminder.dunning)
  → After GracePeriodDays: CANCEL/SUSPEND + fulfillment webhooks
Payment success (webhook)
  → Clear dunning / Resume + optional RecordRecovery if metadata has dunning_campaign_id
```

### What currently works
- Hourly background sweep exists and is wired.
- Pre-due reminders for ACTIVE subs (negative offsets) with dispatch logs.
- PAST_DUE campaign assignment with priority + targeting.
- Communication dispatch via event bus → Communications → Messaging.
- Terminal CANCEL/SUSPEND with product fulfillment fan-out.
- Pause until datetime respected for PAST_DUE processing.
- Default campaign seed on tenant template bootstrap and via ops “Deploy Recommended Strategy”.

### P0 correctness gap: online failed charges never enter dunning

`BillingEngineJob` only marks PAST_DUE when **no vaulted token**:

```69:125:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs
if (!string.IsNullOrEmpty(sub.VaultedTokenId) && !string.IsNullOrEmpty(sub.VaultedCustomerId))
{
    // log attempt + ExecuteOffSessionCharge — does NOT change Status
}
else
{
    sub.MarkAsPastDue();
    // publishes eventType "subscription.suspended" with status "PAST_DUE"  ← wrong event name
}
```

`ExecuteOffSessionChargeIntegrationEventHandler` on failure **only logs**:

```47:50:apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs
if (!success)
{
    _logger.LogError("Off-session charge failed...");
}
```

`GatewayPaymentFailedIntegrationEvent` is defined but:
- **never published**
- **never subscribed**
- Webhook processor **explicitly ignores** non-completed events:

```55:58:apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs
if (parsedResult.EventType != "PAYMENT_COMPLETED" && parsedResult.EventType != "DISPUTE_CREATED")
{
    return;
}
```

CHIP maps `purchase.payment_failure` → `PAYMENT_FAILED`, then it is dropped.

**Result:** For the primary SaaS path (vaulted Stripe/CHIP), a declined renewal leaves the sub **ACTIVE with past `NextBillingDate`**, charge attempt logged once, **no PAST_DUE, no campaign assign, no WhatsApp/email dunning, no auto-retry steps, no grace cancel.**  
This directly contradicts ADR promises (`docs/architecture-decision-log/014-apps.md` lines about `GatewayPaymentFailed → dunning`, `020` WhatsApp dunning, `021` keep WhatsApp dunning).

Manual/offline path (no token) *does* enter PAST_DUE and can be dunned — so dunning mostly works for “reminder-only / FPX-manual” segments, not for card auto-debit failures.

### P0: auto-retry count is structurally impossible

Engine intends max 4 retries:

```180:182:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs
var attemptCount = await db.ChargeAttemptLogs.CountAsync(
    l => l.SubscriptionId == sub.Id && l.TargetBillingDate == sub.NextBillingDate.Value.Date, ct);
if (attemptCount < 4 && ...)
```

But EF enforces:

```193:198:apps/lazuar-api/Modules/Commerce/Infrastructure/CommerceDbContext.cs
builder.HasIndex(x => new { x.SubscriptionId, x.TargetBillingDate }).IsUnique();
```

After billing’s first attempt row exists, any dunning `AUTO_CHARGE` that `Add`s another log for the same date will throw unique violation on `SaveChanges`, potentially **failing the whole batch** for that tick (or succeeding only if that path is skipped). UI copy promises “max 4 attempts”; DB allows 1.

### P0/P1: off-session success does not rehydrate subscription correctly

Off-session Stripe metadata:

```231:232:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs
var meta = new Dictionary<string, string> { { "receipt", receipt } };
if (dunningCampaignId.HasValue) meta["dunning_campaign_id"] = dunningCampaignId.Value.ToString();
```

`receipt` = subscription id string, but Commerce success handler requires:

```39:39:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs
if ((type == "commerce_subscription" || type == "custom_payment_link")
    && Metadata.TryGetValue("subscription_id", ...))
```

Off-session PI success path (`payment_intent.succeeded`) therefore often **does not advance billing / clear dunning / record recovery**, unless customer pays via update-payment checkout (which *does* set `type` + `subscription_id` + optional `dunning_campaign_id` in `PublicEndpoints`).

Recovery metrics in the campaigns table are therefore **systematically undercounted** for silent auto-charges, and may fail to reactivate at all without a second webhook path.

### Matching model rigidity & miss risks

1. **Exact calendar-day match only:** `DayOffset == daysOverdue` (or `|DayOffset| == daysUntilDue` for pre-dunning). If the hourly job is down across that calendar day, **the step is permanently skipped** (no catch-up “step due if DayOffset ≤ daysOverdue and not dispatched”).
2. **`FirstOrDefault` per day** → second step same day never runs.
3. **Pre-dunning window hardcoded 14 days** in query; UI allows -14, so OK for UI, but not configurable per campaign.
4. **No timezone:** all `Date` math is UTC. SEA merchants (MYT UTC+8) get day-boundary skew on “due today”.
5. **No time-of-day / quiet hours.**
6. **Loads entire campaign set and all candidate subs every hour** — no paging, no leader election; multi-replica deploys risk **double dispatch** (mitigated only by unique indexes on logs, race still possible between check and insert).
7. **Save is one big `SaveChanges` at end** — partial event publish + failed save = possible double-send next hour for events already outboxed vs not (depends on outbox transaction boundaries with same DbContext — outbox likely same unit of work if using OutboxEventBus, but charge logs + events coupled).
8. **Hardcoded `attemptCount < 4`** not campaign-configurable.
9. **Billing does not re-attempt** after first failure (attemptExists short-circuit) — dunning was meant to own retries, but PAST_DUE never set for vaulted failures.

### Terminal action quirks
- On grace exceed: CANCEL records churn; SUSPEND does not.
- After terminal action, `continue` without clearing `CurrentDunningCampaignId` on CANCEL (canceled sub no longer selected by PAST_DUE query — OK). SUSPENDED also not PAST_DUE — **suspended subs never receive further recovery messages** unless they pay via update-payment (which allows SUSPENDED).
- Final action runs **every hour** while still PAST_DUE and overdue ≥ grace? After Cancel/Suspend status changes so they leave the query — OK. If `FinalAction == NONE`, sub stays PAST_DUE forever, steps only fire once per day offset — OK but silent.

### Event naming bug in billing
No-token past-due path publishes `subscription.suspended` while payload status is `PAST_DUE`. Downstream bots may revoke access immediately instead of at true suspend/cancel.

---

## API Surface Analysis

### Campaign CRUD
| Method | Route | Behavior |
|---|---|---|
| GET | `/admin/commerce/dunning-campaigns` | List + steps via Dapper JSON aggregation |
| POST | `/admin/commerce/dunning-campaigns` | Create |
| PUT | `/admin/commerce/dunning-campaigns/{id}` | Full replace details + steps |
| DELETE | `/admin/commerce/dunning-campaigns/{id}` | Hard delete (cascade steps) |
| POST | `/admin/commerce/dunning-campaigns/defaults` | Insert “Standard Recovery Strategy” |

### Subscriber ops
| Method | Route |
|---|---|
| POST | `/admin/commerce/subscribers/{id}/dunning/pause` |
| POST | `/admin/commerce/subscribers/{id}/dunning/resume` |

### What works
- TypeSpec-first contracts; ops client uses generated paths.
- Priority, targets, grace, final action, inline step bodies exposed.
- Pause/resume for ops CS tooling.
- Public update-payment checkout injects `dunning_campaign_id` for recovery attribution when sub already has campaign.

### API gaps
1. **No GET by id** — builder loads *all* campaigns client-side and finds by id.
2. **No assign-campaign-to-subscriber** endpoint — assignment is engine-only, opaque.
3. **No force-run step / dry-run / preview schedule**.
4. **No dunning analytics endpoints** (step conversion, recovery rate over time) — only aggregate counters on campaign DTO.
5. **No validation errors as ProblemDetails for business rules** — handlers throw `InvalidOperationException`.
6. **Delete while in use** not blocked — orphans `CurrentDunningCampaignId`.
7. **Generate defaults** can create duplicates every click (no uniqueness check except bootstrap `hasCampaigns`).
8. **Dead repository method** `GetDefaultTemplateIdsAsync` still queries communications templates by hard-coded names — unused after inline-copy refactor (drift).
9. **`current_dunning_step` in DTO is index, not day/step identity** — misleading for ops.
10. **No webhook/event for “dunning.step_dispatched”** for merchant observability.

---

## Frontend Ops Surface Analysis

### What works
- Empty state with “Deploy Recommended Strategy” UX aligned with product marketing.
- Campaign list shows priority, step count, terminal action, grace, recovered RM, saved/churned, active flag.
- Builder: identity, priority, product multi-select, payment method ONLINE_GATEWAY/MANUAL, final action, grace, timeline editor.
- Step editor: EMAIL / WHATSAPP / AUTO_CHARGE, fixed day-offset dropdown, live preview via communications preview API.
- Subscribers panel for PAST_DUE: campaign name, step index, pause/resume.

### Flexibility / UX gaps
1. **Day offsets fixed to a hard-coded select** (`-14,-7,-3,-1,0,1,3,5,7,14,30`) — not freeform despite domain accepting any int; API allows more than UI.
2. **No multi-channel single step**, no SMS, no “ALL”.
3. **AUTO_CHARGE messaging** claims 4 retries — false given unique index.
4. **Variable placeholders advertise `{{plan_name}}`** but Communications hydrator **does not replace `{{plan_name}}`** for dunning (see next section) → merchants send literal `{{plan_name}}`.
5. **Currency hardcoded “RM”** on recovered revenue display regardless of tenant currency.
6. **No simulation**, no per-subscriber timeline of what already sent.
7. **Step index display always wrong** (backend dead field).
8. **Delete is hard delete** with only `window.confirm`.
9. **No archive-only flow** separate from is_active checkbox on edit.
10. **Target payment methods** only two booleans — no gateway-specific (Stripe vs Billplz) strategies.

---

## Integration with Payments & Communications

### Payments

| Capability | Status |
|---|---|
| Off-session charge event | Implemented |
| Stripe off-session | Implemented (confirm intent) |
| CHIP off-session | Implemented (new purchase + charge token) |
| Razorpay off-session | Partial (hardcoded email/contact) |
| Billplz off-session | `NotSupportedException` |
| Failure → Commerce | **Missing** |
| Success metadata for off-session | **Incomplete** (`receipt` only, not `type`/`subscription_id`) |
| Decline-code smart retry | **Missing** |
| Partial auth / SCA handling | **Missing** (Stripe off_session failures not special-cased) |

Industry comparison: Stripe Billing Smart Retries use ML on decline codes, card networks, and time-of-day. Chargebee/Recurly expose configurable retry schedules with gateway response awareness. Lazuar has a **day-offset AUTO_CHARGE step** with no intelligence and a broken counter.

### Communications / Messaging (“Native WhatsApp Dunning”)

Flow that *does* work when PAST_DUE steps fire:

1. `DunningEngineJob.DispatchCommunicationStepAsync` → `FulfillmentRequestedIntegrationEvent(COMMUNICATIONS, "reminder.dunning", payload with bodies)`.
2. `FulfillmentRequestedIntegrationEventHandler` loads CRM profile + workspace, builds portal/update-payment links, substitutes variables, publishes `DispatchMessageIntegrationEvent`.
3. Messaging handler sends email (tenant Resend key) and/or WhatsApp (credit-gated).

**What works:**
- End-to-end channel plumbing exists.
- WhatsApp credits / suppression / tenant email config integrated.
- Update payment deep link generation.

**Gaps vs product promise:**
1. **`{{plan_name}}` not substituted** — only:

```87:97:apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs
.Replace("{{customer_name}}", ...)
.Replace("{{customer_email}}", ...)
.Replace("{{customer_phone}}", ...)
.Replace("{{business_name}}", ...)
.Replace("{{renewal_link}}", portalLink)
.Replace("{{portal_magic_link}}", portalLink)
.Replace("{{update_payment_link}}", updatePaymentLink);
// no plan_name, amount_due, days_overdue, failure_reason, etc.
```

   Default campaign bodies literally include `{{plan_name}}`.

2. **No product name in dunning payload** — engine does not pass plan/amount/currency into the event.
3. **WhatsApp is plain text**, not Interactive Buttons / template messages as described in ADR 020 (“Tap here to pay RM50 via FPX” inside chat).
4. **No Meta utility template registration / category** — production WhatsApp Business API typically requires approved templates for business-initiated dunning outside 24h session.
5. **Insufficient credits silently drop WhatsApp** (email may still send); no fallback channel, no ops alert.
6. **Channel `ALL` supported in messaging** but step editor does not offer it; migration mapped old ALL → EMAIL.
7. **Dunning copy is denormalized into steps**, diverging from Communications `MessageTemplates` library — dual CMS, no shared versioning, “Templates” page not used by dunning after refactor.

### Recovery path that works
Manual update-payment checkout (`PublicEndpoints`) for PAST_DUE/SUSPENDED → metadata includes subscription + dunning campaign → on complete, arrears cleared + `RecordRecovery`.

---

## Flexibility & Extensibility Gaps

| Area | Current | Industry (Stripe / Chargebee / Recurly) | Gap |
|---|---|---|---|
| Entry trigger | PAST_DUE status + day math | Invoice past due / payment_failed events | Missing payment_failed bridge |
| Schedule model | Fixed day offsets | Custom schedules + smart retries | No hourly/custom, no catch-up |
| Retry intelligence | None | Decline-code ML / card updater | Hardcoded day AUTO_CHARGE |
| Channels | Email, WA, charge | Email, SMS, in-app, dunning portal | No SMS; WA not interactive |
| Templates | Inline strings | Template library + localization | Broken vars; no i18n |
| Segmentation | Product + ONLINE/MANUAL | MRR, plan, region, risk | Very coarse |
| Timezone | UTC dates | Customer/merchant TZ | Missing |
| Campaign versioning | Live mutate steps | Immutable run instances | Re-fire / drift risk |
| Actions | Cancel/Suspend/None + msg/charge | Pause, restrict, coupon, collect | No mid-funnel actions |
| Analytics | 3 counters | Funnel, recovery rate, $ by step | Weak attribution |
| Ops control | Pause until | Skip step, force charge, notes | Minimal |
| Multi-currency / amount | Product.Price only | Open invoice balance | No proration/invoice model |
| Concurrency | Single process assumption | Leader election / queue | Multi-instance risk |

**Hardcoded rigidities (concrete):**
- Job interval: 1 hour.
- Pre-dunning horizon: 14 days.
- Max charge attempts: 4 (and DB makes it 1).
- Payment method inference: `VaultedTokenId` empty → `MANUAL` else `ONLINE_GATEWAY` only.
- Action type string matching with aliases (`AUTOCHARGE` vs `AUTO_CHARGE`).
- Portal base URL hard-coded `https://portal.lazuar.com/...` in communications handler.
- Default campaign: 7-day grace, CANCEL, steps -3 EMAIL, 0 EMAIL, +3 WHATSAPP (no AUTO_CHARGE in default).

---

## Reliability & Correctness Gaps

### Critical (revenue engine broken for core path)
1. **Vaulted payment failure never sets PAST_DUE** → dunning engine never runs for card decline path.
2. **`GatewayPaymentFailedIntegrationEvent` is dead code**; webhooks drop `PAYMENT_FAILED`.
3. **ChargeAttemptLogs unique index vs multi-retry logic** conflict.
4. **Off-session success metadata incomplete** → subscription may not renew/clear dunning/record recovery.
5. **`{{plan_name}}` never filled** → broken customer messaging in default + UI placeholders.

### High
6. **Exact day matching without catch-up** → missed steps on outage.
7. **Campaign edit regenerates step IDs** → duplicate customer spam or lost idempotency semantics.
8. **Delete campaign leaves stuck PAST_DUE** without terminal path.
9. **`CurrentDunningStepIndex` dead** → wrong ops visibility.
10. **Billing publishes wrong event type** (`subscription.suspended` for PAST_DUE).
11. **No distributed lock** → multi-pod double work (partially mitigated by unique indexes; races still possible).
12. **One SaveChanges for all tenants** — one bad row can fail the tick for everyone (depending on exception handling — outer try/catch logs and continues next hour, but partial progress depends on when exception occurs).

### Medium
13. **`RecordChurn` not on SUSPEND**; recovery requires metadata.
14. **SUSPENDED excluded from dunning continuation** (by design?) — no “pay to restore” messaging after suspend unless user finds portal.
15. **No idempotent handling of concurrent pause + engine**.
16. **Generate defaults unbounded duplicates** from API.
17. **Razorpay off-session** uses dummy contact/email — likely production-broken.
18. **Billplz** cannot auto-charge; dunning for Billplz must be manual payment method only — product/docs should say so.

### Domain design debt
19. Dunning is **status+day driven**, not **invoice/attempt driven**. There is no first-class `Invoice` or `PaymentIntent` entity for the subscription cycle — only `NextBillingDate` + logs. That limits flexibility (partial payments, multiple open invoices, mid-cycle plan change).
20. Pre-dunning and post-dunning share the same campaign/step list — good — but pre-dunning ignores pause and campaign assignment (matches any active campaign by targeting each hour), while post-dunning pins campaign. Inconsistent.

---

## Testing Gaps

| Area | Coverage |
|---|---|
| GetDunningCampaigns query smoke | Yes (integration) |
| Create/update/delete campaign | No |
| Default generate | No |
| Pause/resume | No |
| DunningEngineJob pre/post due matching | No |
| PAST_DUE transition on charge failure | No |
| AUTO_CHARGE + ChargeAttemptLog constraints | No |
| Recovery RecordRecovery path | No |
| Template variable substitution | No |
| Terminal CANCEL/SUSPEND fulfillment | No |
| Multi-campaign priority selection | No |
| Step re-edit re-fire | No |
| Multi-instance concurrency | No |

Architecture tests do not assert dunning module boundaries beyond general module rules.

**Net:** a critical revenue path ships **almost untested**.

---

## Recommendations (Prioritized)

### P0 — Make the recovery engine actually start and finish

1. **Payment failure → PAST_DUE bridge**
   - On `ExecuteOffSessionCharge` failure: publish domain/integration event or call Commerce to `MarkAsPastDue()` + assign campaign + optional immediate day-0 action.
   - In `ProcessGatewayWebhookCommandHandler`, handle `PAYMENT_FAILED`: publish `GatewayPaymentFailedIntegrationEvent` with subscription metadata; Commerce handler marks PAST_DUE.
   - Align with ADR 014 design.

2. **Fix off-session metadata**
   - Always set `type=commerce_subscription`, `subscription_id`, `tenant_id`, `dunning_campaign_id` on PaymentIntent/CHIP purchase metadata.
   - Map `receipt` as fallback in success handler if needed.

3. **Fix charge attempt model**
   - Drop unique-on-date or change uniqueness to `(SubscriptionId, TargetBillingDate, AttemptNumber)` / allow multiple rows.
   - Store gateway response code, success/fail, next retry at.
   - Make max attempts a campaign or step setting.

4. **Fix `{{plan_name}}` (and add amount/days)**
   - Pass product name, price, currency, days overdue in dunning payload; substitute in Communications handler.

### P1 — Reliability of the scheduler

5. **Catch-up semantics:** fire step if `DayOffset <= daysOverdue` and not yet logged (ordered), not only equality.
6. **Campaign run instance:** when assigning dunning, snapshot campaign id + step definitions (or version number); never re-key steps for in-flight subs.
7. **Replace dead `CurrentDunningStepIndex`** with `LastCompletedDayOffset` / `NextStepId` / list of completed step ids.
8. **Protect delete:** block or reassign if `CurrentDunningCampaignId` references campaign; prefer archive.
9. **Idempotent defaults:** unique name or “if any campaign exists, no-op” on `/defaults`.
10. **Distributed safety:** process per-tenant batches with `FOR UPDATE SKIP LOCKED` or a queue; avoid full-table hourly load.

### P2 — Flexibility (product differentiation)

11. **Restore template linkage optionally** *or* keep inline but share variable catalog and preview with templates.
12. **Native WhatsApp productization:** approved utility templates + interactive CTA button to update-payment / FPX checkout (ADR 020).
13. **Smart retries:** at least static rules by decline code (retry hard declines less; soft declines more; expired card → payment update messaging only).
14. **Freeform offsets + time-of-day + merchant timezone.**
15. **Multi-action steps** or ordered steps with same day offset without `FirstOrDefault` loss.
16. **Ops APIs:** assign campaign, force retry, skip step, dunning activity timeline per subscriber.
17. **Analytics:** event-sourced recovery funnel (step sent, open/click if available, paid, churned).
18. **Fix billing event type** for no-token PAST_DUE (`subscription.past_due`).

### P3 — Hardening & platform quality

19. **Exhaustive tests** for engine matrix: payment methods × steps × grace × pause × multi-campaign priority × failure/success.
20. **Domain enums** for ActionType/FinalAction; validation in command handlers.
21. **Invoice abstraction** long-term (open amount, multi-item) if CaaS expands beyond single product.Price charges.
22. **Remove dead code:** `AdvanceDunningStep` unused, `GetDefaultTemplateIdsAsync` unused, orphaned `GatewayPaymentFailed` until wired.

### Suggested target architecture (concise)

```
Billing due
  → Create Invoice / BillingAttempt
  → Off-session charge
       success → settle invoice, advance period, RecordRecovery if was dunning
       failure → MarkPastDue, AssignDunningRun(snapshot), enqueue DunningScheduler

DunningScheduler (frequent, catch-up)
  → for each open DunningRun, compute due steps from snapshot
  → actions: Message | RetryCharge | Escalate(Suspend|Cancel)
  → write DunningActionLog (idempotent)

Customer update-payment / successful retry
  → close run, metrics, resume access
```

This matches Chargebee/Recurly “dunning period” more than the current “hourly scan of status strings”.

---

## File-by-File Notes

### `DunningCampaign.cs`
- Solid aggregate shell; metrics methods exist.
- Missing validation, versioning, soft-delete constraints.
- `UpdateDetails` clears targeting lists then re-adds — OK.
- `ClearSteps` is dangerous for in-flight runs.

### `DunningStep.cs`
- Minimal entity; internal ctor only via aggregate — good encapsulation.
- ActionType free string; no order index separate from DayOffset (order is day only).

### `Subscription.cs`
- Dunning fields present; pause/resume/clear OK.
- `AdvanceDunningStep` dead.
- `Activate` preserves dates when PAST_DUE/SUSPENDED — good for recovery payments.
- Status is stringly-typed throughout commerce (`"PAST_DUE"` etc.).

### `DunningCampaignCommands.cs` / `ManageSubscriberDunningCommands.cs`
- Thin records; no validation attributes.
- Generate-defaults has no options (not multi-strategy).

### `DunningCampaignCommandHandlers.cs`
- Create/update/delete straightforward.
- Default strategy: -3 email, 0 email, +3 WhatsApp, grace 7, CANCEL — **no AUTO_CHARGE**, so even if PAST_DUE worked, default path is message-only.
- Defaults do not set priority/targets.

### `ManageSubscriberDunningCommandHandlers.cs`
- Correct tenant check on subscription.
- No validation that pause_until is future; no audit log.

### `DunningEngineJob.cs` (heart of the system)
- Combines pre-dunning + post-dunning + terminal escalation + charge + messaging.
- **Too much orchestration in infrastructure worker** (no domain service / application handler) — hard to test.
- Campaign selection duplicated for pre and post.
- Communication payload lacks plan/amount.
- Uses `Modules.Payments.Contracts.Events` directly from Commerce worker (cross-module coupling expected for integration events).
- Does not advance step index; does not handle SUSPENDED recovery messaging.

### `BillingEngineJob.cs`
- Entry gate for dunning is incomplete (vaulted path).
- Wrong fulfillment event name on past due.
- Single attempt per cycle by design here — retries deferred to dunning (which cannot start).

### `DunningCampaignEndpoints.cs`
- Maps DTO snake_case fields correctly.
- No auth notes here (assumed admin group).
- Defaults endpoint always 200 “generated”.

### `CommerceQueryService.Dunning.cs`
- Efficient JSON aggregation for steps.
- Silent catch on deserialize → empty targets/steps on corruption.
- Recovered revenue cast to double (JS number) — OK for UI, not accounting-grade.

### `CommerceRepository.cs`
- Includes steps on get-by-id — required for update.
- `GetDefaultTemplateIdsAsync` leftover from template-based dunning.
- Subscription get includes ReminderLogs — needed for dispatch checks outside job (job uses DbContext directly).

### `CommerceDbContext.cs`
- Unique charge attempt index is the multi-retry landmine.
- jsonb conversions for campaign targets — fine for MVP.

### Migrations
- `AddDunningEngine`: introduced model; migrated ReminderSchedules → one campaign per org named “Legacy Global Recovery”; **dropped TimeOfDay**.
- `RefactorDunningEngine`: inlined bodies; best-effort join to `communications.MessageTemplates`; forced ActionType EMAIL if empty; dropped TemplateId/Channel.

### `GatewayPaymentCompletedIntegrationEventHandler.cs`
- Recovery metrics only if `wasInArrears` **and** metadata has `dunning_campaign_id`.
- Session id reused as subscription id in some paths (historical id-equality assumption) — fragile.
- Does not handle pure PI metadata with only `receipt`.

### Payments off-session stack
- Event carries `DunningCampaignId` but success path often cannot use it without webhook metadata parity.
- Handler returns bool; no structured decline reason to Commerce.

### `FulfillmentRequestedIntegrationEventHandler.cs`
- Dual path: legacy `reminder.due` (template id) vs `reminder.dunning` (inline).
- Variable set incomplete vs product/docs/UI.
- Hard-coded portal host.

### `DispatchMessageIntegrationEventHandler.cs`
- Real multi-channel send + credits — this part of “native WhatsApp” **infrastructure** is real; **orchestration** into dunning is the weak link.

### Ops UI files
- `DunningCampaignsPage.tsx`: marketing-aligned empty state; RM hardcode.
- `CampaignBuilderPage.tsx`: client-side validation for email/WA bodies; drops unused fields for AUTO_CHARGE; sorts by day_offset on save.
- `DunningStepEditor.tsx`: rigid offsets; good preview UX.
- `CampaignSettingsPanel.tsx`: clear targeting UX.
- `SubscribersPage.tsx`: dunning panel only if `status === "PAST_DUE"` — vaulted-failure subs stuck ACTIVE **never show recovery panel**.

### API spec
- Models match implementation shape.
- No enums for action_type/final_action in TypeSpec (all `string`).
- No activity/analytics routes.

### Docs vs code
- ADR 014/020/021 sell WhatsApp dunning + payment_failed consumption.
- Implementation is a **partial scaffold**: campaign CRUD + message pipeline + worker shell, but **failure detection and retry economics are not closed loops**.

---

## Executive verdict

The monorepo has a **credible UX and data model for configuring dunning campaigns** (ops builder, priority targeting, inline email/WhatsApp/auto-charge steps, pause controls, recovery counters) and a **real messaging spine** (events → Communications variable fill → Messaging email/WhatsApp with credits).

It does **not** yet implement a trustworthy revenue-recovery engine for the primary online subscription path:

1. Failed vaulted charges rarely/never enter `PAST_DUE`.
2. Retries are blocked by schema and missing failure handling.
3. Successful silent retries may not update subscription state or metrics.
4. Default messaging ships broken `{{plan_name}}` placeholders.
5. Step progress shown in ops is a dead field.
6. Scheduler is calendar-equality based, hourly, non-versioned, lightly tested.

Until the **payment-failed → dunning run → retry/message → paid/cancel** loop is closed and tested, “Native WhatsApp Dunning” remains a product claim supported by UI and partial plumbing rather than a reliable recovery system comparable to Stripe Smart Retries, Chargebee, or Recurly.

---

*Analysis based solely on repository source as of the inspection date; no runtime behavior was executed.*
