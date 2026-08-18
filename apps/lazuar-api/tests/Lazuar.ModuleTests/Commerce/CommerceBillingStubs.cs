using Lazuar.ApiTypes;
using Modules.Billing.Contracts;
using NSubstitute;

namespace Lazuar.ModuleTests.Commerce;

/// <summary>
/// Shared billing stubs for Commerce ModuleTests that construct handlers with optional
/// <see cref="IBillingQueryService"/>.
/// </summary>
internal static class CommerceBillingStubs
{
    /// <summary>
    /// Composed billing, merchant not SST-registered.
    /// Same contract as BillingEngineJobTests.SetUp / SubscriptionBillingAmountTests.Gross_NoSst_Is100.
    /// </summary>
    /// <remarks>
    /// Issue 167: MerchantHasSstAsync fail-closes when IBillingQueryService is null
    /// ("refusing to undercharge"). Tests that are not about SST must pass this stub so
    /// SST evaluation runs and returns false. Do not reuse QuoteOfflineSstTests registered
    /// SST (W10-…) — that would change net money asserts (250→270, 500→540, 100→108).
    /// </remarks>
    public static IBillingQueryService NoSstBilling()
    {
        var billing = Substitute.For<IBillingQueryService>();
        billing.GetBillingProfileAsync(Arg.Any<Guid>()).Returns((TenantBillingProfileDto?)null);
        return billing;
    }
}
