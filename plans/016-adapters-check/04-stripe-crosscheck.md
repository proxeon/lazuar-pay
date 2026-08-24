# 04 — Stripe cross-check: Hub `StripeGatewayAdapter` vs Pay `StripeHosted` + `StripeWebhook`

**Family:** 016-adapters-check  
**Slice:** Stripe specifically. Steal HTTP judgment. Do not clone `Modules/Payments`.  
**Date:** 24 August 2026  
**Type:** Uncondensed evaluation. **Not** an implementation. **Not** a project reference into `apps/lazuar-api`. **Not** a flip of 011/11 cells or 015 checklist ticks.

---

## 0. SHA, trees, method

| | |
|--|--|
| **Repo** | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` |
| **Branch** | `feat/015-four-adapters` (`ref: refs/heads/feat/015-four-adapters`) |
| **HEAD** | `c621ceba7fc7b79f16954d0819200cb21db6f22b` (`c621ceba`) — `docs(015): check off implemented T–Q phases` |
| **Parent index** | [016 README](./README.md) — this file is the Stripe evidence, not a bullet digest of the index |
| **Historical** | [014/05-stripe-port.md](../014-evals/05-stripe-port.md) at `ee2db8e5` on `main`. That paper described a **thinner** host: Stripe verify was process-only `Pay:StripeWebhookSecret`, webhook parse lived inside `WebhookEndpoints.cs`, `StripeHosted` returned a URL string, fulfill was a second transaction, and `WebhookTests` had three cases. **Live 015 code on this SHA is authority.** 014 is a delta map, not a substitute for opening the files again. |
| **015 map** | [015/00](../015-four-adapters/00-what-must-be-done.md) §3.6 Stripe harden list; checklists **H10–H25**, **P12**, **P16**, **P17**, **P19**, parked-offsession. Checklists are a map. Ticks are not proof. This paper re-reads live files. |

Files actually opened and quoted (not “I know this module”):

| Tree | Path | Lines / role |
|------|------|----------------|
| Hub | `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` | **743 lines, read in full** |
| Hub | `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs` | minor units, paying-tenant metadata, fee stamp, currency |
| Hub | `apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs` | port + `GatewayWebhookParsedResult` |
| Hub | `apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs` | Stripe row of the honest matrix |
| Hub | `apps/lazuar-api/Modules/Payments/Infrastructure/Endpoints.cs` | `POST /webhooks/payments/{gatewayType}/{tenantId}` |
| Hub | `ProcessGatewayWebhookCommandHandler.cs` | event-type allow-list, dual-event business key, outbox publish |
| Hub | `CheckoutSessionCashier.cs` | `KEY_MODE_MISMATCH` on `sk_test_` vs live |
| Hub | `StripeGatewayAdapterTests.cs` | Connect-fee ban, setup mode, `PAYMENT_COMPLETED` on setup, PI dual-event parse |
| Hub | `ProcessGatewayWebhookCommandHandlerTests.cs` | Stripe dual-event collapse |
| Hub | `PaymentGatewayCapabilitiesTests.cs` | Stripe off-session yes, API refund yes, DuitNow/wallet no, e-mandate no |
| Hub | `Modules.Payments.Infrastructure.csproj` + `apps/lazuar-api/Directory.Packages.props` | Stripe.net **48.0.1** |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Gateways/StripeHosted.cs` | **entire file, 50 lines** |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Gateways/StripeWebhook.cs` | **entire file, 84 lines** — did not exist in 014 |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs` | **entire file, 144 lines** — five-rail switch, one TX |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs` | PUT/GET BYOK including required `webhook_secret` |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Gateways/IHostedRail.cs` | two-method rail, not `IPaymentGatewayAdapter` |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Gateways/PayProviders.cs` | lowercase names; Stripe email optional |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Gateways/PspParseResult.cs` | paid vs ignored shape |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` | how Stripe success becomes `paid` + `RCPT-` |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Money/MoneyMath.cs` | always ×100; 3-letter currency |
| New | `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` | buyer `POST /v1/pay/{token}/start` dispatch |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` | DI: `StripeHosted`, `Fulfillment`, no MediatR |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj` | Stripe.net **48.0.0** |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs` | AES-GCM wrap; Production forbids git-known key |
| New | `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs` + `PayDbContext.cs` | `WebhookCiphertext`, `psp_webhook_events` PK |
| New | `apps/lazuar-pay/tests/Lazuar.Pay.Tests/WebhookTests.cs` | **entire file** — seven tests, six of them Stripe |
| New | `apps/lazuar-pay/tests/Lazuar.Pay.Tests/GatewayTests.cs` | PUT stripe requires `webhook_secret`; member 403 |
| New | `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPayTests.cs` | empty webhook body 400 (lives here, not in `WebhookTests`) |
| New | `apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs` | bans Hub types + `ApplicationFeeAmount` |
| New | `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PayApiFactory.cs` | `Pay:StripeWebhookSecret` hermetic HMAC |
| New | `apps/lazuar-pay/.env.example` | process `whsec_` is **dev fallback** |
| Law | 011 fail lock “Setup session counted as paid”; `NP-GW-002` / `NP-GW-008` / `NP-XX-012` |
| Law | 013/06 standing law + 015 §3.6 Stripe harden |

Method: Hub adapter is **HTTP judgment** (what to POST to Stripe, what to verify, which event types are cash). New Pay is the living host. Steal the decision. Do not copy MediatR, outbox, `IPaymentGatewayAdapter`, factory of five, Stripe Billing, Connect `application_fee_amount`, or `GatewayPaymentCompletedIntegrationEvent`.

**Production-ready Stripe on new Pay: still no.** Dogfood-ready for **one** org with a **test** `sk_` and a **pasted per-org** `whsec_`, against cards in `mode=payment`, with a tunnel and explicit Success/Cancel HTTPS: **narrow yes, with remaining money holes**. Multi-merchant live keys with Dashboard dynamic payment methods: **no**. Reasons are in §16, not in this sentence.

014’s two **blockers** (platform `whsec_` as SoT; unique insert committed before fulfill) **landed as code** on this SHA. They are not fully **proven** by tests, and they are not the only remaining holes. Do not treat 015 H-ticks as a close-out.

---

## 1. Standing law this slice is scored against

From 013/06, 011/01, and 015/00, restated only because this paper applies them to **live** Stripe files:

1. **BYOK.** Money settles on the **merchant’s** Stripe account. Pay is software, not an acquirer, not a Merchant of Record, not Stripe Connect `application_fee_amount`.
2. **`mode=payment` for charge.** `mode=setup` is **not** paid (`NP-GW-008`). Hub still maps setup to `EventType: "PAYMENT_COMPLETED"` with `AmountPaid: 0`. Steal the HTTP extract of customer + PM for a later vault. **Do not steal the event name.**
3. **Never Stripe Billing `subscription.updated` as source of truth** (`NP-XX-012`). Never `mode=subscription`. Never instantiate `Stripe.Subscription`. Pay’s later billing job mints a checkout or an off-session charge.
4. **Webhook:** verify; empty body **400**; idempotent `(org_id, provider, event_id)`; retry no-ops. Same-handler fulfillment **in one DB transaction**. Do **not** reintroduce `GatewayPaymentCompletedIntegrationEvent`.
5. **Steal adapters as HTTP judgment.** IsolationTests ban `MediatR` / `BuildingBlocks` / `Modules.` / `lazuar-api` / `IPaymentGatewayAdapter` / `PaymentGatewayFactory` / `GatewayPaymentCompletedIntegrationEvent` / `ApplicationFeeAmount` in the new host.
6. **015 amendment:** Stripe is one of **five** `hosted_link` wraps. Capability JSON must not claim off-session. Tax is out: no SST throw, no tax journal line, Official Receipt only.

011/03 fail lock, still in force:

> Fail (do not paper over):  
> - Setup session counted as paid.  
> - Webhook retry double-journals.

The dual of the second lock is **webhook retry never journals** (unique row committed, fulfill threw, Stripe gets `duplicate`). 015 H12/H25 exist to close that dual. Live code attempts the close. Tests do not inject a throwing fulfill.

---

## 2. What 014 said, and what 015 actually changed on Stripe

014/05 (`ee2db8e5`) is not wrong for its SHA. It is **stale as a description of this SHA**. Delta, from re-reading both trees:

| 014/05 claim at `ee2db8e5` | Live `c621ceba` |
|----------------------------|-----------------|
| `StripeHosted.cs` is 48 lines, returns `string` URL only | 50 lines, implements `IHostedRail`, returns `HostedSession(url, session.Id)` |
| Webhook parse is inline in `WebhookEndpoints` | Split to `StripeWebhook.Parse`; switch of five names in `WebhookEndpoints` |
| Verify uses **only** `config["Pay:StripeWebhookSecret"]` | Org `WebhookCiphertext` first; process env is **non-Production fallback** |
| `gateway_credentials` has no webhook column | `WebhookCiphertext` exists; PUT **requires** `webhook_secret` |
| Unique insert `SaveChanges` **then** `FulfillPaidAsync` starts its own TX | Outer `BeginTransaction` covers insert + fulfill `SaveChanges`; `Fulfillment` no longer begins a TX |
| No org bind: fulfill by checkout id globally | Handler 400s if `checkout.OrgId != path orgId` |
| No amount compare; books checkout amount blindly | Compare `AmountTotal` to `MoneyMath.ToMinor(checkout.Amount)`; 400 mismatch |
| `WebhookTests` has **three** tests | **Seven** tests: 503 missing secret, bad sig, paid+replay, **setup ignored**, **zero ignored**, **cross-org 400**, unknown provider |
| `Fulfillment` throws if `SstRegistered is null` | Throw **gone**. Comment on `OrgSettingsRow.SstRegistered`: unused. Two-line cash/revenue journal. |
| Capability always stripe because GET hard-codes it | GET uses `active_provider` or `?provider=`; capability string `"hosted_link"` for every rail |
| Start injects `StripeHosted` only | Start `switch`es five `IHostedRail` implementations |

014 blockers **B1** (platform `whsec_`) and **B2** (two transactions) are **addressed in source**. They are **not closed as production-ready** because: (1) non-Production still verifies with a process secret when the row is empty, and `IsProduction()` is the only gate — `Staging` would use the fallback; (2) tests never sign with a **different** row secret vs process secret, so H10 is unproven; (3) InMemory suppresses transactions, so H12 is call-order, not Postgres proof; (4) H25 has no throwing-fulfill test.

The remaining 014 **high** gaps that **still live** are listed in §14. Do not re-open B1/B2 as if 015 never happened. Do not declare them done because a checklist box is `[x]`.

---

## 3. Hub Stripe adapter — the 743-line HTTP map

Path: `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs`. Class implements `IPaymentGatewayAdapter`. `GatewayType => "STRIPE"`. Constructor is only `ILogger`. **No** `StripeClient` singleton; every call does `new StripeClient(apiKey)` with the **tenant** key the cashier decrypted. That is the BYOK shape. Steal it. Pay already does the same in `StripeHosted`.

The Hub port is a **five-verb cathedral**. New Pay’s `IHostedRail` is **one** verb. Parse is a static function next to the webhook route. That split is the anti-factory. Keep it.

```32:80:apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs
public interface IPaymentGatewayAdapter
{
    string GatewayType { get; }
    
