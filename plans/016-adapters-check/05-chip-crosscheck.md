# 05 — CHIP Collect cross-check: Hub `ChipCollectGatewayAdapter` vs Pay `ChipHosted` + `ChipWebhook`

**Date:** 24 August 2026  
**Branch:** `feat/015-four-adapters`  
**HEAD:** `c621ceba7fc7b79f16954d0819200cb21db6f22b` — same SHA as [016 README](./README.md) (`c621ceba` — `docs(015): check off implemented T–Q phases`)  
**Type:** Uncondensed evaluation. **Not** an implementation. **Not** a project reference into `apps/lazuar-api`. Live files are authority. 015 C10–C32 checklists are a map, not proof.

**Slice:** CHIP Collect hosted_link on 8081. Steal HTTP from Hub. Refuse the silent registrar. `purchase.preauthorized` must not pay.

Parent judgment this file is evidence for: [00-evaluation.md](./00-evaluation.md) (written after `01`–`10`). CHIP must-do in 015: [00-what-must-be-done.md](../015-four-adapters/00-what-must-be-done.md) §5.1. Locked decisions: [decisions.md](../015-four-adapters/checklists/decisions.md) — CHIP register webhooks = **dashboard paste PEM**; provider string lowercase `chip`; host `https://gate.chip-in.asia/api/v1/`; capability `hosted_link`; event ids namespaced; currency fail-closed; no default MYR.

---

## 1. Files opened (complete)

### 1.1 Hub (judgment only)

| Path | Lines | Role |
|------|------:|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` | 648 | Purchases POST, RSA parse, off-session charge, refund, portal throw, vault extract |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipWebhookRegistrar.cs` | 131 | `GET/POST https://gate.chip-in.asia/api/v1/webhooks/`, company `GET /public_key/` fallback |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs` | (helpers) | `ToMinorUnitsRounded` AwayFromZero, `TryResolveEmail`, `ExtractName`, `TryNormalizeCurrency`, `ApplyPayingTenantMetadata`, placeholder `customer@example.com` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Commands/UpdatePaymentConfigCommandHandler.cs` | CHIP block ~106–132 | On new CHIP key: rewrite `localhost` → `lazuar-local-dev.com`, then `ChipWebhookRegistrar.EnsureRegisteredAsync` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/ChipCollectGatewayAdapterTests.cs` | 554 | Refund HTTP, tenant metadata, vault extract, off-session, RSA paid/fail/refund/preauthorized+token=paid, missing currency, missing id, bad/missing signature |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/ChipWebhookRegistrarTests.cs` | 85 | List-before-create; existing callback does not POST again |

### 1.2 New Pay host

| Path | Lines | Role |
|------|------:|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Gateways/ChipHosted.cs` | 77 | `IHostedRail.CreateHostedUrlAsync` only |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Gateways/ChipWebhook.cs` | 119 | RSA verify + event map. **Not** on `IHostedRail` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Gateways/IHostedRail.cs` | 12 | `Provider` + `CreateHostedUrlAsync` → `HostedSession` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Gateways/PspParseResult.cs` | 17 | Shared parse DTO + `PspVerifyException` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Gateways/PayProviders.cs` | 29 | `chip` in allow-list; Brand ID required; email required |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Gateways/BuyerEmail.cs` | 26 | Placeholder refuse + name-from-email |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs` | 212 | PUT/GET; Brand ID + PEM required for chip |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs` | 144 | Shared Plane B: empty 400, switch, unique, TX fulfill |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` | 127 | Start dispatch + `email_required` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Program.cs` | 80 | `AddHttpClient("chip")`, `AddScoped<ChipHosted>()` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj` | 20 | EF Design + Npgsql + **Stripe.net only**. No CHIP package |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Money/MoneyMath.cs` | 27 | `ToMinor` AwayFromZero; `TryNormalizeCurrency` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` | 130 | Same-handler Official Receipt + two-line journal |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs` | — | `PublicMerchantId`, `WebhookCiphertext`, `ProviderSessionId` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs` | — | Unique `(OrgId, Provider, EventId)` on `psp_webhook_events` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Gateways/StripeHosted.cs` | 50 | Shape CHIP was supposed to copy (small class, not Hub adapter) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Gateways/StripeWebhook.cs` | 84 | Contrast: setup/zero ignored at parse; CHIP does not ignore zero at parse |

### 1.3 Tests

| Path | CHIP-relevant cases |
|------|---------------------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/RailTests.cs` | `Chip_start_and_paid_webhook`, `Chip_preauthorized_is_ignored`, `Chip_start_without_email_is_400`, `Chip_empty_body_400` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/GatewayTests.cs` | `Chip_put_requires_brand_id` only. Shared: member 403, webhook_secret required (stripe body) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/WebhookTests.cs` | Stripe-only. Empty/bad-sig/replay/setup/zero/cross-org **not cloned** for chip |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs` | Bans Hub factory/adapter/MediatR/`Razorpay.Api`. Does **not** grep `ChipWebhookRegistrar` or a CHIP NuGet id |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/PayApiFactory.cs` | Replaces **all** `IHttpClientFactory` with `StaticHttpFactory(Psp)` — hermetic |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/FakePspHandler.cs` | Records `LastBody` + `LastUri` only. **No headers.** Bearer is unassertable |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPayTests.cs` | Empty webhook on **stripe** path. No chip GET `email_required` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Lazuar.Pay.Tests.csproj` | No CHIP package. ProjectReference host only |

There is **no** `ChipWebhookTests.cs`. 014 said do not name it `ChipCollectGatewayAdapterTests`. Live name is a shared `RailTests` class with four chip methods plus billplz/xendit/razorpay.

### 1.4 015 C10–C32 + parked registrar + frontends

Opened every `c10`–`c32` checklist, `parked-chip-registrar.md`, `u12-chip-fields.md`, `p19`/`p20`/`p27`/`k11`/`h13`/`h14`, merchant `WorkspacePage.tsx`, checkout `App.tsx`, host `README.md`.

### 1.5 Grep inside `apps/lazuar-pay` (this program’s tree)

| Needle | Hits in `apps/lazuar-pay` |
|--------|---------------------------|
| `gate.chip-in.asia` | `ChipHosted.cs` `ApiBase` constant; `RailTests.cs` stub `checkout_url` |
| `force_recurring` | **Only** `RailTests.cs` assertion `Does.Not.Contain("force_recurring")` |
| `skip_capture` | **Zero** |
| `ChipWebhookRegistrar` | **Zero** |
| `EnsureRegisteredAsync` | **Zero** |
| `public_key/` (company key URL) | **Zero** |
| `/api/v1/webhooks/` toward CHIP | **Zero**. Pay’s own route is `/v1/webhooks/{provider}/{orgId}` |
| `lazuar-local-dev.com` | `BillplzHosted.cs` **refuses** that host. Chip does not rewrite to it |
| CHIP / ChipIn NuGet | **Zero** on host and test csproj. Packages: EF Design, Npgsql, Stripe.net |

Registrar live only in Hub: `ChipWebhookRegistrar.cs`, `ChipWebhookRegistrarTests.cs`, `UpdatePaymentConfigCommandHandler.cs:125`.

---

## 2. Size and shape: CHIP looks like StripeHosted, not like the Hub adapter

Hub `ChipCollectGatewayAdapter` is a five-verb `IPaymentGatewayAdapter`: generate checkout, parse webhook, charge off-session, issue refund, generate portal (throws). 648 lines. It injects `IHttpClientFactory` and calls **unnamed** `CreateClient()`. A historical `_configuration` field was already deleted; the live host is still always `https://gate.chip-in.asia/api/v1/` regardless of tenant `environment=test`.

