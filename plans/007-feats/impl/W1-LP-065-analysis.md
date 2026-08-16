# W1-LP-065 — Implementation analysis: Offline / manual payment subscription

**Date:** 16 August 2026  
**ID:** LP-065 (Wave 1 — sellable CaaS)  
**Status:** analysis only. Do not implement from this file.  
**Canonical name:** Offline / manual payment subscription — finish existing `CreateManualSubscriber` / `RecordSubscriberPayment` so the path is honest and complete.

Tracker rows:

- [00-implement-ids.md](../00-implement-ids.md) — `LP-065 | Offline / manual payment subscription`
- [00-checklist-tracker.md](../00-checklist-tracker.md) — Wave 1 `Offline / manual payment sub` (Lazuar **P**). Backlog pairs it with LP-053 as “first-class reminder-only / offline renewals.”
- Evidence (do not reopen as product strategy): [11-subscriptions-lifecycle.md](../11-subscriptions-lifecycle.md) SL-080–SL-086, SL-095; [08-subscription-billing-engines.md](../08-subscription-billing-engines.md) BE-062 / BE-033; [01-lazuar-feature-inventory.md](../01-lazuar-feature-inventory.md) “Manual enroll” / “Record offline payment”

**ID collision (ignore for this ticket):** [01-lazuar-feature-inventory.md](../01-lazuar-feature-inventory.md) later remaps `LP-065` to “Xero / QuickBooks”. Wave 4 Xero is `LP-121`. This file uses the Wave-1 tracker ID.

Related but **not** this ticket:

| ID | Why adjacent, not this work |
|----|-----------------------------|
| LP-047 | Honest vault / Billplz reminder-only (Wave 0, **done**). Flag + engine skip already exist. |
| LP-052 | Automatic renewal actually runs (Wave 0, **done**). Non-vaulted due rows mint a hosted checkout then `PAST_DUE`. |
| LP-053 | First-class “send link each cycle” product (Wave 1 sibling). Do not rebuild the billing-engine mint here. |
| LP-056 | Cancel at period end |
| LP-064 | CSV import of existing subscribers |
| LP-077 | Recovered-revenue metrics (Wave 0, **done**). Log Payment already `RecordRecovery`s on arrears + amount > 0. |
| LP-091–093 | Refund full/partial/UI. Offline cash cannot go through Stripe. Hide the button; do not invent an offline-refund ledger here. |
| LP-137 | M2M subscription admin API |
| LP-142 | `Idempotency-Key` on POST (use clerk `reference_number` only) |
| LP-151 | Receipt / portal-access email (Wave 0, **done**). First `SubscriptionActivated(IsFirstPayment)` already sends **Portal Access**. |
| LP-173 | Hub portal: update payment method |
| SL-083 / mark-paid | `MarkCheckoutAsPaidOffline` is a **sibling** offline path. Touch it only where it shares the ledger idempotency key. |
| SL-084 | Send-invoice / AR object. We do not have an Invoice aggregate. |
| SL-094 | Lifetime SKU. `COMPED` is one complimentary **period**, not forever. |

---

## 0. Verdict

The **buttons exist**. The **money loop is not closed**.

Ops can enroll a bank-transfer member and later click **Log Payment**. Both handlers flip `ACTIVE` and set `IsReminderOnly = true`. That is why the inventory says **SHIPPED** and the tracker is only **P**.

It is still **P**, not **Y**, because a merchant who actually collects offline (the Malaysian creator job: WhatsApp + Maybank receipt) hits all of these:

1. **Cycle 2+ never hits the Billing ledger or an Official Receipt.** `ManualSubscriberEnrolledIntegrationEventHandler` keys `LedgerEntries` on `ReferenceType=MANUAL_ENROLLMENT` + `ReferenceId=subscriptionId`. That pair is **unique**. Enroll books once; every later `RecordSubscriberPayment` publishes the same event with the same id and is silently dropped.
2. **Enroll writes no `CommerceTransactionLog`.** The member console “Payment Ledger” searches global transactions **by customer email**. A brand-new enroll shows “No payments logged” even when RM 150 just hit the ledger.
3. **`subscription.activated` fires on every paid Log Payment while ACTIVE.** Frozen catalog has no `subscription.renewed` / `subscription.updated`. Integrators (Aura) treat `activated` as “grant access for the first time.”
4. **SUSPENDED recovery uses `Resume`, which does not move `CurrentPeriodEnd`.** Ops “Period Ends” stays in the past.
5. **Ops “Period Ends” is the wrong column.** Create/record set `CurrentPeriodEnd = now` and `NextBillingDate = now+interval`. Webhooks already publish paid-through = `NextBillingDate`. The table shows `current_period_end` → membership looks expired today.
6. **Copy lies.** Modal: “Complimentary (Free Access)” and “will receive manual payment links upon renewal.” `COMPED` still sets a due date (billing engine will `PAST_DUE`). “Welcome Email” is **Portal Access** (LP-151), not a welcome template. “Copy Portal Link” is the **Stripe** Billing Portal — it fails for reminder-only rows.
7. **Create is unvalidated and untested.** No handler tests. Archived / `one_time` products enroll. Duplicate ACTIVE rows for the same email+product are allowed. Create endpoint has no `try/catch` (`Guid.Parse` → 500). Record-payment has three tests, all about dunning counters.

