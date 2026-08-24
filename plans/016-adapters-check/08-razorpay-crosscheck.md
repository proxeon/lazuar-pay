# 08 — Razorpay cross-check: Hub `RazorpayGatewayAdapter` vs Pay HTTP + HMAC

**Date:** 24 August 2026  
**Branch:** `feat/015-four-adapters`  
**HEAD SHA:** `c621ceba7fc7b79f16954d0819200cb21db6f22b` (`c621ceba`) — `docs(015): check off implemented T–Q phases`  
**Slice:** Razorpay only. Live files are authority. [015](../015-four-adapters/README.md) R10–R25 checklists are a map, not proof. [016 README](./README.md) assigns this file as Hub payment-link HTTP vs `RazorpayHosted` + `RazorpayWebhook`.  
**Not an implementation.** Do not treat a checked box in `plans/015-four-adapters/checklists/r*.md` as a test that exists.

Parent product lock ([015 `decisions.md`](../015-four-adapters/checklists/decisions.md)):

- Transport is **HttpClient**. **No** `Razorpay.Api` unless HTTP is blocked and A00 is amended.
- Payment **link**, not invoice, not registration/mandate.
- Fulfill **`event == payment.captured` only**.
- Event id: header `X-Razorpay-Event-Id` if present, else `captured:{pay_id}` / `failed:{pay_id}` — **never** bare `pay_`.
- HMAC-SHA256 of **raw body** with the per-org webhook secret.
- Processor JSON `tax` / `fee` are **not journal lines**. `unknown ≠ 0`.
- Email required if Hub sent a customer block (`BuildPaymentLinkRequest` always does).
- No e-mandate, no `ChargeOffSession`, no official SDK.
- Capability this program: `hosted_link`. Reminder-only copy.

Hub is read for HTTP judgment. Pay must not `ProjectReference` `apps/lazuar-api`. IsolationTests ban the string `Razorpay.Api` in host source and in Pay `*.csproj` files.

---

## 1. Files opened (this SHA)

### Hub (judgment only)

| Path | Why it is in this slice |
|------|-------------------------|
| [`apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs`](../../apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs) | Full adapter: `GetClient`, `GenerateCheckoutAsync`, `BuildPaymentLinkRequest`, `ParseWebhookAsync`, `ResolveEventId`, `IsPaymentFailedEvent`, `TryReadCurrency`, `MapPaymentFailed`, `ChargeOffSessionAsync`, `IssueRefundAsync`, portal throw |
| [`apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs`](../../apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs) | `ToMinorUnits` / `ToMinorUnitsTruncating`, `ExtractName`, `ProductDescription`, `IsUsableBuyerEmail`, `TryNormalizeCurrency` (Razorpay **does not** call the last one; it has its own `TryReadCurrency`) |
| [`apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs`](../../apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs) | Port Pay refuses: `GenerateCheckoutAsync(..., setupFutureUsage)`, `ParseWebhookAsync` with `TaxAmount`/`GatewayFee`, `ChargeOffSessionAsync`, `IssueRefundAsync`, factory |
| [`apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs`](../../apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs) | `SupportsOffSession("RAZORPAY")` false; `SupportsEmandate` always false; `SupportsApiRefund` **true** for Razorpay (Parked for Pay) |
| [`apps/lazuar-api/Modules/Payments/Infrastructure/Modules.Payments.Infrastructure.csproj`](../../apps/lazuar-api/Modules/Payments/Infrastructure/Modules.Payments.Infrastructure.csproj) | `<PackageReference Include="Razorpay" />` — NuGet id `Razorpay`, C# namespace `Razorpay.Api` |
| [`apps/lazuar-api/Directory.Packages.props`](../../apps/lazuar-api/Directory.Packages.props) | `Razorpay` 3.3.2 |
| [`apps/lazuar-api/Modules/Payments/Infrastructure/DependencyInjection.cs`](../../apps/lazuar-api/Modules/Payments/Infrastructure/DependencyInjection.cs) | `AddScoped<IPaymentGatewayAdapter, RazorpayGatewayAdapter>()` — factory gravity |
| [`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs`](../../apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs) | Why Hub parses `fee`/`tax`: books `ExpenseGatewayFee` and `LiabilityTaxPayable` when `> 0` |
| [`apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/RazorpayGatewayAdapterTests.cs`](../../apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/RazorpayGatewayAdapterTests.cs) | Hub unit lock: no registration keys; header EventId; fail-then-capture distinct ids; missing currency; invoice.expired ignored |

### New Pay host

| Path | Role |
|------|------|
| [`apps/lazuar-pay/src/Lazuar.Pay/Gateways/RazorpayHosted.cs`](../../apps/lazuar-pay/src/Lazuar.Pay/Gateways/RazorpayHosted.cs) | `IHostedRail`. `POST https://api.razorpay.com/v1/payment_links`. `TrySplit`. No SDK. |
| [`apps/lazuar-pay/src/Lazuar.Pay/Gateways/RazorpayWebhook.cs`](../../apps/lazuar-pay/src/Lazuar.Pay/Gateways/RazorpayWebhook.cs) | HMAC raw body; `payment.captured` vs `payment.failed`; EventId header or `captured:`/`failed:` |
| [`apps/lazuar-pay/src/Lazuar.Pay/Gateways/IHostedRail.cs`](../../apps/lazuar-pay/src/Lazuar.Pay/Gateways/IHostedRail.cs) | Two members only: `Provider` + `CreateHostedUrlAsync`. No off-session, no refund, no parse. |
| [`apps/lazuar-pay/src/Lazuar.Pay/Gateways/PspParseResult.cs`](../../apps/lazuar-pay/src/Lazuar.Pay/Gateways/PspParseResult.cs) | No `TaxAmount`, no `GatewayFee`. Amount is `AmountMinor`. |
| [`apps/lazuar-pay/src/Lazuar.Pay/Gateways/PayProviders.cs`](../../apps/lazuar-pay/src/Lazuar.Pay/Gateways/PayProviders.cs) | `"razorpay"`; `RequiresEmail` true; `AllowsPublicMerchantId` false; `Capability = "hosted_link"` |
| [`apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs`](../../apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs) | PUT join `key_id:key_secret`; `TrySplit` 400; last4 of **key_id**; reject `public_merchant_id` |
| [`apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs`](../../apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs) | Switch arm; empty 400; unique `(org, provider, event_id)`; amount/currency match; one TX fulfill |
| [`apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs`](../../apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs) | Start dispatch; email 400 before rail; persist `plink_` session id |
| [`apps/lazuar-pay/src/Lazuar.Pay/Gateways/BuyerEmail.cs`](../../apps/lazuar-pay/src/Lazuar.Pay/Gateways/BuyerEmail.cs) | Same placeholder `customer@example.com` as Hub `GatewayCommon.PlaceholderEmail` |
| [`apps/lazuar-pay/src/Lazuar.Pay/Money/MoneyMath.cs`](../../apps/lazuar-pay/src/Lazuar.Pay/Money/MoneyMath.cs) | `ToMinor` AwayFromZero ×100; currency length === 3, no MYR default |
| [`apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs`](../../apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs) | Two lines: cash D / revenue C for `checkout.Amount`. Official Receipt. No tax line. |
| [`apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs`](../../apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs) | Create defaults currency **MYR** when omitted (merchant `:5178` also hard-codes MYR) |
| [`apps/lazuar-pay/src/Lazuar.Pay/Program.cs`](../../apps/lazuar-pay/src/Lazuar.Pay/Program.cs) | `AddHttpClient("razorpay")`; `AddScoped<RazorpayHosted>()` |
| [`apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj`](../../apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj) | Stripe.net only. No `Razorpay`, no `Razorpay.Api`. |

### Tests

