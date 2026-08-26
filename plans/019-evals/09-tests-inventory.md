# 09 — Tests inventory after 018

**Date:** 26 August 2026  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Branch:** `feat/018-merchant-shell`  
**HEAD:** `9f04ad58` — `fix(pay-ui): match receipts table to pay-link chrome`  
**Type:** Uncondensed inventory. **Not** an implementation. **Not** a run of live CHIP / Billplz / Xendit / Razorpay / Stripe. **Not** a flip of 015/016 checklist ticks.  
**Authority:** live files under `apps/lazuar-pay/tests/Lazuar.Pay.Tests/**/*.cs` (excluding `bin/` and `obj/`), `apps/lazuar-pay-merchant/src/locks.test.ts`, `apps/lazuar-pay-merchant/src/auth/bearerToken.test.ts`, `apps/lazuar-pay-merchant/src/lib/staffDisplay.test.ts`, `apps/lazuar-pay-checkout/src/locks.test.ts`. Checklist ticks and [016/09-tests-inventory.md](../016-adapters-check/09-tests-inventory.md) are background, not proof.

Parent index: [README.md](./README.md). 016 inventory (HEAD `c621ceba`, **58** NUnit / **8** vitest) is **background only**. This file re-counts live methods on **this SHA**.

---

## Coordinates

| Item | Value |
|------|--------|
| Host tests | `apps/lazuar-pay/tests/Lazuar.Pay.Tests/` |
| Merchant vitest | `apps/lazuar-pay-merchant/src/{locks,auth/bearerToken,lib/staffDisplay}.test.ts` |
| Checkout vitest | `apps/lazuar-pay-checkout/src/locks.test.ts` |
| Runner | `task pay:test` → `dotnet test Lazuar.Pay.slnx` from `apps/lazuar-pay` |
| DB in `PayApiFactory` | EF Core **InMemory**, unique name per factory (`pay-{guid}`) |
| Postgres in `task pay:test` | **None** via `PayApiFactory`. `CorsTests` + two `HealthTests` boot raw `WebApplicationFactory<Program>` (not Testing) — see §Lies and §InMemory |
| `[TestCase]` | **0** |
| `[Ignore]` / `Assert.Ignore` / Skip | **0** (the only `.Ignore(` is `InMemoryEventId.TransactionIgnoredWarning` in the factory) |

### Live count on this SHA

Grep `\[Test]` under `apps/lazuar-pay/tests/Lazuar.Pay.Tests/**/*.cs` excluding `bin/`/`obj/`: **123** matching attributes, each a method. No parameterized cases.

Grep `it(` in the four named vitest files: **32** cases.

| Bucket | Count |
|--------|------:|
| NUnit `[Test]` methods | **123** |
| NUnit `[TestCase]` | **0** |
| Merchant vitest `it(` | **23** (16 locks + 4 bearer + 3 staffDisplay) |
| Checkout vitest `it(` | **9** |
| Vitest total | **32** |
| **Combined** | **155** |

016 on `c621ceba`: 58 NUnit + 8 vitest = 66. Delta on this SHA: **+65 NUnit, +24 vitest**.

This file does **not** claim a live `task pay:test` exit code. Inventory is what `dotnet test` / `vitest` would enumerate from source.

---

## Files opened

### Host test project (source only)

| Path | Role |
|------|------|
| `IsolationTests.cs` | Grep bans (Hub types, csproj, Gateways dump namespace, `IEnumerable<IHostedRail>`) |
| `Catalog/CatalogTests.cs` | Product create writer gate |
| `Checkouts/CheckoutTests.cs` | Writer mint, org bind, idempotency, provider required, list |
| `Credentials/GatewayTests.cs` | PUT/GET/LIST processors, writer gate, Test refuses secrets |
| `Hosting/CorsTests.cs` | 5178/5179/4179 allow, 3003/3004 deny — **not** `PayApiFactory` |
| `Hosting/HealthTests.cs` | `/health`, `/v1/health` |
| `Identity/OneWebhookTests.cs` | One `X-Lazuar-Signature`, pause/unpause |
| `Identity/OrgReadyTests.cs` | `/v1/orgs/{id}/ready` |
| `Identity/WhoamiTests.cs` | `/v1/whoami` |
| `Infrastructure/PayApiFactory.cs` | Hermetic host factory (**not a test**) |
| `Infrastructure/FakeOneHandler.cs` | Fake One `/me` + `authz/check` (**not a test**) |
| `Infrastructure/FakePspHandler.cs` | Fake `IHttpClientFactory` for CHIP/Billplz/Xendit/Razorpay HTTP (**not a test**) |
| `Infrastructure/FulfillmentProbe.cs` | Throwing `IFulfillPaid` decorator (**not a test**) |
| `Infrastructure/PayTest.cs` | Shared Owner / Put / SeedCheckout / SeedPaymentLink / StartPay (**not a test**) |
| `Money/PaymentQueryTests.cs` | GET payments + receipts after Test start |
| `PaymentLinks/PaymentLinkTests.cs` | Capacity, occupancy, public GET |
| `PublicPay/PublicPayTests.cs` | Public GET/start, chip start-twice, email_required, paused |
| `Rails/Billplz/BillplzRailTests.cs` | Billplz start + paid + unpaid + localhost + empty |
| `Rails/Chip/ChipRailTests.cs` | CHIP start + paid + preauth + email |
| `Rails/Razorpay/RazorpayRailTests.cs` | Razorpay captured + failed + plink join |
| `Rails/Stripe/StripeRailTests.cs` | Missing `Stripe-Signature` header |
| `Rails/Test/TestRailTests.cs` | Local Test processor, no secrets |
| `Rails/Xendit/XenditRailTests.cs` | Xendit PAID + SETTLED |
| `Secrets/SecretBoxTests.cs` | Production wrap key required; Testing round-trip |
| `Webhooks/FillTests.cs` | 015-lied fills: fulfill-throw, amount/currency mismatch, empty, never-started |
| `Webhooks/WebhookTests.cs` | Stripe Plane B: paid+replay, setup, zero, cross-org, paused |
| `Lazuar.Pay.Tests.csproj` | NUnit + `Microsoft.AspNetCore.Mvc.Testing` + EF InMemory; **ProjectReference** only `src/Lazuar.Pay` |

There is **no** leftover `RailTests.cs` dump at the test project root. Rails are split by folder to match `src/Lazuar.Pay/Rails/{Chip,Billplz,...}`. IsolationTests does **not** lock that layout (see §IsolationTests cathedral and §Gaps).

### Frontend

| Path | Role |
|------|------|
| `apps/lazuar-pay-merchant/src/locks.test.ts` | Source greps: no Hub login, vault cards, Test processor, table chrome, pay-link mint |
| `apps/lazuar-pay-merchant/src/auth/bearerToken.test.ts` | JWT access vs opaque / id_token |
| `apps/lazuar-pay-merchant/src/lib/staffDisplay.test.ts` | Sidebar label prefers email, never numeric Zitadel sub |
| `apps/lazuar-pay-checkout/src/locks.test.ts` | Source greps: no OIDC, no wallets, verifying, poll, slot_key, placeholder email |

No `@testing-library/react`. Vitest `environment: 'node'`. No `App.tsx` / `GatewayPage.tsx` render tests.

---

## How the hermetic suite is built (PayApiFactory, FakeOne, FakePsp)

### `PayApiFactory`

```csharp
public sealed class PayApiFactory : WebApplicationFactory<Program>
{
    public FakeOneHandler One { get; } = new();
    public FakePspHandler Psp { get; } = new();
    readonly string _dbName = "pay-" + Guid.NewGuid().ToString("N");
    public string StripeWebhookSecret { get; init; } = "whsec_test_local";
    public string OneWebhookSecret { get; init; } = "";
    public string PublicBaseUrl { get; init; } = "https://pay.test.example";
    public FulfillmentProbe Probe { get; } = new();
    // UseEnvironment("Testing")
    // Pay:StripeWebhookSecret, Pay:OneWebhookSecret, Pay:PublicBaseUrl, Pay:CheckoutBaseUrl
    // OneClient replaced with FakeOneHandler (Timeout 2s)
    // IHttpClientFactory replaced with StaticHttpFactory(Psp)
    // PayDbContext UseInMemoryDatabase(_dbName)
    // InMemoryEventId.TransactionIgnoredWarning ignored
    // IFulfillPaid → ProbingFulfillment(Fulfillment, Probe)
}
```

Implications:

1. **Testing env** skips `Program.cs` Npgsql registration (`if (!builder.Environment.IsEnvironment("Testing"))`). Hermetic tests that use this factory do not need port 5435.
2. **`Pay:PublicBaseUrl=https://pay.test.example`** means default Billplz start **already has a public HTTPS origin**. Localhost Billplz is a **separate** factory `{ PublicBaseUrl = "http://localhost:8081" }` (`Billplz_localhost_callback_start_is_400_without_psp_http`). The **paid** method is still named `Billplz_paid_form_and_localhost_blocked` — the identifier still lies (see §Lies).
3. **Not a Stripe.net HTTP stub.** `StripeHosted` still does `new SessionService(new StripeClient(secret))`. No test calls `POST /v1/pay/{token}/start` with `provider=stripe` against a network. Do not add one that uses a real `sk_test_`.
4. **InMemory `BeginTransaction` is a no-op.** Factory XML comment: H25/G12 proof uses `FulfillmentProbe`, which throws **before** `Fulfillment.SaveChanges` so the event row is not committed. That is **not** Postgres TX rollback of a saved row.
5. **Filtered unique `(PaymentLinkId, SlotKey)` is Npgsql-only** in `PayDbContext`. InMemory occupancy races cannot use that index.
6. `EnsureCreated()` on InMemory. No migrations run in Testing.

### `FakeOneHandler`

Shared by almost every HTTP test. `PayTest.Owner` returns `/me` as t1 owner and **`{"allowed":true}` on every other path**, including authz. Writer vs member is **whoami role**, not a fake `authz/check` relation One does not have. `GatewayTests.Role("member")` still returns `allowed:true` on authz; `MemberGate.RequireWriterAsync` must still 403. That is the correct Fake One shape.

