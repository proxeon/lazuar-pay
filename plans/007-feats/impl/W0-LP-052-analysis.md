# W0-LP-052 — Automatic renewal actually runs

**Date:** 16 August 2026  
**Tracker:** [00-checklist-tracker.md](../00-checklist-tracker.md) `LP-052` (Wave 0, Lazuar = **P**)  
**Implement list:** [00-implement-ids.md](../00-implement-ids.md) — “Automatic renewal actually runs”  
**This file is analysis only. Do not implement from this document until a follow-up ticket says so.**

**Do not confuse IDs.** `01-lazuar-feature-inventory.md` uses a *different* `LP-052` (“CRM directory”). This analysis is the **tracker / Wave 0** row: merchant-initiated renewal.

---

## 0. Feature lock

BillingEngineJob / the renewal worker must, when `NextBillingDate <= now`:

| Path | Must happen |
|------|-------------|
| **Vaulted** (`VaultedCustomerId` + `VaultedTokenId` both set) | Charge off-session (attempt **1**, source `BILLING`). Period advances only after `PAYMENT_COMPLETED`. |
| **Non-vaulted** (Billplz / manual / COMPED / zero-amount / no token) | Create a **reminder-only hosted checkout / payment link** bound to the **existing** subscription id, then mark `PAST_DUE`. |

“Actually runs” means the worker **does work on the due date**, not that Stripe Billing hosts the clock. We own the job.

### Explicit non-goals (other Wave 0 / 1 IDs)

| ID | Why it is not this ticket |
|----|---------------------------|
| **LP-047** | Honest vault / capability matrix (Billplz cannot off-session). We *branch* on vault presence; we do not add `SupportsOffSession`. |
| **LP-053** | First-class “send-link-each-cycle” product mode / ops copy. We only **mint the link** at due time. |
| **LP-065** | Offline / record-payment UX. Already exists. |
| **LP-071** | Failed **vaulted** charge → `PAST_DUE` + campaign assign. Handler already exists; do not redesign it. |
| **LP-072** | Dunning `AUTO_CHARGE` attempts 2–4. Billing must keep owning **only attempt 1**. |
| **LP-073 / LP-151 / LP-153** | Email send + template variables. Do **not** start publishing `reminder.due` here (nothing publishes it today; hydrator still requires `template_id`). |
| **LP-056 / SL-023 / SL-011** | Cancel-at-period-end, calendar anchors, price snapshot. Leave UTC `AddMonths` / catalog `product.Price`. |

---

## 1. Inventory (current tree, 16 August 2026)

Stale gap memos (`docs/001-gaps/01-dunning-engine.md`, `07-commerce-module.md`, `17-background-workers.md`) still claim “no SKIP LOCKED”, “no-vault emits `subscription.suspended`”, and “failed off-session never goes PAST_DUE”. **Those three are wrong in source.** Prefer this file + `11-subscriptions-lifecycle.md` + `12-dunning-and-recovery.md`.

### 1.1 `BillingEngineJob`

| Item | Value |
|------|--------|
| Path | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` |
| Registration | `AddHostedService<BillingEngineJob>()` in `Commerce.Infrastructure.DependencyInjection` |
| Interval | `Workers:BillingEngineInterval` default `01:00:00` (`BackgroundWorkerOptions.SectionName` = **`Workers`**, not `BackgroundWorkers`) |
| Batch | 50, one row per scoped DbContext + transaction |
| Lock | Postgres `FOR UPDATE SKIP LOCKED`; in-memory LINQ path for tests |
| Hook | `internal RunOnceAsync` used by `BillingEngineJobTests` |
| Event bus | Keyed `CommerceEventBus` (`OutboxEventBus<CommerceDbContext>` → `CommerceOutboxPublisherJob` → `InMemoryEventBus`) |

Claim SQL (relational):

```sql
WHERE "NextBillingDate" IS NOT NULL
  AND "NextBillingDate" <= NOW()
  AND "Status" NOT IN ('PAST_DUE', 'SUSPENDED', 'CANCELED')
