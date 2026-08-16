# W1-LP-056 — Cancel at period end (not only immediate `Cancel()`)

**Date:** 16 August 2026  
**Tracker:** [00-checklist-tracker.md](../00-checklist-tracker.md) `LP-056` (Wave 1, Lazuar = **N**)  
**Implement list:** [00-implement-ids.md](../00-implement-ids.md) — “Cancel at period end”  
**Evidence:** [11-subscriptions-lifecycle.md](../11-subscriptions-lifecycle.md) SL-029–SL-033 / SL-066; [08-subscription-billing-engines.md](../08-subscription-billing-engines.md) cancel is immediate; [00-evaluation.md](../00-evaluation.md) Wave 1 buyer portal.

**This file is analysis only. Do not implement from this document until a follow-up ticket says so.**

**Do not confuse IDs.**

| Alias | What it is | This ticket? |
|-------|------------|--------------|
| **LP-056** (tracker / Wave 1) | Cancel at period end | **Yes** |
| `01-lazuar-feature-inventory.md` `LP-056` | Platform admin console | No |
| `LP-COM-009` in `04-stripe.md` / `20-sequencing` | Same job, still labeled Wave 3 “LATER” in those files | Same job. **Tracker Wave 1 wins.** |
| **LP-055** | Cancel immediately | Already **Y**. Keep it. |
| **SL-031 / SL-033** | Period-end + access until paid-through | Yes |
| **SL-032** | Undo scheduled cancel | **Yes — same ticket** (portal is unusable without Keep) |
| **SL-066** | Portal cancel honesty | Yes (copy + default) |

---

## 0. Feature lock

Sellable sentence after this ticket:

> A customer (or merchant) can mark an **ACTIVE** subscription “do not renew.” Status stays `ACTIVE` until `NextBillingDate`. Access stays granted. The hourly billing job **does not charge** and **does not mint a reminder checkout**; on that due tick it calls `Cancel()` and integrators get `subscription.canceled`. Until then the customer can Keep the plan.

| Path | Must happen |
|------|-------------|
| **Schedule** (`at_period_end = true`, `ACTIVE`, `NextBillingDate > now`) | `CancelAtPeriodEnd = true`. Status **unchanged**. **No** `SubscriptionCanceledIntegrationEvent`. **No** outbound `subscription.canceled`. |
| **Immediate** (`at_period_end = false` or no remaining paid time) | Today’s `Cancel()`: `CANCELED` + typed canceled event. Clears the flag. |
| **Billing due + flag** | **Skip charge / skip reminder mint / skip `PAST_DUE`.** Finalize: `Cancel()` + same canceled event as admin/portal immediate. |
| **Undo** | `CancelAtPeriodEnd = false`. Status stays `ACTIVE`. No event. |
| **Portal** | Period-end is the **default** for healthy `ACTIVE`. Immediate remains available. Scheduled rows show paid-through + **Keep plan**. |

“Billing skip” **is not** “leave the row `ACTIVE` forever and never charge.” A skip without finalize never revokes access and never fires the frozen cancel webhook. The same `BillingEngineJob` tick that would have renewed **must** cancel.

### Clock

`CancelAtPeriodEnd` is the only new column. The cancel instant **is** `NextBillingDate` (the paid-through instant). Do **not** add `CancelAt`, `NON_RENEWING`, or `subscription.updated`.

Webhook `current_period_end` is already documented as `NextBillingDate`, not the `CurrentPeriodEnd` column (`CommerceWebhookPayload`). Use that same instant in the portal.

### Explicit non-goals

| ID / topic | Why it is not this ticket |
|------------|---------------------------|
| **LP-055** | Immediate cancel already works. Do not remove it. |
| **LP-057** | Pause / resume as a product action. Different verb. |
| **LP-058 / LP-059 / LP-174** | Plan change, proration, `invoice_now`. |
| **LP-078** | Dunning terminal `CANCEL` stays **immediate** (non-pay). Do not wait for period end. |
| **LP-137** | M2M subscription admin. Reuse the admin body later. |
| **LP-173** | Portal update payment method. |
| **SL-034** | Reactivate a `CANCELED` row. Enroll again. |
| Stripe Billing Portal | Ops `POST /subscribers/portal-link` is **Stripe’s** portal. Canceling there still does not mutate Hub. Do not “fix” that here. |
| New outbound type | Frozen catalog. **Do not** emit `subscription.updated` when the flag flips. |
| New status | Frozen webhook `status` union is `ACTIVE \| PAST_DUE \| CANCELED \| SUSPENDED`. Scheduled cancel **stays `ACTIVE`**. |

