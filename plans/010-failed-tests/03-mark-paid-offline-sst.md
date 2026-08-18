# 03 — Mark checkout paid offline: SST fail-closed, not CRM arity, not session org lookup

**Slice:** offline mark-paid / mark checkout paid  
**Verdict:** all four assigned tests fail for the **same product exception**: `SubscriptionBillingAmount.MerchantHasSstAsync` throws when `IBillingQueryService` is null (issue **167** fail-closed). They do **not** fail on CRM `GetClientProfileAsync` arity (issue **165**). They do **not** fail on checkout-session organization lookup (issue **8a872da9** / worker-lookup org scope).  
**Fix class:** test composition only. Pass a **no-SST** `IBillingQueryService` stub into `MarkCheckoutAsPaidOfflineCommandHandler`. Keep 167 fail-closed. Do not change product SST math, do not revert `MerchantHasSstAsync` to `return false`.

---

## 1. Title, assigned tests, file paths, HEAD

| Field | Value |
| --- | --- |
| Title | Mark checkout paid offline fails at SST fail-closed (`IBillingQueryService` required) |
| Assigned suite | Lazuar.ModuleTests / Commerce / offline mark-paid |
| Test file (all four) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs` |
| Handler | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs` |
| SST helper | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs` (`MerchantHasSstAsync`) |
| CRM contract | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/CRM/Contracts/ICrmQueryService.cs` |
| SST-aware sibling tests (pass) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/QuoteOfflineSstTests.cs` |
| SST-unaware sibling test (same fail, **not assigned**) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/ChosenPriceDiscountTests.cs` (`MarkPaid_YearlyTenPercentCoupon_BooksChosenRowNotCatalogPrice`) |
| Branch | `fix/180-unify-outbox-inbox` |
| HEAD | `4531f210f61b3d58d0332f1728b6a7889a1d2cad` (`4531f210 fix(api): register every module outbox and inbox through one helper`) |
| Reproduced | 2026-08-18, `dotnet test` against `apps/lazuar-api/tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj` |

### Assigned tests — file confirmation

`rg` over `*.cs` finds **exactly four** matches, **all in one file**:

| Test method | File | Line |
| --- | --- | --- |
| `MarkCheckoutAsPaidOffline_ProductSession_CreatesActiveSubscription_AndTxLog` | `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs` | 269 |
| `MarkCheckoutAsPaidOffline_ProductRequiresTaxId_PublishesB2b` | same | 310 |
| `MarkCheckoutAsPaidOffline_CustomSession_CompletesWithoutSubscription` | same | 338 |
| `MarkCheckoutAsPaidOffline_OneTime_Qty3_WritesLineTotalOrder` | same | 1316 |

`QuoteOfflineSstTests.cs` constructs the **same handler** (lines 99–100 and 132–133) but is **not** in the assigned set. Those two tests **pass** because they inject `SstBilling(orgId)`.

`ChosenPriceDiscountTests.MarkPaid_YearlyTenPercentCoupon_BooksChosenRowNotCatalogPrice` (line 47, construct at line 73) is the same three-arg constructor bug. Not assigned; documented in §4.5 so the later patch does not leave a twin.

### What this analysis is *not*

- Not a product SST bug. Issue **034** (`fix/034-quote-offline-sst`, commit `f1f7ba03`) already taught the handler to call `MerchantHasSstAsync` + `GrossBreakdown` / `CustomQuoteBreakdown`.
- Not a CRM compile break. Issue **165** (`42b7ad37`) already widened `GetClientProfileAsync` to `(Guid organizationId, Guid profileId)` and updated every mock in this file.
- Not a session-org-lookup miss. Commit `8a872da9` already changed `GetCheckoutSessionByIdAsync` / `GetProductByIdAsync` / `GetCouponByIdAsync` to take `organizationId`, and the completeness tests already stub those overloads.
- Not a “session not found” or `TryComplete` status failure. The session is mocked in-memory as `OPEN`, `TryComplete()` returns true, then CRM returns a DTO, **then** SST throws.
- This document does **not** implement the test fix.

---

## 2. Handler constructor: `IBillingQueryService?` is optional

The handler still accepts a **nullable optional** billing query service. Three-arg construction compiles. That is why the assigned tests compile and only fail at runtime.

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

Facts about this constructor:

- `ICrmQueryService` is **required**. Tests must (and do) pass a substitute.
- `IBillingQueryService? billingQueryService = null` is **optional**. Omitting it is still a legal C# call. `_billingQueryService` is then `null`.
- Production MediatR / DI **will** inject `IBillingQueryService` because Billing registers it:

```51:51:apps/lazuar-api/Modules/Billing/Infrastructure/DependencyInjection.cs
        services.AddScoped<IBillingQueryService, BillingQueryService>();
```

- The optional parameter was added in `f1f7ba03` (`fix(commerce): apply exclusive SST on quotes and mark-paid`, 2026-08-17) when issue **034** wired SST onto mark-paid. At that moment `MerchantHasSstAsync(null, …)` **returned false**, so three-arg tests kept passing and silently booked net.
- Issue **167** (`49606466`, 2026-08-18) flipped the helper to **throw** on null billing. Completeness tests were **not** in that commit’s test update set. That is the regression window.

Every assigned test constructs the handler with three arguments only:

```294:295:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs
        var handler = new MarkCheckoutAsPaidOfflineCommandHandler(repository, eventBus, crm);
        await handler.Handle(new MarkCheckoutAsPaidOfflineCommand(orgId, session.Id), CancellationToken.None);
```

