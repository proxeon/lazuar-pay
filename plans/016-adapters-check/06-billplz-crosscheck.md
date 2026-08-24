# 06 — Billplz Hub HTTP vs Pay `BillplzHosted` + form webhook

**Date:** 24 August 2026  
**Program:** `plans/016-adapters-check` — uncondensed evaluation. **Not an implementation.** **Not** a project reference from `apps/lazuar-pay` into `apps/lazuar-api`.  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Branch:** `feat/015-four-adapters`  
**HEAD:** `c621ceba7fc7b79f16954d0819200cb21db6f22b` — `docs(015): check off implemented T–Q phases`  
**Slice:** Billplz. Hub `BillplzGatewayAdapter` + `BillplzPublicBase` + `PublicDnsFallback` as **HTTP judgment**. Live authority is Pay `BillplzHosted` + `BillplzWebhook`. [015](../015-four-adapters/README.md) B10–B29 checklists are a map, not proof.

Parent index: [README.md](./README.md). Binding papers: [015/00-what-must-be-done.md](../015-four-adapters/00-what-must-be-done.md) §5.2; [015/checklists/b10–b29](../015-four-adapters/checklists/); [015/checklists/decisions.md](../015-four-adapters/checklists/decisions.md); [014/06-malaysia-rails.md](../014-evals/06-malaysia-rails.md) §5 (Hub walk, still true as HTTP, stale on “Pay has no Billplz code”).

**This paper does not implement.** It records what the files do on this SHA, what 015 asked, which tests actually lock the steal, and which B-row checkmarks are dishonest against live `RailTests`.

---

## 0. Files opened (all of them)

Hub (judgment only):

| Path | Lines (this SHA) | Job |
|------|------------------|-----|
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs` | 333 | v3 bills JSON create, form HMAC parse, `ChargeOffSession` returns false, `IssueRefund` returns false |
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzPublicBase.cs` | 85 | `IsProductionApi` (not hostname), `TryResolveCallbackBase` fail-closed |
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/PublicDnsFallback.cs` | 193 | Named HttpClient `"Billplz"` UDP A-record to 1.1.1.1 / 8.8.8.8 |
| `apps/lazuar-api/Modules/Payments/Infrastructure/DependencyInjection.cs` | 70 | `AddHttpClient("Billplz")` + `ConnectCallback = PublicDnsFallback.ConnectAsync` |
| `apps/lazuar-api/Modules/Payments/Infrastructure/Endpoints.cs` | query loop | Copies query keys to `Query-{key}` headers |
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs` | email/name/cents | `TryResolveEmail`, `ExtractName`, `ToMinorUnits` AwayFromZero |
| `apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs` | 5 methods | Generate + Parse + Refund + Portal + Off-session |
| `apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs` | reminder / mark-refunded | `SupportsOffSession("BILLPLZ")` false; `SupportsApiRefund` false; `RequiresMarkRefunded` true |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/BillplzGatewayAdapterTests.cs` | parse / refund / off-session | Query-checkout_id metadata; unpaid = `PAYMENT_FAILED`; refund false; off-session false |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/BillplzPublicBaseTests.cs` | 5 tests | localhost, fiction DNS, https tunnel, hostname ≠ live, insecure flag |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/BillplzFeeHonestyTests.cs` | grep | Handler passes zero estimated-fee args |

Pay (live authority):

| Path | Job |
|------|-----|
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/BillplzHosted.cs` | `IHostedRail`. POST bills. Public-base fail-closed. |
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/BillplzWebhook.cs` | Form HMAC, dual extra-fields, ignore unpaid, join checkout id |
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/IHostedRail.cs` | `Provider` + `CreateHostedUrlAsync` only |
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/PspParseResult.cs` | EventId / Ignored / CheckoutId / AmountMinor / Currency |
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/PayProviders.cs` | `billplz`; `RequiresPublicMerchantId`; `RequiresEmail`; capability `hosted_link` |
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs` | PUT requires environment for billplz; collection as `public_merchant_id` |
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs` | Switch arm `BillplzWebhook.Parse(raw, request.Query, cred, box)` |
| `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` | Dispatch `PayProviders.Billplz => billplz`; callback-base 400 |
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/BuyerEmail.cs` | Placeholder refuse; name from payer name else email local-part |
| `apps/lazuar-pay/src/Lazuar.Pay/Money/MoneyMath.cs` | `ToMinor` AwayFromZero ×100 |
| `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` | Same-handler `RCPT-` + two-line journal; **does not** write `ProviderSessionId` |
| `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` | `AddHttpClient("billplz")` (no connect hook); `AddScoped<BillplzHosted>()` |
| `apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj` | Stripe.net only. No Billplz NuGet. `InternalsVisibleTo` tests. |
| `apps/lazuar-pay/README.md` | `Pay__PublicBaseUrl` public https; localhost callbacks 400 |
| `apps/lazuar-pay/.env.example` | Commented tunnel origin |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/RailTests.cs` | **One** Billplz test: `Billplz_paid_form_and_localhost_blocked` |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PayApiFactory.cs` | Forces `Pay:PublicBaseUrl=https://pay.test.example` |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/FakePspHandler.cs` | Captures `LastUri` / `LastBody`; **not** Authorization |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/GatewayTests.cs` | Chip collection analogue exists (`Chip_put_requires_brand_id`); **no** Billplz PUT cases |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/WebhookTests.cs` | Stripe only |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPayTests.cs` | Empty webhook 400 on **stripe** path (shared handler) |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs` | Bans factory/adapter types; does **not** grep `PublicDnsFallback` |
| `apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx` | Billplz fields + reminder copy |
| `apps/lazuar-pay-checkout/src/App.tsx` | `email_required`; 400 copy mentions callback base |

015 map (opened, not treated as evidence): `b10-billplz-class.md` through `b29-billplz-tunnel-runbook.md`, plus `parked-dns-fallback.md`, `parked-refunds.md`, `parked-offsession.md`, `u13-billplz-fields.md`, `p20-placeholder-email-400.md`, `p27-hosted-rail-two-methods.md`.

---

## 1. Standing law this slice does not reverse

From 015 `decisions.md` and §5.2, still binding on this SHA:

- Steal HTTP. Do not copy `IPaymentGatewayAdapter`, `PaymentGatewayFactory`, MediatR, outbox, `Modules.Payments`.
- Two verbs: create hosted bill URL; verify Plane B form and call existing `Fulfillment.FulfillPaidAsync`.
- Capability stays `hosted_link`. Billplz never silent-debits. Do not port Agreements v5 / e-mandate (`SupportsEmandate` remains false in Hub; Pay has no such flag because the interface has no off-session method).
- `paid=true` or `state=paid` fulfills. Verified unpaid is **not** a receipt.
- Event id namespaced `paid:{billId}` (Pay) — not Hub `PAYMENT_COMPLETED:{billId}`, not a bare bill id.
- Callback base is `Pay:PublicBaseUrl`, public **https**. Localhost / loopback / `lazuar-local-dev.com` fail create. Do not port `PublicDnsFallback` unless this host actually cannot resolve `www.billplz-sandbox.com` **and** A00 is amended.
- Collection ID plaintext, API secret + X-Signature secret encrypted separately, `environment` `test`|`live` owns the API host. Do not infer live from `lazuar.com`.
- No `IssueRefund`. Billplz Payment Order is a disbursement. Parked-refunds stays parked.
- Tests hermetic. Fake PSP HTTP. No live Billplz in `task pay:test`.

014/06 said “new Pay has no Billplz code.” That sentence is **stale**. This SHA has `BillplzHosted`, `BillplzWebhook`, named client `"billplz"`, merchant fields, and one RailTests method. 014’s **Hub HTTP walk** is still the steal source. This paper re-reads Hub and then the Pay files.

---

## 2. Hub adapter, as live HTTP (not a port)

### 2.1 Shape Hub grew because the interface lied

`BillplzGatewayAdapter` implements `IPaymentGatewayAdapter` (`GatewayType => "BILLPLZ"`). The interface has five methods. Billplz honestly implements **two** of them (generate + parse) and **fakes** the rest:

| Method | Hub live |
|--------|----------|
| `GenerateCheckoutAsync` | `POST {sandbox\|www}/api/v3/bills`. JSON. Basic `{apiKey}:`. |
| `ParseWebhookAsync` | Form body. Dual HMAC. Paid → `PAYMENT_COMPLETED:{billId}`. Unpaid → `PAYMENT_FAILED:{billId}` **verified**. |
| `ChargeOffSessionAsync` | Logs a warning. Returns `false`. Does not throw. Test `ChargeOffSessionAsync_DoesNotThrow_ReturnsFalse`. |
| `IssueRefundAsync` | Returns `false`. Comment: Payment Order is a new disbursement, not a reversal. Test `IssueRefundAsync_AlwaysReturnsFalse`. |
| `GenerateCustomerPortalAsync` | Throws `InvalidOperationException("Billplz does not provide a managed customer billing portal.")`. |

014/06 already said: a billing job that can see `ChargeOffSession` will call it; Hub’s defence is capability short-circuit plus a method that returns false. 015’s defence for Pay is **stronger**: `IHostedRail` has no such method. Live Pay matches that. `BillplzHosted` cannot be asked to silent-debit without adding a method.

### 2.2 The only outbound HTTP call

Hub constants:

```22:23:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs
    private const string ProductionApiUrl = "https://www.billplz.com/api/v3/";
    private const string SandboxApiUrl = "https://www.billplz-sandbox.com/api/v3/";
```

Host pick is **not** `Contains("lazuar.com")`. `BillplzPublicBase.IsProductionApi` order:

1. `App:BillplzEnvironment` `production`/`live` → www.
2. Same key `sandbox`/`test` → sandbox.
3. Else tenant `configEnvironment` from metadata `hub_payment_environment` (`live`/`production` vs `test`/`sandbox`).
4. Else **sandbox**. `apiBaseUrl` is discarded (`_ = apiBaseUrl`). Tests lock `pay-local.lazuar.com` and `hub.lazuar.com` do **not** force live.

Generate then:

1. Missing `merchantId` → `"MerchantId (Collection ID) is required for Billplz."`
2. `GatewayCommon.TryResolveEmail` — blank or `customer@example.com` fails.
3. `App:ApiBaseUrl` (default `http://localhost:8080/api/v1`) through `TryResolveCallbackBase`. Loopback / fiction DNS / non-https fail unless `App:AllowInsecureBillplzCallback`.
4. `POST {endpoint}bills` on named client `PublicDnsFallback.HttpClientName` (`"Billplz"`).
5. `Authorization: Basic` base64 UTF-8 of `{apiKey}:` (empty password).
6. `JsonContent.Create(payload)` — JSON, not form-urlencoded. Official docs often show form; Hub used JSON; it is the steal.
7. Read `url` + `id`. Missing url → failure result, not throw.

Payload keys Hub actually sends:

| JSON key | Source |
|----------|--------|
| `collection_id` | `merchantId` |
| `email` | resolved buyer email |
| `name` | `GatewayCommon.ExtractName(email)` — **local-part only**, never a real payer name |
| `amount` | `ToMinorUnitsTruncating(amount, quantity)` which **calls** `ToMinorUnits` (AwayFromZero). The method name lies. |
| `description` | product name + optional `(x{qty})` |
| `callback_url` | `{callbackBase}/webhooks/payments/billplz/{tenantId}?type=…&reference_1=…` and optional `&checkout_id=` |
| `redirect_url` | success URL. **Cancel URL is unused.** v3 bills have no cancel redirect. |
| `reference_1_label` | `"Reference"` |
| `reference_1` | `subscription_id` ?? `tenant_id` ?? `tenantId.ToString()` — Hub commerce/M2M folklore |
| `reference_2_label` | `"Type"` |
| `reference_2` | metadata `type` or `"payment"` |

`setupFutureUsage` is an unused parameter. Commerce still passed `true`; Billplz ignored it. Steal that honesty: do not send a vault flag.

That is the **only** outbound call. No GET bill. No refund HTTP. No webhook CRUD. No delete-unpaid-bill. Session merge by stored bill id is Hub-side (`IntegrationCheckoutSession`).

### 2.3 Callback URL and the Query-* cathedral

Billplz **strips body metadata**. Hub therefore stamps identity onto the **callback query**:

```83:93:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs
        var webhookUrl = $"{callbackBase}/webhooks/payments/billplz/{tenantId}";
        webhookUrl = $"{webhookUrl}?type={Uri.EscapeDataString(typeValue)}&reference_1={Uri.EscapeDataString(ref1)}";
        // ...
        if (metadata.TryGetValue("checkout_id", out var checkoutId)
            && !string.IsNullOrWhiteSpace(checkoutId))
        {
            webhookUrl = $"{webhookUrl}&checkout_id={Uri.EscapeDataString(checkoutId)}";
        }
```

Inbound `Endpoints.cs` copies every query key into headers as `Query-{key}` so `ParseWebhookAsync` can read `Query-checkout_id`, `Query-reference_1`, `Query-type`, `Query-subscription_id` without seeing `HttpRequest`. That cathedral exists because the adapter interface only gets `Dictionary<string, string> headers`. New Pay does **not** need it: `BillplzWebhook.Parse` takes `IQueryCollection` directly.

Hub test `GenerateCheckout_WithCheckoutId_AppendsQueryParam` **does not assert the query**. It expects `Success == false` because there is no mock HTTP. 014/06 already called this a lying test. Pay was supposed to write the real mock-HTTP assertion. See §10.

### 2.4 Form HMAC — with-extra then without-extra

Extra / always-exclude sets, live:

```31:39:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs
    private static readonly HashSet<string> ExtraFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "paid_at", "transaction_id", "transaction_status"
    };

    private static readonly HashSet<string> AlwaysExclude = new(StringComparer.OrdinalIgnoreCase)
    {
        "x_signature"
    };
```

Algorithm (steal this, not the class):

1. `ParseFormBody` = `QueryHelpers.ParseQuery` on the raw body. Case-insensitive dictionary. Empty body → empty dict.
2. Require form field `x_signature`. Missing → not verified, `"Missing x_signature in Billplz callback."`
3. `ComputeHmac(form, secret, excludeExtra: false)`: drop `x_signature`; keep extras; each remaining field → `key+value` (no `=`); **Ordinal** sort of those concatenations; join with `|`; HMAC-SHA256 UTF-8 key = webhook secret, UTF-8 data = source; hex **lowercase**.
4. `FixedTimeEqualsHex` (trim, lower, length-equal, `CryptographicOperations.FixedTimeEquals` on UTF-8 bytes of the hex).
5. If mismatch, recompute with `excludeExtra: true` (drop `paid_at`, `transaction_id`, `transaction_status` as well). If both fail → `"Billplz x_signature verification failed."`
6. Bill id from form `id`. Missing/whitespace → `AsUnusable()`, `"Missing stable Billplz bill id"`.
7. Paid iff `paid` equals `"true"` ignore-case **or** `state` equals `"paid"`. Else EventType `PAYMENT_FAILED` **and still Verified: true**.
8. Amount: `paid_amount` sen / 100m. Currency **hardcoded `"MYR"`**.
9. Metadata reconstruction from `reference_1` / `reference_2` / Query-* / form `checkout_id`. Hub maps `reference_1` to `subscription_id` or `tenant_id` depending on platform-collected type. That mapping is Hub Commerce/SaaS. **Do not steal it.**
10. EventId = `{PAYMENT_COMPLETED|PAYMENT_FAILED}:{billId}`.
11. Fee formula uses estimated percentage + fixed fee. Handler now passes **zeros**. `BillplzFeeHonestyTests` greps that. Do not revive a made-up MDR.

**No timestamp in the HMAC.** Replay of the same body verifies forever. Dedup is the event-id grain. Unpaid and paid are **different** Hub event ids (`PAYMENT_FAILED:bill` vs `PAYMENT_COMPLETED:bill`), which is why Hub needed a late-fail-after-completed ignore. Pay’s unpaid grain is `unpaid:{billId}` and does not fulfill, so the same collision class is smaller.

Hub tests: bad signature not verified; missing/empty id unusable; unpaid is `PAYMENT_FAILED:bill_unpaid_1`; Query-checkout_id lands in metadata. Hub tests **do not** exercise extra-fields HMAC (their `ComputeXSignature` helper always includes extras except `x_signature`). The dual-compute exists in production Hub code without a dedicated extra-fields fixture.