**LP-065 is: one ledger key, one enroll transaction log, honest events, honest dates/copy, and a test matrix on the two commands.** Do not build Chargebee invoices, CSV import, lifetime SKUs, or a new webhook type.

---

## 1. Product contract (what “done” means)

Sellable sentence after this ticket:

> A merchant can enroll a member who paid cash or bank transfer (or grant one complimentary period), see that payment on the member, get an Official Receipt when amount > 0, and each later offline payment extends access by one interval, books a **new** ledger row + receipt, and does **not** re-fire `subscription.activated`. The member is reminder-only. There is no card on file. Next cycle the existing billing engine mints a pay link and marks `PAST_DUE` unless ops logs another payment.

| Input | Result |
|-------|--------|
| Enroll recurring + `BANK_TRANSFER`/`CASH` + amount > 0 | CRM upsert; `Subscription` ACTIVE, `IsReminderOnly=true`; `CurrentPeriodEnd` + `NextBillingDate` as locked below; `CommerceTransactionLog` CONFIRMED; `ManualSubscriberEnrolled` with **per-payment** ledger key; Official Receipt if Billing/Resend live; `subscription.activated` + Portal Access **only if** `send_welcome_email` |
| Enroll `COMPED` (amount forced 0) | Same sub, **no** ledger, tx log amount 0 / `COMPED`; still one due date (not lifetime). Copy must say so |
| Enroll `one_time` or archived/`IsActive=false` product | **400**. One-time money uses checkout / mark-paid |
| Enroll when an ACTIVE sub already exists for that client + product | **400**. Do not mint a second row (CSV import is LP-064) |
| Log Payment on ACTIVE | Advance **from now** (or optional `next_billing_date`); tx log; new ledger/receipt if amount > 0; **no** `SubscriptionActivated` |
| Log Payment on `PAST_DUE` | `RecoverFromPayment`; clear dunning; `RecordRecovery` if amount > 0 and not COMPED (already W0-LP-077); `SubscriptionActivated(IsFirstPayment: false)` |
| Log Payment on `SUSPENDED` | Same recovery dates as PAST_DUE (`RecoverFromPayment`, not `Resume`); `SubscriptionResumed` |
| Log Payment `COMPED` | Amount 0; advance period; no ledger; no `RecordRecovery`; activated only if recovering arrears |
| Log Payment `PENDING` / `CANCELED` / `one_time` / amount < 0 | **400** (already, keep) |
| Same `reference_number` on the same subscription | Idempotent success (no second period, no second ledger) |
| No `reference_number` | New payment every click. Ops button already disables while in-flight |
| Reminder-only member, “Copy Portal Link” | Do **not** offer Stripe portal. Leave Hub magic-link to LP-151/LP-173 |

Industry cousins (do not copy extras): Chargebee **record a payment** / offline subscription; HitPay **mark as paid (cash)**; Stripe Invoice `collection_method=send_invoice` + paid-out-of-band; Billplz “create a bill, they pay later.” We already chose **no Invoice aggregate**. This ticket is ops cash/bank against a **Subscription**, not AR.

### 1.1 Clock lock (do not “fix” into anniversary billing)

UTC only. `Interval == "yr"` → `AddYears(1)`, else `AddMonths(1)` (same as every other Commerce writer).

| Writer | `CurrentPeriodEnd` | `NextBillingDate` (paid-through / next due) |
|--------|--------------------|-----------------------------------------------|
| Create, no overrides | `start` (`StartDate` ?? now) | `start + 1 interval` |
| Create, `next_billing_date` set | `start` | the override |
| Create `one_time` | refuse | refuse |
| Record-payment, no override | `now` | `now + 1 interval` |
| Record-payment, optional `next_billing_date` | `now` | the override |

**Arrears reset the clock to now.** A member who pays 20 days late gets a fresh month/year from the payment instant. That is offline collection, not “original anchor + N.” Do not change it.

Webhook `current_period_end` stays paid-through = `NextBillingDate` ([CommerceWebhookPayload.cs](../../../apps/lazuar-api/Modules/Commerce/Application/CommerceWebhookPayload.cs)). Ops UI must show **that** date as “Paid through / Next due.” Do not globally retune `Activate(now, next)` on the online checkout path.

`COMPED` is **one complimentary period**. Next due is still set. Billing engine will mint a link and `PAST_DUE` when it hits. A true lifetime SKU is SL-094 / later.

---

## 2. What exists (read, not redesigned)

### 2.1 Surface

