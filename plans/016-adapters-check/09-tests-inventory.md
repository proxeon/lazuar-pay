# 09 — Tests inventory: what exists vs C32 / B28 / X23 / R25 / H19 / H20 / G25 / H18–H25 / P23

**Date:** 24 August 2026  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Branch:** `feat/015-four-adapters`  
**HEAD:** `c621ceba7fc7b79f16954d0819200cb21db6f22b` — `c621ceba` — `docs(015): check off implemented T–Q phases`  
**Type:** Uncondensed inventory. **Not** an implementation. **Not** a run of live CHIP / Billplz / Xendit / Razorpay / Stripe.  
**Authority:** live files under `apps/lazuar-pay/tests/Lazuar.Pay.Tests/*.cs` (excluding `bin/` and `obj/`), `apps/lazuar-pay-checkout/src/locks.test.ts`, `apps/lazuar-pay-merchant/src/locks.test.ts`, plus the 015 phase files named below. Checklist ticks are a map, not proof.

Parent index: [README.md](./README.md). 015 map: [../015-four-adapters/checklists/README.md](../015-four-adapters/checklists/README.md). The Case × five-rails table this file scores is [015/00 §7](../015-four-adapters/00-what-must-be-done.md).

This file names **every** `[Test]` method that exists on this SHA, scores it against the 015 matrices, and then lists **one test method per remaining gap** so a later implementer does not invent scope. Other 016 slices own rails and frontends. This slice owns the test matrix.

---

## 0. How this inventory was taken

1. Listed `apps/lazuar-pay/tests/Lazuar.Pay.Tests/` excluding `bin/` and `obj/`. Source files on this SHA:

   | File | Role |
   |------|------|
   | `WebhookTests.cs` | Stripe Plane B |
   | `RailTests.cs` | CHIP / Billplz / Xendit / Razorpay start + webhook |
   | `GatewayTests.cs` | PUT/GET `/v1/orgs/{orgId}/gateway` |
   | `PublicPayTests.cs` | Public GET pay + Stripe empty webhook |
   | `IsolationTests.cs` | Grep bans (Hub types, csproj, Vite `@repo/api-types-ts`) |
   | `CheckoutTests.cs` | Writer mint, org bind, idempotency, health-skips-One |
   | `CatalogTests.cs` | Product create writer gate |
   | `PayApiFactory.cs` | Hermetic host factory (not a test) |
   | `FakePspHandler.cs` | Fake `IHttpClientFactory` for CHIP/Billplz/Xendit/Razorpay HTTP (not a test) |
   | `FakeOneHandler.cs` | Fake One `/me` + `authz/check` (not a test) |
   | `CorsTests.cs` | 5178/5179 allow, 3003/3004 deny |
   | `HealthTests.cs` | `/health`, `/v1/health` |
   | `OrgReadyTests.cs` | `/v1/orgs/{id}/ready` |
   | `WhoamiTests.cs` | `/v1/whoami` |
   | `Lazuar.Pay.Tests.csproj` | NUnit + `Microsoft.AspNetCore.Mvc.Testing` + EF InMemory; **ProjectReference** only `src/Lazuar.Pay` |

2. Grepped `[Test]` in those `*.cs` files. **58** methods. No `[Ignore]`, no `Assert.Ignore`, no Skip. Inventory by listing methods is what `dotnet test` would enumerate; this file does not claim a live `task pay:test` exit code on this agent.

3. Opened every MUST OPEN file in full: `WebhookTests`, `RailTests`, `GatewayTests`, `PublicPayTests`, `IsolationTests`, `CheckoutTests`, `CatalogTests`, `PayApiFactory`, `FakePspHandler`, checkout `locks.test.ts`, merchant `locks.test.ts`, 015 `c32-chip-webhook-tests.md`, `b28-billplz-tests.md`, `x23-xendit-tests.md`, `r25-razorpay-tests.md`, `h19-setup-not-paid-test.md`, `h20-zero-amount-not-paid-test.md`, `h18`–`h25`, `p23-empty-body-400-all.md`, 013 `g25-webhook-tests.md`, 015 README, 016 README.

4. Cross-read live handlers so “missing test” is not confused with “missing code”: `WebhookEndpoints.cs`, `ChipWebhook.cs`, `BillplzWebhook.cs`, `XenditWebhook.cs`, `RazorpayWebhook.cs`, `StripeWebhook.cs`, `PublicPayEndpoints.cs`, `GatewayEndpoints.cs`, `BuyerEmail.cs`, `BillplzHosted.TryPublicBase`, `Fulfillment.cs`.

5. Frontend vitest: `apps/lazuar-pay-checkout/src` has **only** `locks.test.ts` (2 `it`s). `apps/lazuar-pay-merchant/src` has `locks.test.ts` (2 `it`s) and `auth/bearerToken.test.ts` (4 `it`s). No component tests of `App.tsx` / `WorkspacePage.tsx`.

**Scoring legend used below**

| Mark | Meaning |
|------|---------|
| **exists** | A `[Test]` / `it(...)` whose primary job is this case, with the assertions the phase named |
| **partial** | Case is mixed into another method, or assertions the phase named are absent |
| **missing** | No method locks it. Code may still implement it. Checklist `[x]` is not evidence |
| **n/a** | Phase says the cell does not apply to that rail (e.g. Brand ID on Stripe) |
| **blocked** | Hermetic test would need a seam that does not exist (Stripe.net start) |

015 phases C32, B28, X23, R25, H18–H25, P23, G25, T16, A99.2 are **all ticked `[x]`** on this SHA. Several of those ticks are false against live tests. Named in §8.

---

## 1. Fixture inventory (not tests)

### 1.1 `PayApiFactory`

```csharp
public sealed class PayApiFactory : WebApplicationFactory<Program>
{
    public FakeOneHandler One { get; } = new();
    public FakePspHandler Psp { get; } = new();
    public string StripeWebhookSecret { get; init; } = "whsec_test_local";
    // UseEnvironment("Testing")
    // Pay:StripeWebhookSecret = StripeWebhookSecret
    // Pay:PublicBaseUrl = "https://pay.test.example"   // B28.2: bypasses Billplz localhost
    // OneClient replaced with FakeOneHandler
    // IHttpClientFactory replaced with StaticHttpFactory(Psp)
    // PayDbContext UseInMemoryDatabase(unique name)
    // InMemoryEventId.TransactionIgnoredWarning ignored  // H12.2: InMemory is not Postgres TX proof
}
```

This factory is G25.1 for One and for CHIP/Billplz/Xendit/Razorpay HTTP. It is **not** a Stripe.net HTTP stub. `StripeHosted` still constructs `new StripeClient(secret)` and would hit the network if a test called `POST /v1/pay/{token}/start` with `active_provider=stripe`. No test does that. Do not add a Stripe start test that uses a real `sk_test_`.

`Pay:PublicBaseUrl=https://pay.test.example` means **every** Billplz start against this factory already has a public HTTPS origin. `RailTests.Billplz_paid_form_and_localhost_blocked` therefore **cannot** prove B15. The method name is a lie. See §4.3.

### 1.2 `FakePspHandler`

```csharp
public sealed class FakePspHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, string?, HttpResponseMessage>? Responder { get; set; }
    public string? LastBody { get; private set; }
    public Uri? LastUri { get; private set; }
}
public sealed class StaticHttpFactory(HttpMessageHandler handler) : IHttpClientFactory
```

Default with no `Responder`: 404 `{}`. Used by `Chip_start_and_paid_webhook`, `Billplz_paid_form_and_localhost_blocked`, `Xendit_paid_and_settled`, `Razorpay_captured`. `LastBody` is asserted only for CHIP (`force_recurring` absent, `checkout_id` present). `LastUri` is asserted only for Billplz (`billplz-sandbox`). No test asserts CHIP URL host `gate.chip-in.asia`, Xendit `/v2/invoices`, Razorpay `/v1/payment_links`, or Basic/Bearer headers.

### 1.3 `FakeOneHandler`

Shared by almost every HTTP test. Typical owner responder (copied into `WebhookTests` and `RailTests`):

```csharp
return FakeOneHandler.Json(HttpStatusCode.OK,
    """{"user_id":"u1","is_platform_admin":false,"tenants":[{"id":"t1","role":"owner","status":"active"}]}""");
```

Writer vs member is **whoami role**, not a fake `authz/check` relation One does not have (H18.2). `GatewayTests.Role("member")` still returns `{"allowed":true}` on authz; `MemberGate.RequireWriterAsync` must still 403. That is the correct Fake One shape.

### 1.4 Helpers inside test classes (not reusable fixtures)

`WebhookTests.Sign(secret, payload, t)` — Stripe `t=…,v1=hex(HMACSHA256)`.  
`WebhookTests.SeedRailAndCheckout` — PUT stripe `sk_test_dummy` + `whsec_test_local`, POST checkout `amount:10`, return checkout id.  
`RailTests.SeedCheckout` — POST checkout, return `(public_token, id)`.  
`RailTests.Put` — PUT gateway JSON as owner.  
CHIP tests generate `RSA.Create(2048)` PEM as `webhook_secret` and sign with `RSASignaturePadding.Pkcs1`.  
Billplz tests call `BillplzWebhook.ParseForm` + `ComputeHmac` **from production code** (acceptable; do not copy Hub).  
Razorpay tests HMAC-SHA256 hex of raw JSON with `wh_rzp`.

