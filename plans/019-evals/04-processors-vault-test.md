# 04 — Independent processor vault, bind-at-mint, Test rail

**Date:** 26 August 2026  
**Branch:** `feat/018-merchant-shell`  
**HEAD:** `9f04ad58` — `fix(pay-ui): match receipts table to pay-link chrome`  
**Type:** Uncondensed evaluation. Not an implementation. Live files on this SHA are authority. [016](../016-adapters-check/01-new-host-seams.md) and [018-evals](../018-evals/001-evals.md) are historical; if they disagree with source, source wins.

**Question:** After 018 merchant-shell, do processors vault independently, is the rail bound at mint (not by a single org default), and is the local Test processor honest (no secrets, always offered when allowed, can fulfill, cannot leak into Production)? Identify bugs and gaps. For each, explain how to solve it.

---

## Coordinates

| | |
|---|---|
| Host | `apps/lazuar-pay/src/Lazuar.Pay` on **8081** |
| Vault HTTP | `Credentials/GatewayEndpoints.cs` — `PUT/GET /v1/orgs/{id}/gateway`, `GET /v1/orgs/{id}/gateways` |
| Names | `Rails/PayProviders.cs` — `stripe\|chip\|billplz\|xendit\|razorpay\|test` |
| Wrap | `Secrets/SecretBox.cs` — AES-GCM, `Pay:WrapKey` |
| Bind-at-mint | `Checkouts/CheckoutEndpoints.cs`, `PaymentLinks/PaymentLinkEndpoints.cs` |
| Test rail | `Rails/Test/TestHosted.cs`, `Rails/Test/TestWebhook.cs` |
| Start dispatch | `PublicPay/PublicPayEndpoints.cs` — `row.Provider ?? link.Provider`, **not** `OrgSettings.ActiveProvider` |
| Merchant | `apps/lazuar-pay-merchant` `:5178` — `pages/org/GatewayPage.tsx`, `pages/org/CheckoutsPage.tsx`, `lib/processors.ts` |
| Tests | `tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs`, `Rails/Test/TestRailTests.cs`, `Checkouts/CheckoutTests.cs`, `PaymentLinks/PaymentLinkTests.cs`, `Secrets/SecretBoxTests.cs` |
| Schema | `Data/Rows.cs`, `Data/PayDbContext.cs`, `Data/Migrations/20260824120000_FourAdaptersHostedRails.cs` (adds per-rail vault columns **and** leftover `ActiveProvider`) |
| Out of scope here | Full webhook pipeline (paper 06), checkout SPA chrome (paper 03), TypeSpec whole paper (paper 08). Spec vs live is cited only where it collides with vault/mint/Test. |

---

## Files opened

Host:

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/IHostedRail.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestHosted.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestWebhook.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeHosted.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeWebhook.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipHosted.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipWebhook.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Billplz/BillplzHosted.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Billplz/BillplzWebhook.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Xendit/XenditHosted.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Xendit/XenditWebhook.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Razorpay/RazorpayHosted.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Razorpay/RazorpayWebhook.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutStore.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutSession.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CreateCheckoutRequest.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/CreatePaymentLinkRequest.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260821152601_Initial.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260824120000_FourAdaptersHostedRails.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260825120000_PaymentLinkPayers.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/PayDbContextModelSnapshot.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/appsettings.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/appsettings.Development.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Properties/launchSettings.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/.env.example`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/docker-compose.pay.yml`

Merchant:

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/org/OverviewPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/lib/processors.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/lib/payApi.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/locks.test.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/layout/nav.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/README.md`

Tests:

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Test/TestRailTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Checkouts/CheckoutTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Secrets/SecretBoxTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPay/PublicPayTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/WebhookTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayTest.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Stripe/StripeRailTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Chip/ChipRailTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Billplz/BillplzRailTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Xendit/XenditRailTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Razorpay/RazorpayRailTests.cs`

Plans / spec / task:

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/016-adapters-check/00-evaluation.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/016-adapters-check/01-new-host-seams.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/018-evals/001-evals.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/019-evals/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/main.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/Taskfile.yml`

Buyer file opened only to confirm Test fulfill copy (chrome itself is paper 03):

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-checkout/src/App.tsx` (provider === `'test'` sentence)

Grep coverage: `ActiveProvider` / `active_provider` in `*.cs` / `*.ts` / `*.tsx`; `WrapKey` / `StripeWebhookSecret`; `AllowsTest` / `IsProduction`; `last4` / `webhook_configured` / `environment` in tests; `IEnumerable<IHostedRail>`; `MapDelete` / `DeleteGateway`.

---

## What exists (schema, PUT/GET, Test rail, mint bind)

### Schema: per-rail rows, leftover org default column

`gateway_credentials` is the vault. Primary key is `(OrgId, Provider)`. One org can hold five BYOK rows at once. `Last4` was already on the Initial table. Four-adapter migration added the per-rail columns 016 needed:

```16:56:apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260824120000_FourAdaptersHostedRails.cs
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
```

Live row types:

```3:12:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
public sealed class OrgSettingsRow
{
    public required string OrgId { get; set; }
    public string Currency { get; set; } = "MYR";
    public bool ChargesPaused { get; set; }
    /// <summary>Unused. Tax is out of this program. Column kept; do not read on the pay path.</summary>
    public bool? SstRegistered { get; set; }
    /// <summary>Unused. Vault save does not pick a default rail. Column kept; do not read on the pay path.</summary>
    public string? ActiveProvider { get; set; }
}
```

```77:86:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
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

`PayDbContext` maps `GatewayCredentialRow` to `gateway_credentials` with composite key `(OrgId, Provider)` (`PayDbContext.cs:76-80`). Snapshot still has `OrgSettings.ActiveProvider` (`PayDbContextModelSnapshot.cs:358-378`) and the four-adapter columns on `gateway_credentials` (`PayDbContextModelSnapshot.cs:209-239`). Payment links carry a **required** `Provider` (`Rows.cs:42`, created in `20260825120000_PaymentLinkPayers.cs:24`).

**There is no `org_settings.active_provider` snake column.** EF/Npgsql persist PascalCase `ActiveProvider`. 016 papers used the snake name as English. Live SQL identifier is `"ActiveProvider"`.

Grep of `*.cs` / `*.ts` / `*.tsx` for `ActiveProvider` after this SHA: **writes/reads on the pay path = none.** Remaining hits are the unused property, the four-adapter Up/Down, the snapshot, and two test asserts that the column stays **null** after PUT.

### Names, Test offer, capability

```5:40:apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs
public static class PayProviders
{
    public const string Stripe = "stripe";
    public const string Chip = "chip";
    public const string Billplz = "billplz";
    public const string Xendit = "xendit";
    public const string Razorpay = "razorpay";
    public const string Test = "test";

    public const string Capability = "hosted_link";

    public static readonly string[] All = [Stripe, Chip, Billplz, Xendit, Razorpay];

    public static IReadOnlyList<string> Listed(IHostEnvironment env) =>
        AllowsTest(env) ? [..All, Test] : All;