---

## 1. Inventory (current tree, 16 August 2026)

### 1.1 `Subscription.Cancel()`

Path: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs`

```153:157:apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs
    public void Cancel()
    {
        Status = "CANCELED";
        UpdatedAt = DateTime.UtcNow;
    }
```

There is **no** `CancelAtPeriodEnd`, `CancelAt`, `ScheduleCancel*`, or `ClearScheduledCancel`. `Cancel()` does not clear dunning, vault, `NextBillingDate`, or renewal-checkout URL. Harmless for billing (claim excludes `CANCELED`); leftover flag after this ticket would be a lie — `Cancel()` must clear it.

Other writers that matter:

| Method | Interaction if a flag existed today |
|--------|-------------------------------------|
| `Activate` (already `ACTIVE`) | Would keep the flag; dates move on a stray renewal webhook |
| `RecoverFromPayment` / `Resume` | Arrears recovery — customer paid to stay. **Clear the flag** (see §3). |
| `MarkAsPastDue` | Would leave a scheduled-cancel `PAST_DUE` that billing **never claims** (status excluded) → stuck. **Do not allow** schedule on non-`ACTIVE` / already-due rows. |
| `AssignDunningCampaign` / `ClearDunning` | Unrelated. `Cancel()` need not clear dunning for Y; optional nicety. |

EF (`CommerceDbContext` `Subscriptions` block): no cancel-at columns. Snapshot matches. Latest related migration is `20260816120000_AddSubscriptionRenewalCheckout` (LP-052 URL columns).

### 1.2 Who calls `Cancel()` (all immediate)

| Caller | File | Event? |
|--------|------|--------|
| Admin | `CancelAdminSubscriptionCommandHandler` | `SubscriptionCanceledIntegrationEvent` then `SaveChanges` |
| Portal | `CancelPortalSubscriptionCommandHandler` | same |
| Dunning terminal | `PastDueDunningProcessor` (`FinalAction=CANCEL`) | same + `RecordChurn` / `RecordDunningCancel` |
| GDPR | `ClientProfileAnonymizedIntegrationEventHandler` | same, every non-`CANCELED` row |

Admin + portal share the same rules today:

- Missing / wrong org → `"Subscription not found."`
- Already `CANCELED` → **idempotent return**, no event
- Else must be `ACTIVE` \| `PAST_DUE` \| `SUSPENDED` or throw
- Then `Cancel()` + typed event

Commands have **no** `AtPeriodEnd` field:

- `CancelAdminSubscriptionCommand(OrganizationId, SubscriptionId)`
- `CancelPortalSubscriptionCommand(TenantSlug, Token, SubscriptionId)`

Portal extra auth (keep): HMAC token → token’s subscription’s `ClientProfileId` must own the target row; tenant slug must match.

### 1.3 HTTP + TypeSpec

**Admin** `POST /admin/commerce/subscribers/{id}/cancel`  
`SubscriberEndpoints.cs` — no body. Always immediate. `200 { status: "CANCELED" }`.  
TypeSpec `admin-routes.tsp` `cancelSubscriber` — no request model.

**Public** `POST /public/commerce/{tenantSlug}/portal/cancel?token=`  
`PublicPortalEndpoints.cs` — body `CancelPortalRequest { subscription_id }` only. `200 { status: "canceled" }`.

```33:35:packages/api-spec/modules/commerce/models/portal.tsp
model CancelPortalRequest {
  subscription_id: string; // GUID
}
```

`PortalSubscriptionDto` is `id`, `product_id`, `product_name`, `status`, `current_period_end?`. **No** `cancel_at_period_end`.  
`CommerceSubscriptionDto` (ops list / CSV) — same hole.

There is **no** keep / undo route.

### 1.4 Portal UI (two surfaces, one live)

**Live:** `apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx`

- Shows Cancel Plan when `status === ACTIVE \| PAST_DUE`.
- Server action POSTs `{ subscription_id }` only.
- No confirm, no “access until …”, no “this is immediate”, no Keep.
- Label `Renews/Expires` uses `sub.current_period_end` from the API.

**Dead:** `CommunityPortalView.tsx` is **not imported** anywhere. Confirm copy **lies**: “You will lose access at the end of your billing cycle” / “retain access … until {nextDateStr}”. It then sets local status to `CANCELED` immediately. Do not wire this component in this ticket. Either fix the strings if the file stays or leave it unused.

**Legal:** `apps/lazuar-portal/src/app/legal/refund/page.tsx` §4: “Canceling your subscription will immediately stop all future automated charges.” Charge-stop is true for period-end too; access-until is missing. Optional copy.

**Ops:** `SubscribersPage.tsx` confirm “Are you sure you want to cancel this subscription?” then optimistic `status: "CANCELED"`. Prompt library already asks “Cancel … at the end of the month” (`prompt-library.ts`) — chat cannot do that (and ops AI is hidden). Not this ticket.

**Query honesty bug (blocks honest portal copy):**

```28:33:apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Portal.cs
        const string subsSql = @"
            SELECT s.""Id"", s.""ProductId"", p.""Name"" as ProductName, s.""Status"", s.""CurrentPeriodEnd""