There is **no** shared `RailHarness`. A later implementer adding the gap list should either keep copying these helpers or extract one helper file. Do not invent a Hub `PaymentGatewayFactory` test host.

---

## 2. Complete `[Test]` catalog (58 methods, quoted)

NUnit. No parameterized `TestCase` for provider. Every rail case is a separately named method or is missing.

### 2.1 `WebhookTests` — 7 methods (Stripe Plane B)

```
WebhookTests.Missing_webhook_secret_is_503_when_rail_configured
WebhookTests.Invalid_signature_is_400
WebhookTests.Completed_session_writes_receipt_and_replay_is_noop
WebhookTests.Setup_mode_is_ignored
WebhookTests.Zero_amount_session_is_ignored
WebhookTests.Cross_org_checkout_is_400
WebhookTests.Unknown_provider_is_400
```

**`Missing_webhook_secret_is_503_when_rail_configured`** — factory `StripeWebhookSecret = ""`, PUT stripe, then **nulls** `GatewayCredentials.WebhookCiphertext`, POST `/v1/webhooks/stripe/t1` body `{"id":"evt_x"}` with **no** signature. Asserts **503**. Locks H11 “empty process env + empty row → 503”. Does **not** lock H11 Production-must-not-use-process-env-when-row-empty (factory is always `Testing`).

**`Invalid_signature_is_400`** — signed-looking header `t=1,v1=deadbeef` on a completed-session-shaped body. Asserts **400**. G25.2 bad signature. Does **not** assert missing `Stripe-Signature` header (H10.1 “Missing Stripe-Signature header → 400”).

**`Completed_session_writes_receipt_and_replay_is_noop`** — signed `checkout.session.completed`, `mode=payment`, `amount_total:1000` vs checkout `amount:10` (match in sen), `client_reference_id` + metadata checkout/org. First POST 200, `Documents.Count == 1`, `Number` starts with `RCPT-`, journal D sum = C sum. Replay same payload 200 body contains `duplicate`, still one document. This is G25.2 replay (G21), G25 paid path, H12.4 serial duplicate, H10.3 signed event still 200 + `RCPT-`. **Does not** assert `Title == "Official Receipt"`, **does not** assert checkout `status == paid`, **does not** mention `SstRegistered` (T16 ticks are over-claims). **Does not** assert two journal **lines** (only debit=credit). Razorpay’s R21 test is the only `JournalLines.Count() == 2`.

**`Setup_mode_is_ignored`** — signed completed session `"mode":"setup"`, `amount_total:0`, `client_reference_id` = **open** checkout with amount 10 (the H19 trap). Asserts 200, body contains `ignored`, `Documents.Count == 0`, checkout still `open`. **This is H19.** Body in live code is `{ ignored: "setup_or_zero" }` (`StripeWebhook`); test does not require the token `setup` specifically, only `ignored`. H19.1 “body contains `ignored` / `setup`” is met on `ignored`. Exists.

**`Zero_amount_session_is_ignored`** — `"mode":"payment"`, `amount_total:0`, `payment_status:paid` (the H20 trap: PSP says paid, amount 0). Asserts 200 and `Documents.Count == 0`. **Does not** assert body `ignored`, **does not** assert checkout still `open`. H20.1 is **partial**. Code path is `setup_or_zero` in `StripeWebhook`.

**`Cross_org_checkout_is_400`** — t1 checkout, PUT stripe keys on **t2** (whoami switched to t2 owner), POST `/v1/webhooks/stripe/t2` with t1 checkout id. Asserts 400, zero documents. **This is H13** for Stripe only. C32 “Cross-org bind still holds for chip path” is **missing**.

**`Unknown_provider_is_400`** — POST `/v1/webhooks/paypal/t1` body `{"id":"x"}`. Asserts 400. **This is P22** webhook path. No PUT-unknown test.

### 2.2 `RailTests` — 7 methods (four new rails, uneven)

```
RailTests.Chip_start_and_paid_webhook
RailTests.Chip_preauthorized_is_ignored
RailTests.Chip_start_without_email_is_400
RailTests.Billplz_paid_form_and_localhost_blocked
RailTests.Xendit_paid_and_settled
RailTests.Razorpay_captured
RailTests.Chip_empty_body_400
```

**`Chip_start_and_paid_webhook`** — RSA PEM PUT chip + `public_merchant_id=brand_1`, FakePsp 200 `{id, checkout_url}`, start with name+email, assert `LastBody` has no `force_recurring` and has `checkout_id`, signed `purchase.paid` total 1000 MYR, 200, one `RCPT-`, balanced journal, replay body contains `duplicate`, still one document. **This is C32 paid+replay, C15 no recurring flag, C17 start (partial), C19, C25.** Missing C17 asserts: response `redirect_url`, `checkout.Provider == chip`, `ProviderSessionId == purch_1`. Missing C14 `org_id` in metadata (only `checkout_id`).

**`Chip_preauthorized_is_ignored`** — signed `purchase.preauthorized` with `recurring_token`. 200, body contains `preauthorized`, zero documents. **This is C21 / C32 preauthorized / H19 analogue for CHIP.** Does not assert checkout still `open`.

**`Chip_start_without_email_is_400`** — start `{"name":"Ada"}` only. 400. **This is C30 / P19 chip.** Does not cover placeholder `customer@example.com` (P20). Does not cover billplz/xendit/razorpay missing email.

**`Billplz_paid_form_and_localhost_blocked`** — PUT billplz with collection + env test, FakePsp bill URL, start with email **succeeds** (because factory PublicBaseUrl is `https://pay.test.example`), `LastUri` contains `billplz-sandbox`, form `paid=true` HMAC with `excludeExtra: false` including `checkout_id` query+form, POST `/v1/webhooks/billplz/t1?checkout_id=…`, 200, `Documents.Count == 1`. **Does not** assert `RCPT-`. **Does not** replay. **Does not** POST with `Pay:PublicBaseUrl=http://localhost:8081`. **Does not** assert `Psp.LastUri` is null on localhost. The identifier `localhost_blocked` is false. B15 **missing**. B28 paid **partial**. B28 replay **missing**. B19 extra-fields variant **partial** (uses with-extra HMAC on `checkout_id`, not Hub ExtraFields `paid_at` / `transaction_id` / `transaction_status`).

**`Xendit_paid_and_settled`** — PUT xendit callback token `tok_1`, FakePsp invoice_url, start with email, PAID with `x-callback-token: tok_1` `paid_amount:10` (major units; host `MoneyMath.ToMinor`), then SETTLED same invoice id. Asserts SETTLED 200 and **one** document. **This is X16.** **Does not** assert `RCPT-`, journal balance, PAID replay `{duplicate:true}`, EXPIRED, bad token, empty body, missing email. X23 is **partial** (paid + settled only).

**`Razorpay_captured`** — PUT `secret: rzp_test:secret`, FakePsp `short_url`, start with email, `payment.captured` with `"tax":12,"fee":30`, HMAC header, 200, `Documents.Count == 1`, `JournalLines.Count() == 2`. **This is R21** (tax/fee not booked) and R17 paid **partial**. **Does not** assert `RCPT-`, debit=credit (count 2 is the lock), replay, `payment.failed`, empty body, bad signature, missing email. R25 is **partial**.

**`Chip_empty_body_400`** — PUT chip, POST `/v1/webhooks/chip/t1` content `"  "` (whitespace). 400. **This is C26 / P23 chip / G25 empty for chip.** Stripe empty is `PublicPayTests.Empty_webhook_is_400` with `""`. Billplz / Xendit / Razorpay empty **missing**.

### 2.3 `GatewayTests` — 4 methods

```
GatewayTests.Member_cannot_put_gateway
GatewayTests.Put_requires_webhook_secret
GatewayTests.Put_and_get_does_not_echo_secret
GatewayTests.Chip_put_requires_brand_id
```

**`Member_cannot_put_gateway`** — role `member`, PUT stripe keys, **403**. **This is H18.1 member PUT.** H18 also wants Owner PUT 200 (covered inside `Put_and_get_does_not_echo_secret`) and **Member GET still 200 metadata (S18)** — **missing**.

**`Put_requires_webhook_secret`** — owner PUT stripe `{provider, secret}` without `webhook_secret`, **400**. **This is P12.3.**

**`Put_and_get_does_not_echo_secret`** — PUT stripe sk + whsec, PUT body must not contain plaintext, GET `configured`, `provider=stripe`, `capability=hosted_link`, JSON must not contain `sk_test` or `whsec_abc`, `AuditEvents` has `gateway.credentials.upsert`, `OrgSettings.ActiveProvider=stripe`. **This is S18, P13, P16, H23 (audit exists), P14 for stripe.** Does not GET as member. Does not assert audit row omits last4/secret columns (the row type has no secret field; still no explicit assert). Does not PUT chip then GET active chip.

**`Chip_put_requires_brand_id`** — PUT chip with PEM but no `public_merchant_id`, **400**. **This is C31 PUT.** Billplz collection required **missing**. Chip start with empty Brand ID after PUT (C31 start 503) **missing**.

### 2.4 `PublicPayTests` — 3 methods

```
PublicPayTests.Public_get_does_not_need_bearer
PublicPayTests.Public_missing_is_404
PublicPayTests.Empty_webhook_is_400
```

**`Public_get_does_not_need_bearer`** — create checkout as owner, GET `/v1/pay/{token}` without Authorization, 200, One was called on create but second public GET does not increment `One.SendCount`. Does **not** assert `email_required`. P19 public hint **missing**.

