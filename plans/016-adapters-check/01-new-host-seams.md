# 01 — New Pay host gateway seams (`apps/lazuar-pay`)

**Date:** 24 August 2026  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Branch (`.git/HEAD` → `refs/heads/feat/015-four-adapters`):** `feat/015-four-adapters`  
**HEAD (`git rev-parse` equivalent from `.git/refs/heads/feat/015-four-adapters`):** `c621ceba7fc7b79f16954d0819200cb21db6f22b`  
**`git log -1 --oneline` equivalent (`.git/logs/HEAD` last commit):** `c621ceba docs(015): check off implemented T–Q phases`  
**Type:** Uncondensed evaluation of **live** host seams. **Not** an implementation. **Not** Vite. **Not** per-PSP HTTP extract vs Hub. Live code is authority over 015 checklists.

Parent index: [README.md](./README.md). 015 law: [../015-four-adapters/00-what-must-be-done.md](../015-four-adapters/00-what-must-be-done.md) §3.

---

## 0. Method

This file answers: **how do PUT/GET keys, start dispatch, the webhook pipeline, secrets, fulfillment, and IsolationTests actually work on the new 8081 host right now?**

How the evidence was gathered:

1. Recorded branch and SHA from `.git/HEAD`, `.git/refs/heads/feat/015-four-adapters`, and `.git/logs/HEAD` (same commit the 016 index names: `c621ceba`).
2. Opened every file listed as mandatory for this slice, plus the wiring those files call (`PayDbContext`, migration `FourAdaptersHostedRails`, `CheckoutEndpoints`, `MemberGate`, `OneWebhookEndpoints`, `MoneyMath`, the five `*Hosted` / `*Webhook` types as **existence and host-seam use**, hermetic tests that lock the seams).
3. Grepped `apps/lazuar-pay` for `SstRegistered`, `IPaymentGatewayAdapter`, `PaymentGatewayFactory`, `MediatR`, `FulfillPaidAsync`, `hosted_link`, `BeginTransaction`, wrap-key defaults, registrar / DNS fallback strings.
4. Treated [015 §3](../015-four-adapters/00-what-must-be-done.md) as **standing law**, not as proof that the cells in `plans/015-four-adapters/checklists/` are true. Several of those cells are checked in docs; this paper re-reads the C#.
5. Did **not** judge Stripe/CHIP/Billplz/Xendit/Razorpay HTTP against Hub adapters (other 016 files). Those classes are named where the host **dispatches into** them.

Scope boundary:

| In this file | Out |
|--------------|-----|
| `apps/lazuar-pay` DI, routes, credential row, `active_provider`, start `switch`, webhook TX, `SecretBox`, `Fulfillment`, IsolationTests | `:5178` / `:5179` field sets, verifying poll UI |
| Capability string `hosted_link` as the host emits it | Hub `Modules/Payments/Infrastructure/Gateways/` HTTP judgment |
| Whether IsolationTests still ban Hub types | Inventory of every `WebhookTests`/`RailTests` case (file 09) |

016 index problem statement this slice serves:

```10:18:plans/016-adapters-check/README.md
**Problem:** 015 landed five hosted_link rails on 8081 plus merchant/checkout UIs. We must (1) verify the **new host** and how **`:5178` / `:5179`** actually call it, (2) cross-check each rail against Hub `Modules/Payments/Infrastructure/Gateways/` as **HTTP judgment**, (3) name which **tests exist vs must still be written**. Live code is authority. [015](../015-four-adapters/README.md) checklists are a map, not proof.

New stack:

| Path | Role |
|------|------|
| `apps/lazuar-pay` | Host **8081**. `Gateways/*`, `PUT/GET /v1/orgs/{orgId}/gateway`, `POST /v1/pay/{token}/start`, `POST /v1/webhooks/{provider}/{orgId}` |
```

Assigned slice from the same index: host seams — PUT/GET, `IHostedRail`, start dispatch, webhook TX, secrets.

---

## 1. Standing law (015 §3), quoted as law not proof

015 §1 (kept; this program does not reverse it):

```28:38:plans/015-four-adapters/00-what-must-be-done.md
| Keep | Meaning |
|------|---------|
| Steal HTTP judgment | Read Hub adapters. Do not copy `Modules/Payments`, MediatR, outbox, `PaymentsDbContext`. IsolationTests stay red on those strings. |
| Same-handler fulfillment | Verified PSP event → journal + `RCPT-` in-process. Rails do not book cash. |
| Wrap-rails honesty | CHIP *can* vault later; this slice still labels it `hosted_link`. Billplz / Xendit / Razorpay **never** silent-debit. `SupportsEmandate` remains false. |
| Setup ≠ paid | Stripe `mode=setup` / amount 0, CHIP `purchase.preauthorized`, unpaid Billplz, non-PAID Xendit, non-`payment.captured` Razorpay → **do not fulfill**. |
| BYOK | Merchant’s keys. Not Connect `application_fee`. Not Lazuar as MoR. |
| Buyers are not One humans | `:5179` stays public. |
| One active rail per org | Four adapters in the **code**. Not four logos on the buyer page. Merchant picks **one** provider. Hub was one `GatewayType` per tenant; keep that. |
| Receipt ≠ tax invoice | Stronger now: we are **not shipping tax at all**. |
```

015 §3 is **Must-do 0 — shared host (do this first)**. The four HTTP extracts are must-do 1–4. This slice is must-do 0 plus whether the host door still matches that law after the four classes landed.

### 1.1 §3.1 Strip tax from the money path

```70:87:plans/015-four-adapters/00-what-must-be-done.md
### 3.1 Strip tax from the money path

Live tax residue (all of it, new Pay only):

- `OrgSettingsRow.SstRegistered` (`bool?`) and the column in `org_settings`
- `Fulfillment`: throw `"SST registration unknown; fail closed"`
- `CheckoutEndpoints.Create`: seed `SstRegistered = false` (this was fail-**open** anyway)
- `OneWebhookEndpoints` on insert: `SstRegistered = false`

**Do:**

1. Remove the throw in `Fulfillment`. Book cash debit + revenue credit for `checkout.Amount`. No tax line. No fee line (`unknown ≠ 0`: do not invent 0).
2. Stop reading `SstRegistered` on the pay path. Leave the column in place (do not spend a migration on drop unless you want it). Stop seeding it as a business signal.
3. Do **not** add a merchant SST yes/no field. Do **not** port Hub `SstTaxMath`. Do **not** add LHDN types.
4. Keep title `"Official Receipt"`. Keep `PENDING` if number missing. Keep refuse: never print VALID, never title Tax Invoice.
5. Merchant + checkout copy: amount is GMV as charged; this is not an e-invoice; SST is the merchant’s problem with LHDN later.
```

### 1.2 §3.2 Credential row that can hold any of the four

```89:106:plans/015-four-adapters/00-what-must-be-done.md
### 3.2 Credential row that can hold any of the four

Steal Hub `TenantPaymentConfiguration` **fields**, not the type:

| Column | Encrypt? | Who uses it |
|--------|----------|-------------|
| `OrgId` + `Provider` PK | — | all (`stripe` \| `chip` \| `billplz` \| `xendit` \| `razorpay`) |
| `Ciphertext` (API key) | yes | Stripe `sk_`, CHIP Bearer, Billplz secret, Xendit secret, Razorpay `key_id:key_secret` |
| `Last4` | no | GET hint of API key |
| `WebhookCiphertext` | yes | Stripe `whsec_`, CHIP PEM, Billplz X-Signature secret, Xendit callback token, Razorpay webhook secret |
| `PublicMerchantId` | **no** (not a secret) | CHIP Brand ID, Billplz Collection ID. Null for Stripe/Xendit/Razorpay |
| `Environment` | no | Billplz `test`\|`live` (host selection). Others may ignore |
| `UpdatedAt` | no | |

`org_settings.active_provider` (or equivalent): **one** name the org charges with. PUT gateway sets it. Public start uses it. Buyer page does not offer a dropdown of four PSPs.

Move Stripe verify off `Pay:StripeWebhookSecret`. Process env may remain a **dev fallback** for Stripe only, and must 503 in Production if the org row has no `WebhookCiphertext`.
```

### 1.3 §3.3 One DB transaction on Plane B

```107:113:plans/015-four-adapters/00-what-must-be-done.md
### 3.3 One DB transaction on Plane B

`WebhookEndpoints` today: insert `psp_webhook_events` → `SaveChanges` → `FulfillPaidAsync` (own TX).

**Must:** verify → parse → insert unique → fulfill → **one** commit. Unique hit → 200 `{ duplicate: true }`. Fulfill throw → rollback event id → PSP retry is correct. Bind `checkout.OrgId == path orgId` before fulfill. Match amount when the PSP sent one (minor units) or refuse.

Unhandled event types must **not** consume the unique grain unless you store them as `ignored` and still no-op fulfill. Better: only insert when you will fulfill **or** when you have a stable event id you must never fulfill (setup/preauth) — and test both.
```

### 1.4 §3.4 Dispatch without a factory of five

```115:131:plans/015-four-adapters/00-what-must-be-done.md
### 3.4 Dispatch without a factory of five

Do **not** add `IPaymentGatewayAdapter` or `PaymentGatewayFactory`.

Do add a **small** hosted-rail shape (when the second class exists), two methods only:

- `string Provider { get; }`
- `Task<string> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)`
- Parse stays next to the webhook route **or** `bool TryParsePaid(raw, headers, creds, out PaidWebhook)` — verify + event id + checkout id + provider ref + ignore.

`WebhookEndpoints`: `switch (provider)` on the **allow-list** (`stripe|chip|billplz|xendit|razorpay`). Unknown → 400. A switch of five **known** names is not Hub’s `GetAdapter` over `IEnumerable`. Do not register unused names “for later.”

`PublicPayEndpoints.Start`: load `active_provider` (or `checkout.Provider` if already set). Call that rail. Persist `checkout.Provider` + `PspRedirectUrl` + provider session id (CHIP purchase id, Billplz bill id, Xendit invoice id, Razorpay `plink_`, Stripe `cs_`). Missing email: **400** for CHIP/Billplz/Xendit (Hub `TryResolveEmail`). Stripe may keep optional email.

`GatewayEndpoints.Put`: allow the five names. Writer only. Per-rail required fields (see §5). `Get`: return the **active** rail’s metadata (`provider`, `last4`, `configured`, `capability: "hosted_link"`, `public_merchant_id` if any). Optional `GET ?provider=` for a specific row.

`POST /v1/checkouts`: `RequireWriterAsync` (closes the member-can-charge hole). Store `Provider` only at start, not at create (merchant may switch rails before the buyer pays).
```

### 1.5 §3.5 IsolationTests extra greps

```133:137:plans/015-four-adapters/00-what-must-be-done.md
### 3.5 IsolationTests extra greps (same PR)

Ban in `src/`: `IPaymentGatewayAdapter`, `PaymentGatewayFactory`, `IPaymentGatewayFactory`, `AddPaymentsModule`, `GatewayPaymentCompletedIntegrationEvent`, `Modules.Payments`.

Do **not** add a csproj reference to Hub. CHIP/Billplz/Xendit = `HttpClient`. Razorpay = `HttpClient` to Payment Links (do **not** add `Razorpay.Api` unless HTTP is blocked; Hub’s SDK is gravity, not a requirement). Stripe.net stays.
```

### 1.6 §3.6 Stripe-path money holes the four rails would copy

```139:149:plans/015-four-adapters/00-what-must-be-done.md
### 3.6 Still recommended on the Stripe path (not “tax”, still money)

These are not adapters, but four new rails will copy the holes if Stripe still has them:

- Per-org `whsec_` (3.2)
- One TX (3.3)
- `mode=setup` / amount 0 **test**
- Wrap key: no git-known default outside Testing
- `:5179` verifying poll (success URL is not paid)

If you skip these, CHIP RSA will be BYOK while Stripe verify stays a platform secret. That is a worse product than Hub.
```

Refuse list items that bind this slice (015 §9), still law. Full §9 list (quoted; items 6–15 are rail/frontend refuses other slices own, but they still constrain the host):

```310:330:plans/015-four-adapters/00-what-must-be-done.md
## 9. Refuse list for the implementing PRs

1. `IPaymentGatewayAdapter` / `PaymentGatewayFactory` / `IEnumerable<IHostedRail>` lookup of unused names.
2. ProjectReference `apps/lazuar-api` or `Modules.*`.
3. MediatR, outbox, `GatewayPaymentCompletedIntegrationEvent`.
4. Silent `ChipWebhookRegistrar` on PUT/boot.
5. `PublicDnsFallback` / `lazuar-local-dev.com`.
6. CHIP `purchase.preauthorized` as paid.
7. Stripe `mode=setup` as paid (keep ignore; **add the test** in step 0).
8. Off-session, Billing Portal, refunds, disputes.
9. SST field, `SstTaxMath`, tax journal, Tax Invoice, VALID, LHDN.
10. Booking processor `tax` / `fee` as 0.
11. Wallet / FPX / DuitNow tiles on `:5179`.
12. Buyer-facing provider dropdown.
13. Placeholder `customer@example.com` to CHIP/Billplz/Xendit.
14. Razorpay e-mandate / `ChargeOffSession`.
15. Default missing currency to MYR.
16. ACK 200 **before** unique insert; signature fail 500.
17. Fulfill inside the rail class.
```

---

## 2. Process, listen port, JSON, CORS

The host is a single ASP.NET minimal API. `launchSettings.json` binds **8081**. `Lazuar.Pay.csproj` is `net10.0`, nullable, warnings-as-errors, `InternalsVisibleTo` the test project, packages: EF Design, Npgsql, **Stripe.net 48.0.0**. No `Razorpay.Api`. No project reference into `apps/lazuar-api`.

JSON is snake_case globally:

```13:18:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
});
```

CORS allow-list is merchant `:5178` and checkout `:5179` only (not ops `:3003`, not portal `:3004`):

```40:50:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
        p.WithOrigins(
                "http://localhost:5178",
                "http://127.0.0.1:5178",
                "http://localhost:5179",
                "http://127.0.0.1:5179")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
```

`Program.cs` is the composition root and the route table. There is no MediatR, no `MapControllers`, no `AddPaymentsModule`. Testing environment **skips** the real Npgsql registration so `PayApiFactory` can swap InMemory:

```34:39:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
if (!builder.Environment.IsEnvironment("Testing"))
{
    var payCs = builder.Configuration.GetConnectionString("Pay")
        ?? "Host=localhost;Port=5435;Database=lazuar_pay;Username=postgres;Password=postgres";
    builder.Services.AddDbContext<PayDbContext>(o => o.UseNpgsql(payCs));
}
```

Default non-Testing connection is local Postgres **5435** / database `lazuar_pay`. That is the new Pay DB, not Hub.

---

## 3. DI: five concrete rails, no factory of five

Live registration in `Program.cs`:

```19:33:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
builder.Services.AddOptions<OneOptions>().BindConfiguration(OneOptions.Section);
builder.Services.AddHttpClient<OneClient>();
builder.Services.AddHttpClient("chip");
builder.Services.AddHttpClient("billplz");
builder.Services.AddHttpClient("xendit");
builder.Services.AddHttpClient("razorpay");
builder.Services.AddDataProtection();
builder.Services.AddSingleton<SecretBox>();
builder.Services.AddScoped<CheckoutStore>();
builder.Services.AddScoped<StripeHosted>();
builder.Services.AddScoped<ChipHosted>();
builder.Services.AddScoped<BillplzHosted>();
builder.Services.AddScoped<XenditHosted>();
builder.Services.AddScoped<RazorpayHosted>();
builder.Services.AddScoped<Fulfillment>();
```

What this is:

- Named `HttpClient`s for CHIP / Billplz / Xendit / Razorpay. Stripe still uses Stripe.net (`SessionService` + `StripeClient`), not a named client.
- `SecretBox` is a **singleton** wrapping AES-GCM. `AddDataProtection()` is registered and **never read** by `SecretBox` (grep: only this line). Dead DI for wrap; the wrap path is homemade AES-GCM (see §9).
- Five **concrete** `AddScoped<*Hosted>()` registrations. There is **no** `AddScoped<IHostedRail, T>`, **no** `IEnumerable<IHostedRail>`, **no** `PaymentGatewayFactory`, **no** `IPaymentGatewayAdapter`.
- `Fulfillment` is scoped and injected into the webhook handler, not into the rails.

`Start` pulls the five types by constructor injection and switches on the allow-list. That is the dispatch shape 015 asked for (a switch of known names), not Hub `GetAdapter`.

Webhook parse is **not** DI. `StripeWebhook` / `ChipWebhook` / `BillplzWebhook` / `XenditWebhook` / `RazorpayWebhook` are `internal static` classes with `Parse(...)`. The host switch in `WebhookEndpoints` calls them. Rails do not fulfill.

Files that exist under `Gateways/` (named so other agents can find them; HTTP vs Hub is not this slice):

- `PayProviders.cs`, `IHostedRail.cs`, `PspParseResult.cs`, `BuyerEmail.cs`
- `GatewayEndpoints.cs`, `WebhookEndpoints.cs`
- `StripeHosted.cs`, `StripeWebhook.cs`
- `ChipHosted.cs`, `ChipWebhook.cs`
- `BillplzHosted.cs`, `BillplzWebhook.cs`
- `XenditHosted.cs`, `XenditWebhook.cs`
- `RazorpayHosted.cs`, `RazorpayWebhook.cs`

No `ChipWebhookRegistrar`. No `PublicDnsFallback`. Grep in `apps/lazuar-pay` for those strings is empty except IsolationTests banning `ApplicationFeeAmount` / `Razorpay.Api`.

---

## 4. Route map (host 8081)

`Program.cs` maps:

```54:76:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/v1/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/ready", async (PayDbContext db, CancellationToken ct) =>
{
    try
    {
        await db.Database.CanConnectAsync(ct);
        return Results.Ok(new { status = "ready" });
    }
    catch
    {
        return Results.Json(new { status = "not_ready" }, statusCode: 503);
    }
});
app.MapWhoami();
app.MapOrgReady();
app.MapCheckouts();
app.MapCatalog();
app.MapPublicPay();
app.MapGateways();
app.MapWebhooks();
app.MapPaymentQueries();
app.MapOneWebhooks();
```

Gateway / pay / webhook / checkout seams that this slice owns:

| Method | Path | Auth | Handler |
|--------|------|------|---------|
| `PUT` | `/v1/orgs/{orgId}/gateway` | Writer (`owner`/`admin`) | `GatewayEndpoints.Put` |
| `GET` | `/v1/orgs/{orgId}/gateway` optional `?provider=` | Member | `GatewayEndpoints.Get` |
| `GET` | `/v1/pay/{token}` | **none** | `PublicPayEndpoints.Get` |
| `POST` | `/v1/pay/{token}/start` | **none** | `PublicPayEndpoints.Start` |
| `POST` | `/v1/webhooks/{provider}/{orgId}` | PSP signature (not One) | `WebhookEndpoints.Handle` |
| `POST` | `/v1/checkouts` | Writer | `CheckoutEndpoints.Create` — stores **no** `Provider` |
| `GET` | `/v1/checkouts/{id}` | Member | `CheckoutEndpoints.Get` |
| `POST` | `/v1/one/webhooks` | HMAC `Pay:OneWebhookSecret` | pause/unpause charges; **does not** seed SST |
| `GET` | `/v1/orgs/{orgId}/payments` | Member | charges list; no tax fields |
| `GET` | `/v1/orgs/{orgId}/receipts` and `/v1/orgs/{orgId}/receipts/{id}` | Member | `number` or `"PENDING"`, `title` as stored (`Official Receipt`) |

`GatewayEndpoints` entire map:

```10:14:apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs
    public static void MapGateways(this WebApplication app)
    {
        app.MapPut("/v1/orgs/{orgId}/gateway", Put);
        app.MapGet("/v1/orgs/{orgId}/gateway", Get);
    }
```

`WebhookEndpoints` entire map:

```13:16:apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs
    public static void MapWebhooks(this WebApplication app)
    {
        app.MapPost("/v1/webhooks/{provider}/{orgId}", Handle);
    }
```

`PublicPayEndpoints` entire map:

```11:15:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
    public static void MapPublicPay(this WebApplication app)
    {
        app.MapGet("/v1/pay/{token}", Get);
        app.MapPost("/v1/pay/{token}/start", Start);
    }
```

Writer vs member (One façade, not Hub authz):

```42:68:apps/lazuar-pay/src/Lazuar.Pay/One/MemberGate.cs
    public static async Task<IResult?> RequireWriterAsync(
        HttpRequest request,
        OneClient one,
        string orgId,
        CancellationToken cancellationToken)
    {
        var denied = await RequireMemberAsync(request, one, orgId, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        Bearer.TryGet(request, out var authorization);
        request.Headers.TryGetValue("X-Lazuar-Tenant-Id", out var hint);
        var who = await one.GetWhoamiAsync(authorization, hint.ToString(), cancellationToken);
        if (who.Value is null)
        {
            return PayErrors.Status(503, "Service Unavailable", "Identity provider failed");
        }

        var role = who.Value.Tenants.FirstOrDefault(t => t.Id == orgId)?.Role;
        if (role is not ("owner" or "admin"))
        {
            return PayErrors.Status(403, "Forbidden", "Writer role required");
        }

        return null;
    }
```

`POST /v1/checkouts` uses that writer gate. Live:

```22:27:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs
        var orgId = body?.OrgId?.Trim();
        var denied = await MemberGate.RequireWriterAsync(request, one, orgId ?? "", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }
```

Create always writes `Interval = "one_off"` and never sets `Provider` / `ProviderSessionId`:

```54:66:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs
        var session = new CheckoutSession
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = orgId!,
            PublicToken = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Convert.ToHexString(Guid.NewGuid().ToByteArray()),
            Amount = body.Amount.Value,
            Currency = currency,
            Status = "open",
            Interval = "one_off",
            SuccessUrl = body.SuccessUrl,
            CancelUrl = body.CancelUrl,
            CreatedAt = DateTimeOffset.UtcNow
        };
```

`CheckoutStore.CreateAsync` copies those fields onto `CheckoutRow` and **does not** copy a provider. That is the 015 rule “store `Provider` only at start.”

Errors are a JSON `{ status, title, detail }` triple:

```3:6:apps/lazuar-pay/src/Lazuar.Pay/One/PayErrors.cs
internal static class PayErrors
{
    public static IResult Status(int status, string title, string detail) =>
        Results.Json(new { status, title, detail }, statusCode: status);
}
```

---

## 5. Allow-list, hosted-rail shape, parse DTO, buyer email

### 5.1 `PayProviders`

```1:29:apps/lazuar-pay/src/Lazuar.Pay/Gateways/PayProviders.cs
namespace Lazuar.Pay.Gateways;

public static class PayProviders
{
    public const string Stripe = "stripe";
    public const string Chip = "chip";
    public const string Billplz = "billplz";
    public const string Xendit = "xendit";
    public const string Razorpay = "razorpay";

    public const string Capability = "hosted_link";

    public static readonly string[] All = [Stripe, Chip, Billplz, Xendit, Razorpay];

    public static bool TryNormalize(string? raw, out string provider)
    {
        provider = (raw ?? "").Trim().ToLowerInvariant();
        return provider is Stripe or Chip or Billplz or Xendit or Razorpay;
    }

    public static bool RequiresPublicMerchantId(string provider) =>
        provider is Chip or Billplz;

    public static bool RequiresEmail(string provider) =>
        provider is not Stripe;

    public static bool AllowsPublicMerchantId(string provider) =>
        RequiresPublicMerchantId(provider);
}
```

Facts locked here:

- Provider strings are **lowercase**. `TryNormalize` lowercases then allow-lists. Unknown names fail. This is the only allow-list the host uses for PUT, GET `?provider=`, start, and webhook path.
- **One capability string for all five:** `hosted_link`. There is no `SupportsEmandate`, no per-rail capability field, no FPX/DuitNow tile flag on the host JSON.
- Public merchant id is **required** for `chip` and `billplz`, **rejected** for the other three (`AllowsPublicMerchantId` is an alias of `RequiresPublicMerchantId`).
- Email is required for every rail except Stripe. Razorpay is in the required set (015 §5.4 “if the Payment Link API requires customer — match Hub”; live host just requires it).

### 5.2 `IHostedRail`

```1:12:apps/lazuar-pay/src/Lazuar.Pay/Gateways/IHostedRail.cs
using Lazuar.Pay.Data;

namespace Lazuar.Pay.Gateways;

public readonly record struct HostedSession(string RedirectUrl, string? ProviderSessionId);

public interface IHostedRail
{
    string Provider { get; }

    Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct);
}
```

015 sketched `Task<string> CreateHostedUrlAsync`. Live returns `HostedSession` so start can persist both the redirect URL and the processor session id (`cs_`, purchase id, bill id, invoice id, `plink_`) without a second round-trip. Still two members on the interface: `Provider` and create. **Parse is not on the interface.** That matches “parse stays next to the webhook route.”

All five `*Hosted` types implement this. They throw `InvalidOperationException("rail not configured")` when the org row is missing (CHIP/Billplz also when `PublicMerchantId` is blank). They do **not** call `Fulfillment`.

### 5.3 `PspParseResult`

```1:17:apps/lazuar-pay/src/Lazuar.Pay/Gateways/PspParseResult.cs
namespace Lazuar.Pay.Gateways;

public sealed class PspParseResult
{
    public required string EventId { get; init; }
    public bool Ignored { get; init; }
    public string? IgnoreReason { get; init; }
    public string? CheckoutId { get; init; }
    public string? ProviderRef { get; init; }
    public long? AmountMinor { get; init; }
    public string? Currency { get; init; }
}

public sealed class PspVerifyException : Exception
{
    public PspVerifyException(string message) : base(message) { }
}
```

No `taxRate`, no `taxAmount`, no Hub `PaidWebhook`. Amount is optional minor units; currency optional ISO. Verify failures are `PspVerifyException` → host 400. Missing webhook material is `InvalidOperationException` containing `"webhook secret"` → host 503 (see §8).

### 5.4 `BuyerEmail`

```1:25:apps/lazuar-pay/src/Lazuar.Pay/Gateways/BuyerEmail.cs
namespace Lazuar.Pay.Gateways;

public static class BuyerEmail
{
    public const string Placeholder = "customer@example.com";

    public static bool IsUsable(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && !string.Equals(email.Trim(), Placeholder, StringComparison.OrdinalIgnoreCase);

    public static string NameFrom(string? email, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return "Customer";
        }

        var at = email.IndexOf('@');
        return at > 0 ? email[..at] : "Customer";
    }
}
```

015 refuse #13: placeholder `customer@example.com` to CHIP/Billplz/Xendit. Live rejects that string (case-insensitive) as unusable. `Start` uses `IsUsable` before dispatch for every non-Stripe rail. Each non-Stripe `*Hosted` also re-checks `IsUsable` and throws `"email is required"` if somehow called without it.

---

## 6. Credential columns (schema + row types)

### 6.1 Rows (live types)

`OrgSettingsRow` — tax column kept, marked unused, **plus** `ActiveProvider`:

```3:11:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
public sealed class OrgSettingsRow
{
    public required string OrgId { get; set; }
    public string Currency { get; set; } = "MYR";
    public bool ChargesPaused { get; set; }
    /// <summary>Unused. Tax is out of this program. Column kept; do not read on the pay path.</summary>
    public bool? SstRegistered { get; set; }
    public string? ActiveProvider { get; set; }
}
```

`CheckoutRow` — `Provider` and `ProviderSessionId` exist; create path leaves them null:

```13:31:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
public sealed class CheckoutRow
{
    public required string Id { get; set; }
    public required string OrgId { get; set; }
    public required string PublicToken { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string Status { get; set; }
    public string Interval { get; set; } = "one_off";
    public string? SuccessUrl { get; set; }
    public string? CancelUrl { get; set; }
    public string? PspRedirectUrl { get; set; }
    public string? PayerName { get; set; }
    public string? PayerEmail { get; set; }
    public string? ProductId { get; set; }
    public string? Provider { get; set; }
    public string? ProviderSessionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

`GatewayCredentialRow` — the 015 field set:

```58:68:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
public sealed class GatewayCredentialRow
{
    public required string OrgId { get; set; }
    public required string Provider { get; set; }
    public required string Ciphertext { get; set; }
    public string? Last4 { get; set; }
    public string? WebhookCiphertext { get; set; }
    public string? PublicMerchantId { get; set; }
    public string Environment { get; set; } = "test";
    public DateTimeOffset UpdatedAt { get; set; }
}
```

`PspWebhookEventRow` — unique grain `(OrgId, Provider, EventId)`:

```70:76:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
public sealed class PspWebhookEventRow
{
    public required string OrgId { get; set; }
    public required string Provider { get; set; }
    public required string EventId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}
```

EF keys:

```60:69:apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs
        model.Entity<GatewayCredentialRow>(e =>
        {
            e.ToTable("gateway_credentials");
            e.HasKey(x => new { x.OrgId, x.Provider });
        });
        model.Entity<PspWebhookEventRow>(e =>
        {
            e.ToTable("psp_webhook_events");
            e.HasKey(x => new { x.OrgId, x.Provider, x.EventId });
        });
```

Snapshot confirms the extra credential columns are on the model:

```198:227:apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/PayDbContextModelSnapshot.cs
            modelBuilder.Entity("Lazuar.Pay.Data.GatewayCredentialRow", b =>
                {
                    b.Property<string>("OrgId")
                        .HasColumnType("text");

                    b.Property<string>("Provider")
                        .HasColumnType("text");

                    b.Property<string>("Ciphertext")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Environment")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Last4")
                        .HasColumnType("text");

                    b.Property<string>("PublicMerchantId")
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("WebhookCiphertext")
                        .HasColumnType("text");

                    b.HasKey("OrgId", "Provider");

                    b.ToTable("gateway_credentials", "public");
                });
```

`SstRegistered` remains on `org_settings` in the snapshot (`PayDbContextModelSnapshot.cs` around the `OrgSettingsRow` entity, `bool?`). No drop migration. That is the 015 “leave the column” instruction.

### 6.2 What Initial had vs what FourAdapters added

Initial `gateway_credentials` (2026-08-21): `OrgId`, `Provider`, `Ciphertext`, `Last4`, `UpdatedAt` only — PK `(OrgId, Provider)` already. That is why 015 called the live host “Stripe-shaped and too thin”: the PK could store `"chip"` but PUT 400’d anything except stripe, GET always `FindAsync([orgId, "stripe"])`, and there was no webhook ciphertext.

Migration `20260824120000_FourAdaptersHostedRails`:

```13:55:apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260824120000_FourAdaptersHostedRails.cs
        migrationBuilder.AddColumn<string>(
            name: "WebhookCiphertext",
            schema: "public",
            table: "gateway_credentials",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicMerchantId",
            schema: "public",
            table: "gateway_credentials",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Environment",
            schema: "public",
            table: "gateway_credentials",
            type: "text",
            nullable: false,
            defaultValue: "test");

        migrationBuilder.AddColumn<string>(
            name: "ActiveProvider",
            schema: "public",
            table: "org_settings",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Provider",
            schema: "public",
            table: "checkouts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProviderSessionId",
            schema: "public",
            table: "checkouts",
            type: "text",
            nullable: true);
```

Column names in the database are **PascalCase** (`WebhookCiphertext`, not `webhook_ciphertext`) because EF is not configured with snake-case naming. JSON on the wire is snake_case; Postgres columns follow the CLR names. That is a host fact, not a 015 violation.

---

## 7. PUT / GET keys (`GatewayEndpoints`)

The entire PUT/GET surface is one file. Behaviour, in order.

### 7.1 PUT body

```203:212:apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs
public sealed class PutGatewayRequest
{
    public string? Provider { get; set; }
    public string? Secret { get; set; }
    public string? WebhookSecret { get; set; }
    public string? PublicMerchantId { get; set; }
    public string? Environment { get; set; }
    public string? KeyId { get; set; }
    public string? KeySecret { get; set; }
}
```

JSON names (global snake_case): `provider`, `secret`, `webhook_secret`, `public_merchant_id`, `environment`, `key_id`, `key_secret`. Razorpay may send either `secret` as `key_id:key_secret` or the split fields; PUT concatenates:

```36:42:apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs
        var secret = body?.Secret?.Trim();
        if (string.IsNullOrWhiteSpace(secret)
            && !string.IsNullOrWhiteSpace(body?.KeyId)
            && !string.IsNullOrWhiteSpace(body?.KeySecret))
        {
            secret = body.KeyId.Trim() + ":" + body.KeySecret.Trim();
        }
```

### 7.2 PUT validation (writer, allow-list, per-rail)

```25:80:apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs
        var denied = await MemberGate.RequireWriterAsync(request, one, orgId, ct);
        if (denied is not null)
        {
            return denied;
        }

        if (!PayProviders.TryNormalize(body?.Provider, out var provider))
        {
            return PayErrors.Status(400, "Bad Request", "unknown provider");
        }

        var secret = body?.Secret?.Trim();
        if (string.IsNullOrWhiteSpace(secret)
            && !string.IsNullOrWhiteSpace(body?.KeyId)
            && !string.IsNullOrWhiteSpace(body?.KeySecret))
        {
            secret = body.KeyId.Trim() + ":" + body.KeySecret.Trim();
        }

        var webhookSecret = body?.WebhookSecret?.Trim();
        if (string.IsNullOrWhiteSpace(secret))
        {
            return PayErrors.Status(400, "Bad Request", "secret is required");
        }

        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return PayErrors.Status(400, "Bad Request", "webhook_secret is required");
        }

        var publicId = body?.PublicMerchantId?.Trim();
        if (PayProviders.RequiresPublicMerchantId(provider) && string.IsNullOrWhiteSpace(publicId))
        {
            return PayErrors.Status(400, "Bad Request", "public_merchant_id is required");
        }

        if (!PayProviders.AllowsPublicMerchantId(provider) && !string.IsNullOrWhiteSpace(publicId))
        {
            return PayErrors.Status(400, "Bad Request", "public_merchant_id is not used for this provider");
        }

        var environment = string.IsNullOrWhiteSpace(body?.Environment) ? "test" : body.Environment.Trim().ToLowerInvariant();
        if (environment is not ("test" or "live"))
        {
            return PayErrors.Status(400, "Bad Request", "environment must be test or live");
        }

        if (provider == PayProviders.Billplz && string.IsNullOrWhiteSpace(body?.Environment))
        {
            return PayErrors.Status(400, "Bad Request", "environment is required");
        }

        if (provider == PayProviders.Razorpay && !RazorpayHosted.TrySplit(secret, out _, out _))
        {
            return PayErrors.Status(400, "Bad Request", "secret must be key_id:key_secret");
        }
```

Per-rail required fields as the **host** enforces them (HTTP extract details belong to 04–08):

| Provider | `secret` | `webhook_secret` | `public_merchant_id` | `environment` |
|----------|----------|------------------|----------------------|---------------|
| `stripe` | required (wrapped as API key) | required | **400 if present** | default `test` |
| `chip` | required (Bearer) | required (PEM, not format-checked) | **required** (Brand ID) | default `test` |
| `billplz` | required | required (X-Signature) | **required** (Collection ID) | **required** `test`\|`live` |
| `xendit` | required | required (callback token) | **400 if present** | default `test` |
| `razorpay` | `key_id:key_secret` or split fields | required | **400 if present** | default `test` |
| anything else | 400 `unknown provider` | | | |

`last4`: last four of the API secret, except Razorpay uses last four of `key_id` so GET does not leak the secret suffix:

```82:86:apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs
        var last4 = secret.Length >= 4 ? secret[^4..] : secret;
        if (provider == PayProviders.Razorpay && RazorpayHosted.TrySplit(secret, out var keyId, out _))
        {
            last4 = keyId.Length >= 4 ? keyId[^4..] : keyId;
        }
```

### 7.3 PUT persist + one active_provider

Both secrets go through `SecretBox.Protect`. Upsert is `FindAsync([orgId, provider])`. **PUT always writes `OrgSettings.ActiveProvider = provider`.** There is no “stage CHIP keys while Stripe remains active.” One PUT is both BYOK store and rail switch. Multiple credential rows can coexist (PK is org+provider); only `ActiveProvider` is the charge name.

```88:134:apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs
        var wrapped = box.Protect(secret);
        var wrappedWh = box.Protect(webhookSecret);
        var row = await db.GatewayCredentials.FindAsync([orgId, provider], ct);
        if (row is null)
        {
            db.GatewayCredentials.Add(new GatewayCredentialRow
            {
                OrgId = orgId,
                Provider = provider,
                Ciphertext = wrapped,
                WebhookCiphertext = wrappedWh,
                PublicMerchantId = publicId,
                Environment = environment,
                Last4 = last4,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            row.Ciphertext = wrapped;
            row.WebhookCiphertext = wrappedWh;
            row.PublicMerchantId = publicId;
            row.Environment = environment;
            row.Last4 = last4;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var settings = await db.OrgSettings.FindAsync([orgId], ct);
        if (settings is null)
        {
            settings = new OrgSettingsRow { OrgId = orgId, ActiveProvider = provider };
            db.OrgSettings.Add(settings);
        }
        else
        {
            settings.ActiveProvider = provider;
        }

        db.AuditEvents.Add(new AuditEventRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = orgId,
            Action = "gateway.credentials.upsert",
            At = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
```

Audit action is `gateway.credentials.upsert`. One `SaveChanges` covers credential row + active provider + audit. No Chip registrar HTTP on PUT.

Response JSON **never echoes ciphertext**. Capability is always `hosted_link`:

```190:200:apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs
    static object GatewayJson(string orgId, GatewayCredentialRow row, bool configured) => new
    {
        org_id = orgId,
        provider = row.Provider,
        last4 = row.Last4,
        configured,
        capability = PayProviders.Capability,
        public_merchant_id = row.PublicMerchantId,
        environment = row.Environment,
        webhook_configured = !string.IsNullOrWhiteSpace(row.WebhookCiphertext)
    };
```

Hermetic lock (`GatewayTests.Put_and_get_does_not_echo_secret`): PUT/GET body must not contain `sk_test_dummy` or `whsec_abc`; `capability == "hosted_link"`; `OrgSettings.ActiveProvider == "stripe"`; audit row exists. `Member_cannot_put_gateway` is 403. `Put_requires_webhook_secret` is 400 if `webhook_secret` omitted. `Chip_put_requires_brand_id` is 400 without `public_merchant_id`.

### 7.4 GET: active rail, optional `?provider=`

GET is **member** (not writer). Empty `provider` query → `org_settings.ActiveProvider`. Present `provider` → normalize or 400. Missing active name → `{ org_id, configured: false }` with **no** provider field. Missing row for that name → `{ org_id, provider, configured: false }`. Hit → `GatewayJson` with `configured: true`.

```147:188:apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs
    static async Task<IResult> Get(
        string orgId,
        string? provider,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        CancellationToken ct)
    {
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId, ct);
        if (denied is not null)
        {
            return denied;
        }

        string? name = provider;
        if (string.IsNullOrWhiteSpace(name))
        {
            var settings = await db.OrgSettings.AsNoTracking().FirstOrDefaultAsync(x => x.OrgId == orgId, ct);
            name = settings?.ActiveProvider;
        }
        else if (!PayProviders.TryNormalize(name, out var normalized))
        {
            return PayErrors.Status(400, "Bad Request", "unknown provider");
        }
        else
        {
            name = normalized;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Results.Json(new { org_id = orgId, configured = false }, OneClient.Json);
        }

        var row = await db.GatewayCredentials.AsNoTracking().FirstOrDefaultAsync(x => x.OrgId == orgId && x.Provider == name, ct);
        if (row is null)
        {
            return Results.Json(new { org_id = orgId, provider = name, configured = false }, OneClient.Json);
        }

        return Results.Json(GatewayJson(orgId, row, configured: true), OneClient.Json);
    }
```

This is **not** the old `FindAsync([orgId, "stripe"])`. GET describes whatever name is active, or a specific allow-listed name. `public_merchant_id` is returned in plaintext (015: not a secret).

---

## 8. Start dispatch (`PublicPayEndpoints`)

### 8.1 Public GET — no Bearer, `email_required` from active/started rail

```17:38:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
    static async Task<IResult> Get(string token, CheckoutStore store, PayDbContext db, CancellationToken ct)
    {
        var session = await store.GetByPublicTokenAsync(token, ct);
        if (session is null)
        {
            return PayErrors.Status(404, "Not Found", "Checkout not found");
        }

        var row = await db.Checkouts.AsNoTracking().FirstAsync(x => x.Id == session.Id, ct);
        var settings = await db.OrgSettings.AsNoTracking().FirstOrDefaultAsync(x => x.OrgId == session.OrgId, ct);
        var provider = row.Provider ?? settings?.ActiveProvider;
        var emailRequired = PayProviders.TryNormalize(provider, out var p) && PayProviders.RequiresEmail(p);
        return Results.Json(new
        {
            token,
            amount = session.Amount,
            currency = session.Currency,
            status = session.Status,
            payer_name = session.PayerName,
            payer_email = session.PayerEmail,
            email_required = emailRequired
        }, OneClient.Json);
    }
```

No provider picker in this JSON. Buyer sees amount, currency, status, payer fields, and whether email is required. After start, `row.Provider` wins so a mid-flight rail switch on `ActiveProvider` does not flip `email_required` for a checkout already sent to CHIP.

`PublicPayTests.Public_get_does_not_need_bearer` proves GET is public and does not re-hit One on the second fetch.

### 8.2 POST start — entire method

```41:120:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
    static async Task<IResult> Start(
        string token,
        StartPayRequest? body,
        CheckoutStore store,
        PayDbContext db,
        StripeHosted stripe,
        ChipHosted chip,
        BillplzHosted billplz,
        XenditHosted xendit,
        RazorpayHosted razorpay,
        CancellationToken ct)
    {
        var session = await store.GetByPublicTokenAsync(token, ct);
        if (session is null)
        {
            return PayErrors.Status(404, "Not Found", "Checkout not found");
        }

        if (session.Status is "paid" or "expired")
        {
            return PayErrors.Status(409, "Conflict", "Checkout is not open");
        }

        var settings = await db.OrgSettings.FindAsync([session.OrgId], ct);
        if (settings?.ChargesPaused == true)
        {
            return PayErrors.Status(403, "Forbidden", "Org charges are paused");
        }

        var row = await db.Checkouts.FirstAsync(x => x.Id == session.Id, ct);
        if (!string.IsNullOrWhiteSpace(body?.Name))
        {
            row.PayerName = body.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(body?.Email))
        {
            row.PayerEmail = body.Email.Trim();
        }

        var provider = row.Provider ?? settings?.ActiveProvider;
        if (!PayProviders.TryNormalize(provider, out var name))
        {
            return PayErrors.Status(503, "Service Unavailable", "rail not configured");
        }

        if (PayProviders.RequiresEmail(name) && !BuyerEmail.IsUsable(row.PayerEmail))
        {
            return PayErrors.Status(400, "Bad Request", "email is required");
        }

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
        catch (InvalidOperationException ex)
        {
            var status = ex.Message.Contains("callback base", StringComparison.Ordinal) ? 400 : 503;
            return PayErrors.Status(status, status == 400 ? "Bad Request" : "Service Unavailable", ex.Message);
        }
        catch (Stripe.StripeException)
        {
            return PayErrors.Status(503, "Service Unavailable", "Stripe rejected the org key");
        }
    }
```

Dispatch order, as live code actually runs it:

1. Load checkout by **public token**. 404 if missing. No One Bearer.
2. 409 if `paid` or `expired`. (`open` continues; there is no explicit `verifying` status on the host — verifying is a checkout-SPA concern.)
3. 403 if `ChargesPaused`.
4. Overlay optional `name` / `email` from `StartPayRequest` onto the **tracked** `CheckoutRow`.
5. Provider = `checkout.Provider` (retry start, do not switch mid-flight) **else** `org_settings.ActiveProvider`.
6. If that string is missing or not in the five names → **503** `"rail not configured"` (not 400; P17 checklist said 400 for unknown active_provider; live is 503).
7. Non-Stripe + unusable email (blank or `customer@example.com`) → **400** `"email is required"`. Stripe may start with no email.
8. `switch` onto the five scoped instances. The `_ => stripe` arm is **dead** given `TryNormalize` already returned a known name. It is not an `IEnumerable<IHostedRail>` lookup. It is a smell: a future sixth allow-list name would silently hit Stripe.
9. `CreateHostedUrlAsync`. Persist `Provider`, `PspRedirectUrl`, `ProviderSessionId`. Return `{ redirect_url }`.
10. `InvalidOperationException` whose message contains `"callback base"` → 400 (Billplz public HTTPS). Any other `InvalidOperationException` (`rail not configured`, `CHIP rejected the org key`, `email is required` if a rail re-throws) → 503. Stripe.net failures → 503 `"Stripe rejected the org key"`.

Billplz host selection and localhost fail-closed live on the rail the switch calls (existence, not Hub cross-check):

```35:38:apps/lazuar-pay/src/Lazuar.Pay/Gateways/BillplzHosted.cs
        var host = string.Equals(cred.Environment, "live", StringComparison.OrdinalIgnoreCase)
            ? "https://www.billplz.com/api/v3/"
            : "https://www.billplz-sandbox.com/api/v3/";
        var callback = $"{publicBase}/v1/webhooks/billplz/{checkout.OrgId}?checkout_id={Uri.EscapeDataString(checkout.Id)}";
```

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

That last clause is the 015 refuse of `PublicDnsFallback` / `lazuar-local-dev.com`: the host **rejects** that host rather than rewriting DNS.

Stripe create is `Mode = "payment"` (not setup) and writes metadata `checkout_id` / `org_id`. Default success URL already includes `?status=verifying` on `:5179` — host-side hint that success URL is not paid (SPA poll is file 03).

One-active-rail honesty on start: the buyer never chooses a PSP. Missing `ActiveProvider` is 503, not a dropdown.

---

## 9. Secrets (`SecretBox`) and wrap-rails capability

### 9.1 Wrap

```1:57:apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Secrets;

/// <summary>AES-GCM wrap for BYOK. Key from Pay:WrapKey (32-byte base64). Never log plaintext.</summary>
public sealed class SecretBox(IConfiguration config, IHostEnvironment env)
{
    public string Protect(string plaintext)
    {
        var key = LoadKey();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    public string Unprotect(string wrapped)
    {
        var key = LoadKey();
        var raw = Convert.FromBase64String(wrapped);
        var nonce = raw[..12];
        var tag = raw[12..28];
        var cipher = raw[28..];
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    byte[] LoadKey()
    {
        var b64 = config["Pay:WrapKey"];
        if (string.IsNullOrWhiteSpace(b64))
        {
            if (env.IsProduction())
            {
                throw new InvalidOperationException("Pay:WrapKey is required in Production");
            }

            return SHA256.HashData("lazuar-pay-dev-wrap-key"u8.ToArray());
        }

        var key = Convert.FromBase64String(b64);
        if (key.Length != 32)
        {
            throw new InvalidOperationException("Pay:WrapKey must be 32 bytes base64");
        }

        return key;
    }
}
```

Live wrap rules:

- Production missing `Pay:WrapKey` → throw at Protect/Unprotect. Matches 015.
- Non-Production missing key → **git-known** `SHA256("lazuar-pay-dev-wrap-key")`. That includes `Development` and `Testing`. 015 §3.6 said “no git-known default **outside Testing**.” Live still has the default in Development. `.env.example` documents `Pay__WrapKey` as commented optional: “Dev has a fallback; production must set this.”
- `PayApiFactory` does **not** set `Pay:WrapKey`; tests rely on the git string. That is acceptable for Testing; it is the Development hole.
- `AddDataProtection()` does not wrap keys. BYOK is AES-GCM with a process key, not ASP.NET DPAPI.

PUT protects both API secret and webhook secret. Rails unprotect `Ciphertext` for HTTP Authorization and `WebhookCiphertext` for verify.

### 9.2 Stripe webhook secret: per-org first, process env fallback, Production 503

```70:83:apps/lazuar-pay/src/Lazuar.Pay/Gateways/StripeWebhook.cs
    static string? ResolveSecret(GatewayCredentialRow cred, SecretBox box, IConfiguration config, IHostEnvironment env)
    {
        if (!string.IsNullOrWhiteSpace(cred.WebhookCiphertext))
        {
            return box.Unprotect(cred.WebhookCiphertext);
        }

        if (!env.IsProduction())
        {
            return config["Pay:StripeWebhookSecret"];
        }

        return null;
    }
```

If the resolved secret is blank, `Parse` throws `InvalidOperationException("webhook secret missing")`. `WebhookEndpoints` maps any `InvalidOperationException` whose message contains `"webhook secret"` to **503**:

```61:68:apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs
        catch (PspVerifyException ex)
        {
            return PayErrors.Status(400, "Bad Request", ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("webhook secret", StringComparison.Ordinal))
        {
            return PayErrors.Status(503, "Service Unavailable", ex.Message);
        }
```

CHIP/Billplz/Xendit/Razorpay have **no** process-env fallback. Empty `WebhookCiphertext` → same `"webhook secret missing"` → 503. PUT requires `webhook_secret`, so a configured org should have ciphertext unless an operator nulls the column (the Stripe test `Missing_webhook_secret_is_503_when_rail_configured` does exactly that).

`PayApiFactory` still sets `Pay:StripeWebhookSecret` for tests. `WebhookTests.Completed_session_writes_receipt_and_replay_is_noop` signs with `factory.StripeWebhookSecret` **and** PUT stores `whsec_test_local` on the row, so the row path is the one that runs when ciphertext is present. The process env is the 015 “dev fallback for Stripe only.”

### 9.3 Wrap-rails capability string

Host GET/PUT JSON: `capability = PayProviders.Capability` = `"hosted_link"` for **every** provider. There is no second capability. There is no `SupportsEmandate` property on the host. README restates the sales line:

```48:50:apps/lazuar-pay/README.md
Checkouts persist in Postgres `lazuar_pay` on **5435**. `owner`/`admin` paste **one** processor (stripe, chip, billplz, xendit, razorpay). Capability is `hosted_link`. A verified PSP webhook writes an Official Receipt `RCPT-…` and a two-line journal. Pay does not compute SST or file e-invoices. Buyers have no One account (`:5179/c/{token}`).

Per-org `webhook_secret` (Stripe `whsec_`, CHIP PEM, Billplz X-Signature, Xendit callback token, Razorpay HMAC). Process `Pay__StripeWebhookSecret` is a **dev fallback** only. Billplz needs `Pay__PublicBaseUrl` as public **https** (localhost callbacks 400). `Pay__WrapKey` is required in Production.
```

Host-side honesty that the five rails are wraps, not auto-debit, is this constant plus: CHIP start does not send `force_recurring` (`RailTests` asserts `LastBody` does not contain it); Stripe create is `Mode = "payment"`; Razorpay payload has no e-mandate / setup_future; Fulfillment is not called from any `*Hosted` class.

---

## 10. Webhook pipeline and TX order

The entire handler:

```18:143:apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs
    static async Task<IResult> Handle(
        string provider,
        string orgId,
        HttpRequest request,
        PayDbContext db,
        IConfiguration config,
        IHostEnvironment env,
        SecretBox box,
        Fulfillment fulfillment,
        CancellationToken ct)
    {
        if (!PayProviders.TryNormalize(provider, out var name))
        {
            return PayErrors.Status(400, "Bad Request", "unknown provider");
        }

        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var raw = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return PayErrors.Status(400, "Bad Request", "empty body");
        }

        var cred = await db.GatewayCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == orgId && x.Provider == name, ct);
        if (cred is null)
        {
            return PayErrors.Status(400, "Bad Request", "rail not configured");
        }

        PspParseResult parsed;
        try
        {
            parsed = name switch
            {
                PayProviders.Stripe => StripeWebhook.Parse(raw, request.Headers, cred, box, config, env),
                PayProviders.Chip => ChipWebhook.Parse(raw, request.Headers, cred, box),
                PayProviders.Billplz => BillplzWebhook.Parse(raw, request.Query, cred, box),
                PayProviders.Xendit => XenditWebhook.Parse(raw, request.Headers, cred, box),
                PayProviders.Razorpay => RazorpayWebhook.Parse(raw, request.Headers, cred, box),
                _ => throw new InvalidOperationException("unknown provider")
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
            db.PspWebhookEvents.Add(new PspWebhookEventRow
            {
                OrgId = orgId,
                Provider = name,
                EventId = parsed.EventId,
                ReceivedAt = DateTimeOffset.UtcNow
            });
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
    }
```

### 10.1 Order of operations (paid path)

| Step | What | Commits? |
|------|------|----------|
| 1 | Normalize path `{provider}` — unknown 400 | no |
| 2 | Read raw body — empty/whitespace 400 | no |
| 3 | Load `gateway_credentials` for `(orgId, provider)` — missing 400 `rail not configured` | no |
| 4 | `switch` parse/verify — bad sig 400; missing webhook secret 503 | no |
| 5 | `FindAsync` unique `(orgId, provider, eventId)` — hit 200 `{ duplicate: true }` | no write |
| 6 | If `Ignored` → `InsertEventAsync` (own SaveChanges, swallow unique) → 200 `{ ignored }` | **yes, ignore row only** |
| 7 | Need `CheckoutId`; load checkout; **`checkout.OrgId != path orgId` → 400** (H13 bind) | no |
| 8 | Currency mismatch 400; amount mismatch (minor units vs `MoneyMath.ToMinor(checkout.Amount)`) 400 | no unique insert |
| 9 | `BeginTransaction` | |
| 10 | Insert `PspWebhookEventRow` + `SaveChanges` | in TX |
| 11 | `Fulfillment.FulfillPaidAsync` (own `SaveChanges` on same context) | in TX |
| 12 | `CommitAsync` | **one commit** |
| 13 | `DbUpdateException` → rollback → 200 `{ duplicate: true }` | no |
| 14 | Any other throw → `await using` disposes uncommitted TX → 5xx | no (intended) |

This is **not** the old 015 bug (“insert `psp_webhook_events` → `SaveChanges` → `FulfillPaidAsync` (own TX)”). There is one `BeginTransactionAsync` around unique insert **and** fulfill. `Fulfillment` itself does **not** open a second transaction; it only `SaveChangesAsync`.

015 must: verify → parse → insert unique → fulfill → one commit. Live matches on the paid path, with two caveats:

1. Unique `FindAsync` is **outside** the transaction (optimistic). Concurrent first deliveries both miss, one unique-wins, the other `DbUpdateException` → `{ duplicate: true }`. That is the intended race handler. It is **not** a pre-insert ACK of success.
2. Ignored events **do** consume the unique grain via `InsertEventAsync`, **not** inside the fulfill TX. 015 allowed “store as ignored and still no-op fulfill.” Live does that for setup/preauth/unpaid/failed/settled/other types.

Refuse #16: ACK 200 **before** unique insert — paid path does not. Signature fail is 400, not 500.

Refuse #17: Fulfill inside the rail class — grep `FulfillPaidAsync` is only `Fulfillment.cs` and this handler.

### 10.2 What each parser marks ignored (host-seam, not Hub HTTP)

These are the `Ignored = true` exits the **same handler** will unique-insert without calling fulfill. Event id namespaces matter so paid and ignore do not collide.

| Rail | Paid | Ignored (unique consumed, no `RCPT-`) |
|------|------|----------------------------------------|
| Stripe | `checkout.session.completed` and not `mode=setup` and `amount_total` not 0 | other event types (`EventId = stripeEvent.Id`); no session; `setup_or_zero` |
| CHIP | `purchase.paid` → `EventId = paid:{purchaseId}` | `preauth:{id}`; `failed:{id}`; other `event_type:{id}` |
| Billplz | `paid=true` or `state=paid` → `paid:{billId}` | `unpaid:{billId}` |
| Xendit | `PAID` / `invoice.paid` → `paid:{invoiceId}` | `settled:{id}` (explicit); other statuses `{status}:{id}` |
| Razorpay | `payment.captured` → header `X-Razorpay-Event-Id` or `captured:{pay_id}` (**never** bare `pay_`) | `payment.failed` (header id or `failed:{pay_id}`); other events (header or event type string) |

Stripe setup/zero is live in `StripeWebhook.Parse`:

```42:55:apps/lazuar-pay/src/Lazuar.Pay/Gateways/StripeWebhook.cs
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
```

`WebhookTests.Setup_mode_is_ignored` and `Zero_amount_session_is_ignored` lock that. CHIP preauthorized is locked in `RailTests.Chip_preauthorized_is_ignored`. Xendit SETTLED is locked in `RailTests.Xendit_paid_and_settled` (second POST does not mint a second document).

### 10.3 Org bind, amount match, provider bind

- Org bind: `checkout is null || checkout.OrgId != orgId` → 400, **no unique insert**. `WebhookTests.Cross_org_checkout_is_400` posts a t1 checkout id to `/v1/webhooks/stripe/t2` after putting t2 keys; zero documents.
- Amount: `parsed.AmountMinor.Value != MoneyMath.ToMinor(checkout.Amount)` → 400, no unique insert. PSP will retry. 015 said match or refuse; live refuses with 400.
- Currency: same, 400. Billplz parser **hard-codes** `Currency = "MYR"`. A non-MYR checkout that somehow used Billplz would 400 currency mismatch.
- **Provider bind is missing.** The handler does not require `checkout.Provider == name`. After a merchant PUTs CHIP (flipping `ActiveProvider`) leftover Stripe credentials still verify Stripe webhooks. A Stripe event with that org’s checkout id would fulfill and stamp `Charge.Provider = "stripe"` even if `checkout.Provider` is `chip` (or still null). Same-handler fulfill still runs; the charge’s provider is the **path** name.

### 10.4 InMemory vs Postgres TX

```40:41:apps/lazuar-pay/tests/Lazuar.Pay.Tests/PayApiFactory.cs
            services.AddDbContext<PayDbContext>(o => o.UseInMemoryDatabase(_dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
```

Hermetic tests **ignore transactions**. Replay `{ duplicate: true }` + one `RCPT-` is locked (`WebhookTests`, CHIP replay in `RailTests`). Rollback-on-fulfill-throw is **not** proven on 5435 and not injectable (no test double for `Fulfillment`). 015 H25 even allowed “if not injectable, H12 one-TX is the proof.” Live code: only `DbUpdateException` is caught as duplicate; a fulfill `InvalidOperationException` / generic failure bubbles 5xx and the `await using` transaction should rollback on Postgres. That is the intended money-safety. It is not a test.

`InsertEventAsync` for ignored:

```126:143:apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs
    static async Task InsertEventAsync(PayDbContext db, string orgId, string provider, string eventId, CancellationToken ct)
    {
        db.PspWebhookEvents.Add(new PspWebhookEventRow
        {
            OrgId = orgId,
            Provider = provider,
            EventId = eventId,
            ReceivedAt = DateTimeOffset.UtcNow
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // duplicate ignore
        }
    }
```

### 10.5 Early unique hit vs fulfill no-op

If unique miss, checkout is `open`, amount matches, TX inserts event, then `FulfillPaidAsync` finds `Status != "open"` (lost update) it **returns without throwing**. The handler still **commits** the event and returns `{ ok: true }` with no new receipt. That is a second-event no-op, not lost cash on first paid. First paid with `open` writes the receipt inside the same TX.

If `FulfillPaidAsync` returns early because checkout is missing: the handler already loaded the checkout; fulfill reloads by id. Should still see it.

---

## 11. Fulfillment (same handler, tax out)

Entire money writer:

```1:130:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
using Lazuar.Pay.Data;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Money;

public sealed class Fulfillment(PayDbContext db)
{
    public async Task FulfillPaidAsync(string checkoutId, string provider, string? providerRef, CancellationToken ct)
    {
        var checkout = await db.Checkouts.FirstOrDefaultAsync(x => x.Id == checkoutId, ct);
        if (checkout is null)
        {
            return;
        }

        if (checkout.Amount <= 0)
        {
            return;
        }

        if (checkout.Status != "open")
        {
            return;
        }

        checkout.Status = "paid";
        db.Charges.Add(new ChargeRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = checkout.OrgId,
            CheckoutId = checkout.Id,
            Provider = provider,
            ProviderRef = providerRef,
            Amount = checkout.Amount,
            Currency = checkout.Currency,
            Status = "paid"
        });

        string? payerId = null;
        if (!string.IsNullOrWhiteSpace(checkout.PayerEmail) || !string.IsNullOrWhiteSpace(checkout.PayerName))
        {
            payerId = Guid.NewGuid().ToString("N");
            db.Payers.Add(new PayerRow
            {
                Id = payerId,
                OrgId = checkout.OrgId,
                Email = checkout.PayerEmail,
                Name = checkout.PayerName
            });
        }

        if (checkout.Interval is "mo" or "yr")
        {
            db.Subscriptions.Add(new SubscriptionRow
            {
                Id = Guid.NewGuid().ToString("N"),
                OrgId = checkout.OrgId,
                CheckoutId = checkout.Id,
                PayerId = payerId,
                Status = "active",
                Interval = checkout.Interval
            });
        }

        var entryId = Guid.NewGuid().ToString("N");
        db.JournalEntries.Add(new JournalEntryRow
        {
            Id = entryId,
            OrgId = checkout.OrgId,
            CheckoutId = checkout.Id,
            Currency = checkout.Currency,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.JournalLines.Add(new JournalLineRow
        {
            Id = Guid.NewGuid().ToString("N"),
            EntryId = entryId,
            Account = "cash",
            Dc = "D",
            Amount = checkout.Amount
        });
        db.JournalLines.Add(new JournalLineRow
        {
            Id = Guid.NewGuid().ToString("N"),
            EntryId = entryId,
            Account = "revenue",
            Dc = "C",
            Amount = checkout.Amount
        });

        var year = MalaysiaTime.Year(DateTimeOffset.UtcNow);
        var seq = await db.DocumentSequences.FindAsync([checkout.OrgId, "RCPT", year], ct);
        if (seq is null)
        {
            seq = new DocumentSequenceRow { OrgId = checkout.OrgId, Series = "RCPT", YearMyt = year, LastN = 0 };
            db.DocumentSequences.Add(seq);
        }

        seq.LastN += 1;
        var number = $"RCPT-{year}-{seq.LastN:00000}";
        db.Documents.Add(new DocumentRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = checkout.OrgId,
            CheckoutId = checkout.Id,
            Number = number,
            Title = "Official Receipt",
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.AuditEvents.Add(new AuditEventRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = checkout.OrgId,
            Action = "checkout.paid",
            At = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }
}

public static class MalaysiaTime
{
    public static int Year(DateTimeOffset utc)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Singapore Standard Time" : "Asia/Kuala_Lumpur");
        return TimeZoneInfo.ConvertTime(utc, zone).Year;
    }
}
```

Receipt year is Malaysia time (`Asia/Kuala_Lumpur`, Windows `Singapore Standard Time`), not UTC. That is host law for `RCPT-{year}-NNNNN`, not tax.

Tax-out evidence in this method:

- **No** read of `OrgSettings`. **No** `SstRegistered`. **No** throw `"SST registration unknown; fail closed"`. Grep of `apps/lazuar-pay/src` for that string is empty. `SstRegistered` remains only on the row type, Initial migration, and snapshot.
- Journal is **two lines**: `cash` debit and `revenue` credit, both `checkout.Amount`. No tax line. No fee line. Razorpay webhook JSON may contain `tax` / `fee`; parsers do not put them on `PspParseResult`; fulfillment never sees them. `RailTests.Razorpay_captured` posts `tax:12, fee:30` and asserts `JournalLines.Count() == 2`.
- Title is `"Official Receipt"`. Receipt GET uses `number ?? "PENDING"` and stored title. No VALID, no Tax Invoice in this host.
- Checkout create no longer seeds `SstRegistered = false`. It inserts `new OrgSettingsRow { OrgId = orgId }` when missing (null SST). One webhook on `tenant.suspended` sets `ChargesPaused = true` and does **not** touch SST:

```58:69:apps/lazuar-pay/src/Lazuar.Pay/One/OneWebhookEndpoints.cs
        if (type == "tenant.suspended" && !string.IsNullOrWhiteSpace(orgId))
        {
            var settings = await db.OrgSettings.FindAsync([orgId], ct);
            if (settings is null)
            {
                db.OrgSettings.Add(new OrgSettingsRow { OrgId = orgId, ChargesPaused = true });
            }
            else
            {
                settings.ChargesPaused = true;
            }
        }
```

T16 checklist claimed a named SST-null test. **There is no `SstRegistered` string in `apps/lazuar-pay/tests`.** The live proof is: default `bool?` is null, `WebhookTests.Completed_session_writes_receipt_and_replay_is_noop` still mints `RCPT-` and a balanced journal. That is implicit, not a test named for SST. The throw is gone in source; a future regression that re-introduces the throw would fail that Stripe paid test.

`MoneyMath.ToMinor` is `Round(amount * 100, AwayFromZero)` as `long`. Host amount match uses that.

Same-handler: only `WebhookEndpoints` calls `FulfillPaidAsync`. Rails create hosted URLs; they do not book cash.

---

## 12. IsolationTests (Hub types banned)

Entire test class:

```1:112:apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
namespace Lazuar.Pay.Tests;

public class IsolationTests
{
    static readonly string[] Banned = ["lazuar-api", "Modules.", "BuildingBlocks", "MediatR", "Lazuar.Api"];
    static readonly string[] BannedSrc =
    [
        "MediatR", "Modules.One", "BuildingBlocks", "IPaymentGatewayAdapter", "PaymentGatewayFactory",
        "IPaymentGatewayFactory", "AddPaymentsModule", "GatewayPaymentCompletedIntegrationEvent", "Modules.Payments",
        "ApplicationFeeAmount", "Razorpay.Api"
    ];

    [Test]
    public void Host_csproj_does_not_reference_the_old_api()
    {
        AssertNoBanned(File.ReadAllText(FindHostCsproj()));
    }

    [Test]
    public void Test_csproj_does_not_reference_the_old_api()
    {
        var root = FindPayRoot();
        var csproj = Path.Combine(root, "tests", "Lazuar.Pay.Tests", "Lazuar.Pay.Tests.csproj");
        Assert.That(File.Exists(csproj), Is.True);
        AssertNoBanned(File.ReadAllText(csproj));
    }

    [Test]
    public void Source_does_not_use_mediatr_or_hub_modules()
    {
        var src = Path.Combine(FindPayRoot(), "src");
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var token in BannedSrc)
            {
                Assert.That(text, Does.Not.Contain(token), file);
            }
        }
    }

    [Test]
    public void Source_does_not_create_org_or_user_tables()
    {
        var src = Path.Combine(FindPayRoot(), "src");
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.That(text, Does.Not.Contain("ToTable(\"organizations\")"), file);
            Assert.That(text, Does.Not.Contain("ToTable(\"users\")"), file);
            Assert.That(text, Does.Not.Contain("ToTable(\"members\")"), file);
        }
    }

    [Test]
    public void Vite_apps_do_not_use_hub_types()
    {
        var repo = FindPayRoot();
        while (repo is not null && !Directory.Exists(Path.Combine(repo, "apps", "lazuar-pay-merchant")))
        {
            repo = Directory.GetParent(repo)?.FullName;
        }

        Assert.That(repo, Is.Not.Null);
        foreach (var name in new[] { "lazuar-pay-merchant", "lazuar-pay-checkout" })
        {
            var pkg = Path.Combine(repo, "apps", name, "package.json");
            Assert.That(File.Exists(pkg), Is.True, pkg);
            var text = File.ReadAllText(pkg);
            Assert.That(text, Does.Not.Contain("@repo/api-types-ts"), pkg);
        }
    }

    [Test]
    public void No_csproj_references_apps_lazuar_api()
    {
        var root = FindPayRoot();
        foreach (var file in Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories))
        {
            Assert.That(File.ReadAllText(file), Does.Not.Contain("apps/lazuar-api"), file);
            Assert.That(File.ReadAllText(file), Does.Not.Contain(@"apps\lazuar-api"), file);
            Assert.That(File.ReadAllText(file), Does.Not.Contain("Razorpay.Api"), file);
        }
    }

    static void AssertNoBanned(string text)
    {
        foreach (var token in Banned)
        {
            Assert.That(text, Does.Not.Contain(token));
        }
    }

    static string FindHostCsproj() =>
        Path.Combine(FindPayRoot(), "src", "Lazuar.Pay", "Lazuar.Pay.csproj");

    static string FindPayRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "Lazuar.Pay", "Lazuar.Pay.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not find apps/lazuar-pay root");
    }
}
```

015 §3.5 greps are all present in `BannedSrc`. Extra bans live added: `ApplicationFeeAmount` (Connect fee / refuse BYOK-as-MoR), `Razorpay.Api` (SDK gravity), `Modules.One`, `BuildingBlocks`. Csproj bans include `lazuar-api` and `MediatR`.

What IsolationTests do **not** ban (and live does not have, but the test would not catch):

- `IEnumerable<IHostedRail>` lookup — 015 refuse #1. Live injects five concretes. The string `IHostedRail` is allowed (the small shape).
- `ChipWebhookRegistrar`, `PublicDnsFallback` — not in the grep list. Absence is from source grep for this paper, not from IsolationTests.
- `SstTaxMath`, `Tax Invoice`, `VALID` — not in IsolationTests. Fulfillment title + no tax journal is the live tax-out.
- Test sources are not scanned for Hub tokens (only `src/**/*.cs` and csproj). That is correct: the ban is on the host.

`Lazuar.Pay.csproj` packages, again: EF, Npgsql, Stripe.net. `Lazuar.Pay.Tests.csproj` references only the host project plus NUnit / MVC Testing / EF InMemory.

---

## 13. How hermetic tests currently lock these seams

Not a substitute for file 09. These are the **host-seam** assertions that exist today:

| Seam | Test |
|------|------|
| Writer PUT gateway | `GatewayTests.Member_cannot_put_gateway` |
| PUT requires `webhook_secret` | `GatewayTests.Put_requires_webhook_secret` |
| PUT/GET no echo; `hosted_link`; `ActiveProvider` | `GatewayTests.Put_and_get_does_not_echo_secret` |
| CHIP Brand ID required | `GatewayTests.Chip_put_requires_brand_id` |
| Writer create checkout | `CheckoutTests.Member_cannot_create_checkout` |
| Empty Stripe webhook 400 | `PublicPayTests.Empty_webhook_is_400` |
| Unknown webhook provider 400 | `WebhookTests.Unknown_provider_is_400` |
| Stripe missing row whsec 503 | `WebhookTests.Missing_webhook_secret_is_503_when_rail_configured` |
| Bad Stripe sig 400 | `WebhookTests.Invalid_signature_is_400` |
| Paid → `RCPT-` + balanced journal + replay duplicate | `WebhookTests.Completed_session_writes_receipt_and_replay_is_noop` |
| Stripe setup / zero not paid | `WebhookTests.Setup_mode_is_ignored`, `Zero_amount_session_is_ignored` |
| Org bind | `WebhookTests.Cross_org_checkout_is_400` |
| CHIP start + paid + replay; no `force_recurring` | `RailTests.Chip_start_and_paid_webhook` |
| CHIP preauth ignored | `RailTests.Chip_preauthorized_is_ignored` |
| CHIP start without email 400 | `RailTests.Chip_start_without_email_is_400` |
| Billplz paid form (PublicBaseUrl in factory is `https://pay.test.example`) | `RailTests.Billplz_paid_form_and_localhost_blocked` — **name claims localhost blocked; body does not POST with localhost PublicBaseUrl** |
| Xendit PAID then SETTLED still one doc | `RailTests.Xendit_paid_and_settled` |
| Razorpay captured; tax/fee in JSON | `RailTests.Razorpay_captured` |
| CHIP empty body 400 | `RailTests.Chip_empty_body_400` |
| Isolation greps | `IsolationTests` |