| Layer | Path | Notes |
|-------|------|--------|
| TypeSpec | [packages/api-spec/modules/commerce/models/subscriber.tsp](../../../packages/api-spec/modules/commerce/models/subscriber.tsp) | `CreateManualSubscriberDto`, `RecordPaymentRequestDto` (`amount`, `payment_method`, `reference_number?`). Money is `float64`. |
| Admin routes | [packages/api-spec/modules/commerce/admin-routes.tsp](../../../packages/api-spec/modules/commerce/admin-routes.tsp) | `POST /subscribers`, `POST /subscribers/{id}/record-payment` → `StatusResponse` |
| Endpoint | [SubscriberEndpoints.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/SubscriberEndpoints.cs) | Uses generated DTOs (the old local `CreateManualSubscriberRequest` / `plan_id` drift is **gone**). Create **discards** the handler `Guid`. Create has **no** `InvalidOperationException` → 400. Record-payment does. `Guid.Parse(product_id)` is uncaught. |
| Create command | [CreateManualSubscriberCommand.cs](../../../apps/lazuar-api/Modules/Commerce/Contracts/Commands/CreateManualSubscriberCommand.cs) + [CreateManualSubscriberCommandHandler.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/CreateManualSubscriberCommandHandler.cs) | CRM resolve → product org check → `Activate(..., isReminderOnly: true)` → optional ledger event → optional `SubscriptionActivated(IsFirstPayment: true)` → `SaveChanges`. **No tx log. No amount/status/archive/`one_time`/duplicate checks.** `one_time` already leaves `NextBillingDate` null (W0-LP-052). |
| Record command | [RecordSubscriberPaymentCommand.cs](../../../apps/lazuar-api/Modules/Commerce/Contracts/Commands/RecordSubscriberPaymentCommand.cs) + [RecordSubscriberPaymentCommandHandler.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/RecordSubscriberPaymentCommandHandler.cs) | Rejects `PENDING`/`CANCELED`/`one_time`/negative. `COMPED` → amount 0. Recovery campaign captured **before** clear (W0-LP-077). ACTIVE → `Activate` + `ClearDunning`. PAST_DUE → `RecoverFromPayment`. SUSPENDED → `Resume` (**period end not moved**). Always writes tx log. Ledger event if amount > 0. Activated if `wasInArrears \|\| amount > 0` (except resume branch). |
| Event | [ManualSubscriberEnrolledIntegrationEvent.cs](../../../apps/lazuar-api/Modules/Commerce/Contracts/Events/ManualSubscriberEnrolledIntegrationEvent.cs) | Name is **overloaded**: enroll **and** every later offline payment **and** mark-paid. Internal Billing event, not the outbound catalog. **Keep the type.** |
| Ledger | [ManualSubscriberEnrolledIntegrationEventHandler.cs](../../../apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ManualSubscriberEnrolledIntegrationEventHandler.cs) | `referenceId = SubscriptionId.ToString()`. Unique index on `(ReferenceType, ReferenceId)` in [BillingDbContext.cs](../../../apps/lazuar-api/Modules/Billing/Infrastructure/BillingDbContext.cs). Receipt via `GenerateAndStoreDocumentCommand` + `CorrelationId = SubscriptionId` (W0-LP-151 CRM fallback works **for the first** payment). |
| Tx log | [CommerceTransactionLog.cs](../../../apps/lazuar-api/Modules/Commerce/Domain/Entities/CommerceTransactionLog.cs) | **No `SubscriptionId`.** Query: [CommerceQueryService.Transactions.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Transactions.cs) — email/name `ILIKE` only. |
| Ops | [CreateSubscriberModal.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/CreateSubscriberModal.tsx), [SubscribersPage.tsx](../../../apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx) | Typed `product_id` (frontend `plan_id` drift is **gone**). Reminder-only badge exists (W0-LP-047). Log Payment + Stripe portal + Refund on every CONFIRMED row. |
| Welcome | [PortalAccessEmailHandlers.cs](../../../apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/PortalAccessEmailHandlers.cs) | First-payment `SubscriptionActivated` only. Not a “Welcome” catalog template. |
| Outbound | [SubscriptionLifecycleIntegrationEventHandlers.cs](../../../apps/lazuar-api/Modules/Commerce/Application/EventHandlers/SubscriptionLifecycleIntegrationEventHandlers.cs) | `subscription.activated` / `resumed` / … Frozen: **no** `subscription.updated` / `renewed` ([11-subscriptions-lifecycle.md](../11-subscriptions-lifecycle.md)). |
| Bus | [Commerce DependencyInjection.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/DependencyInjection.cs) | `OutboxEventBus<CommerceDbContext>`. Publish-then-`SaveChanges` on the handler is the correct outbox pattern. |

### 2.2 Create handler (today)

```47:93:apps/lazuar-api/Modules/Commerce/Application/Commands/CreateManualSubscriberCommandHandler.cs
        DateTime currentPeriodEnd = request.StartDate ?? DateTime.UtcNow;
        DateTime? nextBillingDate = null;

        if (product.Interval != "one_time")
        {
            nextBillingDate = request.NextBillingDate ?? (product.Interval == "yr" ? currentPeriodEnd.AddYears(1) : currentPeriodEnd.AddMonths(1));
        }

        var subscription = new Subscription(/* ... */);
        subscription.Activate(currentPeriodEnd, nextBillingDate, isReminderOnly: true);
        // ...
        if (request.AmountPaid > 0 && request.PaymentMethod != "COMPED")
            await _eventBus.PublishAsync(new ManualSubscriberEnrolledIntegrationEvent(/* SubscriptionId */));

        if (request.SendWelcomeEmail)
            await _eventBus.PublishAsync(new SubscriptionActivatedIntegrationEvent(..., IsFirstPayment: true));

        await _repository.SaveChangesAsync(ct);
```