**`Public_missing_is_404`** — GET `/v1/pay/missing`, 404, One SendCount 0.

**`Empty_webhook_is_400`** — POST `/v1/webhooks/stripe/t1` content `""`, **no** rail configured. 400. Live handler reads body **after** allow-list, **before** creds (`WebhookEndpoints`). **This is P23 stripe / G25.2 empty (G20).** Whitespace-only on stripe is not tested (chip whitespace is).

### 2.5 `IsolationTests` — 6 methods

```
IsolationTests.Host_csproj_does_not_reference_the_old_api
IsolationTests.Test_csproj_does_not_reference_the_old_api
IsolationTests.Source_does_not_use_mediatr_or_hub_modules
IsolationTests.Source_does_not_create_org_or_user_tables
IsolationTests.Vite_apps_do_not_use_hub_types
IsolationTests.No_csproj_references_apps_lazuar_api
```

`Banned` on csproj text: `lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api`.  
`BannedSrc` on `src/**/*.cs`: `MediatR`, `Modules.One`, `BuildingBlocks`, `IPaymentGatewayAdapter`, `PaymentGatewayFactory`, `IPaymentGatewayFactory`, `AddPaymentsModule`, `GatewayPaymentCompletedIntegrationEvent`, `Modules.Payments`, `ApplicationFeeAmount`, `Razorpay.Api`.

**H21 exists** for the Hub adapter type names.  
**H22 partial:** `ApplicationFeeAmount` is banned; live H22.1 also named `application_fee`, `TransferData`, `transfer_data` — those strings are **not** in `BannedSrc`.  
**R14 exists** via `Razorpay.Api` in both src grep and every csproj under `apps/lazuar-pay`.  
**T17 partial:** Isolation does **not** grep `Lhdn`, `MyInvois`, `UBL`, `XAdES`, `Irbm`. `Modules.` would catch `Modules.Lhdn` only if someone pasted that token.  
**C28 / B23 missing as greps:** `ChipWebhookRegistrar`, `PublicDnsFallback` are not in `BannedSrc`. Do **not** ban the hostname `lazuar-local-dev.com` — `BillplzHosted.TryPublicBase` **contains** that string as a **block list**. Ban the type name `PublicDnsFallback`.  
**Vite lock:** only `package.json` must not contain `@repo/api-types-ts`. Does not grep merchant/checkout `src` for Hub type imports.

### 2.6 `CheckoutTests` — 10 methods (host checkouts, not rails)

```
CheckoutTests.Create_without_bearer_is_401
CheckoutTests.Create_and_get_open_session
CheckoutTests.Get_unknown_is_404
CheckoutTests.Create_for_other_org_is_403
CheckoutTests.Get_other_org_session_is_403
CheckoutTests.Create_idempotent_on_key
CheckoutTests.Create_defaults_currency_to_myr
CheckoutTests.Create_rejects_non_positive_amount
CheckoutTests.Member_cannot_create_checkout
CheckoutTests.Health_still_skips_one
```

**`Member_cannot_create_checkout`** is H17. **`Health_still_skips_one`** is G25.1 “health still 200 if One throws”, **not** “if the PSP handler would throw”. No test sets `Psp.Responder = (_,__) => throw`.

### 2.7 `CatalogTests` — 2 methods

```
CatalogTests.Create_product_as_owner
CatalogTests.Member_cannot_create_product
```

Writer gate analogue; not a rail matrix cell.

### 2.8 `CorsTests` — 4 methods (Q17)

```
CorsTests.Health_allows_merchant_origin        // Origin http://localhost:5178
CorsTests.Health_allows_checkout_origin        // Origin http://localhost:5179
CorsTests.Health_does_not_allow_ops_origin     // Origin http://localhost:3003
CorsTests.Health_does_not_allow_portal_origin  // Origin http://localhost:3004
```

Q17 exists. Uses `WebApplicationFactory<Program>` **not** `PayApiFactory` (real host CORS, Testing env from launch). Not a rail cell.

### 2.9 `HealthTests` — 3 methods

```
HealthTests.Health_returns_ok
HealthTests.V1_health_returns_ok
HealthTests.Health_does_not_call_one
```

### 2.10 `OrgReadyTests` — 6 methods

```
OrgReadyTests.Ready_when_one_allows_member
OrgReadyTests.Ready_forbidden_when_allowed_false
OrgReadyTests.Ready_forbidden_when_one_403
OrgReadyTests.Ready_503_when_one_500
OrgReadyTests.Ready_401_without_bearer_skips_one
OrgReadyTests.Ready_checks_path_org_not_header
```

### 2.11 `WhoamiTests` — 6 methods

```
WhoamiTests.Whoami_maps_org_id_from_one_me
WhoamiTests.Whoami_allows_empty_tenants
WhoamiTests.Whoami_without_authorization_is_401_and_skips_one
WhoamiTests.Whoami_maps_one_401
WhoamiTests.Whoami_maps_one_timeout_to_503
WhoamiTests.Whoami_maps_one_500_to_503
```

---

## 3. Frontend vitest catalog (8 `it`s)

### 3.1 `apps/lazuar-pay-checkout/src/locks.test.ts`

```
describe('checkout honesty')
  it('has no OIDC dependency')
  it('does not render wallet tiles or card PAN')
```

`has no OIDC dependency` reads `package.json`, forbids `oidc-client-ts`, `react-oidc-context`, `@repo/api-types-ts`. **K15 exists.**

`does not render wallet tiles or card PAN` reads **only** `src/App.tsx` (not a walk of `src/`), forbids case-insensitive `grabpay|tng|touchngo|boost|duitnow|fpx|shopee` and `autocomplete="cc-number"`. **K12 / K17 exist as greps.** Does not lock:

- `email_required` from public GET (K11; host `PublicPayEndpoints` already returns the flag; UI uses it; **no test**)
- placeholder `customer@example.com` still clickable (host P20 400; UI only checks `!email.trim()`)
- `?status=verifying` is not paid (K14; UI implements verifying screen; **no test**)
- poll `GET /v1/pay/{token}` every 2s cap 15 (K13; UI implements; **no test**)
- start 503 → `rail not configured` (K16; UI implements; **no test**)
- start 400 conflates B15 and P19 into one string `'callback base not public or email required'` (host/UI mismatch; **no test of either mapping**)

### 3.2 `apps/lazuar-pay-merchant/src/locks.test.ts`

```
describe('merchant honesty locks')
  it('has no password form or Hub login')
  it('package.json does not depend on Hub types')
```

Walks `src/**/*.ts,tsx,css` excluding `*.test.*`. Forbids `type="password"`, `/one/auth/login`, `lazuar_auth`. Package forbids `@repo/api-types-ts`, `lazuar-ops`.

Does **not** lock U10 five names vs `PayProviders.All`, U11–U15 field placeholders, U16 writer-only paste, U17 member last4, U18 logo wall, U19 wrap copy (“we do not auto-debit”), U20 `VITE_*` secrets / `sk_live` / PEM defaults, U21 active provider, PUT JSON keys (`provider`, `secret`, `webhook_secret`, `public_merchant_id`, `environment`, Razorpay `key_id:key_secret`).

### 3.3 `apps/lazuar-pay-merchant/src/auth/bearerToken.test.ts` (adjacent, not 015 rails)

```
isJwtLike: accepts compact JWS and rejects opaque / JWE / empty
pickApiBearerToken: returns undefined when signed out
pickApiBearerToken: sends JWT access_token and never the companion id_token
pickApiBearerToken: does not fall back to JWT id_token when access is opaque or empty
```

Not a C32/B28 cell. Keep. Do not replace with adapter tests.

### 3.4 `canWriteMoney` — **no test file**

Live: `apps/lazuar-pay-merchant/src/lib/roles.ts` `role === 'owner' || role === 'admin'`. U16 depends on it. Host H18 is the API 403. UI hide is untested.

---

## 4. Master matrix — Case × stripe × chip × billplz × xendit × razorpay

Source of rows: 015/00 §7 table, plus C32.1, B28.1, X23.1, R25.1, P19/P20/P23/P24, H13/H14. Cells are **tests**, not code.