### 2.5 Public base fail-closed (steal the rule)

`BillplzPublicBase.TryResolveCallbackBase`:

- Empty `App:ApiBaseUrl` → `CALLBACK_BASE_NOT_PUBLIC`.
- Must be absolute http(s).
- Loopback: `Uri.IsLoopback` or host `localhost` / `127.0.0.1` / `::1`.
- Fiction: host contains `lazuar-local-dev.com` (CHIP registrar used to rewrite localhost to that name; Billplz **refuses** the fiction).
- Unless `App:AllowInsecureBillplzCallback` is true: loopback, fiction, or non-https → fail with “set App:ApiBaseUrl to a public https origin Billplz can POST (Cloudflare tunnel)…”.
- Tests: localhost rejected; fiction rejected; `https://pay-local.example.com/api/v1` accepted; insecure flag allows localhost.

Steal: fail-closed on loopback / fiction / non-https. **Do not** steal the insecure flag unless A00 is amended. B15 said the flag is out of this program.

### 2.6 PublicDnsFallback (do not steal)

193 lines. Named client `"Billplz"` only. CHIP uses unnamed `CreateClient()`. Stripe.net has its own handler. The hook:

- `SocketsHttpHandler.ConnectCallback`.
- UDP query A records at 1.1.1.1 then 8.8.8.8 (hand-rolled encoder/decoder, 2s timeout).
- Else `Dns.GetHostAddressesAsync`.
- Try each address TCP connect.

015 B23 / parked-dns-fallback / 014/09: folklore until **this** Pay host cannot resolve `www.billplz-sandbox.com`. Do not copy 193 lines “just in case.” Do not register a `"Billplz"` client with an empty class.

---

## 3. Pay `BillplzHosted` — live create path

Class: `apps/lazuar-pay/src/Lazuar.Pay/Gateways/BillplzHosted.cs`. `public sealed class BillplzHosted(…) : IHostedRail`. `Provider => PayProviders.Billplz` (`"billplz"` lowercase).

Registered in `Program.cs`:

```21:30:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
builder.Services.AddHttpClient("chip");
builder.Services.AddHttpClient("billplz");
builder.Services.AddHttpClient("xendit");
builder.Services.AddHttpClient("razorpay");
// ...
builder.Services.AddScoped<BillplzHosted>();
```

`AddHttpClient("billplz")` is the **standard** typed-name client. No `SocketsHttpHandler`. No `ConnectCallback`. No timeout override (Hub Billplz client was 15s; Pay uses the framework default). Name is lowercase `"billplz"`, not Hub `"Billplz"`. B13 said do not use the Hub DNS client name. Live complies.

`IHostedRail` on this SHA is two members only:

```5:12:apps/lazuar-pay/src/Lazuar.Pay/Gateways/IHostedRail.cs
public readonly record struct HostedSession(string RedirectUrl, string? ProviderSessionId);

public interface IHostedRail
{
    string Provider { get; }
    Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct);
}
```

P27 originally wrote `Task<string>`; live grew a record so start can persist bill id. Parse is **not** on the interface. WebhookEndpoints owns verify. No refund. No off-session. No portal. IsolationTests ban `IPaymentGatewayAdapter` / `PaymentGatewayFactory` in `src/`. `BillplzHosted.cs` contains neither.

### 3.1 CreateHostedUrlAsync, step by step

1. Load `GatewayCredentials` for `(checkout.OrgId, "billplz")`. Missing row **or** whitespace `PublicMerchantId` → `InvalidOperationException("rail not configured")`. Start maps that to **503**.
2. `BuyerEmail.IsUsable(checkout.PayerEmail)` — blank or `customer@example.com` (trim, ignore-case) → `"email is required"`. Start also gates `PayProviders.RequiresEmail("billplz")` **before** calling the rail, so this is defence in depth. Start maps `"email is required"` to 503 if it ever escaped the 400 gate… wait: Start’s catch maps messages containing `"callback base"` to 400, **else 503**. So a rail-level email throw would be 503, not 400. In practice Start’s `RequiresEmail` check returns 400 first. The double check is still correct if someone calls the rail from another site later.
3. `TryPublicBase(config["Pay:PublicBaseUrl"], …)` — see §5. Fail → throw `baseError` (`"callback base not public"`). Start maps that to **400** because the message contains `"callback base"`.
4. Host: `cred.Environment` equals `"live"` ignore-case → `https://www.billplz.com/api/v3/`, **else** `https://www.billplz-sandbox.com/api/v3/`. Anything that is not exactly `live` (including empty, `test`, a hypothetical `production` string) is sandbox. PUT only allows `test`|`live`, so `production` cannot be stored through the API.
5. Callback: `{publicBase}/v1/webhooks/billplz/{checkout.OrgId}?checkout_id={Uri.EscapeDataString(checkout.Id)}`.
6. JSON payload (see §3.2).
7. `http.CreateClient("billplz")`. `POST {host}bills`. Basic `base64(Unprotect(ciphertext) + ":")`. `JsonContent.Create`.
8. Non-success → `"Billplz rejected the org key"` → Start 503. Body is **not** echoed to the buyer (Hub logged the Billplz body server-side). Honest.
9. Parse `url` + `id`. Missing/whitespace url → `"Billplz returned no URL"` → 503.
10. Return `HostedSession(url, id)`. Start persists `row.Provider = "billplz"`, `PspRedirectUrl`, `ProviderSessionId`.

### 3.2 Create JSON vs Hub

| Field | Hub | Pay live | Verdict |
|-------|-----|----------|---------|
| `collection_id` | `merchantId` | `cred.PublicMerchantId` | Steal. Required. |
| `email` | resolved email | `checkout.PayerEmail.Trim()` | Steal. Usable-email already gated. |
| `name` | email local-part **only** | `BuyerEmail.NameFrom(email, checkout.PayerName)` — **payer name if present**, else local-part | **Better than Hub.** 014/013 called Hub’s local-part a honesty bug. Checkout SPA has a name field. |
| `amount` | AwayFromZero sen, × quantity | `MoneyMath.ToMinor(checkout.Amount)` AwayFromZero sen, **no quantity** | Same rounding policy. Pay checkouts have no qty. |
| `description` | product name | hardcoded `"Pay"` | Acceptable for this program (CHIP also hardcodes `"Pay"`). Not a Hub port of `ProductDescription`. |
| `callback_url` | Hub `/webhooks/payments/billplz/{tenantId}?type&reference_1&checkout_id` | Pay `/v1/webhooks/billplz/{orgId}?checkout_id=` | Correct. Not Hub path. |
| `redirect_url` | success URL | `checkout.SuccessUrl` ?? `http://localhost:5179/c/{token}?status=verifying` | Browser hop, not Billplz POST. Localhost here is **allowed**. |
| `reference_1_label` | `"Reference"` | `"Checkout"` | B17 allowed either. |
| `reference_1` | subscription/tenant/org folklore | `checkout.Id` | **Required steal.** B17. |
| `reference_2` / `_label` | type / `"Type"` | **omitted** | B17: do not invent Hub `commerce_subscription`. Correct omission. |
| `setupFutureUsage` | unused param | **not sent** | Correct. |
| Auth | Basic `{apiKey}:` | Basic `{unprotect(ciphertext)}:` | Steal. |
| Client | `"Billplz"` + DNS hook | `"billplz"` standard | Correct refuse. |
| Host pick | `App:BillplzEnvironment` then tenant env then sandbox | **row** `Environment == live` else sandbox | Correct. No hostname inference. No process `Pay:BillplzEnvironment` override (Hub had one; 015 said the row owns it). |

Pay does **not** send `currency`. Hub did not either. Billplz v3 bills are MYR sen. See §8.4 for the USD-checkout hole.

`redirect_url` vs `callback_url` are different jobs. Callback is Billplz **server** POST — must be public https. Redirect is the **buyer browser** after pay — `:5179` verifying poll is the 015 dogfood path. Merchant Vite `createProductAndLink` does **not** set `success_url` on `POST /v1/checkouts`, so live dogfood always gets the localhost:5179 fallback. That is fine when the buyer is the developer on the same laptop. It is not a production merchant success URL. Not a Billplz HTTP bug; it is a merchant-UI omission. Call it out so 10-honesty can rank it.