```330:331:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs
        var handler = new MarkCheckoutAsPaidOfflineCommandHandler(repository, eventBus, crm);
        await handler.Handle(new MarkCheckoutAsPaidOfflineCommand(orgId, session.Id), CancellationToken.None);
```

```366:367:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs
        var handler = new MarkCheckoutAsPaidOfflineCommandHandler(repository, eventBus, crm);
        await handler.Handle(new MarkCheckoutAsPaidOfflineCommand(orgId, session.Id), CancellationToken.None);
```

```1339:1340:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs
        var handler = new MarkCheckoutAsPaidOfflineCommandHandler(repository, eventBus, crm);
        await handler.Handle(new MarkCheckoutAsPaidOfflineCommand(orgId, session.Id), CancellationToken.None);
```

`CommerceProductCompletenessTests.cs` usings (lines 1–32) include `Modules.CRM.Contracts` and `Modules.Commerce.Application` but **do not** include `Modules.Billing.Contracts`. There is no billing stub helper in this fixture. The later patch must add that using (or a fully-qualified type) plus a small factory.

The command itself is only org + session; it does not carry a billing flag:

```6:8:apps/lazuar-api/Modules/Commerce/Contracts/Commands/MarkCheckoutAsPaidOfflineCommand.cs
public record MarkCheckoutAsPaidOfflineCommand(Guid OrganizationId, Guid SessionId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
```

Admin endpoint (production path) is MediatR, so billing is composed:

```59:65:apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints.cs
        adminGroup.MapPost("/checkouts/{id:guid}/mark-paid", async Task<Ok<StatusResponse>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new MarkCheckoutAsPaidOfflineCommand(ctx.TenantId, id));
            return TypedResults.Ok(new StatusResponse { Status = "completed" });
        }).RequireAuthorization("OrgMember");
```

So this is a **unit-test host** hole, not a live mark-paid endpoint that is missing Billing in `AddAllModules`.

---

## 3. CRM `GetClientProfileAsync(org, profile)` — mocks compile; SST throws first

### 3.1 Current contract (issue 165 is already on HEAD)

```8:13:apps/lazuar-api/Modules/CRM/Contracts/ICrmQueryService.cs
public interface ICrmQueryService
{
    Task<ClientProfileDto?> GetClientProfileAsync(Guid organizationId, Guid profileId);
    Task<IEnumerable<ClientProfileDto>> GetClientProfilesAsync(Guid organizationId, IEnumerable<Guid> profileIds);
    Task<ClientProfileDto?> GetClientProfileByEmailAsync(Guid organizationId, string email);
}
```

There is **no** one-argument `GetClientProfileAsync(Guid profileId)` left on the interface. A one-arg mock would be a **compile** error (`CS1501` / NSubstitute cannot bind).

Implementation is org-scoped (issue 165 product fix):

```54:62:apps/lazuar-api/Modules/CRM/Infrastructure/CrmQueryService.cs
    public async Task<ClientProfileDto?> GetClientProfileAsync(Guid organizationId, Guid profileId)
    {
        var profile = await _dbContext.ClientProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.Id == profileId);

        return profile == null ? null : MapToDto(profile);
    }
```

### 3.2 Handler already calls the two-arg overload

```49:51:apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs
        var clientProfile = await _crmQueryService.GetClientProfileAsync(session.OrganizationId, session.ClientProfileId);
        var customerName = clientProfile?.Full_name ?? "Unknown Customer";
        var customerEmail = clientProfile?.Email ?? string.Empty;
```

That call happens **before** any `MerchantHasSstAsync`. Order in `Handle`:

1. `GetCheckoutSessionByIdAsync(request.OrganizationId, request.SessionId, ct)` — line 37.
2. Null / org mismatch → `"Checkout session not found."` — lines 39–42.
3. `session.TryComplete()` — line 44. OPEN → COMPLETED in memory.
4. `GetClientProfileAsync(session.OrganizationId, session.ClientProfileId)` — line 49.
5. Product branch (`session.ProductId.HasValue`) → `HandleProductSessionAsync` — lines 54–57 → SST at line 97.
6. Custom branch (`AdHocLineItems.Any()`) → `HandleCustomSessionAsync` — lines 61–64 → SST at line 195.

### 3.3 Assigned-test mocks already use two args

Issue **165** commit `42b7ad37` rewrote every `GetClientProfileAsync` in this file from `GetClientProfileAsync(clientId)` to `GetClientProfileAsync(Arg.Any<Guid>(), clientId)`. Current assigned mocks:

```287:292:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs
        crm.GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Offline Buyer",
            Email = "offline@example.com"
        });
```

```323:328:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs
        crm.GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Offline Buyer",
            Email = "offline@example.com"
        });
```

```359:364:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs
        crm.GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Custom Buyer",
            Email = "custom@example.com"
        });
```

```1332:1337:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs
        crm.GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Offline Buyer",
            Email = "offline@example.com"
        });
```

Compile proof: `dotnet test` **built** `Lazuar.ModuleTests` and discovered all four cases. There is no CS error on CRM arity. NSubstitute binds `(Guid, Guid)` and returns the DTO. The handler then uses `Full_name` / `Email` only for the tx-log fields that these tests never reach.

**Answer to the question in the assignment:** yes — the test mocks still compile (they already use two-arg `GetClientProfileAsync`) and they fail at SST first. CRM is not the exception.