| Case | stripe | chip | billplz | xendit | razorpay |
|------|--------|------|---------|--------|----------|
| Empty body 400 (P23, G20, C26, B28, X23, R25) | **exists** `PublicPayTests.Empty_webhook_is_400` (`""`) | **exists** `RailTests.Chip_empty_body_400` (`"  "`) | **missing** | **missing** | **missing** |
| Bad / missing signature 400 (G19, C27, B28, X14, R16) | **exists** `Invalid_signature_is_400` (bad v1). Missing header **missing** | **missing** (C27 ticked) | **missing** (B28 “Bad HMAC 400” ticked) | **missing** (X23 “Bad callback token 400” ticked) | **missing** (R25 “Bad signature 400” ticked) |
| Paid → one `RCPT-` + balanced journal | **exists** `Completed_session_writes_receipt_and_replay_is_noop` | **exists** `Chip_start_and_paid_webhook` | **partial** `Billplz_paid_form_and_localhost_blocked` (count 1, no `RCPT-`, no journal) | **partial** `Xendit_paid_and_settled` (count 1, no `RCPT-`, no journal) | **partial** `Razorpay_captured` (count 1, 2 lines, no `RCPT-` prefix) |
| Replay `{ duplicate: true }`, still one doc (G21, C25, B28, X23, R25) | **exists** same method | **exists** same CHIP paid method | **missing** | **missing** (SETTLED ≠ replay of PAID; event ids `paid:` vs `settled:`) | **missing** |
| Not-paid ignored, zero docs (G22 / H19 / H20 / C21 / B21 / X17 / R18) | **exists** setup; **partial** zero | **exists** preauthorized | **missing** unpaid | **partial** SETTLED-after-PAID only; EXPIRED/PENDING **missing** | **missing** `payment.failed` |
| Start returns `redirect_url` (HTTP mocked) | **blocked** Stripe.net, no FakePsp | **partial** start 200, no `redirect_url` assert, no `Provider`/`ProviderSessionId` | **partial** start success + sandbox host, no `redirect_url` JSON assert | **partial** start success, no `redirect_url` JSON assert | **partial** start success, no `short_url` mapped assert |
| Missing Brand / Collection 400 on PUT | **n/a** | **exists** `Chip_put_requires_brand_id` | **missing** B27 | **n/a** | **n/a** |
| Missing Brand / Collection on **start** → 503 | **n/a** | **missing** C31 start | **missing** B27 start | **n/a** | **n/a** |
| Email required on start (P19, C30, B26, X22, R24) | **n/a** (optional; untested that missing email still starts — blocked Stripe.net) | **exists** `Chip_start_without_email_is_400` | **missing** | **missing** | **missing** |
| Placeholder `customer@example.com` 400 (P20) | **n/a** | **missing** | **missing** | **missing** | **missing** |
| Cross-org checkout 400 (H13, C32) | **exists** `Cross_org_checkout_is_400` | **missing** | **missing** | **missing** | **missing** |
| Amount mismatch does not mint `RCPT-` (H14) | **missing** (H14.4 ticked) | **missing** | **missing** | **missing** | **missing** |
| Missing / unusable currency fail-closed (C24, X19, R20, H14) | **missing** (Stripe `TryNormalizeCurrency` may null; no test) | **missing** (C32 “Missing currency no pay” ticked) | **n/a** (parser hard-codes `Currency = "MYR"` — honesty gap is code, not a missing clone of C24) | **missing** | **missing** |
| Localhost callback start 400 without PSP HTTP (B15, B28) | **n/a** | **n/a** | **missing** (method **name** claims it; factory PublicBaseUrl is https public) | **n/a** | **n/a** |
| Extra-fields HMAC then without-extra (B19) | **n/a** | **n/a** | **missing** as two fixtures (`paid_at` included vs excluded) | **n/a** | **n/a** |
| SETTLED after PAID still one doc (X16) | **n/a** | **n/a** | **n/a** | **exists** `Xendit_paid_and_settled` | **n/a** |
| Fixture `tax`/`fee` still two journal lines (R21) | **n/a** | **n/a** | **n/a** | **n/a** | **exists** inside `Razorpay_captured` |
| No `Razorpay.Api` in csproj (R14) | **n/a** | **n/a** | **n/a** | **n/a** | **exists** `IsolationTests` |
| Unknown `{provider}` 400 (P22) | **exists** webhook `paypal` | same allow-list (not separately posted as `chip` misspelling) | same | same | same. **PUT** unknown **missing** |
| Rail not configured webhook 400 (P24) | **missing** (empty body 400s first; no POST with body + no PUT) | **missing** | **missing** | **missing** | **missing** |
| Missing org webhook secret 503 when rail configured (H10/H11) | **exists** `Missing_webhook_secret_is_503_when_rail_configured` | **missing** (CHIP PEM missing would 503 via same catch) | **missing** | **missing** | **missing** |
| Writer member 403 PUT gateway (H18) | **exists** `Member_cannot_put_gateway` | not repeated per rail (same endpoint; **enough** if one 403) | same | same | same |
| Member GET gateway metadata 200 (H18/S18) | **missing** | **missing** | **missing** | **missing** | **missing** |
| GET never echoes secret (S18) | **exists** `Put_and_get_does_not_echo_secret` | **missing** (PEM must not echo) | **missing** | **missing** | **missing** |
| `capability: hosted_link` | **exists** inside PUT/GET stripe | **missing** on chip GET | **missing** | **missing** | **missing** |
| No `force_recurring` / `skip_capture` on start (C15) | **n/a** | **exists** `LastBody` assert | **n/a** | **n/a** | **n/a** |
| Public GET `email_required` hint (P19.2 / K11) | **missing** (should be false) | **missing** (should be true when active=chip) | **missing** | **missing** | **missing** |

A99.2 on this SHA says: “Hermetic tests exist for `stripe`, `chip`, `billplz`, `xendit`, `razorpay`: paid + replay + not-paid”. Live tests: **stripe yes**, **chip yes**, **billplz paid only**, **xendit paid+settled only**, **razorpay paid+tax-lines only**. A99.2 is **false** for three rails.

---

## 5. H18–H25, G25, P23 scored against live methods

### 5.1 G25 — Hermetic webhook tests (013, Stripe-shaped)

| G25 cell | Live | Verdict |
|----------|------|---------|
| G25.1 FakeHttp or stub signature | `PayApiFactory` + `Sign` HMAC; CHIP RSA in `RailTests`; **no** Stripe.net HTTP | **exists** for verify; start Stripe **blocked** |
| G25.1 Fixture wrap key + webhook secret | Testing allows git `lazuar-pay-dev-wrap-key`; tests PUT `webhook_secret` | **exists** for Testing; Production wrap **missing** (H16) |
| G25.1 Health 200 if PSP handler would throw | `Health_still_skips_one` / `Health_does_not_call_one` throw **One**, not Psp | **partial** |
| G25.2 Bad / missing signature → 4xx | `Invalid_signature_is_400` | **exists** bad; **missing** missing-header |
| G25.2 Empty body → 400 | `Empty_webhook_is_400` | **exists** Stripe |
| G25.2 Two posts same event_id → 200 no-op, fulfill once | `Completed_session_writes_receipt_and_replay_is_noop` | **exists** serial. Concurrent 23505 **missing** (H24.3 optional) |
| G25.2 Setup / amount≤0 / skip_capture-without-token | `Setup_mode_is_ignored` exists; `Zero_amount_session_is_ignored` partial; CHIP skip_capture is C15 start grep not a webhook | **partial** |
| G25.3 Do not skip on “Stripe not configured” | no `[Ignore]` | **exists** |
| G25.3 IsolationTests still green | IsolationTests exist; this agent did not run them | **exists as tests**, run status unverified here |

014 papers said G22/G25 setup-not-paid was missing. **On this SHA that hole is closed** by `Setup_mode_is_ignored`. Do not re-open it as a 014 finding. Do record H20 assertion holes and the four-rail clones G25 never named.

### 5.2 H18 — Member 403 on PUT gateway

| H18 cell | Live | Verdict |
|----------|------|---------|
| Member PUT `/v1/orgs/t1/gateway` → 403 | `GatewayTests.Member_cannot_put_gateway` | **exists** |
| Owner PUT still 200 | `Put_and_get_does_not_echo_secret` | **exists** (bundled) |
| Member GET still 200 metadata | none | **missing** → write `GatewayTests.Member_can_get_gateway_metadata` |
| Do not invent One role `viewer` | no such string in tests | ok |

### 5.3 H19 — Stripe `mode=setup` is not paid

**exists:** `WebhookTests.Setup_mode_is_ignored`. Fixture has `"mode": "setup"`, open checkout amount 10, 200, `ignored`, zero documents, checkout `open`. Strengthen optionally: `Does.Contain("setup")` because live reason is `setup_or_zero`.

### 5.4 H20 — Stripe amount 0 is not paid

**partial:** `WebhookTests.Zero_amount_session_is_ignored`. Fixture is correct (`mode=payment`, `amount_total:0`, `payment_status:paid`). Missing asserts: body `ignored`, checkout still `open`. **Do not add a second method.** Strengthen this one. See §9.1.

### 5.5 H21 — IsolationTests ban Hub adapter type names

**exists** in `BannedSrc` and `Source_does_not_use_mediatr_or_hub_modules`. H21.3 “fail a deliberate string in a scratch test then remove” is process, not a remaining CI test.

### 5.6 H22 — No Stripe Connect `application_fee`

**partial.** `ApplicationFeeAmount` is banned. Add `application_fee`, `TransferData`, `transfer_data` to `BannedSrc` (same method, extra tokens). Do not add a new test class.

### 5.7 H23 — Audit row on gateway PUT

**partial.** `Put_and_get_does_not_echo_secret` asserts `AuditEvents.Any(a => a.Action == "gateway.credentials.upsert")`. Missing: audit row has no last4/secret (row type has `Action`/`OrgId`/`At` only — assert `db.AuditEvents.Single().Action` and that GET JSON last4 is not written into an audit payload if one is added later). Strengthen, or add `GatewayTests.Put_audit_does_not_store_secret` if an extra JSON column appears. On this SHA the row cannot store a secret; one extra assert on Action+OrgId is enough.

### 5.8 H24 — Unique violation is 200 duplicate

**partial.** Serial replay exists (Stripe + CHIP). Concurrent two-POSTs **missing**. H24.3 says concurrent is optional on InMemory. **Do not require** a concurrent InMemory test. Optional Postgres-only test is out of `task pay:test` hermetic factory. Implementer: skip concurrent unless they add a Postgres test collection. Document in the test comment that InMemory cannot provoke 23505 reliably.