Pay `ChipHosted` is 77 lines, `sealed`, primary-constructor, implements `IHostedRail` with **one** verb. Parse lives in a 119-line static `ChipWebhook`. Fulfill lives in `WebhookEndpoints` + `Fulfillment`. That is the 015/014 rule: CHIP’s first class should look like `StripeHosted` (50 lines, create only), not like the Hub file.

`IHostedRail` as live:

```7:12:apps/lazuar-pay/src/Lazuar.Pay/Gateways/IHostedRail.cs
public interface IHostedRail
{
    string Provider { get; }

    Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct);
}
```

P27 wrote `Task<string>`. Live returns `HostedSession(RedirectUrl, ProviderSessionId)` so start can persist `purch_1`. That is a better seam, not a factory. IsolationTests bans `IPaymentGatewayAdapter`, `PaymentGatewayFactory`, `IPaymentGatewayFactory`, `AddPaymentsModule`, `GatewayPaymentCompletedIntegrationEvent`, `Modules.Payments`. `ChipHosted` hits none of those strings.

C10 asked for `public const string Provider = "chip"`. Live is `public string Provider => PayProviders.Chip` (`"chip"` lowercase). The const that exists is `ApiBase`, which is the thing that must not drift.

DI:

```21:32:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
builder.Services.AddHttpClient("chip");
builder.Services.AddHttpClient("billplz");
builder.Services.AddHttpClient("xendit");
builder.Services.AddHttpClient("razorpay");
...
builder.Services.AddScoped<ChipHosted>();
```

014 preferred typed `AddHttpClient<ChipHosted>()` so the class would not carry a magic name. Live uses named `"chip"` plus `http.CreateClient("chip")`. That is still **not** Hub’s unnamed `CreateClient()`. There is **no** timeout on the named client (014 mentioned 15s; default is 100s). Tests replace the entire `IHttpClientFactory` with `StaticHttpFactory` at 5s, so CI never sees the production timeout.

`ChipHosted` does **not** implement refund, off-session, portal, `ExtractVaultIds`, or `IsOffSessionPaid`. Those Hub methods stay museum.

---

## 3. Purchases HTTP

### 3.1 Hub `GenerateCheckoutAsync` (what to steal)

Always:

```
POST https://gate.chip-in.asia/api/v1/purchases/
Authorization: Bearer {apiKey}
```

JSON object (dictionary + anonymous types via `JsonContent.Create`):

- `brand_id` = `merchantId` argument. Empty merchant id returns a failed `GatewayCheckoutResult` **without HTTP**: `"MerchantId (Brand ID) is required for CHIP Collect."`
- `client.email` from `GatewayCommon.TryResolveEmail` (placeholder refused)
- `client.full_name` from `GatewayCommon.ExtractName(email)` — local-part, **not** a separate name argument. Hub checkout does not take payer name.
- `purchase.products[0].name` = `ProductDescription(productName, quantity)` (`"Plan (x2)"` or `"Lazuar Payment"`)
- `purchase.products[0].price` = `ToMinorUnitsRounded(amount, quantity)` — **integer cents**, AwayFromZero, quantity multiplied in
- `purchase.metadata` = caller dictionary after `ApplyPayingTenantMetadata` (`tenant_id` kept if paying workspace; `platform_tenant_id` stamped when adapter tenant differs)
- `success_redirect` = caller success URL
- `failure_redirect` = caller cancel URL
- `cancel_redirect` = caller cancel URL

**Do not steal** the next block:

```80:88:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs
        if (setupFutureUsage)
        {
            payload["force_recurring"] = true;

            if (amountInCents == 0)
            {
                payload["skip_capture"] = true;
            }
        }
```

That is the vault path. `$0` + `skip_capture` is what makes CHIP fire `purchase.preauthorized` instead of `purchase.paid`. Hub then mapped preauthorized+token to `PAYMENT_COMPLETED`. 015 parked off-session. C15 forbids both keys. There is still **no Hub test** that generate with `setupFutureUsage` and amount 0 actually sets `skip_capture` (014/009 lying-by-omission). Do not add that flag in Pay just because Hub could.

Success: read `checkout_url` and root `id`. Missing URL is a failed result. Non-success HTTP logs status+**full body** and returns `"CHIP API error: {responseBody}"` — that can leak processor messages; it should not leak the Bearer, but it is chatty. Exceptions become `ex.Message`.

Hub does **not** send `purchase.currency`. Brand default is the currency. Webhook later fail-closes if CHIP omits `purchase.currency`.

### 3.2 Pay `ChipHosted.CreateHostedUrlAsync` (what landed)

```17:75:apps/lazuar-pay/src/Lazuar.Pay/Gateways/ChipHosted.cs
    public async Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)
    {
        var cred = await db.GatewayCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == checkout.OrgId && x.Provider == PayProviders.Chip, ct);
        if (cred is null || string.IsNullOrWhiteSpace(cred.PublicMerchantId))
        {
            throw new InvalidOperationException("rail not configured");
        }

        if (!BuyerEmail.IsUsable(checkout.PayerEmail))
        {
            throw new InvalidOperationException("email is required");
        }

        var payload = new Dictionary<string, object?>
        {
            ["brand_id"] = cred.PublicMerchantId,
            ["client"] = new
            {
                email = checkout.PayerEmail!.Trim(),
                full_name = BuyerEmail.NameFrom(checkout.PayerEmail, checkout.PayerName)
            },
            ["purchase"] = new
            {
                products = new[]
                {
                    new { name = "Pay", price = MoneyMath.ToMinor(checkout.Amount) }
                },
                metadata = new Dictionary<string, string>
                {
                    ["checkout_id"] = checkout.Id,
                    ["org_id"] = checkout.OrgId
                }
            },
            ["success_redirect"] = checkout.SuccessUrl ?? "http://localhost:5179/c/" + checkout.PublicToken + "?status=verifying",
            ["failure_redirect"] = checkout.CancelUrl ?? "http://localhost:5179/c/" + checkout.PublicToken,
            ["cancel_redirect"] = checkout.CancelUrl ?? "http://localhost:5179/c/" + checkout.PublicToken
        };

        var client = http.CreateClient("chip");
        using var request = new HttpRequestMessage(HttpMethod.Post, ApiBase + "purchases/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", box.Unprotect(cred.Ciphertext));
        request.Content = JsonContent.Create(payload);
        ...
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("CHIP rejected the org key");
        }
        ...
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("CHIP returned no URL");
        }

        return new HostedSession(url, id);
    }
```

Stolen correctly:

- Same host, same path, trailing slash, Bearer from **unprotected ciphertext** (not Hub plaintext `apiKey` argument).
- `brand_id` from `PublicMerchantId` plaintext.
- `client.email` / `full_name`.
- Products price in **cents** as a numeric `long`, not a ringgit float.
- Three redirect keys with Hub names.
- Read `checkout_url` + `id`.
- Missing Brand ID does not call CHIP.
- Missing URL throws (Start maps `InvalidOperationException` to **503**, except Billplz “callback base” which is 400).

Improved vs Hub:

- Non-success HTTP is a short `"CHIP rejected the org key"` — C12 said do not leak the full secret; live also does not leak the CHIP error body. Hub leaked the body.
- Email checked with `BuyerEmail.IsUsable` (placeholder refused) before HTTP.
- Name can come from checkout `PayerName` if the buyer typed one; Hub could only derive from email.
- Metadata is only `checkout_id` + `org_id` (C14). No `tenant_id` / `platform_tenant_id` / `hub_payment_environment`.
- **No** `force_recurring`. **No** `skip_capture`. **No** `if (setupFutureUsage)` block. Grep-clean in src.