`PaymentMethod != "COMPED"` is **case-sensitive**. Record-payment uppercases first. Create `"comped"` would still ledger.

`ResolveClientProfile` matches on org+email and **does not** overwrite an existing name ([ResolveClientProfileCommandHandler.cs](../../../apps/lazuar-api/Modules/CRM/Infrastructure/ResolveClientProfileCommandHandler.cs)). Same email twice → same `ClientProfileId`, two subscriptions.

Product list used by the modal is **all** products, including archived and `one_time` ([CommerceQueryService.Products.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Products.cs)).

### 2.3 Record-payment handler (today)

```74:153:apps/lazuar-api/Modules/Commerce/Application/Commands/RecordSubscriberPaymentCommandHandler.cs
        var periodEnd = DateTime.UtcNow;
        var nextBilling = product.Interval == "yr" ? DateTime.UtcNow.AddYears(1) : DateTime.UtcNow.AddMonths(1);

        if (wasSuspended) subscription.Resume(nextBilling);
        else if (subscription.Status == "PAST_DUE") subscription.RecoverFromPayment(periodEnd, nextBilling);
        else { subscription.Activate(periodEnd, nextBilling, subscription.IsReminderOnly); subscription.ClearDunning(); }

        // RecordRecovery if arrears && amount > 0 && !COMPED   (W0-LP-077 — keep)

        var externalRef = string.IsNullOrWhiteSpace(request.ReferenceNumber)
            ? $"MANUAL-{subscription.Id:N}"[..32]
            : request.ReferenceNumber.Trim();
        _repository.AddTransactionLog(/* recordedByName: method, externalReference: externalRef */);

        if (amount > 0 && method != "COMPED")
            await _eventBus.PublishAsync(new ManualSubscriberEnrolledIntegrationEvent(..., request.ReferenceNumber));

        if (wasSuspended) await Publish(SubscriptionResumed);
        else if (wasInArrears || amount > 0) await Publish(SubscriptionActivated(IsFirstPayment: false));
```

Default `MANUAL-{subId}` is **the same string every cycle**. Fine as a display fallback; **illegal** as a ledger unique key (and it is not even used as the ledger key today — the Guid is).

`Resume` ([Subscription.cs](../../../apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs) 114–122) sets `NextBillingDate` only. `RecoverFromPayment` sets both dates and clears the renewal checkout URL. Record-payment must use **RecoverFromPayment** for SUSPENDED as well.

### 2.4 Ledger uniqueness (the completeness bug)

```24:30:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ManualSubscriberEnrolledIntegrationEventHandler.cs
        var referenceType = LedgerReferenceTypes.ManualEnrollment;
        var referenceId = @event.SubscriptionId.ToString();

        if (await _repository.HasEntryBeenProcessedAsync(referenceType, referenceId))
            return;
```

`billing.LedgerEntries` has `HasIndex(ReferenceType, ReferenceId).IsUnique()`. First enroll (or first mark-paid on that sub id) wins. Every later offline payment on that row is a no-op: **no cash, no receipt, no document email.** Commerce still writes a tx log and still advances the clock — ops and Billing **diverge**.

W0-LP-151 fixed **customer email** on the first receipt (`GetCustomerForDocumentAsync` falls back to the subscription’s CRM profile via `CorrelationId`). It did **not** fix the unique key.

Mark-paid ([MarkCheckoutAsPaidOfflineCommandHandler.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs)) publishes the same event with `SubscriptionId = entitlementId`. After an offline checkout creates the sub, **Log Payment on that member is also dropped in Billing.** Same fix.

### 2.5 Ops UI honesty

[SubscribersPage.tsx](../../../apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx):

- Table + side panel “Period Ends” = `current_period_end` (create/record = **now**). `next_billing_date` is on the DTO and unused in the panel.
- Payment Ledger: `GET /admin/commerce/transactions?search={customer_email}`. Mixes every product and every member who shares an email. Misses enroll (no log).
- Refund is shown for every `CONFIRMED` amount > 0. [RecordRefundCommandHandler.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/RecordRefundCommandHandler.cs) always emits `GatewayRefundRequested` (default gateway **STRIPE**) using `ExternalReference` as the Stripe id. `BANK_TRANSFER` / `CASH` / `MANUAL-{sub}` will fail. Hide Refund when `recorded_by_name` is not a gateway. Do not implement offline refund here (LP-091).
- “Copy Portal Link” → `POST /subscribers/portal-link` is documented as **Stripe Customer Portal**. Reminder-only members have no Stripe customer. Hide or disable when `is_reminder_only`.
- Log Payment is disabled only for `CANCELED`. List query already excludes `PENDING`. Prefill amount with `product_price`. No date override. No “this grants one period from today” copy.
- Optimistic `status: "ACTIVE"` after Log Payment does not refresh `next_billing_date`.

[CreateSubscriberModal.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/CreateSubscriberModal.tsx):

- Honest reminder-only amber box (keep). The “manual payment links upon renewal” half is **true after W0-LP-052** (engine mints checkout). Keep that sentence.
- “Complimentary (Free Access)” implies forever. Change to one period.
- “Send automated Welcome Email & Access Links” → Portal Access only. Rename.
- Amount not prefilled from the selected product. Archived / `one_time` still listed.

