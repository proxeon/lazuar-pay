# 04 — Payments adapters, capabilities, inbound webhooks

**Date:** 17 August 2026  
**Branch:** `feat/007-waves-1-4-implement` (`297ba98`)  
**Slice:** Payments module only — Stripe / Billplz / CHIP / Razorpay / Xendit adapters, `PaymentGatewayCapabilities`, inbound webhook verify + `PaymentWebhookLog` unique `(Provider, EventId)`, off-session charge, refunds at adapter layer, wallets, setup mode, credential use.  
**Out of scope:** Commerce subscription state after the event is published (01–03), Billing ledger booking (05), ops UI forms (09).  
**Authority:** source as compiled on this branch after `a1afc09` (EventId namespace) and `8b3567d` (Stripe `$0` setup mode). Workspace HEAD when this report was written is `30d07d2` (docs-only after `297ba98`); every Payments `.cs` path quoted below is identical at `297ba98`.  
**This is not a rewrite of `plans/008-evals/02-payments-adapters-rails.md`.** 008 named bugs against the pre-fix tree. This file re-reads the tree **now**. A 008 P0 is closed only when the current code no longer contains it.

Do not implement. Do not condense.

---

## 0. Files read

Primary trees: `apps/lazuar-api/Modules/Payments/` and `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/`.

| Path | Role |
|------|------|
| `Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs` | Port: generate, parse, refund, portal, off-session |
| `Modules/Payments/Contracts/PaymentGatewayCapabilities.cs` | Honest matrix (off-session, refund, QR, wallets, e-mandate) |
| `Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` | Stripe Checkout + webhook + refund + off-session + portal |
| `Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` | CHIP purchases + RSA webhook + charge/refund |
| `Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs` | Billplz bills + HMAC form callback |
| `Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs` | Payment link / card registration + HMAC webhook |
| `Modules/Payments/Infrastructure/Gateways/XenditGatewayAdapter.cs` | Hosted invoices + callback-token webhook |
| `Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs` | Name/email defaults, rounded vs truncating minor units |
| `Modules/Payments/Infrastructure/Gateways/PaymentGatewayFactory.cs` | Uppercase lookup, throw if unknown |
| `Modules/Payments/Infrastructure/Gateways/BillplzPublicBase.cs` | Public HTTPS callback + test/live host |
| `Modules/Payments/Infrastructure/Gateways/PublicDnsFallback.cs` | 1.1.1.1 / 8.8.8.8 A-record fallback for Billplz |
| `Modules/Payments/Infrastructure/Endpoints.cs` | `POST /webhooks/payments/{gateway}/{tenantId}` |
| `Modules/Payments/Infrastructure/IntegrationEndpoints.cs` | M2M checkouts + `/me` |
| `Modules/Payments/Infrastructure/PlatformEndpoints.cs` | Platform payment-config GET/PUT |
| `Modules/Payments/Infrastructure/DependencyInjection.cs` | Five adapters + factory + outbox/inbox jobs |
| `Modules/Payments/Application/Commands/ProcessGatewayWebhookCommand.cs` | Command record |
| `Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` | Verify → filter → log → publish |
| `Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.Idempotency.cs` | Business key + 23505 swallow |
| `Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.Logging.cs` | Intake ACK log |
| `Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.Metadata.cs` | Session metadata merge |
| `Modules/Payments/Application/Commands/CreateIntegrationCheckoutCommandHandler.cs` | M2M create |
| `Modules/Payments/Application/Services/CheckoutSessionCashier.cs` | Shared generate path |
| `Modules/Payments/Application/Services/CheckoutAmountRules.cs` | M2M min amount / ISO currency |
| `Modules/Payments/Application/Services/IntegrationCheckoutMetadata.cs` | Stamp / fingerprint |
| `Modules/Payments/Application/Queries/GenerateCheckoutSessionQueryHandler.cs` | Legacy string URL (Commerce hop 2) |
| `Modules/Payments/Application/Queries/GenerateCheckoutSessionDetailedQueryHandler.cs` | Rich result |
| `Modules/Payments/Application/Queries/GenerateSystemCheckoutSessionQueryHandler.cs` | Platform / Hub SaaS / credits |
| `Modules/Payments/Application/Queries/GenerateCustomerPortalQueryHandler.cs` | Stripe-only portal |
| `Modules/Payments/Application/Queries/GetIntegrationCheckoutQueryHandler.cs` | M2M GET + lazy expire |
| `Modules/Payments/Application/Queries/GetPaymentsMeQueryHandler.cs` | M2M `/me` |
| `Modules/Payments/Application/OffSessionDeclinedException.cs` | Stripe decline-code exception |
| `Modules/Payments/Application/Exceptions/PaymentIntegrationException.cs` | M2M ProblemDetails codes |
| `Modules/Payments/Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs` | Inbox → adapter charge |
| `Modules/Payments/Infrastructure/EventHandlers/GatewayRefundRequestedIntegrationEventHandler.cs` | Inbox → adapter refund |
| `Modules/Payments/Infrastructure/EventHandlers/IntegrationCheckoutGatewayEventsHandler.cs` | M2M session + outbound `payment.*` |
| `Modules/Payments/Infrastructure/Commands/UpdatePaymentConfigCommandHandler.cs` | Encrypt + CHIP auto-RSA + webhook register |
| `Modules/Payments/Infrastructure/Queries/GetPaymentConfigQueryHandler.cs` | Masked DTO, never returns secrets |
| `Modules/Payments/Infrastructure/Repositories/PaymentRepositories.cs` | Config + webhook log (EventId **not** tenant-scoped) |
| `Modules/Payments/Infrastructure/Repositories/IntegrationCheckoutSessionRepository.cs` | M2M session by id / idempotency / provider session |
| `Modules/Payments/Infrastructure/Configurations/PaymentConfigurations.cs` | Unique `(Provider, EventId)` and `(Provider, BusinessKey)` |
| `Modules/Payments/Infrastructure/PaymentsDbContext.cs` | `payments` schema |
| `Modules/Payments/Domain/Entities/PaymentWebhookLog.cs` | EventId + Provider + BusinessKey + OutboxMessageId |
| `Modules/Payments/Domain/Aggregates/TenantPaymentConfiguration.cs` | Encrypted keys, `IsActive`, `Environment` |
| `Modules/Payments/Domain/Aggregates/IntegrationCheckoutSession.cs` | `open` / `completed` / `failed` / `expired` |
| `Modules/Payments/Domain/PaymentGatewayEnvironment.cs` | `test`/`live`, Stripe-shaped key infer |
| `Modules/Payments/Contracts/PlatformCheckoutTypes.cs` | `utility_credit_topup` / `platform_saas_fee` / system org |
| `Modules/Payments/Contracts/Events/*.cs` | Completed / failed / dispute / refund / off-session |
| `Modules/Payments/Infrastructure/Migrations/20260627124811_InitialPaymentsSchema.cs` | Unique `(Provider, EventId)`, EventId is `text` |
| `Modules/Payments/Infrastructure/Migrations/20260803151832_AddPaymentWebhookBusinessKey.cs` | Unique `(Provider, BusinessKey)` filtered |
| `Modules/Payments/Infrastructure/Migrations/20260816235900_AddPaymentWebhookOutboxMessageId.cs` | Outbox correlation |
| `Modules/Payments/Infrastructure/Workers/PaymentsInboxConsumerJob.cs` | Inbox worker |
| `Modules/Payments/Infrastructure/Workers/PaymentsOutboxPublisherJob.cs` | Outbox worker |
| `Modules/Payments/README.md` | Stale: four adapters, “stateless checkouts” |
| `BuildingBlocks/Infrastructure/OutboxEventBus.cs` | `PublishAsync` only `Add`s; save is the handler’s `SaveChanges` |
| `tests/Lazuar.ModuleTests/Payments/*.cs` | Twenty test fixtures (listed in §8) |
| `plans/008-evals/02-payments-adapters-rails.md` | Prior eval; EventId collision was P0 there |
| CHIP Collect official callbacks page (`docs.chip-in.asia/chip-collect/overview/callbacks`) | `skip_capture=true` success callback only on **capture** |

Not read as authority: ops/admin TSX (slice 09), Commerce handlers after publish (slices 01–03), Billing ledger (slice 05). Commerce `InitiateCheckoutCommandHandler` `$0` branch is cited only as the **caller** that now drives Stripe setup mode and CHIP `skip_capture`.

---

## 1. Mechanics (how money enters this module)

Lazuar Pay is a BYOK cashier. The tenant’s Stripe / CHIP / Billplz / Razorpay / Xendit account settles. This module’s job is:

1. Mint a hosted hop-2 URL (`GenerateCheckoutAsync`).
2. Verify the processor webhook and map it to `PAYMENT_COMPLETED` / `PAYMENT_FAILED` / `DISPUTE_CREATED`.
3. Persist `PaymentWebhookLog` under unique `(Provider, EventId)` and unique `(Provider, BusinessKey)` when a transaction id exists.
4. Publish `GatewayPayment*` / `GatewayDispute*` onto the Payments outbox **in the same `SaveChanges` as the log**.
5. On Commerce request: run an off-session charge (Stripe / CHIP only, by capability) or an API refund (Stripe / CHIP / Razorpay / Xendit).

The factory after Waves 0–4 still registers five adapters:

```34:39:apps/lazuar-api/Modules/Payments/Infrastructure/DependencyInjection.cs
        services.AddScoped<IPaymentGatewayAdapter, StripeGatewayAdapter>();
        services.AddScoped<IPaymentGatewayAdapter, BillplzGatewayAdapter>();
        services.AddScoped<IPaymentGatewayAdapter, RazorpayGatewayAdapter>();
        services.AddScoped<IPaymentGatewayAdapter, ChipCollectGatewayAdapter>();
        services.AddScoped<IPaymentGatewayAdapter, XenditGatewayAdapter>();
        services.AddScoped<IPaymentGatewayFactory, PaymentGatewayFactory>();
```

`PaymentGatewayFactory.GetAdapter` uppercases and throws `InvalidOperationException` if nothing matches (`PaymentGatewayFactory.cs:14-24`). There is no Fiuu, Midtrans, Cashfree, SenangPay, or PayPal class.

The inbound allow-list is the same five names (`Endpoints.cs:13-20`). M2M create allow-lists the same five (`CreateIntegrationCheckoutCommandHandler.cs:17-20`).

`IPaymentGatewayAdapter` is one port for all five rails (`IPaymentGatewayAdapter.cs:27-76`):

| Method | Meaning |
|--------|---------|
| `GenerateCheckoutAsync` | Hosted hop-2. Returns URL + provider session id. |
| `ParseWebhookAsync` | Verify + map. Returns `GatewayWebhookParsedResult`. |
| `IssueRefundAsync` | POST a processor refund. `bool` only — no refund id, no fee reclaimed. |
| `GenerateCustomerPortalAsync` | Stripe Billing Portal or throw. |
| `ChargeOffSessionAsync` | Merchant-initiated charge against a stored token. |

`GatewayWebhookParsedResult` (`IPaymentGatewayAdapter.cs:10-25`) carries `Verified`, `EventType`, `EventId`, amounts, `GatewayTransactionId`, metadata, optional `GatewayCustomerId` / `GatewayTokenId`. There is **no** refund event type on this record. There is **no** `Supports*` property on the interface. Capability is a static helper in Contracts.

Shared money math lives in `GatewayCommon` (`GatewayCommon.cs:42-49`): CHIP/Xendit use banker's `ToMinorUnitsRounded`; Billplz/Razorpay use truncating `ToMinorUnitsTruncating`. Stripe multiplies by 100 in the adapter itself (`StripeGatewayAdapter.cs:279, 308, 486`) and does **not** call `GatewayCommon` for amounts.