    Task<GatewayCheckoutResult> GenerateCheckoutAsync(
        string apiKey,
        Guid tenantId,
        decimal amount,
        string currency,
        string productName,
        string customerEmail,
        string successUrl,
        string cancelUrl,
        Dictionary<string, string> metadata,
        string? merchantId,
        bool setupFutureUsage = false,
        int quantity = 1);
        
    Task<GatewayWebhookParsedResult> ParseWebhookAsync(...);
    Task<bool> IssueRefundAsync(...);
    Task<string> GenerateCustomerPortalAsync(...);
    Task<bool> ChargeOffSessionAsync(...);
}
```

Pay must not grow this interface. IsolationTests fail the string `IPaymentGatewayAdapter` anywhere under `apps/lazuar-pay/src`.

### 3.1 Generate hosted Checkout (`GenerateCheckoutAsync` + `CreateCheckoutSessionOptions`)

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

`merchantId` is unused on Stripe (CHIP Brand ID / Billplz Collection ID). Fine. Pay has no Stripe public merchant id; `PayProviders.RequiresPublicMerchantId` is Chip/Billplz only.

`CreateCheckoutSessionOptions` is the judgment:

1. `ApplyPayingTenantMetadata` — keep incoming `tenant_id` (platform SaaS charges on Hub); stamp `platform_tenant_id` when the adapter tenant differs. **New Pay has no system org.** Steal “do not clobber the paying org.” Do **not** steal Hub platform checkout.
2. **`$0` + `setupFutureUsage` → `Mode = "setup"`.** Comment on the file: “A `$0` PaymentIntent is invalid.” Line items are omitted. `SetupIntentData.Metadata = metadata`. `CustomerCreation = "always"`. Card-only PM list. **This session is a vault, not a capture.**
3. Else **`Mode = "payment"`**. One line item. Currency lowercased. `UnitAmountDecimal = GatewayCommon.ToMinorUnits(amount, currency)` (zero-decimal ISO currencies are **not** ×100; half away from zero). Product name or `GatewayCommon.DefaultProductName` (`"Lazuar Payment"`). Quantity is the line-item quantity, **not** also multiplied into `ToMinorUnits` on this call (the overload is invoked without the quantity argument).
4. **Metadata copied onto both Session and PaymentIntent** (`PaymentIntentData.Metadata = metadata`). That is why Hub can fulfill `payment_intent.succeeded` using the same `checkout_id` / `tenant_id`.
5. `ApplyCardWalletPaymentMethodTypes`: `PaymentMethodTypes = ["card"]` only. Comment: wallets (Apple Pay / Google Pay) ride on `card`; listing `apple_pay` / `google_pay` is invalid. **This list replaces Dashboard dynamic PMs.** Stripe FPX / GrabPay / Link / bank redirects will **not** appear on a Lazuar-created session. Hub tests lock this (`CreateCheckoutSessionOptions_IncludesCard_NotApplePay`).
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

Hub tests lock Connect refuse on this generate path (`CreateCheckoutSessionOptions_HasNoApplicationFeeOrTransfer_AndKeepsPayingTenant` and `PaymentAdapters_DoNotSetConnectApplicationFeeOrTransfer`):

- `options.PaymentIntentData.ApplicationFeeAmount` is **null**.
- `TransferData` is **null**.
- Source of all five adapters must not contain `ApplicationFeeAmount`, `application_fee`, `TransferData`, `transfer_data`.

There is **no** `Stripe-Account` header, no Connect client, no destination charge. `new StripeClient(apiKey)` with the merchant `sk_` **is** the platform model. Steal that. Never add `application_fee_amount`.

Pay IsolationTests ban the string `ApplicationFeeAmount` in `src/**/*.cs`. They do **not** ban `application_fee`, `TransferData`, or `transfer_data`. H22 asked for all four. Grep of `apps/lazuar-pay/src/Lazuar.Pay/Gateways` on this SHA: **zero** hits for any of those four. Absence is real. The test net is thinner than Hub’s.

### 3.2 Parse webhook (`ParseWebhookAsync`)

Auth is **not** Bearer. Auth is `Stripe-Signature` + `webhookSecret` (the **tenant’s** `whsec_`, decrypted in the command handler from `TenantPaymentConfiguration.WebhookSecret`).

Pipeline:

1. Missing `Stripe-Signature` (case-insensitive key scan) → `Verified=false`, error `"Missing Stripe-Signature header."`
2. `EventUtility.ConstructEvent(rawBody, signature, webhookSecret)` — Stripe library HMAC + ~300s timestamp. **Default `throwOnApiVersionMismatch: true`.** `StripeException` → `Verified=false`. Other construct failures → `AsUnusable()`.
3. **Mapped as money / vault / dispute / refund** (below).
4. Anything else → verified passthrough with **raw** `stripeEvent.Type` and `stripeEvent.Id`. Handler then **drops** it (see §4) **without inserting a webhook log**. That is how `customer.subscription.updated` / `invoice.paid` stay non-SoT **without a dedicated refuse branch** — they are never mapped to `PAYMENT_COMPLETED`. Accidental correctness. New Pay should keep the **ignore**, but Pay **does** insert the unique grain on ignore (H15 policy A). Both are legal for Stripe `evt_` uniqueness.

**Honor as cash (Hub maps to `PAYMENT_COMPLETED`):**

| Stripe type | Object | Hub `EventType` | `EventId` | `GatewayTransactionId` | Amount |
|-------------|--------|-----------------|-----------|------------------------|--------|
| `checkout.session.completed` | `Session` with PaymentIntent | `PAYMENT_COMPLETED` | `stripeEvent.Id` (`evt_…`) | `session.PaymentIntentId ?? session.SetupIntentId ?? session.Id` | `(AmountTotal ?? 0) / 100m` |
| `checkout.session.completed` | `Session` **setup**, no PI | `PAYMENT_COMPLETED` **amount 0** if PM extracted; else `Verified=false` so Stripe retries (B04-P20) | `evt_…` | SetupIntent id | `0` |
| `payment_intent.succeeded` | `PaymentIntent` | `PAYMENT_COMPLETED` | `evt_…` | `pi.Id` | `AmountReceived / 100m` |
| `setup_intent.succeeded` | `SetupIntent` | **`PAYMENT_COMPLETED` amount 0** if PM present | `evt_…` | `si.Id` | `0` |

That last two setup rows are the **Hub lie** 013/06 named: “There is **no** distinct `SETUP_COMPLETED` type. Setup is stuffed into `PAYMENT_COMPLETED`.” Hub tests **lock the lie**:

```490:508:apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/StripeGatewayAdapterTests.cs
    public async Task ParseWebhook_CheckoutSessionCompleted_SetupIntentWithoutPi_ExtractsCustomerAndPaymentMethod()
    {
        ...
        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("PAYMENT_COMPLETED");
        result.EventId.Should().Be("evt_cs_setup");
        result.GatewayTransactionId.Should().Be("seti_1");
        result.AmountPaid.Should().Be(0m);
        result.GatewayCustomerId.Should().Be("cus_setup_1");
        result.GatewayTokenId.Should().Be("pm_setup_1");
    }
```

```536:554:apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/StripeGatewayAdapterTests.cs
    public async Task ParseWebhook_SetupIntentSucceeded_ExtractsPaymentMethod()
    {
        ...
        result.EventType.Should().Be("PAYMENT_COMPLETED");
        result.AmountPaid.Should().Be(0m);
        result.GatewayCustomerId.Should().Be("cus_ok");
        result.GatewayTokenId.Should().Be("pm_ok");
    }
