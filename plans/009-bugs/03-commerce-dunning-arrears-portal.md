# 03 — Commerce: dunning jobs, PAST_DUE, arrears, update-payment, magic-link tokens

**Date:** 17 August 2026  
**Branch:** `feat/007-waves-1-4-implement` (`297ba98`)  
**Product slice:** Lazuar Pay Commerce — DunningEngineJob (Claim / PreDunning / PastDue / Dispatch), PastDueDunningProcessor, DunningStepDispatcher, PublicArrearsEndpoints, ArrearsAccess, MagicLinkTokenService, portal cancel / keep / plan-change / update-payment, reminder logs, charge attempt limits, decline classifier, dunning campaign snapshot.  
**Code read:** the trees below as they sit on `297ba98`. Recently claimed fixes `9b531d2` (HMAC on arrears / update-payment; siblings same `ClientProfileId`) and `eba0741` (SST Gross on dunning AUTO_CHARGE and arrears; ACTIVE update-payment stays RM 1) are re-read, not trusted from commit messages.

This is the uncondensed 009 bug audit for slice 03. It is not a rewrite of `plans/007-feats` or `plans/008-evals`. 008 named P0/P1s that this branch then patched. A 008 bug is closed only if this tree no longer contains it. A bug 008 missed is still written up.

Skepticism rule: a Wave `*-done.md`, a tracker cell, or a commit subject is not evidence. The claim SQL, the HMAC compare, the reminder unique index, the portal `href`, and the test that would go red if the failure mode returned — those are evidence.

Refuse-list gaps (no WhatsApp product, no e-mandate, no unused-time proration, no immediate plan change) are **not** bugs.

Out of scope (other 009 reports): BillingEngineJob claim SQL (02), adapter HTTP / EventId (04), ledger / refunds / disputes as Billing-owned money (05), One auth except token crypto (07). Communications hydrate is cited only where it consumes a dunning payload or mints the same HMAC.

---

## Scope lock

This report covers **only**:

- Hourly `DunningEngineJob` claim (pre-dunning vs PAST_DUE), catch-up, WhatsApp demote, AUTO_CHARGE, terminal CANCEL/SUSPEND.
- `PastDueDunningProcessor` snapshot assign / lazy backfill, attempt cap, hard-decline skip, in-flight PENDING defer.
- `DunningStepDispatcher` amount / checkout_url / action_type.
- `GatewayPaymentFailedIntegrationEventHandler` (Commerce): fail attempt → PAST_DUE → run processor.
- `GatewayPaymentCompletedIntegrationEventHandler.Subscription`: `update_payment=1` vs recover / activate.
- Public arrears GET/POST, `ArrearsAccess`, `MagicLinkTokenService`.
- Buyer portal: magic-link request, list, cancel, keep, plan change, update-payment page.
- ReminderDispatchLog unique key, ChargeAttemptLog unique key, `DeclineClassifier`, `ChargeAttemptLimits`.
- Campaign snapshot JSON freeze vs live edit.
- Tests that claim to pin the above.

Not covered: hop-1 checkout, BillingEngineJob due-claim starvation, Payments adapter signatures, ledger journals, One login cookies except as they confuse the portal page.

---

## Current files table

Paths are under `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/` unless noted.

### Domain (the truth the job is allowed to write)

| File | What it owns |
|------|----------------|
| `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs` | Status, vault, `IsReminderOnly`, dunning pin, reminder collection, `RecoverFromPayment` / `MarkAsPastDue` / `StoreVaultedToken` |
| `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/DunningCampaign.cs` | Live campaign + steps + `Matches` + `RecordChurn` / `RecordRecovery` |
| `apps/lazuar-api/Modules/Commerce/Domain/Entities/DunningStep.cs` | DayOffset + action + copy |
| `apps/lazuar-api/Modules/Commerce/Domain/Entities/ChargeAttemptLog.cs` | PENDING/FAILED/SUCCEEDED/SKIPPED; billing attempt 1 vs dunning 2–4 |
| `apps/lazuar-api/Modules/Commerce/Domain/Entities/ReminderDispatchLog.cs` | One row per `(SubscriptionId, TargetBillingDate, DayOffset)` |
| `apps/lazuar-api/Modules/Commerce/Domain/ValueObjects/DunningCampaignSnapshot.cs` | Frozen v1 JSON; unknown `v` is corrupt |
| `apps/lazuar-api/Modules/Commerce/Domain/DunningCampaignMatcher.cs` | `MANUAL` vs `ONLINE_GATEWAY` from vault presence |
| `apps/lazuar-api/Modules/Commerce/Domain/DeclineClassifier.cs` | Static Stripe hard-code table |
| `apps/lazuar-api/Modules/Commerce/Domain/ChargeAttemptLimits.cs` | `MaxAttemptsPerBillingCycle = 4` |

### Application (policies HTTP and workers are supposed to obey)

| File | What it owns |
|------|----------------|
| `.../Application/ArrearsAccess.cs` | HMAC gate: same sub, or same org + `ClientProfileId` |
| `.../Application/PortalSubscriptionAccess.cs` | Same sibling rule + tenant slug must own the token sub |
| `.../Application/SubscriptionBillingAmount.cs` | Unit × seats + SST Gross; `UnitAmount > 0` else catalog `Price` |
| `.../Application/SstTaxMath.cs` | Exclusive SST if merchant has SST ID and type `02` |
| `.../Application/RenewalCheckoutIssuer.cs` | Hosted bill; cancel URL `...?token=` |
| `.../Application/SubscriptionCancelDecision.cs` | ACTIVE / PAST_DUE / SUSPENDED / **TRIALING** |
| `.../Application/SubscriptionCancelApplier.cs` | Persist + `SubscriptionCanceledIntegrationEvent` |
| `.../Application/PlanChangePolicy.cs` | Next-renewal-only; live status ACTIVE/TRIALING |
| `.../Application/Commands/RequestPortalMagicLinkCommandHandler.cs` | Always no-op on miss; newest sub is token subject |
| `.../Application/Commands/CancelPortalSubscriptionCommandHandler.cs` | Token + sibling + cancel table |
| `.../Application/Commands/KeepPortalSubscriptionCommandHandler.cs` | Clears `CancelAtPeriodEnd` |
| `.../Application/Commands/ChangePortalPlanCommandHandler.cs` | PAST_DUE / flagged guards |
| `.../Application/Commands/DunningCampaignAutoChargeGuard.cs` | Blocks AUTO_CHARGE on all-Billplz / all-MANUAL targets |
| `.../Application/Commands/DunningCampaignCommandHandlers.cs` | CRUD + default seed (−3/0/3 EMAIL, 1/5 AUTO_CHARGE, grace 7, CANCEL) |
| `.../Application/Commands/ManageSubscriberDunningCommandHandlers.cs` | Per-sub `DunningPausedUntil` |
| `.../Application/DunningRecoveryAttribution.cs` | Campaign id from metadata or live pin |
| `.../Application/CommerceWebhookPayload.cs` | `subscription.past_due` amount now Gross |

### Infrastructure (HTTP + workers + SQL)