Intentional product differences (not bugs):

- Line item name is always `"Pay"`, same as `StripeHosted`. Catalog product name is unused on the CHIP page. Hub sent the commerce plan name.
- Quantity is 1; `checkout.Amount` is already the total. Hub multiplied `amount * quantity` inside `ToMinorUnitsRounded`.
- Default redirects go to `:5179/c/{token}?status=verifying` and `:5179/c/{token}`, not Hub `/api/v1/...`. Success URL is **not** fulfillment (K14). Checkout UI polls while `status=verifying`.
- `environment` on the credential row is stored (`test`/`live`) and **ignored** for host selection. Hub also always hit the live CHIP host. Honesty: there is no CHIP sandbox URL in either tree. A merchant who pastes a test Brand ID still talks to `gate.chip-in.asia`.

Not stolen (correct refuse):

- Off-session create+`/charge/` + `Idempotency-Key` + `reference` lookup.
- `POST purchases/{id}/refund/`.
- Portal.

Gaps on the HTTP itself:

- `ChipHosted` has **no** try/catch around `SendAsync`. `HttpRequestException` / timeout bubbles out of `PublicPayEndpoints.Start` uncaught → **500**, not 503. Hub swallowed exceptions into a failed result. Stripe path catches `StripeException` only. CHIP network fail is harsher than CHIP 4xx.
- No `CancellationToken` timeout shorter than HttpClient default.
- Create payload still has **no currency field**, same as Hub. H14 only runs at webhook time. If the Brand is USD and the checkout is MYR, the buyer can pay USD on CHIP’s page and the webhook will 400 `currency mismatch` — money taken, no `RCPT-`, CHIP will retry. That is fail-closed for books, not fail-closed for the card. Same class of problem as any hosted_link rail that does not send currency on create. CHIP’s API may reject a currency mismatch at the brand; this file did not call live CHIP to prove it.
- `FakePspHandler` does not record `Authorization`. RailTests never assert Bearer or `POST …/purchases/`. LastUri is captured and unused in the chip start test.

Serialization: `JsonContent.Create` uses `JsonSerializerDefaults.Web` (camelCase), **not** the host’s `SnakeCaseLower` HTTP JSON options. Dictionary keys (`brand_id`, `success_redirect`, …) are preserved. Anonymous `full_name` / `checkout_id` already contain underscores. Same pattern as Hub. Live mock body is not snapshotted as JSON in tests beyond `Contains("checkout_id")` and `Not.Contain("force_recurring")`.

---

## 4. Cents

Hub: `GatewayCommon.ToMinorUnitsRounded(amount, quantity)` → `(int)Math.Round(amount * qty * 100m, 0, MidpointRounding.AwayFromZero)` for MYR-shaped money. Zero-decimal ISO list exists on `ToMinorUnits` but `ToMinorUnitsRounded` **hard-codes `"MYR"`**, so JPY would still ×100 if someone called the rounded helper. CHIP generate uses the rounded helper.

Pay: `MoneyMath.ToMinor(checkout.Amount)`:

```5:6:apps/lazuar-pay/src/Lazuar.Pay/Money/MoneyMath.cs
    public static long ToMinor(decimal amount) =>
        (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
```

C13 asked for this policy and forbade Hub `ToMinorUnitsTruncating` (which, in the live Hub file, is **identical** to rounded — a Hub lie). Pay always AwayFromZero. Quantity this program is 1 (`CheckoutEndpoints` seeds `Interval = "one_off"`; create rejects `Amount <= 0`).

Create: `price` is a `long` cents integer. C13.2 “Do not send MYR as a float ringgit to `price`” — live sends `ToMinor`, not `checkout.Amount`. **No test asserts the number `1000` in `LastBody`.** Seed checkout is `{"org_id":"t1","amount":10}` → 1000 sen. The paid webhook fixture uses `"total":1000` which is the inverse of the same policy, so an accidental ringgit-on-create would still match an accidental ringgit-on-webhook. The pair can be **consistently wrong** and still mint `RCPT-`. That is why C13.3 wanted the mock body to show cents.

Webhook side:

- Hub: `purchase.total` decimal **cents** → `amountPaid = amountCents / 100m` major units, because `GatewayWebhookParsedResult.AmountPaid` is ringgit.
- Pay: `AmountMinor = (long)total` **stays cents**. `WebhookEndpoints` compares `parsed.AmountMinor` to `MoneyMath.ToMinor(checkout.Amount)`.

C19.1 wording “Amount from `purchase.total` cents / 100” is Hub-major-unit language. Live Pay is correct for H14 (minor vs minor). The dangerous cast is `(long)total`: **truncates toward zero**, not AwayFromZero. CHIP totals are integer sen in practice. If CHIP ever sent `1000.9`, Pay would book vs 1000.

H14 mismatch → 400 `amount mismatch` **before** unique insert on the paid path. A later corrected total can still pay. Good. **No CHIP test sends total 999 against checkout 10.00.** H14.4 wrote that fixture for Stripe; `WebhookTests` as opened does not contain `999` either. Shared handler would enforce it if CHIP parse sets `AmountMinor`.

Zero: `StripeWebhook` ignores `AmountTotal` 0 / `mode=setup` at parse (`IgnoreReason = "setup_or_zero"`). `ChipWebhook` does **not** ignore `purchase.paid` with `total=0`. If checkout is RM10, total 0 is amount mismatch 400. If checkout were 0, `CheckoutEndpoints` already refuses `amount must be greater than 0`, so the Stripe-style zero-paid path is structurally hard to reach for CHIP. C19 still asked parse-time `amount > 0`. Live relies on create-time amount>0 + H14. Not a paid-money bug today.

Fees: Hub reads `payment.fee_amount` / `net_amount` (cents/100) and stamps `gateway_fee_status`. Pay **does not parse fees**. `Fulfillment` books `checkout.Amount` cash debit / revenue credit. C19.2 “Do not book CHIP `payment.fee_amount` as a fee line (`unknown ≠ 0` if you skip it)” — skipped. Official Receipt title. No tax line. Matches T13/T14.

---

## 5. Metadata

Hub generate stamps commerce folklore: paying `tenant_id`, optional `platform_tenant_id`, and whatever the caller stuffed in (subscription ids, SST keys on the **off-session** path). Webhook copies **all** purchase.metadata keys into the parsed dictionary. Checkout join in Hub is not this file’s job (`ProcessGatewayWebhookCommandHandler` + metadata `checkout_id` / similar).

Pay generate (C14):

```45:49:apps/lazuar-pay/src/Lazuar.Pay/Gateways/ChipHosted.cs
                metadata = new Dictionary<string, string>
                {
                    ["checkout_id"] = checkout.Id,
                    ["org_id"] = checkout.OrgId
                }
```

Pay parse reads **only** `purchase.metadata.checkout_id`. It does not read `org_id` from metadata. Org bind is `checkout.OrgId != path orgId` in `WebhookEndpoints` (H13). Metadata `org_id` is a breadcrumb for humans and for a future check; it is not enforced. Stripe H13.1 asked “metadata `org_id` if present must match path”. Shared handler does not do that for any rail.

C14.3 “Mocked POST body includes both keys”: `Chip_start_and_paid_webhook` asserts `LastBody` contains `checkout_id`. It does **not** assert `org_id`. The key is in source.