Factory HTTP: `FakePspHandler` + `StaticHttpFactory` replace **all** `IHttpClientFactory` clients. Named `chip`/`billplz`/`xendit`/`razorpay` share one fake. `Pay:PublicBaseUrl` is a public https host so Billplz start succeeds in tests.

---

## 14. §3 checklist vs live (law, then code)

| Law | Live |
|-----|------|
| 3.1 Remove SST throw; book `checkout.Amount`; no tax/fee line | **Done.** `Fulfillment` never reads SST. Two journal lines. Title Official Receipt. |
| 3.1 Stop reading/seeding `SstRegistered`; leave column | **Done.** Column + comment. Create and One webhook do not seed it. |
| 3.1 No merchant SST field, no `SstTaxMath`, no LHDN | **Done** in host src (grep empty except column). |
| 3.2 Credential columns | **Done** via `FourAdaptersHostedRails` + `GatewayCredentialRow`. |
| 3.2 `active_provider`; PUT sets it; start uses it | **Done.** |
| 3.2 Stripe verify off process env; Production 503 without row ciphertext | **Done** (`StripeWebhook.ResolveSecret`). |
| 3.3 One TX insert+fulfill; unique → duplicate; fulfill throw rollback; org bind; amount match | **Insert+fulfill in one TX. Unique hit duplicate. Org bind. Amount match. Fulfill throw rollback is coded, not hermetically proven (InMemory ignores TX; no failing double).** |
| 3.3 Ignored events | **Stored as ignored with namespaced ids; no fulfill.** |
| 3.4 No `IPaymentGatewayAdapter` / factory | **Done** (IsolationTests + DI). |
| 3.4 Small `IHostedRail` | **Done**, with `HostedSession` instead of `string`. Parse is static next to webhook. |
| 3.4 Webhook `switch` allow-list; unknown 400 | **Done.** |
| 3.4 Start: active or checkout.Provider; persist provider + URL + session id; email 400 non-Stripe | **Done.** Unknown/missing provider is **503** not 400. Dead `_ => stripe`. |
| 3.4 PUT five names, writer; GET active + `?provider=`; capability `hosted_link` | **Done.** |
| 3.4 Checkout create writer; Provider only at start | **Done.** |
| 3.5 Isolation greps + no Hub csproj + Razorpay HTTP not SDK | **Done.** Stripe.net stays. |
| 3.6 Per-org whsec | **Done** (required on PUT). |
| 3.6 One TX | **Coded; tests do not prove rollback.** |
| 3.6 setup/zero tests | **Done** (`WebhookTests`). |
| 3.6 Wrap key: no git-known default outside Testing | **Not done for Development.** Production throws. Testing/Development hash the git string. |
| 3.6 `:5179` verifying poll | Out of this slice; host default Stripe success URL already has `?status=verifying`. |

