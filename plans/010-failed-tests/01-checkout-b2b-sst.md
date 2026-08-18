# 01 — Checkout B2B identity tests fail after issue 167 SST fail-closed

**Date:** 18 August 2026  
**Suite:** `Lazuar.ModuleTests` (`apps/lazuar-api/tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj`)  
**Fixtures:** `Lazuar.ModuleTests.Commerce.CheckoutB2bIdentityTests` + related B2B initiate `Lazuar.ModuleTests.Commerce.CreateCustomCheckoutAndInitiateSessionTests.InitiateCheckout_SessionId_StampsB2bMetadataAndRequiresTin`  
**Handler:** `apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs`  
**SST helper:** `apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs` → `MerchantHasSstAsync`  
**Issue:** 167 / B10-X11 (`issues/167-p1-b10-x11-getservice-sst-fail-open-undercharge.md`) — fail-closed when billing is not composed  
**Wave / branch:** Wave 161–180, `fix/180-unify-outbox-inbox`  
**HEAD:** `4531f210f61b3d58d0332f1728b6a7889a1d2cad` (`4531f210 fix(api): register every module outbox and inbox through one helper`)  
**167 commit (the throw):** `49606466c25fa3181d3b7528cfa94fbf5fcd3426` (`fix(commerce): refuse to bill SST when billing is not composed`)  
**This document:** analysis only. Do not implement the product fix here. Do not revert 167.

---

## 0. Verdict in one paragraph

These six tests are not proving a B2B identity regression. They are constructing `InitiateCheckoutCommandHandler` with the five-argument constructor and therefore leaving the optional sixth parameter `IBillingQueryService? billingQueryService = null`. After 167, `SubscriptionBillingAmount.MerchantHasSstAsync` throws `InvalidOperationException("IBillingQueryService is required to decide SST; refusing to undercharge.")` when `billing` is null. Production `AddAllModules` still registers `IBillingQueryService` via `AddBillingModule`, so live hop-1 is fine. The tests never reach the B2B / TIN / ID-pair / metadata assertions they were written for. Two of the six wrap the exception in FluentAssertions `WithMessage` and therefore surface as a *message mismatch* (`*ID type*` or `*tax ID*` vs the SST string). The other four surface as an uncaught SST throw. The fix is to pass a stub `IBillingQueryService` that returns no SST registration number, matching `SubscriptionBillingAmountTests.Gross_NoSst_Is100` after 167.

Re-run of the assigned filter on this HEAD (18 August 2026):

```
dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj \
  --filter "FullyQualifiedName~CheckoutB2bIdentityTests|FullyQualifiedName~InitiateCheckout_SessionId_StampsB2bMetadataAndRequiresTin"

Total tests: 8
     Passed: 2
     Failed: 6
```

The two that still pass in that filter are `InitiateCheckout_RequiresTaxId_MissingTin_ThrowsExistingMessage` and `MergeClientIntoGateway_StampsB2bWhenRequested`. They never reach `MerchantHasSstAsync`.

---

## 1. Title, assigned tests, suite, HEAD

### 1.1 Title

Checkout B2B identity ModuleTests fail because issue 167 made `MerchantHasSstAsync` refuse a null `IBillingQueryService`, and the B2B initiate fixtures still construct `InitiateCheckoutCommandHandler` without billing.

### 1.2 Assigned failed tests (6)

| # | Test | Fixture | File |
|---|------|---------|------|
| 1 | `InitiateCheckout_CustomSession_CopiesIsB2bRequiredIntoMetadata` | `CheckoutB2bIdentityTests` | `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CheckoutB2bIdentityTests.cs` lines 83–130 |
| 2 | `InitiateCheckout_CustomSession_MissingIdPair_Throws` | `CheckoutB2bIdentityTests` | same file, lines 132–153 |
| 3 | `InitiateCheckout_CustomSession_PassesIdPairNamed_NotCompanyNameAsIdValue` | `CheckoutB2bIdentityTests` | same file, lines 155–188 |
| 4 | `InitiateCheckout_ProductFlagOff_DoesNotStampB2b` | `CheckoutB2bIdentityTests` | same file, lines 65–81 |
| 5 | `InitiateCheckout_RequiresTaxId_WithTinAndCompany_ResolvesCrmWithoutIdValue_AndStampsB2b` | `CheckoutB2bIdentityTests` | same file, lines 36–63 |
| 6 | `InitiateCheckout_SessionId_StampsB2bMetadataAndRequiresTin` | `CreateCustomCheckoutAndInitiateSessionTests` | `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CreateCustomCheckoutAndInitiateSessionTests.cs` lines 95–144 |

### 1.3 Suite

- Project: `apps/lazuar-api/tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj`
- Framework: NUnit 3 via VSTest / NUnit Adapter 5.0.0.0
- Assertions: FluentAssertions
- Isolation: NSubstitute stubs; no DI container; `new InitiateCheckoutCommandHandler(...)` in helpers
- Namespace: `Lazuar.ModuleTests.Commerce`

### 1.4 HEAD and relevant history

```
4531f210 fix(api): register every module outbox and inbox through one helper   ← current HEAD (fix/180-unify-outbox-inbox)
...
49606466 fix(commerce): refuse to bill SST when billing is not composed       ← issue 167
f1f7ba03 fix(commerce): apply exclusive SST on quotes and mark-paid           ← custom hop-2 started calling MerchantHasSstAsync
eba07414 fix(commerce): charge SST on renewals and dunning                    ← product hop-1 started calling MerchantHasSstAsync
fca58f70 feat: LP-022/122 company TIN checkout and merchant legal profile     ← IBillingQueryService? added to handler ctor (optional, default null)
```

Issue 167's file still quotes the *pre-fix* body (`return false` when billing is null). The live code on this HEAD is the throw. Status in that issue file is `resolved` on `fix/167-sst-fail-closed`. The B2B fixtures were never updated when 167 landed.

---

## 2. Exact current test construction (how `IBillingQueryService` is / is not passed)

### 2.1 Handler constructor arity on this HEAD

```31:45:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
    public InitiateCheckoutCommandHandler(
        IOneQueryService oneQueryService,
        ICommerceRepository repository,
        IMediator mediator,
        IConfiguration configuration,
        ICommunicationsQueryService communicationsQueryService,
        IBillingQueryService? billingQueryService = null)
    {
        _oneQueryService = oneQueryService;
        _repository = repository;
        _mediator = mediator;
        _configuration = configuration;
        _communicationsQueryService = communicationsQueryService;
        _billingQueryService = billingQueryService;
    }
```

Six parameters. The last is **optional and defaults to null**. That default is why the tests compile and why they fail only at runtime inside `Handle`. Field:

```29:29:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
    private readonly IBillingQueryService? _billingQueryService;
```

### 2.2 `CheckoutB2bIdentityTests` — two helpers, both five-arg `new`

Every handler in this fixture is created through `CreateHandler`. There is no `IBillingQueryService` anywhere in the file. The file does not even `using Modules.Billing.Contracts`.

**Overload A** — product-catalog tests (`requiresTaxId` true/false). Builds a `Product` with `CheckoutConfiguration(false, requiresTaxId, false)`, stubs `GetProductBySlugAsync("pro-plan")`, then delegates to overload B:

```222:245:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CheckoutB2bIdentityTests.cs
    private static InitiateCheckoutCommandHandler CreateHandler(
        out IMediator mediator,
        out ICommerceRepository repository,
        bool requiresTaxId)
    {
        var orgId = Guid.CreateVersion7();
        var product = new Product(
            orgId,
            "Pro Plan",
            "pro-plan",
            100m,
            "FIXED",
            0m,
            "MYR",
            "one_time",
            "STRIPE",
            new CheckoutConfiguration(false, requiresTaxId, false),
            new[] { "telegram" });

        repository = Substitute.For<ICommerceRepository>();
        repository.GetProductBySlugAsync(orgId, "pro-plan", Arg.Any<CancellationToken>()).Returns(product);
        mediator = Substitute.For<IMediator>();
        return CreateHandler(orgId, repository, mediator);
    }
```

Product SST defaults (from `Product` ctor) are `SstTaxType = "06"` and `SstRatePercent = 0m`. These tests never call `product.SetSst`. Even *after* a no-SST billing stub is wired, hop-2 amount stays 100 net. That is what the B2B assertions want.

**Overload B** — the actual `new`. Used by both product tests (via A) and the three custom-session tests (directly):