| File | What it owns |
|------|----------------|
| `.../Infrastructure/Workers/DunningEngineJob.cs` | Hourly loop; load active campaigns AsNoTracking; pre then past-due |
| `.../Infrastructure/Workers/DunningEngineJob.Claim.cs` | `FOR UPDATE SKIP LOCKED`; batch 50; exclude failed/processed ids |
| `.../Infrastructure/Workers/DunningEngineJob.PreDunning.cs` | Live campaign; DayOffset < 0; consume even on WhatsApp skip |
| `.../Infrastructure/Workers/DunningEngineJob.PastDue.cs` | Thin wrap → processor |
| `.../Infrastructure/Workers/DunningEngineJob.Dispatch.cs` | Thin wrap → dispatcher |
| `.../Infrastructure/Dunning/PastDueDunningProcessor.cs` | Assign + snapshot + AUTO_CHARGE + terminal |
| `.../Infrastructure/Dunning/DunningStepDispatcher.cs` | Demote WhatsApp; Gross amount; live renewal URL only |
| `.../Infrastructure/Endpoints/PublicArrearsEndpoints.cs` | GET arrears + POST update-payment; query `token` required in handler |
| `.../Infrastructure/Endpoints/PublicPortalEndpoints.cs` | Portal GET/docs/plans/cancel/keep/change-plan + always-200 magic-link |
| `.../Infrastructure/Endpoints/PublicEndpoints.cs` | Composer |
| `.../Infrastructure/Security/MagicLinkTokenService.cs` | HMAC-SHA256 hex, standard Base64, 24h, `Jwt:Secret` or fallback |
| `.../Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` | Any failed PI with `subscription_id` → PAST_DUE |
| `.../Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs` | `update_payment=1` only if still ACTIVE |
| `.../Infrastructure/Services/CommerceQueryService.Portal.cs` | List every non-PENDING sub for the token’s client |
| `.../Infrastructure/Services/PortalDocumentQueryService.cs` | Documents for client; **also merges same-email profiles** |
| `.../Infrastructure/Repositories/CommerceRepository.cs` | `GetSubscriptionByIdAsync` IgnoreQueryFilters; newest-by-CreatedAt |
| `.../Infrastructure/CommerceDbContext.cs` | Unique `(Sub, TargetDate, DayOffset)` and `(Sub, TargetDate, AttemptNumber)` |
| `.../Infrastructure/Workers/BillingEngineJob.cs` | Cited only for mint + `StartPastDueDunningRunAsync` + attempt 1 |

### Portal (what a buyer actually clicks)

| File | What it owns |
|------|----------------|
| `apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx` | Magic-link form if no token; cancel/keep/plan/update-payment |
| `apps/lazuar-portal/src/app/[tenantSlug]/portal/layout.tsx` | Header “Buyer Dashboard” **drops token** |
| `apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx` | Requires token; GET arrears; POST update-payment |
| `apps/lazuar-portal/src/modules/portal/components/PortalPlanChange.tsx` | Encodes token on plans/change-plan |
| `apps/lazuar-portal/src/modules/portal/components/RequestMagicLinkForm.tsx` | Always treats response as success |

### Communications (only the money-link mint)

| File | What it owns |
|------|----------------|
| `.../Communications/Application/MessageTemplateHydrator.cs` | `?token=` appended, **not** URL-encoded |
| `.../Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs` | Hydrate dunning; throw on missing email/body |
| `.../Communications/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` | Immediate “Payment Failed” mail with same HMAC |

### Tests that pin this slice (not a compliment — a map)

Under `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/`:

`ArrearsAccessTests`, `MagicLinkTokenServiceTests`, `PublicArrearsEndpointsBoundaryTests`, `RequestPortalMagicLinkCommandHandlerTests`, `DunningEngineJobTests`, `DunningCampaignSnapshotTests`, `DunningCampaignCommandHandlerTests`, `DunningCampaignDomainTests`, `GatewayPaymentFailedIntegrationEventHandlerTests`, `GatewayPaymentCompletedRecoveryMetricsTests`, `DeclineClassifierTests`, `ChargeAttemptLogTests`, `SubscriptionBillingAmountTests`, `SubscriptionRecoveryTests`, `ChangePortalPlanCommandHandlerTests`, `SubscriptionCancelAtPeriodEndTests`.

Communications (token in body only): `DunningTemplateVariableSubstitutionTests`, `MessageTemplateHydratorTests`, `GatewayPaymentFailedEmailHandlerTests`.

There is **no** WebApplicationFactory / HTTP test that GET/POST arrears without `token` returns 401. There is **no** test that `update_payment=1` failure leaves status ACTIVE. There is **no** test that AUTO_CHARGE `Amount` equals SST Gross.

---

## What the code actually does

### 1. Token wire format

`MagicLinkTokenService` mints `Base64("{subscriptionId}:{expiryUnix}:{hmacHex}")`. HMAC-SHA256 is over `"{subscriptionId}:{expiry}"` with UTF-8 `Jwt:Secret`. Hex is lowercase 64 chars. TTL is 24 hours from mint.

```22:51:apps/lazuar-api/Modules/Commerce/Infrastructure/Security/MagicLinkTokenService.cs
    public string GenerateToken(Guid subscriptionId)
    {
        var expiry = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds();
        var payload = $"{subscriptionId}:{expiry}";
        var hash = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(_secret),
            Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        var tokenString = $"{payload}:{hash}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenString));
    }

    public Guid? ValidateToken(string token)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = decoded.Split(':');
            if (parts.Length != 3) return null;
            if (!Guid.TryParse(parts[0], out var subId)) return null;
            if (!long.TryParse(parts[1], out var expiry)) return null;

            var expectedHash = Convert.ToHexString(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(_secret),
                Encoding.UTF8.GetBytes($"{subId}:{expiry}"))).ToLowerInvariant();

            if (parts[2] != expectedHash) return null;
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiry) return null;

            return subId;
        }
```

Compare is `!=` on the hex string — **not** `CryptographicOperations.FixedTimeEquals`. Expiry is checked **after** HMAC (correct). Uppercase hex fails closed (generate always lowercases). Missing `Jwt:Secret` uses `"fallback_dev_secret_key"` (constructor line 19). Tests pin that fallback as a passing case (`MagicLinkTokenServiceTests.GenerateToken_UsesFallbackSecret_WhenJwtSecretMissing`).

The payload alphabet is GUID + unix + hex, so standard Base64 of current tokens is padding-heavy (`==`) and, in 20 000 random samples, did not emit `+` or `/`. That is an accident of the alphabet, not Base64url. Emails and most portal `href`s still concatenate the raw token with no `Uri.EscapeDataString` / `encodeURIComponent` (`MessageLinkBuilder` 36, portal page 174). `PortalPlanChange` is the exception (it encodes).

### 2. Arrears / update-payment gate (post-`9b531d2`)

Both public money routes take `[FromQuery] string token` and refuse before SQL if `ArrearsAccess` is false.

```36:38:apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs
            if (!await ArrearsAccess.IsAuthorizedAsync(tokenService, repository, token, subId))
            {
                return TypedResults.Unauthorized();
```

`ArrearsAccess`:

1. Blank token → false (does not even call `ValidateToken`).
2. Invalid / expired HMAC → false.
3. Token subject == path GUID → true **without loading rows**.
4. Else both rows must exist and share `OrganizationId` **and** `ClientProfileId`.

There is **no tenant slug** on `/public/commerce/checkout/{subId}/arrears`. The token is the only secret. A bare GUID with no token is 401. 008 P0-2’s “anyone with a v7 GUID can mint RM 1” is **closed on the handler**. TypeSpec `public-routes.tsp` 147–159 now documents `@query token: string` on both verbs.

Siblings are an explicit product rule, pinned by `ArrearsAccessTests.TokenForA_CanAccessSibling_SameOrganizationAndClient` and by `PortalSubscriptionAccess` (cancel / keep / change-plan). Portal GET lists every non-PENDING sub for that client (`CommerceQueryService.Portal.cs` 48). That is how one email token drives the dashboard.

Portal documents go **wider** than the sibling rule: `PortalDocumentQueryService` adds any CRM profile in the org with the same email (lines 57–63) and then pulls transaction logs by that email. A shared inbox across two CRM rows in one workspace sees both document sets.

### 3. What GET arrears returns

After the gate, Dapper loads commerce-only columns (L-03: no `crm.` / `one.` SQL — that is all `PublicArrearsEndpointsBoundaryTests` asserts). Amount is `SubscriptionBillingAmount.Gross` (unit snapshot or catalog price, seats, SST if `IBillingQueryService` says the merchant has an SST number).

`Is_reminder_only` is **not** `Subscription.IsReminderOnly`. It is `PaymentGatewayCapabilities.IsReminderOnlyGateway(row.ProductGatewayName)` — true for anything that is not Stripe/CHIP. A Stripe row that is reminder-only because of `ProcessZeroAmountCheckoutCommand` (`reminderOnly: true` at line 93) is advertised as “has a card to update.” 008 P1-15 is still in the file, now at line 65.

### 4. What POST update-payment charges

