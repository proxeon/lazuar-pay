---
number: "001"
id: B01-C01
severity: P0
status: resolved
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
resolved_branch: fix/001-trial-hop2-activate
---

# 001 — B01-C01 — `type=trial` hop-2 is dropped; Stripe/CHIP trials never activate

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/001-trial-hop2-activate`

New vaulting trials stamp `commerce_subscription` (not `type=trial`). `IsCommerceSubscriptionType` also accepts leftover `trial` so in-flight Stripe setup sessions still activate `TRIALING` with vault ids.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C01 — `type=trial` hop-2 is dropped; Stripe/CHIP trials never activate

**Severity:** P0  
**One-sentence fault:** Initiate stamps vaulting trial hop-2 as `type=trial`; `GatewayPaymentCompleted` returns before looking at the OPEN session because `IsCommerceSubscriptionType` does not accept `"trial"`.

**Evidence.**

Initiate, after `MergeClientIntoGateway` (which would have set `commerce_subscription`):

```299:299:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
                vaultMetadata["type"] = isTrial ? "trial" : "commerce_subscription";
```

Filter:

```33:36:apps/lazuar-api/Modules/Commerce/Application/CommerceCheckoutMetadata.cs
    public static bool IsCommerceSubscriptionType(string? type) =>
        string.Equals(type, TypeCommerce, StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, TypeSaas, StringComparison.OrdinalIgnoreCase);
```

```33:37:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs
        var type = @event.Metadata.GetValueOrDefault("type");
        if (!CommerceCheckoutMetadata.IsCommerceSubscriptionType(type) && type != "custom_payment_link")
        {
            return;
        }
```

Stripe will emit `PAYMENT_COMPLETED` for setup-mode `checkout.session.completed` and will pass the metadata through (`StripeGatewayAdapter.ParseWebhookAsync`, `EventType: "PAYMENT_COMPLETED"`, `Metadata: meta`). Commerce never reaches `HandleOpenCheckoutSessionAsync`. `SubscriptionActivation.Start` → `ActivateTrial` is dead on the vaulting path.

008 (`plans/008-evals/01-commerce-subscriptions-checkout.md` §6 Trial) claimed: “Vaulting gateways: $0 setup-future hop 2, `type=trial`. Webhook `HandleOpenCheckoutSessionAsync` then `SubscriptionActivation.Start`.” That paragraph describes the **intended** wire. The type filter was not re-read. The intention is not the live behaviour.

**Reproduction in words.** Merchant creates a monthly Stripe product with `TrialDays = 14`. Buyer submits hop-1. Initiate persists an OPEN session, mints a Stripe Checkout session in `mode=setup`, returns that URL, `IsZeroAmountBypass = false`. Buyer completes card setup. Stripe fires `checkout.session.completed`. Payments publishes `GatewayPaymentCompletedIntegrationEvent` with `type=trial`, `subscription_id={sessionId}`, customer + payment method ids. Commerce handler returns. Session remains OPEN. Public status poller stays `PENDING`. Success page spins. No `Subscription` row. No `TRIALING`. No `SubscriptionActivatedIntegrationEvent`. After 24 hours the expiry job marks EXPIRED. The card is a Stripe Customer + PaymentMethod with no Commerce row to hang it on.

Billplz trial does **not** hit this bug: `SupportsOffSession("BILLPLZ")` is false, so initiate calls `ProcessZeroAmount`, which `ActivateTrial(..., reminderOnly: true)` in-process.

A 100% coupon on a **non-trial** Stripe monthly product is also not this bug: `type` is `commerce_subscription`, webhook accepts it, vaults, `Activate`s.

**Blast radius.** Every Stripe or CHIP product with `TrialDays > 0`. The sold trial path (card on file, convert at day N) does not create the membership. Buyer thinks they started a trial. Merchant sees an OPEN then EXPIRED session and no subscriber. Integrators waiting on `subscription.activated` never fire fulfillment. The orphaned Stripe customer/PM is not attached to anything Commerce can charge.

**Why tests missed it.** `SubscriptionTrialTests` only hit `ActivateTrial` and `SetTrialDays` on the domain. `InitiateCheckout_HundredPercentCoupon_StripeMonthly_MintsHop2SetupSession` asserts `type == "commerce_subscription"`, not a trial product. `IsCommerceSubscriptionType_AcceptsSaasAlias` asserts commerce + saas true and custom false; it never mentions `"trial"`. `GatewayPaymentCompleted_NonCommerceType_LeavesSessionOpen` uses `utility_credit_topup` and thereby **pins the drop behaviour** for any non-allowlisted type, which includes `trial`.

**Fix direction (do not implement).** Stop sending `type=trial`. Keep `commerce_subscription` (or saas) and let `SubscriptionActivation.IsTrialOffer(product)` decide `ActivateTrial` — the open-checkout handler already does that. Alternatively add `"trial"` to the allow-list **and** to `IsCommerceSubscriptionType`, and add a test: initiate trial Stripe → webhook with `type=trial` (or whatever you stamp) → one `TRIALING` row with vault ids. Do not treat this as a Payments bug; Payments is doing what Commerce asked.

---

