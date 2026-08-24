# 04 — Hub Payments MODULE adapter seam (the contract we steal HTTP judgment FROM)

**Family:** 014-evals  
**Slice:** Hub Payments port, factory, capabilities, DI, checkout, webhook, refund, off-session, customer portal — who calls the adapters, what cathedral runs AFTER, what must not be copied into `apps/lazuar-pay`.  
**Date:** 24 August 2026  
**Type:** Uncondensed analysis. **Not an implementation.** **Not** a flip of 011/11 cells. **Not** a per-PSP HTTP extract (Stripe / CHIP / Billplz / Xendit / Razorpay live in 05–07).

**Repos and SHAs (this write-up):**

| Tree | Path | Branch | HEAD |
|------|------|--------|------|
| Pay (this tree) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `main` | `ee2db8e5758305089a38298456c456d6bf0e97ca` (`ee2db8e5`) — `feat(pay): Bar B receipts, webhook secret, merchant money UI` |

Parent index: [README.md](./README.md). Standing law this paper restates, not re-argues: [013/06](../013-prods/06-money-rails.md) item 3 (factory of five is the Hub lie), item 11 (steal HTTP judgment). Historical: [008/02](../008-evals/02-payments-adapters-rails.md) — live files on this SHA win when they disagree.

This paper owns the **common contract and call graph**. Other 014 agents own per-rail HTTP.

---

## 0. How to read this paper

The Hub Payments module is a **cashier port**. Five classes implement one interface. A factory looks them up by uppercase name. Callers never construct `StripeGatewayAdapter` themselves; they ask `IPaymentGatewayFactory.GetAdapter`. After the adapter returns, Hub does **not** fulfill. It writes a `PaymentWebhookLog` row, publishes an integration event onto `PaymentsEventBus`, and waits for Commerce / Billing / Communications / Lhdn / an M2M outbound handler to do the product work.

That split is the cathedral. New Pay on 8081 already has `StripeHosted.CreateHostedUrlAsync`, `POST /v1/webhooks/{provider}/{orgId}`, and `Fulfillment.FulfillPaidAsync` in the **same request**. The job of this paper is to name the Hub seam so the steal is the HTTP decision, not the type graph.

**What “steal HTTP judgment” means here.** Copy the **decision** (how a rail verifies, which event is paid, which is vaulted, which is a no-op, which boolean is a lie). Do **not** copy `Modules.Payments.*`, MediatR, `IEventBus`, outbox/inbox jobs, `PaymentsDbContext` schema name `payments`, or `BuildingBlocks`. IsolationTests will fail a project reference. That is the point.

**What this paper does not do.** It does not dump Stripe Checkout options, CHIP RSA, Billplz HMAC, Xendit `x-callback-token`, or Razorpay payment-link bodies. Those extracts belong to 05–07. It does name, for each **interface method**, who calls it, which adapters implement vs throw/no-op, and which Hub fulfillment happens **after**.

---

## 0.1 Standing law that binds the seam (do not weaken)

From [013/06](../013-prods/06-money-rails.md) §0.1, live on this SHA:

1. **Steal adapters as HTTP judgment.** Do not copy the module, MediatR, `IEventBus`, outbox/inbox jobs, `PaymentsDbContext` schema name, or `BuildingBlocks`.
2. **One dogfood rail first** (Stripe is already on 8081). CHIP **or** Billplz is the Malaysian rail. Razorpay / Xendit stay later (`NP-LAT-002`).
3. **Factory of five is the Hub lie new Pay refuses on day one.** Registering five `IPaymentGatewayAdapter` implementations “because the factory already did” is how day-one became five.
4. **`PaymentGatewayCapabilities` is honest wrap-rails law.** The **matrix** may be restated in new Pay (ten lines next to the charge function). The **class file** `Modules.Payments.Contracts.PaymentGatewayCapabilities` must not be referenced. IsolationTests ban `Modules.`.
5. **Same-handler fulfillment.** Hub’s README is explicit that Payments is “not a fulfillment engine.” That split is the cathedral. New Pay’s webhook HTTP handler **is** the fulfillment entry.
6. **Never treat setup / setup-intent as paid** (`NP-GW-008`). Hub still emits `EventType: "PAYMENT_COMPLETED"` with `AmountPaid: 0` for setup-mode. Steal the HTTP extract of customer + PM. **Do not steal the event name.**
7. **Never Stripe Billing `subscription.updated` as source of truth** (`NP-XX-012`).
8. **`SupportsEmandate` is false for every name.** No homemade FPX e-mandate (`NP-XX-011`).

008/02 is historical. Live files on `ee2db8e5` win. Named residuals from 008 that **this SHA has already closed** (do not re-litigate as if they were the new design): empty webhook body is **400**; webhook log unique is `(OrganizationId, Provider, EventId)`; CHIP `purchase.preauthorized` with a token maps to `PAYMENT_COMPLETED` (still an `NP-GW-008` lie if copied as paid). Named residuals that **remain live Hub lies**: signature-fail bubbles as **500**; setup stuffed into `PAYMENT_COMPLETED`; factory of five; BILLPLZ last-resort when `requireActiveGateway` is false.

---

## 1. What the cashier README claims

File: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/README.md`.

> The `Payments` module is the gateway orchestrator. Live adapters are **Stripe**, **Billplz**, **CHIP**, **Razorpay**, and **Xendit**. FPX/DuitNow QR/hosted wallets/e-mandate capability flags exist on the matrix but have no generate-time readers — they are not product. It handles checkout mint, webhook verify, idempotency, and M2M integration checkouts.

Core responsibilities it lists: gateway orchestration (checkout + customer portal), webhook ingestion (raw HTTP, verify, parse), idempotency (`PaymentWebhookLog`), fee & tax extraction (Stripe expand / CHIP `payment`; Billplz journals are gross-only because estimated fee args on `ParseWebhookAsync` are unused).

Architectural boundaries it claims:

> **Not an Accounting Ledger.** This module does *not* calculate MRR, Net Profit, Recognized Revenue, or Tax Liabilities. It only reports the *Gross Amount* and *Gateway Fee* extracted from the provider.  
> **Not a Fulfillment Engine.** It does not activate Commerce subscriptions, unlock products, or manage subscription lifecycles. It only reports that a financial transaction occurred.  
> **Not checkout-stateless:** Machine (`/integrations/payments/checkouts`) sessions are stored as `IntegrationCheckoutSessions`. Commerce hop-2 still passes `subscription_id` through gateway metadata.

That last paragraph is the live correction of ADR 004 / ADR 009’s “Payments is completely stateless regarding checkout sessions.” Hub grew a session table because Billplz strips metadata. New Pay already has `/v1/checkouts`. **Do not grow `IntegrationCheckoutSessions` as a second product.**

Integration events the README names (the cathedral after the adapter):

- `GatewayPaymentCompletedIntegrationEvent`
- `GatewayPaymentFailedIntegrationEvent`
- `GatewayRefundCompletedIntegrationEvent`
- `GatewayRefundRequestedIntegrationEvent` (internal request, not a PSP callback)

Database schema the README names — all in isolated `payments`:

- `payments.TenantPaymentConfigurations`
- `payments.PaymentWebhookLogs`
- `payments.IntegrationCheckoutSessions`
- `payments.OutboxMessages`
- `payments.InboxMessages`

`PaymentsDbContext` confirms the schema name:

```32:34:apps/lazuar-api/Modules/Payments/Infrastructure/PaymentsDbContext.cs
        modelBuilder.HasDefaultSchema("payments");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly);
```

New Pay must **not** copy the schema name, the outbox/inbox tables, or `PlatformDbContext`. IsolationTests ban `BuildingBlocks`. The focused host already has `PayDbContext` with its own checkout / credential / webhook-event rows.

---

## 2. The port — `IPaymentGatewayAdapter` (full interface)

File: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs`.

There is **no** `Supports*` method on the interface. Capability is a static helper in Contracts, not a property of the adapter. That split is why a Razorpay adapter can still *implement* `ChargeOffSessionAsync` while `PaymentGatewayCapabilities.SupportsOffSession("RAZORPAY")` is false — the engine never calls it.

### 2.1 Result records

```8:29:apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs
public record GatewayCheckoutResult(bool Success, string? CheckoutUrl, string? SessionId, string? Error);

public record GatewayWebhookParsedResult(
    bool Verified,
    string EventType,
    string EventId,
    decimal AmountPaid,
    string Currency,
    string? GatewayTransactionId,
    Dictionary<string, string> Metadata,
    decimal GatewayFee,
    decimal TaxAmount,
    decimal NetAmount,
    decimal FxRate,
    string BaseCurrency,
    string? Error,
    string? GatewayCustomerId = null,
    string? GatewayTokenId = null,
    bool UnusableAfterVerify = false)
{
    public GatewayWebhookParsedResult AsUnusable() => this with { UnusableAfterVerify = true };
}
```

`GatewayCheckoutResult` is hop-2 mint: a URL + provider session id (`cs_…` / purchase id / bill id / payment-link id / invoice id), or `Success=false` plus an error string. There is no amount, no currency, no “this is setup not payment” flag. Setup vs payment is encoded **inside** each adapter’s generate body, then later stuffed into `PAYMENT_COMPLETED` at parse. New Pay must not copy that stuffing.

`GatewayWebhookParsedResult` fields that matter at the seam:

| Field | Job | Steal? |
|-------|-----|--------|
| `Verified` | Signature/HMAC/RSA/token check passed | **Yes** — fail closed |
| `EventType` | Hub’s five-name vocabulary: `PAYMENT_COMPLETED`, `PAYMENT_FAILED`, `DISPUTE_CREATED`, `DISPUTE_CLOSED`, `REFUND_COMPLETED`, or raw passthrough | **No** as a shared enum. New Pay wants `paid` / `failed` / `ignored` / `vaulted`. Do **not** steal `PAYMENT_COMPLETED` for setup. |
| `EventId` | Idempotency key into `PaymentWebhookLog` | **Yes** the idea (PSP event id). **No** inventing Guids. |
| `AmountPaid` / `Currency` | Money on the event | **Yes**, fail-closed currency |
| `GatewayTransactionId` | PI / purchase / bill / payment / invoice id | **Yes** |
| `Metadata` | Round-tripped context for Commerce / M2M | Steal `checkout_id` + `org_id`. Refuse Hub `subscription_id` as the only pointer. Refuse `hub_payment_environment` as a product key. |
| `GatewayFee` / `TaxAmount` / `NetAmount` / `FxRate` / `BaseCurrency` | Fee extraction | Steal “unknown ≠ 0”. Refuse estimated-fee columns (already deleted). Handler always passes `0, 0, 0`. |
| `GatewayCustomerId` / `GatewayTokenId` | Vault ids | Steal when a **real** PM/token exists. Not from setup counted as paid. |
| `UnusableAfterVerify` | Poison after verify (missing id/currency) | **Yes** — 400 so the PSP stops |
| `Error` | Human string | Fine |