### 2.6 Tests today

**Zero** tests mention `CreateManualSubscriber`.

Record-payment only in [CommerceProductCompletenessTests.cs](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs):

| Test | Asserts |
|------|---------|
| `RecordSubscriberPayment_FromPastDue_RecoversAndLogsManualTx` | ACTIVE, campaign cleared, `RecoveredRevenue`, tx `BANK_TRANSFER`, both events |
| `RecordSubscriberPayment_FromPastDue_Comped_DoesNotRecordRecovery` | no `RecordRecovery` |
| `RecordSubscriberPayment_FromActive_DoesNotRecordRecovery` | ACTIVE renewal does not bump campaign |

No test for: ledger event `SubscriptionId` vs payment id, SUSPENDED period end, no `SubscriptionActivated` on ACTIVE paid renew, `one_time`/`PENDING`/`CANCELED` reject, default external ref, enroll path at all.

Billing [ManualSubscriberEnrolledHandlerTests.cs](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/ManualSubscriberEnrolledHandlerTests.cs) only asserts SaveChanges-before-document order. Does **not** assert a second event on the same subscription is processed.

### 2.7 Already done on adjacent tickets (do not redo)

| Ticket | What LP-065 can assume |
|--------|------------------------|
| W0-LP-047 | `IsReminderOnly`, ops badge, no AUTO_CHARGE on reminder-only |
| W0-LP-052 | Create `one_time` does not write `NextBillingDate`; billing mints renewal checkout then `PAST_DUE` |
| W0-LP-077 | Log Payment `RecordRecovery` when arrears and amount > 0 |
| W0-LP-151 | First-activate Portal Access; receipt CRM fallback via `CorrelationId` |

Stale gap docs (do not “fix” them as if current): [docs/001-gaps/19-frontend-backend-integration.md](../../../docs/001-gaps/19-frontend-backend-integration.md) still says record-payment is missing and the modal sends `plan_id`. [docs/001-gaps/07-commerce-module.md](../../../docs/001-gaps/07-commerce-module.md) still says mark-paid creates no Sub/tx log. Code has moved.

---

## 3. Gaps in scope for LP-065

| # | Gap | Why it blocks “honest and complete” |
|---|-----|-------------------------------------|
| G1 | Ledger `ReferenceId = subscriptionId` | Cycle 2+ cash never books; unique index would throw if the early-return were removed |
| G2 | Create writes no `CommerceTransactionLog` | Member “Payment Ledger” is empty; refund/search have nothing to attach |
| G3 | Tx log has no `SubscriptionId`; list is email search | Wrong rows / missing rows |
| G4 | `SubscriptionActivated` on ACTIVE + amount > 0 | Re-grants / re-webhooks every offline renewal |
| G5 | SUSPENDED → `Resume` | `CurrentPeriodEnd` stale |
| G6 | Ops date column + COMPED/welcome/Stripe-portal/Refund copy | Merchant cannot trust the screen |
| G7 | Create: no `one_time`/archived/duplicate/amount/method/case validation; 500 on bad Guid | Footguns; untested |
| G8 | Record-payment: no date override, no clerk-ref idempotency, default ref reused | Double-click with a bank slip id extends twice; cannot honor an agreed anniversary |
| G9 | No Create tests; Record tests ignore money/events | Regressions will ship |

Out of scope (do not pick up while “in the file”):

- New outbound event type (`subscription.renewed`).
- Renaming `ManualSubscriberEnrolledIntegrationEvent`.
- Invoice / net-terms / AR (SL-084).
- Lifetime / `NextBillingDate = null` for COMPED (SL-094).
- CSV import (LP-064) or unique DB index on `(org, client, product)` for historical dupes — handler reject of **ACTIVE** dupes is enough.
- Changing online checkout `Activate(now, next)` globally.
- Hub portal / Stripe portal replacement (LP-173).
- Offline refund booking (LP-091).
- `float64` → decimal money on all Commerce DTOs (program-wide).
- Billing-engine pay-link mint (LP-053 / already W0-LP-052).

---

## 4. Recommended semantics (lock this, then code)

### 4.1 One payment = one ledger key

Keep `ManualSubscriberEnrolledIntegrationEvent`. Change what Billing treats as `ReferenceId`:

1. Commerce creates the `CommerceTransactionLog` **first** (it has a Guid).
2. Event still carries `SubscriptionId` (the sub).
3. Billing `ReferenceId` = **transaction log id** (string). `CorrelationId` stays `SubscriptionId` so W0-LP-151 CRM fallback still works.
4. Pass the log id on the event. Smallest honest change: add `string LedgerReferenceId` (or `Guid TransactionLogId`) to the record. Mark-paid fills it the same way.

Do **not** use raw clerk `reference_number` as `ReferenceId` (cross-tenant collision on the global unique index). Do **not** use `event.Id` only — a Commerce retry that mints a new event id would double-book after a successful ledger write.

`HasEntryBeenProcessed(MANUAL_ENROLLMENT, txLog.Id)` then means “this payment,” not “this member ever.”

