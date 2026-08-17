# 02 — Commerce: subscription state machine + BillingEngineJob

**Date:** 17 August 2026  
**Branch:** `feat/007-waves-1-4-implement`  
**HEAD:** `297ba98` (`fix(one): add /accept-invite on ops and mint invite URLs there`)  
**Slice lock:** subscription statuses PENDING / TRIALING / ACTIVE / PAST_DUE / SUSPENDED / CANCELED; cancel immediate vs period-end; collection pause; trial convert; pending plan change; pending quantity; UnitAmount snapshot; MRR; off-session vs reminder mint; claim SQL; failedIds.  
**Product:** Lazuar Pay Commerce billing engine  
**Code read:** current tree at HEAD. `plans/008-evals/01-commerce-subscriptions-checkout.md` is historical (commit `4624070`). Where 008 and this tree disagree, this tree wins.

This report does **not** implement fixes. It does **not** re-open the three items listed as recently fixed if the current code still contains the fix. It does **not** treat a missing refuse-list feature as a bug. Speculation is labeled.

Out of scope (other 009 slices): hop-1 checkout internals, dunning step dispatch, payment adapter HTTP, ledger journals, frontends except where they prove a lifecycle bug.

---

## Scope lock

In: the `Subscription` writers for the six statuses and the Wave 3 columns; cancel/keep/change-plan/quantity/pause; Gross / snapshot / MRR; `BillingEngineJob` claim + ProcessOne + failedIds; payment completed/failed only where they convert TRIALING, roll `NextBillingDate`, or `RefreshSnapshot`. Frontend only as proof a lifecycle verb exists.

Out: hop-1 (01), dunning dispatch (03), adapter HTTP (04), ledger (05).

---

## Files table

Paths are under `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/` unless noted.

### Domain

| File | What it owns in this slice |
|------|----------------------------|
| `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs` | Every status write, snapshot, pending plan/qty, pause, cancel flag, recover/resume, vault, dunning pins, renewal URL |
| `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Product.cs` | `Price`, `Interval`, `Prices`, `DefaultPrice()`, `GetPrice()`, SST, TrialDays |
| `apps/lazuar-api/Modules/Commerce/Domain/Entities/ChargeAttemptLog.cs` | Attempt 1 (billing) vs 2–4 (dunning); unique `(SubscriptionId, TargetBillingDate, AttemptNumber)` |
| `apps/lazuar-api/Modules/Commerce/Domain/ChargeAttemptLimits.cs` | `MaxAttemptsPerBillingCycle = 4` |

### Application policies and verbs

| File | What it owns |
|------|----------------|
| `.../Application/SubscriptionCancelDecision.cs` | Schedule vs immediate vs already-canceled vs illegal status |
| `.../Application/SubscriptionCancelApplier.cs` | Persist + `SubscriptionCanceledIntegrationEvent` only on immediate |
| `.../Application/PlanChangePolicy.cs` | Next-renewal-only, live-status, same gateway/currency/interval |
| `.../Application/SubscriptionBillingAmount.cs` | Unit / Seats / Line / Gross / AdvanceFrom / ResolveInterval |
| `.../Application/SubscriptionActivation.cs` | First activate: trial vs paid period |
| `.../Application/RenewalCheckoutIssuer.cs` | Hosted bill bound to existing sub id; Quantity 1; amount = Gross |
| `.../Application/CommerceMrr.cs` | Monthly equivalent helper (ACTIVE, unpaused, mo/yr) |
| `.../Application/CommerceWebhookPayload.cs` | `current_period_end` = NextBillingDate; amount 0 while TRIALING else Gross |
| `.../Application/Commands/CancelAdminSubscriptionCommandHandler.cs` | Ops cancel; default `at_period_end` is an HTTP concern |
| `.../Application/Commands/CancelPortalSubscriptionCommandHandler.cs` | Magic-link cancel after `PortalSubscriptionAccess` |
| `.../Application/Commands/KeepAdminSubscriptionCommandHandler.cs` | Clear flag; 400 if already CANCELED |
| `.../Application/Commands/KeepPortalSubscriptionCommandHandler.cs` | Same, after token ownership |
| `.../Application/Commands/ChangePlanCommandHandler.cs` | Admin pending product; also SetQuantity / Pause / Resume handlers |
| `.../Application/Commands/ChangePortalPlanCommandHandler.cs` | Portal pending product; extra PAST_DUE / flagged guards |
| `.../Application/Commands/RecordSubscriberPaymentCommandHandler.cs` | Clerk cash; date roll; RecoverFromPayment vs Activate |
| `.../Application/Commands/CreateManualSubscriberCommandHandler.cs` | Ops enroll qty 1, reminder-only, optional trial |
| `.../Application/Commands/AnonymizeSubscriberCommandHandler.cs` | Scrub logs + CRM; cancel is the anonymized event handler |
| `.../Contracts/Commands/ChangePlanCommand.cs` | `PlanChangePreview`, change-plan / quantity / pause / resume records |

### Infrastructure (job, SQL, HTTP, recovery)

| File | What it owns |
|------|----------------|
| `.../Infrastructure/Workers/BillingEngineJob.cs` | Hourly loop, batch 50, claim, ProcessOne, failedIds |
| `.../Infrastructure/Workers/DunningEngineJob.Claim.cs` | Contrast only: `processedIds ∪ failedIds` |
| `.../Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs` | Trial/ACTIVE convert, RecoverFromPayment, Resume, RefreshSnapshot |
| `.../Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` | Fail attempt → PAST_DUE (including from TRIALING) |
| `.../Infrastructure/EventHandlers/ClientProfileAnonymizedIntegrationEventHandler.cs` | GDPR `Cancel()` including TRIALING and PENDING |
| `.../Infrastructure/EventHandlers/CommerceGatewayDisputeCreatedHandler.cs` | Sets `HasOpenDispute` |
| `.../Infrastructure/Endpoints/SubscriberEndpoints.cs` | Ops cancel/keep/change-plan/quantity/pause/resume |
| `.../Infrastructure/Endpoints/PublicPortalEndpoints.cs` | Portal cancel default `at_period_end ?? true` |
| `.../Infrastructure/Endpoints/IntegrationSubscriptionEndpoints.cs` | M2M cancel hard immediate |
| `.../Infrastructure/Services/CommerceQueryService.Stats.cs` | MRR/ARR/ARPU SQL |
| `.../Infrastructure/Services/CommerceQueryService.Portal.cs` | `NextBillingDate` aliased as `current_period_end` |
| `.../Infrastructure/CommerceDbContext.cs` | Unique charge-attempt index; Wave 3 columns |
| `.../Infrastructure/Migrations/20260820120000_AddWave3SubscriptionBilling.cs` | Quantity, pending, UnitAmount, BillingInterval, TrialEndsAt, CollectionPausedUntil, HasOpenDispute |

### Frontends cited only as lifecycle proof

| File | What it proves |
|------|----------------|
| `apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx` | TRIALING can cancel at period end / immediately; Keep; plan change only healthy ACTIVE |
| `apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx` | TRIALING cancel + plan/seats; pause collection only ACTIVE |

### Tests (under `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/`)

`Workers/BillingEngineJobTests` (claim, off-session, mint, pause isolation, pending plan, seats, SST 108). `SubscriptionCancelAtPeriodEndTests` (domain + admin/portal + TRIALING). `SubscriptionTrialTests`, `SubscriptionCollectionPauseTests` (domain only). `SubscriptionRecoveryTests` (Recover vs Activate-from-PAST_DUE). `ChangePlanCommandHandlerTests` / `ChangePortalPlanCommandHandlerTests` / `PlanChangePolicyTests`. `SubscriptionBillingAmountTests` (108 / 324). `CommerceMrrTests` (helper only). `RecordSubscriberPaymentCommandHandlerTests`. `GatewayPaymentCompletedRecoveryMetricsTests`. `CommerceGatewayDisputeCreatedHandlerTests` (flag write). `CommerceEndpointsAuthorizationTests` (list = OrgRead; does **not** assert change-plan / pause).

---

## Intended mechanics (what the code is trying to be)

The sellable sentence this slice is supposed to implement:

> A recurring row is PENDING until first activate, TRIALING until `TrialEndsAt`, then ACTIVE. Each due tick the hourly job claims one row, applies next-renewal catalog mutations, then either off-session charges Gross or mints a hosted bill and marks PAST_DUE. Cancel-at-period-end is a flag on ACTIVE/TRIALING that the job finalizes when `NextBillingDate` is due, without charging. Collection pause is a date on ACTIVE, not a status. MRR is committed snapshot monthly equivalent on unpaused ACTIVE rows.

That is Chargebee-shaped vocabulary with these locked product rules (refuse-list / Wave 3 done notes, not bugs):

- No unused-time proration. `PlanChangePolicy.Preview` hard-codes `AmountDueNow = 0`. `prorate=true` and `apply=immediate` throw.
- No interval swap via change-plan. “Interval change requires a new checkout.”
- No `PAUSED` status. Pause is `CollectionPausedUntil` on ACTIVE.
- Billing owns attempt 1 only. Dunning owns 2–4. The job must **not** advance `NextBillingDate` on dispatch; the success webhook does.
- Reminder-only (Billplz, no vault, `IsReminderOnly`) never off-sessions. It mints and goes PAST_DUE.
- Seats are `Quantity` 1–99. Mint callers that already have a line total must pass `Quantity: 1` so the adapter does not square.

The three post-008 fixes this report is required to re-verify, not re-open:

1. `911d358` — claim excludes `CollectionPausedUntil > now`; skip adds `failedIds`.
2. `616b37d` — TRIALING is a cancelable status; period-end on a future trial stays TRIALING and the job must not charge when the flag is set.
3. `eba0741` — renewals charge Gross (SST) while `UnitAmount` stays net.

---

## Status machine as written

There is no enum. Status is a string. Writers always use uppercase ASCII. Readers are case-sensitive.

