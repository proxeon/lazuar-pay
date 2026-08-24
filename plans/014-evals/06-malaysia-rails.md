# 06 — Malaysian rails in Hub (CHIP Collect and Billplz): HTTP judgment for new Pay

**Date:** 24 August 2026  
**Program:** `plans/014-evals` — uncondensed evaluation. **Not an implementation.** **Not** a flip of `011/11` cells. **Not** a project reference from `apps/lazuar-pay` into `apps/lazuar-api`.  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Branch:** `main`  
**HEAD:** `ee2db8e5758305089a38298456c456d6bf0e97ca` — `feat(pay): Bar B receipts, webhook secret, merchant money UI`

Parent index: [README.md](./README.md). Binding papers: [011/11-checklist.md](../011-new-lazuar-pay/11-checklist.md) `NP-GW-001` / `NP-GW-003` / `NP-GW-007`; [013/06-money-rails.md](../013-prods/06-money-rails.md); [007/05-malaysia-gateways.md](../007-feats/05-malaysia-gateways.md) (product judgment, **not** live truth); [008-evals/02](../008-evals/02-payments-adapters-rails.md) and [009-bugs/04](../009-bugs/04-payments-adapters-webhooks.md) (historical; re-verified against live files below).

**Slice job.** How Hub’s CHIP Collect and Billplz adapters actually work on this SHA. Honest wrap-rails (CHIP can vault/off-session; Billplz is reminder + hosted bill, cannot vault). How to port **HTTP extract** into new Pay **after** Stripe dogfood. Pick **one** Malaysian rail for next, not both on day one. Confirm new Pay currently has **no** CHIP/Billplz code.

**Standing law this file obeys.**

- `NP-GW-003`: one Malaysian rail you will dogfood (CHIP **or** Billplz), not five adapters.
- `SupportsEmandate` is false for every name. No homemade FPX e-mandate.
- Billplz-class = reminder + hosted link, **never** silent debit.
- CHIP auto-charge only if a vaulted token exists.
- `PublicDnsFallback` / `BillplzPublicBase` exist because of Malaysian DNS / public-callback issues — document whether new Pay still needs them.
- `ChipWebhookRegistrar` auto-registers webhooks — steal vs refuse (new Pay must **not** surprise-register into a merchant CHIP account without an explicit merchant action).
- Steal HTTP. Do **not** copy MediatR, outbox/inbox, `IPaymentGatewayFactory`, or a factory of five.

---

## 0. Recorded tree and grep

### 0.1 Git

```
branch: main
ee2db8e5758305089a38298456c456d6bf0e97ca feat(pay): Bar B receipts, webhook secret, merchant money UI
```

Commit body on this SHA: verify Stripe webhooks with `Pay:StripeWebhookSecret` (not BYOK `sk_`), list receipts, auto-seed SST unknown as unregistered, catch a bad org key on public start. Merchant workspace can paste keys, mint a MYR pay link, and show payments. Webhook replay is a no-op.

Bar B’s first rail is **already Stripe**. This paper is the Malaysian *next* rail, not a re-pick of Bar B.

### 0.2 New Pay has no CHIP / Billplz code

Grep of `apps/lazuar-pay` for `chip|billplz|Chip|Billplz` (case-insensitive): **no matches**.

Grep of `apps/lazuar-pay/**/Lazuar.Pay/**` for the same: **no matches**.

Live new-Pay gateway tree:

```
apps/lazuar-pay/src/Lazuar.Pay/Gateways/
  GatewayEndpoints.cs     PUT/GET /v1/orgs/{orgId}/gateway — Stripe only
  StripeHosted.cs         Checkout Session mode=payment
  WebhookEndpoints.cs     POST /v1/webhooks/{provider}/{orgId} — Stripe only
```

`Lazuar.Pay.csproj` references `Stripe.net` only. `Program.cs` registers `StripeHosted` and no other rail. Merchant Vite hard-codes `provider: 'stripe'` on paste (`apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx`). IsolationTests ban `MediatR`, `Modules.`, `BuildingBlocks`, and a project reference to `apps/lazuar-api`.

**Confirmed:** there is no CHIP Collect client, no Billplz bills client, no RSA `X-Signature` verifier, no Billplz HMAC, no Brand ID field, no Collection ID field, no `PublicDnsFallback`, no `ChipWebhookRegistrar`. The Malaysian rails exist only in Hub.

### 0.3 The 400 that is the current law of 8081

```28:31:apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs
        if (provider != StripeHosted.Provider)
        {
            return PayErrors.Status(400, "Bad Request", "Bar B first rail is stripe");
        }
```

Webhook twin:

```26:29:apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs
        if (string.Equals(provider, StripeHosted.Provider, StringComparison.OrdinalIgnoreCase) == false)
        {
            return PayErrors.Status(400, "Bad Request", "unknown provider");
        }
```

Public start is not a switch. It always constructs a Stripe Checkout Session:

```41:70:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        StripeHosted stripe,
        CancellationToken ct)
    {
        // ...
            var url = await stripe.CreateHostedUrlAsync(row, ct);
            row.PspRedirectUrl = url;
```

PUT of `{ "provider": "chip", "secret": "…" }` or `{ "provider": "billplz", "secret": "…" }` is **400** today. That is correct for Bar B. It is the seam the next Malaysian rail must widen **without** becoming a factory of five.

---

## 1. What Hub files this paper actually opened

Must-open list, all read in full on this SHA:

