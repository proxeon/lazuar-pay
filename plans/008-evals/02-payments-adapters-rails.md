# 02 — Payments adapters, capabilities, webhooks, wallets

**Date:** 16 August 2026  
**Slice:** Payments module + ops / admin payment-settings surfaces + Commerce capability consumers  
**Parent:** [README.md](./README.md)  
**Code authority:** tree as of Waves 0–4. Historical `plans/007-feats` tracker cells are cited only when this report re-checks the code.

This report is uncondensed. It does not implement anything. It does not treat a Wave 4 `*-done.md` as truth unless the corresponding source still matches.

---

## 0. What this slice is

Lazuar Pay is a **BYOK cashier**, not an acquirer and not a Merchant of Record. Money settles on the tenant’s Stripe / CHIP / Billplz / Razorpay / Xendit account. The Payments module is the port: generate a hosted checkout URL, verify the processor’s webhook, publish `GatewayPayment*` / `GatewayRefund*` / `GatewayDispute*` integration events, and (for Stripe and CHIP only) run an off-session charge when Commerce asks.

The factory after Waves 0–4 registers **five** adapters:

```34:39:apps/lazuar-api/Modules/Payments/Infrastructure/DependencyInjection.cs
        services.AddScoped<IPaymentGatewayAdapter, StripeGatewayAdapter>();
        services.AddScoped<IPaymentGatewayAdapter, BillplzGatewayAdapter>();
        services.AddScoped<IPaymentGatewayAdapter, RazorpayGatewayAdapter>();
        services.AddScoped<IPaymentGatewayAdapter, ChipCollectGatewayAdapter>();
        services.AddScoped<IPaymentGatewayAdapter, XenditGatewayAdapter>();
        services.AddScoped<IPaymentGatewayFactory, PaymentGatewayFactory>();
```

`PaymentGatewayFactory.GetAdapter` uppercases the type and throws if nothing matches (`apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/PaymentGatewayFactory.cs:14-24`). There is no Fiuu, Midtrans, Cashfree, SenangPay, or PayPal class in `Infrastructure/Gateways/`.

The inbound webhook allow-list is the same five names:

```13:20:apps/lazuar-api/Modules/Payments/Infrastructure/Endpoints.cs
    private static readonly HashSet<string> AllowedGatewayTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "STRIPE",
        "BILLPLZ",
        "RAZORPAY",
        "CHIP",
        "XENDIT"
    };
```

M2M checkout (`POST /integrations/payments/checkouts`) allow-lists the same five (`CreateIntegrationCheckoutCommandHandler.cs:17-20, 60-64`).

That is the compiled rail set. Marketing and the root README have not caught up (see §14).

---

## 1. Port shape (what every adapter must implement)

`IPaymentGatewayAdapter` is one port for all five rails (`apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs:27-76`):

| Method | Meaning |
|--------|---------|
| `GenerateCheckoutAsync` | Create a hosted hop-2 session. Returns URL + provider session id. |
| `ParseWebhookAsync` | Verify signature/token, map to `PAYMENT_COMPLETED` / `PAYMENT_FAILED` / `DISPUTE_CREATED` / passthrough. |
| `IssueRefundAsync` | POST a processor refund. `bool` only — no refund id, no fee reclaimed. |
| `GenerateCustomerPortalAsync` | Stripe Billing Portal or throw. |
| `ChargeOffSessionAsync` | Merchant-initiated charge against a stored token. |

`GatewayWebhookParsedResult` carries `Verified`, `EventType`, `EventId`, amounts, `GatewayTransactionId`, metadata, optional `GatewayCustomerId` / `GatewayTokenId` (`IPaymentGatewayAdapter.cs:10-25`). There is **no** refund event type on this record. There is **no** `Supports*` property on the interface. Capability is a static helper in Contracts, not a method on the adapter.

Shared money math lives in `GatewayCommon` (`GatewayCommon.cs:42-49`): CHIP/Xendit use banker's `ToMinorUnitsRounded`; Billplz/Razorpay use truncating `ToMinorUnitsTruncating`. Stripe multiplies by 100 in the adapter itself (`StripeGatewayAdapter.cs:257, 285, 442`). That split is intentional and tested; it is also a place where a 0.5 sen can diverge by rail.

`CheckoutSessionCashier` is the one generate path for Commerce hop 2, M2M, and detailed queries (`CheckoutSessionCashier.cs:33-115`). It decrypts the tenant key, stamps `hub_payment_environment`, and refuses a soft-disabled or missing config when `requireActiveGateway` is true. Preferred gateway → first active tenant config → legacy `"BILLPLZ"` last resort only when `requireActiveGateway` is false (`CheckoutSessionCashier.cs:117-144`). M2M never takes that last resort.

---

## 2. `PaymentGatewayCapabilities` — the honest matrix type

The entire capability story after Waves 0–4 is one static class:

```1:58:apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs
/// Honest collection-mode matrix. Only Stripe and CHIP Collect can vault and charge off-session.
/// Billplz, Razorpay (not demoable), unknown, and blank names are reminder-only.
/// Refund capability is a separate axis: Razorpay can API-refund; Billplz cannot.
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
}
```

Normalization is trim + `ToUpperInvariant` (`PaymentGatewayCapabilities.cs:57`). Unknown names, `null`, and `""` are reminder-only and not API-refundable. Blank / offline names require mark-refunded.

Tests lock the matrix (`PaymentGatewayCapabilitiesTests.cs:10-60`):

- Off-session: `STRIPE` / `CHIP` (any case / padding) true; `BILLPLZ`, `RAZORPAY`, `""`, `null`, `UNKNOWN` false.
- API refund: `STRIPE`, `CHIP`, `RAZORPAY`, `XENDIT` true; `BILLPLZ` / null / empty false.
- Mark-refunded: Billplz + offline names true; Stripe / CHIP / Razorpay false. **Xendit is not in the mark-refunded test.** Code returns false for Xendit (API refund path).
- Xendit extras: off-session false, e-mandate false, DuitNow QR true, GrabPay hosted-wallet true; Billplz GrabPay hosted-wallet **false**.

That last pair is a product inconsistency: Billplz collections commonly show GrabPay / TnG / Boost when the merchant enabled them, and `SupportsDuitNowQr("BILLPLZ")` is true, but `SupportsHostedWallet("BILLPLZ", "GRABPAY")` is false. Wave 4 analysis said Billplz wallets are collection-default **P**. The code chose “QR yes, wallets no” without hop-1 copy either way.

### 2.1 Who actually reads the flags

| Flag | Readers in `apps/` |
|------|--------------------|
| `SupportsOffSession` / `IsReminderOnlyGateway` | BillingEngineJob `canCharge` (`BillingEngineJob.cs:238-241`); dunning AUTO_CHARGE skip (`PastDueDunningProcessor.cs:112-117`); campaign save guard (`DunningCampaignAutoChargeGuard.cs:51`); vault persist (`GatewayPaymentCompletedIntegrationEventHandler.Helpers.cs:79`); zero-amount checkout (`ProcessZeroAmountCheckoutCommand.cs:89`); trial vault (`InitiateCheckoutCommandHandler.cs:290`); product DTO (`CommerceQueryService.Products.cs:132`); arrears page (`PublicArrearsEndpoints.cs:50, 85`); off-session handler short-circuit (`ExecuteOffSessionChargeIntegrationEventHandler.cs:39`). |
| `SupportsApiRefund` / `RequiresMarkRefunded` | `RecordRefundCommandHandler.cs:82-111`; transaction DTO `supports_api_refund` (`CommerceQueryService.Transactions.cs:170`); ops RefundModal (`RefundModal.tsx:8, 33`). |
| `SupportsDuitNowQr` | **Tests + the static class.** Zero portal / ops / adapter generate-path readers under `apps/`. |
| `SupportsHostedWallet` | **Tests + the static class.** Zero hop-1 readers. |
| `SupportsEmandate` | Tests + the static class. Always false. No product toggle. |