`CheckoutSessionCashier` is the one generate path for Commerce hop 2, M2M, and detailed queries (`CheckoutSessionCashier.cs:33-115`). It decrypts the tenant key, stamps `hub_payment_environment`, and refuses a soft-disabled or missing config when `requireActiveGateway` is true. Preferred gateway → first active tenant config → legacy `"BILLPLZ"` last resort only when `requireActiveGateway` is false (`CheckoutSessionCashier.cs:117-144`). M2M never takes that last resort.

Inbound HTTP is `POST /webhooks/payments/{gatewayType}/{tenantId}` (`Endpoints.cs:26-89`). The handler is `ProcessGatewayWebhookCommandHandler`. Outbox publish is `OutboxEventBus<PaymentsDbContext>.PublishAsync`, which **only `Add`s** an `OutboxMessage` (`OutboxEventBus.cs:16-27`). The log insert and the outbox insert commit together in `IPaymentWebhookLogRepository.SaveChangesAsync` (comment at `IPaymentRepositories.cs:31-33`).

---

## 2. `PaymentGatewayCapabilities` — current matrix

```1:58:apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs
/// <summary>
/// Honest collection-mode matrix. Only Stripe and CHIP Collect can vault and charge off-session.
/// Billplz, Razorpay (not demoable), unknown, and blank names are reminder-only.
/// Refund capability is a separate axis: Razorpay can API-refund; Billplz cannot.
/// </summary>
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
    public static bool SupportsDuitNowQr(string? gatewayName)
    {
        var g = Normalize(gatewayName);
        return g is "XENDIT" or "CHIP" or "BILLPLZ";
    }
    public static bool SupportsHostedWallet(string? gatewayName, string? wallet)
    {
        var g = Normalize(gatewayName);
        if (g is not ("XENDIT" or "CHIP"))
        {
            return false;
        }
        var w = Normalize(wallet);
        return w is "GRABPAY" or "SHOPEEPAY" or "TNG" or "TOUCHNGO" or "BOOST" or "DUITNOW";
    }
    public static bool SupportsEmandate(string? gatewayName)
    {
        _ = gatewayName;
        return false;
    }
    public static bool RequiresMarkRefunded(string? gatewayName)
    {
        var g = Normalize(gatewayName);
        return g is "" or "BILLPLZ" or "OFFLINE" or "BANK_TRANSFER" or "CASH" or "MANUAL_OFFLINE" or "COMPED";
    }
}
```

Normalization is trim + `ToUpperInvariant` (line 57). Unknown names, `null`, and `""` are reminder-only and not API-refundable. Blank / offline names require mark-refunded. **Xendit is API-refundable and not mark-refunded.** `SupportsEmandate` is hard-false for every name, including Razorpay.

Tests lock the off-session / refund / mark-refunded / Xendit wallet subset (`PaymentGatewayCapabilitiesTests.cs:10-60`). They do **not** assert `RequiresMarkRefunded("XENDIT") == false` (code returns false). They do **not** assert `SupportsEmandate("RAZORPAY")`. They do **not** assert TnG / Boost / DuitNow on Xendit vs the adapter channel list.

### 2.1 Who actually reads the flags (Payments tree)

Inside `Modules/Payments/`, the only runtime reader is `ExecuteOffSessionChargeIntegrationEventHandler.cs:39` (`SupportsOffSession`). The other flags are unread by adapters, webhook handler, cashier, and M2M create. `SupportsDuitNowQr` / `SupportsHostedWallet` / `SupportsEmandate` have **zero** readers under `Modules/Payments/`. They exist for Commerce / portal / ops (other slices) and for tests.

### 2.2 Capability vs adapter (code, not marketing)

| Axis | Stripe | CHIP | Billplz | Razorpay | Xendit |
|------|--------|------|---------|----------|--------|
| Hosted checkout | Y (`payment` or `setup`) | Y (`purchases/`) | Y (`bills`) | Y (link **or** card registration) | Y (`/v2/invoices`) |
| Webhook verify | `Stripe-Signature` + `EventUtility.ConstructEvent` | RSA `X-Signature` vs PEM | HMAC-SHA256 `x_signature` (two field sets) | HMAC `X-Razorpay-Signature` | Fixed-time `x-callback-token` |
| `EventId` now | Stripe `evt_…` | **`{mapped}:{purchaseId}`** | **`{mapped}:{billId}`** | header Event-Id else `pay_…` | **`{mapped}:{invoiceId}`** |
| `GatewayTransactionId` | PI / SetupIntent / session | purchase id | bill id | payment id | invoice id |
| Off-session | Y + idempotency key | Y, **no** idempotency key | returns false | code exists, capability false | returns false |
| `SupportsEmandate` | false | false | false | **false**; generate uses `method=card` | false |
| API refund | Y + idempotency | Y, no key | always false | Y, no key | Y, no key; `invoice_id` |
| Customer portal | Stripe Billing Portal | throws | throws | throws | throws |
| Disputes inbound | `charge.dispute.created` | N | N | N | N |
| Refund webhook map | **No** | **`payment.refunded` registered, not mapped** | n/a | **No** | **No** |

---

## 3. Quoted walk — Stripe

File: `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs`.

### 3.1 Checkout, including `$0` setup (`8b3567d`)

`GenerateCheckoutAsync` builds options via `CreateCheckoutSessionOptions` (`440-507`) and `SessionService.CreateAsync`.

`$0` + `setupFutureUsage` is no longer `mode=payment` with a `$0` PaymentIntent (invalid at Stripe). It is Checkout **setup mode**:

```454:473:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs
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

Non-zero stays `mode=payment` (`475-507`). Quantity is a line-item quantity (`492`). Currency is lowercased (`485`, `460`). Unit amount is `amount * 100` as `UnitAmountDecimal` — **no banker's round, no truncate** (`486`). A unit of `10.005` becomes `1000.5` sen. Off-session and refunds cast `(long)(amount * 100)` and **truncate** (`279`, `308`). Three different minor-unit policies inside one adapter.

`ApplyPayingTenantMetadata` keeps an incoming `tenant_id` (platform charges) and stamps `platform_tenant_id` when the adapter tenant differs (`427-438`). This is the **only** adapter that does not clobber a paying tenant. Tests lock it (`StripeGatewayAdapterTests.cs:117-185`).

`ApplyCardWalletPaymentMethodTypes` sets `PaymentMethodTypes = ["card"]` (`417-420`). Comment on 415-416: wallets ride on `card`; listing `apple_pay` / `google_pay` is invalid. This **replaces** Dashboard dynamic payment methods. Stripe FPX, GrabPay, and Link will not appear on a Lazuar-created session. Tests forbid those strings (`StripeGatewayAdapterTests.cs:26-36, 66-87`).

`ApplySetupFutureUsage` when true: `PaymentIntentData.SetupFutureUsage = "off_session"` and `CustomerCreation = "always"` (`522-533`). Tests lock the pairing (`242-267`).

`$0` **without** `setupFutureUsage` falls through to `mode=payment` with `UnitAmountDecimal = 0`. Stripe will reject that PaymentIntent. M2M `CheckoutAmountRules` refuses `amount <= 0` (`CheckoutAmountRules.cs:24-27`), so M2M cannot hit this. Commerce `$0` trial/coupon on an off-session gateway now passes `setupFutureUsage: true` (`InitiateCheckoutCommandHandler.cs:286-316`). Reminder-only `$0` still uses `ProcessZeroAmount` (Commerce, out of scope).

### 3.2 Webhook

`ParseWebhookAsync` (`45-262`):

1. Requires `Stripe-Signature` (case-insensitive). Missing → `Verified=false` (`51-55`). Test: `ParseWebhook_MissingStripeSignature_IsNotVerified`.
2. `EventUtility.ConstructEvent(rawBody, signature, webhookSecret)` — Stripe library verify, default timestamp tolerance (~300s). `StripeException` → `Verified=false` (`257-260`). **Non-`StripeException` is not caught** and will 500 the endpoint. Test: `ParseWebhook_BadSecret_IsNotVerified`.
3. There is **no** signature-skip flag, no empty-secret bypass, no “test mode accept unsigned”. Empty `WebhookSecret` on the tenant config is refused **before** parse (`ProcessGatewayWebhookCommandHandler.cs:59-62`).
4. Maps:
   - `checkout.session.completed` / `payment_intent.succeeded` on a `Session` → `PAYMENT_COMPLETED`, `EventId = stripeEvent.Id`, `GatewayTransactionId = PaymentIntentId ?? SetupIntentId ?? session.Id` (`59-147`). **This is the `$0` setup extract.** If there is no PI, `ReadSetupSessionVaultIds` pulls customer + PM from the expanded SetupIntent (`104-106`, `513-520`). If the event object has no expanded SI, the adapter `GetAsync`s the SetupIntent (`107-125`). Expand failure logs a warning and leaves `GatewayTokenId` null — **the event is still `PAYMENT_COMPLETED` with amount 0**.
   - Same types on a `PaymentIntent` → `PAYMENT_COMPLETED`, `EventId = stripeEvent.Id`, `GatewayTransactionId = pi.Id` (`148-207`).
   - `payment_intent.payment_failed` → `PAYMENT_FAILED` via `MapPaymentIntentPaymentFailed` (`210-214`, `322-352`). Decline code copied into metadata. Test: `MapPaymentIntentPaymentFailed_CopiesDeclineCode`.
   - `charge.dispute.created` → `DISPUTE_CREATED` (`216-253`). Metadata pulled from the PI when possible. Expand failure leaves metadata empty, **dispute still publishes**.
   - Anything else → verified passthrough with raw `stripeEvent.Type` and `stripeEvent.Id` (`255`). Handler then drops it (`ProcessGatewayWebhookCommandHandler.cs:83-88`).

Fee extraction expands `latest_charge.balance_transaction`. Expand failure logs a warning and leaves `GatewayFee=0` rather than blocking fulfillment (`99-102`, `182-186`). Honesty gap on the fee axis, not on paid/unpaid.

Currency on the webhook is `session.Currency ?? "myr"` / `pi.Currency ?? "myr"` / `dispute.Currency ?? "myr"` — **lowercase, and invented `myr` when missing**. Razorpay and Xendit fail closed on missing currency. Stripe does not.

`EventId` is the Stripe event id (`evt_…`). Dual money events (`checkout.session.completed` + `payment_intent.succeeded`) share business key `PAYMENT_COMPLETED:{PaymentIntentId}` (`ProcessGatewayWebhookCommandHandler.Idempotency.cs:13-22`). Fail then later succeed uses different `evt_` ids and different business keys (`PAYMENT_FAILED:pi_x` vs `PAYMENT_COMPLETED:pi_x`). Stripe fail-then-pay at the **log** layer is safe.

Dropped Stripe types that matter: `charge.refunded`, `refund.updated`, `refund.failed`, `charge.dispute.closed`, `charge.dispute.updated`, `checkout.session.async_payment_succeeded`, `checkout.session.async_payment_failed`, `setup_intent.succeeded` (if `checkout.session.completed` is lost, setup vault is gone), `charge.succeeded` without a Checkout/PI mapping already handled.

### 3.3 Off-session

`ChargeOffSessionAsync` (`264-297`) creates a PaymentIntent with `OffSession=true`, `Confirm=true`, metadata from `BuildOffSessionMetadata`, and `RequestOptions.IdempotencyKey` from `ResolveOffSessionIdempotencyKey`. Success is `succeeded` **or** `processing` (`289`). `StripeException` becomes `OffSessionDeclinedException` with decline code. The handler publishes `GatewayPaymentFailed` with that code (`ExecuteOffSessionChargeIntegrationEventHandler.cs:91-99`).

Idempotency: `lazuar-offsession:{chargeAttemptId}` when Commerce supplied an attempt id, else the inbox event id (`StripeGatewayAdapter.cs:362-375`; handler `66-68`). Tests lock the prefix (`StripeGatewayAdapterTests.cs:281-300`; `ExecuteOffSessionChargeIntegrationEventHandlerTests.cs:32-106`).

**Success does not publish `GatewayPaymentCompleted`.** The handler returns quietly (`111-116` is only the `!success` branch). Commerce learns about a successful off-session charge from the **later** `payment_intent.succeeded` webhook. That is the closed loop. If the webhook is lost, the adapter returned `true` and nobody publishes completed.

`processing` treated as success means a later `payment_intent.payment_failed` is a **different** EventId and a **different** business key (`PAYMENT_FAILED:pi_x`). Both events publish. That is what the comment on line 210 wants for AUTO_CHARGE. It is also how a “we thought it worked” window exists until the fail webhook.

Soft-disabled config is refused for off-session (`ExecuteOffSessionChargeIntegrationEventHandler.cs:50-58`) but refunds still run (`GatewayRefundRequestedIntegrationEventHandler.cs:30` + test `SoftDisabledConfig_StillCallsAdapter`).

### 3.4 Refund

`IssueRefundAsync` (`299-320`) refunds a **PaymentIntent** by id, amount in truncated minor units, idempotency `lazuar-refund:{transactionId}:{minor}` (`354-360`). Returns true for `succeeded` **or** `pending` (`313`). **Pending is treated as adapter success.** There is no mapping of `charge.refunded`, `refund.updated`, or `refund.failed`. The refund loop is adapter HTTP, not webhook. Test: `FormatRefundIdempotencyKey_UsesTransactionAndMinorAmount`.

### 3.5 Portal

`GenerateCustomerPortalAsync` (`535-555`) lists customers by email (limit 1) and creates a Billing Portal session. First customer with that email wins. No customer → `InvalidOperationException`. This is the only adapter that implements a portal. The query handler hard-requires Stripe (`GenerateCustomerPortalQueryHandler.cs:28-38`) and refuses a soft-disabled Stripe config.

---

## 4. Quoted walk — CHIP Collect

File: `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs`.

### 4.1 Checkout

Requires `merchantId` as Brand ID (`43-46`). POST `https://gate.chip-in.asia/api/v1/purchases/` with Bearer key (`91-93`). Payload: brand, client email/name, one product (rounded minor units), `success_redirect` / `failure_redirect` / `cancel_redirect`, purchase metadata (`54-77`).