| File | Lines | Role |
|------|------:|------|
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` | 648 | CHIP Collect: purchases, RSA webhooks, refund, off-session charge |
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipWebhookRegistrar.cs` | 131 | Auto-subscribe CHIP webhooks + PEM extract |
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs` | 333 | Billplz v3 bills, HMAC form callback, no vault, no refund |
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzPublicBase.cs` | 85 | Public HTTPS callback vs sandbox/prod API host |
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/PublicDnsFallback.cs` | 193 | 1.1.1.1 / 8.8.8.8 A-record connect hook; **Billplz-named client only** |
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs` | 146 | Email, minor units, paying-tenant metadata, fee-status stamp |
| `apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs` | 58 | Honest matrix |
| `apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs` | 87 | Capability-blind port (do **not** copy as the new Pay shape) |
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/PaymentGatewayFactory.cs` | 26 | String match. Cathedral. Do not copy. |
| `apps/lazuar-api/Modules/Payments/Infrastructure/Commands/UpdatePaymentConfigCommandHandler.cs` | 173 | CHIP key-save **surprise-registers** webhooks |
| `apps/lazuar-api/Modules/Payments/Infrastructure/Endpoints.cs` | 100 | `POST /webhooks/payments/{gatewayType}/{tenantId}` |
| `apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` | (partial) | Verify → log unique `(org, provider, event_id)` → outbox. Do not copy the bus. |
| `apps/lazuar-api/Modules/Payments/Infrastructure/DependencyInjection.cs` | 70 | Five `AddScoped<IPaymentGatewayAdapter, …>` + named Billplz HttpClient |

Tests opened: `ChipCollectGatewayAdapterTests.cs`, `ChipWebhookRegistrarTests.cs`, `BillplzGatewayAdapterTests.cs`, `BillplzPublicBaseTests.cs`, `PublicDnsFallbackTests.cs`, `PaymentGatewayCapabilitiesTests.cs`, `GatewayCommonTests.cs`, `BillplzFeeHonestyTests.cs`, `PaymentWebhookEmptyBodyTests.cs`.

Hub registration of the five, quoted because this is the factory new Pay must **not** grow:

```34:47:apps/lazuar-api/Modules/Payments/Infrastructure/DependencyInjection.cs
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
```

`PublicDnsFallback.HttpClientName` is `"Billplz"`. CHIP does **not** use this client. CHIP calls `_httpFactory.CreateClient()` (unnamed, machine DNS). That is live fact, not folklore.

---

## 2. Historical papers vs live files (008 / 009 / 007 are not authority)

`013/06` already warned: `008-evals/02` was written against a pre-fix tree; `009-bugs/04` re-read `297ba98` / `30d07d2`. Live adapters on this SHA (`ee2db8e5`, Hub code unchanged in spirit from the `6f866ff0` walk in 013) have moved. This section re-checks every CHIP / Billplz claim that 007/008/009 made, against the files opened above.

| Claim in 007 / 008 / 009 | Live on `ee2db8e5` | Status |
|--------------------------|--------------------|--------|
| CHIP `EventId` = bare purchase id (008 P0 fail-then-pay collision) | `EventId = $"{mappedEventType}:{purchaseId}"` (`ChipCollectGatewayAdapter.cs:185`). Tests: `PAYMENT_COMPLETED:purch_root_1`, `PAYMENT_FAILED:purch_fail_1`, `REFUND_COMPLETED:purch_ref_1`. | **Fixed.** Do not re-introduce bare object id. |
| Billplz `EventId` = bare bill id | `EventId = $"{COMPLETED\|FAILED}:{billId}"` (`BillplzGatewayAdapter.cs:237–240`). Tests: `PAYMENT_COMPLETED:bill_abc123`, `PAYMENT_FAILED:bill_unpaid_1`. | **Fixed** at adapter. Residual: unpaid-after-paid is a **new** EventId (B04-P08). Hub handler now **ignores late FAIL after COMPLETED** (`ProcessGatewayWebhookCommandHandler.cs:123–137`). Steal that ignore. |
| CHIP EventId Guid fallback if id missing (007 table) | `ReadStablePurchaseId` never invents a Guid. Missing id → `Verified=false` + `AsUnusable()`. Test `ParseWebhook_PurchasePaid_NoIds_IsNotVerified`. | **Closed.** Keep fail-closed. |
| Empty webhook body HTTP 500 (009 B04-P18) | `Endpoints.cs:45–48` returns `Results.BadRequest` `"Empty request body."`. Test `PaymentWebhookEmptyBodyTests`. New Pay already 400s empty (`WebhookEndpoints.cs:33–36`). | **Closed** on both hosts. |
| Webhook unique `(Provider, EventId)` not tenant-scoped (009 B04-P06) | Hub: unique `(OrganizationId, Provider, EventId)` (`PaymentConfigurations.cs:29–30`). Lookup is tenant-scoped (`GetByEventIdAsync(..., request.TenantId)`). New Pay PK is `(OrgId, Provider, EventId)` on `psp_webhook_events`. | **Closed.** Keep the triple. |
| CHIP `$0` `skip_capture` never vaults; `purchase.preauthorized` dropped (009 B04-P01 / issue 005) | Generate still sets `force_recurring` + `skip_capture` when amount cents == 0. Parse **now** maps `purchase.preauthorized` **with a recurring token** to `PAYMENT_COMPLETED` and extracts vault ids. Auth-hold without token stays raw `purchase.preauthorized` (not paid). Tests: `ParseWebhook_PreauthorizedRecurringToken_IsPaymentCompletedWithVault`, `ParseWebhook_PreauthorizedAuthHold_IsNotPaymentCompleted`. | **Partially closed at Hub parse.** Still an `NP-GW-008` hazard if new Pay books that as **paid money**. Steal: preauthorized + token = **vaulted, not captured**. `purchase.paid` = money. New Pay `Fulfillment.FulfillPaidAsync` already refuses `checkout.Amount <= 0` and Stripe webhook ignores `mode=setup` / amount 0. Keep that when CHIP lands. |
| CHIP `payment.refunded` registered and dropped (007 / 008 / 009 B04-P13 / B04-P22) | Mapped to `REFUND_COMPLETED` (`ChipCollectGatewayAdapter.cs:167–171`). Test `ParseWebhook_PaymentRefunded_IsRefundCompleted`. | **Closed at parse.** New Pay has **no refund path yet** — do not invent one on the first Malaysian rail day. |
| CHIP off-session has no idempotency key (009 B04-P04) | Live sends `Idempotency-Key` header via `SendJsonAsync`, stamps `reference` on create, and `GET /purchases/?reference=` to reuse. Tests: `ChargeOffSession_SendsProcessorIdempotencyKeyOnCreateAndCharge`, `ChargeOffSession_ReusesExistingPurchaseForSameIdempotencyKey`. `pending_charge` is **no longer** adapter-true. | **Mitigated.** Not Stripe-class (CHIP may ignore the header). Steal the reference lookup. Adapter-true is only `status == "paid"`. |
| CHIP off-session treats `pending_charge` as success (009 B04-P07) | `IsOffSessionPaid` is **only** `"paid"` (case-insensitive). Test locks `pending_charge` false. | **Closed.** Steal the lock. |
| CHIP GET-by-token 404 cannot fall back to client (009 B04-P03) | `ResolveOffSessionClientAsync` tries token as purchase, then customer as purchase, then `GET /clients/{customerId}/`. Test `ChargeOffSession_TokenGet404_FallsBackToClient`. | **Closed at adapter.** Steal the fallback. |
| CHIP clobbers paying `tenant_id` (009 B04-P05) | `GatewayCommon.ApplyPayingTenantMetadata`. Test `GenerateCheckout_KeepsPayingTenant_AndStampsPlatformTenant`. | **Closed.** New Pay has no “system org”; still stamp `org_id` / `checkout_id` and never overwrite a paying org. |
| CHIP currency invented as MYR (009 B04-P15) | `TryNormalizeCurrency`; missing → unusable, **refuses to default to MYR**. Test `ParseWebhook_PurchasePaid_MissingCurrency_IsNotVerified`. | **Closed.** Steal fail-closed. |
| CHIP webhook auto-register duplicates; PEM is company key (009 B04-P19) | `ChipWebhookRegistrar.TryFindExistingAsync` lists by callback URL (B04-P19 mitigated). Prefers `Webhook.public_key`, falls back to `GET /public_key/`. Comment on the type: “Verify PEM is Webhook.public_key, not the company GET /public_key/ key.” Hub **still surprise-registers on CHIP key save** (`UpdatePaymentConfigCommandHandler.cs:106–132`) and still rewrites `localhost` → `lazuar-local-dev.com`. | **Duplicates mitigated. Surprise-register still live. Fiction DNS still live.** New Pay: **refuse** surprise-register. See §8. |
| Billplz `ChargeOffSessionAsync` throws `NotSupportedException` (007) | Returns `false`, logs a warning, discards idempotency args. Test `ChargeOffSessionAsync_DoesNotThrow_ReturnsFalse`. Capability short-circuit in `ExecuteOffSessionChargeIntegrationEventHandler` never even calls it for `BILLPLZ`. | **Live = return false, not throw.** New Pay: **do not implement** the method for Billplz. Absence is honesty. |
| Billplz minor units truncate; CHIP banker-round (007 / 008 / 009 B04-P17) | `ToMinorUnits` is `MidpointRounding.AwayFromZero`. `ToMinorUnitsTruncating` **calls the same function**. Test: `ToMinorUnits(10.005m) == 1001` (banker's ToEven would be 1000); `ToMinorUnitsTruncating(10.009m) == 1001`. | **Names lie.** Both rails round away-from-zero on live. Steal the live policy, not the method names. |
| Hub hostname infers Billplz live (older quickstart / 007) | `BillplzPublicBase.IsProductionApi` follows `App:BillplzEnvironment` then tenant `environment`. Comment: do **not** infer from Hub hostname (`pay-local.lazuar.com` must never go live). Tests lock hostname does not force live. | **Closed.** Steal the comment. |
| `SupportsEmandate` maybe someday for Curlec | Always `return false`. Unused `gatewayName`. | **Law.** Do not grow it for CHIP or Billplz. |

**Do not treat 007’s capability table (lines 1070–1088) as live.** It still says CHIP EventId Guid fallback, CHIP maps `purchase.paid` only, CHIP `payment.refunded` unmapped, Billplz off-session throws, CHIP banker round. Every one of those cells is stale versus the files quoted in this paper.

**Do not treat 008’s EventId = object id as live.** `a1afc09` namespaced CHIP/Billplz. Tests lock the namespace.

**Do not treat 009 B04-P01 as still “never vaults.”** Parse extracts the token. The remaining bug for **new Pay** is booking that event as cash (`NP-GW-008`), not failing to see the token.

---

## 3. Capability matrix (honest wrap-rails)

Live Hub helper, quoted in full because new Pay must steal the **judgments**, not the Contracts project:

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

Tests lock: `STRIPE`/`CHIP` off-session true; `BILLPLZ` false; API refund CHIP true Billplz false; mark-refunded Billplz true CHIP false; e-mandate Xendit false (and therefore every name); Billplz GrabPay hosted-wallet **false** even though `SupportsDuitNowQr("BILLPLZ")` is true.

### 3.1 Matrix for the two Malaysian rails, as of this SHA

| Axis | CHIP Collect | Billplz v3 |
|------|--------------|------------|
| Hub `GatewayType` | `"CHIP"` | `"BILLPLZ"` |
| New Pay provider string (proposed) | `chip` (lowercase, match Stripe’s `stripe`) | `billplz` |
| Who acquires | CHIP IN Sdn. Bhd. — merchant’s CHIP brand | Billplz Sdn. Bhd. — merchant’s collection |
| Lazuar’s job | BYOK wrap. Hosted purchase URL. Verify RSA. Optionally charge a **card** token later. | BYOK wrap. Hosted bill URL. Verify HMAC. Reminder forever. |
| Hosted checkout | `POST /api/v1/purchases/` → `checkout_url` | `POST /api/v3/bills` → `url` |
| Methods on hop-2 | Whatever the **brand** enabled: FPX, cards, DuitNow QR, wallets, BNPL, maybe Google Pay / stablecoins. Lazuar sends **no** allow-list. | Whatever the **collection** enabled: FPX, cards, wallets, Atome. Lazuar sends **no** method codes. |
| `SupportsOffSession` | **true** | **false** |
| Vault mechanism | `force_recurring` on generate; `recurring_token` on webhook; `POST …/charge/` with `{ recurring_token }` | **None in the adapter.** Billplz Agreements v5 / Auto-Deduct exist at the company. Pay **refuses** to homemade them (`NP-XX-011` spirit). |
| Auto-charge condition | Only if a vaulted **card** token exists. Never FPX. CHIP FAQ: CHIP does not run the subscription clock. | **Never.** Returning false and inventing PAST_DUE is `NP-FUL-005`. |
| `SupportsEmandate` | **false** | **false** |
| `SupportsApiRefund` | **true** (`POST …/refund/` with optional amount, `Idempotency-Key`) | **false**. Payment Order is a **new disbursement**, not a reversal. |
| `RequiresMarkRefunded` | false | **true** |
| Customer portal | Throws `InvalidOperationException` | Throws `InvalidOperationException` |
| DuitNow QR | Flag true; pixels are CHIP’s page | Flag true; pixels are Billplz’s page |
| Hosted wallets flag | GrabPay / ShopeePay / TnG / Boost / DuitNow **true** (unread by generate) | **false** for GrabPay even if the collection shows it. Do not productize the unread flags. |
| Currency | Fail-closed on webhook (`TryNormalizeCurrency`). Generate ignores ISO and always posts sen. | Webhook **hardcodes `"MYR"`**. Generate ignores the `currency` argument. Acceptable **for Billplz only**. |
| Fees | `payment.fee_amount` / `net_amount` sen when present; else `gateway_fee_status=unknown` and fee 0 | Formula exists; Hub handler always passes `0,0,0`. Production fee is always 0. Do not invent a fee (`NP-MON-002`). |
| Metadata out | JSON `purchase.metadata` (first-class) | `reference_1` / `reference_2` + **callback query** (`type`, `reference_1`, `checkout_id`) because Billplz strips body metadata |
| Webhook transport | JSON POST | `application/x-www-form-urlencoded` POST |
| Signature | Header `X-Signature` base64, RSA SHA256 PKCS#1 v1.5 of **raw body**, PEM | Body field `x_signature`, HMAC-SHA256 hex, dual-compute |
| EventId | `{mapped}:{purchaseId}` | `{mapped}:{billId}` |
| Sandbox | Same host `gate.chip-in.asia`. Test mode is a **dashboard toggle**. | Separate host `www.billplz-sandbox.com` + separate account. |
| Local callback | Hub rewrites localhost → fiction DNS `lazuar-local-dev.com` on register. **Do not copy.** | Fail-closed unless public HTTPS or `App:AllowInsecureBillplzCallback`. |
| DNS fallback | Not used | Named HttpClient `"Billplz"` with UDP A-record to 1.1.1.1 / 8.8.8.8 |
| Auto webhook provision | **Yes**, on CHIP key save (surprise). | **No**. Per-bill `callback_url`. |
| Bar B new Pay today | Absent (400) | Absent (400) |
| Wrap-rails copy | “Hosted page shows whatever you enabled on the brand. Auto-debit is **card token only**. We will not silent-debit FPX. CHIP does not run your subscription clock.” | “**Pay-link renewals.** Billplz cannot vault. Each cycle we create a hosted bill and email it. There is no silent auto-charge. Use Stripe or CHIP Collect when you need recurring auto-debit.” |
| New Pay GET capability today | Stripe PUT/GET returns `capability = "hosted_link"` even for Stripe (Bar B honesty: first charge is hosted hop-2, no vault yet). | n/a |

`NP-GW-007` in 011:

> Honest matrix: Stripe/CHIP auto-charge if vaulted; Billplz-class = reminder + hosted link. Never silent debit on reminder-only rails.

Bar B already stores `hosted_link` next to Stripe. That is correct **until** a PM / CHIP `recurring_token` is persisted. Flipping Stripe (or future CHIP) to `vaulted_autocharge` before a real token exists would be the same class of lie as printing “we will charge your card automatically” on Billplz.

New Pay must **not** copy unread Hub flags (`SupportsDuitNowQr`, `SupportsHostedWallet`) onto `:5178` / `:5179` as tiles. QR and wallets live on the processor page.

---

## 4. CHIP Collect — live HTTP walk

File: `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` (648 lines). `GatewayType => "CHIP"`. API base **always**:

```22:22:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs
    private const string ApiBaseUrl = "https://gate.chip-in.asia/api/v1/";