015 §9 refuse items 1–5, 16–17 as they apply to the host: live does not have the factory, Hub reference, MediatR, registrar, DNS fallback, pre-insert ACK, or fulfill-in-rail.

---

## 15. Gaps and risks (host seams only)

### 15.1 Money / TX

1. **InMemory ignores transactions.** `PayApiFactory` explicitly suppresses `TransactionIgnoredWarning`. Replay tests prove “second HTTP is duplicate and still one document” after a **successful** first commit, not “fulfill throw rolls back the event id.” On Postgres the `await using` TX should do that; CI does not run this handler against 5435.
2. **Only `DbUpdateException` is mapped to `{ duplicate: true }`.** Other fulfill failures 5xx (good for retry) **if** the exception escapes. `FulfillPaidAsync` **swallows** missing checkout / non-open / amount≤0 by returning. A paid parse that passes amount match then hits a non-open checkout **commits the unique grain** and returns `{ ok: true }` with no new receipt. First-delivery lost-cash is the old bug; this is a quieter “unique consumed, no document” if status flipped outside the TX between load and fulfill.
3. **Ignored insert is a separate SaveChanges.** Fine for no-op fulfill. A crash after ignore-insert is what 015 wanted (never fulfill that setup id).
4. **Amount/currency mismatch 400 without unique insert** → PSP retries forever. Law said refuse. Operational noise, not double-book.
5. **No `checkout.Provider` bind on webhook.** Leftover credentials for a previous `active_provider` remain valid Plane B. Switching rails by PUT does not disable the old webhook path.