There is **no** distinct `SETUP_COMPLETED` type. Setup is stuffed into `PAYMENT_COMPLETED`. That is a Hub lie (`NP-GW-008`).

### 2.2 Interface methods

```31:82:apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs
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
        
    Task<GatewayWebhookParsedResult> ParseWebhookAsync(
        string apiKey,
        string webhookSecret,
        string rawBody,
        Dictionary<string, string> headers,
        decimal estimatedFeePercentage = 0,
        decimal fixedFee = 0,
        decimal taxRate = 0);
        
    Task<bool> IssueRefundAsync(
        string apiKey,
        string transactionId,
        decimal amount);
        
    Task<string> GenerateCustomerPortalAsync(
        string apiKey,
        string customerEmail,
        string returnUrl);

    Task<bool> ChargeOffSessionAsync(
        string apiKey,
        string customerId,
        string tokenId,
        decimal amount,
        string currency,
        string description,
        string receipt,
        Guid tenantId,
        Guid? dunningCampaignId = null,
        string? idempotencyKey = null,
        Guid? chargeAttemptId = null,
        decimal taxAmount = 0,
        string? taxType = null);
}

public interface IPaymentGatewayFactory
{
    IPaymentGatewayAdapter GetAdapter(string gatewayType);
}
```

Five methods plus a name. Every rail must implement all five even when four of them throw or return false. That is the factory-of-five tax: adding a rail means pretending it has a portal, a refund API, and an off-session charge.

`ParseWebhookAsync` still carries `estimatedFeePercentage` / `fixedFee` / `taxRate`. Production always passes zeros. The columns were removed in migration `20260705131411_RemoveAccountingOverrides`. The parameters remain as a scar. New Pay: do not put estimated-fee args on the parse function.

`IssueRefundAsync` returns `bool` only — no refund id, no reclaimed fee. `GatewayRefundRequestedIntegrationEventHandler` then publishes `GatewayRefundCompletedIntegrationEvent` with `RefundedFee = 0m` (“Policy: gateway fees stay with us”). That is Hub policy, not PSP truth.

`ChargeOffSessionAsync` takes `receipt` as a string that Hub fills with `SubscriptionId.ToString()`. New Pay has no Commerce subscription id. If off-session lands in V1, the idempotency key and the Pay checkout/charge id are the pointers.

`GenerateCustomerPortalAsync` is Stripe Billing Portal. Only Stripe implements it. The other four throw `InvalidOperationException`. **Not v1 dogfood.** Buyer magic-link portal is `NP-BUY-004` (V1), not this method.

Factory:

```5:26:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/PaymentGatewayFactory.cs
public class PaymentGatewayFactory : IPaymentGatewayFactory
{
    private readonly IEnumerable<IPaymentGatewayAdapter> _adapters;

    public PaymentGatewayFactory(IEnumerable<IPaymentGatewayAdapter> adapters)
    {
        _adapters = adapters;
    }

    public IPaymentGatewayAdapter GetAdapter(string gatewayType)
    {
        var normalizedType = gatewayType.ToUpperInvariant();
        var adapter = _adapters.FirstOrDefault(a => a.GatewayType == normalizedType);

        if (adapter == null)
        {
            throw new InvalidOperationException($"Payment gateway type '{gatewayType}' is not supported.");
        }

        return adapter;
    }
}
```

Uppercase, first match, throw. There is no Fiuu, Midtrans, Cashfree, SenangPay, PayPal, or Toyyib class. Unknown name is a 500 at the webhook endpoint unless the allow-list already 400’d (the endpoint catch special-cases `"is not supported"`). New Pay: allow-list at the door, no factory scan of five.

---

## 3. DI — how adapters are registered (the factory of five)

File: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/DependencyInjection.cs`.

```19:70:apps/lazuar-api/Modules/Payments/Infrastructure/DependencyInjection.cs
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        services.AddDbContext<PaymentsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "payments");
            }));

        services.AddScoped<ITenantPaymentConfigRepository, TenantPaymentConfigRepository>();
        services.AddScoped<IPaymentWebhookLogRepository, PaymentWebhookLogRepository>();
        services.AddScoped<IIntegrationCheckoutSessionRepository, IntegrationCheckoutSessionRepository>();
        services.AddScoped<Modules.Payments.Application.Services.CheckoutSessionCashier>();

        services.AddScoped<IPaymentGatewayAdapter, StripeGatewayAdapter>();
        services.AddScoped<IPaymentGatewayAdapter, BillplzGatewayAdapter>();
        services.AddScoped<IPaymentGatewayAdapter, RazorpayGatewayAdapter>();
        services.AddScoped<IPaymentGatewayAdapter, ChipCollectGatewayAdapter>();
        services.AddScoped<IPaymentGatewayAdapter, XenditGatewayAdapter>();
        services.AddScoped<IPaymentGatewayFactory, PaymentGatewayFactory>();

        services.AddHttpClient(PublicDnsFallback.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        }).ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.SocketsHttpHandler
        {
            ConnectCallback = PublicDnsFallback.ConnectAsync,
        });

        services.AddModuleOutboxInbox<PaymentsDbContext, PaymentsOutboxPublisherJob, PaymentsInboxConsumerJob>("PaymentsEventBus");

        services.AddOutboxSchemaMetrics("payments");

        services.AddTransient<GatewayRefundRequestedIntegrationEventHandler>();
        services.AddTransient<ExecuteOffSessionChargeIntegrationEventHandler>();
        services.AddTransient<IntegrationCheckoutGatewayEventsHandler>();

        return services;
    }

    public static IApplicationBuilder UsePaymentsSubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriptions>();

        eventBus.Subscribe<GatewayRefundRequestedIntegrationEvent, GatewayRefundRequestedIntegrationEventHandler>();
        eventBus.Subscribe<ExecuteOffSessionChargeIntegrationEvent, ExecuteOffSessionChargeIntegrationEventHandler>();
        eventBus.Subscribe<GatewayPaymentCompletedIntegrationEvent, IntegrationCheckoutGatewayEventsHandler>();
        eventBus.Subscribe<GatewayPaymentFailedIntegrationEvent, IntegrationCheckoutGatewayEventsHandler>();

        return app;
    }
```

This is the entire cathedral in one method:

1. Isolated `payments` schema + migrations history in that schema.
2. Three repositories + cashier.
3. **Five** adapters into `IEnumerable<IPaymentGatewayAdapter>` so the factory can scan them.
4. A named HttpClient `"Billplz"` with `PublicDnsFallback` (1.1.1.1 / 8.8.8.8 connect hook). Only Billplz uses it.
5. `AddModuleOutboxInbox` — BuildingBlocks outbox publisher + inbox consumer keyed `"PaymentsEventBus"`.
6. Three in-process integration-event handlers.

Host composition (`ModuleRegistrationExtensions.cs`) wires the module into the modular monolith:

```27:27:apps/lazuar-api/src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs
        services.AddPaymentsModule(configuration);
```

```41:41:apps/lazuar-api/src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs
        app.UsePaymentsSubscriptions();
```

```71:72:apps/lazuar-api/src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs
        apiGroup.MapPaymentsEndpoints();
        apiGroup.MapPaymentsIntegrationEndpoints();
```

```86:86:apps/lazuar-api/src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs
        platformGroup.MapPlatformEndpoints();
```

Public Hub paths (under `/api/v1`):

| Map | Path | Auth |
|-----|------|------|
| `MapPaymentsEndpoints` | `POST /webhooks/payments/{gatewayType}/{tenantId}` | Anonymous (PSP HMAC/RSA/Stripe-Signature) |
| `MapPaymentsIntegrationEndpoints` | `POST/GET /integrations/payments/checkouts`, `GET /integrations/payments/me` | Machine scopes |
| `MapPlatformEndpoints` | `GET/PUT /api/v1/platform/payment-config` | Hub `SUPER_ADMIN` |
| Commerce `MapPaymentConfigEndpoints` | `GET/PUT /admin/commerce/payment-config` | Hub `OrgAdmin` |

New Pay already maps `PUT/GET /v1/orgs/{orgId}/gateway` and `POST /v1/webhooks/{provider}/{orgId}`. Do not add `/api/v1`. Do not add `/admin/commerce`. Do not add `/integrations/payments`. `NP-SOON-007` is a later second consumer of the **same** `/v1/checkouts`.

Application layer DI is a **marker class** for MediatR assembly scanning:

```1:8:apps/lazuar-api/Modules/Payments/Application/DependencyInjection.cs
namespace Modules.Payments.Application;

/// <summary>
/// Marker class for MediatR assembly scanning and Architecture Tests.
/// </summary>
public static class DependencyInjection
{
}
```

IsolationTests on the focused host ban the substring `MediatR`. Copying this marker would fail those tests on purpose.

Compiled rail set (live types, `GatewayType` constants):

| Class | `GatewayType` | File |
|-------|---------------|------|
| `StripeGatewayAdapter` | `STRIPE` | `Infrastructure/Gateways/StripeGatewayAdapter.cs` |
| `ChipCollectGatewayAdapter` | `CHIP` | `Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` |
| `BillplzGatewayAdapter` | `BILLPLZ` | `Infrastructure/Gateways/BillplzGatewayAdapter.cs` |
| `RazorpayGatewayAdapter` | `RAZORPAY` | `Infrastructure/Gateways/RazorpayGatewayAdapter.cs` |
| `XenditGatewayAdapter` | `XENDIT` | `Infrastructure/Gateways/XenditGatewayAdapter.cs` |

Inbound webhook allow-list is the same five (`Endpoints.cs`). M2M checkout allow-list is the same five (`CreateIntegrationCheckoutCommandHandler.cs`). Factory throw is the sixth name. That is how Xendit got a URL before it was dogfood.

---

## 4. `PaymentGatewayCapabilities` — honest wrap-rails, unread flags, refuse the class file

File: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs`.

```1:58:apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs
namespace Modules.Payments.Contracts;

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

    public static bool IsReminderOnlyGateway(string? gatewayName) => !SupportsOffSession(gatewayName);

    public static bool SupportsApiRefund(string? gatewayName)
    {
        var g = Normalize(gatewayName);
        return g is "STRIPE" or "CHIP" or "RAZORPAY" or "XENDIT";
    }

    /// <summary>Hosted Xendit invoice / CHIP collect may show DuitNow QR. We do not render QR ourselves.</summary>
    public static bool SupportsDuitNowQr(string? gatewayName)
    {
        var g = Normalize(gatewayName);
        return g is "XENDIT" or "CHIP" or "BILLPLZ";
    }

    /// <summary>Wallets appear on the processor hosted page when the merchant enables them there.</summary>
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

    /// <summary>True FPX auto-debit. Off until Curlec/Xendit mandate tokens soak.</summary>
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

    private static string Normalize(string? gatewayName) => (gatewayName ?? "").Trim().ToUpperInvariant();
}
```

### 4.1 The matrix (restate in new Pay; do not reference this file)