If CRM were wrong (no mock, or mock not matching), the handler would **not** throw. It would fall back to `"Unknown Customer"` / `string.Empty` (lines 50–51) and continue into SST anyway. A missing CRM mock cannot produce `IBillingQueryService is required to decide SST`.

### 3.4 Session org lookup is also not the exception

Repository contract (post-`8a872da9`):

```18:18:apps/lazuar-api/Modules/Commerce/Application/ICommerceRepository.cs
    Task<CheckoutSession?> GetCheckoutSessionByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default);
```

Real repository filters both columns:

```101:106:apps/lazuar-api/Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs
    public async Task<CheckoutSession?> GetCheckoutSessionByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default)
    {
        return await _context.CheckoutSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.Id == id, ct);
    }
```

Assigned tests never hit EF. They stub:

```
repository.GetCheckoutSessionByIdAsync(Arg.Any<Guid>(), session.Id, Arg.Any<CancellationToken>()).Returns(session);
```

`session.OrganizationId` is the same `orgId` passed in `MarkCheckoutAsPaidOfflineCommand`. Lines 39–42 cannot throw `"Checkout session not found."` on this fixture.

Product loads are the same pattern (`Arg.Any<Guid>(), product.Id` → product). The three product tests would not throw `"Associated product not found."` either.

A session-org failure would be `InvalidOperationException: Checkout session not found.` That string does **not** appear in any of the four stacks.

---

## 4. Per-test failure

### 4.0 Shared actual exception (reproduced)

Command run from repo root:

```text
dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj \
  --filter "FullyQualifiedName~MarkCheckoutAsPaidOffline_CustomSession_CompletesWithoutSubscription|FullyQualifiedName~MarkCheckoutAsPaidOffline_OneTime_Qty3_WritesLineTotalOrder|FullyQualifiedName~MarkCheckoutAsPaidOffline_ProductRequiresTaxId_PublishesB2b|FullyQualifiedName~MarkCheckoutAsPaidOffline_ProductSession_CreatesActiveSubscription_AndTxLog"
```

Result: **Total tests: 4. Failed: 4.** Build succeeded (0 warnings, 0 errors). Runtime only.

**Same exception on all four:**

```text
System.InvalidOperationException : IBillingQueryService is required to decide SST; refusing to undercharge.
```

Throw site (issue 167, commit `49606466`):

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

167’s own pin test (passes on this HEAD):

```120:125:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SubscriptionBillingAmountTests.cs
    [Test]
    public async Task MerchantHasSst_Null_Billing_Throws()
    {
        var act = () => SubscriptionBillingAmount.MerchantHasSstAsync(null, Guid.CreateVersion7());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*refusing to undercharge*");
    }
```

`49606466` updated `SubscriptionBillingAmountTests.Gross_NoSst_Is100` to pass `SstBilling(..., sstNumber: "")` instead of `billing: null`. It did **not** touch `CommerceProductCompletenessTests.cs`. Completeness tests kept the pre-167 “omit billing, treat as no SST” habit.

Classification vs the three hypotheses:

| Hypothesis | Exception that would prove it | Observed? |
| --- | --- | --- |
| SST throw (167) | `IBillingQueryService is required to decide SST; refusing to undercharge.` | **Yes, all four** |
| CRM arity (165) | compile error, or NSubstitute / missing-method at the CRM call | **No** |
| Session org lookup | `Checkout session not found.` | **No** |

### 4.1 `MarkCheckoutAsPaidOffline_ProductSession_CreatesActiveSubscription_AndTxLog`

- **File / line:** `CommerceProductCompletenessTests.cs` 269–307. Throw at **test line 295**.
- **Arrange:** `CreateProduct(orgId, interval: "mo")` → catalog price `100`, `SstTaxType = "06"`, `SstRatePercent = 0`. Session is product checkout, qty default 1, no coupon.
- **Handler construct:** three-arg, no billing (line 294).
- **Handle path:** session found → `TryComplete` → CRM DTO `"Offline Buyer"` → `ProductId.HasValue` → `HandleProductSessionAsync`.
- **Throw:**

```text
at SubscriptionBillingAmount.MerchantHasSstAsync(...) SubscriptionBillingAmount.cs:line 97
at MarkCheckoutAsPaidOfflineCommandHandler.HandleProductSessionAsync(...) MarkCheckoutAsPaidOfflineCommandHandler.cs:line 97
at MarkCheckoutAsPaidOfflineCommandHandler.Handle(...) MarkCheckoutAsPaidOfflineCommandHandler.cs:line 56
at CommerceProductCompletenessTests.MarkCheckoutAsPaidOffline_ProductSession_CreatesActiveSubscription_AndTxLog() CommerceProductCompletenessTests.cs:line 295
```

Product SST call site:

```96:102:apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs
        var unitNet = Math.Max(0, unitAmount - unitDiscount);
        var merchantHasSst = await SubscriptionBillingAmount.MerchantHasSstAsync(
            _billingQueryService, session.OrganizationId);
        var breakdown = SubscriptionBillingAmount.GrossBreakdown(
            unitNet, quantity, product.SstTaxType, product.SstRatePercent, merchantHasSst);
        var totalAmount = breakdown.Gross;
```

Nothing after line 97 runs: no `Subscription`, no `CommerceTransactionLog`, no `SubscriptionActivatedIntegrationEvent`, no `ManualSubscriberEnrolledIntegrationEvent`, no `SaveChangesAsync`.

**What the test wanted after a no-SST stub:**