    public static bool AllowsTest(IHostEnvironment env) =>
        !env.IsProduction();
    // ...
    public static bool TryNormalize(string? raw, out string provider)
    {
        provider = (raw ?? "").Trim().ToLowerInvariant();
        return provider is Stripe or Chip or Billplz or Xendit or Razorpay or Test;
    }
```

`All` is the five BYOK rails. `Listed` appends `test` when the process is **not** Production. `TryNormalize` accepts `test` so mint/start/webhook can name it; PUT then rejects Test separately. Capability is still the single string `"hosted_link"` for every rail including Test.

`Program.cs` registers six concretes and maps them by switch. IsolationTests bans `IEnumerable<IHostedRail>`, `PaymentGatewayFactory`, `ChipWebhookRegistrar`, `PublicDnsFallback` (`IsolationTests.cs:6-17`). That law still holds.

### PUT / GET / list

`MapGateways` (`GatewayEndpoints.cs:14-18`):

| Method | Path | Gate | Body / query | Result |
|---|---|---|---|---|
| PUT | `/v1/orgs/{orgId}/gateway` | **writer** (`owner`/`admin`) | one provider + secrets | upsert that row; **does not** write `ActiveProvider` |
| GET | `/v1/orgs/{orgId}/gateway` | member | empty query → **list**; `?provider=` → one row | metadata, never ciphertext |
| GET | `/v1/orgs/{orgId}/gateways` | member | none | `{ org_id, processors: [...] }` covering `PayProviders.Listed` |

PUT is writer-gated:

```30:34:apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs
        var denied = await MemberGate.RequireWriterAsync(request, one, orgId, ct);
        if (denied is not null)
        {
            return denied;
        }
```

`MemberGate.RequireWriterAsync` (`MemberGate.cs:45-71`) is member + whoami role in `owner`/`admin`. GET/list use `RequireMemberAsync`.

PUT rejects unknown names, rejects Test, requires `secret` (or Razorpay `key_id`+`key_secret` joined as `key_id:key_secret`), requires `webhook_secret`, requires `public_merchant_id` for CHIP/Billplz, **rejects** `public_merchant_id` for the others, requires `environment` in `{test,live}` (default `test` if omitted **except Billplz, which 400s if omitted**), Razorpay must split on colon. Then AES-GCM wrap both secrets, upsert `(orgId, provider)`, insert `OrgSettings` **only if missing** (no `ActiveProvider` assignment), audit `gateway.credentials.upsert`, one `SaveChanges`.

```126:140:apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs
        if (await db.OrgSettings.FindAsync([orgId], ct) is null)
        {
            db.OrgSettings.Add(new OrgSettingsRow { OrgId = orgId });
        }