```

Steal customer + PM extract **later** (parked-offsession). **Do not steal `PAYMENT_COMPLETED` as the name.** New Pay must not book this as cash (`NP-GW-008`). Live Pay ignores setup as `setup_or_zero` and ignores `setup_intent.succeeded` as unknown type. That is the correct refuse.

Fee extract on the Hub cash path: extra HTTP `PaymentIntentService.GetAsync(id, Expand = latest_charge.balance_transaction [, payment_method])`. `ApplyBalanceTransactionFee` copies `Abs(bt.Fee / 100m)` and FX. Expand failure logs a warning, stamps `gateway_fee_status=unknown`, **does not block fulfillment**. `TaxAmount` on a Session is `(session.TotalDetails?.AmountTax ?? 0) / 100m`. There is **no** `automatic_tax` on generate, so this is Stripe Tax if the Dashboard enabled it, not SST. New Pay journal does not book fee/tax lines (cash + revenue only). Steal “unknown ≠ 0” (`NP-MON-002`) when fees exist; do not invent 0 as known. Do not port the expand on the first capture path — it is a second Stripe HTTP inside the webhook, timeout risk.

Currency: `GatewayCommon.TryNormalizeCurrency` — 3-letter, uppercased. Missing currency → unusable, “refusing to invent MYR.” Steal fail-closed. Pay’s `MoneyMath.TryNormalizeCurrency` is the same helper. Pay’s **use** of it is not fail-closed (see §7.4).

**Honor as failed (not paid):**

- `payment_intent.payment_failed` → `PAYMENT_FAILED`, decline code copied into metadata (`decline_code`), `Error = LastPaymentError.Message`. Handler publishes `GatewayPaymentFailedIntegrationEvent`. Late `PAYMENT_FAILED` after `PAYMENT_COMPLETED` for the same PI is ignored (`PAYMENT_COMPLETED:` + tx id lookup).

**Honor as dispute (not paid, not a journal reverse by itself):**

- `charge.dispute.created` → `DISPUTE_CREATED`
- `charge.dispute.closed` → `DISPUTE_CLOSED` + `meta["dispute_outcome"] = status`
- `charge.dispute.updated` → `DISPUTE_CLOSED` if status is `won` / `lost` / `warning_closed`, else treated as `DISPUTE_CREATED`

Amount = `dispute.Amount / 100m`. Metadata pulled from the PaymentIntent when possible (second HTTP GET). New Pay has **no** dispute table. Do not port this on hosted_link. Do not drop it from the steal-list for later.

**Honor as refund completed:**

- `TryMapRefundCompleted`: `Refund` object with `status == succeeded`, or a `Charge` whose refunds list has a succeeded refund. Maps to `REFUND_COMPLETED`. **`EventId = refund.Id` (`re_…`), not `evt_…`.** Business key is **null** so PI-level collapse does not eat later refund slices. Pending refunds pass through as raw type (test: `ParseWebhook_RefundUpdatedPending_IsNotCompleted` expects `EventType == "refund.updated"`). Hub `IssueRefundAsync` treats only `succeeded` as success.

**Ignore (verified passthrough, handler returns without log):**

- `customer.updated`, `customer.subscription.updated`, `invoice.paid`, `invoice.payment_succeeded`, `charge.succeeded`, `payment_intent.created`, `payment_intent.processing`, Radar, Billing Portal, etc.
- There is **no** `case "customer.subscription.updated"` in the adapter. `Mode` on generate is never `"subscription"`. That is `NP-XX-012` encoded by omission.

Hub parse **does not** check `session.PaymentStatus == "paid"`. Hub **does** force `PaymentMethodTypes = ["card"]` on generate, which is the other half of that fence. Pay dropped the wrap and also does not check `payment_status`. That combination is worse than Hub.

### 3.3 Off-session charge (`ChargeOffSessionAsync`) — refuse for this program

HTTP: `PaymentIntentService.CreateAsync` with `OffSession = true`, `Confirm = true`, amount via `ToMinorUnits`, idempotency `lazuar-offsession:{chargeAttemptId}`. Success is **`succeeded` only** (`IsOffSessionSucceeded`). Tests lock `processing` / `requires_action` / `failed` as false. `StripeException` → `OffSessionDeclinedException`.

Caller waits for `payment_intent.succeeded` to publish completed. Steal: “adapter true is not a `RCPT-`.” New Pay must not build this for hosted_link. 015 `parked-offsession.md` is explicit. `PaymentGatewayCapabilities.SupportsOffSession("STRIPE")` is **true** on Hub and **must not** become a JSON flag on Pay.

### 3.4 Refund (`IssueRefundAsync`) — refuse for this program

HTTP: `RefundService.CreateAsync` with `PaymentIntent = transactionId`. Needs a **PaymentIntent id**. Pay stores `ProviderRef = session.Id` (`cs_…`). That is a later trap. Do not copy `IssueRefundAsync` until `pi_` is persisted.

### 3.5 Customer portal (`GenerateCustomerPortalAsync`) — refuse

HTTP: `CustomerService.ListAsync({ Email, Limit = 1 })` then `Stripe.BillingPortal.SessionService.CreateAsync`. First customer with that email wins. This is **Stripe Billing Portal**. It is not Pay’s buyer magic-link. New Pay correctly has **zero** portal code. Keep it that way. `NP-XX-012`. G23.

### 3.6 Things the Hub adapter does **not** do (search results on this SHA)

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
| `ephemeral` | **Zero hits** under `Modules/Payments`. No Stripe.js / Payment Element. Wrap is hosted Checkout. |
| tax | Session `TotalDetails.AmountTax`; off-session SST **metadata**. No `automatic_tax` on Session create. |
| amount zero | Generate: $0 + setupFutureUsage → setup mode. Parse: amount 0 still `PAYMENT_COMPLETED`. |
| `EventType PAYMENT_COMPLETED` | Cash **and** setup. |
| `application_fee` / `ApplicationFeeAmount` / `TransferData` | **Forbidden by test.** Not in source. |
| `customer.subscription` / `mode = "subscription"` | **Not in the adapter.** |
| Connect `Stripe-Account` header | **Not present.** |
| `payment_status` | **Not read** on parse. Card wrap is the substitute. |

---

## 4. Hub webhook door and handler — Stripe branches (do not copy the shape)

HTTP door: `POST /webhooks/payments/{gatewayType}/{tenantId:guid}` (`Modules/Payments/Infrastructure/Endpoints.cs`). Allow-list includes `STRIPE`. Empty body **400** `"Empty request body."` Then `IMediator.Send(ProcessGatewayWebhookCommand)`. ACK `{ received: true }`. Comment on the endpoint: **“Intake ACK only. Domain fulfillment lives in Commerce / Billing / M2M session.”** That split is the cathedral new Pay exists to leave.

```23:75:apps/lazuar-api/Modules/Payments/Infrastructure/Endpoints.cs
    public static IEndpointRouteBuilder MapPaymentsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/webhooks/payments");

        group.MapPost("/{gatewayType}/{tenantId:guid}", async (
            string gatewayType,
            Guid tenantId,
            HttpContext context,
            IMediator mediator,
            ILoggerFactory loggerFactory) =>
        {
            ...
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return Results.BadRequest(new { error = "Empty request body." });
            }
            ...
                await mediator.Send(command);
                // Intake ACK only. Domain fulfillment lives in Commerce / Billing / M2M session.
                return Results.Ok(new { received = true });
```

Handler core (Stripe-relevant):

1. Load `TenantPaymentConfiguration` for `(tenantId, gatewayType)`. Missing config or empty `WebhookSecret` → throw `"Webhook secret not configured for this tenant gateway."` **Per-tenant `whsec_`.** Decrypt with `ISecretVault`. Decrypt API key too (fee expand uses it). Soft-disabled gateways still process (comment: credentials retained).
2. `adapter.ParseWebhookAsync(plainApiKey, plainWebhookSecret, rawBody, headers, 0, 0, 0)` — estimated fee/tax on the port are dead (always 0). Do not port those parameters.
3. `Verified=false` + unusable → `PaymentWebhookUnusablePayloadException` → HTTP **400**. `Verified=false` otherwise → `InvalidOperationException` “verification failed”. Endpoints **do not catch** `InvalidOperationException` (the `when` filter excludes it) → **HTTP 500 on bad signature.** 013/06 anti-goal 12: “Signature fail 500. Hub does this. Stripe retries until the endpoint looks like an outage.” **Steal 400 from Pay, not Hub’s 500.**
4. Event types that proceed: `PAYMENT_COMPLETED`, `DISPUTE_CREATED`, `DISPUTE_CLOSED`, `PAYMENT_FAILED`, `REFUND_COMPLETED`. Anything else **return** (200, **no log insert**). That drop is how Billing events stay non-SoT.
5. Inbound `metadata.tenant_id` ≠ URL tenant, unless `IsPlatformCheckoutWebhook` (system org + `platform_tenant_id`) → warn, return, no publish. New Pay has no system org; 013 said mismatch must **200 ignore**, never 400 (Stripe retries poison). 015 H13 **chose 400**. Live Pay 400s. See §7.3.
6. Idempotency: unique `(OrganizationId, Provider, EventId)` plus **business key** `EVENTTYPE:GatewayTransactionId` so `checkout.session.completed` and `payment_intent.succeeded` for the same `pi_…` collapse. Test `Handle_StripeDualEvents_SameBusinessKey_Publishes_OnlyOnce`: two `evt_` ids, one `piId`, **one** `GatewayPaymentCompletedIntegrationEvent`. Refunds: business key null.
7. Unique-violation `23505` on save is swallowed (concurrent delivery).
8. **Publish** `GatewayPaymentCompletedIntegrationEvent` onto keyed `"PaymentsEventBus"` / outbox.

```89:96:apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs
        if (parsedResult.EventType != "PAYMENT_COMPLETED"
            && parsedResult.EventType != "DISPUTE_CREATED"
            && parsedResult.EventType != "DISPUTE_CLOSED"
            && parsedResult.EventType != "PAYMENT_FAILED"
            && parsedResult.EventType != "REFUND_COMPLETED")
        {
            return;
        }
```

New Pay grep for `GatewayPaymentCompletedIntegrationEvent` under `apps/lazuar-pay`: **zero hits.** IsolationTests ban the string. Keep it that way.

Hub dual-event test (the reason you must **not** “just add” `payment_intent.succeeded` on Pay):

```463:527:apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/ProcessGatewayWebhookCommandHandlerTests.cs
    public async Task Handle_StripeDualEvents_SameBusinessKey_Publishes_OnlyOnce()
    {
        ...
        const string piId = "pi_dual_shared";
        ...
                // First: payment_intent.succeeded; second: checkout.session.completed — same PI.
                var eventId = call == 1 ? "evt_pi_succeeded" : "evt_session_completed";
                return new GatewayWebhookParsedResult(
                    true, "PAYMENT_COMPLETED", eventId, 50m, "MYR", piId, ...);
        ...
        await eventBus.Received(1).PublishAsync(Arg.Is<GatewayPaymentCompletedIntegrationEvent>(e =>
            e.GatewayTransactionId == piId
            && e.Metadata["checkout_id"] == checkoutId.ToString()));
    }
```

Pay currently ignores PI events. Unique grain is `evt_…` only. Safe **because** PI is ignored. Becomes a double-fulfill bug the day someone honors PI without a `paid:{pi}` collapse **and** without the setup/zero fence.

---

## 5. Hub `PaymentGatewayCapabilities` — Stripe row

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
    // SupportsDuitNowQr => XENDIT or CHIP or BILLPLZ
    // SupportsHostedWallet => XENDIT or CHIP + GRABPAY/SHOPEEPAY/TNG/...
    // SupportsEmandate => false (every name)
    // RequiresMarkRefunded => blank / BILLPLZ / OFFLINE / ...
}
```

Locked by `PaymentGatewayCapabilitiesTests`:

| Axis | Stripe (`"STRIPE"` / `"stripe"`) | Implication for Pay |
|------|----------------------------------|---------------------|
| `SupportsOffSession` | **true** | Stripe **can** vault later. JSON on 8081 must **not** say so yet. Live GET returns `capability: "hosted_link"`. P16. Correct. |
| `IsReminderOnlyGateway` | **false** | Opposite of Billplz. Do not tell merchants Stripe is reminder-only. |
| `SupportsApiRefund` | **true** | Later. Needs `pi_`. |
| `SupportsDuitNowQr` | **false** | Do not render QR. |
| `SupportsHostedWallet` | **false** | Wallets ride on `card` when Stripe shows them. Hub forced card-only wrap. |
| `SupportsEmandate` | **false** | Stay false. |
| `RequiresMarkRefunded` | **false** | Not a SOP-refund rail. |

Pay has **no** capability matrix type. `PayProviders.Capability = "hosted_link"` is a single string for all five names. That is honest for 015. Do not port `PaymentGatewayCapabilities` into a Contracts assembly. Do not return `vaulted` / `off_session` / `emandate` from GET. GatewayTests lock `"hosted_link"` on a stripe PUT.

---

## 6. New Pay `StripeHosted.cs` — entire living generate path

The whole class, because it is the create-options SoT:

```1:50:apps/lazuar-pay/src/Lazuar.Pay/Gateways/StripeHosted.cs
using Lazuar.Pay.Data;
using Lazuar.Pay.Money;
using Lazuar.Pay.Secrets;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace Lazuar.Pay.Gateways;

public sealed class StripeHosted(PayDbContext db, SecretBox box) : IHostedRail
{
    public string Provider => PayProviders.Stripe;

    public async Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)
    {
        var cred = await db.GatewayCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == checkout.OrgId && x.Provider == PayProviders.Stripe, ct);
        if (cred is null)
        {
            throw new InvalidOperationException("rail not configured");
        }

        var secret = box.Unprotect(cred.Ciphertext);
        var service = new SessionService(new StripeClient(secret));
        var cents = MoneyMath.ToMinor(checkout.Amount);
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
        var url = session.Url ?? throw new InvalidOperationException("Stripe returned no URL");
        return new HostedSession(url, session.Id);
    }
}
```

Caller: `PublicPayEndpoints.Start` (`POST /v1/pay/{token}/start`). Buyer has **no** One account. Decrypts **that checkout’s org** `sk_` from `gateway_credentials`. `StripeException` → 503 `"Stripe rejected the org key"`. Missing rail → 503 `"rail not configured"`. Pause → 403. Paid/expired → 409.

```92:119:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        IHostedRail rail = name switch
        {
            PayProviders.Stripe => stripe,
            PayProviders.Chip => chip,
            PayProviders.Billplz => billplz,
            PayProviders.Xendit => xendit,
            PayProviders.Razorpay => razorpay,
            _ => stripe
        };

        try
        {
            var hosted = await rail.CreateHostedUrlAsync(row, ct);
            row.Provider = name;
            row.PspRedirectUrl = hosted.RedirectUrl;
            row.ProviderSessionId = hosted.ProviderSessionId;
            await db.SaveChangesAsync(ct);
            return Results.Json(new { redirect_url = hosted.RedirectUrl }, OneClient.Json);
        }
        ...
        catch (Stripe.StripeException)
        {
            return PayErrors.Status(503, "Service Unavailable", "Stripe rejected the org key");
        }
```

014 said start was Stripe-only. Live is a five-way switch. The `_ => stripe` default is dead (`TryNormalize` already allow-listed). Stripe email is **optional** (`PayProviders.RequiresEmail` is `provider is not Stripe`). That matches P19 and Hub’s `CustomerEmail` nullable. Pay still **does not send** the email to Stripe even when the buyer typed one.

`Program.cs` registers `AddScoped<StripeHosted>()` and `AddScoped<Fulfillment>()`. No MediatR. IsolationTests still ban Hub tokens.

### 6.1 HTTP create options — steal vs live, field by field

| Option | Hub payment session | Pay `StripeHosted` | Steal or refuse? |
|--------|---------------------|--------------------|------------------|
| Client | `new StripeClient(apiKey)` per call, merchant `sk_` | `new StripeClient(secret)` from org ciphertext | **Stolen.** Keep. |
| `Mode` | `"payment"` (or `"setup"` if $0+vault) | **always `"payment"`** | **Stolen** for hosted_link. Do **not** add setup generate in this program (H19.2). Webhook still fences setup if a Dashboard-created session hits the endpoint. |
| `mode=subscription` | **Absent** | **Absent** (grep zero under `Lazuar.Pay`) | **Refuse** forever as SoT. |
| `ClientReferenceId` | Not set on adapter (Commerce metadata instead) | `checkout.Id` | **Keep.** Webhook lookup uses it first. |
| Session `Metadata` | Caller dict + paying-tenant stamps | `{ checkout_id, org_id }` | **Stolen keys.** No `tenant_id`, no `platform_tenant_id` — correct (no Hub SaaS org). |
| `PaymentIntentData.Metadata` | **Copy of the same dict** | **Unset** | **Gap.** PI fallback cannot find checkout later. |
| `PaymentMethodTypes` | `["card"]` | **Unset** — Dashboard defaults | **Gap, high.** Delayed PMs can complete with `payment_status=unpaid`. |
| `CustomerEmail` | Set when non-blank | **Unset** | **Gap, medium.** Payer email already on checkout row after start. |
| Product `Name` | caller / `"Lazuar Payment"` | always `"Pay"` | **Gap, low.** Catalog name exists and is unused. |
| Minor units | `ToMinorUnits(amount, currency)` — JPY factor 1 | `MoneyMath.ToMinor` = **always ×100** | **Gap when currency is not two-decimal.** Catalog default MYR. Checkout create does **not** refuse non-MYR. |
| Quantity | line `Quantity` from caller | always `1` | Honest for one-off checkout. |
| Success/Cancel | caller HTTPS | checkout URLs **or `http://localhost:5179/...`** | Laptop fallback. Production without `SuccessUrl` lands on a machine origin. Default success includes `?status=verifying` — **stolen** from 013 (landing is not paid). |
| `SetupFutureUsage` | optional `off_session` + `CustomerCreation=always` | **Absent** | **Refuse** until vault columns exist. |
| `ApplicationFeeAmount` / `TransferData` | **null**, tested | **unset**, Isolation greps `ApplicationFeeAmount` only | **Stolen absence.** Keep. |
| Stripe `Idempotency-Key` | **Absent** on Session create | **Absent** | **Gap, high.** Double-start mints two `cs_` URLs. |
| Persist session id | cashier stores gateway session id | `HostedSession.ProviderSessionId = session.Id` written to `checkout.ProviderSessionId` | **Stolen in 015** (014 said URL-only). |
| `KEY_MODE_MISMATCH` | cashier: `sk_test_` vs live env → 409 | **Absent.** `Environment` column is stored, **never read** by `StripeHosted` | **Gap, high.** Live key on a “test” org charges live cards. |
| Connect `Stripe-Account` | Absent | Absent | Keep absent. |

Hub minor-units policy, the one Pay did **not** steal:

```68:85:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs
    private static readonly HashSet<string> ZeroDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIF", "CLP", "DJF", "GNF", "ISK", "JPY", "KMF", "KRW", "PYG",
        "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF"
    };
    public static long ToMinorUnits(decimal amount, string? currency = "MYR", int quantity = 1)
    {
        var qty = quantity < 1 ? 1 : quantity;
        var factor = IsZeroDecimalCurrency(currency) ? 1m : 100m;
        return (long)Math.Round(amount * qty * factor, 0, MidpointRounding.AwayFromZero);
    }
```

Pay:

```5:6:apps/lazuar-pay/src/Lazuar.Pay/Money/MoneyMath.cs
    public static long ToMinor(decimal amount) =>
        (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
```

Away-from-zero midpoint: stolen. Zero-decimal table: not stolen. Checkout create:

```42:47:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs
        if (body?.Amount is null || body.Amount <= 0)
        {
            return PayErrors.Status(400, "Bad Request", "amount must be greater than 0");
        }

        var currency = string.IsNullOrWhiteSpace(body.Currency) ? "MYR" : body.Currency.Trim().ToUpperInvariant();
```

Amount `<= 0` cannot be created, so Pay never generates Hub’s setup-mode `$0` session. That is why generate-never-setup is safe **on the Pay create path**. A merchant can still create a setup Session in the Stripe Dashboard and point it at Pay’s webhook URL. The webhook fence exists for that.

`UnitAmount` (long) vs Hub `UnitAmountDecimal`: for integer sen (MYR 10.00 → 1000) they agree. Fractional sen after `ToMinor` is already rounded.

Connect fee absence on the create options object: there is nothing to quote. The object initializer does not mention `PaymentIntentData` at all, so `ApplicationFeeAmount` cannot be set. Hub **explicitly** allocates `PaymentIntentData` for metadata and then tests that fee fields stay null. Pay’s silence is correct for Connect, and **incorrect** for PI metadata copy.

There is **no hermetic test of `CreateHostedUrlAsync`**. `FakePspHandler` intercepts `IHttpClientFactory` clients (`chip` / `billplz` / `xendit` / `razorpay`). Stripe.net talks to Stripe itself. RailTests cover the other four starts. Stripe start is an untested network call. A regression that set `Mode = "subscription"` or `ApplicationFeeAmount = 1` would not fail `WebhookTests`. Isolation greps `ApplicationFeeAmount` as a string, which would catch a source add, not a runtime options object built from a helper in another assembly. Today the options are a literal in `StripeHosted.cs`, so the grep is enough **until the file grows**.

---

## 7. New Pay `StripeWebhook.cs` + `WebhookEndpoints` Stripe path

### 7.1 Entire parse (living)

```12:83:apps/lazuar-pay/src/Lazuar.Pay/Gateways/StripeWebhook.cs
    public static PspParseResult Parse(
        string json,
        IHeaderDictionary headers,
        GatewayCredentialRow cred,
        SecretBox box,
        IConfiguration config,
        IHostEnvironment env)
    {
        var whsec = ResolveSecret(cred, box, config, env);
        if (string.IsNullOrWhiteSpace(whsec))
        {
            throw new InvalidOperationException("webhook secret missing");
        }

        headers.TryGetValue("Stripe-Signature", out var sig);
        Event stripeEvent;
        try
        {
            EventUtility.ValidateSignature(json, sig.ToString(), whsec);
            stripeEvent = EventUtility.ConstructEvent(json, sig.ToString(), whsec, throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            throw new PspVerifyException("invalid signature");
        }
        catch (Exception)
        {
            throw new PspVerifyException("invalid event");
        }

        if (stripeEvent.Type is not "checkout.session.completed")
        {
            return new PspParseResult { EventId = stripeEvent.Id, Ignored = true, IgnoreReason = stripeEvent.Type };
        }

        if (stripeEvent.Data.Object is not Session session)
        {
            return new PspParseResult { EventId = stripeEvent.Id, Ignored = true, IgnoreReason = "no_session" };
        }

        if (session.Mode == "setup" || session.AmountTotal is null or 0)
        {
            return new PspParseResult { EventId = stripeEvent.Id, Ignored = true, IgnoreReason = "setup_or_zero" };
        }

        var checkoutId = session.ClientReferenceId ?? session.Metadata?.GetValueOrDefault("checkout_id");
        MoneyMath.TryNormalizeCurrency(session.Currency, out var currency);

        return new PspParseResult
        {
            EventId = stripeEvent.Id,
            CheckoutId = checkoutId,
            ProviderRef = session.Id,
            AmountMinor = session.AmountTotal,
            Currency = string.IsNullOrEmpty(currency) ? null : currency
        };
    }

    static string? ResolveSecret(...)
    {
        if (!string.IsNullOrWhiteSpace(cred.WebhookCiphertext))
            return box.Unprotect(cred.WebhookCiphertext);
        if (!env.IsProduction())
            return config["Pay:StripeWebhookSecret"];
        return null;
    }
```

Handler maps Stripe into the five-way switch:

```48:68:apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs
        PspParseResult parsed;
        try
        {
            parsed = name switch
            {
                PayProviders.Stripe => StripeWebhook.Parse(raw, request.Headers, cred, box, config, env),
                PayProviders.Chip => ChipWebhook.Parse(raw, request.Headers, cred, box),
                ...
            };
        }
        catch (PspVerifyException ex)
        {
            return PayErrors.Status(400, "Bad Request", ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("webhook secret", StringComparison.Ordinal))
        {
            return PayErrors.Status(503, "Service Unavailable", ex.Message);
        }
```

Then unique find, ignore insert, org bind, currency/amount compare, **one TX** insert + fulfill:

```70:123:apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs
        if (await db.PspWebhookEvents.FindAsync([orgId, name, parsed.EventId], ct) is not null)
        {
            return Results.Ok(new { duplicate = true });
        }

        if (parsed.Ignored)
        {
            await InsertEventAsync(db, orgId, name, parsed.EventId, ct);
            return Results.Json(new { ignored = parsed.IgnoreReason }, OneClient.Json);
        }

        if (string.IsNullOrWhiteSpace(parsed.CheckoutId))
        {
            return PayErrors.Status(400, "Bad Request", "checkout not found");
        }

        var checkout = await db.Checkouts.FirstOrDefaultAsync(x => x.Id == parsed.CheckoutId, ct);
        if (checkout is null || checkout.OrgId != orgId)
        {
            return PayErrors.Status(400, "Bad Request", "checkout not found");
        }

        if (parsed.Currency is not null
            && !string.Equals(parsed.Currency, checkout.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return PayErrors.Status(400, "Bad Request", "currency mismatch");
        }

        if (parsed.AmountMinor is not null && parsed.AmountMinor.Value != MoneyMath.ToMinor(checkout.Amount))
        {
            return PayErrors.Status(400, "Bad Request", "amount mismatch");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.PspWebhookEvents.Add(new PspWebhookEventRow { OrgId = orgId, Provider = name, EventId = parsed.EventId, ... });
            await db.SaveChangesAsync(ct);
            await fulfillment.FulfillPaidAsync(checkout.Id, name, parsed.ProviderRef, ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            return Results.Ok(new { duplicate = true });
        }

        return Results.Json(new { ok = true }, OneClient.Json);
```

Empty body 400, unknown provider 400, rail not configured 400 — all live, before parse.

Path is `/v1/webhooks/{provider}/{orgId}`. Provider must normalize to `stripe` (lowercase). Hub used `STRIPE` in a GUID tenant path. Steal the **idea** (per-org door, no Bearer). Do not steal Hub’s path or MediatR.

### 7.2 Webhook events honored vs ignored

**Honored as cash (fulfill):**

- `checkout.session.completed`
- **and** `Data.Object` is a `Session`
- **and** `session.Mode != "setup"`
- **and** `session.AmountTotal` is not null and not 0
- **and** checkout id from `ClientReferenceId` or metadata `checkout_id` exists
- **and** that checkout’s `OrgId` equals path `{orgId}`
- **and** if parse emitted a currency, it matches checkout currency (case-insensitive)
- **and** if parse emitted `AmountMinor`, it equals `ToMinor(checkout.Amount)`
- **then** `FulfillPaidAsync(checkout.Id, "stripe", session.Id)`

There is **no** `payment_status == "paid"` check. A `mode=payment` session with `amount_total=1000` and `payment_status=unpaid` (async bank / voucher / delayed method) is cash as far as Pay is concerned. Hub’s card wrap was the mitigation Pay dropped.

There is **no** read of `session.PaymentIntentId`. `ProviderRef` is `session.Id` (`cs_…`). Hub’s `GatewayTransactionId` prefers `pi_`. Refunds, disputes, and a future dual-event key all want `pi_`.

**Ignored (200 `{ ignored: reason }`, unique row inserted, no fulfill):**

| Stripe type / shape | `IgnoreReason` | vs Hub |
|---------------------|----------------|--------|
| anything other than `checkout.session.completed` | the **raw type string** (e.g. `payment_intent.succeeded`, `setup_intent.succeeded`, `customer.subscription.updated`, `invoice.paid`, `charge.dispute.created`, `refund.updated`) | Hub **maps several of these to money/vault/dispute/refund**. Pay **refuses the Hub maps** for hosted_link. Correct for 015. |
| `checkout.session.completed` but object is not `Session` | `no_session` | Hub would fail the `is Session` branch and maybe look at `PaymentIntent`. |
| `mode == "setup"` | `setup_or_zero` | Hub maps to `PAYMENT_COMPLETED` amount 0 if a PM exists; else `Verified=false` to force retry. Pay **does not retry-storm** and **does not pay**. Steal Pay, refuse Hub. |
| `AmountTotal` null or 0 | `setup_or_zero` | Hub still `PAYMENT_COMPLETED` with 0. Pay ignores. `NP-GW-008`. |

H15.2: “Do not map `setup_intent.succeeded` to paid.” Live: type is not `checkout.session.completed` → ignored with reason `setup_intent.succeeded`. **Not** paid. **Not** vaulted. PM is discarded. Parked-offsession wants the extract later.

H15.2: “Do not listen `customer.subscription.*` as SoT.” Live: ignored as unknown type. **No** `Stripe.Subscription` type in Pay src (grep zero). `NP-XX-012` encoded by omission, same as Hub generate.

**400 (not ignored, not paid, unique grain NOT consumed):**

- empty body
- unknown provider
- rail not configured (no credential row)
- invalid signature / invalid event
- missing checkout id after a non-ignored parse
- checkout missing **or org mismatch**
- currency mismatch (only if parse emitted a currency)
- amount mismatch (only if parse emitted `AmountMinor`)

Stripe retries 400s. Permanent mismatches (coupon changed `amount_total`; cross-org; garbage `client_reference_id`) become **poison** for that endpoint until Stripe gives up (~3 days). 013/06 said metadata org mismatch should be **200 ignore**. H13 picked 400. Live is 400. Ops noise, not a second `RCPT-` on the victim org (the org check holds).

**503:**

- webhook secret missing after `ResolveSecret` (Production + empty row; or Testing + empty row + empty process env)

**200 `{ duplicate: true }`:**

- unique `(orgId, provider, eventId)` already present (Find hit, **or** `DbUpdateException` on insert)

H24 asked the catch comment to name **23505**. Live catch is bare `DbUpdateException` with no 23505 comment. InMemory may throw a different unique exception; the catch is broad enough to 200. Concurrent test: **absent**. Serial replay is tested.

### 7.3 Setup-not-paid

Generate never creates setup. Webhook still fences it.

Parse:

```52:55:apps/lazuar-pay/src/Lazuar.Pay/Gateways/StripeWebhook.cs
        if (session.Mode == "setup" || session.AmountTotal is null or 0)
        {
            return new PspParseResult { EventId = stripeEvent.Id, Ignored = true, IgnoreReason = "setup_or_zero" };
        }
```

Belt in fulfillment (PSP-zero is the H20 target; this is checkout-row zero):

```16:19:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
        if (checkout.Amount <= 0)
        {
            return;
        }
```

Checkout create already rejects `Amount <= 0`, so the fulfillment belt is for a mutated row, not the happy path.

H19 fixture exists: `WebhookTests.Setup_mode_is_ignored`. Signed `checkout.session.completed` with `"mode":"setup"`, `amount_total: 0`, `client_reference_id` of an **open** checkout with amount 10. Asserts 200, body contains `ignored`, `Documents.Count == 0`, checkout still `open`. **This is the 011 fail lock as a test.** 014 said it was missing. 015 landed it.

H20 fixture exists: `Zero_amount_session_is_ignored`. `mode=payment`, `amount_total: 0`, `payment_status: paid` (trap). Asserts documents 0. Does **not** assert body `ignored`, does **not** assert checkout still `open`. Partial relative to H20.1 text. The parse path is the same `setup_or_zero` branch, so it should 200 ignore; the test is weaker than H19.

Hub’s setup-as-paid tests must **never** be ported as expectations on Pay.

### 7.4 Amount match and currency

H14: compare `session.AmountTotal` (minor) to checkout ×100 AwayFromZero. Mismatch → do not fulfill; prefer 400 so unique-as-paid is not consumed.

Live handler does that. Live **tests do not**. There is **no** `amount_total: 999` vs checkout `10.00` case under `apps/lazuar-pay/tests`. H14.4 exit “Hermetic: `amount_total` 999 vs checkout 10.00 does not mint `RCPT-`” is a **ticked checklist without a test**. Code inspection says it would 400 and leave the checkout open. CI does not say so.

Currency: parse calls `TryNormalizeCurrency`. On failure it sets `Currency = null` (`string.IsNullOrEmpty(currency) ? null : currency`). Handler:

```
if (parsed.Currency is not null && !string.Equals(...))
    return 400 currency mismatch;
```

**Missing or non-3-letter currency skips the check** and can still fulfill. H14.1: “Missing currency → refuse, do not default MYR.” Live is **fail-open**. Hub parse is fail-closed (`AsUnusable()`). Steal Hub’s refuse. Do not invent MYR. Do not skip.

Happy-path fixture uses `"currency":"myr"` and checkout default `MYR`. Amount 1000 sen vs `amount: 10`. That path is tested. The mismatch path is not.

Fulfill still books **`checkout.Amount`**, not Stripe `AmountTotal`. After the compare, they should agree. A forged `amount_total` that **matches** the checkout still only pays that checkout’s amount. The remaining forge is “mark a **different** checkout paid,” which is the org bind + per-org `whsec_` problem.

### 7.5 Per-org `whsec_` vs process `Pay:StripeWebhookSecret`

014 B1: process secret is the production-BYOK hole. 015 H10/H11/P12:

- PUT stripe requires `secret` **and** `webhook_secret` (`GatewayEndpoints`: empty webhook → 400 `"webhook_secret is required"`). Test: `GatewayTests.Put_requires_webhook_secret`.
- Both wrapped with `SecretBox`. `last4` is API key last 4, not the `whsec_`. GET never echoes plaintext. Test: `Put_and_get_does_not_echo_secret`. That test does **not** assert `webhook_configured: true` even though `GatewayJson` emits it. P12.3 asked for that assert.
- `ResolveSecret`: row ciphertext wins; else non-Production process env; else null → 503.
- `.env.example`: “Dev fallback only; Production uses per-org webhook_secret.”
- Host README: same sentence.

What is **not** proven:

1. **Row vs process when they differ.** `WebhookTests` seed PUT uses `"webhook_secret":"whsec_test_local"` and signs with `factory.StripeWebhookSecret` which **defaults to the same string**. A bug that preferred process env would still pass. H10’s SoT claim is untested.
2. **Production 503 when only process env is set.** Factory uses `UseEnvironment("Testing")`. `IsProduction()` is false. The Production branch of `ResolveSecret` is never executed in CI.
3. **`Staging` / `Production` typo.** `!env.IsProduction()` is broader than H11’s “Testing or Development.” An ASPNETCORE_ENVIRONMENT of `Staging` verifies with the platform secret if the row is empty.
4. **Cross-org different secrets.** `Cross_org_checkout_is_400` PUTs t2 with the **same** `whsec_test_local` as t1, then signs with the factory secret, then posts t1’s checkout id to `/v1/webhooks/stripe/t2`. That tests **org bind**, not **org-scoped HMAC**. An attacker who holds t2’s `whsec_` still cannot pay t1 (400). An attacker who holds the **process** secret in Testing **and** a row with empty ciphertext **can** forge every such org.

PUT always writes ciphertext, so empty-row is not the merchant path. The merchant path **is** per-org after paste. Dogfood with one pasted `whsec_` is BYOK on verify. Production N merchants with N Dashboard endpoints is BYOK **if they paste**. Process env is a footgun for anyone who deploys Testing/Staging with a shared secret and then nulls ciphertext.

Hub: `TenantPaymentConfiguration` always had both `ApiKey` and `WebhookSecret` per `(tenant, gateway)`. Missing webhook secret throws. No process-env fallback. Steal Hub’s “no fallback in production.” Pay implemented that as `IsProduction()` only.

`SecretBox` Production: missing `Pay:WrapKey` throws. Testing/Development: SHA256 of `"lazuar-pay-dev-wrap-key"`. H16 landed in source. That wraps `sk_` **and** `whsec_`. A production host without wrap key fails closed. A Testing host encrypts with a git-known string — acceptable for CI, not for a laptop that then uses live keys.

Hub cashier `KEY_MODE_MISMATCH` is still not on Pay:

```149:164:apps/lazuar-api/Modules/Payments/Application/Services/CheckoutSessionCashier.cs
    internal static void EnsureKeyModeMatchesGateway(bool? requestIsTestMode, string plainApiKey)
    {
        ...
        var k2Test = k2.StartsWith("sk_test_", StringComparison.OrdinalIgnoreCase);
        var k2Live = k2.StartsWith("sk_live_", StringComparison.OrdinalIgnoreCase);
        ...
                    ? "Test API key cannot charge a live gateway credential (KEY_MODE_MISMATCH)."
                    : "Live API key cannot charge a test gateway credential (KEY_MODE_MISMATCH).",
```

Pay stores `Environment` test|live on PUT (default `"test"`) and never compares it to `sk_live_` / `sk_test_` at start. Steal this before the first friend-pays with a mixed Dashboard.

### 7.6 Event id grain

Pay unique key: `(OrgId, Provider, EventId)` on `psp_webhook_events`. `EventId = stripeEvent.Id` from `ConstructEvent` — Stripe `evt_…`. Never a Guid. Never `cs_…`. Never `pi_…`.

Ignored events **consume** that grain (H15 policy A). Stripe event ids are unique per event object, so a setup `evt_` will never later be a payment `evt_`. CHIP fail-then-pay must namespace; Stripe must not reuse CHIP’s `paid:{id}` trick for Sessions.

Hub additionally has business key `PAYMENT_COMPLETED:{pi}`. Pay does not. Consequences:

- Two Stripe events for one capture (`checkout.session.completed` + `payment_intent.succeeded`) are two `evt_` rows. Today the PI row is **ignored** (no second journal). If PI is later honored without a collapse key, **two journals** unless checkout status `!= open` saves you. Status no-op prevents a second `RCPT-`. It does **not** prevent two Stripe captures from two Sessions (double-start).
- Refunds Hub-style (`EventId = re_…`) are not implemented. If someone uses `evt_` for a refund later, PI collapse must stay off for refunds.

Find-then-insert race: two deliveries, both miss Find, both insert. One `DbUpdateException` → 200 duplicate, **no fulfill on the loser**. Winner fulfills once **if** the unique constraint holds. Postgres PK is `(OrgId, Provider, EventId)`. InMemory: `TransactionIgnoredWarning` swallowed; unique may or may not throw the same way. Serial replay is the proof that exists.

H15.3 asked for “Policy written in the handler comment (two sentences max).” `WebhookEndpoints.cs` and `StripeWebhook.cs` have **no** such comment. Checklist tick is a lie. Policy is visible only in this paper and in the ignore JSON.

Fulfill throw after insert, same TX: rollback should drop the event row; handler does not catch generic Exception, so ASP.NET 500; Stripe retries. That is H25. There is no test double for a throwing `Fulfillment`. `FulfillPaidAsync` returning early (missing checkout, amount <= 0, status not open) does **not** throw, so the event row **commits** and future retries are `duplicate` without a receipt. The org-bind 400 happens **before** insert, so a missing checkout is retry-poison rather than a stuck unique row. A checkout that exists, passes amount match, then is flipped non-open by a concurrent request could consume grain without a document — the winner should have paid it.

---

## 8. How Stripe success becomes `paid` — `Fulfillment.cs`

Webhook calls `FulfillPaidAsync(checkoutId, "stripe", session.Id, ct)` **in the same HTTP request**, inside the webhook’s transaction. No outbox. No `GatewayPaymentCompletedIntegrationEvent`. Same-handler at process **and** (intended) DB level.

```6:119:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
public sealed class Fulfillment(PayDbContext db)
{
    public async Task FulfillPaidAsync(string checkoutId, string provider, string? providerRef, CancellationToken ct)
    {
        var checkout = await db.Checkouts.FirstOrDefaultAsync(x => x.Id == checkoutId, ct);
        if (checkout is null) return;
        if (checkout.Amount <= 0) return;
        if (checkout.Status != "open") return;

        checkout.Status = "paid";
        db.Charges.Add(new ChargeRow { ..., Provider = provider, ProviderRef = providerRef, Amount = checkout.Amount, ... });
        // optional payer from checkout name/email
        // if Interval is mo/yr → Pay subscriptions row (not Stripe Billing)
        // journal cash D + revenue C, amount = checkout.Amount. No fee. No tax.
        // RCPT-{MalaysiaYear}-{n:00000}, title "Official Receipt"
        // audit checkout.paid
        await db.SaveChangesAsync(ct);
    }
}
```

014 described an inner `BeginTransactionAsync` and an SST fail-closed throw. **Both are gone.** SST column remains unused. Title is Official Receipt. Balanced two-line journal. Checkout create still hard-codes `Interval = "one_off"`, so the Pay subscription insert is dead on the current create path. Legal under `NP-XX-012` **if** the clock is Pay’s. Today it never fires from Stripe hosted pay.

`FulfillPaidAsync` still loads checkout **by id only**. Org check is the handler’s job. If a future caller skips the handler, the old global-fulfill bug returns. Passing `orgId` into fulfill would make H13 a type, not a convention.

`ProviderRef = session.Id` (`cs_`). Charge row cannot `IssueRefundAsync` Hub-style without a retrieve.

No fee line. `NP-MON-002`: do not print “Stripe fee RM0.00”. Unknown ≠ 0.

---

## 9. BYOK paste — `GatewayEndpoints` Stripe fields

`PUT /v1/orgs/{orgId}/gateway` + GET. Writer (owner/admin via `/me` role). Member GET sees `last4` + `configured`. Body `provider` allow-list of five lowercase names. For **every** rail including Stripe: `secret` required, `webhook_secret` required. Stripe **rejects** `public_merchant_id` (`AllowsPublicMerchantId` is Chip/Billplz only). `environment` defaults to `test`; must be `test` or `live` if sent. Stripe does not require environment the way Billplz does.

Protect both secrets. Audit `gateway.credentials.upsert` in the same SaveChanges. Sets `org_settings.active_provider`. GET never returns ciphertext. Capability `"hosted_link"`. `webhook_configured` is `!string.IsNullOrWhiteSpace(row.WebhookCiphertext)`.

Tests: member 403 PUT; PUT without webhook_secret 400; PUT+GET no plaintext, capability hosted_link, audit row, active_provider stripe. Missing: `webhook_configured: true` assert; `environment` round-trip; last4 of `sk_test_dummy` is `ummy`.

This is the Hub ops “paste both” judgment, minus MediatR and minus CHIP registrar auto-register. Steal paste-both. Refuse silent Stripe Webhook Endpoints API on PUT (015 parked CHIP registrar; do not invent a Stripe registrar here either).

---

## 10. Stripe.net versions and ConstructEvent

| Tree | Package | Version |
|------|---------|---------|
| New Pay `Lazuar.Pay.csproj` | `PackageReference Include="Stripe.net" Version="48.0.0"` | **48.0.0** (pinned on the host; no CPM) |
| Hub `Directory.Packages.props` | `PackageVersion Include="Stripe.net" Version="48.0.1"` | **48.0.1** |
| Hub Infrastructure csproj | versionless, CPM | 48.0.1 |

Pay: `ValidateSignature` **then** `ConstructEvent(..., throwOnApiVersionMismatch: false)`. Tests sign `api_version: "2024-06-20"`. Hub tests use `2025-03-31.basil` and ConstructEvent with library default **true**. Pay’s `false` is why the older fixture still constructs. Steal: pin Dashboard endpoint API version **or** keep `false` and accept thinner objects. Do not silently parse Billing objects as cash either way — Pay’s type allow-list (only `checkout.session.completed`) is the real fence, not the API version flag.

Hub does **not** call `ValidateSignature` separately. ConstructEvent verifies. Pay double-verifies. Harmless. Missing `Stripe-Signature`: `TryGetValue` false, `sig.ToString()` empty, StripeException → 400 `invalid signature`. Hub returns a dedicated “Missing Stripe-Signature header.” Pay lumps missing and bad together. 400 either way. Untested missing-header case (Invalid_signature sends junk, not absence).

`IHeaderDictionary.TryGetValue` is case-insensitive. Hub’s explicit case-insensitive scan is equivalent. Stolen in effect.

---

## 11. 015 H10–H25 vs live (honesty table)

Checklists are ticked. Live vs the **exit criteria**, not vs the box.

| ID | Claimed exit | Live code | Live test | Honesty |
|----|--------------|-----------|-----------|---------|
| **H10** | Org row is SoT for Stripe verify | Row ciphertext first. Process env only if row empty and not Production. | Seed PUT includes `webhook_secret`. Paid event 200 + `RCPT-`. **Does not** sign with a secret that differs from process env. | **Code yes, proof weak.** |
| **H11** | Production cannot verify with only process env | `IsProduction()` → null → 503. Non-Production fallback including Staging. | 503 when **both** empty in Testing. Production branch untested. | **Partial.** Staging hole. |
| **H12** | One TX unique insert + fulfill | Outer `BeginTransaction`; fulfill SaveChanges; no inner TX. | Replay after **success** is duplicate + one document. InMemory ignores transactions. | **Code yes. Postgres unproven.** |
| **H13** | `checkout.OrgId == path orgId`; metadata `org_id` if present must match | Checkout org bind **yes**. Metadata `org_id` **never read**. | `Cross_org_checkout_is_400`: t1 checkout posted to `/stripe/t2` → 400, zero documents. Same `whsec_`. | **Org bind yes. Metadata bullet no.** |
| **H14** | Amount + currency match; missing currency refuse; hermetic 999 vs 10.00 | Amount compare **yes**. Currency compare **only if parse emitted currency**. Missing currency **skip**. | **No mismatch test.** Happy path 1000 vs 10.00 only. | **Code partial. Test missing. Checklist overclaim.** |
| **H15** | Ignore setup/zero/unknown; policy comment; do not map setup_intent to paid; no Billing SoT | Ignore yes. Setup_intent not paid. No subscription mode. **No handler comment.** Policy A insert on ignore. | Setup + zero tests. **No** `payment_intent.succeeded` ignore test. **No** `customer.subscription.updated` test. | **Behavior yes. Comment no. Unknown-type untested.** |
| **H16** | Production wrap key required | `SecretBox.LoadKey` throws in Production if missing / not 32-byte | Indirect (tests run Testing with git hash). | **Code yes.** |
| **H17** | POST /v1/checkouts is writer | `RequireWriterAsync` | Not in `WebhookTests`; sibling checkout tests (out of this file’s full re-read of checkout tests). Stripe slice: create used by webhook seed with owner responder. | **Out of Stripe HTTP extract; assumed by seed.** |
| **H18** | Member 403 PUT gateway | `RequireWriterAsync` | `GatewayTests.Member_cannot_put_gateway` | **Yes.** |
| **H19** | Hermetic mode=setup is not paid | Parse `setup_or_zero` | `Setup_mode_is_ignored` — 200, ignored, 0 docs, checkout open | **Yes. This is the lock.** |
| **H20** | Hermetic amount 0 is not paid | Same branch | `Zero_amount_session_is_ignored` — 0 docs only | **Mostly. Weaker asserts.** |
| **H21** | Isolation bans Hub adapter types | `IsolationTests.BannedSrc` includes factory/port/event + `ApplicationFeeAmount` | IsolationTests | **Yes for named strings.** |
| **H22** | No Connect fee; grep ApplicationFeeAmount, application_fee, TransferData, transfer_data | Source has none. Isolation only greps `ApplicationFeeAmount`. | IsolationTests | **Absence yes. Test net thinner than Hub.** |
| **H23** | Audit on PUT | `gateway.credentials.upsert` | GatewayTests | **Yes.** |
| **H24** | Unique violation → 200 duplicate; comment names 23505 | `catch (DbUpdateException)` → duplicate. **No 23505 comment.** | Serial replay only. Concurrent optional and absent. | **Serial yes. Comment no.** |
| **H25** | Fulfill throw rolls back event id; 5xx; second delivery can pay | Same TX + uncaught throw → 500. Early `return` in fulfill does **not** rollback. | **No throwing-fulfill test.** H25.2 allowed skip. | **Code intended. Unproven. Early-return hole.** |
| **P12** | Stripe PUT requires sk_ and whsec_ | Both required, both wrapped | PUT without webhook_secret 400; GET no plaintext | **Yes. webhook_configured assert missing.** |
| **P16** | capability hosted_link | Constant for all five | GatewayTests stripe PUT | **Yes.** |
| **P19** | Stripe email optional | `RequiresEmail` is not Stripe | No Stripe-start-without-email test (start is untested for Stripe) | **Code yes.** |

---

## 12. Tests that actually exist for Stripe on new Pay

### 12.1 `WebhookTests.cs` — entire inventory (seven methods)

Hermetic: `PayApiFactory` in-memory EF, `UseSetting("Pay:StripeWebhookSecret", …)`, HMAC helper matching Stripe `t=…,v1=…`. No network to Stripe. IsolationTests still ban MediatR.

Seed: owner PUT `{ provider: stripe, secret: sk_test_dummy, webhook_secret: whsec_test_local }` then POST checkout `{ org_id: t1, amount: 10 }` (default MYR, one_off).

| Test | What it locks | What it does not |
|------|---------------|------------------|
| `Missing_webhook_secret_is_503_when_rail_configured` | After wiping **row** ciphertext, with factory process secret **also empty**, POST unsigned `{id:evt_x}` → **503** | Production-only; row-present vs process-different; signed body with empty secret |
| `Invalid_signature_is_400` | Junk `Stripe-Signature` on a completed-looking JSON → **400** | Missing header; body text `invalid signature`; wrong-secret but well-formed HMAC |
| `Completed_session_writes_receipt_and_replay_is_noop` | Signed `checkout.session.completed`, `mode=payment`, `amount_total=1000`, `currency=myr`, `client_reference_id` + metadata `checkout_id`/`org_id`, `payment_status=paid`. First 200, one `RCPT-`, balanced journal. Second 200 `duplicate`, still one document. | Amount mismatch; currency mismatch; `payment_status=unpaid`; PI id stored; org metadata vs path; row≠process secret |
| `Setup_mode_is_ignored` | `mode=setup`, amount 0, open checkout with amount 10 → 200 `ignored`, 0 docs, still `open` | `setup_intent.succeeded` as a **separate** type; Hub-style expanded SetupIntent object |
| `Zero_amount_session_is_ignored` | `mode=payment`, `amount_total=0`, `payment_status=paid` trap → 0 docs | Body `ignored`; checkout still open; `amount_total: null` |
| `Cross_org_checkout_is_400` | t1 checkout id posted to `/v1/webhooks/stripe/t2` (t2 has its own stripe row, **same** secret) → 400, 0 docs | Distinct `whsec_` per org; t1 still `open` explicitly (implied by 0 docs); metadata `org_id` mismatch when checkout org **does** match path |
| `Unknown_provider_is_400` | `/v1/webhooks/paypal/t1` → 400 | Empty body (lives in PublicPayTests) |

### 12.2 Elsewhere (Stripe-touching, not in WebhookTests)

| Test | File | Stripe relevance |
|------|------|------------------|
| `Empty_webhook_is_400` | `PublicPayTests` | POST empty body to `/v1/webhooks/stripe/t1` → 400. `NP-GW-005`. |
| `Member_cannot_put_gateway` | `GatewayTests` | Stripe JSON body, role member → 403. H18. |
| `Put_requires_webhook_secret` | `GatewayTests` | Stripe secret without `webhook_secret` → 400. P12. |
| `Put_and_get_does_not_echo_secret` | `GatewayTests` | Stripe PUT both secrets; GET capability `hosted_link`; no plaintext; audit upsert; active_provider stripe. |
| Isolation Hub-token + `ApplicationFeeAmount` | `IsolationTests` | Source grep, not a Stripe HTTP test. |
| Chip/Billplz/Xendit/Razorpay start+webhook | `RailTests` | **Zero** `stripe` string hits. Stripe start untested. |

### 12.3 Claimed by 015 / 014, **not** present

| Wanted | Status |
|--------|--------|
| H14 amount_total 999 vs 10.00 does not mint `RCPT-` | **Missing** |
| Currency missing refuse / currency mismatch | **Missing** |
| `payment_intent.succeeded` is ignored (not paid, not a second journal) | **Missing** |
| `customer.subscription.updated` / `invoice.paid` ignored | **Missing** |
| `setup_intent.succeeded` ignored (H15.2) | **Missing** as its own type; setup **session** is tested |
| `payment_status=unpaid` with amount_total 1000 is not paid | **Missing** |
| Row `whsec_org` vs process `whsec_process` — only org signature verifies | **Missing** |
| Production environment 503 on process-only secret | **Missing** |
| Org A event_id does not collide with org B (same `evt_`, two orgs) | Schema is composite PK so it **should** work; **untested** |
| Concurrent 23505 → 200 duplicate | **Missing** (H24 optional) |
| Throwing fulfill → 5xx, retry pays | **Missing** (H25 skip allowed) |
| `CreateHostedUrlAsync` options: mode payment, no Connect fee, card wrap, metadata keys, cents | **Missing** as a unit test of the options object. Isolation greps Connect fee **string**. |
| Stripe start 503 on bad key / 503 rail not configured | **Missing** (would need a fake StripeClient or a recorded HTTP layer) |
| Double-start two `cs_` | **Missing** |
| `KEY_MODE_MISMATCH` | **Missing** (feature missing) |
| Persist `ProviderRef` as `pi_` | **Missing** (feature missing) |
| `webhook_configured: true` on GET | **Missing** assert (property exists) |

Hub `StripeGatewayAdapterTests` is a **much** denser net: card-only wrap, Connect fee null, setup generate mode, setup parse as PAYMENT_COMPLETED (the lie), PI succeeded parse, unmapped `customer.updated` verified-passthrough, refund succeeded vs pending, off-session succeeded-only, fee expand known/unknown. Pay should steal the **generate** tests (card wrap, no Connect, mode payment) and the **ignore** tests (unmapped type, setup session). Pay must **not** steal the setup-as-PAYMENT_COMPLETED expectations.

---

## 13. Steal-list (HTTP judgment only)

Copy the **decision**. Not the class. Not MediatR.

### 13.1 Already stolen (keep)

1. `new StripeClient(orgSk)` per call — merchant key, never Lazuar’s, never `Stripe-Account`.
2. `SessionService.CreateAsync` **only** for hop-2. No Elements, no ephemeral keys.
3. `Mode = "payment"` for amount > 0. **Never** `mode=subscription`. Generate never setup.
4. `ClientReferenceId = checkout.Id`.
5. Session metadata `checkout_id` + `org_id`.
6. Success URL default includes `?status=verifying` (landing is not paid).
7. Persist `session.Id` onto `checkout.ProviderSessionId` at start (015).
8. Raw body; empty 400; unknown provider 400; rail not configured 400.
9. `EventUtility` HMAC verify; bad sig **400** (not Hub 500).
10. Per-org `whsec_` column + PUT requires it (015). Process env is fallback, not SoT in Production.
11. `checkout.session.completed` + not setup + amount > 0 → in-process `FulfillPaidAsync`.
12. `mode=setup` / zero `AmountTotal` → 200 `ignored: setup_or_zero`. No `RCPT-`.
13. Unknown Stripe types → 200 ignored, **not** paid. Billing stays non-SoT.
14. Unique `(org, stripe, evt_…)`. Replay 200 duplicate. Event id is Stripe’s, never a Guid.
15. `checkout.OrgId` must equal path `{orgId}` (015).
16. Amount minor compare when `AmountTotal` present (015).
17. One HTTP handler does journal + Official Receipt. No `GatewayPaymentCompletedIntegrationEvent`.
18. One intended DB transaction around unique insert + fulfill (015).
19. Unique-violation → 200 duplicate (015, serial-tested).
20. Capability `"hosted_link"`. No off-session JSON.
21. Connect application fee **absent**.
22. Stripe email optional on start.
23. Isolation bans Hub adapter types and `ApplicationFeeAmount`.
24. Writer-only PUT keys; member 403.
25. AES-GCM wrap; Production wrap key required.

### 13.2 Steal next (still gaps)

1. **`PaymentMethodTypes = ["card"]`.** Do not list `apple_pay` / `google_pay` / `fpx`. Wallets ride on `card`. Leaving it unset is how delayed methods sneak in. Hub tests exist; copy the **idea** into a Pay source grep or options unit test.
2. **`payment_status == paid`** when the object is a Session. Even with cards, async methods exist. Hub skipped this **because** of the wrap. Pay has neither.
3. **Copy metadata onto `PaymentIntentData`.** Keys: `checkout_id`, `org_id`. Needed before anyone honors `payment_intent.succeeded`.
4. **`CustomerEmail`** when start stored one.
5. **Product name** from catalog/checkout, not `"Pay"`.
6. **`GatewayCommon.ToMinorUnits` zero-decimal table** (or refuse non-MYR on checkout create). Always ×100 is a JPY footgun.
7. **Stripe `Idempotency-Key = "lazuar-checkout:" + checkout.Id`** on Session create so double-start does not mint two payable `cs_`.
8. **`KEY_MODE_MISMATCH`** against `sk_test_` / `sk_live_` vs `Environment`.
9. **Persist `pi_`** (`session.PaymentIntentId`) as charge `ProviderRef` (keep `cs_` on `ProviderSessionId`).
10. **Fail-closed missing currency** (Hub unusable). Do not skip the compare.
11. **Row vs process secret test** (sign with org A’s `whsec_`, process env a different value, assert A verifies and the process value does not).
12. **H14 mismatch test** (999 vs 10.00).
13. **Unknown-type ignore test** (`payment_intent.succeeded`, `customer.subscription.updated`).
14. **Card-wrap / no-Connect unit test** of `SessionCreateOptions` (Hub style, not MediatR).
15. Optional later: `payment_intent.succeeded` **only after** `paid:{pi}` unique **and** open payment-mode checkout **and** amount > 0. Do not add PI fulfill first.
16. Optional later: Hub PM extract on setup — persist `cus_`/`pm_`, **never** call `FulfillPaidAsync`. Parked-offsession.

### 13.3 Event types — honor / ignore (Pay policy, after steal)

**Cash, this program:**

- `checkout.session.completed` where `Mode == "payment"` and `AmountTotal > 0` **and** (steal) `PaymentStatus == "paid"`. Lookup by `ClientReferenceId` / `metadata.checkout_id`. Book Pay’s checkout amount. Bind org. Match amount/currency fail-closed.

**Not paid, 200 ignore (insert grain):**

- `checkout.session.completed` + setup or zero — already.
- `setup_intent.succeeded` — ignore (do **not** Hub-name it `PAYMENT_COMPLETED`).
- `payment_intent.succeeded` — **ignore until** dual-event key exists.
- `payment_intent.payment_failed` — ignore on one-off hosted_link; later decline codes.
- `charge.dispute.*` — ignore until a dispute table.
- `refund.updated` / `charge.refunded` — ignore until refunds.
- `customer.subscription.*`, `invoice.*`, `customer.updated`, `payment_intent.created`/`processing`, `charge.succeeded`, `billing_portal.*`, Radar, payouts, accounts — ignore. `NP-XX-012`.

If a merchant enables Stripe Billing in their Dashboard independently, those events may hit the same endpoint. **Ignore them.** Pay’s clock is Pay.

---

## 14. Refuse-list (must never be copied)

1. **`Modules/Payments` as a project.** `AddPaymentsModule`, `PaymentsDbContext`, `IPaymentGatewayAdapter`, `PaymentGatewayFactory`. IsolationTests are the tripwire.
2. **MediatR `ProcessGatewayWebhookCommand`.** New host already does not.
3. **`GatewayPaymentCompletedIntegrationEvent` + Payments outbox + Commerce inbox.** Fulfillment is the webhook handler.
4. **`HandleExistingLogAsync` / Dead outbox requeue.** Exists because Hub ACKs before Commerce. Same-TX fulfill deletes the need. If you ACK before fulfill, you will reinvent it.
5. **Stripe Billing as SoT:** `customer.subscription.updated`, `invoice.paid`, Checkout `mode=subscription`, `Stripe.Subscription` / `Price` / `Coupon`, Billing Portal as Pay’s buyer portal. `NP-XX-012`.
6. **`setup_intent.succeeded` or setup-mode `checkout.session.completed` mapped as `PAYMENT_COMPLETED`.** Hub tests lock the lie. Steal extract; refuse the name. `NP-GW-008`.
7. **Stripe Connect `application_fee_amount` / `TransferData` / `Stripe-Account` / connected accounts as Lazuar’s tenant model.** Hub tests ban the strings. 007 `LP-XX-007`. 013 standing law 1.
8. **Platform / system org checkout** (`ApplyPayingTenantMetadata` for Hub SaaS fee, `SystemOrganizationId`). New Pay is not billing Hub.
9. **BILLPLZ last-resort** when Stripe keys are missing (`CheckoutSessionCashier` `return "BILLPLZ"`). Start already 503s `rail not configured`.
10. **`DecryptOrPlaintext`.** Wrong wrap key must not send ciphertext to Stripe as Bearer. Pay `SecretBox.Unprotect` throws.
11. **`Jwt:Secret` as KMS.** Do not add a JWT fallback to `SecretBox`.
12. **Vite `sk_live_` / `whsec_`.** Merchant 5178 / checkout 5179 hold origins only.
13. **Homemade FPX e-mandate** / Stripe FPX as silent debit. Card wrap is enough. `SupportsEmandate` stays false.
14. **Elements / Payment Element / ephemeral keys / publishable key in Pay.** Wrap is hosted Checkout. PCI SAQ-A stays the PSP’s page.
15. **Off-session `ChargeOffSessionAsync` as the first `RCPT-`.** Parked. Adapter `true` is not paid.
16. **Hub signature-fail 500.** Pay already 400s. Keep 400.
17. **Invented event ids** (Guid fallback).
18. **Wait for One** to ACK money or grant buyer access.
19. **Fee = 0 meaning “Stripe charged 0 MDR.”** Stamp unknown. Do not add a fee journal line of 0.
20. **ngrok as staging/prod origin.**
21. **Customer Portal “Copy Portal Link” as update-payment.**
22. **Porting Hub `taxRate` / `TaxAmount` / `sst_tax_amount` metadata.** 015: no tax.
23. **Silent Stripe webhook endpoint registrar** on PUT (CHIP registrar analogue). Paste `whsec_`.
24. **Treating 015 `[x]` as proof.** This paper exists because ticks lied on H14 tests, H13 metadata, H15 comment, H24 comment, H10 row-vs-process.

---

## 15. Side-by-side: generate options (short table, then the verdict)

Hub payment session (judgment):

- mode `payment`
- card-only PM types
- metadata on session **and** PI
- `tenant_id` / optional `platform_tenant_id`
- customer email
- product name
- `UnitAmountDecimal` via `ToMinorUnits` (zero-decimal table)
- success/cancel from caller (Commerce public HTTPS)
- optional `setup_future_usage=off_session` + `customer_creation=always`
- `ApplicationFeeAmount` null, tested
- no Stripe idempotency key on create (Hub also missing — steal a **better** create than Hub here)

Pay payment session (living `c621ceba`):

- mode `payment` ✓
- **no** PM types ✗
- metadata on session only: `checkout_id`, `org_id` ✓ keys, ✗ PI copy
- `ClientReferenceId` ✓
- **no** customer email ✗
- product name `"Pay"` ✗
- `UnitAmount = round(amount*100)` — MYR ok, JPY not
- success/cancel from checkout **or localhost:5179**
- no setup_future_usage ✓ for hosted_link
- no Connect fields ✓
- returns and persists `cs_` ✓ (015)
- no `KEY_MODE_MISMATCH` ✗
- no create idempotency ✗

Hub setup session (do not book as cash; do not generate on Pay):

- mode `setup`
- SetupIntent metadata
- customer_creation always
- card-only

Pay: **does not generate setup.** Webhook still fences it if a merchant-created setup Session somehow posts. Good defense. Tested.

---

## 16. Production-ready Stripe on new Pay: **no**

**No** for production multi-merchant live Stripe.

Not because the host still has MediatR (it does not). Not because it books `setup_intent` as paid (it does not — and H19 now **tests** that). Not because verify is still a single process `whsec_` as SoT (015 moved SoT to the org row when ciphertext is present). Not because it takes an application fee (it does not; Isolation greps the Connect string). Those are Hub sins new Pay actually avoided, and 014’s two blockers are **source-addressed**.

It is not production-ready because:

1. **Dashboard dynamic PMs + no `payment_status` check** can mark unpaid Sessions paid. Hub’s card wrap exists to prevent that class of lie. Pay dropped the wrap and did not add the status check. This is 014 H1/H2, still live, still **high**. First non-author charge with a merchant who enabled FPX/bank in the Stripe Dashboard is the incident.
2. **Two payable Checkout Sessions per Pay checkout** on double-start (no Stripe idempotency key; `PspRedirectUrl` overwritten; both `cs_` remain payable). Pay fulfills the first webhook; the second is the same checkout id so status `paid` no-ops — **but Stripe still captured twice on the merchant account.** Merchant support nightmare, not a double `RCPT-`. 014 H4, still live.
3. **No `sk_test_` / `sk_live_` vs `Environment` guard.** A pasted live key on a laptop dogfood charges live cards. Hub cashier 409s. Pay will happily `SessionService.CreateAsync` with `sk_live_`. 014 H5, still live.
4. **`ProviderRef = cs_…`.** Refunds, disputes, dual-event keys want `pi_`. Storing only `cs_` makes later `IssueRefundAsync` judgment unusable without a retrieve. 014 H7, still live.
5. **Create options untested in CI.** Stripe.net is a real HTTP client. A future “helpful” `Mode = "setup"` on generate would only be caught if a webhook fixture still used an open checkout — generate itself would not fail a test. Card wrap cannot regress-fail because it was never asserted.
6. **H14 amount mismatch is untested.** Code looks right. Production money compares should not be “looks right.”
7. **H10 row-vs-process is untested.** Production BYOK verify is a comment and a branch. Tests use identical secrets.
8. **Missing currency fail-open.** Hub refused to invent MYR. Pay skips the compare.
9. **Always ×100.** Fine while every checkout is MYR. Checkout create accepts any 3-letter string.
10. **Default success/cancel `http://localhost:5179`.** A production checkout minted without URLs sends the buyer to a laptop origin after paying. Fail closed or require public HTTPS.
11. **One-TX / rollback unproven on Postgres.** InMemory ignores transactions. H25 throwing-fulfill untested. Early return in fulfill after insert commits a unique row with no receipt (status-not-open / amount<=0).
12. **Stripe.net 48.0.0 vs Hub 48.0.1** + `throwOnApiVersionMismatch: false` + fixtures on `2024-06-20`. Thin events / missing fields. Pin deliberately.
13. **Metadata `org_id` unused** despite H13.1 last bullet. Org bind is checkout-row only. Good enough if checkout ids are unguessable ULIDs/Guids **and** `whsec_` is per org. Still not what the checklist wrote.
14. **Cross-org 400 retry poison.** Safer than fulfilling the wrong org. Noisier than 013’s 200 ignore. Acceptable if documented; today it is a tick that said “400 or 404” without an ops note.

**Narrow dogfood yes**, if all of these are true at once:

- One org (or N orgs each of which **pasted** their own `whsec_` on PUT — not relying on `Pay__StripeWebhookSecret`).
- Stripe **test** mode, `sk_test_`, `environment=test`.
- Operator creates **one** Dashboard endpoint `https://<public-https>/v1/webhooks/stripe/{thatOrgId}` subscribed to `checkout.session.completed` (other types 200 ignore).
- Cards only in that Stripe account **or** you add the card allow-list before the first friend-pays.
- Checkout `SuccessUrl` / `CancelUrl` are the tunnel/Vite **HTTPS** origins, not forgotten localhost.
- `Pay__WrapKey` set (or accept git hash on a laptop **test** key only).
- You do not double-click start (or you expire extra Sessions by hand).
- You watch Dashboard for 400 storms (amount mismatch, cross-org, garbage reference).

That is Bar B on a laptop with a tunnel. It is not “production BYOK Stripe.”

014’s closer still applies, updated: **do not add `payment_intent.succeeded` fulfill until** dual-event `paid:{pi}` unique exists **and** PI metadata is copied at create **and** setup/zero/unpaid fences apply to the PI path. **Do not** add Billing Portal, Connect fees, `mode=subscription`, or off-session in the same change as card wrap + payment_status + create idempotency.

---

## 17. Quote appendix — Hub setup-as-paid (the lie not to copy)

```659:697:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs
    internal static GatewayWebhookParsedResult? TryMapSetupIntentSucceeded(Event stripeEvent)
    {
        if (stripeEvent.Type != "setup_intent.succeeded")
        {
            return null;
        }
        ...
        return new GatewayWebhookParsedResult(
            true, "PAYMENT_COMPLETED", stripeEvent.Id, 0, "", si.Id, meta, 0, 0, 0, 1, "",
            null, si.CustomerId, token);
    }

    internal static GatewayWebhookParsedResult RefuseSetupSessionWithoutToken(...) =>
        new(
            false, "PAYMENT_COMPLETED", eventId, 0, currency ?? "", transactionId, meta, 0, 0, 0, 1, currency ?? "",
            "Setup session missing payment method.",
            customerId, null);
```

Pay’s corresponding fence (the steal of the **lock**, not the Hub name):

```42:55:apps/lazuar-pay/src/Lazuar.Pay/Gateways/StripeWebhook.cs
        if (stripeEvent.Type is not "checkout.session.completed")
        {
            return new PspParseResult { EventId = stripeEvent.Id, Ignored = true, IgnoreReason = stripeEvent.Type };
        }
        ...
        if (session.Mode == "setup" || session.AmountTotal is null or 0)
        {
            return new PspParseResult { EventId = stripeEvent.Id, Ignored = true, IgnoreReason = "setup_or_zero" };
        }
```

Hub Connect refuse (steal the test idea):

```190:216:apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/StripeGatewayAdapterTests.cs
    public void PaymentAdapters_DoNotSetConnectApplicationFeeOrTransfer()
    {
        ...
            src.Should().NotContain("ApplicationFeeAmount");
            src.Should().NotContain("application_fee");
            src.Should().NotContain("TransferData");
            src.Should().NotContain("transfer_data");
    }
```

Pay Isolation fragment (thinner):

```6:11:apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
    static readonly string[] BannedSrc =
    [
        "MediatR", "Modules.One", "BuildingBlocks", "IPaymentGatewayAdapter", "PaymentGatewayFactory",
        "IPaymentGatewayFactory", "AddPaymentsModule", "GatewayPaymentCompletedIntegrationEvent", "Modules.Payments",
        "ApplicationFeeAmount", "Razorpay.Api"
    ];
```

Hub unmapped type stays the Stripe type (handler then drops with no log):

```621:648:apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/StripeGatewayAdapterTests.cs
    public async Task ParseWebhook_UnmappedType_IsVerifiedWithStripeType()
    {
        ...
              "type": "customer.updated",
        ...
        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("customer.updated");
        result.EventId.Should().Be("evt_unmapped");
    }
```

Pay equivalent is untested; behavior is `Ignored = true`, `IgnoreReason = "customer.updated"`, unique insert, 200.

---

## 18. What this slice is not

This file does not score CHIP / Billplz / Xendit / Razorpay HTTP. Those are 05–08. It does not score `:5178` / `:5179` field names (02 / 03). It does not implement card wrap, payment_status, or tests. It does not flip 011 tracker cells.

Live files win over 014. 014 wins over 008. 015 checklist ticks do not win over `WebhookTests.cs`.

**Stripe on 8081 is a real hosted_link rail with BYOK `sk_` + pasted `whsec_`, same-handler Official Receipt, setup-not-paid tested, Connect fee absent, Billing not SoT.** It is **not** Hub’s adapter, and it should not become one. It is **not** done.