| Path | Razorpay evidence |
|------|-------------------|
| [`apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`](../../apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs) | `BannedSrc` includes `"Razorpay.Api"`; csproj loop also greps `Razorpay.Api` |
| [`apps/lazuar-pay/tests/Lazuar.Pay.Tests/RailTests.cs`](../../apps/lazuar-pay/tests/Lazuar.Pay.Tests/RailTests.cs) | **One** method: `Razorpay_captured`. Start + HMAC captured + `tax:12`/`fee:30` + `JournalLines.Count() == 2` |
| [`apps/lazuar-pay/tests/Lazuar.Pay.Tests/WebhookTests.cs`](../../apps/lazuar-pay/tests/Lazuar.Pay.Tests/WebhookTests.cs) | Stripe-only verify/replay/setup. Unknown provider. **No** razorpay arm. |
| [`apps/lazuar-pay/tests/Lazuar.Pay.Tests/GatewayTests.cs`](../../apps/lazuar-pay/tests/Lazuar.Pay.Tests/GatewayTests.cs) | Stripe PUT/GET + CHIP brand_id. **No** razorpay key-split PUT. |
| [`apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPayTests.cs`](../../apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPayTests.cs) | Empty webhook 400 on **stripe** path (shared handler). No razorpay email start. |
| [`apps/lazuar-pay/tests/Lazuar.Pay.Tests/FakePspHandler.cs`](../../apps/lazuar-pay/tests/Lazuar.Pay.Tests/FakePspHandler.cs) | Records `LastUri` / `LastBody`. Razorpay test does not read them. |
| [`apps/lazuar-pay/tests/Lazuar.Pay.Tests/Lazuar.Pay.Tests.csproj`](../../apps/lazuar-pay/tests/Lazuar.Pay.Tests/Lazuar.Pay.Tests.csproj) | No `Razorpay` package. ProjectReference host only. |

Repo-wide grep of `apps/lazuar-pay/tests/**/*.cs` for `Razorpay` / `razorpay` hits **only** IsolationTests + `RailTests.Razorpay_captured`. There is no second Razorpay test method anywhere in the new host test project.

### 015 map (not proof)

R10 class, R11 PUT fields, R12 key split, R13 `POST /v1/payment_links`, R14 no official SDK, R15 discard `SetupFutureUsage`, R16 HMAC raw body, R17 `payment.captured` fulfill, R18 `payment.failed` ignore, R19 EventId header or `captured:{pay_}`, R20 missing currency fail-closed, R21 do not book JSON tax/fee, R22 no e-mandate, R23 no off-session, R24 customer email, R25 hermetic tests.

Every R10–R25 file is checked `[x]` including R18.2 “Test ignore”, R20.2 “Fixture without currency does not pay”, R12.2 “Helper unit test”, and R25.1’s full must-exist list. Live tests do **not** match that list. Section 10 records the mismatch line by line.

### Frontends (Razorpay field/copy only)

[`apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx`](../../apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx): rails include `razorpay`; copy **“Hosted payment link. Not e-mandate. We do not auto-debit.”**; two boxes `key_id` / `key_secret` joined client-side as `secret`; webhook secret; webhook URL hint `{payApi}/v1/webhooks/razorpay/{orgId}`; product+link hard-codes `currency: 'MYR'`.

[`apps/lazuar-pay-checkout/src/App.tsx`](../../apps/lazuar-pay-checkout/src/App.tsx): `email_required` from `GET /v1/pay/{token}`; Razorpay is `RequiresEmail` so the buyer cannot start without email. Callback `?status=verifying` matches `RazorpayHosted` default `callback_url`.

---

## 2. What Hub actually is (full adapter, this SHA)

`RazorpayGatewayAdapter` is a class on `IPaymentGatewayAdapter` with `GatewayType => "RAZORPAY"` (uppercase). It `using Razorpay.Api`. That is the gravity 015 refused to copy: the official SDK client, plus the Hub port’s refund / portal / off-session verbs, plus Billing’s habit of booking `GatewayFee` and `TaxAmount` as extra ledger lines.

### 2.1 `GetClient` — key split

```csharp
var parts = apiKey.Split(':');
var keyId = parts[0];
var keySecret = parts.Length > 1 ? parts[1] : "";
return new RazorpayClient(keyId, keySecret);
```

Judgment to steal:

- Ciphertext is stored as `key_id:key_secret` (one secret, colon).
- HTTP Basic is that same pair (SDK wraps it).

Judgment **not** to copy blindly:

- `Split(':')` then `parts[1]` **drops** any colon inside the secret. A secret `rzp_test_xxx:ab:cd` becomes keySecret `ab`.
- No colon → `keySecret = ""` and the SDK is still constructed. Hub fails later at the network, not at parse.
- Empty keyId (`:secret`) still builds a client.

Pay’s `TrySplit` takes the **first** colon and keeps the rest of the string as `keySecret`, and refuses empty either side. That is a strictness improvement, not a behavior copy.

### 2.2 `GenerateCheckoutAsync` — payment link, `SetupFutureUsage` discarded

```csharp
// Reminder-only: we do not claim e-mandate. SetupFutureUsage still mints a
// payment link, not a card-registration mandate (max_amount = 10× first charge).
_ = setupFutureUsage;
var client = GetClient(apiKey);
var req = BuildPaymentLinkRequest(...);
var link = client.PaymentLink.Create(req);
return Task.FromResult(new GatewayCheckoutResult(true, link["short_url"].ToString(), link["id"].ToString(), null));
```

Historical lie (009 B04-P11 / 013 `06-money-rails`): when `setupFutureUsage` was true, Hub used to mint a **card-registration** / subscription-registration payment link (`max_amount` 10× first charge) while capability said reminder-only. Live Hub **discards** the flag and always `PaymentLink.Create`. 015 R15 is “keep that discard; never send registration payloads.”

SDK call `client.PaymentLink.Create` is HTTP `POST https://api.razorpay.com/v1/payment_links` with Basic `key_id:key_secret`. That is the only create-verb 015 wants. Not invoices (`/v1/invoices`). Not orders+checkout (`/v1/orders`). Not recurring registration links.

Errors: Hub catches `Exception`, logs, returns `GatewayCheckoutResult(false, ...)`. Pay throws `InvalidOperationException` and Start maps it to **503**. Different seam, same operator meaning: do not redirect the buyer.

### 2.3 `BuildPaymentLinkRequest` (internal static — Hub tests lock this)

Request dictionary Hub sends (and Hub tests assert):

| Key | Hub value |
|-----|-----------|
| `amount` | `GatewayCommon.ToMinorUnitsTruncating(amount, quantity)` — despite the name this is `ToMinorUnits`: `Round(amount * qty * 100, AwayFromZero)` for non-zero-decimal. Default currency factor MYR=100 even when the payment currency is INR. **JPY would be wrong if anyone passed JPY through this helper**, because `ToMinorUnitsTruncating` does not take the currency argument. |
| `currency` | `currency.ToUpperInvariant()` — **no** length-3 check at create |
| `description` | `ProductDescription(productName, quantity)` (`"Plan (x2)"` or `"Lazuar Payment"`) |
| `customer` | `name` (metadata `customer_name` or `ExtractName(email)`), `email` = `customerEmail`, optional `contact` from metadata `customer_phone` |
| `notes` | **all** inbound metadata keys as objects |
| `callback_url` | `successUrl` |
| `callback_method` | `"get"` |

Hub tests `BuildPaymentLinkRequest_NeverMintsCardRegistration`: must **not** contain keys `subscription_registration` or `type`; amount 10 MYR → `1000`; currency `"MYR"`; customer without `contact` when phone absent.

Create-time email: Hub’s helper does **not** call `GatewayCommon.ResolveEmail` inside `BuildPaymentLinkRequest`. It will put whatever string the cashier passed into `customer.email`. The usable-email gate lives in Hub’s cashier / commerce, not in this method. Pay gates in `PublicPayEndpoints.Start` **and** inside `RazorpayHosted`.

`callback_url` is a **browser GET**, not the Lazuar webhook. [`docs/001-gaps/02-payment-webhooks.md`](../../docs/001-gaps/02-payment-webhooks.md) still says this correctly. Webhooks are a **dashboard** paste per merchant, URL `/v1/webhooks/razorpay/{orgId}` on Pay. That is why Billplz’s localhost-callback 400 does **not** apply to Razorpay create.

### 2.4 `ParseWebhookAsync` — HMAC then JSON

Order of operations in Hub:

1. Find header `X-Razorpay-Signature` (case-insensitive). Missing → `Verified=false`, error `"Missing X-Razorpay-Signature header."`
2. `Utils.verifyWebhookSignature(rawBody, signature, webhookSecret)` — **this is the official SDK**. HMAC-SHA256 of the raw JSON string with the webhook secret, hex compare. Throws on mismatch; Hub’s outer `catch` turns that into `Verified=false`.
3. `JsonDocument.Parse`. `event` via `GetProperty` (throws if missing → catch → not verified).
4. `payment.failed` → `MapPaymentFailed` (Verified **true**, EventType `PAYMENT_FAILED`, namespaced EventId). This is **not** “ignore” in Hub’s parse result; Hub’s cashier can mark failed. 015 tells Pay to **ignore** failed (no receipt). Steal the namespace idea, not the EventType enum.
5. Any other event including `invoice.expired` → `Verified=true`, EventType = raw string, **empty EventId**. Hub test `ParseWebhook_InvoiceExpired_IsIgnoredNotPaymentFailed` locks that `invoice.expired` is not mapped to `PAYMENT_FAILED`.
6. `payment.captured` only then walks `payload.payment.entity`.