ORDER BY "NextBillingDate"
LIMIT 1
FOR UPDATE SKIP LOCKED;
```

Eligible today: `ACTIVE`, `PENDING`, and any future unknown status. `TRIALING` would be billed if it existed. `DunningPausedUntil` is **ignored** (correct: pause dunning ≠ pause billing).

`ProcessOneSubscriptionAsync`:

1. Load product (`IgnoreQueryFilters`). Missing product → **return with no status change** (row stays due; reclaimed every hour forever).
2. **Both vault fields set**  
   - `targetDate = NextBillingDate.Value.Date`  
   - If `ChargeAttemptLogs` count for `(sub, targetDate) == 0`: insert attempt **1** `SourceBilling`, publish `Modules.Payments.Contracts.Events.ExecuteOffSessionChargeIntegrationEvent` (`DunningCampaignId: null`, `GatewayName: product.GatewayName`, `Amount: product.Price`, `ChargeAttemptId: attempt.Id`).  
   - If count ≥ 1: **no-op** (retries are LP-072). `NextBillingDate` is **not** advanced on dispatch.
3. **Else (no vault)**  
   - `MarkAsPastDue()`  
   - CRM email optional  
   - `FulfillmentRequested` only for `internal:*` product targets with event type **`subscription.past_due`**  
   - `OutboundWebhookRequested` `subscription.past_due`  
   - **No checkout. No payment link. No `reminder.due`.**

`IsReminderOnly` is **not** read. Vault columns are the branch.

### 1.2 `NextBillingDate` writers

Clock is UTC `DateTime.UtcNow` + `AddMonths(1)` unless `product.Interval == "yr"` → `AddYears(1)`. Anything that is not `"yr"` is a month.

| Writer | Sets `NextBillingDate` to | Reminder-only? |
|--------|---------------------------|----------------|
| Open checkout (`GatewayPaymentCompleted`…`OpenCheckout.cs`) | now + 1 mo/yr | Only if gateway returned no tokens |
| `ProcessZeroAmountCheckout` | now + 1 mo/yr | Always (no vault) |
| `MarkCheckoutAsPaidOffline` (product) | now + 1 mo/yr | `isReminderOnly: true` |
| `CreateManualSubscriber` | override or now + 1 mo/yr; **`one_time` uses `start ?? now`** | Always `true` |
| `HandleSubscriptionPaymentAsync` (renewal / update-payment / off-session success) | now + 1 mo/yr via `Activate` / `RecoverFromPayment` / `Resume` | Preserves flag unless `StoreVaultedToken` |
| `RecordSubscriberPayment` | same; rejects `one_time` / `PENDING` / `CANCELED` | Preserves flag |
| `Resume(newNext)` | caller date | — |
| `Activate` when already `PAST_DUE`/`SUSPENDED` | **does not move dates** | Can flip `IsReminderOnly` |

`Cancel()` does **not** null `NextBillingDate`. Harmless: claim excludes `CANCELED`.

Manual enroll of a `one_time` product still calls `Activate(start, start)` → due **immediately**. Next engine tick marks `PAST_DUE`. That is a live footgun.

Webhook `current_period_end` is paid-through = `NextBillingDate`, not the `CurrentPeriodEnd` column (`CommerceWebhookPayload`).

### 1.3 Off-session charge events

**Event:** `Modules.Payments.Contracts.Events.ExecuteOffSessionChargeIntegrationEvent`  
(`TenantId`, `SubscriptionId`, `Amount`, `Currency`, `GatewayCustomerId`, `GatewayTokenId`, `DunningCampaignId?`, `GatewayName` default `STRIPE`, `ChargeAttemptId?`)

Publishers: `BillingEngineJob` (attempt 1) and `DunningEngineJob.PastDue` (attempts 2–4). Dead `Commerce.Contracts` duplicate is **gone**.

**Delivery:** Commerce outbox row → `InMemoryEventBus` → Payments `UsePaymentsSubscriptions` handler. Payments does **not** subscribe via its own inbox; the handler runs **inside the Commerce outbox worker**.

**Handler:** `ExecuteOffSessionChargeIntegrationEventHandler`

| Adapter result | Commerce effect |
|----------------|-----------------|
| No / inactive gateway | `GatewayPaymentFailed` `failure_reason=gateway_not_configured` |
| `ChargeOffSessionAsync` returns `false` | `GatewayPaymentFailed` `failure_reason=charge_declined` |
| Adapter **throws** (Billplz `NotSupportedException`) | **Unhandled.** Outbox `ApplyFailure` retries the **same** event. Attempt row stays `PENDING`. No `GatewayPaymentFailed`. |
| Returns `true` | **No** `GatewayPaymentCompleted` here. Period moves only on webhook. |

Failed-event metadata (good): `type=commerce_subscription`, `subscription_id`, `tenant_id`, `receipt`, `failure_source=off_session`, optional `dunning_campaign_id` + `charge_attempt_id`.

**Adapters**

| Gateway | Off-session | Metadata on charge | Sync “success” |
|---------|-------------|--------------------|----------------|
| Stripe | `PaymentIntent` `OffSession+Confirm`; PI metadata `type` / `subscription_id=receipt` / `tenant_id` | Yes | `succeeded` **or `processing`** |
| CHIP | New purchase + `charge/` with `recurring_token` | Same keys on purchase metadata | `paid` **or `pending_charge`** |
| Razorpay | Recurring payment; dummy `billing@lazuar.com` / `0000000000` | notes | payment id present |
| Billplz | **Throws** | n/a | n/a |

`ChargeOffSessionAsync` has **no** `ChargeAttemptId` / idempotency argument. Stripe create has **no** `Idempotency-Key`. Outbox retry after a timeout can double-charge.

**Close loop (success)**

1. Stripe `payment_intent.succeeded` **is** mapped to `PAYMENT_COMPLETED` (`StripeGatewayAdapter`).  
2. CHIP `purchase.paid` → `PAYMENT_COMPLETED`. `pending_charge` waits for that webhook.  
3. `ProcessGatewayWebhookCommandHandler` publishes `GatewayPaymentCompleted`.  
4. Commerce `HandleAsync`: `type` must be `commerce_subscription` / `saas_subscription`; correlation = `subscription_id` else `receipt`.  
5. OPEN `CheckoutSession` with that id → **first subscribe** (new Subscription). Else `HandleSubscriptionPaymentAsync`.  
6. That handler: `RecoverFromPayment` / `Resume` / `Activate`; `StoreVaultedToken` if ids present; `MarkSucceeded` on attempt (by `charge_attempt_id` or latest `PENDING`); `SubscriptionActivated(IsFirstPayment: false)` or `SubscriptionResumed`.

Off-session PIs put `subscription_id` = real sub id, so they take path (6), not (5). **Do not** mint a Commerce `CheckoutSession` and stamp *its* id as `subscription_id` — that creates a **second** subscription.

**Close loop (failure):** `GatewayPaymentFailedIntegrationEventHandler` marks attempt failed, `MarkAsPastDue` if not already terminal, assigns campaign, emits `subscription.past_due` once. That is **LP-071** (already coded). Billing will not re-dispatch attempt 1 after the log exists.

**Unique index:** `ChargeAttemptLogs (SubscriptionId, TargetBillingDate, AttemptNumber)`.

### 1.4 Reminder / non-vaulted path (as coded)

There is **no** “create checkout on due date” path.

What exists instead:

| Piece | What it does | Why it is not LP-052 |
|-------|----------------|----------------------|
| Billing no-vault branch | `PAST_DUE` + outbound `subscription.past_due` | No URL, no Billplz bill |
| `FulfillmentRequested` `subscription.past_due` | Only `internal:*` on the product | Communications **ignores** this event type (only `reminder.due` / `reminder.dunning`) |
| `reminder.due` | Hydrator exists; **no publisher** | Orphan templates (“Subscription Renewal Due Today”, …) |
| Dunning pre/past EMAIL | Inline campaign copy; `{{update_payment_link}}` = portal `/{slug}/update-payment/{subId}` | Requires a campaign; link is a **page**, not a pre-created bill |
| `POST /public/commerce/checkout/{subId}/update-payment` | Buyer-initiated `GenerateCheckoutSessionQuery` with **real** `subscription_id`; only `PAST_DUE`/`SUSPENDED` | Not merchant-initiated; creates a new gateway session **per click** |
| Portal `update-payment/[subId]` | Server action POSTs that route | Customer must open the page |
| `GenerateCheckoutSessionQuery` → `CheckoutSessionCashier` | Hosted URL; **does not** persist `IntegrationCheckoutSession` | Billplz still correlates via `reference_1` = `subscription_id` |
| M2M `CreateIntegrationCheckout` | Persists `payments.IntegrationCheckoutSessions` | Different product (cashier), not Commerce renewals |

`CheckoutSessionExpiryJob` only expires Commerce `CheckoutSession` rows (24h first-buy). It does not touch reminder bills.

Default dunning seed (`-3` email, `0` email, `+3` WhatsApp, grace 7, `CANCEL`) can email the **update-payment page** if the merchant generated defaults. That is LP-073’s world. LP-052 is the missing **hosted checkout created by the worker**.

### 1.5 Tests that exist

| Test | Covers | Misses |
|------|--------|--------|
| `tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs` | Two due unvaulted rows → both `PAST_DUE`; skip already `PAST_DUE`/`CANCELED` | **No vaulted case.** No event assert. No checkout URL. |
| `ExecuteOffSessionChargeIntegrationEventHandlerTests` | Receipt/tenant/campaign forwarded; decline + unconfigured → failed event | Throw path; idempotency; success does not publish completed |
| `GatewayPaymentFailedIntegrationEventHandlerTests` | PAST_DUE + attempt fail by id | — |
| `SubscriptionRecoveryTests` | `RecoverFromPayment` / `Activate` arrears semantics | Not wired to the job |

No test that `RunOnce` + vault → `ExecuteOffSessionCharge` + attempt 1 only.

---

## 2. Gaps (LP-052 only)

| # | Severity | Gap |
|---|----------|-----|
| G1 | **P0** | Non-vaulted due path does **not** create a checkout or payment link. Billplz / reminder-only / manual renewals only flip `PAST_DUE`. |
| G2 | **P0** | Vaulted dispatch is **untested**. Tracker cell `P` is honest: the code path exists; we cannot claim “actually runs” without a job-level assertion. |
| G3 | **P1** | `ExecuteOffSessionCharge` throw (Billplz, transport) is not caught → outbox retry of the same event. Combined with **no Stripe/CHIP idempotency key**, a timeout retry can **double-charge**. |
| G4 | **P1** | Missing product: silent return, infinite reclaim. `one_time` subs (manual enroll sets `NextBillingDate = now`) get claimed and `PAST_DUE`. |
| G5 | **P2** | Claim includes `PENDING`. Leftover constructor rows with a date would be billed. |
| G6 | **P2** | Off-session success is webhook-only. `processing` / CHIP `pending_charge` leave `ACTIVE` + stale `NextBillingDate` + attempt 1 until webhook. Acceptable if we document it; do not invent a sync “completed” event in this ticket. |
| G7 | **P2** | `charge_attempt_id` is not copied onto gateway metadata, so success stamps the latest `PENDING` log. Fine for attempt 1; leave adapter widening to LP-072 if needed. |
| G8 | **P2** | Razorpay dummy PII — do not demo as a renewal rail (LP-047 / later). |

Not gaps for this ID: dunning pause ignored; catalog price re-read; UTC `AddMonths`; no `subscription.renewed` (success already emits `SubscriptionActivated` / `subscription.activated`).

---

## 3. Minimal changes

Keep the worker. Do not add a second renewal job. Do not create a Commerce `CheckoutSession` for renewals (that id in `subscription_id` would open a **new** Subscription).

### 3.1 Vaulted — make attempt 1 safe and proven

In `BillingEngineJob.ProcessOneSubscriptionAsync` (vault branch): **keep** “insert attempt 1 + publish event + do not advance dates”.

In `ExecuteOffSessionChargeIntegrationEventHandler`:

1. `try/catch` around `ChargeOffSessionAsync`. On unexpected throw, publish `GatewayPaymentFailed` (`failure_reason=charge_exception`) and **do not rethrow** (stops outbox hammer / Billplz hang).  
2. Pass an idempotency key into adapters that support it: use **`@event.Id`** (stable across outbox retries).  
   - Stripe: `RequestOptions.IdempotencyKey`.  
   - CHIP/Razorpay: pass through if trivial; otherwise document “best-effort”.  
   Smallest signature change: optional last arg on `ChargeOffSessionAsync`, default `null`, so existing tests still compile with the current 9 args.

Do **not** mark `PAST_DUE` inside the billing job on vaulted dispatch (LP-071 owns fail). Do **not** fire attempt 2 (LP-072).

### 3.2 Non-vaulted — mint a reminder checkout, then `PAST_DUE`

New small helper (same file or `RenewalCheckoutIssuer` in Commerce Application) called **only** from the no-vault branch.

**Create (HTTP) before status change:**

```
GenerateCheckoutSessionQuery(
  TenantId: sub.OrganizationId,
  Amount: product.Price,
  Currency: product.Currency,
  ProductName: product.Name,
  CustomerEmail: crm email,
  SuccessUrl: {App:ClientUrl}/{slug}/portal,
  CancelUrl:  {App:ClientUrl}/{slug}/update-payment/{sub.Id},
  Metadata: {
    type: commerce_subscription,
    subscription_id: <existing Subscription.Id>,   // NOT a new session id
    tenant_id: org
  },
  SetupFutureUsage: true,   // Stripe/CHIP may vault this cycle
  GatewayName: product.GatewayName
)
```

Billplz `reference_1` becomes the real sub id → webhook → `HandleSubscriptionPaymentAsync`. Same as update-payment.

**Persist** so the URL is not only in a log line (2 nullable columns on `commerce.Subscriptions`, or a 1-row-per-cycle child with unique `(SubscriptionId, TargetBillingDate)`):

- `CurrentRenewalCheckoutUrl`  
- `CurrentRenewalCheckoutForDate`  

Clear both in `RecoverFromPayment` / successful `Activate` on this cycle (and `Resume`).

**Then** `MarkAsPastDue` + existing `subscription.past_due` webhook. Add `checkout_url` to that payload (optional field; `DefaultIgnoreCondition.WhenWritingNull` already on `CommerceWebhookPayload.Snake`).

**If CRM email or gateway generate fails:** do **not** mark `PAST_DUE`; throw → existing `failedIds` + rollback → retry next hour.  
**If email is missing:** log + mark `PAST_DUE` without URL (cannot create a Billplz bill). Same as today plus a warning.

**Do not** publish `reminder.due` (LP-073). Dunning can still email `update_payment_link`.

**Optional 8-line follow-through (recommended):** `POST .../update-payment` returns `CurrentRenewalCheckoutUrl` when `CurrentRenewalCheckoutForDate == NextBillingDate.Date` instead of opening a **second** Billplz bill. Buyer click and worker mint become the same link.

### 3.3 Claim / one_time hygiene

- Claim SQL + in-memory: also `Status != 'PENDING'`.  
- After product load: if `product == null` or `Interval == "one_time"` → `failedIds` **or** (for `one_time`) null `NextBillingDate` if you add a domain setter; cheapest: skip + `failedIds` **and** stop writing a due date in `CreateManualSubscriber` when `Interval == "one_time"` (`Activate` with `nextBillingDate: start` is the bug).  
- Prefer **not** adding `NextBillingDate` null-setter unless you need it; fixing enroll + skip in the job is enough.

### 3.4 DI

`BillingEngineJob` already resolves `ICrmQueryService?`. Add scoped `IMediator` + `IOneQueryService` + `IConfiguration` (or a dedicated issuer that takes those). No new hosted service.

### 3.5 Do not touch

- Dunning claim / AUTO_CHARGE / grace cancel  
- `ChargeAttemptLimits.MaxAttemptsPerBillingCycle`  
- Portal copy, ops UI, TypeSpec (unless you add `checkout_url` to the frozen webhook schema — **prefer optional extra key in the JSON object only**, same builder, no new event name)  
- Interval math / timezone  

---

## 4. Tests to add

Keep in-memory `CommerceDbContext` + keyed `IEventBus` substitute (existing fixture).

### `BillingEngineJobTests` (required)

1. **Vaulted due** — `StoreVaultedToken`; `RunOnce`; assert:  
   - status still `ACTIVE`  
   - `NextBillingDate` unchanged  
   - one `ChargeAttemptLog` attempt 1, `SourceBilling`, `PENDING`  
   - `PublishAsync(ExecuteOffSessionChargeIntegrationEvent)` once with `SubscriptionId`, `product.Price`, vault ids, `DunningCampaignId == null`, `ChargeAttemptId == log.Id`, `GatewayName == product.GatewayName`  
2. **Vaulted already has attempt 1** — second `RunOnce` publishes **zero** new off-session events; still one log row.  
3. **Non-vaulted due** — after issuer is wired: `PAST_DUE`; `GenerateCheckoutSessionQuery` sent (mock `IMediator`); URL stored; `OutboundWebhookRequested` `subscription.past_due`.  
4. **Non-vaulted generate throws** — status stays `ACTIVE`; no URL; next `RunOnce` retries (same due row).  
5. **Skip** `PAST_DUE` / `SUSPENDED` / `CANCELED` / future `NextBillingDate` (keep existing skip test; add future-not-due).  
6. **`one_time` product** — due sub is **not** marked `PAST_DUE` and does not charge.  
7. **Missing product** — does not throw the whole batch; sibling due sub still processed (`failedIds` behaviour).

### Payments (small)

8. `ExecuteOffSessionCharge` — adapter **throws** → `GatewayPaymentFailed` `charge_exception`, no throw out of `HandleAsync`.  
9. Existing decline / unconfigured tests stay green.  
10. If Stripe adapter gains idempotency: unit test that `RequestOptions.IdempotencyKey == event.Id` (optional if you only catch-throw in this ticket).

### Do not add here

- Full Stripe webhook soak (operator residual).  
- Dunning AUTO_CHARGE tests (LP-072).  
- Communications `reminder.due` (LP-073).  
- `HandleSubscriptionPaymentAsync` date math (already `SubscriptionRecoveryTests` + payment-completed handler tests if present).

---

## 5. Acceptance

A reviewer can mark tracker `LP-052` **Y** when all of the following are true in **code + tests** (no production soak required for the cell flip):

1. **Vaulted, due:** hourly job inserts billing attempt 1 and publishes `ExecuteOffSessionCharge` with catalog amount, product gateway, and vault ids. Dates do not move on dispatch.  
2. **Vaulted, already attempted this `NextBillingDate.Date`:** job is a no-op (no second attempt 1).  
3. **Vaulted, webhook `PAYMENT_COMPLETED` with `subscription_id` = that sub:** existing handler advances `NextBillingDate` by interval and leaves `ACTIVE` (already implemented; do not regress).  
4. **Non-vaulted, due:** job creates a hosted checkout/link whose gateway metadata `subscription_id` is the **existing** Subscription id, persists the URL, then `PAST_DUE`. Paying that link does **not** create a second Subscription.  
5. **Non-vaulted, generate fails:** no `PAST_DUE`; retry next tick.  
6. **Excluded statuses** (`PAST_DUE`, `SUSPENDED`, `CANCELED`) and **not-yet-due** rows are untouched.  
7. **`one_time` is not renewed.**  
8. Adapter throw on off-session does **not** leave an infinitely retried outbox poison pill without a failed event.  
9. Dunning `AUTO_CHARGE` still owns attempts 2–4; billing does not start sending email.

### Honest demo script (after implement)

1. Stripe product, vaulted ACTIVE, `NextBillingDate` 1 minute ago → wait/run job → PaymentIntent + attempt 1 → webhook → next bill +1 month.  
2. Same product, Billplz, no vault → job → Billplz bill URL stored + `PAST_DUE` → pay bill → same row `ACTIVE`, dates advanced.  
3. Confirm a second Subscription row was **not** created in (2).

---

## 6. File map (expected touch list)

| File | Change |
|------|--------|
| `Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` | Skip `PENDING` / `one_time`; no-vault mint checkout; failedIds on missing product |
| `Modules/Commerce/Domain/Aggregates/Subscription.cs` | Store/clear renewal checkout URL + date |
| `Modules/Commerce/Application/CommerceWebhookPayload.cs` | Optional `checkout_url` |
| `Modules/Commerce/Infrastructure/CommerceDbContext.cs` + new migration | Two columns (or child table) |
| `Modules/Commerce/Application/Commands/CreateManualSubscriberCommandHandler.cs` | Do not set a due date on `one_time` |
| `Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs` | Optional: reuse stored URL |
| `Modules/Payments/Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs` | Catch throw → failed event |
| `Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs` + 4 adapters | Optional idempotency arg |
| `Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` | `IdempotencyKey = event.Id` |
| `tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs` | G2 + reminder mint |
| `tests/Lazuar.ModuleTests/Payments/ExecuteOffSessionChargeIntegrationEventHandlerTests.cs` | Throw path |

---

## 7. Verdict

| Path | Today | After this ticket |
|------|--------|-------------------|
| Vaulted Stripe/CHIP | Dispatch exists; untested; throw/idempotency holes | Attempt 1 is the renewal; tests lock it; fail event on throw |
| Non-vaulted / Billplz | `PAST_DUE` only | Merchant-initiated hosted link + `PAST_DUE` |
| Email / dunning retries | Separate engines | Unchanged |

Tracker should stay **P** until G1 + G2 + G3 land. Do not flip to **Y** on dispatch-only.