```

First activate is `Activate(UtcNow, now+interval)` → `CurrentPeriodEnd` is **subscribe time**, `NextBillingDate` is paid-through. Webhook `current_period_end` is `NextBillingDate`. Portal shows the **wrong clock**. Period-end copy that used today’s portal field would say “until today.”

### 1.5 `BillingEngineJob`

Path: `Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs`  
(LP-052 already shipped: skip `PENDING` / `one_time`, vaulted attempt 1, non-vaulted mint then `PAST_DUE`.)

Claim (relational + in-memory):

```sql
WHERE "NextBillingDate" IS NOT NULL
  AND "NextBillingDate" <= NOW()
  AND "Status" NOT IN ('PENDING', 'PAST_DUE', 'SUSPENDED', 'CANCELED')
```

Eligible: **`ACTIVE` (and any future unknown status)**. A scheduled-cancel `ACTIVE` row **is claimed** when due.

`ProcessOneSubscriptionAsync` today, after product load:

1. Vaulted + not reminder-only → attempt 1 + `ExecuteOffSessionCharge` (or no-op if attempt exists). **Would charge a customer who asked not to renew.**
2. Else → mint reminder checkout (LP-052) + `MarkAsPastDue` + `subscription.past_due`. **Would past-due a non-renewing Billplz member and start dunning.**

There is no flag branch.

`DunningEngineJob` pre-dunning claim: `Status = ACTIVE` and `NextBillingDate` in `(now, now+14d]`. A scheduled-cancel row **still gets “renewal due” emails**. Past-due claim is `PAST_DUE` only — fine if we never past-due a flagged row.

### 1.6 Events after a real cancel (keep this path)

`SubscriptionCanceledIntegrationEvent` → Commerce `SubscriptionLifecycleIntegrationEventHandlers` → `OutboundWebhookRequested` `subscription.canceled` (status `CANCELED`, paid-through = `NextBillingDate`). Communications `LifecycleEventHandlers` sends the **Subscription Cancelled** template. Integrators (Aura SaaS) **revoke on this event**.

If we published that event at schedule time, access would drop while the customer still paid. **Schedule must be silent.**

Frozen TypeSpec (`webhooks.tsp`): five types, no `subscription.updated`, status union without `NON_RENEWING`.

### 1.7 Tests that exist

| Test | What it locks |
|------|----------------|
| `CommerceProductCompletenessTests.CancelAdminSubscription_SetsCanceledAndPublishesEvent` | Immediate admin → `CANCELED` + 1 event |
| `CrossTenantIdorTests.CancelAdminSubscription_ForeignOrg_ThrowsNotFound` | Keep |
| `BillingEngineJobTests.RunOnce_SkipsPastDueSuspendedCanceledAndFutureNotDue` | Claim excludes those statuses |
| `BillingEngineJobTests` vaulted / reminder-only (LP-052) | Charge / mint on **due ACTIVE** |
| `DunningEngineJobTests` terminal CANCEL | Unrelated immediate path |
| `SubscriptionLifecycleWebhookTests` | Canceled envelope shape |

**Missing:** portal cancel handler tests (zero hits). Period-end. Billing skip/finalize. Undo. Portal DTO paid-through. Pre-dunning skip.

---

## 2. Gaps (LP-056 only)

| # | Severity | Gap |
|---|----------|-----|
| G1 | **P0** | No `CancelAtPeriodEnd` on the aggregate / table. Only immediate `Cancel()`. |
| G2 | **P0** | Billing due on `ACTIVE` **always** charges or past-dues. A “do not renew” customer is billed. |
| G3 | **P0** | Even if we skipped charge and left `ACTIVE` + stale `NextBillingDate`, the row would be reclaimed hourly **forever** and never emit `subscription.canceled`. Skip **must** finalize. |
| G4 | **P0** | Portal cancel has no `at_period_end`. Live page is silent immediate. Dead `CommunityPortalView` promises period-end. |
| G5 | **P0** | Portal `current_period_end` is the `CurrentPeriodEnd` column (usually subscribe instant), not paid-through. Period-end UX would lie without this map fix. |
| G6 | **P1** | No undo. Accidental Cancel Plan is a support ticket. Stripe/Paddle both reverse before the timestamp. |
| G7 | **P1** | Admin/ops cancel is immediate-only. Merchants cannot schedule from the directory. |
| G8 | **P1** | Pre-dunning still emails renewal on `ACTIVE` + flag. |
| G9 | **P2** | Ops DTO / CSV / stats have no scheduled-cancel signal (MRR still counting them as active is **correct** until finalize). |
| G10 | **P2** | Legal §4 and developers hint (“Cancel or dunning cancel”) do not mention paid-through. |

Not gaps for this ID: dunning grace cancel; Stripe-hosted portal; `NextBillingDate` left on `CANCELED`; days-overdue on canceled list rows; reactivate.

---

## 3. Minimal changes

Keep `Cancel()`. Add a flag. Teach billing to finalize. Teach portal to choose. Do not add a second worker.

### 3.1 Domain

On `Subscription`:

```csharp
public bool CancelAtPeriodEnd { get; private set; }