Captured entity Hub reads:

- `id` → `GatewayTransactionId` (`pay_…`)
- `ResolveEventId(headers, "PAYMENT_COMPLETED", paymentId)`
- `amount` `GetDecimal() / 100m` → **major units**
- `fee` / `tax` `GetDecimal() / 100m` if present and non-null, else **0m**
- `netAmount = amount - fee`
- all `notes` into `Metadata`
- `customer_id` / `token_id` (vault leftovers; Pay has no vault)
- `TryReadCurrency` — missing → `Verified=false`, `"Missing payment currency; refusing to default to MYR."`, `AsUnusable()`

`TryReadCurrency` (Hub, Razorpay-local): property exists, `ValueKind == String`, non-whitespace, then `Trim().ToUpperInvariant()`. **No ISO length-3 check.** `"IN"` or `"MYRR"` would pass Hub and fail Pay `MoneyMath.TryNormalizeCurrency`.

Fee/tax: Hub **parses them into the result object**. Downstream `GatewayPaymentCompletedHandler` does:

```csharp
entry.AddLine(AccountTypes.AssetCash, @event.NetAmount, ...);
if (@event.GatewayFee > 0)
    entry.AddLine(AccountTypes.ExpenseGatewayFee, @event.GatewayFee, ...);
var grossRevenue = @event.AmountPaid - taxAmount;
entry.AddLine(AccountTypes.RevenueGross, -grossRevenue, ...);
if (taxAmount > 0)
    entry.AddLine(AccountTypes.LiabilityTaxPayable, -taxAmount, ...);
```

Razorpay’s webhook `tax` is **processor GST on MDR**, not Malaysian SST. Hub books it as `TaxAmount` / `LiabilityTaxPayable`. That is the category error 015 §4 named: “Razorpay’s webhook JSON has a `tax` field (processor GST on MDR). **Do not book it.** Same as fees: `unknown ≠ 0`.” Pay’s `PspParseResult` has no fee/tax fields. `RazorpayWebhook.Parse` never reads `tax` or `fee`. `Fulfillment` never sees them.

**Do not treat Hub `TaxAmount: 0` when the field is absent as “fee known zero.”** Hub uses 0 as the CLR default. 015 refuse list item 10: “Booking processor `tax` / `fee` as 0.” Pay avoids the field entirely.

### 2.5 `ResolveEventId`

```csharp
// Prefer header X-Razorpay-Event-Id if non-empty (trim).
// Else if paymentId blank → null (caller AsUnusable, no Guid.NewGuid).
// Else return mappedEventType + ":" + paymentId
//   captured path: "PAYMENT_COMPLETED:" + pay_
//   failed path:   "PAYMENT_FAILED:" + pay_
```

008 residual was: EventId = bare `pay_` (or worse, `Guid.NewGuid()` on missing). Fail then capture for the same payment collided on the unique webhook log, so the capture was dropped. Live Hub tests `ParseWebhook_FailThenCapture_WithoutHeader_UseDistinctEventIds` lock:

- failed → `PAYMENT_FAILED:pay_same`
- captured → `PAYMENT_COMPLETED:pay_same`
- both Verified, ids distinct, `GatewayTransactionId` still `pay_same`

[`docs/001-gaps/02-payment-webhooks.md`](../../docs/001-gaps/02-payment-webhooks.md) §4 still quotes the **pre-fix** tree (`EventId ?? Guid.NewGuid()`). That paragraph is **not** live Hub. Live Hub is `ResolveEventId` + unusable if both missing. 016 must quote the `.cs` file, not 001-gaps.

015 R19 **does not copy Hub’s prefix strings**. It names `captured:{pay_}` and `failed:{pay_}`. Pay follows 015, not `PAYMENT_COMPLETED:`. Same collision fix, different namespace tokens. That is intentional HTTP judgment, not a drift bug.

### 2.6 `ChargeOffSessionAsync` — dead pipe that still compiles

Hub still implements `Order.Create` + `Payment.CreateRecurringPayment` with `recurring: true`, `customer_id`, `token`. `PaymentGatewayCapabilities.SupportsOffSession("RAZORPAY")` is **false**, so the commerce dunning job is not supposed to publish (`PastDue_Razorpay_DoesNotPublish`). The method is the “dead pipe”: gravity sitting on the adapter because the port requires it. Comment: never invent `billing@lazuar.com`; email/phone only from notes (which this method’s own `notes` dictionary does **not** populate with email — so the live off-session path often posts **without** email/contact). 001-gaps still flags dummy contact; live code removed the dummy and may now post an incomplete recurring payload. Either way: **do not port**.

015 refuse list item 14: “Razorpay e-mandate / `ChargeOffSession`.” R23: no method on `RazorpayHosted`. `IHostedRail` cannot grow this verb without a new interface.

### 2.7 Refunds and portal

`IssueRefundAsync` uses SDK `Payment.Fetch(id).Refund(...)`. Hub `SupportsApiRefund("RAZORPAY")` is true. 015 parked-refunds: Pay does not implement refunds this program. Do not steal the SDK refund.

`GenerateCustomerPortalAsync` throws `"Razorpay does not provide a managed customer billing portal."` Pay has no portal route.

### 2.8 Hub test inventory (what Pay is **not** required to clone 1:1, but the **cases** are the HTTP contract)

| Hub test | HTTP contract |
|----------|----------------|
| `BuildPaymentLinkRequest_NeverMintsCardRegistration` | POST body is a payment link; no `subscription_registration` / `type` |
| `ParseWebhook_InvoiceExpired_IsIgnoredNotPaymentFailed` | Non-captured, non-failed events must not fulfill and must not be mapped as failed |
| `ParseWebhook_MissingSignature_IsNotVerified` | Missing `X-Razorpay-Signature` → reject (Pay: 400) |
| `ParseWebhook_CapturedWithoutHeaderAndPaymentId_IsNotVerified` | No Guid EventId; unusable |
| `ParseWebhook_HeaderEventIdAndPaymentId_MapsIdentities` | Header wins for EventId; `pay_` is provider ref; currency from entity |
| `ParseWebhook_PaymentFailed_MapsPaymentFailed` | Failed is a distinct verified event (Pay: ignore + namespaced id) |
| `ParseWebhook_CapturedWithoutCurrency_DoesNotInventMyr` | Missing currency → do not fulfill, currency is not `"MYR"` |
| `ParseWebhook_FailThenCapture_WithoutHeader_UseDistinctEventIds` | `failed:` vs `captured:` (Pay tokens) so capture still fulfills |

Pay’s `RailTests` covers **one** of those stories (captured + tax in JSON). The rest are unchecked on 8081.

---

## 3. Pay `RazorpayHosted` vs Hub create HTTP

Full class is 94 lines. It is the steal.

### 3.1 Type and DI

```csharp
public sealed class RazorpayHosted(PayDbContext db, SecretBox box, IHttpClientFactory http) : IHostedRail
{
    public const string ApiBase = "https://api.razorpay.com/v1/";
    public string Provider => PayProviders.Razorpay; // "razorpay"
```

- Lowercase provider string matches 015 lock. Hub’s `"RAZORPAY"` stays in the museum.
- Named client `"razorpay"` from `Program.cs` `AddHttpClient("razorpay")`. **No BaseAddress** on the client; `CreateHostedUrlAsync` uses the absolute URL `ApiBase + "payment_links"` → `https://api.razorpay.com/v1/payment_links`.
- Razorpay has **one** API host for test and live. Test vs live is `rzp_test_` / `rzp_live_` inside the key. `GatewayCredentialRow.Environment` is stored (default `"test"`) and **not** consulted for the URL. That is correct; Billplz is the rail that forks hosts.
- `IHostedRail` has no `ChargeOffSession`, no `SetupFutureUsage` flag, no parse. R10 / R23 / P27 hold in the type system.

### 3.2 Credential load and key split

```csharp
var cred = await db.GatewayCredentials.AsNoTracking()
    .FirstOrDefaultAsync(x => x.OrgId == checkout.OrgId && x.Provider == PayProviders.Razorpay, ct);
if (cred is null) throw new InvalidOperationException("rail not configured");
if (!BuyerEmail.IsUsable(checkout.PayerEmail)) throw new InvalidOperationException("email is required");
if (!TrySplit(box.Unprotect(cred.Ciphertext), out var keyId, out var keySecret))
    throw new InvalidOperationException("rail not configured");
if (!MoneyMath.TryNormalizeCurrency(checkout.Currency, out var currency))
    throw new InvalidOperationException("Currency is required.");
```