| Status | Who writes it | Who claims it for billing | Cancel |
|--------|---------------|---------------------------|--------|
| `PENDING` | Constructor only | Excluded | Decision throws. GDPR `Cancel()` still does. |
| `TRIALING` | `ActivateTrial` | Included (not in the NOT IN list) | Immediate or schedule if `NextBillingDate > UtcNow` |
| `ACTIVE` | `Activate`, `RecoverFromPayment`, `Resume` | Included | Immediate or schedule if future next |
| `PAST_DUE` | `MarkAsPastDue` (job mint path, payment-failed handler) | Excluded (dunning owns it) | Immediate only (schedule request falls through) |
| `SUSPENDED` | `Suspend` (dunning final; out of slice) | Excluded | Immediate only |
| `CANCELED` | `Cancel` | Excluded | Idempotent already-canceled |

There is no seventh status in this tree. Collection holiday is not a status. `HasOpenDispute` is a boolean next to the machine, not a state.

Constructor (`Subscription.cs` 69–85): `Status = "PENDING"`, `Quantity = 1`, `UnitAmount = 0m`, no dates, no vault, `IsReminderOnly = false`.

`ActivateTrial` (118–134): requires `endsAt > UtcNow`. Writes TRIALING, `TrialEndsAt = NextBillingDate = CurrentPeriodEnd = endsAt`, snapshot, reminder flag. That is why a trial is claimed exactly when the trial ends: the due clock **is** the trial clock.

`Activate` (87–116): always sets ACTIVE. If the row was PAST_DUE or SUSPENDED, it **refuses to write dates** (`NextBillingDate = NextBillingDate` is a no-op). Recovery must call `RecoverFromPayment` or `Resume`. Tests pin this (`SubscriptionRecoveryTests.Activate_FromPastDue_DoesNotAdvanceBillingDates`). The payment webhook obeys it. Record-payment also uses RecoverFromPayment for arrears.

`MarkAsPastDue` / `Suspend` / `Cancel` are unguarded writes. Any status can be forced PAST_DUE. Cancel clears `CancelAtPeriodEnd`. Cancel does **not** clear `SuspendedAt`, `TrialEndsAt`, `PendingProductId`, `PendingQuantity`, or vault ids.

`ScheduleCancelAtPeriodEnd` (341–349) is stricter than the decision table: only ACTIVE or TRIALING, and `NextBillingDate` must be in the future. The decision table calls this only when those are already true; if the merchant asked for period-end on a due ACTIVE, the decision **does not** call `ScheduleCancelAtPeriodEnd` — it falls through to `Cancel()`. That is why “cancel at period end when already due” is immediate. Test: `Admin_AtPeriodEndTrue_WhenDue_FallsBackToImmediate`.

---

## Line-by-line walk — cancel

### Decision table

```15:46:apps/lazuar-api/Modules/Commerce/Application/SubscriptionCancelDecision.cs
    internal static Outcome Apply(Subscription subscription, bool atPeriodEnd)
    {
        if (subscription.Status == "CANCELED")
        {
            return Outcome.AlreadyCanceled;
        }

        if (subscription.Status is not ("ACTIVE" or "PAST_DUE" or "SUSPENDED" or "TRIALING"))
        {
            throw new InvalidOperationException(
                $"Subscription cannot be canceled from status '{subscription.Status}'.");
        }

        if (atPeriodEnd)
        {
            if (subscription.CancelAtPeriodEnd)
            {
                return Outcome.Scheduled;
            }

            if (subscription.Status is "ACTIVE" or "TRIALING"
                && subscription.NextBillingDate is { } next
                && next > DateTime.UtcNow)
            {
                subscription.ScheduleCancelAtPeriodEnd();
                return Outcome.Scheduled;
            }
        }

        subscription.Cancel();
        return Outcome.ImmediateCanceled;
    }
```

Read it as a table, because 008’s table is stale:

- CANCELED → AlreadyCanceled. No event. No second write.
- Not ACTIVE/PAST_DUE/SUSPENDED/TRIALING → throw. The only remaining constructor status is PENDING. Test: `Admin_Pending_StillRejected`.
- `atPeriodEnd` + already flagged → Scheduled, no event. Idempotent keep-the-flag.
- `atPeriodEnd` + (ACTIVE or TRIALING) + future `NextBillingDate` → set flag, stay in status, no event.
- Everything else, including PAST_DUE period-end, SUSPENDED period-end, and due ACTIVE/TRIALING period-end → immediate `Cancel()`.

008 said TRIALING was illegal. That was true at `4624070`. `616b37d` put TRIALING in the allow-list and in the schedule predicate. The current tree matches the commit message: “Period-end cancel on a trial stays TRIALING until trial end and does not charge.”

The “does not charge” half is **not** in this file. This file only sets a flag. The job is the thing that must see the flag before it charges. That is ProcessOne step 4, quoted below.

### Applier

```19:43:apps/lazuar-api/Modules/Commerce/Application/SubscriptionCancelApplier.cs
        var outcome = SubscriptionCancelDecision.Apply(subscription, atPeriodEnd);
        if (outcome == SubscriptionCancelDecision.Outcome.AlreadyCanceled)
        {
            return canceledStatus;
        }

        if (outcome == SubscriptionCancelDecision.Outcome.Scheduled)
        {
            await repository.SaveChangesAsync(ct);
            return "scheduled";
        }

        var product = await repository.GetProductByIdAsync(subscription.ProductId, ct);
        var fulfillmentTargets = product?.FulfillmentTargets.ToList() ?? [];

        await eventBus.PublishAsync(new SubscriptionCanceledIntegrationEvent(
            subscription.OrganizationId,
            subscription.Id,
            subscription.ClientProfileId,
            subscription.ProductId,
            fulfillmentTargets));

        await repository.SaveChangesAsync(ct);
        return canceledStatus;
```

Event only on immediate. Scheduled is a quiet column flip. Admin returns `"CANCELED"` on immediate (`canceledStatus: "CANCELED"`). Portal returns `"canceled"` (lowercase). Integration uses the admin command with `AtPeriodEnd: false`, so it is always immediate and always `"CANCELED"`.

Admin HTTP default is `body?.At_period_end ?? false` (`SubscriberEndpoints.cs` 109). Portal HTTP default is `body.At_period_end ?? true` (`PublicPortalEndpoints.cs` 166). Ops UI has two buttons. Portal UI has “Cancel Plan” (period-end) and “Cancel immediately”. That split is product, not a bug.

Keep handlers (`KeepAdminSubscriptionCommandHandler.cs` 26–32, portal twin 38–44) throw if already CANCELED, otherwise `ClearScheduledCancel()`. They do not publish an event. They do not check that the flag was set. Keep on a healthy ACTIVE is a no-op write.

Portal trial cancel is no longer hidden. `portal/page.tsx` 73–75:

```73:75:apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx
              const isActiveOrTrialing = sub.status === "ACTIVE" || sub.status === "TRIALING";
              const isHealthyActive = sub.status === "ACTIVE" && !sub.cancel_at_period_end;
              const isHealthyForCancel = isActiveOrTrialing && !sub.cancel_at_period_end;
```

`isHealthyForCancel` drives both cancel buttons. `isHealthyActive` drives plan change — a trial can leave, it cannot change plan from the portal. Ops (`SubscribersPage.tsx` 574–668) allows TRIALING plan/seats and TRIALING period-end cancel. Policy `GuardLiveStatus` allows TRIALING, so ops is consistent with the handler. Portal is stricter. Not a bug.

GDPR does not use the decision table. `ClientProfileAnonymizedIntegrationEventHandler.cs` 55–63 loads every non-CANCELED row for the profile, including PENDING and TRIALING, and calls `Cancel()` plus the typed event. That is the one path that can cancel PENDING. Decision-table HTTP cannot.

---

## Line-by-line walk — snapshot, seats, Gross, interval

```18:32:apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs
    public static decimal Unit(Subscription sub, Product product)
    {
        ArgumentNullException.ThrowIfNull(sub);
        ArgumentNullException.ThrowIfNull(product);
        return sub.UnitAmount > 0 ? sub.UnitAmount : product.Price;
    }

    public static int Seats(Subscription sub)
    {
        ArgumentNullException.ThrowIfNull(sub);
        return Math.Max(1, sub.Quantity);
    }

    public static decimal Line(Subscription sub, Product product) =>
        Unit(sub, product) * Seats(sub);
```

`UnitAmount > 0` is a **missing-sentinel**, not a price. Wave 3 migration defaulted `UnitAmount` to `0m` (`20260820120000_AddWave3SubscriptionBilling.cs` 43–51). Pre-wave rows therefore fall back to catalog on the first renewal, which is the intended backfill. A deliberate $0 snapshot (100% coupon forever, COMPED-as-price, negotiated free seats) also looks missing and is replaced with `product.Price`. That is B02-C10.

Gross (`34–63`) is: SST tax on the **unit** net, then `unitGross * seats`. `SstTaxMath.Compute` (`SstTaxMath.cs` 8–24) returns 0 unless the merchant has an SST registration, the product type is `02`, rate > 0, and net > 0. Rounding is `AwayFromZero` at 2 dp **per unit**, then multiplied. 8% of 100 × 3 = 324 exactly (tested). 8% of 33.33 × 3 is 8.01 via this helper and 8.00 if you tax the line. Sen-level. B02-C21.

`AdvanceFrom` (`94–97`) is `AddYears(1)` if interval is `yr` (ordinal ignore case), else `AddMonths(1)`. Unknown / null / `one_time` / `mo` all add one month. There is no end-of-month anniversary pin. 31 Jan + 1 month = 28 Feb; next add is 28 Mar. Combined with “advance from UtcNow on payment,” the paid-through day walks.

`ResolveInterval` (`99–107`) prefers `sub.BillingInterval` over `product.Interval`. The webhook uses this. Resume-collection uses this. Stats SQL does **not**. Record-payment does **not**. Plan-change apply does **not**. That split is the interval family of P1s.

### Activation snapshot

```11:39:apps/lazuar-api/Modules/Commerce/Application/SubscriptionActivation.cs
    public static void Start(
        Subscription subscription,
        Product product,
        int quantity,
        decimal unitAmount,
        bool reminderOnly,
        string? billingInterval = null,
        Guid? priceId = null,
        DateTime? now = null)
    {
        var instant = now ?? DateTime.UtcNow;
        var interval = string.IsNullOrWhiteSpace(billingInterval) ? product.Interval : billingInterval;

        if (IsTrialOffer(product))
        {
            subscription.ActivateTrial(instant.AddDays(product.TrialDays), reminderOnly, quantity, unitAmount);
        }
        else
        {
            var next = SubscriptionBillingAmount.AdvanceFrom(instant, interval);
            subscription.Activate(instant, next, reminderOnly, quantity, unitAmount);
        }

        subscription.SetBillingInterval(interval);
        if (priceId.HasValue)
        {
            subscription.SetPriceId(priceId);
        }
    }
```