Ops frontend duplicates off-session as `gatewaySupportsOffSession` (`apps/lazuar-ops/src/lib/utils.ts:14-22`): prefer API `supports_off_session` when present, else `STRIPE` or `CHIP`. It does **not** know about Xendit, Razorpay, or e-mandate. That is correct given the C# helper.

### 2.2 Capability matrix per gateway (code, not marketing)

| Axis | Stripe | CHIP | Billplz | Razorpay | Xendit | Offline / blank |
|------|--------|------|---------|----------|--------|-----------------|
| Hosted checkout | Y (`mode=payment`) | Y (`purchases/`) | Y (`bills`) | Y (payment link **or** card registration link) | Y (`/v2/invoices`) | n/a |
| Webhook verify | Stripe-Signature + `EventUtility.ConstructEvent` | RSA `X-Signature` vs PEM | HMAC-SHA256 `x_signature` (with/without extra fields) | HMAC `X-Razorpay-Signature` | Fixed-time `x-callback-token` | n/a |
| `EventId` | Stripe `evt_…` (unique per delivery) | **Purchase id** (same object on fail and pay) | **Bill id** (same object on unpaid and paid) | `X-Razorpay-Event-Id` else payment id | **Invoice id** | n/a |
| Business key | `EVENTTYPE:pi_…` / session PI | `EVENTTYPE:purchaseId` | `EVENTTYPE:billId` | `EVENTTYPE:pay_…` | `EVENTTYPE:invoiceId` | n/a |
| Off-session vault + charge | **Y** | **Y** (`force_recurring` + `/charge/`) | N (returns false) | Code exists, **capability false**, never called by engine | N (returns false) | N |
| FPX e-mandate | N | N | N | Label claims it; `method=card` only; `SupportsEmandate` false | Invoice only; `SupportsEmandate` false | N |
| DuitNow QR (our pixels) | N | N | N | N | N | N |
| DuitNow QR (hosted hop 2) | N | Flag true; no method filter | Flag true; collection default | N | Flag true; optional `QR_CODE` | N |
| GrabPay / ShopeePay / TnG / Boost (hosted) | N (session is `card` only) | Flag true; no method filter | Flag **false** (QR flag still true) | N | Flag true; filter has GRABPAY/SHOPEEPAY, **not** TNG/BOOST | N |
| Apple Pay / Google Pay | Wrap: `payment_method_types=['card']` | Not our wrap | N | N | N | N |
| API refund | Y (PI + idempotency key) | Y (`purchases/{id}/refund/`) | N (always false) | Y (`Payment.Fetch.Refund`) | Y (`POST /refunds` invoice_id) | N |
| Mark-refunded | N | N | **Required** | N | N | **Required** |
| Refund webhook closed loop | **No** (no `charge.refunded` map) | **No** (`payment.refunded` registered, not mapped) | n/a | **No** | **No** | n/a |
| Customer portal | Stripe Billing Portal | throws | throws | throws | throws | n/a |
| Disputes inbound | `charge.dispute.created` | N | N | N | N | n/a |
| Ops credential form | Y | Y (Brand ID + key; RSA auto-fetch) | Y (Collection + 128-char X-Signature) | Y (KeyId:KeySecret) | **Dropdown only; no fields** | n/a |
| Reminder-only product copy | Auto-debit | Auto-debit | Pay-link (honest) | Pay-link **but dropdown says e-mandate** | Pay-link **but dropdown says wallets** | Manual |

---

## 3. Stripe adapter

File: `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs`.

### 3.1 Checkout

`GenerateCheckoutAsync` builds `SessionCreateOptions` via `CreateCheckoutSessionOptions` (`417-463`) and `SessionService.CreateAsync`. Mode is always `"payment"` (`433`). There is no Stripe Billing subscription object. Quantity is a line-item quantity (`448`). Currency is lowercased (`441`). `ApplyPayingTenantMetadata` keeps an incoming `tenant_id` (platform charges) and stamps `platform_tenant_id` when the adapter tenant differs (`404-415, 429`).

`ApplyCardWalletPaymentMethodTypes` sets `PaymentMethodTypes = ["card"]` (`390-397, 460`). Comment on 392-393: wallets ride on `card`; listing `apple_pay` / `google_pay` is invalid. This **replaces** Dashboard dynamic payment methods for the session. Stripe FPX, GrabPay, and Link will **not** appear on hop 2 for a Lazuar-created Checkout Session. That is the LP-037 trade: Apple/Google Pay can show; MY APMs on Stripe cannot, unless the merchant somehow still gets them via card-network wallets.

`ApplySetupFutureUsage` (`465-476`) when true: `PaymentIntentData.SetupFutureUsage = "off_session"` and `CustomerCreation = "always"`. Without a Customer, Stripe often returns no reusable PM. Tests lock both the card-only list and the customer-creation pairing (`StripeGatewayAdapterTests.cs:26-36, 216-239`).

Commerce first-time paid checkout sets `SetupFutureUsage` to `resolved.Interval != "one_time"` (`InitiateCheckoutCommandHandler.cs:350-362`). Recurring Stripe products therefore vault. One-time products do not.

### 3.2 Webhook

`ParseWebhookAsync` (`45-239`):

1. Requires `Stripe-Signature` (case-insensitive). Missing → `Verified=false`.
2. `EventUtility.ConstructEvent(rawBody, signature, webhookSecret)` — Stripe library verify. `StripeException` → `Verified=false`.
3. Maps:
   - `checkout.session.completed` / `payment_intent.succeeded` on a `Session` → `PAYMENT_COMPLETED`, `EventId = stripeEvent.Id`, `GatewayTransactionId = PaymentIntentId ?? session.Id`, vault ids from expanded PI (`customer` + `payment_method`) (`59-123`).
   - Same types on a `PaymentIntent` object → `PAYMENT_COMPLETED`, `EventId = stripeEvent.Id`, `GatewayTransactionId = pi.Id` (`125-184`).
   - `payment_intent.payment_failed` → `PAYMENT_FAILED` via `MapPaymentIntentPaymentFailed` (`187-191, 299-328`), decline code copied into metadata.
   - `charge.dispute.created` → `DISPUTE_CREATED`, metadata pulled from the PI when possible (`193-230`).
   - Anything else → verified passthrough with raw `stripeEvent.Type` and `stripeEvent.Id` (`232`). Handler then drops it (`ProcessGatewayWebhookCommandHandler.cs:83-88`).

Fee extraction expands `latest_charge.balance_transaction`. Expand failure logs a warning and leaves `GatewayFee=0` rather than blocking fulfillment (`99-102, 159-163`). That is a known honesty gap on the fee axis, not on paid/unpaid.

`EventId` is the Stripe event id (`evt_…`). Dual money events (`checkout.session.completed` + `payment_intent.succeeded`) share a **business key** `PAYMENT_COMPLETED:{PaymentIntentId}` (`ProcessGatewayWebhookCommandHandler.Idempotency.cs:13-22`). Event ids differ; business key collapses them. That is the intended Stripe dual-event design. Fail then later succeed uses **different** `evt_` ids and **different** business keys (`PAYMENT_FAILED:pi_x` vs `PAYMENT_COMPLETED:pi_x`). Stripe fail-then-pay is safe.

### 3.3 Off-session

`ChargeOffSessionAsync` (`241-274`) creates a PaymentIntent with `OffSession=true`, `Confirm=true`, metadata from `BuildOffSessionMetadata`, and `RequestOptions.IdempotencyKey = lazuar-offsession:{chargeAttemptId}` when Commerce supplied an attempt id (`339-352, 380-388`). Success is `succeeded` or `processing`. `StripeException` becomes `OffSessionDeclinedException` with decline code. The handler publishes `GatewayPaymentFailed` with that code (`ExecuteOffSessionChargeIntegrationEventHandler.cs:91-99`). Soft-disabled config is refused for off-session (`50-58`) but refunds still run (`GatewayRefundRequestedIntegrationEventHandler.cs:30` comment + test `SoftDisabledConfig_StillCallsAdapter`).