C14.2 “Do not rely only on CHIP’s purchase id without storing `ProviderSessionId`”: start writes:

```104:108:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
            var hosted = await rail.CreateHostedUrlAsync(row, ct);
            row.Provider = name;
            row.PspRedirectUrl = hosted.RedirectUrl;
            row.ProviderSessionId = hosted.ProviderSessionId;
            await db.SaveChangesAsync(ct);
```

C17 asked the test to assert `checkout.Provider == chip` and `ProviderSessionId == purch_1`. **RailTests does not open the DB after start.** The paid webhook uses metadata `checkout_id`, not `ProviderSessionId`. Join would still work if start failed to persist the purchase id. P18 is unproven for CHIP.

Webhook paid result sets `ProviderRef = purchaseId` (stable nested/root id). Fulfillment stores that on `charges.ProviderRef`. Good. Event id is **not** the checkout id (C23.2).

---

## 6. RSA verify

### 6.1 Hub

`ParseWebhookAsync` (sync work wrapped in `Task.FromResult`):

1. Find header key equals `X-Signature` case-insensitive.
2. Missing → `Verified: false`, error `"Missing X-Signature header."` (not thrown).
3. `Convert.FromBase64String` **uncaught** here — falls into generic catch → error `ex.Message`.
4. `RSA.Create()`, `ImportFromPem(webhookSecret)` uncaught similarly.
5. `VerifyData(UTF8(rawBody), signatureBytes, SHA256, RSASignaturePadding.Pkcs1)`.
6. Invalid → warning log, `Verified: false`, `"RSA signature verification failed."`.
7. Then JSON parse and event map.

PEM is the **plaintext** `webhookSecret` argument. Hub registrar was supposed to store `Webhook.public_key`. Fallback `GET /api/v1/public_key/` is the **company** key. Comment on the registrar type: “Verify PEM is Webhook.public_key, not the company GET /public_key/ key.” A wrong PEM is 400-forever. Tests use an ephemeral 2048-bit pair and `SignData` PKCS1 SHA256, then `ParseWebhookAsync(..., publicPem, body, headers)`.

Hub tests that exist: missing signature; garbage 64-byte base64 that fails verify; happy-path signed fixtures via `ParseSignedAsync`.

### 6.2 Pay `ChipWebhook.Parse`

```12:50:apps/lazuar-pay/src/Lazuar.Pay/Gateways/ChipWebhook.cs
        var sigKey = headers.Keys.FirstOrDefault(k => k.Equals("X-Signature", StringComparison.OrdinalIgnoreCase));
        if (sigKey is null || !headers.TryGetValue(sigKey, out var sig) || string.IsNullOrWhiteSpace(sig))
        {
            throw new PspVerifyException("invalid signature");
        }
        ...
        var pem = box.Unprotect(cred.WebhookCiphertext);
        ...
        signatureBytes = Convert.FromBase64String(sig.ToString()); // FormatException → invalid signature
        ...
        rsa.ImportFromPem(pem); // Exception → invalid signature
        var ok = rsa.VerifyData(Encoding.UTF8.GetBytes(raw), signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (!ok) throw new PspVerifyException("invalid signature");
```

Stolen: header name case-insensitive, base64, `ImportFromPem`, SHA256, PKCS#1 v1.5, **raw UTF-8 bytes of the same string later parsed as JSON**. Not HMAC. Not Stripe `EventUtility`. Not Billplz `x_signature`.

Improved: Base64 and PEM import are mapped to `PspVerifyException`, which `WebhookEndpoints` turns into **400** `"invalid signature"`, not 500. Missing webhook ciphertext is `InvalidOperationException("webhook secret missing")` → **503** (same as Stripe missing `whsec_`).

PEM source: `SecretBox.Unprotect(WebhookCiphertext)` from the **org row**, not process env, not company `GET /public_key/`. Merchant pastes dashboard PEM on PUT. There is **no** comment in `ChipWebhook` warning that the company public key is the wrong key. Steal that one-liner from the Hub registrar XML; the foot-gun moved from auto-fetch to Ada pasting the wrong blob.

Empty body never reaches `ChipWebhook`: `WebhookEndpoints` reads UTF-8, `IsNullOrWhiteSpace` → 400 `"empty body"` **before** credential lookup and **before** the provider switch. Whitespace `"  "` counts as empty. C26 is implemented in the shared handler.

C18.2: RailTests generates `RSA.Create(2048)`, `ExportSubjectPublicKeyInfoPem()`, PUT as `webhook_secret`, `SignData` PKCS1 SHA256, `X-Signature` base64. That is the right fixture. It is used for **paid** and **preauthorized** only.

**C27 is not a test.** There is no CHIP case for garbage `X-Signature`, missing header, or non-base64. Stripe `WebhookTests.Invalid_signature_is_400` is the clone target. C32.1 listed “Bad / missing RSA 400 (C27)” as checked; live code would 400 if you sent it; **the test file does not send it.** Checklist checkmarks are not evidence.

Re-encoding risk: `StreamReader(request.Body, UTF8)` then `Encoding.UTF8.GetBytes(raw)` is the same as Hub’s `GetBytes(rawBody)` after the host already had a string. CHIP signs the raw POST bytes. If a proxy mutated the body, verify fails closed.

---

## 7. Event ids, kinds, and `preauthorized` must not pay

### 7.1 Hub map (do not copy the paid name for vault)

| CHIP `event_type` | Hub `EventType` | Hub `EventId` |
|------------------|-----------------|---------------|
| `purchase.paid` | `PAYMENT_COMPLETED` | `PAYMENT_COMPLETED:{purchaseId}` |
| `purchase.preauthorized` **and** recurring token / `is_recurring_token` | `PAYMENT_COMPLETED` | `PAYMENT_COMPLETED:{purchaseId}` |
| `purchase.preauthorized` without token | raw `purchase.preauthorized`, verified ignore, **no id required** | empty |
| `purchase.payment_failure` | `PAYMENT_FAILED` | `PAYMENT_FAILED:{purchaseId}` |
| `payment.refunded` | `REFUND_COMPLETED` | `REFUND_COMPLETED:{purchaseId}` |
| anything else | raw type, ignore | empty |

008/009 history: EventId used to be the **bare purchase id**. Fail then pay shared the unique grain; `PAYMENT_COMPLETED` after `PAYMENT_FAILED` was dropped. Hub tests now lock `PAYMENT_COMPLETED:purch_root_1` and `PAYMENT_FAILED:purch_fail_1`. Steal the **namespace**, not the Hub enum strings.

Hub `ReadStablePurchaseId`: nested `purchase.id` if object+non-empty string, else root `id`. Never invent a Guid. Tests: nested wins over root; missing ids → not verified, error contains `"purchase id"`.

Hub `ParseWebhook_PreauthorizedRecurringToken_IsPaymentCompletedWithVault` is the bug 015 forbids stealing. Amount 0, token present, `PAYMENT_COMPLETED`. That is vault dressed as cash.

### 7.2 Pay map (live)

After verify + JSON:

1. Require stable purchase id **for every event**, including ignores. Missing → `PspVerifyException("missing purchase id")` → 400. Hub allowed unknown/preauthorized-without-token through without an id. Pay is stricter; CHIP may retry a junk event forever. Preferable to inventing a Guid.
2. `purchase.preauthorized` → `EventId = "preauth:" + purchaseId`, `Ignored = true`, `IgnoreReason = "preauthorized"`. **No token exception.** Recurring token in JSON does not matter.
3. `purchase.payment_failure` → `EventId = "failed:" + purchaseId`, ignored `"payment_failure"`.
4. Anything other than `purchase.paid` (including `payment.refunded`) → `EventId = (eventType ?? "chip") + ":" + purchaseId`, ignored with reason = event type.
5. `purchase.paid` → currency required, metadata checkout_id, `EventId = "paid:" + purchaseId`, `AmountMinor = (long)total`, `ProviderRef = purchaseId`.

