using FluentAssertions;
using Modules.Payments.Contracts;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class PaymentGatewayCapabilitiesTests
{
    [TestCase("STRIPE", true)]
    [TestCase("stripe", true)]
    [TestCase(" CHIP ", true)]
    [TestCase("BILLPLZ", false)]
    [TestCase("RAZORPAY", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    [TestCase("UNKNOWN", false)]
    public void SupportsOffSession_OnlyStripeAndChip(string? gateway, bool expected)
    {
        PaymentGatewayCapabilities.SupportsOffSession(gateway).Should().Be(expected);
        PaymentGatewayCapabilities.IsReminderOnlyGateway(gateway).Should().Be(!expected);
    }

    [TestCase("STRIPE", true)]
    [TestCase("CHIP", true)]
    [TestCase("RAZORPAY", true)]
    [TestCase("XENDIT", true)]
    [TestCase("BILLPLZ", false)]
    [TestCase(null, false)]
    [TestCase("", false)]
    public void SupportsApiRefund_StripeChipRazorpay(string? gateway, bool expected)
    {
        PaymentGatewayCapabilities.SupportsApiRefund(gateway).Should().Be(expected);
    }

    [TestCase("BILLPLZ", true)]
    [TestCase("OFFLINE", true)]
    [TestCase("BANK_TRANSFER", true)]
    [TestCase("CASH", true)]
    [TestCase("MANUAL_OFFLINE", true)]
    [TestCase("COMPED", true)]
    [TestCase("", true)]
    [TestCase(null, true)]
    [TestCase("STRIPE", false)]
    [TestCase("CHIP", false)]
    [TestCase("RAZORPAY", false)]
    public void RequiresMarkRefunded_OfflineAndBillplz(string? gateway, bool expected)
    {
        PaymentGatewayCapabilities.RequiresMarkRefunded(gateway).Should().Be(expected);
    }

    [Test]
    public void Xendit_IsReminderOnly_AndHostsWallets()
    {
        PaymentGatewayCapabilities.SupportsOffSession("XENDIT").Should().BeFalse();
        PaymentGatewayCapabilities.SupportsEmandate("XENDIT").Should().BeFalse();
        PaymentGatewayCapabilities.SupportsDuitNowQr("XENDIT").Should().BeTrue();
        PaymentGatewayCapabilities.SupportsHostedWallet("XENDIT", "GRABPAY").Should().BeTrue();
        PaymentGatewayCapabilities.SupportsHostedWallet("BILLPLZ", "GRABPAY").Should().BeFalse();
    }
}
