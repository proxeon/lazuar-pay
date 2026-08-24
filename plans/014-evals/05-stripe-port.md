# 05 — Stripe port: Hub `StripeGatewayAdapter` vs new Pay `StripeHosted` + `WebhookEndpoints`

**Family:** 014-evals  
**Slice:** Stripe specifically. Steal HTTP judgment. Do not clone `Modules/Payments`.  
**Date:** 24 August 2026  
**Type:** Uncondensed evaluation. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) cells.

---

## 0. SHA, trees, method

| | |
|--|--|
| **Repo** | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` |
| **Branch** | `main` (`ref: refs/heads/main`) |
| **HEAD** | `ee2db8e5758305089a38298456c456d6bf0e97ca` (`ee2db8e5`) — `feat(pay): Bar B receipts, webhook secret, merchant money UI` |
| **Parent index** | [014 README](./README.md) — this file is the Stripe evidence, not a bullet digest of the index |

Files actually opened and quoted (not “I know this module”):

| Tree | Path | Lines / role |
|------|------|----------------|
| Hub | `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` | **742 lines, read in full** |
| Hub | `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs` | minor units, paying-tenant metadata, fee stamp |
| Hub | `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/PaymentGatewayFactory.cs` | five-name factory |
| Hub | `apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs` | port + `GatewayWebhookParsedResult` |
| Hub | `apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs` | Stripe row of the honest matrix |
| Hub | `apps/lazuar-api/Modules/Payments/Infrastructure/Endpoints.cs` | `POST /webhooks/payments/{gatewayType}/{tenantId}` |
| Hub | `ProcessGatewayWebhookCommandHandler.cs` + `.Idempotency.cs` + `.Metadata.cs` + `.Logging.cs` | Stripe event types, dual-event key, outbox publish |
| Hub | `ExecuteOffSessionChargeIntegrationEventHandler.cs` | Stripe off-session caller |
| Hub | `CheckoutSessionCashier.cs` | decrypt BYOK, `KEY_MODE_MISMATCH`, BILLPLZ last resort |
| Hub | `Contracts/Events/GatewayPaymentCompletedIntegrationEvent.cs` | the event new Pay must not reintroduce |
| Hub | `StripeGatewayAdapterTests.cs` | Connect-fee ban, setup mode, `PAYMENT_COMPLETED` on setup |
| Hub | `ProcessGatewayWebhookCommandHandlerTests.cs` | Stripe dual-event, outbox requeue, mismatch |
| Hub | `Modules.Payments.Infrastructure.csproj` + `apps/lazuar-api/Directory.Packages.props` | Stripe.net **48.0.1** |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Gateways/StripeHosted.cs` | **entire file, 48 lines** |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs` | **entire file, 104 lines** |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs` | **entire file** — BYOK PUT/GET |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` | how Stripe success becomes `paid` |
| New | `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` | buyer `POST /v1/pay/{token}/start` |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` | DI: `StripeHosted`, `Fulfillment`, no MediatR |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj` | Stripe.net **48.0.0** |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs` | AES-GCM wrap for `sk_`, not `whsec` |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs` + `PayDbContext.cs` | `gateway_credentials`, `psp_webhook_events` |
| New | `apps/lazuar-pay/tests/Lazuar.Pay.Tests/WebhookTests.cs` | **entire file** |
| New | `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPayTests.cs` | empty-body 400 lives here, not in `WebhookTests` |
| New | `apps/lazuar-pay/.env.example` | `Pay__StripeWebhookSecret` comment |
| Law | [011/01](../011-new-lazuar-pay/01-product.md), [011/03](../011-new-lazuar-pay/03-first-slice.md), [011/11](../011-new-lazuar-pay/11-checklist.md) `NP-GW-002` / `NP-GW-008` / `NP-XX-012` |
| Law | [013/06](../013-prods/06-money-rails.md) standing law + Stripe HTTP extract + webhook pipeline |
| Historical | [008/02](../008-evals/02-payments-adapters-rails.md) §3 Stripe — **wrong in places; live files win** |
| Historical | [007/04](../007-feats/04-stripe.md) Connect refuse, Stripe.net 48.0.1 |

Method: Hub adapter is **HTTP judgment** (what to POST to Stripe, what to verify, which event types are cash). New Pay is the living host. Steal the decision. Do not copy MediatR, outbox, `IPaymentGatewayAdapter`, factory of five, Stripe Billing, Connect `application_fee_amount`, or `GatewayPaymentCompletedIntegrationEvent`.

**Production-ready Stripe on new Pay: no.** Dogfood-ready for **one** org with a **test** `sk_` and a **single** `whsec_` in process config, against cards in `mode=payment`, with a tunnel: **narrow yes**. Multi-merchant BYOK on live keys: **no**. Reasons are in §16, not in this sentence.

---

## 1. Standing law this slice is scored against

From [013/06 §0.1](../013-prods/06-money-rails.md) and [011/01](../011-new-lazuar-pay/01-product.md), restated only because this paper applies them to Stripe files:

1. **BYOK.** Money settles on the **merchant’s** Stripe account. Pay is software, not an acquirer, not a Merchant of Record, not Stripe Connect `application_fee_amount`.
2. **`mode=payment` for charge.** `mode=setup` is **not** paid (`NP-GW-008`). Hub still maps setup to `EventType: "PAYMENT_COMPLETED"` with `AmountPaid: 0`. Steal the HTTP extract of customer + PM. **Do not steal the event name.**
3. **Never Stripe Billing `subscription.updated` as source of truth** (`NP-XX-012`). Never `mode=subscription`. Never instantiate `Stripe.Subscription`. Pay’s later billing job mints a checkout or an off-session charge.
4. **Webhook:** verify; empty body **400**; idempotent `(org_id, provider, event_id)`; retry no-ops. Same-handler fulfillment. Do **not** reintroduce `GatewayPaymentCompletedIntegrationEvent`.
5. **Steal adapters as HTTP judgment.** IsolationTests already ban `MediatR` / `BuildingBlocks` / `Modules.` / `lazuar-api` in the new host.

[011/11](../011-new-lazuar-pay/11-checklist.md) rows that this slice talks about (status left `todo` / `refuse` in that file; 014 does not flip them):

| ID | Feature | Notes on the checklist |
|----|---------|------------------------|
| NP-GW-002 | Stripe card checkout | S1, Dogfood Y, “Off-session only if a real PM/token exists” |
| NP-GW-008 | Never treat setup / setup-intent as paid | Fail mode in [011/03](../011-new-lazuar-pay/03-first-slice.md): “Setup session counted as paid” is a **fail lock** |
| NP-XX-012 | Stripe Billing `subscription.updated` as SoT | **refuse** |

[011/03](../011-new-lazuar-pay/03-first-slice.md) fail lock, quoted:

> Fail (do not paper over):  
> - Setup session counted as paid.  
> - Webhook retry double-journals.

---

## 2. Hub Stripe adapter — the 742-line HTTP map (quote the living file)

Path: `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs`. Class implements `IPaymentGatewayAdapter`. `GatewayType => "STRIPE"`. Constructor is only `ILogger`. **No** `StripeClient` singleton; every call does `new StripeClient(apiKey)` with the **tenant** key the cashier decrypted. That is the BYOK shape.

### 2.1 Generate hosted Checkout (`GenerateCheckoutAsync`, lines 22–44 + `CreateCheckoutSessionOptions` 556–623)

HTTP: `SessionService.CreateAsync` against Stripe Checkout Sessions.

```22:44:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs
    public async Task<GatewayCheckoutResult> GenerateCheckoutAsync(
        string apiKey, Guid tenantId, decimal amount, string currency,
        string productName, string customerEmail,
        string successUrl, string cancelUrl, Dictionary<string, string> metadata,
        string? merchantId, bool setupFutureUsage = false, int quantity = 1)
    {
        try
        {
            var client = new StripeClient(apiKey);
            var service = new SessionService(client);
            var options = CreateCheckoutSessionOptions(
                tenantId, amount, currency, productName, customerEmail,
                successUrl, cancelUrl, metadata, setupFutureUsage, quantity);

            var session = await service.CreateAsync(options);
            return new GatewayCheckoutResult(true, session.Url, session.Id, null);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe checkout generation failed for Tenant {TenantId}", tenantId);
            return new GatewayCheckoutResult(false, null, null, ex.Message);
        }
    }
```

`merchantId` is unused on Stripe (CHIP Brand ID / Billplz Collection ID). Fine.

`CreateCheckoutSessionOptions` is the judgment:

1. `ApplyPayingTenantMetadata` — keep incoming `tenant_id` (platform SaaS charges on Hub); stamp `platform_tenant_id` when the adapter tenant differs. **New Pay has no system org.** Steal “do not clobber the paying org.” Do **not** steal Hub platform checkout.
2. **`$0` + `setupFutureUsage` → `Mode = "setup"`.** Comment on the file: “A `$0` PaymentIntent is invalid.” Line items are omitted. `SetupIntentData.Metadata = metadata`. `CustomerCreation = "always"`. Card-only PM list. **This session is a vault, not a capture.**
3. Else **`Mode = "payment"`**. One line item. Currency lowercased. `UnitAmountDecimal = GatewayCommon.ToMinorUnits(amount, currency)` (zero-decimal ISO currencies are **not** ×100; half away from zero). Product name or `GatewayCommon.DefaultProductName` (`"Lazuar Payment"`). Quantity is the line-item quantity.
4. **Metadata copied onto both Session and PaymentIntent** (`PaymentIntentData.Metadata = metadata`). That is why Hub can fulfill `payment_intent.succeeded` using the same `checkout_id` / `tenant_id`.
5. `ApplyCardWalletPaymentMethodTypes`: `PaymentMethodTypes = ["card"]` only. Comment: wallets (Apple Pay / Google Pay) ride on `card`; listing `apple_pay` / `google_pay` is invalid. **This list replaces Dashboard dynamic PMs.** Stripe FPX / GrabPay / Link will **not** appear on a Lazuar-created session.
6. `ApplySetupFutureUsage` when true on a **payment** session: `PaymentIntentData.SetupFutureUsage = "off_session"` and `CustomerCreation = "always"`. Comment: without a Customer, Stripe often returns no reusable PM.

Quoted generate options (payment branch):

```591:622:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            CustomerEmail = !string.IsNullOrWhiteSpace(customerEmail) ? customerEmail : null,
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency.ToLowerInvariant(),
                        UnitAmountDecimal = GatewayCommon.ToMinorUnits(amount, currency),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = string.IsNullOrWhiteSpace(productName) ? GatewayCommon.DefaultProductName : productName
                        },
                    },
                    Quantity = quantity,
                }
            },
            Metadata = metadata,
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Metadata = metadata
            },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
        };

        ApplyCardWalletPaymentMethodTypes(options);
        ApplySetupFutureUsage(options, setupFutureUsage);
        return options;
