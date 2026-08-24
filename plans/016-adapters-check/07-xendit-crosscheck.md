# 07 — Xendit cross-check: Hub invoices vs Pay `XenditHosted` + `XenditWebhook`

**Date:** 24 August 2026  
**Branch:** `feat/015-four-adapters`  
**HEAD:** `c621ceba7fc7b79f16954d0819200cb21db6f22b` — `docs(015): check off implemented T–Q phases`  
**Slice:** Xendit only. HTTP judgment against Hub. Not an implementation. Not a project reference into `apps/lazuar-api`.  
**Parent index:** [README.md](./README.md). 015 map: [015-four-adapters](../015-four-adapters/README.md), especially [00 §5.3](../015-four-adapters/00-what-must-be-done.md) and checklists X10–X23.

Live code is authority. 015 checkboxes are a map, not proof. Several X23 boxes are checked while the corresponding fixture does not exist.

---

## 0. What this file opened

Hub (steal HTTP; do not copy types):

- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/XenditGatewayAdapter.cs` (full, 451 lines)
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs` (`ToMinorUnitsRounded`, `TryNormalizeCurrency`, `TryResolveEmail`)
- `apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` (intake, unique, SETTLED collision)
- `apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/XenditGatewayAdapterTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/PaymentGatewayCapabilitiesTests.cs`

New host:

- `apps/lazuar-pay/src/Lazuar.Pay/Gateways/XenditHosted.cs` (full)
- `apps/lazuar-pay/src/Lazuar.Pay/Gateways/XenditWebhook.cs` (full)
- `apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs` (shared TX + amount/currency match)
- `apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs` (PUT secret + callback token)
- `apps/lazuar-pay/src/Lazuar.Pay/Gateways/PayProviders.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Gateways/BuyerEmail.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Gateways/IHostedRail.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Gateways/PspParseResult.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/MoneyMath.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs`, `PayDbContext.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs`, `CheckoutStore.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/RailTests.cs` (`Xendit_paid_and_settled` and neighbours)
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/WebhookTests.cs`, `GatewayTests.cs`, `PublicPayTests.cs`, `PayApiFactory.cs`, `FakePspHandler.cs`

Frontends:

- `apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx`
- `apps/lazuar-pay-checkout/src/App.tsx`
- `apps/lazuar-pay-checkout/src/locks.test.ts`

015 Xendit phases (all opened): `x10-xendit-class.md` through `x23-xendit-tests.md`, plus `u14-xendit-fields.md`, `p11`, `p19`, `p20`, `p23`, `k12`, `decisions.md`, `00-what-must-be-done.md` §5.3.

Context that is judgment, not copy: [014/07 §5](../014-evals/07-sea-later-rails.md) (Hub HTTP extract), issue 073 (callback token is a shared secret), issue 227 (placeholder email), issue 225 (`xendit_payment_methods` unused), issue 062 (paying tenant_id — Hub-only, must not be ported as xenPlatform).

---

## 1. Product shape (what Pay is allowed to be)

015 freeze (`checklists/decisions.md`):

- Xendit is a **hosted_link** wrap of the **legacy Invoice API** `POST /v2/invoices`. Reminder-only. Capability string is `hosted_link` for all five rails.
- Verbs: `CreateHostedUrl` + verify webhook + `Fulfillment.FulfillPaidAsync`. No refund, portal, off-session, factory, registrar, DNS fallback.
- Fulfill **PAID only**. SETTLED / PENDING / EXPIRED / FAILED = 200 ignored, no second journal.
- Amount to Xendit in **major units** (Hub `ToMinorUnitsRounded / 100m`). Do not send Stripe-style cents.
- Callback token header equals stored secret. Not HMAC, not RSA, not Stripe EventUtility.
- No xenPlatform (`for-user-id`, application fees, split settlement).
- Do **not** copy Hub `payment_methods` / `xendit_payment_methods` / `MalaysiaHostedChannels`. Wallets stay on Xendit’s page.
- Email required. Never `customer@example.com`.
- Missing currency fail-closed. Do not invent MYR.
- Fees / processor tax are not booked. `unknown ≠ 0`.
- `:5179` has no GrabPay / TnG / Boost / DuitNow / FPX tiles.

Hub class comment (live, accurate, steal the job not the type):

```csharp
/// BYOK wrap of Xendit hosted invoices. Money settles on the tenant Xendit account.
/// Reminder-only until a payment-token soak proves off-session. We do not rebuild wallets.
```

`IHostedRail` on 8081 is two methods of surface (`Provider` + `CreateHostedUrlAsync`). Parse lives in a static `XenditWebhook`. That is the intended seam. There is no `IPaymentGatewayAdapter` in Pay source (IsolationTests bans the token). Grep of `apps/lazuar-pay/src` for `for-user-id`, `xenPlatform`, `xenplatform`, `payment_methods`, `IssueRefund`, `ChargeOffSession`, `setupFuture` is empty. That is the refuse list holding in code, not just in checklists.

Xendit the company also sells Payment Sessions (`POST /sessions`), Payment Tokens, Subscriptions, and xenPlatform. 014 already warned that Xendit’s own docs (updated Jul 2026) tell merchants to migrate `/v2/invoices` to sessions. Pay copied the **legacy invoice job** on purpose. A later ticket that “upgrades” to sessions without re-reading docs will inherit a sunset. Out of this slice.

---

## 2. Files and wiring on 8081

| Piece | Path | Role |
|-------|------|------|
| Hosted create | `Gateways/XenditHosted.cs` | `IHostedRail`, `Provider = "xendit"`, `POST https://api.xendit.co/v2/invoices` |
| Webhook parse | `Gateways/XenditWebhook.cs` | Static `Parse`. Callback token, PAID/SETTLED/other, currency, amount, checkout id |
| Dispatch start | `PublicPay/PublicPayEndpoints.cs` | `PayProviders.Xendit => xendit` |
| Dispatch webhook | `Gateways/WebhookEndpoints.cs` | `PayProviders.Xendit => XenditWebhook.Parse(...)` |
| PUT keys | `Gateways/GatewayEndpoints.cs` | secret + `webhook_secret`; reject `public_merchant_id` |
| Named client | `Program.cs` | `AddHttpClient("xendit")`, `AddScoped<XenditHosted>()` |
| Allow-list | `PayProviders.cs` | `"xendit"` lowercase; `RequiresEmail = true`; `AllowsPublicMerchantId = false` |

Hub contrast: one class implements `IPaymentGatewayAdapter` with generate + parse + refund + portal-throw + off-session-false. Factory in `DependencyInjection.cs`. GatewayType `"XENDIT"` uppercase. Pay lowercases the name in path, PK, and JSON (`decisions.md` lock). Do not treat Hub `XENDIT` as a string to copy.

---

## 3. Invoice HTTP (`POST /v2/invoices`)

### 3.1 Hub live extract

`XenditGatewayAdapter.GenerateCheckoutAsync` → `BuildInvoicePayload` → unnamed `IHttpClientFactory.CreateClient()` →

```
POST https://api.xendit.co/v2/invoices
Authorization: Basic base64(apiKey.Trim() + ":")
Content-Type: application/json
```

There is **no test host**. Tenant `environment=test` does not change the URL. Test vs live is the **key prefix** (`xnd_development_…` vs `xnd_production_…`) on Xendit’s side. Pay inherits this: `XenditHosted.ApiBase = "https://api.xendit.co"` and `cred.Environment` is never read. PUT still stores `environment` default `"test"`. Dead field for this rail. Harmless. Do not invent `api.xendit.co` vs a sandbox hostname the way Billplz does.