- `session.Status == "COMPLETED"` (already true in memory after line 44, but the test never reaches the assert).
- one `Subscription` with `Status == "ACTIVE"` and `IsReminderOnly == true` (`HandleProductSessionAsync` 130–139 calls `SubscriptionActivation.Start(..., reminderOnly: true)`; product has `TrialDays = 0` so `Activate` not `ActivateTrial`).
- one tx log `CONFIRMED` / `MANUAL_OFFLINE`.
- both `SubscriptionActivatedIntegrationEvent` and `ManualSubscriberEnrolledIntegrationEvent` published.

`CreateProduct` defaults (lines 56–77 of the test file) never call `SetSst`. Product constructor forces tax type `06` / rate `0`:

```75:76:apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Product.cs
        SstTaxType = "06";
        SstRatePercent = 0m;
```

`SstTaxMath.Compute` returns `(06, 0)` unless type is `02` **and** merchant has SST **and** rate > 0:

```8:20:apps/lazuar-api/Modules/Commerce/Application/SstTaxMath.cs
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
```

So even a **registered-SST** stub would still book `100` on this product. The test does not assert amount. A no-SST stub (`Sst_registration_number` empty) is still the right stub because that is what the test’s pre-167 world assumed, and it keeps custom-path assertions honest (see 4.3).

### 4.2 `MarkCheckoutAsPaidOffline_ProductRequiresTaxId_PublishesB2b`

- **File / line:** `CommerceProductCompletenessTests.cs` 310–335. Throw at **test line 331**.
- **Arrange:** `CreateProduct(orgId, requiresTaxId: true)` → `CheckoutConfiguration(false, true, false)`. Session is product checkout. No coupon. Default interval `"mo"`.
- **Handler construct:** three-arg, no billing (line 330).
- **Handle path:** identical to 4.1 through `HandleProductSessionAsync` line 97.
- **Throw:**

```text
at SubscriptionBillingAmount.MerchantHasSstAsync(...) :97
at HandleProductSessionAsync(...) :97
at Handle(...) :56
at MarkCheckoutAsPaidOffline_ProductRequiresTaxId_PublishesB2b() :331
```

Never reaches the B2B assert:

```333:334:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs
        await eventBus.Received().PublishAsync(Arg.Is<ManualSubscriberEnrolledIntegrationEvent>(e =>
            e.IsB2bRequired));
```

The product code that *would* set the flag (after SST, after tx log, only if `totalAmount > 0`):

```170:183:apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs
        if (totalAmount > 0)
        {
            await _eventBus.PublishAsync(new ManualSubscriberEnrolledIntegrationEvent(
                session.OrganizationId,
                entitlementId,
                session.ClientProfileId,
                product.Id,
                totalAmount,
                currency,
                "MANUAL_OFFLINE",
                $"Manual settlement for session {session.Id}",
                txLog.Id,
                session.IsB2bRequired || product.CheckoutConfiguration.RequiresTaxId));
        }
```

Product-session constructor hard-codes `IsB2bRequired = false` (`CheckoutSession.cs` 70). The event flag is therefore `false || product.CheckoutConfiguration.RequiresTaxId`. That is the 034/536296fc “keep B2B on offline product mark-paid” behavior. With a no-SST stub, `totalAmount` is `100 > 0`, the event publishes, the assert passes.

This test does not add a tx-log capture and does not call `SaveChanges` expectations. SST is the only blocker.

### 4.3 `MarkCheckoutAsPaidOffline_CustomSession_CompletesWithoutSubscription`

- **File / line:** `CommerceProductCompletenessTests.cs` 338–375. Throw at **test line 367**.
- **Arrange:** ad-hoc session, one line `AdHocLineItem("Consulting", 1, 250m)`, `isB2bRequired: false`, **no** `ProductId`.
- **Handler construct:** three-arg, no billing (line 366).
- **Handle path:** session found → `TryComplete` → CRM `"Custom Buyer"` → `ProductId` is null → `AdHocLineItems.Any()` → `HandleCustomSessionAsync`.
- **Throw:**

```text
at SubscriptionBillingAmount.MerchantHasSstAsync(...) SubscriptionBillingAmount.cs:line 97
at MarkCheckoutAsPaidOfflineCommandHandler.HandleCustomSessionAsync(...) MarkCheckoutAsPaidOfflineCommandHandler.cs:line 195
at MarkCheckoutAsPaidOfflineCommandHandler.Handle(...) MarkCheckoutAsPaidOfflineCommandHandler.cs:line 63
at CommerceProductCompletenessTests.MarkCheckoutAsPaidOffline_CustomSession_CompletesWithoutSubscription() :367
```

Custom SST call site:

```194:197:apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs
        var customNet = session.AdHocLineItems.Sum(x => x.UnitPrice * x.Quantity);
        var merchantHasSst = await SubscriptionBillingAmount.MerchantHasSstAsync(
            _billingQueryService, session.OrganizationId);
        var totalAmount = SubscriptionBillingAmount.CustomQuoteBreakdown(customNet, merchantHasSst).Gross;
```

`CustomQuoteBreakdown` **always** asks for service tax type `02` at 8% when the merchant has SST:

```41:42:apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs
    public static Breakdown CustomQuoteBreakdown(decimal net, bool merchantHasSst) =>
        GrossBreakdown(net, 1, SstTaxMath.ServiceTax, DefaultServiceTaxRatePercent, merchantHasSst);
```

```39:39:apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs
    public const decimal DefaultServiceTaxRatePercent = 8m;
```