```

Tenant `environment=test` does **not** select a CHIP test host. Test vs live is the CHIP dashboard switch on the **same** API. Merchant copy must say so. A `sk_test_`-shaped prefix guard is a Stripe-only idea; CHIP secrets are not Stripe-shaped.

### 4.1 HTTP endpoints the adapter actually hits

| Method | URL | When | Auth | Body / notes |
|--------|-----|------|------|----------------|
| `POST` | `https://gate.chip-in.asia/api/v1/purchases/` | Generate checkout | `Authorization: Bearer {apiKey}` | `brand_id`, `client.email` + `full_name`, `purchase.products[]` (name, `price` sen), `purchase.metadata`, `success_redirect`, `failure_redirect`, `cancel_redirect`. Optional `force_recurring`, `skip_capture`. |
| `POST` | `https://gate.chip-in.asia/api/v1/purchases/` | Off-session create | Bearer + optional `Idempotency-Key` | Same shape; `reference` = processor idempotency key; metadata `type=commerce_subscription`, `subscription_id`, `tenant_id`, `receipt`, optional dunning/tax stamps. |
| `GET` | `https://gate.chip-in.asia/api/v1/purchases/?reference={key}` | Off-session reuse | Bearer | List/object; first `id` wins. Handles `{ results: [] }` **or** a bare array. |
| `GET` | `https://gate.chip-in.asia/api/v1/purchases/{id}/` | Off-session: resolve brand/client; or lookup existing paid | Bearer | Token id and customer id are both tried as purchase ids. |
| `GET` | `https://gate.chip-in.asia/api/v1/clients/{customerId}/` | Off-session fallback | Bearer | Brand + email + name from client object. |
| `POST` | `https://gate.chip-in.asia/api/v1/purchases/{id}/charge/` | Off-session capture | Bearer + optional `Idempotency-Key` | `{ "recurring_token": tokenId }`. Success only if JSON `status` is `paid`. |
| `POST` | `https://gate.chip-in.asia/api/v1/purchases/{transactionId}/refund/` | Refund | Bearer + `Idempotency-Key: lazuar-refund:{id}:{minor}` | `{}` or `{ "amount": sen }` if amount > 0. HTTP success → true. |

Registrar (not the adapter; called from config save):

| Method | URL | When | Auth |
|--------|-----|------|------|
| `GET` | `https://gate.chip-in.asia/api/v1/webhooks/` | List existing callbacks | Bearer |
| `POST` | `https://gate.chip-in.asia/api/v1/webhooks/` | Create if missing | Bearer; events listed in §4.3 |
| `GET` | `https://gate.chip-in.asia/api/v1/public_key/` | Fallback PEM if webhook object has no `public_key` | Bearer |

**Not called:** `/purchases/{id}/capture/` (we never capture a `skip_capture` hold). **Not called:** token list/delete APIs. **Not called:** CHIP Send / Expense / Advance. **Not called:** payment-method whitelist. **Not called:** CHIP mini / POS.

HttpClient: **unnamed** default. No `PublicDnsFallback`. If `gate.chip-in.asia` fails DNS on a LAN, CHIP generate fails; Hub did not special-case it.

### 4.2 Generate checkout

Requires `merchantId` (Brand ID). Missing → `"MerchantId (Brand ID) is required for CHIP Collect."`

Requires a usable buyer email (`GatewayCommon.TryResolveEmail`). Placeholder `customer@example.com` is refused. `full_name` is `ExtractName(email)` = **email local-part**. 013/05 already called this a class of lie. New Pay has `PayerName` on the checkout row (`NP-BUY-001`). Steal: send the session name, fall back to local-part only if blank.

Amount: `GatewayCommon.ToMinorUnitsRounded(amount, quantity)` = away-from-zero sen, quantity folded into one product line. Description: `ProductDescription(productName, quantity)` (`"Plan (x3)"`).

Vault flags (the `$0` path):

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

CHIP official callbacks (quoted via 009, still the official rule): `skip_capture=true` success callback fires on **capture**, not on pay. We never `POST /capture/`. The buyer-complete event for a `$0` vault is `purchase.preauthorized`, not `purchase.paid`.

Success parse: `checkout_url` + root `id` as session id. No URL → error. HTTP non-success logs tenant + body.

**There is still no Hub test that generate with `setupFutureUsage` and amount 0 actually sets `skip_capture`.** 009 called that a lying-by-omission test. It is **still** absent in `ChipCollectGatewayAdapterTests.cs`. Steal as a **new Pay** test if CHIP is the next rail and you ever send `$0` / setup. Bar C first Malaysian charge should be **non-zero** `mode=payment` equivalent (a real purchase, `skip_capture` off) so `NP-GW-008` cannot fire on day one.

Paying-tenant metadata: `ApplyPayingTenantMetadata` keeps existing `tenant_id`, stamps `platform_tenant_id` when the adapter tenant differs. New Pay has no platform/system org; still put `org_id` and `checkout_id` in `purchase.metadata` (CHIP will echo them on the webhook — first-class JSON, no query hack).

### 4.3 Webhook verify algorithm

Inbound Hub path: `POST /webhooks/payments/chip/{tenantId}`. New Pay already uses `POST /v1/webhooks/{provider}/{orgId}` — CHIP would be `/v1/webhooks/chip/{orgId}`.

Algorithm, live:

1. Find header whose name equals `X-Signature` (ordinal-ignore-case). Missing → `Verified=false`, `"Missing X-Signature header."`
2. `bodyBytes = Encoding.UTF8.GetBytes(rawBody)` — **the string the ASP.NET endpoint already read**. Do not JSON re-serialize. Model-binding the body before verify will break CHIP the same way it breaks Stripe.
3. `signatureBytes = Convert.FromBase64String(signatureBase64)`.
4. `RSA.Create(); rsa.ImportFromPem(webhookSecret);`.
5. `rsa.VerifyData(bodyBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)`.
6. Fail → `Verified=false`, `"RSA signature verification failed."` Hub then 500s (`InvalidOperationException` “Webhook signature verification failed”) so CHIP retries. New Pay Stripe path 400s invalid signature (`WebhookEndpoints.cs:57–60`). **Prefer 400** so a wrong PEM does not hammer the host; a **wrong** secret should not retry forever. A **transient** parse exception after a valid signature is a different story — Hub maps unusable-after-verify to 400 (`PaymentWebhookUnusablePayloadException`) so the PSP **stops**. Steal that split: bad sig → 400 (or 401); verified but missing id/currency → 400 unusable; unknown event type → 200 ignore.

Event map, live (this is the table 007 got wrong):

| CHIP `event_type` | Mapped `EventType` | When | New Pay should treat as |
|-------------------|--------------------|------|-------------------------|
| `purchase.paid` | `PAYMENT_COMPLETED` | Always (after id+currency) | **Paid money.** Fulfill if amount > 0 and checkout open. |
| `purchase.preauthorized` **and** a recurring token is present | `PAYMENT_COMPLETED` | `$0` skip_capture vault | **Vaulted, not captured.** Do **not** mint `RCPT-`. Do **not** journal cash. Persist token if/when Pay has a vault column. `NP-GW-008`. |
| `purchase.preauthorized` without token | passthrough raw type | Auth-hold | 200 ignore. Not paid. |
| `purchase.payment_failure` | `PAYMENT_FAILED` | | Log. If checkout already `paid`, ignore (Hub late-fail guard). Do not reverse a journal on a later fail. |
| `payment.refunded` | `REFUND_COMPLETED` | | Hub maps it. New Pay has no refund SOP yet. 200 ignore **or** park for paper 07. Do not reverse cash in Bar C day-one. |
| anything else | passthrough | `customer.updated`, etc. | 200 ignore. |

Stable purchase id:

```574:587:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs
    internal static string? ReadStablePurchaseId(JsonElement root)
    {
        if (root.TryGetProperty("purchase", out var purchase)
            && purchase.ValueKind == JsonValueKind.Object)
        {
            var nested = ReadString(purchase, "id");
            if (!string.IsNullOrWhiteSpace(nested))
            {
                return nested;
            }
        }

        return ReadString(root, "id");
    }
```

Nested `purchase.id` first, then root `id`. Never a Guid. EventId = `$"{mapped}:{purchaseId}"`. `GatewayTransactionId` = purchase id (the object, not the namespaced EventId). New Pay `psp_webhook_events.event_id` should store the **namespaced** id (or CHIP’s own event uuid **if** they ever send one — they do not, today). Do **not** store bare purchase id: fail then pay would collide.

Vault extract (`ExtractVaultIds`):

- `recurring_token` on root or purchase node → token id.
- Else if `is_recurring_token` true → token id = stable purchase id.
- Customer id = `client.id` on root or purchase; if missing and token exists, customer falls back to token (charge path only needs the token).