Start-path mapping (`PublicPayEndpoints`): `InvalidOperationException` whose message contains `"callback base"` → 400; **anything else including these three throws → 503**. Email is **also** gated earlier at Start:

```csharp
if (PayProviders.RequiresEmail(name) && !BuyerEmail.IsUsable(row.PayerEmail))
    return PayErrors.Status(400, "Bad Request", "email is required");
```

So a missing/placeholder email on `POST /v1/pay/{token}/start` is **400**, never 503, never a Razorpay HTTP call. The throw inside `RazorpayHosted` is defense in depth if someone calls the rail without the public gate.

Currency missing on the **checkout row**: 503 `"Currency is required."` Create currently defaults blank to MYR, so this throw is the length≠3 / whitespace path.

`TrySplit`:

```csharp
internal static bool TrySplit(string secret, out string keyId, out string keySecret)
{
    keyId = ""; keySecret = "";
    var i = secret.IndexOf(':');
    if (i <= 0 || i == secret.Length - 1) return false;
    keyId = secret[..i];
    keySecret = secret[(i + 1)..];
    return true;
}
```

| Input | Hub `GetClient` | Pay `TrySplit` |
|-------|-----------------|----------------|
| `rzp_test_abc:sk_live_1` | keyId `rzp_test_abc`, secret `sk_live_1` | same |
| `rzp_test_abc:ab:cd` | secret **`ab`** (rest dropped) | secret **`ab:cd`** |
| `rzp_test_abc` (no colon) | secret `""`, SDK constructed | **false** — PUT 400 / start 503, no HTTP |
| `:secret` | keyId `""` | **false** |
| `id:` | secret `""` | **false** |
| empty | keyId `""` | **false** (`i` is -1 or 0) |

R12 says: unprotect, split on **first** `:`, missing secret part → do not call Razorpay. Pay matches R12. Hub’s `parts[1]` is the weaker split. Steal Pay’s.

PUT (`GatewayEndpoints`) uses the same helper **before** Protect:

```csharp
if (provider == PayProviders.Razorpay && !RazorpayHosted.TrySplit(secret, out _, out _))
    return PayErrors.Status(400, "Bad Request", "secret must be key_id:key_secret");
```

Two input shapes (R11):

1. `secret` = `key_id:key_secret` (merchant UI does this client-side).
2. `key_id` + `key_secret` fields joined with `:` when `secret` is blank.

`webhook_secret` is required for **every** provider including Razorpay (400 `"webhook_secret is required"`). Razorpay dashboard webhook secret, not the API secret. Storing the API secret in both columns would verify HMAC with the wrong key.

`public_merchant_id`: `AllowsPublicMerchantId` is only chip/billplz. A Razorpay PUT with a Brand/Collection id is **400** `"public_merchant_id is not used for this provider"`. Hub has `merchantId` on `GenerateCheckoutAsync` and ignores it for Razorpay (no collection/brand). Correct reject.

last4 (R11 “document which”):

```csharp
var last4 = secret.Length >= 4 ? secret[^4..] : secret;
if (provider == PayProviders.Razorpay && RazorpayHosted.TrySplit(secret, out var keyId, out _))
    last4 = keyId.Length >= 4 ? keyId[^4..] : keyId;
```

So `secret = "rzp_test_hello:supersecret"` → last4 is last four of **key_id** (`ello`), not of the secret, not of the concatenated string. GET never echoes ciphertext (`GatewayJson` has `last4`, `webhook_configured`, `capability`). Isolation / S18 hold for this rail the same as others.

There is **no** GatewayTests method that PUTs `provider=razorpay`. R11.2 “PUT round-trip” and R12.2 “Helper unit test” are checklist claims without a dedicated test. `Razorpay_captured` does a PUT of `"rzp_test:secret"` as a fixture setup; it never asserts last4, never asserts reject of `public_merchant_id`, never asserts reject of `rzp_test` without a colon, never exercises the two-field join.

### 3.3 Payment-link HTTP body

```csharp
var payload = new Dictionary<string, object?>
{
    ["amount"] = MoneyMath.ToMinor(checkout.Amount),
    ["currency"] = currency,
    ["description"] = "Pay",
    ["customer"] = new { email = checkout.PayerEmail!.Trim(), name = BuyerEmail.NameFrom(checkout.PayerEmail, checkout.PayerName) },
    ["notes"] = new Dictionary<string, string>
    {
        ["checkout_id"] = checkout.Id,
        ["org_id"] = checkout.OrgId
    },
    ["callback_url"] = checkout.SuccessUrl ?? "http://localhost:5179/c/" + checkout.PublicToken + "?status=verifying",
    ["callback_method"] = "get"
};
```

Then:

```csharp
using var request = new HttpRequestMessage(HttpMethod.Post, ApiBase + "payment_links");
request.Headers.Authorization = new AuthenticationHeaderValue(
    "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(keyId + ":" + keySecret)));
request.Content = JsonContent.Create(payload);
```

This is the Hub SDK create, written as HTTP.

**Amount.** `MoneyMath.ToMinor` = `(long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero)`. Hub `ToMinorUnitsTruncating(amount, quantity)` is the same AwayFromZero ×100 ×qty with implied MYR factor. Pay has no `quantity` on the link: `checkout.Amount` is already the charge total. R13 “Amount minor units AwayFromZero” holds. Neither side uses true truncation. Zero-decimal currencies (JPY) are ×100 on both paths — residual if anyone ever charges JPY through this wrap; 015 did not ask for ISO exponent tables on Pay.

**Currency.** Already normalized to 3-letter uppercase. Sent as `"MYR"` / `"INR"` etc. No silent MYR inside this method.

**Description.** Hub sends the product name. Pay hard-codes `"Pay"`. Honest enough for a wrap; not a money bug. Not tested.

**Customer.** Hub always sent `customer.email` + `customer.name`. Pay does the same. R24: because Hub sent a customer block, Pay requires email (400 at Start; 503 if the rail is invoked anyway). `BuyerEmail.NameFrom` is Hub `ExtractName` plus an explicit `PayerName` when the buyer typed one. No `contact` / phone — Hub only sent contact when metadata had `customer_phone`. Pay has no phone field on start. Hub test asserts customer does not contain `contact` in the no-phone case; Pay matches.

**Notes.** Hub dumped **all** commerce metadata (`subscription_id`, `tenant_id`, …). Pay sends exactly `checkout_id` and `org_id`. Fulfill reads `notes.checkout_id`. That is the join 015 named. Extra Hub notes were for dunning/off-session, which Pay does not run.

**Callback.** GET, browser success, default `:5179` verifying poll. Matches checkout frontend `verifyingQuery()`. Localhost is **legal** here (unlike Billplz `callback_url`). Merchant create-checkout does not set `SuccessUrl`, so dogfood always hits the default.

**Auth.** `Authorization: Basic base64(key_id:key_secret)`. R12.1. FakePspHandler does not assert the header. The test would pass if Pay sent Bearer.

**No registration keys.** The dictionary has no `subscription_registration`, `type`, `max_amount`, `setup_future_usage`, `recurring`, `token`. R15 / R22 hold in source. **Not locked by LastBody asserts** (Chip asserts `LastBody` does not contain `force_recurring`; Razorpay asserts nothing about the outbound JSON).

**No Idempotency-Key.** A retried Start mints a second `plink_` and overwrites `checkout.ProviderSessionId`. Buyer might still pay the first link; notes still carry `checkout_id`, so the webhook still fulfills once. Two live links for one checkout is an operational residual, same class as other rails.

### 3.4 Response handling

```csharp
if (!response.IsSuccessStatusCode)
    throw new InvalidOperationException("Razorpay rejected the org key");
var url = ... "short_url" ...
var id = ... "id" ...
if (string.IsNullOrWhiteSpace(url))
    throw new InvalidOperationException("Razorpay returned no URL");
return new HostedSession(url, id);
```

- Non-2xx (bad key, INR account vs MYR amount, validation) → 503 with a **key-shaped** message even when the body is a currency error. R20 says a MYR checkout against an INR Razorpay account fails at **start** (API error → 503) and must not be laundered in the webhook. The message is slightly dishonest (“org key”) but the status is right.
- Missing `short_url` → 503. R13.
- Missing `id` with a URL still redirects. `ProviderSessionId` may be null. 015 wanted `plink_…` persisted (`s15`). Weak: a 200 with URL and no id is accepted. Not tested.

`PublicPayEndpoints.Start` then writes `row.Provider = "razorpay"`, `PspRedirectUrl`, `ProviderSessionId`. Charge later stores `ProviderRef = pay_` from the webhook, not the `plink_`. That matches Hub `GatewayTransactionId = paymentId`.