```

Setup-mode branch (must never be booked as cash):

```570:588:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs
        // $0 + vault: Checkout setup mode (SetupIntent). A $0 PaymentIntent is invalid.
        if (amount == 0 && setupFutureUsage)
        {
            var setupOptions = new SessionCreateOptions
            {
                Mode = "setup",
                Currency = currency.ToLowerInvariant(),
                CustomerEmail = !string.IsNullOrWhiteSpace(customerEmail) ? customerEmail : null,
                Metadata = metadata,
                SetupIntentData = new SessionSetupIntentDataOptions
                {
                    Metadata = metadata
                },
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                CustomerCreation = "always",
            };
            ApplyCardWalletPaymentMethodTypes(setupOptions);
            return setupOptions;
        }
```

Hub tests lock Connect refuse on this generate path (`StripeGatewayAdapterTests.CreateCheckoutSessionOptions_HasNoApplicationFeeOrTransfer_AndKeepsPayingTenant` and `PaymentAdapters_DoNotSetConnectApplicationFeeOrTransfer`):

- `options.PaymentIntentData.ApplicationFeeAmount` is **null**.
- `TransferData` is **null**.
- Source of all five adapters must not contain `ApplicationFeeAmount`, `application_fee`, `TransferData`, `transfer_data`.

There is **no** `Stripe-Account` header, no Connect client, no destination charge. `new StripeClient(apiKey)` with the merchant `sk_` **is** the platform model. Steal that. Never add `application_fee_amount`.

### 2.2 Parse webhook (`ParseWebhookAsync`, lines 46–319)

Auth is **not** Bearer. Auth is `Stripe-Signature` + `webhookSecret` (the **tenant’s** `whsec_`, decrypted in the command handler from `TenantPaymentConfiguration.WebhookSecret`).

Pipeline:

1. Missing `Stripe-Signature` (case-insensitive key scan) → `Verified=false`, error `"Missing Stripe-Signature header."`
2. `EventUtility.ConstructEvent(rawBody, signature, webhookSecret)` — Stripe library HMAC + ~300s timestamp. `StripeException` → `Verified=false`. Other construct failures → `AsUnusable()`.
3. **Mapped as money / vault / dispute / refund** (below).
4. Anything else → verified passthrough with **raw** `stripeEvent.Type` and `stripeEvent.Id`. Handler then **drops** it (see §3). That is how `customer.subscription.updated` / `invoice.paid` stay non-SoT **without a dedicated refuse branch** — they are never mapped to `PAYMENT_COMPLETED`. Accidental correctness. New Pay should keep that: unknown type after verify is **200 ignore**, not fulfill.

**Honor as cash (Hub maps to `PAYMENT_COMPLETED`):**

| Stripe type | Object | Hub `EventType` | `EventId` | `GatewayTransactionId` | Amount |
|-------------|--------|-----------------|-----------|------------------------|--------|
| `checkout.session.completed` | `Session` with PaymentIntent | `PAYMENT_COMPLETED` | `stripeEvent.Id` (`evt_…`) | `session.PaymentIntentId ?? session.SetupIntentId ?? session.Id` | `(AmountTotal ?? 0) / 100m` |
| `checkout.session.completed` | `Session` **setup**, no PI | `PAYMENT_COMPLETED` **amount 0** if PM extracted; else `Verified=false` so Stripe retries (B04-P20) | `evt_…` | SetupIntent id | `0` |
| `payment_intent.succeeded` | `PaymentIntent` | `PAYMENT_COMPLETED` | `evt_…` | `pi.Id` | `AmountReceived / 100m` |
| `setup_intent.succeeded` | `SetupIntent` | **`PAYMENT_COMPLETED` amount 0** if PM present | `evt_…` | `si.Id` | `0` |

That last two setup rows are the **Hub lie** [013/06](../013-prods/06-money-rails.md) named: “There is **no** distinct `SETUP_COMPLETED` type. Setup is stuffed into `PAYMENT_COMPLETED`.” Steal customer + PM extract. **Do not steal `PAYMENT_COMPLETED` as the name.** New Pay must not book this as cash (`NP-GW-008`).

Fee extract on the cash path: extra HTTP `PaymentIntentService.GetAsync(id, Expand = latest_charge.balance_transaction [, payment_method])`. `ApplyBalanceTransactionFee` copies `Abs(bt.Fee / 100m)` and FX. Expand failure logs a warning, stamps `gateway_fee_status=unknown`, **does not block fulfillment**. `TaxAmount` on a Session is `(session.TotalDetails?.AmountTax ?? 0) / 100m`. There is **no** `automatic_tax` on generate, so this is Stripe Tax if the Dashboard enabled it, not SST. New Pay journal does not yet book fee/tax lines (cash + revenue only). Steal “unknown ≠ 0” (`NP-MON-002`) when fees exist; do not invent 0 as known.

Currency: `GatewayCommon.TryNormalizeCurrency` — 3-letter, uppercased. Missing currency → unusable, “refusing to invent MYR.” Steal fail-closed. Billplz inventing MYR is a different rail.

**Honor as failed (not paid):**

- `payment_intent.payment_failed` → `PAYMENT_FAILED`, decline code copied into metadata (`decline_code`), `Error = LastPaymentError.Message`. Handler publishes `GatewayPaymentFailedIntegrationEvent`. Late `PAYMENT_FAILED` after `PAYMENT_COMPLETED` for the same PI is ignored (`PAYMENT_COMPLETED:` + tx id lookup).

**Honor as dispute (not paid, not a journal reverse by itself):**

- `charge.dispute.created` → `DISPUTE_CREATED`
- `charge.dispute.closed` → `DISPUTE_CLOSED` + `meta["dispute_outcome"] = status`
- `charge.dispute.updated` → `DISPUTE_CLOSED` if status is `won` / `lost` / `warning_closed`, else treated as `DISPUTE_CREATED`

Amount = `dispute.Amount / 100m`. Metadata pulled from the PaymentIntent when possible (second HTTP GET). New Pay has **no** dispute table. Do not port this on day one of BYOK dogfood. Do not drop it from the steal-list for later.

**Honor as refund completed:**

- `TryMapRefundCompleted`: `Refund` object with `status == succeeded`, or a `Charge` whose refunds list has a succeeded refund. Maps to `REFUND_COMPLETED`. **`EventId = refund.Id` (`re_…`), not `evt_…`.** Business key is **null** so PI-level collapse does not eat later refund slices. Pending refunds pass through as raw type (test: `ParseWebhook_RefundUpdatedPending_IsNotCompleted` expects `EventType == "refund.updated"`). Hub `IssueRefundAsync` itself treats only `succeeded` as success (`IsRefundSucceeded` is **not** pending — 008 claimed pending counted; **live helper is succeeded-only**).

**Ignore (verified passthrough, handler returns without log):**

- `customer.updated`, `customer.subscription.updated`, `invoice.paid`, `invoice.payment_succeeded`, `charge.succeeded`, `payment_intent.created`, `payment_intent.processing`, Radar, Billing Portal, etc.
- There is **no** `case "customer.subscription.updated"` in the adapter. `Mode` on generate is never `"subscription"`. That is `NP-XX-012` encoded by omission.

**Unusable after verify:** missing currency; JSON after verify; setup session with no PM (`RefuseSetupSessionWithoutToken`, `Verified=false` so Stripe retries — Hub **wants** the retry until a PM exists, because Commerce used the same event type for vault). New Pay must **not** retry-storm setup: 200 `ignored: setup_or_zero` (already the new host’s choice).

### 2.3 Off-session charge (`ChargeOffSessionAsync`, lines 321–356)

HTTP: `PaymentIntentService.CreateAsync` with:

- `Amount = GatewayCommon.ToMinorUnits(amount, currency)`
- `Currency` lowercased
- `Customer` + `PaymentMethod` (vault ids)
- `OffSession = true`, `Confirm = true`
- metadata `type=commerce_subscription`, `subscription_id`, `tenant_id`, `receipt`, optional `dunning_campaign_id`, `charge_attempt_id`, SST keys
- idempotency `lazuar-offsession:{chargeAttemptId}`

Success is **`succeeded` only** (`IsOffSessionSucceeded`). Tests lock `processing` / `requires_action` / `failed` as false. 008 said `processing` counted; **live does not**. `StripeException` → `OffSessionDeclinedException` with decline code.

Caller: `ExecuteOffSessionChargeIntegrationEventHandler` — MediatR/integration event, capability short-circuit (`SupportsOffSession` = Stripe or CHIP), decrypt tenant `sk_`, then **does not book cash on adapter `true`**. Hub waits for `payment_intent.succeeded` to publish completed. Steal: “adapter true is not a `RCPT-`.” New Pay must not build this for Bar B. `NP-FUL-004` is V1. [013/06](../013-prods/06-money-rails.md): “Do not build `ChargeOffSessionAsync` in order to prove the first `RCPT-`.”

### 2.4 Refund (`IssueRefundAsync`, lines 358–379)

HTTP: `RefundService.CreateAsync` with `PaymentIntent = transactionId`, `Amount = GatewayCommon.ToMinorUnits(amount)` (currency default MYR inside that helper), idempotency `lazuar-refund:{transactionId}:{minor}`. Returns `IsRefundSucceeded(refund.Status)` — **succeeded only**. New Pay has **no** refund route. Do not copy yet. When you do, you need a **PaymentIntent id** on the charge row. Today new Pay stores `ProviderRef = session.Id` (`cs_…`). That is a later trap (gap, high for refunds, not for first capture).

### 2.5 Customer portal (`GenerateCustomerPortalAsync`, lines 721–741)

HTTP: `CustomerService.ListAsync({ Email, Limit = 1 })` then `Stripe.BillingPortal.SessionService.CreateAsync`. First customer with that email wins. No customer → throw.

This is **Stripe Billing Portal**. It is not Pay’s buyer magic-link. [013/05](../013-prods/05-checkout-frontend.md) and G23: **not v1**. New Pay correctly has **zero** portal code. Keep it that way. Update-payment later is Pay-hosted + wrap-rails, not Stripe Billing as SoT.

### 2.6 Things the Hub adapter does **not** do (search results)

| Search | Hub `StripeGatewayAdapter.cs` |
|--------|-------------------------------|
| `setup_intent` | Mapped: session without PI fetches `SetupIntentService.GetAsync`; `TryMapSetupIntentSucceeded` on `setup_intent.succeeded`. Event name is the lie. |
| `payment_intent` | Generate: `PaymentIntentData` metadata + setup_future_usage. Parse: `payment_intent.succeeded` / `payment_intent.payment_failed`. Off-session: create+confirm. |
| `checkout.session` | Generate + parse `checkout.session.completed`. |
| customer portal | `GenerateCustomerPortalAsync` — Billing Portal. |
| refund | `IssueRefundAsync` + `TryMapRefundCompleted`. |
| `ChargeOffSession` | Yes, as above. |
| `SetupFutureUsage` | Yes, payment-mode vault + $0 setup-mode. |
| `PaymentMethod` | Extracted for vault ids; not Elements; no ephemeral keys. |
| `ephemeral` | **Zero hits** under `Modules/Payments`. No Stripe.js / Payment Element / ephemeral key. Wrap is hosted Checkout. |
| tax | Session `TotalDetails.AmountTax`; off-session SST **metadata** (`sst_tax_amount`). No `automatic_tax` on Session create. |
| amount zero | Generate: $0 + setupFutureUsage → setup mode. Parse: amount 0 still `PAYMENT_COMPLETED`. |
| `EventType PAYMENT_COMPLETED` | Cash **and** setup. |
| `application_fee` / `ApplicationFeeAmount` / `TransferData` | **Forbidden by test.** Not in source. |
| `customer.subscription` / `mode = "subscription"` | **Not in the adapter.** |
| Connect `Stripe-Account` header | **Not present.** |

---

## 3. Hub `ProcessGatewayWebhookCommandHandler` — Stripe-related branches (do not copy the shape)

HTTP door: `POST /webhooks/payments/{gatewayType}/{tenantId:guid}` (`Modules/Payments/Infrastructure/Endpoints.cs`). Allow-list includes `STRIPE`. Empty body **400** `"Empty request body."` (B04-P18). Then `IMediator.Send(ProcessGatewayWebhookCommand)`. ACK `{ received: true }`. Comment on the endpoint: **“Intake ACK only. Domain fulfillment lives in Commerce / Billing / M2M session.”** That split is the cathedral new Pay exists to leave.

Handler core (Stripe-relevant):

1. Load `TenantPaymentConfiguration` for `(tenantId, gatewayType)`. Missing config or empty `WebhookSecret` → throw `"Webhook secret not configured for this tenant gateway."` **Per-tenant `whsec_`.** Decrypt with `ISecretVault`. Decrypt API key too (fee expand uses it).
2. `adapter.ParseWebhookAsync(plainApiKey, plainWebhookSecret, rawBody, headers, 0, 0, 0)` — estimated fee/tax on the port are dead (always 0).
3. `Verified=false` + unusable → `PaymentWebhookUnusablePayloadException` → HTTP **400**. `Verified=false` otherwise → `InvalidOperationException` “verification failed”. Endpoints **do not catch** `InvalidOperationException` (the `when` filter excludes it) → **HTTP 500 on bad signature.** [013/06](../013-prods/06-money-rails.md) anti-goal 12: “Signature fail 500. Hub does this. Stripe retries until the endpoint looks like an outage.” **Steal 400 from new Pay’s current code, not Hub’s 500.**
4. Event types that proceed: `PAYMENT_COMPLETED`, `DISPUTE_CREATED`, `DISPUTE_CLOSED`, `PAYMENT_FAILED`, `REFUND_COMPLETED`. Anything else **return** (200, no log). That drop is how Billing events stay non-SoT.
5. Inbound `metadata.tenant_id` ≠ URL tenant, unless `IsPlatformCheckoutWebhook` (system org + `platform_tenant_id`) → warn, return, no publish. New Pay has no system org; mismatch must **200 ignore**, never 400 (Stripe retries poison).
6. Idempotency: unique `(OrganizationId, Provider, EventId)` plus **business key** `EVENTTYPE:GatewayTransactionId` so `checkout.session.completed` and `payment_intent.succeeded` for the same `pi_…` collapse. Tests: `Handle` twice with different `evt_` and same `piId` publishes **one** `GatewayPaymentCompletedIntegrationEvent`. Refunds: business key null.
7. Unique-violation `23505` on save is swallowed (concurrent delivery). Steal the idea; do not steal outbox.
8. **Publish** `GatewayPaymentCompletedIntegrationEvent` onto keyed `"PaymentsEventBus"` / outbox. Existing log with Dead outbox → `TryRequeueDeadOutboxAsync`. Missing outbox → republish. This is the fulfillment **delay**. New Pay same-handler rule forbids it.

`PublishParsedEventAsync` for the completed case (the type IsolationTests would fail if copied):

```264:279:apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs
        var completedEvent = new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: request.TenantId,
            GatewayTransactionId: parsedResult.GatewayTransactionId ?? parsedResult.EventId,
            AmountPaid: parsedResult.AmountPaid,
            Currency: parsedResult.Currency,
            GatewayFee: parsedResult.GatewayFee,
            TaxAmount: parsedResult.TaxAmount,
            NetAmount: parsedResult.NetAmount,
            FxRate: parsedResult.FxRate,
            BaseCurrency: parsedResult.BaseCurrency,
            LineItems: new List<LineItemDto>(),
            Metadata: metadata,
            GatewayCustomerId: parsedResult.GatewayCustomerId,
            GatewayTokenId: parsedResult.GatewayTokenId);
        log.AssignOutboxMessageId(completedEvent.Id);
        await _eventBus.PublishAsync(completedEvent);
