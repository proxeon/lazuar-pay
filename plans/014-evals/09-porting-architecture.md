# 09 — Porting architecture: how new Pay takes Hub rails as HTTP judgment, not as a factory of five

**Family:** 014-evals  
**Paper:** 09 — the proposed seam on 8081  
**Date:** 24 August 2026  
**Type:** Uncondensed design. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) cells. **Not** a project reference into `apps/lazuar-api`. **Not** a rewrite of Pay in Go.

**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Branch:** `main`  
**HEAD:** `ee2db8e5` — `feat(pay): Bar B receipts, webhook secret, merchant money UI`

Parent index: [README.md](./README.md). Sibling evidence (do not treat this paper as a substitute): [01-new-pay-host.md](./01-new-pay-host.md), [04-old-adapter-seam.md](./04-old-adapter-seam.md), [05-stripe-port.md](./05-stripe-port.md), [06-malaysia-rails.md](./06-malaysia-rails.md), [07-sea-later-rails.md](./07-sea-later-rails.md), [08-webhooks-secrets-fulfillment.md](./08-webhooks-secrets-fulfillment.md). Those papers describe **current state** and **per-PSP HTTP**. This paper designs **how a later implementer adds CHIP or Billplz** without cloning `IPaymentGatewayAdapter` / `PaymentGatewayFactory` / MediatR.

Historical papers this one updates against live files: [011/01-product.md](../011-new-lazuar-pay/01-product.md) wrap-rails, [011/04-linux-shape.md](../011-new-lazuar-pay/04-linux-shape.md) “call the function”, [013-prods/06-money-rails.md](../013-prods/06-money-rails.md) §5–§6 (sketched before StripeHosted existed), [013-prods/checklists/g10-pick-rail.md](../013-prods/checklists/g10-pick-rail.md) through [g26-pay-spec-webhooks.md](../013-prods/checklists/g26-pay-spec-webhooks.md), [013-prods/checklists/decisions.md](../013-prods/checklists/decisions.md). When 013 sketches disagree with `apps/lazuar-pay/src/Lazuar.Pay/Gateways/` on this SHA, **the live files win**.

---

## 0. The belief, answered in one page

> “I think we can implement these adapters into our new lazuar-pay implementation.”

**Yes**, as HTTP judgment, **one rail at a time**, inside `apps/lazuar-pay/src/Lazuar.Pay/Gateways/`. Stripe is already there as `StripeHosted`. The next commit that earns the word “adapter” is **one Malaysian hosted rail** (CHIP, locked as next in Bar B `decisions.md`; Billplz only if that lock is amended). The commit after that is not Xendit.

**No**, as a copy of `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/` plus a factory of five. That folder is a cashier module: five classes on `IPaymentGatewayAdapter`, `PaymentGatewayFactory.GetAdapter`, MediatR `ProcessGatewayWebhookCommand`, outbox events, `TenantPaymentConfiguration` in a `payments` schema, CHIP registrar on key save, Billplz `PublicDnsFallback` on a named HttpClient. IsolationTests exist so that shape cannot sneak into 8081. Copying it would compile in Hub and **fail** `Lazuar.Pay.Tests.IsolationTests`.

The rest of this paper is the seam that makes the Yes cheap and the No enforceable.

| Claim | Verdict | Where it lives |
|-------|---------|----------------|
| Port Stripe/CHIP/Billplz **HTTP** (hosted URL + verify + event id) | **Yes** | New files next to `StripeHosted.cs` |
| Port `IPaymentGatewayAdapter` (five methods, uppercase `STRIPE`) | **No** | Do not create this type in Pay |
| Port `PaymentGatewayFactory` + `IEnumerable<IPaymentGatewayAdapter>` | **No** | That is how day-one became five |
| Project-reference `Modules.Payments.*` | **No** | IsolationTests ban `lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR` |
| Shared kernel NuGet of adapters | **No** | Same cathedral, different package |
| Rewrite Pay in Go to “do adapters right” | **No** | [011/05](../011-new-lazuar-pay/05-language.md) said Go for a **new** kernel. This tree is C#. IsolationTests, `PayDbContext`, Stripe.net, and `:8081` already exist. Bar B lock: “Not a Go rewrite in this program.” |
| Add CHIP **and** Billplz **and** Xendit **and** Razorpay in one PR | **No** | One Malaysian rail. SEA later. |
| Add `ChargeOffSession` / `GenerateCustomerPortal` on day one of CHIP | **No** | Stripe dogfood is hosted link, capability `"hosted_link"`. Off-session is a later verb, and only for Stripe/CHIP. |
| Silent `ChipWebhookRegistrar` on boot or on PUT | **No** | Merchant action or dashboard paste. |
| Port `PublicDnsFallback` “just in case” | **No** | Only if the chosen MY rail still cannot resolve its host from this machine. |

---

## 1. What this paper is for

A later implementer should be able to open this file and add CHIP (or Billplz) without asking “where does the factory go?” The answer is: **it does not go**. They add one class, one allow-list token, one switch arm, one secret shape, one test file copied from `WebhookTests.cs`, and they grow `packages/pay-spec/main.tsp` by the fields that PUT actually stores. They call `Fulfillment.FulfillPaidAsync` — they do not journal inside the rail class.

If the PR also adds `IPaymentGatewayAdapter`, `PaymentGatewayFactory`, `AddPaymentsModule`, `GatewayPaymentCompletedIntegrationEvent`, or a csproj `ProjectReference` to anything under `apps/lazuar-api`, it has failed this paper regardless of whether CHIP returns a URL.

### 1.1 Standing law (do not weaken)

From 011, 012, 013 Bar B, IsolationTests, and the 014 README:

1. **IsolationTests:** host and test csproj + `src/**/*.cs` must not contain `MediatR`, `BuildingBlocks`, `Modules.`, `lazuar-api`. Vite apps must not depend on `@repo/api-types-ts`. No csproj may `ProjectReference` `apps/lazuar-api`.
2. **One process, function calls, public `/v1`.** The PSP POST that verified is the process that books cash. No Payments outbox. No Commerce inbox. No “wait for One to hear `payment.succeeded`.”
3. **One dogfood rail first.** Stripe already exists on 8081 (`StripeHosted`, capability `"hosted_link"`). Next = **one** Malaysian rail. Razorpay / Xendit stay later.
4. **Wrap-rails matrix lives in new Pay as a small honest function**, not a project ref to `Modules.Payments.Contracts.PaymentGatewayCapabilities`.
5. **Same-handler fulfillment.** Adapters return a hosted URL or a parsed verified event. They do not insert `charges`, `journal_lines`, or `RCPT-`.
6. **Do not add Hub’s five-method port** (`ChargeOffSession`, `GenerateCustomerPortal`, `IssueRefund` as required methods) on day one of the second rail unless Stripe dogfood needs them. Live Stripe dogfood does **not**: `StripeHosted` has one method, `CreateHostedUrlAsync`. Webhook parsing is inline in `WebhookEndpoints`. Capability is `"hosted_link"`.

### 1.2 What “one Malaysian rail” is, on this SHA

[013 decisions.md](../013-prods/checklists/decisions.md) locked:

> First rail = **Stripe**. CHIP is the next Malaysian rail, not this Bar B. Billplz is reminder-only — not first.

This paper does **not** reopen G10. The implementer of the second rail writes **CHIP** unless B00 is amended in writing to Billplz. If they amend to Billplz, the seam below still holds; only the HTTP extract and the secret fields change. They do not implement both “while we are here,” and they do not leave a stub `BillplzHosted` registered but unused.

CHIP vs Billplz HTTP details are sibling papers 06/07. This paper only names what the **host** must grow so either class can sit next to `StripeHosted`.

---

## 2. Live new host — the seam that already exists

Authority: files under `apps/lazuar-pay/src/Lazuar.Pay/` on `ee2db8e5`. 013 §5–§6 described a host that had **no** Stripe.net, **no** webhook route, **no** `gateway_credentials` table, and an in-memory checkout fixture. That sketch is **historical**. Live:

### 2.1 Folder

```
apps/lazuar-pay/src/Lazuar.Pay/Gateways/
  GatewayEndpoints.cs     PUT/GET /v1/orgs/{orgId}/gateway
  StripeHosted.cs         CreateHostedUrlAsync only
  WebhookEndpoints.cs     POST /v1/webhooks/{provider}/{orgId} — Stripe only
```

There is no `IPaymentGatewayAdapter`. There is no factory. There is no `ChipHosted`. `Program.cs` registers the concrete type:

```csharp
builder.Services.AddScoped<StripeHosted>();
builder.Services.AddScoped<Fulfillment>();
```

`PublicPayEndpoints.Start` takes `StripeHosted stripe` as a parameter and always calls it. That is the whole dispatch story today: **there is no dispatch**. One rail, one class, one constructor argument.

### 2.2 `StripeHosted` — the shape to copy, not Hub’s adapter

Live class (`Gateways/StripeHosted.cs`):

- `public const string Provider = "stripe";` — **lowercase**. Hub used `"STRIPE"`. New Pay stays lowercase in the path, the credential PK, and the JSON.
- One constructor: `(PayDbContext db, SecretBox box)`.
- One method: `CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)`.
- Loads `gateway_credentials` for `(checkout.OrgId, "stripe")`, `SecretBox.Unprotect`, Stripe.net `SessionService.CreateAsync`.
- `Mode = "payment"` only. Not `subscription`. Not `setup`.
- Stamps `ClientReferenceId = checkout.Id` and metadata `checkout_id`, `org_id`.
- Returns `session.Url` or throws `InvalidOperationException("Stripe returned no URL")` / `"rail not configured"`.
- Does **not** parse webhooks. Does **not** refund. Does **not** charge off-session. Does **not** open Billing Portal. Does **not** journal.