First paid activate writes `CurrentPeriodEnd = instant` (period **start**) and `NextBillingDate = instant + interval` (the date every other layer treats as paid-through). Trial activate writes both clocks to trial end. After a trial converts via webhook `Activate(UtcNow, UtcNow+interval)`, `CurrentPeriodEnd` becomes “now” again and `TrialEndsAt` is **left in place** (B02-C14). Portal paid-through is not that column. Portal SQL aliases `NextBillingDate as CurrentPeriodEnd` (`CommerceQueryService.Portal.cs` 41–42). Webhook payload does the same (`CommerceWebhookPayload.cs` 77–89). The column `Subscriptions.CurrentPeriodEnd` is a lie for paid rows and the truth for trials. B02-C16.

---

## Line-by-line walk — plan change and seats

### Policy

```14:34:apps/lazuar-api/Modules/Commerce/Application/PlanChangePolicy.cs
    public static PlanChangePreview Preview(Subscription sub, Product currentProduct, Product targetProduct, int quantity)
    {
        ArgumentNullException.ThrowIfNull(sub);
        ArgumentNullException.ThrowIfNull(currentProduct);
        ArgumentNullException.ThrowIfNull(targetProduct);

        var qty = Math.Max(1, quantity);
        var currentUnit = sub.UnitAmount > 0 ? sub.UnitAmount : currentProduct.Price;
        var nextUnit = targetProduct.Price;

        return new PlanChangePreview(
            sub.ProductId,
            currentUnit * Math.Max(1, sub.Quantity),
            currentProduct.Currency,
            currentProduct.Interval,
            targetProduct.Id,
            nextUnit * qty,
            sub.NextBillingDate,
            AmountDueNow: 0m,
            Policy: NextRenewal);
    }
```

`nextUnit` is the target **default catalog** price (`Product.Price`), not `target.GetPrice(sub.BillingInterval)`. Preview `Interval` is the **current product default**, not `BillingInterval`. `AmountDueNow` is always 0. Tests pin the zero and the 2×40 / 2×90 arithmetic (`PlanChangePolicyTests.Preview_MidCycle_AmountDueNowZero_EffectiveAtNextBill`). They do not pin a yearly seat on a monthly-default product.

`GuardLiveStatus` allows only ACTIVE and TRIALING. PAST_DUE must update payment first (portal message) or gets the generic throw (admin).

`GuardTargetProduct` (58–89) requires same org, active, `mo|yr`, same gateway, same currency, **same `Product.Interval`**. It does not look at `sub.BillingInterval`. A yearly checkout against a product whose default write-through is monthly has `BillingInterval = "yr"` and `current.Interval = "mo"`. The merchant can only schedule another monthly-default product. The job will then snapshot that product’s **monthly** default. B02-C03.

`RejectImmediateOrProrate` is the 400 for `prorate=true` / `apply=immediate`. Handlers call it first.

### Admin change-plan

```34:50:apps/lazuar-api/Modules/Commerce/Application/Commands/ChangePlanCommandHandler.cs
        if (request.ProductId is null || request.ProductId == subscription.ProductId)
        {
            subscription.ClearPendingPlanChange();
            await _repository.SaveChangesAsync(ct);
            return PlanChangePolicy.Preview(subscription, current, current, subscription.Quantity);
        }
        // ...
        PlanChangePolicy.GuardTargetProduct(subscription, current, target);
        subscription.SchedulePlanChange(target.Id);
        await _repository.SaveChangesAsync(ct);
        return PlanChangePolicy.Preview(subscription, current, target, subscription.PendingQuantity ?? subscription.Quantity);
```

`SchedulePlanChange` (`Subscription.cs` 207–222): empty guid throws; same as current **clears** pending; else writes `PendingProductId`. It does **not** touch `ProductId`, `UnitAmount`, or `Quantity`. Tests: `Schedule_SetsPending_DoesNotMutateProductId`.

Admin does **not** reject `CancelAtPeriodEnd`. A flagged ACTIVE can carry a pending product that the job will throw away, because ProcessOne finalizes cancel **before** `ApplyPendingPlanChange`. B02-C19.

Portal extra guards (`ChangePortalPlanCommandHandler.cs` 40–48): PAST_DUE → “Update payment first”; flagged → “Keep the current plan before scheduling a different product.” Portal also refuses to show the picker on TRIALING (`isHealthyActive`). Admin/ops will schedule a trial plan change; the job applies it on the trial-end tick if the trial is not flagged.

### Quantity

```243:276:apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs
    public void ScheduleQuantity(int qty)
    {
        if (qty < 1 || qty > 99)
        {
            throw new InvalidOperationException("Quantity must be between 1 and 99.");
        }

        if (qty == Quantity)
        {
            PendingQuantity = null;
            UpdatedAt = DateTime.UtcNow;
            return;
        }

        PendingQuantity = qty;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool ApplyPendingQuantity()
    {
        if (PendingQuantity is not int qty)
        {
            return false;
        }
        // ...
        Quantity = qty;
        PendingQuantity = null;
        return true;
    }
```

`SetSubscriptionQuantityCommandHandler` (`ChangePlanCommandHandler.cs` 54–86) is the only writer. It calls `GuardLiveStatus` and `ScheduleQuantity`. It does not call `ExecuteOffSessionCharge`. Preview uses `request.Quantity` against current or pending product. There is **no** `SetSubscriptionQuantityCommandHandler` test. There is **no** `RunOnce_AppliesPendingQuantity` test. The job does call `ApplyPendingQuantity()` after plan apply (line 244). Order:

1. `ApplyPendingPlanChange` → `SetSnapshot(newUnit, sub.Quantity)` using the **old** seat count.
2. `ApplyPendingQuantity()` overwrites `Quantity`.
3. `Gross(sub, product, …)` uses the **new** seat count.

Final persisted snapshot is new unit + new seats. Charge is new unit × new seats × SST. The intermediate `SetSnapshot(..., oldQty)` is overwritten. Functionally correct. Untested. A missing-product return between (1) and (2) would persist new ProductId + old Quantity and drop the pending qty (B02-C02 swallows it).

Portal has no seat stepper. Ops does (`SubscribersPage.tsx` 599–614).

### Collection pause

Domain (`Subscription.cs` 171–199): pause only from ACTIVE; `until` must be in the future; status stays ACTIVE; `IsCollectionPaused(utcNow)` is `CollectionPausedUntil > utcNow`. PAST_DUE pause throws (tested). TRIALING pause throws (no test; domain `Status != "ACTIVE"`). Resume clears the date and optionally pushes `NextBillingDate` if the provided next is later than current.

Resume handler (`ChangePlanCommandHandler.cs` 128–136): if `NextBillingDate` is null or **in the past**, set next = `AdvanceFrom(UtcNow, ResolveInterval(...))`. That **skips** the invoice that came due during the holiday.

The job, when a pause **expires** (date in the past, claim now succeeds), does **not** skip. It charges the still-stale `NextBillingDate` as a normal due tick. Auto-expiry collects; manual resume forgives. B02-C08. W3-LP-057-done said “does not roll `NextBillingDate`” as the feature. Rolling on skip is optional. The resume/expire split is the bug.

Pause HTTP is `POST /admin/commerce/subscribers/{id}/collection/pause|resume` with **no** `.RequireAuthorization("OrgMember")`. The group is `OrgRead` (`Endpoints.cs` 23). Cancel/keep/record-payment are OrgMember. A reader token can pause collection, resume collection, change plan, and set seats. B02-C11. `CommerceEndpointsAuthorizationTests` never maps those four routes.

---

## Line-by-line walk — BillingEngineJob

### Loop

Hosted `BackgroundService`. Interval `BackgroundWorkers:BillingEngineInterval` default `01:00:00` (`BackgroundWorkerOptions.cs` 27, `appsettings.json` 112). Outer try/catch logs and continues. One cycle is `ProcessBillingAsync` (also `RunOnceAsync` for tests).

```68:121:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs
    private async Task ProcessBillingAsync(CancellationToken ct)
    {
        var failedIds = new HashSet<Guid>();

        for (var i = 0; i < BatchSize; i++)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            // ...
                if (db.Database.IsRelational())
                {
                    tx = await db.Database.BeginTransactionAsync(ct);
                    sub = await ClaimDueSubscriptionAsync(db, failedIds, ct);
                    if (sub == null)
                    {
                        await tx.RollbackAsync(ct);
                        break;
                    }
                }
                else
                {
                    sub = await ClaimDueSubscriptionInMemoryAsync(db, failedIds, ct);
                    if (sub == null) break;
                }

                try
                {
                    await ProcessOneSubscriptionAsync(..., sub, failedIds, ct);
                    await db.SaveChangesAsync(ct);
                    if (tx != null) await tx.CommitAsync(ct);
                }
                catch (Exception ex)
                {
                    failedIds.Add(sub.Id);
                    // rollback if tx
                }
```

`BatchSize = 50`. One scope, one DbContext, one transaction per slot. `IBillingQueryService` is resolved from the scope (`Billing` registers it scoped). SST on renewals works in process if Billing is composed, which the host does.

There is **no** `processedIds`. Contrast the sibling worker, which learned this:

```43:77:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.Claim.cs
            var excludeIds = new HashSet<Guid>(failedIds);
            excludeIds.UnionWith(processedIds);
            // ...
                    await db.SaveChangesAsync(ct);
                    if (tx != null) await tx.CommitAsync(ct);
                    processedIds.Add(sub.Id);
```

Billing only excludes `failedIds`. A row that is processed **successfully** but remains claimable (same due predicate) is slot-2’s first candidate. That is B02-C01.

Relational rollback on exception means in-memory mutations from a thrown `ProcessOne` do not persist. In-memory tests have no transaction: a throw leaves the tracked entity mutated and does not `SaveChanges`. Tests share one context. Production is Postgres. Call that out when a test is the only observer.