C20 prefer `paid:` over `PAYMENT_COMPLETED:`. Live uses `paid:` / `failed:` / `preauth:`. Unique key is `(org_id, provider, event_id)` so `chip` + `paid:purch_1` cannot collide with `failed:purch_1`.

`WebhookEndpoints` for ignored events **inserts** the unique row then 200 `{ ignored: "<reason>" }`. That consumes `failed:` / `preauth:` grains, **not** `paid:`. A later `purchase.paid` can still fulfill. **That sequence is untested** (C22.2).

Replay of the same `paid:` : find unique → 200 `{ duplicate: true }` without fulfill. `Chip_start_and_paid_webhook` covers this and asserts still one `RCPT-`. Does not re-sum journal after replay; first assertion already required debit==credit.

`payment.refunded`: ignored, not `REFUND_COMPLETED`. Parked-refunds stays parked. No refund API on `ChipHosted`.

### 7.3 `ReadStablePurchaseId`

Pay copy is the Hub order with `ValueKind == String` on nested id:

```105:118:apps/lazuar-pay/src/Lazuar.Pay/Gateways/ChipWebhook.cs
    static string? ReadStablePurchaseId(JsonElement root)
    {
        if (root.TryGetProperty("purchase", out var purchase) && purchase.ValueKind == JsonValueKind.Object
            && purchase.TryGetProperty("id", out var nested) && nested.ValueKind == JsonValueKind.String)
        {
            var id = nested.GetString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }

        return root.TryGetProperty("id", out var top) && top.ValueKind == JsonValueKind.String ? top.GetString() : null;
    }
```

RailTests paid fixture sets **both** root and nested to `purch_1`. Nested-wins-over-root is **untested**. Missing id is **untested**. C23.3 said “helper covered by C19 fixture using nested id” — the fixture is ambiguous.

### 7.4 Preauthorized live test (the load-bearing one)

`Chip_preauthorized_is_ignored` signs a body with `event_type=purchase.preauthorized`, `total:0`, `currency:MYR`, metadata checkout_id, **and** `"recurring_token":"tok"`. Asserts 200, body contains `"preauthorized"`, **zero documents**. That is the inversion of Hub `ParseWebhook_PreauthorizedRecurringToken_IsPaymentCompletedWithVault`. This is the NP-GW-008 bar. Code + this test both refuse Hub’s event name.

It does not assert checkout `status` stays `open` (C21.1). Documents=0 is the money assertion; status is implied because fulfill is the only writer of `paid`.

`ExtractVaultIds` was not ported. C21.3 satisfied.

---

## 8. Currency fail-closed

Hub: missing `purchase.currency` → not verified, error `"Missing purchase currency; refusing to default to MYR."`, `AsUnusable()`, and a test that `Currency` is not `"MYR"`.

Pay: `MoneyMath.TryNormalizeCurrency` — trim, upper, length 3, else false. `ChipWebhook` throws `"missing currency"` on paid only. WebhookEndpoints then also compares parsed currency to `checkout.Currency` case-insensitive → 400 `currency mismatch`.

Create still does not send currency (see §3). Default MYR exists on **checkout create** when the writer omits currency (`CheckoutEndpoints`: blank → `"MYR"`). That is not a webhook default. CHIP omitting currency cannot become MYR inside `ChipWebhook`.

**C24 test does not exist.** C32.1 listed “Missing currency no pay (C24)” as checked. There is no signed `purchase.paid` without `currency` in `RailTests` or `WebhookTests`. Hub has `ParseWebhook_PurchasePaid_MissingCurrency_IsNotVerified`. Pay must still write that fixture: RSA-valid JSON, no currency key, 400, zero `RCPT-`.

Non-3-letter (`"MY"` / `"RINGGIT"`) also fails normalize. Untested.

---

## 9. Email required

Hub CHIP generate: `TryResolveEmail` — blank or `customer@example.com` (trim, case-insensitive) fails with `"Customer email is required."` Off-session also `IsUsableBuyerEmail`.

Pay:

- `PayProviders.RequiresEmail` is true for every provider **except stripe**.
- `PublicPayEndpoints.Start` after copying body email: if required and `!BuyerEmail.IsUsable` → 400 `"email is required"`.
- `ChipHosted` checks again before HTTP (defense in depth).
- `BuyerEmail.Placeholder = "customer@example.com"`; `IsUsable` matches Hub decision, not the Hub class file.
- `NameFrom(email, name)`: typed name wins; else local-part; else `"Customer"`. C30 said do not send empty client. Live always sends a non-empty `full_name` once email passed.
- Public GET sets `email_required` from checkout.Provider or org active_provider.

`:5179` disables Pay when `email_required && !email.trim()`. It does **not** treat `customer@example.com` as empty (K11.1 asked “not placeholder”). Host still 400s the placeholder. UI can submit it; API refuses. No Pay test sends the placeholder (grep `customer@example.com` in `apps/lazuar-pay/tests` is **empty**). P20.3 “Hermetic 400” is a checkmark without a CHIP (or any-rail) test.

`Chip_start_without_email_is_400` sends `{"name":"Ada"}` only. That covers P19/C30 missing email. Blank `" "` would fail `IsUsable` too; untested.

GET `email_required` is **untested**. Checkout UI depends on it so the buyer is not surprised by 400.

---

## 10. Brand ID

Hub: `merchantId` argument on generate; config column `MerchantId`; error string names Brand ID.

Pay:

- PUT: `PayProviders.RequiresPublicMerchantId` is `chip` or `billplz`. Whitespace public id → 400 `"public_merchant_id is required"`. Stripe/xendit/razorpay **reject** a public id if present (`AllowsPublicMerchantId`).
- Stored plaintext on `GatewayCredentialRow.PublicMerchantId`. Not encrypted. C11.3 “Do not treat Brand ID as a secret.”
- GET echoes `public_merchant_id`, `last4` of API secret, `webhook_configured`, `capability: hosted_link`.
- `ChipHosted`: missing/blank Brand ID → `"rail not configured"` without HTTP. Start maps that to **503**. C31.1.

Tests:

- `GatewayTests.Chip_put_requires_brand_id` PUTs chip with secret + PEM-shaped webhook_secret and **no** `public_merchant_id` → 400. Does not assert detail string. Does not PUT a successful chip row and GET Brand ID / last4 / webhook_configured.
- C11.2 “PUT chip missing PEM → 400”: shared `Put_requires_webhook_secret` uses **stripe** JSON. The branch is the same `webhook_secret is required` before provider-specific Brand ID. CHIP-labelled missing PEM is untested but the code path is shared.
- C31.1 “Start with chip row whose Brand ID is empty → 503”: **no test**. PUT cannot persist empty Brand ID. Would need to mutate DB after PUT, or insert a row in the test. Untested.
- Member PUT 403 is stripe-bodied (`Member_cannot_put_gateway`). Writer gate is provider-agnostic.

Merchant `:5178` (`WorkspacePage.tsx`): provider select includes `chip`; copy says hosted page, auto-debit later, **paste PEM, Pay does not register webhooks**; fields secret + PEM placeholder + Brand ID input; webhook URL `{payApi}/v1/webhooks/chip/{orgId}`. U12 asked for a **textarea** for PEM; live is a single-line `<input>`. PEM with headers and `\n` is painful in an input. Functional, hostile.