```142:165:apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs
            var isActiveUpdate = sub.Status == "ACTIVE";
            var billing = http.RequestServices.GetService<IBillingQueryService>();
            var chargeAmount = isActiveUpdate
                ? 1m
                : await ResolveGrossAsync(
                    billing,
                    sub.OrganizationId,
                    sub.UnitAmount,
                    sub.Quantity,
                    sub.Price,
                    sub.SstTaxType,
                    sub.SstRatePercent);
            // ...
            if (isActiveUpdate)
            {
                metadata["update_payment"] = "1";
            }
```

ACTIVE + gateway-reminder-only → 400 `REMINDER_ONLY`. ACTIVE + Stripe/CHIP → RM **1**, `SetupFutureUsage: true`, quantity 1, cache URL for **today** only. PAST_DUE / SUSPENDED → Gross (seats × unit + SST), metadata may include `dunning_campaign_id`. TRIALING / PENDING / CANCELED → 400.

Cache reuse: ACTIVE if `CurrentRenewalCheckoutForDate == UtcNow.Date`; arrears if that date equals `NextBillingDate.Date`. **Only ACTIVE persists the newly minted URL** (lines 197–204). PAST_DUE mint is returned and forgotten. Billing’s reminder-only mint (`RenewalCheckoutIssuer` + `SetCurrentRenewalCheckout`) is the only way a PAST_DUE cache hit happens.

Cancel URL keeps the inbound token (`...?token={token}`). Success URL is `/{slug}/portal` **with no token**.

`IBillingQueryService` is registered in Billing DI as scoped. The API host resolves it. DunningEngineJobTests do **not** register it, so those tests see Gross with `merchantHasSst: false`.

### 5. Completed vs failed payment on a subscription id

Completed path (`GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs` 38–53):

- `update_payment == "1"` **and** `Status == "ACTIVE"` → vault if the gateway can, log RM 1, **return**. Dates stay. `IsReminderOnly` becomes false if `StoreVaultedToken` runs.
- Else if SUSPENDED → `Resume` (advances next).
- Else if PAST_DUE → `RecoverFromPayment` (advances, clears dunning).
- Else → `Activate` which **also advances** dates for a healthy ACTIVE.

Failed path (`GatewayPaymentFailedIntegrationEventHandler.cs` 72–96):

- Skip PAST_DUE only for `CANCELED` or `SUSPENDED`.
- **Does not read `update_payment`.**
- Any other status, including healthy ACTIVE and TRIALING, becomes PAST_DUE and immediately runs `PastDueDunningProcessor`.

Stripe maps `payment_intent.payment_failed` to `PAYMENT_FAILED` and copies **PaymentIntent metadata**. Arrears checkout puts `subscription_id` and `update_payment=1` on `PaymentIntentData.Metadata`. A declined RM 1 is therefore a Commerce PAST_DUE event.

### 6. Dunning engine loop

One cycle (`DunningEngineJob.cs` 62–87): load every active campaign + steps (AsNoTracking, ignore tenant filters), read `Messaging:WhatsAppEnabled` (default **false**), run pre-dunning batch, then PAST_DUE batch.

Each batch (`Claim.cs`): up to 50 rows. Relational: `BEGIN`; `SELECT … FOR UPDATE SKIP LOCKED`; process; `SaveChanges`; `COMMIT`. In-memory (tests): no lock. Failed ids are excluded for the rest of the tick. Interval is `Workers:DunningEngineInterval` default 01:00:00.

**Pre-dunning claim** (SQL and in-memory agree):

- `Status = 'ACTIVE'`
- `CancelAtPeriodEnd` is not true
- collection pause is null or already elapsed
- `NextBillingDate` in `(now, now + 14 days]`

Not claimed: TRIALING, PAST_DUE, anything with `NextBillingDate <= now`, anything more than 14 days out, anything with dunning **pause** (`DunningPausedUntil` is **not** in this WHERE).

**PAST_DUE claim:**

- `Status = 'PAST_DUE'`
- `NextBillingDate IS NOT NULL`
- dunning pause null or elapsed

Statuses do not overlap. A row cannot be in both batches of the same tick. Overlap is **cross-worker**: Billing `StartPastDueDunningRunAsync` and Commerce `GatewayPaymentFailed` run the same processor **without** this claim lock, then the hourly job claims the same row.

### 7. Pre-dunning steps (live campaign, not snapshot)

`FindBest` on the in-memory campaign list. `daysUntilDue = (NextBillingDate.Date - now.Date).Days`. Due steps: `DayOffset < 0`, `daysUntilDue <= |DayOffset|`, action EMAIL/WHATSAPP/ALL, not already logged for `(DayOffset, TargetBillingDate)`.

Catch-up is intentional: at 3 days out, −14/−7/−3 all fire if not logged. The **claim window is 14 days**, so a −21 step never fires on day −21; the first time the row is visible (day −14) `14 <= 21` is true and the −21 copy goes out a week late.

AUTO_CHARGE with a negative offset is filtered out (not a comms action) and **does not** write a reminder log (`PreDunning_DoesNotAutoCharge`). The step is a zombie every hour.

WhatsApp demote: `DunningStepDispatcher.ResolveEffectiveCommunicationAction`. Flag false + WHATSAPP + no email body → skip publish but **still** `RecordReminderDispatched`. Flag false + WHATSAPP with email, or ALL → EMAIL, `whatsapp_body` cleared.

Pre-dunning uses the **live** campaign. `Snapshot_E9` pins that adding a −3 step after the first tick still fires. Mid-flight ops edits change pre-dunning. That is the documented contract; it is not a snapshot bug.

### 8. PAST_DUE processor (snapshot)

If `CurrentDunningCampaignId` is null: `FindBest` + `AssignDunningCampaign(id, Snapshot.From(live))`. No match → warn, return, row stays PAST_DUE forever.

Then, if `DunningPausedUntil > now`, return (assignment may already have happened — `HandleAsync_Paused_AssignsButDoesNotDispatch`).

Snapshot resolve:

1. Parse JSON; if v1 and `CampaignId` matches the pin, use it.
2. Else load **live** campaign (including archived) and `CaptureDunningCampaignSnapshot`. Pre-migration rows and `AssignDunningCampaign(id)` without JSON pick up **today’s** live definition, including edits made after the pin.

Due steps: `DayOffset >= 0 && DayOffset <= daysOverdue`, not logged for `(DayOffset, targetDate)`, ordered by offset.

**Same DayOffset is structurally one slot.** Reminder uniqueness is `(SubscriptionId, TargetBillingDate, DayOffset)` (`CommerceDbContext.cs` 312). The in-memory filter is the same key, not `StepId`. A campaign with day-0 EMAIL **and** day-0 AUTO_CHARGE runs whichever appears first in the ordered list; the other is invisible. Default seed avoids this (EMAIL 0/3, AUTO_CHARGE 1/5). A merchant-built “email and retry on the same day” campaign cannot.

AUTO_CHARGE:

- `cannotCharge` if product missing, `!SupportsOffSession` (Billplz/Razorpay/Xendit/blank), `IsReminderOnly`, attempt count ≥ 4, or empty vault. Skip publish, **consume offset**, usually **no** ChargeAttemptLog (Billplz/reminder-only/no-vault tests assert 0 logs).
- Hard decline already on the cycle: insert SKIPPED `hard_decline_skip`, consume offset.
- PENDING or SUCCEEDED already, or this tick already published one off-session: `consumeOffset = false`, do not publish. Two AUTO_CHARGE offsets due the same tick only fire one charge (`PastDue_TwoAutoChargeOffsetsDue_OnlyOneChargeThisTick`).
- Else insert PENDING dunning attempt, publish `ExecuteOffSessionChargeIntegrationEvent` with `SubscriptionBillingAmount.Gross(sub, product, billing)` and `ChargeAttemptId`.

`HasOpenDispute` is **not** in `cannotCharge`. The dispute handler now sets the flag (`CommerceGatewayDisputeCreatedHandler` 82, 124). Dunning still retries the card.