Cancel URL is unused, same as Hub. ChipHosted sends `failure_redirect` / `cancel_redirect`. Billplz v3 cannot. Do not invent a cancel field.

### 3.3 Basic auth

Pay:

```55:56:apps/lazuar-pay/src/Lazuar.Pay/Gateways/BillplzHosted.cs
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(apiKey + ":")));
```

Hub: `Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:"))`. Same empty-password Basic. Official Billplz: API secret as username, password empty.

**Tests do not lock this.** `FakePspHandler` stores `LastUri` and `LastBody` only. `Billplz_paid_form_and_localhost_blocked` never inspects `Authorization`. A regression that sent Bearer, or `apiKey` without the trailing colon, or the webhook secret as the Basic user, would still go green.

### 3.4 collection_id

PUT (`GatewayEndpoints`):

- `PayProviders.RequiresPublicMerchantId("billplz")` is true (chip or billplz).
- Whitespace `public_merchant_id` → 400 `"public_merchant_id is required"`.
- Stored plaintext on `GatewayCredentialRow.PublicMerchantId`. GET echoes it. Not a secret.
- Create JSON always includes `collection_id` from that column.
- Start with a row whose `PublicMerchantId` is later emptied would 503 `"rail not configured"`. PUT cannot empty it for billplz (required). Direct DB vandalism only.

**Tests:** `Chip_put_requires_brand_id` exists. **No** `Billplz_put_requires_collection_id`. Shared `RequiresPublicMerchantId` means the Chip test is not a Billplz proof. B27.1 “PUT without public_merchant_id → 400” is **unchecked by a Billplz fixture**. Start-with-empty-collection is also untested.

Merchant UI: Collection ID placeholder when provider is billplz; sent as `public_merchant_id`. Matches B11 / U13.

---

## 4. Hosts: test vs live, not hostname

Pay live:

```35:37:apps/lazuar-pay/src/Lazuar.Pay/Gateways/BillplzHosted.cs
        var host = string.Equals(cred.Environment, "live", StringComparison.OrdinalIgnoreCase)
            ? "https://www.billplz.com/api/v3/"
            : "https://www.billplz-sandbox.com/api/v3/";
```

POST target is `{host}bills` — same concatenation as Hub (`endpoint + "bills"`), producing `…/api/v3/bills`.

PUT:

- Environment defaults to `"test"` **for other rails**.
- **Billplz is special-cased:** if `body.Environment` is whitespace → 400 `"environment is required"`. Cannot accidentally live-charge because a staff form omitted the select. Merchant UI always sends `environment` for billplz (`test` | `live`).
- Allowed values after trim/lower: `test` or `live` only. `"production"` / `"sandbox"` → 400 `"environment must be test or live"`.

Grep of Pay `src/` for `lazuar.com` as a live-host inference: **none** in the Billplz files. `TryPublicBase` does not look at `lazuar.com`. B12.1 “Do not use Contains(lazuar.com) to pick live” holds.

**Tests:** RailTests asserts `factory.Psp.LastUri` **contains** `billplz-sandbox` after a `environment: test` PUT + start. That locks the test host for the happy path.

**Missing:**

- `environment: live` start → URI contains `www.billplz.com` and does **not** contain `billplz-sandbox`.
- PUT billplz without `environment` → 400.
- PUT `environment: production` → 400.
- Unit test of host selection as a pure function. Host pick is inline in `CreateHostedUrlAsync`, not `BillplzPublicBase.IsProductionApi`. B12.2 “Host selection unit-testable” is only true in the sense that you *could* extract it. Live did not.

No process override `Pay:BillplzEnvironment`. Hub’s ops override is gone, on purpose: the credential row is SoT. A developer who sets `Pay:PublicBaseUrl` to a production tunnel and forgets `environment=live` still hits **sandbox**. That is the fail-safe 015 wanted (pay-local.lazuar.com must never go live). The opposite mistake (row `live` while still on a tunnel) would hit **www** with a sandbox key and Billplz would 401 — Start 503 `"Billplz rejected the org key"`. Acceptable.

---

## 5. Localhost callback is 400 (code yes, test no)

### 5.1 Live rule

```76:103:apps/lazuar-pay/src/Lazuar.Pay/Gateways/BillplzHosted.cs
    internal static bool TryPublicBase(string? raw, out string callbackBase, out string error)
    {
        callbackBase = "";
        error = "";
        var value = (raw ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "callback base not public";
            return false;
        }

        var host = uri.Host;
        var loopback = uri.IsLoopback
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase)
            || host.Contains("lazuar-local-dev.com", StringComparison.OrdinalIgnoreCase);
        if (loopback)
        {
            error = "callback base not public";
            return false;
        }

        callbackBase = value;
        return true;
    }
```

Compared to Hub `TryResolveCallbackBase`:

| Case | Hub | Pay |
|------|-----|-----|
| Empty | fail `CALLBACK_BASE_NOT_PUBLIC` | fail `"callback base not public"` |
| `http://localhost:8081` | fail (loopback + http) | fail (not https, before host check) |
| `https://localhost:8081` | fail loopback | fail loopback |
| `https://127.0.0.1` | fail | fail |
| `https://[::1]` | fail (`Uri.IsLoopback`) | fail |
| host contains `lazuar-local-dev.com` | fail fiction | fail (folded into the `loopback` local) |
| `http://public.example` | fail unless insecure flag | **always fail** (scheme must be https) |
| `https://pay.test.example` | accept | accept |
| `App:AllowInsecureBillplzCallback` | allows localhost http | **not ported** (B15) |
| Rewrite localhost → fiction DNS | **not in this class** (CHIP registrar did that; Billplz refused it) | **not present** |
| Config key | `App:ApiBaseUrl` | `Pay:PublicBaseUrl` |

`appsettings.json` does **not** set `Pay:PublicBaseUrl`. `.env.example` has it commented. Default local `task pay:dev` with no env var: Billplz start throws `"callback base not public"` → HTTP **400**, and `TryPublicBase` runs **before** `CreateClient` / `SendAsync`, so **no HTTP to Billplz**. That is the B15 product behaviour.

Start catch:

```111:115:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        catch (InvalidOperationException ex)
        {
            var status = ex.Message.Contains("callback base", StringComparison.Ordinal) ? 400 : 503;
            return PayErrors.Status(status, status == 400 ? "Bad Request" : "Service Unavailable", ex.Message);
        }
```

Checkout SPA maps 400 to `'callback base not public or email required'`. Merchant copy: “Callback must be public https (localhost will fail).” README: “Billplz needs `Pay__PublicBaseUrl` as public **https** (localhost callbacks 400).” `.env.example`: “Public https origin Billplz can POST (tunnel). Localhost is rejected.” B29 runbook exists as docs, not product DNS.

### 5.2 The test named `Billplz_paid_form_and_localhost_blocked` does not block localhost

`PayApiFactory.ConfigureWebHost` **always**:

```26:26:apps/lazuar-pay/tests/Lazuar.Pay.Tests/PayApiFactory.cs
        builder.UseSetting("Pay:PublicBaseUrl", "https://pay.test.example");
```

`Billplz_paid_form_and_localhost_blocked`:

- Puts billplz keys with `environment: test`.
- Starts with email. **Asserts success.**
- Asserts LastUri contains `billplz-sandbox`.
- HMACs a paid form. Webhook 200. One document.

It never:

- Overrides `Pay:PublicBaseUrl` to `http://localhost:8081` or `https://localhost`.
- Asserts start is 400/503.
- Asserts `factory.Psp.LastUri` is null / SendCount did not rise (no HTTP to Billplz).
- Calls `BillplzHosted.TryPublicBase` directly (it is `internal static`; tests have `InternalsVisibleTo`).

B15.3 “Default local `PublicBaseUrl=http://localhost:8081` + billplz start → 400/503 without HTTP to Billplz” is **checked off in the checklist and not present in the test project.** B28.1 “localhost PublicBaseUrl start 400/503 without network (B15)” same lie. The method name packs two B28 rows into one test that only implements **paid form + sandbox host**.

B28.2 “Tests set `Pay:PublicBaseUrl=https://pay.test.example` so create can run” is true — and is exactly why the localhost case never runs in that factory unless a test overrides the setting.

**Must still write:**