### 3.4 Refund

`IssueRefundAsync` (`276-297`) refunds a **PaymentIntent** by id, amount in minor units, idempotency `lazuar-refund:{transactionId}:{minor}` (`331-337`). Returns true for `succeeded` or `pending`. **Pending is treated as success.** Commerce will apply the refund when it sees `GatewayRefundCompleted` (see §12). There is no mapping of `charge.refunded`, `refund.updated`, or `refund.failed` in `ParseWebhookAsync`. The refund loop is adapter HTTP, not webhook.

### 3.5 Portal

`GenerateCustomerPortalAsync` (`478-498`) lists customers by email (limit 1) and creates a Billing Portal session. First customer with that email wins. No customer → `InvalidOperationException`. This is the only adapter that implements a portal.

### 3.6 Apple Pay / Google Pay (LP-037)

Shipped as a wrap, not a product:

- Session create sends `card` only (`StripeGatewayAdapter.cs:390-397, 460`).
- Ops Stripe blurb (`PaymentSettingsPage.tsx:329-333`): wallets appear on Stripe-hosted Checkout when the account can take cards and the device supports them; not on Billplz; Lazuar does not host wallet buttons or do domain verify.
- Admin twin (`PlatformPaymentSettingsPage.tsx:310-313`) same paragraph.
- Portal hop 1 has **no** Apple Pay / Google Pay logos, buttons, or copy (`OrderSummaryCard.tsx` recurring copy is only `summary.notAutoDebit` / `summary.cardSaved`; `messages.ts:55-56`).
- Tests forbid `apple_pay` / `google_pay` / `fpx` on the session list and assert non-Stripe adapters do not contain those strings (`StripeGatewayAdapterTests.cs:26-36, 81-84, 90-111`).

Tracker `LP-037` = **W** is honest. Claiming “Lazuar Apple Pay” would not be.

---

## 4. CHIP Collect adapter

File: `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs`.

### 4.1 Checkout

Requires `merchantId` as Brand ID (`43-46`). POST `https://gate.chip-in.asia/api/v1/purchases/` with Bearer key (`91-93`). Payload: brand, client email/name, one product (rounded minor units), `success_redirect` / `failure_redirect` / `cancel_redirect`, purchase metadata including `tenant_id` (`54-77`).

`setupFutureUsage` sets `force_recurring=true` and, if amount is 0, `skip_capture=true` (`79-87`). That is the CHIP vault path. There is **no** payment-method allow-list. FPX, DuitNow QR, wallets, cards, BNPL appear if the CHIP brand is configured for them. Lazuar does not request or suppress them.

Saving a new CHIP key from ops triggers `UpdatePaymentConfigCommandHandler` (`105-145`) to GET `public_key/` (stored as webhook PEM) and POST a webhook subscription for `purchase.paid`, `purchase.payment_failure`, `payment.refunded`, `purchase.preauthorized`. Localhost is rewritten to `lazuar-local-dev.com`. **`payment.refunded` is registered and then ignored by the parser** (§12).

### 4.2 Webhook

`ParseWebhookAsync` (`124-228`):

1. Requires `X-Signature` base64.
2. Imports webhook secret as RSA PEM, `VerifyData` SHA256 PKCS1. Fail → `Verified=false`.
3. Maps `purchase.paid` → `PAYMENT_COMPLETED`. Maps `purchase.payment_failure` → `PAYMENT_FAILED`. **`purchase.preauthorized` is explicitly not paid** (`155-167`, test `ParseWebhook_Preauthorized_IsVerified_NotPaymentCompleted`).
4. Stable id: nested `purchase.id` then root `id` (`ReadStablePurchaseId`, `369-382`). Missing → `Verified=false`, “Missing stable CHIP purchase id”. Never invents a Guid (test `ParseWebhook_PurchasePaid_NoIds_IsNotVerified`).
5. **`EventId = purchaseId`** (`177`). `GatewayTransactionId = purchaseId` (`211`). Fail and pay for the same purchase therefore share EventId. See §8.
6. Vault: `ExtractVaultIds` (`384-408`) prefers `recurring_token`, else purchase id when `is_recurring_token`, customer from `client.id` or fallback to token.

Fees from `payment.fee_amount` / `net_amount` when present (`188-192`).

### 4.3 Off-session

`ChargeOffSessionAsync` (`230-323`): GET original purchase by `tokenId`, clone brand + client, POST a new purchase, POST `purchases/{newId}/charge/` with `{ recurring_token: tokenId }`. Success statuses `paid` or `pending_charge`. **No idempotency key** — comment on 236: “CHIP purchase/charge has no idempotency key (best-effort).” `idempotencyKey` is discarded. A retried Commerce attempt can double-charge at CHIP if the first charge succeeded and the HTTP response was lost. Stripe is the only adapter with a real off-session idempotency key.

Capability `SupportsOffSession("CHIP")` is true, so Billing and dunning **will** call this path when vault ids exist.

### 4.4 Refund

`IssueRefundAsync` (`325-355`) POST `purchases/{transactionId}/refund/` with optional `{ amount }` in rounded minor units. HTTP success → true. No refund id returned. `payment.refunded` webhook is not mapped (see §12).

### 4.5 Portal

Throws `InvalidOperationException` (`357-360`).

---

## 5. Billplz adapter

File: `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs`.

### 5.1 Checkout

Requires Collection ID (`56-59`). Resolves callback base via `BillplzPublicBase.TryResolveCallbackBase` (`62-65`): must be public https unless `App:AllowInsecureBillplzCallback`. Loopback and `lazuar-local-dev.com` fail closed with `CALLBACK_BASE_NOT_PUBLIC`. Production vs sandbox API host is **not** inferred from Hub hostname (`BillplzPublicBase.cs:39-42`). It follows `App:BillplzEnvironment` then tenant `environment` (`test`|`live`) (`22-43`). LP-182 shipped this. Ops copy: “Hub hostname does not pick Billplz sandbox vs live” (`PaymentSettingsPage.tsx:235-237`).

Callback URL is `{base}/webhooks/payments/billplz/{tenantId}?type=&reference_1=` and optional `checkout_id` for M2M (`78-88`). Billplz does not persist arbitrary metadata; query string + server-side session merge are the recovery path (`ProcessGatewayWebhookCommandHandler.Metadata.cs:13-77`).

`setupFutureUsage` is an unused parameter. There is no vault. Recurring Commerce still passes `SetupFutureUsage: true` (`InitiateCheckoutCommandHandler.cs:359`, `RenewalCheckoutIssuer.cs:57`). Billplz ignores it. Honest.

Minor units **truncate** (`90`). Currency on the webhook is hardcoded `"MYR"` (`237`) — Billplz is MY-only in this wrap.

### 5.2 Webhook

Form body, not JSON (`ParseFormBody`, `311-322`). Verify (`144-166`):

1. Require `x_signature`.
2. HMAC-SHA256 over sorted `key+value` joined by `|`, excluding `x_signature`. First try including `paid_at` / `transaction_id` / `transaction_status`; if that fails, retry excluding those extra fields (`32-40, 157-166`). Fixed-time hex compare (`304-309`).
3. Missing/blank bill `id` → `Verified=false` (`171-176`).
4. Paid if `paid=true` or `state=paid` (`181-182`). Else **`PAYMENT_FAILED`**.
5. **`EventId = billId`** (`235`). `GatewayTransactionId = billId` (`238`). Unpaid then paid on the same bill share EventId. See §8.
6. Metadata from `reference_2` (type) and `reference_1` (subscription id, or `tenant_id` for platform types via `PlatformCheckoutTypes.IsPlatformCollected`) (`200-212`). `checkout_id` from `Query-checkout_id` or form (`214-224`).