        db.AuditEvents.Add(new AuditEventRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = orgId,
            Action = "gateway.credentials.upsert",
            At = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return Results.Json(GatewayJson(orgId, row, configured: true), OneClient.Json);
```

JSON never echoes plaintext. last4, capability, public_merchant_id, environment, webhook_configured hydrate from the row:

```228:250:apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs
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

    static object TestGatewayJson(string orgId) => new
    {
        org_id = orgId,
        provider = PayProviders.Test,
        last4 = (string?)null,
        configured = true,
        capability = PayProviders.Capability,
        public_merchant_id = (string?)null,
        environment = "test",
        webhook_configured = true
    };
```

GET with empty `provider` **aliases List** (`GatewayEndpoints.cs:158-161`). That is the 018 replacement for 016’s “empty query → `ActiveProvider`”. GET `?provider=test` in a non-Production env returns `TestGatewayJson` without a DB row. GET `?provider=test` in Production falls through to credential lookup, finds nothing, returns `{ configured: false }`. List emits one object per `Listed` name: Test synthetic, missing BYOK rows as `configured: false` with `last4/public_merchant_id/environment` null and `webhook_configured: false`.

There is **no DELETE / PATCH**. Rotation is full PUT of secret + webhook_secret. You cannot un-vault a rail.

### last4

```92:96:apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs
        var last4 = secret.Length >= 4 ? secret[^4..] : secret;
        if (provider == PayProviders.Razorpay && RazorpayHosted.TrySplit(secret, out var keyId, out _))
        {
            last4 = keyId.Length >= 4 ? keyId[^4..] : keyId;
        }
```

For Stripe `sk_test_dummy`, last4 is `ummy` (016 was right about the suffix). For Razorpay, last4 is the **key_id** suffix, not the secret. Webhook secret has no last4. Members and writers receive the **same** `GatewayJson`. last4 is metadata, not a second secret channel. GET never returns ciphertext (`GatewayTests.Member_can_get_gateway_metadata`).

### WrapKey / AES-GCM / Stripe process fallback

`SecretBox` is AES-GCM, 12-byte nonce + 16-byte tag + ciphertext, key from `Pay:WrapKey` as 32-byte base64. Missing key:

```36:47:apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs
    byte[] LoadKey()
    {
        var b64 = config["Pay:WrapKey"];
        if (string.IsNullOrWhiteSpace(b64))
        {
            if (!env.IsEnvironment("Testing"))
            {
                throw new InvalidOperationException("Pay:WrapKey is required");
            }

            return SHA256.HashData("lazuar-pay-dev-wrap-key"u8.ToArray());
        }
```

Git-known wrap is **Testing-only**. Development, Staging, and Production throw. That is stricter than 016’s live note (016 `01-new-host-seams.md` still described a Development fallback). `SecretBoxTests` locks Production throw and Testing round-trip. README matches code: “`Pay__WrapKey` is required outside Testing.” `.env.example` is stale: “Dev has a fallback; production must set this.”

Stripe webhook verify:

```78:91:apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeWebhook.cs
    static string? ResolveSecret(GatewayCredentialRow cred, SecretBox box, IConfiguration config, IHostEnvironment env)
    {
        if (!string.IsNullOrWhiteSpace(cred.WebhookCiphertext))
        {
            return box.Unprotect(cred.WebhookCiphertext);
        }

        if (env.IsEnvironment("Testing"))
        {
            return config["Pay:StripeWebhookSecret"];
        }

        return null;
    }
```

Process `Pay:StripeWebhookSecret` is Testing-only, matching README. Empty ciphertext in Development/Production → `null` → `"webhook secret missing"` → 503 (`WebhookEndpoints.cs:85-88`). CHIP/Billplz/Xendit/Razorpay have **no** process fallback.

### Bind-at-mint

Checkout create (`CheckoutEndpoints.cs:53-73`):

1. Writer gate.
2. `TryNormalize(body.Provider)` or 400 `"unknown provider"` (missing provider is this path — `Create_without_provider_is_400`).
3. If Test: `AllowsTest` or 400 `"test processor is not enabled"`. No credential row required (`Create_test_without_vault_is_201`).
4. Else: `GatewayCredentials` row for `(orgId, provider)` must exist or 400 `"rail not configured"` (`Create_unconfigured_rail_is_400` — Stripe vaulted, CHIP mint 400).
5. Stamp `CheckoutSession.Provider` / `CheckoutRow.Provider` at insert (`CheckoutEndpoints.cs:86`, `CheckoutStore.cs:28`).

Payment-link create is the same bind (`PaymentLinkEndpoints.cs:51-71`) onto **required** `PaymentLinkRow.Provider`. Merchant mint is this door (`CheckoutsPage.tsx` POST `/v1/payment-links`), not `POST /v1/checkouts`.

Public start **does not** read `ActiveProvider`:

```140:166:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        var provider = row.Provider ?? link?.Provider;
        if (!PayProviders.TryNormalize(provider, out var name))
        {
            return PayErrors.Status(503, "Service Unavailable", "rail not configured");
        }
        // ...
        IHostedRail rail = name switch
        {
            PayProviders.Stripe => stripe,
            PayProviders.Chip => chip,
            PayProviders.Billplz => billplz,
            PayProviders.Xendit => xendit,
            PayProviders.Razorpay => razorpay,
            PayProviders.Test => test,
            _ => throw new InvalidOperationException("rail not configured")
        };
```

`email_required` is computed from `row.Provider` (checkout view) or `link.Provider` (unstarted link view) via `PayProviders.RequiresEmail` (everything except Stripe and Test). That is bind-at-mint: the buyer never picks a PSP; staff picked at mint.

Webhook bind (in scope only as vault/Test evidence): `checkout.Provider` must equal path `{provider}` or 400 `"provider mismatch"` (`WebhookEndpoints.cs:120-124`). Leftover Stripe credentials cannot fulfill a CHIP-bound checkout. 016’s “no checkout.Provider bind” is **fixed** on this SHA.

### Test rail: no secrets, can fulfill

PUT Test is 400 `"test processor does not take secrets"` (`GatewayEndpoints.cs:41-44`, `GatewayTests.Put_test_processor_is_400`).

`TestHosted.CreateHostedUrlAsync` does not load credentials. If `AllowsTest`, it returns the hosted success URL (`CheckoutUrls.Success` → `?status=verifying`) and session id `"test:" + checkout.Id`. Otherwise throws `"rail not configured"` (start maps that to 503).

Start **auto-fulfills** Test in the same request:

```176:186:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
            if (PayProviders.IsTest(name))
            {
                db.PspWebhookEvents.Add(new PspWebhookEventRow
                {
                    OrgId = row.OrgId,
                    Provider = name,
                    EventId = hosted.ProviderSessionId ?? "test:" + row.Id,
                    ReceivedAt = DateTimeOffset.UtcNow
                });
                await fulfillment.FulfillPaidAsync(row.Id, name, hosted.ProviderSessionId, ct);
            }
```

`TestRailTests.Mint_and_start_pays_without_keys` locks: start 200, GET status `paid`, `provider=test`, one Official Receipt, **zero** PSP HTTP.

`TestWebhook.Parse` has **no signature, no secret, no header check**. It reads JSON `id` / `checkout_id` / `amount_total` / `currency`. Missing `id` becomes a new Guid (`TestWebhook.cs:27-29`), so replay uniqueness is optional. Webhook path skips credential load for Test (`WebhookEndpoints.cs:50-56`) and only asks `AllowsTest`. `TestRailTests.Webhook_pays_open_test_checkout` POSTs unsigned JSON to `/v1/webhooks/test/t1` and gets a receipt.

### Merchant vault UI and mint picker

`lib/processors.ts` hardcodes `rails = ['test', 'stripe', 'chip', 'billplz', 'xendit', 'razorpay']` and Test copy: “Local only. No secrets.”

`payApi.ts` has **no** typed processor helpers. Pages call `payFetch(token, `/v1/orgs/${orgId}/gateways`)` and `PUT /v1/orgs/${orgId}/gateway`.

`GatewayPage.tsx`:

- Subtitle: “Vault keys per rail. Saving a secret does not pick the rail for pay links.” (`GatewayPage.tsx:121-123`)
- Cards from hardcoded `rails`, not from `Listed`. Test is always drawn. Status: Test → “Ready”; others → “On file” / “Empty”.
- Test has no Edit. Real rails: Edit dialog.
- Save is writer-only. Member sees last4 + webhook on file, cannot paste (`GatewayPage.tsx:180-182`).
- CHIP webhook is a **textarea** (“PEM from CHIP dashboard”). Stripe placeholder `whsec_… (endpoint signing secret)`. Billplz `X-Signature secret`. Xendit `x-callback-token`. Razorpay falls through to `webhook secret`.
- Brand ID / Collection ID only for chip/billplz. Environment `<Select>` **only for billplz**. Razorpay is two fields `key_id` / `key_secret`, joined as `secret` on the wire.
- `openEdit` hydrates `environment` and `public_merchant_id` from GET. Secret inputs start empty. Re-save always sends a fresh `webhook_secret` (host requires it).
- Webhook URL copy: `{payApi}/v1/webhooks/{editing}/{orgId}`.

`CheckoutsPage.tsx` mint dialog:

- Loads `/gateways`, `withTest()` injects `{ provider: 'test', configured: true }` if the host omitted Test (`CheckoutsPage.tsx:30-38`).
- POST `/v1/payment-links` with explicit `provider`.
- Select lists only `configured && isRail`. Test is always in that set (injected).
- Initial React state is `'test'`. `setProvider` keeps `prev` if it is still in `ready`. Because Test is always in `ready` and prev starts as `'test'`, **`firstReal` never runs on first load.**

`OverviewPage.tsx` lists `/gateways` `configured` rows as “On file”, including Test (synthetic `configured: true`).

`locks.test.ts` is source-grep honesty: Processor cards not “One active rail”; Test has no secret editor; pay links send a chosen provider; overview lists processors; CHIP PEM textarea; environment hydrate from GET.

### Development apply four-adapter columns

```74:77:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<PayDbContext>().Database.MigrateAsync();
}
```

`pay:dev` (`ASPNETCORE_ENVIRONMENT=Development` in `launchSettings.json`) applies **all** pending migrations, including `20260824120000_FourAdaptersHostedRails` (WebhookCiphertext, PublicMerchantId, Environment, ActiveProvider, checkout Provider/ProviderSessionId) and `20260825120000_PaymentLinkPayers`. That is the 019 README claim “apply four-adapter columns on Development start,” and it is true.

Production/Staging do **not** auto-migrate. `task pay:db:migrate` is the explicit path (`Taskfile.yml:121-125`). Tests use InMemory `EnsureCreated` from the current model (`PayApiFactory.cs:75-79`), not the migration files.

---

## 016 vs 018 (re-verified: did PUT stop flipping a single active provider?)

### What 016 said

016 `00-evaluation.md:70`: “One `org_settings.active_provider` per org; PUT always flips it.”

016 `01-new-host-seams.md:856-858`:

> Both secrets go through `SecretBox.Protect`. Upsert is `FindAsync([orgId, provider])`. **PUT always writes `OrgSettings.ActiveProvider = provider`.** There is no “stage CHIP keys while Stripe remains active.” One PUT is both BYOK store and rail switch.

The 016 quoted handler (`Gateways/GatewayEndpoints.cs` in that paper) assigned `settings.ActiveProvider = provider` on every successful PUT. GET with empty query loaded that name. Public start used `row.Provider ?? settings.ActiveProvider`. Merchant `:5178` was a five-name `<select>` that PUTed and thereby switched the charge rail. Buyer page had no picker.

016 `01-new-host-seams.md:928` hermetic lock: `OrgSettings.ActiveProvider == "stripe"` after Stripe PUT.

### What 018 claimed

`plans/018-evals/001-evals.md` is a kernel/escrow product paper. **It does not claim independent vault.** The claim lives in the merchant-shell work this branch is evaluating:

- 019 README: “Vault processors independently; bind rail at mint”; “Local Test processor with no secrets”; “always offer Test when minting”.
- Host README (`apps/lazuar-pay/README.md:65`): “`owner`/`admin` paste keys **per rail** … Saving a vault does not pick a default. Mint a pay link with an explicit `provider` that already has keys.”
- Row comment (`Rows.cs:10-11`): “Unused. Vault save does not pick a default rail. Column kept; do not read on the pay path.”
- Merchant Processor subtitle and locks.test.ts “does not pick the rail for pay links”.

### Re-verify against live files (this SHA)

| 016 law | Live 018 host | Verdict |
|---|---|---|
| PUT always writes `ActiveProvider = provider` | PUT inserts `OrgSettings` if missing **with no ActiveProvider**. Never assigns the property. | **PUT stopped flipping.** |
| GET empty query → active rail | GET empty query → `List` (`processors` of `Listed`) | **Changed.** |
| GET `?provider=` inspects a non-active row without switching | GET `?provider=` still one row; there is nothing to switch | **Still true, vacuously.** |
| Start uses `row.Provider ?? ActiveProvider` | Start uses `row.Provider ?? link.Provider`. `ActiveProvider` is unread. | **Bind-at-mint.** |
| One charge name per org | N vault rows + explicit mint `provider` | **Independent vault.** |
| Hermetic: PUT stripe ⇒ `ActiveProvider=="stripe"` | `GatewayTests.Put_and_get_does_not_echo_secret` line 82: `ActiveProvider` **Is.Null**. `List_returns_all_five_and_put_does_not_default_pay_links` line 172: still Null after Stripe+CHIP PUTs, two credential rows. | **Lock inverted on purpose.** |
| “Cannot stage CHIP while Stripe remains active” | You can PUT CHIP and PUT Stripe; mint either; start uses the minted name. | **016 product law retired.** |

**Can you mint Stripe while CHIP is “active”?** There is no active rail. Sequence that 016 forbade is now the happy path: PUT chip, PUT stripe, `POST /v1/checkouts` or `/v1/payment-links` with `provider: "stripe"` succeeds if the Stripe row exists. Inverse: Stripe vaulted, CHIP mint 400 `"rail not configured"` (`CheckoutTests.Create_unconfigured_rail_is_400`) — mint requires **that** rail’s keys, not “whatever was last pasted.”

**Saving a vault must not pick a default (README) — true on the host.** PUT does not write a default. List after two PUTs still has `ActiveProvider` null. Start will 503 if you somehow minted a checkout with null Provider (no ActiveProvider rescue).

**Merchant is not a second default, but it has a UX default:** the mint `<Select>` initial state is Test, and `withTest` keeps Test in the list, so the dialog opens on Test even when Stripe is on file. That is a picker default, not an org_settings default. It does **not** make PUT pick a rail. It **does** mean staff can mint Test without intending to. See Bugs.

**016 GET `/gateway` as a single `GatewayView` is broken for old clients.** Empty GET now returns `{ org_id, processors }`. `packages/pay-spec/main.tsp:172-181` still documents GET `/orgs/{orgId}/gateway` → `GatewayView` (single), no `/gateways`, no `?provider=`, no `test`, no `key_id`/`key_secret`, and `CreateCheckoutRequest` has **no** `provider`. Paper 08 owns the spec; named here because independent vault is invisible in the contract.

---

## Bugs

A bug is live behavior that contradicts the vault/mint/Test law, or that can book cash / leak Test / lose a rail without the staff meaning to.

### B1 — Test is allowed in every non-Production environment, including Staging

`PayProviders.AllowsTest` is `!env.IsProduction()` (`PayProviders.cs:21-22`). Development, Staging, Testing, and any mis-set `ASPNETCORE_ENVIRONMENT` that is not the literal `Production` get:

- Test in `GET /gateways`
- Mint Test without keys
- Start that **marks paid and writes `RCPT-` with no PSP**
- Unsigned `POST /v1/webhooks/test/{orgId}` that fulfills any open Test checkout whose id is in the JSON

Production host: mint 400 `"test processor is not enabled"`; list omits Test; start throws `"rail not configured"` → 503; webhook 400 `"rail not configured"`. That door is real **only** if the process name is `Production`.

**Why it matters:** Staging is the environment that looks like production, is on the public internet, and often is **not** `IsProduction()`. Test start is a public, unauthenticated fulfill. Combined with B2 (unsigned webhook), a Staging deploy is a free Official Receipt printer.

**How to solve:** Change `AllowsTest` to `env.IsDevelopment() || env.IsEnvironment("Testing")` (or an explicit `Pay:AllowTestProcessor` that defaults false). Fail closed for Staging. Add a host test with `UseEnvironment("Staging")` and `UseEnvironment("Production")` that mint/start/webhook Test are 400/503. Do not treat “we won’t set Staging” as the control.

### B2 — Test webhook has no authenticator

`TestWebhook.Parse` (`TestWebhook.cs:8-59`) parses JSON and returns a `PspParseResult`. No HMAC, no shared secret, no loopback check. `WebhookEndpoints.Handle` skips `GatewayCredentials` for Test (`WebhookEndpoints.cs:50-56`). Anyone who can POST to the host and guess `{orgId, checkoutId}` (checkout ids are 32-hex GUIDs; org ids are One tenant ids, often in the public webhook URL staff copy from the Processor dialog) can fulfill an **open** Test checkout.

Start already auto-fulfills, so the webhook is a **second** unsigned door for checkouts that were minted but not started.

Missing `id` mints a fresh event id (`TestWebhook.cs:27-29`), so duplicate detection does not bind replays without a client-supplied id. After paid, `Fulfillment` no-ops on non-open, so this is not double-pay — it is an unauthenticated pay.

**How to solve:** Pick one:

1. **Delete the Test webhook path.** Test fulfills only on start (already true for the buyer). `WebhookEndpoints` returns 400 for `test`. Drop `TestRailTests.Webhook_pays_open_test_checkout` or retarget it to 400.
2. If you keep Plane B for Test, require a process secret (`Pay:TestWebhookSecret`) or bind the route to loopback, and require a stable `id`.

Unsigned Plane B for a rail that writes `RCPT-` is not “local only” once the host is reachable.

### B3 — Merchant always offers Test, even when the host omitted it

Host `Listed` omits Test in Production (`PayProviders.cs:18-19`). Merchant does not care:

- `processors.ts` `rails` always includes `'test'`.
- `GatewayPage` draws Test as “Ready” via `const isTest = r === 'test'; const on = isTest || Boolean(row?.configured)` (`GatewayPage.tsx:128-130`) — **Ready even if `/gateways` did not return Test.**
- `CheckoutsPage.withTest` unshifts Test if the API list lacks it (`CheckoutsPage.tsx:32-38`), then the mint select includes it.

A Production-pointed `:5178` (or a built merchant talking to a Production 8081) still shows Test Ready and lets a writer POST `provider: "test"`. Host then 400s `"test processor is not enabled"`. That is a confusing fail, not a silent pay — **unless** B1 also holds (Staging). Together, merchant + Staging is a one-click unpaid receipt.

**How to solve:** Drive the Processor grid and mint select from `GET /gateways` `processors`. Do not hardcode Test as Ready. Delete `withTest`. If the host omits Test, the card and the option disappear. Source-grep lock: `withTest` gone; GatewayPage maps `processors` from the API (still may show Empty cards for the five BYOK names from `All`).

### B4 — Mint dialog defaults to Test even when a real rail is on file

`CheckoutsPage.tsx:109` `useState<Rail | ''>('test')`. After `/gateways` loads:

```134:139:apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx
        setProvider((prev) => {
          if (prev && ready.some((p) => p.provider === prev)) return prev
          const firstReal = ready.find((p) => p.provider !== 'test')?.provider
          const first = firstReal ?? ready[0]?.provider
          return isRail(first) ? first : 'test'
        })
```

`firstReal` is the intended “prefer Stripe if on file.” It is dead on first load: `prev` is `'test'` and `withTest` guarantees Test is in `ready`, so the function returns `'test'`. Staff with Stripe + CHIP on file still mint Test unless they notice the select. README says mint an explicit provider that already has keys; the SPA’s default is the rail that **has no keys**.

This is not PUT picking a default (host law holds). It is the mint door picking Test.

**How to solve:** Initialize `provider` to `''`. In `setProvider`, if any non-test configured rail exists, select `firstReal`; only if the list is Test-only, select Test. Disable Create until a choice is made (`disabled={busy || !provider}` already exists). Add a merchant lock that `firstReal` is used (or a vitest of the helper extracted from the page).

### B5 — Re-saving a non-Billplz vault always writes `environment=test`

Host: omitted `environment` becomes `"test"` and **overwrites** the column (`GatewayEndpoints.cs:76-80`, `117-123`). Billplz is the only rail that **requires** the field (400 if missing) and the only hosted rail that **reads** it (`BillplzHosted.cs:39-41` sandbox vs www).

Merchant `pasteKey` sends `environment` only for Billplz (`GatewayPage.tsx:98-100`). Stripe/CHIP/Xendit/Razorpay re-save therefore reset `Environment` to `test` even if an API client had stored `live`. `openEdit` hydrates `environment` into state (`GatewayPage.tsx:74-78`) and `locks.test.ts` asserts that hydrate — then Save ignores the state for every rail except Billplz.

Today Stripe/CHIP/Xendit/Razorpay hosted code ignores `Environment` (test vs live is the key itself). So this is not a money bug **yet**. It is a foot-gun the moment a rail starts reading the column, and it already lies in GET `environment` after a UI rotation.

**How to solve:** Either (a) stop storing `environment` except for Billplz and stop returning it for others, or (b) send the hydrated value on every Save (even if the select is hidden). Do not default-overwrite on PUT when the body omits the field — keep the existing row’s environment on rotation if you add a partial update. Prefer (a) plus Billplz-only column semantics until another rail needs a host split.

### B6 — PUT accepts any CHIP `webhook_secret`; PEM is only checked at verify

UI is honest: textarea, “PEM from CHIP dashboard” (`GatewayPage.tsx:241-249`, lock in `locks.test.ts:38-43`). Host PUT Protects whatever string (`GatewayTests` and `ChipRailTests` both PUT `"pem"` as the webhook secret and succeed). `ChipWebhook.Parse` `ImportFromPem` at verify; failure is `"invalid signature"` (`ChipWebhook.cs:40-48`) — same copy as a bad RSA sig.

Staff can vault CHIP, mint CHIP, start CHIP, and only discover the PEM is junk when Plane B 400s. 016 wanted Brand ID 400 at PUT (exists: `Chip_put_requires_brand_id`). Symmetric PEM 400 is missing.

**How to solve:** On PUT chip, `RSA.Create().ImportFromPem(webhookSecret)` and 400 `"webhook_secret must be a PEM public key"` on throw. Keep the textarea. Add `Chip_put_requires_pem` next to `Chip_put_requires_brand_id`. Do not add a registrar.

### B7 — 016-era open checkouts with null `Provider` can no longer start

016 start: `row.Provider ?? settings.ActiveProvider`. 018 start: `row.Provider ?? link.Provider`. `ActiveProvider` is unread. Checkouts minted **before** bind-at-mint (Provider null, never started) 503 `"rail not configured"` even if `ActiveProvider` still says `stripe` in Postgres.

Payment links are new in `20260825120000` — no leftover. Direct checkouts from 016 dogfood can still sit in 5435.

**How to solve:** One-time SQL/backfill: `UPDATE checkouts SET "Provider" = s."ActiveProvider" FROM org_settings s WHERE checkouts."OrgId" = s."OrgId" AND checkouts."Provider" IS NULL AND s."ActiveProvider" IS NOT NULL`. Do **not** restore ActiveProvider on the start path (that re-opens one-active-rail). Then stop reading the column; a later migration may drop it (not this paper’s job to implement).

### B8 — Overview counts Test as “On file”

`OverviewPage.tsx:23` `processors.filter((p) => p.configured)`. Test JSON is `configured: true` with no keys. A workspace that has never pasted a secret shows “On file 1” and `Test · webhook on file`. Processor cards say Ready (honest). Overview says On file (not honest).

**How to solve:** Filter `provider !== 'test'` for the On file count, or label Test “Ready (no keys)” separately. Same list the mint dialog uses.

---

## Gaps

A gap is missing product surface, stale docs, leftover schema, or honesty vs spec — not a wrong branch on the live pay path.

### G1 — `ActiveProvider` column kept, unused, still in the snapshot

Four-adapter Up **added** it; nothing in 018 dropped it. `Rows.cs` documents unused. PUT no longer writes it. Tests assert null. Leftover non-null values from 016 deploys are traps for B7 and for any future reader who greps the snapshot and assumes the law is still 016.

**How to solve:** After B7 backfill, add a migration that drops `ActiveProvider`. Until then, Isolation-style grep: Pay path must not read `settings.ActiveProvider`. Do not reintroduce a default rail to “use the column.”

### G2 — No way to un-vault or disable a rail

No DELETE. Mint picker shows every `configured` row forever. Compromised CHIP keys stay mintable until you overwrite them with new secrets (and you must also re-paste PEM).

**How to solve:** `DELETE /v1/orgs/{id}/gateway?provider=` writer-gated, 400 for Test, audit `gateway.credentials.delete`. Or a `disabled` flag. Do not invent a new active_provider to “turn off” a row.

### G3 — PUT is all-or-nothing rotation

Host requires `secret` and `webhook_secret` on every PUT. UI warning: “Webhook secret on file. Saving again requires a fresh value.” (`GatewayPage.tsx:267-271`). You cannot rotate `sk_` without the `whsec_` in the same paste. There is no PATCH.

**How to solve:** If you add rotation, two explicit fields with “leave blank to keep.” Until then, the copy is honest. Do not silently keep old webhook ciphertext when the body omits it — that is how you get empty-column Stripe fallback stories.

### G4 — `environment` is a Billplz-only runtime field stored on every row

CHIP/Xendit/Razorpay/Stripe hosted classes do not read `Environment`. GET still returns `environment: "test"` for those rows (default). Merchant hydrates it and hides the select. Spec `GatewayView.environment` is optional for all.

**How to solve:** Document Billplz-only in GET (omit or null for others) **or** actually use the column where a rail has two hosts. Do not add a factory to “interpret environment.”

### G5 — last4 / webhook_configured / environment hydrate exist but are thinly locked

Live GET returns all three (`GatewayJson`). Merchant shows last4 and webhook on the card; hydrates environment and public_merchant_id in the dialog. Tests:

- `Put_and_get_does_not_echo_secret` asserts `webhook_configured` true, **not** `last4=="ummy"`, **not** `environment=="test"`.
- `Member_can_get_gateway_metadata` asserts no plaintext, **not** last4 present.
- `List_returns_all_five_and_put_does_not_default_pay_links` asserts six processors and configured flags, **not** last4/environment on the Stripe/CHIP objects.
- No test that member last4 equals writer last4.
- No Billplz GET round-trip of `environment: live`.

016 already named last4 `ummy` and environment round-trip as missing. Still missing.

**How to solve:** Extend `Put_and_get_does_not_echo_secret` with `last4=="ummy"`, `environment=="test"`. Add `Billplz_put_round_trips_live_environment`. Assert member GET last4. Do not hide last4 from members — 016 honesty said do not claim member-cannot-see-last4; live is members-see-last4, writers-see-the-same.

### G6 — Secret last4 is the same JSON for members and writers

Intended: members see metadata, cannot PUT (`Member_cannot_put_gateway` 403, GatewayPage hides Edit). last4 of an API secret is a mild identifier. If the product ever wants last4 writer-only, the host must branch `GatewayJson` on role — it does not today.

**How to solve:** Keep shared last4 unless product says otherwise. Do not return webhook last4 (you don’t). Do not return ciphertext (you don’t — locked).

### G7 — CHIP PEM / Billplz X-Signature / Xendit callback token / Razorpay HMAC vs UI

| Rail | UI field | JSON field | Verify | Match? |
|---|---|---|---|---|
| Stripe | `whsec_… (endpoint signing secret)` | `webhook_secret` | `Stripe-Signature` + org `WebhookCiphertext` (Testing process fallback) | Yes |
| CHIP | textarea “PEM from CHIP dashboard” | `webhook_secret` | `X-Signature` RSA-SHA256 PKCS1, `ImportFromPem` | UI match; PUT does not validate (B6) |
| Billplz | `X-Signature secret` | `webhook_secret` | form `x_signature` HMAC-SHA256 (with/without extra fields) | Yes |
| Xendit | `x-callback-token` | `webhook_secret` | header `x-callback-token`, SHA256 then fixed-time | Yes |
| Razorpay | placeholder `"webhook secret"` (generic) | `webhook_secret` | `X-Razorpay-Signature` HMAC-SHA256 hex | Weak copy; crypto is HMAC. Dashboard name is “Webhook secret” so this is tolerable. |
| Razorpay keys | `key_id` + `key_secret` | `secret` as `key_id:key_secret` (host also accepts `KeyId`/`KeySecret` on `PutGatewayRequest`) | Basic auth | Yes. Spec `PutGateway` has only `secret`, not the split fields. |
| CHIP Brand / Billplz Collection | labelled Brand ID / Collection ID | `public_merchant_id` | hosted request | Yes |
| Test | no editor | PUT 400 | n/a | Yes |

**How to solve:** Razorpay placeholder → `HMAC webhook secret (X-Razorpay-Signature)` if you want parity with Stripe/Xendit placeholders. Spec: add `key_id`/`key_secret` optional, `test` in the provider enum, `/gateways` list — paper 08. No registrar, no second field name on the wire (`webhook_secret` stays the one JSON key).

### G8 — pay-spec and README vs live vault

`packages/pay-spec/main.tsp`:

- `CreateCheckoutRequest` has no `provider` (live 400 without it).
- `CheckoutSession` has no `provider`.
- `Gateways.get` returns `GatewayView`, not `{ processors }`.
- No `GET /orgs/{id}/gateways`.
- No payment-links.
- Provider comment is `stripe|chip|billplz|xendit|razorpay` — no `test`.
- `PutGateway` has no `key_id`/`key_secret`.

Host README is closer to live (per-rail paste, vault does not pick a default, explicit provider with keys, Testing-only Stripe process fallback, WrapKey required outside Testing).

`.env.example:8` “Dev has a fallback” **contradicts** `SecretBox` (Development throws). Local `.env` has a WrapKey so `pay:dev` works; a laptop that copies only `.env.example` will 500 on first PUT.

**How to solve:** Fix `.env.example` to match README/code. Spec honesty is paper 08; this slice’s contract holes are listed so 08 does not rediscover them.

### G9 — Test `webhook_configured: true` is a lie about ciphertext

`TestGatewayJson` sets `webhook_configured = true` with no `WebhookCiphertext` and no signing. Processor page hides that line for Test. List/Overview consumers that trust the flag will think Plane B is armed.

**How to solve:** `webhook_configured: false` for Test, or omit the field. `configured: true` can stay (meaning “you may mint this without keys” in non-Production).

### G10 — Merchant `payApi.ts` has no processor client

Pages duplicate `/v1/orgs/${orgId}/gateways` and PUT `/gateway`. Types live in `processors.ts` (`Processor`). Fine for a small SPA. Gap if a third page starts talking to the vault.

**How to solve:** Optional `listGateways` / `putGateway` in `payApi.ts`. Do not generate from Hub types. Isolation already bans `@repo/api-types-ts`.

### G11 — Development boot-migrate vs Production apply

Four-adapter columns apply on Development start (`Program.cs:74-77`). Production must run `task pay:db:migrate`. A Production host against an Initial-only 5435 will fail when EF selects `WebhookCiphertext`. Tests never run the migration files (InMemory `EnsureCreated`).

**How to solve:** Keep Development auto-migrate (019 claim). Production: init container / `pay:db:migrate` before ready. Optional: one Testcontainers test that `MigrateAsync`s `FourAdaptersHostedRails` + `PaymentLinkPayers` on Postgres — paper 09.

### G12 — `List_returns_all_five_…` is six

The test name still says five; body asserts `GetArrayLength() == 6` and finds `test` (`GatewayTests.cs:140-168`). Rename. Not a product bug.

### G13 — Mint payment-link unconfigured rail is untested

Checkout has `Create_unconfigured_rail_is_400`. PaymentLink create uses the same `cred is null` branch (`PaymentLinkEndpoints.cs:65-70`) with **no** twin test. Merchant mint is payment-links. Also missing: payment-link without provider; payment-link Test in a Production-shaped factory.

**How to solve:** Copy the checkout tests onto `/v1/payment-links`.

### G14 — No Production-shaped factory for Test / WrapKey / Stripe fallback

`PayApiFactory` hardcodes `UseEnvironment("Testing")` (`PayApiFactory.cs:35`). Every hermetic test allows Test, git-known wrap, and Stripe process fallback. The fail-closed doors (B1, WrapKey, Stripe 503 without ciphertext) are unit-tested only in `SecretBoxTests.Production_missing_wrap_key_throws` (not through HTTP) and `WebhookTests.Missing_webhook_secret_is_503_when_rail_configured` (Testing + empty process secret, which is not the Production branch of `ResolveSecret`).

**How to solve:** A second factory or `UseEnvironment` parameter: Production, WrapKey set, `Pay:StripeWebhookSecret` set, ciphertext nulled → Stripe webhook 503 (proves fallback off); Production mint Test 400; Staging mint Test 400 after B1.

### G15 — leftover `AddDataProtection()` does not wrap secrets

`Program.cs:36-37` registers DataProtection and `SecretBox`. Nothing uses `IDataProtector`. Vault is hand-rolled AES-GCM. Honest; do not “fix” by importing Hub `AesSecretVault`.

---

## Tests vs missing

### What the suite actually locks (vault / mint / Test)

| Method | File | What it proves for this slice |
|---|---|---|
| `Member_cannot_put_gateway` | GatewayTests | Writer gate. Member 403 on PUT Stripe. |
| `Put_requires_webhook_secret` | GatewayTests | PUT without `webhook_secret` 400. |
| `Put_and_get_does_not_echo_secret` | GatewayTests | PUT/GET no `sk_test_dummy` / `whsec_abc`; `configured`; `provider=stripe`; `hosted_link`; `webhook_configured`; audit upsert; **`ActiveProvider` null**. |
| `Chip_put_requires_brand_id` | GatewayTests | CHIP without `public_merchant_id` 400. |
| `Put_unknown_provider_is_400` | GatewayTests | `paypal` 400. |
| `Member_can_get_gateway_metadata` | GatewayTests | Member GET `?provider=stripe` 200, no plaintext. |
| `List_returns_all_five_and_put_does_not_default_pay_links` | GatewayTests | PUT stripe+chip; GET `/gateways` length 6; stripe/chip configured; xendit not; test configured; bare GET `/gateway` also lists 6; ActiveProvider null; two credential rows. **This is the independent-vault lock.** |
| `Put_test_processor_is_400` | GatewayTests | PUT test 400, copy “does not take secrets”. |
| `Get_unknown_provider_query_is_400` | GatewayTests | `?provider=paypal` 400. |
| `Billplz_put_requires_collection_id` | GatewayTests | Billplz without collection 400 (environment present). |
| `Razorpay_put_requires_key_id_colon_secret` | GatewayTests | `secret: nocolon` 400. |
| `Mint_and_start_pays_without_keys` | TestRailTests | Test mint+start → paid + Official Receipt, 0 PSP HTTP. **Fulfill without secrets.** |
| `Webhook_pays_open_test_checkout` | TestRailTests | Unsigned Test webhook fulfills. **Also documents B2.** |
| `Create_without_provider_is_400` | CheckoutTests | Bind-at-mint: missing provider 400 unknown. |
| `Create_unknown_provider_is_400` | CheckoutTests | paypal 400. |
| `Create_unconfigured_rail_is_400` | CheckoutTests | Stripe vaulted, CHIP mint 400. **Mint requires that rail’s keys.** |
| `Create_test_without_vault_is_201` | CheckoutTests | Test exception: no vault, 201, provider test. |
| `Create_and_get_open_session` | CheckoutTests | Stripe mint stamps `provider=stripe`. |
| `Create_defaults_to_one_payer` etc. | PaymentLinkTests | Payment-link mint with `provider:test` works; CHIP payment-link start after CHIP PUT. **No unconfigured-rail twin.** |
| `Production_missing_wrap_key_throws` | SecretBoxTests | WrapKey required outside Testing (Production). |
| `Testing_allows_dev_wrap_key` | SecretBoxTests | Git-known wrap in Testing. |
| `Missing_webhook_secret_is_503_when_rail_configured` | WebhookTests | Nulled ciphertext + empty process secret → 503 (Testing). |
| `Xendit_paid_and_settled` | XenditRailTests | PUT xendit secret + `webhook_secret` as callback token; header `x-callback-token`. |
| `Razorpay_captured` | RazorpayRailTests | PUT `rzp_test:secret` + HMAC `webhook_secret`; header `X-Razorpay-Signature`. |
| `Billplz_paid_form_and_localhost_blocked` | BillplzRailTests | PUT environment test → sandbox host. |
| `Chip_start_and_paid_webhook` | ChipRailTests | PUT real PEM + Brand ID; `X-Signature`. |
| Isolation `IEnumerable<IHostedRail>` / factory / registrar | IsolationTests | Refuse list still grepped. |
| Merchant locks | locks.test.ts | Cards not org default; Test no editor; mint sends provider; PEM textarea; environment hydrate; `/gateways`. |

### Missing methods (one hole → one test)

1. **PUT stripe then PUT chip then mint stripe 201** — “can you mint Stripe while CHIP is on file.” Logic is implied by unconfigured-rail + two-row list; not named.
2. **Payment-link unconfigured rail 400** — merchant door.
3. **Payment-link missing provider 400.**
4. **Production (or Staging, after B1) mint Test 400** through HTTP, not only `AllowsTest` unit.
5. **Production GET `/gateways` length 5, no `test`.**
6. **GET `?provider=test` in Production → `configured: false`** (live fall-through).
7. **last4 `ummy`** on PUT/GET stripe `sk_test_dummy`.
8. **Razorpay last4 is key_id suffix** (`rzp_test:secret` → `test`).
9. **Billplz `environment: live` GET round-trip** and start hits `www.billplz.com`.
10. **Xendit PUT with `public_merchant_id` 400** (AllowsPublicMerchantId is CHIP/Billplz only). Inverse of CHIP Brand ID.
11. **CHIP PUT non-PEM webhook_secret 400** (after B6).
12. **Member GET last4 present, still no plaintext.**
13. **Development missing WrapKey throws** (SecretBox, env name `Development`) — README law; only Production is tested.
14. **Stripe Testing fallback actually verifies** when ciphertext is null and `Pay:StripeWebhookSecret` is set — today the 503 test zeros the env, so the success fallback is unlocked.
15. **Production Stripe fallback off** when process env is set and ciphertext is null → 503.
16. **Test start in Production 503** if a leftover Test checkout exists.
17. Merchant: extract `withTest`/`firstReal` and unit-test B4 once fixed.

Do not add a factory, a registrar, or an e-mandate test to “cover” these holes.

---

## Ranked findings

Severity is money / Production leak first, then law contradiction, then honesty, then missing tests.

| Rank | ID | Class | Finding | Solve |
|---|---|---|---|---|
| 1 | B1 | Bug | `AllowsTest = !IsProduction()` enables auto-pay Test + unsigned webhook on Staging | Allow Test only in Development/Testing (or explicit flag default false). HTTP tests for Staging/Production. |
| 2 | B2 | Bug | Test webhook is unauthenticated Plane B that writes Official Receipts | Remove Test webhook, or require a secret / loopback. |
| 3 | B3 | Bug | Merchant always Ready/injects Test even when host omitted it | Render from `GET /gateways`. Delete `withTest`. |
| 4 | B4 | Bug | Mint select defaults to Test while real rails are on file (`firstReal` dead) | Empty initial state; prefer first configured non-test. |
| 5 | B7 | Bug | 016 open checkouts with null Provider 503; ActiveProvider unread | Backfill Provider from leftover ActiveProvider once; never read it on start. |
| 6 | B6 | Bug | CHIP PEM not validated at PUT | `ImportFromPem` 400. |
| 7 | B5 | Bug | Non-Billplz Save overwrites `environment` to test | Omit field on PUT keep-or-Billplz-only. |
| 8 | B8 | Bug | Overview “On file” counts Test | Exclude Test from On file. |
| 9 | G1 | Gap | Dead `ActiveProvider` column | Drop after backfill. |
| 10 | G2 | Gap | No un-vault | DELETE or disabled flag. Writer only. |
| 11 | G14 / G13 | Gap | No Production factory; payment-link unconfigured untested | See Tests vs missing. |
| 12 | G5 | Gap | last4 / environment / webhook_configured not asserted | Extend GatewayTests. |
| 13 | G8 | Gap | `.env.example` WrapKey lie; pay-spec still one-active GET | Fix example now; spec in paper 08. |
| 14 | G9 | Gap | Test `webhook_configured: true` | false / omit. |
| 15 | G4 / G7 | Gap | environment Billplz-only; Razorpay webhook placeholder generic | Document or send; tighten placeholder. |
| 16 | G11 | Gap | Four-adapter columns auto-apply only in Development | Keep; Production migrate job. |
| 17 | G3 / G6 / G10 / G12 / G15 | Gap | Full-PUT rotation; shared last4; no payApi helper; stale test name; unused DataProtection | Product/hygiene. Not money. |

**Independent vault law, re-verified:** PUT does **not** flip a single active provider. README “Saving a vault does not pick a default” is **true on the host**. Mint requires an explicit provider that already has keys, with Test as the documented exception when `AllowsTest`. Writer-gated PUT is true. Members see last4, not secrets. WrapKey is AES-GCM; git-known wrap is Testing-only (016 paper that said Development still fell back is **stale**). Stripe `Pay:StripeWebhookSecret` is Testing-only. You **can** mint Stripe while CHIP is vaulted. Development boot `MigrateAsync` does apply the four-adapter columns.

**The holes that make the 018 story weaker than the README:** Test is not “local only” (B1/B2); merchant always offers it (B3); mint UX defaults to it (B4). Host law is fine. The Test rail’s fulfill path is real money in any non-Production process.

---

## Refuse (do not add factory, registrar, e-mandate here)

Do **not** “solve” independent vault by:

- `IEnumerable<IHostedRail>` / `IPaymentGatewayFactory` / `PaymentGatewayFactory` (IsolationTests bans; `Program.cs` stays six `AddScoped` + switch).
- CHIP webhook **registrar** HTTP on PUT (`ChipWebhookRegistrar` grepped out). Staff paste PEM. B6 is validate-on-PUT, not register-on-PUT.
- E-mandate / `force_recurring` / off-session / auto-debit. CHIP start test already asserts body does not contain `force_recurring`. Test rail is hosted_link dogfood, not a mandate.
- A new `org_settings.active_provider` (or renaming it `default_provider`) to “help” the mint dialog. That is 016. Fix B4 in the SPA.
- Hub `AesSecretVault` / `DecryptOrPlaintext` / `Jwt:Secret` wrap. SecretBox stays.
- Process-wide `Pay:XenditCallbackToken` / `Pay:ChipPem` / `Pay:RazorpayWebhookSecret`. Stripe process fallback is Testing-only and must not grow.
- Buyer PSP picker. Bind stays at mint. Public JSON may include `provider` (live does, for Test copy); the buyer still does not choose.
- Porting Hub `TenantPaymentConfiguration` as a second vault table.

Fixes that **are** in-scope for a later implement program: B1–B8, G1–G2, G8 `.env.example`, the missing tests list. Not a seventh rail.

---

## Appendix: quoted evidence

### 016 PUT always flips (historical; not live)

From `plans/016-adapters-check/01-new-host-seams.md:856-897` (quoted in that paper from then-`Gateways/GatewayEndpoints.cs`):

> PUT always writes `OrgSettings.ActiveProvider = provider`. … `settings.ActiveProvider = provider;`

Hermetic lock then: `OrgSettings.ActiveProvider == "stripe"`.

### Live PUT does not flip

```126:129:apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs
        if (await db.OrgSettings.FindAsync([orgId], ct) is null)
        {
            db.OrgSettings.Add(new OrgSettingsRow { OrgId = orgId });
        }
```

```79:82:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.AuditEvents.Any(a => a.Action == "gateway.credentials.upsert" && a.OrgId == "t1"));
        Assert.That(db.OrgSettings.Single().ActiveProvider, Is.Null);
```

```170:174:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.OrgSettings.Single().ActiveProvider, Is.Null);
        Assert.That(db.GatewayCredentials.Count(), Is.EqualTo(2));