`CheckoutTests.Allow` is stricter: authz allowed only for `/tenants/{orgId}/authz/check`. That is why `Create_for_other_org_is_403` / `List_other_org_is_403` work.

### `FakePspHandler`

Default with no `Responder`: 404 `{}`. `SendCount` / `LastUri` / `LastBody` are the HTTP locks. Used by CHIP/Billplz/Xendit/Razorpay start. **Test rail never calls it** (`Psp.SendCount == 0` in `Mint_and_start_pays_without_keys`). Stripe start never calls it either (Stripe.net, untested).

No test asserts CHIP URL host `gate.chip-in.asia` on `LastUri` (start JSON `redirect_url` is asserted instead). Billplz asserts `LastUri` contains `billplz-sandbox`. Xendit/Razorpay do not assert `LastUri` path (`/v2/invoices`, `/v1/payment_links`) or Basic/Bearer headers.

### `FulfillmentProbe` / `ProbingFulfillment`

```csharp
if (probe.ThrowNext) { probe.ThrowNext = false; throw new InvalidOperationException("fulfill boom"); }
return inner.FulfillPaidAsync(...);
```

`FillTests.Fulfill_throw_returns_5xx_event_not_committed_retry_pays` is the only consumer. Throw happens at the **start** of `FulfillPaidAsync`, before `Fulfillment` adds documents or `SaveChanges`. WebhookEndpoints has already `Add`ed a `PspWebhookEventRow` on the same context, then `BeginTransaction` (no-op), then fulfill throws, then `Rollback` (no-op). A **new scope** then sees zero rows because nothing `SaveChanges`d. That is the InMemory trick, not 23505 / real rollback.

### `PayTest` helpers

| Helper | What it does |
|--------|----------------|
| `Owner` | `/me` t1 owner; else `allowed:true` |
| `Put` | PUT `/v1/orgs/t1/gateway` as Bearer tok; asserts success |
| `SeedCheckout` | POST `/v1/checkouts` `{org_id:t1, amount:10, provider}` → `(public_token, id)` |
| `SeedPaymentLink` | POST `/v1/payment-links` with `max_payers` / `unlimited` |
| `StartPay` | POST `/v1/pay/{token}/start` injecting `slot_key` into JSON |

There is **no** shared `RailHarness` and **no** `IPaymentGatewayFactory` test host. Keep copying these helpers. Do not invent a Hub factory.

### Who is **not** on `PayApiFactory`

`CorsTests` (all 5) and `HealthTests.Health_returns_ok` / `V1_health_returns_ok` use `new WebApplicationFactory<Program>()`. `Program.cs` registers Npgsql unless environment is Testing, and **migrates on Development**. Those seven methods are not the same hermetic box. `HealthTests.Health_does_not_call_one` and `CheckoutTests.Health_still_skips_one` **do** use `PayApiFactory`.

---

## Full method inventory (by folder)

Every `[Test]` and every vitest `it(` on this SHA. One line: the assertion that matters.

### `IsolationTests` — 6 methods

| Method | What it actually locks |
|--------|------------------------|
| `Host_csproj_does_not_reference_the_old_api` | `src/Lazuar.Pay/Lazuar.Pay.csproj` text does not contain `lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api` |
| `Test_csproj_does_not_reference_the_old_api` | Same tokens absent from `Lazuar.Pay.Tests.csproj` |
| `Source_does_not_use_mediatr_or_hub_modules` | Every `src/**/*.cs` lacks `BannedSrc` (Hub adapter types, Connect fee strings, LHDN tokens, `IEnumerable<IHostedRail>`, `namespace Lazuar.Pay.Gateways`, `namespace Lazuar.Pay.One;`) |
| `Source_does_not_create_org_or_user_tables` | No `ToTable("organizations"\|"users"\|"members")` in Pay `src` |
| `Vite_apps_do_not_use_hub_types` | Merchant + checkout `package.json` do not contain `@repo/api-types-ts` (does **not** walk `src/`) |
| `No_csproj_references_apps_lazuar_api` | Every `apps/lazuar-pay/**/*.csproj` lacks `apps/lazuar-api`, `apps\lazuar-api`, `Razorpay.Api` |

Does **not** walk `tests/`. Does **not** assert `Rails/Chip/` folders exist. Does **not** fail a recreated `tests/RailTests.cs` dump. Cathedral strings vs 017 folders: see §What 018 added and §Lies.

### `Catalog/CatalogTests` — 2 methods

| Method | What it actually locks |
|--------|------------------------|
| `Create_product_as_owner` | Owner POST `/v1/orgs/t1/products` `{name, amount:10}` → **201** |
| `Member_cannot_create_product` | whoami role `member` (authz still `allowed:true`) POST product → **403** |

No GET product, no member GET, no other-org, no amount 0.

### `Checkouts/CheckoutTests` — 16 methods

| Method | What it actually locks |
|--------|------------------------|
| `Create_without_bearer_is_401` | POST `/v1/checkouts` no Authorization → **401**; `One.SendCount == 0` |
| `Create_and_get_open_session` | POST amount 12.50 MYR stripe + success/cancel URLs → **201**, `status=open`, `provider=stripe`; GET `/v1/checkouts/{id}` **200** same id |
| `Get_unknown_is_404` | GET `/v1/checkouts/missing` with Bearer → **404**; One not called |
| `Create_for_other_org_is_403` | Allow(t1) POST `org_id:t2` → **403** |
| `Get_other_org_session_is_403` | Mint t1, switch Allow(t2), GET that id → **403** |
| `Create_idempotent_on_key` | Two POSTs `Idempotency-Key: k1` → same checkout `id` |
| `Create_defaults_currency_to_myr` | POST without currency → `currency=MYR` |
| `Create_without_provider_is_400` | POST `{org_id, amount}` no provider → **400** `unknown provider` (bind-at-mint; no org default rail) |
| `Create_unknown_provider_is_400` | `provider:paypal` → **400** `unknown provider` |
| `Create_unconfigured_rail_is_400` | Stripe keys on file, mint `provider:chip` → **400** `rail not configured` |
| `Create_test_without_vault_is_201` | `provider:test`, no gateway PUT → **201**, `provider=test` |
| `Create_rejects_non_positive_amount` | `amount:0` → **400** |
| `Member_cannot_create_checkout` | role member, authz allowed → **403** |
| `List_returns_org_checkouts_newest_first` | Two Test seeds; GET `/v1/orgs/t1/checkouts` length 2, `provider=test`, `status=open`, `public_token` non-empty (does **not** compare created_at order beyond array length + first row) |
| `List_other_org_is_403` | Allow(t1) GET `/v1/orgs/t2/checkouts` → **403** |
| `Health_still_skips_one` | `One.ThrowOnSend`; GET `/health` success; `One.SendCount == 0` (duplicate of `HealthTests.Health_does_not_call_one`, One only, not Psp) |

No admin-as-writer. No paused-org mint. No Test-in-Production 400 on checkout create.

### `Credentials/GatewayTests` — 11 methods

| Method | What it actually locks |
|--------|------------------------|
| `Member_cannot_put_gateway` | role member PUT stripe keys → **403** |
| `Put_requires_webhook_secret` | Owner PUT stripe `{provider, secret}` no `webhook_secret` → **400** |
| `Put_and_get_does_not_echo_secret` | PUT stripe sk+whsec; PUT/GET JSON lack `sk_test` / `whsec_abc`; GET `configured`, `provider=stripe`, `capability=hosted_link`, `webhook_configured`; audit `gateway.credentials.upsert` org t1; **`OrgSettings.ActiveProvider` is Null** (vault does not pick a default rail) |
| `Chip_put_requires_brand_id` | PUT chip PEM **without** `public_merchant_id` → **400** |
| `Put_unknown_provider_is_400` | PUT `paypal` → **400** |
| `Member_can_get_gateway_metadata` | Owner PUT then member GET `?provider=stripe` → **200**, no sk/whsec, `capability=hosted_link` |
| `List_returns_all_five_and_put_does_not_default_pay_links` | PUT stripe + chip; GET `/v1/orgs/t1/gateways` **`processors` length 6** (five hosted + Test); stripe/chip configured, xendit not, **test configured=true** without PUT; bare GET `/gateway` also length 6; `ActiveProvider` still Null; credential **row count 2** |
| `Put_test_processor_is_400` | PUT `provider:test` with secrets → **400** `does not take secrets` |
| `Get_unknown_provider_query_is_400` | GET `?provider=paypal` → **400** |
| `Billplz_put_requires_collection_id` | PUT billplz secret+whsec+env **without** `public_merchant_id` → **400** |
| `Razorpay_put_requires_key_id_colon_secret` | PUT razorpay `secret:nocolon` → **400** |

No admin PUT. No GET as member on `/gateways` list. No PEM echo test on chip GET. No live-vs-test Billplz host on PUT (environment is stored; start-time host is `BillplzRailTests`). Method name `List_returns_all_five_…` asserts **six** processors — see §Lies.

### `Hosting/CorsTests` — 5 methods

All use raw `WebApplicationFactory<Program>` (not `PayApiFactory`). Origin header on **GET `/health` only**.

| Method | What it actually locks |
|--------|------------------------|
| `Health_allows_merchant_origin` | Origin `http://localhost:5178` → ACAO contains that origin |
| `Health_allows_checkout_origin` | Origin `http://localhost:5179` → ACAO contains that origin |
| `Health_allows_preview_checkout_origin` | Origin `http://localhost:4179` → ACAO contains that origin |
| `Health_does_not_allow_ops_origin` | Origin `http://localhost:3003` → **no** ACAO header |
| `Health_does_not_allow_portal_origin` | Origin `http://localhost:3004` → **no** ACAO header |

Program also allows `127.0.0.1:5178/5179/4178/4179` and **`localhost:4178`** (merchant preview). **4178 is untested.** No CORS test on `/v1/pay` or `/v1/whoami`.