Terminal day = `max(max(0, grace), last DayOffset >= 0)`. Pre-dunning offsets do not delay cancel. FinalAction `CANCEL` / `SUSPEND` only; `NONE` leaves the row PAST_DUE after the last comms step. CANCEL calls `RecordChurn` on a **tracked** live campaign (separate load). SUSPEND does not increment churn.

`daysOverdue = (now.Date - NextBillingDate.Date).Days`. If `NextBillingDate` is still in the future (healthy ACTIVE thrown into PAST_DUE by an RM 1 decline), `daysOverdue` is negative, no past-due step matches, terminal is not reached. Billing claim excludes PAST_DUE. The row sits until the original due date.

### 9. Amounts (post-`eba0741`)

```70:72:apps/lazuar-api/Modules/Commerce/Infrastructure/Dunning/DunningStepDispatcher.cs
        var amount = product == null
            ? 0m
            : await SubscriptionBillingAmount.Gross(sub, product, billing);
```

AUTO_CHARGE uses the same `Gross`. Arrears GET/POST PAST_DUE use `Gross`. Billing off-session (report 02) uses `Gross`. 008 P1-5 / P1-9 are **closed in the call graph** if Billing’s query service is in the container.

The remaining money lie is the fallback:

```18:23:apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs
    public static decimal Unit(Subscription sub, Product product)
    {
        ArgumentNullException.ThrowIfNull(sub);
        ArgumentNullException.ThrowIfNull(product);
        return sub.UnitAmount > 0 ? sub.UnitAmount : product.Price;
    }
```

`UnitAmount == 0` (100% coupon first cycle, some trial activations, default constructor) charges **catalog `Price` × seats + SST**, not RM 0.

### 10. Portal verbs

| Verb | Token | Sibling | Extra guard |
|------|--------|---------|-------------|
| GET portal / documents / plans | required | list is all client subs | tenant slug must resolve |
| POST cancel | required | yes | `SubscriptionCancelDecision`; portal default `at_period_end ?? true`; PAST_DUE schedule falls through to immediate |
| POST keep | required | yes | 400 if already CANCELED |
| POST change-plan | required | yes | refuse PAST_DUE and `CancelAtPeriodEnd`; `PlanChangePolicy` ACTIVE/TRIALING |
| POST magic-link | none | n/a | always 200 |

TRIALING is now in `SubscriptionCancelDecision` (008 P0-4 closed). Portal shows cancel on `isHealthyForCancel` which includes TRIALING (`portal/page.tsx` 75). Plan change is only `isHealthyActive` (ACTIVE and not flagged) — trials cannot change plan from the UI.

If the portal page is opened **without** `?token=` but `/one/auth/me` returns a body (merchant cookie on the portal host), the page does **not** show the magic-link form; it calls the portal API with `token ?? ""` and `notFound()`s. Header “Buyer Dashboard” links to `/{tenantSlug}` and drops the token.

---

## Quoted walk — six money paths

### Walk A — Hourly PAST_DUE day 0 email (happy)

1. Job loads campaigns AsNoTracking.
2. Claim SQL takes one `PAST_DUE` row `FOR UPDATE SKIP LOCKED`, loads `ReminderLogs`.
3. Processor assigns campaign + snapshot if needed.
4. `daysOverdue >= 0` → day-0 EMAIL due.
5. Dispatcher publishes `FulfillmentRequestedIntegrationEvent(COMMUNICATIONS, reminder.dunning)` with Gross `amount` / `total_price`, `checkout_url` only if `CurrentRenewalCheckoutForDate.Date == NextBillingDate.Date`.
6. `RecordReminderDispatched(step.Id, targetDate, 0)`.
7. `SaveChanges` + commit.
8. Communications hydrator mints a **new** 24h HMAC and rewrites `{{renewal_link}}` / `{{update_payment_link}}` to `/{slug}/update-payment/{id}?token=…`. Hosted Billplz URL in `checkout_url` wins over that page (`FulfillmentRequestedIntegrationEventHandler` 108–110) and is **not** given `?token=` (hosted bills are gateway pages).

Pinned: `PastDue_Day0Email_PublishesReminderDunningAndRecordsLog`, `PastDue_Day0Email_IncludesCheckoutUrl_WhenMintedForCurrentDueDate`, `PastDue_Day0Email_SecondRunIsIdempotent`.

### Walk B — Vaulted Stripe fail → PAST_DUE → AUTO_CHARGE 2

1. Billing (report 02) inserts ChargeAttempt 1 PENDING and publishes off-session. Status stays ACTIVE.
2. Stripe `payment_intent.payment_failed` → Commerce fail handler marks attempt FAILED (soft unless decline code is in the hard table), `MarkAsPastDue`, runs processor **in the webhook**.
3. Day-0 EMAIL in the default seed fires immediately (`HandleAsync_FirstFail_DispatchesDay0Email_DoesNotOffSession`).
4. Hourly job later: day-1 AUTO_CHARGE. `cycleAttempts.Count + 1 == 2`. Publishes attempt 2 with Gross.
5. Hard `stolen_card` on attempt 1: day-1 becomes SKIPPED, offset consumed, no PI (`PastDue_HardDecline_DoesNotCharge_ConsumesOffset`).

### Walk C — ACTIVE “update card” RM 1 (the new P0)

1. Buyer opens `/{slug}/update-payment/{id}?token=…`. Page 404s without token.
2. GET arrears authorized → shows RM 1 copy (`update-payment/[subId]/page.tsx` 86–87).
3. POST mints Stripe Checkout amount `1m`, metadata `type=commerce_subscription`, `subscription_id`, `update_payment=1`, `SetupFutureUsage: true`.
4. **Success:** completed handler sees ACTIVE + flag, vaults, does not roll `NextBillingDate`. Intended.
5. **Decline:** Stripe emits `payment_intent.payment_failed` with the same metadata. Commerce fail handler **does not look at the flag**, `MarkAsPastDue()`, starts dunning. Billing will not touch a PAST_DUE row. If `NextBillingDate` is next month, `daysOverdue < 0` and **no** dunning email / AUTO_CHARGE runs until that date.

There is no test for step 5.

### Walk D — PAST_DUE “Complete Payment” twice

1. Vaulted fail path never minted a hosted URL (`CurrentRenewalCheckoutUrl` null).
2. First POST update-payment mints Gross, returns URL, **does not UPDATE the subscription**.
3. Second tab / second click mints a **second** Checkout session for the same Gross.
4. Buyer pays both (or pays the first, then a saved session).
5. First `PAYMENT_COMPLETED`: PAST_DUE → `RecoverFromPayment` → ACTIVE, dates +1 interval, dunning cleared.
6. Second `PAYMENT_COMPLETED`: status is ACTIVE, `update_payment` is **absent** (arrears mint does not set it) → `Activate(...)` **advances dates again**. Two captures, one cycle skipped.

Billing-minted Billplz URLs are cached and reused; this hole is specifically the no-URL PAST_DUE path (the common Stripe vault-fail path).

### Walk E — Pre-dunning −3 vs pause

1. Ops `PauseSubscriberDunning` sets `DunningPausedUntil`.
2. Pre-dunning claim SQL does not mention that column. Collection pause is excluded; dunning pause is not.
3. −3 EMAIL still sends. PAST_DUE batch would skip the same row after it actually fails.

`PreDunning_FlaggedActiveDueInThreeDays_DoesNotDispatchEmail` pins cancel-at-period-end. Nothing pins dunning-pause on pre-dunning.

### Walk F — Magic link always-200

1. `POST /{slug}/portal/magic-link` always returns `{ status: "ok" }` (`PublicPortalEndpoints.cs` 65–73).
2. Handler returns on blank email, unknown slug, unknown CRM email, or no subscription — **without** publishing.
3. Hit: `GetNewestSubscriptionForClientAsync` orders by `CreatedAt` desc with **no status filter**. A brand-new CANCELED row is the HMAC subject. Portal list still shows older ACTIVE siblings (sibling rule).
4. Comment says “Existing public-route throttle (if configured) is the only rate limit.” Grep of `apps/lazuar-api/src` finds **no** rate limiter on `/public/commerce`. The comment is a wish.

`RequestPortalMagicLink_UnknownEmail_NoDispatch_Returns200` does not touch HTTP. It asserts the handler published nothing.

