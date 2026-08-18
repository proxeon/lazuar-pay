---
number: "221"
id: B04-P20
severity: P2
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 221 — B04-P20 — Stripe setup `PAYMENT_COMPLETED` with null token if SetupIntent expand fails

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P20 — P2 — Stripe setup `PAYMENT_COMPLETED` with null token if SetupIntent expand fails

**Where.** `StripeGatewayAdapter.cs:107-125`. Catch logs warning, continues, still returns `PAYMENT_COMPLETED` amount 0 (`130-146`).

**What.** Buyer finished setup. We tell Commerce “paid / vaulted” with no PM. Commerce vault persist requires both ids (other slice). Subscription may activate reminder-only after a setup checkout. `setup_intent.succeeded` is not a backup map.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Stripe `$0` + `setupFutureUsage` is Checkout **setup** mode (`CreateCheckoutSessionOptions` at `StripeGatewayAdapter.cs:554-572`). The money event we consume is still `checkout.session.completed` / `payment_intent.succeeded` mapped to `PAYMENT_COMPLETED` with `AmountPaid = (AmountTotal ?? 0) / 100`. When there is no PaymentIntent (setup session), `ReadSetupSessionVaultIds` copies customer + PM from an **already-expanded** SetupIntent on the event object. If the event JSON only has `setup_intent: "seti_…"`, the adapter `GetAsync`s the SetupIntent with `Expand = payment_method`. Any exception in that fetch is logged as a warning and **swallowed**. The method still returns `Verified=true`, `EventType=PAYMENT_COMPLETED`, `AmountPaid=0`, `GatewayTokenId=null`. Commerce `GatewayPaymentCompletedIntegrationEventHandler` then runs the paid path. `TryVaultIds` now only requires a non-blank token (customer falls back to the token string) — if the token is null, `hasVault` is false and open checkout starts the seat with `reminderOnly: true`. The buyer finished a vaulting checkout; we told Commerce “completed” with nothing to charge later. `setup_intent.succeeded` is still an unmapped Stripe type: `ParseWebhookAsync` falls through to verified passthrough (`stripeEvent.Type`, `stripeEvent.Id`); the handler allow-list drops it. There is no backup extract if `checkout.session.completed` is lost or the SI expand failed.

### Still present?
**STILL BROKEN**

Expand/fetch failure still continues into `PAYMENT_COMPLETED`:

```114:156:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs
                    else
                    {
                        ReadSetupSessionVaultIds(session, ref customerId, ref paymentMethodId);
                        if (string.IsNullOrEmpty(paymentMethodId)
                            && !string.IsNullOrEmpty(session.SetupIntentId))
                        {
                            try
                            {
                                var client = new StripeClient(apiKey);
                                var siService = new SetupIntentService(client);
                                var si = await siService.GetAsync(session.SetupIntentId, new SetupIntentGetOptions
                                {
                                    Expand = new List<string> { "payment_method" }
                                });
                                paymentMethodId = si.PaymentMethodId;
                                customerId ??= si.CustomerId;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to fetch Stripe SetupIntent for payment method extraction.");
                            }
                        }
                    }
                    // ...
                    return new GatewayWebhookParsedResult(
                        Verified: true,
                        EventType: "PAYMENT_COMPLETED",
                        // ...
                        GatewayTokenId: paymentMethodId
                    );
```

Unmapped types, including `setup_intent.succeeded`, still passthrough (`StripeGatewayAdapter.cs:296`). Handler still drops anything not in `PAYMENT_COMPLETED` / `PAYMENT_FAILED` / `DISPUTE_*` / `REFUND_COMPLETED` (`ProcessGatewayWebhookCommandHandler.cs:83-90`). Commerce still reminder-only without a token:

```84:91:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Helpers.cs
        if (string.IsNullOrWhiteSpace(tokenId))
        {
            return false;
        }

        vaultTokenId = tokenId;
        vaultCustomerId = string.IsNullOrWhiteSpace(customerId) ? tokenId : customerId;
        return true;
```

```92:106:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs
            var hasVault = TryVaultIds(...);
            Modules.Commerce.Application.SubscriptionActivation.Start(
                ...
                reminderOnly: !hasVault,
```