### 4.2 Events

| Situation | Outbound | Portal Access | Ledger event |
|-----------|----------|---------------|--------------|
| Create + `send_welcome_email` | `subscription.activated` `is_first_payment=true` | yes (existing handler) | if amount > 0 |
| Create, checkbox off | **none** | no | if amount > 0 |
| Record ACTIVE | **none** | no | if amount > 0 |
| Record PAST_DUE | `subscription.activated` `is_first_payment=false` | no | if amount > 0 |
| Record SUSPENDED | `subscription.resumed` | no | if amount > 0 |
| COMPED | same as status row; no ledger | only create+welcome | no |

Do not add `subscription.renewed`.

### 4.3 Idempotency

If `reference_number` is non-empty after trim:

- Look up existing CONFIRMED log for this **organization + subscription + exact ExternalReference**.
- If found: return success, do **not** advance dates, do **not** publish.
- Else write the log with that ExternalReference.

If omitted: `ExternalReference = txLog.Id` (unique every time). Drop the `MANUAL-{subId}` 32-char reuse.

Ops should send the bank slip / DuitNow reference when the clerk has one.

### 4.4 Validation (both commands)

Allow-list `payment_method` after trim + upper: `BANK_TRANSFER` | `CASH` | `COMPED`. Unknown → 400.

Create:

- Product must exist, same org, `IsActive`, `Interval` is `mo` or `yr`.
- Email required (non-empty, has `@`). Phone already required by DTO/UI.
- Amount > 0 unless COMPED; COMPED forces 0; amount never < 0.
- Reject if an **ACTIVE** subscription already exists for `(OrganizationId, ClientProfileId, ProductId)`.
- `next_billing_date` if set must be ≥ `start`.
- Map `InvalidOperationException` (and `FormatException` on product id) to 400 like record-payment.

Record:

- Existing rejects stay.
- Optional `next_billing_date` on the DTO (TypeSpec + regen). If set, use it as `NextBillingDate`; `CurrentPeriodEnd` still `now`.
- SUSPENDED uses `RecoverFromPayment(periodEnd, nextBilling)`.

### 4.5 Transaction log column

Add nullable `SubscriptionId` on `commerce.TransactionLogs` + index `(OrganizationId, SubscriptionId, CreatedAt)`.

- Create + Record write it.
- Mark-paid product path writes it when a sub is created (same migration; one extra assignment). Custom mark-paid stays null.
- Member console loads `GET /transactions?subscription_id={id}` (add optional query; ignore email when set).
- Global Transactions page unchanged.

Constructor change on `CommerceTransactionLog` is required. Call sites: Create (new), Record, Mark-paid, gateway `LogTransactionAsync` (pass `subscriptionId` when known, else null).

### 4.6 Ops copy / chrome

- Table + panel: show `next_billing_date` as **Paid through / Next due**. Keep `current_period_end` off the primary label (or subtitle “period started”).
- Prefill Log Payment + enroll amount from `product_price`.
- COMPED: “Complimentary — grants one period, then they come due.”
- Welcome checkbox: “Email a portal access link.”
- Hide Stripe “Copy Portal Link” when `is_reminder_only`.
- Hide Refund when `recorded_by_name` is `BANK_TRANSFER` / `CASH` / `COMPED` / `MANUAL` / `MANUAL_OFFLINE` (case-insensitive).
- Product select: `is_active && (interval === "mo" \|\| interval === "yr")`.
- After Log Payment, refresh the selected row from the list query (do not only patch `status`).

Do not change TypeSpec `StatusResponse`. List invalidate is enough; do not add a create-response id unless a later ticket needs it.

---

## 5. Minimal code changes

### 5.1 Billing (G1)

[ManualSubscriberEnrolledIntegrationEventHandler.cs](../../../apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ManualSubscriberEnrolledIntegrationEventHandler.cs)

- `referenceId` = new per-payment field (tx log id). Fallback: if old events in the outbox have it empty, keep `SubscriptionId` so in-flight first enrolls do not double-post.
- Keep `CorrelationId = SubscriptionId`.
- Description can mention payment method; unused for uniqueness.

### 5.2 Event contract

[ManualSubscriberEnrolledIntegrationEvent.cs](../../../apps/lazuar-api/Modules/Commerce/Contracts/Events/ManualSubscriberEnrolledIntegrationEvent.cs) — add `Guid TransactionLogId` (or `string LedgerReferenceId`). All three publishers: Create, Record, Mark-paid.

### 5.3 Create handler

[CreateManualSubscriberCommandHandler.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/CreateManualSubscriberCommandHandler.cs)

- Validations in §4.4.
- Load CRM profile for name/email **after** resolve (or reuse resolve + a query) so the tx log is not empty.
- Always write a `CommerceTransactionLog` (amount 0 for COMPED).
- Ledger event only when amount > 0, keyed by log id.
- `PaymentMethod` normalized to upper like Record.

Need `ICrmQueryService` on this handler (Record already has it).

Duplicate check: `ICommerceRepository` needs `HasActiveSubscriptionAsync(org, client, product)` (or query existing list). Do not add a unique DB constraint this ticket.

### 5.4 Record handler