### Claim SQL

```129:148:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs
        var excludeClause = excludeIds.Count == 0
            ? ""
            : $""" AND "Id" NOT IN ({string.Join(",", excludeIds.Select(id => $"'{id}'"))})""";

        var sql = $"""
            SELECT * FROM commerce."Subscriptions"
            WHERE "NextBillingDate" IS NOT NULL
              AND "NextBillingDate" <= NOW()
              AND "Status" NOT IN ('PENDING', 'PAST_DUE', 'SUSPENDED', 'CANCELED')
              AND ("CollectionPausedUntil" IS NULL OR "CollectionPausedUntil" <= NOW())
              {excludeClause}
            ORDER BY "NextBillingDate"
            LIMIT 1
            FOR UPDATE SKIP LOCKED;
            """;

        return await db.Subscriptions
            .FromSqlRaw(sql)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ct);
```

Included: ACTIVE, TRIALING, and any future string we invent.  
Excluded: PENDING, PAST_DUE, SUSPENDED, CANCELED.  
Also excluded, after `911d358`: currently paused (`CollectionPausedUntil > NOW()`).  
Not excluded: `CancelAtPeriodEnd` (handled after claim — correct, so a due flagged row is finalized).  
Not excluded: `IsReminderOnly`, `HasOpenDispute`, “already has ChargeAttemptLog for this date”, `BillingInterval`.

`IgnoreQueryFilters()` is platform-wide on purpose. The worker has no ambient tenant.

`NOW()` vs `timestamp with time zone` (`CommerceDbContextModelSnapshot.cs` 525–526, 500–501): both are timestamptz. `NOW()` is an absolute instant. Session TimeZone does not make a paused UTC timestamp look expired. In-memory uses `DateTime.UtcNow`. The clocks agree for this host (API + Postgres in UTC). Residual: if a future operator sets the Postgres session TZ and also stores `timestamp without time zone`, comparisons rot. Today the columns are timestamptz. Not a live bug. Labeled speculation under B02-C18.

`excludeClause` is string concatenation into `FromSqlRaw`. The values are `Guid.ToString()`, which is hex plus dashes. There is no user-controlled string in that clause. This is **not** a SQL injection with the current type. It is still the one raw concat in the claim path. B02-C13 (P2 hygiene). A parameterized `NOT IN` (or `processedIds` so the clause is used for successes too) is the fix direction.

In-memory claim (151–168) is the same predicate, including pause and `!excludeIds.Contains`. Tests exercise this path only (`UseInMemoryDatabase`). The SQL string is not executed in CI.

### ProcessOne order

Order is the product. A bug here is a money bug.

**1. Load product + prices.** Missing → `failedIds.Add`, return, then **SaveChanges** (no mutation). Sibling can be claimed because the orphan is excluded for the rest of this cycle. Test: `RunOnce_MissingProduct_DoesNotThrowBatch_SiblingStillProcessed`. Next hour the orphan is due again and burns one slot. Acceptable.

**2. `one_time` interval.** `failedIds.Add`, return. A one-time row with a due date is a zombie. Test: `RunOnce_OneTimeProduct_DoesNotPastDueOrCharge`. Without `failedIds` it would starve like pause used to. They added `failedIds` here.

**3. Collection pause (defense in depth).**

```201:206:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs
        if (sub.IsCollectionPaused(DateTime.UtcNow))
        {
            failedIds.Add(sub.Id);
            _logger.LogInformation("Billing skipped collection-paused subscription {Id} until {Until}.", sub.Id, sub.CollectionPausedUntil);
            return;
        }
```

Claim already excludes pause, so this is a race: pause written after claim, before process. They add `failedIds` so the same cycle does not reclaim. They do **not** roll `NextBillingDate`. Tests: `RunOnce_CollectionPaused_SkipsChargeAndKeepsActive`, `..._SiblingStillProcessed`, `..._SecondCycleDoesNotStarveSibling`, `RunOnce_FiftyPausedDue_DoesNotBlockOneSibling`. Fifty paused + one sibling is the `911d358` proof. **Re-verified fixed.** Do not re-open.

**4. Cancel at period end.**

```208:221:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs
        if (sub.CancelAtPeriodEnd)
        {
            sub.Cancel();
            await eventBus.PublishAsync(new SubscriptionCanceledIntegrationEvent(
                sub.OrganizationId,
                sub.Id,
                sub.ClientProfileId,
                sub.ProductId,
                product.FulfillmentTargets.ToList()));
            // ...
            return;
        }
```

No charge. No mint. No PAST_DUE. No pending apply. Status becomes CANCELED, which the next claim excludes, so this path does not starve. Tests: flagged due vaulted cancels and sibling still charges; flagged due reminder does not mint; flagged **future** is not claimed.

There is **no** test that a flagged **TRIALING** due row cancels without charging. The branch does not read Status. If `CancelAtPeriodEnd` is true and the row was claimed, it cancels. Domain `ScheduleCancelAtPeriodEnd` allows TRIALING. Decision allows TRIALING. The job is consistent. Test gap, not a logic hole. 616b37d’s “does not charge” is this `return` before the charge block.

Is this “finalized too early”? The job uses the same clock the portal calls paid-through (`NextBillingDate`). `CurrentPeriodEnd` on a paid ACTIVE is usually the activation instant and is already in the past; if they had used that column they would cancel immediately after first activate. They did not. Relative to the advertised period end, finalize-on-due is correct. Relative to a merchant who thinks “end of calendar day in MYT,” UTC midnight is early. B02-C18.

**5. Apply pending plan.**

```223:242:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs
        if (sub.ApplyPendingPlanChange())
        {
            product = await db.Products.IgnoreQueryFilters().Include(p => p.Prices).FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);
            if (product == null)
            {
                failedIds.Add(sub.Id);
                _logger.LogWarning(
                    "Billing skipped subscription {Id}: pending product {ProductId} is missing.",
                    sub.Id, sub.ProductId);
                return;
            }

            var pendingPrice = product.Prices.FirstOrDefault(p => p.Interval == product.Interval) ?? product.DefaultPrice();
            sub.SetSnapshot(pendingPrice?.Amount ?? product.Price, sub.Quantity);
            sub.SetBillingInterval(pendingPrice?.Interval ?? product.Interval);
            if (pendingPrice != null)
            {
                sub.SetPriceId(pendingPrice.Id);
            }
        }
```

`ApplyPendingPlanChange` (`230–241` of the aggregate) writes `ProductId = pending` and clears `PendingProductId` **before** the reload. If the target row is gone, ProcessOne returns **without throwing**. The caller still `SaveChanges`. The subscription now points at a missing product, pending is gone, snapshot is the old plan. Next tick hits the **first** missing-product branch forever. B02-C02.

If the product exists, snapshot is `Prices.FirstOrDefault(p => p.Interval == product.Interval)` else `DefaultPrice()` else `product.Price`. That is the catalog **default** interval, not `sub.BillingInterval`. Combined with `GuardTargetProduct` requiring matching **catalog** intervals, a yearly seat scheduled onto another monthly-default catalog lands on the monthly amount. Test `RunOnce_AppliesPendingProductThenChargesNewPrice` uses two monthly products at 50 and 80. It cannot see B02-C03.

Apply is **eager relative to charge success**. Off-session publish happens after the swap. If the charge later fails, the row is PAST_DUE on the **new** product. That is a product choice (next-renewal apply at the due tick, money may bounce). Not filed as a bug.

Apply once: pending is null after success. A second tick cannot apply twice unless someone writes `PendingProductId` again. “Applied twice” as a live bug is not in this tree. The missing-product commit is the live cousin.

**6. Apply pending quantity.** Then compute Gross.

```244:246:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs
        sub.ApplyPendingQuantity();
        var merchantHasSst = await SubscriptionBillingAmount.MerchantHasSstAsync(billing, sub.OrganizationId);
        var chargeAmount = SubscriptionBillingAmount.Gross(sub, product, merchantHasSst);
```

`eba0741` is here. `UnitAmount` is not written. Test `RunOnce_SstStub_OffSessionChargesGross108` asserts `sub.UnitAmount == 100m` and event `Amount == 108m`. `RunOnce_QuantityTimesUnitAmount` asserts 3 × 50 = 150 with no SST. **Re-verified fixed** for the off-session path.

`billing` is optional. If the worker scope cannot resolve `IBillingQueryService`, `MerchantHasSstAsync` returns false and Gross = net. Billing’s DI registers the service. Host composition includes Billing. Not a bug unless someone runs Commerce without Billing (not this host).

**7. Off-session vs mint.**

```248:287:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs
        var canCharge = PaymentGatewayCapabilities.SupportsOffSession(product.GatewayName)
                        && !sub.IsReminderOnly
                        && !string.IsNullOrEmpty(sub.VaultedTokenId)
                        && !string.IsNullOrEmpty(sub.VaultedCustomerId);

        if (canCharge)
        {
            var targetDate = sub.NextBillingDate!.Value.Date;
            var attemptCount = await db.ChargeAttemptLogs
                .CountAsync(l => l.SubscriptionId == sub.Id && l.TargetBillingDate == targetDate, ct);

            if (attemptCount == 0)
            {
                var attempt = new ChargeAttemptLog(
                    sub.Id,
                    targetDate,
                    attemptNumber: 1,
                    source: ChargeAttemptLog.SourceBilling);
                db.ChargeAttemptLogs.Add(attempt);

                await eventBus.PublishAsync(new Modules.Payments.Contracts.Events.ExecuteOffSessionChargeIntegrationEvent(
                    sub.OrganizationId,
                    sub.Id,
                    chargeAmount,
                    product.Currency,
                    sub.VaultedCustomerId!,
                    sub.VaultedTokenId!,
                    DunningCampaignId: null,
                    GatewayName: product.GatewayName,
                    ChargeAttemptId: attempt.Id
                ));
            }

            return;
        }
```

`SupportsOffSession` is Stripe or CHIP only (`PaymentGatewayCapabilities.cs` 10–14). Billplz with junk vault still mints (`RunOnce_BillplzOrReminderOnlyOrNoVault_MarksPastDue_DoesNotPublishOffSession`). Reminder-only wins over a live vault.