### 15.2 Dispatch / door

6. **Start `_ => stripe` default.** Unreachable today; would mis-route a sixth allow-list name.
7. **Unknown / missing `ActiveProvider` on start is 503** `"rail not configured"`, while P17 text said 400. Live is consistent with “no rail” (also used when CHIP Brand ID missing throws). Not a money bug; door copy differs from one checklist.
8. **PUT is the only way to set `ActiveProvider` and it always flips.** You cannot store CHIP Brand ID without making CHIP the charge rail. One-active-rail law is satisfied; ops flexibility is not. Stale rows remain.
9. **GET `?provider=` is implemented but not hermetically asserted** (no test sends the query string).
10. **CHIP PEM is not validated on PUT.** Bad PEM fails later at webhook verify (400 invalid signature after `ImportFromPem` catch). Start can succeed with a nonsense webhook secret.

### 15.3 Secrets / wrap

11. **Git-known wrap key outside Testing.** `SHA256("lazuar-pay-dev-wrap-key")` for every non-Production environment. 015 §3.6 asked to kill that for Development. Production is fail-closed. `.env.example` leaves `Pay__WrapKey` commented.
12. **`AddDataProtection()` is unused.** Confusion risk, not a wrap bypass.
13. **Stripe process env fallback still exists in Testing/Development** by design (015). Production without row ciphertext 503. CHIP RSA is BYOK-only; Stripe can still verify from `Pay:StripeWebhookSecret` in dev if the column is empty — the hole 015 called “worse than Hub” is **closed in Production**, **open in Development** if someone PUTs keys then nulls `WebhookCiphertext` or uses an old row from before the migration (nullable column). New PUTs always write `WebhookCiphertext`.