Live `WebhookEndpoints` catches `DbUpdateException` and returns `{ duplicate: true }`. Unproven on InMemory unique indexes.

### 5.9 H25 — Fulfill throw rolls back event id

**missing.** No test double for `Fulfillment`. H25.2: “If not injectable without a seam, document that H12 one-TX is the proof and skip a fake 5xx — do **not** fake it by SaveChanges-then-throw.”

On this SHA there is **no** comment in `WebhookTests` that H25 was skipped. The handler **does** `BeginTransaction` + insert + `FulfillPaidAsync` + `Commit`. That is H12 code, not H25 proof.

**Write** `WebhookTests.Fulfill_throw_returns_5xx_and_retry_pays` **only if** `PayApiFactory` can replace `Fulfillment` with a throwing decorator. If the team refuses a seam, the implementer must add a 10-line comment on `WebhookTests` citing H25.2 and **not** tick H25 as tested. Do not SaveChanges-then-throw inside production code to make a test.

Suggested seam (do not invent a Hub port): in `ConfigureTestServices`, `services.AddSingleton<Fulfillment>(_ => new ThrowingFulfillment(...))` is impossible today because `Fulfillment` is a concrete class registered however `Program` registers it. Implementer may add `internal virtual` or a tiny `IFulfillPaid` **in the Pay host** if they need the test. That seam is in scope for H25; a factory of five adapters is not.

### 5.10 P23 — Empty body 400 on every rail

| Rail | Method | Verdict |
|------|--------|---------|
| stripe | `PublicPayTests.Empty_webhook_is_400` | **exists** (`""`) |
| chip | `RailTests.Chip_empty_body_400` | **exists** (`"  "`) |
| billplz | — | **missing** |
| xendit | — | **missing** |
| razorpay | — | **missing** |

Shared production check is `string.IsNullOrWhiteSpace(raw)` **before** `switch (name)`. One implementation, three untested path names. P23.2 said “When each rail lands, add empty-body 400 (C26, B28, X23, R25)”. C26 landed a test. B28/X23/R25 ticked the bullet without methods.

Unknown provider runs **before** empty check, so `/v1/webhooks/paypal/t1` with empty body is `unknown provider`, not `empty body`. P22.1 allows unknown to win. Do not “fix” that in a test.

---

## 6. Adjacent 015 test claims that are ticked and false (or partial)

These are not the user-named matrices but they leak into A99 and will be mis-cloned.

| Phase | Claim | Live |
|-------|-------|------|
| T16 | Hermetic null SST still `RCPT-`, Title Official Receipt, checkout paid, no SST string | Accidental path: PUT gateway inserts `OrgSettings` with `SstRegistered` default null; `Completed_session_…` mints `RCPT-`. **No** assert on Title / paid / SST string. **partial** |
| H14 | `amount_total` 999 vs checkout 10.00 does not mint `RCPT-` | **missing** all rails |
| H11 | Production cannot verify with only process env | **missing** (`PayApiFactory` is Testing) |
| H16 | Production missing wrap key cannot use git string | **missing** (no `SecretBoxTests`) |
| P15 | GET `?provider=chip` does not change active | **missing** |
| P22 | PUT unknown provider 400 | **missing** (webhook paypal exists) |
| P24 | POST webhook stripe/t1 with no PUT keys → 400 | **missing** (need a **non-empty** body so P23 does not win) |
| C17 | start asserts `redirect_url`, `Provider=chip`, `ProviderSessionId` | **partial** |
| C22 | `purchase.payment_failure` ignore; failure then paid still mints | **missing** |
| C24 | missing currency no pay | **missing** |
| B15 | localhost PublicBaseUrl start 400 without HTTP | **missing** |
| B19 | extra-excluded HMAC still 200; extra-included still 200; wrong secret 400 | **missing** as three methods |
| B21 | unpaid ignore | **missing** |
| B26 / X22 / R24 | email required on those starts | **missing** (only CHIP) |
| B27 | PUT without collection 400 | **missing** |
| X17 | EXPIRED fixture | **missing** |
| X19 / R20 | missing currency | **missing** |
| R18 | `payment.failed` ignore | **missing** |
| R19 | header Event-Id or `captured:{pay_}`, never bare `pay_` | **missing** as assert on unique grain |
| C28 | grep registrar | **missing** Isolation token |
| B23 | grep `PublicDnsFallback` | **missing** Isolation token |
| T17 | Isolation LHDN/UBL | **missing** tokens |
| K11–K16 | UI behaviour | only K12/K15/K17 greps |
| U16–U21 | merchant field sets / wrap copy / VITE | **missing** vitest (UI code exists) |

---

## 7. What the four-rail tests actually lock (so clones do not double-count)

### 7.1 CHIP — closest to C32, still not the bundle

Exists:

- `Chip_start_and_paid_webhook` — start mock + paid + replay + no `force_recurring` + `checkout_id` in body + `RCPT-` + balanced journal
- `Chip_preauthorized_is_ignored`
- `Chip_start_without_email_is_400`
- `Chip_empty_body_400`
- `Chip_put_requires_brand_id`

C32.1 still open: bad/missing RSA 400; missing currency no pay; cross-org on chip path.

C32 asked “one test class (or file) a later implementer can clone for Billplz.” Live CHIP cases live in `RailTests` mixed with Billplz/Xendit/Razorpay. **Do not split files as a requirement of this inventory.** Add methods on `RailTests` with the `Chip_` prefix. A later cleanup may split `ChipTests.cs`; that is not a gap.

### 7.2 Billplz — one overloaded method, B28 mostly missing

Exists (partial): `Billplz_paid_form_and_localhost_blocked` — sandbox host + paid form HMAC + one document.

Rename is not required. **Do not** keep claiming localhost in new tests. Write `Billplz_localhost_callback_start_is_400_without_psp_http` as a **separate** method that **overrides** `Pay:PublicBaseUrl`.

### 7.3 Xendit — PAID + SETTLED only

Exists: `Xendit_paid_and_settled`.

SETTLED is X16, not X23 “PAID → RCPT- + replay”. Replay of the **same PAID body** is a different event-id grain (`paid:{id}`) and is **missing**.

### 7.4 Razorpay — captured + two lines (tax ignored)

Exists: `Razorpay_captured` (R17 partial + R21).

R25 empty / bad sig / failed / replay are **missing**. Journal count 2 is the tax lock; still add `RCPT-` and debit=credit on strengthen.

---

## 8. Honesty vs ticked checklists

On HEAD `c621ceba` the following phase files are `[x]` while live tests do not contain the named case:

- **C32.1** Bad/missing RSA 400; missing currency; chip cross-org  
- **C27.2** “Test green” — no `Chip_*signature*` method  
- **C24.2** “Test fixture without currency” — no method  
- **C22.2** “failure then paid” — no method  
- **B28.1** Empty body; bad HMAC; extra-fields HMAC variant; unpaid ignore; localhost start; replay  
- **B15.3** Default local PublicBaseUrl start — no method; the named method uses https public base  
- **B19.2** three HMAC fixtures  
- **B21.2** unpaid  
- **B26.2 / B27.2** email / collection tests  
- **X23.1** Empty body; bad token; PAID replay; EXPIRED; missing email  
- **X17.2** EXPIRED fixture  
- **X22.2** start email  
- **R25.1** Empty body; bad signature; replay; `payment.failed`  
- **R18.2** failed ignore  
- **R24.2** start email  
- **H14.4** amount 999 vs 10.00  
- **H18.1** Member GET 200  
- **H20.1** checkout still open / ignored body (partial method)  
- **H25.2** no throwing fulfill test and no skip comment  
- **P23.2** empty-body for Billplz/Xendit/Razorpay  
- **P20.3** hermetic 400 placeholder — no method on any rail  
- **P24.3** webhook with no PUT keys  
- **T16.1** Title / SST string / checkout paid  
- **A99.2** paid+replay+not-paid for all five names  

016 must not treat those ticks as a test plan that is already done. The list in §10 is the remaining work.

---

## 9. Strengthen existing methods first (not new methods)

An implementer must **edit** these eight methods before adding new ones, so we do not clone a weak paid test five times.

### 9.1 `WebhookTests.Zero_amount_session_is_ignored` (H20)

After the existing `Documents.Count == 0` add:

- `Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("ignored"));`
- `Assert.That(db.Checkouts.Single().Status, Is.EqualTo("open"));`

Do **not** add `Zero_amount_session_is_ignored_keeps_checkout_open`.

### 9.2 `WebhookTests.Setup_mode_is_ignored` (H19 tighten)

Optional: `Does.Contain("setup")` to match live `setup_or_zero`. Not a new method.

### 9.3 `WebhookTests.Completed_session_writes_receipt_and_replay_is_noop` (T16)

Add:

- `Assert.That(db.Documents.Single().Title, Is.EqualTo("Official Receipt"));`
- `Assert.That(db.Checkouts.Single().Status, Is.EqualTo("paid"));`
- `Assert.That(await first.Content.ReadAsStringAsync(), Does.Not.Contain("SST registration unknown"));`
- After PUT, `Assert.That(db.OrgSettings.Single().SstRegistered, Is.Null);`

### 9.4 `RailTests.Chip_start_and_paid_webhook` (C17)