Auth: HTTP Basic, username = secret API key, **empty password**. Hub `BasicAuth` trims the key. Pay concatenates `box.Unprotect(cred.Ciphertext) + ":"` without a second trim. PUT already `Trim()`s `secret` before `SecretBox.Protect`, so the stored plaintext is trimmed. Equivalent for the happy path.

Success: JSON `invoice_url` + `id`. Missing `invoice_url` → Hub `GatewayCheckoutResult` false with `"Xendit returned no invoice_url."` Pay throws `"Xendit returned no URL"` → PublicPay maps `InvalidOperationException` to **503** (unless the message contains `"callback base"`, which this does not). Non-2xx: Hub logs status+body and returns the body in the error string. Pay throws `"Xendit rejected the org key"` and **swallows the body**. Buyer/start sees 503 `detail: "Xendit rejected the org key"` for a 400 validation error (bad amount, unpaid KYC, currency not enabled) the same as a 401. Intentional opacity vs Hub. Ops debugging a first Xendit invoice will need server logs; Pay currently does not log the PSP body on this path.

`setupFutureUsage` and `merchantId` are discarded on Hub (`_ =`). Pay has no such flags on `IHostedRail`. X13 holds.

### 3.2 Payload: Hub vs Pay, field by field

Hub `BuildInvoicePayload` (live):

```csharp
var line = GatewayCommon.ToMinorUnitsRounded(amount, quantity) / 100m;
GatewayCommon.ApplyPayingTenantMetadata(metadata, tenantId);

var payload = new Dictionary<string, object>
{
    ["external_id"] = "lazuar_" + Guid.CreateVersion7().ToString("N"),
    ["amount"] = line,
    ["currency"] = GatewayCommon.TryNormalizeCurrency(currency, out var iso)
        ? iso
        : throw new InvalidOperationException("Currency is required."),
    ["description"] = GatewayCommon.ProductDescription(productName, quantity),
    ["payer_email"] = GatewayCommon.TryResolveEmail(customerEmail, out var buyerEmail, out _)
        ? buyerEmail
        : throw new InvalidOperationException("Customer email is required."),
    ["success_redirect_url"] = successUrl,
    ["failure_redirect_url"] = cancelUrl,
    ["metadata"] = metadata
};

var methods = ResolveRequestedPaymentMethods(metadata);
if (methods.Count > 0)
    payload["payment_methods"] = methods;
```

Pay `XenditHosted.CreateHostedUrlAsync` (live):

```csharp
var payload = new Dictionary<string, object?>
{
    ["external_id"] = checkout.Id,
    ["amount"] = MoneyMath.FromMinor(MoneyMath.ToMinor(checkout.Amount)),
    ["currency"] = currency, // TryNormalizeCurrency already
    ["description"] = "Pay",
    ["payer_email"] = checkout.PayerEmail!.Trim(),
    ["success_redirect_url"] = checkout.SuccessUrl ?? "http://localhost:5179/c/" + checkout.PublicToken + "?status=verifying",
    ["failure_redirect_url"] = checkout.CancelUrl ?? "http://localhost:5179/c/" + checkout.PublicToken,
    ["metadata"] = new Dictionary<string, string>
    {
        ["checkout_id"] = checkout.Id,
        ["org_id"] = checkout.OrgId
    }
};
```

No `payment_methods` key is ever inserted. No `should_send_email`, `items`, `fees`, `callback_url` (Xendit invoice callbacks are dashboard-configured, not per-invoice in this wrap). No `for-user-id` header on the `HttpRequestMessage`.

### 3.3 Amount units — this is the money line

Xendit Invoice API wants **major units** (RM 10.50 is `10.50`, not `1050`). Stripe, CHIP, Billplz, and Razorpay on this host send **minor units**. Mixing them is how a merchant invoices RM 10.00 as RM 1,000.00 or as RM 0.10.

Hub policy, live:

```csharp
// GatewayCommon
public static long ToMinorUnits(decimal amount, string? currency = "MYR", int quantity = 1)
{
    var qty = quantity < 1 ? 1 : quantity;
    var factor = IsZeroDecimalCurrency(currency) ? 1m : 100m;
    return (long)Math.Round(amount * qty * factor, 0, MidpointRounding.AwayFromZero);
}

public static int ToMinorUnitsRounded(decimal amount, int quantity = 1) =>
    (int)ToMinorUnits(amount, "MYR", quantity);
```

Then `line = ToMinorUnitsRounded(amount, quantity) / 100m`.

Consequences of the Hub helper:

1. Rounding is **half away from zero**, through integer sen, then divided back to a decimal with at most two places.
2. Quantity is folded into the line **inside minor units**, then divided. RM 10 × qty 2 → 2000 sen → `20.00`.
3. `ToMinorUnitsRounded` **hard-codes currency `"MYR"`**, so the ×100 factor applies even when the invoice currency is IDR/JPY/VND. The zero-decimal table in `ToMinorUnits` is unused on this path.
4. Return type is **`int`**. Overflow around ~RM 21,474,836.

Pay policy, live:

```csharp
public static long ToMinor(decimal amount) =>
    (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

public static decimal FromMinor(decimal cents) => cents / 100m;
```

Create sends `FromMinor(ToMinor(checkout.Amount))`. For qty=1 MYR this is the same round-trip as Hub: RM 10.125 → 1012.5 → 1013 sen → `10.13`.

X12.2 said: “Round AwayFromZero via cents then `/ 100m` like Hub — do not send raw float without that policy.” Pay did that. It did **not** send `checkout.Amount` raw. Good.

Differences that remain:

| | Hub | Pay |
|--|--|--|
| Quantity | folded in | Pay checkouts have no quantity; merchant sends the total |
| Zero-decimal ISO | ignored (always ×100 then /100) | ignored (always ×100 then /100) |
| Overflow | `int` sen | `long` sen |
| SST | none on this payload | none |
| Storage | Commerce amount decimal | `checkouts.amount` `decimal(18,2)` |

Webhook compare on 8081 is **not** “PSP major vs checkout major as decimals”. `WebhookEndpoints` does:

```csharp
if (parsed.AmountMinor is not null && parsed.AmountMinor.Value != MoneyMath.ToMinor(checkout.Amount))
    return 400 "amount mismatch";
```

`XenditWebhook` sets `AmountMinor = MoneyMath.ToMinor(amount)` where `amount` is `paid_amount` else `amount` as a JSON number (major). So both sides go through `ToMinor`. A create that rounded 10.125 → 10.13 on the wire will still match a checkout stored as 10.125 if InMemory keeps extra precision (`ToMinor(10.125)=1013` and `ToMinor(10.13)=1013`). Postgres `numeric(18,2)` will store 10.13. Either way the sen match. Do not “simplify” create to send `checkout.Amount` without the round-trip; a later reader will send cents.

**Must not:** send `MoneyMath.ToMinor(checkout.Amount)` as `"amount"` the way `RazorpayHosted` / `ChipHosted` / `BillplzHosted` do. That would be RM 10 → Xendit amount 1000 → a thousand ringgit invoice. No test currently inspects `factory.Psp.LastBody` for the Xendit create. See §14.