Fees: if root `payment` object has `fee_amount`, `GatewayFee = fee/100` and `gateway_fee_status=known`. Else stamp `unknown`. `NetAmount` from `net_amount` if present else amount paid. Tax on this adapter is always 0 (SST is Pay’s job, not CHIP’s). FxRate 1, BaseCurrency = purchase currency.

Registrar events (what CHIP will **deliver** if Hub/Pay subscribed):

```33:33:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipWebhookRegistrar.cs
            events = new[] { "purchase.paid", "purchase.payment_failure", "payment.refunded", "purchase.preauthorized" },
```

New Pay should subscribe to at least `purchase.paid` and `purchase.payment_failure`. Add `purchase.preauthorized` **only** if you have a vault column and an `NP-GW-008` test that amount 0 does not fulfill. Add `payment.refunded` only with a refund SOP.

### 4.4 Refund / off-session / portal

**Refund.** `IssueRefundAsync` posts sen, optional amount, `Idempotency-Key = "lazuar-refund:" + transactionId + ":" + minor`. HTTP success → true. Test `IssueRefundAsync_PostsMinorUnitsToPurchaseRefund` expects `https://gate.chip-in.asia/api/v1/purchases/purch_99/refund/` and body `"1234"`. CHIP extra refund fee is FPX-only at the processor (RM 1 / RM 2); we do not model it. Dashboard refunds now **can** enter Hub via `payment.refunded` → `REFUND_COMPLETED`. New Pay: do not ship refund HTTP on the first Malaysian hosted-charge day unless paper 07 is in the same slice. Capability still says CHIP **can** API-refund; UI must not say “mark refunded only” the way Billplz must.

**Off-session.** Full sequence:

1. Resolve brand + client email (purchase by token, purchase by customer, client by customer). No brand or unusable email → false.
2. If idempotency key present, `GET purchases/?reference=` and if found, `ChargeOrReusePurchaseAsync(..., lookupStatus: true)`: GET purchase, if already `paid` return true **without** charging again; else POST charge.
3. Else POST new purchase (with `reference` if key present).
4. POST `{id}/charge/` with `{ recurring_token }`. True only if `status == "paid"`.

Hub Commerce / dunning **will** call this because `SupportsOffSession("CHIP")` is true. New Pay **must not** call it until (a) a token was stored from a verified webhook and (b) a renew job exists. 013/06: “Until then, **do not implement AUTO_CHARGE**.” A billing job that cannot see the helper will AUTO_CHARGE Billplz. The defence is: **no ChargeOffSession method on the Billplz type, and no call site until the helper exists.**

**Portal.** Throws. Keep throwing. There is no CHIP Billing Portal. Buyer “update payment” on CHIP is a new hosted purchase with `force_recurring`, not a portal session.

### 4.5 Secrets shape (CHIP)

Hub `TenantPaymentConfiguration` for CHIP:

| Field | What it is | Secret? |
|-------|------------|---------|
| `ApiKey` | CHIP secret key (Bearer) | **Yes.** Encrypted. |
| `MerchantId` | Brand ID (UUID-ish) | **No.** Needed on every `POST /purchases/`. |
| `WebhookSecret` | RSA **public** PEM used to verify `X-Signature` | Public key, but stored encrypted anyway. Prefer `Webhook.public_key` from the webhook object, not company `GET /public_key/` (success-callback key). |
| `Environment` | `test` / `live` | Not a CHIP host switch. Copy-only. |

New Pay `GatewayCredentialRow` today:

```56:62:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
public sealed class GatewayCredentialRow
{
    public required string OrgId { get; set; }
    public required string Provider { get; set; }
    public required string Ciphertext { get; set; }
    public string? Last4 { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

PUT body is `{ provider, secret }` only. That is enough for Stripe secret key (webhook verify uses **platform** `Pay:StripeWebhookSecret`, not the org `sk_`). CHIP **cannot** fit in one `secret` without lying:

- Brand ID is not a secret but **is required** to mint a purchase.
- Webhook PEM is per-org (CHIP signs with the webhook’s key pair). It is **not** a Pay-wide env like Stripe’s `whsec_`.
- Bearer secret is per-org, like Stripe `sk_`.

Porting CHIP therefore **extends** the credential row (or stores a small JSON envelope inside ciphertext). Do not overload `Last4` as Brand ID. Do not put Brand ID in Vite as `VITE_CHIP_*`.

Stripe in new Pay verifies with `Pay:StripeWebhookSecret` (one secret for the Pay process). That matches Stripe’s “one endpoint, one whsec” Connect-less BYOK only if **every** merchant’s Stripe account sends to Pay’s endpoint **and** Pay uses each merchant’s own endpoint secret — which Bar B does **not**. Bar B is a **Pay-operated** Stripe webhook secret. CHIP’s PEM is **per merchant account**. You cannot copy the Stripe env-var pattern for CHIP. The PEM belongs next to the org’s CHIP key.

### 4.6 `ChipWebhookRegistrar` — steal vs refuse

Hub `UpdatePaymentConfigCommandHandler` on a **new** CHIP API key (not keep-existing):

```106:132:apps/lazuar-api/Modules/Payments/Infrastructure/Commands/UpdatePaymentConfigCommandHandler.cs
        if (gatewayType == "CHIP" &&
            !SecretVaultExtensions.IsKeepExistingSecret(request.ApiKey) &&
            !string.IsNullOrEmpty(resolvedPlainApiKey))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", resolvedPlainApiKey);

                var apiBaseUrl = _configuration["App:ApiBaseUrl"]?.TrimEnd('/') ?? "http://localhost:8080/api/v1";
                var webhookUrl = $"{apiBaseUrl}/webhooks/payments/chip/{request.OrganizationId}";

                if (webhookUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase))
                {
                    webhookUrl = webhookUrl.Replace("localhost", "lazuar-local-dev.com", StringComparison.OrdinalIgnoreCase);
                }

                resolvedPlainWebhook = await ChipWebhookRegistrar.EnsureRegisteredAsync(client, webhookUrl, ct);
            }
            catch (Exception ex)
            {
                throw new BusinessRuleValidationException(
                    new GenericBusinessRule($"Failed to setup CHIP Collect. Please verify your API Key. Detail: {ex.Message}"));
            }
        }
```

What this does in the merchant’s CHIP account, **without a second confirmation**:

1. `GET /api/v1/webhooks/` with **their** Bearer key.
2. If no row has `callback` equal to Pay/Hub’s URL, `POST /api/v1/webhooks/` titled `"Lazuar Platform Webhook"` for four events.
3. Store whatever PEM came back (webhook object, else company key) as **their** `WebhookSecret`.
4. If Hub’s public base was `http://localhost:8080/...`, rewrite the callback to `http://lazuar-local-dev.com:8080/...` — a **fiction DNS** that BillplzPublicBase would **refuse**.

Registrar list-before-create is the part worth stealing (tests: `EnsureRegistered_ExistingCallback_DoesNotPostAgain`, `EnsureRegistered_MissingCallback_PostsOnce_UsesWebhookPublicKey`). PEM normalize (`Trim`, strip quotes, `\\n` → newline) is worth stealing. Company-key fallback is a last resort and a soak risk (CHIP docs: webhook deliveries use the webhook key pair; success-callback uses company key).

**Refuse for new Pay:**

- Auto-POST into the merchant’s CHIP account as a side effect of “paste secret key.” That is a write to a third-party system the merchant did not click. Surprise-register is how Hub grew duplicate webhook rows (B04-P19) and how a forgotten ngrok URL keeps getting paid events.
- Localhost → `lazuar-local-dev.com` rewrite. If `Pay:PublicBaseUrl` is loopback / non-HTTPS, **do not register**. Print the callback URL for the merchant to paste in CHIP portal, or fail the “register webhook” action with the same class of error as `CALLBACK_BASE_NOT_PUBLIC`.
- Blocking key-save on registrar failure (`Failed to setup CHIP Collect. Please verify your API Key`). A valid secret + Brand ID should save. Webhook registration is a **separate explicit action**: button “Register Pay webhook on this CHIP brand” or a checkbox Ada ticks, only when public HTTPS is known.

Steal the HTTP of `EnsureRegisteredAsync`. Call it only from that explicit action. Title the CHIP webhook `"Lazuar Pay"` (not `"Lazuar Platform Webhook"` / Hub). Callback must be `https://<pay-public>/v1/webhooks/chip/<orgId>`.

---

## 5. Billplz — live HTTP walk

File: `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs` (333 lines). `GatewayType => "BILLPLZ"`.

```22:23:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs
    private const string ProductionApiUrl = "https://www.billplz.com/api/v3/";
    private const string SandboxApiUrl = "https://www.billplz-sandbox.com/api/v3/";
```

007 product judgment (still true as **market**, not as adapter completeness): Billplz is the Malaysian default “just send a bill.” v3 is **frozen** (official: new work is v4/v5). Hub is on v3. New Pay, if it ever takes this rail, stays on v3 bills — do not quietly start Agreements v5.

### 5.1 HTTP endpoints the adapter actually hits

| Method | URL | When | Auth | Body / notes |
|--------|-----|------|------|----------------|
| `POST` | `https://www.billplz.com/api/v3/bills` **or** `https://www.billplz-sandbox.com/api/v3/bills` | Generate | HTTP Basic `base64("{apiKey}:")` (empty password) | JSON: `collection_id`, `email`, `name`, `amount` sen, `description`, `callback_url`, `redirect_url`, `reference_1_label/1`, `reference_2_label/2` |

**That is the only outbound call.** No refund HTTP. No charge HTTP. No webhook CRUD. No GET bill (session merge is Hub-side by stored bill id). No delete-unpaid-bill.

HttpClient name: `PublicDnsFallback.HttpClientName` (`"Billplz"`). Connect callback: custom UDP DNS to 1.1.1.1 then 8.8.8.8 for A records, then `Dns.GetHostAddressesAsync` as last resort. Comment: “common for `www.billplz-sandbox.com` on some LANs.”

### 5.2 Generate checkout

Requires `merchantId` (Collection ID). Missing → `"MerchantId (Collection ID) is required for Billplz."`

Requires usable email. Name = email local-part again.

Public callback base:

```66:83:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs
        var apiBaseUrl = _configuration["App:ApiBaseUrl"]?.TrimEnd('/') ?? "http://localhost:8080/api/v1";
        if (!BillplzPublicBase.TryResolveCallbackBase(_configuration, apiBaseUrl, out var callbackBase, out var baseError))
        {
            return new GatewayCheckoutResult(false, null, null, baseError);
        }

        metadata.TryGetValue("hub_payment_environment", out var configEnvironment);
        var isProd = BillplzPublicBase.IsProductionApi(_configuration, apiBaseUrl, configEnvironment);
        var endpoint = isProd ? ProductionApiUrl : SandboxApiUrl;
        // ...
        var webhookUrl = $"{callbackBase}/webhooks/payments/billplz/{tenantId}";
        webhookUrl = $"{webhookUrl}?type={Uri.EscapeDataString(typeValue)}&reference_1={Uri.EscapeDataString(ref1)}";
```

Then optional `&checkout_id=` for M2M because Billplz strips body metadata. `setupFutureUsage` is an **unused parameter**. Honest. Recurring Commerce still passed `true`; Billplz ignored it. New Pay must **not** pass a vault flag into a Billplz client, and must **not** show “save this card” copy on a Billplz hop-2.

`redirect_url` = success URL. Cancel URL is unused (Billplz has no cancel redirect on v3 create). Redirect and callback are **not ordered**. Browser `?paid=true` is UX. Money is the callback.

Minor units: `ToMinorUnitsTruncating` which on live **is away-from-zero**, despite the name.

Success: `url` + `id` (bill id). Persist bill id as `provider_session_id` / `PspRedirectUrl` analogue so a stripped callback can merge by bill id. New Pay already stores `PspRedirectUrl` on the checkout row; also store the bill id (Stripe stores session id on fulfill from the event, not at create). For Billplz, **create-time bill id is the SoT** because the webhook body is the only later signal and metadata is unreliable.

### 5.3 Webhook verify algorithm

Transport: form body, not JSON. `Content-Type: application/x-www-form-urlencoded`. A JSON body on the Billplz route is unusable → 400.

Hub copies query params into headers as `Query-{key}` (`Endpoints.cs:58–61`) so `checkout_id` / `type` / `reference_1` survive if Billplz dropped them from the form.

Algorithm, live:

1. `ParseFormBody` via `QueryHelpers.ParseQuery` (ASP.NET’s query parser on the raw body string). Case-insensitive dictionary.
2. Require form field `x_signature`. Missing → not verified, `"Missing x_signature in Billplz callback."`
3. HMAC-SHA256:
   - Exclude `x_signature` always (`AlwaysExclude`).
   - First try **including** extra fields `paid_at`, `transaction_id`, `transaction_status`.
   - Each remaining field → concatenation `key+value` (no `=`).
   - Sort those concatenations with `StringComparer.Ordinal`.
   - Join with `|`.
   - HMAC-SHA256 (UTF-8 key = webhook secret, UTF-8 data = source string).
   - Hex lower (`Convert.ToHexString(hash).ToLowerInvariant()`).
4. Compare to provided signature with `FixedTimeEqualsHex` (trim, lower, length-equal, `CryptographicOperations.FixedTimeEquals`).
5. If first compute fails, recompute **excluding** the extra fields. If both fail → `"Billplz x_signature verification failed."`
6. Bill id from `id`. Missing/whitespace → unusable (`AsUnusable()`), `"Missing stable Billplz bill id"`.
7. Paid iff `paid` is `"true"` (ignore-case) **or** `state` is `"paid"`. Else `PAYMENT_FAILED`.
8. Amount: `paid_amount` sen / 100. Currency: **`"MYR"` hardcoded**.
9. Metadata reconstruction: `reference_2` → `type` (else `Query-type`); `reference_1` → `subscription_id` unless `PlatformCheckoutTypes.IsPlatformCollected(type)` then `tenant_id`; `Query-checkout_id` else form `checkout_id`.
10. EventId = `PAYMENT_COMPLETED:{billId}` or `PAYMENT_FAILED:{billId}`.

**No timestamp on the HMAC.** Replay of the same body verifies forever. Dedup is `(org, provider, event_id)`. Replay of **paid** is a no-op after the first. Replay of **unpaid** after paid is a **different** EventId (`PAYMENT_FAILED:bill` vs `PAYMENT_COMPLETED:bill`). Hub now ignores that late fail if a COMPLETED business key exists. New Pay: if checkout is already `paid`, a later fail is `{ ignored: "already_paid" }`, not a journal reverse.

Fee formula `(paid * estimatedFeePercentage/100) + fixedFee` is dead: handler passes zeros. `BillplzFeeHonestyTests` locks the comment `estimatedFeePercentage - removed from config`. Do not revive a made-up MDR.

Test `GenerateCheckout_WithCheckoutId_AppendsQueryParam` **does not assert the query param**. It expects `Success == false` because there is no mock HTTP. 009 listed this as a lying test. Steal the **intent** (callback URL must carry `checkout_id`); write a real mock-HTTP test in new Pay.

### 5.4 Refund / off-session / portal

```278:290:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs
    /// <summary>
    /// Billplz has no bill-refund API. A Payment Order is a new disbursement, not a reversal.
    /// Commerce must mark-refunded instead of calling this adapter.
    /// </summary>
    public Task<bool> IssueRefundAsync(string apiKey, string transactionId, decimal amount)
    {
        return Task.FromResult(false);
    }

    public Task<string> GenerateCustomerPortalAsync(string apiKey, string customerEmail, string returnUrl)
    {
        throw new InvalidOperationException("Billplz does not provide a managed customer billing portal.");
    }
```

Off-session: log warning, return false. Capability short-circuit means Hub dunning should never call it; the adapter is belt-and-braces.

New Pay: **there is no Billplz charge method to port.** If this rail is ever added, `PublicPay` mints a bill, webhook marks paid, renew mints **another bill** and emails the URL. That is the whole product. AUTO_CHARGE on Billplz is a ship-stopper (`NP-GW-007`).

### 5.5 Secrets shape (Billplz)

| Field | What it is | Secret? |
|-------|------------|---------|
| `ApiKey` | Billplz secret key (Basic username) | **Yes.** |
| `MerchantId` | Collection ID | **No.** Required to create bills. |
| `WebhookSecret` | X-Signature key, **128 hex chars** (ops hint in Hub) | **Yes.** Used as HMAC key. **Not** the API key. |
| `Environment` | `test` / `live` | **Yes, behavioral.** Selects sandbox vs www host. Must **not** follow Pay’s hostname. |

Three values. New Pay’s single `secret` field cannot hold them. Same credential-row extension as CHIP, different columns.

Callback URL is **not** a secret Ada pastes into Pay. Hub stamps it per bill. Prefer that (staging/prod/local do not share one collection-level callback). Optionally print the URL for Ada to set as collection default; per-bill still wins.

### 5.6 `BillplzPublicBase` — steal the fail-closed, not the Hub hostname folklore

```14:84:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzPublicBase.cs
    public static bool IsProductionApi(...) { /* App:BillplzEnvironment, then configEnvironment live/test; default false */ }

    public static bool TryResolveCallbackBase(...)
    {
        // empty → CALLBACK_BASE_NOT_PUBLIC
        // not absolute http(s) → CALLBACK_BASE_NOT_PUBLIC
        // loopback / localhost / 127.0.0.1 / ::1 / host contains "lazuar-local-dev.com" / non-https
        //   → CALLBACK_BASE_NOT_PUBLIC unless App:AllowInsecureBillplzCallback
    }
```

Comment on the type: **do not** `Contains("lazuar.com")` — that would send `pay-local.lazuar.com` to production Billplz.

Tests: localhost rejected; `lazuar-local-dev.com` rejected; `https://pay-local.example.com/api/v1` accepted; Hub hostname does not force live; insecure flag allows localhost.

New Pay equivalent: `Pay:PublicBaseUrl` (or whatever 013 named). Billplz generate **fails** if that origin is not public HTTPS, unless a **dev-only** hatch is on. The hatch must be off in staging/prod. ngrok in production is an incident.

**Does new Pay need this even if CHIP is the Malaysian rail?** CHIP also cannot POST to `http://127.0.0.1:8081`. The **strict Billplz helper** is the best teacher; apply the same public-HTTPS check to **any** rail whose PSP calls you, including CHIP registrar. Do not keep the name `BillplzPublicBase` if CHIP is the rail — steal the predicate as `PayPublicBase` / `PspCallbackBase`.

---

## 6. `PublicDnsFallback` — does new Pay still need it?

193 lines of hand-rolled DNS: encode a UDP A query, speak to 1.1.1.1:53 then 8.8.8.8:53, decode answers, connect the first working A. Wired **only** to the named HttpClient `"Billplz"`. CHIP, Stripe, Razorpay, Xendit use default DNS.

013/06: “Park unless dogfood proves Hub DNS is still a problem.”

**Recommendation.**

- If the next rail is **CHIP**: **do not port** `PublicDnsFallback`. `gate.chip-in.asia` was never on this hook. Adding a 193-line DNS client “because Hub had one” is cathedral. If CHIP DNS actually fails on the dogfood LAN, fix the machine resolver or add the hook **then**, with a test against `gate.chip-in.asia`, not Billplz’s hostname.
- If the next rail is **Billplz**: **maybe.** The comment names `www.billplz-sandbox.com` on some LANs. That is a real class of Malaysian-office failure. Port only after one failed generate with default DNS on the dogfood laptop, or if CI in MY reproduces it. Prefer `HttpClient` + default DNS first; keep the 193 lines in Hub as a known extract.
- New Pay on 8081 today has **no** `SocketsHttpHandler.ConnectCallback`. Stripe.net uses its own client. There is no named-client DI to hang a fallback on until you add `AddHttpClient("Billplz")`.

Do not copy the UDP encoder “for completeness.”

---

## 7. Shared `GatewayCommon` bits they actually share

Adapters call these **statically**. No abstract base, no shared HTTP. New Pay should copy the **functions it needs** next to the one rail, not a `GatewayCommon` type that pretends five rails exist.

Used by **both** CHIP and Billplz generate:

- `TryResolveEmail` / `IsUsableBuyerEmail` / `PlaceholderEmail`
- `ExtractName` (local-part; override with payer name in new Pay)
- `ProductDescription`
- `ToMinorUnits` / `ToMinorUnitsRounded` / `ToMinorUnitsTruncating` (live: same away-from-zero)

CHIP-only:

- `ApplyPayingTenantMetadata`
- `TryNormalizeCurrency` (webhook fail-closed)
- `StampGatewayFeeStatus` / `GatewayFeeStatusKey`
- `FormatRefundIdempotencyKey` (`lazuar-refund:{txn}:{minor}`)

Billplz-only: none of the CHIP-only helpers. Billplz invents MYR, does not stamp fee status, does not refund.

Zero-decimal ISO table is unused for MYR. Keep it out of the first Malaysian rail.

---

## 8. New Pay current money path (the thing a Malaysian rail must join)

This is the **host** the HTTP extract lands in, not Hub.

**Paste keys.** `PUT /v1/orgs/{orgId}/gateway` — writer gate, Stripe-only, one secret, AES-GCM `SecretBox`, `last4`, response `capability = "hosted_link"`.

**Start pay.** `POST /v1/pay/{token}/start` — optional name/email on the checkout row, `StripeHosted.CreateHostedUrlAsync`, persist `PspRedirectUrl`.

**Webhook.** Raw body, empty 400, rail-not-configured 400, missing `Pay:StripeWebhookSecret` 503, invalid signature 400, unique `(orgId, provider, eventId)`, `checkout.session.completed` with `mode=setup` or amount 0 → `{ ignored: "setup_or_zero" }`, else `Fulfillment.FulfillPaidAsync` in the **same request**.

**Fulfill.** One DB transaction: refuse amount ≤ 0; refuse non-`open`; SST fail-closed if `SstRegistered` is null; `paid` + charge + optional payer + optional subscription (`mo`/`yr`) + balanced journal cash/revenue + `RCPT-{MYT year}-#####` titled Official Receipt + audit `checkout.paid`.

A Malaysian rail that does not call `FulfillPaidAsync` from the **same webhook handler** after verify+dedup is cloning Hub’s outbox-to-self. IsolationTests exist to stop that.

Same-handler fulfillment is **already** the Bar B law. CHIP/Billplz port is: verify that rail’s signature, map **paid money** to `FulfillPaidAsync(checkoutId, "chip"|"billplz", purchaseOrBillId)`, ignore vault/setup/zero.

---

## 9. Recommendation: CHIP Collect as the next Malaysian rail (not Billplz, not both)

**Pick: CHIP Collect.** One adapter. After Stripe dogfood is boring. Not Billplz on the same day. Not Razorpay/Xendit. Not a factory.

### 9.1 Why CHIP, in this repo, on this SHA

1. **`NP-GW-003` is still open and Stripe cannot tick it.** Bar B locked first rail = Stripe (`plans/013-prods/checklists/decisions.md`: “CHIP is the next Malaysian rail, not this Bar B. Billplz is reminder-only — not first.”). 011/01 dogfood sentence names **“pastes CHIP or Stripe keys”** — Billplz is not in that sentence. Checklist `NP-GW-003`: “One Malaysian rail you will dogfood (CHIP **or** Billplz). Not five adapters on day one.” Stripe is `NP-GW-002`. You cannot mark 003 done with Stripe cards, and you cannot mark 002 done because CHIP showed a Visa form.

2. **CHIP is the only Malaysian rail that can vault.** Wrap-rails honesty (`NP-GW-007`) has two ends. Stripe already occupies “auto-charge if vaulted.” Billplz occupies “reminder + hosted link.” Shipping Billplz next **duplicates the reminder-only story** without teaching Pay how to store a `recurring_token` or how to refuse FPX-as-mandate. CHIP is the rail that forces the honest split: hosted FPX/QR/wallets on **their** page, silent debit **only** with a card token, e-mandate still false.

3. **HTTP quality.** JSON metadata, RSA body signature, fee node when present, refund API, off-session charge with reference lookup, fail-closed currency and ids. Billplz is form HMAC, query metadata, fee always 0, no refund, no vault. For a second rail that has to share `Fulfillment` and `psp_webhook_events` with Stripe, CHIP’s envelope is closer to “JSON + header signature + object id” than Billplz’s form dual-HMAC.

4. **Hub already poked CHIP harder.** 008/009 spent more pages on `$0` `skip_capture` than on Billplz generate. That is evidence of soak intent, not completeness. The live parse now maps preauthorized+token. The live off-session path now has idempotency-key + reference reuse + `paid`-only success. Those extracts are ready. Billplz’s remaining unsolved product problem (v3 frozen, Agreements not implemented, public callback, DNS) does not get easier by being first.

5. **Aura-shaped “I already have Billplz” is a later BYOK**, not the dogfood rail for the Pay team. 007/05 is explicit: Billplz **wins informal send-a-link**; CHIP is the K2 for merchants who need cards + vault + refunds + DuitNow QR + no annual fee. New Pay’s first Malaysian merchant is **us**. We want a token story that is not Stripe’s SetupIntent so the wrap-rails matrix is real in two processors.

6. **Registrar exists as HTTP**, so Ada need not paste a PEM if — and only if — she clicks register on a public HTTPS origin. Billplz makes her paste a 128-char X-Signature **and** a Collection ID **and** survive `CALLBACK_BASE_NOT_PUBLIC` before the first bill. CHIP is fewer moving parts **if** we refuse surprise-register and still require Brand ID.

### 9.2 Why not Billplz as the next rail

- Reminder-only. Cannot tick a vault path. `NP-GW-007` copy is already true of “we only have Stripe hosted_link.” Billplz would not add a new capability class.
- Three secrets + public HTTPS + sandbox host split + optional DNS fallback. More ops surface for a **weaker** money API.
- v3 frozen. Agreements v5 is the company-level vault and we have **refused** to homemade it. Picking Billplz next is picking a rail whose interesting future we will not implement.
- 007 called it “primary rail. Keep. Deepen.” That was Hub/Aura System B product judgment in August 2026. New Pay’s product dogfood sentence and 013 Bar B lock already moved on. Honour 011/013 over 007’s “primary.”
- `CheckoutSessionCashier` last-resort `"BILLPLZ"` is a Hub foot-gun 013 forbade copying. Do not default new Pay to Billplz because Hub did.

### 9.3 Why not both on day one

`NP-GW-003` is the sentence. Five-adapter factory is the Hub lie. Two Malaysian adapters plus Stripe is three; the third is how Razorpay “while we are here” starts. IsolationTests will not save you from a `PaymentGatewayFactory` you write yourself.

Billplz can come **after** CHIP hosted pay is boring: same `FulfillPaidAsync`, different verify, `capability = hosted_link` **forever**, no `ChargeOffSession` method. That is a later slice (`NP-LAT` / Bar C+), not the next commit after this paper.

### 9.4 Risks of picking CHIP (do not skip)

- **`NP-GW-008`.** Hub maps `purchase.preauthorized` + token as `PAYMENT_COMPLETED` with `AmountPaid` possibly 0. New Pay fulfill already no-ops `amount <= 0`. **Keep that.** Map CHIP preauthorized to a **vault** branch, not `FulfillPaidAsync`. If you copy Hub’s EventType string blindly and then later allow amount-0 fulfill, you mint `RCPT-` for a card save. Fail the slice if that happens.
- **Surprise-register.** Hub still does it. New Pay must not. See §4.6.
- **PEM mismatch.** Company `GET /public_key/` vs `Webhook.public_key`. Prefer webhook object. A wrong PEM is 400 invalid signature forever until rotated. Tests with a generated RSA pair (Hub already does this in `ChipCollectGatewayAdapterTests.ParseSignedAsync`) must land in `Lazuar.Pay.Tests`.
- **Test vs live is a dashboard toggle.** A live brand key on a dogfood org charges real FPX. Copy must scream. Do not infer from Pay hostname.
- **No method filter.** The hosted page may show BNPL / stablecoins if the brand has them. Wrap-rails: we wrap whatever CHIP shows; we do not advertise Atome as a Lazuar product.
- **Off-session is not Stripe-class.** Reference lookup + `Idempotency-Key` header is best-effort. Do not implement AUTO_CHARGE in the same slice as first hosted `RCPT-`.
- **Ticking `NP-GW-002` because CHIP showed cards.** Forbidden.

### 9.5 What CHIP day-one is **not**

Not off-session renew. Not refund HTTP. Not CHIP Send. Not payment-method whitelist. Not a five-name dropdown on `:5178`. Not Hub `IPaymentGatewayAdapter`. Not MediatR `ProcessGatewayWebhookCommand`. Not `lazuar-local-dev.com`. Not treating FPX completion as a vault.

Day-one CHIP: paste Brand ID + secret (encrypted), optional explicit “register webhook,” `POST /purchases/` for an **open non-zero** checkout, redirect to `checkout_url`, RSA-verify `purchase.paid`, same-handler `FulfillPaidAsync`, replay no-op, wrap copy that auto-debit is **not** on until a token exists.

---

## 10. Porting sketch (new files under `Lazuar.Pay/Gateways/`, not a factory of five)

Steal HTTP extract. Do not copy the module.

### 10.1 Files to add (CHIP next)