1. Direct `TryPublicBase` cases: empty, http public, https localhost, 127.0.0.1, `::1`, `https://foo.lazuar-local-dev.com`, `https://pay.test.example` (trim trailing slash).
2. HTTP-level: factory with `UseSetting("Pay:PublicBaseUrl", "http://localhost:8081")`, billplz PUT, start → 400, message contains `callback base not public`, `Psp.LastUri` still null.

Until those exist, B15 is **code-complete, test-absent**. Hub has `BillplzPublicBaseTests`. Pay does not have the equivalent.

---

## 6. callback_url + checkout_id query (join)

### 6.1 Mint

Pay callback is exactly B14.1:

`{Pay:PublicBaseUrl trimmed}/{v1/webhooks/billplz}/{orgId}?checkout_id={escaped checkout.Id}`

Not Hub `/api/v1/webhooks/payments/billplz/{tenantId}`. No secrets in the query. `type` and Hub `reference_1` query keys are **not** copied (Pay puts checkout id in `reference_1` body field instead).

**Tests do not assert `LastBody` contains that URL.** Chip start asserts `LastBody` contains `checkout_id` (CHIP metadata). Billplz start only asserts the **host** in `LastUri`. A regression that omitted `?checkout_id=` or pointed callback at Hub’s path would still pass `Billplz_paid_form_and_localhost_blocked` because the webhook test **itself** adds `?checkout_id=` on the test request. That tests the verify join, not the create stamp.

Must write: mock bills POST, assert `LastBody` JSON `callback_url` equals `https://pay.test.example/v1/webhooks/billplz/t1?checkout_id={id}` and `reference_1` equals the checkout id.

### 6.2 Verify join order

`BillplzWebhook.Parse` after HMAC + bill id + paid check:

1. `query["checkout_id"]` (`IQueryCollection` — not Hub `Query-checkout_id` headers).
2. Else form field `checkout_id`.
3. Else form `reference_1` (B17 fallback; `reference_1` **is** checkout id on Pay-minted bills).
4. Else `CheckoutId = null`.

`WebhookEndpoints` if `CheckoutId` whitespace → 400 `"checkout not found"`. Then load checkout by id; if null **or** `checkout.OrgId != path orgId` → same 400. Cross-org bind is live (Stripe test `Cross_org_checkout_is_400`; no Billplz-specific twin).

B16.1 also said: **then** `ProviderSessionId` match on bill id if still missing; then 400 unusable. Live `WebhookEndpoints` has **no** lookup by `ProviderSessionId` / bill id. Start **does** persist `ProviderSessionId = bill id`. A collection-level dashboard callback to `/v1/webhooks/billplz/{orgId}` **without** query and **without** `reference_1` in the form would 400 even though the bill id is sitting on an open checkout. That is a **code gap vs B16**, not only a test gap. Hub’s safety net was `IntegrationCheckoutSession` by `ProviderSessionId == GatewayTransactionId`. 014/09 told Pay to persist bill id at generate and treat bill-id merge as fallback. Persist happened; merge did not.

Fulfillment also does **not** “persist ProviderSessionId if empty” (B22.1). It writes `ChargeRow.ProviderRef`. If start already stored the bill id, fulfill does not need to. If create returned a url with a null id, `ProviderSessionId` stays null and join still works via query/`reference_1`.

### 6.3 Merchant webhook hint vs per-bill callback

`:5178` prints `{payApi}/v1/webhooks/{provider}/{orgId}` with **no** `checkout_id`. That is the collection-level hint. Real money path is the **per-bill** `callback_url` stamped at create. If a merchant *also* pastes the hint into the Billplz collection callback setting, join falls back to `reference_1 = checkout.Id`. That is why B17 exists. If they paste the hint and we had not set `reference_1` to checkout id, unpaid-of-join would 400. Live sets it. Untested against a webhook **without** query, join-by-`reference_1` only.

---

## 7. HMAC with-extra then without-extra

Pay `BillplzWebhook` copies Hub’s dual compute. Extra set is the same three names. `x_signature` excluded by string equals ignore-case (Hub used `AlwaysExclude` HashSet — same effect).

```33:41:apps/lazuar-pay/src/Lazuar.Pay/Gateways/BillplzWebhook.cs
        var secret = box.Unprotect(cred.WebhookCiphertext);
        var withExtra = ComputeHmac(form, secret, excludeExtra: false);
        if (!FixedTimeEqualsHex(provided, withExtra))
        {
            var without = ComputeHmac(form, secret, excludeExtra: true);
            if (!FixedTimeEqualsHex(provided, without))
            {
                throw new PspVerifyException("invalid signature");
            }
        }
```

`ComputeHmac`: drop `x_signature`; if `excludeExtra`, also drop `paid_at` / `transaction_id` / `transaction_status`; `key+value`; Ordinal sort; join `|`; HMAC-SHA256; hex lower. `FixedTimeEqualsHex` same as Hub (trim, lower, length, `CryptographicOperations.FixedTimeEquals`).

Missing `x_signature` → `PspVerifyException("invalid signature")` → WebhookEndpoints **400**. Not 500. Not Hub’s `Verified: false` result object — Pay throws, host maps. Same HTTP outcome.

Empty body never reaches Parse: WebhookEndpoints 400 `"empty body"` first (shared for all five rails).

JSON body on the Billplz route: **no Content-Type gate**. 014/06 said “A JSON body on the Billplz route is 400.” Live will `ParseQuery` the JSON string, fail to find `x_signature`, 400 `"invalid signature"`. Close enough for fail-closed; not the explicit “this is not form” 400. Billplz dashboard sends form. Fine.

Webhook secret missing on the row → `InvalidOperationException("webhook secret missing")` → 503 (host special-cases that message). PUT requires `webhook_secret`, so this is “row vandalised after PUT” / migration residue. Stripe has `Missing_webhook_secret_is_503_when_rail_configured`. Billplz does not.

### 7.1 B19 tests vs live RailTests

B19.2 demanded three fixtures:

| Fixture | Live? |
|---------|-------|
| Verifies with extra **included** still 200 | The one paid test signs with `excludeExtra: false`. Form has **no** extra fields, so with-extra and without-extra **hashes are identical**. Does not prove the include path as distinct from exclude. |
| Verifies **only** with extra-excluded still 200 | **Absent.** Need form with `paid_at` (and/or the other two), HMAC computed with `excludeExtra: true`, POST, 200 + receipt. |
| Wrong secret both ways → 400 | **Absent.** Stripe `Invalid_signature_is_400` is Stripe-Signature, not form HMAC. |

The paid test even does a confused dance:

```156:160:apps/lazuar-pay/tests/Lazuar.Pay.Tests/RailTests.cs
        var form = "id=bill_1&paid=true&state=paid&paid_amount=1000&x_signature=pending&checkout_id=" + checkoutId;
        var fields = BillplzWebhook.ParseForm(form);
        fields["x_signature"] = "pending";
        var mac = BillplzWebhook.ComputeHmac(fields, "xsig", excludeExtra: false);
        form = "id=bill_1&paid=true&state=paid&paid_amount=1000&x_signature=" + mac + "&checkout_id=" + checkoutId;
```

That is a valid with-extra (no extras present) happy path. It is **not** B19.

Hub production needed the dual path because Billplz dashboard versions disagree on whether `paid_at` / `transaction_id` / `transaction_status` are in the signed set. If Pay only tests the no-extra body, the first live extra-field callback from a collection on the “new” signing rules still works (with-extra first). A collection on the “old” rules with extras present in the form would need the second compute. **That is the case that is untested.** Shipping without it is how you discover HMAC 400s in sandbox after a Billplz side upgrade.

Must write:

1. Form with `paid_at=2026-08-24 12:00:00&transaction_id=t1&transaction_status=completed` plus the usual paid fields. MAC with `excludeExtra: false` → 200.
2. Same form, MAC with `excludeExtra: true` (so with-extra mismatches, without-extra matches) → 200.
3. Same form, MAC with wrong secret → 400, zero documents.
4. Deadbeef / missing `x_signature` → 400.

---

## 8. Paid fulfill, unpaid ignore, event id, amount, currency

### 8.1 Paid

`isPaid = paid == "true" ignore-case OR state == "paid"`. Same OR as Hub.

Then `PspParseResult`:

- `EventId = "paid:" + billId` (B22; 015 namespaced; **not** Hub `PAYMENT_COMPLETED:{billId}`).
- `CheckoutId` from the join in §6.2.
- `ProviderRef = billId`.
- `AmountMinor = paid_amount` parsed as `long` (Hub used `int` then `/ 100m`).
- `Currency = "MYR"` hardcoded (same as Hub).

WebhookEndpoints: unique `(org, provider, event_id)`; amount match `parsed.AmountMinor == MoneyMath.ToMinor(checkout.Amount)`; currency match ignore-case; one TX insert + `FulfillPaidAsync(checkout.Id, "billplz", billId)`. Official Receipt `RCPT-`, cash debit + revenue credit, no tax line, no fee line.

Live paid test asserts HTTP 200 and `Documents.Count == 1`. It does **not** assert `Number` starts with `RCPT-`, journal two lines, debit == credit, checkout `paid`, `Charges.ProviderRef == bill_1`. Chip’s paid test asserts `RCPT-` and balanced journal **and replay**. Billplz paid test is thinner.

**Replay:** Chip posts twice, second body contains `duplicate`, still one document. Billplz paid test does **not** replay. B20.2 / B28.1 “paid form → RCPT- + replay” is half-done. Unique grain `paid:bill_1` would make a second POST `{ duplicate: true }` if anyone wrote it. Code path is shared. Fixture is missing.

### 8.2 Unpaid — Hub failed-event vs Pay ignore

Hub: verified unpaid → `PAYMENT_FAILED:{billId}`, still `Verified: true`. Commerce/M2M may publish a failed integration event.

015 B21 / decisions “Setup ≠ paid”: HMAC valid, `paid=false` and state not paid → 200 `{ ignored: "unpaid" }`; no `RCPT-`; checkout stays `open`; unique grain must **not** be `paid:{billId}` (use `unpaid:{billId}` or do not insert).

Pay live:

```54:57:apps/lazuar-pay/src/Lazuar.Pay/Gateways/BillplzWebhook.cs
        if (!isPaid)
        {
            return new PspParseResult { EventId = "unpaid:" + billId, Ignored = true, IgnoreReason = "unpaid" };
        }
```

WebhookEndpoints: duplicate check on that EventId; if Ignored, `InsertEventAsync` then `Results.Json({ ignored: unpaid })`. No fulfill. Grain `unpaid:billId` ≠ `paid:billId`, so a later paid callback still fulfills. A replay of unpaid returns `{ duplicate: true }`. Late unpaid after paid: ignored insert, checkout remains `paid` (fulfill not called). Hub needed extra “ignore FAIL after COMPLETED” because both were real event types. Pay’s ignore is simpler and correct.

**Tests: there is no unpaid Billplz fixture.** B21.2 “Test green” is a checklist lie. Chip has `Chip_preauthorized_is_ignored`. Xendit test covers SETTLED-after-PAID. Billplz unpaid is the hole 015 named in the per-provider matrix (“Not-paid ignored, zero docs | paid=false”) and then did not write.

Must write: HMAC-valid `paid=false&state=due&id=bill_1&paid_amount=0` → 200 body contains `unpaid`, `Documents.Count == 0`, checkout `open`. Then a paid callback for the same bill still writes one `RCPT-`.

### 8.3 Missing bill id

Whitespace/missing `id` after HMAC → `PspVerifyException("missing bill id")` → 400, no fulfill. Hub `AsUnusable()`. Pay throws. Same money outcome. Hub tests lock this; Pay tests do not.

### 8.4 Currency hardcoded MYR vs “do not default MYR”

015 decisions: “Fail closed if PSP omits currency. **Do not default MYR.**” B20.1 carves Billplz: “Billplz is MYR for this program — still do not invent if you later add others; checkout currency must be MYR.”

Live webhook **hardcodes** `Currency = "MYR"` like Hub. Billplz form typically has no currency field, so fail-closed-if-omitted would reject every real callback. Hardcode is the steal.

The hole is on **create**: `BillplzHosted` does not refuse a non-MYR checkout. `CheckoutEndpoints.Create` defaults missing currency to MYR but **accepts** `USD` if sent. Start would POST sen to Billplz (interpreted as RM). Buyer pays RM. Webhook `Currency=MYR` vs checkout `USD` → 400 `"currency mismatch"`. Money taken at Billplz, **no receipt**. Hub generate also ignored the `currency` argument. 014/06 called that “acceptable **for Billplz only**” on the webhook. For Pay, start should 400 if `checkout.Currency` is not MYR. **Not implemented. Not tested.**

`paid_amount` missing on a paid callback defaults to 0 → amount mismatch 400 (checkout create refuses amount ≤ 0). Fail-closed. Untested.

---

## 9. No refund API, no off-session, no DNS fallback

### 9.1 Refund

Hub `IssueRefundAsync` returns false. Comment: Payment Order is a disbursement. `RequiresMarkRefunded("BILLPLZ")` true. `SupportsApiRefund("BILLPLZ")` false.

Pay: `IHostedRail` has no refund method. Grep of `apps/lazuar-pay/src` for `IssueRefund` / `ChargeOffSession` / `PublicDnsFallback` / `Dns.GetHostEntry`: **no matches**. `BillplzHosted` does not POST `/payment_orders` or any refund path. parked-refunds remains parked. B24 holds in code.

Merchant U19 copy is the reminder sentence, not a “we will refund via API” claim. Ops Hub UI still has mark-refunded warning; `:5178` does not offer refunds at all. Correct for this program.

### 9.2 Off-session

Hub method exists and returns false; dunning matcher still has to remember not to call it. Pay: no method, no call site, capability string is always `hosted_link` (`PayProviders.Capability`). Merchant copy: “Reminder + hosted bill. We do not auto-debit.” Checkout SPA does not say “we will charge your card.” B25 holds.

Do not grow `IHostedRail` with an optional `ChargeOffSessionAsync` “for CHIP later.” That is how Billplz grew a lie on Hub’s interface.

### 9.3 DNS fallback

Pay `AddHttpClient("billplz")` — no connect hook. `PublicDnsFallback.cs` does not exist under `apps/lazuar-pay`. IsolationTests do **not** grep the string `PublicDnsFallback`; they would not fail if someone pasted the 193-line file. B23.1 “Grep Pay src for PublicDnsFallback … none” is **true of src today** (this paper grepped). `lazuar-local-dev.com` **does** appear, once, in `BillplzHosted.TryPublicBase` as a **refuse** token. That is not the CHIP registrar rewrite. B23’s “none” and B15’s “refuse fiction DNS” are not in conflict if you read the string as a denylist. parked-dns-fallback stays parked. Do not treat the denylist as a port of folklore DNS.

If dogfood on this laptop cannot resolve `www.billplz-sandbox.com`, amend A00; do not sneak a handler into B13.

---

## 10. Tests inventory (what exists vs what B28 listed)

### 10.1 The one Billplz test, quoted in full as behaviour

`RailTests.Billplz_paid_form_and_localhost_blocked` (lines 136–169):

- Factory with public base `https://pay.test.example` (not localhost).
- Fake PSP 200 `{"id":"bill_1","url":"https://www.billplz-sandbox.com/bills/bill_1"}`.
- PUT `provider=billplz`, `secret=bp_sk`, `webhook_secret=xsig`, `public_merchant_id=col_1`, `environment=test`.
- Seed checkout amount 10.
- Start with `{"email":"ada@acme.test"}` (no name).
- Assert start success.
- Assert LastUri contains `billplz-sandbox`.
- HMAC form `id,paid=true,state=paid,paid_amount=1000,checkout_id` with extra included.
- POST `/v1/webhooks/billplz/t1?checkout_id={id}` as `application/x-www-form-urlencoded`.
- Assert 200 and one document.

**Locks:** test-environment hits sandbox host; form content-type paid path fulfills; query `checkout_id` join; email-required rail can start when email is present; PUT with collection + environment is accepted.

**Does not lock:** localhost 400; extra-fields HMAC; unpaid ignore; replay; `RCPT-` prefix; journal; Basic auth; `collection_id` in body; `callback_url` query; `reference_1`; live host; missing collection PUT; missing environment PUT; empty webhook body on this path; bad HMAC; missing bill id; placeholder email; start without email; amount mismatch; currency mismatch; bill-id fallback join; `TryPublicBase` unit cases.