| Question | Stripe | CHIP | Billplz | Razorpay | Xendit | blank / OFFLINE |
|----------|--------|------|---------|----------|--------|-----------------|
| Hosted checkout (buyer present) | Y | Y | Y | Y (parked) | Y (parked) | n/a |
| Vault + off-session | Y if PM exists | Y if `recurring_token` (cards, not FPX) | **N** | N (capability false; adapter still has HTTP) | N (returns false) | N |
| API refund | Y | Y | **N** | Y (later) | Y (later) | N |
| Mark-refunded SOP | N | N | **Y** | N | N | Y |
| Homemade FPX e-mandate | N | N | N | N | N | N |
| DuitNow QR as **our** pixels | N | N | N | N | N | N |
| Hosted wallet tiles as **our** UI | N (card wrap) | Flag true; no generate reader | Flag false for GrabPay (QR flag true) | N | Flag true; no generate reader | N |

Unknown / blank / `OFFLINE` = reminder-only. Never silent debit (`NP-GW-007`).

### 4.2 Who actually reads the flags (live `apps/` on this SHA)

| Flag | Readers | New Pay |
|------|---------|---------|
| `SupportsOffSession` / `IsReminderOnlyGateway` | `ExecuteOffSessionChargeIntegrationEventHandler` (short-circuit + `off_session_not_supported`); `BillingEngineJob` `canCharge`; `PastDueDunningProcessor` AUTO_CHARGE skip; `DunningCampaignAutoChargeGuard`; `DunningCampaignCommandHandlers`; `GatewayPaymentCompletedIntegrationEventHandler.Helpers` vault persist skip; `InitiateCheckoutCommandHandler` `$0` vault hop; `RenewalCheckoutIssuer`; `CommerceQueryService.Products` DTO; `PublicArrearsEndpoints` | Restate **next to the charge function** and a future renew job. Merchant GET `/v1/orgs/{id}/gateway` already returns `capability = "hosted_link"` for Stripe. Do not reimplement the matrix in Vite. |
| `SupportsApiRefund` / `RequiresMarkRefunded` | `RecordRefundCommandHandler`; `CommerceQueryService.Transactions` `supports_api_refund`; ops `RefundModal` | Paper 07 refund SOP. Billplz button is “mark refunded,” not “call PSP.” |
| `SupportsDuitNowQr` | **Tests + the static class.** Zero generate-path readers under `Modules/Payments/`. | Do not show a DuitNow toggle on `:5178`. |
| `SupportsHostedWallet` | **Tests + the static class.** | Do not show GrabPay tiles on `:5179`. PSP hosted page owns tiles. |
| `SupportsEmandate` | Tests + the static class. Always false. | Keep false. No Curlec `method=emandate`. |

Ops frontend duplicates off-session as `gatewaySupportsOffSession` (`apps/lazuar-ops/src/lib/utils.ts`) — Hub ops, not new merchant Vite. New merchant UI should take `supports_off_session` from **GET**, not re-code `STRIPE or CHIP`.

**Runtime reader inside Payments itself** is only the off-session handler:

```39:46:apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs
        if (!PaymentGatewayCapabilities.SupportsOffSession(@event.GatewayName))
        {
            _logger.LogWarning(
                "Off-session charge skipped for subscription {SubscriptionId}: gateway {GatewayName} does not support vaulted charges.",
                @event.SubscriptionId, @event.GatewayName);
            await PublishPaymentFailedAsync(@event, failureReason: "off_session_not_supported");
            return;
        }
```

That is wrap-rails enforced **before** `GetAdapter`. A Billplz name never reaches `ChargeOffSessionAsync`. A Razorpay name also never reaches the adapter’s *implemented* recurring-payment HTTP — capability false wins. The Razorpay method existing is a foot-gun if someone later “just calls the adapter.”

### 4.3 Refuse the class file, keep the law

New Pay restates four questions as a 10-line helper next to charge. It does **not**:

- `using Modules.Payments.Contracts;`
- ProjectReference `Modules.Payments.Contracts.csproj`
- Duplicate unread DuitNow/wallet flags as product chrome
- Grow `AUTO_CHARGE` as a dunning step string

IsolationTests:

```5:5:apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
    static readonly string[] Banned = ["lazuar-api", "Modules.", "BuildingBlocks", "MediatR", "Lazuar.Api"];
```

A project reference whose path contains `lazuar-api`, or source that contains `Modules.`, fails. That is the tripwire for copying this class.

---

## 5. Shared helpers at the seam (`GatewayCommon`, `PublicDnsFallback`)

### 5.1 `GatewayCommon` — steal the decisions, not the type

File: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs`.

Internal static. Adapters call it; there is **no** abstract base class and **no** shared HTTP client for all five rails. That part is honest. New Pay should also not invent `PaymentGatewayAdapterBase`.

Judgment worth stealing:

| Helper | Decision | New Pay |
|--------|----------|---------|
| `PlaceholderEmail = "customer@example.com"` + `IsUsableBuyerEmail` / `TryResolveEmail` / `ResolveEmail` | Never send the placeholder to a processor. Fail closed. | Steal. CHIP/Billplz generate already refuse it. |
| `ExtractName` | Local-part of email, else `"Customer"` | Fine |
| `ProductDescription` | Quantity suffix; default `"Lazuar Payment"` | Fine; new StripeHosted currently hard-codes `Name = "Pay"` |
| `ToMinorUnits` | Half away from zero; zero-decimal ISO currencies not ×100 | Steal as money math. New `StripeHosted` currently does `amount * 100` with `AwayFromZero` for all currencies — Hub’s zero-decimal list is the fuller judgment. |
| `ToMinorUnitsRounded` vs `ToMinorUnitsTruncating` | Both now delegate to `ToMinorUnits` (live; 008 remembered a banker/truncate split) | Live files win. One money policy. |
| `TryNormalizeCurrency` | Fail-closed: blank / not 3 letters → false. No invented MYR. | Steal. Billplz-only hardcode `MYR` is acceptable **for Billplz only**. |
| `StampGatewayFeeStatus` / `gateway_fee_status` | `known` vs `unknown`. Zero fee + unknown is not “the fee is zero”. | Steal the stamp (`NP-MON-002`). |
| `FormatRefundIdempotencyKey` | `lazuar-refund:{txn}:{minor}` | Steal the idea when refunds exist. Do not copy the prefix as a Hub brand. |
| `ApplyPayingTenantMetadata` | Keep existing `tenant_id`; stamp `platform_tenant_id` when adapter tenant differs (platform / system org charges) | **Refuse `platform_tenant_id` and `SystemOrganizationId`.** New Pay has no system org. Stamp `org_id` + `checkout_id`. |

`ApplyPayingTenantMetadata` is the Hub platform-checkout lie in one function. `GenerateSystemCheckoutSessionQueryHandler` uses platform keys under `PlatformCheckoutTypes.SystemOrganizationId = 00000000-0000-0000-0000-000000000001`. New Pay is not billing Hub. Refuse.

### 5.2 `PublicDnsFallback` — Billplz-only LAN folklore

File: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/PublicDnsFallback.cs`.

Comment: “HttpClient connect hook that resolves hosts via 1.1.1.1 / 8.8.8.8 when the machine resolver cannot (common for www.billplz-sandbox.com on some LANs).”

Named client `"Billplz"`. UDP DNS query encoder/decoder. Fallback to `Dns.GetHostAddressesAsync`. **Only Billplz’s adapter uses this named client.** Park unless dogfood proves Hub DNS is still a problem on the laptop that will run 8081. Do not copy as production policy. Do not run a homemade DNS client in Pay.

### 5.3 `BillplzPublicBase` (seam-level, not the HMAC extract)

Fail-closed public HTTPS callback. Refuses loopback, `lazuar-local-dev.com`, and non-HTTPS unless `App:AllowInsecureBillplzCallback`. Error token `CALLBACK_BASE_NOT_PUBLIC`. Live vs sandbox host follows `App:BillplzEnvironment` then tenant `environment`, **not** Hub hostname (`pay-local.lazuar.com` must never go live).

Steal the **fail-closed public origin** for **any** rail whose PSP cannot POST to localhost. Do not steal the CHIP registrar’s opposite move (rewrite localhost → `lazuar-local-dev.com` on key save). Billplz public-base would refuse that host. New Pay: ngrok/tunnel for local; public HTTPS for staging; no fiction DNS.

### 5.4 `ChipWebhookRegistrar` (seam-level)

On CHIP key **save** (not generate), `UpdatePaymentConfigCommandHandler` may `EnsureRegisteredAsync`: list existing callbacks (idempotent on URL), POST `purchase.paid` / `purchase.payment_failure` / `payment.refunded` / `purchase.preauthorized`, prefer `Webhook.public_key`, fall back to company `GET /public_key/`. Then it rewrites localhost → `lazuar-local-dev.com`.

Steal: list-before-create; register Pay’s **public** `/v1/webhooks/chip/{orgId}`; prefer webhook PEM over company key. Refuse: localhost rewrite as production policy; registering Hub `/api/v1/webhooks/payments/chip/{tenantId}`.

---

## 6. Domain at the seam (three aggregates/entities)

### 6.1 `TenantPaymentConfiguration`