`targetDate` is `NextBillingDate.Date` — UTC calendar day, time stripped, `DateTimeKind.Unspecified`. The unique index is `(SubscriptionId, TargetBillingDate, AttemptNumber)`. Two dues on the same UTC day cannot both have attempt 1. A due at 2026-08-17 23:00 UTC and a later recover-to 2026-08-17 23:30 UTC would collide if anyone tried to insert another attempt 1; the count check stops the insert. The cycle key is a UTC date, not the instant. B02-C18.

If `attemptCount == 0`, insert attempt 1 and publish. **Do not** advance dates. **Do not** flip TRIALING → ACTIVE. **Do not** add `failedIds`. Then `return`. SaveChanges commits the attempt (and any pending apply). The row is still ACTIVE/TRIALING with `NextBillingDate` in the past.

If `attemptCount != 0`, do nothing. **Do not** add `failedIds`. Then `return`. The row is still due.

Slot 2 of the same `RunOnce` claims by `ORDER BY NextBillingDate`. This row is still first. It no-ops. Slots 3–50 no-op. One Stripe due row consumes the entire hourly batch after it has been dispatched. Two Stripe dues in the same hour: **one** charge event. 200 Stripe dues: 200 hours, one worker. Multiple API replicas help only while their transactions overlap (`SKIP LOCKED`); each replica then wastes its remaining slots on already-dispatched rows. This is the same class of bug `911d358` fixed for pause, still live for the happy path. B02-C01.

Payments `ExecuteOffSessionChargeIntegrationEventHandler` passes `@event.Amount` straight into `ChargeOffSessionAsync`. There is no quantity field on the event. Seats cannot be squared on off-session. Hunt item “off-session Gross double-count” is **not** a bug.

The job does **not** look at `HasOpenDispute`. A disputed ACTIVE vault is still `canCharge`. B02-C09. The flag is written now (`CommerceGatewayDisputeCreatedHandler.cs` 82, 124; tests assert true). 008 said the flag was dead. Half of that is fixed. The job half is not.

**8. Mint + PAST_DUE.**

If `!canCharge`: load CRM email. No email → PAST_DUE with no URL (warning). Email + missing mediator/one/tokens → **throw** (catch → failedIds → retry next cycle; status stays ACTIVE). Email + services → `RenewalCheckoutIssuer.MintAsync` then `SetCurrentRenewalCheckout(url, NextBillingDate)`. Then `MarkAsPastDue`, start PAST_DUE dunning (out of slice except that it runs), emit `subscription.past_due` with Gross in the payload.

Mint throw keeps ACTIVE (`RunOnce_NonVaultedGenerateThrows_DoesNotMarkPastDue_RetriesNextTick`). That is why mint failure uses the exception path, not a quiet skip: they want a retry, and they do **not** want PAST_DUE without a bill. After success, status PAST_DUE excludes the row from the next claim. Mint path does not starve.

### RenewalCheckoutIssuer — seats vs Quantity 1

```54:65:apps/lazuar-api/Modules/Commerce/Application/RenewalCheckoutIssuer.cs
        var url = await mediator.Send(new GenerateCheckoutSessionQuery(
            sub.OrganizationId,
            await SubscriptionBillingAmount.Gross(sub, product, billing),
            product.Currency,
            product.Name,
            customerEmail,
            successUrl,
            cancelUrl,
            metadata,
            SetupFutureUsage: true,
            Quantity: 1,
            GatewayName: product.GatewayName), ct);
```

`GenerateCheckoutSessionQuery` is documented (`GenerateCheckoutSessionQuery.cs` 7–11): `Amount` is unit; `Quantity` multiplies inside the adapter; callers with a line total **must** pass `Quantity = 1`. This caller has Gross (unit × seats + tax) and passes 1. That is the correct pairing. Hunt item “seats vs Quantity:1 on mint vs off-session Gross double-count” is **not** a live bug on this tree. There is no mint-of-3-seats test. If someone later “fixes” Quantity to `sub.Quantity`, they will square. Test gap under tests-that-lie.

Success URL is `/{slug}/portal` with no token. Cancel URL is `/{slug}/update-payment/{sub.Id}?token=...`. Metadata `type=commerce_subscription`. Arrears auth is slice 03 (`9b531d2`).

`SetCurrentRenewalCheckout` stores `forDate.Date` as UTC. Dunning (out of slice) only attaches the URL when that date still matches.

---

## Line-by-line walk — trial convert

A trial is due when `NextBillingDate` (`= TrialEndsAt`) ≤ now. Claim includes TRIALING.

| Trial row at due tick | What ProcessOne does |
|-----------------------|----------------------|
| Flagged `CancelAtPeriodEnd` | Cancel. No charge. Stays out of convert. 616b37d. |
| Stripe/CHIP + vault + not reminder-only | Attempt 1 off-session. **Status remains TRIALING.** Dates frozen. |
| Reminder-only / Billplz / no vault | Mint + PAST_DUE. Never becomes ACTIVE unless they pay. |
| Not due | Not claimed. `RunOnce_TrialNotDue_DoesNotCharge`. |

Convert to ACTIVE is **not** the job. It is the success webhook:

```64:85:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs
        var periodEnd = DateTime.UtcNow;
        var interval = SubscriptionBillingAmount.ResolveInterval(existingSub, productInfo);
        var catalogUnit = existingSub.PriceId.HasValue
            ? (productInfo.Prices.FirstOrDefault(p => p.Id == existingSub.PriceId)?.Amount ?? productInfo.Price)
            : productInfo.Price;
        var updatedNextBilling = SubscriptionBillingAmount.AdvanceFrom(DateTime.UtcNow, interval);

        if (wasSuspended)
        {
            existingSub.Resume(updatedNextBilling);
        }
        else if (existingSub.Status == "PAST_DUE")
        {
            existingSub.RecoverFromPayment(periodEnd, updatedNextBilling);
        }
        else
        {
            existingSub.Activate(periodEnd, updatedNextBilling, existingSub.IsReminderOnly);
        }

        existingSub.RefreshSnapshot(catalogUnit);
```

TRIALING is the `else`: `Activate(...)` sets ACTIVE and rolls dates from **UtcNow**, not from `TrialEndsAt`. If the off-session succeeds three days late, the next anniversary is three days late. `H6_ActiveRenewal_DoesNotIncrement` pins that an ACTIVE renewal **does** move `NextBillingDate`. There is no test that a TRIALING success becomes ACTIVE. The `else` branch is the same as ACTIVE renewal.

If the off-session **fails**, `GatewayPaymentFailedIntegrationEventHandler` marks PAST_DUE from TRIALING (`becamePastDue = sub.Status != "PAST_DUE"`). The trial never spends a tick as ACTIVE. `TrialEndsAt` remains. B02-C14 / convert-to-arrears.

If the off-session **never** comes back (adapter hang, inbox drop), the row stays TRIALING with attempt 1 and a past `NextBillingDate`. The job will not charge again (`attemptCount != 0`). Dunning will not pick it (not PAST_DUE). Combined with B02-C01, that row also occupies the claim queue. Stuck trial. B02-C15 (P1, consequence).

`RefreshSnapshot(catalogUnit)` runs on **every** successful subscription payment, including ACTIVE renewals. The snapshot the job just charged is replaced with the live catalog amount for `PriceId` or `product.Price`. Wave 3’s “catalog edits do not move MRR” is true **until the next successful payment**. Then UnitAmount becomes the catalog. B02-C04. `CommerceMrrTests.CatalogEditDoesNotChangeSnapshotMath` only tests the helper with a frozen argument; it never goes through the webhook.

`Resume` (SUSPENDED success) does not write `CurrentPeriodEnd`. Portal does not use that column. B02-C17.

Record-payment on TRIALING is not arrears, so it calls `Activate` and converts early. Next date:

```88:90:apps/lazuar-api/Modules/Commerce/Application/Commands/RecordSubscriberPaymentCommandHandler.cs
        var nextBilling = request.NextBillingDate
            ?? (product.Interval == "yr" ? periodEnd.AddYears(1) : periodEnd.AddMonths(1));
```

That is `product.Interval`, not `ResolveInterval`. A yearly seat recorded-paid by a clerk gets +1 month. B02-C05. Tests `R1_ActivePaid_AdvancesFromNow` use a monthly product.

---

## Line-by-line walk — MRR

```11:38:apps/lazuar-api/Modules/Commerce/Application/CommerceMrr.cs
    public static decimal MonthlyEquivalent(
        string status,
        DateTime? collectionPausedUntil,
        DateTime utcNow,
        string? interval,
        decimal unitAmount,
        int quantity,
        decimal fallbackUnit = 0m)
    {
        if (!string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        if (collectionPausedUntil.HasValue && collectionPausedUntil.Value > utcNow)
        {
            return 0m;
        }

        if (interval is not ("mo" or "yr"))
        {
            return 0m;
        }

        var unit = unitAmount > 0 ? unitAmount : fallbackUnit;
        var line = unit * Math.Max(1, quantity);
        return interval == "yr" ? line / 12m : line;
    }
```

Helper: TRIALING 0, PAST_DUE 0, paused ACTIVE 0, one_time 0, yearly ÷ 12, snapshot wins over fallback. Tests cover those cases. **The helper is honest.**

Stats SQL is not:

```33:61:apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Stats.cs
        const string subSql = @"
            SELECT 
                s.""Status"" as Status, s.""CreatedAt"" as CreatedAt, s.""UpdatedAt"" as UpdatedAt, 
                p.""Price"" as Price, p.""Interval"" as Interval,
                s.""UnitAmount"" as UnitAmount, s.""Quantity"" as Quantity,
                s.""CollectionPausedUntil"" as CollectionPausedUntil
            FROM commerce.""Subscriptions"" s
            JOIN commerce.""Products"" p ON s.""ProductId"" = p.""Id""
            WHERE s.""OrganizationId"" = @OrgId AND s.""Status"" != 'PENDING'";
        // ...
        var activeSubs = subs.Where(s => s.Status == "ACTIVE" || s.Status == "PAST_DUE").ToList();
        var mrr = subs.Sum(s => CommerceMrr.MonthlyEquivalent(
            s.Status,
            s.CollectionPausedUntil,
            now,
            s.Interval,
            s.UnitAmount,
            s.Quantity,
            s.Price));
        // ...
        double arpu = activeSubs.Count > 0 ? (double)(mrr / activeSubs.Count) : 0;
```