Parse start JSON; `redirect_url` equals stub `https://gate.chip-in.asia/p/x`. After start, `db.Checkouts.Single().Provider == "chip"` and `ProviderSessionId == "purch_1"`. Assert `LastBody` contains `"org_id"` (C14).

### 9.5 `RailTests.Billplz_paid_form_and_localhost_blocked` (B28 paid)

Keep the method (do not rename in this program unless the implementer is already touching it — then rename to `Billplz_paid_form_sandbox_start`). Add `Documents.Single().Number` starts with `RCPT-`. Add a replay POST of the same form+HMAC; body contains `duplicate`; still one document. **Do not** assert localhost here.

### 9.6 `RailTests.Xendit_paid_and_settled` (X23 paid + X16)

After PAID: `RCPT-`, debit=credit, checkout paid. After SETTLED: body contains `settled` or `ignored`; still one document. Add a **third** POST of the **original PAID** payload in a **new** method `Xendit_paid_replay_is_duplicate` (replay is a gap, not a strengthen of SETTLED).

### 9.7 `RailTests.Razorpay_captured` (R21 keep, R25 paid)

Add `Number` starts with `RCPT-`, debit sum = credit sum. Replay goes to new method `Razorpay_captured_replay_is_duplicate`.

### 9.8 `IsolationTests.Source_does_not_use_mediatr_or_hub_modules` (H21/H22/T17/C28/B23)

Append to `BannedSrc` (same method):

```
"application_fee", "TransferData", "transfer_data",
"ChipWebhookRegistrar", "PublicDnsFallback",
"Lhdn", "MyInvois", "UBL", "XAdES", "Irbm"
```

Do **not** add `lazuar-local-dev.com` (false positive in `BillplzHosted`). Do **not** add `/webhooks/` (false positive: Pay’s own route).

### 9.9 `GatewayTests.Put_and_get_does_not_echo_secret` (H23/P12)

Assert GET `webhook_configured == true`. Assert audit `OrgId == "t1"` and Action as today.

---

## 10. Tests to write — one method per gap

Class names are existing unless noted. Fixtures: `PayApiFactory`, owner One responder as in `RailTests.Owner`, `Put` / `SeedCheckout` copies. Do not call live PSP. Do not add `Razorpay.Api`. Do not add MediatR.

Each item is **in scope**. Do not add parked refunds / off-session / LHDN math tests.

### 10.1 Stripe / `WebhookTests` (H14, H11, H15, H25, P24, G25 missing-header)

**1. `WebhookTests.Missing_stripe_signature_header_is_400`**  
Fixture: `PayApiFactory` default, `Owner`, `SeedRailAndCheckout`.  
POST `/v1/webhooks/stripe/t1` with a completed-session JSON **and no** `Stripe-Signature`.  
Assert 400, `Documents.Count == 0`.  
Maps: H10.1, G25.2 missing signature.

**2. `WebhookTests.Amount_mismatch_does_not_mint_receipt`**  
Seed checkout amount **10**. Signed `checkout.session.completed` `mode=payment` `amount_total:999` (H14.4).  
Assert 400 (live `WebhookEndpoints` amount mismatch is 400), `Documents.Count == 0`, checkout `open`.  
Maps: H14.

**3. `WebhookTests.Currency_mismatch_does_not_mint_receipt`**  
Checkout MYR. Signed session `currency: usd`, `amount_total:1000`.  
Assert 400, zero documents.  
Maps: H14 currency.

**4. `WebhookTests.Unknown_event_type_is_ignored`**  
Signed `type: charge.refunded` (or `customer.subscription.updated`) with a valid structure Stripe.net will parse.  
Assert 200, body `ignored`, zero documents, checkout `open`.  
Maps: H15. Do not fulfill refunds (parked).

**5. `WebhookTests.Rail_not_configured_is_400_when_body_present`**  
**No** PUT gateway. POST `/v1/webhooks/stripe/t1` with non-empty `{"id":"evt_x"}` and optional junk signature.  
Assert 400, body contains `rail not configured` (live string).  
Maps: P24.3. Empty body would hit P23 first — this method must use a non-empty body.

**6. `WebhookTests.Production_missing_org_whsec_is_503_even_if_process_env_set`**  
New factory settings: `UseEnvironment("Production")`, `Pay:WrapKey` = 32-byte base64 (required or SecretBox throws), `Pay:StripeWebhookSecret` = `whsec_process`. PUT stripe keys then **null** `WebhookCiphertext`. Sign payload with `whsec_process`. POST.  
Assert **503**, not 200.  
Maps: H11. If Production boot needs extra config, set only those two keys; do not hit real Stripe.

**7. `WebhookTests.Fulfill_throw_returns_5xx_event_not_committed_retry_pays`**  
**Only if** `Fulfillment` is replaceable in `ConfigureTestServices`. First POST signed paid → 5xx, `Documents.Count == 0`, `PspWebhookEvents.Count == 0`. Restore real `Fulfillment`. Second POST same event → 200, one `RCPT-`.  
Maps: H25. If not replaceable: do **not** write a fake 5xx; add a comment on `WebhookTests` citing H25.2 instead. That comment is the deliverable, not a silent skip.

**8. `SecretBoxTests.Production_missing_wrap_key_throws`** (new class `SecretBoxTests.cs`)  
Construct `SecretBox` with empty `Pay:WrapKey` and `IHostEnvironment` Production. `Protect("x")` throws `Pay:WrapKey is required in Production`.  
Maps: H16.  
Companion **9. `SecretBoxTests.Testing_allows_dev_wrap_key`**: Testing + missing WrapKey, `Protect`/`Unprotect` round-trip. Proves tests still green without a committed real key.

### 10.2 CHIP / `RailTests` (C32 remainder, C22, C24, C27, C31 start, P20, H13, H14)

**10. `RailTests.Chip_bad_signature_is_400`**  
PUT chip with real PEM. POST valid `purchase.paid` JSON, header `X-Signature: aGVsbG8=` (garbage base64).  
Assert 400, zero documents.  
Maps: C27 / C32.

**11. `RailTests.Chip_missing_signature_header_is_400`**  
Same PUT, same JSON, **no** `X-Signature`.  
Assert 400, zero documents.  
Maps: C27 missing header.

**12. `RailTests.Chip_missing_currency_does_not_pay`**  
Signed `purchase.paid` with purchase `total:1000` and **no** `currency` (or `currency:""`).  
Assert 400 (live `PspVerifyException("missing currency")`), zero documents, checkout `open`.  
Maps: C24 / C32.

**13. `RailTests.Chip_payment_failure_is_ignored`**  
Signed `event_type: purchase.payment_failure`, purchase id `purch_fail`.  
Assert 200, body contains `payment_failure`, zero documents.  
Maps: C22.

**14. `RailTests.Chip_failure_then_paid_still_mints_one_receipt`**  
Same purchase id: first `purchase.payment_failure` (unique `failed:{id}`), then `purchase.paid` total 1000 MYR metadata checkout.  
Assert one `RCPT-` after the second POST.  
Maps: C22.1 namespace.

**15. `RailTests.Chip_cross_org_checkout_is_400`**  
Clone `WebhookTests.Cross_org_checkout_is_400` on `/v1/webhooks/chip/t2` with t1 checkout in CHIP metadata, t2 chip keys + PEM.  
Assert 400, zero documents.  
Maps: C32 H13.

**16. `RailTests.Chip_placeholder_email_is_400`**  
Active chip. Start `{"name":"Ada","email":"customer@example.com"}`.  
Assert 400, `Psp.LastUri` is null (no HTTP).  
Maps: P20 / C30.

**17. `RailTests.Chip_start_without_brand_id_is_503`**  
PUT chip **with** brand, then DB-clear `PublicMerchantId` (or PUT is 400 without it — this is start-time incomplete). Start with email.  
Assert 503, body rail not configured, `Psp` not called.  
Maps: C31 start.

**18. `RailTests.Chip_amount_mismatch_does_not_pay`**  
Checkout amount 10. Signed paid `total: 999` MYR.  
Assert 400, zero documents.  
Maps: H14 CHIP.

### 10.3 Billplz / `RailTests` + `GatewayTests` (B28 remainder)

**19. `RailTests.Billplz_empty_body_400`**  
PUT billplz collection+env. POST `/v1/webhooks/billplz/t1` content `"  "` `application/x-www-form-urlencoded`.  
Assert 400.  
Maps: B28 / P23.

**20. `RailTests.Billplz_bad_hmac_is_400`**  
Valid form `paid=true` with `x_signature=deadbeef`.  
Assert 400, zero documents.  
Maps: B28 / B19.2 wrong secret.

**21. `RailTests.Billplz_hmac_with_extra_fields_paid`**  
Form includes `paid_at`, `transaction_id`, `transaction_status` **and** `paid=true`. HMAC via `ComputeHmac(..., excludeExtra: false)`. Query `checkout_id`.  
Assert 200, one `RCPT-`.  
Maps: B19 extra-included.

**22. `RailTests.Billplz_hmac_without_extra_fields_paid`**  
Same extra fields present in the form, but signature computed with `excludeExtra: true` (Hub with-extra first **fails**, without-extra **passes**).  
Assert 200, one `RCPT-`.  
Maps: B19 extra-excluded. Use a **fresh** checkout so it does not collide with 21.

**23. `RailTests.Billplz_unpaid_is_ignored`**  
Valid HMAC, `paid=false`, `state=due`.  
Assert 200, body contains `unpaid`, zero documents, checkout `open`.  
Maps: B21 / B28.

