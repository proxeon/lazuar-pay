# Cluster 02 — InitiateCheckout quantity / coupon paths and MarkPaid chosen-price fail after issue 167 SST fail-closed

## 1. Title, assigned tests, HEAD

**Title.** ModuleTests in `CommerceProductCompletenessTests` (InitiateCheckout quantity, coupon, trial, zero-amount, paid hop-2, custom session) plus `ChosenPriceDiscountTests.MarkPaid_YearlyTenPercentCoupon_BooksChosenRowNotCatalogPrice` all throw `InvalidOperationException: IBillingQueryService is required to decide SST; refusing to undercharge.` They do not fail the quantity math they were written to pin.

**HEAD at analysis time.**

- Branch: `fix/180-unify-outbox-inbox` (tracking `origin/fix/180-unify-outbox-inbox`)
- Commit: `4531f210f61b3d58d0332f1728b6a7889a1d2cad`
- Subject: `fix(api): register every module outbox and inbox through one helper`

Issue 167’s product change is already an ancestor of this HEAD (`49606466 fix(commerce): refuse to bill SST when billing is not composed`, 2026-08-18). Do **not** revert 167. The tests were never updated to compose `IBillingQueryService` after `MerchantHasSstAsync` stopped treating a missing billing dependency as “no SST.”

**Assigned tests (13 NUnit cases / 12 method names).**

File: `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs`

| # | Test | Constructor helper | Handler line that throws |
|---|------|--------------------|--------------------------|
| 1 | `InitiateCheckout_CustomSession_StillSendsLineSumAndQuantityOne` | `CreateInitiateHandler` (line 1238) | custom path `InitiateCheckoutCommandHandler.cs:121` |
| 2 | `InitiateCheckout_FixedOneTime_Qty3_PersistsSessionAndPaidOrderQuantity` | `CreateInitiateHandler` (line 1197) | product path `:339` |
| 3 | `InitiateCheckout_FixedOneTime_Qty3_SendsUnitNetAndQuantity` | `CreateInitiateHandler` (line 1071) | product path `:339` |
| 4 | `InitiateCheckout_FixedOneTime_Qty3_TenPercentCoupon_SendsUnitNetNinety` | `CreateInitiateHandler` (line 1107) | product path `:339` |
| 5 | `InitiateCheckout_FixedRecurring_NonOneQuantity_Persists("mo","FIXED",3)` | `CreateInitiateHandler` (line 1153) | product path `:339` |
| 6 | `InitiateCheckout_FixedRecurring_NonOneQuantity_Persists("yr","FIXED",2)` | `CreateInitiateHandler` (line 1153) | product path `:339` |
| 7 | `InitiateCheckout_HundredPercentCoupon_BillplzMonthly_StillBypasses` | `CreateInitiateHandler` (line 686) | product path `:339` |
| 8 | `InitiateCheckout_HundredPercentCoupon_Qty3_WritesZeroAmountOrderWithQuantity` | `CreateInitiateHandler` (line 1300) | product path `:339` |
| 9 | `InitiateCheckout_HundredPercentCoupon_StripeMonthly_MintsHop2SetupSession` | `CreateInitiateHandler` (line 545) | product path `:339` |
| 10 | `InitiateCheckout_PaidPath_KeepsSessionOpen_AndReturnsGatewayUrl` | **inline** `new InitiateCheckoutCommandHandler(...)` (line 780) | product path `:339` |
| 11 | `InitiateCheckout_TrialStripeMonthly_MintsHop2WithCommerceType` | `CreateInitiateHandler` (line 584) | product path `:339` |
| 12 | `InitiateCheckout_ZeroAmountCoupon_ReturnsSuccessUrlWithSessionId_AndCompletesSession` | **inline** (line 740) | product path `:339` |

File: `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/ChosenPriceDiscountTests.cs`

| # | Test | Constructor | Handler line that throws |
|---|------|-------------|--------------------------|
| 13 | `MarkPaid_YearlyTenPercentCoupon_BooksChosenRowNotCatalogPrice` | `new MarkCheckoutAsPaidOfflineCommandHandler(repository, eventBus, crm)` (line 73) — **no billing arg** | `MarkCheckoutAsPaidOfflineCommandHandler.cs:97` (`HandleProductSessionAsync`) |

**Verified by running the filter on HEAD.** 13 failed, 0 passed. Every error message is exactly:

```
System.InvalidOperationException : IBillingQueryService is required to decide SST; refusing to undercharge.
```

Thrown from `SubscriptionBillingAmount.MerchantHasSstAsync` at `apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs:97`.

There is **one root cause** for all assigned InitiateCheckout tests, and **the same root cause** (null optional `IBillingQueryService`) for MarkPaid, on a different handler. MarkPaid is not a second product bug. Quantity, coupon, chosen yearly price, hop-2 Amount/Quantity, and zero-amount bypass logic are **not reached**.

---

## 2. How these tests construct `InitiateCheckoutCommandHandler` / MarkPaid handlers (billing optional?)

### 2.1 Production constructors still treat billing as optional

`InitiateCheckoutCommandHandler` (`apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs`):

```22:45:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
public class InitiateCheckoutCommandHandler : ICommandHandler<InitiateCheckoutCommand, CheckoutResultDto>
{
    private readonly IOneQueryService _oneQueryService;
    private readonly ICommerceRepository _repository;
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;
    private readonly ICommunicationsQueryService _communicationsQueryService;
    private readonly IBillingQueryService? _billingQueryService;

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

`MarkCheckoutAsPaidOfflineCommandHandler` (`apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs`):

```16:33:apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs
public class MarkCheckoutAsPaidOfflineCommandHandler : ICommandHandler<MarkCheckoutAsPaidOfflineCommand>
{
    private readonly ICommerceRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IBillingQueryService? _billingQueryService;