IDR: a checkout of `10000` IDR sends `10000.00` after the round-trip. Xendit IDR invoices typically want whole rupiah. Extra `.00` is usually accepted. JPY/VND same Hub leftover. Not a Pay regression; do not “fix” zero-decimal in this rail without a soak.

### 3.4 `external_id` and metadata (join keys)

Hub `external_id` is `"lazuar_" + Guid.CreateVersion7("N")` — **new every generate**, not the checkout id. 014 called this a lost join. Metadata on Hub carries Commerce keys (`tenant_id`, `subscription_id`, …) plus `ApplyPayingTenantMetadata` (issue 062: do not clobber paying workspace with the system org).

Pay `external_id` **is** `checkout.Id`. Metadata is only `checkout_id` + `org_id`. X12 explicitly: “do not require Hub `external_id` `lazuar_` prefix; checkout id is enough.” Correct for a host that has no platform checkout / `SystemOrganizationId`.

Webhook checkout resolution (Pay):

```csharp
checkoutId from invoice.metadata.checkout_id
checkoutId ??= invoice.external_id
```

Both are the same string on a Pay-created invoice. If Xendit drops metadata (it generally does not for invoices, unlike Billplz), `external_id` still joins. Hub cannot say that; Hub’s `external_id` is a random `lazuar_` uuid.

`ApplyPayingTenantMetadata` / `platform_tenant_id` is **not** copied. 013 parked Hub platform checkout. Do not reintroduce it as a Xendit header.

### 3.5 Redirect URLs

Pay defaults, same string as CHIP/Stripe on this host:

- success → `http://localhost:5179/c/{publicToken}?status=verifying`
- failure → `http://localhost:5179/c/{publicToken}`

K14 / 015: processor success URL is **not** paid. `:5179` polls when `?status=verifying`. Xendit `success_redirect_url` is a browser hop after the hosted invoice; fulfillment is the webhook. Copy on checkout already says so.

If `CreateCheckoutRequest.SuccessUrl` is set, that wins. Merchant `:5178` `createProductAndLink` does **not** send `success_url`. Local dogfood therefore redirects buyers to `:5179`. Production merchants who create checkouts via API without `success_url` will also bounce to localhost. Host-wide, not Xendit-specific. Billplz is the rail that **must** fail-closed on localhost (callback URL). Xendit invoice callbacks are dashboard-configured to `POST /v1/webhooks/xendit/{orgId}` — the create payload has **no** `callback_url`. Local Xendit dogfood still needs a **public HTTPS** webhook endpoint in the Xendit dashboard, same as Hub. Merchant copy for Xendit does **not** say this (Billplz copy does: “Callback must be public https”). Operational footgun: staff paste keys, create a link, buyer pays, webhook never arrives because `localhost:8081` is not reachable from Xendit.

### 3.6 Description / items / email-from-Xendit

Hub sends `ProductDescription(productName, quantity)` (`"Plan (x3)"` or `"Lazuar Payment"`). Pay hardcodes `"Pay"`. Fine for a wrap. Xendit’s hosted page will title the invoice “Pay”. Merchant product name on `:5178` is not forwarded onto the Xendit invoice. Honesty: the Official Receipt is `RCPT-` from Pay’s ledger, not Xendit’s PDF.

`payer_email` is required. Xendit’s Invoice API **defaults `should_send_email` to true**. Creating an invoice therefore emails the buyer from Xendit’s template **and** `:5179` redirects them. Hub did the same. Pay does not set `should_send_email: false`. Reminder-only product can live with the processor email. Do not call that “we built dunning.”

### 3.7 `payment_methods` — do not copy (X12 / X20 / issue 225)

Hub allow-list `MalaysiaHostedChannels`:

```
CREDIT_CARD, DD_FPX, QR_CODE, OVO, DANA, LINKAJA, SHOPEEPAY, GCASH, GRABPAY, PAYMAYA
```

Only attached when metadata key `xendit_payment_methods` is set. Commerce/portal **never set that key** (issue 225). Production Hub invoices use **dashboard defaults**. Test `BuildInvoicePayload_FiltersUnknownChannels` locks the unused filter. Capability `SupportsHostedWallet("XENDIT","GRABPAY")` is true and has **zero generate-path readers**. TNG / BOOST / DUITNOW are in the capability matrix and **not** in `MalaysiaHostedChannels`. DuitNow QR is `QR_CODE`, not `DUITNOW`. 014 named this mismatch.

Pay copies **none** of it. `XenditHosted` never writes `payment_methods`. Isolation/grep: no `payment_methods` under `Lazuar.Pay`. Wallets that appear are whatever the merchant enabled on the Xendit invoice / payment-link settings. That is the honest wrap.

Do not “complete” Xendit later by porting `MalaysiaHostedChannels`. Do not render hop-1 tiles from `SupportsHostedWallet`. See §11.

### 3.8 xenPlatform — do not copy (X21)

Hub invoice request has no `for-user-id` header, no xenPlatform split, no application fee. Money settles on the **tenant** Xendit account. Class comment says so.

Pay: no such headers. `HttpRequestMessage` only sets `Authorization` and JSON body. X21 checklist “grep Pay Gateways for `xen` / `for-user-id` — none” holds on this SHA.

Hub issue 062 (CHIP/Xendit clobber paying `tenant_id`) is a **platform-checkout** story. Pay has no `SystemOrganizationId` credits. Metadata is `org_id` = the checkout’s org. Do not invent `platform_tenant_id` here. Do not add `for-user-id` “so SaaS can take 2%.” That is tracker trap `PY-022` / xenPlatform envy (007/06).

### 3.9 Refunds / off-session / portal — parked

Hub still has them:

- `IssueRefundAsync`: GET invoice, prefer `payment_id`, POST `/refunds` with `Idempotency-key: lazuar-refund:…`. Unsoaked (issue 071). `SupportsApiRefund("XENDIT")` true.
- `ChargeOffSessionAsync`: always `false`. Test `ChargeOffSession_AlwaysFalse_UntilTokenSoak`.
- `GenerateCustomerPortalAsync`: throws.

015 verbs lock: Pay does not implement any of these. `IHostedRail` cannot. A later refund ticket must GET the invoice for a `py-` id because `ProviderSessionId` / `Charge.ProviderRef` will be the **invoice** id (`XenditHosted` returns `id` from create; webhook `ProviderRef` is invoice id). Do not POST Stripe PaymentIntent ids. Do not enable refunds by copying Hub `BuildRefundPayload` without a sandbox soak.

---

## 4. Callback token (X14, issue 073)

Xendit does **not** HMAC the body. The dashboard issues a **callback token**. Every invoice webhook includes header `x-callback-token`. Verification is “does this header match the stored secret?” Stolen token + any JSON with `status=PAID` and a new `id` is a paid event. No timestamp, no `t=` window. That is the processor’s model, not a Pay bug. Mitigations that belong here: HTTPS, rotate the token, idempotency `(org, xendit, event_id)`, amount/currency match, checkout already-paid no-op. Do not invent a body HMAC Xendit does not send.

### 4.1 Hub live (post-073)

