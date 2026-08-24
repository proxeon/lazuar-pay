# 07 — Xendit + Razorpay as later rails (HTTP judgment, not day one)

**Family:** 014-evals  
**Paper:** 07 — Hub Xendit and Razorpay adapters; why they stay later; how they would port without recreating the factory of five  
**Date:** 24 August 2026  
**Type:** Uncondensed evaluation. **Not an implementation.** **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) cells. **Not** a project reference into `apps/lazuar-api`. **Not** a NuGet add. **Not** a new adapter on 8081.

**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Branch:** `main`  
**HEAD:** `ee2db8e5` — `feat(pay): Bar B receipts, webhook secret, merchant money UI`  
(`ee2db8e5758305089a38298456c456d6bf0e97ca`; reflog after fast-forward merge of `feat/013-bar-b` onto `main`.)

Parent index: [README.md](./README.md). Parent judgment is [00-evaluation.md](./00-evaluation.md) after `01`–`10`. This file is the Xendit/Razorpay evidence. Do not treat the index table as the analysis.

---

## 0. Standing law (this slice)

Binding from [011/11](../011-new-lazuar-pay/11-checklist.md), [011/01](../011-new-lazuar-pay/01-product.md), and [013/06](../013-prods/06-money-rails.md):

1. **Not day one.** `NP-LAT-002`: “More rails (Razorpay, Xendit) as reminder-only, labelled as such” — wave **later**, status **todo**. The old C# tree does not count as `done`.
2. **013 standing law** ([013/06](../013-prods/06-money-rails.md) §0.1 item 3): “One Malaysian rail you will dogfood (CHIP or Billplz), not five adapters day one. Razorpay and Xendit stay `NP-LAT-002`. The factory of five is the Hub lie this slice refuses to copy.”
3. New Pay GatewayEndpoints currently **400 anything except stripe**. WebhookEndpoints 400 `unknown provider` unless `stripe`. That is live, not a plan.
4. **Wrap-rails.** Both rails are reminder-only for auto-debit unless live code proves otherwise. Live `PaymentGatewayCapabilities.SupportsOffSession` is **only** `STRIPE` / `CHIP`. Razorpay and Xendit are **not** off-session. `SupportsEmandate` is false for every name (`NP-XX-011`).
5. **`SupportsApiRefund`:** Razorpay and Xendit **true**. **`SupportsDuitNowQr`:** Xendit true (hosted page; we do not render QR).
6. **Steal HTTP later. Do not add NuGet for both now.** Hub Razorpay is the `Razorpay` 3.3.2 SDK (`Razorpay.Api`). Hub Xendit is already raw `HttpClient`. New host csproj already has Stripe.net for the dogfood rail. Adding `Razorpay` “while we are here” is how the factory of five re-enters through a PackageReference.

008-evals and 013-prods papers are **historical**. Live files on this SHA are authority when they disagree. This paper quotes live files, then names 008 only as residual history — including the two 008 claims this slice was asked to verify: “Xendit UI inoperable” and “Razorpay `SetupFutureUsage` discarded.”

---

## 1. Method / files actually opened

### 1.1 Must-open (read in full)

| Path | Why |
|------|-----|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/XenditGatewayAdapter.cs` | 451 lines. Hosted invoice HTTP. Callback token. Refund lookup. Off-session always false. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs` | 394 lines. Payment links. HMAC. Dead `ChargeOffSessionAsync`. `SetupFutureUsage` discarded. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs` | Off-session / refund / QR / wallet / e-mandate matrix. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx` | Live Xendit/Razorpay fields + amber copy. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/06-sea-fintech-platforms.md` | Product context: Xendit is an acquirer + xenPlatform, not a checkout widget to clone. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/11-checklist.md` | `NP-LAT-002`, `NP-GW-003`, `NP-SOON-008`, refuse `NP-XX-011`. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/008-evals/02-payments-adapters-rails.md` | Historical Razorpay §6 / Xendit §7. Verified against live. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/014-evals/README.md` | This evaluation’s assignment and standing problem. |

### 1.2 Also opened (to verify live vs 008, and to lock new Pay 400)

- `PaymentGatewayFactory.cs`, `IPaymentGatewayAdapter.cs`, `DependencyInjection.cs` (five `AddScoped<IPaymentGatewayAdapter, …>`), `Endpoints.cs` webhook allow-list, `Modules.Payments.Infrastructure.csproj` (`PackageReference Include="Razorpay"`), `apps/lazuar-api/Directory.Packages.props` (`Razorpay` 3.3.2).
- Tests: `XenditGatewayAdapterTests.cs`, `RazorpayGatewayAdapterTests.cs`, `PaymentGatewayCapabilitiesTests.cs`, `DunningEngineJobTests.PastDue_Razorpay_DoesNotPublish`.
- Commerce readers: `RenewalCheckoutIssuer.cs`, `InitiateCheckoutCommandHandler.cs` (still passes `SetupFutureUsage: interval != one_time`), `GatewayPaymentCompletedIntegrationEventHandler.Helpers.cs` (`TryVaultIds` refuses reminder-only), `ExecuteOffSessionChargeIntegrationEventHandler.cs`, `RecordRefundCommandHandler.cs`, `GatewayRefundRequestedIntegrationEventHandler.cs`.
- `GatewayCommon.cs` (email fail-closed, paying-tenant metadata, refund idempotency key, minor-unit policy).
- New host: `apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs`, `WebhookEndpoints.cs`, `StripeHosted.cs`, `Lazuar.Pay.csproj` (Stripe.net only), `tests/Lazuar.Pay.Tests/IsolationTests.cs`.
- Merchant Vite: `apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx` (hard-codes `provider: 'stripe'`).
- Ops twins: `PaymentSettingsModal.tsx` (workspace + leftover root), `apps/lazuar-admin/.../PlatformPaymentSettingsPage.tsx`.
- Issues that closed 008 residuals: `068` (registration link), `066` (EventId fallback).
- [013/06](../013-prods/06-money-rails.md) §2.6 parked rails, §9 anti-goals, G24 “one live adapter”.
- Root `README.md` honesty watermark (live, post-008).
- Hub `Modules/Payments/README.md` adapter list.
- Grep of `apps/lazuar-pay` for `xendit`/`razorpay`: **no matches**. Grep of `apps/lazuar-pay-merchant` for the same: **no matches**.

### 1.3 What this paper is answering

1. What do the Hub Xendit and Razorpay adapters **actually do** on this SHA (HTTP, signatures, objects, refunds, wallets, off-session)?
2. Which 008 honesty problems are **still true**, which **moved**, and which would **re-enter** if copied onto 8081?
3. Why copying them **now** recreates the factory-of-five lie even if the C# compiles and the tests are green.
4. A later-port checklist that steals HTTP judgment **without** weakening day-one Stripe (and the eventual one Malaysian rail).

---

## 2. New Pay on this SHA: the 400 wall

Focused Pay does not know these rails. That is the correct day-one shape.

### 2.1 Paste keys — stripe only

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs`:

```csharp
var provider = body?.Provider?.Trim().ToLowerInvariant();
var secret = body?.Secret?.Trim();
if (provider != StripeHosted.Provider)
{
    return PayErrors.Status(400, "Bad Request", "Bar B first rail is stripe");
}
```

`StripeHosted.Provider` is the string `"stripe"`. A merchant PUT with `razorpay` or `xendit` is **400**, not a queued later-rail. GET only looks up the stripe row. Capability returned is `"hosted_link"` — not off-session, not e-mandate, not wallets.

Merchant Vite (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx`) hard-codes the body:

```ts
body: JSON.stringify({ provider: 'stripe', secret: sk }),
```

The heading is “Stripe keys”. There is no dropdown of five. There is no Curlec label. There is no Xendit amber banner because there is no Xendit field.

### 2.2 Plane B — stripe only

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs`:

```csharp
if (string.Equals(provider, StripeHosted.Provider, StringComparison.OrdinalIgnoreCase) == false)
{
    return PayErrors.Status(400, "Bad Request", "unknown provider");
}
```

Empty body is 400. Invalid Stripe signature is 400. Duplicate `(orgId, stripe, eventId)` is 200 `{ duplicate: true }`. `checkout.session.completed` with `mode=setup` or amount 0 is ignored (`NP-GW-008` spirit). Fulfillment is in-process (`fulfillment.FulfillPaidAsync`). There is no `IMediator`, no outbox, no `GatewayPaymentCompletedIntegrationEvent`.

Grep of `apps/lazuar-pay` for `xendit` / `razorpay` / `Xendit` / `Razorpay`: **no matches**. The strings do not exist in the focused host, its tests, or its Vite clients.

### 2.3 Packages on 8081

`Lazuar.Pay.csproj` PackageReferences: EF Design, Npgsql, **Stripe.net 48.0.0**. No `Razorpay`. No Xendit SDK (Hub never had one). IsolationTests ban `lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api`. Copying `XenditGatewayAdapter.cs` as a class under `Modules.Payments` is physically rejected. Copying the **HTTP** into a new `XenditHosted.cs` next to `StripeHosted.cs` is possible and is exactly how the factory of five would re-enter if done before Stripe (and the one MY rail) are boring.

### 2.4 What 011/013 already wrote (do not weaken)

[011/01](../011-new-lazuar-pay/01-product.md) must-have:

> Honest matrix: Stripe/CHIP can auto-charge if vaulted; Billplz/Xendit/Razorpay-class = **reminder + hosted link**, never silent debit.

Later, not v1:

> More rails (Razorpay, Xendit) as reminder-only, labelled as such.

[011/11](../011-new-lazuar-pay/11-checklist.md):

| ID | Feature | Wave | Status |
|----|---------|------|--------|
| NP-GW-003 | One Malaysian rail you will dogfood (CHIP **or** Billplz) | S1 | todo — **Not five adapters on day one** |
| NP-SOON-008 | Second gateway only after the first two are boring in production | soon | todo |
| NP-LAT-002 | More rails (Razorpay, Xendit) as reminder-only, labelled as such | later | todo |
| NP-XX-011 | Homemade FPX e-mandate | refuse | refuse |

“Second gateway” in `NP-SOON-008` is the S1 pair (Stripe + one MY rail), **not** Razorpay as a third name while CHIP is still a plan. Putting Xendit on 8081 this week would skip both `soon` and `later` and spend the only “second rail” slot on a wrap we will not dogfood.

---

## 3. Hub’s factory of five (the lie to not copy)

### 3.1 Registration

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/DependencyInjection.cs`:

```csharp
services.AddScoped<IPaymentGatewayAdapter, StripeGatewayAdapter>();
services.AddScoped<IPaymentGatewayAdapter, BillplzGatewayAdapter>();
services.AddScoped<IPaymentGatewayAdapter, RazorpayGatewayAdapter>();
services.AddScoped<IPaymentGatewayAdapter, ChipCollectGatewayAdapter>();
services.AddScoped<IPaymentGatewayAdapter, XenditGatewayAdapter>();
services.AddScoped<IPaymentGatewayFactory, PaymentGatewayFactory>();
```