File: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Domain/Aggregates/TenantPaymentConfiguration.cs`.

Per `(OrganizationId, GatewayType)` unique (`PaymentConfigurations.cs`). Fields:

| Field | Job | Steal? |
|-------|-----|--------|
| `ApiKey` | AES-encrypted gateway secret (base64 IV+ciphertext). Stripe `sk_…` lives here (`SecretKey` form field maps into this column). | Steal “encrypted at rest, never GET plaintext.” New Pay already uses `SecretBox` AES-GCM + `Pay:WrapKey`. Do not import `AesSecretVault`. |
| `WebhookSecret` | AES-encrypted signing secret / PEM / X-Signature key | Steal the column idea. New Pay Bar B currently uses **process** `Pay:StripeWebhookSecret` (one secret for the host), not per-org webhook secret. That is a honesty gap 08 owns; the Hub seam’s judgment is per-tenant. |
| `MerchantId` | Brand ID / Collection ID, **plaintext** | Steal |
| `IsActive` | Soft-disable. Credentials retained. New checkouts/charges skip. **Webhooks still process.** | Steal that comment. Disable ≠ throw away paid money. |
| `Environment` | `test` \| `live`. New rows default test. Owns Billplz host selection. | Steal. Do not stamp Hub metadata key `hub_payment_environment`. |

`IMustHaveTenant` + `IAggregateRoot` + `Entity` are BuildingBlocks. Refuse those interfaces. New Pay rows are POCOs in `PayDbContext`.

Repository **bypasses tenant query filters**:

```23:28:apps/lazuar-api/Modules/Payments/Infrastructure/Repositories/PaymentRepositories.cs
    public async Task<TenantPaymentConfiguration?> GetByTenantAndGatewayAsync(Guid tenantId, string gatewayType, CancellationToken ct = default)
    {
        return await _context.TenantPaymentConfigurations
            .IgnoreQueryFilters() // Bypass tenant isolation so the creator can read the system's public keys
            .FirstOrDefaultAsync(c => c.OrganizationId == tenantId && c.GatewayType == gatewayType.ToUpperInvariant(), ct);
```

The comment is the platform/system-org story (`SystemOrganizationId`). New Pay has no query filters of that kind and no system org. Filter by `org_id` in the WHERE and stop.

### 6.2 `IntegrationCheckoutSession`

File: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Domain/Aggregates/IntegrationCheckoutSession.cs`.

M2M `/integrations/payments/checkouts` row. Statuses `open` / `completed` / `failed` / `expired`. Default TTL 24h. `SetupFutureUsage`, `ProviderSessionId`, `GatewayTransactionId`, `CheckoutUrl`, `MetadataJson`, idempotency key + request fingerprint.

**Do not copy this type into 8081.** The focused host already has a checkout row (`CheckoutRow` / fixture-grown table). One Pay checkout is enough. Hub grew this table because ADR 009 claimed Payments was stateless and Billplz stripped metadata. New Pay keeps **its** session row so bill-id lookup is a fallback, not a second product.

`TryExpireIfPast` lazy-expires open sessions. 009 B04-P25: Hub M2M dropping late pay on `expired` is a residual. New Pay: a late paid webhook on an expired **open** session should still fulfill (buyer paid). Do not copy the expire-then-drop.

### 6.3 `PaymentWebhookLog`

File: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Domain/Entities/PaymentWebhookLog.cs`.

Idempotency ledger. Live unique indexes (`PaymentConfigurations.cs`):

```29:34:apps/lazuar-api/Modules/Payments/Infrastructure/Configurations/PaymentConfigurations.cs
        builder.HasIndex(x => new { x.OrganizationId, x.Provider, x.EventId }).IsUnique();

        builder.HasIndex(x => new { x.OrganizationId, x.Provider, x.BusinessKey })
            .IsUnique()
            .HasFilter("\"BusinessKey\" IS NOT NULL");
```

009 B04-P06 “EventId not tenant-scoped” is **closed on this SHA**. Steal `(org_id, provider, event_id)`. New Pay already has `PspWebhookEventRow` keyed `(OrgId, Provider, EventId)` and short-circuits duplicates **before** fulfill.

`BusinessKey` = `EVENTTYPE:GatewayTransactionId` collapses Stripe `checkout.session.completed` + `payment_intent.succeeded` for the same PI. Refunds skip business key (`BuildBusinessKey` returns null for `REFUND_COMPLETED`) so partial refunds do not collapse. Steal the dual-event idea. Do not steal `EVENTTYPE:` Hub vocabulary if new parse uses `paid:{pi}`.

`OutboxMessageId` correlates the log row to `payments.OutboxMessages`. `HandleExistingLogAsync` requeues Dead outbox on redelivery. **That machine is the cathedral.** New Pay has no Payments outbox. Duplicate delivery is 200 + do not fulfill again. There is nothing to requeue.

`ProcessedAt` comment: “UTC time this webhook was received and domain work was queued (outbox insert). Not Commerce / Billing / session fulfillment.” That sentence is the cashier split. New Pay’s insert is in the **same transaction** as fulfill.

### 6.4 Ports file `IPaymentRepositories.cs`

The assigned path is one file containing two interfaces (not a third `IPaymentRepositories` type):

- `ITenantPaymentConfigRepository` — `GetByTenantAndGatewayAsync`, `GetAllByTenantIdAsync`
- `IPaymentWebhookLogRepository` — `GetByEventIdAsync`, `GetByBusinessKeyAsync`, `TryRequeueDeadOutboxAsync`, `Add`, `SaveChangesAsync`
- `OutboxRequeueResult` enum: `Requeued` / `AlreadyActive` / `Missing`

Third port, same folder: `IIntegrationCheckoutSessionRepository` — by id, by idempotency key, by provider session id, add, save.

`TryRequeueDeadOutboxAsync` is Hub-only. New Pay: do not implement an outbox requeue enum.

---

## 7. Call graph — who calls each adapter method

Adapters are never constructed by Commerce. Every production call goes `IPaymentGatewayFactory.GetAdapter(gatewayType)` then a method. Tests substitute the factory.

```
                    HTTP / MediatR / IEventBus
                              |
          +-------------------+-------------------+
          |                   |                   |
     Endpoints            Queries              EventHandlers
     (raw HTTP)           (MediatR)            (inbox/outbox)
          |                   |                   |
          v                   v                   v
  ProcessGatewayWebhook   CheckoutSessionCashier  ExecuteOffSession…
  CommandHandler          GenerateCustomerPortal  GatewayRefundRequested
          |               GenerateSystemCheckout          |
          |                   |                           |
          +-------------------+---------------------------+
                              |
                    IPaymentGatewayFactory.GetAdapter
                              |
          +---------+---------+---------+---------+
          |         |         |         |         |
       STRIPE     CHIP     BILLPLZ   RAZORPAY   XENDIT
```

That diagram **is** the cathedral. New Pay collapses the middle column: endpoint → decrypt → PSP HTTP → fulfill. No MediatR, no factory scan, no keyed event bus.

### 7.1 `GenerateCheckoutAsync`

**Who calls it (production):**

1. **`CheckoutSessionCashier.GenerateAsync`** — the one generate path for Commerce hop-2 and M2M.

```81:95:apps/lazuar-api/Modules/Payments/Application/Services/CheckoutSessionCashier.cs
        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);

        var result = await adapter.GenerateCheckoutAsync(
            plainApiKey,
            tenantId,
            amount,
            currency,
            productName,
            customerEmail,
            successUrl,
            cancelUrl,
            stampedMetadata,
            config.MerchantId,
            setupFutureUsage,
            quantity);
```

Cashier callers:

| Caller | MediatR type | `requireActiveGateway` | Last-resort BILLPLZ? |
|--------|--------------|------------------------|----------------------|
| `GenerateCheckoutSessionQueryHandler` | `GenerateCheckoutSessionQuery` → `string` (URL only) | **false** | **Yes** (`ResolveGatewayNameAsync` returns `"BILLPLZ"` when no active config) |
| `GenerateCheckoutSessionDetailedQueryHandler` | `GenerateCheckoutSessionDetailedQuery` → `GenerateCheckoutSessionResult` | caller-supplied | depends on flag |
| `CreateIntegrationCheckoutCommandHandler` | `CreateIntegrationCheckoutCommand` | **true** | **No** — `PAYMENTS_NOT_CONFIGURED` |

`GenerateCheckoutSessionDetailedQueryHandler` has **no production caller** on this SHA (only its own type + handler). Dead at the seam. Do not port a “detailed” twin.

`GenerateCheckoutSessionQuery` callers **outside Payments** (Commerce / Billing cathedral — refuse as destinations):

- `InitiateCheckoutCommandHandler` — product hop-2; `$0` recurring + `SupportsOffSession` mints setup-mode (`SetupFutureUsage: true`, amount 0); paid path stamps `CommerceCheckoutMetadata` (`type=commerce_subscription`, `subscription_id` = Commerce checkout session id, `tenant_id`).
- `RenewalCheckoutIssuer` — reminder renewals; `SetupFutureUsage: SupportsOffSession(product.GatewayName)`.
- `PublicArrearsEndpoints` — arrears pay-now link.
- `BillingEngineJob` — when `canCharge` is false, mints a hosted checkout via the same query (reminder path).
- Custom checkout / quote tests send the same query.

2. **`GenerateSystemCheckoutSessionQueryHandler`** — **bypasses the cashier**. Decrypts platform (system org) keys, stamps `tenant_id` if missing, calls `GenerateCheckoutAsync` with `setupFutureUsage: false`, `quantity: 1`. Callers: `CreateSaasCheckoutCommandHandler` (Billing), `AdminCreditsEndpoints` (utility credits). `PlatformCheckoutTypes`: `utility_credit_topup`, `platform_saas_fee`, `SystemOrganizationId`. **Refuse for v1.** Pay is not Hub billing itself.

**Hub fulfillment AFTER generate:** none at the adapter. Cashier returns URL + session id + gateway name. Commerce stores the URL on `CheckoutSession`. M2M persists `IntegrationCheckoutSession.MarkProviderIssued`. Success/cancel URLs are **redirects, not fulfillment.** Hub portal `/success` must not treat landing as paid; new checkout Vite the same.

**Who implements:** all five adapters mint a hosted URL (Stripe Checkout Session, CHIP purchase, Billplz bill, Razorpay payment link, Xendit invoice). Razorpay **discards** `setupFutureUsage`:

```39:41:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs
            // Reminder-only: we do not claim e-mandate. SetupFutureUsage still mints a
            // payment link, not a card-registration mandate (max_amount = 10× first charge).
            _ = setupFutureUsage;
```

Billplz’s signature still accepts `setupFutureUsage` and ignores it (no vault). That is honest. Commerce still sends `SetupFutureUsage: true` for every recurring interval, including Billplz. New Pay: pass `setup_future_usage` **only** for Stripe/CHIP when you intend to vault. Billplz path never sets it.

**New Pay today:** `StripeHosted.CreateHostedUrlAsync` is the generate function. No factory. Provider allow-list is `"stripe"`. That is the day-one refusal of five.

### 7.2 `ParseWebhookAsync`

**Who calls it (production):** only `ProcessGatewayWebhookCommandHandler.HandleCoreAsync`.

HTTP door:

```
PSP POST /api/v1/webhooks/payments/{gatewayType}/{tenantId}
  Endpoints.MapPaymentsEndpoints
    allow-list five names else 400
    empty/whitespace body → 400 { error: "Empty request body." }   // B04-P18 closed
    raw body + headers + Query-* into ProcessGatewayWebhookCommand
    IMediator.Send  // cathedral
    200 { received: true }  // "Intake ACK only. Domain fulfillment lives in Commerce / Billing / M2M session."
```

Handler:

```58:76:apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs
        var config = await _configRepository.GetByTenantAndGatewayAsync(request.TenantId, request.GatewayType, cancellationToken);
        if (config == null || string.IsNullOrEmpty(config.WebhookSecret))
        {
            throw new InvalidOperationException("Webhook secret not configured for this tenant gateway.");
        }

        // Webhooks still process when gateway is soft-disabled (credentials retained).
        var plainApiKey = _secretVault.DecryptOrPlaintextNullable(config.ApiKey) ?? "";
        var plainWebhookSecret = _secretVault.DecryptOrPlaintext(config.WebhookSecret!);

        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);
        var parsedResult = await adapter.ParseWebhookAsync(
            plainApiKey,
            plainWebhookSecret,
            request.RawBody,
            request.Headers,
            0, // estimatedFeePercentage - removed from config
            0, // fixedFee - removed from config
            0); // taxRate - removed from config