[RecordSubscriberPaymentCommandHandler.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/RecordSubscriberPaymentCommandHandler.cs)

- Clerk-ref idempotency.
- `RecoverFromPayment` for SUSPENDED.
- Optional next-date override.
- `SubscriptionActivated` **only** when `wasInArrears && !wasSuspended`.
- Ledger event uses tx log id; `ExternalReference` as §4.3.
- Keep W0-LP-077 `RecordRecovery` exactly.

### 5.5 Tx log + query

- Entity + EF config + migration `AddTransactionLogSubscriptionId`.
- [CommerceQueryService.Transactions.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Transactions.cs) — optional `subscriptionId` filter.
- TypeSpec `getTransactions` query param.
- Regen `api-types-ts` / `api-types-dotnet`.
- Record-payment DTO: optional `next_billing_date`.
- Gateway log writer: pass null or the real sub id when the open-checkout path has one (do not block if the helper has no sub yet).

### 5.6 Endpoint

[SubscriberEndpoints.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/SubscriberEndpoints.cs)

- Create: `try/catch` `InvalidOperationException` + bad `product_id` → `BadRequest(StatusResponse)`.
- Record: bind optional `next_billing_date`.

### 5.7 Ops

`CreateSubscriberModal.tsx`, `SubscribersPage.tsx` only. No portal/docs app.

---

## 6. Tests to add

New fixture `CreateManualSubscriberCommandHandlerTests` + extend `CommerceProductCompletenessTests` / `ManualSubscriberEnrolledHandlerTests`. No HTTP host required (same style as existing handler tests).

### 6.1 Create

| # | Case | Assert |
|---|------|--------|
| C1 | Recurring + BANK_TRANSFER + amount 150 | ACTIVE, `IsReminderOnly`, `NextBillingDate ≈ start+1mo`, tx log 150 / `BANK_TRANSFER` / `SubscriptionId`, ledger event `TransactionLogId == log.Id`, activated **only** if welcome true |
| C2 | Welcome false | no `SubscriptionActivated` |
| C3 | COMPED + amount 99 in command | amount stored 0; **no** ledger event; tx log 0 / `COMPED`; next due still set |
| C4 | `one_time` | throw; no sub |
| C5 | Archived product | throw |
| C6 | Wrong org product | throw (existing message ok) |
| C7 | Second ACTIVE same client+product | throw |
| C8 | `next_billing_date` override | that date; `CurrentPeriodEnd` = start |
| C9 | `yr` product, no override | `AddYears(1)` |
| C10 | `payment_method: "comped"` | treated as COMPED (upper) |
| C11 | amount 0 + BANK_TRANSFER | throw |
| C12 | amount < 0 | throw |

### 6.2 Record

| # | Case | Assert |
|---|------|--------|
| R1 | ACTIVE + 100 | dates from now; tx log; ledger event with **new** log id; **no** `SubscriptionActivated`; no `RecordRecovery` |
| R2 | Two payments on same sub (no clerk ref) | two logs; two ledger events; **two different** `TransactionLogId`s |
| R3 | Same `reference_number` twice | one log; dates unchanged on second call; one ledger event |
| R4 | PAST_DUE | `RecoverFromPayment`; recovery metrics; `SubscriptionActivated(false)` |
| R5 | SUSPENDED | `CurrentPeriodEnd` moved (not only `NextBillingDate`); `SubscriptionResumed`; no `SubscriptionActivated` |
| R6 | COMPED from PAST_DUE | amount 0; no ledger; no recovery $; still ACTIVE + activated? **yes** (arrears, first-payment false) |
| R7 | COMPED from ACTIVE | no activated; no ledger |
| R8 | `PENDING` / `CANCELED` / `one_time` / negative | throw |
| R9 | Optional `next_billing_date` | that date |
| R10 | Reminder-only preserved | `IsReminderOnly` still true |

### 6.3 Billing handler

| # | Case | Assert |
|---|------|--------|
| B1 | Two events, same `SubscriptionId`, different `TransactionLogId` | `HasEntryBeenProcessed` false then true **per id**; `Add` twice |
| B2 | Replay same `TransactionLogId` | second `Add` not called |
| B3 | Existing order: SaveChanges before `GenerateAndStore`; `CorrelationId` = sub id | keep |

### 6.4 Query / UI contract (lightweight)

- `GetTransactionsAsync` with `subscription_id` returns only that sub’s logs (integration test next to existing `CommerceQueryServiceTests` if the test host is cheap; else a SQL-shaped unit is enough).
- Do **not** require a Playwright pass. Ops copy is reviewable in the PR.

---

## 7. Files to touch (when implementing)

### Must