```247:266:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CheckoutB2bIdentityTests.cs
    private static InitiateCheckoutCommandHandler CreateHandler(
        Guid orgId,
        ICommerceRepository repository,
        IMediator mediator)
    {
        var one = Substitute.For<IOneQueryService>();
        one.GetTenantIdBySlugAsync("acme").Returns(orgId);

        var comms = Substitute.For<ICommunicationsQueryService>();
        comms.HasValidEmailConfigAsync(orgId).Returns(true);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:ClientUrl"] = "https://portal.test"
            })
            .Build();

        return new InitiateCheckoutCommandHandler(one, repository, mediator, config, comms);
    }
```

That `new` is **five arguments**. C# binds `billingQueryService` to `null`. There is no sixth argument, no named `billingQueryService:`, no local stub.

Call sites of overload B inside this fixture:

- Line 101 — `InitiateCheckout_CustomSession_CopiesIsB2bRequiredIntoMetadata` (`CreateHandler(orgId, repository, mediator)`)
- Line 146 — `InitiateCheckout_CustomSession_MissingIdPair_Throws` (`CreateHandler(orgId, repository, Substitute.For<IMediator>())`)
- Line 175 — `InitiateCheckout_CustomSession_PassesIdPairNamed_NotCompanyNameAsIdValue` (`CreateHandler(orgId, repository, mediator)`)
- Line 244 — overload A returns `CreateHandler(orgId, repository, mediator)` for:
  - Line 28 — `InitiateCheckout_RequiresTaxId_MissingTin_ThrowsExistingMessage` (still passes; see §4 / §7)
  - Line 39 — `InitiateCheckout_RequiresTaxId_WithTinAndCompany_ResolvesCrmWithoutIdValue_AndStampsB2b` (fails)
  - Line 68 — `InitiateCheckout_ProductFlagOff_DoesNotStampB2b` (fails)

### 2.3 `GuestCommand` used by every product and custom-session test in this file

```198:220:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CheckoutB2bIdentityTests.cs
    private static InitiateCheckoutCommand GuestCommand(
        string? taxId,
        string? companyName,
        string? idType = null,
        string? idValue = null) =>
        new(
            "acme",
            "pro-plan",
            "Ada",
            "ada@example.com",
            Phone: null,
            TaxId: taxId,
            IdType: idType,
            IdValue: idValue,
            CompanyName: companyName,
            AddressLine1: null,
            City: null,
            PostalCode: null,
            StateCode: null,
            CountryCode: null,
            Quantity: 1,
            IsGuestCheckout: true,
            CouponCode: null);
```

Command record parameter order (`InitiateCheckoutCommand.cs` lines 7–29):

```
TenantSlug, ProductSlug, Name, Email, Phone, TaxId, CompanyName,
AddressLine1, City, PostalCode, StateCode, CountryCode,
Quantity, IsGuestCheckout, CouponCode,
SessionId = null, Metadata = null, IdempotencyKey = null,
IdType = null, IdValue = null, Interval = null, PriceId = null
```

`GuestCommand` uses named arguments, so TIN / company / ID pair land in the correct slots. Custom-session tests then do `with { SessionId = session.Id }` to take the session path.

### 2.4 Related B2B initiate — `CreateCustomCheckoutAndInitiateSessionTests`

`InitiateCheckout_SessionId_StampsB2bMetadataAndRequiresTin` (lines 95–144) constructs the handler **inline**, also five-arg:

```110:119:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CreateCustomCheckoutAndInitiateSessionTests.cs
        var one = Substitute.For<IOneQueryService>();
        one.GetTenantIdBySlugAsync("acme").Returns(orgId);
        var comms = Substitute.For<ICommunicationsQueryService>();
        comms.HasValidEmailConfigAsync(orgId).Returns(true);
        var mediator = Substitute.For<IMediator>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["App:ClientUrl"] = "http://localhost:3004" })
            .Build();

        var handler = new InitiateCheckoutCommandHandler(one, repository, mediator, config, comms);
```

Same five-arg `new`. Same implicit `billingQueryService: null`.

This test's second `InitiateCheckoutCommand` uses **positional** arguments plus named `IdType` / `IdValue`. That is correct for the record order (`TaxId` then `CompanyName` at positions 6 and 7):

```132:136:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CreateCustomCheckoutAndInitiateSessionTests.cs
        var result = await handler.Handle(new InitiateCheckoutCommand(
            "acme", "custom", "Buyer", "buyer@example.com", null, "C111122223333", "Buyer Sdn Bhd",
            null, null, null, null, null, 1, true, null, session.Id,
            IdType: "BRN",
            IdValue: "202401001234"), CancellationToken.None);
```

The first handle in the same test (missing TIN) is also positional and also never reaches the TIN check — see §4.6.

The sibling test `InitiateCheckout_CompletedSession_Throws` (lines 146–173) also uses five-arg `new` (split across 165–166) **and still passes**, because the completed-session guard fires *before* SST. See §7.

`CreateCustomCheckout_AllocatesQuoteNumberOnce` and `CreateCustomCheckout_Net30_SetsDueAtAbout30Days` never construct `InitiateCheckoutCommandHandler`.

### 2.5 Contrast: the fixture that already does the right thing

`QuoteOfflineSstTests.InitiateCustom_SstMerchant_ChargesGrossAndStampsMetadata` is the only initiate test that already passes billing. It uses the **six-argument** constructor:

```55:56:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/QuoteOfflineSstTests.cs
        var handler = new InitiateCheckoutCommandHandler(
            one, repository, mediator, config, comms, SstBilling(orgId));
```

Its stub returns an SST-**registered** merchant (`Sst_registration_number = "W10-1234-12345678"`) because that test is about charging 5400 on a 5000 quote. That is the opposite polarity from what the B2B fixtures need. Do **not** copy `QuoteOfflineSstTests.SstBilling` into the B2B helpers. Copy the empty-number pattern from `SubscriptionBillingAmountTests` instead (see §5).

### 2.6 Contrast: 167 already updated the SST unit tests

After 167, `Gross_NoSst_Is100` no longer passes `billing: null`. It passes a stub with an empty SST number:

```75:82:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SubscriptionBillingAmountTests.cs
    public async Task Gross_NoSst_Is100()
    {
        var (sub, product) = Create(unitAmount: 100m, quantity: 1);

        (await SubscriptionBillingAmount.Gross(sub, product, SstBilling(sub.OrganizationId, sstNumber: ""))).Should().Be(100m);
        SubscriptionBillingAmount.Line(sub, product).Should().Be(100m);
        sub.UnitAmount.Should().Be(100m);
    }
```

And 167 added the explicit null-billing contract test:

```120:125:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SubscriptionBillingAmountTests.cs
    [Test]
    public async Task MerchantHasSst_Null_Billing_Throws()
    {
        var act = () => SubscriptionBillingAmount.MerchantHasSstAsync(null, Guid.CreateVersion7());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*refusing to undercharge*");
    }
```

Helper used by both:

```127:137:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SubscriptionBillingAmountTests.cs
    private static IBillingQueryService SstBilling(Guid organizationId, string sstNumber)
    {
        var billing = Substitute.For<IBillingQueryService>();
        billing.GetBillingProfileAsync(organizationId).Returns(new TenantBillingProfileDto
        {
            Legal_name = "Acme",
            Tin = "C12345678901",
            Sst_registration_number = sstNumber
        });
        return billing;
    }
```

`MerchantHasSstAsync` treats empty/whitespace `Sst_registration_number` as "merchant does not have SST" (`!string.IsNullOrWhiteSpace(profile?.Sst_registration_number)`). That is the stub polarity the B2B tests must use.

### 2.7 What production does (why this is a test-only hole)

`Program.cs` line 227 calls `builder.Services.AddAllModules(builder.Configuration)`.

```20:33:apps/lazuar-api/src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs
    public static IServiceCollection AddAllModules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOneModule(configuration);
        services.AddMessagingModule(configuration);
        services.AddCrmModule(configuration);
        services.AddPaymentsModule(configuration);
        services.AddOpsModule(configuration);
        services.AddBillingModule(configuration);
        services.AddLhdnModule(configuration);
        services.AddCommerceModule(configuration);
        services.AddCommunicationsModule(configuration);
        return services;
    }
```

`AddBillingModule` (called **before** `AddCommerceModule`) registers the live implementation:

```51:51:apps/lazuar-api/Modules/Billing/Infrastructure/DependencyInjection.cs
        services.AddScoped<IBillingQueryService, BillingQueryService>();
```