Pinned in `QuoteOfflineSstTests`:

```141:145:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/QuoteOfflineSstTests.cs
    public void CustomQuoteBreakdown_MatchesGrossBreakdown()
    {
        SubscriptionBillingAmount.CustomQuoteBreakdown(5000m, true).Gross.Should().Be(5400m);
        SubscriptionBillingAmount.CustomQuoteBreakdown(5000m, false).Gross.Should().Be(5000m);
    }
```

So:

| Stub | `merchantHasSst` | booked amount | `logs[0].Amount.Should().Be(250m)` |
| --- | --- | --- | --- |
| omitted / null (today) | throw | n/a | never reached |
| SST registered (`W10-…`) | true | **270** | **fails** |
| SST empty / whitespace | false | **250** | passes |

This is why the recommended stub is **no SST**, not a copy of `QuoteOfflineSstTests.SstBilling`. That sibling test *wants* 270 for a 250 custom line (`MarkPaid_CustomSst_BooksGross`, lines 108–137).

**What the test wanted after a no-SST stub:**

- `session.Status == "COMPLETED"`.
- `subscriptions` empty (custom path never calls `AddSubscription`).
- one tx log amount **250**, product name `"Custom Payment Request"`.
- `ManualSubscriberEnrolledIntegrationEvent` published (`ProductId: Guid.Empty`, entitlement id = session id — line 218–228).
- **no** `SubscriptionActivatedIntegrationEvent`.

### 4.4 `MarkCheckoutAsPaidOffline_OneTime_Qty3_WritesLineTotalOrder`

- **File / line:** `CommerceProductCompletenessTests.cs` 1316–1347. Throw at **test line 1340**.
- **Arrange:** `CreateProduct(orgId, interval: "one_time")`, session `quantity: 3`. Catalog unit 100. No coupon.
- **Handler construct:** three-arg, no billing (line 1339).
- **Handle path:** product branch → `HandleProductSessionAsync`. `quantity = Math.Max(1, session.Quantity)` = 3. `unitNet = 100`. SST throw before `new Order(...)`.
- **Throw:**

```text
at SubscriptionBillingAmount.MerchantHasSstAsync(...) :97
at HandleProductSessionAsync(...) :97
at Handle(...) :56
at MarkCheckoutAsPaidOffline_OneTime_Qty3_WritesLineTotalOrder() :1340
```

Never reaches:

```107:118:apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs
        if (product.Interval == "one_time")
        {
            var order = new Order(
                session.OrganizationId,
                session.ClientProfileId,
                product.Id,
                totalAmount,
                currency,
                quantity);
            order.Complete();
            _repository.AddOrder(order);
```

**What the test wanted:**

```1342:1346:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs
        session.Status.Should().Be("COMPLETED");
        orders.Should().HaveCount(1);
        orders[0].AmountPaid.Should().Be(300m);
        orders[0].Quantity.Should().Be(3);
        await eventBus.Received().PublishAsync(Arg.Any<OrderCompletedIntegrationEvent>());
```

`300m` is **line total without SST** (`100 × 3`). Product tax type is still `06` / 0, so a registered-SST stub would *also* produce 300. A no-SST stub produces 300. An omitted stub throws.

If a future edit `SetSst("02", 8m)` on this product without changing the assert, a registered stub would book `324` and the test would fail for a *different* reason. Keep the stub no-SST unless the test is rewritten as an SST test.

### 4.5 Sibling (not assigned) — same exception

`ChosenPriceDiscountTests.MarkPaid_YearlyTenPercentCoupon_BooksChosenRowNotCatalogPrice` (`ChosenPriceDiscountTests.cs` 47–78) constructs:

```73:74:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/ChosenPriceDiscountTests.cs
        var handler = new MarkCheckoutAsPaidOfflineCommandHandler(repository, Substitute.For<IEventBus>(), crm);
        await handler.Handle(new MarkCheckoutAsPaidOfflineCommand(orgId, session.Id), CancellationToken.None);
```

Reproduced on the same HEAD:

```text
Failed MarkPaid_YearlyTenPercentCoupon_BooksChosenRowNotCatalogPrice
System.InvalidOperationException : IBillingQueryService is required to decide SST; refusing to undercharge.
  at MerchantHasSstAsync (...) :97
  at HandleProductSessionAsync (...) :97
  at Handle (...) :56
  at ChosenPriceDiscountTests.MarkPaid_YearlyTenPercentCoupon_BooksChosenRowNotCatalogPrice() :74
```

CRM mock is already two-arg (line 66). Session/product/coupon org stubs are already two-arg. Same SST-first failure. Expects `log.Amount == 900m` (yearly 1000 minus 10%). Product tax type `06`, so no-SST stub keeps 900.

`ChosenPriceDiscountTests.ProcessZeroAmount_YearlyHundredPercentCoupon_DoesNotThrow` **passes** — it uses `ProcessZeroAmountCheckoutCommandHandler`, which does not call `MerchantHasSstAsync`.

`QuoteOfflineSstTests` (four tests) **all pass**. Contrast:

```99:100:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/QuoteOfflineSstTests.cs
        var handler = new MarkCheckoutAsPaidOfflineCommandHandler(
            repository, Substitute.For<IEventBus>(), crm, SstBilling(orgId));
```