```

### GET `/gateway` vs `/gateways`

```14:18:apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs
    public static void MapGateways(this WebApplication app)
    {
        app.MapPut("/v1/orgs/{orgId}/gateway", Put);
        app.MapGet("/v1/orgs/{orgId}/gateway", Get);
        app.MapGet("/v1/orgs/{orgId}/gateways", List);
    }
```

```158:161:apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs
        if (string.IsNullOrWhiteSpace(provider))
        {
            return await List(orgId, request, one, db, env, ct);
        }
```

### README vault law

`apps/lazuar-pay/README.md:65`:

> `owner`/`admin` paste keys **per rail** (stripe, chip, billplz, xendit, razorpay). Saving a vault does not pick a default. Mint a pay link with an explicit `provider` that already has keys.

### Mint requires keys; Test exception

```53:73:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs
        if (!PayProviders.TryNormalize(body.Provider, out var provider))
        {
            return PayErrors.Status(400, "Bad Request", "unknown provider");
        }

        if (PayProviders.IsTest(provider))
        {
            if (!PayProviders.AllowsTest(env))
            {
                return PayErrors.Status(400, "Bad Request", "test processor is not enabled");
            }
        }
        else
        {
            var cred = await db.GatewayCredentials.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrgId == orgId && x.Provider == provider, cancellationToken);
            if (cred is null)
            {
                return PayErrors.Status(400, "Bad Request", "rail not configured");
            }
        }