### 3.5 What Pay correctly did **not** steal from create

- `RazorpayClient` / `PaymentLink.Create`
- `setupFutureUsage` argument (the port does not have it)
- quantity / product description / phone
- metadata dump
- wrapping failures as `GatewayCheckoutResult(false)` (Pay uses exceptions + HTTP status)

---

## 4. Pay `RazorpayWebhook` vs Hub parse

Full class is 109 lines. Parse is a static function next to the route, not a method on `IHostedRail`. Fulfill stays in `WebhookEndpoints` + `Fulfillment` (015 refuse item 17).

### 4.1 HMAC of **raw body** (R16)

```csharp
if (string.IsNullOrWhiteSpace(cred.WebhookCiphertext))
    throw new InvalidOperationException("webhook secret missing"); // → 503

var sigKey = headers.Keys.FirstOrDefault(k => k.Equals("X-Razorpay-Signature", StringComparison.OrdinalIgnoreCase));
if (sigKey is null || !headers.TryGetValue(sigKey, out var signature) || string.IsNullOrWhiteSpace(signature))
    throw new PspVerifyException("invalid signature"); // → 400

var secret = box.Unprotect(cred.WebhookCiphertext);
var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(raw));
var expected = Convert.ToHexString(mac).ToLowerInvariant();
var provided = signature.ToString().Trim().ToLowerInvariant();
var left = Encoding.UTF8.GetBytes(provided);
var right = Encoding.UTF8.GetBytes(expected);
if (left.Length != right.Length || !CryptographicOperations.FixedTimeEquals(left, right))
    throw new PspVerifyException("invalid signature"); // → 400

using var doc = JsonDocument.Parse(raw); // only after HMAC
```

This is Hub `Utils.verifyWebhookSignature` without importing `Razorpay.Api`. R16.2 “Do not use Razorpay.Api `Utils` (that **is** the SDK). Implement HMAC yourself (like Billplz).”

Contract details:

- Header name case-insensitive. Razorpay docs use `X-Razorpay-Signature`.
- Input to HMAC is the **raw request body string** as `WebhookEndpoints` read it (`StreamReader` UTF-8, no model bind). Not a re-serialized JSON, not form HMAC, not RSA (CHIP), not `x-callback-token` (Xendit), not Stripe `t=v1`.
- Key is the **webhook** secret from `WebhookCiphertext`, not `Ciphertext` (API key). Mixing them would 400 every real delivery.
- Hex, lowercased, trimmed. Official SDK compare is typically case-sensitive hex; Pay lowercases both sides, so uppercase hex from a proxy still verifies. Fine.
- Compare is constant-time on the **hex character bytes**, after an explicit length check. Same pattern as Xendit’s token compare. Length mismatch fails closed (length leak of hex size only).
- **Then** JSON parse. R16.1. A forged body never reaches `event` matching.
- Missing webhook ciphertext on a configured row: 503, not 400. 015 “missing org creds 400” is the **no row** path (`rail not configured`). Secret-on-row-missing is the Stripe-shaped 503 (`WebhookTests.Missing_webhook_secret_is_503_when_rail_configured` is Stripe-only; Razorpay uses the same catch).

`JsonDocument.Parse` throwing `JsonException` after a **valid** HMAC is **not** mapped to `PspVerifyException`. `WebhookEndpoints` only catches `PspVerifyException` → 400 and webhook-secret `InvalidOperationException` → 503. Garbage JSON with a good signature is an unhandled 500. Hub’s outer `catch (Exception)` turned that into `Verified=false`. Residual: rare (Razorpay sends JSON), but it is a 500 after verify, which 015 refuse item 16 was about for **signature** fail, not parse. Still worth naming: do not copy Hub’s “everything is Verified=false”.

`GetInt64()` on `amount` if Razorpay ever sent `1000.0` as a JSON number that is not a raw integer can throw `JsonException` → same 500. Hub used `GetDecimal() / 100m`, more lenient. Live Razorpay payment amounts are integer paise.

### 4.2 Event switch: failed vs captured vs other

```csharp
var eventType = doc.RootElement.TryGetProperty("event", out var ev) ? ev.GetString() : null;
// payload.payment.entity via TryGetProperty (no throw)
var paymentId = hasEntity && entity.TryGetProperty("id", ...) ? pid.GetString() : null;
var headerEventId = Header(headers, "X-Razorpay-Event-Id"); // trim, empty → null

if (eventType == "payment.failed")
{
    var failedId = headerEventId ?? (string.IsNullOrWhiteSpace(paymentId) ? null : "failed:" + paymentId);
    if (string.IsNullOrWhiteSpace(failedId))
        throw new PspVerifyException("missing event id");
    return new PspParseResult { EventId = failedId, Ignored = true, IgnoreReason = "payment_failed" };
}

if (eventType != "payment.captured")
{
    var otherId = headerEventId ?? eventType ?? "razorpay";
    return new PspParseResult { EventId = otherId, Ignored = true, IgnoreReason = eventType };
}
```

**`payment.failed` (R18).** Ignored. 200 `{ ignored: "payment_failed" }` after unique insert (`WebhookEndpoints` insert-on-ignore). **Does not fulfill.** Documents stay 0. Hub mapped this to `PAYMENT_FAILED` Verified=true for commerce; Pay does not have that plane. Namespace: header if present, else `failed:{pay_}`. Never bare `pay_`. Missing both header and payment id → 400 unusable (Hub `AsUnusable`). Later `payment.captured` for the same `pay_` uses `captured:{pay_}` or a **different** header Event-Id, so the unique key `(org, razorpay, event_id)` does not collide and capture can still fulfill. That is R18.1 / R19. **There is no Pay test that posts `payment.failed`.** R18.2 Exit “Test ignore” is a checked box over empty evidence. R25.1 “`payment.failed` ignore” is the same lie.

**Other events (`order.paid`, `payment.authorized`, `invoice.expired`, missing `event`).** Ignored. R17.2: do not fulfill `order.paid` unless A00 amended. Hub returned Verified=true with **empty** EventId for these; Pay **must** supply an EventId because `PspWebhookEventRow` PK is `(OrgId, Provider, EventId)`. Pay uses `headerEventId ?? eventType ?? "razorpay"`.

This other-event id is **weaker than CHIP/Xendit**:

- CHIP: `(eventType ?? "chip") + ":" + purchaseId` — per object.
- Xendit: `status + ":" + invoiceId`.
- Razorpay other: **event type only**, no `pay_` / `plink_`.

Two `payment.authorized` deliveries for **different** payments without `X-Razorpay-Event-Id` share EventId `"payment.authorized"`. Second is `{ duplicate: true }` still ignored, so **no double fulfill**. Unique log is wrong; harmless for money. Missing `event` after a valid HMAC collapses to EventId `"razorpay"` for every such body — same class of log collision. Header Event-Id, when Razorpay sends it, fixes this. Not tested.

**`payment.captured` only then continues.** Not `order.paid`. Not `payment.authorized`. Not invoice events.

### 4.3 Captured entity (R17, R19, R20, R21)

```csharp
if (!hasEntity || string.IsNullOrWhiteSpace(paymentId))
    throw new PspVerifyException("missing payment id");

if (!MoneyMath.TryNormalizeCurrency(
        entity.TryGetProperty("currency", ...) ? cur.GetString() : null,
        out var currency))
    throw new PspVerifyException("missing currency");

var amount = entity.TryGetProperty("amount", out var amt) && amt.ValueKind == JsonValueKind.Number
    ? amt.GetInt64()
    : 0L;

// notes.checkout_id
var eventId = !string.IsNullOrWhiteSpace(headerEventId) ? headerEventId : "captured:" + paymentId;
return new PspParseResult {
    EventId = eventId,
    CheckoutId = checkoutId,
    ProviderRef = paymentId,
    AmountMinor = amount,
    Currency = currency
};
```

**Event id (R19).** Header `X-Razorpay-Event-Id` wins if non-whitespace after trim. Else `captured:{paymentId}`. Never checkout id. Never bare `pay_`. Captured **without** payment id throws even if a header is present — slightly stricter than Hub (`ResolveEventId` can return the header alone). Fail-closed. **No Pay test sends `X-Razorpay-Event-Id`.** `Razorpay_captured` omits the header, so the live path that prefers the header is untested; the fallback `captured:pay_1` is the only EventId the fixture produces, and the test never **asserts** the stored `PspWebhookEvents.EventId`.