```

Missing secret → `InvalidOperationException` → endpoint does **not** swallow it → **500**. Signature fail (`Verified=false`, not unusable) → same 500. Stripe retries forever. **Do not copy.** Signature fail and missing config are **400**. New Pay already 400s unknown provider, empty body, missing rail, invalid signature.

`UnusableAfterVerify` → `PaymentWebhookUnusablePayloadException` → endpoint **400**. Steal that (poison after verify, stop retries).

Unknown `EventType` (not the five Hub names) → silent `return` then endpoint 200. Steal “verified unknown → 200 ignored.” Forward compatible.

Inbound `tenant_id` metadata mismatch vs URL tenant, unless platform checkout (`urlTenant == SystemOrganizationId` and `platform_tenant_id` matches) → log + return (200). New Pay has no platform org; mismatch is always ignore + log, **not** 400 (Stripe retries a 400 with the same poison).

**Hub fulfillment AFTER parse (the cathedral):**

```
verified + known EventType
  → business key lookup + event-id lookup
  → existing log? HandleExistingLogAsync (outbox requeue / republish) STOP
  → late PAYMENT_FAILED after PAYMENT_COMPLETED on same txn? ignore STOP
  → MergeSessionMetadataAsync (IntegrationCheckoutSession by ProviderSessionId)
  → new PaymentWebhookLog
  → PublishParsedEventAsync  // IEventBus PaymentsEventBus
  → TrySaveChangesAsync (23505 unique → treat as success)
```

`PublishParsedEventAsync` map:

| Parsed `EventType` | Integration event | Downstream (other modules) |
|--------------------|-------------------|----------------------------|
| `DISPUTE_CREATED` | `GatewayDisputeCreatedIntegrationEvent` | Billing `ChargebackClawbackHandler`; Commerce `CommerceGatewayDisputeCreatedHandler` |
| `DISPUTE_CLOSED` | `GatewayDisputeClosedIntegrationEvent` | Billing `GatewayDisputeLostHandler`; Commerce `CommerceGatewayDisputeClosedHandler` |
| `PAYMENT_FAILED` | `GatewayPaymentFailedIntegrationEvent` | Commerce fail handler (dunning / PAST_DUE); Communications failed-pay email; Payments `IntegrationCheckoutGatewayEventsHandler` (M2M `payment.failed` outbound) |
| `REFUND_COMPLETED` | `GatewayRefundCompletedIntegrationEvent` (`PaymentRecordId: Guid.Empty` from webhook path) | Billing refund ledger; Lhdn credit note; Commerce refund-completed handler |
| else (including `PAYMENT_COMPLETED`) | `GatewayPaymentCompletedIntegrationEvent` | **Commerce** activate / vault persist; **Billing** ledger + platform top-up + SaaS fee; **Payments** M2M `payment.completed` outbound (`OutboundWebhookRequestedIntegrationEvent`, TargetUrl null → One fan-out) |

That table **is** why Payments is “not a fulfillment engine.” New Pay’s `WebhookEndpoints` already calls `fulfillment.FulfillPaidAsync` in-process after the idempotency insert. Do not `PublishAsync` a `GatewayPaymentCompletedIntegrationEvent`.

`IntegrationCheckoutGatewayEventsHandler` is Plane C-shaped even for first-party M2M: it marks the session and enqueues `payment.completed` / `payment.failed` outbound. Comment: “Does not require Commerce products or fulfillment URLs.” Fail-then-pay: completed money wins over failed/expired; already-completed is idempotent; failed only if still `open`. Steal **paid wins** if money captured. Refuse the outbound webhook as a blocker for first-party dogfood (`NP-SOON-007`).

Off-session HTTP success does **not** publish completed from the adapter. Hub waits for `payment_intent.succeeded` (Stripe) / purchase paid (CHIP). Steal “adapter `true` is not paid.” New same-handler world: wait for the webhook to book cash, or book a **pending** that paper 07 must not call `RCPT-` yet.

**Who implements parse:** all five. Per-PSP verify algorithms are 05–07. Seam-level: estimated fee args discarded (Xendit `_ = estimatedFeePercentage`); Billplz still **computes** `gatewayFee = amount * pct + fixed` but production always passes 0 so the fee is always 0. Tests lock that the handler passes zeros (`BillplzFeeHonestyTests`). Steal: do not invent a fee. Stamp unknown.

### 7.3 `IssueRefundAsync`

**Who calls it (production):** only `GatewayRefundRequestedIntegrationEventHandler`.

Upstream: Commerce `RecordRefundCommandHandler`:

- If `RequiresMarkRefunded(gatewayName)` (Billplz / offline / blank): require `MarkRefunded`, apply locally, publish `GatewayRefundCompletedIntegrationEvent` **without** calling the adapter.
- Else if `!SupportsApiRefund`: throw `GATEWAY_REFUND_UNSUPPORTED`.
- Else: `MarkRefundPending`, publish `GatewayRefundRequestedIntegrationEvent`.

Payments inbox then:

```46:72:apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/GatewayRefundRequestedIntegrationEventHandler.cs
        var plainApiKey = _secretVault.DecryptOrPlaintext(config.ApiKey);
        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);
        var success = await adapter.IssueRefundAsync(plainApiKey, @event.GatewayTransactionId, @event.Amount);
        if (success)
        {
            // Policy: gateway fees stay with us. Publishers do not reclaim MDR.
            var refundedFee = 0m;
            var netRefunded = @event.Amount - refundedFee;

            await _eventBus.PublishAsync(new GatewayRefundCompletedIntegrationEvent(
                ...
            ));
        }
        else
        {
            await _eventBus.PublishAsync(new GatewayRefundFailedIntegrationEvent(
                @event.OrganizationId, @event.SubscriptionId, @event.PaymentRecordId, "Gateway adapter failed to issue refund."));
        }
```

Refunds still allowed when soft-disabled (“historical payment obligations”). Missing config → `GatewayRefundFailedIntegrationEvent` (message says “not found or inactive” even though `IsActive` is **not** checked). Amount `<= 0` → failed event.

**Hub fulfillment AFTER adapter `true`:** another integration event, then Billing ledger reverse + Lhdn credit note + Commerce status. Not in-process. New Pay paper 07: refund SOP in the same binary; Billplz is mark-refunded; do not copy `bool`-only then a second event.

**Who implements vs no-op:**

| Adapter | `IssueRefundAsync` |
|---------|--------------------|
| Stripe | HTTP refund on PaymentIntent + idempotency key. `false` on `StripeException`. |
| CHIP | `POST purchases/{id}/refund/` + `lazuar-refund:` key. `false` on non-success. |
| Razorpay | Payment fetch + refund. Capability true. Parked product. |
| Xendit | `POST /refunds` after resolving payment id. Capability true. Parked product. |
| Billplz | **`return Task.FromResult(false)`**. Comment: “Billplz has no bill-refund API. A Payment Order is a new disbursement, not a reversal.” |

Webhook path can **also** emit `REFUND_COMPLETED` from `ParseWebhookAsync` (Stripe refund map live; CHIP `payment.refunded` live). That is a second entry to the same downstream event. Dual-path is Hub complexity. New Pay: one refund write, idempotent.

### 7.4 `GenerateCustomerPortalAsync`

**Who calls it (production):** `GenerateCustomerPortalQueryHandler`, which **hard-codes** gateway `"STRIPE"`:

```28:47:apps/lazuar-api/Modules/Payments/Application/Queries/GenerateCustomerPortalQueryHandler.cs
        // For customer portal, we specifically look for Stripe as it's the only one supporting it.
        var config = await _configRepository.GetByTenantAndGatewayAsync(request.TenantId, "STRIPE", cancellationToken);
        ...
        return await adapter.GenerateCustomerPortalAsync(
            plainApiKey,
            request.CustomerEmail,
            request.ReturnUrl);
```

HTTP: Commerce `POST /admin/commerce/subscribers/portal-link` (`OrgMember`). Stripe Billing Portal by email lookup. No customer → `InvalidOperationException`.

**Hub fulfillment AFTER:** none. Returns a URL. Buyer is sent to Stripe-hosted portal.

**Who implements vs throw:**

| Adapter | Portal |
|---------|--------|
| Stripe | Billing Portal session |
| CHIP | throws `InvalidOperationException("CHIP Collect does not provide a managed customer billing portal.")` |
| Billplz | throws (same shape) |
| Razorpay | throws |
| Xendit | throws |

**Not v1 dogfood.** Do not copy the query, the endpoint, or the four throw methods “for interface completeness.”

### 7.5 `ChargeOffSessionAsync`

**Who calls it (production):** only `ExecuteOffSessionChargeIntegrationEventHandler`, and **only after** `SupportsOffSession`.

Upstream publishers (Commerce cathedral — refuse as S1 destinations):

- `BillingEngineJob` when `canCharge` (off-session + not reminder-only + no open dispute + vaulted customer + token). Attempt 1 only; later retries are dunning AUTO_CHARGE.
- `PastDueDunningProcessor` AUTO_CHARGE step.

Handler decrypts, builds idempotency `lazuar-offsession:{chargeAttemptId}` (Stripe helper, used for **all** off-session rails), calls the adapter.

**Hub fulfillment AFTER adapter `true`:** **nothing.** No completed event. Wait for webhook. Adapter `false` / `OffSessionDeclinedException` / `NotSupportedException` / generic exception → `GatewayPaymentFailedIntegrationEvent` with `failure_source=off_session` and a synthetic transaction id `off_session_attempt:{guid}` or `off_session:{subscriptionId}:{eventId}`.

Stripe throws `OffSessionDeclinedException` so the decline code is not swallowed as boolean false. CHIP returns `false` on exception. Billplz logs a warning and returns `false` (never reached if capability is checked). Xendit returns `false` with a class comment “hosted invoices do not vault.” Razorpay **implements** Order + `CreateRecurringPayment` — but capability false means the handler never calls it. If someone bypasses the helper, Razorpay would fire a recurring payment Hub’s product copy says it does not do.

**Not the first charge.** First dogfood is hosted hop-2, amount `> 0`, buyer present. Do not build `ChargeOffSessionAsync` in order to prove the first `RCPT-`.

**Who implements vs no-op:**

| Adapter | Off-session |
|---------|-------------|
| Stripe | PaymentIntent `OffSession=true`, `Confirm=true`, idempotency key. Success is **`succeeded` only** (not `processing`). Decline throws. |
| CHIP | Create purchase + `POST purchases/{id}/charge/` with recurring token; reference lookup for idempotency (not Stripe-class). Returns `false` on exception. |
| Billplz | **no-op `false`**, warning log. Does not throw. |
| Xendit | **no-op `false`**, comment reminder-only. |
| Razorpay | **HTTP exists**, capability false, engine never calls. Lie if copied. |

---

## 8. Checkout cashier — last-resort BILLPLZ, test/live keys, amount rules

### 8.1 Resolve order (the last-resort lie)

```117:144:apps/lazuar-api/Modules/Payments/Application/Services/CheckoutSessionCashier.cs
    /// Explicit preferred → first active tenant config → optional BILLPLZ last resort (legacy only).
    public async Task<string> ResolveGatewayNameAsync(...)
    {
        if (!string.IsNullOrWhiteSpace(preferredGateway))
        {
            return preferredGateway.Trim().ToUpperInvariant();
        }

        var configs = await _configRepository.GetAllByTenantIdAsync(tenantId, cancellationToken);
        var firstActive = configs.FirstOrDefault(c => c.IsActive && !string.IsNullOrWhiteSpace(c.ApiKey));
        if (firstActive != null && !string.IsNullOrWhiteSpace(firstActive.GatewayType))
        {
            return firstActive.GatewayType.Trim().ToUpperInvariant();
        }

        if (requireActiveGateway)
        {
            throw PaymentIntegrationException.PaymentsNotConfigured();
        }

        return "BILLPLZ";
    }