```csharp
internal static bool VerifyCallbackToken(string webhookSecret, Dictionary<string, string> headers)
{
    if (string.IsNullOrWhiteSpace(webhookSecret)) return false;
    var headerKey = headers.Keys.FirstOrDefault(k => k.Equals(CallbackTokenHeader, StringComparison.OrdinalIgnoreCase));
    if (headerKey == null || !headers.TryGetValue(headerKey, out var presented) || string.IsNullOrEmpty(presented))
        return false;

    // Hash first so a length mismatch is still constant-time. Xendit does not
    // send a body HMAC — this is a shared callback token, not a signature.
    var expected = SHA256.HashData(Encoding.UTF8.GetBytes(webhookSecret));
    var actual = SHA256.HashData(Encoding.UTF8.GetBytes(presented));
    return CryptographicOperations.FixedTimeEquals(expected, actual);
}
```

Issue 073 (resolved): length-mismatch compare was not constant-time. Fix was **SHA-256 both sides then FixedTimeEquals**, so the compared buffers are always 32 bytes. Test `VerifyCallbackToken_LengthMismatch_IsNotVerified`. Empty stored secret → false. Missing header → false. `ParseWebhook_MissingToken_IsNotVerified` asserts `Verified=false` and error contains `x-callback-token`. Header name is case-insensitive (`X-Callback-Token` in the EXPIRED fixture).

`apiKey` is unused at parse (`_ = apiKey`). Fee estimate args unused (handler always passes 0,0,0).

Hub intake: `!Verified` without `UnusableAfterVerify` throws `Webhook signature verification failed` (historically 500). Missing config throws `Webhook secret not configured` (issue 311, 500). Missing currency / missing id → `AsUnusable()` → 400 so Xendit stops.

### 4.2 Pay live

`XenditWebhook.Parse`:

1. Empty `cred.WebhookCiphertext` → `InvalidOperationException("webhook secret missing")` → WebhookEndpoints maps to **503**.
2. Scan `headers.Keys` for `x-callback-token` OrdinalIgnoreCase. ASP.NET’s header dictionary is already case-insensitive; the loop still works.
3. UTF-8 bytes of provided vs `box.Unprotect(cred.WebhookCiphertext)`.
4. **`if (left.Length != right.Length || !FixedTimeEquals(left, right))` throw `PspVerifyException("invalid signature")` → 400.**

X14 checklist text: “UTF-8 bytes, length check, `CryptographicOperations.FixedTimeEquals`.” That is what shipped. It is **not** Hub’s 073 fix. A length mismatch returns false **before** `FixedTimeEquals`. An attacker who can time the 400 vs a longer compare learns the stored token length. The token is a shared secret with no body binding; length leak is the smaller part of 073, but Pay reintroduced the length-mismatch timing hole Hub closed.

Empty body: `WebhookEndpoints` reads raw **first**, whitespace-only → 400 `"empty body"` **before** the switch and before `XenditWebhook.Parse`. P23 holds for all five names including `xendit`. No 500 on empty + missing header.

Missing / mismatch token: 400 `invalid signature`. X14 “Do not 500. Do not use Stripe EventUtility.” Holds. Message is generic `"invalid signature"`, not Hub’s `"Missing or invalid x-callback-token."` Same class.

Unprotect uses `SecretBox` AES-GCM. PUT encrypts `webhook_secret` into `WebhookCiphertext`. GET never echoes it (`webhook_configured` boolean only). Last4 is the **API secret** last4, not the token.

No test on 8081 asserts bad callback token → 400 for Xendit. Stripe `Invalid_signature_is_400` and CHIP RSA tests exist. See §14.

### 4.3 PUT requires the token (X11 vs Hub 311)

Hub ops/admin first-save of Xendit historically allowed a secret key without a callback token (issue 311). First paid invoice then 500s “secret not configured.”

Pay `GatewayEndpoints.Put`: empty `webhook_secret` → 400 `"webhook_secret is required"` for **every** provider including xendit. `GatewayTests.Put_requires_webhook_secret` locks this on Stripe JSON; the branch is shared. Merchant `:5178` always sends `webhook_secret` in the PUT body (may be `""` if the box is empty — host still 400s). X11 “Require `secret` and `webhook_secret`” holds on the host. No Xendit-specific PUT test. `public_merchant_id` non-empty for xendit → 400 `"public_merchant_id is not used for this provider"` (`AllowsPublicMerchantId` is CHIP/Billplz only). Merchant UI does not render Brand ID for xendit. Untested for the xendit name.

---

## 5. PAID vs SETTLED vs EXPIRED vs PENDING (the journal line)

### 5.1 Hub `MapStatus` (dangerous for SETTLED)

```csharp
internal static string? MapStatus(string status)
{
    var s = status.Trim();
    if (s.Equals("PAID", ...) || s.Equals("SETTLED", ...) || s.Equals("invoice.paid", ...))
        return "PAYMENT_COMPLETED";
    if (s.Equals("EXPIRED", ...) || s.Equals("FAILED", ...) || s.Equals("invoice.expired", ...) || s.Equals("invoice.failed", ...))
        return "PAYMENT_FAILED";
    return null; // PENDING and everything else
}
```

EventId is `$"{mapped}:{invoiceId}"`:

- PAID → `PAYMENT_COMPLETED:inv_1`
- SETTLED → `PAYMENT_COMPLETED:inv_1`  **same EventId**
- EXPIRED → `PAYMENT_FAILED:inv_1`

Hub `ProcessGatewayWebhookCommandHandler` persists only those EventTypes. `PENDING` is verified ACK, empty EventId, **no log**. Issue 223 residual: do not fulfill unpaid.

PAID then SETTLED on Hub: same EventId **and** same mapped type, so the unique log / business key `PAYMENT_COMPLETED:{invoiceId}` should ACK the second as duplicate. That is accidental safety, not “we ignored SETTLED.” If a future Hub change namespaced `SETTLED` separately while still mapping it to `PAYMENT_COMPLETED`, Billing would **second-journal**. 015 X16 exists because of that.

`invoice.settled` as an **event name** is **not** in Hub `MapStatus`. Unwrap is `status` from `data` else root `event`. A modern envelope `{ event: "invoice.settled", data: { status: "SETTLED", id } }` still maps via `data.status`. A body with only `event: "invoice.settled"` and no status would fall through to `MapStatus("invoice.settled")` → **null** → passthrough ignore. Inconsistent.

EXPIRED on Hub is `PAYMENT_FAILED`. That can mark a Commerce checkout failed. A later PAID on the same invoice would be a **different** EventId (`PAYMENT_COMPLETED:inv` vs `PAYMENT_FAILED:inv`) and could fulfill after fail (handler has the inverse guard: late FAILED after COMPLETED is ignored; late COMPLETED after FAILED is not obviously blocked in the snippet). Typical Xendit invoices do not resurrect after EXPIRED.

Amount: prefer `paid_amount` else `amount`, major units. Fee: `fees_paid_amount` if numeric, else 0. Tax 0. Net = amount − fee. Currency uppercased **without** a 3-letter check (blank refused; `"MY"` would pass). Metadata copied plus `external_id`. `GatewayTransactionId` = invoice id.

Missing id / missing currency: `Verified=false` + `AsUnusable()`. Test `ParseWebhook_PaidWithoutCurrency_DoesNotInventMyr`. Test `ParseWebhook_Paid_MapsCompleted` locks EventId `PAYMENT_COMPLETED:inv_paid_1`, amount 50m, currency MYR. Test `ParseWebhook_Expired_MapsFailed` locks `PAYMENT_FAILED` for EXPIRED.

Hub **does** book `GatewayFee` when Billing sees `GatewayFee > 0`. Xendit is one of the few adapters that actually parse a processor fee field (`fees_paid_amount`). Whether production invoices populate it is a soak question. Pay must **not** copy that into a journal fee line (X15.2, freeze “Fees / processor tax: Do not book”).