---

## 11. No NuGet

`Lazuar.Pay.csproj` PackageReference: `Microsoft.EntityFrameworkCore.Design` 10.0.0, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.0, `Stripe.net` 48.0.0. No Chip, ChipIn, ChipCollect, or similar.

Tests csproj: coverlet, Mvc.Testing, Test.Sdk, NUnit, EF InMemory. ProjectReference to the host.

CHIP transport is `HttpClient` + `System.Net.Http.Json` + `System.Security.Cryptography.RSA`. C29 holds.

IsolationTests bans `Razorpay.Api` by string in csproj and source. It does **not** ban a hypothetical CHIP package id. If someone added `Chip.In` later, IsolationTests would not go red unless the package name collided with `Banned` (`lazuar-api`, `Modules.`, …). C29 is currently true by csproj inspection, not by a CHIP-specific grep test.

---

## 12. No registrar (must remain refused)

### 12.1 What Hub does on PUT

`UpdatePaymentConfigCommandHandler` when `gatewayType == "CHIP"` and a **new** API key is supplied:

1. Unnamed HttpClient, Bearer = the new key.
2. Callback URL = `{App:ApiBaseUrl}/webhooks/payments/chip/{organizationId}`.
3. If that URL contains `localhost`, **rewrite host to `lazuar-local-dev.com`**.
4. `ChipWebhookRegistrar.EnsureRegisteredAsync`.
5. On any exception: `BusinessRuleValidationException` “Failed to setup CHIP Collect…” — saving the key **fails closed on registrar failure**. Ada cannot paste a key without Pay POSTing into her CHIP account.

Registrar:

- `GET https://gate.chip-in.asia/api/v1/webhooks/`
- If an item’s `callback` equals the URL (ordinal ignore case) and has `public_key`, return normalized PEM. Else company key.
- If missing: `POST` same URL with `title = "Lazuar Platform Webhook"`, events `purchase.paid`, `purchase.payment_failure`, `payment.refunded`, `purchase.preauthorized`.
- Prefer create body’s `public_key`; else `GET https://gate.chip-in.asia/api/v1/public_key/`.
- `NormalizePem`: trim, trim quotes, `\\n` → newline.

B04-P19 duplicates: list-before-create is real (`EnsureRegistered_ExistingCallback_DoesNotPostAgain`). Surprise-register is still live. Fiction DNS is still live. Billplz public-base **refuses** `lazuar-local-dev.com`; CHIP PUT **creates** it.

Company key fallback is the PEM foot-gun. Webhook signatures will not verify if the stored key is the company key.

### 12.2 What Pay does on PUT

`GatewayEndpoints.Put` injects `OneClient`, `PayDbContext`, `SecretBox`. **No HttpClient.** It encrypts secret + webhook PEM, stores Brand ID, sets `active_provider`, writes audit `gateway.credentials.upsert`, returns JSON without ciphertext.

There is no hosted service, no boot job, no “ensure CHIP webhook” call, no `POST gate.chip-in.asia/api/v1/webhooks/`.

Merchant copy on `:5178` is the 015 path: Ada pastes PEM and copies `http://localhost:8081/v1/webhooks/chip/{orgId}` (or whatever `VITE_PAY_API_URL` is) into the CHIP dashboard herself. Local CHIP webhooks need a **public** HTTPS callback the same way Billplz does; Pay does not rewrite localhost to fiction DNS. That is honest: local CHIP dogfood needs a tunnel, and the UI does not pretend otherwise (Billplz is the rail that 400s localhost callbacks; CHIP create has no callback URL in the purchase JSON — CHIP uses the dashboard subscription).

Parked: `parked-chip-registrar.md` — explicit future **button** may steal list-before-create. Not silent. Not this program.

C28.1 grep: no `/webhooks/` toward `gate.chip-in.asia` in Pay src. Confirmed.

If Ada never subscribes `purchase.paid` in the CHIP dashboard, start still returns `checkout_url`, buyer can pay, Pay never hears, `:5179` polls verifying until it gives up. Hub hid that by auto-subscribe (when registrar succeeded). 015 accepted that product cost. Document it in dogfood, not by porting the class.

---

## 13. Shared webhook TX (not CHIP-specific, CHIP uses it)

`POST /v1/webhooks/chip/{orgId}`:

1. Unknown provider 400.
2. Empty/whitespace 400 `empty body`.
3. Missing cred 400 `rail not configured`.
4. `ChipWebhook.Parse` — 400 on `PspVerifyException`, 503 on missing webhook secret.
5. Unique hit → 200 `{ duplicate: true }` **without** fulfill.
6. Ignored → insert unique (swallow duplicate) → 200 `{ ignored: reason }`.
7. Missing checkout id / wrong org → 400 `checkout not found` **without** paid unique (H13). CHIP metadata checkout_id of org t1 posted to `/chip/t2` with t2’s PEM: checkout org mismatch. **Test exists only for stripe.**
8. Currency mismatch / amount mismatch → 400, no unique.
9. Begin TX: insert `psp_webhook_events` (`chip`, `paid:{id}`), `FulfillPaidAsync`, commit. Unique violation → 200 duplicate.

Fulfill: amount<=0 no-op; status not `open` no-op; else `paid`, charge row provider `chip`, Official Receipt `RCPT-`, cash/revenue, audit `checkout.paid`. No SST. No outbox. No `GatewayPaymentCompletedIntegrationEvent`.

One TX: in-memory tests ignore transactions (`InMemoryEventId.TransactionIgnoredWarning`). Unique-violation path is the replay safety net. C25 chip replay is green on in-memory.

---

## 14. C10–C32 vs live code vs tests

Legend: **code** = present in `apps/lazuar-pay/src`. **test** = a CHIP-labelled (or honestly shared) assertion in `Lazuar.Pay.Tests`. Checklist `[x]` is ignored when tests are missing.