---

## Bug catalog

Severity: **P0** = lost / stolen money or a healthy subscription forced into the recovery machine. **P1** = wrong amount, silent skip of a recovery step, or a recovery control that does not control. **P2** = crypto hygiene, scale, honesty, residual 008 items that no longer take money this week.

### B03-C01 — P0 — RM 1 / hosted-checkout decline marks a healthy subscription PAST_DUE

**Evidence.** Commerce fail handler, after resolving `subscription_id` / `receipt`:

```83:96:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs
        var becamePastDue = sub.Status != "PAST_DUE";
        if (becamePastDue)
        {
            sub.MarkAsPastDue();
            // ...
        }
        // ...
        await processor.ProcessAsync(
            _dbContext, _eventBus, sub, campaigns, whatsAppEnabled, CancellationToken.None, _billingQueryService);
```

No read of `update_payment`. Skip list is only `CANCELED` and `SUSPENDED` (74–80). TRIALING and ACTIVE are eligible.

Arrears POST **does** set the flag (PublicArrearsEndpoints 162–165). Stripe adapter copies PI metadata on `payment_intent.payment_failed` (`StripeGatewayAdapter.MapPaymentIntentPaymentFailed` 322–326) and checkout creation writes that metadata onto `PaymentIntentData` (`StripeGatewayAdapter` 495–499).

Completed handler is the only place that special-cases the flag, and only while the row is still ACTIVE (Subscription.cs handler 38–41). After this bug fires, a later success on a leftover session is no longer “method update only.”

**Repro.**

1. ACTIVE Stripe sub, `NextBillingDate` = +20 days, vault present.
2. Open `/{slug}/update-payment/{id}?token=valid`.
3. Complete Payment → Stripe test card `4000000000000002`.
4. Webhook lands. Row is PAST_DUE. `CurrentDunningCampaignId` assigned. Billing job will not claim it. Dunning past-due steps wait 20 days (`daysOverdue` negative).

**Blast.** Every “update card” decline (and any other hosted Checkout bound to `subscription_id` without the flag) is a false arrears event. Portal copy flips to “past due / cancel immediately.” AUTO_CHARGE may fire a **full Gross** if the due date is today. Merchant support sees a paying customer in dunning.

**Tests that would go red if fixed.** None exist. `GatewayPaymentFailedIntegrationEventHandlerTests` only cover ordinary off-session fails. Add: ACTIVE + metadata `update_payment=1` → status stays ACTIVE, no campaign assign, no `subscription.past_due`.

**Fix direction.** If `update_payment=1`, mark the attempt (if any) failed and return. Do not `MarkAsPastDue`. Optionally email “card not updated” without entering the campaign.

---

### B03-C02 — P0 — PAST_DUE update-payment mint is not cached; two completions double-capture and skip a cycle

**Evidence.** Persist branch is ACTIVE-only:

```197:204:apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs
                if (isActiveUpdate)
                {
                    await Dapper.SqlMapper.ExecuteAsync(connection, @"
                        UPDATE commerce.""Subscriptions""
                        SET ""CurrentRenewalCheckoutUrl"" = @Url, ""CurrentRenewalCheckoutForDate"" = @ForDate
                        WHERE ""Id"" = @SubId",
                        new { Url = checkoutUrl, ForDate = DateTime.UtcNow.Date, SubId = subId });
                }
```

Completed handler after first recover:

```71:83:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs
        if (wasSuspended) { existingSub.Resume(updatedNextBilling); }
        else if (existingSub.Status == "PAST_DUE")
        {
            existingSub.RecoverFromPayment(periodEnd, updatedNextBilling);
        }
        else
        {
            existingSub.Activate(periodEnd, updatedNextBilling, existingSub.IsReminderOnly);
        }
```

Arrears Gross mint does **not** set `update_payment`. Second completion is the `else` branch.

**Repro.** Stripe vault fail (no `CurrentRenewalCheckoutUrl`). Open Complete Payment twice before paying. Pay both Checkout sessions.

**Blast.** Buyer charged 2× Gross. `NextBillingDate` jumps two intervals. Dunning recovery metrics record only the first (`DunningRecoveryAttribution` returns null when `wasInArrears` is false). Ledger (report 05) books both.

**Tests.** None. Need: two POSTs without a stored URL create two sessions; after first recover, second completion must **not** call `Activate` / must treat as already-settled (idempotent on `GatewayTransactionId` or leftover PAST_DUE checkout).

**Fix direction.** Persist URL + `NextBillingDate` for PAST_DUE/SUSPENDED the same way Billing does. On completed, if already ACTIVE and metadata is a dunning/arrears pay (no `update_payment`), ignore date roll; refund-or-credit is a product decision but must not silently skip a cycle.

---

### B03-C03 — P1 — One reminder slot per DayOffset; same-day EMAIL + AUTO_CHARGE cannot both run

**Evidence.** Unique index:

```307:312:apps/lazuar-api/Modules/Commerce/Infrastructure/CommerceDbContext.cs
        modelBuilder.Entity<ReminderDispatchLog>(builder =>
        {
            builder.ToTable("ReminderDispatchLogs");
            builder.HasKey(x => x.Id);
            // Idempotency by DayOffset so campaign step ID regeneration does not re-fire or orphan logs.
            builder.HasIndex(x => new { x.SubscriptionId, x.TargetBillingDate, x.DayOffset }).IsUnique();
```

Processor filter (`PastDueDunningProcessor.cs` 93–97) is the same triple, not `step.Id`. Default seed separates offsets. Ops UI does not.

**Repro.** Campaign: day 0 EMAIL, day 0 AUTO_CHARGE. PAST_DUE day 0. Only the first in `OrderBy(DayOffset)` (stable by insert) runs. The other never appears in logs as a distinct step.

**Blast.** Merchants who design “email and retry the card today” get email-only or charge-only. Recovery rate drops; they think AUTO_CHARGE is broken.

**Tests.** Default-seed tests pass because offsets differ. Add a test that two steps at offset 0 both take effect **or** that create/update campaign rejects duplicate offsets.

**Fix direction.** Unique key `(Sub, Date, StepId)` **or** forbid duplicate offsets at campaign save **or** treat AUTO_CHARGE as not consuming the comms slot (separate attempt log is already the charge receipt).

---

### B03-C04 — P1 — Reminder log is written when Commerce publishes, not when the buyer is emailed

**Evidence.** Processor records the offset in the same unit of work that publishes `FulfillmentRequested`. Communications hydrate **throws** on missing `client_profile_id`, missing CRM profile, empty email, or empty EMAIL body (`FulfillmentRequestedIntegrationEventHandler.cs` 67–75, 78–86, 89–96, 193–201). Inbox can retry Communications; Commerce will not re-dispatch because the unique log exists.

**Repro.** PAST_DUE sub whose CRM profile has no email. Hourly tick: reminder log day 0 written, Communications throws, no Resend. Later ticks: `PastDue_Day0Email_SecondRunIsIdempotent` behaviour — silence.

**Blast.** Entire dunning timeline can be “green” in ops (`LastCompletedDayOffset` advances) with zero inbox. Terminal CANCEL still fires on grace (`Cancel_WhenNoPastDueSteps_OnGraceDay` is the empty-timeline cousin).

**Tests.** Job tests mock `IEventBus` and never run Communications. Add an integration that a thrown hydrate does **not** leave a reminder log, or a dead-letter that re-opens the offset.

**Fix direction.** Write the log only after Communications acks, **or** use a PENDING dispatch row, **or** do not consume on publish failure (outbox + inbox in one Commerce transaction with a delivery receipt).

---

### B03-C05 — P1 — ACTIVE update-payment is RM 1; Stripe MYR minimum in this repo is RM 2

**Evidence.** Arrears POST hard-codes `1m` (line 145). `CheckoutAmountRules.MyrMinimum = 2.00m` is enforced on **M2M** `CreateIntegrationCheckout`, not on `GenerateCheckoutSessionQuery` (Commerce cashier). Stripe adapter sends `UnitAmountDecimal = amount * 100` (100 sen). Stripe’s documented MYR floor is 2.00; the host’s own rule agrees.