### 15.4 Tax / receipt

14. **`SstRegistered` column remains.** Correct per 015. Comment says do not read. A future PR can still read it; IsolationTests will not stop that; only the missing throw test-by-name (T16) is weak.
15. **`Fulfillment` still creates subscriptions for `mo`/`yr`.** Checkout create hard-codes `one_off`, so this is dead on the public mint path. Catalog prices have intervals; checkout does not copy them. Not a tax issue; leftover Bar B shape.
16. Receipt list returns stored `title`. As long as fulfill writes Official Receipt, GET cannot print Tax Invoice. No VALID string in host src.

### 15.5 Isolation / Hub gravity

17. IsolationTests would **not** fail if someone added `ChipWebhookRegistrar` or `PublicDnsFallback` under new names. Current tree does not have them. Billplz explicitly rejects `lazuar-local-dev.com`.
18. `IHostedRail` is public and implemented five times; **not** resolved as `IEnumerable<IHostedRail>`. Keep it that way.
19. Vite Isolation check only bans `@repo/api-types-ts` in `package.json`. Out of this slice except to note IsolationTests **does** walk merchant/checkout package.json from the pay test project.

### 15.6 Tests that over-claim or skip host law

20. `RailTests.Billplz_paid_form_and_localhost_blocked` does not assert localhost 400; factory sets a public `Pay:PublicBaseUrl`. The fail-closed lives in `BillplzHosted.TryPublicBase` untested by that method name.
21. No test injects a throwing `Fulfillment` (H25). No test sets `SstRegistered` null explicitly (T16). Both are still **true in source** via default null + paid Stripe webhook.
22. `GatewayTests` does not PUT all five names for field matrices (Billplz environment required, Razorpay split, public id rejected on Stripe). Some of that is exercised indirectly by `RailTests` PUTs.