### `Hosting/HealthTests` — 3 methods

| Method | What it actually locks |
|--------|------------------------|
| `Health_returns_ok` | Raw factory GET `/health` success, body contains `ok` |
| `V1_health_returns_ok` | Raw factory GET `/v1/health` success, body contains `ok` |
| `Health_does_not_call_one` | `PayApiFactory`, `One.ThrowOnSend`; both health paths success; `One.SendCount == 0` |

### `Identity/WhoamiTests` — 6 methods

| Method | What it actually locks |
|--------|------------------------|
| `Whoami_maps_org_id_from_one_me` | Bearer tok forwarded as `Bearer tok` to One GET `/me`; Pay JSON `user_id`, `email`, `name`, **`active_org_id=t1`**, tenants[0].id t1; One SendCount 1 |
| `Whoami_allows_empty_tenants` | One me with `tenants:[]` → 200, tenants length 0 |
| `Whoami_without_authorization_is_401_and_skips_one` | GET `/v1/whoami` no header → 401; One SendCount 0 |
| `Whoami_maps_one_401` | One 401 → Pay 401 |
| `Whoami_maps_one_timeout_to_503` | `One.Delay = 5s` vs client timeout 2s → **503** |
| `Whoami_maps_one_500_to_503` | One 500 → Pay 503 |

Does **not** assert tenant `role` / `slug`. `WhoamiResponse` has no `active_role` field; the One fixture’s `active_role` is dropped (not a failing assert).

### `Identity/OrgReadyTests` — 6 methods

| Method | What it actually locks |
|--------|------------------------|
| `Ready_when_one_allows_member` | POST One `/tenants/t1/authz/check`; body has `relation:member`, `type:tenant`, `id:t1`, **no `user_id`**; Pay 200 `{org_id:t1, ready:true}` |
| `Ready_forbidden_when_allowed_false` | One `{allowed:false}` → **403** |
| `Ready_forbidden_when_one_403` | One HTTP 403 → Pay 403 |
| `Ready_503_when_one_500` | One 500 → Pay 503 |
| `Ready_401_without_bearer_skips_one` | No Authorization → 401; One SendCount 0 |
| `Ready_checks_path_org_not_header` | Path `path-org`, header `X-Lazuar-Tenant-Id: header-org` → One path contains `/tenants/path-org/authz/check`; Pay `org_id=path-org` |

### `Identity/OneWebhookTests` — 8 methods

Factory `OneWebhookSecret = "one_whsec_test"` except missing-secret. Header `X-Lazuar-Signature` = `t={unix},v1={hex HMAC of "{unix}.{body}"}`.

| Method | What it actually locks |
|--------|------------------------|
| `Valid_tenant_suspended_sets_charges_paused` | Body `{type:tenant.suspended, org_id:t1}` signed → 200; `OrgSettings.t1.ChargesPaused == true` |
| `Valid_tenant_id_field_sets_charges_paused` | Same with **`tenant_id`** instead of `org_id` → paused true |
| `Body_only_uppercase_hex_is_401` | Header is raw uppercase HMAC of **body only** (old dialect, no `t=`/`v1=`) → **401** |
| `Missing_signature_is_401` | No header → 401 |
| `Stale_timestamp_is_401` | `t` = now − 1000s (> 300s skew) → 401 |
| `Missing_secret_is_503` | Factory `OneWebhookSecret=""` → **503** |
| `Replay_delivery_is_duplicate` | Same signed body twice → second 200 body contains `duplicate` |
| `Tenant_reactivated_clears_pause` | suspend then `{type:tenant.reactivated}` → `ChargesPaused == false` |

No isolated HMAC **vector** test of `OneWebhookSignature.TryVerify` (known t/v1 bytes). No `tenant.deleted`. No unknown type ignored. No unsigned pause.

### `Money/PaymentQueryTests` — 2 methods

Both: Test checkout + start `{name:Ada}` (pays immediately), then list.

| Method | What it actually locks |
|--------|------------------------|
| `List_payments_includes_provider_and_label` | GET `/v1/orgs/t1/payments` length 1; `provider=test`, `status=paid`, `payer_name=Ada`, `amount=10` (does **not** assert `label`; method name overclaims) |
| `List_receipts_includes_number_amount_and_payer` | GET `/v1/orgs/t1/receipts` length 1; `number` starts `RCPT-`, `title=Official Receipt`, `status=issued`, `payer_name=Ada`, `amount=10` |

No other-org 403. No member vs writer. No GET `/v1/orgs/{org}/receipts/{id}`.

### `PaymentLinks/PaymentLinkTests` — 12 methods (018)

| Method | What it actually locks |
|--------|------------------------|
| `Create_defaults_to_one_payer` | POST `/v1/payment-links` test, no max → 201 `max_payers=1`, `unlimited=false`, `status=open`, `remaining=1`, `public_token` set |
| `Create_unlimited_has_null_max` | Seed unlimited; public GET `max_payers` and `remaining` JSON **null**, `status=open` |
| `Create_max_zero_is_400` | `max_payers:0` → 400 body contains `max_payers` |
| `Create_without_bearer_is_401` | No Authorization → 401; One SendCount 0 |
| `List_returns_newest_first_with_capacity` | Seed max 1 then max 3; GET `/v1/orgs/t1/payment-links` length 2; **[0] max=3 remaining=3**, [1] max=1 (newest-first via created_at of sequential seeds) |
| `List_other_org_is_403` | Allow-t1-only responder; GET `/v1/orgs/t2/payment-links` → 403 |
| `Two_people_can_pay_a_link_of_two` | max=2 Test starts with distinct slots; third slot **409** body `full`; GET `?slot_key=` of payer A `status=paid`; GET other slot `status=full`, `remaining=0` |
| `Same_slot_start_twice_does_not_take_two_seats` | CHIP link max=2; same `slot_key` twice → 200 both, **`Psp.SendCount == 1`**; list `taken_count=1`, `remaining=1` |
| `Unlimited_accepts_three_payers` | Three distinct Test starts 200; public GET still `open`, `paid_count=3`, `remaining` null |
| `One_person_link_shows_paid_without_slot_after_pay` | max=1 Test start; GET **without** slot_key `status=paid` |
| `Start_link_without_slot_key_is_400` | Start JSON `{name:Ada}` no slot → 400 body `slot_key` |
| `Public_get_does_not_need_bearer` | GET `/v1/pay/{token}` 200; second GET does not increment One (One was used on mint only) |

**Sequential only.** No `Task.WhenAll` race. Test rail **pays on start**, so `Two_people_can_pay_a_link_of_two` never holds an `open` child against capacity. Writer 403 on create is **missing**. Slot length 7 is **missing** (`NormalizeSlotKey` rejects `< 8`).

### `PublicPay/PublicPayTests` — 9 methods

| Method | What it actually locks |
|--------|------------------------|
| `Public_get_does_not_need_bearer` | Stripe checkout public GET 200; second GET One SendCount unchanged |
| `Public_missing_is_404` | GET `/v1/pay/missing` 404; One SendCount 0 |
| `Start_twice_returns_same_url_without_second_psp_http` | CHIP start twice; `redirect_url` same; **`Psp.SendCount == 1`**; `ProviderSessionId=purch_1`, `Provider=chip` |
| `Public_get_exposes_started_and_redirect_after_start` | Before start: `started=false`, no redirect; after CHIP start: `started=true`, `redirect_url` stub |
| `Start_paid_is_409` | DB-mutate checkout `status=paid`; start → **409** |
| `Start_paused_is_403_even_with_stored_url` | Stored `PspRedirectUrl` + `ChargesPaused`; start → **403**; Psp SendCount 0 |
| `Email_required_true_when_active_chip` | Public GET chip checkout `email_required` true |
| `Email_required_false_when_active_stripe` | Public GET stripe checkout `email_required` false |
| `Start_without_rail_is_503` | Manual open checkout **no** `Provider` and no keys; start → **503** `rail not configured` |

CHIP start-twice is public **checkout** idempotency, not payment-link occupancy. Stripe **start** (Stripe.net) is not exercised.

### `Rails/Stripe/StripeRailTests` — 1 method

| Method | What it actually locks |
|--------|------------------------|
| `Missing_stripe_signature_header_is_400` | PUT stripe; POST `/v1/webhooks/stripe/t1` completed-session JSON **with no** `Stripe-Signature` → **400** |

Does **not** seed a checkout, does **not** assert `Documents.Count == 0`. Weaker than the 016 proposed method of the same name.

### `Rails/Chip/ChipRailTests` — 5 methods

| Method | What it actually locks |
|--------|------------------------|
| `Chip_start_and_paid_webhook` | RSA PEM PUT + FakePsp `{id:purch_1, checkout_url}`; start name+email → `redirect_url` stub; `LastBody` has **no** `force_recurring`, has `checkout_id` and `org_id`; signed `purchase.paid` total 1000 MYR → 200; checkout `Provider=chip`, `ProviderSessionId=purch_1`; one `RCPT-`; debit sum = credit sum; replay body `duplicate`; still one document |
| `Chip_preauthorized_is_ignored` | Signed `purchase.preauthorized` + `recurring_token` → 200 body `preauthorized`; **zero documents**. Does **not** assert checkout still `open` |
| `Chip_start_without_email_is_400` | Start `{name:Ada}` only → 400 (does not assert Psp unused) |
| `Chip_empty_body_400` | POST webhook content `"  "` → 400 |
| `Chip_placeholder_email_is_400` | Start email `customer@example.com` → 400; **`Psp.LastUri` is Null** |

No bad/missing RSA, no `purchase.payment_failure`, no cross-org, no amount mismatch, no missing currency, no start after Brand ID cleared.

### `Rails/Billplz/BillplzRailTests` — 5 methods