```147:157:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/QuoteOfflineSstTests.cs
    private static IBillingQueryService SstBilling(Guid organizationId)
    {
        var billing = Substitute.For<IBillingQueryService>();
        billing.GetBillingProfileAsync(organizationId).Returns(new TenantBillingProfileDto
        {
            Legal_name = "Studio",
            Tin = "C12345678901",
            Sst_registration_number = "W10-1234-12345678"
        });
        return billing;
    }
```

Sibling filter run on this HEAD: **Failed 1, Passed 6** (`QuoteOfflineSstTests` × 4 + `MerchantHasSst_Null_Billing_Throws` + `ProcessZeroAmount_…` pass; ChosenPrice `MarkPaid_…` fails).

### 4.6 In-memory side effect (not the failure, worth knowing)

`TryComplete()` runs **before** SST:

```44:47:apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs
        if (!session.TryComplete())
        {
            throw new InvalidOperationException($"Cannot mark session as paid. Current status is {session.Status}.");
        }
```

```145:155:apps/lazuar-api/Modules/Commerce/Domain/Aggregates/CheckoutSession.cs
    public bool TryComplete()
    {
        if (!string.Equals(Status, "OPEN", StringComparison.Ordinal))
        {
            return false;
        }

        Status = "COMPLETED";
        UpdatedAt = DateTime.UtcNow;
        return true;
    }
```

After the throw, the **in-memory** `session.Status` is already `"COMPLETED"`, but `SaveChangesAsync` was never called. These tests use a substitute repository, so there is no EF tracker leak. A real DbContext in the same scope would have a dirty COMPLETED session that rolls away when the scope dies. Do not “fix” that as part of this test slice unless a separate product issue is opened. It is not why NUnit reports failure.

`f1f7ba03` originally completed the session *after* SST inside each private method (`session.Complete()`). `17885429` moved completion to the top as `TryComplete()` (OPEN-only). That reorder is why the side effect exists today.

---

## 5. Recommended fix

**Do this (test-only):**

1. Keep `MerchantHasSstAsync` fail-closed. Do **not** restore `return false` for null billing. Issue 167 is resolved and pinned by `MerchantHasSst_Null_Billing_Throws`.
2. Keep the handler’s SST calls. Do **not** skip SST on mark-paid (that would reopen issue 034).
3. In the four assigned tests, pass a **no-SST** `IBillingQueryService` stub as the fourth constructor argument.
4. Leave the CRM mocks as they are. They already call `GetClientProfileAsync(Arg.Any<Guid>(), clientId)`. Verified.
5. Leave session / product / coupon org stubs as they are. Verified.
6. Do **not** copy `QuoteOfflineSstTests.SstBilling` with `Sst_registration_number = "W10-1234-12345678"` into the custom-session test. That stub books **270** and breaks `logs[0].Amount.Should().Be(250m)`.

**Why no-SST rather than registered SST for this fixture**

These four tests are completeness / money-loop tests from before exclusive SST. They assert **net** amounts (250 custom, 300 one-time qty 3) or they do not assert amount at all (product session, B2B flag). `CreateProduct` never sets tax type `02`. Custom path *would* add 8% if `merchantHasSst` is true.

`SubscriptionBillingAmountTests.Gross_NoSst_Is100` is the canonical “merchant has a billing profile but no SST number” stub: `SstBilling(orgId, sstNumber: "")`. Mirror that.

**Why not make `_billingQueryService` required now**

Making the parameter required (drop `= null`) would turn this class of bugs into compile errors. That is a reasonable later hardening (same spirit as issue **181** on `InitiateCheckoutCommandHandler`). It is **not** required to green these four tests, and it is a product/API constructor change. The assignment is: pass stub billing; keep 167 fail-closed.

`InitiateCheckoutCommandHandler` still has `IBillingQueryService? = null` (lines 29–37). Issue **181** is still **open**. Completeness `CreateInitiateHandler` (lines 1392–1410) also omits billing. That is a **different** failing-test family if 181 is applied. Out of scope here.

**CRM verification (already done, repeat at patch time):**

```text
rg "GetClientProfileAsync" apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs
```

Every hit is `GetClientProfileAsync(Arg.Any<Guid>(), clientId)`. No one-arg leftover. Do not touch those lines unless a later test adds a new mock.

---

## 6. Concrete patch sketch

Sketch only. Do not apply in this analysis pass.

### 6.1 Using

At the top of `CommerceProductCompletenessTests.cs`, add:

```csharp
using Modules.Billing.Contracts;
```

`TenantBillingProfileDto` lives in `Lazuar.ApiTypes` (already imported, line 9). `IBillingQueryService` lives in `Modules.Billing.Contracts` (not currently imported).

### 6.2 Helper on the fixture (next to `CreateProduct` / `CreateInitiateHandler`)

Follow `SubscriptionBillingAmountTests.SstBilling` / `QuoteOfflineSstTests.SstBilling`, but **empty SST number**:

```csharp
private static IBillingQueryService NoSstBilling(Guid organizationId)
{
    var billing = Substitute.For<IBillingQueryService>();
    billing.GetBillingProfileAsync(organizationId).Returns(new TenantBillingProfileDto
    {
        Legal_name = "Studio",
        Tin = "C12345678901",
        Sst_registration_number = ""
    });
    return billing;
}
```

Empty string is enough: `MerchantHasSstAsync` does `!string.IsNullOrWhiteSpace(profile?.Sst_registration_number)` → `false`. Null `Sst_registration_number` would also work. Prefer `""` to match `Gross_NoSst_Is100`.