**`tenant_id` is overwritten**, not preserved:

```51:51:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs
        metadata["tenant_id"] = tenantId.ToString();
```

`GenerateSystemCheckoutSessionQueryHandler` passes `systemId` as `tenantId` and has already put the paying tenant in metadata (`GenerateSystemCheckoutSessionQueryHandler.cs:44-59`). Stripe keeps the paying tenant. CHIP clobbers it to the system org. Platform CHIP / Hub SaaS fee on a CHIP brand would publish `tenant_id = 00000000-0000-0000-0000-000000000001`.

`setupFutureUsage` sets `force_recurring=true` and, if amount in cents is 0, `skip_capture=true` (`79-87`). That is the CHIP `$0` vault path after `8b3567d` (Commerce now mints hop-2 for `$0` recurring on off-session gateways instead of `ProcessZeroAmount`).

There is **no** payment-method allow-list. FPX, DuitNow QR, wallets, cards, BNPL appear if the CHIP brand is configured for them.

`_configuration` is injected and **never read** (`ChipCollectGatewayAdapter.cs:21, 32`). Dead field. CHIP always hits the live host `https://gate.chip-in.asia/api/v1/`. Tenant `environment=test` does not select a CHIP test host.

Saving a new CHIP key from `UpdatePaymentConfigCommandHandler` (`105-145`) GETs `public_key/` (stored as webhook PEM) and POSTs a webhook subscription for `purchase.paid`, `purchase.payment_failure`, `payment.refunded`, `purchase.preauthorized`. Localhost is rewritten to `lazuar-local-dev.com`. There is **no list-before-create**. Re-saving a CHIP key registers another webhook. `payment.refunded` is registered and then ignored by the parser.

CHIP official docs distinguish **company** public key (`GET /public_key/`, used for `success_callback`) from **webhook** `Webhook.public_key` (dedicated key pair). We always verify inbound `/webhooks/payments/chip/...` with the company PEM. If CHIP signs webhook deliveries with the webhook-specific key, every CHIP webhook is `Verified=false` and the handler 500s. This tree has been doing company-PEM since Wave 0; soak status is not in this module. Residual, not proven broken.

### 4.2 Webhook

`ParseWebhookAsync` (`124-228`):

1. Requires `X-Signature` base64 (`130-134`). Missing → not verified. Test: `ParseWebhook_MissingSignature_IsNotVerified`.
2. Imports webhook secret as RSA PEM, `VerifyData` SHA256 PKCS1 (`139-147`). Fail → not verified. Test: `ParseWebhook_BadSignature_IsNotVerified`.
3. PEM import / base64 decode exceptions fall into the outer `catch` (`223-227`) → `Verified=false`. Parse errors are **swallowed into not-verified**, then the handler throws and the gateway retries.
4. Maps `purchase.paid` → `PAYMENT_COMPLETED`. Maps `purchase.payment_failure` → `PAYMENT_FAILED`. **Everything else, including `purchase.preauthorized` and `payment.refunded`, is verified passthrough** (`164-167`). Handler drops passthrough (`ProcessGatewayWebhookCommandHandler.cs:83-88`). Test: `ParseWebhook_Preauthorized_IsVerified_NotPaymentCompleted` **locks this drop**.
5. Stable id: nested `purchase.id` then root `id` (`ReadStablePurchaseId`, `369-382`). Missing → `Verified=false`, “Missing stable CHIP purchase id”. Never invents a Guid. Test: `ParseWebhook_PurchasePaid_NoIds_IsNotVerified`.
6. **`EventId = $"{mappedEventType}:{purchaseId}"`** (`177`). **`GatewayTransactionId = purchaseId`** (`211`). This is `a1afc09`. Fail and pay no longer share EventId. Tests: `ParseWebhook_PurchasePaid_UsesRootId` expects `PAYMENT_COMPLETED:purch_root_1`; `ParseWebhook_PaymentFailure_UsesStablePurchaseId` expects `PAYMENT_FAILED:purch_fail_1`; handler test `Handle_FailThenPay_SameObject_PublishesFailedAndCompleted`.
7. Currency: `purchase.currency` or **invented `"MYR"`** (`183`). Not fail-closed.
8. Vault: `ExtractVaultIds` (`384-408`) prefers `recurring_token`, else purchase id when `is_recurring_token`, customer from `client.id` or fallback to token. **`ExtractVaultIds` uses root `id` for the purchase-id fallback, not `ReadStablePurchaseId`.** Nested-vs-root disagreement yields `GatewayTransactionId` = nested and `GatewayTokenId` = root.

Fees from `payment.fee_amount` / `net_amount` when present, divided by 100 (`188-192`).

### 4.3 `$0` + `skip_capture` — the remaining money hole

CHIP Collect official callbacks page (`https://docs.chip-in.asia/chip-collect/overview/callbacks`):

> The system will generate a callback when:
> - a Purchase with `skip_capture=false` is successfully paid
> - a Purchase with `skip_capture=true` is successfully **captured** (`POST /purchases/{id}/capture/`)
> - a Purchase is successfully paid using a recurring token

We never call `/capture/`. For `$0` + `force_recurring` + `skip_capture=true`:

- `purchase.paid` **does not fire**.
- `purchase.preauthorized` is registered, verified, and **dropped** (not `PAYMENT_COMPLETED`, vault ids never extracted).
- Commerce (`InitiateCheckoutCommandHandler.cs:286-316`) now sends the buyer to this hop-2 instead of `ProcessZeroAmount`.
- Stripe was given setup-mode + webhook extract in `8b3567d`. CHIP generate already had `skip_capture`. CHIP **parse was not given the equivalent of `ReadSetupSessionVaultIds`**.

Historical gap `docs/001-gaps/02-payment-webhooks.md` was the opposite bug: `purchase.preauthorized` treated as paid (auth-hold fulfilled). Wave 0 dropped preauthorized as paid. That is still correct for **money**. It is **incorrect for `$0` vault**, which is not money — it is the CHIP analogue of Stripe setup mode.

`8b3567d` commit message says “vault `$0` Stripe **and CHIP** recurring checkouts”. The Commerce caller change is real. The CHIP webhook side of the vault is not. A 100% coupon / `$0` recurring price on CHIP Collect creates a hosted purchase the buyer completes; Lazuar never publishes `GatewayPaymentCompleted` and never stores a token.

This is **B04-P01**.

### 4.4 Off-session

`ChargeOffSessionAsync` (`230-323`): GET original purchase by `tokenId`, clone brand + client, POST a new purchase, POST `purchases/{newId}/charge/` with `{ recurring_token: tokenId }`. Success statuses `paid` or `pending_charge`. **No idempotency key** — comment on 236: “CHIP purchase/charge has no idempotency key (best-effort).” `idempotencyKey` is discarded. A retried inbox message after a lost HTTP 200 can double-charge at CHIP.

The GET uses `tokenId` as a **purchase** id (`242`). `ExtractVaultIds` prefers `recurring_token` when present (`392-396`). Test `ExtractVaultIds_PurchaseNodeTokenAndClient_FallsBackCustomerToToken` sets `tokenId = "tok_from_purchase"`. Charge would `GET /purchases/tok_from_purchase/`. If CHIP’s `recurring_token` is not the original purchase id, GET 404s and the charge returns false. Capability `SupportsOffSession("CHIP")` is true, so Billing **will** call this path when vault ids exist.

`pending_charge` is adapter `true`. Commerce still waits for `purchase.paid` on the **new** purchase (new EventId `PAYMENT_COMPLETED:{newId}`). If the charge stays pending forever, adapter said success and no completed webhook arrives.

### 4.5 Refund

`IssueRefundAsync` (`325-355`) POST `purchases/{transactionId}/refund/` with optional `{ amount }` in rounded minor units. HTTP success → true. No refund id. No idempotency key. `payment.refunded` webhook is not mapped. Test: `IssueRefundAsync_PostsMinorUnitsToPurchaseRefund`.

### 4.6 Portal

Throws `InvalidOperationException` (`357-360`).

---

## 5. Quoted walk — Billplz

File: `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs`.

### 5.1 Checkout

Requires Collection ID (`56-59`). Resolves callback base via `BillplzPublicBase.TryResolveCallbackBase` (`62-65`): must be public https unless `App:AllowInsecureBillplzCallback`. Loopback and `lazuar-local-dev.com` fail closed with `CALLBACK_BASE_NOT_PUBLIC`. Production vs sandbox API host is **not** inferred from Hub hostname (`BillplzPublicBase.cs:39-42`). It follows `App:BillplzEnvironment` then tenant `environment` (`test`|`live`) (`22-43`). `ProductionHosts` is assigned then discarded (`39-42`) — dead table after LP-182. Tests: `BillplzPublicBaseTests.cs`.