**Repro.** ACTIVE MYR Stripe sub, Update payment method. Session create fails (`amount_too_small`) → portal `?err=1`. Or session creates and capture fails — then B03-C01.

**Blast.** The only authenticated “change card while healthy” path for Malaysian Stripe/CHIP is broken or flaky. Buyers stay on the old PM until PAST_DUE.

**Tests.** None assert RM 1 vs minimum. `CheckoutAmountRules` tests live under Payments and never call arrears.

**Fix direction.** Use Stripe Checkout `mode=setup` (no capture) for ACTIVE updates, **or** charge `max(2, verification)` and treat it as `update_payment` (and fix B03-C01). Do not leave RM 1 as the sold behaviour.

---

### B03-C06 — P1 — Arrears `is_reminder_only` is gateway-derived; Stripe reminder-only is sold as “update card”

**Evidence.** GET line 65; POST 109–113 uses the same helper. Row flag is `Subscription.IsReminderOnly`. `ProcessZeroAmountCheckoutCommand` still starts recurring subs with `reminderOnly: true` (line 93) even on Stripe (008 P0-3; 8b3567d vaulted a **different** $0 path). Portal hides the button using the **row** flag (`portal/page.tsx` 172). The update-payment **page** uses the GET DTO flag. A buyer who follows an email (always the GUID page) sees the RM 1 form. POST allows it. Success calls `StoreVaultedToken` which **clears** `IsReminderOnly` (`Subscription.cs` 279–284). Next cycle is a live off-session Gross of catalog price if `UnitAmount` is 0 (B03-C08).

**Repro.** 100% coupon Stripe monthly. Email “update payment”. Page says RM 1. Pay. Sub is no longer reminder-only.

**Blast.** Invoice-only Stripe buyers are converted to auto-debit without a hop-1 consent that said so.

**Tests.** 008 already named this. Still no test that GET `is_reminder_only` equals the row.

**Fix direction.** Return `s.IsReminderOnly`. POST must refuse `ACTIVE && sub.IsReminderOnly`, not just Billplz.

---

### B03-C07 — P1 — `DunningPausedUntil` does not pause pre-dunning

**Evidence.** Claim SQL pre-dunning (`DunningEngineJob.Claim.cs` 105–116) filters collection pause, not dunning pause. PAST_DUE SQL (118–126) filters dunning pause. `PauseSubscriberDunningCommandHandler` only writes the column.

**Repro.** Pause dunning 14 days on an ACTIVE due in 3 days. Hourly job still sends “renews soon.”

**Blast.** The control ops thinks they have (LP-080) does not stop the mail that is actually going out this week.

**Tests.** `PastDue_PausedUntilFuture_NotClaimed` and `Paused_SkipsTerminal` are PAST_DUE only. Add a pre-dunning twin.

**Fix direction.** Add the same `DunningPausedUntil` predicate to the pre-dunning claim (SQL + in-memory).

---

### B03-C08 — P1 — `UnitAmount == 0` Gross is catalog `Price`, not zero

**Evidence.** `SubscriptionBillingAmount.Unit` (`> 0` else `product.Price`). Seats `Max(1, Quantity)`. Used by AUTO_CHARGE, dunning email, arrears Gross, billing (02).

**Repro.** Sub with `UnitAmount = 0`, catalog 100, qty 3, SST 8%, merchant registered → arrears / AUTO_CHARGE 324, not 0.

**Blast.** Coupon / $0 snapshot rows over-collect on recovery. Combined with B03-C06 this is a surprise first auto-debit.

**Tests.** `SubscriptionBillingAmountTests` only use `unitAmount: 100`. Add `UnitAmount=0` → decide product (0 vs catalog) and pin it.

**Fix direction.** Treat `UnitAmount` as the source of truth including zero; only fall back to catalog when the snapshot was never written (`Activate` without unit).

---

### B03-C09 — P1 — Success and “dashboard” links drop the HMAC; buyer pays and cannot open the portal

**Evidence.**

- `RenewalCheckoutIssuer` 43: `successUrl = $"{clientUrl}/{workspace.Slug}/portal"` — no token.
- Arrears POST 139: same.
- Update-payment page 74 and 110: `<Link href={`/${tenantSlug}/portal`}>`.
- Portal layout 21–26: header “Buyer Dashboard” → `/{tenantSlug}`.

After a Billplz/Stripe success redirect the buyer hits the magic-link form. The token they already had is gone.

**Blast.** Paid-through buyer files “I paid and I’m locked out.” They request another link (B03-C10). Support load, not double charge — unless they also click a leftover session (B03-C02).

**Tests.** Billing tests assert cancel URL has `?token=mint-token` (`BillingEngineJobTests` ~316). Nobody asserts success URL.

**Fix direction.** Mint a fresh token into `successUrl` (and keep it on dashboard/header links when the request had one).

---

### B03-C10 — P1 — Magic-link endpoint is always-200 and unthrottled in this tree

**Evidence.** `PublicPortalEndpoints.cs` 65–73. Handler early-returns (`RequestPortalMagicLinkCommandHandler.cs` 34–55). No `AddRateLimiter` / public-commerce throttle under `apps/lazuar-api/src`. Timing: unknown email stops after CRM; known email + sub publishes an outbox event and `SaveChanges`.

Always-200 is the **correct** anti-enumeration shape. Unthrottled always-200 plus a measurable CRM/outbox delta is an oracle and an email-bomb.

**Repro.** Script `POST /public/commerce/{slug}/portal/magic-link` with a victim email 1 000 times.

**Blast.** Inbox flood; Resend spend; confirmation that the email is a customer if the attacker can see the mailbox or the send latency.

**Tests.** Handler unit test only. Add a limiter test and a constant-time/constant-work path.

**Fix direction.** Per-IP and per-email throttle on this route (the comment already pretends it exists). Optionally always enqueue a no-op delay.

---

### B03-C11 — P1 — HMAC compare is not constant-time; missing `Jwt:Secret` is a shared mint key

**Evidence.** `parts[2] != expectedHash` (`MagicLinkTokenService.cs` 48). Constructor: `_secret = configuration["Jwt:Secret"] ?? "fallback_dev_secret_key"`. Test **requires** the fallback to validate across two service instances.

If production ever boots without `Jwt:Secret`, anyone who knows the string and a subscription GUID (emails, webhooks, ops screens, v7 time leak) can mint a 24h portal token and pass `ArrearsAccess`.

Timing leak on a 64-char hex compare is the smaller half; still real against a hot validate endpoint.

**Tests.** `GenerateToken_UsesFallbackSecret_WhenJwtSecretMissing` would go **red** if the fallback were removed. That test is a landmine.

**Fix direction.** Fail closed without `Jwt:Secret`. `CryptographicOperations.FixedTimeEquals` on the hex UTF-8 bytes (or compare raw MAC). Switch to Base64url while versioning tokens. Add `ValidateToken_Expired_ReturnsNull`.

---

### B03-C12 — P1 — Failed-handler / Billing `StartPastDueDunningRunAsync` race the hourly claim

**Evidence.** Processor is invoked from three places: hourly job (row locked), Billing mint path (`BillingEngineJob.cs` 316–317, no dunning claim lock), Commerce fail handler (no lock). Reminder unique index and ChargeAttempt unique index are the only serialisers. Two publishers can both `PublishAsync` EMAIL before either `SaveChanges`. One insert wins; the other tick errors and is retried. Buyer can get two day-0 mails.

**Repro.** Stripe fail webhook and the hourly job on the same due row in the same second (or Billing mint + job).

**Blast.** Duplicate “you’re past due” + two hosted sessions if both minted. Unique-index exception on the job looks like a random dunning error.

**Tests.** All in-memory, single-threaded.

**Fix direction.** Take the same `FOR UPDATE` claim (or an advisory lock on `subscription.Id`) inside the fail handler and Billing start-run before `ProcessAsync`.

---

### B03-C13 — P1 — Grace 0 / last-step day cancels in the same tick as “please pay”