Do **not** return `Substitute.For<IBillingQueryService>()` with no `GetBillingProfileAsync` setup and call that “good enough.” An unconfigured substitute returns `null` profile, which also yields `merchantHasSst = false`. That happens to work, but it is an implicit null profile, not an explicit “merchant has billing, no SST.” Prefer the explicit DTO.

### 6.3 Four constructor call sites

Replace:

```csharp
var handler = new MarkCheckoutAsPaidOfflineCommandHandler(repository, eventBus, crm);
```

with:

```csharp
var handler = new MarkCheckoutAsPaidOfflineCommandHandler(
    repository, eventBus, crm, NoSstBilling(orgId));
```

Sites:

- line 294 — `MarkCheckoutAsPaidOffline_ProductSession_CreatesActiveSubscription_AndTxLog`
- line 330 — `MarkCheckoutAsPaidOffline_ProductRequiresTaxId_PublishesB2b`
- line 366 — `MarkCheckoutAsPaidOffline_CustomSession_CompletesWithoutSubscription`
- line 1339 — `MarkCheckoutAsPaidOffline_OneTime_Qty3_WritesLineTotalOrder`

No assertion changes. After the stub:

| Test | Expected still |
| --- | --- |
| Product session | COMPLETED, 1 ACTIVE reminder-only sub, 1 CONFIRMED MANUAL_OFFLINE log, both events |
| Requires tax id | `ManualSubscriberEnrolledIntegrationEvent.IsB2bRequired == true` |
| Custom session | COMPLETED, 0 subs, log amount **250**, enroll event, no activate event |
| One-time qty 3 | COMPLETED, 1 order `AmountPaid == 300`, `Quantity == 3`, `OrderCompletedIntegrationEvent` |

### 6.4 Optional same-PR sibling (recommended, not assigned)

`ChosenPriceDiscountTests.cs` line 73, same fourth argument. That file already imports `Lazuar.ApiTypes` and `Modules.CRM.Contracts`; add `Modules.Billing.Contracts` and either a local `NoSstBilling` or a shared test helper.

Do **not** change `QuoteOfflineSstTests` — those tests must keep a **registered** SST number.

### 6.5 What the patch must not do

```csharp
// WRONG — reopens 167
if (billing == null) return false;

// WRONG — skips 034 on the completeness path
var merchantHasSst = false;

// WRONG — breaks custom amount 250
Sst_registration_number = "W10-1234-12345678"

// WRONG — product change for a test-host hole
// delete MerchantHasSstAsync from HandleProductSessionAsync / HandleCustomSessionAsync
```

### 6.6 Verify command (after someone implements)

```text
dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj \
  --filter "FullyQualifiedName~MarkCheckoutAsPaidOffline_CustomSession_CompletesWithoutSubscription|FullyQualifiedName~MarkCheckoutAsPaidOffline_OneTime_Qty3_WritesLineTotalOrder|FullyQualifiedName~MarkCheckoutAsPaidOffline_ProductRequiresTaxId_PublishesB2b|FullyQualifiedName~MarkCheckoutAsPaidOffline_ProductSession_CreatesActiveSubscription_AndTxLog|FullyQualifiedName~MerchantHasSst_Null_Billing_Throws|FullyQualifiedName~QuoteOfflineSstTests|FullyQualifiedName~ChosenPriceDiscountTests"
```

Expect:

- four assigned tests: pass
- `MerchantHasSst_Null_Billing_Throws`: still pass (167 stays closed)
- `QuoteOfflineSstTests`: still pass (registered SST still books 108 / 270)
- `ChosenPriceDiscountTests.MarkPaid_…`: pass only if the sibling constructor is also patched

---

## 7. Files to change later

### Must change to green the assigned four

| File | Change |
| --- | --- |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs` | add `using Modules.Billing.Contracts`; add `NoSstBilling`; pass it at lines 294, 330, 366, 1339 |

### Should change in the same test PR (same bug, not assigned)

| File | Change |
| --- | --- |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/ChosenPriceDiscountTests.cs` | same stub at line 73 |

### Optional later hygiene (not required to green)

| File | Change |
| --- | --- |
| A small shared test helper under `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/` or `Lazuar.TestSupport` | `NoSstBilling` / `SstBilling` so Quote / Completeness / ChosenPrice / SubscriptionBillingAmount do not each invent a DTO |
| `MarkCheckoutAsPaidOfflineCommandHandler` constructor | drop `= null` so omitted billing is a compile error (align with 181). Only after every `new MarkCheckoutAsPaidOfflineCommandHandler` site is updated |
| `InitiateCheckoutCommandHandler` + `CreateInitiateHandler` | issue **181** (open). Different slice. Completeness initiate tests will break the same way if 181 flips `MerchantHasSstAsync` callers without stubs |

### Must not change for this slice