`PaymentGatewayFactory.GetAdapter` uppercases the name and throws `InvalidOperationException` if nothing matches. There is no “parked” bit. If the string is `RAZORPAY`, the Razorpay class runs. That is how a later rail becomes a day-one product: **the factory cannot say no**.

Webhook allow-list (`Endpoints.cs`) is the same five names: `STRIPE`, `BILLPLZ`, `RAZORPAY`, `CHIP`, `XENDIT`. M2M create (`CreateIntegrationCheckoutCommandHandler`) is the same five. Ops/admin dropdowns are the same five. Commerce products can store `RAZORPAY` / `XENDIT` as `GatewayName`. The billing job then has to know **not** to off-session them. That knowledge lives in `PaymentGatewayCapabilities` plus a scatter of Commerce readers. The factory itself is capability-blind.

Hub Payments README on this SHA is more honest than 008 remembered, and still a cathedral:

> Live adapters are **Stripe**, **Billplz**, **CHIP**, **Razorpay**, and **Xendit**.  
> **Not an Accounting Ledger.** **Not a Fulfillment Engine.**

The “not a fulfillment engine” sentence is why Hub needs MediatR + outbox + Commerce handlers. New Pay’s webhook **is** fulfillment. Copying the five-adapter port into 8081 would drag the split back in, because `IPaymentGatewayAdapter` only returns a URL and a parsed event — someone else has to journal.

### 3.2 The port that forces every rail to pretend to be Stripe