```

New Pay grep for `GatewayPaymentCompletedIntegrationEvent` under `apps/lazuar-pay`: **zero hits.** Keep it that way.

Metadata merge (`ProcessGatewayWebhookCommandHandler.Metadata.cs`): adapter metadata wins; `IntegrationCheckoutSession` fills missing keys; **force** `checkout_id` from the session row. New Pay must **not** grow `IntegrationCheckoutSessions`. Stamp `checkout_id` + `org_id` on the Stripe Session at create time (new Pay already does). Billplz-style merge is a later rail.

---

## 4. Hub `PaymentGatewayCapabilities` — Stripe row

```8:58:apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs
public static class PaymentGatewayCapabilities
{
    public static bool SupportsOffSession(string? gatewayName)
    {
        var g = Normalize(gatewayName);
        return g is "STRIPE" or "CHIP";
    }
    // ...
    public static bool SupportsApiRefund(string? gatewayName)
    {
        var g = Normalize(gatewayName);
        return g is "STRIPE" or "CHIP" or "RAZORPAY" or "XENDIT";
    }
    public static bool SupportsDuitNowQr(...) => g is "XENDIT" or "CHIP" or "BILLPLZ";
    public static bool SupportsHostedWallet(...) => XENDIT or CHIP + GRABPAY/SHOPEEPAY/TNG/...;
    public static bool SupportsEmandate(...) => false; // every name
    public static bool RequiresMarkRefunded(...) => blank / BILLPLZ / OFFLINE / ...;
}
```

Stripe implications for new Pay:

| Axis | Stripe |
|------|--------|
| Hosted checkout | Y |
| Off-session if a real PM exists | Y (`NP-GW-002` note). Not until vault exists. |
| FPX e-mandate | N (`SupportsEmandate` false) |
| DuitNow QR as our pixel | N |
| Hosted GrabPay as our flag | N (card-only session wrap) |
| API refund | Y |
| Mark-refunded SOP | N |
| Apple/Google Pay | Wrap via `card`, not a product |
| Reminder-only | **No** — Stripe is auto-debit **if vaulted** |

New Pay has **no** capability matrix type. `StripeHosted` is the only rail. That is honest for Bar B. When CHIP arrives, copy the **two axes that have readers** (off-session, API refund), not unread `SupportsDuitNowQr` chrome. Do not copy the static class into a Contracts assembly for a sibling folder.

---

## 5. Who calls the Hub adapter (so we do not port the callers)

| Caller | Job | Port to 8081? |
|--------|-----|----------------|
| `CheckoutSessionCashier.GenerateAsync` | Decrypt tenant `sk_`, stamp `hub_payment_environment`, `KEY_MODE_MISMATCH` on `sk_test_` vs live, factory → `GenerateCheckoutAsync` | Steal decrypt + test/live guard. **Do not** steal BILLPLZ last resort (`return "BILLPLZ"` when no config). |
| `GenerateCheckoutSessionQueryHandler` / detailed / M2M `CreateIntegrationCheckoutCommandHandler` | All go through the cashier | Do not port M2M or Hub query handlers. New Pay `POST /v1/checkouts` + public `POST /v1/pay/{token}/start`. |
| `Endpoints.MapPaymentsEndpoints` | PSP webhook HTTP | New path `/v1/webhooks/{provider}/{orgId}` already exists. |
| `ProcessGatewayWebhookCommandHandler` | Verify + outbox | Replace with in-process fulfill. |
| `ExecuteOffSessionChargeIntegrationEventHandler` | Billing/dunning debit | V1. Not Bar B. |
| `GenerateCustomerPortalQueryHandler` | Billing Portal URL | Refuse as SoT. |
| Commerce `InitiateCheckoutCommandHandler` `SetupFutureUsage: Interval != one_time` | Vault on first paid recurring | Later, with `NP-GW-008` still true for $0. |

Factory:

```14:24:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/PaymentGatewayFactory.cs
    public IPaymentGatewayAdapter GetAdapter(string gatewayType)
    {
        var normalizedType = gatewayType.ToUpperInvariant();
        var adapter = _adapters.FirstOrDefault(a => a.GatewayType == normalizedType);
        if (adapter == null)
            throw new InvalidOperationException($"Payment gateway type '{gatewayType}' is not supported.");
        return adapter;
    }