Callback URL is `{base}/webhooks/payments/billplz/{tenantId}?type=&reference_1=` and optional `checkout_id` for M2M (`78-88`). Billplz does not persist arbitrary metadata; query string + server-side session merge are the recovery path (`ProcessGatewayWebhookCommandHandler.Metadata.cs:13-77`).

`setupFutureUsage` is an unused parameter. There is no vault. Honest.

Minor units **truncate** (`90`). Currency on the webhook is hardcoded `"MYR"` (`237`) — Billplz is MY-only in this wrap. Generate ignores the `currency` argument.

**Billplz does not overwrite `tenant_id`.** It reads `metadata["tenant_id"]` as `reference_1` when `subscription_id` is missing (`73-75`). Platform types map `reference_1` back to `tenant_id` on parse (`208-212`). Platform Billplz can keep the paying tenant. Tests: `ParseWebhook_PlatformSaasFee_MapsReference1ToTenantId`.

HTTP client is `PublicDnsFallback.HttpClientName` (`110`) — the only adapter that uses the 1.1.1.1 / 8.8.8.8 connect hook.

### 5.2 Webhook

Form body, not JSON (`ParseFormBody`, `311-322`). Verify (`144-166`):

1. Require `x_signature`.
2. HMAC-SHA256 over sorted `key+value` joined by `|`, excluding `x_signature`. First try including `paid_at` / `transaction_id` / `transaction_status`; if that fails, retry excluding those extra fields (`32-40, 157-166`). Fixed-time hex compare (`304-309`). This is compatibility, not a skip. Both tries need the secret.
3. Missing/blank bill `id` → `Verified=false` (`171-176`). Tests: `ParseWebhook_MissingId_IsNotVerified`, `ParseWebhook_EmptyId_IsNotVerified`.
4. Paid if `paid=true` or `state=paid` (`181-182`). Else **`PAYMENT_FAILED`**. A create-time `due` callback is a verified fail.
5. **`EventId = $"{(isPaid ? "PAYMENT_COMPLETED" : "PAYMENT_FAILED")}:{billId}"`** (`235`). **`GatewayTransactionId = billId`** (`238`). This is `a1afc09`. Tests: `ParseWebhook_QueryCheckoutId_IncludedInMetadata` expects `PAYMENT_COMPLETED:bill_abc123`; `ParseWebhook_Unpaid_IsPaymentFailed_WithBillId` expects `PAYMENT_FAILED:bill_unpaid_1`.
6. Metadata from `reference_2` (type) and `reference_1` (subscription id, or `tenant_id` for platform types) (`200-212`). `checkout_id` from `Query-checkout_id` or form (`214-224`).

Fee is `(paidAmount * estimatedFeePercentage / 100) + fixedFee` (`226-230`). **The webhook handler always passes `0, 0, 0`** (`ProcessGatewayWebhookCommandHandler.cs:74-76`). Billplz `GatewayFee` is therefore **always 0** in production. Accounting overrides were removed by migration `20260705131411_RemoveAccountingOverrides`.

### 5.3 Off-session

Logs a warning and returns false (`255-267`). Does not throw. Capability short-circuit means Commerce should not call this; if it does, the handler treats `success=false` as `charge_declined`. Test: `ChargeOffSessionAsync_DoesNotThrow_ReturnsFalse`.

### 5.4 Refund

```273:276:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs
    public Task<bool> IssueRefundAsync(...)
    {
        return Task.FromResult(false);
    }
```

`RequiresMarkRefunded("BILLPLZ")` is true. Test: `IssueRefundAsync_AlwaysReturnsFalse`.

### 5.5 Late unpaid after paid

Because EventId is now namespaced, an unpaid callback **after** a paid callback is a **new** EventId (`PAYMENT_FAILED:billId` ≠ `PAYMENT_COMPLETED:billId`) and a **new** business key. The handler will publish `GatewayPaymentFailed` **after** `GatewayPaymentCompleted` for the same bill. There is no “completed supersedes failed on the same object” check. Billplz can replay an old `paid=false` body (no timestamp on the HMAC). M2M session that is already `completed` will ignore the fail (`IntegrationCheckoutGatewayEventsHandler.cs:100-106`). Commerce reaction is out of scope; **Payments will still publish the fail**.

---

## 6. Quoted walk — Razorpay / Curlec

File: `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs`.

### 6.1 Checkout — card registration, not e-mandate

API key format `KeyId:KeySecret` (`24-30`). Two generate paths (`58-99`):

- `setupFutureUsage == true`: `Invoice.CreateRegistrationLink` with `subscription_registration.method = "card"`, `max_amount = amountPaise * 10`, `expire_at` +10 years (`58-82`).
- else: `PaymentLink.Create` (`84-98`).

There is **no** `method=emandate` / FPX mandate path. `SupportsEmandate` is false. `SupportsOffSession("RAZORPAY")` is false. The adapter **comments do not claim e-mandate**; they hard-code `"card"` (`62`). The leftover claim lives in ops labels (slice 09). This slice’s honesty: **the adapter registers a card and the capability matrix refuses to charge it.**

Commerce still sends `SetupFutureUsage: true` for every recurring interval (Commerce, out of scope as a product decision). On Razorpay that **does** create a registration link. Webhook may return `customer_id` / `token_id` (`172-173`). Vault persist is Commerce. At adapter layer we emit those ids when present.

Placeholder contact `+60100000000` when `customer_phone` is missing (`47`). Dummy phone on every checkout. Email is **not** run through `GatewayCommon.ResolveEmail` — blank email is sent blank.

Currency on generate is `ToUpperInvariant()` (`71`, `89`). Minor units truncate (`40`).

### 6.2 Webhook

`Utils.verifyWebhookSignature` (`120`). Missing `X-Razorpay-Signature` → not verified. Test: `ParseWebhook_MissingSignature_IsNotVerified`. Bad signature throws, outer catch → not verified (`199-203`).

Mapped events (`125-133`, `301-302`):

- `payment.failed` / `invoice.expired` → `PAYMENT_FAILED` (`MapPaymentFailed`).
- `payment.captured` → `PAYMENT_COMPLETED`.
- else verified passthrough (dropped by handler). **`payment.authorized` is not mapped.** If the Razorpay account is not auto-capture, authorized payments never complete.

`EventId`: prefer `X-Razorpay-Event-Id`, else payment id. Missing both → `Verified=false`, never invent a Guid (`138-156`, `336-349`). Currency missing → fail closed, no invented MYR (`174-179`, `364-371`). Tests: `ParseWebhook_CapturedWithoutCurrency_DoesNotInventMyr`, `ParseWebhook_HeaderEventIdAndPaymentId_MapsIdentities`, `ParseWebhook_PaymentFailed_MapsPaymentFailed`.

If Razorpay sends the Event-Id header (they usually do), fail and capture have **different** EventIds. Fail-then-pay is safe. **If a delivery omits the header**, both events fall back to the same `pay_…` and EventId collides the same way CHIP/Billplz used to. `a1afc09` did **not** namespace Razorpay. Residual: **B04-P09**.

`invoice.expired` is treated as `PAYMENT_FAILED` via `MapPaymentFailed`, which looks for `payload.payment.entity`. An expire payload is typically `payload.invoice.entity`. Without a payment id and without `X-Razorpay-Event-Id`, EventId is missing → `Verified=false` → handler throws → **Razorpay retries the expire forever**. With the header, we publish `PAYMENT_FAILED` with `GatewayTransactionId = null` (falls back to EventId in the handler). A registration-link expiry becomes a payment-failed integration event.

### 6.3 Off-session (dead to the engine)

`ChargeOffSessionAsync` (`206-278`) creates an order then `Payment.CreateRecurringPayment` with `recurring=true`. No idempotency key (comment line 212). Buyer email/phone are copied **from the notes dictionary this method just built** (`217-233`, `256-268`). Those notes only contain `type`, `subscription_id`, `tenant_id`, `receipt`, optional dunning/attempt ids. They never contain `customer_email`. The “buyer email if present” branch is dead. Dummy email is gone; real email is also gone. Capability false means Billing never calls this. The method is leftover pipe. **No unit test covers `ChargeOffSessionAsync`.**

### 6.4 Refund

`Payment.Fetch(transactionId).Refund` (`280-294`). `transactionId` must be a Razorpay **payment** id (webhook `GatewayTransactionId`). Amount truncated to paise. True if SDK returns non-null. No refund webhook map. No idempotency key.

### 6.5 Portal

Throws (`296-299`).

---

## 7. Quoted walk — Xendit

File: `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/XenditGatewayAdapter.cs`.

Class comment (`16-19`) is accurate: BYOK hosted invoices; money on the tenant Xendit account; reminder-only until payment tokens soak; we do not rebuild wallets.

### 7.1 Checkout

POST `https://api.xendit.co/v2/invoices` Basic `apiKey:` (`58-60`). `setupFutureUsage` discarded (`50-51`). `external_id = lazuar_{guid}` (`189`). Amount is rounded minor units / 100 (major units, Xendit style) (`184`). Currency default on **generate** is invented `MYR` (`191`).

**`tenant_id` is overwritten**, same as CHIP (`185`). Platform Xendit loses the paying tenant.

Optional `payment_methods` from metadata key `xendit_payment_methods` filtered to `MalaysiaHostedChannels` (`199-237`):

```
CREDIT_CARD, DD_FPX, QR_CODE, OVO, DANA, LINKAJA, SHOPEEPAY, GCASH, GRABPAY, PAYMAYA
```

Unknown codes dropped (test `BuildInvoicePayload_FiltersUnknownChannels`). Empty list = merchant dashboard defaults. There is **no TnG, Boost, or named DUITNOW** in the allow-list. `SupportsHostedWallet("XENDIT", "TNG"|"BOOST"|"DUITNOW")` is nevertheless true. `SupportsDuitNowQr("XENDIT")` is true; the requestable code is `QR_CODE`, not `DUITNOW`. Capability names and Xendit codes do not match 1:1.

Nothing in this module sets `xendit_payment_methods`. Production invoices therefore use dashboard defaults. The filter is an unused hook.

Live host only (`LiveApiBase = "https://api.xendit.co"`). Tenant `environment` does not select `api.xendit.co` vs a test host. Xendit keys themselves are test/live.

### 7.2 Webhook

`VerifyCallbackToken`: webhook secret required; header `x-callback-token`; fixed-time compare **only when lengths match** (`240-256`). Different lengths return false immediately (length leak, not a skip). Missing/mismatch → not verified. `apiKey` unused (token, not HMAC of body). Tests: `ParseWebhook_MissingToken_IsNotVerified`.

There is **no timestamp and no body signature**. Anyone who captures the token can replay any body, forever. Dedup is EventId / business key. Replay of the same PAID is safe. Forgery of a **new** invoice id with a stolen token is `PAYMENT_COMPLETED` we never minted.

`MapInvoiceCallback` (`258-335`): unwraps `data` if present. `PAID` / `SETTLED` / `invoice.paid` → `PAYMENT_COMPLETED`. `EXPIRED` / `FAILED` / `invoice.expired` / `invoice.failed` → `PAYMENT_FAILED`. Missing invoice id or currency → not verified (no invented MYR on the webhook path). **`EventId = $"{mapped}:{invoiceId}"`**. **`GatewayTransactionId = invoiceId`**. `a1afc09`. Tests: `ParseWebhook_Paid_MapsCompleted` expects `PAYMENT_COMPLETED:inv_paid_1`; `ParseWebhook_Expired_MapsFailed` expects `PAYMENT_FAILED:inv_exp`.