That is already smaller than Hub’s `StripeGatewayAdapter` (which implements five interface methods, expands `latest_charge.balance_transaction` for fees, maps setup to `PAYMENT_COMPLETED`, and throws or no-ops on portal/off-session depending on the rail). **CHIP’s first class in this folder should look like `StripeHosted`, not like `ChipCollectGatewayAdapter`.**

### 2.3 Gateway HTTP — allow-list of one

`GatewayEndpoints.Put`:

- `MemberGate.RequireWriterAsync` (owner/admin). Members 403.
- `provider` lowercased. If not `StripeHosted.Provider` → **400** `"Bar B first rail is stripe"`.
- `secret` required. Wrapped with `SecretBox.Protect`. Last4 stored.
- PK `(orgId, provider)` on `gateway_credentials`.
- Returns `{ org_id, provider, last4, capability: "hosted_link" }`.

`GatewayEndpoints.Get`:

- `RequireMemberAsync`.
- **Always** `FindAsync([orgId, StripeHosted.Provider])`. It cannot describe a CHIP row even if one were inserted by hand.
- Missing → `{ configured: false, provider: "stripe" }`. Not a fake Billplz row. Keep that honesty.

`PutGatewayRequest` is `{ Provider, Secret }`. No `webhook_secret`. No `brand_id` / `collection_id`. No `environment`.

### 2.4 Webhook HTTP — Stripe inline, process-level `whsec`

`WebhookEndpoints.Handle`:

1. Provider must be `"stripe"` else **400** `"unknown provider"`.
2. Raw body string. Empty → **400** `"empty body"`.
3. Org must have a **stripe** credential row else **400** `"rail not configured"`.
4. Signing secret from **`config["Pay:StripeWebhookSecret"]`**, not from the credential row. Missing → **503**.
5. `Stripe-Signature` + `EventUtility.ValidateSignature` / `ConstructEvent(..., throwOnApiVersionMismatch: false)`. Fail → **400**.
6. Unique `(orgId, "stripe", stripeEvent.Id)` in `psp_webhook_events`. Hit → **200** `{ duplicate: true }`.
7. Insert the row, `SaveChanges`, **then** if `checkout.session.completed` and not setup/zero, `fulfillment.FulfillPaidAsync(checkoutId, "stripe", session.Id, ct)`.
8. Setup or `AmountTotal` 0 → **200** `{ ignored: "setup_or_zero" }` — after the idempotency insert. Good: retry will no-op.

This is the same-handler rule, live. It is also the **first thing to harden before CHIP**: the webhook secret is a **process env var shared by every org**. That is compatible with a single dogfood Stripe account. It is **incompatible with BYOK**. CHIP’s PEM is per brand. Billplz’s X-Signature is per collection. Stripe Connect-less BYOK means each merchant’s Dashboard endpoint has its own `whsec_`. Sequence step 0 is: **move Stripe’s webhook secret onto the credential row** (or a sibling ciphertext column) so the second rail does not invent a second secret story.

Hub signature-fail was 500. Live Pay is 400. Keep 400.

Hub empty-body used to be 500. Live Pay is 400 (`PublicPayTests.Empty_webhook_is_400`). Keep 400.

### 2.5 Fulfillment is already a function

`Money/Fulfillment.cs` `FulfillPaidAsync(checkoutId, provider, providerRef, ct)`:

- One DB transaction.
- No-op if checkout missing, amount ≤ 0, or status ≠ `open`.
- SST unknown → throw (fail closed).
- Marks `paid`, inserts charge (with `Provider` + `ProviderRef`), optional payer, optional subscription for `mo`/`yr`, journal cash/revenue, `RCPT-{MYT year}-#####`, audit `checkout.paid`.

The rail must not grow a second copy of this. CHIP’s parse returns a checkout id and a purchase id; the handler calls the same method with `provider: "chip"`.

### 2.6 Credentials table is too thin for a second rail

`GatewayCredentialRow` / migration `20260821152601_Initial`:

| Column | Live | Second rail needs |
|--------|------|-------------------|
| `OrgId` + `Provider` PK | yes | keep |
| `Ciphertext` | API key only | keep (Stripe `sk_`, CHIP Bearer, Billplz secret) |
| `Last4` | yes | keep, of the API key |
| `UpdatedAt` | yes | keep |
| webhook secret | **missing** (process `Pay:StripeWebhookSecret`) | **required** per org before CHIP |
| CHIP Brand ID / Billplz Collection ID | **missing** | **required** for that rail’s generate |
| `environment` test\|live | **missing** | add **when Billplz** (sandbox host). Do not add “for Stripe” — prefix is enough. CHIP test is a dashboard toggle on the same API host. |
| `is_active` | **missing** | optional later. Missing keys already fail generate. Soft-disable is Hub chrome, not S1. |

Do not copy Hub `TenantPaymentConfiguration` as a type. Steal the **three secrets**: api key, webhook secret, public merchant id. Encrypt the first two. Leave Brand/Collection plaintext (they are not secrets; GET may show them).

### 2.7 IsolationTests — the tripwire that makes this paper enforceable

`tests/Lazuar.Pay.Tests/IsolationTests.cs` bans:

- Tokens in csproj: `lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api`
- Source under `src/`: `MediatR`, `Modules.One`, `BuildingBlocks`
- `ToTable("organizations"|"users"|"members")`
- Vite `@repo/api-types-ts`
- Any `*.csproj` under `apps/lazuar-pay` containing `apps/lazuar-api`

It does **not** currently grep for `IPaymentGatewayAdapter` or `PaymentGatewayFactory`. A well-meaning port could recreate those names inside `Lazuar.Pay.Gateways` and IsolationTests would stay green. **This paper forbids the types anyway.** Optional later lock: IsolationTests assert `src/` does not contain `IPaymentGatewayAdapter`, `PaymentGatewayFactory`, `IPaymentGatewayFactory`, `AddPaymentsModule`. Recommend adding that grep in the same PR as CHIP, not as a reason to delay CHIP.

Host csproj live packages: `Npgsql.EntityFrameworkCore.PostgreSQL`, `Stripe.net`, EF Design. **No** CHIP/Billplz NuGet — they are raw HTTP. Do not add Razorpay’s official SDK “for later.”

### 2.8 Tests that already exist (the pattern to copy)

`WebhookTests.cs`:

- `SeedRailAndCheckout`: PUT stripe secret + POST checkout, returns checkout id.
- Missing process webhook secret → **503**.
- Bad `Stripe-Signature` → **400**.
- Signed `checkout.session.completed` with `client_reference_id` = checkout id → **200**, one `RCPT-`, balanced journal; replay → **200** `duplicate`, still one document.

`PublicPayTests.Empty_webhook_is_400`.

**Honesty gap vs G22 / G25:** there is **no** hermetic test that a Stripe `mode=setup` / amount 0 payload does **not** call fulfill. `WebhookEndpoints` implements the branch (`ignored = "setup_or_zero"`). The test is missing. Sequence step 0 includes adding that Stripe test **before** claiming CHIP inherited G22. Do not tick CHIP “setup not paid” by pointing at Hub.

No dedicated `GatewayTests.cs`. CatalogTests cover member-cannot-create-product; gateway PUT writer-gate is exercised only as a setup step inside webhook tests. When CHIP lands, add: unknown provider 400; `member` PUT 403; CHIP PUT without Brand ID 400.

### 2.9 Merchant UI is Stripe-hardcoded

`apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx` `pasteKey()` always `JSON.stringify({ provider: 'stripe', secret: sk })`. Heading is “Stripe keys”. Adding CHIP without a provider picker will silently keep saving stripe. The Vite change is in scope of the CHIP slice **as a picker of two names**, not as Hub’s five-option dropdown. Do not add Razorpay to the `<select>`.

### 2.10 pay-spec is behind the host on keys, ahead of the host on “any provider”

`packages/pay-spec/main.tsp`:

- Has `POST /v1/webhooks/{provider}/{orgId}` (string, not enum) and `POST /v1/one/webhooks`.
- Has **no** `PUT`/`GET /v1/orgs/{orgId}/gateway`.
- Has **no** `StartPayRequest` payer fields (host has them).
- Comment still says checkout is a fixture — live is Postgres.

When adding a second provider, grow the spec so it **names** the allow-list (`stripe` | `chip`), documents that unknown provider is 400, and adds the gateway PUT/GET models the host already serves. Do **not** publish Hub’s five names in TypeSpec so OpenAPI looks complete.

---

## 3. Live Hub — what not to clone (names, not a dump)

Path: `apps/lazuar-api/Modules/Payments/`. README on this tree still says the module is **not** a fulfillment engine and **not** an accounting ledger. New Pay’s webhook **is** fulfillment. Copying the README’s architecture is the failure mode.

### 3.1 The port that is too big

`Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs`:

| Method | Hub job | New Pay first CHIP/Billplz |
|--------|---------|----------------------------|
| `GatewayType` | `"STRIPE"` / `"CHIP"` / … uppercase | lowercase const on the class |
| `GenerateCheckoutAsync` (11 args including `setupFutureUsage`, `merchantId`, `quantity`, `Guid tenantId`) | hosted URL | **Yes, thinner:** checkout row + secrets → URL |
| `ParseWebhookAsync` (returns `GatewayWebhookParsedResult` with fee, tax, fx, net, customer, token, `UnusableAfterVerify`) | cashier parse + fee math | **Yes, thinner:** verify + kind + event id + checkout id + amount + currency |
| `IssueRefundAsync` → `bool` | later | **No** on day one of CHIP |
| `GenerateCustomerPortalAsync` | Stripe Billing Portal; CHIP/Billplz throw | **No.** Not v1. Buyer magic-link is a different product. |
| `ChargeOffSessionAsync` (11 args, dunning campaign, tax type) | Stripe PI / CHIP `purchases/{id}/charge/` | **No** on day one. Only after hosted is boring, and only Stripe/CHIP. Billplz must never grow this method. |

`GatewayWebhookParsedResult` stuffs setup into `PAYMENT_COMPLETED` with amount 0. New Pay must not copy that event name. Live Pay already uses `{ ignored: "setup_or_zero" }` / `{ ok: true }` / `{ duplicate: true }`. Keep kinds as **paid | failed | ignored | vaulted**, never Hub’s `PAYMENT_COMPLETED` for a setup session.

`IPaymentGatewayFactory` + `PaymentGatewayFactory`:

```csharp
public IPaymentGatewayAdapter GetAdapter(string gatewayType)
{
    var normalizedType = gatewayType.ToUpperInvariant();
    var adapter = _adapters.FirstOrDefault(a => a.GatewayType == normalizedType);
    if (adapter == null) throw new InvalidOperationException(...);
    return adapter;
}
```

Registered in `AddPaymentsModule` as five `AddScoped<IPaymentGatewayAdapter, …>` plus the factory. **That registration list is the product lie.** Adding Xendit was a DI line. New Pay must make adding Xendit a **product decision** (allow-list, spec, tests, wrap copy), not a DI line.

### 3.2 Capabilities — steal four booleans, not the Contracts project

`PaymentGatewayCapabilities` (live, honest on the axes that have readers):

- `SupportsOffSession` = `STRIPE` or `CHIP`
- `IsReminderOnlyGateway` = inverse
- `SupportsApiRefund` = Stripe, CHIP, Razorpay, Xendit (not Billplz)
- `RequiresMarkRefunded` = Billplz + offline names
- `SupportsEmandate` = **always false**
- `SupportsDuitNowQr` / `SupportsHostedWallet` = **unread by generate**. Do not promote into Pay product chrome.

New Pay rewrite (lowercase, same folder as rails, **not** a class library):

```text
WrapRails.SupportsOffSession("stripe"|"chip") → true
WrapRails.SupportsOffSession("billplz"|"razorpay"|"xendit"|unknown) → false
WrapRails.SupportsEmandate(_) → false
WrapRails.Capability(provider) for Bar B/C hosted dogfood → "hosted_link"
  (even on Stripe/CHIP until a real PM / recurring_token exists)
```

GET gateway already returns `capability = "hosted_link"`. Keep returning that until off-session ships. Do not return `vaulted_autocharge` because the class *could* vault. Hub’s unread DuitNow flag is how a UI lies.

### 3.3 Hub extras that look reusable and are not

| Type | Temptation | New Pay |
|------|------------|---------|
| `GatewayCommon` | email defaults, minor units, fee stamps, paying-tenant metadata | Steal **ToMinorUnits** (half away from zero; zero-decimal ISO) as ~20 lines in Pay. Do **not** copy `PlaceholderEmail = "customer@example.com"` — CHIP/Billplz must fail without a real payer email. Do not copy `ApplyPayingTenantMetadata` / `platform_tenant_id` — new Pay has no system org. |
| `ChipWebhookRegistrar` | auto PEM on key save | **No silent registrar.** §11. |
| `PublicDnsFallback` | 1.1.1.1 connect hook | **No** unless Billplz is the rail **and** this machine still cannot resolve `www.billplz-sandbox.com`. §10. |
| `BillplzPublicBase` | fail closed on localhost callback | Steal the **rule** when Billplz lands: public HTTPS or documented tunnel. Do not copy Hub hostname folklore. |
| `CheckoutSessionCashier` last-resort `"BILLPLZ"` | “always mint something” | **Refuse.** Missing keys → 503/409, not a surprise bill. |
| `AesSecretVault` + `Jwt:Secret` fallback + `DecryptOrPlaintext` | “we already have encryption” | Pay already has `SecretBox` AES-GCM + `Pay:WrapKey`. Use it. Never fall back to Hub JWT secret. Never send undecryptable ciphertext to a PSP. |
| `ProcessGatewayWebhookCommandHandler` + outbox requeue | “idempotency is hard” | Live Pay already inserts `psp_webhook_events` and calls `Fulfillment`. Duplicate is 200. Do not copy `HandleExistingLogAsync`. |
| `IntegrationCheckoutSessions` | “Billplz strips metadata” | Pay already has `checkouts` with `PspRedirectUrl`. Persist CHIP purchase id / Billplz bill id on the **same row** (column `provider_session_id` if missing — today `StripeHosted` does not persist session id on the checkout; webhook uses `ClientReferenceId`. CHIP should persist purchase id at generate time). |

### 3.4 Hub DI that IsolationTests will catch if copied, and will **not** catch if reimplemented

`Modules/Payments/Infrastructure/DependencyInjection.cs` `AddPaymentsModule`: PaymentsDbContext (`payments` schema), five adapters, factory, named HttpClient with `PublicDnsFallback.ConnectAsync`, `AddModuleOutboxInbox`, refund/off-session/integration event handlers.

A literal copy fails IsolationTests (`BuildingBlocks`, `Modules.Payments`). A “clean room” reimplementation of the same graph inside `Lazuar.Pay` would **pass** IsolationTests and still be the cathedral. Review the PR as a **graph**: if the new code has a factory that can resolve a name that has no hosted dogfood, no wrap copy, and no WebhookTests, reject it.

---

## 4. Proposed Pay provider interface — smaller than Hub’s

### 4.1 Do we need an interface at all?

Today: no. `StripeHosted` is a concrete class. That is the correct number of abstractions for one rail.

The moment `PublicPayEndpoints.Start` and `WebhookEndpoints.Handle` must talk to **two** classes, a **small** interface (or a two-arm switch on concretes) earns its keep. The Hub interface earned five methods by pretending Billplz could `ChargeOffSession`. We will not.

**Pick:** introduce `IHostedRail` in `Lazuar.Pay.Gateways` **in the same PR as the second rail**, not before. Do not add it “so Stripe is consistent” in a refactor-only PR. Stripe can stay concrete until CHIP compiles.

### 4.2 The two verbs

Design (not code to paste as-is):

**`IHostedRail`**

- `string Provider { get; }` — lowercase `"stripe"` / `"chip"` / `"billplz"`.
- `Task<string> CreateHostedUrlAsync(CheckoutRow checkout, RailSecrets secrets, CancellationToken ct)` — returns the PSP hosted URL. Throws `InvalidOperationException` with a stable message (`"rail not configured"`, `"payer email required"`, `"brand_id required"`) that the HTTP layer maps to 400/503. Does **not** catch-and-return Hub’s `GatewayCheckoutResult.Success=false` record unless we want Result types everywhere; live StripeHosted **throws**, PublicPay maps to 503. **Keep throw** so CHIP matches Stripe, not Hub’s Result monad.
- `ParsedPspEvent ParseWebhook(RawPspWebhook inbound, RailSecrets secrets)` — **synchronous** for Stripe `EventUtility`, CHIP RSA, Billplz HMAC. All three Hub parsers are CPU + crypto on the raw body; Hub’s Stripe parse is async only because it **then HTTP-gets** the PaymentIntent for fees. New Pay does **not** expand fees on day one of CHIP (`unknown` ≠ 0; fulfillment books `checkout.Amount`). If Stripe later grows fee expand, that is an extra call in the **Stripe** class after parse, not a reason to make Billplz parse `async`.

Do **not** put on this interface in the CHIP PR:

- `IssueRefundAsync`
- `ChargeOffSessionAsync`
- `GenerateCustomerPortalAsync`
- `setupFutureUsage` / `quantity` / `Guid tenantId` / `estimatedFeePercentage`

Refund is a later interface or a later method **on the rails that support it** (`IRefundRail` with Stripe + CHIP only). Billplz never implements it; merchant SOP is mark-refunded. Off-session is a later `IVaultRail` with Stripe + CHIP only. Putting those methods on `IHostedRail` is how Billplz grew a method that logs a warning and returns `false` — and how a billing job can call it anyway.

### 4.3 `RailSecrets` — what PUT stores, what generate/parse consume

```text
RailSecrets
  ApiKey            plaintext after SecretBox.Unprotect (sk_ / CHIP Bearer / Billplz secret)
  WebhookSecret     plaintext after Unprotect (whsec_ / PEM / X-Signature hex)
  PublicId          plaintext Brand ID or Collection ID; null for Stripe
```

The rail class should **not** load credentials itself once two rails exist. Today `StripeHosted` queries `gateway_credentials` internally. That couples the rail to “there is one ciphertext column.” CHIP needs two ciphertexts + a public id. **Move credential load to the HTTP edge** (or a 20-line `CredentialStore`) and pass `RailSecrets` in. Then `StripeHosted.CreateHostedUrlAsync` stops taking `PayDbContext` for keys.