```

New Pay `WebhookEndpoints` hard-codes `StripeHosted.Provider`. `GatewayEndpoints` 400s anything other than `"stripe"` (`"Bar B first rail is stripe"`). That is the anti-factory. Keep it until CHIP is an explicit G10 flip.

---

## 6. New Pay `StripeHosted.cs` — entire living generate path

```1:48:apps/lazuar-pay/src/Lazuar.Pay/Gateways/StripeHosted.cs
using Lazuar.Pay.Data;
using Lazuar.Pay.Secrets;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace Lazuar.Pay.Gateways;

public sealed class StripeHosted(PayDbContext db, SecretBox box)
{
    public const string Provider = "stripe";

    public async Task<string> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)
    {
        var cred = await db.GatewayCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == checkout.OrgId && x.Provider == Provider, ct);
        if (cred is null)
        {
            throw new InvalidOperationException("rail not configured");
        }

        var secret = box.Unprotect(cred.Ciphertext);
        var service = new SessionService(new StripeClient(secret));
        var cents = (long)Math.Round(checkout.Amount * 100m, MidpointRounding.AwayFromZero);
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = checkout.Id,
            SuccessUrl = checkout.SuccessUrl ?? "http://localhost:5179/c/" + checkout.PublicToken + "?status=verifying",
            CancelUrl = checkout.CancelUrl ?? "http://localhost:5179/c/" + checkout.PublicToken,
            Metadata = new Dictionary<string, string> { ["checkout_id"] = checkout.Id, ["org_id"] = checkout.OrgId },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = checkout.Currency.ToLowerInvariant(),
                        UnitAmount = cents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions { Name = "Pay" }
                    }
                }
            ]
        }, cancellationToken: ct);
        return session.Url ?? throw new InvalidOperationException("Stripe returned no URL");
    }
}
```

Caller: `PublicPayEndpoints.Start` (`POST /v1/pay/{token}/start`). Buyer has **no** One account. Decrypts **that checkout’s org** `sk_` from `gateway_credentials`. StripeException → 503 `"Stripe rejected the org key"`. Missing rail → 503 `"rail not configured"`. Pause → 403. Paid/expired → 409.

`Program.cs` registers `AddScoped<StripeHosted>()` and `AddScoped<Fulfillment>()`. No MediatR. IsolationTests still ban Hub tokens.

---

## 7. New Pay `WebhookEndpoints.cs` — entire living parse + fulfill entry

```10:104:apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs
internal static class WebhookEndpoints
{
    public static void MapWebhooks(this WebApplication app)
    {
        app.MapPost("/v1/webhooks/{provider}/{orgId}", Handle);
    }

    static async Task<IResult> Handle(...)
    {
        if (string.Equals(provider, StripeHosted.Provider, StringComparison.OrdinalIgnoreCase) == false)
            return PayErrors.Status(400, "Bad Request", "unknown provider");

        // raw body
        if (string.IsNullOrWhiteSpace(json))
            return PayErrors.Status(400, "Bad Request", "empty body");

        // org has a stripe sk_?
        if (!configured)
            return PayErrors.Status(400, "Bad Request", "rail not configured");

        var whsec = config["Pay:StripeWebhookSecret"];
        if (string.IsNullOrWhiteSpace(whsec))
            return PayErrors.Status(503, "Service Unavailable", "Pay:StripeWebhookSecret missing");

        EventUtility.ValidateSignature(json, sig.ToString(), whsec); // StripeException → 400 invalid signature
        stripeEvent = EventUtility.ConstructEvent(json, sig.ToString(), whsec, throwOnApiVersionMismatch: false);
        // construct fail → 400 invalid event

        // idempotency Find then insert psp_webhook_events (org, provider, event_id)
        // duplicate → 200 { duplicate: true }

        if (stripeEvent.Type is "checkout.session.completed")
        {
            if (session.Mode == "setup" || (session.AmountTotal is null or 0))
                return Results.Json(new { ignored = "setup_or_zero" }, ...);

            var checkoutId = session.ClientReferenceId ?? session.Metadata?["checkout_id"];
            if (!string.IsNullOrWhiteSpace(checkoutId))
                await fulfillment.FulfillPaidAsync(checkoutId, StripeHosted.Provider, session.Id, ct);
        }

        return Results.Json(new { ok = true }, ...);
    }
}
```

Path matches [013/06 §5.1](../013-prods/06-money-rails.md) and pay-spec `POST /v1/webhooks/{provider}/{orgId}`. Not Hub `/webhooks/payments/...`. Not Plane A `/v1/one/webhooks`. No Bearer. Isolation-legal.

---

## 8. New Pay BYOK paste — `GatewayEndpoints.cs`

`PUT /v1/orgs/{orgId}/gateway` + `GET` same. Writer (owner/admin via `/me` role, not OpenFGA `admin` relation — G12 checklist said `authz/check admin`; live is `RequireWriterAsync` on whoami). Member GET sees `last4` + `configured`. Body `provider` must be `"stripe"`. Secret required. `SecretBox.Protect` AES-GCM with `Pay:WrapKey` (dev fallback SHA256 of `"lazuar-pay-dev-wrap-key"` — production must set wrap key). Stores **one** ciphertext column. **No webhook-secret column.** Capability advertised: `"hosted_link"`.

That PUT is the merchant `sk_test_` / `sk_live_`. It is **not** the `whsec_`. Hub ops pasted **both**. New Pay `.env.example`:

```
# Stripe webhook signing secret (whsec_…). Checkout secret key is BYOK per org.
# Pay__StripeWebhookSecret=
```

The comment is honest. The architecture is **not** yet BYOK on the verify axis. See §12.

---

## 9. How Stripe success becomes `paid` — `Fulfillment.cs`

Webhook calls `FulfillPaidAsync(checkoutId, "stripe", session.Id, ct)` **in the same HTTP request**. No outbox. No `GatewayPaymentCompletedIntegrationEvent`. That is the same-handler rule, at process level.

`FulfillPaidAsync` (full file read):

1. **Its own** `BeginTransactionAsync` (not the webhook’s insert txn).
2. Load checkout by **id only** (not org). Missing → return (still 200 at webhook, event already saved).
3. `checkout.Amount <= 0` → return (second `NP-GW-008` fence).
4. Status not `"open"` → commit empty, return (replay-safe **if** the first attempt actually paid).
5. `OrgSettings.SstRegistered is null` → throw `"SST registration unknown; fail closed"`. Checkout create currently **inserts** `SstRegistered = false` when settings are missing, so dogfood does not hit this throw.
6. `checkout.Status = "paid"`. Insert `charges` (`Provider = "stripe"`, `ProviderRef = session.Id` i.e. `cs_…`, amount from **checkout row not Stripe `AmountTotal`**).
7. Optional payer row from checkout name/email.
8. If `Interval is "mo" or "yr"` → insert Pay `subscriptions` `active`. Checkout create **hard-codes** `Interval = "one_off"`, so this branch is dead on the current create path. Catalog products have an interval that is **not** copied onto checkout. This is Pay’s own subscription row, **not** a Stripe Billing subscription. Legal under `NP-XX-012` **if** the clock is Pay’s. Today it never fires from Stripe hosted pay.
9. Journal: cash D + revenue C, amount = checkout.Amount. **No fee line. No tax line.** Balanced. `NP-MON-002` not yet real for Stripe MDR.
10. `RCPT-{MalaysiaYear}-{n:00000}`, title **"Official Receipt"** (not Tax Invoice). Audit `checkout.paid`. Same txn as the journal.

This is the opposite of Hub’s “Payments is not a fulfillment engine.” Correct product. Incomplete money (fee/tax), incomplete txn coupling (webhook insert vs fulfill), incomplete org check.

---

## 10. Tests that actually exist on new Pay

`WebhookTests.cs` (entire file): three tests.

1. `Missing_webhook_secret_is_503_when_rail_configured` — empty `Pay:StripeWebhookSecret` → 503 after seeding a stripe `sk_`.
2. `Invalid_signature_is_400` — junk `Stripe-Signature` → 400.
3. `Completed_session_writes_receipt_and_replay_is_noop` — signed `checkout.session.completed` with `mode=payment`, `amount_total=1000`, `client_reference_id` = checkout id, metadata `checkout_id` + `org_id`. First POST 200, one `RCPT-`, balanced journal. Second POST 200 body contains `duplicate`, still one document.

`PublicPayTests.Empty_webhook_is_400` — POST empty JSON body to `/v1/webhooks/stripe/t1` → 400. This is `NP-GW-005`, **not** in `WebhookTests`.

Hermetic: `PayApiFactory` in-memory EF, `UseSetting("Pay:StripeWebhookSecret", …)`, HMAC helper matching Stripe `t=…,v1=…`. No network to Stripe. IsolationTests still ban MediatR.

**Claimed by 013 checklists, not present in tests:**

| Checklist | Claim | Live |
|-----------|-------|------|
| G22.3 / G25.2 | Fixture payload for setup-intent **or** amount 0: fulfill **not** called | **No test** greps `setup_or_zero`, `setup_intent`, or `mode=setup` under `apps/lazuar-pay/tests` |
| G21.2 | Org A `event_id` does not collide with org B | **No test** |
| G21.1 | Unique-violation concurrent race → 200 duplicate | **No try/catch 23505** in `WebhookEndpoints` |
| G19.1 | Decrypt **that org’s** webhook secret | Code reads **process** `Pay:StripeWebhookSecret` |

G22 code path exists (`Mode == "setup" \|\| AmountTotal is null or 0` → `{ ignored = "setup_or_zero" }`) plus fulfillment `Amount <= 0` return. The fail lock is **implemented, under-tested**. 014 does not flip 011; it records the honesty gap.

---

## 11. Stripe.net versions (confirm)

| Tree | Package | Version |
|------|---------|---------|
| New Pay `Lazuar.Pay.csproj` | `PackageReference Include="Stripe.net" Version="48.0.0"` | **48.0.0** (pinned on the host csproj; no central props) |
| Hub `Directory.Packages.props` | `PackageVersion Include="Stripe.net" Version="48.0.1"` | **48.0.1** |
| Hub `Modules.Payments.Infrastructure.csproj` | `PackageReference Include="Stripe.net" />` (versionless, CPM) | resolves 48.0.1 |

Task said “New Pay currently has Stripe.net 48.0.0 in csproj. Hub likely does too — confirm.” **Hub is 48.0.1, not 48.0.0.** Patch skew. New Pay `ConstructEvent(..., throwOnApiVersionMismatch: false)` is the pragmatic companion: tests sign `api_version: "2024-06-20"`; Stripe.net 48’s default is the basil line Hub tests use (`2025-03-31.basil` in `StripeGatewayAdapterTests`). Hub `ConstructEvent` uses the library default (`throwOnApiVersionMismatch: true`). Steal: either pin the Dashboard endpoint to the SDK’s API version **or** keep `false` and accept thinner objects. Do not silently parse Billing objects as cash either way.

008/007 said Hub was 48.0.1 and (in 007/04) “No `payment_method_types` allow-list.” **Live Hub has the card allow-list.** 007/04 wrap table is stale on that row. Live adapter wins.

---

## 12. `Pay:StripeWebhookSecret` — platform secret vs per-org `whsec_` (honesty)

**This is the production-BYOK hole.**

Hub:

- `TenantPaymentConfiguration` has **both** `ApiKey` (`sk_…`) and `WebhookSecret` (`whsec_…`), encrypted per `(tenant, gateway)`.
- Webhook URL is per tenant: `/webhooks/payments/STRIPE/{tenantId}`.
- Handler: “Webhook secret not configured **for this tenant gateway**.”
- Ops UI (007/04): Stripe fields = Secret Key **and** Webhook Signing Secret.

New Pay:

- `gateway_credentials` columns: `OrgId`, `Provider`, `Ciphertext`, `Last4`, `UpdatedAt`. **No webhook secret.**
- Verify uses `IConfiguration["Pay:StripeWebhookSecret"]` — one value for the whole process.
- `.env.example` says the quiet part: “Checkout secret key is BYOK per org.”
- Missing that config → **503** (ops bug; Stripe will retry). Missing org `sk_` → **400** `rail not configured`.

Stripe signs each **endpoint** with a unique `whsec_`. BYOK means Ada’s Stripe account and Ben’s Stripe account. Ada’s Dashboard endpoint `https://pay…/v1/webhooks/stripe/{adaOrg}` has `whsec_ada`. Ben has `whsec_ben`. A single `Pay:StripeWebhookSecret` can verify **one** of those endpoints, not both.