**Checkout id.** `payload.payment.entity.notes.checkout_id` only. Hub put the whole metadata bag in notes at create; Pay puts `checkout_id` + `org_id`. Razorpay copies payment-link notes onto the payment entity for this flow (Hub’s assumption; 001-gaps still says “usually true; not guaranteed for all flows”). A dashboard payment with no notes → `CheckoutId` null → `WebhookEndpoints` 400 `"checkout not found"` **without** inserting unique. Razorpay will retry. That is fail-closed for money (no Guid, no random fulfill) and noisy operationally. Same class as other rails’ missing join.

**Amount.** Stay in **minor** (`GetInt64`). Hub divided by 100 into major. `WebhookEndpoints` compares `parsed.AmountMinor` to `MoneyMath.ToMinor(checkout.Amount)`. Fixture: checkout `amount: 10` → 1000; webhook `"amount": 1000` → match. Missing amount → 0L → amount mismatch 400 (checkout is > 0). H14 holds in the shared handler.

**Currency (R20).** `MoneyMath.TryNormalizeCurrency`: reject null/whitespace/non-3-letter. **Does not default MYR.** 400 `"missing currency"` via `PspVerifyException`. After parse, `WebhookEndpoints` also 400s `"currency mismatch"` if entity currency ≠ `checkout.Currency` (ordinal ignore case). Create defaults omitted currency to MYR; merchant UI hard-codes MYR. An INR Razorpay account capturing INR against a MYR checkout is 400 mismatch, not a silent MYR fulfill. Hub `TryReadCurrency` is weaker (any non-empty string). Pay is stricter. **There is no Pay fixture without currency.** R20.2 Exit is a checked box over empty evidence. Hub **does** have `ParseWebhook_CapturedWithoutCurrency_DoesNotInventMyr`.

**Tax / fee (R21).** Not read. `PspParseResult` cannot carry them. Fixture in `Razorpay_captured` **includes** `"tax":12,"fee":30`. See §9 for what the test actually asserts.

**Provider ref.** `pay_…`, passed to `FulfillPaidAsync(checkout.Id, "razorpay", paymentId)`. Charge row stores that, not `plink_`.

### 4.4 Shared webhook handler behavior that Razorpay inherits

From `WebhookEndpoints.Handle`, after parse:

1. Empty body 400 **before** cred lookup. Shared. Tested on stripe (`PublicPayTests.Empty_webhook_is_400`) and chip (`RailTests.Chip_empty_body_400`). Not on `/v1/webhooks/razorpay/...`. The code path is the same function; a razorpay-specific empty-body test would only lock the route string.
2. Unknown provider 400 (`WebhookTests.Unknown_provider_is_400` uses `paypal`).
3. No cred row → 400 `"rail not configured"`. Untested for razorpay.
4. Duplicate `(orgId, provider, eventId)` → 200 `{ duplicate: true }` **before** ignore/fulfill. Chip replay asserts this. Razorpay captured **does not replay**.
5. `parsed.Ignored` → insert unique (swallow unique violation) → 200 `{ ignored: reason }`. Failed would hit this. Untested.
6. Missing checkout id / wrong org → 400, **no** unique insert (retry storm). H13 org bind is the `checkout.OrgId != orgId` branch. Stripe has `Cross_org_checkout_is_400`. Razorpay does not.
7. Currency mismatch / amount mismatch → 400, no unique insert.
8. Begin transaction: insert event row + `FulfillPaidAsync` + commit. Unique violation → 200 duplicate. In-memory tests ignore transactions (`InMemoryEventId.TransactionIgnoredWarning`); H12 is real on Postgres, not on `PayApiFactory`.

Signature fail is 400, not Hub’s 500. That steal is correct and **untested on the razorpay route**.

---

## 5. Fulfillment, tax JSON, two-line journal (R21)

`Fulfillment.FulfillPaidAsync` (all rails, including Razorpay):

- No-op if checkout missing, `Amount <= 0`, or status not `"open"`.
- Status → `paid`. Charge amount = **`checkout.Amount`**, not parsed minor, not `amount - fee`, not `tax`.
- Journal: one entry, **exactly two lines** — `cash` D `checkout.Amount`, `revenue` C `checkout.Amount`.
- Document title `"Official Receipt"`, number `RCPT-{MYT year}-{n}`. Not Tax Invoice, not VALID.
- Recurring `Interval` `mo`/`yr` still inserts a local `SubscriptionRow`. That is **not** e-mandate. Renewal still needs another hosted link. Capability remains `hosted_link`.

R21.1: “Entity may contain `tax` and `fee` — **do not** add journal lines. Two-line cash/revenue for **checkout.Amount** only. Do not port Hub `TaxAmount` into Fulfillment.”

Live source: holds. `RazorpayWebhook` does not mention `tax` or `fee`. `PspParseResult` has no such properties. `Fulfillment` has no third line.

Hub contrast (do not copy): `GatewayPaymentCompletedHandler` treats Razorpay `tax` as `LiabilityTaxPayable` and `fee` as `ExpenseGatewayFee`, and cash as **net**. That is processor GST + MDR booked as if they were our SST/fee truth. 015 tax-out + `unknown ≠ 0`.

---

## 6. Email (R24) and `:5179`

Hub `BuildPaymentLinkRequest` always sends `customer.email`. Payment Links API accepts a customer object; Hub treated it as required in practice.

Pay:

- `PayProviders.RequiresEmail` is `provider is not Stripe` → **razorpay requires email**.
- `GET /v1/pay/{token}` sets `email_required` from that flag once `active_provider` / `checkout.Provider` is razorpay.
- Checkout Vite blocks Start when `email_required && !email.trim()`.
- Start 400 if `!BuyerEmail.IsUsable` (blank or `customer@example.com`).
- `RazorpayHosted` sends trimmed email + `NameFrom`.

Placeholder: same string as Hub `GatewayCommon.PlaceholderEmail`. 015 refuse item 13 listed CHIP/Billplz/Xendit; R24/P20 extend it to Razorpay because the customer block exists. **No Pay test** uses `customer@example.com` on razorpay (grep of tests for `placeholder` / `customer@example.com` is empty). `Chip_start_without_email_is_400` exists; no `Razorpay_start_without_email_is_400`. The Start gate is shared, so chip’s test is weak evidence the razorpay **name** is in `RequiresEmail` — that boolean is one line in `PayProviders.cs` and is true.

---

## 7. No e-mandate / no off-session / no SDK (R14, R15, R22, R23)

| Hub gravity | Pay |
|-------------|-----|
| `using Razorpay.Api`; package `Razorpay` 3.3.2 | Host csproj: Stripe.net only |
| `IPaymentGatewayAdapter` + factory DI | Switch of known names in Start + WebhookEndpoints |
| `setupFutureUsage` discarded, still a parameter | Parameter does not exist |
| Registration-link history (`max_amount` 10×) | No such keys in payload |
| `ChargeOffSessionAsync` compiles and would hit `CreateRecurringPayment` | No method; `IHostedRail` cannot express it |
| `SupportsEmandate` false; old ops copy lied “MY e-mandate” | Merchant copy: **“Not e-mandate. We do not auto-debit.”** |
| `SupportsApiRefund` true | Refunds parked; no Pay refund client |
| `GenerateCustomerPortalAsync` throws | No route |

IsolationTests:

1. `BannedSrc` includes `"Razorpay.Api"` — every `src/**/*.cs` is grepped. A `using Razorpay.Api;` fails the test. R14 + H21.
2. `No_csproj_references_apps_lazuar_api` also greps **every** `*.csproj` under `apps/lazuar-pay` for the substring `Razorpay.Api`.

**Lock hole:** Hub’s package id is `Razorpay`, not `Razorpay.Api`. A well-meaning `<PackageReference Include="Razorpay" />` in `Lazuar.Pay.csproj` would **not** trip the csproj grep. It would trip the source grep only after someone writes `using Razorpay.Api`. R14.1 “IsolationTests may grep `Razorpay.Api` in csproj” matches what was written, and is incomplete versus the actual NuGet id. Mention for 09-tests-inventory / 10-honesty: consider also banning `"Include=\"Razorpay\""` or the token `Razorpay` as a PackageReference. Not a current ship bug — the package is absent.

No `ChargeOffSession` string in `RazorpayHosted.cs`. Parked-offsession still parked. R23 holds in source.

Capability GET: `PayProviders.Capability` is the constant `"hosted_link"` for every rail including razorpay. No `vaulted`, no `emandate`.

---

## 8. `RailTests.Razorpay_captured` — what it actually locks

The entire Razorpay behavioral test, quoted in substance:

1. Fake One owner.
2. Fake PSP HTTP **always** 200 `{"id":"plink_1","short_url":"https://rzp.io/i/x"}` regardless of method/path/body.
3. PUT `{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh_rzp"}`.
4. Create checkout `{org_id, amount:10}` → default currency **MYR**.
5. Start with `email: ada@acme.test` — asserts **only** `started.IsSuccessStatusCode`.
6. Webhook body:

```json
{"event":"payment.captured","payload":{"payment":{"entity":{"id":"pay_1","amount":1000,"currency":"MYR","tax":12,"fee":30,"notes":{"checkout_id":"<id>"}}}}}
```

7. HMAC-SHA256(UTF8(`wh_rzp`), UTF8(payload)) hex lowercase as `X-Razorpay-Signature`. **No** `X-Razorpay-Event-Id`.
8. POST `/v1/webhooks/razorpay/t1` → 200.
9. `Documents.Count() == 1`.
10. `JournalLines.Count() == 2`.

### 8.1 Does RailTests assert journal 2 lines?

**Yes.** Line 243: `Assert.That(db.JournalLines.Count(), Is.EqualTo(2));` and the captured payload **does** include `"tax":12,"fee":30`. That is R21.2’s exit sentence almost verbatim: “Paid test journal still two lines even if fixture includes `"tax": 12`.”

What that assert **does not** prove:

| Claim in R21.1 | Locked? |
|----------------|---------|
| Two lines (not 3/4) | **Yes** — `Count() == 2` |
| Accounts are `cash` / `revenue` | **No** |
| Amounts equal `checkout.Amount` (10), not net 10−0.30, not 12 | **No** |
| Debit sum == credit sum | **No** (Chip test does this) |
| Document number starts with `RCPT-` | **No** (Chip and Stripe tests do this) |
| Document title Official Receipt | **No** |
| `tax`/`fee` JSON keys are unread | Only indirectly: extra lines would fail the count. A two-line journal of **net** would still pass. Live `Fulfillment` uses `checkout.Amount`, so source is correct; the test is weaker than the checklist sentence “checkout.Amount only”. |

Chip’s paid test is the stronger money lock (`RCPT-` prefix + D sum == C sum + replay duplicate). Razorpay copied the tax fixture into the body and then only counted lines.

### 8.2 What the same test does **not** lock (HTTP)

- `factory.Psp.LastUri` contains `https://api.razorpay.com/v1/payment_links`. The fake responder ignores the request. A regression that POSTed `/v1/invoices` or `/v1/orders` would still 200 the Start. Billplz at least asserts `LastUri` contains `billplz-sandbox`. Razorpay asserts neither URI nor Basic auth nor `LastBody`.
- Outbound JSON has `notes.checkout_id` / `org_id`, `callback_method=get`, `customer.email`, currency, amount 1000.
- Outbound JSON **lacks** `subscription_registration`, `type`, `max_amount`, `setup_future_usage`. Chip locked the analogous CHIP lie (`force_recurring`). Razorpay did not.
- Redirect body `{ redirect_url: "https://rzp.io/i/x" }`.
- Checkout `Provider == razorpay`, `ProviderSessionId == plink_1`.

### 8.3 What the same test does **not** lock (webhook)

- Replay / duplicate 200.
- `X-Razorpay-Event-Id` preferred over `captured:pay_1`.
- Stored EventId is not bare `pay_1`.
- Bad signature 400.
- Missing signature 400.
- `payment.failed` 200 ignored, Documents 0, then captured still pays.
- Missing currency 400, Documents 0, currency not invented MYR.
- `order.paid` / `payment.authorized` ignored.
- Amount mismatch 400.
- Currency mismatch (INR capture vs MYR checkout) 400.
- Cross-org 400.
- Empty body on the **razorpay** URL (shared handler, stripe/chip already cover empty).

---

## 9. IsolationTests Razorpay.Api ban (opened in full)

`IsolationTests` is not Razorpay-specific except two tokens.

`BannedSrc` (grepped in every `apps/lazuar-pay/src/**/*.cs`):

```
MediatR, Modules.One, BuildingBlocks, IPaymentGatewayAdapter, PaymentGatewayFactory,
IPaymentGatewayFactory, AddPaymentsModule, GatewayPaymentCompletedIntegrationEvent,
Modules.Payments, ApplicationFeeAmount, Razorpay.Api
```

`RazorpayHosted.cs` and `RazorpayWebhook.cs` do not contain `Razorpay.Api`. HMAC is `System.Security.Cryptography.HMACSHA256`. HTTP is `HttpClient`. This is the R14 lock that matters.

Csproj loop: host + tests + any other csproj under `apps/lazuar-pay` must not contain `apps/lazuar-api` or `Razorpay.Api`. Live `Lazuar.Pay.csproj` PackageReference set is EF Design, Npgsql, **Stripe.net**. Live test csproj has no Razorpay package.

`Banned` for host/test csproj text also includes `lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api` — factory gravity.

H21.2 “Do not add `Razorpay.Api` (R14)” is checked and **source-true**. The NuGet-id hole is §7.

---

## 10. 015 R10–R25 vs live evidence

Checklists are 100% `[x]`. Live evidence is not.

| Phase | Claim | Source at this SHA | Test at this SHA |
|-------|-------|--------------------|------------------|
| **R10** class, HttpClient, `short_url`, no SDK, no ChargeOffSession | True | `RazorpayHosted` + `IHostedRail` + csproj | IsolationTests (SDK string); no compile-only test beyond the solution build |
| **R11** PUT `key_id:key_secret` or two fields, webhook secret, reject public_merchant_id, last4 of key_id, active_provider, writer | True in `GatewayEndpoints` | Two-field join + `TrySplit` 400 + last4 of key_id | **No** razorpay PUT test. Writer test is Stripe. |
| **R12** first-colon split, incomplete → no HTTP, Basic auth | True in `TrySplit` + hosted | PUT 400 / start 503 | **No** helper unit test. Fixture `"rzp_test:secret"` only. |
| **R13** `POST https://api.razorpay.com/v1/payment_links`, minor AwayFromZero, notes, callback GET, read `short_url`+id, missing URL 503 | True in hosted | Mocked start succeeds | Does **not** assert URI/body/id. R13.3 “Mocked start test” is a green Start, not an HTTP contract test. |
| **R14** no `Razorpay.Api` package | True (package id `Razorpay` also absent) | IsolationTests | **Yes** (string ban). Hole vs NuGet id `Razorpay`. |
| **R15** discard SetupFutureUsage; always payment link | True — flag does not exist; payload has no registration keys | — | **No** LastBody negative assert |
| **R16** HMAC raw body, missing/invalid → 400, then JSON | True in `RazorpayWebhook` + `WebhookEndpoints` | Captured uses a **good** sig | **No** bad-sig razorpay test. Stripe `Invalid_signature_is_400` is a different algorithm. |
| **R17** `payment.captured` → FulfillPaid, notes checkout id, H13/H14, no fee/tax book | True | Captured → 1 document | No `RCPT-` prefix assert; no org-bind/amount-mismatch razorpay cases |
| **R18** `payment.failed` 200 ignore; namespace so capture still pays | True in parse + insert-on-ignore | **No test** | R18.2 “Test ignore” is **false** as evidence |
| **R19** header Event-Id else `captured:`/`failed:`; missing both 400; not checkout id | True in parse | Captured omits header (fallback path only) | Does not assert EventId value; no header fixture; no fail+capture collision fixture |
| **R20** missing currency do not default MYR; must match checkout | True (`TryNormalizeCurrency` + mismatch 400) | **No test** | R20.2 “Fixture without currency” is **false** as evidence. Hub still has this test. |
| **R21** ignore JSON tax/fee; two-line cash/revenue | True in source; fixture includes tax/fee | **`JournalLines.Count() == 2`** | Count only; not amounts/accounts/`RCPT-` |
| **R22** no mandate variants; merchant copy not “e-mandate”; capability hosted_link | True in payload + `WorkspacePage` copy + `PayProviders.Capability` | No payload assert; no UI test | Copy is source-true |
| **R23** no ChargeOffSession on hosted | True (`IHostedRail`) | — | Vacuous (no method to call) |
| **R24** require email; include name | True Start + hosted + `:5179` `email_required` | Chip missing-email 400 only | No razorpay missing-email / placeholder test |
| **R25** hermetic must-exist list | Partial | See table below | R25.1 over-checked |

### R25.1 must-exist vs files