### 10.2 Shared tests that happen to cover Billplz *if* you squint

| Test | Why it is not enough |
|------|----------------------|
| `PublicPayTests.Empty_webhook_is_400` | Path is `/v1/webhooks/stripe/t1`. Handler is shared, so Billplz empty would 400 too. B28 asked for a Billplz empty-body case. Chip has `Chip_empty_body_400`. Billplz does not. |
| `WebhookTests.Unknown_provider_is_400` | `paypal`. Not Billplz. |
| `Chip_start_without_email_is_400` | Shared `RequiresEmail` for non-stripe. Does not prove Billplz start. A future “email required only for chip” regression would keep Chip green and break Billplz. |
| `Chip_put_requires_brand_id` | Shared `RequiresPublicMerchantId`. Same class of hole for Collection ID. |
| `GatewayTests.Put_requires_webhook_secret` | Stripe body. Shared required-webhook-secret. |
| `IsolationTests.Source_does_not_use_mediatr_or_hub_modules` | Bans adapter/factory strings. Does not ban `PublicDnsFallback` / DNS connect hooks. |

### 10.3 B28.1 must-exist vs this SHA

| B28 / §7 matrix cell | Checklist | Live |
|----------------------|-----------|------|
| Empty body 400 | [x] | Shared stripe/chip only. **No billplz path.** |
| Bad HMAC 400 | [x] | **Missing.** |
| Extra-fields HMAC variant (B19) | [x] | **Missing.** Paid test has no extras. |
| paid form → `RCPT-` + replay | [x] | Paid → one document. **No `RCPT-` assert. No replay.** |
| unpaid ignore | [x] | **Missing.** |
| localhost PublicBaseUrl start 400 without network | [x] | **Missing.** Test name lies. |
| Mocked `POST …/bills` → redirect_url | [x] | Start succeeds (implies a URL). **Does not assert `redirect_url` JSON or LastBody.** |
| Tests set public https origin | [x] | True (`pay.test.example`). |

015 §7 table said the same musts. Checklists B15, B19, B21, B26, B27, B28 are **checked off**. Live tests do not support those checkmarks. 016’s job is to say that out loud so 00-evaluation does not treat B-row `[x]` as proof.

### 10.4 Hub tests we should not pretend Pay inherited

Pay tests do not ProjectReference Hub. Hub `BillplzGatewayAdapterTests` still exist in `lazuar-api` and still lock Hub behaviour (unpaid = FAILED event, Query-* headers, refund false). They do **not** run under `task pay:test`. Do not cite them as Pay coverage.

Hub also never tested extra-fields HMAC. Pay was supposed to be better (B19). It is not, yet.

---

## 11. PUT / secrets / email / frontends (Billplz-shaped)

### 11.1 PUT fields (B11)

Live `PutGatewayRequest`: `provider`, `secret`, `webhook_secret`, `public_merchant_id`, `environment`, plus Razorpay `key_id`/`key_secret` unused here.

Billplz:

- `secret` required (API key) → AES-GCM `Ciphertext`. Last4 = last 4 of secret.
- `webhook_secret` required (X-Signature) → `WebhookCiphertext`. **Not** skipped if equal to secret. Two fields. Merchant may paste twice.
- `public_merchant_id` required (Collection ID) plaintext.
- `environment` required `test`|`live`.
- Sets `org_settings.active_provider = billplz`.
- Writer only (`RequireWriterAsync`). Member PUT 403 (tested with **stripe** body, shared gate).
- GET never echoes ciphertext. `webhook_configured` boolean. `capability: "hosted_link"`.

Hub ops UI required X-Signature length 128 hex. Pay merchant UI **does not** validate length. HMAC uses whatever was pasted. If the collection’s X-Signature key is 128 hex and the merchant pastes the API secret instead, callbacks 400 invalid signature. Product gap, not an HTTP-algorithm gap. Note for honesty paper.

No process-wide Billplz webhook secret. Stripe still has `Pay:StripeWebhookSecret` as **dev fallback**. Billplz has no analogue — missing webhook ciphertext is 503. Correct.

### 11.2 Email required (B26 / P20)

`PayProviders.RequiresEmail("billplz")` true. Start 400 `"email is required"` if unusable. `BuyerEmail.Placeholder = "customer@example.com"` same as Hub. **No Pay test sends the placeholder on a billplz (or any) start.** Grep of `apps/lazuar-pay/tests` for `customer@example.com`: **no matches.** P20 “Hermetic 400” is checked off without a fixture. Chip_start_without_email covers **missing** email, not the placeholder.

Name: Hub local-part; Pay prefers `PayerName`. Checkout SPA collects name+email; email blocked when `email_required`. GET `/v1/pay/{token}` sets `email_required` from active/checkout provider. Billplz as active rail → SPA requires email. Good.

### 11.3 Merchant `:5178` (U13)

Fields: API secret, X-Signature placeholder, Collection ID, test|live select. Copy: “Reminder + hosted bill. We do not auto-debit. Callback must be public https (localhost will fail).” Webhook URL hint without query. No five-logo wall. Writer-only paste. Matches B11/U13 as UI. Does not send `success_url` on checkout create (see §3.2).

### 11.4 Checkout `:5179`

No provider picker. No wallet/FPX tiles. No Billplz JS. `email_required` gate. 400 start maps to a combined “callback base not public or email required” string — coarse, but it names the Billplz fail. Verifying poll: Billplz `redirect_url` fallback already includes `?status=verifying`. Success URL is not paid. Wrap copy on the page is the generic “completing payment on the processor is not the same as a success URL,” **not** the NP-GW-007 sentence “we cannot auto-debit this Billplz method.” Merchant page says reminder-only; buyer page does not. Rank for 10-honesty: buyer wrap-rails sentence missing when active rail is billplz.

---

## 12. B10–B29 versus live code versus live tests

Legend: **code** = Pay src does the thing. **test** = `Lazuar.Pay.Tests` locks it. Checklist `[x]` is what 015 claimed.

| ID | Goal | Code | Test | Checklist honesty |
|----|------|------|------|-------------------|
| B10 | Class, scoped, no adapter interface, no off-session/refund/DNS port | Yes | Isolation greps adapter/factory; no DNS grep | Mostly honest |
| B11 | PUT secret + xsig + collection + environment | Yes | Only as a setup step inside the paid test | PUT-without-environment / without-collection **unchecked** |
| B12 | test→sandbox, live→www, no lazuar.com inference | Yes | sandbox URI only | live host **unchecked** |
| B13 | POST bills, Basic, JSON fields, url+id | Yes | URI host only; **no** body/auth asserts | Over-claimed |
| B14 | callback `{public}/v1/webhooks/billplz/{orgId}?checkout_id=` | Yes | Webhook **request** includes query; create body **not** asserted | Over-claimed |
| B15 | localhost 400, no fiction rewrite, https only | **Yes** | **No** | **Lie.** Test name is the tell. |
| B16 | query then form then reference_1 then bill-id | query/form/ref1 **yes**; bill-id lookup **no** | query path only | Code short vs B16.1; tests shorter |
| B17 | `reference_1 = checkout.Id` | Yes | **No** LastBody assert | Over-claimed |
| B18 | Form HMAC, Ordinal, hex lower, fixed-time, 400 on bad sig | Yes | Happy-path HMAC only | Bad-sig **unchecked** |
| B19 | with-extra then without-extra | **Yes in src** | **No extra-fields fixture** | **Lie** |
| B20 | paid OR state=paid → fulfill | Yes | One document; no RCPT-/journal/replay | Partial |
| B21 | unpaid ignore, grain `unpaid:` | **Yes in src** | **No** | **Lie** |
| B22 | `paid:{billId}`, missing id 400 | Yes | Missing-id **unchecked**; paid EventId unasserted | Partial |
| B23 | no PublicDnsFallback | Yes | Isolation does not grep it | Code honest |
| B24 | no refund method | Yes | n/a (absence) | Honest |
| B25 | no off-session, hosted_link, copy | Yes | capability asserted on **stripe** GET | Copy lives in Vite |
| B26 | email required on start | Yes (shared + rail) | Chip missing-email only | Billplz/placeholder **unchecked** |
| B27 | collection required | Yes | Chip brand-id only | Billplz **unchecked** |
| B28 | the matrix in §10.3 | Partial | Partial | **Over-checked** |
| B29 | README + .env.example tunnel | Yes | n/a (docs) | Honest as docs |