`IPaymentGatewayAdapter` (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs`) requires every adapter to implement:

| Method | Xendit live | Razorpay live |
|--------|-------------|---------------|
| `GenerateCheckoutAsync(..., bool setupFutureUsage = false)` | Hosted invoice. `_ = setupFutureUsage`. | Payment link. `_ = setupFutureUsage`. |
| `ParseWebhookAsync` | Callback token + invoice map | HMAC + captured/failed |
| `IssueRefundAsync` | POST `/refunds` (major units) | SDK `Payment.Fetch.Refund` (paise) |
| `GenerateCustomerPortalAsync` | throws `InvalidOperationException` | throws `InvalidOperationException` |
| `ChargeOffSessionAsync` | **always `false`** | **implemented, never called** |

The bool `setupFutureUsage` on generate is the Hub lie in miniature. Stripe uses it. CHIP uses it. Billplz ignores it. Xendit discards it. Razorpay **used to honor it by minting a card-registration mandate** while the capability matrix said reminder-only (008 §6.1, issue 068). Live Razorpay discards it too. The port still asks every rail the Stripe question.

`ChargeOffSessionAsync` is the same shape. Xendit is honest (`return false`). Razorpay still contains a full Order + `CreateRecurringPayment` path that Billing will never call because `SupportsOffSession("RAZORPAY")` is false. Dead pipe is how “finish the adapter” happened without a soak.

New Pay’s `StripeHosted` has **one** method: `CreateHostedUrlAsync`. Parse lives in `WebhookEndpoints`. There is no portal. There is no off-session. There is no `setupFutureUsage` flag. That is the shape to keep. A later Xendit/Razorpay port should add **two functions** (create hosted URL, verify+map webhook), not implement a five-method interface “for symmetry.”

### 3.3 NuGet gravity

Hub Payments.Infrastructure:

```xml
<PackageReference Include="Stripe.net" />
<PackageReference Include="Razorpay" />
<PackageReference Include="Newtonsoft.Json" />
```

`Directory.Packages.props` pins `Razorpay` **3.3.2**. The Razorpay SDK is Newtonsoft-era (`Razorpay.Api.RazorpayClient`, `Utils.verifyWebhookSignature`). Xendit has **no** NuGet — it is `HttpClient` + `JsonContent.Create` + Basic `apiKey:`. CHIP and Billplz are also raw HTTP.

Standing law: steal HTTP later; **do not add NuGet for both now**. If a later Razorpay port happens, the steal is `POST https://api.razorpay.com/v1/payment_links` and HMAC-SHA256 of the raw body — not `RazorpayClient`. Adding the package on day one would also pull Newtonsoft into a host that currently does not need it.

---

## 4. Capability matrix (live, honest on the axes that have readers)

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs`:

```csharp
/// Honest collection-mode matrix. Only Stripe and CHIP Collect can vault and charge off-session.
/// Billplz, Razorpay (not demoable), unknown, and blank names are reminder-only.
/// Refund capability is a separate axis: Razorpay can API-refund; Billplz cannot.
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

public static bool SupportsHostedWallet(string? gatewayName, string? wallet)
{
    var g = Normalize(gatewayName);
    if (g is not ("XENDIT" or "CHIP")) return false;
    var w = Normalize(wallet);
    return w is "GRABPAY" or "SHOPEEPAY" or "TNG" or "TOUCHNGO" or "BOOST" or "DUITNOW";
}

/// <summary>True FPX auto-debit. Off until Curlec/Xendit mandate tokens soak.</summary>
public static bool SupportsEmandate(string? gatewayName)
{
    _ = gatewayName;
    return false;
}
```

Tests (`PaymentGatewayCapabilitiesTests`):

- `SupportsOffSession("RAZORPAY")` false; `IsReminderOnlyGateway` true. `XENDIT` not in the OffSession TestCases list, but `Xendit_IsReminderOnly_AndHostsWallets` locks `SupportsOffSession("XENDIT")` false, `SupportsEmandate` false, `SupportsDuitNowQr` true, `SupportsHostedWallet("XENDIT","GRABPAY")` true, Billplz GrabPay false.
- `SupportsApiRefund` true for Stripe, CHIP, Razorpay, Xendit; false for Billplz / null / blank.
- `RequiresMarkRefunded` false for Razorpay (Xendit not in that TestCase list; code returns false — API refund path).

**Unread as product (do not promote on :5178 / :5179):** `SupportsDuitNowQr` and `SupportsHostedWallet` have **zero generate-path readers** under Payments. Payments README says so. 013/06 says so. The flags describe what the **processor hosted page might show**, not a Lazuar checkbox. Copying them onto new Pay as hop-1 tiles is how “we support GrabPay” becomes a lie when the merchant’s Xendit dashboard never enabled GrabPay and Commerce never set `xendit_payment_methods`.

**Readers that matter (Hub):**

| Reader | What it does with Razorpay/Xendit |
|--------|-----------------------------------|
| `ExecuteOffSessionChargeIntegrationEventHandler` | Short-circuits `!SupportsOffSession` → `off_session_not_supported`. Razorpay `ChargeOffSessionAsync` is never reached. Xendit would return false anyway. |
| `PastDueDunningProcessor` / `DunningEngineJobTests.PastDue_Razorpay_DoesNotPublish` | Vaulted Razorpay + AUTO_CHARGE campaign does **not** publish `ExecuteOffSessionCharge`. Writes a reminder log instead. |
| `RenewalCheckoutIssuer` | `SetupFutureUsage: SupportsOffSession(product.GatewayName)` — **false** for these rails (post-068 residual fix). |
| `PublicArrearsEndpoints` | Same gate. |
| `GatewayPaymentCompletedIntegrationEventHandler.Helpers.TryVaultIds` | `IsReminderOnlyGateway` → discard `customer_id` / `token_id` even if the webhook carried them. |
| `RecordRefundCommandHandler` | API refund path (not mark-refunded). |
| `InitiateCheckoutCommandHandler` | **Still** passes `SetupFutureUsage: resolved.Interval != "one_time"` for **every** gateway, including Razorpay/Xendit. Harmless only because both adapters discard the flag. Residual Hub lie. |

Comment on the matrix: “Razorpay **(not demoable)**.” That is standing product language. Xendit is demoable only with a real Xendit account, a dashboard callback token, and a public HTTPS URL. Neither is a Bar B dogfood rail.

---

## 5. Xendit adapter — what it actually does

File: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/XenditGatewayAdapter.cs` (451 lines).

Class comment (live, accurate):

```csharp
/// <summary>
/// BYOK wrap of Xendit hosted invoices. Money settles on the tenant Xendit account.
/// Reminder-only until a payment-token soak proves off-session. We do not rebuild wallets.
/// </summary>
```

This is **not** xenPlatform, **not** Xendit Subscriptions, **not** Payment Sessions (`POST /sessions`), **not** Payment Tokens, **not** e-mandate, **not** a DuitNow member connection. It is a wrap of the **legacy Invoice API** (`POST /v2/invoices`). [007/06](../007-feats/06-sea-fintech-platforms.md) already warned: Xendit’s own docs (updated Jul 2026) tell merchants to migrate legacy Payment Links / Invoices to Payment Sessions. A later port that copies `/v2/invoices` as if it were the 2027 SKU will inherit a sunset. Steal the **job** (hosted payable page + callback), re-read Xendit’s current docs at port time.

### 5.1 HTTP extract — create hosted invoice

| Item | Live |
|------|------|
| Method / URL | `POST https://api.xendit.co/v2/invoices` (`LiveApiBase = "https://api.xendit.co"`). **No test host.** Tenant `environment=test` does not change the URL. Test vs live is the **key prefix** (`xnd_development_…` vs `xnd_production_…`) on Xendit’s side. |
| Auth | HTTP Basic, username = secret API key, **empty password**: `Convert.ToBase64String(Encoding.UTF8.GetBytes(apiKey.Trim() + ":"))`. |
| Client | `IHttpClientFactory.CreateClient()` — unnamed client, no DNS fallback (Billplz-only). |
| Success | JSON `invoice_url` + `id`. Missing `invoice_url` → `GatewayCheckoutResult` false. Non-2xx → false with body in error. |
| Returns to Hub | `CheckoutUrl = invoice_url`, `SessionId = invoice id`. |

Payload (`BuildInvoicePayload`):

```csharp
["external_id"] = "lazuar_" + Guid.CreateVersion7().ToString("N"),
["amount"] = line,  // GatewayCommon.ToMinorUnitsRounded(amount, quantity) / 100m  → major units
["currency"] = ISO-4217 or throw "Currency is required.",
["description"] = GatewayCommon.ProductDescription(productName, quantity),
["payer_email"] = TryResolveEmail or throw "Customer email is required.",
["success_redirect_url"] = successUrl,
["failure_redirect_url"] = cancelUrl,
["metadata"] = metadata,  // ApplyPayingTenantMetadata keeps paying tenant_id, stamps platform_tenant_id
```

Optional `payment_methods` only if metadata key `xendit_payment_methods` is set. **Nothing in Commerce/portal sets that key.** Production invoices use **merchant dashboard defaults**. The filter is an unused hook (Payments README says so; test `BuildInvoicePayload_FiltersUnknownChannels` locks the filter, not a caller).

`setupFutureUsage` and `merchantId` are discarded (`_ =`). Hosted invoice does not vault. There is no Brand ID / Collection ID. The Xendit account is fully identified by the secret key.

**Email honesty (live, post-227 spirit):** `GenerateCheckoutAsync` calls `GatewayCommon.TryResolveEmail` **before** HTTP. Blank / `customer@example.com` → `Success=false`, `"Customer email is required."` `BuildInvoicePayload` throws the same if called directly. 008 / issue 227 described CHIP/Billplz/Xendit substituting `customer@example.com`. **Live Xendit generate fails closed.** Steal that. Do not re-import the placeholder.

**Paying-tenant metadata (issue 062 closed):** `GatewayCommon.ApplyPayingTenantMetadata`. System-org generate keeps existing `tenant_id` and stamps `platform_tenant_id`. Test `BuildInvoicePayload_KeepsPayingTenant_AndStampsPlatformTenant`. New Pay does not have Hub’s `SystemOrganizationId` credits (`NP` refuse in 013/06). Do not copy platform checkout. Do copy “never clobber the org that must be activated” if a later multi-tenant Xendit webhook URL is shared.

**Amount policy:** Xendit wants **major units** (10.50 not 1050). Hub converts via `ToMinorUnitsRounded / 100m`. Rounding is banker's-away-from-zero through `GatewayCommon.ToMinorUnits` (`MidpointRounding.AwayFromZero`). Quantity multiplies inside minor units then divides back. A later port must not send Stripe-style cents to `/v2/invoices`.

**external_id:** `lazuar_` + UUID v7 hex, **new every generate**. It is not the Pay checkout id. Metadata should carry `checkout_id` / `org_id` (new Pay Stripe already does). A later port should stamp Pay’s checkout id in metadata **and** consider using it as `external_id` so dashboard search matches Pay. Hub’s random external_id is a lost join.

### 5.2 HTTP extract — webhook

Xendit does **not** HMAC the body. The dashboard issues a **callback token**. Every invoice webhook includes header `x-callback-token`. Verification is “does this header match the stored secret?”

Live `VerifyCallbackToken`:

```csharp
internal const string CallbackTokenHeader = "x-callback-token";

// Hash first so a length mismatch is still constant-time. Xendit does not
// send a body HMAC — this is a shared callback token, not a signature.
var expected = SHA256.HashData(Encoding.UTF8.GetBytes(webhookSecret));
var actual = SHA256.HashData(Encoding.UTF8.GetBytes(presented));
return CryptographicOperations.FixedTimeEquals(expected, actual);
```

Empty stored secret → false. Missing header → false. Test `VerifyCallbackToken_LengthMismatch_IsNotVerified` plus `ParseWebhook_MissingToken_IsNotVerified`.

**Honesty about this scheme (keep in the later-port notes):** a captured body replayed with the stolen token **succeeds**. There is no timestamp, no body signature, no Stripe-style `t=` window. That is Xendit’s documented invoice-callback model, not a Hub bug. Mitigations that belong at port time, not Hub copy: HTTPS only, rotate the token, idempotency on `(org, xendit, event_id)`, reject if checkout already `paid` for a second `PAID`. Do not invent a body HMAC Xendit does not send.

`apiKey` is unused at parse (`_ = apiKey`). Fee estimate args are unused (handler always passes 0,0,0).

**Map (`MapInvoiceCallback`):**

- Unwraps `data` if present (some Xendit callback envelopes).
- Status from invoice `status` or root `event`.
- `PAID` / `SETTLED` / `invoice.paid` → `PAYMENT_COMPLETED`.
- `EXPIRED` / `FAILED` / `invoice.expired` / `invoice.failed` → `PAYMENT_FAILED`.
- Everything else (including `PENDING`) → verified passthrough, empty EventId, **no fulfill**. Issue 223 residual: Xendit `PENDING` is ignored. Do **not** map PENDING to paid.
- Missing invoice `id` or `currency` → `Verified=false` + `AsUnusable()` (400 so Xendit stops). **No invented MYR.** Test `ParseWebhook_PaidWithoutCurrency_DoesNotInventMyr`.

**EventId (live, improved vs 008):** `$"{mapped}:{invoiceId}"` e.g. `PAYMENT_COMPLETED:inv_paid_1`. 008 said `EventId = invoiceId` so EXPIRED then PAID on the same invoice would collide. Live namespacing means `PAYMENT_FAILED:inv` and `PAYMENT_COMPLETED:inv` are **different** EventIds. `PAID` then `SETTLED` still share EventId **and** mapped type — intended idempotency. Typical Xendit invoices do not resurrect after EXPIRED.

Amount: prefer `paid_amount`, else `amount` (major units, not /100). Fee: `fees_paid_amount` if numeric, else 0. Tax always 0. Net = amount − fee. Currency uppercased. Metadata copied from invoice `metadata` plus `external_id`.

**GatewayTransactionId is the invoice id**, not a `py-` payment id. That matters for refunds (§5.4).

**Not mapped:** refund callbacks, payment-token events, xenPlatform events, Payment Session lifecycle, disputes. Hub `ProcessGatewayWebhookCommandHandler` only persists `PAYMENT_COMPLETED` / `PAYMENT_FAILED` / `DISPUTE_*` / `REFUND_COMPLETED`. Adapter passthrough of `PENDING` is a verified ACK with no log. New Pay should keep “unknown event = 200, no fulfill,” not invent a journal line.

**Dashboard configuration:** Xendit invoice callbacks are **not** auto-registered by this adapter (CHIP has `ChipWebhookRegistrar`; Xendit does not). Merchant pastes the callback token Hub stores as `WebhookSecret`, and must point Xendit’s dashboard at `POST /webhooks/payments/xendit/{tenantId}` (Hub) or later `POST /v1/webhooks/xendit/{orgId}` (Pay). No registrar means no localhost→fiction-DNS rewrite, which is good. It also means a later port cannot “save key and it just works” the way CHIP pretends to.

### 5.3 Off-session

```csharp
_ = (apiKey, customerId, tokenId, amount, currency, description, receipt, tenantId, ...);
// Honest: hosted invoices do not vault. Stay reminder-only until payment tokens soak.
return Task.FromResult(false);
```

Test `ChargeOffSession_AlwaysFalse_UntilTokenSoak`. Capability agrees. Xendit **the company** sells Subscriptions + Payment Tokens + MIT on wallets/DD. **This adapter does not.** A later port that flips `SupportsOffSession("xendit")` without a payment-token soak is the 068 Razorpay story with a different logo. Keep false until a named merchant’s Xendit token renews once in sandbox **and** the webhook is `payment` not `invoice`.

### 5.4 Refunds

`SupportsApiRefund("XENDIT")` true. `RequiresMarkRefunded` false.

Live path (improved vs 008’s “POST `/refunds` with `invoice_id` only”):

1. `GET https://api.xendit.co/v2/invoices/{transactionId}` with Basic auth.
2. `TryReadPaymentId` prefers `payment_id` / `credit_card_charge_id`, else first `payments[].id` / `payment_id`. Unwraps `data`. JSON errors → null.
3. `POST https://api.xendit.co/refunds` with header `Idempotency-key: lazuar-refund:{transactionId}:{minorUnits}` (`GatewayCommon.FormatRefundIdempotencyKey`).
4. Body: `amount` in **major units**, `reason=REQUESTED_BY_CUSTOMER`, plus `payment_id` if resolved else `invoice_id`.

Tests lock `TryReadPaymentId` and `BuildRefundPayload` (payment_id XOR invoice_id). HTTP success → `true`. 4xx → false → Hub `REFUND_FAILED`. No inbound refund webhook map. Dashboard refunds will not reverse the journal unless someone later maps them.

008 residual still true: this refund path is **unsoaked**. Xendit’s refund API historically wants a payment id. The GET lookup is a mitigation, not a soak. A later port must sandbox-refund once with the same id Pay stored as `provider_session_id`. If Pay stores the invoice id (it will — that is `GatewayTransactionId`), the lookup is mandatory. Do not send Stripe PaymentIntent ids to Xendit.

Partial refunds: amount is the Commerce-requested major-unit amount. No currency on the refund body (Xendit infers from the payment). No refund id returned to Hub (`IssueRefundAsync` is `bool`). New Pay paper 07 will want a refund id eventually; do not pretend Hub has one.

### 5.5 Portal

Throws. Keep throwing. There is no Xendit Billing Portal analogue we should wrap in v1 or in `NP-LAT-002`. Buyer magic-link portal is `NP-BUY-004` (V1), processor-agnostic.

### 5.6 Wallets / DuitNow / channels

Allow-list `MalaysiaHostedChannels`:

```
CREDIT_CARD, DD_FPX, QR_CODE, OVO, DANA, LINKAJA, SHOPEEPAY, GCASH, GRABPAY, PAYMAYA
```

Unknown codes dropped. Empty list = dashboard defaults (honest wrap).

**Mismatch with `SupportsHostedWallet`:** the capability says TNG / TOUCHNGO / BOOST / DUITNOW are hosted wallets on Xendit. **None of those strings are in `MalaysiaHostedChannels`.** DuitNow QR is requested as `QR_CODE`, not `DUITNOW`. Boost cannot be requested. TnG cannot be requested. 008 §7.1 already named this. **Live still true.** A later port must not show hop-1 “Touch ’n Go” because a flag is true.

GrabPay / ShopeePay **are** in the allow-list. They still only appear if (a) metadata sets `xendit_payment_methods` or (b) the merchant enabled them on the Xendit invoice / payment-link settings. We do not render wallet buttons. We do not render QR. `SupportsDuitNowQr("XENDIT")` means “the hosted page may show a QR,” not “Pay draws DuitNow.”

Regional honesty from [007/06](../007-feats/06-sea-fintech-platforms.md): Xendit is licensed in MY via **Payex PLT**, and is also ID/PH/TH/VN/SG/HK/MX. The Hub allow-list mixes MY (`CREDIT_CARD`, `DD_FPX`, `QR_CODE`, `GRABPAY`) with ID/PH wallets (`OVO`, `DANA`, `LINKAJA`, `GCASH`, `PAYMAYA`). Requesting `OVO` on a MY-only Xendit account is a 4xx or a silently ignored method — depending on Xendit’s mood that week. A later MY-dogfood port should default to **dashboard methods**, not a SEA souvenir list.

`DD_FPX` on a hosted invoice is **customer-present** FPX, not an e-mandate. `SupportsEmandate` is false. Ops amber copy is correct: “No silent auto-charge, no FPX e-mandate.”

### 5.7 UI honesty — 008 said inoperable; live has fields

008 §7.5: **“Is Xendit operable from the ops UI? No.”** Dropdown without a credential block. `handleSubmit` had no `XENDIT` validation. First-time PUT sent empty `api_key`; backend refused. W4-LP-045-done claimed “dropdown include `XENDIT`” and called that done.

**Live `PaymentSettingsPage.tsx` (opened in full) has moved.** Evidence:

- Type includes `"XENDIT"`.
- `handleSubmit`: `if (gatewayType === "XENDIT" && !hasApiKey && !apiKey.trim())` toast “API Key is required for first-time Xendit configuration.”
- Dropdown: `Xendit (SEA hosted invoice + wallets)`.
- Credential block with **amber banner**:

> **Hosted invoice only.** Xendit is reminder-only. We create a hosted invoice and email the link. No silent auto-charge, no FPX e-mandate.

- Fields: Secret API Key (`xnd_development_…` / `xnd_production_…`); Callback token (`x-callback-token`); helper “Must match the x-callback-token Xendit sends on invoice webhooks.”
- Same amber + fields in `PaymentSettingsModal.tsx` (workspace + leftover root) and admin `PlatformPaymentSettingsPage.tsx`.

So 008’s “dropdown without a form” is **closed on live ops**. What is **not** closed:

- New Pay `:5178` has **no** Xendit fields (correct; later).
- The dropdown still lists Xendit as a first-class Hub gateway next to CHIP. That is the factory-of-five product lie: the form works, therefore the rail is a peer of Stripe. It is not dogfood. It is not soaked. It is reminder-only. Hub still lets a merchant pick it on a subscription product.
- README honesty watermark **did** catch up (008 §7.6 / 008/10 said README denied the adapter). Live root README: “BYOK Stripe / Billplz / CHIP / Razorpay / **Xendit**” and “Xendit is a hosted-invoice wrap (reminder-only).” Docs under `docs/001-gaps/20` and some `lazuar-docs` pages still omit Xendit or list “Billplz / Stripe / CHIP / Razorpay” only. Historical. Not a reason to put Xendit on 8081.
- “SEA hosted invoice + wallets” over-claims vs unused `xendit_payment_methods` and the TNG/Boost mismatch. Steal the **amber paragraph**, not the option label.

### 5.8 Demoability (Xendit)

To demo a real Xendit hop-2 on Hub today you need: a Xendit account (Payex KYC for MY), a secret key, a callback token, a **public** HTTPS webhook URL, an invoice payment method enabled on the account, and a buyer who will pay on `invoice_url`. There is no Hub fixture. There is no recorded sandbox soak in this tree. CHIP/Stripe are the dogfood names in 011’s sentence. Xendit is a **complement** (BYOK adapter on top of an acquirer), not a displacement of HitPay’s WhatsApp link ([007/06](../007-feats/06-sea-fintech-platforms.md) verdict: complement first, rival second; do not rebuild xenPlatform; do not compete on time-to-first-WhatsApp-link).

Demo line if someone insists: “We email a Xendit hosted invoice. The buyer pays GrabPay/FPX/card **on Xendit’s page** if you enabled it there. We do not silent-debit. We do not draw QR.”

---

## 6. Razorpay adapter — what it actually does

File: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs` (394 lines).

`GatewayType => "RAZORPAY"`. SDK: `Razorpay.Api` (`RazorpayClient`, `PaymentLink.Create`, `Utils.verifyWebhookSignature`, `Order.Create`, `Payment.CreateRecurringPayment`, `Payment.Fetch.Refund`).

### 6.1 Keys

```csharp
var parts = apiKey.Split(':');
var keyId = parts[0];
var keySecret = parts.Length > 1 ? parts[1] : "";
return new RazorpayClient(keyId, keySecret);
```

Ops copy: “Format must be KeyId:KeySecret”, placeholder `rzp_live_xxx:secret_yyy`. That concatenation is a Hub vault accident (one `ApiKey` column). A later port should store `key_id` and `key_secret` as two secrets, or at least not invent a third `secret_key` column like Stripe. Do not send the concatenated string to Razorpay as a Bearer token.

No host switch on `environment`. Razorpay test vs live is the **key id prefix** (`rzp_test_` / `rzp_live_`). Same pattern as Stripe. `KEY_MODE_MISMATCH` in Hub only compares Stripe-shaped `sk_test_` / `sk_live_`. Razorpay secrets are not gated (quickstart docs say so). A later port may add `rzp_test_` vs org environment, or may not — but must not use Hub hostname to pick a Razorpay host.

### 6.2 HTTP extract — create payment link (not invoice, not registration)

Live generate:

```csharp
// Reminder-only: we do not claim e-mandate. SetupFutureUsage still mints a
// payment link, not a card-registration mandate (max_amount = 10× first charge).
_ = setupFutureUsage;
var client = GetClient(apiKey);
var req = BuildPaymentLinkRequest(...);
var link = client.PaymentLink.Create(req);
return Task.FromResult(new GatewayCheckoutResult(true, link["short_url"].ToString(), link["id"].ToString(), null));
```

SDK `PaymentLink.Create` is HTTP `POST https://api.razorpay.com/v1/payment_links` with Basic `keyId:keySecret`. Steal that URL later; do not steal the SDK.

`BuildPaymentLinkRequest` (tested `BuildPaymentLinkRequest_NeverMintsCardRegistration`):

```csharp
["amount"] = GatewayCommon.ToMinorUnitsTruncating(amount, quantity), // paise / sen
["currency"] = currency.ToUpperInvariant(),
["description"] = GatewayCommon.ProductDescription(productName, quantity),
["customer"] = { name, email, contact? },
["notes"] = metadata as object dictionary,
["callback_url"] = successUrl,
["callback_method"] = "get"
```

**Not present:** `subscription_registration`, `type`, `method=card`, `max_amount`, `expire_at`. Test asserts those keys are absent and that `contact` is omitted when `customer_phone` is missing.

**008 vs live — SetupFutureUsage (the assigned honesty problem):**

008 §6.1 described **two** generate paths:

- `setupFutureUsage == true` → `Invoice.CreateRegistrationLink` with `subscription_registration.method = "card"`, `max_amount = amountPaise * 10`, expire +10 years.
- else → `PaymentLink.Create`.

Commerce sent `SetupFutureUsage: true` for every recurring interval. Tokens came back on the webhook (`customer_id` / `token_id`). `TryVaultIds` discarded them because `IsReminderOnlyGateway("RAZORPAY")`. Buyer authorized a card mandate. Hub threw it away. Issue **068 / B04-P11** (P1) closed on `fix/068-razorpay-no-card-registration`.

**Live: the registration path is gone from the adapter.** Grep of `apps/lazuar-api` for `CreateRegistrationLink` / `subscription_registration` as code: **only the test asserting the key is absent.** The comment still mentions `max_amount = 10×` as the thing we **do not** do.

**Residual:** `InitiateCheckoutCommandHandler` still passes `SetupFutureUsage: resolved.Interval != "one_time"` (line 430) for Razorpay products. Harmless **because** the adapter discards. `RenewalCheckoutIssuer` and arrears **do** gate on `SupportsOffSession`. A later Pay port must not take a `setup_future_usage` bool on a reminder rail at all. New Pay `StripeHosted.CreateHostedUrlAsync` has no such flag. Keep it that way for Razorpay.

`callback_url` is the **browser success URL**, not the webhook. Razorpay webhooks are configured in the Razorpay dashboard per merchant (or account-level). Hub does not auto-register them. Same operational hole as Xendit, different header.

**Phone honesty:** 008 / issue 227 said Razorpay always sent `contact: +60100000000`. **Live generate omits `contact` unless `customer_phone` is in metadata.** Test locks that. `ChargeOffSessionAsync` still has a dead email/phone copy from notes it never populated (§6.4). Do not re-import dummy MY mobiles.

**Email:** Razorpay generate does **not** call `TryResolveEmail`. Blank email is sent blank. Xendit fails closed. Stripe omits. A later port should fail closed like Xendit/GatewayCommon, not invent `customer@example.com`, not send empty.

**Amount:** `ToMinorUnitsTruncating` — comment in 008 said CHIP/Xendit round, Billplz/Razorpay truncate. Live `GatewayCommon.ToMinorUnits` is **one** policy (`AwayFromZero`); `ToMinorUnitsTruncating` is now an alias of `ToMinorUnits`. The name is a fossil. Steal “Razorpay amount is integer paise,” not “truncate vs round” folklore.

**Currency:** uppercased. Razorpay accounts are often INR. A MYR payment link on an INR-only account 4xxs. There is no Hub guard. Demoability (“not demoable”) is partly this: Curlec/Razorpay MY is a different onboarding than the INR keys a developer already has.

### 6.3 HTTP extract — webhook

Signature:

```csharp
Utils.verifyWebhookSignature(rawBody, signature, webhookSecret);
```

Missing `X-Razorpay-Signature` (case-insensitive) → not verified, error `"Missing X-Razorpay-Signature header."` Test `ParseWebhook_MissingSignature_IsNotVerified`.

The SDK helper is HMAC-SHA256 of the **raw body** with the webhook secret, hex digest, compared to the header. Steal as 15 lines of `HMACSHA256` + `CryptographicOperations.FixedTimeEquals` on lowercase hex. Tests already implement `Sign()` that way. Do not need `Razorpay.Api` to verify.

JSON: `event` at root. Mapped:

| Processor event | Live map | 008 map | Notes |
|-----------------|----------|---------|-------|
| `payment.captured` | `PAYMENT_COMPLETED` | same | Amount `/100`, fee/tax from payload `/100`, notes → metadata, `customer_id` / `token_id` captured then discarded by Commerce |
| `payment.failed` | `PAYMENT_FAILED` | same | `IsPaymentFailedEvent` is **only** this string |
| `invoice.expired` | **passthrough** (verified, empty EventId, not `PAYMENT_FAILED`) | 008 said mapped to `PAYMENT_FAILED` | Test `ParseWebhook_InvoiceExpired_IsIgnoredNotPaymentFailed` (issue 069). Payment links are not invoices; expire is not a failed pay of `pay_`. |
| `payment.authorized` | passthrough | passthrough | Uncaptured. Do not fulfill. Issue 223 residual. |
| `refund.*` | passthrough | passthrough | No refund mapper. API refunds do not get a `REFUND_COMPLETED` inbound event. |

Missing payment id and missing `X-Razorpay-Event-Id` → `Verified=false` `AsUnusable()`, never `Guid.NewGuid()`. Test `ParseWebhook_CapturedWithoutHeaderAndPaymentId_IsNotVerified`.

**EventId (live, improved vs 008):**

```csharp
internal static string? ResolveEventId(..., string mappedEventType, string? paymentId)
{
    // prefer X-Razorpay-Event-Id
    // else mappedEventType + ":" + paymentId
    // else null
}
```

008: fallback was the **bare payment id**. Fail then capture of the same `pay_` collided; completed dropped (issue **066 / B04-P09**). Live fallback is `PAYMENT_FAILED:pay_same` vs `PAYMENT_COMPLETED:pay_same`. Test `ParseWebhook_FailThenCapture_WithoutHeader_UseDistinctEventIds`. Prefer the Razorpay delivery id when present (`evt_rzp_1`). Steal **both**: header first; namespaced fallback; never Guid.

Currency missing → fail closed, no MYR. Test `ParseWebhook_CapturedWithoutCurrency_DoesNotInventMyr`. Currency is uppercased (`TryReadCurrency`). Hub then publishes the string as-is (issue 072 residual: case split across rails). New Pay should normalize once at fulfill.

Fee/tax: from payment entity, `/100`. Net = amount − fee. If fee absent, 0 — **does not** stamp `gateway_fee_status=unknown` (CHIP does). `NP-MON-002`: unknown ≠ 0. A later port should stamp unknown when Razorpay omitted `fee`, not journal MDR 0.

### 6.4 Off-session (dead pipe)

`ChargeOffSessionAsync` still:

1. `Order.Create` with amount paise, currency upper, `payment_capture=true`, notes (`type=commerce_subscription`, `subscription_id`, `tenant_id` = **adapter tenant**, receipt, optional dunning/attempt).
2. `Payment.CreateRecurringPayment` with `customer_id`, `token`, `recurring=true`.
3. Success if `razorpay_payment_id` present.
4. `_ = idempotencyKey` — comment: “Razorpay recurring create has no idempotency key (best-effort).”
5. Email/phone copied from **the notes dictionary this method just built**. Those notes never contain `customer_email` / `customer_phone`. 008 §6.3: dummy `billing@lazuar.com` was removed; the replacement branch is dead. Live still dead.

**The engine never calls this.** `SupportsOffSession("RAZORPAY")` is false. `ExecuteOffSessionChargeIntegrationEventHandler` publishes `off_session_not_supported` first. `PastDue_Razorpay_DoesNotPublish` locks reminder-not-charge even if a junk token is stored.

**Do not port `ChargeOffSessionAsync`.** Do not flip the capability because the method exists. Do not “soak tokens” by re-enabling registration links. Curlec e-mandate is `NP-XX-011`. If a named merchant later has Razorpay tokens that actually renew, that is a new paper with a sandbox log, not a copy of this method.

Off-session Razorpay **does** set `tenant_id` to the adapter tenant (issue 062 note). Dead while capability is false; poisonous if someone flips the flag for platform charges.

### 6.5 Refunds

`SupportsApiRefund("RAZORPAY")` true.

```csharp
var refundReq = new Dictionary<string, object>
{
    ["amount"] = GatewayCommon.ToMinorUnitsTruncating(amount),
    ["notes"] = new Dictionary<string, object>
    {
        ["idempotency_key"] = GatewayCommon.FormatRefundIdempotencyKey(transactionId, amount)
    }
};
var refund = client.Payment.Fetch(transactionId).Refund(amount > 0 ? refundReq : null);
return Task.FromResult(refund != null);
```

HTTP under the SDK: `POST https://api.razorpay.com/v1/payments/{pay_id}/refund`. `transactionId` **must** be a Razorpay **payment** id (`pay_…`). Webhook `GatewayTransactionId` is that id for `payment.captured`. Good.

Idempotency is a **note**, not Razorpay’s idempotency header. Worker retry after a lost 200 can double-refund at the processor. 008 named this for CHIP/Razorpay/Xendit. Stripe is the only rail with a real processor idempotency key on refunds (and off-session). A later port should send Razorpay’s `X-Razorpay-Idempotency-Key` header if the API still supports it — verify at port time — and not trust a note the merchant can see.

`amount > 0 ? refundReq : null` — Hub refund handler already rejects `Amount <= 0`, so the null full-refund branch is dead. Steal “always send amount in paise.”

No refund webhook map. Ops can click refund; inbound `refund.processed` is a 200 no-op. Paper 07 reverse-once must not wait for a Razorpay refund event that Hub drops.

### 6.6 Portal

Throws. Same as Xendit.

### 6.7 UI honesty — label moved; e-mandate did not ship

008 §6.1 quoted:

```
<option value="RAZORPAY">Razorpay / Curlec (MY e-mandate + cards)</option>
```

**Live ops/admin (opened):**

```
<option value="RAZORPAY">Razorpay / Curlec (cards; reminder-only until token soak)</option>
```

Fields: API Key (`KeyId:KeySecret`); Webhook Signing Secret. **No amber banner** (Xendit has one; Billplz has one; Razorpay does not). The option text is the honesty. A later `:5178` port should use a Billplz-class amber paragraph, not rely on a dropdown subtitle:

> **Pay-link renewals.** Razorpay is reminder-only. We create a payment link and email it. There is no silent auto-charge and no Curlec FPX e-mandate. Use Stripe or CHIP when you need recurring auto-debit.

`SupportsEmandate` is still false. `method=emandate` does not exist in the adapter. Do not re-open Curlec because the option still says “Curlec.”

### 6.8 Demoability (Razorpay)

Capabilities comment: “Razorpay **(not demoable)**.” Reasons that are still true on this SHA:

- No recorded sandbox soak in this tree.
- Key format is easy to get wrong (`KeyId:KeySecret`).
- INR vs MYR account mismatch.
- Webhooks must be configured in Razorpay dashboard; no registrar.
- Recurring looks like it works in the class (`CreateRecurringPayment`) and does not work in the product.
- India-primary brand; Curlec is the MY story we **refused** to finish (`NP-XX-011`, LP-032 done as “do not claim auto-debit”).
- New Pay 400s the name.

A Hub demo of Razorpay is: paste test keys, create a one-off, buyer pays a **payment link**, webhook `payment.captured` fulfills. A Hub demo of Razorpay **subscriptions** is an emailed link each cycle. Anyone who says “Razorpay auto-debit” is reading the dead method or the old e-mandate label.

---

## 7. Invoice vs payment-link models (do not collapse)

These are **different processor objects**. A later port that wraps both behind `IPaymentGatewayAdapter.GenerateCheckoutAsync` is how Hub lost the distinction.

| Axis | Xendit (Hub) | Razorpay (Hub) | Stripe (new Pay) | CHIP / Billplz (later MY) |
|------|--------------|----------------|------------------|---------------------------|
| Processor object | **Invoice** (`/v2/invoices`) — payable hosted page, xenInvoice lineage | **Payment Link** (`/v1/payment_links`) | Checkout Session `mode=payment` | CHIP purchase / Billplz bill |
| Not | LHDN tax invoice; Xendit Subscription; Payment Session | Razorpay Invoice; Subscription; Registration link; e-mandate | Stripe Billing Subscription | CHIP Send / Billplz Agreements |
| Buyer UX | `invoice_url` | `short_url` | `session.Url` | CHIP/Billplz hosted |
| Redirect | `success_redirect_url` / `failure_redirect_url` | `callback_url` GET (success only) | success/cancel on session | CHIP redirect / Billplz |
| Fulfillment event | Invoice `PAID`/`SETTLED` callback | `payment.captured` | `checkout.session.completed` | `purchase.paid` / bill `paid` |
| Provider session id stored | Invoice id | Payment **link** id on generate; webhook transaction id is **payment** id (`pay_`) | Session id | purchase / bill id |
| Amount units | **Major** (10.50) | **Minor** (1050) | **Minor** | CHIP minor / Billplz major depending |
| Auth | Basic `key:` | Basic `keyId:keySecret` | Bearer `sk_` | CHIP Bearer / Billplz Basic |
| Recurring | Email a new invoice | Email a new payment link | Off-session PI if PM exists | CHIP token / Billplz email link |
| Wallets | Hosted page if enabled; optional allow-list unused | Hosted page (account methods) | Cards (+ Apple/Google on card) | CHIP brand / Billplz collection |

**Generate id vs webhook id (Razorpay trap):** generate returns `plink_…`. Webhook `payment.captured` carries `pay_…`. Hub `GatewayTransactionId` for refunds is `pay_…`. If new Pay stores only `provider_session_id = plink_…` from create, a later refund will `Fetch("plink_…")` and fail. Steal: persist **both** link id (open session) and payment id (paid / refund). Stripe already uses session id for both create and fulfill; do not assume every rail is Stripe-shaped.

**Xendit generate id vs refund id:** generate/webhook use invoice id. Refund prefers `py-` payment id via GET. Persist invoice id; look up payment id at refund time (live does this). Do not assume they are the same string.

**Tax documents:** neither object is a MyInvois tax invoice. Pay’s `RCPT-` is still a payment receipt (`NP-DOC-003`). Xendit’s word “invoice” is a payable request. Do not title the receipt “Xendit Invoice.” [007/06](../007-feats/06-sea-fintech-platforms.md) table: Payment Link / Invoice API is checkout, not statutory XML.

**Xendit product migration:** 007/06 — English IA says Payment Links; legacy API is `/v2/invoices`; docs tell merchants to migrate to Payment Sessions (`POST /sessions`) with different webhook names. A 2026-later port should **re-read docs**, not fossilize Hub’s `/v2/invoices` as sacred. The job to steal is “hosted payable page + callback token (or the successor’s signature).”

---

## 8. Webhook signatures (side by side)

| | Xendit | Razorpay | Stripe (already on 8081) |
|--|--------|----------|--------------------------|
| Header | `x-callback-token` | `X-Razorpay-Signature` | `Stripe-Signature` |
| What is signed | **Nothing.** Shared token compare. | HMAC-SHA256(**raw body**, webhook secret) hex | Stripe scheme (`t=` + v1 HMAC) |
| Extra id header | none | `X-Razorpay-Event-Id` (prefer) | event `id` in body (`evt_`) |
| Empty secret | fail closed | SDK verify throws → `Verified=false` | 503 if `Pay:StripeWebhookSecret` missing (new Pay) |
| Timing-safe | SHA256 both sides then `FixedTimeEquals` | SDK (steal: hex compare fixed-time) | Stripe.net |
| Replay | captured body + token works | captured body + signature works (no timestamp) | limited by Stripe tolerance |
| Auto-register | **No** | **No** | **No** on new Pay (secret is env `Pay:StripeWebhookSecret` today, not per-org — a Bar B honesty gap for **Stripe**, out of this slice except as a warning: do not add a second global `Pay:XenditCallbackToken`) |
| Empty body | Hub 400; adapter not reached | same | new Pay 400 |
| Unknown event | 200, no fulfill | 200, no fulfill | new Pay ignores non-`checkout.session.completed` |
| Signature fail HTTP | Hub **500** (`InvalidOperationException`) — **do not copy** | same Hub 500 | new Pay **400** |

013/06: “Xendit `x-callback-token` and Razorpay `X-Razorpay-Signature` are **not** S1. Do not add ‘just the verify function.’” Correct. Adding verify functions without a dogfood merchant is how Hub grew five parse methods and still 500’d bad signatures.

**Per-org secrets:** Hub stores webhook secret per `(OrganizationId, GatewayType)` in `TenantPaymentConfiguration`. New Pay Stripe currently uses **process env** `Pay:StripeWebhookSecret` (WebhookEndpoints). A later Xendit/Razorpay port **must not** add `Pay:XenditCallbackToken` as a second global. BYOK means the merchant’s callback token / Razorpay webhook secret sits next to their API key in `GatewayCredentials`, encrypted. If Stripe is still global-secret when that port happens, **fix Stripe first** rather than cloning the env-var shortcut.

---

## 9. Refunds (API true, inbound false)

| | Xendit | Razorpay |
|--|--------|----------|
| `SupportsApiRefund` | true | true |
| `RequiresMarkRefunded` | false | false |
| Call | `POST /refunds` + `Idempotency-key` header; amount **major**; prefer `payment_id` | SDK refund on `pay_`; amount **paise**; idempotency in **notes** |
| Soak | **No** | **No** |
| Inbound `REFUND_COMPLETED` | not mapped | not mapped |
| Partial | amount field exists; unsoaked | amount field exists; unsoaked |
| Failure | adapter `false` → `REFUND_FAILED`; ops retry | same |
| Mark-refunded escape | **none** (unlike Billplz) | none |

New Pay S1 does not need this. `NP-MON-005` is V1. `NP-SOON-006` partial refunds are soon. `NP-LAT-002` can ship hosted-pay **without** refunds on day-of-port, or with refunds if Stripe refunds are already boring. Do not port refunds first. Do not port refunds without mapping the inbound event **or** documenting “dashboard refunds do not reverse the journal.”

Hub `GatewayRefundRequestedIntegrationEventHandler` is MediatR/outbox. New Pay same-handler world: merchant POST refund → PSP HTTP → reverse journal in **one** transaction if PSP 2xx, with unique refund idempotency. Do not `PublishAsync` a `GatewayRefundRequested` to yourself.

---

## 10. Wallets (GrabPay etc.) — hosted page, not Pay pixels

Standing law: we do not render QR. We do not rebuild wallets.

Live truth:

- Xendit hosted invoice **may** show GrabPay / ShopeePay / cards / FPX / QR if the **Xendit account** has them, and if (unused) metadata requests them.
- Razorpay hosted payment link shows whatever that **Razorpay/Curlec account** acquired (cards, UPI, netbanking, …). Hub does not pass a method allow-list.
- `SupportsHostedWallet("XENDIT","GRABPAY")` is true and **unread** at generate.
- Billplz GrabPay flag is false; Billplz DuitNow QR flag is true; neither is a hop-1 tile.
- New Pay Stripe session is cards (Hub Stripe also forces `card`; wallets ride on card). `:5179` must not grow a GrabPay button because a later Xendit port exists in a plan file.

[007/06](../007-feats/06-sea-fintech-platforms.md) §9: GrabPay / DuitNow / PayNow are **rails**, not products a salon switches to. Lazuar must not become a DuitNow member. Expose whatever the active BYOK adapter already acquires. Adding Xendit to “get GrabPay” is a merchant conversation for `NP-LAT-002`, not a reason to register the adapter this week.

Hop-1 copy when (later) the gateway is Xendit: “You will pay on Xendit’s page. Methods depend on the merchant’s Xendit account.” Not a grid of wallet logos we do not control.

---

## 11. Honesty problems — 008 vs live vs new Pay

008 is historical. Live files win. New Pay must not re-import closed Hub bugs **or** closed Hub *product* lies.

| Claim in 008 §6–7 / 008/10 | Live Hub on `ee2db8e5` | New Pay |
|----------------------------|------------------------|---------|
| Razorpay `SetupFutureUsage` mints registration link; tokens discarded | **Closed at adapter** (068). Always payment link. `_ = setupFutureUsage`. Tokens still discarded if they appeared (`TryVaultIds`). InitiateCheckout still **asks** for setup on recurring. | Do not take the flag on a reminder rail. Do not implement registration links. |
| Razorpay EventId fallback = payment id; fail-then-capture drop | **Closed** (066). Fallback `TYPE:pay_`. Header preferred. | Steal namespaced EventId. Never Guid. |
| Razorpay `invoice.expired` → `PAYMENT_FAILED` | **Closed** (069). Ignored. Only `payment.failed`. | Do not map link-expiry to a failed pay of a payment that does not exist. |
| Razorpay label “MY e-mandate + cards” | **Closed as copy.** Live: “cards; reminder-only until token soak.” Capability still false. | Never say e-mandate. `NP-XX-011`. |
| Razorpay dummy `billing@lazuar.com` | Removed; replacement branch dead | Do not port `ChargeOffSessionAsync` |
| Razorpay dummy `+60100000000` | **Closed on generate** (omit contact). Issue 227 may still describe the old line. | Omit unknown phone. Fail closed on blank email. |
| Xendit ops UI inoperable | **Closed as form.** Amber + API key + callback token. Dropdown still first-class. | Do not add the dropdown until `NP-LAT-002`. When added, steal amber, not five-option peer status. |
| Xendit README “adapter does not exist” | **Closed as README watermark.** Adapter is named as hosted-invoice wrap. Some docs still lag. | New Pay README/docs must not list Xendit as a live rail until the 400 wall moves. |
| Xendit EventId = invoice id | **Moved.** Now `PAYMENT_COMPLETED:inv` / `PAYMENT_FAILED:inv`. PAID+SETTLED still collapse. | Steal namespacing. Collapse PAID/SETTLED on purpose. |
| Xendit refund `invoice_id` only, unsoaked | **Moved.** GET invoice, prefer `payment_id`, `Idempotency-key` header. Still unsoaked. | Do not ship refunds without a sandbox 2xx. |
| Xendit callback token not body HMAC | **Still true.** Hash-then-compare improved vs raw string compare. | State replay risk. Do not invent HMAC. |
| Wallet flags ≠ allow-list (TnG/Boost/DuitNow) | **Still true.** Unused `xendit_payment_methods`. | Do not ship hop-1 wallet tiles. |
| Five-adapter factory as product | **Still live in Hub.** | **Refuse.** G24. `NP-GW-003`. This paper. |
| Signature fail 500 | **Still live in Hub** webhook endpoint | New Pay Stripe is 400. Keep 400 for later rails. |
| Hub “not a fulfillment engine” | **Still live** | New Pay same-handler. Do not copy the split. |

**Leftover Hub lies that would become new Pay lies if copied as-is:**

1. **`IPaymentGatewayAdapter` + factory.** Capability-blind resolver. Every name is a peer.
2. **`ChargeOffSessionAsync` on a reminder rail.** Dead code that looks like a feature.
3. **`setupFutureUsage` on every generate.** Commerce still sends true for Razorpay recurring.
4. **Unread wallet/QR flags as product.** 
5. **Five-option dropdown.** Even with honest labels, listing the rail makes it a dogfood candidate.
6. **Razorpay SDK + Newtonsoft** on a host that does not need them.
7. **Webhook allow-list of five** before any merchant has keys.
8. **Portal throw** implemented “for the interface.” Just do not add the method.
9. **Inbound refunds dropped** while `SupportsApiRefund` is true — ops thinks refunds are complete; dashboard refunds are invisible.
10. **Docs that say “multi-gateway”** while one rail is soaked.

---

## 12. Why copying them now recreates the factory-of-five lie

The factory of five is not “five class files exist.” It is a **product shape**:

```
dropdown of 5
    → vault of 5
        → factory.GetAdapter(name)
            → same port (generate, parse, refund, portal, off-session)
                → Commerce product.GatewayName ∈ {5}
                    → billing job must remember who cannot charge
                        → marketing says “local Asian gateways”
```

Hub paid that tax. 011 left the old tree so new Pay would not. Copying `XenditGatewayAdapter.cs` and `RazorpayGatewayAdapter.cs` onto 8081 **this week** recreates the lie even if you:

- keep `SupportsOffSession` false,
- paste the amber banners,
- never call `ChargeOffSessionAsync`,
- IsolationTest-rename `Modules.` away.

Because:

**A. The 400 wall is the product.** `GatewayEndpoints` saying `"Bar B first rail is stripe"` is how Bar B stayed one rail. Opening `provider is stripe or xendit or razorpay` so a demo can paste `xnd_` is how Hub’s dropdown grew. G10/G24 already ticked “do not add Razorpay, Xendit, Billplz, Fiuu, or a factory of five.” Undoing that to “steal HTTP” is a status flip of `NP-LAT-002` without a named merchant.

**B. Two later rails are still five with CHIP/Billplz.** Day-one allowed set is Stripe **or** Stripe+one MY rail (`NP-GW-002`/`003`). Adding Xendit **or** Razorpay before the MY rail is dogfood is skipping `NP-SOON-008`. Adding **both** is the factory. Engineers will add both “because the files are a pair” — this paper is the pair; the implementation must not be.

**C. The port infects Stripe.** `IPaymentGatewayAdapter` would have to be invented on 8081 to “plug them in.” Then `StripeHosted` grows `setupFutureUsage`, `ChargeOffSessionAsync`, `GenerateCustomerPortalAsync` “for the interface.” That is how Stripe Billing Portal and setup-as-paid snuck into Hub. New Pay Stripe currently: `Mode = "payment"`, metadata `checkout_id`/`org_id`, one create method. Protect that.

**D. NuGet infects the host.** `Razorpay` 3.3.2 + Newtonsoft on `Lazuar.Pay.csproj` is a dependency the IsolationTests do **not** ban (they ban Hub project names, not PSP SDKs). Stripe.net is already there for a reason. Razorpay is not.

**E. Webhook routes without dogfood.** `POST /v1/webhooks/xendit/{orgId}` with a verify function and no merchant is a 200-or-400 surface attackers can hit. Hub allow-listed Xendit before ops could paste keys (008). Live ops can paste keys; new Pay still should not expose the route.

**F. Demo theater.** A five-name matrix with two unsoaked wrap-rails is exactly 008’s README contradiction (adapter exists / adapter not shipping) in a new folder. Bar B can already take a Stripe card and write `RCPT-`. That is the demo. Xendit GrabPay on a hosted page we do not control is not a Bar B story.

**G. xenPlatform / Curlec gravity.** 007/06: do not rebuild xenPlatform; do not become HitPay; do not homemade FPX e-mandate. Once the classes exist, the next ticket is “flip `SupportsEmandate`” or “add `for-user-id`.” `NP-XX-011` exists because that ticket will be filed. Absence of the class is cheaper than a refuse comment.

**H. Commerce-shaped product picker.** Hub lets a product’s `GatewayName` be `XENDIT`. New Pay catalog on this SHA is MYR amounts and Stripe hosted URL. A Xendit provider column on products, before CHIP exists, teaches merchants to pick rails Pay will not renew silently — without the amber, because `:5178` has not stolen the banner yet.

**I. Same-handler vs factory ACK.** Hub webhook: verify → log → publish → 200 `{ received: true }` — “intake ACK only.” Five adapters made that split feel necessary (each parse is different; fulfillment is generic). New Pay: verify Stripe → insert idempotency → `FulfillPaidAsync` in the same request. Five parse functions will tempt someone to `Publish` again “because Xendit’s PENDING should not fulfill.” Keep a `switch(provider)` in **one** webhook endpoint, or a later `XenditHosted.TryMapPaid`, **called from the same handler**. Do not resurrect `ProcessGatewayWebhookCommand`.

---

## 13. Reasons to keep later (product, not laziness)

These are why `NP-LAT-002` is later, not why the HTTP is worthless.

1. **No named merchant is blocked on them.** 011 dogfood sentence: paste CHIP or Stripe. 013 G10: pick one MY rail, not Razorpay/Xendit. Aura System B historically is Billplz. India expansion is not a 2026 Pay dogfood path. SEA wallets are CHIP-hosted or Xendit-hosted, and CHIP is the MY candidate that can also vault.

2. **They are wrap-rails of wrap-rails.** Xendit is an acquirer. Razorpay/Curlec is an acquirer. Lazuar is software on the merchant’s account. The **complement** (007/06) is correct: compliance + subscription state + ledger on top of Xendit. That complement is worthless until Pay’s own Stripe/CHIP journal and `RCPT-` are boring. A third hosted URL does not teach same-handler fulfillment.

3. **Reminder-only is a product cost.** Each later rail is another “we email a link; we do not silent-debit” banner, another dashboard webhook to configure, another way a merchant believes AUTO_CHARGE works. Hub already has that cost ×3 (Billplz, Razorpay, Xendit). New Pay should pay it **once** for Billplz if Billplz is the MY rail, not three times.

4. **Xendit the company is the displacement threat, not the adapter.** HitPay + Xendit Payment Links are what a non-technical founder already sends on WhatsApp (007/06). Shipping a BYOK Xendit wrap does not win that founder. It wins a **headless** merchant who already has Xendit keys and refuses a second acquire. That merchant is a later conversation. Building the wrap to “match the README list” is Hub Wave 4 LP-045.

5. **Razorpay’s interesting MY story is e-mandate, which we refuse.** Curlec in the dropdown is a fossil of LP-032. `SupportsEmandate` is false. The adapter is a payment link. India cards-on-file would require flipping off-session after a token soak we do not have. There is no honest Razorpay v1 other than “hosted link like Billplz.” Billplz is the MY hosted-link brand. Razorpay-as-Billplz is a duplicate wrap.

6. **Operational load.** Two more dashboard callback URLs, two more secret shapes (`xnd_` vs `KeyId:KeySecret`), two more amount-unit brains (major vs paise), two more EventId schemes, two more unsoaked refund APIs, no auto-register, replay-friendly signatures. Stripe on 8081 already has a global webhook secret shortcut to clean up. Do not add work.

7. **Xendit API generation risk.** Copying `/v2/invoices` now means migrating twice (Hub fossil → Pay fossil → Payment Sessions). Porting later means reading current docs once.

8. **Isolation and NuGet.** Every later rail is a chance to import Hub types. The Razorpay package is the thin end. Standing law: do not add it now.

9. **Unread flags.** DuitNow/GrabPay as capability bits without generate readers already confused 008. Copying the matrix onto 8081 copies the confusion. Wait until a merchant asks “can buyers pay GrabPay?” and the answer is “enable it on CHIP” or “we will wrap Xendit as reminder-only.”

10. **`NP-SOON-008` is the queue.** Second gateway after the first two are boring. Third+ is later. Razorpay and Xendit are not in the first two.

---

## 14. What to steal vs what to leave (HTTP judgment)

### 14.1 Steal later (judgment, not types)

**Xendit**

- `POST https://api.xendit.co/v2/invoices` (or successor Payment Session URL at port time) Basic `key:`.
- Amount in **major units**. Currency fail-closed. Buyer email fail-closed (`TryResolveEmail`).
- Return `invoice_url` + invoice id. Stamp Pay `checkout_id` / `org_id` in metadata (and prefer checkout id as `external_id`).
- `ApplyPayingTenantMetadata` spirit if platform charges ever exist; new Pay v1 has no system-org checkout — **do not copy** `SystemOrganizationId`.
- Verify `x-callback-token` by SHA256-then-`FixedTimeEquals`. Empty secret fail-closed.
- Map PAID/SETTLED/invoice.paid → paid. EXPIRED/FAILED → failed. PENDING → ignore. Missing id/currency → 400 unusable.
- EventId `paid:{invoiceId}` / `failed:{invoiceId}` (do not use Hub’s `PAYMENT_COMPLETED` string if new Pay uses session status). Collapse PAID+SETTLED.
- Refund: GET invoice, prefer payment id, `POST /refunds` with `Idempotency-key`, amount major. Only after a soak.
- Off-session: **absent**. Not `return false`.
- Amber copy from ops.
- Channel filter as **dashboard defaults** unless a merchant asks to pin methods. Do not import `OVO` into a MY dogfood.

**Razorpay**

- `POST https://api.razorpay.com/v1/payment_links` Basic `keyId:keySecret`. **Raw HTTP.** No NuGet unless a later paper argues it.
- Amount integer minor units. Currency fail-closed at webhook; fail-closed at generate too (do not send empty).
- Always payment link. **Never** `subscription_registration`. Discard any future-usage flag at the call site, not inside a dead branch.
- `short_url` + `plink_id` on create; persist `pay_id` from webhook as the refund handle.
- HMAC-SHA256 raw body vs `X-Razorpay-Signature`. Prefer `X-Razorpay-Event-Id`; else `completed:{payId}` / `failed:{payId}`. Never Guid. Never bare `pay_` as EventId.
- Map `payment.captured` / `payment.failed` only. Ignore `invoice.expired`, `payment.authorized`, `refund.*` until inbound refunds exist.
- Omit `contact` when unknown. Fail closed on blank email (stricter than Hub generate).
- Refund: `POST /v1/payments/{pay_id}/refund` with a **real** idempotency header if available; amount paise. Only after a soak.
- Off-session: **absent**.
- Label: reminder-only payment link. Do not say Curlec e-mandate.
- Webhook secret per org, not env.

**Shared**

- Wrap-rails helper: `SupportsOffSession` = stripe|chip only. `IsReminderOnly` = everything else including xendit|razorpay|billplz|blank.
- `SupportsEmandate` = false.
- Empty webhook body 400. Signature fail 400. Duplicate `(org, provider, event_id)` 200 no-op. Same handler fulfill.
- GET keys return last-4, never secrets. VIEWER/member cannot PUT (already true for Stripe on 8081).
- IsolationTests remain. No MediatR. No `Modules.Payments`. No factory interface.

### 14.2 Do not steal

- `IPaymentGatewayAdapter` / `PaymentGatewayFactory` / `AddScoped` × 5.
- `Razorpay` NuGet / `Razorpay.Api` / Newtonsoft (unless a dedicated later paper).
- `ChargeOffSessionAsync` (both).
- `GenerateCustomerPortalAsync` throws.
- `CreateRegistrationLink` / `max_amount * 10` / `method=card` mandate (gone from live; do not resurrect from 008).
- Hub webhook 500 on verify fail.
- Hub `ProcessGatewayWebhookCommand` + outbox + “intake ACK only.”
- `IntegrationCheckoutSessions`.
- `PaymentsDbContext` / `payments` schema / inbox jobs.
- Unread `SupportsHostedWallet` / `SupportsDuitNowQr` as `:5178` chrome.
- Dummy PII (`customer@example.com`, `+60100000000`, `billing@lazuar.com`).
- Guid EventId.
- Invented MYR.
- xenPlatform `for-user-id`, Xendit Subscriptions, Razorpay Subscriptions, Stripe Billing `subscription.updated`.
- Homemade FPX e-mandate / Curlec `method=emandate`.
- CHIP localhost→`lazuar-local-dev.com` registrar folklore (Xendit/Razorpay have no registrar — keep it that way unless a soak needs one).
- Hub last-resort `BILLPLZ` if gateway name blank.
- Five-option dropdown on `:5178`.
- Admin `PlatformPaymentSettingsPage` as a Pay destination (`NP-XX-018`).
- `DecryptOrPlaintext` / `Jwt:Secret` as KMS.
- Commerce `SetupFutureUsage: interval != one_time` for reminder rails.

---

## 15. Later-port checklist (does not weaken day-one Stripe / CHIP)

This is a **later** checklist. Do not start it until: Stripe hosted + webhook + same-handler `RCPT-` is boring in the environment you dogfood, **and** the one Malaysian rail (`NP-GW-003`) is either live and boring **or** explicitly deferred with `NP-GW-003` still todo and a written reason. Do not flip `NP-LAT-002` to doing because this file exists.

### 15.0 Preconditions (fail the port if skipped)

- [ ] `NP-LAT-002` is still the tracker row. Do not hide Xendit under `NP-GW-003` or `NP-SOON-008`.
- [ ] `NP-SOON-008` (second gateway after first two boring) is `done` **or** the port is explicitly “third rail, later,” not “while we are in GatewayEndpoints.”
- [ ] IsolationTests still ban `MediatR` / `Modules.` / `BuildingBlocks` / `lazuar-api`.
- [ ] `Lazuar.Pay.csproj` still has **no** `Razorpay` package. Xendit still has **no** SDK package.
- [ ] `SupportsEmandate` remains false. No Curlec mandate ticket bundled with the wrap.
- [ ] Stripe `CreateHostedUrlAsync` still has no `setupFutureUsage` / portal / off-session “for the interface.”
- [ ] `PUT /v1/orgs/{orgId}/gateway` still 400s unknown providers. The allow-list grows by **one name** per port, with a test that the other later name still 400s.
- [ ] `POST /v1/webhooks/{provider}/{orgId}` still 400s unknown providers. Same one-name growth.
- [ ] Merchant Vite does not gain a five-option `<select>` “while adding Xendit.” One extra provider + amber, or nothing.
- [ ] A **named** merchant (or a named sandbox account in-repo) exists. No adapter without keys in front of you.
- [ ] Wrap copy is written **before** the HTTP client: reminder-only, no silent debit, no e-mandate, wallets on the processor page.

### 15.1 Pick **one** later rail, not both

- [ ] Write the name in the PR: `xendit` **or** `razorpay`. The other stays 400.
- [ ] Default pick if a MY merchant wants SEA wallets on a wrap they already KYC’d: **Xendit** (hosted invoice, GrabPay on **their** page, reminder-only).
- [ ] Default pick if an IN merchant wants a payment link on keys they already have: **Razorpay** (payment link, reminder-only). Do not pick Razorpay to “get Curlec FPX auto-debit.”
- [ ] Do not port the pair because this evaluation is a pair.

### 15.2 Secrets (BYOK, per org)

- [ ] Extra columns or extra `provider` rows on `GatewayCredentials`: API secret + webhook/callback secret. Encrypted with the same `SecretBox` as Stripe. GET last-4 only.
- [ ] Xendit: `xnd_development_` / `xnd_production_`. Razorpay: store key id and key secret **separately** (do not require `KeyId:KeySecret` concatenation as the only API).
- [ ] Webhook secret is **per org**, not `Pay:XenditCallbackToken` env. If Stripe is still global env, fix Stripe’s webhook secret to per-org **first** (otherwise you clone the shortcut).
- [ ] Writer authz same as Stripe PUT (`RequireWriterAsync`). Member GET ok; member PUT no.
- [ ] First-time PUT without a secret is 400 (Hub backend already; Hub 008 UI failed this for Xendit — live UI fixed; keep 400).

### 15.3 Create hosted page (two functions, not a port interface)

- [ ] `XenditHosted.CreateHostedUrlAsync` **or** `RazorpayHosted.CreateHostedUrlAsync` next to `StripeHosted` — same call shape: checkout row in, URL out.
- [ ] HTTP via `HttpClient`. No `IPaymentGatewayFactory`.
- [ ] Stamp `checkout_id` + `org_id` in processor metadata.
- [ ] Persist `provider` + `provider_session_id` (invoice id / `plink_…`) + `checkout_url`. Status stays `open`.
- [ ] Fail closed: missing currency, unusable email, non-2xx, missing URL field.
- [ ] No `setupFutureUsage` parameter.
- [ ] No payment-method allow-list unless the merchant asked; dashboard defaults are the wrap.
- [ ] Quantity/amount: Xendit major units; Razorpay integer minor. Tests lock a 10.50 MYR example both ways.
- [ ] PublicPay redirect uses the same path as Stripe (hosted URL on the checkout row). `:5179` does not grow wallet tiles.

### 15.4 Webhook (same handler as Stripe)

- [ ] Same `POST /v1/webhooks/{provider}/{orgId}` with a provider switch. Empty body 400. Unknown provider 400. Rail not configured 400. Signature fail **400** (not Hub 500).
- [ ] Verify with the **org’s** stored secret.
- [ ] Event id as specified in §14. Insert `(org_id, provider, event_id)` first. Duplicate 200 `{ duplicate: true }` and **do not** fulfill.
- [ ] Paid → `FulfillPaidAsync` (same function Stripe uses). Failed → do not reverse a `paid` session; log; do not invent `PAST_DUE` without a real failed charge (`NP-FUL-005`).
- [ ] Setup/zero/PENDING/authorized/expired-invoice: 200 ignore.
- [ ] Missing id/currency after verify: 400 unusable (stop retries), not 500, not Guid.
- [ ] Tests: good signature paid; bad signature 400; replay no second `RCPT-`; PENDING/authorized ignored; fail then pay distinct event ids.

### 15.5 Wrap-rails (billing job, when it exists)

- [ ] Helper stays: off-session only `stripe`|`chip`. Xendit/Razorpay are reminder-only.
- [ ] Renew job (V1 `NP-FUL-004`): mint hosted URL + email. **Never** call a Razorpay recurring API. **Never** call Xendit payment tokens.
- [ ] Dunning AUTO_CHARGE does not exist on these names. Hub test `PastDue_Razorpay_DoesNotPublish` is the spirit; re-implement as “no off-session publish,” not as a copy of Commerce dunning.
- [ ] Product/ops copy: Not auto-debit. Steal Xendit amber / Billplz amber. Write a Razorpay amber (Hub is missing one).

### 15.6 Refunds (after pay is boring)

- [ ] Optional on first later-rail PR. If included: sandbox 2xx evidence in the PR body.
- [ ] Xendit: GET invoice → payment id → POST `/refunds` + Idempotency-key. Razorpay: refund `pay_…` not `plink_…`. Persist payment id at fulfill time.
- [ ] Inbound refund events: map or document dashboard-refund gap. Do not claim `SupportsApiRefund` while dashboard refunds skip the journal.
- [ ] No mark-refunded for these names (that is Billplz/offline).

### 15.7 Wallets / QR

- [ ] No hop-1 GrabPay/DuitNow tiles.
- [ ] No QRCoder. No `GenerateQrAsync`.
- [ ] Copy: “Methods appear on the processor hosted page when the merchant enabled them there.”
- [ ] Do not copy `SupportsHostedWallet` mismatches (TnG/Boost/DuitNow vs `QR_CODE`).
- [ ] Do not add `xendit_payment_methods` metadata from Commerce unless a merchant asks to pin channels.

### 15.8 Docs / UI honesty at port time

- [ ] `:5178` heading is the provider name + reminder-only, not “Payment Credential Vault” with five peers.
- [ ] Pay docs do not say “multi-gateway BYOK: Stripe, CHIP, Billplz, Razorpay, Xendit” until each name has a soaked webhook in the environment you ship.
- [ ] Do not retarget `lazuar-ops` at 8081. Steal wording, new origin.
- [ ] Do not list Curlec e-mandate. Do not list xenPlatform. Do not list Xendit Subscriptions.

### 15.9 Tests that protect Stripe/CHIP

- [ ] Existing Stripe webhook tests still pass without change of event names.
- [ ] `IsolationTests` still green.
- [ ] PUT `provider=razorpay` 400 until that name is the one being ported; PUT `provider=xendit` 400 if Razorpay is the one being ported (and vice versa).
- [ ] PUT `provider=stripe` still succeeds. Do not “genericize” GatewayEndpoints into a loop over five validators.
- [ ] No new `IPaymentGatewayAdapter` test suite that requires Stripe to implement `ChargeOffSessionAsync` in the focused host.

### 15.10 Explicit non-goals at port time

- [ ] Not Midtrans, PayMongo, HitPay, Fiuu, Cashfree, 2C2P.
- [ ] Not Xendit Payment Tokens / Subscriptions / xenPlatform.
- [ ] Not Razorpay Subscriptions / registration links / e-mandate.
- [ ] Not Pay-hosted wallet buttons.
- [ ] Not a second factory. Not MediatR. Not Hub session tables.
- [ ] Not flipping `NP-GW-002` because Xendit showed a card form.

---

## 16. Mapping onto new Pay files (sketch, not an implementation)

When (later) the checklist starts, the **smallest** surface is:

| New Pay file (today) | Later change |
|----------------------|--------------|
| `Gateways/StripeHosted.cs` | Unchanged. |
| `Gateways/XenditHosted.cs` **or** `RazorpayHosted.cs` | **New.** Create URL only. |
| `Gateways/GatewayEndpoints.cs` | Allow-list adds **one** lowercase name. Still 400 otherwise. `"Bar B first rail is stripe"` becomes a named allow-list constant, not `IEnumerable<IPaymentGatewayAdapter>`. |
| `Gateways/WebhookEndpoints.cs` | Switch on provider; Stripe branch untouched; new branch verify+map+same `FulfillPaidAsync`. |
| `Lazuar.Pay.csproj` | Still no Razorpay package. `HttpClient` for Xendit/Razorpay. |
| `WorkspacePage.tsx` | Optional second paste form with amber. Not a five `<option>` select. |
| IsolationTests | Still ban Hub tokens. Optionally ban `Razorpay.Api` string if someone tries the NuGet. |

That is G24’s “two functions are enough” applied to a later rail. Hub’s five-method port is how later became day one.

---

## 17. SEA product context (007/06) — why HTTP steal is complement, not a SKU

[007/06](../007-feats/06-sea-fintech-platforms.md) is still the right commercial picture, with one code update: on 16 Aug 2026 that paper said the Xendit adapter was **not present**. Live SHA: the adapter **is** present as a hosted-invoice wrap. The commercial verdict did not change.

- Lazuar is **not** in Xendit’s regulatory box (Payex PLT / BI / BSP). BYOK. 0% GMV.
- Xendit Payment Links + Subscriptions + xenPlatform are **finished acquirer products**. Competing on “time-to-first-WhatsApp-link” is a losing fight. Competing as **ledger + reminder state machine + (later) tax provider on top of Xendit** is the intended shape.
- xenPlatform is Stripe Connect for SEA. Tracker trap. Do not grow `for-user-id`.
- HitPay is the closer SMB displacement threat (link + invoice + POS + recurring). Do not become HitPay. Do not add terminals.
- GrabPay/DuitNow/FPX are **rails inside** a processor. CHIP already surfaces them on the brand. Xendit surfaces them on the invoice page. Pay must not draw them.
- Razorpay in 007 is India / Curlec-adjacent, not the MY SME default. Billplz/CHIP/HitPay/Xendit are the names a KL founder says.

Therefore a later Xendit port is **not** “Lazuar launches in Indonesia.” It is “a MY (or ID) merchant who already has Xendit keys can paste them, buyers pay on Xendit’s page, Pay writes `RCPT-` from a verified callback.” Same sentence with Razorpay and `short_url`. If that sentence is not a named merchant’s ask, keep the 400 wall.

---

## 18. Verdict

Live Hub has two **real** wrap adapters:

- **Xendit:** `POST /v2/invoices`, callback-token webhooks, API refund with payment-id lookup, off-session **false**, wallets only as hosted-page pixels, ops form **operable** with honest amber (008’s inoperable-UI finding is **stale**).
- **Razorpay:** `PaymentLink.Create`, HMAC webhooks, API refund on `pay_`, `SetupFutureUsage` **discarded** (008’s registration-link finding is **stale** at the adapter; Commerce still asks), `ChargeOffSessionAsync` **dead**, label reminder-only (008’s e-mandate label is **stale**), not demoable.

Live new Pay (`ee2db8e5`) has **neither** string in the tree. `PUT /v1/orgs/{orgId}/gateway` 400s anything except `stripe`. Webhooks 400 `unknown provider`. Merchant Vite pastes Stripe only. That is `NP-LAT-002` / 013 standing law encoded as HTTP.

Copying the two Hub classes onto 8081 now would recreate the factory of five: a capability-blind port, a NuGet, a five-name dropdown, unread wallet flags, dead off-session methods, and a marketing list of rails nobody dogfoods — while Stripe’s same-handler path is the only one that has actually written a `RCPT-`.

Steal HTTP later, one name at a time, reminder-only, no Razorpay NuGet, no e-mandate, no xenPlatform, same handler as Stripe, amber before code. Until then the correct implementation is the 400.
)