### 5.2 Pay `XenditWebhook` (intentional divergence)

Order of operations after token verify:

1. `JsonDocument.Parse`. Unwrap `data` if object (same as Hub).
2. `status` = invoice `status` else root `event` else `""`.
3. Invoice `id` **required** even for ignored events. Missing → 400 `"missing invoice id"`. Hub skips id check when `MapStatus` is null (PENDING without id still ACK). Pay is stricter. Fine.
4. **SETTLED / `invoice.settled` → `{ EventId: "settled:"+id, Ignored: true, IgnoreReason: "settled" }`.** Returns **before** currency/amount. SETTLED without currency is still 200 ignored, not 400-retry-forever.
5. If not PAID / `invoice.paid` → `{ EventId: status+":"+id, Ignored: true, IgnoreReason: status }`. PENDING, EXPIRED, FAILED, `invoice.expired`, `invoice.failed`, `invoice.created`, garbage — all 200 ignored. **No `PAYMENT_FAILED`.** Checkout stays `open`. No `RCPT-`. Does not consume `paid:{id}`.
6. Currency via `MoneyMath.TryNormalizeCurrency` (non-blank, trim, upper, **length == 3**). Fail → 400 `"missing currency"`. No MYR default.
7. Amount: `paid_amount` else `amount`, JSON **number** only. String amounts stay 0 → later amount mismatch 400 if checkout ≠ 0. Fail-closed.
8. Checkout id: metadata `checkout_id` else `external_id`.
9. Paid result: `EventId = "paid:"+invoiceId`, `ProviderRef = invoiceId`, `AmountMinor = ToMinor(amount)`, `Currency` normalized.

WebhookEndpoints after parse:

- Duplicate `(orgId, provider, EventId)` → `{ duplicate: true }` 200.
- `parsed.Ignored` → insert event row, `{ ignored: reason }` 200. **Does not call fulfill.**
- Missing checkout id / wrong org → 400 `"checkout not found"`.
- Currency mismatch vs checkout (OrdinalIgnoreCase) → 400.
- Amount sen mismatch → 400.
- Else one DB transaction: insert event + `FulfillPaidAsync(checkout.Id, "xendit", invoiceId)`.

`FulfillPaidAsync` itself: amount ≤ 0 return; `status != "open"` return; else mark paid, charge, optional payer, optional subscription if interval mo/yr, **two journal lines cash D + revenue C at `checkout.Amount`**, `RCPT-{year}-{n}`, audit `checkout.paid`. No tax line. No fee line. No `fees_paid_amount`.

### 5.3 SETTLED must not second-journal — live verdict

Pay **does not map SETTLED to paid**. EventId namespace is `settled:` vs `paid:`. Ignored branch never calls fulfill.

Defense in depth if someone later “fixes” SETTLED to fulfill:

- Unique `(org, xendit, paid:inv)` does **not** stop `settled:inv` (different EventId).
- `FulfillPaidAsync` no-ops when checkout is already `paid`.
- So PAID-then-SETTLED still yields one document **even if SETTLED is wrongly fulfilled**.

**The only existing test is exactly that weak case.** `RailTests.Xendit_paid_and_settled` does PAID (fulfills) then SETTLED and asserts `Documents.Count() == 1`. It does **not** assert the SETTLED body contains `"ignored"` / `"settled"`. It does **not** assert journal line count. It does **not** send SETTLED **alone**. A regression that fulfills SETTLED the same as PAID would still pass this test.

X16.1 even admits: “If PAID already inserted `paid:{invoiceId}`, SETTLED must not mint a second receipt even if you mistakenly fulfill — unique + status≠open saves you; still do not call fulfill.” The fixture only locks the safety net, not the ignore.

**SETTLED-only operational footgun (intentional):** if a merchant enables only “invoice settled” in the Xendit dashboard and not “invoice paid”, Pay will 200-ignore forever and never write `RCPT-`. Hub would have fulfilled SETTLED as `PAYMENT_COMPLETED`. 015 chose PAID-only. Merchant `:5178` copy does not say “enable the invoice **paid** callback, not only settled.” Add that to honesty/copy, not to MapStatus.

### 5.4 EXPIRED / FAILED / PENDING

X17: 200 ignored, no receipt, do not consume `paid:{id}`. Live code does this. X17.2 and X23.1 claim “One fixture (EXPIRED) green.” **There is no EXPIRED test in `Lazuar.Pay.Tests`.** Hub has `ParseWebhook_Expired_MapsFailed` (opposite mapping). Pay should assert: verified token, `{ ignored: "EXPIRED" }` (or whatever the status string is), `Documents.Count==0`, checkout `open`, then a later PAID with EventId `paid:inv` still fulfills (proves the grain was not consumed). Untested.

PENDING: ignored. Untested. Correct not to fulfill.

`invoice.paid` event name: treated as paid. Untested. The `data` wrapper is implemented; no fixture uses it.

### 5.5 Event id (X18)

Paid: `paid:{invoiceId}`. Not Hub’s `PAYMENT_COMPLETED:{invoiceId}`. 015 freeze: “Namespaced (`paid:{id}`). Never bare object id for fail-then-pay.” Bare invoice id would collide SETTLED/EXPIRED/PAID. Pay does not use a bare id.

Missing id: 400, not ignored. X18.2 “Covered by X15” — X15’s only fixture is the happy PAID JSON with an id. Missing-id is **not** covered.

Replay PAID: WebhookEndpoints FindAsync on `(t1, xendit, paid:inv_1)` returns `{ duplicate: true }`. Chip test asserts this. **Xendit test never replays PAID.** X23.1 “PAID → `RCPT-` + replay” is overclaimed: no `RCPT-` prefix assert, no replay.

---

## 6. Currency fail-closed (X19)

Hub generate: `TryNormalizeCurrency` or throw `"Currency is required."` No `"MYR"` fallback on generate. (Older audits described `(currency ?? "MYR")` on generate; **live Hub does not invent**.)

Hub webhook: blank currency → unusable, error `"Missing invoice currency; refusing to default to MYR."` Test exists. No 3-letter length check.

Pay generate: `TryNormalizeCurrency(checkout.Currency)` or throw `"Currency is required."` Same helper shape (blank / non-3-letter fail). Checkout create **does** invent MYR when the POST omits currency:

```csharp
var currency = string.IsNullOrWhiteSpace(body.Currency) ? "MYR" : body.Currency.Trim().ToUpperInvariant();
```

That is the checkout resource default, not the adapter inventing a PSP currency. X19 is about the **PSP payload**. A PAID webhook without `currency` 400s. A PAID webhook with `"myr"` normalizes to `MYR` and matches a MYR checkout. A PAID webhook with `"USD"` against a MYR checkout 400s `"currency mismatch"` in the shared handler.

X19.1 “Must match checkout currency” is implemented in `WebhookEndpoints`, not in `XenditWebhook`. Good (one policy for five rails).

X19.2 “Test fixture” is checked. **There is no Xendit missing-currency or mismatch fixture on 8081.** Hub’s `ParseWebhook_PaidWithoutCurrency_DoesNotInventMyr` was not cloned. RailTests PAID body always includes `"currency":"MYR"`.

Checkout create allows `currency: "MY"` (length 2, just uppercased). Xendit start then throws `"Currency is required."` which PublicPay maps to **503**, not 400. Edge. Not Xendit-specific validation.

