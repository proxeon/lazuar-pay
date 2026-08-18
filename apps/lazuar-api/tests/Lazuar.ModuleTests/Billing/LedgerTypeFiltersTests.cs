using FluentAssertions;
using Modules.Billing.Domain;
using Modules.Billing.Infrastructure.Services;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing;

[TestFixture]
public class LedgerTypeFiltersTests
{
    [Test]
    public void Reversals_IncludeChargebackAndDispute_NotSales()
    {
        LedgerTypeFilters.Matches("reversals", LedgerReferenceTypes.GatewayRefund).Should().BeTrue();
        LedgerTypeFilters.Matches("reversals", LedgerReferenceTypes.LhdnCancellation).Should().BeTrue();
        LedgerTypeFilters.Matches("reversals", LedgerReferenceTypes.SystemCreditChargeback).Should().BeTrue();
        LedgerTypeFilters.Matches("reversals", LedgerReferenceTypes.GatewayDispute).Should().BeTrue();
        LedgerTypeFilters.Matches("reversals", LedgerReferenceTypes.SystemSaasFeeReverse).Should().BeTrue();
        LedgerTypeFilters.Matches("reversals", LedgerReferenceTypes.GatewayPayment).Should().BeFalse();
        LedgerTypeFilters.Matches("reversals", LedgerReferenceTypes.SystemSaasFee).Should().BeFalse();
    }

    [Test]
    public void Sales_OnlyPaymentAndManualEnrollment()
    {
        LedgerTypeFilters.Matches("sales", LedgerReferenceTypes.GatewayPayment).Should().BeTrue();
        LedgerTypeFilters.Matches("sales", LedgerReferenceTypes.ManualEnrollment).Should().BeTrue();
        LedgerTypeFilters.Matches("sales", LedgerReferenceTypes.SystemCreditChargeback).Should().BeFalse();
        LedgerTypeFilters.Matches("sales", LedgerReferenceTypes.SystemSaasFee).Should().BeFalse();
        LedgerTypeFilters.Matches("sales", LedgerReferenceTypes.SystemCreditTopup).Should().BeFalse();
        LedgerTypeFilters.Matches("sales", LedgerReferenceTypes.CommissionAccrued).Should().BeFalse();
        LedgerTypeFilters.Matches("sales", LedgerReferenceTypes.ZeroAmountCheckout).Should().BeFalse();
        LedgerTypeFilters.Matches("sales", LedgerReferenceTypes.GatewayRefund).Should().BeFalse();
    }
}