This is the one Stripe **refactor that is required** before CHIP, not optional cleanliness: otherwise CHIP will either (a) reach into columns that do not exist, or (b) grow a parallel loader. Do it in the Stripe-harden step.

### 4.4 `ParsedPspEvent` — smaller than `GatewayWebhookParsedResult`

```text
ParsedPspEvent
  Verified          false → HTTP 400, do not insert, do not fulfill
  Kind              paid | failed | ignored | vaulted
  EventId           Stripe evt_… ; CHIP paid:{purchaseId} ; Billplz paid:{billId}
                    never a new Guid
  CheckoutId        from metadata / client_reference_id / query checkout_id
  ProviderTxnId     cs_ / pi_ / purchase id / bill id — stored on Charge.ProviderRef
  AmountPaid        major units; 0 means not cash
  Currency          ISO; fail closed if missing (Billplz may hardcode MYR)
  Error             for logs / 400 body; never the API key
```

Mapping rules the implementer must not get wrong:

| Inbound | Kind | Fulfill? |
|---------|------|----------|
| Stripe `checkout.session.completed` mode=payment amount>0 | `paid` | yes |
| Stripe mode=setup / amount 0 / `setup_intent.succeeded` | `vaulted` or `ignored` | **no** |
| Stripe unknown type | `ignored` | no (200) |
| CHIP `purchase.paid` amount>0 | `paid` | yes |
| CHIP `purchase.preauthorized` even with `recurring_token` | `vaulted` | **no** — Hub mapped this to `PAYMENT_COMPLETED`. New Pay must not. |
| CHIP `purchase.payment_failure` | `failed` | no (do not reverse a paid session) |
| CHIP `payment.refunded` | `ignored` until refunds exist | 200, do not journal reverse “while we are here” |
| Billplz `paid=true` / `state=paid` | `paid` | yes |
| Billplz due/unpaid | `failed` | no |
| Signature fail | `Verified=false` | 400 |

`EventId` policy (steal Hub judgment, not the type):

- Stripe: `stripeEvent.Id` (`evt_…`).
- CHIP: `{kind}:{purchaseId}` from nested `purchase.id` then root `id` (`ReadStablePurchaseId` judgment). Never invent a Guid. Missing id → 400 unusable, not 200 with a random key.
- Billplz: `{kind}:{billId}`.

Do not collapse Stripe `checkout.session.completed` and `payment_intent.succeeded` until a dual-event test exists. Live Pay only fulfills `checkout.session.completed`. **Keep that for Stripe.** CHIP has one paid event. Do not add Hub’s business-key table “for Stripe completeness” in the CHIP PR.

### 4.5 Who calls fulfill

```text
POST /v1/webhooks/{provider}/{orgId}
  allow-list provider
  read raw body (empty → 400)
  load RailSecrets for (orgId, provider)  (missing → 400)
  parsed = rail.ParseWebhook(...)
  if !parsed.Verified → 400
  insert psp_webhook_events (org, provider, parsed.EventId)
    unique hit → 200 duplicate
  if parsed.Kind != paid → 200 ignored/vaulted/failed
  if parsed.AmountPaid <= 0 → 200 ignored  (belt; fulfill also no-ops)
  resolve checkout_id (parsed.CheckoutId, else provider_session_id match)
  Fulfillment.FulfillPaidAsync(checkoutId, provider, parsed.ProviderTxnId)
  200 ok
```

The rail never receives `PayDbContext` for journal tables. If a CHIP class starts inserting `DocumentRow`, the PR has failed.

### 4.6 Amount: PSP vs checkout row

Live fulfillment books **`checkout.Amount`**, not `session.AmountTotal`. The webhook only uses PSP amount to **ignore zero**. Steal that. Optional later: refuse fulfill if PSP amount and checkout amount differ by more than a rounding epsilon. Do **not** invent Hub fee/tax/net on CHIP day one. `unknown` ≠ 0.

---

## 5. Where DI lives — pick, and why

**Pick: concrete types registered as themselves + a two-name switch at the HTTP edge. Not keyed services. Not `IEnumerable<IHostedRail>` + factory.**

### 5.1 What live does

```csharp
builder.Services.AddScoped<StripeHosted>();
```

`PublicPayEndpoints.Start(..., StripeHosted stripe, ...)`.

That is the correct DI for one rail.

### 5.2 What the CHIP PR does

```csharp
builder.Services.AddScoped<StripeHosted>();
builder.Services.AddScoped<ChipHosted>();
builder.Services.AddHttpClient("chip", c =>
{
    c.BaseAddress = new Uri("https://gate.chip-in.asia/api/v1/");
    c.Timeout = TimeSpan.FromSeconds(15);
});
```

Named HttpClient `"chip"` — **not** Hub’s default `CreateClient()` without a name, and **not** Billplz’s `"Billplz"` client with a DNS connect hook. CHIP’s API host is a constant (`gate.chip-in.asia`). Test vs live is the **brand dashboard**, not a Pay hostname (Hub adapter comment, steal).

`PublicPayEndpoints.Start` becomes:

```text
load checkout
load credential row for this org
  if none → 503 rail not configured
  if more than one active dogfood config and body.provider missing →
    use the single row if exactly one; else 400 "provider required"
provider = row.Provider (or explicit body.provider if we add it)
switch (provider):
  "stripe" → stripe.CreateHostedUrlAsync(...)
  "chip"   → chip.CreateHostedUrlAsync(...)
  else     → 400 unknown provider
map InvalidOperationException → 503/400
map HttpRequestException → 503
```

`WebhookEndpoints.Handle` already switches on provider (today: stripe or 400). Add a `"chip"` arm that calls `chip.ParseWebhook`. **A switch on two names is allowed.** A switch on five names with three arms throwing `NotImplementedException` is a factory of five in a costume.

### 5.3 Why not keyed services

ASP.NET Core keyed DI (`AddKeyedScoped<IHostedRail, StripeHosted>("stripe")`, `GetRequiredKeyedService<IHostedRail>(provider)`) is a **factory with extra syntax**. It makes the sixth rail a registration line — the Hub failure mode. It also forces `IHostedRail` to exist before it has two implementations. Reject for this product stage.

When (if) Pay has four hosted rails all boring in production, keyed services or a dictionary can be revisited. Not in the CHIP PR. Not “so we don’t have to touch Program.cs again.” Touching Program.cs **should** hurt: it is the allow-list.

### 5.4 Why not `IEnumerable<IHostedRail>` + `HostedRailFactory`

That **is** `PaymentGatewayFactory`. `FirstOrDefault(a => a.Provider == name)` plus a throw. The fifth adapter is an `AddScoped<IHostedRail, XenditHosted>()`. IsolationTests will not catch it. Reviewers who only grep `PaymentGatewayFactory` will miss `HostedRailFactory`. Forbid the pattern, not the name.

### 5.5 Why not a “picker” service with nullable CHIP

```csharp
internal sealed class HostedRails(StripeHosted stripe, ChipHosted? chip = null)
```

Nullable optional implementation is how Billplz stayed registered while “parked.” If CHIP is compiled, it is registered. If it is not compiled, there is no type. Do not ship `class BillplzHosted : IHostedRail { throw new NotSupportedException(); }`.

### 5.6 StripeHosted stays concrete

Do not rewrite Stripe to `class StripeHosted : IHostedRail` in a PR that does not add CHIP, unless that PR is the documented Stripe-harden that also moves webhook secret onto the row and extracts `ParseWebhook` out of `WebhookEndpoints`. Combining “extract Stripe parse” + “add CHIP parse” in **one** PR is acceptable if the PR is still reviewable. Splitting is better: (1) Stripe harden, tests still green; (2) CHIP class + switch arm + tests.

### 5.7 `IHttpClientFactory` vs `HttpClient` in the CHIP class

Hub CHIP uses `_httpFactory.CreateClient()` (unnamed). New Pay should use a **named** client so timeouts and (only if proven) a connect callback are per-rail. Inject `IHttpClientFactory` and `CreateClient("chip")`, or inject `HttpClient` via `AddHttpClient<ChipHosted>("chip")` typed client. Typed client is fine: it is not a factory of PSPs, it is one class’s HTTP. Prefer typed `AddHttpClient<ChipHosted>()` so `ChipHosted` does not need a magic name string **and** so we do not add `AddHttpClient<BillplzHosted>()` until Billplz exists.

Stripe continues to use Stripe.net, not HttpClient. Do not wrap Stripe in HttpClient “for symmetry.”

---

## 6. GatewayEndpoints PUT — second provider without a factory of five

### 6.1 Keep the live route

Live: `PUT /v1/orgs/{orgId}/gateway` with `{ provider, secret }`. G12 allowed this **or** `PUT .../gateways/{provider}`. Do not bike-shed a second path in the CHIP PR. Grow the **body**.

### 6.2 Allow-list function, not a factory

```text
EnabledRails.CanPut("stripe") → true
EnabledRails.CanPut("chip")   → true   // after CHIP ships
EnabledRails.CanPut("billplz") → false // until Billplz ships
EnabledRails.CanPut("razorpay"|"xendit"|"fiuu"|...) → false
unknown → 400 "unknown provider" or "Bar C rail is chip" — honest message
```

Replace today’s hard-coded `provider != StripeHosted.Provider` with `EnabledRails.CanPut(provider)`. The message should name **what is allowed**, not “first rail is stripe” after CHIP exists.

Do not accept uppercase `CHIP` as a second spelling in storage. Lowercase on write (`Trim().ToLowerInvariant()`), already live. Hub uppercased in the factory; Pay should not.