---

## 7. Email (X22, P19, P20, issue 227)

Hub generate (post-227 spirit): `TryResolveEmail` before HTTP. Blank / `customer@example.com` → `Success=false`, `"Customer email is required."` `BuildInvoicePayload` throws the same. 008 described substitution of the placeholder; **live Hub Xendit fails closed.** Steal that.

Pay:

- `PayProviders.RequiresEmail` is true for every name except Stripe. Xendit included.
- `PublicPayEndpoints.Start`: if required and `!BuyerEmail.IsUsable(row.PayerEmail)` → 400 `"email is required"`.
- `BuyerEmail.IsUsable`: non-blank and not `customer@example.com` (trim, case-insensitive). Same decision as `GatewayCommon.IsUsableBuyerEmail`.
- `XenditHosted` **also** checks `IsUsable` and throws `"email is required"`. If that throw is reached, PublicPay maps it to **503** (message does not contain `"callback base"`). The start gate should 400 first. Double layer is good; status mismatch if someone bypasses the gate is 503.

`:5179` `email_required` comes from GET `/v1/pay/{token}` based on active/checkout provider. UI disables Pay when email is blank. UI does **not** treat `customer@example.com` as blocked; host 400s. Fine.

X22.2 / X23.1 “Missing email 400” / “Test on start”: the only missing-email start test is `Chip_start_without_email_is_400`. Same start method would 400 for xendit, but the fixture PUTs **chip** and never sets xendit as active. After an org has `active_provider=xendit`, start without email must 400. **No such test.** Placeholder email: **no test for any rail** in `Lazuar.Pay.Tests` despite P20.3 “Hermetic 400”.

XenditHosted sends `checkout.PayerEmail!.Trim()` only after `IsUsable`. It will not POST `customer@example.com`. No assertion on `LastBody`.

Hub `ExtractName` / Pay `BuyerEmail.NameFrom` are unused on the Xendit invoice payload (no `customer` object; only `payer_email`). Name collected on `:5179` is stored on the checkout and used at fulfill for `Payers`, not sent to Xendit.

---

## 8. Fees not booked (X15.2, freeze)

Hub `MapInvoiceCallback` reads `fees_paid_amount` into `GatewayFee`, `NetAmount = amount - fee`. Billing `GatewayPaymentCompletedHandler` posts `EXPENSE_GATEWAY_FEE` when `GatewayFee > 0` and books cash as net. That is Hub economics, often wrong when fee is 0 (issues 222/232/239). Xendit is the rare adapter that **can** populate a real processor fee from the invoice object.

Pay `XenditWebhook` **does not read** `fees_paid_amount`, `fees`, or `should_exclude_credit_card_fee`. `PspParseResult` has no fee field. `FulfillPaidAsync` always journals `checkout.Amount` cash D / revenue C. Two lines. Razorpay test explicitly `JournalLines.Count() == 2` while the payload contains `"tax":12,"fee":30`. Xendit test does not assert journal at all.

Correct per freeze: “Do not book. `unknown ≠ 0`.” Do not later “improve” Xendit by copying `fees_paid_amount` into a fee line without a product decision that processor MDR is in-scope. Official Receipt is gross of the checkout, not Xendit settlement.

---

## 9. Wallets not on `:5179` (X20, K12)

Checkout `App.tsx`: amount, name, email, Pay button, verifying poll, paid/expired copy. No provider picker. No wallet brand. `locks.test.ts`:

```ts
expect(src.toLowerCase()).not.toMatch(/grabpay|tng|touchngo|boost|duitnow|fpx|shopee/)
expect(src).not.toContain('autocomplete="cc-number"')
```

Grep of `lazuar-pay-checkout` for `xendit` / `Xendit` / `GrabPay` / `DuitNow`: **no matches** (the lock regex would fail if those brand strings appeared). Buyer never sees which rail is behind the button except indirectly (`email_required`).

Merchant `:5178` copy for xendit:

> Hosted invoice. Wallets on Xendit’s page if you enabled them there. We do not auto-debit.

U14 also requires webhook URL hint `/v1/webhooks/xendit/{orgId}` — live: `{payApi}/v1/webhooks/{provider}/{orgId}` which becomes `…/xendit/{orgId}` when xendit is selected. Placeholder `x-callback-token`. No Brand ID field. No five-logo wall.

Hub capability `SupportsDuitNowQr("XENDIT")` / `SupportsHostedWallet` is **not** ported as pixels. IsolationTests bans Hub types in Vite apps. Do not add a “we take GrabPay” badge on `:5178` because the Hub matrix says true.

---

## 10. X10–X23 vs live code vs tests

Checklist state on this SHA is all `[x]`. Live code vs tests is not the same thing.

| Phase | Live code | Test that actually locks it |
|-------|-----------|-----------------------------|
| X10 class, `api.xendit.co`, `invoice_url` | Yes. `XenditHosted`. | `Xendit_paid_and_settled` mocks 200 with `invoice_url` and asserts start success. Does **not** assert `LastUri` contains `/v2/invoices` or `invoice_url` in the start JSON. |
| X11 PUT secret + token, reject Brand ID, encrypt, `active_provider=xendit`, writer | Shared PUT. | Writer: `Member_cannot_put_gateway` (Stripe JSON). Webhook required: Stripe JSON. Active provider: Stripe JSON. **No xendit PUT round-trip.** Brand ID reject: untested for xendit (`Chip_put_requires_brand_id` is the inverse). |
| X12 POST invoices, Basic `{secret}:`, major units, currency required, payer_email, redirects verifying, metadata checkout_id+org_id, no `payment_methods` | Yes. | **No `LastBody` inspect.** Amount policy, no `payment_methods`, no `for-user-id` are grep-true, test-false. |
| X13 no vault flags, capability `hosted_link` | No vault keys in payload. GET capability is always `hosted_link`. | Capability asserted only on Stripe GET. Payload vault flags untested (Chip asserts `force_recurring` absent). |
| X14 callback token, case-insensitive, Unprotect, 400 mismatch, empty body first | Yes, with length-check timing vs Hub hash-first. | Empty body: Stripe + Chip fixtures; shared code. **Bad Xendit token: no test.** Good token: implied by paid fixture. |
| X15 PAID / `invoice.paid`, data wrapper, checkout id, `FulfillPaidAsync(..., "xendit", invoiceId)`, paid_amount else amount, no fee line | Yes. | PAID happy path exists. No `invoice.paid` event name. No `data` wrapper. No `RCPT-` prefix. No journal assert. No fee-present-but-unbooked assert. |
| X16 SETTLED ignore, `invoice.settled`, one doc after PAID | Ignore branch yes. | PAID-then-SETTLED one doc **only**. SETTLED-alone, body `ignored:settled`, journal count: **missing**. |
| X17 PENDING/EXPIRED/FAILED ignore | Yes. | **No EXPIRED fixture** despite X17.2 / X23.1. |
| X18 `paid:{invoiceId}`, missing id 400 | Yes. | Paid EventId unasserted (duplicate grain untested). Missing id untested. |
| X19 missing currency do not invent MYR; must match checkout | Yes in parse + shared handler. | **No fixture.** Hub has one. |
| X20 no wallet tiles | Yes + vitest lock. | `locks.test.ts` on checkout. Merchant copy is visual, no test that the xendit sentence exists (`merchant/src/locks.test.ts` does not grep wrap copy). |
| X21 no xenPlatform | Grep clean. | No HTTP-header test. IsolationTests do not search `for-user-id`. |
| X22 email required, no placeholder | Start gate + hosted check. | Missing email tested for **chip only**. Placeholder untested. |
| X23 hermetic suite | One test method. | See list below. Checklist overclaims. |