`PAID` then `SETTLED` share EventId and share business key `PAYMENT_COMPLETED:{id}` — intended idempotency. `EXPIRED` then a later `PAID` on the same invoice (unusual) now has **different** EventIds and will publish both. Typical Xendit invoices do not resurrect after EXPIRED.

No refund / dispute events. `PENDING` is passthrough (dropped).

### 7.3 Off-session

Always `false` (`155-171`). Comment: hosted invoices do not vault. Test: `ChargeOffSession_AlwaysFalse_UntilTokenSoak`. Capability agrees.

### 7.4 Refund

POST `/refunds` with `invoice_id`, `amount` in **major** units, `reason=REQUESTED_BY_CUSTOMER` (`119-148`). HTTP success → true. No idempotency key. Xendit’s refund API historically wants a **payment** id more often than an invoice id. `GatewayTransactionId` is the invoice id. A 4xx becomes adapter false → `GatewayRefundFailed`. There is no mark-refunded fallback (`RequiresMarkRefunded("XENDIT")` is false). Unsoaked. **B04-P14**.

### 7.5 Ops form

Out of scope (slice 09). `cf0f07d` (before `297ba98`) added an Xendit credential form and an honest Razorpay label. This report does not re-litigate the UI.

---

## 8. Inbound webhook pipeline (verify, EventId, unique, replay)

### 8.1 Intake

`POST /webhooks/payments/{gatewayType}/{tenantId}` (`Endpoints.cs:26-89`):

- Unknown gateway → 400 (allow-list).
- Empty body → `throw new InvalidOperationException("Empty request body.")` (`45-48`). The later catch **re-throws** `InvalidOperationException` (`84-88`). Empty body is **HTTP 500**, not 400. Gateway retries. Residual: **B04-P18**.
- Headers copied; query params copied as `Query-{key}` (Billplz checkout_id / type).
- Handler runs; HTTP 200 `{ received: true }` is **intake ACK**, not paid (`73-74`).

Handler (`ProcessGatewayWebhookCommandHandler.cs:56-117`):

1. Load tenant config for that gateway (`GetByTenantAndGatewayAsync` ignores query filters, matches `GatewayType` uppercased). Missing webhook secret → throw (gateway retries).
2. Soft-disable does **not** skip webhooks (`64` comment) — historical payments still fulfill.
3. Decrypt keys. `ParseWebhookAsync` with fee args **hardcoded 0**.
4. `!Verified` → throw `InvalidOperationException` (retry, 500).
5. EventType not in `PAYMENT_COMPLETED` / `PAYMENT_FAILED` / `DISPUTE_CREATED` → **return** (ACK 200, no log, no publish). This is how `purchase.preauthorized`, `payment.refunded`, `customer.updated`, `payment.authorized` die.
6. `businessKey = EventType + ":" + GatewayTransactionId` if tx id present (`Idempotency.cs:13-22`).
7. Lookup `GetByEventId(EventId, Provider)` then `GetByBusinessKey`. **Neither lookup is tenant-scoped** (`PaymentRepositories.cs:48-65`). Unique indexes are `(Provider, EventId)` and `(Provider, BusinessKey)` — global per provider (`PaymentConfigurations.cs:29-35`).
8. Existing → `HandleExistingLogAsync`: skip if no `OutboxMessageId` (pre-ticket backfill); requeue Dead outbox; if already active, **return**; if outbox missing, republish using the **new** parsed result.
9. Fresh → merge IntegrationCheckoutSession metadata by `ProviderSessionId == GatewayTransactionId`, insert `PaymentWebhookLog`, publish, save. Unique 23505 on insert is treated as successful duplicate (`Idempotency.cs:24-57`).

`PublishAsync` only adds an `OutboxMessage` to the same `PaymentsDbContext`. `SaveChanges` is one EF transaction. A 23505 on the log rolls back the outbox add of **that** request. Production does **not** double-publish on a unique race. The test `Handle_UniqueConstraintRace_Returns_WithoutRethrow` uses a mock `IEventBus` and therefore **asserts a publish that production would roll back**. See §10.

There is **no** check that `metadata["tenant_id"]` matches `request.TenantId`. OrganizationId on the published event is the **URL** tenant. A signed body for tenant A posted to tenant B’s URL (same CHIP PEM / same Xendit token / same Billplz X-Signature if they share a processor account) verifies and publishes as B.

### 8.2 EventId after `a1afc09` (re-verify)

| Rail | EventId now | Unique per delivery? | Fail vs pay same object | 008 P0 |
|------|-------------|----------------------|-------------------------|--------|
| Stripe | `evt_…` | Yes | Different evt ids + different business keys | Was already safe |
| Razorpay | header Event-Id, else `pay_` | Header yes; fallback **no** | Safe if header present | Residual |
| CHIP | `{mapped}:{purchaseId}` | Per **type**, not per delivery | **Different** EventIds | **Fixed at log layer** |
| Billplz | `{mapped}:{billId}` | Per type | **Different** EventIds | **Fixed at log layer** |
| Xendit | `{mapped}:{invoiceId}` | Per type | EXPIRED vs PAID different; PAID vs SETTLED same | **Fixed at log layer** |

`GatewayTransactionId` stays the object id on CHIP / Billplz / Xendit. Business key is still `EventType:GatewayTransactionId`. Fail and pay no longer collide on `(Provider, EventId)`. Handler test `Handle_FailThenPay_SameObject_PublishesFailedAndCompleted` (`ProcessGatewayWebhookCommandHandlerTests.cs:488-552`) asserts two logs, two business keys, one failed event, one completed event. Duplicate paid still dedupes (`Handle_DuplicatePaid_SameObject_PublishesCompletedOnce`).

**008 P0 #1 (CHIP/Billplz fail-then-pay EventId collision) is closed at `ProcessGatewayWebhookCommandHandler`.** It is **not** closed at `IntegrationCheckoutGatewayEventsHandler`. See B04-P02.

### 8.3 Session metadata merge

Billplz strips body metadata. Handler merges `IntegrationCheckoutSession` by provider session id (`ProcessGatewayWebhookCommandHandler.Metadata.cs:16-77`): adapter keys win; session fills holes; always stamp `checkout_id` from the session row. Merge errors are swallowed so money still publishes (`68-75`). Tests cover query `checkout_id` and stripped-bill merge.

**The merge test still stubs `EventId = billId` (bare)** (`ProcessGatewayWebhookCommandHandlerTests.cs:350`). That is a stub adapter, not the real Billplz adapter. It does not re-introduce the collision; it is stale relative to `a1afc09`.

### 8.4 Signature skip hunt

Grep of `Modules/Payments` for skip/bypass/unsigned/disable-verify: **no matches**. Every adapter requires its header/token. Empty webhook secret is refused before parse. Billplz’s two HMAC tries both need the secret. Xendit’s length mismatch is a fail, not a skip. Stripe `ConstructEvent` is the library verify.

### 8.5 Replay

- Stripe: timestamp in signature + unique `evt_` + business key. Replay of the same `evt_` hits existing log.
- CHIP / Billplz / Xendit / Razorpay: no timestamp on our verify (except Stripe). Replay of the **same** EventId hits existing log. Replay of a **different** mapped type for the same object is a new EventId after `a1afc09` (intentional for fail-then-pay; harmful for late-fail-after-pay — B04-P08).
- Xendit token replay of a **forged new** invoice id is accepted if the token matches (B04-P16).

---

## 9. Off-session handler, refunds, wallets, credentials, setup mode

### 9.1 Off-session handler

`ExecuteOffSessionChargeIntegrationEventHandler`:

- Capability false → publish failed `off_session_not_supported`, **do not call adapter** (`39-46`). Test: `HandleAsync_Billplz_PublishesOffSessionNotSupported_DoesNotCallAdapter`.
- Missing / empty key / `!IsActive` → `gateway_not_configured` (`50-58`).
- Builds Stripe-shaped idempotency key even for CHIP (`66-68`). CHIP discards it.
- `OffSessionDeclinedException` → decline code in metadata (`91-99`).
- Any other exception → `charge_exception`, **does not rethrow** (`101-108`). Test: `HandleAsync_AdapterThrows_PublishesChargeException_DoesNotRethrow`. Inbox marks the ExecuteOffSession message processed. A transport timeout that actually charged CHIP is a lost charge **and** a published fail. Next attempt is a new ExecuteOffSession (new ChargeAttemptId if Commerce supplies one).
- `success=false` → `charge_declined` (`111-115`).
- `success=true` → **nothing published**. Webhook must close the loop.

Failed-event `GatewayTransactionId` is `"off_session:" + subscriptionId` (`149-152`). **Not unique per attempt.** `ChargeAttemptId` is only in metadata. If any consumer (or a future Payments log) keys idempotency on `GatewayTransactionId`, the second dunning fail for the same subscription is a duplicate. Today this event does **not** go through `PaymentWebhookLog`. It is a direct outbox publish. Commerce inbox is out of scope; the identity is still a Payments-layer footgun: **B04-P10**.

Default `GatewayName = "STRIPE"` on `ExecuteOffSessionChargeIntegrationEvent` (`ExecuteOffSessionChargeIntegrationEvent.cs:14`) is leftover. Callers must pass the product gateway. Same leftover default on `GatewayRefundRequestedIntegrationEvent` (`GatewayRefundRequestedIntegrationEvent.cs:13`).

### 9.2 Refunds at adapter layer

There is **no** inbound refund EventType. Handler only publishes completed / failed / dispute (`ProcessGatewayWebhookCommandHandler.cs:83-88, 162-208`).

`GatewayRefundRequestedIntegrationEventHandler` (`28-73`): load config (soft-disable still refunds), reject amount ≤ 0, `adapter.IssueRefundAsync`. True → `GatewayRefundCompleted` with `RefundedFee = 0` (comment: “until webhook enrichment exists”). False → `GatewayRefundFailed`. Tests cover missing config, non-positive amount, adapter true/false, soft-disable.

Closed-loop gaps (still true after the EventId / setup fixes):

- Stripe `pending` counts as success (`StripeGatewayAdapter.cs:313`).
- No processor-initiated (dashboard) refund ever reaches this module as a typed event.
- CHIP `payment.refunded` is subscribed and ignored.
- Idempotency exists only on Stripe (`lazuar-refund:{pi}:{minor}`). CHIP / Razorpay / Xendit retries can double-refund at the processor if the first call succeeded and we lost the HTTP response.
- Xendit refund body uses `invoice_id` (unsoaked).

### 9.3 Wallets

`SupportsHostedWallet` / `SupportsDuitNowQr` are unread in this module. Stripe session is `card` only. CHIP/Billplz send no method filter. Xendit filter is unused unless metadata is set, and the allow-list does not match the capability names (no TNG / BOOST / DUITNOW). Apple Pay / Google Pay are a Stripe wrap, not a product in this module.

### 9.4 Credential use

`UpdatePaymentConfigCommandHandler` encrypts API key and webhook secret. First-time create requires an API key (`147-151`). Stripe secret is stored in `ApiKey`. GET never returns secrets (`GetPaymentConfigQueryHandler.cs:38-51`). `DecryptOrPlaintext` accepts legacy plaintext rows (test `DecryptOrPlaintext_AcceptsLegacyPlaintextRows`). Soft-disable retains credentials (`TenantPaymentConfiguration.SetActive`).

CHIP on new key: fetch PEM + register webhooks (see §4.1). Other gateways store whatever the client sent. Environment defaults: request → Stripe-shaped key infer → existing → `test` (`UpdatePaymentConfigCommandHandler.cs:97-103`).