Happy-path extract still works when the event JSON embeds the SetupIntent (`ParseWebhook_CheckoutSessionCompleted_SetupIntentWithoutPi_ExtractsCustomerAndPaymentMethod`). Issue 075 (`fix/075-skip-zero-gmv-setup`) now skips `$0` GMV ledger booking (`GatewayPaymentCompletedHandler.cs:47-49`) — that is a Billing honesty fix, not a vault fix.

### Related files
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` — setup-mode generate, SI extract, swallow-on-fetch-fail, unmapped passthrough.
- `apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` — publishes `GatewayPaymentCompleted` for any `PAYMENT_COMPLETED`, including amount 0 / null token.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs` — activates reminder-only when vault ids missing.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Helpers.cs` — `TryVaultIds`.
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` — correctly skips `$0` as GMV (075).
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/StripeGatewayAdapterTests.cs` — expanded-SI happy path only.

### Tests
- Existing tests: `StripeGatewayAdapterTests.CreateCheckoutSessionOptions_ZeroAmountWithSetup_UsesSetupMode`; `ParseWebhook_CheckoutSessionCompleted_SetupIntentWithoutPi_ExtractsCustomerAndPaymentMethod` (JSON **embeds** expanded SetupIntent); `ReadSetupSessionVaultIds_WhenSetupIntentAndNoPi_ExtractsCustomerAndPaymentMethod`; `ParseWebhook_UnmappedType_IsVerifiedWithStripeType` (uses `customer.updated`, not `setup_intent.succeeded`).
- Whether any test would fail if the bug is still there: **no**. The audit already named this: the setup-session test “does not test the fetch-on-unexpanded path or the expand-failure path”.
- What a first regression test should assert: `checkout.session.completed` in `mode=setup` with `setup_intent` as a **string id** and a stub `SetupIntentService` that throws → result is **not** `PAYMENT_COMPLETED` with null token (either `Verified=false` so Stripe retries, or a dedicated non-completed type). Second: `setup_intent.succeeded` with `payment_method` + `customer` maps to `PAYMENT_COMPLETED` and fills `GatewayTokenId` (backup if session.completed was lost).

### Reproduction today
Arrange: Stripe test mode, tenant CHIP/Stripe off-session product, `$0` trial or 100% coupon so Commerce mints setup-mode Checkout (`InitiateCheckoutCommandHandler` vaulting-recurring path). Act: complete Checkout, then either (a) deliver a `checkout.session.completed` fixture whose `setup_intent` is an unexpanded id and whose `GetAsync` fails (revoke API key, airplane mode on the SI fetch), or (b) drop `checkout.session.completed` and deliver only `setup_intent.succeeded`. Assert: (a) Payments still publishes `GatewayPaymentCompleted` amount 0 with `GatewayTokenId=null`; Commerce subscription is `ACTIVE`/`TRIALING` with `reminderOnly` and no stored PM. (b) handler ACKs 200 and publishes nothing.

### Blast radius
Stripe `$0` vault checkouts only (trials, 100% coupons, setup-mode). Buyer is not charged. Seat may activate as **reminder-only** so BillingEngine will never off-session charge it — silent product lie, not a double-charge. Real-money PI path is unaffected. Frequency: only when SI expand/fetch fails (Stripe outage, bad key, unexpanded event + network) or when `checkout.session.completed` is lost. PII: none extra. Ops: “vaulted trial that never auto-renews”.

### Suggested fix
Do not publish `PAYMENT_COMPLETED` for setup-mode until a non-empty `GatewayTokenId` exists. On SI fetch failure return `Verified=false` (Stripe retries `checkout.session.completed`) rather than a silent completed. Optionally map `setup_intent.succeeded` as a backup extract (same EventType `PAYMENT_COMPLETED`, business key on SetupIntent id so it dedupes with the session event). Do not invent a Stripe Billing `subscription.updated`. Do not change TypeSpec. LP-059 remains next-renewal-only. Commerce already reminder-onlys a missing token — do not “fix” that by activating off-session with a null PM.

### Evaluation notes
Duplicates: B04-P22 / 223 (`setup_intent.succeeded` still dropped); B05-L02 / 075 (`$0` no longer books GMV — residual is vault, not ledger). Severity still **P2** (no captured money; fulfillment of a `$0` vault is a capability lie). Not blocked. Residual after 161-200: 072 made Stripe currency fail-closed (setup session without currency now refuses to invent `myr`) but the null-token completed path is unchanged. Audit line “Commerce vault persist requires both ids” is slightly stale: `TryVaultIds` now requires only the token and synthesizes customer from it.