| Method | What it actually locks |
|--------|------------------------|
| `Billplz_paid_form_and_localhost_blocked` | Default factory (HTTPS public base); start **succeeds**; `LastUri` contains `billplz-sandbox`; form `paid=true` HMAC `excludeExtra:false` including `checkout_id`; POST webhook query checkout_id → 200; one `RCPT-`; replay body `duplicate`; still one doc. **Does not POST localhost. Name lies.** |
| `Billplz_placeholder_email_is_400` | Start `customer@example.com` → 400; `Psp.SendCount == 0` |
| `Billplz_localhost_callback_start_is_400_without_psp_http` | Factory `PublicBaseUrl=http://localhost:8081`; start with real email → **400** `callback base not public`; `Psp.SendCount == 0` |
| `Billplz_unpaid_is_ignored` | Valid HMAC `paid=false` `state=due` → 200 body `unpaid`; zero documents (no checkout-open assert) |
| `Billplz_empty_body_400` | POST `"  "` form-urlencoded → 400 |

No bad HMAC, no extra-fields pair (`paid_at` included vs excluded), no missing email (only placeholder), no cross-org, no amount mismatch, no `reference_1` join, no start without collection, no 127.0.0.1 / `lazuar-local-dev.com` variants in the localhost method.

### `Rails/Xendit/XenditRailTests` — 3 methods

| Method | What it actually locks |
|--------|------------------------|
| `Xendit_paid_and_settled` | Start with email; PAID `paid_amount:10` (major) + `x-callback-token: tok_1`; then SETTLED same invoice id → 200; body contains `settled` **or** `ignored`; one `RCPT-`; checkout `paid`. **Not** a replay of the same PAID event id (`paid:{id}` vs `settled:{id}`). No journal assert |
| `Xendit_placeholder_email_is_400` | Start placeholder → 400 (no Psp SendCount assert) |
| `Xendit_empty_body_400` | POST `"  "` → 400 |

No bad/missing callback token, no EXPIRED/PENDING, no **PAID replay**, no missing email, no cross-org, no amount mismatch, no missing currency.

### `Rails/Razorpay/RazorpayRailTests` — 5 methods

| Method | What it actually locks |
|--------|------------------------|
| `Razorpay_captured` | Start + `payment.captured` amount 1000 with `"tax":12,"fee":30` HMAC → 200; one `RCPT-`; **`JournalLines.Count == 2`**; debit = credit. Tax/fee **not** extra lines (R21). **No replay** |
| `Razorpay_placeholder_email_is_400` | Placeholder → 400 |
| `Razorpay_empty_body_400` | `"  "` → 400 |
| `Razorpay_payment_failed_is_ignored` | `payment.failed` valid HMAC → 200 body `payment_failed`; zero documents (no checkout-open assert) |
| `Razorpay_captured_without_notes_joins_plink` | Captured **without** notes checkout_id; payload `payment_link.entity.id=plink_1` matches start session → 200; **Documents.Count == 1** (no `RCPT-` prefix assert) |

No bad/missing signature, no captured **replay**, no `X-Razorpay-Event-Id` grain, no missing email, no missing currency, no cross-org, no amount mismatch, no `payment_link.paid` ignore as its own method (parser ignores non-captured; untested).

### `Rails/Test/TestRailTests` — 2 methods (018)

| Method | What it actually locks |
|--------|------------------------|
| `Mint_and_start_pays_without_keys` | Seed `provider=test` **no** gateway PUT; start `{name:Ada}` → 200; `redirect_url` contains `status=verifying`; public GET `status=paid`, `provider=test`; DB checkout paid; one Official Receipt; **`Psp.SendCount == 0`** |
| `Webhook_pays_open_test_checkout` | POST `/v1/webhooks/test/t1` unsigned JSON `{id, checkout_id, amount_total:1000, currency:myr}` → 200; one document |

Test webhook **has no HMAC** (`TestWebhook.Parse` JSON only). That is locked by omission: the method would fail if a signature were required. No Production `AllowsTest` 400. No second start 409. No replay. No empty body.

### `Secrets/SecretBoxTests` — 2 methods

| Method | What it actually locks |
|--------|------------------------|
| `Production_missing_wrap_key_throws` | `SecretBox` + empty config + env Production; `Protect("x")` throws `InvalidOperationException` containing `Pay:WrapKey` |
| `Testing_allows_dev_wrap_key` | Env Testing, empty config; `Unprotect(Protect("x")) == "x"` |

No Production **with** WrapKey round-trip. No “git wrap key string rejected in Production” beyond missing-key throw.

### `Webhooks/WebhookTests` — 8 methods (Stripe Plane B)

| Method | What it actually locks |
|--------|------------------------|
| `Missing_webhook_secret_is_503_when_rail_configured` | Factory `StripeWebhookSecret=""`; PUT then **null** `WebhookCiphertext`; POST body no signature → **503**. Testing-only process-env fallback is what makes empty row 503 here (`ResolveSecret` returns process env only in Testing; it is set to `""`) |
| `Invalid_signature_is_400` | Header `t=1,v1=deadbeef` → 400 (no documents assert) |
| `Completed_session_writes_receipt_and_replay_is_noop` | Signed `checkout.session.completed` mode payment `amount_total:1000`; first 200; one `RCPT-`; **Title Official Receipt**; checkout **paid**; `SstRegistered` Null; body does not contain `SST registration unknown`; debit=credit; replay 200 `duplicate`; still one document |
| `Setup_mode_is_ignored` | mode **setup** amount 0 on **open** amount-10 checkout; 200 body contains `ignored` **and** `setup`; zero docs; checkout still `open` |
| `Zero_amount_session_is_ignored` | mode payment `amount_total:0` `payment_status:paid`; 200 `ignored`; zero docs; checkout still `open` |
| `Cross_org_checkout_is_400` | t1 checkout posted to `/v1/webhooks/stripe/t2` with t2 keys; 400; zero documents |
| `Unknown_provider_is_400` | POST `/v1/webhooks/paypal/t1` → 400 |
| `Paused_org_does_not_mint_receipt` | `ChargesPaused=true`; signed paid → **409**; zero docs; checkout open; **event id not stored**; unpause; retry same payload 200; one document |

No unknown Stripe event type (`charge.refunded`). No missing currency. No Production-must-not-use-process-env (H11 Production). Stripe **start** blocked.

### `Webhooks/FillTests` — 6 methods (015-lied fills)

016 named these as ticked-without-methods. Live class on this SHA:

| Method | What it actually locks | vs 016 proposal |
|--------|------------------------|-----------------|
| `Fulfill_throw_returns_5xx_event_not_committed_retry_pays` | `Probe.ThrowNext`; signed paid → status ≥500; 0 documents; **0 PspWebhookEvents for that eventId**; retry 200; one document | Exists. InMemory TX is still a no-op; proof is “throw before SaveChanges” |
| `Amount_mismatch_does_not_mint_receipt` | Signed `amount_total:999` vs checkout 10.00 → **400**; 0 docs; **0 events**; checkout **open** | Exists at full strength |
| `Currency_mismatch_does_not_mint_receipt` | Signed `currency:usd` → **400**; 0 documents | **Weaker**: no checkout-open, no event-count |
| `Rail_not_configured_is_400_when_body_present` | **No** PUT; POST `{"id":"evt_x"}` → 400 body `rail not configured` | Exists (P24; non-empty so empty-body does not win) |
| `Never_started_checkout_webhook_is_400` | After seed, DB `Provider=null`; signed paid → 400 `provider mismatch` | Extra vs 016 list (y11). Stripe only |
| `Empty_webhook_is_400` | POST `""` stripe, no rail → 400 | Moved from 016 `PublicPayTests.Empty_webhook_is_400`. Stripe empty string, not whitespace |

### Merchant vitest — `locks.test.ts` — 16 `it(`

Source greps. No render.

| `it(` | What it actually locks |
|-------|------------------------|
| `has no password form or Hub login` | Walk `src/**/*.{ts,tsx,css}` excluding `*.test.*`: no `type="password"`, no `/one/auth/login`, no `lazuar_auth` |
| `package.json does not depend on Hub types` | No `@repo/api-types-ts`, `@repo/aura-ui`, `lazuar-ops` |
| `CHIP PEM uses a textarea` | `GatewayPage.tsx` contains `Textarea` and `PEM from CHIP dashboard` |
| `hydrates environment from GET` | `setEnvironment(row.environment)` and `/gateways` |
| `processor vault is cards not an org default rail` | `CardTitle`; **not** `aspect-square`; **not** `One active rail`; copy `does not pick the rail for pay links` |
| `processor secrets open from Edit into a dialog` | `Edit`, `DialogContent`, `openEdit` |
| `test processor has no secret editor` | `r === 'test'`, `No keys. Use this on Pay links.`; `processors.ts` contains `'test'` |
| `receipts table uses the same chrome as pay links` | `ReceiptsPage.tsx`: `rounded-xl border border-slate-200`, `uppercase tracking-wider`, `No receipts yet`, `Official Receipt`; **not** `CardContent` |
| `payments table uses the same chrome as pay links` | Same chrome strings on `PaymentsPage.tsx` + `formatMoney` + `No payments yet`; not `CardContent` |
| `pay links send a chosen provider` | `CheckoutsPage.tsx` contains `provider`, `/gateways`, `/payment-links`, `'test'`, `Create pay link`, `DialogContent`, `Table`, `unlimited`, `max_payers`, `1 person only` |
| `overview lists processors not a single active rail` | `OverviewPage.tsx` `/gateways`, not `Active rail`, contains `On file` |
| `org shell uses copied AppSidebar not Aura ops nav` | `DashboardChrome` `AppSidebar` + `WorkspaceSwitcher`; `nav.ts` `Processor`; not `Appointments` |
| `sidebar header can switch or create workspace` | `WorkspaceSwitcher` `Create workspace`, `Switch workspace`, `/new` |
| `home redirects into last org dashboard` | `homePath.ts` `/overview` and `/workspaces/new` |
| `slug pattern escapes hyphen for unicode-sets HTML pattern` | `pattern="[a-z0-9\\\\-]{1,64}"` |
| `create workspace form uses card chrome` | `CreateWorkspaceForm` contains `Card` and `workspace_name` |