Cashier key-mode guard only understands `sk_test_` / `sk_live_` (`CheckoutSessionCashier.cs:149-165`). Billplz / CHIP / Razorpay / Xendit keys skip that guard. Config-environment vs request test mode is a second guard (`167-186`). Hosted (null `RequestIsTestMode`) does not throw (`PaymentGatewayEnvironmentTests.cs:29-33`).

### 9.5 Setup mode (re-verify `8b3567d`)

Stripe `$0` + `setupFutureUsage` → Checkout `mode=setup`, `CustomerCreation=always`, card-only, metadata on session + SetupIntent. Webhook `checkout.session.completed` without a PI extracts customer + PM from the SetupIntent (expanded or fetched). Tests: `CreateCheckoutSessionOptions_ZeroAmountWithSetup_UsesSetupMode`, `ParseWebhook_CheckoutSessionCompleted_SetupIntentWithoutPi_ExtractsCustomerAndPaymentMethod`, `ReadSetupSessionVaultIds_WhenSetupIntentAndNoPi_ExtractsCustomerAndPaymentMethod`.

Gaps that remain on the Stripe setup path:

- Expand/fetch failure still publishes `PAYMENT_COMPLETED` amount 0 with null token.
- `setup_intent.succeeded` is not mapped; if `checkout.session.completed` is lost, vault is lost.
- Setup is typed as **payment completed**. That is the contract Commerce needs to persist vault ids. Ledger booking of a `$0` “payment” is slice 05.

CHIP `$0` setup is **not** equivalently implemented. See B04-P01.

---

## 10. 008 re-verify

`plans/008-evals/02-payments-adapters-rails.md` §16 P0/P1, checked against this tree.

| 008 item | 008 severity | Now |
|----------|--------------|-----|
| CHIP/Billplz EventId = object id; fail-then-pay drops completed | P0 | **Fixed at webhook log** by `a1afc09`. EventId is `{mapped}:{objectId}`. Handler test covers the sequence. `GatewayTransactionId` stays object id. **Not fixed** for M2M session state (B04-P02). |
| Xendit listed in settings, cannot store keys | P0 honesty | Ops form is slice 09. `cf0f07d` landed before `297ba98`. Not re-opened here. |
| Razorpay “MY e-mandate + cards” label | P1 | Adapter is `method=card`; `SupportsEmandate` false. Label is slice 09. Adapter-layer leftover is `CreateRegistrationLink` when Commerce sends `SetupFutureUsage` (B04-P11). |
| Recurring Razorpay still sends `SetupFutureUsage: true` | P1 | Caller is Commerce. Adapter still builds a card registration link. Tokens may be emitted and then discarded by Commerce. |
| Refunds are adapter-success, not webhook-confirmed | P1 | **Still true.** Stripe `pending` still succeeds. CHIP `payment.refunded` still dead. No new refund EventType. |
| Wallet / DuitNow flags unread | P1 | **Still true** inside this module. |
| Billplz `GatewayFee` always 0 | P1 | **Still true.** Handler still passes `0, 0, 0`. |
| CHIP off-session has no idempotency key | P1 | **Still true.** Comment on line 236 unchanged. |
| Docs: README / Payments README / four adapters | P1 | **Still true.** `Modules/Payments/README.md` §3 still says checkouts are stateless; §6 still lists Stripe + Billplz only. `IntegrationCheckoutSessions` exist. |
| Dead payment settings modals | P1 | Slice 09. |
| Razorpay `ChargeOffSessionAsync` email branch dead | P1 | **Still true.** Notes never contain `customer_email`. |
| Admin platform settings cannot set environment | P1 | Slice 09. |
| Empty webhook body 500s | P1 | **Still true** (`Endpoints.cs:45-48`). |
| Product form pay-link copy | P1 | Slice 09. |

New since 008, introduced or exposed by the two fixes:

- CHIP `$0` `skip_capture` never produces `purchase.paid`; preauthorized still dropped; Commerce now **uses** that path (`8b3567d`). **New P0.**
- M2M `IntegrationCheckoutGatewayEventsHandler` still `MarkFailed` on first fail and then **refuses** completed. EventId fix made both events publish; the session layer drops the pay. **New P0 (was masked by the old EventId collision).**
- Stripe setup-mode `PAYMENT_COMPLETED` amount 0 with optional null token if SI expand fails.

---

## 11. Bug catalog

Severity: **P0** = lost money or lost fulfillment of a payment the buyer completed. **P1** = wrong money identity, double-charge/refund window, tenant mix-up, or a rail that cannot do what the port claims. **P2** = honesty, retry storms, dead code, test lies that do not themselves lose money.

### B04-P01 — P0 — CHIP `$0` + `skip_capture` never fulfills and never vaults

**Where.** `ChipCollectGatewayAdapter.cs:79-87` (sets `skip_capture` when `setupFutureUsage` and cents == 0); `164-167` (drops `purchase.preauthorized`); `UpdatePaymentConfigCommandHandler.cs:133` (registers `purchase.preauthorized`); Commerce caller `InitiateCheckoutCommandHandler.cs:286-316` (now mints hop-2 for `$0` recurring on CHIP).

**What.** CHIP official callbacks: `skip_capture=true` success callback fires on **capture**, not on buyer completion. We never capture. `purchase.paid` does not fire. `purchase.preauthorized` is verified and returned as raw type; the handler returns without a log. No `GatewayPaymentCompleted`. `ExtractVaultIds` never runs.

**Why it is P0.** After `8b3567d`, a 100% coupon or `$0` recurring CHIP product is a hosted purchase the buyer finishes. Lazuar ACKs nothing and stores no token. Stripe on the same product works (setup mode). The commit claimed both rails.

**Not fixed by EventId namespacing.** There is no second event to namespace.

**Test that lies by omission.** `ParseWebhook_Preauthorized_IsVerified_NotPaymentCompleted` asserts the drop and never asserts a `$0` vault extract. There is **no** test that CHIP `$0` generate sets `skip_capture`. There is **no** test that a preauthorized payload with `is_recurring_token` / `recurring_token` yields vault ids.

### B04-P02 — P0 — M2M fail-then-pay: session stays `failed`, outbound `payment.completed` never sent

**Where.** `IntegrationCheckoutGatewayEventsHandler.cs:59-66` (completed only if `Status == open`); `89-108` (fail marks `failed` while open); `IntegrationCheckoutSession.MarkFailed` (`104-108`) has no “already completed” guard because the handler checks status first.

**What.** After `a1afc09`, `ProcessGatewayWebhookCommandHandler` publishes **both** `GatewayPaymentFailed` and `GatewayPaymentCompleted` for the same CHIP purchase / Billplz bill. The M2M handler consumes failed first (or any unpaid Billplz `due` callback): `MarkFailed`, outbound `payment.failed`. Completed arrives: status is not `open`, debug log “skipping duplicate payment.completed”, **return**. Integrator is told the checkout failed. Buyer paid.

**Why the EventId fix made this visible.** Before `a1afc09`, completed was dropped at the log and M2M never saw it. The log layer is now correct. The session state machine still treats fail as terminal.

**Pay-then-fail is safe here** (completed first → fail skipped). Fail-then-pay is not.

**Test that lies by omission.** `IntegrationCheckoutOutboundWebhookTests` has `Failed_AlreadyFailed_NoSecondPublish` and `Completed_AlreadyCompleted_NoSecondPublish` and **no** `Failed_ThenCompleted_MarksCompleted`. The new handler test `Handle_FailThenPay_SameObject_PublishesFailedAndCompleted` stops at the outbox and never instantiates `IntegrationCheckoutGatewayEventsHandler`.

### B04-P03 — P1 — CHIP off-session: `tokenId` used as a purchase id; `recurring_token` may not be one

**Where.** `ExtractVaultIds` prefers `recurring_token` (`392-396`). `ChargeOffSessionAsync` `GET /purchases/{tokenId}/` (`242`). Test `ExtractVaultIds_PurchaseNodeTokenAndClient_FallsBackCustomerToToken` sets token `tok_from_purchase`.

**What.** If CHIP’s `recurring_token` is a distinct token string, GET 404s, charge returns false, Billing publishes `charge_declined` (via the off-session handler) even though a valid token exists. The charge API itself wants `{ recurring_token }` — that part is right. The GET-to-clone-brand step is the broken assumption.

`ExtractVaultIds` also uses **root** `id` for the is-recurring fallback, not `ReadStablePurchaseId`. Nested-vs-root disagreement splits `GatewayTransactionId` and `GatewayTokenId`.

### B04-P04 — P1 — CHIP off-session has no processor idempotency key

**Where.** `ChipCollectGatewayAdapter.cs:236` (`_ = idempotencyKey`). Handler still passes `lazuar-offsession:{attempt}` (`ExecuteOffSessionChargeIntegrationEventHandler.cs:66-80`).

**What.** Inbox redelivery after CHIP charged and the HTTP response was lost creates a **second** purchase and a **second** `/charge/`. Stripe is the only adapter with a real off-session idempotency key. Capability says CHIP will be called.

### B04-P05 — P1 — CHIP / Xendit clobber paying `tenant_id` on generate

**Where.** `ChipCollectGatewayAdapter.cs:51`; `XenditGatewayAdapter.cs:185`. Contrast `StripeGatewayAdapter.ApplyPayingTenantMetadata` (`427-438`) and Billplz which **reads** `tenant_id` (`73-75`).

**What.** `GenerateSystemCheckoutSessionQueryHandler` passes `PlatformCheckoutTypes.SystemOrganizationId` as the adapter tenant and puts the paying workspace in metadata (`44-59`). System-org CHIP/Xendit checkout overwrites that to the system guid. Webhook metadata `tenant_id` then names the platform, not the workspace that must be activated. Stripe tests explicitly lock the opposite behaviour (`CreateCheckoutSessionOptions_HasNoApplicationFeeOrTransfer_AndKeepsPayingTenant`). There is **no** CHIP/Xendit twin of that test.

Razorpay generate does not set `tenant_id` itself; notes are the incoming dictionary. Off-session Razorpay **does** set `tenant_id` to the adapter tenant (`221`) — dead while capability is false.

### B04-P06 — P1 — No inbound `tenant_id` vs URL tenant check; EventId unique is not tenant-scoped

**Where.** `ProcessGatewayWebhookCommandHandler` publishes `OrganizationId: request.TenantId` (`170-208`). Merge only **fills** missing `tenant_id` (`Metadata.cs:56-59`). `GetByEventId` / `GetByBusinessKey` ignore tenant (`PaymentRepositories.cs:48-65`). Unique indexes are `(Provider, EventId)` (`PaymentConfigurations.cs:30`).

**What.** Two tenants sharing a CHIP brand (same PEM) or a Xendit callback token:

- Replay of tenant A’s body to tenant B’s URL verifies (same secret). If A already logged the EventId, B hits the existing log (`HandleExistingLogAsync`) and may requeue **A’s** outbox or skip. B does not fulfill; A already did — or B stole the first processing slot and A’s later delivery is treated as a duplicate.
- If EventIds are globally unique per provider object (usually true), the second tenant cannot insert a second log for the same object. Shared-account multi-tenant is a first-writer-wins race, not isolation.

Stripe `evt_` ids are globally unique; the practical risk is CHIP/Xendit shared credentials.

### B04-P07 — P1 — Off-session success is webhook-only; `processing` / `pending_charge` are adapter-true

**Where.** `ExecuteOffSessionChargeIntegrationEventHandler` publishes nothing on success. Stripe `intent.Status == "succeeded" || "processing"` (`289`). CHIP `status == "paid" || "pending_charge"` (`311`).