### 6.3 Body fields for two rails

```text
PutGatewayRequest
  provider          required, allow-listed
  secret            API key; required on first insert; blank means keep (steal Hub keep-existing, **if** tests cover it — live currently requires secret every PUT)
  webhook_secret    Stripe whsec_ / CHIP PEM / Billplz X-Signature; optional on Stripe until harden; **required on first CHIP insert**
  public_id         CHIP Brand ID / Billplz Collection ID; required for those rails; ignore for Stripe
```

GET never returns `secret` or `webhook_secret`. GET may return `public_id`, `last4`, `has_webhook_secret: true`, `capability`.

Live GET always looks up stripe. **Change GET** to:

- If `?provider=` present: that row or `{ configured: false, provider }`.
- If absent: **list** configured rows for the org (0–n). Empty list is honest. Do not invent a stripe row.

Merchant UI today expects a single stripe paste. When GET becomes a list, the Vite page can keep working if it finds `provider==stripe` in the list. CHIP UI: second paste form or a select of two.

### 6.4 Writer gate stays owner/admin

`RequireWriterAsync` already maps One `owner`/`admin`. Do not switch CHIP PUT to `check(member)`. Do not invent Pay `VIEWER`.

Audit: live PUT does **not** write `audit_events`. 013 wanted audit on key change in the same transaction. Worth adding in Stripe-harden (action `gateway.credentials.upsert`, no secret in the row). Not a blocker for the CHIP HTTP class, but do not log PEM.

### 6.5 What PUT must **not** do

- Call CHIP `POST /webhooks/` to register a callback (that is the registrar; §11).
- Rewrite `localhost` → `lazuar-local-dev.com`.
- Default provider to `billplz` when body.provider is missing (Hub cashier last resort). Missing provider → 400.
- Encrypt Brand ID.
- Store Razorpay `key:secret` because the form had a spare field.

### 6.6 Schema migration (PayDbContext, one folder)

Add columns on `gateway_credentials` (names indicative):

- `WebhookCiphertext` `text` nullable  
- `PublicId` `text` nullable  

One EF migration in `Data/Migrations/`. Not a second DbContext. Not Hub `payments.TenantPaymentConfigurations`. Backfill: Stripe rows keep `WebhookCiphertext` null until Ada pastes `whsec_` (or until Stripe-harden copies process env into the dogfood org — **do not** copy a process secret into every org in a migration).

After Stripe-harden, `WebhookEndpoints` reads `WebhookCiphertext` for that org, Unprotect, and **falls back** to `Pay:StripeWebhookSecret` only if the column is null (one release). Then remove the process fallback. CHIP must **not** use a process-level PEM.

---

## 7. WebhookEndpoints dispatch — switch on two names is fine

### 7.1 Path stays

`POST /v1/webhooks/{provider}/{orgId}`

Already in pay-spec. Already in the host. CHIP URL Ada pastes at the PSP:

```text
https://<public-pay>/v1/webhooks/chip/{orgId}
```

Not Hub `/api/v1/webhooks/payments/CHIP/{tenantId}`. Not `/v1/one/webhooks`.

### 7.2 Pipeline (shared), crypto (per rail)

The shared pipeline in §4.5 is the Linux-shaped handler: one function, no MediatR. Per-rail work is **only** `ParseWebhook`.

Implementation sketch: keep `Handle` as the shared pipeline; at the parse step:

```text
ParsedPspEvent parsed = provider.ToLowerInvariant() switch
{
    "stripe" => stripe.ParseWebhook(inbound, secrets),
    "chip"   => chip.ParseWebhook(inbound, secrets),
    _        => null // 400 unknown
};
```

Two arms. When Billplz ships, a third arm. **Do not** write `default: GetAdapter(provider).ParseWebhook`.

### 7.3 Content-Type

- Stripe / CHIP: raw **JSON** bytes. Do not JSON-re-serialize before verify. Live already reads `StreamReader` to string then Stripe.net verifies that string. CHIP RSA is over UTF-8 bytes of **that same raw string**. Preserve BOM/whitespace; do not `JsonSerializer.Serialize` a parsed document.
- Billplz (later): `application/x-www-form-urlencoded`. A JSON body on the Billplz route is 400. Enable buffering if anything else reads the body.

Hub copied query keys into `Query-*` headers for Billplz metadata recovery. New Pay should persist `provider_session_id` at generate and put `checkout_id` on the callback query; merge by bill id is the fallback, not a header-map cathedral.

### 7.4 Status codes (normative, including CHIP)

| Situation | HTTP | Notes |
|-----------|------|--------|
| Unknown provider | 400 | |
| Empty body | 400 | no insert |
| Rail not configured / no webhook secret | 400 | **not** 500. Live Stripe missing **process** secret is 503 — after per-org secret, missing org secret is 400 (Ada’s bug). Process wrap-key missing is still our bug (503/refuse boot). |
| Bad signature / bad PEM / bad HMAC | 400 | not 500, not 401, not 200 |
| Unusable after verify (no event id, no currency) | 400 | stop poison retries |
| Verified unknown event | 200 ignored | |
| Verified vault/setup/zero | 200 vaulted/ignored | **not paid** |
| Duplicate event id | 200 duplicate | fulfill not called |
| First paid | 200 ok | fulfill in same request |
| Handler crash before insert | 500 | retry is correct |

Live “missing process Stripe webhook secret → 503” stays until the secret moves per-org. CHIP must not 503 because `Pay:StripeWebhookSecret` is unset.

### 7.5 God-switch forever?

No. Two names is not a god switch. Five names with parked arms is. The allow-list in `EnabledRails` and the `switch` **must stay in lockstep**; a test should assert every `CanPut` provider has a webhook arm (source grep or a small array both use). When the third rail lands, the test fails until the arm exists. That is cheaper than a factory.

---

## 8. Per-org secrets — Stripe vs CHIP vs Billplz

### 8.1 Table of what Ada pastes

| Rail | API secret (`Ciphertext`) | Webhook secret (`WebhookCiphertext`) | Public id (`PublicId`) | Test vs live |
|------|---------------------------|--------------------------------------|------------------------|--------------|
| Stripe (live dogfood) | `sk_test_` / `sk_live_` | `whsec_…` **today process env — must move** | unused | key prefix; refuse mismatch if we store `environment` later |
| CHIP | Bearer secret key | PEM (`Webhook.public_key`, **not** company `GET /public_key/` unless Ada pastes that on purpose and we label it weaker) | Brand ID UUID | same API host; dashboard toggle. Copy must say so. |
| Billplz | secret key (Basic `key:`) | 128-hex X-Signature | Collection ID | **sandbox host vs www host** from stored environment, **not** from Pay hostname |

Plane A (`Pay:OneWebhookSecret`) stays a **process** secret on `/v1/one/webhooks`. Never mix tables. `psp_webhook_events.provider` is never `one`.

### 8.2 Stripe harden (required before CHIP, or the same PR’s first commits)

Today: PUT stores `sk_`; verify uses `Pay:StripeWebhookSecret`. That teaches the wrong lesson (one webhook secret for the whole process). CHIP cannot follow it.

Harden:

1. PUT accepts `webhook_secret`. Encrypt into `WebhookCiphertext`.
2. GET returns `has_webhook_secret` without the value.
3. Webhook loads org’s `WebhookCiphertext`; if null, fallback to process env **once**.
4. Tests: two orgs, two `whsec_`, org A’s signature fails on org B’s URL.
5. Merchant UI: second field “Webhook signing secret” with Stripe Dashboard copy: paste the endpoint URL `https://<pay>/v1/webhooks/stripe/<orgId>`.

Without step 4, BYOK is a slogan.

### 8.3 CHIP secrets

- Brand ID is **not** secret; GET may show it.
- Bearer is secret.
- PEM is secret (it’s the verify key; Hub stored it in `WebhookSecret`).
- Do not fetch company `GET /public_key/` as a default verify key if Ada did not paste a webhook PEM. Hub registrar fell back to company public key; that key is **not** the webhook key. CHIP comments in Hub: “Verify PEM is Webhook.public_key, not the company GET /public_key/ key.” Steal that comment into `ChipHosted` XML or a one-line remark.

### 8.4 Billplz secrets (when/if)

- X-Signature is **not** the API secret. Two fields. Hub stored both.
- Collection ID on every bill.
- Do not infer sandbox from `pay-local.lazuar.com`.

### 8.5 SecretBox

Live `SecretBox` AES-GCM, `Pay:WrapKey` 32-byte base64, dev fallback SHA256 of a fixed string. Production must set `Pay:WrapKey`. CHIP adds more ciphertext bytes; it does not need a second box. Never log Unprotect output. Never put wrap key in Vite.

Vite forbidden: `VITE_STRIPE_*`, `VITE_CHIP_*`, `VITE_BILLPLZ_*`, `VITE_KMS_*`.

---

## 9. Tests — copy `WebhookTests`, do not copy Hub module tests

### 9.1 What to copy from live Pay

File: `apps/lazuar-pay/tests/Lazuar.Pay.Tests/WebhookTests.cs`.

Pattern:

1. `PayApiFactory` hermetic; FakeOne owner; InMemory `PayDbContext`.
2. Seed: PUT gateway + POST checkout (need SST registered for fulfill — live `CheckoutEndpoints` inserts `OrgSettings` with `SstRegistered = false` on create, so fulfill can run).
3. Build a **signed** payload without network.
4. Assert HTTP + documents + journal balance + replay.