Ways this is not “just an env oversight”:

1. **Stripe Connect platform webhook** with `Stripe-Account` + application fees — **refused** (`NP` / 007 `LP-XX-007` / 013 standing law). Would also invert BYOK.
2. **One Lazuar Stripe account, merchants as connected accounts** — same refuse.
3. **Per-org `whsec_` in `gateway_credentials` (or a sibling column), pasted on PUT or created via Stripe Webhook Endpoints API using the org `sk_`** — this is the Hub judgment, minus MediatR. CHIP registrar is the analogue of (create via API). Hub Stripe was paste-both.
4. **Dogfood one org** — platform secret is enough. Bar B can run. Production N merchants cannot.

Consequence of a shared secret even in dogfood: anyone who holds `Pay:StripeWebhookSecret` can forge `checkout.session.completed` for **every** org that has a stripe key pasted. URL `{orgId}` is not cryptographic. Hub’s per-tenant `whsec_` at least requires that org’s secret.

G19.1 is **ticked** (“Decrypt **that** org’s webhook secret”). Live code does not. 014 records the lie; it does not untick the 013 checklist.

---

## 13. Amount: cents rounding, Hub vs Pay

`GatewayCommon.ToMinorUnits` (Hub, the living money policy):

```68:85:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs
    private static readonly HashSet<string> ZeroDecimalCurrencies = ... JPY, KRW, VND, ...
    public static long ToMinorUnits(decimal amount, string? currency = "MYR", int quantity = 1)
    {
        var qty = quantity < 1 ? 1 : quantity;
        var factor = IsZeroDecimalCurrency(currency) ? 1m : 100m;
        return (long)Math.Round(amount * qty * factor, 0, MidpointRounding.AwayFromZero);
    }
```

Hub Stripe **generate** uses `UnitAmountDecimal = GatewayCommon.ToMinorUnits(amount, currency)` — JPY 10000 → 10000.

Hub Stripe **parse** uses `/ 100m` on `AmountTotal`, `AmountReceived`, dispute, refund — JPY would be wrong on the way back. Hub generate and parse are not dual. Steal generate. Fix parse when the currency is not two-decimal. Bar B catalog **refuses non-MYR**, so dogfood is safe.

New Pay generate:

```csharp
var cents = (long)Math.Round(checkout.Amount * 100m, MidpointRounding.AwayFromZero);
UnitAmount = cents,  // long, not UnitAmountDecimal
```

Always ×100. AwayFromZero — same midpoint policy as Hub for MYR. Differences:

| | Hub generate | New Pay generate |
|--|--------------|------------------|
| Factor | 1 or 100 by ISO | always 100 |
| Quantity | in `ToMinorUnits` **and** line `Quantity` (risk of double-multiply if both used; live `CreateCheckoutSessionOptions` passes quantity to line item and `ToMinorUnits(amount, currency)` **without** the quantity argument — quantity is **not** double-counted) | line `Quantity = 1` only |
| Type | `UnitAmountDecimal` | `UnitAmount` (long) |
| Product name | caller / `"Lazuar Payment"` | always `"Pay"` |
| Currency source | argument | `checkout.Currency.ToLowerInvariant()` |

New Pay **fulfill** books `checkout.Amount`, not Stripe `AmountTotal`. If Stripe and Pay diverge (partial capture, Dashboard coupon, zero-decimal), the journal follows Pay. That is safer against a forged `amount_total` **if** the signature is org-scoped. With a platform `whsec_`, an attacker still cannot change Pay’s checkout amount without also changing the checkout row; they can mark a **different** checkout paid. Org check is the missing piece.

Webhook ignore: `AmountTotal is null or 0`. Stripe setup sessions often have null/0 total. Good. A `mode=payment` 3DS session with `amount_total=0` would also ignore — correct under `NP-GW-008`.

---

## 14. Metadata: `checkout_id` / `org_id` vs Hub tenant metadata

Hub generate: caller (Commerce / M2M) supplies a dictionary. Adapter `ApplyPayingTenantMetadata`:

- empty `tenant_id` → stamp adapter tenant
- different `tenant_id` → keep paying tenant, stamp `platform_tenant_id`

Copied onto **Session and PaymentIntent**. Webhook inbound mismatch vs URL tenant is dropped (except Hub SaaS platform checkout). Merge from `IntegrationCheckoutSession` forces `checkout_id`.

New Pay generate stamps **only**:

```csharp
Metadata = { ["checkout_id"] = checkout.Id, ["org_id"] = checkout.OrgId }
ClientReferenceId = checkout.Id
```

No `tenant_id`. No `PaymentIntentData.Metadata`. No `hub_workspace_id`. No `type=commerce_subscription`. No `subscription_id`.

Webhook lookup:

```csharp
var checkoutId = session.ClientReferenceId ?? session.Metadata?["checkout_id"];
```

Does **not** read `org_id` from metadata. Does **not** compare metadata `org_id` to URL `{orgId}`. Does **not** compare `checkout.OrgId` to URL `{orgId}` inside `FulfillPaidAsync`.

[013/06 §5.2 step 11](../013-prods/06-money-rails.md): “Inbound metadata `org_id` / `tenant_id` mismatch vs URL `orgId` → **200** ignore + log (do not 400). New Pay has no platform/system org; mismatch is always ignore.”

**Not implemented.** Cross-org fulfill: a signed event (platform secret) with Ada’s `checkout_id` posted to Ben’s URL still pays Ada’s checkout, and the idempotency row is stored under Ben. Replay to Ada’s URL is a **second** `(ada, stripe, evt_…)` tuple and would fulfill again if the first write did not flip status — actually status **would** be `paid` on Ada’s row, so the second would no-op at `Status != open`. The first hop already wrote Ada’s money from Ben’s URL. Steal Hub’s mismatch ignore **and** key the fulfill by `(orgId, checkoutId)`.

Because `PaymentIntentData` is unset, a future `payment_intent.succeeded` handler would **not** see `checkout_id` unless it expands the Session or stores `cs_` → checkout. Today Pay **ignores** `payment_intent.succeeded` (see dropped list). If you add it, copy metadata onto the PI first, or key only through Checkout Session.

---

## 15. What new Pay already does (Stripe)

Keep this list boring and true. This is the steal that landed.

1. **BYOK `sk_` per org**, AES-GCM at rest, last-4 on GET, writer-only PUT, member-only GET. Not Lazuar’s `sk_`. Not Vite.
2. **Hosted Checkout `mode=payment` only** on the generate path. No `mode=subscription`. No `Stripe.Subscription`. Grep under `apps/lazuar-pay/src` for `customer.subscription` / `Mode = "subscription"`: **zero**.
3. **Buyer hop** `POST /v1/pay/{token}/start` creates the Stripe Session with the **org** key, returns `redirect_url`. Buyer is not a Zitadel human.
4. **Plane B door** `POST /v1/webhooks/stripe/{orgId}` on 8081 `/v1`. Unknown provider 400. Empty body 400. Bad signature 400 (not Hub’s 500). Raw body, no model bind before verify.
5. **`EventUtility.ValidateSignature` + `ConstructEvent`** — Stripe library, not a homemade HMAC (the test helper is only for fixtures).
6. **`checkout.session.completed` + `mode=payment` + non-zero `AmountTotal`** → in-process `FulfillPaidAsync`. Same request. No MediatR. No `GatewayPaymentCompletedIntegrationEvent`.
7. **`mode=setup` or zero/null `AmountTotal`** → 200 `{ ignored: "setup_or_zero" }`. Does not mint `RCPT-`. Complements fulfillment `Amount <= 0` return. This is `NP-GW-008` in the host, even without a dedicated test.
8. **Idempotency tuple** `(org_id, provider, event_id)` on `psp_webhook_events`. Replay 200 `duplicate`, one receipt in the happy-path test. Event id is Stripe `evt_…` (ConstructEvent). Never invents a Guid.
9. **Default success URL** sends the buyer to checkout Vite `?status=verifying`, not “Order Complete” from the query string (frontend paper owns the poll). Backend does not treat landing as paid.
10. **Stripe.net** present on the focused host (48.0.0). Isolation still holds (no project reference to `lazuar-api`).
11. **Connect application fee:** not in `StripeHosted`. Nothing to grep. Do not add it.
12. **One rail advertised** (`capability = "hosted_link"`). Not a factory of five.

---

## 16. What new Pay dropped (relative to Hub Stripe HTTP)

Dropped on purpose (refuse / later) vs dropped by accident (gap). Mixed here as a dump; severity is §19.