**What.** A Stripe PI in `processing` that later fails publishes `PAYMENT_FAILED` (good, different EventId). Until then Commerce has no completed event and the adapter already returned true to the inbox handler (which does not tell Commerce it succeeded). A CHIP `pending_charge` that never becomes `purchase.paid` is a silent hole: adapter true, no completed webhook, subscription renewal hangs. This is the designed loop; it is still a bug when `pending_*` is treated as success at the adapter.

### B04-P08 — P1 — Late `PAYMENT_FAILED` after `PAYMENT_COMPLETED` on the same object still publishes

**Where.** Handler has no “if completed already exists for this `GatewayTransactionId`, ignore fail” check. EventIds after `a1afc09` are different, so the fail is fresh.

**What.** Billplz replay of `paid=false` after pay. CHIP `purchase.payment_failure` after `purchase.paid`. Xendit `EXPIRED` after `PAID` (rare). Payments will emit `GatewayPaymentFailed` after completed. M2M ignores it if already completed (good). Commerce is out of scope; the cashier still lies that the payment failed.

### B04-P09 — P1 — Razorpay EventId fallback is still the payment id

**Where.** `RazorpayGatewayAdapter.cs:138-149`, `336-343`. `a1afc09` did not touch this file.

**What.** Missing `X-Razorpay-Event-Id` on both `payment.failed` and `payment.captured` for the same `pay_…` → same EventId → `GetByEventId` finds the fail → `HandleExistingLogAsync` AlreadyActive → **completed dropped**. Same 008 P0 shape, residual rail. Header is usual; fallback is the hole. No test for fail-then-capture **without** the header.

### B04-P10 — P1 — Off-session fail `GatewayTransactionId` is `off_session:{subscriptionId}`

**Where.** `ExecuteOffSessionChargeIntegrationEventHandler.cs:149-152`.

**What.** Every fail for that subscription shares the same transaction id. Not logged in `PaymentWebhookLog` today. Any consumer that keys on it (or a future Payments-side dedupe) collapses attempts. `ChargeAttemptId` is only metadata.

### B04-P11 — P1 — Razorpay `SetupFutureUsage` still mints a card registration link

**Where.** `RazorpayGatewayAdapter.cs:58-82`. `SupportsOffSession("RAZORPAY")` is false. `SupportsEmandate` is false. `method` is `"card"`.

**What.** The adapter does what the port asks. The product says reminder-only. Hop-2 is a card-registration UX whose tokens Commerce refuses to persist (Commerce, out of scope). At this layer: we claim e-mandate nowhere in C#; we still run the registration-link API. `max_amount = amountPaise * 10` authorizes 10× the first charge as the card-mandate ceiling.

### B04-P12 — P1 — Razorpay `invoice.expired` mapped as payment-failed via the payment entity

**Where.** `IsPaymentFailedEvent` includes `invoice.expired` (`301-302`). `MapPaymentFailed` reads `payload.payment.entity` (`327-330`).

**What.** Expire payloads without a payment entity and without `X-Razorpay-Event-Id` are `Verified=false` → 500 → retry storm. With the header, we publish `PAYMENT_FAILED` for a registration-link / invoice expiry that may not be a payment. Dropped type `payment.authorized` is the complementary hole (auto-capture off).

### B04-P13 — P1 — Refund loop is adapter bool; Stripe `pending` is success; only Stripe has an idempotency key

**Where.** `StripeGatewayAdapter.cs:313, 354-360`; CHIP `325-355`; Razorpay `280-294`; Xendit `119-148`; `GatewayRefundRequestedIntegrationEventHandler.cs:48-72`. CHIP subscribe list includes `payment.refunded` (`UpdatePaymentConfigCommandHandler.cs:133`); parser drops it.

**What.** Unchanged from 008. Dashboard refunds never enter. Fee reclaim is always 0. Worker retry of CHIP/Razorpay/Xendit `IssueRefundAsync` can double-refund.

### B04-P14 — P1 — Xendit refund posts `invoice_id`; API often wants a payment id

**Where.** `XenditGatewayAdapter.cs:126-131`. `GatewayTransactionId` is the invoice id (`327`). `RequiresMarkRefunded("XENDIT")` is false.

**What.** Unsoaked. Failure is at least visible (`GatewayRefundFailed`). There is no mark-refunded escape hatch. No refund test exists for Xendit.

### B04-P15 — P1 — Currency invented or case-split

| Rail | Generate | Webhook |
|------|----------|---------|
| Stripe | `ToLowerInvariant()` | `session.Currency ?? "myr"` (invent + lower) |
| CHIP | unused on generate | `purchase.currency ?? "MYR"` (invent) |
| Billplz | unused | hardcoded `"MYR"` |
| Razorpay | `ToUpperInvariant()` | fail closed, then `ToUpperInvariant()` |
| Xendit | `(currency ?? "MYR").ToUpperInvariant()` (invent on generate) | fail closed, then upper |

**What.** Stripe/CHIP invent a currency when the processor omitted one. Razorpay/Xendit webhook refuse. Stripe events are lowercase `myr`; everyone else tends to `MYR`. This module publishes the string as-is. Case-sensitive consumers (ledger, tax) are other slices; the cashier is the source of the split.

### B04-P16 — P1 — Xendit callback token is a shared secret, not a body signature

**Where.** `VerifyCallbackToken` (`240-256`). No HMAC of `rawBody`. No timestamp.

**What.** Stolen token + any JSON with `status=PAID` and a new `id` is `PAYMENT_COMPLETED`. Length-mismatch compare is not constant-time. Same class of integration as many Xendit docs; still a Payments-layer fact.

### B04-P17 — P1 — Minor-units policy is three-way and quantity is applied differently