CHIP file: `ChipWebhookTests.cs` (or a TestCase source on the same class). Do not name it `ChipCollectGatewayAdapterTests`.

Billplz later: `BillplzWebhookTests.cs` with form body, not JSON.

### 9.2 Stripe tests that must exist **before** CHIP (honesty)

Already green:

- Missing process webhook secret 503 (will become 400 per-org after harden — update the test in the harden PR).
- Invalid signature 400.
- Paid + replay.

Missing, add in Stripe-harden:

- Empty body 400 **without** configuring the rail (already in `PublicPayTests` — keep).
- `mode=setup` or `amount_total: 0` → 200, **zero** documents. G22.
- Unknown provider `POST /v1/webhooks/xendit/{org}` → 400.
- Org A event does not fulfill org B’s checkout (metadata org mismatch → ignore/200, no journal). Live does **not** check metadata `org_id` vs URL orgId. Add the check in harden: mismatch → 200 ignore, log, **do not** 400 (Stripe retry storm on poison). Hub rejected mismatch except platform checkouts; new Pay has no platform org.

### 9.3 CHIP tests (minimum for the CHIP PR)

Hermetic. No `gate.chip-in.asia`. Fixture RSA keypair in the test (generate once in the test, PEM as webhook secret, sign body, send `X-Signature` base64).

| Test | Expect |
|------|--------|
| PUT `provider=chip` without `public_id` | 400 |
| PUT chip as `member` | 403, no row |
| PUT `provider=razorpay` | 400 |
| POST webhook empty | 400 |
| POST webhook bad signature | 400 |
| POST `purchase.paid` with metadata.checkout_id, amount>0 | 200, one RCPT, provider=`chip` on charge |
| Replay same event id | 200 duplicate, still one RCPT |
| `purchase.preauthorized` + recurring_token | 200 vaulted/ignored, **zero** RCPT |
| `purchase.paid` amount 0 | not paid |
| Missing purchase id | 400 |
| IsolationTests still green | |

Generate tests: do **not** hit CHIP. Stub `HttpMessageHandler` on the named/typed client to return `{ "checkout_url": "https://chip.test/pay/x", "id": "purch_test" }`. `POST /v1/pay/{token}/start` after CHIP keys + payer email → `redirect_url` is that URL. Missing email → 400 (CHIP requires it; Stripe today does not — CHIP start must require `StartPayRequest.Email`).

Do not import `Lazuar.ModuleTests` CHIP fixtures. Do not add a project reference to Hub tests.

### 9.4 IsolationTests extra greps (recommended in CHIP PR)

`src/**/*.cs` must not contain:

- `IPaymentGatewayAdapter`
- `PaymentGatewayFactory`
- `IPaymentGatewayFactory`
- `AddPaymentsModule`
- `GatewayPaymentCompletedIntegrationEvent`
- `Modules.Payments`

The last is already partly covered by `Modules.One` / `BuildingBlocks`; adding `Modules.Payments` is cheap.

---

## 10. `PublicDnsFallback` — do not port “just in case”

Hub: `PublicDnsFallback` is a `SocketsHttpHandler.ConnectCallback` that UDP-queries 1.1.1.1 / 8.8.8.8 for A records when the machine resolver fails. **Only Billplz’s named HttpClient uses it.** CHIP and Stripe do not.

New Pay:

- Stripe: Stripe.net. No.
- CHIP: `gate.chip-in.asia` is a normal public hostname. **Do not** attach a custom DNS connect hook because Billplz needed one on someone’s LAN.
- Billplz: **if** the rail is chosen **and** developers on this team still cannot resolve `www.billplz-sandbox.com`, then a **tiny** connect callback on the Billplz typed client is allowed. Port the **idea** (public resolvers, then system DNS), not necessarily the 190-line encoder. A simpler modern option is to document “use 1.1.1.1 as system DNS in dev” and skip the hook until a failing test on this laptop proves it.

Do not add `PublicDnsFallback.cs` to `Gateways/` in the CHIP PR. Do not register a `"Billplz"` HttpClient with an empty class.

---

## 11. `ChipWebhookRegistrar` — no silent registrar on boot

Hub: `UpdatePaymentConfigCommandHandler` on CHIP key save calls `ChipWebhookRegistrar.EnsureRegisteredAsync`, which lists CHIP webhooks, POSTs if missing, and stores PEM. It also rewrote localhost → `lazuar-local-dev.com` (fiction DNS). Billplz public-base would refuse that host.

New Pay:

**Do not** call CHIP `POST /webhooks/` from `Program.cs`, from a hosted `IHostedService`, or from PUT gateway as an implicit side effect.

Two honest options (pick one in the CHIP PR, write it in merchant copy):

**A. Dashboard paste (recommended for first CHIP dogfood).** Ada creates the webhook in CHIP dashboard: URL `https://<public-pay>/v1/webhooks/chip/{orgId}`, events `purchase.paid`, `purchase.payment_failure`, `purchase.preauthorized` (vault, **not** paid), optionally `payment.refunded` (ignored until refunds). Ada pastes `Webhook.public_key` PEM into Pay `webhook_secret`. Pay never calls CHIP’s webhook API.

**B. Explicit merchant action.** `POST /v1/orgs/{orgId}/gateway/chip/register-webhook` (writer). Body empty. Pay uses stored Bearer + `Pay:PublicBaseUrl` (must be HTTPS, non-loopback, not `lazuar-local-dev.com`) to list-then-create, then stores returned PEM. Fail closed if public base is localhost. This is Hub registrar **judgment** (idempotent on callback URL) without Hub’s fiction DNS and without boot.

Local laptop dogfood uses a tunnel origin as `Pay:PublicBaseUrl`. ngrok is **local only**, not staging.

If neither A nor B is in the PR, CHIP webhooks will not verify. Do not “fix” that with a boot registrar.

---

## 12. Sequencing — do not start all five

Locked order:

| Step | What | Why |
|------|------|-----|
| **0. Stripe harden** | Per-org `webhook_secret`; extract `ParseWebhook` into `StripeHosted` (or a sibling `StripeWebhook` type in the same file); setup-not-paid test; GET list; optional audit on PUT; IsolationTests greps | CHIP must copy a **complete** hosted rail, not a process-env shortcut |
| **1. One Malaysian rail: CHIP** (unless B00 amended) | `ChipHosted` two verbs; PUT fields; switch arm; named/typed HttpClient; hermetic tests; merchant picker of two; pay-spec union `stripe \| chip`; wrap copy on GET `capability` still `hosted_link`; no off-session; no registrar on boot | 011/01 dogfood names CHIP with Stripe; decisions.md next rail |
| **1b. Billplz instead** | Only if B00 amended. Form HMAC, collection id, public HTTPS callback, reminder-only copy **mandatory**, no `ChargeOffSession` method at all | Reminder-only; teaches public callback |
| **2. Refunds** | `IRefundRail` or methods on Stripe+CHIP only. Reverse journal **once** in `Fulfillment` (paper 07 / Bar C). Billplz mark-refunded SOP, no API | Parked Bar C |
| **3. Off-session** | Stripe PaymentIntent `off_session` + CHIP `recurring_token` charge. Billing job later. **Never** Billplz. Wait for webhook paid (adapter HTTP success is not `RCPT-`) | Only after hosted is boring |
| **4. Xendit / Razorpay** | Reminder-only hosted invoice / payment link. Labelled. `SupportsOffSession` false. No e-mandate. | `NP-LAT-002` |

**Do not** start step 4 in the CHIP PR. **Do not** implement 1 and 1b together. **Do not** add off-session to prove CHIP is “real” — Hub CHIP off-session is a second product (reference lookup, `force_recurring`, skip_capture). First CHIP `RCPT-` is a hosted purchase with `purchase.paid`.

Parked Bar C (`plans/013-prods/checklists/parked-bar-c.md`): “Second rail only after the first is boring.” Stripe hosted + webhook + receipt **is** the first rail. CHIP is the second. Treat Stripe-harden as making Stripe boring enough to add CHIP, not as a rewrite.

---

## 13. Files Hub may be **read** during implement vs **never copied**

Judgment is “open the file, steal a URL / header / event name, write new code in `Lazuar.Pay/Gateways/`.” Copy is “git add that file under apps/lazuar-pay.”

### 13.1 May be read (HTTP extract)

| File | Steal |
|------|--------|
| `Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` | `POST https://gate.chip-in.asia/api/v1/purchases/` Bearer; `brand_id`; `checkout_url` + `id`; `X-Signature` RSA SHA256 PKCS#1 v1.5 over raw body; PEM import; `purchase.paid` vs `preauthorized` vs `payment_failure`; `ReadStablePurchaseId`; `ExtractVaultIds` **for later vault**, not for paid; currency fail-closed; do **not** steal `PAYMENT_COMPLETED` for preauthorized |
| `Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs` | `POST …/api/v3/bills` Basic; collection_id; form HMAC dual-compute extra fields; `EventId = kind:billId`; no off-session; refund always false |
| `Modules/Payments/Infrastructure/Gateways/BillplzPublicBase.cs` | Fail closed loopback / fiction DNS / non-HTTPS unless explicit insecure **dev** flag |
| `Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` | Already stolen into `StripeHosted` + `WebhookEndpoints`. Further steal: `EventUtility.ConstructEvent`; never `mode=subscription`; setup is not paid; `ApplyCardWalletPaymentMethodTypes` = `["card"]` if we want to pin cards (live StripeHosted does **not** set `PaymentMethodTypes` — Hub does. Do not silently change Stripe in the CHIP PR.) |
| `Modules/Payments/Contracts/PaymentGatewayCapabilities.cs` | Four booleans → `WrapRails` in Pay |
| `Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs` | `ToMinorUnits` policy only |
| `Modules/Payments/Infrastructure/Gateways/ChipWebhookRegistrar.cs` | Event name list + “list before create” **if** option B register action. Not the class. |
| `Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs` | **Later.** Reminder-only payment link; discard `setupFutureUsage`. |
| `Modules/Payments/Infrastructure/Gateways/XenditGatewayAdapter.cs` | **Later.** Hosted invoice; `x-callback-token`; reminder-only class comment. |
| Hub tests under `tests/Lazuar.ModuleTests/Payments/*Webhook*` | Payload shapes as **examples**. Do not copy NSubstitute `IPaymentGatewayAdapter` tests. |