| File | Why |
| --- | --- |
| `apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs` | 167 fail-closed is correct |
| `apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs` | SST call sites and two-arg CRM call are correct |
| `apps/lazuar-api/Modules/CRM/Contracts/ICrmQueryService.cs` | 165 already landed |
| `apps/lazuar-api/Modules/CRM/Infrastructure/CrmQueryService.cs` | 165 already landed |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs` | org-scoped lookup already landed |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/QuoteOfflineSstTests.cs` | already injects registered SST |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SubscriptionBillingAmountTests.cs` | already pins null-billing throw and empty-SST = 100 |
| Issue markdown under `issues/167-…`, `issues/165-…`, `issues/034-…` | status already resolved; this is leftover test composition |

---

## 8. Causal timeline (why these four went red on this HEAD)

| When | Commit | What happened to this slice |
| --- | --- | --- |
| 2026-08-17 15:06 | `f1f7ba03` issue **034** | Handler gained `IBillingQueryService? = null` and began calling `MerchantHasSstAsync`. Null billing meant **no SST**. Completeness tests kept passing. `QuoteOfflineSstTests` added as the SST-positive pin. |
| 2026-08-18 09:48 | `8a872da9` | `GetCheckoutSessionByIdAsync(org, id)` etc. Completeness mocks updated. Not the current red. |
| 2026-08-18 09:51 | `42b7ad37` issue **165** | `GetClientProfileAsync(org, profile)`. Completeness mocks updated to two-arg. Tests still compiled. Not the current red. |
| 2026-08-18 09:55 | `49606466` issue **167** | `MerchantHasSstAsync(null)` now **throws**. Completeness / ChosenPrice mark-paid constructors were not updated. Tests went red. |
| HEAD `4531f210` | `fix/180-unify-outbox-inbox` | Outbox/inbox unification. No further change to this handler or these four tests. Failure is inherited from 167 + leftover three-arg constructors. |

Issue **034** write-up already predicted the exact completeness asserts (`300` for qty 3 at 100; custom raw sum) and said “No billing fake with an SST number is injected into those handlers.” After 034 that omission was fail-**open** (undercharge). After 167 the same omission is fail-**closed** (throw). The tests still have no fake. That is the whole bug.

---

## 9. Handle-path map (for the implementer)

```
Handle(org, sessionId)
  GetCheckoutSessionByIdAsync(org, sessionId)     // mocked → session
  session.OrganizationId == org                   // true
  TryComplete()                                   // OPEN → COMPLETED (in memory)
  GetClientProfileAsync(org, clientId)            // two-arg mock → DTO   ← 165 already OK
  if ProductId
      GetProductByIdAsync(org, productId)         // mocked → product
      coupon? GetCouponByIdAsync(org, couponId)
      MerchantHasSstAsync(_billingQueryService, org)
          _billingQueryService == null            // ← TODAY: throw 167
          else GetBillingProfileAsync(org)
               Sst_registration_number whitespace? false : true
      GrossBreakdown(unitNet, qty, product.Sst*, merchantHasSst)
      one_time → Order(total, qty) + OrderCompleted
      else     → Subscription + Start(reminderOnly: true) + SubscriptionActivated
      tx log + maybe ManualSubscriberEnrolled(B2B = session.IsB2b || product.RequiresTaxId)
      SaveChanges
  else if AdHocLineItems
      MerchantHasSstAsync(...)                    // ← custom test throw 167
      CustomQuoteBreakdown(sum, merchantHasSst)   // 250 or 270
      tx log + maybe ManualSubscriberEnrolled(ProductId Empty)
      SaveChanges
```

Production (MediatR + `AddScoped<IBillingQueryService, BillingQueryService>()`) takes the non-null branch. These four tests take the null branch.

---

## 10. Reproduction evidence (verbatim)

Assigned filter, HEAD `4531f210`:

```text
NUnit3TestExecutor discovered 4 of 4 NUnit test cases
Failed MarkCheckoutAsPaidOffline_CustomSession_CompletesWithoutSubscription
  System.InvalidOperationException : IBillingQueryService is required to decide SST; refusing to undercharge.
  MerchantHasSstAsync :97 → HandleCustomSessionAsync :195 → Handle :63 → test :367

Failed MarkCheckoutAsPaidOffline_OneTime_Qty3_WritesLineTotalOrder
  System.InvalidOperationException : IBillingQueryService is required to decide SST; refusing to undercharge.
  MerchantHasSstAsync :97 → HandleProductSessionAsync :97 → Handle :56 → test :1340

Failed MarkCheckoutAsPaidOffline_ProductRequiresTaxId_PublishesB2b
  System.InvalidOperationException : IBillingQueryService is required to decide SST; refusing to undercharge.
  MerchantHasSstAsync :97 → HandleProductSessionAsync :97 → Handle :56 → test :331

Failed MarkCheckoutAsPaidOffline_ProductSession_CreatesActiveSubscription_AndTxLog
  System.InvalidOperationException : IBillingQueryService is required to decide SST; refusing to undercharge.
  MerchantHasSstAsync :97 → HandleProductSessionAsync :97 → Handle :56 → test :295

Test Run Failed.
Total tests: 4
     Failed: 4
```

---

## 11. Bottom line

All four assigned tests live in **`CommerceProductCompletenessTests.cs`**. All four compile. All four fail at **`SubscriptionBillingAmount.MerchantHasSstAsync` line 97** with **`IBillingQueryService is required to decide SST; refusing to undercharge.`**

| Question | Answer |
| --- | --- |
| SST throw? | **Yes.** Null `_billingQueryService` after 167. |
| CRM arity? | **No.** Two-arg mocks already in place from 165. CRM runs successfully before SST. |
| Session org lookup? | **No.** Mocked `GetCheckoutSessionByIdAsync(org, id)` returns the session; orgs match. |
| Product fix? | **No.** Keep 167 fail-closed. Keep handler SST. |
| Test fix? | **Yes.** `new MarkCheckoutAsPaidOfflineCommandHandler(repository, eventBus, crm, NoSstBilling(orgId))`. |

Also patch `ChosenPriceDiscountTests.MarkPaid_YearlyTenPercentCoupon_BooksChosenRowNotCatalogPrice` when touching constructors, or it remains a twin failure.