**24. `RailTests.Billplz_localhost_callback_start_is_400_without_psp_http`**  
Do **not** use default factory PublicBaseUrl. `builder.UseSetting("Pay:PublicBaseUrl", "http://localhost:8081")` on a `PayApiFactory` subclass or extra property. PUT billplz. Start with email.  
Assert 400 (live maps `callback base` → 400), body contains `callback base not public`, `Psp.LastUri` is null (no bills HTTP).  
Also one variant `https://127.0.0.1/` and one `https://foo.lazuar-local-dev.com` if cheap — **same method** with two extra POSTs, not three classes.  
Maps: B15 / B28 localhost. This is the test the current method name pretended to be.

**25. `RailTests.Billplz_start_without_email_is_400`**  
Clone `Chip_start_without_email_is_400` with provider billplz + collection + env.  
Maps: B26 / P19.

**26. `RailTests.Billplz_placeholder_email_is_400`**  
Start email `customer@example.com`. 400, no PSP HTTP.  
Maps: P20 / B26.

**27. `GatewayTests.Billplz_put_requires_collection_id`**  
PUT `{provider:billplz, secret, webhook_secret, environment:test}` without `public_merchant_id`.  
Assert 400.  
Maps: B27 PUT.

**28. `GatewayTests.Billplz_put_requires_environment`**  
PUT billplz with collection but **no** `environment`. Live `GatewayEndpoints` 400 `"environment is required"`.  
Assert 400.  
Maps: B11.

**29. `RailTests.Billplz_start_without_collection_is_503`**  
PUT valid billplz, clear `PublicMerchantId` on the row, start with email.  
Assert 503, no PSP HTTP.  
Maps: B27 start.

**30. `RailTests.Billplz_cross_org_is_400`**  
t1 checkout id in query on `/v1/webhooks/billplz/t2` with t2 keys. Valid HMAC.  
Assert 400, zero documents.  
Maps: H13 Billplz.

**31. `RailTests.Billplz_amount_mismatch_does_not_pay`**  
`paid_amount=999` vs checkout 10.00 (1000 sen). Valid HMAC.  
Assert 400, zero documents.  
Maps: H14 Billplz.

**32. `RailTests.Billplz_join_via_reference_1_when_query_missing`**  
No `?checkout_id=` query. Form `reference_1={checkoutId}` + paid HMAC.  
Assert 200, one `RCPT-`.  
Maps: B16 / B17. (Live parser: query, then form `checkout_id`, then `reference_1`.)

### 10.4 Xendit / `RailTests` (X23 remainder)

**33. `RailTests.Xendit_empty_body_400`**  
PUT xendit. POST `/v1/webhooks/xendit/t1` `"  "`. 400.  
Maps: X23 / P23.

**34. `RailTests.Xendit_bad_callback_token_is_400`**  
PAID JSON, header `x-callback-token: wrong`. 400, zero documents.  
Maps: X23 / X14.

**35. `RailTests.Xendit_missing_callback_token_is_400`**  
PAID JSON, no header. 400.  
Maps: X14.

**36. `RailTests.Xendit_expired_is_ignored`**  
Valid token, `"status":"EXPIRED"`. 200, body ignored/EXPIRED, zero documents.  
Maps: X17 / X23.

**37. `RailTests.Xendit_pending_is_ignored`**  
`"status":"PENDING"`. Same asserts.  
Maps: X17. (FAILED may share this method as a second POST with a new invoice id — keep **one method**, two statuses, to avoid exploding. If the implementer prefers purity, split `Xendit_failed_is_ignored`. Do not add SETTLED here; SETTLED already exists.)

**38. `RailTests.Xendit_paid_replay_is_duplicate`**  
POST same PAID body twice. Second contains `duplicate`, one document, `RCPT-`.  
Maps: X23 replay. Distinct from SETTLED.

**39. `RailTests.Xendit_start_without_email_is_400`**  
Maps: X22 / P19.

**40. `RailTests.Xendit_placeholder_email_is_400`**  
Maps: P20 / X22.

**41. `RailTests.Xendit_missing_currency_does_not_pay`**  
PAID JSON without `currency`. 400, zero documents.  
Maps: X19.

**42. `RailTests.Xendit_cross_org_is_400`**  
Maps: H13 Xendit.

**43. `RailTests.Xendit_amount_mismatch_does_not_pay`**  
`paid_amount: 9.99` vs checkout 10. Live `AmountMinor = MoneyMath.ToMinor(amount)` then compare to checkout minor. 400.  
Maps: H14 Xendit. **Do not** send `paid_amount: 1000` thinking Xendit is cents — live parser treats numbers as **major** then `ToMinor`.

### 10.5 Razorpay / `RailTests` + `GatewayTests` (R25 remainder)

**44. `RailTests.Razorpay_empty_body_400`**  
PUT razorpay `key_id:secret`. POST `"  "`. 400.  
Maps: R25 / P23.

**45. `RailTests.Razorpay_bad_signature_is_400`**  
Valid captured JSON, `X-Razorpay-Signature: deadbeef`. 400.  
Maps: R25 / R16.

**46. `RailTests.Razorpay_missing_signature_is_400`**  
No header. 400.  
Maps: R16.

**47. `RailTests.Razorpay_payment_failed_is_ignored`**  
`"event":"payment.failed"`, valid HMAC, notes checkout_id. 200, body contains `payment_failed` or ignored, zero documents, checkout `open`.  
Maps: R18 / R25.

**48. `RailTests.Razorpay_failed_then_captured_still_pays`**  
Same `pay_1`: failed (event id `failed:pay_1` if no header), then captured **without** `X-Razorpay-Event-Id` so paid grain is `captured:pay_1`. One `RCPT-`.  
Maps: R18 namespace / R19 never bare `pay_`.

**49. `RailTests.Razorpay_captured_replay_is_duplicate`**  
Two identical captured POSTs. Second `duplicate`, one document.  
Maps: R25 replay.

**50. `RailTests.Razorpay_event_id_prefers_header`**  
Captured body pay_1, header `X-Razorpay-Event-Id: evt_header_1`. After 200, `PspWebhookEvents` has `EventId == "evt_header_1"` not `pay_1` and not `captured:pay_1`.  
Maps: R19.

**51. `RailTests.Razorpay_start_without_email_is_400`**  
Maps: R24 / P19. Live `PayProviders.RequiresEmail` is true for every non-stripe.

**52. `RailTests.Razorpay_placeholder_email_is_400`**  
Maps: P20.

**53. `GatewayTests.Razorpay_put_requires_key_id_colon_secret`**  
PUT `{provider:razorpay, secret:"nocolon", webhook_secret:"wh"}`. 400 `"secret must be key_id:key_secret"`.  
Maps: R12.

**54. `RailTests.Razorpay_missing_currency_does_not_pay`**  
Captured entity without `currency`. 400.  
Maps: R20.

**55. `RailTests.Razorpay_cross_org_is_400`**  
Maps: H13.

**56. `RailTests.Razorpay_amount_mismatch_does_not_pay`**  
Entity `amount: 999` (cents) vs checkout 10. 400.  
Maps: H14. Razorpay amounts are **already minor** in live parser (`GetInt64`).

### 10.6 Gateway door / `GatewayTests` (H18 GET, P15, P22 PUT, P14 chip)

**57. `GatewayTests.Member_can_get_gateway_metadata`**  
Owner PUT stripe. Switch One responder to `Role("member")`. GET `/v1/orgs/t1/gateway` with Bearer.  
Assert 200, `configured true`, `provider stripe`, `capability hosted_link`, JSON does not contain `sk_test` or `whsec`.  
Maps: H18 Member GET / S18.

**58. `GatewayTests.Put_unknown_provider_is_400`**  
Owner PUT `{provider:"paypal", secret:"x", webhook_secret:"y"}`. 400 unknown provider.  
Maps: P22 PUT / A99.2.

**59. `GatewayTests.Get_optional_provider_query_does_not_change_active`**  
PUT stripe (active stripe). PUT chip with brand+PEM (active becomes chip — live PUT always sets active). Then: this test should PUT stripe, then PUT chip? Live PUT sets active to the provider just pasted. To test P15: PUT stripe, PUT chip **is** the way to have two rows — but second PUT flips active to chip. Sequence: PUT chip, PUT stripe (active stripe), GET `?provider=chip` → configured chip metadata, then GET without query → provider stripe, `OrgSettings.ActiveProvider` still stripe.  
Maps: P15.

**60. `GatewayTests.Get_unknown_provider_query_is_400`**  
GET `/v1/orgs/t1/gateway?provider=paypal`. 400.  
Maps: P15 unknown.

**61. `GatewayTests.Put_chip_get_active_is_chip_not_stripe`**  
PUT chip brand+PEM. GET no query. `provider == chip`, `capability == hosted_link`, `public_merchant_id` present, no PEM in JSON.  
Maps: P14.

### 10.7 Public GET / start / `PublicPayTests`

**62. `PublicPayTests.Email_required_true_when_active_chip`**  
PUT chip, create checkout, **no** start. GET `/v1/pay/{token}` without Bearer.  
Assert JSON `email_required === true`.  
Maps: P19.2 / K11 host half.

**63. `PublicPayTests.Email_required_false_when_active_stripe`**  
PUT stripe, create checkout, GET public. `email_required === false` (or absent/false).  
Maps: P19 Stripe optional.