```

Commerce hop-2 (`requireActiveGateway: false`) will mint a Billplz bill against a **missing** config path… actually it still then loads config for `"BILLPLZ"` and throws `not configured` if no row. The last resort only helps when a Billplz row exists but the caller omitted `GatewayName`. It is still a silent default. Query comment: “then BILLPLZ as last resort.” Ops default dropdown historically `BILLPLZ`. **Do not copy.** Missing keys → 400/409 `payments_not_configured`, not a surprise Billplz bill. New Pay `StripeHosted` already throws `rail not configured`. Gateway PUT allow-lists `"stripe"` only (“Bar B first rail is stripe”).

Soft-disabled config: cashier refuses new checkouts. Webhook handler still processes. Steal that pair.

### 8.2 Test vs live (`environment` + Stripe prefix)

`PaymentGatewayEnvironment`:

```7:9:apps/lazuar-api/Modules/Payments/Domain/PaymentGatewayEnvironment.cs
    public const string Test = "test";
    public const string Live = "live";
    public const string MetadataKey = "hub_payment_environment";
```

Normalize: `live` / `production` → live; everything else → test. Infer from `sk_live_` / `sk_test_` only.

Cashier stamps `hub_payment_environment` into **PSP metadata**. New Pay must **not** stamp that Hub key. Stamp `org_id` + `checkout_id`. New `StripeHosted` already stamps those two.

`EnsureKeyModeMatchesGateway`: cheap Stripe-prefix guard. Fixture keys like `sk_test` (no trailing `_`) and Billplz/CHIP skip. `EnsureKeyModeMatchesConfigEnvironment`: test request cannot charge `environment=live` and vice versa (`KEY_MODE_MISMATCH` 409). Steal the guard. CHIP: dashboard test-mode is **not** a key prefix — copy must say so. Billplz: sandbox host vs www host follows `environment`, not Pay’s hostname.

`UpdatePaymentConfigCommandHandler` infers environment from Stripe-shaped key when the PUT omits `environment`. Default test.

M2M `CreateIntegrationCheckoutCommand.RequestIsTestMode` comes from `IExecutionContextAccessor.IsTestMode` (machine key). Commerce hop-2 cashier call does **not** pass `requestIsTestMode` (null → skip prefix guard). That is a Hub hole. New Pay: persist `environment` on the credential row and refuse `sk_live_` against `test`.

### 8.3 `CheckoutAmountRules`

```7:40:apps/lazuar-api/Modules/Payments/Application/Services/CheckoutAmountRules.cs
    public const decimal MyrMinimum = 2.00m;
    public const decimal DefaultMinimum = 0.50m;
    ...
    // 3-letter currency; amount > 0; MYR at most 2 decimal places; amount >= min
```

Used by **M2M** `CreateIntegrationCheckoutCommandHandler.ValidateRequest`. Commerce hop-2 does **not** go through this class (Commerce has its own SST/gross math). New Pay fixture today only checks `amount > 0`. Steal MYR min **RM 2.00** when leaving fixture-cheap amounts, or keep `> 0` until a PSP rejects RM 0.01. Do not silently skip the PSP.

M2M also: description required ≤ 200; email must contain `@`; success/cancel absolute http(s); gateway allow-list five names; metadata max 20 keys / 40 / 500; fingerprint for idempotency conflict; persist session **before** provider call so unique `(org, idempotency_key)` wins races; on provider fail, `MarkFailed` best-effort.

Steal: persist-before-PSP if you need idempotent create; unique `(org_id, Idempotency-Key)`. Fixture already has in-memory idempotency. Refuse the five-name allow-list; dogfood rails only.

### 8.4 Metadata stamps at generate (ADR 009 vs live)

ADR 004 + ADR 009 (June 2026): Payments is **stateless**; Billplz strips `reference_1`; encode context on `callback_url` query string; endpoint copies query into `Query-*` headers; adapter reconstructs metadata.

Live on this SHA: **all of that still happens**, **and** Hub grew `IntegrationCheckoutSessions` + `MergeSessionMetadataAsync` because query-string was not enough. ADR “stateless” is superseded. New Pay keeps one session row. Query-string remains a Billplz recovery path, not the architecture religion.

Commerce stamps (`CommerceCheckoutMetadata.MergeClientIntoGateway`):

- `type` = `commerce_subscription` (or client `saas_subscription`)
- `subscription_id` = Commerce checkout session id
- `tenant_id` = org
- optional `is_b2b_required`, `client_profile_id`, SST keys

M2M stamps (`IntegrationCheckoutMetadata.Stamp`):

- `hub_workspace_id`, `checkout_id`, `tenant_id`, `hub_checkout_kind=integration`
- preserves Aura reserved keys (`integrator`, `booking_id`, `gift_card_id`, …)

Cashier additionally stamps `hub_payment_environment`.

`ApplyPayingTenantMetadata` may add `platform_tenant_id`.

Webhook merge: adapter metadata wins; session fills missing keys; **force** `checkout_id` from session; lookup key is `ProviderSessionId == GatewayTransactionId` (Billplz bill id). Merge errors are logged, never fail the money path.

New Pay: `checkout_id` + `org_id` in Stripe/CHIP JSON metadata (already in `StripeHosted`). Billplz: `callback_url` includes `checkout_id`; persist `provider_session_id` = bill id so merge is a fallback. Never PII on the query string (ADR 009 security consequence — steal that). Never `subscription_id` as the only pointer — Pay’s checkout id **is** the pointer. Never `hub_*` keys.

---

## 9. Config HTTP — paste keys, never return secrets

### 9.1 GET

`GetPaymentConfigQueryHandler`:

```28:52:apps/lazuar-api/Modules/Payments/Infrastructure/Queries/GetPaymentConfigQueryHandler.cs
            var hasApiKey = !string.IsNullOrWhiteSpace(config.ApiKey);
            var hasWebhook = !string.IsNullOrWhiteSpace(config.WebhookSecret);
            var apiHint = _secretVault.HintLast4(config.ApiKey);
            var webhookHint = _secretVault.HintLast4(config.WebhookSecret);

            return new PaymentConfigDto
            {
                Gateway_type = config.GatewayType,
                // Never return stored ciphertext or plaintext secrets.
                Api_key = null,
                Merchant_id = config.MerchantId,
                Webhook_secret = null,
                Secret_key = null,
                ...
                Environment = string.IsNullOrWhiteSpace(config.Environment) ? "test" : config.Environment
            };
```

Steal: GET never fills password fields; last-4 hint; `has_*` flags. New Pay GET `/v1/orgs/{orgId}/gateway` already returns `last4` + `configured` + `capability = "hosted_link"`, never ciphertext.

Twin GET: platform `/api/v1/platform/payment-config` (Hub staff). `NP-XX-018` — not a Pay merchant destination.

Agent tool `GetPaymentConfigAgentQuery` returns **names only** of active configs (`SUPER_ADMIN`/`ADMIN`). Hub ops chat. Refuse as a Pay product.

`GET /integrations/payments/me` (`GetPaymentsMeQueryHandler`): machine key identity + `has_active_gateway` + `gateway_names`. Useful later for `lzr_sk_`. Not S1.

### 9.2 PUT

`UpdatePaymentConfigCommandHandler`:

- `IsKeepExistingSecret`: blank or contains `••••` → keep.
- Stripe: `secret_key` form field maps into `ApiKey` column; `api_key` is the fallback if secret_key is keep.
- Others: `api_key` column is the secret.
- Merchant id keep-if-mask.
- `is_active` default true on create.
- Environment: request, else infer Stripe prefix, else existing, else test.
- CHIP new key → `ChipWebhookRegistrar` + localhost rewrite (foot-gun).
- First-time create requires an API key (`BusinessRuleValidationException`).
- Encrypt via `ISecretVault.Encrypt`; store ciphertext.

HTTP: Commerce `PUT /admin/commerce/payment-config` **`RequireAuthorization("OrgAdmin")`**. Platform twin for Hub staff.

Ops UI `canSaveVault = role === "ADMIN" || role === "SUPER_ADMIN"`. Hub VIEWER cannot paste. Steal the **product rule**, not the role strings. One roles are `owner` \| `admin` \| `member`. Key PUT uses `authz/check` `admin` (013/06 §4.6). New Pay `GatewayEndpoints.Put` uses `MemberGate.RequireWriterAsync` — 08/02 own whether that is admin or member; this paper only notes Hub used OrgAdmin.

### 9.3 Secret vault (`AesSecretVault`)

BuildingBlocks, **not** in Modules/Payments:

```10:24:apps/lazuar-api/BuildingBlocks/Infrastructure/AesSecretVault.cs
/// Shared AES-256-CBC secret vault. Key from <c>Kms:MasterKey</c>, falling back to <c>Jwt:Secret</c> for local/dev.
/// Stored format: base64(IV[16] + ciphertext).
public sealed class AesSecretVault : ISecretVault
{
    ...
        var keyString = FirstNonEmpty(configuration["Kms:MasterKey"], configuration["Jwt:Secret"])
            ?? throw new InvalidOperationException("Kms:MasterKey (or Jwt:Secret fallback) configuration missing.");