| File | Change |
|------|--------|
| `apps/lazuar-api/Modules/Commerce/Contracts/Events/ManualSubscriberEnrolledIntegrationEvent.cs` | per-payment id |
| `apps/lazuar-api/Modules/Commerce/Application/Commands/CreateManualSubscriberCommandHandler.cs` | validate, tx log, events |
| `apps/lazuar-api/Modules/Commerce/Application/Commands/RecordSubscriberPaymentCommandHandler.cs` | recover, events, idempotency, date override |
| `apps/lazuar-api/Modules/Commerce/Contracts/Commands/RecordSubscriberPaymentCommand.cs` | optional `NextBillingDate` |
| `apps/lazuar-api/Modules/Commerce/Domain/Entities/CommerceTransactionLog.cs` | `SubscriptionId?` |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/CommerceDbContext.cs` + new migration | column + index |
| `apps/lazuar-api/Modules/Commerce/Application/ICommerceRepository.cs` + `CommerceRepository.cs` | active-sub exists; tx log by org+sub+ref |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/SubscriberEndpoints.cs` | 400 on create; bind override |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Transactions.cs` | filter by sub |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ManualSubscriberEnrolledIntegrationEventHandler.cs` | new reference id |
| `apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs` | set `TransactionLogId` + `SubscriptionId` on the product-path log |
| `packages/api-spec/modules/commerce/models/subscriber.tsp` + transactions list query | `next_billing_date?`; `subscription_id?` |
| regen `api-types-ts` / `api-types-dotnet` | |
| `apps/lazuar-ops/src/modules/commerce/components/CreateSubscriberModal.tsx` | copy, filter, prefill |
| `apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx` | dates, ledger query, hide Stripe/refund |
| tests listed in §6 | |

### Likely (call sites of `new CommerceTransactionLog(`)

Gateway completion log helper, zero-amount, any other writer — pass `subscriptionId: null` unless already in hand.

### Do not touch

Billing engine, dunning engine, portal update-payment, webhook catalog, Stripe/Billplz adapters, `Resume()` domain meaning (callers change; method stays for any other user).

---

## 8. Acceptance (flip LP-065 to **Y** when)

1. Enroll cash/bank on a monthly product: member is ACTIVE reminder-only; Payment Ledger shows the row; Billing has a `MANUAL_ENROLLMENT` entry whose `ReferenceId` is the **log id**; Official Receipt can resolve CRM email via `CorrelationId`.
2. Log Payment on that same member (new clerk ref or no ref): clock moves one interval from now; **second** ledger row + second receipt path; Commerce has two logs; **no** new `subscription.activated`.
3. Same clerk `reference_number` twice: one period, one ledger row.
4. PAST_DUE Log Payment still recovers, clears dunning, `RecordRecovery` when amount > 0 (W0-LP-077 still green).
5. SUSPENDED Log Payment sets **both** dates; outbound is `subscription.resumed`.
6. COMPED enroll/record never ledgers; copy does not say “free forever”; next due exists.
7. `one_time` / archived / duplicate ACTIVE enroll → 400. Create bad `product_id` → 400, not 500.
8. Ops shows **Next due / paid through** = `next_billing_date`. Stripe portal hidden on reminder-only. Refund hidden on cash/bank/COMPED.
9. Tests in §6 pass. Existing W0-LP-077 record-payment tests still pass.
10. Tracker cell LP-065 Lazuar **P → Y**. Do not flip LP-053.

Not required for **Y**: M2M enroll (LP-137), CSV import, lifetime, invoices, Hub portal button, decimal money DTO sweep, `subscription.renewed`.

---

## 9. Suggested implement order

1. Event field + Billing handler + B1/B2 tests (prove cycle 2 books).
2. Tx log `SubscriptionId` migration + Create writes a log + C1/C3.
3. Record handler: RecoverFromPayment, stop ACTIVE activated, per-payment ledger id, clerk-ref idempotency, R1–R7.
4. Create validations C4–C12 + endpoint 400.
5. Transactions `subscription_id` query + ops ledger/date/copy/hide.
6. Mark-paid publishers pass the new event field (no behavior change beyond G1).
7. Run the filters in §6 plus `CommerceProductCompletenessTests` + `ManualSubscriberEnrolledHandlerTests` + `SubscriptionRecoveryTests`.

---

## 10. Risks

| Risk | Mitigation |
|------|------------|
| In-flight outbox events lack `TransactionLogId` | Billing fallback to `SubscriptionId` only when the new field is empty/`Guid.Empty` |
| Existing first-enroll ledger rows stay keyed by sub id | Leave them. New payments use log ids. Do not migrate old keys |
| Adding a ctor arg to `CommerceTransactionLog` misses a call site | search `new CommerceTransactionLog(`; compile will fail |
| Duplicate ACTIVE reject breaks a tenant who already double-enrolled | reject only **new** creates; do not backfill-merge |
| Merchants wanted anniversary (period end + 1), not clock reset | optional `next_billing_date` on record-payment; default stays **now + interval** |
| Hiding Stripe portal looks like a regression | copy already says reminder-only; Stripe portal cannot work without a customer |

---

## 11. File map (absolute)

| Concern | Path |
|---------|------|
| Create handler | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Commands/CreateManualSubscriberCommandHandler.cs` |
| Record handler | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Commands/RecordSubscriberPaymentCommandHandler.cs` |
| Mark-paid (shared event) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs` |
| Endpoints | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/SubscriberEndpoints.cs` |
| Subscription aggregate | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs` |
| Tx log | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Entities/CommerceTransactionLog.cs` |
| Ledger consumer | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ManualSubscriberEnrolledIntegrationEventHandler.cs` |
| Ops enroll | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/components/CreateSubscriberModal.tsx` |
| Ops member console | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx` |