| New Pay path | Steal from | Do not steal |
|--------------|------------|--------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/ChipHosted.cs` | `GenerateCheckoutAsync` HTTP: Bearer, `brand_id`, products sen, redirects, metadata `checkout_id`/`org_id`. Return `checkout_url`, persist purchase id on the checkout row (new column `ProviderRef` at create, or reuse a field — Stripe only learns session id at webhook). | `IPaymentGatewayAdapter`. `setupFutureUsage` / `skip_capture` on day one (first charge is non-zero hosted). `ExtractName` as the only name. Factory. |
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/ChipWebhook.cs` (or methods on `ChipHosted`) | RSA verify (`ImportFromPem` + PKCS#1 SHA256 of raw bytes). `ReadStablePurchaseId`. Event map **with** `purchase.paid` → fulfill; `preauthorized`+token → vault-not-paid; missing id/currency → 400 unusable; other events → 200 ignore. Event id `{kind}:{purchaseId}`. | Mapping preauthorized to paid money. Guid fallback. Hub `GatewayWebhookParsedResult` record as a shared type. Outbox publish. |
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/ChipRegistrar.cs` | `TryFindExistingAsync`, `ExtractPublicKey`, `NormalizePem`, list-before-create POST. | Call from `PUT /gateway`. Localhost rewrite. Failure blocking key save. Title `"Lazuar Platform Webhook"`. |
| Extend `WebhookEndpoints.cs` | Branch `provider == "chip"`: raw body, empty 400, rail configured, PEM from **org row** (not `Pay:StripeWebhookSecret`), `X-Signature`, unique `(org, chip, eventId)`, then fulfill or ignore. | MediatR command. `IPaymentGatewayFactory.GetAdapter`. Query-string `Query-*` map (CHIP is JSON). |
| Extend `GatewayEndpoints.cs` | Allow `provider == "chip"` **in addition to** stripe (Stripe stays). Body must grow: `secret`, `brand_id` (required), optional `webhook_pem`. Capability: `hosted_link` until a token is stored. | Five-name switch. Auto-registrar. `capability = vaulted_autocharge` on paste. |
| Extend `PublicPayEndpoints.cs` | If org’s configured provider is `chip`, `ChipHosted.CreateHostedUrlAsync`. Stripe path unchanged. Missing CHIP brand → 503 `rail not configured` analogue. | `CheckoutSessionCashier` last-resort Billplz. |
| Extend `GatewayCredentialRow` / migration | Ciphertext for Bearer; **new** `MerchantId`/`BrandId` plaintext; **new** `WebhookPemCiphertext` (or JSON envelope). `Last4` of the secret only. | Hub `TenantPaymentConfiguration` aggregate, `IMustHaveTenant`, `DecryptOrPlaintext`. |
| `Program.cs` | `AddHttpClient` **unnamed or `"Chip"`** with a 15s timeout. `AddScoped<ChipHosted>()`. | Named `"Billplz"` client + `ConnectCallback`. `AddScoped<IPaymentGatewayAdapter, …>` ×5. |
| Merchant `:5178` | Second paste form **or** provider toggle **Stripe \| CHIP**, not five tabs. CHIP fields: secret, Brand ID, “Register webhook” button (disabled if public base is not HTTPS). Amber copy from §3. | Ops `PaymentSettingsPage` five-gateway tab. `VITE_CHIP_*`. |
| Checkout `:5179` | No CHIP keys. Optional wrap sentence from public GET (`rail`, `is_reminder_only`) when the DTO grows. Today’s GET has amount/currency/status/payer only — **NP-GW-007 copy cannot show yet.** Add `rail` / `capability` on public GET when CHIP lands, or the page will say nothing and a future AUTO_CHARGE will lie. | “FPX e-mandate enrolled.” “We will charge your card automatically” before a token. |

**Explicitly out of the first CHIP commit:** `ChargeOffSessionAsync`, `IssueRefundAsync`, `GenerateCustomerPortalAsync`, `PublicDnsFallback`, `BillplzGatewayAdapter`, Razorpay, Xendit, `PaymentGatewayFactory`, MediatR, outbox, `ProcessGatewayWebhookCommandHandler`, `IntegrationCheckoutSession` merge (CHIP has JSON metadata; persist `checkout_id` on the purchase and on the Pay row).

### 10.2 If someone overrides this paper and ships Billplz instead

Then the files are `BillplzHosted.cs`, HMAC dual-compute, `BillplzPublicBase` predicate (rename), form body + `Query-*` headers, Basic auth, collection id + API key + 128-char X-Signature, sandbox host from **org environment not Pay hostname**, `capability = hosted_link` **permanently**, **no** charge method, mark-refunded later. Still not a factory of five. Still not CHIP in the same PR.

DNS fallback: default DNS first.

### 10.3 Webhook route contract (both rails, for when the second exists)

Keep `POST /v1/webhooks/{provider}/{orgId}`. Unknown provider stays 400 (`unknown provider`). Empty body 400. Unique `(org_id, provider, event_id)`. Same `Fulfillment` type. Provider discriminant on `psp_webhook_events` is why Stripe `evt_…` and CHIP `PAYMENT_COMPLETED:purch_…` and One delivery ids must **never** share a table without `provider` / `source`. New Pay already split One into `one_webhook_events`. Keep it that way.

CHIP event_id: `paid:{purchaseId}` **or** keep Hub’s `PAYMENT_COMPLETED:{purchaseId}`. Either is fine **if unique per type**. Do not use Hub’s `PAYMENT_COMPLETED` string as a reason to call fulfill on preauthorized. Prefer Pay-native kinds: `paid`, `failed`, `vaulted`, `refunded`.

Billplz event_id: `paid:{billId}` / `failed:{billId}`.

Never invent a Guid.

### 10.4 Credential PUT shape (CHIP)

Proposed (not implemented here):

```json
{
  "provider": "chip",
  "secret": "<CHIP secret key>",
  "brand_id": "<Brand ID>",
  "webhook_pem": "-----BEGIN PUBLIC KEY-----\n…\n-----END PUBLIC KEY-----"
}
```

`webhook_pem` optional if Ada will click register next. `secret` required. `brand_id` required. Member cannot PUT (existing `RequireWriterAsync`). GET returns `{ provider, last4, brand_id, configured, capability, webhook_registered }` — never plaintext secret, never PEM.

Register action (separate, explicit):

`POST /v1/orgs/{orgId}/gateway/chip/webhook` → `ChipRegistrar.EnsureRegisteredAsync` against `Pay:PublicBaseUrl` + `/v1/webhooks/chip/{orgId}`. Fail if public base is not HTTPS / is loopback. Store returned PEM. Do not run this from PUT.

### 10.5 Public start

`ChipHosted.CreateHostedUrlAsync(CheckoutRow)`:

- Load credential for `(orgId, "chip")`.
- Unprotect secret; read brand id.
- Refuse placeholder/missing email (session `PayerEmail`).
- `POST purchases/` with amount away-from-zero sen, name from `PayerName` else local-part, metadata `checkout_id`, `org_id`.
- **No** `force_recurring` until Pay has a recurring product **and** intends to vault **and** wrap copy is on the 5179 page.
- Persist purchase id + `checkout_url`.

Success URL: existing 5179 `?status=verifying` pattern. CHIP `success_redirect` is UX. Money is `purchase.paid`.

### 10.6 Wrap-rails label at the seam

Stripe Bar B: `capability = "hosted_link"` (honest: no PM stored).  
CHIP day-one: **same** `hosted_link`.  
CHIP after a `recurring_token` is persisted: `vaulted_autocharge` **for that subscription**, not for the org’s rail in the abstract. An org can have CHIP and still have one-off checkouts with no token.

Billplz-class, if it ever lands: `hosted_link` and a boolean `supports_off_session: false` that **cannot** become true by webhook accident.

`SupportsEmandate`: do not add a column. False is a law, not a row.

---

## 11. Tests to steal as **judgment** (not as a project reference)

New Pay tests live in `apps/lazuar-pay/tests/Lazuar.Pay.Tests`. IsolationTests forbid Hub modules. **Re-write** the cases. Do not add `Lazuar.ModuleTests` to the Pay solution.

### 11.1 CHIP tests that are still true and worth rewriting

| Hub test | Judgment to keep |
|----------|------------------|
| `ParseWebhook_MissingSignature_IsNotVerified` | No `X-Signature` → 400, no fulfill. |
| `ParseWebhook_BadSignature_IsNotVerified` | Random 64-byte base64 → 400. |
| `ParseWebhook_PurchasePaid_UsesRootId` | Event id namespaced; not a Guid; fee unknown if no `payment` node. |
| `ParseWebhook_PurchasePaid_WithPaymentFee_IsKnownFee` | `fee_amount` 150 → 1.50; stamp known. Do not journal fee in day-one if Pay’s journal is still cash=gross (Bar B journal is cash/revenue only — **do not silently add EXPENSE_GATEWAY_FEE** without paper 07). Unknown ≠ 0. |
| `ParseWebhook_PurchasePaid_PrefersNestedPurchaseId` | Nested `purchase.id` wins. |
| `ParseWebhook_PurchasePaid_MissingCurrency_IsNotVerified` | No invented MYR. 400 unusable. |
| `ParseWebhook_PurchasePaid_NoIds_IsNotVerified` | Empty event id. Never mint a Guid and 200. |
| `ParseWebhook_PreauthorizedAuthHold_IsNotPaymentCompleted` | Auth-hold ≠ paid. **New Pay: fulfill not called.** |
| `ParseWebhook_PreauthorizedRecurringToken_IsPaymentCompletedWithVault` | Token extracted. **New Pay must change the assertion:** vaulted, **fulfill not called**, no `RCPT-`. This is the NP-GW-008 lock. |
| `ParseWebhook_PaymentFailure_UsesStablePurchaseId` | Namespaced fail id. If already paid, ignore. |
| `ParseWebhook_PaymentRefunded_IsRefundCompleted` | Mapped in Hub. New Pay day-one: 200 ignore, **not** fulfill, **not** reverse. Add a test so a refund webhook cannot mark a checkout paid. |
| `IsOffSessionPaid_OnlyPaidIsTrue` | If ChargeOffSession is later: `pending_charge` is not success. |
| `ExtractVaultIds_*` | Token from `recurring_token` or `is_recurring_token`; customer fallback to token. |
| `ChargeOffSession_TokenGet404_FallsBackToClient` | Later slice. |
| `ChargeOffSession_SendsProcessorIdempotencyKeyOnCreateAndCharge` | Later slice. Steal `reference` + header. |
| `ChargeOffSession_ReusesExistingPurchaseForSameIdempotencyKey` | Later slice. Paid existing → no second `/charge/`. |
| `GenerateCheckout_KeepsPayingTenant_AndStampsPlatformTenant` | New Pay: metadata contains `org_id` + `checkout_id`. No system org to stamp. |
| `IssueRefundAsync_PostsMinorUnitsToPurchaseRefund` | Later slice. |
| `EnsureRegistered_ExistingCallback_DoesNotPostAgain` | Explicit register action: GET then no POST. |
| `EnsureRegistered_MissingCallback_PostsOnce_UsesWebhookPublicKey` | Prefers webhook PEM. |
| `ExtractPublicKey_ReadsWebhookObject` | `\\n` normalize. |

**Missing Hub tests to write first in Pay:**

- Generate **non-zero** purchase JSON contains `brand_id`, sen amount, `checkout_id` metadata, **no** `skip_capture`.
- Generate `$0` + vault (if you ever send it) contains `force_recurring` + `skip_capture` — and webhook of preauthorized **does not** fulfill.
- `PUT /v1/orgs/{id}/gateway` with `provider: "chip"` succeeds after CHIP is allowed; `provider: "billplz"` still 400 on CHIP-only day; `provider: "xendit"` 400.
- `POST /v1/webhooks/chip/{org}` empty body 400; bad RSA 400; duplicate event_id 200 `{ duplicate: true }` and one `RCPT-`; tenant A’s purchase id does not collide with tenant B (shared brand / same PEM is the reason the PK includes `org_id`).
- Fulfill from `purchase.paid` with `purchase.metadata.checkout_id` (or stored provider ref) writes receipt; amount 0 does not.
- Registrar is **not** invoked from PUT (assert HttpClient was not called, or a spy). Registrar **is** invoked from the explicit POST, and **refuses** loopback public base.

### 11.2 Billplz tests (park; rewrite when/if Billplz is the rail)

| Hub test | Judgment |
|----------|----------|
| `ParseWebhook_QueryCheckoutId_IncludedInMetadata` | `checkout_id` on query survives form-stripped body. Persist bill id anyway. |
| `ParseWebhook_PlatformSaasFee_MapsReference1ToTenantId` | Hub platform-checkout folklore. New Pay has no platform SaaS fee on Billplz. Do not port `PlatformCheckoutTypes`. |
| `ParseWebhook_BadSignature_IsNotVerified` | Dual-HMAC still needs the secret. |
| `ParseWebhook_MissingId` / `EmptyId` | Fail-closed. |
| `ParseWebhook_Unpaid_IsPaymentFailed_WithBillId` | Create-time `due` is failed:{id}. Must **not** block a later paid:{id}. If already paid, ignore fail. |
| `IssueRefundAsync_AlwaysReturnsFalse` | Keep if the type exists. Better: no method. |
| `ChargeOffSessionAsync_DoesNotThrow_ReturnsFalse` | Keep if the type exists. Better: no method. |
| `GenerateCheckout_WithCheckoutId_AppendsQueryParam` | **Lies today.** Rewrite with mock HTTP and assert `callback_url` query. |
| `BillplzPublicBaseTests.*` | Steal the predicate tests for `Pay:PublicBaseUrl`. |
| `PublicDnsFallbackTests.*` | Park with the 193-line client. |
| `BillplzFeeHonestyTests` | Do not pass estimated MDR into parse. |
| `PaymentGatewayCapabilitiesTests` Billplz cases | Off-session false, mark-refunded true, GrabPay hosted-wallet false. |

### 11.3 New Pay tests that already exist and must keep passing

`WebhookTests`: missing platform Stripe webhook secret 503; invalid signature 400; completed session writes `RCPT-` and replay is duplicate. `IsolationTests`: no MediatR, no Hub modules, no `apps/lazuar-api` csproj reference. A CHIP port that adds `using Modules.Payments` fails IsolationTests on purpose.

Public start still 503s on Stripe exception (`PublicPayEndpoints`). CHIP HTTP errors should 503 `CHIP rejected the org key` analogue, not 500.

### 11.4 Capabilities helper

Do **not** add a `Lazuar.Pay.Contracts` project. Ten lines next to charge:

```csharp
static bool SupportsOffSession(string provider) =>
    provider is "stripe" or "chip"; // only after a real token exists at the call site
```

Call site must **also** see a stored token. Name-only true is how Hub AUTO_CHARGE almost Billplz’d people who stuffed junk vault ids (`BillingEngineJobTests` Billplz-or-reminder-only path). Billplz with junk vault still must not charge.

---

## 12. Wrap-rails honesty (the product sentence this slice exists to protect)

From 011/01:

> Honest matrix: Stripe/CHIP can auto-charge if vaulted; Billplz/Xendit/Razorpay-class = **reminder + hosted link**, never silent debit.  
> No homemade FPX e-mandate.

From Hub ops amber (steal wording, replace “Hub” with “Pay”):

> **Pay-link renewals.** Billplz cannot vault. Each cycle we create a hosted bill and email it. There is no silent auto-charge (subscription renewals, dunning AUTO_CHARGE). Use Stripe or CHIP Collect when you need recurring auto-debit.

CHIP merchant copy to steal (013/06):

> CHIP Collect hosted page shows whatever you enabled on the brand (FPX, cards, DuitNow QR, wallets). Lazuar does not rebuild those rails. Auto-debit is **card token only**. We will not silent-debit FPX. CHIP does not run your subscription clock — Pay does, and only with a stored token.

`:5178` today says “Paste test `sk_test_`.” When CHIP lands, that heading must not remain “Stripe keys” while the PUT accepts `chip`. Two honest blocks beat one dropdown of five logos.

`:5179` must not print auto-debit on a CHIP checkout that did not send `force_recurring` and has no token. Success page is not paid (paper 05). Browser CHIP redirect is UX.

**Never:**

- Homemade FPX e-mandate / Billplz Agreements v5 as a quiet extra.
- Count `purchase.preauthorized` as cash.
- AUTO_CHARGE Billplz (or CHIP without a token).
- Tick `NP-GW-002` because CHIP rendered a card form.
- Register five adapters.
- Surprise-POST `/webhooks/` into Ada’s CHIP account on key paste.
- Rewrite localhost to fiction DNS.
- Fall back to Billplz when CHIP keys are missing.
- Decrypt-or-plaintext a wrap miss so ciphertext goes to CHIP as Bearer (`SecretBox.Unprotect` must throw; Hub `DecryptOrPlaintext` is the anti-pattern).

---

## 13. `NP-GW-*` reading for this slice (do not flip cells)

| ID | Live new Pay | After a CHIP port (proposed, not done) |
|----|--------------|----------------------------------------|
| NP-GW-001 encrypted BYOK | Stripe secret in `gateway_credentials` via `SecretBox` | Same table, CHIP secret + brand id + PEM |
| NP-GW-002 Stripe | Bar B dogfood. `StripeHosted` + webhook | **Unchanged.** CHIP cards do not tick this. |
| NP-GW-003 one MY rail | **todo.** 400 `"Bar B first rail is stripe"` | CHIP hosted purchase + RSA webhook. **Not** Billplz. **Not** both. |
| NP-GW-004 verify | Stripe-Signature | + CHIP `X-Signature` RSA |
| NP-GW-005 empty 400 | Yes | Same route, both providers |
| NP-GW-006 idempotent `(org, provider, event_id)` | `psp_webhook_events` PK | CHIP namespaced event id in the same table |
| NP-GW-007 honest matrix | Stripe GET returns `hosted_link` | CHIP `hosted_link` until token; never silent debit; e-mandate false |
| NP-GW-008 setup ≠ paid | Stripe setup/zero ignored; fulfill amount≤0 returns | CHIP preauthorized+token **vaulted**; `purchase.paid` amount>0 paid |
| NP-GW-009 paste/rotate | Writer-only PUT Stripe | Writer-only PUT CHIP fields; member 403 |

013 G10 lock already wrote the name **Stripe** for Bar B and **CHIP** as next. This paper agrees with that lock, with evidence from live adapters, and with 007 only as market colour.

---

## 14. Hub webhook event / header / skip_capture search (this SHA)

Grep across Hub Payments for the assigned tokens:

| Token | Live meaning |
|-------|----------------|
| `purchase.paid` | CHIP money. Mapped `PAYMENT_COMPLETED`. Registrar subscribes. |
| `purchase.preauthorized` | Registrar subscribes. Mapped `PAYMENT_COMPLETED` **only if** `ExtractVaultIds` found a token. Else passthrough. `$0` `skip_capture` path. |
| `purchase.payment_failure` | Mapped `PAYMENT_FAILED`. Namespaced EventId so fail-then-pay can both log. |
| `payment.refunded` | Registrar subscribes. Mapped `REFUND_COMPLETED` (007 stale). |
| `skip_capture` | Generate flag when `setupFutureUsage && amountInCents == 0`. Never `/capture/`. |
| `force_recurring` | Generate flag when `setupFutureUsage`. Card vault request. |
| `collection` | Billplz `collection_id` = `merchantId`. Not a CHIP concept (CHIP is `brand_id`). |
| `bill` | Billplz `POST …/bills`, form field `id` = bill id. |
| `x-signature` / `X-Signature` / `x_signature` | CHIP: **header** `X-Signature` (base64 RSA). Billplz: **body field** `x_signature` (hex HMAC). Different algorithms, same English words. Do not share a verifier. |
| `event_type` | CHIP JSON field. Billplz has **no** event type; paid boolean / state. |

Hub allow-list still includes all five names (`Endpoints.cs` `STRIPE`, `BILLPLZ`, `RAZORPAY`, `CHIP`, `XENDIT`). New Pay allow-list is `stripe` today. Widening to `stripe` + `chip` is the whole `NP-GW-003` move.

---

## 15. Verdict

New Pay on `ee2db8e5` is a Stripe-only hosted cashier. Grep finds **zero** CHIP/Billplz symbols under `apps/lazuar-pay`. `PUT /v1/orgs/{orgId}/gateway` returns **400 `"Bar B first rail is stripe"`** for any other provider. That 400 is Bar B law, not Malaysian-rail law.

Hub still has two real Malaysian adapters. Live CHIP is a JSON/RSA/vault/refund rail with a surprise registrar and an `NP-GW-008` foot-gun if you copy its `PAYMENT_COMPLETED` mapping for preauthorized. Live Billplz is a form/HMAC/reminder rail with public-HTTPS fail-closed, a DNS fallback nobody else uses, and no vault.

008/009 CHIP EventId collision and `$0` skip_capture “never vaults” are **not** live as written. EventId is namespaced. Preauthorized+token extracts a vault. Empty body is 400. Unique webhook log is tenant-scoped. Off-session is `paid`-only and has a reference lookup. 007’s table is riddled with those stale cells. **Live files are authority.**

**Next rail: CHIP Collect.** After Stripe dogfood, not beside it on day one. Steal purchases HTTP, RSA verify, namespaced event ids, list-before-create registrar HTTP, public-HTTPS predicate (from Billplz, applied to CHIP register). Refuse surprise-register, fiction DNS, factory of five, MediatR/outbox, `PublicDnsFallback` until proven, Billplz Agreements, FPX e-mandate, and booking `purchase.preauthorized` as cash.

Billplz remains the honest reminder-only wrap for merchants who already have a collection. It is not the Pay team’s next dogfood. `NP-GW-003` is one name: **CHIP**.
)