**Where.** `GatewayCommon.ToMinorUnitsRounded` (banker's, CHIP/Xendit); `ToMinorUnitsTruncating` (Billplz/Razorpay); Stripe checkout `amount * 100` decimal; Stripe off-session/refund `(long)(amount * 100)` truncate.

**What.** `10.005` MYR: CHIP banker's `1000` sen; Billplz truncate `1000`; Stripe checkout `1000.5` sen; Stripe refund `1000`. Quantity: Stripe is unit × line qty; others pre-multiply `amount * quantity * 100`. Callers that pass a line total **and** `quantity > 1` double-count on CHIP/Billplz/Razorpay/Xendit. The query comment says line-total callers must pass `quantity = 1` (`GenerateCheckoutSessionQuery.cs:10-11`). M2M hard-codes `quantity: 1` (`CreateIntegrationCheckoutCommandHandler.cs:147`). Commerce hop 2 passes product quantity (`InitiateCheckoutCommandHandler.cs:359`) with **unit** amount — correct if every adapter obeys the comment. Stripe does. The others fold qty into one product line — also correct **if** amount is unit. The hazard is a future caller passing a line total into CHIP with qty > 1.

Zero-decimal currencies (JPY, KRW) are `* 100` on every rail. Not a MY launch bug; a latent one.

`CheckoutAmountRules.MyrMinimum = 2.00` applies only to M2M (`CreateIntegrationCheckoutCommandHandler.cs:189`). Commerce `$0` setup bypasses it. Honest.

### B04-P18 — P2 — Empty webhook body is HTTP 500

**Where.** `Endpoints.cs:45-48`, catch at `84-88` rethrows `InvalidOperationException`.

**What.** Bad sender / health check / empty retry storms the error log and the gateway retry queue. Not lost money.

### B04-P19 — P2 — CHIP webhook auto-register duplicates; verify key may not be `Webhook.public_key`

**Where.** `UpdatePaymentConfigCommandHandler.cs:116-138`. No GET-list. PEM from `GET /public_key/` (company), not from the created webhook object. CHIP docs: webhook deliveries use a dedicated key pair.

**What.** Re-save → N webhook rows at CHIP, N deliveries of the same event (EventId dedupes after the first). Wrong PEM → every delivery `Verified=false` → 500. Unsoaked; residual.

### B04-P20 — P2 — Stripe setup `PAYMENT_COMPLETED` with null token if SetupIntent expand fails

**Where.** `StripeGatewayAdapter.cs:107-125`. Catch logs warning, continues, still returns `PAYMENT_COMPLETED` amount 0 (`130-146`).

**What.** Buyer finished setup. We tell Commerce “paid / vaulted” with no PM. Commerce vault persist requires both ids (other slice). Subscription may activate reminder-only after a setup checkout. `setup_intent.succeeded` is not a backup map.

### B04-P21 — P2 — Stripe / CHIP fee expand failure is silent `GatewayFee=0`

**Where.** Stripe `99-102`, `182-186`; CHIP missing `payment` node leaves fee 0 (`185-192`); Billplz always 0 (B04-P — 008 leftover, still true).

**What.** Ledger net = gross. Honesty, not fulfillment.

### B04-P22 — P2 — Dropped event types (wrong mapping / swallowed)

| Source | Mapped? | Effect |
|--------|---------|--------|
| CHIP `purchase.preauthorized` | passthrough | B04-P01 |
| CHIP `payment.refunded` | passthrough | B04-P13 |
| Stripe `charge.refunded` / `refund.*` | passthrough | B04-P13 |
| Stripe `setup_intent.succeeded` | passthrough | B04-P20 |
| Stripe `checkout.session.async_payment_*` | passthrough | latent if APMs added |
| Stripe `charge.dispute.created` without `Dispute` object | passthrough | lost dispute |
| Razorpay `payment.authorized` | passthrough | unpaid if no auto-capture |
| Razorpay `refund.*` | passthrough | B04-P13 |
| Xendit `PENDING` | passthrough | ignored |
| Billplz any non-paid | `PAYMENT_FAILED` | B04-P08 if late |

Parse exceptions in CHIP / Billplz / Razorpay / Xendit are caught and returned `Verified=false` (retry). Stripe non-`StripeException` is not caught (500). Handler does not distinguish “bad signature” from “malformed JSON we already verified” — both 500.

Dispute vs refund vs fail: Stripe dispute is the only inbound dispute. It is **not** mapped as a refund (correct; `e18edbe` stopped Commerce booking chargebacks as refunds — other slice). No rail maps a chargeback as `PAYMENT_FAILED`.

### B04-P23 — P2 — M2M amount is `double` on the wire

**Where.** `IntegrationEndpoints.cs:45` `(decimal)body.Amount`; response `(double)result.Amount` (`154`). NSwag DTO `CreateIntegrationCheckoutRequestDto.Amount` is `double`.

**What.** Binary floating point on money at the HTTP edge. Internal command is `decimal`. Typical MYR 2-dp values survive; 3-dp / repeating fractions do not.

### B04-P24 — P2 — Dead / unused in this module

- `ChipCollectGatewayAdapter._configuration` (injected, never read).
- `BillplzPublicBase.ProductionHosts` (filled, discarded).
- `SupportsDuitNowQr` / `SupportsHostedWallet` / `SupportsEmandate` (no Payments readers).
- `xendit_payment_methods` (no Payments/Commerce setter).
- Razorpay `ChargeOffSessionAsync` email/phone branch.
- Estimated fee parameters on `ParseWebhookAsync` (handler always 0).
- Payments README §3 “stateless checkouts”, §6 two adapters, overview “FPX, Curlec”.

### B04-P25 — P2 — Integration checkout GET lazy-expires only while `open`

**Where.** `GetIntegrationCheckoutQueryHandler.cs:31-35`; `TryExpireIfPast` (`IntegrationCheckoutSession.cs:125-134`).

**What.** A `failed` session past TTL stays `failed` (good). An `open` session past 24h becomes `expired` on GET. Webhooks after expire: M2M handler still requires `open` — a late pay on an expired session is dropped. Buyer can pay a 25-hour-old bill; M2M outbound never fires. Related to B04-P02 (terminal states swallow completed).

### B04-P26 — P2 — Placeholder PII on generate

**Where.** `GatewayCommon.PlaceholderEmail = "customer@example.com"`; CHIP/Billplz/Xendit `ResolveEmail`. Razorpay phone `+60100000000`.

**What.** Blank buyer email becomes a real processor customer record on the tenant account. Not a verify skip; it is a data-quality / support bug.

---

## 12. Lying tests and tests that lock the hole

Twenty fixtures under `tests/Lazuar.ModuleTests/Payments/`.

### 12.1 Tests that assert the wrong production invariant

1. **`Handle_UniqueConstraintRace_Returns_WithoutRethrow`** (`ProcessGatewayWebhookCommandHandlerTests.cs:247-280`). Mock `IEventBus` + `SaveChanges` throws a string containing `23505`. Asserts `PublishAsync` was received **and** no rethrow. Production `OutboxEventBus` only `Add`s; the 23505 rolls back the outbox row. The test documents “we published then swallowed”, which is **not** what EF does. It does not prove the HTTP 200 + single-fulfillment race.

2. **`Handle_PaymentCompleted_Merges_SessionMetadata_By_ProviderSessionId`** (`297-380`). Stubs `EventId: billId` (bare object id). Real Billplz adapter now emits `PAYMENT_COMPLETED:billId`. The test still passes because the stub is not the adapter. It does not prove merge still works with namespaced EventId (it should — merge keys on `GatewayTransactionId` — but it does not say so).

3. **`GenerateCheckout_WithCheckoutId_AppendsQueryParam`** (`BillplzGatewayAdapterTests.cs:147-179`). Comment admits it cannot assert the URL without HTTP. Asserts `Success == false` (no mock HTTP). Name claims the query param is appended. It is not asserted.

4. **`ToMinorUnitsRounded_MatchesChipRound`** (`GatewayCommonTests.cs:48-54`). Asserts `ToMinorUnitsRounded(10.005m) == (int)Math.Round(10.005m * 100m, 0)`. Tautology. Does not name banker's `ToEven` or compare to Billplz truncate on the same input (the truncate test uses `10.009m`, a different fixture).

5. **`SupportsApiRefund_StripeChipRazorpay`** (`PaymentGatewayCapabilitiesTests.cs:32`). Name omits Xendit; the cases include Xendit true. Cosmetic lie.

6. **`NonStripeAdapters_DoNotSendApplePayOrPaymentMethodTypes`** (`StripeGatewayAdapterTests.cs:90-114`). Reads `.cs` files as text. A future CHIP `payment_method_whitelist` that happens to include those strings would fail the test without proving runtime behaviour. Weak, not false today.

7. **`ParseWebhook_CheckoutSessionCompleted_SetupIntentWithoutPi_ExtractsCustomerAndPaymentMethod`**. JSON embeds an expanded SetupIntent. Does **not** test the fetch-on-unexpanded path or the expand-failure path that still returns `PAYMENT_COMPLETED` with null token (B04-P20).

### 12.2 Tests that lock a bug in place

8. **`ParseWebhook_Preauthorized_IsVerified_NotPaymentCompleted`**. Correct for money. Locks B04-P01 for `$0` vault. After `8b3567d` this test is the reason nobody extracted vault ids from preauthorized.

9. **`Failed_AlreadyFailed_NoSecondPublish`** / completed-already-completed tests in `IntegrationCheckoutOutboundWebhookTests`. Correct for duplicates. Together with the missing fail-then-completed test, they lock B04-P02.

### 12.3 Coverage that does not exist (unread by tests)

- CHIP generate `skip_capture` / `force_recurring` payload.
- CHIP fail-then-pay through **real** `ParseWebhookAsync` + handler + **M2M session**.
- CHIP `ChargeOffSessionAsync` (HTTP sequence, pending_charge, GET-by-token).
- Razorpay `ChargeOffSessionAsync`.
- Razorpay fail-then-capture **without** `X-Razorpay-Event-Id`.
- Razorpay `invoice.expired` without `payload.payment`.
- Xendit `IssueRefundAsync` body (`invoice_id`).
- Xendit `VerifyCallbackToken` different-length / replay.
- Stripe `IssueRefundAsync` pending = true (only the key formatter is tested).
- Stripe `ChargeOffSessionAsync` `processing` = true (only request-options helpers).
- Handler “late fail after completed still publishes”.
- Handler tenant_id mismatch URL vs metadata.
- `GetByEventId` cross-tenant.
- `CheckoutAmountRules` (no Payments test fixture).
- Platform CHIP/Xendit `tenant_id` overwrite.
- Empty webhook body status code.

`PaymentWebhookLogRepositoryTests` cover requeue / get-by-event-id on in-memory EF. They do not cover unique-index behaviour (in-memory often does not enforce the same uniques as Npgsql).

`ExecuteOffSessionChargeIntegrationEventHandlerTests` are the strongest fixture in the folder (capability short-circuit, decline code, exception swallow, idempotency key). They do not assert “success publishes nothing”.

---

## 13. Unread paths (code that compiles and is not driven)

| Symbol | Readers in Payments |
|--------|---------------------|
| `SupportsDuitNowQr` | Tests only |
| `SupportsHostedWallet` | Tests only |
| `SupportsEmandate` | Tests only (`XENDIT` case) |
| `xendit_payment_methods` | `XenditGatewayAdapter.ResolveRequestedPaymentMethods` only; no caller sets the key |
| `ParseWebhookAsync` fee args | Always 0 from the handler |
| `ChipCollectGatewayAdapter._configuration` | None |
| `BillplzPublicBase.ProductionHosts` | Assigned, unused |
| Razorpay `notes["customer_email"]` in off-session | Dead; notes never contain it |
| `GatewayRefundRequestedIntegrationEvent.GatewayName` default `"STRIPE"` | Callers must pass through; default is unused if they do |
| `ExecuteOffSessionChargeIntegrationEvent.GatewayName` default `"STRIPE"` | Same |
| CHIP `payment.refunded` registration | Parser ignores |
| Payments README adapters list | Humans only |

Commerce / ops / portal readers of the capability flags are other slices. Inside this slice they are a matrix, not a product.

---

## 14. Ranked open bugs

P0 first. Fixed 008 items are not listed as open.

1. **B04-P01 (P0)** — CHIP `$0` `skip_capture` never emits `purchase.paid`; `purchase.preauthorized` dropped; no vault. `8b3567d` turned this path on from Commerce.
2. **B04-P02 (P0)** — M2M `IntegrationCheckoutSession` treats fail as terminal; `a1afc09` now delivers the later completed event and the session layer drops it.

3. **B04-P04 (P1)** — CHIP off-session double-charge window (no idempotency key).
4. **B04-P03 (P1)** — CHIP off-session GET purchase by `recurring_token`.
5. **B04-P05 (P1)** — CHIP/Xendit overwrite paying `tenant_id` (platform checkout).
6. **B04-P13 (P1)** — Refunds are adapter HTTP; Stripe `pending` = success; no inbound refund type; no idempotency except Stripe.
7. **B04-P08 (P1)** — Late fail after completed still published (Billplz unpaid replay).
8. **B04-P09 (P1)** — Razorpay EventId fallback still collides fail/capture.
9. **B04-P06 (P1)** — EventId unique and lookup are not tenant-scoped; no metadata/URL tenant check.
10. **B04-P14 (P1)** — Xendit refund `invoice_id` unsoaked; no mark-refunded hatch.
11. **B04-P16 (P1)** — Xendit token replay / forgery.
12. **B04-P07 (P1)** — `processing` / `pending_charge` adapter-true; completed only via webhook.
13. **B04-P10 (P1)** — Off-session fail transaction id not unique per attempt.
14. **B04-P11 (P1)** — Razorpay registration-link generate on `SetupFutureUsage` while capability is reminder-only.
15. **B04-P12 (P1)** — Razorpay `invoice.expired` mapping / retry storm.
16. **B04-P15 (P1)** — Currency invent + case split (Stripe `myr`, CHIP default MYR).
17. **B04-P17 (P1)** — Three minor-unit policies; quantity contract is comment-only.

18. **B04-P18 (P2)** — Empty webhook body 500.
19. **B04-P19 (P2)** — CHIP duplicate webhook rows; company PEM vs `Webhook.public_key`.
20. **B04-P20 (P2)** — Stripe setup completed with null token on expand fail; `setup_intent.succeeded` dropped.
21. **B04-P21 (P2)** — Fee expand → 0; Billplz fee always 0.
22. **B04-P22 (P2)** — Dropped types (`payment.authorized`, async payment, refund webhooks).
23. **B04-P23 (P2)** — M2M `double` amount.
24. **B04-P24 (P2)** — Dead fields, unread flags, stale Payments README.
25. **B04-P25 (P2)** — Late pay after M2M TTL expire dropped.
26. **B04-P26 (P2)** — Placeholder email / dummy phone.

---

## 15. What 008 P0 #1 looks like in the tree **now** (so nobody “re-fixes” the wrong layer)

`a1afc09` changed three lines of adapter EventId assignment:

```177:177:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs
            var eventId = $"{mappedEventType}:{purchaseId}";
```

```235:235:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs
                EventId: $"{(isPaid ? "PAYMENT_COMPLETED" : "PAYMENT_FAILED")}:{billId}",
```

```324:324:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/XenditGatewayAdapter.cs
            EventId: $"{mapped}:{invoiceId}",
```

`GatewayTransactionId` stayed the object id. Unique `(Provider, EventId)` now holds two rows for fail-then-pay. `ProcessGatewayWebhookCommandHandler` publishes both. That is done.

The remaining money bugs on the same story are **not** EventId collisions:

- CHIP `$0` never produces a typed event at all (B04-P01).
- M2M session refuses to leave `failed` (B04-P02).
- Razorpay without the Event-Id header still collides (B04-P09).
- A late fail is a **new** EventId and is accepted (B04-P08).

Do not namespace EventId a second time as the fix for B04-P01 or B04-P02. B04-P01 needs a CHIP setup-equivalent (map preauthorized + recurring token as vault-complete with amount 0, or capture `$0`, or stop setting `skip_capture` without a parse path). B04-P02 needs the session to allow `failed → completed` (and not `completed → failed`).

---

## 16. Honest sentence for this slice after `297ba98`

Lazuar Pay wraps five BYOK hosted checkouts. Inbound verify is real on all five; there is no signature skip. CHIP / Billplz / Xendit EventIds are namespaced by mapped type so fail-then-pay is no longer dropped at `PaymentWebhookLog`. Stripe `$0` vault uses Checkout `mode=setup` and extracts customer + PM from the SetupIntent. CHIP `$0` vault still sets `skip_capture` and still ignores `purchase.preauthorized`, so a buyer who completes that purchase is invisible to Lazuar. M2M sessions still treat the first fail as terminal and will not emit `payment.completed` after a later pay. Silent renewals are attempted only on Stripe (idempotent) and CHIP (not idempotent). Billplz / Razorpay / Xendit are reminder-only at the capability matrix. Razorpay generate still opens a card registration link when asked to set up future usage. Refunds are adapter HTTP; only Stripe has a refund idempotency key; no rail maps a refund webhook. Apple Pay / Google Pay are Stripe `card` wallets. We do not do FPX e-mandate. We do not draw DuitNow QR or wallet buttons. Fees on Billplz are always zero. Currency strings are not normalized.

That is the Payments-module truth on 17 August 2026 at `297ba98`.