MediatR assembly scan (`MediatRRegistrationExtensions.cs`) registers `InitiateCheckoutCommandHandler` from `Modules.Commerce.Application`. At resolve time the container has `IBillingQueryService`, so the optional sixth parameter is **not** null in production. HTTP entry is `PublicCheckoutEndpoints.MapPost("/checkout")`, which does `mediator.Send(command)` and never `new`s the handler.

`BillingQueryService.GetBillingProfileAsync` (lines 321–329) maps `SstRegistrationNumber` onto `TenantBillingProfileDto.Sst_registration_number`. Missing profile → `null` → `MerchantHasSstAsync` returns `false`. That is fail-closed on *composition*, fail-open-on-tax only when the merchant genuinely has no SST id.

### 2.8 Inventory of every `new InitiateCheckoutCommandHandler` in ModuleTests

| File | Line | Args | Billing? | Effect after 167 |
|------|------|------|----------|------------------|
| `CheckoutB2bIdentityTests.cs` | 265 | 5 | no | assigned failures (via helpers) |
| `CreateCustomCheckoutAndInitiateSessionTests.cs` | 119 | 5 | no | assigned failure `SessionId_StampsB2b...` |
| `CreateCustomCheckoutAndInitiateSessionTests.cs` | 165–166 | 5 | no | still passes (`CompletedSession_Throws`) |
| `QuoteOfflineSstTests.cs` | 55–56 | 6 | yes, SST **on** | still passes |
| `CommerceProductCompletenessTests.cs` | 395 | 5 | no | still passes (`EnforcesRequiresPhone` — throws before SST) |
| `CommerceProductCompletenessTests.cs` | 740 | 5 | no | residual fail (`ZeroAmountCoupon_...`) |
| `CommerceProductCompletenessTests.cs` | 780 | 5 | no | residual fail (`PaidPath_...`) |
| `CommerceProductCompletenessTests.cs` | 1410 (`CreateInitiateHandler`) | 5 | no | residual fail for every happy-path initiate in that fixture |

There is no other `new InitiateCheckoutCommandHandler` in the repo.

---

## 3. Exact production call sites that now throw (file + line + snippet)

### 3.1 The throw itself — `MerchantHasSstAsync`

```93:103:apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs
    public static async Task<bool> MerchantHasSstAsync(IBillingQueryService? billing, Guid organizationId)
    {
        if (billing == null)
        {
            throw new InvalidOperationException(
                "IBillingQueryService is required to decide SST; refusing to undercharge.");
        }

        var profile = await billing.GetBillingProfileAsync(organizationId);
        return !string.IsNullOrWhiteSpace(profile?.Sst_registration_number);
    }
```

167 changed the `if (billing == null)` arm from `return false;` to the throw. Diff of `49606466` (only this method in this file):

```diff
         if (billing == null)
         {
-            return false;
+            throw new InvalidOperationException(
+                "IBillingQueryService is required to decide SST; refusing to undercharge.");
         }
```

NUnit stack traces on this HEAD name **line 97** as the throw site (the `throw` statement).

Wrappers that always call `MerchantHasSstAsync` first:

```105:121:apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs
    public static async Task<Breakdown> GrossBreakdown(
        Subscription sub,
        Product product,
        IBillingQueryService? billing)
    {
        var merchantHasSst = await MerchantHasSstAsync(billing, sub.OrganizationId);
        return GrossBreakdown(sub, product, merchantHasSst);
    }

    public static async Task<decimal> Gross(
        Subscription sub,
        Product product,
        IBillingQueryService? billing)
    {
        var breakdown = await GrossBreakdown(sub, product, billing);
        return breakdown.Gross;
    }
```

The initiate handler does **not** use those wrappers. It calls `MerchantHasSstAsync` itself, then `CustomQuoteBreakdown` / `GrossBreakdown(..., bool merchantHasSst)`.

### 3.2 Custom-session (quote / `SessionId`) path — handler line 121

Taken when `request.SessionId.HasValue`. SST is decided **before** B2B TIN / company / ID-pair validation.

```105:159:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
        if (request.SessionId.HasValue)
        {
            var existingSession = await _repository.GetCheckoutSessionByIdAsync(tenantId.Value, request.SessionId.Value, ct);
            if (existingSession == null || existingSession.OrganizationId != tenantId.Value || existingSession.Status != "OPEN")
            {
                throw new InvalidOperationException("Invalid or completed custom checkout session.");
            }

            if (CommerceCheckoutIdempotency.TryReplayUrl(existingSession, DateTime.UtcNow, out var existingQuoteUrl))
            {
                return new CheckoutResultDto(existingQuoteUrl!, false);
            }

            existingSession.SetIdempotency(idempotencyKey, fingerprint);

            var customNet = existingSession.AdHocLineItems.Sum(x => x.UnitPrice * x.Quantity);
            var customMerchantHasSst = await SubscriptionBillingAmount.MerchantHasSstAsync(
                _billingQueryService, tenantId.Value);
            var customBreakdown = SubscriptionBillingAmount.CustomQuoteBreakdown(
                customNet, customMerchantHasSst);
            var customTotalAmount = customBreakdown.Gross;
            // ...
            var customMetadata = new Dictionary<string, string>
            {
                { "type", "custom_payment_link" },
                { "subscription_id", existingSession.Id.ToString() },
                { "tenant_id", tenantId.Value.ToString() },
                { "is_b2b_required", existingSession.IsB2bRequired ? "true" : "false" }
            };
            SubscriptionBillingAmount.StampSstMetadata(customMetadata, customBreakdown);
            // ...
            if (existingSession.IsB2bRequired)
            {
                if (string.IsNullOrWhiteSpace(request.TaxId))
                {
                    throw new InvalidOperationException("This payment request requires a tax ID.");
                }
                // company name ...
                if (string.IsNullOrWhiteSpace(request.IdType) || string.IsNullOrWhiteSpace(request.IdValue))
                {
                    throw new InvalidOperationException("This payment request requires buyer ID type and ID value (BRN / NRIC / PASSPORT / ARMY).");
                }
```

Stack traces for the four custom-session assigned failures name **handler line 121**.

Order of operations on this path (everything after line 103 `if (request.SessionId.HasValue)`):

1. Load session; throw `"Invalid or completed custom checkout session."` if missing / wrong tenant / not `OPEN`. **This is why `InitiateCheckout_CompletedSession_Throws` still passes.**
2. Replay stored gateway URL via `CommerceCheckoutIdempotency.TryReplayUrl` if the session is still OPEN, unexpired, and already has `GatewayCheckoutUrl`. **This is the second handle in `CopiesIsB2bRequiredIntoMetadata` — never reached today.**
3. `SetIdempotency` (no-op when the test did not send `IdempotencyKey`).
4. Sum ad-hoc line nets.
5. **`MerchantHasSstAsync(_billingQueryService, tenantId)` ← THROWS in these tests.**
6. `CustomQuoteBreakdown(net, merchantHasSst)` — uses `SstTaxMath.ServiceTax` (`"02"`) at `DefaultServiceTaxRatePercent` (8m). If the stub later says the merchant *has* SST, 250 becomes 270 and 100 becomes 108. B2B tests do not assert amount, but other residual tests do (`StillSendsLineSumAndQuantityOne` expects 500).
7. Stamp `is_b2b_required` from `existingSession.IsB2bRequired` (always `"true"` or `"false"`, unlike the product path which only stamps `"true"`).
8. `StampSstMetadata` — no-op when tax is 0.
9. **Only then** B2B validation (TIN, company, ID pair) and `ResolveClientProfileCommand`.
10. `GenerateCheckoutSessionQuery` with `customTotalAmount` and `Quantity = 1`.

This ordering is why `MissingIdPair_Throws` and `SessionId_StampsB2b...` (missing-TIN half) do not get the exception they asked for. The SST throw sits in front of the identity throws.

### 3.3 Product (catalog slug) path — handler line 339

Taken when `SessionId` is null. Product is loaded, quantity/price resolved, `EnforceCheckoutConfiguration` runs, CRM is resolved, session is persisted, **then** SST is decided.

```338:347:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
        var unitNet = isTrial ? 0m : Math.Max(0, resolved.Amount - unitDiscount);
        var merchantHasSst = await SubscriptionBillingAmount.MerchantHasSstAsync(
            _billingQueryService, tenantId.Value);
        var breakdown = SubscriptionBillingAmount.GrossBreakdown(
            unitNet, quantity, product.SstTaxType, product.SstRatePercent, merchantHasSst);
        var sstType = breakdown.TaxType;
        var unitTax = breakdown.UnitTax;
        var unitGross = breakdown.UnitGross;
        var lineNet = breakdown.Gross;
```