| ID | Intent | Code | Test | Note |
|----|--------|------|------|------|
| C10 | Small `ChipHosted`, scoped DI, no adapter/refunds | Yes | Isolation greps Hub types; no CHIP class-shape test | `Provider` is property not const |
| C11 | PUT secret + Brand ID + PEM, encrypt, active_provider | Yes | Brand ID 400 only | Missing PEM is stripe-bodied; no chip GET last4/Brand ID |
| C12 | POST purchases Bearer, brand_id, checkout_url, no recurring flags | Yes | Start 200 + body not `force_recurring` | No assert URI, Bearer, 503 on CHIP 4xx, missing URL |
| C13 | Cents AwayFromZero, not float ringgit | Yes `ToMinor` | **No** `1000` in LastBody | Paid fixture total 1000 can hide a pair of unit bugs |
| C14 | metadata checkout_id + org_id | Yes | `Contains("checkout_id")` only | `org_id` unasserted; ProviderSessionId unasserted |
| C15 | No `force_recurring` / `skip_capture` | Yes (absent) | Only `force_recurring` | **`skip_capture` not asserted** |
| C16 | 5179 verifying / cancel redirects | Yes | **No** `success_redirect` in LastBody | |
| C17 | Hermetic start → redirect_url, persist provider+session | HTTP mocked | Start 200 only | No `redirect_url` JSON assert; no DB Provider/SessionId |
| C18 | RSA PEM verify raw body | Yes | Used on happy + preauth paths | Algorithm proven only when signature is **valid** |
| C19 | `purchase.paid` → one RCPT-, balanced journal | Yes | `Chip_start_and_paid_webhook` | Does not assert checkout status `paid` |
| C20 | Event id `paid:{purchaseId}` | Yes | **No** direct EventId assert | Replay implies unique works; prefix unproven vs bare id |
| C21 | preauthorized ignored even with token | Yes | `Chip_preauthorized_is_ignored` | Load-bearing. Status `open` not asserted |
| C22 | payment_failure ignore; fail then paid still pays | Yes | **Missing both** | C22.2 exit test not written |
| C23 | Nested purchase.id then root; missing 400 | Yes | Fixture sets both equal | Nested-wins and missing-id untested |
| C24 | Missing currency 400, no MYR default | Yes | **Missing** | Hub has this test |
| C25 | Replay duplicate, one RCPT- | Yes | Inside paid test | Journal not re-checked after replay |
| C26 | Empty body 400 | Shared handler | `Chip_empty_body_400` | Status only; not `"empty body"` text. Whitespace `"  "` |
| C27 | Bad / missing RSA 400 | Yes | **Missing** | Highest CHIP test hole after fail-then-paid |
| C28 | No registrar | Yes | Isolation does not grep registrar | Src grep clean; PUT has no HttpClient |
| C29 | No CHIP NuGet | Yes | csproj Isolation does not name CHIP | Stripe.net remains |
| C30 | Start requires email | Yes | `Chip_start_without_email_is_400` | Placeholder untested |
| C31 | Brand ID required PUT; empty Brand start 503 | PUT 400 + start 503 in code | PUT 400 only | Empty-Brand start untested |
| C32 | Bundle: empty, bad RSA, paid+replay, preauth, missing currency, cross-org | Partial | **Bundle incomplete** | Cross-org is stripe-only in `WebhookTests` |

C32’s own exit said `task pay:test` green without network and “NP-GW-003 **may** flip when a human also dogfoods CHIP”. Tests alone do not close dogfood. This file does not run `task pay:test`; it reads the tests that would run.

---

## 15. Test inventory (what exists, line-level)

### 15.1 `RailTests.Chip_start_and_paid_webhook`

Seeds owner One, ephemeral RSA, stubs PSP `200 {"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}`, PUT chip `{secret, pem, public_merchant_id: brand_1}`, checkout amount 10, start `{name, email}`.

Asserts:

- start 200
- `LastBody` does not contain `force_recurring`
- `LastBody` contains `checkout_id`
- signed `purchase.paid` with total 1000 MYR metadata checkout_id → 200
- one document, number starts `RCPT-`
- journal debit sum == credit sum
- replay → body contains `duplicate`, still one document

Does **not** assert: `redirect_url` equals stub; `LastUri` contains `purchases/`; `LastBody` contains `org_id`, `brand_id`, `brand_1`, `"1000"`, `success_redirect`, `verifying`, absence of `skip_capture`; checkout `Provider`/`ProviderSessionId`; checkout `Status=paid`; `EventId` `paid:purch_1`; charge `Provider=chip`; receipt title Official Receipt; no tax journal account.

### 15.2 `RailTests.Chip_preauthorized_is_ignored`

PUT + checkout (no start). Signed preauthorized **with** `recurring_token`. 200 + body `preauthorized` + zero documents. Correct refuse of Hub vault-as-paid.

### 15.3 `RailTests.Chip_start_without_email_is_400`

PUT chip (webhook_secret `"pem"` — never used). Start name only. 400. Does not start HTTP (Psp Responder unset would 404 if it did).

### 15.4 `RailTests.Chip_empty_body_400`

PUT chip. POST `"  "` to `/v1/webhooks/chip/t1`. 400. Shared empty check; rail seed unnecessary but present. Empty string on stripe path also 400 (`PublicPayTests.Empty_webhook_is_400`).

### 15.5 `GatewayTests.Chip_put_requires_brand_id`

PUT chip secret+PEM, no Brand ID. 400.

### 15.6 Hub tests that must **not** be ported as paid behavior

- `ParseWebhook_PreauthorizedRecurringToken_IsPaymentCompletedWithVault`
- `ChargeOffSession_*`
- `IssueRefundAsync_PostsMinorUnitsToPurchaseRefund`
- `ExtractVaultIds_*` as a fulfill input
- `ParseWebhook_PaymentRefunded_IsRefundCompleted` as a Pay fulfill
- Registrar `EnsureRegistered_*`

Hub tests that **should** be cloned into Pay and are not: missing/bad RSA; missing currency; missing purchase id; nested id preferred; payment_failure EventId namespace + subsequent paid.

---

## 16. Test gaps the parent asked to name

Write these. Do not treat C32 `[x]` as done until they exist.

### 16.1 Bad RSA (C27) — **missing**

Clone `WebhookTests.Invalid_signature_is_400` onto chip:

1. PUT chip with a real PEM (or the same ephemeral public key).
2. Valid JSON `purchase.paid` that **would** pay if signed.
3. Cases: no `X-Signature`; `X-Signature: deadbeef` (non-base64); base64 of 64 zero bytes; signature from a **different** RSA key.
4. Expect 400, body `invalid signature` (or at least status 400), `Documents.Count == 0`.
5. Missing header must not 500 (`ImportFromPem` never runs).

Without this, a broken `VerifyData` that always returns true would still pass `Chip_start_and_paid_webhook` if the test signs correctly.

### 16.2 Empty body (C26) — **exists, thin**

`Chip_empty_body_400` covers whitespace. Strengthen: truly empty `""`; assert detail `"empty body"`; confirm no unique row. Optional: empty body **without** chip cred still 400 (shared order — already true on stripe path). Not a P0 hole.

### 16.3 Missing currency (C24) — **missing**

Sign:

```json
{
  "event_type": "purchase.paid",
  "id": "purch_1",
  "purchase": {
    "id": "purch_1",
    "total": 1000,
    "metadata": { "checkout_id": "<id>", "org_id": "t1" }
  }
}
```

Expect 400, no `RCPT-`, currency in the error (`missing currency`). Also: `"currency":"MY"` (not 3-letter). Also: prove the code does not write `MYR` onto the parse result by accident (Hub asserted `Currency.Should().NotBe("MYR")`).

### 16.4 Failure then paid (C22 / C20) — **missing**

This is the 008 EventId collision, the reason prefixes exist.

1. Same checkout, same purchase id `purch_1`, same PEM.
2. POST signed `purchase.payment_failure` → 200, body contains `payment_failure` or `ignored`, zero documents.
3. POST signed `purchase.paid` total 1000 MYR → 200, **one** `RCPT-`.
4. Assert DB `PspWebhookEvents` has both `failed:purch_1` and `paid:purch_1` (or at least that paid was not `{ duplicate: true }`).
5. Negative twin: if someone “fixed” ignore to use EventId `paid:` or bare `purch_1`, this test goes red.

Also write `payment_failure` alone (C22.1) if you do not want it folded into the sequence.

### 16.5 Other CHIP holes worth the same ink

