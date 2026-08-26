---
number: "003"
id: B03-C01
severity: P0
status: resolved
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
resolved_branch: fix/003-update-payment-decline-not-pastdue
---

# 003 — B03-C01 — RM 1 / hosted-checkout decline marks a healthy subscription PAST_DUE

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/003-update-payment-decline-not-pastdue`

`update_payment=1` (or `true`) on `GatewayPaymentFailed` updates the charge attempt and returns. Status stays ACTIVE. No campaign, no `subscription.past_due`.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

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