Fee is `(paidAmount * estimatedFeePercentage / 100) + fixedFee` (`226-230`). **The webhook handler always passes `0, 0, 0`** (`ProcessGatewayWebhookCommandHandler.cs:74-76`, comments “removed from config”). Migration `20260705131411_RemoveAccountingOverrides` deleted tenant fee fields. Billplz `GatewayFee` is therefore **always 0** in production. The leftover `PaymentSettingsModal` still shows “Accounting Overrides” (`modules/workspace/components/PaymentSettingsModal.tsx:214-229`) that the API no longer stores. Stale UI.

### 5.3 Off-session

`ChargeOffSessionAsync` logs a warning and returns false (`255-267`). Does not throw. Capability short-circuit in the handler means Commerce should not call this; if it does, the handler treats `success=false` as `charge_declined` (`ExecuteOffSessionChargeIntegrationEventHandler.cs:111-115`).

### 5.4 Refund

```269:276:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs
    /// Billplz has no bill-refund API. A Payment Order is a new disbursement, not a reversal.
    /// Commerce must mark-refunded instead of calling this adapter.
    public Task<bool> IssueRefundAsync(...)
    {
        return Task.FromResult(false);
    }
```

`RequiresMarkRefunded("BILLPLZ")` is true. Ops RefundModal SOP: refund in Billplz dashboard, then mark (`RefundModal.tsx:44-48`). Test `IssueRefundAsync_AlwaysReturnsFalse`.

### 5.5 Honesty on auto-debit

Ops Billplz form (`PaymentSettingsPage.tsx:281-285`):

> **Pay-link renewals.** Billplz cannot vault. Each cycle we create a hosted bill and email it. There is no silent auto-charge (subscription renewals, dunning AUTO_CHARGE). Use Stripe or CHIP Collect when you need recurring auto-debit.

Admin twin (`PlatformPaymentSettingsPage.tsx:262-266`) same banner. Product form pay-link banner (`ProductForm.tsx:171-174`) and hop 1 `summary.notAutoDebit` (`messages.ts:55`, `OrderSummaryCard.tsx:155-158`) when `supportsOffSession === false`. **We do not claim auto-debit on Billplz.** That Wave 0 / Wave 1 honesty landed.

---

## 6. Razorpay / Curlec adapter

File: `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs`.

### 6.1 Checkout — card registration, not e-mandate

API key format `KeyId:KeySecret` (`24-30`). Two generate paths (`58-99`):

- `setupFutureUsage == true`: `Invoice.CreateRegistrationLink` with `subscription_registration.method = "card"`, `max_amount = amountPaise * 10`, `expire_at` +10 years (`58-82`).
- else: `PaymentLink.Create` (`84-98`).

There is **no** `method=emandate` / FPX mandate path. W4-LP-044-analysis named that as G3 and left it for LP-032. W4-LP-044-done kept card-only. W4-LP-032-done: `SupportsEmandate` hard-false; do not claim auto-debit.

Ops / admin / leftover modal **label**:

```210:210:apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx
                      <option value="RAZORPAY">Razorpay / Curlec (MY e-mandate + cards)</option>
```

Same string in `PlatformPaymentSettingsPage.tsx:205`, `modules/workspace/components/PaymentSettingsModal.tsx:137`, `components/PaymentSettingsModal.tsx:131`.