Stack traces for the two product-path assigned failures name **handler line 339**.

Order of operations on this path that matters to B2B:

1. Tenant + email-config guards (tests stub both to succeed).
2. Idempotency lookup (tests do not send a key).
3. Skip the `SessionId` branch.
4. Load product by slug (`pro-plan`); tests stub this.
5. `CommerceCheckoutQuantity.NormalizeOrThrow` — not in play (qty 1).
6. `ResolveCheckoutPrice` — catalog `one_time` 100m.
7. Trial check — `TrialDays` is 0.
8. **`EnforceCheckoutConfiguration` (lines 472–510).** This is why `RequiresTaxId_MissingTin` still passes:
   - `RequiresTaxId && blank TaxId` → `"This product requires a tax ID at checkout."` (line 487)
   - `RequiresTaxId && blank IdType/IdValue` → `"This product requires buyer ID type and ID value (BRN / NRIC / PASSPORT / ARMY)."` (line 492)
   - `RequiresTaxId && blank CompanyName` → `"This product requires a company name at checkout."` (line 497)
9. Build + send `ResolveClientProfileCommand` with `Tin`, `IdType`, `IdValue`, `CompanyName` from the request (lines 239–251). **Reached by `RequiresTaxId_WithTinAndCompany_...` only after SST is unblocked.** Tests stub this to return a new Guid.
10. Persist `CheckoutSession`. If `request.TaxId` is non-blank, persist metadata gets `is_b2b_required = "true"` (lines 283–287). Product-flag-off test sends `taxId: null`, so this stamp is skipped.
11. **`MerchantHasSstAsync` ← THROWS for the two product happy-path B2B tests.**
12. `GrossBreakdown(unitNet, qty, product.SstTaxType, product.SstRatePercent, merchantHasSst)`. Default product tax type `"06"` + rate `0` → `SstTaxMath` returns `("06", 0)` even if the merchant *were* SST-registered. See `SstTaxMathTests.Product06_NoTax`.
13. `isB2bRequired = !string.IsNullOrWhiteSpace(request.TaxId)` (line 347) — **request TIN**, not the product flag. Product-flag-off with no TIN → `false` → `MergeClientIntoGateway` omits the key.
14. Paid hop-2 `GenerateCheckoutSessionQuery` with `unitGross` and `quantity`.

### 3.4 `SstTaxMath` and `CustomQuoteBreakdown` (why stub polarity matters)

```8:24:apps/lazuar-api/Modules/Commerce/Application/SstTaxMath.cs
    public static (string TaxType, decimal TaxAmount) Compute(
        string? requestedType,
        decimal ratePercent,
        decimal netAmount,
        bool merchantHasSstRegistration)
    {
        if (!merchantHasSstRegistration
            || !string.Equals(requestedType, ServiceTax, StringComparison.OrdinalIgnoreCase)
            || ratePercent <= 0
            || netAmount <= 0)
        {
            return (NotApplicable, 0m);
        }
        // ...
    }
```

```41:42:apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs
    public static Breakdown CustomQuoteBreakdown(decimal net, bool merchantHasSst) =>
        GrossBreakdown(net, 1, SstTaxMath.ServiceTax, DefaultServiceTaxRatePercent, merchantHasSst);
```

- Product path + default catalog product (`"06"` / 0%): tax is always 0, regardless of merchant SST id.
- Custom-session path: tax type is **forced to `"02"` at 8%** when `merchantHasSst` is true. A stub that returns `W10-1234-12345678` changes the hop-2 amount. The assigned B2B tests do not assert amount, but wiring the SST-**on** stub is the wrong default and will break residual amount assertions in `CommerceProductCompletenessTests`.

### 3.5 B2B metadata stamping (what the tests are actually trying to prove)

Custom session, always written, from the session flag:

```130:136:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
            var customMetadata = new Dictionary<string, string>
            {
                { "type", "custom_payment_link" },
                { "subscription_id", existingSession.Id.ToString() },
                { "tenant_id", tenantId.Value.ToString() },
                { "is_b2b_required", existingSession.IsB2bRequired ? "true" : "false" }
            };
```

Product hop-2, only when the **request** carried a TIN:

```44:68:apps/lazuar-api/Modules/Commerce/Application/CommerceCheckoutMetadata.cs
    public static Dictionary<string, string> MergeClientIntoGateway(
        IReadOnlyDictionary<string, string>? client,
        Guid tenantId,
        Guid sessionId,
        bool isB2bRequired = false)
    {
        // ... type / subscription_id / tenant_id ...
        if (isB2bRequired)
        {
            result["is_b2b_required"] = "true";
        }
        return result;
    }
```

CRM command the ID-pair tests assert (note `CompanyName` is its own argument; it is **not** `IdValue`):

```7:18:apps/lazuar-api/Modules/CRM/Contracts/ResolveClientProfileCommand.cs
public record ResolveClientProfileCommand(
    Guid OrganizationId,
    string FullName,
    string Email,
    string Phone,
    string? Tin = null,
    string? IdType = null,
    string? IdValue = null,
    BillingAddressDto? BillingAddress = null,
    bool ConsentedToMarketing = false,
    string? CompanyName = null
) : ICommand<Guid>
```

Custom-session resolve (handler lines 174–183) and product-path resolve (lines 239–249) both pass `IdType` / `IdValue` / `CompanyName` / `Tin` separately. That is the whole point of `PassesIdPairNamed_NotCompanyNameAsIdValue` and the `c.IdValue != "Acme Sdn Bhd"` clause in the copy-metadata test.

### 3.6 Other production `MerchantHasSstAsync` call sites (not in the assigned failures, same 167 contract)

These also throw if billing is null. They are listed so a later implementer does not treat initiate as a one-off:

| File | Approx. line | Context |
|------|--------------|---------|
| `InitiateCheckoutCommandHandler.cs` | 121, 339 | assigned |
| `MarkCheckoutAsPaidOfflineCommandHandler.cs` | 97, 195 | `QuoteOfflineSstTests` already passes billing |
| `CommerceQueryService.CustomCheckouts.cs` | 59, 115 | list/get quote totals |
| `GatewayPaymentFailedIntegrationEventHandler.cs` | 139 | failed-payment amount |
| `BillingEngineJob.cs` | 282 | production now `GetRequiredService` after 167 |
| `PublicArrearsEndpoints.cs` | 144, 228 | production now `GetRequiredService` after 167 |
| `SubscriptionLifecycleIntegrationEventHandlers.cs` | 111 | webhook payload amount |

167's commit message: *"MerchantHasSstAsync throws if IBillingQueryService is missing so a registration typo cannot charge net. Billing, dunning, and arrears resolve billing with GetRequiredService."*

---

## 4. Why each of the 6 tests fails (per-test)

Re-run evidence is quoted from the 18 August 2026 `dotnet test` on HEAD `4531f210`. All six share the same root cause. The *surface* differs by whether the test asserts an exception message or waits for a mediator call that never happens.

### 4.1 `InitiateCheckout_CustomSession_CopiesIsB2bRequiredIntoMetadata`

**What it wants**

1. Custom `CheckoutSession` with `isB2bRequired: true`, line item Consulting × 1 @ 250.
2. First `Handle` with TIN + company + `IdType=BRN` + `IdValue=202401001234` + `SessionId`.
3. Assert hop-2 `GenerateCheckoutSessionQuery.Metadata["is_b2b_required"] == "true"`.
4. Reconfigure the mediator to return a *different* URL, call `Handle` again on the same session, assert replay returns the **first** URL (`https://gateway.test/pay/custom`) and that `GenerateCheckoutSessionQuery` was sent only once.
5. Assert `ResolveClientProfileCommand` carried TIN, company, `BRN`, `202401001234`, and `IdValue != "Acme Sdn Bhd"`.

**How it constructs the handler**

```94:105:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CheckoutB2bIdentityTests.cs
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCheckoutSessionByIdAsync(Arg.Any<Guid>(), session.Id, Arg.Any<CancellationToken>()).Returns(session);

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns("https://gateway.test/pay/custom");

        var handler = CreateHandler(orgId, repository, mediator);
        await handler.Handle(
            GuestCommand(taxId: "C12345678901", companyName: "Acme Sdn Bhd", idType: "BRN", idValue: "202401001234")
                with { SessionId = session.Id },
            CancellationToken.None);
```