        _masterKey = Encoding.UTF8.GetBytes(keyString.PadRight(32, '0')[..32]);
```

Registered once in Hub `Program.cs`: `AddSingleton<ISecretVault, AesSecretVault>()`. Shared with LHDN certs, One webhook secrets, Resend keys.

`SecretVaultExtensions.DecryptOrPlaintext`: decrypt fail → treat as legacy plaintext. `HintLast4`: decrypt fail → hint the ciphertext’s last 4 (worse). `IsKeepExistingSecret`: blank or `••••`.

Steal: ciphertext at rest; IV envelope (or GCM); last-4 after decrypt; blank PUT means keep; never log the secret.

Drop:

- **`Jwt:Secret` fallback.** Couples payment keys to a leftover JWT secret. New Pay is not an IdP. If `Pay:WrapKey` is missing, Hub would pad `"0000…"`. New `SecretBox` currently hashes a **dev string** `"lazuar-pay-dev-wrap-key"` when wrap key is missing — better than Jwt fallback, still a production foot-gun if someone forgets `Pay:WrapKey`. 013/06 said refuse to boot. Live new Pay has not done that yet. Do not import Hub’s Jwt fallback to “fix” it.
- **`DecryptOrPlaintext`.** A mis-keyed deploy sends AES blobs to Stripe as `sk_live_`. New rows are always ciphertext. Reject undecryptable keys at use time. New `SecretBox.Unprotect` throws on GCM fail — keep that.
- **The BuildingBlocks type.** IsolationTests ban `BuildingBlocks`. New Pay already has `SecretBox` AES-GCM. Do not “stay consistent” with CBC.

---

## 10. Estimated fees (dead axis)

README: “Estimated fee profile columns are unused (handler passes 0, 0, 0).”

Migration `20260705131411_RemoveAccountingOverrides` dropped `EstimatedFeePercentage` (and friends) from `TenantPaymentConfigurations`. The **interface still has the args**. Billplz parse still applies the formula. Production always passes 0 so `GatewayFee` is 0. Stripe/CHIP copy processor fees when expand/`payment.fee_amount` succeeds; otherwise stamp `gateway_fee_status=unknown` and still fulfill. `GatewayFee=0` then is not “the fee is zero.”

Steal the stamp. Refuse estimated-fee columns, refuse the three parse args, refuse inventing MDR.

`GatewayRefundRequestedIntegrationEventHandler` always publishes `RefundedFee = 0m` (“do not reverse EXPENSE_GATEWAY_FEE on refund”). Webhook `REFUND_COMPLETED` path same (`RefundedFee: 0m`). Policy, not PSP. Paper 07.

---

## 11. Outbox / inbox / workers (cathedral, refuse)

```
AddModuleOutboxInbox<PaymentsDbContext, PaymentsOutboxPublisherJob, PaymentsInboxConsumerJob>("PaymentsEventBus")
```

`PaymentsOutboxPublisherJob` / `PaymentsInboxConsumerJob` are empty subclasses of BuildingBlocks jobs. They poll `payments.OutboxMessages` / `InboxMessages`. `PaymentsDbContext` **is** a `PlatformDbContext` taking `IMediator` + `DatabaseJobTrigger`.

Webhook ACK 200 means “outbox queued,” not “Commerce activated.” `HandleExistingLogAsync` requeues Dead outbox on PSP retry. Concurrent 23505 unique on the log is swallowed as success.

New Pay: no keyed event bus, no inbox consumer, no outbox publisher, no `PlatformDbContext`. `WebhookEndpoints` inserts `PspWebhookEventRow` then calls `Fulfillment.FulfillPaidAsync` (own transaction). IsolationTests exist so `MediatR` / `BuildingBlocks` cannot sneak in as “thin endpoints.”

Workers are also how `ExecuteOffSessionChargeIntegrationEvent` and `GatewayRefundRequestedIntegrationEvent` **arrive** at Payments: Commerce publishes on **its** bus; BuildingBlocks copies across module inbox. Same-process, still async, still retry/Dead. That latency is the parked-event tax 011 already paid. New Pay must not reintroduce it to “keep modules decoupled.”

---

## 12. Per-method implement vs throw / no-op (seam table)

| Method | Stripe | CHIP | Billplz | Razorpay | Xendit |
|--------|--------|------|---------|----------|--------|
| `GatewayType` | `STRIPE` | `CHIP` | `BILLPLZ` | `RAZORPAY` | `XENDIT` |
| `GenerateCheckoutAsync` | **implements** (Checkout Session; `$0`+setupFutureUsage → setup mode — HTTP extract is 05) | **implements** (purchases; `force_recurring` / `skip_capture` — 06) | **implements** (bills; `setupFutureUsage` ignored; public callback — 06) | **implements** (payment link; **discards** setupFutureUsage) | **implements** (hosted invoice; reminder-only comment) |
| `ParseWebhookAsync` | **implements** | **implements** | **implements** (form + Query-* reconstruction) | **implements** | **implements** (`x-callback-token`) |
| `IssueRefundAsync` | **implements** → bool | **implements** → bool | **no-op false** | **implements** → bool (parked) | **implements** → bool (parked) |
| `GenerateCustomerPortalAsync` | **implements** (Billing Portal) | **throws** | **throws** | **throws** | **throws** |
| `ChargeOffSessionAsync` | **implements** (succeeded only; decline throws) | **implements** (token + reference lookup) | **no-op false** (warn) | **implements HTTP** but capability **false** (engine never calls) | **no-op false** |

Parked ≠ delete from Hub. New Pay simply does not register Razorpay/Xendit. Do not add throw methods to a Pay “interface of five” so the factory compiles.

---

## 13. Steal vs refuse at the seam (normative)

This table is the paper’s product. Per-PSP HTTP rows live in 05–07; this is the **contract**.

| Steal (judgment / HTTP decision) | Refuse (cathedral / type / lie) |
|----------------------------------|---------------------------------|
| One generate function + one parse function per **dogfood** rail | `IPaymentGatewayAdapter` as a five-method port “for later” |
| Factory of **one** (or two after the first is boring) | `PaymentGatewayFactory` scanning `IEnumerable<IPaymentGatewayAdapter>` of five |
| Allow-list at the HTTP door | Five-name Hub allow-list (`STRIPE, BILLPLZ, RAZORPAY, CHIP, XENDIT`) |
| Wrap-rails matrix restated in 10 lines next to charge | `using Modules.Payments.Contracts` / the class file |
| `SupportsOffSession` = stripe \| chip; reminder-only otherwise; e-mandate always false | Unread `SupportsDuitNowQr` / `SupportsHostedWallet` as product tiles |
| Encrypted at rest, GET last-4, blank PUT keep | `AesSecretVault`, `Jwt:Secret` fallback, `DecryptOrPlaintext`, BuildingBlocks `ISecretVault` |
| Webhooks still process when keys are soft-disabled | Disable = drop paid money |
| Empty body 400 | Empty body 500 (008 residual; live Hub is already 400 — keep 400) |
| Signature fail 400 | Live Hub signature fail **500** |
| Missing webhook secret 400 | Live Hub missing secret **500** |
| Idempotent `(org_id, provider, event_id)` | Unique `(Provider, EventId)` without tenant (closed on Hub; do not regress) |
| Unique-violation-is-duplicate (23505) | `HandleExistingLogAsync` outbox requeue / republish |
| Dual-event collapse for Stripe PI (business key or session already paid) | Hub `EVENTTYPE:` vocabulary as a shared enum; setup as `PAYMENT_COMPLETED` |
| Late fail after paid on same txn → ignore | Publishing fail that reverses a paid session |
| Fail-then-pay: paid wins if money captured | `IntegrationCheckoutSession` `failed` as a one-way latch that drops later completed (009 B04-P02 residual — live M2M handler **does** let completed win; steal that, not the type) |
| Query-string recovery for PSPs that strip body metadata (Billplz) | ADR 009 “Payments is stateless” religion; `hub_*` keys; `subscription_id` as the only pointer |
| One checkout row that **is** `/v1/checkouts` | `IntegrationCheckoutSessions` + Commerce hop-2 session + platform `SystemOrganizationId` |
| Persist-before-PSP for create idempotency | M2M table as a second product |
| `KEY_MODE_MISMATCH` for `sk_test_` vs live | Stamping `hub_payment_environment` into PSP metadata |
| MYR min RM 2 / 2 d.p. when leaving fixture | BILLPLZ last-resort resolve |
| Public HTTPS callback fail-closed (Billplz public-base) | CHIP localhost → `lazuar-local-dev.com` rewrite; `PublicDnsFallback` as default |
| CHIP list-before-create webhook registrar against **Pay** public `/v1` | Registrar against Hub `/api/v1/webhooks/payments/chip/{tenantId}` |
| Fee unknown stamp; never invent MDR | Estimated-fee args; fee=0 meaning known zero |
| Adapter `true` on off-session is not paid; wait for webhook (or pending, no `RCPT-`) | Publishing completed from the charge HTTP success |
| Billplz refund = mark in dashboard | Calling `IssueRefundAsync` that always returns false then inventing PAST_DUE |
| Portal throws on non-Stripe | Shipping Stripe Billing Portal as v1; `GenerateCustomerPortalQuery` |
| MediatR-free endpoint is the use case | `IMediator.Send(ProcessGatewayWebhookCommand)` |
| Same-handler fulfill | `IEventBus` / outbox / inbox / `GatewayPaymentCompletedIntegrationEvent` |
| IsolationTests stay red on `Modules.` / `BuildingBlocks` / `MediatR` / `lazuar-api` | “Thin handlers, extract later” |
| New `SecretBox` AES-GCM + `Pay:WrapKey` | Importing Hub vault “for consistency” |
| New `StripeHosted` + `WebhookEndpoints` as the seam to **grow** | Replacing them with `AddPaymentsModule` |
| Dogfood rails only on PUT `/v1/orgs/{orgId}/gateway` | Accepting `razorpay` / `xendit` “while we are here” (`NP-LAT-002`) |

---

## 14. What new Pay already is at this SHA (seam contrast, not paper 01)

Focused host already refused the factory:

- `StripeHosted.Provider = "stripe"` (lowercase). One class, two jobs conceptually: create hosted URL, (parse lives in `WebhookEndpoints`).
- `PUT /v1/orgs/{orgId}/gateway` allow-lists stripe (“Bar B first rail is stripe”).
- `POST /v1/webhooks/{provider}/{orgId}` allow-lists stripe; empty 400; bad signature 400; duplicate `(org, provider, event_id)` 200; then `Fulfillment.FulfillPaidAsync`.
- Setup/zero amount on `checkout.session.completed` → `{ ignored: "setup_or_zero" }` — **already more honest than Hub’s `PAYMENT_COMPLETED` amount 0.**
- `SecretBox` AES-GCM. IsolationTests ban the cathedral strings.

What it has **not** yet stolen from this seam (honesty, not a punch list to implement from this file):

- Per-org webhook secret (Bar B uses process `Pay:StripeWebhookSecret`).
- `environment` test/live on the credential row / `KEY_MODE_MISMATCH`.
- MYR min RM 2.
- Wrap-rails helper next to a future charge function (capability string is `"hosted_link"` only).
- Malaysian rail. Factory of five still refused — keep it that way when CHIP or Billplz lands: **add a function**, do not add `IPaymentGatewayAdapter`.

Growing a second rail by introducing Hub’s interface is how five arrived. Paper 09 owns the new seam shape. This paper’s binding: the old interface is the thing we **read**, not the thing we **compile**.

---

## 15. Cathedral pieces that must NOT be copied into `apps/lazuar-pay`

Named, so a later PR cannot claim they were “just the adapter.”

### 15.1 Types / projects

- Any `ProjectReference` into `apps/lazuar-api` (IsolationTests `apps/lazuar-api` path + banned tokens).
- `Modules.Payments.Application` / `Contracts` / `Domain` / `Infrastructure` csproj.
- `BuildingBlocks.Application` / `Infrastructure` / `Domain` (`ICommand`, `IQuery`, `IEventBus`, `ISecretVault`, `PlatformDbContext`, `Entity`, `IAggregateRoot`, `IMustHaveTenant`, `AesSecretVault`).
- `MediatR` (`IMediator`, `IRequest`, `AddMediatR`, the Application marker class).
- `Lazuar.ApiTypes` DTOs (`PaymentConfigDto`, `SavePaymentConfigRequestDto`, `GenerateCustomerPortalRequestDto`).
- `Modules.Commerce.*` / `Modules.Billing.*` as fulfillment destinations.

### 15.2 Runtime machines

- `AddPaymentsModule` / `UsePaymentsSubscriptions` / `MapPaymentsEndpoints` / `MapPaymentsIntegrationEndpoints` / `MapPlatformEndpoints`.
- `PaymentsDbContext` schema `"payments"` + `__EFMigrationsHistory` in that schema.
- `PaymentsOutboxPublisherJob` / `PaymentsInboxConsumerJob` / `AddModuleOutboxInbox` / keyed `"PaymentsEventBus"`.
- `ProcessGatewayWebhookCommand` + four partials (`Idempotency`, `Metadata`, `Logging`, main).
- `HandleExistingLogAsync` / `TryRequeueDeadOutboxAsync` / `OutboxRequeueResult`.
- `IPaymentGatewayFactory` / five `AddScoped<IPaymentGatewayAdapter, …>`.
- `GenerateSystemCheckoutSessionQueryHandler` / `PlatformCheckoutTypes` / `SystemOrganizationId`.
- `GenerateCustomerPortalQueryHandler`.
- `IntegrationCheckoutSession` + `CreateIntegrationCheckoutCommandHandler` + `/integrations/payments/*`.
- `OutboundWebhookRequestedIntegrationEvent` as a requirement for first-party paid (Plane C).
- `PublicDnsFallback` named HttpClient as default.
- CHIP localhost → `lazuar-local-dev.com` rewrite.
- BILLPLZ last-resort in `ResolveGatewayNameAsync`.
- `DecryptOrPlaintext` / `Jwt:Secret` as KMS.
- Commerce `PaymentConfigEndpoints` `/admin/commerce/payment-config` and Hub `OrgAdmin` strings as the Pay merchant door.
- Platform `/api/v1/platform/payment-config` as a merchant destination (`NP-XX-018`).
- Agent `GetPaymentConfigAgentQuery`.

### 15.3 Event names as a product

Do not compile these records into Pay:

- `GatewayPaymentCompletedIntegrationEvent`
- `GatewayPaymentFailedIntegrationEvent`
- `GatewayRefundRequestedIntegrationEvent` / `Completed` / `Failed`
- `GatewayDisputeCreatedIntegrationEvent` / `Closed`
- `ExecuteOffSessionChargeIntegrationEvent`
- `ApiCreditPurchasedIntegrationEvent`
- `LineItemDto` on the completed event (handler always passes `new List<LineItemDto>()`)

Steal the **meanings** (paid, failed, refunded, disputed, vaulted) as an internal enum next to the webhook handler. Do not steal `PAYMENT_COMPLETED` for setup.

### 15.4 Copy that is a lie if ported as-is

- Setup / `setup_intent.succeeded` / CHIP `purchase.preauthorized`+token as `PAYMENT_COMPLETED`.
- Signature fail 500.
- Factory of five on day one.
- Razorpay off-session HTTP behind a reminder-only capability (the method existing is the lie).
- Estimated fee formula with zeros meaning known zero.
- ADR 009 “stateless cashier” after `IntegrationCheckoutSessions` exists.
- README “not a fulfillment engine” as architecture for 011. New Pay’s webhook **is** fulfillment.

---

## 16. 008/02 historical vs live files (this SHA)

008 claimed things this tree has since closed. Live files win:

| 008/02 claim | Live `ee2db8e5` |
|--------------|-----------------|
| Empty webhook body 500 | **400** `{ error: "Empty request body." }` |
| EventId unique not tenant-scoped | Unique `(OrganizationId, Provider, EventId)` |
| CHIP `$0` skip_capture never vaults | Parse maps `purchase.preauthorized` **with token** to `PAYMENT_COMPLETED` (still `NP-GW-008` if booked as paid) |
| CHIP `payment.refunded` registered and dropped | Live maps `REFUND_COMPLETED` |
| Stripe no refund webhook map | Live `TryMapRefundCompleted` |
| `GatewayWebhookParsedResult` has no refund type | Handler now accepts `REFUND_COMPLETED` |
| Banker vs truncate minor-unit split | `ToMinorUnitsRounded` / `Truncating` both call `ToMinorUnits` (half away from zero) |

008 claims that **remain live** and this paper still refuses:

- Factory of five.
- Setup stuffed into `PAYMENT_COMPLETED`.
- Signature / missing secret → 500.
- BILLPLZ last resort when `requireActiveGateway` is false.
- Unread DuitNow/wallet flags.
- Payments not-fulfillment + outbox as the ACK.

Do not re-open 008 P0s that migrations already closed. Do not treat 008’s EventId paragraph as current Hub truth.

---

## 17. End-to-end call graphs (textual, production)

### 17.1 Hosted pay (Commerce hop-2) — cathedral

```
Buyer → portal hop-1
  Commerce InitiateCheckoutCommandHandler
    stamps CommerceCheckoutMetadata (subscription_id, tenant_id, type)
    IMediator.Send(GenerateCheckoutSessionQuery)     // requireActiveGateway false
      CheckoutSessionCashier
        ResolveGatewayName (preferred → first active → "BILLPLZ")
        decrypt ApiKey (DecryptOrPlaintext)
        stamp hub_payment_environment
        factory.GetAdapter
        adapter.GenerateCheckoutAsync                 // PSP HTTP
      returns URL
    store URL on Commerce CheckoutSession
  redirect buyer to PSP hosted page
PSP → POST /api/v1/webhooks/payments/{gw}/{tenantId}
  Endpoints → IMediator.Send(ProcessGatewayWebhookCommand)
    decrypt webhook secret
    adapter.ParseWebhookAsync                         // verify + map
    PaymentWebhookLog + outbox
    Publish GatewayPaymentCompletedIntegrationEvent
      → Commerce inbox: activate subscription, maybe vault ids
      → Billing inbox: journal
      → Payments inbox: IntegrationCheckoutGatewayEventsHandler (no-op unless checkout_id is M2M)
200 { received: true }  // before Commerce runs
```

### 17.2 M2M checkout — cathedral plus a session table

```
Machine POST /integrations/payments/checkouts
  CreateIntegrationCheckoutCommandHandler
    CheckoutAmountRules
    allow-list five names
    Stamp checkout_id / hub_workspace_id / hub_checkout_kind
    persist IntegrationCheckoutSession OPEN
    cashier.GenerateAsync (requireActiveGateway true)  // no BILLPLZ last resort
    MarkProviderIssued
PSP webhook (same as 17.1)
  MergeSessionMetadataAsync by bill/session id
  Publish completed
    IntegrationCheckoutGatewayEventsHandler
      MarkCompleted
      Publish OutboundWebhookRequestedIntegrationEvent (Plane C / One fan-out)
```

### 17.3 Off-session renew — cathedral

```
BillingEngineJob (or dunning AUTO_CHARGE)
  PaymentGatewayCapabilities.SupportsOffSession?
  Publish ExecuteOffSessionChargeIntegrationEvent
    Payments inbox ExecuteOffSessionChargeIntegrationEventHandler
      capability check again
      adapter.ChargeOffSessionAsync
      on false/throw: Publish GatewayPaymentFailedIntegrationEvent
      on true: wait
PSP payment_intent.succeeded / purchase.paid
  same webhook path as 17.1 → completed → Commerce/Billing
```

### 17.4 Refund — cathedral

```
Ops RefundModal / RecordRefundCommandHandler
  RequiresMarkRefunded? → local apply + GatewayRefundCompleted (no adapter)
  SupportsApiRefund? else throw
  Publish GatewayRefundRequestedIntegrationEvent
    Payments inbox
      adapter.IssueRefundAsync
      true → GatewayRefundCompleted (RefundedFee 0)
      false → GatewayRefundFailed
        Billing + Lhdn + Commerce handlers
and/or PSP refund webhook → ParseWebhookAsync REFUND_COMPLETED → same completed event
```

### 17.5 Customer portal — Stripe only

```
POST /admin/commerce/subscribers/portal-link
  GenerateCustomerPortalQuery (hard-coded STRIPE)
    adapter.GenerateCustomerPortalAsync
  return URL
```

### 17.6 What 8081 already does instead (steal target)

```
Merchant PUT /v1/orgs/{orgId}/gateway  (writer gate, stripe only, SecretBox)
Merchant POST /v1/checkouts            (member gate)
  StripeHosted.CreateHostedUrlAsync    // metadata checkout_id + org_id
Buyer opens checkout_url (Vite 5179, no One account)
PSP POST /v1/webhooks/stripe/{orgId}
  empty 400 / sig 400 / duplicate 200
  insert PspWebhookEventRow
  Fulfillment.FulfillPaidAsync         // same handler, own txn
  ignore setup/zero
```

No factory. No MediatR. No outbox. That is the shape CHIP/Billplz must **join**, not replace.

---

## 18. Binding for papers 05–07 and 09

- **05 Stripe:** compare `StripeGatewayAdapter` HTTP to live `StripeHosted` + `WebhookEndpoints`. This paper’s contract: generate + parse; portal not S1; off-session later; never `subscription.updated`; never setup-as-paid (new host already ignores setup/zero — keep that, steal Hub’s customer+PM extract without the event name).
- **06 CHIP / Billplz:** first Malaysian rail. Join `StripeHosted`’s shape (functions, not `IPaymentGatewayAdapter`). Wrap-rails copy is mandatory if Billplz. CHIP preauthorized+token = vaulted, not paid.
- **07 Xendit / Razorpay:** later. Do not register them to “complete the factory.” Razorpay `ChargeOffSessionAsync` existing is a warning, not a feature.
- **09 new adapter seam:** grow functions on 8081. If a PR adds `interface IPaymentGatewayAdapter` with five methods and a factory of two “so CHIP is symmetric,” that PR has copied this cathedral. Two functions: `CreateHostedCheckout` and `ParseWebhook`. Refund and off-session wait until paper 07 / V1 need them.

---

## 19. Open questions this seam does not pick

CHIP vs Billplz as the Malaysian rail remains 013/06 §10 — unpicked. This paper only binds: whatever you add, it is a **function** next to `StripeHosted`, not a fifth `IPaymentGatewayAdapter`. Factory of five stays refused.

Per-org webhook secret vs process `Pay:StripeWebhookSecret` is 08. Hub’s seam judgment is per-tenant `WebhookSecret`; Bar B is process-wide. Steal the Hub judgment when the second org appears.

Key PUT `admin` vs `member` is 08 / 013/06 §4.6. Hub used OrgAdmin.

Master key algorithm is already GCM in new Pay. This paper only forbids Hub’s Jwt fallback and `DecryptOrPlaintext`.

---

*End of 014-evals paper 04. Do not implement from this file. Do not copy `Modules/Payments`. IsolationTests will fail a project reference; that is the point.*