```

Payment links duplicate that block (`PaymentLinkEndpoints.cs:51-71`).

### Writer-gated PUT

```45:69:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs
    public static async Task<IResult?> RequireWriterAsync(...)
    {
        var denied = await RequireMemberAsync(...);
        // ...
        var role = who.Value.Tenants.FirstOrDefault(t => t.Id == orgId)?.Role;
        if (role is not ("owner" or "admin"))
        {
            return PayErrors.Status(403, "Forbidden", "Writer role required");
        }
```

### Test auto-fulfill and unsigned webhook

`TestHosted.cs:11-20` — success URL, no secrets.

`PublicPayEndpoints.cs:176-186` — start calls `FulfillPaidAsync` for Test.

`TestWebhook.cs:8-59` — JSON only.

`WebhookEndpoints.cs:50-56` — Test skips credentials, `AllowsTest` or 400.

### Merchant “does not pick the rail”

```121:123:apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx
      <PageHeader
        title="Processor"
        subtitle="Vault keys per rail. Saving a secret does not pick the rail for pay links."
      />
```

```51:57:apps/lazuar-pay-merchant/src/locks.test.ts
  it('processor vault is cards not an org default rail', () => {
    const src = readFileSync(join(root, 'src', 'pages', 'org', 'GatewayPage.tsx'), 'utf8')
    expect(src).toContain('CardTitle')
    expect(src).not.toContain('aspect-square')
    expect(src).not.toContain('One active rail')
    expect(src).toContain('does not pick the rail for pay links')
  })
```

### Merchant always-Test inject

```30:38:apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx
const testProcessor: Processor = { provider: 'test', configured: true }

function withTest(list: Processor[]): Processor[] {
  const ready = list.filter((p) => p.configured && isRail(p.provider))
  if (!ready.some((p) => p.provider === 'test')) {
    ready.unshift(testProcessor)
  }
  return ready
}
```

### Development four-adapter apply

```74:77:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<PayDbContext>().Database.MigrateAsync();
}
```

### WrapKey Testing-only; Stripe process fallback Testing-only

`SecretBox.cs:38-46` — throw unless Testing.

`StripeWebhook.cs:85-90` — process env only if Testing.

`apps/lazuar-pay/README.md:67`:

> Process `Pay__StripeWebhookSecret` is a **Testing-only** fallback. … `Pay__WrapKey` is required outside Testing.

`.env.example:8` (stale):

> `# 32-byte base64 wrap key for BYOK. Dev has a fallback; production must set this.`

### pay-spec still one-rail GET

```172:181:packages/pay-spec/main.tsp
@tag("Gateways")
interface Gateways {
  /** BYOK keys. provider is stripe|chip|billplz|xendit|razorpay. Writer only. */
  @put
  @route("/orgs/{orgId}/gateway")
  put(@path orgId: string, @body body: PutGateway): GatewayView;

  @get
  @route("/orgs/{orgId}/gateway")
  get(@path orgId: string): GatewayView;
}
```

Live GET without query is a list. Live mint requires `provider`. Live sixth name is `test`.

---

**End of 04.** Live files on `9f04ad58` are the authority for every verdict above. 016 “PUT always flips `active_provider`” is **false** on this SHA. 018 independent vault + bind-at-mint is **true** on the host. Test can fulfill without secrets. Test can leak into any process that is not named Production, and the merchant UI will offer it anyway. Do not implement from this paper.