`CreateHandler` → five-arg `new` → `_billingQueryService` is null.

**Where it dies**

Custom-session branch, line 121, first `Handle`, before metadata is built, before `GenerateCheckoutSessionQuery`, before `ResolveClientProfileCommand`, before `SetGatewayCheckoutUrl` (so replay can never work either).

**NUnit**

```
Failed InitiateCheckout_CustomSession_CopiesIsB2bRequiredIntoMetadata [148 ms]
Error Message:
 System.InvalidOperationException : IBillingQueryService is required to decide SST; refusing to undercharge.
Stack Trace:
   at SubscriptionBillingAmount.MerchantHasSstAsync (...) SubscriptionBillingAmount.cs:line 97
   at InitiateCheckoutCommandHandler.Handle (...) InitiateCheckoutCommandHandler.cs:line 121
   at CheckoutB2bIdentityTests.InitiateCheckout_CustomSession_CopiesIsB2bRequiredIntoMetadata() CheckoutB2bIdentityTests.cs:line 102
```

Line 102 is the first `await handler.Handle(...)`.

**After a no-SST stub**

- `CustomQuoteBreakdown(250, false)` → Gross 250. No `sst_*` keys. Metadata still has `is_b2b_required=true`.
- First hop-2 send happens; `session.SetGatewayCheckoutUrl("https://gateway.test/pay/custom")`.
- Second handle hits `TryReplayUrl` (session OPEN, unexpired, URL set) and returns the first URL without calling `MerchantHasSstAsync` again. The `Received(1)` on `GenerateCheckoutSessionQuery` still holds.
- CRM resolve assertion can run.

No product-code change required for this test to pass.

### 4.2 `InitiateCheckout_CustomSession_MissingIdPair_Throws`

**What it wants**

Custom B2B session, TIN + company present, **no** `IdType` / `IdValue`. Expect:

```
InvalidOperationException with message matching *ID type*
```

which is handler line 158:

`"This payment request requires buyer ID type and ID value (BRN / NRIC / PASSPORT / ARMY)."`

**How it constructs the handler**

```146:149:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CheckoutB2bIdentityTests.cs
        var handler = CreateHandler(orgId, repository, Substitute.For<IMediator>());
        var act = async () => await handler.Handle(
            GuestCommand(taxId: "C12345678901", companyName: "Acme Sdn Bhd") with { SessionId = session.Id },
            CancellationToken.None);
```

`GuestCommand` defaults `idType` / `idValue` to null. Five-arg handler.

**Where it dies**

Same custom-session SST call at line 121, **before** the ID-pair check at lines 156–159. FluentAssertions therefore sees *an* `InvalidOperationException`, but the message is the SST string.

**NUnit (this is the assigned “expected `*ID type*` but got SST” case)**

```
Failed InitiateCheckout_CustomSession_MissingIdPair_Throws [95 ms]
Error Message:
 Expected exception message to match the equivalent of "*ID type*", but
 "IBillingQueryService is required to decide SST; refusing to undercharge." does not.
Stack Trace:
   ...
   at CheckoutB2bIdentityTests.InitiateCheckout_CustomSession_MissingIdPair_Throws()
      CheckoutB2bIdentityTests.cs:line 151
```

Line 151 is `await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ID type*")`.

**After a no-SST stub**

Flow continues past line 121. `IsB2bRequired` is true, TIN and company are present, ID pair is blank → line 158 throws the expected message. Test passes with no product change.

Do not “fix” this by moving B2B validation above SST in the handler. The assigned remediation is the test fixture. The product ordering is a one-query cost before a validation error; it is not why CI is red.

### 4.3 `InitiateCheckout_CustomSession_PassesIdPairNamed_NotCompanyNameAsIdValue`

**What it wants**

Same B2B custom session + full identity. Assert CRM command:

- `Tin == "C12345678901"`
- `CompanyName == "Acme Sdn Bhd"`
- `IdType == "BRN"`
- `IdValue == "202401001234"`

This is the regression lock for `8d4045f4 fix(commerce): pass quote B2B company name as CompanyName` / `e25d07d6 fix(commerce): require quote B2B ID pair and validate TIN`. Historically company name was (or was at risk of being) stuffed into `IdValue`.

**How it constructs the handler**

```169:179:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CheckoutB2bIdentityTests.cs
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns("https://gateway.test/pay/custom");
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Guid.CreateVersion7());

        var handler = CreateHandler(orgId, repository, mediator);
        await handler.Handle(
            GuestCommand(taxId: "C12345678901", companyName: "Acme Sdn Bhd", idType: "BRN", idValue: "202401001234")
                with { SessionId = session.Id },
            CancellationToken.None);
```

Five-arg again. CRM stub is in place but never reached.

**Where it dies**

Line 121, first `Handle` (test line 176).

**NUnit**

```
Failed InitiateCheckout_CustomSession_PassesIdPairNamed_NotCompanyNameAsIdValue [5 ms]
Error Message:
 System.InvalidOperationException : IBillingQueryService is required to decide SST; refusing to undercharge.
Stack Trace:
   at MerchantHasSstAsync (...) :line 97
   at Handle (...) :line 121
   at ...PassesIdPairNamed_NotCompanyNameAsIdValue() :line 176
```

**After a no-SST stub**

B2B block runs, `ResolveClientProfileCommand` is sent with the four named fields, assertion passes. Hop-2 amount is 250; this test does not look at it.

### 4.4 `InitiateCheckout_ProductFlagOff_DoesNotStampB2b`

**What it wants**

Product with `CheckoutConfiguration.RequiresTaxId = false`. Guest command with `taxId: null, companyName: null`. Assert hop-2 metadata is null, lacks `is_b2b_required`, or has it not equal to `"true"`.

**How it constructs the handler**

```68:74:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CheckoutB2bIdentityTests.cs
        var handler = CreateHandler(out var mediator, out _, requiresTaxId: false);
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Guid.CreateVersion7());
        mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns("https://gateway.test/pay");

        await handler.Handle(GuestCommand(taxId: null, companyName: null), CancellationToken.None);
```

Overload A → overload B → five-arg `new`. No `SessionId`, so product path.

**Where it dies**

`EnforceCheckoutConfiguration` is a no-op (`RequiresTaxId` is false). CRM resolve runs (mediator is stubbed). Session is persisted **without** `is_b2b_required` (TIN is blank). Then line 339 throws. `GenerateCheckoutSessionQuery` is never sent, so the `Received(1)` metadata assertion never runs.

**NUnit**

```
Failed InitiateCheckout_ProductFlagOff_DoesNotStampB2b [28 ms]
Error Message:
 System.InvalidOperationException : IBillingQueryService is required to decide SST; refusing to undercharge.
Stack Trace:
   at MerchantHasSstAsync (...) :line 97
   at Handle (...) :line 339
   at ...DoesNotStampB2b() :line 74
```

**After a no-SST stub**

`GrossBreakdown(100, 1, "06", 0, false)` → tax 0, gross 100. `isB2bRequired` is false because `request.TaxId` is blank. `MergeClientIntoGateway` omits the key. The `Received` predicate passes.

### 4.5 `InitiateCheckout_RequiresTaxId_WithTinAndCompany_ResolvesCrmWithoutIdValue_AndStampsB2b`

**What it wants (despite the historical name)**

The method name still says `ResolvesCrmWithoutIdValue`. The body **does** send `idType: "BRN", idValue: "202401001234"` and asserts `c.IdValue == "202401001234"`. This is leftover naming from the era when company name was used as the ID value. Do not “fix” the test by stripping `IdValue`; `EnforceCheckoutConfiguration` would then throw the product ID-pair message and the CRM assertion would be wrong.

```36:62:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CheckoutB2bIdentityTests.cs
        var handler = CreateHandler(out var mediator, out _, requiresTaxId: true);
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Guid.CreateVersion7());
        mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns("https://gateway.test/pay");

        await handler.Handle(
            GuestCommand(taxId: "C12345678901", companyName: "Acme Sdn Bhd", idType: "BRN", idValue: "202401001234"),
            CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<ResolveClientProfileCommand>(c =>
                c.Tin == "C12345678901"
                && c.CompanyName == "Acme Sdn Bhd"
                && c.IdValue == "202401001234"
                && c.IdType == "BRN"),
            Arg.Any<CancellationToken>());

        await mediator.Received(1).Send(
            Arg.Is<GenerateCheckoutSessionQuery>(q =>
                q.Metadata != null
                && q.Metadata.ContainsKey("is_b2b_required")
                && q.Metadata["is_b2b_required"] == "true"),
            Arg.Any<CancellationToken>());
```