`s.Interval` is `p."Interval"`. `s."BillingInterval"` is never selected. A yearly seat on a monthly-default product is counted as monthly (12× too much). ARR is `mrr * 12`. B02-C06.

`activeSubs` for the ARPU denominator is ACTIVE **or** PAST_DUE. MRR already zeroed PAST_DUE. ARPU is diluted by non-paying arrears. B02-C07.

TRIALING and paused ACTIVE are excluded from MRR by the helper. They are not in `activeSubs` (TRIALING is not ACTIVE or PAST_DUE; paused ACTIVE is). Hunt item “MRR including trials/paused/past-due wrongly”: trials and paused are **not** included. PAST_DUE is not included in the sum. PAST_DUE **is** included in ARPU’s count. File the ARPU one, not a phantom MRR-includes-trials bug.

---

## Bug catalog

### B02-C01 — P0 — Vaulted due row starves the 50-slot batch (failedIds / processedIds hole)

**Evidence.** `ProcessBillingAsync` only excludes `failedIds`. Off-session ProcessOne `return`s after dispatch or after “already has attempt 1” **without** `failedIds.Add`. `NextBillingDate` is intentionally not advanced. Claim predicate still matches. Next slot reclaimes the same `ORDER BY NextBillingDate` row. Dunning’s sibling worker adds `processedIds` after every successful process; Billing does not.

Quote: BillingEngineJob.cs 70, 248–286 (the `return` with no `failedIds`), 129–142 (claim does not exclude attempt-1 rows), DunningEngineJob.Claim.cs 43–77 (the pattern they already know).

**Repro.**

1. Insert two ACTIVE Stripe vaulted subs, both `NextBillingDate` yesterday, A earlier than B.
2. `RunOnceAsync`.
3. Observe one `ExecuteOffSessionChargeIntegrationEvent` (A) and zero for B.
4. `RunOnceAsync` again. Observe still no event for B (A still due, attemptCount=1, occupies the claim).
5. Optional: 50 vaulted dues + 1 reminder due. The reminder is never minted in that hour.

Existing tests do **not** fail: `RunOnce_StripeVaulted_PublishesOffSessionAttempt1_DoesNotAdvanceDates` is one row; `RunOnce_VaultedAlreadyHasAttempt1_DoesNotPublishAgain` is one row; `RunOnce_FiftyPausedDue_DoesNotBlockOneSibling` is the pause predicate, not this path.

**Blast radius.** Every Stripe/CHIP auto-renew in the same hour after the first dispatch. One worker ≈ one off-session per interval. Reminder-only siblings behind a vaulted due also wait. Trials that dispatched attempt 1 and hang sit on the same queue.

**Tests that should exist and do not.** Two vaulted dues in one `RunOnce` → two events. Vaulted A with attempt 1 + due sibling B → B still dispatched. Same shape as the pause tests they added in `911d358`.

**Fix direction.** Mirror dunning: `processedIds.Add(sub.Id)` after every successful ProcessOne (including no-op attempt-1 and successful dispatch). Or add `failedIds.Add` on both off-session returns. Or exclude rows that already have a ChargeAttemptLog for `NextBillingDate::date` in the claim SQL. Do **not** roll `NextBillingDate` on dispatch to “fix” this; that re-opens double-charge races the unique attempt log is there to prevent.

---

### B02-C02 — P1 — Missing pending product commits a broken ProductId

**Evidence.** `ApplyPendingPlanChange()` writes `ProductId` and clears pending, then the job reloads. On null product it `failedIds.Add` and `return`s. `ProcessBillingAsync` treats that as success and `SaveChanges`. There is no restore of the old ProductId.

**Repro.** ACTIVE due, `PendingProductId` = random guid not in `Products`. `RunOnce`. Row is still ACTIVE (or TRIALING), `ProductId` is the missing guid, `PendingProductId` is null. Next ticks hit the first missing-product skip forever. Buyer cannot be billed. Ops change-plan undo looks at current ProductId, which is already the ghost.

**Blast radius.** Any scheduled change onto a product that was archived-and-deleted, or a bad id written by hand. Low frequency, high stuckness. No self-heal.

**Tests.** None. `RunOnce_AppliesPendingProductThenChargesNewPrice` only uses a live target. `RunOnce_MissingProduct_*` uses a sub whose **current** product is missing (no pending apply).

**Fix direction.** Apply pending only after the reload succeeds. Or throw (so the transaction rolls back and pending remains). Or restore `ProductId` / `PendingProductId` before return. Never `SaveChanges` a ghost id.

---

### B02-C03 — P1 — Pending plan snapshot uses catalog default interval, not BillingInterval

**Evidence.** `PlanChangePolicy.GuardTargetProduct` compares `target.Interval` to `current.Interval` (catalog defaults). Preview `nextUnit = targetProduct.Price`. Job: `Prices.FirstOrDefault(p => p.Interval == product.Interval) ?? DefaultPrice()`. `SubscriptionActivation` and hop-1 (out of slice, but it writes `BillingInterval`) can put `BillingInterval = "yr"` on a product whose default is `"mo"`.

**Repro.** Product Basic default `mo` RM 50, yearly price row RM 500. Sub ACTIVE, `BillingInterval=yr`, `UnitAmount=500`, Quantity=1. Schedule change to Pro, also default `mo` RM 80 with yearly RM 800. Due tick. Snapshot becomes 80, `BillingInterval` becomes `mo`. Off-session amount 80 (or 86.40 with SST), not 800.

**Blast radius.** Every yearly (or non-default) seat that uses change-plan. Ops picker lists `p.interval` / `p.price` (the default). Merchants with both prices on one product are the Wave 3 shape.

**Tests.** `PlanChangePolicyTests` and `RunOnce_AppliesPendingProductThenChargesNewPrice` are monthly-only.

**Fix direction.** Guard and snapshot via `ResolveInterval(sub, current)` / `target.GetPrice(interval)`. Preview `NextAmount` from that price × seats. Refuse the change if the target has no row for the subscription’s interval (same message as interval swap).

---

### B02-C04 — P1 — Success webhook RefreshSnapshot unfreezes UnitAmount

**Evidence.** `GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs` 66–85. After Activate / Recover / Resume, `RefreshSnapshot(catalogUnit)` where `catalogUnit` is the live `ProductPrice.Amount` for `PriceId`, else `product.Price`.

**Repro.** ACTIVE, `UnitAmount=40` (negotiated), catalog now 90. Due tick charges 40 (or Gross). Webhook success. `UnitAmount` is 90. Next MRR card and next cycle use 90.

**Blast radius.** Every successful renewal, trial convert, arrears pay, clerk record-payment does **not** go through this handler (clerk does not RefreshSnapshot — only the gateway path). Gateway path is all Stripe/CHIP auto-renew and all hosted-bill pays.

**Tests.** `CommerceMrrTests.CatalogEditDoesNotChangeSnapshotMath` lies about this (helper-only). `H6_ActiveRenewal_DoesNotIncrement` asserts dates move, not UnitAmount.

**Fix direction.** Do not `RefreshSnapshot` on renewal unless ProductId/PriceId just changed. If the job already wrote the new plan snapshot, leave it. Catalog edits should move MRR only via an explicit “reprice” verb, which does not exist.

---

### B02-C05 — P1 — Record-payment advances with product.Interval, not BillingInterval

**Evidence.** `RecordSubscriberPaymentCommandHandler.cs` 88–90 vs resume handler 131–133 which **does** call `ResolveInterval`.

**Repro.** Yearly sub, clerk logs a payment with no override date. `NextBillingDate` becomes now+1 month. Next billing job fires in a month and charges the yearly Gross.

**Blast radius.** Ops “Log Payment” on any non-default-interval seat. Tests `R1_ActivePaid_AdvancesFromNow` use monthly and would stay green.

**Fix direction.** `AdvanceFrom(periodEnd, SubscriptionBillingAmount.ResolveInterval(sub, product))`. Keep the clerk override.

---

### B02-C06 — P1 — Stats MRR uses p.Interval, not BillingInterval

**Evidence.** `CommerceQueryService.Stats.cs` 33–54. Helper is correct; the argument is not.

**Repro.** One ACTIVE yearly seat, `UnitAmount=1200`, product default `mo`. Dashboard MRR = 1200, ARR = 14400. Honest monthly equivalent is 100 / 1200.

**Blast radius.** Every mixed-interval catalog. LP-161 “honest snapshot MRR” is false for interval. Unit snapshot is used (good) until B02-C04 overwrites it.

**Tests.** `CommerceMrrTests` never open Stats.cs.

**Fix direction.** `COALESCE(s."BillingInterval", p."Interval") as Interval`.

---

### B02-C07 — P1 — ARPU denominator includes PAST_DUE

**Evidence.** Stats.cs 46 and 61. `activeSubs = ACTIVE || PAST_DUE`. `mrr` already zeros PAST_DUE.

**Repro.** Two ACTIVE @ 100 and one PAST_DUE @ 100. MRR = 200. ARPU = 66.66. Honest ARPU on paying actives is 100.

**Blast radius.** Ops dashboard only. Not money movement.

**Fix direction.** Denominator = rows that contributed to MRR (ACTIVE, unpaused, mo/yr). Or show two numbers.

---

### B02-C08 — P1 — Pause expiry charges the back invoice; manual resume skips it

**Evidence.** Resume handler 128–136 pushes `NextBillingDate` to now+interval when it is already past. Job pause skip (201–206) and claim exclude do **not** roll the date. When `CollectionPausedUntil` becomes ≤ now, the old due is claimed and charged.

**Repro.** ACTIVE due yesterday, pause until tomorrow. Wait (or set the pause date in the past). Job charges. Contrast: same setup, click Resume today, next bill is +1 interval, no charge this cycle.

**Blast radius.** Every collection holiday that ends by the clock rather than the button. W3-LP-057 sold “does not roll” as if both paths agreed.

**Tests.** Domain resume pushes when given a next. Job pause tests assert the date **stays in the past**. They never expire the pause and watch a charge.