X23.1 claimed:

- Empty body 400 — **shared**, not xendit-named. Chip has a named test; Xendit does not.
- Bad callback token 400 — **missing**.
- PAID → `RCPT-` + replay — **partial** (paid happens; no `RCPT-`, no replay).
- SETTLED after PAID still one doc — **yes, weak**.
- EXPIRED ignore — **missing**.
- Mocked create → `redirect_url` — **partial** (start 2xx; no body assert).
- Missing email 400 — **missing for xendit**.

---

## 11. `RailTests.Xendit_paid_and_settled` — what it actually does

```csharp
[Test]
public async Task Xendit_paid_and_settled()
{
    // PUT xendit secret + tok_1
    // POST checkout amount 10 (currency omitted → MYR)
    // POST /v1/pay/{token}/start with ada@acme.test
    // assert start success
    // POST /v1/webhooks/xendit/t1  {id:inv_1, status:PAID, currency:MYR, paid_amount:10, metadata.checkout_id}
    // header x-callback-token: tok_1
    // POST SETTLED same shape
    // assert SETTLED 200
    // assert Documents.Count == 1
}
```

It proves:

- PUT `provider=xendit` is accepted without `public_merchant_id`.
- Named HttpClient `"xendit"` is intercepted by `FakePspHandler` (factory replaces `IHttpClientFactory` globally).
- Start with email reaches the mock and does not 400/503.
- A PAID JSON with matching sen (10 major → 1000 minor vs checkout 10) and currency MYR fulfills at least one document.
- A following SETTLED JSON with a valid token is 200 and does not create a **second** document.

It does **not** prove:

- `LastUri == https://api.xendit.co/v2/invoices`
- Basic auth username is the unwrapped secret and password empty
- JSON amount is `10` (or `10.0`) not `1000`
- JSON has `payer_email`, `external_id` = checkout id, metadata keys, no `payment_methods`
- No `for-user-id` / `with-split-rule` headers
- Start response `{ redirect_url: "https://checkout.xendit.co/inv_1" }`
- Document number starts with `RCPT-`
- Journal balanced 2 lines, amounts equal checkout, accounts cash/revenue
- Charge.Provider = `xendit`, ProviderRef = `inv_1`
- Checkout.Status = `paid`
- Replay PAID → `{ duplicate: true }` and still one doc
- SETTLED response `{ ignored: "settled" }`
- SETTLED without prior PAID leaves checkout open
- Bad token 400
- Missing currency 400
- Amount mismatch 400
- EXPIRED ignored
- `invoice.paid` / `data` envelope
- Header `X-Callback-Token` mixed case
- Empty body on `/v1/webhooks/xendit/t1`
- Start without email 400
- `customer@example.com` 400

Chip’s neighbouring test is the template X23 said to clone (C32): it asserts `LastBody` contents, `RCPT-`, balanced journal, and replay duplicate. Xendit did not get that clone. Treat X23 as **not exited** even though the markdown boxes are ticked.

---

## 12. Shared host seams that Xendit rides

These are not Xendit-owned, but Xendit money depends on them.

**One transaction (H12).** Insert `PspWebhookEvent` + `FulfillPaidAsync` inside `BeginTransactionAsync`. Unique `(OrgId, Provider, EventId)`. `DbUpdateException` → `{ duplicate: true }`. InMemory tests ignore transactions (`ConfigureWarnings TransactionIgnoredWarning`); uniqueness still holds in memory. Postgres is the real TX.

**Org bind.** Checkout `OrgId !=` URL `{orgId}` → 400. Stripe has `Cross_org_checkout_is_400`. Xendit does not. Same handler.

**Amount match (H14).** Sen equality. Xendit `paid_amount` 10 vs checkout 10 passes. `paid_amount` 10.01 vs 10 → 400. Partial-pay invoices are not a Xendit Invoice API thing; mismatch is fail-closed.

**Zero amount.** Fulfill no-ops `checkout.Amount <= 0`. Checkout create already rejects `amount <= 0`. A PAID webhook with `paid_amount: 0` against a RM 10 checkout 400s mismatch before fulfill. Stripe has `Zero_amount_session_is_ignored` at parse. Xendit has no equivalent “PAID but zero” ignore; mismatch 400 would retry from Xendit. Edge.

**Malformed JSON.** `JsonDocument.Parse` throws `JsonException`. WebhookEndpoints only catches `PspVerifyException` and webhook-secret `InvalidOperationException`. Valid token + broken JSON → **500**. Hub catches all exceptions in `ParseWebhookAsync` and returns `Verified=false` (also 500 via the handler). Same class, still ugly. Prefer 400 unusable so Xendit stops. Not implemented. No test.

**`environment`.** Stored, unused for HTTP. Merchant UI does not show test/live for xendit (only Billplz). Staff with `xnd_production_…` still have row `environment=test` unless they send the field. Do not display “test mode” for Xendit based on that column.

**Named HttpClient `"xendit"`.** No BaseAddress, no extra handler, no DNS fallback (Billplz-only folklore, parked). Absolute URL on the request. IsolationTests ban `IPaymentGatewayAdapter` / factory / `Razorpay.Api`; they do not ban a Xendit SDK because none is referenced. `Lazuar.Pay.csproj` has Stripe.net; no Xendit NuGet. Raw HTTP. Good.

---

## 13. Frontends (U14 + X20) — Xendit-shaped facts only

`:5178` rails array includes `'xendit'`. Copy string is reminder-only and points wallets at Xendit’s page. Secret placeholder is generic `"API secret"` (Stripe gets `sk_test_…`; Xendit does not get `xnd_development_…`). Callback placeholder is `x-callback-token`. Webhook URL updates with the select. Writer-only paste (`canWriteMoney`). Member sees last4 + provider from GET, not the form.

Missing vs Hub ops amber (014): Hub says “Hosted invoice only. … No silent auto-charge, no FPX e-mandate.” Pay says “We do not auto-debit.” FPX e-mandate is not named. `DD_FPX` on a hosted invoice is customer-present FPX, not e-mandate; Hub `SupportsEmandate` is always false. Worth keeping the longer sentence if someone copies ops banners later. Do not add an e-mandate checkbox.

`:5179` does not mention Xendit. Email required when GET says so (true whenever active rail is not Stripe). Verifying poll 15 × 2s. Success URL is not paid.

No Vite secrets (`U20`). IsolationTests: merchant/checkout package.json must not contain `@repo/api-types-ts`.

---

## 14. Test gaps that still must be written

Priority is money-safety, then honesty, then cosmetics. These are Xendit-shaped; shared gaps (empty body already locked on stripe/chip) are noted but not re-owned.

### 14.1 Must write (clone C32 / Hub `XenditGatewayAdapterTests`)

1. **Create HTTP inspect.** After start: `LastUri` is `https://api.xendit.co/v2/invoices`; `LastBody` JSON `amount` is major units (`10` or `10.0`, **not** `1000`); `currency` `MYR`; `payer_email` `ada@acme.test`; `external_id` equals checkout id; `metadata.checkout_id` / `org_id`; **no** `payment_methods` key; **no** `for-user-id` in headers; Authorization is Basic of `xnd_sk:` (or whatever PUT stored). Start JSON has `redirect_url` equal to mock `invoice_url`. This is the RM 10 vs RM 1000 test. Without it, a one-line change to `ToMinor` on the create payload ships green.