    public MarkCheckoutAsPaidOfflineCommandHandler(
        ICommerceRepository repository,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus,
        ICrmQueryService crmQueryService,
        IBillingQueryService? billingQueryService = null)
    {
        _repository = repository;
        _eventBus = eventBus;
        _crmQueryService = crmQueryService;
        _billingQueryService = billingQueryService;
    }
```

Both still have `IBillingQueryService? … = null`. That is issue **181** (open, P2): optional constructor parameter. Issue **167** (resolved) changed the *consumer* `MerchantHasSstAsync` so a null service is no longer “merchant has no SST.” Production MediatR / `AddBillingModule` still injects a real `IBillingQueryService` (`Billing/Infrastructure/DependencyInjection.cs:51`). ModuleTests that `new` the handler never go through that DI.

Issue 181’s audit text is now stale on one point: it still quotes `MerchantHasSstAsync` as `return false` when billing is null. After 167 that branch throws. The constructor is still optional; the null path is now fail-closed.

### 2.2 Shared helper `CreateInitiateHandler` omits billing

```1392:1411:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs
    private static InitiateCheckoutCommandHandler CreateInitiateHandler(
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

Five-argument call → sixth parameter defaults to `null` → `_billingQueryService` is null for every test that uses this helper.

Used by assigned tests at lines **545, 584, 686, 1071, 1107, 1153, 1197, 1238, 1300**. Also used by two tests that **do not fail** because they throw earlier (`InitiateCheckout_FixedOneTime_OutOfRangeQuantity_ThrowsBeforePersist` at 1127, `InitiateCheckout_Pwyw_NonOneQuantity_ThrowsAndDoesNotPersist` at 1172). Updating the helper is still the right fix; those early-throw tests never call `MerchantHasSstAsync`.

### 2.3 Two assigned tests construct the handler inline, still without billing

`InitiateCheckout_ZeroAmountCoupon_ReturnsSuccessUrlWithSessionId_AndCompletesSession` (717–740) and `InitiateCheckout_PaidPath_KeepsSessionOpen_AndReturnsGatewayUrl` (762–780) duplicate `CreateInitiateHandler` by hand:

```717:740:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs
        var one = Substitute.For<IOneQueryService>();
        one.GetTenantIdBySlugAsync("acme").Returns(orgId);
        // ...
        var handler = new InitiateCheckoutCommandHandler(one, repository, mediator, config, comms);
```

Same five-arg call. Same null billing.

A third inline site exists at line **395** (`InitiateCheckout_EnforcesRequiresPhone`). That test still **passes**: `EnforceCheckoutConfiguration` throws `*phone*` at handler line 482, before the SST lookup at 339. Not assigned. Still a constructor site to update if the helper is the only source of truth.

### 2.4 Common stubs those helpers *do* provide

Every assigned InitiateCheckout test (helper or inline) sets:

| Dependency | Stub |
|------------|------|
| `IOneQueryService.GetTenantIdBySlugAsync("acme")` | the test’s `orgId` |
| `ICommunicationsQueryService.HasValidEmailConfigAsync(orgId)` | `true` |
| `IConfiguration["App:ClientUrl"]` | `"https://portal.test"` (inline zero/paid path and helper) |
| `IMediator` | `ResolveClientProfileCommand` → `clientId`; hop-2 / zero-amount commands as needed |
| `ICommerceRepository` | product by slug, coupon lock, `AddCheckoutSession`, and (for zero-amount) session/product/coupon by id |

None of them stub `IBillingQueryService`. None of them call `product.SetSst(...)`. `CreateProduct` (lines 56–76) builds a `Product` with catalog price `100m`, currency `MYR`, default interval `"mo"`, default gateway `"STRIPE"`, pricing model `"FIXED"` unless overridden. Product constructor sets `SstTaxType = "06"` and `SstRatePercent = 0m` (`Product.cs:75–76`).

### 2.5 MarkPaid in `ChosenPriceDiscountTests`

```73:74:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/ChosenPriceDiscountTests.cs
        var handler = new MarkCheckoutAsPaidOfflineCommandHandler(repository, Substitute.For<IEventBus>(), crm);
        await handler.Handle(new MarkCheckoutAsPaidOfflineCommand(orgId, session.Id), CancellationToken.None);
```

Three-arg call → billing defaults to null. Repository returns a dual-price product (catalog 100 monthly + yearly 1000), a reserved 10% coupon, and a session whose `PriceId` is the yearly row. CRM returns a buyer profile. No billing stub.

Sibling test `ProcessZeroAmount_YearlyHundredPercentCoupon_DoesNotThrow` (same file, lines 24–44) **passes**. `ProcessZeroAmountCheckoutCommandHandler` has no `IBillingQueryService` field and never calls `MerchantHasSstAsync` (`ProcessZeroAmountCheckoutCommand.cs:18–29`).

### 2.6 Contrast: tests that already compose billing

`QuoteOfflineSstTests` is the only Initiate/MarkPaid fixture that already passes a billing stub:

```55:56:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/QuoteOfflineSstTests.cs
        var handler = new InitiateCheckoutCommandHandler(
            one, repository, mediator, config, comms, SstBilling(orgId));
```

```99:100:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/QuoteOfflineSstTests.cs
        var handler = new MarkCheckoutAsPaidOfflineCommandHandler(
            repository, Substitute.For<IEventBus>(), crm, SstBilling(orgId));
```

`SstBilling` returns a profile with `Sst_registration_number = "W10-1234-12345678"`. Those tests **assert gross** (5400 custom, 108 product, 270 custom mark-paid). They must **not** be switched to `NoSstBilling()`.

`BillingEngineJobTests.SetUp` (the requested template for this cluster) stubs empty SST:

```67:68:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs
        _billing = Substitute.For<IBillingQueryService>();
        _billing.GetBillingProfileAsync(Arg.Any<Guid>()).Returns((TenantBillingProfileDto?)null);
```

`SubscriptionBillingAmountTests.Gross_NoSst_Is100` uses the same interface with `Sst_registration_number = ""`. Both make `MerchantHasSstAsync` return `false` without throwing.

`SubscriptionBillingAmountTests.MerchantHasSst_Null_Billing_Throws` (lines 121–125) is the unit pin for issue 167 itself: `MerchantHasSstAsync(null, _)` must throw `*refusing to undercharge*`. That test must keep passing. Fixture stubs must be **non-null** services, not a return to passing `null`.

---

## 3. Every `MerchantHasSstAsync` / `GrossBreakdown` call on the happy path of these tests

### 3.1 The fail-closed gate (issue 167)

```93:111:apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs
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

    public static async Task<Breakdown> GrossBreakdown(
        Subscription sub,
        Product product,
        IBillingQueryService? billing)
    {
        var merchantHasSst = await MerchantHasSstAsync(billing, sub.OrganizationId);
        return GrossBreakdown(sub, product, merchantHasSst);
    }
```

Commit `49606466` (`fix(commerce): refuse to bill SST when billing is not composed`) replaced `return false` in the null branch with the throw. `git blame` on lines 97–98 is that commit (2026-08-18). The method signature, profile lookup, and “non-blank SST number ⇒ true” rule are unchanged from `eba07414`.

Issue file: `issues/167-p1-b10-x11-getservice-sst-fail-open-undercharge.md` (status: resolved on `fix/167-sst-fail-closed`). The audit still shows the old `return false` snippet as the *bug*, not the current code.

`IBillingQueryService.GetBillingProfileAsync` (`Modules/Billing/Contracts/IBillingQueryService.cs:18`) returns `TenantBillingProfileDto?`. The SST flag is only `Sst_registration_number` (`Lazuar.ApiContracts.cs:627–628`). A null profile or blank/whitespace number is “merchant has no SST.”

### 3.2 Product InitiateCheckout path (assigned tests 2–12)

Happy-path order inside `Handle` after tenant + email-config succeed:

1. No `SessionId` → skip custom branch.
2. Load product by slug (`GetProductBySlugAsync`). All assigned product tests stub this.
3. `CommerceCheckoutQuantity.NormalizeOrThrow` (`CommerceCheckoutQuantity.cs:18–35`). Qty 3 / 2 on FIXED one_time / mo / yr succeed. Assigned tests that intend to persist never throw here.
4. `ResolveCheckoutPrice` — no `PriceId` / `Interval` on `GuestCheckoutCommand`, so `(product.Price, product.Interval, product.DefaultPrice()?.Id)` = catalog 100 and the product interval (`InitiateCheckoutCommandHandler.cs:469`).
5. Trial check + `EnforceCheckoutConfiguration` — assigned products have `requiresPhone/Address/TaxId = false`.
6. `ResolveClientProfileCommand` via mediator (stubbed to `clientId`).
7. Persist session (and reserve coupon if any) inside `PersistReservationAndSessionAsync` (257–293). **Session is created before SST.** Coupon discount is computed here as `coupon.CalculateDiscount(resolved.Amount)` — **per unit**, not line.
8. **Then** SST:

```338:351:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
        var unitNet = isTrial ? 0m : Math.Max(0, resolved.Amount - unitDiscount);
        var merchantHasSst = await SubscriptionBillingAmount.MerchantHasSstAsync(
            _billingQueryService, tenantId.Value);
        var breakdown = SubscriptionBillingAmount.GrossBreakdown(
            unitNet, quantity, product.SstTaxType, product.SstRatePercent, merchantHasSst);
        var sstType = breakdown.TaxType;
        var unitTax = breakdown.UnitTax;
        var unitGross = breakdown.UnitGross;
        var lineNet = breakdown.Gross;
        var isB2bRequired = !string.IsNullOrWhiteSpace(request.TaxId);

        // Same poller handle as the paid hop-2 return — buyer success must observe session COMPLETED.
        var successUrl = $"{clientUrl}/{request.TenantSlug}/checkout/{request.ProductSlug}/success?sub_id={session.Id}";
```

This is **the** `MerchantHasSstAsync` on the product happy path. It is the stack frame for every assigned product test (handler line 339).

`GrossBreakdown(unitNet, quantity, sstTaxType, sstRatePercent, merchantHasSst)` is the sync overload (`SubscriptionBillingAmount.cs:44–55`). It does **not** call billing again. It calls `SstTaxMath.Compute`.

`SstTaxMath.Compute` (`SstTaxMath.cs:8–24`) returns `("06", 0m)` unless **all** of: merchant registered, requested type `"02"`, rate > 0, net > 0. Assigned products are type `"06"` rate `0`. After a `NoSstBilling()` stub, `merchantHasSst` is also false. Either way tax is 0 and `unitGross == unitNet`, `Gross == unitNet * quantity`.

The local name `lineNet` is actually **gross including tax** (audit note in `plans/009-bugs/01-commerce-checkout-activation.md`). For these fixtures it equals unit net × quantity.

**Zero / vault / paid fork uses `lineNet`, not a second SST call:**

- `lineNet == 0` and `PaymentGatewayCapabilities.SupportsOffSession(product.GatewayName)` and interval `mo`/`yr` → mint hop-2 `GenerateCheckoutSessionQuery` with `Amount: 0m`, `SetupFutureUsage: true`, `Quantity: quantity` (lines 352–381). Stripe and CHIP only (`PaymentGatewayCapabilities.cs:10–14`).
- `lineNet == 0` otherwise → `ProcessZeroAmountCheckoutCommand` (384–389). Billplz monthly 100% and one-time 100% take this fork. `ProcessZeroAmountCheckoutCommandHandler` does **not** call `MerchantHasSstAsync` or `GrossBreakdown`. It re-discounts chosen/catalog unit × quantity (`ProcessZeroAmountCheckoutCommand.cs:42–66`) and writes an order with `AmountPaid: 0m` for one-time (75–85) or a reminder-only subscription for recurring.
- `lineNet > 0` → hop-2 with `Amount: unitGross` (unit, not pre-multiplied) and `Quantity: quantity` (413–426). Comment at 413: “Amount is unit price (net + SST); adapters multiply by Quantity. Do not pre-multiply.” Matches `GenerateCheckoutSessionQuery` XML (`GenerateCheckoutSessionQuery.cs:7–11`).

**No second SST call** on product initiate after line 339. `StampSstMetadata` is **not** used on the product path; product hop-2 writes `sst_*` keys only when `unitTax > 0` (401–406). Assigned products never write those keys once billing is stubbed empty.

### 3.3 Custom-session InitiateCheckout path (assigned test 1)

When `request.SessionId` is set (`InitiateCheckout_CustomSession_StillSendsLineSumAndQuantityOne`):

```105:204:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
        if (request.SessionId.HasValue)
        {
            var existingSession = await _repository.GetCheckoutSessionByIdAsync(...);
            // OPEN + org check (109–111)
            // idempotency replay (113–116)
            existingSession.SetIdempotency(...);

            var customNet = existingSession.AdHocLineItems.Sum(x => x.UnitPrice * x.Quantity);
            var customMerchantHasSst = await SubscriptionBillingAmount.MerchantHasSstAsync(
                _billingQueryService, tenantId.Value);
            var customBreakdown = SubscriptionBillingAmount.CustomQuoteBreakdown(
                customNet, customMerchantHasSst);
            var customTotalAmount = customBreakdown.Gross;
            // ... metadata, optional B2B CRM resolve ...
            var customGatewayQuery = new GenerateCheckoutSessionQuery(
                tenantId.Value,
                customTotalAmount,
                "MYR",
                "Custom Payment Request",
                ...,
                false,
                1,   // Quantity hard-coded to 1
                existingSession.GatewayName
            );
```

This is **the** `MerchantHasSstAsync` on the custom happy path (handler line 121). Stack for `InitiateCheckout_CustomSession_StillSendsLineSumAndQuantityOne` stops here. `request.Quantity` (the test sends 3) is **ignored**. Line qty lives on `AdHocLineItem`. Gateway `Quantity` is always 1; `Amount` is the **already multiplied** custom line sum (plus SST if registered).

`CustomQuoteBreakdown` (`SubscriptionBillingAmount.cs:41–42`):

```csharp
public static Breakdown CustomQuoteBreakdown(decimal net, bool merchantHasSst) =>
    GrossBreakdown(net, 1, SstTaxMath.ServiceTax, DefaultServiceTaxRatePercent, merchantHasSst);
```

Unlike product checkout, custom quotes **force tax type `"02"` and 8%** whenever `merchantHasSst` is true. A stub that returns an SST registration number would change this test’s expected `Amount` from `500` to `540`. That is why this cluster **must** use empty SST, not `QuoteOfflineSstTests.SstBilling`.

`StampSstMetadata` (line 137) is the only other SST helper on this path. With `merchantHasSst == false`, `UnitTax` is 0, so it no-ops (`SubscriptionBillingAmount.cs:81–84`). No second billing call.

**Ordering trap (matters for cluster 01, not this test’s assertions):** SST is decided at 121 **before** B2B field checks at 144–159 (`TaxId`, `CompanyName`, `IdType`/`IdValue`). This assigned custom test sets `isB2bRequired: false`, so it would proceed to hop-2 after a NoSst stub. Cluster 01 tests that expected `*tax ID*` / `*ID type*` currently get the SST exception instead (see §7).

### 3.4 MarkPaid product path (assigned test 13)

`Handle` loads the session, `TryComplete()`, loads CRM, then `HandleProductSessionAsync` because `session.ProductId` is set.

```82:101:apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs
        var quantity = Math.Max(1, session.Quantity);
        var chosen = product.Prices.FirstOrDefault(p => p.Id == session.PriceId);
        var unitAmount = chosen?.Amount ?? product.Price;
        var unitDiscount = 0m;
        if (session.CouponId.HasValue)
        {
            var coupon = await _repository.GetCouponByIdAsync(...);
            if (coupon != null)
            {
                unitDiscount = coupon.CalculateDiscount(unitAmount);
                coupon.ConfirmReservation();
            }
        }

        var unitNet = Math.Max(0, unitAmount - unitDiscount);
        var merchantHasSst = await SubscriptionBillingAmount.MerchantHasSstAsync(
            _billingQueryService, session.OrganizationId);
        var breakdown = SubscriptionBillingAmount.GrossBreakdown(
            unitNet, quantity, product.SstTaxType, product.SstRatePercent, merchantHasSst);
        var totalAmount = breakdown.Gross;
```

This is **the** `MerchantHasSstAsync` on the assigned MarkPaid happy path (line 97). Same throw, different handler.

After a NoSst stub + default product type `06`:

- `DualPriceProduct`: catalog 100 `mo`, `UpsertPrice("yr", 1000m)`.
- Session `PriceId = yearly.Id`, quantity 1.
- `unitAmount = 1000` (chosen row, not catalog 100).
- 10% coupon → `CalculateDiscount(1000) = 100` → `unitNet = 900`.
- `GrossBreakdown(900, 1, "06", 0, false).Gross = 900`.
- Recurring interval → subscription + `CommerceTransactionLog` with `totalAmount` 900. Test asserts `log.Amount == 900`.

No second SST call. Custom MarkPaid (`HandleCustomSessionAsync` lines 195–197) also calls `MerchantHasSstAsync` + `CustomQuoteBreakdown`; that is **not** on this assigned test’s path (product session). It *is* on collateral `MarkCheckoutAsPaidOffline_CustomSession_CompletesWithoutSubscription`.

### 3.5 What is **not** on these happy paths

| Call site | Why assigned tests never reach it today / after fixture fix |
|-----------|--------------------------------------------------------------|
| `ProcessZeroAmountCheckoutCommandHandler` | No billing. Reached only *after* initiate SST (tests 7, 8, 12). |
| `GatewayPaymentCompletedIntegrationEventHandler` | No billing. Reached only *after* initiate SST (test 2’s second half). Order uses `@event.AmountPaid` and `session.Quantity` (`OpenCheckout.cs:123–129`). |
| `SubscriptionBillingAmount.GrossBreakdown(sub, product, billing)` async overload | Initiate/MarkPaid call the sync `(unitNet, seats, type, rate, bool)` overload after a bool from `MerchantHasSstAsync`. |
| `CommerceQueryService.CustomCheckouts` `MerchantHasSstAsync` (lines 59, 115) | Query service, not these command tests. |
| `BillingEngineJob` / dunning / arrears / lifecycle handlers | Different fixtures. Job tests already stub billing. |

### 3.6 Coupon math used after SST would succeed

`Coupon.CalculateDiscount` (`Coupon.cs:66–74`): percentage is `originalPrice * (Amount / 100)`, capped at original; fixed is `min(Amount, originalPrice)`. Always **per unit**. Line discount is `unitDiscount * quantity` only inside `ProcessZeroAmountCheckoutCommandHandler` (59–62) and `ValidateCouponQueryHandler` (39–41). Initiate sends **unit** net/gross to Payments.

---

## 4. Per-test failure (same root cause or not — MarkPaid checked separately)

Ran on HEAD:

```
dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj \
  --filter "FullyQualifiedName~...assigned names..."
Failed!  Failed: 13, Passed: 0, Total: 13
```

Every case: same exception, `SubscriptionBillingAmount.cs:97`. Differ only by caller line.

### 4.1 Product InitiateCheckout (tests 2–12) — one root cause

Stack:

1. test `Handle(...)`
2. `InitiateCheckoutCommandHandler.Handle` line **339**
3. `MerchantHasSstAsync` line **97**

Session persist (step 7 in §3.2) **does** run first. For NSubstitute repositories that only capture `AddCheckoutSession`, a session object exists in the test local, but the test dies before hop-2 / zero-amount / assertions. For the in-memory persist test (test 2), a row may already be inserted when the throw happens; the test never reaches `CreateOpenCheckoutPaymentHandler`.

What each test was trying to pin, and the numbers that would hold with `NoSstBilling()` + default type `06`:

#### `InitiateCheckout_FixedOneTime_Qty3_SendsUnitNetAndQuantity` (1051–1085)

- Product: `one_time`, 100, STRIPE, no coupon, qty 3.
- After SST: `unitNet=100`, `unitGross=100`, `lineNet=300`, paid hop-2.
- Assert: `session.Quantity == 3`, `payments.Amount == 100`, `payments.Quantity == 3`, product `100*3=300`, **not** 300 as Amount, **not** 900 (the squared-charge bug the comment cites: adapters already multiply; see `GenerateCheckoutSessionQuery` and `GatewayCommonTests` qty=2).

#### `InitiateCheckout_FixedOneTime_Qty3_TenPercentCoupon_SendsUnitNetNinety` (1087–1115)

- Same + coupon `SAVE10` PERCENTAGE 10.
- `CalculateDiscount(100)=10` → `unitNet=90` → hop-2 `Amount==90 && Quantity==3`.

#### `InitiateCheckout_FixedRecurring_NonOneQuantity_Persists("mo","FIXED",3)` and `("yr","FIXED",2)` (1138–1158)

- After SST: paid hop-2 (100 × qty).
- Assert: `AddCheckoutSession` received `s.Quantity == quantity`. Does not inspect hop-2 Amount.

#### `InitiateCheckout_FixedOneTime_Qty3_PersistsSessionAndPaidOrderQuantity` (1183–1217)

- Uses real `CommerceRepository(db)` + in-memory `CommerceDbContext`.
- Fails at initiate 339; **never** calls `CreateOpenCheckoutPaymentHandler`.
- After fixture fix: session persisted qty 3 OPEN; then `CreateCommercePaymentCompleted(..., amountPaid: 300m)`; `GatewayPaymentCompleted` writes `Order(AmountPaid=300, Quantity=session.Quantity)` from the **event**, not from SST. No billing needed on that handler. Assertions: no subscription, one COMPLETED order qty 3 amount 300.

#### `InitiateCheckout_HundredPercentCoupon_StripeMonthly_MintsHop2SetupSession` (524–562)

- Default product is monthly STRIPE, coupon `FREE100` 100%.
- `unitDiscount=100`, `unitNet=0`, `lineNet=0`.
- `SupportsOffSession("STRIPE")==true` and interval `mo` → vault hop-2 Amount 0, `SetupFutureUsage=true`, metadata `type=commerce_subscription`.
- Must **not** send `ProcessZeroAmountCheckoutCommand`.
- SST is still required to *decide* that 0 net + 0 tax is 0. The throw happens before the vault fork.

#### `InitiateCheckout_TrialStripeMonthly_MintsHop2WithCommerceType` (564–601)

- `product.SetTrialDays(14)` → `isTrial=true` → `unitNet` forced 0 even without coupon.
- Same vault hop-2. Metadata must be `commerce_subscription`, not `"trial"`.
- Same SST throw at 339 before the fork.

#### `InitiateCheckout_HundredPercentCoupon_BillplzMonthly_StillBypasses` (657–696)

- Gateway `BILLPLZ`. `SupportsOffSession` is false.
- After SST: `lineNet=0` → `ProcessZeroAmountCheckoutCommand` (wired to a real `ProcessZeroAmountCheckoutCommandHandler`). Completes session, success URL `https://portal.test/acme/checkout/pro-plan/success?sub_id={session.Id}`, `IsZeroAmountBypass=true`. Must not mint hop-2.

#### `InitiateCheckout_ZeroAmountCoupon_ReturnsSuccessUrlWithSessionId_AndCompletesSession` (698–747)

- `one_time` + 100% coupon. Interval is not `mo`/`yr` → not vault even on STRIPE.
- Same zero-amount bypass. Inline handler (no `CreateInitiateHandler`). Same throw at 339.

#### `InitiateCheckout_HundredPercentCoupon_Qty3_WritesZeroAmountOrderWithQuantity` (1269–1313)

- `one_time` + 100% + qty 3.
- After SST: `unitNet=0`, `lineNet=0` → ProcessZeroAmount.
- Handler writes `Order(..., 0m, ..., quantity=3)` and `ZeroAmountCheckoutCompletedIntegrationEvent` with `OriginalAmount == 300` and `DiscountAmount == 300` (`lineGross = unitAmount * quantity`, `lineDiscount = unitDiscount * quantity`).

#### `InitiateCheckout_PaidPath_KeepsSessionOpen_AndReturnsGatewayUrl` (749–787)

- Monthly STRIPE, no coupon. After SST: paid hop-2 URL `https://gateway.test/pay/xyz`, session stays OPEN, `IsZeroAmountBypass=false`.
- Inline handler. Same throw at 339.

### 4.2 Custom InitiateCheckout (test 1) — same root cause, different call site

Stack:

1. test line **1259** `Handle(command)`
2. `InitiateCheckoutCommandHandler.Handle` line **121** (custom branch)
3. `MerchantHasSstAsync` line **97**

Setup: existing OPEN custom session, one ad-hoc line `Consulting` qty **2** unit **250**, command `Quantity: 3` + `SessionId: session.Id`. Does not load a product.

After `NoSstBilling()`:

- `customNet = 2 * 250 = 500`
- `CustomQuoteBreakdown(500, false).Gross = 500`
- hop-2 `Amount == 500 && Quantity == 1`
- `GetProductBySlugAsync` must not be called

If someone mistakenly passed `SstBilling` (registered SST number), hop-2 Amount would be **540** and this test would fail for a *different* reason. Empty SST is mandatory here.

### 4.3 MarkPaid yearly 10% (test 13) — same root cause, different handler

Stack:

1. `ChosenPriceDiscountTests.cs:74` `Handle(...)`
2. `MarkCheckoutAsPaidOfflineCommandHandler.Handle` line **56** (`HandleProductSessionAsync`)
3. `HandleProductSessionAsync` line **97**
4. `MerchantHasSstAsync` line **97**

Checked separately as requested:

- **Not** a chosen-price regression. Chosen-row lookup (`product.Prices.FirstOrDefault(p => p.Id == session.PriceId)`) is **above** the throw (lines 83–84). We never get to book 900 vs 90 (10% of catalog 100) vs 1000.
- **Not** a coupon-on-wrong-price bug (that was issue 029 / B01-C03; this test is the pin that yearly 10% books 900).
- **Not** SST on a type-02 product. `DualPriceProduct` never calls `SetSst`; type stays `"06"`.
- Same null `_billingQueryService` as InitiateCheckout.

Sibling `ProcessZeroAmount_YearlyHundredPercentCoupon_DoesNotThrow` still passes (no billing dependency).

### 4.4 Verdict

| Surface | Root cause | Same as others? |
|---------|------------|-----------------|
| Product InitiateCheckout (11 cases) | `CreateInitiateHandler` / inline ctor omit billing → `_billingQueryService == null` → `MerchantHasSstAsync` throws at handler 339 | Yes |
| Custom InitiateCheckout (1 case) | Same helper, custom branch line 121 | Yes, earlier in `Handle` |
| MarkPaid chosen yearly (1 case) | MarkPaid ctor omits billing → throw at handler 97 | Same *dependency* hole, different type |

No assigned test fails quantity math, coupon math, hop-2 unit-vs-line, vault vs bypass, or chosen-price selection. Those code paths are dead until billing is stubbed.

### 4.5 Collateral failures in the same files (not assigned, same hole)

A broader filter (`CheckoutB2bIdentityTests | CreateCustomCheckoutAndInitiateSessionTests | CommerceProductCompletenessTests.InitiateCheckout | CommerceProductCompletenessTests.MarkCheckout | ChosenPriceDiscountTests | QuoteOfflineSstTests`) was **23 failed / 16 passed / 39 total**. Extra failures in *this cluster’s files*:

| Test | File | Why |
|------|------|-----|
| `MarkCheckoutAsPaidOffline_ProductSession_CreatesActiveSubscription_AndTxLog` | Completeness:295 | MarkPaid ctor, product path line 97 |
| `MarkCheckoutAsPaidOffline_ProductRequiresTaxId_PublishesB2b` | Completeness:331 | same |
| `MarkCheckoutAsPaidOffline_CustomSession_CompletesWithoutSubscription` | Completeness:367 | MarkPaid custom path line 195 |
| `MarkCheckoutAsPaidOffline_OneTime_Qty3_WritesLineTotalOrder` | Completeness:1340 | MarkPaid product path line 97 |

Passing completeness Initiate tests (throw **before** SST):

| Test | Why it still passes |
|------|---------------------|
| `InitiateCheckout_EnforcesRequiresPhone` | `EnforceCheckoutConfiguration` at 482 |
| `InitiateCheckout_FixedOneTime_OutOfRangeQuantity_ThrowsBeforePersist` (0, −1, 100) | `NormalizeOrThrow` at 213 |
| `InitiateCheckout_Pwyw_NonOneQuantity_ThrowsAndDoesNotPersist` (2 cases) | `NormalizeOrThrow` “fixed-price” at 213 |

`QuoteOfflineSstTests` (4) all pass — they already inject `SstBilling`.

---

## 5. Recommended fixture fix: stub `IBillingQueryService` with empty SST, same as `BillingEngineJobTests` SetUp. Do **not** revert 167.

### 5.1 What 167 did and why it must stay

Before 167, `MerchantHasSstAsync(null, _)` returned `false`. Tests that omitted billing silently charged net. That was the fail-**open** undercharge (B10-X11). Production host registers billing, so hop-1 in the API process was fine; test hosts and any future Commerce-without-Billing composition were not.

After 167:

- Null service → throw (refuse to bill).
- `BillingEngineJob` / dunning / arrears were moved to `GetRequiredService<IBillingQueryService>()` (commit message).
- `SubscriptionBillingAmountTests.MerchantHasSst_Null_Billing_Throws` pins the throw.

Reverting 167 would make these 13 tests green and re-open undercharge when billing is missing. Out of scope and forbidden.

Making `IBillingQueryService` a **required** constructor parameter (issue 181’s product direction) is a product change, not this fixture fix. Optional `= null` can stay; tests must stop relying on it.

### 5.2 What the stub must return

`MerchantHasSstAsync` after a non-null service:

```csharp
var profile = await billing.GetBillingProfileAsync(organizationId);
return !string.IsNullOrWhiteSpace(profile?.Sst_registration_number);
```

Empty SST (merchant not registered) is:

```csharp
var billing = Substitute.For<IBillingQueryService>();
billing.GetBillingProfileAsync(Arg.Any<Guid>()).Returns((TenantBillingProfileDto?)null);
```

That is **exactly** `BillingEngineJobTests.SetUp` lines 67–68. Equivalent: return a DTO with `Sst_registration_number = ""` (`SubscriptionBillingAmountTests.Gross_NoSst_Is100`).

Do **not** copy `QuoteOfflineSstTests.SstBilling` (non-blank `W10-1234-12345678`) into this cluster:

- Custom assigned test would expect 500 but hop-2 would send **540** (`CustomQuoteBreakdown` always applies 8% when registered).
- Product assigned tests would still pass *amounts* only because `CreateProduct` / `DualPriceProduct` leave type `"06"` (tax 0 even if registered). That is accidental. Empty SST matches the tests’ intent (net = gross) without depending on product SST defaults.

Do **not** pass `billing: null`. That is the current failure and the 167 pin.

### 5.3 Why empty SST preserves every assigned assertion

| Test | Expected money / qty | With `NoSstBilling()` + type 06 |
|------|----------------------|----------------------------------|
| Qty3 send unit | Amount 100, Qty 3 | unitGross 100 |
| Qty3 + 10% | Amount 90, Qty 3 | unitNet 90 |
| Recurring persist | session.Quantity 3 or 2 | persist happens before SST; hop-2 still created after |
| Persist + paid | order 300 × qty 3 | initiate 100×3; webhook uses event 300 |
| Stripe 100% monthly | Amount 0 setup | lineNet 0, vault |
| Trial Stripe monthly | Amount 0 setup | unitNet forced 0 |
| Billplz 100% monthly | zero bypass, COMPLETED | lineNet 0, not vault |
| One-time 100% | success URL + COMPLETED | lineNet 0, not vault |
| Qty3 100% | order 0 × qty 3, event 300/300 | ProcessZeroAmount line math |
| Paid path | gateway URL, OPEN | unitGross 100 |
| Custom 2×250, cmd qty 3 | Amount 500, Qty 1 | CustomQuoteBreakdown(500, false) |
| MarkPaid yearly 10% | log.Amount 900 | 1000 − 100, tax 0 |

### 5.4 What not to change in product code for this cluster

- Do not move `MerchantHasSstAsync` after B2B validation (that would be a product change that also unblocks cluster 01’s message assertions another way; fixture stub is enough).
- Do not hard-code `merchantHasSst: false` in the handler to “help tests.”
- Do not make `_billingQueryService` skip the call when null.
- Do not add SST to `CreateProduct` unless writing a new SST test.

---

## 6. Concrete helper to add (`NoSstBilling()`) and every constructor site to update

### 6.1 Helper

Add a small shared stub (NSubstitute lives in ModuleTests, not `Lazuar.TestSupport` — TestSupport has no NSubstitute package). Suggested file:

`apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceBillingStubs.cs`

```csharp
using Lazuar.ApiTypes;
using Modules.Billing.Contracts;
using NSubstitute;

namespace Lazuar.ModuleTests.Commerce;

internal static class CommerceBillingStubs
{
    /// <summary>
    /// Composed billing, merchant not SST-registered.
    /// Same contract as BillingEngineJobTests.SetUp.
    /// </summary>
    public static IBillingQueryService NoSstBilling()
    {
        var billing = Substitute.For<IBillingQueryService>();
        billing.GetBillingProfileAsync(Arg.Any<Guid>()).Returns((TenantBillingProfileDto?)null);
        return billing;
    }
}
```

Optional second method `SstBilling(Guid orgId, string number = "W10-1234-12345678")` can later replace the private copies in `QuoteOfflineSstTests` and `SubscriptionBillingAmountTests`. **Do not** use that method for this cluster.

### 6.2 Constructor sites that assigned tests actually execute

Must change for the 13 assigned cases to go green:

1. **`CommerceProductCompletenessTests.CreateInitiateHandler` line 1410**

   ```csharp
   return new InitiateCheckoutCommandHandler(
       one, repository, mediator, config, comms, CommerceBillingStubs.NoSstBilling());
   ```

   Fixes assigned tests at 545, 584, 686, 1071, 1107, 1153, 1197, 1238, 1300 in one edit.

2. **`CommerceProductCompletenessTests` inline Initiate at line 740** (`InitiateCheckout_ZeroAmountCoupon_...`)

   ```csharp
   var handler = new InitiateCheckoutCommandHandler(
       one, repository, mediator, config, comms, CommerceBillingStubs.NoSstBilling());
   ```

   Better: delete the inline duplicate and call `CreateInitiateHandler(orgId, repository, mediator)` so there is one site.

3. **`CommerceProductCompletenessTests` inline Initiate at line 780** (`InitiateCheckout_PaidPath_...`)

   Same as (2). Prefer `CreateInitiateHandler`.

4. **`ChosenPriceDiscountTests` MarkPaid at line 73**

   ```csharp
   var handler = new MarkCheckoutAsPaidOfflineCommandHandler(
       repository, Substitute.For<IEventBus>(), crm, CommerceBillingStubs.NoSstBilling());
   ```

### 6.3 Same-file sites that will still fail if left alone

Not assigned, but the next test run of `CommerceProductCompletenessTests` will stay red without them:

| Line | Call | Path |
|------|------|------|
| 294 | `new MarkCheckoutAsPaidOfflineCommandHandler(repository, eventBus, crm)` | product SST 97 |
| 330 | same | product SST 97 |
| 366 | same | custom SST 195 |
| 1339 | same | product SST 97 (`MarkCheckoutAsPaidOffline_OneTime_Qty3_WritesLineTotalOrder`) |
| 395 | inline Initiate (phone) | does not reach SST; still pass billing for consistency |

Recommend a `CreateMarkPaidHandler(repository, eventBus, crm)` next to `CreateInitiateHandler` that always passes `NoSstBilling()`.

### 6.4 Sites that must **keep** SST-registered stubs

| File | Line | Why |
|------|------|-----|
| `QuoteOfflineSstTests.cs` | 55–56 | asserts hop-2 5400 + sst metadata |
| `QuoteOfflineSstTests.cs` | 99–100 | asserts log 108 |
| `QuoteOfflineSstTests.cs` | 132–133 | asserts log 270 |

Do not point these at `NoSstBilling()`.

### 6.5 Suggested `CreateInitiateHandler` after the fix

```csharp
private static InitiateCheckoutCommandHandler CreateInitiateHandler(
    Guid orgId,
    ICommerceRepository repository,
    IMediator mediator,
    IBillingQueryService? billing = null)
{
    // ... existing one / comms / config ...
    return new InitiateCheckoutCommandHandler(
        one, repository, mediator, config, comms,
        billing ?? CommerceBillingStubs.NoSstBilling());
}
```

Default empty SST. SST tests in other fixtures keep constructing with an explicit registered stub.

---

## 7. Shared helper with cluster 01 if both touch InitiateCheckout

`plans/010-failed-tests/` was empty when this file was written (`01-*.md` not present yet). Cluster 01 is expected to be the **other** InitiateCheckout / B2B / custom-session tests that construct the same handler without billing.

### 7.1 Cluster 01 surfaces that share the hole

Confirmed red on the same HEAD with the broader filter:

| Test | File | Handler line | Notes |
|------|------|--------------|-------|
| `InitiateCheckout_CustomSession_CopiesIsB2bRequiredIntoMetadata` | `CheckoutB2bIdentityTests.cs:102` | custom 121 | uses `CreateHandler` at 265 |
| `InitiateCheckout_CustomSession_MissingIdPair_Throws` | `:147` | custom 121 | expected `*ID type*`; **SST runs before B2B validation** so message is now the undercharge throw |
| `InitiateCheckout_CustomSession_PassesIdPairNamed_NotCompanyNameAsIdValue` | `:176` | custom 121 | |
| `InitiateCheckout_ProductFlagOff_DoesNotStampB2b` | `:74` | product 339 | `CreateHandler` |
| `InitiateCheckout_RequiresTaxId_WithTinAndCompany_ResolvesCrmWithoutIdValue_AndStampsB2b` | `:45` | product 339 | |
| `InitiateCheckout_SessionId_StampsB2bMetadataAndRequiresTin` | `CreateCustomCheckoutAndInitiateSessionTests.cs:125` | custom 121 | first call expected `*tax ID*`; SST throw wins because 121 &lt; 148 |

`CheckoutB2bIdentityTests.CreateHandler` (247–265) is a **clone** of `CreateInitiateHandler` and also does:

```csharp
return new InitiateCheckoutCommandHandler(one, repository, mediator, config, comms);
```

`CreateCustomCheckoutAndInitiateSessionTests` inlines the same five-arg ctor at **119** and **165**. Line 165 (`InitiateCheckout_CompletedSession_Throws`) still **passes**: completed session throws at handler 110, before SST at 121.

### 7.2 Cluster 01 tests that do **not** need billing to pass

| Test | Why |
|------|-----|
| `InitiateCheckout_RequiresTaxId_MissingTin_ThrowsExistingMessage` | `EnforceCheckoutConfiguration` at 484 (`*tax ID*`) is **before** product SST 339 |
| `MergeClientIntoGateway_StampsB2bWhenRequested` | no handler |
| `CreateCustomCheckout_AllocatesQuoteNumberOnce` | `CreateCustomCheckoutCommandHandler`, no SST |
| `CreateCustomCheckout_Net30_SetsDueAtAbout30Days` | same |
| `InitiateCheckout_CompletedSession_Throws` | status check at 110 |

### 7.3 Shared helper recommendation

**Yes. Both clusters touch `InitiateCheckoutCommandHandler`. Use one `CommerceBillingStubs.NoSstBilling()`.**

Also share one `CreateInitiateHandler` if practical:

- Move `CreateInitiateHandler` (and optionally `GuestCheckoutCommand`) to an `internal static` test fixture helper used by `CommerceProductCompletenessTests`, `CheckoutB2bIdentityTests`, and `CreateCustomCheckoutAndInitiateSessionTests`.
- Or only share `NoSstBilling()` and pass it at each ctor. Smaller diff; still correct.

Cluster 01 **must** use empty SST, not registered SST:

- Custom B2B tests assert metadata / CRM arity, not 8% tax. Registered SST would add `sst_tax_amount` / change Amount (250 → 270, 100 → 108).
- After `NoSstBilling()`, `InitiateCheckout_CustomSession_MissingIdPair_Throws` and `InitiateCheckout_SessionId_StampsB2bMetadataAndRequiresTin` reach the B2B checks at 144+ and their original `*ID type*` / `*tax ID*` assertions become valid again. **No product reorder required** for those two if billing is stubbed.

Do **not** put the stub in `Lazuar.TestSupport` unless you add NSubstitute (and Billing.Contracts) there. Keep it in ModuleTests/Commerce.

---

## 8. Files to change later (fixture only)

Product code is **not** in this list. Issue 167 stays. Issue 181 (required ctor) is a separate product change.

### 8.1 Required for assigned 13

| File | Change |
|------|--------|
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceBillingStubs.cs` | **add** `NoSstBilling()` (new file) |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs` | pass billing in `CreateInitiateHandler` (1410); replace or fix inline ctors at 740 and 780 |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/ChosenPriceDiscountTests.cs` | pass `NoSstBilling()` at MarkPaid ctor line 73 |

### 8.2 Required so the rest of the same fixtures stay green

| File | Change |
|------|--------|
| `CommerceProductCompletenessTests.cs` | MarkPaid ctors at 294, 330, 366, 1339 (and optionally 395 Initiate) |

### 8.3 Shared with cluster 01 (do in the same fixture PR or immediately after)

| File | Change |
|------|--------|
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CheckoutB2bIdentityTests.cs` | `CreateHandler` line 265 |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CreateCustomCheckoutAndInitiateSessionTests.cs` | ctors at 119 and 165 |

### 8.4 Do not change for this fix

| File | Reason |
|------|--------|
| `Modules/Commerce/Application/SubscriptionBillingAmount.cs` | 167 pin; `MerchantHasSst_Null_Billing_Throws` |
| `Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` | optional billing is 181; behavior after stub is already correct |
| `Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs` | same |
| `Modules/Commerce/Application/Commands/ProcessZeroAmountCheckoutCommand.cs` | no billing; not the failure |
| `Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler*.cs` | no billing; test 2 second half is fine |
| `QuoteOfflineSstTests.cs` | already correct with registered SST |
| `SubscriptionBillingAmountTests.cs` | already covers null-throw + NoSst + SST 108/324 |
| `BillingEngineJobTests.cs` | already the template |
| `Lazuar.TestSupport` | no NSubstitute today |

### 8.5 Suggested verification after the fixture PR

```bash
dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj \
  --filter "FullyQualifiedName~CommerceProductCompletenessTests|FullyQualifiedName~ChosenPriceDiscountTests|FullyQualifiedName~CheckoutB2bIdentityTests|FullyQualifiedName~CreateCustomCheckoutAndInitiateSessionTests|FullyQualifiedName~QuoteOfflineSstTests|FullyQualifiedName~SubscriptionBillingAmountTests"
```

Assigned 13 must pass. `MerchantHasSst_Null_Billing_Throws` must still pass. `QuoteOfflineSstTests` must still assert 5400 / 108 / 270.

---

## Appendix A — Issue 167 / 181 timeline vs these tests

| When | What |
|------|------|
| Audit (`plans/009-bugs/10-tenancy-workers-contracts-tests.md` B10-X11, `plans/009-bugs/01-commerce-checkout-activation.md` B01-C11) | Null billing ⇒ `MerchantHasSstAsync` returns false ⇒ hop-1 undercharge. Tests pin that skip. |
| `issues/167-...` | P1, resolved on `fix/167-sst-fail-closed`. |
| `49606466` (2026-08-18) | Throw instead of `return false`. |
| `issues/181-...` | P2, still **open**. Wants required ctor + an initiate test that stubs SST and asserts Amount 108. Audit snippet still shows pre-167 `return false`. |
| These ModuleTests | Never updated after 49606466. HEAD `4531f210` still constructs handlers with implicit `billing: null`. |

## Appendix B — `CreateProduct` / `GuestCheckoutCommand` defaults (assigned completeness tests)

```56:76:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs
    private static Product CreateProduct(
        Guid orgId,
        string interval = "mo",
        ...
        string gatewayName = "STRIPE",
        string pricingModel = "FIXED")
    {
        return new Product(
            orgId, "Pro Plan", "pro-plan", 100m, pricingModel, 0m, "MYR",
            interval, gatewayName, new CheckoutConfiguration(...), new[] { "telegram" });
    }
```

```1372:1390:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs
    private static InitiateCheckoutCommand GuestCheckoutCommand(string? couponCode, int quantity = 1) =>
        new("acme", "pro-plan", "Ada", "ada@example.com", ..., Quantity: quantity, IsGuestCheckout: true, CouponCode: couponCode);
```

No `PriceId`, no `Interval`, no `SessionId` (except the custom test, which builds its own command). Tenant slug is always `"acme"`.

## Appendix C — Dual-price MarkPaid setup

```80:88:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/ChosenPriceDiscountTests.cs
    private static Product DualPriceProduct(Guid orgId)
    {
        var product = new Product(
            orgId, "Plan", "plan", 100m, "FIXED", 0m, "MYR", "mo", "BILLPLZ",
            new CheckoutConfiguration(false, false, false),
            new[] { "telegram" });
        product.UpsertPrice("yr", 1000m, isDefault: false);
        return product;
    }
```

Session: `new CheckoutSession(orgId, client, product.Id, coupon.Id, expires, 1, yearly.Id)`. Quantity 1, chosen yearly 1000, 10% → 900 booked on the tx log after SST is allowed to return false.

## Appendix D — Live stacks (assigned 13)

All `InvalidOperationException` / `IBillingQueryService is required to decide SST; refusing to undercharge.`

| Test | Test line | Next frame |
|------|-----------|------------|
| `MarkPaid_YearlyTenPercentCoupon_BooksChosenRowNotCatalogPrice` | ChosenPriceDiscountTests.cs:74 | MarkPaid `HandleProductSessionAsync` :97 |
| `InitiateCheckout_CustomSession_StillSendsLineSumAndQuantityOne` | CompletenessTests.cs:1259 | Initiate `Handle` :121 |
| `InitiateCheckout_FixedOneTime_Qty3_PersistsSessionAndPaidOrderQuantity` | :1198 | Initiate `Handle` :339 |
| `InitiateCheckout_FixedOneTime_Qty3_SendsUnitNetAndQuantity` | :1072 | :339 |
| `InitiateCheckout_FixedOneTime_Qty3_TenPercentCoupon_SendsUnitNetNinety` | :1108 | :339 |
| `InitiateCheckout_FixedRecurring_NonOneQuantity_Persists("mo","FIXED",3)` | :1155 | :339 |
| `InitiateCheckout_FixedRecurring_NonOneQuantity_Persists("yr","FIXED",2)` | :1155 | :339 |
| `InitiateCheckout_HundredPercentCoupon_BillplzMonthly_StillBypasses` | :687 | :339 |
| `InitiateCheckout_HundredPercentCoupon_Qty3_WritesZeroAmountOrderWithQuantity` | :1301 | :339 |
| `InitiateCheckout_HundredPercentCoupon_StripeMonthly_MintsHop2SetupSession` | :546 | :339 |
| `InitiateCheckout_PaidPath_KeepsSessionOpen_AndReturnsGatewayUrl` | :781 | :339 |
| `InitiateCheckout_TrialStripeMonthly_MintsHop2WithCommerceType` | :585 | :339 |
| `InitiateCheckout_ZeroAmountCoupon_ReturnsSuccessUrlWithSessionId_AndCompletesSession` | :741 | :339 |

Innermost frame for all thirteen: `SubscriptionBillingAmount.MerchantHasSstAsync` at `SubscriptionBillingAmount.cs:97`.

## Appendix E — Handler construction map (every `new InitiateCheckoutCommandHandler` / `new MarkCheckoutAsPaidOfflineCommandHandler` in ModuleTests)

**InitiateCheckout**

| File | Line | Billing passed? | Assigned? |
|------|------|-----------------|-----------|
| CompletenessTests.cs | 395 | no | no (phone, passes) |
| CompletenessTests.cs | 740 | no | **yes** (zero coupon) |
| CompletenessTests.cs | 780 | no | **yes** (paid path) |
| CompletenessTests.cs | 1410 (`CreateInitiateHandler`) | no | **yes** (most) |
| CheckoutB2bIdentityTests.cs | 265 (`CreateHandler`) | no | cluster 01 |
| CreateCustomCheckoutAndInitiateSessionTests.cs | 119 | no | cluster 01 |
| CreateCustomCheckoutAndInitiateSessionTests.cs | 165 | no | no (completed session, passes) |
| QuoteOfflineSstTests.cs | 55–56 | **yes, SstBilling** | keep |

**MarkPaid**

| File | Line | Billing passed? | Assigned? |
|------|------|-----------------|-----------|
| ChosenPriceDiscountTests.cs | 73 | no | **yes** |
| CompletenessTests.cs | 294 | no | collateral |
| CompletenessTests.cs | 330 | no | collateral |
| CompletenessTests.cs | 366 | no | collateral |
| CompletenessTests.cs | 1339 | no | collateral |
| QuoteOfflineSstTests.cs | 99–100 | **yes, SstBilling** | keep |
| QuoteOfflineSstTests.cs | 132–133 | **yes, SstBilling** | keep |

## Appendix F — Why hop-2 Amount stays unit after the fixture fix

Assigned qty tests exist because adapters multiply `Amount * Quantity` (comment at CompletenessTests.cs:1053–1054). Handler comment at InitiateCheckoutCommandHandler.cs:413–414. Query contract at GenerateCheckoutSessionQuery.cs:7–11. Custom sessions are the exception: they pass the **line sum** as Amount and force Quantity 1 (handler 187–198). That is what `InitiateCheckout_CustomSession_StillSendsLineSumAndQuantityOne` pins (command Quantity 3 must not leak into hop-2).

None of that is broken. SST fail-closed just runs before those `mediator.Send(GenerateCheckoutSessionQuery)` lines, so NSubstitute never captures the query and FluentAssertions never runs.

---

**Bottom line.** Update fixtures to compose `IBillingQueryService` the way `BillingEngineJobTests.SetUp` already does: a real substitute whose `GetBillingProfileAsync` returns a null profile (no SST number). Share `NoSstBilling()` with cluster 01. Leave `MerchantHasSstAsync` fail-closed. The quantity / coupon / chosen-price product code is not the failure.