**Fix direction.** Pick one product rule and implement both sides. Skip-the-invoice: on pause skip or on expire, set `NextBillingDate = max(CollectionPausedUntil, AdvanceFrom(old, interval))` and `failedIds`. Collect-the-invoice: resume must not jump the clock; charge on resume/expiry. Document it on the ops button.

---

### B02-C09 — P1 — HasOpenDispute is set and billing ignores it

**Evidence.** `MarkHasOpenDispute` exists. `CommerceGatewayDisputeCreatedHandler` calls it when metadata resolves a sub (tests: `HasOpenDispute.Should().BeTrue()`). Claim SQL and `canCharge` do not read the flag. There is no `ClearHasOpenDispute`.

**Repro.** ACTIVE Stripe vaulted, dispute event with `subscription_id`. Flag true. Due tick. Off-session still publishes.

**Blast radius.** Every card that is in chargeback. Charging again during an OPEN dispute is how you lose the next one. 008 said the flag was dead; the writer is alive, the reader is not.

**Fix direction.** Exclude `HasOpenDispute` from claim (and from `canCharge`) **or** delete the column. If you exclude, add a clear-on-won/lost path or the row is paused forever.

---

### B02-C10 — P1 — UnitAmount > 0 sentinel cannot represent a $0 snapshot

**Evidence.** `SubscriptionBillingAmount.Unit` and `CommerceMrr.MonthlyEquivalent` both treat `<= 0` as missing and fall back to catalog / fallbackUnit.

**Repro.** ACTIVE, `UnitAmount=0`, `Quantity=1`, product.Price=100, Stripe vaulted, due. Off-session amount 100 (or 108), not 0. MRR 100, not 0.

**Blast radius.** 100% coupon lifetime, COMPED-as-price if anyone stored 0, Wave 3 default-0 rows that were **meant** to stay free. Pre-wave backfill wanting catalog is the conflicting intent. The same operator cannot express both.

**Tests.** `SnapshotZero_FallsBackToCatalog` **asserts** the sentinel. It will go red if you fix this without a real “missing” nullable.

**Fix direction.** Nullable `UnitAmount` or a `HasSnapshot` bit. `0` must be 0. Missing uses catalog.

---

### B02-C11 — P1 — OrgRead can change plan, set seats, pause and resume collection

**Evidence.** `Endpoints.cs` 23: admin group `RequireAuthorization("OrgRead")`. `SubscriberEndpoints.cs` 157–243: change-plan, quantity, collection pause, collection resume have **no** extra policy. Cancel (98–116), keep (118–132), record-payment (134–155) are OrgMember. Anonymize is OrgAdmin.

**Repro.** Token with OrgRead only. `POST /admin/commerce/subscribers/{id}/collection/pause`. 200 paused. Same for change-plan and quantity.

**Blast radius.** Viewer / read-scoped API keys. Not anonymous (group is not AllowAnonymous). Still a write via a read policy.

**Tests.** `CommerceEndpointsAuthorizationTests.MapCommerceEndpoints_GetSubscribers_Requires_OrgRead` only. No test for the four write routes.

**Fix direction.** `.RequireAuthorization("OrgMember")` on those four, matching cancel.

---

### B02-C12 — P1 — Trial convert can stall in TRIALING after attempt 1 (webhook-dependent, job will not retry)

**Evidence.** Job leaves TRIALING on dispatch (248–286). Failed handler is the only job-adjacent path to PAST_DUE. Completed handler is the only path to ACTIVE from TRIALING. If neither event arrives, claim keeps picking a TRIALING+due+attempt1 row (B02-C01) and dunning will not, because status is not PAST_DUE.

**Repro.** TRIALING due, vaulted. RunOnce (attempt 1 published). Drop the payments inbox. Wait. Status TRIALING, `NextBillingDate` yesterday, one PENDING attempt, no further charges, no mint, no dunning.

**Blast radius.** Any trial whose off-session never completes. Combined with C01, it also blocks other dues.

**Tests.** `RunOnce_TrialNotDue_DoesNotCharge` only. No due-trial test at all.

**Fix direction.** After attempt 1 is already present and still TRIALING/ACTIVE past a grace, mark PAST_DUE (or re-publish). Add `RunOnce_TrialDueVaulted_PublishesAttempt1_StaysTrialing` and a convert test on the webhook. C01’s processedIds at least stops the starve.

---

### B02-C13 — P2 — Claim exclude clause is FromSqlRaw string concat

**Evidence.** BillingEngineJob.cs 129–131. Values are `Guid`. Not exploitable as injection today.

**Repro.** None that breaks out of a Guid. Hygiene review only.

**Fix direction.** EF parameterized `WHERE NOT IN` or `processedIds` as `Guid[]` bound parameter.

---

### B02-C14 — P2 — TrialEndsAt is never cleared

**Evidence.** `ActivateTrial` sets it. `Activate` / `RecoverFromPayment` / `Resume` / `Cancel` do not. Portal hides via Status == TRIALING; ops/API still return `trial_ends_at` on ACTIVE/PAST_DUE/CANCELED. Clear in `Activate` / `RecoverFromPayment`.

### B02-C16 — P2 — CurrentPeriodEnd means start on paid rows and end on trials

**Evidence.** `SubscriptionActivation.Start` passes `instant` as `currentPeriodEnd`. Trial sets both to endsAt. Portal/webhooks advertise `NextBillingDate` as `current_period_end`. Write `CurrentPeriodEnd = next` on paid activate, or stop selecting the column.

### B02-C17 — P2 — Resume() does not set CurrentPeriodEnd

**Evidence.** `Subscription.Resume` (300–308) vs `RecoverFromPayment` (315–325). Webhook uses Resume for SUSPENDED; clerk uses RecoverFromPayment. Use RecoverFromPayment for both.

### B02-C18 — P2 — Cycle key and “period end” are UTC Date, not merchant local

**Evidence.** `NextBillingDate!.Value.Date` for ChargeAttemptLog; `SetCurrentRenewalCheckout` stores `.Date` UTC; claim uses full timestamptz. 2026-09-01 00:00 UTC is 08:00 MYT. **Speculation:** merchants say “bill on the 1st” in MYT. Document UTC or store a merchant-TZ date.

---

### B02-C19 — P2 — Admin can schedule plan/qty on a flagged sub; job discards them

**Evidence.** `ChangePlanCommandHandler` has no `CancelAtPeriodEnd` guard. Portal does. ProcessOne cancels before apply.

**Repro.** Flag period-end. Schedule Pro. Due tick. CANCELED, still on Basic, pending gone.

**Fix direction.** Same portal guard on admin, or apply pending onto the canceled row (usually wrong). Prefer the guard.

---

### B02-C20 — P2 — SST per-unit then × seats can be 1 sen off a line tax

**Evidence.** `GrossBreakdown` taxes `unitNet` then multiplies. `SstTaxMath` rounds to 2 dp first.

**Repro.** Unit 33.33, 8%, 3 seats. Helper 8.01. Line tax 8.00.

**Fix direction.** Tax `unitNet * seats` once if LHDN wants line-level. Out of this slice to decide.

---

### B02-C21 — P2 — Activate-from-arrears no-op dates is a footgun

**Evidence.** `Subscription.cs` 94–99. Webhook and record-payment do **not** use Activate for PAST_DUE. A future caller who “just Activate” after pay leaves the due date in the past → re-claim → second charge. `Activate_FromPastDue_DoesNotAdvanceBillingDates` documents the trap. Fix: throw from PAST_DUE/SUSPENDED so RecoverFromPayment is the only door.

### B02-C22 — P2 — ApplyPendingPlanChange returns true even when pending == current ProductId

**Evidence.** `SchedulePlanChange` clears when ids match; `ApplyPendingPlanChange` does not. A SQL-stuck `PendingProductId = ProductId` re-snapshots from catalog (cousin of C04). Domain Schedule will not write that row. Speculation on how it appears.

### B02-C23 — P2 — Integration cancel is immediate only

**Evidence.** `IntegrationSubscriptionEndpoints.cs` 87: `AtPeriodEnd: false`. Honest vs the contract (no body). Do not document “integrator can schedule period-end.”

---

## Closed 008 items, re-verified

These were named in the task as recently fixed. They are **still fixed** on `297ba98`. Do not re-open.

### Pause batch starvation (`911d358`) — still fixed

Claim SQL line 138: `AND ("CollectionPausedUntil" IS NULL OR "CollectionPausedUntil" <= NOW())`.  
In-memory line 165: same.  
Skip line 203: `failedIds.Add(sub.Id)`.  
Tests 613–711: sibling processed; second cycle does not starve; fifty paused do not block one sibling.

008’s P0 write-up (`plans/008-evals/01-commerce-subscriptions-checkout.md` 433–439) describes the **old** claim without the pause predicate. That paragraph is stale.

What remains related is **not** starvation: B02-C08 (expire vs resume) and B02-C01 (a different row shape, vaulted attempt 1).

### TRIALING cancel (`616b37d`) — still fixed

Decision allow-list includes TRIALING (line 22) and the schedule predicate (35–36).  
Domain `ScheduleCancelAtPeriodEnd` allows TRIALING (`Subscription.cs` 343–344; `SubscriptionTrialTests.ScheduleCancelAtPeriodEnd_AllowsTrialing`).  
HTTP tests: `Admin_Trialing_Immediate_CancelsAndPublishesEvent`, `Admin_Trialing_AtPeriodEnd_Future_StaysTrialingNoEvent`.  
Portal `isHealthyForCancel` includes TRIALING. Ops period-end button includes TRIALING.

Period-end on a **future** trial does not charge: the row is not due, so the job does not claim it (`RunOnce_FlaggedFutureNextBilling_Untouched` is the ACTIVE twin; trial-not-due is tested separately). Period-end on a **due** trial cancels in ProcessOne step 4 before charge. No dedicated trial+flagged+due test; the branch is status-blind.

008’s P0 “Trial cannot be canceled through any HTTP path” is stale.

### SST Gross on renewals (`eba0741`) — still fixed