**Evidence.** `GraceZero_DispatchesDayZeroThenCancels` asserts EMAIL **and** `SubscriptionCanceled` in one `RunOnce`. Processor: dispatch loop, then terminal (`PastDueDunningProcessor.cs` 227–244) with no “wait for pay link to exist.”

**Repro.** Default-like campaign with grace 0 and a day-0 EMAIL. Mark PAST_DUE same day. Buyer gets a pay link for a CANCELED sub. Arrears POST then 400 “canceled.”

**Blast.** Recovery email is a lie. Chargeback/support.

**Tests.** The behaviour is **pinned as success**. That is a lying-adjacent test if product intent is “email then wait.”

**Fix direction.** Terminal on the **next** tick after the last comms offset, or require `daysOverdue > terminalDay`, or send a different “we canceled you” template instead of the pay template.

---

### B03-C14 — P1 — AUTO_CHARGE / Gross ignore `HasOpenDispute`

**Evidence.** `cannotCharge` (`PastDueDunningProcessor.cs` 114–119) does not read `HasOpenDispute`. The flag **is** written now (`CommerceGatewayDisputeCreatedHandler` 82, 124) — 008’s “dead boolean” is half-fixed. Dunning will still open attempt 2–4 on a card that is in chargeback.

**Blast.** Another PI on a disputed card. Scheme risk. Out of ledger scope but this is the dunning trigger.

**Fix direction.** Treat `HasOpenDispute` as `cannotCharge` (and skip billing attempt 1 in report 02).

---

### B03-C15 — P2 — Pre-dunning claim window is hardcoded 14 days

A −21 / −30 step cannot fire on time (`Claim.cs` 112, `PreDunning.cs` 36–38). First visibility is day −14, when the step catch-up-fires. Campaign builder does not warn.

**Fix.** Claim `NOW() + INTERVAL 'N days'` from `max(|negative offsets|)` among active campaigns, or store a per-org window.

---

### B03-C16 — P2 — TRIALING is invisible to pre-dunning

Claim requires ACTIVE. Trials due in 3 days get no “trial ending” comms from this engine. Cancel works (008 P0-4 closed). Update-payment UI is hidden for TRIALING; POST would 400 anyway.

---

### B03-C17 — P2 — Tokens are standard Base64 concatenated into query strings

`MessageLinkBuilder` 36, `RenewalCheckoutIssuer` 45, portal `href` 174, arrears cancel URL 140. `PortalPlanChange` encodes; everyone else does not. Current alphabet appears to avoid `+`/`/`; padding `=` is always present. A token-format change, or a proxy that strips `=`, breaks the gate you just added.

**Fix.** Base64url + `Uri.EscapeDataString` at every mint site.

---

### B03-C18 — P2 — Arrears / renewal mint always `SetupFutureUsage: true`

`PublicArrearsEndpoints` 190, `RenewalCheckoutIssuer` 63. On Billplz/Xendit this is ignored. On Razorpay it is a card-registration link for a reminder-only product (008 payments report). PAST_DUE Billplz still hits this path.

---

### B03-C19 — P2 — Snapshot lazy-backfill re-reads the live campaign

`ResolveSnapshotAsync` (`PastDueDunningProcessor.cs` 299–334): matching v1 JSON is frozen (E1–E5, `HandleAsync_AlreadyAssigned_LiveCampaignEditDoesNotRewriteSnapshot`). Null / corrupt / wrong `CampaignId` copies **live**, including later edits. `AssignDunningCampaign(Guid)` without JSON (still used in several tests and any pre-migration row) is that path. Production assign sites are supposed to use the snapshot overload (comment on `Subscription.cs` 370–372). They do, **after** first PAST_DUE. Manual pin + edit + first tick = mutated plan.

---

### B03-C20 — P2 — `DeclineClassifier` is a Stripe hard-code table; `expired_card` is soft

Hard: `incorrect_number`, `lost_card`, `pickup_card`, `stolen_card`, revocation pair, `authentication_required`, `highest_risk_level`, `transaction_not_allowed`. Soft: null, NSF, `card_declined`, **`expired_card`**, anything CHIP-shaped. `authentication_required` as HARD means 3DS cards never get AUTO_CHARGE 2–4 (maybe intended). CHIP codes all retry until max 4.

---

### B03-C21 — P2 — PENDING ChargeAttempt never times out

`hasInFlightOrSettled` defers AUTO_CHARGE forever while a row is PENDING (`PastDue_PendingAttempt_DoesNotPublish_DoesNotConsumeOffset`). Lost webhook = no further card retries; EMAIL steps and terminal still run. Conservative, but a stuck PENDING is silent.

---

### B03-C22 — P2 — Org-wide AUTO_CHARGE campaign is allowed on a Billplz-only tenant

`DunningCampaignAutoChargeGuard`: empty product list **returns** (lines 44–47). Default seed adds AUTO_CHARGE 1 and 5. Runtime skip + consume (B03-C03’s cousin). Ops thinks retries exist; logs say skipped.

---

### B03-C23 — P2 — Newest-sub token subject ignores status

`GetNewestSubscriptionForClientAsync` (`CommerceRepository.cs` 106–116): no `Status` filter. Newest CANCELED / PENDING is the HMAC subject. Sibling rule still opens ACTIVE rows. Confusing, rarely money.

---

### B03-C24 — P2 — Batch 50 / hour

`BatchSize = 50`, interval 1 hour, both modes. 2 000 PAST_DUE rows → ~40 hours to visit each. Catch-up still fires when visited; terminal is delayed by the queue. Pre-dunning has the same cap.

---

### B03-C25 — P2 — Portal documents merge by email, wider than ArrearsAccess

`PortalDocumentQueryService.cs` 57–77. Two CRM profiles, one inbox, one org: one token lists both document sets. Sibling rule on money verbs is tighter.

---

### B03-C26 — P2 — `InferPaymentMethod` is “has vault id”

No token → `MANUAL`. Unvaulted Stripe PAST_DUE does not match an ONLINE_GATEWAY-only campaign and gets **no** emails (`PastDue_EmailStep_NoMatchingCampaign_DoesNotPublish`). Default empty targets still match.

---

### B03-C27 — P2 — WhatsApp flag true still “dispatches”

Demote when **false** is correct and tested (`PastDue_WhatsAppOnlyNoEmailBody_RecordsLogWithoutPublish`). When `Messaging:WhatsAppEnabled=true`, ALL/WHATSAPP pass through; Messaging’s console stub sends. README honesty says WhatsApp is not shipping. Flipping the flag in prod is a lie, not a default-on bug.

Communications **Payment Failed** template uses `template.Channel` and does not demote in the Commerce dispatcher (separate handler). Messaging still skips WA when the flag is false.

---

### B03-C28 — P2 — Arrears API is not tenant-slug-bound

After HMAC, slug is irrelevant. 008 asked to bind slug. Residual: a stolen token works on `/public/commerce/checkout/{anySibling}/…` without knowing the workspace slug (the GUID is already in the email). Low extra risk given the token.

---

### B03-C29 — P2 — `current_period_end` in dunning copy is `NextBillingDate`

Dispatcher 88–90; portal SQL aliases `NextBillingDate as CurrentPeriodEnd`. For PAST_DUE that is the missed date. Templates that say “renews on” are a day-0 lie. Honesty, not overcharge.

---

### B03-C30 — P2 — No HTTP test that missing token is 401

`PublicArrearsEndpointsBoundaryTests` only forbids `crm."` / `one."` SQL. A future refactor that makes `token` optional would not go red. This is a test hole that protects B03-C01’s cousin (008 P0-2 regression).

---

## 008 re-verify (this slice only)