**Where it dies**

Product path. `EnforceCheckoutConfiguration` passes (TIN, company, ID pair all present). CRM **does** run *before* SST on this path (line 251 vs 339). That is a subtle difference from the custom-session path:

- Custom session: SST (121) **then** CRM (174).
- Product path: CRM (251) **then** persist session **then** SST (339) **then** hop-2.

So on this test, `ResolveClientProfileCommand` is actually sent before the throw. The first `Received` *would* succeed if NUnit got there. NUnit never gets there because `Handle` throws at 339, so both assertions are skipped.

**NUnit**

```
Failed InitiateCheckout_RequiresTaxId_WithTinAndCompany_ResolvesCrmWithoutIdValue_AndStampsB2b [5 ms]
Error Message:
 System.InvalidOperationException : IBillingQueryService is required to decide SST; refusing to undercharge.
Stack Trace:
   at MerchantHasSstAsync (...) :line 97
   at Handle (...) :line 339
   at ...AndStampsB2b() :line 45
```

Line 45 is the `await handler.Handle(...)`.

**After a no-SST stub**

Hop-2 is minted. `request.TaxId` is non-blank → `isB2bRequired` true → metadata stamp. Both `Received` assertions pass. Product tax type `"06"` keeps amount at 100.

### 4.6 `InitiateCheckout_SessionId_StampsB2bMetadataAndRequiresTin`

**What it wants (two acts, one test)**

1. Custom B2B session (`isB2bRequired: true`, Work × 1 @ 100, gateway `BILLPLZ`).
2. First `Handle` with **no TIN / no company / no ID pair**. Expect `InvalidOperationException` matching `*tax ID*` — handler line 148: `"This payment request requires a tax ID."`
3. Second `Handle` with TIN `C111122223333`, company `Buyer Sdn Bhd`, `IdType=BRN`, `IdValue=202401001234`. Expect hop-2 URL `https://pay.example/hop2` and metadata `is_b2b_required == "true"`.

**How it constructs the handler**

Inline five-arg `new` at line 119 (quoted in §2.4). Mediator stubs for hop-2 and CRM are installed **after** the first act (lines 127–130), which is fine for the intended ordering.

**Where it dies**

First act, custom-session line 121, **before** the TIN check at lines 146–148. Same FluentAssertions message-mismatch pattern as `MissingIdPair_Throws`, different expected wildcard.

**NUnit (this is the assigned “expected `*tax ID*` but got SST” case)**

```
Failed InitiateCheckout_SessionId_StampsB2bMetadataAndRequiresTin [12 ms]
Error Message:
 Expected exception message to match the equivalent of "*tax ID*", but
 "IBillingQueryService is required to decide SST; refusing to undercharge." does not.
Stack Trace:
   ...
   at CreateCustomCheckoutAndInitiateSessionTests.InitiateCheckout_SessionId_StampsB2bMetadataAndRequiresTin()
      CreateCustomCheckoutAndInitiateSessionTests.cs:line 125
```

Line 125 is:

```125:125:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CreateCustomCheckoutAndInitiateSessionTests.cs
        await missingTin.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tax ID*");
```

The second act is never executed. Even if the first assertion were deleted, the second act would throw the same SST exception at line 121.

**After a no-SST stub**

First act: SST lookup returns false, breakdown is net 100, then TIN check throws the expected message. Second act: CRM + hop-2 run, metadata has `is_b2b_required=true` (always written on the custom path from `existingSession.IsB2bRequired`). Test passes.

### 4.7 Side-by-side of the six surfaces

| Test | Path | Handler line that throws | Test line | Surface |
|------|------|--------------------------|-----------|---------|
| `CustomSession_CopiesIsB2bRequiredIntoMetadata` | session | 121 | 102 | uncaught SST |
| `CustomSession_MissingIdPair_Throws` | session | 121 | 151 | `WithMessage("*ID type*")` mismatch |
| `CustomSession_PassesIdPairNamed_...` | session | 121 | 176 | uncaught SST |
| `ProductFlagOff_DoesNotStampB2b` | product | 339 | 74 | uncaught SST |
| `RequiresTaxId_WithTinAndCompany_...` | product | 339 | 45 | uncaught SST |
| `SessionId_StampsB2bMetadataAndRequiresTin` | session | 121 | 125 | `WithMessage("*tax ID*")` mismatch |

Exception type is `InvalidOperationException` in every case. That is why the two `ThrowAsync<InvalidOperationException>()` tests do not fail on type; they fail on message.

---

## 5. Recommended fix

### 5.1 Do this

Update the **test fixtures** so every `new InitiateCheckoutCommandHandler` that is expected to reach amount / hop-2 / B2B-after-SST receives an `IBillingQueryService` stub.

Polarity: **no SST id**, matching `SubscriptionBillingAmountTests.Gross_NoSst_Is100` after 167 (`SstBilling(orgId, sstNumber: "")`).

That means:

- `GetBillingProfileAsync(organizationId)` returns a `TenantBillingProfileDto` whose `Sst_registration_number` is `""` or `null` (both are `IsNullOrWhiteSpace` → `MerchantHasSstAsync` returns `false`).
- Or returns `null` profile entirely — same `false`.
- Do **not** use `QuoteOfflineSstTests.SstBilling` (that one sets `W10-1234-12345678` and is for the SST-on quote test).

Minimum assigned-scope edits:

1. `CheckoutB2bIdentityTests.CreateHandler(Guid, ICommerceRepository, IMediator)` — add `using Modules.Billing.Contracts`, add a `NoSstBilling(Guid)` helper, pass it as the sixth constructor argument. Overload A already delegates here, so all five product/custom tests in the file pick it up, including the two that already pass.
2. `CreateCustomCheckoutAndInitiateSessionTests.InitiateCheckout_SessionId_StampsB2bMetadataAndRequiresTin` — pass the same sixth argument at the line-119 `new`. Optionally do the same at the line-165 `CompletedSession` `new` for consistency (not required for green).

### 5.2 Do not do this

- **Do not revert 167.** `MerchantHasSst_Null_Billing_Throws` is the lock. Fail-open (`return false`) is how a registration typo undercharges every renewal, dunning AUTO_CHARGE, arrears display, and hop-1. Production `AddAllModules` already composes billing; 167 exists for the composition footgun, not for today’s happy path.
- **Do not change `InitiateCheckoutCommandHandler` product behavior** to skip SST when the test did not pass billing. That re-opens 167 at the call site.
- **Do not move B2B validation above SST** as the way to make `MissingIdPair` / missing TIN pass. After the stub is in, those tests pass with the current order.
- **Do not make `IBillingQueryService` required in the handler constructor as the *only* fix without updating tests.** Making the sixth parameter required (drop `= null`) is a *good follow-up* because it turns this class of failure into a compile error, but it is not a substitute for the stub: a required parameter still has to be passed, and the stub still has to return no SST id so amounts stay net. If that follow-up is taken, every `new` in the table in §2.8 must be updated in the same PR, including `CommerceProductCompletenessTests` (see §7 residual).
- **Do not inject a live `BillingQueryService`.** These are NSubstitute unit tests with no billing schema.

### 5.3 Why empty SST id, not SST-on

Assigned B2B tests do not assert hop-2 `Amount`. An SST-on stub would still let the metadata / CRM assertions pass. It would **not** be a faithful “B2B identity, ignore tax” fixture, and it would poison any later assertion on quote totals. `CustomQuoteBreakdown` applies 8% whenever `merchantHasSst` is true. Residual `InitiateCheckout_CustomSession_StillSendsLineSumAndQuantityOne` asserts `q.Amount == 500m` on a 2 × 250 quote; SST-on would send 540 and fail that test after someone copies the wrong helper.

### 5.4 Production stays unchanged

`AddAllModules` → `AddBillingModule` → `AddScoped<IBillingQueryService, BillingQueryService>()` → MediatR constructs the handler with a real billing port → `MerchantHasSstAsync` does not throw → profile SST number decides tax. That path is not what is red.

---

## 6. Concrete patch sketch for `CheckoutB2bIdentityTests` (helpers, constructor arity)

This is a sketch for a later implementation PR. Do not apply it in this analysis task.