### 15.7 What is actually solid

- Five lowercase names, one `active_provider`, capability **always** `hosted_link`.
- Writer PUT keys and writer mint checkout; member GET metadata; public start/GET pay.
- Parse/verify in static webhook types; cash only in `Fulfillment`; one webhook route `POST /v1/webhooks/{provider}/{orgId}`.
- Empty body 400, unknown provider 400, bad signature 400, missing Stripe/org webhook secret 503.
- Setup ≠ paid for Stripe; preauthorized ≠ paid for CHIP; SETTLED ≠ second pay for Xendit.
- Placeholder email rejected for non-Stripe.
- No MediatR, no Hub project reference, no `IPaymentGatewayAdapter`, no `PaymentGatewayFactory`, no `Razorpay.Api`.
- Tax throw gone. Official Receipt. Two-line journal of `checkout.Amount`.

---

## 16. Short verdict for 00-evaluation.md (do not treat as a substitute for the quotes above)

Must-do 0 **landed in source** on `feat/015-four-adapters` at `c621ceba`. The 8081 host is no longer Stripe-only: PUT/GET speak five names, start switches five concrete `IHostedRail`s, webhooks switch five parsers, credentials hold webhook ciphertext + public merchant id + environment, `org_settings.ActiveProvider` is the one charge rail, fulfillment is the same handler without an SST throw, IsolationTests grep the factory/Hub strings.

Residual host-seam risk that 016 should not wave away: Development wrap-key git default; webhook provider not bound to `checkout.Provider`; one-TX rollback unproven under InMemory; start’s dead `_ => stripe`; PUT always flips active (cannot stage a second rail); T16/H25 documented as checked in 015 while tests only imply them.

Vite and per-PSP Hub HTTP are other files.