| 008 item | 008 verdict | This tree (`297ba98`) | 009 |
|----------|-------------|------------------------|-----|
| P0-2 Public GUID arrears / update-payment | Unauthenticated GUID; RM 1 / full mint | Handler requires HMAC; portal 404s without token; TypeSpec has `@query token`; emails append `?token=` (`9b531d2`) | **Closed** as P0. Residuals: B03-C09, B03-C17, B03-C28, B03-C30 |
| Siblings same `ClientProfileId` | (fix commit) | `ArrearsAccess` + `PortalSubscriptionAccess`; tests pin allow | **Intended**, not a bug. Documents-by-email is wider (B03-C25) |
| Cancel URLs `?token=` | Missing | `RenewalCheckoutIssuer` 45; arrears cancel 140; Billing test 316 | **Closed** |
| SST Gross on AUTO_CHARGE + arrears | P1-5 / P1-9 missing | Dispatcher + processor + arrears `ResolveGrossAsync` (`eba0741`); `IBillingQueryService` in host | **Closed in production call graph.** Tests do not pin SST on those paths (billing null in job tests) |
| ACTIVE update-payment RM 1 | Keep RM 1 | Still `1m` | **Still the product.** Now conflicts with MYR min (B03-C05) and fail handler (B03-C01) |
| Email amount = `product.Price` | P1-9 | Now Gross | **Closed** (fallback B03-C08 remains) |
| `is_reminder_only` gateway-derived | P1-15 | Line 65 unchanged | **Open** B03-C06 |
| Trial cancel | P0-4 | `SubscriptionCancelDecision` includes TRIALING; portal buttons on trial | **Closed** (not re-litigated here) |
| Zero-amount forced reminder-only | P0-3 | `ProcessZeroAmount` still `reminderOnly: true` | Still in tree; money effect is B03-C06 / B03-C08. Ownership: report 01 |
| Magic-link always-200 | Design | Still always 200; **no** throttle | Design kept; **open** as B03-C10 |
| WhatsApp demote | Stub | Flag default false; demote + Messaging skip | **Holds.** Flag-true is B03-C27 |
| Snapshot mutation | Asked freeze | Frozen when JSON matches; lazy backfill + pre-dunning live | **Mostly holds.** Residual B03-C19 |
| `HasOpenDispute` dead | P1-11 | Flag **is** written now; dunning still ignores it | Half-closed; **open** B03-C14 |
| Collection-pause billing starve | P0-1 | Out of scope (02). Pre-dunning **does** exclude collection pause | n/a here |

---

## Lying tests and tests that pin the wrong thing

1. **`PublicArrearsEndpointsBoundaryTests`** — name says “boundary.” Body is L-03 schema purity (`crm."`, `ClientProfiles`). It will stay green if token is removed tomorrow.

2. **`RequestPortalMagicLink_UnknownEmail_NoDispatch_Returns200`** — does not return 200. It never stands up HTTP. Always-200 is unpinned.

3. **`GenerateToken_UsesFallbackSecret_WhenJwtSecretMissing`** — encodes a production footgun as a feature.

4. **`PastDue_Day0Email_PublishesReminderDunningAndRecordsLog`** asserts `total_price == 50m` with no `IBillingQueryService`. An SST regression on the dispatcher is invisible.

5. **`PastDue_AutoCharge_StripeVault_PublishesStripeGateway`** / CHIP / attempt-2 tests never assert `Amount`. `eba0741` is unpinned on the charge event.

6. **`GraceZero_DispatchesDayZeroThenCancels`** pins B03-C13 as success.

7. **`ProcessZeroAmount_Recurring_ActivatesReminderOnly`** (completeness suite) still asserts the 008 P0-3 behaviour. Checkout report’s problem; it feeds B03-C06.

8. **`HandleAsync_DoesNotPublishDispatchMessage`** only forbids `Modules.Messaging.Contracts.DispatchMessageIntegrationEvent` on the **Commerce** bus. Communications has its own fail-email handler. The test name over-claims “no mail.”

9. **In-memory `ClaimSubscriptionInMemoryAsync`** never exercises `SKIP LOCKED`, unique-index races, or the interpolated `NOT IN ('guid')` SQL. Green CI is not a locked-row proof.

10. **No test** for expired HMAC, for `update_payment` failure, for missing HTTP token, for duplicate DayOffset, for `UnitAmount == 0` Gross, for pre-dunning + `DunningPausedUntil`.

Honest tests that should stay: snapshot E1–E9, hard-decline skip, pending defer, Billplz/Razorpay/reminder-only AUTO_CHARGE skip, two PAST_DUE in one batch, flagged ACTIVE excluded from pre-dunning, `ArrearsAccess` no-token / sibling / different-client.

---

## Unread / not claimed

- `InvoiceReminderJob` and quote AR (custom checkout reminders).
- `BillingEngineJob` due-claim SQL, collection-pause starve, attempt-1 amount (report 02). Cited only where it starts PAST_DUE dunning or mints cancel URLs.
- Payments adapter HTTP, EventId, CHIP/Billplz unpaid callbacks as EventId collisions (report 04). Cited only for metadata copy and MYR minimum.
- Ledger, refunds, dispute-as-refund (report 05). `HasOpenDispute` write is noted because dunning should read it.
- One login, invites, API keys (report 07). `/one/auth/me` on the portal page is cited only as a UX fork.
- `ConsoleMessagingService` internals (report 08).
- Ops dunning-campaign UI (report 09).
- Checkout hop-1/hop-2, coupons, $0 vault path details (report 01) except `ProcessZeroAmount`’s `reminderOnly: true` as an input to arrears honesty.

---

## Ranked open bugs

| Rank | ID | Sev | One line |
|------|----|-----|----------|
| 1 | B03-C01 | P0 | Declined RM 1 / any `subscription_id` fail throws a healthy sub into PAST_DUE. |
| 2 | B03-C02 | P0 | Uncached PAST_DUE mint; two Checkouts → two captures + skipped cycle. |
| 3 | B03-C05 | P1 | RM 1 vs MYR 2 minimum; card-update path fails or never captures. |
| 4 | B03-C06 | P1 | Stripe reminder-only sold as update-card; success clears the flag. |
| 5 | B03-C08 | P1 | `UnitAmount==0` recovers at catalog Price × seats + SST. |
| 6 | B03-C03 | P1 | Same DayOffset cannot hold EMAIL and AUTO_CHARGE. |
| 7 | B03-C04 | P1 | Reminder unique log burns the step if Communications never sends. |
| 8 | B03-C07 | P1 | Dunning pause does not stop pre-dunning mail. |
| 9 | B03-C12 | P1 | Webhook/Billing vs hourly job can double-send day 0. |
| 10 | B03-C13 | P1 | Grace 0 emails “pay” and cancels in the same tick. |
| 11 | B03-C14 | P1 | AUTO_CHARGE ignores `HasOpenDispute`. |
| 12 | B03-C09 | P1 | Success / dashboard drop the HMAC after the buyer pays. |
| 13 | B03-C10 | P1 | Always-200 magic-link, no throttle in tree. |
| 14 | B03-C11 | P1 | Timing compare + compiled-in fallback secret. |
| 15 | B03-C15–C30 | P2 | 14-day window, trial pre-dunning, Base64url, SetupFutureUsage, backfill, classifier, PENDING timeout, org-wide AUTO_CHARGE, newest-canceled token, batch 50, email-merge docs, MANUAL infer, WA flag, slug bind, period-end alias, missing HTTP 401 test. |

**Do not ship another “GUID is enough” regression** (B03-C30). **Do not treat `9b531d2` / `eba0741` as done** until C01, C02, C05, C06, C08 have tests that fail if those commits are reverted or bypassed.

---

## Fix order (direction only — this report does not implement)

1. Fail handler: honor `update_payment=1` (C01). Test it.
2. Persist PAST_DUE minted URL; make second completion idempotent (C02).
3. ACTIVE update = setup mode or ≥ RM 2 (C05), still flagged so C01 cannot fire.
4. Arrears DTO + POST use **row** `IsReminderOnly` (C06). Gross must not invent catalog price from a true zero snapshot (C08).
5. Campaign save: unique DayOffset or split charge vs comms receipts (C03). Delivery-acked reminder logs (C04).
6. Pre-dunning claim respects `DunningPausedUntil` (C07). Lock processor callers (C12). Terminal after last comms tick (C13). Skip AUTO_CHARGE when `HasOpenDispute` (C14).
7. Success URLs keep a token (C09). Throttle magic-link (C10). Kill fallback secret + constant-time compare (C11). HTTP 401 test (C30).

Code wins. The HMAC gate is real. The fail handler and the uncached arrears mint can still take money and status that `9b531d2` does not protect.