### 6.1 Constructor arity after the sketch

Keep the production constructor as it is (6 parameters, last optional) unless a separate follow-up makes billing required. Tests stop using the 5-arg sugar and always pass argument 6.

```
new InitiateCheckoutCommandHandler(
    one,          // IOneQueryService
    repository,   // ICommerceRepository
    mediator,     // IMediator
    config,       // IConfiguration
    comms,        // ICommunicationsQueryService
    billing)      // IBillingQueryService  ← NEW, no-SST stub
```

Overload A stays 3 parameters of *test* concern (`out mediator`, `out repository`, `requiresTaxId`) and still delegates to overload B. Overload B stays 3 parameters of *test* concern (`orgId`, `repository`, `mediator`) and grows the `new` internally. No test method should have to mention billing.

### 6.2 Usings to add

```csharp
using Lazuar.ApiTypes;
using Modules.Billing.Contracts;
```

`TenantBillingProfileDto` lives in `Lazuar.ApiTypes` (already used that way in `SubscriptionBillingAmountTests`). `IBillingQueryService` lives in `Modules.Billing.Contracts`. The test project already references both (other Commerce fixtures compile them today).

### 6.3 Helper to add (copy polarity from `Gross_NoSst_Is100`)

Place next to the existing `CreateHandler` overloads at the bottom of `CheckoutB2bIdentityTests`:

```csharp
private static IBillingQueryService NoSstBilling(Guid organizationId)
{
    var billing = Substitute.For<IBillingQueryService>();
    billing.GetBillingProfileAsync(organizationId).Returns(new TenantBillingProfileDto
    {
        Legal_name = "Acme",
        Tin = "C12345678901",
        Sst_registration_number = ""
    });
    return billing;
}
```

`sstNumber: ""` is the exact argument `Gross_NoSst_Is100` uses. A `null` number is equivalent. Do not put `W10-...` here.

### 6.4 Change overload B only

Replace the five-arg `new` (current line 265):

```csharp
// before
return new InitiateCheckoutCommandHandler(one, repository, mediator, config, comms);

// after
return new InitiateCheckoutCommandHandler(
    one, repository, mediator, config, comms, NoSstBilling(orgId));
```

That is the entire functional change inside this file. Overload A already has `orgId` and already calls overload B. The three custom-session tests already call overload B with the same `orgId` they used to build the `CheckoutSession`, so `GetBillingProfileAsync` is stubbed for the tenant the handler will query.

### 6.5 What not to change in this file

- Do not touch `GuestCommand`.
- Do not rename `InitiateCheckout_RequiresTaxId_WithTinAndCompany_ResolvesCrmWithoutIdValue_AndStampsB2b` in the same PR unless you want a pure-rename noise commit. The body is already the ID-pair-present case.
- Do not add billing to `MergeClientIntoGateway_StampsB2bWhenRequested` — it never constructs the handler.
- Do not set `product.SetSst` on the catalog product in overload A. Default `"06"` / `0` is what keeps product hop-2 at 100 after the stub.

### 6.6 Related B2B initiate sketch (`CreateCustomCheckoutAndInitiateSessionTests`)

Either duplicate `NoSstBilling` in that fixture or extract a tiny shared test helper later. Smallest assigned-scope change:

```csharp
// line 119, before
var handler = new InitiateCheckoutCommandHandler(one, repository, mediator, config, comms);

// after
var handler = new InitiateCheckoutCommandHandler(
    one, repository, mediator, config, comms, NoSstBilling(orgId));
```

Add the same `using`s and helper. Optionally update the `CompletedSession` constructor at lines 165–166 the same way so the file has one construction style.

### 6.7 Suggested later shared helper (not required for the six)

If residual `CommerceProductCompletenessTests.CreateInitiateHandler` is fixed in the same wave, lift `NoSstBilling` to something like `Lazuar.ModuleTests.Commerce.BillingStubs.NoSst(Guid)` so three fixtures do not each grow a 10-line clone. That is a convenience, not a prerequisite.

### 6.8 How to prove the sketch

```
dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj \
  --filter "FullyQualifiedName~CheckoutB2bIdentityTests|FullyQualifiedName~InitiateCheckout_SessionId_StampsB2bMetadataAndRequiresTin"
```

Expect 8 passed / 0 failed (`CheckoutB2bIdentityTests` has 7 tests; plus the one related initiate = 8). Then, if residual work is in scope:

```
dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj \
  --filter "FullyQualifiedName~CommerceProductCompletenessTests.InitiateCheckout"
```

That second filter is **not** green after only the B2B helper change. See §7.

---

## 7. Residual risks / other tests in this file that still pass

### 7.1 Inside `CheckoutB2bIdentityTests` — 2 of 7 still pass

Re-run: 5 failed, 2 passed in this fixture.

**`InitiateCheckout_RequiresTaxId_MissingTin_ThrowsExistingMessage` (lines 25–34) — PASSES**

```25:34:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CheckoutB2bIdentityTests.cs
    public async Task InitiateCheckout_RequiresTaxId_MissingTin_ThrowsExistingMessage()
    {
        var handler = CreateHandler(out _, out _, requiresTaxId: true);

        var act = async () => await handler.Handle(GuestCommand(taxId: null, companyName: "Acme"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("This product requires a tax ID at checkout.");
    }
```

Product path. `EnforceCheckoutConfiguration` at handler line 485–488 throws **before** line 339. Same five-arg construction, but SST is unreachable. After the helper sketch this test still passes (configuration throw is unchanged). It is also the lock that the product-flag TIN message did not regress when quote-side copy became `"This payment request requires a tax ID."`.

**`MergeClientIntoGateway_StampsB2bWhenRequested` (lines 190–196) — PASSES**

```190:196:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CheckoutB2bIdentityTests.cs
    public void MergeClientIntoGateway_StampsB2bWhenRequested()
    {
        var merged = CommerceCheckoutMetadata.MergeClientIntoGateway(
            null, Guid.CreateVersion7(), Guid.CreateVersion7(), isB2bRequired: true);
        merged["is_b2b_required"].Should().Be("true");
    }
```

Pure function. No handler. No billing. Independent of 167. Keep it.

### 7.2 Inside `CreateCustomCheckoutAndInitiateSessionTests` — 3 of 4 still pass

Re-run of the whole fixture + `QuoteOfflineSstTests`: 1 failed (`SessionId_StampsB2b...`), 7 passed.

Still passing in this fixture:

- `CreateCustomCheckout_AllocatesQuoteNumberOnce` — `CreateCustomCheckoutCommandHandler` only.
- `CreateCustomCheckout_Net30_SetsDueAtAbout30Days` — same.
- `InitiateCheckout_CompletedSession_Throws` — five-arg handler, but `session.Complete()` makes `Status != "OPEN"`, so handler line 110 throws `"Invalid or completed custom checkout session."` before line 121. After the sketch this remains green.

`QuoteOfflineSstTests` (4 tests) all still pass, including `InitiateCustom_SstMerchant_ChargesGrossAndStampsMetadata`, because that fixture already passes billing.

### 7.3 Residual: `CommerceProductCompletenessTests` initiate happy paths — same bug, not assigned

Re-run filter `FullyQualifiedName~CommerceProductCompletenessTests.InitiateCheckout` on this HEAD:

```
Failed: 12
Passed:  6
Total:  18
```

All 12 failures are the same SST throw. Construction is `CreateInitiateHandler` (line 1410, five-arg `new`) or an inline five-arg `new`.

**Fail (product path, handler line 339)**

- `InitiateCheckout_FixedOneTime_Qty3_PersistsSessionAndPaidOrderQuantity` (test line ~1198)
- `InitiateCheckout_FixedOneTime_Qty3_SendsUnitNetAndQuantity` (test line ~1072)
- `InitiateCheckout_FixedOneTime_Qty3_TenPercentCoupon_SendsUnitNetNinety` (test line ~1108)
- `InitiateCheckout_FixedRecurring_NonOneQuantity_Persists("mo","FIXED",3)`
- `InitiateCheckout_FixedRecurring_NonOneQuantity_Persists("yr","FIXED",2)`
- `InitiateCheckout_HundredPercentCoupon_BillplzMonthly_StillBypasses` (test line ~687)
- `InitiateCheckout_HundredPercentCoupon_Qty3_WritesZeroAmountOrderWithQuantity` (test line ~1301)
- `InitiateCheckout_HundredPercentCoupon_StripeMonthly_MintsHop2SetupSession` (test line ~546)
- `InitiateCheckout_PaidPath_KeepsSessionOpen_AndReturnsGatewayUrl` (test line ~781)
- `InitiateCheckout_TrialStripeMonthly_MintsHop2WithCommerceType` (test line ~585)
- `InitiateCheckout_ZeroAmountCoupon_ReturnsSuccessUrlWithSessionId_AndCompletesSession` (test line ~741)