Does **not** lock `canWriteMoney`, hide-paste for members, PUT JSON field names at runtime, `VITE_*` secrets, wrap copy “do not auto-debit”.

### Merchant vitest — `bearerToken.test.ts` — 4 `it(`

| `it(` | What it actually locks |
|-------|------------------------|
| `accepts compact JWS and rejects opaque / JWE / empty` | `isJwtLike(JWT)` true; opaque, 5-part JWE, `''` false |
| `returns undefined when signed out` | `pickApiBearerToken(null\|undefined)` undefined |
| `sends JWT access_token and never the companion id_token` | access JWT returned; not equal to id_token JWT |
| `does not fall back to JWT id_token when access is opaque or empty` | opaque/empty/JWE access + JWT id → undefined |

### Merchant vitest — `staffDisplay.test.ts` — 3 `it(`

| `it(` | What it actually locks |
|-------|------------------------|
| `prefers whoami email over numeric user_id` | whoami email+name used |
| `uses OIDC profile email when whoami email is missing` | OIDC profile email/name; name is not the numeric sub |
| `does not show a numeric Zitadel sub as the label` | no email → name `'Signed in'`, email null |

### Checkout vitest — `locks.test.ts` — 9 `it(`

Reads **`src/App.tsx` only** (except package.json on the first). Not a walk of `src/`.

| `it(` | What it actually locks |
|-------|------------------------|
| `has no OIDC dependency` | package.json lacks `oidc-client-ts`, `react-oidc-context`, `@repo/api-types-ts` |
| `does not render wallet tiles or card PAN` | App.tsx lowercase lacks grabpay/tng/touchngo/boost/duitnow/fpx/shopee; no `autocomplete="cc-number"` |
| `verifying query is not paid` | contains `=== 'verifying'` and `pay.status === 'paid'` |
| `polls public GET while verifying` | contains `/v1/pay/` and `setInterval` |
| `does not treat customer@example.com as satisfying email_required` | contains `customer@example.com` and `usableEmail` |
| `test processor copy is not a wallet tile` | `pay.provider === 'test'` and `No card, no secret` |
| `uses copied aura-ui card chrome not a Hub portal` | `Card`, `Payment received`, `Link expired`, `Link is full`; not `lazuar-portal` |
| `sends a local slot_key so one browser is one payer on a shared link` | `lazuar-pay-slot:`, `localStorage`, `slot_key`, `pay.status === 'full'` |
| `maps start 400 without calling it paid` | `response.status === 400`; **not** `status: 'paid'`; **not** the conflated string `callback base not public or email required` |

`App.tsx` **does** map 503 → `rail not configured`. **No** `it(` locks that. No RTL of occupancy UI.

---

## What 018 added (PaymentLinkTests, TestRailTests, occupancy, UI locks) — re-verified

016 inventory (background): **58** NUnit in a **dump** (`WebhookTests`, `RailTests`, `GatewayTests`, `PublicPayTests`, … at test root). Vitest: 2 checkout + 2 merchant locks + 4 bearer.

On this SHA, live additions / moves:

### Host layout (017 folders, tests followed)

Source is `Credentials/`, `Rails/{Stripe,Chip,Billplz,Xendit,Razorpay,Test}/`, `Webhooks/`, `PublicPay/`, `Identity/`, `PaymentLinks/`, `Money/`, **not** `namespace Lazuar.Pay.Gateways`. Tests mirror that. IsolationTests bans the old dump **namespace** and `IEnumerable<IHostedRail>` (a factory-of-rails cathedral). `Program.cs` registers **named concretes** and `PublicPayEndpoints` switches on provider. That is the intended shape. IsolationTests does **not** assert the folder tree.

### New NUnit classes vs 016

| Class | Methods | 018-relevant lock |
|-------|--------:|-------------------|
| `PaymentLinkTests` | 12 | Capacity mint, sequential occupancy, slot_key required, public GET |
| `TestRailTests` | 2 | Test processor pays without vault; unsigned test webhook pays |
| `PaymentQueryTests` | 2 | Payments/receipts list after Test start |
| `OneWebhookTests` | 8 | One pause/unpause HMAC |
| `SecretBoxTests` | 2 | Production wrap key |
| `FillTests` | 6 | 015-lied money fills |
| `StripeRailTests` | 1 | Missing Stripe-Signature (split out of dump) |
| Split `*RailTests` | 5+5+3+5 | Was 7 methods in one `RailTests.cs` |

### Occupancy — what is actually proven

Live code: `MintOrResume` check-then-insert; `PaymentLinkOccupancy.CountsTowardCapacity` = status `open` **or** `paid`; filtered unique `(PaymentLinkId, SlotKey)` **only if Npgsql**.

Proven **sequentially** on InMemory + Test rail (pays on start):

- default max 1
- max 0 rejected
- unlimited null remaining
- two of two + third 409 `full`
- same CHIP slot does not take two seats / second PSP HTTP
- max 1 GET without slot shows `paid` after pay
- start without slot_key 400

**Not** proven:

- two **parallel** different slots on max=1
- an **open unpaid** CHIP child filling a 1-seat link (Test rail never stays open)
- slot_key length 7 / 129
- duplicate slot unique index (Npgsql-only)
- member cannot mint a pay link
- paused org cannot mint a pay link
- Production Test processor mint 400

### UI locks 018 re-verified

Merchant locks grew from 2 to **16**. They are greps of current chrome: vault **cards** not one active rail, Test has no secret editor, receipts/payments tables share pay-link CSS classes, mint dialog sends `provider` + `max_payers` / `unlimited`, sidebar switcher, hyphen-escaped slug pattern. HEAD commit `match receipts table to pay-link chrome` is what `receipts table uses the same chrome as pay links` greps.

Checkout locks grew from 2 to **9**: verifying ≠ paid, poll, placeholder email, Test copy, aura Card, **slot_key in localStorage**, start 400 not paid.

`canWriteMoney` is used in `OrgLayout` / `GatewayPage` (`write`). **No unit test.**

### Writer gates on this SHA

| Door | Member 403 | Owner 200 | Admin as writer | Member GET |
|------|------------|-----------|-----------------|------------|
| PUT gateway | **exists** | bundled in PUT/GET | **missing** | GET metadata **exists** |
| POST checkout | **exists** | create+get | **missing** | list is member; **no** member-list test |
| POST product | **exists** | create | **missing** | **missing** |
| POST payment-link | **missing** | create defaults | **missing** | list other-org 403 exists; **no** member-list test |
| GET payments/receipts | n/a (RequireMember) | list exists | n/a | **missing** as an explicit member 200 |

---

## Lies: comments/checklists/READMEs that overclaim tests

A lie here = assert weaker than the method name / checklist / comment, or a paper that treats a tick as a method.

### Method names that overclaim their asserts

| Live method | Why it lies |
|-------------|-------------|
| `BillplzRailTests.Billplz_paid_form_and_localhost_blocked` | Localhost is a **different** method. This one uses default `https://pay.test.example`, start **succeeds**, asserts sandbox host + paid + `RCPT-` + replay. Identifier still says blocked. 016 already called this out; **018 did not rename**. |
| `GatewayTests.List_returns_all_five_and_put_does_not_default_pay_links` | Asserts `processors.GetArrayLength() == 6` (five + Test). “all_five” is stale vs Test processor. The ActiveProvider-null half is true. |
| `PaymentQueryTests.List_payments_includes_provider_and_label` | Asserts provider, status, payer_name, amount. **Never reads `label`.** |
| `ChipRailTests.Chip_preauthorized_is_ignored` | Zero documents + body `preauthorized`. Does not lock checkout still `open` (setup/zero Stripe tests do). |
| `XenditRailTests.Xendit_paid_and_settled` | SETTLED is a **different event id** (`settled:inv_1`). Body `settled` **or** `ignored`. Not PAID replay. No journal. |
| `StripeRailTests.Missing_stripe_signature_header_is_400` | 400 only. 016 asked Documents.Count == 0 on a seeded checkout. |
| `FillTests.Currency_mismatch_does_not_mint_receipt` | Sibling amount-mismatch asserts open checkout + zero events. This one only 400 + zero documents. |
| `FillTests.Fulfill_throw_returns_5xx_event_not_committed_retry_pays` | Name + factory comment say TX/event not committed. InMemory rollback is a no-op. Probe throws **before** any `SaveChanges`. Honest as “no SaveChanges”, dishonest as “transaction rolled back”. |
| `CheckoutTests.Health_still_skips_one` / `List_returns_org_checkouts_newest_first` | Health is a duplicate of `HealthTests`. List does not assert ordering by `created_at` beyond “second seed is [0]” implicitly via newest-first + two rows — it never compares timestamps. |
| Merchant `it('receipts table uses the same chrome as pay links')` | CSS class string equality, not a screenshot or shared component import. A divergent table that copies the same classes still passes. |
| Checkout `it('polls public GET while verifying')` | Presence of `/v1/pay/` and `setInterval` anywhere in `App.tsx`, not “inside the verifying effect, 2s, cap 15”. |

### Checklists / papers that still overclaim (do not flip ticks in this file)