public void ScheduleCancelAtPeriodEnd()
{
    if (Status != "ACTIVE")
        throw new InvalidOperationException($"Cannot schedule cancel from status '{Status}'.");
    if (NextBillingDate is null || NextBillingDate.Value <= DateTime.UtcNow)
        throw new InvalidOperationException("No remaining paid period.");
    CancelAtPeriodEnd = true;
    UpdatedAt = DateTime.UtcNow;
}

public void ClearScheduledCancel()
{
    CancelAtPeriodEnd = false;
    UpdatedAt = DateTime.UtcNow;
}

public void Cancel()
{
    Status = "CANCELED";
    CancelAtPeriodEnd = false;
    UpdatedAt = DateTime.UtcNow;
}
```

Also `ClearScheduledCancel()` from `RecoverFromPayment` and `Resume` (arrears pay = they stayed).

**Do not** clear the flag in `Activate` when already `ACTIVE` (stray renewal webhook after a race: they paid another cycle; cancel moves with the new `NextBillingDate`).

Constructor / existing rows: `false`.

### 3.2 Migration + EF

- Column `commerce.Subscriptions.CancelAtPeriodEnd` `boolean NOT NULL DEFAULT false`.
- `HasDefaultValue(false)` next to `IsReminderOnly`.
- No extra index required (claim already uses `NextBillingDate` + `Status`).

### 3.3 Command handlers (same two files)

Add `bool AtPeriodEnd` to both commands (default **false** on admin, **true** on portal — see §3.5).

Shared decision table (implement once, call from both handlers after the existing ownership / status checks):

| `AtPeriodEnd` | Preconditions | Effect | Event |
|---------------|---------------|--------|-------|
| false | `ACTIVE` \| `PAST_DUE` \| `SUSPENDED` | `Cancel()` | Yes |
| true | `ACTIVE` and `NextBillingDate > now` | `ScheduleCancelAtPeriodEnd()` | **No** |
| true | already flagged | no-op | No |
| true | `PAST_DUE` / `SUSPENDED` / due `ACTIVE` / null date | **Treat as immediate** `Cancel()` | Yes |
| already `CANCELED` | any | return (today) | No |

Immediate on a flagged row: `Cancel()` (clears flag) + event. That is “cancel now” after a schedule.

New portal/admin **keep** commands: same token/org checks; if `CANCELED` → 400 too late; else `ClearScheduledCancel()`; no event.

### 3.4 Billing job — skip charge, finalize

In `ProcessOneSubscriptionAsync`, **after** product / `one_time` guards, **before** vault / reminder branches:

```text
if (sub.CancelAtPeriodEnd)
{
    sub.Cancel();
    publish SubscriptionCanceledIntegrationEvent (same shape as admin);
    return;
}
```

Do **not** insert `ChargeAttemptLog`. Do **not** `ExecuteOffSessionCharge`. Do **not** mint `RenewalCheckoutIssuer`. Do **not** `MarkAsPastDue`. Do **not** start a dunning run.

Claim SQL stays as-is. Flagged rows are `ACTIVE` and become due → this branch runs. Future flagged rows (`NextBillingDate > now`) are not claimed (same as any healthy ACTIVE).

In-memory claim already used by `BillingEngineJobTests` — no SQL-only fork.

**Pre-dunning (G8, small):** add `AND s."CancelAtPeriodEnd" IS NOT TRUE` (and the in-memory equivalent) to the pre-dunning claim only. Past-due claim unchanged.

### 3.5 HTTP + TypeSpec

`packages/api-spec` then `task gen`.

**Portal**

```tsp
model CancelPortalRequest {
  subscription_id: string;
  /** Default true. Immediate if false, or if there is no remaining paid period. */
  at_period_end?: boolean = true;
}