### 13.2 Never copied (filenames)

Do not add these files under `apps/lazuar-pay/`, do not `link` them, do not wrap them in a netstandard package:

- `IPaymentGatewayAdapter.cs`
- `PaymentGatewayFactory.cs` (and any `IPaymentGatewayFactory`)
- `Modules/Payments/Infrastructure/DependencyInjection.cs`
- `Modules/Payments/Infrastructure/Commands/ProcessGatewayWebhookCommandHandler*.cs` (all partials)
- `Modules/Payments/Infrastructure/EventHandlers/*.cs`
- `Modules/Payments/Infrastructure/Workers/*.cs`
- `Modules/Payments/Infrastructure/PaymentsDbContext.cs`
- `Modules/Payments/Domain/Aggregates/TenantPaymentConfiguration.cs`
- `Modules/Payments/Infrastructure/Services/CheckoutSessionCashier.cs`
- `Modules/Payments/Infrastructure/Gateways/PublicDnsFallback.cs` (unless §10 trigger, and then a **new** small handler in Pay, not the file)
- `Modules/Payments/Infrastructure/Gateways/ChipWebhookRegistrar.cs` as a boot/PUT side effect
- `BuildingBlocks/**`
- Any `*IntegrationEvent*.cs`
- `Modules/Payments/Contracts/Events/*`
- `Modules/Payments/Contracts/Queries/GenerateCustomerPortalQuery.cs`
- `AesSecretVault` / `SecretVaultExtensions.DecryptOrPlaintext`

`GatewayCommon.cs` as a whole file: **do not copy**. It carries Hub platform-tenant metadata, placeholder emails, and fee stamps that new Pay should re-introduce only when fulfillment books fees.

---

## 14. `packages/pay-spec/main.tsp` — what to add for the second provider

Live spec is the contract for `:8081`. Not `packages/api-spec`.

Add in the **CHIP PR** (or Stripe-harden if gateway ops are still unspecified):

1. **Gateway ops** matching the live host (already serving, currently unspecified):

   - `PUT /v1/orgs/{orgId}/gateway`
   - `GET /v1/orgs/{orgId}/gateway`

   Models: `provider`, `secret` (write-only), `webhook_secret` (write-only), `public_id`, `last4`, `configured`, `capability`, `has_webhook_secret`.

2. **Provider union** instead of free `string` on webhook path, if TypeSpec allows path enums without breaking unknown-400 (unknown can stay a runtime 400 even if spec lists two). Document in comments: allow-list is `stripe` and `chip`. Do **not** list `razorpay`, `xendit`, `billplz` until those rails exist.

3. **Webhook** already exists. Document: no Bearer; PSP signature is auth; empty body 400.

4. **Start pay:** optional `name` / `email` on `POST /v1/pay/{token}/start` — CHIP **requires** email.

5. Do not import Hub commerce payment-config routes. Do not add `/api/v1/webhooks/payments/{gateway}/{tenantId}`.

`task pay:spec` must stay the generator. Dist gitignored.

---

## 15. Wrap-rails helper in new Pay

New file (indicative) `Gateways/WrapRails.cs` — static, 30–40 lines, **no** Contracts project, **no** NuGet:

```text
IsEnabled(provider)           // PUT/webhook allow-list — the only list that may grow
SupportsOffSession(provider)  // stripe, chip
IsReminderOnly(provider)      // !SupportsOffSession
SupportsApiRefund(provider)   // stripe, chip; razorpay/xendit when those exist; not billplz
SupportsEmandate(_)           // false
Capability(provider)          // "hosted_link" until a vaulted token exists on that org
```

GET gateway returns `capability` from this function, not from Vite. Merchant copy:

- Stripe: hosted Checkout, cards; auto-debit only after a real PM (not yet a product button).
- CHIP: hosted page shows whatever the **brand** enabled; auto-debit **card token only**, later; we will not silent-debit FPX; CHIP does not run the subscription clock.
- Billplz (if ever): Hub amber, steal wording: pay-link renewals, no silent auto-charge.

Do not add `SupportsDuitNowQr` to Pay so `:5179` can draw a QR. The PSP page draws the QR.

A future billing job **must** call `SupportsOffSession` before `ChargeOffSession`. Until that job exists, **do not implement AUTO_CHARGE**.

---

## 16. 013 §5–§6 sketches vs live `StripeHosted` (update)

[06-money-rails.md](../013-prods/06-money-rails.md) §5–§6 were written when 8081 had an in-memory checkout fixture, **zero** PackageReference, and **no** webhook in pay-spec. Implementers must not follow those paragraphs as file-level truth.

| 013 sketch (historical) | Live `ee2db8e5` | Porting consequence |
|-------------------------|-----------------|---------------------|
| No webhook in pay-spec | `POST /v1/webhooks/{provider}/{orgId}` exists | Grow allow-list docs, do not add the route again |
| Host has no Stripe.net | `Stripe.net` 48.0.0 in host csproj | CHIP is HttpClient, not a second SDK race |
| Checkout in-memory | `PayDbContext` + Postgres 5435; tests InMemory | Persist CHIP purchase id on `checkouts` |
| Create checkout mints PSP URL | Mint happens on **buyer** `POST /v1/pay/{token}/start` | Keep that: payer email is available at start; CHIP needs it. Do not move mint back to merchant `POST /v1/checkouts` in the CHIP PR |
| PUT `/v1/orgs/{id}/gateways/{provider}` | PUT `/v1/orgs/{id}/gateway` singular, provider in body | Grow body; do not add a second route for CHIP |
| GET list of gateways | GET always stripe | Change GET to list or `?provider=` |
| Webhook secret per org in `TenantPaymentConfiguration` | Process `Pay:StripeWebhookSecret` | **Harden first** |
| Missing secret 400 | Missing process secret **503** | Per-org missing → 400; process wrap-key → 503 |
| Signature fail 400 (do not copy Hub 500) | Live 400 | Keep |
| Empty body 400 | Live 400 | Keep |
| Same handler fulfill | Live `Fulfillment.FulfillPaidAsync` | CHIP calls it; does not reimplement |
| One live adapter class | `StripeHosted` only | CHIP is the second class, not the fifth |
| Factory of five refused | No factory live | Do not add one |
| CHIP auto-register optional | Not present | Stay optional / dashboard |
| `IntegrationCheckoutSessions` refused | Not present | Keep refused |
| NuGet zero | Npgsql + Stripe.net | IsolationTests still ban Hub refs |

The **good** 013 judgments that still bind: Plane B ≠ Plane A; BYOK; wrap-rails; setup not paid; no MediatR; public `/v1`; VIEWER-as-member cannot PUT; ngrok is local-only.

---

## 17. Rejected alternatives

### 17.1 Copy `IPaymentGatewayAdapter` into `Lazuar.Pay`

**Rejected.** Five methods, uppercase names, `GatewayWebhookParsedResult` with fee/fx/net, `setupFutureUsage` on every generate including Billplz (ignored), `ChargeOffSession` that returns `false` and still looks callable. IsolationTests would not catch a clean-room copy. The live host already proved two functions are enough (`CreateHostedUrl` + parse in the webhook). Adding the Hub port “for later” is how later arrives on day one.

### 17.2 Project-reference Hub Payments

**Rejected.** IsolationTests fail on `apps/lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`. Even a “Contracts only” reference pulls the capability class into a second process and teaches new Pay to depend on Hub’s module versioning. Steal by reading files, not by linking them.

### 17.3 Shared kernel NuGet (`Lazuar.Pay.Gateways.Hub`)

**Rejected.** Same types, different shipping vehicle. The Hub adapters import `Modules.Payments.Application.Ports`, `ILogger`, and Hub metadata keys (`hub_payment_environment`, `subscription_id`, platform tenant). A NuGet would freeze that. Pay would still need a translation layer. Two codebases to fix when CHIP changes RSA. The point of 011 is **one** money process.

### 17.4 Go rewrite of adapters / of Pay

**Rejected in this paper.** [011/05-language.md](../011-new-lazuar-pay/05-language.md) argued Go for a **new** kernel that had not been written. [013 decisions.md](../013-prods/checklists/decisions.md): “Not a Go rewrite in this program.” Live: C# `Lazuar.Pay`, `PayDbContext`, IsolationTests, Stripe.net, NUnit, merchant and checkout Vite. Porting CHIP in Go would mean a second binary or a rewrite of a working `:8081` host. That is not the CHIP slice. If a future program starts a Go pay kernel, it may read **this** paper’s HTTP table; it is not this tree’s next commit.