`chargeAmount = SubscriptionBillingAmount.Gross(...)`.  
`RunOnce_SstStub_OffSessionChargesGross108`: event 108, `UnitAmount` 100.  
`SubscriptionBillingAmountTests.Gross_SstRegistered_Unit100_Rate8_Is108` and qty 3 = 324.  
Mint uses the same Gross helper. Webhook payload uses Gross except TRIALING (0).

008’s P1 “SST on hop 1 only / renewals are net” is stale for this slice. Hop-1 display is report 01.

### Adjacent 008 items, not in the “recently fixed” list

| 008 claim | This tree |
|-----------|-----------|
| Public GUID arrears | Out of slice. `9b531d2` exists; report 03 owns it. |
| Zero-amount Stripe forced reminder-only | Hop-1. `8b3567d` exists; report 01 owns it. |
| HasOpenDispute never set | **Writer is fixed.** Reader (this job) is B02-C09. |
| MRR interval is catalog default | **Still true.** B02-C06. |
| ARPU includes PAST_DUE | **Still true.** B02-C07. |
| Trial convert untested | **Still true.** B02-C12. |

---

## Tests that lie

A test lies when it pins a failure mode as success, or when its name promises a property it does not check.

| Test | What it actually asserts | How it lies / gaps |
|------|--------------------------|--------------------|
| `RunOnce_StripeVaulted_PublishesOffSessionAttempt1_DoesNotAdvanceDates` | One row, one event, dates frozen | Pins the **precondition** of B02-C01 as the desired outcome. Never asks about slot 2. |
| `RunOnce_VaultedAlreadyHasAttempt1_DoesNotPublishAgain` | No second event on the same row | Correct idempotency. Implies the batch is healthy. It is not. |
| `RunOnce_CollectionPaused_SkipsChargeAndKeepsActive` | Skip + date stays past | Alone would not prove isolation; the three newer tests do. This one still does not mention `failedIds`. |
| `RunOnce_TrialNotDue_DoesNotCharge` | Future trial untouched | Named like trial coverage. There is no due-trial convert test, no flagged-trial-due test, no reminder-trial-due test. |
| `RunOnce_AppliesPendingProductThenChargesNewPrice` | Monthly 50 → 80 | Named like plan apply. Does not cover missing target (C02), yearly (C03), or apply-then-mint-throw. |
| `RunOnce_QuantityTimesUnitAmount` | 3 × 50 = 150 on **already applied** Quantity | Does not schedule `PendingQuantity` and let the job apply it. Seats-on-renewal is only half pinned. |
| `RunOnce_SstStub_OffSessionChargesGross108` | Off-session 108 | Does not mint 108. Mint code path is untested for SST (code looks right). |
| `RunOnce_NonVaultedDue_MintsCheckoutBoundToExistingSubscription` | `q.Amount == product.Price`, implicit qty 1 | Does not prove 3-seat Gross with Quantity 1. A future Quantity:3 would still pass this test. |
| `CommerceMrrTests.CatalogEditDoesNotChangeSnapshotMath` | Helper(100, fallback 200) = 100 | Does not run stats SQL. Does not run RefreshSnapshot. Name claims the product rule B02-C04 breaks. |
| `CommerceMrrTests.Trialing_IsZero` / `PastDue_IsZero` / `CollectionPaused_IsZero` | Helper zeros | Honest for the helper. Do not prove Stats.cs. |
| `Activate_FromPastDue_DoesNotAdvanceBillingDates` | Dates frozen | Documents a footgun (C21) as intended. Useful, but any new Activate() caller will ship a double-bill. |
| `SnapshotZero_FallsBackToCatalog` | 0 → catalog | Pins B02-C10 as a feature. |
| `PlanChangePolicyTests.Preview_MidCycle_*` | 2×40 / 2×90 monthly | No yearly row, no BillingInterval. |
| `ChangePlanCommandHandlerTests` | Pending set, undo, 400s | No CancelAtPeriodEnd interaction (C19). No quantity handler tests at all. |
| `ChangePortalPlanCommandHandlerTests` | Foreign 401, prorate 400 | Does not test the PAST_DUE / flagged guards that are the only extra logic. |
| `CommerceEndpointsAuthorizationTests` | list = OrgRead, anonymize = OrgAdmin | Implies subscriber writes are covered. Change-plan / quantity / pause are not. |
| `SubscriptionCancelAtPeriodEndTests.Admin_Trialing_*` | HTTP/decision | Do not go through BillingEngineJob. Job half of 616b37d is unpinned. |
| `H6_ActiveRenewal_DoesNotIncrement` | Dates move, recovery counters stay 0 | Does not assert UnitAmount after RefreshSnapshot. |

Wave done notes that say “Commerce filter **355 passed**” are a point-in-time CI brag. This report did not re-run the suite.

---

## Unread / grepped only

Honesty about what this agent did not fully read, so a later pass does not pretend otherwise.

**Unread (out of slice):** hop-1 `InitiateCheckoutCommandHandler` body; `ProcessZeroAmountCheckoutCommand`; adapter `ChargeOffSessionAsync` bodies; ledger journals; LHDN; Communications hydrator; DunningEngineJob.PreDunning / PastDue / Dispatch (claim file was read only for `processedIds`); `PastDueDunningProcessor`; `PublicArrearsEndpoints`; portal checkout form.

**Grepped only:** `GetSubscriberByIdAsync`; unused `HasChargeAttemptAsync`; ProductEndpoints; Coupon handlers.

**Read in full:** the primary trees in the files table, cancel/keep/change-plan/quantity/pause/resume handlers, record-payment date roll, manual enroll activate, payment completed/failed subscription paths, GDPR cancel handler, dispute handler (flag write), subscriber/portal/integration cancel routes, Stats + Portal SQL alias, DbContext subscription map, Wave 3 columns, `SstTaxMath`, `PaymentGatewayCapabilities`, `ChargeAttemptLog`, `GenerateCheckoutSessionQuery` contract, off-session handler amount pass-through, every test in the files table, portal/ops cancel and seats chrome, commits `911d358` / `616b37d` / `eba0741`, 008 report 01 billing/MRR/cancel sections, `W3-LP-057-done.md`.

---

## Ranked open bugs

P0 first, then P1 by money, then P2. Closed 008 items are **not** in this list.

1. **B02-C01 P0** — Vaulted due / attempt-1 no-op does not enter `failedIds` or `processedIds`. One Stripe renewal occupies the whole hourly batch of 50. Same failure class as the pause bug they just closed; the happy path still has it.
2. **B02-C02 P1** — Pending plan onto a missing product commits a ghost `ProductId` and cannot bill again.
3. **B02-C03 P1** — Plan apply / preview snapshot the catalog default interval, not `BillingInterval`. Yearly seats become monthly charges on change-plan.
4. **B02-C04 P1** — Payment webhook `RefreshSnapshot` overwrites negotiated `UnitAmount` with live catalog after every successful pay.
5. **B02-C05 P1** — Clerk record-payment rolls `NextBillingDate` with `product.Interval`, so a yearly seat is due again in a month.
6. **B02-C12 P1** — Trial convert is webhook-only; a lost off-session leaves TRIALING + attempt 1 + no dunning, and feeds C01.
7. **B02-C08 P1** — Pause expiry collects the back invoice; Resume skips it. Two products, one flag.
8. **B02-C09 P1** — `HasOpenDispute` is now written and still not read by the job. Disputed cards are auto-debited.
9. **B02-C10 P1** — `UnitAmount > 0` cannot mean zero. Free snapshots charge catalog.
10. **B02-C06 P1** — Dashboard MRR uses `p.Interval`. Yearly-on-monthly-default inflates ×12.
11. **B02-C07 P1** — ARPU divides by PAST_DUE heads that contributed 0 MRR.
12. **B02-C11 P1** — OrgRead can mutate plan, seats, and collection pause.
13. **B02-C13 P2** — `FromSqlRaw` Guid concat (not injectable today).
14. **B02-C14 P2** — `TrialEndsAt` leftover after convert / cancel.
15. **B02-C16 P2** — `CurrentPeriodEnd` column is start-of-period on paid rows.
16. **B02-C17 P2** — `Resume` skips `CurrentPeriodEnd`.
17. **B02-C18 P2** — UTC `.Date` cycle key vs merchant-local “bill on the 1st.”
18. **B02-C19 P2** — Admin plan/qty on a flagged sub is silently discarded at finalize.
19. **B02-C20 P2** — SST sen rounding unit-then-seats vs line.
20. **B02-C21 P2** — `Activate` from arrears is a silent date no-op; new callers will double-bill.
21. **B02-C22 P2** — `ApplyPendingPlanChange` does not no-op when pending == current.
22. **B02-C23 P2** — M2M cancel cannot schedule period-end (honesty).

**Not bugs on this tree:**

- Pause skip without `failedIds` — **fixed** (`911d358`). In-memory and SQL both exclude; skip still adds `failedIds`.
- TRIALING cannot be canceled — **fixed** (`616b37d`). HTTP, portal, ops, GDPR.
- Period-end on a future trial charges — **false**. Not claimed until `NextBillingDate`. Flagged due cancels before charge.
- Renewals charge net / SST missing — **fixed** (`eba0741`) for off-session, mint helper, webhook payload.
- Seats × Quantity on mint — **false**. Mint `Quantity: 1` + Gross. Off-session has no quantity field.
- Pending plan applied twice on the happy path — **false**. Pending is cleared after one apply. The live cousin is C02 (apply once onto a ghost).
- MRR includes TRIALING or collection-paused — **false** in the helper. Do not file that.
- Claim SQL injection via `excludeIds` — **not exploitable** with `Guid`. Filed only as P2 hygiene.
- `CancelAtPeriodEnd` finalized from `CurrentPeriodEnd` — **false**. They use `NextBillingDate`, which is what they advertise as paid-through.
- Missing refuse-list features (proration, immediate upgrade, interval swap HTTP, portal seats, `PAUSED` status, M2M enroll) — not bugs.

---

Do not start proration, usage, or a `PAUSED` status while C01 is open. The engine cannot empty its own queue. C01 first (`processedIds` + two-vaulted-dues test), then C02/C03/C04/C05 (money), then C12 trial-due tests, then C08/C09/C10/C11.

*End of report 02. Hop-1 checkout is 01. Dunning / arrears / magic-link is 03. Do not summarize this file into a bullet list and discard the quotes.*