**Generate drops**

| Hub | New Pay |
|-----|---------|
| `PaymentMethodTypes = ["card"]` (replaces Dashboard dynamic PMs) | **Unset** — Dashboard defaults apply. Delayed PMs can fire `checkout.session.completed` with `payment_status=unpaid` |
| `CustomerEmail` | **Unset** (payer email is stored on checkout, not sent to Stripe) |
| Real product name | Hard-coded `"Pay"` |
| `PaymentIntentData.Metadata` copy | **Absent** |
| `ClientReferenceId` | **Present** (Pay session id) — keep |
| `ApplySetupFutureUsage` / `$0` setup mode | Generate **never** setup. Recurring interval on catalog is not on checkout. First dogfood is one-off. Correct for Bar B; vault is later |
| `ApplyPayingTenantMetadata` / `platform_tenant_id` | Absent — correct (no Hub SaaS platform org) |
| `GatewayCommon.ToMinorUnits` zero-decimal | Always ×100 |
| `KEY_MODE_MISMATCH` (`sk_test_` vs live) | **Absent** — a live key in a test org will charge live money |
| Stripe `Idempotency-Key` on Session create | **Absent** — double-click `start` can mint two `cs_` URLs; checkout row keeps the last `PspRedirectUrl`; **both sessions are payable** |
| Persist `session.Id` as provider session id | Only the hosted URL is saved (`PspRedirectUrl`). Charge `ProviderRef` is set later from the webhook’s `session.Id` |
| Quantity | Always 1 |
| `StripeException` message returned to cashier | Mapped to generic 503 |

**Parse drops**

| Hub | New Pay |
|-----|---------|
| Per-tenant `whsec_` | Process `Pay:StripeWebhookSecret` |
| `payment_intent.succeeded` as cash (dual-event + business key) | **Ignored** (200 `{ ok: true }`, event still inserted) |
| `payment_intent.payment_failed` | Ignored |
| `setup_intent.succeeded` as `PAYMENT_COMPLETED` 0 | Ignored as unknown type (200 ok) — **better than Hub’s event name**, but does not persist PM |
| Disputes | Ignored |
| Refunds (`refund.updated` succeeded) | Ignored |
| Expand PI for fee / FX / PM | No second Stripe HTTP on webhook (good for timeout; blind on `NP-MON-002`) |
| `payment_status == paid` | **Not checked** |
| Currency fail-closed | Not read |
| Metadata org mismatch | **Not checked** |
| Dual-event business key `paid:{pi}` | Only `evt_…`. Safe **because** PI events are ignored. Becomes a double-fulfill bug the day someone “just adds” `payment_intent.succeeded` without a business key **and** without the setup/zero fence on the PI |
| Unique 23505 race → 200 | Uncaught — concurrent Stripe retries can 500 |
| Insert + fulfill **one DB transaction** | **Two** transactions: `SaveChanges` on `psp_webhook_events`, then `Fulfillment` begins another |
| Outbox requeue if fulfill fails after ACK | No status `received` vs `applied`. Fail after insert + Stripe retry = **lost money** (`duplicate: true`, checkout still `open`) |
| Unusable-after-verify 400 (no currency) | N/A (doesn’t read currency) |
| Hub signature-fail **500** | Pay **400** — Pay is the one to steal |

**Port drops (do not bring back as a cathedral)**

- `IPaymentGatewayAdapter` / factory / MediatR / keyed event bus / `PaymentsDbContext` / `IntegrationCheckoutSessions`
- `GenerateCustomerPortalAsync`
- `ChargeOffSessionAsync` (V1)
- `ExecuteOffSessionChargeIntegrationEventHandler`
- Platform checkout `SystemOrganizationId`
- `DecryptOrPlaintext`
- BILLPLZ last-resort cashier

---

## 17. Steal-list (HTTP judgment only)

Copy the **decision**. Not the class.

### 17.1 HTTP calls to Stripe (generate)

1. `new StripeClient(orgSk)` per call — merchant key, never Lazuar’s, never `Stripe-Account`.
2. `SessionService.CreateAsync` **only** for hop-2. No Elements, no ephemeral keys, no PaymentIntent-create for the first dogfood charge (off-session is later).
3. `Mode = "payment"` for amount > 0. **Never** `mode=subscription`.
4. `Mode = "setup"` **only** when amount is 0 **and** you are vaulting — and the webhook must not fulfill it. Do not add this to prove Bar B.
5. `PaymentMethodTypes = ["card"]`. Do not list `apple_pay` / `google_pay` / `fpx`. Wallets ride on `card`. This wrap is honesty: “cards (+ Apple/Google Pay when Stripe shows them).” Leaving it unset is how delayed methods sneak in.
6. Copy metadata onto **both** Session and `PaymentIntentData`. Keys: `checkout_id`, `org_id`. Not `tenant_id` unless you mean the same thing — pick `org_id` and stick to it. No `platform_tenant_id` (no system org).
7. `ClientReferenceId = checkout.Id` (already stolen).
8. `CustomerEmail` when the buyer typed one on `/v1/pay/{token}/start` (Pay already stores it).
9. Product `Name` from catalog/checkout, not `"Pay"`.
10. Minor units: Hub `ToMinorUnits` (away-from-zero, zero-decimal table). MYR ×100 is enough for Bar B if catalog stays MYR-only.
11. Success/cancel URLs from the checkout row. Default localhost:5179 is a **laptop** fallback, not production.
12. Optional: Stripe `Idempotency-Key = "lazuar-checkout:" + checkout.Id` on Session create so double-start does not mint two payable `cs_`.
13. Optional later: `SetupFutureUsage = "off_session"` + `CustomerCreation = "always"` on **payment** mode when interval is `mo`/`yr` **and** you persist `cus_` / `pm_`. Not a `RCPT-`.
14. **Never** `ApplicationFeeAmount`, `TransferData`, Connect onboarding, `application_fee_amount`.
15. **Never** `automatic_tax` as SST. Stripe Tax is not MyInvois and not MY SST.

### 17.2 Signature / verify

1. Read **raw** body as the PSP sent it. Empty/whitespace → **400**.
2. Require `Stripe-Signature` (case-insensitive). Missing/bad → **400**, never 500, never 200, never 401.
3. `EventUtility.ConstructEvent` (and/or `ValidateSignature`) with **that org’s** `whsec_`. Do not roll your own HMAC except in tests.
4. Decrypt fail on wrap key → 500 once (our bug). Do not `DecryptOrPlaintext`.
5. Org has no stripe row → 400 `rail not configured` (already). Soft-disable later: still verify and fulfill (Hub comment: credentials retained). Disable means no **new** sessions.
6. Unknown `provider` → 400 (already).
7. Unknown event type after verify → **200** `{ ignored: true }`. Forward compatible. This is how Billing stays non-SoT.

### 17.3 Event types to honor

**Cash (fulfill paid), `mode=payment` only, amount > 0, `payment_status` paid when the object is a Session:**

- `checkout.session.completed` where `session.Mode == "payment"` and `AmountTotal > 0` (already) **and** `PaymentStatus == "paid"` (not yet). Lookup checkout by `ClientReferenceId` / `metadata.checkout_id`. Book **Pay’s** checkout amount. Persist `ProviderRef` as **PaymentIntent id** (`session.PaymentIntentId`), not only `cs_`, so refunds later have a handle. You may also store `cs_` separately.

**Cash fallback if Session webhooks are lost (optional, needs business key):**

- `payment_intent.succeeded` **only if** metadata/org lookup finds an **open payment-mode checkout** and amount > 0. Collapse with `checkout.session.completed` via `(org, stripe, "paid:"+pi_id)` unique, **in the same txn as fulfill**. Do not add this without the collapse; Hub’s dual-event test exists because Stripe sends both.

**Not paid, persist vault if you have columns (later):**

- `checkout.session.completed` with `Mode == "setup"` → 200 `vaulted` / `setup_or_zero` (already ignore). Extract `customer` + `payment_method` (Hub `ReadSetupSessionVaultIds` / SetupIntent GET). **Do not** call `FulfillPaidAsync`.
- `setup_intent.succeeded` → same. Steal Hub’s PM extract. **Do not** steal `EventType = "PAYMENT_COMPLETED"`.

**Not paid, later paper 07:**

- `payment_intent.payment_failed` → decline code. Do not invent PAST_DUE on a healthy one-off.
- `charge.dispute.created` / `closed` / `updated` (Hub mapping).
- `refund.updated` / `charge.refunded` with refund `status=succeeded` → reverse journal once. `EventId` for idempotency on refunds should be `re_…` or `evt_…` **without** PI-level collapse.

### 17.4 Event types to ignore (200, no fulfill)

- `customer.subscription.created` / `updated` / `deleted`
- `invoice.paid` / `invoice.payment_succeeded` / `invoice.finalized`
- `customer.updated` / `customer.created`
- `payment_intent.created` / `processing` / `requires_action`
- `charge.succeeded` (duplicate of PI; Hub passthrough-drops)
- `billing_portal.*`
- Radar / review / payout / capability / account
- Anything else unknown

If a merchant enables Stripe Billing in their Dashboard independently, those events may hit the same endpoint. **Ignore them.** Pay’s clock is Pay. `NP-XX-012`.

### 17.5 Handler shape to steal (not types)

From [013/06 §5.2](../013-prods/06-money-rails.md), which new Pay partially implemented:

1. Allow-list provider.
2. Raw body; empty 400.
3. Load org rail.
4. Verify with **org** secret.
5. Parse to `{ kind: paid | failed | ignored | vaulted, event_id, … }`.
6. Unique insert `(org_id, provider, event_id)` **and** fulfill **in one transaction**. Duplicate → 200, no second journal.
7. Metadata org mismatch → 200 ignore.
8. No MediatR, no outbox, no wait for One.

Hub bits to steal as **ideas** only: 23505 swallow; dual-event business key; fail-closed currency; card allow-list; Connect-fee tests; `IsOffSessionSucceeded` = succeeded only.

---

## 18. Refuse-list (must never be copied)

These are Hub (or Stripe-product) shapes that would recreate the cathedral or break standing law.