| R25.1 item | Exists? | Where |
|------------|---------|-------|
| Empty body 400 | Shared handler only | `PublicPayTests.Empty_webhook_is_400` (stripe URL), `RailTests.Chip_empty_body_400` (chip URL). **Not** `/v1/webhooks/razorpay/{org}`. |
| Bad signature 400 | **No** for HMAC-hex Razorpay | Stripe `WebhookTests.Invalid_signature_is_400` is `t=v1`. |
| `payment.captured` → `RCPT-` + replay | **Half.** Captured → 1 document. **No** `RCPT-` prefix. **No** replay. | `Razorpay_captured` |
| `payment.failed` ignore | **No** | — |
| Fixture with `tax` still two journal lines | **Yes** | `Razorpay_captured` `JournalLines.Count() == 2` with `"tax":12,"fee":30` |
| Mocked payment_links → `short_url` | **Weak.** Start 2xx; fake always returns `short_url`; URI not asserted | `Razorpay_captured` |
| No `Razorpay.Api` in csproj | **Yes** | `IsolationTests.No_csproj_references_apps_lazuar_api` |

R25.2 “`task pay:test` green” can be true while R25.1 is a lie: the missing tests were never written, so they cannot fail.

---

## 11. Side-by-side HTTP dictionary (steal this, not the SDK)

### Create `POST /v1/payment_links`

| Field | Hub `BuildPaymentLinkRequest` + SDK | Pay `RazorpayHosted` |
|-------|-------------------------------------|----------------------|
| URL | SDK → `https://api.razorpay.com/v1/payment_links` | Same, written out |
| Auth | SDK Basic from `Split(':')` | Basic `base64(keyId:keySecret)` after `TrySplit` |
| `amount` | paise AwayFromZero × qty | paise AwayFromZero of `checkout.Amount` |
| `currency` | `ToUpperInvariant()` | `TryNormalizeCurrency` (len 3) |
| `description` | product name | `"Pay"` |
| `customer.email` | cashier email | required usable email |
| `customer.name` | metadata or local-part | `PayerName` or local-part |
| `customer.contact` | if `customer_phone` | omitted |
| `notes` | all metadata | `checkout_id`, `org_id` |
| `callback_url` | successUrl | `SuccessUrl` or `:5179/c/{token}?status=verifying` |
| `callback_method` | `get` | `get` |
| registration / mandate keys | none (discarded) | none |
| Response | `short_url`, `id` | `short_url` required, `id` optional |

### Verify

| | Hub | Pay |
|---|-----|-----|
| Header | `X-Razorpay-Signature` | same |
| Algorithm | SDK `Utils.verifyWebhookSignature` HMAC-SHA256 hex | `HMACSHA256.HashData` hex, fixed-time |
| Body | raw string | raw string from `request.Body` |
| Secret | method arg `webhookSecret` | `SecretBox.Unprotect(WebhookCiphertext)` per org |
| Fail | `Verified=false` (Hub endpoint historically 500) | `PspVerifyException` → **400** |

### Fulfill / ignore

| Event | Hub parse | Pay parse | Pay HTTP |
|-------|-----------|-----------|----------|
| `payment.captured` | `PAYMENT_COMPLETED`, EventId header or `PAYMENT_COMPLETED:{pay_}` | EventId header or `captured:{pay_}` | fulfill in TX |
| `payment.failed` | `PAYMENT_FAILED`, header or `PAYMENT_FAILED:{pay_}` | Ignored, header or `failed:{pay_}` | 200 ignored, unique inserted |
| other | Verified, empty EventId | Ignored, EventId = header or event name | 200 ignored |
| missing currency on captured | unusable, no MYR | 400 `missing currency` | no document |
| `tax` / `fee` | parsed, booked downstream | unread | two lines of `checkout.Amount` |

---

## 12. Residuals and ranked honesty (Razorpay-shaped)

These are not “015 forgot the class.” The class is on 8081. These are the remaining holes after reading both trees.

1. **Checklist theater on R18 / R20 / R25.** Failed-ignore, missing-currency, bad HMAC, replay, `RCPT-` prefix, EventId value, HTTP path — marked done, not in `Lazuar.Pay.Tests`. Hub still has the better unit tests for EventId collision and missing currency. If 09-tests-inventory needs a write-list, start here.
2. **`JournalLines.Count() == 2` is necessary and not sufficient for R21.** Live fulfillment is two lines of `checkout.Amount`. A future “helpful” net booking (`amount - fee`) would still pass the Razorpay test. Chip’s D==C + `RCPT-` is the pattern to copy when tests are written.
3. **Outbound payment_links HTTP is unasserted.** Fake PSP returns `short_url` for any URL. The one thing that distinguishes this rail from an invoice wrap is the path `.../v1/payment_links` plus the payload keys. Not locked.
4. **Other-event EventId without object id.** Harmless for money, sloppy for the unique log. CHIP/Xendit namespace with the object id. Prefer header; if absent, `"{event}:{pay_}"` or `"{event}:none"` would match R19’s spirit more closely than bare `eventType`.
5. **IsolationTests csproj token `Razorpay.Api` vs NuGet id `Razorpay`.** Source grep still saves you once `using Razorpay.Api` appears. A package-only add would not fail csproj isolation.
6. **Merchant/create hard-codes MYR.** R20’s “INR account fails at start” is the honest failure mode for Indian keys. `:5178` has no currency picker. Weakest MY dogfood, as 015 §5.4 said; do not advertise “we launched in India.”
7. **Missing `checkout_id` / amount mismatch / currency mismatch → 400 without unique insert.** Correct fail-closed (no Guid). Razorpay retries until the dashboard gives up. Same as other Pay rails.
8. **JSON parse / `GetInt64` throw after good HMAC → 500.** Narrow. Do not “fix” by catching-all into 200.
9. **`plink_` id optional.** URL without id still starts. Persist empty `ProviderSessionId`.
10. **Hub `ChargeOffSession` / refund / `TaxAmount` must stay uncopied.** They still exist in `lazuar-api`. A later “while we are here” port would reintroduce e-mandate-shaped recurring and processor GST as SST. `IHostedRail` is the fence.
11. **EventId prefix strings differ from Hub** (`captured:` vs `PAYMENT_COMPLETED:`). 015 R19 chose Pay’s tokens. Do not “align” back to Hub’s cashier enum; there is no `PAYMENT_COMPLETED` on 8081.
12. **`docs/001-gaps/02` Razorpay `Guid.NewGuid()` paragraph is stale** versus live Hub `ResolveEventId`. Quote this SHA’s `.cs` files.

---

## 13. What is actually good (so the gaps are not the whole story)

Pay did the HTTP extract 015 asked for:

- Payment **links** over HttpClient to `https://api.razorpay.com/v1/payment_links`, Basic `key_id:key_secret`.
- Key split on first colon, refused incomplete keys at PUT (400) and at create (503).
- HMAC-SHA256 of **raw body** with per-org webhook secret, then JSON. No `Razorpay.Api`.
- Fulfill **only** `payment.captured`. Failed and everything else 200 ignore.
- Event ids namespaced (`captured:` / `failed:`) or Razorpay’s delivery header — never bare `pay_`.
- Currency fail-closed, no MYR invention in the webhook parser.
- Email required; placeholder unusable; customer block on the link.
- JSON `tax`/`fee` not booked; fulfillment is two-line Official Receipt of `checkout.Amount`.
- No e-mandate payload, no off-session method, merchant copy says so, capability `hosted_link`.
- IsolationTests ban the SDK namespace in source.

That is a labelled reminder wrap for merchants who already have Razorpay keys. It is not Hub’s adapter, and it must not become one.

The hole is **tests versus the R25 list**, not the create/verify/fulfill shape. `Razorpay_captured` is the only behavioral test; it does assert **two journal lines** on a tax-bearing captured fixture; it does not assert failed-ignore, missing currency, bad HMAC, replay, `RCPT-`, EventId, or `payment_links` URI.

---

## 14. Pointers for sibling papers

- **09-tests-inventory:** Razorpay row = IsolationTests SDK ban + `RailTests.Razorpay_captured` (start 2xx, captured 200, documents 1, journal count 2, tax in fixture). Write-list = R25.1 minus those two. Hub `RazorpayGatewayAdapterTests` is the case catalog to clone onto 8081, with Pay’s `captured:`/`failed:` tokens and 400 instead of `Verified=false`.
- **10-honesty-frontend-risks:** merchant “Not e-mandate” copy is honest; MYR-only mint vs INR keys is the dogfood limit; checklist `[x]` on missing tests is the process risk.
- **01-new-host-seams:** Razorpay PUT last4-of-key_id and two-field join live in `GatewayEndpoints`; Start 400 email vs rail 503 currency/key; webhook 503 on missing webhook ciphertext.
- Do not open a Hub `ProjectReference` to “reuse `ResolveEventId`.” The method is 20 lines; Pay already has the 015 version.
)