| Claim | Live |
|-------|------|
| 015 `a99-four-adapters-done.md` A99.2 `[x] Hermetic tests exist for stripe, chip, billplz, xendit, razorpay: paid + replay + not-paid` | Stripe: paid+replay+setup+zero. CHIP: paid+replay+preauth. Billplz: paid+replay+unpaid. Xendit: paid+**SETTLED** (not PAID replay), no EXPIRED. Razorpay: paid, **no replay**, failed ignored. **A99.2 is still false for Xendit PAID replay and Razorpay replay.** |
| 016 inventory “58 methods”, “Billplz/Xendit/Razorpay happy path only”, “no SecretBoxTests”, “empty webhook lives in PublicPayTests”, “H14 missing”, “H25 missing”, “H18 member GET missing” | Stale vs this SHA. FillTests/SecretBox/member GET/H14 amount/H25 probe **exist**. Do not copy 016 counts. Do not treat 016 §10 as an unstarted list — **re-diff** (this file). |
| 013 papers “31 tests”, Taskfile desc “health + isolation” | Taskfile on this SHA: `Test the focused Pay host (hermetic whoami, checkouts, rails, webhooks)` — still omits payment-links, occupancy, One webhooks, secrets. 013 counts are historical. |
| IsolationTests “source is not a Gateways dump” | Bans **`namespace Lazuar.Pay.Gateways`**. Live namespaces are `Lazuar.Pay.Rails.*`. A dump **folder** named `Gateways/` with a different namespace would not fail. Test files could be dumped again as `RailTests.cs` without failing IsolationTests. |
| `task pay:test` is fully hermetic | True for `PayApiFactory` tests. **False** as a blanket for `CorsTests` + `Health_returns_ok` / `V1_health_returns_ok` (raw factory, not Testing). |
| Frontend locks “honesty” as UX proof | Greps. `canWriteMoney` hide-paste is live in `GatewayPage` (`write`) and **untested**. |

### 015-lied cases that FillTests **did** close (so 016 §6/§8 is not the remaining list)

Closed on this SHA (Stripe only unless noted): H14 amount, H14 currency (weaker), P24 body-present no rail, H25 fulfill-throw (InMemory caveat), P23 stripe empty (`""`), y11 never-started, H10 missing Stripe-Signature (weak), H16 SecretBox Production, H18 member GET, P22 PUT unknown, P23 chip/billplz/xendit/razorpay empty, P20 placeholder on four rails, B15 localhost (separate method), B21 unpaid, R18 failed, B27 PUT collection, R12 PUT colon, P19 email_required GET chip/stripe.

Still ticked in 015/016 without a method: see §Gaps.

---

## Gaps: one proposed method per hole (name, arrange/act/assert in prose)

Do **not** write these here. One method per remaining hole. Strengthen-in-place is named first when a live method is merely weak.

### Strengthen existing methods (not new)

1. **`ChipRailTests.Chip_preauthorized_is_ignored`** — after zero documents, assert checkout `Status == open`.
2. **`FillTests.Currency_mismatch_does_not_mint_receipt`** — assert checkout still `open` and `PspWebhookEvents.Count == 0` (match amount-mismatch).
3. **`BillplzRailTests.Billplz_unpaid_is_ignored`** / **`RazorpayRailTests.Razorpay_payment_failed_is_ignored`** — assert checkout still `open`.
4. **`XenditRailTests.Xendit_paid_and_settled`** — after PAID, assert journal debit=credit; after SETTLED require `settled` (drop `.Or.Contain("ignored")` if live reason is `settled`).
5. **`StripeRailTests.Missing_stripe_signature_header_is_400`** — seed a checkout; assert zero documents.
6. **`PaymentQueryTests.List_payments_includes_provider_and_label`** — either assert `label` (null today without product) or rename. Prefer assert `label` is JSON null without a product, then a second method for product label.
7. **`BillplzRailTests.Billplz_paid_form_and_localhost_blocked`** — rename to `Billplz_paid_form_sandbox_start` when someone already touches the file. Not a new case.

### Stripe / Fill / Webhook remainder

8. **`WebhookTests.Unknown_event_type_is_ignored`** — Arrange: `PayApiFactory`, Owner, seed stripe checkout. Act: signed Stripe event `type=charge.refunded` (parseable). Assert: 200, body `ignored`, zero documents, checkout `open`.
9. **`WebhookTests.Stripe_missing_currency_does_not_pay`** — Signed completed session with no / empty `currency`, `amount_total:1000`. Assert: 400 `missing currency`, zero documents, checkout open.
10. **`FillTests.Stripe_whitespace_webhook_is_400`** — POST `" \n"` to `/v1/webhooks/stripe/t1`. Assert 400. (Empty `""` already exists; chip uses whitespace.)
11. **`WebhookTests.Production_empty_row_whsec_is_503_even_if_process_env_set`** — Factory Production + WrapKey set + `Pay:StripeWebhookSecret=whsec_process`; PUT then null ciphertext; sign with process env. Assert **503**, not 200. Maps H11. Skip if Production boot needs more than WrapKey — then comment, do not fake.
12. **`PublicPayTests.Stripe_start_is_not_called_in_pay_test`** — **do not write a live Stripe.net start.** Optional: a comment on `StripeRailTests` that start is blocked until a Stripe client seam exists. Not a green test against `sk_test_`.

### CHIP remainder

13. **`ChipRailTests.Chip_bad_signature_is_400`** — PUT real PEM. POST valid `purchase.paid` JSON, `X-Signature: aGVsbG8=`. Assert 400, zero documents.
14. **`ChipRailTests.Chip_missing_signature_header_is_400`** — Same JSON, no header. 400, zero documents.
15. **`ChipRailTests.Chip_missing_currency_does_not_pay`** — Signed paid `total:1000` without currency. 400, zero documents, checkout open.
16. **`ChipRailTests.Chip_payment_failure_is_ignored`** — Signed `purchase.payment_failure`. 200, body `payment_failure`, zero documents, checkout open.
17. **`ChipRailTests.Chip_failure_then_paid_still_mints_one_receipt`** — Same purchase id: failure then paid. One `RCPT-`.
18. **`ChipRailTests.Chip_cross_org_checkout_is_400`** — t1 checkout metadata on `/v1/webhooks/chip/t2` with t2 PEM. 400, zero documents.
19. **`ChipRailTests.Chip_amount_mismatch_does_not_pay`** — total 999 vs checkout 10.00. 400, zero documents, checkout open.
20. **`ChipRailTests.Chip_start_without_brand_id_is_503`** — PUT with brand, DB-clear `PublicMerchantId`, start with email. 503, Psp not called.

### Billplz remainder

21. **`BillplzRailTests.Billplz_bad_hmac_is_400`** — `x_signature=deadbeef` on paid form. 400, zero documents.
22. **`BillplzRailTests.Billplz_hmac_with_extra_fields_paid`** — form includes `paid_at`, `transaction_id`, `transaction_status`; HMAC `excludeExtra:false`. 200, one `RCPT-`.
23. **`BillplzRailTests.Billplz_hmac_without_extra_fields_paid`** — extra fields present, HMAC `excludeExtra:true` (Hub with-extra fails, without-extra passes). Fresh checkout. 200, one `RCPT-`.
24. **`BillplzRailTests.Billplz_start_without_email_is_400`** — `{name:Ada}` only. 400, Psp SendCount 0.
25. **`BillplzRailTests.Billplz_start_without_collection_is_503`** — PUT valid, clear `PublicMerchantId`, start with email. 503, no PSP HTTP.
26. **`BillplzRailTests.Billplz_cross_org_is_400`** — t1 checkout_id query on `/v1/webhooks/billplz/t2`. Valid HMAC. 400, zero documents.
27. **`BillplzRailTests.Billplz_amount_mismatch_does_not_pay`** — `paid_amount=999` sen vs 10.00. Valid HMAC. 400, zero documents.
28. **`BillplzRailTests.Billplz_join_via_reference_1_when_query_missing`** — no query `checkout_id`; form `reference_1={id}`. 200, one `RCPT-`.
29. **`BillplzRailTests.Billplz_localhost_variants_are_400`** — same method as existing localhost **or** extra POSTs in it: `https://127.0.0.1/` and `https://foo.lazuar-local-dev.com`. 400, Psp unused. Do not add `lazuar-local-dev.com` to IsolationTests (false positive in `BillplzHosted.TryPublicBase`).

Live PUT **defaults** `environment` to `test` when omitted. Do **not** write `Billplz_put_requires_environment` — 016 proposed it against older code; live `GatewayEndpoints` would 200.

### Xendit remainder

30. **`XenditRailTests.Xendit_bad_callback_token_is_400`** — PAID JSON, `x-callback-token: wrong`. 400, zero documents.
31. **`XenditRailTests.Xendit_missing_callback_token_is_400`** — no header. 400.
32. **`XenditRailTests.Xendit_expired_is_ignored`** — `status:EXPIRED`. 200 ignored, zero documents, checkout open.
33. **`XenditRailTests.Xendit_pending_is_ignored`** — `status:PENDING` (optional second POST FAILED in the same method). Zero documents.
34. **`XenditRailTests.Xendit_paid_replay_is_duplicate`** — same PAID body twice. Second `duplicate`, one `RCPT-`. Distinct from SETTLED.
35. **`XenditRailTests.Xendit_start_without_email_is_400`** — 400, Psp unused.
36. **`XenditRailTests.Xendit_missing_currency_does_not_pay`** — PAID without currency. 400, zero documents.
37. **`XenditRailTests.Xendit_cross_org_is_400`** — t1 metadata on t2 webhook. 400, zero documents.
38. **`XenditRailTests.Xendit_amount_mismatch_does_not_pay`** — `paid_amount: 9.99` major vs checkout 10. **Do not** send 1000 thinking cents. 400, zero documents.

### Razorpay remainder

39. **`RazorpayRailTests.Razorpay_bad_signature_is_400`** — captured JSON, `X-Razorpay-Signature: deadbeef`. 400, zero documents.
40. **`RazorpayRailTests.Razorpay_missing_signature_is_400`** — no header. 400.
41. **`RazorpayRailTests.Razorpay_captured_replay_is_duplicate`** — two identical captured POSTs. Second `duplicate`, one document.
42. **`RazorpayRailTests.Razorpay_failed_then_captured_still_pays`** — same `pay_1`: failed then captured without Event-Id header so grains `failed:pay_1` / `captured:pay_1`. One `RCPT-`.
43. **`RazorpayRailTests.Razorpay_event_id_prefers_header`** — captured pay_1, header `X-Razorpay-Event-Id: evt_header_1`. After 200, `PspWebhookEvents.EventId == evt_header_1`.
44. **`RazorpayRailTests.Razorpay_start_without_email_is_400`** — 400.
45. **`RazorpayRailTests.Razorpay_missing_currency_does_not_pay`** — captured entity without currency. 400.
46. **`RazorpayRailTests.Razorpay_cross_org_is_400`** — 400, zero documents.
47. **`RazorpayRailTests.Razorpay_amount_mismatch_does_not_pay`** — entity `amount:999` already minor vs checkout 10. 400.
48. **`RazorpayRailTests.Razorpay_payment_link_paid_is_ignored`** — `event:payment_link.paid` valid HMAC. 200 ignored, zero documents (parser already ignores non-captured).