1. **`Modules/Payments` as a project.** `AddPaymentsModule`, `PaymentsDbContext`, `IPaymentGatewayAdapter`, `PaymentGatewayFactory` of five. IsolationTests are the tripwire.
2. **MediatR `ProcessGatewayWebhookCommand`.** New host already does not.
3. **`GatewayPaymentCompletedIntegrationEvent` + Payments outbox + Commerce inbox.** Fulfillment is the webhook handler. Reintroducing this event is a fail lock for this slice.
4. **`HandleExistingLogAsync` / Dead outbox requeue.** That exists because Hub ACKs before Commerce. Same-txn fulfill deletes the need. If you ACK before fulfill, you will reinvent it.
5. **Stripe Billing as SoT:** `customer.subscription.updated`, `invoice.paid`, Checkout `mode=subscription`, `Stripe.Subscription` / `Price` / `Coupon` objects, Billing Portal as Pay’s buyer portal (`GenerateCustomerPortalAsync`). `NP-XX-012`. G23.
6. **`setup_intent.succeeded` or setup-mode `checkout.session.completed` mapped as `PAYMENT_COMPLETED`.** Hub tests **lock the lie** (`ParseWebhook_SetupIntentSucceeded_ExtractsPaymentMethod` expects `EventType == "PAYMENT_COMPLETED"` and `AmountPaid == 0`). Steal extract; refuse the name. `NP-GW-008`.
7. **Stripe Connect `application_fee_amount` / `TransferData` / `Stripe-Account` / connected accounts as Lazuar’s tenant model.** Hub tests already ban the strings in adapter source. 007 `LP-XX-007`. 013 standing law 1. Aura “Stripe Connect” marketing is retired.
8. **Platform / system org checkout** (`ApplyPayingTenantMetadata` for Hub SaaS fee, `PlatformCheckoutTypes.SystemOrganizationId`). New Pay is not billing Hub.
9. **BILLPLZ last-resort** when Stripe keys are missing (`CheckoutSessionCashier` `return "BILLPLZ"`).
10. **`DecryptOrPlaintext`.** Wrong wrap key must not send ciphertext to Stripe as Bearer.
11. **`Jwt:Secret` as KMS** (Hub `AesSecretVault` fallback). New Pay `SecretBox` has a **dev** SHA256 fallback — production must set `Pay:WrapKey`; do not add a JWT fallback.
12. **Vite `sk_live_` / `whsec_`.** Merchant 5178 / checkout 5179 hold origins only.
13. **Homemade FPX e-mandate** / Stripe FPX as silent debit. Card wrap is enough. `SupportsEmandate` stays false.
14. **Elements / Payment Element / ephemeral keys / publishable key in Pay.** Wrap is hosted Checkout. PCI SAQ-A stays the PSP’s page.
15. **Five adapters on day one** because the factory had them.
16. **Ticking `NP-GW-002` because CHIP showed a card form.** This paper is Stripe. CHIP is 06.
17. **Off-session `ChargeOffSessionAsync` as the first `RCPT-`.** V1. Adapter `true` is not paid; wait for PI succeeded **or** book pending without `RCPT-`.
18. **Hub signature-fail 500.** Pay already 400s. Keep 400.
19. **Invented event ids** (Guid fallback).
20. **Wait for One** to ACK money or grant buyer access.
21. **Fee = 0 meaning “Stripe charged 0 MDR.”** Stamp unknown.
22. **ngrok as staging/prod origin.**
23. **Customer Portal “Copy Portal Link” as update-payment.**

---

## 19. Gap-list (production BYOK Stripe), with severity

Severity: **blocker** = cannot take live multi-merchant money honestly; **high** = can lose or double money, or book non-cash as cash, under realistic Stripe Dashboard settings; **medium** = dogfood works, production will feel it; **low** = polish / later paper.

### Blocker

| ID | Gap | Why it blocks production BYOK |
|----|-----|-------------------------------|
| B1 | **Webhook secret is platform-scoped** (`Pay:StripeWebhookSecret`), not per `(org_id, stripe)` | N merchants ⇒ N Stripe accounts ⇒ N `whsec_`. One env var verifies one endpoint. Hub stored per-tenant `WebhookSecret`. G19.1 claimed org decrypt; code does not. Shared secret also lets one leak forge **all** orgs. |
| B2 | **Idempotency insert and fulfill are two transactions**; insert happens **first** | [013/06](../013-prods/06-money-rails.md) anti-goal 11: “ACK 200 before idempotency insert + fulfill txn.” Live: insert, `SaveChanges`, then `FulfillPaidAsync` starts a **new** txn. If fulfill throws (bug, SST null, DB blip) after insert, Stripe retries, Pay returns `{ duplicate: true }`, checkout stays `open`, **no `RCPT-`**. Hub needed outbox requeue for this class of bug. New Pay refused outbox and then recreated the hole. |

### High

| ID | Gap | Why |
|----|-----|-----|
| H1 | **No `PaymentMethodTypes = ['card']`** | Dashboard dynamic PMs apply. Bank / FPX / vouchers can complete a Session with `payment_status=unpaid`. Pay fulfills on `checkout.session.completed` without reading `PaymentStatus`. Hub forced card **because** of this. |
| H2 | **No `payment_status == paid` check** | Even with cards, async methods exist. Steal Hub’s wrap **and** check paid. |
| H3 | **No org mismatch guard** | Fulfill looks up checkout by id globally. Platform `whsec_` + known checkout id pays any org. 013 said 200-ignore on mismatch. |
| H4 | **Double-start creates two Stripe Sessions** | No Stripe idempotency key; `PspRedirectUrl` overwritten. Buyer (or two devices) can pay both. Pay fulfills the first webhook; the second checkout id is the same so status `paid` no-ops — **but Stripe still captured twice on the merchant account.** Merchant support nightmare, not a double `RCPT-`. |
| H5 | **No `sk_test_` / `sk_live_` vs environment guard** | Hub `EnsureKeyModeMatchesGateway`. A pasted live key on a laptop dogfood charges live cards. |
| H6 | **Concurrent webhook 23505 uncaught** | Find-then-insert. Two Stripe deliveries → unique violation → **500** → Stripe keeps retrying. Hub swallowed 23505 as duplicate. |
| H7 | **`ProviderRef = session.Id` (`cs_…`)** | Hub `GatewayTransactionId` is PaymentIntent id. Refunds, disputes, dual-event keys all want `pi_`. Storing only `cs_` makes later `IssueRefundAsync` judgment unusable without a retrieve. |
| H8 | **G22/G25 setup-not-paid test is missing** while checklists are ticked | The code path exists; CI does not prove the fail lock. A future “helpful” map of `setup_intent.succeeded` → fulfill would go green. |

### Medium

| ID | Gap | Why |
|----|-----|-----|
| M1 | **`payment_intent.succeeded` ignored** | Fine while only Session is honored. Stripe Dashboard copy in 013/06 §8.2 told Ada to subscribe `checkout.session.completed`, `payment_intent.succeeded`, `payment_intent.payment_failed`. The latter two are no-ops except they **consume** the `(org, stripe, evt_…)` unique row. If fulfill failed on the Session event, the PI event cannot repair it (different `evt_`, but fulfill is keyed by checkout status — actually PI is ignored so it would insert and 200 ok without fulfill). Document the subscribed set: Session is the cash event. If you add PI later, add business key **first**. |
| M2 | **No PaymentIntent metadata copy** | Blocks a safe PI fallback. |
| M3 | **Always ×100 minor units** | Catalog is MYR-only today. Zero-decimal later would overcharge ×100. Steal `GatewayCommon.ToMinorUnits`. |
| M4 | **No fee extract; journal cash=revenue=checkout.Amount** | `NP-MON-002`. Unknown MDR booked as if fee were 0. Do not claim net. Expand `latest_charge.balance_transaction` is Hub HTTP to steal **when** you book fees; until then stamp unknown, do not print “Stripe fee RM0.00”. |
| M5 | **No customer email / product name on Session** | Worse Stripe receipts and Dashboard search. Payer email already collected. |
| M6 | **Success/cancel default `http://localhost:5179`** | Production checkout without `SuccessUrl` set lands on a laptop origin. Fail closed or require public HTTPS. |
| M7 | **No persist of `cs_` at generate time** | Cannot expire/reconcile abandoned Sessions. Webhook is the only attach. |
| M8 | **Stripe.net 48.0.0 vs Hub 48.0.1** + `throwOnApiVersionMismatch: false` | Thin events / missing fields. Pin version and Dashboard API version deliberately. |
| M9 | **`SecretBox` dev wrap-key fallback** | Production without `Pay:WrapKey` encrypts with a repo-known string. Host paper, but it wraps the `sk_`. |
| M10 | **PUT gateway does not accept `whsec_`** | Even if you add a column, the merchant UI/API cannot paste it yet. Hub ops had two fields. |
| M11 | **Catalog interval not copied onto checkout** | `Fulfillment` subscription branch is dead. Recurring Stripe products still look like one-offs. Honest for Bar B; say so in merchant copy. |
| M12 | **No `payment_intent.payment_failed` handling** | One-off dogfood can ignore. Recurring/dunning cannot. |
| M13 | **G21 org-A vs org-B event_id test missing** | Schema is composite PK so it should work; unproven. |

### Low / later (not Bar B)

| ID | Gap |
|----|-----|
| L1 | Off-session charge + vault columns (`cus_`, `pm_`) — V1 `NP-FUL-004`, wrap-rails only if PM exists |
| L2 | Refunds API + refund webhooks |
| L3 | Disputes |
| L4 | Customer Portal (refuse as SoT; magic-link is `NP-BUY-004`) |
| L5 | Apple Pay domain verification (wrap: Stripe-hosted page) |
| L6 | Tax line / SST exclusive math (`NP-MON-003` V1) |
| L7 | Decline codes on the merchant UI |
| L8 | Stripe idempotency on refunds (`lazuar-refund:…`) when refunds exist |
| L9 | Public DNS fallback — Hub Billplz-only; not Stripe |
| L10 | CHIP registrar localhost rewrite — not Stripe |

---

## 20. 008 historical Stripe bits vs live Hub vs new Pay

[008/02 §3](../008-evals/02-payments-adapters-rails.md) is **historical**. Live `StripeGatewayAdapter.cs` on this SHA is authority.