---

## 13. Correct differences (not bugs)

These are Hub→Pay **intended** deltas. Do not “fix” them back to Hub.

1. **No `IPaymentGatewayAdapter`.** Two methods. Parse is a static helper next to the webhook switch.
2. **Provider string `billplz`**, not `BILLPLZ`. Path `/v1/webhooks/billplz/{orgId}`.
3. **`reference_1` is checkout id**, not subscription/tenant. No `reference_2` Hub type folklore.
4. **Unpaid is ignored**, not `PAYMENT_FAILED` published into a commerce bus Pay does not have.
5. **EventId `paid:{billId}` / `unpaid:{billId}`**, not `PAYMENT_COMPLETED:`.
6. **Query via `IQueryCollection`**, not `Query-*` headers.
7. **No estimated fees, no taxAmount** on parse. Amount match is minor units vs checkout.
8. **No `AllowInsecureBillplzCallback`.**
9. **No `App:BillplzEnvironment` process override.** Row `environment` owns www vs sandbox.
10. **No DNS connect hook.**
11. **Payer name** sent when the session has one.
12. **Same-handler fulfillment** in-process; rails do not journal.
13. **JSON create** copied from Hub (not a form POST). Keep it unless Billplz rejects JSON on a dogfood collection.
14. **Cancel URL omitted** — v3 has none.

---

## 14. Gaps that are still real (code or tests)

Ranked for the parent evaluation. This paper does not implement them.

### 14.1 Test gaps 015 named and then skipped (must write)

1. **Localhost start 400 without network** (B15 / B28). Override `Pay:PublicBaseUrl`. Assert no PSP HTTP. Also unit-test `TryPublicBase`.
2. **Unpaid ignore** (B21 / §7 matrix). HMAC-valid `paid=false` → `{ ignored: "unpaid" }`, zero docs, checkout open; then paid still fulfills.
3. **Extra-fields HMAC** (B19). Include extras, MAC with-extra; include extras, MAC without-extra only; wrong secret 400.
4. **Bad HMAC / missing `x_signature` / missing bill id** 400, zero docs.
5. **Replay** of the same paid form → `{ duplicate: true }`, still one `RCPT-`.
6. **Create body** assertions: Basic `apiKey:`, `collection_id`, `callback_url` query, `reference_1`, `amount` 1000, no `reference_2` Hub types, no `setupFutureUsage`.
7. **`environment: live`** → `www.billplz.com/api/v3/bills`.
8. **PUT** billplz without environment / without collection → 400.
9. **Start without email** and **placeholder `customer@example.com`** on billplz → 400.
10. **Join by `reference_1` only** (webhook path **without** query).
11. **Empty body** on `/v1/webhooks/billplz/{orgId}`.
12. Assert `RCPT-` + two-line journal on the paid path (clone Chip).

### 14.2 Code gaps vs the 015 map

1. **No `ProviderSessionId` / bill-id fallback join** (B16.1 last step). Query → form → `reference_1` then 400. Hub session-by-bill-id is not ported.
2. **Start does not refuse non-MYR** checkouts. Webhook hardcodes MYR and will 400-mismatch after the buyer has paid.
3. **Fulfillment does not fill `ProviderSessionId` if empty** (B22.1). Start usually does; still a missing belt.
4. **No Content-Type enforcement** on the Billplz webhook (JSON 400s only because HMAC fails). Optional.
5. **Buyer `:5179` wrap-rails sentence** does not say Billplz cannot auto-debit (merchant page does). Honesty, not HTTP.
6. **Merchant checkout create omits `success_url`**, so Billplz `redirect_url` is always localhost:5179 in the staff UI. Dogfood-only.
7. **IsolationTests** do not grep `PublicDnsFallback` / `ConnectCallback` / `Dns.GetHostAddressesAsync`. Absence is currently true; a later paste would not fail IsolationTests.

### 14.3 Not gaps (refuse)

- Do not add `IssueRefund` / Payment Orders.
- Do not add `ChargeOffSession` / Agreements v5.
- Do not port `PublicDnsFallback`.
- Do not rewrite localhost to `lazuar-local-dev.com`.
- Do not infer live from hostname.
- Do not book fees or SST.
- Do not JSON-parse Billplz callbacks.
- Do not put One tenant id in `reference_1`.
- Do not call live Billplz from `task pay:test`.

---

## 15. Wiring recap (so a later implementer does not invent a factory)

Start (`PublicPayEndpoints`):

```92:97:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        IHostedRail rail = name switch
        {
            PayProviders.Stripe => stripe,
            PayProviders.Chip => chip,
            PayProviders.Billplz => billplz,
```

Webhook:

```51:55:apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs
            parsed = name switch
            {
                PayProviders.Stripe => StripeWebhook.Parse(raw, request.Headers, cred, box, config, env),
                PayProviders.Chip => ChipWebhook.Parse(raw, request.Headers, cred, box),
                PayProviders.Billplz => BillplzWebhook.Parse(raw, request.Query, cred, box),
```

Billplz is the **only** arm that passes `request.Query`. That is the steal of Hub’s Query-* map, without the cathedral.

Csproj: Stripe.net only. Billplz is raw `HttpClient` + `System.Security.Cryptography`. No Billplz NuGet (there isn’t a real one to refuse). IsolationTests ban `Razorpay.Api`; nothing Billplz-shaped to ban except the Hub types already listed.

---

## 16. Tunnel dogfood (B29) — docs only, still the operational constraint

Billplz **server** must POST `callback_url`. Loopback is unreachable from Billplz. Live fail-closed is correct.

What a human must set, from README + `.env.example` (not from code comments):

- One on 8080. Hub **off**. Pay on 8081.
- `Pay__PublicBaseUrl=https://<cloudflare-or-similar>` forwarding to 8081.
- PUT billplz `environment=test` + sandbox API key + collection id + X-Signature secret.
- Collection / per-bill callback lands on `{PublicBaseUrl}/v1/webhooks/billplz/{orgId}`.
- Buyer may still return to `http://localhost:5179/c/{token}?status=verifying` (browser, not Billplz).
- CI does not talk to Billplz. `PayApiFactory` uses `https://pay.test.example` + `FakePspHandler`.

Do not add `/etc/hosts` fiction. Do not claim the paid RailTests method is a tunnel test; it never leaves the in-memory factory.

---

## 17. Sentence the parent evaluation may steal

On `c621ceba`, Pay’s Billplz rail **is** the Hub HTTP extract 015 described: Basic `{key}:` JSON `POST {sandbox|www}/api/v3/bills`, collection id from `public_merchant_id`, `callback_url` with `checkout_id` query, `reference_1` = checkout id, public-https fail-closed, form HMAC with-extra then without-extra, `paid:` / `unpaid:` grains, ignore unpaid, no refund method, no off-session method, no `PublicDnsFallback`. The **tests do not match the checklists.** One method named `Billplz_paid_form_and_localhost_blocked` locks sandbox host + paid form fulfill and **does not** lock localhost, extra-fields HMAC, or unpaid. Treat B15/B19/B21/B28 `[x]` as map, not proof. Write those three fixtures (plus create-body Basic/`callback_url`/`reference_1`) before calling the Billplz slice done.

---

## 18. Opened-line index (for later greps)

Hub generate client: `CreateClient(PublicDnsFallback.HttpClientName)` + Basic `{apiKey}:` + `JsonContent` + `{endpoint}bills`.  
Hub extra fields: `paid_at`, `transaction_id`, `transaction_status`. Always exclude `x_signature`.  
Hub unpaid EventId: `PAYMENT_FAILED:{billId}`.  
Pay generate client: `CreateClient("billplz")` + Basic `{apiKey}:` + `JsonContent` + `{host}bills`.  
Pay extra fields: identical three names.  
Pay unpaid EventId: `unpaid:{billId}`, `Ignored = true`.  
Pay paid EventId: `paid:{billId}`.  
Pay public base error string: `"callback base not public"` (must stay, Start greps `"callback base"`).  
Pay test that exists: `RailTests.Billplz_paid_form_and_localhost_blocked`.  
Pay test factory public base: `https://pay.test.example`.