### Test rail remainder

49. **`TestRailTests.Second_start_on_paid_test_checkout_is_409`** — first start pays; second start same token → 409; still one document.
50. **`TestRailTests.Webhook_replay_is_duplicate`** — same `{id:evt_test_1,...}` twice. Second `duplicate`, one document.
51. **`TestRailTests.Production_test_processor_is_400`** — factory `UseEnvironment("Production")` + WrapKey; POST checkout or payment-link `provider:test`. Assert 400 `test processor is not enabled` / `rail not configured`. Same factory: POST `/v1/webhooks/test/t1` 400. If Production boot is heavy, one method, two acts.
52. **`TestRailTests.Empty_webhook_is_400`** — POST `"  "` `/v1/webhooks/test/t1`. 400.
53. **`TestRailTests.Amount_mismatch_does_not_pay`** — webhook `amount_total:999`. 400, zero documents, checkout open.

Test webhook **unsigned by design** in Testing. Do not add a fake HMAC requirement. Production disable is the lock.

### Occupancy / payment links

54. **`PaymentLinkTests.Member_cannot_create_payment_link`** — role member, authz allowed, POST `/v1/payment-links`. 403. (Writer gate hole.)
55. **`PaymentLinkTests.Admin_can_create_payment_link`** — whoami role `admin`. 201. Proves writer is owner **or** admin, not owner-only.
56. **`PaymentLinkTests.Create_for_other_org_is_403`** — Allow t1, POST `org_id:t2`. 403.
57. **`PaymentLinkTests.Create_unknown_provider_is_400`** — `provider:paypal`. 400 `unknown provider`.
58. **`PaymentLinkTests.Create_unconfigured_rail_is_400`** — `provider:chip` with no vault row. 400 `rail not configured`.
59. **`PaymentLinkTests.Create_paused_org_is_403`** — ChargesPaused, POST link. 403.
60. **`PaymentLinkTests.Create_non_positive_amount_is_400`** — amount 0. 400.
61. **`PaymentLinkTests.Slot_key_too_short_is_400`** — start `slot_key` length 7. 400 `slot_key`.
62. **`PaymentLinkTests.Open_chip_child_fills_a_one_seat_link`** — CHIP max=1; first slot start 200 (open, not paid); second slot start 409 `full`; public GET other slot `full`. This is the occupancy path Test rail hides by paying on start.
63. **`PaymentLinkTests.Concurrent_two_slots_on_max_one_only_one_succeeds`** — `Task.WhenAll` two different slots, max=1, Test or CHIP. Assert **exactly one** 200 and one 409, `taken_count==1`. On InMemory this may flake both-200 — that is the **bug**, not a skip. Prefer a **Postgres collection** (see §InMemory). Do not mark green if both succeed.
64. **`PaymentLinkTests.Expired_child_does_not_count_toward_capacity`** — max=1; insert child `status=expired`; other slot start 200.

### Public start / paused links

65. **`PublicPayTests.Start_paused_payment_link_is_403`** — seed link, pause org, StartPay with slot. 403, no child row (or no extra child).
66. **`PublicPayTests.Chip_start_twice_does_not_overwrite_session_id`** — already implied by `Start_twice_…` + `ProviderSessionId=purch_1`. Optional assert session id unchanged if FakePsp would return `purch_2` on a second call — force Responder to increment; SendCount stays 1 so second body is unused. Strengthen `Start_twice_returns_same_url_without_second_psp_http` rather than a new method if touching it.

### IsolationTests cathedral / folders

67. **`IsolationTests.Source_namespaces_are_not_a_gateways_dump`** — already the `namespace Lazuar.Pay.Gateways` token. Prefer **folder** lock: every `src/Lazuar.Pay/**/*.cs` except obj/bin lives under job folders (`Rails/`, `Credentials/`, `Webhooks/`, `PublicPay/`, `Identity/`, `PaymentLinks/`, `Money/`, `Catalog/`, `Checkouts/`, `Hosting/`, `Secrets/`, `Data/`) **or** `Program.cs` / `Properties`. Fail a file in `src/Lazuar.Pay/Gateways/`.
68. **`IsolationTests.Test_files_mirror_job_folders`** — every `*Tests.cs` except `IsolationTests.cs` lives under `tests/Lazuar.Pay.Tests/{Catalog,Checkouts,Credentials,Hosting,Identity,Money,PaymentLinks,PublicPay,Rails,Secrets,Webhooks}/`. Fail a recreated `RailTests.cs` dump at the test root.
69. **`IsolationTests.No_ienumerable_hosted_rail_factory`** — already `IEnumerable<IHostedRail>` in `BannedSrc`. Keep. Do **not** ban `IHostedRail` (live switch uses it).

### CORS / health hermetic

70. **`CorsTests.Health_allows_preview_merchant_origin`** — Origin `http://localhost:4178` (Program already allows). ACAO contains 4178.
71. **`CorsTests.Uses_pay_api_factory_testing_env`** — not a product case; **change the existing five** to `PayApiFactory` (or `WebApplicationFactory` + `UseEnvironment("Testing")`) so `task pay:test` does not migrate Npgsql. Same for `Health_returns_ok` / `V1_health_returns_ok`.
72. **`HealthTests.Health_still_200_if_psp_handler_throws`** — `Psp.Responder` throws; GET `/health` 200. Today only One is thrown.

### SecretBox / One HMAC vector

73. **`SecretBoxTests.Production_with_wrap_key_round_trips`** — Production + 32-byte WrapKey config; Protect/Unprotect `"x"`.
74. **`OneWebhookTests.Known_vector_verifies`** — call `OneWebhookSignature.TryVerify` with fixed unix, body, secret, expected v1 hex (or POST that vector). Lock lowercase hex + `{unix}.{body}`. Maps 016 w23.

### Money queries / catalog leftovers

75. **`PaymentQueryTests.List_other_org_is_403`** — Allow t1, GET `/v1/orgs/t2/payments` and `/receipts`. 403.
76. **`PaymentQueryTests.Member_can_list_payments`** — role member GET payments **200** (RequireMember, not writer).
77. **`PaymentQueryTests.Get_receipt_by_id`** — after Test pay, GET `/v1/orgs/t1/receipts/{id}` 200 number `RCPT-`; unknown id 404; other org id 404.
78. **`CatalogTests.Member_can_not_only_fail_create`** — optional GET list if the door exists; **skip if no GET products door**.

### Writer admin (one method covers the role table)

79. **`GatewayTests.Admin_can_put_gateway`** — whoami role `admin` PUT stripe. 200. Pairs with payment-link admin create.

Do not add parked: refunds, off-session, CHIP registrar **button**, DNS fallback **implementation**, e-mandate, Hub cutover, SST math, LHDN UBL, Stripe Billing Portal, wallet tiles as product, `IPaymentGatewayFactory` tests.

---

## InMemory limitations (TX, unique indexes)

Live `PayDbContext`:

- `BeginTransaction` on InMemory is a **no-op**. Factory **ignores** `TransactionIgnoredWarning`.
- `PspWebhookEvents` PK `(OrgId, Provider, EventId)` — serial `FindAsync` duplicate path is what replay tests use. Concurrent two-POSTs expecting `DbUpdateException` → `{duplicate:true}` is **unproven** on InMemory (H24.3).
- `Checkouts` unique `PublicToken` — InMemory may or may not enforce; no test collides tokens.
- **Filtered unique `(PaymentLinkId, SlotKey)` is compiled only when `ProviderName` contains `Npgsql`.** InMemory occupancy cannot use it. Two parallel inserts of the same slot can both succeed in Testing.
- There is **no** unique constraint that `taken < max_payers`. Capacity is check-then-insert in `MintOrResume`. Even Postgres can over-admit without `SERIALIZABLE`, an advisory lock, or a constraint trigger. Concurrent max=1 is a **product hole**, not only a test hole.
- `Fulfill_throw_…` proves “Probe threw before `Fulfillment.SaveChanges`, new scope sees nothing”. It does **not** prove webhook `Add(event) + fulfill + Commit` rolls back a saved event on 5435.
- `DocumentSequences` PK `(OrgId, Series, YearMyt)` receipt numbering race: untested.
- `OneWebhookEvents` unique `DeliveryId`: serial replay exists; concurrent untested.

**Postgres test collection** (Testcontainers Pay DB, not Hub `lazuar_mvp`) is the honest home for: concurrent occupancy, concurrent webhook 23505, fulfill-throw **after** event insert, slot unique index. That collection must be **opt-in**, not `task pay:test` default, and must still use FakeOne/FakePsp.

Do not call InMemory unique indexes “Postgres unique indexes”.

---

## Frontend test gaps

Vitest is **node greps + two pure functions** (`pickApiBearerToken`, `staffDisplay`). `environment: 'node'`. No jsdom render.

### Merchant