- **`skip_capture` / `org_id` / cents `1000` / `success_redirect` in LastBody** (C13–C16). Cheap.
- **Start JSON `redirect_url`** equals stub URL (C17).
- **DB after start:** `Provider=chip`, `ProviderSessionId=purch_1` (C17/P18).
- **Placeholder email** `customer@example.com` → 400, and `LastUri` null / Psp not called (P20/C30).
- **Missing purchase id** on a signed paid body → 400, no Guid EventId (C23).
- **Nested id wins:** root `purch_root`, nested `purch_nested` → unique `paid:purch_nested` (C23). Hub has this.
- **Cross-org:** checkout t1, POST `/v1/webhooks/chip/t2` with t2 chip PEM and t1 checkout_id in metadata → 400, no receipt (C32 / H13). Shared handler; stripe-only test today.
- **Amount mismatch:** total 999 vs checkout 10.00 → 400, then a corrected 1000 still pays (H14 + unique not consumed).
- **Empty Brand start 503** (C31): PUT valid chip, null out `PublicMerchantId` in DB, start → 503, Psp not called.
- **CHIP HTTP 4xx** stub → start 503 `"CHIP rejected the org key"`, no secret in body.
- **GET `/v1/pay/{token}` `email_required: true`** after chip is active (K11/P19).
- **PUT chip happy GET** does not echo PEM or secret; shows Brand ID + last4 + `webhook_configured` (C11).
- **`payment.refunded` ignored**, zero docs (parked refunds).
- **Checkout status `open` after preauthorized** (C21.1).

`FakePspHandler` should record headers if Bearer is going to be asserted. Today it cannot.

---

## 17. Frontends (CHIP-facing only)

Merchant `:5178` picker includes `chip`. Copy is honest: hosted CHIP page, FPX/wallets if enabled **on the brand**, auto-debit later, paste PEM, Pay does not register webhooks. Webhook URL uses Pay origin, not Hub `/api/v1/webhooks/payments/chip/...`. Brand ID field shown. Writer-only save. Member sees last4 via GET, cannot PUT.

Checkout `:5179` has no provider picker and no wallet tiles. `email_required` from GET disables Pay when email empty. Success query `?status=verifying` polls; copy says the processor success URL is not paid. 503 → `rail not configured`. 400 → mixed message `callback base not public or email required` (Billplz vs CHIP). A CHIP missing-email 400 is a slightly wrong string. Not a money bug.

Neither Vite app imports Hub types (`IsolationTests.Vite_apps_do_not_use_hub_types`).

---

## 18. Steal vs refuse (closed list)

**Steal (HTTP / policy):**

- `POST https://gate.chip-in.asia/api/v1/purchases/` trailing slash
- `Authorization: Bearer`
- JSON keys `brand_id`, `client.email`, `client.full_name`, `purchase.products[].name/price`, `purchase.metadata`, `success_redirect`, `failure_redirect`, `cancel_redirect`
- Price in integer sen, AwayFromZero
- Response `checkout_url` + `id`
- `X-Signature` RSA SHA256 PKCS#1 v1.5 over raw UTF-8 body, PEM `ImportFromPem`
- Header name case-insensitive
- `ReadStablePurchaseId` nested then root, never Guid
- `purchase.paid` is captured money
- `TryNormalizeCurrency` / refuse missing currency
- Buyer email required; placeholder `customer@example.com` unusable
- Name from local-part when missing
- Brand ID required
- Namespaced EventId (Hub `{mapped}:{id}` → Pay `paid:{id}`)
- Empty body 400, bad sig 400
- Capability stays `hosted_link`

**Refuse:**

- Entire `ChipWebhookRegistrar` and PUT/boot side effect
- `localhost` → `lazuar-local-dev.com`
- Company `GET /public_key/` as verify key
- `force_recurring` / `skip_capture` / `$0` vault purchases
- `purchase.preauthorized` (+ token) as `PAYMENT_COMPLETED`
- `ExtractVaultIds` on the paid path
- `ChargeOffSessionAsync`, refund `/refund/`, portal
- `IPaymentGatewayAdapter` / factory / MediatR / outbox / Hub event names as fulfill types
- CHIP NuGet
- Booking `fee_amount` as 0
- Default MYR on webhook
- Bare purchase id as unique grain
- Fulfill inside `ChipHosted`
- SST / LHDN / tax journal
- Surprise-register event list including refunds/preauthorized as if Pay handled them

---

## 19. Honesty / ranked CHIP issues

**P0 product (Hub bug Pay must not regress):** `purchase.preauthorized` with a token is **ignored**. Code and `Chip_preauthorized_is_ignored` agree. Registrar refused. This is the point of the rail.

**P1 test lies (checklists `[x]`, tests absent):** C22 fail-then-paid, C24 missing currency, C27 bad RSA, C32 cross-org chip, C17 persist/redirect_url, C13 cents in body, C15 `skip_capture` absence, C31 empty Brand start, P20 placeholder. A green `task pay:test` today does **not** lock RSA reject, currency refuse, or the 008 collision.

**P2 behavior gaps (code):**

- CHIP network/timeout is 500, not 503.
- Named HttpClient has no 15s timeout.
- `ChipWebhook` requires a purchase id even for ignores (stricter than Hub; may retry junk events).
- `(long)total` truncates.
- `purchase.paid` with total 0 is not ignored at parse (mitigated by create amount>0 + H14).
- Metadata `org_id` not bound to path.
- No currency on create (same as Hub).
- Product title on CHIP’s page is always `"Pay"`.
- PEM field on `:5178` is an `<input>`, not a textarea.
- `:5179` does not block placeholder email.
- IsolationTests will not catch a future CHIP package or a ported registrar filename unless those strings hit existing bans.
- Merchant is not told **which CHIP events** to subscribe (`purchase.paid` is the only one Pay fulfills). Hub subscribed four because it handled fail/refund/vault.

**P3 nits:** C10 const vs property; P27 `Task<string>` vs `HostedSession`; typed vs named HttpClient; Hub comment about company vs webhook PEM not stolen into `ChipWebhook`; `Chip_empty_body_400` does not read the detail string; paid test does not assert `EventId` or checkout `paid`.

**Not issues:** five-verb adapter copy; factory; silent registrar; fiction DNS; Stripe.net used for CHIP; fulfill in the rail class; booking CHIP MDR; refund API.

---

## 20. Verdict

CHIP hosted_link on 8081 is a **real extract**, not a facade. `ChipHosted` (77 lines) POSTs the same purchases URL Hub used, with Brand ID, Bearer from `SecretBox`, cents, checkout/org metadata, and 5179 verifying redirects. `ChipWebhook` verifies RSA the same way Hub did, namespaces event ids, fail-closes missing currency, and **does not treat `purchase.preauthorized` as cash** even when a recurring token is present. PUT does not call `gate.chip-in.asia/api/v1/webhooks/`. There is no CHIP NuGet. Email and Brand ID are required in the host. Merchant UI tells Ada to paste the PEM.

What is **not** proven: bad signatures, missing currency, payment_failure then paid, nested vs root id, skip_capture absence, cents in the create body, persisted `ProviderSessionId`, empty Brand at start, placeholder email, chip cross-org. C10–C32 are marked done in 015; several exits are code-only. Hub’s 554-line adapter test file is still the richer RSA/currency/id suite.

Do not port `ChipWebhookRegistrar`. Do not map preauthorized to paid. Write the four named gaps (bad RSA, empty body already thin, missing currency, failure then paid) before calling NP-GW-003 closed, and do not flip it from tests alone — 015 C32 already said a human has to dogfood CHIP.

Live code to keep stealing from if those tests are added: Hub `ChipCollectGatewayAdapterTests.ParseSignedAsync`, `ParseWebhook_PurchasePaid_MissingCurrency_IsNotVerified`, `ParseWebhook_PurchasePaid_PrefersNestedPurchaseId`, `ParseWebhook_PaymentFailure_UsesStablePurchaseId`, `ParseWebhook_BadSignature_IsNotVerified`. Invert, do not copy, `ParseWebhook_PreauthorizedRecurringToken_IsPaymentCompletedWithVault`.