2. **Bad callback token 400.** Same PAID body, header `x-callback-token: wrong` (and a length-mismatch `x`). 400. Documents 0. Mixed-case `X-Callback-Token` with the **correct** token still pays (Hub EXPIRED fixture already uses mixed case).

3. **PAID fulfillment assertions.** `RCPT-` prefix, journal 2 lines balanced, charge provider `xendit`, checkout `paid`. Replay same PAID → `duplicate`, still one doc, still two journal lines.

4. **SETTLED ignore for real.** SETTLED **without** prior PAID → 200 body contains `settled`, Documents 0, checkout `open`. Then PAID → one doc. (Optional: SETTLED after PAID body contains `ignored`.)

5. **EXPIRED ignore.** EXPIRED with valid token → ignored, no receipt, checkout open, EventId does not block a later `paid:{id}` if you also send PAID (or at least prove `paid:` grain unused).

6. **Missing currency 400.** PAID, valid token, no `currency` (and not defaulted). Documents 0. Do not invent MYR.

7. **Missing email 400 on xendit.** PUT xendit, start `{name:Ada}` → 400. Placeholder `customer@example.com` → 400. `LastBody` of PSP must remain null / not contain the placeholder.

### 14.2 Should write

8. `invoice.paid` event name + `{ data: { status: PAID, ... } }` envelope fulfills once.
9. `invoice.settled` event name ignored.
10. Currency mismatch USD vs MYR checkout 400.
11. Amount mismatch `paid_amount: 11` vs checkout 10 → 400.
12. Missing invoice `id` 400.
13. Empty body POST `/v1/webhooks/xendit/{org}` 400 (P23 named).
14. PUT xendit with `public_merchant_id` 400; PUT without `webhook_secret` 400; GET capability `hosted_link`, `provider=xendit`, no ciphertext echo.
15. `fees_paid_amount` present on PAID still two journal lines, no fee account.
16. Cross-org: checkout of t1 posted to `/v1/webhooks/xendit/t2` 400 (Stripe already locks the handler).

### 14.3 Do not write as product tests

- Hub `BuildInvoicePayload_FiltersUnknownChannels` clone. The filter must not exist on Pay.
- Hub `BuildRefundPayload` / `TryReadPaymentId`. Refunds parked.
- Hub `ChargeOffSession_AlwaysFalse` as a Pay test — there is no method to call. Grep/`IHostedRail` is the lock.
- `SupportsHostedWallet` on 8081. Capability is `hosted_link` only.
- Live Xendit HTTP in CI. Freeze: hermetic Fake PSP.

---

## 15. Intentional divergences (keep) vs accidental (fix or test)

### Keep (015 chose these)

| Topic | Hub | Pay |
|-------|-----|-----|
| SETTLED | `PAYMENT_COMPLETED` (same EventId as PAID) | Ignore `settled:{id}` |
| EXPIRED / FAILED | `PAYMENT_FAILED` | Ignore, checkout stays open |
| EventId | `{mapped}:{invoiceId}` | `paid:` / `settled:` / `{status}:` |
| `external_id` | random `lazuar_{uuid}` | checkout id |
| `payment_methods` | unused hook + allow-list | absent |
| Platform tenant metadata | `ApplyPayingTenantMetadata` | `org_id` only |
| Fees | parse `fees_paid_amount` | do not parse, do not book |
| Refunds / portal / off-session | implemented / throw / false | not on the interface |
| Gateway type string | `XENDIT` | `xendit` |
| Error body on create 4xx | returned to caller | swallowed as 503 “rejected the org key” |
| Quantity / product name | folded into amount + description | amount is total; description `"Pay"` |

### Accidental / residual (do not treat as freeze)

| Topic | What | Risk |
|-------|------|------|
| Callback compare | Pay length-check then FixedTimeEquals; Hub SHA-256 first (073) | Timing leak on token length. Shared-secret model still allows stolen-token PAID. Hash-first would match Hub without changing Xendit’s protocol. |
| X23 boxes | Checked without fixtures | Next reader believes EXPIRED / bad token / missing currency / major-units create are locked. They are not. |
| SETTLED-only dashboard | Copy does not say “enable invoice paid” | Captured at Xendit, never `RCPT-`. Hub would have booked SETTLED. |
| Webhook URL reachability | Xendit create has no callback URL; dashboard must be public HTTPS; `:5178` Xendit copy omits the Billplz-style localhost warning | First live payment hangs on verifying. |
| SETTLED test weakness | One-doc after PAID also passes if SETTLED fulfills | X16 can regress silently. |
| Malformed JSON | 500 | Xendit retries; Hub same class. |
| Secret trim on create | Hub `apiKey.Trim()`; Pay relies on PUT trim | Only matters if ciphertext is edited by hand. |
| Checkout currency default MYR | Host invents on create when omitted | Not the adapter. Do not “fix” by inventing on the webhook. |
| `environment` column | Always stored, never read | Do not show test/live for Xendit from this field. |

---

## 16. Refuse list (Xendit-shaped, still true on this SHA)

Do not, in a follow-up that “finishes” Xendit:

1. Copy `MalaysiaHostedChannels` / `xendit_payment_methods` / hop-1 GrabPay tiles.
2. Add `for-user-id`, xenPlatform sub-accounts, application fees, split settlement.
3. Map SETTLED to paid because “Hub did” or because a merchant only enabled settled. If dashboard-only-settled is a support ticket, the copy changes, not `MapStatus`.
4. Map EXPIRED to failed in a way that writes a receipt or consumes `paid:{id}`.
5. Send minor units to `/v2/invoices`.
6. Default missing webhook currency to MYR.
7. POST `customer@example.com`.
8. Book `fees_paid_amount` as a journal fee.
9. Implement `IssueRefund` / Payment Tokens / Subscriptions / `POST /sessions` / e-mandate / off-session because Xendit the company sells them.
10. Auto-register dashboard callbacks (there is no CHIP-style registrar to steal; do not write one).
11. ProjectReference Hub or copy `IPaymentGatewayAdapter`.
12. Treat 015 X23 checkboxes as evidence they were written.

---

## 17. Verdict

The new host **stole the HTTP job** of Hub `XenditGatewayAdapter` without copying the class: `POST https://api.xendit.co/v2/invoices` with Basic `{secret}:`, major-unit amount via AwayFromZero sen round-trip, `payer_email`, verifying redirects, metadata join, callback-token header, `data` unwrap, PAID-only fulfill, SETTLED/EXPIRED ignored, currency fail-closed, no xenPlatform, no channel list, no wallet tiles on `:5179`, fees not booked, reminder-only. That is the right shape for 015 §5.3.

The money-safety improvement versus Hub is real: **SETTLED is not `PAYMENT_COMPLETED`.** The money-safety hole versus Hub 073 is also real: **token compare is not hash-first.** The test suite does **not** clone C32. One method `Xendit_paid_and_settled` cannot carry X10–X23. Checklist markdown is ahead of the fixtures.

Ship judgment for this slice: **code is mostly honest; tests are not.** Write the §14.1 fixtures before calling X23 done. Do not implement refunds, sessions, or wallets on the way.
)