**64. `PublicPayTests.Start_without_rail_is_503`**  
Create checkout, **no** PUT gateway. POST `/v1/pay/{token}/start` `{"email":"ada@acme.test"}`.  
Assert 503 `rail not configured`.  
Maps: P24.2 start.

**65. `PublicPayTests.Stripe_whitespace_webhook_is_400`**  
POST `/v1/webhooks/stripe/t1` content `" \n"`. 400. Completes P23 whitespace on stripe (chip already uses whitespace). Optional if the implementer is trimming the list; **include it** so all five names have a whitespace case after 19/33/44.

Do **not** write `Stripe_start_returns_redirect_url` against live Stripe.net. 00 §7 says “Stripe.net hard”. Out of scope until a Stripe client seam exists.

### 10.8 Isolation extras (if not done in §9.8)

If the implementer prefers a dedicated method instead of growing `BannedSrc`:

**66. `IsolationTests.Source_does_not_contain_connect_or_lhdn_or_hub_folklore`**  
Same file walk as `Source_does_not_use_mediatr_or_hub_modules`, extra tokens listed in §9.8. Prefer extending `BannedSrc` over a second walk.

### 10.9 Frontend vitest — checkout `locks.test.ts`

These are **source greps** unless the implementer adds `@testing-library/react`. This inventory does **not** require RTL. Host/UI mismatches are locked by grepping the live strings in `App.tsx`.

**67. `it('requires email when email_required is true')`**  
`App.tsx` contains `email_required` and `emailBlocked` / disable Pay.  
Maps: K11. Today the UI **does** implement this; the lock is missing.

**68. `it('does not treat customer@example.com as satisfying email_required')`**  
**Gap / mismatch:** live `startPay` only checks `!email.trim()`. Host `BuyerEmail.IsUsable` rejects the placeholder. Either:

- change UI (other 016 slice) **and** lock `customer@example.com` in the disable condition, **or**
- write this test **failing** against current `App.tsx` and do not mark K11 done.

This inventory records the mismatch. The test to write, once UI matches host, is: blob must reject placeholder (e.g. `email.trim().toLowerCase() !== 'customer@example.com'`). **Do not** invent a different placeholder.

**69. `it('verifying query is not paid')`**  
`App.tsx` contains `status === 'verifying'` and the verifying heading, and paid UI is gated on `pay.status === 'paid'` not on the query.  
Maps: K14.

**70. `it('polls public GET while verifying')`**  
Contains `/v1/pay/${token}` inside the verifying `setInterval` (or equivalent).  
Maps: K13.

**71. `it('maps start 503 to rail not configured')`**  
Contains `rail not configured` on `response.status === 503`.  
Maps: K16.

**72. `it('maps start 400 without calling it paid')`**  
Contains 400 handling. Optional honesty: the current string `'callback base not public or email required'` conflates B15 and P19 — grep that it does **not** set status paid.

Walk **all** `src` files (today only `App.tsx`) so a future component does not escape the wallet/PAN lock. Change the existing wallet test from `readFileSync(App.tsx)` to a walk like merchant `locks.test.ts`.

### 10.10 Frontend vitest — merchant `locks.test.ts`

**73. `it('staff rails are stripe chip billplz xendit razorpay')`**  
`WorkspacePage.tsx` `rails` array equals those five lowercase names.  
Maps: U10 vs `PayProviders.All`. Host/UI mismatch if someone adds `fiuu`.

**74. `it('hides paste unless canWriteMoney')`**  
`WorkspacePage` uses `canWriteMoney` and the member copy `Cannot paste keys`.  
Maps: U16.

**75. `it('canWriteMoney is owner or admin only')`** — new `roles.test.ts`  
`canWriteMoney('owner')` true, `admin` true, `member` false, `viewer` false, null false.  
Maps: U16 / H18.2 do not invent viewer in **One**; the unit test may still assert viewer is not a writer.

**76. `it('no VITE secrets or sk_live defaults')`**  
Walk merchant `src` + `.env*` if present: forbid `VITE_STRIPE_SECRET`, `sk_live_`, `whsec_` as defaults, `BEGIN PUBLIC KEY` committed, `VITE_CHIP`. Allow `VITE_PAY_API_URL`.  
Maps: U20.

**77. `it('wrap copy says we do not auto-debit for billplz xendit razorpay')`**  
The `copy` record contains `do not auto-debit` (or live wording) for those three. CHIP copy must not claim off-session this program.  
Maps: U19.

**78. `it('has no five-logo wallet wall')`**  
Same grabpay/tng/boost/duitnow/fpx/shopee forbid as checkout, on merchant `src` **except** CHIP copy may mention `FPX/wallets if enabled on the brand` as **text**. Lock: no `<img` of those brands; the CHIP sentence is allowed.  
Maps: U18. If a naive grep of `fpx` fails on CHIP copy, assert via `not.toMatch(/<img[^>]*(grab|tng|fpx)/i)`.

**79. `it('PUT body uses host field names')`**  
`pasteKey` JSON keys: `provider`, `webhook_secret`, `secret` or razorpay `` `${keyId}:${keySecret}` ``, `public_merchant_id` for chip/billplz, `environment` for billplz.  
Maps: U11–U15 vs `PutGatewayRequest`.

**80. `it('pay link is checkout 5179 /c/ token')`**  
Contains `` `http://localhost:5179/c/${body.public_token}` ``.  
Maps: 015/00 §6.1.

---

## 11. Count

| Bucket | Existing `[Test]` / `it` | Strengthen | New methods listed |
|--------|--------------------------|------------|--------------------|
| Host NUnit | 58 | 9 edits (§9) | **66** host methods in §10.1–10.8 (1–66), including optional H25 and SecretBox |
| Checkout vitest | 2 | widen wallet grep to walk src | **6** `it`s (67–72) |
| Merchant vitest | 2 + 4 bearer | — | **8** `it`s (73–80) including `roles.test.ts` |

If H25 is comment-only, the host new-method count is 65 + one comment.

Do not write tests for parked files: refunds, off-session, CHIP registrar **button**, DNS fallback **implementation**, e-mandate, Hub cutover, SST math, LHDN UBL documents, Stripe Billing Portal, wallet tiles as product.

Do not write a live-network CHIP dogfood test in `task pay:test`. A99.1 human loop stays a human loop.

---

## 12. Suggested clone recipe (so implementers do not invent fixtures)

Reuse, do not rewrite:

1. `await using var factory = new PayApiFactory(); factory.One.Responder = Owner; var client = factory.CreateClient();`
2. `RailTests.Put` JSON per rail (copy the existing PUT bodies).
3. `RailTests.SeedCheckout` for token+id.
4. CHIP: `RSA.Create(2048)`, `ExportSubjectPublicKeyInfoPem()`, `SignData` SHA256 PKCS1, header `X-Signature` base64. Checkout amount 10 ↔ `total: 1000`.
5. Billplz: `BillplzWebhook.ParseForm` + `ComputeHmac`. Amount `paid_amount=1000`. Content-Type `application/x-www-form-urlencoded`. Path `/v1/webhooks/billplz/t1?checkout_id=`.
6. Xendit: header `x-callback-token` exact stored token. `paid_amount` is **major** units (`10` not `1000`).
7. Razorpay: HMAC-SHA256 hex of **raw JSON** with webhook secret. Entity `amount` is **cents** (`1000`). Header `X-Razorpay-Signature`.
8. Stripe: `WebhookTests.Sign` + `Stripe-Signature`. `amount_total` cents.
9. FakePsp `Responder` returns the hosted URL JSON; assert `LastUri`/`LastBody` only when the case is start HTTP.
10. For localhost Billplz: **override** `Pay:PublicBaseUrl`; do not use the factory default.

Assert always on money cases: HTTP status, `Documents` count, `RCPT-` prefix on pay, `Title == Official Receipt` on pay, checkout status, journal D=C (and `Count == 2` when tax/fee JSON is present).

---

## 13. Files this inventory opened (absolute)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/WebhookTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/RailTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/GatewayTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPayTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/CheckoutTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/CatalogTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/PayApiFactory.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/FakePspHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/FakeOneHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/CorsTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/HealthTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/OrgReadyTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/WhoamiTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-checkout/src/locks.test.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-checkout/src/App.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/locks.test.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/auth/bearerToken.test.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/015-four-adapters/checklists/c32-chip-webhook-tests.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/015-four-adapters/checklists/b28-billplz-tests.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/015-four-adapters/checklists/x23-xendit-tests.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/015-four-adapters/checklists/r25-razorpay-tests.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/015-four-adapters/checklists/h18-member-cannot-put-gateway.md` through `h25-fulfill-throw-retries.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/015-four-adapters/checklists/p23-empty-body-400-all.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/013-prods/checklists/g25-webhook-tests.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/016-adapters-check/README.md`

---

## 14. Bottom line for the implementer

Stripe Plane B is the only rail whose G25/H19/H13/replay bundle is mostly real. CHIP is the only new rail whose C32 paid+replay+preauthorized+empty+email+brand-PUT bundle is mostly real. Billplz, Xendit, and Razorpay each have **one** happy-path method that 015 ticked as a full clone of C32. They are not.

Write the methods in §10, strengthen §9, and stop treating `[x]` on C32/B28/X23/R25/H20/H25/P23/A99.2 as evidence. Do not implement those tests in this 016 evidence file.