C# remains the language that made the cathedral easy. The counter is IsolationTests + a small interface + a switch on two names, not another language.

### 17.5 Keep Stripe concrete forever and `if (provider=="chip")` copy-paste the whole webhook handler

**Rejected as the long-term shape; acceptable as a 20-line first draft that is immediately extracted.** Duplicating signature/idempotency/fulfill between two giant `Handle` methods will drift (CHIP forgets empty-body 400). Shared pipeline + per-rail parse is the Linux shape: one story.

### 17.6 Keyed DI / plugin registry / `IEnumerable<IHostedRail>`

**Rejected** for the second rail (§5). Revisit only when four rails are actually dogfooded.

### 17.7 Implement CHIP + Billplz + Xendit + Razorpay together “because Hub already did”

**Rejected.** Hub’s factory is the existence proof that this is cheap and wrong. 011: one Malaysian rail you will actually dogfood. SEA later, labelled reminder-only.

### 17.8 Tick `NP-GW-002` because CHIP showed a Visa form

**Rejected.** Stripe row means Stripe adapter. CHIP cards are CHIP.

### 17.9 Silent CHIP registrar + fiction DNS

**Rejected.** §11.

### 17.10 Port `PublicDnsFallback` for CHIP

**Rejected.** §10.

### 17.11 Put fulfill inside the rail class “so each PSP can book fees differently”

**Rejected.** Fees are a later journal line when the PSP **sent** a fee. Rails return amount + optional fee **later**; `Fulfillment` books. Split cashier/ledger was Hub’s README and 011’s reason to leave.

### 17.12 MediatR `IRequest<ParsedPspEvent>` per rail

**Rejected.** IsolationTests ban the string `MediatR`. Even without the package, in-process commands are the cathedral.

### 17.13 Grow `StripeHosted` into `class MultiGateway` with a switch inside one 2k-line file

**Rejected.** Two (then three) **small** classes next to each other. `WebhookEndpoints` may switch. A mega-class is how Hub adapters became unreadable and how CHIP preauthorized-as-paid survived.

### 17.14 Default missing keys to Billplz

**Rejected.** Live GET already returns stripe unconfigured, not a fake bill. Keep fail-closed.

---

## 18. Implementer walk-through (CHIP, assuming B00 stays)

This is the PR sequence, still design, not a patch.

**PR 0 — Stripe harden (may be the first commits of the CHIP branch):**

1. Migration: `WebhookCiphertext`, `PublicId` on `gateway_credentials`.
2. PUT accepts `webhook_secret`; GET returns `has_webhook_secret` + list.
3. `WebhookEndpoints` Unprotect org secret; fallback process env if null.
4. Move Stripe parse into `StripeHosted.ParseWebhook` (or `StripeWebhook.Parse` in the same folder). `Handle` calls it. Behavior unchanged; tests still green.
5. Add setup/zero test. Add unknown provider test.
6. Merchant UI: webhook secret field + printed URL.

**PR 1 — CHIP hosted + webhook:**

1. `WrapRails.IsEnabled` includes `chip`.
2. `ChipHosted` with typed `HttpClient`, `CreateHostedUrlAsync` (purchases POST, require `PublicId` + payer email), `ParseWebhook` (RSA, kinds).
3. `Program.cs`: `AddHttpClient<ChipHosted>(...)`.
4. `GatewayEndpoints`: allow `chip`; require `public_id` + `webhook_secret` on first chip PUT.
5. `PublicPayEndpoints.Start`: load credential; switch stripe/chip; CHIP requires email.
6. `WebhookEndpoints`: `chip` arm.
7. Persist `provider` + `provider_session_id` (purchase id) + `PspRedirectUrl` on the checkout row at start. Add column if `provider_session_id` is not already there (live `CheckoutRow` has `PspRedirectUrl` but **no** provider column — **add `Provider` and `ProviderSessionId`** so Billplz merge and CHIP replay can find the row without metadata). This is a small schema grow in PR 0 or 1.
8. `ChipWebhookTests` + generate stub test.
9. pay-spec union + gateway models + start email.
10. Merchant: provider select `stripe | chip`; CHIP fields Brand ID + PEM + secret; copy that test vs live is the CHIP dashboard.
11. IsolationTests greps. No registrar. No off-session. No refund. No `IPaymentGatewayAdapter`.
12. Document Ada’s dashboard webhook URL. Tunnel for local.

**Stop.** Do not add Billplz in PR 1. Do not add Xendit. Do not add `ChargeOffSession`.

**PR 2 — only after CHIP dogfood mints a real `RCPT-`:** refunds **or** Billplz **or** off-session, one at a time, Bar C.

---

## 19. PublicPay start — the other switch

Live `Start` always uses Stripe. With two rails:

- Resolve **which** credential: if the checkout row already has `Provider` from a previous start, reuse (same URL if `PspRedirectUrl` set and still open — optional cache). First start: if the org has exactly one enabled configured rail, use it; if two, require `provider` on the start body (rare; most orgs will have one).
- Do not pick Stripe because it is the default in code when the org only configured CHIP.
- Do not call Stripe then CHIP on failure.

Buyer has no Bearer. Missing CHIP email is 400, not a placeholder `customer@example.com`.

Stripe can keep allowing missing email (live does). CHIP must not.

---

## 20. Honesty gaps on the current Stripe rail that CHIP will inherit if ignored

These are not CHIP features. If CHIP copies them, the second rail is as incomplete as the first.

1. **Process-level webhook secret** — BYOK lie. Harden.
2. **GET always stripe** — second rail invisible.
3. **No `Provider` on checkout row** — webhook metadata is the only pointer. Billplz strips metadata; CHIP usually does not, but persist purchase id anyway.
4. **G22 test missing** — setup-not-paid is implemented, not locked. Add before CHIP preauthorized test so we have a pattern.
5. **pay-spec missing gateway PUT/GET** — host already serves it. Spec should catch up in harden or CHIP, not in a third “docs” PR that never ships.
6. **README still says in-memory fixture** — live is Postgres. Do not teach CHIP implementers to add a dictionary.
7. **No metadata org mismatch guard** — add in harden.
8. **Fulfillment does not persist `provider` from the checkout**, only from the webhook argument. Fine if the handler passes `"chip"`.
9. **StripeHosted does not pin `payment_method_types=['card']`.** Hub does. Changing that in the CHIP PR is a product change (FPX on Stripe Checkout). Leave Stripe generate behavior alone unless a named Stripe harden item.
10. **Merchant paste is stripe-only.** CHIP without a picker is a dead rail.

---

## 21. Anti-goals (reject the PR)

1. `IPaymentGatewayAdapter` or five-method lookalike in `Lazuar.Pay`.
2. `PaymentGatewayFactory` / `IEnumerable<IHostedRail>` lookup.
3. `ProjectReference` to `apps/lazuar-api` or any `Modules.*`.
4. MediatR, BuildingBlocks, outbox, `GatewayPaymentCompletedIntegrationEvent`.
5. Registering Razorpay/Xendit/Billplz “disabled.”
6. `ChargeOffSession` / Billing Portal on CHIP day one.
7. Counting CHIP `purchase.preauthorized` as paid.
8. Silent CHIP registrar or localhost fiction DNS.
9. `PublicDnsFallback` in the CHIP PR.
10. Vite secrets.
11. Hub `/admin/commerce/payment-config` or `/api/v1/webhooks/payments/...`.
12. Go rewrite of Pay or of adapters in this program.
13. Shared adapter NuGet.
14. Tick Stripe-done because CHIP showed cards.
15. ACK 200 before idempotency insert.
16. Signature fail 500.
17. Invented event ids.
18. Fulfill inside the rail class.
19. `DecryptOrPlaintext` / `Jwt:Secret` as KMS.
20. Five-option merchant dropdown.

---

## 22. Open questions (named, not silent)

1. **CHIP vs Billplz** — **not open for the next rail unless B00 is amended.** Next is CHIP. Billplz remains a later reminder-only rail, not a silent default.
2. **Dashboard paste vs explicit register action for CHIP PEM** — pick in the CHIP PR (§11). This paper recommends dashboard paste for the first CHIP `RCPT-`.
3. **Whether Stripe-harden is a separate PR** — recommended yes; acceptable as stacked commits on one branch.
4. **Whether `IHostedRail` is introduced in PR 0 or PR 1** — introduce when the second class exists (PR 1). PR 0 may extract Stripe parse without an interface.
5. **Checkout `Provider` column** — recommended in PR 0/1. Required before Billplz.
6. **Fee expand on Stripe** — not in CHIP PR. `unknown` ≠ 0 remains.
7. **Keyed DI later** — only after four real rails. Not now.

---

## 23. Verdict, restated for the implementer

You **can** put Hub’s CHIP (and later Billplz) HTTP into new Pay. You do it by adding **one small class** next to `StripeHosted`, **one allow-list token**, **one switch arm**, **per-org webhook secret + brand/collection id**, **hermetic tests cloned from `WebhookTests`**, and a **pay-spec** that names two providers. You call `Fulfillment.FulfillPaidAsync`. You do **not** clone `Modules/Payments/Infrastructure/Gateways`, `IPaymentGatewayAdapter`, or `PaymentGatewayFactory`.

The user belief is true as **judgment**. It is false as **folder copy**. The seam that makes the difference is already on 8081; it is just one rail wide. Widen it by one.

---

*End of 014-evals paper 09. Do not implement from this file. Do not flip tracker cells from this file.*