model KeepPortalRequest {
  subscription_id: string;
}

model PortalSubscriptionDto {
  // existing fields…
  current_period_end?: utcDateTime; // paid-through = NextBillingDate
  cancel_at_period_end: boolean;
}
```

- `POST .../portal/cancel` — pass `body.At_period_end ?? true` into the command. Status response: `"scheduled"` vs `"canceled"`.
- `POST .../portal/keep` — new, same token query as cancel.

**Admin**

```tsp
model CancelSubscriberRequest {
  at_period_end?: boolean = false;
}
```

`POST /subscribers/{id}/cancel` optional body. Omitted / `{}` = **immediate** (do not surprise existing ops).  
`POST /subscribers/{id}/keep` — undo.  
`CommerceSubscriptionDto.cancel_at_period_end: boolean`.

### 3.6 Queries

`CommerceQueryService.Portal.cs`:

- SELECT `s."NextBillingDate"` **as** the value mapped to `Current_period_end` (match webhook paid-through).
- SELECT `s."CancelAtPeriodEnd"` → DTO.

`CommerceQueryService.Subscribers.cs` `RawSubDto` + SQL + `MapSubscriberDto`: add the bool. Update `CommerceHonestyDtoTests.SubscriberMap_IncludesIsReminderOnly` constructor args.

CSV column is optional (P2). Not required for Y.

### 3.7 Frontends

**`portal/page.tsx` (required)**

- `ACTIVE` + not flagged: Cancel Plan posts `{ subscription_id, at_period_end: true }`. One-line copy: access until `current_period_end`.
- Optional second control: “Cancel immediately” posts `at_period_end: false` (or a confirm). Needed so PAST_DUE / “ban me now” still works from the same page. PAST_DUE: **only** immediate; do not promise period-end.
- Flagged `ACTIVE`: hide Cancel Plan; show “Cancels on {date}” + Keep plan → `POST .../keep`.
- `revalidatePath` after either action.

**`SubscribersPage.tsx` (required, small)**

- Confirm: immediate (default, today’s button) **or** “Cancel at period end” when `status === ACTIVE` and `next_billing_date` is in the future.
- Show a badge when `cancel_at_period_end`.
- Keep button when flagged.
- Do not optimistic-flip to `CANCELED` on schedule.

**`CommunityPortalView.tsx`:** do not wire. If the file remains, stop promising period-end until the API does it — or delete in the same PR. Prefer delete only if nothing else imports it (today: nothing).

Do **not** change Stripe `portal-link`.

### 3.8 Do not touch

- Dunning terminal formula / `PastDueDunningProcessor.Cancel()`
- `ClientProfileAnonymized` (already immediate `Cancel()`; new `Cancel()` clears the flag)
- Charge attempt limits, vault, LP-052 mint path (except the early return)
- Frozen webhook models (optional extra JSON keys are **not** required)
- Interval / timezone math
- Communications templates (cancel email already fires on the typed event at finalize)

---

## 4. Tests to add

Keep in-memory `CommerceDbContext` + NSubstitute event bus. New fixture is fine; do not overload `CommerceProductCompletenessTests` further.

### Domain

1. `ScheduleCancelAtPeriodEnd` on `ACTIVE` + future `NextBillingDate` → flag true, status `ACTIVE`, dates unchanged.
2. Schedule from `PAST_DUE` / `SUSPENDED` / due date in the past / null date → throws.
3. `Cancel()` from flagged → `CANCELED` and flag false.
4. `ClearScheduledCancel` → flag false, still `ACTIVE`.
5. `RecoverFromPayment` / `Resume` clear the flag.

### Admin / portal handlers

6. Admin `AtPeriodEnd=false` — existing test still green (`CANCELED` + 1 event).
7. Admin `AtPeriodEnd=true` — `ACTIVE`, flag true, **zero** canceled events, `SaveChanges` once.
8. Admin `AtPeriodEnd=true` when `NextBillingDate <= now` — immediate cancel + event (fallback).
9. Admin already flagged + `AtPeriodEnd=true` — no second event.
10. Admin keep — flag cleared, no event; keep on `CANCELED` throws.
11. Portal schedule — same as (7) after token + client-profile checks.
12. Portal cannot schedule another client’s sub (existing unauthorized path).
13. IDOR: admin keep / cancel foreign org still `*not found*` (extend `CrossTenantIdorTests`).

### `BillingEngineJobTests`

14. **Flagged + due + vaulted** — after `RunOnce`: `CANCELED`, flag false, **zero** `ExecuteOffSessionCharge`, **zero** `ChargeAttemptLog`, **one** `SubscriptionCanceledIntegrationEvent`. Sibling unflagged due sub still charges / past-dues as today.
15. **Flagged + due + reminder-only** — `CANCELED`, **no** `GenerateCheckoutSessionQuery`, **no** `subscription.past_due`.
16. **Flagged + future `NextBillingDate`** — untouched (`ACTIVE`, flag still true).
17. Existing skip / vaulted attempt-1 tests stay green.

### Dunning (one test)

18. Pre-dunning claim does **not** dispatch EMAIL on a flagged `ACTIVE` due in 3 days (`DunningEngineJobTests`).

### Query

19. Portal map: `current_period_end` equals `NextBillingDate`, not `CurrentPeriodEnd`; `cancel_at_period_end` round-trips.
20. `MapSubscriberDto` includes the new bool (`CommerceHonestyDtoTests`).

### Do not add here

- Stripe Portal session tests.
- Communications template soak (already listens to the same event).
- Reactivate / proration / plan change.
- Playwright on `portal/page.tsx` (optional later). Handler tests are the contract.

---

## 5. Acceptance

A reviewer can mark tracker `LP-056` **Y** when **all** of the following are true in code + tests (no production soak):

1. **Schedule:** `ACTIVE` + future paid-through + `at_period_end` → `CancelAtPeriodEnd=true`, status `ACTIVE`, **no** `subscription.canceled` (typed or outbound).
2. **Billing due + flag (vaulted or not):** no off-session, no reminder checkout, no `PAST_DUE`. Status `CANCELED`, flag false, **one** `SubscriptionCanceledIntegrationEvent` → existing outbound `subscription.canceled`.
3. **Immediate still works** from admin (default) and portal (`at_period_end: false` or PAST_DUE). Existing IDOR test green.
4. **Undo** before the due tick restores a normal renewing `ACTIVE` (next billing job **does** charge / mint).
5. **Portal** default for healthy ACTIVE is period-end; copy uses **paid-through** (`NextBillingDate`); Keep is visible while flagged. PAST_DUE is immediate-only and does not promise remaining access.
6. **Ops** can schedule or cancel now; scheduled rows are not shown as `CANCELED`.
7. **Dunning non-pay cancel** and **GDPR cancel** remain immediate.
8. No new webhook type, no `NON_RENEWING`, no `subscription.updated`.

### Honest demo (after implement)

1. Stripe monthly, `ACTIVE`, `NextBillingDate` next week → portal Cancel Plan → row still `ACTIVE`, badge scheduled, no webhook. Wait/run job **before** due → still `ACTIVE`. Set due to yesterday → job → `CANCELED` + webhook + no PaymentIntent.
2. Same, click Keep before due → next job charges as LP-052.
3. Ops “Cancel Sub” without the period-end option → immediate (today).

Tracker stays **N** until G1+G2+G3+G4+G5 land. Do not flip to **P** on a flag that billing ignores, or to **Y** on skip-without-finalize.

---

## 6. File map (expected touch list)

| File | Change |
|------|--------|
| `Modules/Commerce/Domain/Aggregates/Subscription.cs` | Flag + schedule/clear; `Cancel()` clears flag; recover/resume clear |
| `Modules/Commerce/Infrastructure/CommerceDbContext.cs` + new migration + snapshot | `CancelAtPeriodEnd` default false |
| `Modules/Commerce/Contracts/Commands/CancelAdminSubscriptionCommand.cs` | `bool AtPeriodEnd = false` |
| `Modules/Commerce/Contracts/Commands/CancelPortalSubscriptionCommand.cs` | `bool AtPeriodEnd = true` |
| New keep commands + handlers | Portal token / admin org |
| `CancelAdminSubscriptionCommandHandler.cs` / `CancelPortalSubscriptionCommandHandler.cs` | Decision table §3.3 |
| `Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` | Early finalize branch |
| `Modules/Commerce/Infrastructure/Workers/DunningEngineJob.Claim.cs` | Pre-dunning exclude flag |
| `Infrastructure/Endpoints/SubscriberEndpoints.cs` | Optional body + keep route |
| `Infrastructure/Endpoints/PublicPortalEndpoints.cs` | `at_period_end` + keep |
| `Infrastructure/Services/CommerceQueryService.Portal.cs` | Paid-through + flag |
| `Infrastructure/Services/CommerceQueryService.Subscribers.cs` | Flag on ops DTO |
| `packages/api-spec/modules/commerce/models/portal.tsp` | Request + DTO fields |
| `packages/api-spec/modules/commerce/models/subscriber.tsp` | `cancel_at_period_end` + admin cancel body |
| `packages/api-spec/modules/commerce/public-routes.tsp` / `admin-routes.tsp` | keep + cancel body |
| Generated `Lazuar.ApiContracts.cs` / `api-types-ts` | `task gen` |
| `apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx` | Default period-end, Keep, paid-through |
| `apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx` | Schedule vs now + badge + Keep |
| Tests listed in §4 | Required |

Optional: delete or silence `CommunityPortalView.tsx`; one line on `legal/refund/page.tsx`; developers hint “fires when access actually ends.”

---

## 7. Verdict

| Path | Today | After this ticket |
|------|--------|-------------------|
| Admin cancel | Immediate only | Immediate default; optional period-end |
| Portal cancel | Immediate; one UI lies | Period-end default; immediate opt-in; Keep |
| Billing due | Always charge or PAST_DUE | Flag → skip money, then `CANCELED` + webhook |
| Integrator revoke | On click (too early if we “meant” period-end) | On period end (or immediate click) |
| Dunning / GDPR | Immediate | Unchanged |

Tracker `LP-056` is honestly **N**. Immediate cancel is **LP-055** and must stay **Y**.