**Fail (custom-session path, handler line 121)**

- `InitiateCheckout_CustomSession_StillSendsLineSumAndQuantityOne` (test line ~1259) — asserts `Amount == 500m && Quantity == 1`. **Must** use a no-SST stub, not an SST-on stub.

**Pass (throw before SST)**

- `InitiateCheckout_EnforcesRequiresPhone` (inline five-arg `new` at line 395; `EnforceCheckoutConfiguration` phone check)
- `InitiateCheckout_FixedOneTime_OutOfRangeQuantity_ThrowsBeforePersist` × 3 (`0`, `-1`, `100`) — quantity guard before CRM and before SST
- `InitiateCheckout_Pwyw_NonOneQuantity_ThrowsAndDoesNotPersist` × 2 — PWYW quantity rule before CRM/SST

Fixing only `CheckoutB2bIdentityTests` + the one related test leaves these 12 red. The same `NoSstBilling` passed from `CreateInitiateHandler` clears them, provided polarity is no-SST so the 500 / 100 / 90 amount assertions stay valid.

### 7.4 Residual: optional constructor default remains a footgun

Even after fixtures are updated, the production constructor still defaults billing to null. The next person who `new`s the handler in a test will fail the same way. Options, none of which are required to un-red the six:

1. Leave the default; document in the handler XML that tests must pass billing.
2. Remove `= null` so omitted billing is a compile error. Production DI still works. Every `new` must pass something (use `NoSstBilling`).
3. Add an analyzer / architecture test that forbids five-arg construction. Overkill.

Prefer (1) now + (2) if another wave keeps tripping.

### 7.5 Residual: custom-session SST-before-B2B ordering

In production (billing present) a buyer who omits TIN on a B2B quote still pays the cost of `GetBillingProfileAsync` before seeing `"This payment request requires a tax ID."` That is not a CI failure and not 167. Do not couple it to this fix.

### 7.6 Residual: product-path B2B flag is request-TIN, not product flag

`isB2bRequired` on product hop-2 is `!string.IsNullOrWhiteSpace(request.TaxId)` (line 347), while `EnforceCheckoutConfiguration` uses `product.CheckoutConfiguration.RequiresTaxId`. A product with `RequiresTaxId: true` cannot reach hop-2 without a TIN, so the stamp still appears. A product with the flag off *and* a voluntary TIN would stamp B2B. `ProductFlagOff_DoesNotStampB2b` only covers the no-TIN case. Out of scope.

### 7.7 Residual: other `IBillingQueryService? = null` constructors

Same optional-last-arg pattern exists on `MarkCheckoutAsPaidOfflineCommandHandler`, `CommerceQueryService`, `SubscriberQueryService`, `GatewayPaymentFailedIntegrationEventHandler`, `SubscriptionLifecycleIntegrationEventHandlers`, `RenewalCheckoutIssuer`. `QuoteOfflineSstTests` already covers mark-paid with SST-on billing. Any remaining tests that construct those types without billing and then touch an amount path will fail the same way. Not assigned here.

### 7.8 What 167 already covered (do not re-break)

- `SubscriptionBillingAmountTests.MerchantHasSst_Null_Billing_Throws`
- `SubscriptionBillingAmountTests.Gross_NoSst_Is100` (empty SST number, not null billing)
- Billing / dunning / arrears production resolution switched to `GetRequiredService<IBillingQueryService>()` in the same commit
- `DunningEngineJobTests` gained a billing substitute in that commit

---

## 8. Files that will change when we implement later

### 8.1 Must change (assigned six)

| File | Change |
|------|--------|
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CheckoutB2bIdentityTests.cs` | `using` Billing + ApiTypes; add `NoSstBilling`; six-arg `new` in `CreateHandler(Guid, ICommerceRepository, IMediator)` |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CreateCustomCheckoutAndInitiateSessionTests.cs` | same helper (or shared); six-arg `new` at the `SessionId_StampsB2b...` construction (line 119). Optional: line 165–166 `CompletedSession` constructor |

### 8.2 Should change in the same wave if the goal is “initiate ModuleTests green”

| File | Change |
|------|--------|
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs` | `CreateInitiateHandler` (line 1410) and the three inline `new`s (395, 740, 780) pass `NoSstBilling(orgId)` |

### 8.3 Do not change for this fix

| File | Why |
|------|-----|
| `apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs` | 167 is correct |
| `apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` | production path is already wired; optional 6th arg can stay |
| `apps/lazuar-api/src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs` | already calls `AddBillingModule` |
| `apps/lazuar-api/Modules/Billing/Infrastructure/DependencyInjection.cs` | already registers `IBillingQueryService` |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/QuoteOfflineSstTests.cs` | already correct (SST-on) |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SubscriptionBillingAmountTests.cs` | already the template |
| `issues/167-p1-b10-x11-getservice-sst-fail-open-undercharge.md` | status already resolved; optional later note that fixtures lagged |

### 8.4 Optional follow-ups (not required to un-red the six)

- Shared `NoSstBilling` helper under `tests/Lazuar.ModuleTests/Commerce/` or `Lazuar.TestSupport`.
- Make `IBillingQueryService` a required constructor argument on `InitiateCheckoutCommandHandler` (compile-time).
- Refresh the issue-167 markdown snippet so it quotes the throw, not `return false`.
- Same stub pass-through on any remaining `IBillingQueryService? = null` test constructors that reach an amount path.

### 8.5 Verification commands for the later PR

```
# assigned
dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj \
  --filter "FullyQualifiedName~CheckoutB2bIdentityTests|FullyQualifiedName~InitiateCheckout_SessionId_StampsB2bMetadataAndRequiresTin"

# 167 lock — must stay green
dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj \
  --filter "FullyQualifiedName~SubscriptionBillingAmountTests|FullyQualifiedName~QuoteOfflineSstTests"

# residual initiate (only if CreateInitiateHandler is updated)
dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj \
  --filter "FullyQualifiedName~CommerceProductCompletenessTests.InitiateCheckout"
```

---

## 9. Appendix — handle() flowchart for the assigned tests

```
Handle(request)
  tenant = IOneQueryService.GetTenantIdBySlugAsync          // stubbed → orgId
  email  = ICommunicationsQueryService.HasValidEmailConfig  // stubbed → true
  if SessionId.HasValue:                                    // custom-session tests
      load CheckoutSession
      if not OPEN / wrong tenant → throw completed          // CompletedSession still passes
      if TryReplayUrl → return old URL                      // 2nd act of CopiesIsB2b...
      MerchantHasSstAsync(_billingQueryService, tenant)     // LINE 121  ★ throws today
      CustomQuoteBreakdown(net, hasSst)
      stamp is_b2b_required from session flag
      if session.IsB2bRequired:
          require TaxId / CompanyName / IdType+IdValue      // MissingIdPair, SessionId missing TIN
          ResolveClientProfileCommand                       // PassesIdPairNamed, CopiesIsB2b CRM assert
      GenerateCheckoutSessionQuery
  else:                                                     // product tests
      load Product by slug
      EnforceCheckoutConfiguration                          // MissingTin still passes here
      ResolveClientProfileCommand                           // runs BEFORE SST on this path
      persist CheckoutSession
      MerchantHasSstAsync(_billingQueryService, tenant)     // LINE 339  ★ throws today
      GrossBreakdown(unitNet, qty, product SST fields)
      MergeClientIntoGateway(..., isB2bRequired: request.TaxId present)
      GenerateCheckoutSessionQuery
```

---

## 10. Appendix — exact exception text

Single message, both throw sites:

```
IBillingQueryService is required to decide SST; refusing to undercharge.
```

Expected messages the two FluentAssertions tests never get to see:

```
This payment request requires buyer ID type and ID value (BRN / NRIC / PASSPORT / ARMY).
This payment request requires a tax ID.
```

Product-path TIN message that still matches today (passing test):

```
This product requires a tax ID at checkout.
```

---

End of analysis. Implementation is a later task; do not revert 167; prefer no-SST `IBillingQueryService` stubs on the existing five-arg constructor call sites.