That label is a leftover claim. The adapter registers a **card**. `SupportsOffSession("RAZORPAY")` is false. `SupportsEmandate` is false. Billing will not off-session charge. If a recurring Razorpay product is created, hop 1 shows **Not auto-debit** (`supports_off_session` from C# is false). The dropdown still says “e-mandate”. Sales reading the dropdown will lie; the checkout page will not.

Worse: Commerce still sends `SetupFutureUsage: true` for every recurring interval (`InitiateCheckoutCommandHandler.cs:359`) and every renewal mint (`RenewalCheckoutIssuer.cs:57`). On Razorpay that **does** create a registration link (card), not a plain payment link. Buyer may authorize a card. Webhook may return `customer_id` / `token_id` (`RazorpayGatewayAdapter.cs:172-173`). Commerce `TryVaultIds` then **discards** them because `IsReminderOnlyGateway("RAZORPAY")` is true (`GatewayPaymentCompletedIntegrationEventHandler.Helpers.cs:79-82`). So we put the buyer through a card-registration UX and then throw the token away and treat the subscription as reminder-only. That is the leftover of “finish the pipe” without flipping the capability.

### 6.2 Webhook

`Utils.verifyWebhookSignature` (`120`). Missing `X-Razorpay-Signature` → not verified.

Mapped events (`125-133, 301-302`):

- `payment.failed` / `invoice.expired` → `PAYMENT_FAILED` (`MapPaymentFailed`).
- `payment.captured` → `PAYMENT_COMPLETED`.
- else verified passthrough.

`EventId`: prefer `X-Razorpay-Event-Id`, else payment id. Missing both → `Verified=false`, never invent a Guid (`138-156, 336-349`). Currency missing → fail closed, no invented MYR (`174-179, 364-371`). Tests: `ParseWebhook_CapturedWithoutCurrency_DoesNotInventMyr`, `ParseWebhook_HeaderEventIdAndPaymentId_MapsIdentities`.

If Razorpay sends the Event-Id header (they usually do), fail and capture have **different** EventIds. Fail-then-pay is safe. If a delivery omits the header and both events fall back to the same `pay_…`, EventId collides the same way as CHIP/Billplz. Fallback is the residual.

### 6.3 Off-session (dead to the engine)

`ChargeOffSessionAsync` (`206-278`) creates an order then `Payment.CreateRecurringPayment` with `recurring=true`. W4-LP-044 removed hardcoded `billing@lazuar.com`. Replacement: copy `customer_email` / `customer_phone` **from the notes dictionary this method just built** (`217-233, 256-268`). Those notes only contain `type`, `subscription_id`, `tenant_id`, `receipt`, optional dunning/attempt ids. They never contain `customer_email`. The “buyer email if present” branch is dead. Dummy email is gone; real email is also gone. Capability false means Billing never calls this. The method is leftover pipe.

No idempotency key (comment line 212).

### 6.4 Refund

`Payment.Fetch(transactionId).Refund` (`280-294`). `transactionId` must be a Razorpay **payment** id (webhook `GatewayTransactionId`). Amount truncated to paise. True if SDK returns non-null. No refund webhook map.

### 6.5 Portal

Throws (`296-299`).

---

## 7. Xendit adapter

File: `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/XenditGatewayAdapter.cs`.

Wave 4 LP-045 **did** ship a class. The class comment (`16-19`) is accurate: BYOK hosted invoices; money on the tenant Xendit account; reminder-only until payment tokens soak; we do not rebuild wallets.

### 7.1 Checkout

POST `https://api.xendit.co/v2/invoices` Basic `apiKey:` (`58-60`). `setupFutureUsage` discarded (`50-51`). `external_id = lazuar_{guid}` (`189`). Amount is rounded minor units / 100 (major units, Xendit style) (`184`). Optional `payment_methods` from metadata key `xendit_payment_methods` filtered to `MalaysiaHostedChannels` (`199-237`):

```
CREDIT_CARD, DD_FPX, QR_CODE, OVO, DANA, LINKAJA, SHOPEEPAY, GCASH, GRABPAY, PAYMAYA
```

Unknown codes dropped (test `BuildInvoicePayload_FiltersUnknownChannels`). Empty list = merchant dashboard defaults. There is **no TnG, Boost, or named DUITNOW** in the allow-list. `SupportsHostedWallet("XENDIT", "TNG"|"BOOST"|"DUITNOW")` is nevertheless true. `SupportsDuitNowQr("XENDIT")` is true; the requestable code is `QR_CODE`, not `DUITNOW`. Capability names and Xendit codes do not match 1:1.

Nothing in Commerce/portal sets `xendit_payment_methods`. Production invoices therefore use dashboard defaults. The filter is an unused hook.

### 7.2 Webhook

`VerifyCallbackToken`: webhook secret required; header `x-callback-token`; fixed-time compare (`240-256`). Missing/mismatch → not verified. `apiKey` unused (token, not HMAC of body).

`MapInvoiceCallback` (`258-335`): unwraps `data` if present. `PAID` / `SETTLED` / `invoice.paid` → `PAYMENT_COMPLETED`. `EXPIRED` / `FAILED` / `invoice.expired` / `invoice.failed` → `PAYMENT_FAILED`. Missing invoice id or currency → not verified (no invented MYR). **`EventId = invoiceId`**. `PAID` then `SETTLED` share EventId and share business key `PAYMENT_COMPLETED:{id}` — intended idempotency. `EXPIRED` then a later `PAID` on the same invoice (unusual) would collide like CHIP. Typical Xendit invoices do not resurrect after EXPIRED.

No refund / dispute events.

### 7.3 Off-session

Always `false` (`155-171`). Comment: hosted invoices do not vault. Test `ChargeOffSession_AlwaysFalse_UntilTokenSoak`. Capability agrees.

### 7.4 Refund

POST `/refunds` with `invoice_id`, `amount` in **major** units, `reason=REQUESTED_BY_CUSTOMER` (`119-148`). HTTP success → true. This is unsoaked. Xendit’s refund API historically wants a payment id more often than an invoice id. If Commerce stored the invoice id as `ExternalReference` (it will — `GatewayTransactionId` is the invoice id), a 4xx becomes adapter false → `REFUND_FAILED`. Ops can retry. There is no mark-refunded fallback for Xendit (`RequiresMarkRefunded` is false).

### 7.5 Is Xendit operable from the ops UI?

**No.**

Evidence:

1. Dropdown includes Xendit (`PaymentSettingsPage.tsx:7, 211`; admin `PlatformPaymentSettingsPage.tsx:7, 206`; both leftover modals).
2. Credential blocks are only `CHIP`, `BILLPLZ`, `STRIPE`, `RAZORPAY`. After the Razorpay block the form ends (`PaymentSettingsPage.tsx:362-392`). Selecting Xendit shows Gateway Type + Active + Environment and an **empty** Secure Credentials section.
3. `handleSubmit` has no `XENDIT` validation and no Xendit-specific required fields (`81-128`). Saving first-time Xendit PUTs empty `api_key` / `webhook_secret`. Backend first-time create requires an API key (`UpdatePaymentConfigCommandHandler.cs:147-151`). The UI cannot supply one.
4. TypeSpec `SavePaymentConfigRequestDto` is generic (`payment-config.tsp:30-42`). A raw `PUT /admin/commerce/payment-config` with `gateway_type=XENDIT` **can** persist keys. M2M allow-list already includes XENDIT. The **page a merchant uses cannot**.
5. Product form lists whatever `GET /admin/commerce/payment-config` returns (`ProductForm.tsx:166-168`). If someone configured Xendit via API, the product dropdown would show `XENDIT` and hop 1 would treat it as reminder-only (correct).

W4-LP-045-done claimed “ops/admin dropdown include `XENDIT`.” That is the leftover: dropdown without a form. Acceptance in W4-LP-045-analysis (“Tenant pastes Xendit keys”) is **not** met by the UI.

### 7.6 README / docs still say Xendit does not exist

Root README honest watermark (`README.md:18`): “WhatsApp dunning, Xero/QuickBooks sync, and Xendit are **not** shipping until their adapters exist.”

Phase 1 (`README.md:74`): “Stripe, Billplz, CHIP, Razorpay/Curlec. Xendit is a planned wrap, not a live adapter.”

`docs/001-gaps/20-architecture-intent-vs-implementation.md:278`: “**Stripe, Billplz, CHIP, Razorpay** only — **no** Fiuu, SenangPay, Xendit, Midtrans, Cashfree.”

`apps/lazuar-api/Modules/Payments/README.md` still lists Stripe + Billplz only (`§6`) and claims checkouts are stateless (`§3`) even though `IntegrationCheckoutSessions` exist.

Code: adapter registered, webhook allow-listed, tests green. Docs and UI are the leftover claims. Tracker `LP-045` = **W** matches the class, not the operable product.

---

## 8. Webhook verify, EventId, business-key idempotency

### 8.1 Intake path

`POST /webhooks/payments/{gatewayType}/{tenantId}` (`Endpoints.cs:26-89`):

- Unknown gateway → 400 (allow-list).
- Empty body → throw (not 400; will 500). Residual.
- Headers copied; query params copied as `Query-{key}` (Billplz checkout_id / type).
- Handler runs; HTTP 200 `{ received: true }` is **intake ACK**, not paid (`Endpoints.cs:73-74`, W0-LP-090-done).

Handler (`ProcessGatewayWebhookCommandHandler.cs:56-117`):

1. Load tenant config for that gateway. Missing webhook secret → throw (gateway retries).
2. Soft-disable does **not** skip webhooks (`64` comment) — historical payments still fulfill.
3. Decrypt keys. `ParseWebhookAsync` with fee args **hardcoded 0**.
4. `!Verified` → throw (retry).
5. EventType not in `PAYMENT_COMPLETED` / `PAYMENT_FAILED` / `DISPUTE_CREATED` → return (ACK, no log).
6. `businessKey = EventType + ":" + GatewayTransactionId` if tx id present (`Idempotency.cs:13-22`).
7. Lookup `GetByEventId(EventId, Provider)` then `GetByBusinessKey`.
8. Existing → `HandleExistingLogAsync`: skip if no `OutboxMessageId` (pre-ticket backfill); requeue Dead outbox; if already active, **return**; if outbox missing, republish.
9. Fresh → merge IntegrationCheckoutSession metadata by `ProviderSessionId == GatewayTransactionId`, insert `PaymentWebhookLog`, publish, save. Unique 23505 on insert is treated as successful duplicate (`Idempotency.cs:24-57`).

Unique indexes (`PaymentConfigurations.cs:29-35`, initial migration `103-108`, business-key migration `20-26`):

- `(Provider, EventId)` unique always.
- `(Provider, BusinessKey)` unique where BusinessKey is not null.

### 8.2 What each rail uses for EventId

| Rail | EventId | Unique per delivery? | Fail vs pay same object |
|------|---------|----------------------|-------------------------|
| Stripe | `stripeEvent.Id` (`evt_`) | Yes | Different evt ids |
| Razorpay | header Event-Id, else `pay_` | Header yes; fallback no | Safe if header present |
| CHIP | purchase id | **No** | **Same id** |
| Billplz | bill id | **No** | **Same id** |
| Xendit | invoice id | No | Same id (EXPIRED vs PAID rare) |

W0-LP-090 made missing ids **fail-closed**. It did **not** namespace EventId by event type. Tests assert CHIP fail EventId equals purchase id (`ChipCollectGatewayAdapterTests.cs:197-210`) and Billplz unpaid EventId equals bill id (`BillplzGatewayAdapterTests.cs:262-280`). There is **no** test that a later COMPLETED with the same EventId still publishes.

### 8.3 CHIP / Billplz fail-then-pay EventId collision

This is the money bug.

Sequence on CHIP:

1. `purchase.payment_failure` for `purch_1` → `EventId=purch_1`, `EventType=PAYMENT_FAILED`, business key `PAYMENT_FAILED:purch_1`. Log inserted. `GatewayPaymentFailed` published. Commerce may mark PAST_DUE / failed checkout.
2. Buyer pays the same purchase. `purchase.paid` → `EventId=purch_1`, `EventType=PAYMENT_COMPLETED`, business key `PAYMENT_COMPLETED:purch_1`.
3. Handler `GetByEventId("purch_1", "CHIP")` finds the **failure** row (`ProcessGatewayWebhookCommandHandler.cs:91-97`).
4. `HandleExistingLogAsync` (`119-160`): outbox from the **failed** event is AlreadyActive → **return**. No new log (unique EventId would reject it anyway). **`GatewayPaymentCompleted` is never published.**
5. Commerce does not activate. M2M session stays open. Buyer paid. We ACK 200.

Billplz is the same with `EventId=billId` (`BillplzGatewayAdapter.cs:232-235`, unpaid test 262-280). Billplz often only callbacks when paid; an early `paid=false` / `state=due` callback is enough to poison the id. The adapter treats any non-paid verified callback as `PAYMENT_FAILED`. A create-time or abandoned-bill callback followed by a real pay is the collision.

Business keys would **not** collide (`PAYMENT_FAILED:id` ≠ `PAYMENT_COMPLETED:id`). EventId lookup runs first and unique `(Provider, EventId)` makes a second row impossible. The dual-event machinery that saved Stripe (`evt_` + business key on PI) is what sinks CHIP/Billplz.

HandleExistingLog republish cannot save this: it republishes using the **new** parsed result only when outbox is Missing. AlreadyActive (the common case after a processed fail) drops the pay.

**P0.** Fix shape (not implemented here): EventId must be unique per **delivery or type**, e.g. `{eventType}:{purchaseId}` or `{purchaseId}:{event_type}` or CHIP’s own event uuid if present. Business key stays the money-level key. Do not use the object id alone as EventId on rails that emit more than one terminal state for that object.

Xendit `PAID` vs `SETTLED` is the opposite (same type, same key) and is fine. Razorpay with Event-Id header is fine. Stripe is fine.

### 8.4 Session metadata merge

Billplz strips body metadata. Handler merges `IntegrationCheckoutSession` by provider session id (`ProcessGatewayWebhookCommandHandler.Metadata.cs:16-77`): adapter keys win; session fills holes; always stamp `checkout_id`. Merge errors are swallowed so money still publishes. Tests cover query `checkout_id` and stripped-bill merge (`BillplzGatewayAdapterTests.cs:56-88`, `ProcessGatewayWebhookCommandHandlerTests` bill merge case).

---

## 9. Do we claim auto-debit on reminder-only rails?

**Commerce engine: no.**  
**Ops product + hop 1: no for Billplz / Razorpay / Xendit / offline.**  
**Ops gateway dropdown: Razorpay label still says e-mandate.**

Engine gates:

```238:241:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs
        var canCharge = PaymentGatewayCapabilities.SupportsOffSession(product.GatewayName)
                        && !sub.IsReminderOnly
                        && !string.IsNullOrEmpty(sub.VaultedTokenId)
                        && !string.IsNullOrEmpty(sub.VaultedCustomerId);
```

Dunning AUTO_CHARGE uses the same four predicates plus attempt cap and hard-decline skip (`PastDueDunningProcessor.cs:112-117`). Campaign save rejects AUTO_CHARGE when every targeted product is `!SupportsOffSession` (`DunningCampaignAutoChargeGuard.cs:49-54`, message “AUTO_CHARGE is not available for Billplz / reminder-only products”).

Vault persist refuses reminder-only gateways even if the webhook carried token ids (`Helpers.cs:79-82`). Paid Billplz / Razorpay / Xendit recurring checkout activates `IsReminderOnly=true` (Wave 0 LP-047).

Hop 1 (`OrderSummaryCard.tsx:155-163`, `messages.ts:55-56`):

- `supportsOffSession === false` → “Not auto-debit. We email a new payment link each cycle.”
- `supportsOffSession` true → “Your card will be saved for renewals.”

There is **no** “we will debit your bank via FPX e-mandate” copy. There is **no** DuitNow / wallet line (Wave 4 analysis wanted one; it did not land).

Ops product form (`ProductForm.tsx:171-179`) and `CreateProductForm.tsx:105-113`:

- Reminder-only recurring: “Collection mode: pay link each cycle… AUTO_CHARGE will not run.”
- Off-session recurring: “Auto-debit: card is saved for renewals.”

`CreateProductForm.tsx:107` wording is sloppy (“hosted Billplz/CHIP/Stripe page”) even when the selected gateway is Razorpay or Xendit, and it names CHIP/Stripe in a reminder-only banner even though those two are the auto-debit rails. `ProductForm.tsx:173` is the same sentence. Leftover copy, not an auto-debit claim.

Billplz settings banner is explicit no-auto-charge (`PaymentSettingsPage.tsx:281-285`).

**Net:** we do not tell the buyer or the billing job that Billplz / Xendit / Razorpay will silent-debit. We do tell the merchant, via the Razorpay dropdown, that the rail is “MY e-mandate + cards.” That is the leftover claim.

`RenewalCheckoutIssuer` and arrears `PublicArrearsEndpoints` always pass `SetupFutureUsage: true` (`RenewalCheckoutIssuer.cs:57`, `PublicArrearsEndpoints.cs:156`). On Stripe/CHIP that is correct (re-vault). On Billplz/Xendit it is ignored. On Razorpay it opens a **card registration** link for a reminder-only product. That is not a copy claim; it is a hop-2 UX lie.

---

## 10. Apple Pay / Google Pay wrap (card types only)

Covered in §3.6. Restated against the assigned question:

- Implemented only on Stripe.
- Implementation is `PaymentMethodTypes = ["card"]`, not `apple_pay` / `google_pay`.
- Wallets tokenize as cards; off-session uses the same PM id.
- Domain verification is not done in Lazuar; we never host Payment Request buttons.
- CHIP / Billplz / Razorpay / Xendit adapters contain none of those strings (test `NonStripeAdapters_DoNotSendApplePayOrPaymentMethodTypes`).
- Adding Stripe `grabpay` later would be a **manual** list that must keep `card` (W4-LP-035-analysis). Not shipped. Stripe GrabPay is therefore **off** on Lazuar sessions.

Wave 1 LP-037 = **W**, leftover hop-1 logos reserved as `LP-UX-010`. Honest.

---

## 11. Wave 4 shipped vs leftover claims

Wave 4 implement-ids (`00-implement-ids.md:119-129`): LP-032 e-mandate, LP-044 finish Razorpay, LP-045 Xendit, LP-033 DuitNow, LP-034 TnG, LP-035 GrabPay, LP-036 ShopeePay/Boost (plus WhatsApp / Xero / receipt honesty, out of this slice).

| ID | Tracker cell (007, not authority) | Code after Waves 0–4 | Leftover |
|----|-----------------------------------|----------------------|----------|
| **LP-032** FPX e-mandate | N (done file agrees) | `SupportsEmandate` always false (`PaymentGatewayCapabilities.cs:44-49`). No product toggle. Razorpay registration is `method=card`. Xendit invoices do not vault. W4-LP-032-done: “Do not claim auto-debit.” | Ops Razorpay **label** still claims e-mandate. |
| **LP-044** Razorpay / Curlec | W | Failed events mapped; no dummy email; no invented MYR; Curlec in the label. `SupportsOffSession` still false. | Label oversells e-mandate. ChargeOffSession email branch dead. Recurring `SetupFutureUsage` still creates a card registration then discards the token. No sandbox soak. |
| **LP-045** Xendit | W | Adapter + factory + webhook allow-list + M2M + capabilities + tests. Off-session false. | **Ops/admin cannot paste keys.** README still says adapter does not exist. `xendit_payment_methods` unused. Refund `/refunds`+`invoice_id` unsoaked. |
| **LP-033** DuitNow QR | W (033-done) | Flag true for Xendit/CHIP/Billplz. No QRCoder. No `GenerateQrAsync`. | Flag unread by portal. No hop-1 “scan on next page.” CHIP/Billplz send no method code. Xendit code is `QR_CODE` only if metadata set. |
| **LP-034** TnG | analysis only; 033-done bundled | `SupportsHostedWallet(..., TNG\|TOUCHNGO)` true for Xendit/CHIP only. | No hop-1 copy. Xendit allow-list has no TNG. Billplz flag false. No `W4-LP-034-done.md`. |
| **LP-035** GrabPay | same | Flag true Xendit/CHIP. Xendit allow-list includes `GRABPAY`. Stripe session is card-only (GrabPay off). | No hop-1 copy. Billplz flag false. |
| **LP-036** ShopeePay / Boost | same | ShopeePay in Xendit allow-list. Boost in capability list, **not** in Xendit `MalaysiaHostedChannels`. | Boost cannot be requested. No hop-1 copy. |

W4-LP-033-done text: “No homemade QR or wallet pixels… Hop 2 is still the processor page.” That part is true. The analysis acceptance “hop 1 one-liner when the gateway advertises QR” is **not** in `OrderSummaryCard` or `messages.ts`. Grep of `apps/` for DuitNow / GrabPay / ShopeePay / Touch n Go / Boost outside tests and the Xendit channel list is empty.

WhatsApp (LP-074 / LP-155), Xero (LP-121), receipt honesty (LP-100) are other slices.

### 11.1 Waves 0–3 payments leftovers that still matter here

| ID | What shipped | Residual that hits rails |
|----|--------------|--------------------------|
| LP-047 | Capability + engine skip + ops badges | Reminder-only Razorpay still hits registration-link generate. |
| LP-053 | Hop 1 not-auto-debit; pay-link product copy | CreateProductForm names “Billplz/CHIP/Stripe” in the pay-link banner. |
| LP-090 | Verify + EventId fail-closed + business key + dead outbox requeue | **EventId = object id** on CHIP/Billplz. |
| LP-037 | Stripe `card` list | No hop-1 wallets. Stripe FPX/GrabPay/Link gone from session. |
| LP-091/092/093 | Refund command + mark-refunded + ops modal | Refund close is adapter bool, not webhook. |
| LP-182 | test/live environment for Billplz host | Xendit/Razorpay/CHIP do not branch host on `environment` (CHIP/Xendit live URLs only). |

---

## 12. Refunds: adapter vs webhook closed loop

### 12.1 What closed means in this codebase

There is **no** inbound refund webhook type. `ProcessGatewayWebhookCommandHandler` only publishes completed / failed / dispute (`83-88, 162-208`). Adapter `ParseWebhookAsync` never returns a refund EventType.

CHIP **subscribes** to `payment.refunded` (`UpdatePaymentConfigCommandHandler.cs:133`). Parser treats unknown `event_type` as passthrough with empty EventId (`164-167`). Handler returns without logging. The subscription is noise.

Stripe does not map `charge.refunded` / `refund.*`. Razorpay does not map `refund.*`. Xendit does not map refund callbacks.

### 12.2 The actual loop (API rails)

1. Ops RefundModal POST `/admin/commerce/transactions/{id}/refund` (`RefundModal.tsx:61-70`). API rails (`STRIPE`, `CHIP`, `RAZORPAY`, `XENDIT` hardcoded in the modal plus `supports_api_refund`) omit `mark_refunded`. Billplz / offline send `mark_refunded: true`.
2. `RecordRefundCommandHandler` (`82-126`):
   - `RequiresMarkRefunded` → require `MarkRefunded`, `ApplyRefund` immediately, publish `GatewayRefundCompleted`, return `"refunded"`.
   - else require `SupportsApiRefund`, `MarkRefundPending`, publish `GatewayRefundRequested`, return `"refund_requested"`.
3. `GatewayRefundRequestedIntegrationEventHandler` (`28-73`): load config (soft-disable still refunds), reject amount ≤ 0, `adapter.IssueRefundAsync`. True → `GatewayRefundCompleted` with `RefundedFee = 0` (comment: “until webhook enrichment exists”). False → `GatewayRefundFailed`.
4. Commerce `GatewayRefundCompletedIntegrationEventHandler` (`35-42`): apply **only** if status is `REFUND_PENDING`. Mark-refunded already applied; redelivery no-op.
5. `GatewayRefundFailedIntegrationEventHandler` (`30-36`): `MarkRefundFailed` only from pending.

Ops polls while `REFUND_PENDING` (`TransactionsPage.tsx` / `SubscribersPage.tsx`). Toast on request is “Refund requested”, not “Refunded” (`RefundModal.tsx:75-76`). That honesty is good.

### 12.3 Where the loop is not closed

- **Stripe `pending` refund counts as adapter success** (`StripeGatewayAdapter.cs:290`). Commerce applies when the completed event lands. If Stripe later fails the refund, we have no inbound mapper to flip `REFUND_FAILED` or reverse the ledger. Money-out can be a lie until someone looks at the Dashboard.
- **No processor-initiated refund** (dashboard refund, chargeback auto-refund) ever reaches Commerce. `LP-PAY-022` in historical 13-payments file is still the name for inbound dashboard refunds. Not shipped.
- **Fee reclaim is always 0** (`GatewayRefundRequestedIntegrationEventHandler.cs:51-53`).
- **Xendit refund API** may not accept `invoice_id`. Failure is at least visible (`REFUND_FAILED`, retry). There is no mark-refunded escape hatch for Xendit.
- **Billplz** is honest: no API, mark after desk refund.
- **Idempotency:** Stripe refund key `lazuar-refund:{pi}:{minor}` is the only adapter-level refund idempotency. CHIP/Razorpay/Xendit retries can double-refund at the processor if the first call succeeded and we lost the HTTP response. Commerce pending guard blocks a second **click** while pending; it does not protect the worker retry of `IssueRefundAsync`.

Refunds after Wave 1 are a **Commerce pending + adapter bool** loop. They are not a webhook closed loop. That is the accurate sentence.

Default `GatewayName = "STRIPE"` on `GatewayRefundRequestedIntegrationEvent` (`GatewayRefundRequestedIntegrationEvent.cs:13`) is leftover; `RecordRefundCommandHandler` always passes the resolved gateway (`121`). Do not reintroduce a Stripe default in callers.

---

## 13. Ops and admin payment settings (what a merchant can actually configure)

Canonical page: `apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx` (route `/workspace/payment-gateways`). Admin twin: `apps/lazuar-admin/src/modules/platform/pages/PlatformPaymentSettingsPage.tsx` (`GET/PUT /platform/payment-config`).

Both:

| Gateway | Fields shown | First-save validation | Extra copy |
|---------|--------------|----------------------|------------|
| CHIP | Brand ID, Secret Key | Brand + API key | Auto-fetch RSA + register webhooks |
| Billplz | Collection ID, API key, 128-char X-Signature | All three; signature length 128 | Pay-link / no auto-charge banner |
| Stripe | Secret key, webhook secret | Secret key | Apple/Google Pay wrap paragraph |
| Razorpay | `KeyId:KeySecret`, webhook secret | API key | Format hint only — **no** “reminder-only until soak” |
| Xendit | **None** | **None** | Label only |

Shared: `is_active` soft-disable; ops also has `environment` test/live (`PaymentSettingsPage.tsx:225-238`). Admin page **omits** the environment select (`PlatformPaymentSettingsPage.tsx` has no environment state). Platform vault used for utility top-ups / Hub SaaS fee (`GenerateSystemCheckoutSessionQueryHandler`) therefore cannot set test/live from admin UI; it will infer from Stripe-shaped keys or default test (`UpdatePaymentConfigCommandHandler.cs:97-103`).

GET never returns secrets (`GetPaymentConfigQueryHandler.cs:38-51`). Password fields stay empty; hints are last-4.

Leftover modals (not the routed page):

- `apps/lazuar-ops/src/modules/workspace/components/PaymentSettingsModal.tsx` — Xendit in dropdown, no Xendit fields, **Accounting Overrides** (fee % / fixed / tax) that the API dropped, GET still reads `data.gateway_type` as a single object (`36`) while GET returns an **array**. Dead / wrong.
- `apps/lazuar-ops/src/components/PaymentSettingsModal.tsx` — same missing Xendit fields; no environment; no overrides.

Grep shows no import of these modals from pages (only the files themselves). They are leftover surfaces that will confuse the next editor.

RefundModal **does** treat Xendit as an API rail (`RefundModal.tsx:8`). If a Xendit payment exists, ops can request a refund. Configuring the gateway to take that payment still cannot be done from the settings page.

---

## 14. Docs drift that this slice owns

| Claim | Where | Code |
|-------|-------|------|
| Xendit not shipping | `README.md:18, 74` | Adapter registered |
| Four local gateways | README Phase 1; Payments README §6 | Five classes |
| Checkouts stateless | Payments README §3 | `IntegrationCheckoutSessions` + merge |
| No Xendit in tree | `docs/001-gaps/20-architecture-intent-vs-implementation.md:278` | `XenditGatewayAdapter.cs` |
| Dashboard-dynamic Stripe PMs | older `04-stripe.md` / `13-payments-refunds-rails.md` (historical) | Session now forces `card` |
| Xendit operable BYOK | W4-LP-045-done “ops/admin dropdown” | Dropdown without fields |
| Razorpay / Curlec e-mandate | Ops option label | `method=card`, capability false |
| Hop-1 DuitNow / wallets | W4-LP-033-analysis acceptance; 033-done implied disclosure | Flags only |
| Fee estimates on Billplz | Adapter formula + leftover modal | Handler passes 0 |

`docs/payments-integration-quickstart.md:28` still says “Ops → Payment settings: Billplz / Stripe / …” — does not name Xendit. Correct as an operable list.

---

## 15. Per-adapter residual risk (beyond the matrix)

**Stripe.** Session `card` only hides cheap MY APMs on Stripe accounts. Off-session processing status is treated as success; `payment_intent.payment_failed` later is a different EventId (good). Portal picks the first Customer with that email. Fee expand can silently go to 0.

**CHIP.** Off-session is two HTTP calls without an idempotency key. Fail-then-pay EventId collision. `payment.refunded` subscribed and ignored. RSA webhook secret is the **public** key fetched at save; rotating the CHIP key re-registers webhooks.

**Billplz.** Fail-then-pay EventId collision. Callback must be public HTTPS. Metadata lives on the query string + session row. Fee always 0. No refund API.

**Razorpay.** Recurring Commerce generate uses registration links; tokens discarded. Label claims e-mandate. Off-session method is uncalled and cannot see buyer email. Currency fail-closed is good. Event-Id header is the only thing preventing fail-then-pay collision.

**Xendit.** Not savable in ops. Invoice-only. Wallet flags over-claim vs `MalaysiaHostedChannels`. Refund endpoint unsoaked. Callback token is a shared secret, not a body signature — replay of a captured body with the token works (same as many Xendit integrations; worth stating).

**Unknown / Fiuu / Midtrans.** Factory throws; webhook 400. Not in tree.

---

## 16. P0 / P1

### P0

1. **CHIP and Billplz fail-then-pay EventId collision.** `EventId` is the object id. Unique `(Provider, EventId)` plus EventId-first lookup drops `PAYMENT_COMPLETED` after `PAYMENT_FAILED` on the same purchase/bill. Buyer can pay; we never publish completed. Evidence: `ChipCollectGatewayAdapter.cs:177, 208`; `BillplzGatewayAdapter.cs:235`; `ProcessGatewayWebhookCommandHandler.cs:91-102, 119-135`; unique index `PaymentConfigurations.cs:30`; tests that lock EventId = object id on both fail and pay. No test for the sequence.

2. **Xendit is listed as a first-class gateway in the only merchant settings page, and that page cannot store Xendit credentials.** Combined with README “Xendit is not shipping,” a tenant or an implementer will believe one of two false things: that Xendit works from the UI, or that the adapter does not exist. Evidence: `PaymentSettingsPage.tsx:7, 211` vs missing Xendit field block `246-391`; `UpdatePaymentConfigCommandHandler.cs:147-151`; `README.md:18, 74`; adapter exists.

If P0 is reserved for lost money only, keep (1) as P0 and treat (2) as P0-honesty / P1-product. This report keeps both as P0 because (2) is a sellable-rail lie on the settings screen Wave 4 claimed to finish.

### P1

1. **Razorpay “MY e-mandate + cards” label vs `method=card` + `SupportsEmandate=false` + reminder-only engine.** Dropdown and leftover modals. LP-032 correctly not shipped; the label was updated as if it were.

2. **Recurring Razorpay / arrears / renewal still send `SetupFutureUsage: true`**, creating a card registration link whose tokens Commerce then refuses to vault (`InitiateCheckoutCommandHandler.cs:359`; `RenewalCheckoutIssuer.cs:57`; `PublicArrearsEndpoints.cs:156`; `Helpers.cs:79-82`).

3. **Refunds are adapter-success, not webhook-confirmed.** Stripe `pending` applies. No inbound dashboard refund. CHIP `payment.refunded` dead. No adapter refund idempotency except Stripe.

4. **Wave 4 wallet / DuitNow flags are unread.** No hop-1 disclosure. `SupportsHostedWallet` false for Billplz while `SupportsDuitNowQr` is true. Xendit Boost/TnG/DuitNow flags do not match requestable codes.

5. **Billplz `GatewayFee` always 0** because the handler zeros the estimator and accounting overrides were removed. Ledger net = gross for Billplz.

6. **CHIP off-session has no idempotency key.** Retry can double-charge.

7. **Docs:** README, Payments module README, `docs/001-gaps/20` still describe four adapters and no Xendit.

8. **Dead payment settings modals** still compile with accounting overrides and a single-object GET.

9. **Razorpay `ChargeOffSessionAsync` email/phone branch is dead** (notes never contain those keys). Harmless while capability is false; dangerous if someone flips `SupportsOffSession` without fixing metadata plumbing.

10. **Admin platform payment settings** cannot set `environment`; Xendit equally inoperable.

11. **Empty webhook body 500s** (`Endpoints.cs:45-48`) instead of 400 — retry storms on a bad sender.

12. **Product form pay-link copy** names “Billplz/CHIP/Stripe” for every reminder-only gateway (`ProductForm.tsx:173`, `CreateProductForm.tsx:107`).

---

## 17. What “done” means if someone asks to close this slice

Not a plan — a checklist against the code that exists:

- EventId on CHIP/Billplz (and Xendit fail/pay) must not be the bare object id, **or** lookup must be EventId+EventType, **or** COMPLETED must be allowed to supersede FAILED on the same object. Add a test: fail then pay publishes completed.
- Xendit settings fields (API key + callback token) on ops **and** admin, or remove Xendit from the dropdown until then. Align README.
- Razorpay label: “Razorpay / Curlec (cards; reminder-only until token soak)” or similar. Stop saying e-mandate until `SupportsEmandate` is true and a sandbox mandate renews once.
- Recurring generate: pass `SetupFutureUsage` only when `SupportsOffSession` (or a future `SupportsEmandate`). Reminder-only rails mint a plain pay-link.
- Either wire hop-1 DuitNow/wallet one-liners to the flags, or stop marking LP-033–036 as wrap-complete in sales copy. Flags without UI are a matrix, not a product.
- Refund: document “we trust adapter HTTP.” Optionally map Stripe `refund.failed` / CHIP `payment.refunded` before calling the loop closed.
- Delete or quarantine the leftover PaymentSettingsModals.

Until those land, the honest product sentence is:

> Lazuar Pay wraps five BYOK hosted checkouts. Silent renewals work on Stripe and CHIP Collect when a card token exists. Billplz, Razorpay, and Xendit are pay-link / reminder-only. Apple Pay and Google Pay appear only on Stripe hop 2 as cards. We do not do FPX e-mandate. We do not draw DuitNow QR or wallet buttons. Xendit exists as an adapter and is not configurable from the ops form. Refunds are requested from the processor by API (or marked by a human on Billplz/offline); they are not confirmed by refund webhooks. CHIP and Billplz can lose a successful pay if a failure callback for the same bill/purchase was already recorded.

That sentence is the post–Wave 0–4 truth of this slice.