| 008 claim | Live Hub `ee2db8e5` | New Pay |
|-----------|---------------------|---------|
| Mode is always `"payment"` (line 433 in that paper’s numbering) | **False.** `$0` + `setupFutureUsage` → `mode=setup` | Generate is payment-only. Good. |
| No refund webhook map | **False.** `TryMapRefundCompleted` exists; tests for `refund.updated` succeeded | No refund map (later) |
| Off-session success = `succeeded` **or** `processing` | **False.** `IsOffSessionSucceeded` is succeeded-only; tests lock it | N/A |
| Stripe multiplies by 100 **in the adapter itself** | **False on generate.** Uses `GatewayCommon.ToMinorUnits`. Parse still `/100` | Always ×100 on generate |
| No `payment_method_types` allow-list (007/04 same) | **False.** Card-only wrap + tests forbidding `apple_pay` / `google_pay` / `fpx` | **True — Pay dropped the wrap** |
| Refund `IssueRefundAsync` treats pending as success | **False.** `IsRefundSucceeded` is succeeded-only | N/A |
| EventId = Stripe `evt_…`; dual-event business key | **True** | EventId = `evt_…`; **no** dual-event key (PI ignored) |
| Connect application fee absent | **True**, tested | **True**, untested (nothing to set) |
| Billing Portal implemented | **True** | **Absent** (correct refuse) |
| Empty body 500 | **Fixed in Hub** (400). 008 was pre-fix | 400 |

Do not port 008’s Stripe section as a to-do list. Port **this** file’s steal-list against live Hub + live Pay.

---

## 21. Side-by-side: generate options

Hub payment session (judgment):

- mode `payment`
- card-only PM types
- metadata on session **and** PI
- `tenant_id` / optional `platform_tenant_id`
- customer email
- product name
- `UnitAmountDecimal` via `ToMinorUnits`
- success/cancel from caller (Commerce public HTTPS)
- optional `setup_future_usage=off_session` + `customer_creation=always`

Pay payment session (living):

- mode `payment` ✓
- **no** PM types
- metadata on session only: `checkout_id`, `org_id` ✓ keys, ✗ PI copy
- `ClientReferenceId` ✓
- **no** customer email
- product name `"Pay"`
- `UnitAmount = round(amount*100)`
- success/cancel from checkout **or localhost:5179**
- no setup_future_usage

Hub setup session (do not book as cash):

- mode `setup`
- SetupIntent metadata
- customer_creation always
- card-only

Pay: **does not generate setup.** Webhook still fences it if a merchant-created setup Session somehow posts (it will not, unless they reuse the endpoint for Dashboard-created sessions). Good defense.

---

## 22. Side-by-side: webhook pipeline

| Step | Hub | New Pay |
|------|-----|---------|
| Path | `/webhooks/payments/{gateway}/{tenantId}` + `/api/v1` in prod | `/v1/webhooks/{provider}/{orgId}` ✓ |
| Auth | Stripe-Signature + **tenant** `whsec_` | Stripe-Signature + **process** `whsec_` ✗ BYOK |
| Empty body | 400 | 400 ✓ |
| Bad sig | throw → **500** | **400** ✓ (steal Pay, not Hub) |
| Missing org secret | throw 500-class | 400 if no `sk_`; **503** if no platform `whsec_` |
| Event id | `evt_…` | `evt_…` ✓ |
| Unique | `(org, provider, event_id)` + business key | `(org, provider, event_id)` only |
| Cash events | Session completed **or** PI succeeded, including setup-as-completed | Session completed, setup/zero ignored ✓ name, ✗ PI, ✗ payment_status |
| Billing events | passthrough drop | implicit ignore ✓ |
| Fulfill | outbox `GatewayPaymentCompletedIntegrationEvent` | in-process `FulfillPaidAsync` ✓ process, ✗ same txn |
| Retry | outbox requeue | `{ duplicate: true }` only |

---

## 23. Production-ready Stripe on new Pay: **no**

**No** for production BYOK Stripe.

Not because the host still has MediatR (it does not). Not because it books `setup_intent` as paid (it does not). Not because it takes an application fee (it does not). Those are the Hub sins new Pay actually avoided.

It is not production-ready because:

1. **Verify is not BYOK.** `Pay:StripeWebhookSecret` is a platform secret. Multi-merchant Stripe is N signing secrets. Standing law “merchant’s Stripe secret, not Lazuar’s” was implemented for `sk_` and skipped for `whsec_`. One-org dogfood can hide this. Production cannot.
2. **Money can be lost on a successful Stripe capture** if fulfill throws after the idempotency insert. Same-handler at the **process** level is not same-handler at the **transaction** level. 011/03 fail lock is “webhook retry double-journals.” The dual is “webhook retry **never** journals.” Pay currently has the second hole.
3. **Dashboard dynamic PMs + no `payment_status` check** can mark unpaid Sessions paid. Hub’s card wrap exists to prevent that class of lie. Pay dropped it.
4. **Two payable Checkout Sessions per Pay checkout** on double-start, with only one `RCPT-`. The merchant’s Stripe balance will not match Pay.
5. **No live/test key mode guard.**
6. **Checklist honesty:** G19/G21/G22/G25 ticked items that the tests and the `whsec_` column do not support. 014 does not flip 011; it also does not treat 013 G-rows as proof.

**Narrow dogfood yes**, if all of these are true at once:

- One org.
- Stripe **test** mode.
- Operator pastes `sk_test_…` via `PUT /v1/orgs/{orgId}/gateway`.
- Operator creates **one** Dashboard endpoint `https://<tunnel>/v1/webhooks/stripe/{thatOrgId}` subscribed to `checkout.session.completed` (and can subscribe others; they will be ignored).
- Operator sets `Pay__StripeWebhookSecret` to **that** endpoint’s `whsec_`.
- Operator sets `Pay__WrapKey` (or accepts the dev fallback on a laptop only).
- Cards only in that Stripe account (or you add the card allow-list before the first friend-pays).
- Checkout `SuccessUrl`/`CancelUrl` are the tunnel/Vite origins, not forgotten localhost in prod.
- You watch that a webhook 500 after insert cannot happen, or you reconcile by hand.

That is Bar B on a laptop with a tunnel. It is not “production BYOK Stripe.”

---

## 24. What to do next (ordered, still not an implementation)

This is a port judgment, not a commit plan.

1. **Per-org `whsec_`.** Column next to `sk_`, or Stripe Webhook Endpoints API using the org key at PUT-time (CHIP-registrar judgment, not Hub MediatR). PUT body grows a webhook secret field **or** Pay registers the URL and stores the returned secret. Process env can remain a **dev fallback** for one-org, not the production SoT. Decrypt **that** org’s secret in `WebhookEndpoints`.
2. **One DB transaction:** verify → parse → insert unique → fulfill → commit. Duplicate unique → 200. Fulfill throw → rollback insert → 500 → Stripe retry is correct. Delete the two-step save.
3. **Steal card allow-list + `payment_status=paid` + org_id match** before the first non-author live charge.
4. **Stripe idempotency key** on Session create from `checkout.Id`. Persist `cs_` and `pi_` separately when the webhook arrives.
5. **Tests G22 actually asked for:** signed `mode=setup` payload, amount 0, assert zero `RCPT-` and `{ ignored: "setup_or_zero" }`. Cross-org event_id. 23505 race if you can provoke it.
6. **Do not** add `payment_intent.succeeded` fulfill until (2) and a `paid:{pi}` unique exist.
7. **Do not** add Billing Portal, Connect fees, `mode=subscription`, off-session, or a second rail in the same change as (1)–(4).
8. Align Stripe.net with Hub (48.0.1) or document why 48.0.0, and pick `throwOnApiVersionMismatch` against the Dashboard endpoint version.

---

## 25. Quote appendix — Hub setup-as-paid (the lie not to copy)

```659:686:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs
    internal static GatewayWebhookParsedResult? TryMapSetupIntentSucceeded(Event stripeEvent)
    {
        if (stripeEvent.Type != "setup_intent.succeeded")
        {
            return null;
        }
        // ...
        return new GatewayWebhookParsedResult(
            true, "PAYMENT_COMPLETED", stripeEvent.Id, 0, "", si.Id, meta, 0, 0, 0, 1, "",
            null, si.CustomerId, token);
    }
```

Hub test that locks it:

> `ParseWebhook_SetupIntentSucceeded_ExtractsPaymentMethod` → `EventType.Should().Be("PAYMENT_COMPLETED")` and `AmountPaid.Should().Be(0m)`.

New Pay fence (steal the ignore, not the Hub name):

```85:92:apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs
        if (stripeEvent.Type is "checkout.session.completed")
        {
            if (stripeEvent.Data.Object is Stripe.Checkout.Session session)
            {
                if (session.Mode == "setup" || (session.AmountTotal is null or 0))
                {
                    return Results.Json(new { ignored = "setup_or_zero" }, OneClient.Json);
                }
```

---

## 26. Quote appendix — Hub Connect refuse (keep)

```132:160:apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/StripeGatewayAdapterTests.cs
    public void CreateCheckoutSessionOptions_HasNoApplicationFeeOrTransfer_AndKeepsPayingTenant()
    {
        // ...
        options.PaymentIntentData!.ApplicationFeeAmount.Should().BeNull();
        options.PaymentIntentData.TransferData.Should().BeNull();
        // paying tenant_id preserved; platform_tenant_id stamped when adapter tenant differs
    }
```

```191:215:apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/StripeGatewayAdapterTests.cs
    public void PaymentAdapters_DoNotSetConnectApplicationFeeOrTransfer()
    {
        // StripeGatewayAdapter.cs ... Must NotContain ApplicationFeeAmount, application_fee, TransferData, transfer_data
    }
```

New Pay `StripeHosted` never sets those properties. Add the same source-grep test on `apps/lazuar-pay/src/Lazuar.Pay/Gateways` when the file grows, so a “small platform cut” cannot land.

---

## 27. Verdict sentence

New Pay already has the **right shape** for Stripe on 8081: merchant `sk_` in `SecretBox`, hosted `mode=payment`, Plane B verify with Stripe.net, empty body 400, `(org, provider, evt_)` idempotency, same-request fulfillment that writes `paid` + journal + `RCPT-`, setup/zero ignored, no MediatR, no Billing SoT, no Connect fee. It is **not** production BYOK Stripe until the webhook secret is per-org, insert+fulfill are one transaction, card wrap + `payment_status` + org match exist, and the G22 fixture is a real test rather than a ticked cell. Steal Hub’s HTTP (Session create, card list, EventUtility, event-id, dual-event key when PI is added, fee expand later, Connect-fee ban, setup PM extract). Refuse Hub’s event name for setup, outbox, factory, Billing Portal, and `application_fee_amount`.