80. **`it('canWriteMoney is owner or admin only')`** — new `src/lib/roles.test.ts`. `canWriteMoney('owner'|'admin')` true; `member` / `viewer` / null / `''` false. Do not invent a One role `viewer` on the host; the unit test may still say viewer is not a writer.
81. **`it('hides processor Edit unless write')`** — grep `GatewayPage.tsx` uses `write` from outlet and a member copy (live: `{!write ? (`). Maps U16. Grep, not RTL, unless the implementer adds testing-library.
82. **`it('PUT body uses host field names')`** — `GatewayPage` paste JSON keys `provider`, `secret` / razorpay `keyId:keySecret`, `webhook_secret`, `public_merchant_id`, `environment`.
83. **`it('no VITE secrets or sk_live defaults')`** — walk merchant `src` + `.env*` if present: forbid `VITE_STRIPE_SECRET`, `sk_live_`, committed `BEGIN PUBLIC KEY`, `VITE_CHIP`. Allow `VITE_PAY_API_URL`.
84. **`it('wrap copy says we do not auto-debit for billplz xendit razorpay')`** — `processors.ts` / copy record.
85. **`it('staff rails list includes test and not fiuu')`** — `processors.ts` `rails` equals host `PayProviders.Listed` in Testing (stripe chip billplz xendit razorpay **test**).

No RTL of `AppSidebar`, workspace switcher, or tables. Locks will not catch a runtime `write={true}` bug.

### Checkout

86. **`it('maps start 503 to rail not configured')`** — `App.tsx` already contains `response.status === 503` and `rail not configured`. Lock it. 016 K16 still open as a **test**.
87. **Widen wallet/PAN grep to walk `src/`** — today only `App.tsx`. Cheap.
88. **No RTL** that slot_key is sent on start POST, that `status=full` renders “Link is full”, that verifying poll stops at 15. Greps exist; behaviour does not.

### Shared frontend holes

- No Playwright. A99.1 human loop stays a human loop.
- IsolationTests Vite lock is package.json only; merchant locks duplicate Hub-types; checkout locks duplicate OIDC-dep. Keep all three; they fail at different layers.

---

## Ranked: write these tests first (after money fixes, not instead of them)

Money bugs (over-capacity, double receipt, amount mismatch on a rail) get **code fixes first**. Tests below are the proof those fixes hold. Do not write a green InMemory test that documents a leak.

1. **`PaymentLinkTests.Open_chip_child_fills_a_one_seat_link`** then **`Concurrent_two_slots_on_max_one_only_one_succeeds`** (Postgres if InMemory both-200). Occupancy is the 018 money surface.
2. **`XenditRailTests.Xendit_paid_replay_is_duplicate`** and **`RazorpayRailTests.Razorpay_captured_replay_is_duplicate`**. A99.2 is still false without them. Clone Stripe/CHIP replay.
3. **Amount mismatch clones** on chip, billplz, xendit, razorpay, test webhook. Stripe FillTests already exist. Wrong units are how you mint the wrong `RCPT-`.
4. **Cross-org clones** on chip, billplz, xendit, razorpay. Stripe exists. Tenant mix-up is money.
5. **Bad/missing signatures** on chip, billplz, xendit, razorpay. Empty body exists; verify does not.
6. **`PaymentLinkTests.Member_cannot_create_payment_link`** + **`Admin_can_create_payment_link`**. Writer gate on the 018 mint door.
7. **`TestRailTests.Second_start_on_paid_test_checkout_is_409`** + **`Production_test_processor_is_400`**. Test rail must not be a second Stripe in Production and must not double-fulfill.
8. **Strengthen** CHIP preauth checkout open; Xendit journal; currency-mismatch events.
9. **`ChipRailTests.Chip_payment_failure_is_ignored`** + failure-then-paid. Analogous to Razorpay failed (which exists).
10. **`WebhookTests.Unknown_event_type_is_ignored`** + Stripe missing currency.
11. **`roles.test.ts` `canWriteMoney`** + checkout **`maps start 503`**. Cheap, closes 016 frontend holes without RTL.
12. **`CorsTests` 4178** + move CORS/health onto Testing factory so `pay:test` is actually hermetic.
13. **IsolationTests folder locks** (Gateways dump vs `Rails/`). Hygiene after money.
14. **Postgres collection** last: TX fulfill-throw, 23505 duplicate, slot unique. Not inside default `task pay:test`.

Billplz extra-HMAC pair, reference_1 join, Razorpay Event-Id header, One HMAC vector, SecretBox Production round-trip: after the money list.

---

## Refuse (do not call live PSP from task pay:test; do not factory tests)

- **Do not** call live Stripe / CHIP / Billplz / Xendit / Razorpay from `task pay:test`. FakePsp + signed fixtures only. A99.1 human loop stays human.
- **Do not** add `IPaymentGatewayFactory` / `PaymentGatewayFactory` / `IEnumerable<IHostedRail>` tests or production types. IsolationTests must keep failing those strings. Rails stay named concretes + a switch.
- **Do not** `ProjectReference` `apps/lazuar-api`. Do not add `Razorpay.Api`. Do not add MediatR.
- **Do not** write `Stripe_start_returns_redirect_url` against `new StripeClient(sk_test_…)`.
- **Do not** SaveChanges-then-throw inside production `Fulfillment` to fake H25.
- **Do not** ban `lazuar-local-dev.com` in IsolationTests (`BillplzHosted` block list contains it).
- **Do not** flip 015/016 checklist ticks from this paper.
- **Do not** add Hub module tests, Playwright “pay on CHIP hosted page”, refunds, LHDN, SST math, off-session, CHIP registrar button, DNS fallback implementation.
- **Do not** put Testcontainers Postgres in the default hermetic job until it is a **named** collection.

---

## Appendix: quoted evidence

### Count

```
rg -n '\[Test' apps/lazuar-pay/tests/Lazuar.Pay.Tests --glob '*.cs'
# 123 matching lines (this SHA)

rg -n '\bit\(' apps/lazuar-pay-merchant/src/{locks.test.ts,auth/bearerToken.test.ts,lib/staffDisplay.test.ts} apps/lazuar-pay-checkout/src/locks.test.ts
# 32
```

### Factory InMemory TX comment

```28:31:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs
    /// InMemory BeginTransaction is a no-op. H25/G12 proof uses FulfillmentProbe,
    /// which throws before Fulfillment.SaveChanges so the event row is not committed.
    /// </summary>
    public FulfillmentProbe Probe { get; } = new();
```

### Npgsql-only occupancy unique index

```43:48:apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs
            if (Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
            {
                e.HasIndex(x => new { x.PaymentLinkId, x.SlotKey })
                    .IsUnique()
                    .HasFilter("\"SlotKey\" IS NOT NULL");
            }
```

### IsolationTests cathedral strings (Gateways dump vs factory-of-rails)

```5:16:apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
    static readonly string[] Banned = ["lazuar-api", "Modules.", "BuildingBlocks", "MediatR", "Lazuar.Api"];
    static readonly string[] BannedSrc =
    [
        "MediatR", "Modules.One", "BuildingBlocks", "IPaymentGatewayAdapter", "PaymentGatewayFactory",
        "IPaymentGatewayFactory", "AddPaymentsModule", "GatewayPaymentCompletedIntegrationEvent", "Modules.Payments",
        "ApplicationFeeAmount", "Razorpay.Api",
        "application_fee", "TransferData", "transfer_data",
        "ChipWebhookRegistrar", "PublicDnsFallback",
        "Lhdn", "MyInvois", "UBL", "XAdES", "Irbm",
        "IEnumerable<IHostedRail>",
        "namespace Lazuar.Pay.Gateways",
        "namespace Lazuar.Pay.One;"
    ];
```

Live host uses `namespace Lazuar.Pay.Rails.Chip` (etc.), `namespace Lazuar.Pay.Identity.OneWebhooks`, and a **switch** on `IHostedRail` concretes in `PublicPayEndpoints` — not `IEnumerable<IHostedRail>`, not `namespace Lazuar.Pay.Gateways`.

### Occupancy check-then-insert (no capacity unique)

```236:264:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        var taken = await db.Checkouts.CountAsync(
            x => x.PaymentLinkId == link.Id && (x.Status == "open" || x.Status == "paid"),
            ct);
        if (PaymentLinkOccupancy.IsFull(link.MaxPayers, taken))
        {
            return (null, PayErrors.Status(409, "Conflict", "This pay link is full"));
        }
        // ... Checkouts.Add(row); SaveChanges
```

### Test webhook has no signature

```9:17:apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestWebhook.cs
    public static PspParseResult Parse(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw new PspVerifyException("invalid event");
```

### A99.2 tick that still overclaims replay

```24:24:plans/015-four-adapters/checklists/a99-four-adapters-done.md
- [x] Hermetic tests exist for `stripe`, `chip`, `billplz`, `xendit`, `razorpay`: paid + replay + not-paid
```

### Billplz paid method name (still)

```12:12:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Billplz/BillplzRailTests.cs
    public async Task Billplz_paid_form_and_localhost_blocked()
```

versus the actual localhost method:

```73:87:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Billplz/BillplzRailTests.cs
    public async Task Billplz_localhost_callback_start_is_400_without_psp_http()
    {
        await using var factory = new PayApiFactory { PublicBaseUrl = "http://localhost:8081" };
        // ...
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("callback base not public"));
        Assert.That(factory.Psp.SendCount, Is.EqualTo(0));
```

### Gateway list asserts six, method says five

```153:154:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs
        var processors = doc.RootElement.GetProperty("processors");
        Assert.That(processors.GetArrayLength(), Is.EqualTo(6));
```

### Stripe process-env fallback is Testing-only

```85:90:apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeWebhook.cs
        if (env.IsEnvironment("Testing"))
        {
            return config["Pay:StripeWebhookSecret"];
        }

        return null;
```

### `task pay:test` stays hermetic (Taskfile)

```103:107:Taskfile.yml
  pay:test:
    desc: Test the focused Pay host (hermetic whoami, checkouts, rails, webhooks)
    dir: apps/lazuar-pay
    cmds:
      - dotnet test Lazuar.Pay.slnx --nologo --verbosity minimal
```

csproj ProjectReference: only `..\..\src\Lazuar.Pay\Lazuar.Pay.csproj`.

### 016 background counts (do not reuse)

[016/09-tests-inventory.md](../016-adapters-check/09-tests-inventory.md) counted **58** `[Test]` and **8** vitest `it(` on `c621ceba`. This SHA: **123** + **32**.
